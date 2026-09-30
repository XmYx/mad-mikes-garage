using System.Collections.Generic;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // L2 IRON PILGRIMAGE: the block and the heads, each with its keeper. At the bunker, Old Wick (whose father carried the
    // block down) will lift it out safely on his chain hoist once his generator runs; or search the bunker's stores the
    // old way. At the airfield, Marisol Quint (whose brother flew the heads out) keeps runway lights that don't work:
    // mend them and she gives the heads, or buy them, or search the radio hut. Either site first.
    public static partial class StoryLibrary
    {
        static partial void Author_L2(QuestDef q)
        {
            q.build = Build.Playable;
            q.storyOnly = false;
            Q3Anchors("l2_wick", "l2_marisol");
            q.offerSay = "I'LL GO AFTER THE BLOCK AND THE HEADS.";
            q.offerReply = "THEN GO AS A PILGRIM, NOT A LOOTER. OLD WICK SITS AT THE BUNKER WHERE THE BLOCK WAS HIDDEN; MARISOL QUINT KEEPS THE HANGAR WHERE THE HEADS WENT. " +
                           "LISTEN TO THEM FIRST. PEOPLE WHO GUARD A THING FOR THIRTY YEARS HAVE EARNED A HEARING.";
            q.hook = "THE V12 BLOCK LIES IN A BUNKER WITH OLD WICK; THE HEADS IN A HANGAR WITH MARISOL QUINT.";

            Step(q, "block", "THE V12 BLOCK")
                .When(Goal.Event, "l2:block_found", label: "FOUND IN THE BUNKER STORES")
                .When(Goal.Event, "l2:hoist", label: "LIFTED OUT ON WICK'S HOIST")
                .Optional().Pays(r => r.training.Add((Skill.Salvaging, 4f)));
            Step(q, "heads", "THE V12 HEADS")
                .When(Goal.Event, "l2:heads_found", label: "SALVAGED FROM THE RADIO HUT")
                .When(Goal.Event, "l2:lights", label: "MENDED MARISOL'S RUNWAY LIGHTS")
                .Says("l2_marisol", "l2_barter", "I'LL PAY FOR THEM: 60 SCRAP, AND I'LL TELL YOU WHERE THEY END UP.",
                    "SIXTY AND THE TRUTH. DONE. TAKE THEM BEFORE I GET SENTIMENTAL.").Q3Price(60)
                .Optional().Pays(r => r.training.Add((Skill.Mechanics, 3f)));
            Step(q, "wick_tale", "OPTIONAL: HEAR OLD WICK OUT", "l2_wick")
                .Says("l2_wick", "l2_wick", "WHY DID YOU STAY OUT HERE ALL THESE YEARS?",
                    "MY FATHER CARRIED THAT BLOCK DOWN THE RAMP ON A HAND CART AND HIS OWN BACK. HE SAID SOMEONE WOULD COME FOR IT WHO MEANT TO FINISH THE JOB. " +
                    "I THOUGHT HE MEANT ME. TURNS OUT HE MEANT SOMEBODY WITH BETTER KNEES. THE HOIST WILL BRING IT UP GENTLE, IF THE GENERATOR EVER RUNS AGAIN.")
                .Optional().Pays(r => r.training.Add((Skill.Speech, 2f)));
            Step(q, "marisol_tale", "OPTIONAL: ASK MARISOL ABOUT HER BROTHER", "l2_marisol")
                .Says("l2_marisol", "l2_tale", "HOW DID THE HEADS END UP IN A HANGAR?",
                    "MY BROTHER FLEW THEM OUT THE NIGHT THE CREW SPLIT UP, STRAPPED IN THE PASSENGER SEAT LIKE A SWEETHEART. HE NEVER FLEW AGAIN; SAID NOTHING WOULD SOUND THAT GOOD UNDER HIM. " +
                    "I KEEP THE RUNWAY LIGHTS FOR HIM. OR I WOULD, IF THEY WORKED.")
                .Optional().Pays(r => r.training.Add((Skill.Speech, 2f)));
            // the required steps wait for the relic in hand (the game hands it over once a way above is done)
            Step(q, "bunker", "THE BUNKER BLOCK: SEARCH THE BUNKER'S STORES, OR FUEL OLD WICK'S HOIST GENERATOR ([T] PETROL, [E] START) AND LET HIM LIFT IT OUT SAFELY", "l2_wick")
                .When(Goal.Event, "l2:has_block");
            Step(q, "airfield", "THE AIRFIELD HEADS: SEARCH THE HANGAR'S RADIO HUT, MEND MARISOL'S RUNWAY LIGHTS ([B] WITH A HAMMER, R ON EACH), OR MAKE HER AN OFFER", "l2_marisol")
                .When(Goal.Event, "l2:has_heads");
            q.reward.scrap = 20; q.reward.training.Add((Skill.Salvaging, 4f)); q.reward.flag = "l2_done";
            q.payoff = "THE BLOCK AND THE HEADS ARE YOURS, AND SO ARE TWO STORIES: A MAN WHO WAITED FOR SOMEONE WITH BETTER KNEES, AND A WOMAN WHO KEPT THE LIGHTS FOR HER BROTHER.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_L2(List<Member> into)
        {
            into.Add(new Member { key = "l2_wick", name = "OLD WICK", title = "BUNKER KEEPER", anchor = "l2_wick", temper = Temper.Nervous, outfit = new[] { "coat", "pants", "boots", "beanie" } });
            into.Add(new Member { key = "l2_marisol", name = "MARISOL QUINT", title = "HANGAR KEEPER", anchor = "l2_marisol", temper = Temper.Proud, female = true, outfit = new[] { "overalls", "boots", "goggles", "gloves" }, tool = "tool_wrench" });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>Wick outside the bunker that holds the block and Marisol in front of the hangar that held the heads (the
        /// same sites <see cref="MadMax.Game.LastEngine.Sites"/> picks); without such a site, at the edge of the first town.</summary>
        static partial void Anchors_L2(WorldGen world, Settlement town)
        {
            var tc = Q3Town(world, town);
            MadMax.Game.LastEngine.Sites(world, out var block, out var heads);
            if (block != null)
            {
                var c = new Vector3(block.pos.x, 0f, block.pos.y);
                var to = tc - c;
                float a0 = to.sqrMagnitude > 1f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : 0f;
                Q3Ring(world, c, block.reach + 4f, block.reach + 60f, a0, 25f, null, out var p, out var f);
                Q3Set(world, "l2_wick", p, f);
            }
            else Q3Edge(world, town, tc, "l2_wick", 20f + 180f);
            Clearing("l2_wick", 8f);
            if (heads != null)
            {
                var m = heads.ToWorld(Site.ApronX - 13f, -8f);
                var h = heads.ToWorld(Site.ApronX, 0f);
                Q3Set(world, "l2_marisol", new Vector3(m.x, 0f, m.y), Mathf.Atan2(h.x - m.x, h.y - m.y) * Mathf.Rad2Deg);
            }
            else Q3Edge(world, town, tc, "l2_marisol", 20f + 90f);
        }
    }
}
