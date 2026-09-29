using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>Sound effects synthesised at first use for keys that have no recorded clip in Resources/Sfx: distinct gun
    /// reports (pistol crack, rifle crack with a long tail, machine-gun rattle, bow twang), footsteps per surface (sand,
    /// road, gravel, mud, snow, wood, metal, water) and hooves, night insects, structure creaks and collapses, the gyro's rotor
    /// chop and the stall horn. <see cref="Sfx.Clip"/> falls back to <see cref="Make"/>.</summary>
    public static class ProceduralSfx
    {
        const int Rate = 22050;

        /// <summary>A generated clip for <paramref name="key"/>, or null when there is no recipe.</summary>
        public static AudioClip Make(string key)
        {
            var rnd = new System.Random(key.GetHashCode());
            float[] d;
            switch (key)
            {
                case "shot_pistol": d = Shot(rnd, 0.45f, 0.028f, 0.09f, 170f, 0.9f); break;
                case "shot_rifle": d = Shot(rnd, 0.9f, 0.018f, 0.32f, 120f, 1f); break;
                case "shot_mg": d = Shot(rnd, 0.3f, 0.02f, 0.07f, 140f, 0.85f); break;
                case "bow": d = Twang(rnd); break;
                case "step_sand": d = Step(rnd, 0.1f, 900f, 0.03f, 1, 0f); break;
                case "step_road": d = Step(rnd, 0.08f, 2200f, 0.008f, 1, 160f); break;
                case "step_gravel": d = Step(rnd, 0.14f, 3200f, 0.012f, 4, 0f); break;
                case "step_mud": d = Squelch(rnd); break;
                case "step_snow": d = Step(rnd, 0.16f, 5000f, 0.01f, 6, 0f); break;
                case "step_wood": d = Step(rnd, 0.12f, 1400f, 0.02f, 1, 110f); break;
                case "step_metal": d = Ring(rnd, 0.3f, 880f, 1370f, 0.09f); break;
                case "step_water": d = Splash(rnd, 0.25f); break;
                case "hoof": d = Hoof(rnd); break;
                case "insects": d = Insects(rnd, 4f); break;
                case "creak": d = Creak(); break;
                case "collapse": d = Rumble(rnd, 1.6f); break;
                case "rotor": d = Chop(rnd); break;
                case "beep": d = Horn(); break;
                default: return null;
            }
            var clip = AudioClip.Create("Synth_" + key, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static float Noise(System.Random r) => (float)r.NextDouble() * 2f - 1f;

        /// <summary>One-pole low-pass in place.</summary>
        static void LowPass(float[] x, float cutoff)
        {
            float a = Mathf.Exp(-2f * Mathf.PI * cutoff / Rate), y = 0f;
            for (int i = 0; i < x.Length; i++) { y = (1f - a) * x[i] + a * y; x[i] = y; }
        }

        static void HighPass(float[] x, float cutoff)
        {
            float a = Mathf.Exp(-2f * Mathf.PI * cutoff / Rate), lp = 0f;
            for (int i = 0; i < x.Length; i++) { lp = (1f - a) * x[i] + a * lp; x[i] -= lp; }
        }

        static void Normalize(float[] x, float peak)
        {
            float m = 0f; foreach (var v in x) m = Mathf.Max(m, Mathf.Abs(v));
            if (m <= 0f) return;
            for (int i = 0; i < x.Length; i++) x[i] *= peak / m;
        }

        /// <summary>A gunshot: bright crack, a thump of body, a filtered rolling tail.</summary>
        static float[] Shot(System.Random r, float len, float crackT, float tailT, float thumpHz, float peak)
        {
            int n = (int)(len * Rate);
            var crack = new float[n]; var tail = new float[n]; var outp = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; float nz = Noise(r); crack[i] = nz * Mathf.Exp(-t / crackT); tail[i] = nz * Mathf.Exp(-t / tailT); }
            HighPass(crack, 1500f); LowPass(tail, 700f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t * (1f - t * 1.5f)) * Mathf.Exp(-t / 0.05f);
                outp[i] = crack[i] * 1.2f + tail[i] * 0.9f + thump * 0.8f;
            }
            Normalize(outp, peak);
            return outp;
        }

        static float[] Twang(System.Random r)
        {
            int n = (int)(0.5f * Rate);
            var x = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = 190f * (1f + 0.04f * Mathf.Sin(t * 40f));
                x[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * f * t)) * Mathf.Exp(-t / 0.13f) + Noise(r) * Mathf.Exp(-t / 0.01f) * 0.6f;
            }
            Normalize(x, 0.8f);
            return x;
        }

        /// <summary>A footfall: <paramref name="grains"/> short noise bursts (crunch) through a low-pass, plus an optional thud.</summary>
        static float[] Step(System.Random r, float len, float cutoff, float grainT, int grains, float thudHz)
        {
            int n = (int)(len * Rate);
            var x = new float[n];
            for (int g = 0; g < grains; g++)
            {
                int s0 = (int)((float)r.NextDouble() * n * 0.5f);
                float amp = 0.5f + (float)r.NextDouble() * 0.5f;
                for (int i = s0; i < n; i++) { float t = (i - s0) / (float)Rate; x[i] += Noise(r) * amp * Mathf.Exp(-t / grainT); }
            }
            LowPass(x, cutoff);
            if (thudHz > 0f) for (int i = 0; i < n; i++) { float t = i / (float)Rate; x[i] += Mathf.Sin(2f * Mathf.PI * thudHz * t) * Mathf.Exp(-t / 0.03f) * 0.8f; }
            Normalize(x, 0.6f);
            return x;
        }

        static float[] Squelch(System.Random r)
        {
            int n = (int)(0.22f * Rate);
            var x = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, f = Mathf.Lerp(320f, 110f, t / 0.22f);
                ph += f / Rate;
                x[i] = (Mathf.Sin(2f * Mathf.PI * ph) * 0.7f + Noise(r) * 0.3f) * Mathf.Exp(-t / 0.07f) * Mathf.Clamp01(t / 0.01f);
            }
            LowPass(x, 900f);
            Normalize(x, 0.55f);
            return x;
        }

        static float[] Ring(System.Random r, float len, float f1, float f2, float decay)
        {
            int n = (int)(len * Rate);
            var x = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                x[i] = (Mathf.Sin(2f * Mathf.PI * f1 * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * f2 * t)) * Mathf.Exp(-t / decay) + Noise(r) * Mathf.Exp(-t / 0.006f);
            }
            Normalize(x, 0.5f);
            return x;
        }

        static float[] Splash(System.Random r, float len)
        {
            int n = (int)(len * Rate);
            var x = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; x[i] = Noise(r) * Mathf.Exp(-t / 0.08f) * (0.6f + 0.4f * Mathf.Sin(t * 180f)); }
            HighPass(x, 500f); LowPass(x, 3500f);
            Normalize(x, 0.55f);
            return x;
        }

        static float[] Hoof(System.Random r)
        {
            int n = (int)(0.2f * Rate);
            var x = new float[n];
            foreach (float at in new[] { 0f, 0.07f })
            {
                int s0 = (int)(at * Rate);
                for (int i = s0; i < n; i++)
                {
                    float t = (i - s0) / (float)Rate;
                    x[i] += Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t / 0.035f) + Noise(r) * Mathf.Exp(-t / 0.004f) * 0.8f;
                }
            }
            LowPass(x, 2500f);
            Normalize(x, 0.7f);
            return x;
        }

        /// <summary>Crickets: trains of short chirps near 4.5 kHz from a few "insects", loopable.</summary>
        static float[] Insects(System.Random r, float len)
        {
            int n = (int)(len * Rate);
            var x = new float[n];
            for (int bug = 0; bug < 5; bug++)
            {
                float f = 3800f + (float)r.NextDouble() * 1600f, period = 0.35f + (float)r.NextDouble() * 0.4f, offset = (float)r.NextDouble() * period;
                float amp = 0.3f + (float)r.NextDouble() * 0.4f;
                for (float t0 = offset; t0 < len - 0.1f; t0 += period)
                    for (int pulse = 0; pulse < 3; pulse++)
                    {
                        int s0 = (int)((t0 + pulse * 0.025f) * Rate), s1 = Mathf.Min(n, s0 + (int)(0.016f * Rate));
                        for (int i = s0; i < s1; i++) { float t = (i - s0) / (float)Rate; x[i] += Mathf.Sin(2f * Mathf.PI * f * t) * amp * Mathf.Sin(Mathf.PI * t / 0.016f); }
                    }
            }
            Normalize(x, 0.35f);
            return x;
        }

        static float[] Creak()
        {
            int n = (int)(0.9f * Rate);
            var x = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, f = 170f + 90f * t + 25f * Mathf.PerlinNoise(t * 30f, 0.5f);
                ph += f / Rate;
                float saw = (ph % 1f) * 2f - 1f;
                x[i] = saw * Mathf.PerlinNoise(t * 60f, 3.3f) * Mathf.Sin(Mathf.PI * t / 0.9f);
            }
            LowPass(x, 1200f);
            Normalize(x, 0.5f);
            return x;
        }

        static float[] Rumble(System.Random r, float len)
        {
            int n = (int)(len * Rate);
            var x = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; x[i] = Noise(r) * Mathf.Exp(-t / 0.6f) * Mathf.Clamp01(t / 0.05f); }
            LowPass(x, 160f);
            var clicks = new float[n];
            for (int k = 0; k < 40; k++)
            {
                int s0 = (int)((float)r.NextDouble() * n * 0.8f);
                for (int i = s0; i < Mathf.Min(n, s0 + 400); i++) clicks[i] += Noise(r) * Mathf.Exp(-(i - s0) / 60f) * 0.5f;
            }
            HighPass(clicks, 800f);
            for (int i = 0; i < n; i++) x[i] = x[i] * 3f + clicks[i];
            Normalize(x, 0.9f);
            return x;
        }

        /// <summary>Two blade slaps in half a second (the loop's pitch follows the rotor speed).</summary>
        static float[] Chop(System.Random r)
        {
            int n = (int)(0.5f * Rate);
            var x = new float[n];
            foreach (float at in new[] { 0f, 0.25f })
            {
                int s0 = (int)(at * Rate);
                for (int i = s0; i < n; i++) { float t = (i - s0) / (float)Rate; x[i] += Noise(r) * Mathf.Exp(-t / 0.045f); }
            }
            LowPass(x, 500f);
            Normalize(x, 0.6f);
            return x;
        }

        /// <summary>Stall horn: a buzzing tone, a quarter second on, a quarter off (loop).</summary>
        static float[] Horn()
        {
            int n = (int)(0.5f * Rate);
            var x = new float[n];
            for (int i = 0; i < n / 2; i++) { float t = i / (float)Rate; x[i] = (Mathf.Sin(2f * Mathf.PI * 620f * t) > 0f ? 0.5f : -0.5f) * Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((0.25f - t) / 0.01f); }
            LowPass(x, 2400f);
            return x;
        }
    }
}
