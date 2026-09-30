using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_L1(List<Scenario> into) => into.Add(new StoryLastEngine());
    }

    /// <summary>THE LAST ENGINE chapters L1-L5 in one sandbox world, each through one of its non-violent ways: Cask's
    /// tape, Mags and Harlan's Works (the relics go on the map); Wick's hoist and Marisol's price; Dell's marker called in
    /// on Grist; the procession truck brought to the shrine and the blowers lent; the V12 built at a garage, mounted,
    /// tuned and driven to the Last Run's destination. Repairs, drives and the garage are disclosed fixtures.</summary>
    class StoryLastEngine : Scenario
    {
        public override string Id => "story.last_engine";
        public override float Timeout => 360f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            yield return Q3T.OnFoot(g);
            if (!c.Check(Q3T.Open("L1"), "L1 is on offer in a sandbox world")) yield break;
            var pins = new List<WastelandGame.Pin>(); g.JobPins(pins);
            c.Check(pins.Any(p => p.label.StartsWith("? BROTHER CASK")), "the map shows Brother Cask's shrine");
            c.Metric("shrine_to_town", Vector3.Distance(StoryAnchors.Get("cask"), StoryAnchors.Get("town1")), "m");
            c.Metric("last_run_latitude", MadMax.World.WorldGen.Latitude(StoryAnchors.Get("l5_end").z), "deg");
            c.Metric("last_run_distance", Vector3.Distance(StoryAnchors.Get("l5_end"), StoryAnchors.Get("town1")), "m");

            // ---- L1 THE SOUND BEFORE THE STORM
            yield return Q3T.TalkAt(c, "cask", "cask", "THEY SAY YOU KEEP");
            yield return H.Until(() => Q3T.Active("L1"), 3f);
            yield return Q3T.TalkAt(c, "cask", "cask", "PLAY ME THE TAPE");
            yield return Q3T.Step(c, "L1", "tape", 3f, "heard the tape");
            c.Screenshot("shrine");
            yield return null;
            yield return Q3T.TalkAt(c, "l1_mags", "l1_mags", "A V12, TWO BLOWERS");
            yield return Q3T.Step(c, "L1", "vet", 3f, "Mags knows the shop bell");
            yield return H.Walk(c, "l1_works", 1f);
            yield return Q3T.Step(c, "L1", "works", 4f, "Harlan's Works");
            yield return H.Until(() => LastEngine.Has("heard:relic_crank"), 3f);
            c.Check(g.Inventory.GetItem("story_build_sheet") > 0 && LastEngine.Relics.All(r => LastEngine.Has("heard:" + r)), "Harlan's build sheet: the four pieces are on the map");
            yield return Q3T.TalkAt(c, "l1_mags", "l1_mags", "HARLAN'S BUILD SHEET");
            yield return Q3T.Step(c, "L1", "why", 3f, "why it was split");
            yield return Q3T.TalkAt(c, "cask", "cask", "IT WAS BUILT AT HARLAN'S");
            yield return Q3T.Finished(c, "L1", "L1 THE SOUND BEFORE THE STORM is done");

            // ---- L2 IRON PILGRIMAGE
            yield return H.Until(() => Q3T.Open("L2") && Q3T.Open("L3") && Q3T.Open("L4"), 3f);
            c.Check(Q3T.Open("L2") && Q3T.Open("L3") && Q3T.Open("L4"), "L2, L3 and L4 open after L1");
            yield return Q3T.TalkAt(c, "cask", "cask", "I'LL GO AFTER THE BLOCK");
            yield return H.Until(() => Q3T.Active("L2"), 3f);
            yield return Q3T.TalkAt(c, "l2_wick", "l2_wick", "WHY DID YOU STAY");
            var gen = Q3T.Prop(g, "generator", "l2_wick", 6f);
            var gc = gen ? gen.GetComponent<Generator>() : null;
            if (!c.Check(gc && !gc.on && gc.fuel <= 0f, "Wick's hoist generator is dry and off")) yield break;
            g.Inventory.Add(ResourceType.Fuel, 10);
            c.Fixture("10 L petrol in the pack");
            gc.Use(g, true); gc.Use(g, false);
            yield return Q3T.Step(c, "L2", "block", 5f, "the block");
            yield return H.Until(() => LastEngine.Has("relic_block"), 3f);
            c.Check(Plot.Route("L2", "block") == "LIFTED OUT ON WICK'S HOIST" && g.Inventory.GetItem("relic_block") > 0, "Wick lifted the block out safely");
            if (g.Inventory.Get(ResourceType.Scrap) < 60) { g.Inventory.Add(ResourceType.Scrap, 60); c.Fixture("60 scrap for Marisol"); }
            yield return Q3T.TalkAt(c, "l2_marisol", "l2_marisol", "I'LL PAY FOR THEM");
            yield return Q3T.Step(c, "L2", "heads", 4f, "the heads");
            yield return H.Until(() => LastEngine.Has("relic_heads"), 3f);
            c.Check(g.Inventory.GetItem("relic_heads") > 0, "Marisol sold the heads");
            yield return Q3T.Finished(c, "L2", "L2 IRON PILGRIMAGE is done");

            // ---- L3 A CRANKSHAFT AND A GRUDGE
            yield return Q3T.TalkAt(c, "cask", "cask", "WHO HAS THE CRANKSHAFT");
            yield return H.Until(() => Q3T.Active("L3"), 3f);
            yield return Q3T.TalkAt(c, "l3_dell", "l3_dell", "GRIST SAYS HE PAYS");
            c.Check(g.Inventory.GetItem("story_grist_marker") > 0, "Dell's marker on Grist");
            yield return H.Walk(c, "l3_camp", 3f);
            yield return Q3T.Step(c, "L3", "camp", 4f, "found Grist's camp");
            c.Screenshot("grist_camp");
            yield return null;
            yield return Q3T.TalkAt(c, "l3_camp", "l3_grist", "YOUR CREW MIGHT LIKE");
            yield return Q3T.Step(c, "L3", "crank", 3f, "the crankshaft changes hands");
            yield return H.Until(() => LastEngine.Has("relic_crank"), 3f);
            c.Check(g.Inventory.GetItem("relic_crank") > 0 && g.Inventory.GetItem("story_grist_marker") == 0, "the crank for the marker");
            c.Check(!MadMax.Npc.NpcRegistry.IsDead("cast:l3_grist"), "nobody bled for it");
            yield return Q3T.TalkAt(c, "cask", "cask", "GRIST'S LUCK");
            yield return Q3T.Finished(c, "L3", "L3 A CRANKSHAFT AND A GRUDGE is done");

            // ---- L4 WHAT A RELIC IS FOR
            yield return Q3T.TalkAt(c, "cask", "cask", "WILL THE CHURCH PART");
            yield return H.Until(() => Q3T.Active("L4") && StoryTag.Find("l4_truck") != null, 3f);
            var truck = StoryTag.Find("l4_truck") ? StoryTag.Find("l4_truck").GetComponent<VehicleDriver>() : null;
            if (!c.Check(truck && truck.Engine && truck.Engine.GetComponent<VehiclePart>().damage > 0.8f && truck.GetComponent<VehicleSystems>().fuel < 0.5f, "the procession truck: engine nearly seized, tank dry")) yield break;
            truck.Engine.GetComponent<VehiclePart>().damage = 0.2f; truck.GetComponent<VehicleSystems>().fuel = 20f;
            c.Fixture("the truck's engine repaired and fuelled");
            var shrine = StoryAnchors.Get("cask");
            yield return TestWorld.Place(c, truck, shrine + Quaternion.Euler(0f, StoryAnchors.Yaw("cask"), 0f) * new Vector3(0f, 0f, 8f), Vector3.forward, 1f);
            c.Fixture("drove it to the shrine (placed)");
            yield return Q3T.Step(c, "L4", "trust", 5f, "the Church's trust");
            c.Check(Plot.Route("L4", "trust") == "THE PROCESSION TRUCK RUNS AGAIN", "earned by a mechanic's kindness");
            yield return Q3T.TalkAt(c, "cask", "cask", "LEND THEM FOR ONE RUN");
            yield return Q3T.Finished(c, "L4", "L4 WHAT A RELIC IS FOR is done");
            c.Check(LastEngine.Has("relic_blower") && g.Inventory.GetItem("relic_blower") > 0, "the blowers came down from the altar");

            // ---- L5 THE LAST RUN
            yield return H.Until(() => Q3T.Open("L5"), 3f);
            yield return Q3T.TalkAt(c, "cask", "cask", "THE FOUR PIECES");
            yield return H.Until(() => Q3T.Active("L5"), 3f);
            var rec = RecipeLibrary.Get("last_engine");
            var gp = g.Player.transform.position + g.Player.transform.forward * 9f;
            gp.y = MadMax.World.DeformableTerrain.Instance.Height(gp.x, gp.z);
            var garage = FurnitureLibrary.Spawn("garage", g.Build.Structures, gp, Quaternion.identity, g.propMaterial);
            yield return null;
            var st = garage ? garage.GetComponentInChildren<CraftingStation>() : null;
            if (!c.Check(rec != null && st, "a garage and the Last Engine's recipe")) yield break;
            foreach (var (t, n) in rec.resources) { int need = RecipeLibrary.Amount(n); if (g.Inventory.Get(t) < need) g.Inventory.Add(t, need - g.Inventory.Get(t)); }
            c.Fixture("a garage built beside the shrine; iron, copper and oil topped up");
            string why = g.CraftBlockReason(rec, st);
            c.Check(why == null, "the V12 can be built: " + (why ?? "ok"));
            g.Craft(rec, st);
            if (st.queue.Count > 0) st.queue[0].progress = 0.95f;
            yield return Q3T.Step(c, "L5", "build", 20f, "the Last Engine is built");
            c.Check(LastEngine.Relics.All(r => g.Inventory.GetItem(r) == 0), "the four relics went into it");
            var v12 = Object.FindObjectsByType<VehiclePart>(FindObjectsSortMode.None).FirstOrDefault(p => p.partId == LastEngine.Part && !p.Socket);
            var car = Q3T.GroundCar(g);
            MountSocket sock = null;
            if (car && v12) foreach (var s in car.GetComponent<VehicleChassis>().Sockets) if (s.accepts == PartCategory.Engine && s.maxSizeClass >= v12.sizeClass) { sock = s; break; }
            if (!c.Check(v12 && sock, "the V12 and a car with a big enough engine bay")) yield break;
            var old = sock.Detach(false);
            if (old) Object.Destroy(old.gameObject);
            c.Check(sock.Attach(v12), "the V12 mounted");
            c.Fixture("swapped the car's engine for the V12 (wrench)");
            yield return TestWorld.Place(c, car, shrine + new Vector3(12f, 0f, 12f), Vector3.forward, 1f);
            if (car.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = Mathf.Max(sys.fuel, 30f);
            g.Enter(car);
            c.Fixture("in the V12 car, fuelled");
            yield return Q3T.Step(c, "L5", "mount", 6f, "the Last Engine runs");
            car.GetComponent<VehicleTuning>().map = 0.4f;
            c.Fixture("a power map set at the tuning bench");
            yield return Q3T.Step(c, "L5", "tune", 3f, "tuned");
            c.Check(Plot.Route("L5", "tune") == "TUNED AT A BENCH", "tuned at a bench");
            c.Screenshot("v12");
            yield return null;
            var end = StoryAnchors.Get("l5_end");
            yield return TestWorld.Place(c, car, end + new Vector3(0f, 0f, -20f), Vector3.forward, 1.5f);
            c.Fixture("drove north to the Last Run's end (placed)");
            yield return Q3T.Step(c, "L5", "run", 6f, "THE LAST RUN");
            c.Check(LastEngine.Has("run"), "the Last Engine's legend is complete (run)");
            c.Screenshot("last_run");
            yield return null;
            yield return Q3T.OnFoot(g);
            yield return Q3T.TalkAt(c, "cask", "cask", "IT RAN ALL THE WAY");
            yield return Q3T.Finished(c, "L5", "L5 THE LAST RUN is done");
            c.Check(g.Inventory.GetItem("story_harlan_horn") > 0 && Plot.Flag("paint_last_engine"), "Harlan's horn button and the paint keepsake");
            c.Check(StoryLibrary.Get("L5").payoff.Contains("LOAN"), "the Church's loan is remembered in the last line");
        }
    }
}
