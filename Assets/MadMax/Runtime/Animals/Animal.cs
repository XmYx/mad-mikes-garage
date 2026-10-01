using System.Collections.Generic;
using MadMax.Audio;
using MadMax.Building;
using MadMax.Game;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Animals
{
    /// <summary>One animal (roadmap 23). Voxel body with a procedural gait (walk, trot, gallop; birds hop and flap;
    /// snakes slither and coil), a kinematic capsule for hits, ground following with obstacle and water avoidance.
    /// Senses: sight (crouching halves it, night dims it for day animals), hearing (running, engines, gunshots via
    /// <see cref="AnimalDirector.Noise"/>), smell carried by the wind. Nature decides what it does about people: prey
    /// flee with the herd, boars charge, dog and wolf packs stalk and bite in turns (and raid livestock at night),
    /// vultures circle the dead, rats scatter, snakes rattle and strike. Owned animals roam their pen, follow or stay;
    /// guard dogs fight what threatens you. Wild horses and dogs are tamed by a calm approach and treats; a saddled
    /// horse is ridden (walk / trot / gallop on stamina, jumps, saddlebags). Butchered through <see cref="Carcass"/>.</summary>
    [DefaultExecutionOrder(-40)]   // before the rider's PlayerCharacter, so the saddle never lags a frame
    public partial class Animal : MonoBehaviour, IDamageable, IInteractable
    {
        public enum State { Idle, Graze, Wander, Flee, Alert, Charge, Stalk, Attack, Follow, Stay, Sleep, Ridden, Circle, Land, Perch, Coiled, Dead }

        public static readonly List<Animal> All = new List<Animal>();
        /// <summary>Eats crops off unfenced garden beds (hoofed plant-eaters).</summary>
        public bool Grazer => Def != null && !Def.flies && Def.plan == BodyPlan.Quadruped && (Def.nature == Nature.Livestock || Def.nature == Nature.Prey) && Def.id != "chicken" && Def.id != "dog" && state != State.Ridden;
        /// <summary>The animal the local player is riding.</summary>
        public static Animal Mounted { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { All.Clear(); Mounted = null; }

        public AnimalDef Def { get; private set; }
        public AnimalMeshes Mesh { get; private set; }
        public string key;                   // wild "w:cx:cz:herd:i", owned "o:n", village "v:town:i"
        public int herd;                     // members of one herd flee and hunt together (0 = none)
        public bool owned;
        public int town = -1;                // a village's livestock
        public float health, trust, stamina = 100f;
        public int born = -999, fedDay = -1, hungryDays, stock;
        public bool saddled;
        public int order;                    // owned: 0 roam near home, 1 follow, 2 stay
        public Vector3 home;
        public Container bags;
        public State state;
        public Vector3 circleAt;             // vultures: the body they circle
        public Component carcassOf;          // ... and what it is (Carcass or a dead Npc)

        public bool Alive => state != State.Dead;
        public float Age => DayNight.TotalDays - born;
        public bool Adult => !owned || Age >= Def.adultDays;
        public float Size => owned ? Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(Age / Mathf.Max(0.1f, Def.adultDays))) : 1f;
        public bool Winded => winded;
        public string Label => (owned && !Adult ? "YOUNG " : "") + (owned && Def.id == "horse" ? "HORSE" : owned && Def.id == "dog" ? "DOG" : Def.name);

        Transform rig, head, tail, snakeHead, rattle;
        Transform[] legs, wings, segs;
        Transform saddleT;
        CapsuleCollider col;
        Seat seat;
        Vector2 rideMove; bool rideRun, rideJump, winded, jumping, listed, warned;
        Vector3 moveDir, threat, goal, flyVel, avoid;
        float moveSpeed, speed, vy, air, phase, thinkT, stateUntil, attackCd, callT, blockT, runT, hurtUntil, scaredUntil, circAng, circR, circAlt, landAt, headPitch, headYaw, rattleUntil;
        int circDir = 1, packStart;
        Component target;
        Transform hurter;
        AnimalCall call;
        SynthVoice voice;
        int lastHalf;
        static readonly RaycastHit[] hits = new RaycastHit[8];

        // ------------------------------------------------------------------ building

        public static Animal Spawn(AnimalDef d, Vector3 pos, float yaw, Material mat, string key)
        {
            var go = new GameObject("Animal_" + d.id);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var a = go.AddComponent<Animal>();
            a.key = key; a.home = pos;
            a.Setup(d, mat);
            return a;
        }

        void Setup(AnimalDef d, Material mat)
        {
            Def = d; health = d.health; Mesh = AnimalModels.For(d);
            rig = new GameObject("Rig").transform;
            rig.SetParent(transform, false);
            float s = VoxelMesher.DefaultSize * d.scale;
            if (d.plan == BodyPlan.Snake)
            {
                segs = new Transform[Mesh.segments];
                for (int i = 0; i < segs.Length; i++) segs[i] = Part(rig, "Seg", Mesh.segment, Vector3.back * i * Mesh.segLen, mat);
                snakeHead = Part(rig, "Head", Mesh.snakeHead, Vector3.zero, mat);
                rattle = Part(segs[segs.Length - 1], "Rattle", Mesh.rattle, Vector3.back * Mesh.segLen * 0.5f, mat);
                state = State.Coiled;
            }
            else
            {
                Part(rig, "Body", Mesh.body, Vector3.zero, mat);
                head = Part(rig, "Head", Mesh.head, Mesh.neck, mat);
                legs = new Transform[Mesh.legRoots.Length];
                for (int i = 0; i < legs.Length; i++)
                {
                    legs[i] = Part(rig, "Leg", Mesh.leg, Mesh.legRoots[i], mat);
                    if (Mesh.sprawl && Mesh.legRoots[i].x < 0f) legs[i].localScale = new Vector3(-1f, 1f, 1f);   // sprawled legs: the left ones mirror the right
                }
                if (Mesh.tail) tail = Part(rig, "Tail", Mesh.tail, Mesh.tailRoot, mat);
                if (Mesh.wing)
                {
                    wings = new[] { Part(rig, "WingR", Mesh.wing, Mesh.wingRoot, mat), Part(rig, "WingL", Mesh.wing, new Vector3(-Mesh.wingRoot.x, Mesh.wingRoot.y, Mesh.wingRoot.z), mat) };
                    wings[1].localScale = new Vector3(-1f, 1f, 1f);
                }
            }
            col = gameObject.AddComponent<CapsuleCollider>();
            col.direction = 2;
            if (d.plan == BodyPlan.Snake) { col.radius = 0.09f; col.height = 0.6f; col.center = new Vector3(0f, 0.09f, -0.15f); }
            else
            {
                col.radius = Mathf.Max(0.08f, Mathf.Max(d.width, d.depth) * 0.5f * s);
                col.height = Mathf.Max(col.radius * 2.05f, (d.len + d.head) * s);
                col.center = new Vector3(0f, (d.leg + d.depth * 0.5f) * s, d.head * 0.4f * s);
            }
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            ApplySize();
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Vector3 at, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        public void ApplySize() => transform.localScale = Vector3.one * Size;

        public void SetSaddled(bool on)
        {
            saddled = on;
            if (on && !saddleT && Mesh.saddle) saddleT = Part(rig, "Saddle", Mesh.saddle, new Vector3(0f, (Def.leg + Def.depth) * VoxelMesher.DefaultSize * Def.scale, -0.05f), rig.GetComponentInChildren<MeshRenderer>().sharedMaterial);
            if (saddleT) saddleT.gameObject.SetActive(on);
            if (on && !bags)
            {
                var b = new GameObject("Saddlebags");
                b.transform.SetParent(transform, false);
                b.transform.localPosition = Mesh.seat;
                bags = b.AddComponent<Container>();
                bags.title = "SADDLEBAGS"; bags.capacity = 40f;
            }
        }

        void OnEnable() => All.Add(this);
        void OnDisable() { All.Remove(this); List(false); if (Mounted == this) Mounted = null; }

        /// <summary>Only animals with something to offer take the [E] focus (a wandering hen must not hide a workbench).</summary>
        void List(bool on)
        {
            if (on == listed) return;
            listed = on;
            if (on) PartFunctions.Interactables.Add(this); else PartFunctions.Interactables.Remove(this);
        }

        // ------------------------------------------------------------------ update

        // ------------------------------------------------------------------ network proxy (a client's copy of a host animal)
        [System.NonSerialized] public ushort netId;
        [System.NonSerialized] public bool proxy;
        Vector3 proxyPos; float proxyYaw, proxySpeed;

        public State NetState => state;
        public float NetSpeed => speed;
        public float NetSize => Size;

        public void ProxyState(Vector3 pos, float yaw, float sp, State st)
        {
            if (!proxy) return;
            if (proxyPos == Vector3.zero) transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            proxyPos = pos; proxyYaw = yaw; proxySpeed = sp;
            if (st == State.Dead && state != State.Dead) Die(WastelandGame.Instance, false, -transform.right);
            else if (state != State.Dead) state = st == State.Ridden ? State.Wander : st;
        }

        void ProxyTick(float dt)
        {
            transform.position = Vector3.Lerp(transform.position, proxyPos, 1f - Mathf.Exp(-12f * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, proxyYaw, 0f), 1f - Mathf.Exp(-10f * dt));
            speed = proxySpeed;
            Animate(dt);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var g = WastelandGame.Instance;
            if (proxy) { if (dt > 0f && state != State.Dead && proxyPos != Vector3.zero) ProxyTick(dt); return; }
            if (dt <= 0f || !g || !g.Player || state == State.Dead) return;
            CareTick(g, dt);                                                                          // wounds, wool, stable rest (Animal.Husbandry)
            if (state == State.Dead) return;
            var focus = g.Current ? g.Current.transform.position : g.Player.transform.position;
            if (state == State.Ridden && (!g.Player.Sitting || g.Player.SeatedOn != seat)) { state = State.Stay; order = 2; home = transform.position; Mounted = null; }
            if (state != State.Ridden && (transform.position - focus).sqrMagnitude > 170f * 170f) return;     // far away (a distant pen): frozen
            if ((thinkT -= dt) <= 0f)
            {
                thinkT = 0.2f + Random.value * 0.1f;
                if (state != State.Ridden) Think(g, focus);
                List(!Def.flies && state != State.Ridden && (owned || CareListed(g) || (Def.tameable && town < 0 && state != State.Attack && state != State.Stalk)));
            }
            if (state == State.Ridden) RideTick(g, dt);
            else if (Def.flies && (state == State.Circle || state == State.Land || air > 0.05f)) FlyTick(dt);
            else Walk(g, dt);
            Animate(dt);
            if (voice && call != null && !call.Busy) voice.SetActive(false);
            if ((runT -= dt) <= 0f) { runT = 0.1f; RunOver(g); }
        }

        void SetMove(Vector3 dir, float sp) { dir.y = 0f; moveDir = dir; moveSpeed = dir.sqrMagnitude > 0.0001f ? sp * Gait : 0f; }
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ------------------------------------------------------------------ thinking

        void Think(WastelandGame g, Vector3 focus)
        {
            bool inCar = g.Current;
            bool onFoot = !inCar && !g.Player.SeatedIn;
            float d = Flat(focus - transform.position).magnitude;
            bool aware = Senses(g, focus, d, onFoot, inCar) || Time.time < hurtUntil;
            if (CareThink()) return;                                                                  // down with its wounds
            if (owned || town >= 0) { ThinkKept(g, focus); return; }
            switch (Def.nature)
            {
                case Nature.Prey: ThinkPrey(g, focus, d, aware, onFoot, inCar); break;
                case Nature.Charger: ThinkCharger(g, focus, d, aware, onFoot, inCar); break;
                case Nature.Predator: ThinkPredator(g, focus, d, aware, onFoot, inCar); break;
                case Nature.Scavenger: ThinkScavenger(focus, d, onFoot); break;
                case Nature.Vermin: ThinkVermin(g, focus, d, aware, onFoot); break;
                case Nature.Lurker: ThinkLurker(g, focus, d, onFoot); break;
                default: Routine(8f); break;
            }
        }

        /// <summary>Sight (crouching halves it, night dims it for day animals, vehicles stand out), hearing (feet,
        /// engines) and smell (only downwind of you).</summary>
        bool Senses(WastelandGame g, Vector3 focus, float d, bool onFoot, bool inCar)
        {
            var p = g.Player;
            float see = Def.sight * (inCar ? 1.3f : p.Crouching ? 0.5f : 1f) * (Def.nocturnal ? 1f : Mathf.Lerp(1f, 0.55f, DayNight.Darkness));
            if (d < see) return true;
            float v = p.Velocity.magnitude;
            float noise = inCar ? 20f + Mathf.Abs(g.Current.ForwardSpeed) * 2.5f : p.Sliding || (p.run && v > 3f) ? 16f : p.Crouching ? 2.5f : v > 0.5f ? 7f : 3f;
            if (d < noise * Def.hearing / 40f) return true;
            if (!onFoot || Def.smell <= 0f) return false;
            var w = Fx.Wind; w.y = 0f;
            float ws = w.magnitude;
            var toMe = Flat(transform.position - focus);
            float along = ws > 0.1f && toMe.sqrMagnitude > 0.01f ? Vector3.Dot(toMe.normalized, w / ws) : 0f;
            return d < Def.smell * Mathf.Clamp01(0.15f + along) * Mathf.Clamp01(0.4f + ws * 0.15f);
        }

        /// <summary>Daily rhythm: graze, wander about the home spot, stand, sleep (day animals at night and the reverse).</summary>
        void Routine(float radius)
        {
            var me = transform.position;
            if (Time.time < stateUntil && (state == State.Graze || state == State.Wander || state == State.Idle || state == State.Sleep || state == State.Coiled || state == State.Perch))
            {
                if (state == State.Wander) { if (Flat(goal - me).magnitude < 0.7f) { state = State.Idle; SetMove(Vector3.zero, 0f); } else SetMove(goal - me, Def.walk); }
                else SetMove(Vector3.zero, 0f);
                return;
            }
            bool sleepy = Def.nocturnal ? DayNight.Darkness < 0.3f : DayNight.Darkness > 0.6f;
            float r = Random.value;
            if (Def.plan == BodyPlan.Snake)
            {
                var off = Random.insideUnitCircle * 3f;
                goal = me + new Vector3(off.x, 0f, off.y);
                state = r < 0.8f ? State.Coiled : State.Wander; stateUntil = Time.time + Random.Range(8f, 20f);
            }
            else if (sleepy && r < 0.55f && !Def.flies) { state = State.Sleep; stateUntil = Time.time + Random.Range(15f, 40f); }
            else if (r < 0.45f) { state = State.Graze; stateUntil = Time.time + Random.Range(4f, 10f); }
            else if (r < 0.8f)
            {
                var off = Random.insideUnitCircle * radius;
                goal = home + new Vector3(off.x, 0f, off.y);
                if (Grazer && Random.value < 0.4f)                                                // a vegetable bed nobody fenced
                    foreach (var plot in MadMax.Building.GardenPlot.All)
                        if (plot && plot.Tempting && Flat(plot.transform.position - me).magnitude < 16f) { goal = plot.transform.position; break; }
                state = State.Wander; stateUntil = Time.time + 14f;
            }
            else { state = State.Idle; stateUntil = Time.time + Random.Range(2f, 5f); }
            if (state != State.Sleep && Random.value < 0.07f) Say(0);
            if (state == State.Wander) SetMove(goal - me, Def.walk); else SetMove(Vector3.zero, 0f);
        }

        void Flee(Vector3 from, float seconds)
        {
            var me = transform.position;
            threat = from;
            if (state != State.Flee) Say(1);
            state = State.Flee; stateUntil = Time.time + seconds;
            var away = Flat(me - from);
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            SetMove(Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f) * away.normalized, Def.run);
        }

        /// <summary>The herd bolts together.</summary>
        void Alarm(Vector3 from)
        {
            if (herd == 0) return;
            foreach (var a in All)
                if (a != this && a.herd == herd && a.Alive && !a.owned && a.state != State.Flee && (a.transform.position - transform.position).sqrMagnitude < 40f * 40f)
                    a.Flee(from, 6f + Random.value * 4f);
        }

        float FlightDistance(WastelandGame g, bool inCar)
        {
            float f = Def.sight * 0.5f;
            if (inCar) f *= 1.4f + Mathf.Abs(g.Current.ForwardSpeed) * 0.05f;
            else if (g.Player.Crouching) f *= 0.55f;
            if (Def.tameable) f *= 1f - trust * 0.9f;
            // half won over by a calm approach: it lets a crouched or still person come within arm's reach for the treat
            // (at 50 % trust the flight distance above still kept the player ~6 m out, beyond the 2.2 m reach to feed it)
            if (Def.tameable && trust >= 0.5f && !inCar && !(g.Player.run && g.Player.Velocity.magnitude > 3f)
                && (g.Player.Crouching || g.Player.Velocity.magnitude < 0.4f)) f = Mathf.Min(f, 1.2f);
            return f;
        }

        void ThinkPrey(WastelandGame g, Vector3 focus, float d, bool aware, bool onFoot, bool inCar)
        {
            if (Def.tameable && onFoot && d < 12f)
            {
                // a calm approach earns a little trust (up to half); running at it loses it
                bool rushing = g.Player.run && g.Player.Velocity.magnitude > 3f;
                if (rushing) trust = Mathf.Max(0f, trust - 0.04f);
                else if (g.Player.Crouching || g.Player.Velocity.magnitude < 0.4f) trust = Mathf.Min(Mathf.Max(trust, 0.5f), trust + 0.006f);
            }
            if ((aware && d < FlightDistance(g, inCar)) || Time.time < hurtUntil)
            {
                Flee(Time.time < hurtUntil && hurter ? hurter.position : focus, 6f + Random.value * 4f);
                Alarm(focus);
                return;
            }
            if (state == State.Flee)
            {
                if (Time.time < stateUntil) return;
                home = transform.position;
            }
            if (aware && d < Def.sight * 0.8f && !(Def.tameable && trust > 0.3f))
            {
                if (state != State.Alert) { state = State.Alert; stateUntil = Time.time + 3f; }
                SetMove(Vector3.zero, 0f);
                FaceTo(focus);
                return;
            }
            Routine(12f);
        }

        void ThinkCharger(WastelandGame g, Vector3 focus, float d, bool aware, bool onFoot, bool inCar)
        {
            if (health < Def.health * 0.3f) { if (state != State.Flee || Time.time > stateUntil) Flee(focus, 8f); return; }
            bool provoked = Time.time < hurtUntil || (aware && d < 9f && onFoot);
            if (provoked && onFoot && !g.Vitals.Dead)
            {
                if (state == State.Flee && Time.time < stateUntil) return;             // running past after a hit, then turns
                target = g.Player; state = State.Charge;
                SetMove(focus - transform.position, Def.run);
                if (d < Def.reach + 0.5f && Time.time > attackCd) { Bite(g); attackCd = Time.time + 2f; Flee(focus, 1.2f); }
                return;
            }
            if (inCar && aware && d < 14f) { if (state != State.Flee || Time.time > stateUntil) Flee(focus, 5f); return; }
            if (state == State.Flee && Time.time < stateUntil) return;
            if (aware && d < 20f) { state = State.Alert; SetMove(Vector3.zero, 0f); FaceTo(focus); if (Random.value < 0.08f) Say(0); return; }
            Routine(10f);
        }

        void ThinkPredator(WastelandGame g, Vector3 focus, float d, bool aware, bool onFoot, bool inCar)
        {
            var me = transform.position;
            int alive = 0;
            foreach (var a in All) if (herd != 0 && a.herd == herd && a.Alive) alive++;
            if (packStart == 0) packStart = Mathf.Max(1, alive);
            if (Def.id == "wolf" && DayNight.Darkness > 0.6f && Random.value < 0.004f) Say(2);
            if (alive * 2 < packStart || health < Def.health * 0.35f || Time.time < scaredUntil) { if (state != State.Flee || Time.time > stateUntil) Flee(focus, 10f); return; }
            bool night = DayNight.Darkness > 0.5f;
            bool bold = night || alive >= 3 || g.Stats.health < g.Stats.MaxHealth * 0.4f;
            if (inCar)
            {
                if (aware && d < 30f)
                {
                    if (Mathf.Abs(g.Current.ForwardSpeed) > 8f && d < 12f) { if (state != State.Flee || Time.time > stateUntil) Flee(focus, 3f); }
                    else { state = State.Stalk; SetMove(focus - me, d > 9f ? Def.run * 0.8f : 0f); if (Random.value < 0.1f) Say(0); }
                    return;
                }
                if (state == State.Flee && Time.time < stateUntil) return;
                Routine(15f);
                return;
            }
            if (aware && onFoot && d < 45f && bold && !g.Vitals.Dead)
            {
                target = g.Player;
                if (state == State.Attack)
                {
                    SetMove(focus - me, Def.run);
                    if (d < Def.reach + 0.4f && Time.time > attackCd) { Bite(g); attackCd = Time.time + 1.6f; state = State.Stalk; circDir = Random.value < 0.5f ? 1 : -1; }
                    return;
                }
                if (d < 14f && Time.time > attackCd && Random.value < 0.14f) { state = State.Attack; Say(0); return; }
                // circle at 9-12 m, then dart in by turns
                state = State.Stalk;
                var rel = Flat(me - focus);
                float ang = Mathf.Atan2(rel.z, rel.x) + circDir * 0.45f;
                var spot = focus + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Mathf.Lerp(9f, 12f, Mathf.PerlinNoise(herd, Time.time * 0.2f));
                SetMove(spot - me, Def.run * 0.6f);
                if (Random.value < 0.05f) Say(1);
                return;
            }
            // at night the pack goes for livestock when nobody is near
            if (night && (!aware || d > 40f))
            {
                var prey = NearestLivestock(me, 35f);
                if (prey)
                {
                    target = prey; state = State.Attack;
                    SetMove(prey.transform.position - me, Def.run);
                    if (Flat(prey.transform.position - me).magnitude < Def.reach + prey.col.radius && Time.time > attackCd)
                    {
                        attackCd = Time.time + 1.4f;
                        prey.ApplyHit(prey.transform.position + Vector3.up * 0.4f, transform.forward, Def.bite / 28f, 0.3f, gameObject);
                        Say(1);
                    }
                    return;
                }
            }
            if (aware && d < 30f) { state = State.Alert; SetMove(Vector3.zero, 0f); FaceTo(focus); if (Random.value < 0.08f) Say(0); return; }
            if (state == State.Flee && Time.time < stateUntil) return;
            Routine(15f);
        }

        Animal NearestLivestock(Vector3 me, float range)
        {
            Animal best = null; float bd = range * range;
            foreach (var a in All)
            {
                if (!a.Alive || !(a.owned || a.town >= 0) || a.Def.guard || a.Def.rideable) continue;
                float dd = (a.transform.position - me).sqrMagnitude;
                if (dd < bd) { bd = dd; best = a; }
            }
            return best;
        }

        void ThinkScavenger(Vector3 focus, float d, bool onFoot)
        {
            bool bodyGone = !carcassOf || (carcassOf is Carcass c && c.butchered);
            if (bodyGone || state == State.Flee)
            {
                // off to the horizon, then gone
                if (state != State.Flee) { state = State.Flee; stateUntil = Time.time + 20f; threat = focus; }
                if (Time.time > stateUntil) Destroy(gameObject);
                return;
            }
            bool close = d < (onFoot ? 18f : 25f);
            if (state == State.Perch || state == State.Land)
            {
                if (close) { state = State.Circle; landAt = Time.time + 25f; Say(0); return; }
                if (state == State.Perch)
                {
                    // hop about the body, pecking
                    var r = Random.insideUnitCircle;
                    SetMove(Random.value < 0.3f ? circleAt + new Vector3(r.x, 0f, r.y) * 1.5f - transform.position : Vector3.zero, 0.6f);
                }
                return;
            }
            if (state != State.Circle) { state = State.Circle; landAt = Time.time + Random.Range(25f, 60f); }
            if (Time.time > landAt && !close) { state = State.Land; goal = circleAt + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f)); }
        }

        void ThinkVermin(WastelandGame g, Vector3 focus, float d, bool aware, bool onFoot)
        {
            var t = DeformableTerrain.Instance;
            bool bitey = DayNight.Darkness > 0.5f && t && t.BiomeAt(transform.position.x, transform.position.z) == Biome.Nuclear;
            if (bitey && onFoot && d < 4f && !g.Vitals.Dead)
            {
                target = g.Player; state = State.Attack; SetMove(focus - transform.position, Def.run);
                if (d < Def.reach + 0.3f && Time.time > attackCd) { Bite(g); attackCd = Time.time + 1.5f; }
                return;
            }
            if ((aware && d < 7f) || Time.time < hurtUntil) { Flee(focus, 3f + Random.value * 2f); SetMove(Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * moveDir, Def.run); return; }
            if (state == State.Flee && Time.time < stateUntil) return;
            Routine(6f);
        }

        void ThinkLurker(WastelandGame g, Vector3 focus, float d, bool onFoot)
        {
            if (Time.time < hurtUntil) { if (state != State.Flee) Flee(focus, 5f); return; }
            if (state == State.Flee && Time.time < stateUntil) return;
            if (onFoot && d < 5f && !g.Vitals.Dead)
            {
                state = State.Coiled; SetMove(Vector3.zero, 0f); FaceTo(focus);
                rattleUntil = Time.time + 1.5f;
                Say(0);
                if (!warned) { warned = true; g.Toast("A RATTLE IN THE ROCKS - BACK OFF SLOWLY"); }
                if (d < Def.reach && Time.time > attackCd)
                {
                    attackCd = Time.time + 2.5f;
                    Bite(g);
                    g.Stats.sick = Mathf.Max(g.Stats.sick, 60f);                          // venom: sick for a while
                    g.Toast("SNAKEBITE! THE VENOM MAKES YOU SICK");
                }
                return;
            }
            Routine(3f);
        }

        /// <summary>Owned animals and village livestock: calm, near home; follow or stay on order; guard dogs fight.</summary>
        void ThinkKept(WastelandGame g, Vector3 focus)
        {
            var me = transform.position;
            if (Def.guard && Adult && GuardTick(g, focus)) return;
            if (Time.time < hurtUntil && !Def.guard && hurter) { if (state != State.Flee || Time.time > stateUntil) Flee(hurter.position, 4f); return; }
            if (state == State.Flee && Time.time < stateUntil) return;
            if (owned && order == 1)
            {
                float dd = Flat(focus - me).magnitude;
                if (dd > 3.5f) { state = State.Follow; SetMove(focus - me, dd > 12f ? Def.run * 0.75f : Def.walk * 1.5f); }
                else { state = State.Idle; SetMove(Vector3.zero, 0f); }
                return;
            }
            if (owned && order == 2) { if (state != State.Graze && state != State.Idle && state != State.Sleep) state = State.Idle; SetMove(Vector3.zero, 0f); return; }
            Routine(owned ? 5f : 10f);
        }

        /// <summary>A guard dog goes for hostile people and dangerous animals near it (or near you when following).</summary>
        bool GuardTick(WastelandGame g, Vector3 focus)
        {
            var me = transform.position;
            var around = owned && order == 1 ? focus : me;
            Component foe = null; float bd = 16f * 16f;
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || !n.Hostile) continue;
                float dd = (n.transform.position - around).sqrMagnitude;
                if (dd < bd) { bd = dd; foe = n; }
            }
            foreach (var a in All)
            {
                if (!a.Alive || a.owned || a.town >= 0 || (a.Def.nature != Nature.Predator && a.Def.nature != Nature.Charger && a.Def.nature != Nature.Lurker)) continue;
                float dd = (a.transform.position - around).sqrMagnitude;
                if (dd < bd) { bd = dd; foe = a; }
            }
            if (!foe) return false;
            target = foe; state = State.Attack;
            var at = foe.transform.position;
            SetMove(at - me, Def.run);
            if (Random.value < 0.15f) Say(0);
            if (Flat(at - me).magnitude < Def.reach + 0.5f && Time.time > attackCd)
            {
                attackCd = Time.time + 1.2f;
                if (foe is IDamageable dmg) dmg.ApplyHit(at + Vector3.up * 0.8f, transform.forward, Def.bite / 28f, 0.3f, gameObject);
            }
            return true;
        }

        void Bite(WastelandGame g)
        {
            float dmg = Def.bite * (Def.nature == Nature.Predator && DayNight.Darkness < 0.4f ? 0.8f : 1f);
            g.Vitals.Hurt(dmg, "BITE");
            if (Def.Has("venom"))
            {
                g.Stats.sick = Mathf.Max(g.Stats.sick, 90f + Def.bite * 12f);                      // venom: sick until it wears off or antivenom
                g.Toast("ENVENOMED: " + Def.name + " BITE. ANTIVENOM HELPS");
            }
            MadMax.Audio.Sfx.Play("punch", g.Player.transform.position + Vector3.up, 0.8f, Def.mass > 80f ? 0.8f : 1.3f);
            if (Def.nature == Nature.Charger && g.cameraRig) g.cameraRig.Shake(3f);
            Say(1);
        }

        void FaceTo(Vector3 at)
        {
            var d = Flat(at - transform.position);
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 40f);
        }

        // ------------------------------------------------------------------ noises

        /// <summary>A loud noise: prey and vermin bolt, predators scatter from gunfire, livestock startle, vultures lift.</summary>
        public void Hear(Vector3 at, float loud)
        {
            if (!Alive || state == State.Ridden || Calm) return;
            if (owned || town >= 0) { if (!Def.guard && loud > 30f) Flee(at, 2.5f); return; }
            switch (Def.nature)
            {
                case Nature.Predator: if (loud >= 40f) { scaredUntil = Time.time + 12f; Flee(at, 10f); } else if (state != State.Attack) { state = State.Alert; FaceTo(at); } break;
                case Nature.Scavenger: if (state == State.Perch || state == State.Land) { state = State.Circle; landAt = Time.time + 30f; } break;
                case Nature.Lurker: break;
                case Nature.Charger: if (loud >= 40f && health < Def.health) Flee(at, 6f); break;
                default: Flee(at, 6f + Random.value * 4f); if (Def.tameable) trust = Mathf.Max(0f, trust - 0.2f); break;
            }
        }

        // ------------------------------------------------------------------ moving

        void Walk(WastelandGame g, float dt)
        {
            var me = transform.position;
            float target = moveSpeed;
            var dir = moveDir;
            if (target > 0.05f && dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
                if ((blockT -= dt) <= 0f) { blockT = 0.15f; avoid = Avoid(g, dir, target); }
                if (avoid.sqrMagnitude > 0.001f) dir = avoid;
                float turn = (Def.mass > 200f ? 170f : 320f) * dt;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turn);
                target *= 0.25f + 0.75f * Mathf.Clamp01(Vector3.Dot(transform.forward, dir));    // turn before running off
            }
            else target = 0f;
            float acc = target > speed ? Def.run * 0.9f : Def.run * 1.6f;
            speed = Mathf.MoveTowards(speed, target, acc * dt);
            var p = me + transform.forward * speed * dt;
            p.y = Ground(g, p) + air;
            transform.position = p;
        }

        Vector3 Avoid(WastelandGame g, Vector3 dir, float sp)
        {
            if (!Blocked(g, dir, sp)) return Vector3.zero;
            for (int i = 1; i <= 3; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    var d2 = Quaternion.Euler(0f, side * 45f * i, 0f) * dir;
                    if (!Blocked(g, d2, sp)) return d2;
                }
            return -dir;
        }

        bool Blocked(WastelandGame g, Vector3 dir, float sp)
        {
            var me = transform.position;
            float look = 0.6f + sp * 0.3f + col.radius;
            var ahead = me + dir * look;
            var t = DeformableTerrain.Instance;
            if (t && !Def.flies)
            {
                if (t.WaterDepthNoLoad(ahead.x, ahead.z) > (Def.mass > 200f ? 0.9f : 0.3f)) return true;
                if (t.HeightNoLoad(ahead.x, ahead.z) - me.y > 1.1f) return true;
            }
            float y = Mathf.Max(0.2f, Def.Height * 0.5f);
            int n = Physics.SphereCastNonAlloc(me + Vector3.up * y, Mathf.Min(0.25f, col.radius), dir, hits, look, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c.transform.IsChildOf(transform) || c.GetComponentInParent<DeformableTerrain>()) continue;
                if (target && c.transform.IsChildOf(target.transform)) continue;
                if (g.Player && c.transform.IsChildOf(g.Player.transform)) continue;
                return true;
            }
            return false;
        }

        /// <summary>Terrain, or a floor / foundation / ramp standing on it.</summary>
        float Ground(WastelandGame g, Vector3 p)
        {
            var t = DeformableTerrain.Instance;
            float h = t ? t.HeightNoLoad(p.x, p.z) : p.y;
            if (Physics.Raycast(new Vector3(p.x, Mathf.Max(p.y, h) + 0.9f, p.z), Vector3.down, out var hit, 1.8f, ~0, QueryTriggerInteraction.Ignore)
                && hit.point.y > h + 0.05f && hit.normal.y > 0.6f && !hit.collider.transform.IsChildOf(transform) && !(g.Player && hit.collider.transform.IsChildOf(g.Player.transform)) && !hit.collider.GetComponentInParent<Animal>())
                return hit.point.y;
            return h;
        }

        void FlyTick(float dt)
        {
            var g = WastelandGame.Instance;
            var me = transform.position;
            float ground = Ground(g, me);
            Vector3 want; float sp = Def.run;
            if (state == State.Circle)
            {
                if (circR <= 0f) { circR = Random.Range(7f, 11f); circAlt = Random.Range(20f, 32f); circDir = Random.value < 0.5f ? 1 : -1; circAng = Random.value * 6.28f; }
                circAng += dt * sp / circR * 0.8f * circDir;
                want = circleAt + new Vector3(Mathf.Cos(circAng), 0f, Mathf.Sin(circAng)) * circR;
                want.y = circleAt.y + circAlt;
            }
            else if (state == State.Land) { want = goal; sp = 5f; }
            else if (state == State.Flee) { var away = Flat(me - threat); if (away.sqrMagnitude < 0.01f) away = transform.forward; want = me + away.normalized * 30f; want.y = ground + 40f; }
            else { want = me; want.y = ground; sp = 3f; }                                          // came down: settle
            var vel = want - me;
            if (vel.magnitude > sp) vel = vel.normalized * sp;
            flyVel = Vector3.Lerp(flyVel, vel, 1f - Mathf.Exp(-2.5f * dt));
            var p = me + flyVel * dt;
            if (p.y < ground) p.y = ground;
            transform.position = p;
            var fwd = new Vector3(flyVel.x, flyVel.y * 0.3f, flyVel.z);
            if (fwd.sqrMagnitude > 0.05f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd), 1f - Mathf.Exp(-4f * dt));
            air = p.y - ground;
            speed = flyVel.magnitude;
            if (state == State.Land && air < 0.15f) { state = State.Perch; air = 0f; flyVel = Vector3.zero; transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f); stateUntil = Time.time + 30f; }
        }

        // ------------------------------------------------------------------ riding

        /// <summary>A vulture over a body: circle it high, land when nobody is near.</summary>
        public void Circle(Vector3 at, Component body) { circleAt = at; carcassOf = body; state = State.Circle; circR = 0f; }

        /// <summary>Climb into the saddle.</summary>
        public void Mount(WastelandGame g)
        {
            if (!seat)
            {
                var go = new GameObject("RiderSeat");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Mesh.seat;
                seat = go.AddComponent<Seat>();
                seat.hidden = true; seat.rest = 1f; seat.reading = 1f; seat.hasExit = true;
                seat.exitLocal = new Vector3(-(Def.width * 0.5f + 8f) * VoxelMesher.DefaultSize * Def.scale, -Mesh.seat.y + 0.05f, 0f);
                seat.ride = Ride;
            }
            g.Player.SitOn(seat, Vector3.zero);
            state = State.Ridden; Mounted = this; speed = 0f;
            MadMax.Audio.Sfx.Play("chest", transform.position, 0.4f, 1.3f);
            g.Toast("W TROT, SHIFT GALLOP, SPACE JUMP, F DISMOUNT");
        }

        void Ride(Vector2 move, bool run, bool jump, float dt) { rideMove = move; rideRun = run; if (jump) rideJump = true; }

        void RideTick(WastelandGame g, float dt)
        {
            var me = transform.position;
            float target = rideMove.y > 0.1f ? (rideRun && !winded ? Def.run : Mathf.Lerp(Def.walk, 4.2f, rideMove.y)) : rideMove.y < -0.1f ? -1.1f : 0f;
            speed = Mathf.MoveTowards(speed, target, (Mathf.Abs(target) > Mathf.Abs(speed) ? 3.2f : 7f) * dt);
            transform.Rotate(0f, rideMove.x * Mathf.Lerp(110f, 55f, Mathf.Abs(speed) / Def.run) * dt, 0f);
            if (speed > 6f) { stamina -= 11f * dt * StaminaUse; if (stamina <= 0f) { stamina = 0f; winded = true; g.Toast("YOUR HORSE IS WINDED"); } }
            else stamina = Mathf.Min(100f, stamina + (speed < 2f ? 9f : 3f) * dt);
            if (winded && stamina > 35f) winded = false;
            if (rideJump && !jumping && stamina > 8f) { vy = 6.2f; jumping = true; stamina -= 8f; Say(1); }
            rideJump = false;
            if (Mathf.Abs(speed) > 0.05f)
            {
                var dir = transform.forward * Mathf.Sign(speed);
                float clear = jumping ? air + 0.35f : 0.35f;                                         // in the air only what is taller than the jump stops us
                float look = 0.9f + Mathf.Abs(speed) * dt;
                var t = DeformableTerrain.Instance;
                var ahead = me + dir * look;
                bool wall = t && (t.WaterDepthNoLoad(ahead.x, ahead.z) > 1.1f || t.HeightNoLoad(ahead.x, ahead.z) - me.y > 1.2f);
                if (!wall)
                {
                    int n = Physics.SphereCastNonAlloc(me + Vector3.up * (clear + 0.3f), 0.3f, dir, hits, look, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n && !wall; i++)
                    {
                        var c = hits[i].collider;
                        if (c.transform.IsChildOf(transform) || c.transform.IsChildOf(g.Player.transform) || c.GetComponentInParent<DeformableTerrain>()) continue;
                        wall = true;
                    }
                }
                if (wall)
                {
                    if (Mathf.Abs(speed) > 8f)
                    {
                        MadMax.Audio.Sfx.Play("hit_wood", me + Vector3.up, 0.8f, 0.7f);
                        g.Player.StandUp();
                        g.Vitals.Hurt(8f, "FALL");
                        g.Toast("THROWN FROM THE SADDLE");
                        state = State.Stay; order = 2; Mounted = null;
                    }
                    speed = 0f;
                }
            }
            var p = me + transform.forward * speed * dt;
            if (jumping) { air += vy * dt; vy -= 20f * dt; if (air <= 0f) { air = 0f; jumping = false; MadMax.Audio.Sfx.Play("dig", me, 0.4f, 0.7f); } }
            p.y = Ground(g, p) + air;
            transform.position = p;
        }

        // ------------------------------------------------------------------ animation

        void Animate(float dt)
        {
            float s = speed;
            if (Def.plan == BodyPlan.Snake) { AnimateSnake(dt); return; }
            if (Mesh.sprawl) { AnimateSprawl(dt); return; }
            bool lying = state == State.Sleep;
            float legLen = Mathf.Max(0.05f, Mesh.legLen);
            phase += s / (legLen * 2.6f) * dt;
            float wphase = phase * Mathf.PI * 2f;
            // hooves (the big ones) on each half stride, near the listener
            int half = Mathf.FloorToInt(phase * 2f);
            if (half != lastHalf)
            {
                lastHalf = half;
                if (s > 0.4f && Def.mass >= 150f && Def.plan == BodyPlan.Quadruped && MadMax.Audio.Sfx.HasListener && (transform.position - MadMax.Audio.Sfx.ListenerPosition).sqrMagnitude < 30f * 30f)
                    MadMax.Audio.Sfx.Play("hoof", transform.position, Mathf.Clamp(0.25f + s * 0.05f, 0.25f, 0.6f), Random.Range(0.85f, 1.1f) * (Def.mass > 400f ? 0.85f : 1f), 30f);
            }
            float gallop = Mathf.InverseLerp(Def.walk * 2.5f, Def.run * 0.8f, s);
            float swing = Mathf.Clamp01(s / Mathf.Max(0.3f, Def.walk)) * 24f + gallop * 14f;
            if (Def.plan == BodyPlan.Bird)
            {
                bool flying = air > 0.2f;
                for (int i = 0; i < legs.Length; i++) legs[i].localRotation = Quaternion.Euler(flying ? 60f : Mathf.Sin(wphase + i * Mathf.PI) * swing * 1.3f, 0f, 0f);
                if (wings != null)
                {
                    bool flap = (flying && (flyVel.y > 0.4f || state == State.Flee || speed < 3f)) || (!flying && state == State.Flee);
                    float wing = flap ? Mathf.Sin(Time.time * (Def.flies ? 9f : 18f)) * 50f : flying ? 8f + Mathf.Sin(Time.time * 1.3f) * 4f : 0f;
                    float fold = flying || flap ? 0f : Def.flies ? 75f : 0f;
                    wings[0].localRotation = Quaternion.Euler(0f, -fold, wing);
                    wings[1].localRotation = Quaternion.Euler(0f, -fold, wing);
                }
                // hens bob the head with each step and peck when grazing
                float peck = state == State.Graze || state == State.Perch ? Mathf.Max(0f, Mathf.Sin(Time.time * 6f + herd)) * 70f : 0f;
                head.localPosition = Mesh.neck + Vector3.forward * (Mathf.Sin(wphase * 2f) * 0.015f * Mathf.Clamp01(s));
                head.localRotation = Quaternion.Euler(peck, Mathf.PerlinNoise(Time.time * 0.8f, herd) * 60f - 30f, 0f);
                return;
            }
            // four legs: walk (lateral sequence), trot (diagonal pairs), gallop (front pair, then the hind pair)
            float[] off = gallop > 0.5f ? Gallop : s > Def.walk * 1.8f ? Trot : WalkSeq;
            for (int i = 0; i < 4; i++)
            {
                float a = lying ? (i < 2 ? -80f : 80f) : Mathf.Sin(wphase + off[i] * Mathf.PI * 2f) * swing;
                legs[i].localRotation = Quaternion.Slerp(legs[i].localRotation, Quaternion.Euler(a, 0f, 0f), 1f - Mathf.Exp(-20f * dt));
            }
            float bob = Mathf.Abs(Mathf.Sin(wphase * 2f)) * legLen * 0.05f * Mathf.Clamp01(s / Def.walk);
            float lie = lying ? -legLen * 0.8f : 0f;
            rig.localPosition = Vector3.Lerp(rig.localPosition, new Vector3(0f, bob + lie, 0f), 1f - Mathf.Exp(-10f * dt));
            rig.localRotation = Quaternion.Euler(gallop * Mathf.Sin(wphase) * 5f, 0f, 0f);
            // head: down to graze, up when alert, pumping at a gallop, idly looking about
            float pitch = state == State.Graze ? 55f + Mathf.Sin(Time.time * 2f) * 5f
                        : state == State.Alert || state == State.Stalk ? -12f
                        : state == State.Charge ? 18f
                        : lying ? 20f : Mathf.Sin(wphase * 2f) * (3f + gallop * 7f);
            float yaw = state == State.Idle || state == State.Stay ? Mathf.PerlinNoise(Time.time * 0.25f, herd + 0.5f) * 60f - 30f : 0f;
            headPitch = Mathf.Lerp(headPitch, pitch, 1f - Mathf.Exp(-5f * dt));
            headYaw = Mathf.Lerp(headYaw, yaw, 1f - Mathf.Exp(-3f * dt));
            head.localRotation = Quaternion.Euler(headPitch, headYaw, 0f);
            if (tail)
            {
                bool wag = (owned || town >= 0) && Def.guard && state == State.Follow;
                tail.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.7f) * 6f, Mathf.Sin(Time.time * (wag ? 14f : 2.3f)) * (wag ? 30f : 14f), 0f);
            }
        }

        static readonly float[] WalkSeq = { 0f, 0.5f, 0.75f, 0.25f }, Trot = { 0f, 0.5f, 0.5f, 0f }, Gallop = { 0f, 0.12f, 0.55f, 0.67f };

        /// <summary>Lizards and arthropods: legs sweep fore and aft in two alternating groups (diagonal pairs / tripods),
        /// lifting as they swing forward; the body wiggles, a scorpion's tail sways and strikes, the head bobs.</summary>
        void AnimateSprawl(float dt)
        {
            float s = speed;
            float legLen = Mathf.Max(0.03f, Mesh.legLen);
            phase += s / (legLen * 5f) * dt;
            float wphase = phase * Mathf.PI * 2f;
            float move = Mathf.Clamp01(s / Mathf.Max(0.2f, Def.walk));
            bool dead = state == State.Dead;
            for (int i = 0; i < legs.Length; i++)
            {
                bool left = Mesh.legRoots[i].x < 0f;
                int pair = Def.plan == BodyPlan.Arthropod ? i / 2 : (i < 2 ? 0 : 1);
                float grp = ((pair + (left ? 1 : 0)) & 1) == 0 ? 0f : Mathf.PI;
                float sweep = Mathf.Sin(wphase + grp) * 22f * move;
                float lift = Mathf.Max(0f, Mathf.Cos(wphase + grp)) * 22f * move + (dead ? 50f : 0f);
                float fan = Mesh.legFan != null && i < Mesh.legFan.Length ? Mesh.legFan[i] : 0f;
                float yaw = (left ? 1f : -1f) * (fan + sweep);
                legs[i].localRotation = Quaternion.Slerp(legs[i].localRotation, Quaternion.Euler(0f, yaw, left ? -lift : lift), 1f - Mathf.Exp(-25f * dt));
            }
            rig.localRotation = Quaternion.Euler(0f, Mathf.Sin(wphase) * 6f * move, 0f);
            if (head) head.localRotation = Quaternion.Euler(state == State.Attack ? 15f : Mathf.Sin(Time.time * 3f + herd) * 4f, Def.plan == BodyPlan.Arthropod ? 0f : Mathf.Sin(wphase) * -8f * move, 0f);
            if (tail)
            {
                bool strike = state == State.Attack && Time.time < attackCd - 0.4f;
                if (Def.Has("stinger")) tail.localRotation = Quaternion.Euler(strike ? 35f : Mathf.Sin(Time.time * 2.2f) * 6f, Mathf.Sin(Time.time * 1.3f) * 8f, 0f);
                else tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(wphase + 1f) * 18f * move + Mathf.Sin(Time.time * 1.5f) * 4f, 0f);
            }
        }

        void AnimateSnake(float dt)
        {
            bool coiled = state == State.Coiled || state == State.Idle || state == State.Sleep;
            phase += (0.8f + speed * 6f) * dt;
            for (int i = 0; i < segs.Length; i++)
            {
                Vector3 want;
                if (coiled)
                {
                    float a = i * 0.75f, r = 0.07f + i * 0.012f;
                    want = new Vector3(Mathf.Cos(a) * r, i * 0.004f, Mathf.Sin(a) * r - 0.1f);
                }
                else want = new Vector3(Mathf.Sin(phase - i * 0.7f) * 0.05f * Mathf.Clamp01(i / 3f), 0f, -i * Mesh.segLen);
                segs[i].localPosition = Vector3.Lerp(segs[i].localPosition, want, 1f - Mathf.Exp(-8f * dt));
                var prev = i == 0 ? snakeHead.localPosition : segs[i - 1].localPosition;
                var dir = prev - segs[i].localPosition;
                if (dir.sqrMagnitude > 1e-5f) segs[i].localRotation = Quaternion.LookRotation(dir);
            }
            snakeHead.localPosition = coiled ? new Vector3(0f, 0.12f, 0.02f) : Vector3.forward * 0.02f;
            snakeHead.localRotation = Quaternion.Euler(coiled ? -20f : 0f, 0f, 0f);
            if (rattle) rattle.localPosition = Vector3.back * Mesh.segLen * 0.5f + (Time.time < rattleUntil ? Vector3.right * Mathf.Sin(Time.time * 90f) * 0.01f : Vector3.zero);
        }

        // ------------------------------------------------------------------ voice

        void Say(int kind)
        {
            if (Time.time < callT || Def.plan == BodyPlan.Arthropod && Def.mass < 5f || Def.id == "lizard" || Def.id == "beetle") return;   // small critters are silent
            callT = Time.time + 2f + Random.value * 3f;
            var g = WastelandGame.Instance;
            if (!g || !g.Player || (transform.position - g.Player.transform.position).sqrMagnitude > 45f * 45f) return;
            if (call == null) call = new AnimalCall();
            if (!voice) voice = SynthVoice.Create(transform, "Voice", Mesh.neck, call, 45f);
            voice.gain = GameSettings.Current.sfxVolume * (Def.mass > 200f ? 1f : 0.8f);
            call.Play(AnimalCall.For(Def, kind, Size));
            voice.SetActive(true);
        }

        // ------------------------------------------------------------------ hits, death

        void RunOver(WastelandGame g)
        {
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.Body || v.Body.isKinematic) continue;
                if ((v.transform.position - transform.position).sqrMagnitude > 36f) continue;
                float sp = v.Body.linearVelocity.magnitude;
                if (sp < 3f) continue;
                var c = transform.position + Vector3.up * Mathf.Max(0.15f, Def.Height * 0.5f * Size);
                var cp = v.Body.ClosestPointOnBounds(c);
                float r = col.radius * Size + 0.35f;
                if ((cp - c).sqrMagnitude > r * r) continue;
                var push = Flat(v.Body.linearVelocity).normalized;
                ApplyHit(c, push, sp * sp * 0.03f, 0.3f, v.gameObject);
                transform.position += push * (Alive ? 1.5f * Mathf.Clamp(v.Body.mass / Mathf.Max(20f, Def.mass), 0.3f, 2f) : 1.2f);
                return;
            }
        }

        public void ApplyHit(Vector3 point, Vector3 direction, float power, float radius, GameObject source)
        {
            if (!Alive) return;
            if (proxy) { BloodStains.Splash(transform.position, Mathf.Clamp01(power * 0.5f)); MadMax.Net.NetSession.Instance?.SendActorHit(netId, point, direction, power, radius); return; }
            var g = WastelandGame.Instance;
            float dmg = power * 28f;
            health -= dmg;
            hurtUntil = Time.time + 8f;
            hurter = source ? source.transform : null;
            Wounded(dmg, source);                                                                     // bleeding, a limp
            BloodStains.Splash(transform.position, Mathf.Clamp01(dmg / 40f));
            MadMax.Audio.Sfx.Play("punch", point, 0.6f, Random.Range(0.9f, 1.2f));
            bool byPlayer = g && g.Player && source && (source.transform.IsChildOf(g.Player.transform) || (g.Current && source.transform.IsChildOf(g.Current.transform)));
            if (Def.tameable && !owned) trust = 0f;
            if (health <= 0f) { Die(g, byPlayer, direction); return; }
            Say(1);
            if (Def.flies && (state == State.Perch || state == State.Land)) { state = State.Circle; landAt = Time.time + 40f; }
            if (byPlayer && Def.nature == Nature.Predator && !owned) { state = State.Attack; target = g.Player; }
        }

        void Die(WastelandGame g, bool byPlayer, Vector3 dir)
        {
            state = State.Dead;
            if (Mounted == this && g && g.Player) { g.Player.StandUp(); Mounted = null; }
            speed = 0f; air = 0f;
            var p = transform.position;
            if (g) p.y = Ground(g, p);
            transform.position = p;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (Def.plan == BodyPlan.Arthropod)
            {
                rig.localRotation = Quaternion.Euler(0f, 0f, 180f);                                   // belly up, legs curled
                rig.localPosition = new Vector3(0f, (Def.leg + Def.depth) * VoxelMesher.DefaultSize * Def.scale, 0f);
                AnimateSprawl(1f);
            }
            else if (Def.plan != BodyPlan.Snake)
            {
                // over on its side, legs out
                float s = VoxelMesher.DefaultSize * Def.scale;
                float side = Vector3.Dot(dir, transform.right) >= 0f ? -1f : 1f;
                float cy = (Def.leg + Def.depth * 0.5f) * s;
                rig.localRotation = Quaternion.Euler(0f, 0f, 90f * side);
                rig.localPosition = new Vector3(side * cy, Def.width * 0.5f * s, 0f);
                foreach (var l in legs) l.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), 0f, 0f);
                if (head) head.localRotation = Quaternion.Euler(25f, 0f, 0f);
                if (wings != null) foreach (var w in wings) w.localRotation = Quaternion.Euler(0f, 0f, 20f);
            }
            var carcass = gameObject.AddComponent<Carcass>();
            carcass.Init(Def, Size);
            if (g && byPlayer)
            {
                if (!string.IsNullOrEmpty(Def.pest)) MadMax.Npc.Contracts.ReportPest(Def.pest);
                g.Stats.Practice(Skill.Survival, 2f);
                if (town >= 0) { MadMax.Npc.Factions.Shift(MadMax.Npc.Faction.Settlers, -3); g.Toast("THE VILLAGERS WON'T FORGET THAT " + Def.name); }
            }
            if (g && owned) g.Toast("YOUR " + Label + " IS DEAD");
            AnimalDirector.Instance?.Died(this);
            if (voice) voice.SetActive(false);
            enabled = false;
        }

        // ------------------------------------------------------------------ interaction

        string Treat(WastelandGame g)
        {
            foreach (var f in Def.likes) if (g.Inventory.GetItem(f) > 0) return f;
            return null;
        }

        string OrderLabel => order == 0 ? "FOLLOW ME" : order == 1 ? "STAY HERE" : Def.guard ? "GUARD HERE" : "ROAM HERE";

        public string Prompt(WastelandGame g)
        {
            if (!Alive || g.Current || state == State.Ridden || Def.flies) return null;
            var care = CarePrompt(g);                                                                 // treat, shear, fit saddlebags
            if (care != null) return care;
            if (!owned)
            {
                if (!Def.tameable || town >= 0 || state == State.Attack || state == State.Stalk) return null;
                var food = Treat(g);
                return food != null ? "[E] OFFER " + ItemCatalog.Name(food) + " (TRUST " + Mathf.RoundToInt(trust * 100f) + "%)" : Def.name + " - IT MIGHT TAKE " + ItemCatalog.Name(Def.likes[0]);
            }
            if (Def.rideable && Adult)
            {
                if (saddled) return "[E] RIDE THE " + Label + "  [T] SADDLEBAGS";
                return g.Inventory.GetItem("use_saddle") > 0 ? "[E] SADDLE THE " + Label + "  [T] " + OrderLabel : Label + " (NEEDS A SADDLE)  [T] " + OrderLabel;
            }
            if (stock > 0 && Def.product != null) return "[E] " + (Def.product == "food_egg" ? "COLLECT EGGS" : "MILK THE " + Label) + " (" + stock + ")  [T] " + OrderLabel;
            var treat = Treat(g);
            string mood = fedDay == DayNight.Day ? " (FED)" : hungryDays > 0 ? " (HUNGRY)" : "";
            if (treat != null && fedDay != DayNight.Day) return "[E] FEED " + ItemCatalog.Name(treat) + " TO THE " + Label + mood + "  [T] " + OrderLabel;
            return Label + mood + "  [T] " + OrderLabel;
        }

        public void Use(WastelandGame g, bool secondary)
        {
            if (!Alive || g.Current) return;
            if (!secondary && CareUse(g)) return;
            if (secondary)
            {
                if (!owned) return;
                if (Def.rideable && saddled && Adult) { if (bags) g.Menus.OpenContainer(bags); return; }
                order = (order + 1) % 3;
                if (order == 0) home = transform.position;
                g.Toast(Label + (order == 1 ? " FOLLOWS YOU" : order == 2 ? " STAYS" : Def.guard ? " GUARDS THIS PLACE" : " ROAMS HERE"));
                Say(0);
                return;
            }
            if (!owned)
            {
                if (!Def.tameable || town >= 0) return;
                var food = Treat(g);
                if (food == null || !g.Inventory.TakeItem(food)) return;
                float gain = 0.25f * (1f + g.Stats.Level(Skill.Farming) * 0.06f + g.Stats.Level(Skill.Survival) * 0.04f);
                trust = Mathf.Min(1f, trust + gain);
                g.Stats.Practice(Skill.Farming, 2f);
                Say(0);
                if (trust >= 1f) Tame(g);
                else g.Toast("THE " + Def.name + " TAKES IT (TRUST " + Mathf.RoundToInt(trust * 100f) + "%)");
                return;
            }
            if (Def.rideable && Adult)
            {
                if (saddled) { Mount(g); return; }
                if (g.Inventory.TakeItem("use_saddle")) { SetSaddled(true); MadMax.Audio.Sfx.Play("chest", transform.position, 0.5f); g.Toast("SADDLED - [E] TO RIDE"); }
                return;
            }
            if (stock > 0 && Def.product != null)
            {
                g.Inventory.AddItem(Def.product, stock);
                g.Toast("GOT " + stock + " " + ItemCatalog.Name(Def.product));
                g.Stats.Practice(Skill.Farming, stock);
                stock = 0;
                Say(0);
                return;
            }
            var treat = Treat(g);
            if (treat != null && fedDay != DayNight.Day && g.Inventory.TakeItem(treat))
            {
                fedDay = DayNight.Day; hungryDays = 0;
                g.Toast("THE " + Label + " EATS");
                g.Stats.Practice(Skill.Farming, 1f);
                Say(0);
            }
        }

        void Tame(WastelandGame g)
        {
            owned = true; herd = 0; order = 1; home = transform.position; born = DayNight.Day - 100;
            fedDay = DayNight.Day;
            AnimalDirector.Instance?.Adopt(this);
            g.Stats.Practice(Skill.Farming, 10f);
            g.Toast(Def.rideable ? "THE HORSE TRUSTS YOU - SADDLE IT TO RIDE ([T] FOLLOW / STAY)" : "THE " + Def.name + " IS YOURS - IT GUARDS YOU ([T] ORDERS)");
            MadMax.Audio.Sfx.Play2D("ding", 0.6f);
        }
    }
}
