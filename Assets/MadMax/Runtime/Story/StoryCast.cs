using System.Collections.Generic;
using MadMax.Npc;

namespace MadMax.Story
{
    /// <summary>The authored characters of KEEP THE LIGHT ON: fixed names, titles, temperaments and looks laid over a
    /// deterministic <see cref="NpcProfile"/> (id "cast:{key}"), so dialogue, voices, trade and saves work as for anyone
    /// else. Where each one stands comes from <see cref="StoryAnchors"/>.</summary>
    public static partial class StoryCast
    {
        public struct Member
        {
            public string key, name, title, anchor; public Temper temper; public bool female; public string[] outfit; public string tool;
            /// <summary>Optional: when they are around (default: while a quest they give or talk in is open/active).</summary>
            public System.Func<bool> present;
            /// <summary>Optional: the anchor they stand at right now (default: <see cref="anchor"/>).</summary>
            public System.Func<string> anchorNow;
        }

        static readonly Member[] Core =
        {
            new Member { key = "nell", name = "NELL MERCER", title = "ROADSIDE MECHANIC", anchor = "nell", temper = Temper.Gruff, female = true, outfit = new[] { "overalls", "boots", "gloves", "beanie" }, tool = "tool_wrench" },
            new Member { key = "june", name = "JUNE 'SWITCH' BELL", title = "RELAY TECHNICIAN", anchor = "relay", temper = Temper.Joker, female = true, outfit = new[] { "hoodie", "jeans", "boots", "goggles" } },
            new Member { key = "clerk", name = "HOLLAND CROSS", title = "FREIGHT CLERK", anchor = "a2_office", temper = Temper.Gruff, outfit = new[] { "sweater", "pants", "boots" } },
            new Member { key = "len", name = "LEN PIKE", title = "HIRED DRIVER", anchor = "a2_stall", temper = Temper.Nervous, outfit = new[] { "jacket", "jeans", "boots", "bandana" } },
            new Member { key = "sera", name = "SERA DUNE", title = "WATER SURVEYOR", anchor = "town1", temper = Temper.Proud, female = true, outfit = new[] { "duster", "pants", "boots", "shemagh" } },
            new Member { key = "mara", name = "MARA VALE", title = "CONVOY LEADER", anchor = "depot", temper = Temper.Proud, female = true, outfit = new[] { "bomber", "jeans", "combat_boots" } },
            new Member { key = "ada", name = "ADA VENN", title = "GUILD SUPERINTENDENT", anchor = "dispatch", temper = Temper.Proud, female = true, outfit = new[] { "coat", "pants", "boots" } },
            new Member { key = "mae", name = "MAE HOLLOWAY", title = "COOK", anchor = "a2_diner", temper = Temper.Friendly, female = true, outfit = new[] { "tshirt", "pants", "boots", "bandana" } },
            new Member { key = "sol", name = "SOL MOSS", title = "UNDERTAKER", anchor = "chapel", temper = Temper.Pious, outfit = new[] { "coat", "pants", "boots", "cowboy" } },
            new Member { key = "jo", name = "JO KETTLE", title = "FARMER", anchor = "jo", temper = Temper.Gruff, female = true, outfit = new[] { "overalls", "boots", "sunhat", "gloves" }, tool = "tool_shovel" },
            new Member { key = "una", name = "UNA PRITCH", title = "GARDENER", anchor = "una", temper = Temper.Joker, female = true, outfit = new[] { "overalls", "sunhat", "boots", "gloves" }, tool = "tool_hoe" },
            new Member { key = "gus", name = "GUS ALDER", title = "GRANDFATHER", anchor = "gus", temper = Temper.Friendly, outfit = new[] { "sweater", "pants", "boots", "cowboy" } },
            new Member { key = "vic", name = "VIC ALVAREZ", title = "SALT-ROUTE DRIVER", anchor = "garage_yard", temper = Temper.Joker, female = true, outfit = new[] { "jacket", "jeans", "boots", "goggles" } },
            new Member { key = "ezra", name = "EZRA POOLE", title = "GARDENER", anchor = "garage_yard", temper = Temper.Nervous, outfit = new[] { "sweater", "pants", "boots", "beanie" } },
            new Member { key = "guard1", name = "KURTZ", title = "GUILD GUARD", anchor = "depot_gate", temper = Temper.Greedy, outfit = new[] { "vest_scrap", "pants", "combat_boots", "helmet" }, tool = "tool_pipe_shotgun" },
            new Member { key = "guard2", name = "DOYLE", title = "GUILD GUARD", anchor = "depot_gate", temper = Temper.Gruff, outfit = new[] { "jacket", "pants", "boots", "bandana" }, tool = "tool_pipe_club" },
            new Member { key = "ren", name = "REN OKAFOR", title = "CONVOY MECHANIC", anchor = "depot_store", temper = Temper.Proud, outfit = new[] { "overalls", "boots", "gloves" } },
            new Member { key = "cask", name = "BROTHER CASK", title = "KEEPER OF THE LAST ENGINE", anchor = null, temper = Temper.Pious, outfit = new[] { "poncho", "pants", "boots" } },
            new Member { key = "ivo", name = "DR. IVO RUSK", title = "REMNANT ARCHIVIST", anchor = null, temper = Temper.Nervous, outfit = new[] { "coat", "pants", "boots", "goggles" } },
        };

        /// <summary>Everyone: the core cast plus the members the quests add (Cast_&lt;id&gt; hooks).</summary>
        public static readonly List<Member> All = MakeAll();
        static List<Member> MakeAll() { var l = new List<Member>(Core); CastExtra(l); return l; }

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
            p.look.body = MadMax.Game.HDHuman.BodyFor(mv.female, p.look.build);                          // HD body shape
            p.outfit.Clear(); p.outfit.AddRange(mv.outfit);
            p.tool = mv.tool;
            made[key] = p;
            return p;
        }

        /// <summary>The cast key of a spawned NPC (null for everyone else).</summary>
        public static string KeyOf(MadMax.Npc.Npc n) => n && n.Profile != null && n.Profile.id.StartsWith("cast:") ? n.Profile.id.Substring(5) : null;
    }
}
