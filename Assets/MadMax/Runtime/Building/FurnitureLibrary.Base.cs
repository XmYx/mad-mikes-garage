using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Base building: foundations and ramps (drivable decks), the garage door and window shutters, defences
    /// (spike wall, barbed wire, auto-turret, alarm bell) and the claim flag. Upgrade chains live here too.</summary>
    public static partial class FurnitureLibrary
    {
        const byte Concrete = (byte)ResourceType.Concrete, Copper = (byte)ResourceType.Copper;

        static IEnumerable<FurnitureDef> BaseDefs()
        {
            var B = BuildCategory.Structure; var Df = BuildCategory.Defence;
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var St = ResourceType.Stone; var C = ResourceType.Cloth; var G = ResourceType.Glass;
            var Fe = ResourceType.Iron; var Cu = ResourceType.Copper; var Co = ResourceType.Concrete;

            var fw = D("foundation_wood", "TIMBER FOUNDATION", B, Foundation(false), 10, true, null, (W, 10)).Deck(1f, 1f, 0.12f, 0.12f);
            fw.foundation = true; yield return fw;
            var fs = D("foundation_stone", "STONE FOUNDATION", B, Foundation(true), 45, true, null, (St, 16), (ResourceType.Lime, 2)).Deck(1f, 1f, 0.12f, 0.12f);
            fs.foundation = true; yield return fs;
            yield return D("ramp_wood", "WOOD RAMP", B, Ramp(false), 10, true, null, (W, 16)).Deck(1f, 2.96f, 0.04f, 1.0f);
            yield return D("ramp_concrete", "CONCRETE RAMP", B, Ramp(true), 45, true, null, (Co, 14)).Deck(1f, 2.96f, 0.04f, 1.0f);
            yield return D("garage_frame", "GARAGE DOORWAY", B, GarageFrame(), 40, true, null, (Co, 8), (Fe, 2));
            yield return D("garage_door", "GARAGE DOOR", B, RollerLeaf(true), 30, false, go =>
            {
                var d = go.AddComponent<RollerDoor>(); d.top = 2.12f; d.watts = 400f;
                var n = go.AddComponent<UtilityNode>(); n.kinds = UtilityKind.Power; n.port = new Vector3(0f, 2.3f, 0.2f);
            }, (Fe, 6), (S, 6), (Cu, 2)).Snap("garage_frame");
            yield return D("shutter", "WINDOW SHUTTER", B, RollerLeaf(false), 8, false, go => go.AddComponent<RollerDoor>().top = 1.8f, (W, 3), (S, 1)).Snap("_window");

            yield return D("spike_wall", "SPIKE WALL", Df, SpikeWall(), 10, false, go => go.AddComponent<DefenceHazard>().kind = DefenceHazard.Kind.Spikes, (W, 6), (Fe, 1));
            yield return D("barbed_wire", "BARBED WIRE", Df, BarbedWire(), 6, false, go => go.AddComponent<DefenceHazard>().kind = DefenceHazard.Kind.Wire, (S, 3), (Fe, 1));
            yield return D("auto_turret", "AUTO TURRET", Df, TurretBase(), 14, false, go =>
            {
                var n = go.AddComponent<UtilityNode>(); n.kinds = UtilityKind.Power; n.port = new Vector3(0f, 0.3f, 0f);
                var c = go.AddComponent<Container>(); c.title = "TURRET AMMO (MG BELTS)"; c.capacity = 12f;
                go.AddComponent<AutoTurret>().head = TurretHead(go);
            }, (Fe, 10), (Cu, 6), (S, 6), (G, 1));
            yield return D("alarm_bell", "ALARM BELL", Df, AlarmBellGrid(), 6, false, go => go.AddComponent<AlarmBell>(), (W, 4), (Cu, 3));
            yield return D("claim_flag", "CLAIM FLAG", Df, ClaimFlagGrid(), 12, false, go => go.AddComponent<ClaimFlag>(), (W, 4), (C, 3), (S, 2));
        }

        /// <summary>Upgrade in place: wood → brick → concrete (and the doors, fences, floors, foundations, ramps).</summary>
        static void Upgrades()
        {
            void U(string from, string to) { var d = defs.Find(x => x.id == from); if (d != null) d.upgrade = to; }
            U("wall_wood", "wall_brick"); U("wall_brick", "wall_concrete");
            U("wall_wood_window", "wall_brick_window");
            U("doorway_wood", "doorway_brick"); U("doorway_brick", "doorway_concrete");
            U("floor_wood", "floor_concrete"); U("foundation_wood", "foundation_stone"); U("ramp_wood", "ramp_concrete");
            U("door_wood", "door_metal"); U("fence_wood", "fence_wire");
        }

        static readonly Color32[] ConcRamp = { Pal.Metal[3], Pal.Chrome[0], Pal.Chrome[1], Pal.Cream[0] };
        static VoxMat PlankMat(int seed) => Pal.Stripe(Pal.Ramp(Pal.Wood, 2, seed), Pal.Ramp(Pal.Wood, 1, seed + 1), 0, 4);
        static VoxMat ConcMat(int seed) => p => (p.x % 12 == 0 || p.z % 12 == 0) ? ConcRamp[0] : Pal.Pick(ConcRamp, p, seed, 2);

        /// <summary>2 × 2 m deck at floor height on 1.9 m legs (timber stilts with braces, or a stone plinth skirt).</summary>
        static VoxelGrid Foundation(bool stone)
        {
            var g = new VoxelGrid();
            if (stone)
            {
                g.Mat(Concrete); g.Box(-12, 0, -12, 12, 1, 12, ConcMat(1701));
                g.Mat(Stone);
                for (int y = -24; y <= -1; y++)
                for (int x = -12; x <= 12; x++)
                for (int z = -12; z <= 12; z++)
                {
                    if (Mathf.Abs(x) != 12 && Mathf.Abs(z) != 12) continue;
                    int course = (y + 24) / 3;
                    bool joint = (y + 24) % 3 == 0 || ((Mathf.Abs(x) == 12 ? z : x) + 24 + course % 2 * 3) % 6 == 0;
                    g.Set(x, y, z, joint ? Pal.Solid(Pal.Cream[0]) : Pal.Ramp(Pal.Sand, 1, 1702));
                }
                return g;
            }
            g.Mat(Wood);
            g.Box(-12, 1, -12, 12, 1, 12, Pal.Stripe(Pal.Ramp(Pal.Wood, 2, 1703), Pal.Ramp(Pal.Wood, 1, 1704), 0, 3));
            g.Box(-12, 0, -12, 12, 0, -11, Pal.Ramp(Pal.Wood, 0, 1705)); g.Box(-12, 0, 11, 12, 0, 12, Pal.Ramp(Pal.Wood, 0, 1705));   // joists
            g.Box(-12, 0, -1, 12, 0, 0, Pal.Ramp(Pal.Wood, 0, 1705));
            foreach (int x in new[] { -11, 0, 11 }) foreach (int z in new[] { -11, 0, 11 }) g.Box(x, -24, z, x + 1, -1, z + 1, Pal.Ramp(Pal.Wood, 1, 1706 + x));
            foreach (int z in new[] { -11, 11 })
            {
                g.Tube(new Vector3(-11, -22, z), new Vector3(0, -2, z), 0.5f, Pal.Ramp(Pal.Wood, 1, 1710));
                g.Tube(new Vector3(11, -22, z), new Vector3(0, -2, z), 0.5f, Pal.Ramp(Pal.Wood, 1, 1711));
            }
            g.Mat(Iron); foreach (int x in new[] { -11, 12 }) foreach (int z in new[] { -11, 12 }) g.Set(x, 1, z, Pal.Solid(Pal.Metal[3]));   // bolt heads
            return g;
        }

        /// <summary>2 m wide, 5.9 m long ramp rising to 1 m (under 10°: vehicles drive up onto foundations).</summary>
        static VoxelGrid Ramp(bool concrete)
        {
            var g = new VoxelGrid().Mat(concrete ? Concrete : Wood);
            for (int z = -36; z <= 36; z++)
            {
                int top = Mathf.RoundToInt((z + 36) * 12f / 72f);
                if (concrete) g.Box(-12, 0, z, 12, top, z, p => p.y == top && (p.z & 3) == 0 ? ConcRamp[0] : Pal.Pick(ConcRamp, p, 1720, 2));
                else
                {
                    g.Box(-12, Mathf.Max(0, top - 1), z, 12, top, z, (z & 3) == 0 ? Pal.Ramp(Pal.Wood, 0, 1721) : PlankMat(1722));
                    foreach (int x in new[] { -12, 12 }) g.Box(x, 0, z, x, top, z, Pal.Ramp(Pal.Wood, 1, 1723));
                    if (z % 12 == 0 && z > -36) g.Box(-11, 0, z, 11, top - 1, z, Pal.Ramp(Pal.Wood, 1, 1724));
                }
            }
            if (concrete) { g.Mat(Iron); g.Box(-12, 0, -36, -11, 0, -36, Pal.Solid(Pal.Ochre[3])); g.Box(11, 0, -36, 12, 0, -36, Pal.Solid(Pal.Ochre[3])); }
            return g;
        }

        /// <summary>4 m wide concrete frame with a 3.3 × 2.1 m opening and the roller drum housing over it.</summary>
        static VoxelGrid GarageFrame()
        {
            var g = new VoxelGrid().Mat(Concrete);
            for (int x = -24; x <= 24; x++)
            for (int y = 0; y <= 29; y++)
            {
                if (Mathf.Abs(x) <= 20 && y <= 26) continue;
                g.Set(x, y, 0, ConcMat(1730)); g.Set(x, y, 1, ConcMat(1730));
            }
            g.Mat(Iron);
            g.Box(-21, 27, 2, 21, 29, 3, Pal.Weathered(Pal.Metal, 0.4f, 1731, 2, 0));
            foreach (int x in new[] { -21, 21 }) g.Box(x, 0, 2, x, 26, 2, p => (p.y / 3 & 1) == 0 ? Pal.Ochre[3] : Pal.Black[1]);   // guide rails, hazard paint
            return g;
        }

        /// <summary>Roller leaf: garage door (3.3 × 2.1 m corrugated steel) or a window shutter (slatted wood).</summary>
        static VoxelGrid RollerLeaf(bool garage)
        {
            var g = new VoxelGrid();
            if (garage)
            {
                g.Mat(Iron);
                var steel = Pal.Weathered(Pal.Metal, 0.3f, 1740, 2, 0);
                g.Box(-20, 0, 2, 20, 26, 2, p => p.y % 3 == 0 ? Pal.Metal[1] : steel(p));
                g.Box(-20, 0, 3, 20, 0, 3, Pal.Solid(Pal.Black[1]));
                g.Box(-2, 3, 3, 2, 3, 3, Pal.Solid(Pal.Chrome[2]));                                   // handle
                return g;
            }
            g.Mat(Wood);
            g.Box(-6, 12, 2, 6, 22, 2, p => p.y % 2 == 0 ? Pal.Wood[1] : Pal.Pick(Pal.Wood, p, 1741, 2));
            g.Mat(Iron); g.Box(-6, 12, 3, 6, 12, 3, Pal.Solid(Pal.Metal[2]));
            return g;
        }

        static VoxelGrid SpikeWall()
        {
            var g = new VoxelGrid().Mat(Wood);
            g.Box(-12, 0, -2, 12, 2, 1, Pal.Ramp(Pal.Wood, 1, 1750));
            for (int x = -11; x <= 11; x += 3)
            {
                float lean = (x & 1) == 0 ? 9f : 7f;
                g.Mat(Wood); g.Tube(new Vector3(x, 1, -1), new Vector3(x, 14, lean - 1), 0.7f, Pal.Ramp(Pal.Wood, 2, 1751 + x));
                g.Mat(Iron); g.Tube(new Vector3(x, 14, lean - 1), new Vector3(x, 17, lean + 1), 0.4f, Pal.Ramp(Pal.Rust, 2, 1752));
            }
            return g;
        }

        static VoxelGrid BarbedWire()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -12, 0, 12 }) g.Box(x, 0, 0, x, 12, 0, Pal.Ramp(Pal.Wood, 1, 1760 + x));
            g.Mat(Iron);
            for (int i = 0; i <= 260; i++)
            {
                float a = i * 0.42f;
                int x = Mathf.RoundToInt(-12 + i * 24f / 260f), y = Mathf.RoundToInt(6 + 5f * Mathf.Sin(a)), z = Mathf.RoundToInt(5f * Mathf.Cos(a));
                g.Set(x, y, z, (i % 7) == 0 ? Pal.Solid(Pal.Chrome[3]) : Pal.Ramp(Pal.Chrome, 1, 1763));
            }
            return g;
        }

        static VoxelGrid TurretBase()
        {
            var g = new VoxelGrid().Mat(Iron);
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f;
                g.Tube(new Vector3(Mathf.Sin(a) * 8f, 0, Mathf.Cos(a) * 8f), new Vector3(0, 9, 0), 0.6f, Pal.Ramp(Pal.Metal, 1, 1770));
            }
            g.CylY(0, 0, 2.5f, 7, 11, Pal.Ramp(Pal.Metal, 2, 1771));
            g.Box(-3, 0, -3, 3, 3, 3, Pal.Weathered(Pal.RigGreen, 0.3f, 1772, 2, 0));                    // power box
            g.Set(0, 2, 4, Pal.Solid(Pal.Ochre[4]));
            return g;
        }

        static Mesh turretHeadMesh;
        static Transform TurretHead(GameObject go)
        {
            if (!turretHeadMesh)
            {
                var g = new VoxelGrid().Mat(Iron);
                g.Box(-3, 0, -4, 3, 5, 4, Pal.Weathered(Pal.RigGreen, 0.35f, 1775, 2, 0));
                foreach (int x in new[] { -2, 2 }) g.CylZ(x, 3, 0.8f, 5, 16, Pal.Ramp(Pal.Black, 2));
                g.Box(4, 0, -3, 6, 3, 2, Pal.Ramp(Pal.Olive, 2, 1776));                                    // ammo can
                g.Mat((byte)ResourceType.Glass); g.Box(-1, 5, 2, 1, 6, 3, Pal.Solid(Pal.Crimson[4]));     // sensor eye
                g.Bevel();
                turretHeadMesh = VoxelMesher.Build(g, "Furniture_turret_head");
            }
            var h = new GameObject("Head", typeof(MeshFilter), typeof(MeshRenderer));
            h.transform.SetParent(go.transform, false);
            h.transform.localPosition = new Vector3(0f, 0.92f, 0f);
            h.GetComponent<MeshFilter>().sharedMesh = turretHeadMesh;
            h.GetComponent<MeshRenderer>().sharedMaterial = go.GetComponent<MeshRenderer>().sharedMaterial;
            return h.transform;
        }

        static VoxelGrid AlarmBellGrid()
        {
            var g = new VoxelGrid().Mat(Wood);
            foreach (int x in new[] { -6, 6 }) g.Box(x, 0, 0, x, 30, 0, Pal.Ramp(Pal.Wood, 1, 1780));
            g.Box(-7, 31, -1, 7, 32, 1, Pal.Ramp(Pal.Wood, 2, 1781));
            g.Mat(Copper);
            for (int y = 22; y <= 30; y++) g.CylY(0, 0, 1.2f + (30 - y) * 0.42f, y, y, Pal.Ramp(Pal.Bronze, y > 27 ? 2 : 1, 1782), y < 23 ? -1f : 0.6f + (30 - y) * 0.42f);
            g.Box(0, 20, 0, 0, 21, 0, Pal.Solid(Pal.Metal[2]));                                             // clapper
            g.Mat(Wood); g.Box(0, 4, 0, 0, 19, 0, Pal.Solid(Pal.Cream[1]));                                   // bell rope
            return g;
        }

        static VoxelGrid ClaimFlagGrid()
        {
            var g = new VoxelGrid().Mat(Stone);
            g.Box(-3, 0, -3, 3, 2, 3, Pal.Ramp(Pal.Sand, 1, 1790));
            g.Mat(Iron); g.Box(0, 3, 0, 0, 60, 0, Pal.Ramp(Pal.Metal, 2, 1791)); g.Set(0, 61, 0, Pal.Solid(Pal.Chrome[3]));
            g.Mat(Cloth);
            for (int x = 1; x <= 20; x++)
            for (int y = 44; y <= 58; y++)
            {
                int wave = Mathf.RoundToInt(Mathf.Sin(x * 0.45f) * 1.2f);
                int cx = x - 11, cy = y - 51;
                bool skull = (cx * cx + cy * cy * 2 < 16 && cy >= -1) || (Mathf.Abs(cx) <= 2 && cy >= -4 && cy < -1 && (cx & 1) == 0);
                bool eye = cy == 1 && (cx == -2 || cx == 2);
                g.Set(x, y, wave, eye ? Pal.Solid(Pal.Black[0]) : skull ? Pal.Solid(Pal.Cream[3]) : Pal.Ramp(Pal.Crimson, 2, 1792));
            }
            return g;
        }
    }
}
