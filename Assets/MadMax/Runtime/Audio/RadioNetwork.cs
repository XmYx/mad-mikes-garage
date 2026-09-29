using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace MadMax.Audio
{
    [Serializable] public class RadioFile { public string file, title, kind, cond; public float len; }
    [Serializable] public class RadioStationDef
    {
        public string id, freq, name, dj, tagline, borrow;
        public bool talk;
        public List<RadioFile> tracks = new List<RadioFile>(), links = new List<RadioFile>(), shows = new List<RadioFile>();
    }
    [Serializable] public class RadioManifest { public List<RadioStationDef> stations = new List<RadioStationDef>(); public List<RadioFile> ads = new List<RadioFile>(); }
    [Serializable] public class RadioCaption { public string file; public List<string> lines = new List<string>(); }
    [Serializable] public class RadioCaptions { public List<RadioCaption> files = new List<RadioCaption>(); }

    /// <summary>All stations broadcast continuously on a shared clock: each station builds a deterministic programme
    /// (songs, DJ links, jingles, ad breaks; talk shows by time of day and weather) and receivers join mid-item.
    /// Audio is loaded on demand from StreamingAssets/Radio (Ogg Vorbis, kept compressed) with a small cache.
    /// Also owns the scene's AudioListener, which follows the player or the driven vehicle instead of the far camera.</summary>
    public class RadioNetwork : MonoBehaviour
    {
        public struct Item { public RadioFile file; public double start; public double End => start + file.len; }

        class Programme
        {
            public RadioStationDef def;
            public System.Random rng;
            public readonly List<Item> items = new List<Item>();
            public readonly Queue<RadioFile> recent = new Queue<RadioFile>();
            public int count;
        }

        public static RadioNetwork Instance { get; private set; }
        public static RadioManifest Manifest { get; private set; }
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> lastUse = new Dictionary<string, float>();
        static readonly HashSet<string> loading = new HashSet<string>();
        static readonly RadioFile Silence = new RadioFile { title = "", kind = "silence", len = 20f };
        const int CacheSize = 10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Manifest = null; cache.Clear(); lastUse.Clear(); loading.Clear(); captions = null; }

        // spoken lines per clip (captions.json from audio/export_captions.py), for the RADIO CAPTIONS setting
        static Dictionary<string, List<string>> captions;

        /// <summary>The caption line being spoken <paramref name="offset"/> seconds into <paramref name="file"/> (null: music,
        /// or no script). Lines share the clip's length by their length.</summary>
        public static string Caption(RadioFile file, float offset)
        {
            if (file == null || string.IsNullOrEmpty(file.file)) return null;
            if (captions == null)
            {
                captions = new Dictionary<string, List<string>>();
                string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Radio", "captions.json");
                if (System.IO.File.Exists(path))
                {
                    var all = JsonUtility.FromJson<RadioCaptions>(System.IO.File.ReadAllText(path));
                    if (all != null) foreach (var c in all.files) captions[c.file] = c.lines;
                }
            }
            if (!captions.TryGetValue(file.file, out var lines) || lines.Count == 0) return null;
            int total = 0; foreach (var l in lines) total += l.Length;
            float at = Mathf.Clamp01(offset / Mathf.Max(0.1f, file.len)) * total;
            foreach (var l in lines) { if (at <= l.Length) return l; at -= l.Length; }
            return lines[lines.Count - 1];
        }

        public int StationCount => Manifest != null ? Manifest.stations.Count : 0;
        public RadioStationDef Station(int i) => Manifest.stations[Wrap(i)];
        public int Wrap(int i) => StationCount == 0 ? 0 : ((i % StationCount) + StationCount) % StationCount;
        /// <summary>Broadcast clock (real seconds): stations keep playing while nobody listens and while paused.</summary>
        public static double Clock => Time.realtimeSinceStartupAsDouble + 3600.0;

        readonly List<Programme> programmes = new List<Programme>();
        Transform listener;

        void Awake()
        {
            Instance = this;
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Radio", "manifest.json");
            if (Manifest == null && System.IO.File.Exists(path)) Manifest = JsonUtility.FromJson<RadioManifest>(System.IO.File.ReadAllText(path));
            if (Manifest == null) { Debug.LogWarning("Radio manifest missing: " + path); Manifest = new RadioManifest(); }
            foreach (var st in Manifest.stations) programmes.Add(new Programme { def = st, rng = new System.Random(Hash(st.id)) });
            if (!Application.isBatchMode)
            {
                foreach (var l in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude)) l.enabled = false;
                listener = new GameObject("AudioListener").AddComponent<AudioListener>().transform;
                listener.SetParent(transform, false);
            }
        }

        static int Hash(string s) { int h = 17; foreach (char c in s) h = h * 31 + c; return h; }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void LateUpdate()
        {
            if (!listener) return;
            var g = MadMax.Game.WastelandGame.Instance;
            Transform target = g && g.Current ? g.Current.transform : g && g.Player ? g.Player.transform : null;
            var cam = Camera.main;
            if (target) listener.position = target.position + Vector3.up * 1.2f;
            else if (cam) listener.position = cam.transform.position;
            if (cam) { var f = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up); if (f.sqrMagnitude > 1e-4f) listener.rotation = Quaternion.LookRotation(f); }
            Sfx.Tick(listener.position);
        }

        /// <summary>Item on air at the current clock and the offset into it (seconds).</summary>
        public Item Now(int station, out float offset)
        {
            var p = programmes[Wrap(station)];
            double now = Clock;
            if (p.items.Count == 0) Append(p, now - p.rng.NextDouble() * 120.0);
            while (p.items[p.items.Count - 1].End <= now) Append(p, p.items[p.items.Count - 1].End);
            while (p.items.Count > 1 && p.items[0].End <= now) p.items.RemoveAt(0);
            var it = p.items[0];
            offset = (float)(now - it.start);
            return it;
        }

        /// <summary>Item after the one on air (for pre-loading).</summary>
        public Item Next(int station)
        {
            Now(station, out _);
            var p = programmes[Wrap(station)];
            if (p.items.Count < 2) Append(p, p.items[p.items.Count - 1].End);
            return p.items[1];
        }

        /// <summary>Schedules one programme block starting at t.</summary>
        void Append(Programme p, double t)
        {
            int before = p.items.Count;
            void Add(RadioFile f) { if (f == null || f.len <= 0f) return; p.items.Add(new Item { file = f, start = t }); t += f.len; }
            var st = p.def;
            int n = p.count++;
            if (st.talk)
            {
                if (n % 6 == 0) Add(Pick(p, st.shows, "id"));
                string kind = TalkKind(n);
                if (kind == "weather") Add(WeatherReport(p, st));
                else if (kind == "music") Add(Fresh(p, Borrowed(st)));
                else Add(Pick(p, st.shows, kind));
                if (n % 3 == 2) { Add(Pick(p, Manifest.ads, null)); Add(Pick(p, Manifest.ads, null)); }
            }
            else
            {
                if (n % 5 == 0) Add(Pick(p, st.tracks, "jingle"));
                Add(Fresh(p, st.tracks));
                if (p.rng.NextDouble() < 0.4) Add(Pick(p, st.links, null));
                if (n % 4 == 3) { Add(Pick(p, Manifest.ads, null)); Add(Pick(p, Manifest.ads, null)); }
            }
            if (p.items.Count == before) Add(Silence);
        }

        static string TalkKind(int n)
        {
            float h = MadMax.World.DayNight.Hours;
            if (n % 4 == 1) return (n / 4) % 2 == 0 ? "news" : "weather";
            if (n % 5 == 4) return "music";
            if (h >= 5f && h < 11f) return "morning";
            if (h >= 11f && h < 14f) return "garden";
            if (h >= 14f && h < 21f) return "callin";
            return "night";
        }

        RadioFile WeatherReport(Programme p, RadioStationDef st)
        {
            string cond = MadMax.World.Weather.Snowing ? "snow" : MadMax.World.Weather.Raining ? "rain" : MadMax.World.DayNight.Darkness > 0.5f ? "night" : "dry";
            foreach (var f in st.shows) if (f.kind == "weather" && f.cond == cond) return f;
            return Pick(p, st.shows, "weather");
        }

        static List<RadioFile> Borrowed(RadioStationDef st)
        {
            foreach (var s in Manifest.stations) if (s.id == st.borrow) return s.tracks;
            return st.tracks;
        }

        static RadioFile Pick(Programme p, List<RadioFile> list, string kind)
        {
            int count = 0;
            foreach (var f in list) if (kind == null || f.kind == kind) count++;
            if (count == 0) return null;
            int k = p.rng.Next(count);
            foreach (var f in list) if ((kind == null || f.kind == kind) && k-- == 0) return f;
            return null;
        }

        /// <summary>A song or instrumental not among the station's recent plays.</summary>
        static RadioFile Fresh(Programme p, List<RadioFile> list)
        {
            RadioFile pick = null;
            for (int tries = 0; tries < 16 && list.Count > 0; tries++)
            {
                var f = list[p.rng.Next(list.Count)];
                if (f.kind == "jingle") continue;
                pick = f;
                if (!p.recent.Contains(f)) break;
            }
            if (pick != null) { p.recent.Enqueue(pick); while (p.recent.Count > Mathf.Max(1, list.Count / 2)) p.recent.Dequeue(); }
            return pick;
        }

        /// <summary>Clip for a file if loaded; starts loading it otherwise.</summary>
        public AudioClip Clip(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            if (cache.TryGetValue(file, out var c))
            {
                if (c) { lastUse[file] = Time.unscaledTime; return c; }
                cache.Remove(file);
            }
            if (loading.Add(file)) StartCoroutine(Load(file));
            return null;
        }

        IEnumerator Load(string file)
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Radio", file);
            string uri = path.Contains("://") ? path : "file://" + path;
            using (var req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS))
            {
                ((DownloadHandlerAudioClip)req.downloadHandler).compressed = true;
                yield return req.SendWebRequest();
                loading.Remove(file);
                if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning("Radio: " + file + " " + req.error); yield break; }
                var clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = file;
                cache[file] = clip; lastUse[file] = Time.unscaledTime;
            }
            Trim();
        }

        void Trim()
        {
            while (cache.Count > CacheSize)
            {
                string oldest = null; float t = float.MaxValue;
                foreach (var kv in cache)
                {
                    if (RadioReceiver.Uses(kv.Key)) continue;
                    float u = lastUse.TryGetValue(kv.Key, out var v) ? v : 0f;
                    if (u < t) { t = u; oldest = kv.Key; }
                }
                if (oldest == null) return;
                if (cache[oldest]) Destroy(cache[oldest]);
                cache.Remove(oldest); lastUse.Remove(oldest);
            }
        }
    }
}
