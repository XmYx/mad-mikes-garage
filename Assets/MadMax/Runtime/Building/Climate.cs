using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Space heating / cooling: electric heater, air conditioner (power), fireplace and wood stove (burn wood).
    /// Warms (or cools) everything within <see cref="radius"/>; indoors the full effect, outdoors a fraction.</summary>
    public class Climate : MonoBehaviour, IPlaceState, IInteractable
    {
        public static readonly List<Climate> All = new List<Climate>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public float heat = 15f, radius = 7f;
        public bool burnsWood, on = true;
        public float fire;                          // minutes of burning left (wood)
        UtilityNode node;
        float smoke;

        void Awake() => node = GetComponent<UtilityNode>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool Active => on && (burnsWood ? fire > 0f : !node || node.Powered);

        void Update()
        {
            float dt = Time.deltaTime;
            if (node && !burnsWood) node.demand = on ? (heat > 0f ? 1500f : 1200f) : 0f;
            if (!burnsWood || !on || fire <= 0f) return;
            fire = Mathf.Max(0f, fire - dt / 60f);
            if ((smoke += dt) > 0.8f) { smoke = 0f; MadMax.World.Fx.Smoke(transform.TransformPoint(new Vector3(0, 2.4f, -0.1f)), Vector3.up + MadMax.World.Fx.Wind * 0.3f, 0.5f, new Color(0.3f, 0.3f, 0.3f, 0.5f), 3f); }
            if (fire <= 0f) GetComponent<Placeable>()?.Dirty();
        }

        /// <summary>Temperature offset from active heaters / coolers at a point.</summary>
        public static float At(Vector3 p)
        {
            float t = 0f;
            foreach (var c in All)
            {
                if (!c || !c.Active) continue;
                float d = Vector3.Distance(c.transform.position, p);
                if (d < c.radius) t += c.heat * (1f - d / c.radius * 0.6f);
            }
            return t;
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (burnsWood) return (fire > 0f ? (on ? "[T] PUT OUT" : "[T] LIGHT") : "[T] ADD WOOD") + "  FIRE " + Mathf.CeilToInt(fire) + " MIN";
            return (heat > 0f ? "HEATER" : "COOLER") + (on ? " ON  [T] OFF" : " OFF  [T] ON") + (node && !node.Powered ? "  NO POWER" : "");
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!secondary) return;
            if (burnsWood && (fire <= 0f || !on))
            {
                if (fire <= 0f)
                {
                    var wood = g.Inventory.Get(MadMax.Items.ResourceType.Wood) > 0 ? MadMax.Items.ResourceType.Wood : MadMax.Items.ResourceType.Charcoal;
                    if (!g.Inventory.TrySpend(wood, 1)) { g.Toast("NEED WOOD OR CHARCOAL"); return; }
                    fire += wood == MadMax.Items.ResourceType.Charcoal ? 8f : 4f;
                }
                on = true;
            }
            else on = !on;
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState() => (on ? "1" : "0") + ";" + fire.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';'); on = p[0] == "1";
            if (p.Length > 1) float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fire);
        }
    }
}
