using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    public static partial class StoryQuestTests
    {
        static partial void Tests_S14(List<Scenario> into) => into.Add(new StoryS14());
    }

    /// <summary>S14 SOMETHING IN THE WELL in a sandbox world: Oona's piped well water reads OILY on the test kit she
    /// hands over; 20 L of clean water go into her kettle barrel meanwhile; the leak is traced uphill to Dunmore's
    /// pumpjack and he agrees to cap it; her spare cartridge goes into the filter; the kit reads CLEAN once the filter has
    /// caught up; the route and the free refills are remembered.</summary>
    class StoryS14 : Scenario
    {
        public override string Id => "story.s14";
        public override float Timeout => 240f;

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return new WaitForSeconds(2f);
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            c.Check(MadMax.Story.Story.StateOf("S14") == MadMax.Story.Story.State.Open, "S14 is on offer in a sandbox world");
            yield return H.Walk(c, "oona", 3f);
            yield return H.Until(() => g.CastBody("oona") != null, 5f);
            if (!c.Check(H.Talk(g, g.CastBody("oona"), "THE WATER HERE TASTES"), "Oona Vale's water tastes of oil")) yield break;
            yield return H.Until(() => MadMax.Story.Story.StepDone("S14", "look"), 5f);
            c.Check(g.Inventory.GetItem(UtilityIds.WaterTest) > 0, "Oona hands over the test kit she can't read");
            var inn = StoryAnchors.Get("oona");
            var tank = Placeable.All.FirstOrDefault(p => p && p.id == "water_tank" && g.IsStoryProp(p) && Flat(p.transform.position, inn) < 12f);
            var filter = Placeable.All.FirstOrDefault(p => p && p.id == "filter" && g.IsStoryProp(p) && Flat(p.transform.position, inn) < 12f);
            if (!c.Check(tank && filter, "the inn's tank and filter are piped up")) yield break;
            var tn = tank.GetComponent<UtilityNode>(); var cart = filter.GetComponent<FilterCartridge>();
            c.Check(cart && cart.Spent, "the filter's cartridge is spent");
            c.Screenshot("inn");
            yield return null;

            // ---- the test kit at the tank
            var tp = tank.transform.position + (inn - tank.transform.position).normalized * 1.8f; tp.y = MadMax.World.DeformableTerrain.Instance.Height(tp.x, tp.z) + 0.3f;
            g.Player.Teleport(tp, 0f); c.Fixture("walked to the inn's tank (teleport)");
            yield return new WaitForSeconds(1.1f);
            g.UseItem(UtilityIds.WaterTest);
            string reading = g.ToastText ?? "";
            c.Note(reading);
            c.Check(reading.Contains("OILY"), "the kit reads the inn's water OILY: " + reading);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S14", "test"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S14", "test"), "the water is tested");

            // ---- meanwhile: 20 L into the kettle barrel
            var barrel = Placeable.All.FirstOrDefault(p => p && p.id == "barrel" && g.IsStoryProp(p) && p.GetComponent<S14KettleBarrel>());
            if (c.Check(barrel, "Oona's kettle barrel"))
            {
                g.Inventory.Add(ResourceType.Water, 20); c.Fixture("20 L of clean water in the pack");
                var kb = barrel.GetComponent<S14KettleBarrel>();
                kb.Use(g, true); kb.Use(g, true);
                yield return H.Until(() => MadMax.Story.Story.StepDone("S14", "supply"), 4f);
                c.Check(MadMax.Story.Story.StepDone("S14", "supply") && MadMax.Story.Story.Route("S14", "supply") == "HAULED 20 L OF CLEAN WATER", "the kettles keep going meanwhile ([T] twice)");
            }

            // ---- upstream: Dunmore's pumpjack
            yield return H.Walk(c, "s14_leak", 6f);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S14", "trace"), 4f);
            c.Check(MadMax.Story.Story.StepDone("S14", "trace"), "traced the oil uphill to an old pumpjack");
            var pump = Placeable.All.FirstOrDefault(p => p && p.id == "pumpjack" && g.IsStoryProp(p));
            c.Check(pump && pump.hits < pump.MaxHits, "the pumpjack is cracked");
            yield return H.Until(() => g.CastBody("s14_dunmore") != null, 5f);
            c.Screenshot("pumpjack");
            yield return null;
            c.Check(H.Talk(g, g.CastBody("s14_dunmore"), "YOUR PUMP IS WEEPING"), "Abel Dunmore agrees to cap it");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S14", "stop"), 4f);
            c.Check(Q5Test.Route("S14", "stop", "YOUR PUMP"), "the leak is stopped, by talking: " + MadMax.Story.Story.Route("S14", "stop"));

            // ---- the cartridge, then the kit again
            yield return H.Walk(c, "oona", 3f);
            yield return H.Until(() => g.CastBody("oona") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("oona"), "GOT A SPARE CARTRIDGE"), "Oona's spare cartridge");
            yield return H.Until(() => g.Inventory.GetItem(UtilityIds.Cartridge) > 0, 4f);
            var fp = filter.transform.position + (inn - filter.transform.position).normalized * 1.4f; fp.y = MadMax.World.DeformableTerrain.Instance.Height(fp.x, fp.z) + 0.3f;
            g.Player.Teleport(fp, 0f); c.Fixture("walked to the filter (teleport)");
            yield return new WaitForSeconds(0.8f);
            c.Check(cart.Replace(g), "a new cartridge in the filter ([E])");
            yield return H.Until(() => MadMax.Story.Story.StepDone("S14", "filter"), 4f);
            float t0 = Time.time;
            yield return H.Until(() => { WaterQuality.Reading(tn, out string v, out _); return v == "CLEAN"; }, 90f);
            c.Metric("clears_in", Time.time - t0, "s");
            g.Player.Teleport(tp, 0f);
            yield return new WaitForSeconds(0.6f);
            string clean = g.TestWater() ?? "";
            c.Note(clean);
            c.Check(clean.StartsWith("WATER TEST: CLEAN"), "the kit reads CLEAN now: " + clean);
            yield return H.Until(() => MadMax.Story.Story.StepDone("S14", "kept"), 5f);
            c.Check(MadMax.Story.Story.StepDone("S14", "clean") && MadMax.Story.Story.StepDone("S14", "kept"), "clean water and the inn kept going");
            yield return H.Walk(c, "oona", 3f);
            yield return H.Until(() => g.CastBody("oona") != null, 5f);
            c.Check(H.Talk(g, g.CastBody("oona"), "THE WELL'S CLEAN"), "tell Oona");
            yield return H.Until(() => MadMax.Story.Story.StateOf("S14") == MadMax.Story.Story.State.Done, 5f);
            c.Check(MadMax.Story.Story.StateOf("S14") == MadMax.Story.Story.State.Done && MadMax.Story.Story.Flag("inn_refills"), "S14 SOMETHING IN THE WELL is done: free refills at the inn");
            c.Check(g.Inventory.GetItem(UtilityIds.Cartridge) >= 2, "filters paid");
            UtilityGrid.NetWater(tn, out float share);
            c.Check(UtilityGrid.NetWater(tn, out _) > 100f && share > 0.99f, "the inn's tank is full of clean water for the refills");
            c.Check(StoryLibrary.Get("S14").payoff.Contains("DUNMORE CAPPED"), "the closing line remembers how the leak was stopped");
            c.Screenshot("clean");
            yield return null;
        }
    }
}
