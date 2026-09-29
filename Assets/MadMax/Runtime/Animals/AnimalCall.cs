using MadMax.Audio;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>Procedural animal voices (roadmap 23), rendered through a <see cref="SynthVoice"/>: a pitched tone
    /// with harmonics and vibrato gliding from f0 to f1, mixed with noise and optionally chopped into pulses —
    /// barks, growls, howls, neighs and snorts, moos, bleats, grunts, clucks, squeaks, screeches, a rattle.</summary>
    public class AnimalCall : ISynth
    {
        public struct Call
        {
            public float f0, f1, dur, vib, vibDepth, noise, pulses, harm;
            public Call(float f0, float f1, float dur, float noise, float harm = 0.5f, float pulses = 0f, float vib = 0f, float vibDepth = 0f)
            { this.f0 = f0; this.f1 = f1; this.dur = dur; this.noise = noise; this.harm = harm; this.pulses = pulses; this.vib = vib; this.vibDepth = vibDepth; }
        }

        volatile int pending;
        Call next, cur;
        float t, phase, lp;
        bool active;
        uint rng = 0x9e3779b9;

        public bool Busy => active || pending != 0;
        public void Play(Call c) { next = c; pending = 1; }

        public void Render(float[] mono, int frames, int sampleRate)
        {
            if (pending != 0) { cur = next; pending = 0; t = 0f; active = true; }
            float dt = 1f / sampleRate;
            for (int i = 0; i < frames; i++)
            {
                if (!active) { mono[i] = 0f; continue; }
                float u = t / cur.dur;
                if (u >= 1f) { active = false; mono[i] = 0f; continue; }
                float f = Mathf.Lerp(cur.f0, cur.f1, u) * (1f + Mathf.Sin(t * cur.vib * 6.2832f) * cur.vibDepth);
                phase += f * dt;
                if (phase > 1f) phase -= 1f;
                float tone = Mathf.Sin(phase * 6.2832f) + cur.harm * (Mathf.Sin(phase * 12.566f) * 0.5f + Mathf.Sin(phase * 18.85f) * 0.25f);
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float n = (rng & 0xffff) / 32768f - 1f;
                lp += (n - lp) * Mathf.Clamp01(f * dt * 6f);                                        // noise coloured by the pitch
                float s = tone * (1f - cur.noise) + lp * cur.noise * 2.2f;
                float env = Mathf.Min(1f, u * 30f) * Mathf.Min(1f, (1f - u) * 8f);
                if (cur.pulses > 0f)
                {
                    float pu = u * cur.pulses, fr = pu - Mathf.Floor(pu);
                    env *= fr < 0.55f ? Mathf.Sin(fr / 0.55f * Mathf.PI) : 0f;
                }
                mono[i] = s * env * 0.45f;
                t += dt;
            }
        }

        /// <summary>A species' call: 0 = everyday (bark, neigh, moo, cluck), 1 = alarm / anger (growl, snort, squeal), 2 = long (howl).</summary>
        public static Call For(AnimalDef d, int kind, float size)
        {
            float p = Mathf.Clamp(1f / Mathf.Max(0.5f, size), 0.8f, 1.6f);                          // young ones sound higher
            switch (d.id)
            {
                case "dog": case "guarddog":
                    return kind == 1 ? new Call(95f, 70f, 0.9f, 0.6f, 1f, 0f, 25f, 0.15f) : new Call(520f * p, 330f * p, 0.55f, 0.45f, 0.8f, 2f);
                case "wolf":
                    return kind == 2 ? new Call(330f, 470f, 2.4f, 0.05f, 0.3f, 0f, 5f, 0.02f) : kind == 1 ? new Call(85f, 65f, 1f, 0.6f, 1f, 0f, 22f, 0.15f) : new Call(480f, 320f, 0.5f, 0.45f, 0.8f, 2f);
                case "horse":
                    return kind == 1 ? new Call(160f, 100f, 0.4f, 0.9f, 0.2f) : new Call(900f * p, 480f * p, 1.1f, 0.15f, 0.9f, 0f, 14f, 0.08f);
                case "antelope": return new Call(700f, 420f, 0.25f, 0.5f, 0.6f);
                case "boar": case "radboar":
                    return kind == 1 ? new Call(1100f, 650f, 0.6f, 0.3f, 0.6f, 0f, 20f, 0.05f) : new Call(115f, 90f, 0.6f, 0.55f, 1f, 3f);
                case "cow": return new Call(115f * p, 140f * p, 1.6f, 0.05f, 1f, 0f, 4f, 0.02f);
                case "goat": return new Call(380f * p, 330f * p, 0.8f, 0.1f, 0.9f, 0f, 9f, 0.12f);
                case "pig": return new Call(160f * p, 120f * p, 0.5f, 0.4f, 1f, 2f);
                case "chicken": return new Call(850f * p, 700f * p, 0.5f, 0.2f, 0.6f, 3f);
                case "rat": return new Call(3200f, 2600f, 0.25f, 0.1f, 0.3f, 2f);
                case "vulture": return new Call(1300f, 900f, 0.6f, 0.7f, 0.5f);
                case "snake": case "cottonmouth": case "python": return new Call(60f, 60f, 1.4f, 1f, 0f, 45f);
                case "coyote": return kind == 1 ? new Call(420f, 300f, 0.5f, 0.5f, 0.8f, 3f) : new Call(700f, 1100f, 1.6f, 0.1f, 0.4f, 4f, 8f, 0.1f);   // yips and a howl
                case "deer": return new Call(900f, 600f, 0.3f, 0.6f, 0.4f);
                case "bear": return kind == 1 ? new Call(70f, 55f, 1.4f, 0.7f, 1f, 0f, 18f, 0.2f) : new Call(110f, 80f, 0.8f, 0.6f, 1f, 2f);
                case "jackrabbit": case "armadillo": return new Call(2200f, 1800f, 0.15f, 0.4f, 0.2f);
                case "raccoon": return new Call(1500f, 900f, 0.4f, 0.4f, 0.5f, 3f);
                case "crow": return new Call(1100f, 800f, 0.35f, 0.6f, 0.6f, 1f);
                case "turkey": return new Call(700f, 500f, 0.8f, 0.2f, 0.7f, 8f);
                case "radlizard": case "gila": return new Call(90f, 60f, 1f, 0.95f, 0.2f);                          // a hiss
                case "radscorpion": case "cavespider": case "radroach": return new Call(3000f, 2400f, 0.4f, 0.9f, 0.1f, 12f);   // chitter
                default: return new Call(400f, 300f, 0.4f, 0.3f);
            }
        }
    }
}
