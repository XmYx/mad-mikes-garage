using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S20 LOW CLOUDS, HIGH HOPES: Oren Beck's three weather loggers sit kite-high on the ridge above town. Fetch them by
    // air (his trike waits on the road), on foot up the steep side, or by driving the long way round; each logger records
    // how you came. His reading of them decides nothing by itself: you advise Ida, who has a medicine run to make.
    public static partial class StoryLibrary
    {
        static partial void Author_S20(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_logger", "WEATHER LOGGER");
            ItemIds.Register("story_flight_booklet", "FLIGHT MAINTENANCE BOOKLET");
            Q3Anchors("oren", "s20_strip", "s20_i1", "s20_i2", "s20_i3");
            q.offerSay = "WHAT ARE YOU WATCHING UP THERE?";
            q.offerReply = "MY WEATHER. THREE LITTLE STATIONS ALONG THE RIDGE, KITE-HIGH, LOGGING PRESSURE AND WIND FOR A MONTH. THE BATTERIES ARE DONE AND SO ARE MY KNEES. " +
                           "FETCH ME THE LOGGERS: FLY MY TRIKE UP IF YOU LIKE, IT'S ON THE ROAD, OR CLIMB, OR DRIVE THE LONG WAY ROUND. THE RIDGE DOESN'T CARE HOW YOU ARRIVE.";
            q.hook = "OREN BECK'S WEATHER LOGGERS SIT ON THE RIDGE. IDA'S MEDICINE RUN WAITS ON WHAT THEY SAY.";

            string[] where = { "THE FIRST LOGGER, ON THE RIDGE", "THE SECOND LOGGER, FURTHER ALONG THE RIDGE", "THE LAST LOGGER, AT THE FAR END OF THE RIDGE" };
            for (int i = 1; i <= 3; i++)
                Step(q, "i" + i, "FETCH " + where[i - 1] + ": FLY OREN'S TRIKE, CLIMB ON FOOT, OR DRIVE THE LONG WAY ROUND", "s20_i" + i)
                    .When(Goal.Event, "s20:i" + i + ":air", label: "BY AIR")
                    .When(Goal.Event, "s20:i" + i + ":foot", label: "ON FOOT")
                    .When(Goal.Event, "s20:i" + i + ":road", label: "BY ROAD")
                    .Pays(r => r.items.Add(("story_logger", 1)));
            Step(q, "read", "BRING THE THREE LOGGERS TO OREN", "oren")
                .Says("oren", "s20_read", "THREE LOGGERS, ALL IN ONE PIECE.",
                    "LET'S SEE... PRESSURE FALLING FOR SIX DAYS, WIND BACKING SOUTH-WEST, CLOUD BASE DOWN ON THE RIDGE. A FRONT, TWO DAYS OUT, MAYBE LESS. " +
                    "IDA'S TAKING THE MEDICINE RUN ACROSS THE FLATS. YOU TELL HER; SHE LISTENS TO PEOPLE WHO AREN'T ME. AND TAKE MY NAVIGATION NOTES: EVERY STRIP I KNOW IS IN THEM.")
                .Needs("story_logger")
                .Pays(r => { r.take.Add(("story_logger", 3)); r.training.Add((Skill.Survival, 3f)); });
            Step(q, "advise", "ADVISE IDA FENWICK ON HER MEDICINE RUN ACROSS THE FLATS", "oren")
                .Says("s20_ida", "s20_go", "GO TONIGHT, AHEAD OF THE FRONT.",
                    "TONIGHT. RIGHT. LAMPS, SPARE TYRE, NO STOPPING TO ADMIRE THE STARS. IF IT'S WRONG I'M BLAMING OREN.")
                .Says("s20_ida", "s20_wait", "WAIT TWO DAYS AND LET IT BLOW THROUGH.",
                    "TWO DAYS. THE CLINIC HAS ENOUGH FOR TWO DAYS. BETTER LATE THAN IN A DITCH WITH THE INSULIN.")
                .Says("s20_ida", "s20_low", "TAKE THE LOW ROAD BY THE RIVER, OUT OF THE WIND.",
                    "LONGER, BUT OUT OF THE DUST. I CAN DO LONGER. THANK YOU. THANK OREN TOO, BUT NOT LOUDLY; HE'LL WANT TO EXPLAIN ISOBARS.");
            q.reward.resources.Add((ResourceType.Fuel, 20)); q.reward.items.Add(("story_flight_booklet", 1));
            q.reward.training.Add((Skill.Mechanics, 6f)); q.reward.flag = "airfield_service";
            q.payoff = "OREN'S RIDGE STATIONS ARE READ AND IDA KNOWS WHAT THE SKY IS DOING. HIS NAVIGATION NOTES PUT EVERY AIRSTRIP HE KNOWS ON YOUR MAP.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S20(List<Member> into)
        {
            into.Add(new Member { key = "oren", name = "OREN BECK", title = "SURVEYOR", anchor = "oren", temper = Temper.Friendly, outfit = new[] { "bomber", "pants", "boots", "goggles" } });
            into.Add(new Member { key = "s20_ida", name = "IDA FENWICK", title = "MEDICINE COURIER", anchor = "oren", temper = Temper.Nervous, female = true, outfit = new[] { "jacket", "jeans", "boots", "scarf" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>Oren at the edge of the first town, his trike on the nearest road, and three stations along the highest
        /// ground within ~1.4 km (open ground, 70-160 m apart).</summary>
        static partial void Anchors_S20(WorldGen world, Settlement town)
        {
            var tc = Q3Town(world, town);
            Q3Edge(world, town, tc, "oren", 125f);
            Clearing("oren", 10f);
            var oren = Get("oren");
            if (Roadside(world, oren, 15f, 160f, 14f, p => Q3Free(p, 12f), out var strip, out var sf)) Q3Set(world, "s20_strip", strip, sf + 90f);
            else Q3Set(world, "s20_strip", oren + new Vector3(8f, 0f, 8f), 0f);
            Clearing("s20_strip", 8f);

            // the ridge: the highest open ground 300-1400 m out
            var cands = new List<(float h, Vector3 p)>();
            for (float x = -1400f; x <= 1400f; x += 50f)
            for (float z = -1400f; z <= 1400f; z += 50f)
            {
                float r = Mathf.Sqrt(x * x + z * z);
                if (r < 300f || r > 1400f) continue;
                var p = tc + new Vector3(x, 0f, z);
                if (!world.Habitable(p.x, p.z)) continue;
                cands.Add((world.BaseHeight(p.x, p.z), p));
            }
            cands.Sort((a, b) => b.h.CompareTo(a.h) != 0 ? b.h.CompareTo(a.h) : a.p.x.CompareTo(b.p.x));
            Vector3 i1 = tc + new Vector3(0f, 0f, 400f); bool got = false;
            foreach (var (_, p) in cands)
                if (Open(world, p) && Q3Free(p, 60f)) { i1 = p; got = true; break; }
            if (!got) Q3Ring(world, tc, 400f, 900f, 40f, 40f, null, out i1, out _);
            Q3Set(world, "s20_i1", i1, 0f);
            var i2 = Q3Crest(world, i1, i1, 70f);
            Q3Set(world, "s20_i2", i2, 0f);
            var i3 = Q3Crest(world, i2, i1, 110f);
            Q3Set(world, "s20_i3", i3, 0f);
            foreach (var k in new[] { "s20_i1", "s20_i2", "s20_i3" }) Clearing(k, 5f);
        }

        /// <summary>The highest open point 70-160 m from <paramref name="from"/> and at least <paramref name="apart"/> m
        /// from <paramref name="other"/> (the next station along a crest).</summary>
        static Vector3 Q3Crest(WorldGen w, Vector3 from, Vector3 other, float apart)
        {
            Vector3 best = from + new Vector3(90f, 0f, 0f); float bh = float.MinValue;
            for (float r = 70f; r <= 160f; r += 15f)
                for (int k = 0; k < 24; k++)
                {
                    float a = k * 15f * Mathf.Deg2Rad;
                    var p = from + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                    if (Q3Dist(p, other) < apart || !w.Habitable(p.x, p.z)) continue;
                    float h = w.BaseHeight(p.x, p.z);
                    if (h <= bh || !Open(w, p) || !Q3Free(p, 45f)) continue;
                    bh = h; best = p;
                }
            return best;
        }
    }
}
