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
            if (!furnish || furnishings == null) return;
            foreach (var f in furnishings)
                FurnitureLibrary.Spawn(f.id, transform, f.position, Quaternion.Euler(f.euler), furnitureMaterial);
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
