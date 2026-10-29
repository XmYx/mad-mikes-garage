using MadMax.Game;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A raider lights a bottle before he throws it (2026-10-29; the molotov used to leave the car with no
    /// warning): the rag catches with a match strike at the window on the side facing you, the bottle is held out and
    /// swung back burning (its orange light on the door, black smoke streaming off in the car's wind), and after
    /// <see cref="Delay"/> s it is thrown, aimed where you will be — a moment to swerve or brake. A car knocked out in
    /// that moment drops it beside itself. Host side (the convoy's authority); clients see the bottle only in flight.</summary>
    public class RaiderBottle : MonoBehaviour
    {
        public const float Delay = 0.9f;
        public const string Id = "throw_molotov";
        /// <summary>Bottles lit at a window / thrown / dropped by a car knocked out mid-swing (tests).</summary>
        public static int Lit, Thrown, Dropped;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Lit = 0; Thrown = 0; Dropped = 0; }

        Transform target, prop;
        Vector3 local;
        float t;
        Component car;

        /// <summary>Light a bottle at <paramref name="shooter"/>'s window toward <paramref name="target"/> (one at a time per car).</summary>
        public static bool Light(Component shooter, Transform target)
        {
            if (!shooter || !target || shooter.GetComponent<RaiderBottle>()) return false;
            var b = shooter.gameObject.AddComponent<RaiderBottle>();
            b.car = shooter; b.target = target;
            return true;
        }

        void Start()
        {
            var g = WastelandGame.Instance;
            if (!g || !target) { Destroy(this); return; }
            var rel = transform.InverseTransformPoint(target.position);
            float side = rel.x >= 0f ? 1f : -1f;
            var box = GetComponentInChildren<BoxCollider>();
            float half = box ? Mathf.Abs(box.size.x * box.transform.lossyScale.x) * 0.5f : 0.9f;
            local = new Vector3(side * (half + 0.12f), 1.3f, 0.25f);
            prop = new GameObject("RaiderBottle").transform;
            prop.SetParent(transform, false);
            prop.localPosition = local;
            WorldItemModels.AddVisual(prop, Id, g.propMaterial, out var bounds);
            ThrownLit.Light(prop, Id, true, new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            Lit++;
            var at = prop.position;
            MadMax.Audio.Sfx.Play("strike", at, 0.55f, 1f, 18f);
            MadMax.Audio.Sfx.Play("fire", at, 0.4f, 1.8f, 20f);
            Attention.Ping(at, Attention.Kind.Fire, 1.2f);                                       // the player turns to the flame
        }

        void Update()
        {
            if (!prop) { Destroy(this); return; }
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / Delay);
            // held out of the window, then swung back over the shoulder for the throw
            float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, u));
            prop.localPosition = local + new Vector3(Mathf.Sign(local.x) * 0.15f * Mathf.Sin(u * Mathf.PI), 0.25f * u + 0.2f * back, -0.45f * back);
            prop.localRotation = Quaternion.Euler(-70f * back, 0f, Mathf.Sin(t * 9f) * 6f);
            var vd = car ? car.GetComponent<VehicleDriver>() : null;
            bool fit = car && (!(car is Behaviour b) || b.enabled) && vd && vd.enabled && vd.driveable;
            if (!fit && u > 0.2f) { Release(true); return; }
            if (u < 1f) return;
            Release(false);
        }

        void Release(bool drop)
        {
            var g = WastelandGame.Instance;
            var from = prop.position;
            Vector3 v;
            var rb = GetComponent<Rigidbody>();
            var carry = rb ? rb.linearVelocity : Vector3.zero;
            if (drop || !target) { v = carry + Vector3.down; Dropped++; }
            else
            {
                var trb = target.GetComponentInParent<Rigidbody>();
                var aimAt = target.position + (trb && !trb.isKinematic ? trb.linearVelocity * 0.8f : Vector3.zero);
                float T = Mathf.Clamp(Vector3.Distance(from, aimAt) / 16f, 0.5f, 1.6f);
                v = (aimAt - from) / T - 0.5f * Physics.gravity * T;
                Thrown++;
                MadMax.Audio.Sfx.Play("whoosh", from, 0.45f, 1f, 18f);
            }
            Destroy(prop.gameObject);
            if (g) g.SpawnThrown(Id, from + (drop ? Vector3.zero : v.normalized * 0.6f), v, true);
            Destroy(this);
        }

        void OnDestroy() { if (prop) Destroy(prop.gameObject); }
    }
}
