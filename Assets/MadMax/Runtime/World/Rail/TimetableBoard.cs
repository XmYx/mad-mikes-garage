using System.Collections.Generic;
using MadMax.Rendering;
using MadMax.Voxel;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A chalk timetable board on every passenger platform (2026-10-31, the 1030 suggestion). Two posts and a
    /// framed blackboard at the platform's end, chalked with the station's name and the next two trains: the game time
    /// each is due (from the service clock, <see cref="TrainService.SecondsUntil"/>), where it goes next, and NOW
    /// BOARDING while one stands at the platform; "NO SERVICE" when no train calls. Shown within <see cref="ShowReach"/>
    /// m (<see cref="Tend"/>, 1 Hz), redrawn every 2 s while in view. Not saved (derived from the timetable).</summary>
    public class TimetableBoard : MonoBehaviour
    {
        public const float ShowReach = 150f, FoldReach = 210f;
        const int W = 96, H = 44;
        static Mesh frameMesh;
        public static readonly Dictionary<int, TimetableBoard> Shown = new Dictionary<int, TimetableBoard>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Shown.Clear(); frameMesh = null; }

        RailStation station;
        PixelCanvas canvas;
        Material mat;
        Renderer face;
        float redrawAt;
        /// <summary>The lines chalked on the board (tests).</summary>
        public readonly List<string> Lines = new List<string>();
        public RailStation Station => station;

        /// <summary>Show boards at passenger stations near <paramref name="focus"/>, fold away far ones.</summary>
        public static void Tend(Vector3 focus, Material propMat)
        {
            var g = MadMax.Game.WastelandGame.Instance;
            var net = g && g.World != null ? g.World.rail : null;
            if (net == null || !TrainDirector.Instance) return;
            foreach (var st in net.stations)
            {
                if (!st.Passenger || st.platforms.Count == 0) continue;
                var at = st.World(st.platforms[0].x + 1.4f, (st.platforms[0].z + st.platforms[0].w) * 0.5f, RailNetwork.PlatformTop);
                float d = Vector2.Distance(new Vector2(at.x, at.z), new Vector2(focus.x, focus.z));
                Shown.TryGetValue(st.id, out var b);
                if (b && d > FoldReach) { Destroy(b.gameObject); Shown.Remove(st.id); }
                else if (!b && d < ShowReach) Shown[st.id] = Make(st, at, propMat);
            }
        }

        static TimetableBoard Make(RailStation st, Vector3 at, Material propMat)
        {
            var go = new GameObject("TimetableBoard " + st.id, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, st.Yaw, 0f));   // faces +u: down the platform
            go.GetComponent<MeshFilter>().sharedMesh = FrameMesh();
            go.GetComponent<MeshRenderer>().sharedMaterial = propMat;
            var box = go.AddComponent<BoxCollider>(); box.center = new Vector3(0f, 1.05f, 0f); box.size = new Vector3(1.7f, 2.1f, 0.15f);
            var b = go.AddComponent<TimetableBoard>();
            b.station = st;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Chalk";
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(go.transform, false);
            q.transform.localPosition = new Vector3(0f, 1.36f, 0.075f);
            q.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);            // a quad faces −Z; the board faces +Z
            q.transform.localScale = new Vector3(1.44f, 1.44f * H / W, 1f);
            b.canvas = new PixelCanvas(W, H);
            b.mat = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
            b.mat.SetTexture("_BaseMap", b.canvas.texture);
            b.mat.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.78f));
            b.face = q.GetComponent<Renderer>();
            b.face.sharedMaterial = b.mat;
            b.face.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.Draw();
            return b;
        }

        static Mesh FrameMesh()
        {
            if (frameMesh) return frameMesh;
            var g = new VoxelGrid().Mat((byte)MadMax.Items.ResourceType.Wood);
            var post = Pal.Ramp(Pal.Wood, 1, 3201);
            g.Box(-10, 0, -1, -9, 24, 0, post); g.Box(9, 0, -1, 10, 24, 0, post);                          // posts
            g.Box(-10, 8, -1, 10, 9, 0, Pal.Ramp(Pal.Wood, 2, 3202)); g.Box(-10, 25, -1, 10, 26, 0, Pal.Ramp(Pal.Wood, 2, 3203));   // frame
            g.Box(-10, 9, -1, -10, 25, 0, post); g.Box(10, 9, -1, 10, 25, 0, post);
            g.Box(-9, 9, -1, 9, 25, -1, Pal.Ramp(Pal.Moss, 0, 3204));                                     // the board's back
            g.Box(-8, 27, -1, 8, 28, 0, Pal.Ramp(Pal.Rust, 2, 3205));                                     // a tin hood
            g.Box(-3, 8, 1, 3, 8, 1, Pal.Ramp(Pal.Wood, 0, 3206));                                        // chalk ledge
            g.Mat((byte)MadMax.Items.ResourceType.Stone); g.Box(-1, 9, 1, 0, 9, 1, Pal.Solid(Pal.Cream[3]));   // a stub of chalk
            g.Bevel();
            return frameMesh = VoxelMesher.Build(g, "Rail_TimetableBoard");
        }

        /// <summary>The game time <paramref name="seconds"/> of the clock from now, to five minutes.</summary>
        public static string Clock(float seconds)
        {
            float perHour = DayNight.DayMinutes * 60f / 24f;
            if (perHour <= 0.01f) return "SOON";
            float h = Mathf.Repeat(DayNight.Hours + seconds / perHour, 24f);
            int hh = Mathf.FloorToInt(h), mm = Mathf.FloorToInt((h - hh) * 60f / 5f) * 5;
            return hh.ToString("00") + ":" + mm.ToString("00");
        }

        void Update()
        {
            if (Time.time < redrawAt || !face || !face.isVisible) return;
            Draw();
        }

        /// <summary>Chalk the board again (also every 2 s while it is in view).</summary>
        public void Draw()
        {
            redrawAt = Time.time + 2f;
            Lines.Clear();
            var g = MadMax.Game.WastelandGame.Instance;
            var dir = TrainDirector.Instance;
            var trains = new List<(float t, string text)>();
            if (dir)
                foreach (var s in dir.services)
                {
                    if (s.ended || s.armoured) continue;
                    for (int k = 0; k < s.stopStation.Count; k++)
                    {
                        if (s.stopStation[k] != station.id) continue;
                        float secs = s.SecondsUntil(k, out int next);
                        if (secs < 0f) continue;
                        string to = next >= 0 && next < s.stopStation.Count ? StationName(s.stopStation[next], g) : "THE LINE";
                        trains.Add((secs, (secs <= 0f ? "NOW " : Clock(secs) + " ") + Short(to)));
                    }
                }
            trains.Sort((a, b) => a.t.CompareTo(b.t));
            Lines.Add(Short(station.Name(g ? g.World : null)));
            for (int i = 0; i < Mathf.Min(2, trains.Count); i++) Lines.Add(trains[i].text);
            if (trains.Count == 0) Lines.Add("NO SERVICE");
            else if (trains[0].t <= 0f) Lines.Add("NOW BOARDING");
            // chalk on a blackboard: smudges, a ruled line under the name
            canvas.Clear(new Color32(32, 44, 36, 255));
            var rnd = new System.Random(station.id * 31 + 7);
            for (int i = 0; i < 40; i++) canvas.Set(rnd.Next(W), rnd.Next(H), new Color32(52, 64, 56, 255));
            var chalk = new Color32(226, 224, 210, 255);
            var dim = new Color32(170, 176, 160, 255);
            canvas.Text(3, 3, Lines[0], chalk, 1, false);       // y runs down from the top
            canvas.Line(3, 10, W - 4, 10, dim);
            canvas.Text(3, 13, "NEXT TRAINS", dim, 1, false);
            for (int i = 1; i < Lines.Count; i++)
                canvas.Text(3, 13 + i * 8, Lines[i], Lines[i] == "NOW BOARDING" ? new Color32(236, 200, 120, 255) : chalk, 1, false);
            canvas.Upload();
        }

        static string StationName(int id, MadMax.Game.WastelandGame g)
        {
            var net = g && g.World != null ? g.World.rail : null;
            return net != null && id >= 0 && id < net.stations.Count ? net.stations[id].Name(g.World) : "THE LINE";
        }

        static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace(" TERMINAL", "").Replace(" STATION", "").Replace(" HALT", "");
            return s.Length > 17 ? s.Substring(0, 17) : s;
        }

        void OnDestroy() { if (mat) Destroy(mat); }
    }
}
