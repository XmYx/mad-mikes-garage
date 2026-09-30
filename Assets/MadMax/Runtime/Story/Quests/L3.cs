using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // L3 A CRANKSHAFT AND A GRUDGE: Grist, a road boss, wears the crank on a chain and camps off the long road between
    // runs. Beat his time from his flag to the marker post (wheels on the ground; try again as often as you like), trade
    // him a supercharger kit or pay him, show his crew the marker he never paid Dell Farrow (the mechanic who machined
    // the crank), or take it: killing Grist, or the boss of the longest road the old way, counts too.
    public static partial class StoryLibrary
    {
        static partial void Author_L3(QuestDef q)
        {
            q.build = Build.Playable;
            q.storyOnly = false;
            ItemIds.Register("story_grist_marker", "GRIST'S MARKER");
            Q3Anchors("l3_camp", "l3_finish", "l3_dell");
            q.offerSay = "WHO HAS THE CRANKSHAFT?";
            q.offerReply = "A ROAD BOSS CALLED GRIST WEARS IT ON A CHAIN AND CALLS IT HIS LUCK. HE CAMPS OFF THE LONG ROAD BETWEEN RUNS. YOU NEEDN'T BLEED FOR IT: " +
                           "GRIST LIKES A RACE, LIKES A DEAL, AND OWES A MECHANIC CALLED DELL FARROW MORE THAN HE ADMITS.";
            q.hook = "GRIST, A ROAD BOSS, WEARS THE V12 CRANKSHAFT FOR LUCK. RACE HIM, DEAL, CALL IN AN OLD DEBT, OR TAKE IT.";

            Step(q, "dell", "OPTIONAL: DELL FARROW, THE MECHANIC GRIST NEVER PAID, SITS AT THE EDGE OF THE FIRST TOWN", "l3_dell")
                .Says("l3_dell", "l3_dell", "GRIST SAYS HE PAYS HIS DEBTS.",
                    "HA! I MACHINED THAT CRANK TRUE FOR HIM: SIX WEEKS ON A LATHE WITH NO BELT, FOR PROTECTION THAT NEVER CAME. HERE'S HIS MARKER, WITH HIS MARK ON IT. " +
                    "HIS CREW DON'T KNOW. SHOW THEM AND WATCH HIM SWEAT.")
                .Optional().Pays(r => r.items.Add(("story_grist_marker", 1)));
            Step(q, "race", "OPTIONAL: CHALLENGE GRIST TO A RACE", "l3_camp")
                .Says("l3_grist", "l3_race", "I'LL RACE YOU FOR IT.",
                    "HA! FROM MY FLAG TO THE MARKER POST DOWN THE ROAD, WHEELS ON THE GROUND. BEAT MY TIME AND IT'S YOURS. LOSE AND TRY AGAIN; I LIKE WATCHING.")
                .Optional();
            Step(q, "camp", "FIND GRIST'S CAMP OFF THE LONG ROAD", "l3_camp")
                .When(Goal.Reach, "l3_camp", 14f)
                .When(Goal.Event, "l3:has_crank", label: "THE CRANK CAME TO YOU FIRST");
            Step(q, "crank", "WIN THE CRANKSHAFT: BEAT GRIST'S TIME, TRADE HIM A SUPERCHARGER KIT OR 150 SCRAP, SHOW HIS CREW DELL'S MARKER, OR TAKE IT", "l3_camp")
                .When(Goal.Event, "l3:race_won", label: "BEAT GRIST'S TIME")
                .Says("l3_grist", "l3_trade", "A SUPERCHARGER KIT FOR THAT OLD CRANK.",
                    "...A BLOWER FOR MY OWN RIG. THE LUCK WAS GETTING HEAVY ANYWAY. DEAL.").Needs("kit_supercharger")
                .Says("l3_grist", "l3_buy", "150 SCRAP. MORE THAN IT'S WORTH TO ANYONE BUT YOU.",
                    "IT'S WORTH MORE TO ME. BUT SCRAP SPENDS AND LUCK DOESN'T. TAKE IT.").Q3Price(150)
                .Says("l3_grist", "l3_marker", "YOUR CREW MIGHT LIKE TO SEE WHAT YOU OWE DELL FARROW.",
                    "...PUT THAT AWAY. PUT IT AWAY! FINE. THE CRANK FOR THE MARKER, AND I PAY DELL, AND NOBODY SAYS ANYTHING. EVER.").Needs("story_grist_marker")
                .When(Goal.Event, "l3:fought", label: "TOOK IT BY FORCE");
            Step(q, "back", "BRING THE CRANKSHAFT TO BROTHER CASK", "cask")
                .Says("cask", "l3_back", "GRIST'S LUCK. IT'S HARLAN'S AGAIN.",
                    "YOU CAN SEE HARLAN'S MARKS ON THE WEBS. KEEP IT WITH THE OTHERS. AND GO CAREFULLY ON THE LONG ROAD: GRIST'S KIND DON'T FORGET, EVEN WHEN THEY LOST FAIR.")
                .Needs("relic_crank")
                .When(Goal.Event, "l3:in_engine", label: "IT'S ALREADY IN THE ENGINE");
            q.reward.training.Add((Skill.Driving, 4f)); q.reward.training.Add((Skill.Speech, 4f)); q.reward.flag = "l3_done";
            q.payoff = "THE CRANKSHAFT IS HARLAN'S AGAIN.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_L3(List<Member> into)
        {
            into.Add(new Member { key = "l3_grist", name = "GRIST", title = "ROAD BOSS", anchor = "l3_camp", temper = Temper.Proud, outfit = new[] { "vest_scrap", "pants", "combat_boots", "goggles" }, tool = "tool_pipe_shotgun" });
            into.Add(new Member { key = "l3_dell", name = "DELL FARROW", title = "MACHINIST", anchor = "l3_dell", temper = Temper.Gruff, female = true, outfit = new[] { "overalls", "boots", "gloves", "bandana" }, tool = "tool_wrench" });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>Grist's camp beside a road 500-1300 m out of the first town, the race's marker post 550-850 m from it
        /// by another roadside, and Dell at the edge of the first town.</summary>
        static partial void Anchors_L3(WorldGen world, Settlement town)
        {
            var tc = Q3Town(world, town);
            float r0 = town != null ? town.radius : 40f;
            Q3Roadside(world, tc, r0 + 500f, r0 + 1300f, town, "l3_camp", 120f, 30f);
            Clearing("l3_camp", 14f);
            Q3Roadside(world, Get("l3_camp"), 550f, 850f, town, "l3_finish", 40f, 0f);
            Clearing("l3_finish", 5f);
            Q3Edge(world, town, tc, "l3_dell", 70f);
            Clearing("l3_dell", 6f);
        }
    }
}
