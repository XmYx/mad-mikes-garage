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
    /// <summary>Scheduled update 2026-10-15: the digger's word, the chase on the radio, the neighbour's errand, the medic
    /// restocking in town, a tip's held part, the oil-pressure bar; living pieces (a TV picture, bees and flies, the
    /// shower and sprinkler spray, the piece in work on a bench).</summary>
    public static class ScheduledScenarios1015
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new DiggerWord();
            yield return new ChaseRadio();
            yield return new NeighbourErrand();
            yield return new MedicRestock();
            yield return new HeldPart();
            yield return new OilBar();
            yield return new TvPicture();
            yield return new LivingPieces();
        }

        /// <summary>Stand <paramref name="dist"/> m from <paramref name="target"/> (from <paramref name="side"/>), look at it
        /// in first person (no body in the way), let the camera settle and capture.</summary>
        internal static IEnumerator Shoot(ScenarioContext c, WastelandGame g, Vector3 target, Vector3 side, float dist, string tag)
        {
            side.y = 0f; side.Normalize();
            var at = target + side * dist;
            at.y = DeformableTerrain.Instance.Height(at.x, at.z) + 0.1f;
            g.Player.Teleport(at, Quaternion.LookRotation(-side).eulerAngles.y);
            if (g.cameraRig) g.cameraRig.mode = ViewMode.FirstPerson;
            yield return new WaitForSeconds(0.3f);
            if (g.cameraRig) g.cameraRig.LookToward((target - (at + Vector3.up * 1.68f)).normalized);
            yield return new WaitForSeconds(0.6f);
            c.Screenshot(tag);
            yield return null;
        }

        internal static void SaveRoundTrip(WastelandGame g)
        {
            var d = new SaveData();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(WastelandGame).GetMethod("SaveNightly1013", flags).Invoke(g, new object[] { d });
            typeof(WastelandGame).GetMethod("LoadNightly1013", flags).Invoke(g, new object[] { d });
        }
    }

    /// <summary>A caught digger either sours their town's stalls (+10 % from tomorrow, on the town news) or, let off
    /// with their half, finds the player in town with the next stash.</summary>
    class DiggerWord : Scenario
    {
        public override string Id => "trade.digger_word";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var town = g.NearestTown(g.Player.transform.position);
            if (!c.Check(town != null, "a town")) yield break;
            g.SourStalls.Clear(); g.DiggerTips.Clear(); g.Caches.Clear();
            bool sawSour = false, sawTip = false;
            for (int i = 0; i < 12 && !(sawSour && sawTip); i++)
            {
                var n = ScheduledScenarios1012.Person(g, "dig:test" + i, NpcRole.Wanderer, 70 + i, ScheduledScenarios1012.Beside(g, 4f));
                if (g.DiggerWord(n, town)) sawTip = true; else sawSour = true;
                Object.Destroy(n.gameObject);
            }
            c.Check(sawSour && sawTip, "both outcomes happen across diggers");
            c.Check(g.SourStalls.TryGetValue(town.index, out var s) && s.from == DayNight.Day + 1, "the word reaches the stalls tomorrow");
            c.Check(Mathf.Approximately(g.StallSour(town.index), 1f), "not today");
            Trade.Town = town;
            int fair = Trade.BuyPrice("tool_crowbar", 0f);
            g.SourStalls[town.index] = (DayNight.Day, DayNight.Day);
            int sour = Trade.BuyPrice("tool_crowbar", 0f);
            Trade.Town = null;
            c.Note($"crowbar {fair} -> {sour}");
            c.Check(sour > fair, "soured stalls charge more");
            var tip = g.DiggerTips.FirstOrDefault();
            if (c.Check(tip != null && tip.town == town.index && tip.day == DayNight.Day + 1, "a grateful digger will turn up tomorrow"))
            {
                ScheduledScenarios1015.SaveRoundTrip(g);
                c.Check(g.DiggerTips.Count == 1 && g.SourStalls.ContainsKey(town.index), "saved and restored");
                string line = g.GiveDiggerTip(g.DiggerTips[0]);
                c.Note(line);
                c.Check(line != null && line.Contains("FAIR'S FAIR"), "and tells of the next stash");
                c.Check(g.DiggerTips.Count == 0, "once");
            }
            g.SourStalls.Clear(); g.DiggerTips.Clear(); g.Caches.Clear();
        }
    }

    /// <summary>A long boss-car chase calls the gang: raider columns within reach answer and hurry along their road
    /// toward the call (and look for the player three times as far).</summary>
    class ChaseRadio : Scenario
    {
        public override string Id => "raid.chase_radio";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var dir = NpcDirector.Instance;
            var raiders = dir ? dir.Convoys.Where(k => k != null && k.raiders && !k.Friendly && k.phase == Convoy.Phase.Travel && !k.Spawned).ToList() : new List<Convoy>();
            if (raiders.Count < 2) { c.Block("needs two raider columns on the road (" + raiders.Count + ")"); yield break; }
            var caller = raiders[0];
            var other = raiders.OrderBy(k => k == caller ? float.MaxValue : Vector3.Distance(k.Position, caller.Position)).First();
            // the call goes out a little way along the answering column's own road
            var at = other.PointAt(other.travel + 250f, out _);
            float d0 = Vector3.Distance(other.Position, at);
            int n = caller.CallGang(g, at);
            c.Note(g.ToastText);
            c.Check(n >= 1 && other.Answering, $"another column answers ({n})");
            c.Check(Convoy.LastCallAnswered == n, "counted");
            var far = at + new Vector3(6000f, 0f, 6000f);                                   // folded-away travel only
            for (int i = 0; i < 10; i++) other.Tick(g, far, 0.5f);
            float d1 = Vector3.Distance(other.Position, at);
            c.Metric("closed_m", d0 - d1, "m");
            c.Check(d1 < d0, $"they hurry along the road toward the call ({d0:0} -> {d1:0} m)");
            typeof(Convoy).GetField("callUntil", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(other, 0f);
            c.Check(!other.Answering, "the call runs out");
            yield return null;
        }
    }

    /// <summary>The neighbour who looked in asks a favour a couple of days later; done, they are helped and warmer.</summary>
    class NeighbourErrand : Scenario
    {
        public override string Id => "npc.neighbour_errand";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var home = g.Player.transform.position;
            var town = g.NearestTown(home);
            if (town == null || Vector2.Distance(town.pos, new Vector2(home.x, home.z)) > WastelandGame.VisitReach) { c.Block("no town within visiting reach"); yield break; }
            g.NeighbourErrands.Clear();
            WastelandGame.LastNeighbour = null;
            g.ForceNeighbourVisit(home);
            string id = WastelandGame.LastNeighbour;
            if (!c.Check(id != null && g.NeighbourErrands.ContainsKey(id), "the visit opens a favour")) yield break;
            var e = g.NeighbourErrands[id];
            c.Check(e.due == DayNight.Day + WastelandGame.ErrandAfterDays && e.town == town.index, "asked in a couple of days, in their town");
            var prof = NpcProfile.Make(id, NpcRole.Resident, NpcDirector.StableKey(id) ^ g.World.seed);
            var n = MadMax.Npc.Npc.Spawn(prof, ScheduledScenarios1012.Beside(g, 2.5f), 180f, null, g.propMaterial);
            yield return new WaitForSeconds(0.3f);
            var talk = new Dialogue(g, n);
            c.Check(!talk.choices.Any(k => k.label.StartsWith("YOU LOOKED IN ON ME")), "not before the day");
            e.due = DayNight.Day;
            talk = new Dialogue(g, n);
            var ask = talk.choices.FirstOrDefault(k => k.label.StartsWith("YOU LOOKED IN ON ME"));
            if (!c.Check(ask.act != null, "on the day they have something to ask")) { Object.Destroy(n.gameObject); yield break; }
            ask.act();
            c.Note(talk.line);
            var take = talk.choices.FirstOrDefault(k => k.label == "CONSIDER IT DONE.");
            take.act?.Invoke();
            c.Check(e.state == 1, "taken on");
            var look = new List<WastelandGame.Pin>(); g.LookFor(look);
            c.Check(look.Any(p => p.label.EndsWith("'S FAVOUR")), "on LOOK FOR");
            var a = WastelandGame.NeighbourAsks[e.kind];
            if (a.item.StartsWith("res:")) { var rt = (ResourceType)int.Parse(a.item.Substring(4)); g.Inventory.TrySpend(rt, g.Inventory.Get(rt)); }
            else g.Inventory.TakeItem(a.item, g.Inventory.GetItem(a.item));                         // a reused world may have left some in the pack
            c.Check(g.DoErrand(prof) == null, "not without the goods");
            if (a.item.StartsWith("res:")) g.Inventory.Add((ResourceType)int.Parse(a.item.Substring(4)), a.n); else g.Inventory.AddItem(a.item, a.n);
            int disp = NpcRegistry.Get(prof).disposition;
            talk = new Dialogue(g, n);
            var give = talk.choices.FirstOrDefault(k => k.label == "I BROUGHT WHAT YOU NEEDED.");
            give.act?.Invoke();
            c.Note(talk.line);
            c.Check(e.state == 2 && NpcRegistry.Get(prof).Has(NpcSave.Helped) && NpcRegistry.Get(prof).disposition > disp, "done: helped and warmer");
            ScheduledScenarios1015.SaveRoundTrip(g);
            c.Check(g.NeighbourErrands.TryGetValue(id, out var back) && back.state == 2 && back.kind == e.kind, "saved and restored");
            g.NeighbourErrands.Clear();
            Object.Destroy(n.gameObject);
        }
    }

    /// <summary>A crew medic with an empty kit standing in town asks to buy bandages; once allowed they buy their own
    /// out of the crew's scrap.</summary>
    class MedicRestock : Scenario
    {
        public override string Id => "npc.medic_restock";
        public override float Timeout => 25f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var town = g.NearestTown(g.Player.transform.position);
            if (!c.Check(town != null, "a town")) yield break;
            var at = new Vector3(town.pos.x + 4f, 0f, town.pos.y + 4f);
            at.y = DeformableTerrain.Instance.Height(at.x, at.z) + 0.2f;
            g.Player.Teleport(at, 0f);
            yield return new WaitForSeconds(0.5f);
            var medic = ScheduledScenarios1009.Recruit(g, true, ScheduledScenarios1012.Beside(g, 3f));
            yield return new WaitForSeconds(0.3f);
            if (!c.Check(medic && medic.pack, "a crew medic")) yield break;
            if (g.World.SettlementAt(medic.transform.position.x, medic.transform.position.z) == null) { c.Block("the medic is not inside the town"); ScheduledScenarios1009.Drop(g, medic); yield break; }
            foreach (var id in WastelandGame.MedicStock) medic.pack.inventory.TakeItem(id, medic.pack.inventory.GetItem(id));
            g.MedicMayShop.Clear();
            WastelandGame.LastRestock = null;
            g.UpdateMedicRestock(true);
            c.Check(WastelandGame.LastRestock != null && WastelandGame.LastRestock.Contains("WANT ME TO BUY"), "they ask first: " + WastelandGame.LastRestock);
            c.Check(WastelandGame.MedicKit(medic) == 0, "and spend nothing");
            g.LetMedicShop(medic.Profile, true);
            int cost = g.RestockCost(town);
            g.Inventory.Add(ResourceType.Scrap, cost + 5);
            int scrap = g.Inventory.Get(ResourceType.Scrap);
            g.UpdateMedicRestock(true);
            c.Note(WastelandGame.LastRestock);
            c.Check(medic.pack.inventory.GetItem("med_bandage") == WastelandGame.MedicRestockBandages && g.Inventory.Get(ResourceType.Scrap) == scrap - cost, $"trusted, they buy {WastelandGame.MedicRestockBandages} bandages for {cost} scrap");
            ScheduledScenarios1015.SaveRoundTrip(g);
            c.Check(g.MedicMayShop.Contains(medic.Profile.id), "the trust is saved");
            g.MedicMayShop.Clear();
            ScheduledScenarios1009.Drop(g, medic);
        }
    }

    /// <summary>A rescued survivor's third kind of tip: a parts stall in their town holds a part at half price until
    /// their week is out.</summary>
    class HeldPart : Scenario
    {
        public override string Id => "trade.held_part";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var town = g.NearestTown(g.Player.transform.position);
            if (!c.Check(town != null, "a town")) yield break;
            Trade.Held.Clear();
            string tip = null;
            for (int i = 0; i < 40 && tip == null; i++)
            {
                string id = "test:held" + i;
                NpcRegistry.Get(NpcProfile.Make(id, NpcRole.Trader, 90 + i)).rescuedDay = DayNight.Day;
                g.Rescues[id] = new WastelandGame.Rescue { town = town.index, name = "TESTER" };
                string t = g.HitcherTip(id);
                if (t != null && t.Contains("HOLDING")) tip = t;
                g.Rescues.Remove(id);
            }
            c.Note(tip);
            if (!c.Check(tip != null && Trade.Held.ContainsKey(town.index), "some survivors have a part held for the player")) yield break;
            var h = Trade.Held[town.index];
            c.Check(h.until == DayNight.Day + WastelandGame.RescueDays, "until the week is out");
            Trade.Town = town;
            var p = NpcProfile.Make("test:partsman", NpcRole.Shopkeeper, 7, "parts");
            var o = Trade.Stock(p, NpcRegistry.Get(p), 0f).FirstOrDefault(k => k.note == Trade.HeldNote);
            c.Check(o.id == h.id && o.count == 1, "the stall lists it as HELD FOR YOU");
            int full = Trade.BuyPrice(h.id, 0f);
            c.Check(o.price <= Mathf.CeilToInt(full * 0.5f) + 1, $"at half price ({o.price} of {full})");
            var food = NpcProfile.Make("test:foodman", NpcRole.Shopkeeper, 8, "food");
            c.Check(!Trade.Stock(food, NpcRegistry.Get(food), 0f).Any(k => k.note == Trade.HeldNote), "only at a parts stall");
            ScheduledScenarios1015.SaveRoundTrip(g);
            c.Check(Trade.Held.ContainsKey(town.index), "saved and restored");
            Trade.Town = null;
            Trade.Held.Clear();
            g.Caches.Clear();
            yield return null;
        }
    }

    /// <summary>The oil-pressure bar: rises once the engine runs; drawn on the vehicle panel.</summary>
    class OilBar : Scenario
    {
        public override string Id => "ui.oil_pressure_bar";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = g.Current;
            if (!v) { c.Block("starts on foot without a car to hand"); yield break; }
            var sys = v.GetComponent<VehicleSystems>();
            if (!c.Check(sys, "vehicle systems")) yield break;
            yield return TestWorld.StartEngine(c, v);
            float t0 = Time.time;
            while (Time.time - t0 < 8f && sys.OilPressure < VehicleSystems.OilLampBelow) yield return null;
            float share = PixelHud.OilPressureShare(sys);
            c.Metric("oil_bar", share, "");
            c.Check(share > 0.05f && share <= 1f, $"the bar shows the pressure ({sys.OilPressure:0.00} bar)");
            yield return null;
            c.Screenshot("oil_pressure_bar");
            yield return null;
        }
    }

    /// <summary>A playing TV shows a moving picture (not only a flicker in the room).</summary>
    class TvPicture : Scenario
    {
        public override string Id => "living.tv_picture";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var fwd = g.Player.transform.forward; fwd.y = 0f; fwd.Normalize();
            var tv = PH.Piece(g, "tv", g.Player.transform.position + fwd * 2.2f, -fwd);
            if (!c.Check(tv, "a TV")) yield break;
            var set = tv.GetComponent<TvSet>();
            set.Play("vhs_mechanic");
            yield return new WaitForSeconds(0.5f);
            set.Draw(1.3f);
            float a = set.Brightness;
            c.Check(set.Picture && set.Picture.enabled && a > 0.05f, $"the screen shows a picture (brightness {a:0.00})");
            set.Draw(3.7f);
            c.Check(!Mathf.Approximately(a, set.Brightness), "and it moves");
            yield return ScheduledScenarios1015.Shoot(c, g, tv.transform.position + Vector3.up * 0.45f, tv.transform.forward, 1.5f, "tv_picture");
            set.Stop();
            c.Check(!set.Picture.enabled, "dark when stopped");
            Object.Destroy(tv.gameObject);
        }
    }

    /// <summary>Pieces that used to stand still: bees at the hive mouth by day, flies over a fresh latrine pit, the shower
    /// and sprinkler spraying, the item in work on a bench.</summary>
    class LivingPieces : Scenario
    {
        public override string Id => "living.pieces";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            DayNight.SetHours(11f);
            bool wasRain = Weather.Raining; Weather.Raining = false;
            var me = g.Player.transform.position;
            var fwd = g.Player.transform.forward; fwd.y = 0f; fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            var hive = PH.Piece(g, "beehive", me + fwd * 3f - right * 1.5f, -fwd);
            var latrine = PH.Piece(g, "latrine", me + fwd * 4.5f + right * 2.5f, -fwd);
            var shower = PH.Piece(g, "shower", me + fwd * 3.5f + right * 0.6f, -fwd);
            var sprinkler = PH.Piece(g, "sprinkler", me + fwd * 2f - right * 3.2f, -fwd);
            var bench = PH.Piece(g, "workbench", me + fwd * 2.4f + right * 3.6f, -fwd);
            if (!c.Check(hive && latrine && shower && sprinkler && bench, "pieces placed")) yield break;
            latrine.GetComponent<Latrine>().fresh = 60f;
            var st = bench.GetComponent<CraftingStation>();
            var r = RecipeLibrary.All.FirstOrDefault(k => k.station == "workbench" && k.kind == OutputKind.Item && WorldItemModels.For(k.output, out _, out _));
            if (c.Check(st && r != null, "a workbench and an item recipe")) st.queue.Add(new CraftingStation.Job { recipe = r.id, progress = 0.4f, speed = 0.01f });
            yield return new WaitForSeconds(2f);
            var bees = hive.GetComponent<Swarm>(); var flies = latrine.GetComponent<Swarm>();
            float busy = hive.GetComponent<Beehive>().Busy();
            c.Note($"hive busy {busy:0.00}, temp {Weather.TemperatureAt(hive.transform.position.z):0} C");
            c.Check(bees && (bees.Flying > 0 || busy <= 0.1f), $"bees at the hive mouth ({(bees ? bees.Flying : -1)})");
            c.Check((flies && flies.Flying > 0) || Weather.TemperatureAt(latrine.transform.position.z) < 4f, $"flies over the fresh pit ({(flies ? flies.Flying : -1)})");
            yield return ScheduledScenarios1015.Shoot(c, g, hive.transform.position + Vector3.up * 0.6f, hive.transform.forward + hive.transform.right * 0.4f, 1.6f, "living_bees");
            shower.GetComponent<WaterOutlet>().Spray(8f);
            sprinkler.GetComponent<Sprinkler>().Spray(8f);
            yield return ScheduledScenarios1015.Shoot(c, g, shower.transform.position + Vector3.up * 1.3f, shower.transform.forward + shower.transform.right * 0.5f, 2.6f, "living_shower");
            var sp = shower.GetComponentInChildren<Droplets>();
            c.Check(shower.GetComponent<WaterOutlet>().Spraying && sp && sp.Airborne > 5, $"the shower rose runs ({(sp ? sp.Airborne : -1)} drops in the air)");
            yield return ScheduledScenarios1015.Shoot(c, g, sprinkler.transform.position + Vector3.up * 0.5f, sprinkler.transform.forward, 4.5f, "living_sprinkler");
            var jet = sprinkler.GetComponentInChildren<Droplets>();
            if (jet) c.Note("sprinkler: " + jet.Spread());
            if (sp) c.Note("shower: " + sp.Spread());
            c.Check(sprinkler.GetComponent<Sprinkler>().Spraying && jet && jet.Airborne > 5, $"the sprinkler throws its jet ({(jet ? jet.Airborne : -1)} drops)");
            var work = st ? st.GetComponent<StationWork>() : null;
            c.Check(work && r != null && work.Showing == r.output, "the item in work sits on the bench (" + (work ? work.Showing : "none") + ")");
            yield return ScheduledScenarios1015.Shoot(c, g, st ? st.OutputPoint : bench.transform.position, bench.transform.forward, 1.4f, "living_bench");
            if (st) st.queue.Clear();
            yield return null;
            c.Check(!work || work.Showing == null, "and goes when the work does");
            Weather.Raining = wasRain;
            foreach (var p in new[] { hive, latrine, shower, sprinkler, bench }) if (p) Object.Destroy(p.gameObject);
        }
    }
}
