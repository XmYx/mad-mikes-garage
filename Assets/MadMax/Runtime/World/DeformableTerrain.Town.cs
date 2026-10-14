using UnityEngine;

namespace MadMax.World
{
    /// <summary>Town ground (feature codes <see cref="TownPlan"/>.F*): cracked street asphalt, repair patches and oil
    /// stains, faded white and yellow paint, kerb stones, slab sidewalks, paver squares, patchy lawns, fields in rows, bare
    /// lots, a little sand only on the walks and car parks; their HD terrain classes, far colours and ground cover (lawns
    /// with flowers, flower-bed islands, crops, weeds on empty lots). With the HD terrain the cells stay unpainted: HDLit
    /// draws the streets, kerbs, walks and markings from the vertex street fields (<see cref="TownSurface"/>) and the
    /// cells only show where those don't fit together (a 0.25 m seam at the town's rim).</summary>
    public partial class DeformableTerrain
    {
        static readonly Color32[] Lawn = { C(0x30481a), C(0x3a561e), C(0x466424), C(0x52722a), C(0x60802e) };
        static readonly Color32 DryLawn = C(0x6c6634), PaintW = C(0xb0a896), KerbStone = C(0x8c8880), Paver0 = C(0x7a644e), Paver1 = C(0x6c5844),
            PaverJoint = C(0x4c4036), OilStain = C(0x1c1917), PatchTar = C(0x262220);

        Color32 TownColor(byte feat, float hs, float gx, float gz)
        {
            var plan = World != null ? World.PlanAt(gx, gz) : null;
            var l = plan != null ? plan.Local(gx, gz) : new Vector2(gx, gz);
            float drift = Mathf.PerlinNoise(gx * 0.07f + 13f, gz * 0.07f + 41f) + (hs - 0.5f) * 0.08f;     // wind-blown sand
            bool crack = Mathf.Abs(Mathf.PerlinNoise(gx * 0.8f + 17f, gz * 0.8f + 3f) - 0.5f) < 0.022f
                         || Mathf.Abs(Mathf.PerlinNoise(gx * 0.33f + 91f, gz * 0.33f - 7f) - 0.5f) < 0.012f;
            float tone = Mathf.PerlinNoise(gx * 0.03f + 5f, gz * 0.03f + 9f);
            int bi = tone > 0.62f ? 3 : tone > 0.42f ? 2 : 1;
            Color32 col;
            bool hard = true;
            switch (feat)
            {
                case TownPlan.FStreet:
                case TownPlan.FPaint:
                case TownPlan.FYellow:
                case TownPlan.FParking:
                {
                    cellClass = (byte)TerrainClass.Asphalt;
                    col = Asphalt[hs < 0.15f ? 0 : hs < 0.8f ? 1 : 2];
                    float patch = Mathf.PerlinNoise(gx * 0.11f + 5f, gz * 0.11f + 71f);
                    if (patch > 0.72f) col = PatchTar;                                               // tar repairs
                    else if (patch < 0.26f) col = Color32.Lerp(col, Concrete[1], 0.25f);              // sun-bleached
                    if (feat == TownPlan.FParking) col = Color32.Lerp(col, Concrete[0], 0.2f);
                    if (hdMesh) { if (crack) col = Crack; break; }                                     // HD: HDLit paints the lines
                    if (feat == TownPlan.FPaint && hs > 0.1f) col = Color32.Lerp(PaintW, col, hs > 0.85f ? 0.45f : 0.08f);
                    if (feat == TownPlan.FYellow && hs > 0.1f) col = Color32.Lerp(Line, col, hs > 0.85f ? 0.45f : 0.08f);
                    if (hs > 0.993f || (feat == TownPlan.FParking && hs > 0.975f)) col = OilStain;
                    if (crack) col = Crack;
                    break;
                }
                case TownPlan.FKerb:
                    cellClass = (byte)TerrainClass.Concrete;
                    col = Mathf.Repeat(l.x + l.y, 1.2f) < 0.25f ? Concrete[1] : hs < 0.12f ? Concrete[2] : KerbStone;
                    break;
                case TownPlan.FSidewalk:
                {
                    cellClass = (byte)TerrainClass.Concrete;
                    if (Mathf.Repeat(l.x, 1.5f) < 0.25f || Mathf.Repeat(l.y, 1.5f) < 0.25f) col = Concrete[1];   // slab joints
                    else
                    {
                        float slab = Hash01(Mathf.FloorToInt(l.x / 1.5f), Mathf.FloorToInt(l.y / 1.5f));
                        col = Concrete[slab < 0.15f ? 1 : slab < 0.75f ? 2 : 3];
                        if (slab > 0.94f) col = Color32.Lerp(col, Crack, 0.45f);                       // a broken slab
                    }
                    if (crack) col = Crack;
                    else if (hs > 0.985f) col = Litter;
                    break;
                }
                case TownPlan.FPlaza:
                {
                    cellClass = (byte)TerrainClass.Concrete;
                    if (Mathf.Repeat(l.x, 3f) < 0.25f || Mathf.Repeat(l.y, 3f) < 0.25f) col = PaverJoint;
                    else col = ((Mathf.FloorToInt(l.x * 2f) + Mathf.FloorToInt(l.y * 2f)) & 1) == 0 ? Paver0 : Paver1;
                    if (hs < 0.06f) col = Color32.Lerp(col, PaverJoint, 0.6f);                        // a missing paver
                    if (crack) col = Color32.Lerp(col, Crack, 0.6f);
                    else if (hs > 0.988f) col = Litter;
                    break;
                }
                case TownPlan.FPark:
                case TownPlan.FIsland:
                case TownPlan.FYard:
                {
                    cellClass = (byte)TerrainClass.Grass;
                    hard = false;
                    col = Lawn[Mathf.Clamp(bi + (hs > 0.9f ? 1 : hs < 0.1f ? -1 : 0) + (feat == TownPlan.FIsland ? 1 : 0), 0, 4)];
                    float dry = Mathf.PerlinNoise(gx * 0.16f + 3f, gz * 0.16f + 61f) - (feat == TownPlan.FYard ? 0.12f : 0f);
                    if (dry < 0.34f) col = Color32.Lerp(col, DryLawn, Mathf.Round((0.34f - dry) * 8f) / 2f);
                    if (feat == TownPlan.FYard && hs > 0.96f) { col = Dirt[1]; cellClass = (byte)TerrainClass.Dirt; }
                    break;
                }
                case TownPlan.FField:
                    cellClass = (byte)TerrainClass.Dirt;
                    hard = false;
                    col = TownRow(gx, gz) ? (hs > 0.3f ? Crop : Soil) : Soil;
                    break;
                default:                                                                              // bare lots: gravel, dirt, rubble
                {
                    cellClass = (byte)TerrainClass.Gravel;
                    hard = false;
                    float p = Mathf.PerlinNoise(gx * 0.2f + 31f, gz * 0.2f + 7f);
                    col = Color32.Lerp(Gravel[hs < 0.3f ? 0 : 1], Dirt[1], Mathf.Clamp01((p - 0.45f) * 2f));
                    if (hs > 0.95f) col = RockGrey[hs > 0.985f ? 3 : 2];
                    break;
                }
            }
            // wind-blown sand: a light dusting on walks, squares and car parks (the carriageways are kept swept)
            bool dusty = feat == TownPlan.FSidewalk || feat == TownPlan.FPlaza || feat == TownPlan.FParking;
            if (hard && dusty && drift > 0.7f)
                col = Color32.Lerp(col, Sand[2], Mathf.Min(0.3f, Mathf.Round((drift - 0.7f) * 16f) / 8f));
            return col;
        }

        static float Hash01(int x, int z) => Hash(x * 7 + 3, z * 13 + 5);

        /// <summary>Crop rows of a town plan's fields (1.5 m apart along the plan's u axis).</summary>
        bool TownRow(float gx, float gz)
        {
            var plan = World != null ? World.PlanAt(gx, gz) : null;
            float u = plan != null ? plan.Local(gx, gz).x : gx;
            return Mathf.Repeat(u, 1.5f) >= 0.6f;
        }

        static Color32 FarTownColor(byte f) => f switch
        {
            TownPlan.FStreet or TownPlan.FPaint or TownPlan.FYellow or TownPlan.FParking => Asphalt[1],
            TownPlan.FSidewalk or TownPlan.FKerb => Concrete[3],
            TownPlan.FPlaza => Paver0,
            TownPlan.FPark or TownPlan.FIsland => Lawn[2],
            TownPlan.FYard => Lawn[1],
            TownPlan.FField => Crop,
            _ => Gravel[1]
        };

        static float TownFloraDensity(byte f) => f switch
        {
            TownPlan.FPark => 0.7f, TownPlan.FIsland => 0.85f, TownPlan.FYard => 0.5f, TownPlan.FField => 0.9f, _ => 0.16f
        };

        /// <summary>Ground cover on town ground: mown lawns with dandelions and daisies, flower beds on the islands, weeds
        /// and dry tufts on bare lots.</summary>
        void TownPlant(in CellInfo cell, byte feat, float r1, float r2, bool frost)
        {
            switch (feat)
            {
                case TownPlan.FIsland:
                    if (!frost && r1 < 0.55f) Flower(cell, r2 < 0.3f ? FlowerRed : r2 < 0.55f ? FlowerYellow : r2 < 0.8f ? FlowerPink : FlowerWhite);
                    else Tuft(cell, frost ? Straw : MeadowGrass, 2, 0.08f);
                    break;
                case TownPlan.FPark:
                case TownPlan.FYard:
                    if (!frost && r1 < (feat == TownPlan.FPark ? 0.1f : 0.06f)) Flower(cell, r2 < 0.5f ? FlowerYellow : FlowerWhite);
                    else if (r1 < 0.93f) Tuft(cell, frost ? Straw : MeadowGrass, 3, feat == TownPlan.FPark ? 0.07f : 0.1f);
                    else Shrub(cell, frost ? Straw : MeadowGrass, 0.22f);
                    break;
                default:
                    if (r1 < 0.7f) Tuft(cell, frost ? Straw : DryGrass, 2, 0.12f);
                    else if (r1 < 0.9f) Twigs(cell);
                    else Shrub(cell, DeadBush, 0.2f);
                    break;
            }
        }
    }
}
