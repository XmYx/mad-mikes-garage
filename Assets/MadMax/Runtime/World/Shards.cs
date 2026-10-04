using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Litter left where something broke: broken windscreen glass, red / amber / clear lamp lenses, shreds of a
    /// blown tyre. Small flat
    /// scatters of 3.5 cm cubes lying on the ground, pooled (oldest reused first), not saved.</summary>
    public static class Shards
    {
        public enum Kind { Glass, Clear, Red, Amber, Rubber }
        const int Max = 96, Variants = 3;
        static readonly List<GameObject> pool = new List<GameObject>();
        static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        static int next;
        /// <summary>Scatters dropped this session, per kind (tests).</summary>
        public static readonly int[] Dropped = new int[5];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { pool.Clear(); meshes.Clear(); next = 0; System.Array.Clear(Dropped, 0, Dropped.Length); }

        static Color32[] Ramp(Kind k) => k == Kind.Glass ? Pal.Glass : k == Kind.Rubber ? Pal.Tire : k == Kind.Red ? new[] { Pal.TailR, Pal.Rust[2], Pal.TailR } :
            k == Kind.Amber ? new[] { Pal.Amber, Pal.Ochre[3], Pal.Amber } : new[] { Pal.LightW, Pal.Glass[3], Pal.Cream[4] };

        static Mesh MeshFor(Kind k, int variant)
        {
            int key = (int)k * Variants + variant;
            if (meshes.TryGetValue(key, out var m) && m) return m;
            if (MadMax.Rendering.HDBits.On) return meshes[key] = MadMax.Rendering.HDBits.Shards(Ramp(k), k == Kind.Glass ? 30 : 14, (k == Kind.Glass ? 11f : 7f) * 0.035f, 4711 + key * 131);
            var g = new VoxelGrid().Mat((byte)(k == Kind.Rubber ? MadMax.Items.ResourceType.Rubber : MadMax.Items.ResourceType.Glass));
            var r = new System.Random(4711 + key * 131);
            var ramp = Ramp(k);
            int n = k == Kind.Glass ? 26 : k == Kind.Rubber ? 18 : 12;
            for (int i = 0; i < n; i++)
            {
                // denser in the middle, a few pieces skittered further out
                float a = (float)r.NextDouble() * Mathf.PI * 2f, d = Mathf.Pow((float)r.NextDouble(), 1.6f) * (k == Kind.Glass ? 11f : 7f);
                int sx = Mathf.RoundToInt(Mathf.Cos(a) * d), sz = Mathf.RoundToInt(Mathf.Sin(a) * d);
                var col = Pal.Solid(ramp[r.Next(ramp.Length)]);
                g.Set(sx, 0, sz, col);
                if (k == Kind.Rubber) { int len = 1 + r.Next(3); for (int j = 1; j <= len; j++) g.Set(sx + (r.Next(2) == 0 ? j : 0), 0, sz + (r.Next(2) == 0 ? 0 : j), col); }   // strips, not chips
            }
            return meshes[key] = VoxelMesher.Build(g, "Shards", 0.035f);
        }

        /// <summary>Scatter shards on the ground under <paramref name="at"/>.</summary>
        public static void Drop(Vector3 at, Kind kind)
        {
            var t = DeformableTerrain.Instance;
            Dropped[(int)kind]++;
            if (!t || Application.isBatchMode) return;
            var mat = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDShapes.Solid : t.worldPropMaterial ? t.worldPropMaterial : null;
            if (!mat) return;
            float y = t.Height(at.x, at.z);
            if (Physics.Raycast(at + Vector3.up * 0.3f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore) && hit.point.y > y) y = hit.point.y;
            GameObject go;
            if (pool.Count < Max)
            {
                go = new GameObject("Shards", typeof(MeshFilter), typeof(MeshRenderer));
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pool.Add(go);
            }
            else { go = pool[next]; next = (next + 1) % Max; if (!go) { pool.Clear(); next = 0; Drop(at, kind); return; } }
            go.GetComponent<MeshFilter>().sharedMesh = MeshFor(kind, Random.Range(0, Variants));
            go.transform.SetPositionAndRotation(new Vector3(at.x, y + 0.02f, at.z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go.SetActive(true);
        }
    }
}
