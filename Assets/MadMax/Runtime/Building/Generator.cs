using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Petrol / ethanol generator: burns fuel while switched on, feeds the power network. Overloaded (its net
    /// can't carry the essential and normal loads) it bogs down and stalls after a few seconds: [E] restarts it once
    /// the load is down (depth stage F outages).</summary>
    public class Generator : MonoBehaviour, IPlaceState, IInteractable
    {
        public string Prompt(MadMax.Game.WastelandGame g) =>
            (stalled && !on ? "STALLED: OVERLOADED  [E] RESTART" : on ? "[E] STOP" : "[E] START") + (solid ? "  [T] STOKE  FIRE " : gas ? "  [T] FILL GAS  " : "  [T] REFUEL  ") + fuel.ToString("0.0") + "/" + tankLitres + (solid ? "" : "L") + (node ? "  LOAD " + Mathf.RoundToInt(node.load) + "/" + Mathf.RoundToInt(output) + "W" : "");
        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) { int n = Refuel(g.Inventory); g.Toast(n > 0 ? (solid ? "STOKED THE FIREBOX" : "ADDED " + n + "L") : solid ? "NO COAL, CHARCOAL OR WOOD" : gas ? "NO BIOGAS: FILL UP AT A DIGESTER" : "NO FUEL OR ETHANOL"); }
            else Toggle();
        }

        public float output = 1500f, tankLitres = 20f;
        /// <summary>Steam generator: burns coal (5 per lump), charcoal (4) or wood (2) instead of liquid fuel.</summary>
        public bool solid;
        /// <summary>Biogas generator: burns Biogas (litres of gas) from the pack or a digester's hose.</summary>
        public bool gas;
        /// <summary>Litres (or firebox units) burnt per minute at full load.</summary>
        public float burnRate = 0.3f;
        public float fuel;
        public bool on;
        /// <summary>Stopped by an overload (not by hand): the prompt says so until it is restarted.</summary>
        public bool stalled;
        public const float StallAfter = 3f;
        UtilityNode node;
        float smoke, strain;

        void Awake() => node = GetComponent<UtilityNode>();

        void Update()
        {
            if (!node) return;
            bool run = on && fuel > 0f;
            MadMax.Audio.Sfx.Loop(this, "generator", run ? 0.5f : 0f, 1f, 25f);
            node.produce = run ? output : 0f;
            if (!run) { strain = 0f; return; }
            if (node.Overloaded) { if ((strain += Time.deltaTime) > StallAfter) { Stall(); return; } }
            else strain = 0f;
            float load = Mathf.Clamp(node.demand + 200f, 200f, output) / output;
            fuel = Mathf.Max(0f, fuel - load * burnRate / 60f * Time.deltaTime);
            smoke += Time.deltaTime * (2f + load * 4f);
            if (smoke > 1f) { smoke = 0f; MadMax.World.Fx.Smoke(transform.TransformPoint(new Vector3(0.3f, 0.9f, -0.2f)), Vector3.up * 0.8f + MadMax.World.Fx.Wind * 0.3f, 0.3f, new Color(0.3f, 0.3f, 0.3f, 0.5f), 2f); }
            if (fuel <= 0f) GetComponent<Placeable>()?.Dirty();
        }

        public int Refuel(Inventory inv)
        {
            if (gas)
            {
                int take = Mathf.Min(Mathf.FloorToInt(tankLitres - fuel), inv.Get(ResourceType.Biogas));
                if (take <= 0 || !inv.TrySpend(ResourceType.Biogas, take)) return 0;
                fuel += take;
                GetComponent<Placeable>()?.Dirty();
                return take;
            }
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

        public void Toggle()
        {
            on = !on;
            if (on && stalled) { stalled = false; MadMax.Story.Story.Note("generator_restarted"); }
            GetComponent<Placeable>()?.Dirty();
        }

        /// <summary>Bogged down by more load than it can carry: it coughs and stops; [E] restarts it.</summary>
        public void Stall()
        {
            on = false; stalled = true; strain = 0f;
            node.produce = 0f;
            MadMax.Audio.Sfx.Play("sputter", transform.position, 0.8f, 0.9f, 30f);
            for (int i = 0; i < 4; i++) MadMax.World.Fx.Smoke(transform.TransformPoint(new Vector3(0.3f, 0.9f, -0.2f)), Vector3.up * 0.6f + Random.insideUnitSphere * 0.3f, 0.35f, new Color(0.15f, 0.15f, 0.15f, 0.7f), 2.5f);
            MadMax.Game.WastelandGame.Instance?.OnGeneratorStall(this);
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState() => (on ? "1" : stalled ? "2" : "0") + ";" + fuel.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            on = p[0] == "1"; stalled = p[0] == "2";
            if (p.Length > 1) float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fuel);
        }
    }
}
