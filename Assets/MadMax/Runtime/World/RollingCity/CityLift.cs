using MadMax.Building;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A city lift (tracked city docks, megacity shafts): a kinematic platform that shuttles between two landings
    /// (dwelling at each end), a moving <see cref="StructureGround"/> deck for wheels and an <see cref="IDeckCarrier"/>
    /// for walkers. Barriers across both open ends of the platform rise whenever it moves (nobody walks off mid-shaft)
    /// and sink into the deck while it stands at a landing.</summary>
    public class CityLift : MonoBehaviour, IDeckCarrier
    {
        public float bottom, top, speed = 1.6f, dwell = 9f;
        /// <summary>Ramp up/down (m/s²; 0 = full speed at once).</summary>
        public float accel;
        /// <summary>Someone waiting at the far landing cuts the dwell short; someone arriving at the landing as it is about
        /// to leave holds it once more (megacity lifts).</summary>
        public bool callable;
        bool held;
        Rigidbody rb;
        float y, wait, dir = 1f, cur, callAt, barrier;
        Vector3 velocity;
        readonly Transform[] ends = new Transform[2];
        readonly BoxCollider[] endBoxes = new BoxCollider[2];
        static Mesh barrierMesh;

        /// <summary>Seconds before it leaves the landing it stands at (0 while moving).</summary>
        public float DwellLeft => wait;
        /// <summary>Standing at a landing (true: the top) with its gates open.</summary>
        public bool Parked(bool atTop) => wait > 0f && Mathf.Abs(y - (atTop ? top : bottom)) < 0.01f;
        /// <summary>The platform's top centre where physics has it this step (riders are held to it).</summary>
        public Vector3 PhysicsTop => rb ? rb.position : transform.position;
        /// <summary>World position of a landing's platform centre.</summary>
        public Vector3 Landing(bool atTop) { var lp = transform.localPosition; lp.y = atTop ? top : bottom; return transform.parent.TransformPoint(lp); }

        public float YawRate => 0f;
        public Vector3 PointVelocity(Vector3 p) => velocity;
        /// <summary>0 at the yard, 1 at the pier.</summary>
        public float Level => Mathf.InverseLerp(bottom, top, y);

        public static CityLift Create(Transform dock, Vector3 local, float H, Mesh mesh, Mesh glow, Mesh solid, Material mat, Material glowMat)
        {
            var go = new GameObject("Lift");
            go.transform.SetParent(dock, false);
            go.transform.localPosition = local + Vector3.down * H;
            var lift = go.AddComponent<CityLift>();
            lift.bottom = local.y - H; lift.top = local.y; lift.y = lift.bottom; lift.wait = 4f;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (glow && glow.vertexCount > 0)
            {
                var g = new GameObject("Glow", typeof(MeshFilter), typeof(MeshRenderer));
                g.transform.SetParent(go.transform, false);
                g.GetComponent<MeshFilter>().sharedMesh = glow;
                g.GetComponent<MeshRenderer>().sharedMaterial = glowMat;
            }
            lift.rb = go.AddComponent<Rigidbody>();
            lift.rb.isKinematic = true;
            lift.rb.interpolation = RigidbodyInterpolation.Interpolate;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = solid;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.2f, 0f);
            box.size = new Vector3(TrackedCity.LiftHalfX * 2f, 0.4f, TrackedCity.LiftHalfZ * 2f);
            StructureGround.AddMovingDeck(lift, go.transform, box, new Vector4(TrackedCity.LiftHalfX, TrackedCity.LiftHalfZ, 0f, 0f), lift.PointVelocity);
            if (!barrierMesh) barrierMesh = VoxelMesher.ToMesh(MegaCity.Gate(TrackedCity.LiftHalfX * 2f - 0.1f, 5).mesh, "CityLift_Barrier");
            for (int k = 0; k < 2; k++)                                                              // barriers across the open ends
            {
                var e = new GameObject("EndBarrier", typeof(MeshFilter), typeof(MeshRenderer));
                e.transform.SetParent(go.transform, false);
                e.transform.localPosition = new Vector3(0f, -1.25f, (k == 0 ? -1f : 1f) * (TrackedCity.LiftHalfZ - 0.12f));
                e.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                e.GetComponent<MeshFilter>().sharedMesh = barrierMesh;
                e.GetComponent<MeshRenderer>().sharedMaterial = mat;
                var eb = e.AddComponent<BoxCollider>();
                eb.center = new Vector3(0f, 0.65f, 0f); eb.size = new Vector3(0.16f, 1.3f, TrackedCity.LiftHalfX * 2f - 0.1f);
                eb.enabled = false;
                lift.ends[k] = e.transform; lift.endBoxes[k] = eb;
            }
            return lift;
        }

        void Update()
        {
            barrier = Mathf.MoveTowards(barrier, wait > 1.5f ? 0f : 1f, Time.deltaTime * 2.5f);       // up from just before it leaves, down at a landing
            for (int k = 0; k < 2; k++)
            {
                if (!ends[k]) continue;
                var lp = ends[k].localPosition; lp.y = -1.25f * (1f - barrier); ends[k].localPosition = lp;
                endBoxes[k].enabled = barrier > 0.5f;
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float target = dir > 0f ? top : bottom;
            if (callable && wait > 0f && Time.time >= callAt)
            {
                callAt = Time.time + 0.25f;
                var game = MadMax.Game.WastelandGame.Instance;
                if (game && game.Player)
                {
                    var me = game.Player.transform.position;
                    var far = Landing(dir > 0f);                                                     // the landing it would leave for next
                    var here = Landing(dir < 0f);
                    if (wait > 1.5f && new Vector2(me.x - far.x, me.z - far.z).magnitude < 9f && Mathf.Abs(me.y - far.y) < 3f) wait = 1.5f;
                    else if (wait <= 1.5f && !held && new Vector2(me.x - here.x, me.z - here.z).magnitude < 9f && Mathf.Abs(me.y - here.y) < 3f) { wait = 4f; held = true; }
                }
            }
            if (wait > 0f) { wait -= dt; velocity = Vector3.zero; cur = 0f; }
            else
            {
                float v = speed;
                if (accel > 0f) v = cur = Mathf.Min(speed, cur + accel * dt, Mathf.Sqrt(2f * accel * Mathf.Abs(target - y)) + 0.15f);
                float ny = Mathf.MoveTowards(y, target, v * dt);
                velocity = transform.parent.TransformDirection(Vector3.up) * ((ny - y) / dt);
                y = ny;
                if (Mathf.Approximately(y, target)) { wait = dwell; dir = -dir; held = false; MadMax.Audio.Sfx.Play("chain", transform.position, 0.5f, 0.6f, 60f); }
            }
            var lp = transform.localPosition; lp.y = y;
            rb.MovePosition(transform.parent.TransformPoint(lp));
            if (wait <= 0f) MadMax.Audio.Sfx.Loop(this, "chain", 0.35f, 0.7f, 50f);
            else MadMax.Audio.Sfx.Loop(this, "chain", 0f, 0.7f, 50f);
        }

        void OnDestroy() => StructureGround.Remove(this);
    }
}
