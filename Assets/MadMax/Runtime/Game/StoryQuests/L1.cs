using MadMax.Story;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game
{
    /// <summary>L1 THE SOUND BEFORE THE STORM: Brother Cask's shrine (awning, benches, lamps, the tape player, an engine
    /// stand for an altar, Church banners), Mags's corner at the edge of town and the ruin of Harlan's Works. The tape
    /// plays when Cask plays it; Harlan's build sheet puts all four relics on the map (LastEngine rumours).</summary>
    public partial class WastelandGame
    {
        float l1Check;

        partial void Scene_L1()
        {
            Q3ResetPayoff("L1");
            if (!StoryAnchors.Has("cask") || !Build || !Build.Structures) return;
            PutAt("cask", "porch_awning", new Vector3(0f, 0f, -2f), 0f);
            PutAt("cask", "tuning_bench", new Vector3(0f, 0f, -3.4f), 0f);                           // the engine stand the relics would sit on
            PutAt("cask", "radio", new Vector3(1.6f, 0f, -2.2f), 0f);                                // the tape
            PutAt("cask", "bench", new Vector3(-2.6f, 0f, 1.2f), 90f);
            PutAt("cask", "bench", new Vector3(2.6f, 0f, 1.2f), -90f);
            PutAt("cask", "lamp", new Vector3(-1.8f, 0f, -3.2f), 0f);
            PutAt("cask", "lamp", new Vector3(1.8f, 0f, -3.4f), 0f);
            PutAt("cask", "flag", new Vector3(-3.4f, 0f, -2.8f), 0f);
            PutAt("cask", "flag", new Vector3(3.4f, 0f, -2.8f), 0f);
            if (StoryAnchors.Has("l1_mags"))
            {
                PutAt("l1_mags", "bench", new Vector3(-1.2f, 0f, -1f), 0f);
                PutAt("l1_mags", "table", new Vector3(1.2f, 0f, -0.8f), 0f);
                PutAt("l1_mags", "tyres", new Vector3(2.8f, 0f, -2f), 30f);
            }
            if (StoryAnchors.Has("l1_works"))
            {
                // Harlan's Works: the roller-door frame still up, one wall, the rest gone to the weather
                PutAt("l1_works", "garage_frame", new Vector3(0f, 0f, 3f), 0f);
                PutAt("l1_works", "wall_brick", new Vector3(-3f, 0f, 3f), 0f);
                PutAt("l1_works", "wall_brick", new Vector3(-4f, 0f, 1f), 90f);
                PutAt("l1_works", "shelf", new Vector3(-3.2f, 0f, -0.4f), 90f);
                PutAt("l1_works", "table", new Vector3(1.8f, 0f, 0.2f), 15f);
                PutAt("l1_works", "tyres", new Vector3(3.4f, 0f, 1.8f), 0f);
                PutAt("l1_works", "barrel", new Vector3(2.8f, 0f, -1.6f), 0f);
            }
        }

        partial void Tick_L1()
        {
            if (!Player || Time.time < l1Check) return;
            l1Check = Time.time + 0.3f;
            if (Plot.StepDone("L1", "tape") && !Plot.Flag("l1_played"))
            {
                Plot.SetFlag("l1_played");
                var at = StoryAnchors.Get("cask") + Vector3.up;
                MadMax.Audio.Sfx.Play("tape_rewind", at, 0.8f, 1f, 30f);
                MadMax.Audio.Sfx.Play("engine_idle", at, 1f, 0.7f, 40f);
                MadMax.Audio.Sfx.Play("bell", at, 0.5f, 1.3f, 30f);
            }
            if (Plot.StepDone("L1", "works") && !Plot.Flag("l1_heard"))
            {
                Plot.SetFlag("l1_heard");
                foreach (var r in LastEngine.Relics) LastEngine.Hear(r);
                Journal.Add("RELIC", "HARLAN'S BUILD SHEET SAYS WHERE THE FOUR PIECES WENT: A BUNKER, AN AIRFIELD, A ROAD BOSS AND THE CHURCH (ON YOUR MAP)");
            }
        }
    }
}
