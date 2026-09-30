using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S01 A FRIDGE FULL OF FLOWERS: Orla Finch's cold cabinet warms everything in it. Three honest faults, found and
    // fixed in any order: flower crates stacked against the back grille (break them or dismantle them), the supply cable
    // come away from her generator (run a new one), a dead compressor (a generator coil from the appliance dump down the
    // track, or one made at a workbench, plus a copper wire, fitted with [T]). Then let it run down below 8 °C, and
    // optionally put it behind an ESSENTIAL load breaker so her lamp goes first in a shortage. Orla was keeping this
    // year's first bunch cold for Sam, six years gone; she puts it on the table instead, and offers you one of his
    // pressed flowers. Take it or leave it with her: the pay is the same.
    public static partial class StoryLibrary
    {
        static partial void Author_S01(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_pressed_flower", "SAM'S PRESSED FLOWER");
            StoryAnchors.Q5Declare("orla", "s01_dump");
            q.offerSay = "THAT CABINET SOUNDS LIKE IT'S GIVING UP.";
            q.offerReply = "IT GAVE UP LAST WEEK. IT'S A COLD CABINET THAT WARMS THINGS NOW: MY FLOWERS WILT BY NOON. THE GENERATOR RUNS, THE LAMP'S ON, THE CABINET JUST SITS THERE " +
                           "GETTING WARMER. YOU LOOK LIKE YOU KNOW WHICH END OF A SPANNER TO HOLD. HAVE A LOOK?";
            q.hook = "ORLA FINCH'S FLOWER CABINET WARMS EVERYTHING INSIDE IT. SOMETHING, OR SEVERAL THINGS, IS WRONG WITH IT.";

            Step(q, "look", "LOOK THE CABINET OVER AT ORLA'S STALL", "orla").When(Goal.Reach, "orla", 5f);
            Step(q, "vent", "THE BACK GRILLE IS BURIED UNDER FLOWER CRATES: CLEAR THEM (BREAK THEM, OR [B] AND X TO DISMANTLE)", "orla").When(Goal.Event, "s01:vent").Optional()
                .Pays(r => r.resources.Add((ResourceType.Wood, 2)));
            Step(q, "cable", "THE SUPPLY CABLE HAS COME AWAY: RUN A NEW CABLE FROM ORLA'S GENERATOR TO THE CABINET ([B], CABLE)", "orla").When(Goal.Event, "s01:cable").Optional();
            Step(q, "part", "THE COMPRESSOR IS DEAD: FIT A GENERATOR COIL AND 1 COPPER ([T] AT THE CABINET)", "s01_dump").When(Goal.Event, "s01:compressor").Optional()
                .Pays(r => r.training.Add((Skill.Mechanics, 4f)));
            Step(q, "fix", "FIX THE CABINET: CLEAR THE GRILLE, RECONNECT THE CABLE, AND FIND A COIL AT THE APPLIANCE DUMP (MARKED) OR MAKE ONE AT A WORKBENCH", "s01_dump")
                .When(Goal.Steps, "vent,cable,part", 3f);
            Step(q, "test", "TEST IT: LET THE CABINET RUN DOWN TO 8 C OR LESS", "orla").When(Goal.Event, "s01:cold")
                .Pays(r => r.training.Add((Skill.Mechanics, 3f)));
            Step(q, "priority", "OPTIONAL: PUT THE CABINET BEHIND A LOAD BREAKER SET ESSENTIAL, SO THE LAMP GOES DARK FIRST IN A SHORTAGE", "orla").When(Goal.Event, "s01:essential").Optional()
                .Pays(r => r.training.Add((Skill.Construction, 4f)));
            Step(q, "tell", "TELL ORLA IT HOLDS THE COLD", "orla")
                .Says("orla", "s01_cold", "IT HOLDS THE COLD NOW. YOUR FLOWERS WILL KEEP.",
                    "...THESE AREN'T FOR SELLING, YOU KNOW. SAM PLANTED THE FIRST BULBS. EVERY SPRING I KEEP THE FIRST BUNCH COLD FOR HIM; SIX YEARS NOW. " +
                    "...NO. SAM WOULD HAVE SOLD THEM BY NINE AND BOUGHT A PIE WITH IT. THIS YEAR'S FIRST BUNCH GOES ON THE TABLE.");
            Step(q, "keepsake", "ORLA OFFERS YOU ONE OF SAM'S PRESSED FLOWERS", "orla")
                .Says("orla", "s01_take", "I'D BE GLAD TO KEEP ONE.", "HERE. IT'S BEEN IN A BOOK FOR SIX YEARS; IT CAN STAND A POCKET.")
                .Says("orla", "s01_leave", "KEEP IT. THAT ONE'S YOURS AND SAM'S.", "...THANK YOU. HE'D HAVE LIKED YOU. HE LIKED ANYONE WHO FIXED THINGS AND DIDN'T MAKE A SPEECH ABOUT IT.");
            Step(q, "keep", "A PRESSED FLOWER FROM ORLA", "orla").When(Goal.Event, "talk:orla:s01_take").Optional()
                .Pays(r => r.items.Add(("story_pressed_flower", 1)));
            Step(q, "bunch", "WATCH THIS YEAR'S FIRST BUNCH GO ON THE TABLE", "orla").When(Goal.Event, "s01:bunch");
            q.reward.scrap = 25;
            q.reward.items.Add(("seed_flower", 4)); q.reward.items.Add(("crop_flower", 3));
            q.reward.resources.Add((ResourceType.Wood, 6)); q.reward.resources.Add((ResourceType.Cloth, 2)); q.reward.resources.Add((ResourceType.Scrap, 2));
            q.reward.training.Add((Skill.Mechanics, 6f)); q.reward.flag = "orla_flowers";
            q.payoff = "ORLA'S CABINET HOLDS FOUR DEGREES. THIS YEAR'S FIRST BUNCH SOLD BEFORE NINE. SHE SENT YOU OFF WITH SEEDS AND THE MAKINGS OF AN ICE BOX.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S01(List<Member> into)
        {
            into.Add(new Member { key = "orla", name = "ORLA FINCH", title = "FLOWER SELLER", anchor = "orla", temper = Temper.Friendly, female = true, outfit = new[] { "sweater", "pants", "boots", "scarf" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>Orla's flower stall on open, level ground at the edge of the first town; the appliance dump on a
        /// patch of waste ground 50-140 m further out.</summary>
        static partial void Anchors_S01(WorldGen world, Settlement town)
        {
            var o = Q5Edge(world, town, "orla", 12f, 90f, 200f, 5f, 30f);
            var tc = Q5Centre(world, town);
            float away = Q5Face(tc, o);
            if (!Q5Ring(world, "s01_dump", o, 50f, 140f, away - 40f, p => Clear(town, p, 25f) && Q5Free(p, 25f) && Q5Dry(world, p, 3f), false)
                && !Q5Ring(world, "s01_dump", o, 40f, 220f, away, p => Clear(town, p, 10f) && Q5Free(p, 15f), false))
                Q5Set(world, "s01_dump", o + Quaternion.Euler(0f, away, 0f) * new Vector3(0f, 0f, 60f), away);
            Clearing("orla", 9f);
            Clearing("s01_dump", 7f);
        }
    }
}
