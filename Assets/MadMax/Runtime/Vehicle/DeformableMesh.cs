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

        public bool IsDamaged { get; private set; }

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
            float q = snapStep / scale;
            for (int i = 0; i < pristine.Length; i++)
            {
                var d = displaced[i] - pristine[i];
                work[i] = pristine[i] + new Vector3(Mathf.Round(d.x / q) * q, Mathf.Round(d.y / q) * q, Mathf.Round(d.z / q) * q);
            }
            mesh.SetVertices(work);
            mesh.RecalculateNormals();   // faces own their vertices, so normals stay flat per face
            mesh.RecalculateBounds();
            IsDamaged = true;
            return true;
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
            mesh.SetVertices(displaced);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            IsDamaged = any;
        }

        public void Repair()
        {
            if (!mesh) return;
            System.Array.Copy(pristine, displaced, pristine.Length);
            mesh.SetVertices(pristine);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            IsDamaged = false;
        }
    }
}
