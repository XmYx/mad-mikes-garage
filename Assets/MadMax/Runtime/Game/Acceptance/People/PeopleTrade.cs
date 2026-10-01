using System.Collections;
using System.Linq;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Trading with a vendor in the town nearest the start through the trade page ([T]): the page trades in the
    /// town's market and prices by the vendor's faction; buying one pays exactly the shown price and counts against the
    /// day's stock until it runs out; selling pays less than buying (no arbitrage) and floods the market; price levels
    /// differ from town to town and the roadside charges a flat markup; charisma and a won haggle (once a day) lower the
    /// prices.</summary>
    class PeopleTrade : Scenario
    {
        public override string Id => "people.trade";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            PH.Attribute(c, g, Attr.Charisma, 5);
            System.Func<MadMax.Npc.Npc, bool> vendorOk = n => n.Profile.Vendor && !n.Profile.Cast && n.Available && !n.Hostile && !n.Closed && (n.Profile.role == NpcRole.Stallkeeper || n.Profile.role == NpcRole.Shopkeeper);
            var found = new PH.Found();
            yield return PH.FindIn(c, vendorOk, false, 4, found, n => n.Profile.role == NpcRole.Stallkeeper ? 1 : 0);
            var vendor = found.npc; var st = found.st;
            if (!c.Check(vendor, "a stallkeeper or shopkeeper open for business in one of the four towns nearest the start")) yield break;
            var P = vendor.Profile; var S = vendor.State;
            c.Note($"vendor {P.Name}: {P.Title} ({P.role}, {P.kind}, {P.temper})");
            yield return PH.Face(c, vendor, P.role == NpcRole.Stallkeeper ? 2.4f : 1.6f);
            if (PH.Scrap(g) < 400) PH.Grant(c, g, "res:" + (int)ResourceType.Scrap, 400 - PH.Scrap(g));

            // ---- the trade page
            vendor.Use(g, true);
            if (!c.Check(g.Menus.Current == MenuSystem.Page.Trade, "[T] opens the trade page")) yield break;
            var town = Market.Near(vendor.transform.position);
            c.Check(Trade.Town == town && Trade.Seller == Factions.Of(vendor), $"trading in {Market.TownName(Trade.Town)}'s market with the {(Trade.Seller == Faction.None ? "road" : Factions.Names[(int)Trade.Seller])}");
            float bargain = Trade.Bargain(g, S);
            var stock = Trade.Stock(P, S, bargain);
            c.Metric("offers", stock.Count, "");
            if (!c.Check(stock.Count > 0, "the vendor has goods today: " + PH.Rows(g))) yield break;
            c.Check(Trade.Stock(P, S, bargain).Select(o => o.id + o.count).SequenceEqual(stock.Select(o => o.id + o.count)), "today's stock is fixed for the day (asked twice, same goods)");

            // ---- buy one, then the rest
            var offer = stock.Where(o => !o.id.StartsWith("part:") && o.id != "res:" + (int)ResourceType.Scrap && o.price * o.count <= PH.Scrap(g)).OrderByDescending(o => o.count).FirstOrDefault();
            if (!c.Check(offer.id != null, "an affordable good that is not a part")) yield break;
            string label = Trade.Name(offer.id);
            int scrap = PH.Scrap(g), have = PH.Have(g, offer.id), disp = S.disposition;
            float f0 = Market.Factor(town, offer.id);
            if (!c.Check(g.Menus.Pick(label), "buy one " + label + ": " + PH.Rows(g))) yield break;
            c.Check(PH.Scrap(g) == scrap - offer.price && PH.Have(g, offer.id) == have + 1, $"paid exactly {offer.price} scrap for one {label} (paid {scrap - PH.Scrap(g)}, got {PH.Have(g, offer.id) - have})");
            int left = Trade.Stock(P, S, Trade.Bargain(g, S)).Where(o => o.id == offer.id).Select(o => o.count).FirstOrDefault();
            c.Check(left == offer.count - 1, $"the day's stock goes down ({offer.count} -> {left})");
            c.Check(S.disposition == Mathf.Min(100, disp + 1), "a paying customer is liked a little more");
            if (town != null) c.Check(Market.Factor(town, offer.id) > f0, $"buying drains the market: factor {f0:0.000} -> {Market.Factor(town, offer.id):0.000}");
            scrap = PH.Scrap(g); have = PH.Have(g, offer.id);
            for (int i = 0; i < 20 && Trade.Stock(P, S, Trade.Bargain(g, S)).Any(o => o.id == offer.id); i++) g.Menus.Adjust(label, 1);   // D: buy ten at a time (the buy rows come first)
            c.Check(PH.Have(g, offer.id) == have + left && !Trade.Stock(P, S, Trade.Bargain(g, S)).Any(o => o.id == offer.id), $"the rest of the stock bought ({PH.Have(g, offer.id) - have} of {left}) and no more on offer today");
            c.Metric("spent", scrap - PH.Scrap(g), "scrap");

            // ---- sell
            string R(ResourceType t) => "res:" + (int)t;
            string[] candidates = { "food_can", "drink_beer", "food_apple", "med_bandage", "misc_bone", "tool_pipe", "book_charm", R(ResourceType.Wood), R(ResourceType.Glass), R(ResourceType.Rubber),
                                    R(ResourceType.Cloth), R(ResourceType.IronOre), R(ResourceType.Rubble), R(ResourceType.Fuel) };
            // something they buy but don't sell today (a row of the same name would be the buy row)
            string sell = candidates.FirstOrDefault(id => Trade.Buys(P.kind, id) && !stock.Any(o => Trade.Name(o.id) == Trade.Name(id)) && Trade.SellPrice(id, bargain) > 0 && Trade.SellPrice(id, bargain) < Trade.BuyPrice(id, bargain));
            if (!c.Check(sell != null, "the vendor buys something we can carry")) yield break;
            PH.Grant(c, g, sell, 8);
            vendor.Use(g, true);                                                                   // the page again, with the new goods listed
            bargain = Trade.Bargain(g, S);
            int price = Trade.SellPrice(sell, bargain);
            string sellLabel = sell.StartsWith("res:") ? ResourceInfo.Name((ResourceType)int.Parse(sell.Substring(4))) : ItemCatalog.Name(sell);
            c.Check(price < Trade.BuyPrice(sell, bargain), $"{sellLabel}: sells for {price}, buys for {Trade.BuyPrice(sell, bargain)} (no arbitrage)");
            scrap = PH.Scrap(g); have = PH.Have(g, sell);
            float s0 = Market.Factor(town, sell);
            var sellRow = g.Menus.Labels().FirstOrDefault(r => r == sellLabel);
            if (!c.Check(sellRow != null && g.Menus.Pick(sellLabel), "sell one " + sellLabel + ": " + PH.Rows(g))) yield break;
            c.Check(PH.Scrap(g) == scrap + price && PH.Have(g, sell) == have - 1, $"sold one for {price} scrap (got {PH.Scrap(g) - scrap})");
            g.Menus.Adjust(sellLabel, 1);                                                          // D: sell all
            c.Check(PH.Have(g, sell) == 0, "the rest sold");
            if (town != null) c.Check(Market.Factor(town, sell) < s0, $"selling floods the market: factor {s0:0.000} -> {Market.Factor(town, sell):0.000}");
            g.Menus.Close();

            // ---- prices by town and on the road
            var factors = g.World.settlements.Take(12).Select(t => Market.Factor(t, "food_can")).ToList();
            c.Metric("food_price_spread", factors.Max() - factors.Min(), "");
            c.Check(factors.Distinct().Count() > 1, "food costs differ from town to town: " + string.Join(", ", factors.Select(f => f.ToString("0.00"))));
            c.Check(Mathf.Approximately(Market.Factor(null, "food_can"), 1.1f), "roadside vendors charge a flat 10 % markup");

            // ---- charisma and a haggle
            float b5 = Trade.Bargain(g, S);
            PH.Attribute(c, g, Attr.Charisma, 9);
            float b9 = Trade.Bargain(g, S);
            c.Check(b9 > b5 && Trade.BuyPrice("food_can", b9) <= Trade.BuyPrice("food_can", b5) && Trade.SellPrice("res:" + (int)ResourceType.Glass, b9) >= Trade.SellPrice("res:" + (int)ResourceType.Glass, b5),
                $"charisma helps: bargain {b5:0.000} -> {b9:0.000}");
            PH.Talk(g, vendor);
            if (g.Menus.Labels().Any(r => r.StartsWith("(POLITE)"))) g.Menus.Pick("(POLITE)");
            float before = Trade.Bargain(g, S); int d0 = S.disposition;
            if (!c.Check(g.Menus.Pick("[CHA "), "a vendor can be haggled with: " + PH.Rows(g))) yield break;
            bool won = S.disposition > d0;
            c.Note((won ? "haggle won: " : "haggle lost: ") + PH.Line(g));
            c.Check(S.haggleDay == MadMax.World.DayNight.Day && !g.Menus.Labels().Any(r => r.StartsWith("[CHA ")), "one haggle a day");
            if (won) c.Check(Trade.Bargain(g, S) > before && Trade.BuyPrice("food_can", Trade.Bargain(g, S)) <= Trade.BuyPrice("food_can", before), "a won haggle lowers today's prices");
            else c.Check(S.disposition < d0, "a failed haggle costs goodwill");
            g.Menus.Pick("GOODBYE.");
            if (g.Menus.IsOpen) g.Menus.Close();
        }
    }
}
