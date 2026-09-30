using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public enum Outcome { Pass, Fail, Blocked }

    /// <summary>One acceptance check (roadmap 26). Runs as a coroutine inside a live game; asserts on game state and
    /// records disclosed fixtures (grants, teleports) and metrics. Subclasses stay small and data-driven.</summary>
    public abstract class Scenario
    {
        /// <summary>Stable id, e.g. "vehicle.drive.Sedan"; the manifest maps README features to these.</summary>
        public abstract string Id { get; }
        /// <summary>Suites this runs in: "fast" (PR gate), "full", "catalogue", "vehicles", "mobility".</summary>
        public virtual string[] Suites => new[] { "fast", "full" };
        /// <summary>Wall-clock budget in seconds; the runner fails the scenario past it.</summary>
        public virtual float Timeout => 60f;
        /// <summary>Needs a loaded world (false: pure data checks).</summary>
        public virtual bool NeedsWorld => true;
        /// <summary>Rules for this scenario's fresh world (null = the standard sandbox rules, seed 7).</summary>
        public virtual GameRules WorldRules => null;
        public abstract IEnumerator Run(ScenarioContext c);
    }

    /// <summary>What a scenario reports: outcome, first failure, step log, disclosed fixtures, metrics.</summary>
    public sealed class ScenarioContext
    {
        public string id;
        public Outcome outcome = Outcome.Pass;
        public string reason;
        public float startedAt, seconds;
        public readonly List<string> log = new List<string>();
        public readonly List<string> fixtures = new List<string>();
        public readonly List<(string name, float value, string unit)> metrics = new List<(string, float, string)>();
        public readonly List<string> evidence = new List<string>();
        public string resultsDir;

        public WastelandGame Game => WastelandGame.Instance;

        /// <summary>Assert: the first false check fails the scenario (later checks still log).</summary>
        public bool Check(bool ok, string what)
        {
            log.Add((ok ? "ok   " : "FAIL ") + what);
            if (!ok && outcome == Outcome.Pass) { outcome = Outcome.Fail; reason = what; }
            return ok;
        }

        /// <summary>A prerequisite is missing: the scenario cannot say pass or fail.</summary>
        public void Block(string why)
        {
            log.Add("BLOCKED " + why);
            if (outcome == Outcome.Pass) { outcome = Outcome.Blocked; reason = why; }
        }

        public bool Failed => outcome != Outcome.Pass;
        public void Note(string what) => log.Add("     " + what);
        /// <summary>Disclose a test shortcut (granted items, teleports, clock changes) in the report.</summary>
        public void Fixture(string what) { fixtures.Add(what); log.Add("fix  " + what); }
        public void Metric(string name, float value, string unit) { metrics.Add((name, value, unit)); log.Add("     " + name + " = " + value.ToString("0.###", CultureInfo.InvariantCulture) + " " + unit); }

        /// <summary>Save a screenshot as evidence. It is written at the end of the frame: yield a frame before changing
        /// what it should show.</summary>
        public void Screenshot(string tag)
        {
            if (resultsDir == null || Application.isBatchMode) return;
            var file = System.IO.Path.Combine(resultsDir, id.Replace('.', '_') + "_" + tag + ".png");
            ScreenCapture.CaptureScreenshot(file);
            evidence.Add(file);
        }
    }
}
