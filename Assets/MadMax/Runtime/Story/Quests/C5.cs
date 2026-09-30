using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // C5 NO EMPTY SEAT (storyline §7): a demonstration convoy from the market to the next town out, which the toll book
    // calls supplied and isn't. Secure drivers (Isaac from C1, Pru from C2, Tobias who quit the Guild), collect the
    // village's agreed cargo from Bo and load it, choose the route (the highway through the Remnant checkpoint, or the back
    // track round it), arrange the checkpoint beforehand (the passage agreement from C3, or the freight rate), then lead
    // the convoy on the story system "player_convoy" (Game/StoryQuests/PlayerConvoy): allies keep formation behind you.
    // Two authored events on the way: a truck breaks down (patch it, tow it, or leave it and take its driver) and a
    // smallholder's cart blocks the road (share water, promise custom, pay, haul the cart off, or drive round her). At the
    // destination Sera and the reeve settle how the route runs from now on: the arc's independent conclusion, recorded
    // for the finale as a Guild concession on public terms, a cooperative, or bilateral agreements.
    public static partial class StoryLibrary
    {
        public const string C5Crate = "story_c5_crate";

        static partial void Author_C5(QuestDef q)
        {
            ItemIds.Register(C5Crate, "VILLAGE CARGO CRATE");
            foreach (var k in new[] { "c5_yard", "c5_depot", "c5_break", "c5_dispute", "c5_check", "c5_back" }) if (!Anchors.Contains(k)) Anchors.Add(k);

            q.build = Build.Playable;
            q.offerSay = "TWO TOWNS SIGNED. WHAT HAPPENS WHEN THE GUILD SAYS IT DOESN'T COUNT?";
            q.offerReply = "WE SHOW THEM IT WORKS. ONE CONVOY: THE VILLAGE'S PRODUCE AND OUR WATER TO THE NEXT TOWN OUT, WHICH THE TOLL BOOK SAYS IS SUPPLIED AND ISN'T. " +
                           "DRIVERS, CARGO, A ROUTE, AND THE REMNANT CHECKPOINT ON THE HIGHWAY THAT HOLDS ANY FREIGHT WITHOUT GUILD PAPERS. NO EMPTY SEAT: EVERY TRUCK " +
                           "DRIVEN BY SOMEONE WHO CHOSE TO BE THERE.";
            q.hook = "SERA WANTS A DEMONSTRATION CONVOY TO THE NEXT TOWN OUT: DRIVERS, THE AGREED CARGO, A ROUTE, AND A WAY PAST THE REMNANT CHECKPOINT.";

            Step(q, "isaac", "ASK ISAAC DUNE TO DRIVE", "c5_yard").Says("c1_driver", "c5_isaac", "DRIVE THE WATER TRUCK IN THE CONVOY?",
                "SOMEONE HAS TO. AND I WANT TO SEE THE TOLL KEEPER'S FACE WHEN WE DON'T STOP.").Optional();
            Step(q, "pru", "ASK PRU HALLORAN TO DRIVE", "c5_yard").Says("c2_hauler", "c5_pru", "DRIVE THE PRODUCE TRUCK?",
                "IF IT'S OUR POTATOES, I'M DRIVING OUR POTATOES. WHICH SEAT?").Optional();
            Step(q, "tobias", "ASK TOBIAS KERR TO DRIVE", "c5_yard").Says("c5_tobias", "c5_tobias", "DRIVE FOR THE CONVOY?",
                "FIFTEEN. I QUIT THE GUILD, NOT EATING. I KNOW EVERY PUMP BETWEEN HERE AND THE COAST AND WHICH ONES ARE LYING.").Optional();
            q.steps[q.steps.Count - 1].any[0].price = 15;
            Step(q, "drivers", "SECURE AT LEAST TWO DRIVERS AT THE CONVOY YARD: ISAAC, PRU OR TOBIAS", "c5_yard").When(Goal.Steps, "isaac,pru,tobias", 2f)
                .Pays(r => r.training.Add((Skill.Speech, 3f)));
            Step(q, "produce", "COLLECT THE VILLAGE'S AGREED CARGO FROM BO TILLMAN", "c3_farm").Says("c3_farmer", "c5_produce", "THE VILLAGE'S SHARE OF THE CARGO?",
                "THREE CRATES: POTATOES, ONIONS AND THE GOOD CHEESE, WHICH IS A SECRET. MIND THE CHEESE.")
                .Pays(r => r.items.Add((C5Crate, 3)));
            Step(q, "load", "LOAD THE AGREED CARGO: BRING THE CRATES TO SERA", "town1").Says("sera", "c5_load", "THE VILLAGE'S CRATES, AS AGREED.",
                "ON THE TRUCKS, WITH OUR WATER. EVERYTHING IN THE AGREEMENT AND NOTHING THAT ISN'T; THAT'S THE WHOLE POINT.").Needs(C5Crate)
                .Pays(r => r.take.Add((C5Crate, 3)));
            Step(q, "route", "CHOOSE THE ROUTE WITH SERA", "town1")
                .Says("sera", "c5_highway", "THE HIGHWAY, THROUGH THE CHECKPOINT.", "FASTER, AND EVERYONE SEES US. SORT OUT THE CAPTAIN FIRST.")
                .Says("sera", "c5_backtrack", "THE BACK TRACK, ROUND THE CHECKPOINT.", "LONGER AND SOFTER, AND NOBODY CAN SAY WE ASKED PERMISSION. OR THAT WE DIDN'T.");
            Step(q, "arrange", "ARRANGE PASSAGE AT THE REMNANT CHECKPOINT: SHOW THE PASSAGE AGREEMENT, OR PAY THE FREIGHT RATE", "c5_check")
                .Says("c5_captain", "c5_papers", "HERE'S THE PASSAGE AGREEMENT, SIGNED BY TWO TOWNS.",
                      "CIVIC FREIGHT. THE GUILD CONTRACT SAYS I HOLD 'COMMERCIAL' LOADS, AND THIS ISN'T ONE. I'LL LIFT THE BOOM FOR YOUR CONVOY. I'LL COUNT THE TRUCKS.").Needs(C3Agreement)
                .Says("c5_captain", "c5_rate", "WHAT'S THE FREIGHT RATE FOR A CONVOY?",
                      "THIRTY FOR THE LOT, AND A RECEIPT, BECAUSE WE'RE NOT ANIMALS. THE BOOM GOES UP WHEN YOUR CONVOY ARRIVES.")
                .When(Goal.Event, "c5:no_checkpoint", label: "NOT GOING THAT WAY");
            q.steps[q.steps.Count - 1].any[1].price = 30;
            Step(q, "roll", "DRIVE YOUR OWN VEHICLE TO THE CONVOY YARD: THE CONVOY FORMS UP BEHIND YOU", "c5_yard").When(Goal.Event, "c5:rolling");
            Step(q, "breakdown", "LEAD THE CONVOY OUT. IF A TRUCK STOPS: PATCH IT (REPAIR KIT), TOW IT ALONG (A WINCH BUMPER, [4] TO HOOK), OR LEAVE IT AND TAKE ITS DRIVER", "c5_break")
                .When(Goal.Event, "c5:fixed", label: "PATCHED IT ON THE ROADSIDE")
                .When(Goal.Event, "c5:towed", label: "TOWED IT")
                .Says("c1_driver", "c5_leave", "LEAVE IT. RIDE WITH ME.", "LEAVE IT? ...FINE. BUT I'M COMING BACK FOR IT WITH A TOW ROPE AND A GRUDGE.")
                .Says("c2_hauler", "c5_leave", "LEAVE IT. RIDE WITH ME.", "IT'S THE VILLAGE'S TRUCK. ...FINE. I'M SITTING IN THE FRONT AND I'M CHOOSING THE STATION.")
                .Says("c5_tobias", "c5_leave", "LEAVE IT. RIDE WITH ME.", "LEFT A TRUCK ON THE ROAD FOR THE GUILD ONCE. SWORE I WOULDN'T AGAIN. ...WELL. HERE WE ARE.");
            Step(q, "dispute", "A CART ACROSS THE ROAD AND A SMALLHOLDER WHO WANTS A WORD: SETTLE IT", "c5_dispute")
                .Says("c5_wren", "c5_water", "OUR WATER TRUCK FILLS YOUR BARREL.",
                      "...THE WHOLE BARREL? FOR NOTHING? THEN THE CART MOVES. MY BOY WILL PUSH. HE LIKES PUSHING.")
                .Says("c5_wren", "c5_eggs", "WE'LL STOP HERE EVERY RUN AND BUY YOUR EGGS.",
                      "EVERY RUN? I'LL HOLD YOU TO IT. I HAVE A GREAT MANY EGGS. MOVE, CART.")
                .Says("c5_wren", "c5_toll", "HERE: FOR THE TROUBLE.",
                      "TEN SCRAP FOR A CART'S WIDTH OF ROAD. FINE. IT'S A POOR ROAD.")
                .When(Goal.Event, "c5:cleared", label: "HAULED THE CART OFF THE ROAD")
                .When(Goal.Event, "c5:round", label: "DROVE ROUND HER");
            q.steps[q.steps.Count - 1].any[2].price = 10;
            Step(q, "checkpoint", "THROUGH THE REMNANT CHECKPOINT, OR ROUND IT ON THE BACK TRACK", "c5_check")
                .When(Goal.Event, "c5:through", label: "THROUGH THE BOOM")
                .When(Goal.Event, "c5:bypassed", label: "ROUND IT")
                .When(Goal.Event, "c5:smashed", label: "THROUGH THE BOOM, THE HARD WAY");
            Step(q, "arrive", "BRING THE CONVOY IN: EVERY TRUCK TO THE DEPOT AT THE NEXT TOWN", "c5_depot").When(Goal.Event, "c5:arrived")
                .Pays(r => r.training.Add((Skill.Driving, 5f)));
            Step(q, "reeve", "OPTIONAL: TALK TO THE REEVE", "c5_depot").Says("c5_reeve", "c5_reeve", "WHAT WILL THE TOWN DO WITH IT?",
                "EAT IT, MOSTLY. THEN WRITE TO EVERY TOWN ON THE ROAD THAT A CONVOY CAME THAT NOBODY OWNED. THE GUILD WILL HATE THE LETTERS MORE THAN THE CONVOY.").Optional();
            Step(q, "terms", "DECIDE WITH SERA HOW THE ROUTE RUNS FROM NOW ON", "c5_depot")
                .Says("sera_c5", "c5_concession", "A GUILD CONCESSION, ON PUBLIC TERMS.",
                      "THEN THE GUILD RUNS IT AND EVERYONE CAN READ THE BOOK. ADA WILL HATE THE READING PART. GOOD. IT'S STRONG AS LONG AS SOMEONE KEEPS READING.")
                .Says("sera_c5", "c5_coop", "A COOPERATIVE: EVERY TOWN ON THE ROUTE HAS A SEAT.",
                      "MEETINGS. SO MANY MEETINGS. ...AND NOBODY OWNS IT, SO NOBODY CAN SELL IT. IT'LL BE SLOW AND IT'LL BE OURS.")
                .Says("sera_c5", "c5_bilateral", "SEPARATE DEALS, TOWN BY TOWN.",
                      "SMALL AND STUBBORN. IF ONE BREAKS, THE OTHERS HOLD. A LOT OF HANDSHAKES AND NO SINGLE THROAT TO SQUEEZE.");
            Step(q, "settled", "THE CONVOY PARKS UP", "c5_depot").When(Goal.Event, "c5:settled");
            q.reward.scrap = 40; q.reward.training.Add((Skill.Driving, 8f)); q.reward.training.Add((Skill.Speech, 5f)); q.reward.flag = "c5_done";
            q.payoff = "THE CONVOY ARRIVED WITH NOBODY'S PERMISSION BUT ITS OWN. ADA VENN NOW HAS TO BARGAIN WITH A ROUTE THAT WORKS WITHOUT HER.";
        }

        /// <summary>The arc's conclusion as a finale flag ("arc_c_concession", "arc_c_coop", "arc_c_bilateral"), null until chosen.</summary>
        public static string C5Conclusion()
        {
            string t = Story.Route("C5", "terms");
            return t == null ? null : t.StartsWith("A GUILD") ? "arc_c_concession" : t.StartsWith("A COOP") ? "arc_c_coop" : "arc_c_bilateral";
        }

        /// <summary>C5's closing line: the road taken, what happened on it, and how the route will run.</summary>
        public static string C5Payoff()
        {
            string route = Story.Route("C5", "route"), brk = Story.Route("C5", "breakdown"), disp = Story.Route("C5", "dispute"), cp = Story.Route("C5", "checkpoint");
            var parts = new List<string>();
            parts.Add(route != null && route.StartsWith("THE BACK") ? "THE CONVOY TOOK THE BACK TRACK" : "THE CONVOY TOOK THE HIGHWAY");
            if (brk != null) parts.Add(brk == "PATCHED IT ON THE ROADSIDE" ? "PATCHED A TRUCK ON THE ROADSIDE" : brk == "TOWED IT" ? "TOWED A BROKEN TRUCK IN" : "LEFT A TRUCK AND KEPT ITS DRIVER");
            if (disp != null) parts.Add(disp == "DROVE ROUND HER" ? "DROVE ROUND WREN MOTT'S CART (SHE'LL REMEMBER)" : disp.StartsWith("WE'LL STOP") ? "PROMISED WREN MOTT CUSTOM EVERY RUN" : disp.StartsWith("OUR WATER") ? "FILLED WREN MOTT'S BARREL" : "GOT PAST WREN MOTT'S CART");
            if (cp != null) parts.Add(cp == "ROUND IT" ? "WENT ROUND THE CHECKPOINT" : cp == "THROUGH THE BOOM" ? "PASSED THE CHECKPOINT BY ARRANGEMENT" : "BROKE THE REMNANT BOOM");
            string end = C5Conclusion() == "arc_c_concession" ? "THE ROUTE RUNS AS A GUILD CONCESSION ON PUBLIC TERMS."
                : C5Conclusion() == "arc_c_coop" ? "THE ROUTE RUNS AS A COOPERATIVE: EVERY TOWN ON IT HAS A SEAT."
                : C5Conclusion() == "arc_c_bilateral" ? "THE ROUTE RUNS ON SEPARATE DEALS, TOWN BY TOWN." : "";
            return string.Join(", ", parts) + ". " + end + " ADA VENN NOW HAS TO BARGAIN WITH SOMETHING THAT WORKS.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_C5(List<Member> into)
        {
            into.Add(new Member { key = "c5_tobias", name = "TOBIAS KERR", title = "EX-GUILD DRIVER", anchor = "c5_yard", temper = Temper.Gruff,
                                  outfit = new[] { "jacket", "jeans", "boots", "goggles" },
                                  anchorNow = () => ArcCCrew.Anchor("c5_tobias", "c5_yard"),
                                  present = () => ArcCCrew.Present("c5_tobias", false) });
            into.Add(new Member { key = "c5_captain", name = "CAPTAIN ANSEL REED", title = "REMNANT CHECKPOINT", anchor = "c5_check", temper = Temper.Proud,
                                  outfit = new[] { "coat", "pants", "combat_boots", "helmet" } });
            into.Add(new Member { key = "c5_wren", name = "WREN MOTT", title = "SMALLHOLDER", anchor = "c5_wren", temper = Temper.Nervous, female = true,
                                  outfit = new[] { "sweater", "pants", "boots", "scarf" } });
            into.Add(new Member { key = "c5_reeve", name = "ODILE MARCH", title = "REEVE", anchor = "c5_depot", temper = Temper.Friendly, female = true,
                                  outfit = new[] { "coat", "pants", "boots", "sunhat" } });
            // Sera rides with the convoy and meets it at the depot (same person as "sera", seen at another place)
            into.Add(new Member { key = "sera_c5", name = "SERA DUNE", title = "WATER SURVEYOR", anchor = "c5_depot", temper = Temper.Proud, female = true,
                                  outfit = new[] { "duster", "pants", "boots", "shemagh" },
                                  present = () => ArcCCrew.C5Active && Story.StepDone("C5", "arrive") });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>C5: along the road from the market's edge to the next settlement out: the convoy yard just outside the
        /// market, the depot at the far end, the breakdown mark about a third of the way, the smallholder's cart halfway,
        /// the Remnant checkpoint (hut beside the road, boom across it) three quarters of the way, and the back track's
        /// bend off to the side of it.</summary>
        static partial void Anchors_C5(WorldGen world, Settlement town)
        {
            ArcC.Bind(world, town);
            var run = ArcC.run; float len = ArcC.Length(run);
            var yard = ArcC.Beside(world, run, Mathf.Min(34f, len * 0.12f), 14f, p => ArcC.Free(p, 26f), out float yf, 1f);
            ArcC.Put(world, "c5_yard", yard, yf);
            var depot = ArcC.Beside(world, run, Mathf.Max(0f, len - 20f), 13f, p => ArcC.Free(p, 24f), out float df, 1f);
            ArcC.Put(world, "c5_depot", depot, df);
            Clearing("c5_yard", 16f); Clearing("c5_depot", 16f);

            var brk = ArcC.At(run, len * 0.3f, out var bd);
            ArcC.Put(world, "c5_break", brk, ArcC.Yaw(bd));
            var cart = ArcC.At(run, len * 0.5f, out var cd);
            ArcC.Put(world, "c5_dispute", cart, ArcC.Yaw(cd));
            var wren = ArcC.Beside(world, run, len * 0.5f, 7f, p => ArcC.Free(p, 6f, "c5_dispute"), out float wf, 1f);
            ArcC.Put(world, "c5_wren", wren, wf);
            Clearing("c5_dispute", 10f);

            var boom = ArcC.At(run, len * 0.76f, out var kd);
            var across = new Vector3(kd.z, 0f, -kd.x);
            float side = ArcC.OffRoad(world, boom + across * 8f, 3f) ? 1f : -1f;
            ArcC.Put(world, "c5_boom", boom, ArcC.Yaw(kd));
            var hut = boom + across * side * 8f;
            ArcC.Put(world, "c5_check", hut, ArcC.Yaw(boom - hut));
            Clearing("c5_check", 10f);
            var back = boom - across * side * 150f;
            foreach (float d in new[] { 150f, 120f, 180f, 100f, 220f })
            {
                var p = boom - across * side * d;
                if (ArcC.OffRoad(world, p, 30f) && ArcC.Free(p, 40f)) { back = p; break; }
            }
            ArcC.Put(world, "c5_back", back, ArcC.Yaw(boom - back));
        }
    }
}
