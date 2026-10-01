using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>A moving visual (HD control surface, rudder blade) turned about a pivot from its rest pose: the pivot is
    /// the middle of the mesh (or its leading edge for a rudder blade), in the parent's space.</summary>
    public struct Hinge
    {
        public Vector3 restPos, pivot;
        public Quaternion restRot;

        /// <summary>Remember <paramref name="t"/>'s rest pose; <paramref name="leadingEdge"/> pivots at the front edge (rudders).</summary>
        public static void Rest(Transform t, out Hinge h, bool leadingEdge = false)
        {
            h = new Hinge { restPos = t.localPosition, restRot = t.localRotation, pivot = t.localPosition };
            if (!t.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) return;
            var b = mf.sharedMesh.bounds;
            var local = leadingEdge ? new Vector3(b.center.x, b.center.y, b.max.z) : b.center;
            h.pivot = t.localPosition + t.localRotation * Vector3.Scale(local, t.localScale);
        }

        /// <summary>Pose <paramref name="t"/> turned by <paramref name="turn"/> (parent space) about the pivot.</summary>
        public void Set(Transform t, Quaternion turn)
        {
            t.localRotation = turn * restRot;
            t.localPosition = pivot + turn * (restPos - pivot);
        }
    }
}
