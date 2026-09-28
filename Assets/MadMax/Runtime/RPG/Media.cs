using System.Collections.Generic;

namespace MadMax.RPG
{
    public enum MediaKind { Book, Tape }

    /// <summary>Books and VHS tapes: consumed over time (reading anywhere, tapes on a TV) for skill XP and knowledge.</summary>
    public class MediaDef
    {
        public string id, name;
        public MediaKind kind;
        public Skill skill;
        public float xp;
        public float seconds = 20f;
        public string[] teaches = new string[0];
    }

    /// <summary>A workbench research project: spend materials and time to discover knowledge.</summary>
    public class ResearchDef
    {
        public string id, name, description, grants;
        public (MadMax.Items.ResourceType type, int amount)[] cost;
        public float seconds = 15f;
        public Skill skill = Skill.Crafting;
        public int minLevel;
    }

    public static class MediaLibrary
    {
        static List<MediaDef> media;
        static List<ResearchDef> research;

        public static IReadOnlyList<MediaDef> All => media ??= BuildMedia();
        public static IReadOnlyList<ResearchDef> Research => research ??= BuildResearch();
        public static MediaDef Get(string id) { foreach (var m in All) if (m.id == id) return m; return null; }
        public static bool IsMedia(string id) => id != null && (id.StartsWith("book_") || id.StartsWith("vhs_"));

        static MediaDef B(string id, string name, Skill s, float xp, float seconds, params string[] teaches) =>
            new MediaDef { id = id, name = name, kind = id.StartsWith("vhs_") ? MediaKind.Tape : MediaKind.Book, skill = s, xp = xp, seconds = seconds, teaches = teaches };

        static List<MediaDef> BuildMedia() => new List<MediaDef>
        {
            B("book_mechanics_1", "WASTELAND MECHANICS I", Skill.Mechanics, 120, 25, "k_radiator", "k_tyres"),
            B("book_mechanics_2", "WASTELAND MECHANICS II", Skill.Mechanics, 260, 35, "k_truck_parts"),
            B("book_gunsmith", "GUNSMITH'S NOTES", Skill.Firearms, 150, 25, "k_firearms"),
            B("book_builder", "BUILDER'S HANDBOOK", Skill.Construction, 150, 25, "k_walls", "k_electric"),
            B("book_scrapper", "THE SCRAPPER'S BIBLE", Skill.Salvaging, 150, 25),
            B("book_chemistry", "HOME CHEMISTRY", Skill.Crafting, 140, 30, "k_coolant", "k_molotov"),
            B("vhs_driving", "VHS: DRIVING SCHOOL", Skill.Driving, 200, 30, "k_drifting"),
            B("vhs_survival", "VHS: DESERT SURVIVAL", Skill.Survival, 200, 30),
            B("vhs_karate", "VHS: STREET KARATE", Skill.Melee, 200, 30),
            B("vhs_demolition", "VHS: DEMOLITION DERBY", Skill.Demolition, 180, 30, "k_weapon_mounts"),
        };

        static ResearchDef R(string id, string name, string grants, string desc, int minLevel, params (MadMax.Items.ResourceType, int)[] cost) =>
            new ResearchDef { id = id, name = name, grants = grants, description = desc, minLevel = minLevel, cost = cost };

        static List<ResearchDef> BuildResearch()
        {
            var S = MadMax.Items.ResourceType.Scrap; var G = MadMax.Items.ResourceType.Glass; var Rb = MadMax.Items.ResourceType.Rubber; var C = MadMax.Items.ResourceType.Cloth;
            return new List<ResearchDef>
            {
                R("r_radiator", "STUDY COOLING", "k_radiator", "LEARN TO BUILD RADIATORS", 0, (S, 6)),
                R("r_tyres", "VULCANISING", "k_tyres", "LEARN TO MAKE WHEELS", 1, (Rb, 4), (S, 2)),
                R("r_walls", "STRUCTURAL FRAMES", "k_walls", "WALLS AND BIG BUILDS", 1, (S, 8)),
                R("r_electric", "WIRING", "k_electric", "LIGHTS AND TV", 2, (S, 6), (G, 2)),
                R("r_firearms", "PIPE BALLISTICS", "k_firearms", "GUNS AND SHELLS", 3, (S, 12)),
                R("r_mounts", "WEAPON MOUNTS", "k_weapon_mounts", "VEHICLE TURRETS", 4, (S, 20)),
                R("r_coolant", "COOLANT MIX", "k_coolant", "BREW COOLANT", 1, (G, 2), (C, 1)),
                R("r_truck", "HEAVY PARTS", "k_truck_parts", "TRUCK RADIATORS AND STACKS", 3, (S, 14)),
            };
        }
    }
}
