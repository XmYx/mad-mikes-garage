using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    // P2.1 THE MARKET NOBODY WANTED: survey stakes on three fields and each elder's corner; a field walked gives its
    // reading from the generator (water table depth, distance to a road, slope), and the report names the best field.
    public partial class WastelandGame
    {
        float p2Check;
        static readonly (string step, string anchor, string name)[] P2Fields =
            { ("field_a", "p2_a", "ODA'S FIELD"), ("field_b", "p2_b", "BARNABY'S FIELD"), ("field_mid", "p2_mid", "THE FIELD BETWEEN") };

        partial void Scene_P2_1()
        {
            if (!Build || !Build.Structures) return;
            foreach (var f in P2Fields)
            {
                if (!StoryAnchors.Has(f.anchor)) continue;
                PutAt(f.anchor, "flag", new Vector3(-4f, 0f, -4f), 0f);                                     // survey stakes
                PutAt(f.anchor, "flag", new Vector3(4f, 0f, 4f), 0f);
            }
            if (StoryAnchors.Has("p2_oda")) { PutAt("p2_oda", "bench", new Vector3(-1.8f, 0f, -1.2f), 0f); PutAt("p2_oda", "barrel", new Vector3(1.8f, 0f, -1.5f), 0f); }
            if (StoryAnchors.Has("p2_barnaby")) { PutAt("p2_barnaby", "fence_wood", new Vector3(-2.5f, 0f, -2.5f), 0f); PutAt("p2_barnaby", "trough", new Vector3(1.5f, 0f, -2.2f), 0f); }
        }

        partial void Tick_P2_1()
        {
            if (Time.time < p2Check) return;
            p2Check = Time.time + 0.5f;
            int read = 0;
            foreach (var f in P2Fields)
            {
                if (!Story.Story.StepDone("P2.1", f.step)) continue;
                read++;
                if (Story.Story.Flag("p2_read_" + f.step)) continue;
                Story.Story.SetFlag("p2_read_" + f.step);
                var text = f.name + ": " + P2Reading(f.anchor, out _);
                Toast("SURVEY " + text);
                Journal.Add("SURVEY", text);
            }
            if (read < P2Fields.Length) return;
            string best = null; float bs = float.MaxValue;
            foreach (var f in P2Fields) { P2Reading(f.anchor, out float s); if (s < bs) { bs = s; best = f.name; } }
            StoryLibrary.Get("P2.1").payoff = "THE SURVEY IS IN: " + best + " HAS THE BEST OF WATER, ROAD AND FLAT GROUND. NEITHER ELDER WAS QUITE RIGHT, WHICH PLEASED BOTH.";
        }

        /// <summary>What the ground says at a field: water table depth, distance to the nearest road, slope across 16 m
        /// (lower <paramref name="score"/> is better).</summary>
        string P2Reading(string anchor, out float score)
        {
            var p = StoryAnchors.Get(anchor);
            float water = World.WaterTable(p.x, p.z);
            float road = World.Sample(p.x, p.z).roadDist;
            float lo = float.MaxValue, hi = float.MinValue;
            for (float x = -8f; x <= 8f; x += 4f)
                for (float z = -8f; z <= 8f; z += 4f) { float h = World.Sample(p.x + x, p.z + z).height; lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h); }
            float slope = Mathf.Atan2(hi - lo, 16f) * Mathf.Rad2Deg;
            score = water * 1.5f + Mathf.Min(road, 300f) * 0.1f + slope * 3f;
            return "WATER " + Mathf.RoundToInt(water) + " M DOWN, " + (road > 250f ? "NO ROAD NEAR" : Mathf.RoundToInt(road) + " M TO THE ROAD") + ", " + Mathf.RoundToInt(slope) + " DEG SLOPE";
        }
    }
}
