using System.Collections.Generic;
using MadMax.Npc;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Gang marks on the road (2026-10-31, the 1029 suggestion): where a raider gang's road leaves the towns at
    /// either end, a tyre cairn and a plank sign in the gang's colours with its emblem stands on the verge, facing the
    /// traffic coming into their ground (a toast and a journal line the first time). Once that gang's column has been
    /// wiped out (<see cref="ConvoySave.generation"/> or <c>deadDay</c>) the sign is shot full of holes and scorched, the
    /// top tyre knocked off, the emblem gone under a white cross — smouldering for the first day. Deterministic from the
    /// convoy routes; nothing saved.</summary>
    public class GangMarks : MonoBehaviour
    {
        public const float ShowReach = 160f, FoldReach = 220f, NoticeReach = 45f, Verge = 6.5f;
        public struct Mark { public string key; public Vector3 at; public float yaw; public int gang; public Convoy convoy; }
        public static GangMarks Instance { get; private set; }
        public static readonly List<Mark> Marks = new List<Mark>();
        public static readonly Dictionary<string, Transform> Shown = new Dictionary<string, Transform>();
        static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        static readonly HashSet<string> noticed = new HashSet<string>();
        /// <summary>The last notice given (tests).</summary>
        public static string LastNotice;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Marks.Clear(); Shown.Clear(); meshes.Clear(); noticed.Clear(); LastNotice = null; }

        bool planned;
        float smokeAt;

        public static GangMarks Ensure()
        {
            if (Instance) return Instance;
            Instance = new GameObject("GangMarks").AddComponent<GangMarks>();
            return Instance;
        }

        /// <summary>The gang's column has been beaten at least once.</summary>
        public static bool Beaten(Mark m) => m.convoy != null && m.convoy.save != null && (m.convoy.save.generation > 0 || m.convoy.save.deadDay >= 0);

        void Plan()
        {
            var d = NpcDirector.Instance;
            var t = DeformableTerrain.Instance;
            var w = t ? t.World : null;
            if (!d || w == null || d.Convoys.Count == 0) return;
            planned = true;
            Marks.Clear();
            foreach (var c in d.Convoys)
            {
                if (!c.raiders || c.route == null || c.route.Count < 4) continue;
                int gi = System.Array.IndexOf(NpcLore.Gangs, c.Gang);
                if (gi < 0) continue;
                int n = c.route.Count;
                for (int end = 0; end < 2; end++)
                {
                    int step = end == 0 ? 1 : -1, i = end == 0 ? 0 : n - 1;
                    while (i + step >= 0 && i + step < n && InTown(w, c.route[i])) i += step;     // out past the town's edge
                    int j = Mathf.Clamp(i + step, 0, n - 1);
                    var inward = c.route[j] - c.route[i]; inward.y = 0f;
                    if (inward.sqrMagnitude < 0.01f) continue;
                    inward.Normalize();
                    var right = new Vector3(inward.z, 0f, -inward.x);
                    Marks.Add(new Mark { key = "gm:" + c.id + ":" + end, at = c.route[i] + right * Verge, yaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg, gang = gi, convoy = c });
                }
            }
        }

        static bool InTown(WorldGen w, Vector3 p)
        {
            foreach (var st in w.settlements) if ((st.pos - new Vector2(p.x, p.z)).sqrMagnitude < (st.radius + 25f) * (st.radius + 25f)) return true;
            return false;
        }

        /// <summary>1 Hz: show / fold marks near <paramref name="focus"/>, notice the first time one is passed.</summary>
        public void Tend(Vector3 focus, Material mat, MadMax.Game.WastelandGame g)
        {
            if (!planned) Plan();
            var t = DeformableTerrain.Instance;
            foreach (var m in Marks)
            {
                var flat = m.at - focus; flat.y = 0f;
                float dist = flat.magnitude;
                bool beaten = Beaten(m);
                Shown.TryGetValue(m.key, out var tr);
                if (tr && (dist > FoldReach || tr.name.EndsWith(beaten ? ":up" : ":down"))) { Destroy(tr.gameObject); Shown.Remove(m.key); tr = null; }
                if (!tr && dist < ShowReach)
                {
                    var at = m.at; at.y = t ? t.HeightNoLoad(at.x, at.z) - 0.05f : focus.y;
                    // the sign's face (−Z of the mesh) looks back along the road at the traffic coming into their ground
                    var go = new GameObject("GangMark " + m.key + (beaten ? ":down" : ":up"), typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(transform, false);
                    go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, m.yaw, 0f));
                    go.GetComponent<MeshFilter>().sharedMesh = MeshFor(m.gang, beaten);
                    go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    var box = go.AddComponent<BoxCollider>(); box.center = new Vector3(0f, 0.5f, 0.64f); box.size = new Vector3(1.1f, 1f, 1.1f);
                    if (!beaten)
                    {
                        var emblem = MadMax.Vehicles.Decals.Mesh(m.gang + 1);
                        if (emblem)
                        {
                            var e = new GameObject("Emblem", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                            e.SetParent(go.transform, false);
                            e.localPosition = new Vector3(0f, 1.65f, -0.06f);
                            e.localRotation = Quaternion.Euler(0f, 180f, 0f);
                            e.localScale = Vector3.one * 1.6f;
                            e.GetComponent<MeshFilter>().sharedMesh = emblem;
                            e.GetComponent<MeshRenderer>().sharedMaterial = mat;
                        }
                    }
                    Shown[m.key] = go.transform;
                }
                if (g && dist < NoticeReach && noticed.Add(m.key + (beaten ? "#b" : "")))
                {
                    string gang = NpcLore.Gangs[m.gang];
                    LastNotice = beaten ? "THE " + gang + " CAIRN, SHOT UP AND BURNT: THEIR ROAD LIES QUIET"
                                        : "A TYRE CAIRN IN " + gang + " COLOURS: THEIR ROAD STARTS HERE";
                    g.Toast(LastNotice);
                    MadMax.Game.Journal.Add("PEOPLE", LastNotice);
                }
            }
        }

        void Update()
        {
            if (Time.time < smokeAt) return;
            smokeAt = Time.time + 0.7f;
            foreach (var m in Marks)
            {
                if (!Beaten(m) || !Shown.TryGetValue(m.key, out var tr) || !tr) continue;
                int dead = m.convoy.save.deadDay;
                if (dead < 0 || DayNight.Day - dead > 1) continue;
                Fx.Smoke(tr.position + tr.forward * 0.6f + Vector3.up * 0.9f, Vector3.up * 0.5f, 0.6f, new Color(0.22f, 0.2f, 0.19f, 0.6f), 3f);
            }
        }

        static Color32[] GangRamp(int gang)
        {
            switch (gang % 4)
            {
                case 0: return Pal.Chrome;
                case 1: return Pal.Rust;
                case 2: return Pal.Cream;
                default: return Pal.Black;
            }
        }

        /// <summary>The cairn and sign (0.08 m voxels; the face is −Z, the tyres behind it at +Z).</summary>
        public static Mesh MeshFor(int gang, bool beaten)
        {
            int key = gang * 2 + (beaten ? 1 : 0);
            if (meshes.TryGetValue(key, out var m) && m) return m;
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Scrap);
            var tyre = Pal.Ramp(Pal.Tire, 1, 3301);
            int tyres = beaten ? 2 : 3;
            for (int i = 0; i < tyres; i++) g.CylY(i % 2 == 0 ? 0 : 1, 8, 6.5f, i * 3, i * 3 + 2, tyre, 3.2f);              // stacked tyres behind the post
            if (beaten) g.Tube(new Vector3(9f, 2f, 2f), new Vector3(14f, 5f, 9f), 2.6f, tyre);                             // the top one knocked off
            var paint = GangRamp(gang);
            g.Mat((byte)MadMax.Items.ResourceType.Wood);
            g.Box(-1, 0, 1, 0, 24, 1, Pal.Ramp(Pal.Wood, 1, 3302));                                                     // post in front of the stack
            g.Box(-9, 16, 0, 9, 25, 0, beaten ? Pal.Weathered(paint, 0.85f, 3303, 2, 0) : Pal.Weathered(paint, 0.35f, 3303, 2, 0));   // the plank sign
            g.Box(-9, 15, 0, 9, 15, 0, Pal.Ramp(Pal.Rust, 1, 3304));                                                    // a tin strip
            g.Mat((byte)MadMax.Items.ResourceType.Scrap);
            g.Box(-10, 26, 0, -8, 27, 0, Pal.Ramp(Pal.Metal, 1)); g.Box(8, 26, 0, 10, 27, 0, Pal.Ramp(Pal.Metal, 1));  // horns of bent iron
            if (!beaten) { g.Box(-3, 26, 0, 3, 28, 0, Pal.Ramp(Pal.Cream, 3, 3305)); g.Box(-1, 29, 0, 1, 30, 0, Pal.Ramp(Pal.Cream, 2, 3306)); }   // a skull on top
            if (beaten)
            {
                g.Remove(p => p.z == 0 && p.y >= 16 && p.y <= 25 && Mathf.Abs(p.x) <= 9 && ((p.x * 73856093) ^ (p.y * 19349663) ^ (gang * 83492791)) % 11 == 0);   // bullet holes
                for (int x = -6; x <= 6; x++) { g.Set(x, 20 + x / 2, -1, Pal.Solid(Pal.Cream[3])); g.Set(x, 20 - x / 2, -1, Pal.Solid(Pal.Cream[3])); }   // a white cross over it
                for (int x = -9; x <= 9; x += 3) g.Box(x, 16, -1, x, 16 + Mathf.Abs(x * x) % 5, -1, Pal.Solid(Pal.Black[1]));                       // scorch streaks
            }
            g.Bevel();
            m = VoxelMesher.Build(g, "GangMark" + key);
            meshes[key] = m;
            return m;
        }
    }
}
