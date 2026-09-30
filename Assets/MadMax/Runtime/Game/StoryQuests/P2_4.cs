using MadMax.Items;
using MadMax.Story;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    // P2.4 ONE SUPPLY RUN: lanterns, a fire and benches at the market field for the opening evening; the lamp fuel is
    // poured into the lanterns when the supplies are handed over; the evening counts from 18:00 at the field; the
    // upkeep choice decides whether the market recurs (flag p2_market: the elders keep stalls there every third day).
    public partial class WastelandGame
    {
        partial void Scene_P2_4()
        {
            StoryAnchors.PersonalMarketSync();
            if (!Build || !Build.Structures || !StoryAnchors.Has("p2_market")) return;
            PutAt("p2_market", "porch_lights", new Vector3(-5f, 0f, 2f), 0f);
            PutAt("p2_market", "porch_lights", new Vector3(5f, 0f, 2f), 0f);
            PutAt("p2_market", "campfire", new Vector3(0f, 0f, 5f), 0f);
            PutAt("p2_market", "bench", new Vector3(-2f, 0f, 7f), 180f);
            PutAt("p2_market", "bench", new Vector3(2f, 0f, 7f), 180f);
            PutAt("p2_market", "table", new Vector3(0f, 0f, -1f), 0f);
        }

        partial void Tick_P2_4()
        {
            bool due = Time.time >= p2Check || (Story.Story.Route("P2.4", "upkeep") != null && !Story.Story.Flag("p2_upkeep_read"));
            if (!due) return;
            p2Check = Time.time + 0.5f;
            StoryAnchors.PersonalMarketSync();
            if (Story.Story.StepDone("P2.4", "deliver") && !Story.Story.Flag("p2_lamps"))
            {
                Story.Story.SetFlag("p2_lamps");
                using var feed = MadMax.Items.Inventory.Source("SUPPLIED", "SUPPLIED");
                int f = Mathf.Min(10, Inventory.Get(ResourceType.Fuel));
                if (f > 0) Inventory.TrySpend(ResourceType.Fuel, f);
                Journal.Add("SUPPLY", f + " L OF LAMP FUEL INTO THE MARKET'S LANTERNS");
            }
            var cur = Story.Story.Current(StoryLibrary.Get("P2.4"));
            if (cur != null && cur.id == "evening" && DayNight.Hours >= 18f && DayNight.Hours < 23.9f && Flat(StoryAnchors.Get("p2_market") - FocusPos) < 30f)
                Story.Story.Note("p2_4:evening");
            string upkeep = Story.Story.Route("P2.4", "upkeep");
            if (upkeep == null) return;
            Story.Story.SetFlag("p2_upkeep_read");
            bool recurs = StoryLibrary.P2Recurs(upkeep);
            if (recurs && !Story.Story.Flag("p2_market")) { Story.Story.SetFlag("p2_market"); Journal.Add("PLACE", "THE HAMLETS' MARKET: EVERY THIRD DAY, 8:00 TO 19:00, ODA AND BARNABY SELL THERE"); }
            StoryLibrary.Get("P2.4").payoff = recurs
                ? "THE MARKET BETWEEN THE HAMLETS OPENS EVERY THIRD DAY. IT'S SMALL, IT ARGUES, AND IT'S THEIRS. IT DOESN'T REPLACE THE TOWN; IT SAVES A LONG WALK."
                : "ONE EVENING OF LANTERNS AND GOATS, AND THEN THE FIELD WENT BACK TO GRASS. THE HAMLETS STILL TALK ABOUT IT.";
        }
    }
}
