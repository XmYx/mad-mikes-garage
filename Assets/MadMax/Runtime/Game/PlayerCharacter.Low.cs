using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Low on the ground (animation revamp 2026-10-13). PRONE (<see cref="Controls.Act.Prone"/>, default X, on
    /// foot): go_prone, then crawl on the belly at <see cref="CrawlSpeed"/> (prone_idle / crawl, quiet, hard to spot,
    /// a 0.6 m capsule); the key again, Space or Shift gets up (prone_up) when there is headroom; never in water deeper
    /// than <see cref="ProneWater"/>. SIT DOWN / LIE DOWN anywhere on open, level ground (context menu): sit_ground_down
    /// → sit_ground, lie_down → lie (rolling onto the side after a while: lie_side); resting there recovers stamina
    /// faster (<see cref="RestSitRegen"/>, <see cref="RestLieRegen"/>); any move gets up through sit_ground_up /
    /// lie_up. Eating and drinking while seated (chair or ground) play eat / drink over the sitting pose with the food
    /// in the hand. Also the shared bits of every on-foot pose: tiredness, heat, a pack, chewing (<see cref="Ambient"/>),
    /// turning on the spot before walking off, startles and covering up from attention.</summary>
    public partial class PlayerCharacter
    {
        public enum Low : byte { None, Prone, Sit, Lie }

        // ---------------------------------------------------------------- tunables
        public const float ProneHeight = 0.6f, SitGroundHeight = 1.0f;
        public const float CrawlSpeed = 0.55f, CrawlTurn = 3.5f;                           // the crawl clip's pace (0.66 m a cycle)
        /// <summary>Prone layout (CLIPS.md): root fixed at the feet's old spot, pelvis 0.25 h ahead, head ~1 m ahead, feet
        /// 0.6 m behind; the controller stays at the root and a probe this far ahead (× h) keeps the head out of walls.</summary>
        public const float ProneReach = 1.05f, ProneProbeRadius = 0.18f;
        /// <summary>Transition times used when a clip is missing (else the clip's own length).</summary>
        public const float GoProneTime = 0.9f, ProneUpTime = 0.95f, SitDownTime = 1.1f, SitUpTime = 1.0f, LieDownTime = 1.6f, LieUpTime = 1.5f;
        /// <summary>Stamina recovery multipliers while resting on the ground.</summary>
        public const float RestSitRegen = 1.6f, RestLieRegen = 2.2f;
        /// <summary>Lying still this long rolls onto the side.</summary>
        public const float RollOnSideAfter = 25f;
        /// <summary>Ground steeper than this (normal y below) is no place to sit or lie.</summary>
        public const float RestMaxSlopeY = 0.86f;
        /// <summary>Water deeper than this (m) stops a crawl (swim instead).</summary>
        public const float ProneWater = 0.4f;
        /// <summary>Chewing after a bite (s).</summary>
        public const float ChewSeconds = 2.2f;
        /// <summary>Setting off in a direction more than this far off the facing turns on the spot first, at this rate,
        /// creeping at this share of the pace meanwhile.</summary>
        public const float TurnInPlaceAngle = 60f, TurnInPlaceRate = 360f, TurnInPlaceCreep = 0.15f;
        /// <summary>Turning to face a danger behind while standing idle (°/s).</summary>
        public const float AttentionTurnRate = 220f;
        /// <summary>Out of breath below this share of the stamina (fully at none).</summary>
        public const float TiredBelow = 0.3f;
        /// <summary>A dust storm this thick: a hand over the eyes walking in the open.</summary>
        public const float ShieldEyesDust = 0.35f;

        [HideInInspector] public bool proneToggle;                  // input: pressed this frame
        bool proneNow;
        /// <summary>The low pose held (also while getting into it); None standing.</summary>
        public Low LowState { get; private set; }
        public bool Prone => LowState == Low.Prone;
        public bool GroundResting => LowState == Low.Sit || LowState == Low.Lie;
        /// <summary>Progress 0..1 of getting down or up; -1 = none.</summary>
        public float LowTransit => lowT;
        /// <summary>The transition clip playing (go_prone, prone_up, sit_ground_down, ...), null = none.</summary>
        public string LowClip => lowT >= 0f ? lowClip : null;
        /// <summary>Lying on the side (rolled over after a while).</summary>
        public bool LyingSide => lowSide;
        /// <summary>Stamina recovery factor of the pose (PlayerVitals).</summary>
        public float RestFactor => lowT >= 0f ? 1f : LowState == Low.Sit ? RestSitRegen : LowState == Low.Lie ? RestLieRegen : 1f;
        /// <summary>Chewing until (Time.time): set when a bite is taken.</summary>
        [System.NonSerialized] public float ChewUntil = -9f;

        float lowT = -1f, lowDur, lowRested;
        bool lowUp, lowSide, lowHid;
        string lowClip;
        Vector3 lowRoot; Quaternion lowRot = Quaternion.identity;

        static float ClipLen(string n, float fallback) => HasClip(n) ? HumanClips.Get(n).length : fallback;

        // ---------------------------------------------------------------- getting down
        /// <summary>Go prone, sit or lie down here. False (with a toast for the player's own request) = not here / not now.</summary>
        public bool GoLow(Low to, bool quiet = false)
        {
            if (to == Low.None || LowState != Low.None || lowT >= 0f || anim == null || Ragdolled) return false;
            if (!cc.enabled || Sitting || SeatedIn || Interior || Traversing || Climbing != null || Swimming || Sliding || InTransit || Puppeted) return false;
            var game = WastelandGame.Instance;
            if (Carried) { if (!quiet) game?.Toast("PUT THE PART DOWN FIRST"); return false; }
            var p = transform.position;
            var terrain = DeformableTerrain.Instance;
            if (terrain && terrain.WaterDepth(p.x, p.z) > (to == Low.Prone ? ProneWater : 0.15f)) { if (!quiet) game?.Toast("TOO WET HERE"); return false; }
            if (to != Low.Prone && Probe(p + Vector3.up * 0.4f, Vector3.down, 0.8f, out var under) && under.normal.y < RestMaxSlopeY) { if (!quiet) game?.Toast("TOO STEEP TO REST HERE"); return false; }
            if (Crouching) SetCrouch(false);
            LowState = to; lowUp = false; lowT = 0f; lowSide = false; lowRested = 0f;
            lowClip = to == Low.Prone ? "go_prone" : to == Low.Sit ? "sit_ground_down" : "lie_down";
            lowDur = ClipLen(lowClip, to == Low.Prone ? GoProneTime : to == Low.Sit ? SitDownTime : LieDownTime);
            swingT = -1f; vy = 0f; Velocity = Vector3.zero; planar = Vector3.zero;
            lowHid = Tool || prop;
            ShowHeld(false);                                                                   // hands to the ground
            if (to == Low.Prone) SetHeight(ProneHeight);
            else { lowRoot = p; lowRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f); cc.enabled = false; }
            if (game && game.Player == this && !quiet)
            {
                if (to == Low.Prone) Hints.Show("prone", "PRONE: MOVE TO CRAWL   " + Controls.Name(Controls.Act.Prone) + " / " + Controls.Name(Controls.Act.Jump) + " GET UP");
                else Hints.Show("rest_ground", "RESTING: STAMINA COMES BACK QUICKER   MOVE TO GET UP");
            }
            return true;
        }

        /// <summary>Get up again (through prone_up / sit_ground_up / lie_up) when there is room to stand.</summary>
        public bool GetUpFromLow(bool quiet = false)
        {
            if (LowState == Low.None || (lowT >= 0f && lowUp)) return false;
            var feet = LowState == Low.Prone ? transform.position : lowRoot;
            if (!Room(feet, 0.3f)) { if (!quiet) WastelandGame.Instance?.Toast("NO ROOM TO STAND UP"); return false; }
            lowClip = LowState == Low.Prone ? "prone_up" : LowState == Low.Sit ? "sit_ground_up" : "lie_up";
            lowDur = ClipLen(lowClip, LowState == Low.Prone ? ProneUpTime : LowState == Low.Sit ? SitUpTime : LieUpTime);
            lowUp = true; lowT = 0f;
            return true;
        }

        /// <summary>Out of any low pose at once (seated, teleported, ragdolled).</summary>
        void ClearLow()
        {
            if (LowState == Low.None && lowT < 0f) return;
            bool wasProne = LowState == Low.Prone;
            LowState = Low.None; lowT = -1f; lowSide = false;
            if (wasProne) SetHeight(StandHeight);
            if (lowHid) { lowHid = false; if (!SeatedIn) ShowHeld(true); }
            EndSeatedGesture();
        }

        void FinishLow()
        {
            var was = LowState;
            LowState = Low.None; lowT = -1f; lowSide = false;
            if (lowHid) { lowHid = false; ShowHeld(true); }
            EndSeatedGesture();
            if (was == Low.Prone) { SetHeight(StandHeight); return; }
            Teleport(lowRoot, lowRot.eulerAngles.y);
        }

        // ---------------------------------------------------------------- the tick
        /// <summary>One frame low on the ground (Update returns after it); false = standing, carry on.</summary>
        bool LowTick(float dt)
        {
            bool toggle = proneNow; proneNow = false;
            if (LowState == Low.None) return toggle && cc.isGrounded && GoLow(Low.Prone);
            var st = new HumanAnimator.State { grounded = true, lookPitch = lookPitch };
            Ambient(ref st);
            if (lowT >= 0f)
            {
                lowT += dt / Mathf.Max(0.05f, lowDur);
                float u = Mathf.Clamp01(lowT);
                bool lowered = lowUp ? u < 0.5f : u > 0.5f;                                   // the base pose under the clip
                if (LowState == Low.Prone) st.prone = lowered;
                else if (LowState == Low.Sit) st.groundSit = lowered;
                else st.lying = lowered;
                if (HasClip(lowClip)) { st.action = lowClip; st.actionT = u; }
                if (LowState == Low.Prone) Gravity(dt);
                if (lowT >= 1f)
                {
                    lowT = -1f;
                    BakeLowRoot();                                                            // lie_down / lie_up move the root (rootZ)
                    if (lowUp) { FinishLow(); TickAnim(dt, st); return true; }
                }
            }
            else if (LowState == Low.Prone)
            {
                if (toggle || jump || (run && moveInput.sqrMagnitude > 0.1f)) { jump = false; GetUpFromLow(); }
                Crawl(dt, ref st);
                if (LowState == Low.None) return true;                                        // forced up (water)
            }
            else
            {
                if (moveInput.sqrMagnitude > 0.25f || jump || toggle) { jump = false; GetUpFromLow(); }
                lowRested += dt;
                if (LowState == Low.Lie && lowRested > RollOnSideAfter) lowSide = true;
                st.groundSit = LowState == Low.Sit; st.lying = LowState == Low.Lie; st.lyingSide = lowSide;
                SeatedGestureTick(dt, ref st);
            }
            if (LowState != Low.Prone)
            {
                // resting: the root stays where the body went down, moved by a transition's root motion (lie_down /
                // lie_up: ∓0.45 m along the facing); procedural lying tips it over
                var pos = lowRoot; var rot = lowRot;
                if (lowT >= 0f && HasClip(lowClip) && HumanClips.Get(lowClip) is HumanClip rc && rc.HasRoot)
                { float u = Mathf.Clamp01(lowT); pos += lowRot * (rc.RootAt(u) * H); rot = lowRot * Quaternion.Euler(0f, rc.RootYawAt(u), 0f); }
                bool flat = st.lying && !anim.UsingClips;
                if (flat) { if (st.lyingSide) LieSideProcedural(ref pos, ref rot, H); else LieProcedural(ref pos, ref rot, H); }
                transform.SetPositionAndRotation(pos, rot);
                Velocity = Vector3.zero;
            }
            TickAnim(dt, st);
            return true;
        }

        /// <summary>The finished transition's root motion becomes the resting root.</summary>
        void BakeLowRoot()
        {
            if (LowState == Low.Prone || !HasClip(lowClip) || !(HumanClips.Get(lowClip) is HumanClip c) || !c.HasRoot) return;
            lowRoot += lowRot * (c.RootAt(1f) * H);
            lowRot *= Quaternion.Euler(0f, c.RootYawAt(1f), 0f);
        }

        void Gravity(float dt)
        {
            if (!cc.enabled) return;
            vy = cc.isGrounded ? -1f : vy + gravity * dt;
            cc.Move(Vector3.up * vy * dt);
        }

        /// <summary>On the belly: slow, quiet, turning slowly; water too deep gets you up.</summary>
        void Crawl(float dt, ref HumanAnimator.State st)
        {
            var terrain = DeformableTerrain.Instance;
            var p = transform.position;
            if (terrain && terrain.WaterDepth(p.x, p.z) > ProneWater)
            {
                WastelandGame.Instance?.Toast("TOO DEEP TO CRAWL");
                st.prone = true;
                if (!GetUpFromLow(true)) FinishLow();
                return;
            }
            var game = WastelandGame.Instance;
            var dir = Quaternion.Euler(0, viewYaw, 0) * new Vector3(moveInput.x, 0, moveInput.y);
            dir = Vector3.ClampMagnitude(AutoWalk ?? dir, 1f);
            float speed = CrawlSpeed * (game && game.Stats != null ? game.Stats.MoveSpeed : 1f) * (game ? game.InjurySpeed : 1f);
            if (terrain) speed *= Mathf.Lerp(1f, 0.6f, terrain.SurfaceAt(p.x, p.z).mud);
            vy = cc.isGrounded ? -1f : vy + gravity * dt;
            // the body lies ahead of the root (head ~1 m, CLIPS.md prone layout): it stops at a wall the capsule can't feel
            var head = transform.forward;
            float ahead = Vector3.Dot(dir, head);
            if (ahead > 0.05f && Physics.SphereCast(p + Vector3.up * 0.25f, ProneProbeRadius, head, out var block, ProneReach * H, ~0, QueryTriggerInteraction.Ignore)
                && !block.transform.IsChildOf(transform) && block.normal.y < 0.6f)
                dir -= head * ahead * Mathf.Clamp01(1f - (block.distance - ProneReach * H * 0.85f) / (ProneReach * H * 0.15f));
            cc.Move((dir * speed + Vector3.up * vy) * dt);
            Velocity = cc.velocity;
            if (terrain)
            {
                float h = terrain.Height(transform.position.x, transform.position.z);
                if (transform.position.y < h - 0.25f) { cc.enabled = false; transform.position = new Vector3(transform.position.x, h + 0.05f, transform.position.z); cc.enabled = true; }
            }
            float yaw0 = transform.eulerAngles.y;
            if (faceView) transform.rotation = Quaternion.Euler(0f, viewYaw, 0f);
            else if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-CrawlTurn * dt));
            float hs = new Vector2(Velocity.x, Velocity.z).magnitude;
            st.prone = true; st.speed = hs; st.turnRate = Mathf.DeltaAngle(yaw0, transform.eulerAngles.y) / Mathf.Max(dt, 1e-4f);
            lastYaw = transform.eulerAngles.y;
        }

        /// <summary>Without clips the body can't roll by its bones: the root tips onto its right side.</summary>
        static void LieSideProcedural(ref Vector3 pos, ref Quaternion rot, float h)
        {
            var pel = pos + rot * Vector3.up * (0.16f * h);
            var tipped = rot * Quaternion.Euler(-88f, 0f, 0f) * Quaternion.Euler(0f, 84f, 0f);
            pos = pel - tipped * Vector3.up * (0.94f * h);
            rot = tipped;
        }

        // ---------------------------------------------------------------- shared pose inputs
        bool hasPack; int packOutfitHash;

        /// <summary>Tiredness, heat / cold, a pack on the back, chewing: the inputs every pose of the player shares.</summary>
        void Ambient(ref HumanAnimator.State st)
        {
            var g = WastelandGame.Instance;
            var stats = g ? g.Stats : null;
            var vitals = g ? g.Vitals : null;
            if (stats != null && vitals)
            {
                float share = stats.stamina / Mathf.Max(1f, stats.MaxStamina);
                st.tired = vitals.Exhausted ? 1f : Mathf.Clamp01((TiredBelow - share) / TiredBelow);
                float core = stats.bodyTemp;
                st.heat = core > 37.4f ? Mathf.Clamp01((core - 37.4f) / 1.2f) : core < 36.4f ? -Mathf.Clamp01((36.4f - core) / 1.2f) : 0f;
                if (st.heat == 0f) st.heat = HumanAnimator.HeatOf(Weather.Temperature) * 0.8f;
            }
            if (Rig)
            {
                int hsh = Rig.outfit.Count;
                for (int i = 0; i < Rig.outfit.Count; i++) hsh = hsh * 31 + (Rig.outfit[i] != null ? Rig.outfit[i].GetHashCode() : 0);
                if (hsh != packOutfitHash)
                {
                    packOutfitHash = hsh; hasPack = false;
                    foreach (var id in Rig.outfit) { var d = ClothingLibrary.Get(id); if (d != null && d.slot == ClothingSlot.Pack) { hasPack = true; break; } }
                }
                st.pack = hasPack;
            }
            st.chewing = Time.time < ChewUntil ? 1f : 0f;
            if (g && st.limpL == 0f && st.limpR == 0f) { st.limpL = g.LimpL; st.limpR = g.LimpR; st.armHurtL = g.ArmHurtL; st.armHurtR = g.ArmHurtR; }
        }

        // ---------------------------------------------------------------- eating seated
        string seatGesture; float seatGestureT = -1f, seatGestureLen; bool seatGestureProp;

        /// <summary>Eat / drink sitting down (chair, bench, the ground): the clip over the seated pose, the food in the hand.
        /// False = not seated (the standing gesture plays instead).</summary>
        public bool SeatedGesture(string clip, Mesh mesh, Material mat)
        {
            bool seated = (Sitting && SeatedOn && !SeatedOn.standing && SeatedOn.ride == null && !seatLie && !InTransit) || (GroundResting && lowT < 0f && LowState == Low.Sit);
            if (!seated || anim == null) return false;
            EndSeatedGesture();
            seatGesture = clip; seatGestureT = 0f; seatGestureLen = ClipLen(clip, 1.2f);
            if (mesh) { HoldProp(mesh, mat); seatGestureProp = true; }
            return true;
        }

        /// <summary>The seated eat / drink playing (tests), null = none.</summary>
        public string SeatedGestureClip => seatGestureT >= 0f ? seatGesture : null;

        void SeatedGestureTick(float dt, ref HumanAnimator.State st)
        {
            if (seatGestureT < 0f) return;
            seatGestureT += dt / Mathf.Max(0.1f, seatGestureLen);
            if (seatGestureT >= 1f) { EndSeatedGesture(); return; }
            if (HasClip(seatGesture)) { st.action = seatGesture; st.actionT = seatGestureT; }
        }

        void EndSeatedGesture()
        {
            if (seatGestureT < 0f) return;
            seatGestureT = -1f; seatGesture = null;
            if (seatGestureProp) { seatGestureProp = false; DropProp(); if (LowState != Low.None) ShowHeld(false); }
        }

        // ---------------------------------------------------------------- clip events
        string evClip; float evT = -1f;
        /// <summary>The last clip event the player's body fired (tests): stepL, bite, sip ...</summary>
        public string LastClipEvent { get; private set; }

        /// <summary>The animator tick with the action clip's events fired as it passes them (bite → chewing, sip → a
        /// gulp, steps of turns and transitions → footsteps).</summary>
        void TickAnim(float dt, HumanAnimator.State st)
        {
            if (st.action != null && st.actionT >= 0f && HumanClips.Get(st.action) is HumanClip c && c.EventCount > 0)
            {
                bool same = st.action == evClip;
                if (!same || st.actionT > evT)
                {
                    float from = same ? evT : -1f;
                    for (int i = 0; i < c.EventCount; i++)
                    {
                        float t = c.EventTime(i);
                        if ((from < 0f ? t >= 0f : t > from) && t <= st.actionT) ClipEvent(c.EventName(i));
                    }
                }
            }
            evClip = st.action; evT = st.actionT;
            anim.Tick(dt, st);
        }

        void ClipEvent(string e)
        {
            LastClipEvent = e;
            switch (e)
            {
                case "stepL": case "stepR": OnFootstep(e == "stepL"); break;
                case "bite": ChewUntil = Time.time + ChewSeconds; break;
                case "sip": MadMax.Audio.Sfx.Play("drink", Eye.position, 0.35f, Random.Range(0.95f, 1.1f), 10f, 0.2f); break;
            }
        }

        // ---------------------------------------------------------------- stepping round on the spot
        /// <summary>The turn clip's share of 90° per degree of the turn, and its pace limits.</summary>
        public const float TurnStepMin = 0.75f, TurnStepMax = 1.5f;
        float turnStepT = -1f, turnStepLen, turnStepFrom, turnStepAngle, turnStepFull;
        string turnStepClip;
        /// <summary>The turn clip stepping the body round (tests), null = none.</summary>
        public string TurningClip => turnStepT >= 0f ? turnStepClip : null;

        /// <summary>Setting off well away from the facing: turn_l90 / turn_r90 turn the root along their rootYaw (scaled to
        /// the angle) while the feet step round; the body doesn't travel meanwhile. False = not turning (no clip: the creep).</summary>
        bool TurnStepTick(Vector3 dir, float dt)
        {
            if (turnStepT < 0f)
            {
                if (!TurnFirst(dir) || !cc.isGrounded || Carried || swingT >= 0f || PoseOverride.HasValue || Crouching) return false;
                float angle = Vector3.SignedAngle(transform.forward, dir, Vector3.up);
                string n = angle > 0f ? "turn_r90" : "turn_l90";
                var c = HasClip(n) ? HumanClips.Get(n) : null;
                if (c == null || !c.HasRoot) return false;
                turnStepClip = n; turnStepT = 0f; turnStepFrom = transform.eulerAngles.y; turnStepAngle = angle;
                turnStepFull = Mathf.Abs(c.RootYawAt(1f)) > 1f ? Mathf.Abs(c.RootYawAt(1f)) : 90f;
                turnStepLen = c.length * Mathf.Clamp(Mathf.Abs(angle) / turnStepFull, TurnStepMin, TurnStepMax);
            }
            var clip = HumanClips.Get(turnStepClip);
            turnStepT += dt / Mathf.Max(0.1f, turnStepLen);
            float u = Mathf.Clamp01(turnStepT);
            float yaw = clip != null ? Mathf.Abs(clip.RootYawAt(u)) / turnStepFull * turnStepAngle : turnStepAngle * u;
            transform.rotation = Quaternion.Euler(0f, turnStepFrom + yaw, 0f);
            if (turnStepT >= 1f) { turnStepT = -1f; return false; }
            return true;
        }

        // ---------------------------------------------------------------- turning, startles, covering up
        float coverUntil = -9f;

        /// <summary>Setting off well away from the facing: turn on the spot first (the animator steps round).</summary>
        bool TurnFirst(Vector3 dir) => !faceView && !aiming && !Sliding && dir.sqrMagnitude > 0.01f && planar.sqrMagnitude < 0.64f
                                       && Vector3.Angle(transform.forward, dir) > TurnInPlaceAngle;

        /// <summary>Standing idle with a danger behind: turn to face it (attention).</summary>
        void FaceAttention(float dt)
        {
            var at = anim != null ? anim.Attention : null;
            if (at == null || faceView || aiming || Carried || swingT >= 0f || PoseOverride.HasValue || AutoWalk.HasValue || !at.Target.HasValue) { turnLatch = null; return; }
            if (at.TurnYaw.HasValue) turnLatch = at.TurnYaw.Value;                               // behind: turn round to face it,
            if (!turnLatch.HasValue) return;
            var want = Quaternion.Euler(0f, turnLatch.Value, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, AttentionTurnRate * dt);
            if (Quaternion.Angle(transform.rotation, want) < 20f) turnLatch = null;               // not just until the neck can manage
        }

        float? turnLatch;

        /// <summary>A reaction asked for by attention (a bang close by): startle (flinch without the clip), cover the head
        /// (arms over it without the clip).</summary>
        void TakeAttentionReaction()
        {
            var at = anim != null ? anim.Attention : null;
            if (at == null) return;
            var r = at.TakeReaction(out var from);
            if (r == null || swingT >= 0f || PoseOverride.HasValue || Carried || LowState != Low.None) return;
            var d = from - transform.position; d.y = 0f;
            if (r == "startle" && Vector3.Dot(d, transform.right) < 0f && HasClip("startle_l")) r = "startle_l";   // turns toward the noise
            if (HasClip(r)) { reactClip = r; reactAt = Time.time; reactFrom = d.sqrMagnitude > 0.01f ? d.normalized * 0.6f : Vector3.zero; return; }
            if (r == "cover_head") { coverUntil = Time.time + 1.4f; return; }
            reactClip = "flinch"; reactAt = Time.time; reactFrom = d.sqrMagnitude > 0.01f ? d.normalized * 0.8f : Vector3.zero;
        }

        /// <summary>Arms over the head, ducking (cover_head stand-in).</summary>
        static readonly ToolPose CoverPose = new ToolPose { armRX = -155f, armRZ = -30f, foreR = -125f, armLX = -155f, armLZ = 30f, foreL = -125f, chestX = 24f, knees = 35f };
        /// <summary>A hand over the eyes (shield_eyes stand-in).</summary>
        static readonly ToolPose ShieldPose = new ToolPose { armRX = -118f, armRZ = -24f, foreR = -112f, handRZ = -20f, armLX = -4f, armLZ = -9f, foreL = -14f, chestX = 8f };

        /// <summary>Covering up this frame (cover_head stand-in playing).</summary>
        public bool CoveringHead => Time.time < coverUntil;

        /// <summary>Walking in a dust storm in the open with the hands free: a hand over the eyes.</summary>
        bool ShieldingEyes => Storms.Dust > ShieldEyesDust && !Interior && !Tool && !Carried && !aiming && swingT < 0f && !PoseOverride.HasValue && !prop;
    }
}
