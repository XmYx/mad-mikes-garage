using MadMax.Audio;
using MadMax.Building;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S15: Hollis Reed's pipe organ, dressed onto the stage lamp that hangs over its music stand (that lamp's
    /// power node is the organ's supply: the blower draws <see cref="BlowerWatts"/> on it as a second load while the quest
    /// runs). A voxel case with a keyboard and a rank of pipes (three gaps until the missing pipes are fitted), and a
    /// procedural organ voice: [T] plays a chord when it has wind; the recital plays its chords through
    /// <see cref="Chord"/>.</summary>
    public class S15Organ : MonoBehaviour, IInteractable
    {
        public const float BlowerWatts = 900f, Drop = 2.4f;
        static Mesh full, gapped;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { full = null; gapped = null; }
        UtilityNode node;
        MeshFilter mf;
        bool fitted;
        SynthVoice voice;
        readonly OrganSynth synth = new OrganSynth();
        float chordUntil;

        public bool Powered => node && node.Powered;

        void Awake()
        {
            node = GetComponent<UtilityNode>();
            var go = new GameObject("Organ", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -Drop, 0f);
            mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            var g = WastelandGame.Instance;
            if (g) mr.sharedMaterial = g.propMaterial;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.3f, -0.1f); box.size = new Vector3(2.5f, 2.6f, 0.9f);
            Refresh();
        }

        /// <summary>Show the full rank once the pipes are fitted.</summary>
        public void Refresh()
        {
            fitted = MadMax.Story.Story.StepDone("S15", "fit") || MadMax.Story.Story.StateOf("S15") == MadMax.Story.Story.State.Done;
            if (!full) full = VoxelMesher.Build(Grid(false), "S15Organ");
            if (!gapped) gapped = VoxelMesher.Build(Grid(true), "S15OrganGapped");
            if (mf) mf.sharedMesh = fitted ? full : gapped;
        }

        void Update()
        {
            bool running = MadMax.Story.Story.StateOf("S15") == MadMax.Story.Story.State.Active;
            if (node) node.auxDemand = running ? BlowerWatts : 0f;
            if (fitted != (MadMax.Story.Story.StepDone("S15", "fit") || MadMax.Story.Story.StateOf("S15") == MadMax.Story.Story.State.Done)) Refresh();
            if (voice && Time.time > chordUntil) { synth.Release(); if (synth.Silent) voice.SetActive(false); }
            if (voice && !Powered) synth.Release();
        }

        /// <summary>Hold a chord (MIDI note numbers) for <paramref name="seconds"/>; silent without wind.</summary>
        public void Chord(float seconds, params int[] notes)
        {
            if (!Powered) return;
            if (!voice) voice = SynthVoice.Create(transform, "OrganVoice", new Vector3(0f, -Drop + 1.8f, 0f), synth, 70f);
            synth.Play(notes, fitted ? 0f : 0.35f);
            voice.gain = 0.5f;
            voice.SetActive(true);
            chordUntil = Time.time + seconds;
        }

        public string Prompt(WastelandGame g) => Powered ? "HOLLIS'S ORGAN  [T] PLAY A CHORD" : "HOLLIS'S ORGAN: THE BLOWER HAS NO POWER";

        public void Use(WastelandGame g, bool secondary)
        {
            if (!secondary) return;
            if (!Powered) { g.Toast("NO WIND: THE ORGAN'S BLOWER NEEDS POWER"); return; }
            Chord(2.5f, 62, 66, 69, 74);
            if (!fitted) g.Toast("A CHORD WITH THREE HOLES IN IT: THE MISSING PIPES WHEEZE");
        }

        /// <summary>A wooden case with a keyboard shelf and music stand, a rank of 13 chrome pipes over it (tallest in the
        /// middle), gold mouths; three of them absent until fitted.</summary>
        static VoxelGrid Grid(bool gaps)
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Wood);
            g.Box(-15, 0, -6, 15, 13, 3, Pal.Weathered(Pal.Wood, 0.12f, 5101, 2, 0));                    // case
            g.Box(-16, 14, -7, 16, 15, 4, Pal.Ramp(Pal.Wood, 1, 5102));                                   // cornice
            g.Box(-13, 9, 4, 13, 10, 8, Pal.Ramp(Pal.Wood, 2, 5103));                                     // keyboard shelf
            g.Box(-12, 11, 4, 12, 11, 7, p => p.x % 2 == 0 ? Pal.Cream[4] : Pal.Black[1]);                 // keys
            g.Box(-6, 12, 3, 6, 17, 3, Pal.Ramp(Pal.Wood, 3, 5104));                                      // music stand
            g.Box(-12, 0, 5, -11, 8, 7, Pal.Ramp(Pal.Wood, 1, 5105)); g.Box(11, 0, 5, 12, 8, 7, Pal.Ramp(Pal.Wood, 1, 5106));   // shelf legs
            foreach (int x in new[] { -9, -6, -3, 0, 3, 6, 9 }) g.Box(x, 1, 8, x + 1, 1, 10, Pal.Solid(Pal.Wood[3]));            // pedals
            g.Mat((byte)MadMax.Items.ResourceType.Scrap);
            int[] missing = { 3, 7, 10 };
            for (int i = 0; i < 13; i++)
            {
                if (gaps && System.Array.IndexOf(missing, i) >= 0) continue;
                int x = -12 + i * 2;
                int top = 16 + 16 - Mathf.Abs(i - 6) * 2;
                g.CylY(x, -2f, 0.9f, 16, top, Pal.Weathered(Pal.Chrome, 0.08f, 5110 + i, 2, 0));
                g.Box(x, 19, 0, x, 20, 0, Pal.Solid(Pal.Ochre[3]));                                          // mouth
            }
            g.Bevel();
            return g;
        }
    }

    /// <summary>A small additive organ: up to four notes, each a fundamental with its octave and twelfth (8', 4', 2 2/3'),
    /// a slow swell and release; a wheeze of wind noise when pipes are missing. Allocation-free on the audio thread.</summary>
    public class OrganSynth : ISynth
    {
        readonly float[] freq = new float[4], phase = new float[12];
        int count;
        float level, target, wheeze;
        uint noise = 22222;
        volatile bool silent = true;

        public bool Silent => silent;

        public void Play(int[] midi, float wheezeAmount)
        {
            count = Mathf.Min(4, midi.Length);
            for (int i = 0; i < count; i++) freq[i] = 440f * Mathf.Pow(2f, (midi[i] - 69) / 12f);
            wheeze = wheezeAmount;
            target = 1f; silent = false;
        }

        public void Release() => target = 0f;

        public void Render(float[] mono, int frames, int sampleRate)
        {
            float dt = 1f / sampleRate;
            for (int n = 0; n < frames; n++)
            {
                level += (target - level) * (target > level ? 3f : 1.5f) * dt;
                float s = 0f;
                for (int i = 0; i < count; i++)
                {
                    for (int h = 0; h < 3; h++)
                    {
                        int k = i * 3 + h;
                        float mul = h == 0 ? 1f : h == 1 ? 2f : 3f;
                        phase[k] += freq[i] * mul * dt;
                        if (phase[k] > 1f) phase[k] -= 1f;
                        s += Mathf.Sin(phase[k] * 6.2831853f) * (h == 0 ? 0.5f : h == 1 ? 0.3f : 0.18f);
                    }
                }
                noise = noise * 1664525u + 1013904223u;
                s += ((noise >> 9) / 8388608f - 1f) * wheeze * 0.25f;
                mono[n] = s * level * 0.18f;
            }
            if (target <= 0f && level < 0.001f) silent = true;
        }
    }
}
