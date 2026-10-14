using System.Collections;
using MadMax.World;
using UnityEngine;
using UnityEngine.Profiling;

namespace MadMax.Game
{
    /// <summary>Load steps of <see cref="Start"/> that would otherwise freeze the loading screen: world generation on a
    /// worker thread, prop templates made beside it, terrain built within a per-frame budget. Every step is a
    /// <c>MadMax.Start.*</c> profiler marker.</summary>
    public partial class WastelandGame
    {
        /// <summary>Main-thread work per load frame (terrain chunks, city pieces): the loading wheel keeps turning.</summary>
        public const float LoadBudgetMs = 24f;

        sealed class WorldJob { public WorldGen world; public System.Exception error, anchorError; public volatile bool done; }

        /// <summary><see cref="WorldGen"/> on its own thread (pure and seeded: the same world as on the main thread), the
        /// prop templates on workers meanwhile; the progress bar creeps from <paramref name="from"/> to <paramref name="to"/>.
        /// With <paramref name="anchors"/> the story places are bound there too (<see cref="MadMax.Story.StoryAnchors.Bind"/>
        /// is pure on the world: a second of sampling that used to stall one load frame); before any chunk data exists.</summary>
        IEnumerator GenerateWorld(int worldSeed, float from, float to, bool anchors = false)
        {
            var job = new WorldJob();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    job.world = new WorldGen(worldSeed);
                    if (anchors)
                    {
                        try { MadMax.Story.StoryAnchors.Bind(job.world); }
                        catch (System.Exception e) { job.anchorError = e; }
                    }
                }
                catch (System.Exception e) { job.error = e; }
                finally { job.done = true; }
            }, 16 * 1024 * 1024) { IsBackground = true, Name = "MadMax WorldGen" };                // road / rail searches: a roomy stack
            thread.Start();
            BiomeProps.Prewarm();
            PropLibrary.Prewarm();
            float t0 = Time.realtimeSinceStartup;
            while (!job.done)
            {
                ScreenFader.Progress(from + (to - from) * (1f - Mathf.Exp(-(Time.realtimeSinceStartup - t0) / 2.5f)));
                yield return null;
            }
            Profiler.BeginSample("MadMax.Start.World");
            if (job.error != null) { Debug.LogException(job.error); job.world = new WorldGen(worldSeed); }   // the same failure here: as before
            World = job.world;
            Profiler.EndSample();
            if (job.anchorError != null)
            {
                Debug.LogException(job.anchorError);
                Profiler.BeginSample("MadMax.Start.Anchors");
                MadMax.Story.StoryAnchors.Rebind(World);                                          // again here, as before (throws the same way)
                Profiler.EndSample();
            }
            ScreenFader.Progress(to);
        }

        /// <summary>The wild and town prop templates (BiomeProps, PropLibrary): made on workers since
        /// <see cref="GenerateWorld"/>, meshed here, colliders cooked on workers.</summary>
        IEnumerator LoadPropTemplates()
        {
            yield return BiomeProps.LoadTemplates();
            while (!PropLibrary.PrewarmDone) yield return null;
            Profiler.BeginSample("MadMax.Start.PropLibrary");
            PropLibrary.EnsureTemplates();
            Profiler.EndSample();
        }

        /// <summary>Terrain around the load focus within <see cref="LoadBudgetMs"/> per frame; progress from..to.</summary>
        IEnumerator BuildTerrain(Vector3 focus, float from, float to)
        {
            yield return terrain.BuildAround(focus, LoadBudgetMs, f => ScreenFader.Progress(from + (to - from) * f));
            Profiler.BeginSample("MadMax.Start.SyncTransforms");
            Physics.SyncTransforms();
            Profiler.EndSample();
            ScreenFader.Progress(to);
        }
    }
}
