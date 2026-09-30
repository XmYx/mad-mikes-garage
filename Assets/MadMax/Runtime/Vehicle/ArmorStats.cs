using System.Collections.Generic;

namespace MadMax.Vehicles
{
    /// <summary>Crash behaviour of armour-type parts: share of the impact they soak up (instead of the body and nearby parts),
    /// spike damage dealt to what they hit, and ram power (carving into props, denting other vehicles).</summary>
    public static class ArmorStats
    {
        public struct Stats { public float absorb, spikes, ram; }

        static readonly Dictionary<string, Stats> table = new Dictionary<string, Stats>
        {
            { "bumper_ram", new Stats { absorb = 0.6f, ram = 2.2f } },
            { "bumper_spiked", new Stats { absorb = 0.4f, spikes = 1.6f, ram = 1.1f } },
            { "rear_spiked", new Stats { absorb = 0.4f, spikes = 1.3f } },
            { "armor_spikes", new Stats { absorb = 0.3f, spikes = 2.0f } },
            { "armor_plate", new Stats { absorb = 0.7f } },
            { "bumper_bull_bar", new Stats { absorb = 0.4f, ram = 1.2f } },
            { "bumper_plow", new Stats { absorb = 0.6f, ram = 1.8f } },
            { "tool_dozer_blade", new Stats { absorb = 0.7f, ram = 2.0f } },
            // depth stage D
            { "armor_window_mesh", new Stats { absorb = 0.3f } },
            { "armor_skirt_spiked", new Stats { absorb = 0.45f, spikes = 1.5f, ram = 0.4f } },
            { "armor_sloped", new Stats { absorb = 0.85f } },
        };

        public static bool Get(string partId, out Stats s) => table.TryGetValue(partId ?? "", out s);
    }
}
