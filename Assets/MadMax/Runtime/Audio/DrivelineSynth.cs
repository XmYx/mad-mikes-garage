using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>The drivetrain between the engine and the wheels (not engine sources): gearbox whine at the mesh
    /// frequency of the gear in use (loud in straight-cut reverse and first, louder worn), the differential's pinion whine
    /// (mostly on the overrun), clutch judder at a slipping pull-away, and clunks — a shift, engaging drive or reverse,
    /// the slack in the driveline taken up when the throttle reverses. Inputs written by the main thread
    /// (<see cref="VehicleAudio"/>); <see cref="Clunk"/> queues an impulse.</summary>
    public sealed class DrivelineSynth : ISynth
    {
        /// <summary>Gearbox input shaft rpm (the engine's while coupled) and prop shaft rpm.</summary>
        public float inputRpm, shaftRpm;
        /// <summary>-1 coasting hard .. 1 full drive.</summary>
        public float drive;
        /// <summary>Clutch / converter slip 0..1, reverse gear (straight-cut), first gear, driveline wear 0..1.</summary>
        public float slip, wear;
        public bool reverse, first;

        public const int GearTeeth = 27, PinionTeeth = 11;
        float pendingClunk, gearPh, gearPh2, diffPh, judPh, whineAmp, diffAmp, judAmp, clunkEnv;
        Svf clunkLow, clunkHigh, judLp;
        Noise noise;

        public DrivelineSynth(uint seed) { noise = new Noise(seed | 1u); }

        /// <summary>A knock in the driveline (0.3 lash … 1 a hard shift into gear).</summary>
        public void Clunk(float strength) => pendingClunk = Mathf.Max(pendingClunk, strength);

        public void Render(float[] buf, int n, int sr)
        {
            float dt = 1f / sr;
            clunkLow.Set(120f, 2.5f, sr); clunkHigh.Set(950f, 4f, sr); judLp.Set(260f, 0.9f, sr);
            float fGear = Mathf.Max(0f, inputRpm) / 60f * GearTeeth, fDiff = Mathf.Max(0f, shaftRpm) / 60f * PinionTeeth;
            float loadAbs = Mathf.Abs(drive);
            float tWhine = fGear > 30f ? (0.003f + (reverse ? 0.045f : first ? 0.009f : 0f) + wear * 0.02f) * (0.35f + loadAbs) * Mathf.Clamp01(fGear / 600f) : 0f;
            float tDiff = fDiff > 20f ? (0.002f + wear * 0.018f) * (0.4f + Mathf.Max(0f, -drive) * 1.6f) * Mathf.Clamp01(fDiff / 300f) : 0f;
            float tJud = slip > 0.25f && drive > 0.15f ? (slip - 0.25f) * drive * (0.6f + wear) * 0.25f : 0f;
            float k = Dsp.Coef(0.05f, sr);
            if (pendingClunk > 0f) { clunkEnv = pendingClunk * (0.6f + 0.4f * noise.Unit()); pendingClunk = 0f; }
            float cDecay = Mathf.Exp(-1f / (0.004f * sr));
            for (int i = 0; i < n; i++)
            {
                whineAmp += (tWhine - whineAmp) * k; diffAmp += (tDiff - diffAmp) * k; judAmp += (tJud - judAmp) * k;
                float nz = noise.Next();
                float o = 0f;
                if (whineAmp > 1e-5f)
                {
                    gearPh += fGear * dt; if (gearPh > 1f) gearPh -= 1f;
                    gearPh2 += fGear * 2.003f * dt; if (gearPh2 > 1f) gearPh2 -= 1f;
                    o += (Dsp.Sin(gearPh) + 0.35f * Dsp.Sin(gearPh2)) * whineAmp * (1f + 0.15f * nz);
                }
                if (diffAmp > 1e-5f) { diffPh += fDiff * dt; if (diffPh > 1f) diffPh -= 1f; o += Dsp.Sin(diffPh) * diffAmp; }
                if (judAmp > 1e-5f)
                {
                    judPh += 11f * dt; if (judPh > 1f) judPh -= 1f;
                    judLp.Process(nz);
                    o += judLp.lp * judAmp * (0.5f + 0.5f * Dsp.Sin(judPh));
                }
                float ex = 0f;
                if (clunkEnv > 1e-4f) { ex = nz * clunkEnv; clunkEnv *= cDecay; }
                clunkLow.Process(ex); clunkHigh.Process(ex);
                o += clunkLow.bp * 0.9f + clunkHigh.bp * 0.25f;
                buf[i] = Dsp.Tanh(o * 1.5f) * 0.8f;
            }
        }
    }
}
