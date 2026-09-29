using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Raycast-suspension vehicle. Wheels, engine and their stats come from whatever parts are mounted,
    /// so swapping parts changes handling. Drives on DeformableTerrain and deforms it.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(VehicleChassis))]
    public class VehicleDriver : MonoBehaviour
    {
        public enum Drive { Rear, Front, All }

        public Drive drive = Drive.Rear;
        [Header("Network")]
        [Tooltip("Stable id shared by all peers.")] public ushort netId;
        [Tooltip("Player id simulating this vehicle (0 = server).")] public ushort owner;
        [Tooltip("Driver can switch between 2WD and 4WD (X).")]
        public bool awdSelectable;
        [Tooltip("Vehicle has lockable differentials (L).")]
        public bool hasDiffLock;
        public bool diffLocked;
        [Tooltip("Manual gearbox: R N 1..n with ShiftUp/ShiftDown. Set from game settings.")]
        public bool manual;
        [System.NonSerialized] public float gripMultiplier = 1f;
        /// <summary>Cranes and winches keep both ends simulated (no resting sleep) until this time.</summary>
        [System.NonSerialized] public float KeepAwakeUntil;
        /// <summary>Driven by an NPC (<see cref="MadMax.Npc.AiDriver"/>): never frozen far away, not saved, automatic gearbox.</summary>
        [System.NonSerialized] public bool aiDriven;
        [Tooltip("Extra ground clearance (m) on top of the suspension geometry.")] public float rideHeight = 0.07f;   // driver skill (set by the game for the player's vehicle)
        [Tooltip("False for trailers: cannot be entered or driven.")]
        public bool driveable = true;

        [Header("Suspension")]
        public float travel = 0.28f;
        public float frequency = 1.7f;
        [Range(0.1f, 1.5f)] public float damping = 0.55f;
        public float antiRoll = 0.6f;

        [Header("Drivetrain")]
        public float[] gears = { 3.1f, 2.0f, 1.45f, 1.1f, 0.88f };
        public float reverseRatio = 3.0f;
        public float finalDrive = 3.7f;
        public float efficiency = 0.85f;

        [Header("Handling")]
        public float maxSteer = 32f;
        public float steerSpeed = 3f;
        public float brakeForce = 16000f;
        [Range(0.05f, 1f)] public float handbrakeGrip = 0.4f;
        public float drag = 0.45f;

        [Header("Input (set by controller)")]
        [Range(-1, 1)] public float steerInput;
        [Range(0, 1)] public float throttleInput;
        [Range(0, 1)] public float brakeInput;
        public bool handbrake;
        [Tooltip("Steering offset (deg) from a bent frame; set by VehicleDamage.")]
        public float steerPull;
        /// <summary>Front-wheel angle (deg) set each step by <see cref="BikeBalance"/>; NaN = from <see cref="steerInput"/>.</summary>
        [System.NonSerialized] public float steerOverride = float.NaN;
        /// <summary>Rider's throttle control (bikes): drive capped at this multiple of the tyre's grip; 0 = off.</summary>
        [System.NonSerialized] public float tractionLimit;
        /// <summary>Tractive force at the driven tyres this step (N).</summary>
        public float DriveForce { get; private set; }
        [Tooltip("Centre of mass offset to the right (m): sidecar outfits.")] public float comOffsetX;
        [Tooltip("Aircraft: wheels roll free (the propeller pushes, FlightModel), the gearbox never reverses.")] public bool aircraft;
        [Tooltip("Use centerOfMass instead of the body-bounds estimate.")] public bool customCom;
        public Vector3 centerOfMass;

        public int Gear { get; private set; } = 1;
        public float DriveCommand { get; private set; }
        public bool FourWheelDrive => drive == Drive.All;
        Drive twoWheelDrive = Drive.Rear;
        VehicleSystems systems;
        VehicleTuning tuning;
        int frontWheels;
        public bool Reversing { get; private set; }
        public float Rpm { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float SpeedKmh => ForwardSpeed * 3.6f;
        public float WheelSlip { get; private set; }
        public float Mud { get; private set; }
        /// <summary>Tyre audio telemetry (per grounded wheel averages): sliding on hard ground, scrub speed on loose ground,
        /// speed through mud, rolling speed on hard ground (m/s).</summary>
        public float TyreSqueal { get; private set; }
        public float TyreLoose { get; private set; }
        public float TyreMud { get; private set; }
        public float TyreRoll { get; private set; }
        public EngineStats Engine => engine;

        /// <summary>A driver is inside. Unoccupied vehicles hold the handbrake and hide the driver mesh.</summary>
        public bool Occupied
        {
            get => occupied;
            set { occupied = value; RefreshDriverMesh(); }
        }

        /// <summary>First-person view hides the occupant's own body.</summary>
        public void SetDriverVisible(bool visible) { driverVisible = visible; RefreshDriverMesh(); }

        /// <summary>Show the baked voxel driver (NPC-style decoration). The player's own body sits in the seat instead.</summary>
        public bool bakedDriver;

        void RefreshDriverMesh() { if (driverMesh) driverMesh.enabled = bakedDriver && occupied && driverVisible; }

        bool occupied, driverVisible = true;
        Renderer driverMesh;
        public Rigidbody Body => rb;

        class Wheel
        {
            public MountSocket socket;
            public VehiclePart part;
            public WheelStats stats;
            public bool front, left, grounded, idler;   // idler: a sidecar wheel (neither driven nor steered)
            public float radius, width, comp, prevComp, spin;
            public Vector3 contact, fwd, side;
            public float maxF, drive, diffSpin, vf, vs, spring;
            public bool broken;          // tyre broke traction last step (kinetic friction is lower)
            public bool deck;            // on a built deck (floor, foundation, ramp): firm, no ruts
            public Surface surf;
        }

        readonly List<Wheel> wheels = new List<Wheel>();
        Rigidbody rb;
        VehicleChassis chassis;
        EngineStats engine;
        float steer, massPerWheel, k, c, restComp, shiftTimer, restTimer;

        /// <summary>Wheels touching the ground last step (0 = airborne: jumps, hang time).</summary>
        public int WheelsDown { get { int n = 0; foreach (var w in wheels) if (w.part && w.grounded) n++; return n; } }

        bool AllGroundedLastStep()
        {
            foreach (var w in wheels) if (w.part && !w.grounded) return false;
            return true;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (!GetComponent<MadMax.Audio.VehicleAudio>()) gameObject.AddComponent<MadMax.Audio.VehicleAudio>();
            chassis = GetComponent<VehicleChassis>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            chassis.Changed += Rebuild;
            systems = GetComponent<VehicleSystems>();
            tuning = GetComponent<VehicleTuning>();
            twoWheelDrive = drive == Drive.All ? Drive.Rear : drive;
            var d = transform.Find("Body/Driver");
            driverMesh = d ? d.GetComponent<Renderer>() : null;
            RefreshDriverMesh();
            Rebuild();
        }

        void OnDestroy() { if (chassis) chassis.Changed -= Rebuild; }

        public void Rebuild()
        {
            wheels.Clear();
            engine = null;
            foreach (var s in chassis.Sockets)
            {
                if (s.accepts == PartCategory.Wheel)
                {
                    var w = new Wheel { socket = s, part = s.Current, front = s.transform.localPosition.z > 0f, left = s.Mirrored, idler = s.name.StartsWith("wheel_side") };
                    if (w.idler) w.front = false;
                    if (w.part)
                    {
                        w.stats = w.part.GetComponent<WheelStats>();
                        w.radius = w.part.radius > 0 ? w.part.radius : 0.4f;
                        w.width = w.stats ? w.stats.width : 0.3f;
                    }
                    wheels.Add(w);
                }
                else if (!engine && s.Current) engine = s.Current.GetComponent<EngineStats>();
            }
            rb.mass = chassis.TotalMass;
            massPerWheel = rb.mass / Mathf.Max(1, wheels.Count);
            float w0 = 2f * Mathf.PI * frequency;
            k = massPerWheel * w0 * w0;
            c = 2f * damping * Mathf.Sqrt(k * massPerWheel);
            restComp = massPerWheel * Physics.gravity.magnitude / k;
            var body = transform.Find("Body");
            if (body && body.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh)
            {
                var b = mf.sharedMesh.bounds;
                float cx = comOffsetX;
                if (TryGetComponent<BikeBalance>(out var bike) && !bike.sidecar)
                {
                    // a single-track bike balances over its tyres' contact line
                    int n = 0; cx = 0f;
                    foreach (var w in wheels) if (!w.idler) { cx += w.socket.transform.localPosition.x + (w.left ? -0.5f : 0.5f) * w.width; n++; }
                    cx = n > 0 ? cx / n : 0f;
                }
                rb.centerOfMass = customCom ? centerOfMass : new Vector3(cx, b.min.y + 0.25f, b.center.z);
            }
        }

        bool IsDriven(Wheel w) => !w.idler && !aircraft && (drive == Drive.All || (drive == Drive.Front) == w.front);

        float DrivenRadius()
        {
            float sum = 0; int n = 0;
            foreach (var w in wheels) if (w.part && IsDriven(w)) { sum += w.radius; n++; }
            return n > 0 ? sum / n : 0.4f;
        }

        void FixedUpdate()
        {
            var terrain = DeformableTerrain.Instance;
            if (!terrain || rb.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            // Rest: a parked vehicle would otherwise jiggle forever on its springs (forces keep it awake), and every
            // camera following it re-samples the low-res image each frame (visible "blinking" terrain).
            if (KeepAwakeUntil > Time.time) { restTimer = 0f; if (rb.IsSleeping()) rb.WakeUp(); }
            bool idleInput = KeepAwakeUntil <= Time.time && throttleInput < 0.01f && Mathf.Abs(steerInput) < 0.01f && (handbrake || brakeInput > 0.1f);
            if (idleInput && rb.linearVelocity.sqrMagnitude < 0.02f && rb.angularVelocity.sqrMagnitude < 0.02f && AllGroundedLastStep())
            {
                restTimer += dt;
                if (restTimer > 0.4f) { if (!rb.IsSleeping()) rb.Sleep(); return; }
            }
            else restTimer = 0f;
            if (rb.IsSleeping()) rb.WakeUp();
            Vector3 up = transform.up;
            var vel = rb.linearVelocity;
            ForwardSpeed = Vector3.Dot(vel, transform.forward);

            float speedFactor = Mathf.Lerp(1f, 0.3f, Mathf.Abs(ForwardSpeed) / 40f);
            float wantSteer = float.IsNaN(steerOverride) ? steerInput * maxSteer * speedFactor : steerOverride;
            steer = Mathf.MoveTowards(steer, wantSteer + steerPull, maxSteer * steerSpeed * dt);

            // ---- transmission: automatic (brake at standstill = reverse) or manual (R N 1..n via ShiftUp/ShiftDown)
            float driveCmd, brakeCmd;
            if (manual)
            {
                Reversing = Gear < 0;
                driveCmd = throttleInput; brakeCmd = brakeInput;
            }
            else
            {
                if (Gear < 1) Gear = 1;
                if (!Reversing && !aircraft && brakeInput > 0.1f && throttleInput < 0.1f && ForwardSpeed < 0.5f) Reversing = true;
                if (Reversing && throttleInput > 0.1f && ForwardSpeed > -0.5f) Reversing = false;
                driveCmd = Reversing ? brakeInput : throttleInput;
                brakeCmd = Reversing ? throttleInput : brakeInput;
            }
            DriveCommand = driveCmd;

            int driven = 0;
            foreach (var w in wheels) if (w.part && w.grounded && IsDriven(w)) driven++;
            frontWheels = 0; foreach (var w in wheels) if (w.front) frontWheels++;

            float driveForce = 0f;
            float power = systems ? systems.PowerFactor : 1f;
            if (engine && power > 0f)
            {
                bool neutral = manual && Gear == 0;
                float ratio = neutral ? 0f : (Reversing ? reverseRatio : gears[Mathf.Clamp(Gear, 1, gears.Length) - 1]) * finalDrive;
                float r = DrivenRadius();
                float wheelRpm = Mathf.Abs(ForwardSpeed) / r * 60f / (2f * Mathf.PI);
                // clutch slip at pull-away (brake-torqued: higher). Uphill the driver slips it nearer the torque peak
                float uphill = Mathf.Clamp01((Vector3.Dot(transform.forward, Vector3.up) * (Reversing ? -1f : 1f) - 0.04f) * 6f) * Mathf.Clamp01(1f - Mathf.Abs(ForwardSpeed) / 4f);
                float launchFrac = brakeCmd > 0.5f ? 0.8f : Mathf.Lerp(0.45f, Mathf.Max(0.55f, engine.peakAt), uphill);
                float launch = engine.idleRpm + Mathf.Max(driveCmd, uphill * 0.8f) * (engine.maxRpm * launchFrac - engine.idleRpm);
                float target = neutral ? Mathf.Lerp(engine.idleRpm, engine.maxRpm, driveCmd) : Mathf.Max(launch, wheelRpm * ratio);
                // while the clutch (or an automatic's torque converter) slips, torque at the wheels is multiplied:
                // up to 1.9x from a standstill in an automatic, 1.3x slipping a manual clutch hard
                float coupling = neutral ? 1f : Mathf.Clamp01(wheelRpm * ratio / Mathf.Max(1f, launch));
                float launchBoost = Mathf.Lerp(manual ? 1.3f : 1.9f, 1f, coupling);
                float freeRev = driven == 0 ? 1f : WheelSlip;
                if (!neutral) target = Mathf.Lerp(target, engine.maxRpm, driveCmd * Mathf.Clamp01(freeRev));
                Rpm = Mathf.Lerp(Rpm, Mathf.Min(target, engine.maxRpm), 12f * dt);
                float health = 1f - 0.7f * Mathf.Clamp01(engine.GetComponent<VehiclePart>().damage);
                float torque = Rpm >= engine.maxRpm * 0.995f ? 0f : engine.TorqueAt(Rpm) * driveCmd * health * power * launchBoost * (tuning ? tuning.TorqueFactor(Rpm / engine.maxRpm) : 1f);
                driveForce = neutral ? 0f : torque * ratio * efficiency / r * (Reversing ? -1f : 1f);

                shiftTimer -= dt;
                if (!manual)
                {
                    if (Reversing) Gear = 1;
                    else if (shiftTimer <= 0f)
                    {
                        if (Rpm > engine.maxRpm * 0.9f && Gear < gears.Length && WheelSlip < 0.3f) { Gear++; shiftTimer = 0.5f; }
                        else if (Rpm < engine.maxRpm * 0.4f && Gear > 1) { Gear--; shiftTimer = 0.5f; }
                    }
                }
            }
            else Rpm = Mathf.Lerp(Rpm, 0f, 3f * dt);

            // ---- pass 1: suspension + tyre state
            foreach (var w in wheels)
            {
                w.prevComp = w.comp;
                w.grounded = false;
                w.maxF = 0f; w.drive = 0f; w.diffSpin = 0f;
                if (!w.part) continue;

                Vector3 outward = transform.right * (w.left ? -1f : 1f);
                Vector3 anchor = w.socket.transform.position + outward * (w.width * 0.5f) + up * (travel - restComp + rideHeight);
                float rayLen = travel + w.radius * (w.stats && w.stats.Popped ? 0.78f : 1f);
                if (up.y < 0.25f) { w.comp = 0; continue; }
                float gh = terrain.Height(anchor.x, anchor.z);
                Vector3 deckN = Vector3.up;
                w.deck = MadMax.Building.StructureGround.Count > 0 && MadMax.Building.StructureGround.Top(anchor, ref gh, out deckN, rb);
                float t = (anchor.y - gh) / up.y;
                if (t > rayLen) { w.comp = 0; continue; }

                w.grounded = true;
                w.comp = Mathf.Min(rayLen - t, travel + 0.15f);
                w.contact = anchor - up * t;
                Vector3 n = w.deck ? deckN : terrain.Normal(w.contact.x, w.contact.z);
                w.surf = terrain.SurfaceAt(w.contact.x, w.contact.z);
                if (w.deck) w.surf = new Surface { road = 1f, wet = w.surf.wet * 0.5f, ice = w.surf.ice };

                float spring = k * w.comp + c * (w.comp - w.prevComp) / dt;
                if (w.comp > travel) spring += (w.comp - travel) * k * 6f;
                w.spring = Mathf.Clamp(spring, 0f, massPerWheel * 40f);
                rb.AddForceAtPosition(up * w.spring, w.contact);

                Vector3 fwd = Quaternion.AngleAxis(w.front ? steer : 0f, up) * transform.forward;
                w.fwd = Vector3.ProjectOnPlane(fwd, n).normalized;
                w.side = Vector3.Cross(n, w.fwd);
                Vector3 v = rb.GetPointVelocity(w.contact);
                w.vf = Vector3.Dot(v, w.fwd); w.vs = Vector3.Dot(v, w.side);

                // dry → wet firm ground (a film of rain on tarmac or hardpan) → mud; each tyre type has its own numbers
                float grip = 0.8f;
                if (w.stats)
                {
                    float wetHard = w.surf.wet * (1f - w.surf.mud);
                    float firm = Mathf.Lerp(w.stats.grip, w.stats.grip * w.stats.wetGrip, wetHard);
                    grip = Mathf.Lerp(firm, w.stats.mudGrip, w.surf.mud) * w.stats.GripFactor * (tuning ? tuning.GripFactor(Mathf.Max(w.surf.mud, w.surf.softness)) : 1f);
                }
                grip *= w.surf.ice > 0f ? Mathf.Lerp(1f, 0.25f, w.surf.ice) : 1f;
                // road hazards dropped by other vehicles: oil takes the grip, caltrops puncture
                if (MadMax.World.RoadHazards.Count > 0 && MadMax.World.RoadHazards.At(w.contact, out var hazard))
                {
                    if (hazard == MadMax.World.RoadHazards.Kind.Oil) grip *= 0.22f;
                    else if (w.stats && !w.stats.Popped && Mathf.Abs(w.vf) > 1.5f && Random.value < dt * 4f) w.stats.Pop();
                }
                if (handbrake && occupied && !w.front) grip *= handbrakeGrip;      // parked vehicles keep full grip
                w.maxF = grip * gripMultiplier * w.spring * (w.broken ? 0.72f : 1f);
            }

            // ---- drive distribution: locked = each wheel up to its own grip; open diff = both wheels of an axle
            // limited by the weaker one (a wheel in the air or on mud steals the torque and just spins)
            float share = driven > 0 ? driveForce / driven : 0f;
            foreach (var w in wheels) if (w.part && w.grounded && IsDriven(w)) w.drive = tractionLimit > 0f ? Mathf.Clamp(share, -w.maxF * tractionLimit, w.maxF * tractionLimit) : share;
            DriveForce = 0f; foreach (var w in wheels) DriveForce += w.drive;
            if (!diffLocked && Mathf.Abs(share) > 0f)
            {
                for (int i = 0; i < wheels.Count; i++)
                for (int j = i + 1; j < wheels.Count; j++)
                {
                    var a = wheels[i]; var b = wheels[j];
                    if (!a.part || !b.part || !IsDriven(a) || !IsDriven(b) || a.left == b.left) continue;
                    if (Mathf.Abs(a.socket.transform.localPosition.z - b.socket.transform.localPosition.z) > 0.3f) continue;
                    float lim = Mathf.Min(a.maxF, b.maxF) * 1.1f + Mathf.Abs(share) * 0.05f;
                    float d = Mathf.Sign(share) * Mathf.Min(Mathf.Abs(share), lim);
                    float spin = Mathf.Clamp01((Mathf.Abs(share) - Mathf.Abs(d)) / Mathf.Abs(share));
                    var weak = a.maxF <= b.maxF ? a : b;
                    if (a.grounded) a.drive = d;
                    if (b.grounded) b.drive = d;
                    weak.diffSpin = spin;
                }
            }

            // ---- pass 2: tyre forces, ruts, wheel spin
            float slipSum = 0, mudSum = 0, squealSum = 0, looseSum = 0, mudTyreSum = 0, rollSum = 0; int grounded = 0;
            foreach (var w in wheels)
            {
                if (!w.part) continue;
                bool isDriven = IsDriven(w);
                if (!w.grounded)
                {
                    if (isDriven) w.spin += driveCmd * 900f * dt * (diffLocked ? 0.2f : 1f) * (Reversing ? -1f : 1f);
                    continue;
                }
                float lat = -w.vs * massPerWheel / dt * 0.5f;
                float lng = w.drive;
                // line lock: throttle + brake at low speed holds the undriven wheels only (burnout)
                bool lineLock = driveCmd > 0.5f && brakeCmd > 0.5f && Mathf.Abs(ForwardSpeed) < 4f && isDriven && driven < wheels.Count;
                float bias = tuning ? tuning.BrakeShare(w.front, frontWheels, wheels.Count) : 1f;
                float brake = (lineLock ? 0f : brakeCmd * brakeForce / wheels.Count * bias) + (handbrake ? (!occupied ? brakeForce : !w.front ? brakeForce * 0.5f : 0f) : 0f);   // parked: every wheel locked
                brake += Mathf.Clamp01(w.part.damage) * 0.35f * w.spring;       // damaged wheel drags
                lng -= Mathf.Clamp(w.vf * massPerWheel / dt, -brake, brake);
                float press = Pressure(w);
                float crr = (0.015f + 0.04f * w.surf.softness * press + w.surf.rut * 0.22f) * (w.stats ? w.stats.rolling : 1f) * (tuning ? tuning.RollingFactor : 1f);   // mud and ruts drag
                lng -= Mathf.Clamp(w.vf * 4f, -1f, 1f) * crr * w.spring;

                float mag = Mathf.Sqrt(lng * lng + lat * lat), slip = 0f;
                if (mag > w.maxF && mag > 0f) { float sc = w.maxF / mag; slip = 1f - sc; lng *= sc; lat *= sc; }
                w.broken = slip > (w.broken ? 0.01f : 0.04f);
                rb.AddForceAtPosition(w.fwd * lng + w.side * lat, w.contact);

                float spinSlip = isDriven ? Mathf.Max((w.broken ? Mathf.Clamp01(slip * 5f) : slip) * driveCmd, w.diffSpin * driveCmd) : 0f;   // broken-loose tyres spin up
                if ((Mathf.Abs(w.vf) > 0.3f || spinSlip > 0.05f) && !w.deck)
                    terrain.Deform(w.contact, w.fwd, w.side, w.width, w.spring * press, spinSlip * Mathf.Min(1f, press), dt);

                w.spin += (w.vf / w.radius + spinSlip * 30f * (Reversing ? -1f : 1f)) * dt * Mathf.Rad2Deg;
                slipSum += Mathf.Max(spinSlip, Mathf.Clamp01(Mathf.Abs(w.vs) / 8f));
                TyreWear(w, spinSlip, slip, brake, dt);
                mudSum += w.surf.mud;
                {
                    float sliding = Mathf.Max(Mathf.Clamp01(slip * 3f), Mathf.Clamp01(spinSlip * 2f), Mathf.Clamp01((Mathf.Abs(w.vs) - 1.5f) / 4f));
                    float hard = w.surf.road * (1f - w.surf.mud) * (1f - w.surf.softness * 0.5f);
                    float sp = Mathf.Sqrt(w.vf * w.vf + w.vs * w.vs);
                    squealSum += sliding * hard * Mathf.Clamp01((sp + spinSlip * 10f) / 3f);
                    looseSum += (1f - hard) * (1f - w.surf.mud) * (sp + spinSlip * 6f) * (0.4f + sliding);
                    mudTyreSum += w.surf.mud * (sp + spinSlip * 8f);
                    rollSum += hard * Mathf.Abs(w.vf);
                }
                grounded++;
            }

            // anti-roll per axle
            for (int i = 0; i < wheels.Count; i++)
            for (int j = i + 1; j < wheels.Count; j++)
            {
                var a = wheels[i]; var b = wheels[j];
                if (a.left == b.left || !a.grounded || !b.grounded) continue;
                if (Mathf.Abs(a.socket.transform.localPosition.z - b.socket.transform.localPosition.z) > 0.3f) continue;   // same axle only
                float f = (a.comp - b.comp) * k * antiRoll;
                rb.AddForceAtPosition(up * f, a.contact);
                rb.AddForceAtPosition(-up * f, b.contact);
            }

            Buoyancy(terrain);
            rb.AddForce(-vel * vel.magnitude * drag);
            WheelSlip = grounded > 0 ? slipSum / grounded : 0f;
            Mud = grounded > 0 ? mudSum / grounded : 0f;
            float gi = grounded > 0 ? 1f / grounded : 0f;
            TyreSqueal = Mathf.Clamp01(squealSum * gi * 1.5f); TyreLoose = looseSum * gi; TyreMud = mudTyreSum * gi; TyreRoll = rollSum * gi;
        }

        /// <summary>Ground pressure relative to a 0.32 m car tyre: wide tyres and long crawler tracks sink less.</summary>
        static float Pressure(Wheel w) => Mathf.Clamp(0.32f / Mathf.Max(0.12f, w.width), 0.45f, 1.8f) / (w.stats ? Mathf.Max(1f, w.stats.footprint) : 1f);

        /// <summary>Tread wear, heat build-up, blowouts, skidmarks and burnout smoke.</summary>
        void TyreWear(Wheel w, float spinSlip, float slip, float brake, float dt)
        {
            var st = w.stats;
            float lateral = Mathf.Clamp01((Mathf.Abs(w.vs) - 1.5f) / 6f);
            float locked = brake > 0f && slip > 0.2f ? Mathf.Clamp01(Mathf.Abs(w.vf) / 6f) * slip : 0f;
            float scrub = Mathf.Max(spinSlip, Mathf.Max(lateral, locked));
            float hard = Mathf.Clamp01(1f - w.surf.softness * 1.4f + w.surf.road);
            if (st)
            {
                float abrasion = Mathf.Lerp(0.35f, 1f, hard) * (1f - w.surf.wet * 0.5f);
                if (!st.Popped) st.wear = Mathf.Min(0.97f, st.wear + (scrub * abrasion * dt * 0.0012f + Mathf.Abs(w.vf) * dt * 0.0000015f) * st.wearRate * (tuning ? tuning.TyreWearFactor : 1f));
                st.heat = Mathf.Max(0f, st.heat + (scrub * hard * 0.06f - 0.03f) * dt);
                if (!st.Popped && st.heat > 1f && st.wear > 0.55f) st.Pop();
                if (!st.Popped && w.part.damage > 0.85f) st.Pop();
                if (st.Popped && Mathf.Abs(w.vf) > 4f && Random.value < dt * 6f) MadMax.World.Fx.Sparks(w.contact, -w.fwd * 0.3f + Vector3.up, 1, new Color(1f, 0.8f, 0.4f));
            }
            int key = w.GetHashCode();
            float mark = scrub * hard;
            MadMax.World.Fx.Skid(key, w.contact, w.side, w.width, mark > 0.15f ? mark : 0f);
            // tracks in snow, sand and mud (fade in a minute and a half); dust clouds behind on dry loose ground
            float speed = Mathf.Abs(w.vf);
            if (speed > 0.5f && !w.deck) MadMax.World.Fx.Track(key + 1, w.contact, w.side, w.width * 0.9f);
            float dry = (1f - w.surf.wet) * (1f - w.surf.road) * Mathf.Clamp01(w.surf.softness * 1.5f + 0.3f) * (MadMax.World.Weather.Raining ? 0.2f : 1f);
            if (speed > 6f && dry > 0.35f && Random.value < dt * speed * 0.35f * dry)
            {
                var biome = MadMax.World.DeformableTerrain.Instance ? MadMax.World.DeformableTerrain.Instance.BiomeAt(w.contact.x, w.contact.z) : MadMax.World.Biome.Desert;
                var dust = biome == MadMax.World.Biome.Desert ? new Color(0.78f, 0.55f, 0.36f, 0.55f) : biome == MadMax.World.Biome.Nuclear ? new Color(0.55f, 0.58f, 0.46f, 0.5f)
                         : biome == MadMax.World.Biome.Forest || biome == MadMax.World.Biome.Tropical ? new Color(0.5f, 0.4f, 0.3f, 0.4f) : new Color(0.68f, 0.52f, 0.38f, 0.5f);
                MadMax.World.Fx.Smoke(w.contact + Vector3.up * 0.3f, -w.fwd * Mathf.Sign(w.vf) * speed * 0.15f + Vector3.up * 0.8f + Random.insideUnitSphere * 0.5f, 0.9f + speed * 0.03f, dust, 2.2f);
            }
            if (scrub > 0.25f && Random.value < scrub * dt * 30f)
            {
                var soil = new Color(0.62f, 0.45f, 0.3f, 0.6f);
                var rubber = new Color(0.86f, 0.86f, 0.84f, 0.7f);
                var c = Color.Lerp(soil, rubber, hard * (1f - w.surf.wet));
                if (w.surf.wet > 0.6f && hard > 0.5f) c = new Color(0.8f, 0.82f, 0.85f, 0.35f);   // spray
                float heatBoost = st ? 1f + st.heat : 1f;
                MadMax.World.Fx.Smoke(w.contact + Vector3.up * 0.2f, -w.fwd * Mathf.Sign(w.vf + 0.01f) * 1.2f + Vector3.up * 0.6f + Random.insideUnitSphere * 0.4f, (0.5f + scrub) * heatBoost, c);
            }
        }

        /// <summary>0..1 how deep the vehicle sits in water.</summary>
        public float InWater { get; private set; }

        void Buoyancy(DeformableTerrain terrain)
        {
            float sub = 0f; int n = 0;
            foreach (var w in wheels)
            {
                if (!w.socket) continue;
                n++;
                var p = w.socket.transform.position;
                float lvl = terrain.WaterLevel(p.x, p.z);
                if (float.IsNaN(lvl)) continue;
                float depth = lvl - (p.y - w.radius);
                if (depth <= 0f) continue;
                float frac = Mathf.Clamp01(depth / 1.5f);
                rb.AddForceAtPosition(Vector3.up * frac * rb.mass * 9.81f * 0.85f / wheels.Count, p);
                var v = rb.GetPointVelocity(p);
                rb.AddForceAtPosition(-v * frac * rb.mass * 0.7f / wheels.Count, p);
                sub += frac;
                if (v.magnitude > 1.5f && Random.value < frac * v.magnitude * 0.08f)
                    MadMax.World.Fx.Smoke(new Vector3(p.x, lvl + 0.05f, p.z), Vector3.up * Random.Range(1.5f, 3f) + v * 0.2f + Random.insideUnitSphere, Random.Range(0.3f, 0.7f), new Color(0.8f, 0.88f, 0.92f, 0.7f), 0.7f);
            }
            InWater = n > 0 ? sub / n : 0f;
        }

        float replicaRuts;

        /// <summary>Remote (interpolated) vehicles: spin wheels from the replicated velocity and press cosmetic ruts.</summary>
        void ReplicaVisuals()
        {
            var rep = GetComponent<MadMax.Net.NetReplica>();
            if (!rep || !rep.active) return;
            steerInput = rep.Steer; throttleInput = rep.Throttle;
            steer = Mathf.MoveTowards(steer, rep.Steer * maxSteer, maxSteer * steerSpeed * Time.deltaTime);
            var vel = rep.Velocity;
            ForwardSpeed = Vector3.Dot(vel, transform.forward);
            var terrain = DeformableTerrain.Instance;
            bool stamp = (replicaRuts += Time.deltaTime) > 0.1f && vel.sqrMagnitude > 0.25f;
            if (stamp) replicaRuts = 0f;
            foreach (var w in wheels)
            {
                if (!w.part) continue;
                w.spin += ForwardSpeed / w.radius * Time.deltaTime * Mathf.Rad2Deg;
                w.grounded = true; w.comp = restComp;
                if (stamp && terrain)
                {
                    var p = w.socket.transform.position;
                    p.y = terrain.Height(p.x, p.z);
                    terrain.Deform(p, transform.forward, transform.right, w.width, massPerWheel * 9.81f, 0f, 0.1f);
                }
            }
        }

        void LateUpdate()
        {
            ReplicaVisuals();
            foreach (var w in wheels)
            {
                if (!w.part || w.part.Socket != w.socket) continue;
                var t = w.part.transform;
                t.localPosition = new Vector3(0f, (w.grounded ? w.comp : 0f) - restComp + rideHeight, 0f);
                float wobble = Mathf.Clamp01(w.part.damage) * 9f * Mathf.Sin(w.spin * Mathf.Deg2Rad);
                t.localRotation = Quaternion.Euler(0f, (w.front ? steer : 0f) * (w.left ? -1f : 1f), wobble) * Quaternion.Euler(w.spin % 360f, 0f, 0f);
            }
        }

        public void ToggleFourWheelDrive()
        {
            if (!awdSelectable) return;
            drive = drive == Drive.All ? twoWheelDrive : Drive.All;
        }

        public void ToggleDiffLock() { if (hasDiffLock) diffLocked = !diffLocked; }

        public void ShiftUp()
        {
            if (!manual) return;
            Gear = Mathf.Min(Gear + 1, gears.Length);
            if (Gear == 0) Rpm = Mathf.Max(Rpm, 900f);
        }

        public void ShiftDown()
        {
            if (!manual) return;
            Gear = Mathf.Max(Gear - 1, -1);
        }

        /// <summary>Switch gearbox mode keeping a sensible gear.</summary>
        public void SetManual(bool on)
        {
            if (manual == on) return;
            manual = on;
            if (on) Gear = Reversing ? -1 : Mathf.Max(1, Gear);
            else { Reversing = Gear < 0; Gear = Mathf.Max(1, Gear); }
        }

        /// <summary>Put the vehicle back on its wheels.</summary>
        public void Recover()
        {
            var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            var p = transform.position;
            var terrain = DeformableTerrain.Instance;
            if (terrain) p.y = terrain.Height(p.x, p.z) + 1.2f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = p;
            rb.rotation = Quaternion.LookRotation(fwd);
            transform.SetPositionAndRotation(p, rb.rotation);
        }
    }
}
