using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MadMax.Building;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A rolling city (roadmap 28): <see cref="CityDesign"/> built on worker threads into one kinematic body that
    /// follows <see cref="CityRoute"/> on a timetable — a dwell at each dock (the gangway lowered onto the pier), then
    /// the leg to the next at walking-pace speed with a gentle start and stop. Its pose comes from the bed under the
    /// front and rear bogies. Riders: the player is carried (<see cref="PointVelocity"/>), parked vehicles are strapped
    /// to the deck while nobody drives them, driven ones grip the deck through <see cref="StructureGround"/>.</summary>
    public class RollingCity : MonoBehaviour
    {
        public static RollingCity Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Instance = null; }

        public const float Speed = 3f, Accel = 0.05f, Dwell = 150f, BogieZ = 21f, GangwaySpeed = 18f;

        public CityRoute Route { get; private set; }
        public string Name => Route != null ? Route.name : "";
        /// <summary>Seconds on the timetable (saved); the pose is a function of it.</summary>
        public double Clock;
        public bool Ready { get; private set; }
        /// <summary>Distance along the circuit, the dock it stands at (-1 = travelling) and the seconds left there.</summary>
        public float S { get; private set; }
        public int Dock { get; private set; } = -1;
        public float DwellLeft { get; private set; }
        public float CurrentSpeed { get; private set; }
        public Vector3 Velocity { get; private set; }
        /// <summary>Turn rate about the vertical (degrees per second).</summary>
        public float YawRate { get; private set; }
        public bool GangwayDown => gangAngle < 1f;
        /// <summary>The dock the city heads for (or stands at) and the seconds until it gets there.</summary>
        public int NextDock { get; private set; }
        public float SecondsToNext { get; private set; }
        public float Cycle => cycle;

        Rigidbody rb;
        Transform gangway, gangDeck;
        BoxCollider gangDeckBox;
        readonly List<GameObject[]> treads = new List<GameObject[]>();
        readonly List<VehicleDriver> strapped = new List<VehicleDriver>();
        float gangAngle = 90f, treadDist, scanAt, smokeAt;
        List<Vector3> stacks = new List<Vector3>();
        int treadPhase = -1;
        bool gangRegistered, wasDocked = true;
        Vector3 lastPos; float lastYaw; bool hasLast;
        float[] legTime;
        float cycle;

        // ------------------------------------------------------------------ building
        public static RollingCity Create(CityRoute route, int seed, Material mat)
        {
            var go = new GameObject("RollingCity_" + route.name);
            var city = go.AddComponent<RollingCity>();
            city.Route = route;
            city.rb = go.AddComponent<Rigidbody>();
            city.rb.isKinematic = true;
            city.rb.interpolation = RigidbodyInterpolation.Interpolate;
            city.Timetable();
            Instance = city;
            city.StartCoroutine(city.Build(seed, mat));
            return city;
        }

        IEnumerator Build(int seed, Material mat)
        {
            // voxels and mesh data on workers; meshes on the main thread, colliders baked on workers
            var half = Vector3.one * (CityDesign.V * 0.5f);
            var piecesTask = Task.Run(() =>
            {
                var pieces = CityDesign.City(seed);
                stacks = CityDesign.Stacks(seed);
                var data = new List<VoxelMesher.MeshData>();
                foreach (var p in pieces) data.Add(VoxelMesher.BuildData(p.grid, CityDesign.V));
                return (pieces, data);
            });
            var bogieTask = Task.Run(() => VoxelMesher.BuildData(CityDesign.Bogie(seed), CityDesign.V));
            var treadTasks = new Task<VoxelMesher.MeshData>[4];
            for (int i = 0; i < 4; i++) { int ph = i; treadTasks[i] = Task.Run(() => VoxelMesher.BuildData(CityDesign.Tread(ph), CityDesign.V)); }
            var gangTask = Task.Run(() => VoxelMesher.BuildData(CityDesign.Gangway(seed), CityDesign.V));
            while (!piecesTask.IsCompleted || !bogieTask.IsCompleted || !gangTask.IsCompleted || !System.Array.TrueForAll(treadTasks, t => t.IsCompleted)) yield return null;
            if (piecesTask.IsFaulted) { Debug.LogException(piecesTask.Exception); yield break; }

            var (pieceList, pieceData) = piecesTask.Result;
            var bakes = new List<(MeshCollider col, Mesh mesh, Task task)>();
            for (int i = 0; i < pieceList.Count; i++)
            {
                var p = pieceList[i];
                var mesh = VoxelMesher.ToMesh(pieceData[i], "City_" + p.name);
                var child = Child(p.name, mesh, mat, half);
                if (p.collide != CityDesign.Collide.Mesh) continue;
                var col = child.AddComponent<MeshCollider>();
                child.AddComponent<Cutaway>();
                col.enabled = false;
                var id = mesh.GetEntityId();
                bakes.Add((col, mesh, Task.Run(() => Physics.BakeMesh(id, false))));
            }
            // the deck: one box for the plates (wheels read it as a moving StructureGround deck); skirts and girders are visual
            var deck = transform.Find("Deck");
            var deckBox = deck.gameObject.AddComponent<BoxCollider>();
            deckBox.center = new Vector3(0f, -0.25f, 0f) - half;
            deckBox.size = new Vector3(CityDesign.HX * 2f * CityDesign.V, 0.5f, CityDesign.HZ * 2f * CityDesign.V);
            StructureGround.AddMovingDeck(this, transform, deckBox, new Vector4(CityRoute.HalfWidth, CityRoute.HalfLength, 0f, 0f), PointVelocity);

            var bogieMesh = VoxelMesher.ToMesh(bogieTask.Result, "City_Bogie");
            var treadMeshes = new Mesh[4];
            for (int i = 0; i < 4; i++) treadMeshes[i] = VoxelMesher.ToMesh(treadTasks[i].Result, "City_Tread" + i);
            foreach (var at in CityDesign.Bogies)
            {
                var b = Child("Bogie", bogieMesh, mat, half);
                b.transform.localPosition = at + half;
                var box = b.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, -2.25f, 0f); box.size = new Vector3(7f, 4.4f, 17f);
                var set = new GameObject[4];
                for (int i = 0; i < 4; i++)
                {
                    var t = new GameObject("Tread" + i, typeof(MeshFilter), typeof(MeshRenderer));
                    t.transform.SetParent(b.transform, false);
                    t.GetComponent<MeshFilter>().sharedMesh = treadMeshes[i];
                    t.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    t.SetActive(i == 0);
                    set[i] = t;
                }
                treads.Add(set);
            }

            gangway = new GameObject("Gangway").transform;
            gangway.SetParent(transform, false);
            gangway.localPosition = new Vector3(CityRoute.HalfWidth, 0f, 0f);
            var gm = Child("GangwayMesh", VoxelMesher.ToMesh(gangTask.Result, "City_Gangway"), mat, half);
            gm.transform.SetParent(gangway, false);
            gm.transform.localPosition = half;
            float gl = CityDesign.GangLen * CityDesign.V, gw = CityDesign.GangHalf * CityDesign.V;
            gangDeckBox = gm.AddComponent<BoxCollider>();
            gangDeckBox.center = new Vector3(gl * 0.5f, -0.25f, 0f) - half; gangDeckBox.size = new Vector3(gl, 0.5f, gw * 2f);
            gangDeck = new GameObject("GangwayDeck").transform;
            gangDeck.SetParent(gangway, false);
            gangDeck.localPosition = new Vector3(gl * 0.5f, 0f, 0f);

            foreach (var b in bakes) while (!b.task.IsCompleted) yield return null;
            foreach (var b in bakes) { b.col.sharedMesh = b.mesh; b.col.enabled = true; }
            Ready = true;
            Snap();
        }

        GameObject Child(string name, Mesh mesh, Material mat, Vector3 offset)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ------------------------------------------------------------------ the timetable
        void Timetable()
        {
            int n = Route.docks.Length;
            legTime = new float[n];
            cycle = 0f;
            for (int k = 0; k < n; k++)
            {
                legTime[k] = LegLength(k) / Speed + Speed / Accel;
                cycle += Dwell + legTime[k];
            }
        }

        float LegLength(int k) { int n = Route.docks.Length; return Route.Wrap(Route.docks[(k + 1) % n] - Route.docks[k]); }

        /// <summary>Clock time (within a cycle) at which the city arrives at dock k.</summary>
        public float ArrivalAt(int k)
        {
            float t = 0f;
            for (int i = 0; i < k; i++) t += Dwell + legTime[i];
            return t;
        }

        void Evaluate(double clock)
        {
            float t = (float)(clock % cycle);
            if (t < 0f) t += cycle;
            int n = Route.docks.Length;
            for (int k = 0; k < n; k++)
            {
                if (t < Dwell)
                {
                    S = Route.docks[k]; Dock = k; DwellLeft = Dwell - t; CurrentSpeed = 0f;
                    NextDock = k; SecondsToNext = 0f;
                    return;
                }
                t -= Dwell;
                if (t < legTime[k])
                {
                    float L = LegLength(k), T = legTime[k], ta = Speed / Accel, d;
                    if (t < ta) { d = 0.5f * Accel * t * t; CurrentSpeed = Accel * t; }
                    else if (t < T - ta) { d = 0.5f * Accel * ta * ta + Speed * (t - ta); CurrentSpeed = Speed; }
                    else { float r = T - t; d = L - 0.5f * Accel * r * r; CurrentSpeed = Accel * r; }
                    S = Route.Wrap(Route.docks[k] + d); Dock = -1; DwellLeft = 0f;
                    NextDock = (k + 1) % n; SecondsToNext = T - t;
                    return;
                }
                t -= legTime[k];
            }
        }

        /// <summary>The deck's pose at distance s: centred between the bogies, level along the bed under them.</summary>
        public void Pose(float s, out Vector3 pos, out Quaternion rot)
        {
            var f = Route.At(s + BogieZ, out _);
            var r = Route.At(s - BogieZ, out _);
            var fwd = f - r;
            pos = (f + r) * 0.5f + Vector3.up * CityRoute.DeckHeight;
            rot = Quaternion.LookRotation(fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward, Vector3.up);
        }

        /// <summary>Jump straight to the pose of the current clock (load, start, tests).</summary>
        public void Snap()
        {
            Evaluate(Clock);
            Pose(S, out var p, out var q);
            transform.SetPositionAndRotation(p, q);
            if (rb) { rb.position = p; rb.rotation = q; }
            lastPos = p; lastYaw = q.eulerAngles.y; hasLast = true;
            Velocity = Vector3.zero; YawRate = 0f;
            wasDocked = Dock >= 0;
            gangAngle = GangwayWanted ? 0f : 90f;
            if (gangway) SetGangway(gangAngle);
            Physics.SyncTransforms();
        }

        /// <summary>Velocity of the deck at a world point (translation plus the turn).</summary>
        public Vector3 PointVelocity(Vector3 p)
        {
            var w = new Vector3(0f, YawRate * Mathf.Deg2Rad, 0f);
            return Velocity + Vector3.Cross(w, p - transform.position);
        }

        /// <summary>World point of a deck-local position (metres; y = 0 the deck surface).</summary>
        public Vector3 DeckPoint(Vector3 local) => transform.TransformPoint(local);

        /// <summary>Is this world point over the deck, up to <paramref name="above"/> metres above it?</summary>
        public bool OnDeck(Vector3 p, float above = 3f)
        {
            var l = transform.InverseTransformPoint(p);
            return Mathf.Abs(l.x) < CityRoute.HalfWidth && Mathf.Abs(l.z) < CityRoute.HalfLength && l.y > -1f && l.y < above;
        }

        // ------------------------------------------------------------------ running
        void FixedUpdate()
        {
            if (!Ready) return;
            float dt = Time.fixedDeltaTime;
            Clock += dt;
            Evaluate(Clock);
            Pose(S, out var p, out var q);
            rb.MovePosition(p);
            rb.MoveRotation(q);
            float yaw = q.eulerAngles.y;
            if (hasLast) { Velocity = (p - lastPos) / dt; YawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt; }
            lastPos = p; lastYaw = yaw; hasLast = true;

            float target = GangwayWanted ? 0f : 90f;
            if (!Mathf.Approximately(gangAngle, target)) { gangAngle = Mathf.MoveTowards(gangAngle, target, GangwaySpeed * dt); SetGangway(gangAngle); }

            bool docked = Dock >= 0;
            if (wasDocked != docked) MadMax.Audio.Sfx.Play("horn", DeckPoint(new Vector3(0f, 20f, -28f)), docked ? 0.8f : 1f, docked ? 0.5f : 0.42f, 600f);
            wasDocked = docked;

            ReleaseDriven();
            if (Time.time >= scanAt) { scanAt = Time.time + 0.5f; StrapParked(); }
        }

        /// <summary>The gangway comes down a few seconds after arriving and goes up before leaving.</summary>
        bool GangwayWanted => Dock >= 0 && DwellLeft > 8f && Dwell - DwellLeft > 4f;

        void SetGangway(float angle)
        {
            gangway.localRotation = Quaternion.Euler(0f, 0f, angle);
            bool want = angle < 1f;
            if (want == gangRegistered) return;
            gangRegistered = want;
            if (want) StructureGround.AddMovingDeck(gangway, gangDeck, gangDeckBox, new Vector4(CityDesign.GangLen * CityDesign.V * 0.5f, CityDesign.GangHalf * CityDesign.V, 0f, 0f), PointVelocity);
            else StructureGround.Remove(gangway);
        }

        void Update()
        {
            if (!Ready) return;
            // treads: the grouser phase steps every quarter metre travelled
            treadDist += CurrentSpeed * Time.deltaTime;
            int ph = Mathf.FloorToInt(treadDist / CityDesign.V) & 3;
            if (ph != treadPhase)
            {
                foreach (var set in treads) for (int i = 0; i < 4; i++) set[i].SetActive(i == ph);
                treadPhase = ph;
            }
            // the stacks smoke harder under way; engines and treads are heard from far off
            float load = Mathf.Clamp01(CurrentSpeed / Speed);
            if (Time.time >= smokeAt)
            {
                smokeAt = Time.time + Mathf.Lerp(0.6f, 0.18f, load);
                foreach (var stack in stacks)
                {
                    var top = DeckPoint(stack);
                    Fx.Smoke(top, Vector3.up * (1.2f + load * 1.5f) + Velocity * 0.5f, 0.9f + load * 0.8f, new Color(0.22f, 0.2f, 0.19f, 0.75f), 5f);
                }
            }
            MadMax.Audio.Sfx.Loop(this, "engine_diesel", 0.45f + 0.4f * load, 0.38f + 0.12f * load, 260f);
            MadMax.Audio.Sfx.Loop(rb, "chain", load * 0.7f, 0.45f + 0.15f * load, 160f);
        }

        // ------------------------------------------------------------------ riders
        static readonly Collider[] hits = new Collider[64];

        /// <summary>Parked vehicles on the deck ride along strapped down (kinematic, parented); taken over by a driver they
        /// are let go with the deck's velocity.</summary>
        void StrapParked()
        {
            var centre = DeckPoint(new Vector3(0f, 1.5f, 0f));
            int n = Physics.OverlapBoxNonAlloc(centre, new Vector3(CityRoute.HalfWidth, 2f, CityRoute.HalfLength), hits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var body = hits[i].attachedRigidbody;
                if (!body || body == rb) continue;
                var v = body.GetComponent<VehicleDriver>();
                if (!v || v.Occupied || v.aiDriven || strapped.Contains(v) || body.transform.parent) continue;
                if (!OnDeck(body.position, 3.5f)) continue;
                var rel = body.isKinematic ? Vector3.zero : body.linearVelocity - PointVelocity(body.position);
                if (rel.sqrMagnitude > 1.5f) continue;                                             // still rolling about
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
                body.transform.SetParent(transform, true);
                strapped.Add(v);
            }
        }

        void ReleaseDriven()
        {
            for (int i = strapped.Count - 1; i >= 0; i--)
            {
                var v = strapped[i];
                if (!v) { strapped.RemoveAt(i); continue; }
                if (!v.Occupied && !v.aiDriven && v.transform.parent == transform) continue;
                Release(v);
                strapped.RemoveAt(i);
            }
        }

        void Release(VehicleDriver v)
        {
            if (v.transform.parent == transform) v.transform.SetParent(null, true);
            var body = v.Body;
            if (!body) return;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = PointVelocity(body.position);
        }

        /// <summary>Vehicles riding strapped on the deck right now.</summary>
        public IReadOnlyList<VehicleDriver> Strapped => strapped;

        void OnDestroy()
        {
            StructureGround.Remove(this);
            if (gangway) StructureGround.Remove(gangway);
            if (Instance == this) Instance = null;
        }
    }
}
