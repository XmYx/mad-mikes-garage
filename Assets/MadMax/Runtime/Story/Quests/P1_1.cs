using System.Collections.Generic;
using MadMax.Items;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // P1.1 A SECOND PAIR OF HANDS (storyline §11, personal chain P1: Nell's unfinished car). Story mode, after B1.
    // Nell's bronze coupe stands behind her stop without rear wheels, radiator, hood or engine: catalogue it with her,
    // then find two wheels and a radiator (her pile, any wreck, a trader, the garage bench) and bolt them on.
    public static partial class StoryLibrary
    {
        static partial void Author_P1_1(QuestDef q)
        {
            q.build = Build.Playable;
            foreach (var a in new[] { "p1_car", "p1_pile", "p1_collector", "p1_ditch", "p1_view" }) if (!Anchors.Contains(a)) Anchors.Add(a);
            ItemIds.Register("story_p1_list", "NELL'S PARTS LIST");
            q.offerSay = "WHAT'S UNDER THE TARP BEHIND YOUR STOP?";
            q.offerReply = "A COUPE. MINE AND TOM'S. WE STARTED HER THE SPRING BEFORE HE DIED AND NEVER FINISHED. MY HANDS WON'T DO THE HEAVY BITS ANY MORE. " +
                           "LEND ME A SECOND PAIR AND WE'LL WRITE DOWN WHAT SHE HAS AND WHAT SHE NEEDS.";
            q.hook = "NELL HAS AN UNFINISHED COUPE BEHIND HER STOP. SHE AND HER LATE PARTNER TOM NEVER GOT IT DONE.";
            Step(q, "look", "LOOK OVER NELL'S COUPE BEHIND HER STOP", "p1_car").When(Goal.Reach, "p1_car", 4.5f);
            Step(q, "catalogue", "CATALOGUE THE COUPE WITH NELL: WHAT'S THERE, WHAT ISN'T", "p1_car")
                .Says("nell", "p1_1_list", "LET'S WRITE HER UP: WHAT'S THERE AND WHAT ISN'T.",
                    "FRONT WHEELS GOOD, BRAKES GOOD, DOORS HUNG STRAIGHT. MISSING: BOTH REAR WHEELS, THE RADIATOR, THE HOOD, THE ENGINE. " +
                    "THERE'S A WHEEL AND THE HOOD IN MY PILE. AND NO, SHE DOESN'T NEED A BIGGER ENGINE. SHE NEEDS ONE SOMEBODY LOOKS AFTER.")
                .Pays(r => { r.items.Add(("story_p1_list", 1)); r.training.Add((Skill.Mechanics, 3f)); });
            Step(q, "wheels", "BOLT TWO REAR WHEELS ON THE COUPE: NELL'S PILE, A WRECK, A TRADER OR A WORKBENCH ([E] WITH THE WRENCH)", "p1_car")
                .When(Goal.Event, "p1_1:wheels");
            Step(q, "radiator", "FIT A RADIATOR (A CAR RADIATOR FROM A WRECK, A TRADER OR A WORKBENCH)", "p1_car")
                .When(Goal.Event, "p1_1:radiator")
                .Pays(r => r.training.Add((Skill.Mechanics, 4f)));
            Step(q, "tell", "TELL NELL SHE'S ON FOUR WHEELS", "nell")
                .Says("nell", "p1_1_done", "SHE'S ON FOUR WHEELS AND SHE'LL HOLD WATER.",
                    "SO SHE WILL. TOM WOULD HAVE PUT A V8 IN HER AND RUINED HER IN A WEEK. THIS IS BETTER: EVERYTHING ON HER WORKS. " +
                    "YOU LEARN MORE FROM A CAR THAT'S LOOKED AFTER THAN ONE THAT'S FAST.");
            q.reward.scrap = 15; q.reward.items.Add(("use_oil_filter", 1)); q.reward.training.Add((Skill.Mechanics, 6f)); q.reward.flag = "p1_catalogued";
            q.payoff = "NELL'S COUPE STANDS ON FOUR WHEELS WITH A RADIATOR. THE ENGINE BAY IS EMPTY: TOM'S STRAIGHT SIX WENT TO A COLLECTOR YEARS AGO.";
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>Personal chains: bind a place (height from the generator).</summary>
        static void PersonalSet(WorldGen w, string k, Vector3 p, float y = 0f) { p.y = w.Sample(p.x, p.z).height; at[k] = p; yaw[k] = y; }

        /// <summary>Personal chains: <paramref name="p"/> is at least <paramref name="d"/> m from every place bound so far.</summary>
        static bool PersonalFree(Vector3 p, float d)
        {
            foreach (var kv in at) if (new Vector2(kv.Value.x - p.x, kv.Value.z - p.z).sqrMagnitude < d * d) return false;
            return true;
        }

        // P1: Nell's coupe and its parts pile beside her stop, Otis Vane's yard at the town's edge, a dead coupe in a
        // ditch out of town, and the lookout: the highest open verge 350-1100 m along the road past Nell's stop.
        static partial void Anchors_P1_1(WorldGen world, Settlement town)
        {
            world.Yard(out var o, out var along, out _);
            var n = at.ContainsKey("nell") ? at["nell"] : o + along * 60f;
            float ny = yaw.ContainsKey("nell") ? yaw["nell"] : 0f;
            var r = Quaternion.Euler(0f, ny, 0f);
            var car = n + r * new Vector3(8f, 0f, -3f);
            foreach (var off in new[] { new Vector3(8f, 0f, -3f), new Vector3(-8.5f, 0f, -3f), new Vector3(8f, 0f, -7f), new Vector3(-8.5f, 0f, -7f), new Vector3(0f, 0f, -9f), new Vector3(11f, 0f, 1f), new Vector3(-11f, 0f, 1f) })
            {
                var p = n + r * off;
                if (Open(world, p) && Level(world, p, 3f, 0.9f)) { car = p; break; }
            }
            PersonalSet(world, "p1_car", car, ny + 90f);
            PersonalSet(world, "p1_pile", car + r * new Vector3(0f, 0f, -3.8f), ny);
            Clearing("p1_car", 7f);

            var tc = town != null ? new Vector3(town.pos.x, 0f, town.pos.y) : o;
            float trad = town != null ? town.radius : 40f;
            bool placed = false;
            for (float rr = trad + 12f; rr <= trad + 120f && !placed; rr += 8f)
                for (int k = 0; k < 24 && !placed; k++)
                {
                    float ang = (k * 15f + 11f) * Mathf.Deg2Rad;
                    var p = tc + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * rr;
                    if (!Open(world, p) || !Level(world, p, 5f, 1f) || !PersonalFree(p, 35f)) continue;
                    var toC = tc - p;
                    PersonalSet(world, "p1_collector", p, Mathf.Atan2(toC.x, toC.z) * Mathf.Rad2Deg);
                    placed = true;
                }
            if (!placed) PersonalSet(world, "p1_collector", tc + new Vector3(trad + 24f, 0f, 6f), -90f);
            Clearing("p1_collector", 9f);

            if (Roadside(world, tc, trad + 150f, trad + 650f, 14f, p => Clear(town, p, 100f) && PersonalFree(p, 60f), out var ditch, out var dy)) PersonalSet(world, "p1_ditch", ditch, dy + 90f);
            else PersonalSet(world, "p1_ditch", tc + new Vector3(0f, 0f, trad + 220f), 0f);
            Clearing("p1_ditch", 8f);

            if (PersonalLookout(world, n, out var view, out var vy)) PersonalSet(world, "p1_view", view, vy);
            else if (Roadside(world, n, 350f, 1400f, 14f, p => PersonalFree(p, 40f), out view, out vy)) PersonalSet(world, "p1_view", view, vy + 180f);
            else PersonalSet(world, "p1_view", n + r * new Vector3(0f, 0f, -400f), ny + 180f);
            Clearing("p1_view", 8f);
        }

        /// <summary>The lookout: along the road nearest <paramref name="from"/>, 350-1100 m either way, the highest open
        /// verge (deterministic); faces away from the road, at the view.</summary>
        static bool PersonalLookout(WorldGen w, Vector3 from, out Vector3 spot, out float face)
        {
            spot = Vector3.zero; face = 0f;
            int road = -1, idx = 0; float bd = float.MaxValue;
            for (int ri = 0; ri < w.roads.roads.Count; ri++)
            {
                var pts = w.roads.roads[ri].points;
                for (int i = 0; i < pts.Count; i++)
                {
                    float d = new Vector2(pts[i].x - from.x, pts[i].z - from.z).sqrMagnitude;
                    if (d < bd) { bd = d; road = ri; idx = i; }
                }
            }
            if (road < 0 || bd > 80f * 80f) return false;
            var rp = w.roads.roads[road].points;
            float best = float.MinValue; bool ok = false;
            foreach (int dir in new[] { 1, -1 })
            {
                float run = 0f, next = 350f;
                for (int i = idx; i + dir >= 0 && i + dir < rp.Count; i += dir)
                {
                    var a = rp[i]; var b = rp[i + dir];
                    run += new Vector2(b.x - a.x, b.z - a.z).magnitude;
                    if (run > 1100f) break;
                    if (run < next) continue;
                    next = run + 24f;                                                              // a candidate every ~24 m
                    var seg = new Vector3(b.x - a.x, 0f, b.z - a.z);
                    if (seg.sqrMagnitude < 0.01f) continue;
                    seg.Normalize();
                    var across = new Vector3(seg.z, 0f, -seg.x);
                    foreach (float off in new[] { 11f, -11f })
                    {
                        var p = new Vector3(b.x, 0f, b.z) + across * off;
                        float hgt = w.Sample(p.x, p.z).height;
                        if (hgt <= best || !Open(w, p) || !Level(w, p, 3f, 1f) || !PersonalFree(p, 40f)) continue;
                        best = hgt; spot = p; ok = true;
                        var f = p - new Vector3(b.x, 0f, b.z);
                        face = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                    }
                }
            }
            return ok;
        }
    }
}
