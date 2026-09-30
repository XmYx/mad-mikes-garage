using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Irrigation timer (depth stage B): sprinklers and drip lines on the same water network only run in its
    /// window (dawn and dusk, dawn only, or overnight) unless a bed is bone dry. Watering in the heat of the day loses
    /// water to evaporation (<see cref="Cost"/>), so a timed network stretches a tank further. [E] cycles the window.</summary>
    public class IrrigationTimer : MonoBehaviour, IPlaceState, IInteractable
    {
        public static readonly List<IrrigationTimer> All = new List<IrrigationTimer>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public int mode;                                                     // 0 dawn + dusk, 1 dawn, 2 overnight
        static readonly string[] Names = { "DAWN AND DUSK", "DAWN ONLY", "OVERNIGHT" };
        UtilityNode node;

        void Awake() => node = GetComponent<UtilityNode>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool Open(float h) => mode switch
        {
            0 => (h >= 5f && h < 8f) || (h >= 18f && h < 21f),
            1 => h >= 5f && h < 8.5f,
            _ => h >= 21f || h < 5f
        };

        /// <summary>Water drawn per watering at this hour on <paramref name="n"/>'s network, relative to the cool hours;
        /// 0 = a timer holds it now. Midday without a timer costs 60 % more (evaporation).</summary>
        public static float Cost(UtilityNode n, bool bedDry)
        {
            float h = MadMax.World.DayNight.Hours;
            if (n)
                foreach (var t in All)
                    if (t && t.node && t.node.waterNet >= 0 && t.node.waterNet == n.waterNet)
                        return t.Open(h) || bedDry ? 1f : 0f;
            return h >= 10f && h < 16f ? 1.6f : 1f;
        }

        public string Prompt(MadMax.Game.WastelandGame g) => "IRRIGATION TIMER: " + Names[mode] + (Open(MadMax.World.DayNight.Hours) ? " (RUNNING)" : " (WAITING)") + "  [E] CHANGE";
        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) return;
            mode = (mode + 1) % Names.Length;
            g.Toast("IRRIGATION: " + Names[mode]);
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState() => mode.ToString();
        public void LoadState(string s) { if (int.TryParse(s, out int m)) mode = Mathf.Clamp(m, 0, Names.Length - 1); }
    }
}
