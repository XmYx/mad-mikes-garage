using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Real cars modelled in Blender (tools/blender/cars.py: lofted from real dimensions, voxelized at 0.08 m with
    /// material codes) and built here into full vehicles: one-voxel shell, interior and driver, door seams, cut doors and
    /// hood, glass, sockets on the standard layout. Codes: P paint, K canvas roof, G glass, H headlamp, A indicator,
    /// T tail lamp, B black plastic, C chrome, R grille, L plate. Files live in Models/Cars (read at bake time).</summary>
    public static partial class VehicleDesigns
    {
        class ModelSpec
        {
            public string file, name, key;
            public float mass = 1000f, travel = 0.22f, finalDrive = 4.1f, brake = 12000f, steer = 34f, fuel = 45f, frequency = 1.9f;
            public float[] gears;
            public VehicleDriver.Drive drive = VehicleDriver.Drive.Front;
            public string engine = "engine_i4", wheel = "wheel_compact", cargo;
            public Color32[] paint = Pal.Cream;
            public float rust = 0.2f;
            public bool airCooled;
            public int seed = 700;
        }

        static readonly string ModelDir = Path.Combine(Application.dataPath, "MadMax/Models/Cars");

        static VehicleDesign ModelCar(ModelSpec m)
        {
            var head = new Dictionary<string, string>();
            var vox = new Dictionary<Vector3Int, char>();
            foreach (var line in File.ReadAllLines(Path.Combine(ModelDir, m.file + ".txt")))
            {
                if (line.StartsWith("#")) { var kv = line.Substring(2).Split(new[] { ' ' }, 2); head[kv[0]] = kv.Length > 1 ? kv[1] : ""; continue; }
                var p = line.Split(' ');
                if (p.Length < 4) continue;
                vox[new Vector3Int(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]))] = p[3][0];
            }
            int H(string k) => int.Parse(head[k], CultureInfo.InvariantCulture);
            int W = H("halfW"), f = H("floor"), belt = H("belt"), roofY = H("roof"), zF = H("zFront"), zR = H("zRear");
            int windZ = H("windZ"), dashZ = windZ - 3, seatZ = dashZ - 5, dx = -Mathf.Min(4, W - 5);
            bool rearEngine = head["engine"] == "rear";
            var doors = new List<(int front, int rear)>();
            foreach (var d in head["doors"].Split(' ')) { var ab = d.Split(','); doors.Add((int.Parse(ab[0]), int.Parse(ab[1]))); }

            var design = new VehicleDesign
            {
                name = m.name, mass = m.mass, drive = m.drive, travel = m.travel, finalDrive = m.finalDrive, gears = m.gears, frequency = m.frequency,
                brakeForce = m.brake, maxSteer = m.steer, fuelL = m.fuel, oilL = 4f, coolantL = m.airCooled ? 0f : 6f, usesCoolant = !m.airCooled,
                eye = new Vector3Int(dx, f + 11, seatZ - 1), hitch = new Vector3Int(0, f + 2, zR - 1)
            };

            // ---- paint by code
            var g = new VoxelGrid();
            var paint = Pal.Weathered(m.paint, m.rust, m.seed, 2, 6);
            var glass = Pal.Ramp(Pal.Glass, 1, m.seed + 1);
            var plastic = Pal.Ramp(Pal.Black, 1, m.seed + 2);
            foreach (var kv in vox)
            {
                var p = kv.Key;
                g.Mat((byte)(kv.Value == 'G' || kv.Value == 'H' || kv.Value == 'T' || kv.Value == 'A' ? ResourceType.Glass : ResourceType.Scrap));
                VoxMat mat = kv.Value switch
                {
                    'G' => glass, 'K' => Pal.Ramp(Pal.Black, 2, m.seed + 3), 'H' => Pal.Solid(Pal.LightW), 'A' => Pal.Solid(Pal.Amber),
                    'T' => Pal.Solid(Pal.TailR), 'B' => plastic, 'C' => Pal.Solid(Pal.Chrome[2]), 'L' => Pal.Solid(Pal.Cream[3]),
                    'R' => Pal.Solid((p.x + p.y) % 2 == 0 ? Pal.Black[0] : Pal.Metal[1]),
                    _ => paint,
                };
                g.Set(p, mat);
            }
            g.Mat((byte)ResourceType.Scrap);

            // ---- one-voxel shell (keeps floor pan, bulkheads come from the interior)
            var solid = new List<Vector3Int>();
            foreach (var p in g.voxels.Keys)
                if (vox.ContainsKey(p + Vector3Int.right) && vox.ContainsKey(p + Vector3Int.left) && vox.ContainsKey(p + Vector3Int.up) &&
                    vox.ContainsKey(p + Vector3Int.down) && vox.ContainsKey(p + new Vector3Int(0, 0, 1)) && vox.ContainsKey(p + new Vector3Int(0, 0, -1)))
                    solid.Add(p);
            foreach (var p in solid) g.voxels.Remove(p);

            // widest voxel of each (y, z) row: where the side skin is
            var rowMax = new Dictionary<Vector2Int, int>();
            foreach (var p in vox.Keys) { var k = new Vector2Int(p.y, p.z); rowMax[k] = Mathf.Max(rowMax.TryGetValue(k, out int v) ? v : 0, Mathf.Abs(p.x)); }
            bool Skin(Vector3Int p) => rowMax.TryGetValue(new Vector2Int(p.y, p.z), out int mx) && Mathf.Abs(p.x) >= mx - 1;

            // ---- interior: seats, dash, wheel, driver; a rear bench for four doors
            Interior(g, f, dashZ, seatZ, dx);
            if (doors.Count > 1)
            {
                var leather = Pal.Ramp(Pal.Olive, 1, 91);
                int cw = W - 3, rz = seatZ - 10;
                g.Box(-cw, f + 1, rz - 4, cw, f + 3, rz, leather);
                g.Box(-cw, f + 4, rz - 5, cw, f + 10, rz - 5, leather);
            }

            // ---- door seams and handles, mirrors
            var seam = Pal.Solid(Pal.Black[0]);
            foreach (var (front, rear) in doors)
                foreach (var p in new List<Vector3Int>(g.voxels.Keys))
                {
                    if ((p.z == front || p.z == rear) && p.y > f && p.y < roofY - 1 && Skin(p) && g.voxels[p].label == 0 && !IsGlass(g.voxels[p])) g.Set(p, seam);
                    if (p.z == rear + 2 && p.y == belt - 2 && Skin(p)) g.Set(p, Pal.Solid(Pal.Chrome[2]));
                }
            foreach (int s in new[] { -1, 1 })
            {
                int mx = rowMax.TryGetValue(new Vector2Int(belt + 1, windZ - 1), out int v) ? v : W - 2;
                g.Box(s * (mx + 1), belt + 1, windZ - 1, s * (mx + 1), belt + 2, windZ - 1, Pal.Solid(Pal.Black[1]));
            }

            // ---- hood (front lid): the top skin between the windshield and the nose
            g.RelabelWhere((p, v) => v.label == 0 && p.z > windZ && p.z < zF - 1 && Mathf.Abs(p.x) <= W - 2 && !g.voxels.ContainsKey(p + Vector3Int.up) && p.y >= belt - 4, "hood");
            int hoodY = int.MaxValue;
            foreach (var kv in g.voxels) if (kv.Value.label == g.Label("hood")) hoodY = Mathf.Min(hoodY, kv.Key.y);
            if (hoodY == int.MaxValue) hoodY = belt - 3;
            g.label = 0;

            g.RelabelWhere((p, v) => IsGlass(v) && v.label == 0, "glass");
            g.Bevel();
            for (int i = 0; i < doors.Count; i++)
            {
                var (front, rear) = doors[i];
                string n = i == 0 ? "door" : "rdoor";
                g.Relabel(W - 3, f + 1, rear + 1, W + 1, roofY - 1, front, n + "_R", Skin);          // skin + side glass only:
                g.Relabel(-W - 1, f + 1, rear + 1, -(W - 3), roofY - 1, front, n + "_L", Skin);      // seats and driver stay
            }
            var (d0f, _) = doors[0];
            design.Cut(g, "door_R", m.key + "_door", PartCategory.Door, new Vector3Int(W - 3, f, d0f), m.mass * 0.03f);
            if (doors.Count > 1) design.Cut(g, "rdoor_R", m.key + "_door_rear", PartCategory.Door, new Vector3Int(W - 3, f, doors[1].front), m.mass * 0.025f);
            design.Cut(g, "hood", m.key + "_hood", PartCategory.Hood, new Vector3Int(0, hoodY, windZ + 1), m.mass * 0.015f);
            design.body = g.Extract("body", Vector3Int.zero);
            design.glass = g.Extract("glass", Vector3Int.zero);
            design.driver = g.Extract("driver", Vector3Int.zero);

            // ---- sockets (standard layout; bumpers are part of these bodies, the sockets stay as swap targets)
            design.Socket("wheel_front", PartCategory.Wheel, H("wheelX"), H("wheelY"), H("wheelZF"), m.wheel, true);
            design.Socket("wheel_rear", PartCategory.Wheel, H("wheelX"), H("wheelY"), H("wheelZR"), m.wheel, true);
            design.Socket("door", PartCategory.Door, W - 3, f, d0f, m.key + "_door", true);
            if (doors.Count > 1) design.Socket("door_rear", PartCategory.Door, W - 3, f, doors[1].front, m.key + "_door_rear", true);
            design.Socket("hood", PartCategory.Hood, 0, hoodY, windZ + 1, m.key + "_hood");
            design.Socket("engine", PartCategory.Engine, 0, f + 1, rearEngine ? zR + 7 : windZ + 4, m.engine);
            design.Socket("radiator", PartCategory.Radiator, 0, f + 2, zF - 3, m.airCooled ? null : "radiator_car");
            design.Socket("bumper_front", PartCategory.FrontBumper, 0, f + 1, zF + 1, null);
            design.Socket("bumper_rear", PartCategory.RearBumper, 0, f + 1, zR - 1, null);
            design.Socket("armor", PartCategory.Armor, W + 1, f + 2, seatZ - 2, null, true);
            design.Socket("cargo", PartCategory.Cargo, 0, roofY + 1, (H("roofFZ") + H("roofRZ")) / 2, m.cargo);
            design.Socket("roof", PartCategory.Weapon, 0, roofY + 1, H("roofFZ") - 4, null);
            return design;
        }

        // ================================================================== the cars (real dimensions in cars.py)
        public static VehicleDesign Fiat126p() => ModelCar(new ModelSpec
        {
            file = "fiat126p", name = "Fiat126p", key = "fiat126p", mass = 600f, drive = VehicleDriver.Drive.Rear, airCooled = true, fuel = 21f,
            gears = new[] { 3.25f, 2.07f, 1.36f, 0.97f }, finalDrive = 4.8f, brake = 8000f, travel = 0.2f, paint = Pal.Crimson, rust = 0.3f, seed = 701
        });
        public static VehicleDesign Renault5() => ModelCar(new ModelSpec
        {
            file = "renault5", name = "Renault5", key = "renault5", mass = 800f, fuel = 38f, gears = new[] { 3.8f, 2.2f, 1.4f, 1.0f },
            paint = Pal.Ochre, rust = 0.3f, seed = 702
        });
        public static VehicleDesign CitroenBX() => ModelCar(new ModelSpec
        {
            file = "citroen_bx", name = "CitroenBX", key = "citroen_bx", mass = 950f, fuel = 52f, travel = 0.26f, frequency = 1.4f,
            paint = Pal.Cream, rust = 0.25f, seed = 703
        });
        public static VehicleDesign CitroenXM() => ModelCar(new ModelSpec
        {
            file = "citroen_xm", name = "CitroenXM", key = "citroen_xm", mass = 1400f, fuel = 80f, travel = 0.26f, frequency = 1.3f,
            engine = "engine_i6", finalDrive = 3.9f, brake = 16000f, paint = Pal.Navy, rust = 0.2f, seed = 704
        });
        public static VehicleDesign CitroenXantia() => ModelCar(new ModelSpec
        {
            file = "citroen_xantia", name = "CitroenXantia", key = "citroen_xantia", mass = 1250f, fuel = 65f, travel = 0.26f, frequency = 1.35f,
            brake = 14000f, paint = Pal.RigGreen, rust = 0.2f, seed = 705
        });
        public static VehicleDesign LanciaYpsilon() => ModelCar(new ModelSpec
        {
            file = "lancia_ypsilon", name = "LanciaYpsilon", key = "lancia_ypsilon", mass = 1000f, fuel = 47f, paint = Pal.Bronze, rust = 0.15f, seed = 706
        });
        public static VehicleDesign Fiat500() => ModelCar(new ModelSpec
        {
            file = "fiat500", name = "Fiat500", key = "fiat500", mass = 500f, drive = VehicleDriver.Drive.Rear, airCooled = true, fuel = 21f,
            gears = new[] { 3.7f, 2.07f, 1.3f, 0.87f }, finalDrive = 5.1f, brake = 7000f, travel = 0.2f, paint = Pal.PaleBlue, rust = 0.3f, seed = 707
        });
        public static VehicleDesign Peugeot205() => ModelCar(new ModelSpec
        {
            file = "peugeot205", name = "Peugeot205", key = "peugeot205", mass = 850f, fuel = 50f, paint = Pal.Crimson, rust = 0.2f, seed = 708
        });
        public static VehicleDesign Peugeot206() => ModelCar(new ModelSpec
        {
            file = "peugeot206", name = "Peugeot206", key = "peugeot206", mass = 1000f, fuel = 50f, paint = Pal.Chrome, rust = 0.15f, seed = 709
        });
        public static VehicleDesign Peugeot207CC() => ModelCar(new ModelSpec
        {
            file = "peugeot207cc", name = "Peugeot207CC", key = "peugeot207cc", mass = 1400f, fuel = 50f, brake = 15000f, paint = Pal.Black, rust = 0.1f, seed = 710
        });
        public static VehicleDesign Peugeot405() => ModelCar(new ModelSpec
        {
            file = "peugeot405", name = "Peugeot405", key = "peugeot405", mass = 1100f, fuel = 70f, paint = Pal.Sand, rust = 0.25f, seed = 711
        });
        public static VehicleDesign Peugeot406Break() => ModelCar(new ModelSpec
        {
            file = "peugeot406break", name = "Peugeot406Break", key = "peugeot406break", mass = 1400f, fuel = 70f, brake = 15000f,
            engine = "engine_diesel_i6", cargo = "cargo_jerry_rack", paint = Pal.Moss, rust = 0.2f, seed = 712
        });
        public static VehicleDesign FiatMultipla() => ModelCar(new ModelSpec
        {
            file = "fiat_multipla", name = "FiatMultipla", key = "fiat_multipla", mass = 1300f, fuel = 63f, paint = Pal.Olive, rust = 0.2f, seed = 713
        });

        /// <summary>Every Blender-modelled car (builder list, fleet and wrecks).</summary>
        public static VehicleDesign[] ModelCars() => new[]
        {
            Fiat126p(), Renault5(), CitroenBX(), CitroenXM(), CitroenXantia(), LanciaYpsilon(), Fiat500(),
            Peugeot205(), Peugeot206(), Peugeot207CC(), Peugeot405(), Peugeot406Break(), FiatMultipla(),
        };

        /// <summary>Prefab names of <see cref="ModelCars"/> (the builder registers them with the game).</summary>
        public static readonly string[] ModelCarNames =
        {
            "Fiat126p", "Renault5", "CitroenBX", "CitroenXM", "CitroenXantia", "LanciaYpsilon", "Fiat500",
            "Peugeot205", "Peugeot206", "Peugeot207CC", "Peugeot405", "Peugeot406Break", "FiatMultipla",
        };
    }
}
