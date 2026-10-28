using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Fresh paint drying (2026-10-28, from the paint station): for a minute and a half dust sticks to it — driving
    /// over dry loose ground or through a dust storm cakes grime on (<see cref="VehicleGrime.dirt"/>) far faster than on
    /// dry paint, rain spots it. Removes itself when dry.</summary>
    public class WetPaint : MonoBehaviour
    {
        public float until;
        bool told;
        VehicleDriver car;
        VehicleGrime grime;

        /// <summary>Grit picked up while wet (tests).</summary>
        public float Gritted { get; private set; }
        public bool Wet => Time.time < until;

        public static WetPaint Mark(VehicleDriver v, float seconds)
        {
            if (!v) return null;
            if (!v.TryGetComponent<WetPaint>(out var w)) w = v.gameObject.AddComponent<WetPaint>();
            w.until = Time.time + seconds; w.told = false;
            return w;
        }

        void Awake() { car = GetComponent<VehicleDriver>(); grime = GetComponent<VehicleGrime>(); }

        void Update()
        {
            if (!Wet) { Destroy(this); return; }
            if (!car || !grime) return;
            float kmh = Mathf.Abs(car.SpeedKmh);
            bool storm = MadMax.World.Storms.Dust > 0.3f;
            bool wet = MadMax.World.Weather.Raining;
            float rate = (kmh > 12f ? Mathf.Clamp01((kmh - 12f) / 40f) * (MadMax.World.Weather.Wetness < 0.3f ? 0.05f : 0.01f) : 0f) + (storm ? 0.04f : 0f) + (wet ? 0.008f : 0f);
            if (rate <= 0f) return;
            float add = rate * Time.deltaTime;
            grime.dirt = Mathf.Min(1f, grime.dirt + add);
            Gritted += add;
            if (!told && Gritted > 0.02f)
            {
                told = true;
                var g = MadMax.Game.WastelandGame.Instance;
                if (g && g.Current == car) g.Toast(wet ? "RAIN SPOTS THE WET PAINT" : "GRIT IS STICKING TO THE WET PAINT");
            }
        }
    }
}
