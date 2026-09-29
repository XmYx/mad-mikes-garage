using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Battery bank rack: [E] slots in a lead-acid battery (use_battery) for +1.5 kWh of storage, up to four.</summary>
    public class BatteryRack : MonoBehaviour, IInteractable, IPlaceState
    {
        public int installed;
        public const int Max = 4;
        const float BaseWh = 3000f, PerBattery = 1500f;

        void Start() => Apply();

        void Apply() { if (TryGetComponent<UtilityNode>(out var n)) n.batteryWh = BaseWh + installed * PerBattery; }

        public string Prompt(MadMax.Game.WastelandGame g) => installed >= Max ? "RACK FULL (" + installed + " BATTERIES)" : "[E] INSTALL BATTERY (" + installed + "/" + Max + ")";

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary || installed >= Max) return;
            if (!g.Inventory.TakeItem("use_battery")) { g.Toast("NEED A CAR BATTERY (LEAD + ACID)"); return; }
            installed++;
            Apply();
            MadMax.Audio.Sfx.Play("click", transform.position, 0.8f, 0.8f);
            g.Toast("BATTERY INSTALLED: " + Mathf.RoundToInt((BaseWh + installed * PerBattery) / 1000f * 10f) / 10f + " KWH");
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState() => installed.ToString();
        public void LoadState(string s) { int.TryParse(s, out installed); Apply(); }
    }
}
