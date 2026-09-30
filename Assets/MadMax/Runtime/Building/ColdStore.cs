using System.Globalization;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Cold storage (depth stage F): a fridge, chest freezer or ice box keeps its own temperature. The
    /// compressor pulls it down to 4 °C (freezer −18 °C) only while powered; after an outage it warms towards the air
    /// over a few minutes. Food keeps by temperature (<see cref="SpoilFactor"/>: frozen stops rot, chilled slows it to
    /// an eighth). A freezer turns water left in it into ice; an ice box has no power and stays cold on ice (or cold
    /// weather). A dead compressor (<see cref="broken"/>) needs a generator coil ([T]).</summary>
    public class ColdStore : MonoBehaviour, IPlaceState, IInteractable
    {
        public enum Kind { Fridge, Freezer, IceBox }
        public Kind kind;
        public float watts = 150f;
        /// <summary>Inside temperature (°C); NaN until the first update, which starts it at the air temperature.</summary>
        public float temp = float.NaN;
        public bool broken;
        public const float CoolRate = 0.8f, IceSeconds = 240f, IceMakeSeconds = 20f;
        public const string Ice = "misc_ice";
        /// <summary>Seconds for the inside to close most of the gap to the air with the power off.</summary>
        float Insulation => kind == Kind.Freezer ? 900f : kind == Kind.IceBox ? 480f : 600f;
        float iceLeft, iceMake;
        UtilityNode node;
        Container box;

        void Awake() { node = GetComponent<UtilityNode>(); box = GetComponent<Container>(); }

        public float Setpoint => kind == Kind.Freezer ? -18f : 4f;
        public bool Running => kind != Kind.IceBox && !broken && node && node.Powered;
        public bool Chilled => !float.IsNaN(temp) && temp <= 8f;
        public bool Frozen => !float.IsNaN(temp) && temp <= -5f;

        /// <summary>Rot speed against the open pack: frozen 0, chilled 0.12, rising to 1 at 20 °C.</summary>
        public float SpoilFactor
        {
            get
            {
                if (float.IsNaN(temp)) return 1f;
                if (temp <= -5f) return 0f;
                if (temp <= 8f) return 0.12f;
                return Mathf.Lerp(0.12f, 1f, (temp - 8f) / 12f);
            }
        }

        public string Label
        {
            get
            {
                if (float.IsNaN(temp)) return "...";
                string t = MadMax.Game.GameSettings.Current != null ? MadMax.Game.GameSettings.Current.Temp(temp) : Mathf.RoundToInt(temp) + "C";
                if (broken) return t + " COMPRESSOR DEAD";
                if (kind == Kind.IceBox) return t + (iceLeft > 0f ? " ON ICE" : Chilled ? "" : " NEEDS ICE");
                if (!node || !node.Powered) return t + (Chilled ? " NO POWER" : " NO POWER, WARM");
                return t + (Frozen ? " FROZEN" : Chilled ? " COLD" : " COOLING");
            }
        }

        static float Air => MadMax.World.Weather.Temperature;

        void Update()
        {
            float dt = Time.deltaTime;
            if (float.IsNaN(temp)) temp = Air;
            if (node && kind != Kind.IceBox) node.demand = broken ? 0f : temp > Setpoint - 1f ? watts : watts * 0.2f;   // the compressor cycles once cold
            if (kind == Kind.IceBox && iceLeft <= 0f && box && box.inventory.GetItem(Ice) > 0 && Air > 4f)
            {
                box.inventory.TakeItem(Ice);                                   // the next block starts melting
                iceLeft = IceSeconds;
            }
            if (Running) temp = Mathf.MoveTowards(temp, Setpoint, CoolRate * dt);
            else if (kind == Kind.IceBox && iceLeft > 0f) { iceLeft -= dt; temp = Mathf.MoveTowards(temp, 3f, CoolRate * 0.5f * dt); }
            else temp += (Air - temp) * Mathf.Clamp01(dt * 3f / Insulation);
            // water left in a running freezer turns to ice
            if (kind == Kind.Freezer && Frozen && box && box.inventory.Get(MadMax.Items.ResourceType.Water) > 0)
            {
                if ((iceMake += dt) >= IceMakeSeconds && box.inventory.TrySpend(MadMax.Items.ResourceType.Water, 1)) { iceMake = 0f; box.inventory.AddItem(Ice); }
            }
            else iceMake = 0f;
        }

        public string Prompt(MadMax.Game.WastelandGame g) => broken ? "[T] REPAIR COMPRESSOR (GENERATOR COIL + COPPER)" : null;

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!secondary || !broken) return;
            if (g.Inventory.GetItem(MadMax.Items.ItemIds.Coil) <= 0 || g.Inventory.Get(MadMax.Items.ResourceType.Copper) < 1) { g.Toast("NEED A GENERATOR COIL AND 1 COPPER"); return; }
            g.Inventory.TakeItem(MadMax.Items.ItemIds.Coil);
            g.Inventory.TrySpend(MadMax.Items.ResourceType.Copper, 1);
            broken = false;
            g.Stats.Practice(MadMax.RPG.Skill.Mechanics, 3f);
            MadMax.Audio.Sfx.Play("ratchet", transform.position, 0.7f);
            g.Toast("COMPRESSOR REPAIRED: IT HUMS AGAIN");
            MadMax.Story.Story.Note("fridge_repaired");
            GetComponent<Placeable>()?.Dirty();
        }

        // state: "temp;broken;ice seconds"
        public string SaveState() => (float.IsNaN(temp) ? "" : temp.ToString("0.#", CultureInfo.InvariantCulture)) + ";" + (broken ? "1" : "0") + ";" + iceLeft.ToString("0", CultureInfo.InvariantCulture);
        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            temp = p[0].Length > 0 && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float t) ? t : float.NaN;
            broken = p.Length > 1 && p[1] == "1";
            if (p.Length > 2) float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out iceLeft);
        }
    }
}
