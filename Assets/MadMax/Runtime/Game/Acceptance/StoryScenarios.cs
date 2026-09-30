using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Storyline stage N0/N1 acceptance: the campaign graph contract (pure data), the first hour of a STORY
    /// world played through its real verbs (A1 then B1), and a sandbox world that runs no campaign.</summary>
    public static class StoryScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new StoryContract();
            yield return new StoryAnchorSeeds();
            yield return new StoryFirstHour();
            yield return new StorySandbox();
        }
    }

    /// <summary>N0: stable unique ids, every prerequisite exists, no cycles, every needed system is registered, the
    /// finale is reachable on paper, and Playable quests are complete (steps, anchors, talk lines, ready systems).</summary>
    class StoryContract : Scenario
    {
        public override string Id => "story.contract";
        public override bool NeedsWorld => false;

        public override IEnumerator Run(ScenarioContext c)
        {
            var all = StoryLibrary.All;
            var ids = new HashSet<string>();
            foreach (var q in all) c.Check(ids.Add(q.id), "unique id " + q.id);
            foreach (var q in all) foreach (var a in q.after) c.Check(ids.Contains(a), $"{q.id} waits for {a}, which exists");
            foreach (var q in all) foreach (var n in q.needs) c.Check(Systems.Known(n), $"{q.id} needs '{n}', a registered system");
            // cycles: depth-first over prerequisites
            var state = new Dictionary<string, int>();
            bool Cyclic(string id)
            {
                if (state.TryGetValue(id, out int s)) return s == 1;
                state[id] = 1;
                var q = StoryLibrary.Get(id);
                if (q != null) foreach (var a in q.after) if (Cyclic(a)) return true;
                state[id] = 2;
                return false;
            }
            foreach (var q in all) c.Check(!Cyclic(q.id), "no prerequisite cycle through " + q.id);
            // the finale's chain reaches the opening
            var chain = new HashSet<string>();
            void Walk(string id) { if (!chain.Add(id)) return; var q = StoryLibrary.Get(id); if (q != null) foreach (var a in q.after) Walk(a); }
            Walk("F1");
            c.Check(chain.Contains("A1") && chain.Contains("B3") && chain.Contains("C3"), "the finale waits on the opening, a lit home (B3) and a supply agreement (C3)");
            foreach (var arc in new[] { Arc.A, Arc.B, Arc.C }) c.Check(all.Any(q => q.arc == arc), "arc " + arc + " has quests");
            // playable quests are complete
            foreach (var q in all.Where(q => q.build == Build.Playable))
            {
                c.Check(q.steps.Count > 0, q.id + " has steps");
                foreach (var n in q.needs) c.Check(Systems.Ready(n), $"{q.id}: system '{n}' is ready");
                foreach (var s in q.steps)
                {
                    c.Check(s.any.Count > 0, $"{q.id}:{s.id} has a way to finish");
                    if (s.waypoint != null) c.Check(StoryLibrary.Anchors.Contains(s.waypoint), $"{q.id}:{s.id} waypoint '{s.waypoint}' is an anchor");
                    foreach (var cond in s.any)
                    {
                        if (cond.goal == Goal.Reach) c.Check(StoryLibrary.Anchors.Contains(cond.key), $"{q.id}:{s.id} reaches anchor '{cond.key}'");
                        if (cond.goal == Goal.Talk) c.Check(cond.say != null && cond.reply != null && StoryCast.Find(cond.key) != null, $"{q.id}:{s.id} talk to '{cond.key}' has both lines");
                        if (cond.goal == Goal.Build) c.Check(FurnitureLibrary.Get(cond.key) != null, $"{q.id}:{s.id} builds '{cond.key}', a real piece");
                    }
                    if (s.reward != null) foreach (var (item, _) in s.reward.items) c.Check(ItemIds.Name(item) != item, $"{q.id}:{s.id} reward '{item}' has a name");
                }
                if (q.giver != null && q.offerSay == null && q.id != "A1") c.Check(false, q.id + " has a giver but no offer line");
            }
            int blocked = all.Count(q => q.needs.Any(n => !Systems.Ready(n)));
            c.Metric("quests", all.Count, "");
            c.Metric("playable", all.Count(q => q.build == Build.Playable), "");
            c.Metric("waiting_on_systems", blocked, "");
            c.Metric("missing_systems", Systems.All.Count(e => !e.ready), "");
            c.Note("missing: " + string.Join(", ", Systems.All.Where(e => !e.ready).Select(e => e.key + " (" + e.stage + ")")));
            yield break;
        }
    }

    /// <summary>Anchor binding on a spread of seeds (storyline §13: a seed that can't host a chapter must be caught, not
    /// silently skipped): every anchor placed, the wreck out of town, Nell and the car within reach.</summary>
    class StoryAnchorSeeds : Scenario
    {
        public override string Id => "story.anchor_seeds";
        public override bool NeedsWorld => false;
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            int ok = 0;
            foreach (int seed in new[] { 7, 11, 1234, 4242, 98765, 31337, 555, 2026 })
            {
                StoryAnchors.Bind(new MadMax.World.WorldGen(seed));
                var bad = StoryAnchors.Validate();
                if (c.Check(bad.Count == 0, "seed " + seed + ": " + (bad.Count == 0 ? "all anchors" : string.Join("; ", bad)))) ok++;
                yield return null;
            }
            c.Metric("seeds_bound", ok, "");
            if (WastelandGame.Instance && WastelandGame.Instance.World != null) StoryAnchors.Bind(WastelandGame.Instance.World);
        }
    }

    /// <summary>N1: a fresh STORY world played through A1 (satchel, Nell, rain collector, stranded car) and B1 (the
    /// garage at the bend) with the game's own verbs; teleports are disclosed fixtures, nothing is granted.</summary>
    class StoryFirstHour : Scenario
    {
        public override string Id => "story.first_hour";
        public override float Timeout => 240f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, story = true };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            // ---- the start
            var bad = StoryAnchors.Validate();
            if (!c.Check(bad.Count == 0, "every story anchor is placed" + (bad.Count > 0 ? ": " + string.Join("; ", bad) : ""))) yield break;
            c.Metric("wreck_to_nell", Vector3.Distance(StoryAnchors.Get("wreck"), StoryAnchors.Get("nell")), "m");
            c.Metric("wreck_to_town", Vector3.Distance(StoryAnchors.Get("wreck"), StoryAnchors.Get("town1")), "m");
            c.Check(MadMax.Story.Story.Campaign && MadMax.Story.Story.StateOf("A1") == MadMax.Story.Story.State.Active, "the campaign runs and A1 is active");
            c.Check(g.StarterLine == null, "FIRST STEPS is off in a story world");
            c.Check(!g.Current, "the player starts on foot");
            yield return new WaitForSeconds(2f);
            c.Check(g.Stats.injuries.Count == 0, "wakes bruised but unhurt: no wounds from the scene (" + g.Stats.injuries.Count + ")");
            c.Check(Vector3.Distance(g.Player.transform.position, StoryAnchors.Get("wreck")) < 12f, "the player wakes by the wreck");
            c.Check(g.Inventory.GetItem(ItemIds.Wrench) == 0 && g.Inventory.GetItem("food_can") == 0 && g.Inventory.Get(ResourceType.Scrap) == 0, "no sandbox kit: no wrench, food or scrap yet");
            c.Check(g.Fleet.Count == 1, $"one vehicle to your name ({g.Fleet.Count})");
            var car = g.Fleet.Count > 0 ? g.Fleet[0] : null;
            var sys = car ? car.GetComponent<VehicleSystems>() : null;
            if (!c.Check(sys && sys.disconnected && sys.fuel < 0.5f, "the stranded car has a loose battery lead and an empty tank")) yield break;
            sys.Crank();
            c.Check(!sys.Cranking, "a loose lead: the starter doesn't even turn");
            c.Screenshot("wake");
            yield return null;

            // ---- A1: the satchel
            yield return Walk(c, "satchel", 1.5f);
            yield return Until(() => MadMax.Story.Story.StepDone("A1", "things"), 4f);
            c.Check(MadMax.Story.Story.StepDone("A1", "things") && g.Inventory.GetItem(ItemIds.Wrench) > 0, "your things: a wrench, a knife, a canteen, a tin, a bandage");
            // ---- Nell's stop
            yield return Walk(c, "nell", 4f);
            yield return Until(() => g.CastBody("nell") != null, 5f);
            var nell = g.CastBody("nell");
            if (!c.Check(nell, "Nell Mercer is at her stop")) yield break;
            c.Check(nell.Profile.Name == "NELL MERCER", "her name is her own: " + nell.Profile.Name);
            yield return Until(() => MadMax.Story.Story.StepDone("A1", "stop"), 4f);
            c.Check(MadMax.Story.Story.StepDone("A1", "stop"), "reached the roadside stop");
            c.Check(Talk(g, nell, "I CRAWLED OUT"), "tell Nell where you came from");
            yield return Until(() => MadMax.Story.Story.StepDone("A1", "nell"), 4f);
            c.Check(g.Inventory.GetItem(ItemIds.ClawHammer) > 0, "Nell lends a claw hammer and patch tin");
            // ---- the rain collector (build mode's repair, paid from the patch tin)
            var rc = Placeable.All.FirstOrDefault(p => p && p.id == "rain_collector" && g.IsStoryProp(p));
            if (!c.Check(rc && rc.hits < rc.MaxHits, "Nell's rain collector is cracked")) yield break;
            g.Build.Repair(rc);
            yield return Until(() => MadMax.Story.Story.StepDone("A1", "collector"), 4f);
            c.Check(rc.hits == rc.MaxHits && MadMax.Story.Story.StepDone("A1", "collector"), "the rain collector is patched");
            c.Check(g.Inventory.Get(ResourceType.Fuel) >= 8, "Nell pays in fuel (" + g.Inventory.Get(ResourceType.Fuel) + " L)");
            // ---- the stranded car
            yield return Walk(c, "car", 3f);
            sys.Reconnect();
            c.Fixture("battery lead reconnected (the service key's wrench action)");
            int moved = sys.Service(g.Inventory);
            c.Check(sys.fuel >= 5f, $"fuel poured in from the can ({moved} L)");
            // onto the road, pointing along it (a player steers there; the test doesn't steer)
            if (NearestRoad(g.World, car.transform.position, out var rp, out var rd))
            {
                if (Vector3.Dot(rd, StoryAnchors.Get("wreck") - rp) > 0f) rd = -rd;
                yield return TestWorld.Place(c, car, rp, rd, 1f);
            }
            g.Enter(car); yield return new WaitForSeconds(0.4f);
            yield return TestWorld.StartEngine(c, car);
            if (c.Failed) yield break;
            car.handbrake = false;
            var p0 = car.transform.position;
            float t0 = Time.time;
            while (MadMax.Story.Story.StateOf("A1") != MadMax.Story.Story.State.Done && Time.time - t0 < 40f)
            {
                FollowRoad(g.World, car, 8.5f);
                yield return null;
            }
            car.throttleInput = 0f; car.steerInput = 0f; car.brakeInput = 1f;
            var cur = MadMax.Story.Story.Current(StoryLibrary.Get("A1"));
            c.Note($"car moved {Vector3.Distance(p0, car.transform.position):0} m, counted {MadMax.Story.Story.Driven("A1", "move"):0} m, current step {(cur != null ? cur.id : "-")}, in car {(g.Current == car)}, {TestWorld.State(car)}");
            c.Check(MadMax.Story.Story.StateOf("A1") == MadMax.Story.Story.State.Done, "A1 SOMEONE LEFT THE RADIO ON is done");
            c.Check(MadMax.Story.Story.Route("A1", "move") == "DRIVE THE CAR 150 M", "route taken: " + MadMax.Story.Story.Route("A1", "move"));
            // ---- the ledger: reloading the story state pays nothing twice
            int scrap = g.Inventory.Get(ResourceType.Scrap);
            MadMax.Story.Story.Load(MadMax.Story.Story.Save());
            yield return new WaitForSeconds(2.2f);
            c.Check(g.Inventory.Get(ResourceType.Scrap) == scrap && MadMax.Story.Story.StateOf("A1") == MadMax.Story.Story.State.Done, "save/load of the story pays no reward twice");
            yield return new WaitForSeconds(1f);
            car.brakeInput = 0f; car.handbrake = true;
            g.Exit(); yield return new WaitForSeconds(0.5f);

            // ---- B1: Nell offers the garage
            c.Check(MadMax.Story.Story.StateOf("B1") == MadMax.Story.Story.State.Open, "B1 opens after A1");
            yield return Walk(c, "nell", 3f);
            yield return Until(() => g.CastBody("nell") != null, 5f);
            c.Check(Talk(g, g.CastBody("nell"), "ANYWHERE AROUND HERE"), "ask Nell about somewhere to work");
            yield return Until(() => MadMax.Story.Story.Flag("garage_built"), 4f);
            c.Check(MadMax.Story.Story.StateOf("B1") == MadMax.Story.Story.State.Active && MadMax.Story.Story.Flag("garage_built"), "B1 is active and the garage stands at the bend");
            yield return Walk(c, "garage", 6f);
            yield return Until(() => MadMax.Story.Story.StepDone("B1", "look"), 4f);
            var bar = Placeable.All.FirstOrDefault(p => p && p.id == "barricade" && g.IsStoryProp(p));
            if (!c.Check(bar, "a barricade blocks the garage doorway")) yield break;
            c.Screenshot("garage");
            yield return null;
            for (int i = 0; i < 12 && bar; i++) { bar.ApplyHit(bar.transform.position + Vector3.up * 0.5f, Vector3.forward, 1f, 0.4f, g.Player.gameObject); yield return null; }
            yield return Until(() => MadMax.Story.Story.StepDone("B1", "clear"), 4f);
            c.Check(MadMax.Story.Story.StepDone("B1", "clear"), "the doorway is clear");
            var a = StoryAnchors.Get("garage"); var r = Quaternion.Euler(0f, StoryAnchors.Yaw("garage"), 0f);
            var bench = FurnitureLibrary.Spawn("workbench", g.Build.Structures, a + r * new Vector3(0f, 0f, -1.8f), r, g.propMaterial);
            c.Fixture("a workbench placed inside (as the build tool would)");
            yield return Until(() => MadMax.Story.Story.StepDone("B1", "bench"), 4f);
            c.Check(bench && MadMax.Story.Story.StepDone("B1", "bench"), "one work area restored");
            FurnitureLibrary.Spawn("claim_flag", g.Build.Structures, a + r * new Vector3(-5.5f, 0f, 5f), r, g.propMaterial);
            c.Fixture("a claim flag planted beside the garage");
            yield return Until(() => MadMax.Story.Story.StateOf("B1") == MadMax.Story.Story.State.Done, 4f);
            c.Check(MadMax.Story.Story.StateOf("B1") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("garage_claimed"), "B1 THE SIGN STILL STANDS is done: the garage is yours");
            c.Screenshot("garage_claimed");
            yield return null;
        }

        /// <summary>A plain road-following driver for tests: steer at a point 10 m ahead on the nearest road, hold a speed.</summary>
        static void FollowRoad(MadMax.World.WorldGen w, VehicleDriver car, float speed)
        {
            var fwd = car.transform.forward; fwd.y = 0f; fwd.Normalize();
            if (!NearestRoad(w, car.transform.position + fwd * 10f, out var target, out _)) { car.throttleInput = 0.5f; car.steerInput = 0f; return; }
            var to = target - car.transform.position; to.y = 0f;
            float ang = Vector3.SignedAngle(fwd, to, Vector3.up);
            car.steerInput = Mathf.Clamp(ang / 25f, -1f, 1f);
            float v = car.ForwardSpeed;
            car.throttleInput = v < speed ? Mathf.Lerp(0.9f, 0.3f, Mathf.Abs(car.steerInput)) : 0f;
            car.brakeInput = v > speed + 3f ? 0.4f : 0f;
        }

        static bool NearestRoad(MadMax.World.WorldGen w, Vector3 p, out Vector3 point, out Vector3 dir)
        {
            float best = float.MaxValue; point = p; dir = Vector3.forward;
            foreach (var road in w.roads.roads)
                for (int i = 0; i + 1 < road.points.Count; i++)
                {
                    var a = road.points[i]; var b = road.points[i + 1];
                    var ab = new Vector3(b.x - a.x, 0f, b.z - a.z);
                    float t = Mathf.Clamp01(Vector3.Dot(new Vector3(p.x - a.x, 0f, p.z - a.z), ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                    var q = new Vector3(a.x, 0f, a.z) + ab * t;
                    float d = new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude;
                    if (d < best) { best = d; point = q; dir = ab.normalized; }
                }
            return best < 60f * 60f;
        }

        static IEnumerator Walk(ScenarioContext c, string anchor, float off)
        {
            var g = c.Game;
            var p = StoryAnchors.Get(anchor) + new Vector3(off, 0f, -off * 0.5f);
            p.y = MadMax.World.DeformableTerrain.Instance.Height(p.x, p.z) + 0.3f;
            g.Player.Teleport(p, 0f);
            c.Fixture("walked to " + anchor + " (teleport)");
            yield return new WaitForSeconds(1.2f);
        }

        static IEnumerator Until(System.Func<bool> ok, float seconds)
        {
            float t0 = Time.time;
            while (!ok() && Time.time - t0 < seconds) yield return null;
        }

        /// <summary>Open a conversation and choose the option starting with <paramref name="say"/>.</summary>
        static bool Talk(WastelandGame g, MadMax.Npc.Npc npc, string say)
        {
            if (!npc) return false;
            var d = new MadMax.Npc.Dialogue(g, npc);
            foreach (var ch in d.choices) if (ch.label.StartsWith(say)) { ch.act(); return true; }
            return false;
        }
    }

    /// <summary>A sandbox world runs no campaign: no story line, no cast, FIRST STEPS as before.</summary>
    class StorySandbox : Scenario
    {
        public override string Id => "story.sandbox";

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(1.5f);
            c.Check(!MadMax.Story.Story.Campaign && g.StoryLine == null, "no campaign in a sandbox world");
            c.Check(g.CastBody("nell") == null, "no story cast");
            c.Check(g.Fleet.Count > 1, $"the sandbox fleet is there ({g.Fleet.Count})");
        }
    }
}
