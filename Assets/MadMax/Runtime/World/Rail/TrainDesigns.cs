using System.Collections.Generic;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    public enum CarKind { Draisine, MakeshiftLoco, ArmoredLoco, ShackWagon, Hopper, Flatcar, WreckFlat, GunWagon, Caboose }

    /// <summary>A rolling stock type: size (local origin = rail-top centre between the bogies, +Z forward), mass,
    /// traction, the deck riders stand on, colliders, the cab seat, the turret mount, what can be searched.</summary>
    public sealed class CarDef
    {
        public CarKind kind;
        public string id, name;
        public float length, bogie, mass, tractive, vmax, deckY, hp = 400f;
        public Vector2 deckHalf;                       // flat walkable/drivable top (x, z half extents); 0 = none
        public bool cab, turret, hand, carDeck;
        public Vector3 seat, turretAt, lever;
        public string loot, lootTitle;
        public int skill;                              // Rail skill needed to drive it well
        public readonly List<(Vector3 c, Vector3 size)> boxes = new List<(Vector3, Vector3)>();
        public float Half => length * 0.5f;
    }

    /// <summary>Rolling stock of the wastes: a hand-pumped draisine, a patched makeshift diesel (mismatched panels, a
    /// rail-scrap cowcatcher), a riveted armoured locomotive with a plough, shack wagons people live in, open hoppers of
    /// scrap and ore, flatcars (cars drive on and ride strapped), flatcars chained with wrecks, gun wagons with a
    /// turret, a caboose with a cupola. Meshes are <see cref="RailKit"/> geometry built once per kind.</summary>
    public static class TrainDesigns
    {
        static readonly Dictionary<CarKind, CarDef> defs = new Dictionary<CarKind, CarDef>();
        static readonly Color32[] Olive = Pal.Olive, Cream = Pal.Cream, Faded = Pal.PaleBlue, Ochre = Pal.Ochre, Plate = Pal.RigGreen, Sheet = { Pal.Metal[2], Pal.Metal[3], Pal.Rust[2] };

        public static CarDef Get(CarKind k)
        {
            if (defs.Count == 0) Define();
            return defs[k];
        }

        public static IEnumerable<CarDef> All { get { if (defs.Count == 0) Define(); return defs.Values; } }

        static CarDef D(CarKind k, string id, string name, float len, float bogie, float mass)
        {
            var d = new CarDef { kind = k, id = id, name = name, length = len, bogie = bogie, mass = mass };
            defs[k] = d;
            return d;
        }

        static void Define()
        {
            var d = D(CarKind.Draisine, "draisine", "DRAISINE", 3.2f, 0.9f, 380f);
            d.hand = true; d.cab = true; d.tractive = 2600f; d.vmax = 7.5f; d.deckY = 0.62f; d.deckHalf = new Vector2(0.95f, 1.5f);
            d.seat = new Vector3(0f, 1.03f, -0.9f); d.lever = new Vector3(0f, 1.1f, 0.2f); d.hp = 120f;
            d.boxes.Add((new Vector3(0f, 0.55f, 0f), new Vector3(1.9f, 0.14f, 3.2f)));

            d = D(CarKind.MakeshiftLoco, "loco_makeshift", "MAKESHIFT LOCO", 11f, 3.4f, 32000f);
            d.cab = true; d.tractive = 110000f; d.vmax = 16f; d.deckY = 1.05f; d.seat = new Vector3(-0.45f, 2.42f, -2.6f); d.skill = 1; d.hp = 900f;
            d.boxes.Add((new Vector3(0f, 0.95f, 0f), new Vector3(2.7f, 0.3f, 11f)));
            d.boxes.Add((new Vector3(0f, 2.1f, 2.1f), new Vector3(1.9f, 2f, 5.6f)));
            d.boxes.Add((new Vector3(0f, 1.5f, -2.6f), new Vector3(2.7f, 0.9f, 3.1f)));
            d.boxes.Add((new Vector3(0f, 3.0f, -4.08f), new Vector3(2.6f, 2.1f, 0.1f)));
            d.boxes.Add((new Vector3(0f, 4.12f, -2.6f), new Vector3(2.9f, 0.16f, 3.5f)));

            d = D(CarKind.ArmoredLoco, "loco_armoured", "ARMOURED LOCO", 13f, 4f, 64000f);
            d.cab = true; d.tractive = 170000f; d.vmax = 13f; d.deckY = 1.1f; d.seat = new Vector3(0f, 2.0f, -3.4f); d.skill = 2; d.hp = 3200f;
            d.boxes.Add((new Vector3(0f, 2.3f, 0f), new Vector3(2.9f, 3.2f, 13f)));

            d = D(CarKind.ShackWagon, "wagon_shack", "SHACK WAGON", 12f, 4f, 14000f);
            d.deckY = 1.0f; d.deckHalf = new Vector2(1.35f, 1.6f); d.loot = "house"; d.lootTitle = "SHACK CUPBOARD";
            d.boxes.Add((new Vector3(0f, 0.9f, 0f), new Vector3(2.8f, 0.2f, 12f)));
            d.boxes.Add((new Vector3(0f, 2.3f, -0.9f), new Vector3(2.7f, 2.6f, 8.2f)));

            d = D(CarKind.Hopper, "wagon_hopper", "OPEN HOPPER", 10f, 3.4f, 22000f);
            d.deckY = 2.4f; d.loot = "rail_hopper"; d.lootTitle = "HOPPER LOAD";
            d.boxes.Add((new Vector3(0f, 1.6f, 0f), new Vector3(2.8f, 2.2f, 10f)));

            d = D(CarKind.Flatcar, "wagon_flat", "FLATCAR", 12f, 4.2f, 10000f);
            d.deckY = 1.0f; d.deckHalf = new Vector2(1.38f, 5.95f); d.carDeck = true;
            d.boxes.Add((new Vector3(0f, 0.9f, 0f), new Vector3(2.8f, 0.2f, 12f)));

            d = D(CarKind.WreckFlat, "wagon_wreck", "WRECK FLAT", 12f, 4.2f, 11500f);
            d.deckY = 1.0f; d.deckHalf = new Vector2(1.38f, 1.4f); d.loot = "rail_wreck"; d.lootTitle = "CHAINED WRECK";
            d.boxes.Add((new Vector3(0f, 0.9f, 0f), new Vector3(2.8f, 0.2f, 12f)));
            d.boxes.Add((new Vector3(0f, 1.75f, 1f), new Vector3(1.9f, 1.5f, 4.6f)));

            d = D(CarKind.GunWagon, "wagon_gun", "GUN WAGON", 11f, 3.6f, 34000f);
            d.turret = true; d.turretAt = new Vector3(0f, 3.15f, 0f); d.deckY = 1.0f; d.hp = 1800f; d.loot = "rail_armoury"; d.lootTitle = "AMMO LOCKER";
            d.boxes.Add((new Vector3(0f, 2.0f, 0f), new Vector3(2.9f, 2.4f, 11f)));

            d = D(CarKind.Caboose, "wagon_caboose", "CABOOSE", 8f, 2.6f, 9000f);
            d.deckY = 1.0f; d.deckHalf = new Vector2(1.3f, 3.8f); d.loot = "rail_office"; d.lootTitle = "CONDUCTOR'S DESK";
            d.boxes.Add((new Vector3(0f, 0.9f, 0f), new Vector3(2.7f, 0.2f, 8f)));
            d.boxes.Add((new Vector3(0f, 2.3f, 0f), new Vector3(2.6f, 2.6f, 5f)));
        }

        // ------------------------------------------------------------------ meshes

        static Matrix4x4 I => Matrix4x4.identity;
        static void B(RailKit k, float x, float y, float z, float hx, float hy, float hz, Color32[] ramp, int salt = 0) => k.Box(new Vector3(x, y, z), Vector3.right, Vector3.up, Vector3.forward, hx, hy, hz, ramp, salt);

        /// <summary>Underframe, buffers, couplers, two bogies with wheelsets, steps at the corners.</summary>
        static void Chassis(RailKit k, CarDef d, float frameY = 0.82f)
        {
            float h = d.Half;
            k.rust = 0.45f;
            for (int s = -1; s <= 1; s += 2) B(k, s * 1.05f, frameY, 0f, 0.1f, 0.16f, h - 0.2f, Pal.Black, 1);
            for (int e = -1; e <= 1; e += 2)
            {
                float z = e * (h - 0.15f);
                B(k, 0f, frameY, z, 1.35f, 0.18f, 0.15f, Pal.Black, 2);
                for (int s = -1; s <= 1; s += 2)
                {
                    k.Cylinder(new Vector3(s * 0.85f, frameY, z), new Vector3(s * 0.85f, frameY, z + e * 0.45f), 0.16f, 8, Pal.Metal, 3);
                    k.Cylinder(new Vector3(s * 0.85f, frameY, z + e * 0.45f), new Vector3(s * 0.85f, frameY, z + e * 0.5f), 0.24f, 10, Pal.Chrome, 4);
                    // steps: platform height, then the deck
                    B(k, s * 1.45f, 0.3f, z - e * 0.35f, 0.18f, 0.03f, 0.28f, Pal.Metal, 5);
                    B(k, s * 1.45f, 0.62f, z - e * 0.35f, 0.18f, 0.03f, 0.28f, Pal.Metal, 5);
                    B(k, s * 1.48f, 0.47f, z - e * 0.1f, 0.03f, 0.2f, 0.03f, Pal.Metal, 6);
                }
                B(k, 0f, frameY - 0.05f, z + e * 0.42f, 0.14f, 0.1f, 0.3f, Pal.Rust, 7);
            }
            if (d.kind == CarKind.Draisine) return;
            for (int bo = -1; bo <= 1; bo += 2)
            {
                float bz = bo * d.bogie;
                B(k, 0f, 0.62f, bz, 1.0f, 0.12f, 1.25f, Pal.Black, 8);
                for (int s = -1; s <= 1; s += 2) B(k, s * 1.0f, 0.55f, bz, 0.08f, 0.2f, 1.35f, Pal.Rust, 9);
                for (int ax = -1; ax <= 1; ax += 2)
                {
                    float wz = bz + ax * 0.9f;                                                       // the wheelsets turn (RailWheels)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        B(k, s * 1.0f, 0.45f, wz, 0.1f, 0.1f, 0.1f, Pal.Black, 12);                        // axle box
                        B(k, s * 0.72f, 0.5f, wz - ax * 0.52f, 0.1f, 0.16f, 0.05f, Pal.Rust, 13);           // brake block
                    }
                }
            }
        }

        static void Deck(RailKit k, CarDef d, float y, Color32[] ramp)
        {
            k.rust = 0.1f;
            for (float z = -d.Half + 0.35f; z < d.Half - 0.2f; z += 0.42f) B(k, 0f, y - 0.05f, z, 1.4f, 0.05f, 0.19f, ramp, 20);
            for (float z = -d.Half + 1f; z < d.Half; z += 2f) for (int s = -1; s <= 1; s += 2) B(k, s * 1.42f, y + 0.12f, z, 0.05f, 0.14f, 0.07f, Pal.Metal, 21);
        }

        static void Rivets(RailKit k, Vector3 a, Vector3 b, int n, Vector3 outward)
        {
            for (int i = 0; i <= n; i++) { var p = Vector3.Lerp(a, b, i / (float)n) + outward * 0.02f; k.Box(p, Vector3.right, Vector3.up, Vector3.forward, 0.035f, 0.035f, 0.035f, Pal.Chrome, 30); }
        }

        /// <summary>The body mesh of a car kind (variant changes the patchwork); turret / lever meshes separately.</summary>
        public static RailKit Body(CarKind kind, int variant)
        {
            var d = Get(kind);
            var k = new RailKit(variant * 131 + (int)kind * 17);
            var rnd = new System.Random(variant * 977 + (int)kind);
            Color32[] Patch() { switch (rnd.Next(5)) { case 0: return Olive; case 1: return Cream; case 2: return Faded; case 3: return Ochre; default: return Sheet; } }
            switch (kind)
            {
                case CarKind.Draisine:
                {
                    Chassis(k, d, 0.42f);
                    Deck(k, d, 0.62f, RailKit.Timber);
                    k.rust = 0.3f;
                    B(k, 0f, 0.95f, 0.2f, 0.12f, 0.3f, 0.12f, Pal.Metal, 40);                    // pump pivot post
                    B(k, 0f, 0.82f, -0.9f, 0.55f, 0.06f, 0.22f, RailKit.Timber, 41);            // bench
                    for (int s = -1; s <= 1; s += 2) B(k, s * 0.85f, 0.95f, 0f, 0.03f, 0.3f, 1.5f, Pal.Metal, 42);
                    break;
                }
                case CarKind.MakeshiftLoco:
                {
                    Chassis(k, d);
                    Deck(k, d, 1.05f, Pal.Black);
                    // the hood: patched panels over the engine
                    k.rust = 0.55f;
                    for (float z = -0.9f; z < 4.9f; z += 1.15f)
                    {
                        var ramp = Patch();
                        B(k, 0f, 2.1f, z + 0.55f, 0.95f, 1.0f, 0.56f, ramp, 50);
                        Rivets(k, new Vector3(0.96f, 1.2f, z), new Vector3(0.96f, 3f, z), 5, Vector3.right);
                        Rivets(k, new Vector3(-0.96f, 1.2f, z), new Vector3(-0.96f, 3f, z), 5, Vector3.left);
                        for (int g = 0; g < 4; g++) B(k, 0.97f, 1.6f + g * 0.22f, z + 0.55f, 0.02f, 0.04f, 0.4f, Pal.Black, 51);   // louvres
                    }
                    B(k, 0f, 3.15f, 2.0f, 1.0f, 0.06f, 3.0f, Sheet, 52);
                    k.Cylinder(new Vector3(0.3f, 3.1f, 3.4f), new Vector3(0.3f, 4.4f, 3.4f), 0.17f, 10, Pal.Black, 53);   // exhaust stack
                    k.Cylinder(new Vector3(0.3f, 4.4f, 3.4f), new Vector3(0.3f, 4.5f, 3.6f), 0.2f, 10, Pal.Rust, 54);
                    k.Cylinder(new Vector3(-0.4f, 3.2f, 1.2f), new Vector3(-0.4f, 3.2f, 1.9f), 0.11f, 8, Pal.Chrome, 55);    // air horn
                    // the cab at the back: walls with windows, a roof of scrap sheet
                    var cab = Patch();
                    k.rust = 0.4f;
                    B(k, 0f, 1.5f, -2.6f, 1.35f, 0.45f, 1.55f, cab, 56);
                    for (int s = -1; s <= 1; s += 2) { B(k, s * 1.3f, 3.0f, -1.25f, 0.06f, 1.05f, 0.18f, cab, 57); B(k, s * 1.3f, 3.0f, -3.95f, 0.06f, 1.05f, 0.18f, cab, 57); B(k, s * 1.31f, 3.3f, -2.6f, 0.03f, 0.5f, 1.1f, RailKit.Lens, 58); B(k, s * 1.3f, 2.3f, -2.6f, 0.06f, 0.35f, 1.2f, cab, 57); }
                    B(k, 0f, 3.0f, -1.12f, 1.3f, 1.05f, 0.05f, cab, 57);
                    B(k, -0.6f, 3.3f, -1.08f, 0.45f, 0.5f, 0.03f, RailKit.Lens, 58); B(k, 0.6f, 3.3f, -1.08f, 0.45f, 0.5f, 0.03f, RailKit.Lens, 58);
                    B(k, 0f, 3.0f, -4.08f, 1.3f, 1.05f, 0.05f, cab, 57);
                    B(k, 0f, 4.12f, -2.6f, 1.45f, 0.08f, 1.75f, Sheet, 59);
                    B(k, -0.45f, 2.15f, -2.6f, 0.3f, 0.12f, 0.3f, RailKit.Timber, 60);   // seat
                    B(k, -0.45f, 2.55f, -1.6f, 0.4f, 0.3f, 0.2f, Pal.Black, 61);              // controls
                    // cowcatcher of rail offcuts, lamp
                    k.rust = 0.6f;
                    for (int i = -3; i <= 3; i++) k.Beam(new Vector3(i * 0.38f, 0.15f, 5.95f + (3 - Mathf.Abs(i)) * 0.18f), new Vector3(i * 0.3f, 0.95f, 5.4f), 0.08f, 0.08f, Pal.Rust, 62);
                    k.emit = true; k.Cylinder(new Vector3(0f, 2.7f, 5.66f), new Vector3(0f, 2.7f, 5.72f), 0.2f, 10, RailKit.Lamp, 63); k.emit = false;
                    k.Cylinder(new Vector3(0f, 2.7f, 5.4f), new Vector3(0f, 2.7f, 5.66f), 0.24f, 10, Pal.Black, 64);
                    // handrails
                    for (int s = -1; s <= 1; s += 2) k.Beam(new Vector3(s * 1.3f, 2.0f, -0.9f), new Vector3(s * 1.3f, 2.0f, 5.2f), 0.04f, 0.04f, Ochre, 65);
                    break;
                }
                case CarKind.ArmoredLoco:
                {
                    Chassis(k, d);
                    k.rust = 0.3f;
                    B(k, 0f, 1.95f, 0f, 1.45f, 1.1f, 6.2f, Plate, 70);
                    B(k, 0f, 3.35f, 0f, 1.2f, 0.32f, 5.6f, Plate, 71);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        B(k, s * 1.5f, 0.75f, 0f, 0.05f, 0.5f, 6.1f, Plate, 72);              // skirts over the wheels
                        for (float z = -5.5f; z <= 5.6f; z += 1.1f) Rivets(k, new Vector3(s * 1.46f, 1.0f, z), new Vector3(s * 1.46f, 2.9f, z), 6, s > 0 ? Vector3.right : Vector3.left);
                        for (float z = -5f; z < 5.5f; z += 2.5f) B(k, s * 1.47f, 2.55f, z, 0.03f, 0.05f, 0.45f, Pal.Black, 73);   // vision slits
                        B(k, s * 1.46f, 1.4f, 0f, 0.02f, 0.12f, 5.8f, Ochre, 74);            // warning band
                    }
                    for (int e = -1; e <= 1; e += 2)
                    {
                        // ploughs both ends
                        for (int i = 0; i < 4; i++) B(k, 0f, 0.3f + i * 0.22f, e * (6.45f + (3 - i) * 0.12f), 1.3f - i * 0.1f, 0.11f, 0.1f, Pal.Black, 75);
                        B(k, 0f, 2.3f, e * 6.25f, 1.1f, 0.06f, 0.04f, Pal.Black, 76);
                        k.emit = true; B(k, 0f, 2.75f, e * 6.27f, 0.16f, 0.12f, 0.02f, RailKit.Lamp, 77); k.emit = false;
                    }
                    k.Cylinder(new Vector3(0f, 3.6f, 2.5f), new Vector3(0f, 4.6f, 2.5f), 0.2f, 10, Pal.Black, 78);
                    k.Cylinder(new Vector3(0f, 3.7f, -1.5f), new Vector3(0f, 4.0f, -1.5f), 0.6f, 12, Plate, 79);   // commander's cupola
                    break;
                }
                case CarKind.ShackWagon:
                {
                    Chassis(k, d);
                    Deck(k, d, 1.0f, RailKit.Timber);
                    k.rust = 0.6f;
                    var wall = Patch();
                    for (int s = -1; s <= 1; s += 2) B(k, s * 1.33f, 2.3f, -0.9f, 0.05f, 1.3f, 4.1f, wall, 80);
                    B(k, 0f, 2.3f, -5.0f, 1.33f, 1.3f, 0.05f, wall, 80);
                    B(k, 0.6f, 2.3f, 3.2f, 0.75f, 1.3f, 0.05f, wall, 80);
                    B(k, -1.1f, 2.3f, 3.2f, 0.25f, 1.3f, 0.05f, wall, 80);
                    B(k, -0.42f, 1.95f, 3.21f, 0.42f, 0.95f, 0.04f, RailKit.Timber, 81);        // door
                    for (int s = -1; s <= 1; s += 2) for (int w = -1; w <= 1; w++) B(k, s * 1.36f, 2.6f, -0.9f + w * 2.3f, 0.03f, 0.35f, 0.5f, RailKit.Lens, 82);
                    for (int r = 0; r < 4; r++) B(k, 0f, 3.62f + r * 0.12f, -0.9f, 1.5f - r * 0.35f, 0.06f, 4.3f, Sheet, 83);
                    k.Cylinder(new Vector3(-0.8f, 3.6f, -3f), new Vector3(-0.8f, 4.6f, -3f), 0.1f, 8, Pal.Black, 84);   // stovepipe
                    // porch with a rail
                    for (int s = -1; s <= 1; s += 2) k.Beam(new Vector3(s * 1.35f, 1.0f, 3.3f), new Vector3(s * 1.35f, 2.0f, 3.3f), 0.05f, 0.05f, RailKit.Timber, 85);
                    k.Beam(new Vector3(-1.35f, 2f, 5.8f), new Vector3(1.35f, 2f, 5.8f), 0.05f, 0.05f, RailKit.Timber, 85);
                    for (int s = -1; s <= 1; s += 2) k.Beam(new Vector3(s * 1.35f, 2f, 3.3f), new Vector3(s * 1.35f, 2f, 5.8f), 0.05f, 0.05f, RailKit.Timber, 85);
                    // washing line and a drum
                    k.Cylinder(new Vector3(0.9f, 1.0f, 4.6f), new Vector3(0.9f, 1.9f, 4.6f), 0.3f, 10, Pal.Rust, 86);
                    break;
                }
                case CarKind.Hopper:
                {
                    Chassis(k, d);
                    k.rust = 0.7f;
                    B(k, 0f, 1.75f, 0f, 1.4f, 0.85f, 4.9f, Pal.Rust, 90);
                    for (float z = -4.5f; z <= 4.6f; z += 1.5f) for (int s = -1; s <= 1; s += 2) B(k, s * 1.43f, 1.75f, z, 0.04f, 0.85f, 0.07f, Pal.Black, 91);
                    for (int s = -1; s <= 1; s += 2) { k.Beam(new Vector3(s * 0.7f, 0.95f, -4.6f), new Vector3(s * 0.2f, 0.6f, -2.5f), 0.4f, 0.4f, Pal.Rust, 92); k.Beam(new Vector3(s * 0.7f, 0.95f, 4.6f), new Vector3(s * 0.2f, 0.6f, 2.5f), 0.4f, 0.4f, Pal.Rust, 92); }
                    // the load: a heap of scrap and ore
                    k.rust = 0.2f;
                    for (int i = 0; i < 28; i++)
                    {
                        float x = (float)(rnd.NextDouble() - 0.5) * 2.4f, z = (float)(rnd.NextDouble() - 0.5) * 9f, y = 2.5f + (float)rnd.NextDouble() * 0.35f * (1f - Mathf.Abs(z) / 5f);
                        B(k, x, y, z, 0.2f + (float)rnd.NextDouble() * 0.3f, 0.12f, 0.2f + (float)rnd.NextDouble() * 0.4f, (i & 3) == 0 ? Pal.Black : (i & 3) == 1 ? Pal.Sand : Sheet, 93);
                    }
                    B(k, 0f, 2.45f, 0f, 1.35f, 0.08f, 4.85f, Pal.Sand, 94);
                    break;
                }
                case CarKind.Flatcar:
                case CarKind.WreckFlat:
                {
                    Chassis(k, d);
                    Deck(k, d, 1.0f, RailKit.Timber);
                    if (kind == CarKind.Flatcar)
                    {
                        // tie-down rings and two loading skids at one end
                        for (float z = -5f; z <= 5.1f; z += 2.5f) for (int s = -1; s <= 1; s += 2) B(k, s * 1.2f, 1.03f, z, 0.08f, 0.03f, 0.08f, Pal.Chrome, 100);
                        break;
                    }
                    // a gutted car body chained down
                    k.rust = 0.85f;
                    var paint = Patch();
                    B(k, 0f, 1.55f, 1f, 0.92f, 0.38f, 2.2f, paint, 101);
                    B(k, 0f, 2.15f, 0.7f, 0.85f, 0.25f, 1.1f, paint, 102);
                    B(k, 0f, 2.15f, 1.85f, 0.8f, 0.2f, 0.05f, RailKit.Lens, 103);
                    for (int a = -1; a <= 1; a += 2) for (int s = -1; s <= 1; s += 2) k.Cylinder(new Vector3(s * 0.95f, 1.35f, 1f + a * 1.4f), new Vector3(s * 0.75f, 1.35f, 1f + a * 1.4f), 0.32f, 10, Pal.Tire, 104);
                    for (int c = -1; c <= 1; c += 2) for (int s = -1; s <= 1; s += 2) k.Beam(new Vector3(s * 1.3f, 1.05f, 1f + c * 2f), new Vector3(s * 0.85f, 1.9f, 1f + c * 1.6f), 0.04f, 0.04f, Pal.Chrome, 105);
                    // crates at the other end
                    for (int i = 0; i < 3; i++) B(k, -0.6f + i * 0.6f, 1.3f, -4f, 0.28f, 0.3f, 0.3f, RailKit.Timber, 106);
                    break;
                }
                case CarKind.GunWagon:
                {
                    Chassis(k, d);
                    k.rust = 0.35f;
                    B(k, 0f, 1.95f, 0f, 1.45f, 1.0f, 5.3f, Plate, 110);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        for (float z = -4.8f; z <= 4.9f; z += 1.2f) Rivets(k, new Vector3(s * 1.46f, 1.1f, z), new Vector3(s * 1.46f, 2.8f, z), 5, s > 0 ? Vector3.right : Vector3.left);
                        for (float z = -4f; z <= 4.1f; z += 2f) B(k, s * 1.47f, 2.4f, z, 0.03f, 0.06f, 0.3f, Pal.Black, 111);    // firing slits
                        B(k, s * 1.5f, 0.75f, 0f, 0.05f, 0.45f, 5.2f, Plate, 112);
                    }
                    B(k, 0f, 2.98f, 0f, 1.3f, 0.06f, 5f, Plate, 113);
                    k.Cylinder(new Vector3(0f, 2.95f, 0f), new Vector3(0f, 3.1f, 0f), 1.15f, 16, Pal.Black, 114);
                    // sandbags along the roof edges
                    for (float z = -4.6f; z <= 4.7f; z += 0.7f) for (int s = -1; s <= 1; s += 2) B(k, s * 1.15f, 3.15f, z, 0.18f, 0.12f, 0.32f, Pal.Sand, 115);
                    break;
                }
                case CarKind.Caboose:
                {
                    Chassis(k, d);
                    Deck(k, d, 1.0f, RailKit.Timber);
                    k.rust = 0.4f;
                    var red = Pal.Crimson;
                    for (int s = -1; s <= 1; s += 2) B(k, s * 1.3f, 2.3f, 0f, 0.05f, 1.3f, 2.5f, red, 120);
                    for (int e = -1; e <= 1; e += 2) { B(k, 0.7f, 2.3f, e * 2.5f, 0.6f, 1.3f, 0.05f, red, 120); B(k, -0.9f, 2.3f, e * 2.5f, 0.4f, 1.3f, 0.05f, red, 120); B(k, -0.1f, 1.95f, e * 2.51f, 0.4f, 0.95f, 0.04f, RailKit.Timber, 121); }
                    for (int s = -1; s <= 1; s += 2) { B(k, s * 1.33f, 2.6f, -1f, 0.03f, 0.35f, 0.45f, RailKit.Lens, 122); B(k, s * 1.33f, 2.6f, 1f, 0.03f, 0.35f, 0.45f, RailKit.Lens, 122); }
                    B(k, 0f, 3.65f, 0f, 1.45f, 0.06f, 2.9f, Pal.Black, 123);
                    B(k, 0f, 4.1f, 0f, 0.9f, 0.4f, 0.9f, red, 124);                                  // cupola
                    for (int s = -1; s <= 1; s += 2) B(k, s * 0.92f, 4.15f, 0f, 0.02f, 0.2f, 0.6f, RailKit.Lens, 125);
                    B(k, 0f, 4.55f, 0f, 1.0f, 0.05f, 1.0f, Pal.Black, 126);
                    k.emit = true; B(k, 1.2f, 3.2f, -3.95f, 0.1f, 0.12f, 0.05f, RailKit.Red, 127); k.emit = false;
                    for (int e = -1; e <= 1; e += 2) k.Beam(new Vector3(-1.35f, 2f, e * 3.85f), new Vector3(1.35f, 2f, e * 3.85f), 0.05f, 0.05f, Ochre, 128);
                    break;
                }
            }
            return k;
        }

        /// <summary>The gun wagon's turret: a riveted drum with a twin barrel along +Z (yaws about its origin).</summary>
        public static RailKit Turret()
        {
            var k = new RailKit(77) { rust = 0.3f };
            k.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.9f, 0f), 1.0f, 14, Plate, 1);
            k.Cylinder(new Vector3(0f, 0.9f, 0f), new Vector3(0f, 1.05f, 0f), 0.8f, 14, Plate, 2);
            for (int s = -1; s <= 1; s += 2) k.Cylinder(new Vector3(s * 0.22f, 0.5f, 0.6f), new Vector3(s * 0.22f, 0.5f, 3.0f), 0.08f, 8, Pal.Black, 3);
            k.Box(new Vector3(0f, 0.5f, 0.85f), Vector3.right, Vector3.up, Vector3.forward, 0.45f, 0.25f, 0.3f, Plate, 4);
            k.Box(new Vector3(0f, 0.95f, -0.6f), Vector3.right, Vector3.up, Vector3.forward, 0.3f, 0.15f, 0.25f, Ochre, 5);
            return k;
        }

        /// <summary>The draisine's see-saw pump handle (rocks about its origin on X).</summary>
        public static RailKit Lever()
        {
            var k = new RailKit(78) { rust = 0.2f };
            k.Beam(new Vector3(0f, 0f, -0.9f), new Vector3(0f, 0f, 0.9f), 0.06f, 0.06f, Pal.Metal, 1);
            for (int e = -1; e <= 1; e += 2) k.Beam(new Vector3(-0.45f, 0f, e * 0.9f), new Vector3(0.45f, 0f, e * 0.9f), 0.05f, 0.05f, RailKit.Timber, 2);
            return k;
        }

        /// <summary>Wheelsets of a car (axle centre, wheel radius), as <see cref="RailWheels"/> spins them.</summary>
        public static IEnumerable<(Vector3 at, float r)> Axles(CarDef d)
        {
            if (d.kind == CarKind.Draisine) { for (int ax = -1; ax <= 1; ax += 2) yield return (new Vector3(0f, 0.3f, ax * 0.9f), 0.3f); yield break; }
            for (int bo = -1; bo <= 1; bo += 2)
                for (int ax = -1; ax <= 1; ax += 2) yield return (new Vector3(0f, 0.45f, bo * d.bogie + ax * 0.9f), 0.45f);
        }

        /// <summary>One wheelset about its axle (spins on local X): two wheels with flanges on the inside, three spokes
        /// and a pale balance weight on each outer face so the turning reads, the axle between them.</summary>
        public static RailKit Wheelset(float r)
        {
            var k = new RailKit(91 + Mathf.RoundToInt(r * 100f)) { rust = 0.35f };
            for (int s = -1; s <= 1; s += 2)
            {
                k.Cylinder(new Vector3(s * 0.62f, 0f, 0f), new Vector3(s * 0.82f, 0f, 0f), r, 12, Pal.Black, 1);
                k.Cylinder(new Vector3(s * 0.6f, 0f, 0f), new Vector3(s * 0.635f, 0f, 0f), r + 0.045f, 12, Pal.Metal, 2);   // flange
                k.Cylinder(new Vector3(s * 0.82f, 0f, 0f), new Vector3(s * 0.86f, 0f, 0f), r * 0.28f, 8, Pal.Chrome, 3);  // hub
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2f / 3f;
                    var dir = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
                    k.Beam(new Vector3(s * 0.835f, 0f, 0f) + dir * r * 0.25f, new Vector3(s * 0.835f, 0f, 0f) + dir * r * 0.85f, 0.035f, 0.05f, Pal.Rust, 4);
                }
                var w = new Vector3(0f, -Mathf.Sin(1.05f), Mathf.Cos(1.05f)) * r * 0.62f;
                k.Box(new Vector3(s * 0.84f, 0f, 0f) + w, Vector3.right, Vector3.up, Vector3.forward, 0.025f, r * 0.2f, r * 0.2f, Ochre, 5);   // balance weight
            }
            k.Cylinder(new Vector3(-0.95f, 0f, 0f), new Vector3(0.95f, 0f, 0f), 0.08f, 6, Pal.Metal, 6);
            return k;
        }

        /// <summary>The cab crew (<see cref="CabCrew"/>), seat-frame (origin = hips, facing +Z): the seated body,
        /// the head (pivot at the neck) and the right arm (pivot at the shoulder, hanging along −Y). Guild drivers wear
        /// ochre overalls and a cap, raider crews black and rust with a scarf.</summary>
        public static RailKit CrewBody(bool raider)
        {
            var k = new RailKit(raider ? 61 : 62) { rust = 0.05f };
            var cloth = raider ? Pal.Black : Ochre; var trim = raider ? Pal.Rust : Pal.Navy;
            for (int s = -1; s <= 1; s += 2)
            {
                k.Box(new Vector3(s * 0.1f, 0f, 0.2f), Vector3.right, Vector3.up, Vector3.forward, 0.075f, 0.075f, 0.22f, trim, 1);      // thigh
                k.Box(new Vector3(s * 0.1f, -0.24f, 0.42f), Vector3.right, Vector3.up, Vector3.forward, 0.065f, 0.22f, 0.065f, trim, 2); // shin
                k.Box(new Vector3(s * 0.1f, -0.46f, 0.47f), Vector3.right, Vector3.up, Vector3.forward, 0.07f, 0.05f, 0.11f, Pal.Black, 3); // boot
            }
            k.Box(new Vector3(0f, 0.27f, -0.02f), Vector3.right, Vector3.up, Vector3.forward, 0.18f, 0.25f, 0.11f, cloth, 4);          // torso
            k.Box(new Vector3(0f, 0.06f, -0.02f), Vector3.right, Vector3.up, Vector3.forward, 0.185f, 0.04f, 0.115f, Pal.Black, 5);    // belt
            k.Box(new Vector3(-0.23f, 0.33f, 0.1f), Vector3.right, Vector3.up, Vector3.forward, 0.05f, 0.16f, 0.06f, cloth, 6);        // left arm on the brake
            k.Box(new Vector3(-0.23f, 0.18f, 0.28f), Vector3.right, Vector3.up, Vector3.forward, 0.045f, 0.045f, 0.16f, cloth, 7);
            k.Box(new Vector3(-0.23f, 0.18f, 0.46f), Vector3.right, Vector3.up, Vector3.forward, 0.045f, 0.045f, 0.045f, Pal.Skin, 8);
            return k;
        }

        public static RailKit CrewHead(bool raider)
        {
            var k = new RailKit(raider ? 63 : 64) { rust = 0f };
            k.Box(new Vector3(0f, 0.04f, 0f), Vector3.right, Vector3.up, Vector3.forward, 0.045f, 0.05f, 0.045f, Pal.Skin, 1);      // neck
            k.Box(new Vector3(0f, 0.17f, 0.01f), Vector3.right, Vector3.up, Vector3.forward, 0.1f, 0.11f, 0.11f, Pal.Skin, 2);
            k.Box(new Vector3(0f, 0.18f, 0.12f), Vector3.right, Vector3.up, Vector3.forward, 0.07f, 0.015f, 0.008f, Pal.Black, 3);   // eyes
            if (raider)
            {
                k.Box(new Vector3(0f, 0.1f, 0.03f), Vector3.right, Vector3.up, Vector3.forward, 0.105f, 0.04f, 0.1f, Pal.Crimson, 4);  // scarf
                k.Box(new Vector3(0f, 0.27f, 0f), Vector3.right, Vector3.up, Vector3.forward, 0.09f, 0.03f, 0.1f, Pal.Black, 5);       // shaved top
            }
            else
            {
                k.Box(new Vector3(0f, 0.28f, 0f), Vector3.right, Vector3.up, Vector3.forward, 0.11f, 0.035f, 0.115f, Pal.Navy, 4);     // cap
                k.Box(new Vector3(0f, 0.255f, 0.15f), Vector3.right, Vector3.up, Vector3.forward, 0.1f, 0.012f, 0.05f, Pal.Navy, 5);   // peak
            }
            return k;
        }

        public static RailKit CrewArm(bool raider)
        {
            var k = new RailKit(raider ? 65 : 66) { rust = 0.05f };
            var cloth = raider ? Pal.Black : Ochre;
            k.Box(new Vector3(0f, -0.15f, 0f), Vector3.right, Vector3.up, Vector3.forward, 0.05f, 0.15f, 0.055f, cloth, 1);
            k.Box(new Vector3(0f, -0.4f, 0f), Vector3.right, Vector3.up, Vector3.forward, 0.045f, 0.12f, 0.045f, cloth, 2);
            k.Box(new Vector3(0f, -0.56f, 0f), Vector3.right, Vector3.up, Vector3.forward, 0.045f, 0.045f, 0.045f, Pal.Skin, 3);
            return k;
        }

        /// <summary>The stationmaster's flag on its stick (held in the hand: the stick along the hand's −Y).</summary>
        public static RailKit Flag()
        {
            var k = new RailKit(67) { rust = 0f };
            k.Beam(new Vector3(0f, 0.05f, 0f), new Vector3(0f, -0.55f, 0f), 0.012f, 0.012f, RailKit.Timber, 1);
            k.Box(new Vector3(0f, -0.43f, 0.14f), Vector3.right, Vector3.up, Vector3.forward, 0.006f, 0.1f, 0.14f, RailKit.Green, 2);
            return k;
        }

        /// <summary>Consists: a makeshift service, an armoured train, what a terminal yard has standing.</summary>
        public static CarKind[] Makeshift(int seed)
        {
            var r = new System.Random(seed);
            var l = new List<CarKind> { CarKind.MakeshiftLoco };
            int n = 2 + r.Next(3);
            CarKind[] pool = { CarKind.ShackWagon, CarKind.Hopper, CarKind.WreckFlat, CarKind.Flatcar, CarKind.ShackWagon, CarKind.Hopper };
            for (int i = 0; i < n; i++) l.Add(pool[r.Next(pool.Length)]);
            l.Add(CarKind.Caboose);
            return l.ToArray();
        }

        public static CarKind[] Armoured(int seed) => new System.Random(seed).Next(2) == 0
            ? new[] { CarKind.GunWagon, CarKind.ArmoredLoco, CarKind.GunWagon }
            : new[] { CarKind.Flatcar, CarKind.GunWagon, CarKind.ArmoredLoco, CarKind.GunWagon };
    }
}
