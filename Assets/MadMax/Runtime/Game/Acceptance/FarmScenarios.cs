using System.Collections;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Depth stage B: the farming ladder end to end. The hoe tills a field bed by hand and it is sown by hand;
    /// the tractor ploughs a strip with its plough lowered, then the seeder sows it from the hopper, the sprayer waters
    /// it from the tank and (after the crop is grown on) the harvester reaps it into the grain bin. The irrigation
    /// timer holds sprinklers to its window and midday watering costs more without one.</summary>
    class FarmingLadder : Scenario
    {
        public override string Id => "farming.ladder";
        public override float Timeout => 200f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(26f, 0f, out var pad, out var dir)) { c.Block("no open pad"); yield break; }
            var side = Vector3.Cross(Vector3.up, dir);
            // a clear lane off the road: four 2 m cells wide and 16 m long, every cell tillable, level and free of
            // props (which only stream in near the player: walk over, let them load, look again)
            var field = pad + side * 14f; bool lane = false;
            int first = 0;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                lane = false;
                for (int k = first; k < 200 && !lane; k++)                                            // off any paving (the start yard is paved): both sides, further out
                {
                    var start = pad + side * ((k & 1) == 0 ? 1f : -1f) * (12f + (k / 2) * 3f);
                    lane = true;
                    for (float t = 0f; t <= 16f && lane; t += 2f)
                        foreach (float o in new[] { -3f, -1f, 1f, 3f })                                // the sprayer's booms reach 2.1 m out
                            if (!Fields.CanTill(start + dir * t + side * o, out _) || Flat(start + dir * t + side * o) < 0.99f) { lane = false; break; }   // level: the tractor crawls up soft slopes
                    if (lane && !Clear(start - dir * 4f, dir, 24f, 6f)) lane = false;              // rocks and props the implements would catch on
                    if (lane) { field = start; first = k + 1; }
                }
                if (!lane) break;
                var look = field + side * 5f; look.y = MadMax.World.DeformableTerrain.Instance.Height(look.x, look.z) + 0.3f;
                g.Player.Teleport(look, 0f);
                yield return new WaitForSeconds(2.5f);                                               // the props there spawn
                if (Clear(field - dir * 4f, dir, 24f, 6f)) break;
                lane = false;
            }
            if (!lane) c.Note("no fully clear lane found; using the nearest");
            // ---- by hand: hoe, then sow
            var terrain = MadMax.World.DeformableTerrain.Instance;
            Vector3 hoePos = field - dir * 12f; string why = null;
            for (int k = 0; k < 12; k++)
            {
                var tryAt = field - dir * 12f + side * (k * 2.5f);
                if (Fields.CanTill(tryAt + dir * 1.2f, out why)) { hoePos = tryAt; why = null; break; }
            }
            if (why != null) c.Note("no tillable ground near the pad: " + why);
            hoePos.y = terrain.Height(hoePos.x, hoePos.z) + 0.2f;
            g.Player.Teleport(hoePos, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(0.6f);
            Fields.CanTill(g.Player.transform.position + g.Player.transform.forward * 1.2f, out why);
            c.Note("ground in front of the player: " + (why ?? "tillable") + $", stamina {g.Vitals.Stamina:0}");
            c.Fixture("on foot at a field site beside the road");
            var hoe = ToolLibrary.Create("tool_hoe", g.propMaterial);
            g.Player.Equip(hoe);
            int before = GardenPlot.All.Count;
            hoe.Strike(g.Player);
            yield return null;
            var bed = Fields.BedAt(g.Player.transform.position + g.Player.transform.forward * 1.2f);
            c.Check(GardenPlot.All.Count == before + 1 && bed, "the hoe tills a 2 m field bed on open ground");
            if (g.Inventory.GetItem("seed_corn") <= 0) { g.Inventory.AddItem("seed_corn", 2); c.Fixture("2 corn seeds"); }
            if (bed) { bed.Use(g, false); c.Check(bed.crop != null, "sown by hand: " + bed.crop); }

            // ---- the tractor
            var prefab = g.vehiclePrefabs.FirstOrDefault(p => p && p.name == "Tractor");
            if (!c.Check(prefab, "the tractor exists")) yield break;
            var headland = field + dir * 1f;                                                     // the implement trails ~3.5 m behind the tractor's middle
            var tr = Object.Instantiate(prefab, headland + Vector3.up * 1.5f, Quaternion.LookRotation(dir)).GetComponent<VehicleDriver>();
            yield return TestWorld.Place(c, tr, headland, dir, 1.2f);
            var m = tr.GetComponent<Machine>();
            var tool = tr.GetComponentsInChildren<MountSocket>().FirstOrDefault(s => s.name == "tool");
            c.Check(m && m.kind == Machine.Kind.Tractor && tool && tool.Current && tool.Current.partId == "tool_plough", "a tractor with a plough on its hitch");
            g.Enter(tr); yield return new WaitForSeconds(0.4f);
            yield return TestWorld.StartEngine(c, tr);
            if (!c.Check(tr.GetComponent<VehicleSystems>().Started, "the tractor runs")) yield break;
            c.Screenshot("tractor");
            yield return null;

            int beds0 = GardenPlot.All.Count;
            yield return Pass(c, tr, headland, dir);
            int ploughed = GardenPlot.All.Count - beds0;
            c.Metric("ploughed_beds", ploughed, "");
            c.Check(ploughed >= 8, $"the lowered plough tills a strip ({ploughed} beds)");
            var strip = GardenPlot.All.Where(p => p && p.GetComponent<Placeable>() && p.GetComponent<Placeable>().id == "field_bed" && p != bed).ToList();

            // ---- seeder, sprayer, harvester over the same strip
            yield return Swap(c, g, tr, tool, "tool_seeder");
            m.Store.inventory.AddItem("seed_wheat", 40);
            c.Fixture("40 wheat seeds in the hopper");
            yield return Pass(c, tr, headland, dir);
            int sown = strip.Count(p => p && p.crop == "seed_wheat");
            c.Metric("sown_beds", sown, "");
            c.Check(sown >= ploughed * 0.6f, $"the seeder sows the strip from the hopper ({sown} of {ploughed})");

            yield return Swap(c, g, tr, tool, "tool_sprayer");
            foreach (var p in strip) if (p) p.water = 0f;
            m.Store.inventory.Add(ResourceType.Water, 60);
            c.Fixture("60 L water in the tank; the strip dried out");
            yield return Pass(c, tr, headland, dir);
            int wet = strip.Count(p => p && p.crop != null && p.water > 0.5f);
            c.Metric("sprayed_beds", wet, "");
            c.Check(wet >= sown * 0.6f, $"the sprayer waters the sown beds ({wet} of {sown})");

            yield return Swap(c, g, tr, tool, "tool_harvester");
            foreach (var p in strip) if (p && p.crop != null) { p.growth = 1f; p.health = 1f; }
            c.Fixture("the crop grown on (time skipped)");
            int grain0 = m.Store.inventory.GetItem("crop_wheat");
            yield return Pass(c, tr, headland, dir);
            int grain = m.Store.inventory.GetItem("crop_wheat") - grain0;
            int reaped = strip.Count(p => p && p.crop == null);
            c.Metric("harvested_wheat", grain, "");
            c.Check(grain > 0 && reaped >= sown * 0.6f, $"the harvester reaps into the grain bin ({grain} wheat, {reaped} beds cleared)");
            c.Screenshot("harvested");
            yield return null;
            tr.throttleInput = 0f; tr.brakeInput = 1f;

            // ---- irrigation timer
            var tank = FurnitureLibrary.Spawn("water_tank", g.Build.Structures, pad - side * 12f, Quaternion.identity, g.propMaterial);
            var timer = FurnitureLibrary.Spawn("irrigation_timer", g.Build.Structures, pad - side * 12f + dir * 2f, Quaternion.identity, g.propMaterial);
            yield return null;
            var tankNode = tank.GetComponent<UtilityNode>(); var timerNode = timer.GetComponent<UtilityNode>();
            float noon = MadMax.World.DayNight.Hours;
            MadMax.World.DayNight.SetHours(12f);
            float free = IrrigationTimer.Cost(tankNode, false);
            timerNode.Link(tankNode, UtilityKind.Water);
            yield return new WaitForSeconds(1.2f);
            float held = IrrigationTimer.Cost(tankNode, false), dry = IrrigationTimer.Cost(tankNode, true);
            MadMax.World.DayNight.SetHours(6f);
            float dawn = IrrigationTimer.Cost(tankNode, false);
            MadMax.World.DayNight.SetHours(noon);
            c.Check(free > 1.3f, $"midday watering without a timer loses water to the heat (x{free:0.0})");
            c.Check(held == 0f && dry == 1f && dawn == 1f, $"a timer holds its network till dawn, except for a bone-dry bed (noon {held}, dry {dry}, dawn {dawn})");
        }

        /// <summary>One pass down the lane: back to the headland (a disclosed fixture: the driver's turn), implement
        /// lowered, 16 m straight at a walking pace.</summary>
        /// <summary>Nothing but terrain and triggers in a 2.5 m high box over the strip (<paramref name="width"/> m wide).</summary>
        static bool Clear(Vector3 from, Vector3 dir, float length, float width)
        {
            var t = MadMax.World.DeformableTerrain.Instance;
            var mid = from + dir * (length * 0.5f); mid.y = t.Height(mid.x, mid.z) + 1.35f;
            int terrain = LayerMask.NameToLayer("Terrain");
            foreach (var col in Physics.OverlapBox(mid, new Vector3(width * 0.5f, 1.2f, length * 0.5f), Quaternion.LookRotation(dir), ~0, QueryTriggerInteraction.Ignore))
                if (col.gameObject.layer != terrain && !col.GetComponentInParent<VehicleDriver>() && !col.GetComponentInParent<PlayerCharacter>()) return false;
            return true;
        }

        static float Flat(Vector3 p) => MadMax.World.DeformableTerrain.Instance.Normal(p.x, p.z).y;

        static IEnumerator Pass(ScenarioContext c, VehicleDriver tr, Vector3 headland, Vector3 dir)
        {
            var m = tr.GetComponent<Machine>(); int w0 = m.Worked;
            yield return TestWorld.Place(c, tr, headland, dir, 0.8f);
            var p0 = tr.transform.position;
            WastelandGame.MachineInput = new MachineKeys { up = -1f };
            yield return new WaitForSeconds(1f);
            WastelandGame.MachineInput = default;
            tr.handbrake = false;
            float t0 = Time.time;
            while (Vector3.Distance(p0, tr.transform.position) < 16f && Time.time - t0 < 16f)
            {
                tr.steerInput = Mathf.Clamp(Vector3.SignedAngle(tr.transform.forward, dir, Vector3.up) / 20f, -1f, 1f);
                tr.throttleInput = tr.ForwardSpeed < 2.6f ? 0.7f : 0f;
                tr.brakeInput = 0f;
                yield return null;
            }
            var touching = new System.Collections.Generic.HashSet<string>();
            foreach (var col in tr.GetComponentsInChildren<MountSocket>().Where(k => k.name == "tool" && k.Current).SelectMany(k => k.Current.GetComponentsInChildren<Collider>()))
            {
                if (!col.enabled) continue;
                var b = col.bounds;
                foreach (var o in Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    if (!o.transform.IsChildOf(tr.transform) && o.gameObject.layer != LayerMask.NameToLayer("Terrain")) touching.Add(o.name + "@" + LayerMask.LayerToName(o.gameObject.layer) + " vs " + col.name + "@" + LayerMask.LayerToName(col.gameObject.layer));
            }
            c.Note("implement overlaps: " + (touching.Count == 0 ? "none" : string.Join("; ", touching)) + $"; body mass {tr.Body.mass:0} kg");
            var gs = MadMax.World.DeformableTerrain.Instance.SurfaceAt(p0.x, p0.z);
            c.Note($"pass: moved {Vector3.Distance(p0, tr.transform.position):0.0} m in {Time.time - t0:0.0} s, worked {m.Worked - w0}, {m.Status} (ground: level {Flat(p0):0.000}, soft {gs.softness:0.00}, mud {gs.mud:0.00}; {TestWorld.Contacts(tr)}; {TestWorld.State(tr)})");
            tr.throttleInput = 0f; tr.brakeInput = 1f; tr.steerInput = 0f;
            yield return new WaitForSeconds(0.6f);
            tr.brakeInput = 0f;
        }

        static IEnumerator Swap(ScenarioContext c, WastelandGame g, VehicleDriver tr, MountSocket tool, string id)
        {
            var old = tool.Detach(false);
            if (old) Object.Destroy(old.gameObject);
            var part = g.SpawnPart(id, tool.transform.position, tool.transform.rotation);
            bool ok = part && tool.Attach(part);
            c.Check(ok, "hitched the " + id.Substring(5));
            c.Fixture("swapped the implement for the " + id.Substring(5) + " (as with the wrench)");
            yield return new WaitForSeconds(0.5f);
        }
    }
}
