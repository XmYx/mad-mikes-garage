using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Recipes for depth stage I. Planks: the saw bench (hand saw: slow, 2 planks from 3 wood) → the sawmill
    /// (1.5 kW: three times as fast, 4 planks from 3 wood). Bricks: clay and water in the brick mould make green bricks,
    /// the kiln fires them (charcoal) into brick. Prefab concrete panels are cast at the mixer. Landmines (kits laid in
    /// build mode) at the gunsmith bench.</summary>
    public static partial class RecipeLibrary
    {
        public const string GreenBrick = "misc_green_brick";

        static IEnumerable<Recipe> DefenceRecipes()
        {
            var W = ResourceType.Wood; var Pl = ResourceType.Plank; var Wa = ResourceType.Water;

            // ---- planks
            var hand = Res("saw_planks", "SAW PLANKS X2", RecipeCategory.Refining, "saw_bench", Pl, 2, "HAND SAW: SLOW, AND A THIRD OF THE LOG IS SAWDUST", (W, 3));
            hand.seconds = 30f;
            yield return hand;
            var mill = Res("mill_planks", "MILL PLANKS X4", RecipeCategory.Refining, "sawmill", Pl, 4, "CIRCULAR SAW (1.5 KW): FAST, THIN KERF", (W, 3));
            mill.seconds = 10f;
            yield return mill;
            var bulk = Res("mill_planks_bulk", "MILL PLANKS X12", RecipeCategory.Refining, "sawmill", Pl, 12, "A WHOLE STACK OF LOGS THROUGH THE SAW", (W, 9));
            bulk.seconds = 26f;
            yield return bulk;

            // ---- bricks: mould, then fire
            var mould = Itm("mould_bricks", "GREEN BRICKS X6", RecipeCategory.Refining, "brick_mould", GreenBrick, 6, "CLAY AND WATER PACKED IN THE MOULD: FIRE THEM IN A KILN", null, (ResourceType.Clay, 4), (Wa, 2));
            mould.seconds = 20f;
            yield return mould;
            var fire = Res("fire_bricks", "FIRE BRICKS X6", RecipeCategory.Smelting, "kiln", ResourceType.Brick, 6, "GREEN BRICKS HARDEN IN THE KILN (CHARCOAL)", (ResourceType.Charcoal, 1));
            fire.items = new[] { (GreenBrick, 6) };
            fire.seconds = 40f;
            yield return fire;

            // ---- prefab concrete
            var panel = Itm("concrete_panel", "PREFAB CONCRETE PANEL", RecipeCategory.Building, "mixer", MadMax.Building.FurnitureLibrary.ConcretePanelKit, 1,
                "A CAST WALL PANEL IN A STEEL FRAME: BUILDS A PREFAB WALL OR DOORWAY", null, (ResourceType.Concrete, 5), (ResourceType.Iron, 1), (Wa, 1));
            panel.seconds = 30f;
            yield return panel;

            // ---- landmines
            yield return Itm("landmine", "LANDMINES X2", RecipeCategory.Weapons, "gunsmith", MadMax.Building.FurnitureLibrary.LandmineKit, 2,
                "TILT-ROD MINES: LAY THEM IN BUILD MODE (DEFENCE); YOU KNOW WHERE YOURS ARE", null, (ResourceType.Gunpowder, 2), (ResourceType.Iron, 2), (ResourceType.Scrap, 2));
        }
    }
}
