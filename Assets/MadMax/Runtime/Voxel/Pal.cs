using UnityEngine;

namespace MadMax.Voxel
{
    /// <summary>Limited wasteland palette + procedural weathering materials.</summary>
    public static class Pal
    {
        public static Color32 Hex(string h)
        {
            ColorUtility.TryParseHtmlString("#" + h, out var c);
            return c;
        }

        static Color32[] R(params string[] hex) { var r = new Color32[hex.Length]; for (int i = 0; i < hex.Length; i++) r[i] = Hex(hex[i]); return r; }

        public static readonly Color32[] Black = R("0d0d10", "18171c", "232127", "302d33");
        public static readonly Color32[] Rust = R("3f1e10", "5e2c15", "80401d", "a4582a", "c47436");
        public static readonly Color32[] Chrome = R("4d4f56", "7c7f88", "aeb1b8", "e3e5e8");
        public static readonly Color32[] Glass = R("10151d", "1a2330", "2c3c52", "5a7390");
        public static readonly Color32[] Tire = R("121010", "1c1918", "282322", "3a3230");
        public static readonly Color32[] Bronze = R("5a2c14", "7d4020", "a0582c", "c47a40");
        public static readonly Color32[] Olive = R("4a2a1a", "6b3e24", "8c5634", "b07244", "cf9458");
        public static readonly Color32[] Metal = R("24211f", "38332f", "4f4842", "6b625a");
        public static readonly Color32[] Sand = R("8a4a24", "a65c2e", "bb6c36", "cf8044", "e09a58");
        public static readonly Color32[] Wood = R("3a2414", "56361e", "74492a", "93603a", "b07a4c");
        public static readonly Color32[] Cream = R("9c9888", "c4c0b0", "dcd8c8", "eeeadc", "fbf8ee");
        public static readonly Color32[] PaleBlue = R("4c7c96", "6ea4c0", "8cbcd6", "aad2e6", "c4e2f0");
        public static readonly Color32[] RigGreen = R("141a17", "1e2824", "2a3832", "3a4c44", "4e6458");
        public static readonly Color32[] Skin = R("8a5a3a", "b07a52");
        // dye ramps (furniture paint, later clothing)
        public static readonly Color32[] Crimson = R("3a0c10", "5a141a", "7c1c22", "a02a2c", "c44038");
        public static readonly Color32[] Navy = R("0e1630", "18244a", "243866", "34508a", "4a6aa8");
        public static readonly Color32[] Moss = R("142410", "20381a", "2e5024", "40682e", "58843c");
        public static readonly Color32[] Ochre = R("5a3a0c", "7c5214", "a06e1e", "c48c2a", "e0ac40");
        // animals (roadmap 23): grey fur, pink skin (pigs, bald vulture heads, rat tails)
        public static readonly Color32[] Fur = R("2e2c2a", "4a4642", "6a645e", "8e8680", "b0a8a0");
        public static readonly Color32[] Pink = R("7a3c3a", "a05a56", "c07a70", "d8988a", "eab4a4");
        /// <summary>Ramp of a dye (1 red, 2 blue, 3 green, 4 yellow, 5 black, 6 white); null = undyed.</summary>
        public static Color32[] DyeRamp(int dye) => dye switch { 1 => Crimson, 2 => Navy, 3 => Moss, 4 => Ochre, 5 => Black, 6 => Cream, _ => null };
        // Shared workshop / HUD colours: charcoal enamel, linen, brass and sage.
        public static readonly Color32 Panel = Hex("202a29"), PanelEdge = Hex("68746a"), PanelLight = Hex("b2a17c");
        public static readonly Color32 Ink = Hex("f2e6cd"), MutedInk = Hex("b6b6a0"), Accent = Hex("e6b76c"), Selection = Hex("405651");
        public static readonly Color32 HazeDay = Hex("b7c2ba"), HazeDusk = Hex("c7a18a"), HazeNight = Hex("171e31");
        public static readonly Color32 SunDay = Hex("fff0d8"), SunDusk = Hex("ffc08a"), MoonLight = Hex("a5badb");
        public static readonly Color32 Steam = Hex("c8c5b6"), WorkshopDust = Hex("bbab8b");
        public static readonly Color32 Void = Hex("07070a");
        public static readonly Color32 LightY = Hex("ffd15a");
        public static readonly Color32 LightW = Hex("fff3c0");
        public static readonly Color32 TailR = Hex("b02818");
        public static readonly Color32 Amber = Hex("f08a24");

        public static float Hash(int x, int y, int z, int seed = 0)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791) ^ (uint)(seed * 668265263);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; h *= 0x27d4eb2d; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        public static float Hash(Vector3Int p, int seed = 0) => Hash(p.x, p.y, p.z, seed);

        /// <summary>Trilinear value noise in [0,1].</summary>
        public static float Noise(Vector3 p, int seed)
        {
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = p.x - x0, fy = p.y - y0, fz = p.z - z0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
            float L(int dx, int dy, int dz) => Hash(x0 + dx, y0 + dy, z0 + dz, seed);
            float a = Mathf.Lerp(Mathf.Lerp(L(0, 0, 0), L(1, 0, 0), fx), Mathf.Lerp(L(0, 1, 0), L(1, 1, 0), fx), fy);
            float b = Mathf.Lerp(Mathf.Lerp(L(0, 0, 1), L(1, 0, 1), fx), Mathf.Lerp(L(0, 1, 1), L(1, 1, 1), fx), fy);
            return Mathf.Lerp(a, b, fz);
        }

        public static float Fbm(Vector3Int p, float scale, int seed) =>
            Noise((Vector3)p * scale, seed) * 0.65f + Noise((Vector3)p * scale * 2.3f, seed + 31) * 0.35f;

        /// <summary>Pick a ramp colour, biased towards the middle tones.</summary>
        public static Color32 Pick(Color32[] ramp, Vector3Int p, int seed, int bias = 1)
        {
            float h = Hash(p, seed);
            int i = h < 0.12f ? bias - 1 : h < 0.78f ? bias : bias + 1;
            return ramp[Mathf.Clamp(i, 0, ramp.Length - 1)];
        }

        public static VoxMat Solid(Color32 c) => _ => c;
        public static VoxMat Ramp(Color32[] ramp, int bias = 1, int seed = 0) => p => Pick(ramp, p, seed, bias);

        /// <summary>Painted metal with noise-driven rust blooms, heavier near the ground.</summary>
        public static VoxMat Weathered(Color32[] paint, float rust, int seed, int paintBias = 1, float groundLine = 6f)
        {
            return p =>
            {
                float n = Fbm(p, 0.21f, seed) + (Hash(p, seed + 5) - 0.5f) * 0.18f;
                n += Mathf.Max(0, groundLine - p.y) * 0.035f;
                float t = 1f - rust;
                if (n > t + 0.08f) return Pick(Rust, p, seed + 1, n > t + 0.2f ? 3 : 2);
                if (n > t) return Pick(Rust, p, seed + 2, 1);
                return Pick(paint, p, seed, paintBias);
            };
        }

        public static VoxMat Stripe(VoxMat a, VoxMat b, int axis, int period, int width = 1) =>
            p => (((axis == 0 ? p.x : axis == 1 ? p.y : p.z) % period + period) % period) < width ? b(p) : a(p);
    }
}
