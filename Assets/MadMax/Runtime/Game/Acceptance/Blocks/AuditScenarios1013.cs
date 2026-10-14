using System.Collections;
using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap audit (2026-10-13), stage 26: press / hold / release through <see cref="Controls"/>, the
    /// player's own recovery of an overturned car (never the road pilot's), and the multiplayer authority gaps kept
    /// visible as BLOCKED entries until they are closed.</summary>
    public static class AuditScenarios1013
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new HoldRelease();
            yield return new RecoverKey();
            yield return new AuthorityGap("online.client_trade_authority", "talking and trading on a client act on the client's local copy (no host authority for stock, disposition or payment)");
            yield return new AuthorityGap("online.client_taming_authority", "taming and riding animals need the host (clients see proxies only)");
        }
    }

    /// <summary>An injected hold reads as Down on its first frame, Held while it lasts and Up exactly once on release.</summary>
    class HoldRelease : Scenario
    {
        public override string Id => "controls.hold_release";
        public override bool NeedsWorld => false;
        public override float Timeout => 10f;

        public override IEnumerator Run(ScenarioContext c)
        {
            const Controls.Act act = Controls.Act.Horn;
            const int frames = 6;
            var press = ActionPress.Hold(act, frames);
            int downs = 0, helds = 0, ups = 0, guard = 0;
            while (press && guard++ < 60)
            {
                yield return null;                                                            // coroutines resume after every Update
                if (Controls.Down(act)) downs++;
                if (Controls.Held(act)) helds++;
                if (Controls.Up(act)) ups++;
            }
            for (int i = 0; i < 3; i++) { yield return null; if (Controls.Up(act)) ups++; if (Controls.Held(act)) helds++; }
            c.Check(downs == 1, $"Down fires once ({downs})");
            c.Check(helds >= frames - 1 && helds <= frames + 1, $"Held lasts the hold ({helds} frames for {frames})");
            c.Check(ups == 1, $"Up fires once on release ({ups})");
        }
    }

    /// <summary>The player's RECOVER key puts an overturned car back on its wheels (an explicit action, measured).</summary>
    class RecoverKey : Scenario
    {
        public override string Id => "vehicle.recover_key";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = TestWorld.Vehicle("Sedan");
            if (!v) { c.Block("no Sedan in the start fleet"); yield break; }
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no level test pad"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward);
            g.Enter(v);
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(g.Current == v, "entered the Sedan")) yield break;
            var t = DeformableTerrain.Instance;
            var at = new Vector3(pad.x, t.Height(pad.x, pad.z) + 1.6f, pad.z);
            v.Body.position = at;
            v.Body.rotation = Quaternion.AngleAxis(180f, Vector3.forward);
            v.Body.linearVelocity = Vector3.zero; v.Body.angularVelocity = Vector3.zero;
            v.Body.WakeUp();
            c.Fixture("rolled the Sedan onto its roof");
            yield return new WaitForSeconds(2f);
            if (!c.Check(v.transform.up.y < 0.5f, $"the car lies on its side or roof (up.y {v.transform.up.y:0.00})")) yield break;
            float t0 = Time.time;
            var press = ActionPress.Press(Controls.Act.Recover);
            while (v.transform.up.y < 0.8f && Time.time - t0 < 3f) yield return null;
            c.Budget("recover_response", Time.time - t0, 0.5f, "s");
            yield return new WaitForSeconds(1.5f);
            c.Check(v.transform.up.y > 0.85f, $"back on its wheels after settling (up.y {v.transform.up.y:0.00})");
            c.Check(g.Current == v, "the driver stays at the wheel");
            if (press) Object.Destroy(press.gameObject);
        }
    }

    /// <summary>A documented multiplayer limitation: always BLOCKED with its reason, so the coverage report shows it.</summary>
    class AuthorityGap : Scenario
    {
        readonly string id, why;
        public AuthorityGap(string id, string why) { this.id = id; this.why = why; }
        public override string Id => id;
        public override string[] Suites => new[] { "full" };
        public override bool NeedsWorld => false;
        public override IEnumerator Run(ScenarioContext c) { c.Block("known multiplayer gap: " + why); yield break; }
    }
}
