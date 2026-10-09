using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Free-frame pieces (roadmap 29): beams and ladders placed point to point, spans laid over beam ends.
    /// Costs are per 2 m of beam / ladder and per 2 m² of span (<see cref="FrameCost"/>).</summary>
    public static partial class FurnitureLibrary
    {
        static IEnumerable<FurnitureDef> FramePieces()
        {
            var B = BuildCategory.Structure;
            FurnitureDef F(string id, string name, FrameKind kind, int style, int hits, string desc, params (ResourceType, int)[] cost)
            {
                var g = new VoxelGrid().Mat((byte)(style == 0 ? ResourceType.Wood : ResourceType.Scrap));
                g.Box(-1, -1, 0, 1, 1, 10, style == 0 ? Pal.Ramp(Pal.Wood, 2, 2910) : Pal.Ramp(Pal.Metal, 2, 2911));    // menu icon / fallback mesh
                var d = Def(id, name, g, cost);
                d.category = B; d.hits = hits; d.frame = kind; d.desc = desc;
                d.setup = go =>
                {
                    if (kind == FrameKind.Span) go.AddComponent<FrameSpan>().style = style;
                    else { var fb = go.AddComponent<FrameBeam>(); fb.kind = kind; fb.style = style; fb.SetLength(1f); }
                };
                return d;
            }
            yield return F("beam_wood", "TIMBER BEAM", FrameKind.Beam, 0, 6, "A BEAM FROM ANY POINT TO ANY POINT: CLICK THE START, THEN THE END. SNAPS TO OTHER BEAM ENDS. PER 2 M", (ResourceType.Wood, 2));
            yield return F("beam_steel", "STEEL BEAM", FrameKind.Beam, 1, 16, "A RIVETED I-BEAM FROM ANY POINT TO ANY POINT. PER 2 M", (ResourceType.Scrap, 3), (ResourceType.Iron, 1));
            yield return F("ladder_frame", "LADDER (ANY LENGTH)", FrameKind.Ladder, 0, 5, "FROM ITS FOOT TO ITS TOP: CLIMB IT, STEP OFF AT THE TOP. PER 2 M", (ResourceType.Wood, 2));
            yield return F("ladder_frame_steel", "STEEL LADDER", FrameKind.Ladder, 1, 12, "A WELDED LADDER FROM ITS FOOT TO ITS TOP. PER 2 M", (ResourceType.Scrap, 2));
            yield return F("span_planks", "PLANK SPAN", FrameKind.Span, 0, 6, "A FLOOR LAID OVER 3-6 BEAM ENDS: CLICK THEM, CLICK THE FIRST AGAIN (OR ENTER). PER 2 M2", (ResourceType.Wood, 2));
            yield return F("span_sheet", "SHEET SPAN", FrameKind.Span, 1, 14, "RIVETED SHEET OVER 3-6 BEAM ENDS: DRIVABLE WHEN LEVEL. PER 2 M2", (ResourceType.Scrap, 3));
            yield return F("span_grating", "GRATING SPAN", FrameKind.Span, 2, 10, "OPEN GRATING OVER 3-6 BEAM ENDS (LIGHT, LETS RAIN THROUGH). PER 2 M2", (ResourceType.Scrap, 2));
        }

        /// <summary>The cost of a frame piece of a given size (beams / ladders: metres; spans: square metres).</summary>
        public static (ResourceType type, int amount)[] FrameCost(FurnitureDef def, float size)
        {
            int units = Mathf.Max(1, Mathf.CeilToInt(size / 2f));
            var c = new (ResourceType, int)[def.cost.Length];
            for (int i = 0; i < c.Length; i++) c[i] = (def.cost[i].type, def.cost[i].amount * units);
            return c;
        }
    }
}
