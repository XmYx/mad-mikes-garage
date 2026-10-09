using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Unattended acceptance runner (roadmap 26, Q1). Player flags:
    /// <c>-acceptance &lt;suite&gt;</c> (fast | full | catalogue | vehicles | mobility), <c>-results &lt;dir&gt;</c>,
    /// <c>-scenario &lt;id prefixes, comma-separated&gt;</c>, <c>-skip &lt;id prefixes&gt;</c>, <c>-reuse</c> (quick: one world for
    /// consecutive standard-rule scenarios, fresh after a failure, every <see cref="ReuseLimit"/> scenarios and for
    /// <see cref="Scenario.Isolated"/> ones), <c>-runtimeout &lt;s&gt;</c>, and <c>-profiledir &lt;dir&gt;</c> (see <see cref="Profile"/>).
    /// Starts a fixed-seed new game, runs every matching scenario with its own timeout, writes results.json +
    /// junit.xml + coverage.json and quits with 0 (all passed), 1 (a failure), 2 (only blocked) or 3 (hang / crash:
    /// a watchdog thread reports a stalled main thread). No input is needed from launch to exit.</summary>
    public class AcceptanceRunner : MonoBehaviour
    {
        public static AcceptanceRunner Instance { get; private set; }
        /// <summary>Scenarios can check this to avoid side effects meant for players (hints, autosaves).</summary>
        public static bool Running => Instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Instance = null; }

        string suite = "fast", filter, skip, resultsDir;
        bool reuse;
        const int ReuseLimit = 8;
        float runTimeout = 5400f;                                             // the fast suite runs ~80 scenarios
        readonly List<ScenarioContext> results = new List<ScenarioContext>();
        readonly object gate = new object();
        long heartbeat;
        volatile bool finished;
        ScenarioContext current;
        Thread watchdog;
        float runStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-acceptance");
            if (i < 0 || Instance) return;
            var go = new GameObject("AcceptanceRunner");
            DontDestroyOnLoad(go);
            go.SetActive(false);                                                            // options before Awake
            var r = go.AddComponent<AcceptanceRunner>();
            if (i + 1 < args.Length && !args[i + 1].StartsWith("-")) r.suite = args[i + 1];
            string Arg(string k) { int j = Array.IndexOf(args, k); return j >= 0 && j + 1 < args.Length ? args[j + 1] : null; }
            r.filter = Arg("-scenario");
            r.skip = Arg("-skip");
            r.reuse = Array.IndexOf(args, "-reuse") >= 0;
            r.resultsDir = Path.GetFullPath(Arg("-results") ?? Path.Combine(Profile.Dir, "acceptance"));
            if (float.TryParse(Arg("-runtimeout"), NumberStyles.Float, CultureInfo.InvariantCulture, out var rt)) r.runTimeout = rt;
            go.SetActive(true);
        }

        /// <summary>Start a run from the editor (Play mode, e.g. via Unity MCP): an isolated profile under the project's
        /// Temp folder, results in <paramref name="results"/>; stops Play mode when done instead of quitting.</summary>
        public static void StartInEditor(string suite, string filter, string results) => StartInEditor(suite, filter, results, null, false);

        /// <summary>As above, leaving out the scenarios whose ids start with any of <paramref name="skip"/> (comma-separated);
        /// <paramref name="reuse"/>: a quick run sharing worlds between scenarios (see <c>-reuse</c>).</summary>
        public static void StartInEditor(string suite, string filter, string results, string skip, bool reuse)
        {
            if (Instance) return;
            Profile.Use(Path.Combine(Application.dataPath, "../Temp/acceptance_profile"));
            var go = new GameObject("AcceptanceRunner");
            DontDestroyOnLoad(go);
            go.SetActive(false);
            var r = go.AddComponent<AcceptanceRunner>();
            r.suite = suite; r.filter = string.IsNullOrEmpty(filter) ? null : filter; r.resultsDir = Path.GetFullPath(results);
            r.skip = string.IsNullOrEmpty(skip) ? null : skip; r.reuse = reuse;
            go.SetActive(true);
        }

        void Awake()
        {
            WastelandGame.RespawnWait = 8f;                                                   // a death the scenario did not plan picks the default
            Instance = this;
            Directory.CreateDirectory(resultsDir ?? (resultsDir = Path.Combine(Profile.Dir, "acceptance")));
            Application.logMessageReceivedThreaded += OnLog;
            Interlocked.Exchange(ref heartbeat, DateTime.UtcNow.Ticks);
            watchdog = new Thread(Watch) { IsBackground = true, Name = "AcceptanceWatchdog" };
            watchdog.Start();
            Debug.Log($"[acceptance] suite {suite}, results {resultsDir}, profile {Profile.Dir} (isolated {Profile.Isolated})");
            StartCoroutine(RunAll());
        }

        void OnDestroy() { Application.logMessageReceivedThreaded -= OnLog; finished = true; }

        void Update() => Interlocked.Exchange(ref heartbeat, DateTime.UtcNow.Ticks);

        void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Assert) return;
            if (msg.Contains("NoSubscription")) return;                                     // Unity AI package noise
            var c = current;
            if (c != null) lock (gate) c.Check(false, "unhandled exception: " + msg.Split('\n')[0] + " @ " + (stack ?? "").Split('\n')[0]);
        }

        /// <summary>Hang / deadline guard on its own thread: the main thread may be stuck.</summary>
        void Watch()
        {
            var begin = DateTime.UtcNow;
            while (!finished)
            {
                Thread.Sleep(1000);
                var idle = DateTime.UtcNow - new DateTime(Interlocked.Read(ref heartbeat));
                bool hung = idle.TotalSeconds > 120, late = (DateTime.UtcNow - begin).TotalSeconds > runTimeout + 60;
                if (!hung && !late) continue;
                lock (gate)
                {
                    var c = current;
                    if (c != null) { c.outcome = Outcome.Fail; c.reason = hung ? "main thread stalled for " + (int)idle.TotalSeconds + " s" : "run deadline exceeded"; if (!results.Contains(c)) results.Add(c); }
                    try { WriteReports(hung ? "HANG" : "DEADLINE"); } catch { }
                }
                Console.Error.WriteLine("[acceptance] " + (hung ? "main thread hang" : "deadline") + ": exiting 3");
#if UNITY_EDITOR
                finished = true;                                                                // never kill the editor
#else
                Environment.Exit(3);
#endif
            }
        }

        IEnumerator RunAll()
        {
            runStart = Time.realtimeSinceStartup;
            var all = new List<Scenario>();
            foreach (var s in AcceptanceScenarios.All())
                if (Array.IndexOf(s.Suites, suite) >= 0 && Matches(s.Id, filter, true) && !Matches(s.Id, skip, false)) all.Add(s);
            Debug.Log($"[acceptance] {all.Count} scenarios" + (skip != null ? " (skipping " + skip + ")" : "") + (reuse ? " (quick: worlds reused)" : ""));

            // data-only checks first: they need no world
            foreach (var s in all) if (!s.NeedsWorld) yield return RunOne(s);

            if (all.Exists(s => s.NeedsWorld))
            {
                var world = new ScenarioContext { id = "setup.new_game", resultsDir = resultsDir, startedAt = Time.realtimeSinceStartup };
                yield return StartWorld(world);
                world.seconds = Time.realtimeSinceStartup - world.startedAt;
                lock (gate) results.Add(world);
                bool first = true, standard = true, lastFailed = false;
                int sinceFresh = 0;
                foreach (var sc in all)
                {
                    if (!sc.NeedsWorld) continue;
                    if (world.outcome != Outcome.Pass) { lock (gate) results.Add(new ScenarioContext { id = sc.Id, outcome = Outcome.Blocked, reason = "no world: " + world.reason }); continue; }
                    // isolation: every scenario gets the same fresh fixed-seed world (a new game loads in a few seconds);
                    // a quick run (reuse) keeps the standard world going until something fails or needs its own
                    bool keep = reuse && !first && standard && sc.WorldRules == null && !sc.Isolated && !lastFailed && sinceFresh < ReuseLimit;
                    if (keep) sinceFresh++;
                    else if (!first || sc.WorldRules != null)
                    {
                        var fresh = new ScenarioContext { id = "setup.fresh_world", resultsDir = resultsDir, startedAt = Time.realtimeSinceStartup };
                        yield return StartWorld(fresh, sc.WorldRules);
                        if (fresh.outcome != Outcome.Pass) { lock (gate) results.Add(new ScenarioContext { id = sc.Id, outcome = Outcome.Blocked, reason = "fresh world failed: " + fresh.reason }); continue; }
                        sinceFresh = 0;
                    }
                    first = false;
                    standard = sc.WorldRules == null && !sc.Isolated;
                    int before = results.Count;
                    yield return RunOne(sc);
                    lastFailed = results.Count > before && results[results.Count - 1].outcome == Outcome.Fail;
                    Reset();
                }
            }
            Finish();
        }

        /// <summary>Does the id start with any of the comma-separated prefixes (none given: <paramref name="empty"/>)?</summary>
        static bool Matches(string id, string prefixes, bool empty)
        {
            if (string.IsNullOrEmpty(prefixes)) return empty;
            foreach (var f in prefixes.Split(',')) { var p = f.Trim(); if (p.Length > 0 && id.StartsWith(p)) return true; }
            return false;
        }

        /// <summary>A fixed-seed new game with the standard rules; waits until the player can act.</summary>
        IEnumerator StartWorld(ScenarioContext c, GameRules rules = null)
        {
            float t0 = Time.realtimeSinceStartup;
            while ((!WastelandGame.Instance || !WastelandGame.Instance.Ready || ScreenFader.Busy) && Time.realtimeSinceStartup - t0 < 180f) yield return null;
            if (!c.Check(WastelandGame.Instance && WastelandGame.Instance.Ready, "world loads within 180 s")) yield break;
            WastelandGame.ExternalInput = false;
            var menu = WastelandGame.Instance;
            menu.StartNewGame(rules ?? new GameRules { randomSeed = false }, new MadMax.RPG.CharacterStats(), new Appearance(), false);
            while ((WastelandGame.Instance == menu || !WastelandGame.Instance || !WastelandGame.Instance.Ready || TitleSequence.Playing || ScreenFader.Busy) && Time.realtimeSinceStartup - t0 < 300f) yield return null;
            var g = WastelandGame.Instance;
            if (!c.Check(g && g != menu && g.Ready, "new game (seed " + (g ? g.seed : -1) + ") ready within 300 s")) yield break;
            c.Metric("new_game_load", Time.realtimeSinceStartup - t0, "s");
            WastelandGame.ExternalInput = true;
            for (int i = 0; i < 30; i++) yield return null;                                 // let the first frames settle
        }

        IEnumerator RunOne(Scenario s)
        {
            var c = new ScenarioContext { id = s.Id, resultsDir = resultsDir, startedAt = Time.realtimeSinceStartup };
            lock (gate) current = c;
            Debug.Log("[acceptance] ▶ " + s.Id);
            IEnumerator it = null;
            try { it = s.Run(c); } catch (Exception e) { c.Check(false, "threw on start: " + e.Message); }
            while (it != null)
            {
                bool more;
                try { more = it.MoveNext(); }
                catch (Exception e) { c.Check(false, "threw: " + e.GetType().Name + ": " + e.Message); break; }
                if (!more) break;
                if (Time.realtimeSinceStartup - c.startedAt > s.Timeout) { c.Check(false, $"timeout after {s.Timeout:0} s"); break; }
                if (Time.realtimeSinceStartup - runStart > runTimeout) { c.Check(false, "run deadline reached"); break; }
                yield return it.Current;
            }
            c.seconds = Time.realtimeSinceStartup - c.startedAt;
            lock (gate) { results.Add(c); current = null; }
            Debug.Log($"[acceptance] {c.outcome.ToString().ToUpperInvariant()} {s.Id} ({c.seconds:0.0} s){(c.reason != null ? ": " + c.reason : "")}");
            if (c.outcome == Outcome.Fail) c.Screenshot("fail");
        }

        /// <summary>Between scenarios: release every input, back on foot, normal time.</summary>
        static void Reset()
        {
            Time.timeScale = 1f;
            WastelandGame.MachineInput = default;
            var g = WastelandGame.Instance;
            if (!g) return;
            try                                                                                       // a scenario that broke the world must not end the run
            {
                if (g.Current) { var v = g.Current; v.throttleInput = v.brakeInput = v.steerInput = 0f; v.handbrake = true; g.Exit(); }
                if (g.Player) g.Player.moveInput = Vector2.zero;
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        void Finish()
        {
            int code;
            lock (gate) code = WriteReports("DONE");
            finished = true;
            Debug.Log("[acceptance] finished, exit " + code);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }

        // ---------------------------------------------------------------- reports
        int WriteReports(string state)
        {
            int pass = 0, fail = 0, blocked = 0;
            foreach (var r in results) { if (r.outcome == Outcome.Pass) pass++; else if (r.outcome == Outcome.Fail) fail++; else blocked++; }
            int code = state == "HANG" || state == "DEADLINE" ? 3 : fail > 0 ? 1 : blocked > 0 ? 2 : 0;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\n  \"suite\": ").Append(Q(suite)).Append(",\n  \"state\": ").Append(Q(state)).Append(",\n  \"exit\": ").Append(code);
            sb.Append(",\n  \"build\": ").Append(Q(Application.version)).Append(",\n  \"platform\": ").Append(Q(Application.platform.ToString()));
            sb.Append(",\n  \"seed\": ").Append(WastelandGame.Instance ? WastelandGame.Instance.seed : -1);
            sb.Append(",\n  \"profile\": ").Append(Q(Profile.Dir)).Append(",\n  \"isolated\": ").Append(Profile.Isolated ? "true" : "false");
            sb.Append(",\n  \"counts\": { \"pass\": ").Append(pass).Append(", \"fail\": ").Append(fail).Append(", \"blocked\": ").Append(blocked).Append(" },\n  \"scenarios\": [\n");
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                sb.Append("    { \"id\": ").Append(Q(r.id)).Append(", \"outcome\": ").Append(Q(r.outcome.ToString().ToUpperInvariant()))
                  .Append(", \"seconds\": ").Append(r.seconds.ToString("0.00", inv)).Append(", \"reason\": ").Append(Q(r.reason));
                sb.Append(", \"fixtures\": [").Append(string.Join(", ", r.fixtures.ConvertAll(Q))).Append("]");
                sb.Append(", \"metrics\": {");
                for (int m = 0; m < r.metrics.Count; m++) sb.Append(m > 0 ? ", " : " ").Append(Q(r.metrics[m].name)).Append(": ").Append(r.metrics[m].value.ToString("0.###", inv));
                sb.Append(" }, \"evidence\": [").Append(string.Join(", ", r.evidence.ConvertAll(Q))).Append("]");
                sb.Append(", \"step_times\": [").Append(string.Join(", ", r.times.ConvertAll(t => t.ToString("0.00", inv)))).Append("]");
                sb.Append(", \"log\": [").Append(string.Join(", ", r.log.ConvertAll(Q))).Append("] }").Append(i < results.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(resultsDir, "results.json"), sb.ToString());

            var x = new StringBuilder();
            x.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<testsuite name=\"").Append(X(suite)).Append("\" tests=\"").Append(results.Count)
             .Append("\" failures=\"").Append(fail).Append("\" skipped=\"").Append(blocked).Append("\">\n");
            foreach (var r in results)
            {
                x.Append("  <testcase classname=\"acceptance\" name=\"").Append(X(r.id)).Append("\" time=\"").Append(r.seconds.ToString("0.00", inv)).Append("\">");
                if (r.outcome == Outcome.Fail) x.Append("<failure message=\"").Append(X(r.reason)).Append("\"/>");
                if (r.outcome == Outcome.Blocked) x.Append("<skipped message=\"BLOCKED: ").Append(X(r.reason)).Append("\"/>");
                x.Append("<system-out>").Append(X(string.Join("\n", r.log))).Append("</system-out></testcase>\n");
            }
            x.Append("</testsuite>\n");
            File.WriteAllText(Path.Combine(resultsDir, "junit.xml"), x.ToString());
            File.WriteAllText(Path.Combine(resultsDir, "coverage.json"), CoverageReport.Build(results));
            return code;
        }

        static string Q(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "").Replace("\t", " ") + "\"";
        static string X(string s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
