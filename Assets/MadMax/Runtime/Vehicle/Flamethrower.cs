using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Flamethrower: while LMB is held it sprays burning fuel from the vehicle's own tank (0.6 L/s) in a
    /// 10 m stream: burns people and props, sets dry things and wrecks alight.</summary>
    public class Flamethrower : VehicleWeapon
    {
        Transform mount, nozzle;
        float burnT, lastSpray = -1f;
        bool warned;
        static readonly RaycastHit[] hits = new RaycastHit[12];

        void Start() { mount = transform.Find("mount"); nozzle = mount ? mount.Find("nozzle") : null; }

        public override string Status
        {
            get
            {
                var sys = Vehicle ? Vehicle.GetComponent<VehicleSystems>() : null;
                return "FLAMER " + (sys ? Mathf.RoundToInt(sys.fuel) + " L" : "") + "  [LMB] BURN";
            }
        }

        public override void Operate(in WeaponInput i)
        {
            Train(mount, nozzle, i.aim, 110f, 10f, 30f, i.dt);
            var v = Vehicle;
            var sys = v ? v.GetComponent<VehicleSystems>() : null;
            if (!i.fire || !nozzle || !sys) { warned = false; return; }
            if (sys.fuel < 1f) { if (!warned) { warned = true; MadMax.Game.WastelandGame.Instance?.Toast("FLAMER: THE FUEL TANK IS DRY"); } return; }
            sys.fuel -= 0.6f * i.dt;
            lastSpray = Time.time;
            var tip = nozzle.TransformPoint(new Vector3(0f, 0f, 1.1f));
            var dir = nozzle.forward;
            for (int k = 0; k < 3; k++)
                Fx.Smoke(tip, dir * Random.Range(9f, 14f) + Random.insideUnitSphere * 1.4f + Vector3.up * 0.8f, Random.Range(0.25f, 0.45f),
                    Color.Lerp(new Color(1f, 0.85f, 0.35f, 0.95f), new Color(1f, 0.35f, 0.08f, 0.9f), Random.value), 0.55f);
            MadMax.Audio.Sfx.Loop(this, "fire", 0.9f, 1.3f, 50f);
            if ((burnT -= i.dt) > 0f) return;
            burnT = 0.2f;
            // what the stream reaches burns
            int n = Physics.SphereCastNonAlloc(tip, 0.7f, dir, hits, 10f, ~0, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < n; k++)
            {
                var h = hits[k];
                if (h.distance <= 0f || (v && h.collider.transform.IsChildOf(v.transform))) continue;   // (overlaps at the start report no point)
                h.collider.GetComponentInParent<IDamageable>()?.ApplyHit(h.point, dir, 0.4f, 0.3f, v ? v.gameObject : null);
                if (Random.value < 0.25f) Fire.Ignite(h.point, h.collider.attachedRigidbody ? h.collider.transform : null, 6f, 0.7f, !h.collider.attachedRigidbody);
            }
            if (MadMax.Npc.NpcDirector.Instance) MadMax.Npc.NpcDirector.Instance.Noise(tip, 40f);
        }

        void Update()
        {
            if (lastSpray >= 0f && Time.time - lastSpray > 0.15f) { lastSpray = -1f; MadMax.Audio.Sfx.Loop(this, "fire", 0f, 1f, 50f); }
        }
    }
}
