using MadMax.Building;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Crowbar work: a swing next to a padlocked loot spot, a locked door or a locked container forces it
    /// (someone else's door breaks its lock; your own just unlocks).</summary>
    public partial class WastelandGame
    {
        public bool PryNearest(Vector3 at)
        {
            foreach (var l in LootSpots)
            {
                if (!l || !l.locked || Vector3.Distance(l.transform.position, at) > 1.6f) continue;
                if (l.Pry(this)) return true;
                return true;                               // the attempt counts as a hit
            }
            foreach (var p in Placeable.All)
            {
                if (!p || Vector3.Distance(p.transform.position, at) > 1.8f) continue;
                if (p.TryGetComponent<Door>(out var door) && door.locked)
                {
                    MadMax.Audio.Sfx.Play("hit_metal", at, 0.9f, 0.7f, 40f);
                    if (Random.value < 0.25f + Stats.Attribute(MadMax.RPG.Attr.Strength) * 0.06f) { door.SetLocked(false); Toast("LOCK BROKEN"); MadMax.Npc.NpcDirector.Instance?.Noise(at, 30f); }
                    else Toast("THE DOOR HOLDS");
                    return true;
                }
            }
            return false;
        }
    }
}
