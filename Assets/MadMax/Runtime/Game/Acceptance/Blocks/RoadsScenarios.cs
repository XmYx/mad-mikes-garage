using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the Roads block.</summary>
    public static class RoadsScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new RoadsLadder();
        }
    }

    /// <summary>Depth stage C, the road ladder end to end on an off-road lane near the start: gravel broken by hand at
    /// the workbench and crushed at a powered rock crusher (stone, rubble with sand besides); the rake spreads it into
    /// a road-like gravel patch that shows on the minimap, the tamper sets cobbles; asphalt takes no paint while wet,
    /// then the line painter paints a cell, a line while held, and yellow; heavy wear breaks it into a pothole the rake
    /// patches with asphalt; the tipper spreads a gravel strip while it drives with the bed raised; the paver offers
    /// gravel; a car set on a timber bridge over a ditch rests on the deck, and the steel bridge's deck carries wheels too.</summary>
    class RoadsLadder : Scenario
    {
        public override string Id => "roads.ladder";
        public override float Timeout => 200f;

        static Vector3 Ground(Vector3 p) { var t = DeformableTerrain.Instance; return new Vector3(p.x, t.Height(p.x, p.z), p.z); }
        static float Yaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        static Vector3 Snap(Vector3 p) { const float c = DeformableTerrain.Cell; return new Vector3(Mathf.Round(p.x / c) * c, p.y, Mathf.Round(p.z / c) * c); }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!OffRoadLane(26f, out var pad, out var dir)) { c.Block("no level, clear off-road lane near the start"); yield break; }
            var side = Vector3.Cross(Vector3.up, dir);
            c.Fixture($"test lane off the road at {pad.x:0},{pad.z:0}, heading {Yaw(dir):0}°");
            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (rig) rig.SetTarget(g.Player.transform);

            // ---- 1. gravel: by hand at the workbench, then the powered rock crusher
            var yard = Ground(pad - side * 9f);
            g.Player.Teleport(yard + side * 2f + Vector3.up * 0.3f, Yaw(-side));
            yield return new WaitForSeconds(0.4f);
            var bench = FurnitureLibrary.Spawn("workbench", g.Build.Structures, Ground(yard - dir * 3f), Quaternion.LookRotation(side), g.propMaterial);
            var gen = FurnitureLibrary.Spawn("coal_generator", g.Build.Structures, Ground(yard + dir * 3.5f), Quaternion.LookRotation(side), g.propMaterial);
            var crusher = FurnitureLibrary.Spawn("rock_crusher", g.Build.Structures, yard, Quaternion.LookRotation(side), g.propMaterial);
            yield return null;
            var crush = crusher ? crusher.GetComponent<CraftingStation>() : null;
            var genNode = gen ? gen.GetComponent<UtilityNode>() : null; var genComp = gen ? gen.GetComponent<Generator>() : null;
            if (!c.Check(crush && crush.type == "rock_crusher" && crush.watts > 0f, "the rock crusher is a powered station")) yield break;
            if (!c.Check(genNode && genComp, "steam generator spawns")) yield break;
            genComp.fuel = 60f; genComp.on = true;
            crusher.GetComponent<UtilityNode>().Link(genNode, UtilityKind.Power);
            c.Fixture("workbench, fuelled steam generator (2.5 kW) cabled to the rock crusher");
            float t0 = Time.time;
            while (!crush.Powered && Time.time - t0 < 3f) yield return null;
            c.Check(crush.Powered, "the crusher has power");
            yield return Craft(c, g, "gravel_hand", bench ? bench.GetComponent<CraftingStation>() : null);
            yield return Craft(c, g, "crush_stone", crush);
            int sand0 = g.Inventory.Get(ResourceType.Sand) + crush.tray.Get(ResourceType.Sand);
            yield return Craft(c, g, "crush_rubble", crush);
            c.Check(g.Inventory.Get(ResourceType.Sand) + crush.tray.Get(ResourceType.Sand) > sand0, "crushed rubble leaves sand besides");
            c.Metric("gravel_made", g.Inventory.Get(ResourceType.Gravel), "units");

            // ---- 2. the rake: a gravel patch from the crushed gravel
            var rakeAt = Ground(pad + side * 6f);
            g.Player.Teleport(rakeAt + Vector3.up * 0.3f, Yaw(dir));
            yield return new WaitForSeconds(0.4f);
            var front = Ground(g.Player.transform.position + g.Player.transform.forward * 1.4f);
            var bare = t.SurfaceAt(front.x, front.z);
            var rake = ToolLibrary.Create("tool_rake", g.propMaterial) as PavingTool;
            if (!c.Check(rake, "the road rake is a paving tool")) yield break;
            g.Player.Equip(rake);
            int gv0 = g.Inventory.Get(ResourceType.Gravel);
            rake.Strike(g.Player);
            yield return null;
            var gs = t.SurfaceAt(front.x, front.z);
            c.Check(t.PaveAt(front.x, front.z) == DeformableTerrain.PaveGravel, "the rake lays gravel in front of the player");
            c.Check(gs.road >= 0.4f && gs.mud <= 0.001f && gs.softness <= 0.35f && gs.road > bare.road,
                    $"the gravel is road-like (road {bare.road:0.00} → {gs.road:0.00}, mud {gs.mud:0.00}, softness {gs.softness:0.00})");
            c.Check(g.Inventory.Get(ResourceType.Gravel) == gv0 - 1, "one gravel per patch");
            c.Check(t.PlayerRoadAt(front.x, front.z, out bool gravelMark) && gravelMark, "the gravel patch shows on the minimap");
            int patched = 0;
            for (float s = -1f; s <= 1f; s += 0.25f) if (t.PaveAt(front.x + side.x * s, front.z + side.z * s) == DeformableTerrain.PaveGravel) patched++;
            c.Metric("gravel_patch_width_cells", patched, "cells");

            // ---- 3. the tamper: cobbles from stone
            g.Player.Teleport(rakeAt + dir * 3.5f + Vector3.up * 0.3f, Yaw(dir));
            yield return new WaitForSeconds(0.3f);
            var tamper = ToolLibrary.Create("tool_tamper", g.propMaterial) as PavingTool;
            g.Player.Equip(tamper);
            g.Inventory.Add(ResourceType.Stone, 4);
            c.Fixture("4 stone for the tamper");
            var cobbleAt = g.Player.transform.position + g.Player.transform.forward * 1.2f;
            if (tamper) tamper.Strike(g.Player);
            yield return null;
            c.Check(t.PaveAt(cobbleAt.x, cobbleAt.z) == DeformableTerrain.PaveCobbles && t.SurfaceAt(cobbleAt.x, cobbleAt.z).road >= 0.9f, "the tamper sets firm cobbles");

            // ---- 4. road paint on set asphalt
            var paintAt = Ground(pad + side * 6f + dir * 9f);
            var slab = Ground(paintAt + dir * 1.6f);
            t.ApplyTerraform((byte)DeformableTerrain.TerraOp.Pave, slab, 2.2f, 0f, DeformableTerrain.PaveAsphalt);
            c.Fixture("a 4.4 m patch of wet asphalt laid with the paver's terraform op");
            g.Player.Teleport(paintAt + Vector3.up * 0.3f, Yaw(dir));
            yield return new WaitForSeconds(0.3f);
            var painter = ToolLibrary.Create("tool_line_painter", g.propMaterial) as LinePainterTool;
            if (!c.Check(painter, "the line painter exists")) yield break;
            g.Player.Equip(painter);
            g.Inventory.AddItem("dye_white", 2); g.Inventory.AddItem("dye_yellow", 1);
            c.Fixture("2 white and 1 yellow dye");
            LinePainterTool.Colour = 1;
            var nozzle = g.Player.transform.position + g.Player.transform.forward * painter.reach;
            painter.Strike(g.Player);
            c.Check(t.PaveAt(nozzle.x, nozzle.z) == DeformableTerrain.PaveAsphalt, "wet asphalt takes no paint");
            t.CureNow(slab, 2.3f);
            c.Fixture("the asphalt patch set at once (curing time skipped)");
            painter.Strike(g.Player);
            c.Check(t.PaveAt(nozzle.x, nozzle.z) == DeformableTerrain.PaveAsphaltWhite, "the painter paints set asphalt white (pave " + t.PaveAt(nozzle.x, nozzle.z) + ")");
            painter.spraying = true;
            var walk0 = g.Player.transform.position;
            for (int i = 1; i <= 20; i++) { g.Player.Teleport(walk0 + dir * (i * 0.125f), Yaw(dir)); yield return null; }
            painter.spraying = false;
            yield return null;
            int line = 0;
            for (float s = 0f; s <= 2.5f; s += DeformableTerrain.Cell) { var q = nozzle + dir * s; if (t.PaveAt(q.x, q.z) == DeformableTerrain.PaveAsphaltWhite) line++; }
            c.Fixture("walked 2.5 m in 0.125 m steps with the button held");
            c.Metric("line_cells", line, "cells");
            c.Check(line >= 8, $"holding the button paints a line as the player walks ({line} of 11 cells)");
            c.Check(LinePainterTool.Metres > 1.5f, $"paint is used by the metre ({LinePainterTool.Metres:0.0} m towards the next dye)");
            LinePainterTool.Colour = 2;
            g.Player.Teleport(paintAt + side * 1f + Vector3.up * 0.3f, Yaw(dir));
            yield return null;
            var yellowAt = g.Player.transform.position + g.Player.transform.forward * painter.reach;
            painter.Strike(g.Player);
            c.Check(t.PaveAt(yellowAt.x, yellowAt.z) == DeformableTerrain.PaveAsphaltYellow, "and yellow");
            c.Screenshot("paint");
            yield return null;

            // ---- 5. heavy wear breaks the asphalt into a pothole; the rake patches it
            var hole = Ground(Snap(slab - side * 1.2f + dir * 0.5f));                                 // on a cell corner: heights are exact there
            float keepLoad = t.wearLoad;
            t.wearLoad = 1f;
            t.Deform(hole, dir, side, 0.4f, 30000f, 0f, 0.02f);
            t.wearLoad = keepLoad;
            c.Fixture("wear scaled so one heavy wheel pass breaks the asphalt (instead of 120+)");
            var ps = t.SurfaceAt(hole.x, hole.z);
            c.Check(t.PaveAt(hole.x, hole.z) == DeformableTerrain.PavePothole && ps.road < 1f && ps.mud > 0f && t.Height(hole.x, hole.z) < hole.y - 0.02f,
                    $"a heavy wheel on set asphalt breaks a pothole (road {ps.road:0.00}, mud {ps.mud:0.00})");
            g.Player.Equip(rake = ToolLibrary.Create("tool_rake", g.propMaterial) as PavingTool);
            g.Player.Teleport(hole - dir * 1.4f + Vector3.up * 0.3f, Yaw(dir));
            yield return new WaitForSeconds(0.2f);
            g.Inventory.Add(ResourceType.Asphalt, 1);
            c.Fixture("1 asphalt for the patch");
            int asph = g.Inventory.Get(ResourceType.Asphalt);
            rake.Strike(g.Player);
            yield return null;
            c.Check(t.PaveAt(hole.x, hole.z) == DeformableTerrain.PaveAsphalt && t.CureAt(hole.x, hole.z) < 1f && g.Inventory.Get(ResourceType.Asphalt) == asph - 1,
                    "the rake patches the pothole with fresh asphalt");
            c.Check(Mathf.Abs(t.Height(hole.x, hole.z) - hole.y) < 0.04f, "the patch is level with the road again");

            // ---- 6. the tipper spreads a gravel road on the move
            yield return Tipper(c, g, pad, dir);

            // ---- 7. the paver lays gravel as its third material
            var paver = TestWorld.Vehicle("Paver");
            var pm = paver ? paver.GetComponent<Machine>() : null;
            if (pm)
            {
                pm.Control(new MachineKeys { ePressed = true }); pm.Control(new MachineKeys { ePressed = true });
                c.Check(pm.gravel && (pm.Status ?? "").Contains(ResourceInfo.Name(ResourceType.Gravel)), "the paver's material switch reaches gravel: " + pm.Status);
                pm.Control(new MachineKeys { ePressed = true });
                c.Check(!pm.gravel && !pm.concrete, "and back to asphalt");
            }
            else c.Note("no paver in the fleet: material switch not checked");

            // ---- 8. bridges
            yield return Bridges(c, g);

            // ---- 9. signs, rails, curbs, bollards
            int built = 0;
            var ids = new[] { "sign_stop", "sign_speed", "sign_direction", "guard_rail", "curb", "bollard" };
            for (int i = 0; i < ids.Length; i++)
            {
                var p = FurnitureLibrary.Spawn(ids[i], g.Build.Structures, Ground(pad + side * 3.5f + dir * (i * 2.4f)), Quaternion.LookRotation(-side), g.propMaterial);
                if (c.Check(p && p.GetComponent<Collider>(), ids[i] + " builds with a collider")) built++;
            }
            c.Metric("road_pieces", built, "");
            c.Check(FurnitureLibrary.Get("sign_stop").mesh.bounds.size.y > 2f, "signs stand over 2 m tall");
            g.Player.Teleport(pad + side * 1.5f + Vector3.up * 0.3f, Yaw(dir));
            if (rig) rig.SetTarget(g.Player.transform);
            yield return new WaitForSeconds(1f);
            c.Screenshot("roads");
            yield return null;
        }

        /// <summary>Craft one recipe at a station from granted inputs (time skipped) and check its output arrives.</summary>
        static IEnumerator Craft(ScenarioContext c, WastelandGame g, string id, CraftingStation st)
        {
            var r = RecipeLibrary.Get(id);
            if (!c.Check(r != null && st && st.type == r.station, $"recipe {id} at its station ({(r != null ? r.station : "?")})")) yield break;
            foreach (var (ty, n) in r.resources) if (ty != ResourceType.None) g.Inventory.Add(ty, RecipeLibrary.Amount(n));
            c.Fixture($"inputs for {r.name} granted; the job skipped to 95 %");
            string why = g.CraftBlockReason(r, st);
            if (!c.Check(why == null, r.name + " craftable" + (why != null ? ": " + why : ""))) yield break;
            int before = g.Inventory.Get(r.outputResource) + st.tray.Get(r.outputResource);
            g.Craft(r, st);
            if (st.queue.Count > 0) st.queue[0].progress = 0.95f;
            float t0 = Time.time;
            while (st.queue.Count > 0 && Time.time - t0 < 20f) yield return null;
            yield return new WaitForSeconds(0.2f);
            int got = g.Inventory.Get(r.outputResource) + st.tray.Get(r.outputResource) - before;
            c.Check(got == r.amount, $"{st.title}: {r.name} delivers {got} of {r.amount}");
        }

        /// <summary>Tipper with 60 gravel, bed raised ~24°, 16 m at a walking pace: a gravel strip behind it, no pile.</summary>
        static IEnumerator Tipper(ScenarioContext c, WastelandGame g, Vector3 pad, Vector3 dir)
        {
            var t = DeformableTerrain.Instance;
            var truck = TestWorld.Vehicle("DumpTruck");
            var tm = truck ? truck.GetComponent<Machine>() : null;
            if (!tm || !tm.Store) { c.Block("dump truck missing from the fleet"); yield break; }
            yield return TestWorld.Place(c, truck, pad, dir, 1.5f);
            tm.Store.inventory.Add(ResourceType.Gravel, 60);
            c.Fixture("60 gravel in the tipper bed");
            g.Enter(truck); yield return new WaitForSeconds(0.4f);
            yield return TestWorld.StartEngine(c, truck);
            if (c.Failed) yield break;
            WastelandGame.MachineInput = new MachineKeys { up = 1f };
            yield return new WaitForSeconds(2f);
            WastelandGame.MachineInput = default;
            int gravel0 = tm.Store.inventory.Get(ResourceType.Gravel);
            var tail0 = truck.transform.TransformPoint(new Vector3(0, 0, -50) * 0.08f);
            var p0 = truck.transform.position;
            truck.handbrake = false;
            bool spreading = false;
            float t0 = Time.time;
            while (Vector3.Distance(p0, truck.transform.position) < 16f && Time.time - t0 < 20f)
            {
                truck.steerInput = Mathf.Clamp(Vector3.SignedAngle(truck.transform.forward, dir, Vector3.up) / 20f, -1f, 1f);
                truck.throttleInput = truck.ForwardSpeed < 2.5f ? 0.6f : 0f;
                truck.brakeInput = 0f;
                spreading |= tm.Spreading;
                yield return null;
            }
            truck.throttleInput = 0f; truck.brakeInput = 1f; truck.steerInput = 0f;
            float moved = Vector3.Distance(p0, truck.transform.position);
            c.Note($"tipper: moved {moved:0.0} m in {Time.time - t0:0.0} s, {tm.Status}");
            yield return new WaitForSeconds(0.8f);
            int used = gravel0 - tm.Store.inventory.Get(ResourceType.Gravel);
            int cells = 0, samples = 0;
            for (float s = 1f; s <= moved - 1f; s += 0.5f) { var q = tail0 + dir * s; samples++; if (t.PaveAt(q.x, q.z) == DeformableTerrain.PaveGravel) cells++; }
            c.Metric("tipper_gravel_used", used, "units");
            c.Metric("tipper_strip_cover", samples > 0 ? cells / (float)samples : 0f, "");
            c.Check(spreading, "the tipper reports SPREADING GRAVEL with the bed raised on the move");
            c.Check(moved > 10f && samples > 0 && cells >= samples * 0.7f, $"a gravel strip behind the tipper ({cells} of {samples} points along {moved:0.0} m)");
            c.Check(used > 0 && used <= Mathf.CeilToInt(moved / 0.6f) + 2, $"about a unit of gravel per 0.6 m ({used} for {moved:0.0} m)");
            c.Screenshot("tipper");
            yield return null;
            WastelandGame.MachineInput = new MachineKeys { up = -1f };
            yield return new WaitForSeconds(2.2f);
            WastelandGame.MachineInput = default;
            truck.brakeInput = 0f; truck.handbrake = true;
            g.Exit(); yield return new WaitForSeconds(0.4f);
        }

        /// <summary>Timber bridge over a 2 m ditch: its deck carries a sedan's wheels; the steel bridge's deck registers too.</summary>
        static IEnumerator Bridges(ScenarioContext c, WastelandGame g)
        {
            var t = DeformableTerrain.Instance;
            if (!TestWorld.Pad(9f, out var bpad)) { c.Block("no level pad for the bridge"); yield break; }
            var span = Vector3.forward;
            var bridge = FurnitureLibrary.Spawn("bridge_timber", g.Build.Structures, Ground(bpad - span * 4f), Quaternion.LookRotation(-span), g.propMaterial);
            yield return null; yield return null;
            var rb = bridge ? bridge.GetComponent<RoadBridge>() : null;
            if (!c.Check(rb, "the timber bridge builds with a deck")) yield break;
            var mid = rb.Middle;
            c.Check(Vector3.Distance(new Vector3(mid.x, 0f, mid.z), new Vector3(bpad.x, 0f, bpad.z)) < 0.3f, "the bridge spans forward from where it is placed");
            for (int i = 0; i < 5; i++) t.ApplyTerraform((byte)DeformableTerrain.TerraOp.Dig, Ground(bpad), 2.4f, 0.6f, 0);
            float bottom = t.Height(bpad.x, bpad.z);
            c.Fixture($"a ditch dug under mid-span ({mid.y - bottom:0.0} m below the deck)");
            c.Check(bottom < mid.y - 1.5f, "the bridge spans a real gap");
            float h = bottom;
            bool onDeck = StructureGround.Top(mid + Vector3.up, ref h, out _);
            c.Check(onDeck && Mathf.Abs(h - mid.y) < 0.05f, $"wheels at mid-span find the deck ({h:0.00} vs {mid.y:0.00})");
            var car = TestWorld.Vehicle("Sedan");
            if (!car) { c.Block("sedan missing from the fleet"); yield break; }
            car.Body.position = mid + Vector3.up * 1.0f;
            car.Body.rotation = Quaternion.LookRotation(span);
            car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
            car.throttleInput = car.brakeInput = car.steerInput = 0f; car.handbrake = true;
            car.Body.WakeUp();
            c.Fixture("sedan set down 1 m over the bridge's mid-span");
            yield return new WaitForSeconds(2.5f);
            float rest = car.transform.position.y - mid.y;
            c.Metric("car_over_deck", rest, "m");
            c.Check(rest > -0.3f && rest < 1.3f && car.GroundedWheels >= 3, $"the car rests on the deck, not in the ditch ({rest:0.00} m over the deck, {car.GroundedWheels} wheels down)");
            var cam = Object.FindAnyObjectByType<CameraRig>();
            if (cam) cam.SetTarget(car.transform);
            yield return new WaitForSeconds(0.8f);
            c.Screenshot("bridge");
            yield return null;
            if (cam) cam.SetTarget(g.Player.transform);

            var steel = FurnitureLibrary.Spawn("bridge_steel", g.Build.Structures, Ground(bpad + Vector3.right * 7f - span * 6f), Quaternion.LookRotation(-span), g.propMaterial);
            yield return null; yield return null;
            var sb = steel ? steel.GetComponent<RoadBridge>() : null;
            if (!c.Check(sb, "the steel bridge builds with a deck")) yield break;
            var smid = sb.Middle;
            float sh = t.Height(smid.x, smid.z) - 1f;
            c.Check(StructureGround.Top(smid + Vector3.up, ref sh, out _) && Mathf.Abs(sh - smid.y) < 0.05f, "the steel bridge's 12 m deck carries wheels at mid-span");
        }

        /// <summary>A level, dry lane off the roads near the player, clear of props for the tipper run and the side pads.</summary>
        static bool OffRoadLane(float lane, out Vector3 pad, out Vector3 dir)
        {
            var g = WastelandGame.Instance; var t = DeformableTerrain.Instance;
            var focus = g.Player.transform.position;
            var dirs = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            for (int k = 0; k < 400; k++)
            {
                float a = k * 2.39996f, r = 14f + k * 0.3f;
                var p = focus + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                foreach (var d in dirs)
                {
                    var side = Vector3.Cross(Vector3.up, d);
                    float h0 = t.Height(p.x, p.z);
                    bool ok = true;
                    for (float s = -6f; s <= lane && ok; s += 4f)
                    foreach (float o in new[] { -9f, 0f, 6f })
                    {
                        var q = p + d * s + side * o;
                        var sf = t.SurfaceAt(q.x, q.z);
                        if (sf.road > 0.3f || sf.mud > 0.3f || t.WaterDepth(q.x, q.z) > 0f || t.HardRoadAt(q.x, q.z)
                            || Mathf.Abs(t.Height(q.x, q.z) - h0) > 0.06f * Mathf.Max(3f, Vector3.Distance(p, q))
                            || Vector3.Angle(t.Normal(q.x, q.z), Vector3.up) > 8f || (o == 0f && !Clear(t, q, 2.2f))) { ok = false; break; }   // the tipper's lane clear, the side pads level
                    }
                    if (ok) { pad = new Vector3(p.x, h0, p.z); dir = d; return true; }
                }
            }
            pad = dir = default;
            return false;
        }

        static readonly Collider[] hits = new Collider[32];

        static bool Clear(DeformableTerrain t, Vector3 p, float radius)
        {
            var q = new Vector3(p.x, t.Height(p.x, p.z) + 0.4f + radius, p.z);
            int n = Physics.OverlapCapsuleNonAlloc(q, q + Vector3.up * 3f, radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!hits[i].transform.name.StartsWith("Chunk")) return false;
            return true;
        }
    }
}
