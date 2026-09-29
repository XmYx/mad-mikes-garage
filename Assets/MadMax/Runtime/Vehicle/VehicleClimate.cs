using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Cabin heating (engine heat) and air conditioning (engine load: a little more fuel). K while driving
    /// switches AUTO / OFF. <see cref="CabinTemperature"/> feeds the driver's body temperature.</summary>
    public class VehicleClimate : MonoBehaviour
    {
        public bool on = true;
        public float CabinTemperature { get; private set; } = 20f;
        public bool Enclosed { get; private set; } = true;
        VehicleSystems sys;
        VehicleDriver driver;

        void Awake()
        {
            sys = GetComponent<VehicleSystems>(); driver = GetComponent<VehicleDriver>();
            var n = gameObject.name;
            Enclosed = !(n.StartsWith("Paver") || n.StartsWith("Roller") || GetComponent<BikeBalance>() || n.StartsWith("Ultralight") || n.StartsWith("DuneBuggy"));   // open platforms, bikes, open cockpits
            CabinTemperature = MadMax.World.Weather.Temperature;
        }

        void Update()
        {
            float outside = MadMax.World.Weather.Temperature;
            float target = outside;
            bool running = sys && driver && driver.Occupied && sys.fuel > 0f;
            if (Enclosed) target = Mathf.Lerp(outside, 18f, 0.25f);                    // shelter from wind
            if (on && running && Enclosed)
            {
                bool heat = outside < 18f && sys.Temperature > 50f;
                bool cool = outside > 24f;
                if (heat) target = 21f;
                if (cool) { target = 22f; sys.fuelMultiplier *= 1.1f; }
            }
            CabinTemperature = Mathf.MoveTowards(CabinTemperature, target, Time.deltaTime * (on && running ? 0.4f : 0.1f));
        }
    }
}
