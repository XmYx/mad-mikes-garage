using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>What the dirty water on a network carries (depth stage F). Silt = plain dirty (rain dust, lakes);
    /// filters take out silt, oil and sewage (oil wears the cartridge fast); only a desalinator or a still takes out salt.</summary>
    [System.Flags] public enum WaterTaint : byte { None = 0, Silt = 1, Oil = 2, Salt = 4, Sewage = 8, Toxic = 16 }

    /// <summary>Water quality (depth stage F): what the ground water under a well carries (oil fields, fallout, a
    /// latrine or trough upstream, brackish shore), whether a water body is the sea, and the words the test kit, taps
    /// and prompts use.</summary>
    public static class WaterQuality
    {
        /// <summary>Oil field strength (WorldGen.OilAt) above which ground water tastes of it.</summary>
        public const float OilLimit = 0.3f;
        /// <summary>A latrine with anything in its pit, or a trough, this close fouls a well.</summary>
        public const float SewageReach = 12f;
        /// <summary>Fuel (20 L+) or chemicals (10 L+) soaked into the ground this close foul a well (<see cref="Spills"/>).</summary>
        public const float SpillReach = 15f;

        /// <summary>The worst thing in a taint set, as the test kit and taps say it.</summary>
        public static string Word(WaterTaint t)
        {
            if ((t & WaterTaint.Toxic) != 0) return "TOXIC";
            if ((t & WaterTaint.Oil) != 0) return "OILY";
            if ((t & WaterTaint.Sewage) != 0) return "FOUL";
            if ((t & WaterTaint.Salt) != 0) return "SALTY";
            return t == WaterTaint.None ? "CLEAN" : "DIRTY";
        }

        /// <summary>Every contaminant in a taint set ("OILY, SALTY").</summary>
        public static string Words(WaterTaint t)
        {
            if (t == WaterTaint.None) return "CLEAN";
            var sb = new System.Text.StringBuilder();
            void Add(WaterTaint f, string w) { if ((t & f) != 0) { if (sb.Length > 0) sb.Append(", "); sb.Append(w); } }
            Add(WaterTaint.Toxic, "TOXIC"); Add(WaterTaint.Oil, "OILY"); Add(WaterTaint.Sewage, "FOUL"); Add(WaterTaint.Salt, "SALTY"); Add(WaterTaint.Silt, "SILTY");
            return sb.ToString();
        }

        /// <summary>The taint of a node's dirty water (plain silt when it holds dirty water of unknown origin).</summary>
        public static WaterTaint TaintOf(UtilityNode n) => !n ? WaterTaint.Silt : n.taint == WaterTaint.None ? WaterTaint.Silt : n.taint;

        /// <summary>What dirty water from this node goes into a canteen as: sea water when it is salty, else dirty water.</summary>
        public static ResourceType Carried(UtilityNode n) => (TaintOf(n) & WaterTaint.Salt) != 0 ? ResourceType.SeaWater : ResourceType.DirtyWater;

        /// <summary>The sea (not a lake) at a point: water standing at sea level on the ocean side of the coast.</summary>
        public static bool IsSea(float x, float z)
        {
            var t = DeformableTerrain.Instance;
            if (!t || t.World == null) return false;
            float lvl = t.WaterLevel(x, z);
            return !float.IsNaN(lvl) && Mathf.Abs(lvl - Weather.LakeRise - WorldGen.SeaLevel) < 0.05f && t.World.ContinentNoise(x, z) <= 0.58f;
        }

        /// <summary>What ground water at <paramref name="p"/> carries, and the reason in words (null when clean).</summary>
        public static WaterTaint GroundTaint(Vector3 p, out string cause)
        {
            var taint = WaterTaint.None;
            cause = null;
            var t = DeformableTerrain.Instance;
            if (t && t.World != null)
            {
                if (t.BiomeAt(p.x, p.z) == Biome.Nuclear) { taint |= WaterTaint.Toxic; cause = "FALLOUT IN THE GROUND"; }
                float oil = t.World.OilAt(p.x, p.z);
                if (oil > OilLimit) { taint |= WaterTaint.Oil; cause ??= "OIL IN THE GROUND"; }
                if (t.World.ContinentNoise(p.x, p.z) < 0.515f) { taint |= WaterTaint.Salt; cause ??= "THE SEA SEEPS IN THIS CLOSE TO THE SHORE"; }
            }
            // fuel, oil or chemicals poured or leaked into the ground nearby
            Spills.SoakedNear(p, SpillReach, out float fuelIn, out float toxicIn);
            if (fuelIn >= 20f) { taint |= WaterTaint.Oil; cause ??= "FUEL SOAKED INTO THE GROUND NEARBY"; }
            if (toxicIn >= 10f) { taint |= WaterTaint.Toxic; cause ??= "CHEMICALS SPILLED NEARBY"; }
            float best = SewageReach;
            string near = null;
            foreach (var pl in Placeable.All)
            {
                if (!pl) continue;
                bool latrine = pl.id == "latrine", trough = pl.id == "trough";
                if (!latrine && !trough) continue;
                if (latrine && pl.TryGetComponent<Latrine>(out var lt) && lt.fresh + lt.ripe < 1f) continue;   // an unused pit fouls nothing
                float d = Vector3.Distance(pl.transform.position, p);
                if (d < best) { best = d; near = (latrine ? "A LATRINE " : "A TROUGH ") + Mathf.RoundToInt(d) + " M AWAY"; }
            }
            if (near != null) { taint |= WaterTaint.Sewage; cause ??= near; }
            return taint;
        }

        public static WaterTaint GroundTaint(Vector3 p) => GroundTaint(p, out _);

        /// <summary>The test kit's reading of a node's water network (one HUD line): verdict, litres, how much is clean,
        /// and the cause when the node draws fouled ground water. <paramref name="verdict"/> = CLEAN or the worst taint.</summary>
        public static string Reading(UtilityNode n, out string verdict, out WaterTaint taint)
        {
            verdict = "EMPTY"; taint = WaterTaint.None;
            if (!n) return "NOTHING TO SAMPLE";
            float w = UtilityGrid.NetWater(n, out float clean);
            if (w < 0.5f) return "WATER TEST: NO WATER TO SAMPLE";
            bool allClean = clean >= 0.99f;
            taint = allClean ? WaterTaint.None : TaintOf(n);
            verdict = Word(taint);
            var sb = new System.Text.StringBuilder("WATER TEST: ").Append(allClean ? "CLEAN" : Words(taint)).Append("  ").Append(Mathf.RoundToInt(w)).Append(" L ").Append(Mathf.RoundToInt(clean * 100f)).Append("% CLEAN");
            if (n.TryGetComponent<WaterSource>(out var src) && src.mode == WaterSource.Mode.Well)
            {
                GroundTaint(n.transform.position, out string cause);
                if (cause != null) sb.Append("  CAUSE: ").Append(cause);
            }
            return sb.ToString();
        }

        public static string Reading(UtilityNode n) => Reading(n, out _, out _);

        /// <summary>What to do about a taint (journal line).</summary>
        public static string Advice(WaterTaint t)
        {
            if (t == WaterTaint.None) return "SAFE TO DRINK";
            if ((t & WaterTaint.Salt) != 0) return "FILTERS WON'T TAKE SALT OUT: DESALINATE OR DISTIL IT";
            if ((t & WaterTaint.Oil) != 0) return "A FILTER CLEANS IT, BUT OIL CLOGS CARTRIDGES FAST: FIND WHERE IT COMES IN";
            if ((t & WaterTaint.Toxic) != 0) return "FALLOUT: FILTER IT TWICE OR FIND ANOTHER SOURCE";
            if ((t & WaterTaint.Sewage) != 0) return "FOUL: MOVE THE LATRINE OR TROUGH, OR FILTER IT";
            return "FILTER OR BOIL IT";
        }

        /// <summary>The kit dipped into open water: the sea, a lake, a toxic pool.</summary>
        public static string OpenWaterReading(Vector3 p)
        {
            var t = DeformableTerrain.Instance;
            if (t && t.BiomeAt(p.x, p.z) == Biome.Nuclear) return "WATER TEST: TOXIC  FALLOUT: DON'T DRINK IT";
            if (IsSea(p.x, p.z)) return "WATER TEST: SALTY  SEA WATER: DISTIL OR DESALINATE IT";
            return "WATER TEST: DIRTY  LAKE WATER: BOIL OR FILTER IT";
        }
    }
}
