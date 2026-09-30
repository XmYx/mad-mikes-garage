using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S22(List<Scenario> into) => into.Add(new StoryS22());
    }

    /// <summary>S22 A JACKET FOR THE END OF THE WORLD in a sandbox world: Dax's offer, Ottilie's two miseries, leather
    /// makings swapped for lining cloth, a leather duster sewn at Dax's table, Ottilie wearing it, the test walk to the
    /// lookout with her following, and Dax's patch; the coat and the walk's weather are remembered in the payoff.</summary>
    class StoryS22 : Scenario
    {
        public override string Id => "story.s22";
        public override float Timeout => 180f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            yield return Q3T.OnFoot(g);
            if (!c.Check(Q3T.Open("S22"), "S22 is on offer in a sandbox world")) yield break;
            yield return Q3T.TalkAt(c, "dax", "dax", "YOU LOOK LIKE A MAN WITH A DEADLINE");
            yield return H.Until(() => Q3T.Active("S22"), 3f);
            if (!c.Check(Q3T.Active("S22"), "Dax's job is taken")) yield break;
            // the makings will be leather, swapped for lining cloth
            g.Inventory.TrySpend(ResourceType.Cloth, g.Inventory.Get(ResourceType.Cloth));
            g.Inventory.Add(ResourceType.Leather, 4);
            c.Fixture("no cloth; 4 leather in the pack");
            yield return Q3T.TalkAt(c, "s22_home", "s22_ottilie", "DAX SAYS YOU'RE WALKING NORTH");
            yield return Q3T.Step(c, "S22", "meet", 3f, "met Ottilie");
            yield return Q3T.Step(c, "S22", "makings", 4f, "brought the makings");
            yield return H.Until(() => g.Inventory.Get(ResourceType.Cloth) >= 8, 3f);
            c.Check(Plot.Route("S22", "makings") == "LEATHER" && g.Inventory.Get(ResourceType.Cloth) >= 8, "Dax cut 8 cloth of lining for the leather");
            // sew the duster at Dax's table
            var table = Q3T.Prop(g, "sewing_table", "dax", 6f);
            var st = table ? table.GetComponentInChildren<CraftingStation>() : null;
            var rec = RecipeLibrary.Get("c_duster");
            if (!c.Check(st && rec != null, "Dax's sewing table and the duster pattern")) yield break;
            yield return H.Walk(c, "dax", 1.5f);
            foreach (var (t, n) in rec.resources) { int need = RecipeLibrary.Amount(n); if (g.Inventory.Get(t) < need) { g.Inventory.Add(t, need - g.Inventory.Get(t)); c.Fixture("topped up " + t); } }
            string why = g.CraftBlockReason(rec, st);
            c.Check(why == null, "the duster can be sewn here: " + (why ?? "ok"));
            g.Craft(rec, st);
            if (st.queue.Count > 0) st.queue[0].progress = 0.95f;
            c.Fixture("sewing hurried along");
            yield return Q3T.Step(c, "S22", "sew", 15f, "sewed the coat");
            c.Check(g.Inventory.GetItem("cloth_duster") > 0, "a leather duster in the pack");
            c.Screenshot("dax_stall");
            yield return null;
            // Ottilie tries it on and wears it
            yield return Q3T.TalkAt(c, "s22_home", "s22_ottilie", "HERE: A LEATHER DUSTER");
            yield return Q3T.Step(c, "S22", "fit", 3f, "Ottilie tried it on");
            yield return H.Until(() => g.CastBody("s22_ottilie") != null && StoryCast.Profile("s22_ottilie", g.World.seed).outfit.Contains("duster"), 5f);
            c.Check(g.Inventory.GetItem("cloth_duster") == 0 && StoryCast.Profile("s22_ottilie", g.World.seed).outfit.Contains("duster"), "the duster went to Ottilie and she wears it");
            // the test walk
            var her = g.CastBody("s22_ottilie");
            if (her)
            {
                var p = her.transform.position + her.transform.forward * 1.5f; p.y = MadMax.World.DeformableTerrain.Instance.Height(p.x, p.z) + 0.2f;
                g.Player.Teleport(p, 0f);
                c.Fixture("walked up to Ottilie (teleport)");
            }
            yield return H.Until(() => g.CastBody("s22_ottilie") && g.CastBody("s22_ottilie").companion, 4f);
            c.Check(g.CastBody("s22_ottilie") && g.CastBody("s22_ottilie").companion, "Ottilie falls in beside you");
            yield return H.Walk(c, "s22_view", 1f);
            yield return Q3T.Step(c, "S22", "walk", 40f, "the test walk to the lookout");
            c.Check(g.CastBody("s22_ottilie") && !g.CastBody("s22_ottilie").companion, "she stops following once the walk is done");
            c.Screenshot("lookout");
            yield return null;
            yield return Q3T.TalkAt(c, "dax", "dax", "SHE WALKED IT IN");
            yield return Q3T.Finished(c, "S22", "S22 A JACKET FOR THE END OF THE WORLD is done");
            c.Check(g.Inventory.GetItem("story_road_patch") > 0 && Plot.Flag("dax_patch"), "Dax's road patch");
            c.Check(Plot.Route("S22", "sew") == "FOR THE RAIN: A LEATHER DUSTER", "the choice of coat is remembered");
            c.Check(StoryLibrary.Get("S22").payoff.Contains("LEATHER DUSTER") && (Plot.Flag("s22_wet") || Plot.Flag("s22_dry")), "the payoff tells what she wore and how the walk went");
        }
    }
}
