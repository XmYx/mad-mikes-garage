using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Structure plans (blueprints of your own buildings): capture every built piece connected to the one
    /// under the cursor, then place the whole structure again from the build menu (Structure category, pays the summed
    /// cost). Kept with the save.</summary>
    public static class StructurePlans
    {
        public const int Max = 6;
        public struct Entry { public string id; public Vector3 pos; public Quaternion rot; public byte dye; }
        public class Plan { public List<Entry> pieces = new List<Entry>(); public FurnitureDef def; }

        static readonly List<Plan> plans = new List<Plan>();
        static FurnitureDef captureDef;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { plans.Clear(); captureDef = null; }

        public static Plan Get(int i) => i >= 0 && i < plans.Count ? plans[i] : null;
        public static int Count => plans.Count;

        /// <summary>Appends the capture tool and one pseudo piece per saved plan (mesh = the whole structure).</summary>
        public static void AddDefs(List<FurnitureDef> list)
        {
            if (captureDef == null || !captureDef.mesh)
            {
                var g = new MadMax.Voxel.VoxelGrid().Mat((byte)ResourceType.Cloth);
                g.Box(-4, 0, -6, 4, 0, 6, MadMax.Voxel.Pal.Ramp(MadMax.Voxel.Pal.PaleBlue, 2, 1795));
                g.Box(-4, 1, -6, 4, 1, -5, MadMax.Voxel.Pal.Ramp(MadMax.Voxel.Pal.Cream, 2, 1796));
                g.Bevel();
                captureDef = new FurnitureDef { id = "plan_capture", name = "NEW STRUCTURE PLAN", category = BuildCategory.Structure, cost = new (ResourceType, int)[0], plan = -2, mesh = MadMax.Voxel.VoxelMesher.Build(g, "Furniture_plan") };
            }
            list.Add(captureDef);
            for (int i = 0; i < plans.Count; i++) { if (plans[i].def == null || !plans[i].def.mesh) Build(i); list.Add(plans[i].def); }
        }

        /// <summary>Every piece touching <paramref name="anchor"/> (transitively) in the same root.</summary>
        public static List<Placeable> Connected(Placeable anchor)
        {
            var root = anchor.transform.parent;
            var pool = new List<Placeable>(); var boxes = new List<Bounds>();
            foreach (var p in Placeable.All)
            {
                if (!p || p.transform.parent != root || p.Collapsing || (p.transform.position - anchor.transform.position).sqrMagnitude > 60f * 60f) continue;
                var r = p.GetComponent<Renderer>(); if (!r) continue;
                var b = r.bounds; b.Expand(0.1f); pool.Add(p); boxes.Add(b);
            }
            var got = new List<Placeable>(); var seen = new bool[pool.Count]; var open = new Queue<int>();
            int a = pool.IndexOf(anchor); if (a < 0) return got;
            seen[a] = true; open.Enqueue(a);
            while (open.Count > 0)
            {
                int i = open.Dequeue(); got.Add(pool[i]);
                for (int j = 0; j < pool.Count; j++) if (!seen[j] && boxes[i].Intersects(boxes[j])) { seen[j] = true; open.Enqueue(j); }
            }
            return got;
        }

        /// <summary>Save the structure around <paramref name="anchor"/> as a new plan (oldest dropped past <see cref="Max"/>).
        /// Pieces made from kits or needing an item are left out.</summary>
        public static int Capture(Placeable anchor, out int skipped)
        {
            skipped = 0;
            var set = Connected(anchor);
            if (set.Count == 0) return 0;
            // frame: centroid of the piece origins at the lowest origin, yawed like the anchor
            Vector3 c = Vector3.zero; float low = float.MaxValue;
            foreach (var p in set) { c += p.transform.position; low = Mathf.Min(low, p.transform.position.y); }
            c /= set.Count; c.y = low;
            var yaw = Quaternion.Euler(0f, Mathf.Round(anchor.transform.eulerAngles.y / 90f) * 90f, 0f);
            var inv = Quaternion.Inverse(yaw);
            var plan = new Plan();
            foreach (var p in set)
            {
                var def = FurnitureLibrary.Get(p.id);
                if (def == null || def.kit != null || def.needsItem != null || def.category == BuildCategory.Hidden) { skipped++; continue; }
                plan.pieces.Add(new Entry { id = p.id, pos = inv * (p.transform.position - c), rot = inv * p.transform.rotation, dye = p.dye });
            }
            if (plan.pieces.Count == 0) return 0;
            plans.Add(plan);
            if (plans.Count > Max) plans.RemoveAt(0);
            for (int i = 0; i < plans.Count; i++) plans[i].def = null;                   // renumber names
            return plan.pieces.Count;
        }

        public static void Forget(int i) { if (i >= 0 && i < plans.Count) { plans.RemoveAt(i); for (int k = 0; k < plans.Count; k++) plans[k].def = null; } }

        static void Build(int i)
        {
            var plan = plans[i];
            var cost = new Dictionary<ResourceType, int>();
            var parts = new List<CombineInstance>();
            foreach (var e in plan.pieces)
            {
                var def = FurnitureLibrary.Get(e.id);
                if (def == null) continue;
                foreach (var (t, n) in def.cost) cost[t] = (cost.TryGetValue(t, out int have) ? have : 0) + n;
                if (def.mesh) parts.Add(new CombineInstance { mesh = def.mesh, transform = Matrix4x4.TRS(e.pos, e.rot, Vector3.one) });
            }
            var mesh = new Mesh { name = "Plan_" + i, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            var list = new List<(ResourceType, int)>();
            foreach (var kv in cost) list.Add((kv.Key, kv.Value));
            plan.def = new FurnitureDef { id = "plan_" + i, name = "PLAN " + (i + 1) + " (" + plan.pieces.Count + " PIECES)", category = BuildCategory.Structure, cost = list.ToArray(), plan = i, mesh = mesh };
        }

        // ------------------------------------------------------------------ save: plans '\n', pieces ';', fields ','
        public static string Save()
        {
            if (plans.Count == 0) return null;
            var sb = new StringBuilder(); var ci = CultureInfo.InvariantCulture;
            foreach (var plan in plans)
            {
                if (sb.Length > 0) sb.Append('\n');
                for (int k = 0; k < plan.pieces.Count; k++)
                {
                    var e = plan.pieces[k];
                    if (k > 0) sb.Append(';');
                    var r = e.rot.eulerAngles;
                    sb.Append(e.id).Append(',').Append(e.pos.x.ToString("0.###", ci)).Append(',').Append(e.pos.y.ToString("0.###", ci)).Append(',').Append(e.pos.z.ToString("0.###", ci))
                      .Append(',').Append(r.x.ToString("0.#", ci)).Append(',').Append(r.y.ToString("0.#", ci)).Append(',').Append(r.z.ToString("0.#", ci)).Append(',').Append(e.dye);
                }
            }
            return sb.ToString();
        }

        public static void Load(string s)
        {
            plans.Clear();
            if (string.IsNullOrEmpty(s)) return;
            var ci = CultureInfo.InvariantCulture;
            foreach (var line in s.Split('\n'))
            {
                var plan = new Plan();
                foreach (var item in line.Split(';'))
                {
                    var f = item.Split(',');
                    if (f.Length < 8 || FurnitureLibrary.Get(f[0]) == null) continue;
                    float F(int i) => float.Parse(f[i], ci);
                    plan.pieces.Add(new Entry { id = f[0], pos = new Vector3(F(1), F(2), F(3)), rot = Quaternion.Euler(F(4), F(5), F(6)), dye = byte.Parse(f[7], ci) });
                }
                if (plan.pieces.Count > 0) plans.Add(plan);
            }
        }
    }
}
