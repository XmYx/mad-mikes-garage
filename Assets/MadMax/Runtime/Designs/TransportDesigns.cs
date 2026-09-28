using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Wrecker (crane truck) and car transport trailers (single deck, double deck with a tilting upper deck).</summary>
    public static partial class VehicleDesigns
    {
        // ================================================================== WRECKER (tow / crane truck)
        public static VehicleDesign Wrecker()
        {
            var d = new VehicleDesign { name = "Wrecker", mass = 5200, drive = VehicleDriver.Drive.Rear, travel = 0.26f, frequency = 1.6f, finalDrive = 5.4f, brakeForce = 50000f, maxSteer = 32f, eye = new Vector3Int(4, 25, 16), hitch = new Vector3Int(0, 10, -44), fuelL = 160f, oilL = 14f, coolantL = 22f };
            var g = new VoxelGrid();
            var paint = Pal.Weathered(Pal.Rust, 0.3f, 1300, 3, 6);
            foreach (int s in new[] { -1, 1 }) g.Box(s * 6, 8, -44, s * 8, 11, 40, Pal.Ramp(Pal.Metal, 0, 1301));      // chassis rails
            for (int z = 22; z <= 42; z++)                                                                              // bonnet
            for (int x = -12; x <= 12; x++)
            for (int y = 10; y <= 19; y++)
            {
                bool shell = Abs(x) == 12 || z == 42 || y == 10 || y == 19;
                if (shell) g.Set(x, y, z, z == 42 && y < 18 && Abs(x) < 10 ? Pal.Stripe(Pal.Solid(Pal.Void), Pal.Ramp(Pal.Chrome, 1), 1, 2) : paint);
            }
            Cab(g, 12, 4, 21, 14, 32, paint, 1302);
            g.Box(-13, 11, -42, 13, 12, 3, Pal.Stripe(Pal.Ramp(Pal.Metal, 1), Pal.Ramp(Pal.Metal, 0), 2, 3));                // flat bed
            foreach (int s in new[] { -1, 1 }) g.Box(s * 13, 13, -42, s * 13, 15, 3, Pal.Ramp(Pal.Rust, 1));
            g.Box(-3, 33, 10, 3, 34, 12, Pal.Stripe(Pal.Solid(Pal.Amber), Pal.Solid(Pal.Black[1]), 0, 2));                  // light bar
            Lamps(g, 10, 15, 43, -43);
            Finish(d, g);
            d.Socket("wheel_front", PartCategory.Wheel, 12, 8, 32, "wheel_truck", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 12, 8, -28, "wheel_truck", true);
            d.Socket("engine", PartCategory.Engine, 0, 12, 30, "engine_diesel_i6");
            d.Socket("radiator", PartCategory.Radiator, 0, 12, 40, "radiator_car");
            d.Socket("bumper_front", PartCategory.FrontBumper, 0, 9, 43, "bumper_winch");
            d.Socket("cargo", PartCategory.Cargo, 0, 13, -30, "cargo_crane");
            return d;
        }

        static void TrailerFrame(VoxelGrid g, int halfLen, int deckY, int seed)
        {
            var steel = Pal.Ramp(Pal.Metal, 1, seed);
            foreach (int s in new[] { -1, 1 }) g.Box(s * 12, deckY - 3, -halfLen, s * 14, deckY - 1, halfLen, steel);        // side beams
            for (int z = -halfLen; z <= halfLen; z += 8) g.Box(-12, deckY - 2, z, 12, deckY - 1, z, steel);                   // cross members
            g.Tube(new Vector3(0, deckY - 2, halfLen), new Vector3(0, deckY - 2, halfLen + 14), 0.8f, steel);                  // drawbar
            g.Box(-1, deckY - 3, halfLen + 14, 1, deckY - 1, halfLen + 15, Pal.Solid(Pal.Metal[3]));
            foreach (int s in new[] { -1, 1 }) g.Box(s * 13, deckY - 2, -halfLen - 1, s * 11, deckY - 1, -halfLen - 1, Pal.Solid(Pal.TailR));
        }

        static VoxelGrid Deck(int halfW, int len, int seed)
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Scrap);
            g.Box(-halfW, 0, -len, halfW, 1, 0, p => (p.z % 3 == 0) ? Pal.Metal[0] : Pal.Metal[2]);
            foreach (int x in new[] { -halfW, halfW }) g.Box(x, 2, -len, x, 3, 0, Pal.Ramp(Pal.Rust, 2, seed));
            g.Bevel();
            return g;
        }

        static VoxelGrid Ramp(int halfW, int len)
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Scrap);
            g.Box(-halfW, 0, -len, halfW, 1, 0, p => (p.z % 2 == 0) ? Pal.Metal[1] : Pal.Metal[2]);
            g.Bevel();
            return g;
        }

        // ================================================================== CAR TRAILER (single deck)
        public static VehicleDesign CarTrailer()
        {
            var d = new VehicleDesign { name = "CarTrailer", mass = 700, driveable = false, fuelL = 0f, oilL = 0f, coolantL = 0f, usesCoolant = false, maxSteer = 0f, travel = 0.16f, frequency = 1.9f, brakeForce = 12000f, coupler = new Vector3Int(0, 7, 55) };
            var g = new VoxelGrid();
            TrailerFrame(g, 40, 9, 1310);
            foreach (int s in new[] { -1, 1 }) g.Box(s * 15, 11, -8, s * 17, 11, 8, Pal.Ramp(Pal.Black, 1));                 // fenders
            g.Use("legs");
            g.Box(0, 1, 50, 0, 7, 50, Pal.Ramp(Pal.Metal, 1)); g.Box(-1, 1, 49, 1, 1, 51, Pal.Ramp(Pal.Metal, 1));
            g.label = 0;
            g.Bevel();
            d.legs = g.Take("legs", Vector3Int.zero);   // legs leave the body mesh/colliders: they wind up when hitched
            d.body = g;
            d.colliders.Add(VehicleDesign.Box(-14, 6, -40, 14, 8, 40));                                                        // frame under the deck
            d.movable.Add(("Deck", Deck(14, 80, 1311), new Vector3Int(0, 9, 40), Vector3.zero));
            d.movable.Add(("RampL", Ramp(4, 22), new Vector3Int(-8, 9, -41), new Vector3(-22f, 0, 0)));
            d.movable.Add(("RampR", Ramp(4, 22), new Vector3Int(8, 9, -41), new Vector3(-22f, 0, 0)));
            d.Socket("wheel_front", PartCategory.Wheel, 12, 4, 4, "wheel_small", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 12, 4, -6, "wheel_small", true);
            return d;
        }

        // ================================================================== CAR TRAILER (double deck, tilting upper deck)
        public static VehicleDesign CarTrailerDouble()
        {
            var d = new VehicleDesign { name = "CarTrailerDouble", mass = 1400, driveable = false, fuelL = 0f, oilL = 0f, coolantL = 0f, usesCoolant = false, maxSteer = 0f, travel = 0.16f, frequency = 1.9f, brakeForce = 20000f, coupler = new Vector3Int(0, 7, 55) };
            var g = new VoxelGrid();
            TrailerFrame(g, 40, 9, 1320);
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 15, 11, -8, s * 17, 11, 8, Pal.Ramp(Pal.Black, 1));
                g.Box(s * 14, 9, 38, s * 14, 36, 40, Pal.Ramp(Pal.Metal, 2, 1321));                                            // front posts
                g.Box(s * 14, 9, -40, s * 14, 36, -38, Pal.Ramp(Pal.Metal, 2, 1322));                                          // rear posts
                g.Box(s * 14, 36, -40, s * 14, 37, 40, Pal.Ramp(Pal.Metal, 1, 1323));                                          // top rails
            }
            g.Box(-14, 36, 38, 14, 37, 40, Pal.Ramp(Pal.Metal, 1));
            g.Use("legs");
            g.Box(0, 1, 50, 0, 7, 50, Pal.Ramp(Pal.Metal, 1)); g.Box(-1, 1, 49, 1, 1, 51, Pal.Ramp(Pal.Metal, 1));
            g.label = 0;
            g.Bevel();
            d.legs = g.Take("legs", Vector3Int.zero);   // legs leave the body mesh/colliders: they wind up when hitched
            d.body = g;
            d.colliders.Add(VehicleDesign.Box(-14, 6, -40, 14, 8, 40));
            d.colliders.Add(VehicleDesign.Box(-14, 9, 38, -13, 37, 40)); d.colliders.Add(VehicleDesign.Box(13, 9, 38, 14, 37, 40));
            d.colliders.Add(VehicleDesign.Box(-14, 9, -40, -13, 37, -38)); d.colliders.Add(VehicleDesign.Box(13, 9, -40, 14, 37, -38));
            d.movable.Add(("Deck", Deck(13, 80, 1324), new Vector3Int(0, 9, 40), Vector3.zero));
            d.movable.Add(("UpperDeck", Deck(12, 78, 1325), new Vector3Int(0, 33, 37), Vector3.zero));
            d.movable.Add(("RampL", Ramp(4, 22), new Vector3Int(-8, 9, -41), new Vector3(-22f, 0, 0)));
            d.movable.Add(("RampR", Ramp(4, 22), new Vector3Int(8, 9, -41), new Vector3(-22f, 0, 0)));
            d.Socket("wheel_front", PartCategory.Wheel, 12, 4, 4, "wheel_small", true);
            d.Socket("wheel_rear", PartCategory.Wheel, 12, 4, -6, "wheel_small", true);
            return d;
        }
    }
}
