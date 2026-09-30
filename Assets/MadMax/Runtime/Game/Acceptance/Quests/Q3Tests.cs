using System.Collections;
using System.Linq;
using MadMax.Building;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    /// <summary>Shared steps of the S19-S22 and L1-L5 quest scenarios (on top of <see cref="H"/>).</summary>
    static class Q3T
    {
        public static bool Open(string q) => Plot.StateOf(q) == Plot.State.Open;
        public static bool Active(string q) => Plot.StateOf(q) == Plot.State.Active;
        public static bool Done(string q) => Plot.StateOf(q) == Plot.State.Done;

        /// <summary>Walk to a cast member's place, wait for them, and choose the line starting with <paramref name="say"/>.</summary>
        public static IEnumerator TalkAt(ScenarioContext c, string anchor, string npc, string say, float off = 2.5f)
        {
            var g = c.Game;
            if (!g.CastBody(npc) || Vector3.Distance(g.CastBody(npc).transform.position, g.Player.transform.position) > 6f) yield return H.Walk(c, anchor, off);
            yield return H.Until(() => g.CastBody(npc) != null, 6f);
            var body = g.CastBody(npc);
            if (body && Vector3.Distance(body.transform.position, g.Player.transform.position) > 6f)
            {
                var p = body.transform.position + body.transform.forward * 1.6f; p.y = MadMax.World.DeformableTerrain.Instance.Height(p.x, p.z) + 0.2f;
                g.Player.Teleport(p, body.transform.eulerAngles.y + 180f);
                yield return new WaitForSeconds(0.3f);
            }
            c.Check(H.Talk(g, g.CastBody(npc), say), npc + ": \"" + say + "\"");
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>Wait for a step to finish, then check it (with its route, if any).</summary>
        public static IEnumerator Step(ScenarioContext c, string q, string step, float seconds, string what)
        {
            yield return H.Until(() => Plot.StepDone(q, step), seconds);
            var r = Plot.Route(q, step);
            c.Check(Plot.StepDone(q, step), what + (r != null ? " (" + r + ")" : ""));
        }

        public static IEnumerator Finished(ScenarioContext c, string q, string what)
        {
            yield return H.Until(() => Done(q), 6f);
            c.Check(Done(q), what);
            c.Note("payoff: " + StoryLibrary.Get(q).payoff);
        }

        public static Placeable Prop(WastelandGame g, string id, string anchor, float r) =>
            Placeable.All.Where(p => p && p.id == id && g.IsStoryProp(p) && Vector2.Distance(new Vector2(p.transform.position.x, p.transform.position.z), new Vector2(StoryAnchors.Get(anchor).x, StoryAnchors.Get(anchor).z)) < r)
                         .FirstOrDefault();

        public static IEnumerator OnFoot(WastelandGame g)
        {
            if (!g.Current) yield break;
            g.Exit();
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>A ground vehicle of the fleet (no aircraft, boats, bikes or machines if avoidable).</summary>
        public static VehicleDriver GroundCar(WastelandGame g) =>
            g.Fleet.FirstOrDefault(v => v && v.driveable && !v.GetComponent<FlightModel>() && !v.GetComponent<BoatModel>() && !v.GetComponent<BikeBalance>() && !v.GetComponent<Machine>())
            ?? g.Fleet.FirstOrDefault(v => v && v.driveable && !v.GetComponent<FlightModel>() && !v.GetComponent<BoatModel>());
    }
}
