using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Headlights (spots) and tail/brake lights, placed from the body bounds. Mode: auto (on at night while
    /// driven), on, off — N while driving. Brake lights flare with the brake.</summary>
    public class VehicleLights : MonoBehaviour
    {
        public int mode;                       // 0 auto, 1 on, 2 off
        public static readonly string[] ModeNames = { "LIGHTS AUTO", "LIGHTS ON", "LIGHTS OFF" };
        /// <summary>Headlights lit (light bars and searchlights follow).</summary>
        public bool On { get; private set; }
        Light[] heads, tails;
        VehicleDriver driver;

        void Start()
        {
            driver = GetComponent<VehicleDriver>();
            var body = transform.Find("Body");
            var mf = body ? body.GetComponent<MeshFilter>() : null;
            if (!mf || !mf.sharedMesh || !driver || !driver.driveable) { enabled = false; return; }
            var b = mf.sharedMesh.bounds;
            float y = Mathf.Lerp(b.min.y, b.max.y, 0.3f), hx = b.extents.x * 0.7f;
            heads = new Light[2]; tails = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -hx : hx;
                heads[i] = Make("Headlight", body, new Vector3(x, y, b.max.z + 0.05f), Quaternion.Euler(8f, 0f, 0f), LightType.Spot, new Color(1f, 0.93f, 0.8f), 28f, 60f);
                tails[i] = Make("Taillight", body, new Vector3(x, y, b.min.z - 0.08f), Quaternion.identity, LightType.Point, new Color(1f, 0.1f, 0.05f), 2f, 0.4f);
            }
            heads[1].enabled = false;                       // one spot per vehicle carries the beam (light budget), both tail lamps glow
            heads[0].transform.localPosition = new Vector3(0f, y, b.max.z + 0.05f);
            heads[0].spotAngle = 75f;
        }

        static Light Make(string name, Transform parent, Vector3 pos, Quaternion rot, LightType type, Color c, float range, float intensity)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = pos; l.transform.localRotation = rot;
            l.type = type; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            if (type == LightType.Spot) { l.spotAngle = 60f; l.innerSpotAngle = 30f; }
            return l;
        }

        void Update()
        {
            if (heads == null) return;
            bool on = mode == 1 || (mode == 0 && driver.Occupied && DayNight.Darkness > 0.3f);
            On = on;
            heads[0].enabled = on && LightBudget.Allowed(heads[0], true);
            float brake = driver.brakeInput > 0.1f || driver.handbrake && driver.Occupied ? 1f : 0f;
            foreach (var t in tails)
            {
                t.enabled = (on || brake > 0f && driver.Occupied) && LightBudget.Allowed(t, true);
                t.intensity = brake > 0f ? 0.6f : 0.25f;
            }
        }
    }
}
