using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    // P2.2 A THIRD FIELD: the market field follows the choice (the field between, or Oda's field for the rotation), gets
    // a marker flag and sign once chosen; two of the player's own market stalls on it are counted.
    public partial class WastelandGame
    {
        partial void Tick_P2_2()
        {
            if (Time.time < p2Check) return;
            p2Check = Time.time + 0.5f;
            StoryAnchors.PersonalMarketSync();
            string choice = Story.Story.Route("P2.2", "choose");
            if (choice == null) return;
            bool turns = StoryLibrary.P2Rotates(choice);
            if (!Story.Story.Flag("p2_marked") && Build && Build.Structures)
            {
                Story.Story.SetFlag("p2_marked");
                PutAt("p2_market", "flag", new Vector3(-7f, 0f, 7f), 0f);
                PutAt("p2_market", "sign", new Vector3(-7f, 0f, 5.5f), 0f);
                Journal.Add("PLACE", turns ? "THE MARKET STARTS IN ODA'S FIELD AND TAKES TURNS WITH BARNABY'S" : "THE MARKET GOES IN THE FIELD BETWEEN THE HAMLETS");
            }
            StoryLibrary.Get("P2.2").payoff = turns
                ? "ODA'S FIELD HOSTS THE FIRST MARKET: TWO STALLS, A LATRINE, WATER. NEXT MONTH IT'S BARNABY'S TURN, AND HE'S ALREADY COMPLAINING ABOUT IT."
                : "THE FIELD BETWEEN THE HAMLETS HAS TWO STALLS, A LATRINE AND WATER. IT BELONGS TO NEITHER, WHICH IS THE POINT.";
            var at = StoryAnchors.Get("p2_market");
            if (!Story.Story.StepDone("P2.2", "stalls"))
            {
                int stalls = 0;
                foreach (var p in Placeable.All)
                    if (p && p.id == "player_stall" && !IsStoryProp(p) && Flat(p.transform.position - at) <= 18f) stalls++;
                if (stalls >= 2) Story.Story.Note("p2_2:stalls");
            }
            if (Story.Story.StepDone("P2.2", "latrine") && !Story.Story.StepDone("P2.2", "water"))
            {
                // water within 24 m of the field, and no latrine of the player's within the sewage reach of it
                bool ok = false, tooClose = false;
                foreach (var wp in Placeable.All)
                {
                    if (!wp || IsStoryProp(wp) || (wp.id != "well" && wp.id != "rain_collector" && wp.id != "water_tank") || Flat(wp.transform.position - at) > 24f) continue;
                    bool clear = true;
                    foreach (var lp in Placeable.All)
                        if (lp && lp.id == "latrine" && Flat(lp.transform.position - wp.transform.position) < WaterQuality.SewageReach) { clear = false; break; }
                    if (clear) { ok = true; break; }
                    tooClose = true;
                }
                if (ok) Story.Story.Note("p2_2:water");
                else if (tooClose) P1Say("ODA: NOT NEXT TO THE LATRINE. TWELVE PACES AT LEAST, OR NOBODY DRINKS HERE TWICE.");
            }
        }
    }
}
