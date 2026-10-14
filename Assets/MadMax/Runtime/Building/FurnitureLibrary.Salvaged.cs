using System.Collections.Generic;
using MadMax.Items;
using MadMax.Rendering;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Salvaged designs: every world prop template (trees, rocks, shacks, houses, towers, pumps, stalls,
    /// landmarks...) is a pseudo piece <c>prop:&lt;template id&gt;</c>, resolved on demand by <see cref="Get"/>: the
    /// template's own voxels at its own voxel size, the HD model of the template over it, and a price of what breaking it
    /// would yield (<see cref="SalvageDesigns.Of"/>). They show in the SALVAGED build category once learned by taking one
    /// apart (or while a kit of one is in the pack).</summary>
    public static partial class FurnitureLibrary
    {
        static readonly Dictionary<string, FurnitureDef> salvaged = new Dictionary<string, FurnitureDef>();

        static FurnitureDef SalvagedDef(string id)
        {
            if (id == null || !id.StartsWith(SalvageDesigns.PropPrefix)) return null;
            if (salvaged.TryGetValue(id, out var d) && d.mesh) return d;
            string tid = id.Substring(SalvageDesigns.PropPrefix.Length);
            if (tid.StartsWith("site:")) return null;
            var grid = MadMax.World.PropLibrary.TemplateGrid(tid);
            if (grid == null || grid.Count == 0) return null;
            float size = MadMax.World.PropLibrary.TemplateSize(tid);
            var sv = SalvageDesigns.Of(grid, size, SalvageDesigns.IsPlant(tid));
            d = new FurnitureDef
            {
                id = id, name = SalvageDesigns.TemplateName(tid), category = BuildCategory.Salvaged, grid = grid, voxel = size,
                cost = sv.cost, hits = Mathf.Clamp(sv.voxels / 150, 3, 60), meshCollider = !sv.Kit,      // walk into what you rebuilt
                mesh = VoxelMesher.Build(grid, "Salvaged_" + tid, size),                                  // the template is bevelled already
                desc = "TAKEN APART OUT THERE AND LEARNED: BUILT FROM WHAT IT IS MADE OF" + (sv.Kit ? ". COMES APART INTO A KIT AGAIN" : ""),
                setup = go => DressSalvaged(go, tid),
            };
            if (sv.Kit) SalvageDesigns.Portable.Add(id);
            salvaged[id] = d;
            return d;
        }

        /// <summary>The template's HD model (world domain) over the rebuilt piece, plus what made it useful out there.</summary>
        static void DressSalvaged(GameObject go, string tid)
        {
            var mat = go.GetComponent<MeshRenderer>() ? go.GetComponent<MeshRenderer>().sharedMaterial : null;
            HDVisual.Dress(go, HDDomain.World, tid, MadMax.World.HDProp.FlagsOf(mat));
            if (tid.StartsWith("GasPump"))
            {
                var gp = go.AddComponent<MadMax.World.GasPump>();
                gp.stock = 0f; gp.nozzle = new Vector3(0.5f, 0.8f, 0.15f);
            }
            if (tid.StartsWith("Crate") || tid.StartsWith("Barrel")) Box(go, tid.StartsWith("Crate") ? "CRATE" : "BARREL", 40f, false);
        }

        /// <summary>The SALVAGED category: learned designs (props and hidden pieces) and those whose kit is in the pack.</summary>
        static void SalvagedDefs(List<FurnitureDef> into)
        {
            var seen = new HashSet<string>();
            void Add(string pid)
            {
                if (pid == null || !seen.Add(pid)) return;
                var d = Get(pid);
                if (d == null || (d.category != BuildCategory.Salvaged && d.category != BuildCategory.Hidden)) return;   // everything else has its own category
                if (d.category == BuildCategory.Hidden && d.link != UtilityKind.None) return;
                into.Add(d);
            }
            foreach (var k in SalvageDesigns.Known) Add(k);
            if (SalvageDesigns.Pack != null)
                foreach (var kv in SalvageDesigns.Pack.Items) if (kv.Value > 0 && SalvageDesigns.IsKit(kv.Key)) Add(SalvageDesigns.PieceOfKit(kv.Key));
            into.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }
    }
}
