using System.Collections;
using System.Linq;
using MadMax.Items;
using MadMax.Npc;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Faction standing (roadmap 21): a new world starts neutral with everyone; a deed shifts one faction and
    /// ripples a third to its friends and the other way to its enemies; reaching TRUSTED brings one gift, never two;
    /// standing survives save/load; ground belongs to a town's faction or the gang riding the road; a vendor's prices
    /// follow its faction's standing, and a hostile faction's vendors refuse to trade on either key.</summary>
    class PeopleFactions : Scenario
    {
        public override string Id => "people.factions";
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            var r0 = PH.Reps();
            c.Check(r0.All(v => v == 0), "a new world starts neutral with every faction: " + string.Join(",", r0));

            // ---- a deed and its ripple
            int chits = g.Inventory.GetItem(Contracts.Chit), fuel = g.Inventory.Get(ResourceType.Fuel);
            Factions.Shift(Faction.FuelGuild, 30);
            var r1 = PH.Reps();
            bool ripple = true;
            for (int i = 0; i < Factions.Count; i++)
            {
                var f = (Faction)i;
                int want = f == Faction.FuelGuild ? r0[i] + 30 : r0[i] + Factions.Relation(Faction.FuelGuild, f) * 10;
                if (r1[i] != want) { ripple = false; c.Note($"{Factions.Names[i]}: {r0[i]} -> {r1[i]}, expected {want}"); }
            }
            c.Check(ripple, "Guild +30: settlers (friends) +10, Church and the gangs (enemies) -10, Nomads and Remnants unmoved");
            c.Check(Factions.Standing(Faction.FuelGuild) == "TRUSTED" && Factions.Friendly(Faction.FuelGuild) && Factions.Standing(Faction.Church) == "DISTRUSTED", $"ranks: Guild {Factions.Standing(Faction.FuelGuild)}, Church {Factions.Standing(Faction.Church)}");
            c.Check(g.Inventory.GetItem(Contracts.Chit) == chits + 4 && g.Inventory.Get(ResourceType.Fuel) == fuel + 40, "TRUSTED by the Guild: a gift of 4 chits and 40 L of fuel");
            Factions.Shift(Faction.FuelGuild, -8); Factions.Shift(Faction.FuelGuild, 8);
            c.Check(g.Inventory.GetItem(Contracts.Chit) == chits + 4, "falling back and rising again brings no second gift");

            // ---- save and load
            var saved = Factions.Save();
            var before = PH.Reps();
            Factions.Load(null);
            c.Check(PH.Reps().All(v => v == 0 || v == NpcRegistry.Reputation), "cleared");
            Factions.Load(saved);
            var after = PH.Reps();
            after[0] = before[0];                                                                  // the settlers live in the NPC registry (saved with it)
            c.Check(after.SequenceEqual(before), "standing survives a save and load: " + string.Join(",", after));
            Factions.Shift(Faction.FuelGuild, -15); Factions.Shift(Faction.FuelGuild, 15);
            c.Check(g.Inventory.GetItem(Contracts.Chit) == chits + 4, "the gift is remembered across the load");

            // ---- territory
            var st = PH.HomeTown(g, true) ?? PH.HomeTown(g, false);
            if (!c.Check(st != null, "a settlement")) yield break;
            c.Check(Factions.TerritoryAt(PH.Centre(st)) == Factions.OfSettlement(st), $"{Market.TownName(st)} is {Factions.Names[(int)Factions.OfSettlement(st)]} land");
            var gang = NpcDirector.Instance ? NpcDirector.Instance.Convoys.FirstOrDefault(k => k.raiders && k.phase != Convoy.Phase.Gone && g.World.SettlementAt(k.Position.x, k.Position.z) == null) : null;
            if (gang != null) c.Check(Factions.TerritoryAt(gang.Position) == Factions.OfGang(gang.Gang), $"the road the {gang.Gang} ride is theirs");
            else c.Note("no raider gang on an open road to test territory");

            // ---- prices follow standing
            yield return PH.ToTown(c, st);
            var centre = PH.Centre(st);
            float reach = st.radius + 40f;
            yield return PH.Until(() => PH.Nearest(centre, reach, n => n.Profile.Vendor && n.Available && !n.Hostile && !n.Closed && Factions.Of(n) != Faction.None) != null, 25f);
            var vendor = PH.Nearest(centre, reach, n => n.Profile.Vendor && n.Available && !n.Hostile && !n.Closed && Factions.Of(n) != Faction.None);
            if (!c.Check(vendor, "a town vendor")) yield break;
            var side = Factions.Of(vendor);
            yield return PH.Face(c, vendor, vendor.Profile.role == NpcRole.Stallkeeper ? 2.4f : 1.6f);
            int Price()
            {
                vendor.Use(g, true);                                                                // the page sets the market and the seller
                var o = Trade.Stock(vendor.Profile, vendor.State, Trade.Bargain(g, vendor.State)).OrderByDescending(k => Trade.Value(k.id)).FirstOrDefault();
                g.Menus.Close();
                return o.id == null ? -1 : o.price;
            }
            Factions.Load(null);
            if (Factions.Rep(side) != 0) Factions.Shift(side, -Factions.Rep(side), false);         // the settlers' standing is the registry's reputation
            c.Fixture("standing reset to neutral for the price comparison");
            int neutral = Price();
            if (!c.Check(neutral > 0, "the vendor has something to sell")) yield break;
            Factions.Shift(side, 40 - Factions.Rep(side), false);
            int allied = Price();
            Factions.Shift(side, -25 - Factions.Rep(side), false);
            int distrusted = Price();
            c.Metric("price_neutral", neutral, "scrap"); c.Metric("price_trusted", allied, "scrap"); c.Metric("price_distrusted", distrusted, "scrap");
            c.Check(allied <= neutral && distrusted >= neutral && (allied < neutral || distrusted > neutral), $"{Factions.Names[(int)side]}: trusted {allied} < neutral {neutral} < distrusted {distrusted}");

            Factions.Shift(side, -60 - Factions.Rep(side), false);
            c.Check(Factions.Hostile(side), Factions.Names[(int)side] + " now " + Factions.Standing(side));
            vendor.Use(g, true);
            c.Check(g.Menus.Current != MenuSystem.Page.Trade, "[T]: a hostile faction's vendor won't open the trade page");
            if (g.Menus.IsOpen) g.Menus.Close();
            PH.Talk(g, vendor);
            if (g.Menus.Labels().Any(r => r.StartsWith("(POLITE)"))) g.Menus.Pick("(POLITE)");
            if (g.Menus.Labels().Contains("SHOW ME WHAT YOU'VE GOT."))
            {
                g.Menus.Pick("SHOW ME WHAT YOU'VE GOT.");
                c.Check(g.Menus.Current == MenuSystem.Page.Talk && PH.Line(g).Contains("DON'T TRADE WITH YOU"), "asked in words: " + PH.Line(g));
            }
            else c.Note("no hub (" + PH.Line(g) + ")");
            if (g.Menus.IsOpen) g.Menus.Close();
        }
    }
}
