using System.Collections;
using System.Linq;
using MadMax.Animals;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>The bounty board of the town nearest the start, through its own rows: a pest job is taken once (a second
    /// press or a stale row takes nothing), the kills count, and the pay (scrap and settler standing) comes exactly once;
    /// the town faction's supply job is handed in and paid at the board; a haul puts its crates by the board, and when its
    /// deadline passes it fails and costs Fuel Guild standing.</summary>
    class PeopleContracts : Scenario
    {
        public override string Id => "people.contracts";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            var st = PH.HomeTown(g, false);
            if (st == null) { c.Block("no settlement in the world"); yield break; }
            yield return PH.ToTown(c, st);
            yield return PH.Until(() => BountyBoard.All.Any(b => b && b.town == st.index), 20f);
            var board = BountyBoard.All.FirstOrDefault(b => b && b.town == st.index);
            if (!c.Check(board, "the bounty board stands in " + Market.TownName(st))) yield break;
            var toCentre = PH.Centre(st) - board.transform.position; toCentre.y = 0f;
            var front = board.transform.position + (toCentre.sqrMagnitude > 0.01f ? toCentre.normalized : Vector3.forward) * 2f;
            yield return PH.Go(c, front, PH.Yaw(board.transform.position - front), "the bounty board");
            c.Check(board.Prompt(g).StartsWith("[E] BOUNTY BOARD"), "prompt: " + board.Prompt(g));

            var offers = Contracts.Offers(st);
            c.Metric("jobs_today", offers.Count, "");
            c.Note("today: " + string.Join(" | ", offers.Select(o => o.title + " (" + o.reward + ")")));
            board.Use(g, false);
            if (!c.Check(g.Menus.Current == MenuSystem.Page.Board, "[E] reads the board")) yield break;
            c.Check(offers.All(o => g.Menus.Labels().Contains(o.title)), "every job of the day is a row: " + PH.Rows(g));
            c.Screenshot("board");
            yield return null;

            // ---- a pest job: taken once
            var pest = offers.FirstOrDefault(o => o.kind == 2);
            if (!c.Check(pest != null, "a pest job on the board")) yield break;
            var stale = pest;
            g.Menus.Pick(pest.title);
            pest = Contracts.Active.FirstOrDefault(k => k.id == stale.id) ?? stale;                // the board's row is its own copy of the offer
            c.Check(Contracts.Active.Count(k => k.id == pest.id) == 1 && Journal.Entries.Last().kind == "JOB", "taken: " + pest.title);
            c.Check(!Contracts.Offers(st).Any(o => o.id == pest.id), "no longer on offer today");
            Contracts.Accept(g, stale, board.transform.position);                                  // a stale row pressed again
            c.Check(Contracts.Active.Count(k => k.id == pest.id) == 1, "a second press takes nothing");

            // ---- the kills
            var def = AnimalLibrary.Get(pest.target);
            if (!c.Check(def != null && def.pest == pest.target, "the pest is a species: " + pest.target)) yield break;
            var at = g.Player.transform.position;
            for (int i = 0; i < pest.need; i++)
            {
                var p = PH.Ground(at + Quaternion.Euler(0f, i * 40f, 0f) * Vector3.forward * 14f);
                var a = Animal.Spawn(def, p, 0f, g.propMaterial, "t:pest:" + i);
                a.ApplyHit(a.transform.position + Vector3.up * 0.4f, Vector3.forward, 20f, 0.2f, g.Player.gameObject);
                c.Check(!a.Alive, def.name + " " + (i + 1) + " is dead");
            }
            c.Fixture(pest.need + " " + def.name + " turned up by the town and were shot (hits from the player)");
            c.Check(pest.done == pest.need && pest.completed, $"the kills count: {pest.done}/{pest.need}");

            // ---- paid once
            int scrap = PH.Scrap(g), chits = g.Inventory.GetItem(Contracts.Chit), settlers = Factions.Rep(Faction.Settlers);
            board.Use(g, false);
            if (!c.Check(g.Menus.Pick(pest.title), "claim it at the board: " + PH.Rows(g))) yield break;
            c.Check(PH.Scrap(g) == scrap + pest.reward && g.Inventory.GetItem(Contracts.Chit) == chits + pest.chits, $"paid {pest.reward} scrap (got {PH.Scrap(g) - scrap})");
            c.Check(Factions.Rep(Faction.Settlers) == Mathf.Min(100, settlers + pest.rep), $"settler standing +{pest.rep} ({settlers} -> {Factions.Rep(Faction.Settlers)})");
            c.Check(!Contracts.Active.Contains(pest) && !g.Menus.Labels().Contains(pest.title), "the job leaves the board");
            scrap = PH.Scrap(g);
            Contracts.Pay(g, pest);                                                                   // a stale claim
            c.Check(PH.Scrap(g) == scrap, "claiming it again pays nothing");

            // ---- the town's supply job
            var sup = offers.FirstOrDefault(o => o.kind == 4);
            if (sup == null) c.Note("no supply job today (the town's faction is hostile)");
            else
            {
                var res = (ResourceType)int.Parse(sup.target.Substring(4));
                var side = (Faction)sup.faction;
                var supOffer = sup;
                g.Menus.Pick(sup.title);
                sup = Contracts.Active.FirstOrDefault(k => k.id == supOffer.id);
                if (!c.Check(sup != null, "supply job taken: " + supOffer.title)) yield break;
                g.Menus.Pick(sup.title);
                c.Check(!sup.completed && Contracts.Active.Contains(sup), "handing in with nothing to give does nothing");
                PH.Grant(c, g, sup.target, sup.need);
                int have = g.Inventory.Get(res), rep = Factions.Rep(side);
                scrap = PH.Scrap(g);
                g.Menus.Pick(sup.title);
                c.Check(g.Inventory.Get(res) == have - sup.need && PH.Scrap(g) == scrap + sup.reward, $"{sup.need} {ResourceInfo.Name(res)} handed over, {sup.reward} scrap paid");
                c.Check(Factions.Rep(side) == Mathf.Min(100, rep + sup.rep), $"{Factions.Names[(int)side]} standing +{sup.rep}");
                c.Check(!Contracts.Active.Contains(sup) && !Contracts.HandIn(g, sup), "and only once");
            }

            // ---- a haul that runs out of time
            var haul = offers.FirstOrDefault(o => o.kind == 3);
            if (!c.Check(haul != null, "a haul on the board")) yield break;
            var haulOffer = haul;
            g.Menus.Pick(haul.title);
            haul = Contracts.Active.FirstOrDefault(k => k.id == haulOffer.id);
            if (!c.Check(haul != null, "haul taken: " + haulOffer.title)) yield break;
            yield return new WaitForSeconds(0.5f);
            int crates = Object.FindObjectsByType<VehiclePart>(FindObjectsSortMode.None).Count(p => p && p.partId == "cargo_crate" && PH.Flat(p.transform.position, board.transform.position) < 5f);
            c.Check(Contracts.Active.Contains(haul) && haul.deadline == DayNight.Day + haul.days && crates == haul.need, $"haul taken: {crates}/{haul.need} crates by the board, due day {haul.deadline + 1}");
            c.Check(Contracts.Hauling, "cargo aboard draws raiders (hauling)");
            int guild = Factions.Rep(Faction.FuelGuild);
            DayNight.SetDay(haul.deadline + 1);
            c.Fixture("clock moved on past the haul's deadline (day " + (haul.deadline + 1) + ")");
            yield return PH.Until(() => haul.failed, 3f);
            c.Check(haul.failed && Factions.Rep(Faction.FuelGuild) == Mathf.Max(-100, guild - haul.rep), $"the haul failed and the Guild remembers ({guild} -> {Factions.Rep(Faction.FuelGuild)})");
            board.Use(g, false);
            g.Menus.Pick(haul.title);
            c.Check(!Contracts.Active.Contains(haul), "a failed job is struck off the board");
            g.Menus.Close();
        }
    }
}
