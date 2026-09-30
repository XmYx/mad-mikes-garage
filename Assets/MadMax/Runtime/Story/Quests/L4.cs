using System.Collections.Generic;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // L4 WHAT A RELIC IS FOR: the Church keeps the twin blowers. Earn its trust by standing (FRIENDLY with the Church:
    // the sandbox gift arrives as before) or by a mechanic's kindness: the Church's procession truck hasn't run in nine
    // years (engine nearly seized, dry tank); get it running and drive it to the shrine. Then Cask asks what a relic is for:
    // give the blowers to an engine that runs, or take them on loan for one run.
    public static partial class StoryLibrary
    {
        static partial void Author_L4(QuestDef q)
        {
            q.build = Build.Playable;
            q.storyOnly = false;
            Q3Anchors("l4_truck");
            q.offerSay = "WILL THE CHURCH PART WITH THE BLOWERS?";
            q.offerReply = "THE CHURCH ISN'T SURE THE BLOWERS WANT PARTING. SOME OF US SAY A RELIC IS FOR KEEPING STILL; SOME SAY HARLAN DIDN'T BUILD A STATUE. " +
                           "EARN OUR TRUST FIRST: BE A FRIEND TO THE CHURCH, OR DO US A MECHANIC'S KINDNESS. OUR PROCESSION TRUCK HASN'T TURNED A WHEEL IN NINE YEARS.";
            q.hook = "THE CHURCH KEEPS THE TWIN BLOWERS. BROTHER CASK ISN'T SURE WHAT A RELIC IS FOR.";

            Step(q, "trust", "EARN THE CHURCH'S TRUST: STAND AS A FRIEND OF THE CHURCH, OR GET ITS PROCESSION TRUCK RUNNING (ENGINE, FUEL, SERVICE [G]) AND DRIVE IT TO THE SHRINE", "cask")
                .When(Goal.Event, "l4:standing", label: "A FRIEND OF THE CHURCH")
                .When(Goal.Bring, "l4_truck", 14f, "THE PROCESSION TRUCK RUNS AGAIN")
                .Pays(r => r.training.Add((Skill.Mechanics, 5f)));
            Step(q, "decide", "WHAT IS A RELIC FOR? BROTHER CASK ASKS YOU", "cask")
                .Says("cask", "l4_run", "AN ENGINE THAT NEVER RUNS IS A PHOTOGRAPH OF AN ENGINE.",
                    "...HARLAN DIDN'T BUILD A PHOTOGRAPH. NO. TAKE THEM. LET THEM SING; WE'LL LISTEN FROM HERE.")
                .Says("cask", "l4_loan", "LEND THEM FOR ONE RUN. THEY COME HOME AFTER.",
                    "A LOAN, THEN, WRITTEN IN THE BOOK IN YOUR NAME. BRING THEM HOME WHEN THE RUN IS DONE, OR BRING US THE STORY; THE BOOK ACCEPTS EITHER.");
            Step(q, "given", "THE BLOWERS COME DOWN FROM THE ALTAR", "cask").When(Goal.Event, "l4:given");
            q.reward.training.Add((Skill.Speech, 3f)); q.reward.flag = "l4_done";
            q.payoff = "THE CHURCH'S BLOWERS ARE IN YOUR KEEPING NOW.";
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>The procession truck parked 40-80 m from Cask's shrine (bring it to within 14 m).</summary>
        static partial void Anchors_L4(WorldGen world, Settlement town)
        {
            var c = Get("cask");
            Q3Ring(world, c, 40f, 80f, Yaw("cask") + 180f, 20f, null, out var p, out var f);
            Q3Set(world, "l4_truck", p, f + 90f);
            Clearing("l4_truck", 7f);
        }
    }
}
