using MadMax.Building;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>The house battery of a walk-in vehicle (Hauler module, Bus, Ambulance, Houseboat, any interior): a power
    /// node on the vehicle's bus (<see cref="UtilityGrid"/> joins everything aboard) that the running engine charges
    /// through its alternator. Cabin lights, the fridge and anything built aboard run from it; an onboard generator part
    /// adds to it. Wrecks come with a flat battery. A submarine keeps its own battery instead.
    /// <see cref="Furnish"/> fits a new cabin with strip lights along the ceiling and a light switch beside the main door.</summary>
    public class CabinPower : MonoBehaviour
    {
        public float alternator = 350f;
        UtilityNode node;
        VehicleSystems systems;

        public UtilityNode Node => node;

        /// <summary>Add the battery to a walk-in vehicle (once).</summary>
        public static CabinPower Fit(InteriorSpace space)
        {
            if (!space || space.GetComponent<Submarine>()) return null;
            var have = space.GetComponentInChildren<CabinPower>();
            if (have) return have;
            var go = new GameObject("CabinPower");
            go.transform.SetParent(space.transform, false);
            go.transform.localPosition = new Vector3(0f, space.floorY + 0.3f, (space.min.y + space.max.y) * 0.5f);
            var n = go.AddComponent<UtilityNode>();
            n.kinds = UtilityKind.Power; n.port = Vector3.zero;
            n.batteryWh = 1500f;
            n.batteryCharge = space.name.StartsWith("Wreck") ? 0f : 1100f;
            return go.AddComponent<CabinPower>();
        }

        void Awake() { node = GetComponent<UtilityNode>(); systems = GetComponentInParent<VehicleSystems>(); }

        void Update()
        {
            if (!node) return;
            node.produce = systems && systems.HasEngine && systems.Started ? alternator : 0f;
        }

        /// <summary>Strip lights down the cabin's ceiling and a switch on the wall beside the main door (new cabins only:
        /// saved vehicles bring their pieces back from the save). The switch starts off.</summary>
        public static void Furnish(InteriorSpace space)
        {
            if (!space || space.doors == null || space.doors.Length == 0) return;
            var mat = space.furnitureMaterial;
            float len = space.max.y - space.min.y;
            int n = Mathf.Clamp(Mathf.RoundToInt(len / 3f), 1, 4);
            float x = Mathf.Clamp(0.5f, space.min.x + 0.3f, space.max.x - 0.3f);
            for (int i = 0; i < n; i++)
            {
                float z = Mathf.Lerp(space.min.y, space.max.y, (i + 0.5f) / n);
                FurnitureLibrary.Spawn("light_strip", space.transform, new Vector3(x, space.ceilingY, z), Quaternion.Euler(180f, 0f, 0f), mat);
            }
            if (!SwitchSpot(space, out var pos, out var inward)) return;
            var sw = FurnitureLibrary.Spawn("light_switch", space.transform, pos, Quaternion.LookRotation(Vector3.up, inward), mat);
            if (sw && sw.TryGetComponent<LightSwitch>(out var ls)) ls.on = false;
        }

        /// <summary>A wall spot 1.2 m up, 0.7 m beside the main door on the wall the door is in (vehicle space; the wall is
        /// found against the vehicle's box colliders, else the walkable bounds' edge + 0.25 m).</summary>
        public static bool SwitchSpot(InteriorSpace space, out Vector3 pos, out Vector3 inward)
        {
            var door = space.doors[0].inside;
            float dxMin = door.x - space.min.x, dxMax = space.max.x - door.x, dzMin = door.z - space.min.y, dzMax = space.max.y - door.z;
            float best = Mathf.Min(Mathf.Min(dxMin, dxMax), Mathf.Min(dzMin, dzMax));
            Vector3 outward = best == dzMin ? Vector3.back : best == dzMax ? Vector3.forward : best == dxMin ? Vector3.left : Vector3.right;
            var along = new Vector3(outward.z, 0f, -outward.x);
            inward = -outward;
            float y = space.floorY + 1.2f;
            foreach (float side in new[] { 0.7f, -0.7f })
            {
                var o = new Vector3(door.x, y, door.z) + along * side - outward * 0.3f;
                if (!InsideBounds(space, o)) continue;
                if (WallHit(space, o, outward, out float t)) { pos = o + outward * (t - 0.005f); return true; }
            }
            var fb = new Vector3(door.x, y, door.z) + along * 0.7f;
            if (outward.x != 0f) fb.x = (outward.x > 0f ? space.max.x : space.min.x) + 0.25f * outward.x;
            else fb.z = (outward.z > 0f ? space.max.y : space.min.y) + 0.25f * outward.z;
            pos = fb;
            return true;
        }

        static bool InsideBounds(InteriorSpace s, Vector3 p) => p.x > s.min.x - 0.35f && p.x < s.max.x + 0.35f && p.z > s.min.y - 0.35f && p.z < s.max.y + 0.35f;

        /// <summary>Nearest box collider of the vehicle along a vehicle-space ray (axis-aligned slab test, no physics query:
        /// the colliders may not be synced yet on the first frame).</summary>
        static bool WallHit(InteriorSpace space, Vector3 o, Vector3 d, out float best)
        {
            best = 1.5f;
            bool any = false;
            var root = space.transform;
            foreach (var bc in space.GetComponentsInChildren<BoxCollider>())
            {
                if (bc.isTrigger || bc.GetComponentInParent<Placeable>() || bc.GetComponentInParent<VehiclePart>()) continue;
                var m = root.worldToLocalMatrix * bc.transform.localToWorldMatrix;
                Vector3 mn = Vector3.positiveInfinity, mx = Vector3.negativeInfinity;
                for (int k = 0; k < 8; k++)
                {
                    var c = bc.center + Vector3.Scale(bc.size * 0.5f, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p);
                }
                float t0 = 0f, t1 = best;
                bool hit = true;
                for (int a = 0; a < 3 && hit; a++)
                {
                    if (Mathf.Abs(d[a]) < 1e-5f) { if (o[a] < mn[a] || o[a] > mx[a]) hit = false; continue; }
                    float ta = (mn[a] - o[a]) / d[a], tb = (mx[a] - o[a]) / d[a];
                    if (ta > tb) (ta, tb) = (tb, ta);
                    t0 = Mathf.Max(t0, ta); t1 = Mathf.Min(t1, tb);
                    if (t0 > t1) hit = false;
                }
                if (hit && t0 > 0.01f && t0 < best) { best = t0; any = true; }
            }
            return any;
        }
    }
}
