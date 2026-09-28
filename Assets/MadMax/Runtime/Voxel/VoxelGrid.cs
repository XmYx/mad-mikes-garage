using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Voxel
{
    public delegate Color32 VoxMat(Vector3Int p);

    public struct Vox
    {
        public Color32 color;
        public int label;
        public byte mat;     // material id (MadMax.Items.ResourceType) for destruction yield
    }

    /// <summary>Sparse voxel grid. Voxel centers sit on integer coordinates; each voxel carries a colour and a part label.</summary>
    public class VoxelGrid
    {
        public readonly Dictionary<Vector3Int, Vox> voxels = new Dictionary<Vector3Int, Vox>();
        readonly Dictionary<string, int> labels = new Dictionary<string, int> { { "body", 0 } };

        /// <summary>Label applied by subsequent paint calls.</summary>
        public int label;

        /// <summary>Material id applied by subsequent paint calls.</summary>
        public byte material;

        public int Label(string name)
        {
            if (!labels.TryGetValue(name, out var id)) labels[name] = id = labels.Count;
            return id;
        }

        public VoxelGrid Use(string name) { label = Label(name); return this; }
        public VoxelGrid Mat(byte m) { material = m; return this; }

        public VoxelGrid Clone()
        {
            var g = new VoxelGrid { label = label, material = material };
            foreach (var kv in voxels) g.voxels[kv.Key] = kv.Value;
            return g;
        }

        static Vector3Int[] neighbours26;

        /// <summary>26-connected neighbourhood (faces, edges, corners) used for structural support.</summary>
        public static Vector3Int[] Neighbours26
        {
            get
            {
                if (neighbours26 != null) return neighbours26;
                var l = new List<Vector3Int>();
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                    if (x != 0 || y != 0 || z != 0) l.Add(new Vector3Int(x, y, z));
                return neighbours26 = l.ToArray();
            }
        }

        /// <summary>Voxels connected (26-neighbourhood) to any voxel at or below <paramref name="groundY"/>.</summary>
        public HashSet<Vector3Int> Supported(int groundY)
        {
            var visited = new HashSet<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            foreach (var p in voxels.Keys) if (p.y <= groundY && visited.Add(p)) queue.Enqueue(p);
            var n = Neighbours26;
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                for (int i = 0; i < n.Length; i++)
                {
                    var q = p + n[i];
                    if (voxels.ContainsKey(q) && visited.Add(q)) queue.Enqueue(q);
                }
            }
            return visited;
        }

        /// <summary>Delete voxels with no path to the ground (authoring safety for anchored props).</summary>
        public void PruneUnsupported()
        {
            var ok = Supported(MinY());
            if (ok.Count == voxels.Count) return;
            Remove(p => !ok.Contains(p));
        }

        public int MinY()
        {
            int m = int.MaxValue;
            foreach (var p in voxels.Keys) if (p.y < m) m = p.y;
            return m == int.MaxValue ? 0 : m;
        }

        public bool Has(Vector3Int p) => voxels.ContainsKey(p);
        public bool Has(int x, int y, int z) => voxels.ContainsKey(new Vector3Int(x, y, z));

        public void Set(Vector3Int p, VoxMat m) => voxels[p] = new Vox { color = m(p), label = label, mat = material };
        public void Set(int x, int y, int z, VoxMat m) => Set(new Vector3Int(x, y, z), m);
        public void Clear(int x, int y, int z) => voxels.Remove(new Vector3Int(x, y, z));

        public void Box(int x0, int y0, int z0, int x1, int y1, int z1, VoxMat m)
        {
            Order(ref x0, ref x1); Order(ref y0, ref y1); Order(ref z0, ref z1);
            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
                Set(x, y, z, m);
        }

        public void ClearBox(int x0, int y0, int z0, int x1, int y1, int z1)
        {
            Order(ref x0, ref x1); Order(ref y0, ref y1); Order(ref z0, ref z1);
            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
                Clear(x, y, z);
        }

        /// <summary>Paint only voxels that already exist inside the box.</summary>
        public void Repaint(int x0, int y0, int z0, int x1, int y1, int z1, VoxMat m)
        {
            Order(ref x0, ref x1); Order(ref y0, ref y1); Order(ref z0, ref z1);
            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
            {
                var p = new Vector3Int(x, y, z);
                if (voxels.TryGetValue(p, out var v)) { v.color = m(p); voxels[p] = v; }
            }
        }

        /// <summary>Re-label existing voxels inside a box (used to cut parts out of a whole-vehicle design).</summary>
        public void Relabel(int x0, int y0, int z0, int x1, int y1, int z1, string name, Func<Vector3Int, bool> filter = null)
        {
            int id = Label(name);
            Order(ref x0, ref x1); Order(ref y0, ref y1); Order(ref z0, ref z1);
            var keys = new List<Vector3Int>(voxels.Keys);
            foreach (var p in keys)
            {
                if (p.x < x0 || p.x > x1 || p.y < y0 || p.y > y1 || p.z < z0 || p.z > z1) continue;
                if (filter != null && !filter(p)) continue;
                var v = voxels[p]; v.label = id; voxels[p] = v;
            }
        }

        /// <summary>Cylinder whose axis runs along X.</summary>
        public void CylX(float cy, float cz, float r, int x0, int x1, VoxMat m, float rIn = -1f)
        {
            Order(ref x0, ref x1);
            int R = Mathf.CeilToInt(r);
            for (int x = x0; x <= x1; x++)
            for (int y = Mathf.FloorToInt(cy) - R; y <= Mathf.CeilToInt(cy) + R; y++)
            for (int z = Mathf.FloorToInt(cz) - R; z <= Mathf.CeilToInt(cz) + R; z++)
            {
                float d = Mathf.Sqrt((y - cy) * (y - cy) + (z - cz) * (z - cz));
                if (d <= r && d > rIn) Set(x, y, z, m);
            }
        }

        public void CylY(float cx, float cz, float r, int y0, int y1, VoxMat m, float rIn = -1f)
        {
            Order(ref y0, ref y1);
            int R = Mathf.CeilToInt(r);
            for (int y = y0; y <= y1; y++)
            for (int x = Mathf.FloorToInt(cx) - R; x <= Mathf.CeilToInt(cx) + R; x++)
            for (int z = Mathf.FloorToInt(cz) - R; z <= Mathf.CeilToInt(cz) + R; z++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (d <= r && d > rIn) Set(x, y, z, m);
            }
        }

        public void CylZ(float cx, float cy, float r, int z0, int z1, VoxMat m, float rIn = -1f)
        {
            Order(ref z0, ref z1);
            int R = Mathf.CeilToInt(r);
            for (int z = z0; z <= z1; z++)
            for (int x = Mathf.FloorToInt(cx) - R; x <= Mathf.CeilToInt(cx) + R; x++)
            for (int y = Mathf.FloorToInt(cy) - R; y <= Mathf.CeilToInt(cy) + R; y++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d <= r && d > rIn) Set(x, y, z, m);
            }
        }

        /// <summary>Solid tube between two points (for roll cages, bull bars, pipes).</summary>
        public void Tube(Vector3 a, Vector3 b, float radius, VoxMat m)
        {
            float len = Vector3.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len * 3f));
            int R = Mathf.CeilToInt(radius);
            for (int i = 0; i <= steps; i++)
            {
                var c = Vector3.Lerp(a, b, i / (float)steps);
                for (int x = -R; x <= R; x++)
                for (int y = -R; y <= R; y++)
                for (int z = -R; z <= R; z++)
                {
                    var p = new Vector3Int(Mathf.RoundToInt(c.x) + x, Mathf.RoundToInt(c.y) + y, Mathf.RoundToInt(c.z) + z);
                    if (Vector3.Distance(p, c) <= radius + 0.01f) Set(p, m);
                }
            }
        }

        public void RelabelWhere(Func<Vector3Int, Vox, bool> pred, string name)
        {
            int id = Label(name);
            var keys = new List<Vector3Int>(voxels.Keys);
            foreach (var p in keys)
            {
                var v = voxels[p];
                if (pred(p, v)) { v.label = id; voxels[p] = v; }
            }
        }

        public void Recolor(Func<Vector3Int, bool> pred, VoxMat m)
        {
            var keys = new List<Vector3Int>(voxels.Keys);
            foreach (var p in keys)
                if (pred(p)) { var v = voxels[p]; v.color = m(p); voxels[p] = v; }
        }

        public void Remove(Func<Vector3Int, bool> pred)
        {
            var keys = new List<Vector3Int>(voxels.Keys);
            foreach (var p in keys) if (pred(p)) voxels.Remove(p);
        }

        /// <summary>Pixel-art edge treatment: lighten exposed top edges, darken undersides.</summary>
        public void Bevel(float light = 0.22f, float dark = 0.25f)
        {
            var result = new Dictionary<Vector3Int, Vox>(voxels.Count);
            foreach (var kv in voxels)
            {
                var p = kv.Key; var v = kv.Value;
                bool topOpen = !Has(p + Vector3Int.up);
                bool sideOpen = !Has(p + Vector3Int.left) || !Has(p + Vector3Int.right) ||
                                !Has(p + new Vector3Int(0, 0, 1)) || !Has(p + new Vector3Int(0, 0, -1));
                if (topOpen && sideOpen) v.color = Color32.Lerp(v.color, new Color32(255, 236, 200, 255), light);
                else if (!Has(p + Vector3Int.down) && sideOpen) v.color = Color32.Lerp(v.color, new Color32(0, 0, 0, 255), dark);
                result[p] = v;
            }
            voxels.Clear();
            foreach (var kv in result) voxels[kv.Key] = kv.Value;
        }

        /// <summary>Copy every voxel of a label into a new grid, re-centred on origin.</summary>
        public VoxelGrid Extract(string name, Vector3Int origin)
        {
            int id = Label(name);
            var g = new VoxelGrid();
            foreach (var kv in voxels)
                if (kv.Value.label == id)
                    g.voxels[kv.Key - origin] = new Vox { color = kv.Value.color, label = 0, mat = kv.Value.mat };
            return g;
        }

        /// <summary>Extract + remove: the labelled voxels move out of this grid (separately animated pieces like trailer legs).</summary>
        public VoxelGrid Take(string name, Vector3Int origin)
        {
            var g = Extract(name, origin);
            int id = Label(name);
            Remove(p => voxels[p].label == id);
            return g;
        }

        public int Count => voxels.Count;

        static void Order(ref int a, ref int b) { if (a > b) (a, b) = (b, a); }
    }
}
