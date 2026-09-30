using System.Collections.Generic;
using MadMax.Items;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    // L5 THE LAST RUN: build the V12 at a garage, mount it and drive it, tune it (a tuning bench) or run it in (3 km),
    // then a marked expedition north to the White Wall, the polar ice (the old drivers said "the edge of the world"; the
    // world wraps east-west, so the ice is where the road ends). Where the land gives out before the ice, the run ends at
    // the last cape instead: the destination is the most northerly point reachable over land from the start, found when
    // the world is bound. A pilgrims' fuel cache waits halfway. The engine, its thirst and a horn button are the reward.
    public static partial class StoryLibrary
    {
        static partial void Author_L5(QuestDef q)
        {
            q.build = Build.Playable;
            q.storyOnly = false;
            ItemIds.Register("story_harlan_horn", "HARLAN'S HORN BUTTON");
            Q3Anchors("l5_end", "l5_cache");
            q.offerSay = "THE FOUR PIECES ARE TOGETHER. WHAT NOW?";
            q.offerReply = "NOW HARLAN'S ENGINE GETS WHAT IT NEVER HAD: A FINISH LINE. BUILD IT AT A GARAGE, TUNE IT, AND TAKE IT NORTH TO THE WHITE WALL, THE ICE AT THE TOP OF THE WORLD. " +
                           "NOT OFF THE EDGE: THE OLD DRIVERS SAID EDGE, BUT THE WORLD IS ROUND, AND YOU'D ONLY COME BACK AROUND. THE WALL IS WHERE THE ROAD ENDS.";
            q.hook = "THE LAST RUN: BUILD HARLAN'S V12, TUNE IT, AND DRIVE IT NORTH TO THE WHITE WALL.";

            Step(q, "build", "BUILD THE LAST ENGINE AT A GARAGE (THE FOUR RELICS, 20 IRON, 8 COPPER, 5 OIL)")
                .When(Goal.Craft, MadMax.Game.LastEngine.Part)
                .When(Goal.Event, "l5:built", label: "IT WAS ALREADY BUILT");
            Step(q, "mount", "MOUNT THE V12 IN A CAR ([E] WITH A WRENCH ON THE ENGINE SOCKET) AND DRIVE IT").When(Goal.Event, "l5:running");
            Step(q, "tune", "TUNE IT AT A TUNING BENCH ([T]), OR RUN IT IN: 3 KM BEHIND THE WHEEL")
                .When(Goal.Event, "l5:tuned", label: "TUNED AT A BENCH")
                .When(Goal.Event, "l5:run_in", label: "RUN IN ON THE ROAD")
                .Pays(r => r.training.Add((Skill.Mechanics, 6f)));
            Step(q, "cache", "OPTIONAL: THE PILGRIMS' FUEL CACHE ON THE WAY NORTH", "l5_cache").When(Goal.Reach, "l5_cache", 10f).Optional()
                .Pays(r => { r.resources.Add((ResourceType.Fuel, 40)); r.resources.Add((ResourceType.Oil, 3)); r.items.Add(("food_can", 2)); });
            Step(q, "run", "THE LAST RUN: DRIVE THE V12 NORTH TO THE WHITE WALL, OR TO THE LAST CAPE WHERE THE LAND GIVES OUT (WAYPOINT)", "l5_end")
                .When(Goal.Event, "l5:arrived")
                .Pays(r => r.training.Add((Skill.Driving, 8f)));
            Step(q, "home", "COME HOME AND TELL BROTHER CASK", "cask")
                .Says("cask", "l5_tell", "IT RAN ALL THE WAY. HARLAN'S ENGINE FINISHED ITS RACE.",
                    "THEN IT'S FINISHED, AND IT ISN'T A LEGEND ANY MORE: IT'S A THING THAT HAPPENED. THOSE ARE RARER. THE RADIO WILL HAVE IT BY TONIGHT WHETHER YOU LIKE IT OR NOT. " +
                    "HERE: HARLAN'S HORN BUTTON. IT WAS IN THE BOX WITH THE TAPE.");
            q.reward.items.Add(("story_harlan_horn", 1)); q.reward.training.Add((Skill.Mechanics, 4f)); q.reward.flag = "paint_last_engine";
            q.payoff = "THE LAST RUN IS MADE. THE ENGINE IS YOURS, THIRST AND ALL, AND THE RADIO TELLS IT WRONG IN SEVERAL WAYS.";
        }
    }

    public static partial class StoryAnchors
    {
        /// <summary>The Last Run's destination: a 100 m breadth-first search over land (not sea, above the shore) from the
        /// start yard, bounded by the date-line ocean and the foot of the northern ice wall. The first cell at 81° N or more
        /// is the White Wall; failing that, the most northerly reachable cell is the last cape. The fuel cache sits halfway
        /// along the found path.</summary>
        static partial void Anchors_L5(WorldGen world, Settlement town)
        {
            world.Yard(out var o, out _, out _);
            const float C = 100f;
            float xMax = WorldGen.HalfX - WorldGen.Meridian - 350f, zMin = o.z - 1200f, zMax = WorldGen.ZNorth - 160f;
            int nx = Mathf.FloorToInt(2f * xMax / C), nz = Mathf.FloorToInt((zMax - zMin) / C);
            Vector3 Cell(int i, int j) => new Vector3(-xMax + (i + 0.5f) * C, 0f, zMin + (j + 0.5f) * C);
            bool Land(Vector3 p) => !world.Ocean(p.x, p.z) && world.BaseHeight(p.x, p.z) > WorldGen.SeaLevel + 1f;
            var parent = new int[nx * nz];
            for (int k = 0; k < parent.Length; k++) parent[k] = -2;
            int si = Mathf.Clamp(Mathf.FloorToInt((o.x + xMax) / C), 0, nx - 1), sj = Mathf.Clamp(Mathf.FloorToInt((o.z - zMin) / C), 0, nz - 1);
            int start = sj * nx + si, goal = -1, north = start;
            var queue = new Queue<int>();
            parent[start] = -1; queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue(), ci = c % nx, cj = c / nx;
                var cp = Cell(ci, cj);
                if (WorldGen.Latitude(cp.z) >= 81f) { goal = c; break; }
                if (cj > north / nx) north = c;
                foreach (var (di, dj) in new[] { (0, 1), (1, 0), (-1, 0), (0, -1) })
                {
                    int ni = ci + di, nj = cj + dj;
                    if (ni < 0 || nj < 0 || ni >= nx || nj >= nz) continue;
                    int n = nj * nx + ni;
                    if (parent[n] != -2 || !Land(Cell(ni, nj))) continue;
                    parent[n] = c; queue.Enqueue(n);
                }
            }
            int end = goal >= 0 ? goal : north;
            var path = new List<int>();
            for (int c = end; c >= 0; c = parent[c]) path.Add(c);
            var ep = Cell(end % nx, end / nx);
            Q3Set(world, "l5_end", ep, 0f);
            int mid = path[path.Count / 2];
            var mp = Cell(mid % nx, mid / nx);
            if (Q3Ring(world, mp, 0f, 40f, 0f, 30f, null, out var cache, out var cf)) mp = cache;
            Q3Set(world, "l5_cache", mp, cf);
            Clearing("l5_cache", 8f);
            Clearing("l5_end", 8f);
        }
    }
}
