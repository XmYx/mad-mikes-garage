using System.Collections.Generic;
using System.Text;

namespace MadMax.Story
{
    /// <summary>Story system "allocation" (storyline C3, the finale's supply): a finite shipment shared out among customers.
    /// Each customer has a claim (what they asked for), a need (what inspection shows they really burn; mending a
    /// wasteful consumer lowers it) and may be essential. A plan turns that into whole shares that always add up to what is
    /// on hand: <see cref="Plan.ByNeed"/> covers essential needs first, then the rest in proportion to need, and keeps
    /// what nobody needs as a reserve; <see cref="Plan.Even"/> splits it evenly; <see cref="Plan.Favour"/> gives one
    /// customer its full claim openly and the rest by need. Pure arithmetic (largest remainder, ties to the earlier
    /// customer), so the posted numbers are reproducible and can be checked by anyone reading the board.</summary>
    public static class Allocation
    {
        public enum Plan { ByNeed, Even, Favour }

        public struct Customer
        {
            public string key, name;
            public int claim, need;
            public bool essential;
            public Customer(string key, string name, int claim, int need, bool essential = false) { this.key = key; this.name = name; this.claim = claim; this.need = need; this.essential = essential; }
        }

        /// <summary>Whole shares of <paramref name="supply"/> units for each customer (same order). Unallocated units are
        /// the reserve (<see cref="Reserve"/>).</summary>
        public static int[] Shares(IList<Customer> cs, int supply, Plan plan, int favoured = -1)
        {
            int n = cs.Count;
            var shares = new int[n];
            if (n == 0 || supply <= 0) return shares;
            int left = supply;
            switch (plan)
            {
                case Plan.Even:
                {
                    var want = new float[n];
                    for (int i = 0; i < n; i++) want[i] = supply / (float)n;
                    Round(want, supply, shares);
                    return shares;
                }
                case Plan.Favour:
                    if (favoured >= 0 && favoured < n)
                    {
                        shares[favoured] = System.Math.Min(cs[favoured].claim, left);
                        left -= shares[favoured];
                    }
                    ByNeed(cs, left, shares, favoured);
                    return shares;
                default:
                    ByNeed(cs, left, shares, -1);
                    return shares;
            }
        }

        /// <summary>Essential needs first (in full while they last), then the others' needs in proportion; never more than
        /// a need, so what nobody needs stays in reserve.</summary>
        static void ByNeed(IList<Customer> cs, int left, int[] shares, int skip)
        {
            int n = cs.Count;
            for (int i = 0; i < n && left > 0; i++)
            {
                if (i == skip || !cs[i].essential) continue;
                int give = System.Math.Min(cs[i].need, left);
                shares[i] += give; left -= give;
            }
            int wanted = 0;
            for (int i = 0; i < n; i++) if (i != skip && !cs[i].essential) wanted += cs[i].need;
            if (wanted <= 0 || left <= 0) return;
            if (wanted <= left) { for (int i = 0; i < n; i++) if (i != skip && !cs[i].essential) shares[i] += cs[i].need; return; }
            var want = new float[n];
            for (int i = 0; i < n; i++) want[i] = i != skip && !cs[i].essential ? cs[i].need * left / (float)wanted : 0f;
            var part = new int[n];
            Round(want, left, part);
            for (int i = 0; i < n; i++) shares[i] += part[i];
        }

        /// <summary>Largest-remainder rounding of <paramref name="want"/> to whole units summing to <paramref name="total"/>.</summary>
        static void Round(float[] want, int total, int[] into)
        {
            int n = want.Length, sum = 0;
            for (int i = 0; i < n; i++) { into[i] = (int)System.Math.Floor(want[i] + 1e-4f); sum += into[i]; }
            while (sum < total)
            {
                int best = -1; float bf = -1f;
                for (int i = 0; i < n; i++)
                {
                    if (want[i] <= 0f) continue;
                    float frac = want[i] - into[i];
                    if (frac > bf + 1e-4f) { bf = frac; best = i; }
                }
                if (best < 0) break;
                into[best]++; sum++;
            }
        }

        public static int Reserve(int[] shares, int supply) { int s = 0; foreach (var x in shares) s += x; return supply - s; }

        /// <summary>Total need left uncovered by the shares.</summary>
        public static int Shortfall(IList<Customer> cs, int[] shares)
        {
            int s = 0;
            for (int i = 0; i < cs.Count; i++) if (shares[i] < cs[i].need) s += cs[i].need - shares[i];
            return s;
        }

        /// <summary>The posted line: "CLINIC 4 OF 4, FARM 3 OF 3, LAMPS 2 OF 2 (NEED); 3 CANS IN RESERVE".</summary>
        public static string Posted(IList<Customer> cs, int[] shares, int supply, string unit)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cs.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(cs[i].name).Append(' ').Append(shares[i]).Append(" OF ").Append(cs[i].need);
            }
            sb.Append(" (NEED)");
            int r = Reserve(shares, supply);
            if (r > 0) sb.Append("; ").Append(Units(r, unit)).Append(" IN RESERVE");
            int gap = Shortfall(cs, shares);
            if (gap > 0) sb.Append("; ").Append(Units(gap, unit)).Append(" SHORT");
            return sb.ToString();
        }

        /// <summary>"1 CAN", "3 CANS" (units are given plural).</summary>
        public static string Units(int n, string unit) => n + " " + (n == 1 && unit.EndsWith("S") ? unit.Substring(0, unit.Length - 1) : unit);
    }
}
