using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Petrol / ethanol generator: burns fuel while switched on, feeds the power network.</summary>
    public class Generator : MonoBehaviour, IPlaceState, IInteractable
    {
        public string Prompt(MadMax.Game.WastelandGame g) =>
            (on ? "[E] STOP" : "[E] START") + (solid ? "  [T] STOKE  FIRE " : "  [T] REFUEL  ") + fuel.ToString("0.0") + "/" + tankLitres + (solid ? "" : "L") + (node ? "  LOAD " + Mathf.RoundToInt(node.demand) + "W" : "");
        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) { int n = Refuel(g.Inventory); g.Toast(n > 0 ? (solid ? "STOKED THE FIREBOX" : "ADDED " + n + "L") : solid ? "NO COAL, CHARCOAL OR WOOD" : "NO FUEL OR ETHANOL"); }
            else Toggle();
        }

        public float output = 1500f, tankLitres = 20f;
        /// <summary>Steam generator: burns coal (5 per lump), charcoal (4) or wood (2) instead of liquid fuel.</summary>
        public bool solid;
        public float fuel;
        public bool on;
        UtilityNode node;
        float smoke;

        void Awake() => node = GetComponent<UtilityNode>();

        void Update()
        {
            if (!node) return;
            bool run = on && fuel > 0f;
            MadMax.Audio.Sfx.Loop(this, "generator", run ? 0.5f : 0f, 1f, 25f);
            node.produce = run ? output : 0f;
            if (!run) return;
            float load = Mathf.Clamp(node.demand + 200f, 200f, output) / output;
            fuel = Mathf.Max(0f, fuel - load * 0.3f / 60f * Time.deltaTime);
            smoke += Time.deltaTime * (2f + load * 4f);
            if (smoke > 1f) { smoke = 0f; MadMax.World.Fx.Smoke(transform.TransformPoint(new Vector3(0.3f, 0.9f, -0.2f)), Vector3.up * 0.8f + MadMax.World.Fx.Wind * 0.3f, 0.3f, new Color(0.3f, 0.3f, 0.3f, 0.5f), 2f); }
            if (fuel <= 0f) GetComponent<Placeable>()?.Dirty();
        }

        public int Refuel(Inventory inv)
        {
            if (solid)
            {
                int lumps = 0;
                foreach (var (t, per) in new[] { (ResourceType.Coal, 5f), (ResourceType.Charcoal, 4f), (ResourceType.Wood, 2f) })
                    while (fuel + per <= tankLitres + 0.01f && inv.TrySpend(t, 1)) { fuel += per; lumps++; }
                if (lumps > 0) GetComponent<Placeable>()?.Dirty();
                return lumps;
            }
            int want = Mathf.FloorToInt(tankLitres - fuel);
            int n = 0;
            foreach (var t in new[] { ResourceType.Diesel, ResourceType.Fuel, ResourceType.Ethanol })
            {
                int take = Mathf.Min(want - n, inv.Get(t));
                if (take > 0 && inv.TrySpend(t, take)) n += take;
            }
            fuel += n;
            GetComponent<Placeable>()?.Dirty();
            return n;
        }

        public void Toggle() { on = !on; GetComponent<Placeable>()?.Dirty(); }

        public string SaveState() => (on ? "1" : "0") + ";" + fuel.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            on = p[0] == "1";
            if (p.Length > 1) float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fuel);
        }
    }
}
