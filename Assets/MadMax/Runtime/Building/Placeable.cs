using System;
using System.Collections.Generic;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Functional component on a placed piece whose state is saved and replicated (door open, container
    /// contents, plot growth, stored water...). Serialise to a compact string.</summary>
    public interface IPlaceState
    {
        string SaveState();
        void LoadState(string s);
    }

    /// <summary>A built piece (furniture, panel) attached to any surface of the world or a vehicle.
    /// Breaks after a few hammer blows and refunds half its cost as pickups.</summary>
    public class Placeable : MonoBehaviour, IDamageable
    {
        public string id;
        public int hits = 3;
        [System.NonSerialized] public uint netId;
        public string owner;             // builder's name (locks)

        public static event Action Changed;
        public static readonly List<Placeable> All = new List<Placeable>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { All.Clear(); Changed = null; }
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Stable identity (saves, cables, network) — assigned on first placement.</summary>
        public uint Id { get { if (netId == 0) netId = (uint)UnityEngine.Random.Range(1, int.MaxValue); return netId; } }

        public static Placeable ById(uint id) { if (id == 0) return null; foreach (var p in All) if (p && p.netId == id) return p; return null; }

        const char Sep = '\u001e';

        public string SaveState()
        {
            var parts = GetComponents<IPlaceState>();
            if (parts.Length == 0) return null;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++) { if (i > 0) sb.Append(Sep); sb.Append(parts[i].SaveState() ?? ""); }
            return sb.ToString();
        }

        public void LoadState(string state)
        {
            if (string.IsNullOrEmpty(state)) return;
            var parts = GetComponents<IPlaceState>();
            var chunks = state.Split(Sep);
            for (int i = 0; i < parts.Length && i < chunks.Length; i++) parts[i].LoadState(chunks[i]);
        }

        float dirtyAt = -1f;
        /// <summary>A functional state changed locally: replicate it (batched).</summary>
        public void Dirty() { if (dirtyAt < 0f) dirtyAt = Time.unscaledTime + 0.15f; }

        void LateUpdate()
        {
            if (dirtyAt < 0f || Time.unscaledTime < dirtyAt) return;
            dirtyAt = -1f;
            MadMax.Net.NetSession.Instance?.SendPlaceState(this);
        }
        static readonly List<DebrisSystem.Chunk> chunks = new List<DebrisSystem.Chunk>();

        void Start() => Changed?.Invoke();
        void OnDestroy() => Changed?.Invoke();

        public void ApplyHit(Vector3 point, Vector3 direction, float power, float radius, GameObject source)
        {
            hits -= Mathf.Max(1, Mathf.RoundToInt(power));
            Emit(hits > 0 ? 6 : 40, direction);
            if (hits > 0) return;
            MadMax.Net.NetSession.Instance?.SendPlaceBroken(this);
            var def = FurnitureLibrary.Get(id);
            if (TryGetComponent<Container>(out var box)) box.Spill();
            if (def != null && PickupSystem.Instance)
                foreach (var (type, amount) in def.cost)
                    if (amount / 2 > 0) PickupSystem.Instance.Spawn(type, amount / 2, transform.position + Vector3.up * 0.3f, UnityEngine.Random.insideUnitSphere * 1.5f + Vector3.up * 2f);
            Destroy(gameObject);
        }

        void Emit(int count, Vector3 dir)
        {
            var mesh = GetComponent<MeshFilter>().sharedMesh;
            if (!DebrisSystem.Instance || !mesh) return;
            var verts = mesh.vertices; var cols = mesh.colors32;
            chunks.Clear();
            for (int i = 0; i < count; i++)
            {
                int k = UnityEngine.Random.Range(0, verts.Length);
                chunks.Add(new DebrisSystem.Chunk { position = transform.TransformPoint(verts[k]), color = cols[k] });
            }
            DebrisSystem.Instance.Emit(chunks, 0.08f, dir * 2f);
        }
    }
}
