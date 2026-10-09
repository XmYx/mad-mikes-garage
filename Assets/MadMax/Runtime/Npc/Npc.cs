using System.Collections.Generic;
using MadMax.Building;
using MadMax.Game;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A person on foot: HumanRig body (shared meshes), HumanAnimator gait, CharacterController movement.
    /// Stands at a post (shopkeepers, stallkeepers, town bosses), wanders around home (residents, wanderers, pack
    /// traders), keeps a daily routine (roadmap 20: residents gather at the campfire in the evening and sleep indoors
    /// at night, wanderers sit up by their own fire, shops close after dark), flees when scared, or fights (raiders,
    /// anyone pushed too far): melee within reach or shotgun blasts at range, at the player, the player's vehicle or
    /// another person. Gunmen take cover, knife-men fan out, the badly hurt break off, the beaten surrender.
    /// Companions (<see cref="Companions"/>) follow the player, ride along or drive a second vehicle, wait, guard a
    /// base and fight whatever threatens the player. Talk with [E], trade with [T]. Takes hits through
    /// <see cref="IDamageable"/>, gets run over by fast vehicles, dies into a searchable body; killing peaceful folk
    /// (or someone who surrendered) costs reputation.</summary>
    public partial class Npc : MonoBehaviour, IInteractable, IDamageable
    {
        public static readonly List<Npc> All = new List<Npc>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public enum Mode { Stand, Wander, Flee, Fight, Dead, Follow, Sleep, Gather, Surrender }
        public NpcProfile Profile { get; private set; }
        public NpcSave State { get; private set; }
        public Mode mode = Mode.Wander;
        public Vector3 home;
        public float homeRadius = 8f, homeYaw;
        /// <summary>Raiders of a convoy that is attacking.</summary>
        public bool aggro;
        public Convoy convoy;
        /// <summary>The key of the car this convoy driver climbed out of (looted off the body; <see cref="Convoy"/>).</summary>
        [System.NonSerialized] public string carriedKey;
        [System.NonSerialized] public VehicleIgnition keyFor;
        /// <summary>Raiding a claimed base (<see cref="BaseRaid"/>): batter the nearest built piece around
        /// <see cref="raidAt"/> unless the player is close enough to fight.</summary>
        public bool raiding;
        /// <summary>Raiding: goes for parked vehicles (siphons and smashes) before the buildings.</summary>
        [System.NonSerialized] public bool carBreaker;
        MadMax.Vehicles.VehicleDriver siegeCar;
        public Vector3 raidAt;
        Placeable siegeTarget;
        float siegeRepick;
        /// <summary>Walks with the player (roadmap 20): <see cref="order"/> 0 follow, 1 wait here, 2 guard (home = post).</summary>
        public bool companion;
        public int order;
        /// <summary>Let go (dismissed, spared): walks off and is folded away once out of sight.</summary>
        public bool leaving;
        /// <summary>A companion's pack (25 kg).</summary>
        public Container pack;
        public bool Hostile => mode != Mode.Dead && !Surrendered && (State.Has(NpcSave.Hostile) || (Profile.Raider && aggro));
        public bool Alive => mode != Mode.Dead;
        public bool Surrendered => State.Has(NpcSave.Surrendered);
        /// <summary>Here to be talked to: not asleep indoors, not riding or driving.</summary>
        public bool Available => Alive && !asleep && !Riding && !Driving && !Down;
        public bool Riding => rideSeat;
        public bool Driving => drivenCar;
        public VehicleDriver DrivenCar => drivenCar;
        /// <summary>Shops and stalls keep hours (7:00–20:00).</summary>
        public bool Closed => (Profile.role == NpcRole.Shopkeeper || Profile.role == NpcRole.Stallkeeper) && (DayNight.Hours < 7f || DayNight.Hours >= 20f);
        public float Health => health;

        HumanRig rig;
        HumanAnimator anim;
        CharacterController cc;
        HandTool tool;
        float health, maxHealth, vy, swing = -1f, attackCd, repath, fleeUntil, faceUntil, stuck, lastHurt, bleedUntil;
        bool lastByPlayer;

        /// <summary>An open wound (arrows, bolts, blades): loses health for a while, leaving a blood trail.</summary>
        public void Bleed(float seconds) { if (mode != Mode.Dead) bleedUntil = Mathf.Max(bleedUntil, Time.time + seconds); }
        Vector3 goal, lastPos, detour, lastBlow, lastVelocity, staggerDir;
        float reactAt = -9f, staggerUntil;
        string reactClip;
        float detourUntil;
        bool hasGoal;
        Vector3 faceTarget;
        // combat AI and routine
        Npc foe, hurtBy;
        float thinkT, hurtByUntil, coverT, flank;
        Vector3 cover;
        bool hasCover, retreated, asleep, seated;
        Campfire fire;
        int fireSpot = -1;
        PassengerSeat rideSeat;
        VehicleDriver drivenCar;
        static readonly ToolPose HandsUp = new ToolPose { armRX = -165f, armRZ = 14f, foreR = -55f, armLX = -165f, armLZ = -14f, foreL = -55f };

        public static Npc Spawn(NpcProfile p, Vector3 pos, float yaw, Transform parent, Material mat)
        {
            var go = new GameObject("NPC " + p.Name);
            if (parent) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var n = go.AddComponent<Npc>();
            n.Profile = p; n.State = NpcRegistry.Get(p);
            n.home = pos; n.homeYaw = yaw;
            n.cc = go.AddComponent<CharacterController>();
            n.cc.height = 1.75f * p.look.height; n.cc.radius = 0.28f; n.cc.center = new Vector3(0f, n.cc.height * 0.5f, 0f);
            n.cc.stepOffset = 0.35f; n.cc.slopeLimit = 50f;
            n.rig = go.AddComponent<HumanRig>();
            n.rig.material = mat; n.rig.shareMeshes = true; n.rig.noStrands = true;
            n.rig.appearance = p.look.Clone();
            n.rig.appearance.lost = n.State.lost;                                               // limbs lost in an earlier fight (Npc.Limbs)
            n.rig.outfit.AddRange(p.outfit);
            n.rig.Rebuild();
            n.anim = new HumanAnimator(n.rig);
            n.anim.Footstep += left => n.OnFootstep();
            n.maxHealth = n.health = p.role == NpcRole.RaiderBoss ? 160f : p.Raider ? 90f : p.role == NpcRole.Leader ? 110f : 70f;
            n.flank = (Mathf.Abs(p.seed >> 4) % 3 - 1) * 55f;
            if (n.State.Has(NpcSave.Surrendered)) n.leaving = true;                          // spared before: keeps out of the way
            if (!n.State.Has(NpcSave.Surrendered)) n.SetTool(p.tool, mat);
            n.lastPos = pos;
            return n;
        }

        void OnFootstep()
        {
            var g = WastelandGame.Instance;
            if (!g || !g.Player) return;
            var p = transform.position;
            if ((p - g.Player.transform.position).sqrMagnitude > 20f * 20f) return;                  // only the close ones are heard
            var t = DeformableTerrain.Instance;
            MadMax.Audio.Sfx.Play(PlayerCharacter.StepSound(p, t ? t.SurfaceAt(p.x, p.z) : default), p, 0.2f, Random.Range(0.85f, 1.05f), 14f);
        }

        void SetTool(string id, Material mat)
        {
            if (tool) Destroy(tool.gameObject);
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.Npc.SetTool");
            tool = id != null ? ToolLibrary.Create(id, mat) : null;
            UnityEngine.Profiling.Profiler.EndSample();
            if (!tool) return;
            tool.transform.SetParent(rig.RightHand, false);
            tool.transform.localPosition = new Vector3(0f, -0.055f, 0.01f);
            tool.transform.localRotation = Quaternion.identity;
            tool.enabled = tool is LightTool;                              // torches keep their flame; nothing strikes by itself
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void OnDestroy()
        {
            if (rideSeat) rideSeat.Occupant = null;
            if (fire && fireSpot >= 0) fire.Free(fireSpot);
        }

        bool Ranged => tool is RangedTool;

        // ------------------------------------------------------------------ brain

        // ------------------------------------------------------------------ network proxy (a client's copy of a host NPC)
        /// <summary>Host-assigned network id (0 = not replicated yet).</summary>
        [System.NonSerialized] public ushort netId;
        /// <summary>A client's copy: no brain, posed from the host's snapshots; hits are sent to the host.</summary>
        [System.NonSerialized] public bool proxy;
        Vector3 proxyPos; float proxyYaw, proxySpeed; byte proxyFlags;

        public const byte FlagDead = 1, FlagSitting = 2, FlagSurrender = 4, FlagSwing = 8, FlagAsleep = 16;

        /// <summary>Replication flags for the host's snapshot.</summary>
        public byte NetFlags => (byte)((mode == Mode.Dead ? FlagDead : 0) | (seated ? FlagSitting : 0) | (mode == Mode.Surrender ? FlagSurrender : 0) | (swing >= 0f ? FlagSwing : 0) | (asleep ? FlagAsleep : 0));
        public float NetSpeed => Flat(lastVelocity).magnitude;

        public void ProxyState(Vector3 pos, float yaw, float speed, byte flags)
        {
            if (!proxy) return;
            if (proxyFlags == 0 && proxyPos == Vector3.zero) transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            proxyPos = pos; proxyYaw = yaw; proxySpeed = speed; proxyFlags = flags;
            if ((flags & FlagDead) != 0 && mode != Mode.Dead) ProxyDie();
        }

        void ProxyTick(float dt)
        {
            if (proxyPos != Vector3.zero)
            {
                transform.position = Vector3.Lerp(transform.position, proxyPos, 1f - Mathf.Exp(-12f * dt));
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, proxyYaw, 0f), 1f - Mathf.Exp(-10f * dt));
            }
            bool sit = (proxyFlags & (FlagSitting | FlagAsleep)) != 0;
            if ((proxyFlags & FlagSwing) != 0 && swing < 0f) swing = 0f;
            if (swing >= 0f) { swing += dt / (tool ? tool.swingDuration : 0.5f); if (swing >= 1f) swing = -1f; }
            anim.Tick(dt, new HumanAnimator.State
            {
                speed = proxySpeed, grounded = true, sitting = sit, lounging = sit,
                tool = (proxyFlags & FlagSurrender) != 0 ? HandsUp : tool ? (swing >= 0f ? tool.Pose(swing) : tool.IdlePose) : (ToolPose?)null,
                twoHanded = tool && tool.TwoHanded
            });
        }

        /// <summary>The host says this one died: go limp here too; the body is searchable (same deterministic loot).</summary>
        void ProxyDie()
        {
            mode = Mode.Dead;
            cc.enabled = false;
            if (tool) { tool.transform.SetParent(null, true); DropPhysics(tool.gameObject); Destroy(tool.gameObject, 60f); }
            Ragdoll.For(rig).Go(-transform.forward * 60f, transform.position + Vector3.up * 1.1f, Vector3.zero);
            var loot = rig.bones[BodyPart.Pelvis].gameObject.AddComponent<Lootable>();
            loot.key = "N" + Profile.id; loot.table = Profile.Raider ? "raider" : Profile.Vendor ? "shop" : "house";
            loot.title = Profile.Name + "'S BODY";
        }

        void Update()
        {
            if (mode == Mode.Dead) return;
            float dt = Time.deltaTime;
            if (proxy) { ProxyTick(dt); return; }
            var g = WastelandGame.Instance;
            if (!g || g.Player == null) return;
            if (Down)
            {
                if (Time.time < bleedUntil && (health -= 3f * dt) <= 0f) { Die(lastByPlayer); return; }
                DownTick();
                return;
            }
            if (Time.time < bleedUntil)
            {
                health -= 3f * dt;
                if (Random.value < dt * 3f) BloodStains.Splash(transform.position, 0.15f);
                if (health <= 0f) { Die(lastByPlayer); return; }
            }
            if (berthed)
            {
                // on a clinic bed (depth stage G): propped up, still, until let go
                if (!berth) Unberth();
                else { transform.SetPositionAndRotation(berth.position - berth.up * (0.94f * Profile.look.height), berth.rotation); anim.Tick(dt, new HumanAnimator.State { sitting = true, lounging = true, grounded = true }); return; }
            }
            if (Riding) { RideTick(g, dt); return; }
            if (Driving) { DriveTick(g); return; }
            var terrain = DeformableTerrain.Instance;
            // wanderers light a torch after dark
            if (Profile.role == NpcRole.Wanderer && Profile.tool == null && !companion && !Surrendered && (DayNight.Darkness > 0.45f) != (tool is LightTool))
                SetTool(DayNight.Darkness > 0.45f ? "tool_torch" : null, rig.material);

            Vector3 me = transform.position;
            var playerPos = g.Current ? g.Current.transform.position : g.Player.transform.position;
            float dPlayer = Vector3.Distance(me, playerPos);
            if (leaving && dPlayer > 170f) { Destroy(gameObject); return; }
            UnityEngine.Profiling.Profiler.BeginSample("MadMax.Npc.RunOver");
            RunOver(g);
            UnityEngine.Profiling.Profiler.EndSample();

            Vector3 move = Vector3.zero;
            float speed = 0f;
            // who to fight: companions pick the nearest threat, anyone hits back at a person who hurt them
            if ((thinkT -= dt) <= 0f) { thinkT = 0.4f; UnityEngine.Profiling.Profiler.BeginSample("MadMax.Npc.PickFoe"); foe = PickFoe(g); UnityEngine.Profiling.Profiler.EndSample(); }
            if (Surrendered) mode = Mode.Surrender;
            else if (foe) mode = Mode.Fight;
            else if (Hostile && dPlayer < (mode != Mode.Fight && !g.Current && g.Player.Crouching ? 28f : 60f) && !g.Vitals.Dead) mode = Mode.Fight;   // crouching: harder to spot
            else if (mode != Mode.Flee || Time.time > fleeUntil) mode = Routine();
            if (mode != Mode.Gather) LeaveFire();
            if (mode != Mode.Sleep && asleep) SetAsleep(false);

            bool sieging = raiding && !foe && (dPlayer > 22f || g.Vitals.Dead);
            if (sieging) Siege(dt, ref move, ref speed);
            else if (fireTarget && !foe && FireTick(me, dt, ref move, ref speed)) { }
            else if (tending && !foe && TendTick(me, dt, ref move, ref speed)) { }
            else if (companion && order == 2 && ManPost(me, dt, ref move, ref speed)) { }
            else switch (mode)
            {
                case Mode.Stand:
                    if ((me - home).sqrMagnitude > 0.25f) { move = Toward(home); speed = (me - home).sqrMagnitude > 16f ? 2.4f : 1.2f; }
                    else if (Time.time > faceUntil) FaceYaw(homeYaw, dt);
                    break;
                case Mode.Wander:
                    if (!hasGoal || Time.time > repath || Flat(goal - me).magnitude < 0.6f)
                    {
                        if (hasGoal && Flat(goal - me).magnitude < 0.6f && Random.value < 0.6f) { hasGoal = false; repath = Time.time + Random.Range(3f, 9f); break; }  // linger
                        if (Time.time > repath) PickGoal(terrain);
                    }
                    if (hasGoal) { move = Toward(goal); speed = Profile.role == NpcRole.Packer ? 1.05f : 1.25f; }
                    break;
                case Mode.Flee:
                {
                    var from = foe ? foe.transform.position : playerPos;
                    move = Flat(me - from).normalized; speed = 3.6f;
                    break;
                }
                case Mode.Fight:
                    if (foe) Fight(g, foe.transform.position, Vector3.Distance(me, foe.transform.position), false, dt, ref move, ref speed);
                    else Fight(g, playerPos, dPlayer, g.Current, dt, ref move, ref speed);
                    break;
                case Mode.Follow:
                    Follow(g, playerPos, dPlayer, ref move, ref speed);
                    if (Riding) return;
                    break;
                case Mode.Sleep:
                    if (!asleep) { if (Flat(home - me).sqrMagnitude > 1f) { move = Toward(home); speed = 1.4f; } else SetAsleep(true); }
                    break;
                case Mode.Gather:
                    Gather(me, ref move, ref speed);
                    break;
                case Mode.Surrender:
                    faceTarget = playerPos; faceUntil = Time.time + 0.5f;
                    break;
            }
            if (asleep) return;                                                               // indoors till morning
            if (Time.time < chatUntil && (mode == Mode.Stand || mode == Mode.Wander || mode == Mode.Gather) && !seated) { move = Vector3.zero; speed = 0f; faceTarget = chatAt; faceUntil = chatUntil; }   // chatting with a neighbour
            if (mode != Mode.Fight && mode != Mode.Surrender && dPlayer < 3.5f && !g.Current) { faceTarget = playerPos; faceUntil = Time.time + 2f; }
            if (DefenceHazard.All.Count > 0) speed *= DefenceHazard.SlowAt(me);                // snagged in barbed wire
            if (rig.appearance.lost != 0) speed *= LimbPace;                                   // hopping on one leg, crawling on none

            // knocked back by a heavy blow: carried along it, slowing, still facing the one who struck
            bool staggering = Time.time < staggerUntil;
            if (staggering && !seated) { move = staggerDir; speed = 2.6f * (staggerUntil - Time.time) / StaggerTime; }

            float moved = 0f;
            if (seated) SitAtFire();
            else
            {
                // movement: CharacterController against buildings and props, gravity, stuck → sidestep / new goal
                if (Time.time < detourUntil && move.sqrMagnitude > 0.001f) move = detour;
                if (move.sqrMagnitude > 0.001f && !Blocked(terrain, me, move)) { if (!staggering) Face(move, dt); }
                else if (move.sqrMagnitude > 0.001f) { hasGoal = false; move = Vector3.zero; speed = 0f; }
                if (Time.time < faceUntil && speed < 0.1f) Face(Flat(faceTarget - me), dt);
                vy = cc.isGrounded ? -1f : vy + Physics.gravity.y * dt;
                if (cc.enabled) cc.Move((move.normalized * speed + Vector3.up * vy) * dt);
                if (terrain)
                {
                    var p = transform.position;
                    float h = terrain.HeightNoLoad(p.x, p.z);
                    if (p.y < h - 0.3f) { p.y = h + 0.05f; transform.position = p; }
                }
                lastVelocity = (transform.position - lastPos) / Mathf.Max(dt, 1e-4f);
                moved = Flat(transform.position - lastPos).magnitude / Mathf.Max(dt, 1e-4f);
                if (speed > 0.5f && moved < 0.2f)
                {
                    if ((stuck += dt) > 1f)
                    {
                        stuck = 0f; hasGoal = false; repath = 0f; routeCheck = 0f;
                        var side = Vector3.Cross(Vector3.up, move).normalized * (Random.value < 0.5f ? 1f : -1f);
                        detour = side + move.normalized * 0.3f; detourUntil = Time.time + 1.4f;
                    }
                }
                else stuck = 0f;
            }
            lastPos = transform.position;

            if (swing >= 0f)
            {
                swing += dt / (tool ? tool.swingDuration : 0.5f);
                if (swing >= 1f) swing = -1f;
            }
            // in a conversation they gesture; they look at the player who talks to them or stands close by
            var gm = WastelandGame.Instance;
            bool talking = gm && gm.Menus && gm.Menus.TalkingTo == this;
            bool near = gm && gm.Player && (mode == Mode.Stand || mode == Mode.Wander || mode == Mode.Follow)
                        && (gm.Player.transform.position - transform.position).sqrMagnitude < 3.5f * 3.5f;
            anim.LookAt = talking || near ? gm.Player.Eye.position : (Vector3?)null;
            float rt = -1f;
            string react = swing >= 0f ? null : Reaction(out rt);
            // carried at rest; a gun is up at the shoulder while fighting at range
            string hold = tool && swing < 0f && !seated && mode != Mode.Surrender && !(mode == Mode.Fight && Ranged) ? ToolHolds.Clip(tool) : null;
            anim.Tick(dt, new HumanAnimator.State
            {
                speed = moved, grounded = seated || cc.isGrounded, verticalSpeed = seated ? 0f : vy, sitting = seated, lounging = seated,
                tool = mode == Mode.Surrender ? HandsUp : tool ? (swing >= 0f ? tool.Pose(swing) : hold == null ? tool.IdlePose : null) : (ToolPose?)null,
                twoHanded = tool && tool.TwoHanded, talking = talking, hold = hold,
                grip2 = !tool || mode == Mode.Surrender ? null : swing >= 0f ? tool.SecondGrip : hold != null && tool.HoldTwoHands ? tool.transform : null,
                action = swing >= 0f ? PlayerCharacter.SwingClip(tool) : react, actionT = swing >= 0f ? swing : react != null ? rt : -1f,
                actionHit = swing >= 0f && tool ? tool.strikeAt : 0f
            });
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        const float StaggerTime = 0.45f;

        /// <summary>A melee blow: the swing plays, the hit lands at the tool's strike moment unless they are struck first.</summary>
        public void BeginSwing()
        {
            swing = 0f; attackCd = 1.3f;
            Invoke(nameof(MeleeHit), tool ? tool.swingDuration * tool.strikeAt : 0.3f);
        }

        public bool Swinging => swing >= 0f;
        /// <summary>The hit reaction playing ("flinch", "stagger") or null; knocked back by a heavy blow.</summary>
        public string Reacting => Time.time - reactAt < 1f ? reactClip : null;
        public bool Staggering => Time.time < staggerUntil;

        /// <summary>The hit-reaction clip playing (flinch / stagger) and its progress; null when none.</summary>
        string Reaction(out float t)
        {
            t = -1f;
            if (reactClip == null) return null;
            var c = HumanClips.Get(reactClip);
            t = c != null ? (Time.time - reactAt) / c.length : 1f;
            if (t < 1f) return reactClip;
            reactClip = null; t = -1f;
            return null;
        }

        /// <summary>Struck: a flinch; a solid blow during the wind-up spoils the swing; a heavy one (sledge, a big hit)
        /// staggers them back a step along the blow and costs them a beat before they can strike again.</summary>
        void React(Vector3 direction, float power, float dmg)
        {
            bool heavy = power >= 0.9f || dmg >= 22f;
            bool windingUp = swing >= 0f && swing < (tool ? tool.strikeAt : 0.6f);
            if (heavy || windingUp && dmg >= 8f) { swing = -1f; CancelInvoke(nameof(MeleeHit)); }
            if (heavy)
            {
                reactClip = "stagger";
                var d = Flat(direction);
                staggerDir = d.sqrMagnitude > 0.01f ? d.normalized : -transform.forward;
                staggerUntil = Time.time + StaggerTime;
                attackCd = Mathf.Max(attackCd, 1.2f);
            }
            else
            {
                reactClip = "flinch";
                attackCd = Mathf.Max(attackCd, 0.45f);
            }
            reactAt = Time.time;
        }

        // ---- pathfinding (NpcPath): straight at the goal when the way is clear, else along a grid route
        readonly List<Vector3> route = new List<Vector3>();
        Vector3 routeGoal;
        int routeIndex;
        float routeCheck, doorCheck;
        bool routing;

        Vector3 Toward(Vector3 p)
        {
            var me = transform.position;
            var direct = Flat(p - me);
            if (direct.sqrMagnitude < 0.36f) return direct;
            if (Time.time >= routeCheck || Flat(p - routeGoal).sqrMagnitude > 9f)
            {
                routeCheck = Time.time + 0.8f + Random.value * 0.6f;                              // staggered across people
                routeGoal = p; routeIndex = 0;
                UnityEngine.Profiling.Profiler.BeginSample("MadMax.Npc.Path");
                routing = !NpcPath.Clear(me, p, transform);
                if (routing && !NpcPath.Find(me, p, route, transform)) { routing = false; routeCheck = Time.time + 0.25f; }   // out of budget: soon again
                UnityEngine.Profiling.Profiler.EndSample();
            }
            if (!routing) return direct;
            while (routeIndex < route.Count - 1 && Flat(route[routeIndex] - me).sqrMagnitude < 0.45f * 0.45f) routeIndex++;
            if (routeIndex >= route.Count) return direct;
            var step = Flat(route[routeIndex] - me);
            if (Time.time > doorCheck) { doorCheck = Time.time + 0.5f; OpenDoorAhead(me, step); }
            return step;
        }

        /// <summary>A closed, unlocked door right ahead swings open for the walker.</summary>
        void OpenDoorAhead(Vector3 me, Vector3 dir)
        {
            foreach (var d in Placeable.All)
            {
                if (!d || (d.transform.position - me).sqrMagnitude > 2.2f * 2.2f || !d.TryGetComponent<Door>(out var door) || door.open || door.locked) continue;
                if (Vector3.Dot(Flat(d.transform.position - me), dir) > 0f) { door.Toggle(); return; }
            }
        }

        void Face(Vector3 dir, float dt)
        {
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-8f * dt));
        }

        void FaceYaw(float yaw, float dt) => transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), 1f - Mathf.Exp(-4f * dt));

        void PickGoal(DeformableTerrain terrain)
        {
            for (int i = 0; i < 6; i++)
            {
                var c = Random.insideUnitCircle * homeRadius;
                var p = home + new Vector3(c.x, 0f, c.y);
                if (terrain && terrain.WaterDepthNoLoad(p.x, p.z) > 0.3f) continue;
                goal = p; hasGoal = true; repath = Time.time + 20f;
                return;
            }
            repath = Time.time + 2f;
        }

        /// <summary>Deep water or a cliff ahead: don't walk into it.</summary>
        bool Blocked(DeformableTerrain terrain, Vector3 me, Vector3 dir)
        {
            if (!terrain) return false;
            var ahead = me + dir.normalized * 0.8f;
            if (terrain.WaterDepthNoLoad(ahead.x, ahead.z) > 0.6f && mode != Mode.Fight) return true;
            return terrain.HeightNoLoad(ahead.x, ahead.z) - me.y > 1.2f;
        }

        // ------------------------------------------------------------------ routine (roadmap 20 schedules)

        /// <summary>What to do when nothing is happening: companions by their order; townsfolk by the clock —
        /// residents gather at the campfire in the evening and sleep indoors at night, stallkeepers and town bosses
        /// sleep at night, wanderers sit up by their own fire, shopkeepers mind the shop.</summary>
        Mode Routine()
        {
            if (companion) return order == 1 ? Mode.Stand : order == 2 ? Mode.Wander : Mode.Follow;
            bool keeper = Profile.role == NpcRole.Shopkeeper || Profile.role == NpcRole.Stallkeeper || Profile.role == NpcRole.Leader;
            if (leaving || Hostile || Profile.Raider || convoy != null || raiding) return keeper ? Mode.Stand : Mode.Wander;
            float h = DayNight.Hours;
            bool night = h >= 22f || h < 6f, evening = h >= 18.5f && h < 22f;
            switch (Profile.role)
            {
                case NpcRole.Resident:
                    if (night) return Mode.Sleep;
                    return evening && Campfire.Nearest(home, 60f) ? Mode.Gather : Mode.Wander;
                case NpcRole.Stallkeeper: case NpcRole.Leader:
                    return night ? Mode.Sleep : Mode.Stand;
                case NpcRole.Wanderer:
                    return (night || evening) && Campfire.Nearest(home, 30f) ? Mode.Gather : Mode.Wander;
                case NpcRole.Shopkeeper:
                    return Mode.Stand;
                default:
                    return Mode.Wander;
            }
        }

        /// <summary>Indoors for the night: hidden, not in anyone's way.</summary>
        void SetAsleep(bool on)
        {
            if (asleep == on) return;
            asleep = on;
            SetVisible(!on);
            cc.enabled = !on;
        }

        void SetVisible(bool on) { foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on; }

        void Gather(Vector3 me, ref Vector3 move, ref float speed)
        {
            if (!fire || fireSpot < 0)
            {
                fire = Campfire.Nearest(home, 60f);
                fireSpot = fire ? fire.Claim() : -1;
                if (fireSpot < 0) { fire = null; mode = Mode.Wander; return; }
            }
            if (seated) return;
            var spot = fire.Seat(fireSpot);
            if (Flat(spot - me).magnitude > 0.45f) { move = Toward(spot); speed = 1.3f; }
            else { seated = true; cc.enabled = false; }
        }

        void LeaveFire()
        {
            if (fire && fireSpot >= 0) fire.Free(fireSpot);
            fire = null; fireSpot = -1;
            if (!seated) return;
            seated = false;
            var p = transform.position;
            var t = DeformableTerrain.Instance;
            if (t) p.y = t.HeightNoLoad(p.x, p.z) + 0.05f;
            transform.position = p;
            cc.enabled = !asleep;
        }

        /// <summary>On a log by the fire, facing the flames (hips on the log: the sitting pose keeps them at standing height).</summary>
        void SitAtFire()
        {
            if (!fire) { LeaveFire(); return; }
            var spot = fire.Seat(fireSpot);
            var face = Flat(fire.transform.position - spot);
            transform.SetPositionAndRotation(spot + Vector3.up * (Campfire.BenchHeight - 0.94f * Profile.look.height),
                Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : transform.forward));
        }

        // ------------------------------------------------------------------ companions (roadmap 20)

        public void EnsurePack()
        {
            if (pack) return;
            pack = gameObject.AddComponent<Container>();
            pack.title = Profile.Name + "'S PACK"; pack.capacity = 25f;
        }

        void Follow(WastelandGame g, Vector3 playerPos, float dPlayer, ref Vector3 move, ref float speed)
        {
            var car = g.Current;
            if (car)
            {
                // ride along: to the passenger door and in
                var seat = car.GetComponentInChildren<PassengerSeat>();
                if (seat && !seat.Taken && !seat.Occupant)
                {
                    var door = seat.transform.position;
                    if (Flat(door - transform.position).magnitude < 1.8f) { Board(seat); return; }
                    if (dPlayer < 60f) { move = Toward(door); speed = 3.8f; return; }
                }
            }
            if (dPlayer > 80f) { CatchUp(g); return; }
            float want = car ? 8f : 3f;
            if (dPlayer > want) { move = Toward(playerPos); speed = dPlayer > 10f ? 4.2f : dPlayer > 6f ? 3f : 1.6f; }
        }

        /// <summary>Left far behind: turn up next to the player (or in their passenger seat).</summary>
        void CatchUp(WastelandGame g)
        {
            var car = g.Current;
            var seat = car ? car.GetComponentInChildren<PassengerSeat>() : null;
            if (seat && !seat.Taken && !seat.Occupant) { Board(seat); return; }
            var pt = g.Player.transform;
            var p = car ? car.transform.position - car.transform.forward * 9f : pt.position - pt.forward * 4f;
            var t = DeformableTerrain.Instance;
            if (t) p.y = t.HeightNoLoad(p.x, p.z) + 0.1f;
            cc.enabled = false; transform.position = p; cc.enabled = true;
        }

        /// <summary>Sit in a vehicle's passenger seat and ride with it.</summary>
        public void Board(PassengerSeat seat)
        {
            if (!seat || seat.Occupant || Riding) return;
            LeaveFire();
            rideSeat = seat; seat.Occupant = this;
            cc.enabled = false;
            MadMax.Audio.Sfx.Play("car_door", seat.transform.position, 0.5f);
        }

        public void Unboard()
        {
            if (!rideSeat) return;
            var exit = rideSeat.transform.position;                                          // the passenger door
            var t = DeformableTerrain.Instance;
            if (t) exit.y = t.HeightNoLoad(exit.x, exit.z) + 0.05f;
            rideSeat.Occupant = null; rideSeat = null;
            transform.SetPositionAndRotation(exit, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            cc.enabled = true;
        }

        void RideTick(WastelandGame g, float dt)
        {
            var seat = rideSeat;
            if (!seat || !seat.Seat || !seat.Vehicle) { rideSeat = null; cc.enabled = true; return; }
            bool stay = seat.Vehicle.aiDriven ? !companion : companion && order == 0 && g.Current == seat.Vehicle;
            if (!stay && Mathf.Abs(seat.Vehicle.ForwardSpeed) < 2f) { Unboard(); return; }
            var st = seat.Seat.transform;
            transform.SetPositionAndRotation(st.position + Vector3.down * (0.94f * Profile.look.height), st.rotation);
            lastPos = transform.position;
            anim.Tick(dt, new HumanAnimator.State { sitting = true, lounging = true, grounded = true });
        }

        /// <summary>Take the wheel of a vehicle and drive it behind the player.</summary>
        public void TakeWheel(VehicleDriver v)
        {
            if (!v || Driving) return;
            LeaveFire(); Unboard();
            if (!v.TryGetComponent<AiDriver>(out var ai)) ai = v.gameObject.AddComponent<AiDriver>();
            ai.enabled = true;
            v.aiDriven = true; v.bakedDriver = true; v.Occupied = true; v.handbrake = false;
            if (v.Body && v.Body.isKinematic) v.Body.isKinematic = false;
            ai.goal = AiDriver.Goal.Escort;
            drivenCar = v;
            SetVisible(false); cc.enabled = false;
            MadMax.Audio.Sfx.Play("car_door", v.transform.position, 0.6f);
        }

        public void LeaveWheel()
        {
            if (!drivenCar) return;
            var v = drivenCar; drivenCar = null;
            var gw = WastelandGame.Instance;
            if (v.TryGetComponent<AiDriver>(out var ai) && !(gw && gw.Current == v)) ai.Release();   // also when it was already switched off: never leave it "occupied"
            var p = v.transform.position - v.transform.right * 2.2f;
            var t = DeformableTerrain.Instance;
            if (t) p.y = t.HeightNoLoad(p.x, p.z) + 0.05f;
            transform.position = p;
            SetVisible(true); cc.enabled = true;
        }

        void DriveTick(WastelandGame g)
        {
            var v = drivenCar;
            if (!v || !v.TryGetComponent<AiDriver>(out var ai) || !ai.enabled || g.Current == v) { if (v) { drivenCar = v; LeaveWheel(); } else { drivenCar = null; SetVisible(true); cc.enabled = true; } return; }
            ai.target = g.Current ? g.Current.transform : g.Player.transform;
            transform.position = v.transform.position;                                       // stays with the car
        }

        /// <summary>The nearest threat for a companion (a hostile person near them or the player), or whoever just
        /// hurt this person.</summary>
        Npc PickFoe(WastelandGame g)
        {
            if (Surrendered) return null;
            if (hurtBy && hurtBy.Alive && !hurtBy.Surrendered && Time.time < hurtByUntil && !(companion && hurtBy.companion)) return hurtBy;
            if (!companion) return null;
            var me = transform.position; var pp = g.Player.transform.position;
            float reach = order == 2 ? 30f : 26f;
            Npc best = null; float bd = reach * reach;
            foreach (var n in All)
            {
                if (!n || n == this || !n.Alive || n.companion || !n.Hostile || !n.Available) continue;
                float d = Mathf.Min((n.transform.position - me).sqrMagnitude, (n.transform.position - pp).sqrMagnitude);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        // ------------------------------------------------------------------ combat

        void Fight(WastelandGame g, Vector3 target, float dist, bool vehicleTarget, float dt, ref Vector3 move, ref float speed)
        {
            // hurt badly: break off once and come back (not bosses, not companions)
            if (!companion && !retreated && Profile.role != NpcRole.RaiderBoss && health < maxHealth * 0.35f)
            {
                retreated = true; mode = Mode.Flee; fleeUntil = Time.time + Random.Range(4f, 7f);
                return;
            }
            float want = Ranged ? 9f : vehicleTarget ? 2.2f : 1.25f;
            bool covering = false;
            if (Ranged && !vehicleTarget && dist < 30f)
            {
                // gunmen shoot from behind something solid
                if ((coverT -= dt) <= 0f) { coverT = Random.Range(2.5f, 4f); hasCover = FindCover(target, out cover); }
                if (hasCover && Flat(cover - transform.position).magnitude > 0.6f) { move = Toward(cover); speed = 3.4f; covering = true; }
            }
            if (!covering)
            {
                var to = Toward(target);
                if (!Ranged && dist > 4f) to = Quaternion.Euler(0f, flank * Mathf.Clamp01((dist - 4f) / 8f), 0f) * to;   // fan out, come in from the sides
                if (dist > want) { move = to; speed = dist > 12f ? 3.4f : 2.6f; }
                else if (Ranged && dist < 5f) { move = -Toward(target); speed = 2f; }        // keep the gun's distance
            }
            faceTarget = target; faceUntil = Time.time + 0.5f;
            if (speed < 0.1f) Face(Toward(target), dt);
            if ((attackCd -= dt) > 0f || swing >= 0f) return;
            if (Ranged && dist < 18f && speed < 3f) { Shoot(g, target, dist); attackCd = Random.Range(1.8f, 2.8f); }
            else if (!Ranged && dist < want + 0.5f) BeginSwing();
        }

        /// <summary>A spot 2.5–6 m away with something solid (a wall, a car, a rock, the lie of the land) between it
        /// and the threat.</summary>
        bool FindCover(Vector3 threat, out Vector3 best)
        {
            best = default; float bd = float.MaxValue;
            var me = transform.position;
            var t = DeformableTerrain.Instance;
            var g = WastelandGame.Instance;
            for (int i = 0; i < 10; i++)
            {
                var p = me + Quaternion.Euler(0f, i * 36f + (Profile.seed & 31), 0f) * Vector3.forward * (2.5f + (i % 3) * 1.6f);
                if (t) { if (t.WaterDepthNoLoad(p.x, p.z) > 0.3f) continue; p.y = t.HeightNoLoad(p.x, p.z); }
                if (Flat(p - threat).magnitude < 6f) continue;
                if (!Physics.Linecast(threat + Vector3.up * 1.3f, p + Vector3.up * 1.1f, out var hit, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.GetComponentInParent<Npc>() || hit.collider.GetComponentInParent<PlayerCharacter>()) continue;
                if (g && g.Current && hit.collider.transform.IsChildOf(g.Current.transform)) continue;
                if ((hit.point - p).sqrMagnitude > 16f) continue;                           // the cover must be right there
                float d = (p - me).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            return bd < float.MaxValue;
        }

        /// <summary>Raid: walk to the nearest built piece of the claim and smash it.</summary>
        void Siege(float dt, ref Vector3 move, ref float speed)
        {
            if (carBreaker && SiegeCar(dt, ref move, ref speed)) return;
            if (!siegeTarget || Time.time > siegeRepick)
            {
                siegeRepick = Time.time + 3f;
                siegeTarget = null; float bd = float.MaxValue;
                foreach (var p in Placeable.All)
                {
                    if (!p || p.Collapsing || (Flat(p.transform.position - raidAt)).sqrMagnitude > 45f * 45f || p.GetComponentInParent<Rigidbody>() || DefenceWorks.Concealed(p)) continue;   // mines and tripwires: unseen
                    float d = (p.transform.position - transform.position).sqrMagnitude;
                    if (PoweredLight.FloodlitAt(p.transform.position)) d += 30f * 30f;                  // they keep out of the floodlights
                    if (d < bd) { bd = d; siegeTarget = p; }
                }
            }
            if (!siegeTarget) { move = Toward(raidAt); speed = Flat(raidAt - transform.position).magnitude > 2f ? 2.6f : 0f; return; }
            var col = siegeTarget.GetComponent<Collider>();
            var cp = col ? col.bounds.ClosestPoint(transform.position + Vector3.up) : siegeTarget.transform.position;
            if (Flat(cp - transform.position).magnitude > 1.2f) { move = Toward(cp); speed = 2.8f; return; }
            faceTarget = cp; faceUntil = Time.time + 0.5f;
            if ((attackCd -= dt) > 0f || swing >= 0f) return;
            swing = 0f; attackCd = 1.4f;
            siegeTarget.ApplyHit(cp, transform.forward, Profile.role == NpcRole.RaiderBoss ? 2f : 1f, 0.2f, gameObject);
            MadMax.Audio.Sfx.Play(siegeTarget.id.Contains("wood") ? "hit_wood" : "hit_metal", cp, 0.7f);
        }

        // ------------------------------------------------------------------ companions man the base
        AutoTurret manning;
        AlarmBell ringing;
        float postCheck, rangUntil;

        /// <summary>A companion guarding a claim when hostiles come within 70 m of the post: rings the claim's bell first,
        /// then takes the nearest free turret (manned turrets fire without power, faster and truer). False when there is
        /// nothing to man (fights as usual).</summary>
        bool ManPost(Vector3 me, float dt, ref Vector3 move, ref float speed)
        {
            if ((postCheck -= dt) <= 0f)
            {
                postCheck = 1f;
                bool threat = false;
                foreach (var n in All) if (n && n != this && !n.companion && n.Alive && (n.Hostile || n.raiding) && (n.transform.position - home).sqrMagnitude < 70f * 70f) { threat = true; break; }
                if (!threat) { LeavePost(); return false; }
                if (!ringing && Time.time > rangUntil)
                    foreach (var b in AlarmBell.All) if (b && (b.transform.position - home).sqrMagnitude < 40f * 40f) { ringing = b; break; }
                if (!manning || (manning.gunner && manning.gunner != this && manning.gunner.Alive))
                {
                    manning = null; float bd = 30f * 30f;
                    foreach (var p in Placeable.All)
                    {
                        if (!p || !p.TryGetComponent<AutoTurret>(out var t) || !t.on || (t.gunner && t.gunner != this && t.gunner.Alive)) continue;
                        float d = (p.transform.position - home).sqrMagnitude;
                        if (d < bd) { bd = d; manning = t; }
                    }
                }
            }
            if (!ringing && !manning) return false;
            var at = ringing ? ringing.transform.position : manning.transform.position - manning.transform.forward * 1.1f;   // behind the gun
            if (Flat(at - me).magnitude > 1.3f) { move = Toward(at); speed = 3.6f; return true; }
            if (ringing) { ringing.Ring(true); ringing = null; rangUntil = Time.time + 180f; return true; }
            manning.gunner = this;
            if (manning.Target) { faceTarget = manning.Target.transform.position; faceUntil = Time.time + 0.5f; }
            return true;
        }

        void LeavePost()
        {
            if (manning && manning.gunner == this) manning.gunner = null;
            manning = null; ringing = null;
        }

        /// <summary>A car breaker's raid: the nearest unattended vehicle within the claim, fuel siphoned and bodywork
        /// smashed. False when there is none left worth it.</summary>
        int breakerBlows;
        /// <summary>Every this many blows a car breaker unbolts a part and walks off with it.</summary>
        public const int BreakerStealEvery = 4;

        bool SiegeCar(float dt, ref Vector3 move, ref float speed)
        {
            var g = WastelandGame.Instance;
            if (!siegeCar || Time.time > siegeRepick || siegeCar == g.Current)
            {
                siegeCar = null; float bd = float.MaxValue;
                foreach (var v in g.AllVehicles)
                {
                    if (!v || v == g.Current || v.aiDriven || v.Occupied || Flat(v.transform.position - raidAt).sqrMagnitude > 45f * 45f) continue;
                    if (v.TryGetComponent<MadMax.Vehicles.VehicleDamage>(out var vd) && vd.FrameDamage > 0.8f) continue;       // wrecked enough
                    float d = (v.transform.position - transform.position).sqrMagnitude;
                    if (d < bd) { bd = d; siegeCar = v; }
                }
                if (!siegeCar) { carBreaker = false; return false; }
            }
            var cp = siegeCar.Body.ClosestPointOnBounds(transform.position + Vector3.up);
            if (Flat(cp - transform.position).magnitude > 1.3f) { move = Toward(cp); speed = 2.8f; return true; }
            faceTarget = cp; faceUntil = Time.time + 0.5f;
            if ((attackCd -= dt) > 0f || swing >= 0f) return true;
            swing = 0f; attackCd = 1.6f;
            if (siegeCar.TryGetComponent<MadMax.Vehicles.VehicleSystems>(out var sys)) sys.fuel = Mathf.Max(0f, sys.fuel - 3f);
            if (siegeCar.TryGetComponent<MadMax.Vehicles.VehicleDamage>(out var dmg)) dmg.ApplyHit(cp, transform.forward, Profile.role == NpcRole.RaiderBoss ? 2f : 1f, 0.3f, gameObject);
            MadMax.Audio.Sfx.Play("hit_metal", cp, 0.8f);
            if (++breakerBlows % BreakerStealEvery == 0) g.BreakerSteals(siegeCar, raidAt, Profile.gang);     // a part off it, to be sold on in town
            return true;
        }

        void MeleeHit()
        {
            var g = WastelandGame.Instance;
            if (!g || mode == Mode.Dead) return;
            var hitAt = transform.position + transform.forward * 0.9f + Vector3.up * 1.1f;
            float power = Profile.role == NpcRole.RaiderBoss ? 1.3f : 1f;
            if (foe)
            {
                if (Vector3.Distance(foe.transform.position, transform.position) < 2.2f)
                {
                    foe.ApplyHit(hitAt, transform.forward, power * 0.85f, 0.3f, gameObject);
                    MadMax.Audio.Sfx.Play(tool && tool.id.Contains("machete") ? "scratch" : "punch", hitAt, 0.8f);
                }
                else MadMax.Audio.Sfx.Play("punch", hitAt, 0.3f, 0.7f);
                return;
            }
            if (g.Current)
            {
                if (Vector3.Distance(g.Current.transform.position, transform.position) < 4f)
                {
                    g.Current.GetComponent<VehicleDamage>()?.ApplyHit(hitAt, transform.forward, power * 0.4f, 0.3f, gameObject);
                    MadMax.Audio.Sfx.Play("hit_metal", hitAt, 0.7f);
                }
                return;
            }
            var pp = g.Player.transform.position;
            if (Vector3.Distance(pp, transform.position) > 2f || Vector3.Dot(Flat(pp - transform.position).normalized, transform.forward) < 0.3f) { MadMax.Audio.Sfx.Play("punch", hitAt, 0.3f, 0.7f); return; }
            if (boutStrike != null) { boutStrike(this); MadMax.Audio.Sfx.Play("punch", pp + Vector3.up, 0.6f, 1.1f); return; }   // a supervised bout scores it
            g.HitBladed = Limbs.Bladed(Profile.tool);
            g.Vitals.Hurt(Random.Range(7f, 13f) * power, "MELEE");
            g.Player.React(power >= 1.2f);
            BloodStains.Splash(pp, 0.4f);
            MadMax.Audio.Sfx.Play(tool && tool.id.Contains("machete") ? "scratch" : "punch", pp + Vector3.up, 0.8f);
        }

        void Shoot(WastelandGame g, Vector3 target, float dist)
        {
            if (foe) { ShootAt(foe, dist); return; }
            Blast(gameObject, transform.position + Vector3.up * 1.35f + transform.forward * 0.5f, target + Vector3.up, dist, 1f);
        }

        /// <summary>A shot at another person; holds fire when the player (or their vehicle) is in the line.</summary>
        void ShootAt(Npc f, float dist)
        {
            var g = WastelandGame.Instance;
            var muzzle = transform.position + Vector3.up * 1.35f + transform.forward * 0.5f;
            var aim = (f.transform.position + Vector3.up * 1.1f - muzzle).normalized;
            if (g && Physics.Raycast(muzzle, aim, out var hit, dist + 2f, ~0, QueryTriggerInteraction.Ignore)
                && (hit.collider.transform.IsChildOf(g.Player.transform) || (g.Current && hit.collider.transform.IsChildOf(g.Current.transform)))) return;
            MadMax.Audio.Sfx.Play("shotgun", muzzle, 0.9f, Random.Range(0.95f, 1.1f), 110f);
            var fx = DebrisSystem.Instance;
            if (fx) for (int i = 0; i < 3; i++) fx.EmitPuff(muzzle + aim * 0.6f, new Color32(255, 200, 90, 255), 0.06f, aim * Random.Range(2f, 5f) + Random.insideUnitSphere, 0.12f);
            if (Random.value < Mathf.Clamp01(1.05f - dist / 22f)) f.ApplyHit(f.transform.position + Vector3.up, aim, 0.45f, 0.1f, gameObject);
        }

        /// <summary>A shotgun blast (or turret round, power > 1) from <paramref name="muzzle"/> at the player or their
        /// vehicle. Misses more at range and against a moving target; hits dent the vehicle and can wound the driver.</summary>
        public static void Blast(GameObject source, Vector3 muzzle, Vector3 target, float dist, float power)
        {
            var g = WastelandGame.Instance;
            if (!g) return;
            MadMax.Audio.Sfx.Play(power > 1.2f ? "explosion" : "shotgun", muzzle, power > 1.2f ? 0.6f : 1f, Random.Range(0.9f, 1.05f), 120f);
            var fx = DebrisSystem.Instance;
            var aim = (target - muzzle).normalized;
            if (fx) for (int i = 0; i < 4; i++) fx.EmitPuff(muzzle + aim * 0.6f, new Color32(255, 200, 90, 255), 0.07f * power, aim * Random.Range(2f, 5f) + Random.insideUnitSphere, 0.12f);
            float moving = g.Current ? Mathf.Abs(g.Current.ForwardSpeed) : g.Player.Velocity.magnitude;
            float chance = Mathf.Clamp01(1.05f - dist / 20f - moving * 0.03f);
            if (SmokeScreen.Blocks(muzzle, target)) chance *= 0.15f;                            // firing blind into smoke
            int n = Physics.RaycastNonAlloc(muzzle, aim, shotHits, dist + 3f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit hit = default; float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (shotHits[i].distance < best && !shotHits[i].collider.transform.IsChildOf(source.transform)) { best = shotHits[i].distance; hit = shotHits[i]; }
            if (best == float.MaxValue) return;
            if (Random.value > chance) { if (fx) fx.EmitPuff(hit.point, new Color32(190, 150, 110, 255), 0.06f, hit.normal, 0.5f); return; }
            if (g.Current && hit.collider.GetComponentInParent<VehicleDriver>() == g.Current)
            {
                g.Current.GetComponent<VehicleDamage>()?.ApplyHit(hit.point, aim, 0.5f * power, 0.3f * power, source);
                MadMax.Audio.Sfx.Play("hit_metal", hit.point, 0.7f);
                float grille = g.Current.TryGetComponent<VehicleArmor>(out var arm) ? arm.GrilleCover : 0f;
                if (Random.value < 0.12f * (1f - grille)) g.Vitals.Hurt(Random.Range(5f, 10f), "SHOT");  // through the glass (grilles catch most)
            }
            else if (!g.Current && hit.collider.transform.IsChildOf(g.Player.transform))
            {
                g.Vitals.Hurt(Random.Range(12f, 24f) * power * (1f - Mathf.Min(dist, 25f) / 30f), "SHOT");
                BloodStains.Splash(g.Player.transform.position, 0.6f);
            }
            else hit.collider.GetComponentInParent<IDamageable>()?.ApplyHit(hit.point, aim, 0.3f * power, 0.1f, source);
        }

        static readonly RaycastHit[] shotHits = new RaycastHit[8];

        /// <summary>Fast vehicles knock people over: checked a little ahead of the bumper, so the hit lands before the
        /// car meets the (immovable) CharacterController capsule. The person is thrown along the car's path as a limp
        /// body (<see cref="BodyKnock"/>); whoever survives gets up again a few seconds later.</summary>
        void RunOver(WastelandGame g)
        {
            var me = transform.position + Vector3.up * 0.8f;
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.Body || v.Body.isKinematic) continue;
                var d = v.transform.position - transform.position;
                if (d.sqrMagnitude > 64f) continue;
                var vel = v.Body.linearVelocity;
                float sp = vel.magnitude;
                if (sp < 4f) continue;
                var cp = VehicleStorage.Closest(v, me);
                if (!BodyKnock.InPath(me - cp, vel, 0.5f, sp * Mathf.Max(0.05f, Time.deltaTime * 2f))) continue;   // touching, or about to: not passing close beside
                ApplyHit(transform.position + Vector3.up, vel.normalized, sp * sp * 0.03f, 0.3f, v.gameObject);
                NpcVoice.CarHit(this);
                if (mode != Mode.Dead) KnockDown(vel);
                var rd = rig.GetComponent<Ragdoll>();
                if (rd && rd.Active && rig.TryGetComponent<BodyKnock>(out var knock)) knock.Knock(v, cp, false);
                return;
            }
        }

        float downUntil;
        /// <summary>Knocked down and lying limp (gets up after <see cref="downUntil"/>).</summary>
        public bool Down => downUntil > 0f;

        /// <summary>Struck hard but alive: limp on the ground for a few seconds.</summary>
        void KnockDown(Vector3 velocity)
        {
            if (Down || mode == Mode.Dead) return;
            if (seated) { seated = false; LeaveFire(); }
            cc.enabled = false;
            Ragdoll.For(rig).Go(velocity.normalized * 60f, transform.position + Vector3.up * 1.1f, lastVelocity);
            downUntil = Time.time + Random.Range(2.5f, 4f);
            mode = Hostile ? Mode.Fight : Mode.Flee; fleeUntil = Time.time + 12f;
        }

        /// <summary>Lying after a knock-down: up again once the body has come to rest where it fell.</summary>
        void DownTick()
        {
            var rd = rig.GetComponent<Ragdoll>();
            if (!rd || !rd.Active) { downUntil = 0f; cc.enabled = true; return; }
            var pelvis = rig.bones[BodyPart.Pelvis];
            var prb = pelvis ? pelvis.GetComponent<Rigidbody>() : null;
            if (Time.time < downUntil || (prb && !prb.isKinematic && prb.linearVelocity.sqrMagnitude > 1f && Time.time < downUntil + 4f)) return;
            var at = rd.Pelvis;
            var terrain = DeformableTerrain.Instance;
            float ground = terrain ? terrain.HeightNoLoad(at.x, at.z) : at.y - 0.9f;
            rd.Restore();
            downUntil = 0f;
            var fwd = Flat(lastBlow); if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            transform.SetPositionAndRotation(new Vector3(at.x, Mathf.Max(ground + 0.05f, at.y - 1.2f), at.z), Quaternion.LookRotation(-fwd.normalized));
            lastPos = transform.position;
            vy = 0f;
            cc.enabled = true;
        }

        public void ApplyHit(Vector3 point, Vector3 direction, float power, float radius, GameObject source)
        {
            if (mode == Mode.Dead) return;
            if (power > 0.25f && rig) rig.Bleed(point, Mathf.Clamp(power, 0.6f, 1.6f));                  // a splat where it struck
            if (proxy)                                                                         // the host decides; blood here at once
            {
                BloodStains.Splash(point, Mathf.Clamp01(power * 0.5f));
                MadMax.Net.NetSession.Instance?.SendActorHit(netId, point, direction, power, radius);
                return;
            }
            if (boutHit != null && boutHit(this, point, direction, power, source)) return;       // a supervised bout scores it instead
            // their armour: gunfire (player guns, vehicle weapons) or blows
            var gp = WastelandGame.Instance;
            int kind = (int)DamageKind.Melee;
            if (gp && source && (source.GetComponentInParent<VehicleDriver>() || (gp.Player && gp.Player.Tool is RangedTool && source.transform.IsChildOf(gp.Player.transform)))) kind = (int)DamageKind.Shot;
            float armour = WastelandGame.NpcProtection(rig.outfit, kind);
            if (armour > 0.25f) MadMax.Audio.Sfx.Play("hit_metal", point, 0.5f, 1.2f);
            float dmg = power * 28f * (1f - 0.7f * armour);
            health -= dmg;
            TryDismember(point, dmg, source);
            lastBlow = direction.normalized * Mathf.Clamp(power * 90f, 30f, 400f);
            lastHurt = Time.time;
            BloodStains.Splash(transform.position, Mathf.Clamp01(dmg / 40f));
            MadMax.Audio.Sfx.Play("punch", point, 0.7f, Random.Range(0.8f, 1.1f));
            var g = WastelandGame.Instance;
            var byNpc = source ? source.GetComponentInParent<Npc>() : null;
            if (byNpc && byNpc != this) { hurtBy = byNpc; hurtByUntil = Time.time + 8f; }
            bool byPlayer = g && source && (source.transform.IsChildOf(g.Player.transform) || (g.Current && source.transform.IsChildOf(g.Current.transform))
                            || (byNpc && byNpc.companion) || OwnedPiece(g, source));
            lastByPlayer = byPlayer;
            if (health <= 0f) { Die(byPlayer); return; }
            React(direction, power, dmg);
            // beaten: throw the weapon down (less likely with the boss still up and friends around)
            if (!companion && Hostile && health < maxHealth * 0.22f && Profile.role != NpcRole.RaiderBoss)
            {
                float chance = 0.3f + (convoy != null && (!convoy.Boss || !convoy.Boss.Alive) ? 0.3f : 0f) + (AlliesNear() == 0 ? 0.25f : 0f);
                if (Random.value < chance) { Surrender(g); return; }
            }
            if (!byPlayer) return;
            if (companion)
            {
                if (byNpc && byNpc.companion) return;
                State.disposition = Mathf.Max(-100, State.disposition - 5);                    // friendly fire
                g.Toast(Profile.Name + ": HEY! WATCH IT!");
                return;
            }
            State.disposition = Mathf.Max(-100, State.disposition - 30);
            convoy?.Provoked();
            // armed or proud folk fight back, the rest run
            if (Profile.Raider || tool && !(tool is LightTool) || Profile.temper == Temper.Proud || Profile.temper == Temper.Gruff) State.Set(NpcSave.Hostile);
            else { mode = Mode.Flee; fleeUntil = Time.time + 10f; }
            Alarm(g);
        }

        /// <summary>Hits from the player's own turret count as theirs (bounties, reputation).</summary>
        static bool OwnedPiece(WastelandGame g, GameObject source)
        {
            if (!g || !source) return false;
            var p = source.GetComponentInParent<Placeable>();
            return p && g.OwnsPiece(p) && !string.IsNullOrEmpty(p.owner);
        }

        int AlliesNear()
        {
            int k = 0;
            foreach (var n in All) if (n && n != this && n.Alive && n.Hostile && (n.transform.position - transform.position).sqrMagnitude < 15f * 15f) k++;
            return k;
        }

        /// <summary>A dropped tool falls (it may already carry a body from an earlier drop).</summary>
        static void DropPhysics(GameObject go)
        {
            if (!go.TryGetComponent<Rigidbody>(out var rb)) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = false; rb.mass = 2f;
        }

        /// <summary>Drop the weapon, hands up: out of the fight. [E] decides what happens to them.</summary>
        void Surrender(WastelandGame g)
        {
            State.Set(NpcSave.Surrendered);
            aggro = false; raiding = false; foe = null; mode = Mode.Surrender;
            if (tool && !(tool is LightTool))
            {
                tool.transform.SetParent(null, true);
                DropPhysics(tool.gameObject);
                Destroy(tool.gameObject, 60f);
                tool = null;
            }
            if (g) g.Toast(Profile.Name + " THROWS DOWN THEIR WEAPON AND SURRENDERS  [E]");
        }

        /// <summary>Let a surrendered or dismissed person go: they walk off and are gone once out of sight.</summary>
        public void LetGo()
        {
            leaving = true;
            home = transform.position + Random.insideUnitSphere.normalized * 40f;
            homeRadius = 20f; hasGoal = false;
        }

        /// <summary>Peaceful folk around see the attack: they turn on the player or run.</summary>
        void Alarm(WastelandGame g)
        {
            if (Profile.Raider) return;
            foreach (var n in All)
            {
                if (n == this || !n.Alive || n.Profile.Raider || n.companion || (n.transform.position - transform.position).sqrMagnitude > 25f * 25f) continue;
                n.State.disposition = Mathf.Max(-100, n.State.disposition - 15);
                if (n.tool && !(n.tool is LightTool) && n.Profile.temper != Temper.Nervous) n.State.Set(NpcSave.Hostile);
                else { n.mode = Mode.Flee; n.fleeUntil = Time.time + 8f; }
            }
        }

        public void Scare(float seconds) { if (mode == Mode.Dead || Hostile || companion) return; mode = Mode.Flee; fleeUntil = Time.time + seconds; }

        void Die(bool byPlayer)
        {
            bool spared = Surrendered;
            mode = Mode.Dead;
            State.dead = true;
            LeaveFire();
            if (rideSeat) { rideSeat.Occupant = null; rideSeat = null; }
            if (drivenCar) LeaveWheel();
            if (asleep) SetAsleep(false);
            if (companion) Companions.Lost(this);
            // standing (roadmap 21): the dead one's faction remembers; its friends and enemies take note
            var side = Factions.Of(this);
            if (byPlayer && spared) { Factions.Shift(Faction.Settlers, -10, false); Factions.Shift(side, -8); WastelandGame.Instance?.Toast("YOU KILLED SOMEONE WHO HAD GIVEN UP"); }
            else if (byPlayer && !Profile.Raider && !State.Has(NpcSave.Hostile) && !companion) Factions.Shift(side == Faction.None ? Faction.Settlers : side, -15);
            else if (byPlayer && Profile.Raider) Factions.Shift(side, -6);
            if (byPlayer) Contracts.ReportKill(this);                                        // bounties
            cc.enabled = false;
            if (tool) { tool.transform.SetParent(null, true); DropPhysics(tool.gameObject); Destroy(tool.gameObject, 60f); }
            // go limp: a physics ragdoll takes the blow and falls where it may; the body is searchable at the pelvis
            var push = lastBlow.sqrMagnitude > 0.01f ? lastBlow : -transform.forward * 60f;
            Ragdoll.For(rig).Go(push, transform.position + Vector3.up * 1.1f, lastVelocity);
            var pelvis = rig.bones[BodyPart.Pelvis].gameObject;
            var loot = pelvis.AddComponent<Lootable>();
            loot.key = "N" + Profile.id; loot.table = Profile.Raider ? "raider" : Profile.Vendor ? "shop" : "house";
            loot.title = Profile.Name + "'S BODY";
            foreach (var o in rig.outfit)                                                     // strip their armour
            {
                var cd = ClothingLibrary.Get(o);
                if (cd?.armor != null && Random.value < 0.7f) loot.extra.Add(ClothingLibrary.ItemId(cd));
            }
            if (carriedKey != null) loot.extra.Add(carriedKey);                               // the car key on their ring
            if (pack) foreach (var kv in new List<KeyValuePair<string, int>>(pack.inventory.Items)) for (int i = 0; i < kv.Value; i++) loot.extra.Add(kv.Key);   // what they carried for you
            MadMax.Game.LastEngine.BossDrop(Profile, loot);
            MadMax.Audio.Sfx.Play("bone", transform.position, 0.8f);
            convoy?.MemberDied(this);
        }

        // ------------------------------------------------------------------ talk / trade

        /// <summary>The name once you have met (talked to them, or they ride with you); until then what you see of them.</summary>
        public string KnownName => State.Has(NpcSave.Met) || companion ? Profile.Name : Stranger;

        string Stranger => Profile.role switch
        {
            NpcRole.Shopkeeper => "THE SHOPKEEPER", NpcRole.Stallkeeper => "THE STALLHOLDER", NpcRole.Trader => "A TRADER",
            NpcRole.Raider => "A RAIDER", NpcRole.RaiderBoss => "THE GANG'S BOSS", NpcRole.Leader => "THE TOWN'S HEAD",
            NpcRole.Packer => "A PACKER", NpcRole.Resident => "A LOCAL", _ => "A STRANGER"
        };

        public string Prompt(WastelandGame g)
        {
            if (mode == Mode.Dead || !Available) return null;
            if (Surrendered) return "[E] " + KnownName + " (SURRENDERED)";
            if (Hostile && !Profile.Raider) return KnownName + " WANTS YOU DEAD";
            string s = "[E] " + (Profile.Raider ? "PARLEY WITH " : companion ? "ORDERS FOR " : "TALK TO ") + KnownName;
            if (companion) s += "  [T] THEIR PACK";
            else if (Profile.Vendor && !Hostile && State.disposition > -40) s += Closed ? "  (CLOSED TILL 7:00)" : "  [T] TRADE";
            return s;
        }

        public void Use(WastelandGame g, bool secondary)
        {
            if (mode == Mode.Dead) return;
            if (secondary)
            {
                if (companion) { EnsurePack(); g.Menus.OpenContainer(pack); return; }
                if (Profile.Vendor && !Hostile)
                {
                    // the same refusals as the talk page's "SHOW ME WHAT YOU'VE GOT" ([T] used to skip them)
                    if (Closed) g.Toast("CLOSED - COME BACK AFTER SUNRISE");
                    else if (State.disposition <= -40) g.Toast(Profile.Name + ": I DON'T SELL TO YOUR KIND.");
                    else if (Factions.Hostile(Factions.Of(this))) g.Toast("THE " + Factions.Names[(int)Factions.Of(this)] + " DON'T TRADE WITH YOU");
                    else g.Menus.OpenTalk(this, true);
                }
                return;
            }
            g.Menus.OpenTalk(this, false);
        }

        /// <summary>Look at the speaker while a conversation is open.</summary>
        public void Attend(Vector3 at) { faceTarget = at; faceUntil = Time.time + 1f; if (mode == Mode.Wander) hasGoal = false; }

        float chatUntil;
        Vector3 chatAt;
        /// <summary>Stop and face <paramref name="at"/> for a while: a chat with a neighbour (<see cref="NpcVoice"/>).</summary>
        public void Chat(Vector3 at, float seconds) { chatAt = at; chatUntil = Time.time + seconds; }
        /// <summary>Where the voice comes from.</summary>
        public Transform Head => rig ? rig.Head : null;
        /// <summary>The body (bones, HD or voxel look) and its animator (tests, tools).</summary>
        public HumanRig Rig => rig;
        public HumanAnimator Animator => anim;

        public void Heal() => health = maxHealth;

        /// <summary>A supervised bout (story system "nonlethal_bout", <see cref="MadMax.Game.BoutRing"/>): while set, blows
        /// on this person go to the handler first (true = scored there, no harm done), and their own blows on the player
        /// are scored by <see cref="boutStrike"/> instead of hurting.</summary>
        [System.NonSerialized] public System.Func<Npc, Vector3, Vector3, float, GameObject, bool> boutHit;
        [System.NonSerialized] public System.Action<Npc> boutStrike;

        // ------------------------------------------------------------------ clinic (depth stage G)
        Transform berth; bool berthed; Vector3 berthExit;
        public float MaxHealth => maxHealth;
        /// <summary>Hurt or bleeding: worth a clinic bed.</summary>
        public bool Wounded => mode != Mode.Dead && (health < maxHealth - 0.5f || Time.time < bleedUntil);
        /// <summary>The clinic bed spot this person lies on (hips; null when up and about).</summary>
        public Transform Berthed => berthed ? berth : null;
        /// <summary>Treatment: the bleeding stops and <paramref name="hp"/> health comes back.</summary>
        public void Mend(float hp) { bleedUntil = 0f; health = Mathf.Min(maxHealth, health + hp); }

        /// <summary>Lie on a clinic bed: hips at <paramref name="at"/>, AI paused; <paramref name="exit"/> is where to get up.</summary>
        public void Berth(Transform at, Vector3 exit)
        {
            if (!at || mode == Mode.Dead) return;
            berth = at; berthed = true; berthExit = exit;
            LeaveFire(); SetAsleep(false);
            cc.enabled = false; hasGoal = false;
        }

        /// <summary>Get up from the clinic bed.</summary>
        public void Unberth()
        {
            if (!berthed) return;
            berthed = false; berth = null;
            transform.position = berthExit;
            cc.enabled = mode != Mode.Dead;
        }
    }
}
