using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Human body: one transform per body part (pivot at the joint, identity rest rotation). Voxel mode: a skin
    /// mesh per part, garment layers per part, a hair cap and physics hair strands. HD mode (HumanRig.HD.cs, when the
    /// HD character pack is installed): skinned HD body, face, hair and garments on the same bones. Rebuild() applies
    /// Appearance + outfit.</summary>
    public partial class HumanRig : MonoBehaviour
    {
        public Material material;
        public Appearance appearance = new Appearance();
        public readonly List<string> outfit = new List<string>();
        /// <summary>Garment condition by def id (1 = new); worn-out garments render ragged. Null = everything new.</summary>
        public System.Func<string, float> condition;

        public readonly Dictionary<BodyPart, Transform> bones = new Dictionary<BodyPart, Transform>();
        readonly List<Renderer> headRenderers = new List<Renderer>();
        readonly List<Renderer> bodyRenderers = new List<Renderer>();
        readonly List<Object> owned = new List<Object>();
        HairStrands strands;
        /// <summary>NPCs: take part meshes from a shared cache (same looks + garment = same mesh) instead of owning them.</summary>
        public bool shareMeshes;
        /// <summary>No physics hair strands (distant or numerous characters).</summary>
        public bool noStrands;
        static readonly Dictionary<string, Mesh> shared = new Dictionary<string, Mesh>();
        // meshes being built ahead on a worker (Prewarm): one task per character, keyed by every part it makes
        static readonly Dictionary<string, System.Threading.Tasks.Task<Dictionary<string, MadMax.Voxel.VoxelMesher.MeshData>>> warming =
            new Dictionary<string, System.Threading.Tasks.Task<Dictionary<string, MadMax.Voxel.VoxelMesher.MeshData>>>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { shared.Clear(); warming.Clear(); }

        static string KeyOf(Appearance a) => $"{a.skinTone},{(int)a.hair},{a.hairColor},{a.beard},{a.height:0.00},{a.build:0.00}";
        string LookKey => KeyOf(appearance);

        Mesh Cached(string key, string name, System.Func<Mesh> make)
        {
            if (!shareMeshes) return make();
            key = LookKey + "|" + key;
            if (shared.TryGetValue(key, out var m) && m) return m;
            if (warming.TryGetValue(key, out var job) && job.IsCompleted)
            {
                // meshed ahead on a worker: only the upload is left
                warming.Remove(key);
                MadMax.Voxel.VoxelMesher.MeshData md = null;
                if (!job.IsFaulted) job.Result.TryGetValue(key, out md);
                m = job.IsFaulted ? make() : md != null ? MadMax.Voxel.VoxelMesher.ToMesh(md, name) : null;
            }
            else m = make();
            shared[key] = m;
            return m;
        }

        /// <summary>Crowds: mesh a shared-mesh character's parts on a worker thread. True once all of them are ready, so
        /// the spawn itself only uploads meshes (a body is ~20 ms of voxel work).</summary>
        public static bool Prewarm(Appearance a, List<string> outfit)
        {
            if (HDHuman.Enabled) return PrewarmHD(a, outfit);
            return PrewarmVoxel(a, outfit, true, true);
        }

        /// <summary>HD crowds: the catalogue meshes are loaded with it; only garments without an HD mesh (and a hair cap
        /// the pack lacks) are voxel work.</summary>
        static bool PrewarmHD(Appearance a, List<string> outfit)
        {
            var cat = HDCharacterCatalog.Instance;
            string shape = HDHuman.ShapeFor(a, cat);
            if (shape == null || HDHuman.Body(cat, shape, a.skinTone) == null) return PrewarmVoxel(a, outfit, true, true);
            var voxel = new List<string>();
            foreach (var id in outfit) if (!HDHuman.HasGarment(cat, shape, id)) voxel.Add(id);
            bool cap = a.hair != HairStyle.Bald && HDHuman.Hair(cat, shape, a.hair, 0f, false) == null;
            return voxel.Count == 0 && !cap || PrewarmVoxel(a, voxel, false, cap);
        }

        static bool PrewarmVoxel(Appearance a, List<string> outfit, bool body, bool hair)
        {
            string look = KeyOf(a) + "|";
            bool ready = true;
            List<(string key, System.Func<MadMax.Voxel.VoxelMesher.MeshData> make)> missing = null;
            void Need(string key, System.Func<MadMax.Voxel.VoxelMesher.MeshData> make)
            {
                if (shared.ContainsKey(key)) return;
                if (warming.TryGetValue(key, out var running)) { ready &= running.IsCompleted; return; }
                (missing ??= new List<(string, System.Func<MadMax.Voxel.VoxelMesher.MeshData>)>()).Add((key, make));
            }
            if (body) foreach (var b in HumanDesign.Skeleton(a)) { var part = b.part; Need(look + "body" + part, () => HumanDesign.BodyData(part, a)); }
            foreach (var id in outfit)
            {
                var d = ClothingLibrary.Get(id);
                if (d == null) continue;
                foreach (var part in d.coverage.Keys) { var pp = part; Need(look + d.id + pp, () => HumanDesign.GarmentData(d, pp, a)); }
                if (d.prop != null) Need(look + d.id + "prop", () => HumanDesign.PropData(d, a));
            }
            if (hair) Need(look + "hair", () => HumanDesign.HairCapData(a));
            if (missing == null) return ready;
            var list = missing;
            var job = System.Threading.Tasks.Task.Run(() =>
            {
                var made = new Dictionary<string, MadMax.Voxel.VoxelMesher.MeshData>();
                foreach (var (key, make) in list) made[key] = make();
                return made;
            });
            foreach (var (key, _) in list) warming[key] = job;
            return false;
        }

        public Transform Eye { get; private set; }
        public Transform RightHand => bones.TryGetValue(BodyPart.HandR, out var t) ? t : transform;
        public Transform Head => bones[BodyPart.Head];

        public void Rebuild()
        {
            foreach (var o in owned) if (o) Destroy(o);
            owned.Clear(); headRenderers.Clear(); bodyRenderers.Clear(); hdParts.Clear(); hdVoxel.Clear(); blood.Clear(); HDPieces.Clear();
            var saved = new Dictionary<BodyPart, Quaternion>();
            foreach (var kv in bones) if (kv.Value) { saved[kv.Key] = kv.Value.localRotation; Destroy(kv.Value.gameObject); }
            bones.Clear();

            foreach (var b in HumanDesign.Skeleton(appearance))
            {
                var t = new GameObject(b.part.ToString()).transform;
                t.SetParent(b.parent.HasValue ? bones[b.parent.Value] : transform, false);
                t.localPosition = b.offset;
                if (saved.TryGetValue(b.part, out var r)) t.localRotation = r;
                bones[b.part] = t;
            }
            IsHD = false;
            if (HDHuman.Enabled && BuildHD()) { FinishRebuild(); return; }
            ClearLods();
            foreach (var b in HumanDesign.Skeleton(appearance))
            {
                var part = b.part;
                AddMesh(bones[part], Cached("body" + part, "Body_" + part, () => HumanDesign.BodyMesh(part, appearance)), b.part == BodyPart.Head);
            }
            // garments, ordered by layer so outer shells win
            var defs = new List<ClothingDef>();
            foreach (var id in outfit) { var d = ClothingLibrary.Get(id); if (d != null) defs.Add(d); }
            defs.Sort((x, y) => x.inflate.CompareTo(y.inflate));
            foreach (var d in defs)
            {
                var dd = d;
                bool torn = condition != null && condition(d.id) < 0.35f;
                foreach (var part in d.coverage.Keys)
                {
                    var pp = part;
                    AddMesh(bones[part], Cached(d.id + part + (torn ? "t" : ""), d.id + "_" + part, () => HumanDesign.GarmentMesh(dd, pp, appearance, torn)), part == BodyPart.Head);
                }
                if (d.prop != null) AddMesh(bones[d.propBone], Cached(d.id + "prop", d.id + "_prop", () => HumanDesign.PropMesh(dd, appearance)), d.propBone == BodyPart.Head);
            }
            AddMesh(bones[BodyPart.Head], Cached("hair", "HairCap", () => HumanDesign.HairCap(appearance)), true);
            FinishRebuild();
        }

        void FinishRebuild()
        {
            Eye = new GameObject("DriverEye").transform;
            Eye.SetParent(bones[BodyPart.Head], false);
            Eye.localPosition = new Vector3(0, 0.195f * appearance.height, 0.1f);
            owned.Add(Eye.gameObject);

            if (strands) Destroy(strands);
            if (noStrands || IsHD) return;
            strands = gameObject.AddComponent<HairStrands>();
            strands.Init(this);
            owned.Add(strands);
        }

        Renderer AddMesh(Transform bone, Mesh mesh, bool head, Material mat = null)
        {
            if (!mesh) return null;
            var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(bone, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat ? mat : material;
            (head ? headRenderers : bodyRenderers).Add(r);
            owned.Add(go);
            if (!shareMeshes) owned.Add(mesh);
            return r;
        }

        /// <summary>First person hides the head (hair, face, headwear); voxel bodies keep arms, body and legs, HD bodies keep
        /// only the arms and hands.</summary>
        public void SetHeadVisible(bool v)
        {
            if (IsHD) { hdFirstPerson = !v; ApplyHDVisibility(); return; }
            foreach (var r in headRenderers) if (r) r.enabled = v;
            if (strands) strands.SetVisible(v);
        }

        public void SetVisible(bool v)
        {
            if (IsHD)
            {
                hdVisible = v;
                foreach (var r in bodyRenderers) if (r) r.enabled = v;
                foreach (var r in headRenderers) if (r) r.enabled = v;
                ApplyHDVisibility();
                return;
            }
            foreach (var r in bodyRenderers) if (r) r.enabled = v;
            SetHeadVisible(v);
        }

        public void ResetHair() { if (strands) strands.ResetChains(); }

        public Transform Bone(BodyPart p) => bones[p];

        public bool Wearing(string id) => outfit.Contains(id);

        /// <summary>Wear a garment, replacing whatever occupies its slot.</summary>
        public void Wear(string id)
        {
            var def = ClothingLibrary.Get(id);
            if (def == null) return;
            outfit.RemoveAll(o => ClothingLibrary.Get(o)?.slot == def.slot);
            outfit.Add(def.id);
            Rebuild();
        }

        public void TakeOff(string id)
        {
            var def = ClothingLibrary.Get(id);
            if (def != null && outfit.Remove(def.id)) Rebuild();
        }
    }
}
