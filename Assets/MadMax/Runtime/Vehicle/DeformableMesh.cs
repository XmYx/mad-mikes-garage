using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Per-instance dentable copy of a voxel mesh. Displacement is stored against the pristine vertices,
    /// clamped, and snapped to a small step so dents stay crisp in pixel art.</summary>
    [RequireComponent(typeof(MeshFilter))]
    public class DeformableMesh : MonoBehaviour
    {
        public float snapStep = 0.02f;

        Mesh mesh;
        Vector3[] pristine, displaced, work;
        float scale = 1f;
        bool dirty;

        public bool IsDamaged { get; private set; }
        /// <summary>Undented vertex positions (null until the first dent).</summary>
        public Vector3[] Pristine => pristine;

        void Init()
        {
            if (mesh) return;
            var mf = GetComponent<MeshFilter>();
            mesh = Instantiate(mf.sharedMesh);
            mesh.name = mf.sharedMesh.name + " (dented)";
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
            pristine = mesh.vertices;
            displaced = (Vector3[])pristine.Clone();
            work = new Vector3[pristine.Length];
        }

        /// <summary>Push vertices within <paramref name="radius"/> of a world point along a world direction.</summary>
        public bool Dent(Vector3 worldPoint, Vector3 worldDir, float depth, float radius, float maxDisplacement)
        {
            var mf = GetComponent<MeshFilter>();
            if (!mf.sharedMesh) return false;
            var lp = transform.InverseTransformPoint(worldPoint);
            var ld = transform.InverseTransformDirection(worldDir).normalized;
            var ls = transform.lossyScale;
            float scale = (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f;
            float lr = radius / scale, ldepth = depth / scale, lmax = maxDisplacement / scale;
            var b = mf.sharedMesh.bounds;
            b.Expand(lr * 2f);
            if (!b.Contains(lp)) return false;

            Init();
            bool changed = false;
            float r2 = lr * lr;
            for (int i = 0; i < pristine.Length; i++)
            {
                var o = pristine[i];
                float d2 = (o - lp).sqrMagnitude;
                if (d2 > r2) continue;
                float w = 1f - Mathf.Sqrt(d2) / lr;
                var disp = displaced[i] - o + ld * (ldepth * w * w);
                if (disp.sqrMagnitude > lmax * lmax) disp = disp.normalized * lmax;
                displaced[i] = o + disp;
                changed = true;
            }
            if (!changed) return false;
            this.scale = scale;
            IsDamaged = true;
            MarkDirty();
            return true;
        }

        /// <summary>Dents are uploaded once per frame, however many contacts and hits landed (wrecks take dozens at spawn).</summary>
        void MarkDirty() { dirty = true; enabled = true; }

        void LateUpdate() { Flush(); enabled = false; }

        /// <summary>Upload pending dents now (snapped to <see cref="snapStep"/>).</summary>
        public void Flush()
        {
            if (!dirty || !mesh) return;
            dirty = false;
            float q = snapStep / scale;
            for (int i = 0; i < pristine.Length; i++)
            {
                var d = displaced[i] - pristine[i];
                work[i] = pristine[i] + new Vector3(Mathf.Round(d.x / q) * q, Mathf.Round(d.y / q) * q, Mathf.Round(d.z / q) * q);
            }
            mesh.SetVertices(work);
            mesh.RecalculateNormals();   // faces own their vertices, so normals stay flat per face
            mesh.RecalculateBounds();
        }

        /// <summary>The dents as base64 (vertex count, then index delta + displacement in snap steps per moved vertex);
        /// null when undamaged.</summary>
        public string SaveState()
        {
            if (!mesh || !IsDamaged) return null;
            float q = snapStep / scale;
            using var ms = new System.IO.MemoryStream();
            using var w = new System.IO.BinaryWriter(ms);
            w.Write(pristine.Length);
            int last = 0, n = 0;
            for (int i = 0; i < pristine.Length; i++)
            {
                var d = displaced[i] - pristine[i];
                int x = Mathf.Clamp(Mathf.RoundToInt(d.x / q), -127, 127), y = Mathf.Clamp(Mathf.RoundToInt(d.y / q), -127, 127), z = Mathf.Clamp(Mathf.RoundToInt(d.z / q), -127, 127);
                if (x == 0 && y == 0 && z == 0) continue;
                for (uint v = (uint)(i - last); ; v >>= 7) { if (v < 0x80) { w.Write((byte)v); break; } w.Write((byte)(v | 0x80)); }
                last = i;
                w.Write((sbyte)x); w.Write((sbyte)y); w.Write((sbyte)z);
                n++;
            }
            return n == 0 ? null : System.Convert.ToBase64String(ms.ToArray());
        }

        public void LoadState(string state)
        {
            if (string.IsNullOrEmpty(state) || !GetComponent<MeshFilter>().sharedMesh) return;
            Init();
            var ls = transform.lossyScale;
            scale = (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f;
            float q = snapStep / scale;
            using var r = new System.IO.BinaryReader(new System.IO.MemoryStream(System.Convert.FromBase64String(state)));
            if (r.ReadInt32() != pristine.Length) return;                                   // the model changed since
            int i = 0;
            while (r.BaseStream.Position < r.BaseStream.Length)
            {
                int step = 0; for (int sh = 0; ; sh += 7) { byte b = r.ReadByte(); step |= (b & 0x7f) << sh; if (b < 0x80) break; }
                i += step;
                var d = new Vector3(r.ReadSByte(), r.ReadSByte(), r.ReadSByte()) * q;
                if (i < displaced.Length) displaced[i] = pristine[i] + d;
            }
            IsDamaged = true;
            MarkDirty();
        }

        /// <summary>Beat out part of the dents (welder): each vertex moves a fraction back to its original place.</summary>
        public void RepairPartial(float fraction)
        {
            if (!mesh || !IsDamaged) return;
            bool any = false;
            for (int i = 0; i < displaced.Length; i++)
            {
                displaced[i] = Vector3.MoveTowards(displaced[i], pristine[i], Mathf.Max(snapStep, (displaced[i] - pristine[i]).magnitude * fraction));
                if ((displaced[i] - pristine[i]).sqrMagnitude > 1e-6f) any = true;
            }
            IsDamaged = any;
            MarkDirty();
        }

        public void Repair()
        {
            if (!mesh) return;
            System.Array.Copy(pristine, displaced, pristine.Length);
            dirty = false;
            mesh.SetVertices(pristine);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            IsDamaged = false;
        }
    }
}
