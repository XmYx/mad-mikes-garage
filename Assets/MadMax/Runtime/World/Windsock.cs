using MadMax.Building;
using MadMax.Rendering;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The airfield windsock (2026-10-29; was painted into the hangar grid, pointing one way for ever). A sock of
    /// five banded cloth segments on a swivel ring at the mast top: the ring swings downwind on a damped spring (it lags a
    /// veer and overshoots a little), each segment fills in turn as the wind rises — one stripe per ~1.5 m/s, straight out
    /// at 7.7 m/s (15 knots, as on a real field) — and hangs limp against the mast in a calm; the tail snaps and wanders in
    /// gusts, the swivel squeaks when it swings hard. Pilots read it from the air (<see cref="Reading"/> on the flight HUD).
    /// Built within <see cref="Reach"/> m on the hangar grid's mast (drawn in either look).</summary>
    public class Windsock : MonoBehaviour
    {
        public const float Reach = 180f, FullWind = 7.7f, MastHeight = 5.2f;
        const int Segments = 5;
        const float SegLen = 0.36f, Mouth = 0.22f, Taper = 0.6f;

        public static readonly System.Collections.Generic.List<Windsock> All = new System.Collections.Generic.List<Windsock>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        Transform swivel;
        readonly Transform[] segs = new Transform[Segments];
        float yaw, yawVel, squeakAt, seed;
        bool built;

        /// <summary>The sock's downwind heading (degrees, world) and how far it stands out (0 limp .. 1 full).</summary>
        public float Heading => yaw;
        public float Fill { get; private set; }
        /// <summary>Segments standing out (tests, the flight HUD: "SOCK 3/5").</summary>
        public int Filled { get; private set; }
        /// <summary>Squeaks of the swivel (tests).</summary>
        public int Squeaks { get; private set; }
        /// <summary>The tail segment's world position (tests: it hangs low in a calm, stands out downwind in a breeze).</summary>
        public Vector3 Tail => segs[Segments - 1] ? segs[Segments - 1].TransformPoint(new Vector3(0f, 0f, SegLen)) : transform.position;

        public static Windsock Nearest(Vector3 p, float within)
        {
            Windsock best = null; float bd = within * within;
            foreach (var w in All)
            {
                if (!w) continue;
                float d = (w.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = w; }
            }
            return best;
        }

        /// <summary>"SOCK NW 3/5": blowing from the north-west, three stripes out (what a pilot reads off it).</summary>
        public string Reading
        {
            get
            {
                if (Filled == 0) return "SOCK LIMP";
                float from = Mathf.Repeat(yaw + 180f, 360f);
                string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
                return "SOCK " + dirs[Mathf.RoundToInt(from / 45f) % 8] + " " + Filled + "/" + Segments;
            }
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Start()
        {
            seed = Mathf.Repeat(transform.position.x * 0.37f + transform.position.z * 0.11f, 10f);
            var w = Fx.Wind;
            yaw = w.sqrMagnitude > 0.01f ? Mathf.Atan2(w.x, w.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
        }

        void Build()
        {
            built = true;
            bool hd = HDBits.On;
            var g = MadMax.Game.WastelandGame.Instance;
            var mat = hd ? HDShapes.Solid : g ? g.propMaterial : null;
            swivel = new GameObject("Swivel").transform;
            swivel.SetParent(transform, false);
            swivel.localPosition = new Vector3(0f, MastHeight - 0.1f, 0f);
            PieceBits.Shape(swivel, "Ring", Vector3.zero, "windsock_ring", hd, RingVoxel, RingHD, mat);
            var parent = swivel;
            for (int i = 0; i < Segments; i++)
            {
                int k = i;
                float r0 = Radius(i), r1 = Radius(i + 1);
                var s = PieceBits.Shape(parent, "Sock" + i, i == 0 ? new Vector3(0f, 0f, 0.05f) : new Vector3(0f, 0f, SegLen), "windsock_" + i, hd,
                    vg => SegVoxel(vg, k, r0, r1), sh => SegHD(sh, k, r0, r1), mat);
                segs[i] = s;
                parent = s;
            }
        }

        static float Radius(int i) => Mouth * (1f - Taper * i / Segments);
        static Color32 Band(int i) => i % 2 == 0 ? Pal.Ochre[3] : Pal.Cream[3];

        void Update()
        {
            if (!PieceBits.Near(transform.position, Reach))
            {
                if (swivel && swivel.gameObject.activeSelf) swivel.gameObject.SetActive(false);
                return;
            }
            if (!built) Build();
            if (!swivel.gameObject.activeSelf) swivel.gameObject.SetActive(true);
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (dt <= 0f) return;
            var wind = Fx.Wind;
            float speed = new Vector2(wind.x, wind.z).magnitude;
            float gust = WindDust.Gust;
            float t = Time.time + seed;
            // the swivel: a damped spring toward downwind (lags, overshoots), wandering with the gusts
            float want = speed > 0.3f ? Mathf.Atan2(wind.x, wind.z) * Mathf.Rad2Deg : yaw;
            want += (Mathf.PerlinNoise(t * 0.7f, seed) - 0.5f) * 30f * (0.3f + gust) * Mathf.Clamp01(speed / 3f);
            float stiff = 2.5f + speed * 1.4f;                                                      // more wind, quicker to follow
            for (float left = dt; left > 1e-5f; left -= 1f / 120f)                                  // sub-stepped: steady at long frames
            {
                float h = Mathf.Min(left, 1f / 120f);
                yawVel += (Mathf.DeltaAngle(yaw, want) * stiff - yawVel * 2.2f) * h;
                yaw += yawVel * h;
            }
            yaw = Mathf.Repeat(yaw, 360f);
            if (Mathf.Abs(yawVel) > 55f && Time.time > squeakAt)
            {
                squeakAt = Time.time + 1.4f;
                Squeaks++;
                MadMax.Audio.Sfx.Play("creak", swivel.position, 0.3f, 1.6f + Random.value * 0.2f, 25f);
            }
            swivel.rotation = Quaternion.Euler(0f, yaw, 0f);
            // the cloth: segment i stands out once the wind passes its share of 15 knots
            Fill = Mathf.Clamp01(speed / FullWind);
            int filled = 0;
            float k = 1f - Mathf.Exp(-8f * dt);
            for (int i = 0; i < Segments; i++)
            {
                float stand = Mathf.Clamp01((speed - i * FullWind / Segments) / (FullWind / Segments));
                if (stand > 0.5f) filled++;
                // limp: the mouth tips over and the rest hangs straight down; standing: a small sag, the tail flicking
                float droop = i == 0 ? Mathf.Lerp(70f, 4f, stand) : Mathf.Lerp(18f, 1f, stand);
                float flick = (0.2f + 0.8f * i / (Segments - 1)) * (2f + 9f * gust) * Mathf.Clamp01(speed / 2f);
                float yawFlick = Mathf.Sin(t * (5.3f + i * 1.7f) + i) * flick + Mathf.Sin(t * 13.1f + i * 2.3f) * flick * 0.35f;
                float pitchFlick = Mathf.Sin(t * (6.1f + i * 1.3f) + i * 0.7f) * flick * 0.6f;
                segs[i].localRotation = Quaternion.Slerp(segs[i].localRotation, Quaternion.Euler(droop + pitchFlick, yawFlick, 0f), k);
            }
            Filled = filled;
        }

        // ------------------------------------------------------------------ art

        static void RingVoxel(VoxelGrid g)
        {
            g.Mat((byte)MadMax.Items.ResourceType.Iron);
            int r = Mathf.RoundToInt(Mouth / 0.08f);
            for (int a = 0; a < 24; a++)
            {
                float q = a / 24f * Mathf.PI * 2f;
                g.Set(Mathf.RoundToInt(Mathf.Cos(q) * r), Mathf.RoundToInt(Mathf.Sin(q) * r), 0, Pal.Solid(Pal.Metal[2]));
            }
            g.Set(0, -r - 1, 0, Pal.Solid(Pal.Metal[1]));
        }

        static void RingHD(HDShapes s)
        {
            for (int a = 0; a < 16; a++)
            {
                float q0 = a / 16f * Mathf.PI * 2f, q1 = (a + 1) / 16f * Mathf.PI * 2f;
                s.Tube(new Vector3(Mathf.Cos(q0), Mathf.Sin(q0), 0f) * Mouth, new Vector3(Mathf.Cos(q1), Mathf.Sin(q1), 0f) * Mouth, 0.012f, 0.012f, Pal.Metal[2], 5);
            }
            s.Tube(new Vector3(0f, -Mouth, 0f), new Vector3(0f, -0.1f - Mouth, 0f), 0.02f, 0.02f, Pal.Metal[1], 6);
        }

        static void SegVoxel(VoxelGrid g, int i, float r0, float r1)
        {
            g.Mat((byte)MadMax.Items.ResourceType.Cloth);
            int len = Mathf.RoundToInt(SegLen / 0.08f);
            var band = Band(i);
            var shade = HDShapes.Tone(band, 0.85f);
            for (int z = 0; z < len; z++)
            {
                float r = Mathf.Lerp(r0, r1, z / (float)len) / 0.08f;
                int ri = Mathf.CeilToInt(r);
                for (int x = -ri; x <= ri; x++)
                    for (int y = -ri; y <= ri; y++)
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        if (d > r + 0.35f || d < r - 0.85f) continue;
                        g.Set(x, y, z, p => Pal.Hash(p, 3301 + i) < 0.15f ? shade : band);
                    }
            }
        }

        static void SegHD(HDShapes s, int i, float r0, float r1)
        {
            var band = Band(i);
            s.Tube(Vector3.zero, new Vector3(0f, 0f, SegLen), r0, r1, band, 12);
            s.Tube(new Vector3(0f, 0f, SegLen), Vector3.zero, r1 * 0.97f, r0 * 0.97f, HDShapes.Tone(band, 0.7f), 12);   // the inside of the cloth
        }
    }
}
