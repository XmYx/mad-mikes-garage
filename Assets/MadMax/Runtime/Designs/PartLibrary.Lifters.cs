using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Heavy lifters (cargo socket, size 4): the lattice-boom magnet crane (<see cref="MagnetCrane"/>) and the
    /// knuckle-boom scrap grapple (<see cref="GrappleCrusher"/>). Both are authored with the boom lying flat along -z
    /// (its transport pose); the components raise and swing the hinged segments at runtime.</summary>
    public static partial class PartLibrary
    {
        static IEnumerable<PartDesign> LiftersParts()
        {
            yield return MagnetCraneArm();
            yield return GrappleCraneArm();
        }

        static VoxMat Hazard(int axis, int seed) => Pal.Stripe(Pal.Ramp(Pal.Black, 1, seed), Pal.Solid(Pal.Amber), axis, 4, 2);

        /// <summary>Rivet heads every <paramref name="step"/> voxels along a line.</summary>
        static void RivetLine(VoxelGrid g, Vector3Int a, Vector3Int b, int step)
        {
            var d = b - a;
            int n = Mathf.Max(Mathf.Abs(d.x), Mathf.Max(Mathf.Abs(d.y), Mathf.Abs(d.z)));
            for (int i = 0; i <= n; i += step)
            {
                float t = n == 0 ? 0f : i / (float)n;
                g.Set(Vector3Int.RoundToInt(Vector3.Lerp(a, b, t)), Pal.Solid(Pal.Chrome[1]));
            }
        }

        /// <summary>Scrap-yard magnet crane: slewing house with the magnet generator, cable drum, A-frame gantry and a
        /// cast counterweight on the cab side; an 6.7 m lattice boom (four chords, laced, cross-battened) with a striped
        /// sheave head. Turret pivots at the mount, the boom at (0, 10, -10); the hoist cable leaves the head sheave.</summary>
        public static PartDesign MagnetCraneArm()
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Steel);
            var yellow = Pal.Weathered(Cat, 0.3f, 7101, 2, 0);
            var yellowDark = Pal.Ramp(Cat, 1, 7102);
            var iron = Pal.Weathered(Pal.Metal, 0.35f, 7103, 1, 0);
            var black = Pal.Ramp(Pal.Black, 1, 7105);
            var chrome = Pal.Ramp(Pal.Chrome, 2, 7106);

            // ---------------------------------------------------------- turret
            g.Use("turret");
            g.CylY(0, 0, 8.4f, 0, 1, Pal.Ramp(Pal.Metal, 0, 7107));                                         // slew bearing
            for (int i = 0; i < 40; i += 2)                                                                  // ring gear teeth
            {
                float a = i * Mathf.PI / 20f;
                g.Set(Mathf.RoundToInt(Mathf.Cos(a) * 9f), 1, Mathf.RoundToInt(Mathf.Sin(a) * 9f), Pal.Solid(Pal.Metal[2]));
            }
            g.Box(-10, 2, -13, 10, 3, 13, iron);                                                              // turntable deck
            RivetLine(g, new Vector3Int(-10, 3, -13), new Vector3Int(-10, 3, 13), 3);
            RivetLine(g, new Vector3Int(10, 3, -13), new Vector3Int(10, 3, 13), 3);
            // machinery house: the magnet generator set
            g.Box(-9, 4, -7, 9, 13, 6, yellow);
            foreach (int s in new[] { -1, 1 })
            {
                for (int y = 6; y <= 11; y += 2) g.Box(s * 9, y, -5, s * 9, y, -1, black);                     // louvres
                g.Box(s * 9, 5, 1, s * 9, 12, 1, yellowDark); g.Box(s * 9, 12, 1, s * 9, 12, 5, yellowDark);   // service door
                g.Set(s * 10, 8, 4, Pal.Solid(Pal.Chrome[2]));                                                  // latch
                RivetLine(g, new Vector3Int(s * 9, 13, -7), new Vector3Int(s * 9, 13, 6), 2);
            }
            g.Box(-7, 5, -8, 7, 11, -8, Pal.Stripe(Pal.Ramp(Pal.Metal, 0, 7108), Pal.Ramp(Pal.Black, 0, 7109), 1, 2));   // radiator grille
            // cast counterweight (cab side): slabs, lifting eyes, hazard band
            g.Box(-10, 4, 7, 10, 12, 13, iron);
            for (int y = 6; y <= 12; y += 3) g.Box(-10, y, 13, 10, y, 13, Pal.Solid(Pal.Metal[0]));            // slab seams
            g.Box(-10, 4, 13, 10, 5, 13, Hazard(0, 7110));
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 10, 4, 7, s * 10, 5, 13, Hazard(2, 7111));
                g.Box(s * 6, 13, 10, s * 6, 14, 10, Pal.Solid(Pal.Metal[3]));                                  // lifting eyes
            }
            // hoist drum on the house roof, between its cheek plates
            g.CylX(17, -1, 3.3f, -6, 6, Pal.Ramp(Pal.Black, 2, 7112));
            for (int x = -5; x <= 5; x += 2) g.CylX(17, -1, 3.5f, x, x, Pal.Ramp(Pal.Metal, 2, 7113), 3.0f);   // wire wraps
            foreach (int s in new[] { -1, 1 }) g.CylX(17, -1, 4.2f, s * 7, s * 7, yellowDark);
            g.CylX(17, -1, 0.9f, -8, 8, chrome);
            // A-frame gantry for the boom pendants
            foreach (int s in new[] { -1, 1 })
            {
                g.Tube(new Vector3(s * 8, 14, -6), new Vector3(s * 2, 27, 1), 0.8f, yellow);
                g.Tube(new Vector3(s * 8, 14, 7), new Vector3(s * 2, 27, 1), 0.8f, yellow);
            }
            g.Box(-2, 27, 0, 2, 28, 2, yellowDark);
            g.CylX(28, 1, 1.6f, -1, 1, chrome);
            // exhaust stack, beacon, hand rails, ladder, control cabinet
            g.Tube(new Vector3(6, 14, 4), new Vector3(6, 21, 4), 0.8f, black);
            g.Box(5, 22, 3, 7, 22, 5, Pal.Ramp(Pal.Rust, 2, 7114));
            g.Set(-7, 14, 5, Pal.Solid(Pal.Metal[1])); g.Set(-7, 15, 5, Pal.Solid(Pal.Amber)); g.Set(-7, 16, 5, Pal.Solid(Pal.LightY));
            for (int z = -6; z <= 6; z += 6) foreach (int s in new[] { -1, 1 }) g.Box(s * 9, 14, z, s * 9, 15, z, Pal.Solid(Pal.Amber));
            foreach (int s in new[] { -1, 1 }) g.Tube(new Vector3(s * 9, 15, -6), new Vector3(s * 9, 15, 6), 0.5f, Pal.Solid(Pal.Amber));
            for (int y = 4; y <= 13; y += 2) g.Box(-10, y, 3, -10, y, 5, Pal.Solid(Pal.Metal[2]));            // ladder rungs
            g.Box(-11, 4, 3, -11, 14, 3, Pal.Solid(Pal.Metal[1])); g.Box(-11, 4, 5, -11, 14, 5, Pal.Solid(Pal.Metal[1]));
            g.Box(6, 4, -12, 10, 9, -9, black);                                                               // magnet rectifier cabinet
            g.Set(8, 8, -13, Pal.Solid(Pal.LightY)); g.Set(9, 8, -13, Pal.Solid(Pal.TailR));
            g.Tube(new Vector3(8, 9, -10), new Vector3(2, 15, -2), 0.5f, Pal.Ramp(Pal.Black, 0, 7115));       // power cable to the drum
            // boom foot brackets, pin and the luffing ram lugs
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 6, 4, -13, s * 7, 12, -7, yellowDark);
                g.Box(s * 3, 3, -13, s * 4, 5, -11, yellowDark);
            }
            g.CylX(10, -10, 1.3f, -8, 8, chrome);

            // ---------------------------------------------------------- lattice boom (flat, pointing -z)
            g.Use("boom");
            const int foot = -17, head = -86;
            System.Func<int, int, int, Vector3> chord = (sx, sy, z) =>
            {
                float t = Mathf.InverseLerp(foot, head, z);
                return new Vector3(sx * Mathf.Lerp(5f, 3f, t), 10f + sy * Mathf.Lerp(4f, 3f, t), z);
            };
            g.Box(-5, 6, foot, 5, 14, -8, yellow);                                                            // box-section foot
            g.CylX(10, -10, 2.4f, -5, 5, yellowDark);
            g.Box(5, 9, -15, 5, 11, -11, Pal.Ramp(Pal.Cream, 2, 7116));                                        // load chart plate
            g.Box(-5, 9, -15, -5, 11, -11, Pal.Ramp(Pal.Cream, 2, 7116));
            RivetLine(g, new Vector3Int(-5, 14, foot), new Vector3Int(-5, 14, -8), 2);
            RivetLine(g, new Vector3Int(5, 14, foot), new Vector3Int(5, 14, -8), 2);
            foreach (int sx in new[] { -1, 1 })
                foreach (int sy in new[] { -1, 1 })
                    g.Tube(chord(sx, sy, foot), chord(sx, sy, head), 0.9f, yellow);                          // four chords
            var lace = Pal.Ramp(Cat, 1, 7117);
            bool flip = false;
            for (int z = foot; z > head; z -= 6, flip = !flip)
            {
                int z2 = Mathf.Max(head, z - 6);
                foreach (int s in new[] { -1, 1 })
                {
                    // side faces and top / bottom faces: zig-zag lacing between panel points
                    g.Tube(chord(s, flip ? 1 : -1, z), chord(s, flip ? -1 : 1, z2), 0.5f, lace);
                    g.Tube(chord(flip ? 1 : -1, s, z), chord(flip ? -1 : 1, s, z2), 0.5f, lace);
                    // cross battens at every panel point
                    g.Tube(chord(s, -1, z), chord(s, 1, z), 0.5f, lace);
                    g.Tube(chord(-1, s, z), chord(1, s, z), 0.5f, lace);
                }
            }
            // ram lugs under the boom
            foreach (int s in new[] { -1, 1 }) g.Box(s * 3, 2, -41, s * 3, 5, -39, yellowDark);
            // head: striped cheek plates, twin sheaves, pendant lug, work lamp
            g.Box(-3, 7, -94, 3, 13, head, yellow);
            foreach (int s in new[] { -1, 1 }) g.Box(s * 3, 7, -94, s * 3, 13, -88, Hazard(2, 7118));
            g.ClearBox(-2, 3, -96, 2, 9, -89);
            g.CylX(8, -92, 3.4f, -2, 2, Pal.Ramp(Pal.Black, 1, 7119));
            g.CylX(8, -92, 2.2f, -2, 2, Pal.Ramp(Pal.Metal, 2, 7120), 1.4f);
            g.CylX(8, -92, 0.8f, -4, 4, chrome);
            g.Box(0, 14, -88, 0, 15, -86, Pal.Solid(Pal.Metal[3]));
            g.Set(-1, 13, -95, Pal.Solid(Pal.LightW)); g.Set(1, 13, -95, Pal.Solid(Pal.LightW)); g.Set(0, 13, -95, Pal.Solid(Pal.Metal[1]));
            g.Set(0, 14, -94, Pal.Solid(Pal.TailR));                                                         // aircraft warning lamp

            g.Use("body");
            var p = Make(MagnetCrane.PartId, PartCategory.Cargo, g, 2600, 4);
            return p.Segment("turret", new Vector3Int(0, 0, 0)).Segment("boom", new Vector3Int(0, 10, -10), "turret");
        }

        /// <summary>Scrap handler: tall slewing pedestal with an elevated operator cab, counterweight and oil tank; a
        /// 4 m box boom, a hanging stick and a four-tine orange-peel claw (two jaws of two tines) on a rotator.
        /// Pivots: boom (0, 26, -4), stick at the boom tip (0, 26, -54), claw at the stick end (0, 0, -54), jaws at the
        /// head's edges.</summary>
        public static PartDesign GrappleCraneArm()
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Steel);
            var yellow = Pal.Weathered(Cat, 0.32f, 7201, 2, 0);
            var yellowDark = Pal.Ramp(Cat, 1, 7202);
            var iron = Pal.Weathered(Pal.Metal, 0.35f, 7203, 1, 0);
            var black = Pal.Ramp(Pal.Black, 1, 7204);
            var chrome = Pal.Ramp(Pal.Chrome, 2, 7205);
            var tine = Pal.Weathered(Pal.Metal, 0.45f, 7206, 2, 0);

            // ---------------------------------------------------------- turret: pedestal, house, cab
            g.Use("turret");
            g.CylY(0, 0, 6.5f, 0, 1, Pal.Ramp(Pal.Metal, 0, 7207));
            for (int i = 0; i < 32; i += 2)
            {
                float a = i * Mathf.PI / 16f;
                g.Set(Mathf.RoundToInt(Mathf.Cos(a) * 7f), 1, Mathf.RoundToInt(Mathf.Sin(a) * 7f), Pal.Solid(Pal.Metal[2]));
            }
            g.CylY(0, 0, 4.5f, 2, 14, yellow);                                                                // pedestal column
            g.CylY(0, 0, 5f, 3, 3, Pal.Ramp(Pal.Metal, 1, 7208));
            g.CylY(0, 0, 5f, 13, 13, Pal.Ramp(Pal.Metal, 1, 7208));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                g.Set(Mathf.RoundToInt(Mathf.Cos(a) * 5f), 4, Mathf.RoundToInt(Mathf.Sin(a) * 5f), Pal.Solid(Pal.Chrome[1]));
            }
            g.Box(-6, 15, -6, 6, 22, 8, yellow);                                                              // upper house
            for (int y = 16; y <= 20; y += 2) g.Box(-6, y, 0, -6, y, 6, black);                                // louvres
            g.Box(-6, 14, 9, 6, 22, 13, iron);                                                                // counterweight
            g.Box(-6, 14, 13, 6, 15, 13, Hazard(0, 7209));
            for (int y = 17; y <= 21; y += 4) g.Box(-6, y, 13, 6, y, 13, Pal.Solid(Pal.Metal[0]));
            RivetLine(g, new Vector3Int(-6, 22, -6), new Vector3Int(-6, 22, 13), 2);
            // elevated operator's cab on the right
            var glass = Pal.Ramp(Pal.Glass, 1, 7210);
            for (int x = 7; x <= 12; x++)
            for (int y = 15; y <= 26; y++)
            for (int z = -3; z <= 7; z++)
            {
                bool wall = x == 7 || x == 12 || z == -3 || z == 7 || y == 15 || y == 26;
                if (!wall) continue;
                bool post = (x == 12 || x == 7) && (z == -3 || z == 7);
                bool pane = !post && y >= 19 && y <= 25 && x != 7 && y != 26;
                g.Set(x, y, z, pane ? glass : y == 26 ? iron : yellow);
            }
            g.Box(6, 27, -4, 13, 27, 8, iron);                                                                // roof with visor
            g.Box(8, 25, -5, 11, 25, -4, Pal.Ramp(Pal.Metal, 1, 7211));                                        // wiper housing
            g.Box(9, 16, 3, 11, 18, 5, Pal.Ramp(Pal.Olive, 1, 7212));                                          // seat
            g.Box(9, 19, 5, 11, 22, 5, Pal.Ramp(Pal.Olive, 1, 7212));
            g.Box(8, 18, 0, 8, 20, 0, black); g.Box(11, 18, 0, 11, 20, 0, black);                               // joysticks
            g.Set(8, 21, 0, Pal.Solid(Pal.TailR)); g.Set(11, 21, 0, Pal.Solid(Pal.TailR));
            g.Set(12, 26, -4, Pal.Solid(Pal.LightW)); g.Set(10, 28, 6, Pal.Solid(Pal.Amber));                  // work lamp, beacon
            for (int y = 2; y <= 14; y += 2) g.Box(7, y, 6, 9, y, 6, Pal.Solid(Pal.Metal[2]));                 // ladder up to the cab
            g.Box(7, 2, 7, 7, 15, 7, Pal.Solid(Pal.Metal[1])); g.Box(9, 2, 7, 9, 15, 7, Pal.Solid(Pal.Metal[1]));
            // oil tank and hoses on the left, exhaust behind
            g.Box(-11, 15, -2, -7, 21, 6, Pal.Weathered(Pal.Metal, 0.3f, 7213, 2, 0));
            g.Set(-9, 22, 2, Pal.Solid(Pal.Chrome[2]));
            g.Box(-11, 17, -2, -11, 20, -2, Pal.Ramp(Pal.Glass, 3, 7214));                                     // sight glass
            g.Tube(new Vector3(-7, 20, -2), new Vector3(-2, 25, -7), 0.5f, Pal.Ramp(Pal.Black, 0, 7215));
            g.Tube(new Vector3(-7, 18, -1), new Vector3(2, 24, -7), 0.5f, Pal.Ramp(Pal.Black, 0, 7215));
            g.Tube(new Vector3(-4, 22, 11), new Vector3(-4, 29, 11), 0.7f, black);
            g.Box(-5, 30, 10, -3, 30, 12, Pal.Ramp(Pal.Rust, 2, 7216));
            // boom brackets, pin, ram lugs
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 3, 22, -8, s * 4, 29, -1, yellowDark);
                g.Box(s * 2, 16, -8, s * 2, 18, -6, yellowDark);
            }
            g.CylX(26, -4, 1.2f, -5, 5, chrome);

            // ---------------------------------------------------------- boom (flat, pointing -z)
            g.Use("boom");
            for (int z = -2; z >= -52; z--)
            {
                float t = Mathf.InverseLerp(-2, -52, z);
                int h = Mathf.RoundToInt(Mathf.Lerp(3.4f, 2f, t));
                g.Box(-2, 26 - h, z, 2, 26 + h, z, yellow);
            }
            g.CylX(26, -4, 2.8f, -2, 2, yellow);
            foreach (int zc in new[] { -16, -28, -40 })                                                        // lightening holes
                foreach (int s in new[] { -1, 1 })
                    for (int dz = -2; dz <= 2; dz++)
                        for (int dy = -1; dy <= 1; dy++)
                            if (Mathf.Abs(dz) + Mathf.Abs(dy) < 3) g.Set(s * 2, 26 + dy, zc + dz, Pal.Solid(Pal.Black[0]));
            RivetLine(g, new Vector3Int(-2, 29, -4), new Vector3Int(-2, 28, -50), 3);
            RivetLine(g, new Vector3Int(2, 29, -4), new Vector3Int(2, 28, -50), 3);
            foreach (int s in new[] { -1, 1 }) g.Tube(new Vector3(s, 30, -6), new Vector3(s, 29, -50), 0.5f, Pal.Ramp(Pal.Black, 0, 7217));   // hoses along the top
            foreach (int s in new[] { -1, 1 }) g.Box(s * 2, 21, -25, s * 2, 23, -23, yellowDark);               // ram lugs
            g.Box(-3, 23, -57, 3, 29, -51, yellowDark);                                                        // tip clevis
            foreach (int s in new[] { -1, 1 }) g.Box(s * 3, 23, -57, s * 3, 29, -52, Hazard(1, 7218));
            g.CylX(26, -54, 1.2f, -4, 4, chrome);
            g.Set(0, 23, -58, Pal.Solid(Pal.LightW));

            // ---------------------------------------------------------- stick (hangs from the boom tip)
            g.Use("stick");
            g.Box(-2, 2, -56, 2, 25, -52, yellow);
            g.CylX(26, -54, 2.2f, -2, 2, yellowDark);
            g.Box(-2, 5, -56, 2, 7, -52, Hazard(1, 7219));
            RivetLine(g, new Vector3Int(-2, 24, -56), new Vector3Int(-2, 3, -56), 3);
            RivetLine(g, new Vector3Int(2, 24, -56), new Vector3Int(2, 3, -56), 3);
            g.Tube(new Vector3(-1, 24, -51), new Vector3(-1, 3, -51), 0.5f, Pal.Ramp(Pal.Black, 0, 7220));
            g.Tube(new Vector3(1, 24, -51), new Vector3(1, 3, -51), 0.5f, Pal.Ramp(Pal.Black, 0, 7220));
            g.Box(-1, 0, -55, 1, 1, -53, Pal.Ramp(Pal.Metal, 2, 7221));

            // ---------------------------------------------------------- claw head + rotator
            g.Use("claw");
            g.Box(-1, -3, -55, 1, -1, -53, black);                                                             // rotator link
            g.CylY(0, -54, 2.4f, -5, -4, iron);                                                                // rotator motor
            g.CylY(0, -54, 4.6f, -7, -6, yellow);                                                              // head
            g.CylY(0, -54, 4.6f, -6, -6, Hazard(0, 7222), 3.8f);
            foreach (int s in new[] { -1, 1 })
            {
                g.Box(s * 2, -5, -55, s * 3, -3, -53, black);                                                  // jaw rams
                g.Set(s * 2, -2, -54, Pal.Solid(Pal.Chrome[2]));
                g.CylZ(s * 3.5f, -7, 1.2f, -57, -51, chrome);                                                  // jaw hinge pins
            }
            // ---------------------------------------------------------- jaws: two tines each, curling in to meet
            foreach (int s in new[] { -1, 1 })
            {
                g.Use(s < 0 ? "jaw_a" : "jaw_b");
                foreach (int z in new[] { -57, -51 })
                {
                    g.Tube(new Vector3(s * 3, -7, z), new Vector3(s * 7, -11, z), 0.8f, tine);
                    g.Tube(new Vector3(s * 7, -11, z), new Vector3(s * 6, -15, z), 0.8f, tine);
                    g.Tube(new Vector3(s * 6, -15, z), new Vector3(s * 2, -17, z), 0.7f, tine);
                    g.Box(s * 1, -18, z, s * 2, -17, z, Pal.Solid(Pal.Chrome[2]));                              // hardened tips
                }
                g.Tube(new Vector3(s * 7, -11, -57), new Vector3(s * 7, -11, -51), 0.7f, Pal.Ramp(Pal.Metal, 1, 7223));   // cross bar
                g.Tube(new Vector3(s * 5, -9, -56), new Vector3(s * 5, -9, -52), 0.5f, Pal.Ramp(Pal.Metal, 1, 7223));
            }

            g.Use("body");
            var p = Make(GrappleCrusher.PartId, PartCategory.Cargo, g, 1900, 4);
            return p.Segment("turret", new Vector3Int(0, 0, 0))
                    .Segment("boom", new Vector3Int(0, 26, -4), "turret")
                    .Segment("stick", new Vector3Int(0, 26, -54), "boom")
                    .Segment("claw", new Vector3Int(0, 0, -54), "stick")
                    .Segment("jaw_a", new Vector3Int(-3, -7, -54), "claw")
                    .Segment("jaw_b", new Vector3Int(3, -7, -54), "claw");
        }
    }
}
