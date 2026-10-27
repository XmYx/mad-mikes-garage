using MadMax.Building;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Crowbar work: a swing next to a padlocked loot spot, a locked door or a locked container forces it
    /// (a door is levered over a moment: <see cref="DoorPry"/>).</summary>
    public partial class WastelandGame
    {
        public bool PryNearest(Vector3 at)
        {
            foreach (var l in LootSpots)
            {
                if (!l || !l.locked || Vector3.Distance(l.transform.position, at) > 1.6f) continue;
                bool gave = l.Pry(this);
                if (Inventory.GetItem("tool_crowbar") > 0)
                {
                    var o = Player ? Player.transform.position - l.transform.position : Vector3.forward; o.y = 0f;
                    DoorPry.Burst(l.transform.position + Vector3.up * 0.6f, o.normalized, l.title != null && l.title.Contains("LOCKER"), gave ? 2f : 0.6f);
                }
                return true;                               // the attempt counts as a hit
            }
            foreach (var p in Placeable.All)
            {
                if (!p || Vector3.Distance(p.transform.position, at) > 1.8f) continue;
                if (p.TryGetComponent<Door>(out var door) && door.locked)
                {
                    // the bar goes into the gap and is levered (DoorPry): sparks / splinters, then it snaps open or holds
                    MadMax.Audio.Sfx.Play("hit_metal", at, 0.9f, 0.7f, 40f);
                    DoorPry.Begin(door);
                    return true;
                }
            }
            return false;
        }
    }
}
