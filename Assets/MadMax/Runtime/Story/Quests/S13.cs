using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S13 ONE GOOD ROOF (storyline §10): Cal Nix's partner Margo is on a flat roof above a collapsed outside stair with a
    // bad ankle. Assess it, get up (climb the crates at the back and mantle, the grappling hook, or a ladder you build),
    // steady her (bandage, splint or an improvised brace), bring her down (a ladder, or pay Cal's crew for the long one).
    // Cal lends his hook, a bandage and ladder rails, so no skill or kit gates the rescue. No timer: there is no storm.
    public static partial class StoryLibrary
    {
        static partial void Author_S13(QuestDef q)
        {
            foreach (var k in new[] { "cal", "s13_house", "s13_yard" }) if (!Anchors.Contains(k)) Anchors.Add(k);

            q.build = Build.Playable;
            q.offerSay = "YOU'RE STARING AT THAT ROOF LIKE IT OWES YOU MONEY.";
            q.offerReply = "IT DOES. MARGO'S UP THERE. THE OUTSIDE STAIR CAME DOWN UNDER HER AND SHE'S TURNED AN ANKLE, AND MY BACK WON'T TAKE A LADDER TODAY. " +
                           "SHE SAYS THERE'S NO RUSH. SHE'S LYING, BUT POLITELY. HAVE A LOOK FOR A SAFE WAY UP?";
            q.hook = "CAL NIX'S PARTNER MARGO IS STUCK ON A ROOF ABOVE A COLLAPSED STAIR WITH A TURNED ANKLE.";

            Step(q, "look", "ASSESS THE BUILDING: THE STAIR IS DOWN. WALK ROUND IT FOR ANOTHER WAY UP", "s13_house").When(Goal.Reach, "s13_house", 7f);
            Step(q, "gear", "OPTIONAL: BORROW CAL'S ACCESS KIT", "cal")
                .Says("cal", "s13_gear", "LEND ME WHAT YOU'VE GOT FOR GETTING UP THERE.",
                      "MY HOOK: TWENTY-FOUR METRES OF ROPE AND BAD DECISIONS. A BANDAGE FOR HER ANKLE. AND THE RAILS OFF MY OLD LADDER, " +
                      "IF YOU'D RATHER NAIL ONE TOGETHER. THE CRATES AT THE BACK WILL TAKE A CLIMBER TOO, IF YOU'RE THAT SORT.").Optional()
                .Pays(r => { r.items.Add(("tool_grapple", 1)); r.items.Add(("med_bandage", 1)); r.resources.Add((ResourceType.Wood, 3)); });
            Step(q, "up", "GET ONTO THE ROOF: CLIMB THE CRATES AT THE BACK ([SPACE] AT A LEDGE), THROW THE GRAPPLING HOOK, OR BUILD A LADDER AGAINST A WALL ([B]) AND WALK INTO IT", "s13_house")
                .When(Goal.Event, "s13:up_climb", label: "CLIMBED THE CRATES")
                .When(Goal.Event, "s13:up_hook", label: "THE GRAPPLING HOOK")
                .When(Goal.Event, "s13:up_ladder", label: "UP A LADDER")
                .Pays(r => r.training.Add((Skill.Athletics, 5f)));
            Step(q, "steady", "MARGO'S ANKLE IS SWOLLEN. STEADY HER BEFORE ANYONE MOVES", "s13_house")
                .Says("s13_margo", "s13_strap", "LET ME STRAP THAT ANKLE.",
                      "OW. OKAY. THAT'S... ACTUALLY BETTER. DON'T TELL CAL I SAID OW.").Needs("med_bandage")
                .Says("s13_margo", "s13_splint", "SPLINT FIRST, THEN WE MOVE.",
                      "PROPER KIT. EITHER YOU'VE DONE THIS BEFORE OR YOU'RE A VERY CONFIDENT LIAR. EITHER WAY: THANK YOU.").Needs("med_splint")
                .Says("s13_margo", "s13_brace", "SIT STILL. I'LL BRACE IT WITH A ROOF BATTEN AND YOUR BELT.",
                      "MY GOOD BELT. FINE. ...IT'S HOLDING. YOU'D MAKE A ROOFER: YOU IMPROVISE BADLY, WITH CONFIDENCE.")
                .Pays(r => r.training.Add((Skill.Survival, 4f)));
            Step(q, "down", "BRING MARGO DOWN SAFELY: A LADDER AGAINST THE WALL ([B]), OR SHOUT FOR CAL'S CREW AND THEIR LONG LADDER", "s13_house")
                .When(Goal.Build, "ladder", 5f, "DOWN A LADDER")
                .Says("s13_margo", "s13_crew", "I'LL SHOUT FOR CAL'S CREW AND THEIR LONG LADDER.",
                      "THEY'LL CHARGE CAL DOUBLE AND TELL THE STORY FOR A YEAR. WORTH IT. ...HERE THEY COME, ARGUING.");
            q.steps[q.steps.Count - 1].any[1].price = 15;
            Step(q, "tell", "MARGO'S DOWN. TELL CAL", "cal")
                .Says("cal", "s13_tell", "SHE'S DOWN. KEEP HER OFF ROOFS FOR A WEEK.",
                      "A WEEK. SHE'LL LAST TWO DAYS. ...THANK YOU. REALLY. KEEP WHATEVER YOU BORROWED, AND HAVE MY OLD HANDBOOK: " +
                      "THE ROOFING CHAPTER'S THE ONE WITH THE TEA STAINS.");
            q.reward.items.Add(("book_builder", 1)); q.reward.resources.Add((ResourceType.Cloth, 6));
            q.reward.training.Add((Skill.Athletics, 5f)); q.reward.training.Add((Skill.Construction, 5f)); q.reward.flag = "s13_done";
            q.payoff = "MARGO NIX IS DOWN OFF THE ROOF, ANKLE AND PRIDE BOTH STRAPPED.";
        }

        /// <summary>S13's closing journal line from the routes taken.</summary>
        public static string S13Payoff()
        {
            string up = Story.Route("S13", "up"), st = Story.Route("S13", "steady"), dn = Story.Route("S13", "down");
            string how = st == null ? "" : st.StartsWith("LET ME STRAP") ? " WITH A STRAPPED ANKLE" : st.StartsWith("SPLINT") ? " IN A PROPER SPLINT" : " IN A BATTEN-AND-BELT BRACE";
            string way = dn != null && dn.StartsWith("I'LL SHOUT") ? "CAL'S CREW BROUGHT THE LONG LADDER, ARGUING" : "SHE CAME DOWN YOUR LADDER RUNG BY RUNG";
            string cal = up == "THE GRAPPLING HOOK" ? "HOOK LIKE A PIRATE" : up == "UP A LADDER" ? "BUILD LADDERS LIKE A SENSIBLE PERSON" : "CLIMB LIKE A CAT";
            return "MARGO NIX IS OFF THE ROOF" + how + ": " + way + ". CAL TELLS PEOPLE YOU " + cal + ".";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S13(List<Member> into)
        {
            into.Add(new Member { key = "cal", name = "CAL NIX", title = "ROOFER", anchor = "cal", temper = Temper.Joker,
                                  outfit = new[] { "tshirt", "overalls", "boots", "cowboy", "gloves" }, tool = "tool_claw_hammer" });
            // on the roof until she's down (the game lifts her there), then in the yard beside Cal
            into.Add(new Member { key = "s13_margo", name = "MARGO NIX", title = "ROOFER", anchor = "s13_house", temper = Temper.Proud, female = true,
                                  outfit = new[] { "tshirt", "jeans", "boots", "gloves", "beanie" },
                                  present = () => Story.StateOf("S13") == Story.State.Active || Story.StateOf("S13") == Story.State.Done,
                                  anchorNow = () => Story.StepDone("S13", "down") || Story.StateOf("S13") == Story.State.Done ? "s13_yard" : "s13_house" });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>S13: a small flat-roofed workshop on level open ground beside a road just out of town; Cal in front of
        /// it, the yard where Margo sits afterwards.</summary>
        static partial void Anchors_S13(WorldGen world, Settlement town)
        {
            if (town == null) return;
            var tc = new Vector3(town.pos.x, 0f, town.pos.y);
            if (!Roadside(world, tc, town.radius + 40f, town.radius + 420f, 20f,
                    p => Clear(town, p, 30f) && Level(world, p, 5f, 0.45f) && Q2.OpenAround(world, p, 5f) && Q2.Free(world, p, 45f), out var h, out var f)
                && !Q2.Near(world, tc, town.radius + 60f, town.radius + 700f, 45f, 5f, 0.7f, out h, out f))
                h = Q2.Anywhere(world, tc, town.radius + 120f, 200f, out f);
            Q2.Put(world, "s13_house", h, f);
            var r = Quaternion.Euler(0f, f, 0f);
            Q2.Put(world, "cal", h + r * new Vector3(-1.2f, 0f, 6.5f), f + 180f);
            Q2.Put(world, "s13_yard", h + r * new Vector3(4.2f, 0f, 5.2f), f + 200f);
            Clearing("s13_house", 12f);
        }
    }
}
