using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the lights block: wall switches on a cabled circuit and in a room, a town house's
    /// door switch at night, a walk-in vehicle's cabin lights on its house battery, town street lamps by day and night
    /// and a smashed lamp staying dark.</summary>
    public static class LightsScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new LightsSwitch();
            yield return new LightsBuilding();
            yield return new LightsVehicleInterior();
            yield return new LightsStreet();
        }

        internal static IEnumerator Until(System.Func<bool> ok, float seconds)
        {
            float t0 = Time.time;
            while (!ok() && Time.time - t0 < seconds) yield return null;
        }

        /// <summary>Sets the clock and waits until the darkness has followed it.</summary>
        internal static IEnumerator Clock(ScenarioContext c, float hours)
        {
            DayNight.SetHours(hours);
            c.Fixture($"clock set to {hours:00.00} h");
            yield return null; yield return null;
            yield return Until(() => (DayNight.Darkness > 0.3f) == (hours >= 20f || hours < 5f), 3f);
        }

        /// <summary>Settlements of the given kinds, nearest the player first.</summary>
        internal static List<Settlement> Towns(WastelandGame g, params Biome[] kinds)
        {
            var at = g.Player.transform.position;
            var l = new List<Settlement>();
            foreach (var st in g.World.settlements) if (System.Array.IndexOf(kinds, st.kind) >= 0) l.Add(st);
            l.Sort((a, b) => (a.pos - new Vector2(at.x, at.z)).sqrMagnitude.CompareTo((b.pos - new Vector2(at.x, at.z)).sqrMagnitude));
            return l;
        }

        internal static void Go(WastelandGame g, Vector2 p, float yaw)
        {
            var t = DeformableTerrain.Instance;
            g.Player.Teleport(new Vector3(p.x, t.Height(p.x, p.y) + 0.4f, p.y), yaw);
        }
    }

    /// <summary>A generator, two wall lamps and two switches on a pad: switch A is cabled to lamp 1 (its circuit by
    /// cable), switch B has no cable and runs lamp 2 in its room (but not lamp 1, which A's cable claims). Each switch
    /// turns only its own lamp off and on; the switch state survives a save / load of the piece.</summary>
    class LightsSwitch : Scenario
    {
        public override string Id => "lights.switch";

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(9f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -4f), 0f);
            yield return new WaitForSeconds(0.3f);
            c.Fixture("player on a clear pad; pieces spawned directly (no build costs), cables linked in code");
            Vector3 P(float x, float z) { var q = pad + new Vector3(x, 0f, z); q.y = t.Height(q.x, q.z); return q; }
            Placeable Put(string id, float x, float z) => FurnitureLibrary.Spawn(id, g.Build.Structures, P(x, z), Quaternion.identity, g.propMaterial);

            var gen = Put("generator", -3f, 2f);
            var lamp1 = Put("light_wall", 0f, 2f); var lamp2 = Put("light_wall", 2.5f, 2f);
            var swA = Put("light_switch", -1f, 0f); var swB = Put("light_switch", 3.5f, 0.5f);
            yield return null;
            if (!c.Check(gen && lamp1 && lamp2 && swA && swB, "generator, two wall lamps and two light switches spawn")) yield break;
            var gc = gen.GetComponent<Generator>(); var gn = gen.GetComponent<UtilityNode>();
            gc.fuel = 20f; gc.on = true;
            c.Fixture("generator fuelled (20 L) and running");
            var l1 = lamp1.GetComponent<PoweredLight>(); var l2 = lamp2.GetComponent<PoweredLight>();
            var a = swA.GetComponent<LightSwitch>(); var b = swB.GetComponent<LightSwitch>();
            lamp1.GetComponent<UtilityNode>().Link(gn, UtilityKind.Power);
            lamp2.GetComponent<UtilityNode>().Link(gn, UtilityKind.Power);
            swA.GetComponent<UtilityNode>().Link(lamp1.GetComponent<UtilityNode>(), UtilityKind.Power);
            c.Fixture("cables: generator-lamp 1, generator-lamp 2, switch A-lamp 1; switch B uncabled");
            yield return LightsScenarios.Clock(c, 22f);
            a.Resolve(); b.Resolve();
            yield return LightsScenarios.Until(() => l1.Glowing && l2.Glowing, 4f);
            c.Check(l1.Glowing && l2.Glowing, "both lamps burn at night on the running generator");
            c.Check(a.Wired && Has(a, l1) && !Has(a, l2), $"switch A's circuit is its cable: lamp 1 only ({a.Circuit.Count} lights, wired {a.Wired})");
            c.Check(!b.Wired && Has(b, l2) && !Has(b, l1), $"switch B's circuit is its room: lamp 2, not the cabled lamp 1 ({b.Circuit.Count} lights)");

            a.Flip();
            yield return LightsScenarios.Until(() => !l1.Glowing, 2f);
            c.Check(!a.on && !l1.Glowing && l2.Glowing, "switch A off: lamp 1 dark, lamp 2 still burning");
            var lever = swA.transform.Find("Lever");
            c.Check(lever && lever.localRotation.eulerAngles.x > 180f, "switch A's lever is down");
            a.Flip();
            yield return LightsScenarios.Until(() => l1.Glowing, 2f);
            c.Check(a.on && l1.Glowing, "switch A on again: lamp 1 burns");
            b.Flip();
            yield return LightsScenarios.Until(() => !l2.Glowing, 2f);
            c.Check(!l2.Glowing && l1.Glowing, "switch B off: lamp 2 dark, lamp 1 untouched");
            c.Check(l2.Prompt(g).Contains("WALL SWITCH"), "the lamp's own prompt defers to the wall switch: " + l2.Prompt(g));

            // saved: the switch state round-trips through the piece state
            string state = swB.SaveState();
            var copy = Put("light_switch", 5f, -2f);
            yield return null;
            copy.LoadState(state);
            var cs = copy.GetComponent<LightSwitch>();
            c.Check(cs && !cs.on, "switch B's OFF state survives save and load of the piece (state '" + state.Replace('\u001e', '|') + "')");
            b.Flip();
            yield return LightsScenarios.Until(() => l2.Glowing, 2f);
            c.Check(l2.Glowing, "switch B on again: lamp 2 burns");
            foreach (var p in new[] { gen, lamp1, lamp2, swA, swB, copy }) if (p) Object.Destroy(p.gameObject);
        }

        static bool Has(LightSwitch s, PoweredLight l) { foreach (var x in s.Circuit) if (x == l) return true; return false; }
    }

    /// <summary>The nearest village or town house (farmhouse, brick house or shack) has its lights and a switch inside
    /// within 2 m of its front door. At night the residents have the lights on (town grid); the switch turns the interior
    /// light off and on; by day the residents switch it off.</summary>
    class LightsBuilding : Scenario
    {
        public override string Id => "lights.building";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var list = new List<(string id, Vector2 pos, float yaw)>();
            Vector2 house = default; float yaw = 0f; string hid = null; int town = -1;
            foreach (var st in LightsScenarios.Towns(g, Biome.Village, Biome.Town))
            {
                BiomeProps.Buildings(g.World, st, list);
                foreach (var b in list)
                    if (b.id.StartsWith("Farmhouse") || b.id.StartsWith("BrickHouse") || b.id.StartsWith("Shack")) { house = b.pos; yaw = b.yaw; hid = b.id; town = st.index; break; }
                if (hid != null) break;
            }
            if (hid == null) { c.Block("no village or town house in this world"); yield break; }
            var front = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            LightsScenarios.Go(g, house + new Vector2(front.x, front.z) * 7f, yaw);
            c.Fixture($"player teleported in front of {hid} in settlement {town} ({house.x:0},{house.y:0})");
            BuildingLights bl = null;
            yield return LightsScenarios.Until(() =>
            {
                foreach (var x in BuildingLights.All) if (x && (new Vector2(x.transform.position.x, x.transform.position.z) - house).sqrMagnitude < 0.25f) { bl = x; return true; }
                return false;
            }, 40f);
            if (!c.Check(bl, "the house streams in with its lights")) yield break;
            var sw = bl.lightSwitch;
            if (!c.Check(sw && bl.lights.Count > 0, $"it has {bl.lights.Count} light(s) and a switch")) yield break;
            c.Check(!bl.ruin && bl.Powered, "an inhabited house on the town grid");

            // by the door: the front wall is the template's lowest z
            var dv = bl.GetComponent<DestructibleVoxels>();
            int minZ = int.MaxValue;
            foreach (var k in dv.Grid.voxels.Keys) if (k.y > 2 && k.y < 13 && Mathf.Abs(k.x) <= 8 && k.z < minZ) minZ = k.z;     // the front wall beside the door
            var local = bl.transform.InverseTransformPoint(sw.transform.position);
            float fromDoor = new Vector2(local.x, local.z - minZ * dv.voxelSize).magnitude;
            c.Metric("switch_from_door", fromDoor, "m");
            c.Check(fromDoor < 2f && local.y > 0.9f && local.y < 1.6f, $"the switch is inside by the front door ({fromDoor:0.00} m from it, {local.y:0.00} m up)");
            c.Check(sw.Prompt(g).Contains("LIGHT SWITCH"), "[E] prompt: " + sw.Prompt(g));

            var lamp = bl.lights[0];
            yield return LightsScenarios.Clock(c, 21f);
            yield return LightsScenarios.Until(() => sw.on && lamp.Glowing, 4f);
            c.Check(sw.on && lamp.Glowing, "at dusk the residents have the light on");
            sw.Use(g, false);
            yield return LightsScenarios.Until(() => !lamp.Glowing, 2f);
            c.Check(!sw.on && !lamp.Glowing, "[E] on the switch: the interior light goes out");
            yield return new WaitForSeconds(1.5f);
            c.Check(!sw.on, "the player's choice holds (residents only change it at dusk / bedtime)");
            sw.Use(g, false);
            yield return LightsScenarios.Until(() => lamp.Glowing, 2f);
            c.Check(sw.on && lamp.Glowing, "[E] again: the light is back on");
            yield return LightsScenarios.Clock(c, 12f);
            yield return LightsScenarios.Until(() => !sw.on && !lamp.Glowing, 4f);
            c.Check(!sw.on && !lamp.Glowing, "by day the residents switch it off");
        }
    }

    /// <summary>A walk-in vehicle (Hauler, else the Bus) comes with cabin strip lights, a switch by its main door (off)
    /// and a house battery; the switch runs every cabin light from the battery, which drains while they burn; with the
    /// battery flat and the engine off they stay dark.</summary>
    class LightsVehicleInterior : Scenario
    {
        public override string Id => "lights.vehicle_interior";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(9f, 14f, out var pad, out var dir)) { c.Block("no open pad"); yield break; }
            VehicleDriver v = null;
            foreach (var design in new[] { "Hauler", "Bus", "Ambulance" }) { v = g.SpawnFound(design, pad + Vector3.up * 1.2f, Quaternion.LookRotation(dir)); if (v) break; }
            if (!c.Check(v, "a walk-in vehicle spawns")) yield break;
            c.Fixture($"spawned a found {v.name.Replace("(Clone)", "")} on a pad (new: cabin furnished)");
            yield return TestWorld.Place(c, v, pad, dir);
            var space = v.GetComponent<InteriorSpace>();
            if (!c.Check(space, "it has a walk-in interior")) yield break;
            var power = v.GetComponentInChildren<CabinPower>();
            var sw = v.GetComponentInChildren<LightSwitch>();
            var lights = new List<PoweredLight>();
            foreach (var l in v.GetComponentsInChildren<PoweredLight>()) if (l.Switchable && l.GetComponent<Placeable>() && l.GetComponent<Placeable>().id == "light_strip") lights.Add(l);
            if (!c.Check(power && power.Node && power.Node.batteryWh > 0f, "a house battery on the vehicle's bus")) yield break;
            if (!c.Check(sw && lights.Count > 0, $"{lights.Count} cabin strip light(s) and a light switch")) yield break;
            var door = space.transform.TransformPoint(space.doors[0].inside);
            float d = Vector3.Distance(new Vector3(door.x, 0f, door.z), new Vector3(sw.transform.position.x, 0f, sw.transform.position.z));
            c.Metric("switch_from_door", d, "m");
            c.Check(d < 1.5f, $"the switch is beside the main door ({d:0.00} m)");
            yield return LightsScenarios.Clock(c, 22f);
            sw.Resolve();
            bool AllLit() { foreach (var l in lights) if (!l.Glowing) return false; return true; }
            bool NoneLit() { foreach (var l in lights) if (l.Glowing) return false; return true; }
            c.Check(!sw.on && NoneLit(), "the cabin switch starts off: the lights are dark");
            int claimed = 0; foreach (var l in lights) if (l.Master == sw) claimed++;
            c.Check(claimed == lights.Count, $"the switch runs every cabin light ({claimed}/{lights.Count})");

            var sys = v.GetComponent<VehicleSystems>();
            c.Check(!sys || !sys.Started, "engine off (lights on the battery alone)");
            float charge0 = power.Node.batteryCharge;
            sw.Use(g, false);
            yield return LightsScenarios.Until(AllLit, 3f);
            c.Check(sw.on && AllLit(), "switched on: every cabin light burns from the battery");
            yield return new WaitForSeconds(2.5f);
            float used = charge0 - power.Node.batteryCharge;
            c.Metric("battery_used", used, "Wh");
            c.Check(used > 0f, $"the battery drains while they burn ({used:0.000} Wh in 2.5 s)");
            sw.Use(g, false);
            yield return LightsScenarios.Until(NoneLit, 2f);
            c.Check(!sw.on && NoneLit(), "switched off: dark again");

            float keep = power.Node.batteryCharge;
            power.Node.batteryCharge = 0f;
            c.Fixture("house battery emptied (engine off)");
            sw.Use(g, false);
            yield return new WaitForSeconds(1.5f);
            c.Check(NoneLit(), "flat battery: switched on but the lights stay dark");
            c.Check(sw.Prompt(g).Contains("NO POWER"), "the switch says so: " + sw.Prompt(g));
            power.Node.batteryCharge = keep;
            yield return LightsScenarios.Until(AllLit, 2f);
            c.Check(AllLit(), "battery back: the lights come on");
            Object.Destroy(v.gameObject);
        }
    }

    /// <summary>Street lamps in the nearest village or town: dark by day, burning at night on the town grid; a lamp
    /// whose head is shot stays dark (also when it streams in again), and the smashed key is in the save.</summary>
    class LightsStreet : Scenario
    {
        public override string Id => "lights.street";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var spots = TownLights.Spots(g.World);
            c.Metric("street_lamps_world", spots.Count, "");
            var towns = LightsScenarios.Towns(g, Biome.Village, Biome.Town, Biome.City);
            TownLights.Spot spot = default; bool found = false;
            foreach (var st in towns)
            {
                float best = float.MaxValue;
                foreach (var s in spots) if (s.town == st.index && s.piece != "floodlight_mast") { float dd = (s.pos - st.pos).sqrMagnitude; if (dd < best) { best = dd; spot = s; found = true; } }
                if (found) break;
            }
            if (!c.Check(found, "the settlements have planned street lamps")) yield break;
            LightsScenarios.Go(g, spot.pos + new Vector2(2.5f, 2.5f), 0f);
            c.Fixture($"player teleported beside a {spot.piece} in settlement {spot.town} ({spot.pos.x:0},{spot.pos.y:0})");
            WorldLamp lamp = null;
            yield return LightsScenarios.Until(() => { lamp = Find(spot.key); return lamp; }, 40f);
            if (!c.Check(lamp && lamp.Lamp, "the lamp streams in with its chunk")) yield break;
            var others = new List<WorldLamp>();
            foreach (var w in Object.FindObjectsByType<WorldLamp>(FindObjectsSortMode.None))
                if (w != lamp && w.Lamp && !w.Lamp.broken && (w.transform.position - lamp.transform.position).sqrMagnitude < 80f * 80f) others.Add(w);
            c.Metric("lamps_nearby", others.Count + 1, "");

            yield return LightsScenarios.Clock(c, 12f);
            yield return null;
            int litDay = 0; foreach (var w in others) if (w.Lamp.Glowing) litDay++;
            c.Check(!lamp.Lamp.Glowing && litDay == 0, $"by day the street lamps are off ({litDay} of {others.Count} others lit)");
            yield return LightsScenarios.Clock(c, 22f);
            yield return LightsScenarios.Until(() => lamp.Lamp.Glowing, 3f);
            int litNight = 0; foreach (var w in others) if (w.Lamp.Glowing) litNight++;
            c.Metric("lamps_lit_night", litNight + (lamp.Lamp.Glowing ? 1 : 0), "");
            c.Check(lamp.Lamp.Glowing && litNight == others.Count, $"at night they burn on the town grid ({litNight + 1} of {others.Count + 1})");

            var head = lamp.GetComponentInChildren<LampHead>();
            if (!c.Check(head, "the lamp has a smashable head")) yield break;
            var hp = head.GetComponent<Collider>().bounds.center;
            head.ApplyHit(hp, Vector3.down, 0.3f, 0.1f, g.Player.gameObject);
            c.Fixture("a shot (0.3 power) applied to the lamp head");
            yield return LightsScenarios.Until(() => !lamp.Lamp.Glowing, 1f);
            c.Check(lamp.Lamp.broken && !lamp.Lamp.Glowing, "the smashed lamp goes dark");
            yield return new WaitForSeconds(2.5f);
            c.Check(!lamp.Lamp.Glowing, "and stays dark at night");
            var saved = new List<string>();
            TownLights.Save(saved);
            c.Check(saved.Contains("x:" + spot.key), "the smashed lamp is in the save (" + saved.Count + " entries)");

            // streaming back in: a fresh lamp with the same key starts smashed
            var again = FurnitureLibrary.WorldFixture(spot.piece, null, lamp.transform.position + Vector3.right * 40f, lamp.transform.rotation, DeformableTerrain.Instance.worldPropMaterial);
            var wl = again ? again.gameObject.AddComponent<WorldLamp>() : null;
            if (wl) { wl.key = spot.key; wl.town = spot.town; }
            c.Fixture("a second copy of the lamp spawned with the same key (as when its chunk streams back in)");
            yield return null; yield return null;
            c.Check(again && again.broken && !again.Glowing, "it comes back smashed and dark");
            if (again) Object.Destroy(again.gameObject);
            yield return LightsScenarios.Clock(c, 12f);
        }

        static WorldLamp Find(string key)
        {
            foreach (var w in Object.FindObjectsByType<WorldLamp>(FindObjectsSortMode.None)) if (w && w.key == key) return w;
            return null;
        }
    }
}
