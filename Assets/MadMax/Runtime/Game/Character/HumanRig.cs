using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Voxel human: one transform per body part (pivot at the joint), a skin mesh per part, garment layers
    /// per part, a hair cap and physics hair strands. Rebuild() applies Appearance + outfit.</summary>
    public class HumanRig : MonoBehaviour
    {
        public Material material;
        public Appearance appearance = new Appearance();
        public readonly List<string> outfit = new List<string>();

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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => shared.Clear();

        string LookKey => $"{appearance.skinTone},{(int)appearance.hair},{appearance.hairColor},{appearance.beard},{appearance.height:0.00},{appearance.build:0.00}";

        Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (!shareMeshes) return make();
            key = LookKey + "|" + key;
            if (shared.TryGetValue(key, out var m) && m) return m;
            m = make();
            shared[key] = m;
            return m;
        }

        public Transform Eye { get; private set; }
        public Transform RightHand => bones.TryGetValue(BodyPart.HandR, out var t) ? t : transform;
        public Transform Head => bones[BodyPart.Head];

        public void Rebuild()
        {
            foreach (var o in owned) if (o) Destroy(o);
            owned.Clear(); headRenderers.Clear(); bodyRenderers.Clear();
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
                var part = b.part;
                AddMesh(t, Cached("body" + part, () => HumanDesign.BodyMesh(part, appearance)), b.part == BodyPart.Head);
            }
            // garments, ordered by layer so outer shells win
            var defs = new List<ClothingDef>();
            foreach (var id in outfit) { var d = ClothingLibrary.Get(id); if (d != null) defs.Add(d); }
            defs.Sort((x, y) => x.inflate.CompareTo(y.inflate));
            foreach (var d in defs)
                foreach (var part in d.coverage.Keys)
                {
                    var dd = d; var pp = part;
                    AddMesh(bones[part], Cached(d.id + part, () => HumanDesign.GarmentMesh(dd, pp, appearance)), part == BodyPart.Head);
                }
            AddMesh(bones[BodyPart.Head], Cached("hair", () => HumanDesign.HairCap(appearance)), true);

            Eye = new GameObject("DriverEye").transform;
            Eye.SetParent(bones[BodyPart.Head], false);
            Eye.localPosition = new Vector3(0, 0.195f * appearance.height, 0.1f);
            owned.Add(Eye.gameObject);

            if (strands) Destroy(strands);
            if (noStrands) return;
            strands = gameObject.AddComponent<HairStrands>();
            strands.Init(this);
            owned.Add(strands);
        }

        void AddMesh(Transform bone, Mesh mesh, bool head)
        {
            if (!mesh) return;
            var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(bone, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            (head ? headRenderers : bodyRenderers).Add(r);
            owned.Add(go);
            if (!shareMeshes) owned.Add(mesh);
        }

        /// <summary>First person hides only the head (hair, face, headwear) so arms, body and legs stay visible.</summary>
        public void SetHeadVisible(bool v)
        {
            foreach (var r in headRenderers) if (r) r.enabled = v;
            if (strands) strands.SetVisible(v);
        }

        public void SetVisible(bool v)
        {
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
