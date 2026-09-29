using MadMax.Game;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A docking collar on a sea dome or tunnel (user additions): a submarine that settles its belly hatch (the
    /// middle of its keel) within 3.5 m of the collar's mouth is docked — the base's power charges its battery and the base air tops up its cabin.
    /// [E] at the collar climbs into the docked submarine; leaving the submarine through its hatch while docked comes
    /// back down into the base (see <see cref="WastelandGame"/>).</summary>
    public class DockingCollar : MonoBehaviour, IInteractable
    {
        public static readonly System.Collections.Generic.List<DockingCollar> All = new System.Collections.Generic.List<DockingCollar>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        /// <summary>Local point of the collar's mouth (where the sail meets it) and where you stand inside below it.</summary>
        public Vector3 mouth = new Vector3(0f, 0.8f, 0f), inside = new Vector3(0f, -2.2f, 0f);
        public Submarine Docked { get; private set; }
        UtilityNode node;
        float check;

        void Awake() { node = GetComponent<UtilityNode>(); }
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public Vector3 InsideWorld => transform.TransformPoint(inside);

        void Update()
        {
            if ((check -= Time.deltaTime) > 0f) return;
            check = 1f;
            var m = transform.TransformPoint(mouth);
            Docked = null;
            var g = WastelandGame.Instance;
            if (!g) return;
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.TryGetComponent<Submarine>(out var sub) || !v.TryGetComponent<BoatModel>(out var hull)) continue;
                var belly = v.transform.TransformPoint(new Vector3(0f, hull.keelY, 0f));
                if ((belly - m).sqrMagnitude < 3.5f * 3.5f) { Docked = sub; break; }
            }
            if (Docked)
            {
                bool power = node && node.Powered;
                if (node) node.demand = 3000f;
                Docked.Dock(power ? 3000f : 0f);
            }
            else if (node) node.demand = 0f;
        }

        public string Prompt(WastelandGame g) => Docked ? "DOCKING COLLAR: [E] CLIMB INTO THE SUBMARINE" + (node && node.Powered ? " (CHARGING)" : " (NO POWER)") : "DOCKING COLLAR: NOTHING DOCKED";

        public void Use(WastelandGame g, bool secondary)
        {
            if (!Docked || secondary) return;
            var space = Docked.GetComponent<InteriorSpace>();
            if (space) g.BoardInterior(space);
        }

        /// <summary>The collar a submarine is docked at (null = none).</summary>
        public static DockingCollar Of(Submarine sub)
        {
            foreach (var c in All) if (c && c.Docked == sub) return c;
            return null;
        }
    }
}
