using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Vehicle parts for depth stage D (metalworking and part depth): two exhausts, two radiators, three lamps,
    /// three armour pieces, a ducktail spoiler and a forged V8 from the machine shop. Stats live in <c>ArmorStats</c>
    /// (armour) and <c>MetalPartFunctions</c> (lamp beams, exhaust outlets, radiator cooling); recipes in
    /// <c>RecipeLibrary.Metal</c> (forge, machine shop, garage).</summary>
    public static partial class PartLibrary
    {
        static IEnumerable<PartDesign> MetalParts()
        {
            yield return TwinChromePipes();
            yield return FlameSpitterStack();
            yield return BigCoreRadiator();
            yield return OilCoolerRadiator();
            yield return LedBar();
            yield return FogPods();
            yield return SearchPod();
            yield return WindowMesh();
            yield return SpikedSkirts();
            yield return SlopedPlate();
            yield return Ducktail();
            yield return ForgedV8();
        }

        // ---------------------------------------------------------------- exhausts
        /// <summary>Twin polished tailpipes (right side): a header into a muffler can under the sill, then two chrome
        /// pipes rearwards ending in rolled tips (outlets at x 0 and 3, y 1, z -17).</summary>
        public static PartDesign TwinChromePipes()
        {
            var g = new VoxelGrid();
            var chrome = Pal.Weathered(Pal.Chrome, 0.1f, 4101, 2, -10);
            g.Box(-1, 0, 0, 0, 1, 1, Pal.Ramp(Pal.Rust, 1));                                                     // header flange into the body
            g.Tube(new Vector3(0, 1, 0), new Vector3(1, 1, -2), 0.8f, Pal.Ramp(Pal.Metal, 1, 4102));              // down pipe
            g.CylZ(1.5f, 1, 2f, -9, -2, p => p.z == -2 || p.z == -9 ? Pal.Chrome[1] : Pal.Pick(Pal.Black, p, 4103, 2));   // muffler can, chrome end caps
            g.Box(0, 3, -6, 0, 4, -5, Pal.Ramp(Pal.Metal, 0));                                                   // hanger to the sill
            foreach (int x in new[] { 0, 3 })
            {
                g.CylZ(x, 1, 1f, -15, -10, chrome);                                                              // tailpipe
                g.CylZ(x, 1, 1.5f, -17, -16, Pal.Solid(Pal.Chrome[3]), 0.6f);                                    // rolled tip
                g.Set(x, 1, -16, Pal.Solid(Pal.Void));                                                           // the dark bore
            }
            return Make("exhaust_twin_chrome", PartCategory.Exhaust, g, 16);
        }

        /// <summary>Zoomie stack (right side): out from the sill, up and raked back to a flared, sooty bell at y 13 that
        /// spits flames on the overrun (<c>FlameStack</c>). Heat-blued above the perforated shield.</summary>
        public static PartDesign FlameSpitterStack()
        {
            var g = new VoxelGrid();
            var pipe = Pal.Weathered(Pal.Chrome, 0.2f, 4111, 1, -10);
            g.Box(-1, 0, -1, 0, 1, 1, Pal.Ramp(Pal.Rust, 2));                                                    // flange
            g.Tube(new Vector3(0, 0.5f, 0), new Vector3(2, 2, 0), 0.9f, pipe);                                    // out of the body
            g.Tube(new Vector3(2, 2, 0), new Vector3(2, 10, -1.5f), 0.9f, pipe);                                  // up, raked back
            g.Recolor(p => p.y >= 7, p => (p.y + p.z) % 3 == 0 ? Pal.Navy[3] : Pal.Pick(Pal.Chrome, p, 4112, 1));  // heat bluing
            g.CylY(2, -0.5f, 1.6f, 3, 6, p => (p.y + p.z) % 2 == 0 ? Pal.Chrome[1] : Pal.Void, 0.95f);          // perforated heat shield
            g.CylY(2, -1.5f, 1.9f, 11, 13, p => p.y == 13 ? Pal.Black[1] : Pal.Chrome[2], 0.9f);                 // flared bell, sooty lip
            g.Set(2, 11, -1, Pal.Solid(Pal.Void)); g.Set(2, 11, -2, Pal.Solid(Pal.Void));                        // throat
            g.Box(0, 5, -1, 1, 5, 0, Pal.Ramp(Pal.Metal, 0));                                                    // stay to the body
            return Make("exhaust_flame_stack", PartCategory.Exhaust, g, 14);
        }

        // ---------------------------------------------------------------- radiators
        /// <summary>Two-row copper core with twin electric fans: half again the cooling of a stock radiator.</summary>
        public static PartDesign BigCoreRadiator()
        {
            var g = new VoxelGrid();
            const int hx = 8, h = 8;
            g.Box(-hx, 0, 0, hx, h, 0, Pal.Stripe(Pal.Solid(Pal.Bronze[1]), Pal.Solid(Pal.Bronze[3]), 1, 2));          // front row of fins
            g.Box(-hx, 0, -1, hx, h, -1, Pal.Stripe(Pal.Solid(Pal.Bronze[0]), Pal.Solid(Pal.Bronze[2]), 0, 2));         // second row, crossways
            g.Box(-hx, h + 1, -1, hx, h + 2, 0, Pal.Weathered(Pal.Chrome, 0.25f, 4121, 1, 0));                       // top tank
            g.Box(-hx, -1, -1, hx, -1, 0, Pal.Ramp(Pal.Metal, 1, 4122));                                              // bottom tank
            g.Box(-hx - 1, -1, -1, -hx - 1, h + 2, 0, Pal.Ramp(Pal.Metal, 1)); g.Box(hx + 1, -1, -1, hx + 1, h + 2, 0, Pal.Ramp(Pal.Metal, 1));
            g.Set(-3, h + 3, 0, Pal.Solid(Pal.Chrome[3]));                                                           // cap
            foreach (int x in new[] { -4, 4 })
            {
                g.CylZ(x, 4, 3.4f, -2, -2, Pal.Ramp(Pal.Black, 1, 4123), 2.6f);                                     // fan shroud
                for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    if (dx == 0 || dy == 0 || dx == dy) g.Set(x + dx, 4 + dy, -3, Pal.Solid(dx == 0 && dy == 0 ? Pal.Chrome[2] : Pal.Black[2]));   // blades and hub
            }
            g.Box(-3, h, -2, -3, h, -5, Pal.Ramp(Pal.Black, 1)); g.Box(3, 0, -2, 3, 0, -5, Pal.Ramp(Pal.Black, 1));    // hoses
            return Make("radiator_bigcore", PartCategory.Radiator, g, 34, 2);
        }

        /// <summary>Car-size radiator with a finned oil cooler slung in front and braided lines: cools a third better and
        /// the engine oil lasts nearly twice as long.</summary>
        public static PartDesign OilCoolerRadiator()
        {
            var g = new VoxelGrid();
            const int hx = 7, h = 5;
            g.Box(-hx, 0, 0, hx, h, 0, Pal.Stripe(Pal.Solid(Pal.Metal[0]), Pal.Solid(Pal.Metal[2]), 1, 2));            // coolant core
            g.Box(-hx, h + 1, 0, hx, h + 1, 0, Pal.Weathered(Pal.Chrome, 0.3f, 4131, 1, 0));                         // header tank
            g.Box(-hx - 1, 0, 0, -hx - 1, h + 1, 0, Pal.Ramp(Pal.Metal, 1)); g.Box(hx + 1, 0, 0, hx + 1, h + 1, 0, Pal.Ramp(Pal.Metal, 1));
            g.Set(-2, h + 2, 0, Pal.Solid(Pal.Chrome[2]));                                                           // cap
            g.Box(-5, -3, 1, 5, -2, 1, Pal.Stripe(Pal.Solid(Pal.Black[1]), Pal.Solid(Pal.Chrome[1]), 0, 2));          // oil cooler, vertical fins
            foreach (int x in new[] { -6, 6 }) g.Box(x, -3, 0, x, -1, 1, Pal.Ramp(Pal.Metal, 0));                    // brackets
            var braid = Pal.Stripe(Pal.Solid(Pal.Chrome[1]), Pal.Solid(Pal.Black[2]), 2, 2);
            g.Tube(new Vector3(5, -2, 1), new Vector3(8, 1, -3), 0.5f, braid);                                       // braided oil lines
            g.Tube(new Vector3(-5, -2, 1), new Vector3(-8, 1, -3), 0.5f, braid);
            g.Box(-3, h, -1, -3, h, -3, Pal.Ramp(Pal.Black, 1)); g.Box(3, 0, -1, 3, 0, -3, Pal.Ramp(Pal.Black, 1));    // hoses
            return Make("radiator_oil_cooler", PartCategory.Radiator, g, 22, 1);
        }

        // ---------------------------------------------------------------- lamps
        /// <summary>Slim black extrusion of LED cells with heat-sink fins: four wide floods.</summary>
        public static PartDesign LedBar()
        {
            var g = new VoxelGrid();
            foreach (int x in new[] { -10, 10 }) g.Box(x, 0, 0, x, 2, 0, Pal.Ramp(Pal.Metal, 1, 4141));                // feet
            g.Box(-12, 3, -1, 12, 4, 0, Pal.Ramp(Pal.Black, 1, 4142));                                                 // extrusion
            g.Box(-11, 3, 1, 11, 4, 1, p => p.x % 5 == 0 ? Pal.Black[2] : Pal.LightW);                                   // LED cells
            g.Box(-12, 5, -1, 12, 5, -1, Pal.Stripe(Pal.Solid(Pal.Black[0]), Pal.Solid(Pal.Black[2]), 0, 2));           // fins
            return Make("lights_led_bar", PartCategory.Lights, g, 9, 1);
        }

        /// <summary>A pair of round rally pods with amber lenses, chrome bezels and stone guards on a short bar.</summary>
        public static PartDesign FogPods()
        {
            var g = new VoxelGrid();
            g.Box(-8, 2, 0, 8, 2, 0, Pal.Ramp(Pal.Metal, 1, 4151));                                                   // bar
            foreach (int x in new[] { -5, 5 }) g.Box(x, 0, 0, x, 1, 0, Pal.Ramp(Pal.Metal, 1));                        // feet
            foreach (int x in new[] { -6, 6 })
            {
                g.CylZ(x, 5, 2.3f, -2, 1, Pal.Weathered(Pal.Black, 0.2f, 4152 + x, 1, 0));                             // pod
                g.CylZ(x, 5, 1.7f, 2, 2, p => (p.x - x) * (p.x - x) + (p.y - 5) * (p.y - 5) <= 1 ? Pal.LightW : Pal.LightY);   // amber lens, hot centre
                g.CylZ(x, 5, 2.3f, 2, 2, Pal.Solid(Pal.Chrome[2]), 1.7f);                                             // bezel
                g.Box(x, 3, 3, x, 7, 3, Pal.Solid(Pal.Black[1])); g.Box(x - 2, 5, 3, x + 2, 5, 3, Pal.Solid(Pal.Black[1]));   // stone guard
                g.Box(x, 3, 0, x, 3, 0, Pal.Ramp(Pal.Metal, 2));                                                      // stem
            }
            return Make("lights_fog", PartCategory.Lights, g, 8, 1);
        }

        /// <summary>Armoured searchlight pod on a turntable: the "lamp" segment follows the driver's aim; finned back,
        /// big lens, carry handle.</summary>
        public static PartDesign SearchPod()
        {
            var g = new VoxelGrid();
            g.CylY(0, 0, 2.4f, 0, 1, Pal.Ramp(Pal.Metal, 1, 4161));                                                   // turntable
            g.Use("lamp");
            g.Box(-1, 2, -1, 1, 2, 1, Pal.Ramp(Pal.Metal, 2, 4162));                                                   // yoke
            g.Box(-3, 3, -4, 3, 8, 2, Pal.Weathered(Pal.RigGreen, 0.25f, 4163, 2, 0));                                 // pod
            g.Box(-3, 3, -5, 3, 8, -5, Pal.Stripe(Pal.Solid(Pal.Black[0]), Pal.Solid(Pal.Black[2]), 1, 2));            // cooling fins
            g.Box(-3, 3, 3, 3, 8, 3, Pal.Solid(Pal.Chrome[1]));                                                        // bezel
            g.Box(-2, 4, 3, 2, 7, 3, p => (p.x + p.y) % 3 == 0 ? Pal.Cream[4] : Pal.LightW);                          // lens
            g.Box(-2, 9, -1, -2, 10, -1, Pal.Ramp(Pal.Black, 1)); g.Box(2, 9, -1, 2, 10, -1, Pal.Ramp(Pal.Black, 1));
            g.Box(-2, 10, -1, 2, 10, -1, Pal.Ramp(Pal.Black, 1));                                                     // handle
            g.Use("body");
            return Make("lights_search_pod", PartCategory.Lights, g, 30, 1).Segment("lamp", new Vector3Int(0, 2, 0));
        }

        // ---------------------------------------------------------------- armour (right side, +X out, length along Z)
        /// <summary>Welded diamond mesh over the side windows on three clamp posts: stops rocks and hands, not bullets.</summary>
        public static PartDesign WindowMesh()
        {
            var g = new VoxelGrid();
            var frame = Pal.Weathered(Pal.Metal, 0.35f, 4171, 2, 0);
            foreach (int z in new[] { -12, 0, 12 }) g.Box(0, 0, z, 1, 4, z, frame);                                    // clamp posts
            for (int y = 5; y <= 12; y++)
            for (int z = -13; z <= 13; z++)
            {
                bool edge = y == 5 || y == 12 || z == -13 || z == 13 || z == 0;
                bool wire = ((y + z) % 3 + 3) % 3 == 0 || ((y - z) % 3 + 3) % 3 == 0;
                if (edge) g.Set(0, y, z, frame);
                else if (wire) g.Set(0, y, z, Pal.Ramp(Pal.Chrome, 0, 4172));
            }
            return Make("armor_window_mesh", PartCategory.Armor, g, 22, 1);
        }

        /// <summary>Riveted skirt along the sill with a serrated edge and spikes angled out and down.</summary>
        public static PartDesign SpikedSkirts()
        {
            var g = new VoxelGrid();
            g.Box(0, -3, -12, 1, 1, 12, Pal.Weathered(Pal.Rust, 0.3f, 4181, 2, 20));                                  // skirt plate (clear of the tyres)
            for (int z = -12; z <= 12; z += 2) g.Box(0, -4, z, 1, -4, z, Pal.Ramp(Pal.Rust, 1));                        // serrated edge
            for (int z = -10; z <= 10; z += 4) { g.Set(2, 0, z, Pal.Solid(Pal.Chrome[1])); g.Set(2, -2, z, Pal.Solid(Pal.Chrome[1])); }   // rivets
            for (int z = -9; z <= 9; z += 3)
                g.Tube(new Vector3(2, -1, z), new Vector3(6, -2, z), 0.55f, p => p.x >= 5 ? Pal.Chrome[3] : p.x >= 4 ? Pal.Chrome[1] : Pal.Metal[2]);   // spikes
            return Make("armor_skirt_spiked", PartCategory.Armor, g, 42, 2);
        }

        /// <summary>Steel plate leaning in towards the top (bullets glance off), chamfered ends, weld seams and bolts.</summary>
        public static PartDesign SlopedPlate()
        {
            var g = new VoxelGrid();
            var steel = Pal.Weathered(Pal.Metal, 0.2f, 4191, 3, 0);
            foreach (int z in new[] { -10, 0, 10 }) g.Box(0, 0, z, 2, 1, z, Pal.Ramp(Pal.Metal, 1));                   // brackets
            for (int y = -2; y <= 9; y++)
            {
                int x0 = 3 - (y + 2) / 3, zl = 16 - Mathf.Max(0, y - 6);
                g.Box(x0, y, -zl, x0 + 1, y, zl, p => p.z % 11 == 0 ? Pal.Rust[1] : steel(p));                          // plate, weld seams
            }
            foreach (int y in new[] { 0, 6 })
                for (int z = -12; z <= 12; z += 6) g.Set(3 - (y + 2) / 3 + 2, y, z, Pal.Solid(Pal.Chrome[2]));          // bolt heads
            return Make("armor_sloped", PartCategory.Armor, g, 75, 2);
        }

        // ---------------------------------------------------------------- body
        /// <summary>Low kicked-up lip on the boot lid with a chrome trim strip.</summary>
        public static PartDesign Ducktail()
        {
            var g = new VoxelGrid();
            var paint = Pal.Weathered(Pal.Black, 0.2f, 4201, 1, -10);
            g.Box(-11, 0, -1, 11, 0, 1, paint);                                                                       // base
            g.Box(-11, 1, -2, 11, 1, -1, paint);                                                                      // sweep
            g.Box(-11, 2, -3, 11, 2, -3, paint);                                                                      // lip
            g.Box(-10, 0, 2, 10, 0, 2, Pal.Solid(Pal.Chrome[1]));                                                     // trim
            return Make("spoiler_ducktail", PartCategory.Spoiler, g, 7);
        }

        // ---------------------------------------------------------------- engine
        /// <summary>Forged-steel V8 from the machine shop: a cast block, billet cam covers, eight velocity stacks and
        /// tubular headers. Revs higher than the blown V8, less torque, no blower whine.</summary>
        public static PartDesign ForgedV8()
        {
            var g = new VoxelGrid();
            var steel = Pal.Weathered(Pal.Metal, 0.06f, 4211, 2, 2);
            var polish = Pal.Weathered(Pal.Chrome, 0.04f, 4212, 2, -6);
            g.Box(-5, 0, 0, 5, 4, 10, steel);                                                                         // block
            g.Box(-6, 3, 1, -4, 5, 9, polish); g.Box(4, 3, 1, 6, 5, 9, polish);                                       // billet cam covers
            for (int z = 2; z <= 8; z += 2) { g.Set(-7, 4, z, Pal.Solid(Pal.Crimson[3])); g.Set(7, 4, z, Pal.Solid(Pal.Crimson[3])); }   // plug leads
            for (int z = 1; z <= 9; z += 3)
            {
                g.Tube(new Vector3(-6, 2, z), new Vector3(-8, 0, z), 0.5f, Pal.Ramp(Pal.Rust, 2, 4213));              // headers
                g.Tube(new Vector3(6, 2, z), new Vector3(8, 0, z), 0.5f, Pal.Ramp(Pal.Rust, 2, 4214));
            }
            g.Box(-3, 5, 1, 3, 5, 9, steel);                                                                          // plenum
            foreach (int x in new[] { -2, 2 })
                for (int z = 2; z <= 8; z += 2) { g.CylY(x, z, 0.9f, 6, 8, polish); g.Set(x, 8, z, Pal.Solid(Pal.Void)); }   // velocity stacks
            g.CylZ(0, 2, 1.6f, 11, 11, Pal.Solid(Pal.Black[1])); g.Set(0, 2, 12, Pal.Solid(Pal.Chrome[3]));           // pulley
            var d = Make("engine_v8_forged", PartCategory.Engine, g, 235, 2);
            d.torque = 560f; d.maxRpm = 7800f; d.peakAt = 0.68f;
            return d;
        }
    }
}
