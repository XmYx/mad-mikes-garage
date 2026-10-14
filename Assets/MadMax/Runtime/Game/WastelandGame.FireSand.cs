using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Sand on a fire (ROADMAP fixes 2026-10-04): a shovel swung at a burning vehicle within reach throws a
    /// load of soil over it — dug from the ground underfoot, else a unit of sand / clay / rubble from the pack — worth
    /// half an extinguisher burst; burning pools there are smothered too. Slower than a bottle, but anyone has a shovel.</summary>
    public partial class WastelandGame
    {
        public const float SandReach = 3f, SandBurst = 0.5f;
        static readonly ResourceType[] SandLoads = { ResourceType.Sand, ResourceType.Clay, ResourceType.Rubble, ResourceType.Laterite };

        /// <summary>Loads of sand thrown on fires this session (tests).</summary>
        public int SandThrown { get; private set; }

        /// <summary>From the shovel's strike: true when it went on a fire instead of digging.</summary>
        public bool ShovelSand(Vector3 at)
        {
            if (!Player) return false;
            var me = Player.transform.position;
            VehicleSystems best = null; float bd = SandReach * SandReach;
            foreach (var s in VehicleSystems.All)
            {
                if (!s || !s.Burning) continue;
                float d = (s.FirePos - me).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
            bool pools = Spills.Instance && Spills.Smother(at, 1.2f) > 0;
            if (!best) return pools;
            var t = DeformableTerrain.Instance;
            float cap = 0f;
            if (t) t.SeepAt(me.x, me.z, out cap, out _);
            bool soil = cap > 0f && t.WaterDepth(me.x, me.z) < 0.05f;                              // bare ground to scoop from (not tarmac, rock or water)
            if (!soil)
            {
                ResourceType load = ResourceType.None;
                foreach (var r in SandLoads) if (Inventory.Get(r) > 0) { load = r; break; }
                if (load == ResourceType.None) { Toast("NOTHING TO THROW: NO BARE GROUND HERE AND NO SAND IN THE PACK"); return pools; }
                using (Inventory.Source(null, "THROWN ON THE FIRE")) Inventory.TrySpend(load, 1);
            }
            SandThrown++;
            bool out_ = best.Extinguish(SandBurst);
            if (DebrisSystem.Instance) for (int i = 0; i < 6; i++) DebrisSystem.Instance.EmitPuff(best.FirePos + Random.insideUnitSphere * 0.4f, MadMax.Voxel.Pal.Sand[2], 0.06f, Vector3.up * 1.2f + Random.insideUnitSphere, 0.7f);
            MadMax.Audio.Sfx.Play("dig", best.FirePos, 0.6f, 1.2f, 25f, 0.1f);
            if (out_) Toast("THE FIRE IS OUT");
            return true;
        }
    }
}
