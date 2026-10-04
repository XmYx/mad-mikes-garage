using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>An engine's sound recipe: its <see cref="EngineSpec"/> (cylinders, cycle, fuel, size, valvetrain...)
    /// plus the exhaust system fitted (pipe length, muffler, rasp, header runners, the Y-pipe offset between banks).</summary>
    public class EngineProfile
    {
        public EngineSpec spec;
        public float pipeLength, mufflerHz, rasp, crackle, gain = 1f, reflection = -0.6f;
        /// <summary>Runner delay per cylinder (ms; unequal runners and the second bank's longer way to the Y-pipe).</summary>
        public float[] headerMs;
        /// <summary>No exhaust on a vehicle built for one: open headers, raw and loud.</summary>
        public bool openHeaders;

        /// <param name="exhaustId">mounted exhaust part, or null</param>
        /// <param name="exhaustSocket">the vehicle has an exhaust socket (empty socket = open headers)</param>
        public static EngineProfile For(string engineId, string exhaustId, bool exhaustSocket, EngineSpec spec = null)
        {
            spec ??= EngineSpec.For(engineId);
            var p = new EngineProfile { spec = spec, pipeLength = spec.pipeLength, mufflerHz = spec.mufflerHz };
            p.rasp = spec.diesel ? 0.12f : spec.twoStroke ? 0.5f : 0.22f + spec.cam * 0.2f;
            p.crackle = spec.diesel ? 0f : spec.twoStroke ? 0.3f : 0.3f + spec.cam * 0.4f;
            if (spec.twoStroke) p.reflection = -0.75f;                          // an expansion chamber rings
            p.headerMs = new float[spec.cylinders];
            var n = new Noise((uint)(engineId ?? "").GetHashCode() | 1u);
            for (int i = 0; i < p.headerMs.Length; i++) p.headerMs[i] = n.Unit() * 0.15f + spec.bank[i] * (spec.cylinders >= 8 && spec.cam > 0.5f ? 0.55f : 0.25f);
            exhaustId ??= "";
            if (exhaustId.Contains("side_pipes")) { p.mufflerHz *= 1.9f; p.rasp += 0.25f; p.pipeLength *= 0.55f; p.crackle += 0.3f; p.gain *= 1.25f; }
            else if (exhaustId.Contains("stack")) { p.pipeLength *= 1.35f; p.mufflerHz *= 0.9f; p.gain *= 1.1f; }
            else if (exhaustSocket && exhaustId.Length == 0) { p.openHeaders = true; p.mufflerHz *= 3f; p.rasp += 0.45f; p.pipeLength *= 0.3f; p.crackle += 0.35f; p.gain *= 1.45f; }
            return p;
        }
    }

    /// <summary>Engine sound built from what an engine does, on a crank-angle clock: every cylinder fires at its angle in
    /// the 720° (four-stroke) or 360° (two-stroke) cycle, its exhaust valve opens 130° later and a blowdown pulse (peak by
    /// load and cylinder health, width by crank angle — long at idle, short at the redline, longer for big cylinders) runs
    /// down its header runner into the pipe (a waveguide with an inverted open-end reflection), through the muffler and
    /// out of the tailpipe (radiated as the flow's rate of change). A leak before the muffler lets raw pulses and hiss out
    /// at the engine; a damaged muffler opens up. Intake valves draw pulses through the air box (a Helmholtz resonance)
    /// with throttle hiss; a clogged filter muffles and whistles. Mechanically every valve seats with a click (pushrods
    /// clack, overhead cams tick, two-strokes have none; worn or dry lifters tick loud), worn rod bearings knock as the
    /// load reverses, cold or worn pistons slap, diesels clatter (sharp pressure rise) and their injectors tick, bad
    /// petrol pings under load, all rung through block resonances scaled by cylinder size (air-cooled fins ring, a fan
    /// blows). Worn cylinders lose compression unevenly (a lope), misfires leave gaps and bang in the pipe, cams with
    /// overlap lope at idle, blowers whine, turbos spool and blow off, the limiter cuts. The starter turns the crank at
    /// 150–300 rpm, slowing on every compression stroke (the rhythm of a real crank), puffing air out of the pipe,
    /// coughing if it will catch and flaring past idle when it does; switched off, the flywheel coasts down through its
    /// compressions.
    /// <para>Two outputs from one simulation: <see cref="Exhaust"/> (at the tailpipe) and <see cref="Bay"/> (mechanical
    /// and intake, at the engine). Main thread writes the inputs; rendering runs on the audio thread, allocation-free.</para></summary>
    public sealed class EngineSynth
    {
        // ------------------------------------------------------------------ inputs (main thread)
        public float rpm, load, maxRpm = 6000f, idleRpm = 900f;
        public bool running, cranking, crankCatches;
        /// <summary>0..1 through the current crank.</summary>
        public float crankProgress;
        /// <summary>Condition 0..1: engine wear, oil (1 = full and good), misfire, spark-knock tendency, cold engine,
        /// exhaust leak before the muffler, muffler damage, clogged air filter.</summary>
        public float wear, oil = 1f, misfire, ping, cold, leak, mufflerDamage, clog;
        /// <summary>Fuel at the injectors / carburettor (false: cranks without a cough).</summary>
        public bool fuelled = true;

        public readonly ISynth Exhaust, Bay;
        public readonly EngineProfile profile;
        public EngineSpec Spec => profile.spec;

        sealed class Tap : ISynth
        {
            readonly EngineSynth core; readonly int ch;
            public Tap(EngineSynth core, int ch) { this.core = core; this.ch = ch; }
            public void Render(float[] mono, int frames, int sampleRate) => core.Pull(ch, mono, frames, sampleRate);
        }

        // ------------------------------------------------------------------ state
        enum Ev : byte { Fire, ExhaustOpen, IntakeOpen, ValveSeat, Tdc, Bdc, Inject }
        readonly float[] evAngle; readonly Ev[] evKind; readonly int[] evCyl;
        readonly float[] comp, cylAmp; readonly bool[] burned;
        readonly int[] delay;
        readonly float[] ring = new float[2048];
        readonly float[] pipe = new float[8192];
        readonly float[][] outBuf = { new float[8192], new float[8192] };
        readonly int[] seen = new int[2];
        readonly object gate = new object();
        int block, lastFrames;

        int sr, ev, rw, pw, pipeLen;
        float theta, curRpm, curLoad, prevLoad, jitter, overrun, spool, bov, flare, crankFx, coast, startClunk;
        float starterPh, blowerPh, turboPh, beltPh, gearPh, fanLp, bearLp;
        float ex1, ex2, in1, in2, pipeLp, raspLp, mPrev, hpX, hpY, hpX2, hpY2, leakEnv;
        float exImp, inImp, combExc, valveExc, rodExc, slapExc, dieselExc, injExc, pingExc, leakExc;
        bool wasRunning, wasCranking, cutCycle;
        Svf muffler, muffler2, raspBand, airbox, hiss, leakBand, blockA, blockB, blockC, valve, rod, dieselA, dieselB, injector, pingRes, fin, starterGrind, bovBand, whistle, blowby;
        Noise noise;
        readonly float blockScale, size, valveHz, valveGain, crankBase, flywheel;

        public EngineSynth(EngineProfile profile, uint seed)
        {
            this.profile = profile;
            var s = profile.spec;
            noise = new Noise(seed | 1u);
            int n = s.cylinders;
            comp = new float[n]; cylAmp = new float[n]; burned = new bool[n]; delay = new int[n];
            // per-cylinder share of the wear: one cylinder always worst, the rest scattered
            var cn = new Noise(seed * 2654435761u | 1u);
            for (int c = 0; c < n; c++) comp[c] = c == (int)(seed % (uint)n) ? 1f : cn.Unit() * 0.6f;
            // event table: per cylinder, sorted by crank angle
            var list = new System.Collections.Generic.List<(float a, Ev k, int c)>();
            float cyc = s.Cycle;
            for (int c = 0; c < n; c++)
            {
                float f = s.fireDeg[c];
                list.Add((f, Ev.Fire, c)); list.Add((f, Ev.Tdc, c));
                if (s.twoStroke)
                {
                    list.Add((f + 100f, Ev.ExhaustOpen, c)); list.Add((f + 125f, Ev.IntakeOpen, c)); list.Add((f + 180f, Ev.Bdc, c));
                }
                else
                {
                    list.Add((f + 130f, Ev.ExhaustOpen, c)); list.Add((f + 350f, Ev.IntakeOpen, c));
                    list.Add((f + 375f, Ev.ValveSeat, c)); list.Add((f + 580f, Ev.ValveSeat, c)); list.Add((f + 360f, Ev.Bdc, c));
                    if (s.diesel) list.Add((f - 8f, Ev.Inject, c));
                }
            }
            for (int i = 0; i < list.Count; i++) { var e = list[i]; e.a = Mathf.Repeat(e.a, cyc); list[i] = e; }
            list.Sort((x, y) => x.a.CompareTo(y.a));
            evAngle = new float[list.Count]; evKind = new Ev[list.Count]; evCyl = new int[list.Count];
            for (int i = 0; i < list.Count; i++) { evAngle[i] = list[i].a; evKind[i] = list[i].k; evCyl[i] = list[i].c; }

            size = Mathf.Sqrt(Mathf.Max(0.2f, s.displacement) / 2f);                      // loudness by displacement
            blockScale = Mathf.Pow(0.5f / Mathf.Max(0.1f, s.PerCylinder), 0.33f);           // big cylinders ring low
            valveHz = s.valves == Valvetrain.Ohv ? 2400f : s.valves == Valvetrain.Sohc ? 3600f : 5200f;
            valveGain = s.valves == Valvetrain.Ports ? 0f : s.valves == Valvetrain.Ohv ? 1.3f : s.valves == Valvetrain.Sohc ? 0.8f : 0.5f;
            if (s.diesel) valveGain *= 1.3f;
            flywheel = Mathf.Max(0.3f, s.flywheel);
            crankBase = (s.diesel ? 170f : s.twoStroke ? 320f : 250f) / (0.8f + 0.2f * Mathf.Sqrt(flywheel));
            theta = noise.Unit() * s.Cycle;
            Exhaust = new Tap(this, 0); Bay = new Tap(this, 1);
        }

        void Init(int rate)
        {
            sr = rate;
            pipeLen = Mathf.Clamp(Mathf.RoundToInt(2f * profile.pipeLength / 500f * sr), 8, pipe.Length - 1);   // round trip in hot gas (~500 m/s)
            for (int c = 0; c < delay.Length; c++) delay[c] = Mathf.Clamp(Mathf.RoundToInt(profile.headerMs[c] * 0.001f * sr), 0, ring.Length - 1);
            ev = 0; while (ev < evAngle.Length && evAngle[ev] <= theta) ev++;
            leakBand.Set(1900f, 1.2f, sr); bovBand.Set(2600f, 0.8f, sr); blowby.Set(900f, 0.7f, sr);
            injector.Set(6400f, 10f, sr); pingRes.Set(6300f, 14f, sr);
            dieselA.Set(2100f * Mathf.Lerp(1f, blockScale, 0.5f), 6f, sr); dieselB.Set(3400f * Mathf.Lerp(1f, blockScale, 0.5f), 7f, sr);
            blockA.Set(620f * blockScale, 2.5f, sr); blockB.Set(1450f * blockScale, 3f, sr); blockC.Set(3100f * blockScale, 4f, sr);
            valve.Set(valveHz, 5f, sr); rod.Set(420f * blockScale, 4f, sr); fin.Set(2800f, 22f, sr); starterGrind.Set(2300f, 1.5f, sr);
            airbox.Set(Mathf.Clamp(190f / Mathf.Sqrt(Mathf.Max(0.3f, Spec.displacement) / 2f), 70f, 420f), 3f, sr);
        }

        // ------------------------------------------------------------------ the two voices share one simulation

        void Pull(int ch, float[] buf, int n, int rate)
        {
            lock (gate)
            {
                if (n > outBuf[0].Length) { System.Array.Clear(buf, 0, n); return; }
                if (seen[ch] == block || n != lastFrames || rate != sr) { RenderBlock(n, rate); block++; lastFrames = n; }
                seen[ch] = block;
                System.Array.Copy(outBuf[ch], buf, n);
            }
        }

        // ------------------------------------------------------------------ events

        float Roughness => 0.03f + Spec.cam * 0.22f * Mathf.Clamp01(1.4f - curRpm / Mathf.Max(1f, idleRpm * 2.2f)) + wear * 0.15f + misfire * 0.2f + cold * 0.05f;

        float Pressure(int c)
        {
            // cylinder pressure when the exhaust opens: throttle (diesel: fuel) by load; worn cylinders leak it away
            float l = Mathf.Pow(curLoad, 0.8f);
            float baseP = Spec.diesel ? 0.45f + 0.55f * l : 0.36f + 0.64f * l;                 // ~2.5 bar at the exhaust valve idling, ~6 flat out
            float health = 1f - wear * comp[c] * 0.75f;
            return baseP * health * (1f + jitter + noise.Next() * Roughness * (1.2f - curLoad));
        }

        void Event(int i)
        {
            int c = evCyl[i];
            switch (evKind[i])
            {
                case Ev.Fire:
                {
                    bool fire;
                    if (cranking && !running) fire = fuelled && noise.Unit() < (crankCatches ? Mathf.SmoothStep(0.25f, 0.95f, crankProgress) * 0.8f : 0.1f);
                    else if (!running || cutCycle) fire = false;
                    else
                    {
                        float miss = misfire * 0.55f + 0.002f * (1f - curLoad) + Mathf.Max(0f, wear - 0.55f) * 0.35f * comp[c];
                        if (wear > 0.85f && comp[c] >= 1f) miss = 1f;                                    // a dead cylinder
                        if (Spec.twoStroke && curLoad < 0.15f && curRpm < idleRpm * 2.5f) miss += 0.3f;   // two-strokes "four-stroke" off load
                        fire = noise.Unit() >= miss;
                    }
                    burned[c] = fire;
                    float p = fire ? (cranking && !running ? 0.55f : Pressure(c)) : 0f;
                    cylAmp[c] = fire ? p : (running ? 0.1f : 0.07f) * (1f - wear * 0.5f);              // unfired: just compressed air
                    combExc += p * (Spec.diesel ? 1.2f : 0.7f);
                    if (fire && Spec.diesel) dieselExc += p * (1.5f - 0.7f * curLoad) * (1f + cold * 1.2f) + 0.15f;
                    if (fire && !Spec.diesel && ping > 0f && curLoad > 0.45f && noise.Unit() < ping * curLoad) pingExc += 0.6f + noise.Unit() * 0.4f;
                    if (fire) slapExc += (cold * 0.7f + wear * 0.6f) * (0.3f + curLoad) * 0.5f;
                    break;
                }
                case Ev.ExhaustOpen:
                {
                    float a = cylAmp[c] * (Spec.twoStroke ? 0.9f : 1f);
                    int mask = ring.Length - 1;
                    ring[(rw + delay[c]) & mask] += a;
                    leakExc += a;
                    // unburnt charge lights in the hot pipe: a bang (overrun, misfires)
                    if (!burned[c] && running && !Spec.diesel && curRpm > idleRpm * 1.4f && noise.Unit() < profile.crackle * (overrun > 0f ? 0.35f : 0.12f))
                        ring[(rw + delay[c] + 30 + (int)(noise.Unit() * 400f)) & mask] += 0.9f + noise.Unit() * 0.9f;
                    break;
                }
                case Ev.IntakeOpen:
                    inImp += (0.25f + 0.75f * curLoad) * (Spec.twoStroke ? 0.6f : 1f);
                    break;
                case Ev.ValveSeat:
                {
                    float lash = wear * 0.8f + (1f - oil) * 1.2f + cold * 0.25f;                       // worn, dry or pumped-down lifters tick
                    float rn = Mathf.Clamp01(curRpm / Mathf.Max(1f, maxRpm));
                    valveExc += valveGain * (0.3f + 1.4f * rn * rn) * (1f + lash * 3.5f) * (0.8f + noise.Unit() * 0.4f);
                    break;
                }
                case Ev.Tdc:
                case Ev.Bdc:
                {
                    // rod bearings knock as the load reverses; dry ones worst
                    float bear = Mathf.Pow(Mathf.Max(0f, wear - 0.3f) * 1.4f, 1.5f) + (1f - oil) * (1f - oil) * 1.6f;
                    if (bear > 0.01f) rodExc += bear * (0.35f + 0.65f * curLoad + Mathf.Abs(curLoad - prevLoad) * 4f) * (evKind[i] == Ev.Tdc ? 1f : 0.45f);
                    break;
                }
                case Ev.Inject:
                    if (running || cranking) injExc += 0.35f + 0.25f * curLoad;
                    break;
            }
        }

        void NewCycle()
        {
            jitter = jitter * 0.7f + noise.Next() * Roughness * 0.12f * (1f - curLoad);
            cutCycle = running && curRpm > maxRpm * 0.985f && curLoad > 0.3f && noise.Unit() < 0.6f;
            prevLoad = curLoad;
        }

        // ------------------------------------------------------------------ rendering

        void RenderBlock(int n, int rate)
        {
            if (rate != sr) Init(rate);
            var s = Spec;
            var exOut = outBuf[0]; var bayOut = outBuf[1];
            float dt = 1f / sr, blockT = n * dt;
            int cyl = s.cylinders;

            // ---- block-rate state: start, catch, coast, overrun
            bool on = running;
            if (cranking && !wasCranking) startClunk = 1f;                                       // the solenoid throws the pinion in
            if (!cranking && wasCranking) crankFx = 0.18f;                                      // ...and out
            if (on && !wasRunning && (wasCranking || curRpm < idleRpm * 0.6f)) flare = 1f;      // it caught: flares past idle
            if (!on && wasRunning) coast = 1f;
            wasRunning = on; wasCranking = cranking;
            flare = Mathf.Max(0f, flare - blockT / 1.1f);
            crankFx = Mathf.Max(0f, crankFx - blockT);
            if (curRpm < 40f && !on && !cranking) { coast = 0f; curRpm = 0f; }                    // the last compression stops it dead
            float targetLoad = on ? Mathf.Clamp01(load) : 0f;
            if (curLoad > 0.55f && targetLoad < 0.12f && curRpm > maxRpm * 0.4f) { overrun = 1.4f; if (s.air == Aspiration.Turbo && spool > 0.35f) bov = spool * (s.diesel ? 0.3f : 1f); }
            overrun = Mathf.Max(0f, overrun - blockT);

            bool crankOnly = cranking && !on;
            float targetRpm;
            if (crankOnly) targetRpm = crankBase * (1f - 0.25f * cold) * (1f + wear * 0.25f);     // worn = less compression = turns faster
            else if (on) targetRpm = Mathf.Max(rpm, idleRpm * (0.95f + 0.6f * flare * flare * (3f - 2f * flare)));
            else targetRpm = 0f;
            float rpmTau = (on || crankOnly ? 0.05f : 0.35f) * Mathf.Pow(flywheel, on ? 0.6f : 0.25f);
            float rpmK = Dsp.Coef(rpmTau, sr), loadK = Dsp.Coef(0.03f, sr), spoolK = Dsp.Coef(1.0f + 0.4f * s.displacement / 6f, sr);

            float rpmN = Mathf.Clamp01((curRpm - idleRpm) / Mathf.Max(1f, maxRpm - idleRpm));
            float blow = Mathf.Clamp(70f / (6f * Mathf.Max(curRpm, 80f)) * (0.75f + 0.25f / blockScale), 0.0004f, 0.02f);   // blowdown: ~70° of crank
            float a = Mathf.Exp(-1f / (blow * sr)), norm = 2.718f / (1f - a);
            float ai = Mathf.Exp(-1f / (blow * 0.7f * sr)), normI = 2.718f / (1f - ai);
            float mufHz = profile.mufflerHz * (0.65f + 0.7f * curLoad + 0.3f * rpmN) * (1f + 2.2f * mufflerDamage);
            muffler.Set(mufHz, 0.75f, sr); muffler2.Set(mufHz * 1.3f, 0.6f, sr);                  // two chambers: 24 dB/octave
            raspBand.Set(3800f, 0.7f, sr);
            hiss.Set((1400f + 3000f * rpmN) * (1f - 0.45f * clog), 1.3f, sr);
            whistle.Set(2400f + 1800f * rpmN, 25f, sr);
            float pipeK = Dsp.Coef(1f / (2f * Mathf.PI * 2500f), sr), raspK = Dsp.Coef(1f / (2f * Mathf.PI * 900f), sr);
            float fastDecay = Mathf.Exp(-1f / (0.0004f * sr)), bovDecay = Mathf.Exp(-1f / (0.35f * sr));
            float rasp = profile.rasp * (1f + mufflerDamage * 1.5f) * (0.25f + 0.75f * Mathf.Max(curLoad, rpmN));   // raw edge comes with flow
            float level = 0.36f * profile.gain * (0.6f + 0.4f * size) / Mathf.Sqrt(Mathf.Max(1f, cyl / 4f));
            float period = s.Cycle / cyl, compDepth = Mathf.Clamp(0.55f * 4f / cyl, 0.1f, 0.6f) * (s.diesel ? 1.2f : 1f) * (1f - wear * 0.5f);
            float fluct = 0.025f * Mathf.Min(1f, 4f / cyl) * (1f + s.cam);                      // the crank speeds up after each firing
            float first = s.fireDeg[0];
            int mask = ring.Length - 1, pmask = pipe.Length - 1;
            float exBleed = profile.openHeaders ? 0.5f : 0.22f;

            for (int i = 0; i < n; i++)
            {
                curRpm += (targetRpm * (1f + jitter * 0.4f) - curRpm) * rpmK;
                curLoad += (targetLoad - curLoad) * loadK;

                // instantaneous crank speed: compressions slow it (cranking, coasting), firings kick it (idle)
                float rel = (theta - first) / period; rel -= Mathf.Floor(rel);
                float cosv = Mathf.Cos(rel * 2f * Mathf.PI);
                float inst = curRpm;
                if (crankOnly || (!on && curRpm < 400f))
                {
                    float c4 = cosv > 0f ? cosv * cosv * cosv * cosv : 0f;                        // near each compression TDC
                    inst *= 1f - compDepth * c4 * (rel > 0.5f ? 1f : 0.35f);
                }
                else if (on) inst *= 1f + fluct * Mathf.Sin(rel * 2f * Mathf.PI) * (1f - curLoad * 0.7f);

                // advance the crank (6° per second per rpm); fire the events it passes
                float th = theta + inst * 6f * dt;
                if (th >= s.Cycle)
                {
                    while (ev < evAngle.Length) Event(ev++);
                    th -= s.Cycle; ev = 0; NewCycle();
                }
                theta = th;
                while (ev < evAngle.Length && evAngle[ev] <= theta) Event(ev++);
                if (startClunk > 0f) { combExc += startClunk * 3f; valveExc += startClunk * 2f; startClunk = 0f; }

                float nz = noise.Next();

                // ---- exhaust: blowdown pulses (alpha-function) with turbulence, runners → pipe → muffler → tailpipe
                exImp = ring[rw]; ring[rw] = 0f; rw = (rw + 1) & mask;
                ex1 = a * ex1 + (1f - a) * exImp;
                ex2 = a * ex2 + (1f - a) * ex1;
                float flow = ex2 * norm;
                float x = flow * (1f + nz * 0.2f * rasp);
                float back = pipe[(pw - pipeLen) & pmask];
                pipeLp += (back - pipeLp) * pipeK;
                float y = x * (1f - 0.45f * leak) + profile.reflection * pipeLp;
                pipe[pw] = y; pw = (pw + 1) & pmask;
                muffler.Process(y); muffler2.Process(muffler.lp);
                raspLp += (y - raspLp) * raspK;
                raspBand.Process(y - raspLp);
                float m = muffler2.lp + raspBand.lp * rasp;
                float rad = m - 0.55f * mPrev; mPrev = m;                                         // radiated: leans towards the flow's rate of change
                float exhaust = rad * 2.6f;

                // ---- leak at the manifold: the raw pulse and a hiss of gas, heard at the engine
                leakEnv += (leakExc - leakEnv) * 0.02f; leakExc *= fastDecay;
                leakBand.Process(nz * leakEnv + leakExc);
                float leaky = leak * (leakBand.bp * 1.4f + (flow - ex1) * 0.6f);

                // ---- intake: valve pulses through the air box, throttle hiss, a clogged filter whistles
                in1 = ai * in1 + (1f - ai) * inImp; inImp = 0f;
                in2 = ai * in2 + (1f - ai) * in1;
                airbox.Process(in2 * normI * (1f - 0.5f * clog));
                float airflow = rpmN * (0.25f + 0.75f * curLoad) + (curRpm > idleRpm * 1.5f && curLoad < 0.08f && on ? 0.25f : 0f);
                hiss.Process(nz * airflow);
                whistle.Process(nz);
                float intake = airbox.bp * 0.5f + hiss.bp * 0.22f + whistle.bp * clog * airflow * 0.6f;

                // ---- mechanical: impulses rung through block, valve, bearing, diesel and ping resonances
                blockA.Process(combExc + slapExc * 0.6f + rodExc * 0.3f); blockB.Process(combExc * 0.6f + slapExc); blockC.Process(combExc * 0.3f + valveExc * 0.15f);
                valve.Process(valveExc);
                rod.Process(rodExc);
                float mech = blockA.bp * 0.35f + blockB.bp * 0.3f + blockC.bp * 0.25f + valve.bp * 0.22f + rod.bp * 0.7f;
                if (s.diesel) { dieselA.Process(dieselExc); dieselB.Process(dieselExc * 0.8f); injector.Process(injExc); mech += dieselA.bp * 0.35f + dieselB.bp * 0.3f + injector.bp * 0.12f; }
                if (ping > 0f) { pingRes.Process(pingExc); mech += pingRes.bp * 0.3f; }
                if (s.airCooled) { fin.Process(combExc * 0.5f); mech += fin.bp * 0.08f; fanLp += (nz * rpmN - fanLp) * 0.08f; mech += fanLp * 0.05f * (0.3f + rpmN); }
                combExc = valveExc = rodExc = slapExc = dieselExc = injExc = pingExc = 0f;
                // worn mains rumble once a turn; blow-by puffs from the breather
                if (wear > 0.4f) { bearLp += (nz - bearLp) * 0.02f; mech += bearLp * (wear - 0.4f) * 0.4f * (0.6f + 0.4f * Mathf.Sin(theta * Mathf.Deg2Rad)) * (0.3f + rpmN); }
                if (wear > 0.3f && on) { blowby.Process(nz * flow); mech += blowby.bp * (wear - 0.3f) * curLoad * 0.8f; }
                // accessories: belt and alternator, timing gears on diesels
                beltPh += curRpm / 60f * 2.7f * dt; if (beltPh > 1f) beltPh -= 1f;
                mech += Dsp.Sin(beltPh * 8f) * 0.004f * rpmN;
                if (s.diesel) { gearPh += curRpm / 60f * 38f * dt; if (gearPh > 1f) gearPh -= 1f; mech += Dsp.Sin(gearPh) * 0.006f * (0.2f + rpmN) * (1f + wear); }

                // ---- forced induction
                float forced = 0f;
                if (s.air == Aspiration.Blower)
                {
                    blowerPh += curRpm / 60f * 1.4f * 6f * dt; if (blowerPh > 1f) blowerPh -= 1f;     // roots lobes at 1.4× crank
                    forced += (Dsp.Sin(blowerPh) + 0.3f * Dsp.Sin(blowerPh * 2f)) * 0.03f * (0.3f + rpmN) * (0.4f + curLoad);
                }
                else if (s.air == Aspiration.Turbo)
                {
                    spool += (curLoad * Mathf.Clamp01(rpmN * 1.3f) - spool) * spoolK;
                    turboPh += (1800f + 6400f * spool) * dt; if (turboPh > 1f) turboPh -= 1f;
                    forced += Dsp.Sin(turboPh) * 0.012f * spool;
                    if (bov > 0.001f) { bovBand.Process(nz); forced += bovBand.bp * bov * 0.3f; bov *= bovDecay; }
                }

                // ---- the starter: motor whine and gear mesh, grinding, labouring through every compression
                float starter = 0f;
                if (crankOnly || crankFx > 0f)
                {
                    float spin = crankOnly ? 1f : crankFx / 0.18f;
                    float drive = crankOnly ? inst : crankBase * spin;
                    starterPh += drive / 60f * 14f * dt; if (starterPh > 1f) starterPh -= 1f;
                    starterGrind.Process(nz);
                    float strain = crankOnly ? 1f - Mathf.Clamp01(inst / Mathf.Max(1f, curRpm)) : 0f;   // drawing hard through compression
                    starter = (Dsp.Tanh(3f * Dsp.Sin(starterPh * 9f)) * 0.04f + Dsp.Sin(starterPh * 23f) * 0.022f + starterGrind.bp * 0.06f) * spin * (0.7f + strain * 1.5f);
                }

                mech *= 0.6f;
                exOut[i] = exhaust + mech * 0.12f;
                bayOut[i] = mech + intake * 0.7f + forced + starter + leaky * 1.6f + (exhaust + m * 0.3f) * exBleed;
            }

            // DC blockers, level and soft clip
            for (int i = 0; i < n; i++)
            {
                float e = exOut[i], b = bayOut[i];
                hpY = e - hpX + 0.995f * hpY; hpX = e;
                hpY2 = b - hpX2 + 0.995f * hpY2; hpX2 = b;
                exOut[i] = Dsp.Tanh(hpY * level * 1.5f) * 0.9f;
                bayOut[i] = Dsp.Tanh(hpY2 * level * 1.5f) * 0.9f;
            }
            if (float.IsNaN(hpY) || float.IsNaN(hpY2) || float.IsNaN(curRpm))
            {
                hpY = hpX = hpY2 = hpX2 = ex1 = ex2 = in1 = in2 = pipeLp = raspLp = mPrev = 0f; curRpm = 0f;
                System.Array.Clear(pipe, 0, pipe.Length); System.Array.Clear(ring, 0, ring.Length);
            }
        }

        /// <summary>Render both outputs offline (tests, previews) with the current inputs.</summary>
        public void Offline(float[] exhaust, float[] bay, int rate, int from = 0, int count = -1)
        {
            if (count < 0) count = exhaust.Length - from;
            var tmp = new float[512];
            for (int done = 0; done < count;)
            {
                int k = Mathf.Min(512, count - done);
                Pull(0, tmp, k, rate); System.Array.Copy(tmp, 0, exhaust, from + done, k);
                Pull(1, tmp, k, rate); System.Array.Copy(tmp, 0, bay, from + done, k);
                done += k;
            }
        }
    }
}
