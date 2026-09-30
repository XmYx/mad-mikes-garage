using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the Defence block.</summary>
    public static class DefenceScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new DefenceLadder();
        }
    }

    /// <summary>Depth stage I end to end in a live seed-7 world. Materials: planks sawn by hand and at the powered
    /// sawmill (faster, more per log), green bricks moulded and fired in the kiln, a prefab panel cast at the mixer; the
    /// processed walls outlast the raw ones (nine blows break the wood wall, the plank wall stands). Defence: a
    /// motorised gate on a powered net opens for the owner's car and stays shut for a stranger's; a tripwire rings the
    /// alarm bell; the watchtower perch puts the eye above 5 m and widens the view; the MG nest holds fire without a
    /// gunner and hits a raider with the player on it; the owner walks over their own mine, a raider stepping on one and
    /// a car driven over another set them off (the mine is spent, the car damaged).
    /// Layout on the pad (forward = the lane): stations in a half ring behind the player, walls 8 m back, the gate 14 m
    /// up the lane with mines beyond it at 20 and 26 m, tripwire and bell to the right, the tower to the left, the nest
    /// ahead right.</summary>
    class DefenceLadder : Scenario
    {
        public override string Id => "defence.ladder";
        public override float Timeout => 200f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(12f, 30f, out var pad, out var dir)) { c.Block("no open pad with a lane"); yield break; }
            var side = Vector3.Cross(Vector3.up, dir);
            g.Player.Teleport(pad + Vector3.up * 0.3f, Yaw(dir));
            yield return new WaitForSeconds(0.4f);
            c.Fixture($"on foot at a level pad {pad.x:0},{pad.z:0} with a 30 m lane");

            var gen = Spawn(g, "coal_generator", At(pad, dir, side, -1f, 6f), -side);
            yield return null;
            var genNode = gen ? gen.GetComponent<UtilityNode>() : null;
            var genComp = gen ? gen.GetComponent<Generator>() : null;
            if (!c.Check(genNode && genComp, "steam generator spawns")) yield break;
            genComp.fuel = 120f; genComp.on = true;
            c.Fixture("fuelled steam generator (2.5 kW) cabled to the sawmill and the motorised gate");

            yield return Materials(c, g, pad, dir, side, genNode);
            yield return Walls(c, g, pad, dir, side);

            // lay two mines up the lane now: they arm while the other defences are tried
            var m1 = Spawn(g, "landmine", At(pad, dir, side, 20f, 0f), dir);
            var m2 = Spawn(g, "landmine", At(pad, dir, side, 26f, 0f), dir);
            c.Fixture("two landmines laid on the lane at 20 and 26 m (owner: the player)");

            var car = Car(g);
            if (!car) c.Block("no fleet car for the gate and the mine");
            else yield return Gate(c, g, pad, dir, side, genNode, car);
            yield return Wire(c, g, pad, dir, side);
            yield return Tower(c, g, pad, dir, side);
            yield return Nest(c, g, pad, dir, side);
            yield return Mines(c, g, pad, dir, m1, m2, car);

            g.Player.Teleport(pad + Vector3.up * 0.3f, Yaw(dir));
            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (rig) rig.SetTarget(g.Player.transform);
            yield return new WaitForSeconds(1f);
            c.Screenshot("defence_ladder");
            yield return null;
        }

        // ------------------------------------------------------------------ materials
        IEnumerator Materials(ScenarioContext c, WastelandGame g, Vector3 pad, Vector3 dir, Vector3 side, UtilityNode genNode)
        {
            var steps = new[] { ("saw_bench", "saw_planks"), ("sawmill", "mill_planks"), ("brick_mould", "mould_bricks"), ("kiln", "fire_bricks"), ("mixer", "concrete_panel") };
            var me = g.Player.transform.position;
            int made = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                var (piece, id) = steps[i];
                var r = RecipeLibrary.Get(id);
                if (!c.Check(r != null, "recipe " + id + " exists")) continue;
                float a = (100f + i * 40f) * Mathf.Deg2Rad;                                           // a half ring behind the player
                var at = me + (dir * Mathf.Cos(a) + side * Mathf.Sin(a)) * 4f;
                var pl = Spawn(g, piece, at, me - at);
                yield return null;
                var st = pl ? pl.GetComponent<CraftingStation>() : null;
                if (!c.Check(st && st.type == r.station, $"{piece} offers {r.station} recipes")) continue;
                var node = pl.GetComponent<UtilityNode>();
                if (st.watts > 0f && node) { node.Link(genNode, UtilityKind.Power); float t1 = Time.time; while (!st.Powered && Time.time - t1 < 3f) yield return null; }
                if (!c.Check(st.Powered, piece + " has power")) continue;
                foreach (var (t, n) in r.resources) if (t != ResourceType.None) g.Inventory.Add(t, RecipeLibrary.Amount(n));
                foreach (var (it, n) in r.items)                                                        // green bricks come from the mould rung
                    if (g.Inventory.GetItem(it) < n) { c.Fixture("granted " + (n - g.Inventory.GetItem(it)) + " " + it); g.Inventory.AddItem(it, n - g.Inventory.GetItem(it)); }
                string why = g.CraftBlockReason(r, st);
                if (!c.Check(why == null, $"{r.name} craftable at the {piece}" + (why != null ? ": " + why : ""))) continue;
                int before = Output(g, r, st);
                g.Craft(r, st);
                if (st.queue.Count > 0) st.queue[0].progress = 0.95f;
                float t0 = Time.time;
                while (st.queue.Count > 0 && Time.time - t0 < 20f) yield return null;
                yield return new WaitForSeconds(0.2f);
                g.CollectTray(st);
                int got = Output(g, r, st) - before;
                if (c.Check(got == r.amount, $"{piece}: {r.name} delivers {got} of {r.amount}")) made++;
            }
            c.Fixture("inputs granted per rung (wood, clay, water, charcoal, concrete, iron); jobs started at 95 %");
            c.Metric("material_rungs", made, "");
            var hand = RecipeLibrary.Get("saw_planks"); var mill = RecipeLibrary.Get("mill_planks");
            c.Check(hand != null && mill != null && RecipeLibrary.Seconds(mill) < RecipeLibrary.Seconds(hand) && mill.amount > hand.amount && mill.resources[0].amount <= hand.resources[0].amount,
                "the sawmill is faster than the hand saw and gets more planks from the same wood");
        }

        static int Output(WastelandGame g, Recipe r, CraftingStation st) =>
            r.kind == OutputKind.Resource ? g.Inventory.Get(r.outputResource) + st.tray.Get(r.outputResource) : g.Inventory.GetItem(r.output) + st.tray.GetItem(r.output);

        // ------------------------------------------------------------------ walls
        IEnumerator Walls(ScenarioContext c, WastelandGame g, Vector3 pad, Vector3 dir, Vector3 side)
        {
            var ids = new[] { "wall_wood", "wall_plank", "wall_brick", "wall_brick_fired", "wall_panel" };
            var walls = new Placeable[ids.Length];
            for (int i = 0; i < ids.Length; i++) walls[i] = Spawn(g, ids[i], At(pad, dir, side, -8f, (i - 2) * 2.3f), dir);
            yield return null;
            if (!c.Check(walls.All(w => (bool)w), "the five walls stand")) yield break;
            int Hits(string id) => FurnitureLibrary.Get(id).hits;
            c.Check(walls[1].MaxHits > walls[0].MaxHits, $"plank wall {walls[1].MaxHits} hits > wood wall {walls[0].MaxHits}");
            c.Check(walls[3].MaxHits > walls[2].MaxHits, $"fired brick wall {walls[3].MaxHits} hits > stone brick wall {walls[2].MaxHits}");
            c.Check(walls[4].MaxHits > Hits("wall_concrete"), $"prefab panel wall {walls[4].MaxHits} hits > poured concrete {Hits("wall_concrete")}");
            c.Check(FurnitureLibrary.Get("wall_plank").upgrade == "wall_brick_fired" && FurnitureLibrary.Get("wall_brick_fired").upgrade == "wall_panel", "upgrade chain: plank → fired brick → prefab panel");
            var wood = walls[0]; var plank = walls[1];
            for (int i = 0; i < 9; i++)
            {
                if (wood) wood.ApplyHit(wood.transform.position + Vector3.up, -dir, 1f, 0.2f, g.Player.gameObject);
                if (plank) plank.ApplyHit(plank.transform.position + Vector3.up, -dir, 1f, 0.2f, g.Player.gameObject);
            }
            yield return null;
            c.Check(!wood && plank && plank.hits == plank.MaxHits - 9, "nine hammer blows break the wood wall; the plank wall stands" + (plank ? $" ({plank.hits}/{plank.MaxHits})" : ""));
        }

        // ------------------------------------------------------------------ gate
        IEnumerator Gate(ScenarioContext c, WastelandGame g, Vector3 pad, Vector3 dir, Vector3 side, UtilityNode genNode, VehicleDriver car)
        {
            var at = At(pad, dir, side, 14f, 0f);
            var frame = Spawn(g, "gate_frame", at, -dir);
            var piece = Spawn(g, "motorised_gate", at, -dir);                                            // fitted into the frame, as the build snap does
            yield return null;
            var gate = piece ? piece.GetComponent<MotorGate>() : null;
            if (!c.Check(frame && gate && FurnitureLibrary.Get("motorised_gate").snapTo == "gate_frame", "gate frame with a motorised gate fitted")) yield break;
            piece.GetComponent<UtilityNode>().Link(genNode, UtilityKind.Power);
            float t0 = Time.time;
            while (!gate.Motor && Time.time - t0 < 3f) yield return null;
            if (!c.Check(gate.Motor && gate.Openness < 0.01f, "the gate is powered and shut")) yield break;

            yield return TestWorld.Place(c, car, At(pad, dir, side, 4f, 0f), dir, 1.2f);
            g.Enter(car);
            c.Fixture("the player takes the wheel of their " + car.name.Replace("(Clone)", "") + " 10 m before the gate");
            t0 = Time.time;
            while (gate.Openness < 0.98f && Time.time - t0 < 9f) yield return null;
            c.Metric("gate_open", Time.time - t0, "s");
            c.Check(gate.Openness >= 0.98f, $"the gate opens for the owner's car ({gate.Openness * 100f:0} % open)");
            c.Screenshot("gate_open");
            yield return null;
            g.Exit();
            yield return new WaitForSeconds(0.4f);
            t0 = Time.time;
            while (gate.Openness > 0.02f && Time.time - t0 < 12f) yield return null;
            c.Check(gate.Openness <= 0.02f, "it closes once the owner has stepped out");

            var design = car.name.Replace("(Clone)", "").Trim();
            var stranger = g.SpawnAiVehicle(design, At(pad, dir, side, 9.5f, 0f) + Vector3.up, Quaternion.LookRotation(dir));
            if (!stranger) { c.Block("no " + design + " prefab for the stranger's car"); yield break; }
            yield return TestWorld.Place(c, stranger, At(pad, dir, side, 9.5f, 0f), dir, 1f);
            c.Fixture("a stranger's " + design + " (not in the fleet) 4.5 m before the gate");
            yield return new WaitForSeconds(3f);
            c.Check(gate.Openness <= 0.02f, "the gate stays shut for a stranger's car");
            var leaf = gate.leaf ? gate.leaf.GetComponent<Collider>() : null;
            c.Check(leaf && leaf.enabled && !leaf.isTrigger, "the shut leaf is solid (blocks vehicles until rammed through)");
        }

        // ------------------------------------------------------------------ tripwire
        IEnumerator Wire(ScenarioContext c, WastelandGame g, Vector3 pad, Vector3 dir, Vector3 side)
        {
            g.Player.Teleport(pad + Vector3.up * 0.3f, Yaw(dir));
            var wireAt = At(pad, dir, side, -4.5f, 8f);
            var bellPiece = Spawn(g, "alarm_bell", At(pad, dir, side, -7.5f, 8f), dir);
            var wirePiece = Spawn(g, "tripwire", wireAt, dir);                                      // the wire runs across the pad's right side
            yield return null;
            var bell = bellPiece ? bellPiece.GetComponent<AlarmBell>() : null;
            var wire = wirePiece ? wirePiece.GetComponent<Tripwire>() : null;
            if (!c.Check(bell && wire, "alarm bell and tripwire stand")) yield break;
            g.Player.Teleport(wireAt + Vector3.up * 0.1f, Yaw(dir));
            yield return new WaitForSeconds(0.6f);
            c.Check(wire.LastTripped < 0f, "the owner walks across their own wire without pulling it");
            g.Player.Teleport(pad + Vector3.up * 0.3f, Yaw(dir));
            yield return new WaitForSeconds(0.3f);
            float t0 = Time.time;
            var walker = MadMax.Npc.Npc.Spawn(NpcProfile.Make("test:wire", NpcRole.Wanderer, 4242), wireAt + Vector3.up * 0.05f, Yaw(dir), null, g.propMaterial);
            c.Fixture("a wanderer set down on the wire");
            while (wire.LastTripped < t0 && Time.time - t0 < 3f) yield return null;
            c.Check(wire.LastTripped >= t0, "someone crossing pulls the tripwire");
            c.Check(wire.LastBell == bell && bell.LastRung >= t0, "the tripwire rings the nearest alarm bell");
            if (walker) Object.Destroy(walker.gameObject);
        }

        // ------------------------------------------------------------------ watchtower
        IEnumerator Tower(ScenarioContext c, WastelandGame g, Vector3 pad, Vector3 dir, Vector3 side)
        {
            var at = At(pad, dir, side, -3f, -8f);
            var piece = Spawn(g, "watchtower", at, side);
            yield return null;
            var tower = piece ? piece.GetComponent<Watchtower>() : null;
            if (!c.Check(tower, "the watchtower stands")) yield break;
            g.Player.Teleport(piece.transform.TransformPoint(tower.ladderFoot) + Vector3.up * 0.3f, Yaw(-side));
            yield return new WaitForSeconds(0.3f);
            var rig = g.cameraRig;
            float iso0 = rig ? rig.isoSizeRange.y : 0f;
            tower.Use(g, false);
            yield return new WaitForSeconds(0.6f);
            float ground = MadMax.World.DeformableTerrain.Instance.Height(at.x, at.z);
            float eye = g.Player.Eye.position.y - ground;
            c.Metric("watch_eye_height", eye, "m");
            c.Check(tower.Up(g) && eye > 5f, $"on the watchtower the eye is {eye:0.0} m above the ground");
            if (rig) c.Check(rig.isoSizeRange.y > iso0, "the view reaches further from up there");
            c.Screenshot("watchtower");
            yield return null;
            g.Player.StandUp();
            yield return new WaitForSeconds(0.4f);
            c.Check(!tower.Up(g) && g.Player.transform.position.y < ground + 1f, "climbed down at the ladder foot");
            if (rig) c.Check(Mathf.Approximately(rig.isoSizeRange.y, iso0), "the view is back to normal");
        }

        // ------------------------------------------------------------------ MG nest
        IEnumerator Nest(ScenarioContext c, WastelandGame g, Vector3 pad, Vector3 dir, Vector3 side)
        {
            var at = At(pad, dir, side, 4f, 6f);
            var piece = Spawn(g, "mg_nest", at, dir);
            yield return null;
            var nest = piece ? piece.GetComponent<GunNest>() : null;
            var gun = piece ? piece.GetComponent<AutoTurret>() : null;
            var box = piece ? piece.GetComponent<Container>() : null;
            if (!c.Check(nest && gun && box && FurnitureLibrary.Get("mg_nest").upgrade == "auto_turret", "MG nest (upgrades to the auto turret)")) yield break;
            box.inventory.AddItem("ammo_mg", 2);
            c.Fixture("two MG belts in the nest's ammo box");
            var spot = At(pad, dir, side, 16f, 6f);
            var raider = MadMax.Npc.Npc.Spawn(NpcProfile.Make("test:nest", NpcRole.Raider, 5151), spot + Vector3.up * 0.05f, Yaw(-dir), null, g.propMaterial);
            raider.raiding = true; raider.raidAt = spot;
            c.Fixture("a raider 12 m in front of the nest");
            yield return new WaitForSeconds(1.5f);
            c.Check(raider.Alive && raider.Health >= 89f && box.inventory.GetItem("ammo_mg") == 2, "the nest holds fire without a gunner");
            WastelandGame.ExternalInput = true;
            nest.Use(g, true);
            nest.forceAim = true; nest.trigger = true;
            float t0 = Time.time;
            while (raider && raider.Alive && raider.Health > 60f && Time.time - t0 < 6f) { nest.aimAt = raider.transform.position + Vector3.up; yield return null; }
            c.Check(nest.Manning(g), "the player mans the gun");
            c.Check(!raider.Alive || raider.Health <= 60f, $"manned, the gun hits the raider ({(raider.Alive ? raider.Health : 0f):0} health left)");
            nest.trigger = false; nest.forceAim = false;
            WastelandGame.ExternalInput = false;
            g.Player.StandUp();
            yield return new WaitForSeconds(0.3f);
            if (raider) Object.Destroy(raider.gameObject);
        }

        // ------------------------------------------------------------------ mines
        IEnumerator Mines(ScenarioContext c, WastelandGame g, Vector3 pad, Vector3 dir, Placeable p1, Placeable p2, VehicleDriver car)
        {
            var m1 = p1 ? p1.GetComponent<Landmine>() : null;
            var m2 = p2 ? p2.GetComponent<Landmine>() : null;
            if (!c.Check(m1 && m2, "two landmines laid")) yield break;
            var at1 = p1.transform.position; var at2 = p2.transform.position;
            float t0 = Time.time;
            while (!(m1.Armed && m2.Armed) && Time.time - t0 < 12f) yield return null;
            if (!c.Check(m1.Armed && m2.Armed, "the mines arm after " + Landmine.ArmSeconds + " s")) yield break;
            c.Check(DefenceWorks.Concealed(p1) && FurnitureLibrary.Get("landmine").category == BuildCategory.Defence, "mines are hidden from raiders (not siege targets)");

            g.Player.Teleport(at2 + Vector3.up * 0.1f, Yaw(dir));
            c.Fixture("the owner walks onto their own mine");
            yield return new WaitForSeconds(1f);
            c.Check(p2 && !m2.Blown, "the owner on foot is safe on their own mine");
            g.Player.Teleport(pad + Vector3.up * 0.3f, Yaw(dir));
            yield return new WaitForSeconds(0.3f);

            int blasts = DefenceWorks.Blasts;
            var raider = MadMax.Npc.Npc.Spawn(NpcProfile.Make("test:mine", NpcRole.Raider, 7373), at2 + Vector3.up * 0.05f, Yaw(-dir), null, g.propMaterial);
            raider.raiding = true; raider.raidAt = at2;
            c.Fixture("a raider set down on the second mine");
            t0 = Time.time;
            while (p2 && Time.time - t0 < 3f) yield return null;
            c.Check(!p2 && DefenceWorks.Blasts > blasts, "a raider stepping on a mine sets it off; the mine is spent");
            c.Check(!raider || !raider.Alive || raider.Health < 50f, "the raider takes the blast" + (raider && raider.Alive ? $" ({raider.Health:0} left)" : " (dead)"));
            if (raider) Object.Destroy(raider.gameObject);

            if (!car) yield break;
            float wear0 = Wear(car);
            yield return TestWorld.Place(c, car, at1, dir, 2f);
            c.Fixture("the player's car set down over the first mine");
            float wear1 = Wear(car);
            c.Metric("mine_vehicle_damage", wear1 - wear0, "");
            c.Check(!p1, "a vehicle over a mine sets it off; the mine is spent");
            c.Check(wear1 > wear0 + 0.2f, $"the car is damaged (part damage and tyres {wear0:0.00} → {wear1:0.00})");
        }

        /// <summary>Summed part damage, popped tyres and lost parts of a vehicle.</summary>
        static float Wear(VehicleDriver v)
        {
            float w = 0f;
            foreach (var s in v.GetComponentsInChildren<MountSocket>(true)) w += s.Current ? s.Current.damage : 1f;
            foreach (var t in v.GetComponentsInChildren<WheelStats>()) if (t.Popped) w += 1f;
            return w;
        }

        // ------------------------------------------------------------------ helpers
        static float Yaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;

        static Vector3 At(Vector3 pad, Vector3 dir, Vector3 side, float ahead, float right)
        {
            var p = pad + dir * ahead + side * right;
            p.y = MadMax.World.DeformableTerrain.Instance.Height(p.x, p.z);
            return p;
        }

        /// <summary>A piece on the ground at <paramref name="at"/> facing <paramref name="facing"/>, built by the player.</summary>
        static Placeable Spawn(WastelandGame g, string id, Vector3 at, Vector3 facing)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-4f) facing = Vector3.forward;
            var root = g.Build.Structures;
            var p = FurnitureLibrary.Spawn(id, root, root.InverseTransformPoint(at), Quaternion.Inverse(root.rotation) * Quaternion.LookRotation(facing.normalized), g.propMaterial);
            if (p) { p.owner = g.Stats.name; _ = p.Id; }
            return p;
        }

        /// <summary>A plain road car from the fleet (the sedan when there is one).</summary>
        static VehicleDriver Car(WastelandGame g)
        {
            var sedan = TestWorld.Vehicle("Sedan");
            if (sedan && g.Fleet.Contains(sedan)) return sedan;
            return g.Fleet.FirstOrDefault(v => v && v.driveable && !v.aiDriven && !v.GetComponent<Machine>() && !v.GetComponent<BikeBalance>() && !v.GetComponent<FlightModel>() && !v.GetComponent<BoatModel>()
                                               && v.GetComponentsInChildren<WheelStats>().Length >= 4);
        }
    }
}
