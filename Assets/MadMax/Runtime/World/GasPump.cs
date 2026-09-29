using System.Collections.Generic;
using MadMax.Items;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Fuel pump (old town pumps with whatever is left in the tank, or a built one you fill yourself):
    /// [E] refuels the nearest vehicle through a hose, [T] fills jerry cans (inventory fuel). Built pumps: [T] also tops
    /// the pump up from the pack when the pack has fuel and the pump has room.</summary>
    public class GasPump : MonoBehaviour, MadMax.Building.IInteractable, MadMax.Building.IPlaceState
    {
        public static readonly List<GasPump> All = new List<GasPump>();
        /// <summary>Litres used from world pumps by key (saved).</summary>
        public static readonly Dictionary<string, float> Used = new Dictionary<string, float>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { All.Clear(); Used.Clear(); }

        public string key;                 // world pump (null = player-built)
        public float stock = 200f, capacity = 400f;
        /// <summary>What it pumps: petrol (Fuel) or Diesel. World pumps pick by their key; built pumps take what is poured in.</summary>
        public ResourceType kind = ResourceType.Fuel;
        bool kindSet;

        void Start()
        {
            if (kindSet || key == null) return;
            kindSet = true;
            kind = (key.GetHashCode() & 0xff) < 90 ? ResourceType.Diesel : ResourceType.Fuel;       // about a third of the old pumps sell diesel
        }
        public Vector3 nozzle = new Vector3(0.3f, 1.1f, 0.25f);

        /// <summary>Fix what a world pump sells (the refinery's diesel) instead of rolling it from the key.</summary>
        public void SetKind(ResourceType k) { kind = k; kindSet = true; }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public float Fuel
        {
            get => key != null ? Mathf.Max(0f, stock - (Used.TryGetValue(key, out var u) ? u : 0f)) : stock;
        }

        public float Take(float litres)
        {
            float got = Mathf.Min(litres, Fuel);
            if (key != null) { Used.TryGetValue(key, out var u); Used[key] = u + got; }
            else { stock -= got; GetComponent<MadMax.Building.Placeable>()?.Dirty(); }
            return got;
        }

        public Vector3 Nozzle => transform.TransformPoint(nozzle);

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            if (Fuel < 0.5f && key != null) return "PUMP: DRY";
            var v = g.NearestVehicleTo(transform.position, 7f);
            string s = ResourceInfo.Name(kind) + ": " + (v ? "[E] REFUEL " + MadMax.Game.WastelandGame.Name(v) : "PARK A VEHICLE BY THE PUMP") + "  [T] FILL CANS  " + Mathf.RoundToInt(Fuel) + "L";
            if (key == null && g.Inventory.Get(kind) > 0 && stock < capacity) s += " (T WITH " + ResourceInfo.Name(kind) + " IN PACK: TOP UP)";
            return s;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary)
            {
                if (key == null && stock < 1f && g.Inventory.Get(ResourceType.Diesel) > g.Inventory.Get(ResourceType.Fuel)) kind = ResourceType.Diesel;   // an empty pump takes what you bring
                else if (key == null && stock < 1f && g.Inventory.Get(ResourceType.Fuel) > 0) kind = ResourceType.Fuel;
                if (key == null && g.Inventory.Get(kind) > 0 && stock < capacity)
                {
                    int n = Mathf.Min(g.Inventory.Get(kind), Mathf.FloorToInt(capacity - stock));
                    g.Inventory.TrySpend(kind, n); stock += n; GetComponent<MadMax.Building.Placeable>()?.Dirty();
                    g.Toast("PUMP TOPPED UP " + n + " L");
                    return;
                }
                int got = Mathf.FloorToInt(Take(10f));
                if (got > 0) { g.Inventory.Add(kind, got); g.Toast("FILLED CANS " + got + " L " + ResourceInfo.Name(kind)); }
                return;
            }
            var v = g.NearestVehicleTo(transform.position, 7f);
            if (v) g.StartRefuel(v, this);
        }

        public string SaveState() => stock.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + (kind == ResourceType.Diesel ? ";D" : "");
        public void LoadState(string s)
        {
            if (key != null || string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out stock);
            kind = p.Length > 1 && p[1] == "D" ? ResourceType.Diesel : ResourceType.Fuel;
        }
    }
}
