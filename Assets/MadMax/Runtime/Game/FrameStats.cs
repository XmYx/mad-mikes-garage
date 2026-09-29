using UnityEngine;
using UnityEngine.Profiling;

namespace MadMax.Game
{
    /// <summary>Diagnostics for builds. -fpslog: frame stats every 5 s. -mute: pause all audio.
    /// -autotest: start a new game, drive the first car along the nearest road (streaming new terrain), log stats,
    /// record a profiler capture (development builds, -profile &lt;file.raw&gt;) from 15 s to 45 s (-profilefrom N: N to N+30; -1 also records the new-game load),
    /// and quit at 50 s.</summary>
    public class FrameStats : MonoBehaviour
    {
        static bool started;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => started = false;

        float t0, worst, testStart = -1f, profileFrom = 15f;
        int frames, spikes;
        bool autotest, profiling, towtest;
        MadMax.Vehicles.TowCoupling tow;
        System.Collections.Generic.List<Vector3> route;
        int routeIndex, routeDir = 1;
        float stuckT, backT, flipT;
        WastelandGame menuGame;
        float nextTowLog;
        string profileFile, shotsDir;
        float nextShot;
        int shotIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "-mute") >= 0) AudioListener.pause = true;
            bool log = System.Array.IndexOf(args, "-fpslog") >= 0, test = System.Array.IndexOf(args, "-autotest") >= 0;
            int si = System.Array.IndexOf(args, "-shots");
            if ((!log && !test && si < 0) || FindAnyObjectByType<FrameStats>()) return;
            var go = new GameObject("FrameStats"); DontDestroyOnLoad(go);
            var fs = go.AddComponent<FrameStats>();
            fs.autotest = test;
            fs.towtest = System.Array.IndexOf(args, "-towtest") >= 0;
            int pi = System.Array.IndexOf(args, "-profile");
            if (pi >= 0 && pi + 1 < args.Length) fs.profileFile = args[pi + 1];
            int pf = System.Array.IndexOf(args, "-profilefrom");                       // capture start (s after driving begins)
            if (pf >= 0 && pf + 1 < args.Length && float.TryParse(args[pf + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var from)) fs.profileFrom = from;
            if (si >= 0 && si + 1 < args.Length) { fs.shotsDir = args[si + 1]; System.IO.Directory.CreateDirectory(fs.shotsDir); }
        }

        void Update()
        {
            frames++;
            float dtu = Time.unscaledDeltaTime;
            worst = Mathf.Max(worst, dtu);
            if (dtu > 1f / 30f) spikes++;
            float t = Time.realtimeSinceStartup;
            if (autotest) Drive(t);
            // -shots <dir>: a frame every 0.5 s for the first 45 s (boot film, handover, title)
            if (shotsDir != null && t < 45f && t >= nextShot)
            {
                nextShot = t + 0.5f;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotsDir, $"shot_{shotIndex++:000}_{t:00.0}.png"));
            }
            if (t - t0 < 5f) return;
            var gc = WastelandGame.Instance ? WastelandGame.Instance.Current : null;
            Debug.Log($"[fps] t={t:0} avg {frames / (t - t0):0.0} fps, worst {worst * 1000f:0} ms, frames >33ms {spikes}, audio paused {AudioListener.pause}, gc {System.GC.CollectionCount(0)}" + (gc ? $", car {gc.SpeedKmh:0} km/h rpm {gc.Rpm:0} at {gc.transform.position}" : ""));
            t0 = t; frames = 0; worst = 0f; spikes = 0;
        }

        /// <summary>-towtest: rest the small tanker behind the car, hitch it (as J would), then drive and log the joint.</summary>
        bool TowTest(WastelandGame g, MadMax.Vehicles.VehicleDriver c, float s)
        {
            var hitch = MadMax.Vehicles.TowCoupling.HitchOf(c);
            if (!hitch) { Debug.Log("[towtest] current vehicle has no hitch"); towtest = false; return false; }
            if (s < 0.5f)
            {
                // open ground first: a spot where the car and the 15 m ahead touch nothing but terrain
                var terrain0 = MadMax.World.DeformableTerrain.Instance;
                var q = c.transform.position + c.transform.right * 30f;
                for (int k = 0; k < 64; k++)
                {
                    float ang = k * 0.618f * Mathf.PI * 2f, rad = 25f + k * 3f;
                    var cand = c.transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rad;
                    cand.y = terrain0.Height(cand.x, cand.z) + 1.0f;
                    bool blocked = false;
                    foreach (var col in Physics.OverlapBox(cand + c.transform.forward * 7f + Vector3.up * 0.5f, new Vector3(2.5f, 1.2f, 12f), c.transform.rotation))
                        if (!col.transform.IsChildOf(c.transform) && !col.name.StartsWith("Chunk")) { blocked = true; break; }
                    if (!blocked) { q = cand; break; }
                }
                c.Body.position = q; c.transform.position = q; c.Body.linearVelocity = Vector3.zero;
                return true;
            }
            if (s < 2.5f) { c.handbrake = true; return true; }
            if (!tow)
            {
                foreach (var v in g.AllVehicles) if (v && v.name.StartsWith("TankerSmall") && !v.name.StartsWith("Wreck")) tow = v.GetComponent<MadMax.Vehicles.TowCoupling>();
                if (!tow) { towtest = false; return false; }
                var rb = tow.GetComponent<MadMax.Vehicles.VehicleDriver>().Body;
                var terrain = MadMax.World.DeformableTerrain.Instance;
                var rot = c.transform.rotation;
                var couplerLocal = tow.transform.InverseTransformPoint(tow.Coupler.position);
                var p = hitch.position - c.transform.forward * 0.9f - rot * couplerLocal;
                p.y = terrain.Height(p.x, p.z) + 0.6f;
                rb.isKinematic = false; rb.position = p; rb.rotation = rot; tow.transform.SetPositionAndRotation(p, rot);
                rb.linearVelocity = Vector3.zero;
                Debug.Log("[towtest] tanker placed behind " + c.name);
            }
            c.handbrake = s < 7f; c.throttleInput = s < 7f ? 0f : 0.7f; c.steerInput = s > 12f ? 0.25f : 0f;
            if (s > 4f && !tow.Tower && !tow.Busy && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nohitch") < 0) Debug.Log("[towtest] couple=" + tow.Couple(c));
            if (t0 >= 0f && Time.realtimeSinceStartup > nextTowLog)
            {
                nextTowLog = Time.realtimeSinceStartup + 1f;
                var j = tow.GetComponent<ConfigurableJoint>();
                float err = j ? Vector3.Distance(tow.transform.TransformPoint(j.anchor), c.transform.TransformPoint(j.connectedAnchor)) : -1f;
                var td = tow.GetComponent<MadMax.Vehicles.VehicleDriver>();
                var tb = td.Body;
                var legT = tow.transform.Find("Body/Legs");
                float legGap = legT ? legT.GetComponent<Renderer>().bounds.min.y - MadMax.World.DeformableTerrain.Instance.Height(legT.position.x, legT.position.z) : -99f;
                Debug.Log($"[towtest] s={s:0.0} busy={tow.Busy} hitched={(bool)tow.Tower} err={err:0.000} car={c.SpeedKmh:0.0}km/h tanker={tb.linearVelocity.magnitude * 3.6f:0.0}km/h legsUp={tow.LegsUp:0.00} legGap={legGap:0.00}m legCol={(legT && legT.GetComponent<Collider>().enabled)} trailerHB={td.handbrake}");
                var tr = MadMax.World.DeformableTerrain.Instance;
                var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
                var wl = (System.Collections.IList)typeof(MadMax.Vehicles.VehicleDriver).GetField("wheels", F).GetValue(c);
                var sbw = new System.Text.StringBuilder();
                foreach (var w in wl)
                {
                    var T = w.GetType();
                    var sock = (MadMax.Vehicles.MountSocket)T.GetField("socket").GetValue(w);
                    var sp = sock.transform.position;
                    sbw.Append($" [g={T.GetField("grounded").GetValue(w)} part={T.GetField("part").GetValue(w) != null} r={(float)T.GetField("radius").GetValue(w):0.00} sockDy={sp.y - tr.Height(sp.x, sp.z):0.00}]");
                }
                Debug.Log($"[towtest]   wheels up.y={c.transform.up.y:0.00}{sbw}");
                Debug.Log($"[towtest]   car occ={c.Occupied} kin={c.Body.isKinematic} sleep={c.Body.IsSleeping()} thr={c.throttleInput} hb={c.handbrake} brake={c.brakeInput} rpm={c.Rpm:0} gear={c.Gear} drive={c.DriveCommand:0.00} slip={c.WheelSlip:0.00} vel={c.Body.linearVelocity} dy={c.transform.position.y - tr.Height(c.transform.position.x, c.transform.position.z):0.00} mass={c.Body.mass:0} power={(c.TryGetComponent<MadMax.Vehicles.VehicleSystems>(out var cs) ? cs.PowerFactor : -1f)}");
            }
            if (s > 25f) { Application.Quit(); }
            return true;
        }

        /// <summary>Pick the nearest road; <see cref="Pilot"/> then follows it out of town at cruising speed (streams new
        /// terrain the whole run).</summary>
        void RoadPilot(WastelandGame g)
        {
            // a road point well outside town (sheds, the parked fleet), car placed on it facing along
            var c = g.Current;
            var p = c.transform.position;
            float best = float.MaxValue;
            foreach (var r in g.World.roads.roads)
                for (int i = 2; i < r.points.Count - 2; i++)
                {
                    var q = r.points[i];
                    float d = (q - p).sqrMagnitude;
                    if (d < best && d > 120f * 120f && g.World.SettlementAt(q.x, q.z) == null) { best = d; route = r.points; routeIndex = i; }
                }
            if (route == null) return;
            int fwd = Mathf.Min(route.Count - 1, routeIndex + 6), back = Mathf.Max(0, routeIndex - 6);
            routeDir = (route[fwd] - p).sqrMagnitude >= (route[back] - p).sqrMagnitude ? 1 : -1;  // away from town
            var at = route[routeIndex]; var ahead = route[routeIndex + routeDir] - at; ahead.y = 0f;
            at.y = MadMax.World.DeformableTerrain.Instance.Height(at.x, at.z) + 0.8f;
            c.Body.position = at; c.Body.rotation = Quaternion.LookRotation(ahead.normalized);
            c.transform.SetPositionAndRotation(at, c.Body.rotation);
            c.Body.linearVelocity = Vector3.zero; c.Body.angularVelocity = Vector3.zero;
            Debug.Log($"[autotest] road start {at} heading {ahead.normalized}");
        }

        /// <summary>Pure pursuit along the road at ~60 km/h; backs out when stuck.</summary>
        void Pilot(MadMax.Vehicles.VehicleDriver c, float dt)
        {
            var pos = c.transform.position;
            float speed = c.ForwardSpeed, look = 8f + Mathf.Abs(speed) * 0.7f;
            for (int guard = 0; guard < 16; guard++)
            {
                var q = route[routeIndex] - pos; q.y = 0f;
                if (q.magnitude > look) break;
                int next = routeIndex + routeDir;
                if (next < 0 || next >= route.Count) { routeDir = -routeDir; next = routeIndex + routeDir; }
                routeIndex = next;
            }
            var to = route[routeIndex] - pos; to.y = 0f;
            float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(c.transform.forward, Vector3.up), to, Vector3.up);
            flipT = c.transform.up.y < 0.3f ? flipT + dt : 0f;
            if (flipT > 2f) { flipT = 0f; c.Recover(); }                                     // T: back on the wheels
            stuckT = Mathf.Abs(speed) < 1f && c.throttleInput > 0.5f ? stuckT + dt : 0f;
            if (stuckT > 2f) { backT = 2f; routeIndex = Mathf.Clamp(routeIndex + routeDir * 2, 0, route.Count - 1); }
            c.handbrake = false;
            if ((backT -= dt) > 0f) { c.throttleInput = 0f; c.brakeInput = 1f; c.steerInput = -Mathf.Sign(angle); stuckT = 0f; return; }
            c.steerInput = Mathf.Clamp(angle / 30f, -1f, 1f);
            float want = Mathf.Abs(angle) > 35f ? 8f : 17f;
            c.throttleInput = speed < want ? 1f : 0f;
            c.brakeInput = speed > want + 3f ? 0.5f : 0f;
        }

        void StartProfiler()
        {
            profiling = true;
            Profiler.logFile = profileFile; Profiler.enableBinaryLog = true; Profiler.maxUsedMemory = 512 * 1024 * 1024; Profiler.enabled = true;
            Debug.Log("[autotest] profiling to " + profileFile + " dev=" + Debug.isDebugBuild);
        }

        void Drive(float t)
        {
            var g = WastelandGame.Instance;
            if (!g || !g.Ready) return;
            if (!started)
            {
                started = true;
                menuGame = g;                                                                 // the menu's world, until the new one loads
                if (profileFile != null && profileFrom < 0f) StartProfiler();             // -profilefrom -1: include the new-game load
                g.StartNewGame(new GameRules { randomSeed = false }, new MadMax.RPG.CharacterStats(), new Appearance(), false);   // same world every run
                return;
            }
            if (g == menuGame || TitleSequence.Playing || !g.Current) return;
            if (testStart < 0f) { testStart = t; WastelandGame.ExternalInput = true; if (!towtest) RoadPilot(g); Debug.Log("[autotest] driving" + (route != null ? " the road" : "")); }
            float s = t - testStart;
            var c = g.Current;
            if (towtest && TowTest(g, c, s)) return;
            if (route != null) Pilot(c, Time.deltaTime);
            else
            {
                c.handbrake = false;
                c.throttleInput = s % 12f < 10f ? 1f : 0f;         // lift every 12 s
                c.brakeInput = s % 12f > 10.5f ? 0.6f : 0f;
                c.steerInput = Mathf.Sin(s * 0.25f) * 0.35f;
            }
            if (profileFile != null && !profiling && s > profileFrom) StartProfiler();
            if (profiling && s > profileFrom + 30f && Profiler.enabled) { Profiler.enabled = false; Profiler.logFile = ""; Debug.Log("[autotest] profile done"); }
            if (s > Mathf.Max(50f, profileFrom + 35f)) { Debug.Log($"[autotest] done, car at {c.transform.position}, {c.SpeedKmh:0} km/h"); Application.Quit(); }
        }
    }
}
