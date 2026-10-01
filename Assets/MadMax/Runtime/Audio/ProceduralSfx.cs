using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>Sound effects synthesised at first use for keys that have no recorded clip in Resources/Sfx: distinct gun
    /// reports (pistol crack, rifle crack with a long tail, machine-gun rattle, bow twang), footsteps per surface (sand,
    /// road, gravel, mud, snow, wood, metal, water) and hooves, night insects, structure creaks and collapses, the gyro's rotor
    /// chop and the stall horn, and the working loops of crafting stations (`station_*`: sizzle, roar, churn, bubble,
    /// clack, grind, saw, hum) plus the anvil ring. <see cref="Sfx.Clip"/> falls back to <see cref="Make"/>.</summary>
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
                case "starter": d = Starter(rnd); break;
                case "engine_catch": d = Catch(rnd); break;
                case "sputter": d = Sputter(rnd); break;
                case "station_sizzle": d = Seamless(Sizzle(rnd)); break;
                case "station_roar": d = Seamless(Roar(rnd)); break;
                case "station_churn": d = Seamless(Churn(rnd)); break;
                case "station_bubble": d = Seamless(Bubble(rnd)); break;
                case "station_clack": d = Seamless(Clack(rnd)); break;
                case "station_grind": d = Seamless(Grind(rnd)); break;
                case "station_saw": d = Seamless(Saw(rnd)); break;
                case "station_hum": d = Seamless(Hum(rnd)); break;
                case "anvil": d = Ring(rnd, 0.6f, 1180f, 2930f, 0.16f); break;
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

        /// <summary>The starter motor: a whining gear with the engine's compression strokes dragging it (loop).</summary>
        static float[] Starter(System.Random r)
        {
            int n = (int)(0.6f * Rate);
            var x = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float chug = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 6.67f * t);                     // four compressions in 0.6 s
                ph += (180f + 60f * chug) / Rate;
                float whine = (ph % 1f) * 2f - 1f;
                x[i] = whine * 0.45f * chug + Noise(r) * 0.25f * chug;
            }
            LowPass(x, 1800f);
            Normalize(x, 0.55f);
            return x;
        }

        /// <summary>It caught: a few ragged firing pulses rising into a short revving roar.</summary>
        static float[] Catch(System.Random r)
        {
            int n = (int)(1.1f * Rate);
            var x = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(12f, 55f, Mathf.Clamp01(t / 0.35f)) * (t > 0.35f ? Mathf.Lerp(1f, 0.6f, (t - 0.35f) / 0.75f) : 1f);   // rev up, settle
                ph += f / Rate;
                float pulse = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(2f * Mathf.PI * ph)), 6f);
                x[i] = (pulse * 1.2f + Noise(r) * 0.2f * pulse) * Mathf.Clamp01((1.1f - t) / 0.3f);
            }
            LowPass(x, 900f);
            Normalize(x, 0.8f);
            return x;
        }

        /// <summary>It didn't: two or three coughs and the starter winding down.</summary>
        static float[] Sputter(System.Random r)
        {
            int n = (int)(0.8f * Rate);
            var x = new float[n];
            foreach (float at in new[] { 0.05f, 0.25f, 0.5f })
            {
                int s0 = (int)(at * Rate);
                for (int i = s0; i < n; i++) { float t = (i - s0) / (float)Rate; x[i] += (Noise(r) * 0.8f + Mathf.Sin(2f * Mathf.PI * 70f * t)) * Mathf.Exp(-t / 0.05f); }
            }
            LowPass(x, 700f);
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

        // ---- crafting station loops (2 s, ends cross-faded so they loop without a click) ----

        /// <summary>Cross-fades the last 0.25 s into the start and drops it, so the clip loops seamlessly.</summary>
        static float[] Seamless(float[] x)
        {
            int f = Mathf.Min(x.Length / 4, (int)(0.25f * Rate)), n = x.Length - f;
            var y = new float[n];
            System.Array.Copy(x, y, n);
            for (int i = 0; i < f; i++) { float w = i / (float)f; y[i] = x[i] * w + x[n + i] * (1f - w); }
            return y;
        }

        /// <summary>Fat in a pan: dense random crackles over a hiss.</summary>
        static float[] Sizzle(System.Random r)
        {
            int n = (int)(2.25f * Rate);
            var x = new float[n];
            for (int i = 0; i < n; i++) x[i] = Noise(r) * 0.12f;
            for (int k = 0; k < 260; k++)
            {
                int s0 = r.Next(n), len = (int)((0.002f + (float)r.NextDouble() * 0.006f) * Rate);
                float a = 0.3f + (float)r.NextDouble() * 0.7f;
                for (int i = s0; i < Mathf.Min(n, s0 + len); i++) x[i] += Noise(r) * a * (1f - (i - s0) / (float)len);
            }
            HighPass(x, 2500f);
            Normalize(x, 0.4f);
            return x;
        }

        /// <summary>A furnace or forge fire: low turbulent rumble with a slow breathing swell.</summary>
        static float[] Roar(System.Random r)
        {
            int n = (int)(2.25f * Rate);
            var x = new float[n];
            for (int i = 0; i < n; i++) x[i] = Noise(r);
            LowPass(x, 260f); LowPass(x, 400f);
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; x[i] *= 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * t / 2.25f * 2f); }
            Normalize(x, 0.55f);
            return x;
        }

        /// <summary>A drum mixer / wash plant: a slow tumbling slosh with grit knocks, once per half turn.</summary>
        static float[] Churn(System.Random r)
        {
            int n = (int)(2.25f * Rate);
            var x = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; x[i] = Noise(r) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 0.5f)) * 0.5f; }
            LowPass(x, 700f);
            for (int k = 0; k < 40; k++)
            {
                int s0 = r.Next(n);
                for (int i = s0; i < Mathf.Min(n, s0 + 400); i++) { float t = (i - s0) / (float)Rate; x[i] += Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t / 0.008f) * 0.4f; }
            }
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; x[i] += Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.12f; }
            Normalize(x, 0.5f);
            return x;
        }

        /// <summary>A still or a vat: rising bubbles that pop with an upward chirp.</summary>
        static float[] Bubble(System.Random r)
        {
            int n = (int)(2.25f * Rate);
            var x = new float[n];
            for (int k = 0; k < 26; k++)
            {
                int s0 = r.Next(n), len = (int)(0.05f * Rate);
                float f0 = 300f + (float)r.NextDouble() * 500f, a = 0.4f + (float)r.NextDouble() * 0.6f, ph = 0f;
                for (int i = 0; i < len && s0 + i < n; i++)
                {
                    float t = i / (float)Rate;
                    ph += f0 * (1f + t * 30f) / Rate;
                    x[s0 + i] += Mathf.Sin(2f * Mathf.PI * ph) * a * Mathf.Exp(-t / 0.015f);
                }
            }
            for (int i = 0; i < n; i++) x[i] += Noise(r) * 0.02f;
            LowPass(x, 3000f);
            Normalize(x, 0.4f);
            return x;
        }

        /// <summary>A treadle sewing machine, loom or wheel: a regular wooden clack with a soft whir.</summary>
        static float[] Clack(System.Random r)
        {
            int n = (int)(2.25f * Rate);
            var x = new float[n];
            for (int i = 0; i < n; i++) x[i] = Noise(r) * 0.05f;
            LowPass(x, 1200f);
            const float period = 0.25f;
            for (float t0 = 0f; t0 < 2.25f; t0 += period)
            {
                int s0 = (int)(t0 * Rate);
                float a = 0.7f + (float)r.NextDouble() * 0.3f;
                for (int i = s0; i < Mathf.Min(n, s0 + 1200); i++) { float t = (i - s0) / (float)Rate; x[i] += (Mathf.Sin(2f * Mathf.PI * 950f * t) * 0.6f + Noise(r) * 0.5f) * a * Mathf.Exp(-t / 0.006f); }
            }
            Normalize(x, 0.45f);
            return x;
        }

        /// <summary>A crusher, stamp mill or lathe: motor drone with rough, beating grit.</summary>
        static float[] Grind(System.Random r)
        {
            int n = (int)(2.25f * Rate);
            var x = new float[n]; var g = new float[n];
            for (int i = 0; i < n; i++) g[i] = Noise(r);
            LowPass(g, 1800f); HighPass(g, 300f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float motor = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.15f;
                x[i] = motor + g[i] * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 4f * t));
            }
            Normalize(x, 0.5f);
            return x;
        }

        /// <summary>A circular saw biting wood: a high whine that dips under load, with sawdust hiss.</summary>
        static float[] Saw(System.Random r)
        {
            int n = (int)(2.25f * Rate);
            var x = new float[n]; var h = new float[n];
            for (int i = 0; i < n; i++) h[i] = Noise(r);
            HighPass(h, 3000f);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, load = Mathf.Clamp01(Mathf.Sin(Mathf.PI * t / 1f));
                ph += (1400f - 260f * load) / Rate;
                x[i] = (Mathf.Sin(2f * Mathf.PI * ph) * 0.4f + Mathf.Sin(4f * Mathf.PI * ph) * 0.15f) * (0.6f + 0.4f * load) + h[i] * 0.35f * load;
            }
            Normalize(x, 0.4f);
            return x;
        }

        /// <summary>Powered machinery idling along: transformer hum and a fan.</summary>
        static float[] Hum(System.Random r)
        {
            int n = (int)(2.25f * Rate);
            var x = new float[n];
            for (int i = 0; i < n; i++) x[i] = Noise(r) * 0.25f;
            LowPass(x, 900f);
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; x[i] += Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.1f; }
            Normalize(x, 0.4f);
            return x;
        }
    }
}
