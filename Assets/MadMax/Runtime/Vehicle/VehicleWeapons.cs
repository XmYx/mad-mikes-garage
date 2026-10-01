using System.Collections.Generic;
using System.Text;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Operates the weapons mounted on a vehicle while the player drives it: routes the driver's input and aim
    /// point to each weapon part, fires smoke salvos (one ammo_smoke for all launchers), and lists their status for
    /// the HUD. Also the shared hitscan used by mounted guns.</summary>
    [DisallowMultipleComponent]
    public class VehicleWeapons : MonoBehaviour
    {
        public const string SmokeAmmo = "ammo_smoke";
        public Vector3 Aim { get; private set; }
        /// <summary>Driven and aiming this frame (searchlights follow the aim while true).</summary>
        public bool Aiming => Time.time - lastControl < 0.2f;
        public bool Armed => weapons.Count > 0;

        readonly List<VehicleWeapon> weapons = new List<VehicleWeapon>();
        readonly StringBuilder status = new StringBuilder();
        float refreshT, smokeCool, lastControl = -1f;
        static readonly RaycastHit[] hits = new RaycastHit[16];

        public void Control(in WeaponInput input)
        {
            Aim = input.aim;
            bool resumed = Time.time - lastControl > 0.5f;                                    // back at the wheel: parts may have changed meanwhile
            lastControl = Time.time;
            if ((refreshT -= input.dt) <= 0f || resumed)
            {
                refreshT = 1f;
                weapons.Clear();
                foreach (var w in GetComponentsInChildren<VehicleWeapon>()) if (w.Mounted) weapons.Add(w);
            }
            smokeCool -= input.dt;
            bool salvo = input.smoke && smokeCool <= 0f && HasSmoke() && FireSmoke();
            foreach (var w in weapons)
            {
                if (!w) continue;
                if (w is SmokeLauncher s) { if (salvo) s.Discharge(); continue; }
                w.Operate(input);
            }
            if (salvo) smokeCool = 4f;
        }

        /// <summary>Same as <see cref="Control"/> with plain arguments (automation, older compilers).</summary>
        public void ControlArgs(bool fire, bool firePressed, bool alt, bool drop, bool smoke, Vector3 aim, float dt) =>
            Control(new WeaponInput { fire = fire, firePressed = firePressed, alt = alt, drop = drop, smoke = smoke, aim = aim, dt = dt });

        bool HasSmoke() { foreach (var w in weapons) if (w is SmokeLauncher) return true; return false; }

        bool FireSmoke()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g.Inventory.TakeItem(SmokeAmmo)) { g.Toast("SMOKE: NO GRENADES"); smokeCool = 1f; return false; }
            SmokeScreen.Pop(transform.position + Vector3.up * 1.2f, 7f);
            SmokeScreen.Pop(transform.position - transform.forward * 7f + Vector3.up * 1.2f, 6f);
            return true;
        }

        /// <summary>HUD line: every weapon's status, smoke grenades if launchers are fitted.</summary>
        public string Status
        {
            get
            {
                status.Clear();
                foreach (var w in weapons)
                {
                    if (!w || w is SmokeLauncher) continue;
                    var s = w.Status;
                    if (string.IsNullOrEmpty(s)) continue;
                    if (status.Length > 0) status.Append("   ");
                    status.Append(s);
                }
                if (HasSmoke())
                {
                    var g = MadMax.Game.WastelandGame.Instance;
                    if (status.Length > 0) status.Append("   ");
                    status.Append("SMOKE x").Append(g ? g.Inventory.GetItem(SmokeAmmo) : 0).Append("  [U]");
                }
                return status.Length > 0 ? status.ToString() : null;
            }
        }

        /// <summary>First thing along a ray that isn't part of <paramref name="own"/>.</summary>
        public static bool Ray(VehicleDriver own, Vector3 from, Vector3 dir, float range, out RaycastHit hit)
        {
            hit = default;
            int n = Physics.RaycastNonAlloc(from, dir, hits, range, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (own && hits[i].collider.transform.IsChildOf(own.transform)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; hit = hits[i]; }
            }
            return best < float.MaxValue;
        }

        /// <summary>A bullet: damages the first thing hit (IDamageable), kicks up dust or sparks; returns the end point.</summary>
        public static Vector3 Hitscan(VehicleDriver own, Vector3 from, Vector3 dir, float range, float power, float radius)
        {
            if (!Ray(own, from, dir, range, out var hit)) return from + dir * range;
            hit.collider.GetComponentInParent<IDamageable>()?.ApplyHit(hit.point, dir, power, radius, own ? own.gameObject : null);
            var fx = DebrisSystem.Instance;
            bool metal = hit.collider.GetComponentInParent<VehicleDriver>() || hit.collider.GetComponentInParent<VehiclePart>();
            if (metal) Fx.Sparks(hit.point, hit.normal, 3, new Color(1f, 0.8f, 0.4f));
            else if (fx) fx.EmitPuff(hit.point, new Color32(170, 140, 105, 255), 0.06f, hit.normal * 1.5f, 0.4f);
            return hit.point;
        }
    }
}
