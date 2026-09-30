using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Items;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S10(List<Scenario> into) => into.Add(new QuestS10());
    }

    /// <summary>S10 TWELVE VOLTS OF FAME in a sandbox world: Pip's car read off (bald rears, ballast, long gears, nearly
    /// dry), the spares mounted, the ballast out and short gears on the tuning settings, a real service from Pip's cans,
    /// the hill driven from the start flags to the top flag with the game's own driving, Pip told, the tyres and the
    /// stripe handed over; the route taken is in the payoff.</summary>
    class QuestS10 : Scenario
    {
        public override string Id => "story.s10";
        public override float Timeout => 200f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(MadMax.Story.Story.StateOf("S10") == MadMax.Story.Story.State.Open, "S10 is on offer in a sandbox world");
            var start = StoryAnchors.Get("s10_start"); var top = StoryAnchors.Get("s10_top");
            c.Metric("hill_rise", top.y - start.y, "m");
            c.Metric("hill_run", Vector2.Distance(new Vector2(start.x, start.z), new Vector2(top.x, top.z)), "m");
            yield return H.Walk(c, "pip", 2.5f);
            yield return H.Until(() => g.CastBody("pip") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("pip"), "IS THAT YOUR CAR"), "Pip Harlow wants to finish the hill trial")) yield break;
            yield return H.Until(() => MadMax.Story.Story.StepDone("S10", "look"), 4f);
            c.Check(g.Inventory.Get(ResourceType.Oil) >= 4 && g.Inventory.Get(ResourceType.Fuel) >= 10, "Pip's last oil and petrol");
            var tag = StoryTag.Find("pip_car");
            var car = tag ? tag.GetComponent<VehicleDriver>() : null;
            if (!c.Check(car, "Pip's little car waits at the start flags")) yield break;
            var tune = car.GetComponent<VehicleTuning>(); var sys = car.GetComponent<VehicleSystems>(); var chassis = car.GetComponent<VehicleChassis>();
            var rears = chassis.Sockets.Where(s => s.accepts == PartCategory.Wheel && s.Current && car.transform.InverseTransformPoint(s.transform.position).z < 0f).ToList();
            c.Check(tune && tune.ballast >= 100f && tune.gearing > 0.5f, "150 kg of ballast and long gears");
            c.Check(rears.Count == 2 && rears.All(s => s.Current.GetComponent<WheelStats>().wear > 0.8f), "two bald rear tyres");
            c.Check(sys && sys.fuel < 3f && sys.OilFraction < 0.3f, "nearly dry");
            c.Screenshot("pip_car");
            yield return null;

            // ---- the fixes
            var bench = StoryAnchors.Get("s10_bench");
            var spares = VehiclePart.Registry.Where(p => p && !p.Socket && p.GetComponent<WheelStats>() && Vector3.Distance(p.transform.position, bench) < 8f).Take(2).ToList();
            if (!c.Check(spares.Count == 2, "two spare wheels by Pip's bench")) yield break;
            for (int i = 0; i < 2; i++) { rears[i].Detach(true); yield return null; rears[i].Attach(spares[i]); }
            c.Fixture("swapped the rear wheels (the wrench's take and mount)");
            tune.ballast = 0f; tune.gearing = -0.6f; tune.Apply();
            c.Fixture("ballast out and short gears on the tuning page");
            int moved = sys.Service(g.Inventory);
            c.Note("serviced " + moved + " L from Pip's cans");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S10", "prep") && MadMax.Story.Story.StepDone("S10", "plan"), 5f);
            c.Check(MadMax.Story.Story.StepDone("S10", "tyres") && MadMax.Story.Story.StepDone("S10", "weight") && MadMax.Story.Story.StepDone("S10", "fluids"), "tyres, weight and fluids sorted");
            c.Check(MadMax.Story.Story.Route("S10", "plan") == StoryLibrary.S10Gears, "tuned for the slope: " + MadMax.Story.Story.Route("S10", "plan"));

            // ---- the trial, driven
            var up = Quaternion.Euler(0f, StoryAnchors.Yaw("s10_start"), 0f) * Vector3.forward;
            yield return TestWorld.Place(c, car, start - up * 2f, up, 1f);
            g.Enter(car); yield return new WaitForSeconds(0.5f);
            yield return TestWorld.StartEngine(c, car);
            if (c.Failed) yield break;
            yield return new WaitForSeconds(0.5f);
            car.handbrake = false;
            float t0 = Time.time; bool unaided = true;
            while (!MadMax.Story.Story.StepDone("S10", "trial") && Time.time - t0 < 60f)
            {
                var fwd = car.transform.forward; fwd.y = 0f;
                var to = top - car.transform.position; to.y = 0f;
                car.steerInput = Mathf.Clamp(Vector3.SignedAngle(fwd, to, Vector3.up) / 25f, -1f, 1f);
                car.throttleInput = 1f; car.brakeInput = 0f;
                if (Time.time - t0 > 45f && unaided)
                {
                    unaided = false;
                    yield return TestWorld.Place(c, car, top - up * 20f, up, 0.5f);
                    c.Fixture("the car couldn't make the climb in 45 s: placed 20 m below the top to finish the run");
                    car.handbrake = false;
                }
                yield return null;
            }
            car.throttleInput = 0f; car.brakeInput = 1f; car.handbrake = true;
            c.Metric("trial_seconds", Time.time - t0, "s");
            c.Metric("climbed_unaided", unaided ? 1f : 0f, "");
            if (!c.Check(MadMax.Story.Story.StepDone("S10", "trial"), "Pip's car reached the top flag")) yield break;
            c.Screenshot("the_top");
            yield return null;
            yield return new WaitForSeconds(1f);
            g.Exit(); yield return new WaitForSeconds(0.6f);

            // ---- Pip at the top, then the bench
            yield return H.Walk(c, "s10_top", -3f);
            yield return H.Until(() => g.CastBody("pip") != null && Vector3.Distance(g.CastBody("pip").transform.position, top) < 8f, 6f);
            int wheelsBefore = VehiclePart.Registry.Count(p => p && !p.Socket && p.GetComponent<WheelStats>() && Vector3.Distance(p.transform.position, bench) < 8f);
            c.Check(H.Talk(g, g.CastBody("pip"), "SHE MADE IT"), "Pip was waiting at the top");
            yield return H.Until(() => MadMax.Story.Story.Flag("s10_thanks"), 4f);
            yield return H.Walk(c, "pip", 2f);
            yield return H.Until(() => MadMax.Story.Story.StateOf("S10") == MadMax.Story.Story.State.Done, 5f);
            c.Check(MadMax.Story.Story.StateOf("S10") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("s10_done"), "S10 TWELVE VOLTS OF FAME is done");
            int wheelsAfter = VehiclePart.Registry.Count(p => p && !p.Socket && p.GetComponent<WheelStats>() && Vector3.Distance(p.transform.position, bench) < 8f);
            c.Check(wheelsAfter >= wheelsBefore + 2, $"two tyres for your own car by the bench ({wheelsBefore} → {wheelsAfter})");
            if (g.Fleet.Any(v => v && v != car)) c.Check(g.Fleet.Any(v => v && v != car && VehiclePaint.DecalOf(v) == 11), "a hand-painted checker stripe on your own car");
            c.Check(Journal.Entries.Any(e => e.text.Contains("STRAIGHT UP THE HILL ON SHORT GEARS")), "the payoff remembers the route");
        }
    }
}
