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
        /// <summary>Always gets a fresh world, even in a quick run that reuses one (it changes the world for good).</summary>
        public virtual bool Isolated => false;
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
        /// <summary>Seconds since the scenario started, one per <see cref="log"/> line (step timings).</summary>
        public readonly List<float> times = new List<float>();
        public readonly List<string> fixtures = new List<string>();
        public readonly List<(string name, float value, string unit)> metrics = new List<(string, float, string)>();
        public readonly List<string> evidence = new List<string>();
        public string resultsDir;

        public WastelandGame Game => WastelandGame.Instance;

        /// <summary>Assert: the first false check fails the scenario (later checks still log).</summary>
        public bool Check(bool ok, string what)
        {
            Log((ok ? "ok   " : "FAIL ") + what);
            if (!ok && outcome == Outcome.Pass) { outcome = Outcome.Fail; reason = what; }
            return ok;
        }

        /// <summary>A prerequisite is missing: the scenario cannot say pass or fail.</summary>
        public void Block(string why)
        {
            Log("BLOCKED " + why);
            if (outcome == Outcome.Pass) { outcome = Outcome.Blocked; reason = why; }
        }

        public bool Failed => outcome != Outcome.Pass;
        public void Note(string what) => Log("     " + what);
        /// <summary>Disclose a test shortcut (granted items, teleports, clock changes) in the report.</summary>
        public void Fixture(string what) { fixtures.Add(what); Log("fix  " + what); }
        public void Metric(string name, float value, string unit) { metrics.Add((name, value, unit)); Log("     " + name + " = " + value.ToString("0.###", CultureInfo.InvariantCulture) + " " + unit); }

        /// <summary>A measured number against its target (roadmap 26 Q0 budgets): recorded as a metric, fails past
        /// <paramref name="limit"/> (or below it when <paramref name="atLeast"/>).</summary>
        public bool Budget(string name, float value, float limit, string unit, bool atLeast = false)
        {
            Metric(name, value, unit);
            var inv = CultureInfo.InvariantCulture;
            return Check(atLeast ? value >= limit : value <= limit,
                $"{name} {value.ToString("0.###", inv)} {unit} within budget ({(atLeast ? ">=" : "<=")} {limit.ToString("0.###", inv)})");
        }

        // wall clock (thread-safe: log callbacks may arrive off the main thread)
        readonly long born = System.DateTime.UtcNow.Ticks;
        void Log(string line) { log.Add(line); times.Add((float)((System.DateTime.UtcNow.Ticks - born) / 1e7)); }

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
