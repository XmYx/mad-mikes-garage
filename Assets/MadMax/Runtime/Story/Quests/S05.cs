using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S05 NOT THAT KIND OF SHOT (storyline §10): Amos Bell's neglected range at the edge of town. Repair the three target
    // boards, hear the rules, then shoot his course from behind the line with a loaned pistol and loaned rounds: every
    // board hit twice, or the assisted lesson (six rounds into the berm with Amos steadying, no score kept). Either way a
    // dummy round makes the gun stop and you clear it with the muzzle downrange. The precision medal (live course in ten
    // rounds or fewer) is decorative. Nobody is shot.
    public static partial class StoryLibrary
    {
        internal const string S05Live = "WALK ME THROUGH THE RULES.";
        internal const string S05Assisted = "I'D RATHER NOT SHOOT FOR A SCORE. CAN YOU TALK ME THROUGH IT INSTEAD?";
        internal const string S05CourseLive = "THE LIVE COURSE", S05CourseAssisted = "THE ASSISTED LESSON";
        const string S05Default = "AMOS'S RANGE IS OPEN AGAIN. HE STILL SAYS 'MUZZLE DOWNRANGE' TO PEOPLE CARRYING SHOVELS.";

        /// <summary>The closing line: which lesson, and the medal.</summary>
        internal static string S05_Payoff(string course, bool medal)
        {
            if (course == S05CourseAssisted) return "AMOS WROTE YOUR NAME IN HIS BOOK: LESSON DONE, SAFE HANDS, NO SCORE KEPT. HE SAYS THAT'S THE PART THAT MATTERS.";
            if (course == S05CourseLive) return medal ? "EVERY BOARD, TWICE, WITH ROUNDS TO SPARE. AMOS PINNED HIS PRECISION MEDAL ON YOU AND TOLD YOU NOT TO LET IT GO TO YOUR HEAD."
                                                      : "EVERY BOARD, TWICE, AND A JAM CLEARED WITH THE MUZZLE WHERE IT BELONGS. AMOS HAS SEEN WORSE. HE SAYS SO, WARMLY.";
            return S05Default;
        }

        static partial void Author_S05(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_s05_medal", "RANGE PRECISION MEDAL");
            foreach (var k in new[] { "amos", "s05_line", "s05_targets" }) if (!Anchors.Contains(k)) Anchors.Add(k);
            q.offerSay = "IS THIS A SHOOTING RANGE?";
            q.offerReply = "IT WAS. THE BOARDS ARE FIREWOOD, THE BERM'S A RUMOUR, AND HALF THIS TOWN THINKS THE TRIGGER IS A FINGER REST. " +
                           "FIX MY TARGETS AND SHOOT MY COURSE. I LEND THE PISTOL AND THE ROUNDS; YOU BRING THE MANNERS.";
            q.hook = "AMOS BELL KEEPS A RANGE AT THE EDGE OF TOWN. NOBODY HAS SHOT STRAIGHT THERE IN YEARS.";
            Step(q, "look", "WALK DOWNRANGE AND LOOK AT THE TARGET BOARDS (NOBODY'S SHOOTING YET)", "s05_targets").When(Goal.Reach, "s05_targets", 6f)
                .Pays(r => { r.resources.Add((ResourceType.Scrap, 4)); r.resources.Add((ResourceType.Wood, 4)); });
            Step(q, "targets", "RESTORE THE THREE TARGET BOARDS: REPAIR THEM ([B] WITH THE CLAW HAMMER, AIM, [R]) OR PUT UP NEW ONES", "s05_targets")
                .When(Goal.Event, "s05:targets")
                .Pays(r => r.training.Add((Skill.Construction, 3f)));
            Step(q, "rules", "BACK BEHIND THE FIRING LINE: AMOS GOES OVER THE RULES", "amos")
                .Says("amos", "s05_live", S05Live,
                    "MUZZLE DOWNRANGE, ALWAYS. FINGER OFF THE TRIGGER TILL YOU MEAN IT. NOBODY SHOOTS FROM PAST THE LINE AND NOBODY WALKS OUT WHILE ANYONE'S LOADED. " +
                    "EVERY BOARD, TWICE. AND WHEN IT STOPS, AND IT WILL, KEEP IT POINTED AT THE BERM AND CLEAR IT: [R].")
                .Says("amos", "s05_assisted", S05Assisted,
                    "CAN DO. SAME RULES, NO SCORE. YOU HOLD, I STEADY; SIX ROUNDS INTO THE BERM FROM THE LINE, NICE AND SLOW. " +
                    "SOMEWHERE IN THERE IT'LL STOP ON YOU. MUZZLE DOWNRANGE, [R], AND WE CARRY ON. NO MEDAL THAT WAY. SAME LESSON.")
                .Pays(r => { r.items.Add(("tool_pipe_pistol", 1)); r.items.Add(("ammo_cartridge", 12)); });
            Step(q, "course", "SHOOT FROM BEHIND THE LINE: EVERY BOARD HIT TWICE (OR, ASSISTED, SIX ROUNDS INTO THE BERM)", "s05_line")
                .When(Goal.Event, "s05:course", label: S05CourseLive)
                .When(Goal.Event, "s05:assisted", label: S05CourseAssisted);
            Step(q, "jam", "THE GUN STOPPED: MUZZLE DOWNRANGE, CLEAR THE JAM ([R])", "s05_line").When(Goal.Event, "s05:jam")
                .Pays(r => r.training.Add((Skill.Firearms, 4f)));
            Step(q, "medal", "OPTIONAL: THE PRECISION MEDAL: THE LIVE COURSE IN TEN ROUNDS OR FEWER", "s05_line").When(Goal.Event, "s05:medal").Optional()
                .Pays(r => r.items.Add(("story_s05_medal", 1)));
            Step(q, "return", "GIVE AMOS HIS RANGE PISTOL BACK (ANY PIPE PISTOL WILL DO)", "amos")
                .Says("amos", "s05_return", "HERE'S YOUR PISTOL BACK.", "CLEAN, UNLOADED, MUZZLE AWAY FROM ME. YOU'D BE AMAZED HOW RARE THAT IS. THE ROUNDS ARE ON THE HOUSE, AND I OILED YOUR KIT WHILE YOU WERE AT IT.")
                .Needs("tool_pipe_pistol")
                .Pays(r => r.take.Add(("tool_pipe_pistol", 1)));
            q.reward.scrap = 10; q.reward.items.Add(("ammo_cartridge", 6)); q.reward.training.Add((Skill.Firearms, 8f)); q.reward.flag = "s05_done";
            q.payoff = S05Default;
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S05(List<Member> into)
        {
            into.Add(new Member { key = "amos", name = "AMOS BELL", title = "RANGE GROUNDSKEEPER", anchor = "amos", temper = Temper.Gruff, outfit = new[] { "vest", "pants", "boots", "cowboy", "goggles" }, tool = "tool_binoculars" });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>S05: a 25 m lane on open, fairly level ground pointing away from the first town: the firing line, the
        /// boards 20 m out, a berm behind them; Amos stands behind the line.</summary>
        static partial void Anchors_S05(WorldGen world, Settlement town)
        {
            if (town == null) return;
            var tc = new Vector3(town.pos.x, 0f, town.pos.y);
            void Put(string k, Vector3 p, float y) { p.y = world.Sample(p.x, p.z).height; at[k] = p; yaw[k] = y; }
            bool Spare(Vector3 p, float d)
            {
                foreach (var kv in at) if (new Vector2(kv.Value.x - p.x, kv.Value.z - p.z).magnitude < d) return false;
                return true;
            }
            Vector3 line = Vector3.zero, o = Vector3.forward; bool ok = false;
            for (int pass = 0; pass < 3 && !ok; pass++)
                for (float rr = town.radius + 25f; rr <= town.radius + 200f && !ok; rr += 10f)
                    for (int k = 0; k < 36 && !ok; k++)
                    {
                        float ang = (k * 10f + 290f) * Mathf.Deg2Rad;
                        var od = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                        var sd = new Vector3(od.z, 0f, -od.x);
                        var p = tc + od * rr;
                        if (!Open(world, p) || !Spare(p, pass < 2 ? 45f : 28f)) continue;
                        if (pass < 2)
                        {
                            bool clear = Spare(p + od * 20f, 40f);
                            foreach (var l in new[] { od * 6f, od * 12f, od * 20f, od * 26f, sd * 5f, -sd * 5f, -od * 3f - sd * 4.5f, od * 20f + sd * 4f, od * 20f - sd * 4f })
                                if (!clear || !Open(world, p + l)) { clear = false; break; }
                            if (!clear) continue;
                            float h0 = world.Sample(p.x, p.z).height;
                            if (Mathf.Abs(world.Sample(p.x + od.x * 20f, p.z + od.z * 20f).height - h0) > (pass == 0 ? 2f : 3.5f)) continue;
                        }
                        line = p; o = od; ok = true;
                    }
            if (!ok) { o = new Vector3(Mathf.Sin(290f * Mathf.Deg2Rad), 0f, Mathf.Cos(290f * Mathf.Deg2Rad)); line = tc + o * (town.radius + 60f); }
            var s = new Vector3(o.z, 0f, -o.x);
            float down = Mathf.Atan2(o.x, o.z) * Mathf.Rad2Deg;                                 // local +Z points downrange, away from town
            Put("s05_line", line, down);
            Put("s05_targets", line + o * 20f, down);
            Put("amos", line - o * 3f - s * 4.5f, down);
            Put("s05_lane", line + o * 10f, down);                                             // (clearing only)
            Clearing("s05_line", 6f); Clearing("s05_lane", 7f); Clearing("s05_targets", 8f); Clearing("amos", 4f);
        }
    }
}
