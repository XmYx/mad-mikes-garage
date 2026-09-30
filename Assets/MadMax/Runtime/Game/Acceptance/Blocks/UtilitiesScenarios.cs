using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the Utilities block.</summary>
    public static class UtilitiesScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new UtilitiesLadder();
        }
    }

    /// <summary>Depth stage F end to end on one pad: a desalinator on a powered net turns carried sea water into water
    /// and salt and cleans a tank of brine piped to it; the test kit calls a well fouled by a latrine FOUL and the same
    /// water CLEAN once a filter is on the pipe (and a well on an oil field OILY); a switch cuts a lamp and restores it,
    /// a timer and a light sensor follow the clock; with too little supply a LOW load behind a breaker sheds while the
    /// ESSENTIAL one stays on, a NORMAL overload stalls the generator (outage) and a restart brings the essential load
    /// back; a digester fed manure makes biogas that runs a biogas generator; meat rots in an unpowered fridge and keeps
    /// in a powered one (spoilage advanced directly, disclosed); a powered freezer stops rot.</summary>
    class UtilitiesLadder : Scenario
    {
        public override string Id => "utilities.ladder";
        public override float Timeout => 220f;

        static IEnumerator Until(System.Func<bool> ok, float seconds)
        {
            float t0 = Time.time;
            while (!ok() && Time.time - t0 < seconds) yield return null;
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(14f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + Vector3.up * 0.3f, 0f);
            yield return new WaitForSeconds(0.3f);
            c.Fixture("player on a clear pad; every piece spawned there directly (no build costs), cables and pipes linked in code");
            Vector3 P(float x, float z) { var q = pad + new Vector3(x, 0f, z); q.y = t.Height(q.x, q.z); return q; }
            Placeable Put(string id, float x, float z) => FurnitureLibrary.Spawn(id, g.Build.Structures, P(x, z), Quaternion.identity, g.propMaterial);

            // ---- a steam generator for the powered pieces; the fridges start chilling now
            var gen = Put("coal_generator", -6f, -6f);
            yield return null;
            var genNode = gen ? gen.GetComponent<UtilityNode>() : null;
            var genComp = gen ? gen.GetComponent<Generator>() : null;
            if (!c.Check(genNode && genComp, "steam generator spawns")) yield break;
            genComp.fuel = 60f; genComp.on = true;
            c.Fixture("fuelled steam generator (2.5 kW), switched on");
            var fridgeOn = Put("fridge", -9f, 3f); var fridgeOff = Put("fridge", -9f, 7f); var freezer = Put("freezer", -12f, 3f);
            yield return null;
            var coldOn = fridgeOn ? fridgeOn.GetComponent<ColdStore>() : null; var coldOff = fridgeOff ? fridgeOff.GetComponent<ColdStore>() : null;
            var coldFz = freezer ? freezer.GetComponent<ColdStore>() : null;
            if (!c.Check(coldOn && coldOff && coldFz, "fridges and freezer carry a temperature model")) yield break;
            fridgeOn.GetComponent<UtilityNode>().Link(genNode, UtilityKind.Power);
            freezer.GetComponent<UtilityNode>().Link(genNode, UtilityKind.Power);
            fridgeOn.GetComponent<Container>().inventory.AddItem("food_meat_raw", 2);
            fridgeOff.GetComponent<Container>().inventory.AddItem("food_meat_raw", 2);
            freezer.GetComponent<Container>().inventory.AddItem("food_meat_raw", 2);
            c.Fixture("2 raw meat in each of a powered fridge, an unpowered fridge and a powered freezer");

            // ---- desalinator: carried sea water through the station, then a tank of brine through its membranes
            var desal = Put("desalinator", -3f, -6f);
            yield return null;
            var dNode = desal ? desal.GetComponent<UtilityNode>() : null;
            var dSt = desal ? desal.GetComponent<CraftingStation>() : null;
            if (c.Check(dNode && dSt && dSt.type == "desalinator" && desal.GetComponent<Desalinator>(), "the desalinator is a crafting station with a network role"))
            {
                dNode.Link(genNode, UtilityKind.Power);
                yield return Until(() => dSt.Powered, 3f);
                c.Check(dSt.Powered, "desalinator has power");
                var r = RecipeLibrary.Get("desal_sea");
                if (c.Check(r != null, "recipe desal_sea exists"))
                {
                    g.Inventory.Add(ResourceType.SeaWater, RecipeLibrary.Amount(10));
                    c.Fixture("10 L of sea water in the pack");
                    int w0 = g.Inventory.Get(ResourceType.Water) + dSt.tray.Get(ResourceType.Water), s0 = g.Inventory.Get(ResourceType.Salt) + dSt.tray.Get(ResourceType.Salt);
                    string why = g.CraftBlockReason(r, dSt);
                    if (c.Check(why == null, "desalinating is craftable" + (why != null ? ": " + why : "")))
                    {
                        g.Craft(r, dSt);
                        if (dSt.queue.Count > 0) dSt.queue[0].progress = 0.9f;
                        c.Fixture("desalinator job advanced to 90 %");
                        yield return Until(() => dSt.queue.Count == 0, 10f);
                        yield return new WaitForSeconds(0.2f);
                        int dw = g.Inventory.Get(ResourceType.Water) + dSt.tray.Get(ResourceType.Water) - w0, ds = g.Inventory.Get(ResourceType.Salt) + dSt.tray.Get(ResourceType.Salt) - s0;
                        c.Metric("desal_water", dw, "L"); c.Metric("desal_salt", ds, "");
                        c.Check(dw == 9 && ds == 1, $"10 L of sea water made {dw} L of water and {ds} salt");
                    }
                }
                var tank = Put("water_tank", -3f, -10f);
                yield return null;
                var tNode = tank.GetComponent<UtilityNode>();
                tNode.Link(dNode, UtilityKind.Water);
                tNode.dirty = 30f; tNode.taint = WaterTaint.Salt;
                c.Fixture("30 L of sea water poured straight into a water tank piped to the desalinator");
                yield return new WaitForSeconds(1.1f);
                int salt0 = dSt.tray.Get(ResourceType.Salt);
                c.Check(WaterQuality.Reading(tNode).Contains("SALTY"), "the kit calls the tank SALTY: " + WaterQuality.Reading(tNode));
                yield return Until(() => dSt.tray.Get(ResourceType.Salt) > salt0, 30f);
                UtilityGrid.NetWater(tNode, out float cleanShare);
                c.Metric("brine_clean_share", cleanShare, "");
                c.Check(dSt.tray.Get(ResourceType.Salt) > salt0 && cleanShare > 0.25f, $"piped brine comes out sweet ({cleanShare:P0} clean) and leaves salt in the tray");
                dNode.Unlink();
            }

            // ---- water quality: a well fouled by a latrine, cleaned by a filter; a well on an oil field
            var well = Put("well", 5f, -6f); var latrine = Put("latrine", 9f, -6f);
            yield return null;
            var wellNode = well.GetComponent<UtilityNode>();
            latrine.GetComponent<Latrine>().fresh = 60f;
            c.Fixture("a used latrine (60 units in the pit) 4 m from a new well");
            c.Check((WaterQuality.GroundTaint(well.transform.position) & WaterTaint.Sewage) != 0, "the latrine fouls the ground water under the well");
            var pump = well.GetComponent<HandPump>();
            for (int i = 0; i < 3; i++) { pump.Use(g, false); g.Stats.stamina = g.Stats.MaxStamina; }
            c.Fixture("worked the well's hand pump three times");
            g.Player.Teleport(well.transform.position + new Vector3(0f, 0.3f, -1.3f), 0f);
            yield return new WaitForSeconds(0.6f);
            g.Inventory.AddItem(UtilityIds.WaterTest);
            c.Fixture("a water test kit in the pack, used from the hotbar at the well");
            g.UseItem(UtilityIds.WaterTest);
            string dirtyReading = g.ToastText ?? "";
            c.Note(dirtyReading);
            c.Check(dirtyReading.Contains("FOUL"), "the kit reports the well FOUL: " + dirtyReading);
            var filter = Put("filter", 5f, -9f);
            yield return null;
            var cart = filter.GetComponent<FilterCartridge>();
            filter.GetComponent<UtilityNode>().Link(wellNode, UtilityKind.Water);
            c.Check(cart, "the filter has a cartridge");
            yield return Until(() => { WaterQuality.Reading(wellNode, out string v, out _); return v == "CLEAN"; }, 30f);
            string cleanReading = g.TestWater() ?? "";
            c.Note(cleanReading);
            c.Check(cleanReading.StartsWith("WATER TEST: CLEAN"), "filtered, the kit reports it CLEAN: " + cleanReading);
            if (cart) { c.Metric("cartridge_life", cart.life, ""); c.Check(cart.life < 1f, "cleaning foul water wore the cartridge"); }
            Vector3 oily = default; bool found = false;
            for (int k = 0; k < 4000 && !found; k++)
            {
                float a = k * 2.39996f, rr = 30f + k * 1.2f;
                var q = pad + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr;
                if (t.World.OilAt(q.x, q.z) > 0.45f && t.World.Habitable(q.x, q.z) && t.World.ContinentNoise(q.x, q.z) > 0.6f) { oily = q; found = true; }
            }
            if (found)
            {
                oily.y = t.Height(oily.x, oily.z);
                var oilWell = FurnitureLibrary.Spawn("well", g.Build.Structures, oily, Quaternion.identity, g.propMaterial);
                yield return null;
                c.Fixture($"a well on an oil field {Vector3.Distance(pad, oily):0} m away (field {t.World.OilAt(oily.x, oily.z):P0}), pumped by hand");
                oilWell.GetComponent<HandPump>().Use(g, false);
                string oilReading = WaterQuality.Reading(oilWell.GetComponent<UtilityNode>());
                c.Note(oilReading);
                c.Check(oilReading.Contains("OILY"), "the kit reports the oil-field well OILY: " + oilReading);
            }
            else c.Note("no oil field within 4.8 km of the pad: the oily well is not checked");

            // ---- power control: switch, timer, light sensor
            var sw = Put("power_switch", -6f, -2f); var lamp = Put("light_ceiling", -6f, 1f);
            var timer = Put("power_timer", -3f, -2f); var sensor = Put("light_sensor", 0f, -2f);
            yield return null;
            var swNode = sw.GetComponent<UtilityNode>(); var lampNode = lamp.GetComponent<UtilityNode>(); var swComp = sw.GetComponent<UtilitySwitch>();
            swNode.Link(genNode, UtilityKind.Power); lampNode.Link(swNode, UtilityKind.Power);
            yield return Until(() => lampNode.Powered, 3f);
            c.Check(lampNode.Powered, "the lamp is powered through the closed switch");
            swComp.Set(false);
            yield return Until(() => !lampNode.Powered, 3f);
            c.Check(!lampNode.Powered, "switched off, the lamp loses power");
            swComp.Set(true);
            yield return Until(() => lampNode.Powered, 3f);
            c.Check(lampNode.Powered, "switched on again, the lamp has power");
            var tm = timer.GetComponent<UtilitySwitch>(); tm.setting = 2;
            yield return new WaitForSeconds(0.7f);
            float h = DayNight.Hours;
            c.Check(tm.Closed == (h >= 7f && h < 19f), $"the timer (07-19) is {(tm.Closed ? "on" : "off")} at {h:0.0} h");
            var ls = sensor.GetComponent<UtilitySwitch>();
            c.Check(ls.Closed == (DayNight.Darkness > 0.25f), $"the light sensor is {(ls.Closed ? "on" : "off")} at darkness {DayNight.Darkness:0.00}");

            // ---- priorities: 1.5 kW for 2.7 kW of load
            var gen2 = Put("generator", 6f, 4f); var bE = Put("breaker", 4f, 6f); var bL = Put("breaker", 8f, 6f);
            var heater = Put("heater", 4f, 9f); var ac = Put("aircon", 8f, 9f);
            yield return null;
            var g2 = gen2.GetComponent<Generator>(); var g2Node = gen2.GetComponent<UtilityNode>();
            var brE = bE.GetComponent<PowerBreaker>(); var brL = bL.GetComponent<PowerBreaker>();
            var heatNode = heater.GetComponent<UtilityNode>(); var acNode = ac.GetComponent<UtilityNode>();
            g2.fuel = 20f; g2.on = true;
            brE.SetPriority(0); brL.SetPriority(2);
            bE.GetComponent<UtilityNode>().Link(g2Node, UtilityKind.Power); heatNode.Link(bE.GetComponent<UtilityNode>(), UtilityKind.Power);
            bL.GetComponent<UtilityNode>().Link(g2Node, UtilityKind.Power); acNode.Link(bL.GetComponent<UtilityNode>(), UtilityKind.Power);
            heater.GetComponent<Climate>().on = true; ac.GetComponent<Climate>().on = true;
            c.Fixture("a 1.5 kW generator: heater (1.5 kW) behind an ESSENTIAL breaker, aircon (1.2 kW) behind a LOW one");
            yield return new WaitForSeconds(Generator.StallAfter + 1.5f);
            c.Check(heatNode.priority == 0 && acNode.priority == 2, $"breakers set the priorities (heater {heatNode.priority}, aircon {acNode.priority})");
            c.Check(heatNode.Powered && !acNode.Powered && acNode.Shed, "short of power, the LOW aircon sheds and the ESSENTIAL heater stays on");
            c.Check(g2.on && !g2.stalled, "no outage while the low load can be shed");
            brL.SetPriority(1);
            c.Fixture("test outage: the aircon's breaker set to NORMAL");
            yield return Until(() => g2.stalled, Generator.StallAfter + 3f);
            c.Check(g2.stalled && !g2.on, "a NORMAL overload stalls the generator");
            yield return new WaitForSeconds(0.7f);
            c.Check(!heatNode.Powered, "the outage takes the essential heater down too");
            ac.GetComponent<Climate>().on = false;
            g2.Toggle();
            c.Fixture("aircon switched off, generator restarted ([E])");
            yield return Until(() => heatNode.Powered, 3f);
            c.Check(heatNode.Powered && g2.on && !g2.stalled, "after the restart the priority load is back on");

            // ---- biogas: manure → digester → gas → generator → lamp
            var dig = Put("biogas_digester", 0f, 8f); var bgen = Put("biogas_generator", 3f, 11f); var lamp2 = Put("light_ceiling", 0f, 12f);
            yield return null;
            var digC = dig.GetComponent<BiogasDigester>(); var bg = bgen.GetComponent<Generator>(); var lamp2Node = lamp2.GetComponent<UtilityNode>();
            g.Inventory.AddItem("farm_manure", 4);
            c.Fixture("4 manure in the pack, fed with [E]");
            digC.Use(g, false);
            c.Check(digC.slurry >= 4 * BiogasDigester.ManureUnits - 0.01f, $"the digester took the manure ({digC.slurry:0} units)");
            digC.Advance(3f);
            c.Fixture("the digester advanced 3 game hours");
            c.Metric("biogas_litres", digC.gas, "L");
            c.Check(digC.gas > 0f, $"digesting made {digC.gas:0} L of biogas");
            bg.on = true;
            lamp2Node.Link(bgen.GetComponent<UtilityNode>(), UtilityKind.Power);
            yield return Until(() => bg.fuel > 0f && lamp2Node.Powered, 4f);
            c.Check(bg.fuel > 0f, "the hose filled the biogas generator from the digester");
            c.Check(bgen.GetComponent<UtilityNode>().produce > 0f && lamp2Node.Powered, "the biogas generator runs and powers a lamp");

            // ---- cold storage
            if (Weather.Temperature < 12f)
            {
                coldOff.temp = 25f;
                c.Fixture($"cold day ({Weather.Temperature:0} °C): the unpowered fridge set to 25 °C inside");
            }
            yield return Until(() => coldOn.Chilled && coldFz.Frozen, 60f);
            c.Metric("fridge_c", coldOn.temp, "C"); c.Metric("freezer_c", coldFz.temp, "C"); c.Metric("unpowered_c", coldOff.temp, "C");
            c.Check(coldOn.Chilled, $"the powered fridge chills ({coldOn.temp:0} °C)");
            c.Check(!coldOff.Chilled, $"the unpowered fridge stays warm ({coldOff.temp:0} °C)");
            c.Check(coldFz.Frozen && coldFz.SpoilFactor == 0f, $"the powered freezer freezes ({coldFz.temp:0} °C): no rot at all");
            var boxOn = fridgeOn.GetComponent<Container>(); var boxOff = fridgeOff.GetComponent<Container>(); var boxFz = freezer.GetComponent<Container>();
            g.SpoilContainer(boxOn, 3000f); g.SpoilContainer(boxOff, 3000f); g.SpoilContainer(boxFz, 3000f);
            c.Fixture("50 minutes of spoilage applied to each (WastelandGame.SpoilContainer; raw meat rots in 20 in the open)");
            c.Check(boxOff.inventory.GetItem("food_rotten") >= 1, $"meat rots in the unpowered fridge ({boxOff.inventory.GetItem("food_rotten")} rotten)");
            c.Check(boxOn.inventory.GetItem("food_rotten") == 0 && boxOn.inventory.GetItem("food_meat_raw") == 2, "and keeps in the powered one");
            c.Check(boxFz.inventory.GetItem("food_meat_raw") == 2, "and in the freezer");

            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (rig) rig.SetTarget(g.Player.transform);
            g.Player.Teleport(pad + new Vector3(0f, 0.3f, -14f), 0f);
            yield return new WaitForSeconds(1.5f);
            c.Screenshot("utilities_ladder");
            yield return null;
        }
    }
}
