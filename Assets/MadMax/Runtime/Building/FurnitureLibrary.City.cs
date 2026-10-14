using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>City pieces: what the rolling and tracked cities are built of, as build pieces for your own moving base —
    /// diamond-plate deck plates (drivable), a vehicle ramp, a foot ramp, steel stairs, railing sections, corrugated wall
    /// panels, a steel ladder, a gangway, the shanty cabin, the hanging pod and the roof greenhouse of the decks, a banded
    /// chimney stack, a yard crane, and the cities' lights (street lamp, searchlight, flood, bulkhead) plus string lights,
    /// a lantern, a barrel fire, a neon sign, flower boxes and gang banners. Meshed with the city's own kit
    /// (<see cref="CityKit"/>, the same weathered ramps and the same fixture builders as <see cref="TrackedCity"/>), so a
    /// lamp post taken off a city deck and one built at home are the same thing. Authored in the kit's generator axes
    /// (x along = the piece's front +Z, y across = −X, z up); glowing parts (lamps, lit windows, neon) are a "Lens" child
    /// that lights with the piece's <see cref="PoweredLight"/>.</summary>
    public static partial class FurnitureLibrary
    {
        sealed class CityLamp { public bool powered = true; public float watts = 60f, intensity = 10f; public bool outdoor = true; }

        static IEnumerable<FurnitureDef> CityPieces()
        {
            var B = BuildCategory.Structure; var Fu = BuildCategory.Furniture; var Ga = BuildCategory.Garden; var U = BuildCategory.Utility; var De = BuildCategory.Decor;
            var S = ResourceType.Scrap; var Fe = ResourceType.Iron; var G = ResourceType.Glass; var Cu = ResourceType.Copper; var W = ResourceType.Wood;

            // ---- decks, ramps, stairs, rails, walls (the moving base's floor plan)
            yield return CityPiece("city_deck_s", "DECK PLATE (2 M)", B, 10, true, "RIVETED DIAMOND PLATE ON A STEEL FRAME, 2 X 2 M: FLOORS ON ANYTHING, DRIVABLE", k => DeckPlate(k, 2f, 2f), null, null, (S, 6), (Fe, 1)).Deck(1f, 1f, 0.16f, 0.16f).Carry();
            yield return CityPiece("city_deck_m", "DECK PLATE (4 M)", B, 18, true, "A 4 X 4 M DECK SECTION OFF A CITY HULL: DRIVABLE", k => DeckPlate(k, 4f, 4f), null, null, (S, 16), (Fe, 4)).Deck(2f, 2f, 0.16f, 0.16f);
            yield return CityPiece("city_deck_l", "DECK PLATE (8 M)", B, 30, true, "AN 8 X 4 M DECK SECTION ON GIRDERS: A CAR PARKS ON IT", k => DeckPlate(k, 8f, 4f), null, null, (S, 32), (Fe, 8)).Deck(2f, 4f, 0.16f, 0.16f);
            yield return CityPiece("city_ramp", "VEHICLE RAMP", B, 16, true, "A 3 M WIDE STEEL RAMP RISING 1.2 M OVER 5 M: UP ONTO A DECK OR A TRAILER", VehicleRamp, null, null, (S, 18), (Fe, 6)).Deck(1.5f, 2.5f, 0.06f, 1.28f);
            yield return CityPiece("city_ramp_foot", "FOOT RAMP", B, 8, true, "A NARROW CLEATED RAMP WITH A HANDRAIL, 0.8 M UP", FootRamp, null, null, (S, 6), (Fe, 1)).Carry();
            yield return CityPiece("city_stairs", "STEEL STAIRS", B, 12, true, "OPEN STEEL STAIRS: 2.7 M UP, ONE STOREY", Stairs, null, null, (S, 10), (Fe, 3));
            yield return CityPiece("city_railing", "RAILING (1.5 M)", B, 6, false, "POSTS, RAILS AND A TOE BOARD: NOBODY WALKS OFF THE EDGE", k => k.Railing(new List<Vector2> { new Vector2(0f, -0.75f), new Vector2(0f, 0.75f) }, 0f), null, null, (S, 2)).Carry();
            yield return CityPiece("city_railing_long", "RAILING (3 M)", B, 8, false, "TWO RAILING SECTIONS IN ONE RUN", k => k.Railing(new List<Vector2> { new Vector2(0f, -1.5f), new Vector2(0f, 1.5f) }, 0f), null, null, (S, 4)).Carry();
            yield return CityPiece("city_wall", "CORRUGATED WALL", B, 10, true, "A 2 M CORRUGATED PANEL IN A BOLTED FRAME", CorrugatedWall, null, null, (S, 6), (Fe, 1)).Carry();
            yield return CityPiece("city_ladder", "DECK LADDER", B, 6, false, "A WELDED 3.2 M LADDER WITH STAND-OFFS", DeckLadder, null, go => { var l = go.AddComponent<Ladder>(); l.localBounds = go.GetComponent<MeshFilter>().sharedMesh.bounds; }, (S, 3)).Carry();
            yield return CityPiece("city_gangway", "GANGWAY", B, 14, true, "A 6 M RAILED GANGWAY ON A HINGE DRUM: DECK TO DECK, DECK TO PIER", Gangway, null, null, (S, 14), (Fe, 4)).Deck(0.7f, 3f, 0.14f, 0.14f);
            yield return CityPiece("city_cabin", "SHANTY CABIN", B, 20, true, "THE CORRUGATED CABINS STACKED ON THE CITY PIPES: DOOR, WINDOW, AWNING AND A COT", k => TrackedCity.Cabin(k, new TrackedCity.Rng(7), Vector3.zero, 1), null, null, (S, 20), (W, 6), (G, 2));
            yield return CityPiece("city_pod", "LIVING POD", B, 24, true, "A RIVETED POD WITH A PORTHOLE, ON LEGS", LivingPod, null, null, (S, 24), (Fe, 6), (G, 2));

            // ---- garden
            yield return CityPiece("city_greenhouse", "ROOF GREENHOUSE", Ga, 12, true, "THE CITY ROOFS' GLASSHOUSE: GROWS UNDER GLASS ALL YEAR", k => TrackedCity.Greenhouse(k, Vector3.zero), null,
                go => { var p = go.AddComponent<GardenPlot>(); p.soil = new Vector3(0.9f, 0.62f, 0f); }, (W, 10), (G, 14), (ResourceType.Concrete, 2));
            yield return CityPiece("city_flower_box", "FLOWER BOX", Ga, 3, false, "A CLAY TROUGH OF WEEDS AND FLOWERS FOR A RAIL OR A SILL", FlowerBox, null, null, (W, 2), (ResourceType.Clay, 2)).Carry();

            // ---- lights
            yield return CityPiece("city_lamp_post", "DECK LAMP POST", Fu, 10, true, "THE CITY DECKS' SODIUM LAMP ON A 6 M POST", k => TrackedCity.StreetLamp(k, Vector3.zero, 0f),
                new CityLamp { watts = 150f, intensity = 30f }, null, (Fe, 3), (G, 1), (Cu, 1)).Carry();
            yield return CityPiece("city_searchlight", "SEARCHLIGHT", Fu, 12, false, "A TRUNNION SEARCHLIGHT OFF A CITY ROOF: LIGHTS THE ROAD AHEAD", k => TrackedCity.Searchlight(k, new Vector3(0f, 0f, 1.6f), new Vector3(10f, 0f, 5f)),
                new CityLamp { watts = 400f, intensity = 40f }, null, (Fe, 4), (G, 2), (Cu, 3)).Carry();
            yield return CityPiece("city_floodlight", "HULL FLOOD", Fu, 6, false, "A SQUAT FLOOD ON A STUB MOUNT, AS ON THE CITY HULLS", k => TrackedCity.Flood(k, new Vector3(0f, 0f, 0.6f), new Vector3(4f, 0f, 0.2f), new Color(0.85f, 0.92f, 1f), 14f),
                new CityLamp { watts = 150f, intensity = 20f }, null, (Fe, 2), (G, 1), (Cu, 1)).Carry();
            yield return CityPiece("city_bulkhead", "BULKHEAD LAMP", Fu, 3, false, "A CAGED WALL LAMP FROM THE CITY CORRIDORS", Bulkhead, new CityLamp { watts = 40f, intensity = 6f, outdoor = false }, null, (S, 1), (G, 1), (Cu, 1)).Carry();
            yield return CityPiece("city_string_lights", "STRING LIGHTS", Fu, 4, true, "COLOURED BULBS ON A SAGGING CABLE BETWEEN TWO POSTS", StringLights, new CityLamp { watts = 30f, intensity = 6f }, null, (Cu, 2), (G, 2), (S, 2)).Carry();
            yield return CityPiece("city_lantern", "LANTERN", Fu, 2, false, "AN OPEN OIL LANTERN: NO POWER NEEDED", Lantern, new CityLamp { powered = false, intensity = 3f, outdoor = false }, null, (S, 1), (G, 1)).Carry();
            yield return CityPiece("city_barrel_fire", "BARREL FIRE", Fu, 6, false, "A HOLED OIL DRUM OF EMBERS: LIGHT AND WARMTH AFTER DARK (FEED IT WOOD)", BarrelFire, new CityLamp { powered = false, intensity = 5f },
                go => { var cl = go.AddComponent<Climate>(); cl.burnsWood = true; cl.heat = 10f; }, (S, 4)).Carry();

            // ---- utility, decor
            yield return CityPiece("city_water_tank", "TANK ON LEGS", U, 12, false, "THE ROOFTOP WATER TANK: 1000 L ON FOUR LEGS", WaterTankLegs, null, go => Node(go, UtilityKind.Water, 0.4f).waterCapacity = 1000f, (S, 12), (Fe, 4));
            yield return CityPiece("city_neon", "NEON SIGN", De, 3, false, "AN 'OPEN' SIGN IN AMBER AND RED TUBES FOR A WALL", NeonSign, new CityLamp { watts = 30f, intensity = 4f }, null, (S, 2), (G, 2), (Cu, 2)).Carry();
            yield return CityPiece("city_banner", "GANG BANNER", De, 3, true, "A TATTERED BANNER ON A POLE", Banner, null, null, (ResourceType.Cloth, 4), (S, 2)).Carry();
            yield return CityPiece("city_chimney", "CHIMNEY STACK", De, 20, true, "A RED AND WHITE BANDED STACK WITH A WARNING BEACON", Chimney, new CityLamp { powered = false, intensity = 3f }, null, (S, 16), (Fe, 6));
            yield return CityPiece("city_crane", "YARD CRANE", De, 30, true, "A LATTICE JIB CRANE WITH ITS CAB AND HOOK (FOR SHOW)", YardCrane, new CityLamp { powered = false, intensity = 2f }, null, (Fe, 30), (S, 20), (G, 1), (Cu, 2));
        }

        static FurnitureDef Carry(this FurnitureDef d) { SalvageDesigns.Portable.Add(d.id); return d; }

        static int StableHash(string s) { int h = 17; foreach (char c in s) h = h * 31 + c; return h & 0x7fffffff; }

        /// <summary>A piece meshed with the city kit: visible mesh, a glowing "Lens" child, and (with <paramref name="lamp"/>)
        /// a point light at the kit's first night lamp.</summary>
        static FurnitureDef CityPiece(string id, string name, BuildCategory cat, int hits, bool meshCollider, string desc, System.Action<CityKit> build,
            CityLamp lamp, System.Action<GameObject> extra, params (ResourceType, int)[] cost)
        {
            var k = new CityKit(Vector3.zero, StableHash(id) % 997);
            build(k);
            var mesh = VoxelMesher.ToMesh(k.mesh, "City_" + id);
            var lens = k.glow.verts.Count > 0 ? VoxelMesher.ToMesh(k.glow, "City_" + id + "_lens") : null;
            bool lit = lamp != null && k.lamps.Count > 0;
            var at = lit ? k.lamps[0].at : Vector3.zero;
            var color = lit ? k.lamps[0].color : Color.white;
            float range = lit ? k.lamps[0].range : 0f;
            var d = new FurnitureDef { id = id, name = name, category = cat, cost = cost, mesh = mesh, hits = hits, meshCollider = meshCollider, desc = desc };
            d.setup = go =>
            {
                var mat = go.GetComponent<MeshRenderer>().sharedMaterial;
                Renderer lensR = null;
                if (lens)
                {
                    var l = new GameObject("Lens", typeof(MeshFilter), typeof(MeshRenderer));
                    l.transform.SetParent(go.transform, false);
                    l.GetComponent<MeshFilter>().sharedMesh = lens;
                    lensR = l.GetComponent<MeshRenderer>();
                    lensR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lensR.sharedMaterial = mat && (!lit || MadMax.Rendering.HDBits.On) ? RollingCity.GlowMaterial(mat) : mat;   // lit windows (and HD lenses) glow always, as in the cities; voxel lamps with their light
                }
                if (lit)
                {
                    if (lamp.powered) Node(go, UtilityKind.Power, 0.2f);
                    Glow(go, at, color, range, lamp.intensity, lamp.powered, lamp.watts);
                    var pl = go.GetComponent<PoweredLight>();
                    pl.outdoor = lamp.outdoor;
                    if (lensR && !MadMax.Rendering.HDBits.On) pl.SetBulb(lensR);
                }
                extra?.Invoke(go);
            };
            return d;
        }

        // ------------------------------------------------------------------ shapes (generator axes: x front, y across, z up)

        static void DeckPlate(CityKit k, float L, float Wd)
        {
            float hx = L / 2f, hy = Wd / 2f;
            k.Span(CM.DeckPlate, -hx, hx, -hy, hy, 0.06f, 0.16f);
            foreach (float s in new[] { -1f, 1f }) k.Span(CM.SteelDark, -hx, hx, s * (hy - 0.15f) - 0.06f, s * (hy - 0.15f) + 0.06f, 0f, 0.06f);   // girders
            for (float x = -hx + 0.1f; x <= hx - 0.05f; x += 1f) k.Span(CM.SteelDark, x - 0.05f, x + 0.05f, -hy + 0.1f, hy - 0.1f, 0f, 0.06f);
            int n = 0;
            for (float x = -hx + 0.25f; x < hx - 0.1f; x += 0.4f)                                  // diamond tread
                for (float y = -hy + 0.25f; y < hy - 0.1f; y += 0.4f, n++)
                    k.Box(CM.Steel, new Vector3(0.16f, 0.05f, 0.012f), new Vector3(x, y, 0.166f), new Vector3(0f, 0f, (n & 1) == 0 ? 0.6f : -0.6f));
            foreach (float s in new[] { -1f, 1f })                                                   // hazard edges front and back
                for (float y = -hy; y < hy - 0.01f; y += 0.3f)
                    k.Span(((int)((y + hy) / 0.3f) & 1) == 0 ? CM.PaintYellow : CM.Rubber, s * hx - 0.1f, s * hx, y, Mathf.Min(hy, y + 0.3f), 0.16f, 0.175f, false);
            for (float x = -hx + 0.15f; x < hx; x += 0.3f)                                           // rivets along the sides
                foreach (float s in new[] { -1f, 1f }) k.Box(CM.Steel, 0.04f, 0.04f, 0.02f, x, s * (hy - 0.05f), 0.17f);
        }

        static void VehicleRamp(CityKit k)
        {
            const float L = 5f, Wd = 3f, H = 1.2f;
            float ang = Mathf.Atan2(H, L), len = Mathf.Sqrt(L * L + H * H);
            float Z(float x) => (x + L / 2f) / L * H;
            k.Box(CM.DeckPlate, new Vector3(len, Wd, 0.12f), new Vector3(0f, 0f, H / 2f), new Vector3(0f, -ang, 0f), true);
            for (float x = -L / 2f + 0.3f; x < L / 2f - 0.1f; x += 0.45f)                           // cleats
                k.Beam(CM.Steel, new Vector3(x, -Wd / 2f + 0.15f, Z(x) + 0.07f), new Vector3(x, Wd / 2f - 0.15f, Z(x) + 0.07f), 0.04f);
            foreach (float s in new[] { -1f, 1f })
            {
                k.Beam(CM.PaintYellow, new Vector3(-L / 2f, s * Wd / 2f, 0.12f), new Vector3(L / 2f, s * Wd / 2f, H + 0.12f), 0.12f);   // kerbs
                foreach (float x in new[] { 0f, L / 2f - 0.15f })
                {
                    k.Span(CM.SteelDark, x - 0.07f, x + 0.07f, s * (Wd / 2f - 0.25f) - 0.07f, s * (Wd / 2f - 0.25f) + 0.07f, 0f, Z(x));
                    k.Beam(CM.Rust, new Vector3(x - 1.2f, s * (Wd / 2f - 0.25f), Z(x - 1.2f) * 0.5f), new Vector3(x, s * (Wd / 2f - 0.25f), 0.1f), 0.05f);
                }
            }
            k.Span(CM.SteelDark, L / 2f - 0.15f, L / 2f, -Wd / 2f, Wd / 2f, 0f, H, true);            // the end wall under the lip
            for (float y = -Wd / 2f; y < Wd / 2f - 0.01f; y += 0.3f)
                k.Span(((int)((y + Wd / 2f) / 0.3f) & 1) == 0 ? CM.PaintYellow : CM.Rubber, L / 2f - 0.16f, L / 2f + 0.01f, y, y + 0.3f, H - 0.18f, H, false);
        }

        static void FootRamp(CityKit k)
        {
            const float L = 3f, Wd = 1.2f, H = 0.8f;
            float ang = Mathf.Atan2(H, L), len = Mathf.Sqrt(L * L + H * H);
            float Z(float x) => (x + L / 2f) / L * H;
            k.Box(CM.DeckPlate, new Vector3(len, Wd, 0.08f), new Vector3(0f, 0f, H / 2f), new Vector3(0f, -ang, 0f), true);
            for (float x = -L / 2f + 0.2f; x < L / 2f; x += 0.3f) k.Beam(CM.Steel, new Vector3(x, -Wd / 2f + 0.05f, Z(x) + 0.05f), new Vector3(x, Wd / 2f - 0.05f, Z(x) + 0.05f), 0.03f);
            for (float x = -L / 2f + 0.1f; x <= L / 2f; x += 1.45f) k.Beam(CM.Steel, new Vector3(x, Wd / 2f - 0.05f, Z(x)), new Vector3(x, Wd / 2f - 0.05f, Z(x) + 1f), 0.05f);
            k.Beam(CM.Steel, new Vector3(-L / 2f + 0.1f, Wd / 2f - 0.05f, Z(-L / 2f + 0.1f) + 1f), new Vector3(L / 2f, Wd / 2f - 0.05f, H + 1f), 0.05f);
            k.Span(CM.SteelDark, L / 2f - 0.1f, L / 2f, -Wd / 2f, Wd / 2f, 0f, H, true);
        }

        static void Stairs(CityKit k)
        {
            const int n = 12; const float rise = 0.225f, run = 0.3f, hw = 0.5f;
            float x0 = -n * run / 2f;
            for (int i = 0; i < n; i++)
            {
                float x = x0 + i * run, z = (i + 1) * rise;
                k.Span(CM.DeckPlate, x, x + run + 0.02f, -hw, hw, z - 0.05f, z);
                k.Span(CM.PaintYellow, x + run - 0.04f, x + run + 0.02f, -hw, hw, z - 0.002f, z + 0.004f, false);   // nosing
            }
            foreach (float s in new[] { -1f, 1f })
            {
                k.Beam(CM.Steel, new Vector3(x0, s * (hw + 0.04f), 0.02f), new Vector3(-x0, s * (hw + 0.04f), n * rise), 0.06f, 0.24f);    // stringers
                for (int i = 0; i <= n; i += 4)
                {
                    float x = x0 + i * run + 0.15f, z = i * rise;
                    k.Beam(CM.Steel, new Vector3(x, s * (hw + 0.06f), z), new Vector3(x, s * (hw + 0.06f), z + 1f), 0.04f);
                }
                k.Beam(CM.Steel, new Vector3(x0 + 0.15f, s * (hw + 0.06f), 1f), new Vector3(-x0 + 0.1f, s * (hw + 0.06f), n * rise + 1f), 0.045f);
            }
        }

        static void CorrugatedWall(CityKit k)
        {
            const float hy = 1f, H = 2.7f;
            int i = 0;
            for (float y = -hy; y < hy - 0.01f; y += 0.16f, i++)
            {
                float off = (i & 1) == 0 ? 0f : 0.03f;
                k.Span(CM.Corrugated, -0.03f + off, 0.03f + off, y, y + 0.16f, 0.05f, H - 0.05f);
            }
            foreach (float z in new[] { 0f, H - 0.08f, H * 0.5f }) k.Span(CM.SteelDark, -0.07f, 0.07f, -hy - 0.06f, hy + 0.06f, z, z + 0.08f);
            foreach (float s in new[] { -1f, 1f }) k.Span(CM.SteelDark, -0.07f, 0.07f, s * hy - 0.06f, s * hy + 0.06f, 0f, H);
            for (float y = -hy + 0.2f; y < hy; y += 0.4f)
                foreach (float z in new[] { 0.04f, H * 0.5f + 0.04f, H - 0.04f }) k.Box(CM.Steel, 0.03f, 0.04f, 0.04f, 0.085f, y, z);
        }

        static void DeckLadder(CityKit k)
        {
            const float H = 3.2f;
            foreach (float s in new[] { -0.25f, 0.25f })
            {
                k.Beam(CM.Steel, new Vector3(0f, s, 0f), new Vector3(0f, s, H), 0.06f);
                foreach (float z in new[] { 0.3f, H - 0.3f }) k.Beam(CM.SteelDark, new Vector3(0f, s, z), new Vector3(-0.2f, s, z), 0.04f);   // stand-offs to the wall
            }
            for (float z = 0.3f; z < H; z += 0.3f) k.Beam(CM.Steel, new Vector3(0f, -0.25f, z), new Vector3(0f, 0.25f, z), 0.035f);
        }

        static void Gangway(CityKit k)
        {
            const float hx = 3f, hy = 0.7f;
            k.Span(CM.DeckPlate, -hx, hx, -hy, hy, 0.02f, 0.14f);
            for (float x = -hx + 0.2f; x < hx; x += 0.35f) k.Beam(CM.Steel, new Vector3(x, -hy + 0.05f, 0.15f), new Vector3(x, hy - 0.05f, 0.15f), 0.025f);
            k.Railing(new List<Vector2> { new Vector2(-hx + 0.2f, hy), new Vector2(hx - 0.2f, hy) }, 0.14f, false, 1f, 1.5f);
            k.Railing(new List<Vector2> { new Vector2(-hx + 0.2f, -hy), new Vector2(hx - 0.2f, -hy) }, 0.14f, false, 1f, 1.5f);
            k.Tube(CM.SteelDark, new Vector3(-hx, -hy - 0.05f, 0.08f), new Vector3(-hx, hy + 0.05f, 0.08f), 0.1f, 12);   // hinge drum
            k.Box(CM.PaintYellow, new Vector3(0.3f, 2f * hy, 0.03f), new Vector3(hx + 0.12f, 0f, 0.05f), new Vector3(0f, 0.25f, 0f));   // landing lip
        }

        static void LivingPod(CityKit k)
        {
            foreach (float x in new[] { -1.5f, 1.5f })
                foreach (float y in new[] { -1f, 1f }) k.Span(CM.SteelDark, x - 0.08f, x + 0.08f, y - 0.08f, y + 0.08f, 0f, 1.25f);
            k.Span(CM.RustDark, -1.8f, 1.8f, -1.2f, 1.2f, 1.1f, 1.25f);
            TrackedCity.Pod(k, new TrackedCity.Rng(11), new Vector3(0f, 0f, 1.25f), 1, true);
        }

        static void FlowerBox(CityKit k)
        {
            k.Span(CM.Pot, -0.22f, 0.22f, -0.6f, 0.6f, 0f, 0.32f);
            k.Span(CM.Ground, -0.18f, 0.18f, -0.56f, 0.56f, 0.32f, 0.34f);
            var rnd = new TrackedCity.Rng(31);
            for (int i = 0; i < 7; i++)
            {
                float y = -0.48f + i * 0.16f, h = rnd.Range(0.15f, 0.35f);
                k.Box(CM.Foliage, new Vector3(0.2f, 0.16f, h), new Vector3(rnd.Range(-0.06f, 0.06f), y, 0.34f + h / 2f), new Vector3(0f, 0f, rnd.Range(0f, 1.5f)));
                if (rnd.Chance(0.6f)) k.Box(rnd.Pick(CM.PaintYellow, CM.StackRed, CM.StackWhite), new Vector3(0.06f, 0.06f, 0.05f), new Vector3(rnd.Range(-0.08f, 0.08f), y, 0.36f + h));
            }
        }

        static void Bulkhead(CityKit k)
        {
            // a wall piece: out of the wall = +z (the piece's +Y)
            k.Box(CM.SteelDark, new Vector3(0.3f, 0.48f, 0.05f), new Vector3(0f, 0f, 0.025f));
            k.Box(CM.SteelDark, new Vector3(0.24f, 0.4f, 0.16f), new Vector3(0f, 0f, 0.13f));
            k.Box(CM.Lamp, new Vector3(0.18f, 0.32f, 0.03f), new Vector3(0f, 0f, 0.22f));
            foreach (float x in new[] { -0.07f, 0f, 0.07f }) k.Box(CM.Steel, new Vector3(0.025f, 0.42f, 0.025f), new Vector3(x, 0f, 0.25f));
            k.LampAt(new Vector3(0f, 0f, 0.5f), new Color(1f, 0.78f, 0.52f), 7f);
        }

        static readonly string[] OpenGlyphs =
        {
            "111.111.111.101",
            "101.101.100.111",
            "101.111.110.111",
            "101.100.100.111",
            "111.100.111.101",
        };

        static void NeonSign(CityKit k)
        {
            // a wall piece: the face lies on the wall (x = up the wall, y = across), +z out
            const float px = 0.06f;
            int cols = OpenGlyphs[0].Length, rows = OpenGlyphs.Length;
            float hw = cols * px / 2f + 0.12f, hh = rows * px / 2f + 0.12f;
            k.Box(CM.SteelDark, new Vector3(2f * hh, 2f * hw, 0.06f), new Vector3(0f, 0f, 0.03f));
            foreach (float s in new[] { -1f, 1f })
            {
                k.Box(CM.StripCyan, new Vector3(0.03f, 2f * hw - 0.06f, 0.03f), new Vector3(s * (hh - 0.04f), 0f, 0.08f));
                k.Box(CM.StripCyan, new Vector3(2f * hh - 0.06f, 0.03f, 0.03f), new Vector3(0f, s * (hw - 0.04f), 0.08f));
            }
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (OpenGlyphs[r][c] == '1')
                        k.Box(c < 7 ? CM.LampAmber : CM.Beacon, new Vector3(px * 0.8f, px * 0.8f, 0.025f), new Vector3((rows / 2f - r - 0.5f) * px, (cols / 2f - c - 0.5f) * px, 0.08f));
            for (int i = 0; i < 2; i++) k.Box(CM.Steel, 0.04f, 0.04f, 0.04f, (i == 0 ? -1f : 1f) * (hh - 0.06f), hw - 0.06f, 0.07f);   // brackets
            k.LampAt(new Vector3(0f, 0f, 0.6f), new Color(1f, 0.45f, 0.3f), 6f);
        }

        static void StringLights(CityKit k)
        {
            const float hy = 2f, H = 2.6f;
            foreach (float s in new[] { -1f, 1f })
            {
                k.Beam(CM.SteelDark, new Vector3(0f, s * hy, 0f), new Vector3(0f, s * hy, H), 0.08f);
                k.Box(CM.ConcreteDark, 0.3f, 0.3f, 0.12f, 0f, s * hy, 0.06f);
            }
            Vector3 p = new Vector3(0f, -hy, H - 0.1f), q = new Vector3(0f, hy, H - 0.1f);
            const float sag = 0.45f;
            k.Cable(p, q, sag, 14, 0.025f);
            var bulbs = new[] { CM.LampAmber, CM.Lamp, CM.Beacon, CM.LampCyan, CM.Lamp };
            for (int i = 1; i < 12; i++)
            {
                float f = i / 12f;
                var c = Vector3.Lerp(p, q, f) - new Vector3(0f, 0f, sag * 4f * f * (1f - f) + 0.07f);
                k.Box(bulbs[i % bulbs.Length], new Vector3(0.07f, 0.07f, 0.09f), c);
            }
            k.LampAt(new Vector3(0f, 0f, H - 0.7f), new Color(1f, 0.8f, 0.55f), 8f);
        }

        static void Lantern(CityKit k)
        {
            k.Box(CM.SteelDark, new Vector3(0.22f, 0.22f, 0.03f), new Vector3(0f, 0f, 0.015f));
            foreach (float x in new[] { -0.09f, 0.09f })
                foreach (float y in new[] { -0.09f, 0.09f }) k.Beam(CM.Steel, new Vector3(x, y, 0.03f), new Vector3(x, y, 0.3f), 0.018f);
            k.Box(CM.LampAmber, new Vector3(0.05f, 0.05f, 0.09f), new Vector3(0f, 0f, 0.11f));
            k.Box(CM.Lamp, new Vector3(0.025f, 0.025f, 0.05f), new Vector3(0f, 0f, 0.18f));
            k.Box(CM.SteelDark, new Vector3(0.24f, 0.24f, 0.03f), new Vector3(0f, 0f, 0.315f));
            k.Box(CM.SteelDark, new Vector3(0.12f, 0.12f, 0.04f), new Vector3(0f, 0f, 0.35f));
            k.Beam(CM.Steel, new Vector3(0f, -0.07f, 0.37f), new Vector3(0f, 0f, 0.45f), 0.015f);
            k.Beam(CM.Steel, new Vector3(0f, 0f, 0.45f), new Vector3(0f, 0.07f, 0.37f), 0.015f);
            k.LampAt(new Vector3(0f, 0f, 0.2f), new Color(1f, 0.7f, 0.4f), 5f);
        }

        static void BarrelFire(CityKit k)
        {
            k.Tube(CM.Rust, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0.86f), 0.3f, 16, true);
            k.Tube(CM.RustDark, new Vector3(0f, 0f, 0.84f), new Vector3(0f, 0f, 0.9f), 0.32f, 16);
            foreach (float z in new[] { 0.28f, 0.58f }) k.Tube(CM.RustDark, new Vector3(0f, 0f, z - 0.02f), new Vector3(0f, 0f, z + 0.02f), 0.31f, 16);
            for (int i = 0; i < 10; i++)                                                              // punched holes glowing
            {
                float a = i * Mathf.PI * 0.2f + (i & 1) * 0.3f, z = 0.18f + (i % 3) * 0.17f;
                k.Box(CM.LampAmber, new Vector3(0.06f, 0.06f, 0.05f), new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f, z), new Vector3(0f, 0f, a));
            }
            k.Span(CM.LampAmber, -0.24f, 0.24f, -0.24f, 0.24f, 0.8f, 0.86f, false);                  // embers
            var rnd = new TrackedCity.Rng(5);
            for (int i = 0; i < 6; i++) k.Box(CM.Lamp, new Vector3(0.05f, 0.05f, rnd.Range(0.12f, 0.3f)), new Vector3(rnd.Range(-0.15f, 0.15f), rnd.Range(-0.15f, 0.15f), 0.95f), new Vector3(rnd.Range(-0.3f, 0.3f), rnd.Range(-0.3f, 0.3f), 0f));
            for (int i = 0; i < 3; i++) k.Beam(CM.Wood, new Vector3(rnd.Range(-0.2f, 0.2f), -0.25f, 0.88f), new Vector3(rnd.Range(-0.2f, 0.2f), 0.25f, 0.9f), 0.05f);
            k.LampAt(new Vector3(0f, 0f, 1.2f), new Color(1f, 0.5f, 0.2f), 6f);
        }

        static void WaterTankLegs(CityKit k)
        {
            for (int i = 0; i < 4; i++) k.Beam(CM.SteelDark, new Vector3(-1f + 2f * (i % 2), -1f + 2f * (i / 2), 0f), new Vector3(-1f + 2f * (i % 2), -1f + 2f * (i / 2), 2.2f), 0.12f);
            var corners = new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) };
            foreach (float z in new[] { 0.8f, 1.6f })
                for (int i = 0; i < 4; i++)
                {
                    Vector2 a = corners[i], b = corners[(i + 1) % 4];
                    k.Beam(CM.Steel, new Vector3(a.x, a.y, z), new Vector3(b.x, b.y, z), 0.05f);
                }
            k.Tube(CM.Rust, new Vector3(0f, 0f, 2.2f), new Vector3(0f, 0f, 4.4f), 1.3f, 16);
            k.Tube(CM.RustDark, new Vector3(0f, 0f, 4.4f), new Vector3(0f, 0f, 4.55f), 1.35f, 16);
            foreach (float z in new[] { 2.9f, 3.7f }) k.Tube(CM.SteelDark, new Vector3(0f, 0f, z - 0.03f), new Vector3(0f, 0f, z + 0.03f), 1.33f, 16);
            k.Ladder(new Vector3(1.4f, 0f, 0f), new Vector3(1.4f, 0f, 4.5f), 0.4f);
            k.Tube(CM.Pipe, new Vector3(0f, 0f, 2.2f), new Vector3(0f, 0f, 0.4f), 0.08f, 8);
        }

        static void Banner(CityKit k)
        {
            k.Beam(CM.SteelDark, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 3.3f), 0.07f);
            k.Box(CM.ConcreteDark, 0.36f, 0.36f, 0.14f, 0f, 0f, 0.07f);
            k.Beam(CM.Steel, new Vector3(0f, 0f, 3.1f), new Vector3(0f, -1f, 3.1f), 0.04f);
            k.Box(CM.Canvas, new Vector3(0.03f, 0.9f, 1.7f), new Vector3(0.03f, -0.52f, 2.22f));
            k.Box(CM.StackRed, new Vector3(0.035f, 0.9f, 0.16f), new Vector3(0.03f, -0.52f, 2.85f));
            k.Box(CM.StackRed, new Vector3(0.035f, 0.9f, 0.08f), new Vector3(0.03f, -0.52f, 1.6f));
            for (int i = 0; i < 5; i++)                                                                 // a skull: eye holes, teeth
            {
                k.Box(CM.StackWhite, new Vector3(0.04f, 0.36f - i * 0.04f, 0.06f), new Vector3(0.035f, -0.52f, 2.42f - i * 0.06f));
            }
            foreach (float y in new[] { -0.6f, -0.44f }) k.Box(CM.SteelDark, new Vector3(0.045f, 0.08f, 0.08f), new Vector3(0.04f, y, 2.38f));
            var rnd = new TrackedCity.Rng(3);
            for (float y = -0.95f; y < -0.1f; y += 0.1f) k.Box(CM.Canvas, new Vector3(0.03f, 0.08f, rnd.Range(0.05f, 0.22f)), new Vector3(0.03f, y, 1.32f));   // tatters
        }

        static void Chimney(CityKit k)
        {
            k.Tube(CM.RustDark, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f), 0.95f, 20, true);
            for (int i = 0; i < 5; i++)
            {
                float r = 0.62f - i * 0.02f;
                var m = i >= 3 ? (i % 2 == 1 ? CM.StackWhite : CM.StackRed) : (i % 2 == 1 ? CM.Rust : CM.Pipe);
                k.Tube(m, new Vector3(0f, 0f, 1f + i * 1.1f), new Vector3(0f, 0f, 1f + (i + 1) * 1.1f), r, 20, true);
                k.Tube(CM.SteelDark, new Vector3(0f, 0f, 1f + i * 1.1f - 0.03f), new Vector3(0f, 0f, 1f + i * 1.1f + 0.03f), r + 0.04f, 20);
            }
            k.Tube(CM.SteelDark, new Vector3(0f, 0f, 6.45f), new Vector3(0f, 0f, 6.6f), 0.66f, 20);
            k.Ladder(new Vector3(0.68f, 0f, 1f), new Vector3(0.68f, 0f, 6.4f), 0.4f);
            k.Box(CM.Beacon, new Vector3(0.18f, 0.18f, 0.18f), new Vector3(0.45f, 0f, 6.75f));
            k.LampAt(new Vector3(0.45f, 0f, 7.1f), new Color(1f, 0.1f, 0.05f), 8f);
        }

        static void YardCrane(CityKit k)
        {
            const float H = 6f;
            k.Span(CM.ConcreteDark, -0.9f, 0.9f, -0.9f, 0.9f, 0f, 0.4f);
            foreach (float x in new[] { -0.35f, 0.35f })
                foreach (float y in new[] { -0.35f, 0.35f }) k.Beam(CM.Rust, new Vector3(x, y, 0.4f), new Vector3(x, y, H), 0.08f);
            for (float z = 0.4f; z < H - 0.5f; z += 0.8f)                                                // lattice lacing
            {
                k.Beam(CM.RustDark, new Vector3(-0.35f, -0.35f, z), new Vector3(0.35f, -0.35f, z + 0.8f), 0.04f);
                k.Beam(CM.RustDark, new Vector3(0.35f, 0.35f, z), new Vector3(-0.35f, 0.35f, z + 0.8f), 0.04f);
                k.Beam(CM.RustDark, new Vector3(-0.35f, 0.35f, z), new Vector3(-0.35f, -0.35f, z + 0.8f), 0.04f);
                k.Beam(CM.RustDark, new Vector3(0.35f, -0.35f, z), new Vector3(0.35f, 0.35f, z + 0.8f), 0.04f);
            }
            k.Span(CM.RustDark, -0.6f, 0.6f, -0.6f, 0.6f, H, H + 0.3f);
            k.Span(CM.SteelDark, -0.2f, 0.9f, 0.45f, 1.25f, H - 1f, H, false);                          // cab
            k.Box(CM.Glass, new Vector3(0.04f, 0.7f, 0.5f), new Vector3(0.91f, 0.85f, H - 0.45f));
            var root = new Vector3(0f, 0f, H + 0.3f); var tip = new Vector3(7f, 0f, H + 0.6f);
            foreach (float y in new[] { -0.3f, 0.3f }) k.Beam(CM.Rust, root + new Vector3(0f, y, 0f), tip + new Vector3(0f, y, 0f), 0.08f);
            k.Beam(CM.Rust, root + new Vector3(0f, 0f, 0.7f), tip + new Vector3(0f, 0f, 0.2f), 0.08f);
            for (int i = 0; i < 9; i++)
            {
                var a = Vector3.Lerp(root, tip, i / 9f); var b = Vector3.Lerp(root, tip, (i + 1) / 9f);
                foreach (float y in new[] { -0.3f, 0.3f }) k.Beam(CM.Rust, a + new Vector3(0f, y, 0f), b + new Vector3(0f, 0f, 0.7f - 0.5f * (i + 1) / 9f), 0.035f);
            }
            k.Beam(CM.Rust, root, new Vector3(-2.4f, 0f, H + 0.35f), 0.12f);                               // counter jib
            k.Span(CM.ConcreteDark, -2.6f, -1.8f, -0.45f, 0.45f, H - 0.4f, H + 0.3f);
            var hook = new Vector3(5.5f, 0f, H + 0.45f);
            k.Beam(CM.Steel, hook, hook - new Vector3(0f, 0f, 4f), 0.03f);
            k.Box(CM.StackRed, new Vector3(0.3f, 0.3f, 0.4f), hook - new Vector3(0f, 0f, 4.2f));
            k.Box(CM.Beacon, new Vector3(0.14f, 0.14f, 0.14f), tip + new Vector3(0f, 0f, 0.35f));
            k.LampAt(tip + new Vector3(0f, 0f, 0.6f), new Color(1f, 0.1f, 0.05f), 8f);
        }
    }
}
