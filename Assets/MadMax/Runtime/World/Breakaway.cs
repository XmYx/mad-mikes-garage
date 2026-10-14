using System.Collections.Generic;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Something a car drives through instead of into (GTA-style towns): street lamps, poles and signs snap
    /// (<see cref="BreakablePole"/>), light street furniture is knocked loose and heavy pieces are ploughed through
    /// (<see cref="StreetFurniture"/>). Every moving vehicle looks one physics step ahead of its body box
    /// (<see cref="Ahead"/>, from <see cref="VehicleDamage"/>): if it carries enough energy the piece gives way before
    /// the solid contact, the car keeps the momentum the piece doesn't take (mass share + energy to break it) and gets a
    /// moderate crunch instead of a wall stop. A contact the look-ahead missed is handed over from
    /// <see cref="VehicleDamage"/> (<see cref="Struck"/>) with the pre-impact velocity rebuilt from the impulse.
    /// Too slow to break: the ordinary solid hit (a pole may bend).</summary>
    public abstract class Breakaway : MonoBehaviour
    {
        /// <summary>Standing pieces that can still give way (zero = vehicles skip the look-ahead).</summary>
        public static int Active;
        const float CellSize = 8f;
        static readonly Dictionary<Vector2Int, List<Breakaway>> cells = new Dictionary<Vector2Int, List<Breakaway>>();
        static readonly Dictionary<string, Breakaway> byKey = new Dictionary<string, Breakaway>();
        static readonly Dictionary<Rigidbody, Bounds> boxes = new Dictionary<Rigidbody, Bounds>();
        static readonly List<Ignore> ignores = new List<Ignore>();
        static readonly List<Breakaway> near = new List<Breakaway>();
        static readonly Vector3[] corners = new Vector3[8];
        static float lastTick = -1f;

        class Ignore { public Collider[] car; public Collider[] prop; public Rigidbody body; public float until, giveUp; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBase() { Active = 0; LastGaveWay = -1f; cells.Clear(); byKey.Clear(); boxes.Clear(); ignores.Clear(); near.Clear(); lastTick = -1f; }

        /// <summary>Stable key (prop state key, lamp key, rail pole key): saves and network messages.</summary>
        public string key;
        /// <summary>Gave way already: no more look-ahead, an ordinary body now.</summary>
        public bool Gone { get; protected set; }
        /// <summary>Last time anything gave way (tests).</summary>
        public static float LastGaveWay = -1f;

        Vector2Int cell;
        bool listed;

        protected virtual void OnEnable() { if (!Gone) List(); if (!string.IsNullOrEmpty(key)) byKey[key] = this; }

        /// <summary>Give it its key once it is set up (components are enabled the moment they are added).</summary>
        public void SetKey(string k)
        {
            if (!string.IsNullOrEmpty(key) && byKey.TryGetValue(key, out var old) && old == this) byKey.Remove(key);
            key = k;
            if (!string.IsNullOrEmpty(key) && isActiveAndEnabled) byKey[key] = this;
        }
        protected virtual void OnDisable() { Unlist(); if (!string.IsNullOrEmpty(key) && byKey.TryGetValue(key, out var b) && b == this) byKey.Remove(key); }

        protected void List()
        {
            if (listed) return;
            var p = transform.position;
            cell = new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
            if (!cells.TryGetValue(cell, out var l)) cells[cell] = l = new List<Breakaway>();
            l.Add(this); listed = true; Active++;
        }

        protected void Unlist()
        {
            if (!listed) return;
            if (cells.TryGetValue(cell, out var l)) { l.Remove(this); if (l.Count == 0) cells.Remove(cell); }
            listed = false; Active--;
        }

        /// <summary>Mark given way: off the look-ahead registry.</summary>
        protected void SetGone() { Gone = true; Unlist(); LastGaveWay = Time.time; }

        /// <summary>World box the bumper can meet (poles: the post up to head height, not the arm over the road).</summary>
        public abstract Bounds HitBounds { get; }

        /// <summary>A vehicle of <paramref name="body"/> moving at <paramref name="v"/> (before the contact) along flat
        /// <paramref name="dir"/> meets it at <paramref name="point"/>. True = it gave way (car velocity already set,
        /// its crunch applied); false = solid (the contact, if any, does its normal damage).</summary>
        protected abstract bool GiveWay(VehicleDamage car, Rigidbody body, Vector3 v, Vector3 point, Vector3 dir, bool contact);

        /// <summary>Another peer saw it give way: the same break here, without a car.</summary>
        protected abstract void RemoteGiveWay(Vector3 point, Vector3 velocity);

        /// <summary>A knock event from the network (unloaded: remembered until it streams in).</summary>
        public static void ApplyRemote(string key, Vector3 point, Vector3 velocity)
        {
            if (key == null) return;
            if (byKey.TryGetValue(key, out var b) && b) { if (!b.Gone) b.RemoteGiveWay(point, velocity); return; }
            BreakablePole.NoteRemote(key, velocity);
        }

        protected void SendKnock(Vector3 point, Vector3 velocity) => MadMax.Net.NetSession.Instance?.SendKnock(key, point, velocity);

        // ------------------------------------------------------------------ look-ahead

        /// <summary>One physics step ahead of a moving vehicle's body box: anything breakable it is about to meet gives
        /// way now (called from <see cref="VehicleDamage"/>.FixedUpdate).</summary>
        public static void Ahead(VehicleDamage car, Rigidbody rb)
        {
            Tick();
            var v = rb.linearVelocity;
            float sp2 = v.x * v.x + v.z * v.z;
            if (sp2 < 9f) return;                                                             // walking pace pushes, doesn't break
            float sp = Mathf.Sqrt(sp2);
            var dir = new Vector3(v.x / sp, 0f, v.z / sp);
            if (!boxes.TryGetValue(rb, out var box)) boxes[rb] = box = BodyBox(rb);
            var t = rb.transform;
            float dist = sp * Time.fixedDeltaTime * 1.25f + 0.05f;
            var lv = t.InverseTransformDirection(dir) * dist;
            var sweep = new Bounds(box.center + lv * 0.5f, box.size + new Vector3(Mathf.Abs(lv.x), Mathf.Abs(lv.y), Mathf.Abs(lv.z)));
            // world cells under the swept box
            var wc = t.TransformPoint(box.center);
            float reach = box.extents.magnitude + dist;
            int x0 = Mathf.FloorToInt((wc.x - reach) / CellSize), x1 = Mathf.FloorToInt((wc.x + reach) / CellSize);
            int z0 = Mathf.FloorToInt((wc.z - reach) / CellSize), z1 = Mathf.FloorToInt((wc.z + reach) / CellSize);
            near.Clear();
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                if (cells.TryGetValue(new Vector2Int(x, z), out var l)) near.AddRange(l);
            foreach (var b in near)
            {
                if (!b || b.Gone) continue;
                var hb = b.HitBounds;
                if (!Overlaps(t, sweep, hb)) continue;
                var p = hb.ClosestPoint(wc);
                b.GiveWay(car, rb, rb.linearVelocity, p, dir, false);                            // fresh: an earlier one this step may have slowed it
            }
        }

        static bool Overlaps(Transform t, Bounds local, Bounds world)
        {
            var mn = world.min; var mx = world.max;
            for (int i = 0; i < 8; i++)
                corners[i] = t.InverseTransformPoint(new Vector3((i & 1) == 0 ? mn.x : mx.x, (i & 2) == 0 ? mn.y : mx.y, (i & 4) == 0 ? mn.z : mx.z));
            var lo = corners[0]; var hi = corners[0];
            for (int i = 1; i < 8; i++) { lo = Vector3.Min(lo, corners[i]); hi = Vector3.Max(hi, corners[i]); }
            return lo.x <= local.max.x && hi.x >= local.min.x && lo.y <= local.max.y && hi.y >= local.min.y && lo.z <= local.max.z && hi.z >= local.min.z;
        }

        /// <summary>The vehicle's collision box in its own space (cached per body).</summary>
        protected static Bounds CarBox(Rigidbody rb)
        {
            if (!boxes.TryGetValue(rb, out var box)) boxes[rb] = box = BodyBox(rb);
            return box;
        }

        /// <summary>The vehicle's collision box in its own space (body and mounted parts), cached per body.</summary>
        static Bounds BodyBox(Rigidbody rb)
        {
            var t = rb.transform;
            var b = new Bounds(); bool any = false;
            foreach (var c in rb.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger || c.attachedRigidbody != rb) continue;
                Bounds lb;
                if (c is BoxCollider bc) lb = new Bounds(bc.center, bc.size);
                else if (c is MeshCollider mc && mc.sharedMesh) lb = mc.sharedMesh.bounds;
                else if (c is SphereCollider sc) lb = new Bounds(sc.center, Vector3.one * sc.radius * 2f);
                else if (c is CapsuleCollider cc) lb = new Bounds(cc.center, Vector3.one * Mathf.Max(cc.radius * 2f, cc.height));
                else continue;
                var ct = c.transform;
                for (int i = 0; i < 8; i++)
                {
                    var q = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var lp = t.InverseTransformPoint(ct.TransformPoint(q));
                    if (!any) { b = new Bounds(lp, Vector3.zero); any = true; } else b.Encapsulate(lp);
                }
            }
            return any ? b : new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(1.8f, 1.2f, 4.4f));
        }

        /// <summary>The contact the look-ahead missed (from <see cref="VehicleDamage"/>): true = it gave way.</summary>
        public static bool Struck(VehicleDamage car, Rigidbody rb, Collider col, Vector3 point, Vector3 normal, float dv)
        {
            if (!col || !rb) return false;
            var b = col.GetComponentInParent<Breakaway>();
            if (!b || b.Gone) return false;
            var before = rb.linearVelocity - normal * dv;                                  // the impulse pushed the car out along the normal
            var dir = before; dir.y = 0f;
            if (dir.sqrMagnitude < 0.25f) { dir = -normal; dir.y = 0f; }
            if (dir.sqrMagnitude < 1e-4f) return false;
            return b.GiveWay(car, rb, before, point, dir.normalized, true);
        }

        // ------------------------------------------------------------------ shared physics

        /// <summary>The car keeps what the piece doesn't take: an inelastic share with its effective mass at the hit,
        /// minus the energy to break it, never below <paramref name="minKeep"/> of the closing speed. Sets the car's
        /// velocity; returns the closing speed lost.</summary>
        protected static float Exchange(Rigidbody car, Vector3 v, Vector3 dir, float effMass, float energy, float minKeep)
        {
            float M = Mathf.Max(50f, car.mass);
            float vn = Vector3.Dot(v, dir);
            if (vn <= 0f) return 0f;
            float vn1 = vn * M / (M + Mathf.Max(0f, effMass));
            float e = 0.5f * vn1 * vn1 - Mathf.Max(0f, energy) / M;
            float vn2 = Mathf.Max(e > 0f ? Mathf.Sqrt(2f * e) : 0f, vn * minKeep);
            car.linearVelocity = v + dir * (vn2 - vn);
            return vn - vn2;
        }

        /// <summary>No contacts between the car and this piece for a while (and on until they no longer overlap).</summary>
        protected void IgnoreCar(Rigidbody car, float seconds)
        {
            var mine = GetComponentsInChildren<Collider>();
            var theirs = car.GetComponentsInChildren<Collider>();
            foreach (var a in theirs) foreach (var b in mine) if (a && b && !a.isTrigger && !b.isTrigger) Physics.IgnoreCollision(a, b, true);
            ignores.Add(new Ignore { car = theirs, prop = mine, body = car, until = Time.time + seconds, giveUp = Time.time + seconds + 10f });
        }

        static void Tick()
        {
            if (Time.fixedTime == lastTick || ignores.Count == 0) { lastTick = Time.fixedTime; return; }
            lastTick = Time.fixedTime;
            for (int i = ignores.Count - 1; i >= 0; i--)
            {
                var ig = ignores[i];
                if (Time.time < ig.until) continue;
                if (ig.body && Time.time < ig.giveUp && Touching(ig)) { ig.until = Time.time + 0.25f; continue; }   // still inside: wait till it's clear
                foreach (var a in ig.car) foreach (var b in ig.prop) if (a && b && !a.isTrigger && !b.isTrigger) Physics.IgnoreCollision(a, b, false);
                ignores.RemoveAt(i);
            }
        }

        static bool Touching(Ignore ig)
        {
            foreach (var b in ig.prop)
            {
                if (!b || !b.enabled) continue;
                var bb = b.bounds; bb.Expand(0.1f);
                foreach (var a in ig.car) if (a && a.enabled && bb.Intersects(a.bounds)) return true;
            }
            return false;
        }

        /// <summary>Its own crunch on the car: <paramref name="dv"/> m/s at the hit (the normal out of the car's face).</summary>
        protected static void Crunch(VehicleDamage car, Vector3 point, Vector3 dir, float dv)
        {
            if (car && dv >= car.minImpactSpeed) car.Crash(point, -dir, dv, null);
        }

        /// <summary>Energy multiplier of the car's front at the hit (rams and plough blades break things easier).</summary>
        protected static float Ram(VehicleDamage car, Vector3 point) => car ? car.RamFactor(point) : 1f;
    }
}
