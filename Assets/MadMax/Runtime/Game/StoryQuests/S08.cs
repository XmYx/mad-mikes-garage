using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    // S08 THE WEDDING AT THE WRONG END OF THE ROAD: Bracken Bottom (the old mine camp, Fen's awning, sewing table and
    // clothes rack) and Bracken Top (a water tower, flags, a canopy over the stage, tables and a radio). The groom's coat
    // is torn when Fen hands it over and counts as mended once its condition is back; the band follows the player
    // (riding in a free passenger seat, or on foot, catching up when left behind) until they reach Bracken Top, or a
    // carter takes them; the ceremony is a PerformanceScene started on the player's word.
    public partial class WastelandGame
    {
        static readonly string[] S08Band = { "s08_fiddler", "s08_squeeze" };
        static readonly string[] S08BandAt = { "s08_band1", "s08_band2" };
        static readonly Vector3[] S08BandSpot = { new Vector3(4.4f, 0f, -1.2f), new Vector3(5.8f, 0f, -0.6f) };
        float s08T;
        bool s08Rode;

        partial void Scene_S08()
        {
            Q5ResetPayoff("S08");
            if (!Build || !Build.Structures) return;
            if (StoryAnchors.Has("fen"))
            {
                // Bracken Bottom: Fen's pitch in the old mine camp
                Q5Put("fen", "porch_awning", new Vector3(1.2f, 0f, -2.4f), 0f);
                Q5Put("fen", "sewing_table", new Vector3(2.6f, 0f, -2.0f), 0f);
                Q5Put("fen", "wardrobe", new Vector3(4.4f, 0f, -2.6f), 0f);
                Q5Put("fen", "bench", new Vector3(-1.2f, 0f, -2.2f), 0f);
                Q5Put("fen", "tyres", new Vector3(-6.2f, 0f, -3.2f), 20f);
                Q5Put("fen", "barrel", new Vector3(-7.6f, 0f, -1.6f), 0f);
                Q5Put("fen", "barrel", new Vector3(-7.0f, 0f, -4.4f), 0f, 0.32f, 90f);
                for (int i = 0; i < 3; i++) Q5Put("fen", "mine_rail", new Vector3(-9.5f, 0f, -2f - i * 2f), 0f);
                Q5Put("fen", "sign", new Vector3(-4.6f, 0f, 2.6f), 180f);
            }
            if (StoryAnchors.Has("s08_wedding"))
            {
                // Bracken Top: the water tower, a canopy over the stage, flags, tables, the radio
                Q5Put("s08_wedding", "water_tower", new Vector3(-9f, 0f, -8f), 0f);
                Q5Put("s08_wedding", "porch_awning", new Vector3(0f, 0f, -4f), 0f);
                Q5Put("s08_wedding", "rug", new Vector3(0f, 0f, -3f), 0f);
                foreach (var f in new[] { new Vector3(-3.8f, 0f, -4.6f), new Vector3(3.8f, 0f, -4.6f), new Vector3(-3.8f, 0f, 3.4f), new Vector3(3.8f, 0f, 3.4f) }) Q5Put("s08_wedding", "flag", f, 0f);
                Q5Put("s08_wedding", "dining_table", new Vector3(7f, 0f, -4.2f), 90f);
                Q5Put("s08_wedding", "dining_table", new Vector3(7f, 0f, -7f), 90f);
                Q5Put("s08_wedding", "radio", new Vector3(5.6f, 0f, -3.6f), -90f);
                Q5Put("s08_wedding", "bench", new Vector3(-6.4f, 0f, -2f), 90f);
                Q5Put("s08_wedding", "lamppost", new Vector3(-5.2f, 0f, 1.6f), 0f);
                Q5Put("s08_wedding", "lamppost", new Vector3(5.2f, 0f, 1.6f), 0f);
            }
        }

        /// <summary>The ceremony: the band, the mother of the bride officiating, the vows, the crowd; the radio for the dance.</summary>
        Performance S08Ceremony()
        {
            var p = new Performance { key = "s08:ceremony", title = "THE KERRY WEDDING", stage = "s08_stage", extras = 8, radio = true };
            p.performers.AddRange(new[] { "s08_fiddler", "s08_bride", "s08_host", "s08_groom", "s08_squeeze" });
            p.audience.Add("fen");
            bool mendedAtTable = MadMax.Story.Story.Flag("s08:table");
            p.Line(null, "THE BAND STRIKES UP. SOMEONE'S UNCLE TURNS THE RADIO DOWN, THEN UP AGAIN.", 4.5f, "tune")
             .Line("s08_fiddler", "ONE, TWO... NO, WAIT. ONE, TWO, THREE.", 3.5f)
             .Line(Performance.Crowd, "SHHH!", 2.5f)
             .Line("s08_host", "WE'RE HERE FOR LIZA AND TOBIAS, WHO FOUND EACH OTHER AT THE RIGHT END OF THE ROAD. EVENTUALLY.", 5.5f)
             .Line("s08_groom", mendedAtTable ? "I, TOBIAS, IN A COAT YOU CAN'T SEE THE TEAR IN..." : "I, TOBIAS, IN A COAT HELD TOGETHER WITH GOOD INTENTIONS...", 4.5f)
             .Line("s08_bride", "I, LIZA, IN FRONT OF EVERYONE WHO CAME THE LONG WAY ROUND...", 4.5f)
             .Line("s08_host", "THEN BY WHATEVER'S LEFT TO DO IT BY: YOU'RE MARRIED. KISS, AND DON'T MAKE A MEAL OF IT.", 5f, "bell")
             .Line(Performance.Crowd, "HOORAY!", 3f, "crowd_cheer")
             .Line("s08_squeeze", "AND NOW THE DANCE. RADIO, IF YOU PLEASE.", 4f);
            p.onDone = () =>
            {
                string who = Stats != null && !string.IsNullOrEmpty(Stats.name) ? Stats.name.ToUpperInvariant() : "THE MECHANIC WHO FOUND THE RIGHT END OF THE ROAD";
                MadMax.Audio.RadioNetwork.Flash("A DEDICATION FROM THE KERRY WEDDING AT BRACKEN TOP, FOR " + who + ": THANKS FOR FINDING THE RIGHT END OF THE ROAD. HERE'S A SLOW ONE.", 60f);
                Journal.Add("RADIO", "A DEDICATION FROM THE KERRY WEDDING, FOR YOU: 'THANKS FOR FINDING THE RIGHT END OF THE ROAD.'");
            };
            return p;
        }

        partial void Tick_S08()
        {
            if ((s08T += Time.deltaTime) < 0.25f) return;
            s08T = 0f;
            // the coat: torn when Fen hands it over, mended once its condition is back
            if (MadMax.Story.Story.StepDone("S08", "coat") && !MadMax.Story.Story.Flag("s08:torn"))
            {
                MadMax.Story.Story.SetFlag("s08:torn");
                ClothWear.TryGetValue("duster", out float w);
                ClothWear["duster"] = Mathf.Max(w, 0.72f);
                Toast("THE GROOM'S COAT: A SLEEVE HALF OFF, THE HEM IN RIBBONS");
            }
            if (MadMax.Story.Story.Flag("s08:torn") && Inventory.GetItem("cloth_duster") > 0 && GarmentCondition("duster") >= 0.95f)
            {
                if (!MadMax.Story.Story.StepDone("S08", "mend") && StoryAnchors.Has("fen") && Q5Near(StoryAnchors.Get("fen"), 12f)) MadMax.Story.Story.SetFlag("s08:table");
                Q5Note("s08:mended");
            }
            S08Band_Tick();
            if (PerformanceScene.RadioPlaying(StoryAnchors.Get("s08_stage"))) Q5Note("s08:radio");
            // the ceremony runs from the player's word until it is done (restarted from the top after a reload)
            if (MadMax.Story.Story.StepDone("S08", "start") && !MadMax.Story.Story.StepDone("S08", "ceremony") && (!PerformanceScene.Current || PerformanceScene.Current.Show.key != "s08:ceremony"))
                PerformanceScene.Begin(this, S08Ceremony());
        }

        void S08Band_Tick()
        {
            if (!StoryAnchors.Has("s08_wedding")) return;
            var top = StoryAnchors.Get("s08_wedding");
            bool carter = MadMax.Story.Story.StepDone("S08", "band") && Q5Route("S08", "band", "HIRE");
            bool escort = MadMax.Story.Story.StepDone("S08", "go") && !carter;
            int arrived = 0;
            for (int i = 0; i < S08Band.Length; i++)
            {
                string flag = "s08:arrived:" + i;
                var spot = Q5At("s08_wedding", S08BandSpot[i]);
                var body = CastBody(S08Band[i]);
                if (carter && !MadMax.Story.Story.Flag(flag))
                {
                    MadMax.Story.Story.SetFlag(flag);
                    if (body) { body.companion = false; if (body.Riding) body.Unboard(); Q5Teleport(body, spot, StoryAnchors.Yaw("s08_wedding")); }
                }
                if (MadMax.Story.Story.Flag(flag))
                {
                    arrived++;
                    StoryAnchors.Q5Move(S08BandAt[i], spot);
                    if (body && !MadMax.Story.Story.StepDone("S08", "start")) Q5Keep(body, spot, StoryAnchors.Yaw("s08_wedding") + 180f);
                    continue;
                }
                if (!escort) continue;
                if (!body) { if (Player) StoryAnchors.Q5Move(S08BandAt[i], FocusPos); continue; }       // left behind and folded away: they turn up where you are
                if (!body.Alive) continue;
                // walking or riding with the player: their place moves with them so they stay around
                if (!body.companion) { body.companion = true; body.order = 0; }
                body.leaving = false;
                if (body.Riding) s08Rode = true;
                StoryAnchors.Q5Move(S08BandAt[i], body.transform.position);
                if (!body.Riding && Q5Flat(body.transform.position, top) < 22f)
                {
                    body.companion = false;
                    MadMax.Story.Story.SetFlag(flag);
                    if (s08Rode) MadMax.Story.Story.SetFlag("s08:rode");
                    StoryAnchors.Q5Move(S08BandAt[i], spot);
                    Q5Keep(body, spot, StoryAnchors.Yaw("s08_wedding") + 180f);
                    Toast(Q5Name(S08Band[i]) + " IS AT BRACKEN TOP");
                }
            }
            if (arrived == S08Band.Length && !carter)
            {
                Q5Note(MadMax.Story.Story.Flag("s08:rode") ? "s08:band_rode" : "s08:band_walked");
                if (!MadMax.Story.Story.Flag("s08:payoff"))
                {
                    MadMax.Story.Story.SetFlag("s08:payoff");
                    Q5Payoff("S08", "LIZA KERRY AND TOBIAS HALE WERE MARRIED AT THE RIGHT END OF THE ROAD, UNDER THE WATER TOWER, " +
                        (MadMax.Story.Story.Flag("s08:rode") ? "WITH A BAND THAT ARRIVED IN YOUR PASSENGER SEAT, TUNING UP." : "WITH A BAND THAT WALKED UP THE ROAD BEHIND YOU, COMPLAINING IN THREE-QUARTER TIME.") +
                        " FEN SENT YOU OFF IN A SPARE WEDDING JACKET.");
                }
            }
            else if (carter && !MadMax.Story.Story.Flag("s08:payoff"))
            {
                MadMax.Story.Story.SetFlag("s08:payoff");
                Q5Payoff("S08", "LIZA KERRY AND TOBIAS HALE WERE MARRIED AT THE RIGHT END OF THE ROAD, UNDER THE WATER TOWER, WITH A BAND THAT ARRIVED BY MULE. FEN SENT YOU OFF IN A SPARE WEDDING JACKET.");
            }
        }

    }
}
