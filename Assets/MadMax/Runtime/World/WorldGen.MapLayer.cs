using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Map support (HD minimap and map page): every lake on the planet and the lake outline test that
    /// <see cref="LakeAt"/> and <see cref="WaterLevel"/> use, for drawing water without sampling the 3x3 lake cells.</summary>
    public partial class WorldGen
    {
        /// <summary>All lakes (one pass over the lake cells; the cells cache them for <see cref="LakeAt"/> too).</summary>
        public void AllLakes(List<Lake> into)
        {
            into.Clear();
            int x0 = Mathf.FloorToInt(-HalfX / LakeCell), x1 = Mathf.CeilToInt(HalfX / LakeCell);
            int z0 = Mathf.FloorToInt(ZSouth / LakeCell), z1 = Mathf.CeilToInt(ZNorth / LakeCell);
            for (int cz = z0; cz <= z1; cz++)
            for (int cx = x0; cx <= x1; cx++)
            {
                var lake = LakeIn(new Vector2Int(cx, cz));
                if (lake != null) into.Add(lake);
            }
        }

        /// <summary>Distance from a lake's centre in units of its wobbly radius (&lt; 1.05 = open water, as
        /// <see cref="WaterLevel"/>). Thread-safe.</summary>
        public float LakeShape(Lake lake, float x, float z)
        {
            float wob = 0.82f + 0.36f * P(x, z, 0.04f, 3);
            return Vector2.Distance(new Vector2(x, z), lake.pos) / (lake.radius * wob);
        }
    }
}
