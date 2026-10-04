using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of 2026-10-04: explicit looting (no auto pickup, hold-[E] sweep, every item
    /// visible), ploughing only unpaved ground, blown-tyre traces, tyre mud, engine fires that burn a car out (and may
    /// blow the tank), ignition keys and hotwiring, lost limbs and prosthetics.</summary>
    public static class UpdateScenarios1004
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new ExplicitLoot();
            yield return new ItemsVisible();
            yield return new PlowPaved();
            yield return new TyrePopTraces();
            yield return new TyreMud();
            yield return new Burnout();
            yield return new KeysHotwire();
            yield return new LimbLoss();
            yield return new ProstheticsFit();
        }

        /// <summary>A driveable four-wheeled car of the start fleet (the sedan first).</summary>
        internal static VehicleDriver Car(WastelandGame g)
        {
            var s = TestWorld.Vehicle("Sedan");
            if (s) return s;
            foreach (var v in g.AllVehicles)
                if (v && v.driveable && !v.aiDriven && !v.aircraft && !v.GetComponent<Machine>() && !v.GetComponent<BikeBalance>() && !v.GetComponent<BoatModel>()
                    && v.GetComponent<VehicleSystems>() && v.GetComponentsInChildren<WheelStats>().Length == 4) return v;
            return null;
        }
    }

    /// <summary>Resource pickups lie in the world as visible items: nothing reaches the pack by standing on them; [E]
    /// takes one, holding it sweeps everything within 3 m (not what lies further).</summary>
    class ExplicitLoot : Scenario
    {
        public override string Id => "items.explicit_loot";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var ps = PickupSystem.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!c.Check(ps, "the pickup system is up")) yield break;
            var me = g.Player.transform.position; var f = g.Player.transform.forward; var r = g.Player.transform.right;
            int scrap0 = g.Inventory.Get(ResourceType.Scrap), wood0 = g.Inventory.Get(ResourceType.Wood), stone0 = g.Inventory.Get(ResourceType.Stone);
            ps.Spawn(ResourceType.Scrap, 3, me + f * 1.2f + Vector3.up * 0.5f, Vector3.zero);
            ps.Spawn(ResourceType.Wood, 2, me + r * 1.6f + Vector3.up * 0.5f, Vector3.zero);
            ps.Spawn(ResourceType.Stone, 4, me - r * 2.2f + Vector3.up * 0.5f, Vector3.zero);
            ps.Spawn(ResourceType.Glass, 1, me + f * 6f + Vector3.up * 0.5f, Vector3.zero);
            c.Fixture("scrap, wood and stone within 3 m of the player, glass 6 m away");
            yield return new WaitForSeconds(2f);
            c.Check(g.Inventory.Get(ResourceType.Scrap) == scrap0 && g.Inventory.Get(ResourceType.Wood) == wood0 && g.Inventory.Get(ResourceType.Stone) == stone0,
                "nothing is collected by standing next to it");
            WorldItem first = null; int visible = 0;
            foreach (var w in WorldItem.All)
            {
                if (!w || !w.IsResource) continue;
                var mr = w.GetComponentInChildren<MeshRenderer>();
                var mf = w.GetComponentInChildren<MeshFilter>();
                if (mr && mr.enabled && mf && mf.sharedMesh && mr.bounds.size.magnitude > 0.03f) visible++;
                if ((w.transform.position - me).sqrMagnitude < 9f && w.Resource == ResourceType.Scrap) first = w;
            }
            c.Metric("pickups_visible", visible, "");
            c.Check(visible >= 4, $"each pickup is a visible item in the world ({visible})");
            c.Screenshot("pickups");
            yield return null;
            if (!c.Check(first, "the scrap pile lies at the player's feet")) yield break;
            g.ForceLootAll = true;
            first.Use(g, false);                                                                    // [E] on the scrap, then held
            yield return new WaitForSeconds(2f);
            g.ForceLootAll = false;
            c.Check(g.Inventory.Get(ResourceType.Scrap) == scrap0 + 3, "[E] takes the scrap");
            c.Check(g.Inventory.Get(ResourceType.Wood) == wood0 + 2 && g.Inventory.Get(ResourceType.Stone) == stone0 + 4, "holding [E] sweeps the wood and stone within 3 m");
            bool glassLeft = false;
            foreach (var w in WorldItem.All) if (w && w.Resource == ResourceType.Glass) glassLeft = true;
            c.Check(glassLeft, "the glass 6 m away stays where it is");
        }
    }

    /// <summary>Every item id and resource has a visible model on the ground (no invisible stand-ins).</summary>
    class ItemsVisible : Scenario
    {
        public override string Id => "items.visible";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var ids = new HashSet<string>();
            foreach (var r in RecipeLibrary.All) if (r.kind == OutputKind.Item && !string.IsNullOrEmpty(r.output)) ids.Add(r.output);
            foreach (var t in ToolLibrary.AllIds) ids.Add(t);
            foreach (var p in ProstheticLibrary.All) ids.Add(p.id);
            foreach (var l in LootTables.AllIds()) if (!l.StartsWith("part:") && !l.StartsWith("res:")) ids.Add(l);
            ids.Add("limb_arm"); ids.Add("key_sedan_abcdef");
            for (int i = 1; i < ResourceInfo.Count; i++) ids.Add("res:" + i);
            var at = g.Player.transform.position + g.Player.transform.forward * 3f + Vector3.up * 50f;    // up in the air: no clutter on the ground
            int bad = 0, n = 0;
            var missing = new List<string>();
            foreach (var id in ids)
            {
                var w = WorldItem.Create(id, 1, -1f, g.propMaterial, at, Quaternion.identity);
                n++;
                bool ok = false;
                foreach (var mr in w.GetComponentsInChildren<Renderer>())
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mr.enabled && (mr is SkinnedMeshRenderer || (mf && mf.sharedMesh && mf.sharedMesh.vertexCount > 0)) && mr.bounds.size.magnitude > 0.01f) ok = true;
                }
                if (!ok) { bad++; if (missing.Count < 12) missing.Add(id); }
                Object.Destroy(w.gameObject);
                if (n % 40 == 0) yield return null;
            }
            c.Metric("items_checked", n, "");
            c.Check(bad == 0, $"every one of {n} items has a visible model on the ground ({bad} without: {string.Join(", ", missing)})");
        }
    }

    /// <summary>The hoe and the plough only till open, unpaved ground: a paved (gravel) cell refuses, the open cell
    /// beside it does not.</summary>
    class PlowPaved : Scenario
    {
        public override string Id => "farming.paved";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            Vector3 open = default; bool found = false;
            for (int k = 0; k < 400 && !found; k++)
            {
                float a = k * 2.39996f, r = 10f + k * 0.3f;
                var p = Fields.Snap(g.Player.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                if (Fields.CanTill(p, out _) && Fields.CanTill(p + Vector3.right * Fields.Cell * 2f, out _)) { open = p; found = true; }
            }
            if (!found) { c.Block("no tillable ground near the player"); yield break; }
            var paved = open + Vector3.right * Fields.Cell * 2f;
            paved.y = t.Height(paved.x, paved.z);
            t.ApplyTerraform((byte)DeformableTerrain.TerraOp.Pave, paved, 1.1f, 0f, DeformableTerrain.PaveGravel);
            c.Fixture("gravel laid on the cell beside an open one");
            yield return null;
            c.Check(t.PaveAt(paved.x, paved.z) != 0, "the cell is paved");
            c.Check(!Fields.CanTill(paved, out var why) && why == "PAVED", $"the paved cell can't be tilled ({why})");
            c.Check(Fields.CanTill(open, out _), "the open cell beside it can");
            int before = GardenPlot.All.Count;
            c.Check(Fields.Till(g, paved) == null && GardenPlot.All.Count == before, "the plough's Till makes no bed on the paving");
        }
    }

    /// <summary>A blown tyre bursts rubber onto the road, the flat sits low on its rim and leaves rubber strips behind
    /// as the car drives on; a bare rim (carcass torn off) sits lower still.</summary>
    class TyrePopTraces : Scenario
    {
        public override string Id => "vehicle.tyre_pop_traces";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = UpdateScenarios1004.Car(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            if (!TestWorld.Pad(9f, 60f, out var pad, out var fwd)) { c.Block("no level lane"); yield break; }
            yield return TestWorld.Place(c, v, pad, fwd);
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, v);
            v.handbrake = false; v.throttleInput = 0.7f;
            yield return new WaitForSeconds(2f);
            WheelStats front = null;
            foreach (var w in v.GetComponentsInChildren<WheelStats>()) if (v.transform.InverseTransformPoint(w.transform.position).z > 0f) { front = w; break; }
            if (!c.Check(front, "a front tyre")) yield break;
            int rubber0 = Shards.Dropped[(int)Shards.Kind.Rubber];
            front.Pop();
            c.Fixture("blew the front tyre at speed");
            yield return new WaitForSeconds(0.3f);
            c.Check(Shards.Dropped[(int)Shards.Kind.Rubber] > rubber0, "the blowout bursts rubber onto the ground");
            yield return new WaitForSeconds(8f);
            v.throttleInput = 0f; v.brakeInput = 1f;
            int strips = Shards.Dropped[(int)Shards.Kind.Rubber] - rubber0 - 1;
            c.Metric("rubber_strips", strips, "");
            c.Check(strips >= 1, $"driving on the flat leaves rubber strips ({strips})");
            c.Check(Mathf.Abs(front.transform.localScale.y - 0.78f) < 0.02f, $"the flat sits low ({front.transform.localScale.y:0.00})");
            c.Screenshot("flat");
            yield return null;
            front.wear = 1.5f;
            yield return null;
            c.Check(front.Shredded && Mathf.Abs(front.transform.localScale.y - 0.7f) < 0.02f, "a torn-off carcass leaves the bare rim");
            v.brakeInput = 0f;
            g.Exit();
        }
    }

    /// <summary>Tyres cake up driving through mud, and shed it again on a firm road.</summary>
    class TyreMud : Scenario
    {
        public override string Id => "vehicle.tyre_mud";
        public override float Timeout => 70f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = UpdateScenarios1004.Car(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            if (!TestWorld.MudPad(6f, 25f, 0.3f, out var pad, out var dir, out float mud)) { c.Block("no mud near the start"); yield break; }
            c.Metric("mud_at_pad", mud, "");
            yield return TestWorld.Place(c, v, pad, dir);
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, v);
            v.handbrake = false; v.throttleInput = 1f;
            yield return new WaitForSeconds(6f);
            v.throttleInput = 0f;
            float caked = 0f;
            var tyres = v.GetComponentsInChildren<WheelStats>();
            foreach (var w in tyres) caked = Mathf.Max(caked, w.mud);
            c.Metric("tyre_mud", caked, "");
            c.Check(caked > 0.15f, $"the tyres cake up in the mud ({caked:0.00})");
            c.Screenshot("muddy");
            yield return null;
            if (!TestWorld.Pad(9f, 60f, out var road, out var rdir)) { c.Block("no firm lane"); yield break; }
            yield return TestWorld.Place(c, v, road, rdir, 1f);
            if (Weather.Raining) c.Note("raining: the road is wet, shedding is slower");
            v.handbrake = false; v.throttleInput = 0.8f;
            yield return new WaitForSeconds(10f);
            v.throttleInput = 0f; v.brakeInput = 1f;
            float after = 0f;
            foreach (var w in tyres) after = Mathf.Max(after, w.mud);
            c.Metric("tyre_mud_after_road", after, "");
            c.Check(after < caked, $"driving the road sheds it ({caked:0.00} -> {after:0.00})");
            g.Exit();
        }
    }

    /// <summary>An engine fire burns a car out: with fuel in the tank (the roll forced) it explodes when the fire
    /// reaches it, and ends as a charred, undriveable wreck — tyres on the rims, char-black — that saves.</summary>
    class Burnout : Scenario
    {
        public override string Id => "vehicle.burnout";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = UpdateScenarios1004.Car(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!TestWorld.Pad(9f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1f);
            var away = pad + new Vector3(25f, 0f, 0f); away.y = DeformableTerrain.Instance.Height(away.x, away.z) + 0.3f;
            g.Player.Teleport(away, -90f);
            var sys = v.GetComponent<VehicleSystems>(); var burn = v.GetComponent<VehicleBurn>();
            if (!c.Check(sys && burn, "the car has systems and a burn state")) yield break;
            sys.fuel = 30f;
            burn.RigTank(true);
            burn.burn = 0.45f;
            sys.Heat(5f);
            c.Fixture("30 L in the tank, the fire already halfway (burn 0.45), the tank roll forced to explode");
            float t0 = Time.time;
            while (!burn.exploded && Time.time - t0 < 25f) { if (!sys.Burning) sys.Heat(5f); yield return null; }
            c.Metric("to_explosion", Time.time - t0, "s");
            c.Check(burn.exploded, "the tank goes up when the fire reaches it");
            c.Check(sys.fuel <= 0.01f, "the tank is empty after the blast");
            c.Screenshot("explosion");
            yield return null;
            burn.burn = Mathf.Max(burn.burn, 0.97f);
            t0 = Time.time;
            while (!burn.charred && Time.time - t0 < 20f) { if (!sys.Burning) sys.Heat(5f); yield return null; }
            if (!c.Check(burn.charred, "the fire burns it out")) yield break;
            c.Check(!v.driveable, "a charred wreck can't be driven");
            bool rims = true; foreach (var w in v.GetComponentsInChildren<WheelStats>()) rims &= w.Shredded;
            c.Check(rims, "the tyres burned down to the rims");
            var grime = v.GetComponent<VehicleGrime>();
            c.Check(grime && grime.scorch >= 0.99f, "the body is black with char");
            var state = burn.SaveState();
            c.Check(state != null && state.Contains(",c") && state.Contains(",x"), "the burn saves (" + state + ")");
            yield return new WaitForSeconds(1.5f);
            c.Screenshot("charred");
            yield return null;
        }
    }

    /// <summary>No key, no start; the key in the glovebox doesn't start it until it is in the pack; anyone can hotwire
    /// (a skilled hand quickly); wrecks roll their keys (in, glovebox, lost).</summary>
    class KeysHotwire : Scenario
    {
        public override string Id => "vehicle.keys";
        public override float Timeout => 80f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = UpdateScenarios1004.Car(g);
            if (!v) { c.Block("no car in the fleet"); yield break; }
            var ign = v.GetComponent<VehicleIgnition>(); var sys = v.GetComponent<VehicleSystems>();
            var storage = VehicleStorage.For(v);
            var glove = storage ? storage.Get(VehicleStorage.Kind.Glovebox) : null;
            if (!c.Check(ign && sys, "the car has an ignition")) yield break;
            c.Check(ign.key == VehicleIgnition.Where.Ignition, "fleet cars come with the key in");
            int[] counts = new int[4];
            var rnd = new System.Random(7);
            for (int i = 0; i < 300; i++) { ign.RollWreck(rnd, storage); counts[(int)ign.key]++; if (glove) glove.inventory.TakeItem(ign.KeyItem, 99); }
            c.Metric("keys_in_ignition", counts[0], "/300"); c.Metric("keys_in_glovebox", counts[1], "/300"); c.Metric("keys_lost", counts[3], "/300");
            c.Check(counts[0] > 40 && counts[3] > 100 && (counts[1] > 40 || !glove), $"wrecks: key in {counts[0]}, glovebox {counts[1]}, lost {counts[3]} of 300");
            ign.key = VehicleIgnition.Where.Lost; ign.hotwired = false;
            g.Inventory.TakeItem(ign.KeyItem, 99);
            if (!TestWorld.Pad(9f, out var pad)) { c.Block("no pad"); yield break; }
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1f);
            g.Enter(v); yield return new WaitForSeconds(0.3f);
            sys.Stop();
            v.throttleInput = 0.4f; yield return new WaitForSeconds(1f); v.throttleInput = 0f;
            yield return new WaitForSeconds(1.5f);
            c.Check(!sys.Started, "no key: the starter stays dead");
            if (glove)
            {
                glove.inventory.AddItem(ign.KeyItem);
                ign.key = VehicleIgnition.Where.Glovebox;
                v.throttleInput = 0.4f; yield return new WaitForSeconds(1f); v.throttleInput = 0f;
                yield return new WaitForSeconds(1.5f);
                c.Check(!sys.Started, "the key still in the glovebox doesn't start it");
                glove.inventory.TakeItem(ign.KeyItem);
            }
            g.Inventory.AddItem(ign.KeyItem);
            c.Fixture("the key taken into the pack: " + ItemCatalog.Name(ign.KeyItem));
            yield return TestWorld.StartEngine(c, v, 15f);
            g.Inventory.TakeItem(ign.KeyItem);
            sys.Stop();
            g.Stats.skillXp[(int)Skill.Hotwiring] = CharacterStats.XpForLevel(10);
            g.ForceHotwire = true;
            float t0 = Time.time;
            while (!ign.hotwired && Time.time - t0 < 30f) yield return null;
            g.ForceHotwire = false;
            c.Metric("hotwire_time", Time.time - t0, "s");
            c.Check(ign.hotwired, "a skilled hand hotwires it");
            yield return TestWorld.StartEngine(c, v, 15f);
            c.Check(ign.HotwireChance(0) <= 0.25f && ign.HotwireSeconds(0) >= 15f, $"anyone can try, badly ({ign.HotwireChance(0) * 100f:0}% in {ign.HotwireSeconds(0):0} s untrained)");
            g.Exit();
            ign.key = VehicleIgnition.Where.Ignition;
        }
    }

    /// <summary>Lost limbs: a hand comes off (bit dropped, stump bleeding, the bone gone from the body, the other hand
    /// takes the tools), both hands gone = no tools; a lost leg slows to a hop and keeps the gearbox automatic; the loss
    /// serialises with the look; heavy blade blows roll mangled / severed at the expected rates; NPCs lose limbs too.</summary>
    class LimbLoss : Scenario
    {
        public override string Id => "body.limbs";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            var rnd = new System.Random(11);
            int mangled = 0, severed = 0;
            for (int i = 0; i < 2000; i++) { int r = Limbs.Roll(BodyZone.ArmL, 20f, "MELEE", true, false, rnd); if (r == 1) mangled++; if (r == 2) severed++; }
            int sev2 = 0;
            for (int i = 0; i < 2000; i++) if (Limbs.Roll(BodyZone.ArmL, 20f, "MELEE", true, true, rnd) == 2) sev2++;
            c.Metric("mangle_rate_blade", mangled / 2000f, ""); c.Metric("sever_rate_mangled", sev2 / 2000f, "");
            c.Check(mangled > 300 && mangled < 600 && severed == 0, $"a heavy blade blow mangles about 1 in 5 ({mangled}/2000)");
            c.Check(sev2 > 550 && sev2 < 850, $"a mangled limb comes off about 1 in 3 heavy blows ({sev2}/2000)");
            c.Check(Limbs.Roll(BodyZone.Torso, 99f, "MELEE", true, true, rnd) == 0 && Limbs.Roll(BodyZone.ArmL, 5f, "MELEE", true, true, rnd) == 0, "only heavy blows on limbs count");

            g.Inventory.AddItem(ItemIds.Wrench);
            g.UseItem(ItemIds.Wrench);
            int items0 = 0; foreach (var w in WorldItem.All) if (w && w.key == "limb_hand") items0++;
            g.Sever(BodyZone.HandR, false);
            c.Fixture("the right hand severed");
            yield return null;
            var a = g.Player.Rig.appearance;
            c.Check(Limbs.Severed(a.lost, BodyZone.HandR), "the loss is on the look (saved, sent online)");
            c.Check(g.Player.Rig.Bone(BodyPart.HandR).lossyScale.magnitude < 0.01f, "the hand is gone from the body");
            bool stump = false; foreach (var i in g.Stats.injuries) if (i.zone == BodyZone.HandR && i.type == Wound.Stump && i.Bleeding) stump = true;
            c.Check(stump, "the stump bleeds until dressed");
            int items1 = 0; foreach (var w in WorldItem.All) if (w && w.key == "limb_hand") items1++;
            c.Check(items1 > items0, "the hand drops to the ground");
            c.Check(g.ArmHurtR >= 0.99f && g.CanHoldTools, "the left hand takes over the tools");
            c.Check(g.Player.Tool && g.Player.Tool.transform.parent == g.Player.Rig.Bone(BodyPart.HandL), "the wrench moves to the left hand");
            yield return new WaitForSeconds(0.5f);
            c.Screenshot("one_hand");
            yield return null;
            g.Sever(BodyZone.ArmL, false);
            yield return null;
            c.Check(!g.CanHoldTools && !g.Player.Tool, "no hands: the tool drops away");
            g.UseItem(ItemIds.Wrench);
            c.Check(!g.Player.Tool, "no hands: nothing to hold a wrench with");
            g.Sever(BodyZone.LegL, false);
            yield return null;
            c.Check(g.LimbSpeed < 0.35f && !g.CanRunInjured && !g.CanJumpInjured, $"a lost leg: a hop ({g.LimbSpeed:0.00} speed), no running or jumping");
            c.Check(!g.HasClutchFoot, "no left foot: no clutch");
            c.Check(!g.CanClimb, "no hands: no pulling up onto ledges");
            var copy = JsonUtility.FromJson<Appearance>(JsonUtility.ToJson(a));
            c.Check(copy.lost == a.lost, "the losses survive serialisation");
            yield return new WaitForSeconds(0.5f);
            c.Screenshot("limbs_lost");
            yield return null;
            MadMax.Npc.Npc npc = null;
            foreach (var n in MadMax.Npc.Npc.All) if (n && n.Alive && !n.proxy) { npc = n; break; }
            if (npc)
            {
                npc.SeverLimb(BodyZone.ArmR);
                c.Check(Limbs.Severed(npc.State.lost, BodyZone.ArmR), "an NPC loses an arm and remembers it");
            }
            else c.Note("no NPC near the start to test on");
        }
    }

    /// <summary>A healed stump takes a prosthetic: a hook gives a grip back, a peg leg a slow walk, a blade arm a machete
    /// always at hand on its mount; taken off they go back into the pack; every piece is craftable.</summary>
    class ProstheticsFit : Scenario
    {
        public override string Id => "body.prosthetics";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            int recipes = 0; foreach (var r in RecipeLibrary.All) if (ProstheticLibrary.Is(r.output)) recipes++;
            c.Check(recipes == ProstheticLibrary.All.Count, $"every prosthetic has a recipe ({recipes} of {ProstheticLibrary.All.Count})");
            g.Sever(BodyZone.HandR, true); g.Sever(BodyZone.ArmL, true); g.Sever(BodyZone.LegL, true);
            c.Fixture("right hand, left forearm and left lower leg amputated clean");
            g.Inventory.AddItem("pros_hook"); g.Inventory.AddItem("pros_peg_leg"); g.Inventory.AddItem("pros_blade_arm");
            c.Check(!g.FitProsthetic(BodyZone.HandR, "pros_hook"), "a fresh stump takes nothing yet");
            foreach (var i in g.Stats.injuries) if (i.type == Wound.Stump) i.severity = 0.2f;
            c.Fixture("stumps healed");
            g.LimbAction(BodyZone.HandR);
            c.Check(ProstheticLibrary.FittedOn(g.Player.Rig.appearance, BodyZone.HandR) == "pros_hook" && g.Inventory.GetItem("pros_hook") == 0, "the health page fits the hook from the pack");
            c.Check(Mathf.Abs(g.ArmLoss(false) - 0.5f) < 0.01f && g.CanHoldTools, "the hook holds tools again");
            c.Check(g.Player.Rig.ProstheticGrip(BodyZone.HandR), "the hook hangs on the body with a grip");
            c.Check(g.FitProsthetic(BodyZone.LegL, "pros_peg_leg"), "the peg leg goes on");
            c.Check(Mathf.Abs(g.LimbSpeed - 0.8f) < 0.01f && g.HasClutchFoot && g.CanRunInjured && !g.CanJumpInjured, $"a peg leg walks (speed {g.LimbSpeed:0.00}), runs, doesn't jump");
            c.Check(g.FitProsthetic(BodyZone.ArmL, "pros_blade_arm"), "the blade arm goes on");
            c.Check(g.BuiltInTool(ItemIds.Machete) == 1 && System.Array.IndexOf(g.Hotbar, ItemIds.Machete) >= 0, "the blade arm puts a machete on the hotbar");
            g.UseItem(ItemIds.Machete);
            c.Check(g.Player.Tool && g.Player.Tool.id == ItemIds.Machete && g.Player.Tool.transform.parent == g.Player.Rig.ProstheticGrip(BodyZone.ArmL), "the machete sits on the mount");
            c.Check(!g.CanClimb, "a blade arm doesn't take the body's weight on a ledge");
            yield return new WaitForSeconds(0.6f);
            c.Screenshot("prosthetics");
            yield return null;
            c.Check(g.RemoveProsthetic(BodyZone.HandR) && g.Inventory.GetItem("pros_hook") == 1, "taken off, the hook goes back into the pack");
        }
    }
}
