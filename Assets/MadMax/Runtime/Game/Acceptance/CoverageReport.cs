using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Feature-to-scenario manifest (StreamingAssets/Acceptance/manifest.json, roadmap 26 Q0): every README
    /// feature names the scenarios that prove it (id or id prefix ending in '*'), or states why it is a known gap.
    /// The report joins it with a run's results: covered / failing / blocked / not run / gap per feature.</summary>
    public static class CoverageReport
    {
        [System.Serializable] public class Feature { public string id, group, readme, gap; public string[] scenarios; }
        [System.Serializable] class Manifest { public int version; public Feature[] features; }

        public static Feature[] Load()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "Acceptance", "manifest.json");
            if (!File.Exists(path)) return new Feature[0];
            var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            return m != null && m.features != null ? m.features : new Feature[0];
        }

        static bool Matches(string pattern, string id) => pattern.EndsWith("*") ? id.StartsWith(pattern.TrimEnd('*')) : id == pattern;

        public static string Build(List<ScenarioContext> results)
        {
            var sb = new StringBuilder("{\n  \"features\": [\n");
            var feats = Load();
            int covered = 0, gaps = 0, failing = 0, notRun = 0;
            for (int i = 0; i < feats.Length; i++)
            {
                var f = feats[i];
                string status;
                if (f.scenarios == null || f.scenarios.Length == 0) { status = "GAP"; gaps++; }
                else
                {
                    int pass = 0, fail = 0, blocked = 0, seen = 0;
                    foreach (var pat in f.scenarios)
                        foreach (var r in results)
                            if (Matches(pat, r.id)) { seen++; if (r.outcome == Outcome.Pass) pass++; else if (r.outcome == Outcome.Fail) fail++; else blocked++; }
                    status = seen == 0 ? "NOT_RUN" : fail > 0 ? "FAILING" : blocked > 0 ? "BLOCKED" : "COVERED";
                    if (status == "COVERED") covered++; else if (status == "NOT_RUN") notRun++; else failing++;
                }
                sb.Append("    { \"id\": \"").Append(f.id).Append("\", \"group\": \"").Append(f.group).Append("\", \"status\": \"").Append(status)
                  .Append("\", \"gap\": ").Append(f.gap == null ? "null" : "\"" + f.gap.Replace("\"", "'") + "\"").Append(" }").Append(i < feats.Length - 1 ? ",\n" : "\n");
            }
            sb.Append("  ],\n  \"counts\": { \"features\": ").Append(feats.Length).Append(", \"covered\": ").Append(covered).Append(", \"failing_or_blocked\": ").Append(failing)
              .Append(", \"not_run\": ").Append(notRun).Append(", \"gap\": ").Append(gaps).Append(" }\n}\n");
            return sb.ToString();
        }

        /// <summary>Scenario ids the manifest names that no registered scenario provides (a broken contract).</summary>
        public static List<string> Dangling(IEnumerable<string> scenarioIds)
        {
            var ids = new List<string>(scenarioIds);
            var bad = new List<string>();
            foreach (var f in Load())
                if (f.scenarios != null)
                    foreach (var pat in f.scenarios)
                        if (!ids.Exists(id => Matches(pat, id))) bad.Add(f.id + " -> " + pat);
            return bad;
        }
    }
}
