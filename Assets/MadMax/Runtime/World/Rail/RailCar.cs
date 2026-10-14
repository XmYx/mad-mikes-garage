using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>One car of a <see cref="Train"/>: a kinematic body posed on the rails each physics step, a moving deck
    /// riders stand on (<see cref="IDeckCarrier"/>) and, on flatcars, a moving <see cref="StructureGround"/> deck
    /// vehicles drive onto (parked ones ride strapped, like on the rolling city). Hittable: armour soaks blows, a car
    /// knocked out stops shooting or pulling. The cab car carries the driver's seat, gun wagons a turret, wagons with a
    /// load a search spot.</summary>
    public class RailCar : MonoBehaviour, IDeckCarrier, IDamageable
    {
        public CarDef def;
        public Train train;
        /// <summary>The model faces back along the train's path.</summary>
        public bool flipped;
        public float hp;
        public int variant;
        public Rigidbody Body { get; private set; }
        public Seat CabSeat { get; private set; }
        public Transform Turret { get; private set; }
        public Transform Lever { get; private set; }
        public Lootable Loot { get; private set; }
        public bool Broken => hp <= 0f;
        public Vector3 Velocity { get; private set; }
        public float YawRate { get; private set; }

        Transform deckTop;
        BoxCollider deckBox;
        Vector3 lastPos; float lastYaw; bool hasLast;
        float scanAt;
        readonly List<VehicleDriver> strapped = new List<VehicleDriver>();
        static readonly Collider[] overlap = new Collider[32];

        static readonly Dictionary<long, Mesh> meshes = new Dictionary<long, Mesh>();
        static readonly Dictionary<long, Mesh> glows = new Dictionary<long, Mesh>();
        static Mesh turretMesh, leverMesh;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { meshes.Clear(); glows.Clear(); turretMesh = leverMesh = null; }

        static long MeshKey(CarKind k, int v) => ((long)k << 16) | (uint)(v & 0xffff);

        /// <summary>Build the body meshes of a kind/variant once (main thread; a few ms).</summary>
        static Mesh BodyMesh(CarKind kind, int variant, out Mesh glow)
        {
            long key = MeshKey(kind, variant);
            if (meshes.TryGetValue(key, out var m) && m) { glows.TryGetValue(key, out glow); return m; }
            var kit = TrainDesigns.Body(kind, variant);
            m = VoxelMesherShim(kit.mesh, "Rail_" + kind + variant);
            glow = kit.glow.verts.Count > 0 ? VoxelMesherShim(kit.glow, "RailGlow_" + kind + variant) : null;
            meshes[key] = m; glows[key] = glow;
            return m;
        }

        static Mesh VoxelMesherShim(MadMax.Voxel.VoxelMesher.MeshData md, string name)
        {
            var mesh = MadMax.Voxel.VoxelMesher.ToMesh(md, name);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static RailCar Create(CarKind kind, int variant, Material mat, Material glowMat)
        {
            var def = TrainDesigns.Get(kind);
            variant &= 3;
            var go = new GameObject("RailCar_" + def.id, typeof(MeshFilter), typeof(MeshRenderer));
            var car = go.AddComponent<RailCar>();
            car.def = def; car.hp = def.hp; car.variant = variant;
            go.GetComponent<MeshFilter>().sharedMesh = BodyMesh(kind, variant, out var glow);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (glow)
            {
                var g = new GameObject("Glow", typeof(MeshFilter), typeof(MeshRenderer));
                g.transform.SetParent(go.transform, false);
                g.GetComponent<MeshFilter>().sharedMesh = glow;
                g.GetComponent<MeshRenderer>().sharedMaterial = glowMat;
            }
            car.Body = go.AddComponent<Rigidbody>();
            car.Body.isKinematic = true;
            car.Body.interpolation = RigidbodyInterpolation.Interpolate;
            car.Body.mass = def.mass;
            foreach (var (c, size) in def.boxes) { var b = go.AddComponent<BoxCollider>(); b.center = c; b.size = size; }
            // corner steps (platform height, then the deck)
            for (int e = -1; e <= 1; e += 2)
                for (int s = -1; s <= 1; s += 2)
                    for (int st = 0; st < 2; st++)
                    {
                        var b = go.AddComponent<BoxCollider>();
                        b.center = new Vector3(s * 1.45f, st == 0 ? 0.3f : 0.62f, e * (def.Half - 0.5f));
                        b.size = new Vector3(0.36f, 0.06f, 0.56f);
                    }
            if (def.deckHalf.x > 0f)
            {
                deckTopFor(car);
            }
            if (def.cab)
            {
                var seatGo = new GameObject("Cab");
                seatGo.transform.SetParent(go.transform, false);
                var seat = seatGo.AddComponent<Seat>();
                seat.spots = new[] { def.seat };
                seat.hidden = true; seat.rest = 1f;
                seat.hasExit = true; seat.exitLocal = new Vector3(def.hand ? 1.4f : 1.9f, def.hand ? 0.0f : 0.05f, def.seat.z);
                car.CabSeat = seat;
                seat.ride = (m, r, j, dt) => { if (car.train) car.train.Ride(m, r, j, dt); };   // whichever train the car is coupled into
            }
            if (def.turret)
            {
                if (!turretMesh) turretMesh = VoxelMesherShim(TrainDesigns.Turret().mesh, "Rail_Turret");
                var t = new GameObject("Turret", typeof(MeshFilter), typeof(MeshRenderer));
                t.transform.SetParent(go.transform, false);
                t.transform.localPosition = def.turretAt;
                t.GetComponent<MeshFilter>().sharedMesh = turretMesh;
                t.GetComponent<MeshRenderer>().sharedMaterial = mat;
                car.Turret = t.transform;
            }
            if (def.hand)
            {
                if (!leverMesh) leverMesh = VoxelMesherShim(TrainDesigns.Lever().mesh, "Rail_Lever");
                var l = new GameObject("PumpHandle", typeof(MeshFilter), typeof(MeshRenderer));
                l.transform.SetParent(go.transform, false);
                l.transform.localPosition = def.lever + Vector3.up * 0.3f;
                l.GetComponent<MeshFilter>().sharedMesh = leverMesh;
                l.GetComponent<MeshRenderer>().sharedMaterial = mat;
                car.Lever = l.transform;
            }
            if (def.loot != null)
            {
                var lg = new GameObject("Load");
                lg.transform.SetParent(go.transform, false);
                lg.transform.localPosition = new Vector3(0f, def.deckY + 0.6f, def.kind == CarKind.WreckFlat ? 1f : 0f);
                var col = lg.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(3.2f, 1.6f, Mathf.Min(def.length - 1f, 5f));
                car.Loot = lg.AddComponent<Lootable>();
                car.Loot.table = def.loot; car.Loot.title = def.lootTitle;
                car.Loot.enabled = false;                                                        // the train names its key first
            }
            return car;
        }

        static void deckTopFor(RailCar car)
        {
            var dt = new GameObject("DeckTop").transform;
            dt.SetParent(car.transform, false);
            dt.localPosition = new Vector3(0f, car.def.deckY, 0f);
            car.deckTop = dt;
            car.deckBox = car.gameObject.AddComponent<BoxCollider>();
            car.deckBox.center = new Vector3(0f, car.def.deckY - 0.1f, 0f);
            car.deckBox.size = new Vector3(car.def.deckHalf.x * 2f, 0.2f, car.def.deckHalf.y * 2f);
            if (car.def.carDeck) StructureGround.AddMovingDeck(car, dt, car.deckBox, new Vector4(car.def.deckHalf.x, car.def.deckHalf.y, 0f, 0f), car.PointVelocity, null);
        }

        /// <summary>Velocity of the car at a world point (translation plus the turn).</summary>
        public Vector3 PointVelocity(Vector3 p) => Velocity + Vector3.Cross(new Vector3(0f, YawRate * Mathf.Deg2Rad, 0f), p - transform.position);

        /// <summary>Put the car at a pose (physics step; <paramref name="snap"/> teleports without a velocity).</summary>
        public void Pose(Vector3 pos, Quaternion rot, float dt, bool snap)
        {
            if (snap || !hasLast) { transform.SetPositionAndRotation(pos, rot); Body.position = pos; Body.rotation = rot; Velocity = Vector3.zero; YawRate = 0f; }
            else
            {
                Body.MovePosition(pos); Body.MoveRotation(rot);
                if (dt > 0f) { Velocity = (pos - lastPos) / dt; YawRate = Mathf.DeltaAngle(lastYaw, rot.eulerAngles.y) / dt; }
            }
            lastPos = pos; lastYaw = rot.eulerAngles.y; hasLast = true;
            if (def.carDeck)
            {
                ReleaseDriven();
                if (Time.time >= scanAt) { scanAt = Time.time + 0.5f; StrapParked(); }
            }
        }

        // ------------------------------------------------------------------ vehicles riding a flatcar

        void StrapParked()
        {
            var centre = transform.TransformPoint(new Vector3(0f, def.deckY + 1.5f, 0f));
            int n = Physics.OverlapBoxNonAlloc(centre, new Vector3(def.deckHalf.x, 1.5f, def.deckHalf.y), overlap, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var body = overlap[i].attachedRigidbody;
                if (!body || body == Body) continue;
                var v = body.GetComponent<VehicleDriver>();
                if (!v || v.Occupied || v.aiDriven || strapped.Contains(v) || body.transform.parent) continue;
                var l = transform.InverseTransformPoint(body.position);
                if (Mathf.Abs(l.x) > def.deckHalf.x + 0.5f || Mathf.Abs(l.z) > def.deckHalf.y + 0.5f || l.y < def.deckY - 1f) continue;
                var rel = body.isKinematic ? Vector3.zero : body.linearVelocity - PointVelocity(body.position);
                if (rel.sqrMagnitude > 1.5f) continue;
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

        /// <summary>Let every strapped vehicle go (the car is being removed).</summary>
        public void ReleaseAll() { foreach (var v in strapped) if (v) Release(v); strapped.Clear(); }

        // ------------------------------------------------------------------ damage

        public void ApplyHit(Vector3 point, Vector3 direction, float power, float radius, GameObject source)
        {
            if (hp <= 0f) return;
            float armour = def.kind == CarKind.ArmoredLoco || def.kind == CarKind.GunWagon ? 0.35f : 1f;
            hp -= power * 30f * armour;
            Fx.Sparks(point, -direction, 6, new Color(1f, 0.75f, 0.35f));
            MadMax.Audio.Sfx.Play("hit_metal", point, 0.8f, Random.Range(0.8f, 1.05f), 50f);
            if (train) train.OnHit(this, source, power);
            if (hp <= 0f)
            {
                hp = 0f;
                Fx.Smoke(transform.position + Vector3.up * 3f, Vector3.up * 2f, 2.5f, new Color(0.15f, 0.14f, 0.13f, 0.8f), 6f);
                MadMax.Audio.Sfx.Play("explosion", transform.position, 0.6f, 0.8f, 200f);
                if (train) train.OnBroken(this);
            }
        }

        void OnDestroy()
        {
            ReleaseAll();
            StructureGround.Remove(this);
        }
    }
}
