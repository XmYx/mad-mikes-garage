using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Wrench work between two cars (roadmap 26 Q3 "fit/remove compatible parts"): the hoods come off, the
    /// engines swap places (carried, set down, picked up again, bolted in), then a wheel. The receiving car's shown stats
    /// (bench card torque), mass and engine follow the parts, a part's wear travels with it, the car drives on the new
    /// engine, and the save records what sits in each socket.</summary>
    class PartsSwap : Scenario
    {
        public override string Id => "mobility.parts_swap";
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 150f;

        static bool Ordinary(VehicleDriver v) => v && v.driveable && !v.aiDriven && !v.GetComponent<Machine>() && !v.GetComponent<BikeBalance>()
                                                 && !v.GetComponent<FlightModel>() && !v.GetComponent<BoatModel>() && !v.GetComponent<InteriorSpace>();

        static MountSocket Engine(VehicleDriver v) => v.GetComponent<VehicleChassis>().Sockets.FirstOrDefault(s => s.accepts == PartCategory.Engine && s.Current);
        static MountSocket Of(VehicleDriver v, PartCategory cat) => v.GetComponent<VehicleChassis>().Sockets.FirstOrDefault(s => s.accepts == cat && s.Current);
        static float Torque(VehiclePart p) => p && p.TryGetComponent<EngineStats>(out var e) ? e.maxTorque : 0f;

        static void Beside(WastelandGame g, VehicleDriver v, Vector3 world)
        {
            var local = v.transform.InverseTransformPoint(world);
            var side = v.transform.TransformPoint(new Vector3(Mathf.Sign(local.x == 0f ? 1f : local.x) * 1.9f, 0f, local.z));
            side.y = DeformableTerrain.Instance.Height(side.x, side.z) + 0.05f;
            var look = world - side; look.y = 0f;
            g.Player.Teleport(side, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : v.transform.forward).eulerAngles.y);
        }

        /// <summary>E on a part with the wrench (take) and E at a socket with it carried (mount), from beside it.</summary>
        static IEnumerator Take(ScenarioContext c, VehiclePart part, string what)
        {
            var g = c.Game;
            var owner = part.Socket ? part.Socket.GetComponentInParent<VehicleDriver>() : null;
            if (owner) Beside(g, owner, part.transform.position);
            yield return null; yield return null;
            c.Note($"prompt beside the {what}: '{g.Prompt}'");
            g.TakePart(part);
            yield return MobilityKit.Until(() => g.Player.Carried == part, 3f);
            c.Check(g.Player.Carried == part, $"took the {what} ({part.partId}) and carries it");
        }

        static IEnumerator Mount(ScenarioContext c, MountSocket s, string what)
        {
            var g = c.Game;
            var part = g.Player.Carried;
            var v = s.GetComponentInParent<VehicleDriver>();
            Beside(g, v, s.transform.position);
            yield return null; yield return null;
            c.Note($"prompt at {MobilityKit.N(v)} {s.name} carrying {(part ? part.partId : "nothing")}: '{g.Prompt}'");
            g.MountCarried(s);
            yield return MobilityKit.Until(() => s.Current == part, 3f);
            c.Check(part && s.Current == part && !g.Player.Carried, $"bolted the {what} ({(part ? part.partId : "?")}) onto {MobilityKit.N(v)} {s.name}");
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var cars = g.Fleet.Where(Ordinary).ToList();
            VehicleDriver a = null, b = null; float best = 0f;
            foreach (var x in cars)
            foreach (var y in cars)
            {
                if (x == y) continue;
                var sx = Engine(x); var sy = Engine(y);
                if (!sx || !sy || sx.Current.partId == sy.Current.partId || sx.Current.sizeClass > 2 || sy.Current.sizeClass > 2) continue;
                if (sy.maxSizeClass < sx.Current.sizeClass || sx.maxSizeClass < sy.Current.sizeClass) continue;
                if (sx.Current.partId.Contains("diesel") != sy.Current.partId.Contains("diesel")) continue;   // a diesel in a petrol tank won't run (WrongFuel)
                float gap = Mathf.Abs(Torque(sx.Current) - Torque(sy.Current));
                if (gap > best) { best = gap; a = x; b = y; }
            }
            if (!a || !b) { c.Block("no two fleet cars with swappable engines of different torque"); yield break; }
            if (!TestWorld.Pad(9f, 60f, out var pad, out var fwd)) { c.Block("no level pad with a lane"); yield break; }
            var side = Vector3.Cross(Vector3.up, fwd);
            if (g.Current) { g.Exit(); yield return null; }
            var s = GameSettings.Current; bool keepAnim = s.workAnimation;
            s.workAnimation = false;
            c.Fixture("WORK ANIMATION off: wrench jobs are instant (the timed version is anim.vehicle_work)");
            yield return TestWorld.Place(c, b, pad, fwd, 0.2f);
            yield return TestWorld.Place(c, a, pad + side * 5.5f, fwd, 1.2f);
            MobilityKit.Wrench(c);

            var sa = Engine(a); var sb = Engine(b);
            var ea = sa.Current; var eb = sb.Current;
            float tqA = Torque(ea), tqB = Torque(eb), massB = b.Body.mass;
            var tunB = b.GetComponent<VehicleTuning>();
            float cardB0 = 0f; if (tunB) tunB.Card(out cardB0, out _, out _);
            c.Note($"{MobilityKit.N(a)}: {ea.partId} {tqA:0} Nm (socket max size {sa.maxSizeClass}); {MobilityKit.N(b)}: {eb.partId} {tqB:0} Nm (socket max size {sb.maxSizeClass})");
            ea.damage = 0.3f;
            c.Fixture($"{ea.partId} worn to 70 % (wear must travel with the part)");

            // ---- B: hood off, engine out, set down
            var hoodB = Of(b, PartCategory.Hood); var hoodPartB = hoodB ? hoodB.Current : null;
            if (hoodPartB) { yield return Take(c, hoodPartB, "hood of " + MobilityKit.N(b)); g.Player.DropCarried(); yield return null; }
            massB = b.Body.mass;                                                                  // without its hood
            yield return Take(c, eb, "engine of " + MobilityKit.N(b));
            yield return null;
            c.Check(!sb.Current && !b.Engine, $"{MobilityKit.N(b)} has no engine now");
            c.Check(b.GetComponent<VehicleSystems>().StartChance == 0f, "an engineless car can't start");
            c.Check(Mathf.Abs(b.Body.mass - (massB - eb.mass)) < 1f, $"its mass drops by the engine's {eb.mass:0} kg ({massB:0} -> {b.Body.mass:0} kg)");
            g.Player.DropCarried();
            yield return new WaitForSeconds(0.8f);
            c.Check(eb && !eb.Socket && eb.GetComponent<Rigidbody>(), $"the {eb.partId} lies loose on the ground");

            // ---- A: hood off, engine into B
            var hoodA = Of(a, PartCategory.Hood); var hoodPartA = hoodA ? hoodA.Current : null;
            if (hoodPartA) { yield return Take(c, hoodPartA, "hood of " + MobilityKit.N(a)); g.Player.DropCarried(); yield return null; }
            yield return Take(c, ea, "engine of " + MobilityKit.N(a));
            yield return Mount(c, sb, "engine");
            yield return null;
            var nowB = b.Engine ? b.Engine.GetComponent<VehiclePart>() : null;
            c.Check(nowB == ea, $"{MobilityKit.N(b)} runs the {ea.partId} now ({(nowB ? nowB.partId : "none")})");
            c.Check(Mathf.Abs(ea.damage - 0.3f) < 0.001f, $"the engine's wear came with it ({(1f - ea.damage) * 100f:0} %)");
            c.Check(Mathf.Abs(b.Body.mass - (massB - eb.mass + ea.mass)) < 1f, $"mass follows the parts ({b.Body.mass:0} kg)");
            if (tunB)
            {
                tunB.Card(out float cardB1, out float kw1, out float top1);
                c.Metric("card_torque_before", cardB0, "Nm"); c.Metric("card_torque_after", cardB1, "Nm");
                c.Check(Mathf.Abs(cardB1 - cardB0) > 5f && Mathf.Sign(cardB1 - cardB0) == Mathf.Sign(tqA - tqB), $"the bench card shows the new engine's torque ({cardB0:0} -> {cardB1:0} Nm, {kw1:0} kW, top {top1:0} km/h)");
            }

            // ---- B's old engine from the ground into A
            yield return Take(c, eb, "loose " + eb.partId);
            yield return Mount(c, sa, "engine");
            var nowA = a.Engine ? a.Engine.GetComponent<VehiclePart>() : null;
            c.Check(nowA == eb, $"{MobilityKit.N(a)} runs the {eb.partId} now");

            // ---- hoods back on
            if (hoodPartB) { yield return Take(c, hoodPartB, "hood of " + MobilityKit.N(b)); yield return Mount(c, hoodB, "hood"); }
            if (hoodPartA) { yield return Take(c, hoodPartA, "hood of " + MobilityKit.N(a)); yield return Mount(c, hoodA, "hood"); }

            // ---- a wheel across
            var wsb = b.GetComponent<VehicleChassis>().Sockets.FirstOrDefault(k => k.accepts == PartCategory.Wheel && k.Current && !k.Mirrored && k.transform.localPosition.z < 0f);
            var wsa = wsb ? a.GetComponent<VehicleChassis>().Sockets.FirstOrDefault(k => k.accepts == PartCategory.Wheel && k.Current && k.name == wsb.name) : null;
            if (!wsa && wsb) wsa = a.GetComponent<VehicleChassis>().Sockets.FirstOrDefault(k => k.accepts == PartCategory.Wheel && k.Current && !k.Mirrored);
            if (wsa && wsb && wsb.maxSizeClass >= wsa.Current.sizeClass && wsa.maxSizeClass >= wsb.Current.sizeClass && !wsa.Current.partId.StartsWith("wheel_track"))
            {
                var wa = wsa.Current; var wb = wsb.Current;
                if (wa.sizeClass >= 3 || wb.sizeClass >= 3) MobilityKit.Grant(c, "tool_jack", 1);
                yield return Take(c, wb, "wheel of " + MobilityKit.N(b));
                g.Player.DropCarried(); yield return null;
                yield return Take(c, wa, "wheel of " + MobilityKit.N(a));
                yield return Mount(c, wsb, "wheel");
                var st = wa.GetComponent<WheelStats>();
                c.Note($"{MobilityKit.N(b)} {wsb.name}: {wb.partId} -> {wa.partId} (grip {(st ? st.grip : 0f):0.00}, radius {wa.radius:0.00} m)");
                yield return Take(c, wb, "loose " + wb.partId);
                yield return Mount(c, wsa, "wheel");
            }
            else c.Note("no cross-compatible wheel pair: wheel swap skipped");

            // ---- the save knows what sits where
            var d = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.CaptureSave()));
            var vs = d.vehicles.FirstOrDefault(x => x.netId == b.netId);
            var ss = vs != null ? vs.sockets.FirstOrDefault(x => x.socket == sb.name) : null;
            c.Check(ss != null && ss.part == ea.partId && Mathf.Abs(ss.damage - 0.3f) < 0.01f, $"the save records {ea.partId} at 70 % in {MobilityKit.N(b)}'s {sb.name} ({(ss != null ? ss.part + " " + ss.damage.ToString("0.00") : "missing")})");

            // ---- and it drives on it
            g.Enter(b);
            yield return new WaitForSeconds(0.3f);
            yield return TestWorld.StartEngine(c, b);
            if (!c.Failed || b.GetComponent<VehicleSystems>().Started)
            {
                var p0 = b.transform.position;
                b.handbrake = false; b.throttleInput = 1f;
                yield return new WaitForSeconds(4f);
                b.throttleInput = 0f; b.brakeInput = 1f;
                float ahead = Vector3.Dot(b.transform.position - p0, fwd);
                c.Metric("swapped_engine_4s", ahead, "m");
                if (!c.Check(ahead > 4f, $"drives on the swapped engine ({ahead:0.0} m in 4 s)")) c.Note(TestWorld.State(b) + "; touching: " + TestWorld.Contacts(b));
                yield return MobilityKit.Until(() => Mathf.Abs(b.ForwardSpeed) < 0.3f, 6f);
                b.brakeInput = 0f; b.handbrake = true;
            }
            g.Exit();
            s.workAnimation = keepAnim;
            c.Screenshot("swapped");
            yield return null;
        }
    }

    /// <summary>Paint station and armour (roadmap 26 Q3 "armour, tune, paint and decals"): at a placed PAINT STATION
    /// the shop page previews a colour and a gang decal, SPRAY IT takes the dyes and scrap and recolours the body;
    /// leaving a new preview unpaid puts the paid paint back. With a welder, [U] opens the armour page, steel is welded on
    /// the front for the listed cost, the car gets heavier and the plate soaks a hit. Paint, decal and armour go into
    /// the save and (isolated profile) come back after a reload.</summary>
    class ArmourPaint : Scenario
    {
        public override string Id => "mobility.armour_paint";
        public override string[] Suites => MobilityScenarios.Suites;
        public override float Timeout => 260f;

        static int Changed(Color32[] a, Color32[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return -1;
            int n = 0;
            for (int i = 0; i < a.Length; i++) if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b) n++;
            return n;
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            var v = TestWorld.Vehicle("Sedan") ?? TestWorld.Vehicle("Pickup");
            if (!v) { c.Block("no sedan or pickup in the fleet"); yield break; }
            if (!TestWorld.Pad(10f, out var pad)) { c.Block("no level pad"); yield break; }
            if (g.Current) { g.Exit(); yield return null; }
            var s = GameSettings.Current; bool keepAnim = s.workAnimation;
            s.workAnimation = false;
            c.Fixture("WORK ANIMATION off: welding is instant");
            yield return TestWorld.Place(c, v, pad, Vector3.forward, 1.2f);
            var name = MobilityKit.N(v);
            var body = v.transform.Find("Body").GetComponent<MeshFilter>();

            // ---- paint station
            var booth = FurnitureLibrary.Spawn("paint_booth", g.Build.Structures, pad + Vector3.right * 4.5f, Quaternion.LookRotation(Vector3.left), g.propMaterial);
            yield return null;
            var pb = booth ? booth.GetComponentInChildren<PaintBooth>() : null;
            if (!c.Check(pb, "a paint station can be placed")) { s.workAnimation = keepAnim; yield break; }
            c.Fixture("placed a paint station 4.5 m from the " + name);
            g.Player.Teleport(pad + Vector3.right * 6.5f + Vector3.up * 0.3f, -90f);
            yield return null;
            MobilityKit.Grant(c, "dye_red", 2);
            MobilityKit.Grant(c, ResourceType.Scrap, 6);
            int dye0 = g.Inventory.GetItem("dye_red"), scrap0 = g.Inventory.Get(ResourceType.Scrap);
            c.Check(pb.Prompt(g).Contains(name.ToUpperInvariant()), "the station offers to paint the " + name + ": " + pb.Prompt(g));
            var before = (Color32[])body.sharedMesh.colors32.Clone();
            pb.Use(g, false);
            if (!c.Check(g.Menus.Current == MenuSystem.Page.Paint, "[E] opens the paint shop")) { s.workAnimation = keepAnim; yield break; }
            c.Check(Time.timeScale > 0f, "the paint shop doesn't pause the world");
            var paint = VehiclePaint.Of(v);
            g.Menus.Press("COLOUR", 1);
            g.Menus.Press("DECAL", 1);
            string cost = g.Menus.ValueOf("SPRAY IT");
            c.Note($"colour {g.Menus.ValueOf("COLOUR")}, decal {g.Menus.ValueOf("DECAL")}, cost {cost}");
            c.Check(paint.colour == 1 && paint.decal == 1, $"previewing RED with the {Decals.Names[1]} emblem");
            c.Check(cost != null && cost.Contains("2 ") && cost.Contains("1 SCRAP"), "the price is shown before paying: " + cost);
            c.Check(g.Menus.Press("SPRAY IT"), "SPRAY IT is available");
            yield return null;
            c.Check(g.Inventory.GetItem("dye_red") == dye0 - 2 && g.Inventory.Get(ResourceType.Scrap) == scrap0 - 1, $"paid 2 red dye and 1 scrap (dye {dye0} -> {g.Inventory.GetItem("dye_red")}, scrap {scrap0} -> {g.Inventory.Get(ResourceType.Scrap)})");
            g.Menus.Close();
            yield return null;
            c.Check(paint.colour == 1 && paint.decal == 1, "the paid paint stays after leaving the shop");
            int recoloured = Changed(before, v.transform.Find("Body").GetComponent<MeshFilter>().sharedMesh.colors32);
            c.Metric("body_vertices_recoloured", recoloured, "");
            c.Check(recoloured > 50, $"the body is recoloured ({recoloured} vertices)");
            var decal = v.transform.Find("Body/Decal_R");
            c.Check(decal && decal.gameObject.activeInHierarchy, "the emblem is on the flank");
            c.Check(Decals.Gang(paint.decal) == 0, "the CHROME JACKALS emblem reads as that gang's livery");
            yield return null;
            c.Screenshot("painted");
            yield return null;

            // leaving an unpaid preview puts the paid paint back, for free
            dye0 = g.Inventory.GetItem("dye_red"); scrap0 = g.Inventory.Get(ResourceType.Scrap);
            pb.Use(g, false);
            g.Menus.Press("COLOUR", 1);
            c.Check(paint.colour == 2, "previewing BLUE");
            g.Menus.Close();
            yield return null;
            c.Check(paint.colour == 1 && g.Inventory.Get(ResourceType.Scrap) == scrap0, "walking away unpaid keeps the RED paint and costs nothing");

            // ---- armour
            var armour = v.GetComponent<VehicleArmor>();
            if (!c.Check(armour, name + " takes armour")) { s.workAnimation = keepAnim; yield break; }
            MobilityKit.Grant(c, "tool_welder", 1);
            armour.Cost(ArmorZone.Front, ArmorMat.Steel, 1f, out var r1, out int n1, out var r2, out int n2);
            MobilityKit.Grant(c, r1, n1);
            if (n2 > 0) MobilityKit.Grant(c, r2, n2);
            MobilityKit.Grant(c, ResourceType.Fuel, 2);
            int res0 = g.Inventory.Get(r1), fuel0 = g.Inventory.Get(ResourceType.Fuel);
            float mass0 = v.Body.mass;
            g.Player.Teleport(v.transform.position + v.transform.right * 2.3f + Vector3.up * 0.3f, v.transform.eulerAngles.y - 90f);
            yield return null; yield return null;
            c.Check((g.Prompt ?? "").Contains("ARMOUR"), "the welder offers [U] ARMOUR beside the car: " + g.Prompt);
            ActionPress.Press(Controls.Act.Armour);
            yield return MobilityKit.Until(() => g.Menus.Current == MenuSystem.Page.Armour, 2f);
            if (!c.Check(g.Menus.Current == MenuSystem.Page.Armour, "[U] opens the armour page")) g.Menus.OpenArmour(armour);
            g.Menus.Press("FRONT", 1);
            c.Note("front: " + g.Menus.ValueOf("FRONT"));
            c.Check((g.Menus.ValueOf("FRONT") ?? "").Contains("STEEL") && (g.Menus.ValueOf("FRONT") ?? "").Contains(n1.ToString()), $"the page shows steel for the front and its cost ({n1} {ResourceInfo.Name(r1)})");
            g.Menus.Press("FRONT");
            yield return null;
            g.Menus.Close();
            c.Check(armour.mat[(int)ArmorZone.Front] == ArmorMat.Steel && armour.condition[(int)ArmorZone.Front] > 0.99f, "steel plate welded on the front");
            c.Check(g.Inventory.Get(r1) == res0 - n1 && g.Inventory.Get(ResourceType.Fuel) == fuel0 - 1, $"paid {n1} {ResourceInfo.Name(r1)} and 1 petrol for the torch");
            float kg = armour.Kg(ArmorZone.Front);
            c.Metric("front_armour", kg, "kg");
            c.Check(kg > 1f && Mathf.Abs(v.Body.mass - (mass0 + kg)) < 1f, $"the car is {kg:0} kg heavier ({mass0:0} -> {v.Body.mass:0} kg)");
            var front = v.transform.Find("Body").GetComponent<Renderer>().bounds;
            var hit = v.transform.position + v.transform.forward * (front.extents.z - 0.1f) + Vector3.up * (front.center.y - v.transform.position.y);
            v.GetComponent<VehicleDamage>().ApplyHit(hit, -v.transform.forward, 1f, 0.2f, null);
            c.Fixture("one bullet-strength hit on the front");
            c.Metric("front_plate_after_hit", armour.condition[(int)ArmorZone.Front], "");
            c.Check(armour.condition[(int)ArmorZone.Front] < 0.999f, $"the plate soaks the hit (condition {armour.condition[(int)ArmorZone.Front] * 100f:0} %)");
            s.workAnimation = keepAnim;
            yield return null;
            c.Screenshot("armoured");
            yield return null;

            // ---- saved
            var d = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.CaptureSave()));
            var vs = d.vehicles.FirstOrDefault(x => x.netId == v.netId);
            c.Check(vs != null && vs.paint == "1,1", "the save holds the paint and decal: " + (vs != null ? vs.paint : "vehicle missing"));
            c.Check(vs != null && !string.IsNullOrEmpty(vs.armor) && vs.armor.StartsWith("2,"), "the save holds the front steel: " + (vs != null ? vs.armor : ""));
            if (!Profile.Isolated) { c.Note("not an isolated profile (-profiledir): reload round trip skipped"); yield break; }
            ushort id = v.netId;
            float massSaved = v.GetComponent<VehicleChassis>().TotalMass;
            g.SaveGame(3);
            var old = g;
            g.LoadGame(3);
            while ((WastelandGame.Instance == old || !WastelandGame.Instance || !WastelandGame.Instance.Ready || ScreenFader.Busy) && Time.realtimeSinceStartup - c.startedAt < Timeout - 20f) yield return null;
            g = WastelandGame.Instance;
            if (!c.Check(g && g != old && g.Ready, "slot 3 loads")) yield break;
            WastelandGame.ExternalInput = true;
            yield return new WaitForSeconds(1f);
            var v2 = g.AllVehicles.FirstOrDefault(x => x && x.netId == id);
            if (!c.Check(v2, "the " + name + " is back")) yield break;
            var p2 = v2.GetComponent<VehiclePaint>();
            c.Check(p2 && p2.colour == 1 && p2.decal == 1, "RED with the emblem after the reload");
            var dec2 = v2.transform.Find("Body/Decal_R");
            c.Check(dec2 && dec2.gameObject.activeInHierarchy, "the emblem is drawn after the reload");
            var a2 = v2.GetComponent<VehicleArmor>();
            c.Check(a2 && a2.mat[(int)ArmorZone.Front] == ArmorMat.Steel, "the front steel is back");
            float mass2 = v2.GetComponent<VehicleChassis>().TotalMass;
            c.Check(Mathf.Abs(mass2 - massSaved) < 1f, $"the armoured mass is back ({massSaved:0} -> {mass2:0} kg)");
        }
    }
}
