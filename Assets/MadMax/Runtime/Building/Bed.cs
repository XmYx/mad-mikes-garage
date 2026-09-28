using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Sleep until morning (heals, sets the respawn point).</summary>
    public class Bed : MonoBehaviour, IInteractable
    {
        public string Prompt(MadMax.Game.WastelandGame g) => MadMax.World.DayNight.Darkness > 0.2f ? "[E] SLEEP  [T] SET SPAWN" : "[E] REST  [T] SET SPAWN";
        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) { g.SetSpawn(transform.position + transform.forward * 1.2f + Vector3.up * 0.2f); return; }
            g.Sleep();
        }
    }
}
