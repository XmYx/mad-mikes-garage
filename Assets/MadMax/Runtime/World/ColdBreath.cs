using UnityEngine;

namespace MadMax.World
{
    /// <summary>Breath you can see (2026-10-29): below <see cref="Below"/> °C every person and animal near the camera puffs
    /// a little cloud from the mouth / muzzle with each breath — slow and faint at rest, quick and thick when running,
    /// tired, or a big animal at the gallop; the cloud drifts on the wind and is gone in a second. Sizes scale with the
    /// lungs (a hen's wisp, a horse's double plume from the nostrils). Callers keep their own clock
    /// (<see cref="Tick"/>); nothing allocates.</summary>
    public static class ColdBreath
    {
        public const float Below = 4f, Reach = 35f;
        /// <summary>Puffs breathed out (tests).</summary>
        public static int Puffs;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Puffs = 0;

        /// <summary>0 at <see cref="Below"/> °C, 1 at −8 °C and below (rain makes the air colder to the eye).</summary>
        public static float Strength(Vector3 p)
        {
            float t = Weather.TemperatureAt(p.z) - (Weather.Raining ? 1.5f : 0f);
            return Mathf.Clamp01((Below - t) / 12f);
        }

        /// <summary>Advance a breathing clock and breathe out when it comes round. <paramref name="exertion"/> 0 (resting:
        /// a breath every ~4 s) .. 1 (running hard: ~1.3 a second); <paramref name="lungs"/> scales the cloud (1 = a person);
        /// <paramref name="nostrils"/> splits it into a pair of jets (horses, cattle).</summary>
        public static void Tick(ref float clock, Vector3 mouth, Vector3 fwd, float exertion, float lungs, float dt, bool nostrils = false)
        {
            if (dt <= 0f) return;
            float rate = Mathf.Lerp(0.25f, 1.3f, Mathf.Clamp01(exertion)) / Mathf.Clamp(Mathf.Sqrt(lungs), 0.6f, 1.8f);   // big lungs breathe slower
            clock += dt * rate;
            if (clock < 1f) return;
            clock -= Mathf.Floor(clock);
            if (!Building.PieceBits.Near(mouth, Reach)) return;
            float s = Strength(mouth);
            if (s <= 0f) return;
            var t = DeformableTerrain.Instance;
            if (t && t.WaterDepthNoLoad(mouth.x, mouth.z) > 0f && t.WaterLevel(mouth.x, mouth.z) > mouth.y) return;   // head under water
            Puffs++;
            fwd.y = Mathf.Min(fwd.y, 0.1f);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var wind = Fx.Wind * 0.25f;
            float size = 0.07f * Mathf.Pow(lungs, 0.4f) * (0.8f + 0.5f * exertion);
            float alpha = Mathf.Lerp(0.18f, 0.55f, s) * (0.75f + 0.35f * exertion);
            var c = new Color(0.92f, 0.94f, 0.97f, alpha);
            float speed = 0.35f + 0.5f * exertion;
            int n = 2 + Mathf.RoundToInt(exertion * 2f);
            var side = Vector3.Cross(Vector3.up, fwd);
            for (int i = 0; i < n; i++)
            {
                var off = nostrils ? side * ((i & 1) == 0 ? -0.05f : 0.05f) * Mathf.Sqrt(lungs) : Vector3.zero;
                var dir = nostrils ? fwd + Vector3.down * 0.35f + off * 4f : fwd;
                var v = dir * speed * Random.Range(0.7f, 1.2f) + Vector3.up * 0.08f + wind + Random.insideUnitSphere * 0.05f;
                Fx.Smoke(mouth + off + fwd * 0.03f, v, size * Random.Range(0.8f, 1.25f), c, Mathf.Lerp(0.8f, 1.4f, s));
            }
        }
    }
}
