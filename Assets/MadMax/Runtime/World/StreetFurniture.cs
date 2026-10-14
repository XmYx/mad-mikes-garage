using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Street furniture a car drives through (see <see cref="Breakaway"/>). <see cref="Mode.Loose"/> (benches,
    /// bins, bollards, hydrants, phone booths, drums): torn off its bolts and flung ahead as a rigid body, a light
    /// crunch, the car barely slowed; it stays where it lands (<see cref="DestructibleVoxels.Moved"/>, saved).
    /// <see cref="Mode.Plough"/> (planters, hedges, park trees and bushes, island statues and fountains, bus shelters):
    /// a car-wide channel is carved through it along the travel (debris, pickups, carve broadcast), the car pays for
    /// what it smashed but keeps at least <see cref="minKeep"/> of its speed.</summary>
    public class StreetFurniture : Breakaway
    {
        public enum Mode { Loose, Plough }

        public Mode mode;
        /// <summary>Loose: body mass (kg) and the energy (J) that tears it off its bolts.</summary>
        public float mass = 60f, looseEnergy = 2500f;
        /// <summary>Plough: energy per cubic metre smashed (J/m³) and the least share of the speed kept.</summary>
        public float ploughCost = 25000f, minKeep = 0.62f;

        /// <summary>Plough: only a trunk this thick (m) stands in the way (trees: the crown is above the cars).</summary>
        public float trunk;

        DestructibleVoxels dv;
        Rigidbody body, ploughing;
        float retryAt, quietUntil;

        public override Bounds HitBounds
        {
            get
            {
                var p = transform.position;
                if (trunk > 0f) return new Bounds(p + Vector3.up, new Vector3(trunk * 2f, 2f, trunk * 2f));
                var c = dv ? dv.GetComponent<Collider>() : null;
                if (!c || !c.enabled) return new Bounds(p, Vector3.one * 0.5f);
                var b = c.bounds;
                if (b.max.y > p.y + 2.2f) { var mx = b.max; mx.y = p.y + 2.2f; b.SetMinMax(b.min, mx); }   // what a bumper or cab meets
                return b;
            }
        }

        /// <summary>Make a town piece drivable-through by template id. Null: it stays solid.</summary>
        public static StreetFurniture For(DestructibleVoxels d, string tid)
        {
            if (!d || tid == null || d.GetComponent<Rigidbody>()) return null;                 // crates are loose already
            Mode m; float mass = 0f, energy = 0f, cost = 25000f, keep = 0.62f, trunkR = 0f;
            if (tid.StartsWith("Bench")) { m = Mode.Loose; mass = 70f; energy = 2500f; }
            else if (tid.StartsWith("Bin")) { m = Mode.Loose; mass = 25f; energy = 600f; }
            else if (tid.StartsWith("Bollard")) { m = Mode.Loose; mass = 45f; energy = 5000f; }
            else if (tid.StartsWith("Hydrant")) { m = Mode.Loose; mass = 80f; energy = 6000f; }
            else if (tid.StartsWith("PhoneBooth")) { m = Mode.Loose; mass = 240f; energy = 9000f; }
            else if (tid.StartsWith("OilDrum")) { m = Mode.Loose; mass = 60f; energy = 800f; }
            else if (tid.StartsWith("Planter")) { m = Mode.Plough; cost = 40000f; }
            else if (tid.StartsWith("Hedge") || tid.StartsWith("Bush")) { m = Mode.Plough; cost = 4000f; keep = 0.8f; }
            else if (tid.StartsWith("ParkTree") || tid.StartsWith("Pine")) { m = Mode.Plough; cost = 30000f; keep = 0.55f; trunkR = 0.25f; }
            else if (tid.StartsWith("Statue") || tid.StartsWith("Fountain")) { m = Mode.Plough; cost = 30000f; keep = 0.6f; }
            else if (tid.StartsWith("BusStop")) { m = Mode.Plough; cost = 15000f; keep = 0.65f; }
            else return null;
            var f = d.gameObject.AddComponent<StreetFurniture>();
            f.dv = d; f.mode = m; f.mass = mass; f.looseEnergy = energy; f.ploughCost = cost; f.minKeep = keep; f.trunk = trunkR;
            f.SetKey(d.StateKey);
            if (m == Mode.Loose && d.StateKey != null && DestructibleVoxels.Moved.TryGetValue(d.StateKey, out var mv)) f.Restore(mv.p, mv.r);
            else if (m == Mode.Loose && BreakablePole.TakeRemote(d.StateKey, out var rv)) { var o = rv; o.y = 0f; f.Restore(d.transform.position + (o.sqrMagnitude > 0.01f ? o.normalized * 1.5f : Vector3.zero), d.transform.rotation); }   // knocked on another peer
            return f;
        }

        // ------------------------------------------------------------------ hits

        protected override bool GiveWay(VehicleDamage car, Rigidbody rb, Vector3 v, Vector3 point, Vector3 dir, bool contact)
        {
            if (Gone || !dv) return false;
            float vn = Vector3.Dot(v, dir);
            if (vn < 2f) return false;
            float M = Mathf.Max(50f, rb.mass);
            float energy = 0.5f * M * vn * vn * Ram(car, point);
            if (mode == Mode.Loose)
            {
                if (energy < looseEnergy) return false;
                float lost = Exchange(rb, v, dir, mass * 1.3f, looseEnergy, 0.5f);
                Knock(point, dir * vn * (1f + 0.5f * M / (M + mass)));
                IgnoreCar(rb, 0.35f);
                Crunch(car, point, dir, Mathf.Min(8f, 2.3f + lost * 1.5f + mass / 150f));
                SendKnock(point, dir * vn);
                return true;
            }
            // plough a channel as wide and tall as the car's front through it
            if (Time.time < retryAt || (rb == ploughing && Time.time < quietUntil)) return false;   // already through it
            var box = CarBox(rb);
            float half = Mathf.Clamp(box.extents.x + 0.15f, 0.8f, 1.8f);
            float cy = rb.transform.TransformPoint(box.center).y;
            float cut = Plough(point, dir, half, cy, rb.linearVelocity);
            if (cut <= 0f) { retryAt = Time.time + 0.3f; return false; }                      // passing beside it
            float lostP = Exchange(rb, v, dir, 0f, cut * ploughCost, minKeep);
            ploughing = rb; quietUntil = Time.time + Mathf.Clamp((HitBounds.size.magnitude + 5f) / Mathf.Max(3f, vn), 0.4f, 2f);
            IgnoreCar(rb, 0.8f);
            Crunch(car, point, dir, Mathf.Min(8f, 2.4f + lostP * 0.6f));
            return true;
        }

        /// <summary>Carve along <paramref name="dir"/> from the hit through the piece; returns the volume removed (m³).</summary>
        float Plough(Vector3 from, Vector3 dir, float half, float centreY, Vector3 debrisVel)
        {
            var b = HitBounds;
            float len = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(dir.x), 0f, Mathf.Abs(dir.z))) * 2f + 0.6f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(len / 0.6f), 1, 10);
            float size = dv.voxelSize, vox = size * size * size;
            float power = 1.7f;                                                                 // cuts stone (hardness 2.6) as wide as wood
            int removed = 0;
            var start = new Vector3(from.x, centreY, from.z) - dir * 0.3f;
            string k = dv.StateKey;
            for (int i = 0; i < steps && dv; i++)
            {
                var p = start + dir * (i * 0.6f);
                MadMax.Net.NetSession.Instance?.SendCarve(k, p, dir, half, power);
                removed += dv.Carve(p, dir, half, power, debrisVel * 0.4f, true, i == steps - 1);
            }
            if (removed > 0) MadMax.Audio.Sfx.Play(name.Contains("Tree") || name.Contains("Bush") ? "hit_wood" : "crash_small", from, Mathf.Clamp01(0.4f + removed / 400f), 0.8f, 50f, 0.2f);
            if (!dv) SetGone();
            return removed * vox;
        }

        protected override void RemoteGiveWay(Vector3 point, Vector3 velocity)
        {
            if (Gone || mode != Mode.Loose) return;
            Knock(point, velocity * 1.4f);
        }

        // ------------------------------------------------------------------ loose

        /// <summary>Off its bolts: a rigid body flung along <paramref name="fling"/> (remembered where it lands).</summary>
        void Knock(Vector3 point, Vector3 fling)
        {
            SetGone();
            MakeBody();
            body.linearVelocity = fling + Vector3.up * Mathf.Clamp(fling.magnitude * 0.15f, 0.8f, 3f);
            var side = Vector3.Cross(Vector3.up, fling.sqrMagnitude > 0.01f ? fling.normalized : transform.forward);
            body.angularVelocity = side * Mathf.Clamp(fling.magnitude * 0.5f, 1f, 8f) + Vector3.up * ((Mathf.Abs(point.x * 7.1f + point.z * 3.3f) % 2f) - 1f) * 2f;
            MadMax.Audio.Sfx.Play(mass > 150f ? "crash_small" : "hit_metal", point, 0.8f, mass > 150f ? 0.8f : 1.1f, 50f, 0.1f);
            Fx.Sparks(point, fling.normalized * 2f + Vector3.up, 5, new Color(1f, 0.72f, 0.3f));
            if (dv.TemplateId != null && (dv.TemplateId.StartsWith("PhoneBooth") || dv.TemplateId.StartsWith("BusStop"))) Shards.Drop(point, Shards.Kind.Clear);
        }

        void MakeBody()
        {
            dv.anchored = false;                                                                // its pose is remembered from now on (Moved)
            var mc = dv.GetComponent<MeshCollider>();
            var mf = dv.GetComponent<MeshFilter>();
            if (mc) mc.enabled = false;
            var box = gameObject.AddComponent<BoxCollider>();
            if (mf && mf.sharedMesh) { box.center = mf.sharedMesh.bounds.center; box.size = Vector3.Max(mf.sharedMesh.bounds.size, Vector3.one * 0.1f); }
            body = gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        /// <summary>Streamed back in after being knocked loose: where it came to rest.</summary>
        void Restore(Vector3 p, Quaternion r)
        {
            SetGone();
            transform.SetPositionAndRotation(p + Vector3.up * 0.05f, r);
            MakeBody();
        }
    }
}
