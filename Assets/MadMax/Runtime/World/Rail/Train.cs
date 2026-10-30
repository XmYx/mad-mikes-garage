using System.Collections.Generic;
using MadMax.Game;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A consist on the rails: cars placed by distance behind the head along a <see cref="RailPath"/>, each
    /// pose taken from its two bogie points. A service train (<see cref="service"/>) runs its timetable (the head is a
    /// function of the service clock); a free train (player-driven, stolen, derelict) runs on forces — throttle, brakes,
    /// gradient, rolling drag — extending its path through the points ahead and stopping at buffer stops. Free trains
    /// couple when they meet gently and crash when they don't; anyone standing in front of a moving train is struck.
    /// Sounds: the engine, wheel clacks on rail joints, brake squeal, the horn at level crossings.</summary>
    public partial class Train : MonoBehaviour
    {
        public static readonly List<Train> All = new List<Train>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public RailNetwork net;
        public readonly List<RailCar> cars = new List<RailCar>();
        public RailPath path;
        public float head, speed;
        public float Length { get; private set; }
        /// <summary>The timetable this train runs (null: a free train).</summary>
        public TrainService service;
        public string trainName = "TRAIN";
        // free-train controls
        public float throttle, brake = 1f, fuel;
        public int reverser = 1;
        public bool driven;
        /// <summary>The player has made it theirs (driven, built or bought): saved and shown as YOURS.</summary>
        public bool owned;
        public float tank = 300f;
        float[] offsets = new float[0];
        float clackDist, smokeAt, hornAt, crossingCheckAt, practised;
        bool placed;

        void OnEnable() { All.Add(this); RailSwitch.Thrown += Replan; }
        void OnDisable() { All.Remove(this); RailSwitch.Thrown -= Replan; }

        public RailCar Loco { get { foreach (var c in cars) if (c && c.def.cab) return c; return null; } }
        public bool Free => service == null;
        public float Mass { get { float m = 0f; foreach (var c in cars) if (c) m += c.def.mass; return m; } }
        public Vector3 HeadPoint => path.At(head);

        /// <summary>A train of <paramref name="kinds"/> with its head at path distance <paramref name="head"/>.</summary>
        public static Train Create(RailNetwork net, IList<CarKind> kinds, IList<bool> flips, RailPath path, float head, Material mat, Material glow, int seed, string name)
        {
            var go = new GameObject("Train_" + name);
            var t = go.AddComponent<Train>();
            t.net = net; t.path = path; t.head = head; t.trainName = name;
            for (int i = 0; i < kinds.Count; i++)
            {
                var car = RailCar.Create(kinds[i], seed + i * 7, mat, glow);
                car.train = t; car.flipped = flips != null && i < flips.Count && flips[i];
                t.cars.Add(car);
            }
            t.Relayout();
            var loco = t.Loco;
            if (loco) t.tank = loco.def.kind == CarKind.ArmoredLoco ? 600f : loco.def.hand ? 0f : 300f;
            return t;
        }

        /// <summary>Name the search spots of the cars (and switch them on) under a key prefix.</summary>
        public void KeyLoot(string prefix)
        {
            for (int i = 0; i < cars.Count; i++) { var c = cars[i]; if (!c || !c.Loot) continue; c.Loot.key = prefix + ":" + i; c.Loot.enabled = true; }
        }

        /// <summary>Recompute the car offsets from the head (after coupling or uncoupling).</summary>
        public void Relayout()
        {
            offsets = new float[cars.Count];
            float acc = 0f;
            for (int i = 0; i < cars.Count; i++)
            {
                offsets[i] = acc + cars[i].def.Half;
                acc += cars[i].def.length + 0.5f;
            }
            Length = Mathf.Max(0f, acc - 0.5f);
        }

        // ------------------------------------------------------------------ running

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (net == null || path == null || cars.Count == 0) return;
            if (path.Repair(net)) { }
            if (service != null) service.Step(this, dt);
            else { Simulate(dt); CheckContact(); }
            PlaceCars(dt, !placed);
            placed = true;
            DerailCheck(dt);
            if (Mathf.Abs(speed) > 1.5f) StrikeAhead();
        }

        void PlaceCars(float dt, bool snap)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                if (!c) continue;
                float dc = head - offsets[i];
                var f = path.At(dc + c.def.bogie);
                var r = path.At(dc - c.def.bogie);
                var fwd = f - r;
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
                var rot = Quaternion.LookRotation(c.flipped ? -fwd : fwd, Vector3.up);
                var pos = (f + r) * 0.5f + Vector3.up * RailNetwork.RailTop;
                DerailPose(i, ref pos, ref rot);
                c.Pose(pos, rot, dt, snap);
            }
        }

        /// <summary>Teleport the cars to the current head (load, placement).</summary>
        public void Snap() { if (path != null) PlaceCars(0f, true); placed = true; }

        /// <summary>Free-train physics along the path: traction from the loco (by the reverser and how the loco faces),
        /// brakes, gradient, rolling drag; the path grows ahead through the points; buffer stops stop it.</summary>
        void Simulate(float dt)
        {
            float m = Mathf.Max(200f, Mass);
            var loco = Loco;
            float force = 0f;
            if (Derailed) throttle = 0f;
            if (driven && loco && !loco.Broken && throttle > 0f && (loco.def.hand || fuel > 0f))
            {
                float dir = reverser * (loco.flipped ? -1f : 1f);
                float vmax = loco.def.vmax * Competence;
                float along = speed * dir;                                                          // + in the direction of pull
                float fall = Mathf.Clamp01(1f - Mathf.Max(0f, along) / Mathf.Max(0.5f, vmax));
                float surge = Competence < 0.5f ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 2.3f)) : 1f;   // untrained: it lurches
                force = Competence <= 0f ? 0f : throttle * surge * loco.def.tractive * fall * dir;
                if (!loco.def.hand) fuel = Mathf.Max(0f, fuel - throttle * (loco.def.kind == CarKind.ArmoredLoco ? 0.18f : 0.1f) * dt);
            }
            path.At(head - Length * 0.5f, out var tan);
            force -= m * 9.81f * tan.y;                                                              // the gradient
            speed += force / m * dt;
            float decel = (driven || !owned ? brake : 1f) * 1.4f + 0.02f;                            // parked trains stand on their brakes
            if (!driven) decel = Mathf.Max(decel, 1.4f);
            if (Derailed) decel = Mathf.Max(decel, 5.5f);                                            // ploughing through the ballast
            if (Mathf.Abs(speed) <= decel * dt) speed = 0f; else speed -= Mathf.Sign(speed) * decel * dt;
            Advance(speed * dt);
            if (driven && Mathf.Abs(speed) > 0.5f)
            {
                practised += Mathf.Abs(speed) * dt;
                if (practised > 120f) { practised = 0f; WastelandGame.Instance?.Stats?.Practice(MadMax.RPG.Skill.Rail, 2f); }
            }
        }

        /// <summary>0.35 (untrained: slow, jerky) .. 1: how well the one at the controls drives this loco.</summary>
        public float Competence
        {
            get
            {
                var g = WastelandGame.Instance; var loco = Loco;
                if (!g || g.Stats == null || !loco || loco.def.hand || !driven) return 1f;
                int rail = g.Stats.Level(MadMax.RPG.Skill.Rail), drive = g.Stats.Level(MadMax.RPG.Skill.Driving);
                if (rail >= loco.def.skill + 1) return 1f;
                if (rail >= loco.def.skill || (loco.def.skill <= 1 && drive >= 3)) return 0.8f;
                return loco.def.skill >= 2 ? 0f : 0.35f;
            }
        }

        /// <summary>Move the head by <paramref name="delta"/>, growing the path and stopping at dead ends.</summary>
        public void Advance(float delta)
        {
            float before = head;
            head += delta;
            int guard = 0;
            while (path.End < head + 30f && guard++ < 8) if (!path.ExtendFront(net)) break;
            while (path.Start > head - Length - 30f && guard++ < 16) if (!path.ExtendBack(net)) break;
            float hi = path.End - 0.6f, lo = path.Start + Length + 0.6f;
            if (head > hi && delta > 0f) { Bump(speed); head = Mathf.Max(before, hi); speed = 0f; }
            else if (head < lo && delta < 0f) { Bump(speed); head = Mathf.Min(before, lo); speed = 0f; }
            path.Trim(head - Length - 80f, head + 80f);
            // wheel clacks over the rail joints, heard near the listener
            clackDist += Mathf.Abs(head - before);
            if (clackDist > 12.5f)
            {
                clackDist = 0f;
                var near = NearestCar(MadMax.Audio.Sfx.ListenerPosition);
                if (near) MadMax.Audio.Sfx.Play("rail_clack", near.transform.position, Mathf.Clamp01(Mathf.Abs(speed) / 12f) * 0.8f, Random.Range(0.92f, 1.08f), 70f, 0.02f);
            }
        }

        void Bump(float v)
        {
            if (Mathf.Abs(v) < 1f) return;
            MadMax.Audio.Sfx.Play(Mathf.Abs(v) > 4f ? "crash_big" : "hit_metal", HeadPoint, Mathf.Clamp01(Mathf.Abs(v) / 6f), 0.7f, 90f);
            if (Mathf.Abs(v) > 4f) foreach (var c in cars) if (c) c.hp -= (Mathf.Abs(v) - 4f) * 25f;
        }

        public RailCar NearestCar(Vector3 p)
        {
            RailCar best = null; float bd = float.MaxValue;
            foreach (var c in cars) { if (!c) continue; float d = (c.transform.position - p).sqrMagnitude; if (d < bd) { bd = d; best = c; } }
            return best;
        }

        /// <summary>Position along the path of the n-th car (centre).</summary>
        public float CarCentre(int i) => head - offsets[i];

        // ------------------------------------------------------------------ meeting things on the line

        /// <summary>People in front of a moving train are struck aside.</summary>
        void StrikeAhead()
        {
            var g = WastelandGame.Instance;
            if (!g || !g.Player || !g.Player.gameObject.activeSelf) return;
            float front = speed > 0f ? head : head - Length;
            var p = g.Player.transform.position;
            if (!g.Current && !g.Player.Sitting && (p - path.At(front)).sqrMagnitude < 16f && path.Project(p, 1.7f, out float d))
            {
                bool inPath = speed > 0f ? d > head - 1.5f && d < head + 1.2f : d < head - Length + 1.5f && d > head - Length - 1.2f;
                if (inPath && p.y < path.At(d).y + 0.9f)                                              // on the line, not riding a deck
                {
                    path.At(d, out var t);
                    var side = Vector3.Cross(Vector3.up, t).normalized;
                    var lp = p - path.At(d);
                    var shove = Vector3.Dot(lp, side) >= 0f ? side : -side;
                    g.Vitals.Hurt(Mathf.Abs(speed) * 4.5f, "CRASH");
                    if (g.Player && g.Player.gameObject.activeSelf) g.Player.Teleport(p + shove * 2.6f + Vector3.up * 0.2f, g.Player.transform.eulerAngles.y);
                    MadMax.Audio.Sfx.Play("punch", p, 1f, 0.7f, 40f);
                }
            }
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || n.Riding) continue;
                var q = n.transform.position;
                if ((q - path.At(front)).sqrMagnitude > 9f) continue;
                if (!path.Project(q, 1.6f, out float dn)) continue;
                bool inPath = speed > 0f ? dn > head - 1.5f && dn < head + 1f : dn < head - Length + 1.5f;
                if (inPath && q.y < path.At(dn).y + 0.9f) n.ApplyHit(q + Vector3.up, (q - path.At(front)).normalized + Vector3.up * 0.3f, Mathf.Abs(speed) * 0.4f, 0.4f, cars[0] ? cars[0].gameObject : gameObject);
            }
        }

        /// <summary>The nearest other train's car on our path in the direction of travel: the gap to it (false: none).</summary>
        public bool Obstacle(int sign, float reach, out Train other, out float gap)
        {
            other = null; gap = float.MaxValue;
            var me = path.At(head - Length * 0.5f);
            foreach (var t in All)
            {
                if (t == this || t.cars.Count == 0) continue;
                if ((t.path.At(t.head - t.Length * 0.5f) - me).sqrMagnitude > Sq(Length * 0.5f + t.Length * 0.5f + reach + 20f)) continue;
                foreach (var c in t.cars)
                {
                    if (!c || !path.Project(c.transform.position, 1.6f, out float d)) continue;
                    float g2 = sign > 0 ? d - c.def.Half - head : (head - Length) - (d + c.def.Half);
                    if (g2 < -c.def.length - Length) continue;                                       // behind us
                    if (g2 < gap) { gap = g2; other = t; }
                }
            }
            return other != null && gap < reach;
        }

        static float Sq(float x) => x * x;

        /// <summary>Free trains that meet: gently they couple, hard they crash (both stop, both take damage).</summary>
        public void CheckContact()
        {
            if (service != null || Mathf.Abs(speed) < 0.05f) return;
            int sign = speed > 0f ? 1 : -1;
            if (!Obstacle(sign, 0.4f, out var other, out float gap)) return;
            float rel = Mathf.Abs(speed - (other.service != null ? other.speed : 0f));
            if (other.service == null && rel < 2.2f) { Couple(other, sign); return; }
            MadMax.Audio.Sfx.Play("crash_big", HeadPoint, 1f, 0.8f, 150f);
            float dmg = rel * rel * 6f;
            foreach (var c in cars) if (c) c.hp -= dmg * 0.3f;
            foreach (var c in other.cars) if (c) c.hp -= dmg * 0.3f;
            var gi = WastelandGame.Instance; if (gi && gi.cameraRig) gi.cameraRig.Shake(Mathf.Min(8f, rel));
            head -= sign * Mathf.Max(0f, 0.3f - gap);
            speed = 0f;
            if (other.service != null) other.service.Hit(5f);
        }

        /// <summary>Take the other train's cars into this one (they stand on our path ahead or behind).</summary>
        public void Couple(Train other, int sign)
        {
            var list = new List<(float d, RailCar c)>();
            for (int i = 0; i < cars.Count; i++) list.Add((head - offsets[i], cars[i]));
            // extend our path to cover the other train
            for (int k = 0; k < 6; k++) { if (sign > 0) path.ExtendFront(net); else path.ExtendBack(net); }
            foreach (var c in other.cars)
            {
                if (!c) continue;
                if (!path.Project(c.transform.position, 2f, out float d)) return;                   // not on our track after all
                list.Add((d, c));
            }
            list.Sort((a, b) => b.d.CompareTo(a.d));
            cars.Clear();
            foreach (var (d, c) in list)
            {
                c.train = this;
                path.At(d, out var t);
                c.flipped = Vector3.Dot(c.transform.forward, t) < 0f;
                cars.Add(c);
            }
            other.cars.Clear();
            Destroy(other.gameObject);
            float newHead = list[0].d + list[0].c.def.Half;
            head = newHead;
            Relayout();
            speed = 0f;
            MadMax.Audio.Sfx.Play("hit_metal", HeadPoint, 0.9f, 0.6f, 60f);
            MadMax.Audio.Sfx.Play("chain", HeadPoint, 0.6f, 0.9f, 40f);
            WastelandGame.Instance?.Toast("COUPLED: " + cars.Count + (cars.Count == 1 ? " CAR" : " CARS"));
            owned = owned || other.owned;
        }

        /// <summary>Uncouple the car farthest from the loco: it stays where it stands as a train of its own.</summary>
        public Train UncoupleLast(Material mat, Material glow)
        {
            if (cars.Count < 2) return null;
            var loco = Loco;
            int li = loco ? cars.IndexOf(loco) : 0;
            int idx = li < cars.Count / 2f ? cars.Count - 1 : 0;
            var car = cars[idx];
            float d = head - offsets[idx];
            cars.RemoveAt(idx);
            if (idx == 0) head -= car.def.length + 0.5f;
            Relayout();
            var go = new GameObject("Train_" + car.def.name);
            var t = go.AddComponent<Train>();
            t.net = net; t.path = path.Clone(); t.head = d + car.def.Half; t.trainName = car.def.name; t.owned = owned;
            car.train = t; t.cars.Add(car);
            t.Relayout(); t.placed = true;
            MadMax.Audio.Sfx.Play("chain", car.transform.position, 0.6f, 1f, 40f);
            return t;
        }

        // ------------------------------------------------------------------ hits

        public void OnHit(RailCar car, GameObject source, float power)
        {
            var g = WastelandGame.Instance;
            bool byPlayer = g && source && ((g.Player && source.transform.IsChildOf(g.Player.transform)) || (g.Current && source.transform.IsChildOf(g.Current.transform)));
            if (service != null && byPlayer) service.Hit(power);
        }

        public void OnBroken(RailCar car)
        {
            if (service != null) service.CarBroken(this, car);
        }

        // ------------------------------------------------------------------ sound and smoke

        void Update()
        {
            var loco = Loco;
            float load = service != null ? service.Load : driven ? throttle : 0f;
            if (loco && !loco.def.hand && (service != null || driven))
            {
                MadMax.Audio.Sfx.Loop(loco, "engine_diesel", 0.35f + 0.45f * load, 0.42f + 0.25f * load + Mathf.Abs(speed) * 0.01f, 220f);
                if (Time.time >= smokeAt)
                {
                    smokeAt = Time.time + Mathf.Lerp(0.5f, 0.12f, load);
                    var stack = loco.transform.TransformPoint(loco.def.kind == CarKind.ArmoredLoco ? new Vector3(0f, 4.7f, 2.5f) : new Vector3(0.3f, 4.5f, 3.5f));
                    Fx.Smoke(stack, Vector3.up * (1.5f + load * 2f) + loco.Velocity * 0.4f, 0.5f + load * 0.6f, new Color(0.16f, 0.15f, 0.14f, 0.7f), 3.5f);
                }
            }
            var near = NearestCar(MadMax.Audio.Sfx.ListenerPosition);
            float sp = Mathf.Abs(speed);
            if (near && sp > 0.5f) MadMax.Audio.Sfx.Loop(near, "rail_roll", Mathf.Clamp01(sp / 14f) * 0.7f, 0.6f + sp * 0.03f, 90f);
            bool braking = (service != null ? service.Braking : brake > 0.4f && driven) && sp > 2f;
            if (braking && cars.Count > 1 && cars[cars.Count - 1]) MadMax.Audio.Sfx.Loop(cars[cars.Count - 1], "rail_squeal", 0.5f, 0.9f + sp * 0.02f, 120f);
            DerailUpdate();
            if (loco && loco.def.hand && loco.Lever) loco.Lever.localRotation = Quaternion.Euler(Mathf.Sin(pumpPhase) * 22f, 0f, 0f);
            // horn ahead of level crossings
            if (sp > 3f && Time.time >= crossingCheckAt)
            {
                crossingCheckAt = Time.time + 1f;
                float front = speed > 0f ? head : head - Length, sign = Mathf.Sign(speed);
                for (float k = 20f; k < 120f; k += 4f)
                {
                    var l = path.LineAt(front + sign * k, out float s, out _);
                    if (l != null && (l.FlagAt(s) & RailLine.Crossing) != 0)
                    {
                        if (Time.time >= hornAt && (service != null || !driven)) { Horn(); hornAt = Time.time + 12f; }
                        break;
                    }
                }
            }
        }

        public float pumpPhase;
        float refusedAt;

        /// <summary>The driver's input from the cab seat (<see cref="MadMax.Building.Seat.ride"/>): W opens the throttle
        /// (or eases the brake off), S closes it then brakes, Space is the emergency brake; on a draisine W / S pump it
        /// along forward / back on stamina.</summary>
        public void Ride(Vector2 move, bool run, bool jump, float dt)
        {
            if (service != null) return;
            var loco = Loco;
            if (!loco) return;
            driven = true; owned = true;
            var g = WastelandGame.Instance;
            if (loco.def.hand)
            {
                bool pump = Mathf.Abs(move.y) > 0.2f && g && g.Vitals && g.Vitals.Spend((run ? 9f : 5f) * dt);
                throttle = pump ? (run ? 1f : 0.7f) : 0f;
                if (pump) { reverser = move.y > 0f ? 1 : -1; pumpPhase += dt * (run ? 9f : 6f); }
                brake = jump ? 1f : pump ? 0f : 0.15f;
                return;
            }
            if (Derailed)
            {
                throttle = 0f; brake = 1f;
                if (Mathf.Abs(move.y) > 0.2f && g && Time.time > refusedAt) { refusedAt = Time.time + 4f; g.Toast("OFF THE RAILS - SHE WON'T MOVE TILL SHE'S JACKED BACK ON (RE-RAIL)"); }
                return;
            }
            float comp = Competence;
            if (comp <= 0f)
            {
                throttle = 0f; brake = 1f;
                if (Mathf.Abs(move.y) > 0.2f && g && Time.time > refusedAt) { refusedAt = Time.time + 4f; g.Toast("THE IRONCLAD'S CONTROLS ARE BEYOND YOU (" + MadMax.RPG.CharacterStats.SkillNames[(int)MadMax.RPG.Skill.Rail] + " " + loco.def.skill + ")"); }
                return;
            }
            float brakeRate = comp < 0.5f ? 0.3f : 1f;
            if (move.y > 0.2f) { if (brake > 0f) brake = Mathf.Max(0f, brake - dt * brakeRate); else throttle = Mathf.Min(1f, throttle + dt * 0.45f); }
            else if (move.y < -0.2f) { if (throttle > 0f) throttle = Mathf.Max(0f, throttle - dt * 1.2f); else brake = Mathf.Min(1f, brake + dt * brakeRate); }
            if (jump) { throttle = 0f; brake = 1f; }
            if (fuel <= 0f && throttle > 0f && g && Time.time > refusedAt) { refusedAt = Time.time + 5f; g.Toast("THE TANK IS DRY - THE ENGINE WON'T RUN (DIESEL, [G] WITH A CAN)"); }
        }

        /// <summary>Flip the reverser (only standing, or nearly).</summary>
        public bool FlipReverser()
        {
            if (Mathf.Abs(speed) > 0.6f) return false;
            reverser = -reverser;
            MadMax.Audio.Sfx.Play("ratchet", Loco ? Loco.transform.position : HeadPoint, 0.6f, 1.2f, 20f);
            return true;
        }

        /// <summary>The direction the train moves in path terms when the throttle pulls (+1 toward the path's front).</summary>
        public int PullSign { get { var l = Loco; return reverser * (l && l.flipped ? -1 : 1); } }

        /// <summary>Points thrown: drop the path planned beyond them so the train reads the new setting.</summary>
        public void Replan(RailNode n)
        {
            if (service != null) return;
            for (int i = path.segs.Count - 1; i > 0; i--)
            {
                var sg = path.segs[i];
                int entry = sg.fwd ? sg.line.a : sg.line.b;
                if (entry == n.id && sg.start > head + 1f) { path.segs.RemoveRange(i, path.segs.Count - i); break; }
            }
            for (int i = 0; i < path.segs.Count - 1; i++)
            {
                var sg = path.segs[i];
                int exit = sg.fwd ? sg.line.b : sg.line.a;
                if (exit == n.id && sg.start + sg.line.Length < head - Length - 1f) { path.segs.RemoveRange(0, i + 1); break; }
            }
        }

        public void Horn()
        {
            var loco = Loco;
            if (!loco) return;
            LastHornTime = Time.time;
            MadMax.Audio.Sfx.Play(loco.def.hand ? "beep" : "rail_horn", loco.transform.position + Vector3.up * 3f, 1f, loco.def.kind == CarKind.ArmoredLoco ? 0.8f : 1f, 500f, 0.3f);
            MadMax.Npc.NpcDirector.Instance?.Noise(loco.transform.position, 80f);
        }

        void OnDestroy()
        {
            foreach (var c in cars) if (c && c.train == this) Destroy(c.gameObject);
        }
    }
}
