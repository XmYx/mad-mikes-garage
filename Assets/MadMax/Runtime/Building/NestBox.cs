using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Eggs in the straw (2026-10-26): the nest box shows the eggs it holds (two to a compartment, white and
    /// brown, from its <see cref="Container"/>), and a hen that comes to lay sits in a free compartment
    /// (<see cref="Claim"/>) before her egg appears with a cackle. Laying itself is <c>Animal.Home</c>.</summary>
    public class NestBox : MonoBehaviour
    {
        public static readonly List<NestBox> All = new List<NestBox>();
        static Mesh eggMesh, brownMesh;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { All.Clear(); eggMesh = null; brownMesh = null; }
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public const float Reach = 12f, SitHeight = 0.1f;
        const int Slots = 3, PerSlot = 2;
        static readonly float[] SlotX = { -0.4f, 0f, 0.4f };

        Container box;
        readonly List<Transform> eggs = new List<Transform>();
        readonly Object[] sitters = new Object[Slots];
        int shown = -1;
        float pollT;

        /// <summary>Eggs drawn in the box (tests).</summary>
        public int Shown => Mathf.Max(0, shown);
        /// <summary>Eggs it holds.</summary>
        public int Eggs => box ? box.inventory.GetItem("food_egg") : 0;

        void Awake() => box = GetComponent<Container>();

        /// <summary>The nearest nest box within <paramref name="radius"/> of <paramref name="p"/>.</summary>
        public static NestBox Near(Vector3 p, float radius)
        {
            NestBox best = null; float bd = radius * radius;
            foreach (var n in All)
            {
                if (!n) continue;
                float d = (n.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        /// <summary>A free compartment for <paramref name="hen"/> (−1: all taken); she keeps it until <see cref="Free"/>.</summary>
        public int Claim(Object hen)
        {
            for (int i = 0; i < Slots; i++) if (sitters[i] == hen) return i;
            for (int i = 0; i < Slots; i++) if (!sitters[i]) { sitters[i] = hen; return i; }
            return -1;
        }

        public void Free(Object hen) { for (int i = 0; i < Slots; i++) if (sitters[i] == hen) sitters[i] = null; }

        /// <summary>Where a hen sits in compartment <paramref name="i"/> (world, on the straw).</summary>
        public Vector3 SitSpot(int i) => transform.TransformPoint(new Vector3(SlotX[Mathf.Clamp(i, 0, Slots - 1)], 0f, 0.02f));
        /// <summary>The spot in front of the box a hen walks to before hopping in.</summary>
        public Vector3 Approach(int i) => transform.TransformPoint(new Vector3(SlotX[Mathf.Clamp(i, 0, Slots - 1)], 0f, 0.55f));

        /// <summary>A hen lays: the egg goes in the box with a rustle of straw.</summary>
        public void Lay(int n)
        {
            if (!box || n <= 0) return;
            box.inventory.AddItem("food_egg", n);
            MadMax.Audio.Sfx.Play("scratch", transform.position + Vector3.up * 0.2f, 0.25f, 1.5f, 8f);
            pollT = 0f;
        }

        void Update()
        {
            if (Time.time < pollT) return;
            pollT = Time.time + 0.5f;
            int want = Mathf.Min(Slots * PerSlot, Eggs);
            if (want != shown) Show(want);
        }

        void Show(int n)
        {
            shown = n;
            var mat = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDShapes.Solid : GetComponent<MeshRenderer>() ? GetComponent<MeshRenderer>().sharedMaterial : null;
            while (eggs.Count < n)
            {
                int i = eggs.Count, slot = i % Slots, two = i / Slots;
                var go = new GameObject("Egg" + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(SlotX[slot] + (two == 0 ? -0.05f : 0.06f), 0.12f, two == 0 ? -0.02f : 0.05f);
                go.transform.localRotation = Quaternion.Euler(70f + i * 13f, i * 47f, 0f);               // lying on their sides in the straw
                go.transform.localScale = new Vector3(0.55f, 0.72f, 0.55f);
                go.GetComponent<MeshFilter>().sharedMesh = EggMesh((i * 7) % 3 == 1);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                eggs.Add(go.transform);
            }
            for (int i = 0; i < eggs.Count; i++) eggs[i].gameObject.SetActive(i < n);
        }

        static Mesh EggMesh(bool brown)
        {
            var have = brown ? brownMesh : eggMesh;
            if (have) return have;
            var g = new VoxelGrid();
            g.Set(0, 0, 0, Pal.Solid(brown ? Pal.Sand[4] : Pal.Cream[4]));
            var m = VoxelMesher.Build(g, brown ? "Egg_brown" : "Egg");
            if (brown) brownMesh = m; else eggMesh = m;
            return m;
        }
    }
}
