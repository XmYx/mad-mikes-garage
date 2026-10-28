using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Scheduled update 2026-10-28: throws with a wind-up and the thing lit in the hand, thrown things as
    /// themselves, the paint station's spray job and wet paint, nitrous purge and hiss, a turbo kit heard, armour
    /// sparking off a blow.</summary>
    public static class ScheduledScenarios1028
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new ThrowMolotov();
            yield return new ThrowInterrupted();
            yield return new ThrowRock();
            yield return new PaintSpray();
            yield return new NitrousPurge();
            yield return new TurboKitHeard();
            yield return new ArmourDeflect();
        }

        internal static VehicleDriver NearestCar(WastelandGame g, float within)
        {
            VehicleDriver best = null; float bd = within * within;
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.driveable || v.aiDriven || !v.GetComponent<VehicleTuning>()) continue;
                float d = (v.transform.position - g.Player.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }

        internal static void Face(WastelandGame g)
        {
            if (g.cameraRig) g.cameraRig.LookToward(ScheduledScenarios1016.Fwd(g));
        }
    }

    /// <summary>A molotov: the arm winds up with the bottle in the hand and its rag alight (light, flame), it leaves the
    /// hand only at the top of the whip, flies as a bottle model (no cube) with its rag burning, and starts a fire.</summary>
    class ThrowMolotov : Scenario
    {
        public override string Id => "weapons.throw_molotov";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            ScheduledScenarios1028.Face(g);
            int had = g.Inventory.GetItem("throw_molotov");
            g.Inventory.AddItem("throw_molotov", 1);
            int started = WastelandGame.ThrowsStarted, released = WastelandGame.ThrowsReleased;
            g.Stats.stamina = g.Stats.MaxStamina;
            g.UseItem("throw_molotov");
            yield return null;
            c.Check(WastelandGame.ThrowsStarted == started + 1 && g.Throwing, "the throw winds up");
            c.Check(WastelandGame.ThrowsReleased == released, "nothing has left the hand yet");
            var held = g.Player.HeldProp;
            c.Check(held && held.GetComponent<ThrownLit>() && held.GetComponent<ThrownLit>().kind == ThrownLit.Kind.Rag, "the bottle is in the hand with its rag lit");
            yield return new WaitForSeconds(0.25f);
            c.Check(held && held.GetComponentInChildren<Light>(), "the burning rag lights the hand");
            c.Screenshot("throw_windup");
            yield return PH.Until(() => WastelandGame.ThrowsReleased > released, 2f);
            c.Check(WastelandGame.ThrowsReleased == released + 1, "released at the top of the whip");
            var body = WastelandGame.LastThrownBody;
            c.Check(body && body.name == "Molotov" && body.transform.Find("Visual") && !body.GetComponent<MeshFilter>(), "it flies as a bottle model, not a cube");
            var rb = body ? body.GetComponent<Rigidbody>() : null;
            c.Check(rb && rb.angularVelocity.magnitude > 5f, "spinning end over end");
            c.Check(body && body.GetComponent<ThrownLit>(), "the rag burns in flight");
            c.Check(!g.Player.HeldProp, "the hand is empty after the release");
            yield return null;
            c.Screenshot("throw_flight");
            yield return PH.Until(() => !body, 4f);
            c.Check(!body, "it smashed where it landed");
            c.Check(g.Inventory.GetItem("throw_molotov") == had, "one bottle used");
        }
    }

    /// <summary>A stick of dynamite, the throw cut short before the release (into a car): the fuse is pinched and the
    /// stick goes back in the pack; nothing flies.</summary>
    class ThrowInterrupted : Scenario
    {
        public override string Id => "weapons.throw_interrupt";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var car = ScheduledScenarios1028.NearestCar(g, 60f);
            if (!c.Check(car, "a car near the player")) yield break;
            int had = g.Inventory.GetItem("throw_dynamite");
            g.Inventory.AddItem("throw_dynamite", 1);
            var lastBody = WastelandGame.LastThrownBody;
            int released = WastelandGame.ThrowsReleased;
            g.Stats.stamina = g.Stats.MaxStamina;
            g.UseItem("throw_dynamite");
            c.Check(g.Throwing && g.Player.HeldProp && g.Player.HeldProp.GetComponent<ThrownLit>(), "the fuse is lit in the hand");
            g.Enter(car);                                                                           // the same frame: before any release, whatever the frame rate
            yield return null;
            yield return null;
            c.Check(!g.Throwing && WastelandGame.ThrowsReleased == released && WastelandGame.LastThrownBody == lastBody, "the throw stopped: nothing thrown");
            c.Check(g.Inventory.GetItem("throw_dynamite") == had + 1, "the stick is back in the pack");
            g.Exit();
            yield return new WaitForSeconds(0.4f);
            g.Inventory.TakeItem("throw_dynamite", 1);
        }
    }

    /// <summary>A throwing rock flies as a stone (its own model, unlit) and lands as a stone pickup.</summary>
    class ThrowRock : Scenario
    {
        public override string Id => "weapons.throw_rock";
        public override float Timeout => 15f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            ScheduledScenarios1028.Face(g);
            g.Inventory.AddItem("throw_rock", 1);
            int released = WastelandGame.ThrowsReleased;
            g.Stats.stamina = g.Stats.MaxStamina;
            g.UseItem("throw_rock");
            yield return PH.Until(() => WastelandGame.ThrowsReleased > released, 2f);
            var body = WastelandGame.LastThrownBody;
            c.Check(body && body.name == "Rock" && body.transform.Find("Visual"), "a stone flies");
            c.Check(body && !body.GetComponent<ThrownLit>(), "nothing burns on a rock");
            var mf = body ? body.GetComponentInChildren<MeshFilter>() : null;
            c.Check(mf && mf.sharedMesh && mf.sharedMesh.name.Contains("throw_rock"), "with the rock model (" + (mf && mf.sharedMesh ? mf.sharedMesh.name : "none") + ")");
            yield return PH.Until(() => !body, 4f);
            c.Check(!body, "it landed");
        }
    }

    /// <summary>A paint station beside a car: the paid job sprays down both flanks and the roof (overspray in the new
    /// colour), ends with the paint wet, and the wet paint picks up grit when driven on dusty ground.</summary>
    class PaintSpray : Scenario
    {
        public override string Id => "vehicle.paint_spray";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            var car = ScheduledScenarios1028.NearestCar(g, 80f);
            if (!c.Check(car, "a car near the player")) yield break;
            var root = g.Build.Structures;
            var at = PH.Ground(car.transform.position + car.transform.right * 3.2f);
            var p = FurnitureLibrary.Spawn("paint_booth", root, root.InverseTransformPoint(at), Quaternion.Inverse(root.rotation) * Quaternion.LookRotation(-car.transform.right), g.propMaterial);
            var booth = p ? p.GetComponent<PaintBooth>() : null;
            if (!c.Check(booth && PaintBooth.For(car) == booth, "a paint station beside the car")) yield break;
            var paint = VehiclePaint.Of(car);
            int was = paint.colour, sprays = PaintBooth.Sprays;
            paint.colour = was == 1 ? 2 : 1; paint.Apply();
            booth.Queue(car, paint.colour);
            yield return null;
            c.Check(booth.Spraying, "the gun starts on the car");
            yield return new WaitForSeconds(PaintBooth.SpraySeconds * 0.5f);
            c.Check(booth.SprayProgress > 0.3f && booth.SprayProgress < 0.8f, $"working along it ({booth.SprayProgress:0.00})");
            c.Screenshot("paint_spray");
            yield return PH.Until(() => !booth.Spraying, PaintBooth.SpraySeconds);
            c.Check(PaintBooth.Sprays == sprays + 1, "the job finished");
            var wet = car.GetComponent<WetPaint>();
            c.Check(wet && wet.Wet, "the paint is wet afterwards");
            paint.colour = was; paint.Apply();
            if (wet) Object.Destroy(wet);
            Object.Destroy(p.gameObject);
        }
    }

    /// <summary>A nitrous bottle fired: a white purge out of the bonnet, a hiss over the five seconds, then off.</summary>
    class NitrousPurge : Scenario
    {
        public override string Id => "vehicle.nitrous_purge";
        public override float Timeout => 45f;                                                     // 5 s of game time; slow test players cap dt

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var car = ScheduledScenarios1028.NearestCar(g, 80f);
            var tune = car ? car.GetComponent<VehicleTuning>() : null;
            if (!c.Check(tune, "a tunable car near the player")) yield break;
            int bottles = tune.nitrous, fired = VehicleTuning.NitrousFired;
            tune.nitrous = 1;
            c.Check(tune.FireNitrous() && VehicleTuning.NitrousFired == fired + 1 && tune.NitrousOn, "the bottle fires (purge)");
            yield return new WaitForSeconds(1f);
            c.Check(tune.NitrousOn, "still pulling after a second");
            yield return PH.Until(() => !tune.NitrousOn, 30f);
            c.Check(!tune.NitrousOn && tune.nitrous == 0, "empty after five seconds");
            tune.nitrous = bottles;
        }
    }

    /// <summary>A turbo kit on a car is heard: the engine voice is rebuilt with forced induction, and goes back to the
    /// engine's own when the kit is off.</summary>
    class TurboKitHeard : Scenario
    {
        public override string Id => "audio.turbo_kit";
        public override float Timeout => 15f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var car = ScheduledScenarios1028.NearestCar(g, 80f);
            var tune = car ? car.GetComponent<VehicleTuning>() : null;
            var va = car ? car.GetComponent<MadMax.Audio.VehicleAudio>() : null;
            if (!c.Check(tune && va, "a tunable car with an engine voice")) yield break;
            bool t0 = tune.turbo, s0 = tune.supercharger;
            g.Enter(car);
            yield return new WaitForSeconds(0.5f);
            var own = va.Engine != null ? va.Engine.Spec.air : Aspiration.Natural;
            tune.supercharger = false; tune.turbo = true;
            yield return new WaitForSeconds(0.3f);
            c.Check(va.Engine != null && va.Engine.Spec.air == Aspiration.Turbo, "the turbo is in the engine's voice (" + (va.Engine != null ? va.Engine.Spec.air.ToString() : "none") + ")");
            tune.turbo = false; tune.supercharger = true;
            yield return new WaitForSeconds(0.3f);
            c.Check(va.Engine != null && va.Engine.Spec.air == Aspiration.Blower, "a supercharger whines instead");
            tune.turbo = t0; tune.supercharger = s0;
            yield return new WaitForSeconds(0.3f);
            c.Check(va.Engine == null || t0 || s0 || va.Engine.Spec.air == own, "back to the engine's own breathing");
            g.Exit();
            yield return new WaitForSeconds(0.4f);
        }
    }

    /// <summary>Armour catching a blow: sparks and a ring off metal, a thud off soft armour; heavy kit jingles on the
    /// walk.</summary>
    class ArmourDeflect : Scenario
    {
        public override string Id => "body.armour_deflect";
        public override float Timeout => 10f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            int n = WastelandGame.Deflections;
            g.Deflected(MadMax.RPG.BodyZone.Torso, 10f);
            c.Check(WastelandGame.Deflections == n + 1, "the deflection plays (" + (WastelandGame.LastDeflectHard ? "metal" : "soft") + ")");
            yield return null;
            g.Deflected(MadMax.RPG.BodyZone.Head, 10f);
            c.Check(WastelandGame.Deflections == n + 2, "on the head too");
        }
    }
}
