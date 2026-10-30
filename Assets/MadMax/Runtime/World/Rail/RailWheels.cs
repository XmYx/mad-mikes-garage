using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The wheelsets of a <see cref="RailCar"/> (2026-10-30): separate meshes turning by the distance the car
    /// rolls (spokes and a balance weight show it). Hard braking at speed throws sparks off the brake blocks and the
    /// rail; in a tight curve the flanges grind the rail head — a squeal (<c>rail_flange</c>) from the leading wheels,
    /// sparks past <see cref="Train.FlangeAt"/> m/s² sideways. Added by <see cref="RailCar.Create"/>.</summary>
    public class RailWheels : MonoBehaviour
    {
        RailCar car;
        readonly List<Transform> sets = new List<Transform>();
        readonly List<float> radius = new List<float>();
        float rolled, sparkAt;
        /// <summary>Distance rolled (m, signed along the car).</summary>
        public float Rolled => rolled;
        /// <summary>Sideways acceleration in the curve (m/s²).</summary>
        public float Lateral { get; private set; }
        public int Count => sets.Count;
        /// <summary>Spark bursts thrown (brakes or flanges) and frames the flanges squealed (tests).</summary>
        public static int SparkBursts, FlangeFrames;

        static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { meshes.Clear(); SparkBursts = FlangeFrames = 0; }

        static Mesh SetMesh(float r)
        {
            int key = Mathf.RoundToInt(r * 100f);
            if (meshes.TryGetValue(key, out var m) && m) return m;
            m = MadMax.Voxel.VoxelMesher.ToMesh(TrainDesigns.Wheelset(r).mesh, "Rail_Wheelset" + key);
            m.RecalculateBounds();
            meshes[key] = m;
            return m;
        }

        public static RailWheels Add(RailCar car, Material mat)
        {
            var w = car.gameObject.AddComponent<RailWheels>();
            w.car = car;
            foreach (var (at, r) in TrainDesigns.Axles(car.def))
            {
                var go = new GameObject("Wheelset", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(car.transform, false);
                go.transform.localPosition = at;
                go.GetComponent<MeshFilter>().sharedMesh = SetMesh(r);
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                w.sets.Add(go.transform); w.radius.Add(r);
            }
            return w;
        }

        /// <summary>Turn of the first wheelset about its axle, degrees (tests).</summary>
        public float Turn => sets.Count > 0 ? Mathf.Repeat(rolled / radius[0] * Mathf.Rad2Deg, 360f) : 0f;

        void LateUpdate()
        {
            if (!car) return;
            float dt = Time.deltaTime;
            float v = Vector3.Dot(car.Velocity, transform.forward);
            rolled += v * dt;
            for (int i = 0; i < sets.Count; i++) sets[i].localRotation = Quaternion.Euler(Mathf.Repeat(rolled / radius[i] * Mathf.Rad2Deg, 360f), 0f, 0f);
            var t = car.train;
            Lateral = t ? t.PullOf(t.cars.IndexOf(car)) : 0f;
            float sp = Mathf.Abs(v);
            if (!t || sets.Count == 0 || dt <= 0f || sp < 2.5f || !MadMax.Building.PieceBits.Near(transform.position, 160f)) return;
            // the flanges in a curve: the leading wheelset of the car takes it
            if (Lateral > Train.FlangeAt * 0.6f)
            {
                FlangeFrames++;
                float k = Mathf.Clamp01((Lateral - Train.FlangeAt * 0.6f) / Train.FlangeAt);
                MadMax.Audio.Sfx.Loop(this, "rail_flange", 0.25f + 0.5f * k, 0.9f + 0.25f * k + sp * 0.01f, 110f);
                if (Lateral > Train.FlangeAt && Time.time >= sparkAt)
                {
                    sparkAt = Time.time + Mathf.Lerp(0.25f, 0.06f, k);
                    var lead = sets[v >= 0f ? sets.Count - 1 : 0];
                    float side = t.CurvatureAt(t.CarCentre(t.cars.IndexOf(car)), car.def.Half) * t.speed * v >= 0f ? -1f : 1f;                                   // the outer rail
                    var p = lead.position + transform.right * side * 0.7f - Vector3.up * (radius[0] - 0.02f);
                    Fx.Sparks(p, -transform.forward * Mathf.Sign(v) + Vector3.up * 0.4f, 3 + Mathf.RoundToInt(k * 5f), new Color(1f, 0.78f, 0.4f));
                    SparkBursts++;
                }
            }
            // hard braking: sparks off the blocks of a wheelset or two
            if (t.BrakingHard && sp > 4f && Time.time >= sparkAt)
            {
                sparkAt = Time.time + Random.Range(0.08f, 0.2f);
                var w = sets[Random.Range(0, sets.Count)];
                float s = Random.value < 0.5f ? -1f : 1f;
                var p = w.position + transform.right * s * 0.72f - Vector3.up * (radius[0] * 0.8f);
                Fx.Sparks(p, -transform.forward * Mathf.Sign(v) * 1.5f + Vector3.up * 0.3f, 2 + Mathf.RoundToInt(Mathf.Min(sp, 14f) * 0.3f), new Color(1f, 0.6f, 0.25f));
                SparkBursts++;
            }
        }
    }
}
