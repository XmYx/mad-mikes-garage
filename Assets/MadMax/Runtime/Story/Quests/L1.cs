using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // THE LAST ENGINE chapters L1-L5 wrap the sandbox relic hunt (Game/LastEngine.cs: relic flags, rumour pins, the
    // garage recipe, the Last Run) in stories. They run in sandbox worlds too (storyOnly = false): every chapter accepts
    // relics found the old way (bunker and hangar loot, the gang boss's body, the Church's gift at FRIENDLY) and adds
    // people and non-violent ways to the same four pieces.
    //
    // L1 THE SOUND BEFORE THE STORM: Brother Cask plays a tape of the engine; Mags Orton, who raced before the fuel ran
    // out, knows the cracked shop bell on it: Harlan's Works. Harlan's build sheet says why the engine was split in four
    // and where the pieces went (the relics go on the map).
    public static partial class StoryLibrary
    {
        static partial void Author_L1(QuestDef q)
        {
            q.build = Build.Playable;
            q.storyOnly = false;
            ItemIds.Register("story_build_sheet", "HARLAN'S BUILD SHEET");
            Q3Anchors("cask", "l1_mags", "l1_works");
            q.offerSay = "THEY SAY YOU KEEP THE LAST ENGINE.";
            q.offerReply = "KEEP IS A BIG WORD. I KEEP A TAPE: THIRTY SECONDS OF THE ENGINE RUNNING, RECORDED THE DAY IT WAS FINISHED. SIT DOWN. EVERYONE SHOULD HEAR IT ONCE.";
            q.hook = "BROTHER CASK HAS A TAPE OF THE LAST ENGINE RUNNING, AND NOT ONE NAME TO GO WITH IT.";

            Step(q, "tape", "LISTEN TO BROTHER CASK'S TAPE", "cask")
                .Says("cask", "l1_tape", "PLAY ME THE TAPE.",
                    "(A V12 CATCHES, WINDS UP THROUGH TWO BLOWERS AND HOLDS ONE NOTE LIKE A HYMN. UNDER IT: A CRACKED SHOP BELL, A ROLLER DOOR, SOMEBODY LAUGHING.) " +
                    "THIRTY YEARS I'VE PLAYED THAT. I KNOW EVERY BREATH IN IT, AND NOT ONE NAME.");
            Step(q, "vet", "WHO KNOWS THAT SHOP BELL? MAGS ORTON RACED BEFORE THE FUEL RAN OUT: SHE SITS AT THE EDGE OF THE FIRST TOWN", "l1_mags")
                .Says("l1_mags", "l1_bell", "A V12, TWO BLOWERS AND A CRACKED SHOP BELL. KNOW IT?",
                    "...HARLAN'S WORKS. THAT BELL BUZZED ON THE SECOND RING; HARLAN NEVER FIXED IT, SAID IT KEPT CUSTOMERS HUMBLE. I SWEPT HIS FLOOR TWO SUMMERS. " +
                    "WHAT'S LEFT OF THE SHOP IS OUT ON THE OLD ROAD. GO AND LOOK, THEN COME BACK AND TELL ME I'M WRONG.")
                .Pays(r => r.training.Add((Skill.Speech, 2f)));
            Step(q, "works", "HARLAN'S WORKS, OUT ON THE OLD ROAD: SEE WHAT'S LEFT", "l1_works").When(Goal.Reach, "l1_works", 5f)
                .Pays(r => { r.items.Add(("story_build_sheet", 1)); r.training.Add((Skill.Salvaging, 3f)); });
            Step(q, "why", "ASK MAGS WHY ANYONE WOULD BREAK AN ENGINE LIKE THAT INTO FOUR", "l1_mags")
                .Says("l1_mags", "l1_why", "HARLAN'S BUILD SHEET. WHY WOULD ANYONE BREAK AN ENGINE LIKE THAT INTO FOUR?",
                    "BECAUSE IT WORKED. HE BUILT IT FOR THE LAST RACE BEFORE THE FUEL RAN OUT, AND EVERY WARLORD ON THE ROAD WANTED IT. SO THE CREW SPLIT IT FOUR WAYS AND WALKED FOUR WAYS. " +
                    "THE DRIVERS DID THE REST: A MACHINE NOBODY CAN HAVE TURNS INTO A STORY EVERYBODY CAN.")
                .Needs("story_build_sheet");
            Step(q, "back", "TELL BROTHER CASK WHERE HIS TAPE WAS MADE", "cask")
                .Says("cask", "l1_back", "IT WAS BUILT AT HARLAN'S WORKS. HERE'S HIS BUILD SHEET.",
                    "HARLAN. A NAME, AFTER THIRTY YEARS. ...THE SHEET SAYS WHERE THE PIECES WENT: THE BLOCK TO A BUNKER, THE HEADS TO AN AIRFIELD, THE CRANK TO A ROAD BOSS, THE BLOWERS TO US. " +
                    "IF YOU MEAN TO LOOK, GO AS A PILGRIM, AND BRING BACK STORIES AS WELL AS IRON.")
                .Needs("story_build_sheet");
            q.reward.training.Add((Skill.Mechanics, 4f)); q.reward.training.Add((Skill.Speech, 3f)); q.reward.flag = "l1_done";
            q.payoff = "HARLAN'S WORKS BUILT THE LAST ENGINE FOR THE LAST RACE; ITS CREW SPLIT IT SO NO WARLORD COULD OWN IT, AND THE DRIVERS MADE IT A LEGEND. THE FOUR PIECES ARE ON YOUR MAP.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_L1(List<Member> into)
        {
            // Brother Cask (core cast) keeps the Church's shrine now: give him his place
            int i = into.FindIndex(m => m.key == "cask");
            var cask = i >= 0 ? into[i] : new Member { key = "cask", name = "BROTHER CASK", title = "KEEPER OF THE LAST ENGINE", temper = Temper.Pious, outfit = new[] { "poncho", "pants", "boots" } };
            cask.anchor = "cask";
            if (i >= 0) into[i] = cask; else into.Add(cask);
            into.Add(new Member { key = "l1_mags", name = "MAGS ORTON", title = "RETIRED RALLY DRIVER", anchor = "l1_mags", temper = Temper.Gruff, female = true, outfit = new[] { "bomber", "jeans", "boots", "goggles" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>Cask's shrine at the edge of the nearest Church town (the first town when none is near), Mags at the edge
        /// of the first town, and Harlan's Works beside a road 350-900 m out of it.</summary>
        static partial void Anchors_L1(WorldGen world, Settlement town)
        {
            var tc = Q3Town(world, town);
            Settlement church = null; float cd = 3000f;
            foreach (var st in world.settlements)
            {
                if (!Q3Church(world, st)) continue;
                float d = Vector2.Distance(st.pos, new Vector2(tc.x, tc.z));
                if (d < cd) { cd = d; church = st; }
            }
            Q3Edge(world, church ?? town, tc, "cask", 160f, 50f);
            Clearing("cask", 12f);
            Q3Edge(world, town, tc, "l1_mags", 340f);
            Clearing("l1_mags", 6f);
            float r0 = town != null ? town.radius : 40f;
            Q3Roadside(world, tc, r0 + 350f, r0 + 900f, town, "l1_works", 80f, 200f);
            Clearing("l1_works", 12f);
        }

        /// <summary>The settlement belongs to the Church of the Last Engine (the rule of <see cref="Factions.OfSettlement"/>,
        /// computed from this world's seed so it holds before the terrain exists).</summary>
        static bool Q3Church(WorldGen w, Settlement st)
        {
            if (st == null || st.kind == Biome.City) return false;
            var r = new System.Random(Market.Seed(st.index, 2101, w.seed));
            double x = r.NextDouble();
            bool desert = w.NaturalBiome(st.pos.x, st.pos.y) == Biome.Desert;
            if (desert && x < 0.4) return false;
            return x > 0.8;
        }
    }
}
