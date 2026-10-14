using System.Collections.Generic;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Knuckle-boom scrap grapple (part "grapple_crane") and, on a truck with a "CrusherLid", the hydraulic car
    /// crusher bay (the CarCrusher). While driving: 7 close the jaws on whatever is between them / open them, hold 8 / 9
    /// raise / lower the claw straight up and down (the boom and stick work together), Shift+8 / Shift+9 luff the boom,
    /// hold 0 slew (Shift+0 the other way), Shift+7 runs a crush cycle when a vehicle lies in the bay: the lid swings
    /// over and presses the car flat (glass bursts, fluids run out beside the truck, loose bits pop off) and leaves a
    /// crushed bale — a scrap stack worth the car's metal, drawn as the flattened car — for a crane or magnet to lift.</summary>
    public partial class GrappleCrusher : MonoBehaviour
    {
        public const string PartId = "grapple_crane";
        const float S = 0.08f;
        static readonly Vector3 BoomPivot = new Vector3(0f, 26f, -4f) * S;             // turret space
        const float BoomLen = 50f * S, StickLen = 26f * S;
        static readonly Vector3 GripLocal = new Vector3(0f, -12f, 0f) * S, JawBottom = new Vector3(0f, -18f, 0f) * S;   // claw space
        static readonly Vector3 RamTurret = new Vector3(2f, 17f, -7f) * S, RamBoom = new Vector3(2f, -4f, -20f) * S;
        const float LuffMin = -12f, LuffMax = 62f, KnuckleMin = -115f, KnuckleMax = 62f;
        const float JawOpen = 38f, JawShut = 2f, JawHold = 15f;
        /// <summary>Bay floor below the lid hinge, how far the lid presses down, how far it swings open (m, m, degrees).</summary>
        public const float BayDepth = 14f * S, PressDepth = 0.78f, LidOpen = 100f;

        VehicleDriver driver;
        Rigidbody rb;
        VehiclePart part; MountSocket[] sockets; float nextScan;
        Transform turret, boom, stick, claw, jawA, jawB;
        Collider[] craneColliders;
        float slew, slewSpeed, luff = 22f, knuckle = -50f, jaw = JawOpen, jawTarget = JawOpen, worked = -10f, reopenAt = -1f;
        bool closing;
        string note; float noteUntil;
        Rigidbody load; ConfigurableJoint joint; Vector3 loadLocal;
        Vector3 swingPoint, swingPrev; bool swingInit;
        HydraulicRam ramL, ramR;
        LineRenderer hoseL, hoseR;
        Outriggers outriggers;
        static readonly Collider[] hits = new Collider[48];
        static readonly RaycastHit[] rays = new RaycastHit[12];

        // crusher
        Transform lid; Collider lidCol; Vector3 hinge; Bounds lidBounds; bool hasBay;
        HydraulicRam pressF, pressB, swingRam;
        enum Cycle { Idle, Closing, Pressing, Hold, Lifting, Opening }
        Cycle cycle;
        float cycleT, lidAngle = LidOpen, press, nextCrunch, nextBeep, carH, rootAbove, squash = 1f;
        VehicleDriver victim;
        readonly List<Rigidbody> scraps = new List<Rigidbody>();
        int events;
        WorldItem lastBale;

        public string Status { get; private set; }
        public Rigidbody Load => load;
        public bool Crushing => cycle != Cycle.Idle;
        public bool HasBay => hasBay;
        /// <summary>The bale the last crush cycle left (automation, tests).</summary>
        public WorldItem LastBale => lastBale;
        public int LastBaleScrap { get; private set; }
        public Vector3 GripPoint => claw ? claw.TransformPoint(GripLocal) : transform.position;
        public float Jaw => jaw;
        /// <summary>Middle of the bay floor (world; automation, tests).</summary>
        public Vector3 BayFloor { get { Bay(out var c, out _); return transform.TransformPoint(new Vector3(c.x, hinge.y - BayDepth, c.z)); } }

        void Awake() { driver = GetComponent<VehicleDriver>(); rb = GetComponent<Rigidbody>(); }

        /// <summary>The mounted grapple (re-scanned at most once a second while there is none).</summary>
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
            stick = boom ? boom.Find("stick") : null;
            claw = stick ? stick.Find("claw") : null;
            jawA = claw ? claw.Find("jaw_a") : null;
            jawB = claw ? claw.Find("jaw_b") : null;
            craneColliders = part.GetComponentsInChildren<Collider>(true);
            foreach (var c in craneColliders) c.gameObject.layer = Layers.MachineTool;   // the claw may touch the ground without lifting the truck
            var mat = LifterKit.BodyMaterial(transform);
            ramL ??= new HydraulicRam(transform, mat, 16, 1.5f, "BoomRamL");
            ramR ??= new HydraulicRam(transform, mat, 16, 1.5f, "BoomRamR");
            if (!hoseL) { hoseL = LifterKit.Line(transform, "ClawHoseL", 0.03f); hoseR = LifterKit.Line(transform, "ClawHoseR", 0.03f); }
            outriggers ??= new Outriggers(transform, rb);
            FindBay(mat);
            swingInit = false;
            Pose();
        }

        void Unmounted()
        {
            Drop();
            part = null; turret = boom = stick = claw = jawA = jawB = null;
            ramL?.Destroy(); ramR?.Destroy(); ramL = ramR = null;
            if (hoseL) Destroy(hoseL.gameObject);
            if (hoseR) Destroy(hoseR.gameObject);
        }

        void FindBay(Material mat)
        {
            if (lid) return;
            lid = transform.Find("CrusherLid");
            hasBay = lid && lid.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh;
            if (!hasBay) return;
            hinge = lid.localPosition;
            lidBounds = lid.GetComponent<MeshFilter>().sharedMesh.bounds;   // authored closed: it covers the bay
            lidCol = lid.GetComponent<Collider>();
            pressF = new HydraulicRam(transform, mat, 11, 1.8f, "PressRamF");
            pressB = new HydraulicRam(transform, mat, 11, 1.8f, "PressRamB");
            swingRam = new HydraulicRam(transform, mat, 10, 1.3f, "LidRam");
            PoseLid();
        }

        /// <summary>The bay box in vehicle space (centre, half extents).</summary>
        void Bay(out Vector3 centre, out Vector3 half)
        {
            float floor = hinge.y - BayDepth;
            centre = new Vector3(hinge.x + lidBounds.center.x, floor + BayDepth * 0.5f + 0.25f, hinge.z + lidBounds.center.z);
            half = new Vector3(lidBounds.extents.x - 0.16f, BayDepth * 0.5f + 0.25f, lidBounds.extents.z - 0.16f);
        }

        bool InBay(Vector3 world, float margin = 0f)
        {
            Bay(out var c, out var h);
            var l = transform.InverseTransformPoint(world) - c;
            return Mathf.Abs(l.x) <= h.x + margin && Mathf.Abs(l.y) <= h.y + margin && Mathf.Abs(l.z) <= h.z + margin;
        }

        // ------------------------------------------------------------------ arm kinematics (turret space: u out along -z, v up)
        static void Forward(float luffDeg, float knuckleDeg, out Vector2 tip, out Vector2 pin)
        {
            float l = luffDeg * Mathf.Deg2Rad, k = knuckleDeg * Mathf.Deg2Rad;
            tip = new Vector2(-BoomPivot.z, BoomPivot.y) + new Vector2(Mathf.Cos(l), Mathf.Sin(l)) * BoomLen;
            pin = tip + new Vector2(Mathf.Sin(k), -Mathf.Cos(k)) * StickLen;
        }

        /// <summary>Boom and stick angles that put the claw pin at <paramref name="pin"/> (elbow up); false when out of reach.</summary>
        static bool Solve(Vector2 pin, out float luffDeg, out float knuckleDeg)
        {
            luffDeg = knuckleDeg = 0f;
            var d = pin - new Vector2(-BoomPivot.z, BoomPivot.y);
            float dist = d.magnitude;
            if (dist > BoomLen + StickLen - 0.01f || dist < Mathf.Abs(BoomLen - StickLen) + 0.01f) return false;
            float cosA = (BoomLen * BoomLen + dist * dist - StickLen * StickLen) / (2f * BoomLen * dist);
            float l = Mathf.Atan2(d.y, d.x) + Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f));
            var tip = new Vector2(-BoomPivot.z, BoomPivot.y) + new Vector2(Mathf.Cos(l), Mathf.Sin(l)) * BoomLen;
            var dir = (pin - tip) / StickLen;
            luffDeg = l * Mathf.Rad2Deg; knuckleDeg = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
            return luffDeg >= LuffMin && luffDeg <= LuffMax && knuckleDeg >= KnuckleMin && knuckleDeg <= KnuckleMax;
        }

        void Pose()
        {
            if (turret) turret.localRotation = Quaternion.Euler(0f, slew, 0f);
            if (boom) boom.localRotation = Quaternion.Euler(luff, 0f, 0f);
            if (stick) stick.localRotation = Quaternion.Euler(knuckle - luff, 0f, 0f);
        }

        /// <summary>Lowest the jaws may go here: the ground, or 0.3 m into whatever lies below (they close round it).</summary>
        bool Blocked()
        {
            if (!claw) return false;
            var bottom = claw.TransformPoint(JawBottom);
            var t = DeformableTerrain.Instance;
            if (t && bottom.y < t.Height(bottom.x, bottom.z) - 0.05f) return true;
            if (load && LoadResting()) return true;                                                  // set down: the claw stops pushing it in
            var from = claw.position;
            int n = Physics.RaycastNonAlloc(from, Vector3.down, rays, 3f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = rays[i].collider;
                if (!c || (craneColliders != null && System.Array.IndexOf(craneColliders, c) >= 0) || (load && c.attachedRigidbody == load)) continue;
                if (bottom.y < rays[i].point.y - 0.3f) return true;
            }
            return false;
        }

        /// <summary>The held load sits on something (ground, a deck, the bay floor).</summary>
        bool LoadResting()
        {
            var cols = load.GetComponentsInChildren<Collider>();
            if (cols.Length == 0) return false;
            var b = cols[0].bounds;
            foreach (var c in cols) if (c.enabled) b.Encapsulate(c.bounds);
            var t = DeformableTerrain.Instance;
            var com = load.worldCenterOfMass;
            if (t && b.min.y < t.Height(com.x, com.z) + 0.08f) return true;
            int n = Physics.RaycastNonAlloc(com, Vector3.down, rays, com.y - b.min.y + 0.12f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = rays[i].collider;
                if (!c || c.attachedRigidbody == load || (craneColliders != null && System.Array.IndexOf(craneColliders, c) >= 0)) continue;
                return true;
            }
            return false;
        }

        /// <summary>Automation: swing, luff and fold the arm so the jaws centre on <paramref name="world"/>. False when out of reach.</summary>
        public bool Reach(Vector3 world)
        {
            if (!Part || !turret) return false;
            var parent = turret.parent;
            var l = parent.InverseTransformPoint(world);
            float yaw = Mathf.Atan2(-l.x, -l.z) * Mathf.Rad2Deg;                       // the boom points along -z at slew 0
            float u = new Vector2(l.x, l.z).magnitude;
            var pin = new Vector2(u, l.y - GripLocal.y);
            if (!Solve(pin, out float lf, out float kn)) return false;
            slew = Mathf.Repeat(yaw, 360f); luff = lf; knuckle = kn; slewSpeed = 0f;
            Pose();
            return true;
        }

        /// <param name="slewDir">-1, 0 or +1</param>
        public void Control(bool press7, bool hold8, bool hold9, float slewDir, bool shift = false, bool crush = false)
        {
            var p = Part;
            if (!p) { Status = null; return; }
            float dt = Time.deltaTime;
            if (press7 || hold8 || hold9 || slewDir != 0f || crush) worked = Time.time;
            slewSpeed = Mathf.MoveTowards(slewSpeed, slewDir * 30f, 60f * dt);
            if (Mathf.Abs(slewSpeed) > 0.01f) slew = Mathf.Repeat(slew + slewSpeed * dt, 360f);
            float oldL = luff, oldK = knuckle;
            if (shift && (hold8 || hold9))
                luff = Mathf.Clamp(luff + ((hold8 ? 1f : 0f) - (hold9 ? 1f : 0f)) * 14f * dt, LuffMin, LuffMax);
            else if (hold8 || hold9)
            {
                Forward(luff, knuckle, out _, out var pin);
                pin.y += ((hold8 ? 1f : 0f) - (hold9 ? 1f : 0f)) * 0.9f * dt;
                if (Solve(pin, out float lf, out float kn)) { luff = lf; knuckle = kn; }
            }
            Pose();
            if ((hold9 || (shift && hold9)) && Blocked()) { luff = oldL; knuckle = oldK; Pose(); }
            if ((press7 && shift) || crush) StartCrush();
            else if (press7) { if (load) Drop(); else if (!closing) { closing = true; jawTarget = JawShut; } }

            string what = load ? load.name.Replace("(Clone)", "").Replace("Item ", "").ToUpperInvariant() + " " + Mathf.RoundToInt(load.mass) + " KG" : null;
            Status = cycle != Cycle.Idle ? "CRUSHER: " + (cycle == Cycle.Closing ? "LID CLOSING" : cycle == Cycle.Pressing ? "PRESSING " + Mathf.RoundToInt(press / PressDepth * 100f) + "%" : cycle == Cycle.Hold ? "BALED" : "LID OPENING")
                   : load ? "GRAPPLE: " + what + "  [7] RELEASE  [8/9] RAISE / LOWER  [0] SLEW"
                   : Time.time < noteUntil ? "GRAPPLE: " + note
                   : "GRAPPLE  [7] GRAB  [8/9] RAISE / LOWER  [SHIFT+8/9] BOOM  [0] SLEW" + (hasBay ? "  [SHIFT+7] CRUSH" : "");
        }

        void Say(string s) { note = s; noteUntil = Time.time + 2.5f; }

        void TryGrab()
        {
            var grip = GripPoint;
            Rigidbody best = null; float bd = 1.1f; Vector3 bp = grip;
            int n = Physics.OverlapSphereNonAlloc(grip, 1.1f, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i];
                var r = c.attachedRigidbody;
                if (!r || r == rb || !LifterKit.Liftable(r, transform) || !MadMax.Net.NetSession.MayLift(r)) continue;
                if (victim && r == victim.Body) continue;
                var cp = LifterKit.Closest(c, grip);
                float d = Vector3.Distance(cp, grip);
                if (d < bd) { bd = d; best = r; bp = cp; }
            }
            if (!best) return;
            if (best.mass > rb.mass * 0.9f) { Say("TOO HEAVY (" + Mathf.RoundToInt(best.mass) + " KG)"); return; }
            best.isKinematic = false;
            load = best;
            loadLocal = load.transform.InverseTransformPoint(bp);
            joint = rb.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = load;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = transform.InverseTransformPoint(grip);
            joint.connectedAnchor = loadLocal;
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Limited;
            joint.linearLimit = new SoftJointLimit { limit = 0.05f };
            joint.linearLimitSpring = new SoftJointLimitSpring { spring = 600000f, damper = 40000f };
            joint.enableCollision = true;
            LifterKit.IgnoreLoad(load, craneColliders, true);
            closing = false; jawTarget = JawHold;
            MadMax.Audio.Sfx.Play("hit_metal", bp, 0.8f, 0.85f, 45f, 0.1f);
            Fx.Sparks(bp, Vector3.up, 12, new Color(1f, 0.85f, 0.45f));
        }

        public void Drop()
        {
            if (joint) Destroy(joint);
            if (load) { LifterKit.IgnoreLoad(load, craneColliders, false); load.WakeUp(); MadMax.Net.NetSession.ReleaseCarry(load); }
            joint = null; load = null;
            jawTarget = JawOpen; closing = false;
        }

        // ------------------------------------------------------------------ crusher
        void StartCrush()
        {
            if (!hasBay) { Say("NO CRUSHER BAY ON THIS TRUCK"); return; }
            if (cycle != Cycle.Idle) return;
            if (claw && InBay(GripPoint, 0.6f)) { Say("SWING THE CLAW CLEAR OF THE BAY"); return; }
            Bay(out var c, out var h);
            var centre = transform.TransformPoint(c);
            VehicleDriver best = null;
            scraps.Clear();
            int n = Physics.OverlapBoxNonAlloc(centre, h, hits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var r = hits[i].attachedRigidbody;
                if (!r || r == rb || !InBay(r.worldCenterOfMass, 0.2f)) continue;
                if (r.TryGetComponent<VehicleDriver>(out var v)) { if (v != driver && (!best || r.mass > best.Body.mass)) best = v; }
                else if (r.TryGetComponent<VehiclePart>(out var vp) && !vp.IsMounted && !scraps.Contains(r)) scraps.Add(r);
            }
            if (!best && scraps.Count == 0) { Say("THE BAY IS EMPTY"); return; }
            if (best && (best.Occupied || best.aiDriven)) { Say("SOMEONE IS IN THAT CAR"); return; }
            if (best && load == best.Body) Drop();
            foreach (var s in scraps) if (s == load) Drop();
            victim = best;
            remoteCrush = false;
            MadMax.Net.NetSession.Instance?.SendCrush(driver, victim, scraps);                   // the other peers press the same car
            BeginCycle(c, h);
        }

        /// <summary>Pin and line up what is in the bay and start the press (<paramref name="c"/>, <paramref name="h"/>: the bay box).</summary>
        void BeginCycle(Vector3 c, Vector3 h)
        {
            float floor = transform.TransformPoint(new Vector3(0f, hinge.y - BayDepth, 0f)).y;
            if (victim)
            {
                var vb = victim.Body;
                if (victim.TryGetComponent<MadMax.Net.NetReplica>(out var rep)) rep.enabled = false;   // held here for the press, not interpolated
                Pin(vb);
                foreach (var col in victim.GetComponentsInChildren<Collider>()) col.enabled = false;
                var bayLocal = transform.InverseTransformPoint(victim.transform.position);
                bayLocal.x = c.x;
                bayLocal.z = Mathf.Clamp(bayLocal.z, c.z - h.z + 1.6f, c.z + h.z - 1.6f);
                victim.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(victim.transform.forward, transform.up), transform.up);
                victim.transform.position = transform.TransformPoint(bayLocal);
                var b = WorldBounds(victim.transform);
                rootAbove = victim.transform.position.y - b.min.y;
                carH = Mathf.Max(0.5f, b.size.y);
                victim.transform.position += Vector3.up * (floor + rootAbove - victim.transform.position.y);
                victim.KeepAwakeUntil = Time.time + 20f;
                if (!remoteCrush) PopParts(victim);                                                 // remote: the parts come as PartDetached
            }
            foreach (var s in scraps) { Pin(s); foreach (var col in s.GetComponentsInChildren<Collider>()) col.enabled = false; }
            cycle = Cycle.Closing; cycleT = 0f; events = 0; squash = 1f; nextCrunch = 0f;
            worked = Time.time;
            MadMax.Audio.Sfx.Play("creak", transform.TransformPoint(c), 0.8f, 0.7f, 40f);
        }

        /// <summary>Held still for the press (frozen as well as kinematic: the sleeper pass wakes kinematic cars near the player).</summary>
        static void Pin(Rigidbody r)
        {
            r.isKinematic = true; r.useGravity = false; r.detectCollisions = false;
            r.constraints = RigidbodyConstraints.FreezeAll;
        }

        static Bounds WorldBounds(Transform t)
        {
            bool any = false; var b = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || r.name == "Driver") continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>Lamps, mirrors and trim burst off as the lid lands (two at most).</summary>
        void PopParts(VehicleDriver v)
        {
            var rnd = new System.Random(v.netId * 31 + 5);
            int popped = 0;
            foreach (var s in v.GetComponentsInChildren<MountSocket>())
            {
                if (popped >= 2 || !s.Current) continue;
                var cat = s.Current.category;
                if (cat != PartCategory.Lights && cat != PartCategory.FrontBumper && cat != PartCategory.RearBumper && cat != PartCategory.Exhaust) continue;
                if (rnd.NextDouble() < 0.45) continue;
                var p = s.Detach(true);
                if (p) MadMax.Net.NetSession.Instance?.SendPartDetached(v, s.name, p, 0);
                if (!p || !p.TryGetComponent<Rigidbody>(out var pr)) continue;
                pr.isKinematic = false;
                float side = Vector3.Dot(p.transform.position - v.transform.position, transform.right) >= 0f ? 1f : -1f;
                pr.linearVelocity = transform.right * side * (3f + (float)rnd.NextDouble() * 2f) + Vector3.up * 4f;
                pr.angularVelocity = Random.insideUnitSphere * 6f;
                popped++;
            }
        }

        void Crunch(Vector3 at, float vol)
        {
            MadMax.Audio.Sfx.Play(Random.value < 0.6f ? "crash_small" : "creak", at, vol, 0.7f + Random.value * 0.3f, 45f, 0.1f);
            Fx.Sparks(at, Random.insideUnitSphere + Vector3.up, 8, new Color(1f, 0.8f, 0.4f));
            Fx.Smoke(at, Random.insideUnitSphere * 0.5f + Vector3.up * 0.4f, 0.6f, new Color(0.6f, 0.55f, 0.48f, 0.5f), 1.4f);
        }

        void Drain(VehicleDriver v, Vector3 at)
        {
            if (!v.TryGetComponent<VehicleSystems>(out var sys)) return;
            foreach (var s in new[] { VehicleSystems.FluidSystem.Fuel, VehicleSystems.FluidSystem.Oil, VehicleSystems.FluidSystem.Coolant })
            {
                var mix = new FluidMix();
                float l = sys.Draw(s, 9999f, mix);
                if (l > 0.05f && !mix.Empty) Spills.Pour(at, mix, l);
            }
            MadMax.Audio.Sfx.Play("pour", at, 0.7f, 0.8f, 25f);
        }

        void TickCrush(float dt)
        {
            cycleT += dt;
            Bay(out var c, out _);
            var centre = transform.TransformPoint(c);
            float floor = transform.TransformPoint(new Vector3(c.x, hinge.y - BayDepth, c.z)).y;
            bool moving = true;
            switch (cycle)
            {
                case Cycle.Closing:
                    lidAngle = Mathf.Lerp(LidOpen, 0f, Mathf.SmoothStep(0f, 1f, cycleT / 1.5f));
                    if (cycleT >= 1.5f)
                    {
                        lidAngle = 0f; cycle = Cycle.Pressing; cycleT = 0f;
                        MadMax.Audio.Sfx.Play("crash_big", centre, 1f, 0.75f, 70f);
                        Crunch(centre + Vector3.up * 0.6f, 0.8f);
                        var g = MadMax.Game.WastelandGame.Instance;
                        if (g && g.cameraRig && Vector3.Distance(g.FocusPos, centre) < 25f) g.cameraRig.Shake(0.6f);
                    }
                    break;
                case Cycle.Pressing:
                    float t = Mathf.Clamp01(cycleT / 2.8f);
                    press = PressDepth * (1f - (1f - t) * (1f - t));
                    if (victim && t > 0.15f && (events & 1) == 0)
                    {
                        events |= 1;
                        if (victim.TryGetComponent<VehicleBreakables>(out var br)) br.Smash(victim.Body.worldCenterOfMass, 4f, 8f);
                        MadMax.Audio.Sfx.Play("glass_break", centre, 1f, 0.9f, 40f);
                    }
                    if (victim && t > 0.3f && (events & 2) == 0)
                    {
                        events |= 2;
                        Bay(out var bc, out var bh);
                        var spout = transform.TransformPoint(new Vector3(bc.x - bh.x - 0.8f, 0f, bc.z - bh.z + 0.6f));
                        var tr = DeformableTerrain.Instance;
                        if (tr) spout.y = tr.Height(spout.x, spout.z);
                        if (!remoteCrush) Drain(victim, spout);                                          // spills replicate from the simulating side
                        for (int i = 0; i < 5; i++) Fx.Smoke(spout + Vector3.up * 0.2f, Random.insideUnitSphere * 0.3f, 0.4f, new Color(0.15f, 0.12f, 0.1f, 0.6f), 1.2f);
                    }
                    if (victim && victim.TryGetComponent<VehicleDamage>(out var vd))
                    {
                        if (t > 0.35f && (events & 4) == 0) { events |= 4; vd.DentAt(victim.Body.worldCenterOfMass + transform.up * carH, -transform.up, 0.12f, 0.8f); }
                        if (t > 0.65f && (events & 8) == 0) { events |= 8; vd.DentAt(victim.Body.worldCenterOfMass + transform.up * carH + transform.forward, -transform.up, 0.1f, 0.7f); }
                    }
                    if (Time.time >= nextCrunch)
                    {
                        nextCrunch = Time.time + Random.Range(0.25f, 0.5f);
                        Bay(out var bc2, out var bh2);
                        var edge = transform.TransformPoint(bc2 + new Vector3(Random.Range(-bh2.x, bh2.x), BayDepth * 0.5f - press, Random.value < 0.5f ? -bh2.z : bh2.z));
                        Crunch(edge, 0.7f);
                    }
                    if (t >= 1f) { if (remoteCrush) RemoteBale(); else Bale(floor); cycle = Cycle.Hold; cycleT = 0f; }
                    break;
                case Cycle.Hold:
                    moving = false;
                    if (cycleT >= 0.5f) { cycle = Cycle.Lifting; cycleT = 0f; MadMax.Audio.Sfx.Play("hit_metal", centre, 0.8f, 0.6f, 40f); }
                    break;
                case Cycle.Lifting:
                    press = Mathf.Lerp(PressDepth, 0f, Mathf.SmoothStep(0f, 1f, cycleT / 1.3f));
                    if (cycleT >= 1.3f) { press = 0f; cycle = Cycle.Opening; cycleT = 0f; }
                    break;
                case Cycle.Opening:
                    lidAngle = Mathf.Lerp(0f, LidOpen, Mathf.SmoothStep(0f, 1f, cycleT / 1.7f));
                    if (cycleT >= 1.7f)
                    {
                        lidAngle = LidOpen; cycle = Cycle.Idle; remoteCrush = false;
                        if (lastBale && lastBale.Box && lidCol) Physics.IgnoreCollision(lastBale.Box, lidCol, false);
                    }
                    break;
            }
            if (moving) MadMax.Audio.Sfx.Loop(this, "hydraulic", cycle == Cycle.Pressing ? 1f : 0.7f, cycle == Cycle.Pressing ? 0.65f : 0.9f, 55f);
            if (moving && Time.time >= nextBeep) { nextBeep = Time.time + 0.7f; MadMax.Audio.Sfx.Play("beep", centre, 0.35f, 1.6f, 30f); }

            // squash what is in the bay to the gap under the lid (scaled about its base, kept on the floor)
            if (cycle == Cycle.Closing || cycle == Cycle.Pressing)
            {
                float over = lidAngle >= 89f ? 99f : BayDepth - press + Mathf.Sin(lidAngle * Mathf.Deg2Rad) * Mathf.Abs(lidBounds.center.x);
                float minSquash = (BayDepth - PressDepth - 0.04f) / Mathf.Max(0.5f, carH);
                squash = Mathf.Min(squash, Mathf.Clamp(over / Mathf.Max(0.5f, carH), minSquash, 1f));
                if (victim)
                {
                    victim.transform.localScale = new Vector3(1f + (1f - squash) * 0.08f, squash, 1f - (1f - squash) * 0.04f);
                    var pos = victim.transform.position; pos.y = floor + rootAbove * squash; victim.transform.position = pos;
                }
                foreach (var s in scraps) if (s) s.transform.localScale = new Vector3(1.1f, Mathf.Max(0.3f, squash), 1.05f);
            }
            PoseLid();
        }

        void Bale(float floor)
        {
            int units = 0;
            Mesh mesh = null; Material mat = null; Quaternion rot = transform.rotation; Vector3 at = transform.TransformPoint(new Vector3(hinge.x + lidBounds.center.x, 0f, hinge.z + lidBounds.center.z));
            if (victim)
            {
                var vd = victim.GetComponent<VehicleDamage>();
                units += Mathf.CeilToInt(vd ? vd.SalvageLeft : victim.Body.mass / 25f);
                foreach (var p in victim.GetComponentsInChildren<VehiclePart>()) units += Mathf.RoundToInt(p.mass / 40f);
                mesh = LifterKit.BaleFrom(victim, Mathf.Clamp((BayDepth - PressDepth - 0.04f) / carH, 0.12f, 0.4f), out mat);
                rot = victim.transform.rotation;
                at = victim.transform.position;
            }
            foreach (var s in scraps) if (s) units += Mathf.Max(1, Mathf.RoundToInt(s.mass / 25f));
            at.y = floor + 0.02f;
            if (!mat) mat = LifterKit.BodyMaterial(transform);
            lastBale = LifterKit.SpawnBale(units, mesh ? mesh : LifterKit.GenericBale(), mat, at, rot);
            LastBaleScrap = units;
            MadMax.Net.NetSession.Instance?.SendItemSpawn(lastBale, Vector3.zero);                 // a world item on every peer (host-owned)
            if (lastBale.Box && lidCol) Physics.IgnoreCollision(lastBale.Box, lidCol, true);
            // the rubber and glass mostly go: a little falls out beside the bay
            var pickups = PickupSystem.Instance;
            if (pickups && victim)
            {
                var side = at + transform.right * -2.1f + Vector3.up * 0.4f;
                pickups.Spawn(ResourceType.Rubber, 2, side, -transform.right * 1.5f + Vector3.up);
                pickups.Spawn(ResourceType.Glass, 1, side + transform.forward * 0.6f, -transform.right + Vector3.up);
            }
            if (victim)
            {
                if (victim.TryGetComponent<VehicleDamage>(out var vd)) vd.Crush();
                else Destroy(victim.gameObject);
            }
            foreach (var s in scraps) if (s) Destroy(s.gameObject);
            scraps.Clear();
            victim = null;
            MadMax.Audio.Sfx.Play("crash_big", at, 0.9f, 0.55f, 60f);
            MadMax.Game.WastelandGame.Instance?.Toast("CRUSHED: A BALE OF " + units + " SCRAP");
        }

        void PoseLid()
        {
            if (!lid) return;
            lid.localRotation = Quaternion.Euler(0f, 0f, lidAngle);
            lid.localPosition = hinge + Vector3.down * press;
            var up = transform.up;
            float zf = lidBounds.max.z - 0.3f, zb = lidBounds.min.z + 0.3f;
            pressF?.Set(transform.TransformPoint(hinge + new Vector3(-0.1f, -BayDepth - 0.5f, zf)), lid.TransformPoint(new Vector3(-0.1f, 0f, zf)), transform.right);
            pressB?.Set(transform.TransformPoint(hinge + new Vector3(-0.1f, -BayDepth - 0.5f, zb)), lid.TransformPoint(new Vector3(-0.1f, 0f, zb)), transform.right);
            swingRam?.Set(transform.TransformPoint(hinge + new Vector3(-0.55f, -0.95f, lidBounds.center.z)), lid.TransformPoint(new Vector3(-0.24f, 0.24f, lidBounds.center.z)), up);
        }

        void FixedUpdate()
        {
            if (!part) return;
            bool stopped = driver && Mathf.Abs(driver.ForwardSpeed) < 0.6f && driver.throttleInput < 0.05f;
            bool wantLegs = stopped && (load || cycle != Cycle.Idle || Time.time - worked < 4f);
            if (outriggers != null && outriggers.Any)
            {
                if (outriggers.Tick(wantLegs, Time.fixedDeltaTime)) MadMax.Audio.Sfx.Loop(this, "hydraulic", 0.5f, 0.8f, 40f);
                outriggers.Push();
            }
            if (!load && cycle == Cycle.Idle) return;
            driver.KeepAwakeUntil = Time.time + 0.5f;
            if (!load) return;
            if (load.TryGetComponent<VehicleDriver>(out var other)) other.KeepAwakeUntil = Time.time + 0.5f;
            if (load.IsSleeping()) load.WakeUp();
        }

        void Update()
        {
            if (!part) return;
            if (cycle != Cycle.Idle) TickCrush(Time.deltaTime);
        }

        void LateUpdate()
        {
            if (!Part || !claw) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            // jaws: closing on a grab look for something between them; empty, they snap shut and reopen
            jaw = Mathf.MoveTowards(jaw, jawTarget, 85f * dt);
            if (closing && !load && jaw <= JawHold + 2f) TryGrab();
            if (closing && !load && jaw <= JawShut + 0.5f)
            {
                closing = false; reopenAt = Time.time + 0.6f;
                if (Time.time >= noteUntil) Say("NOTHING IN THE JAWS");
                MadMax.Audio.Sfx.Play("hit_metal", GripPoint, 0.5f, 1.3f, 30f, 0.2f);
            }
            if (reopenAt > 0f && Time.time >= reopenAt) { reopenAt = -1f; jawTarget = JawOpen; }
            if (jawA) jawA.localRotation = Quaternion.Euler(0f, 0f, -jaw);
            if (jawB) jawB.localRotation = Quaternion.Euler(0f, 0f, jaw);

            // the claw hangs from its rotator pin: plumb, swinging a little behind the arm's motion
            var pin = claw.position;
            if (!swingInit) { swingPoint = swingPrev = pin + Vector3.down * 0.7f; swingInit = true; }
            var v = (swingPoint - swingPrev) * Mathf.Pow(0.08f, dt);
            swingPrev = swingPoint;
            swingPoint += v + Physics.gravity * dt * dt;
            var off = swingPoint - pin;
            swingPoint = pin + (off.sqrMagnitude > 1e-6f ? off.normalized : Vector3.down) * 0.7f;
            var hang = load ? Vector3.up : Vector3.Slerp(Vector3.up, (pin - swingPoint).normalized, 0.6f);
            claw.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(turret.forward, hang), hang);

            var grip = GripPoint;
            if (joint) joint.anchor = transform.InverseTransformPoint(grip);
            var mirror = new Vector3(-1f, 1f, 1f);
            ramR?.Set(turret.TransformPoint(RamTurret), boom.TransformPoint(RamBoom), transform.up);
            ramL?.Set(turret.TransformPoint(Vector3.Scale(RamTurret, mirror)), boom.TransformPoint(Vector3.Scale(RamBoom, mirror)), transform.up);
            // hoses from the boom tip down to the rotator
            var tipTop = stick.TransformPoint(new Vector3(0f, 1.5f, 0.3f) * S * 2f);
            hoseL.SetPosition(0, tipTop + stick.right * 0.1f); hoseL.SetPosition(1, claw.TransformPoint(new Vector3(-2f, -4f, 0f) * S));
            hoseR.SetPosition(0, tipTop - stick.right * 0.1f); hoseR.SetPosition(1, claw.TransformPoint(new Vector3(2f, -4f, 0f) * S));
            if (Mathf.Abs(jaw - jawTarget) > 0.5f || Mathf.Abs(slewSpeed) > 1f) MadMax.Audio.Sfx.Loop(this, "hydraulic", 0.45f, 1.15f, 35f);
        }

        void OnDestroy()
        {
            pressF?.Destroy(); pressB?.Destroy(); swingRam?.Destroy();
        }
    }
}
