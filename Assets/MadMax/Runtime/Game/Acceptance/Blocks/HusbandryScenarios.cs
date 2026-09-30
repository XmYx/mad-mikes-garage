using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Animals;
using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the Husbandry block.</summary>
    public static class HusbandryScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new HusbandryLadder();
        }
    }

    /// <summary>Depth stage E end to end on a pad near the start of a seed-7 world: the scythe cuts hay (meadow and a ripe
    /// wheat bed), hay and grain are mixed into feed that fills a trough; a sheep is shorn and its wool spun into thread
    /// and woven into cloth; a beehive makes honey over days (by flowering beds) while a snare and a baited cage trap
    /// catch game and the fleece grows back; a butchering table gets more from a deer than a knife on the ground; a
    /// wounded goat is bandaged and splinted and its health rises; leather boots come off the leather bench; a horse in
    /// the stable gets its stamina back. Fixtures (grants, spawned animals, the clock moved on) are disclosed.</summary>
    class HusbandryLadder : Scenario
    {
        public override string Id => "husbandry.ladder";
        public override float Timeout => 180f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(14f, out var pad)) { c.Block("no open pad"); yield break; }
            g.Player.Teleport(pad + Vector3.up * 0.3f, 0f);
            yield return new WaitForSeconds(0.4f);
            c.Fixture("on foot on a clear pad near the start");
            Vector3 At(float x, float z) { var p = pad + new Vector3(x, 0f, z); p.y = t.Height(p.x, p.z); return p; }
            Placeable Put(string id, float x, float z, float yaw = 180f) => FurnitureLibrary.Spawn(id, g.Build.Structures, At(x, z), Quaternion.Euler(0f, yaw, 0f), g.propMaterial);
            Animal Beast(string species, float x, float z, bool kept)
            {
                var a = Animal.Spawn(AnimalLibrary.Get(species), At(x, z), 0f, g.propMaterial, "t:" + species + ":" + x);
                if (kept) { a.owned = true; a.order = 2; a.home = a.transform.position; }
                return a;
            }

            // ---- 1. hay: the scythe on the best meadow nearby, then on a ripe wheat bed
            var scythe = ToolLibrary.Create("tool_scythe", g.propMaterial);
            c.Check(scythe is ScytheTool, "the scythe is a tool");
            g.Player.Equip(scythe);
            float best = 0f; Vector3 meadow = pad;
            for (int k = 0; k < 220; k++)
            {
                float a = k * 2.39996f, r = 3f + k * 0.2f;
                var p = pad + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (Physics.CheckSphere(new Vector3(p.x, t.Height(p.x, p.z) + 1f, p.z), 0.7f, ~0, QueryTriggerInteraction.Ignore)) continue;
                float cv = t.Cover(p, ScytheTool.Reach);
                if (cv > best) { best = cv; meadow = p; }
            }
            c.Metric("meadow_cover", best, "cells");
            int hay0 = g.Inventory.Get(ResourceType.Hay);
            if (best >= 9f)
            {
                var fwd = g.Player.transform.forward; fwd.y = 0f; fwd.Normalize();
                var stand = meadow - fwd * 1.1f;
                g.Player.Teleport(new Vector3(stand.x, t.Height(stand.x, stand.z) + 0.2f, stand.z), Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg);
                c.Fixture("walked to the thickest grass within 47 m");
                yield return new WaitForSeconds(0.4f);
                var swath = g.Player.transform.position + g.Player.transform.forward * 1.1f;
                float before = t.Cover(swath, ScytheTool.Reach);
                scythe.Strike(g.Player);
                int cut = g.Inventory.Get(ResourceType.Hay) - hay0;
                c.Metric("meadow_hay", cut, "");
                if (before >= 9f) c.Check(cut >= 1 && t.Cover(swath, 0.8f) < before * 0.3f, $"a sweep of the scythe cuts the grass into hay ({cut} from {before:0} cells) and flattens it");
                else c.Note($"the player turned away from the grass ({before:0.0} cells in front, {cut} hay)");
            }
            else c.Note($"no meadow near the pad (best cover {best:0.0}); the wheat bed alone shows the scythe");
            g.Player.Teleport(pad + Vector3.up * 0.3f, 0f);
            yield return new WaitForSeconds(0.3f);
            var ahead = g.Player.transform.position + g.Player.transform.forward * 1.1f;
            var bed = FurnitureLibrary.Spawn("planter", g.Build.Structures, new Vector3(ahead.x, t.Height(ahead.x, ahead.z), ahead.z), Quaternion.identity, g.propMaterial);
            yield return null;
            var plot = bed ? bed.GetComponent<GardenPlot>() : null;
            if (c.Check(plot && plot.Sow("seed_wheat", 1f), "a planter sown with wheat"))
            {
                plot.growth = 1f; plot.health = 1f; plot.water = 1f;
                c.Fixture("the wheat grown ripe (time skipped)");
                int wheat0 = g.Inventory.GetItem("crop_wheat"), hay1 = g.Inventory.Get(ResourceType.Hay);
                scythe.Strike(g.Player);
                c.Check(g.Inventory.GetItem("crop_wheat") > wheat0 && g.Inventory.Get(ResourceType.Hay) >= hay1 + 3 && plot.crop == null,
                    $"the scythe reaps the ripe bed: +{g.Inventory.GetItem("crop_wheat") - wheat0} wheat, +{g.Inventory.Get(ResourceType.Hay) - hay1} hay (straw)");
            }
            g.Player.Equip(null);

            // ---- 2. feed: hay and wheat mixed at the workbench, into the trough; the mill makes three times the batch
            var bench = Put("workbench", -3f, 3f);
            var trough = Put("trough", 9f, 3f);
            var millP = Put("feed_mill", -6f, 3f);
            yield return null;
            var made = new int[1];
            yield return Craft(c, g, "feed_crop_wheat", bench ? bench.GetComponent<CraftingStation>() : null, made);
            c.Check(made[0] == 4, $"hay and wheat mixed by hand into {made[0]} animal feed");
            var tr = trough ? trough.GetComponent<Trough>() : null;
            if (c.Check(tr, "a trough"))
            {
                g.Inventory.Add(ResourceType.Water, 6);
                c.Fixture("6 L of water for the trough");
                int feed0 = g.Inventory.Get(ResourceType.Feed);
                tr.Use(g, false);
                c.Check(tr.mix >= 4f && g.Inventory.Get(ResourceType.Feed) < feed0, $"the trough takes the mixed feed first (feed {tr.feed:0}, mixed {tr.mix:0})");
                c.Check(tr.Eat(0.6f) && tr.LastMixed, "a day's ration from it is a mixed one (richer: more produce, faster wool)");
            }
            var hand = RecipeLibrary.Get("feed_crop_wheat"); var milled = RecipeLibrary.Get("mill_feed_crop_wheat");
            var mill = millP ? millP.GetComponent<CraftingStation>() : null;
            c.Check(mill && mill.type == "feedmill" && milled != null && milled.station == "feedmill" && milled.amount >= hand.amount * 3, "the feed mill makes three times the hand batch");

            // ---- 3. wool: shear a sheep, spin the wool into thread, weave the thread into cloth
            var sheep = Beast("sheep", 2f, -3f, true);
            c.Fixture("a kept sheep in full fleece");
            g.Inventory.AddItem("tool_shears", 1);
            c.Fixture("shears");
            yield return new WaitForSeconds(0.6f);
            c.Check(sheep.Prompt(g) != null && sheep.Prompt(g).Contains("SHEAR"), "the sheep offers [E] SHEAR: " + sheep.Prompt(g));
            int wool0 = g.Inventory.Get(ResourceType.Wool);
            sheep.Use(g, false);
            int shorn = g.Inventory.Get(ResourceType.Wool) - wool0;
            c.Metric("wool_shorn", shorn, "");
            c.Check(shorn >= 2 && sheep.wool == 0f, $"shorn: +{shorn} wool, the fleece gone");
            var wheel = Put("spinning_wheel", 0f, 3f);
            var loom = Put("loom", 3f, 3f);
            yield return null;
            yield return Craft(c, g, "spin_wool", wheel ? wheel.GetComponent<CraftingStation>() : null, made);
            c.Check(made[0] == 3, $"the spinning wheel spins the wool into {made[0]} thread");
            yield return Craft(c, g, "loom_thread", loom ? loom.GetComponent<CraftingStation>() : null, made);
            c.Check(made[0] == 3, $"the loom weaves the thread into {made[0]} cloth");
            var ropeR = RecipeLibrary.Get("spin_rope");
            c.Check(ropeR != null && ropeR.station == "spinning" && ropeR.output == "misc_rope", "hemp twists into rope at the spinning wheel");

            // ---- 4. time: a beehive by flowering beds, a snare, a baited cage trap; the fleece grows back
            var hiveP = Put("beehive", -8f, -6f);
            foreach (var x in new[] { -12f, -9.5f, -7f, -4.5f })
            {
                var fp = Put("planter", x, -9f);
                var f = fp ? fp.GetComponent<GardenPlot>() : null;
                if (f && f.Sow("seed_flower", 1f)) { f.growth = 1f; f.health = 1f; }
            }
            c.Fixture("four planters of flowers in bloom beside the hive");
            var snareP = Put("snare", 8f, -8f);
            var cageP = Put("cage_trap", 11f, -8f);
            var candles = Put("candles", -3f, 6f);
            yield return null;
            var hive = hiveP ? hiveP.GetComponent<Beehive>() : null;
            var snare = snareP ? snareP.GetComponent<SnareTrap>() : null;
            var cage = cageP ? cageP.GetComponent<SnareTrap>() : null;
            if (!c.Check(hive && snare && cage, "a beehive, a snare and a cage trap stand")) yield break;
            g.Inventory.AddItem("food_carrot", 2);
            cage.Use(g, true);
            c.Fixture("a carrot as bait");
            c.Check(cage.bait == 1, "the cage trap is baited");
            hive.Tick(); snare.Tick(); cage.Tick();
            float snareHab = snare.Habitat(out var game);
            c.Note($"forage {hive.Forage():0.00}, small game here {snareHab:0.00} ({game}), {Weather.TemperatureAt(pad.z):0} C");
            int skip = Weather.TemperatureAt(pad.z) < 6f ? 6 : 3;                                     // a cold spell: the colony needs longer
            DayNight.SetDay(DayNight.Day + skip);
            c.Fixture("the clock moved on " + skip + " days");
            yield return new WaitForSeconds(0.8f);
            hive.Tick(); snare.Tick(); cage.Tick();
            c.Metric("honey_made", hive.honey, "");
            c.Check(hive.honey >= 1f, $"the hive made honey in {skip} days ({hive.honey:0.0}, wax {hive.wax:0.0}, forage {hive.forage:0.00})");
            var torch = ToolLibrary.Create("tool_torch", g.propMaterial);
            g.Player.Equip(torch);
            c.Fixture("a burning torch in hand (smoke calms the bees)");
            int honey0 = g.Inventory.Get(ResourceType.Honey); float hp0 = g.Stats.health;
            hive.Use(g, false);
            g.Player.Equip(null);
            c.Check(g.Inventory.Get(ResourceType.Honey) > honey0 && g.Stats.health >= hp0, $"honey taken under smoke without a sting (+{g.Inventory.Get(ResourceType.Honey) - honey0})");
            c.Check(snare.caught != null, "the snare caught something: " + snare.caught);
            int meat0 = g.Inventory.GetItem("food_meat_raw");
            snare.Use(g, false);
            c.Check(g.Inventory.GetItem("food_meat_raw") > meat0 && snare.caught == null, "the catch taken from the snare (meat) and the snare reset");
            c.Check(cage.caught != null, "the cage trap holds a live catch: " + cage.caught);
            c.Check(sheep.wool > 0.8f, $"the fleece grew back ({sheep.wool * 100f:0}%)");
            c.Check(candles && candles.GetComponentInChildren<Light>(), "beeswax candles give light");

            // ---- 5. butchering: a knife on the ground against the table
            var tableP = Put("butcher_table", -10f, 8f);
            yield return null;
            g.Inventory.AddItem("tool_knife", 1);
            c.Fixture("a knife; two deer killed for the comparison (one by the table, one 18 m off)");
            var deerHand = Beast("deer", 9f, 10f, false); var deerTable = Beast("deer", -10f, 6.3f, false);
            deerHand.ApplyHit(deerHand.transform.position, Vector3.down, 99f, 0.1f, null);
            deerTable.ApplyHit(deerTable.transform.position, Vector3.down, 99f, 0.1f, null);
            yield return null;
            var byHand = deerHand.GetComponent<Carcass>(); var onTable = deerTable.GetComponent<Carcass>();
            if (c.Check(byHand && onTable && ButcherTable.Near(onTable.transform.position) && !ButcherTable.Near(byHand.transform.position), "one carcass by the table, one out in the open"))
            {
                int Take() => g.Inventory.GetItem("food_meat_raw") + g.Inventory.Get(ResourceType.Hide);
                int b0 = Take(); byHand.Use(g, false); int handGot = Take() - b0;
                b0 = Take(); tableP.GetComponent<ButcherTable>().Use(g, false); int tableGot = Take() - b0;
                c.Metric("butcher_by_hand", handGot, "meat+hide"); c.Metric("butcher_on_table", tableGot, "meat+hide");
                c.Check(tableGot > handGot && onTable.butchered, $"the butchering table beats the knife on the ground ({tableGot} against {handGot} meat and hide)");
            }

            // ---- 6. animal treatment: a wounded goat bandaged and splinted
            var goat = Beast("goat", -2f, -3f, true);
            c.Fixture("a kept goat, wounded (a heavy blow)");
            yield return null;
            goat.ApplyHit(goat.transform.position + Vector3.up * 0.4f, Vector3.right, 0.9f, 0.3f, null);
            yield return new WaitForSeconds(0.6f);
            float h0 = goat.health;
            c.Note($"goat: health {goat.health:0.0}/{goat.Def.health}, bleeding {goat.bleed:0.00}, lame {goat.limp:0.00}, down {goat.Downed}");
            c.Check(goat.Alive && goat.Hurt && goat.bleed > 0f, "the blow left it bleeding");
            g.Inventory.AddItem("med_bandage", 1); g.Inventory.AddItem("med_splint", 1);
            c.Fixture("a bandage and a splint");
            string prompt = goat.Prompt(g);
            c.Check(prompt != null && prompt.Contains("TREAT"), "[E] TREAT offered with medicine in the pack: " + prompt);
            goat.Use(g, false);
            if (goat.limp > 0.05f) goat.Use(g, false);
            c.Metric("goat_health_gain", goat.health - h0, "hp");
            c.Check(goat.health > h0 && goat.bleed == 0f && goat.limp == 0f, $"treated: health {h0:0.0} -> {goat.health:0.0}, bleeding stopped, the leg set");

            // ---- 7. leather goods at the leather bench
            var leatherP = Put("leather_bench", 6f, 3f);
            yield return null;
            var bootsR = RecipeLibrary.Get("l_boots");
            c.Check(bootsR != null && bootsR.station == "leather", "leather boots are made at the leather bench (not the sewing table)");
            yield return Craft(c, g, "l_boots", leatherP ? leatherP.GetComponent<CraftingStation>() : null, made);
            c.Check(made[0] == 1 && ClothingLibrary.Get("cloth_leather_boots") != null, "leather boots off the bench");
            var belt = ClothingLibrary.Get("work_belt"); var cuirass = ClothingLibrary.Get("leather_cuirass");
            c.Check(belt != null && belt.carry > 0f && cuirass != null && cuirass.armor != null, "belts carry more, the cuirass is armour");

            // ---- 8. the stable: a tired horse gets its wind back
            var stableP = Put("stable", 0f, -10f);
            yield return null;
            var stable = stableP ? stableP.GetComponent<Stable>() : null;
            var horse = Beast("horse", 0f, -10.5f, true);
            horse.stamina = 20f;
            c.Fixture("a kept, tired horse (stamina 20) in the stable");
            yield return new WaitForSeconds(2f);
            c.Check(stable && horse.InStable && horse.stamina >= 40f, $"in the stable the horse gets its wind back (stamina {horse.stamina:0})");
            c.Check(stableP && stableP.GetComponent<Trough>(), "the stable's manger feeds like a trough");

            c.Metric("tally_hay", g.HusbandryCount("hay"), ""); c.Metric("tally_wool", g.HusbandryCount("wool"), "");
            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (rig) rig.SetTarget(g.Player.transform);
            yield return new WaitForSeconds(1f);
            c.Screenshot("husbandry");
            yield return null;
        }

        /// <summary>Queue one recipe at a station (only what the pack lacks is granted, and disclosed), hurry the job
        /// and report how many came out (<paramref name="made"/>[0], -1 on a failure).</summary>
        static IEnumerator Craft(ScenarioContext c, WastelandGame g, string id, CraftingStation st, int[] made)
        {
            made[0] = -1;
            var r = RecipeLibrary.Get(id);
            if (!c.Check(r != null, "recipe " + id + " exists")) yield break;
            if (!c.Check(st && st.type == r.station, $"{r.name} is made at the {r.station} ({(st ? st.type : "no station")})")) yield break;
            foreach (var (t, n) in r.resources)
            {
                if (t == ResourceType.None) continue;
                int need = RecipeLibrary.Amount(n) - g.Inventory.Get(t);
                if (need > 0) { g.Inventory.Add(t, need); c.Fixture(need + " " + ResourceInfo.Name(t) + " for " + r.name); }
            }
            foreach (var (it, n) in r.items)
            {
                int need = n - g.Inventory.GetItem(it);
                if (need > 0) { g.Inventory.AddItem(it, need); c.Fixture(need + " " + ItemCatalog.Name(it) + " for " + r.name); }
            }
            string why = g.CraftBlockReason(r, st);
            if (!c.Check(why == null, r.name + " can be made" + (why != null ? ": " + why : ""))) yield break;
            int before = Output(g, r, st);
            g.Craft(r, st);
            if (st.queue.Count > 0) st.queue[0].progress = 0.95f;
            float t0 = Time.time;
            while (st.queue.Count > 0 && Time.time - t0 < 20f) yield return null;
            yield return new WaitForSeconds(0.3f);
            made[0] = Output(g, r, st) - before;
        }

        static int Output(WastelandGame g, Recipe r, CraftingStation st) =>
            r.kind == OutputKind.Resource ? g.Inventory.Get(r.outputResource) + st.tray.Get(r.outputResource) : g.Inventory.GetItem(r.output) + st.tray.GetItem(r.output);
    }
}
