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

        /// <summary>Authored story characters (<see cref="MadMax.Story.StoryCast"/>): their own full name, title and voice.</summary>
        public string fullName, title;
        public bool female;
        public bool Cast => fullName != null;

        public string Name => fullName ?? (role == NpcRole.Raider || role == NpcRole.RaiderBoss ? nick : first + " '" + nick + "'");
        public string Title => title ?? role switch
        {
            NpcRole.Shopkeeper or NpcRole.Stallkeeper or NpcRole.Trader or NpcRole.Packer => NpcLore.TradeName(kind),
            NpcRole.Leader => "TOWN BOSS",
            NpcRole.Raider => gang, NpcRole.RaiderBoss => gang + " BOSS", NpcRole.Resident => "LOCAL",
            NpcRole.Wanderer when kind != null => NpcLore.TradeName(kind), _ => "WANDERER"
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
            p.look.body = MadMax.Game.HDHuman.BodyFor(NpcLore.Feminine(p.first) && !p.Raider, p.look.build);   // HD body shape
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
                p.outfit.Add(Pick("coat", "duster")); p.outfit.Add("jeans"); p.outfit.Add(Pick("cowboy", "sunhat", "beanie"));
                p.tool = r.NextDouble() < 0.6 ? "tool_revolver" : null;
            }
            else if (role == NpcRole.Packer)
            {
                p.outfit.Add(Pick("poncho", "duster")); p.outfit.Add("pants"); p.outfit.Add("sunhat"); p.outfit.Add(Pick("hikingpack", "framepack"));
            }
            else
            {
                p.outfit.Add(Pick("tshirt", "tank", "hoodie", "tshirt")); p.outfit.Add(Pick("pants", "jeans", "shorts", "jeans", "patched_cargo"));
                if (r.NextDouble() < 0.4) p.outfit.Add(Pick("jacket", "coat", "vest", "work_jacket"));
                if (r.NextDouble() < 0.4) p.outfit.Add(Pick("sunhat", "beanie", "scarf", "goggles"));
                if (role == NpcRole.Wanderer && r.NextDouble() < 0.35) p.tool = Pick("tool_pipe", "tool_machete", "tool_axe");
            }
            p.outfit.RemoveAll(o => o == null);
            // 2026-10-12 visual pass: outfits by trade and past, hair and wear come from a second stream, so names,
            // tempers, tools and raider armour stay exactly what the first one always gave (the civilian picks above
            // only keep its draws in step; NpcLooks replaces those outfits)
            var v = new System.Random(unchecked(seed * 486187739 + 0x2545F491));
            NpcLooks.Dress(p, v);
            return p;
        }

        /// <summary>How worn the clothes are (0 rags .. 1 new), per profile; each garment varies around it.</summary>
        public float wear = 1f;

        /// <summary>Garment condition for <see cref="HumanRig.condition"/>: the profile's wear ± a stable per-garment
        /// jitter (worn HD variants below 0.6, voxel holes below 0.35).</summary>
        public float Condition(string defId)
        {
            if (wear >= 0.999f || defId == null) return 1f;
            uint h = 2166136261u ^ (uint)seed;
            foreach (char ch in defId) h = (h ^ ch) * 16777619u;
            float j = ((h >> 8) & 0xffff) / 65535f * 0.3f - 0.15f;
            return Mathf.Clamp(wear + j, 0.25f, 1f);
        }

        public string Backstory(int layer) => layer switch
        {
            0 => Raider ? NpcLore.RaiderStory[origin % NpcLore.RaiderStory.Length] : NpcLore.Origin[origin],
            1 => Raider ? "I RIDE WITH THE " + gang + ". NOTHING ELSE MATTERS." : NpcLore.Drive[drive],
            _ => NpcLore.Secret[secret]
        };
    }

    /// <summary>What people wear (visual pass 2026-10-12): an outfit by trade, role and past (<see cref="NpcLore.Origin"/>,
    /// <see cref="NpcLore.Secret"/>), some undercuts, and how worn it is. Every garment of <see cref="ClothingLibrary"/> turns
    /// up on someone; one garment per slot (no doubles). Raiders keep the armour the profile rolled; only the cosmetic
    /// pieces around it change. Deterministic from the stream it is handed.</summary>
    public static class NpcLooks
    {
        public enum Kind { Townsfolk, Mechanic, Farmer, Cook, Medic, Trader, Packer, Guard, Salvager, Scavenger, Drifter, Bunker, Driver, Singer, Escapee, Radio, Preacher, Boss }

        public static void Dress(NpcProfile p, System.Random v)
        {
            double D() => v.NextDouble();
            string Pick(params string[] o) => o[v.Next(o.Length)];
            if (p.look.hair != HairStyle.Bald && D() < (p.Raider ? 0.15 : 0.12)) p.look.hair = HairStyle.Undercut;

            if (p.Raider)
            {
                Raider(p, v);
                p.wear = 0.4f + (float)D() * 0.4f;
                Tidy(p.outfit);
                return;
            }

            var k = KindOf(p, v);
            var o = new List<string>();
            void W(string id)
            {
                if (id == null) return;
                var d = ClothingLibrary.Get(id);
                if (d == null) return;
                o.RemoveAll(x => ClothingLibrary.Get(x)?.slot == d.slot);
                o.Add(id);
            }
            void Maybe(double chance, string id) { if (D() < chance) W(id); }
            bool armed = p.tool != null;
            bool handFree = !armed && p.role != NpcRole.Wanderer;                                  // wanderers carry a torch at night

            switch (k)
            {
                case Kind.Mechanic:
                    W(Pick("tshirt", "tank", "tshirt")); W(D() < 0.7 ? "overalls" : Pick("patched_cargo", "pants"));
                    Maybe(0.5, "work_jacket"); Maybe(0.8, "toolbelt"); Maybe(0.5, "beanie");
                    W(D() < 0.5 ? "gloves" : D() < 0.5 ? "fingerless" : null);
                    if (D() < 0.08) W("welding_mask"); else Maybe(0.15, "goggles");
                    W(Pick("boots", "combat_boots", "boots"));
                    break;
                case Kind.Farmer:
                    W(Pick("cowboy", "sunhat", "cowboy")); Maybe(0.06, "bee_veil");
                    W(Pick("tshirt", "tank", "sweater")); W(Pick("overalls", "patched_cargo", "jeans"));
                    Maybe(0.7, "work_belt"); Maybe(0.15, "leather_chaps"); Maybe(0.15, "scarf"); Maybe(0.2, "work_jacket");
                    W(Pick("leather_boots", "boots"));
                    break;
                case Kind.Cook:
                    W(Pick("tank", "tshirt")); W(Pick("jeans", "pants", "shorts")); Maybe(0.5, "fanny_bag"); Maybe(0.3, "beanie");
                    Maybe(0.15, "scarf"); W(Pick("boots", "boots", null));
                    break;
                case Kind.Medic:
                    W(Pick("sweater", "tshirt", "hoodie")); Maybe(0.55, "coat"); W(Pick("pants", "jeans"));
                    Maybe(0.7, "shoulder_bag"); Maybe(0.5, "gloves"); Maybe(0.2, "scarf"); W(Pick("boots", "leather_boots"));
                    break;
                case Kind.Trader:
                case Kind.Packer:
                    W(Pick("tshirt", "sweater", "tshirt")); W(Pick("pants", "jeans", "patched_cargo"));
                    W(k == Kind.Packer ? Pick("poncho", "duster") : Pick("poncho", "duster", "poncho", "jacket"));
                    W(k == Kind.Packer ? Pick("framepack", "framepack", "hikingpack") : Pick("hikingpack", "framepack", "leather_satchel", "backpack"));
                    W(Pick("sunhat", "cowboy", null));
                    { double f = D(); W(f < 0.3 ? "shemagh" : f < 0.45 ? "scarf" : f < 0.6 ? "goggles" : null); }
                    { double b = D(); W(b < 0.25 ? "fanny_bag" : b < 0.4 ? "gun_belt" : null); }
                    if (handFree) Maybe(0.15, "duffel");
                    W(Pick("leather_boots", "boots"));
                    break;
                case Kind.Guard:
                    W(Pick("tshirt", "tank")); W(D() < 0.7 ? "bomber" : "jacket"); W(Pick("pants", "patched_cargo"));
                    if (armed) Maybe(0.35, "vest_kevlar");
                    Maybe(0.5, "milpack"); W("combat_boots");
                    { double h = D(); W(h < 0.3 ? "beanie" : h < 0.45 ? "moto_helmet" : null); }
                    Maybe(0.4, "fingerless"); if (armed) Maybe(0.25, "gun_belt");
                    break;
                case Kind.Salvager:
                    if (D() < 0.12) { W("dive_suit"); W("air_tank"); Maybe(0.4, "dive_helmet"); }
                    else { W(Pick("tshirt", "hoodie")); W(Pick("vest", "work_jacket")); W(Pick("patched_cargo", "pants")); Maybe(0.15, "gasmask"); Maybe(0.3, "craftpack"); }
                    Maybe(0.6, "sling_bag"); Maybe(0.6, "gloves"); W(Pick("boots", "combat_boots"));
                    break;
                case Kind.Scavenger:
                    W(Pick("hoodie", "tshirt")); Maybe(0.5, "vest"); W(Pick("patched_cargo", "pants"));
                    W(Pick("craftpack", "backpack", "craftpack")); Maybe(0.5, "fingerless");
                    { double f = D(); W(f < 0.25 ? "gasmask" : f < 0.45 ? "bandana" : null); }
                    Maybe(0.08, "hazmat"); W(Pick("boots", "boots", null));
                    break;
                case Kind.Drifter:
                    W(Pick("tshirt", "tank")); W(Pick("duster", "poncho")); W(Pick("pants", "patched_cargo"));
                    { double f = D(); W(f < 0.5 ? "shemagh" : f < 0.7 ? "scarf" : "goggles"); }
                    W(Pick("sunhat", "cowboy", null)); W(Pick("canvas_satchel", "backpack", "sling_bag"));
                    W(Pick("leather_boots", "boots"));
                    break;
                case Kind.Bunker:
                    W(Pick("hoodie", "sweater")); W(Pick("jeans", "pants"));
                    { double c = D(); W(c < 0.12 ? "hazmat" : c < 0.42 ? "coat" : null); }
                    Maybe(0.35, "gasmask"); W(D() < 0.3 ? "schoolbag" : D() < 0.4 ? "backpack" : null);
                    W(Pick("boots", "combat_boots"));
                    break;
                case Kind.Driver:
                    W("tshirt"); { double c = D(); W(c < 0.4 ? "bomber" : c < 0.7 ? "jacket" : null); }
                    W("jeans"); Maybe(0.3, "beanie"); Maybe(0.25, "schoolbag"); if (handFree) Maybe(0.2, "suitcase");
                    W("boots");
                    break;
                case Kind.Singer:
                    W(Pick("tank", "tshirt")); Maybe(0.7, "jacket"); W("jeans"); Maybe(0.6, "fingerless"); Maybe(0.35, "bandana");
                    Maybe(0.3, "sling_bag"); W(Pick("boots", "leather_boots"));
                    break;
                case Kind.Escapee:
                    W(Pick("tank", "tshirt")); W(Pick("patched_cargo", "shorts")); Maybe(0.5, "craftpack"); Maybe(0.2, "scarf");
                    W(Pick("boots", "boots", null));
                    break;
                case Kind.Radio:
                    W(Pick("sweater", "hoodie")); { double c = D(); W(c < 0.25 ? "work_jacket" : c < 0.4 ? "coat" : null); }
                    W("pants"); Maybe(0.5, "shoulder_bag"); Maybe(0.4, "beanie"); Maybe(0.3, "gloves"); W("boots");
                    break;
                case Kind.Preacher:
                    W(Pick("sweater", "tshirt")); W(Pick("duster", "coat")); W(Pick("jeans", "pants")); Maybe(0.4, "cowboy");
                    Maybe(0.3, "canvas_satchel"); W("leather_boots");
                    break;
                case Kind.Boss:
                    W(Pick("sweater", "tshirt")); W(Pick("coat", "duster", "bomber")); W("jeans"); W(Pick("cowboy", "sunhat", "beanie"));
                    if (armed) Maybe(0.6, "gun_belt"); else Maybe(0.3, "work_belt");
                    Maybe(0.2, "leather_cuirass"); W(Pick("leather_boots", "boots"));
                    break;
                default:
                    W(Pick("tshirt", "tank", "hoodie", "tshirt", "sweater")); W(Pick("pants", "jeans", "shorts", "jeans", "patched_cargo"));
                    if (D() < 0.4) W(Pick("jacket", "coat", "vest", "work_jacket", "bomber"));
                    if (D() < 0.4) W(Pick("sunhat", "beanie", "scarf", "goggles", "cowboy"));
                    { double b = D(); W(b < 0.15 ? "backpack" : b < 0.25 ? "canvas_satchel" : b < 0.3 ? "fanny_bag" : null); }
                    W(Pick("boots", "boots", "leather_boots", null));
                    break;
            }
            p.outfit.Clear();
            p.outfit.AddRange(o);
            Tidy(p.outfit);

            // townsfolk keep their things mended; road people and the scavenging kind wear them out
            bool town = p.role == NpcRole.Resident || p.role == NpcRole.Shopkeeper || p.role == NpcRole.Stallkeeper || p.role == NpcRole.Leader;
            p.wear = town ? 0.7f + (float)D() * 0.3f
                : k == Kind.Scavenger || k == Kind.Escapee ? 0.4f + (float)D() * 0.3f
                : p.role == NpcRole.Wanderer ? 0.4f + (float)D() * 0.4f
                : 0.55f + (float)D() * 0.35f;
        }

        /// <summary>Raiders: the profile's armour stays; jackets, masks, gloves, boots, belts and packs vary around it.</summary>
        static void Raider(NpcProfile p, System.Random v)
        {
            var o = p.outfit;
            double D() => v.NextDouble();
            void Swap(string from, string to) { int i = o.IndexOf(from); if (i >= 0) o[i] = to; }
            bool Has(ClothingSlot s) { foreach (var id in o) if (ClothingLibrary.Get(id)?.slot == s) return true; return false; }
            bool boss = p.role == NpcRole.RaiderBoss;
            if (D() < 0.25) Swap("jacket", "bomber"); else if (D() < 0.15) Swap("vest", "duster");
            { double f = D(); string face = f < 0.2 ? "skull_mask" : f < 0.25 ? "welding_mask" : f < 0.33 ? "gasmask" : null; if (face != null) { Swap("goggles", face); Swap("bandana", face); } }
            { double h = D(); if (boss ? h < 0.4 : h < 0.15) Swap("gloves", "gauntlets"); else if (h < 0.45) Swap("gloves", "fingerless"); }
            if (D() < 0.25) Swap("helmet", "moto_helmet");
            if (D() < 0.25) Swap("vest_scrap", "vest_chitin");
            if (!Has(ClothingSlot.Vest) && D() < 0.2) o.Add("leather_cuirass");
            if (!Has(ClothingSlot.Shins) && D() < 0.15) o.Add("leather_chaps");
            { double b = D(); if (b < 0.35) Swap("boots", "combat_boots"); else if (b < 0.5) Swap("boots", "leather_boots"); }
            if (!Has(ClothingSlot.Torso) && D() < 0.3) o.Add("tank");
            { double b = D(); if (b < 0.15 && p.tool == "tool_pipe_shotgun") o.Add("gun_belt"); else if (b < 0.2) o.Add("toolbelt"); }
            { double b = D(); if (b < 0.15) o.Add("milpack"); else if (b < 0.25) o.Add("sling_bag"); }
        }

        /// <summary>One garment per slot (the last one wins) and no id twice.</summary>
        static void Tidy(List<string> o)
        {
            var slots = new HashSet<ClothingSlot>();
            for (int i = o.Count - 1; i >= 0; i--)
            {
                var d = o[i] != null ? ClothingLibrary.Get(o[i]) : null;
                if (d == null || !slots.Add(d.slot)) o.RemoveAt(i);
            }
        }

        static bool Said(string[] lines, int i, string word) => i >= 0 && i < lines.Length && lines[i].Contains(word);

        /// <summary>The look a person gets: the trade they sell, the role they have, else the past they tell of.</summary>
        public static Kind KindOf(NpcProfile p, System.Random v)
        {
            double D() => v.NextDouble();
            switch (p.kind)
            {
                case "parts": return Kind.Mechanic;
                case "build": return D() < 0.6 ? Kind.Mechanic : Kind.Townsfolk;
                case "scrap": return D() < 0.5 ? Kind.Mechanic : Kind.Scavenger;
                case "food": return D() < 0.5 ? Kind.Cook : Kind.Farmer;
                case "fuel": return D() < 0.5 ? Kind.Guard : Kind.Trader;
                case "salvage": return Kind.Salvager;
                case "scavenger": return Kind.Scavenger;
                case "pack": return Kind.Packer;
            }
            if (p.role == NpcRole.Leader) return Kind.Boss;
            if (p.role == NpcRole.Packer) return Kind.Packer;
            if (p.role == NpcRole.Trader) return Kind.Trader;
            if (p.role == NpcRole.Wanderer && p.tool != null && D() < 0.45) return Kind.Guard;
            if (Said(NpcLore.Secret, p.secret, "MEDIC") && D() < 0.5) return Kind.Medic;
            if (Said(NpcLore.Secret, p.secret, "RAIDERS") && D() < 0.4) return Kind.Guard;
            var o = NpcLore.Origin; int i = p.origin;
            if (Said(o, i, "MECHANIC")) return Kind.Mechanic;
            if (Said(o, i, "FARMED")) return Kind.Farmer;
            if (Said(o, i, "NURSE")) return Kind.Medic;
            if (Said(o, i, "NOODLE")) return Kind.Cook;
            if (Said(o, i, "SALT") || Said(o, i, "HAULED WATER")) return Kind.Drifter;
            if (Said(o, i, "BUNKER")) return Kind.Bunker;
            if (Said(o, i, "SCHOOL BUS")) return Kind.Driver;
            if (Said(o, i, "SING")) return Kind.Singer;
            if (Said(o, i, "WORK CAMP")) return Kind.Escapee;
            if (Said(o, i, "RADIO")) return Kind.Radio;
            if (Said(o, i, "PREACHER")) return Kind.Preacher;
            return Kind.Townsfolk;
        }
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
        public int choreStamp, choreIndex, choreState; // seasonal chore (NpcLore.Chores): half-season stamp + 1, which chore, 1 accepted / 2 done
        public int haggleDay = -1, tradeDay = -1, tollDay = -1;
        public List<string> bought = new List<string>();
        public List<int> boughtN = new List<int>();
        public bool dead;
        public int lost;                              // severed limbs (Appearance.lost bits, Limbs)
        public int rescuedDay = -1;                   // world day the player brought them in off the road (hitchers remember)
        public int tended;                            // times this companion dressed the player's wounds (Companions.Knack)

        public const int Met = 1, Threatened = 2, Helped = 4, Hostile = 8, Parleyed = 16, Companion = 32, Surrendered = 64, StudiedWounds = 128;
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

        /// <summary>The state if this NPC has one yet (never creates it).</summary>
        public static NpcSave Peek(string id) => id != null && states.TryGetValue(id, out var s) ? s : null;

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
