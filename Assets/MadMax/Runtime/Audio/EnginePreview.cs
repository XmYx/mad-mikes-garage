using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>Renders an engine's sound offline to a WAV for listening and analysis (tests, the editor): a scripted
    /// run — crank and catch, idle, a rev to near the limiter, a lift (overrun), idle, switch off and coast — with a
    /// simple flywheel model standing in for the drivetrain. Stereo: left = exhaust voice, right = engine bay voice.</summary>
    public static class EnginePreview
    {
        public struct Condition
        {
            public float wear, oil, misfire, ping, cold, leak, muffler, clog;
            public bool catches;
            public static Condition Good => new Condition { oil = 1f, catches = true };
        }

        public const float Length = 10.5f;

        /// <summary>Render the scripted run to <paramref name="wavPath"/>; returns the peak level.</summary>
        public static float Render(string engineId, string exhaustId, Condition cond, string wavPath, int rate = 48000)
        {
            var spec = EngineSpec.For(engineId);
            float maxRpm = MaxRpm(engineId), idle = spec.idleRpm;
            var synth = new EngineSynth(EngineProfile.For(engineId, exhaustId, true, spec), 12345u)
            {
                maxRpm = maxRpm, idleRpm = idle, wear = cond.wear, oil = cond.oil, misfire = cond.misfire, ping = cond.ping, cold = cond.cold,
                leak = cond.leak, mufflerDamage = cond.muffler, clog = cond.clog
            };
            int total = Mathf.RoundToInt(Length * rate), step = 512;
            var ex = new float[total]; var bay = new float[total];
            float rpm = 0f, tau = 0.35f * Mathf.Sqrt(Mathf.Max(0.3f, spec.flywheel));
            for (int at = 0; at < total; at += step)
            {
                float t = at / (float)rate;
                bool crank = t >= 0.3f && t < 1.6f, run = cond.catches && t >= 1.6f && t < 8.5f;
                float throttle = t >= 4f && t < 5.4f ? 1f : 0f;
                float target = run ? idle + throttle * (maxRpm - idle) * 0.95f : 0f;
                rpm += (target - rpm) * (1f - Mathf.Exp(-(step / (float)rate) / (throttle > 0f ? tau : tau * 0.8f)));
                synth.cranking = crank; synth.crankCatches = cond.catches; synth.crankProgress = crank ? (t - 0.3f) / 1.3f : 0f;
                synth.running = run; synth.rpm = Mathf.Max(rpm, run ? idle : 0f); synth.load = throttle;
                synth.Offline(ex, bay, rate, at, Mathf.Min(step, total - at));
            }
            float peak = 0f;
            for (int i = 0; i < total; i++) peak = Mathf.Max(peak, Mathf.Max(Mathf.Abs(ex[i]), Mathf.Abs(bay[i])));
            WriteWav(wavPath, ex, bay, rate);
            return peak;
        }

        /// <summary>Redline of the engine part (the part library's numbers), for previews outside a vehicle.</summary>
        public static float MaxRpm(string id)
        {
            switch (id)
            {
                case "engine_truck_diesel": case "engine_marine_diesel": return 2600f;
                case "engine_diesel_i6": return 4400f;
                case "engine_i6": return 5200f;
                case "engine_i4": return 6200f;
                case "engine_v8_blower": return 7200f;
                case "engine_v8_forged": case "engine_v12_last": return 7800f;
                case "engine_2stroke": return 4800f;
                case "engine_2stroke_aero": return 6500f;
                case "engine_outboard": case "engine_vtwin": return 5800f;
                case "engine_single": return 9500f;
                default: return 6000f;
            }
        }

        static void WriteWav(string path, float[] l, float[] r, int rate)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            using var w = new System.IO.BinaryWriter(System.IO.File.Create(path));
            int n = l.Length, bytes = n * 4;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(rate); w.Write(rate * 4); w.Write((short)4); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            for (int i = 0; i < n; i++)
            {
                w.Write((short)Mathf.Clamp(Mathf.RoundToInt(l[i] * 32000f), -32767, 32767));
                w.Write((short)Mathf.Clamp(Mathf.RoundToInt(r[i] * 32000f), -32767, 32767));
            }
        }
    }
}
