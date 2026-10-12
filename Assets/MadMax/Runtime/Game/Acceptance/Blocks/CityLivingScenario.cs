using System.Collections;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 28 R3, living aboard the generated tracked city: shopkeepers stand behind the ground-floor
    /// counters, the housing clerk waits by the gangway and residents are about; nobody may build on the open deck;
    /// renting a free flat at its door allows building inside it (and only there); a piece built in the flat rides with
    /// the city; leases survive a save; the player climbs the block's stair core to the flat on foot.</summary>
    class CityLiving : Scenario
    {
        public override string Id => "city.living";
        public override float Timeout => 200f;
        public override GameRules WorldRules => new GameRules { randomSeed = false, start = 1 };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.Until(() => g.City && g.City.Ready, 40f);
            var city = g.City;
            if (!c.Check(city && city.Ready && city.Plan != null, "the generated tracked city is built")) yield break;
            var folk = city.GetComponent<MadMax.Npc.CityFolk>();
            yield return PH.Until(() => folk && folk.Keepers >= 3 && folk.Clerk, 40f);
            c.Check(folk && folk.Keepers >= 3, $"shopkeepers behind the ground-floor counters ({(folk ? folk.Keepers : 0)})");
            c.Check(folk && folk.Clerk, "the housing clerk by the gangway");
            c.Metric("people_aboard", folk ? folk.Alive : 0, "");
            var P = g.Player;
            var rig = g.cameraRig;

            var spots = TrackedCity.DeckSpots(city.Plan);
            var deckPoint = city.PlanPoint(spots[0] + new Vector3(0f, 0f, 0.1f));
            c.Check(!g.CanBuildAt(deckPoint, out var why), $"building on the open deck is refused ({why})");

            TrackedCity.Room room = null;
            foreach (var r in city.Plan.rooms) if (!WastelandGame.Occupied(city.Plan, r) && r.floor >= 2 && r.floor <= 4) { room = r; break; }
            if (!c.Check(room != null, "a free flat to rent")) yield break;
            int rent = WastelandGame.Rent(room);
            g.Inventory.Add(ResourceType.Scrap, rent + 10);
            c.Fixture($"{rent + 10} scrap granted");
            string prompt = g.FlatPrompt(city, room.index);
            c.Check(prompt != null && prompt.Contains("FOR RENT"), $"its door sign offers it ({prompt})");
            g.FlatUse(city, room.index, false);
            var lease = g.LeaseOf(CityHost.Hosts.IndexOf(city), room.index);
            c.Check(g.Holds(lease), $"rented {WastelandGame.FlatLabel(city.Plan, room)} for {rent} scrap (to day {(lease != null ? lease.until : 0):0.0})");
            var inside = city.PlanPoint(new Vector3((room.x0 + room.x1) / 2, (room.y0 + room.y1) / 2, room.z0 + 0.05f));
            c.Check(g.CanBuildAt(inside, out why), "building is allowed inside the rented flat");
            var nextDoor = city.PlanPoint(new Vector3(room.x1 + 1.5f, (room.y0 + room.y1) / 2, room.z0 + 0.05f));
            var other = city.RoomAt(nextDoor);
            c.Check(other == null || other == room || !g.CanBuildAt(nextDoor, out why), $"...and not in the flat next door ({why})");

            // walk up: lobby → stair core → corridor → the flat (the plan's own route, on foot)
            var route = TrackedCity.RouteIn(city.Plan, room);
            P.Teleport(city.PlanPoint(route[0]) + Vector3.up * 0.1f, city.transform.eulerAngles.y);
            if (rig) rig.mode = ViewMode.Isometric;
            yield return new WaitForSeconds(0.5f);
            float t0 = Time.time;
            for (int i = 1; i < route.Count && Time.time - t0 < 100f; i++)
            {
                for (float t = 0f; t < 12f; t += Time.deltaTime)
                {
                    var w = city.PlanPoint(route[i]);
                    var d = w - P.transform.position; d.y = 0f;
                    if (d.magnitude < 0.45f) break;
                    P.AutoWalk = d.normalized;
                    yield return null;
                }
            }
            P.AutoWalk = null;
            yield return new WaitForSeconds(0.5f);
            var here = city.RoomAt(P.transform.position);
            c.Check(here == room, $"climbed the stair core to the flat on foot ({(here != null ? WastelandGame.FlatLabel(city.Plan, here) : "not in a flat")}, {Time.time - t0:0} s, deck-local y {city.transform.InverseTransformPoint(P.transform.position).y:0.0})");
            c.Screenshot("in_the_flat");
            yield return null;

            var piece = MadMax.Building.FurnitureLibrary.Spawn("chair", city.transform, city.transform.InverseTransformPoint(inside), Quaternion.identity, g.propMaterial);
            if (c.Check(piece, "a chair placed in the flat"))
            {
                piece.owner = g.Stats.name;
                var local0 = city.transform.InverseTransformPoint(piece.transform.position);
                var keep = city.transform.InverseTransformPoint(P.transform.position);
                city.Clock += 200; city.Snap();
                P.Teleport(city.transform.TransformPoint(keep), P.transform.eulerAngles.y);              // the player stays in the flat
                yield return new WaitForSeconds(0.5f);
                c.Check(Vector3.Distance(local0, city.transform.InverseTransformPoint(piece.transform.position)) < 0.05f, "the chair rides with the city (parented to the hull)");
            }

            var d1 = new SaveData();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(WastelandGame).GetMethod("QuartersSave", flags).Invoke(g, new object[] { d1 });
            g.Leases.Clear();
            typeof(WastelandGame).GetMethod("QuartersLoad", flags).Invoke(g, new object[] { d1 });
            c.Check(g.Holds(g.LeaseOf(CityHost.Hosts.IndexOf(city), room.index)), $"the lease survives a save ({d1.blockQuarters.Count} line)");

            TrackedCity.Room shop = null;
            foreach (var r in city.Plan.rooms) if (r.shop) { shop = r; break; }
            if (shop != null)
            {
                P.Teleport(city.PlanPoint(new Vector3(shop.door.x, shop.door.y, shop.z0)) + Vector3.up * 0.1f, city.transform.eulerAngles.y);
                if (rig) rig.mode = ViewMode.ThirdPerson;
                yield return new WaitForSeconds(1.5f);
                c.Screenshot("a_shop");
                yield return null;
            }
            var clerk = folk ? folk.Clerk : null;
            if (clerk)
            {
                P.Teleport(clerk.transform.position + clerk.transform.forward * 2.5f + Vector3.up * 0.1f, clerk.transform.eulerAngles.y + 180f);
                if (rig) { rig.mode = ViewMode.ThirdPerson; rig.LookToward(clerk.transform.position - P.transform.position); }
                yield return new WaitForSeconds(1.5f);
                c.Screenshot("housing_clerk");
                yield return null;
            }
            if (rig) rig.mode = ViewMode.Isometric;
        }
    }
}
