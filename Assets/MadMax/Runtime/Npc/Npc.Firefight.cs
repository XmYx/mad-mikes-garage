using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A driver beating an engine fire: walks to the burning bay with the car's extinguisher, squeezes a
    /// burst every <see cref="BurstEvery"/> seconds until the fire is out, the bottle is empty or the fire is past
    /// saving (then runs). Set by <see cref="Convoy"/>; a foe interrupts it.</summary>
    public partial class Npc
    {
        public const float BurstEvery = 0.9f, SprayReach = 2.4f;
        /// <summary>The vehicle whose engine fire this NPC is fighting (null = none).</summary>
        [System.NonSerialized] public VehicleSystems fireTarget;
        /// <summary>Bursts left in the bottle.</summary>
        [System.NonSerialized] public int foamLeft;
        /// <summary>Last fight's outcome: 1 out, −1 gave up, 0 still going / none.</summary>
        [System.NonSerialized] public int fireOutcome;
        float sprayT;

        public void FightFire(VehicleSystems vs, int bursts)
        {
            fireTarget = vs; foamLeft = bursts; fireOutcome = 0; sprayT = 0.6f;
            SetTool("tool_extinguisher", rig.material);
        }

        void EndFireFight(int outcome)
        {
            fireTarget = null; fireOutcome = outcome;
            SetTool(Profile.tool, rig.material);
            if (outcome < 0) Scare(15f);
        }

        /// <summary>The firefight step; false once there is nothing left to do (the brain carries on as usual).</summary>
        bool FireTick(Vector3 me, float dt, ref Vector3 move, ref float speed)
        {
            var vs = fireTarget;
            if (!vs) { EndFireFight(-1); return false; }
            if (!vs.Burning) { EndFireFight(1); return false; }
            var burn = vs.GetComponent<VehicleBurn>();
            if ((burn && (burn.burn >= 0.5f || burn.charred)) || foamLeft <= 0) { EndFireFight(-1); return false; }   // past saving: get clear
            var at = vs.FirePos;
            var to = Flat(at - me);
            if (to.magnitude > SprayReach)
            {
                var stand = at - to.normalized * (SprayReach - 0.6f);
                move = Toward(stand); speed = 3.2f;
                return true;
            }
            faceTarget = at; faceUntil = Time.time + 0.5f;
            if ((sprayT -= dt) > 0f) return true;
            sprayT = BurstEvery;
            foamLeft--;
            var from = me + Vector3.up * 1.1f + to.normalized * 0.4f;
            MadMax.Game.ExtinguisherTool.Spray(from, (at - from).normalized);
            MadMax.Audio.Sfx.Play("steam", from, 0.8f, 1.3f, 20f);
            if (vs.Extinguish(1f)) EndFireFight(1);
            return true;
        }
    }
}
