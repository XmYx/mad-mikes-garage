using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    public static partial class TrackedCity
    {
        /// <summary>Pier half length along the circuit, pier width, lift platform half size (dock-local metres).</summary>
        public const float PierHalfLen = 30f, PierWidth = 8f, LiftHalfX = 3.6f, LiftHalfZ = 5.5f;
        /// <summary>Stair tower and lift shaft centres along the pier (dock-local z).</summary>
        public const float StairZ = 36f, LiftZ = -37f;

        // dock-local game metres (x out from the pier's inner edge, y up from the pier top, z along) → generator space
        static Vector3 G(float x, float y, float z) => new Vector3(z, -x, y);
        static void SpanD(CityKit K, CM m, float x0, float x1, float y0, float y1, float z0, float z1, bool solid = true) => K.Span(m, z0, z1, -x1, -x0, y0, y1, solid);

        /// <summary>A dock for a tracked city at deck height <paramref name="H"/> above its yard (dock-local game space:
        /// origin at the pier's inner edge on its top, +x away from the circuit, +z along it): the pier on lattice
        /// towers, a switchback stair tower down to the yard at one end, the vehicle lift shaft with its winch house at
        /// the other (the platform itself moves: <see cref="LiftPlatform"/>), lamps, railings and the dock's board.</summary>
        public static CityKit Dock(float H, int seed)
        {
            var K = new CityKit(Vector3.zero, seed);
            var rnd = new Rng(seed);
            SpanD(K, CM.DeckPlate, 0, PierWidth, -0.5f, 0, -PierHalfLen, PierHalfLen);
            for (float z = -PierHalfLen; z < PierHalfLen; z += 6) SpanD(K, CM.PaintYellow, 0.1f, 0.6f, 0, 0.01f, z, z + 3, false);
            foreach (float x in new[] { 0.5f, PierWidth - 0.5f }) SpanD(K, CM.RustDark, x - 0.4f, x + 0.4f, -1.6f, -0.5f, -PierHalfLen, PierHalfLen);
            for (float z = -PierHalfLen + 4; z <= PierHalfLen - 4; z += 13)
            {
                foreach (float x in new[] { 0.9f, PierWidth - 0.9f })
                {
                    SpanD(K, CM.Rust, x - 0.4f, x + 0.4f, -H, -1.6f, z - 0.4f, z + 0.4f);
                    SpanD(K, CM.ConcreteDark, x - 1.2f, x + 1.2f, -H, -H + 0.6f, z - 1.2f, z + 1.2f);
                }
                for (float y = -H; y < -2.5f; y += 4.5f)
                {
                    float y1 = Mathf.Min(y + 4.5f, -1.6f);
                    K.Beam(CM.RustDark, G(0.9f, y, z), G(PierWidth - 0.9f, y1, z), 0.25f);
                    K.Beam(CM.RustDark, G(PierWidth - 0.9f, y, z), G(0.9f, y1, z), 0.25f);
                    K.Beam(CM.Rust, G(0.9f, y1, z), G(PierWidth - 0.9f, y1, z), 0.3f);
                }
            }
            for (float z = -PierHalfLen + 4; z < PierHalfLen - 4 - 12; z += 13)                                          // longitudinal X-bracing
                foreach (float x in new[] { 0.9f, PierWidth - 0.9f })
                    for (float y = -H; y < -2.5f; y += 9f)
                    {
                        float y1 = Mathf.Min(y + 9f, -1.6f);
                        K.Beam(CM.RustDark, G(x, y, z), G(x, y1, z + 13), 0.22f);
                        K.Beam(CM.RustDark, G(x, y1, z), G(x, y, z + 13), 0.22f);
                    }
            K.Railing(new List<Vector2> { new Vector2(-PierHalfLen + 0.2f, -(PierWidth - 0.2f)), new Vector2(PierHalfLen - 0.2f, -(PierWidth - 0.2f)) }, 0f);
            // stair tower: switchback flights (16 risers per 2.8 m), landings, a lattice cage
            float sz0 = StairZ - 4f, sz1 = StairZ + 4f, sx0 = 1f, sx1 = PierWidth - 1f, half = (sx1 - sx0) / 2;
            SpanD(K, CM.DeckPlate, 0, PierWidth, -0.5f, 0, PierHalfLen, sz0);
            K.Railing(new List<Vector2> { new Vector2(PierHalfLen, -0.2f), new Vector2(sz0, -0.2f) }, 0f);
            K.Railing(new List<Vector2> { new Vector2(PierHalfLen, -(PierWidth - 0.2f)), new Vector2(sz0, -(PierWidth - 0.2f)) }, 0f);
            int flights = Mathf.CeilToInt(H / 1.4f);
            float rise = H / flights, run = (sz1 - sz0 - 2.4f) / 8f;
            for (int f = 0; f < flights; f++)
            {
                float yb = -H + f * rise;
                bool up = f % 2 == 0;
                for (int i = 0; i < 8; i++)
                {
                    float zA = up ? sz0 + 1.2f + i * run : sz1 - 1.2f - (i + 1) * run;
                    float x0 = up ? sx0 : sx0 + half, x1 = up ? sx0 + half : sx1;
                    SpanD(K, CM.Steel, x0, x1, yb + i * rise / 8f, yb + (i + 1) * rise / 8f, zA, zA + run);
                }
                float lz0 = up ? sz1 - 1.2f : sz0, lz1 = up ? sz1 : sz0 + 1.2f;
                SpanD(K, CM.DeckPlate, sx0, sx1, yb + rise - 0.12f, yb + rise, lz0, lz1);
            }
            SpanD(K, CM.DeckPlate, sx0, sx1, -0.4f, 0, sz0, sz0 + 1.2f);
            foreach (float x in new[] { sx0 - 0.15f, sx1 + 0.15f })
                foreach (float z in new[] { sz0, sz1 }) SpanD(K, CM.Rust, x - 0.2f, x + 0.2f, -H, 2.6f, z - 0.2f, z + 0.2f);
            for (float y = -H; y < 0; y += 3f)
            {
                K.Beam(CM.RustDark, G(sx0 - 0.15f, y, sz0), G(sx0 - 0.15f, y + 3f, sz1), 0.12f);
                K.Beam(CM.RustDark, G(sx1 + 0.15f, y, sz1), G(sx1 + 0.15f, y + 3f, sz0), 0.12f);
                K.Beam(CM.RustDark, G(sx0 - 0.15f, y, sz1), G(sx1 + 0.15f, y + 3f, sz1), 0.12f);
                K.SolidWall(G(sx0 - 0.1f, 0, sz0), G(sx0 - 0.1f, 0, sz1), y, y + 3f);
                K.SolidWall(G(sx1 + 0.1f, 0, sz0), G(sx1 + 0.1f, 0, sz1), y, y + 3f);
                K.SolidWall(G(sx0, 0, sz1 + 0.1f), G(sx1, 0, sz1 + 0.1f), y, y + 3f);
            }
            SpanD(K, CM.Corrugated, sx0 - 0.4f, sx1 + 0.4f, 2.6f, 2.8f, sz0 - 0.4f, sz1 + 0.4f);
            SpanD(K, CM.ConcreteDark, sx0 - 1f, sx1 + 1f, -H - 0.2f, -H, sz0 - 2.5f, sz1 + 1f);
            // lift shaft: corner columns, bracing, guide rails, the winch house with drum and counterweights
            float lz = LiftZ, cx = PierWidth / 2;
            SpanD(K, CM.DeckPlate, 0, PierWidth, -0.5f, 0, lz + LiftHalfZ + 0.4f, -PierHalfLen);
            foreach (float x in new[] { 0.2f, PierWidth - 0.2f })
                K.Railing(new List<Vector2> { new Vector2(lz + LiftHalfZ + 0.4f, -x), new Vector2(-PierHalfLen, -x) }, 0f);
            foreach (float x in new[] { cx - LiftHalfX - 0.4f, cx + LiftHalfX + 0.4f })
                foreach (float z in new[] { lz - LiftHalfZ - 0.4f, lz + LiftHalfZ + 0.4f })
                    SpanD(K, CM.Rust, x - 0.3f, x + 0.3f, -H - 0.2f, 6.5f, z - 0.3f, z + 0.3f);
            for (float y = -H; y < 6f; y += 4f)
            {
                K.Beam(CM.RustDark, G(cx - LiftHalfX - 0.4f, y, lz - LiftHalfZ - 0.4f), G(cx - LiftHalfX - 0.4f, y + 4, lz + LiftHalfZ + 0.4f), 0.2f);
                K.Beam(CM.RustDark, G(cx + LiftHalfX + 0.4f, y, lz + LiftHalfZ + 0.4f), G(cx + LiftHalfX + 0.4f, y + 4, lz - LiftHalfZ - 0.4f), 0.2f);
                K.Beam(CM.RustDark, G(cx - LiftHalfX - 0.4f, y, lz - LiftHalfZ - 0.4f), G(cx + LiftHalfX + 0.4f, y + 4, lz - LiftHalfZ - 0.4f), 0.2f);
            }
            foreach (float x in new[] { cx - LiftHalfX - 0.15f, cx + LiftHalfX + 0.15f }) SpanD(K, CM.SteelDark, x - 0.08f, x + 0.08f, -H, 6f, lz - 0.1f, lz + 0.1f, false);
            SpanD(K, CM.Corrugated, cx - LiftHalfX - 1f, cx + LiftHalfX + 1f, 6.5f, 10.5f, lz - LiftHalfZ - 1f, lz + LiftHalfZ + 1f);
            SpanD(K, CM.SteelDark, cx - LiftHalfX - 1.2f, cx + LiftHalfX + 1.2f, 10.5f, 10.8f, lz - LiftHalfZ - 1.2f, lz + LiftHalfZ + 1.2f);
            K.Tube(CM.SteelDark, G(cx - 2.5f, 8.2f, lz), G(cx + 2.5f, 8.2f, lz), 1.1f, 20);
            K.Box(CM.WindowLit, new Vector3(2.2f, 0.06f, 1.0f), G(cx, 8.8f, lz - LiftHalfZ - 1.03f));
            foreach (float x in new[] { cx - LiftHalfX - 0.7f, cx + LiftHalfX + 0.7f })
                SpanD(K, CM.ConcreteDark, x - 0.35f, x + 0.35f, -H * 0.5f, -H * 0.5f + 3.2f, lz - 1.2f, lz + 1.2f, false);
            K.Box(CM.Beacon, new Vector3(0.4f, 0.4f, 0.4f), G(cx, 11.2f, lz));
            K.SolidWall(G(cx - LiftHalfX - 0.3f, 0, lz - LiftHalfZ - 0.3f), G(cx + LiftHalfX + 0.3f, 0, lz - LiftHalfZ - 0.3f), -H, 2.4f);
            SpanD(K, CM.ConcreteDark, cx - LiftHalfX - 1.5f, cx + LiftHalfX + 1.5f, -H - 0.3f, -H, lz - LiftHalfZ - 1.5f, lz + LiftHalfZ + 6f);
            // lamps along the pier, the dock board over the stair bridge, crates and drums
            for (float z = -PierHalfLen + 6; z < PierHalfLen; z += 15)
            {
                K.Beam(CM.SteelDark, G(PierWidth - 0.4f, 0, z), G(PierWidth - 0.4f, 6f, z), 0.18f, -1f, true);
                K.Beam(CM.Steel, G(PierWidth - 0.4f, 6f, z), G(PierWidth - 1.8f, 6.2f, z), 0.09f);
                K.Box(CM.Lamp, new Vector3(0.6f, 0.4f, 0.06f), G(PierWidth - 1.8f, 6.1f, z));
                K.LampAt(G(PierWidth - 1.8f, 5.4f, z), new Color(1f, 0.62f, 0.28f), 13f);
            }
            SpanD(K, CM.SteelDark, PierWidth - 0.6f, PierWidth - 0.4f, 0, 5.2f, PierHalfLen + 0.5f, PierHalfLen + 0.7f);
            SpanD(K, CM.SteelDark, 0.4f, 0.6f, 0, 5.2f, PierHalfLen + 0.5f, PierHalfLen + 0.7f);
            SpanD(K, CM.HullOrange, 0.4f, PierWidth - 0.4f, 3.6f, 5.0f, PierHalfLen + 0.55f, PierHalfLen + 0.65f, false);
            for (int i = 0; i < 6; i++) SpanD(K, CM.Plaster, 1.0f + i * 1.0f, 1.6f + i * 1.0f, 4.0f, 4.6f, PierHalfLen + 0.5f, PierHalfLen + 0.52f, false);
            for (int i = 0; i < 10; i++)
            {
                float z = rnd.Range(-PierHalfLen + 3, PierHalfLen - 3), x = rnd.Range(5.5f, PierWidth - 0.8f), s = rnd.Range(0.8f, 1.3f);
                if (rnd.Chance(0.5f)) K.Box(rnd.Pick(CM.Wood, CM.Rust), new Vector3(s, s, s), G(x, s / 2, z), new Vector3(0, 0, rnd.Range(0f, 1.5f)), true);
                else K.Tube(rnd.Pick(CM.Rust, CM.StackRed, CM.HullWhite), G(x, 0, z), G(x, 0.9f, z), 0.3f, 12, true);
            }
            return K;
        }

        /// <summary>The lift platform (platform-local game space, origin at its top centre): plate deck, kerbs, rails.</summary>
        public static CityKit LiftPlatform(int seed)
        {
            var K = new CityKit(Vector3.zero, seed);
            SpanD(K, CM.DeckPlate, -LiftHalfX, LiftHalfX, -0.35f, 0, -LiftHalfZ, LiftHalfZ);
            SpanD(K, CM.PaintYellow, -LiftHalfX, LiftHalfX, 0, 0.02f, -LiftHalfZ, -LiftHalfZ + 0.4f, false);
            SpanD(K, CM.PaintYellow, -LiftHalfX, LiftHalfX, 0, 0.02f, LiftHalfZ - 0.4f, LiftHalfZ, false);
            foreach (float x in new[] { -LiftHalfX + 0.1f, LiftHalfX - 0.1f })
            {
                SpanD(K, CM.SteelDark, x - 0.1f, x + 0.1f, 0, 0.25f, -LiftHalfZ, LiftHalfZ);
                K.Railing(new List<Vector2> { new Vector2(-LiftHalfZ, -x), new Vector2(LiftHalfZ, -x) }, 0f, false, 1.1f, 1.8f);
            }
            SpanD(K, CM.RustDark, -LiftHalfX, LiftHalfX, -0.9f, -0.35f, -0.4f, 0.4f, false);
            SpanD(K, CM.RustDark, -0.3f, 0.3f, -0.9f, -0.35f, -LiftHalfZ, LiftHalfZ, false);
            K.Box(CM.LampAmber, new Vector3(0.3f, 0.3f, 0.3f), G(LiftHalfX - 0.3f, 1.25f, LiftHalfZ - 0.3f));
            return K;
        }

        /// <summary>The gangway of a tracked city (gangway-local game space: hinge at the origin on the deck edge, out
        /// along +x by <paramref name="len"/>): plate deck on two stringers, rails both sides.</summary>
        public static CityKit Gangway(float len, int seed)
        {
            var K = new CityKit(Vector3.zero, seed);
            SpanD(K, CM.DeckPlate, 0, len, -0.25f, 0, -GangHalfW, GangHalfW);
            foreach (float z in new[] { -GangHalfW + 0.2f, GangHalfW - 0.2f })
            {
                SpanD(K, CM.RustDark, 0, len, -0.7f, -0.25f, z - 0.15f, z + 0.15f, false);
                K.Railing(new List<Vector2> { new Vector2(z, 0f), new Vector2(z, -len) }, 0f, false, 1.1f, 1.4f);
            }
            for (float x = 0.6f; x < len; x += 1.2f) SpanD(K, CM.SteelDark, x, x + 0.08f, 0, 0.03f, -GangHalfW + 0.3f, GangHalfW - 0.3f, false);
            return K;
        }
    }
}
