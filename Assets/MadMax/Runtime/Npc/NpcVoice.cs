using System;
using System.Collections;
using System.Collections.Generic;
using MadMax.Game;
using UnityEngine;
using UnityEngine.Networking;

namespace MadMax.Npc
{
    /// <summary>Spoken NPC lines (user additions): Southern wasteland barks and NPC-to-NPC exchanges rendered offline
    /// (audio/gen_npc_voices.py → StreamingAssets/Voices: Ogg clips + manifest.json). The cast is ten characters with
    /// their own lines (no two share one); an NPC gets the character matching its gender and temperament, a slice of
    /// that character's lines (NPCs sharing a character mostly say different things), never repeats itself until the
    /// slice runs dry, and a slight pitch of its own. It speaks through a 3D source at the head with a caption bubble
    /// (<see cref="Captions"/>, drawn by the HUD). Triggers: greeting, idle muttering, bumped, near misses and hits by cars, fights (spotted, taunts,
    /// hurt, flee, surrender), conversation and trade reactions (<see cref="Say"/>), and chats between two idle NPCs
    /// near the player. Volume = the VOICE channel. Without the manifest everything stays silent.</summary>
    public class NpcVoice : MonoBehaviour
    {
        [Serializable] class Manifest { public VoiceInfo[] voices; public Line[] lines; public Exchange[] exchanges; }
        [Serializable] class VoiceInfo { public string id; public bool female; public string[] tempers; }
        [Serializable] class Line { public string id, cat, temper, role, cond, text; public string[] voices; public int bucket = -1, buckets = 1; }
        [Serializable] class Exchange { public string id, ctx; public string[] lines, pairs; }

        public struct Caption { public Transform at; public string text; public float until; }
        /// <summary>Lines being spoken right now (HUD bubbles).</summary>
        public static readonly List<Caption> Captions = new List<Caption>();
        public static NpcVoice Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Captions.Clear(); Instance = null; }

        const float Range = 30f;
        Manifest manifest;
        readonly Dictionary<string, List<Line>> byCat = new Dictionary<string, List<Line>>();
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastUse = new Dictionary<string, float>();
        readonly HashSet<string> loading = new HashSet<string>();
        readonly Queue<string> recent = new Queue<string>();
        readonly Dictionary<Npc, float> quietUntil = new Dictionary<Npc, float>(), greeted = new Dictionary<Npc, float>(), idleAt = new Dictionary<Npc, float>();
        readonly Dictionary<Npc, Npc.Mode> lastMode = new Dictionary<Npc, Npc.Mode>();
        readonly Dictionary<Npc, float> lastHealth = new Dictionary<Npc, float>();
        readonly Dictionary<Npc, HashSet<string>> said = new Dictionary<Npc, HashSet<string>>();
        readonly List<AudioSource> sources = new List<AudioSource>();
        readonly Dictionary<AudioSource, Transform> follow = new Dictionary<AudioSource, Transform>();
        readonly List<Npc> nearby = new List<Npc>();
        float scan, chatAt, globalIdle;
        bool chatting;

        void Awake()
        {
            Instance = this;
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Voices", "manifest.json");
            if (!System.IO.File.Exists(path)) return;
            try { manifest = JsonUtility.FromJson<Manifest>(System.IO.File.ReadAllText(path)); }
            catch (Exception e) { Debug.LogWarning("NpcVoice: " + e.Message); manifest = null; return; }
            foreach (var l in manifest.lines)
            {
                if (!byCat.TryGetValue(l.cat, out var list)) byCat[l.cat] = list = new List<Line>();
                list.Add(l);
            }
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("NpcVoice" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 2.5f; s.maxDistance = Range;
                s.dopplerLevel = 0f; s.ignoreListenerPause = true;
                sources.Add(s);
            }
        }

        public bool Ready => manifest != null && manifest.voices != null && manifest.voices.Length > 0;

        /// <summary>The NPC's character: female voices for feminine first names (raiders are male), preferring the
        /// characters whose temperaments include the NPC's; the seed decides among them, so a person always sounds
        /// the same.</summary>
        public string VoiceOf(NpcProfile p)
        {
            bool female = NpcLore.Feminine(p.first) && !p.Raider;
            string temper = p.temper.ToString().ToLowerInvariant();
            int count = 0, matched = 0;
            foreach (var v in manifest.voices)
                if (v.female == female) { count++; if (v.tempers != null && Array.IndexOf(v.tempers, temper) >= 0) matched++; }
            bool byTemper = matched > 0;
            if (count == 0) { female = !female; byTemper = false; foreach (var v in manifest.voices) if (v.female == female) count++; }
            int n = byTemper ? matched : count;
            int k = (int)((uint)(p.seed * 2654435761u) % (uint)Mathf.Max(1, n)), i = 0;
            foreach (var v in manifest.voices)
            {
                if (v.female != female || byTemper && (v.tempers == null || Array.IndexOf(v.tempers, temper) < 0)) continue;
                if (i == k) return v.id;
                i++;
            }
            return manifest.voices[0].id;
        }

        static uint Hash(NpcProfile p) => (uint)(p.seed * 0x9E3779B1u) ^ 0x5bd1e995u;

        /// <summary>Speak a line of <paramref name="cat"/> (see audio/npc_cast) if one exists for this NPC's voice,
        /// temperament and role, it is in earshot and not talking already. <paramref name="force"/> skips the per-NPC
        /// quiet time (conversation replies).</summary>
        public static bool Say(Npc npc, string cat, bool force = false) => Instance && Instance.Speak(npc, cat, force);

        bool Speak(Npc npc, string cat, bool force)
        {
            if (!Ready || !npc || npc.Profile == null || !byCat.TryGetValue(cat, out var list)) return false;
            if (!npc.Alive) return false;
            if (!force && quietUntil.TryGetValue(npc, out var q) && Time.unscaledTime < q) return false;
            if (Talking(npc)) return false;
            var g = WastelandGame.Instance;
            if (!g || g.Player == null || Vector3.Distance(Listener(g), npc.transform.position) > Range) return false;
            var p = npc.Profile;
            string voice = VoiceOf(p), temper = p.temper.ToString().ToLowerInvariant();
            string cond = Condition();
            bool raider = p.Raider, raiderOnly = cat == "taunt" || cat == "spotted" || cat == "greet" || cat == "idle";
            uint slice = Hash(p);
            if (!said.TryGetValue(npc, out var mine)) said[npc] = mine = new HashSet<string>();
            // candidates: this character, this NPC's slice, raider lines for raiders; weather lines while it holds;
            // lines this NPC already said only once the rest are used up
            Line best = null;
            for (int pass = 0; pass < 2 && best == null; pass++)
            {
                int seen = 0; bool condHit = false;
                foreach (var l in list)
                {
                    if (l.voices == null || Array.IndexOf(l.voices, voice) < 0) continue;
                    if (!string.IsNullOrEmpty(l.temper) && l.temper != temper) continue;
                    if (l.buckets > 1 && l.bucket >= 0 && l.bucket != (int)(slice % (uint)l.buckets)) continue;
                    bool raiderLine = l.role == "raider";
                    if (raiderLine && !raider) continue;
                    if (raider && raiderOnly && !raiderLine) continue;
                    if (!string.IsNullOrEmpty(l.cond) && l.cond != cond) continue;
                    string key = voice + "/" + l.id;
                    if (recent.Contains(key) || pass == 0 && mine.Contains(cat + "|" + key)) continue;
                    bool isCond = !string.IsNullOrEmpty(l.cond);
                    if (condHit && !isCond) continue;
                    if (isCond && !condHit && UnityEngine.Random.value < 0.5f) { condHit = true; seen = 0; }   // talk about the weather
                    seen++;
                    if (UnityEngine.Random.Range(0, seen) == 0) best = l;                           // reservoir pick
                }
                if (best == null && pass == 0) mine.RemoveWhere(k => k.StartsWith(cat + "|"));          // slice used up: start over
            }
            if (best == null) return false;
            string file = voice + "/" + best.id;
            mine.Add(cat + "|" + file);
            recent.Enqueue(file); while (recent.Count > 24) recent.Dequeue();
            quietUntil[npc] = Time.unscaledTime + UnityEngine.Random.Range(5f, 9f);
            StartCoroutine(Play(npc, file + ".ogg", best.text, null));
            return true;
        }

        IEnumerator Play(Npc npc, string file, string text, Action done)
        {
            var clip = Clip(file);
            float wait = 0f;
            while (!clip && loading.Contains(file) && wait < 3f) { wait += Time.unscaledDeltaTime; yield return null; clip = Clip(file); }
            var src = clip && npc ? FreeSource() : null;
            if (!src) { done?.Invoke(); yield break; }
            var head = npc.Head ? npc.Head : npc.transform;
            follow[src] = head;
            src.transform.position = head.position;
            src.clip = clip;
            src.volume = Mathf.Clamp01(GameSettings.Current.voiceVolume) * 0.95f;
            src.pitch = npc.Profile != null ? 0.95f + (Hash(npc.Profile) >> 8 & 0xff) / 255f * 0.1f : 1f;   // everyone a little different
            src.Play();
            if (GameSettings.Current.voiceCaptions) Captions.Add(new Caption { at = head, text = text.ToUpperInvariant(), until = Time.unscaledTime + clip.length + 0.8f });
            float end = Time.unscaledTime + clip.length;
            while (Time.unscaledTime < end && src && src.isPlaying) yield return null;
            follow.Remove(src);
            done?.Invoke();
        }

        AudioSource FreeSource()
        {
            foreach (var s in sources) if (!s.isPlaying && !follow.ContainsKey(s)) return s;
            return null;
        }

        bool Talking(Npc npc)
        {
            var head = npc.Head ? npc.Head : npc.transform;
            foreach (var kv in follow) if (kv.Value == head) return true;
            return false;
        }

        static Vector3 Listener(WastelandGame g) => g.Current ? g.Current.transform.position : g.Player.transform.position;

        static string Condition()
        {
            if (MadMax.World.Storms.Dust > 0.3f) return "dust";
            if (MadMax.World.Weather.Raining) return MadMax.World.Weather.Snowing ? "snow" : "rain";
            if (MadMax.World.DayNight.Darkness > 0.55f) return "night";
            float t = MadMax.World.Weather.Temperature;
            return t < 2f ? "cold" : t > 33f ? "hot" : null;
        }

        AudioClip Clip(string file)
        {
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
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Voices", file);
            string uri = path.Contains("://") ? path : "file://" + path;
            using (var req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var clip = DownloadHandlerAudioClip.GetContent(req);
                    clip.name = file;
                    cache[file] = clip; lastUse[file] = Time.unscaledTime;
                }
                loading.Remove(file);
            }
            while (cache.Count > 48)
            {
                string oldest = null; float t = float.MaxValue;
                foreach (var kv in cache)
                {
                    bool playing = false;
                    foreach (var s in sources) if (s.clip == kv.Value && s.isPlaying) playing = true;
                    float u = lastUse.TryGetValue(kv.Key, out var v) ? v : 0f;
                    if (!playing && u < t) { t = u; oldest = kv.Key; }
                }
                if (oldest == null) break;
                if (cache[oldest]) Destroy(cache[oldest]);
                cache.Remove(oldest); lastUse.Remove(oldest);
            }
        }

        void LateUpdate()
        {
            foreach (var kv in follow) if (kv.Value && kv.Key) kv.Key.transform.position = kv.Value.position;
            for (int i = Captions.Count - 1; i >= 0; i--) if (!Captions[i].at || Time.unscaledTime > Captions[i].until) Captions.RemoveAt(i);
        }

        void Update()
        {
            if (!Ready) return;
            var g = WastelandGame.Instance;
            if (!g || !g.Ready || g.Player == null || (g.Menus && g.Menus.IsOpen)) return;
            if ((scan -= Time.deltaTime) > 0f) return;
            scan = 0.25f;
            var me = Listener(g);
            var car = g.Current;
            float carSpeed = car && car.Body ? car.Body.linearVelocity.magnitude : 0f;
            nearby.Clear();
            foreach (var n in Npc.All)
            {
                if (!n || n.Profile == null) continue;
                float d = Vector3.Distance(me, n.transform.position);
                if (d > Range) { lastMode.Remove(n); lastHealth.Remove(n); continue; }
                nearby.Add(n);
                // fights: mode changes and health drops
                var mode = n.mode;
                var was = lastMode.TryGetValue(n, out var w) ? w : mode;
                lastMode[n] = mode;
                float hp = n.Health;
                bool hurt = lastHealth.TryGetValue(n, out var hp0) && hp < hp0 - 1f;
                lastHealth[n] = hp;
                if (!n.Alive) continue;
                if (hurt) Say(n, "hurt", UnityEngine.Random.value < 0.5f);
                if (mode != was)
                {
                    if (mode == Npc.Mode.Fight && d < 40f) Say(n, "spotted", true);
                    else if (mode == Npc.Mode.Flee) Say(n, "flee", true);
                    else if (mode == Npc.Mode.Surrender) Say(n, "surrender", true);
                    continue;
                }
                if (mode == Npc.Mode.Fight) { if (UnityEngine.Random.value < 0.06f) Say(n, "taunt"); continue; }
                if (n.Hostile || !n.Available || n.companion) continue;
                // cars: near misses
                if (car && carSpeed > 7f && d < 4.5f && Vector3.Dot(car.Body.linearVelocity, n.transform.position - me) < 0f) { Say(n, "car_close"); continue; }
                if (car) continue;
                // on foot: walked into, greeted, muttering
                var pv = g.Player.transform.position;
                var flat = new Vector3(n.transform.position.x - pv.x, 0f, n.transform.position.z - pv.z);
                if (flat.magnitude < 0.8f && Mathf.Abs(n.transform.position.y - pv.y) < 1.2f && g.Player.Velocity.magnitude > 1f) { Say(n, "bumped", true); continue; }
                if (d < 6f && (!greeted.TryGetValue(n, out var gt) || Time.unscaledTime > gt))
                {
                    greeted[n] = Time.unscaledTime + 300f;
                    Say(n, "greet");
                    continue;
                }
                if (d < 12f && Time.unscaledTime > globalIdle)
                {
                    if (!idleAt.TryGetValue(n, out var it)) { idleAt[n] = Time.unscaledTime + UnityEngine.Random.Range(8f, 25f); continue; }
                    if (Time.unscaledTime > it)
                    {
                        idleAt[n] = Time.unscaledTime + UnityEngine.Random.Range(35f, 80f);
                        if (Say(n, "idle")) globalIdle = Time.unscaledTime + 7f;
                    }
                }
            }
            if (!chatting && Time.unscaledTime > chatAt) TryChat(g, me);
        }

        /// <summary>A car knocked this NPC down (from <see cref="Npc"/>'s run-over check).</summary>
        public static void CarHit(Npc n) { if (Instance && n && n.Alive) Instance.Speak(n, "car_hit", true); }

        /// <summary>Two idle NPCs close together near the player strike up one of the rendered exchanges for their pair
        /// of voices; they stop and face each other while it lasts.</summary>
        void TryChat(WastelandGame g, Vector3 me)
        {
            chatAt = Time.unscaledTime + UnityEngine.Random.Range(10f, 18f);
            if (manifest.exchanges == null || manifest.exchanges.Length == 0) return;
            for (int i = 0; i < nearby.Count; i++)
            {
                var a = nearby[i];
                if (!Idle(a) || Vector3.Distance(a.transform.position, me) > 20f) continue;
                for (int j = i + 1; j < nearby.Count; j++)
                {
                    var b = nearby[j];
                    if (!Idle(b) || a.Profile.Raider != b.Profile.Raider || Vector3.Distance(a.transform.position, b.transform.position) > 6f) continue;
                    string va = VoiceOf(a.Profile), vb = VoiceOf(b.Profile);
                    bool stranger = Vector3.Distance(me, (a.transform.position + b.transform.position) * 0.5f) < 10f && !g.Current;
                    bool camp = a.mode == Npc.Mode.Gather || b.mode == Npc.Mode.Gather;
                    bool town = a.Profile.role == NpcRole.Resident || a.Profile.role == NpcRole.Shopkeeper || b.Profile.role == NpcRole.Resident || b.Profile.role == NpcRole.Shopkeeper;
                    Exchange pick = null; bool swap = false; int seen = 0;
                    foreach (var x in manifest.exchanges)
                    {
                        if (a.Profile.Raider ? x.ctx != "raider" : x.ctx == "raider") continue;
                        if (x.ctx == "stranger" && !stranger || x.ctx == "camp" && !camp || x.ctx == "town" && !town) continue;
                        if (recent.Contains("x/" + x.id) || x.pairs == null) continue;
                        bool ab = Array.IndexOf(x.pairs, va + "|" + vb) >= 0, ba = Array.IndexOf(x.pairs, vb + "|" + va) >= 0;
                        if (!ab && !ba) continue;
                        seen++;
                        if (UnityEngine.Random.Range(0, seen) == 0) { pick = x; swap = !ab; }
                    }
                    if (pick == null) continue;
                    recent.Enqueue("x/" + pick.id); while (recent.Count > 24) recent.Dequeue();
                    StartCoroutine(Chat(pick, swap ? b : a, swap ? a : b));
                    return;
                }
            }
        }

        static bool Idle(Npc n) => n && n.Alive && n.Available && !n.Hostile && !n.companion && (n.mode == Npc.Mode.Stand || n.mode == Npc.Mode.Wander || n.mode == Npc.Mode.Gather);

        IEnumerator Chat(Exchange x, Npc a, Npc b)
        {
            chatting = true;
            string va = VoiceOf(a.Profile), vb = VoiceOf(b.Profile);
            for (int i = 0; i < x.lines.Length; i++)
            {
                if (!Idle(a) || !Idle(b)) break;
                var who = i % 2 == 0 ? a : b;
                a.Chat(b.transform.position, 6f); b.Chat(a.transform.position, 6f);
                bool done = false;
                StartCoroutine(Play(who, "x/" + x.id + "/" + va + "_" + vb + "_" + i + ".ogg", x.lines[i], () => done = true));
                while (!done) yield return null;
                quietUntil[who] = Time.unscaledTime + 6f;
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.25f, 0.6f));
            }
            chatting = false;
        }
    }
}
