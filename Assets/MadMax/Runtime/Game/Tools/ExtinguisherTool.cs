using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Fire extinguisher: each squeeze (a burst of dry powder, 1/<see cref="Bursts"/> of the bottle) smothers
    /// the fires in a cone in front of the nozzle. An engine fire needs a few bursts (<see cref="MadMax.Vehicles.VehicleSystems.Extinguish"/>)
    /// and then stays out for a while; ground and prop fires lose most of their fuel. Empty bottles are refilled at a
    /// workbench (sand), like a tool repair.</summary>
    public class ExtinguisherTool : HandTool
    {
        public const int Bursts = 8;
        public const float Reach = 3.2f;
        public Transform nozzle;

        public override void Strike(PlayerCharacter user)
        {
            var g = WastelandGame.Instance;
            if (!g) return;
            if (g.Condition(id) <= 0f) { g.Toast("THE EXTINGUISHER IS EMPTY: REFILL IT AT A WORKBENCH"); MadMax.Audio.Sfx.Play("click", user.transform.position, 0.5f); return; }
            var from = nozzle ? nozzle.position : user.transform.position + Vector3.up * 1.1f;
            var dir = user.transform.forward;
            var at = user.transform.position + dir * 1.6f + Vector3.up * 0.5f;
            Spray(from, dir);
            MadMax.Audio.Sfx.Play("steam", from, 0.8f, 1.3f, 20f);
            int hit = 0; bool vehicleOut = false, vehicleFire = false;
            for (int i = MadMax.World.Fire.All.Count - 1; i >= 0; i--)
            {
                var f = MadMax.World.Fire.All[i];
                if (!f) continue;
                var to = f.transform.position - user.transform.position; to.y = 0f;
                if (to.magnitude > Reach || (to.magnitude > 0.6f && Vector3.Angle(dir, to) > 50f)) continue;
                var vs = f.GetComponentInParent<MadMax.Vehicles.VehicleSystems>();
                if (vs && vs.Burning) { vehicleFire = true; if (vs.Extinguish(1f)) vehicleOut = true; }
                else { f.fuel = Mathf.Max(0f, f.fuel - 40f); f.intensity *= 0.3f; }
                hit++;
            }
            hit += MadMax.World.Spills.Smother(at, 1.6f);
            g.WearTool(id, 1f / Bursts);
            if (vehicleOut) g.Toast("THE ENGINE FIRE IS OUT");
            else if (vehicleFire) g.Toast("THE FLAMES DIE DOWN... KEEP AT IT");
            else if (hit > 0) g.Toast("SMOTHERED THE FLAMES");
        }

        public static void Spray(Vector3 from, Vector3 dir)
        {
            for (int i = 0; i < 18; i++)
            {
                var v = (dir + Random.insideUnitSphere * 0.18f).normalized * Random.Range(3f, 6f);
                MadMax.World.Fx.Smoke(from, v, Random.Range(0.15f, 0.35f), new Color(0.93f, 0.93f, 0.9f, 0.75f), Random.Range(0.6f, 1.2f));
            }
        }
    }
}
