using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Harpoon launcher: LMB fires a barbed bolt (ammo_harpoon) on a steel cable up to 35 m. Whatever it bites
    /// (a raider's car, a wreck, a crate, the ground) is tethered: hold LMB to reel in and drag it, Shift+LMB cuts it
    /// loose (the bolt is often recovered). Hits also damage.</summary>
    public class Harpoon : VehicleWeapon
    {
        public const string Ammo = "ammo_harpoon";
        const float Range = 35f, MaxPull = 90000f;
        Transform mount, launcher;
        Collider anchor; Rigidbody anchorRb; Vector3 anchorLocal;
        float length;
        LineRenderer cable;

        public bool Hooked => anchor;
        Vector3 Tip => launcher ? launcher.TransformPoint(new Vector3(0f, 0f, 0.95f)) : transform.position;
        Vector3 AnchorPoint => anchor ? anchor.transform.TransformPoint(anchorLocal) : Tip;

        void Start() { mount = transform.Find("mount"); launcher = mount ? mount.Find("launcher") : null; }

        public override string Status
        {
            get
            {
                if (Hooked) return "HARPOON " + Vector3.Distance(Tip, AnchorPoint).ToString("0") + " M  [LMB] REEL  [SHIFT+LMB] CUT";
                var g = MadMax.Game.WastelandGame.Instance;
                return "HARPOON x" + (g ? g.Inventory.GetItem(Ammo) : 0) + "  [LMB] FIRE";
            }
        }

        public override void Operate(in WeaponInput i)
        {
            Train(mount, launcher, Hooked ? AnchorPoint : i.aim, Hooked ? 240f : 120f, 8f, 60f, i.dt);
            if (Hooked)
            {
                if (i.alt && i.firePressed) { Release(true); return; }
                if (i.fire) length = Mathf.Max(3f, length - 4f * i.dt);
                return;
            }
            if (i.firePressed && !i.alt) Fire();
        }

        void Fire()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!launcher) return;
            if (!g.Inventory.TakeItem(Ammo)) { g.Toast("HARPOON: NO BOLTS"); return; }
            var from = Tip; var dir = launcher.forward;
            MadMax.Audio.Sfx.Play("crash_small", from, 0.6f, 1.6f, 80f);
            if (!VehicleWeapons.Ray(Vehicle, from, dir, Range, out var hit)) { Tracers.Add(from, from + dir * Range, new Color(0.75f, 0.75f, 0.72f, 0.9f), 0.12f, 0.03f); return; }
            Tracers.Add(from, hit.point, new Color(0.75f, 0.75f, 0.72f, 0.9f), 0.12f, 0.03f);
            MadMax.Audio.Sfx.Play("hit_metal", hit.point, 0.8f, 0.8f);
            var dmg = hit.collider.GetComponentInParent<IDamageable>();
            dmg?.ApplyHit(hit.point, dir, 1.4f, 0.15f, Vehicle ? Vehicle.gameObject : null);
            if (hit.collider.GetComponentInParent<MadMax.Npc.Npc>()) return;                  // it goes through people; nothing to tether
            anchor = hit.collider; anchorRb = hit.rigidbody;
            anchorLocal = anchor.transform.InverseTransformPoint(hit.point);
            length = hit.distance + 0.5f;
            if (anchorRb && anchorRb.isKinematic && anchorRb.GetComponent<VehicleDriver>()) anchorRb.isKinematic = false;   // wake a parked wreck
            if (!cable)
            {
                cable = new GameObject("HarpoonCable").AddComponent<LineRenderer>();
                cable.transform.SetParent(transform, false);
                cable.sharedMaterial = Fx.TransparentMaterial(null);
                cable.widthMultiplier = 0.03f;
                cable.startColor = cable.endColor = new Color(0.14f, 0.13f, 0.12f, 1f);
            }
            cable.enabled = true;
        }

        public void Release(bool recover)
        {
            if (recover && Hooked && Random.value < 0.7f) MadMax.Game.WastelandGame.Instance?.Inventory.AddItem(Ammo);
            anchor = null; anchorRb = null;
            if (cable) cable.enabled = false;
        }

        void FixedUpdate()
        {
            if (!Hooked) { if (anchorRb || (cable && cable.enabled)) Release(false); return; }
            var v = Vehicle;
            if (!v || !v.Body) { Release(false); return; }
            v.KeepAwakeUntil = Time.time + 0.5f;
            if (anchorRb) { var od = anchorRb.GetComponent<VehicleDriver>(); if (od) od.KeepAwakeUntil = Time.time + 0.5f; if (anchorRb.IsSleeping()) anchorRb.WakeUp(); }
            Vector3 a = Tip, b = AnchorPoint;
            var d = b - a;
            float dist = d.magnitude;
            if (dist > Range * 1.4f) { Release(false); MadMax.Game.WastelandGame.Instance?.Toast("THE HARPOON TORE FREE"); return; }
            if (dist <= length || dist < 0.01f) return;
            var dir = d / dist;
            var rb = v.Body;
            float closing = Vector3.Dot((anchorRb ? anchorRb.GetPointVelocity(b) : Vector3.zero) - rb.GetPointVelocity(a), dir);
            float force = Mathf.Clamp((dist - length) * 60000f + closing * 8000f, 0f, MaxPull);
            rb.AddForceAtPosition(dir * force, a);
            if (anchorRb && !anchorRb.isKinematic) anchorRb.AddForceAtPosition(-dir * force, b);
        }

        void LateUpdate()
        {
            if (!cable || !cable.enabled) return;
            var a = Tip; var b = AnchorPoint;
            float slack = Mathf.Max(0f, length - Vector3.Distance(a, b));
            cable.positionCount = 8;
            for (int k = 0; k < 8; k++) { float t = k / 7f; cable.SetPosition(k, Vector3.Lerp(a, b, t) + Vector3.down * slack * 2f * t * (1f - t)); }
        }
    }
}
