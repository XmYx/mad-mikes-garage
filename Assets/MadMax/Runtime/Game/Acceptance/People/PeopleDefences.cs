using System.Collections;
using System.Linq;
using MadMax.Building;
using MadMax.Npc;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Base defences against people: a claimed base with an alarm bell and a powered auto turret fed MG belts
    /// from its ammo box. A raider coming at the base rings the bell by itself; the turret picks the hostile, spends
    /// rounds and hits; then a small raid (<see cref="BaseRaid.Begin"/>) gathers out of sight, walks in to batter the
    /// pieces, and meets the turret.</summary>
    class PeopleDefences : Scenario
    {
        public override string Id => "people.defences";
        public override float Timeout => 180f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            if (!TestWorld.Pad(12f, 30f, out var pad, out var dir)) { c.Block("no open pad with a lane"); yield break; }
            var side = Vector3.Cross(Vector3.up, dir);
            yield return PH.Go(c, pad - dir * 6f, PH.Yaw(dir), "an open pad");

            var flag = PH.Piece(g, "claim_flag", pad, dir);
            var bell = PH.Piece(g, "alarm_bell", pad + side * 4f, dir);
            var tur = PH.Piece(g, "auto_turret", pad + dir * 4f, dir);
            var gen = PH.Piece(g, "coal_generator", pad - side * 5f, side);
            yield return null;
            var claim = flag ? flag.GetComponent<ClaimFlag>() : null;
            var alarm = bell ? bell.GetComponent<AlarmBell>() : null;
            var turret = tur ? tur.GetComponent<AutoTurret>() : null;
            var genComp = gen ? gen.GetComponent<Generator>() : null;
            if (!c.Check(claim && alarm && turret && genComp, "claim flag, alarm bell, auto turret and a generator stand")) yield break;
            genComp.fuel = 120f; genComp.on = true;
            tur.GetComponent<UtilityNode>().Link(gen.GetComponent<UtilityNode>(), UtilityKind.Power);
            var box = tur.GetComponent<Container>();
            box.inventory.AddItem("ammo_mg", 6);
            c.Fixture("built: claim flag, bell 4 m right, turret 4 m ahead cabled to a fuelled generator, 6 MG belts in its box");
            yield return PH.Until(() => turret.Prompt(g).Contains("WATCHING"), 4f);
            c.Check(turret.Prompt(g).Contains("WATCHING"), "the turret is armed and watching: " + turret.Prompt(g));
            c.Metric("claim_defence", claim.Defence(), "");
            c.Check(claim.Defence() >= 5, $"the claim counts its defences ({claim.Defence()}: turret and bell)");

            // ---- one raider comes at the base
            var r1 = PH.Raider(g, "test:def:1", 4040, pad + dir * 24f, PH.Yaw(-dir), false, false);
            c.Fixture("a raider spawned 24 m up the lane, coming at the base");
            float t0 = Time.time, hp = r1.Health;
            int belts0 = box.inventory.GetItem("ammo_mg") * AutoTurret.BeltRounds + turret.rounds;
            yield return PH.Until(() => alarm.LastRung >= t0, 4f);
            c.Check(alarm.LastRung >= t0, "the bell rings by itself");
            yield return PH.Until(() => !r1 || !r1.Alive || r1.Health < hp, 15f);
            int spent = belts0 - (box.inventory.GetItem("ammo_mg") * AutoTurret.BeltRounds + turret.rounds);
            c.Metric("rounds_on_one", spent, "");
            c.Check(spent > 0 && (!r1 || !r1.Alive || r1.Health < hp), $"the turret engages: {spent} rounds, raider at {(r1 ? Mathf.Max(0f, r1.Health) : 0f):0}/{hp:0}");
            c.Screenshot("turret");
            yield return null;
            if (r1 && r1.Alive) r1.ApplyHit(r1.transform.position + Vector3.up, dir, 20f, 0.2f, g.Player.gameObject);

            // ---- a small raid
            var raid = BaseRaid.Instance;
            if (!c.Check(raid, "raids are running")) yield break;
            for (int i = 0; i < 5; i++) PH.Piece(g, "wall_wood", pad - dir * 3f + side * (i * 2.2f - 4.4f), dir);
            c.Fixture("five wood walls behind the flag (a base worth raiding)");
            int before = MadMax.Npc.Npc.All.Count(n => n && n.raiding);
            raid.Begin(claim);
            c.Fixture("a raid called on the claim now (instead of on its night)");
            var party = MadMax.Npc.Npc.All.Where(n => n && n.raiding).ToList();
            c.Metric("raiders", party.Count - before, "");
            c.Check(raid.Live && party.Count > before && party.All(n => PH.Flat(n.transform.position, pad) > 60f), $"the party gathers out of sight ({party.Count - before} raiders, {party.Select(n => PH.Flat(n.transform.position, pad)).DefaultIfEmpty(0f).Min():0} m out)");
            float closest = float.MaxValue;
            belts0 = box.inventory.GetItem("ammo_mg") * AutoTurret.BeltRounds + turret.rounds;
            float ring = alarm.LastRung;
            t0 = Time.time;
            bool hurt = false;
            var hp0 = party.ToDictionary(n => n, n => n.Health);
            while (Time.time - t0 < 90f)
            {
                foreach (var n in party) if (n) { closest = Mathf.Min(closest, PH.Flat(n.transform.position, pad)); if (!n.Alive || n.Health < hp0[n]) hurt = true; }
                if (party.All(n => !n || !n.Alive)) break;
                yield return null;
            }
            spent = belts0 - (box.inventory.GetItem("ammo_mg") * AutoTurret.BeltRounds + turret.rounds);
            int dead = party.Count(n => !n || !n.Alive);
            c.Metric("raid_closest", closest, "m"); c.Metric("raid_rounds", spent, ""); c.Metric("raid_dead", dead, "");
            c.Note($"after {Time.time - t0:0} s: {dead}/{party.Count} down, raid live {raid.Live}, claim pieces {claim.Pieces()}");
            c.Check(closest < 30f, $"the raiders walk in on the base (closest {closest:0} m)");
            c.Note(alarm.LastRung > ring ? "the bell rang again for the raid" : "the bell was still in its 25 s quiet spell");
            c.Check(spent > 0 && hurt, $"the turret meets them ({spent} rounds, {dead} down)");
            foreach (var n in party) if (n && n.Alive) n.ApplyHit(n.transform.position + Vector3.up, dir, 20f, 0.2f, g.Player.gameObject);
            c.Screenshot("raid");
            yield return null;
        }
    }
}
