using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The player's notebook: rumours heard, errands and jobs taken, places found — newest first, with the day.
    /// Shown on the JOURNAL page next to the live job list (contracts, town bosses). Saved as <c>SaveData.journal</c>.</summary>
    public static class Journal
    {
        public struct Entry { public int day; public string kind, text; }

        const int Max = 80;
        static readonly List<Entry> entries = new List<Entry>();
        public static IReadOnlyList<Entry> Entries => entries;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => entries.Clear();

        /// <summary>Note something (the same text on the same day is kept once).</summary>
        public static void Add(string kind, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            int day = MadMax.World.DayNight.Day + 1;
            foreach (var e in entries) if (e.day == day && e.text == text) return;
            entries.Insert(0, new Entry { day = day, kind = kind, text = text });
            if (entries.Count > Max) entries.RemoveAt(entries.Count - 1);
        }

        public static List<string> Save()
        {
            var l = new List<string>();
            foreach (var e in entries) l.Add(e.day + "|" + e.kind + "|" + e.text);
            return l;
        }

        public static void Load(List<string> saved)
        {
            entries.Clear();
            if (saved == null) return;
            foreach (var s in saved)
            {
                var p = s.Split(new[] { '|' }, 3);
                if (p.Length == 3 && int.TryParse(p[0], out int d)) entries.Add(new Entry { day = d, kind = p[1], text = p[2] });
            }
        }
    }
}
