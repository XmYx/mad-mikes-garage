using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Build pieces for depth stage F: water quality, desalination, power control, biogas, cold storage.
    /// Sea water: solar still (sun, no power) → desalinator (power, fast; also cleans a network's brine). Power
    /// control: switch, timer, light sensor, float switch, load breaker (priority shedding), water valve. Biogas:
    /// digester (manure, rotten food) → biogas generator (hose within 8 m). Cold storage: chest freezer, ice box.
    /// The existing fridge gains a temperature model (<see cref="ColdStore"/>) and the water filter a cartridge
    /// (<see cref="FilterCartridge"/>): both are added to their definitions here, so the base list stays untouched.</summary>
    public static partial class FurnitureLibrary
    {
        static IEnumerable<FurnitureDef> UtilitiesPieces()
        {
            UtPatch("fridge", go => go.AddComponent<ColdStore>().kind = ColdStore.Kind.Fridge);
            UtPatch("filter", go => go.AddComponent<FilterCartridge>());

            var U = BuildCategory.Utility; var Fu = BuildCategory.Furniture;
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var G = ResourceType.Glass; var C = ResourceType.Cloth; var Rb = ResourceType.Rubber;
            var Fe = ResourceType.Iron; var Cu = ResourceType.Copper; var Al = ResourceType.Aluminium;

            // ---- sea water: the still (sun) and the desalinator (power)
            yield return D("solar_still", "SOLAR STILL", U, UtStillGrid(), 4, false, go =>
            {
                Node(go, UtilityKind.Water, 0.3f).waterCapacity = 30f;
                go.AddComponent<SolarStill>();
            }, (W, 4), (G, 4), (S, 2));
            yield return Kit("desalinator", "DESALINATOR", UtDesalGrid(), UtilityIds.DesalKit, U).With(go =>
            {
                Node(go, UtilityKind.Power | UtilityKind.Water, 0.7f).waterCapacity = 40f;
                var st = Station(go, "desalinator", "DESALINATE (DESALINATOR)", 1200f);
                st.output = new Vector3(-0.6f, 0.7f, 0.3f);
                go.AddComponent<Desalinator>();
            });

            // ---- power control
            yield return D("power_switch", "POWER SWITCH", U, UtSwitchGrid(0), 3, false, go => UtSwitch(go, UtilitySwitch.Mode.Manual, UtilityKind.Power), (Cu, 1), (S, 1));
            yield return D("power_timer", "POWER TIMER", U, UtSwitchGrid(1), 3, false, go => UtSwitch(go, UtilitySwitch.Mode.Timer, UtilityKind.Power), (Cu, 2), (S, 1), (G, 1));
            yield return D("light_sensor", "LIGHT SENSOR", U, UtSwitchGrid(2), 3, false, go => UtSwitch(go, UtilitySwitch.Mode.Sensor, UtilityKind.Power), (Cu, 1), (S, 1), (G, 1));
            yield return D("float_switch", "FLOAT SWITCH", U, UtSwitchGrid(3), 3, false, go => UtSwitch(go, UtilitySwitch.Mode.Float, UtilityKind.Power), (Cu, 1), (S, 1), (Rb, 1));
            yield return D("breaker", "LOAD BREAKER", U, UtBreakerGrid(), 4, false, go => { Node(go, UtilityKind.Power, 0.5f); go.AddComponent<PowerBreaker>(); }, (Cu, 3), (S, 2));
            yield return D("water_valve", "WATER VALVE", U, UtValveGrid(), 4, false, go => UtSwitch(go, UtilitySwitch.Mode.Manual, UtilityKind.Water), (Fe, 1), (S, 2));

            // ---- biogas
            yield return D("biogas_digester", "BIOGAS DIGESTER", U, UtDigesterGrid(), 12, false, go => go.AddComponent<BiogasDigester>(), (S, 14), (Fe, 4), (Rb, 4));
            yield return D("biogas_generator", "BIOGAS GENERATOR", U, UtGasGenGrid(), 8, false, go =>
            {
                Node(go, UtilityKind.Power, 0.8f);
                var gen = go.AddComponent<Generator>(); gen.gas = true; gen.output = 1200f; gen.tankLitres = 120f; gen.burnRate = 12f;
                go.AddComponent<GasHose>();
            }, (Fe, 6), (Cu, 4), (S, 4), (Rb, 2));

            // ---- cold storage
            yield return D("freezer", "CHEST FREEZER", Fu, UtFreezerGrid(), 5, false, go =>
            {
                Node(go, UtilityKind.Power, 0.6f);
                Box(go, "FREEZER", 80f, true);
                var cs = go.AddComponent<ColdStore>(); cs.kind = ColdStore.Kind.Freezer; cs.watts = 250f;
            }, (S, 10), (Cu, 2), (Al, 2));
            yield return D("ice_box", "ICE BOX", Fu, UtIceBoxGrid(), 3, false, go =>
            {
                Box(go, "ICE BOX", 40f, true);
                go.AddComponent<ColdStore>().kind = ColdStore.Kind.IceBox;
            }, (W, 6), (S, 2), (C, 2));
            // an earth-banked root cellar: no power, a third of the rot (a sixth in winter) for the harvest
            yield return D("root_cellar", "ROOT CELLAR", Fu, UtCellarGrid(), 8, false, go =>
            {
                Box(go, "ROOT CELLAR", 160f, false);
                go.GetComponent<Container>().keep = 0.33f;
            }, (W, 10), (ResourceType.Stone, 14));
        }

        /// <summary>Add a function to an existing definition's setup (runs after its own).</summary>
        static void UtPatch(string id, System.Action<GameObject> extra)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i].id != id) continue;
                var old = defs[i].setup;
                defs[i].setup = go => { old?.Invoke(go); extra(go); };
                return;
            }
        }

        static void UtSwitch(GameObject go, UtilitySwitch.Mode mode, UtilityKind kind)
        {
            Node(go, mode == UtilitySwitch.Mode.Float ? UtilityKind.Power | UtilityKind.Water : kind, 0.35f);
            var s = go.AddComponent<UtilitySwitch>(); s.mode = mode; s.cuts = kind;
        }

        /// <summary>A shallow black basin on timber legs under a pitched glass roof, gutters along both eaves into a spout.</summary>
        static VoxelGrid UtStillGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -9, 9 }) foreach (int z in new[] { -6, 6 }) g.Box(x, 0, z, x, 3, z, Pal.Ramp(Pal.Wood, 1, 4101));
            g.Box(-9, 3, -6, 9, 3, 6, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 4102), Pal.Ramp(Pal.Wood, 1, 4103), 0, 3));      // plank bed
            g.Mat(Scrap);
            g.Box(-9, 4, -6, 9, 5, 6, Pal.Weathered(Pal.Metal, 0.45f, 4104, 1, 0));                                       // tray walls
            g.Box(-8, 5, -5, 8, 5, 5, p => (p.x + p.z) % 5 == 0 ? Pal.Black[2] : Pal.Black[0]);                             // black basin, a glint of water
            g.Mat(Glass);
            for (int z = -6; z <= 6; z++)
            {
                int y = 6 + (6 - Mathf.Abs(z)) / 2;
                g.Box(-9, y, z, 9, y, z, p => Mathf.Abs(p.x) == 9 ? Pal.Chrome[1] : (p.x + 20) % 6 == 0 ? Pal.PaleBlue[3] : Pal.Glass[3]);   // roof panes, condensate streaks
            }
            g.Mat(Scrap);
            foreach (int z in new[] { -7, 7 }) g.Box(-9, 5, z, 9, 5, z, Pal.Ramp(Pal.Chrome, 1, 4105));                     // gutters
            g.Box(10, 3, 7, 10, 5, 7, Pal.Ramp(Pal.Chrome, 2)); g.Set(10, 2, 7, Pal.Solid(Pal.PaleBlue[2]));               // spout, a drip
            g.Mat(Stone); g.Set(0, 6, 0, Pal.Ramp(Pal.Sand, 3)); g.Box(-4, 5, 0, -3, 5, 1, Pal.Solid(Pal.Cream[4]));   // pebble weight, salt crust
            return g;
        }

        /// <summary>Skid-mounted desalinator: a long white membrane vessel, a high-pressure pump and motor, gauges, a blue
        /// intake, a salt tray at the brine end.</summary>
        static VoxelGrid UtDesalGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-10, 0, -5, 10, 1, 5, p => (p.x + p.z) % 6 == 0 ? Pal.Metal[0] : Pal.Metal[1]);                              // skid
            foreach (int z in new[] { -2, 2 })
            {
                g.CylX(5, z * 1.2f, 2f, -9, 6, Pal.Weathered(Pal.Cream, 0.18f, 4111 + z, 2, 0));                            // membrane vessels
                foreach (int x in new[] { -9, 6 }) g.CylX(5, z * 1.2f, 2.3f, x, x, Pal.Ramp(Pal.Chrome, 1));               // end caps
            }
            g.Box(7, 2, -4, 10, 7, 4, Pal.Weathered(Pal.Navy, 0.3f, 4113, 2, 0));                                          // pump
            g.Box(8, 8, -2, 10, 10, 2, Pal.Weathered(Pal.Olive, 0.3f, 4114, 2, 0));                                       // motor
            g.Box(8, 11, 0, 8, 11, 0, Pal.Solid(Pal.Amber));                                                                // run lamp
            g.Mat(Glass);
            foreach (int x in new[] { -3, 1 }) { g.Box(x, 8, 0, x + 1, 9, 0, Pal.Solid(Pal.Cream[4])); g.Set(x, 9, 1, Pal.Solid(Pal.Black[0])); }   // gauges
            g.Mat(Scrap);
            g.Box(-3, 7, 0, 2, 7, 0, Pal.Ramp(Pal.Chrome, 2));                                                              // gauge manifold
            g.CylZ(4, 3, 1f, 5, 9, Pal.Ramp(Pal.PaleBlue, 1));                                                             // intake
            g.Box(-10, 2, 5, -6, 3, 8, Pal.Weathered(Pal.Metal, 0.4f, 4115, 1, 0));                                        // salt tray
            g.Box(-9, 3, 6, -7, 3, 7, Pal.Solid(Pal.Cream[4]));
            return g;
        }

        /// <summary>Small wall-mount control boxes: 0 lever switch, 1 clock timer, 2 light sensor dome, 3 float switch on a pipe.</summary>
        static VoxelGrid UtSwitchGrid(int kind)
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-1, 0, -1, 1, 1, 1, Pal.Ramp(Pal.Metal, 0, 4120));                                                       // foot
            g.Box(0, 2, 0, 0, 3, 0, Pal.Ramp(Pal.Metal, 1));
            g.Box(-3, 4, -1, 3, 11, 1, Pal.Weathered(kind == 1 ? Pal.Cream : kind == 3 ? Pal.PaleBlue : Pal.Olive, 0.3f, 4121 + kind, 2, 0));
            g.Box(-3, 11, -1, 3, 11, 1, Pal.Ramp(Pal.Metal, 2));                                                           // lid rim
            g.Mat(Copper); g.Box(0, 4, -2, 0, 4, -2, Pal.Solid(Pal.Bronze[2]));                                            // cable gland
            g.Mat(Scrap);
            switch (kind)
            {
                case 0:
                    g.Box(-1, 6, 2, 1, 9, 2, Pal.Ramp(Pal.Black, 1));                                                     // lever slot
                    g.Box(0, 8, 3, 0, 10, 3, Pal.Ramp(Pal.Chrome, 2)); g.Set(0, 10, 4, Pal.Solid(Pal.Crimson[3]));        // lever, red knob
                    g.Set(2, 10, 2, Pal.Solid(Pal.LightY));
                    break;
                case 1:
                    g.Mat(Glass); g.Box(-2, 6, 2, 2, 10, 2, Pal.Solid(Pal.Cream[4]));                                      // clock face
                    g.Mat(Scrap); g.Box(0, 8, 3, 0, 10, 3, Pal.Solid(Pal.Black[0])); g.Box(0, 8, 3, 1, 8, 3, Pal.Solid(Pal.Black[0]));   // hands
                    foreach (var (x, y) in new[] { (0, 10), (2, 8), (0, 6), (-2, 8) }) g.Set(x, y, 3, Pal.Solid(Pal.Crimson[3]));        // pins
                    break;
                case 2:
                    g.Mat(Glass); g.Box(-1, 12, -1, 1, 13, 1, Pal.Ramp(Pal.Glass, 3)); g.Set(0, 14, 0, Pal.Solid(Pal.LightY));   // dome with the cell
                    g.Mat(Scrap); g.Set(2, 9, 2, Pal.Solid(Pal.Amber));
                    break;
                default:
                    g.CylX(1, 0, 1.4f, -6, 6, Pal.Ramp(Pal.Chrome, 1));                                                   // pipe stub
                    g.Box(-1, 6, 2, 1, 8, 2, Pal.Ramp(Pal.Black, 1)); g.Set(0, 9, 2, Pal.Solid(Pal.LightY));
                    break;
            }
            return g;
        }

        /// <summary>A breaker panel on a post: three big throws under a hazard stripe and a lamp for each tier.</summary>
        static VoxelGrid UtBreakerGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(0, 0, -1, 0, 8, -1, Pal.Ramp(Pal.Wood, 1, 4130));
            g.Mat(Iron);
            g.Box(-5, 6, -1, 5, 16, 1, Pal.Weathered(Pal.RigGreen, 0.35f, 4131, 3, 0));
            g.Box(-5, 17, -1, 5, 17, 1, Pal.Stripe(Pal.Solid(Pal.Ochre[3]), Pal.Solid(Pal.Black[1]), 0, 2));              // hazard stripe
            var lamps = new[] { Pal.TailR, Pal.LightY, Pal.Amber };
            for (int i = 0; i < 3; i++)
            {
                int x = -3 + i * 3;
                g.Box(x, 8, 2, x, 12, 2, Pal.Ramp(Pal.Black, 1));                                                        // throw slot
                g.Box(x, 11, 3, x, 13, 3, Pal.Ramp(Pal.Chrome, 2));                                                      // handle
                g.Mat(Glass); g.Set(x, 15, 2, Pal.Solid(lamps[i])); g.Mat(Iron);
            }
            g.Mat(Copper); g.Box(-5, 5, 0, -5, 6, 0, Pal.Solid(Pal.Bronze[2])); g.Box(5, 5, 0, 5, 6, 0, Pal.Solid(Pal.Bronze[2]));   // cable glands
            return g;
        }

        /// <summary>A gate valve in a pipe run: flanges, bonnet and a red hand wheel.</summary>
        static VoxelGrid UtValveGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-2, 0, -1, 2, 1, 1, Pal.Ramp(Pal.Metal, 0, 4140));                                                       // saddle
            g.CylX(3, 0, 1.4f, -7, 7, Pal.Weathered(Pal.Metal, 0.5f, 4141, 2, 0));                                         // pipe
            foreach (int x in new[] { -3, 3 }) g.CylX(3, 0, 2.2f, x, x, Pal.Ramp(Pal.Rust, 2, 4142));                     // flanges
            g.Box(-1, 4, -1, 1, 7, 1, Pal.Ramp(Pal.Metal, 2, 4143));                                                       // bonnet
            g.Box(0, 8, 0, 0, 9, 0, Pal.Ramp(Pal.Chrome, 1));                                                              // stem
            g.CylY(0, 0, 3f, 10, 10, Pal.Ramp(Pal.Crimson, 3, 4144), 2f);                                                   // hand wheel
            g.Box(-2, 10, 0, 2, 10, 0, Pal.Ramp(Pal.Crimson, 2)); g.Box(0, 10, -2, 0, 10, 2, Pal.Ramp(Pal.Crimson, 2));   // spokes
            return g;
        }

        /// <summary>A half-buried dome digester: rust-streaked green tank, a timber feed chute, the gas dome and its pipe,
        /// a pressure gauge and a stained outflow for the digestate.</summary>
        static VoxelGrid UtDigesterGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.CylY(0, 0, 9f, 0, 8, Pal.Weathered(Pal.RigGreen, 0.45f, 4150, 3, 3f));                                      // tank
            for (int y = 9; y <= 13; y++) g.CylY(0, 0, 9f - (y - 8) * 1.6f, y, y, Pal.Weathered(Pal.Moss, 0.3f, 4151, 2, 0));   // dome
            foreach (int y in new[] { 3, 7 }) g.CylY(0, 0, 9.2f, y, y, Pal.Ramp(Pal.Rust, 2, 4152), 8.5f);                 // hoops
            g.Mat(Wood);
            g.Tube(new Vector3(-12, 2, 0), new Vector3(-8, 9, 0), 1.6f, Pal.Ramp(Pal.Wood, 1, 4153));                     // feed chute
            g.Box(-14, 0, -2, -11, 3, 2, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 4154), Pal.Ramp(Pal.Wood, 1, 4155), 1, 2));      // feed box
            g.Mat((byte)ResourceType.Clay);
            g.Box(-13, 3, -1, -12, 3, 1, Pal.Ramp(Pal.Olive, 0, 4156));                                                    // muck in the box
            g.Mat(Iron);
            g.Box(0, 14, 0, 0, 16, 0, Pal.Ramp(Pal.Chrome, 1));                                                            // gas riser
            g.Box(0, 16, 0, 9, 16, 0, Pal.Stripe(Pal.Solid(Pal.Ochre[3]), Pal.Ramp(Pal.Metal, 2), 0, 3));                 // yellow gas line
            g.Box(9, 0, 0, 9, 15, 0, Pal.Stripe(Pal.Solid(Pal.Ochre[3]), Pal.Ramp(Pal.Metal, 2), 1, 3));
            g.Mat(Glass); g.Box(2, 14, 1, 3, 15, 1, Pal.Solid(Pal.Cream[4])); g.Set(3, 15, 2, Pal.Solid(Pal.Black[0]));   // gauge
            g.Mat(Iron);
            g.Box(8, 0, -3, 11, 1, -1, Pal.Ramp(Pal.Olive, 0, 4157));                                                      // digestate outflow
            g.CylX(2, -2, 1f, 8, 10, Pal.Ramp(Pal.Rust, 1));
            return g;
        }

        /// <summary>A gas engine set: skid, green engine block, yellow gas train and regulator, muffler and a bag of gas.</summary>
        static VoxelGrid UtGasGenGrid()
        {
            var g = new VoxelGrid().Mat(Iron);
            g.Box(-8, 0, -4, 8, 1, 4, Pal.Ramp(Pal.Metal, 0, 4160));
            g.Box(-7, 2, -3, 1, 8, 3, Pal.Weathered(Pal.Moss, 0.3f, 4161, 2, 0));                                         // engine
            g.Box(2, 2, -3, 6, 7, 3, Pal.Ramp(Pal.Metal, 2, 4162));                                                        // alternator
            g.Mat(Copper); g.Box(7, 4, 0, 7, 5, 1, Pal.Solid(Pal.Bronze[2]));
            g.Mat(Scrap);
            g.Box(-6, 9, -2, -2, 10, 2, Pal.Ramp(Pal.Rust, 2, 4163));                                                      // manifold
            g.Box(-1, 9, 2, -1, 14, 2, Pal.Ramp(Pal.Metal, 1)); g.Set(-1, 15, 2, Pal.Ramp(Pal.Rust, 1));  // muffler stack
            g.Box(-8, 4, -2, -8, 9, -2, Pal.Stripe(Pal.Solid(Pal.Ochre[3]), Pal.Ramp(Pal.Metal, 2), 1, 3));               // gas train
            g.Box(-9, 6, -3, -8, 7, -1, Pal.Ramp(Pal.Ochre, 2)); g.Set(-9, 8, -2, Pal.Solid(Pal.TailR));                  // regulator, shut-off
            g.Mat((byte)ResourceType.Rubber);
            for (int y = 2; y <= 6; y++) g.CylZ(y, 0, 2.6f - Mathf.Abs(y - 4) * 0.4f, -3, 3, Pal.Ramp(Pal.Black, 2, 4164)); // gas bag behind
            g.Mat(Glass); g.Set(1, 8, 3, Pal.Solid(Pal.Amber));
            return g;
        }

        /// <summary>A white chest freezer: lid seam, chrome handle, compressor grille and a thermostat dial.</summary>
        static VoxelGrid UtFreezerGrid()
        {
            var g = new VoxelGrid().Mat(Scrap);
            g.Box(-7, 0, -4, 7, 10, 4, Pal.Weathered(Pal.Cream, 0.15f, 4170, 3, 0));
            g.Box(-7, 8, 4, 7, 8, 4, Pal.Solid(Pal.Cream[0]));                                                              // lid seam
            g.Box(-7, 11, -4, 7, 11, 4, Pal.Ramp(Pal.Cream, 3, 4171));                                                     // lid
            g.Box(-2, 9, 5, 2, 9, 5, Pal.Ramp(Pal.Chrome, 2));                                                             // handle
            g.Box(3, 1, 4, 6, 4, 4, p => p.y % 2 == 0 ? Pal.Metal[0] : Pal.Void);                                           // compressor grille
            g.Set(-5, 4, 5, Pal.Solid(Pal.PaleBlue[2])); g.Set(-5, 5, 5, Pal.Solid(Pal.Chrome[1]));                        // dial
            g.Set(-3, 5, 5, Pal.Solid(Pal.LightY));                                                                         // power lamp
            return g;
        }

        /// <summary>A plank ice box with zinc-lined lid, iron corners and a drain tap.</summary>
        static VoxelGrid UtCellarGrid()
        {
            var g = new VoxelGrid().Mat(Stone);
            // earth bank, stepped in towards the sod top
            for (int y = 0; y <= 7; y++)
            {
                int hx = 11 - y, hz = 9 - y;
                if (hz < 2) break;
                g.Box(-hx, y, -hz - 2, hx, y, hz - 2, Pal.Ramp(Pal.Sand, y < 2 ? 0 : 1, 4190 + y));
            }
            g.Box(-5, 7, -5, 5, 7, 1, Pal.Ramp(Pal.Moss, 2, 4198));                                          // sod
            // stone face with the doorway, a slanted pair of plank doors
            g.Box(-7, 0, 7, 7, 6, 8, Pal.Ramp(Pal.Fur, 2, 4199));
            g.ClearBox(-4, 0, 7, 4, 4, 8);
            g.Mat(Wood);
            for (int y = 0; y <= 4; y++)
                g.Box(-4, y, 7 + (4 - y) / 2, 4, y, 7 + (4 - y) / 2, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 4200), Pal.Ramp(Pal.Wood, 1, 4201), 1, 2));
            g.Box(0, 0, 7, 0, 4, 9, Pal.Ramp(Pal.Wood, 0, 4202));                                                          // door split
            g.Mat(Scrap);
            g.Set(-1, 2, 9, Pal.Solid(Pal.Chrome[1])); g.Set(1, 2, 9, Pal.Solid(Pal.Chrome[1]));                           // ring pulls
            g.Box(-3, 7, -3, -3, 9, -3, Pal.Ramp(Pal.Metal, 1, 4203));                                                     // vent pipe
            return g;
        }

        static VoxelGrid UtIceBoxGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-6, 0, -4, 6, 8, 4, Pal.Stripe(Pal.Ramp(Pal.Wood, 3, 4180), Pal.Ramp(Pal.Wood, 1, 4181), 1, 3));
            g.Mat(Scrap);
            g.Box(-6, 9, -4, 6, 9, 4, Pal.Weathered(Pal.Metal, 0.3f, 4182, 2, 0));                                         // zinc lid
            foreach (int x in new[] { -6, 6 }) foreach (int z in new[] { -4, 4 }) g.Box(x, 0, z, x, 9, z, Pal.Ramp(Pal.Metal, 1, 4183));
            g.Box(-1, 7, 5, 1, 7, 5, Pal.Ramp(Pal.Chrome, 2));                                                             // latch
            g.Box(5, 1, 5, 5, 1, 6, Pal.Ramp(Pal.Chrome, 1)); g.Set(5, 0, 6, Pal.Solid(Pal.PaleBlue[3]));                 // drain tap, a drip
            return g;
        }
    }
}
