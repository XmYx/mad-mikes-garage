using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Generic, vehicle-agnostic parts. Each grid's origin is the part's mount point.
    /// Side parts are authored for the RIGHT side (+X outward); left sockets mirror them.</summary>
    public static partial class PartLibrary
    {
        public static IEnumerable<PartDesign> All()
        {
            yield return StreetWheel();
            yield return OffroadWheel();
            yield return V8Blower();
            yield return LastEngine();
            yield return DieselSix();
            yield return SidePipes();
            yield return ChromeBumper();
            yield return BullBar();
            yield return RearBumper();
            yield return RearCarrier();
            yield return RearSpoiler();
            yield return TwinFuelTanks();
            yield return JerryRack();
            yield return Turret();
            yield return SmallWheel();
            yield return CompactWheel();
            yield return TruckWheel();
            yield return TwoStroke();
            yield return TruckDiesel();
            yield return ExhaustStack();
            yield return Plow();
            yield return TrabantBumper(true);
            yield return TrabantBumper(false);
            yield return Radiator(false);
            yield return Radiator(true);
            yield return Ram();
            yield return SpikedBumper(true);
            yield return SpikedBumper(false);
            yield return SideSpikes();
            yield return SidePlate();
            yield return ExcavatorArm();
            yield return LoaderBucket();
            yield return DozerBlade();
            yield return DumpBed();
            yield return PaverScreed();
            yield return WinchBumper();
            yield return CraneArm();
            yield return PetrolFour();
            yield return PetrolSix();
            yield return MudWheel();
            yield return RainWheel();
            yield return SportWheel();
            yield return TrackWheel();
            yield return HoeArm();
            yield return MonsterWheel();
            foreach (var p in Attachments()) yield return p;
            foreach (var p in Bikes()) yield return p;
            foreach (var p in Aircraft()) yield return p;
            foreach (var p in Marine()) yield return p;
            foreach (var p in Farm()) yield return p;
        }

        static PartDesign Make(string key, PartCategory c, VoxelGrid g, float mass, int size = 1, float radius = 0)
        {
            g.Bevel();
            return new PartDesign { key = key, category = c, grid = g, mass = mass, sizeClass = size, radius = radius };
        }

        static float Angle01(float y, float z) => (Mathf.Atan2(y, z) / (2 * Mathf.PI) + 1f) % 1f;

        // ---------------------------------------------------------------- wheels
        public static PartDesign StreetWheel()
        {
            var g = new VoxelGrid();
            const float R = 5.4f, rim = 3.3f;
            for (int x = 0; x <= 3; x++)
            for (int y = -6; y <= 6; y++)
            for (int z = -6; z <= 6; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                var p = new Vector3Int(x, y, z);
                Color32 c;
                if (d > 4.5f)
                {
                    int seg = Mathf.FloorToInt(Angle01(y, z) * 28);
                    c = x == 3 ? Pal.Tire[1] : (seg % 2 == 0 ? Pal.Tire[2] : Pal.Tire[0]);
                }
                else if (d > rim) c = x == 3 ? Pal.Tire[1] : Pal.Tire[0];
                else
                {
                    if (x == 3) continue; // recessed dish
                    if (x < 2) c = Pal.Metal[0];
                    else if (d < 0.7f) c = Pal.Void;
                    else if (d < 1.5f) c = Pal.Bronze[1];
                    else if (d < 2.5f)
                    {
                        int spoke = Mathf.FloorToInt(Angle01(y, z) * 10);
                        c = spoke % 2 == 0 ? Pal.Bronze[2] : Pal.Bronze[0];
                    }
                    else c = Pal.Hash(p, 4) > 0.8f ? Pal.Rust[3] : Pal.Bronze[3];
                }
                g.Set(p, Pal.Solid(c));
            }
            // lug nuts
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2;
                g.Set(2, Mathf.RoundToInt(Mathf.Sin(a) * 1.4f), Mathf.RoundToInt(Mathf.Cos(a) * 1.4f), Pal.Solid(Pal.Chrome[2]));
            }
            var part = Make("wheel_street", PartCategory.Wheel, g, 22, 1, R * VoxelMesher.DefaultSize);
            part.grip = 1.15f; part.mudGrip = 0.42f; part.width = 0.32f; part.wetGrip = 0.78f;
            return part;
        }

        /// <summary>Generic tyre builder: rim radius, tyre radius, width in voxels; tread(x, angleSeg, depth) returns
        /// false to cut a groove. Rim painted by <paramref name="rimPaint"/>(x, d).</summary>
        static VoxelGrid Tyre(float R, float rim, int wx, System.Func<int, float, float, bool> tread, System.Func<int, float, Vector3Int, Color32> rimPaint, Color32 side)
        {
            var g = new VoxelGrid();
            int n = Mathf.CeilToInt(R);
            for (int x = 0; x <= wx; x++)
            for (int y = -n; y <= n; y++)
            for (int z = -n; z <= n; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                var p = new Vector3Int(x, y, z);
                if (d > rim)
                {
                    if (d > R - 1.1f && !tread(x, Angle01(y, z), d)) continue;
                    g.Set(p, Pal.Solid(x == wx ? side : d > R - 1.1f ? Pal.Tire[2] : Pal.Tire[x == 0 ? 1 : 0]));
                }
                else
                {
                    if (x == wx) continue;                                               // recessed rim face
                    g.Set(p, Pal.Solid(x < wx - 1 ? Pal.Metal[0] : rimPaint(x, d, p)));
                }
            }
            return g;
        }

        /// <summary>Mud-terrain tyre: tall paddle lugs, wide. Digs through slop, hums and wanders on tarmac.</summary>
        public static PartDesign MudWheel()
        {
            var g = Tyre(6.8f, 3.7f, 5, (x, a, d) => ((int)(a * 14) + (x > 2 ? 1 : 0)) % 2 == 0 || d < 6.2f,
                (x, d, p) => d < 1f ? Pal.Chrome[2] : d > 2.9f ? Pal.Metal[2] : Pal.Pick(Pal.Olive, p, 150, 1), Pal.Tire[1]);
            var part = Make("wheel_mud", PartCategory.Wheel, g, 38, 2, 6.8f * VoxelMesher.DefaultSize);
            part.grip = 0.92f; part.mudGrip = 1.0f; part.width = 0.44f; part.wetGrip = 0.72f; part.rolling = 1.35f; part.wearRate = 1.2f;
            return part;
        }

        /// <summary>Rain tyre: siped tread with deep circumferential grooves; keeps its grip on wet tarmac.</summary>
        public static PartDesign RainWheel()
        {
            var g = Tyre(5.4f, 3.4f, 3, (x, a, d) => x != 1 && ((int)(a * 40) % 5 != 0 || x == 3),
                (x, d, p) => d < 0.8f ? Pal.Chrome[3] : d < 2.6f ? ((int)(Angle01(p.y, p.z) * 12) % 2 == 0 ? Pal.Chrome[1] : Pal.Metal[1]) : Pal.Chrome[2], Pal.PaleBlue[0]);
            var part = Make("wheel_rain", PartCategory.Wheel, g, 22, 1, 5.4f * VoxelMesher.DefaultSize);
            part.grip = 1.1f; part.mudGrip = 0.5f; part.width = 0.3f; part.wetGrip = 1.0f;
            return part;
        }

        /// <summary>Sport tyre: low profile on a big spoked rim, nearly slick. Sticks on dry tarmac, useless in mud, wears fast.</summary>
        public static PartDesign SportWheel()
        {
            var g = Tyre(5.6f, 4.3f, 4, (x, a, d) => !(x == 2 && (int)(a * 60) % 6 == 0),
                (x, d, p) => d < 0.9f ? Pal.Chrome[3] : d > 3.6f ? Pal.Chrome[2] : ((int)(Angle01(p.y, p.z) * 10) % 2 == 0 ? Pal.Hex("b02818") : Pal.Metal[0]), Pal.Tire[1]);
            var part = Make("wheel_sport", PartCategory.Wheel, g, 20, 1, 5.6f * VoxelMesher.DefaultSize);
            part.grip = 1.38f; part.mudGrip = 0.3f; part.width = 0.36f; part.wetGrip = 0.6f; part.rolling = 0.9f; part.wearRate = 1.9f;
            return part;
        }

        /// <summary>Crawler track roller: hidden inside a machine's track. Wide and long footprint: barely sinks; the grousers bite
        /// harder in mud than rubber does on dry tarmac.</summary>
        public static PartDesign TrackWheel()
        {
            var g = new VoxelGrid();
            g.CylX(0, 0, 4.3f, 0, 5, p => (p.y + p.z) % 3 == 0 ? Pal.Metal[2] : Pal.Metal[0]);
            var part = Make("wheel_track", PartCategory.Wheel, g, 180, 3, 4.3f * VoxelMesher.DefaultSize);
            part.grip = 1.1f; part.mudGrip = 1.6f; part.width = 0.48f; part.wetGrip = 0.95f; part.rolling = 1.7f; part.wearRate = 0.2f; part.footprint = 7f;
            return part;
        }

        public static PartDesign OffroadWheel()
        {
            var g = new VoxelGrid();
            const float R = 6.5f, rim = 3.6f;
            for (int x = 0; x <= 4; x++)
            for (int y = -7; y <= 7; y++)
            for (int z = -7; z <= 7; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                var p = new Vector3Int(x, y, z);
                int seg = Mathf.FloorToInt(Angle01(y, z) * 18);
                if (d > 5.6f && (seg + (x >= 2 ? 1 : 0)) % 2 == 0) continue; // staggered lugs
                Color32 c;
                if (d > 5.6f) c = Pal.Tire[x == 4 || x == 0 ? 1 : 2];
                else if (d > rim) c = x == 4 ? (d > 5f ? Pal.Tire[2] : Pal.Tire[1]) : Pal.Tire[0];
                else
                {
                    if (x == 4) continue;
                    if (x < 3) c = Pal.Metal[0];
                    else if (d < 0.8f) c = Pal.Metal[3];
                    else if (d < 1.8f) c = Pal.Metal[1];
                    else if (d > 3.0f) c = Pal.Hash(p, 9) > 0.5f ? Pal.Rust[2] : Pal.Metal[2]; // bead-lock ring
                    else c = Pal.Pick(Pal.Rust, p, 12, 2);
                }
                g.Set(p, Pal.Solid(c));
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2;
                g.Set(4, Mathf.RoundToInt(Mathf.Sin(a) * 3.2f), Mathf.RoundToInt(Mathf.Cos(a) * 3.2f), Pal.Solid(Pal.Chrome[1]));
            }
            var part = Make("wheel_offroad", PartCategory.Wheel, g, 34, 2, R * VoxelMesher.DefaultSize);
            part.grip = 1.0f; part.mudGrip = 0.85f; part.width = 0.4f; part.wetGrip = 0.82f; part.rolling = 1.12f;
            return part;
        }

        /// <summary>Trabant-style steel wheel: narrow tyre, cream rim, chrome hubcap.</summary>
        public static PartDesign SmallWheel()
        {
            var g = new VoxelGrid();
            const float R = 4.3f, rim = 3.1f;
            for (int x = 0; x <= 2; x++)
            for (int y = -5; y <= 5; y++)
            for (int z = -5; z <= 5; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                Color32 c;
                if (d > rim) c = x == 2 ? Pal.Tire[1] : Pal.Tire[Mathf.FloorToInt(Angle01(y, z) * 20) % 2 == 0 ? 2 : 0];
                else
                {
                    if (x == 2) continue;
                    if (x == 0) c = Pal.Metal[0];
                    else if (d < 1.3f) c = d < 0.6f ? Pal.Chrome[3] : Pal.Chrome[2];
                    else c = d > 2.4f ? Pal.Cream[2] : Pal.Cream[3];
                }
                g.Set(x, y, z, Pal.Solid(c));
            }
            var part = Make("wheel_small", PartCategory.Wheel, g, 12, 1, R * VoxelMesher.DefaultSize);
            part.grip = 1.0f; part.mudGrip = 0.35f; part.width = 0.2f; part.wetGrip = 0.8f;
            return part;
        }

/// <summary>Everyday hatchback / saloon wheel (0.31 m, 175-195 section): steel rim, plastic trim, four studs.</summary>
        public static PartDesign CompactWheel()
        {
            var g = new VoxelGrid();
            const float R = 3.9f, rim = 2.8f;
            for (int x = 0; x <= 2; x++)
            for (int y = -4; y <= 4; y++)
            for (int z = -4; z <= 4; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                Color32 c;
                if (d > rim) c = x == 2 ? Pal.Tire[1] : Pal.Tire[Mathf.FloorToInt(Angle01(y, z) * 18) % 2 == 0 ? 2 : 0];
                else
                {
                    if (x == 2) continue;
                    if (x == 0) c = Pal.Metal[0];
                    else if (d < 1.1f) c = Pal.Black[2];
                    else c = Mathf.FloorToInt(Angle01(y, z) * 8) % 2 == 0 ? Pal.Chrome[1] : Pal.Chrome[2];
                }
                g.Set(x, y, z, Pal.Solid(c));
            }
            var part = Make("wheel_compact", PartCategory.Wheel, g, 14, 1, R * VoxelMesher.DefaultSize);
            part.grip = 1.05f; part.mudGrip = 0.36f; part.width = 0.19f; part.wetGrip = 0.8f;
            return part;
        }

        public static PartDesign TruckWheel()
        {
            var g = new VoxelGrid();
            const float R = 6.7f, rim = 4.0f;
            for (int x = 0; x <= 4; x++)
            for (int y = -7; y <= 7; y++)
            for (int z = -7; z <= 7; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                var p = new Vector3Int(x, y, z);
                int seg = Mathf.FloorToInt(Angle01(y, z) * 24);
                if (d > 6f && seg % 3 == 0 && x != 2) continue;           // block tread
                Color32 c;
                if (d > rim) c = x == 4 || x == 0 ? Pal.Tire[1] : Pal.Tire[d > 6f ? 2 : 0];
                else
                {
                    if (x == 4) continue;
                    if (x < 3) c = Pal.Metal[0];
                    else if (d < 1.2f) c = Pal.Metal[3];
                    else if (d < 2.2f) c = Pal.Pick(Pal.Rust, p, 140, 3);
                    else c = d > 3.3f ? Pal.Metal[2] : Pal.Pick(Pal.Rust, p, 141, 2);
                }
                g.Set(p, Pal.Solid(c));
            }
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2;
                g.Set(3, Mathf.RoundToInt(Mathf.Sin(a) * 2.7f), Mathf.RoundToInt(Mathf.Cos(a) * 2.7f), Pal.Solid(Pal.Chrome[2]));
            }
            var part = Make("wheel_truck", PartCategory.Wheel, g, 90, 3, R * VoxelMesher.DefaultSize);
            part.grip = 1.05f; part.mudGrip = 0.7f; part.width = 0.42f;
            return part;
        }

        /// <summary>Monster truck tyre (roadmap 19): 1.7 m tall, deep chevron lugs, wide and soft — floats over mud and
        /// crushes cars, slow to turn and heavy to spin up.</summary>
        public static PartDesign MonsterWheel()
        {
            var g = new VoxelGrid();
            const float R = 10.6f, rim = 5.2f;
            for (int x = 0; x <= 8; x++)
            for (int y = -11; y <= 11; y++)
            for (int z = -11; z <= 11; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                var p = new Vector3Int(x, y, z);
                int seg = Mathf.FloorToInt(Angle01(y, z) * 30);
                bool lug = ((seg + (x < 4 ? 0 : 1)) % 3) == 0;                      // staggered chevrons
                if (d > R - 1.2f && !lug) continue;
                Color32 c;
                if (d > rim) c = x == 8 || x == 0 ? Pal.Tire[1] : Pal.Tire[d > R - 1.5f ? 2 : d > rim + 2f ? 1 : 0];
                else
                {
                    if (x > 6) continue;
                    if (x < 4) c = Pal.Metal[0];
                    else if (d < 1.4f) c = Pal.Chrome[3];
                    else if (d < 2.6f) c = Pal.Chrome[1];
                    else c = (Mathf.FloorToInt(Angle01(y, z) * 8) % 2 == 0) ? Pal.Chrome[2] : Pal.Pick(Pal.Metal, p, 1911, 2);   // beadlock spokes
                }
                g.Set(p, Pal.Solid(c));
            }
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2;
                g.Set(6, Mathf.RoundToInt(Mathf.Sin(a) * 4.6f), Mathf.RoundToInt(Mathf.Cos(a) * 4.6f), Pal.Solid(Pal.Chrome[3]));   // bead ring bolts
            }
            var part = Make("wheel_monster", PartCategory.Wheel, g, 190, 4, R * VoxelMesher.DefaultSize);
            part.grip = 0.95f; part.mudGrip = 0.95f; part.width = 0.72f; part.wetGrip = 0.75f; part.rolling = 1.35f; part.wearRate = 0.6f;
            return part;
        }

        // ---------------------------------------------------------------- engines
        /// <summary>Radiator core in front of the engine: coolant leaks from its damage; without one the engine cooks.</summary>
        public static PartDesign Radiator(bool truck)
        {
            var g = new VoxelGrid();
            int hx = truck ? 8 : 7, h = truck ? 9 : 5;
            g.Box(-hx, 0, 0, hx, h, 0, Pal.Stripe(Pal.Solid(Pal.Metal[0]), Pal.Solid(Pal.Metal[2]), 1, 2));    // fins
            g.Box(-hx, h + 1, 0, hx, h + 1, 0, Pal.Weathered(Pal.Chrome, 0.4f, 171, 1, 0));                    // header tank
            g.Box(-hx - 1, 0, 0, -hx - 1, h + 1, 0, Pal.Ramp(Pal.Metal, 1)); g.Box(hx + 1, 0, 0, hx + 1, h + 1, 0, Pal.Ramp(Pal.Metal, 1));
            g.Set(-2, h + 2, 0, Pal.Solid(Pal.Chrome[2]));                                                      // cap
            g.Box(-3, h, -1, -3, h, -2, Pal.Ramp(Pal.Black, 1)); g.Box(3, 1, -1, 3, 1, -2, Pal.Ramp(Pal.Black, 1)); // hoses
            return Make(truck ? "radiator_truck" : "radiator_car", PartCategory.Radiator, g, truck ? 40 : 14, truck ? 3 : 1);
        }

        public static PartDesign TwoStroke()
        {
            var g = new VoxelGrid();
            g.Box(-4, 0, 0, 4, 4, 7, Pal.Ramp(Pal.Metal, 1, 150));
            g.Box(-3, 5, 1, 3, 6, 6, Pal.Stripe(Pal.Solid(Pal.Metal[2]), Pal.Solid(Pal.Metal[0]), 2, 2)); // cooling fins
            g.CylY(2, 4, 1.4f, 7, 7, Pal.Ramp(Pal.Black, 1));                                           // air filter
            g.Tube(new Vector3(-4, 1, 3), new Vector3(-6, 0, -2), 0.5f, Pal.Ramp(Pal.Rust, 2));          // exhaust
            var d = Make("engine_2stroke", PartCategory.Engine, g, 60, 1);
            d.torque = 90f; d.maxRpm = 4800f; d.peakAt = 0.62f;
            return d;
        }

        public static PartDesign TruckDiesel()
        {
            var g = new VoxelGrid();
            var block = Pal.Weathered(Pal.RigGreen, 0.35f, 160, 2, 3);
            g.Box(-7, 0, 0, 7, 8, 15, block);
            g.Box(-8, 6, 1, -6, 9, 14, Pal.Ramp(Pal.Metal, 2)); g.Box(6, 6, 1, 8, 9, 14, Pal.Ramp(Pal.Metal, 2)); // heads
            g.CylX(8, 12, 2.2f, -3, 3, Pal.Weathered(Pal.Chrome, 0.2f, 161, 2, 0));                     // turbo
            g.CylZ(0, 5, 3.5f, 16, 16, Pal.Ramp(Pal.Black, 1));                                             // fan
            var d = Make("engine_truck_diesel", PartCategory.Engine, g, 900, 3);
            d.torque = 2600f; d.maxRpm = 2600f; d.peakAt = 0.5f;
            return d;
        }

        // ---------------------------------------------------------------- engines (legacy)
        /// <summary>Supercharged V8. Origin = bottom/front-of-bay. Blower pokes 6 voxels above a y+6 hood line.</summary>
        public static PartDesign V8Blower()
        {
            var g = new VoxelGrid();
            var metal = Pal.Ramp(Pal.Metal, 1, 2);
            var chrome = Pal.Weathered(Pal.Chrome, 0.15f, 7, 1, -10);
            g.Box(-5, 0, 0, 5, 4, 10, metal);                          // block
            g.Box(-6, 3, 1, -4, 5, 9, chrome); g.Box(4, 3, 1, 6, 5, 9, chrome);   // valve covers
            for (int z = 2; z <= 8; z += 2) { g.Set(-7, 2, z, Pal.Ramp(Pal.Rust, 2)); g.Set(7, 2, z, Pal.Ramp(Pal.Rust, 2)); } // headers
            g.CylZ(0, 6, 1.6f, 10, 11, Pal.Solid(Pal.Black[1]));      // pulley
            g.Box(-3, 5, 2, 3, 5, 8, metal);                           // manifold
            g.Box(-3, 6, 2, 3, 9, 8, Pal.Stripe(Pal.Solid(Pal.Chrome[2]), Pal.Solid(Pal.Chrome[0]), 2, 2)); // blower case
            g.Box(-3, 10, 3, 3, 12, 7, Pal.Ramp(Pal.Black, 1, 3));    // scoop
            g.Box(-2, 12, 4, -1, 12, 6, Pal.Solid(Pal.Void));          // butterflies
            g.Box(1, 12, 4, 2, 12, 6, Pal.Solid(Pal.Void));
            g.Box(-3, 10, 8, 3, 10, 8, Pal.Solid(Pal.Chrome[3]));
            var d = Make("engine_v8_blower", PartCategory.Engine, g, 260, 2);
            d.torque = 680f; d.maxRpm = 7200f; d.peakAt = 0.62f;
            return d;
        }

        /// <summary>The Last Engine: a supercharged V12 of legend (endgame; assembled at a garage from four relics, see
        /// <c>Game.LastEngine</c>). Same bay footprint as the blown V8, a little longer, twin blowers.</summary>
        public static PartDesign LastEngine()
        {
            var g = new VoxelGrid();
            var metal = Pal.Weathered(Pal.Metal, 0.08f, 12, 1, 2);
            var chrome = Pal.Weathered(Pal.Chrome, 0.05f, 13, 2, -6);
            g.Box(-5, 0, 0, 5, 4, 12, metal);                                                   // long block
            g.Box(-6, 3, 1, -4, 5, 11, Pal.Solid(Pal.TailR)); g.Box(4, 3, 1, 6, 5, 11, Pal.Solid(Pal.TailR));   // red cam covers
            for (int z = 1; z <= 11; z += 2) { g.Set(-7, 2, z, chrome); g.Set(7, 2, z, chrome); }                 // twelve header stubs
            g.CylZ(0, 6, 1.6f, 12, 13, Pal.Solid(Pal.Black[1]));                               // pulley
            g.Box(-3, 5, 1, 3, 5, 11, metal);                                                   // manifold
            for (int b = 0; b < 2; b++)
            {
                int z0 = 1 + b * 6;
                g.Box(-3, 6, z0, 3, 9, z0 + 4, Pal.Stripe(Pal.Solid(Pal.Chrome[3]), Pal.Solid(Pal.Chrome[1]), 2, 2));   // twin blowers
                g.Box(-2, 10, z0 + 1, 2, 11, z0 + 3, Pal.Ramp(Pal.Black, 1, 3));               // scoops
                g.Box(-1, 11, z0 + 2, 1, 11, z0 + 2, Pal.Solid(Pal.Void));
            }
            var d = Make("engine_v12_last", PartCategory.Engine, g, 380, 2);
            d.torque = 1050f; d.maxRpm = 7800f; d.peakAt = 0.6f;
            return d;
        }

        /// <summary>Everyday petrol inline-four (coupes, saloons, wagons).</summary>
        public static PartDesign PetrolFour()
        {
            var g = new VoxelGrid();
            var block = Pal.Weathered(Pal.Metal, 0.3f, 41, 1, 3);
            g.Box(-3, 0, 0, 3, 3, 7, block);
            g.Box(-2, 4, 1, 2, 4, 6, Pal.Solid(Pal.TailR));                                    // red cam cover
            for (int z = 1; z <= 6; z += 2) g.Set(-4, 2, z, Pal.Ramp(Pal.Rust, 1));           // exhaust ports
            g.CylY(2, 2, 1.6f, 5, 5, Pal.Ramp(Pal.Black, 1, 42));                             // air cleaner (fits under a low hood)
            var d = Make("engine_i4", PartCategory.Engine, g, 140, 1);
            d.torque = 190f; d.maxRpm = 6200f; d.peakAt = 0.62f;
            return d;
        }

        /// <summary>Torquey petrol inline-six (pickups, tow trucks).</summary>
        public static PartDesign PetrolSix()
        {
            var g = new VoxelGrid();
            var block = Pal.Weathered(Pal.RigGreen, 0.35f, 43, 1, 3);
            g.Box(-3, 0, 0, 3, 5, 10, block);
            g.Box(-2, 6, 1, 2, 6, 9, Pal.Ramp(Pal.Chrome, 1));                                  // valve cover
            for (int z = 1; z <= 9; z += 2) g.Set(-4, 3, z, Pal.Ramp(Pal.Rust, 1));
            g.CylY(2, 3, 2f, 7, 7, Pal.Ramp(Pal.Black, 1, 44));
            var d = Make("engine_i6", PartCategory.Engine, g, 220, 2);
            d.torque = 400f; d.maxRpm = 5200f; d.peakAt = 0.5f;
            return d;
        }

        public static PartDesign DieselSix()
        {
            var g = new VoxelGrid();
            var block = Pal.Weathered(Pal.Olive, 0.4f, 17, 1, 3);
            g.Box(-4, 0, 0, 4, 5, 11, block);
            g.Box(-3, 6, 1, 3, 6, 10, Pal.Ramp(Pal.Metal, 2));
            for (int z = 2; z <= 9; z += 2) g.Set(-5, 3, z, Pal.Ramp(Pal.Rust, 1));
            g.CylY(3, 3, 2.2f, 6, 7, Pal.Ramp(Pal.Metal, 1, 5));       // air filter
            g.Set(3, 8, 3, Pal.Solid(Pal.Chrome[2]));
            var d = Make("engine_diesel_i6", PartCategory.Engine, g, 310, 2);
            d.torque = 820f; d.maxRpm = 4400f; d.peakAt = 0.45f;
            return d;
        }

        // ---------------------------------------------------------------- exhaust
        /// <summary>Bundle of four side pipes running rearwards along the sill (right side).</summary>
        public static PartDesign SidePipes()
        {
            var g = new VoxelGrid();
            var pipe = Pal.Weathered(Pal.Chrome, 0.35f, 23, 1, -10);
            g.Box(0, 0, -15, 1, 1, 0, pipe);
            g.Box(-1, 0, 0, -1, 1, 1, Pal.Ramp(Pal.Rust, 1));        // headers into body
            g.Box(0, 0, -15, 1, 1, -15, Pal.Solid(Pal.Void));          // outlets
            g.Box(0, 0, -14, 1, 1, -14, Pal.Solid(Pal.Chrome[3]));
            g.Box(0, -1, -9, 1, -1, -8, Pal.Ramp(Pal.Metal, 0));       // hanger
            return Make("exhaust_side_pipes", PartCategory.Exhaust, g, 18);
        }

        /// <summary>Vertical exhaust stack with a perforated heat shield (right side; mirrors left).</summary>
        public static PartDesign ExhaustStack()
        {
            var g = new VoxelGrid();
            var pipe = Pal.Weathered(Pal.Chrome, 0.35f, 170, 2, -10);
            g.CylY(0, 0, 1.2f, 0, 30, pipe);
            g.CylY(0, 0, 1.6f, 8, 20, p => (p.y + p.z) % 2 == 0 ? Pal.Chrome[1] : Pal.Void);             // heat shield
            g.Tube(new Vector3(0, 30, 0), new Vector3(0, 33, -2), 1.1f, pipe);                             // curved tip
            g.Set(0, 33, -3, Pal.Solid(Pal.Void));
            g.Box(-2, 0, -1, -1, 1, 1, Pal.Ramp(Pal.Metal, 0));                                            // bracket
            return Make("exhaust_stack", PartCategory.Exhaust, g, 25, 2);
        }

        /// <summary>Wedge cow-catcher plow for heavy trucks.</summary>
        public static PartDesign Plow()
        {
            var g = new VoxelGrid();
            var steel = Pal.Weathered(Pal.Rust, 0.2f, 180, 2, 20);
            for (int y = 0; y <= 13; y++)
            for (int x = -15; x <= 15; x++)
            {
                int z = 6 - Mathf.Abs(x) / 3 - y / 4;                    // V in plan, raked back going up
                if (z < 0) z = 0;
                g.Set(x, y, z, (x + y) % 4 == 0 ? Pal.Solid(Pal.Rust[4]) : steel);
                if (y % 4 == 0) g.Set(x, y, z + 1, Pal.Solid(Pal.Metal[1]));
            }
            foreach (int x in new[] { -9, 0, 9 }) g.Box(x, 6, -1, x, 7, 0, Pal.Ramp(Pal.Metal, 1));      // mounts
            return Make("bumper_plow", PartCategory.FrontBumper, g, 220, 3);
        }

        public static PartDesign TrabantBumper(bool front)
        {
            var g = new VoxelGrid();
            var c = Pal.Weathered(Pal.Cream, 0.12f, front ? 190 : 191, 3, -10);
            int s = front ? 1 : -1;
            g.Box(-8, 0, 0, 8, 1, 0, c);
            g.Box(-8, 0, -s, -8, 1, -s, c); g.Box(8, 0, -s, 8, 1, -s, c);
            g.Box(-8, 1, 0, 8, 1, 0, Pal.Solid(Pal.Chrome[2]));
            return Make(front ? "bumper_trabant_front" : "bumper_trabant_rear", front ? PartCategory.FrontBumper : PartCategory.RearBumper, g, 8);
        }

        // ---------------------------------------------------------------- bumpers
        public static PartDesign ChromeBumper()
        {
            var g = new VoxelGrid();
            var chrome = Pal.Weathered(Pal.Chrome, 0.3f, 31, 2, -10);
            g.Box(-12, 0, 0, 12, 1, 1, chrome);
            g.Box(-12, 0, -1, -12, 1, -1, chrome); g.Box(12, 0, -1, 12, 1, -1, chrome);
            g.Box(-6, 0, 2, -5, 1, 2, Pal.Solid(Pal.Black[1])); g.Box(5, 0, 2, 6, 1, 2, Pal.Solid(Pal.Black[1]));
            return Make("bumper_chrome", PartCategory.FrontBumper, g, 20);
        }

        public static PartDesign BullBar()
        {
            var g = new VoxelGrid();
            var steel = Pal.Weathered(Pal.Metal, 0.45f, 41, 2, 20);
            g.Box(-12, 0, 0, 12, 2, 1, steel);                                    // base beam
            g.Tube(new Vector3(-11, 2, 2), new Vector3(11, 2, 2), 0.5f, steel);
            g.Tube(new Vector3(-10, 9, 2), new Vector3(10, 9, 2), 0.5f, steel);   // top hoop
            foreach (int x in new[] { -10, -5, 5, 10 }) g.Tube(new Vector3(x, 2, 2), new Vector3(x, 9, 2), 0.5f, steel);
            g.Tube(new Vector3(-10, 9, 2), new Vector3(-10, 9, 0), 0.5f, steel);
            g.Tube(new Vector3(10, 9, 2), new Vector3(10, 9, 0), 0.5f, steel);
            g.Box(-3, 1, 1, 3, 3, 2, Pal.Ramp(Pal.Black, 1));                    // winch
            g.Box(-2, 2, 3, 2, 2, 3, Pal.Solid(Pal.Rust[4]));                    // hook
            return Make("bumper_bull_bar", PartCategory.FrontBumper, g, 60, 2);
        }

        public static PartDesign RearBumper()
        {
            var g = new VoxelGrid();
            var chrome = Pal.Weathered(Pal.Chrome, 0.4f, 37, 2, -10);
            g.Box(-12, 0, -1, 12, 1, 0, chrome);
            g.Box(-12, 0, 1, -12, 1, 1, chrome); g.Box(12, 0, 1, 12, 1, 1, chrome);
            return Make("bumper_rear_chrome", PartCategory.RearBumper, g, 16);
        }

        public static PartDesign RearCarrier()
        {
            var g = new VoxelGrid();
            var steel = Pal.Weathered(Pal.Metal, 0.5f, 43, 2, 20);
            g.Box(-12, 0, -1, 12, 2, 0, steel);
            g.Box(-1, 3, -1, 1, 13, -1, steel);                                     // carrier post
            for (int y = 3; y <= 15; y++)
            for (int x = -6; x <= 6; x++)
            {
                float d = Mathf.Sqrt(x * x + (y - 9) * (y - 9));
                if (d > 5.4f) continue;
                Color32 c = d > 3.3f ? Pal.Tire[(x + y) % 2 == 0 ? 1 : 2] : d < 1f ? Pal.Void : Pal.Bronze[2];
                g.Set(x, y, -3, Pal.Solid(c));
                if (d > 3.3f) { g.Set(x, y, -2, Pal.Solid(Pal.Tire[0])); g.Set(x, y, -4, Pal.Solid(Pal.Tire[1])); }
            }
            return Make("rear_spare_carrier", PartCategory.RearBumper, g, 45, 2);
        }

        // ---------------------------------------------------------------- body add-ons
        public static PartDesign RearSpoiler()
        {
            var g = new VoxelGrid();
            var paint = Pal.Weathered(Pal.Black, 0.3f, 51, 1, -10);
            g.Box(-8, 0, 0, -8, 1, 0, paint); g.Box(8, 0, 0, 8, 1, 0, paint);
            g.Box(-11, 2, -1, 11, 2, 1, paint);
            g.Box(-11, 3, -1, -11, 3, 0, paint); g.Box(11, 3, -1, 11, 3, 0, paint);
            return Make("spoiler_rear", PartCategory.Spoiler, g, 12);
        }

        public static PartDesign TwinFuelTanks()
        {
            var g = new VoxelGrid();
            var tank = Pal.Weathered(Pal.Metal, 0.5f, 61, 2, 20);
            g.CylX(1, -1.5f, 1.45f, -9, 9, tank);
            g.CylX(1, 1.5f, 1.45f, -9, 9, tank);
            foreach (int x in new[] { -5, 5 }) { g.CylX(1, -1.5f, 1.6f, x, x, Pal.Solid(Pal.Black[0])); g.CylX(1, 1.5f, 1.6f, x, x, Pal.Solid(Pal.Black[0])); }
            g.Set(7, 3, -1, Pal.Solid(Pal.Chrome[2])); g.Set(-7, 3, 2, Pal.Solid(Pal.Chrome[2]));
            return Make("cargo_twin_fuel_tanks", PartCategory.Cargo, g, 90, 2);
        }

        public static PartDesign JerryRack()
        {
            var g = new VoxelGrid();
            var rail = Pal.Ramp(Pal.Metal, 1, 71);
            g.Tube(new Vector3(-11, 1, -6), new Vector3(-11, 1, 6), 0.4f, rail);
            g.Tube(new Vector3(11, 1, -6), new Vector3(11, 1, 6), 0.4f, rail);
            foreach (int z in new[] { -6, 0, 6 }) g.Tube(new Vector3(-11, 1, z), new Vector3(11, 1, z), 0.4f, rail);
            foreach (int x in new[] { -11, 11 }) foreach (int z in new[] { -6, 6 }) g.Box(x, 0, z, x, 3, z, rail);
            var can = Pal.Weathered(Pal.Olive, 0.45f, 73, 3, 20);
            foreach (int x in new[] { -7, -2, 3 })
            {
                g.Box(x, 2, -4, x + 3, 6, -1, can);
                g.Box(x + 1, 7, -3, x + 2, 7, -3, Pal.Solid(Pal.Metal[1]));
                g.Box(x, 4, -4, x + 3, 4, -4, Pal.Solid(Pal.Olive[0]));
            }
            g.Box(-9, 2, 2, 8, 4, 4, Pal.Weathered(Pal.Sand, 0.2f, 75, 1, 0)); // tarp roll
            g.Tube(new Vector3(9, 2, 5), new Vector3(9, 20, 5), 0f, Pal.Solid(Pal.Metal[0])); // whip antenna
            g.Set(9, 21, 5, Pal.Solid(Pal.TailR));
            return Make("cargo_jerry_rack", PartCategory.Cargo, g, 70, 3);
        }

        public static PartDesign Turret()
        {
            var g = new VoxelGrid();
            var hull = Pal.Weathered(Pal.Olive, 0.45f, 81, 2, 3);
            var steel = Pal.Ramp(Pal.Metal, 1, 83);
            g.CylY(0, 0, 4.4f, 0, 1, steel);                                    // ring
            g.Box(-4, 2, -5, 4, 6, 3, hull);                                   // housing
            g.Box(-3, 7, -4, 3, 7, 2, hull);
            g.CylY(0, -1, 1.8f, 8, 8, Pal.Ramp(Pal.Metal, 2));                 // hatch
            g.Box(-2, 3, 4, 2, 5, 5, steel);                                   // mantlet
            g.CylZ(0, 4, 1.05f, 6, 20, Pal.Weathered(Pal.Metal, 0.3f, 85, 2, 0));  // barrel
            g.Box(-1, 3, 20, 1, 5, 22, Pal.Solid(Pal.Metal[0]));             // muzzle brake
            g.Set(0, 4, 22, Pal.Solid(Pal.Void));
            g.Box(5, 2, -3, 6, 5, 1, Pal.Weathered(Pal.Rust, 0.1f, 87, 2, 0)); // ammo box
            for (int z = -4; z <= 2; z += 3) { g.Set(-5, 6, z, Pal.Solid(Pal.Chrome[1])); g.Set(5, 6, z, Pal.Solid(Pal.Chrome[1])); } // rivets
            g.Tube(new Vector3(-3, 7, -5), new Vector3(-3, 17, -6), 0f, Pal.Solid(Pal.Metal[0]));
            return Make("weapon_turret_cannon", PartCategory.Weapon, g, 180, 3);
        }
    
        // ---------------------------------------------------------------- armour & rams
        public static PartDesign Ram()
        {
            var g = new VoxelGrid();
            var steel = Pal.Weathered(Pal.Metal, 0.4f, 1101, 2, 20);
            for (int y = 0; y <= 8; y++)
            for (int x = -13; x <= 13; x++)
            {
                int z = 5 - Mathf.Abs(x) / 4;
                g.Box(x, y, 0, x, y, z, (x + y) % 5 == 0 ? Pal.Solid(Pal.Rust[2]) : steel);
            }
            g.Box(-13, 9, 0, 13, 9, 3, Pal.Ramp(Pal.Rust, 1, 1102));
            foreach (int x in new[] { -10, 0, 10 }) g.Box(x, 3, 6, x + 1, 5, 7, Pal.Ramp(Pal.Chrome, 1));
            return Make("bumper_ram", PartCategory.FrontBumper, g, 140, 2);
        }

        static void Spikes(VoxelGrid g, int x0, int x1, int y, int z, int dir, int step)
        {
            for (int x = x0; x <= x1; x += step)
                g.Tube(new Vector3(x, y, z), new Vector3(x, y, z + dir * 5), 0.6f, p => Mathf.Abs(p.z - z) > 3 ? Pal.Chrome[2] : Pal.Metal[1]);
        }

        public static PartDesign SpikedBumper(bool front)
        {
            var g = new VoxelGrid();
            int dir = front ? 1 : -1;
            g.Box(-12, 0, 0, 12, 3, dir, Pal.Weathered(Pal.Rust, 0.3f, front ? 1105 : 1106, 2, 20));
            Spikes(g, -11, 11, 2, dir, dir, 4);
            Spikes(g, -9, 9, 0, dir, dir, 6);
            return Make(front ? "bumper_spiked" : "rear_spiked", front ? PartCategory.FrontBumper : PartCategory.RearBumper, g, front ? 45 : 35, 2);
        }

        /// <summary>Side armour, authored for the right side (+X out), length along Z.</summary>
        public static PartDesign SideSpikes()
        {
            var g = new VoxelGrid();
            g.Box(0, 0, -14, 1, 3, 14, Pal.Weathered(Pal.Metal, 0.5f, 1107, 2, 20));
            for (int z = -12; z <= 12; z += 4) g.Tube(new Vector3(1, 2, z), new Vector3(6, 2, z), 0.6f, p => p.x > 3 ? Pal.Chrome[2] : Pal.Metal[1]);
            return Make("armor_spikes", PartCategory.Armor, g, 30, 1);
        }

        public static PartDesign SidePlate()
        {
            var g = new VoxelGrid();
            g.Box(0, -2, -16, 1, 6, 16, p => (p.z % 8 == 0 || p.y == -2) ? Pal.Rust[1] : Pal.Pick(Pal.Metal, p, 1108, 2));
            for (int z = -14; z <= 14; z += 7) g.Set(2, 4, z, Pal.Solid(Pal.Chrome[1]));
            return Make("armor_plate", PartCategory.Armor, g, 60, 2);
        }

        public static PartDesign WinchBumper()
        {
            var g = new VoxelGrid();
            var steel = Pal.Weathered(Pal.Metal, 0.4f, 1130, 2, 20);
            g.Box(-12, 0, 0, 12, 3, 1, steel);
            g.CylX(2, 3, 2f, -4, 4, Pal.Ramp(Pal.Black, 2));                         // drum
            g.Box(-5, 0, 2, 5, 1, 4, Pal.Ramp(Pal.Metal, 1));
            g.Box(-1, 2, 5, 1, 2, 6, Pal.Solid(Pal.Amber));                          // fairlead
            foreach (int x in new[] { -10, 10 }) g.Box(x, 4, 0, x, 6, 1, Pal.Ramp(Pal.Rust, 2));   // shackle mounts
            return Make("bumper_winch", PartCategory.FrontBumper, g, 55, 2);
        }

        /// <summary>Knuckle crane for a cargo socket: turret, boom rising backwards, hook block (tip at 0, 30, -40 voxels).</summary>
        public static PartDesign CraneArm()
        {
            var g = new VoxelGrid();
            var paint = Pal.Weathered(Cat, 0.25f, 1131, 2, 0);
            g.Use("turret");
            g.CylY(0, 0, 4f, 0, 3, Pal.Ramp(Pal.Metal, 0));                          // slew ring
            g.Box(-3, 4, -3, 3, 10, 3, paint);                                        // turret
            g.Box(-1, 4, 4, 1, 8, 5, Pal.Solid(Pal.Amber));                           // beacon
            g.Tube(new Vector3(0, 6, 2), new Vector3(0, 9, -2), 0.7f, Pal.Ramp(Pal.Chrome, 2));   // ram foot
            g.Use("boom");
            g.Tube(new Vector3(0, 9, 0), new Vector3(0, 30, -38), 1.6f, paint);        // boom
            g.Tube(new Vector3(0, 10, -3), new Vector3(0, 20, -20), 0.7f, Pal.Ramp(Pal.Chrome, 2));   // lift ram
            g.Box(-2, 28, -41, 2, 31, -37, Pal.Ramp(Pal.Metal, 2));                   // sheave head
            g.Use("body");
            var p = Make("cargo_crane", PartCategory.Cargo, g, 450, 3);
            return p.Segment("turret", new Vector3Int(0, 0, 0)).Segment("boom", new Vector3Int(0, 9, 0), "turret");
        }

        // ---------------------------------------------------------------- machine tools
        static readonly Color32[] Cat = { Pal.Hex("6a5010"), Pal.Hex("9a7418"), Pal.Hex("c89a22"), Pal.Hex("e4bc34") };
        static VoxMat CatPaint(int seed) => Pal.Weathered(Cat, 0.25f, seed, 2, 0);

        static void Bucket(VoxelGrid g, Vector3Int c, int w, int d)
        {
            for (int x = -w; x <= w; x++)
            for (int y = 0; y <= d; y++)
            for (int z = 0; z <= d; z++)
            {
                float r = Mathf.Sqrt(y * y + z * z);
                bool shell = r > d - 1.2f && r <= d || Mathf.Abs(x) == w && r <= d;
                if (shell && !(y > 0 && z > 0 && y + z < d / 2)) g.Set(c.x + x, c.y + y, c.z + z, Pal.Ramp(Pal.Metal, 1, 1110));
            }
            for (int x = -w; x <= w; x += 2) g.Set(c.x + x, c.y, c.z + d + 1, Pal.Solid(Pal.Chrome[2]));   // teeth
        }

        public static PartDesign ExcavatorArm()
        {
            var g = new VoxelGrid();
            var paint = CatPaint(1111);
            g.Use("boom");
            g.CylX(0, 0, 2.2f, -2, 2, Pal.Ramp(Pal.Metal, 1, 1109));                     // foot pin
            g.Tube(new Vector3(0, 0, 0), new Vector3(0, 16, 24), 1.6f, paint);            // boom
            g.Tube(new Vector3(0, 4, 6), new Vector3(0, 14, 20), 0.6f, Pal.Ramp(Pal.Chrome, 2));   // boom ram
            g.Use("stick");
            g.CylX(16, 24, 1.8f, -2, 2, Pal.Ramp(Pal.Metal, 1, 1108));                   // knuckle pin
            g.Tube(new Vector3(0, 16, 24), new Vector3(0, -6, 42), 1.3f, paint);          // stick
            g.Tube(new Vector3(0, 19, 27), new Vector3(0, 6, 38), 0.5f, Pal.Ramp(Pal.Chrome, 2));   // bucket ram
            g.Use("bucket");
            Bucket(g, new Vector3Int(0, -14, 38), 5, 7);
            g.Use("body");
            var p = Make("tool_excavator_arm", PartCategory.Tool, g, 900, 4);
            return p.Segment("boom", new Vector3Int(0, 0, 0)).Segment("stick", new Vector3Int(0, 16, 24), "boom").Segment("bucket", new Vector3Int(0, -6, 42), "stick");
        }

        public static PartDesign LoaderBucket()
        {
            var g = new VoxelGrid();
            var paint = CatPaint(1112);
            g.Use("arms");
            foreach (int x in new[] { -11, 11 }) g.Tube(new Vector3(x, 0, 0), new Vector3(x, -4, 14), 1f, paint);
            g.Tube(new Vector3(-11, -2, 7), new Vector3(11, -2, 7), 0.7f, paint);
            g.Use("bucket");
            Bucket(g, new Vector3Int(0, -10, 12), 12, 6);
            g.Use("body");
            var p = Make("tool_backhoe_loader", PartCategory.Tool, g, 350, 3);
            return p.Segment("arms", new Vector3Int(0, 0, 0)).Segment("bucket", new Vector3Int(0, -4, 14), "arms");
        }

        public static PartDesign DozerBlade()
        {
            var g = new VoxelGrid();
            g.Use("blade");
            for (int x = -19; x <= 19; x++)
            for (int y = 0; y <= 12; y++)
            {
                int z = Mathf.RoundToInt(3f - Mathf.Sin(y / 12f * Mathf.PI) * 2.5f);
                g.Set(x, y, z, y == 0 ? Pal.Solid(Pal.Chrome[2]) : Pal.Solid(Pal.Pick(Cat, new Vector3Int(x, y, z), 1113, 2)));
                g.Set(x, y, z - 1, Pal.Ramp(Pal.Metal, 0, 1114));
            }
            g.Use("lift");
            foreach (int x in new[] { -12, 12 }) g.Tube(new Vector3(x, 4, -1), new Vector3(x, 4, -14), 1f, CatPaint(1115));   // push arms
            foreach (int x in new[] { -6, 6 }) g.Tube(new Vector3(x, 10, -2), new Vector3(x, 12, -12), 0.6f, Pal.Ramp(Pal.Chrome, 2)); // lift rams
            g.Use("body");
            var p = Make("tool_dozer_blade", PartCategory.Tool, g, 600, 4);
            return p.Segment("lift", new Vector3Int(0, 4, -14)).Segment("blade", new Vector3Int(0, 4, 0), "lift");
        }

        /// <summary>Tipper bed, hinged at its rear edge (origin); extends forward.</summary>
        public static PartDesign DumpBed()
        {
            var g = new VoxelGrid();
            var paint = CatPaint(1116);
            g.Box(-14, 0, 0, 14, 1, 48, Pal.Ramp(Pal.Metal, 1, 1117));
            for (int z = 0; z <= 48; z++) { g.Box(-14, 2, z, -14, 11, z, paint); g.Box(14, 2, z, 14, 11, z, paint); }
            g.Box(-14, 2, 48, 14, 14, 49, paint);                                         // headboard
            g.Box(-14, 14, 49, 14, 14, 54, Pal.Ramp(Pal.Metal, 2));                        // cab guard
            g.Box(-14, 2, 0, 14, 9, 0, Pal.Ramp(Pal.Rust, 2, 1118));                      // tailgate
            for (int z = 4; z <= 44; z += 8) { g.Box(-15, 3, z, -15, 10, z, Pal.Ramp(Pal.Metal, 0)); g.Box(15, 3, z, 15, 10, z, Pal.Ramp(Pal.Metal, 0)); }
            return Make("tool_dump_bed", PartCategory.Tool, g, 700, 4);
        }

        /// <summary>Rear backhoe for the backhoe loader: swing post, boom, dipper stick and a bucket that curls towards the machine.</summary>
        public static PartDesign HoeArm()
        {
            var g = new VoxelGrid();
            var paint = CatPaint(1121);
            g.Use("swing");
            g.CylY(0, 0, 2.6f, -3, 2, Pal.Ramp(Pal.Metal, 1, 1122));
            g.Box(-2, -2, -3, 2, 3, 0, paint);
            g.Use("boom");
            g.Tube(new Vector3(0, 1, -2), new Vector3(0, 13, -18), 1.3f, paint);
            g.Tube(new Vector3(0, -1, -4), new Vector3(0, 9, -14), 0.5f, Pal.Ramp(Pal.Chrome, 2));
            g.Use("stick");
            g.Tube(new Vector3(0, 13, -18), new Vector3(0, -3, -28), 1.1f, paint);
            g.Use("bucket");
            var b = new VoxelGrid();
            Bucket(b, new Vector3Int(0, -9, -3), 4, 5);                                   // teeth towards +z: mirrored to face the machine
            foreach (var kv in b.voxels) { g.voxels[new Vector3Int(kv.Key.x, kv.Key.y, -kv.Key.z - 28)] = new Vox { color = kv.Value.color, label = g.label, mat = g.material }; }
            g.Use("body");
            var p = Make("tool_hoe_arm", PartCategory.Tool, g, 420, 3);
            return p.Segment("swing", new Vector3Int(0, 0, 0)).Segment("boom", new Vector3Int(0, 1, -2), "swing").Segment("stick", new Vector3Int(0, 13, -18), "boom").Segment("bucket", new Vector3Int(0, -3, -28), "stick");
        }

        public static PartDesign PaverScreed()
        {
            var g = new VoxelGrid();
            g.Box(-19, 0, -8, 19, 3, 0, Pal.Ramp(Pal.Metal, 1, 1119));                   // screed
            g.Box(-16, 4, -6, 16, 12, 10, CatPaint(1120));                                // hopper
            g.Box(-14, 12, -4, 14, 12, 8, Pal.Solid(Pal.Black[0]));                        // hot mix
            foreach (int x in new[] { -19, 19 }) g.Box(x, 0, -8, x, 5, 0, Pal.Solid(Pal.Amber));
            return Make("tool_paver_screed", PartCategory.Tool, g, 800, 4);
        }
}
}
