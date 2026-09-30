using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S07 AN HONEST FISH (storyline §10): Milt Crowe is accused of buying his contest fish because Hal Draper's scale said
    // nothing in this pond grows that big. Read the water (reeds: insects; the mudbank: worms), catch a fish of your own
    // with the rod, weigh it at Hal's table, find out why it reads heavy (look under the pan, or weigh a litre of water),
    // then say so to everyone or settle it quietly. No waiting on rare fish: any fish from this pond will do.
    public static partial class StoryLibrary
    {
        internal const string S07Public = "(TO EVERYONE) HAL'S SCALE ADDS HALF AGAIN. MILT'S FISH WAS HONEST, AND SO WAS MINE.";
        internal const string S07Quiet = "(QUIETLY) TAKE THE SHIM OUT, GIVE MILT HIS CUP, AND NOBODY ELSE HEARS IT FROM ME.";
        internal const string S07Shim = "LOOKED UNDER THE PAN";
        const string S07Default = "THE POND'S CONTEST HAS AN HONEST SCALE AGAIN. MILT CROWE FISHES THERE MOST MORNINGS, SMUGLY.";

        /// <summary>The closing line: how the scale came out, and the catch that proved it (when known).</summary>
        internal static string S07_Payoff(string choice, string caught)
        {
            string lead = caught != null ? "YOUR " + caught + " WEIGHED TRUE IN THE END. " : "";
            if (choice == S07Public) return lead + "HAL OWNED UP IN FRONT OF THE WHOLE POND. MILT HAS HIS CUP BACK, AND THE ANGLERS HAVE A NEW WORD FOR A HEAVY SCALE: A HAL.";
            if (choice == S07Quiet) return lead + "HAL 'FOUND THE FAULT HIMSELF'. MILT HAS HIS CUP BACK, HAL OWES YOU ONE, AND NOBODY ELSE KNOWS WHY.";
            return S07Default;
        }

        static partial void Author_S07(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_s07_ornament", "CARVED FISH ORNAMENT");
            foreach (var k in new[] { "milt", "s07_weigh", "s07_scale", "s07_reeds", "s07_mud" }) if (!Anchors.Contains(k)) Anchors.Add(k);
            q.offerSay = "YOU LOOK LIKE A MAN WITH A GRIEVANCE.";
            q.offerReply = "FORTY YEARS I'VE FISHED THIS POND. SUNDAY I WEIGHED IN A FISH AND HAL DRAPER SAYS NOTHING IN HERE GROWS THAT BIG, SO I MUST HAVE BOUGHT IT. " +
                           "I'VE NEVER BOUGHT A FISH IN MY LIFE. BAIT, YES. CATCH ONE YOURSELF, HONEST, AND LET'S SEE WHAT HIS SCALE SAYS ABOUT IT.";
            q.hook = "MILT CROWE IS ACCUSED OF BUYING HIS CONTEST FISH. HAL DRAPER RUNS THE WEIGH-IN AT THE POND.";
            Step(q, "reeds", "MAYFLIES HATCHING IN THE REEDS: THE FISH HERE RISE TO INSECTS", "s07_reeds").When(Goal.Reach, "s07_reeds", 3.5f).Optional()
                .Pays(r => r.items.Add((FishLibrary.Insects, 4)));
            Step(q, "mud", "SOFT BLACK MUD ON THE BANK: WORMS, AND THE FISH COME IN CLOSE FOR THEM", "s07_mud").When(Goal.Reach, "s07_mud", 3.5f).Optional()
                .Pays(r => r.items.Add((FishLibrary.Worms, 4)));
            Step(q, "read", "READ THE WATER: THE REEDS AND THE MUDBANK SAY WHAT BITES HERE (EITHER WILL DO)", "s07_reeds").When(Goal.Steps, "reeds,mud", 1f)
                .Pays(r => r.training.Add((Skill.Survival, 3f)));
            Step(q, "catch", "CATCH A FISH FROM THIS POND: [LMB] CASTS, CLICK ON A BITE, HOLD TO REEL", "milt").When(Goal.Event, "s07:catch");
            Step(q, "weigh", "TAKE YOUR CATCH TO HAL'S WEIGH-IN", "s07_weigh")
                .Says("hal", "s07_weigh", "WEIGH MY CATCH. CAUGHT HERE THIS MORNING, IN FRONT OF WITNESSES.",
                      "...HM. ON MY SCALE THAT'S HALF AGAIN WHAT IT LOOKS. NOTHING IN THIS POND GROWS LIKE THAT. WHERE'D YOU BUY IT?").Needs("food_fish_raw");
            Step(q, "scale", "SOMETHING'S WRONG WITH THAT SCALE: CROUCH BY THE TABLE AND LOOK UNDER THE PAN, OR WEIGH SOMETHING YOU KNOW", "s07_scale")
                .When(Goal.Event, "s07:shim", label: S07Shim)
                .Says("hal", "s07_canteen", "PUT MY CANTEEN ON IT. A LITRE OF WATER WEIGHS A KILO.",
                      "...A KILO AND A HALF. THAT'S A VERY HEAVY LITRE. ALL RIGHT, DON'T LOOK AT ME LIKE THAT.").Needs(ItemIds.Canteen);
            Step(q, "choice", "HAL'S SCALE READS HALF AGAIN HEAVY. SAY IT OUT LOUD, OR SETTLE IT QUIETLY", "s07_weigh")
                .Says("hal", "s07_public", S07Public, "...ALL RIGHT! ALL RIGHT. I SHIMMED IT. MY NEPHEW WANTED THE CUP. MILT, I'M SORRY. IN FRONT OF EVERYONE, APPARENTLY.")
                .Says("hal", "s07_quiet", S07Quiet, "...THAT'S DECENT OF YOU. MORE DECENT THAN I WAS. THE CUP'S HIS. I'LL SAY I FOUND THE FAULT MYSELF, IF IT'S ALL THE SAME.");
            Step(q, "tell", "TELL MILT HIS FISH WAS HONEST", "milt").Says("milt", "s07_tell", "IT WAS THE SCALE, NOT YOUR FISH.",
                "FORTY YEARS. I KNEW IT. KEEP THE ROD, AND HAVE THIS: MY WIFE CARVED IT. EVERYTHING SHE MADE WAS FISH-SHAPED. SPOONS, MOSTLY.");
            q.reward.scrap = 12; q.reward.items.Add(("story_s07_ornament", 1)); q.reward.items.Add((FishLibrary.Worms, 4)); q.reward.items.Add((FishLibrary.Insects, 4));
            q.reward.training.Add((Skill.Survival, 8f)); q.reward.flag = "s07_done";
            q.payoff = S07Default;
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S07(List<Member> into)
        {
            into.Add(new Member { key = "milt", name = "MILT CROWE", title = "ANGLER", anchor = "milt", temper = Temper.Proud, outfit = new[] { "sweater", "pants", "boots", "sunhat" }, tool = "tool_fishing_rod" });
            into.Add(new Member { key = "hal", name = "HAL DRAPER", title = "CONTEST ORGANISER", anchor = "s07_weigh", temper = Temper.Greedy, outfit = new[] { "jacket", "jeans", "boots", "cowboy" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>S07: the clean lake nearest the first town (a river or the sea if no lake is within reach): Milt's spot on
        /// the dry bank a couple of metres from the water, with water deep enough to cast into; Hal's weigh-in table behind
        /// it; the reeds and the mudbank along the shore either side.</summary>
        static partial void Anchors_S07(WorldGen world, Settlement town)
        {
            if (town == null) return;
            var tp = town.pos;
            void Put(string k, Vector2 p, float y) { at[k] = new Vector3(p.x, world.Sample(p.x, p.y).height, p.y); yaw[k] = y; }
            bool Dry(Vector2 p) { var s = world.Sample(p.x, p.y); return float.IsNaN(s.water) || s.height > s.water + 0.08f; }
            float Depth(Vector2 p) { var s = world.Sample(p.x, p.y); return float.IsNaN(s.water) ? 0f : s.water - s.height; }
            Vector2 Rot(Vector2 v, float deg) { float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
            Vector2 Edge(Vector2 from, Vector2 d) { for (float m = 0f; m < 160f; m += 0.5f) { var q = from + d * m; if (Dry(q)) return q; } return from + d * 160f; }

            // deep water: the nearest clean lake, else any water (river, sea)
            Vector2 wet = Vector2.zero; bool found = false;
            for (float r = town.radius + 40f; r <= 3600f && !found; r += 25f)
            {
                int n = Mathf.Max(16, Mathf.CeilToInt(2f * Mathf.PI * r / 25f));
                float bd = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float a = (i + 0.5f) * 2f * Mathf.PI / n;
                    var q = tp + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    var lk = world.LakeAt(q.x, q.y, out float t);
                    if (lk == null || lk.toxic || t > 1.2f) continue;
                    float d = Vector2.Distance(lk.pos, tp);
                    if (d < bd) { bd = d; wet = lk.pos; found = true; }
                }
            }
            for (float r = town.radius + 40f; r <= 2600f && !found; r += 40f)
                for (int i = 0; i < 24 && !found; i++)
                {
                    float a = i * 15f * Mathf.Deg2Rad;
                    var q = tp + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (Depth(q) > 0.8f) { wet = q; found = true; }
                }

            // Milt's spot: the first bank (turning from the side facing town) that is dry, has castable water in front and room behind
            Vector2 milt = tp + new Vector2(town.radius + 50f, 0f), d0 = Vector2.left;
            if (found)
            {
                var toTown = (tp - wet).sqrMagnitude > 1f ? (tp - wet).normalized : Vector2.up;
                bool ok = false;
                for (int k = 0; k < 36 && !ok; k++)
                {
                    var d = Rot(toTown, (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * 10f);
                    var edge = Edge(wet, d);
                    var m = edge + d * 2.2f;
                    if (!Dry(m) || !Dry(m + d * 9.5f) || Depth(m - d * 4.5f) < 0.35f || world.SettlementAt(m.x, m.y) != null || world.Sample(m.x, m.y).roadDist < 3f) continue;
                    milt = m; d0 = d; ok = true;
                }
                if (!ok) { d0 = toTown; milt = Edge(wet, toTown) + toTown * 2.2f; }
            }
            float face = Mathf.Atan2(-d0.x, -d0.y) * Mathf.Rad2Deg;                          // looking out over the water
            Put("milt", milt, face);
            Put("s07_scale", milt + d0 * 6f, face);
            Put("s07_weigh", milt + d0 * 9.5f, face);
            var dr = Rot(d0, 50f); var dm = Rot(d0, -50f);
            Put("s07_reeds", found ? Edge(wet, dr) + dr * 0.6f : milt + Rot(d0, 90f) * 12f, face);
            Put("s07_mud", found ? Edge(wet, dm) + dm * 1.2f : milt - Rot(d0, 90f) * 12f, face);
            Clearing("milt", 5f); Clearing("s07_weigh", 6f); Clearing("s07_scale", 3f);
        }
    }
}
