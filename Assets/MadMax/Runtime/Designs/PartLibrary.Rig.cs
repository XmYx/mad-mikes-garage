using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.Designs
{
    /// <summary>Running gear for built moving bases (<see cref="RigVehicle"/>): earthmover and haul-truck tyres, and
    /// crawler sprockets from a wide dozer track up to the rolling city's giant tread drum. Size class 5 = only the
    /// heavy rig mounts take it.</summary>
    public static partial class PartLibrary
    {
        static IEnumerable<PartDesign> RigParts()
        {
            yield return HeavyWheel();
            yield return GiantWheel();
            yield return TrackSprocket("wheel_track_wide", 5.6f, 10, 3, 420f, 0.8f, 9f, 2201);
            yield return TrackSprocket("wheel_track_heavy", 11.25f, 17, 4, 1600f, 1.4f, 12f, 2202);
            yield return GiantTrackDrum();
        }

        /// <summary>Earthmover tyre (1.1 m): deep staggered chevron bars, a yellow wheel-loader rim with a bolted bead ring.</summary>
        public static PartDesign HeavyWheel()
        {
            const float R = 13.75f, rim = 8f; const int wx = 8;
            var g = HaulTyre(R, rim, wx, 22, 2211, Pal.Ochre);
            var part = Make("wheel_heavy", PartCategory.Wheel, g, 420f, 4, R * VoxelMesher.DefaultSize);
            part.grip = 1.0f; part.mudGrip = 0.92f; part.width = 0.7f; part.wetGrip = 0.78f; part.rolling = 1.3f; part.wearRate = 0.5f; part.footprint = 1.6f;
            return part;
        }

        /// <summary>Mining haul-truck tyre (1.6 m): a wall of rubber with a split rim, valve guard and a ring of hub bolts.</summary>
        public static PartDesign GiantWheel()
        {
            const float R = 20f, rim = 11.5f; const int wx = 12;
            var g = HaulTyre(R, rim, wx, 30, 2212, Pal.Crimson);
            var part = Make("wheel_giant", PartCategory.Wheel, g, 1400f, 5, R * VoxelMesher.DefaultSize);
            part.grip = 0.98f; part.mudGrip = 0.95f; part.width = 1.0f; part.wetGrip = 0.76f; part.rolling = 1.45f; part.wearRate = 0.35f; part.footprint = 2.4f;
            return part;
        }

        /// <summary>Haul tyre: chevron bars that alternate across the tread, sidewall with a raised ring and lettering
        /// dashes, a painted multi-piece rim with lock ring, hub cap and bolts.</summary>
        static VoxelGrid HaulTyre(float R, float rim, int wx, int bars, int seed, Color32[] paint)
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Rubber);
            int n = Mathf.CeilToInt(R);
            float lug = Mathf.Max(1.6f, R * 0.11f);
            for (int x = 0; x <= wx; x++)
            for (int y = -n; y <= n; y++)
            for (int z = -n; z <= n; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                var p = new Vector3Int(x, y, z);
                float a = Angle01(y, z);
                if (d > rim)
                {
                    // chevron: the bar shifts along the angle with the distance from the tread centre
                    float across = Mathf.Abs(x - wx * 0.5f) / (wx * 0.5f);
                    bool bar = Mathf.Repeat(a * bars + across * 0.45f * (x < wx * 0.5f ? 1f : -1f), 1f) < 0.42f;
                    if (d > R - lug && !bar) continue;
                    Color32 c;
                    if (x == 0 || x == wx)
                    {
                        bool ring = Mathf.Abs(d - (rim + (R - rim) * 0.42f)) < 0.6f;
                        bool letter = Mathf.Abs(d - (rim + (R - rim) * 0.68f)) < 0.8f && Mathf.Repeat(a * 48f, 1f) < 0.35f && (int)(a * 6f) % 2 == 0;
                        c = letter ? Pal.Tire[3] : ring ? Pal.Tire[2] : Pal.Tire[1];
                    }
                    else c = d > R - lug ? Pal.Tire[Pal.Hash(p, seed) > 0.85f ? 3 : 2] : Pal.Tire[0];
                    g.Set(p, Pal.Solid(c));
                }
                else
                {
                    if (x >= wx - 1 && d > 1.5f && d < rim - 1.6f) continue;                   // deep dish
                    g.Mat((byte)MadMax.Items.ResourceType.Iron);
                    Color32 c;
                    if (x < 2) c = Pal.Metal[0];
                    else if (d > rim - 1.2f) c = Pal.Pick(Pal.Chrome, p, seed + 1, 1);       // lock ring
                    else if (d < 1.5f) c = Pal.Chrome[3];                                    // hub cap
                    else if (d < rim * 0.38f) c = Pal.Metal[2];
                    else c = Pal.Hash(p, seed + 2) > 0.86f ? Pal.Rust[3] : Pal.Pick(paint, p, seed + 3, 3);
                    g.Set(p, Pal.Solid(c));
                    g.Mat((byte)MadMax.Items.ResourceType.Rubber);
                }
            }
            g.Mat((byte)MadMax.Items.ResourceType.Iron);
            int bolts = R > 15f ? 18 : 12;
            for (int i = 0; i < bolts; i++)
            {
                float t = i / (float)bolts * Mathf.PI * 2f;
                g.Set(wx - 2, Mathf.RoundToInt(Mathf.Sin(t) * rim * 0.5f), Mathf.RoundToInt(Mathf.Cos(t) * rim * 0.5f), Pal.Solid(Pal.Chrome[2]));
            }
            g.Box(wx - 1, Mathf.RoundToInt(rim - 2), -1, wx - 1, Mathf.RoundToInt(rim - 1), 0, Pal.Solid(Pal.Chrome[1]));   // valve guard
            return g;
        }

        /// <summary>Crawler sprocket for a moving base: a toothed drive ring between two guide flanges, spoked web, hub and
        /// bolt circle (the belt itself is drawn by <see cref="CrawlerTracks"/> round the road wheels).</summary>
        static PartDesign TrackSprocket(string key, float R, int wx, int size, float mass, float width, float footprint, int seed)
        {
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Iron);
            int n = Mathf.CeilToInt(R);
            int teeth = Mathf.Max(10, Mathf.RoundToInt(R * 2.2f));
            for (int x = 0; x <= wx; x++)
            for (int y = -n; y <= n; y++)
            for (int z = -n; z <= n; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                var p = new Vector3Int(x, y, z);
                bool flange = x <= 1 || x >= wx - 1;
                float a = Angle01(y, z);
                if (d > R - 1.3f && !flange && Mathf.Repeat(a * teeth, 1f) > 0.5f) continue;   // teeth
                if (!flange && d > R * 0.42f && d < R - 1.6f && (int)(a * 6f) % 2 == 1) continue;   // spokes (gaps)
                Color32 c;
                if (flange) c = d > R - 1.2f ? Pal.Pick(Pal.Metal, p, seed, 3) : Pal.Hash(p, seed + 1) > 0.8f ? Pal.Rust[2] : Pal.Metal[1];
                else if (d > R - 1.6f) c = Pal.Pick(Pal.Chrome, p, seed + 2, 1);           // worn bright tooth tips
                else if (d < R * 0.22f) c = Pal.Chrome[2];
                else c = Pal.Weathered(Pal.Ochre, 0.45f, seed + 3, 2, 0f)(p);
                g.Set(p, Pal.Solid(c));
            }
            int bc = Mathf.Max(6, Mathf.RoundToInt(R));
            for (int i = 0; i < bc; i++)
            {
                float t = i / (float)bc * Mathf.PI * 2f;
                g.Set(wx, Mathf.RoundToInt(Mathf.Sin(t) * R * 0.3f), Mathf.RoundToInt(Mathf.Cos(t) * R * 0.3f), Pal.Solid(Pal.Chrome[3]));
            }
            var part = Make(key, PartCategory.Wheel, g, mass, size, R * VoxelMesher.DefaultSize);
            part.grip = 1.1f; part.mudGrip = 1.6f; part.width = width; part.wetGrip = 0.95f; part.rolling = 1.7f; part.wearRate = 0.15f; part.footprint = footprint;
            return part;
        }

        /// <summary>The rolling city's crawler drum scaled to a base (2.0 m): a riveted rust-red wheel with eight box
        /// spokes, cast shoe pads round the rim, a slewing hub with a chain cog on the inboard face.</summary>
        public static PartDesign GiantTrackDrum()
        {
            const float R = 25f; const int wx = 27; const int seed = 2203;
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Steel);
            int n = Mathf.CeilToInt(R);
            for (int x = 0; x <= wx; x++)
            for (int y = -n; y <= n; y++)
            for (int z = -n; z <= n; z++)
            {
                float d = Mathf.Sqrt(y * y + z * z);
                if (d > R) continue;
                var p = new Vector3Int(x, y, z);
                float a = Angle01(y, z);
                bool flange = x <= 2 || x >= wx - 2;
                bool rimBand = d > R - 3.2f;
                bool spoke = Mathf.Repeat(a * 8f, 1f) < 0.16f;
                bool hub = d < R * 0.26f;
                if (!rimBand && !hub && !spoke && !(flange && d > R - 6f)) continue;
                if (!flange && !hub && !rimBand && x > 4 && x < wx - 4) continue;              // spokes on the two faces only
                Color32 c;
                if (rimBand && !flange)
                {
                    // cast shoe pads: grouser bars every pitch, a dark gap between shoes
                    float s = Mathf.Repeat(a * 40f, 1f);
                    if (d > R - 1.1f && s > 0.78f) continue;
                    c = s < 0.18f ? Pal.Metal[3] : Pal.Pick(Pal.Metal, p, seed, 1);
                }
                else if (hub) c = d < 2.5f ? Pal.Chrome[3] : d < 4.5f ? Pal.Metal[2] : Pal.Weathered(Pal.Rust, 0.3f, seed + 1, 1, 0f)(p);
                else c = Pal.Weathered(Pal.Rust, 0.35f, seed + 2, 2, 0f)(p);
                g.Set(p, Pal.Solid(c));
            }
            // rivet rows on the flanges, the chain cog on the inboard face
            for (int i = 0; i < 48; i++)
            {
                float t = i / 48f * Mathf.PI * 2f;
                foreach (int x in new[] { 0, wx })
                    g.Set(x, Mathf.RoundToInt(Mathf.Sin(t) * (R - 4.5f)), Mathf.RoundToInt(Mathf.Cos(t) * (R - 4.5f)), Pal.Solid(Pal.Chrome[1]));
            }
            for (int i = 0; i < 20; i++)
            {
                float t = i / 20f * Mathf.PI * 2f;
                if (i % 2 == 0) g.Box(-1, Mathf.RoundToInt(Mathf.Sin(t) * 9f), Mathf.RoundToInt(Mathf.Cos(t) * 9f), 0, Mathf.RoundToInt(Mathf.Sin(t) * 9f), Mathf.RoundToInt(Mathf.Cos(t) * 9f), Pal.Solid(Pal.Metal[2]));
            }
            g.CylX(0, 0, 8.2f, -1, -1, Pal.Ramp(Pal.Metal, 1, seed + 3), 6.5f);
            var part = Make("wheel_track_giant", PartCategory.Wheel, g, 6000f, 5, R * VoxelMesher.DefaultSize);
            part.grip = 1.05f; part.mudGrip = 1.7f; part.width = 2.2f; part.wetGrip = 0.95f; part.rolling = 1.8f; part.wearRate = 0.08f; part.footprint = 18f;
            return part;
        }
    }
}
