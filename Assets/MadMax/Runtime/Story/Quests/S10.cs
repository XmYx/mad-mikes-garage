using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S10 TWELVE VOLTS OF FAME (storyline §10): Pip Harlow's tiny car and the local hill trial. Fix what's wrong with it
    // (bald rear tyres, 150 kg of her brother's ballast, nearly dry oil and tank), then either shorten the gearing for the
    // measured slope or take the longer, gentler shoulder line, and drive it from the start flags to the top. Finishing is
    // what counts; the best-time medal is optional and pays nothing practical. Pip pays in tyres for your own car and a
    // hand-painted checker stripe.
    public static partial class StoryLibrary
    {
        internal const string S10Gears = "SHORT GEARS FOR THE SLOPE";
        internal const string S10Shoulder = "WE'LL TAKE THE SHOULDER: LONGER, BUT GENTLER.";
        const string S10Default = "PIP'S LITTLE CAR IS A HILL-TRIAL FINISHER NOW. PIP TELLS EVERYONE; THE CAR IS MODEST ABOUT IT.";

        /// <summary>The closing line: how the hill was beaten, and whether the clock was kind.</summary>
        internal static string S10_Payoff(string plan, bool medal, float seconds)
        {
            if (plan == null) return S10Default;
            string how = plan == S10Shoulder ? "PIP'S CAR TOOK THE SHOULDER LINE TO THE TOP AND NEVER ONCE STALLED" : "PIP'S CAR WENT STRAIGHT UP THE HILL ON SHORT GEARS";
            string time = seconds > 0f ? " (" + seconds.ToString("0.0") + " S)" : "";
            return how + time + (medal ? ". MEDAL TIME: PIP HAS NAILED THE MEDAL TO THE BENCH AND POLISHES IT DAILY."
                                       : ". NO MEDAL, AND PIP DOESN'T CARE: FINISHING WAS THE POINT.");
        }

        static partial void Author_S10(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_s10_medal", "HILL TRIAL MEDAL");
            foreach (var k in new[] { "pip", "s10_start", "s10_top", "s10_shoulder" }) if (!Anchors.Contains(k)) Anchors.Add(k);
            q.offerSay = "IS THAT YOUR CAR?";
            q.offerReply = "SHE'S MINE. TWELVE VOLTS, EIGHTEEN HORSES AND A LOT OF OPINIONS. THE HILL TRIAL IS FROM THOSE FLAGS TO THE TOP AND NOBODY THINKS SHE CAN FINISH IT. " +
                           "I DON'T NEED TO WIN. I NEED HER TO GET TO THE TOP. HELP ME SET HER UP?";
            q.hook = "PIP HARLOW WANTS HER TINY CAR TO FINISH THE LOCAL HILL TRIAL. WINNING IS OPTIONAL.";
            Step(q, "look", "LOOK OVER PIP'S CAR AT THE START FLAGS", "pip").When(Goal.Reach, "pip", 8f)
                .Pays(r => { r.resources.Add((ResourceType.Oil, 4)); r.resources.Add((ResourceType.Fuel, 10)); });
            Step(q, "tyres", "THE TYRES: THE REARS ARE BALD. FIT THE SPARES BY PIP'S BENCH ([E] WITH A WRENCH)", "pip").When(Goal.Event, "s10:tyres").Optional();
            Step(q, "weight", "THE WEIGHT: HER BROTHER LEFT 150 KG OF SANDBAGS IN IT. TAKE THE BALLAST OUT AT THE TUNING BENCH ([T])", "pip").When(Goal.Event, "s10:weight").Optional();
            Step(q, "fluids", "THE FLUIDS: OIL AND PETROL ARE NEARLY GONE. SERVICE IT ([G]) WITH PIP'S CANS", "pip").When(Goal.Event, "s10:fluids").Optional();
            Step(q, "prep", "GET PIP'S CAR FIT FOR THE HILL: TYRES, WEIGHT AND FLUIDS", "pip").When(Goal.Steps, "tyres,weight,fluids", 3f)
                .Pays(r => r.training.Add((Skill.Mechanics, 5f)));
            Step(q, "plan", "THE HILL IS STEEP FOR TWELVE VOLTS: SHORT GEARS AT THE TUNING BENCH ([T]), OR ASK PIP ABOUT THE GENTLER LINE", "pip")
                .When(Goal.Event, "s10:geared", label: S10Gears)
                .Says("pip", "s10_line", S10Shoulder, "THAT'S THE LINE MY GRAN WOULD TAKE, AND SHE NEVER LOST A HILL. KEEP LEFT OR RIGHT OF THE FLAG ON THE SHOULDER, EITHER'S FINE.");
            Step(q, "trial", "RUN THE HILL TRIAL: DRIVE PIP'S CAR FROM THE START FLAGS TO THE TOP FLAG. FINISHING IS WHAT COUNTS", "s10_start").When(Goal.Event, "s10:finished")
                .Pays(r => r.training.Add((Skill.Driving, 6f)));
            Step(q, "medal", "OPTIONAL: THE BEST-TIME MEDAL (RUN IT AGAIN IF YOU LIKE; IT CHANGES NOTHING ELSE)", "s10_start").When(Goal.Event, "s10:medal").Optional()
                .Pays(r => r.items.Add(("story_s10_medal", 1)));
            Step(q, "tell", "PIP IS WAITING AT THE TOP", "s10_top").Says("pip", "s10_tell", "SHE MADE IT. TWELVE VOLTS AND ALL.",
                "SHE MADE IT! I'M PAINTING THAT ON A SIGN. COME BY THE BENCH: I PUT SOMETHING ASIDE FOR YOU, AND I'VE GOT PAINT LEFT OVER.");
            Step(q, "collect", "COLLECT PIP'S THANK-YOU AT HER BENCH: TYRES FOR YOUR OWN CAR", "pip").When(Goal.Reach, "pip", 6f);
            q.reward.scrap = 15; q.reward.training.Add((Skill.Mechanics, 4f)); q.reward.training.Add((Skill.Driving, 4f)); q.reward.flag = "s10_done";
            q.payoff = S10Default;
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S10(List<Member> into)
        {
            into.Add(new Member
            {
                key = "pip", name = "PIP HARLOW", title = "TEENAGE MECHANIC", anchor = "pip", temper = Temper.Joker, female = true,
                outfit = new[] { "overalls", "boots", "beanie", "fingerless" }, tool = "tool_wrench",
                anchorNow = () =>
                {
                    var q = StoryLibrary.Get("S10");
                    var cur = q != null ? Story.Current(q) : null;
                    return cur != null && (cur.id == "trial" || cur.id == "tell") ? "s10_top" : "pip";          // waits at the top with a stopwatch
                },
            });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>S10: a hill near the first town that climbs steadily (7-16 m over 110 m, no dips, no cliffs, no water,
        /// roads allowed): the start flags at its foot, the top flag, a gentler shoulder point off to one side, and Pip's
        /// bench beside the start.</summary>
        static partial void Anchors_S10(WorldGen world, Settlement town)
        {
            if (town == null) return;
            var tc = new Vector3(town.pos.x, 0f, town.pos.y);
            const float L = 110f;
            float H(Vector3 p) => world.Sample(p.x, p.z).height;
            void Put(string k, Vector3 p, float y) { p.y = H(p); at[k] = p; yaw[k] = y; }
            bool Spare(Vector3 p, float d)
            {
                foreach (var kv in at) if (new Vector2(kv.Value.x - p.x, kv.Value.z - p.z).magnitude < d) return false;
                return true;
            }
            bool Clean(Vector3 p)
            {
                var s = world.Sample(p.x, p.z);
                return float.IsNaN(s.water) && s.feature == 0 && world.SettlementAt(p.x, p.z) == null && world.SiteAt(p.x, p.z) == null
                       && world.YardWeight(p.x, p.z) <= 0f && !world.RiverAt(p.x, p.z, out _, out _, out _);
            }
            Vector3 S = Vector3.zero, d = Vector3.forward; float best = float.MinValue; bool done = false;
            for (float rr = town.radius + 80f; rr <= town.radius + 760f && !done; rr += 30f)
                for (int k = 0; k < 24 && !done; k++)
                {
                    float ang = (k * 15f + 30f) * Mathf.Deg2Rad;
                    var s0 = tc + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * rr;
                    if (!Spare(s0, 70f)) continue;
                    float hs = world.BaseHeight(s0.x, s0.z);
                    for (int j = 0; j < 8 && !done; j++)
                    {
                        var dj = Quaternion.Euler(0f, j * 45f, 0f) * Vector3.forward;
                        float prev = hs; bool steady = true;
                        for (int i = 1; i <= 11 && steady; i++)
                        {
                            var q = s0 + dj * (i * 10f);
                            float h = world.BaseHeight(q.x, q.z);
                            if (h - prev < -0.6f || h - prev > 3.5f) steady = false;
                            prev = h;
                        }
                        float rise = prev - hs;
                        if (!steady || rise < 5f) continue;
                        if (!Clean(s0) || !Clean(s0 - dj * 6f) || !Clean(s0 + dj * (L * 0.5f)) || !Clean(s0 + dj * L) || !Spare(s0 + dj * L, 60f)) continue;
                        float score = -Mathf.Abs(rise - 11f);
                        if (score > best) { best = score; S = s0; d = dj; }
                        if (rise >= 7f && rise <= 16f) done = true;
                    }
                }
            if (best == float.MinValue) { d = new Vector3(Mathf.Sin(30f * Mathf.Deg2Rad), 0f, Mathf.Cos(30f * Mathf.Deg2Rad)); S = tc + d * (town.radius + 120f); }
            var T = S + d * L;
            var perp = new Vector3(d.z, 0f, -d.x);
            float hS = H(S), hT = H(T);
            Vector3 M = S + d * (L * 0.5f) + perp * 22f;
            foreach (float off in new[] { 26f, -26f, 18f, -18f, 34f, -34f })
            {
                var m = S + d * (L * 0.5f) + perp * off;
                float hm = H(m);
                if (Clean(m) && hm > hS + 1f && hm < hT - 1f) { M = m; break; }
            }
            float side = Clean(S - d * 4f + perp * 6f) ? 1f : -1f;
            var bench = S - d * 4f + perp * (6f * side);
            float up = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Put("s10_start", S, up);
            Put("s10_top", T, up);
            Put("s10_shoulder", M, up);
            Put("s10_bench", bench, up - 90f * side);                                       // faces the car on the line
            Put("pip", bench + perp * (2.8f * side) - d * 1.5f, up - 90f * side);
            Put("s10_lane1", S + d * (L / 3f), up); Put("s10_lane2", S + d * (L * 2f / 3f), up);
            Put("s10_leg1", (S + M) * 0.5f, up); Put("s10_leg2", (M + T) * 0.5f, up);
            Clearing("s10_start", 10f); Clearing("s10_top", 10f); Clearing("s10_shoulder", 8f); Clearing("s10_bench", 6f); Clearing("pip", 5f);
            Clearing("s10_lane1", 9f); Clearing("s10_lane2", 9f); Clearing("s10_leg1", 8f); Clearing("s10_leg2", 8f);
        }
    }
}
