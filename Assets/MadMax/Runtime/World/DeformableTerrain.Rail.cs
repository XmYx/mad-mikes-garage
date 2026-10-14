using UnityEngine;

namespace MadMax.World
{
    /// <summary>Railway ground (<see cref="RailNetwork"/>): grey ballast with rust-brown stains and weeds of oil under
    /// the sleepers (feature 19, gravel class), platform and forecourt concrete in slabs with a worn tactile edge
    /// (feature 20, concrete class).</summary>
    public partial class DeformableTerrain
    {
        static readonly Color32 BallastRust = C(0x5a3a26), BallastOil = C(0x262220);

        Color32 RailColor(byte feat, float hs, float gx, float gz)
        {
            if (feat == 19)
            {
                cellClass = (byte)TerrainClass.Gravel;
                float stain = Mathf.PerlinNoise(gx * 0.21f + 3f, gz * 0.21f + 17f);
                var c = RockGrey[hs < 0.2f ? 2 : hs < 0.75f ? 3 : 4];
                if (stain > 0.68f) c = Color32.Lerp(c, BallastRust, 0.45f);
                if (hs > 0.985f) c = BallastOil;
                return c;
            }
            cellClass = (byte)TerrainClass.Concrete;
            if (Mathf.Repeat(gx, 1.5f) < Cell * 0.99f || Mathf.Repeat(gz, 1.5f) < Cell * 0.99f) return Joint;
            var k = Concrete[hs < 0.12f ? 1 : hs < 0.8f ? 3 : 4];
            return hs > 0.992f ? Litter : k;
        }
    }
}
