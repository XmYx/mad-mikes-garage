using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>Sound effects from Resources/Sfx (CC0, see CREDITS.txt). One-shots are pooled 3D voices and are skipped
    /// beyond their range from the listener. Loops are requests (<see cref="Loop"/> every frame); <see cref="Tick"/> gives
    /// the few loudest-at-the-listener requests a voice and silences the rest, so a burning forest costs 12 voices, not 300.
    /// Volume follows <c>GameSettings.sfxVolume</c>.</summary>
    public static class Sfx
    {
        const int PoolSize = 24, MaxLoops = 12, MaxRequests = 512;

        struct Request { public Component owner; public string key; public float vol, pitch, range, score; public bool flat; public Vector3 pos; }
        class LoopVoice { public AudioSource src; public Component owner; public string key; public bool used; }

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        static readonly Request[] requests = new Request[MaxRequests];
        static readonly int[] chosen = new int[MaxLoops];
        static readonly LoopVoice[] loopVoices = new LoopVoice[MaxLoops];
        static int requestCount;
        static AudioSource[] pool;
        static Transform root;
        static int next;

        public static Vector3 ListenerPosition { get; private set; }
        public static bool HasListener { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            clips.Clear(); lastPlayed.Clear(); requestCount = 0; pool = null; root = null; next = 0; HasListener = false;
            for (int i = 0; i < MaxLoops; i++) loopVoices[i] = null;
        }

        static float Master => MadMax.Game.GameSettings.Current.sfxVolume;
        static bool Muted => Application.isBatchMode;

        public static AudioClip Clip(string key)
        {
            if (clips.TryGetValue(key, out var c) && c) return c;
            c = Resources.Load<AudioClip>("Sfx/" + key);
            clips[key] = c;
            return c;
        }

        static AudioSource NewSource(string name)
        {
            var s = new GameObject(name).AddComponent<AudioSource>();
            s.transform.SetParent(root, false);
            s.playOnAwake = false; s.dopplerLevel = 0f; s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 2f;
            return s;
        }

        static void EnsureRoot()
        {
            if (root && pool != null) return;
            root = new GameObject("Sfx").transform;
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++) pool[i] = NewSource("OneShot");
            for (int i = 0; i < MaxLoops; i++) { var s = NewSource("Loop"); s.loop = true; loopVoices[i] = new LoopVoice { src = s }; }
        }

        static AudioSource Next()
        {
            EnsureRoot();
            for (int i = 0; i < PoolSize; i++) { var s = pool[(next + i) % PoolSize]; if (!s.isPlaying) { next = (next + i + 1) % PoolSize; return s; } }
            var steal = pool[next]; next = (next + 1) % PoolSize; return steal;
        }

        /// <summary>3D one-shot; skipped beyond range. minInterval drops repeats of the same key closer together (seconds).</summary>
        public static void Play(string key, Vector3 pos, float volume = 1f, float pitch = 1f, float range = 40f, float minInterval = 0.05f)
        {
            if (Muted || volume <= 0.001f) return;
            if (HasListener && (pos - ListenerPosition).sqrMagnitude > range * range) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(key, out var t) && now - t < minInterval) return;
            var clip = Clip(key);
            if (!clip) return;
            lastPlayed[key] = now;
            var s = Next();
            s.transform.position = pos;
            s.spatialBlend = 1f; s.maxDistance = range;
            s.clip = clip; s.pitch = pitch; s.volume = Mathf.Clamp01(volume) * Master;
            s.Play();
        }

        /// <summary>Non-positional one-shot (UI).</summary>
        public static void Play2D(string key, float volume = 1f, float pitch = 1f)
        {
            if (Muted) return;
            var clip = Clip(key);
            if (!clip) return;
            var s = Next();
            s.spatialBlend = 0f; s.clip = clip; s.pitch = pitch; s.volume = Mathf.Clamp01(volume) * Master;
            s.Play();
        }

        /// <summary>Requests a looping sound on the owner for this frame (call every frame; volume 0 = none).
        /// flat = non-positional ambience.</summary>
        public static void Loop(Component owner, string key, float volume, float pitch = 1f, float range = 30f, bool flat = false)
        {
            if (Muted || !owner || volume <= 0.001f || requestCount >= MaxRequests) return;
            var pos = owner.transform.position;
            float score = volume;
            if (!flat)
            {
                if (!HasListener) return;
                float d = Vector3.Distance(pos, ListenerPosition);
                if (d >= range) return;
                score *= 1f - d / range;
            }
            requests[requestCount++] = new Request { owner = owner, key = key, vol = volume, pitch = pitch, range = range, flat = flat, pos = pos, score = score };
        }

        /// <summary>Called once per frame after the listener moved (RadioNetwork.LateUpdate).</summary>
        public static void Tick(Vector3 listener)
        {
            ListenerPosition = listener; HasListener = true;
            if (Muted) { requestCount = 0; return; }
            EnsureRoot();
            // pick the loudest requests (partial selection)
            int picked = 0;
            for (int k = 0; k < MaxLoops && k < requestCount; k++)
            {
                int best = -1; float bs = 0f;
                for (int i = 0; i < requestCount; i++)
                {
                    if (requests[i].score <= bs) continue;
                    bool taken = false;
                    for (int j = 0; j < picked; j++) if (chosen[j] == i) { taken = true; break; }
                    if (!taken) { best = i; bs = requests[i].score; }
                }
                if (best < 0) break;
                chosen[picked++] = best;
            }
            foreach (var v in loopVoices) v.used = false;
            // keep voices that already play a chosen request, then hand free voices to the rest
            for (int pass = 0; pass < 2; pass++)
                for (int j = 0; j < picked; j++)
                {
                    ref var r = ref requests[chosen[j]];
                    if (r.owner == null) continue;
                    LoopVoice voice = null;
                    foreach (var v in loopVoices)
                    {
                        if (v.used) continue;
                        if (pass == 0 ? (v.owner == r.owner && v.key == r.key) : !IsClaimed(v, picked)) { voice = v; break; }
                    }
                    if (voice == null) continue;
                    voice.used = true;
                    var s = voice.src;
                    if (voice.owner != r.owner || voice.key != r.key || !s.isPlaying)
                    {
                        var clip = Clip(r.key);
                        if (!clip) { voice.used = false; continue; }
                        voice.owner = r.owner; voice.key = r.key;
                        s.clip = clip; s.time = Random.Range(0f, clip.length * 0.9f); s.Play();
                    }
                    s.transform.position = r.pos;
                    s.spatialBlend = r.flat ? 0f : 1f; s.maxDistance = r.range;
                    s.volume = Mathf.Clamp01(r.vol) * Master; s.pitch = r.pitch;
                    r.owner = null;   // served
                }
            foreach (var v in loopVoices) if (!v.used) { if (v.src.isPlaying) v.src.Stop(); v.owner = null; v.key = null; }
            requestCount = 0;
        }

        static bool IsClaimed(LoopVoice v, int picked)
        {
            if (v.owner == null) return false;
            for (int j = 0; j < picked; j++) { ref var r = ref requests[chosen[j]]; if (r.owner == v.owner && r.key == v.key) return true; }
            return false;
        }
    }
}
