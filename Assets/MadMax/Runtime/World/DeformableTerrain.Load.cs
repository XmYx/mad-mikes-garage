using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

namespace MadMax.World
{
    /// <summary>Terrain at load time without long frames: chunk data generated on workers ahead of time
    /// (<see cref="PrefetchArea"/>), chunk objects built nearest first within a per-frame budget
    /// (<see cref="BuildAround"/>), mesh colliders cooked on workers and assigned once cooked.</summary>
    public partial class DeformableTerrain
    {
        readonly List<Chunk> baking = new List<Chunk>();

        /// <summary>Cook the chunk's collider on a worker; <see cref="PollBakes"/> assigns it (the mesh must not change meanwhile).</summary>
        void BakeLater(Chunk ch)
        {
            var id = ch.mesh.GetEntityId();
            ch.bake = Task.Run(() => Physics.BakeMesh(id, false));
            baking.Add(ch);
        }

        static readonly List<Rigidbody> bodyScratch = new List<Rigidbody>();

        /// <summary>Crates and barrels spawned on a chunk whose collider is still cooking: kinematic until it is in.</summary>
        void HoldLoose(Chunk ch)
        {
            ch.go.GetComponentsInChildren(bodyScratch);
            foreach (var rb in bodyScratch)
            {
                if (rb.isKinematic) continue;
                rb.isKinematic = true;
                (ch.held ??= new List<Rigidbody>()).Add(rb);
            }
            bodyScratch.Clear();
        }

        /// <summary>Assign the colliders whose cooking finished (cheap: PhysX uses the cooked data), let held props go.</summary>
        void PollBakes()
        {
            for (int i = baking.Count - 1; i >= 0; i--)
            {
                var ch = baking[i];
                if (!ch.bake.IsCompleted) continue;
                if (ch.bake.IsFaulted) Debug.LogException(ch.bake.Exception);
                ch.bake = null;
                baking.RemoveAt(i);
                if (ch.col && ch.mesh) ch.col.sharedMesh = ch.mesh;
                if (ch.held == null) continue;
                foreach (var rb in ch.held) if (rb) rb.isKinematic = false;
                ch.held = null;
            }
        }

        void WaitBakes()
        {
            foreach (var ch in baking) try { ch.bake?.Wait(); } catch (System.Exception) { }
            baking.Clear();
        }

        bool LoadedAt(float x, float z) => chunks.ContainsKey(new Vector2Int(FloorDiv(Mathf.RoundToInt(x / Cell), N), FloorDiv(Mathf.RoundToInt(z / Cell), N)));

        /// <summary><see cref="WaterLevel"/> without forcing chunk data (ambient checks around the camera, load time):
        /// the generated lake level at the same grid vertex where the chunk isn't loaded.</summary>
        public float WaterLevelNoLoad(float x, float z)
        {
            if (LoadedAt(x, z) || World == null) return WaterLevel(x, z);
            return World.Sample(Mathf.RoundToInt(x / Cell) * Cell, Mathf.RoundToInt(z / Cell) * Cell).water + Weather.LakeRise;
        }

        /// <summary><see cref="BiomeAt"/> without forcing chunk data.</summary>
        public Biome BiomeAtNoLoad(float x, float z)
        {
            if (LoadedAt(x, z) || World == null) return BiomeAt(x, z);
            return World.Sample(Mathf.RoundToInt(x / Cell) * Cell, Mathf.RoundToInt(z / Cell) * Cell).biome;
        }

        /// <summary>Chunk data made by <see cref="PrefetchArea"/> (workers), inserted by <see cref="BuildAround"/>.</summary>
        Task loadJob;
        Chunk[] loadMade;
        Vector2Int loadCentre;
        int loadDone;

        bool InView(Vector2Int o) => o.magnitude * ChunkWorld <= viewRadius + ChunkWorld;

        /// <summary>Start generating the data of every chunk in view of <paramref name="around"/> on workers (pure), while
        /// the load does other things. <see cref="BuildAround"/> picks it up.</summary>
        public void PrefetchArea(Vector3 around)
        {
            if (World == null) return;
            var fc = ChunkOf(around);
            if (loadJob != null && loadCentre == fc) return;
            var need = new List<Vector2Int>();
            foreach (var o in offsets) if (InView(o) && !chunks.ContainsKey(fc + o)) need.Add(fc + o);
            var made = new Chunk[need.Count];
            var world = World;
            loadCentre = fc; loadMade = made; loadDone = 0;
            loadJob = Task.Run(() => Parallel.For(0, need.Count, i =>
            {
                try { made[i] = Generate(world, need[i]); }
                catch (System.Exception e) { Debug.LogException(e); }
                Interlocked.Increment(ref loadDone);
            }));
        }

        /// <summary>Load-time terrain around a point: the prefetched data (started here if it wasn't), then the chunk
        /// objects nearest first, about <paramref name="budgetMs"/> per frame, then the colliders cooked on workers.
        /// Done when every chunk in view has its object and collider. <paramref name="progress"/> gets 0..1.</summary>
        public IEnumerator BuildAround(Vector3 around, float budgetMs = 12f, System.Action<float> progress = null)
        {
            var fc = ChunkOf(around);
            PrefetchArea(around);
            while (loadJob != null && !loadJob.IsCompleted)
            {
                progress?.Invoke(loadMade.Length > 0 ? 0.35f * Volatile.Read(ref loadDone) / loadMade.Length : 0.35f);
                yield return null;
            }
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var made = loadMade;
            loadJob = null; loadMade = null;
            if (made != null)
                for (int i = 0; i < made.Length; i++)                                             // all data in before any mesh (seamless HD corners)
                {
                    if (clock.Elapsed.TotalMilliseconds > budgetMs) { yield return null; clock.Restart(); }
                    Profiler.BeginSample("MadMax.Terrain.LoadInsert");
                    var ch = made[i];
                    if (ch != null && !chunks.ContainsKey(ch.c)) Insert(ch);
                    Profiler.EndSample();
                }
            var ring = new List<Vector2Int>();                                                    // a copy: the view radius may change between frames
            foreach (var o in offsets) if (InView(o)) ring.Add(o);
            for (int i = 0; i < ring.Count; i++)
            {
                if (clock.Elapsed.TotalMilliseconds > budgetMs)
                {
                    progress?.Invoke(0.35f + 0.6f * i / ring.Count);
                    yield return null;
                    clock.Restart();
                }
                var ch = Data(fc + ring[i]);
                if (!ch.go) BuildObject(ch, true);
            }
            while (baking.Count > 0)
            {
                PollBakes();
                if (baking.Count == 0) break;
                progress?.Invoke(0.97f);
                yield return null;
            }
            progress?.Invoke(1f);
        }
    }
}
