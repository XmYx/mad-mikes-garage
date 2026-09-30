using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game
{
    /// <summary>L3 A CRANKSHAFT AND A GRUDGE: Grist's camp (his start flag), the marker post down the road and Dell's
    /// corner. The race is a time trial against Grist's stated time (shown when you line up at his flag; wheels on the
    /// ground; lose and go again). The crank changes hands through <see cref="LastEngine.Give"/>; killing Grist, or the
    /// longest road's boss the old way, is the "by force" route.</summary>
    public partial class WastelandGame
    {
        float l3Check, l3Start = -1f;
        bool l3Armed;

        partial void Scene_L3()
        {
            Q3ResetPayoff("L3");
            if (!Build || !Build.Structures) return;
            if (StoryAnchors.Has("l3_camp"))
            {
                PutAt("l3_camp", "campfire", new Vector3(-3f, 0f, -3f), 0f);
                PutAt("l3_camp", "tyres", new Vector3(3.4f, 0f, -2.6f), 10f);
                PutAt("l3_camp", "tyres", new Vector3(4.2f, 0f, -1f), 60f);
                PutAt("l3_camp", "barrel", new Vector3(-4.4f, 0f, -0.6f), 0f);
                PutAt("l3_camp", "barrel", new Vector3(-4.8f, 0f, 0.6f), 0f);
                PutAt("l3_camp", "skull_pole", new Vector3(2f, 0f, 2.2f), 0f);
                PutAt("l3_camp", "flag", new Vector3(0f, 0f, 4.5f), 0f);                              // the start flag, on the verge
            }
            if (StoryAnchors.Has("l3_finish")) { PutAt("l3_finish", "post", Vector3.zero, 0f); PutAt("l3_finish", "flag", new Vector3(0.8f, 0f, 0f), 0f); }
            if (StoryAnchors.Has("l3_dell")) { PutAt("l3_dell", "table", new Vector3(1.2f, 0f, -0.8f), 0f); PutAt("l3_dell", "barrel", new Vector3(-1.6f, 0f, -1.2f), 0f); }
        }

        /// <summary>Grist's time from his flag to the marker post (s): the straight line, a road's worth of bends, 13 m/s.</summary>
        static float L3Par => Mathf.Round(Q3Flat(StoryAnchors.Get("l3_camp"), StoryAnchors.Get("l3_finish")) * 1.4f / 13f);

        partial void Tick_L3()
        {
            if (!Player) return;
            bool crankDone = Plot.StepDone("L3", "crank");
            if (Plot.StepDone("L3", "race") && !crankDone) L3Race();
            if (Time.time < l3Check) return;
            l3Check = Time.time + 0.3f;
            if (!crankDone)
            {
                if (MadMax.Npc.NpcRegistry.IsDead("cast:l3_grist") && !LastEngine.Has("relic_crank")) LastEngine.Give(this, "relic_crank", "FROM GRIST'S NECK");
                if (LastEngine.Has("relic_crank")) { Plot.Note("l3:has_crank"); Plot.Note("l3:fought"); }
            }
            else if (!Plot.Flag("l3_handed"))
            {
                Plot.SetFlag("l3_handed");
                if (Q3Route("L3", "crank", "A SUPERCHARGER")) Inventory.TakeItem("kit_supercharger");
                if (Q3Route("L3", "crank", "YOUR CREW"))
                {
                    Inventory.TakeItem("story_grist_marker");
                    Journal.Add("STORY", "GRIST PAID DELL FARROW WHAT HE OWED, WITH A FACE LIKE A SLAPPED TYRE.");
                }
                if (!LastEngine.Has("relic_crank")) LastEngine.Give(this, "relic_crank", "FROM GRIST");
            }
            if (crankDone && LastEngine.Has("built") && Inventory.GetItem("relic_crank") == 0) Plot.Note("l3:in_engine");
            L3Payoff();
        }

        /// <summary>The time trial: line up at Grist's flag in a ground vehicle, the clock starts when you leave it and stops
        /// at the marker post.</summary>
        void L3Race()
        {
            var car = Current;
            bool ground = car && !car.GetComponent<FlightModel>() && !car.GetComponent<BoatModel>();
            var start = StoryAnchors.Get("l3_camp"); var fin = StoryAnchors.Get("l3_finish");
            float par = L3Par;
            if (l3Start < 0f)
            {
                if (!ground) { l3Armed = false; return; }
                float d = Q3Flat(car.transform.position, start);
                if (d < 16f && !l3Armed)
                {
                    l3Armed = true;
                    Toast("AT GRIST'S FLAG. HIS TIME: " + par + " S TO THE MARKER POST. THE CLOCK STARTS WHEN YOU LEAVE THE FLAG");
                    SetWaypoint(fin, "GRIST'S MARKER POST", true);
                }
                else if (l3Armed && d >= 16f) { l3Start = Time.time; l3Armed = false; Toast("GO!"); MadMax.Audio.Sfx.Play2D("horn", 0.6f); }
                return;
            }
            float t = Time.time - l3Start;
            if (!ground || t > par * 3f) { l3Start = -1f; Toast("RACE ABANDONED. GRIST'S FLAG IS WHERE YOU LEFT IT"); return; }
            if (Q3Flat(car.transform.position, fin) > 15f) return;
            l3Start = -1f;
            if (t <= par)
            {
                Plot.Note("l3:race_won");
                Toast("YOU BEAT GRIST: " + t.ToString("0.0") + " S AGAINST HIS " + par + " S");
                MadMax.Audio.Sfx.Play2D("crowd_cheer", 0.7f);
            }
            else Toast("TOO SLOW: " + t.ToString("0.0") + " S AGAINST HIS " + par + " S. BACK TO HIS FLAG FOR ANOTHER GO");
        }

        static void L3Payoff()
        {
            string how = Q3Route("L3", "crank", "BEAT") ? "YOU BEAT GRIST'S TIME, AND HE PAID UP LIKE A MAN WHO BETS ON HIMSELF A LOT"
                       : Q3Route("L3", "crank", "A SUPERCHARGER") ? "GRIST TRADED HIS LUCK FOR A SUPERCHARGER KIT AND CALLED IT A BARGAIN"
                       : Q3Route("L3", "crank", "150") ? "YOU BOUGHT GRIST'S LUCK FOR 150 SCRAP"
                       : Q3Route("L3", "crank", "YOUR CREW") ? "DELL FARROW'S MARKER DID THE TALKING; GRIST PAID HER AND KEPT HIS CREW"
                       : Q3Route("L3", "crank", "TOOK") ? "YOU TOOK THE CRANK BY FORCE; THE LONG ROAD WILL REMEMBER"
                       : "THE CRANK CHANGED HANDS";
            Q3Payoff("L3", how + ". THE CRANKSHAFT IS HARLAN'S AGAIN.");
        }
    }
}
