using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the Items block: things out of the pack become objects in the world (dropped,
    /// placed, picked up, saved) and every gain or loss of the pack shows in the HUD item feed.</summary>
    public static class ItemsScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new ItemsWorld();
            yield return new ItemsFeed();
        }

        /// <summary>On foot on a clear pad near the start, facing along it (disclosed).</summary>
        public static IEnumerator OnFootAtPad(ScenarioContext c, float radius)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (TestWorld.Pad(radius, out var pad))
            {
                g.Player.Teleport(pad + Vector3.up * 0.1f, 0f);
                c.Fixture("on foot on a clear pad near the start");
            }
            else c.Note("no clear pad: staying where the player is");
            yield return new WaitForSeconds(0.6f);
        }

        public static bool HasMesh(WorldItem w)
        {
            var mf = w ? w.GetComponentInChildren<MeshFilter>() : null;
            return mf && mf.sharedMesh && mf.sharedMesh.vertexCount > 0 && w.GetComponentInChildren<MeshRenderer>().enabled;
        }
    }

    /// <summary>The held tool and a food item dropped out of the pack lie in the world as their own meshes with light
    /// bodies that settle; [E] (the item's IInteractable) takes one back; the place API sets one on a table and it stays
    /// there; a save round trip (through JSON) puts both back where they were.</summary>
    class ItemsWorld : Scenario
    {
        public override string Id => "items.world";

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ItemsScenarios.OnFootAtPad(c, 6f);
            var terrain = MadMax.World.DeformableTerrain.Instance;
            const string food = "food_can";
            if (g.Inventory.GetItem(ItemIds.Wrench) <= 0) { g.Inventory.AddItem(ItemIds.Wrench); c.Fixture("a wrench in the pack"); }
            if (g.Inventory.GetItem(food) < 2) { g.Inventory.AddItem(food, 2); c.Fixture("2 canned food in the pack"); }
            if (g.Inventory.Get(ResourceType.Scrap) < 5) { g.Inventory.Add(ResourceType.Scrap, 5); c.Fixture("5 scrap in the pack"); }
            g.Player.Equip(ToolLibrary.Create(ItemIds.Wrench, g.propMaterial));
            c.Fixture("the wrench in hand");
            yield return null;

            // ---- drop the tool in hand and one can
            int wrenches = g.Inventory.GetItem(ItemIds.Wrench), cans = g.Inventory.GetItem(food), scrap = g.Inventory.Get(ResourceType.Scrap);
            var tool = g.DropHeldTool();
            c.Check(tool && tool.key == ItemIds.Wrench && ItemsScenarios.HasMesh(tool), "the dropped wrench is an object in the world with its mesh");
            c.Check(g.Inventory.GetItem(ItemIds.Wrench) == wrenches - 1 && !g.Player.Tool, "one wrench left the pack and the hand");
            var can = g.DropFromPack(food, 1);
            c.Check(can && can.key == food && can.count == 1 && ItemsScenarios.HasMesh(can), "a dropped can is an object in the world with its mesh");
            c.Check(g.Inventory.GetItem(food) == cans - 1, $"the pack holds one can less ({cans} -> {g.Inventory.GetItem(food)})");
            var crate = g.DropFromPack("res:" + (int)ResourceType.Scrap, 5);
            c.Check(crate && crate.IsResource && crate.count == 5 && ItemsScenarios.HasMesh(crate) && g.Inventory.Get(ResourceType.Scrap) == scrap - 5, "5 scrap dropped as a crate of scrap");
            if (!tool || !can) yield break;
            c.Check(tool.Body && tool.Body.mass <= 8f && can.Body.mass <= 8f, $"light bodies (wrench {tool.Body.mass:0.0} kg, can {can.Body.mass:0.0} kg)");
            yield return new WaitForSeconds(2f);
            foreach (var w in new[] { tool, can })
            {
                var p = w.transform.position;
                float ground = terrain.Height(p.x, p.z);
                c.Metric(w.key + "_rest_above_ground", p.y - ground, "m");
                c.Check(p.y > ground - 0.1f && p.y < ground + 0.6f, $"{w.key} came to rest on the ground ({p.y - ground:0.00} m)");
                c.Check(w.Body.linearVelocity.magnitude < 0.2f, $"{w.key} settled ({w.Body.linearVelocity.magnitude:0.00} m/s)");
            }
            c.Screenshot("dropped");
            yield return null;

            // ---- [E] takes the can back
            IInteractable use = can;
            c.Check(use.Prompt(g) != null && use.Prompt(g).Contains("PICK UP"), "the can offers [E] " + use.Prompt(g));
            use.Use(g, false);
            yield return null;
            c.Check(!can && g.Inventory.GetItem(food) == cans, "picked up: the can is back in the pack and gone from the world");

            // ---- the place API: one can on a table (beside the player, clear of what was dropped in front)
            var side = g.Player.transform.right; side.y = 0f; side.Normalize();
            var at = g.Player.transform.position + side * 2.5f;
            at.y = terrain.Height(at.x, at.z);
            var table = FurnitureLibrary.Spawn("table", g.Build.Structures, g.Build.Structures.InverseTransformPoint(at), Quaternion.identity, g.propMaterial);
            c.Fixture("a table spawned 2.5 m beside the player");
            yield return null;
            Physics.SyncTransforms();
            var tb = table ? table.GetComponent<Renderer>().bounds : default;
            RaycastHit top = default;
            bool onTable = table && Physics.Raycast(new Vector3(tb.center.x, tb.max.y + 1f, tb.center.z), Vector3.down, out top, 3f, ~0, QueryTriggerInteraction.Ignore)
                           && top.collider.GetComponentInParent<Placeable>() == table;
            if (!c.Check(onTable, "the table's top is under a ray from above")) yield break;
            c.Check(g.BeginPlaceItem(food) && g.PlacingItem && g.PlacingKey == food, "PLACE starts a preview for the can");
            yield return null;
            c.Check(g.CancelPlacing() && !g.PlacingItem, "the preview can be put away");
            var placed = g.PlaceItemAt(food, 1, top.point, top.normal, 30f, top.collider);
            c.Check(placed && placed.placed && g.Inventory.GetItem(food) == cans - 1, "the place API sets a can down from the pack");
            if (!placed) yield break;
            float tableTop = top.point.y;
            yield return new WaitForSeconds(1.5f);
            c.Metric("placed_height_above_ground", placed.transform.position.y - at.y, "m");
            c.Check(Mathf.Abs(placed.transform.position.y - tableTop) < 0.05f && tableTop - at.y > 0.3f, $"the can stands on the table top ({placed.transform.position.y - tableTop:0.000} m off, table {tableTop - at.y:0.00} m high)");
            c.Screenshot("placed");
            yield return null;

            // ---- save round trip (through JSON, like a save file)
            var savedPos = placed.transform.position; var toolPos = tool.transform.position;
            var d = new SaveData();
            g.SaveWorldItems(d);
            c.Check(d.blockItems.Count == WorldItem.All.Count && d.blockItems.Count >= 3, $"every world item is saved ({d.blockItems.Count} lines)");
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d));
            g.ClearWorldItems();
            c.Check(WorldItem.All.Count == 0, "world items cleared");
            g.LoadWorldItems(back);
            yield return new WaitForSeconds(1f);
            var placed2 = WorldItem.All.FirstOrDefault(w => w && w.key == food && w.placed);
            var tool2 = WorldItem.All.FirstOrDefault(w => w && w.key == ItemIds.Wrench);
            c.Check(placed2 && Vector3.Distance(placed2.transform.position, savedPos) < 0.05f && ItemsScenarios.HasMesh(placed2), "after loading, the placed can is back on the table");
            c.Check(tool2 && Vector3.Distance(tool2.transform.position, toolPos) < 0.3f, "after loading, the wrench is back where it lay");
            c.Check(placed2 && !placed2.Body.isKinematic, "loaded items near the player are awake");

            // tidy up: everything back into the pack
            foreach (var w in WorldItem.All.ToList()) g.PickUpItem(w);
            yield return null;
            c.Check(WorldItem.All.Count == 0 && g.Inventory.GetItem(food) == cans && g.Inventory.GetItem(ItemIds.Wrench) == wrenches, "everything picked back up");
            if (table) Object.Destroy(table.gameObject);
        }
    }

    /// <summary>Gains and losses of the pack through the normal inventory APIs and a real pickup become feed rows with
    /// their labels; the same thing with the same label merges within the window, later ones get a row of their own; a
    /// container's inventory does not feed; rows expire.</summary>
    class ItemsFeed : Scenario
    {
        public override string Id => "items.feed";

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ItemsScenarios.OnFootAtPad(c, 4f);
            yield return null;                                                                    // the feed hooks in on a playing frame
            ItemFeed.Clear();
            var inv = g.Inventory;
            if (inv.Get(ResourceType.Scrap) < 10) { inv.Add(ResourceType.Scrap, 10); c.Fixture("10 scrap in the pack"); }
            ItemFeed.Clear();

            using (WastelandGame.FeedSource("REWARD")) { inv.Add(ResourceType.Scrap, 3); inv.Add(ResourceType.Scrap, 2); }
            var reward = ItemFeed.Find(null, ResourceType.Scrap, "REWARD");
            c.Check(reward != null && reward.amount == 5 && reward.text == "+5 SCRAP", "two rewards merge into one row: " + (reward != null ? reward.text + " " + reward.source : "none"));
            using (Inventory.Source("BOUGHT", "PAID")) { inv.AddItem("food_can", 1); inv.TrySpend(ResourceType.Scrap, 4); }
            var bought = ItemFeed.Find("food_can", ResourceType.None, "BOUGHT");
            var paid = ItemFeed.Find(null, ResourceType.Scrap, "PAID", false);
            c.Check(bought != null && bought.amount == 1, "a purchase shows +1 CANNED FOOD, BOUGHT");
            c.Check(paid != null && paid.amount == -4 && paid.text == "-4 SCRAP", "its price shows -4 SCRAP, PAID");
            c.Check(Inventory.Label(1) == null && Inventory.Label(-1) == null, "the label ends with its block");
            inv.AddItem(ItemIds.Wrench);
            var plain = ItemFeed.Find(ItemIds.Wrench, ResourceType.None);
            c.Check(plain != null && plain.source == null && plain.amount == 1, "an unlabelled gain still shows: " + (plain != null ? plain.text : "none"));
            int rows = ItemFeed.Rows.Count;
            var box = new Inventory();
            box.Add(ResourceType.Scrap, 10); box.AddItem("food_can", 3);
            c.Check(ItemFeed.Rows.Count == rows, "a container's inventory does not feed the HUD");
            c.Screenshot("feed");
            yield return null;

            // past the merge window: a new row of its own
            yield return new WaitForSecondsRealtime(ItemFeed.MergeWindow + 0.2f);
            using (WastelandGame.FeedSource("REWARD")) inv.Add(ResourceType.Scrap, 1);
            var rewards = ItemFeed.Rows.Where(r => r.id == null && r.res == ResourceType.Scrap && r.source == "REWARD").ToList();
            c.Check(rewards.Count == 2 && rewards[0].amount == 1 && rewards[1].amount == 5, $"after {ItemFeed.MergeWindow} s the same reward gets a new row ({rewards.Count} rows)");
            c.Check(ItemFeed.Rows.Count <= ItemFeed.MaxRows, $"at most {ItemFeed.MaxRows} rows ({ItemFeed.Rows.Count})");

            // real paths: a dropped stack and an auto-collected pickup
            ItemFeed.Clear();
            var w = g.DropFromPack("res:" + (int)ResourceType.Scrap, 2);
            var dropped = ItemFeed.Find(null, ResourceType.Scrap, "DROPPED", false);
            c.Check(w && dropped != null && dropped.amount == -2, "dropping shows -2 SCRAP, DROPPED");
            if (w) g.PickUpItem(w);
            var picked = ItemFeed.Find(null, ResourceType.Scrap, "PICKED UP");
            c.Check(picked != null && picked.amount == 2, "taking it back shows +2 SCRAP, PICKED UP");
            if (MadMax.World.PickupSystem.Instance)
            {
                int wood = inv.Get(ResourceType.Wood);
                var feet = g.Player.transform.position;
                MadMax.World.PickupSystem.Instance.Spawn(ResourceType.Wood, 2, feet + Vector3.up * 0.6f, Vector3.zero);
                c.Fixture("a 2-wood pickup at the player's feet");
                yield return new WaitForSeconds(1.5f);
                c.Check(inv.Get(ResourceType.Wood) == wood, "a pickup at the feet is not collected by itself");
                WorldItem pile = null;
                foreach (var it in WorldItem.All) if (it && it.key == "res:" + (int)ResourceType.Wood && (it.transform.position - feet).sqrMagnitude < 4f) pile = it;
                c.Check(pile && pile.GetComponentInChildren<MeshRenderer>(), "the pickup lies there as a visible item");
                if (pile) g.PickUpItem(pile);
                var woodRow = ItemFeed.Find(null, ResourceType.Wood, "PICKED UP");
                c.Check(inv.Get(ResourceType.Wood) == wood + 2 && woodRow != null && woodRow.amount == 2, "[E] on the pickup shows +2 WOOD, PICKED UP");
            }
            else c.Note("no pickup system");

            // rows run out (anything the world adds meanwhile is newer)
            float lastPush = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(ItemFeed.Life + ItemFeed.SlideOut + 0.2f);
            ItemFeed.Tick(Time.unscaledTime);
            int stale = ItemFeed.Rows.Count(r => r.last <= lastPush);
            c.Check(stale == 0, $"rows slide out after {ItemFeed.Life} s ({stale} left)");
        }
    }
}
