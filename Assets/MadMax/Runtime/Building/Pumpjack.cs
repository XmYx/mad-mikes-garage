using System.Globalization;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Pumpjack: over an oil field (tar-stained ground, WorldGen.OilAt) and on power it nods its walking beam and
    /// fills its tank with crude (up to 0.5 L/s on the richest ground). [E] drains the tank into the pack.</summary>
    public class Pumpjack : MonoBehaviour, IInteractable, IPlaceState
    {
        public float stored, capacity = 300f;
        const float Rate = 0.5f;
        float field = -1f, phase;
        UtilityNode node;
        Transform beam;

        void Start()
        {
            node = GetComponent<UtilityNode>();
            beam = transform.Find("Beam");
            var t = MadMax.World.DeformableTerrain.Instance;
            field = t && t.World != null ? t.World.OilAt(transform.position.x, transform.position.z) : 0f;
        }

        bool Working => node && node.Powered && field > 0.05f && stored < capacity;

        void Update()
        {
            if (node) node.demand = field > 0.05f && stored < capacity ? 900f : 0f;
            bool work = Working;
            MadMax.Audio.Sfx.Loop(this, "hydraulic", work ? 0.25f : 0f, 0.6f, 25f);
            if (!work) return;
            stored = Mathf.Min(capacity, stored + Rate * field * Time.deltaTime);
            phase += Time.deltaTime * 1.4f;
            if (beam) beam.localRotation = Quaternion.Euler(Mathf.Sin(phase) * 16f, 0f, 0f);
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (field >= 0f && field <= 0.05f) return "PUMPJACK: NO OIL UNDER HERE";
            return "[E] DRAIN CRUDE " + Mathf.FloorToInt(stored) + "/" + capacity + " L  FIELD " + Mathf.RoundToInt(field * 100f) + "%" + (node && !node.Powered ? "  NO POWER" : "");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) return;
            int n = Mathf.FloorToInt(stored);
            if (n <= 0) { g.Toast("THE TANK IS EMPTY"); return; }
            stored -= n;
            g.Inventory.Add(ResourceType.CrudeOil, n);
            MadMax.Audio.Sfx.Play("pour", transform.position, 0.7f, 0.7f);
            g.Toast("DRAINED " + n + " L CRUDE OIL");
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState() => stored.ToString("0.#", CultureInfo.InvariantCulture);
        public void LoadState(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out stored);
    }
}
