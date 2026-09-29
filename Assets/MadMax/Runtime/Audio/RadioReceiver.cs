using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>A radio (vehicle head unit or placed set): plays whatever its station has on air right now,
    /// with a band-limited speaker sound, tuning static between stations and 3D falloff. Heard in 2D from the driver's seat.</summary>
    public class RadioReceiver : MonoBehaviour
    {
        public static readonly List<RadioReceiver> All = new List<RadioReceiver>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { All.Clear(); noise = null; }

        public bool on;
        public int station;
        /// <summary>0..1 reception here (distance to the nearest radio mast).</summary>
        public float Signal { get; private set; } = 1f;
        float signalCheck;
        [Range(0f, 1f)] public float volume = 0.7f;
        public float range = 30f;
        public float lowPass = 7000f;       // speaker bandwidth
        public Vector3 speaker = new Vector3(0f, 1f, 0.5f);

        /// <summary>Title shown on the HUD when it changes (item or station), with the time it changed.</summary>
        public string NowPlaying { get; private set; }
        public float ChangedAt { get; private set; } = -99f;

        AudioSource src, hiss;
        AudioLowPassFilter lp;
        string playingFile;
        float staticUntil;
        static AudioClip noise;

        /// <summary>The receiver on this object, created when missing (vehicles get their head unit on first use).</summary>
        public static RadioReceiver On(GameObject go) => go.TryGetComponent<RadioReceiver>(out var r) ? r : go.AddComponent<RadioReceiver>();

        public static bool Uses(string file) { foreach (var r in All) if (r && r.playingFile == file) return true; return false; }

        void OnEnable() => All.Add(this);
        void OnDisable() { All.Remove(this); if (src) src.Stop(); if (hiss) hiss.Stop(); playingFile = null; }

        void Build()
        {
            var go = new GameObject("Radio");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = speaker;
            src = go.AddComponent<AudioSource>();
            hiss = go.AddComponent<AudioSource>();
            foreach (var s in new[] { src, hiss })
            {
                s.playOnAwake = false; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 1.5f; s.maxDistance = range; s.dopplerLevel = 0f; s.spatialBlend = 1f;
            }
            hiss.loop = true;
            go.AddComponent<AudioHighPassFilter>().cutoffFrequency = 160f;
            lp = go.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = lowPass;
            if (!noise) noise = Resources.Load<AudioClip>("Sfx/static");
            hiss.clip = noise;
        }

        public void TogglePower()
        {
            on = !on;
            if (on) staticUntil = Time.unscaledTime + 0.35f;
            Changed();
            MadMax.Game.WastelandGame.Instance?.Toast(on ? StationLabel() : "RADIO OFF");
            GetComponent<MadMax.Building.Placeable>()?.Dirty();
        }

        public void Tune(int dir)
        {
            var net = RadioNetwork.Instance;
            if (!net || net.StationCount == 0) return;
            if (!on) on = true;
            station = net.Wrap(station + dir);
            staticUntil = Time.unscaledTime + 0.45f;
            playingFile = null;
            Changed();
            MadMax.Game.WastelandGame.Instance?.Toast(StationLabel());
            GetComponent<MadMax.Building.Placeable>()?.Dirty();
        }

        public void ChangeVolume(int dir)
        {
            volume = Mathf.Clamp01(Mathf.Round((volume + dir * 0.1f) * 10f) / 10f);
            MadMax.Game.WastelandGame.Instance?.Toast("RADIO VOLUME " + Mathf.RoundToInt(volume * 100f) + "%");
            GetComponent<MadMax.Building.Placeable>()?.Dirty();
        }

        /// <summary>M power, comma/period tune, brackets volume.</summary>
        public void HandleKeys(bool power, int tune, int vol)
        {
            if (power) TogglePower();
            if (tune != 0) Tune(tune);
            if (vol != 0) ChangeVolume(vol);
        }

        public string StationLabel()
        {
            var net = RadioNetwork.Instance;
            if (!net || net.StationCount == 0) return "NO SIGNAL";
            var s = net.Station(station);
            return s.freq + " " + s.name.ToUpperInvariant() + (Signal < 0.5f ? " (WEAK SIGNAL)" : "");
        }

        void Changed() { ChangedAt = Time.unscaledTime; }

        void Update()
        {
            var net = RadioNetwork.Instance;
            if (!on || !net || net.StationCount == 0 || Application.isBatchMode)
            {
                if (src && (src.isPlaying || hiss.isPlaying)) { src.Stop(); hiss.Stop(); playingFile = null; }
                return;
            }
            if (!src) Build();

            var game = MadMax.Game.WastelandGame.Instance;
            bool inside = game && game.Current && game.Current.gameObject == gameObject;
            float gain = volume * MadMax.Game.GameSettings.Current.radioVolume;
            src.spatialBlend = hiss.spatialBlend = inside ? 0f : 1f;
            lp.cutoffFrequency = inside ? Mathf.Max(lowPass, 9000f) : lowPass;

            var item = net.Now(station, out float offset);
            string file = item.file.file;
            string title = item.file.title;
            if (title != NowPlaying) { NowPlaying = title; if (!string.IsNullOrEmpty(title)) Changed(); }

            if (file != playingFile)
            {
                var clip = net.Clip(file);
                if (clip && clip.loadState == AudioDataLoadState.Loaded)
                {
                    src.clip = clip;
                    src.time = Mathf.Clamp(offset, 0f, clip.length - 0.05f);
                    src.Play();
                    playingFile = file;
                }
                else if (src.isPlaying && src.clip && src.clip.name != file) src.Stop();
                net.Clip(net.Next(station).file.file);   // pre-load
            }
            else if (src.clip && (!src.isPlaying || Mathf.Abs(src.time - offset) > 0.75f) && offset < src.clip.length - 0.1f)
            {
                src.time = Mathf.Clamp(offset, 0f, src.clip.length - 0.05f);   // resync after a hitch or a pause
                if (!src.isPlaying) src.Play();
            }
            if ((signalCheck -= Time.unscaledDeltaTime) <= 0f)
            {
                signalCheck = 1f;
                var w = MadMax.World.DeformableTerrain.Instance ? MadMax.World.DeformableTerrain.Instance.World : null;
                Signal = w != null ? MadMax.World.BiomeProps.Signal(w, transform.position) : 1f;
            }
            src.volume = gain * (0.35f + 0.65f * Signal);                                         // far from a mast the station fades into hiss

            bool tuning = Time.unscaledTime < staticUntil || (file != null && playingFile != file);
            if (hiss.clip)
            {
                hiss.volume = gain * (tuning ? 0.35f : 0.015f + (1f - Signal) * 0.22f);
                if (!hiss.isPlaying) { hiss.time = Random.Range(0f, hiss.clip.length * 0.8f); hiss.Play(); }
            }
        }

        public string SaveState() => (on ? "1" : "0") + "," + station + "," + volume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(',');
            on = p[0] == "1";
            if (p.Length > 1) int.TryParse(p[1], out station);
            if (p.Length > 2) float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out volume);
        }
    }
}
