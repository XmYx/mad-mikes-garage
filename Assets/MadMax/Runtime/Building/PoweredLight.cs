using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A light that needs power (and a switch). Lights itself only at night when set to auto.</summary>
    public class PoweredLight : MonoBehaviour, IPlaceState, IInteractable
    {
        public string Prompt(MadMax.Game.WastelandGame g) => (on ? "[E] SWITCH OFF" : "[E] SWITCH ON") + (needsPower && node && !node.Powered ? "  NO POWER" : "");
        public void Use(MadMax.Game.WastelandGame g, bool secondary) { if (!secondary) Toggle(); }

        public float watts = 60f;
        public bool on = true;
        public bool needsPower = true;
        Light lamp;
        UtilityNode node;
        void Awake() { lamp = GetComponentInChildren<Light>(); node = GetComponent<UtilityNode>(); }

        void Update()
        {
            bool dark = MadMax.World.DayNight.Darkness > 0.25f;
            bool want = on && dark;
            if (node) node.demand = want ? watts : 0f;
            bool lit = want && (!needsPower || (node && node.Powered));
            if (lamp) lamp.enabled = lit && MadMax.World.LightBudget.Allowed(lamp);
        }

        public void Toggle() { on = !on; GetComponent<Placeable>()?.Dirty(); }
        public string SaveState() => on ? "1" : "0";
        public void LoadState(string s) { on = s != "0"; }
    }
}
