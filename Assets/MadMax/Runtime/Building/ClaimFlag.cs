using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Claim flag: marks a base (<see cref="Radius"/> m). Raiders come for claimed bases (<c>BaseRaid</c>);
    /// [T] makes it the respawn point.</summary>
    public class ClaimFlag : MonoBehaviour, IInteractable
    {
        public const float Radius = 40f;
        public static readonly List<ClaimFlag> All = new List<ClaimFlag>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string Owner => GetComponent<Placeable>() ? GetComponent<Placeable>().owner : null;

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            string who = string.IsNullOrEmpty(Owner) ? "" : Owner.ToUpperInvariant() + "'S ";
            int worn = BaseUpkeep.WornIn(this);
            return who + "CLAIM: " + Pieces() + " PIECES, DEFENCE " + Defence() + (worn > 0 ? ", " + worn + " NEED THE HAMMER" : "") + "  [T] RESPAWN HERE";
        }
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (secondary) g.SetSpawn(transform.position + transform.forward * 1.5f + Vector3.up * 0.2f); }

        /// <summary>Built pieces standing inside the claim (not those on vehicles).</summary>
        public int Pieces()
        {
            int n = 0;
            foreach (var p in Placeable.All) if (p && !p.GetComponentInParent<Rigidbody>() && Inside(p.transform.position)) n++;
            return n;
        }

        public bool Inside(Vector3 p) { var d = p - transform.position; d.y = 0f; return d.sqrMagnitude < Radius * Radius; }

        /// <summary>Rough strength of the defences: armed turrets, spike walls, wire, bells, stone and concrete walls, and
        /// the stage I works (<see cref="DefenceWorks.Score"/>).</summary>
        public int Defence()
        {
            float s = MadMax.Npc.Companions.GuardsAt(transform.position, 40f) * 3f;              // companions on guard (roadmap 20)
            foreach (var p in Placeable.All)
            {
                if (!p || !Inside(p.transform.position)) continue;
                float works = DefenceWorks.Score(p);                                                // mines, wires, towers, gates, nests, processed walls
                if (works >= 0f) { s += works; continue; }
                if (p.TryGetComponent<AutoTurret>(out var t)) s += t.on ? 4f : 1f;
                else if (p.TryGetComponent<DefenceHazard>(out var h)) s += h.kind == DefenceHazard.Kind.Spikes ? 1f : 0.7f;
                else if (p.GetComponent<AlarmBell>()) s += 1f;
                else if (p.id.Contains("concrete") || p.id == "door_metal" || p.id == "garage_door") s += 0.5f;
                else if (p.id.Contains("brick") || p.id.Contains("stone")) s += 0.3f;
                else if (p.id.StartsWith("wall_") || p.id.StartsWith("fence")) s += 0.1f;
            }
            return Mathf.RoundToInt(s);
        }

        public static ClaimFlag Near(Vector3 p)
        {
            foreach (var c in All) if (c && c.Inside(p)) return c;
            return null;
        }
    }
}
