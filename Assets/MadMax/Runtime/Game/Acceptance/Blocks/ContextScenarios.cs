using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the context block: right-click menus on things in the world and in the pack, the
    /// loot window (pack, storage tabs, floor; drag and drop) and the respawn chooser.</summary>
    public static class ContextScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new UiContextMenu();
            yield return new UiLootWindow();
            yield return new UiRespawnChoice();
            yield return new UiLootOverlay();
        }

        public static string Labels(List<ContextOption> l) => string.Join(", ", l.Select(o => o.label + (o.blocked != null ? " (" + o.blocked + ")" : "")));

        /// <summary>Open the menu on <paramref name="target"/>, choose the option starting with <paramref name="prefix"/>
        /// (the call a click on it makes) and wait for <paramref name="done"/>. The player walks up when out of reach.</summary>
        public static IEnumerator Choose(ScenarioContext c, Component target, string prefix, System.Func<bool> done, float seconds, Waited w)
        {
            var g = c.Game;
            w.ok = false;
            if (!g.OpenContextFor(target)) { c.Note("no menu opened on " + target.name); yield break; }
            var labels = g.Menus.PopupLabels();
            if (!g.Menus.ChoosePopup(prefix)) { c.Note("no option '" + prefix + "' in: " + string.Join(", ", labels)); g.Menus.ClosePopup(); yield return null; yield break; }
            yield return SurvivalKit.Until(done, seconds, w);
        }
    }

    /// <summary>Right-click menus: a bed offers sleep / rest, set spawn and (owned) dismantle; a chest out of reach is walked
    /// up to and OPEN opens its loot window; SIT seats the player on a chair; a car offers DRIVE and, inside, GET OUT; a
    /// dropped can offers PICK UP, which puts it back in the pack; a stallkeeper offers TALK and TRADE, TALK opens the
    /// conversation. Every option runs the call its key does.</summary>
    class UiContextMenu : Scenario
    {
        public override string Id => "ui.context_menu";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var P = g.Player; var w = new Waited(); var got = new Waited(); var at = new Vector3[1];
            yield return SurvivalKit.OnFoot(c, 10f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); yield break; }
            var fwd = P.transform.forward; var side = P.transform.right;
            var bed = SurvivalKit.Piece(g, "bed", at[0] + fwd * 3f + side * 2.5f, -fwd);
            var chest = SurvivalKit.Piece(g, "chest", at[0] + fwd * 3f - side * 2.5f, -fwd);
            var chair = SurvivalKit.Piece(g, "chair", at[0] - fwd * 3f, fwd);
            if (g.Inventory.GetItem(ItemIds.ClawHammer) <= 0) g.Inventory.AddItem(ItemIds.ClawHammer);
            c.Fixture("a bed, a chest and a chair built (owned) around the player on a clear pad; a claw hammer in the pack");
            if (!c.Check(bed && chest && chair, "the pieces stand")) yield break;
            yield return SurvivalKit.GameSeconds(0.4f);

            // ---- a bed
            var opts = g.ContextOptionsFor(bed);
            c.Note("bed: " + ContextScenarios.Labels(opts));
            c.Check(opts.Any(o => (o.label.StartsWith("SLEEP") || o.label.StartsWith("REST")) && o.blocked == null), "the bed offers SLEEP / REST");
            c.Check(opts.Any(o => o.label.StartsWith("SET SPAWN")), "the bed offers SET SPAWN");
            c.Check(opts.Any(o => o.label.StartsWith("DISMANTLE") && o.blocked == null), "an own piece offers DISMANTLE (claw hammer in the pack)");

            // ---- a chest out of reach: walk up, open
            var box = chest.GetComponent<Container>();
            float d0 = Vector3.Distance(P.transform.position, chest.transform.position);
            yield return ContextScenarios.Choose(c, chest, "OPEN", () => g.Menus.Current == MenuSystem.Page.Container, 10f, w);
            c.Metric("chest_walk", w.seconds, "s");
            c.Check(w.ok && g.Menus.LootCurrent != null && g.Menus.LootCurrent.box == box, $"OPEN on the chest {d0:0.0} m away walks up and opens its loot window ({w.seconds:0.0} s)");
            c.Screenshot("chest_open");
            yield return null;
            if (g.Menus.IsOpen) g.Menus.Close();
            yield return SurvivalKit.GameSeconds(0.2f);

            // ---- a chair: sit
            var seat = chair.GetComponentInChildren<Seat>();
            yield return ContextScenarios.Choose(c, chair, "SIT", () => P.Sitting, 10f, w);
            c.Check(w.ok && P.SeatedOn == seat, "SIT on the chair seats the player on it");
            if (P.Sitting) P.StandUp();
            yield return SurvivalKit.GameSeconds(0.3f);

            // ---- a dropped can: pick up
            if (g.Inventory.GetItem("food_can") < 1) { g.Inventory.AddItem("food_can", 1); c.Fixture("a can of food in the pack"); }
            int cans = g.Inventory.GetItem("food_can");
            var can = g.DropFromPack("food_can", 1);
            yield return SurvivalKit.GameSeconds(1f);
            if (c.Check(can, "a can lies on the ground"))
            {
                opts = g.ContextOptionsFor(can);
                c.Note("can: " + ContextScenarios.Labels(opts));
                c.Check(opts.Any(o => o.label.StartsWith("PICK UP")), "the can offers PICK UP");
                yield return ContextScenarios.Choose(c, can, "PICK UP", () => !can, 6f, w);
                c.Check(w.ok && g.Inventory.GetItem("food_can") == cans, "PICK UP puts it back in the pack");
            }

            // ---- a stallkeeper: talk / trade
            var vendorAt = SurvivalKit.Ground(P.transform.position + P.transform.forward * 1.5f);
            var npc = MadMax.Npc.Npc.Spawn(NpcProfile.Make("test:ctx", NpcRole.Stallkeeper, 4242, "food"), vendorAt + Vector3.up * 0.05f, 180f, null, g.propMaterial);
            c.Fixture("a food stallkeeper set down in front of the player");
            yield return SurvivalKit.GameSeconds(0.5f);
            if (c.Check(npc, "the stallkeeper stands there"))
            {
                opts = g.ContextOptionsFor(npc);
                c.Note("npc: " + ContextScenarios.Labels(opts));
                c.Check(opts.Any(o => o.label.StartsWith("TALK")), "the stallkeeper offers TALK");
                c.Check(opts.Any(o => o.label.StartsWith("TRADE") || o.label.Contains("CLOSED")), "and TRADE (or says when the stall opens)");
                yield return ContextScenarios.Choose(c, npc, "TALK", () => g.Menus.Current == MenuSystem.Page.Talk, 6f, w);
                c.Check(w.ok, "TALK opens the conversation");
                if (g.Menus.IsOpen) g.Menus.Close();
                Object.Destroy(npc.gameObject);
            }
            yield return SurvivalKit.GameSeconds(0.2f);

            // ---- a car: drive, then get out from the car's own menu
            var car = g.Fleet.FirstOrDefault(v => v && v.driveable && !v.GetComponent<InteriorSpace>() && !v.GetComponent<BikeBalance>() && !v.GetComponent<FlightModel>());
            if (!car) c.Block("no fleet car to drive");
            else
            {
                var stand = SurvivalKit.Ground(car.transform.position + car.transform.right * 2.6f);
                P.Teleport(stand + Vector3.up * 0.1f, car.transform.eulerAngles.y - 90f);
                c.Fixture("the player set down beside " + car.name);
                yield return SurvivalKit.GameSeconds(0.4f);
                opts = g.ContextOptionsFor(car);
                c.Note("car: " + ContextScenarios.Labels(opts));
                c.Check(opts.Any(o => o.label.StartsWith("DRIVE")), "the car offers DRIVE");
                yield return ContextScenarios.Choose(c, car, "DRIVE", () => g.Current == car && !g.Boarding, 12f, w);
                c.Check(w.ok, "DRIVE puts the player at the wheel");
                if (g.Current == car)
                {
                    yield return SurvivalKit.GameSeconds(0.3f);
                    opts = g.ContextOptionsFor(car);
                    c.Check(opts.Any(o => o.label == "GET OUT") && opts.Any(o => o.label == "LIGHTS"), "at the wheel the menu is the car's own: " + ContextScenarios.Labels(opts));
                    yield return ContextScenarios.Choose(c, car, "GET OUT", () => !g.Current && !g.Boarding, 10f, w);
                    c.Check(w.ok, "GET OUT gets the player out");
                }
            }
            if (g.Menus.IsOpen) g.Menus.Close();
            foreach (var p in new[] { bed, chest, chair }) if (p) Object.Destroy(p.gameObject);
        }
    }

    /// <summary>The loot window: a chest opened with a crate and a searchable cupboard within reach shows tabs for all of
    /// them and the FLOOR; goods move pack → chest → crate → floor → pack through the drag-and-drop call (whole stacks,
    /// Ctrl = one, onto a tab), a cupboard is searched into its own side and taken from, a row's menu splits a stack,
    /// a nearly full crate takes only what fits — and the totals over pack, storages and floor never change.</summary>
    class UiLootWindow : Scenario
    {
        public override string Id => "ui.loot_window";
        public override float Timeout => 90f;

        static Dictionary<string, int> Totals(WastelandGame g, Inventory a, Inventory b, Inventory leftovers)
        {
            var d = SurvivalKit.Ledger(g.Inventory, a, b, leftovers);
            foreach (var kv in g.FloorStacks(g.Player.transform.position, 6f)) { d.TryGetValue(kv.Key, out int h); d[kv.Key] = h + kv.Value; }
            return d;
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var P = g.Player; var m = g.Menus; var got = new Waited(); var at = new Vector3[1];
            yield return SurvivalKit.OnFoot(c, 8f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); yield break; }
            var fwd = P.transform.forward; var side = P.transform.right;
            var chestP = SurvivalKit.Piece(g, "chest", at[0] + fwd * 1.3f, -fwd);
            var crateP = SurvivalKit.Piece(g, "crate", at[0] + side * 1.4f, -side);
            var spotGo = new GameObject("TestCupboard");
            spotGo.transform.position = SurvivalKit.Ground(at[0] - side * 1.4f) + Vector3.up * 0.5f;
            spotGo.AddComponent<BoxCollider>().size = Vector3.one * 0.6f;
            var spot = spotGo.AddComponent<Lootable>();
            spot.key = "test:ctx:cupboard:" + Time.frameCount; spot.table = "kitchen"; spot.title = "CUPBOARD";
            spot.extra.Add("food_jam");
            var inv = g.Inventory;
            inv.Add(ResourceType.Scrap, 20); inv.AddItem("food_can", 3);
            c.Fixture("a chest in front, a crate to the right, an unsearched kitchen cupboard (with a jar of jam for sure) to the left; 20 scrap and 3 cans granted");
            var chest = chestP ? chestP.GetComponent<Container>() : null; var crate = crateP ? crateP.GetComponent<Container>() : null;
            if (!c.Check(chest && crate, "chest and crate stand")) yield break;
            yield return SurvivalKit.GameSeconds(0.4f);
            string scrap = "res:" + (int)ResourceType.Scrap;

            m.OpenContainer(chest);
            yield return null;
            c.Check(m.Current == MenuSystem.Page.Container && m.LootCurrent != null && m.LootCurrent.box == chest, "the chest opens in the loot window");
            var srcs = m.LootSources;
            c.Note("tabs: " + string.Join(", ", srcs.Select(s => s.Title)));
            var crateSrc = srcs.FirstOrDefault(s => s.box == crate); var spotSrc = srcs.FirstOrDefault(s => s.spot == spot);
            c.Check(crateSrc != null && spotSrc != null && srcs.Any(s => s.floor), "tabs for the chest, the crate within reach, the cupboard and the floor");
            var before = Totals(g, chest.inventory, crate.inventory, spot.Leftovers(true));

            // pack -> chest: drag the scrap row across (whole stack), one can with Ctrl
            int packScrap = inv.Get(ResourceType.Scrap);
            int moved = m.LootDropRow(m.LootRow(0, scrap), m.LootCurrent, false);
            c.Check(moved == packScrap && chest.inventory.Get(ResourceType.Scrap) == packScrap && inv.Get(ResourceType.Scrap) == 0, $"dragging the scrap onto the chest moves the stack ({moved})");
            int packCans = inv.GetItem("food_can");
            moved = m.LootDropRow(m.LootRow(0, "food_can"), m.LootCurrent, true);
            c.Check(moved == 1 && inv.GetItem("food_can") == packCans - 1 && chest.inventory.GetItem("food_can") == 1, "Ctrl-drag moves one can");
            c.Screenshot("loot_window");
            yield return null;

            // chest -> crate: dropped on the crate's tab
            crateSrc = m.LootSources.First(s => s.box == crate);
            moved = m.LootDropRow(m.LootRow(1, scrap), crateSrc, false);
            c.Check(moved > 0 && crate.inventory.Get(ResourceType.Scrap) == moved && chest.inventory.Get(ResourceType.Scrap) == packScrap - moved, $"dragging the chest's scrap onto the crate tab moves it there ({moved})");

            // pack -> floor (off the sides), floor tab -> pack
            int cans = inv.GetItem("food_can");
            moved = m.LootDropRow(m.LootRow(0, "food_can"), LootSource.Floor, false);
            c.Check(moved == cans && inv.GetItem("food_can") == 0, $"dragged off the window, the cans go on the floor ({moved})");
            yield return SurvivalKit.GameSeconds(0.6f);
            c.Check(g.FloorCount("food_can", P.transform.position, MenuSystem.FloorReach) == cans, "and lie there as world items");
            int floorTab = m.LootSources.ToList().FindIndex(s => s.floor);
            m.SelectLootTab(floorTab);
            c.Check(m.LootCurrent != null && m.LootCurrent.floor && m.LootRow(1, "food_can") >= 0, "the FLOOR tab lists them");
            moved = m.LootDropRow(m.LootRow(1, "food_can"), LootSource.Pack, false);
            c.Check(moved == cans && inv.GetItem("food_can") == cans && g.FloorCount("food_can", P.transform.position, MenuSystem.FloorReach) == 0, "dragged back onto the pack, they are picked up");

            // the cupboard: searched as it is looked into, taken from
            int spotTab = m.LootSources.ToList().FindIndex(s => s.spot == spot);
            m.SelectLootTab(spotTab);
            c.Check(Lootable.Searched.Contains(spot.key) && spot.Leftovers(false) != null && spot.Leftovers(false).GetItem("food_jam") >= 1, "looking into the cupboard searches it into its own side");
            var spotStock = SurvivalKit.Ledger(spot.Leftovers(false));
            int jam = inv.GetItem("food_jam");
            int took = m.LootTakeAll();
            c.Check(took == spotStock.Values.Sum() && !spot.HasLeftovers && inv.GetItem("food_jam") > jam, $"T takes everything ({took})");
            // the search's finds are new goods: count them into the balance
            foreach (var kv in spotStock) { before.TryGetValue(kv.Key, out int h); before[kv.Key] = h + kv.Value; }

            // a row's menu: put half of the cans in the chest
            m.SelectLootTab(0);
            int row = m.LootRow(0, "food_can");
            c.Check(m.OpenRowMenu(row, new Vector2Int(100, 60)) && m.PopupOpen, "RMB on a pack row opens its menu: " + string.Join(", ", m.PopupLabels()));
            int chestCans = chest.inventory.GetItem("food_can");
            c.Check(m.ChoosePopup("PUT HALF") && chest.inventory.GetItem("food_can") == chestCans + cans / 2, "PUT HALF splits the stack into the chest");
            yield return null;
            var diff0 = SurvivalKit.Diff(before, Totals(g, chest.inventory, crate.inventory, spot.Leftovers(false)));
            c.Check(diff0 == null, "every move so far balances (pack + chest + crate + cupboard + floor): " + (diff0 ?? "ok"));

            // a nearly full crate takes only what fits
            crate.inventory.Add(ResourceType.Stone, Mathf.FloorToInt(crate.capacity - crate.Weight - 2f));
            c.Fixture($"the crate filled with stone to within 2 kg of its {crate.capacity:0} kg");
            before = Totals(g, chest.inventory, crate.inventory, spot.Leftovers(false));
            m.SelectLootTab(0);
            moved = m.LootDropRow(m.LootRow(1, scrap), m.LootSources.First(s => s.box == crate), false);
            c.Check(crate.Weight <= crate.capacity + 0.001f && moved <= 3, $"a full crate takes only what fits ({moved} scrap, {crate.Weight:0.0}/{crate.capacity:0} kg)");
            var diff = SurvivalKit.Diff(before, Totals(g, chest.inventory, crate.inventory, spot.Leftovers(false)));
            c.Check(diff == null, "pack + chest + crate + cupboard + floor balance: " + (diff ?? "ok"));

            m.Close();
            yield return null;
            Object.Destroy(chestP.gameObject); Object.Destroy(crateP.gameObject); Object.Destroy(spotGo);
            Lootable.Left.Remove(spot.key);
        }
    }

    /// <summary>The respawn chooser: with a bed built and set as the spawn point (its context menu's SET SPAWN), dying
    /// shows the list with that bed first; choosing it wakes the player there (half health); dying again and choosing
    /// another place wakes the player there, and the choice is remembered for the save.</summary>
    class UiRespawnChoice : Scenario
    {
        public override string Id => "ui.respawn_choice";
        public override float Timeout => 90f;

        static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var P = g.Player; var m = g.Menus; var w = new Waited(); var got = new Waited(); var at = new Vector3[1];
            yield return SurvivalKit.OnFoot(c, 8f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); yield break; }
            var bed = SurvivalKit.Piece(g, "bed", at[0] + P.transform.forward * 1.6f, -P.transform.forward);
            c.Fixture("a bed built in front of the player");
            if (!c.Check(bed, "the bed stands")) yield break;
            yield return SurvivalKit.GameSeconds(0.3f);
            yield return ContextScenarios.Choose(c, bed, "SET SPAWN", () => !g.ContextWalking, 8f, w);
            yield return SurvivalKit.GameSeconds(0.2f);

            // ---- die: the list, the bed first
            g.Vitals.Hurt(100000f, "TEST");
            c.Fixture("the player killed outright (100000 damage)");
            yield return SurvivalKit.Until(() => g.AwaitingRespawn && m.Current == MenuSystem.Page.Respawn, 10f, w);
            if (!c.Check(w.ok, $"after the fall the respawn list comes up ({w.seconds:0.0} s)")) yield break;
            var pts = g.RespawnPoints();
            var labels = m.Labels();
            c.Note("points: " + string.Join(", ", labels));
            c.Check(pts.Count >= 2 && pts[0].kind == "bed" && pts[0].piece == bed.Id && labels[0] == pts[0].label, "the bed set as spawn is offered first: " + labels[0]);
            c.Check(pts.Any(p => p.kind == "start"), "the start road is offered too");
            c.Screenshot("respawn_list");
            yield return null;
            var bedAt = pts[0].at;
            c.Check(m.Press(labels[0]), "choosing it (Enter)");
            yield return SurvivalKit.Until(() => !g.AwaitingRespawn && !P.Ragdolled && !m.IsOpen, 5f, w);
            c.Check(w.ok && Flat(P.transform.position, bedAt) < 1.5f, $"the player wakes at the bed ({Flat(P.transform.position, bedAt):0.00} m)");
            c.Check(Mathf.Abs(g.Stats.health - g.Stats.MaxHealth * 0.5f) < 1f, "with half health");
            yield return SurvivalKit.GameSeconds(1f);

            // ---- die again: another place
            g.Vitals.Hurt(100000f, "TEST");
            c.Fixture("killed again");
            yield return SurvivalKit.Until(() => g.AwaitingRespawn && m.Current == MenuSystem.Page.Respawn, 10f, w);
            if (!c.Check(w.ok, "the list comes up again")) yield break;
            pts = g.RespawnPoints();
            var other = pts.FirstOrDefault(p => p.kind == "start");
            c.Check(m.Press(other.label), "choosing " + other.label);
            yield return SurvivalKit.Until(() => !g.AwaitingRespawn && !P.Ragdolled && !m.IsOpen, 5f, w);
            c.Check(w.ok && Flat(P.transform.position, other.at) < 2f, $"the player wakes there ({Flat(P.transform.position, other.at):0.0} m)");
            c.Check(g.LastRespawn == other.label, "the choice is remembered (saved with the context block)");
            Object.Destroy(bed.gameObject);
        }
    }

    /// <summary>The floating loot panels: the LOOT bar always floats on foot, collapsed; an item on the ground in reach
    /// lists the FLOOR section without opening anything; hovering expands the panels over the running game (no menu, time
    /// runs, the player still walks); a chest within reach joins the LOOT panel; a vehicle's compartments stay off it until
    /// opened (LOOT key / context menu); the YOU panel holds
    /// the pack and a worn bag (never listed as nearby loot); drags between sections (floor → bag, pack → chest, half of
    /// the chest back) and a row menu's TAKE ONE keep pack + bag + chest + floor balanced; the pin key keeps them up.</summary>
    class UiLootOverlay : Scenario
    {
        public override string Id => "ui.loot_overlay";
        public override float Timeout => 90f;

        static Dictionary<string, int> Totals(WastelandGame g, params Inventory[] invs)
        {
            var d = SurvivalKit.Ledger(invs);
            foreach (var kv in g.FloorStacks(g.Player.transform.position, 8f)) { d.TryGetValue(kv.Key, out int h); d[kv.Key] = h + kv.Value; }
            return d;
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var P = g.Player; var o = g.Loot; var w = new Waited(); var got = new Waited(); var at = new Vector3[1];
            yield return SurvivalKit.OnFoot(c, 8f, got, at);
            if (!got.ok) { c.Block("no clear pad near the start"); yield break; }
            if (o.Pinned) o.SetPinned(false);
            var inv = g.Inventory;
            inv.AddItem("food_can", 3); inv.Add(ResourceType.Scrap, 10);
            c.Fixture("3 cans and 10 scrap granted");

            // ---- an item on the ground, faced: the panels come up, the game runs on
            var can = g.DropFromPack("food_can", 1);
            yield return SurvivalKit.GameSeconds(1f);
            if (!c.Check(can, "a can lies on the ground")) yield break;
            yield return SurvivalKit.Until(() => { SurvivalKit.Face(g, can.transform.position); return o.Visible && o.LootSide.Any(s => s.floor); }, 5f, w);
            c.Check(w.ok, $"the floating LOOT bar lists the FLOOR section ({w.seconds:0.0} s)");
            c.Check(!o.Expanded, "it stays collapsed: nothing pops up by itself");
            o.HoverExpand(30f);
            yield return null;
            c.Check(o.Expanded && !o.you.closed && !o.loot.collapsed, "hovering expands LOOT and YOU");
            c.Check(!g.Menus.IsOpen && Time.timeScale == 1f, "no menu page, the world is not paused");
            float t0 = Time.time;
            yield return SurvivalKit.GameSeconds(0.3f);
            c.Check(Time.time > t0, "game time runs while the panels are up");

            // ---- a chest within reach and a worn bag
            var fwd = P.transform.forward; var side = P.transform.right;
            var chestP = SurvivalKit.Piece(g, "chest", P.transform.position + side * 1.4f, -side);
            var chest = chestP ? chestP.GetComponent<Container>() : null;
            if (!c.Check(chest, "a chest stands beside the player")) yield break;
            c.Fixture("a chest built 1.4 m to the side");
            Container bag = g.WornStorage.FirstOrDefault(b => b);
            GameObject madeBag = null;
            if (!bag)
            {
                madeBag = new GameObject("TestBag");
                madeBag.transform.SetParent(P.transform, false);
                bag = madeBag.AddComponent<Container>();
                bag.title = "TEST BAG"; bag.capacity = 10f; bag.worn = true;
                g.WornStorage.Add(bag);
                c.Fixture("no worn bag yet: a 10 kg test bag added to WornStorage");
            }
            yield return SurvivalKit.Until(() => { SurvivalKit.Face(g, chest.transform.position); return o.Visible && o.LootSide.Any(s => s.box == chest); }, 5f, w);
            c.Check(w.ok, "the chest joins the LOOT panel: " + string.Join(", ", o.LootSide.Select(s => s.Title)));
            o.Refresh();
            c.Check(o.LootSide.Any(s => s.floor) && o.LootSide.Any(s => s.box == chest), "LOOT lists the floor and the chest");
            c.Check(o.PlayerSide.Any(s => s.pack) && o.PlayerSide.Any(s => s.box == bag), "YOU lists the pack and the worn bag");
            c.Check(!o.LootSide.Any(s => s.box == bag), "the worn bag is never nearby loot");
            c.Screenshot("loot_overlay");
            yield return null;

            // ---- drags between sections conserve everything
            var before = Totals(g, inv, chest.inventory, bag.inventory);
            var floorSrc = o.LootSide.First(s => s.floor); var chestSrc = o.LootSide.First(s => s.box == chest); var bagSrc = o.PlayerSide.First(s => s.box == bag);
            int moved = o.DragRow(o.RowOf(floorSrc, "food_can"), bagSrc, 0);
            c.Check(moved >= 1 && bag.inventory.GetItem("food_can") >= 1, $"the can dragged from the floor into the bag ({moved})");
            string scrap = "res:" + (int)ResourceType.Scrap;
            int s0 = inv.Get(ResourceType.Scrap);
            moved = o.DragRow(o.RowOf(LootSource.Pack, scrap), chestSrc, 0);
            c.Check(moved == s0 && chest.inventory.Get(ResourceType.Scrap) == s0, $"the scrap dragged from the pack into the chest ({moved})");
            moved = o.DragRow(o.RowOf(chestSrc, scrap), LootSource.Pack, 2);
            c.Check(moved == s0 / 2, $"Shift-drag takes half back ({moved})");
            var row = o.RowOf(chestSrc, scrap);
            var opts = o.RowOptions(row);
            c.Note("row menu: " + string.Join(", ", opts.Select(x => x.label)));
            g.Menus.OpenPopup(row.label, opts, new Vector2Int(100, 60));
            int p0 = inv.Get(ResourceType.Scrap);
            c.Check(g.Menus.ChoosePopup("TAKE ONE") && inv.Get(ResourceType.Scrap) == p0 + 1, "the row menu's TAKE ONE takes one");
            yield return null; yield return null;
            var diff = SurvivalKit.Diff(before, Totals(g, inv, chest.inventory, bag.inventory));
            c.Check(diff == null, "pack + bag + chest + floor balance: " + (diff ?? "ok"));

            // ---- the player can still move; pinned panels stay
            var p1 = P.transform.position;
            yield return SurvivalKit.WalkTo(g, p1 - fwd * 1.2f, 0.3f, w);
            c.Check(Vector3.Distance(P.transform.position, p1) > 0.5f && o.Visible, "the player walks with the panels up");
            o.SetPinned(true);
            yield return null;
            c.Check(o.Visible && o.Pinned && o.Expanded, "the pin key keeps them open");
            o.SetPinned(false);
            o.HoverExpand(0f);
            yield return SurvivalKit.GameSeconds(0.8f);
            c.Check(o.Visible && !o.Expanded, "unpinned and not hovered: folded back into the LOOT bar");

            // ---- a vehicle's trunk / bed: never listed until opened
            VehicleDriver car = null; Container trunk = null;
            foreach (var v in Object.FindObjectsByType<VehicleDriver>(FindObjectsSortMode.None))
            {
                if (!v || v.aiDriven) continue;
                foreach (var b in v.GetComponentsInChildren<Container>()) if (!b.worn) { car = v; trunk = b; break; }
                if (car) break;
            }
            if (car)
            {
                var spot = trunk.AccessAt + (trunk.AccessAt - car.transform.position).normalized * 0.6f;
                var t = MadMax.World.DeformableTerrain.Instance;
                spot.y = (t ? t.Height(spot.x, spot.z) : spot.y) + 0.2f;
                P.Teleport(spot, 0f);
                yield return SurvivalKit.GameSeconds(0.5f);
                o.Refresh();
                c.Check(!o.LootSide.Any(s => s.box && s.box.GetComponentInParent<VehicleDriver>() == car) && !o.Expanded, $"standing at the {trunk.title} of {WastelandGame.Name(car)}: not listed, nothing pops up");
                o.OpenVehicle(car);
                yield return null;
                c.Check(o.LootSide.Any(s => s.box == trunk) && o.Expanded, $"opened by the LOOT key / menu: the {trunk.title} is on the expanded LOOT panel");
                o.SetPinned(false);
            }
            else c.Note("no vehicle with storage in the world: trunk check skipped");

            if (madeBag) { g.WornStorage.Remove(bag); Object.Destroy(madeBag); }
            Object.Destroy(chestP.gameObject);
        }
    }
}
