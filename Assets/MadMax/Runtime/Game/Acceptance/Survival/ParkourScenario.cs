using System.Collections;
using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 Q2 (movement): on a clear lane with test obstacles (plain boxes, disclosed) the player walks,
    /// sprints and slides (crouch at a run), vaults a 0.9 m wall, mantles a 1.8 m block, is refused a mantle where a slab
    /// leaves no room on top (and stays on the near side), cannot walk under a 1.4 m bar standing but crouches under
    /// it, rolls out of a 4.5 m fall with far less damage than landing flat, and reels up onto a 3.5 m block with the
    /// grappling hook. Every move is driven by the player inputs (move, run, jump, crouch, tool); releasing them stops
    /// the player.</summary>
    class SurvivalParkour : Scenario
    {
        public override string Id => "survival.parkour";
        public override string[] Suites => SurvivalScenarios.Suites;
        public override float Timeout => 150f;

        readonly List<GameObject> props = new List<GameObject>();
        Vector3 pad, dir, side;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var P = g.Player; var s = g.Stats;
            if (g.Current) { g.Exit(); yield return null; }
            if (!TestWorld.Pad(3f, 38f, out pad, out dir)) { c.Block("no clear 38 m lane near the start"); yield break; }
            side = Vector3.Cross(Vector3.up, dir);
            MedMineScenarios.Healthy(c, "parkour from a clean slate");
            s.stamina = s.MaxStamina;
            c.Fixture($"test obstacles (boxes) along a clear lane at {pad.x:0},{pad.z:0}");
            var wall = Box(12f, 0.3f, 0.9f, 3f);
            var block = Box(16f, 2f, 1.8f, 3f);
            var bar = Box(22f, 0.3f, 0.3f, 3f, 1.5f);
            var ledge = Box(26f, 2f, 1.8f, 3f);
            Box(26f, 2f, 0.2f, 3f, 2.7f);                                                     // slab 0.9 m over the ledge
            var tower = Box(33f, 2f, 3.5f, 3f);
            yield return null;

            // ---- sprint and slide
            yield return Stand(c, 0f);
            float t0 = Time.time; bool slid = false; float slideTop = 0f;
            P.run = true; P.moveInput = new Vector2(0f, 1f);
            yield return SurvivalKit.GameSeconds(0.7f);
            float sprint = Horizontal(P.Velocity);
            P.crouch = true;
            for (int i = 0; i < 40; i++) { yield return null; if (P.Sliding) { slid = true; slideTop = Mathf.Max(slideTop, Horizontal(P.Velocity)); } }
            P.run = false; P.crouch = false; P.moveInput = Vector2.zero;
            c.Metric("sprint_speed", sprint, "m/s"); c.Metric("slide_speed", slideTop, "m/s");
            c.Check(sprint > 4f, $"sprinting ({sprint:0.0} m/s)");
            c.Check(slid && slideTop > sprint * 0.9f, $"crouch at a run slides ({slideTop:0.0} m/s)");
            yield return SurvivalKit.Until(() => !P.Sliding && Horizontal(P.Velocity) < 0.3f, 3f);
            c.Check(Horizontal(P.Velocity) < 0.3f && !P.Crouching, "releasing the keys stops the player, standing up");

            // ---- vault a low wall
            yield return Stand(c, 9.5f);
            var w = new Waited();
            yield return Approach(P, wall, w);
            float vaultT = Time.time;
            yield return Traverse(P, w);
            c.Metric("vault_time", Time.time - vaultT, "s");
            c.Check(w.ok && Along(P.transform.position) > 12.3f, $"Space at the 0.9 m wall vaults it (now {Along(P.transform.position):0.0} m along, wall at 12)");

            // ---- mantle a 1.8 m block
            yield return Stand(c, 13.5f);
            yield return Approach(P, block, w);
            float stam = s.stamina, low = stam;
            P.jump = true;
            yield return SurvivalKit.Until(() => { low = Mathf.Min(low, s.stamina); return P.Traversing; }, 1f, w);
            yield return SurvivalKit.Until(() => { low = Mathf.Min(low, s.stamina); return !P.Traversing; }, 4f);
            P.moveInput = Vector2.zero;
            yield return SurvivalKit.GameSeconds(0.3f);
            float onTop = P.transform.position.y - GroundY(16f);
            c.Metric("mantle_height", onTop, "m");
            c.Check(w.ok && onTop > 1.6f, $"Space at the 1.8 m block mantles onto it (feet {onTop:0.00} m up)");
            c.Check(low < stam - 5f, $"climbing costs stamina ({stam:0} -> {low:0} at the lowest)");
            c.Screenshot("mantle");
            yield return null;

            // ---- no room on top: refused, stays on the near side
            yield return Stand(c, 23.5f);
            yield return Approach(P, ledge, w);
            bool traversed = false;
            P.jump = true;
            for (int i = 0; i < 60; i++) { yield return null; traversed |= P.Traversing; }
            P.moveInput = Vector2.zero;
            yield return SurvivalKit.GameSeconds(0.6f);
            float up = P.transform.position.y - GroundY(24.5f);
            c.Check(!traversed && Along(P.transform.position) < 25.2f && up < 0.5f, $"no mantle where a slab leaves no room (climbed {traversed}, {Along(P.transform.position):0.0} m along, {up:0.00} m up)");

            // ---- a low bar: blocked standing, passed crouching
            yield return Stand(c, 19.5f);
            P.moveInput = new Vector2(0f, 1f);
            yield return SurvivalKit.GameSeconds(2.5f);
            float stuck = Along(P.transform.position);
            c.Check(stuck < 21.95f, $"standing up the 1.5 m bar stops you ({stuck:0.0} m along, bar at 22)");
            P.crouch = true;
            float crouchT = Time.time;
            yield return SurvivalKit.Until(() => Along(P.transform.position) > 22.8f, 8f, w);
            c.Metric("crouch_pass_time", Time.time - crouchT, "s");
            c.Check(w.ok && P.Crouching, $"crouched you pass under it ({Along(P.transform.position):0.0} m along)");
            P.moveInput = Vector2.zero;
            yield return SurvivalKit.GameSeconds(0.4f);
            P.crouch = false;
            yield return SurvivalKit.Until(() => !P.Crouching, 2f, w);
            c.Check(w.ok, "letting go of crouch stands you up in the open");

            // ---- falls: flat landing against a roll
            MedMineScenarios.Healthy(c, "before the falls");
            yield return Drop(c, 2f, false);
            float flat = lastFall;
            MedMineScenarios.Healthy(c, "between the falls");
            yield return Drop(c, 5f, true);
            float rolled = lastFall;
            c.Metric("fall_damage_flat", flat, "hp"); c.Metric("fall_damage_rolled", rolled, "hp");
            c.Check(flat > 10f, $"a 4.5 m fall hurts ({flat:0.0})");
            c.Check(rolled < flat * 0.5f && (g.ToastText ?? "").Length >= 0, $"crouching as you land rolls out of it ({rolled:0.0} against {flat:0.0})");
            MedMineScenarios.Healthy(c, "after the falls");

            // ---- grappling hook onto a 3.5 m block
            yield return Stand(c, 28.5f);
            var hook = ToolLibrary.Create("tool_grapple", g.propMaterial);
            if (!c.Check(hook is GrappleTool, "the grappling hook is a tool")) { Cleanup(g); yield break; }
            P.Equip(hook);
            SurvivalKit.FirstPerson(g);
            var aimAt = tower.transform.position + Vector3.up * 1.6f - dir * 0.2f;
            for (int i = 0; i < 6; i++) { SurvivalKit.Face(g, aimAt); yield return null; }
            bool zipped = false;
            P.Attack(false);
            yield return SurvivalKit.Until(() => { zipped |= P.Zipping; return zipped && !P.Traversing; }, 6f, w);
            yield return SurvivalKit.GameSeconds(0.5f);
            float topY = P.transform.position.y - GroundY(33f);
            c.Metric("grapple_height", topY, "m");
            c.Check(zipped, "the hook bites and reels the player in: " + (g.ToastText ?? ""));
            c.Check(topY > 3.2f, $"and over the edge onto the 3.5 m block (feet {topY:0.00} m up)");
            Cleanup(g);
        }

        float lastFall;

        IEnumerator Drop(ScenarioContext c, float at, bool roll)
        {
            var g = c.Game; var P = g.Player;
            var p = Lane(at); p.y = GroundY(at) + 4.5f;
            P.Teleport(p, Yaw(dir));
            P.crouch = roll;
            c.Fixture($"dropped from 4.5 m{(roll ? " holding crouch" : "")}");
            float hp = g.Stats.health;
            yield return SurvivalKit.Until(() => P.transform.position.y < GroundY(at) + 0.3f, 4f);
            yield return SurvivalKit.GameSeconds(0.5f);
            P.crouch = false;
            lastFall = hp - g.Stats.health;
            yield return SurvivalKit.GameSeconds(0.6f);
        }

        IEnumerator Stand(ScenarioContext c, float at)
        {
            var P = c.Game.Player;
            P.moveInput = Vector2.zero; P.run = P.jump = P.crouch = false;
            var p = Lane(at); p.y = GroundY(at) + 0.1f;
            P.Teleport(p, Yaw(dir));
            P.viewYaw = Yaw(dir);
            yield return SurvivalKit.GameSeconds(0.4f, 5);
        }

        /// <summary>Walk at an obstacle until its face is within reach, then press Space.</summary>
        IEnumerator Approach(PlayerCharacter P, GameObject target, Waited w)
        {
            float face = Along(target.transform.position) - target.transform.localScale.z * 0.5f;
            P.viewYaw = Yaw(dir);
            P.moveInput = new Vector2(0f, 1f);
            yield return SurvivalKit.Until(() => face - Along(P.transform.position) < 0.55f, 5f, w);
        }

        IEnumerator Traverse(PlayerCharacter P, Waited w)
        {
            P.jump = true;
            yield return SurvivalKit.Until(() => P.Traversing, 1f, w);
            bool started = w.ok;
            yield return SurvivalKit.Until(() => !P.Traversing, 4f);
            P.moveInput = Vector2.zero;
            yield return SurvivalKit.GameSeconds(0.3f);
            w.ok = started;
        }

        GameObject Box(float at, float depth, float height, float width, float lift = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ParkourTestBox";
            float gy = Mathf.Min(GroundY(at - depth * 0.5f), GroundY(at + depth * 0.5f)) - 0.02f;
            var p = Lane(at); p.y = gy + lift + height * 0.5f;
            go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(dir));
            go.transform.localScale = new Vector3(width, height, depth);
            var g = WastelandGame.Instance;
            if (g && g.propMaterial) go.GetComponent<MeshRenderer>().sharedMaterial = g.propMaterial;
            props.Add(go);
            return go;
        }

        void Cleanup(WastelandGame g)
        {
            foreach (var p in props) if (p) Object.Destroy(p);
            props.Clear();
            g.Player.Equip(null);
            g.Player.moveInput = Vector2.zero; g.Player.run = g.Player.jump = g.Player.crouch = false;
        }

        Vector3 Lane(float along) => pad + dir * along;
        float Along(Vector3 p) => Vector3.Dot(p - pad, dir);
        float GroundY(float along) { var p = Lane(along); return DeformableTerrain.Instance.Height(p.x, p.z); }
        static float Yaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        static float Horizontal(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
