using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MadMax.Building;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>A moving base the player builds (roadmap: "if you see it, you can build it"): a frame piece laid on the
    /// ground becomes a vehicle root standing on axle stands; frame extensions bolt on in any direction (any scale);
    /// axle / track mounts, an engine bay and a driver's seat are build pieces whose sockets take the ordinary parts
    /// (wheels, tracks, engines: E with the wrench). With two filled mounts, an engine and a seat it is commissioned: off
    /// the stands onto its wheels, a <see cref="VehicleDriver"/> like any other. Everything built on it rides along and
    /// weighs it down (<see cref="FurnitureLibrary.MassOf"/>, centre of mass from the pieces); its decks carry other
    /// vehicles (<see cref="StructureGround.AddMovingDeck"/>). Gearing, brakes, steering and springs follow its mass,
    /// running gear and suspension unit. Saved as a layout of its rig pieces (<c>VehicleSave.rig</c>).</summary>
    [DisallowMultipleComponent]
    public class RigVehicle : MonoBehaviour
    {
        public const string Design = "rig";
        public static readonly List<RigVehicle> All = new List<RigVehicle>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { All.Clear(); standMeshes.Clear(); }

        /// <summary>Off the stands and on its wheels.</summary>
        public bool Commissioned { get; private set; }
        /// <summary>What still stops it being driven (null = ready).</summary>
        public string Missing { get; private set; } = "BUILD THE RUNNING GEAR";
        /// <summary>Being taken apart as a whole: pieces don't drop their parts.</summary>
        public bool Dissolving { get; private set; }
        /// <summary>Mass of the frame and everything built on it (kg, parts excluded).</summary>
        public float PieceMass { get; private set; }
        public int Frames { get; private set; }
        public Transform SocketRoot { get; private set; }

        VehicleDriver driver;
        VehicleChassis chassis;
        VehicleSystems systems;
        Rigidbody rb;
        Transform body, stands, eye;
        Material material;
        bool dirty = true, refreshing, judge = true;
        string frameKey, trackKey;
        readonly List<RigPiece> frames = new List<RigPiece>();
        readonly List<RigPiece> pieces = new List<RigPiece>();

        public VehicleDriver Driver => driver;

        // ------------------------------------------------------------------ making one
        /// <summary>A new rig from its first frame piece, standing on axle stands at <paramref name="pos"/> (the frame's
        /// bottom); registered by the caller.</summary>
        public static RigVehicle Create(string frameId, Vector3 pos, Quaternion rot, Material mat, string owner, out Placeable frame)
        {
            var rig = Make(pos, rot, mat);
            frame = FurnitureLibrary.Spawn(frameId, rig.transform, Vector3.zero, Quaternion.identity, mat);
            if (frame) { frame.owner = owner; _ = frame.Id; }
            rig.Refresh();
            return rig;
        }

        static RigVehicle Make(Vector3 pos, Quaternion rot, Material mat)
        {
            var go = new GameObject("Moving Base");
            go.SetActive(false);                                                                 // components wake together, Body in place
            go.transform.SetPositionAndRotation(pos, rot);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.mass = 1000f; rb.angularDamping = 0.3f;
            var ch = go.AddComponent<VehicleChassis>();
            ch.vehicleName = Design; ch.bodyMass = 500f;
            var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(go.transform, false);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var eye = new GameObject("DriverEye").transform;
            eye.SetParent(go.transform, false);
            eye.localPosition = new Vector3(0f, 1.6f, 0f);
            var sockets = new GameObject("Sockets").transform;
            sockets.SetParent(go.transform, false);
            var stands = new GameObject("Stands").transform;
            stands.SetParent(go.transform, false);
            var rig = go.AddComponent<RigVehicle>();
            rig.material = mat; rig.SocketRoot = sockets; rig.body = body.transform; rig.eye = eye; rig.stands = stands;
            var drv = go.AddComponent<VehicleDriver>();
            drv.driveable = false;                                                               // no passenger seat / car storage: a base furnishes itself
            drv.drive = VehicleDriver.Drive.All; drv.awdSelectable = true; drv.hasDiffLock = true;
            drv.customCom = true; drv.centerOfMass = new Vector3(0f, 0.3f, 0f);
            var sys = go.AddComponent<VehicleSystems>();
            sys.fuelCapacity = 60f; sys.oilCapacity = 8f; sys.coolantCapacity = 12f;
            sys.fuel = 0f; sys.oil = 7f; sys.coolant = 11f;
            go.AddComponent<VehicleDamage>();
            go.SetActive(true);
            drv.driveable = true;
            if (!go.GetComponent<VehicleTuning>()) go.AddComponent<VehicleTuning>();
            if (!go.GetComponent<VehicleIgnition>()) go.AddComponent<VehicleIgnition>();
            return rig;
        }

        void Awake()
        {
            driver = GetComponent<VehicleDriver>();
            chassis = GetComponent<VehicleChassis>();
            rb = GetComponent<Rigidbody>();
        }

        void OnEnable()
        {
            All.Add(this);
            Placeable.Changed += MarkDirty;
            if (chassis) chassis.Changed += MarkDirty;
        }

        void OnDisable()
        {
            All.Remove(this);
            Placeable.Changed -= MarkDirty;
            if (chassis) chassis.Changed -= MarkDirty;
        }

        public void MarkDirty() { if (!refreshing) dirty = true; }

        void LateUpdate()
        {
            if (dirty) Refresh();
            if (!Commissioned && stands && (Time.frameCount & 31) == 0) FitStands();
        }

        // ------------------------------------------------------------------ the frame
        /// <summary>Outer edges of the frame at vehicle z (m): where the wheels go.</summary>
        public void SidesAt(float z, out float left, out float right)
        {
            left = float.MaxValue; right = float.MinValue;
            RigPiece near = null; float nd = float.MaxValue;
            foreach (var f in frames)
            {
                if (!f) continue;
                var p = f.transform.localPosition;
                float dz = Mathf.Abs(z - p.z) - f.half.y;
                if (dz <= 0.05f) { left = Mathf.Min(left, p.x - f.half.x); right = Mathf.Max(right, p.x + f.half.x); }
                else if (dz < nd) { nd = dz; near = f; }
            }
            if (right < left && near) { var p = near.transform.localPosition; left = p.x - near.half.x; right = p.x + near.half.x; }
            if (right < left) { left = -1f; right = 1f; }
        }

        /// <summary>The frame piece nearest a vehicle-space point (extensions snap to it).</summary>
        public RigPiece FrameAt(Vector3 local)
        {
            RigPiece best = null; float bd = float.MaxValue;
            foreach (var f in frames)
            {
                if (!f) continue;
                var p = f.transform.localPosition;
                float d = Mathf.Max(0f, Mathf.Abs(local.x - p.x) - f.half.x) + Mathf.Max(0f, Mathf.Abs(local.z - p.z) - f.half.y);
                if (d < bd) { bd = d; best = f; }
            }
            return best;
        }

        /// <summary>True when a rectangle (vehicle space centre, half extents) overlaps a frame section already there.</summary>
        public bool FrameOverlaps(Vector3 centre, Vector2 half)
        {
            foreach (var f in frames)
            {
                if (!f) continue;
                var p = f.transform.localPosition;
                if (Mathf.Abs(centre.x - p.x) < half.x + f.half.x - 0.05f && Mathf.Abs(centre.z - p.z) < half.y + f.half.y - 0.05f) return true;
            }
            return false;
        }

        /// <summary>A mount already sits within <paramref name="gap"/> m of vehicle z.</summary>
        public bool MountNear(float z, float gap)
        {
            foreach (var p in pieces) if (p && p.IsMount && Mathf.Abs(p.transform.localPosition.z - z) < gap) return true;
            return false;
        }

        // ------------------------------------------------------------------ refresh
        public void Refresh()
        {
            if (refreshing) return;
            refreshing = true; dirty = false;
            try { RefreshNow(); }
            finally { refreshing = false; }
        }

        void RefreshNow()
        {
            frames.Clear(); pieces.Clear();
            foreach (var p in GetComponentsInChildren<RigPiece>())
            {
                if (!p || (p.TryGetComponent<Placeable>(out var pl) && pl.Collapsing)) continue;
                pieces.Add(p);
                if (p.kind == RigPart.Frame) frames.Add(p);
            }
            Frames = frames.Count;
            if (frames.Count == 0) { if (!Dissolving) Dissolve(); return; }
            BuildBody();
            foreach (var p in pieces) p.Bind(this);

            // driver's seat: the newest one drives
            RigPiece seat = null;
            foreach (var p in pieces) if (p.kind == RigPart.Seat) seat = p;
            if (seat) eye.localPosition = transform.InverseTransformPoint(seat.transform.TransformPoint(seat.eye));
            var dash = transform.Find("Dashboard");
            if (dash && TryGetComponent<VehicleDashboard>(out var vd)) dash.localPosition = eye.localPosition + vd.offsetFromEye;

            Weigh();
            Tune();
            var tracks = GetComponent<CrawlerTracks>();
            chassis.NotifyChanged();                                                             // springs, mass, wheels
            string tk = TrackKey();
            if (tracks && tk != trackKey) tracks.Rebuild();
            trackKey = tk;

            int filled = 0; bool engine = false;
            foreach (var p in pieces)
            {
                if (p.IsMount && p.Filled) filled++;
                if (p.kind == RigPart.EngineBay) foreach (var s in p.Sockets) if (s && s.Current && s.Current.GetComponent<EngineStats>()) engine = true;
            }
            string was = Missing;
            Missing = filled < 2 ? "FIT WHEELS OR TRACKS ON TWO AXLE MOUNTS" : !engine ? "FIT AN ENGINE IN THE ENGINE BAY" : !seat ? "BUILD A DRIVER'S SEAT" : null;
            var g = MadMax.Game.WastelandGame.Instance;
            if (judge && Missing != null && Missing != was && g && g.Player && Time.timeSinceLevelLoad > 5f && (g.Player.transform.position - transform.position).sqrMagnitude < 900f)
                g.Toast("MOVING BASE: " + Missing);                                              // the next step, as the build goes on
            if (!judge)
            {
                judge = true; ShowStands(!Commissioned);                                          // restored: as it was saved
                if (Commissioned && !GetComponent<VehicleDashboard>()) gameObject.AddComponent<VehicleDashboard>();
                return;
            }
            if (Missing == null && !Commissioned) Commission();
            else if (Missing != null && Commissioned && !driver.Occupied && rb.linearVelocity.sqrMagnitude < 0.25f) PutOnStands();
            else if (!Commissioned) FitStands();
        }

        string TrackKey()
        {
            var sb = new StringBuilder();
            foreach (var s in chassis.Sockets) if (s.accepts == PartCategory.Wheel && s.Current) sb.Append(s.name).Append(s.Current.partId);
            return sb.ToString();
        }

        /// <summary>The frame sections drawn as one mesh on Body (paint, grime, dents and bounds see the whole frame);
        /// the pieces keep their colliders for building and dismantling.</summary>
        void BuildBody()
        {
            var sb = new StringBuilder();
            foreach (var f in frames)
            {
                var fm = f.GetComponent<MeshFilter>();
                sb.Append(fm && fm.sharedMesh ? fm.sharedMesh.name : "").Append(f.transform.localPosition).Append(f.transform.localRotation.eulerAngles.y.ToString("0")).Append(';');   // mesh: a dyed frame redraws
            }
            string key = sb.ToString();
            var mf = body.GetComponent<MeshFilter>();
            if (key == frameKey && mf.sharedMesh) return;
            frameKey = key;
            var list = new List<CombineInstance>();
            foreach (var f in frames)
            {
                var fm = f.GetComponent<MeshFilter>();
                if (!fm || !fm.sharedMesh) continue;
                list.Add(new CombineInstance { mesh = fm.sharedMesh, transform = Matrix4x4.TRS(f.transform.localPosition, f.transform.localRotation, Vector3.one) });
                if (f.TryGetComponent<MeshRenderer>(out var r)) r.enabled = false;
            }
            var old = mf.sharedMesh;
            var mesh = new Mesh { name = "RigBody", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(list.ToArray(), true, true);
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            if (old) Destroy(old);
            if (body.TryGetComponent<DeformableMesh>(out var dm)) Destroy(dm);                    // dents were on the old frame
        }

        /// <summary>Body mass from the pieces (rig pieces carry their own, furniture is estimated from its voxels) and the
        /// centre of mass from where everything sits, mounted parts included.</summary>
        void Weigh()
        {
            float m = 0f; Vector3 moment = Vector3.zero;
            foreach (var pl in GetComponentsInChildren<Placeable>())
            {
                if (!pl || pl.Collapsing) continue;
                var rp = pl.GetComponent<RigPiece>();
                float km = rp ? rp.mass : FurnitureLibrary.MassOf(FurnitureLibrary.Get(pl.id));
                var r = pl.GetComponent<Renderer>();
                var c = transform.InverseTransformPoint(r ? r.bounds.center : pl.transform.position);
                if (rp && rp.kind == RigPart.Frame) c = pl.transform.localPosition + Vector3.up * rp.top * 0.4f;
                m += km; moment += c * km;
            }
            PieceMass = m;
            chassis.bodyMass = Mathf.Max(100f, m);
            float total = m;
            foreach (var s in chassis.Sockets)
            {
                if (!s.Current) continue;
                moment += transform.InverseTransformPoint(s.Current.transform.position) * s.Current.mass;
                total += s.Current.mass;
            }
            driver.customCom = true;
            driver.centerOfMass = total > 1f ? moment / total : Vector3.up * 0.3f;
        }

        /// <summary>Handling from what it is: suspension unit (or a stiff default), travel for big wheels, gearing for
        /// the mass and engine, brakes, steering lock for the wheelbase, fuel tanks. An improvised platform is no truck:
        /// its top speed falls with tonnes (3 t ≈ 65 km/h, a 30 t crawler ≈ 17 km/h; tracks × 0.85) and the final drive
        /// puts it on the limiter there; the drivetrain (welded shafts, scrap gearbox) passes on only a share of the
        /// engine's power (≈ 9.5 W/kg at 3 t, less per kg as it grows, more for a strong engine), so a truck diesel
        /// on a 3 t frame pulls to 50 km/h in about 9 s; first gear still pulls a third of its weight.</summary>
        void Tune()
        {
            RigPiece susp = null;
            float tanks = 0f;
            foreach (var p in pieces)
            {
                if (p.kind == RigPart.Suspension && (!susp || p.rank > susp.rank)) susp = p;
                if (p.kind == RigPart.Tank) tanks += p.litres;
            }
            float rMax = 0f, rSum = 0f; int nw = 0; float zMin = float.MaxValue, zMax = float.MinValue; bool tracked = false;
            foreach (var s in chassis.Sockets)
            {
                if (s.accepts != PartCategory.Wheel) continue;
                float z = s.transform.localPosition.z;
                zMin = Mathf.Min(zMin, z); zMax = Mathf.Max(zMax, z);
                if (!s.Current) continue;
                rMax = Mathf.Max(rMax, s.Current.radius); rSum += s.Current.radius; nw++;
                if (s.Current.partId != null && s.Current.partId.StartsWith("wheel_track")) tracked = true;
            }
            driver.steerAxleZ = zMax > zMin ? (zMin + zMax) * 0.5f : 0f;
            float wheelbase = zMax > zMin ? zMax - zMin : 2f;
            float big = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.3f, rMax) / 0.45f), 1f, 2.2f);
            driver.travel = (susp ? susp.travel : 0.2f) * big;
            driver.frequency = susp ? susp.frequency : 2.0f;
            driver.damping = susp ? susp.damping : 0.6f;
            driver.antiRoll = susp ? susp.antiRoll : 0.7f;

            float m = Mathf.Max(200f, chassis.TotalMass), r = nw > 0 ? rSum / nw : 0.45f;
            driver.gears = new[] { 4.8f, 3.1f, 2.05f, 1.42f, 1.0f, 0.82f };
            driver.reverseRatio = 4.8f;
            var eng = driver.Engine;
            if (eng)
            {
                float torque = Mathf.Max(20f, eng.maxTorque);
                float power = torque * eng.peakAt * eng.maxRpm * 2f * Mathf.PI / 60f;           // W at the torque peak
                float heavy = 3000f / m;
                float vtop = 18f * Mathf.Pow(heavy, 0.35f) * Mathf.Clamp(Mathf.Pow(power / m / 118f, 0.15f), 0.6f, 1.1f);
                if (tracked) vtop *= 0.85f;
                vtop = Mathf.Clamp(vtop, 3.5f, 20f);                                            // m/s
                float wheelPower = 9.5f * Mathf.Pow(heavy, 0.3f) * m * Mathf.Clamp(Mathf.Pow(power / 354000f, 0.3f), 0.4f, 1.2f);
                driver.efficiency = 0.85f * Mathf.Clamp(wheelPower / (0.85f * power), 0.02f, 1f);
                float top = driver.gears[driver.gears.Length - 1];
                float fromTop = eng.maxRpm * 2f * Mathf.PI * r / (60f * vtop * top);
                float fromPull = 0.3f * m * 9.81f * r / (torque * driver.efficiency * driver.gears[0]);
                driver.finalDrive = Mathf.Clamp(Mathf.Max(fromTop, fromPull), 2.5f, 250f);
            }
            driver.brakeForce = m * 9.81f * Mathf.Lerp(0.7f, 0.4f, Mathf.InverseLerp(3000f, 30000f, m));   // truck drums → overloaded
            driver.maxSteer = Mathf.Lerp(34f, 18f, Mathf.InverseLerp(3f, 18f, wheelbase));
            driver.yawAssist = Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(4000f, 30000f, m));
            if (!systems) systems = GetComponent<VehicleSystems>();
            if (systems)
            {
                systems.fuelCapacity = 60f + tanks;
                systems.fuel = Mathf.Min(systems.fuel, systems.fuelCapacity);
            }
            if (TryGetComponent<VehicleTuning>(out var tuning)) tuning.Rebase();                 // tuning offsets ride on the new stock values
        }

        // ------------------------------------------------------------------ stands and wheels
        /// <summary>Off the stands onto the wheels: set down at its rest height over the ground under the wheels.</summary>
        public void Commission()
        {
            Commissioned = true;
            ShowStands(false);
            var t = DeformableTerrain.Instance;
            if (t && t.World != null)
            {
                float ground = float.MinValue;
                foreach (var s in chassis.Sockets)
                {
                    if (s.accepts != PartCategory.Wheel || !s.Current) continue;
                    var w = s.transform.position;
                    ground = Mathf.Max(ground, t.Height(w.x, w.z));
                }
                if (ground > float.MinValue)
                {
                    var p = transform.position;
                    p.y = ground + driver.RestHeight + 0.03f;
                    transform.position = p; rb.position = p;
                }
            }
            rb.isKinematic = !MadMax.Net.NetSession.SimulatesHere(driver);                       // a remote copy follows its snapshots
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
            driver.handbrake = true;
            if (!GetComponent<VehicleDashboard>()) gameObject.AddComponent<VehicleDashboard>();
            var g = MadMax.Game.WastelandGame.Instance;
            if (g) g.Toast("MOVING BASE READY: [" + MadMax.Game.Controls.Name(MadMax.Game.Controls.Act.Enter) + "] AT THE SEAT TO DRIVE");
            MadMax.Audio.Sfx.Play("hit_metal", transform.position, 0.6f, 0.6f, 40f, 0.5f);
        }

        /// <summary>Back on axle stands (the running gear came off while parked): held still for building.</summary>
        public void PutOnStands()
        {
            Commissioned = false;
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            ShowStands(true);
        }

        void ShowStands(bool on)
        {
            stands.gameObject.SetActive(on);
            if (on) FitStands();
        }

        static readonly Dictionary<int, Mesh> standMeshes = new Dictionary<int, Mesh>();
        readonly List<Transform> standList = new List<Transform>();

        /// <summary>One screw stand under each frame corner, reaching down to the ground.</summary>
        void FitStands()
        {
            var t = DeformableTerrain.Instance;
            if (!t || t.World == null || !stands.gameObject.activeSelf) return;
            int k = 0;
            foreach (var f in frames)
            {
                if (!f) continue;
                var c = f.transform.localPosition;
                foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                {
                    var local = new Vector3(c.x + sx * (f.half.x - 0.35f), c.y, c.z + sz * (f.half.y - 0.45f));
                    var w = transform.TransformPoint(local);
                    float h = w.y - t.Height(w.x, w.z);
                    int n = Mathf.Clamp(Mathf.RoundToInt(h / VoxelMesher.DefaultSize), 1, 40);
                    if (k >= standList.Count)
                    {
                        var go = new GameObject("Stand", typeof(MeshFilter), typeof(MeshRenderer));
                        go.transform.SetParent(stands, false);
                        go.GetComponent<MeshRenderer>().sharedMaterial = material;
                        standList.Add(go.transform);
                    }
                    var st = standList[k++];
                    st.gameObject.SetActive(true);
                    st.localPosition = local;
                    st.GetComponent<MeshFilter>().sharedMesh = StandMesh(n);
                }
            }
            for (; k < standList.Count; k++) standList[k].gameObject.SetActive(false);
        }

        /// <summary>Axle stand <paramref name="n"/> voxels tall (origin at the saddle): tripod feet, a ratchet column, a U saddle.</summary>
        static Mesh StandMesh(int n)
        {
            if (standMeshes.TryGetValue(n, out var m) && m) return m;
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Iron);
            var paint = Pal.Weathered(Pal.Crimson, 0.35f, 2401, 3, 0f);
            int foot = -n;
            g.Box(-3, foot, -3, 3, foot, 3, Pal.Ramp(Pal.Metal, 1, 2402));
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f + 0.5f;
                g.Tube(new Vector3(Mathf.Cos(a) * 3f, foot, Mathf.Sin(a) * 3f), new Vector3(0f, foot + Mathf.Min(n - 1, 5), 0f), 0.6f, paint);
            }
            g.Box(-1, foot + 1, -1, 1, -2, 1, paint);
            for (int y = foot + 3; y < -2; y += 2) g.Set(1, y, 0, Pal.Solid(Pal.Chrome[1]));       // ratchet teeth
            g.Box(-2, -1, -1, 2, -1, 1, Pal.Ramp(Pal.Metal, 2, 2403));
            g.Box(-2, 0, -1, -2, 0, 1, Pal.Ramp(Pal.Metal, 2, 2404)); g.Box(2, 0, -1, 2, 0, 1, Pal.Ramp(Pal.Metal, 2, 2404));
            g.Bevel();
            return standMeshes[n] = VoxelMesher.Build(g, "RigStand" + n);
        }

        // ------------------------------------------------------------------ taking apart
        void Dissolve()
        {
            Dissolving = true;
            foreach (var s in chassis.Sockets) if (s.Current) s.Detach(true);
            var g = MadMax.Game.WastelandGame.Instance;
            foreach (var pl in GetComponentsInChildren<Placeable>())
                if (pl && g && g.Build) pl.transform.SetParent(g.Build.Structures, true);
            if (g) g.ForgetVehicle(driver);
            Destroy(gameObject);
        }

        /// <summary>Building rules for taking a rig piece down: mounts give their parts back first; a frame section
        /// goes last, or when nothing is built over it.</summary>
        public static bool CanDismantle(Placeable p, out string why)
        {
            why = null;
            var rp = p ? p.GetComponent<RigPiece>() : null;
            var rig = p ? p.GetComponentInParent<RigVehicle>() : null;
            if (!rp || !rig) return true;
            foreach (var s in rp.Sockets) if (s && s.Current) { why = rp.kind == RigPart.EngineBay ? "TAKE THE ENGINE OUT FIRST" : "TAKE THE WHEELS OFF FIRST"; return false; }
            if (rp.kind != RigPart.Frame) return true;
            if (rig.Commissioned && rig.driver.Occupied) { why = "STOP AND GET OUT FIRST"; return false; }
            var c = p.transform.localPosition;
            foreach (var other in rig.GetComponentsInChildren<Placeable>())
            {
                if (other == p) continue;
                var orp = other.GetComponent<RigPiece>();
                if (orp && orp.kind == RigPart.Frame) continue;
                var o = other.transform.localPosition;
                if (Mathf.Abs(o.x - c.x) <= rp.half.x + 0.2f && Mathf.Abs(o.z - c.z) <= rp.half.y + 0.2f) { why = "CLEAR WHAT IS BUILT ON THIS SECTION FIRST"; return false; }
            }
            return true;
        }

        /// <summary>Can be driven from <paramref name="at"/>: ordinary vehicles always; a rig once commissioned, from beside its seat.</summary>
        public static bool Enterable(VehicleDriver v, Vector3 at)
        {
            if (!v || !v.TryGetComponent<RigVehicle>(out var rig)) return true;
            return rig.Commissioned && rig.eye && Vector3.Distance(rig.eye.position, at) < 2.4f;
        }

        /// <summary>Held on its stands (the sleeper leaves it kinematic).</summary>
        public static bool OnStands(VehicleDriver v) => v && v.TryGetComponent<RigVehicle>(out var rig) && !rig.Commissioned;

        // ------------------------------------------------------------------ save
        public static bool IsLayoutPiece(Placeable p) => p && p.GetComponent<RigPiece>() && p.GetComponentInParent<RigVehicle>();

        static string F(float f) => f.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>"c;id|x|y|z|qx|qy|qz|qw|hits|owner|dye|netId;..." — every rig piece (frames, mounts, seat, tanks,
        /// suspension) in vehicle space; c = commissioned. Other pieces on it save as ordinary placed pieces.</summary>
        public static string SaveLayout(VehicleDriver v)
        {
            if (!v || !v.TryGetComponent<RigVehicle>(out var rig)) return null;
            var sb = new StringBuilder(rig.Commissioned ? "1" : "0");
            foreach (var p in rig.GetComponentsInChildren<Placeable>())
            {
                if (!p.GetComponent<RigPiece>() || p.Collapsing) continue;
                var lp = rig.transform.InverseTransformPoint(p.transform.position);
                var lr = Quaternion.Inverse(rig.transform.rotation) * p.transform.rotation;
                sb.Append(';').Append(p.id).Append('|').Append(F(lp.x)).Append('|').Append(F(lp.y)).Append('|').Append(F(lp.z))
                  .Append('|').Append(F(lr.x)).Append('|').Append(F(lr.y)).Append('|').Append(F(lr.z)).Append('|').Append(F(lr.w))
                  .Append('|').Append(p.hits).Append('|').Append((p.owner ?? "").Replace("|", "").Replace(";", "")).Append('|').Append(p.dye)
                  .Append('|').Append(p.Id.ToString(CultureInfo.InvariantCulture));                   // network identity (joiners, PlaceState / PlaceBroken)
            }
            return sb.ToString();
        }

        /// <summary>Rebuild a saved rig (pieces and sockets; the save fills the sockets, fluids and the rest).</summary>
        public static GameObject Restore(string layout, Vector3 pos, Quaternion rot, Material mat)
        {
            if (string.IsNullOrEmpty(layout)) return null;
            var parts = layout.Split(';');
            var rig = Make(pos, rot, mat);
            foreach (var e in parts)
            {
                var f = e.Split('|');
                if (f.Length < 11) continue;
                float P(int i) => float.Parse(f[i], CultureInfo.InvariantCulture);
                var pl = FurnitureLibrary.Spawn(f[0], rig.transform, new Vector3(P(1), P(2), P(3)), new Quaternion(P(4), P(5), P(6), P(7)), mat);
                if (!pl) continue;
                if (int.TryParse(f[8], out int hits)) pl.hits = hits;
                pl.owner = f[9];
                if (int.TryParse(f[10], out int dye) && dye != 0) pl.SetDye(dye);
                if (f.Length > 11 && uint.TryParse(f[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint nid)) pl.netId = nid;
            }
            rig.Commissioned = parts[0] == "1";
            rig.judge = false;                                                                    // parts come next: judge on the next refresh
            rig.Refresh();
            rig.dirty = true;
            return rig.gameObject;
        }
    }
}
