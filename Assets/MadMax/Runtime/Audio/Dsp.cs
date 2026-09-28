using UnityEngine;

namespace MadMax.Audio
{
    /// <summary>Topology-preserving state-variable filter (Zavalishin). Set coefficients per block, process per sample.</summary>
    public struct Svf
    {
        float a1, a2, a3, k, ic1, ic2;
        public float lp, bp, hp;

        public void Set(float hz, float q, int sr)
        {
            float g = Mathf.Tan(Mathf.PI * Mathf.Clamp(hz, 10f, sr * 0.45f) / sr);
            k = 1f / Mathf.Max(0.1f, q);
            a1 = 1f / (1f + g * (g + k)); a2 = g * a1; a3 = g * a2;
        }

        public void Process(float x)
        {
            float v3 = x - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2f * v1 - ic1; ic2 = 2f * v2 - ic2;
            bp = v1; lp = v2; hp = x - k * v1 - v2;
            if (float.IsNaN(ic1) || float.IsNaN(ic2)) { ic1 = ic2 = 0f; }
        }
    }

    /// <summary>Allocation-free xorshift noise for the audio thread.</summary>
    public struct Noise
    {
        uint s;
        public Noise(uint seed) { s = seed == 0 ? 0x9E3779B9u : seed; }
        public float Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 8388608f - 1f; }   // -1..1
        public float Unit() => (Next() + 1f) * 0.5f;                                                               // 0..1
    }

    public static class Dsp
    {
        /// <summary>One-pole smoothing coefficient for a time constant (seconds) at the sample rate.</summary>
        public static float Coef(float seconds, int sr) => 1f - Mathf.Exp(-1f / (Mathf.Max(1e-4f, seconds) * sr));
        public static float Tanh(float x) { if (x > 4f) return 1f; if (x < -4f) return -1f; float x2 = x * x; return x * (27f + x2) / (27f + 9f * x2); }
        public static float Sin(float phase01) => Mathf.Sin(phase01 * 2f * Mathf.PI);
    }
}
