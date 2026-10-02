using MadMax.Rendering;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>HD animals on the game's procedural rig: every part of <see cref="Animal"/>'s <c>Rig</c> (Body, Head,
    /// Leg i, Tail, WingR/WingL, Seg i, Rattle, Saddle, Fleece) gets the HD mesh of the same part (sidecar props
    /// <c>part</c> / <c>rig_part</c> / <c>index</c>, exported at the voxel pivots) as a child placed so the HD rest pose
    /// matches; the voxel part mesh goes dark and the gait animation moves the HD mesh unchanged. Carcasses, kept
    /// animals and ridden ones are the same objects. Species without an export keep their voxels.</summary>
    public static class HDAnimal
    {
        /// <summary>Dress every part under <paramref name="rig"/> (call once after the rig is built, at rest).</summary>
        public static bool Dress(Transform rig, string species)
        {
            var a = HDAssets.Get(HDDomain.Animal, species);
            if (!a || !rig) return false;
            int legs = 0, segs = 0;
            bool any = false;
            for (int i = 0; i < rig.childCount; i++)
            {
                var part = rig.GetChild(i);
                int index = part.name == "Leg" ? legs++ : part.name == "Seg" ? segs++ : 0;
                any |= DressPart(rig, part, a, part.name, index);
                if (part.name == "Seg")
                    for (int k = 0; k < part.childCount; k++)
                        if (part.GetChild(k).name == "Rattle") DressPart(rig, part.GetChild(k), a, "Rattle", 0);
            }
            return any;
        }

        /// <summary>Dress one part made after the rig (saddle, fleece).</summary>
        public static bool DressPart(Transform rig, Transform part, string species, string name, int index = 0)
        {
            var a = HDAssets.Get(HDDomain.Animal, species);
            return a && DressPart(rig, part, a, name, index);
        }

        static HDAssetRef.ObjectInfo Match(HDAssetRef a, string name, int index)
        {
            bool indexed = name == "Leg" || name == "Seg";
            foreach (var o in a.objects)
            {
                if (o.role != "mesh") continue;
                if (indexed) { if ((o.rigPart == name || HDVisual.BaseName(o.part).StartsWith(name + "_")) && o.index == index) return o; }
                else if (o.part == name || HDVisual.BaseName(o.name) == name) return o;
            }
            if (name == "Body")
                foreach (var o in a.objects) if (o.role == "mesh" && o.rigPart == "Body") return o;
            return null;
        }

        /// <summary>Pose of <paramref name="part"/> in <paramref name="frame"/>'s space with mirrors but no growth scale.</summary>
        static Matrix4x4 RestOf(Transform frame, Transform part)
        {
            var m = Matrix4x4.identity;
            for (var t = part; t && t != frame; t = t.parent)
            {
                var s = t.localScale;
                m = Matrix4x4.TRS(t.localPosition, t.localRotation, new Vector3(Mathf.Sign(s.x), Mathf.Sign(s.y), Mathf.Sign(s.z))) * m;
            }
            return m;
        }

        public static bool DressPart(Transform frame, Transform part, HDAssetRef a, string name, int index)
        {
            if (!part || !a) return false;
            var o = Match(a, name, index);
            if (o == null || !HDAssets.MeshOf(a, o.name, out var mesh, out var mats)) return false;
            var go = new GameObject("HD", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = part.gameObject.layer;
            go.transform.SetParent(part, false);
            Apply(go.transform, RestOf(frame, part).inverse * Placement(a, o));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = mats;
            if (part.TryGetComponent<Renderer>(out var voxel)) voxel.forceRenderingOff = true;
            return true;
        }

        static readonly System.Collections.Generic.Dictionary<HDAssetRef, Matrix4x4> turn = new System.Collections.Generic.Dictionary<HDAssetRef, Matrix4x4>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => turn.Clear();

        /// <summary>Mesh vertices to rig space: the imported node's own matrix (it carries the FBX axis conversion the
        /// sidecar's rootMatrix does not), turned half way about Y when the import faces the other way (positions
        /// mirrored in x and z against the sidecar's).</summary>
        static Matrix4x4 Placement(HDAssetRef a, HDAssetRef.ObjectInfo o)
        {
            var node = HDAssets.MeshNode(a, o.name);
            if (!node) return o.rootMatrix;
            var root = a.model.transform;
            if (!turn.TryGetValue(a, out var fix))
            {
                float dot = 0f;
                foreach (var x in a.objects)
                {
                    var n = x.role == "mesh" ? HDAssets.MeshNode(a, x.name) : null;
                    if (!n) continue;
                    Vector3 p = root.InverseTransformPoint(n.position), q = x.rootMatrix.GetColumn(3);
                    dot += p.x * q.x + p.z * q.z;
                }
                turn[a] = fix = dot < 0f ? Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f)) : Matrix4x4.identity;
            }
            return fix * root.worldToLocalMatrix * node.localToWorldMatrix;
        }

        /// <summary>Set a transform's local pose from an affine matrix (a mirror goes into scale x).</summary>
        static void Apply(Transform t, Matrix4x4 m)
        {
            Vector3 cx = m.GetColumn(0), cy = m.GetColumn(1), cz = m.GetColumn(2);
            float kx = cx.magnitude, ky = cy.magnitude, kz = cz.magnitude;
            if (kx < 1e-6f || ky < 1e-6f || kz < 1e-6f) { t.localPosition = m.GetColumn(3); return; }
            if (Vector3.Dot(Vector3.Cross(cx, cy), cz) < 0f) kx = -kx;
            t.localPosition = m.GetColumn(3);
            t.localRotation = Quaternion.LookRotation(cz / kz, cy / ky);
            t.localScale = new Vector3(kx, ky, kz);
        }
    }
}
