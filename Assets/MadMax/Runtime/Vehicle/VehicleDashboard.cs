using MadMax.Rendering;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Diegetic gauge cluster in front of the driver: speed and rpm dials, gear, warning lamps.</summary>
    [RequireComponent(typeof(VehicleDriver))]
    public class VehicleDashboard : MonoBehaviour
    {
        public Vector3 offsetFromEye = new Vector3(0f, -0.14f, 0.42f);
        public Vector2 size = new Vector2(0.3f, 0.11f);
        public float maxSpeedKmh = 200f;
        public float refreshRate = 20f;

        VehicleDriver driver;
        VehicleChassis chassis;
        PixelCanvas canvas;
        float next;

        static readonly Color32 Bg = new Color32(20, 13, 10, 255), Rim = new Color32(90, 55, 32, 255), Tick = new Color32(214, 180, 130, 255),
            Needle = new Color32(255, 110, 40, 255), Amber = new Color32(255, 180, 60, 255), Red = new Color32(230, 40, 30, 255), Off = new Color32(50, 34, 26, 255);

        void Start()
        {
            driver = GetComponent<VehicleDriver>();
            chassis = GetComponent<VehicleChassis>();
            var eye = transform.Find("DriverEye");
            if (!eye) { enabled = false; return; }
            canvas = new PixelCanvas(64, 24);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(quad.GetComponent<Collider>());   // a non-convex mesh collider under a rigidbody is invalid
            quad.name = "Dashboard";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = eye.localPosition + offsetFromEye;
            quad.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var shader = MadMax.World.Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit");
            if (!shader || Application.isBatchMode) { enabled = false; Destroy(quad); return; }   // stripped in builds without a reference / headless server
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", canvas.texture);
            mat.mainTexture = canvas.texture;
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
            quad.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Draw();
        }

        void Update()
        {
            if (canvas == null || !driver.Occupied || Time.time < next) return;
            next = Time.time + 1f / refreshRate;
            Draw();
        }

        void Draw()
        {
            canvas.Clear(Bg);
            canvas.Frame(0, 0, 64, 24, Rim);
            float speed = Mathf.Abs(driver.SpeedKmh);
            var eng = driver.Engine;
            canvas.Dial(11, 12, 10, speed / maxSpeedKmh, Tick, Needle);
            canvas.Dial(52, 12, 10, eng ? driver.Rpm / eng.maxRpm : 0f, Tick, Needle, 0.85f);
            string gear = !eng ? "-" : driver.Reversing ? "R" : driver.manual && driver.Gear == 0 ? "N" : driver.Gear.ToString();
            canvas.Text(29, 3, gear, Amber, 2, false);
            string s = Mathf.RoundToInt(speed).ToString("000");
            canvas.Text(26, 14, s, Tick, 1, false);

            bool wheelMissing = false;
            foreach (var sk in chassis.Sockets) if (sk.accepts == PartCategory.Wheel && !sk.Current) wheelMissing = true;
            Lamp(23, 20, "P", driver.handbrake ? Red : Off);
            Lamp(28, 20, "E", !eng ? Red : eng.GetComponent<VehiclePart>().damage > 0.5f ? Amber : Off);
            Lamp(33, 20, "W", wheelMissing ? Red : Off);
            Lamp(38, 20, "M", driver.Mud > 0.5f ? Amber : Off);
            var sys = GetComponent<VehicleSystems>();
            if (sys)
            {
                var f = sys.Faults;
                Lamp(15, 20, "F", (f & (Fault.NoFuel | Fault.LowFuel | Fault.FuelLeak)) != 0 ? Amber : Off);
                Lamp(43, 20, "O", (f & (Fault.NoOil | Fault.LowOil | Fault.OilLeak | Fault.Seized)) != 0 ? Red : Off);
                Lamp(48, 20, "T", (f & Fault.Overheat) != 0 ? Red : (f & (Fault.LowCoolant | Fault.CoolantLeak)) != 0 ? Amber : Off);
            }
            canvas.Upload();
        }

        void Lamp(int x, int y, string c, Color32 col)
        {
            if (col.r == Off.r && col.g == Off.g) canvas.Rect(x, y, 3, 3, Off);
            else canvas.Rect(x, y, 3, 3, col);
        }
    }
}
