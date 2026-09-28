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
    /// Stands at a post (shopkeepers, stallkeepers), wanders around home (residents, wanderers), flees when scared, or
    /// fights (raiders, anyone pushed too far): melee within reach or shotgun blasts at range, at the player on foot or
    /// at the player's vehicle. Talk with [E], trade with [T]. Takes hits through <see cref="IDamageable"/>, gets run
    /// over by fast vehicles, dies into a searchable body; killing peaceful folk costs reputation.</summary>
    public class Npc : MonoBehaviour, IInteractable, IDamageable
    {
        public static readonly List<Npc> All = new List<Npc>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public enum Mode { Stand, Wander, Flee, Fight, Dead }
        public NpcProfile Profile { get; private set; }
        public NpcSave State { get; private set; }
        public Mode mode = Mode.Wander;
        public Vector3 home;
        public float homeRadius = 8f, homeYaw;
        /// <summary>Raiders of a convoy that is attacking.</summary>
        public bool aggro;
        public Convoy convoy;
        public bool Hostile => mode != Mode.Dead && (State.Has(NpcSave.Hostile) || (Profile.Raider && aggro));
        public bool Alive => mode != Mode.Dead;
        public float Health => health;

        HumanRig rig;
        HumanAnimator anim;
        CharacterController cc;
        HandTool tool;
        float health, maxHealth, vy, swing = -1f, attackCd, repath, fleeUntil, faceUntil, stuck, lastHurt;
        Vector3 goal, lastPos;
        bool hasGoal;
        Vector3 faceTarget;

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
            n.rig.outfit.AddRange(p.outfit);
            n.rig.Rebuild();
            n.anim = new HumanAnimator(n.rig);
            n.maxHealth = n.health = p.role == NpcRole.RaiderBoss ? 160f : p.Raider ? 90f : 70f;
            n.SetTool(p.tool, mat);
            n.lastPos = pos;
            return n;
        }

        void SetTool(string id, Material mat)
        {
            if (tool) Destroy(tool.gameObject);
            tool = id != null ? ToolLibrary.Create(id, mat) : null;
            if (!tool) return;
            tool.transform.SetParent(rig.RightHand, false);
            tool.transform.localPosition = new Vector3(0f, -0.055f, 0.01f);
            tool.transform.localRotation = Quaternion.identity;
            tool.enabled = tool is LightTool;                              // torches keep their flame; nothing strikes by itself
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        bool Ranged => tool is RangedTool;

        // ------------------------------------------------------------------ brain

        void Update()
        {
            if (mode == Mode.Dead) return;
            float dt = Time.deltaTime;
            var g = WastelandGame.Instance;
            if (!g || g.Player == null) return;
            var terrain = DeformableTerrain.Instance;
            // wanderers light a torch after dark
            if (Profile.role == NpcRole.Wanderer && Profile.tool == null && (DayNight.Darkness > 0.45f) != (tool is LightTool))
                SetTool(DayNight.Darkness > 0.45f ? "tool_torch" : null, rig.material);

            Vector3 me = transform.position;
            var playerPos = g.Current ? g.Current.transform.position : g.Player.transform.position;
            float dPlayer = Vector3.Distance(me, playerPos);
            RunOver(g);

            Vector3 move = Vector3.zero;
            float speed = 0f;
            if (Hostile && dPlayer < 60f && !g.Vitals.Dead) mode = Mode.Fight;
            else if (mode == Mode.Fight) mode = Profile.role == NpcRole.Shopkeeper || Profile.role == NpcRole.Stallkeeper ? Mode.Stand : Mode.Wander;
            if (mode == Mode.Flee && Time.time > fleeUntil) mode = Profile.role == NpcRole.Shopkeeper || Profile.role == NpcRole.Stallkeeper ? Mode.Stand : Mode.Wander;

            switch (mode)
            {
                case Mode.Stand:
                    if ((me - home).sqrMagnitude > 0.25f) { move = Toward(home); speed = 1.2f; }
                    else if (Time.time > faceUntil) FaceYaw(homeYaw, dt);
                    break;
                case Mode.Wander:
                    if (!hasGoal || Time.time > repath || Flat(goal - me).magnitude < 0.6f)
                    {
                        if (hasGoal && Flat(goal - me).magnitude < 0.6f && Random.value < 0.6f) { hasGoal = false; repath = Time.time + Random.Range(3f, 9f); break; }  // linger
                        if (Time.time > repath) PickGoal(terrain);
                    }
                    if (hasGoal) { move = Toward(goal); speed = 1.25f; }
                    break;
                case Mode.Flee:
                {
                    var away = Flat(me - playerPos).normalized;
                    move = away; speed = 3.6f;
                    break;
                }
                case Mode.Fight:
                    Fight(g, playerPos, dPlayer, dt, ref move, ref speed);
                    break;
            }
            if (mode != Mode.Fight && dPlayer < 3.5f && !g.Current) { faceTarget = playerPos; faceUntil = Time.time + 2f; }

            // movement: CharacterController against buildings and props, gravity, stuck → new goal
            if (move.sqrMagnitude > 0.001f && !Blocked(terrain, me, move)) Face(move, dt);
            else if (move.sqrMagnitude > 0.001f) { hasGoal = false; move = Vector3.zero; speed = 0f; }
            if (Time.time < faceUntil && speed < 0.1f) Face(Flat(faceTarget - me), dt);
            vy = cc.isGrounded ? -1f : vy + Physics.gravity.y * dt;
            if (cc.enabled) cc.Move((move.normalized * speed + Vector3.up * vy) * dt);
            if (terrain)
            {
                var p = transform.position;
                float h = terrain.Height(p.x, p.z);
                if (p.y < h - 0.3f) { p.y = h + 0.05f; transform.position = p; }
            }
            float moved = Flat(transform.position - lastPos).magnitude / Mathf.Max(dt, 1e-4f);
            lastPos = transform.position;
            if (speed > 0.5f && moved < 0.2f) { if ((stuck += dt) > 1.5f) { stuck = 0f; hasGoal = false; repath = 0f; } } else stuck = 0f;

            if (swing >= 0f)
            {
                swing += dt / (tool ? tool.swingDuration : 0.5f);
                if (swing >= 1f) swing = -1f;
            }
            anim.Tick(dt, new HumanAnimator.State
            {
                speed = moved, grounded = cc.isGrounded, verticalSpeed = vy,
                tool = tool ? (swing >= 0f ? tool.Pose(swing) : tool.IdlePose) : (ToolPose?)null, twoHanded = tool && tool.TwoHanded
            });
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        Vector3 Toward(Vector3 p) => Flat(p - transform.position);

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
                if (terrain && terrain.WaterDepth(p.x, p.z) > 0.3f) continue;
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
            if (terrain.WaterDepth(ahead.x, ahead.z) > 0.6f && mode != Mode.Fight) return true;
            return terrain.Height(ahead.x, ahead.z) - me.y > 1.2f;
        }

        // ------------------------------------------------------------------ combat

        void Fight(WastelandGame g, Vector3 target, float dist, float dt, ref Vector3 move, ref float speed)
        {
            bool inVehicle = g.Current;
            float want = Ranged ? 9f : inVehicle ? 2.2f : 1.25f;
            var to = Toward(target);
            if (dist > want) { move = to; speed = dist > 12f ? 3.4f : 2.6f; }
            else if (Ranged && dist < 5f) { move = -to; speed = 2f; }        // keep the gun's distance
            faceTarget = target; faceUntil = Time.time + 0.5f;
            if (speed < 0.1f) Face(to, dt);
            if ((attackCd -= dt) > 0f || swing >= 0f) return;
            if (Ranged && dist < 18f) { Shoot(g, target, dist); attackCd = Random.Range(1.8f, 2.8f); }
            else if (!Ranged && dist < want + 0.5f) { swing = 0f; attackCd = 1.3f; Invoke(nameof(MeleeHit), (tool ? tool.swingDuration * tool.strikeAt : 0.3f)); }
        }

        void MeleeHit()
        {
            var g = WastelandGame.Instance;
            if (!g || mode == Mode.Dead) return;
            var hitAt = transform.position + transform.forward * 0.9f + Vector3.up * 1.1f;
            float power = Profile.role == NpcRole.RaiderBoss ? 1.3f : 1f;
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
            g.Vitals.Hurt(Random.Range(7f, 13f) * power, "MELEE");
            BloodStains.Splash(pp, 0.4f);
            MadMax.Audio.Sfx.Play(tool && tool.id.Contains("machete") ? "scratch" : "punch", pp + Vector3.up, 0.8f);
        }

        void Shoot(WastelandGame g, Vector3 target, float dist) => Blast(gameObject, transform.position + Vector3.up * 1.35f + transform.forward * 0.5f, target + Vector3.up, dist, 1f);

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
                if (Random.value < 0.25f) g.Vitals.Hurt(Random.Range(6f, 14f), "SHOT");  // through the glass
            }
            else if (!g.Current && hit.collider.transform.IsChildOf(g.Player.transform))
            {
                g.Vitals.Hurt(Random.Range(12f, 24f) * power * (1f - Mathf.Min(dist, 25f) / 30f), "SHOT");
                BloodStains.Splash(g.Player.transform.position, 0.6f);
            }
            else hit.collider.GetComponentInParent<IDamageable>()?.ApplyHit(hit.point, aim, 0.3f * power, 0.1f, source);
        }

        static readonly RaycastHit[] shotHits = new RaycastHit[8];

        /// <summary>Fast vehicles knock people over.</summary>
        void RunOver(WastelandGame g)
        {
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.Body || v.Body.isKinematic) continue;
                var d = v.transform.position - transform.position;
                if (d.sqrMagnitude > 16f) continue;
                float sp = v.Body.linearVelocity.magnitude;
                if (sp < 4f) continue;
                var cp = v.Body.ClosestPointOnBounds(transform.position + Vector3.up * 0.8f);
                if ((cp - (transform.position + Vector3.up * 0.8f)).sqrMagnitude > 0.5f * 0.5f) continue;
                ApplyHit(transform.position + Vector3.up, v.Body.linearVelocity.normalized, sp * sp * 0.03f, 0.3f, v.gameObject);
                if (mode == Mode.Dead) transform.position += v.Body.linearVelocity.normalized * 1.2f;
                return;
            }
        }

        public void ApplyHit(Vector3 point, Vector3 direction, float power, float radius, GameObject source)
        {
            if (mode == Mode.Dead) return;
            float dmg = power * 28f;
            health -= dmg;
            lastHurt = Time.time;
            BloodStains.Splash(transform.position, Mathf.Clamp01(dmg / 40f));
            MadMax.Audio.Sfx.Play("punch", point, 0.7f, Random.Range(0.8f, 1.1f));
            var g = WastelandGame.Instance;
            bool byPlayer = g && source && (source.transform.IsChildOf(g.Player.transform) || (g.Current && source.transform.IsChildOf(g.Current.transform)));
            if (health <= 0f) { Die(byPlayer); return; }
            if (!byPlayer) return;
            State.disposition = Mathf.Max(-100, State.disposition - 30);
            convoy?.Provoked();
            // armed or proud folk fight back, the rest run
            if (Profile.Raider || tool && !(tool is LightTool) || Profile.temper == Temper.Proud || Profile.temper == Temper.Gruff) State.Set(NpcSave.Hostile);
            else { mode = Mode.Flee; fleeUntil = Time.time + 10f; }
            Alarm(g);
        }

        /// <summary>Peaceful folk around see the attack: they turn on the player or run.</summary>
        void Alarm(WastelandGame g)
        {
            if (Profile.Raider) return;
            foreach (var n in All)
            {
                if (n == this || !n.Alive || n.Profile.Raider || (n.transform.position - transform.position).sqrMagnitude > 25f * 25f) continue;
                n.State.disposition = Mathf.Max(-100, n.State.disposition - 15);
                if (n.tool && !(n.tool is LightTool) && n.Profile.temper != Temper.Nervous) n.State.Set(NpcSave.Hostile);
                else { n.mode = Mode.Flee; n.fleeUntil = Time.time + 8f; }
            }
        }

        public void Scare(float seconds) { if (mode == Mode.Dead || Hostile) return; mode = Mode.Flee; fleeUntil = Time.time + seconds; }

        void Die(bool byPlayer)
        {
            mode = Mode.Dead;
            State.dead = true;
            if (byPlayer && !Profile.Raider && !State.Has(NpcSave.Hostile)) NpcRegistry.Reputation = Mathf.Max(-100, NpcRegistry.Reputation - 15);
            if (byPlayer && Profile.Raider) NpcRegistry.Reputation = Mathf.Min(100, NpcRegistry.Reputation + 3);
            cc.enabled = false;
            if (tool) { tool.transform.SetParent(null, true); Destroy(tool.gameObject, 60f); }
            // lie down: tip over backwards, settle on the ground
            var p = transform.position;
            var terrain = DeformableTerrain.Instance;
            if (terrain) p.y = terrain.Height(p.x, p.z) + 0.12f;
            transform.SetPositionAndRotation(p, transform.rotation * Quaternion.Euler(-90f, 0f, 0f));
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0f, 0.85f); box.size = new Vector3(0.6f, 0.4f, 1.8f); box.isTrigger = true;
            var loot = gameObject.AddComponent<Lootable>();
            loot.key = "N" + Profile.id; loot.table = Profile.Raider ? "raider" : Profile.Vendor ? "shop" : "house";
            loot.title = Profile.Name + "'S BODY";
            MadMax.Audio.Sfx.Play("bone", transform.position, 0.8f);
            convoy?.MemberDied(this);
        }

        // ------------------------------------------------------------------ talk / trade

        public string Prompt(WastelandGame g)
        {
            if (mode == Mode.Dead) return null;
            if (Hostile && !Profile.Raider) return Profile.Name + " WANTS YOU DEAD";
            string s = "[E] " + (Profile.Raider ? "PARLEY WITH " : "TALK TO ") + Profile.Name;
            if (Profile.Vendor && !Hostile && State.disposition > -40) s += "  [T] TRADE";
            return s;
        }

        public void Use(WastelandGame g, bool secondary)
        {
            if (mode == Mode.Dead) return;
            if (secondary) { if (Profile.Vendor && !Hostile) g.Menus.OpenTalk(this, true); return; }
            g.Menus.OpenTalk(this, false);
        }

        /// <summary>Look at the speaker while a conversation is open.</summary>
        public void Attend(Vector3 at) { faceTarget = at; faceUntil = Time.time + 1f; if (mode == Mode.Wander) hasGoal = false; }

        public void Heal() => health = maxHealth;
    }
}
