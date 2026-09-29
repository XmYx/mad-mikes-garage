using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Roof machine gun: trains toward the aim point, fires 9 rounds/s while LMB is held. Belts of 20 rounds
    /// (ammo_mg) feed from the pack; the barrel heats up and must cool after ~30 rounds of continuous fire.</summary>
    public class MountedGun : VehicleWeapon
    {
        public const string Ammo = "ammo_mg";
        public const int PerBelt = 20;
        public int rounds;
        Transform mount, gun;
        float cooldown, heat;
        bool warned;

        void Start() { mount = transform.Find("mount"); gun = mount ? mount.Find("gun") : null; }

        public override string Status
        {
            get
            {
                var g = MadMax.Game.WastelandGame.Instance;
                int total = rounds + (g ? g.Inventory.GetItem(Ammo) * PerBelt : 0);
                return "MG " + total + (heat > 0.75f ? " HOT" : "") + "  [LMB] FIRE";
            }
        }

        public override void Operate(in WeaponInput i)
        {
            Train(mount, gun, i.aim, 170f, 8f, 50f, i.dt);
            heat = Mathf.Max(0f, heat - i.dt * 0.25f);
            cooldown -= i.dt;
            if (!i.fire) { warned = false; return; }
            if (cooldown > 0f || heat >= 1f || !gun) return;
            cooldown = 0.11f;
            var g = MadMax.Game.WastelandGame.Instance;
            if (rounds <= 0)
            {
                if (!g.Inventory.TakeItem(Ammo)) { if (!warned) { warned = true; g.Toast("MG: NO AMMO BELTS"); } return; }
                rounds = PerBelt;
                MadMax.Audio.Sfx.Play("ratchet", transform.position, 0.5f, 1.3f);
            }
            rounds--;
            heat += 0.034f;
            var muzzle = gun.TransformPoint(new Vector3(0f, 0f, 1.25f));
            var dir = gun.rotation * Quaternion.Euler(Random.Range(-1.3f, 1.3f), Random.Range(-1.3f, 1.3f), 0f) * Vector3.forward;
            var end = VehicleWeapons.Hitscan(Vehicle, muzzle, dir, 150f, 0.85f, 0.12f);
            Tracers.Add(muzzle, end, new Color(1f, 0.85f, 0.45f, 0.9f), 0.05f, 0.035f);
            MadMax.Audio.Sfx.Play("shot_mg", muzzle, 0.6f, Random.Range(0.95f, 1.05f), 120f, 0.03f);
            var fx = DebrisSystem.Instance;
            if (fx) fx.EmitPuff(muzzle + dir * 0.2f, new Color32(255, 214, 120, 255), 0.06f, dir * 3f, 0.06f);
            if (MadMax.Npc.NpcDirector.Instance) MadMax.Npc.NpcDirector.Instance.Noise(muzzle, 90f);
        }
    }
}
