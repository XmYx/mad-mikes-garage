using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>Sound character of an engine + exhaust combination: cylinders and firing pattern, header timing,
    /// exhaust pipe, muffler and extras. Chosen from the mounted engine and exhaust part ids.</summary>
    public class EngineProfile
    {
        public bool twoStroke;
        public float[] firing = Even(4);   // cycle fraction at which each cylinder fires
        public float[] headerMs;           // extra pulse delay per cylinder (unequal headers = burble)
        public float pulseMs = 0.8f, pipeLength = 2.0f, reflection = -0.55f, mufflerHz = 1000f, rasp = 0.28f;
        public float clatter, blower, turbo, crackle = 0.25f, roughness = 0.08f, gain = 1f;

        static float[] Even(int n) { var f = new float[n]; for (int i = 0; i < n; i++) f[i] = i / (float)n; return f; }

        /// <param name="exhaustId">mounted exhaust part, or null</param>
        /// <param name="exhaustSocket">the vehicle has an exhaust socket (empty socket = open headers)</param>
        public static EngineProfile For(string engineId, string exhaustId, bool exhaustSocket)
        {
            engineId ??= "";
            var p = new EngineProfile();
            if (engineId.Contains("v8"))
            {
                // cross-plane V8, firing order 1-8-4-3-6-5-7-2 alternates banks L R R L R L L R: uneven bank pulses burble
                p.firing = Even(8);
                p.headerMs = new[] { 0f, 0.5f, 0.5f, 0f, 0.5f, 0f, 0f, 0.5f };
                p.pulseMs = 1.0f; p.pipeLength = 2.4f; p.mufflerHz = 850f; p.rasp = 0.35f;
                p.blower = engineId.Contains("blower") ? 1f : 0f; p.crackle = 0.7f; p.roughness = 0.1f;
            }
            else if (engineId.Contains("truck"))
            {
                p.firing = Even(6);
                p.pulseMs = 1.1f; p.pipeLength = 3.6f; p.reflection = -0.6f; p.mufflerHz = 480f; p.rasp = 0.12f;
                p.clatter = 0.8f; p.turbo = 1f; p.crackle = 0f; p.roughness = 0.05f; p.gain = 1.1f;
            }
            else if (engineId.Contains("diesel"))
            {
                p.firing = Even(6);
                p.pulseMs = 0.7f; p.pipeLength = 2.6f; p.mufflerHz = 650f; p.rasp = 0.18f;
                p.clatter = 0.55f; p.turbo = 0.6f; p.crackle = 0.05f; p.roughness = 0.06f;
            }
            else if (engineId.Contains("2stroke") || engineId.Contains("two"))
            {
                p.twoStroke = true; p.firing = new[] { 0f, 0.5f };
                p.pulseMs = 0.45f; p.pipeLength = 0.9f; p.reflection = -0.75f; p.mufflerHz = 2000f; p.rasp = 0.55f;
                p.crackle = 0.3f; p.roughness = 0.12f; p.gain = 0.8f;
            }
            if (p.headerMs == null)
            {
                // slightly unequal runners on every engine
                p.headerMs = new float[p.firing.Length];
                var n = new Noise((uint)engineId.GetHashCode() | 1u);
                for (int i = 0; i < p.headerMs.Length; i++) p.headerMs[i] = n.Unit() * 0.12f;
            }
            exhaustId ??= "";
            if (exhaustId.Contains("side_pipes")) { p.mufflerHz *= 1.9f; p.rasp += 0.25f; p.pipeLength *= 0.55f; p.crackle += 0.3f; p.gain *= 1.25f; }
            else if (exhaustId.Contains("stack")) { p.pipeLength *= 1.35f; p.mufflerHz *= 0.9f; p.gain *= 1.1f; }
            else if (exhaustSocket && exhaustId.Length == 0) { p.mufflerHz *= 2.6f; p.rasp += 0.4f; p.pipeLength *= 0.35f; p.crackle += 0.35f; p.gain *= 1.4f; }
            return p;
        }
    }

    /// <summary>Physically inspired engine sound: each cylinder's combustion is a pressure pulse (width by load)
    /// released into an exhaust waveguide (pipe length resonances, open-end reflection) and a muffler low-pass with
    /// some raw rasp; plus intake roar, diesel knock, blower whine, turbo spool and blow-off, overrun crackle,
    /// rev-limiter cuts, idle roughness and a starter. Main thread writes rpm/load/running; Render runs on the audio thread.</summary>
    public sealed class EngineSynth : ISynth
    {
        public float rpm, load, maxRpm = 6000f, idleRpm = 900f;
        public bool running;

        readonly EngineProfile p;
        readonly float[] amp;
        readonly int[] delay;
        readonly float[] ring = new float[1024];
        readonly float[] pipe = new float[8192];
        int rw, pw, pipeLen, sr;
        float phase, curRpm, curLoad, jitter, overrun, spool, bov, crank, starterPh, blowerPh, turboPh;
        float lp1, lp2, knock, pipeLp, raspLp, hpX, hpY;
        bool wasRunning, cutCycle;
        Svf muffler, intake, bovFilter;
        Noise noise;

        public EngineSynth(EngineProfile profile, uint seed)
        {
            p = profile;
            amp = new float[p.firing.Length];
            delay = new int[p.firing.Length];
            noise = new Noise(seed | 1u);
        }

        void Init(int rate)
        {
            sr = rate;
            pipeLen = Mathf.Clamp(Mathf.RoundToInt(2f * p.pipeLength / 343f * sr), 8, pipe.Length - 1);
            for (int i = 0; i < delay.Length; i++) delay[i] = Mathf.Clamp(Mathf.RoundToInt(p.headerMs[i] * 0.001f * sr), 0, ring.Length - 1);
            bovFilter.Set(2500f, 0.8f, sr);
        }

        void NewCycle()
        {
            jitter = jitter * 0.85f + noise.Next() * p.roughness * 0.03f * (1f - curLoad);
            float baseAmp = curRpm < 120f ? 0f : 0.15f + 0.85f * Mathf.Pow(curLoad, 0.8f);
            if (crank > 0.15f) baseAmp = 0.05f;                    // compression puffs while cranking
            else if (crank > 0f) baseAmp = 0.9f;                   // catches
            cutCycle = curRpm > maxRpm * 0.985f && curLoad > 0.3f && noise.Unit() < 0.6f;
            for (int i = 0; i < amp.Length; i++)
            {
                float misfire = noise.Unit() < 0.004f * (1f - curLoad) ? 0.2f : 1f;
                amp[i] = baseAmp * misfire * (1f + noise.Next() * p.roughness * (1.3f - curLoad));
            }
        }

        void Fire(int c)
        {
            if (cutCycle) return;
            float a = amp[c];
            int mask = ring.Length - 1;
            ring[(rw + delay[c]) & mask] += a;
            knock += a * p.clatter * 0.5f;
            if (overrun > 0f && noise.Unit() < p.crackle * 0.3f)
            {
                ring[(rw + delay[c] + 40 + (int)(noise.Unit() * 200f)) & mask] += 1.2f + noise.Unit();   // late burn in the pipe
                knock += 0.8f * p.crackle;
            }
        }

        public void Render(float[] buf, int n, int rate)
        {
            if (rate != sr) Init(rate);
            float dt = 1f / sr, blockT = n * dt;
            bool on = running;
            if (on && !wasRunning) crank = 0.75f;
            wasRunning = on;
            float targetLoad = on ? Mathf.Clamp01(load) : 0f;
            if (curLoad > 0.55f && targetLoad < 0.12f && curRpm > maxRpm * 0.4f) { overrun = 1.4f; if (p.turbo > 0f && spool > 0.4f) bov = spool; }
            overrun = Mathf.Max(0f, overrun - blockT);
            crank = Mathf.Max(0f, crank - blockT);
            float targetRpm = on ? (crank > 0.15f ? 230f : Mathf.Max(rpm, idleRpm * 0.95f)) : 0f;

            float rpmN = Mathf.Clamp01((curRpm - idleRpm) / Mathf.Max(1f, maxRpm - idleRpm));
            float tau = p.pulseMs * 0.001f * (1.25f - 0.45f * curLoad);
            float a = Mathf.Exp(-1f / (tau * sr));
            float norm = 2.718f / (1f - a);                        // peak of the two-pole pulse = 1
            muffler.Set(p.mufflerHz * (0.65f + 0.7f * curLoad + 0.3f * rpmN), 0.8f, sr);
            intake.Set(700f + 2600f * rpmN, 1.8f, sr);
            float rpmK = Dsp.Coef(crank > 0f ? 0.02f : 0.06f, sr), loadK = Dsp.Coef(0.03f, sr);
            float pipeK = Dsp.Coef(1f / (2f * Mathf.PI * 2500f), sr), raspK = Dsp.Coef(1f / (2f * Mathf.PI * 900f), sr);
            float knockDecay = Mathf.Exp(-1f / (0.0015f * sr)), bovDecay = Mathf.Exp(-1f / (0.35f * sr));
            float spoolK = Dsp.Coef(1.2f, sr);
            float level = 0.35f * p.gain / Mathf.Sqrt(p.firing.Length / 4f);
            float cycleScale = p.twoStroke ? 1f / 60f : 1f / 120f;
            int cyl = p.firing.Length, mask = ring.Length - 1, pmask = pipe.Length - 1;

            for (int i = 0; i < n; i++)
            {
                curRpm += (targetRpm * (1f + jitter) - curRpm) * rpmK;
                curLoad += (targetLoad - curLoad) * loadK;

                float p0 = phase;
                phase += curRpm * cycleScale * dt;
                bool wrap = phase >= 1f;
                if (wrap) { phase -= 1f; NewCycle(); }
                for (int c = 0; c < cyl; c++)
                {
                    float o = p.firing[c];
                    if (wrap ? (o > p0 || o <= phase) : (o > p0 && o <= phase)) Fire(c);
                }

                // combustion pulse: impulse through two one-poles = alpha-function pressure pulse
                float imp = ring[rw]; ring[rw] = 0f; rw = (rw + 1) & mask;
                lp1 = a * lp1 + (1f - a) * imp;
                lp2 = a * lp2 + (1f - a) * lp1;
                float nz = noise.Next();
                knock *= knockDecay;
                float x = lp2 * norm + nz * knock;

                // exhaust pipe: waveguide with a lossy inverted reflection at the open end
                float back = pipe[(pw - pipeLen) & pmask];
                pipeLp += (back - pipeLp) * pipeK;
                float y = x + p.reflection * pipeLp;
                pipe[pw] = y; pw = (pw + 1) & pmask;

                muffler.Process(y);
                raspLp += (y - raspLp) * raspK;
                float o2 = muffler.lp + (y - raspLp) * p.rasp;

                intake.Process(nz * (0.15f + lp1 * 4f));
                o2 += intake.bp * 0.3f * curLoad;

                if (p.blower > 0f)
                {
                    blowerPh += curRpm * 0.45f * dt; if (blowerPh > 1f) blowerPh -= 1f;
                    o2 += Dsp.Sin(blowerPh) * 0.02f * p.blower * rpmN * (0.4f + curLoad);
                }
                if (p.turbo > 0f)
                {
                    spool += (curLoad * rpmN - spool) * spoolK;
                    turboPh += (1800f + 5200f * spool) * dt; if (turboPh > 1f) turboPh -= 1f;
                    o2 += Dsp.Sin(turboPh) * 0.012f * spool * p.turbo;
                    if (bov > 0.001f) { bovFilter.Process(nz); o2 += bovFilter.bp * bov * 0.25f * p.turbo; bov *= bovDecay; }
                }
                if (crank > 0.15f)
                {
                    starterPh += 150f * dt; if (starterPh > 1f) starterPh -= 1f;
                    o2 += Dsp.Tanh(3f * Dsp.Sin(starterPh)) * 0.05f + nz * 0.01f;
                }

                // DC blocker, soft clip
                hpY = o2 - hpX + 0.995f * hpY; hpX = o2;
                buf[i] = Dsp.Tanh(hpY * level * 2f) * 0.8f;
            }
            if (float.IsNaN(hpY)) { hpY = hpX = lp1 = lp2 = pipeLp = raspLp = 0f; System.Array.Clear(pipe, 0, pipe.Length); }
        }
    }
}
