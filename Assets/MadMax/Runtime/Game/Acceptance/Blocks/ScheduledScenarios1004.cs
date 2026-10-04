using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Scheduled update 2026-10-04: story offers in the journal (one per giver, strangers capped), the fire
    /// extinguisher on an engine fire, convoy drivers taking their key, the crutch, the handheld radio, prosthetics at
    /// the salvage vendors.</summary>
    public static class ScheduledScenarios1004
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new StoryOffers();
            yield return new ExtinguisherFire();
            yield return new DriverKeys();
            yield return new CrutchHop();
            yield return new HandRadio();
            yield return new ClinicStock();
        }

        internal static IEnumerator Until(System.Func<bool> ok, float seconds)
        {
            float t0 = Time.time;
            while (!ok() && Time.time - t0 < seconds) yield return null;
        }
    }

    /// <summary>The journal's jobs on day 1: story offers once per giver, no names before meeting, at most
    /// <see cref="WastelandGame.StoryOfferStrangers"/> strangers.</summary>
    class StoryOffers : Scenario
    {
        public override string Id => "ui.story_offers";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return null;
            var pins = new List<WastelandGame.Pin>();
            g.JobPins(pins);
            int offers = 0; var labels = new HashSet<string>(); bool dup = false, named = false;
            foreach (var p in pins)
            {
                if (!p.label.StartsWith("? ")) continue;
                offers++;
                if (!labels.Add(p.label + p.pos)) dup = true;
                foreach (var m in MadMax.Story.StoryCast.All) if (p.label == "? " + m.name) named = true;
            }
            c.Metric("story_offer_pins", offers, "");
            c.Metric("job_pins", pins.Count, "");
            c.Note(string.Join(" | ", pins.ConvertAll(p => p.label)));
            c.Check(offers <= WastelandGame.StoryOfferStrangers, $"day 1 shows at most {WastelandGame.StoryOfferStrangers} story offers ({offers})");
            c.Check(!dup, "one pin per giver");
            c.Check(!named, "givers the player hasn't met go by what they do, not their names");
        }
    }

    /// <summary>An engine fire on a parked car is beaten with the extinguisher before the tank roll: a few bursts put it
    /// out, it stays out, the bottle empties by bursts and is refilled with sand at a workbench.</summary>
    class ExtinguisherFire : Scenario
    {
        public override string Id => "vehicle.extinguisher";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = UpdateScenarios1004.Car(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(9f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1f);
            var sys = v.GetComponent<VehicleSystems>();
            if (!c.Check(sys, "the car has systems")) yield break;
            sys.fuel = 30f;
            sys.Heat(5f);
            yield return ScheduledScenarios1004.Until(() => sys.Burning, 3f);
            if (!c.Check(sys.Burning, "the engine bay catches fire")) yield break;
            Fire fire = null;
            foreach (var f in Fire.All) if (f && f.transform.IsChildOf(v.transform)) fire = f;
            if (!c.Check(fire, "the fire sits on the car")) yield break;
            var at = fire.transform.position + v.transform.forward * 2f; at.y = DeformableTerrain.Instance.Height(at.x, at.z) + 0.1f;
            g.Player.Teleport(at, Quaternion.LookRotation(-v.transform.forward).eulerAngles.y);
            g.Inventory.AddItem(SafetyTools.Extinguisher);
            g.UseItem(SafetyTools.Extinguisher);
            yield return new WaitForSeconds(0.5f);
            var tool = g.Player.Tool as ExtinguisherTool;
            if (!c.Check(tool, "the extinguisher is in hand")) yield break;
            c.Fixture("a burning parked car with 30 L aboard, the player 2 m in front with a full extinguisher");
            c.Screenshot("fire");
            yield return null;
            int bursts = 0;
            while (sys.Burning && bursts < ExtinguisherTool.Bursts) { tool.Strike(g.Player); bursts++; yield return new WaitForSeconds(0.4f); }
            c.Metric("bursts_to_put_out", bursts, "");
            c.Check(!sys.Burning, $"the fire is out after {bursts} bursts");
            c.Screenshot("out");
            yield return null;
            var burn = v.GetComponent<VehicleBurn>();
            c.Check(!burn || (!burn.exploded && !burn.charred), "no explosion, not burned out");
            c.Check(Mathf.Abs(g.Condition(SafetyTools.Extinguisher) - (1f - bursts / (float)ExtinguisherTool.Bursts)) < 0.02f, $"the bottle lost {bursts} of {ExtinguisherTool.Bursts} bursts ({g.Condition(SafetyTools.Extinguisher):0.00} left)");
            yield return new WaitForSeconds(4f);
            c.Check(!sys.Burning, "it stays out");
            for (int i = 0; i < ExtinguisherTool.Bursts; i++) g.WearTool(SafetyTools.Extinguisher, 1f / ExtinguisherTool.Bursts);
            c.Check(g.Inventory.GetItem(SafetyTools.Extinguisher) == 1 && g.Condition(SafetyTools.Extinguisher) <= 0f, "an empty bottle stays in the pack");
            g.Inventory.Add(ResourceType.Sand, 10);
            c.Check(g.RepairTool(SafetyTools.Extinguisher) && g.Condition(SafetyTools.Extinguisher) >= 0.999f, "refilled with sand at the bench");
        }
    }

    /// <summary>A convoy driver who climbs out takes the key: the car won't start for the player until it is looted
    /// off the body.</summary>
    class DriverKeys : Scenario
    {
        public override string Id => "npc.driver_keys";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = UpdateScenarios1004.Car(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var ign = v.GetComponent<VehicleIgnition>();
            if (!c.Check(ign && ign.NeedsKey, "the car takes a key")) yield break;
            ign.key = VehicleIgnition.Where.Ignition;
            var ai = v.gameObject.AddComponent<AiDriver>();
            yield return null;
            var at = v.transform.position - v.transform.right * 2.5f;
            var npc = MadMax.Npc.Npc.Spawn(NpcProfile.Make("test:driver", NpcRole.Raider, 77), at, 0f, null, g.propMaterial);
            var take = typeof(Convoy).GetMethod("TakeKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (!c.Check(take != null, "Convoy.TakeKey exists")) yield break;
            ai.Release();
            take.Invoke(null, new object[] { npc, ai });
            c.Fixture("a raider climbed out of the car (Convoy.TakeKey)");
            c.Check(ign.key == VehicleIgnition.Where.Carried && npc.carriedKey == ign.KeyItem, "the driver has the key on their ring");
            c.Check(!ign.CanStart(g.Inventory), "the car won't start for the player");
            npc.ApplyHit(npc.transform.position + Vector3.up * 1.2f, Vector3.forward, 50f, 0.5f, g.Player.gameObject);
            yield return ScheduledScenarios1004.Until(() => !npc.Alive, 2f);
            if (!c.Check(!npc.Alive, "the driver goes down")) yield break;
            var loot = npc.GetComponentInChildren<Lootable>();
            c.Check(loot && loot.extra.Contains(ign.KeyItem), "the key is on the body");
            g.Inventory.AddItem(ign.KeyItem);
            c.Check(ign.CanStart(g.Inventory), "looted, it starts the car");
            Object.Destroy(ai);
        }
    }

    /// <summary>A crutch lifts the one-legged hop (0.3) to 0.55 and an unsplinted broken leg from 0.45 to 0.6.</summary>
    class CrutchHop : Scenario
    {
        public override string Id => "body.crutch";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            g.Sever(BodyZone.LegL, false);
            yield return null;
            float hop = g.LimbSpeed;
            g.Inventory.AddItem(SafetyTools.Crutch);
            g.UseItem(SafetyTools.Crutch);
            yield return new WaitForSeconds(0.3f);
            c.Check(g.OnCrutch, "the crutch is in hand");
            c.Metric("hop", hop, ""); c.Metric("crutch", g.LimbSpeed, "");
            c.Check(hop < 0.35f && Mathf.Abs(g.LimbSpeed - WastelandGame.CrutchHop) < 0.01f, $"one leg: {hop:0.00} hopping, {g.LimbSpeed:0.00} on the crutch");
            c.Screenshot("crutch");
            yield return null;
            g.Player.Equip(null);
            c.Check(!g.OnCrutch && g.LimbSpeed < 0.35f, "put away, back to hopping");
        }
    }

    /// <summary>The handheld radio plays when held and switched on, keeps its state put away, and carries the home
    /// frequency from the pack (a beep and the line on screen without any other radio on).</summary>
    class HandRadio : Scenario
    {
        public override string Id => "radio.handheld";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(g.Inventory.GetItem(SafetyTools.HandRadio) > 0, "the starting kit has a handheld radio");
            if (g.Inventory.GetItem(SafetyTools.HandRadio) == 0) g.Inventory.AddItem(SafetyTools.HandRadio);
            g.UseItem(SafetyTools.HandRadio);
            yield return new WaitForSeconds(0.3f);
            var tool = g.Player.Tool as HandRadioTool;
            if (!c.Check(tool && tool.Receiver, "the radio is in hand with a receiver")) yield break;
            if (tool.Receiver.on) tool.Strike(g.Player);
            tool.Strike(g.Player);
            c.Check(tool.Receiver.on, "use switches it on");
            int st = tool.Receiver.station;
            g.Player.Equip(null);
            yield return null;
            g.UseItem(SafetyTools.HandRadio);
            yield return new WaitForSeconds(0.3f);
            tool = g.Player.Tool as HandRadioTool;
            c.Check(tool && tool.Receiver.on && tool.Receiver.station == st, "put away and back: still on, same station");
            if (tool) tool.Receiver.on = false;
            g.Player.Equip(null);
            foreach (var r in MadMax.Audio.RadioReceiver.All) if (r) r.on = false;
            // a far station's call reaches the pack
            if (!TestWorld.Pad(8f, out var pad)) { c.Block("no pad"); yield break; }
            var q = pad; q.y = t.Height(q.x, q.z);
            var bench = FurnitureLibrary.Spawn("workbench", g.Build.Structures, q, Quaternion.identity, g.propMaterial);
            var station = bench ? bench.GetComponent<CraftingStation>() : null;
            if (!c.Check(station, "a workbench")) yield break;
            bench.owner = g.Stats.name;
            Recipe rec = null; foreach (var x in RecipeLibrary.All) if (x.station == "workbench") { rec = x; break; }
            station.Enqueue(rec, 0.0001f);
            var far = pad + new Vector3(0f, 0f, -70f); far.y = t.Height(far.x, far.z) + 0.3f;
            g.Player.Teleport(far, 0f);
            yield return new WaitForSeconds(1.5f);
            int calls = g.HandRadioCalls;
            station.watts = 200f;
            yield return ScheduledScenarios1004.Until(() => g.HandRadioCalls > calls, 3f);
            c.Check(g.HandRadioCalls > calls, "the handheld in the pack picks up the home call: " + g.LastHomeCall);
        }
    }

    /// <summary>Salvage vendors (the medical sellers) stock prosthetics and crutches, buy prosthetics back, and price
    /// them above junk; the extinguisher and crutch have recipes, the crutch by hand.</summary>
    class ClinicStock : Scenario
    {
        public override string Id => "trade.prosthetics";
        public override float Timeout => 10f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var ids = new HashSet<string>(Trade.AllStockIds());
            c.Check(ids.Contains("pros_hook") && ids.Contains("pros_peg_leg") && ids.Contains("tool_crutch"), "salvage vendors stock hooks, pegs and crutches");
            c.Check(Trade.Buys("salvage", "pros_hook"), "and buy prosthetics");
            c.Check(Trade.Value("pros_hook") >= 20f && Trade.Value("pros_shotgun_arm") > Trade.Value("pros_hook"), $"priced as goods (hook {Trade.Value("pros_hook")}, shotgun arm {Trade.Value("pros_shotgun_arm")})");
            Recipe ext = null, crutch = null, radio = null;
            foreach (var r in RecipeLibrary.All) { if (r.output == SafetyTools.Extinguisher) ext = r; if (r.output == SafetyTools.Crutch) crutch = r; if (r.output == SafetyTools.HandRadio) radio = r; }
            c.Check(ext != null && radio != null && crutch != null, "extinguisher, radio and crutch recipes exist");
            c.Check(crutch != null && crutch.hand, "the crutch is made by hand");
            c.Check(ToolLibrary.Has(SafetyTools.Extinguisher) && ToolLibrary.Has(SafetyTools.Crutch) && ToolLibrary.Has(SafetyTools.HandRadio), "all three are tools");
            yield break;
        }
    }
}
