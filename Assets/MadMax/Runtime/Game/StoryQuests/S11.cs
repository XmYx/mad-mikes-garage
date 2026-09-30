using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S11 LETTERS NOBODY STOLE in the world: Reva's post at the town's edge, Hal's empty cottage with the note on
    /// the door, Bee Hollow, Hal's sister's place and the orchard on the Pells' old plot. While it runs: letters that were
    /// burned or left with Noor leave the pack, and the closing line follows the choices.</summary>
    public partial class WastelandGame
    {
        string s11Ida, s11Pell;
        bool s11Set;

        partial void Scene_S11()
        {
            if (!Build || !Build.Structures) return;
            // Reva's post: an awning over a sorting table, the postbag crate, a bench and a lamp
            Q2Put("reva", "porch_awning", new Vector3(0f, 0f, -1.6f), 0f);
            Q2Put("reva", "table", new Vector3(0f, 0f, -1.5f), 0f);
            Q2Put("reva", "crate", new Vector3(1.5f, 0f, -2.2f), 10f);
            Q2Put("reva", "bench", new Vector3(-2f, 0f, -1.4f), 90f);
            Q2Put("reva", "lamp", new Vector3(0.9f, 0f, -2.4f), 0f);
            // Hal's old cottage: a one-room shed, empty, the note pinned to a post by the door
            Q2House("s11_old", new Vector3(0f, 0f, -3f), 1, 1, "wall_wood", "doorway_wood", null, true);
            Q2Put("s11_old", "sign", new Vector3(1.6f, 0f, -1.4f), 0f);
            Q2Put("s11_old", "tyres", new Vector3(-2.2f, 0f, -2.2f), 30f);
            // Bee Hollow: a small house, a porch chair, flower pots and a bed of flowers for the bees
            Q2House("s11_ida", new Vector3(0f, 0f, -5f), 2, 2, "wall_wood", "doorway_wood", "wall_wood_window", true);
            Q2Put("s11_ida", "chair", new Vector3(1.4f, 0f, -2.2f), 0f);
            foreach (float x in new[] { -3.4f, -2.8f, 3.2f }) Q2Put("s11_ida", "flower_pot", new Vector3(x, 0f, -2.4f), 0f);
            var bed = Q2Put("s11_ida", "garden_plot", new Vector3(-4.2f, 0f, 0.2f), 90f);
            if (bed && bed.TryGetComponent<GardenPlot>(out var plot)) { plot.crop = "seed_flower"; plot.growth = 0.9f; plot.water = 0.6f; bed.Dirty(); }
            // Hal's sister's place: brick, a porch with a bench, a table and a radio
            Q2House("s11_hal", new Vector3(0f, 0f, -5f), 2, 2, "wall_brick", "doorway_brick", "wall_brick_window", true);
            Q2Put("s11_hal", "porch_awning", new Vector3(0f, 0f, -2.4f), 0f);
            Q2Put("s11_hal", "bench", new Vector3(-1.6f, 0f, -2f), 0f);
            Q2Put("s11_hal", "table", new Vector3(0.8f, 0f, -1.8f), 0f);
            Q2Put("s11_hal", "radio", new Vector3(2.4f, 0f, -2.4f), 180f);
            // the orchard on the Pells' plot: three rows of apple trees, Noor's crates and bench
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    var t = Q2Put("s11_orchard", "tree_planted", new Vector3(-4f + col * 4f, 0f, -3.5f - row * 4f), row * 37f + col * 71f);
                    if (t && t.TryGetComponent<PlantedTree>(out var tree)) { tree.sapling = "sapling_apple"; tree.growth = 1f; tree.fruit = (row + col) % 2 == 0 ? 1f : 0.4f; t.Dirty(); }
                }
            Q2Put("s11_orchard", "crate", new Vector3(5.6f, 0f, -1.2f), 0f);
            Q2Put("s11_orchard", "crate", new Vector3(6.3f, 0f, -1.8f), 25f);
            Q2Put("s11_orchard", "bench", new Vector3(5.8f, 0f, 0.4f), 90f);
            Journal.Add("PLACE", "REVA'S POST: THREE OLD LETTERS, ONE ROAD OUT OF TOWN");
        }

        partial void Tick_S11()
        {
            // the letter Ida watched burn, and the one that stayed at the orchard, leave the pack
            if (!Story.Story.Flag("s11_ida_gone") && Story.Story.StepDone("S11", "ida_choice"))
            {
                Story.Story.SetFlag("s11_ida_gone");
                var r = Story.Story.Route("S11", "ida_choice");
                if (r != null && r.StartsWith("WANT ME TO BURN")) { Inventory.TakeItem(StoryLibrary.S11LetterIda); Journal.Add("STORY", "IDA MARSH'S LETTER BURNED IN HER STOVE, UNREAD."); }
            }
            if (!Story.Story.Flag("s11_pell_gone") && Story.Story.StepDone("S11", "pell_choice"))
            {
                Story.Story.SetFlag("s11_pell_gone");
                var r = Story.Story.Route("S11", "pell_choice");
                if (r != null && !r.StartsWith("I'LL TAKE IT BACK")) Inventory.TakeItem(StoryLibrary.S11LetterPell);
            }
            // the closing line follows the choices (rebuilt only when a route changes)
            string a = Story.Story.Route("S11", "ida_choice"), b = Story.Story.Route("S11", "pell_choice");
            if (!ReferenceEquals(a, s11Ida) || !ReferenceEquals(b, s11Pell) || !s11Set)
            {
                s11Ida = a; s11Pell = b; s11Set = true;
                var q = StoryLibrary.Get("S11"); if (q != null) q.payoff = StoryLibrary.S11Payoff();
            }
        }
    }
}
