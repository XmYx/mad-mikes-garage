using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Visible load in a bucket, in front of a blade or in a tipper bed: a voxel mound in the soil's colour that
    /// grows with the fill (0..1). Meshes are shared per shape and soil, rebuilt when play mode destroyed them.</summary>
    public class SoilHeap : MonoBehaviour
    {
        public enum Shape { Bucket, Blade, Bed }

        /// <summary>Mound size in voxels: half-width (x), height (y), half-depth (z).</summary>
        public Vector3Int size = new Vector3Int(5, 4, 3);
        public Shape shape;

        MeshFilter mf;
        MeshRenderer mr;
        ResourceType shown = ResourceType.None;
        float fill = -1f;

        static readonly Dictionary<long, Mesh> meshes = new Dictionary<long, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => meshes.Clear();

        /// <summary>A heap under <paramref name="parent"/> at <paramref name="localVoxels"/> (0.08 m voxels).</summary>
        public static SoilHeap Create(Transform parent, Vector3 localVoxels, Shape shape, Vector3Int size, Material mat)
        {
            var go = new GameObject("SoilHeap", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localVoxels * VoxelMesher.DefaultSize;
            var h = go.AddComponent<SoilHeap>();
            h.shape = shape; h.size = size;
            h.mf = go.GetComponent<MeshFilter>(); h.mr = go.GetComponent<MeshRenderer>();
            h.mr.sharedMaterial = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDShapes.Solid : mat;
            go.SetActive(false);
            return h;
        }

        /// <summary>Show <paramref name="amount"/> (0..1 of capacity) of <paramref name="type"/>.</summary>
        public void Set(float amount, ResourceType type)
        {
            amount = Mathf.Clamp01(amount);
            bool show = amount > 0.02f && type != ResourceType.None;
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
            if (!show) return;
            if (type != shown || !mf.sharedMesh) { shown = type; mf.sharedMesh = MeshFor(type); fill = -1f; }
            if (Mathf.Abs(amount - fill) < 0.01f) return;
            fill = amount;
            // a bucket fills out from its bottom, a bed and a blade pile rise in height
            float s = Mathf.Lerp(0.35f, 1f, Mathf.Sqrt(amount));
            transform.localScale = shape == Shape.Bucket ? Vector3.one * s
                : new Vector3(shape == Shape.Blade ? s : 1f, Mathf.Max(0.08f, amount), shape == Shape.Blade ? s : 1f);
        }

        Mesh MeshFor(ResourceType type)
        {
            long key = ((long)shape << 48) | ((long)size.x << 36) | ((long)size.y << 28) | ((long)size.z << 20) | (long)type;
            if (meshes.TryGetValue(key, out var m) && m) return m;
            meshes[key] = m = MadMax.Rendering.HDBits.On ? MadMax.Rendering.HDBits.SoilHeap(size, shape == Shape.Bed, ResourceInfo.Color(type), (int)type * 7919 + (int)shape)
                : VoxelMesher.Build(Grid(type), "SoilHeap_" + type);
            return m;
        }

        VoxelGrid Grid(ResourceType type)
        {
            var g = new VoxelGrid().Mat((byte)type);
            Color32 c = ResourceInfo.Color(type);
            var ramp = new[] { Shade(c, 0.72f), Shade(c, 0.86f), c, Shade(c, 1.12f) };
            var r = new System.Random((int)type * 7919 + (int)shape);
            int w = size.x, h = size.y, d = size.z;
            for (int x = -w; x <= w; x++)
            for (int z = -d; z <= d; z++)
            {
                float nx = x / (w + 0.5f), nz = z / (d + 0.5f);
                float rr = nx * nx + nz * nz;
                // bed: a flat load with a crowned top; bucket / blade: a rounded mound with clods on its skin
                float top = shape == Shape.Bed ? h * (0.75f + 0.25f * (1f - Mathf.Min(1f, rr))) : rr >= 1f ? -1f : h * Mathf.Sqrt(1f - rr);
                if (top < 0f) continue;
                int ty = Mathf.RoundToInt(top + (float)r.NextDouble() * 1.2f - 0.4f);
                for (int y = shape == Shape.Bed ? 0 : -Mathf.Min(ty, h / 2); y <= ty; y++)
                    g.Set(x, y, z, Pal.Ramp(ramp, y >= ty - 1 ? 2 : 1, 4271 + (int)type));
            }
            return g;
        }

        static Color32 Shade(Color32 c, float f) => new Color32((byte)Mathf.Min(255f, c.r * f), (byte)Mathf.Min(255f, c.g * f), (byte)Mathf.Min(255f, c.b * f), 255);
    }
}
