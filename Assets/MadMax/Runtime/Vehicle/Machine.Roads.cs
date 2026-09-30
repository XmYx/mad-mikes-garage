using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Roads (depth stage C) at vehicle scale: the tipper spreads a gravel road behind it while it rolls with
    /// the bed raised on a load of gravel (a 2.6 m strip, a unit every 0.6 m), and the paver lays gravel as its third
    /// material after asphalt and concrete.</summary>
    public partial class Machine
    {
        /// <summary>Paver: laying gravel (else <see cref="concrete"/> or asphalt).</summary>
        public bool gravel;
        /// <summary>Tipper: bed angle from which a load of gravel spreads on the move.</summary>
        public const float SpreadMinBed = 15f;
        /// <summary>Tipper: spreading gravel this step.</summary>
        public bool Spreading { get; private set; }
        float spreadTravel;

        ResourceType PaverMaterial => gravel ? ResourceType.Gravel : concrete ? ResourceType.Concrete : ResourceType.Asphalt;
        byte PaverKind => gravel ? DeformableTerrain.PaveGravel : concrete ? DeformableTerrain.PaveConcrete : DeformableTerrain.PaveAsphalt;

        /// <summary>Asphalt → concrete → gravel → asphalt.</summary>
        void NextPaverMaterial()
        {
            if (gravel) { gravel = false; concrete = false; }
            else if (concrete) { concrete = false; gravel = true; }
            else concrete = true;
        }

        /// <summary>Tipper with the bed raised and mostly gravel aboard, rolling: lay a gravel strip at the tailgate.
        /// Returns true while spreading (no pile is dumped then).</summary>
        bool SpreadGravel(DeformableTerrain terrain, float moved)
        {
            Spreading = false;
            if (!store || bed < SpreadMinBed || !driver || Mathf.Abs(driver.ForwardSpeed) < 0.4f) { spreadTravel = 0f; return false; }
            int units = store.inventory.Get(ResourceType.Gravel);
            if (units <= 0 || units * 2 < BedUnits) return false;
            Spreading = true;
            if ((spreadTravel += moved) < 0.6f) return true;
            spreadTravel = 0f;
            var tail = transform.TransformPoint(new Vector3(0, 0, -50) * S);
            var at = new Vector3(tail.x, terrain.Height(tail.x, tail.z), tail.z);
            if (Terraform(DeformableTerrain.TerraOp.Pave, at, 1.3f, 0f, DeformableTerrain.PaveGravel) <= 0f) return true;   // already gravelled here
            store.inventory.TrySpend(ResourceType.Gravel, 1);
            Fx.Smoke(at + Vector3.up * 0.4f, Vector3.down * 0.5f - transform.forward, 0.6f, DustColor(ResourceType.Gravel), 1.2f);
            if (Random.value < 0.5f) Clods(tail + Vector3.up * 0.8f, ResourceType.Gravel, 3, 0.5f, Vector3.down - transform.forward);
            MadMax.Audio.Sfx.Play("dig", at, 0.35f, Random.Range(1.1f, 1.3f), 30f, 0.25f);
            return true;
        }
    }
}
