using System.Collections.Generic;
using MadMax.Audio;
using MadMax.Rendering;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Small life on a placed piece: named moving parts follow the piece's state. Parts are the HD model's
    /// children named after a <see cref="Kind"/> (<c>Lid</c>, <c>Flame</c>, <c>Flywheel</c>, <c>Flag</c>..., also
    /// <c>Lid_L</c> / <c>Flame2</c>; their Blender origin is the pivot) or, in the voxel look, parts cut from the
    /// template by <see cref="VoxelCuts"/>. State comes from the piece's own components: a <see cref="Container"/> shown
    /// in the loot window lifts its lid, a running <see cref="Generator"/> / busy <see cref="CraftingStation"/> spins
    /// wheels and pistons, a lit fire flickers flames and a warm light, the wind (<see cref="MadMax.World.WindDust"/>)
    /// waves flags, awnings, washing and hanging lamps. Other code (the player's timed use) calls <see cref="Trigger"/>.
    /// Added by <see cref="Placeable"/> only to pieces with something to move; skipped beyond <see cref="Reach"/> m of the
    /// focus; nothing allocates per frame.</summary>
    public partial class PieceMotion : MonoBehaviour
    {
        public enum Kind
        {
            Lid, Door, Lever, Handle, Knob, Needle, Dial, Wheel, Flywheel, Belt, Piston, Blade, Pendulum, Bell, Clapper,
            Flag, Awning, Curtain, Laundry, Sign, Lamp, Flame, Embers,
        }

        public const float Reach = 40f;

        public static readonly List<PieceMotion> All = new List<PieceMotion>();
        static Vector3 focus; static int focusFrame = -1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); focusFrame = -1; cuts.Clear(); flywheelMesh = flywheelHD = null; }
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        sealed class Part
        {
            public Kind kind;
            public string name;
            public Transform[] t;
            public Quaternion[] rot;
            public Vector3[] pos, scale;
            public Renderer[] rends;
            public Vector3 axis, thin, longAxis, up, fwd, right, c;
            public float a, len, phase, sway, glow = -1f, shown = float.NaN;
            public bool voxel, swayTip;
        }

        readonly List<Part> parts = new List<Part>();
        Placeable piece;
        HDVisual hd;
        Vector3 hdRest;
        bool discovered;
        int startFrame;

        // state sources (cached once)
        Container box; Generator gen; CraftingStation station; Climate climate; RadioReceiver radio; TvSet tv;
        LightSwitch lightSwitch; UtilitySwitch utilSwitch; PowerBreaker breaker; HandPump handPump; TransferPump transfer;
        WaterTap tap; WaterOutlet outlet; AlarmBell bell; AirCompressor compressor; SnareTrap snare; UtilityNode node;

        float triggerUntil, triggerAt = -99f; string trigger;
        float run, phase, ringAt = -99f, lastRung = -1f;
        bool wasOpen, stationFire, stationMachine;

        // ------------------------------------------------------------------ API

        /// <summary>The motion component of a piece (null when it has nothing to move).</summary>
        public static PieceMotion Of(GameObject go) => go && go.TryGetComponent<PieceMotion>(out var m) ? m : null;

        /// <summary>Play a timed use on a piece: <c>open</c> (lid / door), <c>pump</c> / <c>crank</c> (handle strokes,
        /// wheels turn), <c>turn</c> (handle, knob or valve wheel a quarter turn), <c>ring</c> (bell).</summary>
        public static void Use(GameObject piece, string kind, float seconds = 1.2f) { var m = Of(piece); if (m) m.Trigger(kind, seconds); }

        public void Trigger(string kind, float seconds)
        {
            trigger = kind; triggerAt = Time.time; triggerUntil = Time.time + Mathf.Max(0.2f, seconds);
            if (kind == "ring") ringAt = Time.time;
        }

        /// <summary>Its handle turns (a tap) rather than strokes (a pump).</summary>
        bool TurnHandle => (tap || outlet) && !handPump;

        bool Triggered(string kind) => trigger == kind && Time.time < triggerUntil;

        /// <summary>Current angle (degrees) of the part named <paramref name="name"/> or of kind <paramref name="name"/>
        /// with the largest swing; NaN when there is none (tests).</summary>
        public float Angle(string name)
        {
            float best = float.NaN;
            foreach (var p in parts)
                if (p.name == name || p.kind.ToString() == name)
                    best = float.IsNaN(best) || Mathf.Abs(p.a) > Mathf.Abs(best) ? p.a : best;
            return best;
        }

        /// <summary>The part's first transform (tests: watch it move).</summary>
        public Transform PartTransform(string name)
        {
            foreach (var p in parts) if ((p.name == name || p.kind.ToString() == name) && p.t.Length > 0) return p.t[0];
            return null;
        }

        /// <summary>Names of the parts found (tests, notes).</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts) sb.Append(p.name).Append(p.voxel ? "(vox) " : " ");
            return sb.ToString().Trim();
        }

        public bool Discovered => discovered;

        bool Idle => parts.Count == 0 && !fireCapable && !hasKettle && !dialGlow && !flickerLight && !radio && !(hd && hd.HasLamps);

        // ------------------------------------------------------------------ attach

        /// <summary>From <see cref="Placeable"/>.Start: add the component when the piece has anything to animate.</summary>
        public static void Attach(Placeable p)
        {
            if (!p || p.GetComponent<PieceMotion>()) return;
            var go = p.gameObject;
            bool any = go.GetComponent<Container>() || go.GetComponent<Generator>() || go.GetComponent<CraftingStation>()
                       || go.GetComponent<Climate>() || go.GetComponent<RadioReceiver>() || go.GetComponent<TvSet>()
                       || go.GetComponent<LightSwitch>() || go.GetComponent<UtilitySwitch>() || go.GetComponent<PowerBreaker>()
                       || go.GetComponent<HandPump>() || go.GetComponent<TransferPump>() || go.GetComponent<WaterTap>()
                       || go.GetComponent<WaterOutlet>() || go.GetComponent<AlarmBell>() || go.GetComponent<AirCompressor>()
                       || go.GetComponent<SnareTrap>() || go.GetComponent<Beehive>() || go.GetComponent<Latrine>()
                       || go.GetComponent<ClaimFlag>() || go.GetComponent<WallClock>() || HasCut(p.id)
                       || (go.TryGetComponent<PoweredLight>(out var pl) && !pl.needsPower)
                       || HasNamedParts(go.GetComponent<HDVisual>())
                       || (go.TryGetComponent<HDVisual>(out var v) && v.HasLamps && !go.GetComponent<PoweredLight>());   // windows that glow at night
            if (any) go.AddComponent<PieceMotion>();
        }

        static bool HasNamedParts(HDVisual v)
        {
            if (!v || !v.root) return false;
            foreach (var r in v.Renderers) if (r && TryKind(HDVisual.BaseName(r.name), out _)) return true;
            return false;
        }

        void Awake()
        {
            piece = GetComponent<Placeable>();
            box = GetComponent<Container>(); gen = GetComponent<Generator>(); station = GetComponent<CraftingStation>();
            climate = GetComponent<Climate>(); radio = GetComponent<RadioReceiver>(); tv = GetComponent<TvSet>();
            lightSwitch = GetComponent<LightSwitch>(); utilSwitch = GetComponent<UtilitySwitch>(); breaker = GetComponent<PowerBreaker>();
            handPump = GetComponent<HandPump>(); transfer = GetComponent<TransferPump>(); tap = GetComponent<WaterTap>();
            outlet = GetComponent<WaterOutlet>(); bell = GetComponent<AlarmBell>(); compressor = GetComponent<AirCompressor>();
            snare = GetComponent<SnareTrap>(); node = GetComponent<UtilityNode>();
            if (station)
            {
                stationFire = IsFireStation(station.type);
                stationMachine = IsMachineStation(station.type);
            }
            phase = Random.Range(0f, 100f);
        }

        void Start()
        {
            startFrame = Time.frameCount;
            hd = GetComponent<HDVisual>();
            AmbienceStart();
            if (!hd || !hd.root)                                                                // the voxel look: cut parts now
            {
                CutVoxels(); discovered = true;
                if (Idle) enabled = false;
            }
        }

        static bool IsFireStation(string t) => t == "stove" || t == "campfire" || t == "forge" || t == "furnace" || t == "kiln" || t == "smokehouse";
        static bool IsMachineStation(string t) => t == "loom" || t == "spinning" || t == "sawmill" || t == "saw_bench" || t == "press" || t == "feedmill"
                                                  || t == "mixer" || t == "washplant" || t == "machine_shop" || t == "rock_crusher" || t == "stamp_mill"
                                                  || t == "sewing" || t == "composter" || t == "cannery" || t == "desalinator";

        // ------------------------------------------------------------------ discovery (HD)

        static readonly string[] kindNames = System.Enum.GetNames(typeof(Kind));

        /// <summary>A moving-part name: a kind exactly, or a kind followed by a digit or '_' (not <c>Lamp_*</c>: those are
        /// the exporter's emissive splits).</summary>
        static bool TryKind(string n, out Kind kind)
        {
            kind = Kind.Lid;
            if (string.IsNullOrEmpty(n)) return false;
            for (int i = 0; i < kindNames.Length; i++)
            {
                var k = kindNames[i];
                if (!n.StartsWith(k, System.StringComparison.Ordinal)) continue;
                if (n.Length > k.Length)
                {
                    char ch = n[k.Length];
                    if (!(char.IsDigit(ch) || (ch == '_' && k != "Lamp"))) continue;
                }
                kind = (Kind)i;
                return true;
            }
            return false;
        }

        readonly Dictionary<string, List<Transform>> found = new Dictionary<string, List<Transform>>();

        void DiscoverHD()
        {
            discovered = true;
            if (!hd || !hd.root) return;
            hdRest = hd.root.localPosition;
            found.Clear();
            Collect(hd.root);
            foreach (var kv in found)
            {
                TryKind(kv.Key, out var kind);
                if (kind == Kind.Door && GetComponent<Door>()) continue;                    // the whole piece is the door
                AddPart(kind, kv.Key, kv.Value.ToArray(), false);
            }
            found.Clear();
            bool fly = false;
            foreach (var p in parts) if (p.kind == Kind.Flywheel) { fly = true; break; }
            if (gen && !fly) AddFlywheel(GetComponent<MeshFilter>(), GetComponent<MeshRenderer>());
        }

        void Collect(Transform t)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                var bn = HDVisual.BaseName(c.name);
                if (TryKind(bn, out _) && !HasVoxelHost(bn))
                {
                    if (!found.TryGetValue(bn, out var l)) found[bn] = l = new List<Transform>();
                    l.Add(c);
                    continue;                                                               // its children ride along
                }
                Collect(c);
            }
        }

        /// <summary>The piece's own (non-HD) object of that name exists: HDVisual binds the HD mover to it and the
        /// piece's code moves it (rotors, stamps, bulbs).</summary>
        bool HasVoxelHost(string n)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (hd && c == hd.root) continue;
                if (c.name == n && c.GetComponent<Renderer>()) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ parts

        Part AddPart(Kind kind, string name, Transform[] ts, bool voxel)
        {
            if (ts == null || ts.Length == 0) return null;
            var p = new Part { kind = kind, name = name, t = ts, voxel = voxel, phase = Random.Range(0f, 6.28f) };
            int n = ts.Length;
            p.rot = new Quaternion[n]; p.pos = new Vector3[n]; p.scale = new Vector3[n];
            for (int i = 0; i < n; i++) { p.rot[i] = ts[i].localRotation; p.pos[i] = ts[i].localPosition; p.scale[i] = ts[i].localScale; }
            var rl = new List<Renderer>();
            foreach (var t in ts) rl.AddRange(t.GetComponentsInChildren<Renderer>(true));
            p.rends = rl.ToArray();
            var t0 = ts[0];
            var inv = Quaternion.Inverse(t0.rotation);
            p.up = (inv * transform.up).normalized;
            p.fwd = (inv * transform.forward).normalized;
            p.right = (inv * transform.right).normalized;
            // shape: bounds of the part in its own space (mesh bounds where there is a mesh, else the world box)
            Bounds b = default; bool have = false;
            foreach (var r in p.rends)
            {
                if (!r) continue;
                Bounds lb;
                if (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh)
                {
                    var mb = mf.sharedMesh.bounds;
                    var rt = r.transform;
                    var cw = rt.TransformPoint(mb.center);
                    lb = new Bounds(t0.InverseTransformPoint(cw), rt == t0 ? mb.size : Abs(inv * (rt.rotation * Vector3.Scale(mb.size, rt.lossyScale))));
                }
                else lb = new Bounds(t0.InverseTransformPoint(r.bounds.center), Abs(inv * r.bounds.size));
                if (!have) { b = lb; have = true; } else b.Encapsulate(lb);
            }
            var size = have ? b.size : Vector3.one * 0.1f;
            p.c = have ? b.center : Vector3.zero;
            p.thin = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
            p.longAxis = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
            p.len = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            var ch = p.c - p.up * Vector3.Dot(p.c, p.up);                                    // pivot → centre, flat
            switch (kind)
            {
                case Kind.Lid: case Kind.Awning:
                    p.axis = ch.sqrMagnitude > 1e-4f ? Vector3.Cross(ch, p.up).normalized : p.right;   // +angle lifts the far edge
                    if (ch.sqrMagnitude <= 1e-4f && kind == Kind.Lid) p.axis = -p.right;           // centred pivot: lift the front
                    break;
                case Kind.Door:
                {
                    p.axis = p.up;
                    float s = Vector3.Dot(Vector3.Cross(p.up, ch), p.fwd);                         // swing out to the front
                    p.sway = s >= 0f ? 1f : -1f;
                    break;
                }
                case Kind.Lever: p.axis = p.right; break;
                case Kind.Bell: case Kind.Clapper: p.axis = p.right; break;
                case Kind.Pendulum: p.axis = PieceThinAxis(t0); break;
                case Kind.Handle: p.axis = TurnHandle ? p.up : p.right; break;
                default: p.axis = p.thin; break;
            }
            if (kind == Kind.Flag || kind == Kind.Curtain || kind == Kind.Laundry || kind == Kind.Awning) p.swayTip = SetSwayTip(p);
            parts.Add(p);
            return p;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>The piece's flattest axis (a wall clock's face normal) in the part's space.</summary>
        Vector3 PieceThinAxis(Transform t0)
        {
            var mf = GetComponent<MeshFilter>();
            var s = mf && mf.sharedMesh ? mf.sharedMesh.bounds.size : Vector3.one;
            var a = s.x <= s.y && s.x <= s.z ? transform.right : s.y <= s.z ? transform.up : transform.forward;
            return (Quaternion.Inverse(t0.rotation) * a).normalized;
        }

        static readonly int SwayTipId = Shader.PropertyToID("_SwayTip"), LampOnId = Shader.PropertyToID("_LampOn");
        static MaterialPropertyBlock mpb;

        /// <summary>Cloth whose mesh carries wind weights in its vertex alpha bends in the shader as well.</summary>
        static bool SetSwayTip(Part p)
        {
            bool any = false;
            foreach (var r in p.rends)
            {
                if (!r || !r.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh || !mf.sharedMesh.isReadable) continue;
                var cols = mf.sharedMesh.colors32;
                bool weighted = false;
                for (int i = 0; i < cols.Length; i++) if (cols[i].a < 250) { weighted = true; break; }
                if (!weighted) continue;
                mpb ??= new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(SwayTipId, 0.05f);
                r.SetPropertyBlock(mpb);
                any = true;
            }
            return any;
        }

        // ------------------------------------------------------------------ per frame

        static Vector3 Focus
        {
            get
            {
                if (focusFrame == Time.frameCount) return focus;
                focusFrame = Time.frameCount;
                var g = MadMax.Game.WastelandGame.Instance;
                if (g && (g.Current || g.Player)) focus = g.FocusPos;
                else { var cam = Camera.main; focus = cam ? cam.transform.position : Vector3.zero; }
                return focus;
            }
        }

        public bool Near => (transform.position - Focus).sqrMagnitude < Reach * Reach;

        float check;

        void Update()
        {
            if (!discovered && Time.frameCount - startFrame > 65)                              // after HDVisual bound its movers
            {
                DiscoverHD();
                if (Idle) { enabled = false; return; }                                          // nothing to move or light: no cost
            }
            AmbienceTick();
            if (!Near || parts.Count == 0) return;
            float dt = Time.deltaTime, udt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);   // lids and doors move while the loot window pauses time
            if ((check -= udt) <= 0f) { check = 0.5f; CheckVoxelMesh(); Shelter(); }
            float time = Time.time;
            var wind = MadMax.World.Fx.Wind; wind.y = 0f;
            float gust = MadMax.World.WindDust.Gust;

            bool open = Opened();
            if (box && open != wasOpen && HasHinged()) OpenSound(open);
            wasOpen = open;
            float runTarget = Running() ? 1f : 0f;
            run = Mathf.MoveTowards(run, runTarget, dt * (runTarget > run ? 0.8f : 0.45f));          // spin up, coast down
            phase += dt * run * 12.566f;                                                              // 2 rev/s at full speed
            bool pumping = Triggered("pump") || Triggered("crank") || (transfer && transfer.mode != TransferPump.Mode.Off && node && node.Powered);
            bool lit = FlameLit;
            float ring = RingAge();

            foreach (var p in parts)
            {
                switch (p.kind)
                {
                    case Kind.Lid:
                    {
                        float target = Opened(p) ? 78f : 0f;
                        p.a = Mathf.MoveTowards(p.a, target, udt * (target > p.a ? 220f : 300f));
                        Hinge(p, p.a);
                        break;
                    }
                    case Kind.Door:
                    {
                        float target = Opened(p) ? 100f * p.sway : 0f;
                        p.a = Mathf.MoveTowards(p.a, target, udt * 240f);
                        Hinge(p, p.a);
                        break;
                    }
                    case Kind.Lever:
                        p.a = Mathf.MoveTowards(p.a, LeverTarget(), udt * 470f);                       // ~0.15 s end to end
                        Hinge(p, p.a);
                        break;
                    case Kind.Handle:
                    {
                        if (TurnHandle)
                        {
                            float target = (tap && tap.running) || Triggered("turn") || Triggered("pump") ? 90f : 0f;
                            p.a = Mathf.MoveTowards(p.a, target, dt * 400f);
                        }
                        else if (pumping) p.a = 24f * Mathf.Sin((time - triggerAt) * 8.2f + (transfer ? p.phase : 0f));
                        else p.a = Mathf.MoveTowards(p.a, 0f, dt * 120f);
                        Hinge(p, p.a);
                        break;
                    }
                    case Kind.Knob:
                        p.a = Mathf.MoveTowards(p.a, KnobTarget(), dt * 360f);
                        Hinge(p, p.a);
                        break;
                    case Kind.Needle:
                    {
                        float target = NeedleTarget(time);
                        p.a = Mathf.Lerp(p.a, target, 1f - Mathf.Exp(-dt * 6f));
                        Hinge(p, p.a);
                        break;
                    }
                    case Kind.Dial:
                        if (radio)
                        {
                            int n = MadMax.Audio.RadioNetwork.Instance ? Mathf.Max(1, MadMax.Audio.RadioNetwork.Instance.StationCount) : 1;
                            p.a = Mathf.MoveTowardsAngle(p.a, radio.on ? radio.station * 300f / n : 0f, dt * 200f);
                            Hinge(p, p.a);
                        }
                        break;
                    case Kind.Wheel:
                        if (utilSwitch)
                        {
                            p.a = Mathf.MoveTowards(p.a, utilSwitch.on ? 0f : 270f, dt * 600f);   // a closed valve wound shut
                            Hinge(p, p.a);
                        }
                        else
                        {
                            float speed = (pumping ? 260f : 0f) + run * 200f;
                            if (speed > 0f) { p.a = Mathf.Repeat(p.a + speed * dt, 360f); Hinge(p, p.a); }
                        }
                        break;
                    case Kind.Flywheel:
                        if (run > 0f || pumping) { p.a = Mathf.Repeat(p.a + (run * 720f + (pumping ? 300f : 0f)) * dt, 360f); Hinge(p, p.a); }
                        break;
                    case Kind.Blade:
                        if (run > 0f) { p.a = Mathf.Repeat(p.a + run * 1500f * dt, 360f); Hinge(p, p.a); }
                        break;
                    case Kind.Belt:
                        if (run > 0.01f) Offset(p, p.thin * (Mathf.Sin(time * 47f + p.phase) * 0.0015f * run));
                        break;
                    case Kind.Piston:
                        if (run > 0.01f || pumping)
                        {
                            float amp = Mathf.Min(0.05f, p.len * 0.12f);
                            float ph = pumping && run < 0.01f ? (time - triggerAt) * 8.2f : phase;
                            Offset(p, p.longAxis * (Mathf.Sin(ph + p.phase) * amp));
                        }
                        break;
                    case Kind.Pendulum:
                        p.a = 9f * Mathf.Sin(time * 3.93f + p.phase);
                        Hinge(p, p.a);
                        break;
                    case Kind.Bell:
                        p.a = ring;
                        Hinge(p, p.a);
                        break;
                    case Kind.Clapper:
                        p.a = RingAge(-0.9f) * 1.3f;
                        Hinge(p, p.a);
                        break;
                    case Kind.Flag: Flag(p, wind, gust, time, dt); break;
                    case Kind.Awning:
                    {
                        float s = wind.magnitude * p.sway;
                        p.a = Mathf.Clamp(s * 0.9f, 0f, 6f) * (0.55f + 0.45f * Mathf.Sin(time * (2.6f + s * 0.4f) + p.phase))
                              + gust * 2.5f * (Mathf.PerlinNoise(time * 3f, p.phase) - 0.3f);
                        Hinge(p, p.a);
                        break;
                    }
                    case Kind.Curtain: case Kind.Laundry: case Kind.Sign: case Kind.Lamp: Hang(p, wind, gust, time); break;
                    case Kind.Flame: Flame(p, lit, time, dt); break;
                    case Kind.Embers: Embers(p, time); break;
                }
            }
            if (hd && hd.root && gen)                                                                  // a running engine shakes on its mounts
                hd.root.localPosition = run > 0.01f ? hdRest + new Vector3(Mathf.Sin(time * 61f), Mathf.Sin(time * 73f + 1f), Mathf.Sin(time * 67f + 2f)) * (0.0015f * run) : hdRest;
        }

        // ------------------------------------------------------------------ state

        /// <summary>Shown in the loot window right now (the vehicle compartment rule), or held open by a trigger.</summary>
        public bool Opened()
        {
            if (Triggered("open")) return true;
            if (snare) return snare.caught == null;                                             // a set trap stands open
            if (!box) return false;
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g || !g.Menus || !g.Menus.IsOpen || g.Menus.Current != MadMax.Game.MenuSystem.Page.Container) return false;
            var cur = g.Menus.LootCurrent;
            return cur != null && cur.box == box;
        }

        bool Opened(Part p) => Opened() || (p.kind == Kind.Lid && Triggered("lid"));

        bool HasHinged()
        {
            foreach (var p in parts) if (p.kind == Kind.Lid || p.kind == Kind.Door) return true;
            return false;
        }

        void OpenSound(bool open)
        {
            bool door = false;
            foreach (var p in parts) if (p.kind == Kind.Door) { door = true; break; }
            var at = transform.position + transform.up * 0.5f;
            if (door) MadMax.Audio.Sfx.Play(open ? "door_open" : "door_close", at, 0.45f, open ? 1.15f : 1.05f, 14f, 0.2f);
            else if (open) MadMax.Audio.Sfx.Play("chest", at, 0.5f, Random.Range(0.95f, 1.08f), 14f, 0.2f);
            else MadMax.Audio.Sfx.Play("creak", at, 0.35f, Random.Range(1.2f, 1.4f), 12f, 0.2f);
        }

        bool Running()
        {
            if (gen) return gen.on && gen.fuel > 0f;
            if (station && stationMachine) return station.Busy && station.Powered;
            if (transfer) return transfer.mode != TransferPump.Mode.Off && node && node.Powered;
            if (compressor) return Triggered("pump") || Triggered("use");
            return Triggered("crank");
        }

        float LeverTarget()
        {
            if (lightSwitch) return lightSwitch.on ? 35f : -35f;
            if (utilSwitch) return utilSwitch.on ? 35f : -35f;
            if (breaker) return (1 - breaker.priority) * 35f;
            if (gen) return gen.on ? 30f : -30f;
            if (transfer) return transfer.mode == TransferPump.Mode.In ? 30f : transfer.mode == TransferPump.Mode.Out ? -30f : 0f;
            return Triggered("turn") || Triggered("use") ? 35f : 0f;
        }

        float KnobTarget()
        {
            if (radio) return radio.on ? -135f + 270f * radio.volume : -150f;
            if (tv) return tv.Playing != null ? 45f : -45f;
            if (gen) return gen.on ? 60f : 0f;
            if (climate && !climate.burnsWood) return climate.on ? 90f : 0f;
            return Triggered("turn") || Triggered("use") ? 90f : 0f;
        }

        float NeedleTarget(float time)
        {
            if (radio)
            {
                var net = MadMax.Audio.RadioNetwork.Instance;
                int n = net ? net.StationCount : 0;
                return radio.on && n > 1 ? Mathf.Lerp(-60f, 60f, radio.station / (float)(n - 1)) : -70f;
            }
            if (gen && node) return gen.on && gen.fuel > 0f ? Mathf.Lerp(-50f, 50f, Mathf.Clamp01(node.load / Mathf.Max(1f, gen.output))) + Mathf.Sin(time * 9f) * 1.5f : -60f;
            if (node && node.waterCapacity > 0f) return Mathf.Lerp(-60f, 60f, Mathf.Clamp01(node.Water / node.waterCapacity));
            if (FireLit) return 40f + Mathf.Sin(time * 2f + phase) * 3f;                          // a hot oven's thermometer
            return Running() ? 30f : -60f;
        }

        /// <summary>Bell swing (degrees) after the last ring: three strokes, then a damped settle.</summary>
        float RingAge(float lag = 0f)
        {
            if (bell && bell.LastRung >= 0f && bell.LastRung != lastRung) { lastRung = bell.LastRung; ringAt = bell.LastRung; }
            float dt = Time.time - ringAt + lag;
            if (dt < 0f || dt > 7f) return 0f;
            float amp = dt < 1.65f ? 22f : 22f * Mathf.Exp(-(dt - 1.65f) * 1.3f);
            return amp * Mathf.Sin(dt * 5.71f);
        }

        // ------------------------------------------------------------------ motions

        static void Hinge(Part p, float angle)
        {
            if (angle == p.shown) return;                                                       // at rest: no transform writes
            p.shown = angle;
            var q = Quaternion.AngleAxis(angle, p.axis);
            for (int i = 0; i < p.t.Length; i++)
            {
                var t = p.t[i];
                if (!t) continue;
                t.localRotation = p.rot[i] * q;
            }
        }

        static void Offset(Part p, Vector3 local)
        {
            for (int i = 0; i < p.t.Length; i++)
            {
                var t = p.t[i];
                if (!t) continue;
                t.localPosition = p.pos[i] + p.rot[i] * Vector3.Scale(p.scale[i], local);
            }
        }

        /// <summary>The wind in the part's rest frame.</summary>
        static Vector3 WindLocal(Part p, Vector3 wind)
        {
            var t = p.t[0];
            if (!t) return Vector3.zero;
            var parent = t.parent ? t.parent.rotation : Quaternion.identity;
            return Quaternion.Inverse(parent * p.rot[0]) * wind;
        }

        static void Flag(Part p, Vector3 wind, float gust, float time, float dt)
        {
            var wl = WindLocal(p, wind);
            wl -= p.up * Vector3.Dot(wl, p.up);
            float s = wl.magnitude;
            var cp = p.c - p.up * Vector3.Dot(p.c, p.up);
            if (s > 0.25f && cp.sqrMagnitude > 1e-4f) p.a = Mathf.MoveTowardsAngle(p.a, Vector3.SignedAngle(cp, wl, p.up), dt * 70f);
            float k = Mathf.Clamp01(s / 4f);
            float flutter = (5f + 5f * gust) * k * Mathf.Sin(time * (4.5f + s * 0.7f) + p.phase) + (Mathf.PerlinNoise(time * 2.3f, p.phase) - 0.5f) * 8f * k;
            float droop = (1f - Mathf.Clamp01(s / 3f)) * 28f;
            var lift = cp.sqrMagnitude > 1e-4f ? Vector3.Cross(cp, p.up).normalized : p.right;
            var q = Quaternion.AngleAxis(p.a + flutter, p.up) * Quaternion.AngleAxis(-droop, lift);
            for (int i = 0; i < p.t.Length; i++) if (p.t[i]) p.t[i].localRotation = p.rot[i] * q;
        }

        /// <summary>Hanging things (washing, curtains, signs, lamps) lean downwind and rock with the gusts; sheltered
        /// ones (a roof above) barely move.</summary>
        static void Hang(Part p, Vector3 wind, float gust, float time)
        {
            var wl = WindLocal(p, wind);
            wl -= p.up * Vector3.Dot(wl, p.up);
            float s = wl.magnitude * p.sway;
            float gain = p.kind == Kind.Laundry ? 1.5f : p.kind == Kind.Sign ? 0.6f : p.kind == Kind.Lamp ? 0.7f : 1f;
            float lean = Mathf.Min(s * 2.2f, 14f) * (0.6f + 0.4f * Mathf.Sin(time * 1.7f + p.phase)) + gust * 4f * Mathf.Sin(time * 6.3f + p.phase) * Mathf.Clamp01(s);
            var axis = s > 0.05f ? Vector3.Cross(wl, p.up).normalized : p.right;
            float idle = p.kind == Kind.Lamp ? 0.8f * Mathf.Sin(time * 1.3f + p.phase) : 0f;
            p.a = lean * gain;
            var q = Quaternion.AngleAxis(p.a, axis) * Quaternion.AngleAxis(idle, p.right);
            for (int i = 0; i < p.t.Length; i++) if (p.t[i]) p.t[i].localRotation = p.rot[i] * q;
        }

        void Flame(Part p, bool lit, float time, float dt)
        {
            p.sway = Mathf.MoveTowards(p.sway, lit ? 1f : 0f, dt * (lit ? 0.7f : 1.5f));              // catches, dies down
            float n1 = Mathf.PerlinNoise(time * 7.3f, p.phase), n2 = Mathf.PerlinNoise(p.phase, time * 9.1f);
            float h = Mathf.Max(0.001f, p.sway * (0.85f + 0.32f * n1));
            float w = Mathf.Max(0.001f, p.sway * (0.92f + 0.14f * n2));
            for (int i = 0; i < p.t.Length; i++)
            {
                var t = p.t[i];
                if (!t) continue;
                var s = p.scale[i];
                t.localScale = new Vector3(s.x * w, s.y * h, s.z * w);
            }
            Glow(p, p.sway > 0.01f ? (0.7f + 0.7f * n1) * p.sway : 0f);
        }

        void Embers(Part p, float time) => Glow(p, (fireCapable ? emberHeat : FlameLit ? 0.7f : 0f) * (0.35f + 0.45f * Mathf.PerlinNoise(time * 1.4f, p.phase)));

        /// <summary>Emission of a part's renderers (HDLit <c>_LampOn</c>, kept on top of any cutaway block).</summary>
        static void Glow(Part p, float v)
        {
            if (v == 0f && p.glow == 0f) return;                                                    // dark stays dark without touching the block
            p.glow = v;
            mpb ??= new MaterialPropertyBlock();
            foreach (var r in p.rends)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(LampOnId, v);
                r.SetPropertyBlock(mpb);
            }
        }

        float shelterT;
        /// <summary>Sheltered (a roof within 6 m above): wind-driven parts barely move. Re-read every 10 s.</summary>
        void Shelter()
        {
            if ((shelterT -= 0.5f) > 0f) return;
            shelterT = 10f;
            bool any = false;
            foreach (var p in parts) if (p.kind >= Kind.Flag && p.kind <= Kind.Lamp) { any = true; break; }
            if (!any) return;
            float f = 1f;
            var from = transform.position + Vector3.up * 0.3f;
            var r = GetComponent<Renderer>();
            if (r) from.y = r.bounds.max.y + 0.05f;
            if (Physics.Raycast(from, Vector3.up, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform)) f = 0.12f;
            foreach (var p in parts) if (p.kind >= Kind.Flag && p.kind <= Kind.Lamp && p.kind != Kind.Flag) p.sway = f;
        }
    }
}
