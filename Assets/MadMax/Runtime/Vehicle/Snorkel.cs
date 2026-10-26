using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Raised air intake: while mounted, the engine breathes at the scoop, so it only floods when the water
    /// rises above it (see VehicleSystems). Wading on it shows (2026-10-23): a bow wave curls round the pipe where it
    /// enters the water (by speed), the intake gurgles and spits spray once the water is within a hand's width of the
    /// mouth, the driver is told how close it is, and the pipe drips for a while after the car climbs out.</summary>
    public class Snorkel : MonoBehaviour
    {
        VehiclePart part;
        void Awake() => part = GetComponent<VehiclePart>();

        public bool Mounted => part && part.Socket;
        /// <summary>World position of the intake mouth (head of the pipe, facing forward).</summary>
        public Vector3 Intake => transform.TransformPoint(new Vector3(0.12f, 1.36f, 0.32f));

        /// <summary>Water below the mouth (m) while it is saving the engine; +inf when not wading (tests).</summary>
        public float Margin { get; private set; } = float.PositiveInfinity;
        public const float GurgleBelow = 0.3f, SprayBelow = 0.12f;
        float wetUntil, foamAcc, warnAt;

        /// <summary>From <c>VehicleSystems</c> each step: <paramref name="water"/> = the water level at the engine
        /// (NaN: none), <paramref name="engineIntake"/> = where the engine would breathe without the pipe.</summary>
        public void Wade(float water, float engineIntake, bool running, Vector3 velocity, bool player, MadMax.Game.WastelandGame g, float dt)
        {
            var mouth = Intake;
            bool wading = Mounted && !float.IsNaN(water) && water > engineIntake;
            Margin = wading ? mouth.y - water : float.PositiveInfinity;
            if (!wading)
            {
                MadMax.Audio.Sfx.Loop(this, "station_bubble", 0f, 1f, 15f);
                if (Time.time < wetUntil && Random.value < dt * 4f)                                     // the pipe drips after the crossing
                    Fx.Streak(mouth + Vector3.down * Random.Range(0.1f, 0.9f), Vector3.down * 1.5f, 0.02f, new Color(0.7f, 0.8f, 0.9f), 0.35f);
                return;
            }
            wetUntil = Time.time + 20f;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            var foot = new Vector3(mouth.x, water + 0.02f, mouth.z);
            foamAcc += dt * (1.5f + speed * 3f);
            while (foamAcc > 1f)
            {
                foamAcc -= 1f;
                var side = Random.insideUnitSphere; side.y = 0f;
                Fx.Foam(foot + side * 0.12f, side * 0.4f + velocity * 0.15f + Vector3.up * 0.1f, 0.07f + speed * 0.008f, new Color(0.88f, 0.9f, 0.86f, 0.8f), 2f);
            }
            float close = Mathf.Clamp01(1f - Margin / GurgleBelow);
            MadMax.Audio.Sfx.Loop(this, "station_bubble", running ? close * 0.6f : 0f, 0.8f + close * 0.5f, 15f);
            if (running && Margin < SprayBelow && Random.value < dt * 12f)                               // sucking at the surface: it spits
                Fx.Streak(mouth + transform.forward * 0.05f, (transform.forward + Vector3.up * 0.6f + Random.insideUnitSphere * 0.4f) * 1.2f, 0.03f, new Color(0.75f, 0.85f, 0.95f), 0.5f);
            if (player && g && Margin < GurgleBelow && Time.time > warnAt)
            {
                warnAt = Time.time + 25f;
                g.Toast("BREATHING THROUGH THE SNORKEL: THE WATER IS " + Mathf.Max(0, Mathf.RoundToInt(Margin * 100f)) + " CM BELOW THE INTAKE");
            }
        }
    }
}
