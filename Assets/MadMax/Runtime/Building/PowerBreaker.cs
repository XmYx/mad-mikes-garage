using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Load breaker (depth stage F): cable it between the supply and a branch of loads. Everything on the far
    /// side from the power sources takes its priority — ESSENTIAL, NORMAL or LOW. When supply and batteries fall short,
    /// the grid sheds LOW loads first, then NORMAL; essentials stay on as long as anything can carry them, and a
    /// generator only stalls when essential or normal loads would go dark ([E] cycles the priority).</summary>
    public class PowerBreaker : MonoBehaviour, IPlaceState, IInteractable
    {
        public static readonly string[] Names = { "ESSENTIAL", "NORMAL", "LOW (SHED FIRST)" };
        public int priority;
        UtilityNode node;

        void Awake() => node = GetComponent<UtilityNode>();
        void Start() => Apply();

        void Apply()
        {
            if (!node) return;
            node.breaker = Mathf.Clamp(priority, 0, 2);
            UtilityGrid.Invalidate();
        }

        /// <summary>The loads behind it are dark because the net ran short.</summary>
        public bool Shedding => node && node.NetLevel < priority && (node.Powered || node.Shed);

        public void SetPriority(int p) { priority = Mathf.Clamp(p, 0, 2); Apply(); GetComponent<Placeable>()?.Dirty(); }

        public string Prompt(MadMax.Game.WastelandGame g) => "LOAD BREAKER: " + Names[Mathf.Clamp(priority, 0, 2)] + (Shedding ? "  SHED: SHORT OF POWER" : "") + "  [E] CHANGE";

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary) return;
            SetPriority((priority + 1) % Names.Length);
            MadMax.Audio.Sfx.Play("click", transform.position, 0.7f, 0.9f);
            g.Toast("BREAKER: LOADS BEHIND IT ARE " + Names[priority]);
        }

        public string SaveState() => priority.ToString();
        public void LoadState(string s) { if (int.TryParse(s, out int p)) { priority = Mathf.Clamp(p, 0, 2); Apply(); } }
    }
}
