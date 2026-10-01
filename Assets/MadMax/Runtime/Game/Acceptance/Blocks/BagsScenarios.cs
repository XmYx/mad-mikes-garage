using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios for bags and the back: a worn bag is storage of its own that travels with the bag
    /// (pack, ground, save), belts hand tools to the hotbar, luggage fills the hands, and an overload strains the back
    /// until it gives out.</summary>
    public static class BagsScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new BagsWear();
            yield return new BagsToolbelt();
            yield return new BagsBackStrain();
        }

        /// <summary>On foot on a pad with a pack holding only the worn clothes (the old pack is given back by
        /// <see cref="Restore"/>); returns the old pack's text.</summary>
        internal static string Strip(ScenarioContext c)
        {
            var g = c.Game;
            var snap = InventoryCodec.Encode(g.Inventory);
            var keep = new List<KeyValuePair<string, int>>();
            foreach (var o in g.Player.Rig.outfit) if (BagLibrary.Get(o) == null) keep.Add(new KeyValuePair<string, int>("cloth_" + o, 1));
            g.Inventory.Restore(new int[ResourceInfo.Count], keep);
            if (g.Player.Tool) g.Player.Equip(null);
            c.Fixture("the pack emptied but for the clothes worn, the tool put away (given back at the end)");
            return snap;
        }

        internal static void Restore(ScenarioContext c, string snap)
        {
            var g = c.Game;
            foreach (var o in g.Player.Rig.outfit.ToList()) if (BagLibrary.Get(o) != null) g.TakeOffBagToPack(o);
            InventoryCodec.Decode(g.Inventory, snap);
            g.Stats.injuries.RemoveAll(i => i.type == Wound.Strain);
            g.SetBackStrain(0f);
            c.Fixture("bags taken off, the old pack given back, the back reset");
        }

        internal static int Count(Inventory inv, string key) => key.StartsWith("res:") ? inv.Get((ResourceType)int.Parse(key.Substring(4))) : inv.GetItem(key);

        internal static string FullKeyIn(Inventory inv, string defId)
        {
            foreach (var kv in inv.Items) if (kv.Value > 0 && BagLibrary.IsFilled(kv.Key) && BagLibrary.DefId(kv.Key) == defId) return kv.Key;
            return null;
        }

        internal static readonly string Stone = "res:" + (int)ResourceType.Stone;
    }

    /// <summary>bags.wear: a military backpack is a worn Container in WornStorage with its capacity; goods go in (only
    /// what fits); dropped, it is a backpack in the world with the same contents, [T] looks inside, picking it up keeps
    /// them, wearing it again unpacks them; the worn contents, the full bag in the pack and the bag on the ground all
    /// survive a save round trip.</summary>
    class BagsWear : Scenario
    {
        public override string Id => "bags.wear";
        public override float Timeout => 90f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ItemsScenarios.OnFootAtPad(c, 6f);
            var snap = BagsScenarios.Strip(c);
            const string pack = "cloth_milpack", food = "food_can";
            g.Inventory.AddItem(pack);
            g.Inventory.Add(ResourceType.Stone, 12); g.Inventory.AddItem(food, 2);
            c.Fixture("a military backpack, 12 stone and 2 canned food in the pack");

            // ---- wear it: a worn container with its capacity
            c.Check(g.WearBag(pack), "WEAR puts the military backpack on");
            yield return null;
            var bag = g.WornBag("milpack");
            c.Check(g.Wearing("milpack") && bag && bag.worn && g.WornStorage.Contains(bag), "worn: a Container flagged worn, listed in WornStorage");
            if (!bag) { BagsScenarios.Restore(c, snap); yield break; }
            var spec = BagLibrary.Get("milpack");
            c.Check(Mathf.Approximately(bag.capacity, spec.capacity) && bag.capacity >= 45f, $"its capacity is the bag's ({bag.capacity:0} kg)");
            yield return null;
            c.Check(g.Prompt == null || !g.Prompt.Contains("MILITARY BACKPACK"), "the bag on your back is not offered to [E] as nearby storage");

            // ---- goods in
            float raw0 = g.CarriedWeight, eff0 = g.EffectiveLoad;
            c.Check(g.StowInBag(bag, BagsScenarios.Stone, 10) == 10 && g.StowInBag(bag, food, 2) == 2, "10 stone and 2 cans go into the bag");
            c.Check(bag.inventory.Get(ResourceType.Stone) == 10 && bag.inventory.GetItem(food) == 2 && g.Inventory.Get(ResourceType.Stone) == 2 && g.Inventory.GetItem(food) == 0,
                "they left the pack and are in the bag");
            c.Check(Mathf.Abs(g.CarriedWeight - raw0) < 0.01f, $"the carried weight counts the bag's contents ({raw0:0.0} -> {g.CarriedWeight:0.0} kg)");
            c.Metric("effective_load_drop", eff0 - g.EffectiveLoad, "kg");
            c.Check(g.EffectiveLoad < eff0 - 3f, $"on the hip belt the same goods weigh less on the back ({eff0:0.0} -> {g.EffectiveLoad:0.0} kg)");
            g.Inventory.Add(ResourceType.Stone, 80);
            c.Fixture("80 more stone in the pack");
            int took = g.StowInBag(bag, BagsScenarios.Stone, 80);
            c.Check(took > 0 && took < 80 && bag.Weight <= bag.capacity + 0.01f, $"only what fits goes in ({took} stone, {bag.Weight:0.0}/{bag.capacity:0} kg)");
            c.Check(g.StowInBag(bag, "cloth_backpack", 1) == 0 && bag.Fits("cloth_backpack", 1) == 0, "no bags inside bags");
            g.TakeFromBag(bag, BagsScenarios.Stone, took);
            g.Inventory.TrySpend(ResourceType.Stone, g.Inventory.Get(ResourceType.Stone));
            c.Check(bag.inventory.Get(ResourceType.Stone) == 10, "taken back out: 10 stone stay in the bag");
            c.Screenshot("worn");
            yield return null;

            // ---- drop it: a backpack in the world with the same contents
            var w = g.DropFromPack(pack, 1);
            yield return null;
            c.Check(w && BagLibrary.IsFilled(w.key) && BagLibrary.DefId(w.key) == "milpack", "dropped, it lies in the world as the backpack " + (w ? w.Label : "-"));
            c.Check(!g.Wearing("milpack") && !g.WornBag("milpack") && !g.WornStorage.Contains(bag) && g.Inventory.GetItem(pack) == 0, "off the body, out of WornStorage and the pack");
            if (!w) { BagsScenarios.Restore(c, snap); yield break; }
            c.Check(ItemsScenarios.HasMesh(w), "the world object has the backpack's mesh");
            var inside = new Inventory();
            BagLibrary.Contents(w.key, inside);
            c.Check(inside.Get(ResourceType.Stone) == 10 && inside.GetItem(food) == 2, "the dropped backpack holds the same 10 stone and 2 cans");
            c.Check(Mathf.Abs(w.Weight - (spec.weight + 10f * ItemCatalog.ResourceWeight(ResourceType.Stone) + 2f * ItemCatalog.Weight(food))) < 0.05f, $"it weighs the bag and its contents ({w.Weight:0.0} kg)");
            IInteractable use = w;
            c.Check(use.Prompt(g).Contains("LOOK INSIDE"), "[T] offers to look inside: " + use.Prompt(g));

            // ---- loot it where it lies
            use.Use(g, true);
            yield return null;
            var box = w.GetComponent<Container>();
            c.Check(box && g.Menus.IsOpen && box.inventory.GetItem(food) == 2, "[T] opens its contents in the loot window");
            if (box) { box.inventory.TakeItem(food, 1); g.Inventory.AddItem(food, 1); }
            BagLibrary.Contents(w.key, inside);
            c.Check(inside.GetItem(food) == 1 && g.Inventory.GetItem(food) == 1, "a can taken out of it: the bag on the ground holds one");
            g.Menus.Close();
            yield return null;

            // ---- save round trip of the bag on the ground
            var ds = new SaveData();
            g.SaveWorldItems(ds);
            var dsBack = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(ds));
            string groundKey = w.key;
            g.ClearWorldItems();
            g.LoadWorldItems(dsBack);
            yield return null;
            w = WorldItem.All.FirstOrDefault(x => x && x.key == groundKey);
            c.Check(w, "after a save round trip the backpack lies there with the same contents");
            if (!w) { BagsScenarios.Restore(c, snap); yield break; }

            // ---- pick it up: a full bag in the pack
            c.Check(g.PickUpItem(w), "[E] picks the backpack up");
            yield return null;
            var full = BagsScenarios.FullKeyIn(g.Inventory, "milpack");
            c.Check(full != null && ItemCatalog.Name(full).Contains("INSIDE"), "in the pack it is a full backpack: " + (full != null ? ItemCatalog.Name(full) : "-"));
            var item = JsonUtility.FromJson<ItemSave>(JsonUtility.ToJson(new ItemSave { id = full, count = 1 }));
            c.Check(item.id == full, "the full bag's key survives the save file as a pack item");

            // ---- wear it again: the contents unpack
            c.Check(full != null && g.WearBag(full), "WEAR on the full backpack");
            yield return null;
            bag = g.WornBag("milpack");
            c.Check(bag && bag.inventory.Get(ResourceType.Stone) == 10 && bag.inventory.GetItem(food) == 1 && g.Inventory.GetItem(pack) == 1 && BagsScenarios.FullKeyIn(g.Inventory, "milpack") == null,
                "worn again: its contents are in its storage, the plain backpack in the pack");

            // ---- save round trip of the worn bag
            var d = new SaveData();
            g.SaveBags(d);
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d));
            c.Check(back.blockBags.Any(l => l.StartsWith("w|milpack|")), $"the worn bag is saved ({back.blockBags.Count} lines)");
            g.LoadBags(back);
            yield return null; yield return null;
            var bag2 = g.WornBag("milpack");
            c.Check(bag2 && bag2 != bag && bag2.inventory.Get(ResourceType.Stone) == 10 && bag2.inventory.GetItem(food) == 1, "after loading, the worn backpack holds the same goods");

            // ---- take it off: a full bag in the pack
            c.Check(g.TakeOffBagToPack("milpack") && !g.WornBag("milpack") && BagsScenarios.FullKeyIn(g.Inventory, "milpack") != null && g.Inventory.GetItem(pack) == 0,
                "taken off, it goes into the pack with its contents");
            BagsScenarios.Restore(c, snap);
        }
    }

    /// <summary>bags.toolbelt: a tool belt takes tools only, in six slots; a tool on it stays on the hotbar, is drawn
    /// straight into the hand and goes back on the belt when put away. Luggage fills both hands (no tool), slows you
    /// and is set down again.</summary>
    class BagsToolbelt : Scenario
    {
        public override string Id => "bags.toolbelt";

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ItemsScenarios.OnFootAtPad(c, 6f);
            var snap = BagsScenarios.Strip(c);
            var tools = ToolLibrary.AllIds.Where(id => ItemCatalog.Category(id) == ItemCategory.Tool && ItemCatalog.Weight(id) <= 2f).Take(7).ToList();   // hand tools (not the sledgehammer)
            const string belt = "cloth_toolbelt";
            g.Inventory.AddItem(belt); g.Inventory.AddItem("food_can");
            foreach (var t in tools) g.Inventory.AddItem(t);
            c.Fixture($"a tool belt, a can and {tools.Count} tools in the pack");
            c.Check(g.WearBag(belt), "the tool belt goes on");
            yield return null;
            var b = g.WornBag("toolbelt");
            if (!c.Check(b && b.worn && g.WornStorage.Contains(b), "a worn container in WornStorage")) { BagsScenarios.Restore(c, snap); yield break; }
            c.Check(b.Fits("food_can", 1) == 0 && b.Fits(ItemIds.Wrench, 1) == 1, "it takes tools, not food");
            int stowed = 0;
            foreach (var t in tools) stowed += g.StowInBag(b, t, 1);
            c.Check(stowed == Mathf.Min(tools.Count, b.slots) && b.slots == 6, $"{stowed} of {tools.Count} tools fit its {b.slots} loops");
            var tool = tools[0];
            g.UpdateHotbarNow();
            c.Check(g.Inventory.GetItem(tool) == 0 && g.BeltCount(tool) == 1 && System.Array.IndexOf(g.Hotbar, tool) >= 0, $"{ItemCatalog.Name(tool)} hangs on the belt and keeps its hotbar slot");

            // ---- quick draw and back
            g.UseItem(tool);
            yield return null;
            c.Check(g.Player.Tool && g.Player.Tool.id == tool && g.BeltCount(tool) == 0, "picked on the hotbar, it comes off the belt into the hand");
            g.UseItem(tool);
            yield return null; yield return null;
            c.Check(!g.Player.Tool && g.BeltCount(tool) == 1 && g.Inventory.GetItem(tool) == 0, "put away, it goes back on the belt");

            // ---- save round trip
            var d = new SaveData();
            g.SaveBags(d);
            g.LoadBags(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d)));
            yield return null; yield return null;
            c.Check(g.BeltCount(tool) == 1 && g.WornBag("toolbelt").inventory.Items.Count(kv => kv.Value > 0) == stowed, "after a save round trip the tools hang on the belt");

            // ---- luggage: both hands
            const string suitcase = "cloth_suitcase";
            g.Inventory.AddItem(suitcase);
            c.Fixture("a suitcase in the pack");
            float speed0 = g.InjurySpeed;
            c.Check(g.WearBag(suitcase), "the suitcase is picked up in hand");
            yield return null;
            c.Check(g.WornBag("suitcase") && g.InjurySpeed < speed0 * 0.9f, $"carrying it slows the walk ({speed0:0.00} -> {g.InjurySpeed:0.00})");
            g.UseItem(tools[1]);
            yield return null; yield return null;
            c.Check(!g.Player.Tool, "with the suitcase in hand no tool can be held");
            var w = g.DropFromPack(suitcase, 1);
            yield return null;
            c.Check(w && !g.WornBag("suitcase") && !g.Wearing("suitcase"), "set down, it stands in the world");
            if (w) g.PickUpItem(w);
            BagsScenarios.Restore(c, snap);
        }
    }

    /// <summary>bags.back_strain: a proper backpack at a moderate load never strains the back, even running and jumping;
    /// overloaded (above the comfortable load, still able to run) at a run with jumps the strain rises, the HUD warns,
    /// then the back gives out (STRAINED BACK on the torso: no running or jumping, slower, less stamina); unloaded rest
    /// and a rest on the bed ease strain and injury.</summary>
    class BagsBackStrain : Scenario
    {
        public override string Id => "bags.back_strain";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ItemsScenarios.OnFootAtPad(c, 8f);
            var snap = BagsScenarios.Strip(c);
            MedMineScenarios.Healthy(c, "a sound back to start");
            g.SetBackStrain(0f);
            var p = g.Player;
            g.Inventory.AddItem("cloth_milpack");
            g.Inventory.Add(ResourceType.Stone, 30);
            c.Fixture("a military backpack and 30 stone (30 kg) in the pack");
            g.WearBag("cloth_milpack");
            yield return null;
            var bag = g.WornBag("milpack");
            if (!c.Check(bag && g.StowInBag(bag, BagsScenarios.Stone, 30) == 30, "30 kg in the military backpack")) { BagsScenarios.Restore(c, snap); yield break; }
            float comfort = g.ComfortLoad, cap = g.Stats.CarryCapacity;
            c.Metric("comfortable_load", comfort, "kg"); c.Metric("carry_capacity", cap, "kg");
            c.Metric("effective_load_moderate", g.EffectiveLoad, "kg");
            c.Check(g.EffectiveLoad < comfort, $"30 kg in a proper backpack is a comfortable load ({g.EffectiveLoad:0.0} of {comfort:0.0} kg)");

            // ---- moderate: run and jump, no strain
            float t0 = Time.time;
            yield return RunAndJump(c, p, 10f, () => false);
            c.Check(g.BackStrain < 0.01f && g.BackExcess <= 0f && !g.Stats.injuries.Any(i => i.type == Wound.Strain), $"after {Time.time - t0:0} s running and jumping the back is fine (strain {g.BackStrain * 100f:0} %)");

            // ---- overloaded, still able to run
            int add = Mathf.FloorToInt(cap - 2f - g.EffectiveLoad);
            g.Inventory.Add(ResourceType.Stone, Mathf.Max(0, add));
            c.Fixture($"{add} more stone loose in the pack (just under the carry capacity)");
            yield return null;
            c.Metric("effective_load_overloaded", g.EffectiveLoad, "kg");
            c.Check(!p.Encumbered && g.BackExcess > 0.2f, $"over the comfortable load but not overloaded ({g.EffectiveLoad:0.0} kg, {g.BackExcess * 100f:0} % over)");
            bool warned = false; float warnAt = -1f;
            t0 = Time.time;
            yield return RunAndJump(c, p, 100f, () =>
            {
                if (!warned && g.BackTag != null && g.BackStrain >= WastelandGame.StrainWarn) { warned = true; warnAt = Time.time - t0; }
                return g.Stats.injuries.Any(i => i.type == Wound.Strain);
            });
            c.Metric("seconds_to_warning", warnAt, "s");
            c.Metric("seconds_to_injury", Time.time - t0, "s");
            c.Check(warned, $"the HUD warned first ({g.BackTag ?? "-"} at {warnAt:0} s)");
            var inj = g.Stats.injuries.FirstOrDefault(i => i.type == Wound.Strain);
            if (!c.Check(inj != null && inj.zone == BodyZone.Torso, $"the back gave out: {Injury.ZoneNames[(int)BodyZone.Torso]}: {Injury.WoundNames[(int)Wound.Strain]} after {Time.time - t0:0} s")) { BagsScenarios.Restore(c, snap); yield break; }
            c.Check(!g.CanRunInjured && !g.CanJumpInjured && g.InjurySpeed < 0.8f && g.Stats.TorsoPain > 0.4f,
                $"no running or jumping, slower ({g.InjurySpeed:0.00}), less breath (torso pain {g.Stats.TorsoPain:0.00})");
            c.Screenshot("strained");
            yield return null;

            // ---- rest: unloaded, then a rest on the bed
            g.Inventory.TrySpend(ResourceType.Stone, g.Inventory.Get(ResourceType.Stone));
            c.Fixture("the loose stone dropped");
            float s0 = g.BackStrain, sev0 = inj.severity;
            yield return SurvivalKit.GameSeconds(5f);
            c.Check(g.BackStrain < s0 && inj.severity < sev0, $"standing at rest unloaded, strain and injury ease ({s0 * 100f:0} -> {g.BackStrain * 100f:0} %, {sev0:0.000} -> {inj.severity:0.000})");
            s0 = g.BackStrain; sev0 = inj.severity;
            g.Sleep();
            c.Fixture("a rest (WastelandGame.Sleep)");
            c.Check(g.BackStrain < s0 - 0.1f || g.BackStrain == 0f, $"rest eases the strain ({s0 * 100f:0} -> {g.BackStrain * 100f:0} %)");
            c.Check(!g.Stats.injuries.Contains(inj) || inj.severity < sev0 - 0.1f, $"and mends the back ({sev0:0.00} -> {(g.Stats.injuries.Contains(inj) ? inj.severity : 0f):0.00})");
            BagsScenarios.Restore(c, snap);
        }

        /// <summary>Run back and forth on the pad with a jump every 1.2 s, breath kept up (disclosed), until
        /// <paramref name="done"/> or <paramref name="seconds"/> of game time.</summary>
        static IEnumerator RunAndJump(ScenarioContext c, PlayerCharacter p, float seconds, System.Func<bool> done)
        {
            var g = c.Game;
            c.Fixture("stamina kept full while running");
            float yaw = p.transform.eulerAngles.y, t0 = Time.time, flip = 0f, hop = 0f;
            p.viewYaw = yaw; p.run = true; p.moveInput = new Vector2(0f, 1f);
            while (Time.time - t0 < seconds && !done())
            {
                g.Stats.stamina = g.Stats.MaxStamina;
                if ((flip += Time.deltaTime) > 2f) { flip = 0f; yaw += 180f; p.viewYaw = yaw; }
                if ((hop += Time.deltaTime) > 1.2f) { hop = 0f; p.jump = true; }
                yield return null;
            }
            p.run = false; p.moveInput = Vector2.zero;
            yield return SurvivalKit.GameSeconds(0.3f);
        }
    }
}
