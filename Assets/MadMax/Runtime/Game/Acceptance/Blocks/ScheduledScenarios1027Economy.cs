using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Scheduled update 2026-10-27 (economy remodel): the bounty board carries one poster per job really on
    /// offer there (taking a job tears its poster down, a payout stamps a PAID stub), and the player's stall shows what
    /// it stocks, its takings, and sells to a passer-by who walks over to the counter.</summary>
    public static class ScheduledScenarios1027Economy
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new BoardPosters();
            yield return new StallDisplay();
        }
    }

    /// <summary>At the home town's board: posters = today's offers there; taking one tears exactly that poster off and
    /// it stays off; paying a finished job puts the PAID stub up.</summary>
    class BoardPosters : Scenario
    {
        public override string Id => "economy.board_posters";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            var st = PH.HomeTown(g, false);
            if (!c.Check(st != null, "a settlement near the start")) yield break;
            yield return PH.ToTown(c, st);
            yield return PH.Until(() => BountyBoard.All.Any(b => b && b.town == st.index), 15f);
            var board = BountyBoard.All.FirstOrDefault(b => b && b.town == st.index);
            if (!c.Check(board, "the town has a bounty board")) yield break;
            var front = board.transform.forward; front.y = 0f; front.Normalize();
            yield return PH.Go(c, board.transform.position + front * 3f, PH.Yaw(-front), "the bounty board", 0.6f);
            board.Refresh();
            yield return null;
            var offers = Contracts.Offers(st);
            int town = Contracts.Active.Count(x => x.id.StartsWith("Q") && x.town == st.index && !x.completed && !x.failed);
            c.Check(offers.Count > 0 && board.Posters == offers.Count + town, $"one poster per job on offer ({board.Posters} posters, {offers.Count} offers + {town} town jobs)");
            c.Check(offers.All(o => board.HasPoster(o.id)), "every offer has its poster");
            c.Note("news sheet: " + (board.NewsSheet ? "up" : "no news"));
            yield return ScheduledScenarios1015.Shoot(c, g, board.transform.position + Vector3.up * 1.55f, front, 2.2f, "board_posters");
            // take a job: its poster comes down
            var job = offers.FirstOrDefault(o => o.kind == 2) ?? offers.FirstOrDefault(o => !o.Delivery && !o.Escort);
            if (!c.Check(job != null, "a job to take")) yield break;
            int before = board.Posters, torn = board.Torn;
            Contracts.Accept(g, job, board.transform.position);
            c.Check(board.Posters == before - 1 && board.Torn == torn + 1 && !board.HasPoster(job.id), $"taking \"{job.title}\" tears its poster off ({before} -> {board.Posters})");
            yield return new WaitForSeconds(0.7f);
            board.Refresh();
            c.Check(board.Posters == before - 1 && !board.HasPoster(job.id), "and it stays off the board");
            // done and paid: the stub
            job.done = job.need; job.completed = true;
            int scrap = PH.Scrap(g);
            Contracts.Pay(g, job);
            c.Check(PH.Scrap(g) > scrap && !Contracts.Active.Contains(job), "the job pays out");
            c.Check(board.Stamped, "a PAID stub goes up on the board");
            yield return ScheduledScenarios1015.Shoot(c, g, board.transform.position + Vector3.up * 1.3f, front, 2f, "board_paid");
        }
    }

    /// <summary>A stall built by the player: a bare counter, then the stocked goods on it; a sale brings a resident from
    /// nearby to the counter (the item leaves, the takings show in the cash box); nobody about, it sells unseen.</summary>
    class StallDisplay : Scenario
    {
        public override string Id => "economy.stall_display";
        public override float Timeout => 70f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            var me = g.Player.transform.position;
            var fwd = ScheduledScenarios1016.Fwd(g); var right = Vector3.Cross(Vector3.up, fwd);
            var piece = PH.Piece(g, "player_stall", me + fwd * 5f, -fwd);
            if (!c.Check(piece, "a market stall")) yield break;
            yield return null; yield return null;
            var stall = piece.GetComponent<PlayerStall>();
            var box = piece.GetComponent<Container>();
            if (!c.Check(stall && box, "the stall has its goods crates")) yield break;
            c.Check(stall.Shown == 0 && stall.CashShown == 0, "empty: a bare counter, an empty cash box");
            box.inventory.AddItem("food_egg", 2);
            box.inventory.AddItem("tool_torch", 1);
            box.inventory.Add(ResourceType.Wood, 4);
            c.Fixture("stocked the stall: 2 eggs, a torch, 4 wood");
            yield return null; yield return null;
            c.Check(stall.Shown == 3, $"the counter shows the three goods ({stall.Shown})");
            yield return ScheduledScenarios1015.Shoot(c, g, piece.transform.position + Vector3.up * 0.95f, -fwd, 2.4f, "stall_goods");
            // a customer
            var n = ScheduledScenarios1012.Person(g, "stall:buyer", NpcRole.Resident, 5150, me + fwd * 5f + right * 12f);
            n.home = n.transform.position;
            c.Fixture("a resident 12 m from the stall");
            yield return new WaitForSeconds(0.6f);
            int goods0 = Goods(box), sales0 = stall.Sales;
            stall.SellNow();
            var buyer = stall.Buyer;
            c.Check(buyer && buyer.Browsing, "a sale: someone nearby comes over" + (buyer ? " (" + buyer.Profile.Name + ")" : ""));
            if (!buyer) yield break;
            c.Check(Goods(box) == goods0, "nothing is sold before they get there");
            yield return PH.Until(() => buyer.BrowseSettled || stall.Sales > sales0, 25f);
            if (buyer.BrowseSettled) yield return ScheduledScenarios1015.Shoot(c, g, piece.transform.position + Vector3.up * 0.9f, right, 4f, "stall_customer");
            yield return PH.Until(() => stall.Sales > sales0, 15f);
            c.Check(stall.Sales == sales0 + 1 && !stall.LastSaleInstant, $"the sale lands at the counter ({stall.LastSold})");
            c.Check(stall.LastBuyerDistance < 2f, $"with the buyer standing at it ({stall.LastBuyerDistance:0.0} m)");
            c.Check(Goods(box) == goods0 - 1 && stall.cash > 0, $"one item gone, {stall.cash} scrap in the box");
            yield return null;
            c.Check(stall.CashShown > 0, "coins in the cash box");
            c.Check(!buyer.Browsing, "and the buyer walks on");
            // nobody about: unseen
            bool others = MadMax.Npc.Npc.All.Any(x => x && x != buyer && x.MayBrowse && PH.Flat(x.transform.position, piece.transform.position) < PlayerStall.BuyerReach);
            Object.Destroy(buyer.gameObject);
            yield return null;
            int sales1 = stall.Sales;
            stall.SellNow();
            if (others) c.Note("other people about: the unseen sale is not checked");
            else c.Check(stall.Sales == sales1 + 1 && stall.LastSaleInstant && stall.Buyer == null, "with nobody about the sale happens unseen");
            Object.Destroy(piece.gameObject);
        }

        static int Goods(Container box) => box.inventory.GetItem("food_egg") + box.inventory.GetItem("tool_torch") + box.inventory.Get(ResourceType.Wood);
    }
}
