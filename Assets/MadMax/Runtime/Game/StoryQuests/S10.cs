using MadMax.Building;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>S10 TWELVE VOLTS OF FAME: Pip's tiny car on the start line of a hill (bald rear tyres, 150 kg of ballast,
    /// long gearing, nearly dry), her tuning bench with two spare wheels beside it, flags at the start, the top and the
    /// shoulder. Checks the car's state for each fix, times runs from the start flags to the top flag while the trial is on,
    /// and hands over tyres for your own car and a checker stripe at the end.</summary>
    public partial class WastelandGame
    {
        bool s10Armed, s10Running, s10Noted, s10Shoulder;
        float s10StartT, s10Time, s10TimeSeen = -1f;
        string s10Plan = "-"; bool s10MedalSeen;

        partial void Scene_S10()
        {
            if (!StoryAnchors.Has("s10_start") || !Build || !Build.Structures) return;
            foreach (float x in new[] { -2.8f, 2.8f }) PutAt("s10_start", "flag", new Vector3(x, 0f, 0f), 0f);
            foreach (float x in new[] { -2.5f, 2.5f }) PutAt("s10_top", "flag", new Vector3(x, 0f, 0f), 0f);
            PutAt("s10_shoulder", "flag", Vector3.zero, 0f);
            PutAt("s10_bench", "tuning_bench", Vector3.zero, 0f);
            PutAt("s10_bench", "workbench", new Vector3(-2.2f, 0f, -0.8f), 0f);
            PutAt("s10_bench", "tyres", new Vector3(2.2f, 0f, -1f), 30f);
            PutAt("s10_bench", "barrel", new Vector3(2.6f, 0f, 0.8f), 0f);

            var pf = PrefabFor("Fiat500") ?? PrefabFor("Fiat126p") ?? PrefabFor("Trabant");
            if (!pf) return;
            var up = Quaternion.Euler(0f, StoryAnchors.Yaw("s10_start"), 0f);
            var p = StoryAnchors.Get("s10_start") - up * Vector3.forward * 2f; p.y = terrain.HeightNoLoad(p.x, p.z) + 0.7f;
            var car = Instantiate(pf, p, up).GetComponent<VehicleDriver>();
            car.name = "Pip's Car";
            Register(car, null);
            StoryTag.Set(car.gameObject, "pip_car");
            var paint = VehiclePaint.Of(car); paint.colour = 4; paint.Apply();
            if (car.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = 2f; sys.oil = sys.oilCapacity * 0.2f; }
            if (car.TryGetComponent<VehicleTuning>(out var tune)) { tune.ballast = 150f; tune.gearing = 0.8f; }         // her brother's setup: sandbags and top speed
            string wheel = null;
            var chassis = car.GetComponent<VehicleChassis>();
            if (chassis)
                foreach (var s in chassis.Sockets)
                    if (s.accepts == PartCategory.Wheel && s.Current && s.Current.TryGetComponent<WheelStats>(out var ws))
                    {
                        bool rear = car.transform.InverseTransformPoint(s.transform.position).z < 0f;
                        ws.wear = rear ? 0.9f : 0.3f;
                        if (rear) wheel = s.Current.partId;
                    }
            var b = StoryAnchors.Get("s10_bench"); var br = Quaternion.Euler(0f, StoryAnchors.Yaw("s10_bench"), 0f);
            for (int i = 0; i < 2 && wheel != null; i++)                                             // two spares leaning by the bench
            {
                var wp = b + br * new Vector3(-0.8f + i * 1.6f, 0f, 1.4f); wp.y = terrain.HeightNoLoad(wp.x, wp.z) + 0.4f;
                SpawnPart(wheel, wp, br);
            }
        }

        partial void Tick_S10()
        {
            if (!StoryAnchors.Has("s10_start")) return;
            var q = StoryLibrary.Get("S10");
            string plan = Story.Story.Route("S10", "plan"); bool gotMedal = Story.Story.StepDone("S10", "medal");
            if (q != null && (plan != s10Plan || gotMedal != s10MedalSeen || s10Time != s10TimeSeen || q.payoff == null))
            { s10Plan = plan; s10MedalSeen = gotMedal; s10TimeSeen = s10Time; q.payoff = StoryLibrary.S10_Payoff(plan, gotMedal, s10Time); }
            var tag = StoryTag.Find("pip_car");
            var car = tag ? tag.GetComponent<VehicleDriver>() : null;
            var start = StoryAnchors.Get("s10_start"); var top = StoryAnchors.Get("s10_top"); var shoulder = StoryAnchors.Get("s10_shoulder");
            float Flat2(Vector3 a, Vector3 c) => new Vector2(a.x - c.x, a.z - c.z).magnitude;
            bool viaShoulder = plan == StoryLibrary.S10Shoulder;
            float path = viaShoulder ? Flat2(start, shoulder) + Flat2(shoulder, top) : Flat2(start, top);
            float medal = path / 6f + 6f;
            if (!s10Noted)
            {
                s10Noted = true;
                float rise = top.y - start.y, run = Flat2(start, top);
                Journal.Add("JOB", "TWELVE VOLTS OF FAME: PIP'S NOTES: THE HILL CLIMBS " + rise.ToString("0") + " M IN " + run.ToString("0") + " M (ABOUT 1 IN " + Mathf.Max(1f, run / Mathf.Max(0.1f, rise)).ToString("0") +
                            "). THE SHOULDER LINE IS " + (Flat2(start, shoulder) + Flat2(shoulder, top)).ToString("0") + " M. MEDAL TIME: " + medal.ToString("0") + " S STRAIGHT UP");
            }

            // Pip's thank-you: tyres for your own car and a checker stripe on it
            if (Story.Story.StepDone("S10", "tell") && !Story.Story.Flag("s10_thanks"))
            {
                Story.Story.SetFlag("s10_thanks");
                VehicleDriver mine = null; float md = float.MaxValue;
                foreach (var v in Fleet) if (v && v != car) { float dd = Flat2(v.transform.position, Player.transform.position); if (dd < md) { md = dd; mine = v; } }
                string wheel = "wheel_compact";
                var mc = mine ? mine.GetComponent<VehicleChassis>() : null;
                if (mc) foreach (var s in mc.Sockets) if (s.accepts == PartCategory.Wheel && s.Current) { wheel = s.Current.partId; break; }
                var b = StoryAnchors.Get("s10_bench"); var br = Quaternion.Euler(0f, StoryAnchors.Yaw("s10_bench"), 0f);
                for (int i = 0; i < 2; i++) { var wp = b + br * new Vector3(-1f + i * 2f, 0f, -1.6f); wp.y = terrain.HeightNoLoad(wp.x, wp.z) + 0.4f; SpawnPart(wheel, wp, br); }
                if (mine) { var paint = VehiclePaint.Of(mine); paint.decal = 11; paint.Apply(); }                  // CHECKERS
                Journal.Add("JOB", "TWELVE VOLTS OF FAME: TWO NEW " + wheel.Replace('_', ' ').ToUpperInvariant() + " TYRES BY PIP'S BENCH" +
                                   (mine ? ", AND A HAND-PAINTED CHECKER STRIPE ON YOUR " + Name(mine) : ""));
            }
            if (!car) return;

            // the fixes, read off the car
            if (Time.frameCount % 20 == 0)
            {
                int wheels = 0, worn = 0;
                var ch = car.GetComponent<VehicleChassis>();
                if (ch) foreach (var s in ch.Sockets) if (s.accepts == PartCategory.Wheel && s.Current && s.Current.TryGetComponent<WheelStats>(out var ws)) { wheels++; if (ws.wear >= 0.5f) worn++; }
                if (wheels >= 4 && worn == 0) Story.Story.Note("s10:tyres");
                if (car.TryGetComponent<VehicleTuning>(out var tune))
                {
                    if (tune.ballast <= 10f) Story.Story.Note("s10:weight");
                    if (tune.gearing <= -0.2f || tune.finalDrive >= 0.2f) Story.Story.Note("s10:geared");
                }
                if (car.TryGetComponent<VehicleSystems>(out var sys) && sys.OilFraction >= 0.8f && sys.fuel >= 6f) Story.Story.Note("s10:fluids");
            }

            // the trial: start flags to top flag, in Pip's car, while it is on (and afterwards for the medal)
            var cur = q != null ? Story.Story.Current(q) : null;
            bool on = (cur != null && cur.id == "trial") || (Story.Story.StepDone("S10", "trial") && !Story.Story.StepDone("S10", "medal") && cur != null);
            bool driving = on && Current == car;
            if (!on) { s10Armed = s10Running = false; return; }
            if (!s10Running)
            {
                float ds = Flat2(car.transform.position, start);
                if (!s10Armed && driving && ds < 9f && Mathf.Abs(car.ForwardSpeed) < 1.5f) { s10Armed = true; Toast("ON THE START LINE: THE CLOCK STARTS WHEN YOU LEAVE THE FLAGS"); }
                else if (s10Armed && (!driving || ds > 30f)) s10Armed = false;
                if (s10Armed && driving && ds > 10f) { s10Running = true; s10Armed = false; s10Shoulder = false; s10StartT = Time.time; Toast("GO!"); }
                return;
            }
            if (!driving) { s10Running = false; Toast("THE RUN IS OFF: TAKE HER BACK TO THE START FLAGS"); return; }
            if (Flat2(car.transform.position, shoulder) < 14f) s10Shoulder = true;
            if (Flat2(car.transform.position, top) < 12f)
            {
                s10Running = false;
                float t = Time.time - s10StartT;
                if (s10Time <= 0f || t < s10Time) s10Time = t;
                Story.Story.Note("s10:finished");
                if (t <= medal) Story.Story.Note("s10:medal");
                Toast("THE TOP! " + t.ToString("0.0") + " S" + (s10Shoulder ? " BY THE SHOULDER" : "") + (t <= medal ? ": MEDAL TIME" : ": FINISHED, WHICH IS THE POINT"));
                MadMax.Audio.Sfx.Play2D("crowd_cheer", 0.4f);
            }
        }
    }
}
