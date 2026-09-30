using System.Collections.Generic;
using MadMax.Animals;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    // P2.3 OPENING DAY: crates of cabbages and the gate tally board at the market field, and Barnaby's goats loose among
    // the stalls (kept about while the quest runs and the player is near; they are scenery, not saved).
    public partial class WastelandGame
    {
        readonly List<Animal> p2Goats = new List<Animal>();

        partial void Scene_P2_3()
        {
            StoryAnchors.PersonalMarketSync();
            if (!Build || !Build.Structures || !StoryAnchors.Has("p2_market")) return;
            PutAt("p2_market", "crate", new Vector3(-3f, 0f, -3.5f), 10f);
            PutAt("p2_market", "crate", new Vector3(-1.8f, 0f, -4f), -15f);
            PutAt("p2_market", "barrel", new Vector3(3.5f, 0f, -4f), 0f);
            PutAt("p2_tally", "sign", Vector3.zero, 0f);                                                     // the gate tally board
        }

        partial void Tick_P2_3()
        {
            if (Time.time < p2Check) return;
            p2Check = Time.time + 0.5f;
            StoryAnchors.PersonalMarketSync();
            string how = Story.Story.Route("P2.3", "dispute");
            if (how != null)
                StoryLibrary.Get("P2.3").payoff = how.StartsWith("THE GATE TALLY") ? "THE GATE TALLY SETTLED IT: SIX CABBAGES, TWELVE SCRAP, ONE SHEEPISH GOATHERD. THE RECORDS HELD."
                    : how.StartsWith("PAY HER") ? "SIX CABBAGES WILL BE PAID FOR IN GOAT MANURE, A SACK A WEEK. ODA'S BEDS HAVE NEVER LOOKED BETTER."
                    : "YOU COVERED HALF THE CABBAGES, BARNABY THE OTHER HALF. BOTH ELDERS CLAIM THEY WON.";
            p2Goats.RemoveAll(a => !a);
            var at = StoryAnchors.Get("p2_market");
            if (p2Goats.Count >= 2 || Flat(at - FocusPos) > 120f) return;
            var def = AnimalLibrary.Get("goat");
            if (def == null) return;
            var r = Quaternion.Euler(0f, StoryAnchors.Yaw("p2_market"), 0f);
            var p = at + r * new Vector3(-2.5f + p2Goats.Count * 2f, 0f, -2.5f);
            p.y = terrain.HeightNoLoad(p.x, p.z) + 0.05f;
            p2Goats.Add(Animal.Spawn(def, p, StoryAnchors.Yaw("p2_market") + 60f * p2Goats.Count, propMaterial, "story:p2_goat:" + p2Goats.Count));
        }
    }
}
