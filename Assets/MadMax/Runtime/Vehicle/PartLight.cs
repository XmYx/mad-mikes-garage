using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Light bar (two wide floods) or searchlight (one narrow beam on the "lamp" segment that follows the
    /// driver's aim). Switches with the vehicle's headlights (N). Other lamp parts set <see cref="specs"/> (and
    /// <see cref="tracksAim"/>) before Start.</summary>
    public class PartLight : MonoBehaviour
    {
        /// <summary>One spot beam: local position (on the "lamp" segment when tracking), cone angle, range (m), intensity, pitch down, colour.</summary>
        public struct Beam { public Vector3 at; public float angle, range, intensity, pitch; public Color color; }
        [System.NonSerialized] public Beam[] specs;
        [System.NonSerialized] public bool tracksAim;

        Light[] beams;
        Transform lamp;
        VehiclePart part;
        bool search;

        void Start()
        {
            part = GetComponent<VehiclePart>();
            search = part.partId == "lights_search" || tracksAim;
            lamp = transform.Find("lamp");
            if (specs != null)
            {
                beams = new Light[specs.Length];
                for (int i = 0; i < specs.Length; i++)
                    beams[i] = Make(search && lamp ? lamp : transform, specs[i].at, specs[i].angle, specs[i].range, specs[i].intensity, specs[i].pitch, specs[i].color);
            }
            else if (search && lamp)
                beams = new[] { Make(lamp, new Vector3(0f, 0.24f, 0.3f), 16f, 90f, 260f) };
            else
                beams = new[] { Make(transform, new Vector3(-0.5f, 0.3f, 0.15f), 55f, 45f, 90f), Make(transform, new Vector3(0.5f, 0.3f, 0.15f), 55f, 45f, 90f) };
        }

        static Light Make(Transform parent, Vector3 at, float angle, float range, float intensity, float pitch = 6f, Color color = default)
        {
            var l = new GameObject("Beam").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = at;
            l.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            l.type = LightType.Spot; l.spotAngle = angle; l.innerSpotAngle = angle * 0.6f;
            l.range = range; l.intensity = intensity; l.color = color.a > 0f ? color : new Color(1f, 0.96f, 0.88f); l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }

        void Update()
        {
            if (beams == null) return;
            var lights = part.Socket ? GetComponentInParent<VehicleLights>() : null;
            bool on = lights && lights.On;
            foreach (var b in beams) b.enabled = on && LightBudget.Allowed(b, true);
            if (!search || !lamp || !on) return;
            // the searchlight tracks what the driver aims at
            var weapons = GetComponentInParent<VehicleWeapons>();
            if (!weapons || !weapons.Aiming) return;
            var local = transform.InverseTransformPoint(weapons.Aim) - lamp.localPosition;
            if (local.sqrMagnitude < 0.01f) return;
            var want = Quaternion.LookRotation(local.normalized, Vector3.up);
            lamp.localRotation = Quaternion.RotateTowards(lamp.localRotation, want, 150f * Time.deltaTime);
        }
    }
}
