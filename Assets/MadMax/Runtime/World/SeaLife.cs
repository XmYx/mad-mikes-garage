using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Life in the water around the player (user additions): schools of voxel fish (silver sardines, blue
    /// mackerel, striped perch in lakes, orange reef fish in warm seas) that wheel between the bottom and the surface
    /// and scatter from divers and hulls, now and then a fish jumping; jellyfish drifting and pulsing in the sea (they
    /// sting swimmers and divers); crabs scuttling on sea beaches (animals: <see cref="MadMax.Animals.AnimalLibrary"/>
    /// "crab"). Pooled objects, all within 45 m.</summary>
    public class SeaLife : MonoBehaviour
    {
        const float Range = 45f;
        class Fish { public Transform t; public Vector3 off, vel; }
        class School { public Vector3 centre, drift; public List<Fish> fish = new List<Fish>(); public float floor, top; public bool alive; }
        class Jelly { public Transform t; public Vector3 drift; public float phase; }
        readonly List<School> schools = new List<School>();
        readonly List<Jelly> jellies = new List<Jelly>();
        readonly List<MadMax.Animals.Animal> crabs = new List<MadMax.Animals.Animal>();
        static Mesh[] fishMesh; static Mesh jellyMesh;
        Material mat;
        float scan, stingT;

        static Mesh FishMesh(int kind)
        {
            fishMesh ??= new Mesh[4];
            if (fishMesh[kind]) return fishMesh[kind];
            var g = new VoxelGrid();
            var body = kind == 0 ? Pal.Chrome : kind == 1 ? Pal.Navy : kind == 2 ? Pal.Olive : Pal.Ochre;
            g.Box(-1, 0, -2, 1, 1, 2, p => (kind == 2 && (p.z & 1) == 0) ? Pal.Black[1] : body[p.y == 1 ? 3 : 2]);
            g.Box(0, 0, 3, 0, 1, 3, Pal.Solid(body[3]));                                                   // head
            g.Box(0, -1, -4, 0, 2, -3, Pal.Solid(body[1]));                                                 // tail
            g.Set(-1, 1, 2, Pal.Solid(Pal.Black[0])); g.Set(1, 1, 2, Pal.Solid(Pal.Black[0]));
            return fishMesh[kind] = VoxelMesher.Build(g, "SeaFish" + kind, 0.05f);
        }

        static Mesh JellyMesh()
        {
            if (jellyMesh) return jellyMesh;
            var g = new VoxelGrid();
            for (int x = -4; x <= 4; x++) for (int y = 0; y <= 3; y++) for (int z = -4; z <= 4; z++)
                if (x * x + z * z + y * y * 2.5f <= 20f && x * x + z * z + (y - 0.6f) * (y - 0.6f) * 2.5f > 9f) g.Set(x, y, z, Pal.Solid(y >= 2 ? Pal.Pink[3] : Pal.Pink[2]));   // bell
            for (int k = 0; k < 6; k++) { float a = k * Mathf.PI / 3f; int tx = Mathf.RoundToInt(Mathf.Cos(a) * 2f), tz = Mathf.RoundToInt(Mathf.Sin(a) * 2f); g.Box(tx, -6, tz, tx, -1, tz, Pal.Solid(Pal.Pink[1])); }   // tentacles
            return jellyMesh = VoxelMesher.Build(g, "Jellyfish", 0.05f);
        }

        Transform Make(Mesh m, string name)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = m;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void Update()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            var t = DeformableTerrain.Instance;
            if (!g || !g.Player || !t || t.World == null || Application.isBatchMode) return;
            if (!mat) mat = t.worldPropMaterial ? t.worldPropMaterial : g.propMaterial;
            var focus = g.Current ? g.Current.transform.position : g.Player.transform.position;
            float dt = Time.deltaTime;
            if ((scan -= dt) <= 0f) { scan = 1.5f; Populate(t, focus); }
            Swim(t, g, focus, dt);
        }

        void Populate(DeformableTerrain t, Vector3 focus)
        {
            // schools: keep up to five in water deeper than 1.5 m within range; drop the far ones
            for (int s = schools.Count - 1; s >= 0; s--)
                if ((schools[s].centre - focus).sqrMagnitude > Range * Range * 1.6f) { foreach (var f in schools[s].fish) Destroy(f.t.gameObject); schools.RemoveAt(s); }
            for (int tries = 0; tries < 6 && schools.Count < 5; tries++)
            {
                var o = Random.insideUnitCircle * Range;
                float x = focus.x + o.x, z = focus.z + o.y;
                float lvl = t.WaterLevel(x, z);
                if (float.IsNaN(lvl)) continue;
                float bed = t.HeightNoLoad(x, z);
                if (lvl - bed < 1.5f) continue;
                bool sea = t.World.Ocean(x, z);
                float lat = Mathf.Abs(WorldGen.Latitude(z));
                int kind = !sea ? 2 : lat < 25f && Random.value < 0.5f ? 3 : Random.value < 0.6f ? 0 : 1;
                var sc = new School { centre = new Vector3(x, (lvl + bed) * 0.5f, z), floor = bed + 0.4f, top = lvl - 0.35f, drift = Random.insideUnitSphere * 0.6f };
                sc.drift.y = 0f;
                int n = kind == 0 ? Random.Range(12, 20) : Random.Range(5, 11);
                for (int i = 0; i < n; i++)
                {
                    var tr = Make(FishMesh(kind), "Fish");
                    var off = Random.insideUnitSphere * 1.6f; off.y *= 0.4f;
                    sc.fish.Add(new Fish { t = tr, off = off, vel = sc.drift });
                    tr.position = sc.centre + off;
                }
                schools.Add(sc);
            }
            // jellyfish in the sea
            for (int j = jellies.Count - 1; j >= 0; j--) if ((jellies[j].t.position - focus).sqrMagnitude > Range * Range * 1.6f) { Destroy(jellies[j].t.gameObject); jellies.RemoveAt(j); }
            if (jellies.Count < 4 && Random.value < 0.5f)
            {
                var o = Random.insideUnitCircle * Range;
                float x = focus.x + o.x, z = focus.z + o.y;
                float lvl = t.WaterLevel(x, z);
                if (!float.IsNaN(lvl) && t.World.Ocean(x, z) && lvl - t.HeightNoLoad(x, z) > 2f)
                {
                    var tr = Make(JellyMesh(), "Jellyfish");
                    tr.position = new Vector3(x, lvl - Random.Range(0.4f, 1.6f), z);
                    jellies.Add(new Jelly { t = tr, drift = new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f)), phase = Random.value * 6f });
                }
            }
            // crabs on the sea beaches
            crabs.RemoveAll(c => !c);
            for (int c = crabs.Count - 1; c >= 0; c--) if ((crabs[c].transform.position - focus).sqrMagnitude > 70f * 70f) { Destroy(crabs[c].gameObject); crabs.RemoveAt(c); }
            if (crabs.Count < 5 && Random.value < 0.4f)
            {
                var def = MadMax.Animals.AnimalLibrary.Get("crab");
                var o = Random.insideUnitCircle * 35f;
                float x = focus.x + o.x, z = focus.z + o.y;
                var s = t.World.Sample(x, z);
                if (def != null && s.shore > 0.4f && t.World.Ocean(x, z) && s.height > WorldGen.SeaLevel - 0.1f)
                    crabs.Add(MadMax.Animals.Animal.Spawn(def, new Vector3(x, t.HeightNoLoad(x, z), z), Random.Range(0f, 360f), mat, "crab:" + Mathf.RoundToInt(x) + ":" + Mathf.RoundToInt(z)));
            }
        }

        void Swim(DeformableTerrain t, MadMax.Game.WastelandGame g, Vector3 focus, float dt)
        {
            float time = Time.time;
            var player = g.Player.transform.position + Vector3.up;
            foreach (var sc in schools)
            {
                // the school wanders, turns back at the bottom / surface / shallows, flees a diver or a hull
                sc.drift += new Vector3(Mathf.PerlinNoise(time * 0.2f, sc.centre.x) - 0.5f, 0f, Mathf.PerlinNoise(sc.centre.z, time * 0.2f) - 0.5f) * dt * 0.8f;
                var threat = g.Current ? g.Current.transform.position : player;
                var away = sc.centre - threat; away.y = 0f;
                if (away.sqrMagnitude < 7f * 7f) sc.drift += away.normalized * dt * 4f;
                sc.drift = Vector3.ClampMagnitude(sc.drift, 1.6f);
                var next = sc.centre + sc.drift * dt;
                float lvl = t.WaterLevel(next.x, next.z);
                if (float.IsNaN(lvl) || lvl - t.HeightNoLoad(next.x, next.z) < 1.2f) { sc.drift = -sc.drift; continue; }
                sc.centre = next;
                sc.top = lvl - 0.35f; sc.floor = t.HeightNoLoad(next.x, next.z) + 0.4f;
                sc.centre.y = Mathf.Clamp(sc.centre.y + Mathf.Sin(time * 0.3f + sc.floor) * dt * 0.2f, sc.floor + 0.3f, Mathf.Max(sc.floor + 0.3f, sc.top - 0.3f));
                foreach (var f in sc.fish)
                {
                    f.off += (Random.insideUnitSphere * 0.6f - f.off * 0.25f) * dt;
                    var p = sc.centre + f.off;
                    p.y = Mathf.Clamp(p.y, sc.floor, sc.top);
                    var dir = p - f.t.position;
                    if (dir.sqrMagnitude > 1e-5f) f.t.rotation = Quaternion.Slerp(f.t.rotation, Quaternion.LookRotation(dir.normalized + sc.drift * 0.3f + Vector3.forward * 1e-4f), 1f - Mathf.Exp(-6f * dt));
                    f.t.position = Vector3.Lerp(f.t.position, p, 1f - Mathf.Exp(-3f * dt));
                    f.t.localScale = new Vector3(1f, 1f, 1f + Mathf.Sin(time * 12f + f.off.x * 9f) * 0.08f);   // tail beat
                }
                if (Random.value < dt * 0.05f && sc.fish.Count > 0)                                  // one jumps: a splash at the surface
                {
                    var s = sc.fish[Random.Range(0, sc.fish.Count)].t.position;
                    Fx.Foam(new Vector3(s.x, lvl + 0.03f, s.z), Vector3.zero, 0.5f, new Color(0.9f, 0.95f, 0.97f, 0.8f), 1.5f);
                    MadMax.Audio.Sfx.Play("splash", s, 0.25f, Random.Range(1.3f, 1.7f), 25f, 0.5f);
                }
            }
            foreach (var j in jellies)
            {
                j.phase += dt;
                float pulse = Mathf.Max(0f, Mathf.Sin(j.phase * 2.2f));
                j.t.position += (j.drift + Vector3.up * (pulse * 0.15f - 0.06f)) * dt;
                j.t.localScale = new Vector3(1f + pulse * 0.15f, 1f - pulse * 0.12f, 1f + pulse * 0.15f);
                if ((stingT -= dt) <= 0f && !g.Current && (j.t.position - player).sqrMagnitude < 1.1f * 1.1f && (g.Player.Swimming || g.Player.Diving))
                {
                    stingT = 2f;
                    g.Vitals.Hurt(g.Wearing("dive_suit") ? 1f : 5f, "STUNG");
                    g.Toast("JELLYFISH STING!");
                }
            }
        }
    }
}
