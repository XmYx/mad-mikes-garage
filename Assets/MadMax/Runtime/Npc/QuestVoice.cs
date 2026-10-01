using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Voiced story lines (audio/quest_cast → StreamingAssets/Voices/Quests: Ogg clips + manifest.json). Every
    /// line a cast member speaks — Talk-topic replies, quest offers, performance beats — and the radio voices (the
    /// opening loop, June's broadcast) is rendered offline with that character's own reference voice. A clip is found by
    /// <see cref="Id"/> = FNV-1a 64 of <c>castKey + "\n" + Norm(text)</c>: editing a line's text silences it until the
    /// pipeline renders it again (quest_cast/extract.py + gen.py), and dynamic lines (names, numbers) stay silent.
    /// Playback goes through <see cref="NpcVoice"/> (3D source at the speaker's head, VOICE volume); captions are the
    /// game's own text and stay as they are.</summary>
    public static class QuestVoice
    {
        [Serializable] class Manifest { public Entry[] lines; }
        [Serializable] class Entry { public string id, key, file, text; public float secs; }

        static Dictionary<string, Entry> byId;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { byId = null; loaded = false; }

        /// <summary>Number of voiced lines in the manifest (0 when it is missing).</summary>
        public static int Count { get { Load(); return byId != null ? byId.Count : 0; } }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Voices", "Quests", "manifest.json");
            if (!System.IO.File.Exists(path)) return;
            try
            {
                var m = JsonUtility.FromJson<Manifest>(System.IO.File.ReadAllText(path));
                byId = new Dictionary<string, Entry>();
                if (m != null && m.lines != null) foreach (var e in m.lines) if (e != null && e.id != null) byId[e.id] = e;
            }
            catch (Exception e) { Debug.LogWarning("QuestVoice: " + e.Message); byId = null; }
        }

        /// <summary>How the pipeline compares texts: upper case, whitespace runs collapsed, trimmed.</summary>
        public static string Norm(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder(text.Length);
            bool space = false;
            foreach (char ch in text.ToUpperInvariant())
            {
                if (char.IsWhiteSpace(ch)) { space = sb.Length > 0; continue; }
                if (space) { sb.Append(' '); space = false; }
                sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>The line id: FNV-1a 64 over the UTF-8 bytes of key + "\n" + Norm(text), 16 hex digits.</summary>
        public static string Id(string key, string text)
        {
            ulong h = 0xcbf29ce484222325UL;
            foreach (byte b in System.Text.Encoding.UTF8.GetBytes(key + "\n" + Norm(text))) { h ^= b; h = unchecked(h * 0x100000001b3UL); }
            return h.ToString("x16");
        }

        /// <summary>The clip for this line (path under StreamingAssets/Voices), or null.</summary>
        public static string FileFor(string key, string text, out float secs)
        {
            secs = 0f;
            Load();
            if (byId == null || key == null || string.IsNullOrEmpty(text)) return null;
            if (!byId.TryGetValue(Id(key, text), out var e)) return null;
            secs = e.secs;
            return "Quests/" + e.file;
        }

        public static bool Has(string key, string text) => FileFor(key, text, out _) != null;

        /// <summary>A cast member says a line in a conversation (stops when the Talk page closes). False = no clip.</summary>
        public static bool Talk(Npc npc, string key, string text) => Speak(npc, key, text, true);

        /// <summary>A cast member says a scripted line (a performance beat); it plays to the end.</summary>
        public static bool Say(Npc npc, string key, string text) => Speak(npc, key, text, false);

        static bool Speak(Npc npc, string key, string text, bool talk)
        {
            if (!npc || !NpcVoice.Instance) return false;
            string file = FileFor(key, text, out _);
            if (file == null) return false;
            NpcVoice.Instance.PlayStory(npc, file, talk);
            return true;
        }

        /// <summary>A voice over the radio (no speaker in the world): "radio_mara" (the opening loop), "radio_june" (the
        /// broadcast). Returns the clip length in seconds (0 = no clip).</summary>
        public static float Radio(string key, string text)
        {
            if (!NpcVoice.Instance) return 0f;
            string file = FileFor(key, text, out float secs);
            if (file == null) return 0f;
            NpcVoice.Instance.PlayRadio(file);
            return Mathf.Max(secs, 0.1f);
        }

        public static void Stop() { if (NpcVoice.Instance) NpcVoice.Instance.StopStory(); }
    }
}
