using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // C1 WATER HAS NO FLAG (storyline §7): a Guild toll post on the village road holds Isaac Dune's water bowser. The toll
    // book says 600 L of foul water; Isaac says river water for the village. Hear both, test the water at the bowser's
    // valve with the kit (quantity and quality against the claim), stop the valve weeping, then settle it: the measured
    // toll with the reading, the Nomads' old salt road (legal: it isn't a Guild road), the booked sixty, or just take it
    // (the Guild remembers). Tow the bowser to the village. The toll book has the village down for deliveries that never
    // come: the arc's next question.
    public static partial class StoryLibrary
    {
        public const string C1Slip = "story_c1_slip";

        static partial void Author_C1(QuestDef q)
        {
            ItemIds.Register(C1Slip, "WATER TEST SLIP (ISAAC'S BOWSER)");
            foreach (var k in new[] { "c_market", "c_village", "c1_toll", "c1_rig", "c1_salt", "c1_dest" }) if (!Anchors.Contains(k)) Anchors.Add(k);

            q.build = Build.Playable;
            q.offerSay = "YOU LOOK LIKE SOMEONE'S MAP JUST LEFT YOU OUT.";
            q.offerReply = "MY COUSIN'S. THE GUILD TOLL POST ON THE VILLAGE ROAD IS HOLDING ISAAC'S WATER BOWSER. THEIR BOOK SAYS SIX HUNDRED LITRES OF FOUL " +
                           "WATER: TOLL DOUBLE OR IT GOES IN THE DITCH. ISAAC SAYS IT'S RIVER WATER FOR THE VILLAGE. ONE OF THEM CAN'T READ A GAUGE. GO AND FIND OUT WHICH?";
            q.hook = "SERA DUNE: THE GUILD TOLL POST ON THE VILLAGE ROAD IS HOLDING HER COUSIN ISAAC'S WATER BOWSER. THE TOLL BOOK AND THE DRIVER DISAGREE ABOUT WHAT'S IN IT.";

            Step(q, "post", "FIND THE GUILD TOLL POST ON THE VILLAGE ROAD AND ISAAC'S BOWSER", "c1_toll").When(Goal.Reach, "c1_toll", 16f);
            Step(q, "claim", "HEAR THE TOLL KEEPER", "c1_toll").Says("c1_keeper", "c1_claim", "WHY ARE YOU HOLDING THE NOMAD WATER?",
                    "TOLL BOOK: ONE NOMAD BOWSER, SIX HUNDRED LITRES, UNTREATED. UNTREATED PAYS DOUBLE: SIXTY SCRAP, OR IT GOES IN THE DITCH AS A HEALTH RISK. " +
                    "I DON'T WRITE THE BOOK, I READ IT. AND THE BOOK SAYS THAT VILLAGE HAD A GUILD DELIVERY LAST WEEK, SO THEY'RE NOT THIRSTY.").Optional()
                .Pays(r => r.evidence.Add("toll book"));
            Step(q, "side", "HEAR ISAAC DUNE, THE DRIVER", "c1_rig").Says("c1_driver", "c1_side", "WHAT'S REALLY IN THE BOWSER?",
                    "THREE HUNDRED AND EIGHTY LITRES FROM THE SPRING AT THE RED CUT, SETTLED TWICE. SILTY, NOT FOUL. THEIR MAN FORCED THE VALVE LOOKING FOR " +
                    "CONTRABAND AND NOW IT WEEPS. HERE: SERA'S SPARE TEST KIT, SHE SAID YOU'D WANT PROOF. AND A HOSE CLAMP, FOR THE VALVE.").Optional()
                .Pays(r => { r.items.Add((UtilityIds.WaterTest, 1)); r.resources.Add((ResourceType.Iron, 1)); r.resources.Add((ResourceType.Scrap, 2)); });
            Step(q, "hear", "HEAR BOTH SIDES: THE TOLL KEEPER AND ISAAC", "c1_toll").When(Goal.Steps, "claim,side", 2f);
            Step(q, "test", "TEST THE BOWSER'S WATER: STAND AT ITS VALVE (THE BACK) AND USE THE WATER TEST KIT", "c1_rig").When(Goal.Event, "c1:tested").Optional()
                .Pays(r => r.training.Add((Skill.Survival, 3f)));
            Step(q, "valve", "STOP THE LEAK: [B] BUILD MODE, AIM AT THE BOWSER'S VALVE, R TO REPAIR (ISAAC'S CLAMP COVERS THE IRON)", "c1_rig").When(Goal.Event, "c1:valve").Optional()
                .Pays(r => r.training.Add((Skill.Mechanics, 3f)));
            Step(q, "check", "CHECK THE SHIPMENT AGAINST THE CLAIM AND STOP THE VALVE WEEPING", "c1_rig").When(Goal.Steps, "test,valve", 2f);
            Step(q, "resolve", "SETTLE IT: SHOW THE KEEPER YOUR READING, TAKE THE OLD SALT ROAD, OR PAY WHAT HIS BOOK SAYS", "c1_toll")
                .Says("c1_keeper", "c1_fair", "YOUR BOOK SAYS 600 L OF FOUL WATER. THE KIT SAYS LESS, AND SILTY. CHARGE THE REAL TOLL.",
                      "...THAT'S A LOT OF SILT FOR A HEALTH RISK. FINE. THREE HUNDRED-ODD LITRES OF RIVER WATER, NINETEEN SCRAP. I'LL WRITE 'MEASURED' IN THE MARGIN. NOBODY READS THE MARGIN.").Needs(C1Slip)
                .Says("c1_driver", "c1_saltroad", "THE OLD SALT ROAD ISN'T A GUILD ROAD. WE TAKE THE BOWSER ROUND THAT WAY.",
                      "MY GRANDMOTHER DROVE IT BEFORE THERE WAS A GUILD TO DRIVE AROUND. ROUGH, LONG AND OURS. KEEP IT OFF HIS ROAD PAST THE OLD CAIRN (IT'S ON YOUR MAP) AND HE CAN'T SAY A WORD.")
                .Says("c1_keeper", "c1_booked", "FINE. SIXTY, AS BOOKED.",
                      "SIXTY. PLEASURE. THE BOOK'S ALWAYS RIGHT WHEN SOMEBODY PAYS IT. SHE'S FREE TO GO.")
                .When(Goal.Event, "c1:taken", label: "TOOK IT PAST HIM");
            q.steps[q.steps.Count - 1].any[0].price = 19;
            q.steps[q.steps.Count - 1].any[2].price = 60;
            Step(q, "deliver", "TOW THE BOWSER TO THE VILLAGE: HITCH IT TO A CAR ([J]; ISAAC'S PICKUP WILL DO) AND BRING IT TO ISAAC", "c1_dest").When(Goal.Bring, "c1_bowser", 24f)
                .Pays(r => r.training.Add((Skill.Driving, 4f)));
            Step(q, "thanks", "TALK TO ISAAC AT THE VILLAGE", "c1_dest").Says("c1_driver", "c1_thanks", "THE WATER'S HERE. WHAT ELSE IS IN THAT TOLL BOOK?",
                "IT'S HERE AND MOSTLY STILL WET. THE THING ABOUT THAT BOOK: IT HAS THIS VILLAGE DOWN FOR A GUILD DELIVERY EVERY WEEK. THEY HAVEN'T SEEN A GUILD TRUCK " +
                "SINCE THE SHORTAGE. THREE MORE PLACES ON THIS ROAD ARE IN THERE TOO. ASK SERA WHAT THAT MEANS; I ONLY DRIVE. KEEP THE SPARE TANK. AND THE MAP.");
            q.reward.scrap = 20; q.reward.training.Add((Skill.Survival, 4f)); q.reward.training.Add((Skill.Speech, 3f)); q.reward.flag = "c1_done";
            q.payoff = "ISAAC'S WATER REACHED THE VILLAGE. THE TOLL BOOK HAS SETTLEMENTS DOWN FOR DELIVERIES THAT NEVER COME: WHY?";
        }

        /// <summary>C1's closing line from how it was settled, how much arrived and whether the valve was stopped.</summary>
        public static string C1Payoff(int litres)
        {
            string how = Story.Route("C1", "resolve");
            string way = how == null ? "" : how.StartsWith("YOUR BOOK") ? "AT THE MEASURED TOLL, 'MEASURED' IN THE MARGIN"
                : how.StartsWith("THE OLD SALT") ? (Story.Flag("c1_via_salt") ? "ROUND THE OLD SALT ROAD, NOT A SCRAP PAID" : "ON THE SALT ROAD'S WORD, THOUGH IT CAME BACK ON THE GUILD'S")
                : how.StartsWith("FINE. SIXTY") ? "FOR THE BOOKED SIXTY, SO THE BOOK STAYS RIGHT"
                : "PAST THE KEEPER WITHOUT HIS SAY-SO; THE GUILD HAS YOUR NAME NOW";
            return litres + " L OF ISAAC'S WATER REACHED THE VILLAGE, " + way + ". THE TOLL BOOK HAS SETTLEMENTS DOWN FOR DELIVERIES THAT NEVER COME: WHY?";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_C1(List<Member> into)
        {
            into.Add(new Member { key = "c1_keeper", name = "MORGAN FETCH", title = "GUILD TOLL KEEPER", anchor = "c1_toll", temper = Temper.Gruff,
                                  outfit = new[] { "vest", "pants", "boots", "beanie" } });
            into.Add(new Member { key = "c1_driver", name = "ISAAC DUNE", title = "NOMAD WATER CARRIER", anchor = "c1_rig", temper = Temper.Proud,
                                  outfit = new[] { "poncho", "pants", "boots", "shemagh" },
                                  anchorNow = () => ArcCCrew.Anchor("c1_driver", Story.StepDone("C1", "resolve") ? "c1_dest" : "c1_rig"),
                                  present = () => ArcCCrew.Present("c1_driver", Story.StateOf("C1") == Story.State.Active) });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>C1 (binds arc C's geography first): the toll post beside the public road a fifth of the way from the
        /// market, the bowser parked on its verge, the old salt road's cairn off to the side (away from the warden's
        /// track), Isaac's drop at the village's edge.</summary>
        static partial void Anchors_C1(WorldGen world, Settlement town)
        {
            ArcC.Bind(world, town);
            var road = ArcC.road; float len = ArcC.Length(road);
            ArcC.Put(world, "c_market", road[0], ArcC.Yaw(road[road.Count - 1] - road[0]));
            ArcC.Put(world, "c_village", road[road.Count - 1], ArcC.Yaw(road[0] - road[road.Count - 1]));

            var toll = ArcC.Beside(world, road, len * 0.22f, 10f, p => ArcC.Free(p, 45f, "c_market", "c_village") && ArcC.Flat(p, road[0]) > 40f, out float tf, -ArcC.trackSide);
            ArcC.Put(world, "c1_toll", toll, tf);
            // the bowser's verge: 9 m further along the road on the same side
            var tq = Quaternion.Euler(0f, tf, 0f);
            var rig = toll + tq * new Vector3(-9f, 0f, 0f);
            if (!ArcC.OffRoad(world, rig, 4f)) rig = toll + tq * new Vector3(9f, 0f, 0f);
            ArcC.Put(world, "c1_rig", rig, tf);
            Clearing("c1_toll", 12f); Clearing("c1_rig", 14f);

            // the old salt road's cairn: open country off the far side, around the middle of the route
            var mid = ArcC.At(road, len * 0.55f, out var md);
            var across = new Vector3(md.z, 0f, -md.x) * -ArcC.trackSide;
            Vector3 salt = mid + across * 140f;
            foreach (float d in new[] { 140f, 110f, 170f, 90f, 200f })
            {
                var p = mid + across * d;
                if (ArcC.OffRoad(world, p, 30f) && ArcC.Free(p, 40f)) { salt = p; break; }
            }
            ArcC.Put(world, "c1_salt", salt, ArcC.Yaw(mid - salt));
            Clearing("c1_salt", 6f);

            // Isaac's drop: open ground beside the road at the village's edge
            var dest = ArcC.Beside(world, road, len - 12f, 11f, p => ArcC.Free(p, 18f, "c_village"), out float df);
            ArcC.Put(world, "c1_dest", dest, df);
            Clearing("c1_dest", 10f);
        }
    }
}
