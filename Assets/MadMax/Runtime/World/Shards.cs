using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Litter left where something broke: broken windscreen glass, red / amber / clear lamp lenses. Small flat
    /// scatters of 3.5 cm cubes lying on the ground, pooled (oldest reused first), not saved.</summary>
    public static class Shards
    {
        public enum Kind { Glass, Clear, Red, Amber }
        const int Max = 96, Variants = 3;
        static readonly List<GameObject> pool = new List<GameObject>();
        static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        static int next;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { pool.Clear(); meshes.Clear(); next = 0; }

        static Color32[] Ramp(Kind k) => k == Kind.Glass ? Pal.Glass : k == Kind.Red ? new[] { Pal.TailR, Pal.Rust[2], Pal.TailR } :
            k == Kind.Amber ? new[] { Pal.Amber, Pal.Ochre[3], Pal.Amber } : new[] { Pal.LightW, Pal.Glass[3], Pal.Cream[4] };

        static Mesh MeshFor(Kind k, int variant)
        {
            int key = (int)k * Variants + variant;
            if (meshes.TryGetValue(key, out var m) && m) return m;
            if (MadMax.Rendering.HDBits.On) return meshes[key] = MadMax.Rendering.HDBits.Shards(Ramp(k), k == Kind.Glass ? 30 : 14, (k == Kind.Glass ? 11f : 7f) * 0.035f, 4711 + key * 131);
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Glass);
            var r = new System.Random(4711 + key * 131);
            var ramp = Ramp(k);
            int n = k == Kind.Glass ? 26 : 12;
            for (int i = 0; i < n; i++)
            {
                // denser in the middle, a few pieces skittered further out
                float a = (float)r.NextDouble() * Mathf.PI * 2f, d = Mathf.Pow((float)r.NextDouble(), 1.6f) * (k == Kind.Glass ? 11f : 7f);
                g.Set(Mathf.RoundToInt(Mathf.Cos(a) * d), 0, Mathf.RoundToInt(Mathf.Sin(a) * d), Pal.Solid(ramp[r.Next(ramp.Length)]));
            }
            return meshes[key] = VoxelMesher.Build(g, "Shards", 0.035f);
        }

        /// <summary>Scatter shards on the ground under <paramref name="at"/>.</summary>
        public static void Drop(Vector3 at, Kind kind)
        {
            var t = DeformableTerrain.Instance;
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
