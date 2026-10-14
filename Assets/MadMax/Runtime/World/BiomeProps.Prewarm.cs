using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MadMax.World
{
    public static partial class BiomeProps
    {
        static Task<Dictionary<string, Template>> prewarm;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPrewarm() => prewarm = null;

        /// <summary>Start making the templates' grids and mesh data on a worker (load screen); the first
        /// <see cref="Ensure"/> or <see cref="LoadTemplates"/> takes them.</summary>
        public static void Prewarm()
        {
            if (prewarm != null || Alive()) return;
            prewarm = Task.Run(MakeTemplates);
        }

        static Dictionary<string, Template> TakePrewarmed()
        {
            var p = prewarm;
            prewarm = null;
            if (p == null) return null;
            try { return p.Result; }                                                            // waits when Ensure comes first
            catch (System.Exception e) { Debug.LogException(e); return null; }
        }

        /// <summary>Load step: the templates made on a worker, their meshes made here, colliders cooked on workers
        /// (no single long frame). Templates go live only once cooked.</summary>
        public static IEnumerator LoadTemplates()
        {
            if (Alive()) yield break;
            Prewarm();
            while (prewarm != null && !prewarm.IsCompleted) yield return null;
            var made = TakePrewarmed();
            if (made == null || Alive()) { Ensure(); yield break; }
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.Start.PropMeshes");
            var meshes = Meshes(made);
            var bake = PropRemesher.PrebakeAsync(meshes);
            UnityEngine.Profiling.Profiler.EndSample();
            while (!bake.IsCompleted) yield return null;
            if (bake.IsFaulted) Debug.LogException(bake.Exception);
            if (!Alive()) templates = made;
        }
    }
}
