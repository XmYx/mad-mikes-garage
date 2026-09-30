using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Runtime functions of the depth-stage-D parts, by part id (called from <see cref="PartFunctions.Setup"/>
    /// and <see cref="VehicleSystems"/>): lamp beams of the LED bar, fog pods and searchlight pod, the flame-spitter
    /// stack, where the new exhausts breathe out, and the cooling of the big-core radiator and the oil-cooler combo.</summary>
    public static class MetalPartFunctions
    {
        const float V = 0.08f;
        static readonly Color White = new Color(1f, 0.97f, 0.92f), Led = new Color(0.9f, 0.95f, 1f), Fog = new Color(1f, 0.8f, 0.42f);

        public static void Setup(VehiclePart p)
        {
            switch (p.partId)
            {
                case "lights_led_bar":
                    // four wide, bright floods across the bar: wider and further than the old light bar (55°, 45 m)
                    Lamp(p, false, B(-0.8f, 0.3f, 0.12f, 70f, 60f, 70f, 5f, Led), B(-0.27f, 0.3f, 0.12f, 60f, 70f, 70f, 4f, Led),
                                   B(0.27f, 0.3f, 0.12f, 60f, 70f, 70f, 4f, Led), B(0.8f, 0.3f, 0.12f, 70f, 60f, 70f, 5f, Led));
                    break;
                case "lights_fog":
                    // amber pods: very wide, short and pitched at the road just ahead (cut under fog and dust)
                    Lamp(p, false, B(-0.48f, 0.32f, 0.2f, 85f, 28f, 60f, 14f, Fog), B(0.48f, 0.32f, 0.2f, 85f, 28f, 60f, 14f, Fog));
                    break;
                case "lights_search_pod":
                    // one tight beam on the aimed "lamp" segment, half again the old searchlight's reach
                    Lamp(p, true, B(0f, 0.28f, 0.3f, 11f, 140f, 420f, 0f, White));
                    break;
                case "exhaust_flame_stack": p.gameObject.AddComponent<FlameStack>(); break;
            }
        }

        static PartLight.Beam B(float x, float y, float z, float angle, float range, float intensity, float pitch, Color c) =>
            new PartLight.Beam { at = new Vector3(x, y, z), angle = angle, range = range, intensity = intensity, pitch = pitch, color = c };

        static void Lamp(VehiclePart p, bool tracks, params PartLight.Beam[] beams)
        {
            var l = p.gameObject.AddComponent<PartLight>();
            l.specs = beams; l.tracksAim = tracks;
        }

        /// <summary>Outlet of the new exhausts (world point and direction); false for other parts.</summary>
        public static bool Outlet(VehiclePart part, int i, out Vector3 at, out Vector3 dir)
        {
            var t = part.transform;
            switch (part.partId)
            {
                case "exhaust_twin_chrome":
                    at = t.TransformPoint(((i & 1) == 0 ? 0f : 3f) * V, 1f * V, -17.5f * V); dir = -t.forward; return true;
                case "exhaust_flame_stack":
                    at = t.TransformPoint(2f * V, FlameStack.TipY * V, -1.5f * V); dir = (t.up * 0.85f - t.forward * 0.3f).normalized; return true;
            }
            at = default; dir = default;
            return false;
        }

        /// <summary>Heat the radiator carries away, relative to a stock core.</summary>
        public static float Cooling(string partId) => partId == "radiator_bigcore" ? 1.5f : partId == "radiator_oil_cooler" ? 1.3f : 1f;

        /// <summary>How fast the engine oil breaks down: the oil cooler keeps it young.</summary>
        public static float OilWear(string partId) => partId == "radiator_oil_cooler" ? 0.55f : 1f;
    }
}
