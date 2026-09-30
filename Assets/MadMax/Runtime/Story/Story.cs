using System.Collections.Generic;
using MadMax.Game;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Story
{
    /// <summary>The campaign's running state: which quests are open, active or done, the current step of each, the
    /// routes taken, choices, world flags and the reward ledger (every reward is paid once, however often a step is
    /// re-checked or a save reloaded). Evaluated from live game state once a second by <see cref="Tick"/>; events that
    /// can't be read from state (a talk topic, a repair, a car driven) arrive through <see cref="Note"/>. Saved as
    /// <c>SaveData.story</c>.</summary>
    public static class Story
    {
        public enum State { Locked, Open, Active, Done }

        static readonly Dictionary<string, State> states = new Dictionary<string, State>();
        static readonly Dictionary<string, int> steps = new Dictionary<string, int>();
        static readonly HashSet<string> met = new HashSet<string>();        // "Q:step:condIndex" conditions satisfied by events
        static readonly HashSet<string> done = new HashSet<string>();       // "Q:step" finished steps (also optional ones)
        static readonly HashSet<string> ledger = new HashSet<string>();     // "Q:step" / "Q:" rewards paid
        static readonly HashSet<string> flags = new HashSet<string>();
        static readonly Dictionary<string, string> routes = new Dictionary<string, string>();
        static readonly Dictionary<string, float> driven = new Dictionary<string, float>();
        static readonly Dictionary<string, float> baseline = new Dictionary<string, float>();   // Ground goals: height when the quest began
        static readonly HashSet<string> evidence = new HashSet<string>();
        static float nextTick;
        static Vector3 lastPos;

        /// <summary>Story mode is on for this world (GameRules.story); side quests run in sandbox too.</summary>
        public static bool Campaign;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Clear(); Campaign = false; }

        public static void Clear()
        {
            states.Clear(); steps.Clear(); met.Clear(); done.Clear(); ledger.Clear(); flags.Clear(); routes.Clear(); driven.Clear(); baseline.Clear(); evidence.Clear();
            nextTick = 0f; lastPos = Vector3.zero;
        }

        public static State StateOf(string id) => states.TryGetValue(id, out var s) ? s : State.Locked;
        public static bool Flag(string f) => flags.Contains(f);
        public static void SetFlag(string f) => flags.Add(f);
        public static int StepOf(string id) => steps.TryGetValue(id, out var i) ? i : 0;
        public static bool StepDone(string q, string step) => done.Contains(q + ":" + step);
        public static string Route(string q, string step) => routes.TryGetValue(q + ":" + step, out var r) ? r : null;
        public static bool Paid(string key) => ledger.Contains(key);
        /// <summary>Evidence records the player holds (storyline A: they survive losing the paper copy).</summary>
        public static bool Evidence(string key) => evidence.Contains(key);
        public static IEnumerable<string> AllEvidence => evidence;
        public static float Driven(string q, string step) => driven.TryGetValue(q + ":" + step, out var d) ? d : 0f;

        /// <summary>Quests that may run in this world: Playable ones, and story-only ones only in a campaign.</summary>
        public static bool Runs(QuestDef q) => q.build == Build.Playable && (Campaign || !q.storyOnly);

        /// <summary>The current step of an active quest (null otherwise).</summary>
        public static StepDef Current(QuestDef q)
        {
            if (StateOf(q.id) != State.Active) return null;
            int i = StepOf(q.id);
            while (i < q.steps.Count && (q.steps[i].optional || done.Contains(q.id + ":" + q.steps[i].id))) i++;
            return i < q.steps.Count ? q.steps[i] : null;
        }

        /// <summary>An event happened: marks every current or optional condition that waits for it.</summary>
        public static void Note(string key)
        {
            foreach (var q in StoryLibrary.All)
            {
                if (StateOf(q.id) != State.Active) continue;
                foreach (var s in q.steps)
                {
                    if (done.Contains(q.id + ":" + s.id)) continue;
                    for (int c = 0; c < s.any.Count; c++)
                    {
                        var cond = s.any[c];
                        string want = cond.goal == Goal.Talk ? "talk:" + cond.key + ":" + cond.topic : cond.goal == Goal.Craft ? "craft:" + cond.key : cond.goal == Goal.Event ? cond.key : null;
                        if (want == key) met.Add(q.id + ":" + s.id + ":" + c);
                    }
                }
            }
            nextTick = 0f;
        }

        /// <summary>Open a quest the player accepted (a giver's offer) or that starts by itself.</summary>
        public static void Activate(WastelandGame g, string id)
        {
            var q = StoryLibrary.Get(id);
            if (q == null || StateOf(id) == State.Active || StateOf(id) == State.Done) return;
            states[id] = State.Active; steps[id] = 0;
            CaptureBaselines(q);
            if (q.hook != null) Journal.Add("STORY", q.title + ": " + q.hook);
            Journal.Add("JOB", q.title);
            if (g) { g.Toast("NEW: " + q.title); Guide(g, q); }
        }

        /// <summary>Finish a quest outright: every required step, its rewards, the quest (tests and chapter skips).</summary>
        public static void Complete(WastelandGame g, string id)
        {
            var q = StoryLibrary.Get(id);
            if (q == null || StateOf(id) == State.Done) return;
            if (StateOf(id) != State.Active) Activate(g, id);
            foreach (var s in q.steps) if (!s.optional && !done.Contains(q.id + ":" + s.id)) Finish(g, q, s, 0);
            Complete(g, q);
        }

        static void CaptureBaselines(QuestDef q)
        {
            var t = MadMax.World.DeformableTerrain.Instance;
            if (!t) return;
            foreach (var s in q.steps)
                for (int c = 0; c < s.any.Count; c++)
                {
                    var cond = s.any[c];
                    if (cond.goal != Goal.Ground || !StoryAnchors.Has(cond.key)) continue;
                    string k = q.id + ":" + s.id + ":" + c;
                    if (baseline.ContainsKey(k)) continue;
                    var p = StoryAnchors.Get(cond.key);
                    baseline[k] = t.Height(p.x, p.z);
                }
        }

        /// <summary>Once a second: open quests whose prerequisites are done, check the active ones' steps.</summary>
        public static void Tick(WastelandGame g)
        {
            if (!g || !g.Player) return;
            var pos = g.Current ? g.Current.transform.position : g.Player.transform.position;
            float step = lastPos == Vector3.zero ? 0f : Mathf.Min(8f, Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(lastPos.x, 0f, lastPos.z)));
            lastPos = pos;
            bool inFleet = g.Current && ContainsFleet(g, g.Current);
            if (Time.time < nextTick && step < 0.001f) return;
            foreach (var q in StoryLibrary.All)
            {
                if (!Runs(q)) continue;
                var st = StateOf(q.id);
                if (st == State.Locked && Unlocked(q)) { states[q.id] = State.Open; st = State.Open; if (q.giver == null) Activate(g, q.id); }
                if (st != State.Active) continue;
                if (inFleet) foreach (var s in q.steps) foreach (var c in s.any) if (c.goal == Goal.Drive) { string k = q.id + ":" + s.id; driven[k] = (driven.TryGetValue(k, out var d) ? d : 0f) + step; }
            }
            if (Time.time < nextTick) return;
            nextTick = Time.time + 1f;
            foreach (var q in StoryLibrary.All)
            {
                if (!Runs(q) || StateOf(q.id) != State.Active) continue;
                bool advanced = false;
                for (int pass = 0; pass < 2; pass++)                                                     // optional steps first: a bonus met on arrival still counts
                    foreach (var s in q.steps)
                    {
                        if (s.optional != (pass == 0) || done.Contains(q.id + ":" + s.id)) continue;
                        bool current = s == Current(q);
                        if (!current && !s.optional) continue;
                        int hit = Satisfied(g, q, s, pos);
                        if (hit < 0) continue;
                        Finish(g, q, s, hit);
                        advanced |= current;
                    }
                if (Current(q) == null) Complete(g, q);
                else if (advanced) Guide(g, q);
            }
        }

        static bool ContainsFleet(WastelandGame g, MadMax.Vehicles.VehicleDriver v) { foreach (var f in g.Fleet) if (f == v) return true; return false; }

        static bool Unlocked(QuestDef q)
        {
            foreach (var a in q.after) if (StateOf(a) != State.Done) return false;
            foreach (var n in q.needs) if (!Systems.Ready(n)) return false;
            return true;
        }

        /// <summary>Index of the first satisfied alternative of a step (-1 = none yet).</summary>
        static int Satisfied(WastelandGame g, QuestDef q, StepDef s, Vector3 pos)
        {
            for (int c = 0; c < s.any.Count; c++)
            {
                var cond = s.any[c];
                switch (cond.goal)
                {
                    case Goal.Reach:
                    {
                        var tagged = StoryTag.Find(cond.key);                                           // a tagged vehicle is where it is, not where it started
                        if ((tagged || StoryAnchors.Has(cond.key)) && Flat(pos, tagged ? tagged.transform.position : StoryAnchors.Get(cond.key)) <= cond.amount) return c;
                        break;
                    }
                    case Goal.Have:
                        if (cond.key.StartsWith("res:") && int.TryParse(cond.key.Substring(4), out int t) ? g.Inventory.Get((ResourceType)t) >= cond.amount : g.Inventory.GetItem(cond.key) >= cond.amount) return c;
                        break;
                    case Goal.Build:
                    {
                        var center = StoryAnchors.Get(s.waypoint ?? cond.key);
                        foreach (var p in MadMax.Building.Placeable.All)
                            if (p && p.id == cond.key && Flat(p.transform.position, center) <= cond.amount && !g.IsStoryProp(p)) return c;
                        break;
                    }
                    case Goal.Drive:
                        if (driven.TryGetValue(q.id + ":" + s.id, out var d) && d >= cond.amount) return c;
                        break;
                    case Goal.Steps:
                    {
                        int n = 0;
                        foreach (var id in cond.key.Split(',')) if (done.Contains(q.id + ":" + id.Trim())) n++;
                        if (n >= cond.amount) return c;
                        break;
                    }
                    case Goal.Bring:
                    {
                        if (s.waypoint == null || !StoryAnchors.Has(s.waypoint)) break;
                        var at = StoryAnchors.Get(s.waypoint);
                        foreach (var tag in StoryTag.All)
                            if (tag && tag.key == cond.key && Flat(tag.transform.position, at) <= cond.amount) return c;
                        break;
                    }
                    case Goal.Ground:
                    {
                        string k = q.id + ":" + s.id + ":" + c;
                        var ter = MadMax.World.DeformableTerrain.Instance;
                        if (!ter || !baseline.TryGetValue(k, out var b0) || !StoryAnchors.Has(cond.key)) break;
                        var gp = StoryAnchors.Get(cond.key);
                        float delta = ter.Height(gp.x, gp.z) - b0;
                        if (cond.amount < 0f ? delta <= cond.amount : delta >= cond.amount) return c;
                        break;
                    }
                    default:
                        if (met.Contains(q.id + ":" + s.id + ":" + c)) return c;
                        break;
                }
            }
            return -1;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        static void Finish(WastelandGame g, QuestDef q, StepDef s, int route)
        {
            string key = q.id + ":" + s.id;
            done.Add(key);
            if (s.any.Count > 1 && s.any[route].label != null) routes[key] = s.any[route].label;
            Pay(g, key, s.reward, s.optional ? "FOUND" : "DONE", s.text);
        }

        static void Complete(WastelandGame g, QuestDef q)
        {
            states[q.id] = State.Done;
            Pay(g, q.id + ":", q.reward, "DONE", q.title);
            if (q.payoff != null) Journal.Add("STORY", q.payoff);
            g.Toast("DONE: " + q.title);
            MadMax.Audio.Sfx.Play2D("cash", 0.6f);
        }

        /// <summary>Hand over a reward once; the ledger key makes replays, reloads and double checks harmless.</summary>
        static void Pay(WastelandGame g, string key, Reward r, string kind, string what)
        {
            if (r == null || r.Empty || !ledger.Add(key)) return;
            var parts = new List<string>();
            foreach (var (item, n) in r.take) g.Inventory.TakeItem(item, n);
            if (r.scrap > 0) { g.Inventory.Add(ResourceType.Scrap, r.scrap); parts.Add(r.scrap + " SCRAP"); }
            foreach (var (item, n) in r.items) { g.Inventory.AddItem(item, n); parts.Add(ItemCatalog.Name(item)); }
            foreach (var (t, n) in r.resources) { g.Inventory.Add(t, n); parts.Add(n + " " + ResourceInfo.Name(t)); }
            foreach (var (skill, xp) in r.training) g.Stats.Practice(skill, xp);
            if (r.flag != null) flags.Add(r.flag);
            foreach (var e in r.evidence) if (evidence.Add(e)) { Journal.Add("EVIDENCE", EvidenceText(e)); parts.Add("EVIDENCE: " + EvidenceName(e)); }
            if (parts.Count > 0) Journal.Add(kind, what + " (+" + string.Join(", ", parts) + ")");
        }

        static void Guide(WastelandGame g, QuestDef q)
        {
            var s = Current(q);
            if (s == null) return;
            string text = Controls.Localize(s.text);
            Journal.Add("JOB", q.title + ": " + text);
            if (s.waypoint != null && StoryAnchors.Has(s.waypoint)) g.SetWaypoint(StoryAnchors.Get(s.waypoint), text, true);
        }

        /// <summary>The HUD objective: the first active campaign quest's current step (main arcs first).</summary>
        public static string Line
        {
            get
            {
                foreach (var q in StoryLibrary.All)
                {
                    var s = Current(q);
                    if (s != null) return q.id + " " + q.title + ": " + s.text;
                }
                return null;
            }
        }

        static string EvidenceName(string e) => e switch { "receipt" => "FUEL RECEIPT", "manifest" => "FORGED MANIFEST", "cook" => "THE COOK'S STATEMENT", "len" => "LEN PIKE'S TESTIMONY", _ => e.ToUpperInvariant() };
        static string EvidenceText(string e) => e switch
        {
            "receipt" => "FUEL RECEIPT: YOUR CHIT NUMBER, CASHED THE DAY AFTER THE CRASH, SIGNED IN SOMEONE ELSE'S HAND.",
            "manifest" => "FORGED MANIFEST: YOUR NAME AS DRIVER, ADA VENN'S OFFICE STAMP, A ROUTE THAT OFFICIALLY RECEIVES NOTHING.",
            "cook" => "THE COOK SAW 'YOU' PAY IN GUILD COUPONS AND ASK FOR A CHEAP TYRE PATCH.",
            "len" => "LEN PIKE WAS PAID TO WEAR YOUR NAME ON THE FREIGHT BOOKS. HE'LL SAY SO.",
            _ => e.ToUpperInvariant()
        };

        public static List<string> Save()
        {
            var l = new List<string>();
            foreach (var kv in states) l.Add("q|" + kv.Key + "|" + (int)kv.Value + "|" + StepOf(kv.Key));
            foreach (var k in met) l.Add("m|" + k);
            foreach (var k in done) l.Add("d|" + k);
            foreach (var k in ledger) l.Add("l|" + k);
            foreach (var k in flags) l.Add("f|" + k);
            foreach (var kv in routes) l.Add("r|" + kv.Key + "|" + kv.Value);
            foreach (var kv in driven) l.Add("v|" + kv.Key + "|" + kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var kv in baseline) l.Add("b|" + kv.Key + "|" + kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var k in evidence) l.Add("e|" + k);
            if (Campaign) l.Add("campaign");
            return l;
        }

        public static void Load(List<string> saved)
        {
            Clear();
            Campaign = false;
            if (saved == null) return;
            foreach (var line in saved)
            {
                var p = line.Split('|');
                switch (p[0])
                {
                    case "q" when p.Length >= 4: states[p[1]] = (State)int.Parse(p[2]); steps[p[1]] = int.Parse(p[3]); break;
                    case "m" when p.Length >= 2: met.Add(line.Substring(2)); break;
                    case "d" when p.Length >= 2: done.Add(line.Substring(2)); break;
                    case "l" when p.Length >= 2: ledger.Add(line.Substring(2)); break;
                    case "f" when p.Length >= 2: flags.Add(p[1]); break;
                    case "r" when p.Length >= 3: routes[p[1]] = p[2]; break;
                    case "v" when p.Length >= 3: driven[p[1]] = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture); break;
                    case "b" when p.Length >= 3: baseline[p[1]] = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture); break;
                    case "e" when p.Length >= 2: evidence.Add(p[1]); break;
                    case "campaign": Campaign = true; break;
                }
            }
        }
    }
}
