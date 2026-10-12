using UnityEngine;

namespace MadMax.World
{
    /// <summary>The megacity's site (roadmap 28 R4): a square plaza graded flat at one height, centred
    /// <see cref="centre"/> (x, z), with a <see cref="Shoulder"/> blending back into the land. Generator space maps to it
    /// with x east and y north (<see cref="MegaCityHost"/> yaws its frame by 90°). Read-only after the world is built;
    /// terrain workers query <see cref="Grade"/>.</summary>
    public class MegaSite
    {
        public const float Shoulder = 60f;
        public readonly string name;
        public readonly Vector2 centre;
        public readonly float height, half;
        public readonly MegaCity.Params p;

        public MegaSite(string name, Vector2 centre, float height, MegaCity.Params p)
        {
            this.name = name; this.centre = centre; this.height = height; this.p = p;
            half = MegaCity.PlazaHalf(p) + 4f;
        }

        /// <summary>Chebyshev distance past the plaza edge (negative inside).</summary>
        public float Edge(float x, float z) => Mathf.Max(Mathf.Abs(x - centre.x), Mathf.Abs(z - centre.y)) - half;

        /// <summary>Grade the land to the plaza (WorldGen.Sample, thread-safe). Returns the weight.</summary>
        public float Grade(float x, float z, ref float h, ref GroundSample s)
        {
            float e = Edge(x, z);
            if (e >= Shoulder) return 0f;
            float w = 1f - Mathf.Clamp01(e / Shoulder);
            w = w * w * (3f - 2f * w);
            h = Mathf.Lerp(h, height, w);
            if (w > 0.85f && s.feature == 0) s.feature = 6;                                   // packed ground under the plaza: no props or cover
            if (e < 0f) { s.water = float.NaN; s.shore = 0f; }
            return w;
        }
    }

    public partial class WorldGen
    {
        /// <summary>The megacity (null: none in this world).</summary>
        public MegaSite mega;

        static readonly string[] MegaNames = { "THE SPIRES", "HIGH RUST", "THE STACKS", "NEW BABEL" };

        /// <summary>Candidates on rings 1.5-2.2 km round the start yard; a site must stay off the sea, lakes, rivers, sites,
        /// towns, the crawlerways and the yard; among the clean ones the flattest wins.</summary>
        void BuildMegaCity()
        {
            var rnd = new System.Random(seed * 613 + 29);
            var p = new MegaCity.Params().With(seed * 7 + 11);
            p.name = MegaNames[rnd.Next(MegaNames.Length)];
            float half = MegaCity.PlazaHalf(p) + 4f, outer = half + MegaSite.Shoulder;
            Vector2 Y = new Vector2(yardP.x, yardP.z);
            float th0 = (float)rnd.NextDouble() * Mathf.PI * 2f;
            Vector2 best = default; float bestScore = float.MaxValue, bestH = 0f;
            for (int ring = 0; ring < 3; ring++)
                for (int k = 0; k < 24; k++)
                {
                    float r = 1500f + ring * 350f, th = th0 + k * Mathf.PI * 2f / 24f;
                    var c = Y + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * r;
                    int bad = 0, n = 0;
                    float lo = float.MaxValue, hi = float.MinValue, sum = 0f;
                    for (int i = 0; i < 9; i++)
                        for (int j = 0; j < 9; j++)
                        {
                            float x = c.x + (i / 8f * 2f - 1f) * outer, z = c.y + (j / 8f * 2f - 1f) * outer;
                            if (!Habitable(x, z, 0.62f)) { bad += 4; continue; }
                            if (SiteAt(x, z) != null) bad += 3;
                            if (Radiation(x, z) > 0f) bad += 3;                                      // no city in the fallout
                            if (LakeAt(x, z, out float t) != null && t < 2.5f) bad += 3;
                            if (RiverAt(x, z, out _, out float rh, out float rd) && rd < rh + 20f) bad += 2;
                            foreach (var st in settlements) if (Vector2.Distance(new Vector2(x, z), st.pos) < st.radius + 60f) { bad += 2; break; }
                            if (city != null && TooCloseToRoute(new Vector2(x, z), city, 90f)) bad += 3;
                            if (city2 != null && TooCloseToRoute(new Vector2(x, z), city2, 90f)) bad += 3;
                            var smp = Sample(x, z);
                            if (smp.roadDist < 10f && Mathf.Max(Mathf.Abs(x - c.x), Mathf.Abs(z - c.y)) < half) bad += 1;
                            n++; sum += smp.height;
                            lo = Mathf.Min(lo, smp.height); hi = Mathf.Max(hi, smp.height);
                        }
                    if (Vector2.Distance(c, Y) < outer + 400f) bad += 10;
                    if (n < 40) continue;
                    float score = bad * 1000f + (hi - lo);
                    if (score < bestScore) { bestScore = score; best = c; bestH = sum / n; }
                }
            if (bestScore == float.MaxValue) return;
            mega = new MegaSite(p.name, best, Mathf.Max(bestH, SeaLevel + 2.5f), p);
        }
    }
}
