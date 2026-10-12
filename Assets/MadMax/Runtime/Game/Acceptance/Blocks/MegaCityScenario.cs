using System.Collections;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 28 R4, the megacity: it is built on its graded plaza with a lift per cluster; the housing clerk
    /// waits by the first lift; a call brings the platform down, the landing gates open only while it is parked, the
    /// player rides it to the cluster deck and walks off; shopkeepers are about up there; nobody may build on the open
    /// deck; a free flat is rented at its door and reached on foot up the block's stair core, and building is allowed
    /// only inside it.</summary>
    class MegaCityLiving : Scenario
    {
        public override string Id => "megacity.living";
        public override float Timeout => 300f;
        public override GameRules WorldRules => new GameRules { randomSeed = false };

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (!c.Check(g.World != null && g.World.mega != null, "the world has a megacity site")) yield break;
            yield return PH.Until(() => g.Mega && g.Mega.IsReady, 90f);
            var mega = g.Mega;
            if (!c.Check(mega && mega.IsReady && mega.CityPlan != null, $"{(mega ? mega.CityName : "the megacity")} is built")) yield break;
            var B = mega.Built;
            g.World.Yard(out var yard, out _, out _);
            float yardDist = Vector2.Distance(new Vector2(yard.x, yard.z), g.World.mega.centre);
            c.Check(B.towers.Count >= 3 && B.clusters.Count >= 3 && mega.Lifts.Count == B.clusters.Count, $"{B.towers.Count} towers, {B.clusters.Count} cluster decks, {mega.Lifts.Count} lifts, {B.blocks.Count} blocks, {mega.CityPlan.rooms.Count} flats, {yardDist:0} m from the yard");
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            Weather.Auto = false; Weather.Restore(false, 0f);                                    // clear skies for the captures
            DayNight.SetHours(10.5f);
            var P = g.Player;
            var rig = g.cameraRig;
            var cl = B.clusters[0];
            var lift = mega.Lifts[0];
            var along = cl.liftAlongX ? Vector3.right : Vector3.up;

            // the plaza by the first lift: the clerk
            var front = cl.lift + along * (MegaCity.ShaftHY + 1.0f) + new Vector3(0f, 0f, 0.15f);
            P.Teleport(mega.PlanPoint(front), 0f);
            yield return new WaitForSeconds(1f);
            var folk = mega.GetComponent<MadMax.Npc.CityFolk>();
            yield return PH.Until(() => folk && folk.Clerk, 40f);
            c.Check(folk && folk.Clerk, "the housing clerk waits on the plaza by the first lift");
            if (rig) { rig.mode = ViewMode.ThirdPerson; rig.LookToward(mega.PlanPoint(new Vector3(cl.t.x, cl.t.y, 0f)) - P.transform.position + Vector3.up * 60f); }
            yield return new WaitForSeconds(1.5f);
            c.Screenshot("plaza");
            yield return null;

            // call the lift, board it, ride up
            yield return PH.Until(() => lift.Parked(false) && lift.DwellLeft > 3f, 100f);
            if (!c.Check(lift.Parked(false), $"the call brings the platform down to the plaza ({lift.Level:0.00})")) yield break;
            yield return new WaitForSeconds(0.6f);
            c.Check(!GateClosed(mega, lift, false), "the plaza gates open while it is parked");
            var landing = lift.Landing(false);
            P.run = true;
            for (float t = 0f; t < 6f; t += Time.deltaTime)
            {
                var d = landing - P.transform.position; d.y = 0f;
                if (d.magnitude < 0.6f) break;
                P.AutoWalk = d.normalized;
                yield return null;
            }
            P.AutoWalk = null; P.run = false;
            var onb = landing - P.transform.position; onb.y = 0f;
            c.Check(onb.magnitude < 1.5f && g.Stats.health > 0f, $"walked onto the platform ({onb.magnitude:0.0} m from its centre, health {g.Stats.health:0})");
            float y0 = P.transform.position.y;
            yield return PH.Until(() => lift.Level > 0.3f, 30f);
            c.Check(GateClosed(mega, lift, false), "the plaza gates close behind it");
            c.Check(Mathf.Abs(P.transform.position.y - lift.PhysicsTop.y) < 0.5f, $"riding the platform ({P.transform.position.y - lift.PhysicsTop.y:+0.00;-0.00} m, health {g.Stats.health:0})");
            if (rig) rig.mode = ViewMode.Isometric;
            c.Screenshot("lift_ride");
            yield return null;
            float t0 = Time.time;
            yield return PH.Until(() => lift.Parked(true), 90f);
            float rise = P.transform.position.y - y0, above = P.transform.position.y - lift.Landing(true).y;
            c.Check(lift.Parked(true) && Mathf.Abs(above) < 0.6f, $"rode the lift up {rise:0.0} m to the deck at {cl.z:0} m in {Time.time - t0 + 1f:0} s (feet {above:+0.00;-0.00} m off the platform)");
            c.Metric("lift_ride_s", Time.time - t0, "s");

            // off onto the cluster deck
            yield return new WaitForSeconds(0.6f);
            var off = mega.PlanPoint(cl.lift + along * (MegaCity.ShaftHY + 3f) + new Vector3(0f, 0f, cl.z));
            for (float t = 0f; t < 8f; t += Time.deltaTime)
            {
                var d = off - P.transform.position; d.y = 0f;
                if (d.magnitude < 0.5f) break;
                P.AutoWalk = d.normalized;
                yield return null;
            }
            P.AutoWalk = null;
            yield return new WaitForSeconds(0.5f);
            var at = mega.ToPlan(P.transform.position);
            float miss = Vector2.Distance(new Vector2(at.x, at.y), new Vector2(cl.lift.x, cl.lift.y) + (Vector2)(along * (MegaCity.ShaftHY + 3f)));
            var lp = mega.ToPlan(lift.transform.position);
            c.Check(Mathf.Abs(at.z - cl.z) < 0.5f && miss < 1.2f, $"walked off onto the cluster deck (z {at.z:0.0}, {miss:0.0} m from the spot; at {at.x:0.0},{at.y:0.0}, lift {lp.x:0.0},{lp.y:0.0} along {along})");
            if (rig) rig.mode = ViewMode.Isometric;
            yield return new WaitForSeconds(1f);
            c.Screenshot("cluster_deck");
            yield return null;
            c.Check(!g.CanBuildAt(P.transform.position, out var why), $"building on the open deck is refused ({why})");
            yield return PH.Until(() => folk.Keepers >= 2, 30f);
            c.Check(folk.Keepers >= 2, $"shopkeepers up on the deck ({folk.Keepers}, {folk.Alive} people about)");

            // a flat on this deck: rent it, climb to it
            TrackedCity.Room room = null;
            foreach (var r in mega.CityPlan.rooms)
                if (!WastelandGame.Occupied(mega.CityPlan, r) && r.floor >= 1 && r.floor <= 3 && Mathf.Abs(mega.CityPlan.blocks[r.block].z0 - cl.z) < 0.5f) { room = r; break; }
            if (!c.Check(room != null, "a free flat on this deck")) yield break;
            int rent = WastelandGame.Rent(room);
            g.Inventory.Add(ResourceType.Scrap, rent + 10);
            c.Fixture($"{rent + 10} scrap granted");
            string prompt = g.FlatPrompt(mega, room.index);
            c.Check(prompt != null && prompt.Contains("FOR RENT"), $"its door sign offers it ({prompt})");
            g.FlatUse(mega, room.index, false);
            c.Check(g.Holds(g.LeaseOf(CityHost.Hosts.IndexOf(mega), room.index)), $"rented {WastelandGame.FlatLabel(mega.CityPlan, room)} in {mega.CityName} for {rent} scrap");
            var route = TrackedCity.RouteIn(mega.CityPlan, room);
            P.Teleport(mega.PlanPoint(route[0]) + Vector3.up * 0.1f, 0f);
            yield return new WaitForSeconds(0.5f);
            t0 = Time.time;
            for (int i = 1; i < route.Count && Time.time - t0 < 90f; i++)
                for (float t = 0f; t < 12f; t += Time.deltaTime)
                {
                    var d = mega.PlanPoint(route[i]) - P.transform.position; d.y = 0f;
                    if (d.magnitude < 0.45f) break;
                    P.AutoWalk = d.normalized;
                    yield return null;
                }
            P.AutoWalk = null;
            yield return new WaitForSeconds(0.5f);
            var here = mega.RoomAt(P.transform.position);
            c.Check(here == room, $"climbed the stair core to the flat ({(here != null ? WastelandGame.FlatLabel(mega.CityPlan, here) : "not in a flat")}, {Time.time - t0:0} s)");
            c.Check(g.CanBuildAt(P.transform.position, out why), "building is allowed inside the rented flat");
            c.Screenshot("in_the_flat");
            yield return null;

            // the landing pad and the skyline
            if (B.hasPad)
            {
                P.Teleport(mega.PadCentreWorld + Vector3.up * 0.2f, 0f);
                if (rig) rig.mode = ViewMode.Isometric;
                yield return new WaitForSeconds(1.5f);
                var onPad = mega.ToPlan(P.transform.position);
                c.Check(Mathf.Abs(onPad.z - B.padZ) < 0.6f, $"the landing pad holds the player (z {onPad.z:0.0} of {B.padZ:0})");
                c.Screenshot("landing_pad");
                yield return null;
            }
            float h = g.World.mega.half - 6f;
            P.Teleport(mega.PlanPoint(new Vector3(-h, -h, 0.15f)), 45f);
            if (rig) { rig.mode = ViewMode.ThirdPerson; rig.LookToward(mega.transform.position + Vector3.up * 90f - P.transform.position); }
            yield return new WaitForSeconds(2f);
            c.Screenshot("skyline");
            yield return null;
            bool wasVector = GameSettings.Current.vector;                                        // the same geometry in pixel art (only the shading differs)
            GameSettings.Current.vector = !wasVector; GameSettings.Current.Apply(g);
            yield return new WaitForSeconds(1.5f);
            c.Screenshot(wasVector ? "skyline_pixel" : "skyline_vector");
            yield return null;
            GameSettings.Current.vector = wasVector; GameSettings.Current.Apply(g);
            if (rig) rig.mode = ViewMode.Isometric;
            Weather.Auto = true;
        }

        static bool GateClosed(MegaCityHost mega, CityLift lift, bool top)
        {
            bool any = false;
            foreach (var gt in mega.Gates) if (gt.lift == lift && gt.top == top) { if (!gt.Closed) return false; any = true; }
            return any;
        }
    }
}
