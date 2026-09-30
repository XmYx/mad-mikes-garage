using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Operator keys for a machine: held and pressed state of 1..6 plus Shift.</summary>
    public struct MachineKeys
    {
        public bool h1, h2, h3, h4, h5, h6, p1, p2, p3, p4, p5, p6, shift;
        /// <summary>Natural controls: up = arrows up/down (+ up), side = left/right (+ right), qe = Q (+1) / E (-1);
        /// qPressed / ePressed this frame.</summary>
        public float up, side, qe;
        public bool qPressed, ePressed;
        /// <summary>+1 / -1 / 0 from a pair of held keys.</summary>
        public static float Axis(bool plus, bool minus) => (plus ? 1f : 0f) - (minus ? 1f : 0f);
    }

    /// <summary>Construction machine behaviour. Every hinge of the tool moves on its own (the tool part is baked in
    /// segments, see <see cref="MadMax.Designs.PartDesign.Segment"/>):
    /// Excavator: [1]/[2] boom down/up, [3]/[4] stick in/out, [5]/[6] bucket curl/open, Shift+[1]/[2] swing.
    /// Curling the bucket (or pulling the stick in) with the teeth in the ground digs; opening it dumps into a truck bed
    /// or container in reach, else onto the ground.
    /// Backhoe loader: front loader [1]/[2] arms down/up, [5]/[6] bucket curl/dump (drive into a pile to fill it);
    /// rear hoe Shift+[1]/[2] swing, [3]/[4] boom down/up, Shift+[3]/[4] stick in/out, Shift+[5]/[6] bucket curl/open.
    /// Dozer: [1]/[2] blade down/up (below ground = cut depth), [3]/[4] blade pitch, [5]/[6] blade angle (spills sideways).
    /// Dump truck: [1]/[2] bed down/up (unloads behind when tipped). Paver: [1] paving on/off, [3] material, [5]/[6] screed.
    /// Roller: compacts wet paving while driving. Tools sit on the MachineTool layer: they never collide with the
    /// terrain, so a blade or bucket in the ground does not pin the machine. Terrain edits go through
    /// <see cref="DeformableTerrain.ApplyTerraform"/> and are replicated.</summary>
    public class Machine : MonoBehaviour
    {
        public enum Kind { Excavator, Backhoe, Dozer, DumpTruck, Paver, Roller }
        public Kind kind;
        public float load;                      // m³ of soil in the bucket / blade pile
        public ResourceType loadType = ResourceType.Sand;
        public float rearLoad;                  // backhoe: rear hoe bucket
        public ResourceType rearType = ResourceType.Sand;
        public bool active, concrete;
        public const float UnitsPerM3 = 10f;
        const float S = 0.08f;

        VehicleDriver driver;
        MountSocket toolSocket, rearSocket;
        Container store;
        float tick, rearTick, travel;
        // hinge angles (degrees; + pitches a forward-pointing segment down)
        float slew, boom = -8f, stick = 10f, bucket = 10f;             // excavator
        float arms = 0f, tilt = 0f;                                     // backhoe front loader
        float hoeSwing, hoeBoom = -10f, hoeStick = 0f, hoeBucket = 0f;  // backhoe rear hoe
        float bladeLift = -14f, bladePitch = 0f, bladeAngle = 0f;      // dozer: carried up (negative) until the operator lowers it
        float bed;                                                      // dump truck 0..50
        float screed;                                                   // paver
        Vector3 lastPos;
        SoilHeap heap, rearHeap, bedHeap;
        float bedFill, bedScan, trickle;
        ResourceType bedType = ResourceType.None;
        static readonly List<DebrisSystem.Chunk> clods = new List<DebrisSystem.Chunk>();

        public string Status { get; private set; }
        /// <summary>Hinge angles for diagnostics: excavator bucket / loader tilt, dozer blade lift.</summary>
        public float BucketAngle => kind == Kind.Backhoe ? tilt : kind == Kind.Dozer ? bladeLift : bucket;
        /// <summary>Excavator / loader bucket teeth in world space (zero without a bucket); automation and tests.</summary>
        public Vector3 BucketTip => kind == Kind.Excavator ? Teeth(Seg(Tool, "bucket"), new Vector3(0, -8, 4)) : kind == Kind.Backhoe ? Teeth(Seg(Tool, "bucket"), new Vector3(0, -6, 5)) : Vector3.zero;
        /// <summary>Where a bucket load lands in this tipper's bed (its own position for other machines).</summary>
        public Vector3 BedPoint => store ? LoadPoint(store) : transform.position;
        /// <summary>Soil units in the tipper bed / hopper (not fluids).</summary>
        public int BedUnits { get { int n = 0; if (store) for (int t = 1; t < ResourceInfo.Count; t++) if (!ResourceInfo.IsFluid((ResourceType)t)) n += store.inventory.Get((ResourceType)t); return n; } }
        public float Capacity => kind == Kind.Excavator ? 1.4f : kind == Kind.Backhoe ? 0.8f : kind == Kind.Dozer ? 2.5f : 0f;
        const float RearCapacity = 0.35f;
        const float BedM3 = 6.5f;                                       // tipper bed volume (2.3 x 3.8 x 0.75 m)
        public Container Store => store;

        void Awake()
        {
            driver = GetComponent<VehicleDriver>();
            foreach (var s in GetComponentsInChildren<MountSocket>())
            {
                if (s.name == "tool") toolSocket = s;
                if (s.name == "tool_rear") rearSocket = s;
            }
            if (kind == Kind.DumpTruck || kind == Kind.Paver)
            {
                store = gameObject.AddComponent<Container>();
                store.title = kind == Kind.DumpTruck ? "TIPPER BED" : "PAVER HOPPER";
                store.capacity = kind == Kind.DumpTruck ? 12000f : 4000f;
            }
            lastPos = transform.position;
        }

        Transform Tool => toolSocket && toolSocket.Current ? toolSocket.Current.transform : null;
        Transform Rear => rearSocket && rearSocket.Current ? rearSocket.Current.transform : null;
        readonly Dictionary<(Transform, string), Transform> segs = new Dictionary<(Transform, string), Transform>();

        /// <summary>A named hinge segment under the tool (cached: every machine poses its arm every frame).</summary>
        Transform Seg(Transform root, string name)
        {
            if (!root) return null;
            var key = (root, name);
            if (segs.TryGetValue(key, out var t) && (t || (object)t == null)) return t;      // cached, "none" included; destroyed → look again
            return segs[key] = FindDeep(root, name);
        }
        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r) return r; }
            return null;
        }
        bool Simulated { get { var rep = GetComponent<MadMax.Net.NetReplica>(); return !(rep && rep.active); } }

        static float Terraform(DeformableTerrain.TerraOp op, Vector3 p, float r, float amount, byte extra = 0)
        {
            var t = DeformableTerrain.Instance;
            if (!t) return 0f;
            float moved = t.ApplyTerraform((byte)op, p, r, amount, extra);
            MadMax.Net.NetSession.Instance?.SendTerraform((byte)op, p, r, amount, extra);
            return moved;
        }

        static void Hinge(ref float angle, float dir, float speed, float min, float max, float dt) => angle = Mathf.Clamp(angle + dir * speed * dt, min, max);

        /// <summary>Operator input (called by the game each frame while this machine is driven).</summary>
        public void Control(MachineKeys k)
        {
            var terrain = DeformableTerrain.Instance;
            if (!terrain) return;
            float dt = Time.deltaTime;
            switch (kind)
            {
                case Kind.Excavator:
                {
                    // numbers as before; arrows: up/down boom (Shift: stick out/in), left/right swing, Q curl / E open
                    Hinge(ref slew, Mathf.Clamp((k.shift ? MachineKeys.Axis(k.h2, k.h1) : 0f) + k.side, -1f, 1f), 30f, -150f, 150f, dt);
                    Hinge(ref boom, Mathf.Clamp(k.shift ? 0f : MachineKeys.Axis(k.h1, k.h2) - k.up, -1f, 1f), 22f, -35f, 45f, dt);
                    float stickIn = Mathf.Clamp(MachineKeys.Axis(k.h3, k.h4) - (k.shift ? k.up : 0f), -1f, 1f);
                    Hinge(ref stick, stickIn, 28f, -55f, 75f, dt);
                    // a rock drill in place of the bucket: [5] spins the auger into rock, outcrops and deep ground
                    var bit = Seg(Tool, "bit");
                    if (bit)
                    {
                        bool spin = k.h5 && !k.shift;
                        if (spin) bit.localRotation *= Quaternion.Euler(0f, 900f * dt, 0f);
                        string doing = Drill(bit.TransformPoint(new Vector3(0, -15, 0) * S), spin, dt);
                        Status = "EXCAVATOR DRILL" + (doing != null ? "  " + doing : "") + "  [1/2] BOOM  [3/4] STICK  [5] DRILL  [SHIFT+1/2] SWING";
                        break;
                    }
                    Hinge(ref bucket, Mathf.Clamp(MachineKeys.Axis(k.h5, k.h6) + k.qe, -1f, 1f), 45f, -80f, 75f, dt);
                    var tip = Teeth(Seg(Tool, "bucket"), new Vector3(0, -8, 4));
                    bool inSoil = Dig(ref load, ref loadType, ref tick, Capacity, tip, ((k.h5 || k.h3) && !k.shift) || k.qe > 0.5f || stickIn > 0.5f, 1.0f, 0.14f, dt, 8f);
                    if ((k.h6 || k.qe < -0.5f) && bucket < -25f && load > 0.01f) Dump(ref load, loadType, tip, 7f);
                    Status = "EXCAVATOR  BUCKET " + Pct(load, Capacity) + " " + ResourceInfo.Name(loadType) + (inSoil ? "  (IN SOIL)" : "") +
                             "  UP/DN BOOM  L/R SWING  Q CURL  E DUMP  SHIFT+UP/DN STICK";
                    break;
                }
                case Kind.Backhoe:
                {
                    if (!k.shift)
                    {
                        // front loader: up/down arms, Q curl / E dump
                        Hinge(ref arms, Mathf.Clamp(MachineKeys.Axis(k.h1, k.h2) - k.up, -1f, 1f), 25f, -50f, 14f, dt);
                        Hinge(ref tilt, Mathf.Clamp(MachineKeys.Axis(k.h6, k.h5) - k.qe, -1f, 1f), 40f, -45f, 60f, dt);
                        Hinge(ref hoeBoom, MachineKeys.Axis(k.h4, k.h3), 22f, -45f, 30f, dt);
                    }
                    else
                    {
                        // Shift: the rear hoe — up/down boom, left/right swing, Q curl / E open
                        Hinge(ref hoeSwing, Mathf.Clamp(MachineKeys.Axis(k.h2, k.h1) + k.side, -1f, 1f), 28f, -80f, 80f, dt);
                        Hinge(ref hoeBoom, Mathf.Clamp(k.up, -1f, 1f), 22f, -45f, 30f, dt);
                        Hinge(ref hoeStick, MachineKeys.Axis(k.h4, k.h3), 28f, -60f, 60f, dt);
                        Hinge(ref hoeBucket, Mathf.Clamp(MachineKeys.Axis(k.h6, k.h5) - k.qe, -1f, 1f), 45f, -70f, 80f, dt);
                    }
                    var edge = Teeth(Seg(Tool, "bucket"), new Vector3(0, -6, 5));
                    bool pushing = driver && driver.ForwardSpeed > 0.3f;
                    bool front = Dig(ref load, ref loadType, ref tick, Capacity, edge, pushing || (!k.shift && (k.h5 || k.qe > 0.5f)), 1.3f, 0.08f, dt, 2f);
                    if (!k.shift && (k.h6 || k.qe < -0.5f) && tilt > 25f && load > 0.01f) Dump(ref load, loadType, edge, 4f);
                    var hoeTip = Teeth(Seg(Rear, "bucket"), new Vector3(0, -6, -3));
                    bool rear = Rear && Dig(ref rearLoad, ref rearType, ref rearTick, RearCapacity, hoeTip, k.shift && (k.h5 || k.h3 || k.qe > 0.5f), 0.7f, 0.06f, dt, 3f);
                    if (Rear && k.shift && (k.h6 || k.qe < -0.5f) && hoeBucket > 30f && rearLoad > 0.01f) Dump(ref rearLoad, rearType, hoeTip, 4f);
                    Status = "LOADER " + Pct(load, Capacity) + (front ? "*" : "") + "  HOE " + Pct(rearLoad, RearCapacity) + (rear ? "*" : "") +
                             "  UP/DN ARMS  Q CURL  E DUMP   SHIFT: UP/DN HOE BOOM  L/R SWING  Q/E HOE BUCKET";
                    break;
                }
                case Kind.Dozer:
                {
                    Hinge(ref bladeLift, Mathf.Clamp(MachineKeys.Axis(k.h1, k.h2) - k.up, -1f, 1f), 12f, -20f, 16f, dt);       // up raises
                    Hinge(ref bladePitch, Mathf.Clamp(MachineKeys.Axis(k.h3, k.h4) + k.qe, -1f, 1f), 15f, -15f, 15f, dt);
                    Hinge(ref bladeAngle, Mathf.Clamp(MachineKeys.Axis(k.h6, k.h5) + k.side, -1f, 1f), 20f, -25f, 25f, dt);
                    var e = BladeEdge();
                    float depth = e.HasValue ? terrain.Height(e.Value.x, e.Value.z) - e.Value.y : -1f;
                    Status = "BULLDOZER  BLADE " + (depth > 0.02f ? "CUT " + depth.ToString("0.00") + " M" : "UP " + (-depth).ToString("0.00") + " M") +
                             "  PILE " + load.ToString("0.0") + " M3  UP/DN BLADE  L/R ANGLE  Q/E PITCH";
                    break;
                }
                case Kind.DumpTruck:
                    Hinge(ref bed, Mathf.Clamp(MachineKeys.Axis(k.h2, k.h1) + k.up, -1f, 1f), 12f, 0f, 50f, dt);
                    Status = "TIPPER  " + Mathf.RoundToInt(store.Weight) + " KG  BED " + Mathf.RoundToInt(bed) + "  UP/DN RAISE/LOWER THE BED";
                    break;
                case Kind.Paver:
                    if (k.p1 || k.qPressed) active = !active;
                    if (k.p3 || k.ePressed) concrete = !concrete;
                    Hinge(ref screed, Mathf.Clamp(MachineKeys.Axis(k.h5, k.h6) + k.up, -1f, 1f), 10f, -4f, 10f, dt);
                    var mat = concrete ? ResourceType.Concrete : ResourceType.Asphalt;
                    Status = "PAVER  " + (active ? "PAVING " : "IDLE ") + ResourceInfo.Name(mat) + " " + store.inventory.Get(mat) + "  Q PAVE ON/OFF  E MATERIAL  UP/DN SCREED";
                    break;
                case Kind.Roller:
                    Status = "ROLLER  COMPACTS WET PAVING";
                    break;
            }
        }

        static string Pct(float v, float cap) => Mathf.RoundToInt(v / Mathf.Max(0.01f, cap) * 100f) + "%";

        static Vector3 Teeth(Transform seg, Vector3 local) => seg ? seg.TransformPoint(local * S) : Vector3.zero;

        float drillTick;
        static readonly Collider[] drillHits = new Collider[16];

        /// <summary>The spinning auger: breaks rock props and walls it touches (their ore and stone drop as pickups) and
        /// bores deep into the ground, handing the spoil (ore over a deposit) straight to the operator.</summary>
        string Drill(Vector3 tip, bool spinning, float dt)
        {
            MadMax.Audio.Sfx.Loop(this, "grinder", spinning ? 0.6f : 0f, 0.7f, 40f);
            if (!spinning || (drillTick -= dt) > 0f) return null;
            drillTick = 0.2f;
            string doing = "SPINNING";
            int n = Physics.OverlapSphereNonAlloc(tip, 0.8f, drillHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = drillHits[i];
                if (c.transform.IsChildOf(transform)) continue;
                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null) continue;
                var p = c is MeshCollider mc && !mc.convex ? c.bounds.ClosestPoint(tip) : c.ClosestPoint(tip);
                dmg.ApplyHit(p, Vector3.down, 1.2f, 0.45f, gameObject);
                Fx.Sparks(p, Vector3.up, 3, new Color(1f, 0.8f, 0.4f));
                doing = "BREAKING ROCK";
                break;
            }
            var terrain = DeformableTerrain.Instance;
            float ground = terrain.Height(tip.x, tip.z);
            if (tip.y < ground + 0.1f)
            {
                float depth = terrain.DugDepth(tip.x, tip.z);
                if (depth > 12f) return "AS DEEP AS THE DRILL GOES";
                var at = new Vector3(tip.x, ground, tip.z);
                float moved = Terraform(DeformableTerrain.TerraOp.Dig, at, 0.5f, 0.22f);
                if (moved > 0f)
                {
                    var type = terrain.SoilAt(tip.x, tip.z, depth);
                    int units = Mathf.Max(1, Mathf.RoundToInt(moved * UnitsPerM3));
                    var g = MadMax.Game.WastelandGame.Instance;
                    if (g) g.Inventory.Add(type, units);
                    Fx.Smoke(at + Vector3.up * 0.3f, Vector3.up * 1.2f + Random.insideUnitSphere * 0.5f, 0.5f, DustColor(type), 1.2f);
                    doing = "BORING " + depth.ToString("0.0") + " M: " + ResourceInfo.Name(type);
                }
            }
            return doing;
        }

        /// <summary>Scoop while the teeth are in the soil and the operator is curling / pushing. Returns whether the teeth are in the ground.</summary>
        bool Dig(ref float held, ref ResourceType type, ref float timer, float cap, Vector3 tip, bool working, float radius, float amount, float dt, float maxDepth)
        {
            if (tip == Vector3.zero) return false;
            var terrain = DeformableTerrain.Instance;
            float ground = terrain.Height(tip.x, tip.z);
            bool inSoil = tip.y < ground + 0.08f;
            if (!working || !inSoil || held >= cap || (timer -= dt) > 0f) return inSoil;
            timer = 0.25f;
            float depth = terrain.DugDepth(tip.x, tip.z);
            if (depth > maxDepth) { Status = "TOO DEEP FOR THIS BUCKET"; return inSoil; }
            var at = new Vector3(tip.x, ground, tip.z);
            float bite = Mathf.Clamp01((ground - tip.y + 0.1f) / 0.4f);                 // deeper teeth, bigger bite
            float moved = Terraform(DeformableTerrain.TerraOp.Dig, at, radius, amount * (0.4f + bite));
            if (moved > 0f)
            {
                type = terrain.SoilAt(tip.x, tip.z, depth);
                held = Mathf.Min(cap, held + moved);
                Fx.Smoke(at + Vector3.up * 0.3f, Vector3.up * 0.8f + Random.insideUnitSphere * 0.5f, 0.6f, DustColor(type), 1.5f);
                Clods(tip + Vector3.up * 0.15f, type, 3 + Mathf.RoundToInt(bite * 4f), 0.35f, (tip - transform.position).normalized * 0.8f);
            }
            return inSoil;
        }

        static Color DustColor(ResourceType t) => ((Color)ResourceInfo.Color(t)) * new Color(1f, 1f, 1f, 0.7f);

        Vector3? BladeEdge() { var b = Seg(Tool, "blade"); return b ? b.TransformPoint(new Vector3(0, -4, 2) * S) : (Vector3?)null; }

        /// <summary>Empty a bucket: into a dump truck bed / container in reach, else onto the ground.</summary>
        void Dump(ref float held, ResourceType type, Vector3 tip, float reach)
        {
            int units = Mathf.RoundToInt(held * UnitsPerM3);
            Container target = null; float best = reach;
            foreach (var c in Container.All)
            {
                if (!c || c == store) continue;
                float d = Vector3.Distance(LoadPoint(c), tip);
                if (d < best && c.Weight + units * ItemCatalog.ResourceWeight(type) <= c.capacity) { best = d; target = c; }
            }
            if (target && units > 0) target.inventory.Add(type, units);
            else
            {
                var t = DeformableTerrain.Instance;
                var at = new Vector3(tip.x, t.Height(tip.x, tip.z), tip.z);
                Terraform(DeformableTerrain.TerraOp.Dump, at, 1.2f, held / (Mathf.PI * 1.2f * 1.2f * 0.45f));
            }
            for (int i = 0; i < 8; i++) Fx.Smoke(tip + Random.insideUnitSphere * 0.6f, Vector3.down + Random.insideUnitSphere, 0.5f, DustColor(type), 1.2f);
            Clods(tip, type, Mathf.Clamp(units, 6, 18), 0.5f, Vector3.down * 2f);
            MadMax.Audio.Sfx.Play("dig", tip, 0.7f, Random.Range(0.8f, 1f), 40f, 0.2f);
            held = 0f;
        }

        /// <summary>Where a container takes a load: a tipper's bed centre, else the container itself.</summary>
        static Vector3 LoadPoint(Container c)
        {
            var m = c.GetComponent<Machine>();
            var bed = m && m.store == c ? m.Tool : null;
            return bed ? bed.TransformPoint(new Vector3(0, 6, 24) * S) : c.transform.position;
        }

        /// <summary>Soil clods: physical pixel cubes in the soil's colour (pooled debris).</summary>
        static void Clods(Vector3 at, ResourceType type, int n, float spread, Vector3 impulse)
        {
            var ds = DebrisSystem.Instance;
            if (!ds || n <= 0 || type == ResourceType.None) return;
            Color32 c = ResourceInfo.Color(type);
            clods.Clear();
            for (int i = 0; i < n; i++)
            {
                float f = i % 3 == 0 ? 0.8f : i % 3 == 1 ? 1f : 1.12f;                   // three shades: few cached cube meshes
                clods.Add(new DebrisSystem.Chunk { position = at + Random.insideUnitSphere * spread, color = new Color32((byte)Mathf.Min(255f, c.r * f), (byte)Mathf.Min(255f, c.g * f), (byte)Mathf.Min(255f, c.b * f), 255) });
            }
            ds.Emit(clods, 0.07f, impulse, 0.6f);
        }

        void FixedUpdate()
        {
            if (!Simulated) return;
            var terrain = DeformableTerrain.Instance;
            if (!terrain) return;
            float dt = Time.fixedDeltaTime;
            float moved = Vector3.Distance(transform.position, lastPos);
            lastPos = transform.position;
            travel += moved;
            float speed = driver ? Mathf.Abs(driver.ForwardSpeed) : 0f;
            switch (kind)
            {
                case Kind.Dozer:
                {
                    var e = BladeEdge();
                    if (!e.HasValue || speed < 0.3f || travel < 0.25f) break;
                    travel = 0f;
                    var blade = e.Value;
                    float ground = terrain.Height(blade.x, blade.z);
                    if (blade.y > ground - 0.01f) break;                                     // blade above the ground: nothing to cut
                    float cut = Terraform(DeformableTerrain.TerraOp.Flatten, blade, 1.6f, blade.y);
                    if (cut > 0f) load += cut;
                    if (load > 1.2f)
                    {
                        Clods(blade + transform.forward * 1.2f + Vector3.up * 0.6f, loadType, 8, 1f, transform.forward * 1.5f);
                        // an angled blade rolls the pile off its trailing end; square, it piles up ahead
                        var bladeT = Seg(Tool, "blade");
                        var spill = Mathf.Abs(bladeAngle) > 8f && bladeT ? blade + bladeT.right * Mathf.Sign(bladeAngle) * 1.8f + transform.forward * 0.6f : blade + transform.forward * 1.6f;
                        Terraform(DeformableTerrain.TerraOp.Dump, new Vector3(spill.x, terrain.Height(spill.x, spill.z), spill.z), 1.3f, load / (Mathf.PI * 1.69f * 0.45f));
                        load = 0f;
                    }
                    var soil = terrain.SoilAt(blade.x, blade.z, 0f);
                    Fx.Smoke(blade + Vector3.up * 0.4f, transform.forward + Vector3.up, 0.8f, DustColor(soil), 1.2f);
                    if (cut > 0f) { loadType = soil; Clods(blade + Vector3.up * 0.5f + transform.forward * 0.5f, soil, 4, 0.9f, transform.forward * (speed + 1f)); }
                    break;
                }
                case Kind.DumpTruck:
                    if (bed < 35f || (tick -= dt) > 0f) break;
                    tick = 0.5f;
                    Unload(terrain);
                    break;
                case Kind.Paver:
                    if (!active || speed < 0.15f || speed > 4f || travel < 0.45f || !Tool) break;
                    travel = 0f;
                    {
                        var mat = concrete ? ResourceType.Concrete : ResourceType.Asphalt;
                        if (!store.inventory.TrySpend(mat, 1)) { active = false; break; }
                        var screedAt = Tool.TransformPoint(new Vector3(0, 0, -4) * S);
                        Terraform(DeformableTerrain.TerraOp.Pave, screedAt, 1.55f, 0f, (byte)(concrete ? 2 : 1));
                        Fx.Smoke(screedAt + Vector3.up * 0.3f, Vector3.up * 0.6f, 0.5f, new Color(0.35f, 0.33f, 0.32f, 0.5f), 1.5f);
                    }
                    break;
                case Kind.Roller:
                    if (speed < 0.2f || travel < 0.3f) break;
                    travel = 0f;
                    foreach (int z in new[] { 20, -20 })
                    {
                        var drum = transform.TransformPoint(new Vector3(0, 0, z) * S);
                        Terraform(DeformableTerrain.TerraOp.Roll, new Vector3(drum.x, terrain.Height(drum.x, drum.z), drum.z), 1.3f, 0f);
                    }
                    break;
            }
        }

        void Unload(DeformableTerrain terrain)
        {
            var inv = store.inventory;
            ResourceType pick = ResourceType.None;
            for (int t = 1; t < ResourceInfo.Count; t++) if (inv.Get((ResourceType)t) > 0 && !ResourceInfo.IsFluid((ResourceType)t)) { pick = (ResourceType)t; break; }
            if (pick == ResourceType.None) return;
            int n = Mathf.Min(10, inv.Get(pick));
            inv.TrySpend(pick, n);
            var behind = transform.TransformPoint(new Vector3(0, 0, -56) * S);
            Container target = null; float best = 4f;
            foreach (var c in Container.All)
            {
                if (!c || c == store || c.GetComponentInParent<VehicleDriver>()) continue;
                float d = Vector3.Distance(c.transform.position, behind);
                if (d < best) { best = d; target = c; }
            }
            if (target) target.inventory.Add(pick, n);
            else Terraform(DeformableTerrain.TerraOp.Dump, new Vector3(behind.x, terrain.Height(behind.x, behind.z), behind.z), 1.4f, n / UnitsPerM3 / (Mathf.PI * 1.96f * 0.45f));
            Fx.Smoke(behind + Vector3.up * 0.5f, Vector3.down, 0.9f, DustColor(pick), 1.5f);
            Clods(transform.TransformPoint(new Vector3(0, 10, -50) * S), pick, 8, 0.6f, -transform.forward * 1.5f + Vector3.down);
            MadMax.Audio.Sfx.Play("dig", behind, 0.6f, Random.Range(0.8f, 1f), 40f, 0.3f);
        }

        static void Pose(Transform t, float pitch, float yaw = 0f) { if (t) t.localRotation = Quaternion.Euler(pitch, yaw, 0f); }

        Material HeapMaterial()
        {
            var body = transform.Find("Body");
            return body && body.TryGetComponent<MeshRenderer>(out var r) ? r.sharedMaterial : null;
        }

        /// <summary>The heap on <paramref name="seg"/> (re-made when the tool was swapped).</summary>
        SoilHeap HeapOn(ref SoilHeap h, Transform seg, Vector3 at, SoilHeap.Shape shape, Vector3Int size)
        {
            if (!seg) { if (h) h.Set(0f, ResourceType.None); return null; }
            if (!h || h.transform.parent != seg) { if (h) Destroy(h.gameObject); h = SoilHeap.Create(seg, at, shape, size, HeapMaterial()); }
            return h;
        }

        /// <summary>Loads you can see: bucket and blade heaps, the tipper bed's contents, clods spilling off a full bucket.</summary>
        void Heaps()
        {
            var tool = Tool;
            switch (kind)
            {
                case Kind.Excavator:
                    HeapOn(ref heap, Seg(tool, "bucket"), new Vector3(0, -5.2f, -1.5f), SoilHeap.Shape.Bucket, new Vector3Int(4, 3, 3))?.Set(load / Capacity, loadType);
                    break;
                case Kind.Backhoe:
                    HeapOn(ref heap, Seg(tool, "bucket"), new Vector3(0, -3.6f, 0.4f), SoilHeap.Shape.Bucket, new Vector3Int(11, 3, 3))?.Set(load / Capacity, loadType);
                    HeapOn(ref rearHeap, Seg(Rear, "bucket"), new Vector3(0, -4f, 1f), SoilHeap.Shape.Bucket, new Vector3Int(3, 2, 2))?.Set(rearLoad / RearCapacity, rearType);
                    break;
                case Kind.Dozer:
                    HeapOn(ref heap, Seg(tool, "blade"), new Vector3(0, 0, 8f), SoilHeap.Shape.Blade, new Vector3Int(16, 7, 5))?.Set(load / 1.2f, loadType);
                    break;
                case Kind.DumpTruck:
                    if ((bedScan -= Time.deltaTime) <= 0f && store)
                    {
                        bedScan = 0.5f; float units = 0f, best = 0f; bedType = ResourceType.None;
                        for (int t = 1; t < ResourceInfo.Count; t++)
                        {
                            var rt = (ResourceType)t;
                            int n = store.inventory.Get(rt);
                            if (n <= 0 || ResourceInfo.IsFluid(rt)) continue;
                            units += n;
                            if (n > best) { best = n; bedType = rt; }
                        }
                        bedFill = units / (UnitsPerM3 * BedM3);
                    }
                    HeapOn(ref bedHeap, tool, new Vector3(0, 2f, 24f), SoilHeap.Shape.Bed, new Vector3Int(13, 8, 22))?.Set(bedFill, bedType);
                    break;
            }
            // a heaped bucket sheds clods while it swings or the machine drives
            float full = Capacity > 0f ? load / Capacity : 0f;
            if (heap && heap.gameObject.activeInHierarchy && full > 0.55f && (trickle -= Time.deltaTime) <= 0f)
            {
                float speed = driver ? Mathf.Abs(driver.ForwardSpeed) : 0f;
                trickle = Mathf.Lerp(1.2f, 0.25f, Mathf.Clamp01(speed / 4f)) / full;
                if (speed > 0.8f || Random.value < 0.3f) Clods(heap.transform.position + Vector3.up * 0.1f, loadType, 1 + (full > 0.9f ? 1 : 0), 0.3f, Vector3.zero);
            }
        }

        void LateUpdate()
        {
            Heaps();
            var tool = Tool;
            switch (kind)
            {
                case Kind.Excavator:
                    if (tool) tool.localRotation = Quaternion.Euler(0f, slew, 0f);
                    Pose(Seg(tool, "boom"), boom); Pose(Seg(tool, "stick"), stick); Pose(Seg(tool, "bucket"), bucket);
                    break;
                case Kind.Backhoe:
                    Pose(Seg(tool, "arms"), arms); Pose(Seg(tool, "bucket"), tilt);
                    var rear = Rear;
                    Pose(Seg(rear, "swing"), 0f, hoeSwing); Pose(Seg(rear, "boom"), hoeBoom); Pose(Seg(rear, "stick"), hoeStick); Pose(Seg(rear, "bucket"), hoeBucket);
                    break;
                case Kind.Dozer:
                    Pose(Seg(tool, "lift"), bladeLift); Pose(Seg(tool, "blade"), bladePitch, bladeAngle);
                    break;
                case Kind.DumpTruck:
                    if (tool) tool.localRotation = Quaternion.Euler(-bed, 0f, 0f);
                    break;
                case Kind.Paver:
                    if (tool) tool.localRotation = Quaternion.Euler(screed, 0f, 0f);
                    break;
            }
        }
    }
}
