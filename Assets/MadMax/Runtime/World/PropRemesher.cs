using System.Collections.Generic;
using System.Threading.Tasks;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Meshes carved props off the main thread: a snapshot of the grid gets its collapse check (voxels cut off from
    /// the ground) and its mesh on a worker, PhysX cooks the collider on a worker, and the owner drops the fallen voxels
    /// and swaps mesh and collider in a few frames later (a big building takes tens of ms of each).</summary>
    public class PropRemesher : MonoBehaviour
    {
        class Result { public VoxelMesher.MeshData md; public List<Vector3Int> fallen; }

        struct Job
        {
            public DestructibleVoxels owner; public int version; public bool collider;
            public Task<Result> build; public Mesh mesh; public Task bake;
        }

        static PropRemesher instance;
        static readonly List<Job> jobs = new List<Job>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; jobs.Clear(); }

        /// <summary>Mesh <paramref name="snapshot"/> (a grid nobody else touches), cook its collider too when
        /// <paramref name="collider"/>, and hand the mesh to the owner when done. With <paramref name="groundY"/> the
        /// voxels with no path down to that layer are found first and handed back to fall.</summary>
        public static void Queue(DestructibleVoxels owner, VoxelGrid snapshot, float size, int version, bool collider, int? groundY = null)
        {
            if (!instance) instance = new GameObject("PropRemesher").AddComponent<PropRemesher>();
            jobs.Add(new Job { owner = owner, version = version, collider = collider, build = Task.Run(() => Work(snapshot, size, groundY)) });
        }

        /// <summary>Cook MeshCollider data for many meshes on all cores and wait (load time): the first prop of each
        /// template then costs nothing on the main thread (a house is ~30 ms of cooking).</summary>
        public static void Prebake(List<Mesh> meshes)
        {
            var ids = new List<EntityId>(meshes.Count);
            foreach (var m in meshes) if (m && m.vertexCount > 0) ids.Add(m.GetEntityId());
            Parallel.ForEach(ids, id => Physics.BakeMesh(id, false));
        }

        static Result Work(VoxelGrid g, float size, int? groundY)
        {
            var r = new Result();
            if (groundY.HasValue)
            {
                var held = g.Supported(groundY.Value);
                if (held.Count != g.Count)
                {
                    r.fallen = new List<Vector3Int>();
                    foreach (var p in g.voxels.Keys) if (!held.Contains(p)) r.fallen.Add(p);
                    foreach (var p in r.fallen) g.voxels.Remove(p);
                }
            }
            if (g.Count > 0) r.md = VoxelMesher.BuildData(g, size);
            return r;
        }

        void LateUpdate()
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                if (!j.mesh)
                {
                    // stage 1: collapse + arrays ready → drop the fallen voxels, make the Mesh (main thread), cook on a worker
                    if (!j.build.IsCompleted) continue;
                    if (j.build.IsFaulted) { Debug.LogException(j.build.Exception); jobs.RemoveAt(i--); continue; }
                    var r = j.build.Result;
                    if (r.fallen != null && j.owner) j.owner.Collapse(r.fallen);
                    if (!j.owner || r.md == null) { jobs.RemoveAt(i--); continue; }
                    j.mesh = VoxelMesher.ToMesh(r.md, j.owner.name);
                    if (j.collider) { var id = j.mesh.GetEntityId(); j.bake = Task.Run(() => Physics.BakeMesh(id, false)); }
                    jobs[i] = j;
                    continue;
                }
                // stage 2: cooked (or no collider) → swap in
                if (j.bake != null && !j.bake.IsCompleted) continue;
                jobs.RemoveAt(i--);
                if (j.bake != null && j.bake.IsFaulted) Debug.LogException(j.bake.Exception);
                if (j.owner) j.owner.ApplyMesh(j.mesh, j.version); else Destroy(j.mesh);
            }
        }
    }
}
