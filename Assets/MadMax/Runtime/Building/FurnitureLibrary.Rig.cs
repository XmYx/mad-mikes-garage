using System.Collections.Generic;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Moving bases (build category VEHICLE, <see cref="RigVehicle"/>): chassis frames S/M/L/XL laid on the
    /// ground (the rig stands on axle stands), frame extensions and outriggers bolted to any side (any scale), axle and
    /// track mounts snapped under the frame (wheel sockets at its sides), an engine bay, a driver's seat, fuel tanks and
    /// suspension units. L/XL frames and the heavy mounts need a welder in the pack. Masses are real-ish kg: everything
    /// built on a base weighs it down (<see cref="MassOf"/> for the other pieces).</summary>
    public static partial class FurnitureLibrary
    {
        public const string RigPrefix = "rig_";
        const float RigCoarse = 0.16f;
        const byte RigIron = (byte)ResourceType.Iron;

        static FurnitureDef RigDef(string id, string name, VoxelGrid g, float vs, int hits, float mass, string desc, System.Action<GameObject> setup, params (ResourceType, int)[] cost)
        {
            g.Bevel();
            return new FurnitureDef
            {
                id = id, name = name, category = BuildCategory.Vehicle, grid = g, cost = cost, hits = hits, voxel = vs, mass = mass, desc = desc,
                mesh = VoxelMesher.Build(g, "Furniture_" + id, vs), setup = setup
            };
        }

        static IEnumerable<FurnitureDef> RigPieces()
        {
            var S = ResourceType.Scrap; var Fe = ResourceType.Iron; var Stl = ResourceType.Steel; var Rb = ResourceType.Rubber; var W = ResourceType.Wood;
            var Cu = ResourceType.Copper; var G = ResourceType.Glass; var C = ResourceType.Cloth;

            // ---- frames: the first one laid on the ground starts a base; the rest bolt on
            yield return RigFrame("rig_frame_S", "RIG FRAME S (2.4 X 4 M)", 14, 24, false, false, 450f, "STARTS A MOVING BASE: LAY IT ON OPEN GROUND, IT STANDS ON AXLE STANDS", (Fe, 20), (S, 20));
            yield return RigFrame("rig_frame_M", "RIG FRAME M (3 X 6 M)", 18, 37, false, false, 950f, "STARTS A MOVING BASE: A TRUCK-SIZED CHEQUER-PLATE DECK", (Fe, 34), (Stl, 8), (S, 30));
            yield return RigFrame("rig_frame_L", "RIG FRAME L (4 X 9 M)", 12, 28, true, false, 2300f, "STARTS A BIG MOVING BASE: PLANK AND STRAP DECK (WELDER)", (Fe, 50), (Stl, 30), (S, 40), (W, 30));
            yield return RigFrame("rig_frame_XL", "RIG FRAME XL (6 X 14 M)", 18, 43, true, false, 5400f, "STARTS A LAND-YACHT: SIX METRES ACROSS (WELDER)", (Fe, 110), (Stl, 70), (S, 80), (W, 70));
            yield return RigFrame("rig_frame_ext_S", "FRAME EXTENSION S (2.4 X 2 M)", 14, 12, false, true, 230f, "BOLTS ONTO ANY SIDE OF A RIG FRAME: LONGER OR WIDER", (Fe, 10), (S, 10));
            yield return RigFrame("rig_frame_ext_M", "FRAME EXTENSION M (3 X 2 M)", 18, 12, false, true, 320f, "BOLTS ONTO ANY SIDE OF A RIG FRAME", (Fe, 12), (Stl, 3), (S, 10));
            yield return RigFrame("rig_frame_ext_L", "FRAME EXTENSION L (4 X 3 M)", 12, 9, true, true, 780f, "BOLTS ONTO ANY SIDE OF A RIG FRAME (WELDER)", (Fe, 18), (Stl, 10), (S, 14), (W, 10));
            yield return RigFrame("rig_frame_ext_XL", "FRAME EXTENSION XL (6 X 4 M)", 18, 12, true, true, 1600f, "BOLTS ONTO ANY SIDE OF A RIG FRAME (WELDER)", (Fe, 36), (Stl, 22), (S, 26), (W, 22));
            yield return RigFrame("rig_outrigger", "OUTRIGGER DECK (1.2 X 2 M)", 7, 12, false, true, 120f, "A NARROW SIDE DECK: WIDEN A RIG A LITTLE", (Fe, 5), (S, 6));

            // ---- running gear mounts (snap under the frame; the wheels go on with the wrench)
            yield return RigMount("rig_axle_S", "AXLE MOUNT S", RigPart.Axle, 0, 2, 140f, "WHEEL PAIR FOR CAR WHEELS (SIZE 1-2)", (Fe, 8), (S, 6), (Rb, 2));
            yield return RigMount("rig_axle_M", "AXLE MOUNT M", RigPart.Axle, 1, 4, 320f, "WHEEL PAIR FOR TRUCK, MONSTER AND EARTHMOVER TYRES", (Fe, 14), (Stl, 4), (S, 8), (Rb, 4));
            yield return RigMount("rig_axle_L", "AXLE MOUNT L", RigPart.Axle, 2, 5, 820f, "HEAVY WHEEL PAIR: TAKES THE HAUL TRUCK TYRE (WELDER)", (Stl, 16), (Fe, 18), (S, 10), (Rb, 8));
            yield return RigMount("rig_track_mount", "TRACK MOUNT", RigPart.Track, 1, 3, 300f, "SPROCKET PAIR: FIT ONE FRONT AND ONE BACK FOR A CRAWLER", (Fe, 14), (Stl, 6), (S, 8));
            yield return RigMount("rig_track_mount_heavy", "HEAVY TRACK MOUNT", RigPart.Track, 2, 5, 950f, "DRUM PAIR FOR THE HEAVY AND CITY CRAWLER TRACKS (WELDER)", (Stl, 24), (Fe, 24), (S, 12));

            // ---- drivetrain, cab and tune
            yield return RigDef("rig_engine_bay", "ENGINE BAY", EngineBay(), VoxelMesher.DefaultSize, 10, 110f, "ENGINE CRADLE AND RADIATOR: MOUNT ANY ENGINE IN IT",
                go => { var p = RigPiece.Add(go, RigPart.EngineBay, 110f); p.maxSize = 4; p.socketAt = new Vector3(0f, 0.16f, 0f); }, (Fe, 10), (Cu, 4), (S, 8), (Rb, 2));
            yield return RigDef("rig_driver_seat", "DRIVER'S SEAT", DriverSeat(), VoxelMesher.DefaultSize, 6, 45f, "WHEEL, PEDALS AND GAUGES: DRIVE THE BASE FROM HERE",
                go => { var p = RigPiece.Add(go, RigPart.Seat, 45f); p.eye = new Vector3(0f, 1.24f, -0.06f); }, (Fe, 4), (S, 6), (C, 3), (G, 1), (Cu, 2));
            yield return RigDef("rig_fuel_tank", "RIG FUEL TANK (200 L)", FuelTank(4, 14, 2501), VoxelMesher.DefaultSize, 8, 70f, "ADDS 200 L TO THE BASE'S TANK",
                go => RigPiece.Add(go, RigPart.Tank, 70f).litres = 200f, (Fe, 8), (S, 8), (Rb, 1));
            yield return RigDef("rig_fuel_tank_big", "RIG FUEL TANK (800 L)", FuelTank(7, 24, 2502), VoxelMesher.DefaultSize, 14, 220f, "ADDS 800 L: A ROAD TRAIN'S RANGE",
                go => RigPiece.Add(go, RigPart.Tank, 220f).litres = 800f, (Fe, 20), (Stl, 4), (S, 16), (Rb, 2));
            yield return RigDef("rig_suspension_light", "COILOVER KIT", Coilovers(), VoxelMesher.DefaultSize, 6, 90f, "SOFT LONG-TRAVEL SPRINGS: FAST AND OFF ROAD",
                go => Suspension(go, 90f, 1, 0.3f, 1.5f, 0.5f, 0.6f), (Stl, 4), (Fe, 4), (Rb, 2));
            yield return RigDef("rig_suspension_heavy", "AIR SUSPENSION", AirBags(), VoxelMesher.DefaultSize, 8, 220f, "AIR BAGS AND A RESERVOIR: CARRIES LOADS LEVEL",
                go => Suspension(go, 220f, 2, 0.34f, 1.3f, 0.62f, 0.9f), (Stl, 6), (Fe, 8), (Rb, 6), (Cu, 2));
            yield return RigDef("rig_suspension_crawler", "WALKING BEAM BOGIE", Bogie(), VoxelMesher.DefaultSize, 10, 400f, "STIFF EQUALISED BOGIES FOR TRACKS AND HUGE LOADS",
                go => Suspension(go, 400f, 3, 0.18f, 2.4f, 0.8f, 1.2f), (Stl, 12), (Fe, 14), (S, 6));
        }

        static void Suspension(GameObject go, float mass, int rank, float travel, float freq, float damp, float roll)
        {
            var p = RigPiece.Add(go, RigPart.Suspension, mass);
            p.rank = rank; p.travel = travel; p.frequency = freq; p.damping = damp; p.antiRoll = roll;
        }

        static FurnitureDef RigFrame(string id, string name, int hx, int hz, bool coarse, bool ext, float mass, string desc, params (ResourceType, int)[] cost)
        {
            float vs = coarse ? RigCoarse : VoxelMesher.DefaultSize;
            int deckY = coarse ? 2 : 4;
            float top = (deckY + 0.5f) * vs;
            var half = new Vector2((hx + 0.5f) * vs, (hz + 0.5f) * vs);
            var d = RigDef(id, name, FrameGrid(hx, hz, coarse, ext, 2700 + id.Length * 37 + hx * 11 + hz), vs, coarse ? 60 : 40, mass, desc,
                go => { var p = RigPiece.Add(go, RigPart.Frame, mass); p.half = half; p.top = top; }, cost);
            d.deck = new Vector4(half.x, half.y, top, top);                                      // other vehicles drive onto it
            return d;
        }

        static FurnitureDef RigMount(string id, string name, RigPart kind, int grade, int maxSize, float mass, string desc, params (ResourceType, int)[] cost) =>
            RigDef(id, name, MountHousing(kind, grade), VoxelMesher.DefaultSize, 12 + grade * 8, mass, desc, go =>
            {
                var p = RigPiece.Add(go, kind, mass);
                p.grade = grade; p.maxSize = maxSize;
            }, cost);

        /// <summary>Building a piece of this id needs a welder in the pack (big frames, heavy mounts).</summary>
        public static bool NeedsWelder(string id) => id != null && id.StartsWith(RigPrefix) && (id.EndsWith("_L") || id.EndsWith("_XL") || id.EndsWith("_heavy") || id == "rig_suspension_crawler");

        // ------------------------------------------------------------------ mass of anything built on a vehicle
        static readonly Dictionary<string, float> massCache = new Dictionary<string, float>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetRigStatics() => massCache.Clear();

        /// <summary>Kilograms of a built piece: its own <see cref="FurnitureDef.mass"/>, else its voxels × the density
        /// of their material (voxels stand for thin boards, sheets and tubes: a fill factor of 0.4).</summary>
        public static float MassOf(FurnitureDef d)
        {
            if (d == null) return 0f;
            if (d.mass > 0f) return d.mass;
            if (massCache.TryGetValue(d.id, out var kg)) return kg;
            if (d.grid != null)
            {
                float v3 = d.voxel * d.voxel * d.voxel * 0.4f;
                kg = 0f;
                foreach (var v in d.grid.voxels.Values) kg += Density((ResourceType)v.mat) * v3;
            }
            else if (d.mesh) { var s = d.mesh.bounds.size; kg = s.x * s.y * s.z * 60f; }
            return massCache[d.id] = Mathf.Clamp(kg, 1f, 50000f);
        }

        static float Density(ResourceType r)
        {
            switch (r)
            {
                case ResourceType.Iron: case ResourceType.Steel: case ResourceType.Copper: case ResourceType.Bronze: return 3000f;
                case ResourceType.Scrap: return 1200f;
                case ResourceType.Aluminium: return 1000f;
                case ResourceType.Stone: case ResourceType.Brick: return 1900f;
                case ResourceType.Concrete: return 2300f;
                case ResourceType.Glass: return 1400f;
                case ResourceType.Wood: case ResourceType.Plank: return 550f;
                case ResourceType.Cloth: case ResourceType.Wool: return 200f;
                case ResourceType.Rubber: return 1100f;
                default: return 700f;
            }
        }

        // ------------------------------------------------------------------ looks
        /// <summary>Rig frame: a box-section perimeter rail and two deep chassis members, cross members, a chequer-plate
        /// deck (coarse frames: plank rows strapped with steel), hazard-striped deck edge, rivets, lightening holes,
        /// tie-down eyes, marker lamps and tow eyes on the end rails. Origin at the bottom centre.</summary>
        static VoxelGrid FrameGrid(int hx, int hz, bool coarse, bool ext, int seed)
        {
            var g = new VoxelGrid().Mat(RigIron);
            int deckY = coarse ? 2 : 4, railTop = deckY - 1;
            var rail = Pal.Weathered(Pal.RigGreen, 0.3f, seed, 3, 0f);
            var web = Pal.Weathered(Pal.Metal, 0.28f, seed + 1, 1, 0f);
            // perimeter box rail
            g.Box(-hx, 0, -hz, -hx, railTop, hz, rail); g.Box(hx, 0, -hz, hx, railTop, hz, rail);
            g.Box(-hx, 0, -hz, hx, railTop, -hz, rail); g.Box(-hx, 0, hz, hx, railTop, hz, rail);
            // two main members (C-channel: web + flanges)
            int ix = Mathf.Max(3, hx * 4 / 9);
            foreach (int sx in new[] { -1, 1 })
            {
                int x0 = sx * ix;
                g.Box(x0, 0, -hz, x0, railTop, hz, web);
                g.Box(x0 - sx, 0, -hz, x0 - sx, 0, hz, Pal.Ramp(Pal.Metal, 0, seed + 2));
            }
            // cross members, braced diagonally between every second pair
            int step = coarse ? 5 : 9;
            int k = 0;
            for (int z = -hz + step; z <= hz - step / 2; z += step, k++)
            {
                g.Box(-hx, coarse ? 0 : 1, z, hx, railTop, z, Pal.Ramp(Pal.Metal, 1, seed + 3));
                if ((k & 1) == 0 && z + step < hz)
                    foreach (int sx in new[] { -1, 1 })
                        g.Tube(new Vector3(sx * ix, 0.5f, z), new Vector3(sx * (hx - 1), 0.5f, z + step), 0.5f, Pal.Ramp(Pal.Rust, 1, seed + 4));
            }
            // lightening holes and rivets along the outer rails
            if (!coarse) for (int z = -hz + 3; z <= hz - 3; z += 6) foreach (int x in new[] { -hx, hx }) g.Set(x, 1, z, Pal.Solid(Pal.Black[0]));
            for (int z = -hz + 1; z < hz; z += coarse ? 2 : 3) foreach (int x in new[] { -hx, hx }) g.Set(x, railTop, z, Pal.Solid(Pal.Chrome[1]));
            // deck
            for (int z = -hz; z <= hz; z++)
            {
                if (!coarse)
                {
                    var plate = Pal.Weathered(Pal.Metal, 0.22f, seed + 5, 2, 0f);
                    g.Mat(RigIron);
                    g.Box(-hx, deckY, z, hx, deckY, z, p => (((p.x + p.z) & 3) == 0 || ((p.x - p.z) & 3) == 0) ? Pal.Metal[3] : plate(p));   // chequer tread
                }
                else
                {
                    int band = ((z + hz) % 7 + 7) % 7;
                    if (band == 0)
                    {
                        g.Mat(RigIron);
                        g.Box(-hx, deckY, z, hx, deckY, z, p => (p.x & 3) == 0 ? Pal.Chrome[1] : Pal.Pick(Pal.Metal, p, seed + 6, 2));   // steel strap, bolted
                    }
                    else
                    {
                        g.Mat((byte)ResourceType.Wood);
                        int row = (z + hz) / 7;
                        g.Box(-hx, deckY, z, hx, deckY, z, p => ((p.x + row * 5) % 11 == 0) ? Pal.Wood[0] : Pal.Pick(Pal.Wood, p, seed + 7 + row, 2 + (row & 1)));   // planks, staggered butt joints
                    }
                }
            }
            // hazard-striped deck edge and tie-down eyes
            g.Mat(RigIron);
            VoxMat hazard = p => (((p.x + p.z) / 2) & 1) == 0 ? Pal.Ochre[3] : Pal.Black[1];
            g.Repaint(-hx, deckY, -hz, hx, deckY, -hz, hazard); g.Repaint(-hx, deckY, hz, hx, deckY, hz, hazard);
            g.Repaint(-hx, deckY, -hz, -hx, deckY, hz, hazard); g.Repaint(hx, deckY, -hz, hx, deckY, hz, hazard);
            int eyeStep = coarse ? 9 : 18;
            for (int z = -hz + 3; z <= hz - 3; z += eyeStep) foreach (int x in new[] { -hx + 1, hx - 1 }) g.Set(x, deckY + 1, z, Pal.Solid(Pal.Chrome[2]));
            // end rails: tow eyes and lamps (white ahead, red and amber behind); amber side markers
            if (!ext)
            {
                foreach (int x in new[] { -ix, ix }) foreach (int z in new[] { -hz, hz }) { g.Set(x, 1, z, Pal.Solid(Pal.Black[0])); g.Set(x, 0, z, Pal.Solid(Pal.Chrome[1])); }
                g.Mat((byte)ResourceType.Glass);
                foreach (int x in new[] { -hx + 2, hx - 2 })
                {
                    g.Set(x, railTop, hz, Pal.Solid(Pal.LightW)); g.Set(x + (x > 0 ? -1 : 1), railTop, hz, Pal.Solid(Pal.LightY));
                    g.Set(x, railTop, -hz, Pal.Solid(Pal.TailR)); g.Set(x + (x > 0 ? -1 : 1), railTop, -hz, Pal.Solid(Pal.Amber));
                }
            }
            g.Mat((byte)ResourceType.Glass);
            for (int z = -hz + 4; z <= hz - 4; z += coarse ? 14 : 28) foreach (int x in new[] { -hx, hx }) g.Set(x, coarse ? 0 : 2, z, Pal.Solid(Pal.Amber));
            return g;
        }

        /// <summary>Centre of an axle mount (diff pumpkin with a cover bolt ring, pinion nose, breather) or a track mount
        /// (gearbox case with a cooling fin stack); origin = frame bottom, it hangs below.</summary>
        static VoxelGrid MountHousing(RigPart kind, int grade)
        {
            var g = new VoxelGrid().Mat(RigIron);
            float r = 2.5f + grade * 1.4f;
            const int cy = 0;                                                                                          // on the axle line (the sockets' height)
            g.Box(-Mathf.CeilToInt(r) - 1, -1, -3, Mathf.CeilToInt(r) + 1, -1, 3, Pal.Ramp(Pal.Metal, 1, 2601));    // pad under the frame
            if (kind == RigPart.Axle)
            {
                var paint = Pal.Weathered(Pal.RigGreen, 0.4f, 2602 + grade, 2, 0f);
                g.CylX(cy, 0, r, -Mathf.CeilToInt(r * 0.7f), Mathf.CeilToInt(r * 0.7f), paint);                        // pumpkin
                g.CylZ(0, cy, r * 0.55f, 0, Mathf.CeilToInt(r) + 3, paint);                                            // pinion nose
                g.CylZ(0, cy, r * 0.35f, Mathf.CeilToInt(r) + 4, Mathf.CeilToInt(r) + 4, Pal.Solid(Pal.Chrome[1]));    // yoke
                int bolts = 8 + grade * 2;
                for (int i = 0; i < bolts; i++)
                {
                    float a = i / (float)bolts * Mathf.PI * 2f;
                    g.Set(Mathf.RoundToInt(Mathf.Cos(a) * r * 0.8f), Mathf.RoundToInt(cy + Mathf.Sin(a) * r * 0.8f), -Mathf.CeilToInt(r) - 1, Pal.Solid(Pal.Chrome[2]));
                }
                g.CylZ(0, cy, r * 0.8f, -Mathf.CeilToInt(r) - 1, -Mathf.CeilToInt(r) - 1, Pal.Ramp(Pal.Metal, 2, 2603), r * 0.6f);   // cover
                g.Set(0, cy + Mathf.CeilToInt(r), 1, Pal.Solid(Pal.Chrome[3]));                                          // breather
                g.Set(0, cy - Mathf.CeilToInt(r * 0.6f), -Mathf.CeilToInt(r), Pal.Solid(Pal.Ochre[3]));                 // drain plug
            }
            else
            {
                var paint = Pal.Weathered(Pal.Ochre, 0.45f, 2604 + grade, 2, 0f);
                int h = Mathf.CeilToInt(r) + 1;
                g.Box(-h, -h, -h, h, -2, h, paint);                                                                    // gearbox case
                for (int x = -h + 1; x <= h - 1; x += 2) g.Box(x, -h - 1, -h + 1, x, -h - 1, h - 1, Pal.Ramp(Pal.Metal, 2, 2605));   // fins
                g.Box(-1, -h + 1, h + 1, 1, -2, h + 2, Pal.Ramp(Pal.Black, 1, 2606));                                  // hydraulic motor
                g.Tube(new Vector3(0, -2, h + 2), new Vector3(0, -1, h + 6), 0.5f, Pal.Solid(Pal.Black[2]));           // hoses
                g.Set(h, -h / 2 - 1, 0, Pal.Solid(Pal.Chrome[2])); g.Set(-h, -h / 2 - 1, 0, Pal.Solid(Pal.Chrome[2]));
            }
            return g;
        }

        /// <summary>Engine cradle (origin = engine mount point, +Z forward): rails with rubber mounts, a firewall with a
        /// fuse box, the radiator and fan shroud ahead, battery box and loom beside it.</summary>
        static VoxelGrid EngineBay()
        {
            var g = new VoxelGrid().Mat(RigIron);
            var frame = Pal.Weathered(Pal.Metal, 0.35f, 2610, 1, 0f);
            foreach (int x in new[] { -7, 6 }) g.Box(x, 0, -2, x + 1, 1, 18, frame);
            g.Box(-7, 0, -2, 7, 0, -1, frame); g.Box(-7, 0, 17, 7, 0, 18, frame);
            foreach (int x in new[] { -6, 5 }) foreach (int z in new[] { 2, 12 }) g.Box(x, 2, z, x + 1, 2, z + 1, Pal.Solid(Pal.Tire[1]));   // rubber mounts
            g.Box(-8, 0, -4, 8, 12, -3, Pal.Weathered(Pal.RigGreen, 0.3f, 2611, 2, 0f));                               // firewall
            g.Box(-6, 7, -2, -3, 10, -2, Pal.Ramp(Pal.Black, 1, 2612)); g.Set(-5, 9, -2, Pal.Solid(Pal.Crimson[3])); g.Set(-4, 9, -2, Pal.Solid(Pal.Ochre[3]));   // fuse box
            for (int y = 0; y <= 13; y++)                                                                               // radiator core
                g.Box(-7, y, 19, 7, y, 20, p => p.x == -7 || p.x == 7 || p.y == 0 || p.y == 13 ? Pal.Chrome[1] : (p.y & 1) == 0 ? Pal.Black[1] : Pal.Metal[1]);
            g.Set(4, 14, 19, Pal.Solid(Pal.Chrome[3]));                                                                 // cap
            g.CylZ(0, 7, 6f, 17, 18, Pal.Ramp(Pal.Black, 1, 2613), 5f);                                               // fan shroud
            g.Box(9, 0, -2, 12, 4, 4, Pal.Ramp(Pal.Black, 1, 2614));                                                   // battery
            g.Set(10, 5, -1, Pal.Solid(Pal.Crimson[3])); g.Set(11, 5, 3, Pal.Solid(Pal.Chrome[2]));
            g.Tube(new Vector3(10, 5, -1), new Vector3(3, 11, -3), 0.4f, Pal.Solid(Pal.Black[0]));                    // loom
            g.Tube(new Vector3(-7, 2, 18), new Vector3(-6, 10, -3), 0.4f, Pal.Solid(Pal.Black[2]));                   // hoses
            return g;
        }

        /// <summary>Driver's seat on a pedestal: bucket seat, steering column and wheel, pedals, a gauge pod, roll hoop.</summary>
        static VoxelGrid DriverSeat()
        {
            var g = new VoxelGrid().Mat(RigIron);
            g.Box(-4, 0, -4, 4, 0, 10, Pal.Ramp(Pal.Metal, 1, 2620));                                                   // floor plate
            g.Box(-2, 1, -3, 2, 4, 1, Pal.Ramp(Pal.Metal, 0, 2621));                                                    // pedestal
            g.Mat((byte)ResourceType.Cloth);
            g.Box(-3, 5, -4, 3, 6, 2, p => (p.x & 1) == 0 ? Pal.Black[2] : Pal.Crimson[1]);                             // cushion (pleated)
            g.Box(-3, 7, -5, 3, 14, -4, p => (p.x & 1) == 0 ? Pal.Black[2] : Pal.Crimson[1]);                           // back
            g.Box(-4, 6, -4, -4, 9, 1, Pal.Ramp(Pal.Black, 2, 2622)); g.Box(4, 6, -4, 4, 9, 1, Pal.Ramp(Pal.Black, 2, 2622));   // bolsters
            g.Mat(RigIron);
            g.Tube(new Vector3(0, 1, 9), new Vector3(0, 11, 6), 0.6f, Pal.Ramp(Pal.Metal, 2, 2623));                   // column
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                g.Set(Mathf.RoundToInt(Mathf.Cos(a) * 3f), 11 + Mathf.RoundToInt(Mathf.Sin(a) * 3f), 6, Pal.Solid(Pal.Black[1]));   // wheel rim
            }
            g.Box(-3, 11, 6, 3, 11, 6, Pal.Solid(Pal.Metal[2])); g.Set(0, 11, 6, Pal.Solid(Pal.Chrome[3]));
            g.Box(-3, 1, 7, -2, 2, 8, Pal.Solid(Pal.Chrome[1])); g.Box(1, 1, 7, 2, 2, 8, Pal.Solid(Pal.Chrome[1]));   // pedals
            g.Box(-4, 12, 8, 4, 15, 9, Pal.Ramp(Pal.Black, 1, 2624));                                                   // gauge pod
            g.Mat((byte)ResourceType.Glass);
            g.Set(-2, 14, 10, Pal.Solid(Pal.LightY)); g.Set(0, 14, 10, Pal.Solid(Pal.Amber)); g.Set(2, 14, 10, Pal.Solid(Pal.LightW)); g.Set(3, 13, 10, Pal.Solid(Pal.TailR));
            g.Mat(RigIron);
            var hoop = Pal.Weathered(Pal.Ochre, 0.3f, 2625, 3, 0f);
            g.Tube(new Vector3(-5, 0, -7), new Vector3(-5, 20, -7), 0.7f, hoop); g.Tube(new Vector3(5, 0, -7), new Vector3(5, 20, -7), 0.7f, hoop);
            g.Tube(new Vector3(-5, 20, -7), new Vector3(5, 20, -7), 0.7f, hoop);                                         // roll hoop
            return g;
        }

        /// <summary>Horizontal tank on saddles with straps, filler neck and cap, drain cock, a painted band.</summary>
        static VoxelGrid FuelTank(int r, int len, int seed)
        {
            var g = new VoxelGrid().Mat(RigIron);
            int cy = r + 1;
            g.CylZ(0, cy, r + 0.4f, -len / 2, len / 2, p => Mathf.Abs(p.z) < 2 ? Pal.Crimson[2] : Pal.Weathered(Pal.Ochre, 0.35f, seed, 2, 2f)(p));
            foreach (int z in new[] { -len / 2 + 3, len / 2 - 3 })
            {
                g.Box(-r, 0, z - 1, r, 1, z + 1, Pal.Ramp(Pal.Metal, 1, seed + 1));                                     // saddle
                g.CylZ(0, cy, r + 1.1f, z, z, Pal.Solid(Pal.Black[1]), r + 0.4f);                                        // strap
            }
            g.CylY(0, len / 2 - 4, 1.2f, cy + r, cy + r + 2, Pal.Ramp(Pal.Chrome, 1, seed + 2));                         // filler neck
            g.CylY(0, len / 2 - 4, 1.6f, cy + r + 3, cy + r + 3, Pal.Solid(Pal.Crimson[3]));                             // cap
            g.Set(0, 1, -len / 2 + 1, Pal.Solid(Pal.Chrome[2]));                                                          // drain cock
            return g;
        }

        /// <summary>Two coil-over dampers (red coils over chrome shafts) on a bracket plate.</summary>
        static VoxelGrid Coilovers()
        {
            var g = new VoxelGrid().Mat(RigIron);
            g.Box(-7, 0, -3, 7, 0, 3, Pal.Ramp(Pal.Metal, 1, 2630));
            foreach (int x in new[] { -4, 4 })
            {
                g.CylY(x, 0, 0.8f, 1, 12, Pal.Solid(Pal.Chrome[2]));
                for (int y = 3; y <= 10; y++) g.CylY(x, 0, 2.2f, y, y, Pal.Solid((y & 1) == 0 ? Pal.Crimson[3] : Pal.Crimson[1]), 1.4f);
                g.CylY(x, 0, 2.4f, 2, 2, Pal.Solid(Pal.Ochre[3])); g.CylY(x, 0, 2.4f, 11, 11, Pal.Solid(Pal.Ochre[3]));   // spring seats
                g.Box(x - 1, 12, -1, x + 1, 13, 1, Pal.Ramp(Pal.Metal, 2, 2631));
            }
            return g;
        }

        /// <summary>Air bags (stacked rubber convolutions), a reservoir and a valve block with lines.</summary>
        static VoxelGrid AirBags()
        {
            var g = new VoxelGrid().Mat(RigIron);
            g.Box(-9, 0, -4, 9, 0, 4, Pal.Ramp(Pal.Metal, 1, 2640));
            g.Mat((byte)ResourceType.Rubber);
            foreach (int x in new[] { -5, 5 })
                for (int y = 1; y <= 9; y++) g.CylY(x, 0, (y % 3 == 2) ? 3.4f : 2.7f, y, y, Pal.Solid(Pal.Tire[(y % 3 == 2) ? 2 : 1]));
            g.Mat(RigIron);
            foreach (int x in new[] { -5, 5 }) g.CylY(x, 0, 3.4f, 10, 10, Pal.Ramp(Pal.Metal, 2, 2641));
            g.CylX(1, -3, 1.6f, -8, 8, Pal.Weathered(Pal.Navy, 0.25f, 2642, 3, 0f));                                        // reservoir
            g.Box(-1, 1, 2, 1, 3, 3, Pal.Ramp(Pal.Chrome, 1, 2643));                                                         // valve block
            g.Tube(new Vector3(0, 3, 3), new Vector3(-5, 8, 2), 0.4f, Pal.Solid(Pal.Black[2])); g.Tube(new Vector3(0, 3, 3), new Vector3(5, 8, 2), 0.4f, Pal.Solid(Pal.Black[2]));
            return g;
        }

        /// <summary>Walking-beam bogie: an equaliser beam on a centre pivot boss carrying two solid road wheels.</summary>
        static VoxelGrid Bogie()
        {
            var g = new VoxelGrid().Mat(RigIron);
            var paint = Pal.Weathered(Pal.Ochre, 0.45f, 2650, 2, 0f);
            g.Box(-2, 0, -2, 2, 6, 2, Pal.Ramp(Pal.Metal, 1, 2651));                                                     // pedestal
            g.CylX(7, 0, 2.6f, -3, 3, Pal.Ramp(Pal.Metal, 2, 2652));                                                     // pivot boss
            g.CylX(7, 0, 1f, -4, 4, Pal.Solid(Pal.Chrome[2]));
            g.Box(-1, 5, -14, 1, 8, 14, paint);                                                                           // equaliser
            foreach (int z in new[] { -12, 12 })
            {
                g.CylX(3, z, 3.5f, -4, -2, p => (p.y + p.z) % 3 == 0 ? Pal.Tire[2] : Pal.Tire[1]);                         // road wheels
                g.CylX(3, z, 3.5f, 2, 4, p => (p.y + p.z) % 3 == 0 ? Pal.Tire[2] : Pal.Tire[1]);
                g.CylX(3, z, 1.2f, -5, 5, Pal.Solid(Pal.Chrome[1]));
            }
            return g;
        }
    }
}
