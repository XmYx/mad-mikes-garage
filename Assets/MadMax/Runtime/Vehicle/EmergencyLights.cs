using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Emergency bar function (roadmap 19): while the vehicle's lights are on a red and a blue lamp sweep in
    /// turn; a vehicle carrying one has a siren instead of a horn.</summary>
    public class EmergencyLights : MonoBehaviour
    {
        Light red, blue;
        VehiclePart part;
        VehicleLights lights;

        /// <summary>The vehicle has a mounted emergency bar (its horn is a siren).</summary>
        public static bool Fitted(VehicleDriver v)
        {
            if (!v) return false;
            foreach (var e in v.GetComponentsInChildren<EmergencyLights>()) if (e.part && e.part.Socket) return true;
            return false;
        }

        void Start()
        {
            part = GetComponent<VehiclePart>();
            red = Beacon(new Vector3(-0.5f, 0.35f, 0f), new Color(1f, 0.12f, 0.08f));
            blue = Beacon(new Vector3(0.5f, 0.35f, 0f), new Color(0.2f, 0.35f, 1f));
        }

        Light Beacon(Vector3 local, Color c)
        {
            var go = new GameObject("Beacon");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = 10f; l.intensity = 0f; l.shadows = LightShadows.None; l.enabled = false;
            return l;
        }

        void Update()
        {
            if (!red) return;
            if (!lights) lights = GetComponentInParent<VehicleLights>();
            bool on = part && part.Socket && lights && lights.On;
            float s = Mathf.Sin(Time.time * 7f);
            float a = on ? Mathf.Max(0f, s) : 0f, b = on ? Mathf.Max(0f, -s) : 0f;
            red.intensity = a * 5f; blue.intensity = b * 5f;
            red.enabled = a > 0.05f && LightBudget.Allowed(red);
            blue.enabled = b > 0.05f && LightBudget.Allowed(blue);
        }
    }
}
