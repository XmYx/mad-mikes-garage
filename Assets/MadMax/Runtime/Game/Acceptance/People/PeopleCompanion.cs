using System.Collections;
using System.Linq;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>A companion (roadmap 20) from a wanderer met on the start road, all through the talk page: hired for
    /// 80 scrap once they like you; they follow on foot, climb into the passenger seat and ride along, get out when you
    /// do, wait when told and follow again, take the wheel of a second car and drive it after you, fight a raider who
    /// comes at you, are saved with their orders, and walk off with their pack spilled when dismissed.</summary>
    class PeopleCompanion : Scenario
    {
        public override string Id => "people.companion";
        public override float Timeout => 180f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            PH.Daylight(c);
            PH.Attribute(c, g, Attr.Charisma, 10);
            if (!TestWorld.Pad(10f, 60f, out var pad, out var dir)) { c.Block("no open pad with a lane"); yield break; }
            yield return PH.Go(c, pad, PH.Yaw(dir), "an open stretch of the start road");
            var prof = NpcProfile.Make("test:companion", NpcRole.Wanderer, 424242);
            var mate = MadMax.Npc.Npc.Spawn(prof, PH.Ground(pad - dir * 2.5f), PH.Yaw(dir), null, g.propMaterial);
            c.Fixture("a wanderer, " + prof.Name + ", walking the road (spawned 2.5 m behind)");
            yield return new WaitForSeconds(0.5f);

            // ---- hired
            c.Check(Companions.CanAsk(mate) && Companions.Max(g) == 3, "wanderers can be asked along; CHA 10 leads a crew of three");
            if (!c.Check(PH.Talk(g, mate), "[E] talks to " + prof.Name)) yield break;
            if (g.Menus.Labels().Any(r => r.StartsWith("(POLITE)"))) g.Menus.Pick("(POLITE)");
            if (mate.State.disposition < 10) g.Menus.Pick("[CHA 9]");
            c.Note($"disposition {mate.State.disposition}; rows: {PH.Rows(g)}");
            if (!c.Check(g.Menus.Labels().Any(r => r.StartsWith("(" + Companions.HireScrap + " SCRAP)")), "the hire row is offered at disposition " + mate.State.disposition)) yield break;
            if (PH.Scrap(g) > 0) { g.Inventory.TrySpend(MadMax.Items.ResourceType.Scrap, PH.Scrap(g)); c.Fixture("scrap emptied from the pack"); }
            g.Menus.Pick("(" + Companions.HireScrap + " SCRAP)");
            c.Check(!mate.companion && PH.Line(g) == "COME BACK WHEN YOU CAN PAY.", "no scrap, no hand");
            PH.Grant(c, g, "res:" + (int)MadMax.Items.ResourceType.Scrap, Companions.HireScrap);
            g.Menus.Pick("(" + Companions.HireScrap + " SCRAP)");
            c.Check(mate.companion && Companions.Live.Contains(mate) && PH.Scrap(g) == 0 && mate.State.Has(NpcSave.Companion), "hired for " + Companions.HireScrap + " scrap");
            g.Menus.Pick("(LEAVE)");
            if (!mate.companion) yield break;

            // ---- follows on foot
            float t0 = Time.time;
            while (Time.time - t0 < 4f) { PH.Walk(g, dir, true); yield return null; }
            PH.Stop(g);
            float away = PH.Flat(mate.transform.position, g.Player.transform.position);
            c.Metric("left_behind", away, "m");
            yield return PH.Until(() => PH.Flat(mate.transform.position, g.Player.transform.position) < 4.5f, 20f);
            float near = PH.Flat(mate.transform.position, g.Player.transform.position);
            c.Check(away > 6f && near < 4.5f, $"follows on foot: {away:0.0} m behind after the run, {near:0.0} m after catching up");

            // ---- rides along
            var car = TestWorld.Vehicle("Sedan") ?? g.Fleet.FirstOrDefault(v => v && v.driveable && v.GetComponentInChildren<PassengerSeat>());
            var seat = car ? car.GetComponentInChildren<PassengerSeat>() : null;
            if (!c.Check(car && seat, "a fleet car with a passenger seat")) yield break;
            var here = g.Player.transform.position;
            yield return TestWorld.Place(c, car, here + dir * 6f, dir);
            g.Enter(car);
            yield return PH.Until(() => mate.Riding, 20f);
            c.Check(mate.Riding && seat.Occupant == mate, "climbs into the passenger seat" + (mate.Riding ? "" : $" (still {PH.Flat(mate.transform.position, seat.transform.position):0.0} m from the door)"));
            yield return TestWorld.StartEngine(c, car);
            car.handbrake = false;
            var p0 = car.transform.position;
            t0 = Time.time;
            while (Time.time - t0 < 5f) { car.throttleInput = 0.6f; car.steerInput = 0f; car.brakeInput = 0f; yield return null; }
            car.throttleInput = 0f; car.brakeInput = 1f;
            yield return PH.Until(() => Mathf.Abs(car.ForwardSpeed) < 0.5f, 6f);
            float drove = PH.Flat(car.transform.position, p0);
            c.Metric("rode_along", drove, "m");
            c.Check(drove > 8f && mate.Riding && PH.Flat(mate.transform.position, seat.transform.position) < 1.5f, $"rides along {drove:0} m in the seat");
            car.brakeInput = 0f; car.handbrake = true;
            g.Exit();
            yield return PH.Until(() => !mate.Riding, 5f);
            c.Check(!mate.Riding && seat.Occupant == null, "gets out when you do");

            // ---- orders: wait, then follow
            yield return new WaitForSeconds(0.5f);
            if (!c.Check(PH.Talk(g, mate) && PH.Line(g) == "RIGHT BEHIND YOU, BOSS.", "[E] asks a companion for orders: " + PH.Line(g))) yield break;
            g.Menus.Pick("WAIT HERE.");
            g.Menus.Pick("(LEAVE)");
            var post = mate.transform.position;
            c.Check(mate.order == 1, "WAIT HERE.");
            t0 = Time.time;
            while (Time.time - t0 < 3f) { PH.Walk(g, -dir, true); yield return null; }
            PH.Stop(g);
            yield return new WaitForSeconds(3f);
            c.Check(PH.Flat(mate.transform.position, post) < 2f && PH.Flat(mate.transform.position, g.Player.transform.position) > 8f, $"waits at the spot ({PH.Flat(mate.transform.position, post):0.0} m from it, {PH.Flat(mate.transform.position, g.Player.transform.position):0} m from you)");

            // ---- drives a second car behind you
            var second = g.Fleet.FirstOrDefault(v => v && v != car && v.driveable && !v.aiDriven && v.Engine && !v.GetComponent<BikeBalance>() && !v.GetComponent<FlightModel>() && !v.GetComponent<BoatModel>() && !v.GetComponent<Machine>() && v.GetComponentsInChildren<WheelStats>().Length >= 4);
            if (!c.Check(second, "a second fleet car")) yield break;
            yield return TestWorld.Place(c, second, post - dir * 6f, -dir);
            yield return PH.Face(c, mate, 1.6f);
            PH.Talk(g, mate);
            var wheel = g.Menus.Labels().FirstOrDefault(r => r.StartsWith("TAKE THE "));
            if (!c.Check(wheel != null, "offers to drive a spare car: " + PH.Rows(g))) yield break;
            g.Menus.Pick(wheel);
            var took = mate.DrivenCar;
            c.Check(mate.Driving && took && took.aiDriven && mate.order == 0, "takes the wheel of the " + (took ? WastelandGame.Name(took) : "-") + " and follows");
            g.Menus.Pick("(LEAVE)");
            if (!took) yield break;
            yield return new WaitForSeconds(1f);
            g.Enter(took);
            yield return PH.Until(() => !mate.Driving && !took.aiDriven, 3f);
            c.Check(!mate.Driving && !took.aiDriven && g.Current == took && took.Occupied, "getting in yourself takes it back (and you sit in it)");
            g.Exit();
            yield return new WaitForSeconds(0.8f);

            // ---- fights for you
            var me = g.Player.transform.position;
            var foe = PH.Raider(g, "test:companion:raider", 5150, me + dir * 12f, PH.Yaw(-dir), false, false);
            c.Fixture("a raider on foot comes at you (spawned 12 m ahead, unarmed, unarmoured)");
            float hp = foe.Health;
            yield return PH.Until(() => !foe || !foe.Alive || foe.Health < hp - 1f, 25f);
            c.Check(!foe || !foe.Alive || foe.Health < hp - 1f, $"the companion fights the raider ({(foe ? foe.Health : 0f):0}/{hp:0} health left)");
            if (foe && foe.Alive) foe.ApplyHit(foe.transform.position + Vector3.up, dir, 20f, 0.2f, g.Player.gameObject);

            // ---- saved, then dismissed
            var d = new SaveData();
            Companions.Save(g, d);
            c.Check(d.companions != null && d.companions.Count == 1 && d.companions[0].id == prof.id && d.companions[0].order == mate.order, "saved with their orders");
            mate.EnsurePack();
            mate.pack.inventory.AddItem("food_can", 2);
            c.Fixture("two cans in the companion's pack");
            yield return PH.Face(c, mate, 1.6f);
            PH.Talk(g, mate);
            var bye = g.Menus.Labels().FirstOrDefault(r => r.Contains("WE'RE DONE"));
            if (!c.Check(bye != null, "the parting row: " + PH.Rows(g))) yield break;
            g.Menus.Pick(bye);
            c.Check(!mate.companion && !Companions.Live.Contains(mate) && mate.leaving && !mate.State.Has(NpcSave.Companion), "dismissed: they go their own way");
            c.Check(mate.pack.inventory.GetItem("food_can") == 0, "and spill what they carried for you");
            if (g.Menus.IsOpen) g.Menus.Close();
        }
    }
}
