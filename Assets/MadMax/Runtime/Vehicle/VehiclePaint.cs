using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Paint job and decal (roadmap 19). A repaint swaps the vehicle's main body colour — the palette ramp
    /// most of its paint comes from — for the same shades of the new colour, on the body and its cut panels (doors,
    /// hood); stripes, crosses, flames, glass, chrome, rust and rubber stay. A decal is a small voxel emblem on both
    /// flanks. Saved as "colour,decal" (<c>VehicleSave.paint</c>).</summary>
    public class VehiclePaint : MonoBehaviour
    {
        public int colour, decal;

        public static readonly string[] ColourNames = { "FACTORY", "RED", "BLUE", "GREEN", "YELLOW", "BLACK", "WHITE", "RIG GREEN", "DESERT", "BRONZE", "SKY", "PRIMER" };
        static readonly Color32[] Primer = { Pal.Metal[2], Pal.Metal[3], Pal.Chrome[0], Pal.Chrome[1] };

        public static Color32[] Ramp(int c) => c switch
        {
            1 => Pal.Crimson, 2 => Pal.Navy, 3 => Pal.Moss, 4 => Pal.Ochre, 5 => Pal.Black, 6 => Pal.Cream,
            7 => Pal.RigGreen, 8 => Pal.Sand, 9 => Pal.Bronze, 10 => Pal.PaleBlue, 11 => Primer, _ => null
        };

        /// <summary>The dye item a colour is mixed from (2 per job); null = scrap and oil.</summary>
        public static string Dye(int c) => c >= 1 && c <= 6 ? "dye_" + new[] { "red", "blue", "green", "yellow", "black", "white" }[c - 1] : null;

        public static VehiclePaint Of(VehicleDriver v)
        {
            if (!v) return null;
            return v.TryGetComponent<VehiclePaint>(out var p) ? p : v.gameObject.AddComponent<VehiclePaint>();
        }

        public static int DecalOf(VehicleDriver v) => v && v.TryGetComponent<VehiclePaint>(out var p) ? p.decal : 0;

        public string SaveState() => colour + "," + decal;

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var a = s.Split(',');
            if (a.Length > 0) int.TryParse(a[0], out colour);
            if (a.Length > 1) int.TryParse(a[1], out decal);
            Apply();
        }

        // ------------------------------------------------------------------ colour classes (pure data, built once)
        static Dictionary<int, (int ramp, int idx, int bevel)> paintKeys;
        static Color32[][] paintRamps;
        static readonly Color32 HiLite = new Color32(255, 236, 200, 255), Shade = new Color32(0, 0, 0, 255);
        static int Key(Color32 c) => c.r << 16 | c.g << 8 | c.b;
        /// <summary>A ramp colour as VoxelGrid.Bevel leaves it: 0 flat, 1 top edge, 2 bottom edge.</summary>
        static Color32 Bev(Color32 c, int bevel) => bevel == 1 ? Color32.Lerp(c, HiLite, 0.22f) : bevel == 2 ? Color32.Lerp(c, Shade, 0.25f) : c;

        static void Ensure()
        {
            if (paintKeys != null) return;
            var fixedKeys = new HashSet<int>();
            foreach (var ramp in new[] { Pal.Glass, Pal.Chrome, Pal.Tire, Pal.Metal, Pal.Rust, Pal.Skin, Pal.Wood })
                foreach (var c in ramp) for (int b = 0; b < 3; b++) fixedKeys.Add(Key(Bev(c, b)));
            foreach (var c in new[] { Pal.Void, Pal.LightY, Pal.LightW, Pal.TailR, Pal.Amber }) for (int b = 0; b < 3; b++) fixedKeys.Add(Key(Bev(c, b)));
            paintRamps = new[] { Pal.Ochre, Pal.Olive, Pal.Sand, Pal.Cream, Pal.PaleBlue, Pal.RigGreen, Pal.Crimson, Pal.Navy, Pal.Moss, Pal.Black, Pal.Bronze };
            paintKeys = new Dictionary<int, (int, int, int)>();
            for (int r = 0; r < paintRamps.Length; r++)
            for (int i = 0; i < paintRamps[r].Length; i++)
            for (int b = 0; b < 3; b++)
            {
                int k = Key(Bev(paintRamps[r][i], b));
                if (!fixedKeys.Contains(k) && !paintKeys.ContainsKey(k)) paintKeys[k] = (r, i, b);
            }
        }

        // ------------------------------------------------------------------ applying
        readonly Dictionary<MeshFilter, Color32[]> originals = new Dictionary<MeshFilter, Color32[]>();
        readonly HashSet<Mesh> owned = new HashSet<Mesh>();
        readonly List<MeshFilter> targets = new List<MeshFilter>();
        Transform decalR, decalL;

        void OnDestroy() { foreach (var m in owned) if (m) Destroy(m); }

        /// <summary>Body, roof and the cut panels (doors, hood) mounted right now.</summary>
        void Collect()
        {
            targets.Clear();
            var body = transform.Find("Body");
            if (body)
            {
                if (body.TryGetComponent<MeshFilter>(out var bf) && bf.sharedMesh) targets.Add(bf);
                foreach (Transform c in body)
                    if (c.name != "Glass" && c.name != "Driver" && c.name != "Legs" && !c.name.StartsWith("Decal") && c.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh) targets.Add(mf);
            }
            foreach (var p in GetComponentsInChildren<VehiclePart>())
                if ((p.category == PartCategory.Door || p.category == PartCategory.Hood) && p.Socket && p.TryGetComponent<MeshFilter>(out var pf) && pf.sharedMesh) targets.Add(pf);
        }

        Color32[] Original(MeshFilter mf)
        {
            if (!originals.TryGetValue(mf, out var o)) { o = mf.sharedMesh.colors32; originals[mf] = o; }
            return o;
        }

        Mesh Writable(MeshFilter mf)
        {
            var m = mf.sharedMesh;
            if (owned.Contains(m) || m.name.EndsWith("(dented)")) return m;          // our copy, or the dent copy: recolour in place
            var copy = Instantiate(m);
            copy.name = m.name + " (painted)";
            mf.sharedMesh = copy;
            owned.Add(copy);
            return copy;
        }

        public void Apply()
        {
            Ensure();
            Collect();
            var dst = Ramp(colour);
            // the main paint: the ramp the most painted vertices come from
            var counts = new int[paintRamps.Length];
            foreach (var mf in targets) foreach (var c in Original(mf)) if (paintKeys.TryGetValue(Key(c), out var k)) counts[k.ramp]++;
            int main = -1, best = 0;
            for (int r = 0; r < counts.Length; r++) if (counts[r] > best) { best = counts[r]; main = r; }
            foreach (var mf in targets)
            {
                var src = Original(mf);
                if (dst == null && !owned.Contains(mf.sharedMesh) && !mf.sharedMesh.name.EndsWith("(dented)")) continue;   // factory and untouched
                var cols = new Color32[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var c = src[i];
                    if (dst != null && main >= 0 && paintKeys.TryGetValue(Key(c), out var k) && k.ramp == main)
                    {
                        int n = paintRamps[main].Length;
                        int di = Mathf.RoundToInt(k.idx / (float)Mathf.Max(1, n - 1) * (dst.Length - 1));
                        var nc = Bev(dst[di], k.bevel);
                        nc.a = c.a;
                        c = nc;
                    }
                    cols[i] = c;
                }
                Writable(mf).colors32 = cols;
            }
            UpdateDecals();
        }

        void UpdateDecals()
        {
            var body = transform.Find("Body");
            var mesh = decal > 0 ? Decals.Mesh(decal) : null;
            if (!body || !mesh) { if (decalR) decalR.gameObject.SetActive(false); if (decalL) decalL.gameObject.SetActive(false); return; }
            var bf = body.GetComponent<MeshFilter>();
            var b = bf && bf.sharedMesh ? bf.sharedMesh.bounds : new Bounds(Vector3.up, Vector3.one * 2f);
            var mat = body.GetComponent<MeshRenderer>() ? body.GetComponent<MeshRenderer>().sharedMaterial : null;
            decalR = Decal(body, decalR, "Decal_R", mesh, mat, new Vector3(b.max.x + 0.005f, b.min.y + b.size.y * 0.42f, b.center.z), 90f);
            decalL = Decal(body, decalL, "Decal_L", mesh, mat, new Vector3(b.min.x - 0.005f, b.min.y + b.size.y * 0.42f, b.center.z), -90f);
        }

        static Transform Decal(Transform body, Transform t, string name, Mesh mesh, Material mat, Vector3 local, float yaw)
        {
            if (!t)
            {
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                t = go.transform;
                t.SetParent(body, false);
                var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            t.gameObject.SetActive(true);
            t.localPosition = local;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            t.GetComponent<MeshFilter>().sharedMesh = mesh;
            return t;
        }
    }
}
