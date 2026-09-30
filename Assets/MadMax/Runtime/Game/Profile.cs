using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Where the game keeps saves, settings and hint history. Normally persistentDataPath + PlayerPrefs;
    /// <c>-profiledir &lt;dir&gt;</c> (acceptance runs) redirects everything into that folder as plain files so a test never
    /// reads or overwrites the player's slots, autosave or preferences.</summary>
    public static class Profile
    {
        static string dir;
        static Dictionary<string, string> prefs;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { dir = null; prefs = null; }

        /// <summary>True when running on a disposable profile folder.</summary>
        public static bool Isolated => Dir != Application.persistentDataPath;

        /// <summary>Point the profile at <paramref name="folder"/> (editor-driven acceptance runs; null = back to normal).</summary>
        public static void Use(string folder) { dir = folder == null ? null : Path.GetFullPath(folder); prefs = null; if (dir != null) Directory.CreateDirectory(dir); }

        public static string Dir
        {
            get
            {
                if (dir != null) return dir;
                var args = System.Environment.GetCommandLineArgs();
                int i = System.Array.IndexOf(args, "-profiledir");
                dir = i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : Application.persistentDataPath;
                if (dir != Application.persistentDataPath) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        static string PrefsFile => Path.Combine(Dir, "prefs.txt");

        static Dictionary<string, string> Prefs
        {
            get
            {
                if (prefs != null) return prefs;
                prefs = new Dictionary<string, string>();
                if (File.Exists(PrefsFile))
                    foreach (var line in File.ReadAllLines(PrefsFile))
                    {
                        int eq = line.IndexOf('\t');
                        if (eq > 0) prefs[line.Substring(0, eq)] = line.Substring(eq + 1);
                    }
                return prefs;
            }
        }

        public static bool HasKey(string key) => Isolated ? Prefs.ContainsKey(key) : PlayerPrefs.HasKey(key);
        public static string GetString(string key, string fallback = "") => Isolated ? (Prefs.TryGetValue(key, out var v) ? v : fallback) : PlayerPrefs.GetString(key, fallback);

        public static void SetString(string key, string value)
        {
            if (!Isolated) { PlayerPrefs.SetString(key, value); return; }
            Prefs[key] = value.Replace('\n', ' ');
            Save();
        }

        public static void DeleteKey(string key)
        {
            if (!Isolated) { PlayerPrefs.DeleteKey(key); return; }
            if (Prefs.Remove(key)) Save();
        }

        public static void Save()
        {
            if (!Isolated) { PlayerPrefs.Save(); return; }
            var lines = new List<string>();
            foreach (var kv in Prefs) lines.Add(kv.Key + "\t" + kv.Value);
            File.WriteAllLines(PrefsFile, lines);
        }
    }
}
