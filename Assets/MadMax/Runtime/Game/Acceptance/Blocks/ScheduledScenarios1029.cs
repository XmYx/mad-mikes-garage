using System.Collections;
using System.Collections.Generic;
using MadMax.Animals;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Scheduled update 2026-10-29: the airfield windsock swings and fills with the wind, breath shows in the
    /// cold (people and animals), animals print the snow, parkour hands meet what they vault and landings thump, raiders
    /// light the bottle at the window before throwing it, bikes rest on a kickstand, and the rag and bottle economy.</summary>
    public static class ScheduledScenarios1029
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new WindsockSwings();
            yield return new ColdBreathShows();
            yield return new AnimalPrints();
            yield return new ParkourContact();
            yield return new RaiderBottleTelegraph();
            yield return new BikeKickstand();
            yield return new RagAndBottle();
        }

        internal static void Cold(ScenarioContext c, float deg, float snow = 0f)
        {
            Weather.Restore(false, 0f, snow, deg);
            c.Fixture($"weather set dry, {deg:0} °C{(snow > 0f ? ", snow on the ground" : "")}");
        }

        internal static void Warm(ScenarioContext c) => Weather.Restore(false, 0f, 0f, 22f);
    }

    /// <summary>A windsock by the player: the swivel turns downwind (within 45°), as many segments stand out as the wind
    /// speed says (one per 1.54 m/s), the tail lies downwind of the mast when any segment stands, and it reads as text.</summary>
    class WindsockSwings : Scenario
    {
        public override string Id => "aviation.windsock";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var at = g.Player.transform.position + ScheduledScenarios1016.Fwd(g) * 7f;
            var terrain = DeformableTerrain.Instance;
            if (terrain) at.y = terrain.Height(at.x, at.z);
            c.Fixture("a windsock placed 7 m in front of the player (airfield extra)");
            var go = new GameObject("TestWindsock");
            go.transform.position = at;
            var sock = go.AddComponent<Windsock>();
            yield return SurvivalKit.GameSeconds(4f, 20);
            var swivel = go.transform.Find("Swivel");
            c.Check(swivel && swivel.Find("Sock0") && swivel.GetComponentsInChildren<MeshFilter>().Length >= 6, "built: ring and five sock segments");
            var w = Fx.Wind; w.y = 0f;
            float speed = w.magnitude;
            c.Metric("wind", speed, "m/s");
            int expect = Mathf.Clamp(Mathf.FloorToInt(speed / (Windsock.FullWind / 5f) + 0.5f), 0, 5);
            c.Check(Mathf.Abs(sock.Filled - expect) <= 1, $"{sock.Filled} segments out in {speed:0.0} m/s (about {expect})");
            if (speed > 1f)
            {
                float off = Mathf.Abs(Mathf.DeltaAngle(sock.Heading, Mathf.Atan2(w.x, w.z) * Mathf.Rad2Deg));
                c.Metric("sock_off_wind", off, "deg");
                c.Check(off < 45f, $"the sock points downwind ({off:0}° off)");
            }
            if (sock.Filled > 0)
            {
                var top = at + Vector3.up * Windsock.MastHeight;
                var d = sock.Tail - top; d.y = 0f;
                c.Check(Vector3.Dot(d, w.normalized) > 0.3f, "the tail stands out downwind of the mast");
            }
            c.Check(!string.IsNullOrEmpty(sock.Reading) && sock.Reading.StartsWith("SOCK"), "a pilot can read it: " + sock.Reading);
            var view = g.cameraRig ? g.cameraRig.mode : ViewMode.Overhead;
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward((at - g.Player.transform.position).normalized + Vector3.up * 0.45f); }
            yield return SurvivalKit.GameSeconds(1f, 5);
            c.Screenshot("windsock");
            yield return null;
            if (g.cameraRig) g.cameraRig.mode = view;
            yield return null;
            Object.Destroy(go);
        }
    }

    /// <summary>At −10 °C the player breathes clouds, more often when running; at 22 °C none.</summary>
    class ColdBreathShows : Scenario
    {
        public override string Id => "living.cold_breath";
        public override float Timeout => 150f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            ScheduledScenarios1029.Cold(c, -10f);
            yield return null;
            c.Check(ColdBreath.Strength(g.Player.transform.position) > 0.8f, "−10 °C reads as full-strength cold");
            int p0 = ColdBreath.Puffs;
            yield return SurvivalKit.GameSeconds(12f, 20);
            int rest = ColdBreath.Puffs - p0;
            c.Metric("puffs_rest_12s", rest, "");
            c.Check(rest >= 2, $"breath clouds at rest ({rest} in 12 s)");
            c.Screenshot("cold_breath");
            yield return null;
            ScheduledScenarios1029.Warm(c);
            yield return null;
            int p1 = ColdBreath.Puffs;
            yield return SurvivalKit.GameSeconds(6f, 20);
            c.Check(ColdBreath.Puffs - p1 <= 1, $"none in the warm ({ColdBreath.Puffs - p1})");
        }
    }

    /// <summary>A ridden horse in snow leaves hoofprints and breathes from its nostrils in the cold.</summary>
    class AnimalPrints : Scenario
    {
        public override string Id => "animals.prints_breath";
        public override float Timeout => 60f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            if (!TestWorld.Pad(4f, out var pad)) { c.Block("no clear pad near the player"); yield break; }   // off the road: snow lies there
            var dir = ScheduledScenarios1016.Fwd(g);
            ScheduledScenarios1029.Cold(c, -6f, 1f);
            var def = AnimalLibrary.Get("horse");
            if (!c.Check(def != null, "the horse species exists")) yield break;
            g.Player.Teleport(pad + Vector3.up * 0.1f, PH.Yaw(dir));
            c.Fixture("a tame saddled horse at a clear lane, mounted");
            var a = Animal.Spawn(def, pad + Vector3.Cross(Vector3.up, dir) * 1.5f, PH.Yaw(dir), g.propMaterial, "t1029");
            a.owned = true;
            a.SetSaddled(true);
            yield return SurvivalKit.GameSeconds(0.5f);
            a.Mount(g);
            yield return SurvivalKit.GameSeconds(0.5f);
            int prints = Animal.Prints, puffs = ColdBreath.Puffs;
            g.Player.viewYaw = PH.Yaw(dir);
            g.Player.moveInput = new Vector2(0f, 1f);
            yield return SurvivalKit.GameSeconds(6f, 30);
            g.Player.moveInput = Vector2.zero;
            c.Metric("prints", Animal.Prints - prints, "");
            c.Check(Animal.Prints - prints >= 4, $"hoofprints in the snow ({Animal.Prints - prints})");
            c.Check(ColdBreath.Puffs > puffs, $"breath from horse and rider ({ColdBreath.Puffs - puffs} clouds)");
            c.Screenshot("hoofprints");
            yield return null;
            g.Player.StandUp();
            yield return SurvivalKit.GameSeconds(0.5f);
            Object.Destroy(a.gameObject);
            ScheduledScenarios1029.Warm(c);
        }
    }

    /// <summary>Vaulting a wall: the hands meet its top (a plant, a sound); a fall from 3 m thumps down in dust.</summary>
    class ParkourContact : Scenario
    {
        public override string Id => "parkour.contact";
        public override float Timeout => 40f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var P = g.Player;
            yield return PH.OnFoot(g);
            if (!TestWorld.Pad(3f, 16f, out var pad, out var dir)) { c.Block("no clear lane near the start"); yield break; }
            var t = DeformableTerrain.Instance;
            var wallAt = pad + dir * 6f;
            c.Fixture("a 0.9 m test wall (box) on a clear lane");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "ContactTestWall";
            wall.transform.SetPositionAndRotation(new Vector3(wallAt.x, t.Height(wallAt.x, wallAt.z) + 0.43f, wallAt.z), Quaternion.LookRotation(dir));
            wall.transform.localScale = new Vector3(3f, 0.9f, 0.3f);
            g.Stats.stamina = g.Stats.MaxStamina;
            var start = pad + dir * 3f; start.y = t.Height(start.x, start.z) + 0.1f;
            P.Teleport(start, PH.Yaw(dir));
            P.viewYaw = PH.Yaw(dir);
            yield return SurvivalKit.GameSeconds(0.4f, 5);
            int plants = PlayerCharacter.HandPlants;
            P.moveInput = new Vector2(0f, 1f);
            yield return SurvivalKit.Until(() => Vector3.Dot(P.transform.position - wallAt, dir) > -0.75f, 6f);
            P.jump = true;
            yield return SurvivalKit.Until(() => P.Traversing, 1.5f);
            bool vaulted = P.Traversing;
            yield return SurvivalKit.Until(() => !P.Traversing, 4f);
            P.moveInput = Vector2.zero; P.jump = false;
            c.Check(vaulted, "Space at the wall vaults it");
            c.Check(PlayerCharacter.HandPlants > plants && (PlayerCharacter.LastPlant ?? "").StartsWith("HANDS"), $"the hands meet the wall's top ({PlayerCharacter.LastPlant ?? "none"})");
            // a drop from 3 m
            int thumps = PlayerCharacter.LandingThumps;
            var drop = pad + dir * 12f; drop.y = t.Height(drop.x, drop.z) + 3f;
            P.Teleport(drop, PH.Yaw(dir));
            yield return SurvivalKit.Until(() => PlayerCharacter.LandingThumps > thumps, 5f);
            c.Check(PlayerCharacter.LandingThumps > thumps, "a 3 m fall lands with a thump and dust");
            c.Screenshot("landing_dust");
            yield return null;
            Object.Destroy(wall);
        }
    }

    /// <summary>A raider's molotov is lit at the car window first (a lit bottle with its flame, nothing in flight yet) and
    /// thrown after the delay as a burning bottle.</summary>
    class RaiderBottleTelegraph : Scenario
    {
        public override string Id => "npc.raider_bottle";
        public override float Timeout => 30f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            var car = ScheduledScenarios1028.NearestCar(g, 80f);
            if (!c.Check(car, "a car near the player")) yield break;
            var me = g.Player.transform;
            var at = car.transform.position + car.transform.right * 13f;
            var terrain = DeformableTerrain.Instance;
            at.y = terrain.Height(at.x, at.z) + 0.1f;
            g.Player.Teleport(at, PH.Yaw(car.transform.position - at));
            c.Fixture("the player 13 m off a car's right side; the car lights a bottle as a raider would");
            yield return SurvivalKit.GameSeconds(0.3f);
            int lit = RaiderBottle.Lit, thrown = RaiderBottle.Thrown;
            var last = WastelandGame.LastThrownBody;
            c.Check(RaiderBottle.Light(car, me), "a bottle is lit at the window");
            yield return null;                                                                      // one frame: under the delay at any frame rate (dt ≤ 0.33 s)
            var prop = car.transform.Find("RaiderBottle");
            c.Check(RaiderBottle.Lit == lit + 1 && prop && prop.GetComponent<ThrownLit>(), "the burning bottle shows at the car");
            c.Check(prop && car.transform.InverseTransformPoint(prop.position).x > 0f, "on the side facing the player");
            c.Check(WastelandGame.LastThrownBody == last, "nothing thrown yet: a moment to react");
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward((car.transform.position - at).normalized); }
            yield return null;
            c.Screenshot("raider_bottle_lit");
            yield return SurvivalKit.Until(() => RaiderBottle.Thrown > thrown, 4f);
            yield return null;                                                                      // the prop is destroyed at the end of the release frame
            c.Check(RaiderBottle.Thrown == thrown + 1, "then thrown");
            var body = WastelandGame.LastThrownBody;
            c.Check(body && body != last && body.name == "Molotov", "a molotov in flight");
            c.Check(!car.transform.Find("RaiderBottle"), "the hand is empty");
            yield return SurvivalKit.GameSeconds(3f);
            g.Player.Teleport(at + (at - car.transform.position).normalized * 10f, 0f);
        }
    }

    /// <summary>A dirt bike: parked, the kickstand is down and it leans on it; ridden, the stand folds up; off again, down.</summary>
    class BikeKickstand : Scenario
    {
        public override string Id => "vehicle.kickstand";
        public override float Timeout => 120f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            if (!TestWorld.Pad(4f, 8f, out var pad, out var dir)) { c.Block("no clear pad near the start"); yield break; }
            c.Fixture("a dirt bike spawned on a clear pad");
            var bike = g.SpawnFound("DirtBike", pad + Vector3.up * 0.3f, Quaternion.LookRotation(dir));
            if (!c.Check(bike, "the dirt bike spawned")) yield break;
            yield return SurvivalKit.GameSeconds(2.5f, 20);
            var stand = bike.GetComponent<Kickstand>();
            c.Check(stand, "it has a kickstand");
            c.Check(stand && stand.Down > 0.9f, $"parked: the stand is down ({(stand ? stand.Down : 0f):0.00})");
            var bb = bike.GetComponent<BikeBalance>();
            c.Check(bb && bb.Lean > 3f, $"leaning on it ({(bb ? bb.Lean : 0f):0}°)");
            var look = bike.transform.position - bike.transform.right * 3.2f; look.y = DeformableTerrain.Instance.Height(look.x, look.z) + 0.1f;
            g.Player.Teleport(look, PH.Yaw(bike.transform.right));
            var view = g.cameraRig ? g.cameraRig.mode : ViewMode.Overhead;
            if (g.cameraRig) { g.cameraRig.mode = ViewMode.ThirdPerson; g.cameraRig.LookToward(bike.transform.right - Vector3.up * 0.25f); }
            yield return SurvivalKit.GameSeconds(1f, 5);
            c.Screenshot("kickstand");
            yield return null;
            if (g.cameraRig) g.cameraRig.mode = view;
            g.Enter(bike);
            yield return SurvivalKit.GameSeconds(1f, 10);
            c.Check(stand && stand.Down < 0.1f, $"ridden: folded up ({(stand ? stand.Down : 0f):0.00})");
            g.Exit();
            yield return SurvivalKit.GameSeconds(1.5f, 10);
            c.Check(stand && stand.Down > 0.9f, "off again: kicked down");
            c.Check(stand && stand.Kicks >= 2, $"clicks as it goes ({(stand ? stand.Kicks : 0)})");
            Object.Destroy(bike.gameObject);
        }
    }

    /// <summary>A bottled drink leaves glass, a garment falling apart leaves rags; with fuel that makes a molotov by hand.</summary>
    class RagAndBottle : Scenario
    {
        public override string Id => "items.rag_and_bottle";
        public override float Timeout => 20f;

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return PH.OnFoot(g);
            int glass = g.Inventory.Get(ResourceType.Glass), cloth = g.Inventory.Get(ResourceType.Cloth);
            g.Inventory.AddItem("drink_soda", 1);
            g.Eat("drink_soda");
            yield return null;
            c.Check(g.Inventory.Get(ResourceType.Glass) == glass + 1, "the soda leaves its bottle (glass +1)");
            // a worn-through jacket falls apart
            var outfit = g.Player.Rig.outfit;
            string torso = null;
            foreach (var id in outfit) { var d = ClothingLibrary.Get(id); if (d != null && (d.slot == ClothingSlot.Torso || d.slot == ClothingSlot.Outer) && d.mendWith == ResourceType.Cloth) { torso = id; break; } }
            if (torso != null)
            {
                g.ClothWear[torso] = 0.999f;
                g.TearClothes(null, 5f);
                yield return null;
                c.Check(g.Inventory.Get(ResourceType.Cloth) >= cloth + 2, $"the {torso} fell apart into rags (cloth {cloth} -> {g.Inventory.Get(ResourceType.Cloth)})");
            }
            else c.Check(WastelandGame.IsBottled("drink_beer"), "no cloth torso garment worn: bottles only");
            var r = RecipeLibrary.Get("molotov");
            c.Check(r != null && r.hand, "the molotov is made by hand (HANDCRAFT)");
        }
    }
}
