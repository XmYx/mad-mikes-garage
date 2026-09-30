using System.Collections.Generic;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // C2 THE CHEAP ROAD (storyline §7): the village has two ways to market. The warden's track crosses the flats past his
    // gate (short on the map, soft, his); the public road goes round and dips through a soft wash. Pru Halloran's trial
    // truck carries a real load (seed potatoes); one run each way and the truck keeps the score: minutes, litres, knocks.
    // Then make the cheap road work: buy the warden's season pass, improve the wash on the public road (fill it, surface it
    // with gravel, or bridge it), or share the hauling. Tyres to match the choice, a first haul contract, and Sera pins the
    // numbers up.
    public static partial class StoryLibrary
    {
        static partial void Author_C2(QuestDef q)
        {
            foreach (var k in new[] { "c2_start", "c2_gate", "c2_public", "c2_crossing" }) if (!Anchors.Contains(k)) Anchors.Add(k);

            q.build = Build.Playable;
            q.offerSay = "ISAAC SAYS YOU'LL KNOW WHAT THE TOLL BOOK MEANS.";
            q.offerReply = "IT MEANS SOMEONE IS PAID FOR DELIVERIES THAT DON'T HAPPEN, AND THE VILLAGE PAYS TWICE: ONCE IN THE BOOK, ONCE ON THE ROAD. " +
                           "THEY HAVE TWO WAYS TO MARKET. THE WARDEN'S TRACK OVER THE FLATS: SHORT, SOFT AND HIS. THE PUBLIC ROAD: LONG AND FREE. " +
                           "PRU HALLORAN HAS A TRUCK AND A LOAD. DRIVE BOTH AND WRITE DOWN WHAT CHEAP COSTS.";
            q.hook = "THE VILLAGE HAS TWO WAYS TO MARKET. PRU HALLORAN'S TRIAL TRUCK AND A LOAD OF SEED POTATOES WILL SAY WHICH IS CHEAPER.";

            Step(q, "village", "MEET PRU HALLORAN AND THE TRIAL TRUCK AT THE VILLAGE", "c2_start").When(Goal.Reach, "c2_start", 14f);
            Step(q, "brief", "ASK PRU WHAT THE TRIAL IS", "c2_start").Says("c2_hauler", "c2_brief", "WHAT ARE WE TESTING?",
                "TWO WAYS TO MARKET WITH A REAL LOAD: FOUR SACKS OF SEED POTATOES IN THE BACK. THE WARDEN'S TRACK CROSSES THE FLATS PAST HIS GATE; " +
                "THE PUBLIC ROAD GOES ROUND AND DIPS THROUGH THE WASH, WHICH EATS SUSPENSION. ONE RUN EACH, EITHER DIRECTION. THE TRUCK KEEPS THE SCORE: MINUTES, LITRES, KNOCKS.");
            Step(q, "short", "SURVEY RUN: THE WARDEN'S TRACK, PAST HIS GATE, BETWEEN THE VILLAGE AND THE MARKET IN THE TRIAL TRUCK", "c2_gate")
                .When(Goal.Event, "c2:short").Optional().Pays(r => r.training.Add((Skill.Driving, 3f)));
            Step(q, "public", "SURVEY RUN: THE PUBLIC ROAD, THROUGH THE WASH, BETWEEN THE VILLAGE AND THE MARKET IN THE TRIAL TRUCK", "c2_crossing")
                .When(Goal.Event, "c2:public").Optional().Pays(r => r.training.Add((Skill.Driving, 3f)));
            Step(q, "survey", "SURVEY BOTH ROUTES IN THE TRIAL TRUCK: THE WARDEN'S TRACK AND THE PUBLIC ROAD, ONE RUN EACH (THE JOURNAL KEEPS THE NUMBERS)", "c2_start")
                .When(Goal.Steps, "short,public", 2f);
            Step(q, "decide", "MAKE THE CHEAP ROAD WORK: BUY THE WARDEN'S PASS, IMPROVE THE WASH ON THE PUBLIC ROAD (FILL IT; SURFACE IT: GRAVEL FROM THE RAKE OR TIPPER, ASPHALT ON A HIGHWAY; OR BRIDGE IT), OR SHARE THE HAULING", "c2_crossing")
                .Says("c2_warden", "c2_pass", "A SEASON'S PASS FOR THE VILLAGE TRUCK.",
                      "TWENTY-FIVE AND THE GATE'S OPEN TO THAT TRUCK TILL THE RAINS. THE MUD'S FREE, MIND. I DON'T OWN THE MUD. YET.")
                .When(Goal.Event, "c2:surfaced", label: "SURFACED THE WASH")
                .When(Goal.Event, "c2:filled", label: "FILLED THE WASH")
                .When(Goal.Build, "bridge_timber|bridge_steel", 10f, "BRIDGED THE WASH")
                .Says("c2_hauler", "c2_share", "RUN IT TOGETHER: YOUR TRUCK TWICE A WEEK, THE MARKET PAYS HALF THE FUEL.",
                      "HALF THE FUEL AND A SEAT FOR WHOEVER'S GOING ANYWAY? THE WHOLE VILLAGE WILL WANT TO RIDE. DEAL. YOU PUT IN THE FIRST TANK, I'LL TELL SERA.");
            q.steps[q.steps.Count - 1].any[0].price = 25;
            q.steps[q.steps.Count - 1].any[4].price = 10;
            Step(q, "report", "TELL SERA WHAT CHEAP COSTS", "town1").Says("sera", "c2_report", "HERE'S WHAT CHEAP COSTS.",
                "WRITTEN DOWN IT'S NOT A FEELING ANY MORE, IT'S A NUMBER. I'LL PIN IT UP IN THE SQUARE WHERE THE GUILD CLERK HAS TO WALK PAST IT.");
            q.reward.scrap = 25; q.reward.training.Add((Skill.Driving, 6f)); q.reward.training.Add((Skill.Speech, 3f)); q.reward.flag = "c2_done";
            q.payoff = "THE VILLAGE'S ROAD TO MARKET HAS A PRICE ON IT NOW, IN MINUTES, LITRES AND KNOCKS, PINNED UP IN THE SQUARE.";
        }

        /// <summary>C2: a surveyed run as one line (records "c2:{route}:s|dl|dmg|m" written by the game side), null until driven.</summary>
        public static string C2Run(string route)
        {
            string k = "c2:" + route + ":";
            if (!ArcCRecord.Has(k + "s")) return null;
            int s = ArcCRecord.Get(k + "s"), dl = ArcCRecord.Get(k + "dl"), dmg = ArcCRecord.Get(k + "dmg"), m = ArcCRecord.Get(k + "m");
            return (route == "short" ? "WARDEN'S TRACK " : "PUBLIC ROAD ") + (m / 100) / 10f + " KM, " + s / 60 + " MIN " + s % 60 + " S, "
                   + dl / 10f + " L, " + dmg + "% KNOCKS" + (route == "short" ? ", 5 SCRAP A LOAD AT THE GATE" : ", NO TOLL");
        }

        /// <summary>C2's closing line from the choice made and the numbers.</summary>
        public static string C2Payoff()
        {
            string how = Story.Route("C2", "decide");
            string way = how == null ? "" : how.StartsWith("A SEASON'S") ? "THE VILLAGE BOUGHT THE WARDEN'S PASS: THE SHORT, SOFT TRACK, MUD TYRES AND A RECEIPT."
                : how == "SURFACED THE WASH" ? "YOU SURFACED THE WASH: THE FREE ROAD HOLDS A LOADED TRUCK NOW."
                : how == "FILLED THE WASH" ? "YOU FILLED THE WASH: THE FREE ROAD IS LEVEL AGAIN."
                : how == "BRIDGED THE WASH" ? "YOU BRIDGED THE WASH: THE FREE ROAD STEPS OVER IT NOW."
                : "THE VILLAGE AND THE MARKET SHARE ONE TRUCK AND ITS FUEL ON THE FREE ROAD.";
            string runs = (C2Run("short") ?? "") + (C2Run("public") != null ? "; " + C2Run("public") : "");
            return way + (runs.Length > 0 ? " " + runs + "." : "");
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_C2(List<Member> into)
        {
            into.Add(new Member { key = "c2_hauler", name = "PRU HALLORAN", title = "VILLAGE HAULER", anchor = "c2_start", temper = Temper.Joker, female = true,
                                  outfit = new[] { "jacket", "jeans", "boots", "sunhat" },
                                  anchorNow = () => ArcCCrew.Anchor("c2_hauler", "c2_start"),
                                  present = () => ArcCCrew.Present("c2_hauler", Story.StateOf("C2") == Story.State.Active) });
            into.Add(new Member { key = "c2_warden", name = "DEAN FARRO", title = "TRACK WARDEN", anchor = "c2_gate", temper = Temper.Greedy,
                                  outfit = new[] { "coat", "pants", "boots", "cowboy" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>C2: Pru's truck stop at the village's edge, the warden's gate out on the flats (the wettest open ground
        /// near the straight line between the market and the village, well clear of the public road), a marker halfway
        /// along the public road and its soft wash three quarters of the way out.</summary>
        static partial void Anchors_C2(WorldGen world, Settlement town)
        {
            ArcC.Bind(world, town);
            var road = ArcC.road; float len = ArcC.Length(road);
            var start = ArcC.Beside(world, road, len - 26f, 12f, p => ArcC.Free(p, 14f, "c_village"), out float sf, ArcC.trackSide);
            ArcC.Put(world, "c2_start", start, sf);
            Clearing("c2_start", 11f);

            // the warden's gate on the flats
            var a = road[0]; var b = road[road.Count - 1];
            var chord = new Vector3(b.x - a.x, 0f, b.z - a.z);
            var perp = chord.sqrMagnitude > 1f ? new Vector3(chord.z, 0f, -chord.x).normalized : Vector3.right;
            bool found = false; float best = float.MinValue; Vector3 gate = Vector3.zero;
            foreach (float t in new[] { 0.5f, 0.45f, 0.55f, 0.4f, 0.6f, 0.35f, 0.65f })
                foreach (float o in new[] { 0f, 20f, -20f, 40f, -40f, 60f, -60f })
                {
                    var p = Vector3.Lerp(a, b, t) + perp * o;
                    if (!ArcC.OffRoad(world, p, 25f) || ArcC.Distance(road, p) < 45f || !ArcC.Free(p, 50f) || !Level(world, p, 4f, 1.3f)) continue;
                    float score = world.Sample(p.x, p.z).baseWet - Mathf.Abs(o) * 0.002f - Mathf.Abs(t - 0.5f) * 0.3f;
                    if (score > best) { best = score; gate = p; found = true; }
                }
            if (!found)
            {
                var mid = ArcC.At(road, len * 0.5f, out var md);
                var side = new Vector3(md.z, 0f, -md.x) * ArcC.trackSide;
                gate = mid + side * 80f;
                foreach (float d in new[] { 80f, 60f, 110f, 140f })
                {
                    var p = mid + side * d;
                    if (ArcC.OffRoad(world, p, 20f) && ArcC.Free(p, 40f)) { gate = p; break; }
                }
            }
            var gq = ArcC.At(road, len * 0.5f, out _);
            ArcC.Put(world, "c2_gate", gate, ArcC.Yaw(gq - gate));
            Clearing("c2_gate", 10f);

            var pub = ArcC.At(road, len * 0.5f, out var pd);
            ArcC.Put(world, "c2_public", pub, ArcC.Yaw(pd));
            var wash = ArcC.At(road, len * 0.72f, out var wd);
            ArcC.Put(world, "c2_crossing", wash, ArcC.Yaw(wd));
            Clearing("c2_crossing", 7f);
        }
    }
}
