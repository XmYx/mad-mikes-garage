using System.Collections.Generic;
using MadMax.Npc;

namespace MadMax.Story
{
    /// <summary>The authored characters of KEEP THE LIGHT ON: fixed names, titles, temperaments and looks laid over a
    /// deterministic <see cref="NpcProfile"/> (id "cast:{key}"), so dialogue, voices, trade and saves work as for anyone
    /// else. Where each one stands comes from <see cref="StoryAnchors"/>.</summary>
    public static class StoryCast
    {
        public struct Member { public string key, name, title, anchor; public Temper temper; public bool female; public string[] outfit; public string tool; }

        public static readonly Member[] All =
        {
            new Member { key = "nell", name = "NELL MERCER", title = "ROADSIDE MECHANIC", anchor = "nell", temper = Temper.Gruff, female = true, outfit = new[] { "overalls", "boots", "gloves", "beanie" }, tool = "tool_wrench" },
            new Member { key = "june", name = "JUNE 'SWITCH' BELL", title = "RELAY TECHNICIAN", anchor = "relay", temper = Temper.Joker, female = true, outfit = new[] { "hoodie", "jeans", "boots", "goggles" } },
            new Member { key = "clerk", name = "HOLLAND CROSS", title = "FREIGHT CLERK", anchor = "town1", temper = Temper.Gruff, outfit = new[] { "sweater", "pants", "boots" } },
            new Member { key = "len", name = "LEN PIKE", title = "HIRED DRIVER", anchor = "town1", temper = Temper.Nervous, outfit = new[] { "jacket", "jeans", "boots", "bandana" } },
            new Member { key = "sera", name = "SERA DUNE", title = "WATER SURVEYOR", anchor = "town1", temper = Temper.Proud, female = true, outfit = new[] { "duster", "pants", "boots", "shemagh" } },
            new Member { key = "mara", name = "MARA VALE", title = "CONVOY LEADER", anchor = "depot", temper = Temper.Proud, female = true, outfit = new[] { "bomber", "jeans", "combat_boots" } },
            new Member { key = "ada", name = "ADA VENN", title = "GUILD SUPERINTENDENT", anchor = "dispatch", temper = Temper.Proud, female = true, outfit = new[] { "coat", "pants", "boots" } },
            new Member { key = "cask", name = "BROTHER CASK", title = "KEEPER OF THE LAST ENGINE", anchor = null, temper = Temper.Pious, outfit = new[] { "poncho", "pants", "boots" } },
            new Member { key = "ivo", name = "DR. IVO RUSK", title = "REMNANT ARCHIVIST", anchor = null, temper = Temper.Nervous, outfit = new[] { "coat", "pants", "boots", "goggles" } },
        };

        static readonly Dictionary<string, NpcProfile> made = new Dictionary<string, NpcProfile>();
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => made.Clear();

        public static Member? Find(string key) { foreach (var m in All) if (m.key == key) return m; return null; }

        /// <summary>The profile of cast member <paramref name="key"/> (the same object for the whole session).</summary>
        public static NpcProfile Profile(string key, int worldSeed)
        {
            if (made.TryGetValue(key, out var p)) return p;
            var m = Find(key);
            if (m == null) return null;
            int seed = worldSeed * 7919; foreach (char ch in key) seed = unchecked(seed * 31 + ch);
            p = NpcProfile.Make("cast:" + key, NpcRole.Resident, seed);
            var mv = m.Value;
            p.fullName = mv.name; p.title = mv.title; p.temper = mv.temper; p.female = mv.female;
            p.first = mv.name.Split(' ')[0];
            if (mv.female) p.look.beard = 0;
            p.outfit.Clear(); p.outfit.AddRange(mv.outfit);
            p.tool = mv.tool;
            made[key] = p;
            return p;
        }

        /// <summary>The cast key of a spawned NPC (null for everyone else).</summary>
        public static string KeyOf(MadMax.Npc.Npc n) => n && n.Profile != null && n.Profile.id.StartsWith("cast:") ? n.Profile.id.Substring(5) : null;
    }
}
