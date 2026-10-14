using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The hose on a vehicle's water tank part: parked by a built water piece (tank, tower, well, barrel) it
    /// fills the tank from that network ([E]) or empties the tank into it ([T]) — carry water from a well to the fields;
    /// standing in a lake it pumps lake water in. Elsewhere the tank's tap (WaterOutlet) answers.</summary>
    public class TankHose : MonoBehaviour, IInteractable
    {
        public const float Reach = 8f, PerUse = 100f;
        UtilityNode node;
        void Awake() => node = GetComponent<UtilityNode>();

        /// <summary>The nearest built water piece within reach that isn't on this vehicle.</summary>
        UtilityNode Station()
        {
            if (!node) return null;
            var me = transform.root;
            UtilityNode best = null; float bd = Reach;
            foreach (var n in UtilityNode.All)
            {
                if (!n || n == node || (n.kinds & UtilityKind.Water) == 0 || !n.Piece || n.transform.root == me) continue;
                if (UtilityGrid.NetCapacity(n) <= 0f) continue;
                float d = Vector3.Distance(n.PortWorld, node.PortWorld);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        bool InLake()
        {
            var t = MadMax.World.DeformableTerrain.Instance;
            var p = transform.position;
            return t && t.WaterDepth(p.x, p.z) > 0.3f;
        }

        static string Name(UtilityNode n)
        {
            var d = FurnitureLibrary.Get(n.Piece.id);
            return d != null ? d.name : "THE TANK";
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (!node) return null;
            float mine = UtilityGrid.NetWater(node, out _), cap = UtilityGrid.NetCapacity(node);
            var s = Station();
            if (s)
            {
                float there = UtilityGrid.NetWater(s, out _), room = UtilityGrid.NetCapacity(s) - there;
                string fill = there > 0.5f && mine < cap - 0.5f ? "[E] FILL TANK FROM " + Name(s) : null;
                string empty = mine > 0.5f && room > 0.5f ? "[T] EMPTY TANK INTO " + Name(s) : null;
                if (fill == null && empty == null) return null;
                return (fill ?? "") + (fill != null && empty != null ? "  " : "") + (empty ?? "") + "  (" + Mathf.RoundToInt(mine) + "/" + Mathf.RoundToInt(cap) + "L)";
            }
            return InLake() && mine < cap - 0.5f ? "[E] PUMP LAKE WATER INTO THE TANK" : null;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!node) return;
            float mine = UtilityGrid.NetWater(node, out _), cap = UtilityGrid.NetCapacity(node);
            var s = Station();
            float moved;
            if (!s)
            {
                if (secondary || !InLake()) return;
                moved = UtilityGrid.Pour(node, Mathf.Min(PerUse, cap - mine), false);
                if (moved > 0.5f) g.Toast("PUMPED " + Mathf.RoundToInt(moved) + "L OF LAKE WATER (DIRTY)");
            }
            else if (!secondary)
            {
                float want = Mathf.Min(PerUse, cap - mine, UtilityGrid.NetWater(s, out _));
                float got = UtilityGrid.Draw(s, want, out bool clean);
                moved = UtilityGrid.Pour(node, got, clean);
                if (moved < got) UtilityGrid.Pour(s, got - moved, clean);                       // what didn't fit goes back
                g.Toast("FILLED THE TANK: +" + Mathf.RoundToInt(moved) + "L");
            }
            else
            {
                float want = Mathf.Min(PerUse, mine, UtilityGrid.NetCapacity(s) - UtilityGrid.NetWater(s, out _));
                float got = UtilityGrid.Draw(node, want, out bool clean);
                moved = UtilityGrid.Pour(s, got, clean);
                if (moved < got) UtilityGrid.Pour(node, got - moved, clean);
                g.Toast("EMPTIED " + Mathf.RoundToInt(moved) + "L INTO " + Name(s));
            }
            if (moved > 0.5f) MadMax.Audio.Sfx.Play("pour", transform.position, 0.7f, 0.9f, 20f);
        }
    }
}
