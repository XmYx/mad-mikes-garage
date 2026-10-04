using MadMax.Items;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Ignition key of a motor vehicle (added by <see cref="VehicleDamage"/> to every driveable vehicle; pedal
    /// bikes and engineless vehicles ignore it). The key is an item "key_&lt;design&gt;_&lt;id&gt;"; where it is:
    /// <see cref="Where.Ignition"/> (it starts), <see cref="Where.Glovebox"/> (in the glovebox compartment: take it out),
    /// <see cref="Where.Carried"/> (someone has it: the player's pack counts) or <see cref="Where.Lost"/>. Vehicles
    /// spawn with the key in; wrecks roll ignition / glovebox / lost (<see cref="RollWreck"/>). Without a key the
    /// starter stays dead (<see cref="VehicleSystems.Crank"/>) until the vehicle is <see cref="hotwired"/>: anyone can
    /// try (hold the service key in the seat), badly without the Hotwiring skill. Saved in <c>VehicleSave.ignition</c>.</summary>
    public class VehicleIgnition : MonoBehaviour
    {
        public enum Where { Ignition, Glovebox, Carried, Lost }

        public Where key = Where.Ignition;
        public bool hotwired;
        /// <summary>Short id shared by the vehicle and its key item.</summary>
        public string id;
        /// <summary>A failed attempt blew a fuse: no new attempt before this time.</summary>
        [System.NonSerialized] public float blownUntil;

        VehicleDriver driver;

        void Awake() => driver = GetComponent<VehicleDriver>();

        /// <summary>The key's item id (made on first use).</summary>
        public string KeyItem
        {
            get
            {
                if (string.IsNullOrEmpty(id)) id = ((uint)Random.Range(0x100000, 0xffffff)).ToString("x6");
                var sb = new System.Text.StringBuilder("key_");
                foreach (char ch in name.Replace("(Clone)", "").Replace("Wreck ", "").ToLowerInvariant()) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                return sb.Append('_').Append(id).ToString();
            }
        }

        /// <summary>Needs a key at all: an engine that isn't pedals.</summary>
        public bool NeedsKey
        {
            get
            {
                var e = driver ? driver.Engine : null;
                var ep = e ? e.GetComponent<VehiclePart>() : null;
                return ep && ep.partId != "engine_pedals";
            }
        }

        /// <summary>The ignition answers: key in, hotwired, an AI driver, or the key in this pack.</summary>
        public bool CanStart(Inventory pack) => !NeedsKey || key == Where.Ignition || hotwired || (driver && driver.aiDriven) || Has(pack);

        public bool Has(Inventory pack) => pack != null && pack.GetItem(KeyItem) > 0;

        /// <summary>A found wreck: the key left in, in the glovebox, or gone (deterministic from the wreck's seed).</summary>
        public void RollWreck(System.Random rnd, VehicleStorage storage)
        {
            double r = rnd.NextDouble();
            id = ((uint)rnd.Next(0x100000, 0xffffff)).ToString("x6");
            hotwired = false;
            var glovebox = storage ? storage.Get(VehicleStorage.Kind.Glovebox) : null;
            if (r < 0.25) key = Where.Ignition;
            else if (r < 0.5 && glovebox) { key = Where.Glovebox; glovebox.inventory.AddItem(KeyItem); }
            else key = Where.Lost;
        }

        /// <summary>Seconds a hotwiring attempt takes at a skill level.</summary>
        public float HotwireSeconds(int level) => Mathf.Lerp(18f, 5f, level / 10f) * Difficulty;

        /// <summary>Chance an attempt works at a skill level: ~20 % for anyone, near-certain for a car thief at 10.</summary>
        public float HotwireChance(int level) => Mathf.Clamp(0.2f + level * 0.08f, 0.05f, 0.97f) / Difficulty;

        /// <summary>Bikes and machines are simple, aircraft fussy.</summary>
        float Difficulty => GetComponent<BikeBalance>() || GetComponent<Machine>() ? 0.7f : GetComponent<FlightModel>() ? 1.6f : 1f;

        public string SaveState() => (int)key + "," + (hotwired ? 1 : 0) + "," + id;

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var f = s.Split(',');
            if (f.Length > 0 && int.TryParse(f[0], out int k)) key = (Where)Mathf.Clamp(k, 0, 3);
            if (f.Length > 1) hotwired = f[1] == "1";
            if (f.Length > 2 && f[2].Length > 0) id = f[2];
        }
    }
}
