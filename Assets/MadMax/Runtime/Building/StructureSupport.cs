using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Structural support: a built piece stands if a chain of touching pieces leads down to the ground
    /// (terrain, rock, a world building). When a piece goes, everything left hanging collapses, lowest first.</summary>
    public static class StructureSupport
    {
        static readonly List<Placeable> near = new List<Placeable>();
        static readonly List<Bounds> boxes = new List<Bounds>();
        static readonly Queue<int> open = new Queue<int>();
        static readonly Collider[] overlap = new Collider[16];
        static bool[] held = new bool[64];

        /// <summary>A piece at <paramref name="at"/> was broken or dismantled: collapse whatever lost its support.</summary>
        public static void Removed(Placeable gone, Vector3 at, Transform root)
        {
            if (!root || root.GetComponentInParent<Rigidbody>() || (MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient)) return;   // pieces on vehicles ride on the chassis
            near.Clear(); boxes.Clear();
            foreach (var p in Placeable.All)
            {
                if (!p || p == gone || p.transform.parent != root || p.Collapsing) continue;
                if ((p.transform.position - at).sqrMagnitude > 40f * 40f) continue;
                var r = p.GetComponent<Renderer>();
                if (!r) continue;
                var b = r.bounds; b.Expand(0.1f);
                near.Add(p); boxes.Add(b);
            }
            if (near.Count == 0) return;
            if (held.Length < near.Count) held = new bool[near.Count * 2];
            open.Clear();
            for (int i = 0; i < near.Count; i++) { held[i] = Grounded(near[i], boxes[i], gone); if (held[i]) open.Enqueue(i); }
            while (open.Count > 0)
            {
                int i = open.Dequeue();
                for (int j = 0; j < near.Count; j++)
                    if (!held[j] && boxes[i].Intersects(boxes[j])) { held[j] = true; open.Enqueue(j); }
            }
            var fall = new List<Placeable>();
            for (int i = 0; i < near.Count; i++) if (!held[i]) fall.Add(near[i]);
            fall.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
            for (int i = 0; i < fall.Count; i++) fall[i].Collapse(0.12f + i * 0.07f);
            if (fall.Count > 0) MadMax.Audio.Sfx.Play("crash_big", at, 0.8f, 0.8f);
        }

        /// <summary>Touches something that is not a built piece, a vehicle or a person (terrain, rocks, ruins).</summary>
        public static bool Grounded(Placeable p, Bounds b, Placeable ignore = null)
        {
            var t = MadMax.World.DeformableTerrain.Instance;
            if (t && t.World != null)
            {
                float lowest = float.MaxValue;
                for (int k = 0; k < 5; k++)
                {
                    float x = k == 4 ? b.center.x : (k & 1) == 0 ? b.min.x + 0.15f : b.max.x - 0.15f;
                    float z = k == 4 ? b.center.z : (k & 2) == 0 ? b.min.z + 0.15f : b.max.z - 0.15f;
                    lowest = Mathf.Min(lowest, b.min.y - t.Height(x, z));
                }
                if (lowest < 0.15f) return true;
            }
            int n = Physics.OverlapBoxNonAlloc(b.center, b.extents, overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = overlap[i];
                if (c.transform.IsChildOf(p.transform)) continue;
                if (c.GetComponentInParent<Placeable>() || c.attachedRigidbody || c is CharacterController) continue;
                return true;
            }
            return false;
        }
    }
}
