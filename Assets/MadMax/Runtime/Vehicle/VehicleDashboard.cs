using MadMax.Rendering;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Diegetic gauge cluster in front of the driver: speed and rpm dials, gear, fuel and temperature strips
    /// (<see cref="VehicleGauges"/>), warning lamps. With the HD pack on it is real geometry (<see cref="DashboardHD"/>),
    /// otherwise a 64x24 pixel canvas.</summary>
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
        DashboardHD hd;
        float next, speedF, rpmF;
        static Material hdMat;
        /// <summary>The HD gauge cluster is in use.</summary>
        public bool IsHD => hd != null;
        /// <summary>The fuel / temperature reading drawn last.</summary>
        public VehicleGauges.Reading Last { get; private set; }

        static readonly Color32 Bg = new Color32(20, 13, 10, 255), Rim = new Color32(90, 55, 32, 255), Tick = new Color32(214, 180, 130, 255),
            Needle = new Color32(255, 110, 40, 255), Amber = new Color32(255, 180, 60, 255), Off = new Color32(50, 34, 26, 255);
        static readonly Color32 Ghost = new Color32(30, 20, 15, 255);                                // unlit segments / LEDs (HD)
        static Color32 Red => MadMax.Game.PixelHud.Bad;

        void Start()
        {
            driver = GetComponent<VehicleDriver>();
            chassis = GetComponent<VehicleChassis>();
            var eye = transform.Find("DriverEye");
            if (!eye) { enabled = false; return; }
            if (!Application.isBatchMode && MadMax.Rendering.HDAssets.Enabled && HDMaterial())
            {
                hd = new DashboardHD();
                var go = hd.Build(transform, hdMat, Bg, Rim, Tick, Needle, Red, Ghost);
                go.transform.localPosition = eye.localPosition + offsetFromEye;
                go.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
                float k = size.x / DashboardHD.W;
                go.transform.localScale = new Vector3(k, k, k);
                Draw();
                return;
            }
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

        static bool HDMaterial()
        {
            if (hdMat) return true;
            var src = Resources.Load<Material>("RuntimeMaterials/HDLitVoxel");
            if (!src) return false;
            hdMat = new Material(src) { name = "DashboardHD" };
            hdMat.SetFloat("_Unlit", 1f);                                                     // backlit instruments
            hdMat.SetFloat("_OutlinePx", 0f);
            hdMat.SetFloat("_SnowMask", 0f);
            return true;
        }

        void Update()
        {
            if (hd != null && driver.Occupied)
            {
                // needles move every frame (smoothly), the rest at the refresh rate
                var e = driver.Engine;
                speedF = Mathf.Lerp(speedF, Mathf.Abs(driver.SpeedKmh) / maxSpeedKmh, Time.deltaTime * 10f);
                rpmF = Mathf.Lerp(rpmF, e ? driver.Rpm / e.maxRpm : 0f, Time.deltaTime * 14f);
                hd.SetNeedle(hd.speedNeedle, speedF);
                hd.SetNeedle(hd.rpmNeedle, rpmF);
            }
            if ((canvas == null && hd == null) || !driver.Occupied || Time.time < next) return;
            next = Time.time + 1f / refreshRate;
            Draw();
        }

        void Draw()
        {
            if (hd != null) { DrawHD(); return; }
            canvas.Clear(Bg);
            canvas.Frame(0, 0, 64, 24, Rim);
            float speed = Mathf.Abs(driver.SpeedKmh);                                         // the dial reads 0..max whatever the units
            var eng = driver.Engine;
            canvas.Dial(11, 12, 10, speed / maxSpeedKmh, Tick, Needle);
            canvas.Dial(52, 12, 10, eng ? driver.Rpm / eng.maxRpm : 0f, Tick, Needle, 0.85f);
            string gear = !eng ? "-" : driver.Reversing ? "R" : driver.manual && driver.Gear == 0 ? "N" : driver.Gear.ToString();
            canvas.Text(29, 3, gear, Amber, 2, false);
            string s = Mathf.RoundToInt(speed).ToString("000");
            canvas.Text(26, 14, s, Tick, 1, false);

            var sysG = GetComponent<VehicleSystems>();
            if (sysG)
            {
                var rd = VehicleGauges.Read(sysG);
                Last = rd;
                VehicleGauges.DrawFuelBar(canvas, 23, 2, 16, rd, Off, Tick);
                VehicleGauges.DrawTempBar(canvas, 39, 2, 16, rd, Off, Tick);
            }

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
                Lamp(53, 20, "S", (f & (Fault.ServiceDue | Fault.Clogged | Fault.Misfire)) != 0 ? Amber : Off);
            }
            canvas.Upload();
        }

        void DrawHD()
        {
            float speed = Mathf.Abs(driver.SpeedKmh);
            var eng = driver.Engine;
            string gear = !eng ? "-" : driver.Reversing ? "R" : driver.manual && driver.Gear == 0 ? "N" : driver.Gear.ToString();
            hd.SetText(gear.Length > 1 ? gear.Substring(gear.Length - 1) : gear, Mathf.Min(999, Mathf.RoundToInt(speed)).ToString().PadLeft(3), Amber, Tick, Ghost);
            var sys = GetComponent<VehicleSystems>();
            if (sys)
            {
                var rd = VehicleGauges.Read(sys);
                Last = rd;
                hd.SetBars(rd.fuelShown ? rd.fuelNeedle : 0f, rd.tempShown ? rd.tempNeedle : 0f, rd.low ? Amber : Tick, VehicleGauges.ZoneColour(rd.zone, Tick), Ghost);
            }
            bool wheelMissing = false;
            foreach (var sk in chassis.Sockets) if (sk.accepts == PartCategory.Wheel && !sk.Current) wheelMissing = true;
            var f = sys ? sys.Faults : default(Fault);
            hd.SetLamp(0, (f & (Fault.NoFuel | Fault.LowFuel | Fault.FuelLeak)) != 0 ? Amber : Off);
            hd.SetLamp(1, driver.handbrake ? Red : Off);
            hd.SetLamp(2, !eng ? Red : eng.GetComponent<VehiclePart>().damage > 0.5f ? Amber : Off);
            hd.SetLamp(3, wheelMissing ? Red : Off);
            hd.SetLamp(4, driver.Mud > 0.5f ? Amber : Off);
            hd.SetLamp(5, (f & (Fault.NoOil | Fault.LowOil | Fault.OilLeak | Fault.Seized)) != 0 ? Red : Off);
            hd.SetLamp(6, (f & Fault.Overheat) != 0 ? Red : (f & (Fault.LowCoolant | Fault.CoolantLeak)) != 0 ? Amber : Off);
            hd.SetLamp(7, (f & (Fault.ServiceDue | Fault.Clogged | Fault.Misfire)) != 0 ? Amber : Off);
            hd.SetLamp(8, sys && !sys.Started ? Amber : Off);
            hd.Upload();
        }

        void Lamp(int x, int y, string c, Color32 col)
        {
            if (col.r == Off.r && col.g == Off.g) canvas.Rect(x, y, 3, 3, Off);
            else canvas.Rect(x, y, 3, 3, col);
        }
    }
}
