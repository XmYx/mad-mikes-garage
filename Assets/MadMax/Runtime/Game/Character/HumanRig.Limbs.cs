using System.Collections.Generic;
using MadMax.RPG;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Lost limbs and prosthetics on the rig (<see cref="Limbs"/>, <see cref="ProstheticLibrary"/>): the first
    /// bone of a severed part is scaled to nothing (its meshes, garments and the skinned HD body collapse onto the joint),
    /// a dressed cap closes the stump, and a fitted prosthetic hangs from the joint following the missing bone's pose.</summary>
    public partial class HumanRig
    {
        readonly Dictionary<BodyZone, Transform> grips = new Dictionary<BodyZone, Transform>();

        /// <summary>The bone is missing (cut off itself or below a cut).</summary>
        public bool BoneGone(BodyPart p) => Limbs.BoneGone(appearance.lost, p);

        /// <summary>Grip point of the prosthetic fitted on a zone (null: none).</summary>
        public Transform ProstheticGrip(BodyZone z) => grips.TryGetValue(z, out var t) && t ? t : null;

        /// <summary>Where a tool goes: the right hand; a mount arm carrying its own tool; the right prosthetic's grip; the
        /// left hand (or its prosthetic) when the right side holds nothing.</summary>
        public Transform ToolHand(string toolId)
        {
            foreach (var (zone, def) in ProstheticLibrary.Fitted(appearance))
                if (def.tool != null && def.tool == toolId && ProstheticGrip(zone)) return ProstheticGrip(zone);
            var r = SideGrip(false);
            if (r) return r;
            var l = SideGrip(true);
            return l ? l : RightHand;
        }

        Transform SideGrip(bool left)
        {
            var hand = left ? BodyPart.HandL : BodyPart.HandR;
            if (!BoneGone(hand)) return bones.TryGetValue(hand, out var t) ? t : null;
            var arm = left ? BodyZone.ArmL : BodyZone.ArmR;
            var z = Limbs.Severed(appearance.lost, arm) ? arm : left ? BodyZone.HandL : BodyZone.HandR;
            var d = ProstheticLibrary.Get(ProstheticLibrary.FittedOn(appearance, z));
            return d != null && d.grip >= 0.4f ? ProstheticGrip(z) : null;
        }

        void ApplyLimbs()
        {
            grips.Clear();
            if (appearance.lost == 0) return;
            foreach (var z in Limbs.All)
            {
                if (!Limbs.Severed(appearance.lost, z) || (Limbs.Parent(z) is BodyZone p && Limbs.Severed(appearance.lost, p))) continue;
                if (!bones.TryGetValue(Limbs.CutBone(z), out var cut) || !bones.TryGetValue(Limbs.StumpBone(z), out var stumpBone)) continue;
                cut.localScale = Vector3.one * 0.0001f;                                            // everything below collapses onto the joint
                var cap = new GameObject("Stump", typeof(MeshFilter), typeof(MeshRenderer));
                cap.transform.SetParent(stumpBone, false);
                cap.transform.localPosition = cut.localPosition;
                cap.GetComponent<MeshFilter>().sharedMesh = Limbs.StumpMesh();
                var cr = cap.GetComponent<MeshRenderer>();
                cr.sharedMaterial = material;
                bodyRenderers.Add(cr); owned.Add(cap);
                var def = ProstheticLibrary.Get(ProstheticLibrary.FittedOn(appearance, z));
                if (def == null || !def.Fits(z)) continue;
                var piece = new GameObject("Prosthetic", typeof(MeshFilter), typeof(MeshRenderer));
                piece.transform.SetParent(stumpBone, false);
                piece.transform.localPosition = cut.localPosition;
                piece.GetComponent<MeshFilter>().sharedMesh = ProstheticLibrary.MeshFor(def);
                var pr = piece.GetComponent<MeshRenderer>();
                pr.sharedMaterial = material;
                if (Limbs.Left(z)) piece.transform.localScale = new Vector3(-1f, 1f, 1f);
                piece.AddComponent<LimbFollow>().bone = cut;
                bodyRenderers.Add(pr); owned.Add(piece);
                var grip = new GameObject("Grip").transform;
                grip.SetParent(piece.transform, false);
                grip.localPosition = (Vector3)def.gripAt * HumanDesign.S;
                grips[z] = grip;
            }
        }
    }

    /// <summary>A prosthetic hanging from the stump turns with the bone it replaces (the animator still poses that bone).</summary>
    public class LimbFollow : MonoBehaviour
    {
        public Transform bone;
        void LateUpdate() { if (bone) transform.localRotation = bone.localRotation; }
    }
}
