using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Parkour (roadmap 22). Space at an obstacle vaults it (0.45–1.2 m, or a wall too thin to stand on: over
    /// and down the far side) or mantles a ledge up to 2.3 m (crates, cars, walls, roofs) when there is room on top.
    /// Ctrl crouches (slower, lower, harder to spot); crouching at a run slides. Holding Ctrl as you land a hard fall
    /// rolls out of it. A running jump carries further. Standing on a vehicle bed or roof rides along; at speed you brace
    /// (a standing perch). The grappling hook reels you to where it bites. All of it practises Athletics, which quickens
    /// climbs, lengthens slides and softens falls.</summary>
    public partial class PlayerCharacter
    {
        [HideInInspector] public bool crouch;                  // input: held
        public bool Crouching { get; private set; }
        public bool Sliding => slideT > 0f;
        public bool Traversing => moveT >= 0f;

        float slideT, moveT = -1f, moveDur, sprintJump, slideSpeed, rideFor, climbTop;
        Vector3 moveFrom, moveMid, moveTo, slideDir, zipDir, carry;
        int moveKind;                                          // 0 vault / over, 1 climb up, 2 zip
        bool crouchWas;
        MadMax.Building.Seat ridePerch;
        static readonly RaycastHit[] probeHits = new RaycastHit[12];
        static readonly Collider[] roomHits = new Collider[12];

        const float StandHeight = 1.8f, CrouchHeight = 1.3f, SlideHeight = 1.0f;

        int Athletics { get { var g = WastelandGame.Instance; return g && g.Stats != null ? g.Stats.Level(Skill.Athletics) : 0; } }

        // ------------------------------------------------------------------ probes

        /// <summary>The nearest hit along a ray that is not ourselves.</summary>
        bool Probe(Vector3 from, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(from, dir, probeHits, dist, ~0, QueryTriggerInteraction.Ignore);
            float bd = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (probeHits[i].distance >= bd || probeHits[i].collider.transform.IsChildOf(transform)) continue;
                bd = probeHits[i].distance; best = probeHits[i];
            }
            return bd < float.MaxValue;
        }

        /// <summary>Room for a standing body with its feet at <paramref name="feet"/>, checked from <paramref name="low"/> m up.</summary>
        bool Room(Vector3 feet, float low = 0.5f)
        {
            int n = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * (low + 0.26f), feet + Vector3.up * (StandHeight - 0.28f), 0.26f, roomHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!roomHits[i].transform.IsChildOf(transform)) return false;
            return true;
        }

        static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float t) => Vector3.Lerp(Vector3.Lerp(a, c, t), Vector3.Lerp(c, b, t), t);
        /// <summary>Control point so the curve a→b passes through <paramref name="apex"/> halfway.</summary>
        static Vector3 Through(Vector3 a, Vector3 apex, Vector3 b) => apex * 2f - (a + b) * 0.5f;

        // ------------------------------------------------------------------ vault / climb

        /// <summary>Space in front of an obstacle: vault it or climb onto it. False = nothing to get over (jump instead).</summary>
        bool TryTraverse(Vector3 dir)
        {
            if (Carried || Crouching || Sliding || Swimming || !cc.enabled) return false;
            var fwd = dir.sqrMagnitude > 0.04f ? dir : transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) return false;
            fwd.Normalize();
            var feet = transform.position;
            // a face in front, steep enough to be a wall (a slope is jumped, not climbed); never a person
            if (!Probe(feet + Vector3.up * 0.5f, fwd, 0.85f, out var face) && !Probe(feet + Vector3.up * 1.35f, fwd, 0.85f, out face)) return false;
            if (face.normal.y > 0.5f || face.collider.GetComponentInParent<MadMax.Npc.Npc>()) return false;
            var edge = face.point + fwd * 0.08f;
            if (!Probe(new Vector3(edge.x, feet.y + 2.45f, edge.z), Vector3.down, 2.45f, out var top)) return false;
            float h = top.point.y - feet.y;
            if (h < 0.45f || h > 2.3f || top.normal.y < 0.75f) return false;
            var game = WastelandGame.Instance;
            var vitals = game ? game.Vitals : null;
            if (vitals && vitals.Exhausted) { game.Toast("TOO WINDED TO CLIMB"); return false; }
            bool standable = Probe(top.point + fwd * 0.4f + Vector3.up * 0.6f, Vector3.down, 0.9f, out var onTop) && Mathf.Abs(onTop.point.y - top.point.y) < 0.3f && onTop.normal.y > 0.75f;
            // over: a low obstacle, or a wall too thin to stand on, with ground beyond (never a long drop)
            if (h <= 1.2f || !standable)
            {
                for (int i = 0; i < 2; i++)
                {
                    var beyond = face.point + fwd * (i == 0 ? 0.8f : 1.3f);
                    float from = top.point.y + 0.3f;
                    if (!Probe(new Vector3(beyond.x, from, beyond.z), Vector3.down, from - (feet.y - 1.5f), out var ground)) continue;
                    if (ground.point.y > top.point.y - 0.3f || ground.normal.y < 0.6f || !Room(ground.point)) continue;
                    var to = ground.point + Vector3.up * 0.02f;
                    var apex = new Vector3(edge.x, top.point.y + (h <= 1.2f ? 0.12f : 0.3f), edge.z);
                    StartMove(0, feet, Through(feet, apex, to), to, h <= 1.2f ? 0.5f : 0.7f + h * 0.2f, h <= 1.2f ? 5f : 10f);
                    return true;
                }
            }
            if (!standable || !Room(onTop.point)) return false;
            var gl = WastelandGame.Instance;
            if (gl && !gl.CanClimb) { gl.Toast("CAN'T PULL UP WITHOUT TWO GRIPPING ARMS"); return false; }   // a lost hand with no climbing prosthetic
            // mantle: hands on the edge, pull up level with it, press over onto the top
            climbTop = top.point.y;
            var hang = face.point - fwd * 0.32f;
            StartMove(1, feet, new Vector3(hang.x, top.point.y - 0.62f, hang.z), onTop.point + Vector3.up * 0.02f, 0.5f + h * 0.28f, 6f + h * 4f);
            return true;
        }

        void StartMove(int kind, Vector3 from, Vector3 mid, Vector3 to, float dur, float stamina)
        {
            moveKind = kind; moveFrom = from; moveMid = mid; moveTo = to; moveT = 0f;
            moveDur = dur * Mathf.Max(0.6f, 1f - Athletics * 0.03f);
            cc.enabled = false; vy = 0f; airborne = false; fallSpeed = 0f; slideT = 0f; sprintJump = 0f;
            if (Crouching) SetCrouch(false);
            var game = WastelandGame.Instance;
            if (game)
            {
                game.Stats?.Practice(Skill.Athletics, kind == 1 ? 3f : 2f);
                game.Vitals?.Spend(stamina);
            }
            MadMax.Audio.Sfx.Play(kind == 2 ? "chain" : "punch", from + Vector3.up, 0.25f, kind == 2 ? 1.2f : 1.5f);
        }

        void TraverseTick(float dt)
        {
            moveT += dt / Mathf.Max(0.05f, moveDur);
            float t = Mathf.Clamp01(moveT);
            Vector3 p;
            ToolPose pose;
            if (moveKind == 1)
            {
                // reach (hop if the edge is high), pull up to the edge, press over onto the top
                var reach = new Vector3(moveFrom.x, Mathf.Max(moveFrom.y, climbTop - 2.05f), moveFrom.z);
                if (t < 0.2f) p = Vector3.Lerp(moveFrom, reach, Mathf.SmoothStep(0f, 1f, t / 0.2f));
                else if (t < 0.62f) p = Vector3.Lerp(reach, moveMid, Mathf.SmoothStep(0f, 1f, (t - 0.2f) / 0.42f));
                else
                {
                    float u = (t - 0.62f) / 0.38f;
                    p = Bezier(moveMid, new Vector3(moveMid.x, climbTop + 0.35f, moveMid.z), moveTo, u);
                }
                float pull = Mathf.InverseLerp(0.2f, 0.62f, t), press = Mathf.InverseLerp(0.62f, 1f, t);
                pose = new ToolPose
                {
                    chestX = Mathf.Lerp(4f, 26f, pull) - press * 18f,
                    armRX = Mathf.Lerp(-172f, 12f, pull) - press * 40f, armLX = Mathf.Lerp(-168f, 12f, pull) - press * 40f,
                    foreR = -20f - Mathf.Sin(pull * Mathf.PI) * 70f, foreL = -20f - Mathf.Sin(pull * Mathf.PI) * 70f,
                    armRZ = 8f, armLZ = -8f,
                    knees = 10f + Mathf.Sin(press * Mathf.PI) * 85f + pull * 20f * (1f - press)
                };
            }
            else if (moveKind == 0)
            {
                p = Bezier(moveFrom, moveMid, moveTo, Mathf.SmoothStep(0f, 1f, t));
                float tuck = Mathf.Sin(t * Mathf.PI);
                pose = new ToolPose { chestX = 10f + tuck * 18f, armRX = -30f - tuck * 30f, armLX = -30f - tuck * 30f, armRZ = 20f, armLZ = -20f, foreR = -10f, foreL = -10f, knees = 5f + tuck * 70f };
            }
            else
            {
                p = Bezier(moveFrom, moveMid, moveTo, t);
                pose = new ToolPose { armRX = -174f, foreR = -10f, armLX = -160f, armLZ = -8f, foreL = -35f, knees = 25f, chestX = -6f };
            }
            var face = moveKind == 2 ? zipDir : moveTo - moveFrom;
            face.y = 0f;
            transform.SetPositionAndRotation(p, face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face) : transform.rotation);
            Velocity = Vector3.zero;
            anim.Tick(dt, new HumanAnimator.State { grounded = false, lookPitch = lookPitch, tool = pose,
                action = moveKind == 1 ? "mantle" : moveKind == 0 ? "vault" : null, actionT = t });
            if (moveT < 1f) return;
            moveT = -1f;
            cc.enabled = true; vy = -1f;
            if (moveKind == 2 && !TryTraverse(zipDir)) { vy = 0f; airborne = true; }                 // at the hook: over the edge, else let go
        }

        // ------------------------------------------------------------------ grappling hook

        /// <summary>Reel in to where the hook bit. On a top surface you land on it; on a wall you end hanging below
        /// the hook and climb over if there is an edge within reach, else drop.</summary>
        public bool Zip(Vector3 hook, Vector3 normal)
        {
            if (Traversing || Carried || Sitting || SeatedIn || Interior || !cc.enabled || normal.y < -0.5f) return false;
            Vector3 to;
            if (normal.y > 0.7f) { to = hook + Vector3.up * 0.03f; if (!Room(to)) return false; }
            else
            {
                var n = new Vector3(normal.x, 0f, normal.z).normalized;
                to = hook + n * 0.36f + Vector3.down * 1.75f;
                var terrain = DeformableTerrain.Instance;
                if (terrain) to.y = Mathf.Max(to.y, terrain.Height(to.x, to.z) + 0.03f);
                zipDir = -n;
            }
            if (normal.y > 0.7f) { zipDir = to - transform.position; zipDir.y = 0f; }
            float dist = Vector3.Distance(transform.position, to);
            var apex = Vector3.Lerp(transform.position, to, 0.5f) + Vector3.up * Mathf.Min(1.5f, dist * 0.06f);
            StartMove(2, transform.position, Through(transform.position, apex, to), to, Mathf.Clamp(dist / 13f, 0.35f, 1.8f), 4f + dist * 0.3f);
            return true;
        }

        /// <summary>The hook's rope runs from the right hand while reeling in.</summary>
        public bool Zipping => Traversing && moveKind == 2;

        // ------------------------------------------------------------------ crouch / slide

        float CrouchSpeed(CharacterStats stats, bool canRun)
        {
            float quick = 1f + (stats != null ? stats.Level(Skill.Athletics) : 0) * (canRun ? 0.02f : 0.01f);   // quicker feet with practice
            return Crouching ? 0.45f * quick : quick;
        }

        void CrouchTick(float dt, bool canRun)
        {
            bool pressed = crouch && !crouchWas;
            crouchWas = crouch;
            if (slideT > 0f)
            {
                slideT -= dt;
                if (slideT > 0f) return;
                slideT = 0f;
                SetCrouch(crouch || !Room(transform.position, CrouchHeight - 0.2f));                // came to rest under something: stay low
                return;
            }
            if (pressed && canRun && cc.isGrounded && !Carried && new Vector2(Velocity.x - carry.x, Velocity.z - carry.z).magnitude > 3.8f)
            {
                var v = new Vector3(Velocity.x - carry.x, 0f, Velocity.z - carry.z);
                StartSlide(v.normalized, v.magnitude * 1.2f, 0.85f + Athletics * 0.03f);
                WastelandGame.Instance?.Stats?.Practice(Skill.Athletics, 1f);
                WastelandGame.Instance?.Vitals?.Spend(6f);
                MadMax.Audio.Sfx.Play("dig", transform.position, 0.3f, 1.5f);
                return;
            }
            if (crouch) { if (!Crouching) SetCrouch(true); }
            else if (Crouching && Room(transform.position, CrouchHeight - 0.2f)) SetCrouch(false);
        }

        void StartSlide(Vector3 dir, float speed, float seconds)
        {
            slideDir = dir; slideSpeed = speed; slideT = seconds;
            Crouching = true;
            SetHeight(SlideHeight);
        }

        Vector3 SlideVelocity(float dt)
        {
            slideSpeed = Mathf.MoveTowards(slideSpeed, 1.5f, dt * 5.5f);
            if (DebrisSystem.Instance && Random.value < dt * 18f)
                DebrisSystem.Instance.EmitPuff(transform.position + Vector3.up * 0.05f, new Color32(190, 130, 80, 255), 0.05f, Vector3.up * 0.8f - slideDir, 0.4f);
            return slideDir * slideSpeed;
        }

        void SetCrouch(bool on)
        {
            Crouching = on;
            SetHeight(on ? CrouchHeight : StandHeight);
        }

        void SetHeight(float h)
        {
            cc.height = h;
            cc.center = new Vector3(0f, h * 0.5f, 0f);
        }

        /// <summary>Knees bent (the animator drops the pelvis to keep the feet down); a slide leans back, arms out.</summary>
        ToolPose CrouchPose(ToolPose? held)
        {
            var p = held ?? default;
            if (Sliding)
            {
                p.knees = 118f; p.chestX = -32f;
                if (!held.HasValue) { p.armRX = -55f; p.armLX = -50f; p.armRZ = 35f; p.armLZ = -35f; p.foreR = p.foreL = -15f; }
            }
            else { p.knees = 100f; p.chestX = Mathf.Max(p.chestX, 18f); }
            return p;
        }

        // ------------------------------------------------------------------ landings

        void Land(PlayerVitals vitals)
        {
            float limit = 9f + Athletics * 0.4f;
            if (fallSpeed >= -limit) return;
            float dmg = (-fallSpeed - limit) * 8f;
            if (crouch)
            {
                // tucked and rolled: most of it goes into the roll, which carries on forward
                dmg *= fallSpeed > -18f ? 0.33f : 0.6f;
                var game = WastelandGame.Instance;
                game?.Toast("ROLLED OUT OF THE FALL");
                game?.Stats?.Practice(Skill.Athletics, 3f);
                var fwd = new Vector3(Velocity.x, 0f, Velocity.z);
                StartSlide(fwd.sqrMagnitude > 0.5f ? fwd.normalized : transform.forward, 3.2f, 0.4f);
                if (DebrisSystem.Instance) for (int i = 0; i < 5; i++) DebrisSystem.Instance.EmitPuff(transform.position + Vector3.up * 0.1f, new Color32(180, 130, 90, 255), 0.06f, Random.insideUnitSphere + Vector3.up, 0.5f);
            }
            vitals?.Hurt(dmg, "FALL");
        }

        // ------------------------------------------------------------------ riding on vehicles

        /// <summary>Standing on something moving (a vehicle bed or roof, a crate on a trailer): move with it and turn
        /// with it. On a vehicle at speed, standing still braces on a perch so hard turns don't throw you off.</summary>
        Vector3 Carrier(float dt)
        {
            carry = Vector3.zero;
            if (!cc.isGrounded || !Probe(transform.position + Vector3.up * 0.3f, Vector3.down, 0.6f, out var under)) { rideFor = 0f; return carry; }
            var rb = under.collider.attachedRigidbody;
            if (!rb || rb.isKinematic) { rideFor = 0f; return carry; }
            var v = rb.GetPointVelocity(transform.position);
            carry = new Vector3(v.x, 0f, v.z);
            transform.rotation = Quaternion.Euler(0f, rb.angularVelocity.y * Mathf.Rad2Deg * dt, 0f) * transform.rotation;
            var car = rb.GetComponent<VehicleDriver>();
            if (car && carry.magnitude > 4f && moveInput.sqrMagnitude < 0.05f) { if ((rideFor += dt) > 0.4f) Brace(car, under.point); }
            else rideFor = 0f;
            return carry;
        }

        void Brace(VehicleDriver car, Vector3 at)
        {
            rideFor = 0f;
            if (!ridePerch || ridePerch.transform.parent != car.transform)
            {
                if (ridePerch) Destroy(ridePerch.gameObject);
                var go = new GameObject("RidePerch");
                go.transform.SetParent(car.transform, false);
                ridePerch = go.AddComponent<MadMax.Building.Seat>();
                ridePerch.standing = ridePerch.hidden = true; ridePerch.rest = 1f; ridePerch.reading = 1f; ridePerch.hasExit = true;
            }
            ridePerch.exitLocal = ridePerch.transform.InverseTransformPoint(at + Vector3.up * 0.05f);          // let go = stand where you were
            float hips = 0.94f * Rig.appearance.height;
            SitOn(ridePerch, ridePerch.transform.InverseTransformPoint(at + Vector3.up * hips));
            WastelandGame.Instance?.Toast("HOLDING ON TO THE " + WastelandGame.Name(car) + " - MOVE TO LET GO");
            WastelandGame.Instance?.Stats?.Practice(Skill.Athletics, 1f);
        }
    }
}
