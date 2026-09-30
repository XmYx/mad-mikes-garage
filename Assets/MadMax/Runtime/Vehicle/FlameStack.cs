using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Flame-spitter exhaust stack (depth stage D): lifting off the throttle high in the revs pops a string of
    /// flames out of the bell; while the nitrous burns it breathes fire the whole time. Show and noise, no damage.</summary>
    public class FlameStack : MonoBehaviour
    {
        /// <summary>Height of the bell mouth above the mount (voxels); the outlet in <see cref="MetalPartFunctions.Outlet"/>.</summary>
        public const float TipY = 13f;
        static readonly Color Flame = new Color(1f, 0.55f, 0.15f), Core = new Color(1f, 0.85f, 0.4f), Blue = new Color(0.45f, 0.6f, 1f);

        VehiclePart part;
        VehicleDriver driver;
        VehicleSystems sys;
        VehicleTuning tune;
        float lastCmd, popUntil, nextPop;

        void Awake() => part = GetComponent<VehiclePart>();

        void Update()
        {
            if (!part.Socket) { driver = null; lastCmd = 0f; return; }
            if (!driver)
            {
                driver = GetComponentInParent<VehicleDriver>();
                sys = driver ? driver.GetComponent<VehicleSystems>() : null;
                tune = driver ? driver.GetComponent<VehicleTuning>() : null;
            }
            if (!driver || !sys || !sys.Started || !driver.Engine) { lastCmd = 0f; return; }
            float cmd = driver.DriveCommand, rpm = driver.Rpm / Mathf.Max(1f, driver.Engine.maxRpm);
            if (lastCmd > 0.6f && cmd < 0.15f && rpm > 0.55f) popUntil = Time.time + Random.Range(0.35f, 0.75f);   // lift-off overrun
            lastCmd = cmd;
            bool nitrous = tune && tune.NitrousOn;
            if ((Time.time < popUntil || nitrous) && Time.time >= nextPop)
            {
                nextPop = Time.time + (nitrous ? 0.05f : Random.Range(0.07f, 0.16f));
                Spit(nitrous);
            }
        }

        void Spit(bool nitrous)
        {
            MetalPartFunctions.Outlet(part, 0, out var at, out var dir);
            var carry = driver.Body ? driver.Body.linearVelocity : Vector3.zero;
            Fx.Sparks(at, dir * 1.6f + carry * 0.15f, nitrous ? 2 : 4, nitrous ? Blue : Flame);
            Fx.Smoke(at + dir * 0.15f, dir * 2.5f + carry * 0.6f, nitrous ? 0.22f : 0.3f, nitrous ? Blue : Core, 0.18f);
            if (nitrous) return;
            Fx.Flash(at, Flame, 5f, 2.2f, 0.07f);
            MadMax.Audio.Sfx.Play("pop", at, 0.75f, Random.Range(0.75f, 1.15f), 60f, 0.05f);
        }
    }
}
