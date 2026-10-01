using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Harness self-tests (roadmap 26 Q1): scenarios that must NOT pass. Suite "harness" (never in fast/full);
    /// <c>tools/acceptance/selftest.py</c> checks that each yields its expected outcome and a bounded, nonzero exit.
    /// "harness_hang" stalls the main thread past the watchdog (over two minutes) and runs on its own.</summary>
    public static class HarnessScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new Expect("harness.pass", c => c.Check(true, "a passing check"));
            yield return new Expect("harness.fail", c => c.Check(false, "a deliberately false assertion"));
            yield return new Expect("harness.block", c => c.Block("a deliberately missing prerequisite"));
            yield return new Expect("harness.throw", c => throw new System.InvalidOperationException("deliberate exception"));
            yield return new Expect("harness.budget", c => c.Budget("deliberate_latency", 0.9f, 0.25f, "s"));
            yield return new Timeouts();
            yield return new LoggedException();
            yield return new Hang();
        }

        abstract class Harness : Scenario
        {
            public override string[] Suites => new[] { "harness" };
            public override bool NeedsWorld => false;
            public override float Timeout => 10f;
        }

        sealed class Expect : Harness
        {
            readonly string id; readonly System.Action<ScenarioContext> act;
            public Expect(string id, System.Action<ScenarioContext> act) { this.id = id; this.act = act; }
            public override string Id => id;
            public override IEnumerator Run(ScenarioContext c) { act(c); yield break; }
        }

        /// <summary>Never finishes: the per-scenario timeout must fail it.</summary>
        sealed class Timeouts : Harness
        {
            public override string Id => "harness.timeout";
            public override float Timeout => 3f;
            public override IEnumerator Run(ScenarioContext c) { while (true) yield return null; }
        }

        /// <summary>An exception logged by game code (not thrown in the scenario) must still fail it.</summary>
        sealed class LoggedException : Harness
        {
            public override string Id => "harness.logged_exception";
            public override IEnumerator Run(ScenarioContext c)
            {
                Debug.LogException(new System.Exception("deliberate logged exception"));
                yield return null;
            }
        }

        /// <summary>Stalls the main thread for 150 s: the watchdog thread must report a hang (exit 3).</summary>
        sealed class Hang : Harness
        {
            public override string Id => "harness.hang";
            public override string[] Suites => new[] { "harness_hang" };
            public override float Timeout => 300f;
            public override IEnumerator Run(ScenarioContext c)
            {
                c.Note("stalling the main thread for 150 s");
                yield return null;
                System.Threading.Thread.Sleep(150000);
            }
        }
    }
}
