using System.Collections.Generic;
using MadMax.Rendering;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Life on an HD world building or stall (<see cref="HDProp"/>): curtains, washing, signs, flags, awnings and
    /// hanging lanterns sway in the wind (<see cref="Fx.Wind"/>); lanterns and string lights (<c>Lamp</c>, <c>Lamp2</c>...)
    /// light up after dusk with one point light through <see cref="LightBudget"/>; window panes glow at night and the
    /// <c>Chimney</c> smokes on cold days and evenings, both only while the place is lived in (shops and stalls always,
    /// houses inside a settlement by a per-position hash: some stand empty). Which children move is found once per
    /// template and cached; props beyond <see cref="Reach"/> m of the focus do nothing.</summary>
    public class Ambience : MonoBehaviour
    {
        public const float Reach = 60f;

        enum Kind { Curtain, Laundry, Sign, Lamp, Flag, Awning }
        static readonly string[] kindNames = System.Enum.GetNames(typeof(Kind));

        /// <summary>What a template's HD model has, as child index paths (the same for every instance).</summary>
        sealed class Plan
        {
            public readonly List<(Kind kind, int[][] paths)> groups = new List<(Kind, int[][])>();
            public int[] chimney;
            public bool Any => groups.Count > 0 || chimney != null;
        }

        sealed class Part
        {
            public Kind kind;
            public Transform[] t;
            public Quaternion[] rot;
            public Vector3 up, right;
            public float phase, gain;
        }

        static readonly Dictionary<string, Plan> plans = new Dictionary<string, Plan>();
        public static readonly List<Ambience> All = new List<Ambience>();
        static Vector3 focus; static int focusFrame = -1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { plans.Clear(); All.Clear(); focusFrame = -1; }
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string templateId;
        /// <summary>Lived in: windows glow at night, lanterns burn, the chimney smokes.</summary>
        public bool inhabited;

        readonly List<Part> parts = new List<Part>();
        HDVisual vis;
        Transform chimney;
        Vector3 lampAt; bool hasLamp;
        Light lampLight;
        float smokeT, phase;

        /// <summary>The template has moving or glowing parts (tests).</summary>
        public bool HasParts => parts.Count > 0;
        public bool HasLamp => hasLamp;
        /// <summary>Its lanterns / windows burn right now.</summary>
        public bool LampLit => inhabited && DayNight.Darkness > 0.35f;
        public Light LampLight => lampLight;

        /// <summary>First transform of a part kind ("Curtain", "Laundry", "Lamp"...), for tests.</summary>
        public Transform PartOf(string kind)
        {
            foreach (var p in parts) if (p.kind.ToString() == kind && p.t.Length > 0) return p.t[0];
            return null;
        }

        // ------------------------------------------------------------------ attach

        /// <summary>From <see cref="HDProp.Attach"/>: give the prop its ambience when its template has any (or its
        /// windows need the lived-in rule).</summary>
        public static void Attach(DestructibleVoxels d, HDVisual v, string templateId)
        {
            if (!d || !v || !v.root || string.IsNullOrEmpty(templateId)) return;
            var plan = PlanFor(templateId, v.root);
            bool town = IsBuilding(templateId);
            if (!plan.Any && !(v.HasLamps && town)) return;
            var a = d.gameObject.AddComponent<Ambience>();
            a.templateId = templateId;
            a.vis = v;
            a.inhabited = Inhabited(templateId, d.transform.position);
            a.Setup(plan);
            if (v.HasLamps) v.LampState = () => a && a.LampLit;                                    // windows and signs: only where someone lives
        }

        static bool IsBuilding(string id) => id.StartsWith("Shop") || id.StartsWith("Stall_") || id.StartsWith("Farmhouse") || id.StartsWith("BrickHouse")
                                             || id.StartsWith("Shack") || id.StartsWith("GuardHut");

        /// <summary>Shops and stalls are always kept; houses inside a settlement are lived in three times out of four
        /// (by position, so the same houses every visit); anything out in the wilds stands empty.</summary>
        static bool Inhabited(string id, Vector3 p)
        {
            if (id.StartsWith("Shop") || id.StartsWith("Stall_")) return true;
            var t = DeformableTerrain.Instance;
            if (!t || t.World == null) return false;
            bool inTown = false;
            foreach (var st in t.World.settlements)
                if ((new Vector2(p.x, p.z) - st.pos).sqrMagnitude < st.radius * st.radius * 1.2f) { inTown = true; break; }
            if (!inTown) return id.StartsWith("GuardHut");
            int h = (Mathf.RoundToInt(p.x * 4f) * 73856093) ^ (Mathf.RoundToInt(p.z * 4f) * 19349663) ^ t.World.seed;
            return ((h >> 3) & 3) != 0;
        }

        static bool TryKind(string n, out Kind kind)
        {
            kind = Kind.Curtain;
            if (string.IsNullOrEmpty(n)) return false;
            for (int i = 0; i < kindNames.Length; i++)
            {
                var k = kindNames[i];
                if (!n.StartsWith(k, System.StringComparison.Ordinal)) continue;
                if (n.Length > k.Length)
                {
                    char ch = n[k.Length];
                    if (!(char.IsDigit(ch) || (ch == '_' && k != "Lamp"))) continue;               // Lamp_* = emissive splits
                }
                kind = (Kind)i;
                return true;
            }
            return false;
        }

        static Plan PlanFor(string id, Transform root)
        {
            if (plans.TryGetValue(id, out var plan)) return plan;
            plan = new Plan();
            var groups = new Dictionary<string, (Kind kind, List<int[]> paths)>();
            var path = new List<int>();
            Walk(root, path, groups, plan);
            foreach (var g in groups.Values) plan.groups.Add((g.kind, g.paths.ToArray()));
            plans[id] = plan;
            return plan;
        }

        static void Walk(Transform t, List<int> path, Dictionary<string, (Kind, List<int[]>)> groups, Plan plan)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                path.Add(i);
                var bn = HDVisual.BaseName(c.name);
                if (bn == "Chimney" && plan.chimney == null) plan.chimney = path.ToArray();
                if (TryKind(bn, out var kind))
                {
                    if (!groups.TryGetValue(bn, out var g)) groups[bn] = g = (kind, new List<int[]>());
                    g.Item2.Add(path.ToArray());
                }
                else Walk(c, path, groups, plan);
                path.RemoveAt(path.Count - 1);
            }
        }

        static Transform Resolve(Transform root, int[] path)
        {
            var t = root;
            foreach (int i in path) { if (!t || i >= t.childCount) return null; t = t.GetChild(i); }
            return t;
        }

        void Setup(Plan plan)
        {
            phase = Mathf.Abs(transform.position.x * 0.37f + transform.position.z * 0.71f) % 6.28f;     // deterministic per place
            var root = vis.root;
            if (plan.chimney != null) chimney = Resolve(root, plan.chimney);
            var sum = Vector3.zero; int lamps = 0;
            int k = 0;
            foreach (var (kind, paths) in plan.groups)
            {
                var ts = new List<Transform>();
                foreach (var p in paths) { var t = Resolve(root, p); if (t) ts.Add(t); }
                if (ts.Count == 0) continue;
                var t0 = ts[0];
                var inv = Quaternion.Inverse(t0.rotation);
                var part = new Part
                {
                    kind = kind, t = ts.ToArray(), rot = new Quaternion[ts.Count],
                    up = (inv * Vector3.up).normalized, right = (inv * transform.right).normalized,
                    phase = phase + k++ * 1.3f,
                    gain = kind == Kind.Laundry ? 1.5f : kind == Kind.Curtain ? 0.35f : kind == Kind.Sign ? 0.6f : kind == Kind.Lamp ? 0.7f : 1f,
                };
                for (int i = 0; i < ts.Count; i++) part.rot[i] = ts[i].localRotation;
                parts.Add(part);
                if (kind == Kind.Lamp)
                {
                    var r = t0.GetComponentInChildren<Renderer>();
                    sum += r ? r.bounds.center : t0.position; lamps++;
                }
            }
            if (lamps > 0) { hasLamp = true; lampAt = transform.InverseTransformPoint(sum / lamps); }
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

        void Update()
        {
            float d2 = (transform.position - Focus).sqrMagnitude;
            bool near = d2 < Reach * Reach;
            Lantern(near);
            if (!near) return;
            if (vis && !vis.Shown) return;
            float time = Time.time;
            var wind = Fx.Wind; wind.y = 0f;
            float gust = WindDust.Gust;
            foreach (var p in parts) Sway(p, wind, gust, time);
            if (chimney && inhabited && (smokeT -= Time.deltaTime) <= 0f)
            {
                smokeT = Random.Range(1.1f, 2.2f);
                float h = DayNight.Hours;
                bool evening = h >= 17f || h < 8f;
                if (evening || Weather.TemperatureAt(transform.position.z) < 12f)
                {
                    // the Chimney's pivot is where the smoke leaves
                    Fx.Smoke(chimney.position + Vector3.up * 0.05f, Vector3.up * 0.7f + Fx.Wind * 0.35f, 0.38f, new Color(0.36f, 0.35f, 0.34f, 0.3f), 3.2f);
                }
            }
        }

        static void Sway(Part p, Vector3 wind, float gust, float time)
        {
            var t0 = p.t[0];
            if (!t0) return;
            var parent = t0.parent ? t0.parent.rotation : Quaternion.identity;
            var wl = Quaternion.Inverse(parent * p.rot[0]) * wind;
            wl -= p.up * Vector3.Dot(wl, p.up);
            float s = wl.magnitude;
            Quaternion q;
            if (p.kind == Kind.Flag || p.kind == Kind.Awning)
            {
                float k = Mathf.Clamp01(s / 4f);
                float a = (p.kind == Kind.Flag ? 9f : 4f) * k * (0.6f + 0.4f * Mathf.Sin(time * (3.5f + s * 0.5f) + p.phase)) + gust * 3f * (Mathf.PerlinNoise(time * 2.5f, p.phase) - 0.4f);
                q = Quaternion.AngleAxis(a, p.kind == Kind.Flag ? p.up : p.right);
            }
            else
            {
                float lean = Mathf.Min(s * 2.2f, 14f) * (0.6f + 0.4f * Mathf.Sin(time * 1.7f + p.phase)) + gust * 4f * Mathf.Sin(time * 6.3f + p.phase) * Mathf.Clamp01(s);
                var axis = s > 0.05f ? Vector3.Cross(wl, p.up).normalized : p.right;                   // the lower edge moves downwind
                float idle = p.kind == Kind.Lamp || p.kind == Kind.Curtain ? 0.8f * Mathf.Sin(time * 1.3f + p.phase) : 0f;
                q = Quaternion.AngleAxis(lean * p.gain, axis) * Quaternion.AngleAxis(idle, p.right);
            }
            for (int i = 0; i < p.t.Length; i++) if (p.t[i]) p.t[i].localRotation = p.rot[i] * q;
        }

        /// <summary>One warm point light at the lanterns while they burn (made on first need, capped by
        /// <see cref="LightBudget"/>), with a slow flicker.</summary>
        void Lantern(bool near)
        {
            if (!hasLamp) return;
            bool want = near && LampLit && (!vis || vis.Shown);
            if (!want) { if (lampLight && lampLight.enabled) lampLight.enabled = false; return; }
            if (!lampLight)
            {
                lampLight = new GameObject("LanternLight").AddComponent<Light>();
                lampLight.transform.SetParent(transform, false);
                lampLight.transform.localPosition = lampAt;
                lampLight.type = LightType.Point; lampLight.range = 7f; lampLight.intensity = 2.2f; lampLight.shadows = LightShadows.None;
                lampLight.color = new Color(1f, 0.74f, 0.42f);
            }
            lampLight.enabled = LightBudget.Allowed(lampLight);
            if (lampLight.enabled) lampLight.intensity = 2.2f * (0.88f + 0.22f * Mathf.PerlinNoise(Time.time * 4.5f, phase));
        }
    }
}
