using System.Collections.Generic;
using System.Text;
using MadMax.Building;
using MadMax.Game;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Built-in storage of a drivable vehicle (added at runtime by <see cref="VehicleDamage"/>, laid out from the
    /// body bounds, the driver's eye and the passenger door, so no rebuild is needed): TRUNK / PICKUP BED reached from
    /// behind, GLOVEBOX at the passenger door or from a seat, BACK SEAT at the rear doors or from a seat; machines carry a
    /// TOOLBOX, motorbikes SADDLEBAGS, aircraft a CARGO POD, boats a LOCKER. Capacities by vehicle class
    /// (<see cref="ProfileFor"/>). The design's cargo hold (<c>VehicleDesign.cargoKg</c>, the root
    /// <see cref="Container"/>) keeps its own save field and gets a rear access point. Contents save in
    /// <c>VehicleSave.storage</c>, replicate with <c>NetSession.SendVehicleStore</c>; wrecks spawn with loot in them
    /// (<see cref="FillWreck"/>, deterministic from the wreck's seed).</summary>
    public class VehicleStorage : MonoBehaviour
    {
        public enum Kind { Trunk, Bed, Glovebox, Seats, Toolbox, Saddlebags, Pod, Locker }

        public readonly List<VehicleCompartment> compartments = new List<VehicleCompartment>();
        VehicleDriver vehicle;
        float dirtyAt = -1f;
        bool loading;

        /// <summary>The vehicle's storage (made once; none for vehicles without a body).</summary>
        public static VehicleStorage For(VehicleDriver v)
        {
            if (!v) return null;
            if (v.TryGetComponent<VehicleStorage>(out var s)) return s;
            s = v.gameObject.AddComponent<VehicleStorage>();
            s.Build(v);
            return s;
        }

        /// <summary>The compartment of a kind (null if this vehicle has none).</summary>
        public Container Get(Kind k) { foreach (var c in compartments) if (c && c.kind == k) return c.container; return null; }
        public VehicleCompartment Compartment(Kind k) { foreach (var c in compartments) if (c && c.kind == k) return c; return null; }

        // ------------------------------------------------------------------ vehicle classes
        struct Spec { public Kind kind; public string title; public float kg; }

        static Spec S(Kind k, string title, float kg) => new Spec { kind = k, title = title, kg = kg };

        /// <summary>Compartments and capacities (kg) by vehicle: named designs first, then by class.</summary>
        static List<Spec> ProfileFor(VehicleDriver v, string name)
        {
            var l = new List<Spec>();
            switch (name)
            {
                case "Pickup": l.Add(S(Kind.Bed, "PICKUP BED", 300f)); l.Add(S(Kind.Glovebox, "GLOVEBOX", 5f)); l.Add(S(Kind.Seats, "BEHIND THE SEAT", 15f)); return l;
                case "TowTruck": l.Add(S(Kind.Bed, "TOOL CHEST", 150f)); l.Add(S(Kind.Glovebox, "GLOVEBOX", 5f)); l.Add(S(Kind.Seats, "BEHIND THE SEAT", 15f)); return l;
                case "MonsterTruck": l.Add(S(Kind.Bed, "PICKUP BED", 200f)); l.Add(S(Kind.Glovebox, "GLOVEBOX", 5f)); return l;
                case "Wagon": case "FiatMultipla": l.Add(S(Kind.Trunk, "TRUNK", 140f)); l.Add(S(Kind.Glovebox, "GLOVEBOX", 5f)); l.Add(S(Kind.Seats, "BACK SEAT", 35f)); return l;
                case "Sedan": case "CitroenBX": case "CitroenXM": case "CitroenXantia": l.Add(S(Kind.Trunk, "TRUNK", 90f)); l.Add(S(Kind.Glovebox, "GLOVEBOX", 5f)); l.Add(S(Kind.Seats, "BACK SEAT", 30f)); return l;
                case "Coupe": case "Interceptor": l.Add(S(Kind.Trunk, "TRUNK", 55f)); l.Add(S(Kind.Glovebox, "GLOVEBOX", 5f)); l.Add(S(Kind.Seats, "BACK SEAT", 15f)); return l;
                case "Trabant": case "LanciaYpsilon": l.Add(S(Kind.Trunk, "TRUNK", 40f)); l.Add(S(Kind.Glovebox, "GLOVEBOX", 3f)); l.Add(S(Kind.Seats, "BACK SEAT", 20f)); return l;
                case "Scavenger": l.Add(S(Kind.Trunk, "TRUNK", 90f)); l.Add(S(Kind.Glovebox, "GLOVEBOX", 5f)); l.Add(S(Kind.Seats, "BACK SEAT", 20f)); return l;
                case "DuneBuggy": l.Add(S(Kind.Trunk, "CARGO NET", 25f)); return l;
                case "Apc": l.Add(S(Kind.Trunk, "STOWAGE BIN", 150f)); l.Add(S(Kind.Glovebox, "MAP CASE", 5f)); return l;
                case "SemiTractor": l.Add(S(Kind.Glovebox, "GLOVEBOX", 8f)); l.Add(S(Kind.Seats, "SLEEPER LOCKER", 60f)); return l;
                case "DirtBike": l.Add(S(Kind.Saddlebags, "TAIL PACK", 10f)); return l;
                case "Chopper": l.Add(S(Kind.Saddlebags, "SADDLEBAGS", 25f)); return l;
                case "SidecarOutfit": l.Add(S(Kind.Saddlebags, "SIDECAR BOOT", 40f)); return l;
                case "Bicycle": case "Raft": return l;
            }
            if (v.GetComponent<Machine>()) { l.Add(S(Kind.Toolbox, "TOOLBOX", 25f)); return l; }
            if (v.GetComponent<BikeBalance>()) { l.Add(S(Kind.Saddlebags, "SADDLEBAGS", 15f)); return l; }
            if (v.GetComponent<FlightModel>()) { l.Add(S(Kind.Pod, "CARGO POD", 10f)); return l; }
            if (v.GetComponent<BoatModel>()) { l.Add(S(Kind.Locker, "LOCKER", 25f)); return l; }
            if (v.GetComponent<InteriorSpace>()) { l.Add(S(Kind.Glovebox, "GLOVEBOX", 8f)); return l; }   // walk-in: the bay is the storage
            l.Add(S(Kind.Trunk, "TRUNK", 60f)); l.Add(S(Kind.Glovebox, "GLOVEBOX", 5f)); l.Add(S(Kind.Seats, "BACK SEAT", 20f));
            return l;
        }

        static readonly string[] LidSockets = { "trunk", "trunk_lid", "boot", "boot_lid", "tailgate", "hatch" };

        Bounds hull; bool hasHull;

        /// <summary>Closest point on the vehicle's body box (oriented with the vehicle, bumpers included) to <paramref name="p"/>;
        /// the rigidbody's world AABB for vehicles without a body mesh. Used to tell who a vehicle actually strikes.</summary>
        public static Vector3 Closest(VehicleDriver v, Vector3 p)
        {
            if (v.TryGetComponent<VehicleStorage>(out var s) && s.hasHull)
            {
                var l = v.transform.InverseTransformPoint(p);
                var b = s.hull;
                l = new Vector3(Mathf.Clamp(l.x, b.min.x, b.max.x), Mathf.Clamp(l.y, b.min.y, b.max.y), Mathf.Clamp(l.z, b.min.z, b.max.z));
                return v.transform.TransformPoint(l);
            }
            return v.Body.ClosestPointOnBounds(p);
        }

        void Build(VehicleDriver v)
        {
            vehicle = v;
            EnsureContext();
            var body = transform.Find("Body");
            var mf = body ? body.GetComponent<MeshFilter>() : null;
            var b = mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(1.8f, 1.4f, 4.2f));
            if (mf && mf.sharedMesh && body.localRotation == Quaternion.identity && body.localScale == Vector3.one)
            {
                hull = new Bounds(b.center + body.localPosition, b.size + new Vector3(0.1f, 0f, 0.5f));      // bumpers, bars
                hull.SetMinMax(new Vector3(hull.min.x, Mathf.Min(hull.min.y, 0.25f), hull.min.z), hull.max);
                hasHull = true;
            }
            float half = Mathf.Max(0.3f, Mathf.Max(-b.min.x, b.max.x));
            float y = Mathf.Clamp(b.min.y, 0f, 1.2f) + 0.35f;
            var eyeT = transform.Find("DriverEye");
            var eye = eyeT ? eyeT.localPosition : new Vector3(-0.35f, 1.2f, b.center.z);
            float driverSide = eye.x < -0.05f ? -1f : eye.x > 0.05f ? 1f : -1f;
            var rear = new Vector3(0f, y, b.min.z - 0.55f);

            // the design's cargo hold (box trailer, van, boats): reached from behind
            if (TryGetComponent<Container>(out var hold) && !GetComponent<Machine>() && !hold.accessPoint)
            {
                var at = new GameObject("CargoAccess").transform;
                at.SetParent(transform, false);
                at.localPosition = rear;
                hold.accessPoint = at;
            }
            if (!v.driveable) return;

            var chassis = GetComponent<VehicleChassis>();
            string name = chassis && !string.IsNullOrEmpty(chassis.vehicleName) ? chassis.vehicleName : v.name;
            var pdoor = transform.Find("PassengerDoor");
            foreach (var sp in ProfileFor(v, name))
            {
                Vector3[] access; bool fromSeat = false; Transform lid = null;
                switch (sp.kind)
                {
                    case Kind.Trunk:
                        access = new[] { rear };
                        if (chassis) foreach (var n in LidSockets) { var s = chassis.FindSocket(n); if (s && s.Current) { lid = s.Current.transform; break; } }
                        break;
                    case Kind.Bed:
                    {
                        float z = Mathf.Lerp(b.min.z, eye.z, 0.35f);
                        access = new[] { rear, new Vector3(half + 0.5f, y, z), new Vector3(-half - 0.5f, y, z) };                // over the tailgate or a side
                        break;
                    }
                    case Kind.Glovebox:
                    case Kind.Pod:
                    case Kind.Locker:
                    {
                        var at = pdoor ? pdoor.localPosition + new Vector3(0f, 0f, 0.25f) : new Vector3(-driverSide * (half + 0.45f), 0f, eye.z + 0.25f);
                        at.y = y;
                        access = new[] { at };
                        fromSeat = true;
                        break;
                    }
                    case Kind.Seats:
                    {
                        float z = eye.z - 0.85f;
                        access = new[] { new Vector3(-driverSide * (half + 0.45f), y, z), new Vector3(driverSide * (half + 0.45f), y, z) };
                        fromSeat = true;
                        break;
                    }
                    case Kind.Toolbox:
                        access = new[] { new Vector3(driverSide * (half + 0.45f), y, eye.z - 0.3f) };
                        break;
                    default:                                                                     // saddlebags: either side of the rear wheel
                    {
                        float z = b.min.z + Mathf.Min(0.45f, b.size.z * 0.2f);
                        access = new[] { new Vector3(half + 0.45f, y, z), new Vector3(-half - 0.45f, y, z) };
                        break;
                    }
                }
                var go = new GameObject("Storage_" + sp.kind);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = access[0];
                var box = go.AddComponent<Container>();
                box.title = sp.title; box.capacity = sp.kg;
                box.accessPoint = go.transform;
                var comp = go.AddComponent<VehicleCompartment>();
                comp.kind = sp.kind; comp.vehicle = v; comp.container = box; comp.access = access; comp.fromSeat = fromSeat; comp.lid = lid;
                box.access = g => comp.CanOpen(g);
                box.inventory.Changed += MarkDirty;
                compartments.Add(comp);
            }
        }

        void MarkDirty()
        {
            if (loading || MadMax.Net.NetSession.Applying) return;
            if (dirtyAt < 0f) dirtyAt = Time.unscaledTime + 0.2f;
        }

        void OnEnable() => VehicleDamage.Scrapped += OnScrapped;
        void OnDisable() => VehicleDamage.Scrapped -= OnScrapped;

        /// <summary>Stripped to nothing: what was inside falls out.</summary>
        void OnScrapped(VehicleDriver v)
        {
            if (v != vehicle) return;
            foreach (var c in compartments) if (c && c.container) c.container.Spill();
        }

        void Update()
        {
            if (dirtyAt < 0f || Time.unscaledTime < dirtyAt) return;
            dirtyAt = -1f;
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.Online) net.SendVehicleStore(vehicle, SaveState());
        }

        // ------------------------------------------------------------------ save / replication
        /// <summary>"Kind\u001dcontents" per non-empty compartment, joined by \u001e (null when all are empty).</summary>
        public string SaveState()
        {
            var sb = new StringBuilder();
            foreach (var c in compartments)
            {
                if (!c || !c.container) continue;
                var st = c.container.SaveState();
                if (st == "|") continue;
                if (sb.Length > 0) sb.Append('\u001e');
                sb.Append(c.kind).Append('\u001d').Append(st);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>Restores every compartment (those missing from <paramref name="s"/> are emptied).</summary>
        public void LoadState(string s)
        {
            loading = true;
            try
            {
                var seen = new HashSet<Kind>();
                if (!string.IsNullOrEmpty(s))
                    foreach (var e in s.Split('\u001e'))
                    {
                        int cut = e.IndexOf('\u001d');
                        if (cut <= 0 || !System.Enum.TryParse(e.Substring(0, cut), out Kind k)) continue;
                        var c = Get(k);
                        if (!c) continue;
                        c.LoadState(e.Substring(cut + 1));
                        seen.Add(k);
                    }
                foreach (var c in compartments) if (c && c.container && !seen.Contains(c.kind)) c.container.LoadState("");
            }
            finally { loading = false; }
        }

        // ------------------------------------------------------------------ wreck loot
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => tablesDefined = false;
        static bool tablesDefined;

        static void DefineTables()
        {
            if (tablesDefined) return;
            tablesDefined = true;
            MadMax.World.LootTables.Define("veh_trunk", ("use_spark_plugs", 1, 2, 1f), ("use_oil_filter", 1, 1, 1f), ("use_air_filter", 1, 1, 0.8f), ("res:8", 1, 4, 1.5f), ("res:9", 1, 4, 1f),
                ("res:7", 2, 8, 1.5f), ("res:1", 2, 8, 2.5f), ("res:5", 1, 3, 1f), ("tool_wrench", 1, 1, 0.4f), ("cloth_coat", 1, 1, 0.3f), ("food_can", 1, 2, 1f), ("drink_water", 1, 2, 1f),
                ("misc_paper", 1, 2, 0.4f), ("ammo_shells", 2, 6, 0.4f), ("trophy_hubcap", 1, 1, 0.3f), ("use_battery", 1, 1, 0.3f), ("med_bandage", 1, 2, 0.6f), ("throw_molotov", 1, 1, 0.2f));
            MadMax.World.LootTables.Define("veh_glovebox", ("misc_paper", 1, 3, 3f), ("med_pills", 1, 2, 1.2f), ("med_bandage", 1, 1, 1f), ("drink_soda", 1, 1, 1f), ("ammo_cartridge", 1, 4, 0.5f),
                ("tool_knife", 1, 1, 0.3f), ("cloth_goggles", 1, 1, 0.5f), ("use_spark_plugs", 1, 1, 0.5f), ("dye_black", 1, 1, 0.2f), ("trophy_ornament", 1, 1, 0.2f), ("use_fuel_additive", 1, 1, 0.4f));
            MadMax.World.LootTables.Define("veh_seat", ("cloth_hoodie", 1, 1, 1f), ("cloth_beanie", 1, 1, 0.6f), ("food_can", 1, 1, 1f), ("drink_soda", 1, 2, 1.2f), ("food_ration", 1, 1, 0.6f),
                ("res:6", 1, 3, 1.2f), ("book_mechanics_1", 1, 1, 0.3f), ("vhs_driving", 1, 1, 0.3f), ("use_canteen", 1, 1, 0.4f), ("cloth_scarf", 1, 1, 0.4f));
            MadMax.World.LootTables.Define("veh_toolbox", ("tool_wrench", 1, 1, 1f), ("use_oil_filter", 1, 1, 1f), ("res:8", 1, 4, 2f), ("res:1", 2, 6, 2f), ("res:20", 1, 2, 0.8f), ("tool_shovel", 1, 1, 0.4f));
        }

        static string TableFor(Kind k) => k == Kind.Glovebox || k == Kind.Pod ? "veh_glovebox" : k == Kind.Seats ? "veh_seat" : k == Kind.Toolbox ? "veh_toolbox" : "veh_trunk";

        /// <summary>A wreck's compartments get what its last owner left (deterministic from <paramref name="seed"/>;
        /// LOOT abundance applies, some were picked clean before you).</summary>
        public void FillWreck(int seed)
        {
            DefineTables();
            var rnd = new System.Random(seed);
            int abundance = GameRules.Current != null ? GameRules.Current.loot : 1;
            loading = true;
            try
            {
                foreach (var c in compartments)
                {
                    if (!c || !c.container) continue;
                    if (rnd.NextDouble() < 0.4 - abundance * 0.1) continue;                          // somebody got here first
                    foreach (var (id, n) in MadMax.World.LootTables.Roll(TableFor(c.kind), rnd, abundance, 0))
                    {
                        if (ItemCatalog.TotalWeight(c.container.inventory) >= c.container.capacity) break;
                        if (id.StartsWith("res:") && int.TryParse(id.Substring(4), out int r)) c.container.inventory.Add((ResourceType)r, n);
                        else c.container.inventory.AddItem(id, n);
                    }
                }
            }
            finally { loading = false; }
        }

        // ------------------------------------------------------------------ context menu
        static void EnsureContext()
        {
            if (!ContextActions.Has("vehicle.storage")) ContextActions.Register("vehicle.storage", Options);
        }

        static void Options(WastelandGame g, ContextTarget t, List<ContextOption> into)
        {
            if (t == null || t.item != null || !t.target || !g.Player) return;
            var v = t.target.GetComponentInParent<VehicleDriver>();
            if (!v || !v.TryGetComponent<VehicleStorage>(out var st) || t.target.GetComponentInParent<Placeable>()) return;
            if (g.Current && g.Current != v) return;
            foreach (var c in st.compartments)
            {
                if (!c || !c.container) continue;
                if (c.Seated(g) && !c.fromSeat) continue;                                         // the boot isn't reached from the wheel
                var box = c.container;
                into.Add(new ContextOption { label = "OPEN " + box.title, run = () => g.Menus.OpenContainer(box), reach = false, blocked = c.Why(g) });
            }
        }
    }
}
