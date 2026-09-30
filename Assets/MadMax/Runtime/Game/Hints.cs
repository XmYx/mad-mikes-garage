using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Context hints: one short line the first couple of times you meet something (driving, a bike, a gun, the
    /// map...), instead of a wall of help text. Counts persist per player (PlayerPrefs), setting HINTS turns them off,
    /// H still shows the full sheet.</summary>
    public static class Hints
    {
        const string PrefKey = "madmax.hints";
        static Dictionary<string, int> seen;
        static float until, next;
        static string current;

        /// <summary>The hint on screen (null = none).</summary>
        public static string Current => Time.unscaledTime < until ? current : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { seen = null; until = next = 0f; current = null; }

        static Dictionary<string, int> Seen
        {
            get
            {
                if (seen != null) return seen;
                seen = new Dictionary<string, int>();
                foreach (var pair in Profile.GetString(PrefKey, "").Split(';'))
                {
                    var kv = pair.Split('=');
                    if (kv.Length == 2 && int.TryParse(kv[1], out int n)) seen[kv[0]] = n;
                }
                return seen;
            }
        }

        /// <summary>Show <paramref name="text"/> unless this hint was shown <paramref name="times"/> times already, hints are
        /// off, or another hint is up.</summary>
        public static bool Show(string id, string text, int times = 2)
        {
            if (!GameSettings.Current.hints || Time.unscaledTime < next) return false;
            Seen.TryGetValue(id, out int n);
            if (n >= times) return false;
            seen[id] = n + 1;
            current = text;
            until = Time.unscaledTime + 7f;
            next = until + 4f;
            var parts = new List<string>();
            foreach (var kv in seen) parts.Add(kv.Key + "=" + kv.Value);
            Profile.SetString(PrefKey, string.Join(";", parts));
            return true;
        }

        public static void Reset() { seen = new Dictionary<string, int>(); Profile.DeleteKey(PrefKey); until = next = 0f; }
    }
}
