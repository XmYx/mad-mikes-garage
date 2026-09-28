using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>Tyre and road noise: squeal (resonant rubber band + stick-slip flutter) only when sliding on hard ground,
    /// rolling roar on roads, gravel crackle and rumble on loose ground, squelch in mud, and air rush with speed.
    /// Inputs 0..1, written by the main thread.</summary>
    public sealed class TyreSynth : ISynth
    {
        public float squeal, roll, loose, mud, air;

        int sr;
        float sq, rl, ls, md, ar, wander, chatPh, tonePh, mudPh, gravelEnv;
        Svf bandA, bandB, rollLp, rumble, gravel, mudLp, airBand;
        Noise noise;

        public TyreSynth(uint seed) { noise = new Noise(seed | 1u); }

        public void Render(float[] buf, int n, int rate)
        {
            sr = rate;
            float dt = 1f / sr;
            wander = Mathf.Clamp(wander * 0.98f + noise.Next() * 0.08f, -1f, 1f);
            float f0 = 1050f * (1f + 0.1f * wander) + 300f * sq;
            bandA.Set(f0, 16f, sr); bandB.Set(f0 * 2.07f, 20f, sr);
            rollLp.Set(220f + rl * 500f, 0.7f, sr);
            rumble.Set(140f, 0.7f, sr);
            gravel.Set(2800f + 800f * wander, 1.4f, sr);
            mudLp.Set(260f, 1.2f, sr);
            airBand.Set(900f + ar * 1500f, 0.6f, sr);
            float up = Dsp.Coef(0.03f, sr), down = Dsp.Coef(0.09f, sr), slow = Dsp.Coef(0.15f, sr);
            float gDecay = Mathf.Exp(-1f / (0.0012f * sr));
            float tSq = Mathf.Clamp01(squeal), tRl = Mathf.Clamp01(roll), tLs = Mathf.Clamp01(loose), tMd = Mathf.Clamp01(mud), tAr = Mathf.Clamp01(air);

            for (int i = 0; i < n; i++)
            {
                sq += (tSq - sq) * (tSq > sq ? up : down);
                rl += (tRl - rl) * slow; ls += (tLs - ls) * up; md += (tMd - md) * slow; ar += (tAr - ar) * slow;
                float nz = noise.Next();
                float o = 0f;

                if (sq > 0.002f)
                {
                    chatPh += (26f + 12f * wander) * dt; if (chatPh > 1f) chatPh -= 1f;
                    tonePh += f0 * (1f + 0.012f * nz) * dt; if (tonePh > 1f) tonePh -= 1f;
                    bandA.Process(nz); bandB.Process(nz);
                    float am = 0.6f + 0.4f * Dsp.Sin(chatPh);
                    o += (bandA.bp * 2.5f + bandB.bp * 1.2f + Dsp.Sin(tonePh) * 0.3f) * am * sq * Mathf.Sqrt(sq) * 0.6f;
                }
                if (rl > 0.002f) { rollLp.Process(nz); o += rollLp.lp * rl * 0.5f; }
                if (ls > 0.002f)
                {
                    if (noise.Unit() < ls * 0.006f) gravelEnv = 0.4f + noise.Unit();
                    gravelEnv *= gDecay;
                    gravel.Process(nz * gravelEnv);
                    rumble.Process(nz);
                    o += gravel.bp * ls * 1.4f + rumble.lp * ls * 0.35f;
                }
                if (md > 0.002f)
                {
                    mudPh += (4f + 3f * wander) * dt; if (mudPh > 1f) mudPh -= 1f;
                    mudLp.Process(nz);
                    o += mudLp.lp * md * 0.7f * (0.55f + 0.45f * Dsp.Sin(mudPh));
                }
                if (ar > 0.002f) { airBand.Process(nz); o += airBand.bp * ar * 0.25f; }
                buf[i] = Dsp.Tanh(o * 1.2f) * 0.8f;
            }
        }
    }
}
