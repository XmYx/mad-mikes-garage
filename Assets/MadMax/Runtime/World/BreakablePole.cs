using System.Collections.Generic;
using MadMax.Building;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A pole that snaps at its foot when a vehicle hits it hard enough (street lamps, traffic lights, signs,
    /// city streetlights, rail telegraph poles): it breaks off as a rigid body (real mass, capsule along the post),
    /// pivots over the stump and falls away along the impact, the car keeping most of its speed (see
    /// <see cref="Breakaway"/>). Below <see cref="snapEnergy"/> it only bends (leans away, a share of the next snap
    /// already spent). Lamps go dark (glass shards), live poles throw sparks, its wires come down. Once it lies still
    /// its pose is remembered by key (<see cref="Fallen"/>, saved with the lights block) and it rests kinematic until
    /// something bumps it.</summary>
    public class BreakablePole : Breakaway
    {
        public enum Kind { Steel, Wood }

        /// <summary>Fallen poles by key: where they came to rest.</summary>
        public static readonly Dictionary<string, Pose> Fallen = new Dictionary<string, Pose>();
        /// <summary>Knocked on another peer while not streamed in here: falls along this velocity when it spawns.</summary>
        static readonly Dictionary<string, Vector3> remote = new Dictionary<string, Vector3>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Fallen.Clear(); remote.Clear(); }

        public Kind kind = Kind.Steel;
        /// <summary>Post mass (kg), length (m, from the foot) and radius (m) of the falling body.</summary>
        public float mass = 180f, height = 6f, radius = 0.1f;
        /// <summary>Energy (J) that snaps the foot (≈ ½·m·v² of the car at the speed it just breaks).</summary>
        public float snapEnergy = 15000f;
        /// <summary>Live: sparks and an arc flash when it comes down.</summary>
        public bool electric;
        /// <summary>The lamp on it (goes dark).</summary>
        public PoweredLight lamp;
        /// <summary>Wire spans hanging from it (hidden when it snaps; the neighbour's span to it too, see <see cref="feeds"/>).</summary>
        public readonly List<Renderer> wires = new List<Renderer>();
        /// <summary>The pole whose wires run to this one (rail lines).</summary>
        public BreakablePole feeds;

        /// <summary>Raised when it snaps (world lamps remember it).</summary>
        public event System.Action<BreakablePole> Broke;

        public enum State { Standing, Falling, Down }
        public State state { get; private set; }
        public Rigidbody Body { get; private set; }
        float bent, settleT, fellAt;
        bool thudded;
        DestructibleVoxels voxels;

        public override Bounds HitBounds
        {
            get
            {
                var p = transform.position;
                float r = Mathf.Max(radius, 0.12f);
                return new Bounds(p + Vector3.up * Mathf.Min(height, 3f) * 0.5f, new Vector3(r * 2f, Mathf.Min(height, 3f), r * 2f));
            }
        }

        // ------------------------------------------------------------------ setup

        /// <summary>A town street lamp (<see cref="TownLights"/>): its post, by piece.</summary>
        public static BreakablePole ForLamp(GameObject go, string piece, string key)
        {
            var b = go.AddComponent<BreakablePole>();
            b.lamp = go.GetComponent<PoweredLight>();
            b.electric = true;
            switch (piece)
            {
                case "streetlamp_pole": b.Set(Kind.Wood, 140f, 5.1f, 0.1f, 9000f); break;
                case "streetlamp_gas": b.Set(Kind.Steel, 120f, 2.9f, 0.1f, 8000f); break;
                case "streetlamp_solar": b.Set(Kind.Steel, 90f, 3.7f, 0.08f, 6000f); b.electric = false; break;
                case "floodlight_mast": b.Set(Kind.Steel, 650f, 8f, 0.16f, 120000f); break;
                default: b.Set(Kind.Steel, 190f, 7.2f, 0.1f, 15000f); break;                  // cobra-head highway lamp
            }
            b.SetKey(key);
            return b;
        }

        /// <summary>A voxel pole prop (traffic light, stop sign, the old city streetlights). Null: not a pole.</summary>
        public static BreakablePole ForProp(DestructibleVoxels d, string templateId)
        {
            if (!d || templateId == null) return null;
            BreakablePole b;
            if (templateId.StartsWith("TrafficLight")) { b = d.gameObject.AddComponent<BreakablePole>(); b.Set(Kind.Steel, 230f, 3.8f, 0.1f, 20000f); b.electric = true; }
            else if (templateId.StartsWith("StopSign")) { b = d.gameObject.AddComponent<BreakablePole>(); b.Set(Kind.Steel, 25f, 3.1f, 0.05f, 1500f); }
            else if (templateId.StartsWith("Streetlight")) { b = d.gameObject.AddComponent<BreakablePole>(); b.Set(Kind.Steel, 170f, 4.9f, 0.08f, 13000f); b.electric = true; }
            else return null;
            b.voxels = d;
            d.impactThreshold = 1000f;                                                          // a car bends or snaps it, no longer cuts it in two
            b.SetKey(d.StateKey);
            return b;
        }

        /// <summary>A rail telegraph pole (timber, 6.5 m, wires to the next one).</summary>
        public static BreakablePole ForTelegraph(GameObject go, string key)
        {
            var b = go.AddComponent<BreakablePole>();
            b.Set(Kind.Wood, 320f, 6.5f, 0.12f, 26000f);
            b.SetKey(key);
            return b;
        }

        void Set(Kind k, float m, float h, float r, float e) { kind = k; mass = m; height = h; radius = r; snapEnergy = e; }

        /// <summary>Out of the game as a pole (an old save's leaning lamp): never gives way.</summary>
        public void Retire() { if (state == State.Standing) { SetGone(); state = State.Down; } }

        void Start()
        {
            if (key == null) return;
            if (Fallen.TryGetValue(key, out var pose)) { LieDown(pose); return; }
            if (TakeRemote(key, out var vel)) RemoteGiveWay(transform.position + Vector3.up * 0.5f, vel);
        }

        /// <summary>Remember a knock that happened on another peer while this pole wasn't streamed in.</summary>
        public static void NoteRemote(string key, Vector3 velocity) { if (key != null && !Fallen.ContainsKey(key)) remote[key] = velocity; }

        /// <summary>A remote knock waiting for <paramref name="key"/> (taken: it is applied now).</summary>
        public static bool TakeRemote(string key, out Vector3 velocity)
        {
            velocity = default;
            if (key == null || !remote.TryGetValue(key, out velocity)) return false;
            remote.Remove(key);
            return true;
        }

        // ------------------------------------------------------------------ hits

        protected override bool GiveWay(VehicleDamage car, Rigidbody body, Vector3 v, Vector3 point, Vector3 dir, bool contact)
        {
            if (state != State.Standing) return false;
            float vn = Vector3.Dot(v, dir);
            if (vn < 0.5f) return false;
            float M = Mathf.Max(50f, body.mass);
            float energy = 0.5f * M * vn * vn * Ram(car, point);
            float need = snapEnergy * (1f - bent);
            if (energy < need)
            {
                if (!contact) return false;                                                     // the contact comes and stops the car
                float share = energy / snapEnergy;
                bent = Mathf.Min(0.9f, bent + share * 0.7f);
                Lean(dir, Mathf.Min(22f, share * 28f));
                MadMax.Audio.Sfx.Play(kind == Kind.Wood ? "hit_wood" : "hit_metal", point, Mathf.Clamp01(0.3f + share), 0.8f, 40f, 0.2f);
                return false;
            }
            // snap: the car shares momentum with the post's effective mass at the hit height, pays the snap energy
            float h = Mathf.Clamp(point.y - transform.position.y, 0.2f, height * 0.5f);
            float eff = mass / (1f + 12f * (h - height * 0.5f) * (h - height * 0.5f) / (height * height));
            float lost = Exchange(body, v, dir, eff, need, 0.35f);
            // the foot is kicked out ahead and off to the side it was hit on, clear of the car's line
            var right = Vector3.Cross(Vector3.up, dir);
            var side = right * (Vector3.Dot(transform.position - body.worldCenterOfMass, right) >= 0f ? 1f : -1f);
            Snap(point, dir, vn, side);
            IgnoreCar(body, 1.4f);
            Crunch(car, point, dir, Mathf.Min(9f, 2.6f + lost * 1.2f + vn * 0.1f + mass / 400f));   // a crumpled bumper, not a wall
            SendKnock(point, dir * vn);
            return true;
        }

        protected override void RemoteGiveWay(Vector3 point, Vector3 velocity)
        {
            if (state != State.Standing) return;
            var dir = velocity; dir.y = 0f;
            float vn = dir.magnitude;
            dir = vn > 0.01f ? dir / vn : transform.forward;
            Snap(point, dir, vn, Vector3.zero);
        }

        /// <summary>Bent over at the foot by a hit too light to snap it.</summary>
        void Lean(Vector3 away, float degrees)
        {
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f || degrees <= 0f) return;
            transform.rotation = Quaternion.AngleAxis(degrees, Vector3.Cross(Vector3.up, away.normalized)) * transform.rotation;
        }

        void Snap(Vector3 point, Vector3 dir, float speed, Vector3 side)
        {
            SetGone();
            state = State.Falling;
            fellAt = Time.time; settleT = 0f; thudded = false;
            var top = transform.position + transform.up * height;
            if (lamp) lamp.Smash(top);                                                         // dark, the glass on the ground
            if (voxels) CarryHeads(top);
            Broke?.Invoke(this);
            // the falling body: a capsule along the post (non-convex prop meshes can't be dynamic)
            foreach (var c in GetComponents<Collider>()) if (!c.isTrigger) c.enabled = false;
            var cap = gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 1; cap.radius = Mathf.Max(radius, 0.06f); cap.height = height; cap.center = new Vector3(0f, height * 0.5f, 0f);
            if (voxels) voxels.anchored = false;
            Body = gameObject.GetComponent<Rigidbody>();
            if (!Body) Body = gameObject.AddComponent<Rigidbody>();
            Body.isKinematic = false;
            Body.mass = mass;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.centerOfMass = new Vector3(0f, height * 0.45f, 0f);
            // the foot goes with the bumper (and off to the side it was hit on), the post tips over it away from the car
            float spin = Mathf.Clamp(speed * 0.16f, 0.7f, 3.2f);
            var fall = (dir + side * 0.6f).normalized;
            Body.linearVelocity = dir * Mathf.Min(speed * 0.85f, 14f) + side * Mathf.Min(3f, speed * 0.25f) + Vector3.up * 0.5f;
            Body.angularVelocity = Vector3.Cross(Vector3.up, fall) * spin;
            var foot = transform.position + Vector3.up * 0.3f;
            MadMax.Audio.Sfx.Play(kind == Kind.Wood ? "hit_wood" : "hit_metal", foot, 0.9f, kind == Kind.Wood ? 0.7f : 0.85f, 60f, 0.1f);
            MadMax.Audio.Sfx.Play("creak", foot, 0.7f, 0.7f, 50f, 0.3f);
            if (DebrisSystem.Instance) DebrisSystem.Instance.EmitPuff(foot, kind == Kind.Wood ? MadMax.Voxel.Pal.Wood[2] : MadMax.Voxel.Pal.Cream[1], 0.06f, dir * 2f + Vector3.up, 0.8f);
            if (kind == Kind.Steel) Fx.Sparks(foot, dir * 3f + Vector3.up, 6, new Color(1f, 0.72f, 0.3f));
            bool live = electric && (!lamp || lamp.HasPower);
            if (live)
            {
                Fx.Sparks(top, Vector3.down * 2f + dir, 14, new Color(0.75f, 0.85f, 1f));
                Fx.Flash(top, new Color(0.6f, 0.75f, 1f), 14f, 6f, 0.25f);
                MadMax.Audio.Sfx.Play("sizzle", top, 0.9f, 1.3f, 50f, 0.2f);
            }
            DropWires(top, live);
        }

        /// <summary>A voxel streetlight's separate lamp head (<see cref="TownLights.CityLamp"/>) goes dark and falls with it.</summary>
        void CarryHeads(Vector3 top)
        {
            foreach (var c in Physics.OverlapSphere(top, 1.4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!c || !c.TryGetComponent<LampHead>(out var head) || c.transform.IsChildOf(transform)) continue;
                if (head.lamp) { head.lamp.Smash(c.transform.position); if (!lamp) lamp = head.lamp; }
                c.enabled = false;
                c.transform.SetParent(transform, true);
            }
        }

        void DropWires(Vector3 top, bool live)
        {
            bool any = false;
            foreach (var w in wires) if (w && w.enabled) { w.enabled = false; any = true; }
            if (feeds) foreach (var w in feeds.wires) if (w && w.enabled) { w.enabled = false; any = true; }
            if (!any) return;
            MadMax.Audio.Sfx.Play("chain", top, 0.6f, 1.4f, 45f, 0.2f);                        // the lines snap
            if (live) Fx.Sparks(top, Vector3.up, 10, new Color(0.75f, 0.85f, 1f));
        }

        // ------------------------------------------------------------------ falling, resting

        void FixedUpdate()
        {
            if (state != State.Falling || !Body) return;
            float tilt = Vector3.Angle(transform.up, Vector3.up);
            if (!thudded && tilt > 72f && Time.time - fellAt > 0.3f)
            {
                thudded = true;
                var top = transform.position + transform.up * height * 0.8f;
                MadMax.Audio.Sfx.Play(kind == Kind.Wood ? "hit_wood" : "crash_small", top, 0.8f, 0.6f, 60f, 0.2f);
                if (DebrisSystem.Instance) DebrisSystem.Instance.EmitPuff(top, MadMax.Voxel.Pal.Cream[1], 0.07f, Vector3.up * 1.5f, 1f);
                var gi = MadMax.Game.WastelandGame.Instance;
                if (gi && gi.cameraRig && (gi.FocusPos - top).sqrMagnitude < 20f * 20f) gi.cameraRig.Shake(mass > 300f ? 3f : 1.5f);
            }
            bool still = Body.IsSleeping() || (Body.linearVelocity.sqrMagnitude < 0.04f && Body.angularVelocity.sqrMagnitude < 0.04f);
            settleT = still ? settleT + Time.fixedDeltaTime : 0f;
            if (settleT > 0.8f || Time.time - fellAt > 20f) Settle();
        }

        void Settle()
        {
            state = State.Down;
            Body.isKinematic = true;
            if (key != null) Fallen[key] = new Pose(transform.position, transform.rotation);
        }

        /// <summary>Streamed back in fallen: where it came to rest, dark, a resting body.</summary>
        void LieDown(Pose pose)
        {
            SetGone();
            if (voxels) CarryHeads(transform.position + transform.up * height);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (lamp) lamp.broken = true;
            foreach (var c in GetComponents<Collider>()) if (!c.isTrigger) c.enabled = false;
            var cap = gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 1; cap.radius = Mathf.Max(radius, 0.06f); cap.height = height; cap.center = new Vector3(0f, height * 0.5f, 0f);
            if (voxels) voxels.anchored = false;
            Body = gameObject.GetComponent<Rigidbody>();
            if (!Body) Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = mass; Body.isKinematic = true;
            Body.centerOfMass = new Vector3(0f, height * 0.45f, 0f);
            foreach (var w in wires) if (w) w.enabled = false;
            if (feeds) foreach (var w in feeds.wires) if (w) w.enabled = false;
            state = State.Down;
        }

        void OnCollisionEnter(Collision c)
        {
            // lying still: a vehicle shoves it along (dynamic again until it settles)
            if (state != State.Down || !Body || !c.rigidbody || c.rigidbody.isKinematic || c.relativeVelocity.sqrMagnitude < 1f) return;
            Body.isKinematic = false;
            Body.WakeUp();
            state = State.Falling; fellAt = Time.time; settleT = 0f; thudded = true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (state == State.Falling && key != null && Body) Fallen[key] = new Pose(transform.position, transform.rotation);   // streamed out mid-fall
        }

        // ------------------------------------------------------------------ save (lights block lines "f:key|x|y|z|qx|qy|qz|qw")

        public static void Save(List<string> into)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var kv in Fallen)
            {
                var p = kv.Value.position; var q = kv.Value.rotation;
                into.Add("f:" + kv.Key + "|" + p.x.ToString("0.###", ci) + "|" + p.y.ToString("0.###", ci) + "|" + p.z.ToString("0.###", ci)
                         + "|" + q.x.ToString("0.####", ci) + "|" + q.y.ToString("0.####", ci) + "|" + q.z.ToString("0.####", ci) + "|" + q.w.ToString("0.####", ci));
            }
        }

        /// <summary>Read one saved line; false: not a fallen pole.</summary>
        public static bool Load(string line)
        {
            if (line == null || !line.StartsWith("f:")) return false;
            var parts = line.Substring(2).Split('|');
            if (parts.Length < 8) return true;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var f = new float[7];
            for (int i = 0; i < 7; i++) if (!float.TryParse(parts[parts.Length - 7 + i], System.Globalization.NumberStyles.Float, ci, out f[i])) return true;
            string k = string.Join("|", parts, 0, parts.Length - 7);
            Fallen[k] = new Pose(new Vector3(f[0], f[1], f[2]), new Quaternion(f[3], f[4], f[5], f[6]).normalized);
            return true;
        }
    }
}
