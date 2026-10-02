using MadMax.Items;
using MadMax.Vehicles;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Timed refuelling: from a jerry can (the player pours at the filler neck) or through a pump hose.</summary>
    public partial class WastelandGame
    {
        VehicleSystems refuelTarget;
        GasPump refuelPump;
        float refuelTick;
        LineRenderer hose;
        static Mesh canMesh;

        public bool Refuelling => refuelTarget;

        public VehicleDriver NearestVehicleTo(Vector3 p, float max)
        {
            VehicleDriver best = null; float bd = max;
            foreach (var v in vehicles) { if (!v || !v.GetComponent<VehicleSystems>()) continue; float d = Vector3.Distance(v.transform.position, p); if (d < bd) { bd = d; best = v; } }
            return best;
        }

        /// <summary>The fuel filler: on the left of a car; trucks and buses (bodies over 5.5 m) carry tanks on both
        /// sides, so the one on <paramref name="near"/>'s side (no walk round a long vehicle).</summary>
        static Vector3 Filler(VehicleDriver v, Vector3? near = null)
        {
            var body = v.transform.Find("Body");
            var mf = body ? body.GetComponent<MeshFilter>() : null;
            if (!mf || !mf.sharedMesh) return v.transform.position;
            var b = mf.sharedMesh.bounds;
            float side = -1f;
            if (near.HasValue && b.size.z * Mathf.Abs(body.lossyScale.z) > 5.5f && body.InverseTransformPoint(near.Value).x > b.center.x) side = 1f;
            return body.TransformPoint(new Vector3(b.center.x + side * (b.extents.x + 0.05f), Mathf.Lerp(b.min.y, b.max.y, 0.45f), b.min.z + b.size.z * 0.22f));
        }

        public void StartRefuel(VehicleDriver v, GasPump pump)
        {
            if (!v || !v.TryGetComponent<VehicleSystems>(out var sys)) return;
            if (sys.fuel >= sys.fuelCapacity - 0.5f && pump) { Toast("TANK IS FULL"); return; }
            refuelTarget = sys; refuelPump = pump; refuelTick = 0f;
            if (!pump)
            {
                if (!canMesh)
                {
                    var g = new VoxelGrid();
                    g.Box(-2, -7, -1, 2, 0, 1, Pal.Weathered(Pal.Olive, 0.3f, 1400, 2, 0));
                    g.Box(-1, 1, 0, 1, 1, 0, Pal.Ramp(Pal.Black, 1)); g.Box(2, 0, 0, 3, 2, 0, Pal.Ramp(Pal.Metal, 2));   // handle, spout
                    g.Bevel();
                    canMesh = VoxelMesher.Build(g, "JerryCan");
                }
                Player.HoldProp(canMesh, propMaterial);
                Player.PoseOverride = new HandPoses().Pour;
                Player.ActionClip = "pour";
            }
            Toast(pump ? "REFUELLING FROM THE PUMP" : "POURING...");
        }

        void StopRefuel(string msg)
        {
            if (!refuelTarget) return;
            var v = refuelTarget.GetComponent<VehicleDriver>();
            refuelTarget = null; refuelPump = null;
            Player.PoseOverride = null;
            Player.ActionClip = null;
            Player.DropProp();
            if (hose) hose.enabled = false;
            if (msg != null) Toast(msg);
            MadMax.Net.NetSession.Instance?.SendVehicleMeta(v);
        }

        void UpdateRefuel(float dt)
        {
            if (!refuelTarget) return;
            var v = refuelTarget.GetComponent<VehicleDriver>();
            var filler = Filler(v);
            if (Current || Player.moveInput.sqrMagnitude > 0.1f) { StopRefuel("STOPPED"); return; }
            if (refuelPump)
            {
                if (Vector3.Distance(refuelPump.transform.position, v.transform.position) > 9f) { StopRefuel("HOSE TOO SHORT"); return; }
                if (!hose)
                {
                    hose = new GameObject("FuelHose").AddComponent<LineRenderer>();
                    hose.sharedMaterial = Fx.TransparentMaterial(null);
                    hose.widthMultiplier = 0.05f; hose.positionCount = 10;
                    hose.startColor = hose.endColor = new Color(0.08f, 0.07f, 0.07f, 1f);
                }
                hose.enabled = true;
                var a = refuelPump.Nozzle;
                for (int i = 0; i < 10; i++) { float t = i / 9f; hose.SetPosition(i, Vector3.Lerp(a, filler, t) + Vector3.down * 0.8f * 4f * t * (1f - t)); }
            }
            else
            {
                if (Vector3.Distance(Player.transform.position, filler) > 2.5f) { StopRefuel("TOO FAR FROM THE FILLER"); return; }
                var look = filler - Player.transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) Player.transform.rotation = Quaternion.Slerp(Player.transform.rotation, Quaternion.LookRotation(look), dt * 6f);
                if (Random.value < dt * 6f) Fx.Smoke(filler, Vector3.down * 0.4f, 0.06f, new Color(0.85f, 0.7f, 0.25f, 0.6f), 0.4f);
            }
            if ((refuelTick += dt) < (refuelPump ? 0.2f : 0.45f)) return;
            refuelTick = 0f;
            var sys = refuelTarget;
            if (sys.fuel < sys.fuelCapacity - 0.5f)
            {
                // the engine's fuel only: diesel for diesel engines, petrol (or ethanol) for the rest
                var want = sys.FuelKind;
                if (refuelPump && refuelPump.kind != want) { StopRefuel("THIS PUMP SELLS " + ResourceInfo.Name(refuelPump.kind)); return; }
                var kind = refuelPump ? refuelPump.kind : want == ResourceType.Diesel ? ResourceType.Diesel : Inventory.Get(ResourceType.Fuel) > 0 ? ResourceType.Fuel : ResourceType.Ethanol;
                if (!sys.Accepts(kind)) { StopRefuel("THE TANK HOLDS " + ResourceInfo.Name(sys.tankKind) + ": SIPHON IT FIRST (K)"); return; }
                float got = refuelPump ? refuelPump.Take(1f) : (Inventory.TrySpend(kind, 1) ? 1f : 0f);
                if (got <= 0f) { StopRefuel(refuelPump ? "PUMP RAN DRY" : "OUT OF " + ResourceInfo.Name(want)); return; }
                sys.AddFuel(kind, got);
                return;
            }
            if (!refuelPump) { int n = sys.Service(Inventory); Stats.Practice(MadMax.RPG.Skill.Mechanics, 3f); StopRefuel("SERVICED " + Name(v) + (n > 0 ? ": +" + n + " L OIL/COOLANT" : "")); }
            else StopRefuel("TANK FULL");
        }
    }

    /// <summary>Scripted upper-body poses.</summary>
    public struct HandPoses
    {
        public ToolPose Pour => new ToolPose { chestX = 18f, chestY = -10f, armRX = -55f, armRY = -15f, armRZ = 8f, foreR = -35f, armLX = -45f, armLY = 20f, armLZ = -10f, foreL = -50f, knees = 8f, handRZ = 70f };
        public ToolPose Wash => new ToolPose { chestX = 22f, armRX = -45f, foreR = -60f, armLX = -45f, foreL = -60f, knees = 4f };
    }
}
