using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MadMax.Game;
using UnityEditor;
using UnityEngine;

namespace MadMax.EditorTools
{
    /// <summary><c>MadMax/HD/Build Character Catalog</c>: turns every exported HD character (group <c>character</c>,
    /// tools/blender/hd/PIPELINE.md) into <see cref="HDCharacterCatalog"/> pieces the game wears on its HumanRig:
    /// meshes re-bound to the HumanRig rest pose (identity bone rotations: bindpose = T(-joint) x mesh-to-root, which also
    /// removes the FBX bone axes), sorted by body shape (from the rig's joints), kind (body, eyes, brows, hair + hat cuts,
    /// beard, underwear, garment by ClothingLibrary id, variants from the material), with LODs, the skin / hair tint they
    /// were baked with, and per body + garment the body triangles the garment covers (ray along the skin normal hits the
    /// garment, eroded two rings so no gap opens at hems). Output: Resources/HDGen/Characters.asset (meshes as
    /// sub-assets; gitignored like the rest of the HD catalog).</summary>
    public static class HDCharacterCatalogBuilder
    {
        public const string OutPath = "Assets/MadMax/Resources/HDGen/Characters.asset";
        static readonly string[] Bones = { "Root", "Pelvis", "Chest", "Head", "UpperArmL", "UpperArmR", "ForearmL", "ForearmR", "HandL", "HandR",
                                            "ThighL", "ThighR", "ShinL", "ShinR", "FootL", "FootR" };
        /// <summary>HD garment ids (garments.py GARMENTS) beside ClothingLibrary's; aliases map HD-only names to game ids.</summary>
        static readonly string[] HDGarments = { "underwear", "tshirt", "tank", "hoodie", "sweater", "jacket", "bomber", "duster", "vest", "pants", "jeans",
                                                "shorts", "overalls", "boots", "combat_boots", "gloves", "fingerless", "cowboy", "sunhat", "beanie", "helmet",
                                                "goggles", "bandana", "shemagh", "scarf", "gasmask", "skull_mask", "vest_scrap", "vest_kevlar", "shoulder",
                                                "arm_guards", "shin_guards", "backpack", "milpack", "toolbelt", "goggles_up", "labcoat", "tshirt_worn" };
        static readonly Dictionary<string, (string id, string variant)> Alias = new Dictionary<string, (string, string)>
        {
            { "goggles_up", ("goggles", "up") }, { "tshirt_worn", ("tshirt", "worn") }, { "labcoat", ("coat", "lab") }, { "combat_laces", ("combat_boots", null) },
        };

        class Src
        {
            public HDCharacterCatalog.Piece piece;
            public Mesh[] src = new Mesh[3];
            public Matrix4x4[] meshToRoot = new Matrix4x4[3];
            public string asset;
        }

        [MenuItem("MadMax/HD/Build Character Catalog")]
        public static void BuildMenu() => Build(true);

        /// <summary>Builds the catalogue; false when there is no HD character in the pack.</summary>
        public static bool Build(bool verbose)
        {
            var dir = HDSidecar.Root + "/character";
            if (!Directory.Exists(dir)) { if (verbose) Debug.LogWarning("[HD] no " + dir + ": the character pack is not installed"); return false; }
            var known = new HashSet<string>(HDGarments);
            foreach (var d in ClothingLibrary.All) known.Add(d.id);
            var srcs = new Dictionary<string, Src>();          // key -> source
            var order = new List<string>();
            var dedupe = new HashSet<string>();
            var shapes = new HashSet<string>();
            int assets = 0;
            try
            {
                // full wardrobes (a body shape with every garment, hair and beard) first; older single-outfit exports
                // only add shapes no wardrobe has
                var sides = new List<HDSidecar>();
                foreach (var j in Directory.GetFiles(dir, "*.hd.json", SearchOption.AllDirectories))
                {
                    var sd = HDSidecar.Load(j.Replace('\\', '/'));
                    if (sd != null && sd.kind == "character" && sd.objects != null) sides.Add(sd);
                }
                sides.Sort((a, b) => b.objects.Length != a.objects.Length ? b.objects.Length.CompareTo(a.objects.Length) : string.CompareOrdinal(a.asset, b.asset));
                var complete = new HashSet<string>();
                for (int ai = 0; ai < sides.Count; ai++)
                {
                    var side = sides[ai];
                    EditorUtility.DisplayProgressBar("HD characters", side.asset, ai / (float)sides.Count * 0.4f);
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(side.ModelPath);
                    if (!model) { Debug.LogWarning("[HD] character model not imported: " + side.ModelPath); continue; }
                    assets++;
                    Collect(side, model, known, srcs, order, dedupe, shapes, complete);
                }
                if (srcs.Count == 0) { if (verbose) Debug.LogWarning("[HD] no character meshes found under " + dir); return false; }

                var cat = ScriptableObject.CreateInstance<HDCharacterCatalog>();
                cat.version = HDCharacterCatalog.Version;
                cat.builtAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                cat.shapes.AddRange(shapes);
                cat.shapes.Sort(System.StringComparer.Ordinal);
                var subAssets = new List<Object>();
                var added = new HashSet<Object>();
                foreach (var k in order) { cat.pieces.Add(srcs[k].piece); foreach (var m in srcs[k].piece.lods) if (m && added.Add(m)) subAssets.Add(m); }

                // covers: for each body geometry x LOD, every garment / underwear piece of the same shape
                var bodies = new List<Src>(); var cloth = new List<Src>();
                foreach (var k in order)
                {
                    var s = srcs[k];
                    if (s.piece.kind == "body") bodies.Add(s);
                    else if (s.piece.kind == "garment" || s.piece.kind == "under") cloth.Add(s);
                }
                var doneGeom = new HashSet<string>();
                int pairs = 0, total = 0;
                foreach (var b in bodies) if (doneGeom.Add(b.piece.geom)) foreach (var c in cloth) if (c.piece.shape == b.piece.shape) total += b.piece.lods.Length;
                doneGeom.Clear();
                foreach (var b in bodies)
                {
                    if (!doneGeom.Add(b.piece.geom)) continue;
                    for (int l = 0; l < b.piece.lods.Length; l++)
                    {
                        if (!b.src[l]) continue;
                        var bv = SkinSpace(b.src[l], b.meshToRoot[l], out var bn);
                        var tris = b.src[l].triangles;
                        foreach (var c in cloth)
                        {
                            if (c.piece.shape != b.piece.shape || !c.src[0]) continue;
                            pairs++;
                            if (pairs % 20 == 0) EditorUtility.DisplayProgressBar("HD characters", "covers " + b.piece.key + " / " + c.piece.key, 0.4f + 0.6f * pairs / Mathf.Max(1, total));
                            var bits = Cover(bv, bn, tris, c.src[0], c.meshToRoot[0]);
                            if (bits != null) cat.covers.Add(new HDCharacterCatalog.Cover { body = b.piece.geom, lod = l, garment = c.piece.key, bits = bits });
                        }
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
                if (File.Exists(OutPath)) AssetDatabase.DeleteAsset(OutPath);
                AssetDatabase.CreateAsset(cat, OutPath);
                foreach (var o in subAssets) AssetDatabase.AddObjectToAsset(o, cat);
                EditorUtility.SetDirty(cat);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(OutPath);
                HDCharacterCatalog.Reload();
                Debug.Log($"[HD] character catalogue: {assets} assets, {cat.pieces.Count} pieces, shapes {string.Join(",", cat.shapes)}, {cat.covers.Count} covers -> {OutPath}");
                return true;
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        // ------------------------------------------------------------------ collect
        static Matrix4x4 RootSpace(Transform t, Transform root)
        {
            var m = Matrix4x4.identity;
            for (var x = t; x && x != root; x = x.parent) m = Matrix4x4.TRS(x.localPosition, x.localRotation, x.localScale) * m;
            return m;
        }

        static string Shape(Dictionary<string, Transform> bones, Transform root, out bool female)
        {
            female = false;
            if (!bones.TryGetValue("Head", out var head) || !bones.TryGetValue("UpperArmR", out var arm)) return null;
            float h = RootSpace(head, root).GetColumn(3).y / 1.48f;
            float w = Mathf.Abs(RootSpace(arm, root).GetColumn(3).x) / 0.2f;
            string best = null; float bd = 0.012f;
            foreach (var s in HDHuman.Shapes)
            {
                float d = Mathf.Abs(s.h - h) + Mathf.Abs(s.w - w);
                if (d < bd) { bd = d; best = s.key; female = s.female; }
            }
            if (best != null) return best;
            female = w < 0.93f;
            return $"h{h:0.00}w{w:0.00}";
        }

        static void Collect(HDSidecar side, GameObject model, HashSet<string> known, Dictionary<string, Src> srcs, List<string> order,
                            HashSet<string> dedupe, HashSet<string> shapes, HashSet<string> complete)
        {
            var root = model.transform;
            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (System.Array.IndexOf(Bones, t.name) >= 0 && !bones.ContainsKey(t.name)) bones[t.name] = t;
            string shape = Shape(bones, root, out bool female);
            if (shape == null) { Debug.LogWarning("[HD] " + side.asset + ": no HumanRig bones"); return; }
            if (complete.Contains(shape)) { Debug.Log("[HD] " + side.asset + ": shape " + shape + " already has a full wardrobe, skipped"); return; }
            int meshes = 0;
            foreach (var o in side.objects) if (o.role == "mesh" && o.type == "MESH") meshes++;
            if (meshes >= 60) complete.Add(shape);
            shapes.Add(shape);
            // skin / hair colour the tintable textures were baked in (root props), matched to the game's palettes
            int skinRef = PaletteIndex(RootProp(side, "skin_tone_hex"), true), hairRef = PaletteIndex(RootProp(side, "hair_colour_hex"), false);
            string prefix = side.rootName != null && side.rootName.EndsWith("_Rig") ? side.rootName.Substring(0, side.rootName.Length - 4) + "_" : "";
            var heads = new Dictionary<string, Vector3>();
            foreach (var kv in bones) heads[kv.Key] = kv.Key == "Root" ? Vector3.zero : (Vector3)RootSpace(kv.Value, root).GetColumn(3);

            // variants per garment come from the main piece's material (tshirt in m_tshirt_worn = the worn t-shirt)
            var variantOf = new Dictionary<string, string>();
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var (pc, lod) = PieceName(r.name, prefix);
                if (lod != 0 || !known.Contains(pc)) continue;
                var o = side.Get(r.name);
                string mat = o != null && o.materials != null && o.materials.Length > 0 ? o.materials[0] : null;
                if (mat != null && mat.StartsWith("m_")) mat = mat.Substring(2);
                if (mat != null && mat != pc && Alias.ContainsKey(mat)) variantOf[pc] = mat;
            }

            var sorted = new List<Renderer>(renderers);
            sorted.Sort((x, y) => PieceName(x.name, prefix).lod.CompareTo(PieceName(y.name, prefix).lod));   // LOD0 first
            foreach (var r in sorted)
            {
                var (pc, lod) = PieceName(r.name, prefix);
                if (pc == null || lod > 2) continue;
                var info = Classify(pc, known, variantOf);
                if (info.kind == null) { Debug.Log("[HD] " + side.asset + ": skipped " + r.name); continue; }
                string variantKey = info.variant ?? "";
                string dkey = shape + "|" + info.kind + "|" + pc + "|" + variantKey + (info.kind == "body" || info.kind == "hair" || info.kind == "beard" ? "|" + side.asset : "");
                string key = side.asset + "/" + pc;
                if (!srcs.TryGetValue(key, out var s))
                {
                    if (lod == 0 && !dedupe.Add(dkey)) continue;   // the same garment in another outfit export
                    if (lod > 0) continue;                           // a LOD without its LOD0
                    var o = side.Get(r.name);
                    int tint = 0, toneRef = -1;
                    bool paint = false;
                    if (o != null && o.materials != null)
                        foreach (var m in o.materials)
                        {
                            if (m == "skin_tint" || m == "hair_tint") { tint = m == "skin_tint" ? 1 : 2; toneRef = tint == 1 ? skinRef : hairRef; paint = true; break; }
                            var mm = Regex.Match(m ?? "", @"^(skin|hair)(\d+)$");
                            if (mm.Success) { tint = mm.Groups[1].Value == "skin" ? 1 : 2; toneRef = int.Parse(mm.Groups[2].Value); break; }
                        }
                    s = new Src
                    {
                        asset = side.asset,
                        piece = new HDCharacterCatalog.Piece
                        {
                            key = key, shape = shape, female = female, kind = info.kind, piece = pc, garment = info.garment, variant = info.variant,
                            hair = info.hair, cut = info.cut, tint = tint, toneRef = toneRef, paint = paint, material = r.sharedMaterial,
                        }
                    };
                    srcs[key] = s;
                    order.Add(key);
                }
                var bound = Bind(r, root, bones, heads, side.asset + "/" + r.name, out var m2r, out var boneNames, out var srcMesh);
                if (!bound) continue;
                s.src[lod] = srcMesh; s.meshToRoot[lod] = m2r;
                var lods = new List<Mesh>(s.piece.lods ?? new Mesh[0]);
                while (lods.Count <= lod) lods.Add(null);
                lods[lod] = bound;
                s.piece.lods = lods.ToArray();
                if (lod == 0)
                {
                    s.piece.bones = boneNames;
                    s.piece.geom = srcMesh.vertexCount + "_" + GeomHash(srcMesh);
                }
            }
            // drop LOD holes (a piece with LOD2 but no LOD1 keeps LOD0 as LOD1)
            foreach (var s in srcs.Values)
            {
                if (s.asset != side.asset || s.piece.lods == null) continue;
                var l = s.piece.lods;
                for (int i = 1; i < l.Length; i++) if (!l[i]) { l[i] = l[i - 1]; s.src[i] = s.src[i - 1]; s.meshToRoot[i] = s.meshToRoot[i - 1]; }
            }
        }

        static (string piece, int lod) PieceName(string name, string prefix)
        {
            int lod = 0;
            var m = Regex.Match(name, @"^(.*)__L(\d)$");
            if (m.Success) { name = m.Groups[1].Value; lod = int.Parse(m.Groups[2].Value); }
            if (name.EndsWith("_Rig")) return (null, 0);
            if (prefix.Length > 0 && name.StartsWith(prefix)) name = name.Substring(prefix.Length);
            return (name, lod);
        }

        struct Info { public string kind, garment, variant, hair; public float cut; }

        static Info Classify(string pc, HashSet<string> known, Dictionary<string, string> variantOf)
        {
            if (pc == "Body") return new Info { kind = "body" };
            if (pc == "brows") return new Info { kind = "brows" };
            if (pc.StartsWith("Eye")) return new Info { kind = "eyes" };
            if (pc.StartsWith("beard")) return new Info { kind = "beard" };
            if (pc == "under" || pc == "top") return new Info { kind = "under", garment = "underwear" };
            var hm = Regex.Match(pc, @"^hair_([A-Za-z]+)(_cut([0-9.]*))?$");
            if (hm.Success)
            {
                float cut = 0f;
                if (hm.Groups[1 + 1].Success)
                    cut = hm.Groups[3].Value.Length > 0 && float.TryParse(hm.Groups[3].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cv) ? cv : 1f;
                return new Info { kind = "hair", hair = hm.Groups[1].Value, cut = cut };
            }
            // garment: the longest known id the piece name starts with (jacket_zip -> jacket, vest_scrap_a -> vest_scrap)
            string id = null;
            foreach (var a in Alias.Keys) if (pc == a || pc.StartsWith(a + "_")) { id = a; break; }
            if (id == null)
                foreach (var k in known)
                    if ((pc == k || pc.StartsWith(k + "_")) && (id == null || k.Length > id.Length)) id = k;
            if (id == null) return new Info();
            string variant = null;
            if (variantOf.TryGetValue(id, out var vm)) { id = vm; }
            if (Alias.TryGetValue(id, out var al)) { variant = al.variant; id = al.id; }
            if (id == "underwear") return new Info { kind = "under", garment = id };
            return new Info { kind = "garment", garment = id, variant = variant };
        }

        static string RootProp(HDSidecar side, string key)
        {
            if (side.rootProps != null) foreach (var p in side.rootProps) if (p.k == key) return p.s;
            return null;
        }

        /// <summary>Index of a hex colour in HumanDesign.SkinTones (base shade) or HairColors (nearest), -1 if none.</summary>
        static int PaletteIndex(string hex, bool skin)
        {
            if (string.IsNullOrEmpty(hex) || !ColorUtility.TryParseHtmlString("#" + hex, out var c)) return -1;
            int best = -1; float bd = 0.02f;
            int n = skin ? HumanDesign.SkinTones.Length : HumanDesign.HairColors.Length;
            for (int i = 0; i < n; i++)
            {
                Color p = skin ? HumanDesign.SkinTones[i][0] : HumanDesign.HairColors[i];
                float d = Mathf.Abs(p.r - c.r) + Mathf.Abs(p.g - c.g) + Mathf.Abs(p.b - c.b);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        static int GeomHash(Mesh m)
        {
            var v = m.vertices;
            unchecked
            {
                int h = 17;
                for (int i = 0; i < v.Length; i += Mathf.Max(1, v.Length / 64))
                    h = h * 31 + Mathf.RoundToInt(v[i].x * 1000f) * 7 + Mathf.RoundToInt(v[i].y * 1000f) * 13 + Mathf.RoundToInt(v[i].z * 1000f);
                return h & 0x7fffffff;
            }
        }

        // ------------------------------------------------------------------ binding
        /// <summary>A copy of the renderer's mesh bound to the HumanRig rest pose: bone i = joint i with identity rotation
        /// at its rest position, so bindpose_i = T(-joint_i) x mesh-to-root. Rigid meshes become single-bone skins.</summary>
        static Mesh Bind(Renderer r, Transform root, Dictionary<string, Transform> bones, Dictionary<string, Vector3> heads, string name,
                         out Matrix4x4 meshToRoot, out string[] boneNames, out Mesh src)
        {
            meshToRoot = Matrix4x4.identity; boneNames = null; src = null;
            Mesh copy;
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh)
            {
                src = smr.sharedMesh;
                var bt = smr.bones;
                var bp = src.bindposes;
                int ref0 = -1;
                for (int i = 0; i < bt.Length && i < bp.Length; i++) if (bt[i]) { ref0 = i; break; }
                if (ref0 < 0) return null;
                meshToRoot = RootSpace(bt[ref0], root) * bp[ref0];
                boneNames = new string[bt.Length];
                var nb = new Matrix4x4[bt.Length];
                for (int i = 0; i < bt.Length; i++)
                {
                    string n = GameBone(bt[i]);
                    boneNames[i] = n;
                    nb[i] = Matrix4x4.Translate(-heads.GetValueOrDefault(n, Vector3.zero)) * meshToRoot;
                }
                copy = Object.Instantiate(src);
                copy.bindposes = nb;
            }
            else if (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh)
            {
                src = mf.sharedMesh;
                meshToRoot = RootSpace(r.transform, root);
                string n = GameBone(r.transform.parent);
                if (n == "Root") n = "Head";
                boneNames = new[] { n };
                copy = Object.Instantiate(src);
                var w = new BoneWeight[copy.vertexCount];
                for (int i = 0; i < w.Length; i++) w[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                copy.boneWeights = w;
                copy.bindposes = new[] { Matrix4x4.Translate(-heads.GetValueOrDefault(n, Vector3.zero)) * meshToRoot };
            }
            else return null;
            copy.name = name;
            return copy;
        }

        static string GameBone(Transform t)
        {
            for (var x = t; x; x = x.parent) if (System.Array.IndexOf(Bones, x.name) >= 0) return x.name;
            return "Root";
        }

        // ------------------------------------------------------------------ covers
        static Vector3[] SkinSpace(Mesh m, Matrix4x4 m2r, out Vector3[] normals)
        {
            var v = m.vertices; var n = m.normals;
            normals = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = m2r.MultiplyPoint3x4(v[i]);
                normals[i] = i < n.Length ? m2r.MultiplyVector(n[i]).normalized : Vector3.up;
            }
            return v;
        }

        const float Cell = 0.04f, Reach = 0.09f;

        /// <summary>Hidden-triangle bitset of a body under one garment piece, or null when it covers nothing.</summary>
        static int[] Cover(Vector3[] bv, Vector3[] bn, int[] btris, Mesh garment, Matrix4x4 g2r)
        {
            var gv = garment.vertices;
            for (int i = 0; i < gv.Length; i++) gv[i] = g2r.MultiplyPoint3x4(gv[i]);
            var gt = garment.triangles;
            var grid = new Dictionary<Vector3Int, List<int>>();
            var gmin = Vector3.positiveInfinity; var gmax = Vector3.negativeInfinity;
            for (int t = 0; t < gt.Length; t += 3)
            {
                Vector3 a = gv[gt[t]], b = gv[gt[t + 1]], c = gv[gt[t + 2]];
                var lo = Vector3.Min(a, Vector3.Min(b, c)); var hi = Vector3.Max(a, Vector3.Max(b, c));
                gmin = Vector3.Min(gmin, lo); gmax = Vector3.Max(gmax, hi);
                var l = Vector3Int.FloorToInt(lo / Cell); var h = Vector3Int.FloorToInt(hi / Cell);
                for (int x = l.x; x <= h.x; x++) for (int y = l.y; y <= h.y; y++) for (int z = l.z; z <= h.z; z++)
                {
                    var k = new Vector3Int(x, y, z);
                    if (!grid.TryGetValue(k, out var list)) grid[k] = list = new List<int>();
                    list.Add(t);
                }
            }
            var box = new Bounds(); box.SetMinMax(gmin - Vector3.one * Reach, gmax + Vector3.one * Reach);
            var covered = new bool[bv.Length];
            var cand = new HashSet<int>();
            bool any = false;
            for (int i = 0; i < bv.Length; i++)
            {
                if (!box.Contains(bv[i])) continue;
                var o = bv[i] - bn[i] * 0.003f;
                cand.Clear();
                for (float s = 0f; s <= Reach + Cell * 0.5f; s += Cell * 0.5f)
                    if (grid.TryGetValue(Vector3Int.FloorToInt((o + bn[i] * s) / Cell), out var list)) foreach (var t in list) cand.Add(t);
                foreach (var t in cand)
                    if (Ray(o, bn[i], gv[gt[t]], gv[gt[t + 1]], gv[gt[t + 2]], Reach)) { covered[i] = true; any = true; break; }
            }
            if (!any) return null;
            // erode two rings (welded by position: UV seams split vertices) so hems and cuffs keep skin under them
            var weld = new Dictionary<Vector3Int, int>();
            var id = new int[bv.Length];
            for (int i = 0; i < bv.Length; i++)
            {
                var k = Vector3Int.RoundToInt(bv[i] * 10000f);
                if (!weld.TryGetValue(k, out var w)) weld[k] = w = weld.Count;
                id[i] = w;
            }
            var wc = new bool[weld.Count];
            for (int i = 0; i < bv.Length; i++) wc[id[i]] = true;
            for (int i = 0; i < bv.Length; i++) if (!covered[i]) wc[id[i]] = false;
            for (int ring = 0; ring < 2; ring++)
            {
                var next = (bool[])wc.Clone();
                for (int t = 0; t < btris.Length; t += 3)
                {
                    int a = id[btris[t]], b = id[btris[t + 1]], c = id[btris[t + 2]];
                    if (!wc[a] || !wc[b] || !wc[c]) { next[a] = next[b] = next[c] = false; }
                }
                wc = next;
            }
            var bits = new int[(btris.Length / 3 + 31) / 32];
            bool hid = false;
            for (int t = 0; t < btris.Length; t += 3)
                if (wc[id[btris[t]]] && wc[id[btris[t + 1]]] && wc[id[btris[t + 2]]]) { int ti = t / 3; bits[ti >> 5] |= 1 << (ti & 31); hid = true; }
            return hid ? bits : null;
        }

        /// <summary>Segment o + d * [0, len] against a triangle (either side).</summary>
        static bool Ray(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, float len)
        {
            var e1 = b - a; var e2 = c - a;
            var p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f) return false;
            float inv = 1f / det;
            var s = o - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) return false;
            var q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(d, q) * inv;
            if (v < 0f || u + v > 1f) return false;
            float t = Vector3.Dot(e2, q) * inv;
            return t >= 0f && t <= len;
        }
    }
}

namespace MadMax.EditorTools
{
    /// <summary>Re-exported HD characters rebuild the character catalogue once the import settles.</summary>
    public class HDCharacterCatalogRefresh : UnityEditor.AssetPostprocessor
    {
        static bool queued;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (queued) return;
            foreach (var p in imported)
            {
                var n = p.Replace('\\', '/');
                if (!n.StartsWith(HDSidecar.Root + "/character/") || !n.EndsWith(".fbx") || n.Contains("/anims/")) continue;
                queued = true;
                UnityEditor.EditorApplication.delayCall += () => { queued = false; HDCharacterCatalogBuilder.Build(false); };
                return;
            }
        }
    }
}
