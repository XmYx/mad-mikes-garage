using System.Collections.Generic;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Lifting magnet on a lattice-boom crane (part "magnet_crane", the MagnetCrane truck). While driving:
    /// 7 magnet ON / OFF, hold 8 hoist up, hold 9 lower, Shift+8 / Shift+9 luff the boom up / down, hold 0 slew
    /// (Shift+0 the other way). The disc hangs on its cable from the boom tip and swings behind the motion. Switched on it
    /// pulls up every ferrous body within <see cref="Reach"/> below the pole face (vehicles, wrecks, loose parts, scrap
    /// and iron stacks; never people, animals or wood) and holds each on a cable joint at the disc; OFF drops everything.
    /// Past <see cref="capacityFactor"/> × the truck's mass it is OVERLOADED: the hoist stalls and the truck rocks.
    /// Stopped with the magnet live, the outriggers (if the truck has them) walk out and plant.</summary>
    public partial class MagnetCrane : MonoBehaviour
    {
        public const string PartId = "magnet_crane";
        /// <summary>Shackle to pole face (m) and how far below the pole face the magnet pulls.</summary>
        public const float DiscDepth = 0.96f, Reach = 2.2f, DiscRadius = 0.72f;
        static readonly Vector3 TipLocal = new Vector3(0f, -5f, -83f) * 0.08f;        // boom tip sheave (boom space, m)
        static readonly Vector3 PendantBoom = new Vector3(0f, 5f, -77f) * 0.08f;       // pendant anchor on the boom head
        static readonly Vector3 PendantTurret = new Vector3(0f, 28f, 1f) * 0.08f;      // A-frame apex (turret space)
        static readonly Vector3 RamTurret = new Vector3(3f, 4f, -13f) * 0.08f, RamBoom = new Vector3(3f, -7f, -30f) * 0.08f;
        public float capacityFactor = 0.75f;

        VehicleDriver driver;
        Rigidbody rb;
        VehiclePart part; MountSocket[] sockets; float nextScan;
        Transform turret, boom;
        Collider[] craneColliders;
        float rope = 0.6f, slew, slewSpeed, luff = 8f, worked = -10f, scanAt, strain;
        bool on;

        class Held { public Rigidbody body; public ConfigurableJoint joint; public Vector3 local; public float limit; }
        readonly List<Held> held = new List<Held>();
        static readonly Collider[] hits = new Collider[48];
        static readonly RaycastHit[] rays = new RaycastHit[12];

        Transform disc; MeshRenderer lamp;
        LineRenderer cable, pendant;
        HydraulicRam ramL, ramR;
        Outriggers outriggers;
        Vector3 shackle, shacklePrev;
        bool simInit;

        public string Status { get; private set; }
        public bool On => on;
        public float Rope => rope;
        public float Luff => luff;
        public int HeldCount => held.Count;
        public float HeldMass { get; private set; }
        public float Capacity => rb ? rb.mass * capacityFactor : 0f;
        public bool Overload => HeldMass > Capacity;
        /// <summary>Centre of the pole face (world).</summary>
        public Vector3 PoleFace => disc ? disc.position - disc.up * DiscDepth : Tip + Vector3.down * (rope + DiscDepth);
        public Vector3 Tip => boom ? boom.TransformPoint(TipLocal) : transform.position;
        public Outriggers Legs => outriggers;
        public bool Holds(Rigidbody r) { foreach (var h in held) if (h.body == r) return true; return false; }

        void Awake() { driver = GetComponent<VehicleDriver>(); rb = GetComponent<Rigidbody>(); }

        /// <summary>The mounted magnet crane (re-scanned at most once a second while there is none).</summary>
        public VehiclePart Part
        {
            get
            {
                if (part && part.Socket && part.transform.IsChildOf(transform)) return part;
                if (part) Unmounted();
                if (Time.time < nextScan) return null;
                nextScan = Time.time + 1f;
                sockets ??= GetComponentsInChildren<MountSocket>(true);
                foreach (var s in sockets) if (s && s.Current && s.Current.partId == PartId) { part = s.Current; Mounted(); break; }
                return part;
            }
        }

        void Mounted()
        {
            turret = part.transform.Find("turret");
            boom = turret ? turret.Find("boom") : null;
            craneColliders = part.GetComponentsInChildren<Collider>(true);
            foreach (var c in craneColliders) c.gameObject.layer = Layers.MachineTool;   // a lowered boom never digs the truck in
            var mat = LifterKit.BodyMaterial(transform);
            if (!disc)
            {
                disc = LifterKit.Visual("MagnetDisc", null, LifterKit.MagnetDisc(), mat).transform;
                lamp = LifterKit.Visual("Lamp", disc, LifterKit.MagnetLamp(), mat);
                lamp.enabled = false;
                cable = LifterKit.Line(transform, "MagnetCable", 0.05f);
                pendant = LifterKit.Line(transform, "BoomPendant", 0.035f);
                ramL = new HydraulicRam(transform, mat, 18, 1.4f, "LuffRamL");
                ramR = new HydraulicRam(transform, mat, 18, 1.4f, "LuffRamR");
            }
            outriggers ??= new Outriggers(transform, rb);
            simInit = false;
            Pose();
        }

        void Unmounted()
        {
            Release(false);
            part = null; turret = boom = null;
            if (disc) Destroy(disc.gameObject);
            if (cable) Destroy(cable.gameObject);
            if (pendant) Destroy(pendant.gameObject);
            ramL?.Destroy(); ramR?.Destroy(); ramL = ramR = null;
            disc = null;
        }

        void OnDestroy() { if (disc) Destroy(disc.gameObject); }

        void Pose()
        {
            if (turret) turret.localRotation = Quaternion.Euler(0f, slew, 0f);
            if (boom) boom.localRotation = Quaternion.Euler(luff, 0f, 0f);
        }

        /// <param name="slewDir">-1, 0 or +1</param>
        public void Control(bool press7, bool hold8, bool hold9, float slewDir, bool shift = false)
        {
            var p = Part;
            if (!p) { Status = null; return; }
            float dt = Time.deltaTime;
            bool working = press7 || hold8 || hold9 || slewDir != 0f;
            if (working) worked = Time.time;
            slewSpeed = Mathf.MoveTowards(slewSpeed, slewDir * 22f, 40f * dt);            // a heavy house: slow to start, slow to stop
            if (Mathf.Abs(slewSpeed) > 0.01f) slew = Mathf.Repeat(slew + slewSpeed * dt, 360f);
            // keep the boom clear of the cab when it swings over the front
            float overCab = Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(slew, 180f)) / 45f);
            float minLuff = Mathf.Lerp(0f, 24f, overCab);
            if (shift)
            {
                luff = Mathf.Clamp(luff + ((hold8 ? 1f : 0f) - (hold9 ? 1f : 0f)) * 7f * dt, minLuff, 72f);
            }
            else
            {
                if (hold8 && !Overload) rope = Mathf.Max(0.6f, rope - 0.9f * dt);
                if (hold9) rope = Mathf.Min(9f, rope + 1.1f * dt);
            }
            luff = Mathf.Max(luff, minLuff);
            strain = hold8 && !shift && Overload ? Mathf.MoveTowards(strain, 1f, dt * 2f) : Mathf.MoveTowards(strain, 0f, dt * 3f);
            Pose();
            if (press7) { if (on) Switch(false); else Switch(true); }
            foreach (var h in held) if (h.body) h.body.WakeUp();

            string kg = HeldMass > 0f ? " " + Mathf.RoundToInt(HeldMass) + "/" + Mathf.RoundToInt(Capacity) + " KG" : "";
            string legs = outriggers != null && outriggers.Any && outriggers.Deployed > 0.95f ? "  LEGS DOWN" : "";
            Status = !on ? "MAGNET OFF  [7] ON  [8/9] HOIST  [SHIFT+8/9] BOOM  [0] SLEW"
                   : Overload ? "MAGNET OVERLOAD" + kg + "  [9] LOWER  [7] DROP"
                   : held.Count > 0 ? "MAGNET: " + held.Count + (held.Count == 1 ? " LOAD" : " LOADS") + kg + legs + "  [7] DROP  [8/9] HOIST"
                   : "MAGNET ON  LOWER ONTO IRON  [7] OFF" + legs;
        }

        /// <summary>Energise or release (automation, the 7 key).</summary>
        public void Switch(bool energise)
        {
            if (!Part || energise == on) return;
            on = energise;
            worked = Time.time;
            var at = PoleFace;
            MadMax.Audio.Sfx.Play(on ? "click" : "chain", at, 0.9f, on ? 0.6f : 0.8f, 40f);
            if (on) { scanAt = 0f; Fx.Sparks(at, Vector3.down, 6, new Color(0.6f, 0.75f, 1f)); }
            else Release(true);
        }

        void Release(bool fx)
        {
            foreach (var h in held)
            {
                if (h.joint) Destroy(h.joint);
                if (!h.body) continue;
                LifterKit.IgnoreLoad(h.body, craneColliders, false);
                h.body.WakeUp();
                MadMax.Net.NetSession.ReleaseCarry(h.body);                                         // a wreck a client held goes back to the host
                if (fx) Fx.Smoke(h.body.worldCenterOfMass, Vector3.up * 0.3f, 0.5f, new Color(0.55f, 0.5f, 0.45f, 0.5f), 1.2f);
            }
            held.Clear();
            HeldMass = 0f;
        }

        void Latch(Rigidbody r, Vector3 contact)
        {
            r.isKinematic = false;
            var h = new Held { body = r, local = r.transform.InverseTransformPoint(contact) };
            var j = rb.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = r;
            j.autoConfigureConnectedAnchor = false;
            j.anchor = transform.InverseTransformPoint(Tip);
            j.connectedAnchor = h.local;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Limited;
            h.limit = Vector3.Distance(Tip, contact);
            j.linearLimit = new SoftJointLimit { limit = h.limit };
            j.linearLimitSpring = new SoftJointLimitSpring { spring = 400000f, damper = 30000f };
            j.enableCollision = true;
            h.joint = j;
            held.Add(h);
            LifterKit.IgnoreLoad(r, craneColliders, true);
            HeldMass += r.mass;
            if (r.TryGetComponent<VehicleDriver>(out var v)) v.KeepAwakeUntil = Time.time + 1f;
            MadMax.Audio.Sfx.Play("hit_metal", contact, Mathf.Clamp(0.4f + r.mass / 3000f, 0.4f, 1f), 0.7f + Random.value * 0.2f, 50f, 0.08f);
            Fx.Sparks(contact, Vector3.up, 10, new Color(1f, 0.85f, 0.45f));
            var t = DeformableTerrain.Instance;
            if (t && contact.y - t.Height(contact.x, contact.z) < 1.2f)
                for (int i = 0; i < 4; i++) Fx.Smoke(contact + Random.insideUnitSphere * 0.6f, Random.insideUnitSphere * 0.6f + Vector3.up * 0.4f, 0.7f, new Color(0.72f, 0.6f, 0.45f, 0.55f), 1.6f);
        }

        void Scan()
        {
            var face = PoleFace;
            var up = disc ? disc.up : Vector3.up;
            int n = Physics.OverlapCapsuleNonAlloc(face - up * 0.3f, face - up * (Reach - 0.4f), DiscRadius + 0.45f, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n && held.Count < 8; i++)
            {
                var c = hits[i];
                var r = c.attachedRigidbody;
                if (!r || r == rb || Holds(r) || !LifterKit.Ferrous(r, transform) || !MadMax.Net.NetSession.MayLift(r)) continue;
                var cp = LifterKit.Closest(c, face);
                var d = cp - face;
                float below = -Vector3.Dot(d, up), aside = (d + up * below).magnitude;
                if (below < -0.3f || below > Reach || aside > DiscRadius + 0.5f) continue;
                Latch(r, cp);
            }
        }

        void FixedUpdate()
        {
            if (!part) return;
            float dt = Time.fixedDeltaTime;
            bool stopped = driver && Mathf.Abs(driver.ForwardSpeed) < 0.6f && driver.throttleInput < 0.05f;
            bool wantLegs = stopped && (on || held.Count > 0 || Time.time - worked < 4f);
            if (outriggers != null && outriggers.Any)
            {
                if (outriggers.Tick(wantLegs, dt)) MadMax.Audio.Sfx.Loop(this, "hydraulic", 0.5f, 0.8f, 40f);
                outriggers.Push();
                if (outriggers.Deployed > 0.05f && driver) driver.KeepAwakeUntil = Time.time + 0.5f;
            }
            if (on && Time.time >= scanAt && MadMax.Net.NetSession.SimulatesHere(driver)) { scanAt = Time.time + 0.2f; Scan(); }   // remote copies only show the pose
            if (held.Count == 0) return;
            driver.KeepAwakeUntil = Time.time + 0.5f;
            var tip = Tip;
            float target = rope + DiscDepth;
            HeldMass = 0f;
            for (int i = held.Count - 1; i >= 0; i--)
            {
                var h = held[i];
                if (!h.body || !h.joint) { if (h.joint) Destroy(h.joint); held.RemoveAt(i); continue; }
                HeldMass += h.body.mass;
                h.limit = h.limit > target ? Mathf.Max(target, h.limit - 2.2f * dt) : target;        // pulled up against the pole face
                h.joint.anchor = transform.InverseTransformPoint(tip);
                h.joint.linearLimit = new SoftJointLimit { limit = h.limit };
                if (h.body.IsSleeping()) h.body.WakeUp();
                if (h.body.TryGetComponent<VehicleDriver>(out var v)) v.KeepAwakeUntil = Time.time + 0.5f;
            }
            if (strain > 0.01f)                                                                       // overloaded hoist: the truck shudders
                rb.AddForceAtPosition(Vector3.up * Mathf.Sin(Time.time * 11f) * rb.mass * 0.9f * strain, tip);
        }

        void LateUpdate()
        {
            if (!Part || !boom || !disc) return;                                                     // also notices a crane taken off
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var tip = Tip;
            // the disc: on the main load's contact while holding, else a damped pendulum on the cable
            Held main = null;
            foreach (var h in held) if (h.body && (main == null || h.body.mass > main.body.mass)) main = h;
            Vector3 face, up;
            if (main != null)
            {
                face = main.body.transform.TransformPoint(main.local);
                up = (tip - face).normalized;
                shackle = face + up * DiscDepth;
                shacklePrev = shackle;
            }
            else
            {
                if (!simInit) { shackle = shacklePrev = tip + Vector3.down * rope; simInit = true; }
                var vel = (shackle - shacklePrev) * Mathf.Pow(0.35f, dt);
                shacklePrev = shackle;
                shackle += vel + Physics.gravity * dt * dt;
                var off = shackle - tip;
                shackle = tip + (off.sqrMagnitude > 1e-6f ? off.normalized : Vector3.down) * rope;
                // resting on whatever is below (cable goes slack)
                int n = Physics.RaycastNonAlloc(shackle + Vector3.up * 0.2f, Vector3.down, rays, DiscDepth + 0.25f, ~0, QueryTriggerInteraction.Ignore);
                float floor = float.NegativeInfinity;
                for (int i = 0; i < n; i++)
                    if (rays[i].collider && !(craneColliders != null && System.Array.IndexOf(craneColliders, rays[i].collider) >= 0)) floor = Mathf.Max(floor, rays[i].point.y);
                var t = DeformableTerrain.Instance;
                if (t) floor = Mathf.Max(floor, t.Height(shackle.x, shackle.z));
                if (shackle.y - DiscDepth < floor) { shackle.y = floor + DiscDepth; shacklePrev = Vector3.Lerp(shacklePrev, shackle, 0.5f); }
                up = (tip - shackle).sqrMagnitude > 1e-4f ? (tip - shackle).normalized : Vector3.up;
                up = Vector3.Slerp(Vector3.up, up, 0.7f);
                face = shackle - up * DiscDepth;
            }
            disc.SetPositionAndRotation(shackle, Quaternion.LookRotation(Vector3.ProjectOnPlane(turret.forward, up), up));
            if (lamp) lamp.enabled = on && (Time.time % 0.8f) < 0.55f;
            cable.enabled = true;
            cable.SetPosition(0, tip); cable.SetPosition(1, shackle);
            pendant.SetPosition(0, turret.TransformPoint(PendantTurret)); pendant.SetPosition(1, boom.TransformPoint(PendantBoom));
            var mirror = new Vector3(-1f, 1f, 1f);
            ramR?.Set(turret.TransformPoint(RamTurret), boom.TransformPoint(RamBoom), transform.up);
            ramL?.Set(turret.TransformPoint(Vector3.Scale(RamTurret, mirror)), boom.TransformPoint(Vector3.Scale(RamBoom, mirror)), transform.up);

            // sound: the magnet hums while live, the winch whines, the hydraulics moan when overloaded
            if (on) MadMax.Audio.Sfx.Loop(disc, "station_hum", 0.55f + 0.1f * held.Count, Overload ? 0.8f : 1f, 35f);
            if (strain > 0.05f) MadMax.Audio.Sfx.Loop(this, "hydraulic", 0.9f * strain, 0.6f, 50f);
            if (on && held.Count > 0 && Random.value < dt * 2f) Fx.Sparks(face, Random.insideUnitSphere, 2, new Color(0.65f, 0.8f, 1f));
        }
    }
}
