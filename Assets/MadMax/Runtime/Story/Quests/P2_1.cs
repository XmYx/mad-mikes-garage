using System.Collections.Generic;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // P2.1 THE MARKET NOBODY WANTED (storyline §11, personal chain P2; sandbox too): Oda Fenn and Barnaby Reed each want a
    // market in the other's hamlet. Hear Barnaby, walk three fields (Oda's, Barnaby's, the one between) for water, road
    // access and flat ground, and tell Oda what the ground says.
    public static partial class StoryLibrary
    {
        static partial void Author_P2_1(QuestDef q)
        {
            q.build = Build.Playable;
            foreach (var a in new[] { "p2_oda", "p2_barnaby", "p2_a", "p2_b", "p2_mid", "p2_market", "p2_tally" }) if (!Anchors.Contains(a)) Anchors.Add(a);
            q.offerSay = "I HEARD SOMEONE WANTS A MARKET OUT HERE.";
            q.offerReply = "EVERYONE WANTS A MARKET. NOBODY WANTS IT IN THEIR FIELD. BARNABY REED SAYS HIS HAMLET'S TOO STEEP AND MINE'S TOO CLOSE TO THE ROAD; " +
                           "I SAY THE REVERSE. WALK THE GROUND FOR US: WATER, ACCESS, FLAT. YOU'VE GOT NO COUSINS IN EITHER PLACE.";
            q.hook = "ODA FENN AND BARNABY REED BOTH WANT A MARKET, EACH IN THE OTHER'S HAMLET. THEY NEED A NEUTRAL SURVEY.";
            Step(q, "barnaby", "HEAR BARNABY REED'S SIDE IN THE NEXT HAMLET", "p2_barnaby")
                .Says("barnaby", "p2_1_side", "ODA SAYS THE MARKET SHOULD BE HERE.",
                    "ODA SAYS A LOT. MY FIELD FLOODS EVERY SPRING AND THE ROAD IN IS A GOAT TRACK. HERS IS FLAT AS A PLATE. DON'T TELL HER I SAID FLAT.");
            Step(q, "field_a", "SURVEY ODA'S FIELD (THE STAKES)", "p2_a").When(Goal.Reach, "p2_a", 6f).Optional();
            Step(q, "field_b", "SURVEY BARNABY'S FIELD (THE STAKES)", "p2_b").When(Goal.Reach, "p2_b", 6f).Optional();
            Step(q, "field_mid", "SURVEY THE FIELD BETWEEN THE HAMLETS (THE STAKES)", "p2_mid").When(Goal.Reach, "p2_mid", 6f).Optional();
            Step(q, "survey", "WALK ALL THREE FIELDS: ODA'S, BARNABY'S AND THE ONE BETWEEN (WATER, ROAD, FLAT)", "p2_mid").When(Goal.Steps, "field_a,field_b,field_mid", 3f)
                .Pays(r => r.training.Add((Skill.Construction, 4f)));
            Step(q, "report", "TELL ODA WHAT THE GROUND SAYS", "p2_oda")
                .Says("hamlets", "p2_1_report", "I WALKED ALL THREE FIELDS. HERE'S WHAT THE GROUND SAYS.",
                    "...SO NEITHER OF US WAS RIGHT. THAT'S ALMOST WORSE. LET ME THINK ABOUT WHERE IT GOES.");
            q.reward.scrap = 15; q.reward.training.Add((Skill.Speech, 3f));
            q.payoff = "THE SURVEY IS IN: THREE FIELDS, TWO OPINIONS, ONE REPORT.";
        }

        /// <summary>P2.2's choice: the market takes turns, starting in Oda's field (else it sits in the field between).</summary>
        public static bool P2Rotates(string route) => route != null && route.StartsWith("TAKE TURNS");
    }

    public static partial class StoryCast
    {
        static partial void Cast_P2_1(List<Member> into)
        {
            into.Add(new Member { key = "hamlets", name = "ODA FENN", title = "HAMLET ELDER", anchor = "p2_oda", temper = Temper.Gruff, female = true,
                outfit = new[] { "sweater", "pants", "boots", "sunhat" }, tool = "tool_hoe", anchorNow = () => P2Where("p2_oda", "hamlets", "food") });
            into.Add(new Member { key = "barnaby", name = "BARNABY REED", title = "GOATHERD, HAMLET ELDER", anchor = "p2_barnaby", temper = Temper.Joker,
                outfit = new[] { "overalls", "boots", "cowboy" }, present = () => StoryAnchors.PersonalMarketSync() && Story.StateOf("P2.1") != Story.State.Locked && Story.Runs(StoryLibrary.Get("P2.1")),
                anchorNow = () => P2Where("p2_barnaby", "barnaby", "build") });
        }

        /// <summary>Where a hamlet elder stands: at the market field on opening day and evening (P2.3, P2.4) and, once the
        /// market keeps going (flag p2_market), every third day from 8:00 to 19:00, when they sell there; else at home.</summary>
        static string P2Where(string home, string key, string sells)
        {
            StoryAnchors.PersonalMarketSync();
            bool opening = Story.StateOf("P2.3") == Story.State.Active || Story.StateOf("P2.4") == Story.State.Active;
            bool marketDay = Story.Flag("p2_market") && DayNight.Day % 3 == 0 && DayNight.Hours >= 8f && DayNight.Hours < 19f;
            var g = MadMax.Game.WastelandGame.Instance;
            var p = g && g.World != null ? Profile(key, g.World.seed) : null;
            if (p != null) p.kind = marketDay ? sells : null;                                    // a stall of their own on market days
            return opening || marketDay ? "p2_market" : home;
        }
    }

    public static partial class StoryAnchors
    {
        // P2: two neighbouring settlements (villages first) other than the first town, nearest the start: Oda's hamlet A
        // and Barnaby's B; each elder at the edge facing the other, a field beyond each, and the field between.
        static partial void Anchors_P2_1(WorldGen world, Settlement town)
        {
            world.Yard(out var o, out _, out _);
            Settlement A = null, B = null; float best = float.MaxValue;
            var home = town != null ? town.pos : new Vector2(o.x, o.z);
            for (int pass = 0; pass < 2 && A == null; pass++)
                foreach (var a in world.settlements)
                    foreach (var b in world.settlements)
                    {
                        if (a == town || b == town || a.index >= b.index) continue;
                        if (pass == 0 && (a.kind != Biome.Village || b.kind != Biome.Village)) continue;
                        float gap = Vector2.Distance(a.pos, b.pos) - a.radius - b.radius;
                        if (gap < 150f || gap > 1600f) continue;
                        float score = Vector2.Distance(home, a.pos) + Vector2.Distance(home, b.pos) + gap;
                        if (score < best) { best = score; A = a; B = b; }
                    }
            if (A == null)
            {
                A = town;
                foreach (var s in world.settlements) if (s != town && (B == null || Vector2.Distance(home, s.pos) < Vector2.Distance(home, B.pos))) B = s;
                if (A == null) { A = B; B = null; }
            }
            if (A != null && B != null && Vector2.Distance(home, B.pos) < Vector2.Distance(home, A.pos)) { var t = A; A = B; B = t; }
            var ca = A != null ? A.pos : home; float ra = A != null ? A.radius : 30f;
            var cb = B != null ? B.pos : ca + new Vector2(ra * 2f + 500f, 0f); float rb = B != null ? B.radius : ra;
            var ab = (cb - ca).sqrMagnitude > 1f ? (cb - ca).normalized : Vector2.right;

            var oda = PersonalEdge(world, ca, ra, ab);
            PersonalSet(world, "p2_oda", oda, Mathf.Atan2(-ab.x, -ab.y) * Mathf.Rad2Deg);
            var barn = PersonalEdge(world, cb, rb, -ab);
            PersonalSet(world, "p2_barnaby", barn, Mathf.Atan2(ab.x, ab.y) * Mathf.Rad2Deg);
            PersonalSet(world, "p2_a", PersonalField(world, oda, ab, 10f), Mathf.Atan2(ab.x, ab.y) * Mathf.Rad2Deg);
            PersonalSet(world, "p2_b", PersonalField(world, barn, -ab, 10f), Mathf.Atan2(-ab.x, -ab.y) * Mathf.Rad2Deg);
            var mid2 = B != null && A != B ? (ca + cb) * 0.5f : ca + ab * (ra + 180f);
            PersonalSet(world, "p2_mid", PersonalMid(world, new Vector3(mid2.x, 0f, mid2.y)), Mathf.Atan2(ab.x, ab.y) * Mathf.Rad2Deg);
            foreach (var (k, r) in new[] { ("p2_oda", 6f), ("p2_barnaby", 6f), ("p2_a", 14f), ("p2_b", 14f), ("p2_mid", 16f) }) Clearing(k, r);
            at.Remove("p2_market");
            PersonalMarketSync();
        }

        /// <summary>Open level ground at a settlement's edge, as near the direction <paramref name="dir"/> as possible.</summary>
        static Vector3 PersonalEdge(WorldGen w, Vector2 c, float radius, Vector2 dir)
        {
            float a0 = Mathf.Atan2(dir.y, dir.x);
            for (float rr = radius + 12f; rr <= radius + 150f; rr += 8f)
                for (int k = 0; k < 15; k++)
                {
                    float ang = a0 + ((k + 1) / 2) * (k % 2 == 0 ? 1f : -1f) * 12f * Mathf.Deg2Rad;
                    var p = new Vector3(c.x + Mathf.Cos(ang) * rr, 0f, c.y + Mathf.Sin(ang) * rr);
                    if (Open(w, p) && Level(w, p, 4f, 1f) && PersonalFree(p, 30f)) return p;
                }
            return new Vector3(c.x + dir.x * (radius + 20f), 0f, c.y + dir.y * (radius + 20f));
        }

        /// <summary>A level field 18-70 m beyond <paramref name="from"/> towards <paramref name="dir"/>.</summary>
        static Vector3 PersonalField(WorldGen w, Vector3 from, Vector2 dir, float half)
        {
            float a0 = Mathf.Atan2(dir.y, dir.x);
            for (float rr = 18f; rr <= 70f; rr += 6f)
                for (int k = 0; k < 9; k++)
                {
                    float ang = a0 + ((k + 1) / 2) * (k % 2 == 0 ? 1f : -1f) * 20f * Mathf.Deg2Rad;
                    var p = from + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rr;
                    if (Open(w, p) && Level(w, p, half, 1.4f) && PersonalFree(p, 16f)) return p;
                }
            return from + new Vector3(dir.x, 0f, dir.y) * 24f;
        }

        /// <summary>The field between: open level ground spiralling out from the midpoint, near a road first.</summary>
        static Vector3 PersonalMid(WorldGen w, Vector3 c)
        {
            for (int pass = 0; pass < 2; pass++)
                for (int k = 0; k < 260; k++)
                {
                    float a = k * 2.39996f, r = 6f + k * 1.6f;
                    var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (!Open(w, p) || (pass == 0 && w.Sample(p.x, p.z).roadDist > 60f) || !Level(w, p, 10f, 1.5f) || !PersonalFree(p, 40f)) continue;
                    return p;
                }
            return c;
        }

        /// <summary>P2's market field follows P2.2's choice (turns start in Oda's field; else the field between), with
        /// the gate tally board beside it. Cheap to call often; returns true for use in predicates.</summary>
        public static bool PersonalMarketSync()
        {
            string src = StoryLibrary.P2Rotates(Story.Route("P2.2", "choose")) ? "p2_a" : "p2_mid";
            if (!at.TryGetValue(src, out var p)) return true;
            if (at.TryGetValue("p2_market", out var cur) && (cur - p).sqrMagnitude < 0.01f) return true;
            float y = yaw.TryGetValue(src, out var yy) ? yy : 0f;
            at["p2_market"] = p; yaw["p2_market"] = y;
            at["p2_tally"] = p + Quaternion.Euler(0f, y, 0f) * new Vector3(6f, 0f, -5f); yaw["p2_tally"] = y;
            return true;
        }
    }
}
