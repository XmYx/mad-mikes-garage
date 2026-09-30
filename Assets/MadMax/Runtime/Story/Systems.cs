using System.Collections.Generic;

namespace MadMax.Story
{
    /// <summary>The missing-system register (storyline N0): every verb a quest relies on, whether the game supports it
    /// yet, and which depth stage or story stage delivers it. A quest only becomes Playable when all its needs are
    /// ready; dialogue never promises a verb that is still missing.</summary>
    public static class Systems
    {
        public struct Entry { public string key, what, stage; public bool ready; }

        static readonly Dictionary<string, Entry> all = new Dictionary<string, Entry>();

        static void E(string key, bool ready, string stage, string what) => all[key] = new Entry { key = key, ready = ready, stage = stage, what = what };

        static Systems()
        {
            // already in the game
            E("driving", true, "-", "drive, fuel, service, repair vehicles");
            E("salvage", true, "-", "wrench parts off wrecks, cutter strips shells");
            E("building", true, "-", "build pieces, claim flag, repair (R)");
            E("cooking", true, "A", "campfire, stove, oven, range, cannery, brewing");
            E("garden", true, "-", "garden plots, planters, watering, scarecrows, crows");
            E("power", true, "-", "generators, solar, wind, cables, batteries");
            E("water", true, "-", "rain collector, well, pump, filter, pipes, tanks");
            E("machinery", true, "-", "excavator, backhoe, dozer, dump truck, paver, roller");
            E("towing", true, "-", "hitch, tow, winch, crane");
            E("dialogue", true, "-", "talk, persuade, haggle, parley");
            E("trade", true, "-", "buy, sell, town boards, contracts");
            E("factions", true, "-", "standing, ranks, territory");
            E("radio", true, "-", "stations, receivers, news flashes");
            E("fishing", true, "-", "rod, species, weigh records");
            E("diving", true, "-", "dive gear, air, boats, submarine");
            E("aircraft", true, "-", "trike, gyrocopter, airfields");
            E("boats", true, "-", "rafts, skiffs, trawlers, houseboats");
            E("animals", true, "-", "taming, pens, troughs, riding");
            E("companions", true, "-", "followers, passenger seats, orders");
            E("racing", true, "-", "board races, time trials");
            E("last_engine", true, "-", "relic hunt and V12 recipe");
            E("parkour", true, "-", "vault, mantle, grapple");
            E("sewing", true, "-", "clothes, mending, armour");
            E("story_start", true, "N1", "STORY start: almost nothing, a stranded car, Nell's stop");
            E("cast", true, "N1", "authored characters placed at story anchors");
            // still to build (depth stages B-I and story stages N1-N5)
            E("evidence", false, "N2", "evidence records that survive losing one copy");
            E("fields", true, "B", "tilled field beds on open ground");
            E("tractor", true, "B", "tractor with plough, seeder, harvester, sprayer");
            E("irrigation_control", true, "B", "irrigation timers and river pumps");
            E("roads", false, "C", "gravel, road paint, signs, player roads on the map");
            E("bridges", false, "C", "timber and steel bridge decks");
            E("cold_storage", false, "F", "fridges that keep food by temperature and power");
            E("water_quality", false, "F", "water samples, contamination and clean-up");
            E("power_control", false, "F", "switches, timers, priority loads, outages");
            E("animal_treatment", false, "E", "treat an injured animal");
            E("clinic", false, "G", "clinic bed and treatment of others");
            E("nonlethal_bout", false, "N4", "supervised fist fight with a stop state");
            E("performance", false, "N4", "a public recital or ceremony scene");
            E("transfer", false, "N3", "readiness-gated rescue / escort operations");
            E("broadcast", false, "N3", "prepare and air a broadcast with choices");
            E("residents", false, "N3", "residents who staff a service at the home");
            E("allocation", false, "N3", "assign a finite shipment to customers");
            E("player_convoy", false, "N3", "lead a convoy of allied drivers");
            E("relocation", false, "N5", "move the campaign home");
        }

        public static bool Ready(string key) => all.TryGetValue(key, out var e) && e.ready;
        public static bool Known(string key) => all.ContainsKey(key);
        public static IEnumerable<Entry> All => all.Values;
        public static Entry Get(string key) => all.TryGetValue(key, out var e) ? e : default;
    }
}
