using System.Collections;
using System.Collections.Generic;
using MadMax.Audio;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>The engine sound simulation (<see cref="EngineSynth"/>), rendered offline: idle pulses come at the firing
    /// rate of each engine's cylinders and cycle; the starter's rhythm follows the compression strokes; big engines idle
    /// low; wear brings the mechanical noise up and the compression down; an exhaust leak is heard at the engine; the
    /// fitted engines' idle follows their spec; rendering stays cheap.</summary>
    public static class EngineAudioScenarios
    {
        public static IEnumerable<Scenario> All() { yield return new EngineAudio(); }

        internal const int Rate = 48000;

        /// <summary>Run an engine steadily (or cranking) for <paramref name="seconds"/>; returns exhaust and bay after a settle.</summary>
        internal static (float[] ex, float[] bay) Run(string id, float seconds, bool crank = false, float wear = 0f, float leak = 0f, float oil = 1f)
        {
            var spec = EngineSpec.For(id);
            var s = new EngineSynth(EngineProfile.For(id, null, true, spec), 99u)
            {
                maxRpm = EnginePreview.MaxRpm(id), idleRpm = spec.idleRpm, rpm = spec.idleRpm, running = !crank, cranking = crank,
                crankCatches = false, fuelled = false, wear = wear, leak = leak, mufflerDamage = leak, oil = oil
            };
            var warm = new float[Rate]; var warmB = new float[Rate];
            s.Offline(warm, warmB, Rate);
            int n = Mathf.RoundToInt(seconds * Rate);
            var ex = new float[n]; var bay = new float[n];
            s.Offline(ex, bay, Rate);
            return (ex, bay);
        }

        /// <summary>Strength of a periodicity in the signal's envelope at <paramref name="hz"/> against its neighbours.</summary>
        internal static float Periodicity(float[] x, float hz)
        {
            var env = new float[x.Length]; float lp = 0f, mean = 0f;
            for (int i = 0; i < x.Length; i++) { lp += (Mathf.Abs(x[i]) - lp) * 0.02f; env[i] = lp; mean += lp; }
            mean /= x.Length;
            for (int i = 0; i < env.Length; i++) env[i] -= mean;
            float at = Goertzel(env, hz), side = 0.5f * (Goertzel(env, hz * 0.71f) + Goertzel(env, hz * 1.37f));
            return at / Mathf.Max(1e-9f, side);
        }

        static float Goertzel(float[] x, float hz)
        {
            float w = 2f * Mathf.PI * hz / Rate, c = 2f * Mathf.Cos(w), s1 = 0f, s2 = 0f;
            for (int i = 0; i < x.Length; i++) { float s0 = x[i] + c * s1 - s2; s2 = s1; s1 = s0; }
            return Mathf.Sqrt(Mathf.Max(0f, s1 * s1 + s2 * s2 - c * s1 * s2));
        }

        internal static float Rms(float[] x) { double s = 0; foreach (var v in x) s += v * v; return Mathf.Sqrt((float)(s / Mathf.Max(1, x.Length))); }

        /// <summary>RMS of the 2–6 kHz band (valve ticks, knocks, clatter).</summary>
        internal static float Treble(float[] x)
        {
            var hp = new Svf(); hp.Set(3500f, 0.6f, Rate);
            double s = 0;
            foreach (var v in x) { hp.Process(v); s += hp.bp * hp.bp; }
            return Mathf.Sqrt((float)(s / Mathf.Max(1, x.Length)));
        }

        internal static float Db(float r) => 20f * Mathf.Log10(Mathf.Max(1e-9f, r));
    }

    class EngineAudio : Scenario
    {
        public override string Id => "audio.engine";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            // firing rate: four-strokes fire cylinders/2 times a turn, two-strokes every cylinder every turn
            foreach (var id in new[] { "engine_single", "engine_i4", "engine_i6", "engine_v8_blower", "engine_v12_last", "engine_truck_diesel", "engine_2stroke" })
            {
                var spec = EngineSpec.For(id);
                float fire = spec.idleRpm / 60f * spec.cylinders * (spec.twoStroke ? 1f : 0.5f);
                var (ex, _) = EngineAudioScenarios.Run(id, 1.5f);
                float p = EngineAudioScenarios.Periodicity(ex, fire);
                c.Metric(id + "_firing_peak", p, "x");
                c.Check(p > 4f, $"{id}: idle pulses at the firing rate {fire:0.0} Hz ({spec.cylinders} cyl, {(spec.twoStroke ? "2" : "4")}-stroke, {spec.idleRpm:0} rpm): {p:0.0}x the neighbours");
                yield return null;
            }
            // the crank's rhythm: the starter slows on each compression stroke
            {
                var spec = EngineSpec.For("engine_i4");
                var (ex, bay) = EngineAudioScenarios.Run("engine_i4", 1.2f, crank: true);
                float crankRpm = 250f / (0.8f + 0.2f * Mathf.Sqrt(spec.flywheel));
                float comp = crankRpm / 60f * 2f;
                float p = Mathf.Max(EngineAudioScenarios.Periodicity(bay, comp), EngineAudioScenarios.Periodicity(bay, comp * 0.97f), EngineAudioScenarios.Periodicity(bay, comp * 1.03f));
                c.Check(p > 2f && EngineAudioScenarios.Rms(bay) > 0.003f, $"cranking an i4: the starter labours on each compression ({comp:0.0} Hz): {p:0.0}x");
            }
            // size: big engines idle low
            float truck = EngineSpec.For("engine_truck_diesel").idleRpm, four = EngineSpec.For("engine_i4").idleRpm, single = EngineSpec.For("engine_single").idleRpm;
            c.Check(truck < four && four < single, $"idle by size: 14.8 L truck diesel {truck:0}, 2 L four {four:0}, 0.45 L single {single:0} rpm");
            // wear and a leak, proportionally
            var good = EngineAudioScenarios.Run("engine_i4", 1.5f);
            var worn = EngineAudioScenarios.Run("engine_i4", 1.5f, wear: 0.4f);
            var wrecked = EngineAudioScenarios.Run("engine_i4", 1.5f, wear: 0.8f, oil: 0.3f);
            float tGood = EngineAudioScenarios.Treble(good.bay), tWorn = EngineAudioScenarios.Treble(worn.bay), tWreck = EngineAudioScenarios.Treble(wrecked.bay);
            c.Note($"engine bay 2-6 kHz: good {EngineAudioScenarios.Db(tGood):0.0} dB, worn {EngineAudioScenarios.Db(tWorn):0.0}, wrecked + low oil {EngineAudioScenarios.Db(tWreck):0.0}");
            c.Check(tWorn > tGood * 1.15f && tWreck > tWorn * 1.3f, "wear rattles more, in proportion (ticks, knocks)");
            c.Check(EngineAudioScenarios.Rms(wrecked.ex) < EngineAudioScenarios.Rms(good.ex), $"worn cylinders push weaker pulses down the pipe ({EngineAudioScenarios.Db(EngineAudioScenarios.Rms(wrecked.ex)):0.0} vs {EngineAudioScenarios.Db(EngineAudioScenarios.Rms(good.ex)):0.0} dB)");
            var small = EngineAudioScenarios.Run("engine_i4", 1.5f, leak: 0.3f);
            var big = EngineAudioScenarios.Run("engine_i4", 1.5f, leak: 0.9f);
            float b0 = EngineAudioScenarios.Rms(good.bay), b1 = EngineAudioScenarios.Rms(small.bay), b2 = EngineAudioScenarios.Rms(big.bay);
            c.Check(b1 > b0 && b2 > b1 * 1.3f, $"an exhaust leak is heard at the engine, more as it grows: {EngineAudioScenarios.Db(b0):0.0} / {EngineAudioScenarios.Db(b1):0.0} / {EngineAudioScenarios.Db(b2):0.0} dB");
            yield return null;
            // the fitted engines idle at their spec
            int checkedN = 0, wrong = 0;
            foreach (var v in c.Game.AllVehicles)
            {
                if (!v || !v.Engine) continue;
                checkedN++;
                if (Mathf.Abs(v.Engine.idleRpm - v.Engine.Spec.idleRpm) > 1f) wrong++;
            }
            c.Check(checkedN > 0 && wrong == 0, $"fitted engines idle at their spec ({checkedN} vehicles, {wrong} off)");
            // cost: a worn V8 at 4000 rpm, 5 s
            var syn = new EngineSynth(EngineProfile.For("engine_v8_blower", null, true), 7u) { running = true, rpm = 4000f, load = 0.7f, maxRpm = 7200f, idleRpm = 750f, wear = 0.6f };
            var a = new float[EngineAudioScenarios.Rate * 5]; var b = new float[EngineAudioScenarios.Rate * 5];
            var sw = System.Diagnostics.Stopwatch.StartNew();
            syn.Offline(a, b, EngineAudioScenarios.Rate);
            float ms = sw.ElapsedMilliseconds;
            c.Metric("render_5s_ms", ms, "ms");
            c.Check(ms < 600f, $"rendering is cheap: 5 s of a worn V8 in {ms:0} ms");
        }
    }
}
