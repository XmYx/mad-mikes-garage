using System.Collections;
using System.Collections.Generic;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Animation revamp (2026-10-13): one smoke run through the new body states.</summary>
    public static class AnimRevampScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new AnimRevamp();
        }
    }

    /// <summary>ANIMATION REVAMP: walk and stop (start / stop feel), a turn on the spot before walking back, a gunshot to
    /// the side turns the head (attention), prone and a crawl of a few metres at crawl pace (low capsule), back up, sit
    /// on the ground and eat there (eat over the sitting pose), stand up, into the sedan by its door and out again
    /// (animated boarding: open, enter, close; out, shut), standing clear of the car.</summary>
    class AnimRevamp : Scenario
    {
        public override string Id => "anim.revamp";
        public override float Timeout => 150f;

        static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var p = g.Player;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.5f); }
            if (!TestWorld.Pad(4f, 20f, out var pad, out var fwd)) { c.Block("no clear pad"); yield break; }
            var settings = GameSettings.Current;
            bool oldWork = settings.workAnimation, oldBoard = settings.boardingAnimation;
            settings.workAnimation = settings.boardingAnimation = true;
            if (g.cameraRig && g.cameraRig.mode == ViewMode.FirstPerson) g.cameraRig.mode = ViewMode.ThirdPerson;
            p.Teleport(pad + Vector3.up * 0.1f, Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg);
            WastelandGame.ExternalInput = true;
            var cc = p.GetComponent<CharacterController>();
            yield return new WaitForSeconds(0.6f);
            Vector2 In(Vector3 world) { var d = Quaternion.Inverse(Quaternion.Euler(0f, p.viewYaw, 0f)) * world; return new Vector2(d.x, d.z).normalized; }

            // 1. walk, stop
            p.moveInput = In(fwd);
            yield return new WaitForSeconds(1.2f);
            float walk = new Vector2(p.Velocity.x, p.Velocity.z).magnitude;
            p.moveInput = Vector2.zero;
            string feel = null; float t0 = Time.time, stopped = -1f;
            while (Time.time - t0 < 1.4f)
            {
                feel = feel ?? p.Animator.FeelState;
                if (stopped < 0f && new Vector2(p.Velocity.x, p.Velocity.z).magnitude < 0.2f) stopped = Time.time - t0;
                yield return null;
            }
            c.Metric("walk_speed", walk, "m/s"); c.Metric("stop_time", stopped, "s");
            c.Check(walk > 1f, $"walks ({walk:0.0} m/s)");
            c.Check(stopped >= 0f && stopped < 0.7f, $"stops within {stopped:0.00} s (walk_stop's step straight away)");
            c.Check(feel != null, $"the stop plays its settle ({feel ?? "none"})");
            c.Screenshot("walk_stop");
            yield return null;

            // 2. walk back: off at once, the body swings round while walking
            float yaw0 = p.transform.eulerAngles.y; var pos0 = p.transform.position;
            p.moveInput = In(-fwd);
            string turnFeel = null; t0 = Time.time;
            while (Time.time - t0 < 0.6f) { turnFeel = turnFeel ?? p.TurningClip ?? p.Animator.FeelState; yield return null; }
            float turned = Mathf.Abs(Mathf.DeltaAngle(yaw0, p.transform.eulerAngles.y)), moved = Flat(pos0, p.transform.position);
            p.moveInput = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            c.Metric("turn_0_6s", turned, "deg"); c.Metric("moved_while_turning", moved, "m");
            c.Check(turned > 120f && moved > 0.4f, $"walks off and turns at once ({turned:0}° and {moved:0.00} m in 0.6 s)");

            // 3. a shot to the side turns the head
            var shot = p.transform.position + p.transform.right * 8f + Vector3.up;
            int pings = Attention.PingCount;
            Attention.Ping(shot, Attention.Kind.Gunshot, 60f);
            yield return new WaitForSeconds(0.35f);
            var att = p.Animator.Attention;
            var face = p.Rig.GetComponent<HumanFace>();
            c.Check(Attention.PingCount > pings, "the shot reached the bus");
            c.Check(att != null && att.Target.HasValue && (att.Target.Value - shot).magnitude < 1.5f, $"attention on the shot ({(att != null && att.Target.HasValue ? att.TargetKind.ToString() : "none")})");
            c.Check(face && face.Target.HasValue && (face.Target.Value - shot).magnitude < 1.5f, "the head and eyes look at it");
            c.Screenshot("look_at_shot");
            yield return new WaitForSeconds(1f);

            // 4. prone, crawl, up
            p.proneToggle = true;
            yield return VehicleFixScenarios.Until(() => p.Prone && p.LowTransit < 0f, 4f);
            c.Check(p.Prone, "prone");
            c.Check(cc.height < 0.75f, $"a low capsule ({cc.height:0.00} m)");
            var crawlFrom = p.transform.position;
            p.moveInput = In(fwd);
            yield return new WaitForSeconds(3f);
            p.moveInput = Vector2.zero;
            float crawled = Flat(crawlFrom, p.transform.position);
            c.Metric("crawl_3s", crawled, "m");
            c.Check(crawled > 0.9f && crawled < 2.8f, $"crawls at a crawl ({crawled:0.0} m in 3 s)");
            c.Screenshot("crawl");
            p.proneToggle = true;
            yield return VehicleFixScenarios.Until(() => p.LowState == PlayerCharacter.Low.None, 4f);
            c.Check(p.LowState == PlayerCharacter.Low.None && cc.height > 1.7f, $"back on the feet ({cc.height:0.00} m)");
            yield return new WaitForSeconds(0.4f);

            // 5. sit on the ground, eat, stand up
            c.Check(p.GoLow(PlayerCharacter.Low.Sit, true), "sits down on the ground");
            yield return VehicleFixScenarios.Until(() => p.GroundResting && p.LowTransit < 0f, 4f);
            c.Check(p.RestFactor > 1f, $"resting recovers faster (x{p.RestFactor:0.0})");
            g.Gesture(UseKind.Eat, "food_can");
            yield return null;
            c.Check(!HumanClips.Enabled || HumanClips.Get("eat") == null || p.SeatedGestureClip == "eat", $"eats sitting down ({p.SeatedGestureClip ?? "none"})");
            yield return new WaitForSeconds(0.5f);
            c.Screenshot("sit_eat");
            yield return new WaitForSeconds(2f);
            c.Check(!HumanClips.Enabled || HumanClips.Get("eat") == null || HumanClips.Get("eat").EventAt("bite") < 0f || p.LastClipEvent == "bite", $"the bite event fired ({p.LastClipEvent ?? "none"})");
            p.moveInput = In(fwd);
            yield return VehicleFixScenarios.Until(() => p.LowState == PlayerCharacter.Low.None, 4f);
            p.moveInput = Vector2.zero;
            c.Check(p.LowState == PlayerCharacter.Low.None && cc.enabled, "a move gets up");
            yield return new WaitForSeconds(0.4f);

            // 6. into the sedan by its door and out again
            var v = TestWorld.Vehicle("Sedan");
            if (!v) { c.Block("no sedan in the start fleet"); Restore(oldWork, oldBoard); yield break; }
            if (!TestWorld.Pad(9f, 20f, out var cpad, out var cfwd)) { c.Block("no pad for the car"); Restore(oldWork, oldBoard); yield break; }
            yield return TestWorld.Place(c, v, cpad, cfwd);
            var side = v.transform.position - v.transform.right * 3.2f - v.transform.forward * 1.5f;
            var terrain = MadMax.World.DeformableTerrain.Instance;
            if (terrain) side.y = terrain.Height(side.x, side.z) + 0.1f;
            p.Teleport(side, v.transform.eulerAngles.y + 60f);
            yield return new WaitForSeconds(0.4f);
            p.moveInput = Vector2.zero;
            WastelandGame.ExternalInput = false;                                               // the animated way in is for the player's own key
            var steps = new HashSet<string>();
            g.EnterAnimated(v);
            t0 = Time.time;
            bool shotSeated = false;
            while (Time.time - t0 < 12f && (g.Boarding || g.Current != v))
            {
                if (g.BoardingStep != null) steps.Add(g.BoardingStep);
                if (!shotSeated && g.BoardingStep == "enter" && g.BoardingT > 0.5f) { shotSeated = true; c.Screenshot("getting_in"); }
                yield return null;
            }
            c.Note("steps in: " + string.Join(", ", steps));
            c.Check(g.Current == v && p.SeatedIn == v, "in the driver's seat");
            c.Check(steps.Contains("open") && steps.Contains("enter") && steps.Contains("close"), "by the door: open, in, shut");
            yield return new WaitForSeconds(0.6f);
            c.Screenshot("seated");
            steps.Clear();
            g.ExitAnimated();
            t0 = Time.time;
            while (Time.time - t0 < 12f && (g.Boarding || g.Current))
            {
                if (g.BoardingStep != null) steps.Add(g.BoardingStep);
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            c.Note("steps out: " + string.Join(", ", steps));
            c.Check(!g.Current && !p.SeatedIn && cc.enabled && !p.Puppeted, "out and on the feet");
            c.Check(steps.Contains("out"), "climbs out by the door");
            var feet = p.transform.position;
            bool clear = true;
            foreach (var col in Physics.OverlapCapsule(feet + Vector3.up * 0.45f, feet + Vector3.up * 1.6f, 0.24f, ~0, QueryTriggerInteraction.Ignore))
                if (!col.transform.IsChildOf(p.transform)) { clear = false; c.Note("inside " + col.name); }
            c.Check(clear, "standing clear of the car");
            c.Screenshot("out");
            Restore(oldWork, oldBoard);
        }

        static void Restore(bool work, bool board)
        {
            var s = GameSettings.Current;
            s.workAnimation = work; s.boardingAnimation = board;
            WastelandGame.ExternalInput = false;
        }
    }
}
