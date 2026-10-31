using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The scrapyard's working crane (2026-10-31; the landmark scrapyards were fences, car stacks and a locker).
    /// A rusted tower crane in the middle of the yard: a lattice mast, a slewing jib with its counterweight and the
    /// operator's cab, a trolley running out along the jib, the hoist cable and an electro-magnet. While someone is
    /// within <see cref="WorkReach"/> m it works through the day: slews over a crushed car slab, runs the trolley out,
    /// lowers the magnet, grabs it with a clang and sparks, hoists, swings it over (the hook sways after the slew) and
    /// sets it down on the other heap in a puff of dust, then idles a while. It holds while anyone stands under the hook
    /// (and says so once). By night a floodlight on the jib lights the yard and the crane is still. Decoration: no save.</summary>
    public class ScrapyardCrane : MonoBehaviour
    {
        public const float MastHeight = 10.4f, JibLength = 16f, WorkReach = 160f, ClearOf = 4f, SlabHang = 0.94f;   // slab origin below the magnet's origin (0.26 m disc + 0.68 m slab)
        public enum Step { Idle, Hoist, Slew, Trolley, Lower, Grab }
        static Mesh mastMesh, jibMesh, trolleyMesh, magnetMesh, slabMesh;
        static readonly Color32[] Conc = { Pal.Hex("4a4846"), Pal.Hex("5c5a56"), Pal.Hex("6e6b66"), Pal.Hex("807c76") };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Lifts = 0; Drops = 0; Holds = 0; held = false; All.Clear(); }
        /// <summary>Slabs picked up, set down and frames held for someone underneath this session (tests).</summary>
        public static int Lifts, Drops, Holds;
        public static readonly System.Collections.Generic.List<ScrapyardCrane> All = new System.Collections.Generic.List<ScrapyardCrane>();
        static bool held;

        Transform slew, trolley, cable, magnet, slab;
        Light flood;
        readonly Vector3[] spots = new Vector3[2];
        int from;                        // the slab rests on spots[from] (or hangs from the magnet while carrying)
        bool carrying;
        public Step step = Step.Idle;
        float yaw, radius = 6f, hook = 1.5f, wait = 4f, sway, swaySpeed;
        public bool Carrying => carrying;
        public bool Holding { get; private set; }
        public float Yaw => yaw;
        public float Hook => hook;
        public Vector3 MagnetPosition => magnet ? magnet.position : transform.position;
        public Vector3 SlabPosition => slab ? slab.position : transform.position;
        /// <summary>Tests: skip the idle wait.</summary>
        public void Hurry() { if (step == Step.Idle) wait = 0f; }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        void OnDestroy() { if (slab) Destroy(slab.gameObject); }

        public static ScrapyardCrane Build(Transform parent, Vector3 at, float yardYaw, Vector3 spotA, Vector3 spotB, Material mat)
        {
            var go = new GameObject("ScrapyardCrane");
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yardYaw, 0f));
            var c = go.AddComponent<ScrapyardCrane>();
            c.spots[0] = spotA; c.spots[1] = spotB;
            c.Make(mat);
            return c;
        }

        static Transform Part(Transform parent, Mesh m, Material mat, Vector3 local, string name)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.GetComponent<MeshFilter>().sharedMesh = m;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        void Make(Material mat)
        {
            EnsureMeshes();
            Part(transform, mastMesh, mat, Vector3.zero, "Mast");
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, MastHeight * 0.5f, 0f); box.size = new Vector3(1.2f, MastHeight, 1.2f);
            slew = new GameObject("Slew").transform; slew.SetParent(transform, false); slew.localPosition = new Vector3(0f, MastHeight, 0f);
            Part(slew, jibMesh, mat, Vector3.zero, "Jib");
            trolley = Part(slew, trolleyMesh, mat, new Vector3(0f, -0.1f, radius), "Trolley");
            var cg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cg.GetComponent<Collider>());
            cg.name = "Cable"; cable = cg.transform; cable.SetParent(transform, false);
            var cm = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit")); cm.SetColor("_BaseColor", new Color(0.09f, 0.08f, 0.07f));
            var cr = cg.GetComponent<MeshRenderer>(); cr.sharedMaterial = cm; cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            magnet = Part(transform, magnetMesh, mat, Vector3.zero, "Magnet");
            slab = Part(transform.parent ? transform.parent : transform, slabMesh, mat, Vector3.zero, "CraneSlab");
            slab.position = new Vector3(spots[0].x, GroundAt(spots[0]), spots[0].z);
            slab.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + 12f, 0f);
            var lg = new GameObject("Flood"); lg.transform.SetParent(slew, false); lg.transform.localPosition = new Vector3(0f, -0.4f, 5f);
            lg.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            flood = lg.AddComponent<Light>(); flood.type = LightType.Spot; flood.spotAngle = 95f; flood.range = 26f; flood.intensity = 6f;
            flood.color = new Color(1f, 0.86f, 0.62f); flood.shadows = LightShadows.None; flood.enabled = false;
            yaw = Aim(spots[1], out radius) + 40f; radius = Mathf.Clamp(radius, 2f, JibLength - 0.5f);
            wait = 3f + (Mathf.Abs(transform.position.x * 0.13f) % 5f);
            Pose(0f);
        }

        static void EnsureMeshes()
        {
            if (mastMesh && jibMesh && trolleyMesh && magnetMesh && slabMesh) return;
            const float S = 0.16f;
            var steel = Pal.Weathered(Pal.Ochre, 0.55f, 3101, 2, 0);
            var rust = Pal.Ramp(Pal.Rust, 2, 3102);
            // mast: four corner posts with K bracing, a concrete foot, a ladder
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Scrap);
            int H = Mathf.RoundToInt(MastHeight / S);
            for (int y = 0; y < H; y++)
            {
                foreach (int sx in new[] { -3, 3 }) foreach (int sz in new[] { -3, 3 }) g.Set(sx, y, sz, steel);
                int k = y % 8;
                if (k == 0) { g.Box(-3, y, -3, 3, y, -3, steel); g.Box(-3, y, 3, 3, y, 3, steel); g.Box(-3, y, -3, -3, y, 3, steel); g.Box(3, y, -3, 3, y, 3, steel); }
                int d = -3 + Mathf.RoundToInt(k * 6f / 7f);
                g.Set(d, y, -3, steel); g.Set(-d, y, 3, steel); g.Set(-3, y, -d, steel); g.Set(3, y, d, steel);
                if (y % 2 == 0) g.Set(0, y, -4, rust);                                              // ladder rungs
            }
            g.Box(-1, 0, -4, -1, H - 1, -4, rust); g.Box(1, 0, -4, 1, H - 1, -4, rust);
            g.Mat((byte)MadMax.Items.ResourceType.Stone); g.Box(-6, -3, -6, 6, 0, 6, Pal.Ramp(Conc, 1, 3103));
            g.Bevel(); mastMesh = VoxelMesher.Build(g, "Yard_CraneMast", S);
            // jib: a triangular lattice out to +Z, the counter-jib with concrete blocks, the cab, a turntable
            g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Scrap);
            int L = Mathf.RoundToInt(JibLength / S), C = Mathf.RoundToInt(5f / S);
            g.CylY(0, 0, 4.2f, 0, 1, Pal.Ramp(Pal.Metal, 1, 3104));
            for (int z = -C; z <= L; z++)
            {
                float t = z < 0 ? 1f : 1f - z / (float)L * 0.55f;
                int hgt = Mathf.Max(1, Mathf.RoundToInt(5f * t));
                g.Set(-2, 1, z, steel); g.Set(2, 1, z, steel); g.Set(0, 1 + hgt, z, steel);
                if (z % 6 == 0)
                {
                    g.Box(-2, 1, z, 2, 1, z, steel);
                    for (int i = 0; i <= hgt; i++) { g.Set(Mathf.RoundToInt(-2f + 2f * i / hgt), 1 + i, z, steel); g.Set(Mathf.RoundToInt(2f - 2f * i / hgt), 1 + i, z, steel); }
                }
            }
            g.Box(0, 2, 0, 0, 12, 0, steel);                                                         // the tower head
            g.Tube(new Vector3(0f, 12f, 0f), new Vector3(0f, 6f, L * 0.7f), 0.5f, rust);              // jib tie
            g.Tube(new Vector3(0f, 12f, 0f), new Vector3(0f, 6f, -C), 0.5f, rust);                    // counter-jib tie
            g.Mat((byte)MadMax.Items.ResourceType.Stone);
            g.Box(-3, -3, -C, 3, 2, -C + 6, Pal.Ramp(Conc, 2, 3105));                          // counterweights
            g.Mat((byte)MadMax.Items.ResourceType.Scrap);
            g.Box(3, -6, -2, 9, 0, 4, Pal.Weathered(Pal.Ochre, 0.6f, 3106, 1, 0));                  // the operator's cab
            g.Mat((byte)MadMax.Items.ResourceType.Glass); g.Box(4, -4, 4, 8, -1, 4, Pal.Ramp(Pal.Glass, 2, 3107)); g.Box(9, -4, -1, 9, -1, 3, Pal.Ramp(Pal.Glass, 2, 3108));
            g.Mat((byte)MadMax.Items.ResourceType.Scrap);
            for (int z = 6; z < L; z += 12) g.Box(-1, 0, z, 1, 0, z + 5, Pal.Stripe(Pal.Solid(Pal.Ochre[3]), Pal.Solid(Pal.Black[2]), 2, 2));   // hazard plates
            g.Bevel(); jibMesh = VoxelMesher.Build(g, "Yard_CraneJib", S);
            // trolley: a carriage with sheaves
            g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Scrap);
            g.Box(-3, -1, -2, 3, 0, 2, Pal.Ramp(Pal.Metal, 2, 3109));
            g.CylX(-2, 0, 1.6f, -2, 2, Pal.Ramp(Pal.Chrome, 1));
            g.Bevel(); trolleyMesh = VoxelMesher.Build(g, "Yard_CraneTrolley", 0.08f);
            // magnet: a ribbed disc with chains up to a shackle
            g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Scrap);
            g.CylY(0, 0, 9.5f, -3, 0, Pal.Weathered(Pal.Metal, 0.5f, 3110, 2, 0));
            for (int a = 0; a < 8; a++) { float an = a * Mathf.PI / 4f; int x = Mathf.RoundToInt(Mathf.Cos(an) * 8f), z = Mathf.RoundToInt(Mathf.Sin(an) * 8f); g.Set(x, 1, z, rust); }
            g.CylY(0, 0, 3f, 1, 2, Pal.Solid(Pal.Ochre[3]));
            for (int i = 0; i < 3; i++) { float an = i * 2.094f; g.Tube(new Vector3(Mathf.Cos(an) * 7f, 1f, Mathf.Sin(an) * 7f), new Vector3(0f, 11f, 0f), 0.6f, Pal.Ramp(Pal.Metal, 1)); }
            g.Box(-1, 11, 0, 1, 13, 0, Pal.Ramp(Pal.Chrome, 1));
            g.Bevel(); magnetMesh = VoxelMesher.Build(g, "Yard_CraneMagnet", 0.08f);
            // the load: a crushed car slab in faded paint, a wheel still on
            g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Scrap);
            g.Box(-24, 0, -11, 24, 6, 11, Pal.Weathered(Pal.Navy, 0.6f, 3111, 2, 0));
            g.Box(-13, 7, -8, 9, 8, 8, Pal.Weathered(Pal.Navy, 0.65f, 3112, 1, 0));
            g.Mat((byte)MadMax.Items.ResourceType.Glass); g.Box(-12, 5, -11, 8, 5, -11, Pal.Ramp(Pal.Glass, 1)); g.Mat((byte)MadMax.Items.ResourceType.Scrap);
            g.CylZ(-16, 3, 3f, 11, 13, Pal.Ramp(Pal.Tire, 1));
            g.Bevel(); slabMesh = VoxelMesher.Build(g, "Yard_CraneSlab", 0.08f);
        }

        /// <summary>Slew angle (degrees, world) and trolley radius to put the hook over <paramref name="p"/>.</summary>
        float Aim(Vector3 p, out float r)
        {
            var d = p - transform.position; d.y = 0f;
            r = d.magnitude;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        float GroundAt(Vector3 p)
        {
            var t = DeformableTerrain.Instance;
            return t ? t.HeightNoLoad(p.x, p.z) : transform.position.y;
        }

        bool SomeoneUnder()
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g || !g.Player) return false;
            var me = g.Current ? g.Current.transform.position : g.Player.transform.position;
            var m = magnet.position; me.y = m.y = 0f;
            return (me - m).sqrMagnitude < ClearOf * ClearOf;
        }

        void Update()
        {
            if (!slew) return;
            float dt = Time.deltaTime;
            bool night = DayNight.Darkness > 0.45f;
            bool near = MadMax.Building.PieceBits.Near(transform.position, WorkReach);
            if (flood) flood.enabled = night && near && LightBudget.Allowed(flood);
            if (!near) return;
            bool moving = false;
            Holding = false;
            var target = spots[carrying ? 1 - from : from];
            float wantYaw = Aim(target, out float wantR); wantR = Mathf.Clamp(wantR, 2f, JibLength - 0.5f);
            float topY = trolley.position.y;
            float lowY = GroundAt(target) + SlabHang;                                            // magnet face on the slab top / the slab on the ground
            switch (step)
            {
                case Step.Idle:
                    if (!night && (wait -= dt) <= 0f) step = Step.Hoist;
                    break;
                case Step.Hoist:
                    if (carrying && SomeoneUnder()) { Holding = true; break; }
                    hook = Mathf.MoveTowards(hook, 1.2f, dt * 0.9f); moving = true;
                    if (hook <= 1.21f) step = Step.Slew;
                    break;
                case Step.Slew:
                    if (carrying && SomeoneUnder()) { Holding = true; break; }
                    float before = yaw;
                    yaw = Mathf.MoveTowardsAngle(yaw, wantYaw, dt * 9f); moving = true;
                    swaySpeed += Mathf.DeltaAngle(before, yaw) * 0.02f;
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, wantYaw)) < 0.2f) step = Step.Trolley;
                    break;
                case Step.Trolley:
                    if (carrying && SomeoneUnder()) { Holding = true; break; }
                    radius = Mathf.MoveTowards(radius, wantR, dt * 0.9f); moving = true;
                    if (Mathf.Abs(radius - wantR) < 0.02f) step = Step.Lower;
                    break;
                case Step.Lower:
                    if (SomeoneUnder()) { Holding = true; break; }
                    float want = Mathf.Max(1.2f, topY - lowY);
                    hook = Mathf.MoveTowards(hook, want, dt * 0.8f); moving = true;
                    if (Mathf.Abs(hook - want) < 0.02f) { step = Step.Grab; wait = 1.4f; Clang(); }
                    break;
                case Step.Grab:
                    if ((wait -= dt) > 0f) break;
                    if (!carrying) { carrying = true; Lifts++; step = Step.Hoist; }
                    else
                    {
                        carrying = false; from = 1 - from; Drops++;
                        slab.position = new Vector3(spots[from].x, GroundAt(spots[from]), spots[from].z);
                        slab.rotation = Quaternion.Euler(0f, yaw + 90f, 0f);
                        for (int i = 0; i < 6; i++) Fx.Smoke(slab.position + Random.insideUnitSphere * 1.6f + Vector3.up * 0.2f, Random.insideUnitSphere * 0.6f + Vector3.up * 0.4f, 0.9f, new Color(0.55f, 0.48f, 0.4f, 0.7f), 2.2f);
                        MadMax.Audio.Sfx.Play("hit_metal", slab.position, 1f, 0.45f, 90f);
                        step = Step.Idle; wait = Random.Range(8f, 20f);
                    }
                    break;
            }
            if (Holding)
            {
                Holds++;
                var g = MadMax.Game.WastelandGame.Instance;
                if (!held && g) { held = true; g.Toast("THE CRANE MAN WAITS FOR YOU TO STEP CLEAR OF THE HOOK"); MadMax.Audio.Sfx.Play("creak", magnet.position, 0.7f, 0.8f, 40f); }
            }
            if (moving) MadMax.Audio.Sfx.Loop(this, "hydraulic", 0.32f, 0.75f, 70f);
            Pose(dt);
        }

        void Clang()
        {
            var p = magnet.position + Vector3.down * 0.25f;
            MadMax.Audio.Sfx.Play("hit_metal", p, 1f, Random.Range(0.6f, 0.75f), 80f);
            Fx.Sparks(p, Vector3.up, 10, new Color(1f, 0.8f, 0.4f));
        }

        void Pose(float dt)
        {
            slew.rotation = Quaternion.Euler(0f, yaw, 0f);
            trolley.localPosition = new Vector3(0f, -0.1f, radius);
            // pendulum sway of the hook after a slew, damped
            swaySpeed += -sway * dt * 2.2f; swaySpeed *= Mathf.Exp(-dt * 0.8f); sway += swaySpeed * dt * 4f;
            sway = Mathf.Clamp(sway, -0.6f, 0.6f);
            var top = trolley.position;
            var mp = top + Vector3.down * hook + slew.right * sway * Mathf.Min(1f, hook / 6f);
            magnet.SetPositionAndRotation(mp, Quaternion.Euler(0f, yaw, 0f));
            var shackle = mp + Vector3.up * 1.04f;
            var span = shackle - top;
            cable.position = (top + shackle) * 0.5f;
            if (span.sqrMagnitude > 1e-4f) cable.rotation = Quaternion.FromToRotation(Vector3.up, span.normalized);
            cable.localScale = new Vector3(0.05f, Mathf.Max(0.05f, span.magnitude), 0.05f);
            if (carrying) slab.SetPositionAndRotation(mp + Vector3.down * SlabHang, Quaternion.Euler(0f, yaw + 90f, 0f));
        }
    }
}
