using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>What the roads talk about: radio newsflashes, skirmishes, raids beaten or threatened, season prices — each
    /// with the day and, when it happened somewhere, the place. Bounty boards post the latest headlines within
    /// <see cref="Reach"/> of their town (and every region-wide one), so news from off-screen shows where the player
    /// trades. Saved as <c>SaveData.townNews</c>.</summary>
    public static class TownNews
    {
        public struct Entry { public int day; public bool local, wreck; public float x, z; public string text; }

        public const float Reach = 1800f;
        const int Max = 40;
        static readonly List<Entry> entries = new List<Entry>();
        public static IReadOnlyList<Entry> Entries => entries;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => entries.Clear();

        /// <summary>Post a headline; <paramref name="at"/> = where it happened (null: news for the whole region).
        /// The same text on the same day is kept once.</summary>
        public static void Post(string text, Vector3? at = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = Strip(text);
            int day = MadMax.World.DayNight.Day + 1;
            foreach (var e in entries) if (e.day == day && e.text == text) return;
            var n = new Entry { day = day, text = text };
            if (at.HasValue) { n.local = true; n.x = at.Value.x; n.z = at.Value.z; }
            entries.Insert(0, n);
            if (entries.Count > Max) entries.RemoveAt(entries.Count - 1);
        }

        /// <summary>Tie the newest headline posted at <paramref name="near"/> to the wreck it left at <paramref name="wreck"/>:
        /// boards offer to mark the site on the map.</summary>
        public static void MarkWreck(Vector3 near, Vector3 wreck)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!e.local || (e.x - near.x) * (e.x - near.x) + (e.z - near.z) * (e.z - near.z) > 4f) continue;
                e.wreck = true; e.x = wreck.x; e.z = wreck.z; entries[i] = e;
                return;
            }
        }

        /// <summary>The newest <paramref name="max"/> headlines a board at <paramref name="board"/> carries.</summary>
        public static List<Entry> Near(Vector3 board, int max)
        {
            var l = new List<Entry>();
            foreach (var e in entries)
            {
                if (e.local)
                {
                    float dx = e.x - board.x, dz = e.z - board.z;
                    if (dx * dx + dz * dz > Reach * Reach) continue;
                }
                l.Add(e);
                if (l.Count >= max) break;
            }
            return l;
        }

        static readonly string[] Prefixes = { "WORD ON THE ROAD: ", "ROAD NEWS: ", "NEWS: ", "NEWSFLASH: " };

        /// <summary>Radio lead-ins read oddly on paper: drop them.</summary>
        static string Strip(string t)
        {
            foreach (var p in Prefixes) if (t.StartsWith(p)) return t.Substring(p.Length);
            return t;
        }

        public static List<string> Save()
        {
            var l = new List<string>();
            foreach (var e in entries)
                l.Add(e.day + "|" + (e.local ? e.x.ToString("0", CultureInfo.InvariantCulture) + "," + e.z.ToString("0", CultureInfo.InvariantCulture) + (e.wreck ? ",w" : "") : "") + "|" + e.text);
            return l;
        }

        public static void Load(List<string> saved)
        {
            entries.Clear();
            if (saved == null) return;
            foreach (var s in saved)
            {
                var p = s.Split(new[] { '|' }, 3);
                if (p.Length != 3 || !int.TryParse(p[0], out int d)) continue;
                var e = new Entry { day = d, text = p[2] };
                var xz = p[1].Split(',');
                e.wreck = xz.Length == 3 && xz[2] == "w";
                if (xz.Length >= 2 && float.TryParse(xz[0], NumberStyles.Float, CultureInfo.InvariantCulture, out e.x)
                    && float.TryParse(xz[1], NumberStyles.Float, CultureInfo.InvariantCulture, out e.z)) e.local = true;
                entries.Add(e);
            }
        }
    }
}
