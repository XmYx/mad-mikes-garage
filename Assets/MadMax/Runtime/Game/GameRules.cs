using System;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Sandbox / RPG settings chosen in the NEW GAME menu. Saved with the world.</summary>
    [Serializable]
    public class GameRules
    {
        public int seed = 7;
        public bool randomSeed = true;
        public int difficulty = 1;          // 0 easy .. 3 brutal
        public int startingKit = 1;         // 0 nothing, 1 basic, 2 full workshop
        public int fleet;                   // 0 full fleet, 1 scavenger, 2 trabant, 3 on foot
        public float yield = 1f;            // resource yield multiplier
        public float fuelUse = 1f;          // fuel/oil consumption multiplier
        public float learning = 1f;         // skill XP multiplier
        public float damage = 1f;           // damage taken multiplier
        public int wrecks = 36;
        public int weather = 2;             // 0 never, 1 rare, 2 normal, 3 often
        public int season;                  // starting season: 0 summer, 1 autumn, 2 winter, 3 spring
        public int seasonLength = 2;        // index into SeasonLengths
        public bool snow = true;
        public float biomeScale = 1f;       // size of biome regions
        public bool permadeath;
        public int dayLength = 2;           // index into DayLengths
        public float hungerRate = 1f;       // hunger / thirst / hygiene drain multiplier
        public bool survival = true;        // hunger, thirst, hygiene on/off
        public int loot = 1;                // 0 scarce, 1 normal, 2 plenty
        public int raids = 2;               // raids on claimed bases: 0 never, 1 rare, 2 normal, 3 often (BaseRaid.Intervals)

        public static readonly float[] DayLengths = { 0f, 12f, 24f, 48f, 96f };
        public static readonly string[] DayLengthNames = { "ENDLESS DAY", "12 MIN", "24 MIN", "48 MIN", "96 MIN" };
        public static readonly string[] LootNames = { "SCARCE", "NORMAL", "PLENTY" };
        public static readonly string[] RaidNames = { "NEVER", "RARE (6 DAYS)", "NORMAL (3 DAYS)", "OFTEN (1.5 DAYS)" };

        public static readonly string[] DifficultyNames = { "EASY", "NORMAL", "HARD", "BRUTAL" };
        public static readonly string[] KitNames = { "NOTHING", "BASIC", "FULL WORKSHOP" };
        public static readonly string[] FleetNames = { "FULL FLEET", "SCAVENGER", "TRABANT", "ON FOOT" };
        public static readonly string[] WeatherNames = { "NEVER RAINS", "RARE", "NORMAL", "STORMY" };
        public static readonly string[] SeasonNames = { "SUMMER", "AUTUMN", "WINTER", "SPRING" };
        public static readonly int[] SeasonLengths = { 0, 2, 4, 8 };
        public static readonly string[] SeasonLengthNames = { "NEVER CHANGES", "2 DAYS", "4 DAYS", "8 DAYS" };

        /// <summary>Rules of the running game (defaults until a game starts).</summary>
        public static GameRules Current = new GameRules();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Current = new GameRules(); }

        public float DamageTaken => damage * (0.6f + difficulty * 0.3f);
        public GameRules Clone() => (GameRules)MemberwiseClone();
    }
}
