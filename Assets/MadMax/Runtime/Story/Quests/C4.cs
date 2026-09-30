using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // C4 A BRIDGE YOU CAN AFFORD (storyline §7): the storm took the crossing on the public road (a washed-out culvert, or
    // a ford scoured deep where a river crosses it). Greta Holm's road crew lends a digger and a loaded tipper on an
    // explicit condition: back at the yard with half a tank and nothing new broken (or pay for the wear). Restore it:
    // fill the washout, build a timber or steel bridge deck over it, or (where there's water) hire old Lucas's ferry.
    // Test it with the loaded tipper, return the machines, and choose whom the road-work sign credits.
    public static partial class StoryLibrary
    {
        static partial void Author_C4(QuestDef q)
        {
            foreach (var k in new[] { "c4_crossing", "c4_near", "c4_far", "c4_yard" }) if (!Anchors.Contains(k)) Anchors.Add(k);

            q.build = Build.Playable;
            q.offerSay = "THE PUBLIC ROAD'S THE CHEAP ONE NOW. IS IT STAYING THAT WAY?";
            q.offerReply = "NOT SINCE THE STORM. IT TOOK THE CROSSING ON THE PUBLIC ROAD, AND NOW NOTHING HEAVIER THAN A GOAT GETS OVER. GRETA HOLM'S ROAD CREW " +
                           "HAS MACHINES AND NO MONEY; I HAVE A LITTLE MONEY AND NO MACHINES. A BRIDGE WE CAN AFFORD, PLEASE. NOT A MONUMENT.";
            q.hook = "THE STORM TOOK THE CROSSING ON THE PUBLIC ROAD. GRETA HOLM'S ROAD CREW HAS MACHINES AND NO MONEY.";

            Step(q, "look", "LOOK AT THE WASHED-OUT CROSSING ON THE PUBLIC ROAD", "c4_crossing").When(Goal.Reach, "c4_crossing", 20f);
            Step(q, "crew", "TALK TO GRETA HOLM AT THE ROAD CREW'S YARD", "c4_yard").Says("c4_foreman", "c4_crew", "WHAT WILL IT TAKE?",
                "THREE WAYS. FILL IT: MY DIGGER AND THE TIPPER ARE YOURS TO BORROW IF THEY COME BACK TO THIS YARD WITH HALF A TANK AND NOTHING NEW BROKEN. " +
                "BRIDGE IT: A TIMBER DECK SPANS IT, FORTY WOOD AND FOUR IRON, OR STEEL IF YOU'RE RICH. OR, WHERE THERE'S WATER UNDER IT, OLD LUCAS RUNS A FERRY. " +
                "THEN WE TEST IT WITH THE TIPPER LOADED. IF IT HOLDS A FULL TIPPER, IT HOLDS A BUS.");
            Step(q, "timber", "OPTIONAL: BUY BRIDGE TIMBER FROM THE CREW", "c4_yard").Says("c4_foreman", "c4_timber", "SELL ME BRIDGE TIMBER.",
                "FORTY LENGTHS AND FOUR BOLTS. DON'T TELL THE CARPENTER WHAT I CHARGED YOU; HE'LL WANT A RAISE.").Optional()
                .Pays(r => { r.resources.Add((ResourceType.Wood, 40)); r.resources.Add((ResourceType.Iron, 4)); });
            q.steps[q.steps.Count - 1].any[0].price = 45;
            Step(q, "fix", "RESTORE THE CROSSING: FILL THE WASHOUT (DIGGER: UP/DN BOOM, Q CURL, E DUMP; OR TIP THE TIPPER IN), BUILD A BRIDGE DECK OVER IT, OR HIRE THE FERRY", "c4_crossing")
                .When(Goal.Event, "c4:filled", label: "FILLED IT")
                .When(Goal.Build, "bridge_timber|bridge_steel", 10f, "BRIDGED IT")
                .Says("c4_ferryman", "c4_ferry", "RUN YOUR FERRY FOR THE ROAD'S LOADS.",
                      "THREE TRIPS A DAY, NOT ON SUNDAYS, AND I DON'T TAKE GOATS. TWENTY FOR THE ROPE AND WE'RE IN BUSINESS.")
                .Pays(r => r.training.Add((Skill.Construction, 6f)));
            q.steps[q.steps.Count - 1].any[2].price = 20;
            Step(q, "test", "TEST IT: DRIVE THE CREW'S LOADED TIPPER ACROSS (OR, WITH THE FERRY, PARK IT AT THE NEAR BANK FOR LUCAS)", "c4_crossing")
                .When(Goal.Event, "c4:tested", label: "THE LOADED TIPPER CROSSED")
                .When(Goal.Event, "c4:ferried", label: "THE FERRY TOOK THE LOAD")
                .Pays(r => r.training.Add((Skill.Driving, 4f)));
            Step(q, "return", "BRING GRETA'S MACHINES BACK TO THE YARD: HALF A TANK EACH, NOTHING NEW BROKEN (OR PAY FOR THE WEAR)", "c4_yard")
                .When(Goal.Event, "c4:returned", label: "RETURNED AS AGREED")
                .Says("c4_foreman", "c4_wear", "I'LL PAY FOR THE WEAR AND TEAR.", "FORTY. THE DIGGER'S SEEN WORSE. SO HAVE I.");
            q.steps[q.steps.Count - 1].any[1].price = 40;
            Step(q, "credit", "GRETA PUTS UP A ROAD-WORK SIGN: WHOM DOES IT CREDIT?", "c4_yard")
                .Says("c4_foreman", "c4_crew_sign", "CREDIT THE ROAD CREW.", "HA. THEY'LL PRETEND NOT TO CARE AND THEN READ IT EVERY DAY.")
                .Says("c4_foreman", "c4_all_sign", "CREDIT EVERYONE WHO HELPED.", "EVERYONE. EVEN LUCAS. EVEN THE GOAT THAT WATCHED.")
                .Says("c4_foreman", "c4_me_sign", "PUT MY NAME ON IT.", "IT'S PARTLY YOUR MONEY IN THERE. FINE. BIG LETTERS OR SMALL? DON'T ANSWER THAT.");
            Step(q, "settled", "THE ROAD-WORK SIGN GOES UP AT THE CROSSING", "c4_crossing").When(Goal.Event, "c4:settled");
            q.reward.scrap = 30; q.reward.training.Add((Skill.Construction, 8f)); q.reward.flag = "c4_done";
            q.payoff = "THE CROSSING ON THE PUBLIC ROAD CARRIES A LOADED TIPPER AGAIN. GRETA'S CREW WILL LEND YOU THEIR MACHINES.";
        }

        /// <summary>C4's closing line from the way the crossing was restored, the machines and the sign.</summary>
        public static string C4Payoff()
        {
            string fix = Story.Route("C4", "fix"), ret = Story.Route("C4", "return"), sign = Story.Route("C4", "credit");
            string how = fix == "FILLED IT" ? "FILLED WITH THE CREW'S MACHINES" : fix == "BRIDGED IT" ? "BRIDGED WITH A DECK YOU BUILT" : fix != null ? "SERVED BY OLD LUCAS'S FERRY" : "RESTORED";
            string mach = ret == "RETURNED AS AGREED" ? "GRETA'S MACHINES CAME BACK AS AGREED" : ret != null ? "YOU PAID FOR THE WEAR ON GRETA'S MACHINES" : "";
            string credit = sign == null ? "" : sign.StartsWith("CREDIT THE ROAD") ? "THE SIGN CREDITS THE ROAD CREW" : sign.StartsWith("CREDIT EVERYONE") ? "THE SIGN CREDITS EVERYONE WHO HELPED" : "THE SIGN HAS YOUR NAME ON IT";
            return "THE CROSSING ON THE PUBLIC ROAD IS " + how + ". " + mach + (mach.Length > 0 ? "; " : "") + credit + ". THE CREW WILL LEND YOU THEIR MACHINES AGAIN.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_C4(List<Member> into)
        {
            into.Add(new Member { key = "c4_foreman", name = "GRETA HOLM", title = "ROAD FOREMAN", anchor = "c4_yard", temper = Temper.Proud, female = true,
                                  outfit = new[] { "overalls", "boots", "gloves", "helmet" }, tool = "tool_shovel" });
            into.Add(new Member { key = "c4_ferryman", name = "OLD LUCAS", title = "FERRYMAN", anchor = "c4_ferry", temper = Temper.Pious,
                                  outfit = new[] { "coat", "pants", "boots", "cowboy" },
                                  present = () => StoryAnchors.Has("c4_ferry") && Story.StateOf("C4") == Story.State.Active });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>C4: the crossing on the public road (a ford where a river runs over it, else the old culvert about two
        /// fifths of the way out), road points 14 m to either side, the crew's yard beside the road on the market side,
        /// and, only where there's water, the ferryman's bank.</summary>
        static partial void Anchors_C4(WorldGen world, Settlement town)
        {
            ArcC.Bind(world, town);
            var road = ArcC.road; float len = ArcC.Length(road);
            float cd = -1f;
            for (float d = 20f; d < len - 20f; d += 4f)                                            // a real ford first
            {
                var p = ArcC.At(road, d, out _);
                var s = world.Sample(p.x, p.z);
                if (!float.IsNaN(s.water) && s.water > s.height + 0.1f && world.RiverAt(p.x, p.z, out _, out _, out _)) { cd = d; break; }
            }
            bool wet = cd > 0f;
            if (!wet)
            {
                cd = len * 0.42f;
                foreach (float f in new[] { 0.42f, 0.36f, 0.48f, 0.3f })
                {
                    var p = ArcC.At(road, len * f, out _);
                    if (ArcC.Free(p, 45f, "c2_public")) { cd = len * f; break; }
                }
            }
            var c = ArcC.At(road, cd, out var dir);
            ArcC.Put(world, "c4_crossing", c, ArcC.Yaw(dir));
            ArcC.Put(world, "c4_near", ArcC.At(road, Mathf.Max(0f, cd - 14f), out _), ArcC.Yaw(dir));
            ArcC.Put(world, "c4_far", ArcC.At(road, Mathf.Min(len, cd + 14f), out _), ArcC.Yaw(dir));
            var yard = ArcC.Beside(world, road, Mathf.Max(0f, cd - 30f), 13f, p => ArcC.Free(p, 12f, "c4_near", "c4_crossing"), out float yf, ArcC.trackSide);
            ArcC.Put(world, "c4_yard", yard, yf);
            Clearing("c4_yard", 14f); Clearing("c4_crossing", 9f);
            if (wet)
            {
                var bank = ArcC.At(road, Mathf.Max(0f, cd - 9f), out var bd) + new Vector3(bd.z, 0f, -bd.x) * -ArcC.trackSide * 7f;
                ArcC.Put(world, "c4_ferry", bank, ArcC.Yaw(c - bank));
            }
        }
    }
}
