using System;
using System.Collections.Generic;
using MadMax.Game;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Who an NPC is, generated from a seed: name, role, temperament, trade, gang, backstory, looks and
    /// what they carry. The same id always gives the same person; what changes (disposition, errands, death) lives
    /// in <see cref="NpcSave"/>.</summary>
    public class NpcProfile
    {
        public string id, first, nick, kind, gang, tool;
        public NpcRole role;
        public Temper temper;
        /// <summary>Settlement a town boss runs (-1 = none).</summary>
        public int town = -1;
        public int seed, origin, drive, secret, job;
        public Appearance look;
        public List<string> outfit = new List<string>();

        public string Name => role == NpcRole.Raider || role == NpcRole.RaiderBoss ? nick : first + " '" + nick + "'";
        public string Title => role switch
        {
            NpcRole.Shopkeeper or NpcRole.Stallkeeper or NpcRole.Trader or NpcRole.Packer => NpcLore.TradeName(kind),
            NpcRole.Leader => "TOWN BOSS",
            NpcRole.Raider => gang, NpcRole.RaiderBoss => gang + " BOSS", NpcRole.Resident => "LOCAL", _ => "WANDERER"
        };
        public bool Vendor => kind != null;
        public bool Raider => role == NpcRole.Raider || role == NpcRole.RaiderBoss;

        public static NpcProfile Make(string id, NpcRole role, int seed, string kind = null)
        {
            var r = new System.Random(seed);
            var p = new NpcProfile { id = id, role = role, seed = seed, kind = kind };
            p.first = NpcLore.First[r.Next(NpcLore.First.Length)];
            p.nick = p.Raider ? NpcLore.RaiderNick[r.Next(NpcLore.RaiderNick.Length)] : NpcLore.Nick[r.Next(NpcLore.Nick.Length)];
            Temper[] pool = p.Raider ? new[] { Temper.Proud, Temper.Gruff, Temper.Joker, Temper.Greedy }
                : role == NpcRole.Shopkeeper || role == NpcRole.Stallkeeper || role == NpcRole.Trader ? new[] { Temper.Greedy, Temper.Friendly, Temper.Gruff, Temper.Proud, Temper.Joker }
                : (Temper[])Enum.GetValues(typeof(Temper));
            p.temper = pool[r.Next(pool.Length)];
            p.gang = NpcLore.Gangs[(seed & 0x7fffffff) % NpcLore.Gangs.Length];
            p.origin = r.Next(NpcLore.Origin.Length); p.drive = r.Next(NpcLore.Drive.Length); p.secret = r.Next(NpcLore.Secret.Length);
            p.job = r.Next(NpcLore.Jobs.Length);

            // looks: quantised so body meshes are shared between NPCs (HumanRig mesh cache)
            p.look = new Appearance
            {
                skinTone = r.Next(4), hair = (HairStyle)r.Next(6), hairColor = r.Next(HumanDesign.HairColors.Length), beard = r.Next(3),
                height = new[] { 0.94f, 1f, 1.06f }[r.Next(3)], build = new[] { 0.9f, 1f, 1.1f }[r.Next(3)]
            };
            if (NpcLore.Feminine(p.first) && !p.Raider) p.look.beard = 0;                          // matches the voice
            string Pick(params string[] o) => o[r.Next(o.Length)];
            p.outfit.Add(Pick("boots", "boots", null));
            if (p.Raider)
            {
                p.outfit.Add(Pick("jacket", "vest")); p.outfit.Add(Pick("pants", "jeans"));
                p.outfit.Add(Pick("goggles", "bandana", "goggles")); p.outfit.Add("gloves");
                if (r.NextDouble() < 0.6) p.outfit.Add("shoulder");
                if (r.NextDouble() < 0.5 || role == NpcRole.RaiderBoss) p.outfit.Add("helmet");
                if (p.look.hair == HairStyle.Long || p.look.hair == HairStyle.Ponytail) p.look.hair = HairStyle.Mohawk;
                p.tool = role == NpcRole.RaiderBoss ? "tool_pipe_shotgun" : Pick("tool_pipe_club", "tool_machete", "tool_pipe_shotgun", "tool_pipe");
                // welded and strapped-on armour (the boss wears the best); drops when they fall
                double ar = r.NextDouble();
                if (role == NpcRole.RaiderBoss) p.outfit.Add(ar < 0.4 ? "vest_kevlar" : "vest_scrap");
                else if (ar < 0.35) p.outfit.Add("vest_scrap");
                else if (ar < 0.6) p.outfit.Add("vest_tyre");
                if (r.NextDouble() < 0.35) p.outfit.Add("arm_guards");
                if (r.NextDouble() < 0.3) p.outfit.Add("shin_guards");
            }
            else if (role == NpcRole.Leader)
            {
                p.outfit.Add(Pick("coat", "duster")); p.outfit.Add("jeans"); p.outfit.Add("boots"); p.outfit.Add(Pick("cowboy", "sunhat", "beanie"));
                p.tool = r.NextDouble() < 0.6 ? "tool_revolver" : null;
            }
            else if (role == NpcRole.Packer)
            {
                p.outfit.Add(Pick("poncho", "duster")); p.outfit.Add("pants"); p.outfit.Add("boots"); p.outfit.Add("sunhat"); p.outfit.Add(Pick("hikingpack", "framepack"));
            }
            else
            {
                p.outfit.Add(Pick("tshirt", "tank", "hoodie", "tshirt")); p.outfit.Add(Pick("pants", "jeans", "shorts", "jeans"));
                if (r.NextDouble() < 0.4) p.outfit.Add(Pick("jacket", "coat", "vest"));
                if (r.NextDouble() < 0.4) p.outfit.Add(Pick("sunhat", "beanie", "scarf", "goggles"));
                if (role == NpcRole.Wanderer && r.NextDouble() < 0.35) p.tool = Pick("tool_pipe", "tool_machete", "tool_axe");
            }
            p.outfit.RemoveAll(o => o == null);
            return p;
        }

        public string Backstory(int layer) => layer switch
        {
            0 => Raider ? NpcLore.RaiderStory[origin % NpcLore.RaiderStory.Length] : NpcLore.Origin[origin],
            1 => Raider ? "I RIDE WITH THE " + gang + ". NOTHING ELSE MATTERS." : NpcLore.Drive[drive],
            _ => NpcLore.Secret[secret]
        };
    }

    /// <summary>What changes about an NPC over a game: how they feel about the player, what they told, the errand,
    /// what was bought from them today, whether they are dead. Saved in <see cref="SaveData.npcs"/>.</summary>
    [Serializable]
    public class NpcSave
    {
        public string id;
        public int disposition;
        public int flags;
        public int revealed;                          // backstory layers told (0..3)
        public int jobState;                          // 0 not offered, 1 accepted, 2 done
        public int haggleDay = -1, tradeDay = -1, tollDay = -1;
        public List<string> bought = new List<string>();
        public List<int> boughtN = new List<int>();
        public bool dead;

        public const int Met = 1, Threatened = 2, Helped = 4, Hostile = 8, Parleyed = 16, Companion = 32, Surrendered = 64;
        public bool Has(int f) => (flags & f) != 0;
        public void Set(int f, bool on = true) { if (on) flags |= f; else flags &= ~f; }

        public int Bought(string id, int day)
        {
            if (tradeDay != day) { bought.Clear(); boughtN.Clear(); tradeDay = day; }
            int i = bought.IndexOf(id);
            return i < 0 ? 0 : boughtN[i];
        }

        public void AddBought(string id, int n, int day)
        {
            Bought(id, day);
            int i = bought.IndexOf(id);
            if (i < 0) { bought.Add(id); boughtN.Add(n); } else boughtN[i] += n;
        }
    }

    /// <summary>All NPC states of this world plus the player's standing (reputation) with ordinary folk.</summary>
    public static class NpcRegistry
    {
        static readonly Dictionary<string, NpcSave> states = new Dictionary<string, NpcSave>();
        public static int Reputation;                 // -100..100: killing peaceful folk lowers it, helping raises it
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { states.Clear(); Reputation = 0; }

        public static NpcSave Get(NpcProfile p)
        {
            if (states.TryGetValue(p.id, out var s)) return s;
            s = new NpcSave { id = p.id, disposition = Mathf.Clamp(Reputation / 2 + (p.temper == Temper.Friendly ? 10 : p.temper == Temper.Gruff ? -5 : 0), -60, 60) };
            if (p.Raider) s.disposition = -30;
            states[p.id] = s;
            return s;
        }

        public static bool IsDead(string id) => states.TryGetValue(id, out var s) && s.dead;

        public static List<NpcSave> SaveAll() => new List<NpcSave>(states.Values);

        public static void Load(List<NpcSave> list, int reputation)
        {
            states.Clear();
            Reputation = reputation;
            if (list == null) return;
            foreach (var s in list) if (s != null && s.id != null) states[s.id] = s;
        }
    }
}
