using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Drivable decks of built pieces (floors, foundations, ramps, the garage slab). Wheels sample the terrain
    /// analytically; this adds the upright decks on top so vehicles can drive up ramps and park on foundations.</summary>
    public static class StructureGround
    {
        struct Deck { public Object owner; public Collider collider; public bool sloped; public Matrix4x4 toLocal, toWorld; public Vector4 size; public Vector3 centre; public float reach2; }
        static readonly List<Deck> decks = new List<Deck>();
        static readonly int[] nearBuf = new int[16];
        static readonly HashSet<(Collider, Rigidbody)> ignored = new HashSet<(Collider, Rigidbody)>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { decks.Clear(); ignored.Clear(); }

        public static int Count => decks.Count;

        public static void Add(Placeable p, Vector4 size)
        {
            Remove(p);
            if (p.transform.up.y < 0.9f || p.GetComponentInParent<Rigidbody>()) return;       // only upright decks on the ground
            AddDeck(p, p.transform, p.GetComponent<Collider>(), size);
        }

        /// <summary>A deck that is not a built piece (the title's lunar stage): half extents x/z, top heights at −z/+z.</summary>
        public static void AddDeck(Object owner, Transform t, Collider collider, Vector4 size)
        {
            float r = Mathf.Sqrt(size.x * size.x + size.y * size.y) + 2.5f;           // + a car overhang (ramp lips)
            decks.Add(new Deck { owner = owner, collider = collider, sloped = size.z != size.w, toLocal = t.worldToLocalMatrix, toWorld = t.localToWorldMatrix, size = size, centre = t.position, reach2 = r * r });
        }

        public static void Remove(Object owner)
        {
            for (int i = decks.Count - 1; i >= 0; i--) if (decks[i].owner == owner || !decks[i].owner) decks.RemoveAt(i);
        }

        /// <summary>Raise <paramref name="height"/> to the highest deck under <paramref name="at"/> that lies below it
        /// (a wheel's suspension anchor). Returns true (with the deck normal) when a deck wins over the terrain.</summary>
        public static bool Top(Vector3 at, ref float height, out Vector3 normal, Rigidbody body = null)
        {
            normal = Vector3.up;
            bool found = false;
            int near = 0;
            for (int i = 0; i < decks.Count; i++)
            {
                var d = decks[i];
                float dx = at.x - d.centre.x, dz = at.z - d.centre.z;
                if (dx * dx + dz * dz > d.reach2) continue;
                if (body && d.sloped && d.collider) Ignore(d.collider, body);
                if (body && near < nearBuf.Length) nearBuf[near++] = i;
                var l = d.toLocal.MultiplyPoint3x4(at);
                if (Mathf.Abs(l.x) > d.size.x || Mathf.Abs(l.z) > d.size.y) continue;
                float f = (l.z + d.size.y) / (2f * d.size.y);
                var top = d.toWorld.MultiplyPoint3x4(new Vector3(l.x, Mathf.Lerp(d.size.z, d.size.w, f), l.z));
                if (top.y > at.y + 0.05f || top.y <= height) continue;
                height = top.y; found = true;
                normal = d.toWorld.MultiplyVector(new Vector3(0f, 2f * d.size.y, d.size.z - d.size.w)).normalized;
            }
            // carried by a deck: the body no longer snags on the edges of the decks around it (lips, seams)
            if (found) for (int k = 0; k < near; k++) { var c = decks[nearBuf[k]].collider; if (c) Ignore(c, body); }
            return found;
        }

        /// <summary>Decks carry vehicles on their wheels (analytically): the body never snags on a ramp's lip or slope.</summary>
        static void Ignore(Collider ramp, Rigidbody body)
        {
            if (!ignored.Add((ramp, body))) return;
            foreach (var c in body.GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(ramp, c, true);
        }
    }
}
