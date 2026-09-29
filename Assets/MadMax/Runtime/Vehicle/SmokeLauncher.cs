using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Smoke dischargers on the side armour. [U] fires a salvo from every launcher on the vehicle (one
    /// ammo_smoke per salvo, see VehicleWeapons): a screen round the vehicle that raider fire can't see through.</summary>
    public class SmokeLauncher : VehicleWeapon
    {
        public override void Operate(in WeaponInput input) { }

        /// <summary>The visible part of a salvo: canisters arcing up and out from each tube.</summary>
        public void Discharge()
        {
            for (int z = -1; z <= 1; z++)
            {
                var tube = transform.TransformPoint(new Vector3(0.44f, 0.5f, z * 0.32f));
                var outward = transform.TransformDirection(new Vector3(1f, 1f, 0f)).normalized;
                Fx.Smoke(tube, outward * 6f, 0.5f, new Color(0.85f, 0.85f, 0.8f, 0.9f), 2f);
            }
            MadMax.Audio.Sfx.Play("pop", transform.position, 0.8f, 0.8f);
        }
    }
}
