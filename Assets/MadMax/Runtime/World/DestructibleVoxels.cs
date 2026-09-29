using System.Collections.Generic;
using MadMax.Items;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A voxel object that can be destroyed where it is hit: voxels are carved out (edges crack),
    /// unsupported parts collapse, removed voxels fly off as pixel-cube debris and yield resource pickups.
    /// Pristine instances share a template grid/mesh; the first hit makes a private copy, stored in the
    /// terrain's destruction state so damage survives chunk streaming.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class DestructibleVoxels : MonoBehaviour, IDamageable
    {
        public float voxelSize = VoxelMesher.DefaultSize;
        [Tooltip("Voxels not connected to the ground layer collapse.")]
        public bool anchored = true;
        [Tooltip("Relative speed (m/s) a rigidbody must exceed to break this on impact.")]
        public float impactThreshold = 3.5f;
        [Tooltip("When the remaining fraction drops below this, the whole object breaks apart (crates, furniture).")]
        [Range(0f, 1f)] public float shatterBelow;
        [Tooltip("Extra pickups rolled when the object is fully destroyed (loot).")]
        public ResourceType[] loot;
        public int lootRolls;

        VoxelGrid grid;
        bool owned, pendingRebuild;
        Mesh ownMesh;
        MeshFilter mf;
        Collider col;
        int initialCount, groundY;
        string stateKey, templateId;
        Dictionary<string, VoxelGrid> stateStore;
        readonly float[] yieldAcc = new float[ResourceInfo.Count];

        static readonly List<Vector3Int> removedKeys = new List<Vector3Int>();
        static readonly List<DebrisSystem.Chunk> debris = new List<DebrisSystem.Chunk>();
        static readonly List<Vox> removedVox = new List<Vox>();
        static readonly Color32 CrackColor = new Color32(12, 8, 6, 255);

        public int VoxelCount => grid != null ? grid.Count : 0;
        /// <summary>Current voxels (the shared template until the first hit). Read-only for other systems.</summary>
        public VoxelGrid Grid => grid;
        public string TemplateId => templateId;
        public string StateKey => stateKey;
        /// <summary>Raised after voxels were carved away (overlays such as <see cref="Overgrowth"/> refresh).</summary>
        public event System.Action Carved;

        static readonly Dictionary<string, DestructibleVoxels> byKey = new Dictionary<string, DestructibleVoxels>();
        static readonly Dictionary<string, List<(Vector3 p, Vector3 d, float r, float power)>> pendingCarves = new Dictionary<string, List<(Vector3, Vector3, float, float)>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { byKey.Clear(); pendingCarves.Clear(); All.Clear(); }

        /// <summary>Apply a carve that happened on another peer (no pickups here). Unloaded props queue it until they spawn.</summary>
        public static void ApplyRemoteCarve(string key, Vector3 point, Vector3 dir, float radius, float power)
        {
            if (byKey.TryGetValue(key, out var d) && d) { d.Carve(point, dir, radius, power, dir * 2f, false); return; }
            if (!pendingCarves.TryGetValue(key, out var list)) pendingCarves[key] = list = new List<(Vector3, Vector3, float, float)>();
            list.Add((point, dir, radius, power));
        }

        void OnEnable() { All.Add(this); if (stateKey != null) byKey[stateKey] = this; }
        void OnDisable() { All.Remove(this); if (stateKey != null && byKey.TryGetValue(stateKey, out var d) && d == this) byKey.Remove(stateKey); }

        /// <summary>Spawn an instance. Returns null if the saved state says it was destroyed completely.</summary>
        public static DestructibleVoxels Spawn(string name, VoxelGrid template, Mesh sharedMesh, Material mat, Transform parent,
            Vector3 position, float yaw, bool dynamicBody, Dictionary<string, VoxelGrid> store, string key, float size = VoxelMesher.DefaultSize, string templateId = null)
        {
            VoxelGrid saved = null;
            if (store != null && key != null && store.TryGetValue(key, out saved) && saved.Count == 0) return null;

            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var d = go.AddComponent<DestructibleVoxels>();
            d.voxelSize = size;
            d.mf = go.GetComponent<MeshFilter>();
            d.stateStore = store; d.stateKey = key; d.templateId = templateId;
            d.initialCount = template.Count;
            d.groundY = template.MinY();
            d.grid = saved ?? template;
            d.owned = saved != null;
            if (dynamicBody)
            {
                d.col = go.AddComponent<BoxCollider>();
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = Mathf.Max(5f, template.Count * size * size * size * 600f);
                d.anchored = false;
            }
            else d.col = go.AddComponent<MeshCollider>();
            if (!d.owned) d.SetMesh(sharedMesh);
            else if (d.grid.Count < AsyncVoxels || !sharedMesh) d.SetMesh(d.ownMesh = VoxelMesher.Build(d.grid, name, size));
            else { d.SetMesh(sharedMesh); d.Remesh(); }          // a damaged building streaming back in: the template until its mesh is ready
            if (key != null)
            {
                byKey[key] = d;
                if (pendingCarves.TryGetValue(key, out var queued))
                {
                    pendingCarves.Remove(key);
                    foreach (var q in queued) d.Carve(q.p, q.d, q.r, q.power, Vector3.zero, false);
                    if (!d) return null;
                }
            }
            return d;
        }

        void SetMesh(Mesh m)
        {
            mf.sharedMesh = m;
            if (col is MeshCollider mc) { mc.sharedMesh = null; mc.sharedMesh = m; }
            else if (col is BoxCollider bc) { bc.center = m.bounds.center; bc.size = m.bounds.size; }
        }

        void OnDestroy() { if (ownMesh) Destroy(ownMesh); }

        public static readonly List<DestructibleVoxels> All = new List<DestructibleVoxels>();

        void Broadcast(Vector3 p, Vector3 d, float r, float power) => MadMax.Net.NetSession.Instance?.SendCarve(stateKey, p, d, r, power);

        public void ApplyHit(Vector3 point, Vector3 direction, float power, float radius, GameObject source)
        {
            Broadcast(point, direction, radius, power);
            Carve(point, direction, radius, power, direction * 2f);
        }

        void OnCollisionEnter(Collision c)
        {
            var other = c.rigidbody;
            if (!other || c.contactCount == 0) return;
            float speed = c.relativeVelocity.magnitude;
            if (speed < impactThreshold) return;
            // kinetic energy -> carve radius (a 1.6 t car at 10 m/s ~ 0.5 m, falling debris ~ nothing)
            float energy = 0.5f * other.mass * speed * speed;
            var dmgSrc = other.GetComponent<MadMax.Vehicles.VehicleDamage>();
            if (dmgSrc) energy *= dmgSrc.RamFactor(c.GetContact(0).point);
            float radius = Mathf.Min(1.1f, Mathf.Pow(energy, 1f / 3f) * 0.012f);
            if (other.GetComponent<MadMax.Vehicles.VehicleChassis>())
                radius = Mathf.Max(radius, Mathf.Lerp(0.3f, 0.95f, (speed - impactThreshold) / 10f)); // cut a car-sized hole
            if (radius < voxelSize * 1.5f) return;
            // carve at each distinct contact so a wide bumper opens a wide hole (one collapse check + remesh at the end)
            int removed = 0;
            var dir = c.GetContact(0).normal;                    // from the other body into this one
            var carved = new List<Vector3>(4);
            for (int i = 0; i < c.contactCount && carved.Count < 4; i++)
            {
                var p = c.GetContact(i).point;
                bool near = false;
                foreach (var q in carved) if ((q - p).sqrMagnitude < radius * radius * 0.5f) { near = true; break; }
                if (!near) carved.Add(p);
            }
            for (int i = 0; i < carved.Count; i++)
            {
                if (!this) return;                                  // destroyed by a previous carve
                Broadcast(carved[i], dir, radius, 1f);
                removed += Carve(carved[i], dir, radius, 1f, other.linearVelocity * 0.5f, true, i == carved.Count - 1);
            }
            if (removed > 15 && this && !GetComponent<Rigidbody>())
            {
                // broke through: give back part of the momentum the solid collision took away
                var v = other.linearVelocity;
                other.linearVelocity = Vector3.ProjectOnPlane(v, dir) + dir * Mathf.Max(Vector3.Dot(v, dir), speed * 0.6f);
            }
        }

        /// <summary>Remove voxels in a sphere pushed slightly into the object. Returns the number removed.
        /// <paramref name="rebuild"/> false defers the collapse check and remesh to the next carve that rebuilds.</summary>
        public int Carve(Vector3 worldPoint, Vector3 worldDir, float radius, float power, Vector3 debrisVelocity, bool yields = true, bool rebuild = true)
        {
            if (grid == null) return 0;
            if (!owned)
            {
                grid = grid.Clone();
                owned = true;
                if (stateStore != null && stateKey != null)
                {
                    stateStore[stateKey] = grid;
                    if (templateId != null && DeformableTerrain.Instance) DeformableTerrain.Instance.DestructionTemplates[stateKey] = templateId;
                }
            }
            var lp = transform.InverseTransformPoint(worldPoint) / voxelSize;
            var ld = transform.InverseTransformDirection(worldDir).normalized;
            float R = radius / voxelSize;
            var center = lp + ld * R * 0.35f;
            var c0 = Vector3Int.RoundToInt(center);
            int ext = Mathf.CeilToInt(R * 1.8f + 2f);

            removedKeys.Clear();
            bool cracked = false;
            for (int x = -ext; x <= ext; x++)
            for (int y = -ext; y <= ext; y++)
            for (int z = -ext; z <= ext; z++)
            {
                var p = c0 + new Vector3Int(x, y, z);
                if (!grid.voxels.TryGetValue(p, out var v)) continue;
                float eff = R * power / Mathf.Sqrt(ResourceInfo.HardnessOf((ResourceType)v.mat));
                float d = Vector3.Distance(p, center) + (Pal.Hash(p, 77) - 0.5f) * 0.9f;   // ragged break edge
                if (d <= eff) removedKeys.Add(p);
                else if (d <= eff + 1.6f)
                {
                    v.color = Color32.Lerp(v.color, CrackColor, 0.3f);
                    grid.voxels[p] = v;
                    cracked = true;
                }
            }

            removedVox.Clear();
            debris.Clear();
            foreach (var p in removedKeys) Take(p);
            bool shattered = false, deferred = pendingRebuild, lateCollapse = false;
            if (rebuild)
            {
                if (anchored && (removedKeys.Count > 0 || deferred))
                {
                    if (grid.Count < AsyncVoxels) CollapseUnsupported();
                    else lateCollapse = true;                                  // big: checked on a worker with the remesh
                }
                shattered = shatterBelow > 0f && grid.Count < initialCount * shatterBelow;
                if (shattered)
                {
                    removedKeys.Clear();
                    removedKeys.AddRange(grid.voxels.Keys);
                    foreach (var p in removedKeys) Take(p);
                }
            }
            if (removedVox.Count == 0 && !cracked && !(rebuild && deferred)) return 0;

            if (DebrisSystem.Instance && debris.Count > 0) DebrisSystem.Instance.Emit(debris, voxelSize, debrisVelocity);
            if (yields) Yield(worldPoint, rebuild && (shattered || grid.Count == 0));
            if (!rebuild) { pendingRebuild = true; return removedVox.Count; }
            pendingRebuild = false;

            if (grid.Count == 0) { Destroy(gameObject); return removedVox.Count; }
            Remesh(lateCollapse);
            if (removedVox.Count > 0 || deferred) Carved?.Invoke();
            return removedVox.Count;
        }

        /// <summary>Drop voxels the worker found cut off from the ground (<see cref="PropRemesher"/>): debris, pickups.</summary>
        public void Collapse(List<Vector3Int> fallen)
        {
            removedVox.Clear(); debris.Clear();
            Vector3 sum = Vector3.zero;
            foreach (var p in fallen) { if (grid.voxels.ContainsKey(p)) sum += (Vector3)p; Take(p); }
            if (removedVox.Count == 0) return;
            if (DebrisSystem.Instance) DebrisSystem.Instance.Emit(debris, voxelSize, Vector3.zero);
            Yield(transform.TransformPoint(sum / removedVox.Count * voxelSize), grid.Count == 0);
            if (grid.Count == 0) { Destroy(gameObject); return; }
            Carved?.Invoke();
        }

        const int AsyncVoxels = 3000;       // bigger grids mesh on a worker thread (PropRemesher)
        int meshVersion, appliedVersion;

        /// <summary>New mesh + collider for the current grid: at once for small props, a few frames later for big ones.</summary>
        void Remesh(bool collapse = false)
        {
            meshVersion++;
            if (grid.Count < AsyncVoxels) ApplyMesh(VoxelMesher.Build(grid, name, voxelSize), meshVersion);
            else PropRemesher.Queue(this, grid.Clone(), voxelSize, meshVersion, col is MeshCollider, collapse ? groundY : (int?)null);
        }

        /// <summary>Swap in a mesh built from the grid as it was at carve <paramref name="version"/> (older results are dropped).</summary>
        public void ApplyMesh(Mesh m, int version)
        {
            if (version <= appliedVersion) { Destroy(m); return; }
            appliedVersion = version;
            var old = ownMesh;
            ownMesh = m;
            SetMesh(ownMesh);
            if (old) Destroy(old);
        }

        void Take(Vector3Int p)
        {
            if (!grid.voxels.TryGetValue(p, out var v)) return;
            grid.voxels.Remove(p);
            removedVox.Add(v);
            debris.Add(new DebrisSystem.Chunk { position = transform.TransformPoint((Vector3)p * voxelSize), color = v.color });
        }

        void CollapseUnsupported()
        {
            var visited = grid.Supported(groundY);
            if (visited.Count == grid.Count) return;
            removedKeys.Clear();
            foreach (var p in grid.voxels.Keys) if (!visited.Contains(p)) removedKeys.Add(p);
            foreach (var p in removedKeys) Take(p);
        }

        void Yield(Vector3 at, bool destroyed)
        {
            var pickups = PickupSystem.Instance;
            if (!pickups) return;
            foreach (var v in removedVox) yieldAcc[v.mat] += ResourceInfo.YieldPerVoxel((ResourceType)v.mat) * MadMax.Game.GameRules.Current.yield;
            for (int t = 1; t < yieldAcc.Length; t++)
            {
                int units = Mathf.FloorToInt(yieldAcc[t]);
                if (units <= 0) continue;
                yieldAcc[t] -= units;
                // bundle big yields: at most 5 pickups per material per event
                int bundles = Mathf.Min(5, units), per = Mathf.CeilToInt(units / (float)bundles);
                for (int i = 0; i < bundles && units > 0; i++, units -= per)
                    pickups.Spawn((ResourceType)t, Mathf.Min(per, units), at + Random.insideUnitSphere * 0.3f + Vector3.up * 0.3f, Random.insideUnitSphere * 2f + Vector3.up * 2.5f);
            }
            if (destroyed && loot != null && loot.Length > 0)
                for (int i = 0; i < lootRolls; i++)
                    pickups.Spawn(loot[Random.Range(0, loot.Length)], Random.Range(1, 4), at + Vector3.up * 0.4f, Random.insideUnitSphere * 1.5f + Vector3.up * 3f);
        }
    }
}
