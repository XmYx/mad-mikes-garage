using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the use animations (WastelandGame.UseAnim, PlayerCharacter.Transit): a light
    /// switch flips only after the hand has reached it, sitting down takes a moment and ends seated, a bed is lain on
    /// before the night passes and left standing beside it.</summary>
    public static class UseAnimScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new AnimUseSwitch();
            yield return new AnimSitTransition();
            yield return new AnimLieBed();
        }

        /// <summary>WORK ANIMATION on for the scenario (restored by the returned value).</summary>
        public static bool ForceOn() { bool was = GameSettings.Current.workAnimation; GameSettings.Current.workAnimation = true; return was; }
    }

    /// <summary>[E] on a light switch at chest height: the use is pending for a while with the arm posed (the hands go to
    /// the switch), the switch state is unchanged until then and flipped right after.</summary>
    class AnimUseSwitch : Scenario
    {
        public override string Id => "anim.use_switch";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var P = g.Player; var got = new Waited(); var at = new Vector3[1];
            bool was = UseAnimScenarios.ForceOn();
            yield return SurvivalKit.OnFoot(c, 6f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); GameSettings.Current.workAnimation = was; yield break; }
            var fwd = P.transform.forward;
            var piece = SurvivalKit.Piece(g, "light_switch", SurvivalKit.Ground(at[0] + fwd * 0.9f) + Vector3.up * 1.15f, -fwd, false);
            var sw = piece ? piece.GetComponent<LightSwitch>() : null;
            c.Fixture("a light switch set up at 1.15 m, 0.9 m in front of the player; WORK ANIMATION on");
            if (!c.Check(sw, "the switch stands")) { GameSettings.Current.workAnimation = was; yield break; }
            yield return SurvivalKit.GameSeconds(0.3f);
            bool on0 = sw.on;
            g.UseInteractable(sw, sw, false);
            bool pendingSeen = g.UsePending, posed = false, early = false;
            float t0 = Time.time;
            while (g.UsePending && Time.time - t0 < 3f)
            {
                posed |= P.PoseOverride.HasValue;
                early |= sw.on != on0;
                yield return null;
            }
            float delay = Time.time - t0;
            c.Metric("switch_delay", delay, "s");
            c.Check(pendingSeen && g.UseNowKind == UseKind.FlipSwitch || g.LastUseKind == UseKind.FlipSwitch, "the switch plays the FlipSwitch use");
            c.Check(posed, "the arm is posed (PoseOverride) while the hand goes to the switch");
            c.Check(!early, "the switch keeps its state until the hand reaches it");
            c.Check(sw.on != on0, $"the switch flipped after the reach ({delay:0.00} s)");
            c.Check(delay >= 0.2f && delay <= 1.5f, "the reach takes 0.2-1.5 s");
            c.Screenshot("switch_flip");
            yield return null;
            yield return SurvivalKit.Until(() => !g.UseRunning, 3f);
            c.Check(!P.PoseOverride.HasValue, "the pose is released after the follow-through");
            GameSettings.Current.workAnimation = was;
        }
    }

    /// <summary>[E] on a chair 1.6 m away: the player is seated at once (logically) but the body walks up, turns and lowers
    /// over more than 0.2 s, ending at the seat; standing up again takes a moment and ends on the feet beside it.</summary>
    class AnimSitTransition : Scenario
    {
        public override string Id => "anim.sit_transition";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var P = g.Player; var got = new Waited(); var at = new Vector3[1];
            bool was = UseAnimScenarios.ForceOn();
            yield return SurvivalKit.OnFoot(c, 6f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); GameSettings.Current.workAnimation = was; yield break; }
            var fwd = P.transform.forward;
            var chair = SurvivalKit.Piece(g, "chair", at[0] + fwd * 1.6f, fwd);
            var seat = chair ? chair.GetComponentInChildren<Seat>() : null;
            c.Fixture("a chair 1.6 m in front of the player, facing away; WORK ANIMATION on");
            if (!c.Check(seat, "the chair stands")) { GameSettings.Current.workAnimation = was; yield break; }
            yield return SurvivalKit.GameSeconds(0.3f);
            seat.Use(g, false);
            float t0 = Time.time; bool transit = P.InTransit; var legs = new HashSet<string>();
            while (P.InTransit && Time.time - t0 < 4f) { legs.Add(P.TransitLeg); yield return null; }
            float took = Time.time - t0;
            c.Metric("sit_seconds", took, "s");
            c.Note("legs: " + string.Join(", ", legs));
            c.Check(transit && legs.Contains("Sit"), "sitting down plays a transition with a Sit leg");
            c.Check(took > 0.2f && took < 2f, $"sitting down takes a moment ({took:0.00} s)");
            c.Check(P.Sitting && P.SeatedOn == seat, "it ends seated on the chair");
            float hips = Vector3.Distance(P.transform.position + Vector3.up * (0.94f * P.Rig.appearance.height), seat.transform.TransformPoint(seat.spots[0]));
            c.Check(hips < 0.15f, $"the hips end on the seat ({hips:0.00} m off)");
            yield return SurvivalKit.GameSeconds(0.4f);
            c.Screenshot("seated");
            yield return null;
            P.StandUp();
            t0 = Time.time; bool up = P.InTransit;
            yield return SurvivalKit.Until(() => !P.InTransit, 3f);
            float stood = Time.time - t0;
            c.Metric("stand_seconds", stood, "s");
            var cc = P.GetComponent<CharacterController>();
            c.Check(up && stood > 0.2f, $"standing up takes a moment ({stood:0.00} s)");
            c.Check(!P.Sitting && cc && cc.enabled, "it ends on the feet (controller back on)");
            GameSettings.Current.workAnimation = was;
        }
    }

    /// <summary>[E] on a bed at night: the player walks to its side, sits on the edge and lies back (lying), the night
    /// passes only once lying, and afterwards the player gets up and stands beside the bed.</summary>
    class AnimLieBed : Scenario
    {
        public override string Id => "anim.lie_bed";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var P = g.Player; var got = new Waited(); var at = new Vector3[1];
            bool was = UseAnimScenarios.ForceOn();
            yield return SurvivalKit.OnFoot(c, 8f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); GameSettings.Current.workAnimation = was; yield break; }
            var fwd = P.transform.forward; var side = P.transform.right;
            var piece = SurvivalKit.Piece(g, "bed", at[0] + fwd * 1.8f, side);
            var bed = piece ? piece.GetComponent<Bed>() : null;
            DayNight.SetHours(22.5f);
            c.Fixture("a bed 1.8 m in front of the player, the clock at 22:30; WORK ANIMATION on");
            if (!c.Check(bed, "the bed stands")) { GameSettings.Current.workAnimation = was; yield break; }
            yield return SurvivalKit.GameSeconds(0.3f);
            int day0 = DayNight.Day;
            g.UseInteractable(bed, bed, false);
            c.Check(P.LyingOn == bed.transform, "[E] on the bed starts lying down on it");
            bool slept = false, sleptBeforeLying = false, lyingSeen = false;
            float t0 = Time.time;
            while (P.LyingOn && Time.time - t0 < 10f)
            {
                bool morning = DayNight.Day != day0 || DayNight.Hours < 12f;
                if (morning && !slept) { slept = true; sleptBeforeLying = !P.Lying && P.TransitLeg != "Hold"; }
                if (P.Lying && !lyingSeen)
                {
                    lyingSeen = true;
                    c.Metric("lie_down_seconds", Time.time - t0, "s");
                    c.Screenshot("lying");
                }
                yield return null;
            }
            c.Check(lyingSeen, "the player lies on the bed");
            c.Check(slept, "the night passed (morning)");
            c.Check(!sleptBeforeLying, "the night passed only once lying");
            yield return SurvivalKit.Until(() => !P.InTransit, 3f);
            var cc = P.GetComponent<CharacterController>();
            float off = Vector3.Distance(P.transform.position, bed.transform.position);
            c.Check(!P.BodyBusy && cc && cc.enabled, "the player got up again (controller back on)");
            c.Check(off < 2f, $"standing beside the bed ({off:0.0} m from it)");
            GameSettings.Current.workAnimation = was;
        }
    }
}
