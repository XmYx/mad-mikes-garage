using System.Collections.Generic;
using MadMax.Rendering;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Choices and shared resources for HD humans (<see cref="HumanRig"/> in HD mode): which body shape, body,
    /// hair, beard and garment pieces of the <see cref="HDCharacterCatalog"/> a look wears, and the mesh / material
    /// variants every character with the same look shares: bodies with the skin under the worn garments cut away,
    /// worn-out garments with holes, arms-only meshes for first person, tinted skin / hair materials.</summary>
    public static class HDHuman
    {
        /// <summary>Body shapes of the HD pack (build_characters.py): key, female, height, build.</summary>
        public static readonly (string key, bool female, float h, float w)[] Shapes =
        {
            ("M", false, 1.0f, 1.0f), ("M2", false, 1.02f, 0.95f), ("F", true, 0.96f, 0.9f), ("F2", true, 0.95f, 0.88f),
        };

        /// <summary>Hair cut height per HD headwear (garments.py HEADWEAR): hair above it is cut away under the hat.</summary>
        static readonly Dictionary<string, float> HatCut = new Dictionary<string, float>
        {
            { "cowboy", 0.236f }, { "sunhat", 0.232f }, { "beanie", 0.226f }, { "helmet", 0.216f }, { "moto_helmet", 0.216f }, { "dive_helmet", 0.216f }, { "bee_veil", 0.232f },
        };

        /// <summary>HD humans are off (player flags --voxel-humans / --no-hd, MadMax > Dev > Voxel Humans / Voxel Visuals, or a test).</summary>
        public static bool ForceVoxel;

        public static bool Enabled => !ForceVoxel && !LaunchOptions.VoxelHumans && MadMax.Rendering.HDAssets.Enabled && HDCharacterCatalog.Instance != null;

        static readonly Dictionary<string, Mesh> variants = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> tinted = new Dictionary<string, Material>();
        static readonly Dictionary<string, bool[]> voxelCovers = new Dictionary<string, bool[]>();
        static Material bloodMat;
        static Mesh[] bloodMeshes;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { variants.Clear(); tinted.Clear(); voxelCovers.Clear(); bloodMat = null; bloodMeshes = null; ForceVoxel = false; }

        // ------------------------------------------------------------------ choices
        /// <summary>The body shape for a look: <see cref="Appearance.body"/> when set (0 M, 1 M2, 2 F, 3 F2), else the
        /// male shape closest to the build; a shape missing from the catalogue falls back to one of the same sex.</summary>
        public static string ShapeFor(Appearance a, HDCharacterCatalog cat)
        {
            int want = a.body;
            if (want < 0 || want >= Shapes.Length) want = a.build >= 0.975f ? 0 : 1;
            var s = Shapes[want];
            if (cat.shapes.Contains(s.key)) return s.key;
            string best = null; float bd = float.MaxValue;
            foreach (var t in Shapes)
            {
                if (!cat.shapes.Contains(t.key)) continue;
                float d = (t.female != s.female ? 10f : 0f) + Mathf.Abs(t.w - s.w) + Mathf.Abs(t.h - s.h);
                if (d < bd) { bd = d; best = t.key; }
            }
            return best ?? (cat.shapes.Count > 0 ? cat.shapes[0] : null);
        }

        /// <summary>Body shape index for a person: 2/3 (F/F2) for women, 0/1 (M/M2) for men, by build.</summary>
        public static int BodyFor(bool female, float build) => female ? (build >= 0.95f ? 2 : 3) : (build >= 0.975f ? 0 : 1);

        public static float HeadwearCut(IEnumerable<string> outfit, HDCharacterCatalog cat, string shape, out bool voxelHat)
        {
            float cut = 0f; voxelHat = false;
            foreach (var id in outfit)
            {
                var d = ClothingLibrary.Get(id);
                if (d == null || d.slot != ClothingSlot.Head) continue;
                if (HatCut.TryGetValue(d.id, out var c)) cut = Mathf.Max(cut, c);
                if (!HasGarment(cat, shape, d.id)) { voxelHat = true; if (cut <= 0f) cut = 0.216f; }
            }
            return cut;
        }

        public static bool HasGarment(HDCharacterCatalog cat, string shape, string id)
        {
            foreach (var p in cat.pieces) if (p.kind == "garment" && p.shape == shape && p.garment == id) return true;
            return false;
        }

        /// <summary>Pieces of one garment for a shape; a worn-out t-shirt takes the "worn" variant when there is one.</summary>
        public static List<HDCharacterCatalog.Piece> GarmentPieces(HDCharacterCatalog cat, string shape, string id, float condition)
        {
            var plain = new List<HDCharacterCatalog.Piece>();
            var byVariant = new Dictionary<string, List<HDCharacterCatalog.Piece>>();
            foreach (var p in cat.pieces)
            {
                if (p.kind != "garment" || p.shape != shape || p.garment != id) continue;
                if (string.IsNullOrEmpty(p.variant)) plain.Add(p);
                else { if (!byVariant.TryGetValue(p.variant, out var l)) byVariant[p.variant] = l = new List<HDCharacterCatalog.Piece>(); l.Add(p); }
            }
            if (condition < 0.6f && byVariant.TryGetValue("worn", out var worn)) return worn;
            if (plain.Count > 0) return plain;
            foreach (var kv in byVariant) if (kv.Key != "up") return kv.Value;
            foreach (var kv in byVariant) return kv.Value;
            return plain;
        }

        /// <summary>The body for a shape: one baked in the wanted skin tone, else a tintable one, else any.</summary>
        public static HDCharacterCatalog.Piece Body(HDCharacterCatalog cat, string shape, int tone)
        {
            HDCharacterCatalog.Piece any = null, paint = null;
            foreach (var p in cat.pieces)
            {
                if (p.kind != "body" || p.shape != shape) continue;
                if (p.toneRef == tone) return p;
                if (p.paint) paint ??= p;
                any ??= p;
            }
            return paint ?? any;
        }

        public static HDCharacterCatalog.Piece Hair(HDCharacterCatalog cat, string shape, HairStyle style, float cut, bool hideIfUncut)
        {
            if (style == HairStyle.Bald) return null;
            string name = style.ToString();
            HDCharacterCatalog.Piece full = null, best = null; float bd = float.MaxValue;
            foreach (var p in cat.pieces)
            {
                if (p.kind != "hair" || p.shape != shape || p.hair != name) continue;
                if (p.cut <= 0f) { full ??= p; continue; }
                float d = Mathf.Abs(p.cut - cut);
                if (d < bd) { bd = d; best = p; }
            }
            if (cut <= 0f) return full;
            if (best != null) return best;
            return hideIfUncut ? null : full;
        }

        public static HDCharacterCatalog.Piece First(HDCharacterCatalog cat, string shape, string kind, string piece = null)
        {
            foreach (var p in cat.pieces) if (p.kind == kind && p.shape == shape && (piece == null || p.piece == piece)) return p;
            return null;
        }

        public static IEnumerable<HDCharacterCatalog.Piece> All(HDCharacterCatalog cat, string shape, string kind)
        {
            foreach (var p in cat.pieces) if (p.kind == kind && p.shape == shape) yield return p;
        }

        // ------------------------------------------------------------------ materials
        static Color TintFor(int kind, int tone, int toneRef)
        {
            if (kind == 0 || toneRef < 0 || tone == toneRef) return Color.white;
            Color32 a, b;
            if (kind == 1)
            {
                var t = HumanDesign.SkinTones;
                a = t[Mathf.Clamp(tone, 0, t.Length - 1)][0]; b = t[Mathf.Clamp(toneRef, 0, t.Length - 1)][0];
            }
            else
            {
                var h = HumanDesign.HairColors;
                a = h[Mathf.Clamp(tone, 0, h.Length - 1)]; b = h[Mathf.Clamp(toneRef, 0, h.Length - 1)];
            }
            Color la = ((Color)a).linear, lb = ((Color)b).linear;
            var r = new Color(Mathf.Min(8f, la.r / Mathf.Max(lb.r, 0.004f)), Mathf.Min(8f, la.g / Mathf.Max(lb.g, 0.004f)), Mathf.Min(8f, la.b / Mathf.Max(lb.b, 0.004f)), 1f);
            return r.gamma;      // the material converts the sRGB tint back to this linear ratio
        }

        /// <summary>The piece's material, tinted to the look's skin tone / hair colour (one shared copy per tint).</summary>
        public static Material MaterialFor(HDCharacterCatalog.Piece p, Appearance a)
        {
            var m = p.material;
            if (!m) return null;
            int tone = p.tint == 1 ? a.skinTone : p.tint == 2 ? a.hairColor : -1;
            if (p.tint == 0 || tone < 0 || tone == p.toneRef) return m;
            string key = m.GetEntityId() + ":" + tone;
            if (tinted.TryGetValue(key, out var t) && t) return t;
            t = new Material(m) { name = m.name + "_t" + tone };
            if (p.paint && t.HasProperty(HDModel.PaintRefId))
            {
                // the exporter's tint mask: albedo x colour / factory colour where the mask says skin / hair
                Color32 c = p.tint == 1 ? HumanDesign.SkinTones[Mathf.Clamp(tone, 0, HumanDesign.SkinTones.Length - 1)][0]
                                        : HumanDesign.HairColors[Mathf.Clamp(tone, 0, HumanDesign.HairColors.Length - 1)];
                var col = (Color)c; col.a = 1f;
                t.SetColor(HDModel.PaintColorId, col);
            }
            else t.SetColor("_Tint", TintFor(p.tint, tone, p.toneRef));
            tinted[key] = t;
            return t;
        }

        // ------------------------------------------------------------------ mesh variants
        static Mesh Variant(Mesh src, string what, System.Func<Mesh, int[][]> make)
        {
            if (!src) return null;
            string key = src.GetEntityId() + "|" + what;
            if (variants.TryGetValue(key, out var m)) { if (m || m is null) return m; }
            var subs = make(src);
            if (subs == null) { variants[key] = src; return src; }
            int total = 0;
            foreach (var s in subs) total += s.Length;
            if (total == 0) { variants[key] = null; return null; }
            m = Object.Instantiate(src);
            m.name = src.name + "_" + what;
            for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i, false);
            m.bounds = src.bounds;
            variants[key] = m;
            return m;
        }

        static bool Bit(int[] bits, int i) => bits != null && (i >> 5) < bits.Length && (bits[i >> 5] & (1 << (i & 31))) != 0;

        /// <summary>The body with the triangles under the worn garments removed: HD garments by their baked cover,
        /// voxel garments (no HD mesh) by their coverage along the bones.</summary>
        public static Mesh MaskedBody(HDCharacterCatalog cat, HDCharacterCatalog.Piece body, int lod, List<string> hdGarmentKeys, List<ClothingDef> voxelGarments)
        {
            var src = body.lods[lod];
            if (!src) return null;
            if (hdGarmentKeys.Count == 0 && voxelGarments.Count == 0) return src;
            var keys = new List<string>(hdGarmentKeys);
            foreach (var d in voxelGarments) keys.Add("vox:" + d.id);
            keys.Sort(System.StringComparer.Ordinal);
            string what = "mask" + lod + ":" + string.Join(",", keys);
            return Variant(src, what, m =>
            {
                var covers = new List<int[]>();
                foreach (var k in hdGarmentKeys) { var c = cat.CoverOf(body.geom, lod, k); if (c != null) covers.Add(c.bits); }
                bool[] vox = voxelGarments.Count > 0 ? VoxelCover(m, body, voxelGarments) : null;
                var subs = new int[m.subMeshCount][];
                int triBase = 0;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var tris = m.GetTriangles(s);
                    var keep = new List<int>(tris.Length);
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        int ti = triBase + t / 3;
                        bool hide = false;
                        foreach (var b in covers) if (Bit(b, ti)) { hide = true; break; }
                        if (!hide && vox != null) hide = vox[tris[t]] && vox[tris[t + 1]] && vox[tris[t + 2]];
                        if (!hide) { keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]); }
                    }
                    triBase += tris.Length / 3;
                    subs[s] = keep.ToArray();
                }
                return subs;
            });
        }

        static readonly BodyPart[] partsByName = (BodyPart[])System.Enum.GetValues(typeof(BodyPart));

        static int[] BoneParts(HDCharacterCatalog.Piece p)
        {
            var map = new int[p.bones.Length];
            for (int i = 0; i < map.Length; i++) map[i] = System.Enum.TryParse<BodyPart>(p.bones[i], out var bp) ? (int)bp : -1;
            return map;
        }

        /// <summary>Per vertex: covered by one of the voxel garments (dominant bone + position along it inside the garment's
        /// coverage, shrunk 6 % at both ends so seams never open).</summary>
        static bool[] VoxelCover(Mesh m, HDCharacterCatalog.Piece body, List<ClothingDef> defs)
        {
            var verts = m.vertices;
            var weights = m.boneWeights;
            var bind = m.bindposes;
            var parts = BoneParts(body);
            var skel = HumanDesign.Skeleton(new Appearance());
            var len = new float[partsByName.Length];
            foreach (var b in skel) len[(int)b.part] = b.length;
            var res = new bool[verts.Length];
            if (weights == null || weights.Length != verts.Length) return res;
            for (int v = 0; v < verts.Length; v++)
            {
                int bi = weights[v].boneIndex0;
                if (bi < 0 || bi >= parts.Length || parts[bi] < 0) continue;
                var part = (BodyPart)parts[bi];
                // bone space (identity rest rotation): limbs hang along -y, pelvis / chest / head grow along +y
                var local = bind[bi].MultiplyPoint3x4(verts[v]);
                bool down = part >= BodyPart.UpperArmL;
                float t = (down ? -local.y : local.y) / Mathf.Max(0.01f, len[(int)part]);
                foreach (var d in defs)
                {
                    if (!d.coverage.TryGetValue(part, out var r)) continue;
                    if (t > r.x + 0.06f && t < r.y - 0.06f) { res[v] = true; break; }
                }
            }
            return res;
        }

        static readonly HashSet<BodyPart> Arms = new HashSet<BodyPart> { BodyPart.UpperArmL, BodyPart.UpperArmR, BodyPart.ForearmL, BodyPart.ForearmR, BodyPart.HandL, BodyPart.HandR };

        /// <summary>First person: only the triangles skinned to the arms and hands (null when nothing is left).</summary>
        public static Mesh ArmsOnly(Mesh src, HDCharacterCatalog.Piece p)
        {
            return Variant(src, "arms", m =>
            {
                var w = m.boneWeights;
                var parts = BoneParts(p);
                if (w == null || w.Length != m.vertexCount) return new int[m.subMeshCount][];
                bool Arm(int v) { int b = w[v].boneIndex0; return b >= 0 && b < parts.Length && parts[b] >= 0 && Arms.Contains((BodyPart)parts[b]); }
                var subs = new int[m.subMeshCount][];
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var tris = m.GetTriangles(s);
                    var keep = new List<int>();
                    for (int t = 0; t < tris.Length; t += 3)
                        if (Arm(tris[t]) && Arm(tris[t + 1]) && Arm(tris[t + 2])) { keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]); }
                    subs[s] = keep.ToArray();
                }
                return subs;
            });
        }

        /// <summary>A worn-out garment: ragged holes (about a quarter of the cloth) where a 3D noise peaks.</summary>
        public static Mesh Torn(Mesh src)
        {
            return Variant(src, "torn", m =>
            {
                var v = m.vertices;
                var subs = new int[m.subMeshCount][];
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var tris = m.GetTriangles(s);
                    var keep = new List<int>(tris.Length);
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        var c = (v[tris[t]] + v[tris[t + 1]] + v[tris[t + 2]]) / 3f;
                        float n = Noise(c * 11f) * 0.7f + Noise(c * 29f) * 0.3f;
                        if (n < 0.66f) { keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]); }
                    }
                    subs[s] = keep.ToArray();
                }
                return subs;
            });
        }

        static float Hash(int x, int y, int z)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + z * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static float Noise(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = p.x - x, fy = p.y - y, fz = p.z - z;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
            float L(float a, float b, float t) => a + (b - a) * t;
            return L(L(L(Hash(x, y, z), Hash(x + 1, y, z), fx), L(Hash(x, y + 1, z), Hash(x + 1, y + 1, z), fx), fy),
                     L(L(Hash(x, y, z + 1), Hash(x + 1, y, z + 1), fx), L(Hash(x, y + 1, z + 1), Hash(x + 1, y + 1, z + 1), fx), fy), fz);
        }

        // ------------------------------------------------------------------ blood
        /// <summary>A splat of blood (flat decal mesh, dark red vertex colour) and its material (HD voxel look).</summary>
        public static Mesh BloodMesh(int i, out Material mat)
        {
            if (bloodMeshes == null || !bloodMeshes[0])
            {
                bloodMeshes = new Mesh[4];
                var rnd = new System.Random(77);
                for (int k = 0; k < bloodMeshes.Length; k++)
                {
                    const int n = 9;
                    var verts = new List<Vector3> { Vector3.zero };
                    var cols = new List<Color32> { new Color32(86, 8, 10, 255) };
                    var tris = new List<int>();
                    for (int j = 0; j < n; j++)
                    {
                        float a = j / (float)n * Mathf.PI * 2f, r = 0.022f + (float)rnd.NextDouble() * 0.02f;
                        verts.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
                        cols.Add(new Color32(64, 6, 8, 255));
                        tris.Add(0); tris.Add(1 + (j + 1) % n); tris.Add(1 + j);
                    }
                    var mesh = new Mesh { name = "BloodSplat" + k };
                    mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
                    var nrm = new Vector3[verts.Count];
                    for (int j = 0; j < nrm.Length; j++) nrm[j] = Vector3.back;
                    mesh.normals = nrm;
                    mesh.RecalculateBounds();
                    bloodMeshes[k] = mesh;
                }
            }
            if (!bloodMat)
            {
                var basis = HDModel.VoxelMaterial;
                if (basis)
                {
                    bloodMat = new Material(basis) { name = "HDBlood" };
                    if (bloodMat.HasProperty("_Cull")) bloodMat.SetFloat("_Cull", 0f);
                    if (bloodMat.HasProperty("_Smoothness")) bloodMat.SetFloat("_Smoothness", 0.75f);
                }
            }
            mat = bloodMat;
            return bloodMeshes[Mathf.Abs(i) % bloodMeshes.Length];
        }
    }
}
