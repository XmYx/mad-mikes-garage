using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>"If you see it, you can have it" (salvage block): one smoke scenario.</summary>
    public static class SalvageAnythingScenarios
    {
        public static IEnumerable<Scenario> All() { yield return new SalvageSmoke(); }
    }

    /// <summary>Every city piece meshes and spawns; a small pristine world prop near the start comes apart into a kit with
    /// the right tool, is gone, its design is learned and the kit places it as a piece.</summary>
    class SalvageSmoke : Scenario
    {
        public override string Id => "build.salvage_smoke";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var at = g.Player.transform.position + Vector3.up * 200f;                               // out of everyone's way
            int pieces = 0;
            foreach (var d in FurnitureLibrary.All)
            {
                if (!d.id.StartsWith("city_")) continue;
                pieces++;
                if (!c.Check(d.mesh && d.mesh.bounds.size.sqrMagnitude > 0.01f, d.id + " has a mesh")) continue;
                var p = FurnitureLibrary.Spawn(d.id, g.Build.Structures, g.Build.Structures.InverseTransformPoint(at), Quaternion.identity, g.propMaterial);
                c.Check(p, d.id + " spawns");
                if (p) Object.Destroy(p.gameObject);
            }
            c.Metric("city_pieces", pieces, "");
            c.Check(pieces >= 20, pieces + " city pieces in the library");
            yield return null;

            DestructibleVoxels prop = null; float bd = float.MaxValue;
            foreach (var dv in DestructibleVoxels.All)
            {
                if (!dv || dv.Owned || dv.TemplateId == null || dv.TemplateId.StartsWith("site:") || dv.TemplateId.StartsWith("Stall_")) continue;
                if (!SalvageDesigns.Of(dv.Template, dv.voxelSize).Kit) continue;
                float d = (dv.transform.position - g.Player.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; prop = dv; }
            }
            if (!prop) { c.Block("no small pristine prop loaded near the player"); yield break; }
            var plan = g.SalvagePlanFor(prop, prop.transform.position);
            if (!c.Check(plan != null && plan.kit != null, "a plan with a kit for " + prop.TemplateId)) yield break;
            foreach (var t in SalvageDesigns.Tools(plan.survey.cls, plan.survey.plant)) g.Inventory.AddItem(t);
            c.Fixture("the tools for " + plan.survey.cls + " in the pack");
            string kit = plan.kit, piece = plan.pieceId;
            c.Check(g.TakeApartNow(g.SalvagePlanFor(prop, prop.transform.position)), "taken apart");
            yield return null;
            c.Check(!prop, "the prop is gone");
            c.Check(g.Inventory.GetItem(kit) > 0, "the kit " + kit + " is in the pack");
            c.Check(SalvageDesigns.Known.Contains(piece), "the design " + piece + " is known");
            c.Check(FurnitureLibrary.InCategory(BuildCategory.Salvaged).Exists(d => d.id == piece), "it shows in the SALVAGED build category");
            var placed = FurnitureLibrary.Spawn(piece, g.Build.Structures, g.Build.Structures.InverseTransformPoint(at), Quaternion.identity, g.propMaterial);
            c.Check(placed && placed.id == piece, "the design places as a piece");
            if (placed) Object.Destroy(placed.gameObject);
        }
    }
}
