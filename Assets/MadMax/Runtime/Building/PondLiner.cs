using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A tarp laid in the ground: nothing soaks away under it (<see cref="MadMax.World.Spills"/>), so a hole dug
    /// with a shovel and lined becomes a pond that holds whatever is poured, pumped or rained into it. It drapes
    /// itself over the ground it covers (dig first, then lay it).</summary>
    public class PondLiner : MonoBehaviour
    {
        public static readonly List<PondLiner> All = new List<PondLiner>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        /// <summary>Half size of the lined footprint (m, local x / z).</summary>
        public Vector2 half = new Vector2(1.5f, 1.5f);

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            // drape the sheet over the dug ground (the build preview is flat)
            var mf = GetComponent<MeshFilter>();
            var terrain = MadMax.World.DeformableTerrain.Instance;
            if (!mf || !terrain || !mf.sharedMesh) return;
            const int n = 12;
            var verts = new Vector3[(n + 1) * (n + 1)];
            var cols = new Color32[verts.Length];
            var tris = new int[n * n * 6];
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                var local = new Vector3(Mathf.Lerp(-half.x, half.x, i / (float)n), 0f, Mathf.Lerp(-half.y, half.y, j / (float)n));
                var w = transform.TransformPoint(local);
                w.y = terrain.Height(w.x, w.z) + 0.02f;
                verts[j * (n + 1) + i] = transform.InverseTransformPoint(w);
                byte v = (byte)(46 + ((i * 7 + j * 13) % 3) * 6);
                cols[j * (n + 1) + i] = (i == 0 || j == 0 || i == n || j == n) ? new Color32(70, 66, 52, 255) : new Color32((byte)(v - 6), (byte)v, (byte)(v + 10), 255);
            }
            int t = 0;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i;
                tris[t++] = a; tris[t++] = a + n + 1; tris[t++] = a + n + 2;
                tris[t++] = a; tris[t++] = a + n + 2; tris[t++] = a + 1;
            }
            var m = new Mesh { name = "PondLiner", vertices = verts, colors32 = cols, triangles = tris };
            m.RecalculateNormals(); m.RecalculateBounds();
            mf.sharedMesh = m;
            foreach (var c in GetComponents<Collider>()) c.isTrigger = true;                // walk and drive over it
            if (MadMax.World.Spills.Instance) MadMax.World.Spills.RefreshAround(transform.position, Mathf.Max(half.x, half.y) + 0.5f);
        }

        void OnDestroy()
        {
            if (MadMax.World.Spills.Instance) MadMax.World.Spills.RefreshAround(transform.position, Mathf.Max(half.x, half.y) + 0.5f, this);
        }

        /// <summary>A lined point (no seepage).</summary>
        public static bool Covers(Vector3 p, PondLiner except = null)
        {
            foreach (var l in All)
            {
                if (!l || l == except) continue;
                var q = l.transform.InverseTransformPoint(p);
                if (Mathf.Abs(q.x) <= l.half.x && Mathf.Abs(q.z) <= l.half.y && q.y > -4f && q.y < 2f) return true;
            }
            return false;
        }

    }
}
