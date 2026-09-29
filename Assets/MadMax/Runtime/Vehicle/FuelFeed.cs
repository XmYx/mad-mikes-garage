using MadMax.Building;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Onboard generator: tops its small tank up from the vehicle's fuel tank while mounted.</summary>
    public class FuelFeed : MonoBehaviour
    {
        float timer;

        void Update()
        {
            if ((timer -= Time.deltaTime) > 0f) return;
            timer = 2f;
            var part = GetComponent<VehiclePart>();
            if (!part || !part.Socket || !TryGetComponent<Generator>(out var gen)) return;
            var sys = GetComponentInParent<VehicleSystems>();
            if (!sys) return;
            float want = gen.tankLitres - gen.fuel;
            if (want < 1f || sys.fuel < 3f) return;
            float t = Mathf.Min(want, sys.fuel - 2f);
            sys.fuel -= t; gen.fuel += t;
        }
    }
}
