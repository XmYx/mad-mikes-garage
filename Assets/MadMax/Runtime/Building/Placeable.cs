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
        public byte dye;                 // paint (Pal.DyeRamp index), 0 = as built

        public static event Action Changed;
        public static readonly List<Placeable> All = new List<Placeable>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { All.Clear(); Changed = null; }
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Stable identity (saves, cables, network) — assigned on first placement.</summary>
        public uint Id { get { if (netId == 0) netId = (uint)UnityEngine.Random.Range(1, int.MaxValue); return netId; } }

        public static Placeable ById(uint id) { if (id == 0) return null; foreach (var p in All) if (p && p.netId == id) return p; return null; }

        const char Sep = '\u001e', DyeMark = '\u001f';

        /// <summary>Repaint the piece (dye items): swaps in a recoloured copy of its mesh.</summary>
        public void SetDye(int d)
        {
            dye = (byte)Mathf.Clamp(d, 0, 6);
            var def = FurnitureLibrary.Get(id);
            if (def != null && TryGetComponent<MeshFilter>(out var mf)) mf.sharedMesh = Dyes.MeshFor(def, dye);
            if (TryGetComponent<MadMax.Rendering.HDVisual>(out var hd)) hd.SetTint(dye == 0 ? null : FurnitureLibrary.DyeTint(dye));   // HD look: tinted
        }

        /// <summary>Functional state of the piece's components (+ its paint, as a leading "\u001f n \u001f" block).</summary>
        public string SaveState()
        {
            var parts = GetComponents<IPlaceState>();
            if (parts.Length == 0 && dye == 0) return null;
            var sb = new System.Text.StringBuilder();
            if (dye != 0) sb.Append(DyeMark).Append(dye).Append(DyeMark);
            for (int i = 0; i < parts.Length; i++) { if (i > 0) sb.Append(Sep); sb.Append(parts[i].SaveState() ?? ""); }
            return sb.ToString();
        }

        public void LoadState(string state)
        {
            if (string.IsNullOrEmpty(state)) return;
            if (state[0] == DyeMark)
            {
                int end = state.IndexOf(DyeMark, 1);
                if (end > 1 && int.TryParse(state.Substring(1, end - 1), out int d)) SetDye(d);
                state = end > 0 ? state.Substring(end + 1) : "";
            }
            else if (dye != 0) SetDye(0);
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

        void Start()
        {
            Changed?.Invoke();
            var def = FurnitureLibrary.Get(id);
            if (def != null && def.deck != Vector4.zero) StructureGround.Add(this, def.deck);
        }
        void OnDestroy() { Changed?.Invoke(); StructureGround.Remove(this); }

        /// <summary>Full condition of this piece (its definition's hit count).</summary>
        public int MaxHits { get { var def = FurnitureLibrary.Get(id); return def != null ? def.hits : Mathf.Max(1, hits); } }

        /// <summary>Losing its support: comes down after <paramref name="delay"/> seconds (a quarter of the cost survives).</summary>
        public bool Collapsing { get; private set; }
        public void Collapse(float delay) { if (Collapsing) return; Collapsing = true; Invoke(nameof(CollapseNow), delay); }
        void CollapseNow() { Emit(30, Vector3.down); Break(4); }

        public void ApplyHit(Vector3 point, Vector3 direction, float power, float radius, GameObject source)
        {
            if (Collapsing) return;
            hits -= Mathf.Max(1, Mathf.RoundToInt(power));
            Emit(hits > 0 ? 6 : 40, direction);
            if (hits > 0 && hits <= 2) MadMax.Audio.Sfx.Play("creak", point, 0.5f, UnityEngine.Random.Range(0.9f, 1.2f), 30f, 1f);   // about to give
            if (hits > 0) return;
            Break(2);
        }

        /// <summary>Destroyed: spills its container, drops 1/<paramref name="refundDiv"/> of the cost, and whatever it
        /// held up collapses.</summary>
        void Break(int refundDiv)
        {
            MadMax.Net.NetSession.Instance?.SendPlaceBroken(this);
            var def = FurnitureLibrary.Get(id);
            if (TryGetComponent<Container>(out var box)) box.Spill();
            if (TryGetComponent<UtilityNode>(out var un)) un.Unlink();
            if (def != null && PickupSystem.Instance)
                foreach (var (type, amount) in def.cost)
                    if (amount / refundDiv > 0) PickupSystem.Instance.Spawn(type, amount / refundDiv, transform.position + Vector3.up * 0.3f, UnityEngine.Random.insideUnitSphere * 1.5f + Vector3.up * 2f);
            Collapsing = true;
            StructureSupport.Removed(this, transform.position, transform.parent);
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
