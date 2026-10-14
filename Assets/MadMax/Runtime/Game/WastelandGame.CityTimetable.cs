using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Rolling cities on the map and in the journal (roadmap 28 R1, the open "map pin and timetable"): each
    /// city's live position as a pin, and a timetable line — at which dock it stands and when it leaves, or which dock
    /// it is bound for and in how long. ENTER in the journal sets a waypoint at that dock. Held flats are listed too, and
    /// a rented flat reminds the player a day before the rent runs out and once when it has.</summary>
    public partial class WastelandGame
    {
        public struct CityLine { public string label, when; public Vector3 dock; }

        /// <summary>One line per rolling city (journal "- ROLLING CITIES -").</summary>
        public void CityTimetable(List<CityLine> into)
        {
            into.Clear();
            foreach (var l in Leases)
                if (l.owner == Stats.name && (l.bought || l.until > DayNight.TotalDays))
                    into.Add(new CityLine { label = LeaseName(l), when = l.bought ? "YOURS" : "PAID TO DAY " + Mathf.FloorToInt(l.until), dock = Vector3.zero });
            foreach (var c in Cities)
            {
                if (!c || !c.Ready || c.Route == null || c.Route.docks == null || c.Route.docks.Length == 0) continue;
                var r = c.Route;
                int k = c.Dock >= 0 ? c.Dock : c.NextDock;
                k = Mathf.Clamp(k, 0, r.docks.Length - 1);
                string dock = r.dockNames != null && k < r.dockNames.Length && !string.IsNullOrEmpty(r.dockNames[k]) ? r.dockNames[k] : "DOCK " + (k + 1);
                var at = DockPoint(r, k);
                if (c.Dock >= 0) into.Add(new CityLine { label = c.Name + ": AT " + dock, when = "LEAVES IN " + Clock(c.DwellLeft), dock = at });
                else into.Add(new CityLine { label = c.Name + ": BOUND FOR " + dock, when = "ARRIVES IN " + Clock(c.SecondsToNext), dock = at });
            }
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return s >= 3600 ? s / 3600 + " H " + (s / 60 % 60) + " MIN" : s >= 60 ? s / 60 + " MIN" : s + " S";
        }

        /// <summary>The dock nearest the circuit's stop <paramref name="k"/> (the stop itself without dock objects).</summary>
        Vector3 DockPoint(CityRoute r, int k)
        {
            var p = r.At(r.docks[k], out _);
            Vector3 best = p; float bd = 200f * 200f;
            foreach (var d in CityDocks) if (d) { float q = (d.transform.position - p).sqrMagnitude; if (q < bd) { bd = q; best = d.transform.position; } }
            return best;
        }

        // ------------------------------------------------------------------ lease reminders (Living aboard, open item)

        /// <summary>A rented flat is reminded once when less than this many days are paid, and once when it runs out.</summary>
        public const float LeaseWarnDays = 1f;
        readonly HashSet<string> leaseReminded = new HashSet<string>();
        float leaseNext;
        /// <summary>The last lease reminder (tests).</summary>
        public static string LastLeaseReminder;

        string LeaseName(Lease l)
        {
            var host = l.city >= 0 && l.city < CityHost.Hosts.Count ? CityHost.Hosts[l.city] : null;
            string flat = host && host.CityPlan != null && l.room >= 0 && l.room < host.CityPlan.rooms.Count ? FlatLabel(host.CityPlan, host.CityPlan.rooms[l.room]) : "YOUR FLAT";
            return flat + (host ? " ON " + host.CityName : "");
        }

        void UpdateLeaseReminders()
        {
            if (Time.time < leaseNext || Leases.Count == 0 || Stats == null) return;
            leaseNext = Time.time + 5f;
            float now = DayNight.TotalDays;
            foreach (var l in Leases)
            {
                if (l.bought || l.owner != Stats.name) continue;
                string key = l.city + "|" + l.room + "|" + Mathf.FloorToInt(l.until);
                if (l.until > now && l.until - now < LeaseWarnDays && leaseReminded.Add(key + "|w"))
                    LastLeaseReminder = LeaseName(l) + " IS PAID TO DAY " + Mathf.FloorToInt(l.until) + ": PAY ANOTHER WEEK AT ITS DOOR";
                else if (l.until <= now && l.until > now - 3f && leaseReminded.Add(key + "|x"))
                    LastLeaseReminder = "THE RENT ON " + LeaseName(l) + " HAS RUN OUT: SOMEONE ELSE MAY TAKE IT";
                else continue;
                Toast(LastLeaseReminder);
                Journal.Add("HOME", LastLeaseReminder);
            }
        }

        /// <summary>The rolling cities as map pins (their live position).</summary>
        public void CityPins(List<Pin> into)
        {
            var col = new Color32(240, 200, 120, 255);
            foreach (var c in Cities)
                if (c && c.Ready) into.Add(new Pin { label = c.Name + (c.Dock >= 0 ? " (DOCKED)" : " (ROLLING)"), pos = c.transform.position, color = col });
        }
    }
}
