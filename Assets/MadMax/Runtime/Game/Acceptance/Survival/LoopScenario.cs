using System.Collections;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 Q2 first-session loop in a fresh sandbox world (basic starting kit): FIRST STEPS is running with
    /// its journal line and the homestead stands; the player drives a fleet car 300 m along the road (step 1 pays),
    /// tops the tank up from the pack with the service key (step 2 pays); back home a recipe is crafted at the
    /// homestead bench and a chest is built from the kit and stocked; a roadside trader buys from the pack and sells
    /// back; the night is slept in the homestead bed; the game is saved and loaded with the chest, pack and FIRST STEPS
    /// progress intact. Placements (the car on the road, the trip home, the trader) are disclosed.</summary>
    class SurvivalLoop : Scenario
    {
        public override string Id => "survival.loop";
        public override string[] Suites => SurvivalScenarios.Suites;
        public override float Timeout => 300f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var inv = g.Inventory; var P = g.Player;
            var w = new Waited();
            if (g.Current) { g.Exit(); yield return null; }
            var home = P.transform.position;

            // ---- a fresh sandbox: FIRST STEPS and the homestead
            c.Check(g.StarterStep == 0 && g.StarterLine != null, "FIRST STEPS starts at step 1: " + g.StarterLine);
            c.Check(Journal.Entries.Any(e => e.text.StartsWith("FIRST STEPS")), "the journal has the first job");
            var hsBed = Placeable.All.FirstOrDefault(p => p && p.id == "bed" && g.IsHomestead(p));
            var hsBench = Placeable.All.FirstOrDefault(p => p && p.id == "workbench" && g.IsHomestead(p));
            c.Check(hsBed && hsBench, "the homestead has a bed and a workbench");
            if (hsBench) home = hsBench.transform.position;
            int scrap0 = inv.Get(ResourceType.Scrap);

            // ---- drive 300 m along the road
            var car = TestWorld.Vehicle("Sedan") ?? Object.FindObjectsByType<VehicleDriver>(FindObjectsSortMode.None).FirstOrDefault(v => v.driveable && !v.aiDriven && !v.aircraft && !v.GetComponent<BoatModel>() && !v.GetComponent<BikeBalance>() && !v.GetComponent<Machine>());
            if (!c.Check(car, "a fleet car to drive")) yield break;
            if (!TestWorld.Pad(9f, 60f, out var pad, out var dir)) { c.Block("no clear road lane"); yield break; }
            var route = Route(g, pad);
            yield return TestWorld.Place(c, car, pad, dir);
            g.Enter(car);
            yield return SurvivalKit.GameSeconds(0.3f);
            yield return TestWorld.StartEngine(c, car);
            float driveT = Time.time; int idx = route.Item2;
            yield return SurvivalKit.Until(() =>
            {
                var pos = car.transform.position;
                while (idx + route.Item3 >= 0 && idx + route.Item3 < route.Item1.Count && Flat(route.Item1[idx] - pos).magnitude < 10f) idx += route.Item3;
                if (idx + route.Item3 < 0 || idx + route.Item3 >= route.Item1.Count) route.Item3 = -route.Item3;
                var to = Flat(route.Item1[Mathf.Clamp(idx, 0, route.Item1.Count - 1)] - pos);
                float ang = Vector3.SignedAngle(Flat(car.transform.forward), to, Vector3.up);
                car.handbrake = false; car.steerInput = Mathf.Clamp(ang / 30f, -1f, 1f);
                car.throttleInput = car.ForwardSpeed < (Mathf.Abs(ang) > 35f ? 7f : 14f) ? 0.8f : 0f;
                car.brakeInput = 0f;
                return g.StarterStep >= 1;
            }, 90f, w);
            car.throttleInput = 0f; car.brakeInput = 1f;
            c.Metric("drive_300m_time", Time.time - driveT, "s");
            c.Check(w.ok, "driving 300 m completes FIRST STEPS 1: " + TestWorld.State(car));
            yield return SurvivalKit.Until(() => Mathf.Abs(car.ForwardSpeed) < 0.5f, 6f);
            car.brakeInput = 0f; car.handbrake = true;

            // ---- top up the tank from the pack
            g.Exit();
            yield return SurvivalKit.GameSeconds(0.5f);
            var sys = car.GetComponent<VehicleSystems>();
            var fuelKind = car.GetComponentInChildren<EngineStats>() && car.GetComponentInChildren<EngineStats>().name.Contains("diesel") ? ResourceType.Diesel : ResourceType.Fuel;
            if (inv.Get(fuelKind) < 10) { inv.Add(fuelKind, 10); c.Fixture("granted 10 L of " + fuelKind); }
            if (sys && sys.fuel > sys.fuelCapacity - 10f) { sys.fuel = sys.fuelCapacity * 0.5f; c.Fixture("tank drained to half"); }
            float fuel0 = sys ? sys.fuel : 0f;
            var side = car.transform.right;
            P.Teleport(SurvivalKit.Ground(car.transform.position + side * 2.2f) + Vector3.up * 0.1f, Mathf.Atan2(-side.x, -side.z) * Mathf.Rad2Deg);
            yield return SurvivalKit.GameSeconds(0.4f);
            yield return SurvivalKit.Press(Controls.Act.Service);
            yield return SurvivalKit.Until(() => g.StarterStep >= 2, 25f, w);
            c.Check(sys && sys.fuel > fuel0 + 0.5f, $"the service key fills the tank from the pack ({fuel0:0.0} -> {(sys ? sys.fuel : 0f):0.0} L)");
            c.Check(w.ok, "and completes FIRST STEPS 2");
            c.Metric("first_steps_pay", inv.Get(ResourceType.Scrap) - scrap0, "scrap");
            c.Check(inv.Get(ResourceType.Scrap) - scrap0 >= 35, $"each step pays ({inv.Get(ResourceType.Scrap) - scrap0} scrap)");

            // ---- home: craft at the homestead bench
            P.Teleport(SurvivalKit.Ground(home - (hsBench ? hsBench.transform.forward : Vector3.forward) * -1.4f) + Vector3.up * 0.2f, 0f);
            c.Fixture("back home (teleport)");
            yield return SurvivalKit.GameSeconds(0.5f);
            var st = hsBench ? hsBench.GetComponentInChildren<CraftingStation>() : null;
            var r = RecipeLibrary.All.Where(x => x.station == "workbench" && x.fuel == ResourceType.None && x.items.Length == 0 && x.kind == OutputKind.Item
                                                 && string.IsNullOrEmpty(RecipeLibrary.KnowledgeFor(x)) && st && g.CraftBlockReason(x, st) == null).OrderBy(x => x.resources.Sum(i => i.amount)).FirstOrDefault();
            if (r == null && st)
            {
                r = RecipeLibrary.All.Where(x => x.station == "workbench" && x.fuel == ResourceType.None && x.items.Length == 0 && x.kind == OutputKind.Item && string.IsNullOrEmpty(RecipeLibrary.KnowledgeFor(x))).OrderBy(x => x.resources.Sum(i => i.amount)).First();
                foreach (var (t, n) in r.resources) inv.Add(t, RecipeLibrary.Amount(n));
                c.Fixture("granted the inputs of " + r.id + " (the kit could not pay for any recipe)");
            }
            if (c.Check(st && r != null, "the homestead bench offers a recipe the pack can pay for: " + (r != null ? r.id : "-")))
            {
                yield return SurvivalKit.Use(g, hsBench, false, w);
                c.Check(g.Menus.Current == MenuSystem.Page.Crafting, "[E] at the bench opens crafting");
                int out0 = inv.GetItem(r.output);
                g.Craft(r, st);
                g.Menus.Close();
                c.Check(st.queue.Count > 0, "the job is queued");
                if (st.queue.Count > 0) { st.queue[st.queue.Count - 1].progress = 0.9f; c.Fixture("the job advanced to 90 %"); }
                yield return SurvivalKit.Until(() => inv.GetItem(r.output) > out0, 30f, w);
                c.Check(w.ok, $"{r.name} is made and handed over ({inv.GetItem(r.output) - out0})");
            }

            // ---- a chest of one's own
            if (inv.GetItem(ItemIds.ChestKit) <= 0) { inv.AddItem(ItemIds.ChestKit); c.Fixture("granted a chest kit"); }
            if (inv.GetItem(ItemIds.ClawHammer) <= 0) { inv.AddItem(ItemIds.ClawHammer); c.Fixture("granted a claw hammer"); }
            g.UseItem(ItemIds.ClawHammer);
            var placed = new Placeable[1];
            var spot = Clear(g, P.transform.position);
            yield return SurvivalKit.Build(c, "chest", spot, placed);
            g.Build.SetActive(false);
            if (g.cameraRig) g.cameraRig.mode = ViewMode.Isometric;
            var chest = placed[0] ? placed[0].GetComponent<Container>() : null;
            c.Check(chest, "a chest is built from the kit near home");
            int paper = 6;
            inv.AddItem(ItemIds.Paper, paper);
            if (chest) { inv.TakeItem(ItemIds.Paper, paper); chest.inventory.AddItem(ItemIds.Paper, paper); }
            c.Fixture(paper + " paper stored in the chest as a marker");

            // ---- trade
            var vendorAt = SurvivalKit.Ground(P.transform.position + P.transform.forward * 1.6f);
            var trader = MadMax.Npc.Npc.Spawn(NpcProfile.Make("test:loop", NpcRole.Stallkeeper, 777, "food"), vendorAt + Vector3.up * 0.05f, 180f, null, g.propMaterial);
            c.Fixture("a food stallkeeper set down beside the player");
            yield return SurvivalKit.GameSeconds(0.5f);
            if (c.Check(trader, "the trader stands there"))
            {
                g.Menus.OpenTalk(trader, true);
                c.Check(g.Menus.Current == MenuSystem.Page.Trade, "the trade page opens");
                float bargain = Trade.Bargain(g, trader.State);
                string sell = inv.Items.Where(kv => kv.Value > 0 && Trade.Buys("food", kv.Key) && Trade.SellPrice(kv.Key, bargain) > 0).Select(kv => kv.Key).FirstOrDefault();
                if (sell == null) { inv.AddItem("crop_wheat", 3); sell = "crop_wheat"; c.Fixture("3 wheat to sell"); }
                int s0 = inv.Get(ResourceType.Scrap), n0 = inv.GetItem(sell);
                c.Check(Trade.Sell(g, trader, sell, 1, bargain) && inv.GetItem(sell) == n0 - 1 && inv.Get(ResourceType.Scrap) > s0, $"selling one {sell} pays scrap (+{inv.Get(ResourceType.Scrap) - s0})");
                var offer = Trade.Stock(trader.Profile, trader.State, bargain).Where(o => !o.id.StartsWith("part:")).OrderBy(o => o.price).FirstOrDefault();
                if (offer.id != null)
                {
                    int s1 = inv.Get(ResourceType.Scrap);
                    bool bought = Trade.Buy(g, trader, offer, 1);
                    c.Check(bought && inv.Get(ResourceType.Scrap) == s1 - offer.price, $"buying {offer.id} costs {offer.price} scrap");
                }
                g.Menus.Close();
            }

            // ---- the night in the homestead bed
            if (hsBed)
            {
                DayNight.SetHours(22f);
                c.Fixture("clock set to 22:00");
                P.Teleport(SurvivalKit.Ground(hsBed.transform.position + hsBed.transform.forward * 1.2f) + Vector3.up * 0.2f, 0f);
                yield return SurvivalKit.GameSeconds(0.4f);
                yield return SurvivalKit.Use(g, hsBed, false, w);
                c.Check(w.ok && Mathf.Abs(DayNight.Hours - 7f) < 0.3f, $"[E] at the homestead bed sleeps the night ({DayNight.Hours:0.0} h)");
            }

            // ---- save and load
            if (!Profile.Isolated) { c.Note("not an isolated profile: save/load skipped"); yield break; }
            int step = g.StarterStep; var chestPos = chest ? chest.transform.position : Vector3.zero;
            var pack = SurvivalKit.Ledger(inv);
            g.SaveGame(1);
            var old = g;
            g.LoadGame(1);
            while ((WastelandGame.Instance == old || !WastelandGame.Instance || !WastelandGame.Instance.Ready || ScreenFader.Busy) && Time.realtimeSinceStartup - c.startedAt < 290f) yield return null;
            g = WastelandGame.Instance;
            if (!c.Check(g && g != old && g.Ready, "the save loads")) yield break;
            WastelandGame.ExternalInput = true;
            yield return SurvivalKit.GameSeconds(1f, 10);
            c.Check(g.StarterStep == step, $"FIRST STEPS continues at step {step + 1} ({g.StarterStep + 1})");
            var chest2 = chest ? SurvivalKit.Near("chest", chestPos, 0.2f) : null;
            c.Check(!chest || (chest2 && chest2.GetComponent<Container>().inventory.GetItem(ItemIds.Paper) == paper), "the chest and what is in it are back");
            var diff = SurvivalKit.Diff(pack, SurvivalKit.Ledger(g.Inventory));
            c.Check(diff == null, "the pack is as saved: " + (diff ?? "same"));
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>A clear ground spot 2–3 m from the player (no pieces in the way).</summary>
        static Vector3 Clear(WastelandGame g, Vector3 from)
        {
            for (int k = 0; k < 16; k++)
            {
                var d = Quaternion.Euler(0f, k * 45f + (k >= 8 ? 22f : 0f), 0f) * Vector3.forward * (k >= 8 ? 3f : 2.2f);
                var p = SurvivalKit.Ground(from + d);
                if (!Physics.CheckBox(p + Vector3.up * 0.6f, new Vector3(0.6f, 0.5f, 0.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) return p;
            }
            return SurvivalKit.Ground(from + g.Player.transform.forward * 2.2f);
        }

        /// <summary>The road points nearest the pad, the index to start from and the direction along them.</summary>
        static (System.Collections.Generic.List<Vector3>, int, int) Route(WastelandGame g, Vector3 pad)
        {
            System.Collections.Generic.List<Vector3> best = null; int bi = 0; float bd = float.MaxValue;
            foreach (var r in g.World.roads.roads)
                for (int i = 0; i < r.points.Count; i++)
                {
                    float d = (r.points[i] - pad).sqrMagnitude;
                    if (d < bd) { bd = d; best = r.points; bi = i; }
                }
            if (best == null) return (new System.Collections.Generic.List<Vector3> { pad, pad + Vector3.forward * 400f }, 0, 1);
            int dirn = bi + 1 < best.Count && (best.Count - bi) > bi ? 1 : -1;
            return (best, bi, dirn);
        }
    }
}
