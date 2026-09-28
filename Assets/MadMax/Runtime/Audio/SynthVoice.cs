using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>A procedural sound generator, rendered on the audio thread. Must not allocate.</summary>
    public interface ISynth
    {
        void Render(float[] mono, int frames, int sampleRate);
    }

    /// <summary>Plays an <see cref="ISynth"/> through a normal 3D AudioSource: the source loops a clip of ones and the
    /// synth signal is multiplied in, so distance attenuation, panning and the mixer apply as for any clip.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class SynthVoice : MonoBehaviour
    {
        public ISynth synth;
        public float gain = 1f;
        public AudioSource Source { get; private set; }

        static AudioClip ones;
        static int sampleRate;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { ones = null; sampleRate = 0; }

        readonly float[] mono = new float[8192];
        float appliedGain;

        public static SynthVoice Create(Transform parent, string name, Vector3 localPos, ISynth synth, float range)
        {
            if (!ones)
            {
                sampleRate = AudioSettings.outputSampleRate;
                ones = AudioClip.Create("SynthOnes", 4096, 1, sampleRate, false);
                var d = new float[4096];
                for (int i = 0; i < d.Length; i++) d[i] = 1f;
                ones.SetData(d, 0);
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var src = go.AddComponent<AudioSource>();
            src.clip = ones; src.loop = true; src.playOnAwake = false; src.spatialBlend = 1f; src.dopplerLevel = 0f;
            src.rolloffMode = AudioRolloffMode.Logarithmic; src.minDistance = 3f; src.maxDistance = range;
            var v = go.AddComponent<SynthVoice>();
            v.Source = src; v.synth = synth;
            return v;
        }

        public void SetActive(bool on)
        {
            if (!Source) return;
            if (on && !Source.isPlaying) Source.Play();
            else if (!on && Source.isPlaying) Source.Stop();
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            var s = synth;
            int frames = data.Length / channels;
            if (s == null || frames > mono.Length) { System.Array.Clear(data, 0, data.Length); return; }
            s.Render(mono, frames, sampleRate > 0 ? sampleRate : 48000);
            float g0 = appliedGain, g1 = gain, step = (g1 - g0) / frames;   // ramp gain changes to avoid zipper noise
            for (int i = 0, k = 0; i < frames; i++)
            {
                float v = mono[i] * (g0 + step * i);
                for (int c = 0; c < channels; c++, k++) data[k] *= v;
            }
            appliedGain = g1;
        }
    }
}
