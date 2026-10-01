using System;
using System.Collections.Generic;
using MadMax.Building;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Walk-in vehicle interior. The player moves kinematically in this vehicle's local space (stable while driving),
    /// collides with static obstacles and floor-standing Placeables, and can sit in the driver seat.
    /// All positions are local metres.</summary>
    public class InteriorSpace : MonoBehaviour
    {
        [Serializable]
        public struct Door
        {
            public Vector3 inside;
            public Vector3 outside;   // y ignored (placed on terrain)
        }

        public float floorY, ceilingY;
        /// <summary>Sealed (a submarine): dry and breathable inside even under water.</summary>
        public bool airtight;
        public Vector2 min, max;
        public Door[] doors;
        public Vector3 seat, stand;
        public Bounds[] obstacles;
        [Serializable] public struct Furnishing { public string id; public Vector3 position, euler; }
        public Furnishing[] furnishings;
        public Material furnitureMaterial;
        public bool furnish = true;

        readonly List<Bounds> dynamicObstacles = new List<Bounds>();
        bool obstaclesDirty = true;
        Renderer[] cutawayRenderers;
        Collider[] cutawayColliders;
        bool cutaway;

        void OnEnable() { Placeable.Changed += MarkDirty; }
        void OnDisable() { Placeable.Changed -= MarkDirty; SetCutaway(false); }
        void MarkDirty() => obstaclesDirty = true;

        void Start()
        {
            CabinPower.Fit(this);                                                               // house battery (lights block)
            if (!furnish || furnishings == null) return;
            foreach (var f in furnishings)
                FurnitureLibrary.Spawn(f.id, transform, f.position, Quaternion.Euler(f.euler), furnitureMaterial);
            CabinPower.Furnish(this);                                                           // cabin lights + switch
        }

        public Vector3 SeatWorld => transform.TransformPoint(seat);

        /// <summary>Hide the roof (mesh, colliders, roof-mounted parts) so top-down views can see inside.</summary>
        public void SetCutaway(bool on)
        {
            if (on == cutaway) return;
            cutaway = on;
            if (cutawayRenderers == null)
            {
                var r = new List<Renderer>(); var c = new List<Collider>();
                var roof = transform.Find("Body/Roof");
                if (roof) r.AddRange(roof.GetComponentsInChildren<Renderer>());
                foreach (var col in GetComponentsInChildren<Collider>()) if (col.name == "RoofCollider") c.Add(col);
                cutawayRenderers = r.ToArray(); cutawayColliders = c.ToArray();
            }
            foreach (var r in cutawayRenderers) if (r) r.enabled = !on;
            foreach (var c in cutawayColliders) if (c) c.enabled = !on;
            // anything mounted on the roof
            foreach (var s in GetComponentsInChildren<MountSocket>())
                if (s.transform.localPosition.y > ceilingY - 0.05f && s.Current)
                    foreach (var r in s.Current.GetComponentsInChildren<Renderer>()) r.enabled = !on;
        }

        void RefreshObstacles()
        {
            obstaclesDirty = false;
            dynamicObstacles.Clear();
            foreach (var p in GetComponentsInChildren<Placeable>())
            {
                var box = p.GetComponent<BoxCollider>();
                if (!box) continue;
                // local AABB of the placed piece in this vehicle's space
                var m = transform.worldToLocalMatrix * p.transform.localToWorldMatrix;
                var b = new Bounds(m.MultiplyPoint3x4(box.center), Vector3.zero);
                var e = box.size * 0.5f;
                for (int i = 0; i < 8; i++)
                    b.Encapsulate(m.MultiplyPoint3x4(box.center + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                if (b.min.y < floorY + 1.4f) dynamicObstacles.Add(b);   // only pieces a person would bump into
            }
        }

        /// <summary>Clamp a desired local position to the walkable area, pushing out of obstacles.</summary>
        public Vector3 Resolve(Vector3 local, float radius)
        {
            if (obstaclesDirty) RefreshObstacles();
            local.x = Mathf.Clamp(local.x, min.x, max.x);
            local.z = Mathf.Clamp(local.z, min.y, max.y);
            if (obstacles != null) foreach (var b in obstacles) local = PushOut(local, b, radius);
            foreach (var b in dynamicObstacles) local = PushOut(local, b, radius);
            local.x = Mathf.Clamp(local.x, min.x, max.x);
            local.z = Mathf.Clamp(local.z, min.y, max.y);
            local.y = floorY;
            return local;
        }

        static Vector3 PushOut(Vector3 p, Bounds b, float r)
        {
            float cx = Mathf.Clamp(p.x, b.min.x, b.max.x), cz = Mathf.Clamp(p.z, b.min.z, b.max.z);
            float dx = p.x - cx, dz = p.z - cz, d2 = dx * dx + dz * dz;
            if (d2 >= r * r) return p;
            if (d2 > 1e-6f)
            {
                float d = Mathf.Sqrt(d2), push = r - d;
                p.x += dx / d * push; p.z += dz / d * push;
                return p;
            }
            // centre inside the box: leave by the nearest face
            float l = p.x - b.min.x, rr = b.max.x - p.x, bk = p.z - b.min.z, f = b.max.z - p.z;
            float m = Mathf.Min(Mathf.Min(l, rr), Mathf.Min(bk, f));
            if (m == l) p.x = b.min.x - r; else if (m == rr) p.x = b.max.x + r; else if (m == bk) p.z = b.min.z - r; else p.z = b.max.z + r;
            return p;
        }

        /// <summary>Can a person of <paramref name="radius"/> stand here (inside the walkable area, clear of every
        /// obstacle and floor-standing piece)?</summary>
        public bool Free(Vector3 local, float radius)
        {
            if (obstaclesDirty) RefreshObstacles();
            if (local.x < min.x - 1e-3f || local.x > max.x + 1e-3f || local.z < min.y - 1e-3f || local.z > max.y + 1e-3f) return false;
            float r2 = radius * radius - 1e-5f;
            if (obstacles != null) foreach (var b in obstacles) if (Dist2(local, b) < r2) return false;
            foreach (var b in dynamicObstacles) if (Dist2(local, b) < r2) return false;
            return true;
        }

        static float Dist2(Vector3 p, Bounds b)
        {
            float dx = p.x - Mathf.Clamp(p.x, b.min.x, b.max.x), dz = p.z - Mathf.Clamp(p.z, b.min.z, b.max.z);
            return dx * dx + dz * dz;
        }

        const float Step = 0.05f;
        int gw, gh; float sx, sz;
        bool[] open; int[] queue;

        void Grid(float radius)
        {
            gw = Mathf.Max(2, Mathf.CeilToInt((max.x - min.x) / Step) + 1); gh = Mathf.Max(2, Mathf.CeilToInt((max.y - min.y) / Step) + 1);
            sx = (max.x - min.x) / (gw - 1); sz = (max.y - min.y) / (gh - 1);                     // samples include both bounds
            if (open == null || open.Length < gw * gh) { open = new bool[gw * gh]; queue = new int[gw * gh]; }
            for (int j = 0; j < gh; j++) for (int i = 0; i < gw; i++) open[j * gw + i] = Free(new Vector3(min.x + i * sx, floorY, min.y + j * sz), radius);
        }

        int Cell(Vector3 local) => Mathf.Clamp(Mathf.RoundToInt((local.z - min.y) / sz), 0, gh - 1) * gw + Mathf.Clamp(Mathf.RoundToInt((local.x - min.x) / sx), 0, gw - 1);
        Vector3 At(int c) => new Vector3(min.x + (c % gw) * sx, floorY, min.y + (c / gw) * sz);

        /// <summary>Flood the free floor from <paramref name="from"/>; returns the cells reached (in <c>queue</c>).</summary>
        int Flood(Vector3 from, float radius, bool[] seen)
        {
            int start = Cell(from);
            if (!open[start])                                                                   // standing a touch inside something: the nearest open cell
            {
                float best = float.MaxValue; int bi = -1;
                for (int c = 0; c < gw * gh; c++) if (open[c]) { float d = (At(c) - from).sqrMagnitude; if (d < best) { best = d; bi = c; } }
                if (bi < 0 || best > 0.5f * 0.5f) return 0;
                start = bi;
            }
            System.Array.Clear(seen, 0, gw * gh);
            int head = 0, tail = 0;
            queue[tail++] = start; seen[start] = true;
            while (head < tail)
            {
                int c = queue[head++], ci = c % gw, cj = c / gw;
                for (int k = 0; k < 4; k++)
                {
                    int ni = ci + (k == 0 ? 1 : k == 1 ? -1 : 0), nj = cj + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (ni < 0 || nj < 0 || ni >= gw || nj >= gh) continue;
                    int n = nj * gw + ni;
                    if (seen[n] || !open[n]) continue;
                    seen[n] = true; queue[tail++] = n;
                }
            }
            return tail;
        }

        bool[] seenBuf;

        /// <summary>The door (index) a person of <paramref name="radius"/> standing at <paramref name="from"/> can walk to
        /// past the obstacles and furniture, or -1.</summary>
        public int ReachableDoor(Vector3 from, float radius)
        {
            if (doors == null || doors.Length == 0) return -1;
            Grid(radius);
            if (seenBuf == null || seenBuf.Length < gw * gh) seenBuf = new bool[gw * gh];
            if (Flood(from, radius, seenBuf) == 0) return -1;
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < doors.Length; i++)
            {
                var d = doors[i].inside;
                // the door counts when an open reached cell lies within reach of it (its exit prompt shows at 1.4 m)
                for (int c = 0; c < gw * gh; c++)
                {
                    if (!seenBuf[c]) continue;
                    float dd = Vector2.Distance(new Vector2(At(c).x, At(c).z), new Vector2(d.x, d.z));
                    if (dd < 1.2f && dd < bd) { bd = dd; best = i; }
                }
            }
            return best;
        }

        /// <summary>Where someone getting up from the driver seat stands: <see cref="stand"/> when it is clear and a
        /// door can be reached from it, else the nearest clear spot that reaches a door (furniture can never box the
        /// driver in). False: nothing inside reaches a door, leave the vehicle outright.</summary>
        public bool FindExitSpot(float radius, out Vector3 spot)
        {
            spot = stand;
            if (Free(stand, radius) && ReachableDoor(stand, radius) >= 0) return true;
            // the open floor around any door, nearest the seat
            if (doors == null || doors.Length == 0) return false;
            Grid(radius);
            if (seenBuf == null || seenBuf.Length < gw * gh) seenBuf = new bool[gw * gh];
            float best = float.MaxValue; bool found = false;
            for (int i = 0; i < doors.Length; i++)
            {
                if (Flood(doors[i].inside, radius, seenBuf) == 0) continue;
                for (int c = 0; c < gw * gh; c++)
                {
                    if (!seenBuf[c]) continue;
                    var p = At(c);
                    float d = (p - stand).sqrMagnitude;
                    if (d < best) { best = d; spot = p; found = true; }
                }
            }
            return found;
        }

        float trappedAt = -9f; bool trapped;

        /// <summary>No door can be reached from here (re-checked at most twice a second, or when the furniture changes).</summary>
        public bool Trapped(Vector3 local, float radius)
        {
            if (Time.time - trappedAt < 0.5f && !obstaclesDirty) return trapped;
            trappedAt = Time.time;
            trapped = doors != null && doors.Length > 0 && ReachableDoor(local, radius) < 0;
            return trapped;
        }

        public int NearestDoorInside(Vector3 local, float maxDist)
        {
            int best = -1; float bd = maxDist;
            for (int i = 0; i < doors.Length; i++)
            {
                float d = Vector2.Distance(new Vector2(local.x, local.z), new Vector2(doors[i].inside.x, doors[i].inside.z));
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public int NearestDoorOutside(Vector3 world, float maxDist)
        {
            int best = -1; float bd = maxDist;
            for (int i = 0; i < doors.Length; i++)
            {
                var o = transform.TransformPoint(doors[i].outside);
                float d = Vector2.Distance(new Vector2(world.x, world.z), new Vector2(o.x, o.z));
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }
    }
}
