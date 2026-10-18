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

        /// <summary>Fresh bedding (2026-10-17): a sack of sawdust spread on the stall floor when the manger is filled.
        /// It lasts <see cref="BeddingDays"/> days, browning as it goes; while it is down the dung shovelled from the pen
        /// comes out half again (soiled bedding is the better compost). Not saved: bedding is a chore, not a fixture.</summary>
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
            return true;
        }

        void Update()
        {
            if (bedding && Mathf.Abs(Bedding - shownBedding) > 0.02f) ShowBedding();
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
