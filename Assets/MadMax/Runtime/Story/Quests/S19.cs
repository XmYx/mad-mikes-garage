using System.Collections.Generic;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // S19 THE BELL BENEATH THE WATER: Ester Pell's village lies under a lake, its chapel bell still in the frame. Survey
    // the ruin (dive, swim or look down from her skiff), plan air and ballast (slow and level, or one big bag that fouls
    // on the way up), rig it yourself in her husband's hard hat or pay Bram, bring it ashore by boat or on the shore line,
    // and choose where it hangs. Nobody drowns for it; a fouled sling is cleared, not fatal.
    public static partial class StoryLibrary
    {
        static partial void Author_S19(QuestDef q)
        {
            q.build = Build.Playable;
            ItemIds.Register("story_lift_bags", "LIFT BAGS AND SLING");
            Q3Anchors("ester", "s19_shore", "s19_ruin", "s19_bell", "s19_skiff", "s19_townbell");
            q.offerSay = "THAT'S A LOT OF ROPE FOR ONE SMALL BOAT.";
            q.offerReply = "IT'S FOR A BELL. MY VILLAGE IS UNDER THIS LAKE: THE CHAPEL, THE SCHOOL, MY MOTHER'S KITCHEN. THE BELL'S STILL IN ITS FRAME DOWN THERE. " +
                           "I WANT IT UP WHERE PEOPLE CAN HEAR IT. I BUILD BOATS; I DON'T DIVE ANY MORE. BRAM DOES, FOR A PRICE AND A LOT OF COMPLAINING.";
            q.hook = "ESTER PELL'S VILLAGE LIES UNDER THE LAKE, CHAPEL BELL AND ALL. SHE WANTS THE BELL BACK UP.";

            Step(q, "loan", "OPTIONAL: BORROW ESTER'S DIVING GEAR (HELMET AND AIR TANK; HER COMPRESSOR FILLS THE TANK, [E])", "ester")
                .Says("ester", "s19_loan", "CAN I BORROW YOUR DIVING GEAR?",
                    "MY HUSBAND'S HARD HAT AND TANK. THE COMPRESSOR'S BY THE SHED AND THE GENERATOR'S RUNNING: [E] ON IT FILLS THE TANK. DON'T DROWN IN IT. HE DIDN'T.")
                .Optional()
                .Pays(r => { r.items.Add(("cloth_dive_helmet", 1)); r.items.Add(("cloth_air_tank", 1)); });
            Step(q, "survey", "SURVEY THE DROWNED CHAPEL: DIVE DOWN TO IT, SWIM OUT OVER IT, OR LOOK DOWN FROM ESTER'S SKIFF", "s19_ruin")
                .When(Goal.Event, "s19:survey_dive", label: "DIVED DOWN TO IT")
                .When(Goal.Event, "s19:survey_swim", label: "SWAM OUT OVER IT")
                .When(Goal.Event, "s19:survey_boat", label: "LOOKED DOWN FROM A BOAT")
                .Pays(r => r.training.Add((Skill.Survival, 3f)));
            Step(q, "plan", "PLAN THE LIFT WITH ESTER: AIR FOR THE LIFT BAGS, BALLAST SO THE BELL COMES UP LEVEL", "ester")
                .Says("ester", "s19_slow", "FOUR SMALL BAGS AND SAND ON THE LIP. SLOW AND LEVEL.",
                    "SLOW IS HOW BELLS LIKE IT. IT TAKES AIR: NINETY SECONDS OF TANK TO FILL THEM ALL. HERE ARE THE BAGS AND THE SLING.")
                .Says("ester", "s19_fast", "ONE BIG BAG. IT'S ONLY A BELL.",
                    "IT'S NEVER ONLY A BELL. HALF THE AIR, AND SHE'LL COME UP LIKE A CORK AND CATCH ON EVERY BEAM ON THE WAY. YOUR BELL, YOUR AIR. HERE.")
                .Pays(r => r.items.Add(("story_lift_bags", 1)));
            Step(q, "rig", "RIG THE BELL: DIVE TO IT WITH THE BAGS AND ENOUGH AIR, OR PAY BRAM TO RIG IT WHILE YOU MIND THE SHORE LINE", "s19_bell")
                .When(Goal.Event, "s19:rigged", label: "RIGGED IT YOURSELF")
                .Says("s19_bram", "s19_hire", "BRAM, RIG IT FOR ME. I'LL MIND THE SHORE LINE.",
                    "TWENTY-FIVE AND I DON'T HAVE TO LISTEN TO ESTER TELL ME HOW. DEAL. WATCH THE LINE: IF IT GOES SLACK, SHOUT.").Q3Price(25);
            Step(q, "lines", "OPTIONAL: PAY BRAM TO WATCH THE LINES ON THE WAY UP", "ester")
                .Says("s19_bram", "s19_lines", "WATCH MY LINES ON THE WAY UP, BRAM.",
                    "AYE. IF SHE FOULS, I'LL HAVE HER CLEAR BEFORE YOU'VE FINISHED SWEARING.").Q3Price(10)
                .Optional();
            Step(q, "raise", "THE BAGS ARE FILLING: WATCH HER COME UP (IF THE SLING FOULS, CLEAR IT: DIVE TO IT OR COME ALONGSIDE IN A BOAT)", "s19_bell")
                .When(Goal.Event, "s19:surfaced")
                .Pays(r => r.training.Add((Skill.Salvaging, 4f)));
            Step(q, "ashore", "BRING HER ASHORE AT ESTER'S YARD: TOW HER IN WITH A BOAT (COME ALONGSIDE), OR HAUL HER IN ON THE SHORE LINE WITH ESTER", "s19_shore")
                .When(Goal.Bring, "s19_bell", 10f, "TOWED IN BY BOAT")
                .Says("ester", "s19_haul", "HAUL HER IN ON THE SHORE LINE.", "ON THREE. ONE, TWO... PULL! AND AGAIN. SHE'S COMING. SHE'S COMING!");
            Step(q, "landed", "LET HER SETTLE ON THE SHINGLE", "s19_shore").When(Goal.Bring, "s19_bell", 3.5f);
            Step(q, "hang", "WHERE SHOULD THE BELL HANG? ESTER ASKS YOU", "ester")
                .Says("ester", "s19_town", "IN THE TOWN, WHERE EVERYONE HEARS HER.",
                    "THE TOWN, THEN. THEY'LL COMPLAIN ABOUT THE NOISE FOR A WEEK AND MISS IT FOREVER AFTER. I'LL HAVE HER HUNG BY TONIGHT.")
                .Says("ester", "s19_here", "HERE, OVER THE WATER, FOR THE VILLAGE UNDER IT.",
                    "HERE. YES. SO THEY KNOW DOWN THERE THAT SOMEBODY REMEMBERS. THANK YOU.");
            Step(q, "hear", "GO AND HEAR HER WHERE SHE HANGS ([E] RINGS THE BELL)").When(Goal.Event, "s19:heard");
            q.reward.scrap = 20; q.reward.items.Add(("use_o2_bottle", 2));
            q.reward.training.Add((Skill.Survival, 6f)); q.reward.training.Add((Skill.Salvaging, 6f)); q.reward.flag = "ester_boat_service";
            q.payoff = "THE BELL IS UP. ESTER SAYS THE LAKE SOUNDS DIFFERENT NOW, WHICH IS NONSENSE, AND SHE'S RIGHT. HER SLIPWAY WILL SERVICE YOUR BOATS AT COST.";
        }
    }

    public static partial class StoryCast
    {
        static partial void Cast_S19(List<Member> into)
        {
            into.Add(new Member { key = "ester", name = "ESTER PELL", title = "BOATWRIGHT", anchor = "ester", temper = Temper.Proud, female = true, outfit = new[] { "sweater", "pants", "boots", "beanie" }, tool = "tool_wrench" });
            into.Add(new Member { key = "s19_bram", name = "BRAM TULLY", title = "DIVER", anchor = "ester", temper = Temper.Gruff, outfit = new[] { "dive_suit", "boots", "beanie" } });
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>The nearest good lake to the first town (deep enough to dive, not toxic), Ester's yard on its shore
        /// facing the town, the ruin near the middle, her skiff between them, and a spot in town for the bell.</summary>
        /// <summary><see cref="Open"/> for the lake margin: <c>Sample.water</c> keeps the lake level past the shoreline, so dry
        /// ground is judged by <see cref="WorldGen.WaterLevel"/>.</summary>
        static bool ShoreOpen(WorldGen w, Vector3 p)
        {
            var s = w.Sample(p.x, p.z);
            return s.roadDist >= 6f && float.IsNaN(w.WaterLevel(p.x, p.z)) && (float.IsNaN(s.water) || s.height > s.water + 0.3f) && s.feature == 0 && w.SettlementAt(p.x, p.z) == null
                   && w.YardWeight(p.x, p.z) <= 0f && w.SiteAt(p.x, p.z) == null && !w.RiverAt(p.x, p.z, out _, out _, out _);
        }

        static partial void Anchors_S19(WorldGen world, Settlement town)
        {
            var tc = Q3Town(world, town);
            Lake best = null; float score = float.MaxValue;
            var seen = new HashSet<Lake>();
            int c0x = Mathf.FloorToInt(tc.x / 200f), c0z = Mathf.FloorToInt(tc.z / 200f);
            for (int dx = -13; dx <= 13; dx++)
            for (int dz = -13; dz <= 13; dz++)
            {
                float cx = (c0x + dx) * 200f, cz = (c0z + dz) * 200f;
                if (new Vector2(cx + 100f - tc.x, cz + 100f - tc.z).magnitude > 2700f) continue;
                for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    var lake = world.LakeAt(cx + 40f + i * 40f, cz + 40f + j * 40f, out _);
                    if (lake == null || !seen.Add(lake)) continue;
                    float sc = Vector2.Distance(lake.pos, new Vector2(tc.x, tc.z)) + (lake.toxic ? 6000f : 0f) + (lake.depth < 2.4f ? 900f : 0f) + (lake.radius < 16f ? 400f : 0f);
                    if (sc < score) { score = sc; best = lake; }
                }
            }
            bool placed = false;
            if (best != null)
            {
                var centre = new Vector3(best.pos.x, 0f, best.pos.y);
                var toTown = tc - centre;
                float a0 = toTown.sqrMagnitude > 1f ? Mathf.Atan2(toTown.x, toTown.z) * Mathf.Rad2Deg : 0f;
                for (int k = 0; k < 16 && !placed; k++)
                {
                    float ang = a0 + (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * 22.5f;
                    var d = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                    float edge = -1f;
                    for (float r = best.radius * 0.4f; r <= best.radius * 2.4f; r += 0.5f)
                    {
                        var p = centre + d * r;
                        if (float.IsNaN(world.WaterLevel(p.x, p.z))) { edge = r; break; }
                    }
                    if (edge < 0f) continue;
                    var shore = centre + d * (edge + 1.5f);
                    var yard = centre + d * (edge + 7f);
                    if (!ShoreOpen(world, yard) || !float.IsNaN(world.WaterLevel(shore.x, shore.z)) || !Q3Free(yard, 30f) || !Q3Level(world, yard, 3f, 1.4f)) continue;
                    Q3Set(world, "ester", yard, ang + 180f);
                    Q3Set(world, "s19_shore", shore, ang + 180f);
                    var ruin = centre + d * (edge * 0.12f);
                    Q3Set(world, "s19_ruin", ruin, ang);
                    Q3Set(world, "s19_bell", ruin + d * 2.4f, ang + 180f);
                    Q3Set(world, "s19_skiff", centre + d * (edge * 0.62f), ang + 90f);
                    placed = true;
                }
            }
            if (!placed)
            {
                // no usable lake near this town: a dry fallback keeps the chapter bound (the quest reads oddly, never breaks)
                Q3Edge(world, town, tc, "ester", 200f);
                var e = Get("ester");
                Q3Set(world, "s19_shore", e + new Vector3(0f, 0f, 2f), 0f);
                Q3Set(world, "s19_ruin", e + new Vector3(0f, 0f, 14f), 0f);
                Q3Set(world, "s19_bell", e + new Vector3(0f, 0f, 12f), 0f);
                Q3Set(world, "s19_skiff", e + new Vector3(4f, 0f, 8f), 90f);
            }
            Clearing("ester", 14f);
            // where the bell may hang in town: open ground at the edge, facing the streets
            Q3Edge(world, town, tc, "s19_townbell", 305f, 35f);
            Clearing("s19_townbell", 5f);
        }
    }
}
