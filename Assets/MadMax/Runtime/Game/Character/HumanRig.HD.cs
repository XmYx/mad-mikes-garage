using System.Collections.Generic;
using MadMax.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace MadMax.Game
{
    /// <summary>HD mode of the human (the approved HD character pack, tools/blender/hd/PIPELINE.md): the body shape, face,
    /// hair (hat-cut variants under headwear), beard and every worn garment are SkinnedMeshRenderers bound to the very
    /// same bone transforms the voxel rig uses (named by <see cref="BodyPart"/>; the HD "Root" bone is the rig itself), so
    /// HumanAnimator, the ragdoll, held tools, bag props, seats and everything else that reads bones work unchanged.
    /// Skin under clothing is cut away per outfit (baked covers / voxel coverage), worn-out garments get holes, first
    /// person keeps only the arms, crowds share meshes and materials and switch LODs. Garments without an HD mesh fall
    /// back to their voxel shells in the HD voxel material.</summary>
    public partial class HumanRig
    {
        /// <summary>Built from the HD pack (false = voxel body).</summary>
        public bool IsHD { get; private set; }
        /// <summary>HD body shape in use (M, M2, F, F2).</summary>
        public string HDShape { get; private set; }
        /// <summary>HD garment pieces worn (piece keys), for tests and tools.</summary>
        public readonly List<string> HDPieces = new List<string>();

        struct HDPart { public SkinnedMeshRenderer r; public Mesh mesh; public HDCharacterCatalog.Piece piece; public bool head; }
        readonly List<HDPart> hdParts = new List<HDPart>();
        readonly List<Renderer> hdVoxel = new List<Renderer>();
        readonly List<Transform> blood = new List<Transform>();
        readonly Dictionary<BodyPart, float> boneLength = new Dictionary<BodyPart, float>();
        LODGroup lodGroup;
        bool hdVisible = true, hdFirstPerson;

        static readonly Bounds SkinBounds = new Bounds(new Vector3(0f, -0.05f, 0f), new Vector3(2.2f, 2.4f, 2.2f));

        /// <summary>Builds the HD look on the freshly made bones; false (nothing built) when the pack has no body for it.</summary>
        bool BuildHD()
        {
            IsHD = false; HDShape = null;
            hdParts.Clear(); hdVoxel.Clear(); HDPieces.Clear(); blood.Clear();
            var cat = HDCharacterCatalog.Instance;
            if (!cat) return false;
            string shape = HDHuman.ShapeFor(appearance, cat);
            if (shape == null) return false;
            var body = HDHuman.Body(cat, shape, appearance.skinTone);
            if (body == null || body.lods == null || body.lods.Length == 0 || !body.lods[0]) return false;
            HDShape = shape;
            IsHD = true;

            var map = new Dictionary<string, Transform> { { "Root", transform } };
            foreach (var kv in bones) map[kv.Key.ToString()] = kv.Value;
            foreach (var b in HumanDesign.Skeleton(appearance)) boneLength[b.part] = b.length;
            int levels = shareMeshes ? 3 : 1;
            var lods = new[] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
            var voxMat = HDModel.VoxelMaterial ? HDModel.VoxelMaterial : material;

            void Add(HDCharacterCatalog.Piece p, bool head, System.Func<int, Mesh> meshAt)
            {
                var mat = HDHuman.MaterialFor(p, appearance);
                if (!mat) mat = voxMat;
                for (int l = 0; l < levels; l++)
                {
                    var m = meshAt(l);
                    if (!m) continue;
                    var r = Skinned(p, m, mat, l, map);
                    lods[l].Add(r);
                    (head ? headRenderers : bodyRenderers).Add(r);
                    if (l == 0) hdParts.Add(new HDPart { r = r, mesh = m, piece = p, head = head });
                }
                HDPieces.Add(p.key);
            }
            Mesh Lod(HDCharacterCatalog.Piece p, int l) => p.lods != null && l < p.lods.Length ? p.lods[l] : null;

            // garments: HD pieces where the pack has them, voxel shells otherwise
            var defs = new List<ClothingDef>();
            foreach (var id in outfit) { var d = ClothingLibrary.Get(id); if (d != null) defs.Add(d); }
            defs.Sort((x, y) => x.inflate.CompareTo(y.inflate));
            var covering = new List<string>();
            var voxelCovering = new List<ClothingDef>();
            foreach (var d in defs)
            {
                float cond = condition != null ? condition(d.id) : 1f;
                bool torn = cond < 0.35f;
                bool headSlot = d.slot == ClothingSlot.Head || d.slot == ClothingSlot.Face;
                var pieces = HDHuman.GarmentPieces(cat, shape, d.id, cond);
                if (pieces.Count > 0)
                {
                    foreach (var p in pieces)
                    {
                        var pp = p;
                        Add(p, headSlot, l => torn ? HDHuman.Torn(Lod(pp, l)) : Lod(pp, l));
                        if (!torn) covering.Add(p.key);
                    }
                    continue;
                }
                var dd = d;
                foreach (var part in d.coverage.Keys)
                {
                    var pp = part;
                    var r = AddMesh(bones[part], Cached(d.id + part + (torn ? "t" : ""), d.id + "_" + part, () => HumanDesign.GarmentMesh(dd, pp, appearance, torn)), part == BodyPart.Head, voxMat);
                    if (r) { hdVoxel.Add(r); lods[0].Add(r); if (levels > 1) lods[1].Add(r); }
                }
                if (d.prop != null)
                {
                    var r = AddMesh(bones[d.propBone], Cached(d.id + "prop", d.id + "_prop", () => HumanDesign.PropMesh(dd, appearance)), d.propBone == BodyPart.Head, voxMat);
                    if (r) { hdVoxel.Add(r); lods[0].Add(r); if (levels > 1) lods[1].Add(r); }
                }
                if (!torn) voxelCovering.Add(d);
            }
            var seen = new HashSet<string>();
            foreach (var p in HDHuman.All(cat, shape, "under"))
            {
                if (!seen.Add(p.piece)) continue;
                var pp = p;
                Add(p, false, l => Lod(pp, l));
                covering.Add(p.key);
            }

            // the body, its skin cut away under what is worn
            var bd = body;
            Add(body, false, l => HDHuman.MaskedBody(cat, bd, Mathf.Min(l, bd.lods.Length - 1), covering, voxelCovering));

            // face and hair
            float cut = HDHuman.HeadwearCut(outfit, cat, shape, out bool voxelHat);
            var hair = HDHuman.Hair(cat, shape, appearance.hair, cut, cut > 0f);
            if (hair != null) { var hp = hair; Add(hair, true, l => Lod(hp, l)); }
            else if (appearance.hair != HairStyle.Bald && cut <= 0f && HDHuman.Hair(cat, shape, appearance.hair, 0f, false) == null)
            {
                var r = AddMesh(bones[BodyPart.Head], Cached("hair", "HairCap", () => HumanDesign.HairCap(appearance)), true, voxMat);
                if (r) { hdVoxel.Add(r); lods[0].Add(r); }
            }
            seen.Clear();
            foreach (var kind in new[] { "brows", "eyes" })
                foreach (var p in HDHuman.All(cat, shape, kind))
                    if (seen.Add(p.piece)) { var pp = p; Add(p, true, l => Lod(pp, l)); }
            if (appearance.beard >= 2) { var b = HDHuman.First(cat, shape, "beard", "beard"); if (b != null) Add(b, true, l => Lod(b, l)); }
            else if (appearance.beard == 1) { var b = HDHuman.First(cat, shape, "beard", "beard_stubble"); if (b != null) Add(b, true, l => Lod(b, l)); }

            // crowds: LODs (the player keeps LOD0 only: first person swaps its meshes)
            if (!lodGroup) lodGroup = GetComponent<LODGroup>();
            if (levels > 1)
            {
                if (!lodGroup) lodGroup = gameObject.AddComponent<LODGroup>();
                var list = new List<LOD> { new LOD(0.16f, lods[0].ToArray()) };
                if (lods[1].Count > 0) list.Add(new LOD(0.05f, lods[1].ToArray()));
                if (lods[2].Count > 0) list.Add(new LOD(0.004f, lods[2].ToArray()));
                else list[list.Count - 1] = new LOD(0.004f, list[list.Count - 1].renderers);
                lodGroup.localReferencePoint = new Vector3(0f, 0.9f, 0f);
                lodGroup.size = 2f;
                lodGroup.SetLODs(list.ToArray());
                lodGroup.enabled = true;
            }
            else if (lodGroup) lodGroup.enabled = false;
            ApplyHDVisibility();
            return true;
        }

        void ClearLods()
        {
            if (!lodGroup) lodGroup = GetComponent<LODGroup>();
            if (lodGroup) { lodGroup.SetLODs(new LOD[0]); lodGroup.enabled = false; }
        }

        SkinnedMeshRenderer Skinned(HDCharacterCatalog.Piece p, Mesh m, Material mat, int lod, Dictionary<string, Transform> map)
        {
            var go = new GameObject(lod > 0 ? p.piece + "__L" + lod : p.piece);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SkinnedMeshRenderer>();
            var bt = new Transform[p.bones.Length];
            for (int i = 0; i < bt.Length; i++) bt[i] = map.TryGetValue(p.bones[i], out var t) && t ? t : transform;
            r.bones = bt;
            r.rootBone = bones[BodyPart.Pelvis];
            r.sharedMesh = m;
            if (m.subMeshCount > 1) { var mats = new Material[m.subMeshCount]; for (int i = 0; i < mats.Length; i++) mats[i] = mat; r.sharedMaterials = mats; }
            else r.sharedMaterial = mat;
            r.updateWhenOffscreen = false;
            r.skinnedMotionVectors = false;
            r.quality = !shareMeshes ? SkinQuality.Auto : lod == 0 ? SkinQuality.Bone2 : SkinQuality.Bone1;
            r.localBounds = SkinBounds;
            if (lod >= 2) r.shadowCastingMode = ShadowCastingMode.Off;
            owned.Add(go);
            return r;
        }

        static bool ArmBone(Transform t)
        {
            if (!t) return false;
            var n = t.name;
            return n.StartsWith("UpperArm") || n.StartsWith("Forearm") || n.StartsWith("Hand");
        }

        /// <summary>First person in HD: head pieces off, everything else reduced to its arm and hand triangles.</summary>
        void ApplyHDVisibility()
        {
            foreach (var p in hdParts)
            {
                if (!p.r) continue;
                if (p.head) { p.r.enabled = hdVisible && !hdFirstPerson; continue; }
                var m = hdFirstPerson ? HDHuman.ArmsOnly(p.mesh, p.piece) : p.mesh;
                if (m) p.r.sharedMesh = m;
                p.r.enabled = hdVisible && m;
                p.r.shadowCastingMode = hdFirstPerson ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
            foreach (var r in hdVoxel)
            {
                if (!r) continue;
                bool head = headRenderers.Contains(r);
                r.enabled = hdVisible && (!hdFirstPerson || !head && ArmBone(r.transform.parent));
            }
            if (lodGroup && lodGroup.enabled) lodGroup.ForceLOD(hdFirstPerson ? 0 : -1);
        }

        // ------------------------------------------------------------------ blood
        static float LimbRadius(BodyPart p) => p switch
        {
            BodyPart.Pelvis => 0.14f, BodyPart.Chest => 0.15f, BodyPart.Head => 0.1f,
            BodyPart.UpperArmL or BodyPart.UpperArmR => 0.05f, BodyPart.ForearmL or BodyPart.ForearmR => 0.042f,
            BodyPart.HandL or BodyPart.HandR => 0.03f, BodyPart.ThighL or BodyPart.ThighR => 0.075f,
            BodyPart.ShinL or BodyPart.ShinR => 0.052f, _ => 0.05f
        };

        /// <summary>A blood splat on the body where a wound is (nearest bone, on its surface); the oldest go past 12.</summary>
        public void Bleed(Vector3 world, float amount = 1f)
        {
            BodyPart best = BodyPart.Chest; float bd = float.MaxValue; Vector3 axisPt = world;
            foreach (var kv in bones)
            {
                var t = kv.Value;
                if (!t) continue;
                float len = boneLength.TryGetValue(kv.Key, out var l) ? l : 0.2f;
                bool down = kv.Key >= BodyPart.UpperArmL;
                var dir = kv.Key == BodyPart.FootL || kv.Key == BodyPart.FootR ? t.forward : t.rotation * (down ? Vector3.down : Vector3.up);
                var a = t.position;
                float s = Mathf.Clamp(Vector3.Dot(world - a, dir), 0f, len);
                var c = a + dir * s;
                float d = (world - c).magnitude - LimbRadius(kv.Key);
                if (d < bd) { bd = d; best = kv.Key; axisPt = c; }
            }
            var bone = bones[best];
            var n = world - axisPt;
            if (n.sqrMagnitude < 1e-6f) n = bone.forward;
            n.Normalize();
            var mesh = HDHuman.BloodMesh(blood.Count + Random.Range(0, 4), out var mat);
            if (!mesh || !mat) return;
            var go = new GameObject("Blood", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            go.transform.SetParent(bone, false);
            go.transform.SetPositionAndRotation(axisPt + n * (LimbRadius(best) + 0.014f), Quaternion.LookRotation(-n) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            go.transform.localScale = Vector3.one * Mathf.Clamp(amount, 0.5f, 1.8f);
            blood.Add(go.transform);
            (best == BodyPart.Head ? headRenderers : bodyRenderers).Add(mr);
            while (blood.Count > 12) { if (blood[0]) Destroy(blood[0].gameObject); blood.RemoveAt(0); }
        }

        /// <summary>Washed: every blood splat goes.</summary>
        public void ClearBlood()
        {
            foreach (var b in blood) if (b) Destroy(b.gameObject);
            blood.Clear();
        }

        public int BloodSplats { get { int n = 0; foreach (var b in blood) if (b) n++; return n; } }
    }
}
