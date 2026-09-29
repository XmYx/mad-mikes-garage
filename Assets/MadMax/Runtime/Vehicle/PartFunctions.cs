using System.Collections.Generic;
using MadMax.Building;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Runtime functions of attachment parts, added by part id when a part instance wakes (the baked prefabs are
    /// plain meshes): storage, water tank, onboard generator, lights, snorkel, roof access and weapons. Part-level
    /// interactables are listed here so on-foot interaction finds them.</summary>
    public static class PartFunctions
    {
        /// <summary>Usable functions on parts (water tap, generator switch, roof steps) for on-foot interaction.</summary>
        public static readonly List<MonoBehaviour> Interactables = new List<MonoBehaviour>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Interactables.Clear();

        public static void Setup(VehiclePart p)
        {
            var go = p.gameObject;
            switch (p.partId)
            {
                case "cargo_roof_rack": Box(go, "ROOF RACK", 120f); break;
                case "cargo_box": Box(go, "CARGO BOX", 160f); break;
                case "cargo_water_tank":
                {
                    var n = Node(p, UtilityKind.Water, new Vector3(0f, 0.4f, 0f));
                    n.waterCapacity = 400f;
                    go.AddComponent<WaterSource>().mode = WaterSource.Mode.Rain;
                    Use(go.AddComponent<WaterOutlet>()).kind = WaterOutlet.Kind.Barrel;
                    break;
                }
                case "cargo_generator":
                {
                    Node(p, UtilityKind.Power, new Vector3(0f, 0.72f, 0f));
                    var gen = Use(go.AddComponent<Generator>());
                    gen.output = 2500f; gen.tankLitres = 10f;
                    go.AddComponent<FuelFeed>();
                    break;
                }
                case "lights_bar": case "lights_search": go.AddComponent<PartLight>(); break;
                case "snorkel": go.AddComponent<Snorkel>(); break;
                case "steps_side": case "steps_ladder": Use(go.AddComponent<RoofAccess>()); break;
                case "weapon_mg": go.AddComponent<MountedGun>(); break;
                case "weapon_harpoon": go.AddComponent<Harpoon>(); break;
                case "weapon_flamer": go.AddComponent<Flamethrower>(); break;
                case "rear_dropper": go.AddComponent<RearDropper>(); break;
                case "armor_smoke": go.AddComponent<SmokeLauncher>(); break;
            }
        }

        static T Use<T>(T c) where T : MonoBehaviour { Interactables.Add(c); return c; }

        static void Box(GameObject go, string title, float kg)
        {
            var c = go.AddComponent<Container>();
            c.title = title; c.capacity = kg;
        }

        /// <summary>A utility node on a part: joins the vehicle's power / water bus while mounted.</summary>
        static UtilityNode Node(VehiclePart p, UtilityKind kind, Vector3 port)
        {
            var n = p.gameObject.AddComponent<UtilityNode>();
            n.kinds = kind; n.port = port;
            p.Mounted += (a, b) => UtilityGrid.Invalidate();
            p.Unmounted += (a, b) => UtilityGrid.Invalidate();
            return n;
        }
    }
}
