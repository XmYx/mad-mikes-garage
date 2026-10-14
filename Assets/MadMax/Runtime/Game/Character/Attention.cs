using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Where a character looks (one per <see cref="HumanAnimator"/>): a prioritised look target fed to the
    /// animator's look-at (eyes first, then head, neck and a little chest) with hold times, re-glance cooldowns and
    /// limits. Sources by priority: the player's own aim; danger (hostiles fighting, charging animals, a car coming
    /// straight at you, fire close by, gunshots, explosions); sounds; points of interest (what [E] would use, passing
    /// vehicles, animals, loose items, the waypoint now and then); idle glances (in a vehicle: the mirrors and into the
    /// turn). Events reach every listener through a cheap static bus (<see cref="Ping"/>: Sfx one-shots of loud keys,
    /// NPC / animal noise and horns, blasts, impacts, fires). Beyond <see cref="GiveUpYaw"/> the head gives up — or, for
    /// danger while the player stands idle, asks the body to turn (<see cref="TurnYaw"/>). Close bangs ask for a
    /// reaction clip (startle, cover_head). Nothing runs for NPCs beyond <see cref="Reach"/> of the focus, in first
    /// person (the camera rides the head) or while lying / prone.</summary>
    public sealed class Attention
    {
        public enum Kind : byte { Glance, Poi, Sound, Vehicle, Animal, Fire, Gunshot, Hostile, Explosion, Aim }

        // ---------------------------------------------------------------- tunables (all in one place)
        /// <summary>Beyond this yaw off the body's forward the head gives up (no owl turns).</summary>
        public const float GiveUpYaw = 100f;
        /// <summary>NPCs further than this from the focus don't look around.</summary>
        public const float Reach = 35f;
        /// <summary>Pings this close to the listener are its own noise (steps, own gun) and ignored, blasts excepted.</summary>
        public const float SelfRadius = 1.6f;
        public const float ScanEvery = 0.25f;                       // player: danger / POI scan
        public const float HostileRange = 35f, FireRange = 12f, AnimalRange = 25f, PetRange = 10f, VehicleRange = 30f, ItemRange = 3.5f;
        public const float ReactCooldown = 3f;
        public const float StartleNear = 7f;                        // a loud sound this close makes you jump
        public const float CoverHeadNear = 30f;                     // a blast this close: arms over the head
        public const float GunStartle = 22f;                        // a shot this close: a start
        public const float ShiftBlink = 30f;                        // a gaze shift bigger than this blinks
        public const float GlanceMin = 3f, GlanceMax = 9f;          // idle glances: every 3-9 s
        public const float WaypointMin = 20f, WaypointMax = 40f;    // a look along the waypoint now and then while walking
        const float HomeScore = 0.6f;

        static float Priority(Kind k)
        {
            switch (k)
            {
                case Kind.Aim: return 99f;
                case Kind.Explosion: return 6f;
                case Kind.Hostile: return 5f;
                case Kind.Gunshot: return 4.5f;
                case Kind.Fire: return 3.2f;
                case Kind.Vehicle: return 2.4f;
                case Kind.Animal: return 1.8f;
                case Kind.Sound: return 2.2f;
                case Kind.Poi: return 1.2f;
                default: return 0.5f;
            }
        }

        static float HoldFor(Kind k)
        {
            switch (k)
            {
                case Kind.Explosion: return 3f;
                case Kind.Hostile: return 1.6f;          // refreshed by every scan while it lasts
                case Kind.Gunshot: return 2.2f;
                case Kind.Fire: return 1.8f;
                case Kind.Vehicle: return 1.6f;
                case Kind.Sound: return 1.4f;
                case Kind.Animal: return 1.8f;
                case Kind.Poi: return 1.5f;
                case Kind.Aim: return 0.2f;
                default: return 1.2f;
            }
        }

        // ---------------------------------------------------------------- the bus
        struct Rec { public Vector3 pos; public float range, time; public Kind kind; }
        const int Ring = 32;
        static readonly Rec[] ring = new Rec[Ring];
        static int serial;
        /// <summary>Pings so far (tests) and the last one.</summary>
        public static int PingCount => serial;
        public static Vector3 LastPingAt { get; private set; }
        public static Kind LastPingKind { get; private set; }
        /// <summary>Tests / dev: the whole system off (characters look only where code tells them).</summary>
        public static bool Off;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { serial = 0; for (int i = 0; i < Ring; i++) ring[i] = default; LastPingAt = default; LastPingKind = Kind.Glance; Off = false; }

        /// <summary>Something happened at <paramref name="pos"/> that anyone within <paramref name="loudness"/> metres
        /// notices (a sound's reach, a blast's radius). The same event reported twice (the bang and its noise) merges.</summary>
        public static void Ping(Vector3 pos, Kind kind, float loudness)
        {
            if (loudness < 1f) return;
            float now = Time.time;
            for (int i = 1; i <= 4 && i <= serial; i++)
            {
                int k = (serial - i) % Ring;
                if (now - ring[k].time < 0.3f && (ring[k].pos - pos).sqrMagnitude < 9f)
                {
                    if (kind > ring[k].kind) ring[k].kind = kind;
                    ring[k].range = Mathf.Max(ring[k].range, loudness);
                    return;
                }
            }
            ring[serial % Ring] = new Rec { pos = pos, range = loudness, time = now, kind = kind };
            serial++;
            LastPingAt = pos; LastPingKind = kind;
        }

        // ---------------------------------------------------------------- one character
        readonly HumanRig rig;
        readonly System.Random rnd;
        int seen = -1, player = -1;
        bool has, following;
        Vector3 target, followOffset, lastTarget;
        Transform follow;
        Kind kind;
        float score, until, nextScan, nextGlance, nextWaypoint, reactReady;
        string reaction;
        Vector3 reactFrom;
        float reactAt;
        readonly int[] recent = new int[8];
        readonly float[] recentUntil = new float[8];
        int recentNext;

        /// <summary>The owner hides it this tick (first person: the camera rides the head).</summary>
        public bool Suppressed;
        /// <summary>World point to look at (null = none: the animator's other sources / straight ahead).</summary>
        public Vector3? Target => has ? target : (Vector3?)null;
        public Kind TargetKind => kind;
        /// <summary>The gaze jumped this tick (the face blinks with a big one).</summary>
        public bool Shifted { get; private set; }
        /// <summary>A danger behind: the body should turn to this world yaw (the player turns in place when idle).</summary>
        public float? TurnYaw { get; private set; }
        /// <summary>Last target change (tests).</summary>
        public float ChangedAt { get; private set; } = -9f;

        public Attention(HumanRig rig)
        {
            this.rig = rig;
            rnd = new System.Random(rig ? rig.GetEntityId().GetHashCode() * 31 + 7 : 7);
            nextGlance = Time.time + 2f + (float)rnd.NextDouble() * 4f;
            nextWaypoint = Time.time + WaypointMin;
        }

        /// <summary>A reaction clip asked for by a close bang ("startle", "cover_head"), once; null = none.</summary>
        public string TakeReaction(out Vector3 from)
        {
            from = reactFrom;
            var r = Time.time - reactAt < 0.5f ? reaction : null;                               // a start is now or never
            reaction = null;
            return r;
        }

        bool IsPlayer(WastelandGame g)
        {
            if (player < 0 && rig) player = rig.GetComponent<PlayerCharacter>() ? 1 : 0;
            return player == 1 && g && g.Player && g.Player.Rig == rig;
        }

        void Clear() { has = false; following = false; follow = null; score = 0f; TurnYaw = null; }

        /// <summary>One tick (from HumanAnimator.Tick). <paramref name="home"/> = the player face's auto look (what [E]
        /// would use, the camera's aim while standing) as a low-priority point of interest.</summary>
        public void Tick(float dt, HumanAnimator.State s, Vector3? home)
        {
            Shifted = false; TurnYaw = null;
            if (!rig) return;
            var g = WastelandGame.Instance;
            float now = Time.time;
            if (seen < 0) seen = serial;
            if (Off || Suppressed || s.lying || s.prone || rig.Limp) { Clear(); seen = serial; return; }
            bool me = IsPlayer(g);
            var root = rig.transform;
            if (!me && g && (root.position - g.FocusPos).sqrMagnitude > Reach * Reach) { Clear(); seen = serial; return; }
            var eye = rig.Eye ? rig.Eye.position : root.position + Vector3.up * 1.6f;

            if (has && following) { if (follow) target = follow.position + followOffset; else Clear(); }
            if (has && now > until) Clear();

            // the player's own aim beats everything
            if (me && g.Aiming) { Look(g.AimPoint, Kind.Aim, 99f, null, Vector3.zero, eye, now); Limits(root, s, me, now); return; }
            if (has && kind == Kind.Aim) Clear();

            Hear(root.position, now, eye, me);
            if (me && now >= nextScan) { nextScan = now + ScanEvery; Scan(g, root, eye, s, now); }
            if (me && home.HasValue)
            {
                // what [E] would use (close): a point of interest; the camera's aim while standing: where the eyes rest
                bool nearHome = (home.Value - eye).sqrMagnitude < 36f;
                if (nearHome ? !has || score < Priority(Kind.Poi) : !has || (kind == Kind.Glance && score >= HomeScore))
                    Look(home.Value, nearHome ? Kind.Poi : Kind.Glance, nearHome ? Priority(Kind.Poi) : HomeScore, null, Vector3.zero, eye, now, 0.3f);
            }
            if (now >= nextGlance && (!has || (kind == Kind.Glance && score >= HomeScore))) Glance(g, root, eye, s, me, now);
            Limits(root, s, me, now);
        }

        // ---------------------------------------------------------------- sources
        void Hear(Vector3 at, float now, Vector3 eye, bool me)
        {
            if (serial - seen > Ring) seen = serial - Ring;
            for (; seen < serial; seen++)
            {
                var r = ring[seen % Ring];
                float d = Vector3.Distance(r.pos, at);
                if (d > r.range) continue;
                if (d < SelfRadius && r.kind != Kind.Explosion) continue;                       // our own noise
                float sc = Priority(r.kind) * (0.55f + 0.45f * (1f - d / r.range));
                if (has && sc <= Current(now)) { MaybeReact(r, d, now); continue; }
                var p = r.pos;
                if (p.y < at.y + 0.6f) p.y = at.y + 0.6f;                                       // at the ground: look at the spot, not below it
                Look(p, r.kind, sc, null, Vector3.zero, eye, now);
                MaybeReact(r, d, now);
            }
        }

        void MaybeReact(Rec r, float d, float now)
        {
            if (now < reactReady) return;
            string want = null;
            if (r.kind == Kind.Explosion) want = d < CoverHeadNear ? "cover_head" : d < CoverHeadNear * 2.5f ? "startle" : null;
            else if (r.kind == Kind.Gunshot) want = d < GunStartle ? "startle" : null;
            else if ((r.kind == Kind.Sound || r.kind == Kind.Vehicle) && r.range >= 40f && d < StartleNear) want = "startle";
            if (want == null) return;
            reaction = want; reactFrom = r.pos; reactAt = now; reactReady = now + ReactCooldown;
        }

        /// <summary>The player looks around for danger and things of interest (4 Hz).</summary>
        void Scan(WastelandGame g, Transform root, Vector3 eye, HumanAnimator.State s, float now)
        {
            var at = root.position;
            float cur = Current(now);
            // hostiles in a fight
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || !n.Hostile || n.mode != MadMax.Npc.Npc.Mode.Fight) continue;
                float d = Vector3.Distance(n.transform.position, at);
                if (d > HostileRange) continue;
                float sc = Priority(Kind.Hostile) * (1f - 0.4f * d / HostileRange);
                if (sc > cur) { Look(n.transform.position + Vector3.up * 1.5f, Kind.Hostile, sc, n.transform, Vector3.up * 1.5f, eye, now); cur = sc; }
            }
            // fire close by
            foreach (var f in MadMax.World.Fire.All)
            {
                if (!f) continue;
                float d = Vector3.Distance(f.transform.position, at);
                if (d > FireRange) continue;
                float sc = Priority(Kind.Fire) * (1f - 0.4f * d / FireRange);
                if (sc > cur && Fresh(f.GetEntityId().GetHashCode(), now)) { Look(f.transform.position + Vector3.up * 0.4f, Kind.Fire, sc, f.transform, Vector3.up * 0.4f, eye, now); Remember(f.GetEntityId().GetHashCode(), now, 6f); cur = sc; }
            }
            // animals: a charge is danger, a beast close by is worth a look
            foreach (var a in MadMax.Animals.Animal.All)
            {
                if (!a || a.state == MadMax.Animals.Animal.State.Dead) continue;
                float d = Vector3.Distance(a.transform.position, at);
                if (d > AnimalRange) continue;
                bool danger = a.state == MadMax.Animals.Animal.State.Charge || a.state == MadMax.Animals.Animal.State.Attack || a.state == MadMax.Animals.Animal.State.Stalk || a.state == MadMax.Animals.Animal.State.Circle;
                if (!danger && (d > PetRange || a.state == MadMax.Animals.Animal.State.Ridden)) continue;
                var k = danger ? Kind.Hostile : Kind.Animal;
                float sc = Priority(k) * (1f - 0.4f * d / AnimalRange);
                int id = a.GetEntityId().GetHashCode();
                if (sc > cur && (danger || Fresh(id, now))) { Look(a.transform.position + Vector3.up * 0.6f, k, sc, a.transform, Vector3.up * 0.6f, eye, now); if (!danger) Remember(id, now, 12f); cur = sc; }
            }
            // vehicles going by; one coming straight at you is danger
            var mine = g.Current;
            foreach (var vs in VehicleSystems.All)
            {
                if (!vs || !vs.TryGetComponent<VehicleDriver>(out var v) || v == mine || !v.Body) continue;
                var to = at - v.transform.position;
                float d = to.magnitude;
                if (d > VehicleRange) continue;
                var vel = v.Body.linearVelocity; vel.y = 0f;
                float sp = vel.magnitude;
                if (sp < 4f) continue;
                bool coming = Vector3.Dot(vel, to) > 0.85f * sp * d && sp > 7f;
                float sc = (coming ? Priority(Kind.Hostile) * 0.8f : Priority(Kind.Vehicle)) * (1f - 0.4f * d / VehicleRange);
                int id = v.GetEntityId().GetHashCode();
                if (sc > cur && (coming || Fresh(id, now))) { Look(v.transform.position + Vector3.up * 1f, Kind.Vehicle, sc, v.transform, Vector3.up, eye, now); if (!coming) Remember(id, now, 10f); cur = sc; }
            }
            if (has && cur >= Priority(Kind.Poi)) return;
            // loose things at your feet
            if (s.speed < 2.5f && !s.sitting)
            {
                MadMax.Items.WorldItem best = null; float bd = ItemRange;
                foreach (var w in MadMax.Items.WorldItem.All)
                {
                    if (!w) continue;
                    float d = Vector3.Distance(w.transform.position, at);
                    if (d < bd && Fresh(w.GetEntityId().GetHashCode(), now)) { bd = d; best = w; }
                }
                if (best) { Look(best.transform.position, Kind.Poi, Priority(Kind.Poi) * 0.9f, best.transform, Vector3.zero, eye, now, 1.1f); Remember(best.GetEntityId().GetHashCode(), now, 30f); return; }
            }
            // the way to go, now and then while walking
            if (g.HasWaypoint && s.speed > 0.8f && now >= nextWaypoint && !s.sitting)
            {
                nextWaypoint = now + WaypointMin + (float)rnd.NextDouble() * (WaypointMax - WaypointMin);
                var dir = g.Waypoint - at; dir.y = 0f;
                if (dir.sqrMagnitude > 400f) Look(eye + dir.normalized * 20f, Kind.Poi, Priority(Kind.Poi), null, Vector3.zero, eye, now, 1.2f);
            }
        }

        /// <summary>Nothing to look at: a glance around now and then. Driving: the mirrors, or into the turn.</summary>
        void Glance(WastelandGame g, Transform root, Vector3 eye, HumanAnimator.State s, bool me, float now)
        {
            nextGlance = now + GlanceMin + (float)rnd.NextDouble() * (GlanceMax - GlanceMin);
            float hold = 0.7f + (float)rnd.NextDouble() * 1.1f;
            Vector3 p;
            if (s.sitting && !s.lounging && !s.riding)
            {
                var fwd = root.forward; var right = root.right;
                if (Mathf.Abs(s.steer) > 0.3f) p = eye + (fwd + right * s.steer * 0.9f) * 15f;                 // into the turn
                else
                {
                    double r = rnd.NextDouble();
                    float side = r < 0.4 ? -1f : r < 0.75 ? 1f : 0f;                                         // left mirror, right mirror, the inside one
                    float inner = root.parent ? -Mathf.Sign(root.localPosition.x + 1e-4f) * 0.35f : 0f;       // the cabin's middle
                    p = side != 0f ? eye + fwd * 0.75f + right * side * 0.95f - Vector3.up * 0.12f : eye + fwd * 0.5f + Vector3.up * 0.15f + right * inner;
                    hold = 0.45f + (float)rnd.NextDouble() * 0.5f;                                           // mirrors are quick
                }
            }
            else
            {
                float yaw = ((float)rnd.NextDouble() * 2f - 1f) * 75f, pitch = -12f + (float)rnd.NextDouble() * 18f;
                var dir = root.rotation * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                p = eye + dir * (6f + (float)rnd.NextDouble() * 6f);
            }
            Look(p, Kind.Glance, Priority(Kind.Glance), null, Vector3.zero, eye, now, hold);
        }

        // ---------------------------------------------------------------- helpers
        float Current(float now)
        {
            if (!has) return 0f;
            float left = until - now, hold = HoldFor(kind);
            return left < hold * 0.4f ? score * Mathf.Clamp01(left / (hold * 0.4f)) : score;   // a fading look gives way
        }

        void Look(Vector3 p, Kind k, float sc, Transform f, Vector3 off, Vector3 eye, float now, float hold = -1f)
        {
            bool same = has && k == kind && (f ? f == follow : (p - target).sqrMagnitude < 0.25f);
            if (has && (p - eye).sqrMagnitude > 0.01f && (target - eye).sqrMagnitude > 0.01f && Vector3.Angle(p - eye, target - eye) > ShiftBlink) Shifted = true;
            else if (!has && (lastTarget - eye).sqrMagnitude > 0.01f && Vector3.Angle(p - eye, lastTarget - eye) > ShiftBlink * 1.5f) Shifted = true;
            if (!same) ChangedAt = now;
            has = true; kind = k; score = sc; target = p; lastTarget = p;
            follow = f; following = f; followOffset = off;
            until = now + (hold > 0f ? hold : HoldFor(k));
        }

        /// <summary>No owl turns: beyond <see cref="GiveUpYaw"/> the look is dropped, or for danger while the player
        /// stands idle the body is asked to turn.</summary>
        void Limits(Transform root, HumanAnimator.State s, bool me, float now)
        {
            if (!has) return;
            var lp = root.InverseTransformPoint(target);
            float yaw = Mathf.Atan2(lp.x, lp.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(yaw) <= GiveUpYaw) return;
            if (me && kind >= Kind.Fire && kind != Kind.Aim && s.speed < 0.3f && !s.sitting && !s.aiming && !s.crouching)
            {
                var d = target - root.position;
                TurnYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                return;
            }
            if (kind == Kind.Aim) return;
            Clear();
            nextGlance = Mathf.Max(nextGlance, now + 1f);
        }

        bool Fresh(int id, float now)
        {
            for (int i = 0; i < recent.Length; i++) if (recent[i] == id && now < recentUntil[i]) return false;
            return true;
        }

        void Remember(int id, float now, float seconds)
        {
            recent[recentNext] = id; recentUntil[recentNext] = now + seconds;
            recentNext = (recentNext + 1) % recent.Length;
        }

        // ---------------------------------------------------------------- what Sfx keys mean
        static readonly System.Collections.Generic.Dictionary<string, (Kind kind, float range)> keyKinds = new System.Collections.Generic.Dictionary<string, (Kind, float)>();

        /// <summary>From Sfx.Play: one-shots of loud keys ping the bus (cached per key; quiet keys cost a lookup).</summary>
        public static void FromSound(string key, Vector3 pos, float volume)
        {
            if (Off || key == null) return;
            if (!keyKinds.TryGetValue(key, out var kr)) { kr = Classify(key); keyKinds[key] = kr; }
            if (kr.range <= 0f) return;
            Ping(pos, kr.kind, kr.range * Mathf.Clamp(volume, 0.35f, 1f));
        }

        static (Kind, float) Classify(string k)
        {
            if (k == "explosion") return (Kind.Explosion, 220f);
            if (k == "shotgun" || k.StartsWith("shot_")) return (Kind.Gunshot, 150f);
            if (k == "horn" || k == "siren") return (Kind.Vehicle, 70f);
            if (k == "crash_big" || k == "collapse") return (Kind.Sound, 60f);
            if (k == "tyre_skid") return (Kind.Vehicle, 40f);
            if (k == "crash_small" || k == "glass_break" || k == "bell" || k == "chainsaw" || k == "crowd_cheer" || k == "crowd_aww") return (Kind.Sound, 35f);
            if (k == "pop" || k == "anvil" || k == "hammer" || k == "punch" || k == "hit_wood" || k == "splash" || k == "bone") return (Kind.Sound, 16f);
            if (k == "door_open" || k == "door_close" || k == "car_door" || k == "lock" || k == "chain" || k == "creak" || k == "bow") return (Kind.Sound, 12f);
            return (Kind.Glance, 0f);
        }
    }
}
