using System.Collections.Generic;
using System.Text;

namespace MadMax.Story
{
    /// <summary>Readiness-gated operation (system key <c>transfer</c>, storyline §1 "urgency is honest"): a checklist of
    /// needs read from live state (seats, water, medicine, darkness...), shown to the player as it fills, and a start the
    /// player gives explicitly once the list is met — never a countdown. After the start the owning quest's game hook runs
    /// the phases (boarding, trips, arrival). Its state lives in story flags ("{id}:started"), so it saves with the
    /// campaign and a reload never restarts or loses it.</summary>
    public sealed class Operation
    {
        public struct Need
        {
            public string label, unit;
            public System.Func<int> have, want;
        }

        public readonly string id, title;
        readonly List<Need> needs = new List<Need>();
        readonly StringBuilder sb = new StringBuilder();

        public Operation(string id, string title) { this.id = id; this.title = title; }

        /// <summary>Add a line to the checklist: <paramref name="have"/> must reach <paramref name="want"/>.</summary>
        public Operation Needs(string label, string unit, System.Func<int> have, System.Func<int> want)
        {
            needs.Add(new Need { label = label, unit = unit, have = have, want = want });
            return this;
        }

        public IReadOnlyList<Need> All => needs;

        /// <summary>Every need met right now.</summary>
        public bool Ready
        {
            get
            {
                foreach (var n in needs) if (n.have() < n.want()) return false;
                return true;
            }
        }

        /// <summary>"SEATS 3/6  WATER 12/12 L  ..." (needs that want nothing are left out).</summary>
        public string Checklist()
        {
            sb.Clear();
            foreach (var n in needs)
            {
                int want = n.want();
                if (want <= 0) continue;
                int have = n.have();
                if (sb.Length > 0) sb.Append("  ");
                sb.Append(n.label).Append(' ').Append(have >= want ? want : have).Append('/').Append(want).Append(n.unit).Append(have >= want ? " OK" : "");
            }
            return sb.ToString();
        }

        /// <summary>What is still short ("2 SEATS, 4 L WATER"); empty when ready.</summary>
        public string Missing()
        {
            sb.Clear();
            foreach (var n in needs)
            {
                int short_ = n.want() - n.have();
                if (short_ <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(short_).Append(n.unit.Length > 0 ? n.unit.TrimStart() + " " : " ").Append(n.label);
            }
            return sb.ToString();
        }

        public bool Started => Story.Flag(id + ":started");

        /// <summary>The player says go: starts only when every need is met (the reason otherwise). Notes "{id}:go".</summary>
        public bool TryStart(out string why)
        {
            why = null;
            if (Started) return false;
            if (!Ready) { why = "NOT READY: STILL SHORT OF " + Missing(); return false; }
            Story.SetFlag(id + ":started");
            Story.Note(id + ":go");
            return true;
        }
    }
}
