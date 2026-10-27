using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Stable (depth stage E): an open-fronted shed with two stalls and a manger (the piece's
    /// <see cref="MadMax.Animals.Trough"/>). Animals standing inside get their stamina back fast, heal a limp twice as
    /// quickly and stay calm through gunfire and horns; a horse that has rested is rested for half a day (winds more
    /// slowly). See <see cref="MadMax.Animals.Animal.InStable"/>.</summary>
    public class Stable : MonoBehaviour
    {
        public static readonly List<Stable> All = new List<Stable>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        /// <summary>The stalls (local box: centre and half size, m).</summary>
        public Vector3 centre = new Vector3(0f, 1.2f, 0f), half = new Vector3(2f, 1.5f, 1.55f);

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Flies over a dirty stable (2026-10-18): no bedding down (or trodden brown) and dung piled up in the pen.</summary>
        void Start() => MadMax.World.Swarm.Attach(gameObject, MadMax.World.Swarm.Kind.Flies, centre + new Vector3(0f, -0.6f, 0f), Mathf.Min(half.x, half.z) * 0.8f, 8,
            () => MadMax.World.DayNight.Darkness > 0.6f || MadMax.World.Weather.TemperatureAt(transform.position.z) < 4f ? 0f : Filth);

        /// <summary>How dirty the stalls are (0..1): manure waiting in the pen, worse without fresh bedding over it.</summary>
        public float Filth
        {
            get
            {
                if (!manger) TryGetComponent(out manger);
                int dung = manger ? manger.manure : 0;
                if (dung < DirtyManure) return 0f;
                return Mathf.Clamp01((dung - DirtyManure + 4) / 14f) * (Bedding > 0.35f ? 0.25f : 1f);
            }
        }
        /// <summary>Manure in the pen from which the stalls start to draw flies.</summary>
        public const int DirtyManure = 8;
        MadMax.Animals.Trough manger;

        /// <summary>Restore bedding spread at <paramref name="at"/> (world days) from a save.</summary>
        public void Restore(float at)
        {
            beddedAt = at;
            if (Bedded) ShowBedding();
        }

        /// <summary>A lying spot on the bedding for animal <paramref name="i"/> (world space; spread across the stalls).</summary>
        public Vector3 BedSpot(int i)
        {
            float u = (i % 4 + 0.5f) / 4f, v = (i / 4 % 2 + 0.5f) / 2f;
            return transform.TransformPoint(centre + new Vector3(Mathf.Lerp(-half.x, half.x, u) * 0.75f, -centre.y + 0.05f, Mathf.Lerp(-half.z, half.z, v) * 0.6f));
        }

        /// <summary>World days when a door dog last barked here (a night of barking puts the hens off laying).</summary>
        public float disturbedAt = -9f;

        /// <summary>Roost bar height above the stall floor (m).</summary>
        public const float RoostHeight = 0.85f;

        /// <summary>A place on the roost bar along the back wall for hen <paramref name="i"/> (world space, on the bar).</summary>
        public Vector3 RoostSpot(int i)
        {
            float u = (i % 6 + 0.5f) / 6f;
            return transform.TransformPoint(new Vector3(centre.x + Mathf.Lerp(-half.x, half.x, u) * 0.8f, RoostHeight, centre.z - half.z * 0.72f));
        }

        /// <summary>Where a yard dog lies across the stable mouth (world space, on the floor just outside the open front, +Z).</summary>
        public Vector3 DoorSpot => transform.TransformPoint(new Vector3(centre.x, 0.02f, centre.z + half.z + 0.55f));
        /// <summary>The way out of the stable mouth (world).</summary>
        public Vector3 Outward => transform.forward;

        /// <summary>The nearest stable (bedded or not) within <paramref name="radius"/> of <paramref name="p"/>.</summary>
        public static Stable Near(Vector3 p, float radius)
        {
            Stable best = null; float bd = radius * radius;
            foreach (var s in All) { if (!s) continue; float d = (s.transform.position - p).sqrMagnitude; if (d < bd) { bd = d; best = s; } }
            return best;
        }

        Transform roost;
        /// <summary>The roost bar: a pole along the back wall, put up the first time a hen comes to roost.</summary>
        public void ShowRoost()
        {
            if (roost) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Roost";
            Destroy(go.GetComponent<Collider>());
            roost = go.transform; roost.SetParent(transform, false);
            roost.localPosition = new Vector3(centre.x, RoostHeight - 0.04f, centre.z - half.z * 0.72f);
            roost.localRotation = Quaternion.Euler(0f, 0f, 90f);
            roost.localScale = new Vector3(0.06f, half.x * 0.85f, 0.06f);
            var mat = new Material(MadMax.World.Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", (Color)MadMax.Voxel.Pal.Wood[2]);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>The nearest bedded stable within <paramref name="radius"/> of <paramref name="p"/> (null: none).</summary>
        public static Stable BeddedNear(Vector3 p, float radius)
        {
            Stable best = null; float bd = radius * radius;
            foreach (var s in All) { if (!s || !s.Bedded) continue; float d = (s.transform.position - p).sqrMagnitude; if (d < bd) { bd = d; best = s; } }
            return best;
        }

        /// <summary>Fresh bedding (2026-10-17): a sack of sawdust spread on the stall floor when the manger is filled.
        /// It lasts <see cref="BeddingDays"/> days, browning as it goes; while it is down the dung shovelled from the pen
        /// comes out half again (soiled bedding is the better compost). Saved with the manger (2026-10-18).</summary>
        public float beddedAt = -99f;
        public const float BeddingDays = 3f;
        public float Bedding => Mathf.Clamp01(1f - (MadMax.World.DayNight.TotalDays - beddedAt) / BeddingDays);
        public bool Bedded => Bedding > 0f;
        Transform bedding;
        Material beddingMat;
        float shownBedding = -1f;

        /// <summary>Spread a sack from <paramref name="inv"/> (only once the old bedding is a day old).</summary>
        public bool Bed(MadMax.Items.Inventory inv)
        {
            if (inv == null || Bedding > 1f - 1f / BeddingDays || !inv.TakeItem(Sawdust.Sack)) return false;
            beddedAt = MadMax.World.DayNight.TotalDays;
            ShowBedding();
            GetComponent<Placeable>()?.Dirty();
            return true;
        }

        void Update()
        {
            if ((bedding || Bedded) && Mathf.Abs(Bedding - shownBedding) > 0.02f) ShowBedding();
        }

        void ShowBedding()
        {
            float b = Bedding;
            shownBedding = b;
            if (b <= 0f) { if (bedding) bedding.gameObject.SetActive(false); return; }
            if (!bedding)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Bedding";
                Destroy(go.GetComponent<Collider>());
                bedding = go.transform; bedding.SetParent(transform, false);
                bedding.localPosition = new Vector3(centre.x, 0.025f, centre.z);
                bedding.localScale = new Vector3(half.x * 1.9f, 0.03f, half.z * 1.8f);
                beddingMat = new Material(MadMax.World.Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = beddingMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            bedding.gameObject.SetActive(true);
            var fresh = Color.Lerp((Color)MadMax.Voxel.Pal.Wood[4], Color.white, 0.3f);
            var trodden = (Color)MadMax.Voxel.Pal.Wood[1];
            beddingMat.SetColor("_BaseColor", Color.Lerp(trodden, fresh, b));
        }

        /// <summary>The stable whose stalls hold <paramref name="p"/> (null: none).</summary>
        public static Stable At(Vector3 p)
        {
            foreach (var s in All) if (s && (s.transform.position - p).sqrMagnitude < 36f && s.Inside(p)) return s;
            return null;
        }

        public bool Inside(Vector3 p)
        {
            var l = transform.InverseTransformPoint(p) - centre;
            return Mathf.Abs(l.x) < half.x && Mathf.Abs(l.y) < half.y && Mathf.Abs(l.z) < half.z;
        }

        /// <summary>Is <paramref name="p"/> inside any stable?</summary>
        public static bool Holds(Vector3 p)
        {
            foreach (var s in All) if (s && (s.transform.position - p).sqrMagnitude < 36f && s.Inside(p)) return true;
            return false;
        }
    }
}
