using System.Collections;
using System.Collections.Generic;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Eating a proper meal (hunger ≥ 15) within reach of a table makes you WELL FED for <see cref="hours"/>
    /// game hours (longer when seated): hunger and thirst drop slower and stamina comes back faster. The meal is laid on
    /// the table (2026-10-23): a plate at the place nearest you with the food heaped on it, steaming when it is cooked,
    /// eaten down to crumbs; the empty plate stays until it is cleared ([E] at the table) or a few hours pass.</summary>
    public class DiningTable : MonoBehaviour, IInteractable
    {
        public float hours = 3f;
        public const float PlateHours = 3f;
        readonly List<(GameObject go, float until)> plates = new List<(GameObject, float)>();
        static Mesh plateMesh, crumbsMesh;
        static readonly Dictionary<int, Mesh> heaps = new Dictionary<int, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { plateMesh = null; crumbsMesh = null; heaps.Clear(); }

        /// <summary>Plates on the table (tests).</summary>
        public int Plates { get { int n = 0; foreach (var p in plates) if (p.go) n++; return n; } }

        /// <summary>Longest well-fed time offered by a table within 1.2 m of <paramref name="p"/> (0 = none).</summary>
        public static float Near(Vector3 p) => Nearest(p, out var t) ? t.hours : 0f;

        /// <summary>The table within reach of <paramref name="p"/> (1.2 m from its top) that gives the longest meal.</summary>
        public static bool Nearest(Vector3 p, out DiningTable best)
        {
            best = null;
            foreach (var pl in Placeable.All)
            {
                if (!pl || (pl.transform.position - p).sqrMagnitude > 16f || !pl.TryGetComponent<DiningTable>(out var t)) continue;
                if (pl.TryGetComponent<Collider>(out var c) && c.bounds.SqrDistance(p) > 1.44f) continue;
                if (!best || t.hours > best.hours) best = t;
            }
            return best;
        }

        public string Prompt(MadMax.Game.WastelandGame g) => Plates > 0 ? "[E] CLEAR THE PLATES" : "EAT A MEAL HERE: WELL FED";

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (Plates == 0) { g.Toast("EAT A PROPER MEAL WITHIN REACH OF THE TABLE TO BE WELL FED"); return; }
            Clear();
            MadMax.Audio.Sfx.Play("pickup", transform.position + Vector3.up, 0.4f, 1.3f, 8f);
            g.Toast("CLEARED THE TABLE");
        }

        public void Clear()
        {
            foreach (var p in plates) if (p.go) Destroy(p.go);
            plates.Clear();
        }

        /// <summary>Lay <paramref name="food"/> at the place nearest <paramref name="eater"/> and eat it down over
        /// <paramref name="seconds"/>.</summary>
        public void Serve(Vector3 eater, string food, float seconds = 1.6f)
        {
            var l = transform.InverseTransformPoint(eater);
            float s = VoxelMesher.DefaultSize;
            var place = new Vector3(Mathf.Clamp(l.x, -12f * s, 12f * s), 10.5f * s, Mathf.Sign(l.z == 0f ? 1f : l.z) * 4.5f * s);
            foreach (var p in plates)
                if (p.go && (p.go.transform.localPosition - place).sqrMagnitude < 0.09f) { Destroy(p.go); }
            plates.RemoveAll(p => !p.go);
            var go = new GameObject("Plate", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = place;
            var mat = GetComponent<MeshRenderer>() ? GetComponent<MeshRenderer>().sharedMaterial : null;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.GetComponent<MeshFilter>().sharedMesh = PlateMesh();
            var heap = new GameObject("Food", typeof(MeshFilter), typeof(MeshRenderer));
            heap.transform.SetParent(go.transform, false);
            heap.GetComponent<MeshRenderer>().sharedMaterial = mat;
            heap.GetComponent<MeshFilter>().sharedMesh = HeapMesh(food);
            plates.Add((go, DayNight.TotalDays * 24f + PlateHours));
            StartCoroutine(EatDown(go, heap, food, seconds));
        }

        static bool Hot(string food) => food != null && (food.Contains("cooked") || food.Contains("stew") || food.Contains("soup") || food.Contains("roast") || food.StartsWith("fire_") || food.StartsWith("range_"));

        IEnumerator EatDown(GameObject plate, GameObject heap, string food, float seconds)
        {
            float t = 0f;
            bool hot = Hot(food);
            while (t < seconds && heap)
            {
                t += Time.deltaTime;
                heap.transform.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.25f, t / seconds), 1f);
                if (hot && Random.value < Time.deltaTime * 6f)
                    Fx.Smoke(heap.transform.position + Vector3.up * 0.06f, Vector3.up * 0.25f, 0.05f, new Color(0.92f, 0.92f, 0.9f, 0.5f), 1.4f);
                yield return null;
            }
            if (heap) Destroy(heap);
            if (!plate) yield break;
            var crumbs = new GameObject("Crumbs", typeof(MeshFilter), typeof(MeshRenderer));
            crumbs.transform.SetParent(plate.transform, false);
            crumbs.GetComponent<MeshRenderer>().sharedMaterial = plate.GetComponent<MeshRenderer>().sharedMaterial;
            crumbs.GetComponent<MeshFilter>().sharedMesh = CrumbsMesh();
        }

        void Update()
        {
            if (plates.Count == 0) return;
            float now = DayNight.TotalDays * 24f;
            for (int i = plates.Count - 1; i >= 0; i--)
                if (!plates[i].go || now > plates[i].until) { if (plates[i].go) Destroy(plates[i].go); plates.RemoveAt(i); }
        }

        // a plate: a 5-voxel disc with a raised rim (in the table's local frame, its own origin on the top)
        static Mesh PlateMesh()
        {
            if (plateMesh) return plateMesh;
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Stone);
            for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++)
                {
                    int r2 = x * x + z * z;
                    if (r2 > 5) continue;
                    g.Set(x, 0, z, Pal.Ramp(Pal.Cream, r2 >= 4 ? 2 : 4));
                }
            return plateMesh = VoxelMesher.Build(g, "Table_plate");
        }

        static Mesh CrumbsMesh()
        {
            if (crumbsMesh) return crumbsMesh;
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Wood);
            g.Set(-1, 1, 0, Pal.Ramp(Pal.Ochre, 2)); g.Set(1, 1, 1, Pal.Ramp(Pal.Ochre, 1)); g.Set(0, 1, -1, Pal.Ramp(Pal.Rust, 2));
            return crumbsMesh = VoxelMesher.Build(g, "Table_crumbs");
        }

        /// <summary>The food heaped on the plate, coloured by what it is (meat, bread, greens, stew...).</summary>
        static Mesh HeapMesh(string food)
        {
            var ramp = food == null ? Pal.Ochre : food.Contains("meat") || food.Contains("jerky") || food.Contains("fish") ? Pal.Rust
                : food.Contains("bread") || food.Contains("potato") || food.Contains("corn") ? Pal.Ochre
                : food.Contains("stew") || food.Contains("soup") || food.Contains("bean") ? Pal.Bronze
                : food.Contains("apple") || food.Contains("tomato") ? Pal.Crimson
                : Pal.Olive;
            int key = ramp.GetHashCode();
            if (heaps.TryGetValue(key, out var m) && m) return m;
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Wood);
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) g.Set(x, 1, z, Pal.Ramp(ramp, 1 + ((x + z) & 1)));
            g.Set(0, 2, 0, Pal.Ramp(ramp, 3));
            g.Bevel();
            return heaps[key] = VoxelMesher.Build(g, "Table_food");
        }
    }
}
