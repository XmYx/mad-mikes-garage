using MadMax.Game;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    public enum Faction { None = -1, Settlers, FuelGuild, Church, Nomads, Remnants, Jackals, Rustmen, BoneConvoy, AshRiders }

    /// <summary>The factions of the wastes (roadmap 21): the town settlers, the Fuel Guild (trader convoys, hauls,
    /// chits), the Church of the Last Engine, the Salt Nomads (pack traders, desert towns), the Bunker Remnants, and
    /// the four raider gangs. The player's standing with each runs -100..100 in ranks from HUNTED to ALLIED; deeds
    /// shift it and ripple to the faction's friends and enemies (<see cref="Shift"/>). Settlements belong to a faction,
    /// the roads between them to the gang that rides them (<see cref="TerritoryAt"/>). Standing pays: better prices
    /// with its traders, a gang's safe passage, a gift at TRUSTED, a guard at ALLIED — and hostility closes shops and
    /// sharpens raiders' eyes. The settlers' standing is the old reputation (<see cref="NpcRegistry.Reputation"/>).</summary>
    public static class Factions
    {
        public const int Count = 9;
        public static readonly string[] Names = { "TOWN SETTLERS", "FUEL GUILD", "CHURCH OF THE LAST ENGINE", "SALT NOMADS", "BUNKER REMNANTS", "CHROME JACKALS", "RUSTMEN", "BONE CONVOY", "ASH RIDERS" };
        public static readonly string[] Ranks = { "HUNTED", "HOSTILE", "DISTRUSTED", "NEUTRAL", "FRIENDLY", "TRUSTED", "ALLIED" };
        /// <summary>The faction's emblem in <see cref="MadMax.Vehicles.Decals"/> (0 = none).</summary>
        public static readonly int[] Decal = { 0, 5, 6, 7, 8, 1, 2, 3, 4 };

        static readonly int[] rep = new int[Count];
        static readonly bool[] gifted = new bool[Count], guarded = new bool[Count];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { for (int i = 0; i < Count; i++) { rep[i] = 0; gifted[i] = guarded[i] = false; } lastTerritory = Faction.None; }

        // who likes whom: +1 allies, -1 enemies (settlers, guild, church, nomads, remnants, jackals, rustmen, bone convoy, ash riders)
        static readonly int[,] Rel =
        {
            {  1,  1,  0,  0,  0, -1, -1, -1, -1 },
            {  1,  1, -1,  0,  0, -1, -1, -1, -1 },
            {  0, -1,  1,  0, -1,  0,  1,  0,  0 },
            {  0,  0,  0,  1,  0, -1,  0, -1, -1 },
            {  0,  0, -1,  0,  1, -1, -1, -1, -1 },
            { -1, -1,  0, -1, -1,  1, -1,  0, -1 },
            { -1, -1,  1,  0, -1, -1,  1, -1,  0 },
            { -1, -1,  0, -1, -1,  0, -1,  1, -1 },
            { -1, -1,  0, -1, -1, -1,  0, -1,  1 },
        };

        public static bool Gang(Faction f) => f >= Faction.Jackals;
        public static int Relation(Faction a, Faction b) => a == Faction.None || b == Faction.None ? 0 : Rel[(int)a, (int)b];

        public static int Rep(Faction f) => f == Faction.None ? 0 : f == Faction.Settlers ? NpcRegistry.Reputation : rep[(int)f];
        static void SetRep(Faction f, int v)
        {
            v = Mathf.Clamp(v, -100, 100);
            if (f == Faction.Settlers) NpcRegistry.Reputation = v; else if (f != Faction.None) rep[(int)f] = v;
        }

        public static int Rank(Faction f)
        {
            int r = Rep(f);
            return r <= -60 ? 0 : r <= -30 ? 1 : r <= -10 ? 2 : r < 10 ? 3 : r < 30 ? 4 : r < 60 ? 5 : 6;
        }

        public static bool Friendly(Faction f) => f != Faction.None && Rank(f) >= 4;
        public static bool Hostile(Faction f) => f != Faction.None && Rank(f) <= 1;
        public static string Standing(Faction f) => f == Faction.None ? "" : Ranks[Rank(f)];

        /// <summary>Traders of a faction price by standing: allies 15 % off, the hostile pay 25 % more.</summary>
        public static float PriceFactor(Faction f) => f == Faction.None ? 1f : Rank(f) switch { 0 => 1.4f, 1 => 1.25f, 2 => 1.1f, 4 => 0.95f, 5 => 0.9f, 6 => 0.85f, _ => 1f };

        /// <summary>A deed: standing with the faction moves by <paramref name="delta"/>; its friends move a third as much
        /// the same way, its enemies the other way.</summary>
        public static void Shift(Faction f, int delta, bool ripple = true)
        {
            if (f == Faction.None || delta == 0) return;
            var ranks = new int[Count];
            for (int i = 0; i < Count; i++) ranks[i] = Rank((Faction)i);
            SetRep(f, Rep(f) + delta);
            if (ripple)
                for (int o = 0; o < Count; o++)
                    if (o != (int)f && Rel[(int)f, o] != 0) SetRep((Faction)o, Rep((Faction)o) + Rel[(int)f, o] * delta / 3);
            var g = WastelandGame.Instance;
            for (int i = 0; i < Count; i++)
            {
                int now = Rank((Faction)i);
                if (now == ranks[i]) continue;
                g?.Toast("THE " + Names[i] + ": " + Ranks[now]);
                if (g && now >= 5 && !gifted[i]) Gift(g, (Faction)i);
                if (g && now >= 6 && !guarded[i]) Guard(g, (Faction)i);
            }
        }

        public static Faction OfGang(string gang)
        {
            int i = System.Array.IndexOf(NpcLore.Gangs, gang);
            return i < 0 ? Faction.None : Faction.Jackals + i;
        }

        /// <summary>Whose a settlement is: most towns belong to the settlers; some to the Church, desert towns often to
        /// the Nomads, cities often to the Remnants who crawled out of the bunkers (deterministic per world).</summary>
        public static Faction OfSettlement(Settlement st)
        {
            if (st == null) return Faction.None;
            var w = DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;
            var r = new System.Random(Market.Seed(st.index, 2101, w != null ? w.seed : 0));
            double x = r.NextDouble();
            bool desert = w != null && w.NaturalBiome(st.pos.x, st.pos.y) == Biome.Desert;
            if (st.kind == Biome.City) return x < 0.45 ? Faction.Remnants : Faction.Settlers;
            if (desert && x < 0.4) return Faction.Nomads;
            if (x > 0.8) return Faction.Church;
            return Faction.Settlers;
        }

        /// <summary>Whose ground this is: a settlement's faction, a raider gang's road, or no one's.</summary>
        public static Faction TerritoryAt(Vector3 p)
        {
            var w = DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;
            if (w == null) return Faction.None;
            var st = w.SettlementAt(p.x, p.z);
            if (st != null) return OfSettlement(st);
            var d = NpcDirector.Instance;
            if (!d) return Faction.None;
            var gang = d.NearestRaiders(p, out float dist);
            return gang != null && dist < 350f ? OfGang(gang.Gang) : Faction.None;
        }

        /// <summary>Which faction a person answers to.</summary>
        public static Faction Of(Npc n)
        {
            if (!n) return Faction.None;
            var p = n.Profile;
            switch (p.role)
            {
                case NpcRole.Raider: case NpcRole.RaiderBoss: return OfGang(p.gang);
                case NpcRole.Trader: return Faction.FuelGuild;
                case NpcRole.Packer: return Faction.Nomads;
                case NpcRole.Wanderer: return Faction.None;
                default:
                {
                    var w = DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;
                    return w != null ? OfSettlement(w.SettlementAt(n.home.x, n.home.z)) : Faction.Settlers;
                }
            }
        }

        // ------------------------------------------------------------------ rewards

        static void Gift(WastelandGame g, Faction f)
        {
            gifted[(int)f] = true;
            var inv = g.Inventory;
            string note;
            switch (f)
            {
                case Faction.Settlers: inv.Add(MadMax.Items.ResourceType.Scrap, 100); inv.AddItem("med_antibiotics", 2); note = "100 SCRAP AND MEDICINE"; break;
                case Faction.FuelGuild: inv.AddItem(Contracts.Chit, 4); inv.Add(MadMax.Items.ResourceType.Fuel, 40); note = "4 CHITS AND 40 L OF FUEL"; break;
                case Faction.Church: inv.AddItem("use_nitrous", 2); inv.Add(MadMax.Items.ResourceType.Oil, 10); note = "TWO NITROUS BOTTLES, BLESSED"; break;
                case Faction.Nomads: inv.AddItem("cloth_shemagh"); inv.AddItem("use_canteen"); inv.Add(MadMax.Items.ResourceType.Water, 20); note = "A SHEMAGH, A CANTEEN AND WATER"; break;
                case Faction.Remnants: inv.AddItem("tool_geiger"); inv.AddItem("cloth_hazmat"); note = "A GEIGER COUNTER AND A HAZMAT SUIT"; break;
                default: inv.AddItem("cloth_skull_mask"); inv.Add(MadMax.Items.ResourceType.Fuel, 20); note = "A SKULL MASK AND FUEL - RIDE SAFE ON THEIR ROADS"; break;
            }
            g.Toast("GIFT FROM THE " + Names[(int)f] + ": " + note);
            MadMax.Audio.Sfx.Play2D("cash", 0.7f);
        }

        /// <summary>Allied settlers, Guild or Remnants send a guard to ride with you (a companion, if there is room).</summary>
        static void Guard(WastelandGame g, Faction f)
        {
            if (f != Faction.Settlers && f != Faction.FuelGuild && f != Faction.Remnants) return;
            guarded[(int)f] = true;
            if (Companions.Full(g) || !g.Player) { g.Toast("THE " + Names[(int)f] + " OFFER YOU A GUARD - NO ROOM IN YOUR CREW"); return; }
            var t = DeformableTerrain.Instance;
            var pos = g.Player.transform.position - g.Player.transform.forward * 3f;
            if (t) pos.y = t.Height(pos.x, pos.z) + 0.1f;
            var p = NpcProfile.Make("guard:" + (int)f, NpcRole.Wanderer, 7919 * ((int)f + 3));
            p.tool = "tool_pipe_shotgun";
            p.outfit.Add("vest_scrap");
            var n = Npc.Spawn(p, pos, g.Player.transform.eulerAngles.y, null, g.propMaterial);
            Companions.Recruit(g, n);
            g.Toast("THE " + Names[(int)f] + " SEND " + p.Name + " TO RIDE WITH YOU");
        }

        // ------------------------------------------------------------------ territory notice

        static Faction lastTerritory = Faction.None;
        static float territoryT;

        /// <summary>Crossing into another faction's ground: a notice with your standing there.</summary>
        public static void Tick(WastelandGame g)
        {
            if ((territoryT -= Time.deltaTime) > 0f || !g.Player) return;
            territoryT = 2f;
            var at = g.Current ? g.Current.transform.position : g.Player.transform.position;
            var f = TerritoryAt(at);
            if (f == lastTerritory) return;
            lastTerritory = f;
            if (f != Faction.None) g.Toast((Gang(f) ? "THE " + Names[(int)f] + " RIDE THIS ROAD" : Names[(int)f] + " LAND") + " - YOU ARE " + Standing(f));
        }

        // ------------------------------------------------------------------ save

        public static string Save()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i < Count; i++) sb.Append(rep[i]).Append(',');
            for (int i = 0; i < Count; i++) sb.Append(gifted[i] ? '1' : '0').Append(guarded[i] ? '1' : '0');
            return sb.ToString();
        }

        public static void Load(string s)
        {
            for (int i = 0; i < Count; i++) { rep[i] = 0; gifted[i] = guarded[i] = false; }
            if (string.IsNullOrEmpty(s)) return;
            var parts = s.Split(',');
            for (int i = 1; i < Count && i - 1 < parts.Length; i++) int.TryParse(parts[i - 1], out rep[i]);
            if (parts.Length >= Count)
            {
                var flags = parts[Count - 1];
                for (int i = 0; i < Count && i * 2 + 1 < flags.Length; i++) { gifted[i] = flags[i * 2] == '1'; guarded[i] = flags[i * 2 + 1] == '1'; }
            }
        }
    }
}
