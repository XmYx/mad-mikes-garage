using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The HD land layer's hookup (<see cref="MapLand"/>): one view for the minimap, one for the map page, worker
    /// budgets (few while playing, more with the map page open), and hillshade for the whole-world base map.</summary>
    public partial class PixelHud
    {
        MapLand mapLand;
        readonly MapLand.View miniView = new MapLand.View(), pageView = new MapLand.View();
        int landDone = -1; float landAt;

        /// <summary>The HD land layer (tests: <see cref="MapLand.Finished"/>, <see cref="MapLand.Cached"/>).</summary>
        public MapLand Land => mapLand;

        /// <summary>Tests: is this point drawn from an HD tile in the minimap (<paramref name="page"/> false) or the map
        /// page's last prepared view?</summary>
        public bool LandCovered(Vector3 p, bool page)
        {
            if (mapLand == null) return false;
            mapLand.At(page ? pageView : miniView, p.x, p.z, true, out bool fine);
            return fine;
        }

        /// <summary>Tile builds allowed at once: the map page pauses the game and gets most cores, the minimap a few so
        /// terrain streaming keeps its workers.</summary>
        static int MapWorkers(bool page) => page ? Mathf.Clamp(SystemInfo.processorCount - 2, 2, 8) : Mathf.Clamp(SystemInfo.processorCount / 4, 1, 3);

        /// <summary>Hillshade (sun from the north-west) on the base map's land, from the heights sampled with it; NaN =
        /// sea or off the planet (left flat).</summary>
        static void ShadeBase(Color32[] pix, float[] hgt, int size, float mpp)
        {
            const float lx = -0.5f, lz = 0.5f, ly = 0.7071f, relief = 6f;
            var src = (Color32[])pix.Clone();
            System.Threading.Tasks.Parallel.For(1, size - 1, y =>
            {
                for (int x = 1; x < size - 1; x++)
                {
                    int k = y * size + x;
                    float h = hgt[k], hl = hgt[k - 1], hr = hgt[k + 1], hd = hgt[k - size], hu = hgt[k + size];
                    if (float.IsNaN(h)) continue;
                    if (float.IsNaN(hl)) hl = h; if (float.IsNaN(hr)) hr = h; if (float.IsNaN(hd)) hd = h; if (float.IsNaN(hu)) hu = h;
                    float dx = (hr - hl) / (2f * mpp) * relief, dz = (hu - hd) / (2f * mpp) * relief;
                    float shade = (-dx * lx - dz * lz + ly) / Mathf.Sqrt(dx * dx + dz * dz + 1f);
                    float f = Mathf.Clamp(1f + (shade - ly) * 1.1f, 0.6f, 1.35f);
                    var c = src[k];
                    pix[k] = new Color32((byte)Mathf.Min(255f, c.r * f), (byte)Mathf.Min(255f, c.g * f), (byte)Mathf.Min(255f, c.b * f), 255);
                }
            });
        }

        /// <summary>Town district tints fade out as the plan's own ground shows (zoomed in).</summary>
        static byte DistrictAlpha(float mpp, int full) => (byte)Mathf.Clamp(Mathf.RoundToInt(full * (mpp - 1.5f) / 3f), 0, full);
    }
}
