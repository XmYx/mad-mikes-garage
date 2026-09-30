using MadMax.Story;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    // P1.2 THE WRONG ORIGINAL: the dead coupe in the ditch (its glovebox keeps a hood ornament Otis would trade for),
    // the deal (a traded trophy leaves the pack; bought or traded, Tom's straight six arrives at Nell's pile on a hand
    // cart), and the engine seen in the coupe's socket.
    public partial class WastelandGame
    {
        Lootable p1Glove;
        const string P1GloveKey = "story:p1_glovebox";

        partial void Scene_P1_2()
        {
            if (!StoryAnchors.Has("p1_ditch")) return;
            var w = SpawnRoadWreck("Coupe", StoryAnchors.Get("p1_ditch"), Quaternion.Euler(6f, StoryAnchors.Yaw("p1_ditch"), 10f), World.seed ^ 0x5A17);
            if (w) { w.name = "Wreck Coupe (ditch)"; Rest(w); }
            P1Glovebox();
        }

        partial void Tick_P1_2()
        {
            if (Time.time < p1Check) return;
            p1Check = Time.time + 0.5f;
            P1Glovebox();
            string deal = Story.Story.Route("P1.2", "deal");
            if (Story.Story.StepDone("P1.2", "deal") && !Story.Story.Flag("p1_2_dealt"))
            {
                Story.Story.SetFlag("p1_2_dealt");
                var trophy = StoryLibrary.P1Trophy(deal);
                using var feed = MadMax.Items.Inventory.Source("TRADED", "TRADED");
                if (trophy != null && Inventory.TakeItem(trophy)) Journal.Add("TRADE", "OTIS VANE TOOK THE " + MadMax.Items.ItemCatalog.Name(trophy));
                if (StoryLibrary.P1Bought(deal))
                {
                    P1Loose(StoryLibrary.P1Original, "p1_pile", new Vector3(0f, 0.3f, 2.8f), 0f);
                    Toast("OTIS'S BOY WHEELS TOM'S STRAIGHT SIX TO NELL'S STOP ON A HAND CART");
                    Journal.Add("PLACE", "TOM'S STRAIGHT SIX IS AT NELL'S PARTS PILE");
                }
            }
            if (deal != null)
                StoryLibrary.Get("P1.2").payoff = StoryLibrary.P1Bought(deal)
                    ? "TOM'S STRAIGHT SIX IS BACK IN NELL'S COUPE. OTIS VANE SAYS HE ALWAYS MEANT TO SELL IT BACK, WHICH IS A LIE, BUT A KIND ONE."
                    : "NELL'S COUPE RUNS ON A PRACTICAL SUBSTITUTE. TOM'S STRAIGHT SIX STAYS ON OTIS VANE'S SHELF, AND NELL SAYS THAT'S WHERE IT BELONGS.";
            var car = P1Car();
            if (!car) { EnsureNellCar(); return; }
            if (Story.Story.StepDone("P1.2", "deal") && !Story.Story.StepDone("P1.2", "engine") && P1Mounted(car.GetComponent<VehicleChassis>(), "engine")) Story.Story.Note("p1_2:engine");
        }

        /// <summary>The ditch coupe's glovebox (searched once, saved): a hood ornament. Rebuilt while the quest runs.</summary>
        void P1Glovebox()
        {
            if (p1Glove || Lootable.Searched.Contains(P1GloveKey) || !StoryAnchors.Has("p1_ditch")) return;
            var a = StoryAnchors.Get("p1_ditch"); var r = Quaternion.Euler(0f, StoryAnchors.Yaw("p1_ditch"), 0f);
            var p = a + r * new Vector3(1.6f, 0f, 0.4f); p.y = terrain.HeightNoLoad(p.x, p.z) + 0.8f;    // through the passenger window
            var go = new GameObject("DitchGlovebox");
            go.transform.position = p;
            var col = go.AddComponent<BoxCollider>(); col.size = new Vector3(0.5f, 0.4f, 0.5f); col.isTrigger = true;
            p1Glove = go.AddComponent<Lootable>();
            p1Glove.key = P1GloveKey; p1Glove.table = "none"; p1Glove.title = "DEAD COUPE'S GLOVEBOX";
            p1Glove.extra.Add("trophy_ornament");
        }
    }
}
