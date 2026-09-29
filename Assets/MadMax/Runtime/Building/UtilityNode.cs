using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MadMax.Building
{
    [System.Flags] public enum UtilityKind : byte { None = 0, Power = 1, Water = 2 }

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

        // water
        public float waterCapacity;
        public float clean, dirty;
        [System.NonSerialized] public float sourceDirty, filterRate;   // L/s supplied / converted by this node (set by its role component)
        [System.NonSerialized] public float sourceClean;               // L/s of clean water supplied (wells)

        internal int powerNet = -1, waterNet = -1;

        public readonly List<(uint id, UtilityKind kind)> links = new List<(uint, UtilityKind)>();

        public static readonly List<UtilityNode> All = new List<UtilityNode>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() { All.Add(this); UtilityGrid.Invalidate(); }
        void OnDisable() { All.Remove(this); UtilityGrid.Invalidate(); }

        public Vector3 PortWorld => transform.TransformPoint(port);
        public Placeable Piece => GetComponent<Placeable>();
        public uint Id => Piece ? Piece.Id : 0;

        public float Water => clean + dirty;

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
            UtilityGrid.Invalidate();
        }
    }
}
