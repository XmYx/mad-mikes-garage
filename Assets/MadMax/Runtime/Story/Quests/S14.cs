using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S14 SOMETHING IN THE WELL: Oona Vale's inn water tastes of oil. Diagnosis, not a monster hunt: the water test kit
    // at her sink, tank or well says OILY on ground with no oil in it, so it is coming in from somewhere. Up the hill
    // Abel Dunmore's old pumpjack weeps crude into the ground. Stop it three ways: repair its seal (build mode R),
    // tear it out, or talk Dunmore into capping it. Then a new cartridge in the inn's filter (Oona has one; or make them
    // at a workbench), and the kit again until it says CLEAN. Meanwhile the inn still needs water: pour 20 L of clean
    // water into Oona's kettle barrel, or build her a rain collector or a tank. Filters, a canteen, training and free
    // refills at the inn's sink.
    public static partial class StoryLibrary
    {
        static partial void Author_S14(QuestDef q)
        {
            q.build = Build.Playable;
            StoryAnchors.Q5Declare("oona", "s14_leak");
            q.offerSay = "THE WATER HERE TASTES LIKE A GEARBOX.";
            q.offerReply = "YOU NOTICED. EVERYONE NOTICES. TWO WEEKS NOW. I CHANGED NOTHING, THE WELL CHANGED ON ITS OWN, AND HALF MY REGULARS HAVE GONE TO DRINKING BEER WITH BREAKFAST. " +
                           "I DON'T NEED A MONSTER HUNTER, I NEED SOMEONE WHO CAN READ A TEST KIT. A TRADER LEFT ME ONE. I CAN'T MAKE HEAD OR TAIL OF IT.";
            q.hook = "OONA VALE'S INN WATER TASTES OF OIL. SHE HAS A TEST KIT SHE CAN'T READ.";

            Step(q, "look", "LOOK AT THE INN'S WATER: WELL, FILTER, TANK AND SINK", "oona").When(Goal.Reach, "oona", 7f)
                .Pays(r => r.items.Add((UtilityIds.WaterTest, 1)));
            Step(q, "test", "TEST THE INN'S WATER: USE THE TEST KIT FROM THE HOTBAR AT THE SINK, THE TANK OR THE WELL", "oona").When(Goal.Event, "s14:tested")
                .Pays(r => r.training.Add((Skill.Survival, 2f)));
            Step(q, "supply", "MEANWHILE THE INN NEEDS CLEAN WATER: POUR 20 L INTO OONA'S KETTLE BARREL ([T]), OR BUILD A RAIN COLLECTOR OR A TANK BY THE INN", "oona")
                .When(Goal.Event, "s14:water_given", label: "HAULED 20 L OF CLEAN WATER")
                .When(Goal.Build, "rain_collector|water_tank", 16f, "BUILT A RAIN COLLECTOR OR TANK")
                .Optional().Pays(r => r.training.Add((Skill.Survival, 3f)));
            Step(q, "trace", "OILY, ON GROUND WITH NO OIL IN IT: IT COMES IN FROM SOMEWHERE. FOLLOW THE GROUND UPHILL TO WHATEVER IS LEAKING", "s14_leak").When(Goal.Reach, "s14_leak", 12f);
            Step(q, "stop", "STOP THE LEAK: REPAIR THE PUMPJACK'S SEAL ([B], R), TEAR IT OUT, OR TALK TO ITS OWNER", "s14_leak")
                .When(Goal.Event, "s14:repaired", label: "REPAIRED THE PUMP'S SEAL")
                .When(Goal.Event, "removed_story:pumpjack", label: "TORE THE PUMP OUT")
                .Says("s14_dunmore", "s14_cap", "YOUR PUMP IS WEEPING CRUDE INTO THE INN'S WELL.",
                    "INTO OONA'S? ...THAT OLD THING HASN'T PUMPED A DROP IN A YEAR, I JUST NEVER CAPPED IT. I'LL CAP IT TONIGHT. DON'T TELL HER IT WAS ME. SHE'LL KNOW ANYWAY. SHE ALWAYS KNOWS.")
                .Pays(r => r.training.Add((Skill.Mechanics, 4f)));
            Step(q, "cartridge", "OONA HAS A SPARE FILTER CARTRIDGE IN THE SHED", "oona")
                .Says("oona", "s14_spare", "GOT A SPARE CARTRIDGE FOR THAT FILTER?", "ONE. IN THE SHED. I WASN'T GOING TO WASTE IT WHILE THE WELL STILL STANK OF DIESEL. HERE.")
                .Optional().Pays(r => r.items.Add((UtilityIds.Cartridge, 1)));
            Step(q, "filter", "PUT A NEW CARTRIDGE IN THE INN'S FILTER ([E] AT THE FILTER; OONA HAS ONE, OR MAKE THEM AT A WORKBENCH)", "oona").When(Goal.Event, "s14:filter");
            Step(q, "clean", "TEST THE INN'S WATER AGAIN: IT SHOULD COME UP CLEAN ONCE THE FILTER HAS CAUGHT UP", "oona").When(Goal.Event, "s14:clean")
                .Pays(r => r.training.Add((Skill.Survival, 4f)));
            Step(q, "kept", "THE INN STILL NEEDS CLEAN WATER TODAY: 20 L IN OONA'S KETTLE BARREL ([T]), OR A RAIN COLLECTOR OR TANK BY THE INN", "oona").When(Goal.Steps, "supply", 1f);
            Step(q, "tell", "TELL OONA HER WATER IS CLEAN", "oona")
                .Says("oona", "s14_done", "THE WELL'S CLEAN. IT CAME FROM AN OLD PUMP UP THE HILL.",
                    "UP THE HILL. OF COURSE IT DID. ...IT TASTES OF NOTHING. I'D FORGOTTEN WATER TASTES OF NOTHING. THE SINK'S YOURS WHENEVER YOU'RE PASSING: FILL UP, NO CHARGE, FOR AS LONG AS I'VE GOT A ROOF.");
            q.reward.scrap = 20;
            q.reward.items.Add((UtilityIds.Cartridge, 2)); q.reward.items.Add((ItemIds.Canteen, 1));
            q.reward.training.Add((Skill.Mechanics, 4f)); q.reward.training.Add((Skill.Survival, 4f));
            q.reward.flag = "inn_refills";
            q.payoff = "OONA'S WELL RUNS CLEAN AGAIN, AND HER SINK IS YOURS TO FILL UP AT, FREE, FOR AS LONG AS SHE HAS A ROOF.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S14(List<Member> into)
        {
            into.Add(new Member { key = "oona", name = "OONA VALE", title = "INNKEEPER", anchor = "oona", temper = Temper.Gruff, female = true, outfit = new[] { "sweater", "jeans", "boots", "bandana" } });
            into.Add(new Member { key = "s14_dunmore", name = "ABEL DUNMORE", title = "PUMP OWNER", anchor = "s14_leak", temper = Temper.Proud, outfit = new[] { "overalls", "boots", "cowboy" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>Oona's inn on the road out of the first town (ground water there must be clean: no oil field, no sea);
        /// the leaking pumpjack on the highest open ground 110-240 m from it (upstream).</summary>
        static partial void Anchors_S14(WorldGen world, Settlement town)
        {
            var tc = Q5Centre(world, town);
            float r0 = town != null ? town.radius : 40f;
            System.Func<Vector3, bool> sweet = p => world.OilAt(p.x, p.z) < 0.15f && world.ContinentNoise(p.x, p.z) > 0.54f && world.Sample(p.x, p.z).biome != Biome.Nuclear;
            var inn = Q5Road(world, town, "oona", tc, r0 + 25f, r0 + 260f, 35f, 12f, sweet);
            // upstream: the highest open, level spot on rings 110-240 m out
            Vector3 best = Vector3.zero; float bh = float.MinValue;
            for (float rr = 110f; rr <= 240f; rr += 20f)
                for (int k = 0; k < 36; k++)
                {
                    float ang = (k * 10f + 5f) * Mathf.Deg2Rad;
                    var p = new Vector3(inn.x + Mathf.Sin(ang) * rr, 0f, inn.z + Mathf.Cos(ang) * rr);
                    if (!Open(world, p) || !Clear(town, p, 20f) || !Q5Free(p, 30f) || !Q5Dry(world, p, 3f) || !Level(world, p, 4f, 1.5f)) continue;
                    float h = world.Sample(p.x, p.z).height;
                    if (h > bh) { bh = h; best = p; }
                }
            if (bh > float.MinValue) Q5Set(world, "s14_leak", best, Q5Face(best, inn));
            else if (!Q5Ring(world, "s14_leak", inn, 100f, 300f, 45f, p => Q5Free(p, 15f), true)) Q5Set(world, "s14_leak", inn + new Vector3(0f, 0f, 150f), 180f);
            Clearing("oona", 10f);
            Clearing("s14_leak", 8f);
        }
    }
}
