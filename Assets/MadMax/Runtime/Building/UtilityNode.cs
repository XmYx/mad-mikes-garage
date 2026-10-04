using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>What a node carries; a link of kind <c>Water | OneWay</c> is a one-way pipe from the node that holds it
    /// to the other (<see cref="UtilityGrid"/>).</summary>
    [System.Flags] public enum UtilityKind : byte { None = 0, Power = 1, Water = 2, OneWay = 8 }

    /// <summary>A piece on the power and/or water network. Cables and pipes are links stored on the node that made them.
    /// Producers set <see cref="produce"/>, consumers set <see cref="demand"/> every frame; <see cref="UtilityGrid"/> solves.</summary>
    public class UtilityNode : MonoBehaviour, IPlaceState
    {
        public UtilityKind kinds;
        public Vector3 port = new Vector3(0, 0.5f, 0);   // local cable/pipe attachment

        // power
        [System.NonSerialized] public float produce, demand;
        public float batteryWh, batteryCharge;
        public bool Powered { get; internal set; }

        // power control (depth stage F)
        /// <summary>Kinds this node currently breaks (open switch, closed valve): links through it carry nothing.</summary>
        [System.NonSerialized] public UtilityKind cut;
        /// <summary>A load breaker (0 essential, 1 normal, 2 low; -1 none): loads on its far side from every power
        /// source take this priority.</summary>
        [System.NonSerialized] public int breaker = -1;
        /// <summary>0 essential, 1 normal, 2 low (shed first); set by the grid from breakers.</summary>
        [System.NonSerialized] public int priority = 1;
        /// <summary>W drawn by a second role on the same node (a desalinator's pump beside its crafting station).</summary>
        [System.NonSerialized] public float auxDemand;
        /// <summary>Producers: W they deliver (their share of the served demand).</summary>
        [System.NonSerialized] public float load;
        /// <summary>Dark because its priority was shed for lack of power.</summary>
        public bool Shed { get; internal set; }
        /// <summary>The net cannot carry its essential and normal loads: running generators on it stall.</summary>
        public bool Overloaded { get; internal set; }
        /// <summary>Highest priority tier the net served in the last solve (-1 none).</summary>
        public int NetLevel { get; internal set; } = 1;

        // water
        public float waterCapacity;
        public float clean, dirty;
        [System.NonSerialized] public float sourceDirty, filterRate;   // L/s supplied / converted by this node (set by its role component)
        [System.NonSerialized] public float sourceClean;               // L/s of clean water supplied (wells)
        // water quality (depth stage F)
        /// <summary>What the dirty water on this node's network carries (saved).</summary>
        public WaterTaint taint;
        /// <summary>What this source's dirty water carries (None = plain silt).</summary>
        [System.NonSerialized] public WaterTaint sourceTaint;
        /// <summary>L/s of any dirty water, brine too, this node turns clean (desalinator, still).</summary>
        [System.NonSerialized] public float desalRate;
        /// <summary>Litres this node's filter / desalinator turned clean, and what they carried, since its role last read them.</summary>
        [System.NonSerialized] public float converted;
        [System.NonSerialized] public WaterTaint convertedTaint;

        internal int powerNet = -1, waterNet = -1;
        /// <summary>Litres that left this node through its one-way pipes (tests).</summary>
        [System.NonSerialized] public float flowed;

        public readonly List<(uint id, UtilityKind kind)> links = new List<(uint, UtilityKind)>();

        public static readonly List<UtilityNode> All = new List<UtilityNode>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() { All.Add(this); UtilityGrid.Invalidate(); }
        void OnDisable() { All.Remove(this); UtilityGrid.Invalidate(); }

        public Vector3 PortWorld => transform.TransformPoint(port);
        public Placeable Piece => GetComponent<Placeable>();
        public uint Id => Piece ? Piece.Id : 0;

        public float Water => clean + dirty;

        /// <summary>Something that feeds the power net (a breaker's supply side): batteries and every kind of producer.</summary>
        public bool IsSource => batteryWh > 0f || produce > 0f || GetComponent<Generator>() || GetComponent<SolarPanel>() || GetComponent<Windmill>() || GetComponent<WaterTurbine>();

        public void Link(UtilityNode other, UtilityKind kind)
        {
            if (!other || other == this) return;
            foreach (var l in links) if (l.id == other.Id && l.kind == kind) return;
            foreach (var l in other.links) if (l.id == Id && l.kind == kind) return;
            links.Add((other.Id, kind));
            UtilityGrid.Invalidate();
            Piece?.Dirty();
        }

        public void Unlink()
        {
            links.Clear();
            foreach (var n in All) if (n) n.links.RemoveAll(l => l.id == Id);
            UtilityGrid.Invalidate();
            Piece?.Dirty();
        }

        public string SaveState()
        {
            var sb = new StringBuilder();
            sb.Append(clean.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            sb.Append(dirty.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            sb.Append(batteryCharge.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            foreach (var l in links) sb.Append(l.id).Append(':').Append((int)l.kind).Append(',');
            if (taint != WaterTaint.None) sb.Append(';').Append((int)taint);
            return sb.ToString();
        }

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (p.Length > 0) float.TryParse(p[0], System.Globalization.NumberStyles.Float, ci, out clean);
            if (p.Length > 1) float.TryParse(p[1], System.Globalization.NumberStyles.Float, ci, out dirty);
            if (p.Length > 2) float.TryParse(p[2], System.Globalization.NumberStyles.Float, ci, out batteryCharge);
            links.Clear();
            if (p.Length > 3)
                foreach (var e in p[3].Split(','))
                {
                    var kv = e.Split(':');
                    if (kv.Length == 2 && uint.TryParse(kv[0], out uint id) && int.TryParse(kv[1], out int k)) links.Add((id, (UtilityKind)k));
                }
            taint = p.Length > 4 && int.TryParse(p[4], out int tt) ? (WaterTaint)tt : WaterTaint.None;
            UtilityGrid.Invalidate();
        }
    }
}
