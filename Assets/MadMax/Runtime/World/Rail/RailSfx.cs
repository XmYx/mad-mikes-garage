using UnityEngine;

namespace MadMax.World
{
    /// <summary>Railway sounds synthesised at first use (<see cref="MadMax.Audio.Sfx.Clip"/> falls back here): the
    /// double clack of a bogie over a rail joint, the seamless rolling rumble of steel wheels, the shriek of brake
    /// blocks, a two-tone air horn.</summary>
    public static class RailSfx
    {
        const int Rate = 22050;

        public static AudioClip Make(string key)
        {
            if (key == null || !key.StartsWith("rail_")) return null;
            var r = new System.Random(key.GetHashCode());
            float[] d;
            switch (key)
            {
                case "rail_clack": d = Clack(r); break;
                case "rail_roll": d = Roll(r); break;
                case "rail_squeal": d = Squeal(r); break;
                case "rail_horn": d = Horn(); break;
                case "rail_whistle": d = Whistle(r); break;
                case "rail_flange": d = Loop(Flange(r)); break;
                case "rail_scrape": d = Loop(Scrape(r)); break;
                default: return null;
            }
            var clip = AudioClip.Create("Synth_" + key, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static float N(System.Random r) => (float)r.NextDouble() * 2f - 1f;

        static void LowPass(float[] x, float cutoff) { float a = Mathf.Exp(-2f * Mathf.PI * cutoff / Rate), y = 0f; for (int i = 0; i < x.Length; i++) { y = (1f - a) * x[i] + a * y; x[i] = y; } }

        static void Normalize(float[] x, float peak) { float m = 0f; foreach (var v in x) m = Mathf.Max(m, Mathf.Abs(v)); if (m > 0f) for (int i = 0; i < x.Length; i++) x[i] *= peak / m; }

        /// <summary>Two knocks 0.11 s apart: a dull thump with a ringing steel overtone.</summary>
        static float[] Clack(System.Random r)
        {
            int n = (int)(0.45f * Rate);
            var o = new float[n];
            foreach (float at in new[] { 0f, 0.11f })
            {
                int s0 = (int)(at * Rate);
                for (int i = s0; i < n; i++)
                {
                    float t = (i - s0) / (float)Rate;
                    o[i] += (Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.9f + N(r) * 0.6f) * Mathf.Exp(-t / 0.025f) + Mathf.Sin(2f * Mathf.PI * 1830f * t) * 0.18f * Mathf.Exp(-t / 0.06f);
                }
            }
            LowPass(o, 3200f);
            Normalize(o, 0.9f);
            return o;
        }

        /// <summary>Low rumble with a faint flange whine; loops seamlessly (cross-faded ends).</summary>
        static float[] Roll(System.Random r)
        {
            int n = 2 * Rate;
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = N(r);
            LowPass(o, 180f); LowPass(o, 260f);
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; o[i] += Mathf.Sin(2f * Mathf.PI * 41f * t) * 0.05f + Mathf.Sin(2f * Mathf.PI * 2200f * t) * 0.006f; }
            int fade = Rate / 4;
            for (int i = 0; i < fade; i++) { float w = i / (float)fade; o[i] = o[i] * w + o[n - fade + i] * (1f - w); }
            System.Array.Resize(ref o, n - fade);
            Normalize(o, 0.8f);
            return o;
        }

        /// <summary>Brake shriek: a wavering high partial pair over grinding noise; loops.</summary>
        static float[] Squeal(System.Random r)
        {
            int n = Rate;
            var o = new float[n]; var g = new float[n];
            for (int i = 0; i < n; i++) g[i] = N(r);
            LowPass(g, 1400f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = 2650f + Mathf.Sin(2f * Mathf.PI * 3f * t) * 60f;
                o[i] = Mathf.Sin(2f * Mathf.PI * f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * f * 1.51f * t) * 0.12f + g[i] * 0.35f;
            }
            Normalize(o, 0.7f);
            return o;
        }

        /// <summary>A two-tone air horn chord (311 + 370 Hz) with a reedy edge, 1.4 s.</summary>
        static float[] Horn()
        {
            int n = (int)(1.4f * Rate);
            var o = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 0.06f) * Mathf.Clamp01((1.4f - t) / 0.15f);
                float a = 0f;
                foreach (float f in new[] { 311f, 370f, 466f })
                    for (int h = 1; h <= 5; h++) a += Mathf.Sin(2f * Mathf.PI * f * h * t) / (h * 1.4f);
                o[i] = Mathf.Clamp(a * 0.4f, -1f, 1f) * env;
            }
            LowPass(o, 2600f);
            Normalize(o, 0.9f);
            return o;
        }

        /// <summary>Cross-fade the last quarter second into the start so the clip loops.</summary>
        static float[] Loop(float[] o)
        {
            int n = o.Length, fade = Rate / 4;
            for (int i = 0; i < fade; i++) { float w = i / (float)fade; o[i] = o[i] * w + o[n - fade + i] * (1f - w); }
            System.Array.Resize(ref o, n - fade);
            return o;
        }

        /// <summary>The guard's pea whistle: two blasts, the pea trilling the ~2.9 kHz tone, breath noise on top.</summary>
        static float[] Whistle(System.Random r)
        {
            int n = (int)(1.15f * Rate);
            var o = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = t < 0.32f ? Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((0.32f - t) / 0.03f)
                          : t > 0.42f ? Mathf.Clamp01((t - 0.42f) / 0.02f) * Mathf.Clamp01((1.12f - t) / 0.08f) : 0f;
                float trill = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 34f * t);
                ph += 2f * Mathf.PI * (2900f + 90f * Mathf.Sin(2f * Mathf.PI * 34f * t)) / Rate;
                o[i] = (Mathf.Sin(ph) * trill + N(r) * 0.12f) * env;
            }
            Normalize(o, 0.8f);
            return o;
        }

        /// <summary>Flanges on the rail head in a curve: a thin wandering screech (3.6-4.4 kHz) with scrape, 2 s.</summary>
        static float[] Flange(System.Random r)
        {
            int n = 2 * Rate + Rate / 4;
            var o = new float[n]; var g = new float[n];
            for (int i = 0; i < n; i++) g[i] = N(r);
            LowPass(g, 2600f);
            float ph = 0f, ph2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = 4000f + Mathf.Sin(2f * Mathf.PI * 0.9f * t) * 260f + Mathf.Sin(2f * Mathf.PI * 7.3f * t) * 70f;
                ph += 2f * Mathf.PI * f / Rate; ph2 += 2f * Mathf.PI * f * 1.33f / Rate;
                float amp = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 1.7f * t + 1f);
                o[i] = (Mathf.Sin(ph) * 0.3f + Mathf.Sin(ph2) * 0.1f) * amp + g[i] * 0.22f;
            }
            Normalize(o, 0.6f);
            return o;
        }

        /// <summary>Steel ploughing through ballast: low grinding rumble with stone clatter, 2 s.</summary>
        static float[] Scrape(System.Random r)
        {
            int n = 2 * Rate + Rate / 4;
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = N(r);
            LowPass(o, 700f);
            for (int k = 0; k < 90; k++)
            {
                int s0 = r.Next(n - 600);
                float f = 1200f + (float)r.NextDouble() * 2500f;
                for (int i = 0; i < 500; i++) o[s0 + i] += Mathf.Sin(2f * Mathf.PI * f * i / Rate) * Mathf.Exp(-i / 90f) * 0.5f;
            }
            Normalize(o, 0.8f);
            return o;
        }
    }
}
