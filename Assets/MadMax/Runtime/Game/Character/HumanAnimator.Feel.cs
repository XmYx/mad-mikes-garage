using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Locomotion feel and the low poses (animation revamp 2026-10-13), shared by the clip and the procedural
    /// paths: start / stop clips (walk_start, walk_stop, run_stop; else a lean into the first step and a settle back),
    /// turning in place by steps (turn_l90 / turn_r90 over the legs; else stepping legs) when the body turns faster than
    /// <see cref="TurnStepRate"/> while (nearly) still, a lean into turns from the centripetal acceleration, an uphill
    /// lean, a steadied head while moving, out of breath (tired / tired_walk, else heavy breathing and a hunch), shivers
    /// in the cold, chewing; prone (prone_idle / crawl) and sitting on the ground (sit_ground) with procedural stand-ins;
    /// the idle variant chosen by context (hot: wipe the brow, cold: shiver, a pack: check the straps).</summary>
    public partial class HumanAnimator
    {
        // ---------------------------------------------------------------- tunables
        /// <summary>Lean into a turn: degrees per degree of atan(ω·v / g), capped.</summary>
        public const float LeanGain = 0.35f, LeanMax = 14f;
        /// <summary>Lean with the slope (grade 1 = 45°) while walking up or down.</summary>
        public const float SlopeLean = 14f;
        /// <summary>Share of the head's bob and roll taken out while moving, and how fast its steadied aim follows.</summary>
        public const float HeadSteady = 0.5f, HeadSteadyRate = 14f;
        /// <summary>Yaw rate (°/s) that counts as turning on the spot below 0.5 m/s; one step per this many degrees.</summary>
        public const float TurnStepRate = 110f, TurnStepDegrees = 45f;
        /// <summary>Start / stop: speeds that count as setting off and stopping, run stop above this peak.</summary>
        public const float StartFrom = 0.12f, StartAt = 0.3f, StopBelow = 0.2f, StopPeak = 1.0f, RunStopPeak = 3.4f;
        /// <summary>Metres of crawl per stride cycle (when the crawl clip doesn't say).</summary>
        public const float CrawlStride = 0.66f;
        /// <summary>Chewing: nods per second and their size (degrees).</summary>
        public const float ChewRate = 2.6f, ChewSize = 2.2f;

        /// <summary>Where the character looks on its own (attention: sounds, danger, things of interest, idle glances).</summary>
        public Attention Attention;
        /// <summary>The next tick writes the targets at once (no blend): a puppet sequence switching clips at a matched pose.</summary>
        public void Snap() => snapNext = true;
        bool snapNext;

        float yawPrev, selfTurn, bankCur, slopeCur, prevSpeed, speedPeak, tiredW, coldW;
        bool yawInit;
        float startT = -1f, stopT = -1f, turnHold, turnW, turnT, stepPhase, crawlPhase, chewW;
        float startDist, stopWait, stopEntry, stopRate = 1f, lastStride = 1.4f;
        bool stopHard, turnRight, stopPending, stopDriven;
        HumanClip startClip, stopClip;
        Vector3 headFwd;

        /// <summary>Start / stop / turn-step state (tests): the clip or "lean" / "steps" stand-in playing, null = none.</summary>
        public string FeelState => startT >= 0f ? (startClip != null ? startClip.name : "start_lean") : stopT >= 0f ? (stopClip != null ? stopClip.name : "stop_settle") : turnW > 0.3f ? (TurnClip != null ? TurnClip.name : "turn_steps") : null;
        public float Bank => bankCur;

        /// <summary>A driven stop is moving the body: the owner should move it forward at <see cref="RootSpeed"/> m/s
        /// (State.rootStop owners only).</summary>
        public bool RootDriving => stopPending || stopDriven;
        public float RootSpeed => stopPending ? stopEntry : stopDriven && stopClip != null ? Mathf.Max(0f, stopClip.RootSpeedAt(stopT)) * (pelvisBase / 0.94f) * stopRate : 0f;
        /// <summary>A driven stop could start now (clips installed, not starting / stopping already).</summary>
        public bool CanDriveStop => UsingClips && Clip("walk_stop") != null && startT < 0f && !stopPending && stopT < 0f;
        /// <summary>Metres per gait cycle of the last locomotion tick (tests).</summary>
        public float Stride => lastStride;

        float WalkStride(float h) { var w = Clip("walk"); return (w != null && w.speed > 0.05f ? w.speed * w.length : 1.4f) * h; }

        static void StrideOf(HumanClip c, float w, float fallback, ref float wsum, ref float ssum)
        {
            if (w <= 0f) return;
            wsum += w; ssum += w * (c != null && c.speed > 0.05f ? c.speed * c.length : fallback);
        }

        /// <summary>A heel strike of the gait (left at u = 0, right at u = 0.5): a pending driven stop starts here.</summary>
        void HeelStrike(bool left, State s)
        {
            if (!stopPending) return;
            stopPending = false;
            string n = stopHard && Clip("run_stop") != null ? "run_stop" : "walk_stop";
            stopClip = Clip(left ? n : n + "_r");
            if (stopClip == null) return;
            float h = pelvisBase / 0.94f;
            float entry = stopClip.RootSpeedAt(0f);
            if (entry < 0.2f) entry = n == "run_stop" ? 3.9f : 1.4f;
            stopRate = Mathf.Clamp(stopEntry / (entry * h), 0.7f, 1.6f);
            stopT = 0f; stopDriven = stopClip.HasRoot;
        }

        HumanClip TurnClip => Clip(turnRight ? "turn_r90" : "turn_l90");

        static float Env(float t) => Mathf.Clamp01(t * 6f) * Mathf.Clamp01((1f - t) * 4f);

        /// <summary>Hot (1) .. cold (-1) for an air temperature (°C): NPCs and the idle choice.</summary>
        public static float HeatOf(float airC) => airC > 30f ? Mathf.Clamp01((airC - 30f) / 8f) : airC < 5f ? -Mathf.Clamp01((5f - airC) / 10f) : 0f;

        /// <summary>Per tick, before either path: own turn rate, lean, start / stop / turn steps, tiredness.</summary>
        void FeelBegin(float dt, ref State s)
        {
            float yaw = rig.transform.eulerAngles.y;
            selfTurn = yawInit ? Mathf.DeltaAngle(yawPrev, yaw) / Mathf.Max(dt, 1e-4f) : 0f;
            yawPrev = yaw; yawInit = true;
            bool upright = !s.sitting && !s.lying && !s.prone && !s.groundSit;
            float turn = !upright ? 0f : Mathf.Abs(s.turnRate) > 0.01f ? s.turnRate : selfTurn;
            if (Mathf.Abs(turn) * dt > 60f) turn = 0f;                                          // a snap (teleport, seat change), not a turn
            turn = Mathf.Clamp(turn, -720f, 720f);

            // lean into the turn (centripetal a = ω·v), with the slope
            float acc = turn * Mathf.Deg2Rad * s.speed;
            float want = upright ? Mathf.Clamp(Mathf.Atan2(acc, 9.81f) * Mathf.Rad2Deg * LeanGain, -LeanMax, LeanMax) * (s.grounded ? 1f : 0.3f) : 0f;
            bankCur = Mathf.Lerp(bankCur, want, 1f - Mathf.Exp(-6f * dt));
            float grade = upright && s.grounded && s.speed > 0.3f ? Mathf.Clamp(s.verticalSpeed / Mathf.Max(s.speed, 0.3f), -0.7f, 0.7f) : 0f;
            slopeCur = Mathf.Lerp(slopeCur, grade * SlopeLean, 1f - Mathf.Exp(-4f * dt));

            // setting off: walk_start played by the distance actually walked (its rootZ), so the feet stay planted however
            // quickly the body gets going; the gait phase is set so walk_start's last frame hands over at walk u = 0
            float h = pelvisBase / 0.94f;
            speedPeak = Mathf.Max(speedPeak - dt * 3f, s.speed);
            bool feet = upright && s.grounded && !s.crouching && s.action == null;
            if (feet && startT < 0f && stopT < 0f && !stopPending && s.speed > StartAt && prevSpeed <= StartFrom && s.speed < 2.6f)
            {
                startT = 0f; startDist = 0f; startClip = Clip("walk_start");
                if (startClip != null && startClip.HasRoot) phase = -startClip.RootAt(1f).z * h / WalkStride(h) * Mathf.PI * 2f;
            }
            if (startT >= 0f)
            {
                startDist += s.speed * dt;
                if (startClip != null && startClip.HasRoot) startT = startClip.TimeAtRootZ(startDist / Mathf.Max(0.1f, h));
                else startT += dt / (startClip != null ? startClip.length : 0.4f);
                if (startT >= 1f || s.speed > 3f || !feet) startT = -1f;
            }

            // stopping. Driven (the player, s.rootStop): once the input is let go the body keeps its pace to the next
            // heel strike, then walk_stop / run_stop (or the mirrored _r from the right heel) play from that gait phase
            // and their rootZ moves the body (RootSpeed) — the stopping step lands where the clip puts it. Otherwise
            // (NPCs, procedural): the clip / a settle plays over the body's own deceleration.
            if (s.rootStop && UsingClips)
            {
                if (!stopPending && stopT < 0f && s.wantStop && feet && s.speed > StopPeak && Clip("walk_stop") != null)
                { stopPending = true; stopWait = 0f; stopHard = s.speed > RunStopPeak; stopEntry = s.speed; startT = -1f; }
                if ((stopPending || stopDriven) && (!s.wantStop || !feet)) { stopPending = false; if (stopDriven) { stopT = -1f; stopDriven = false; } }
                if (stopPending && (stopWait += dt) > 0.7f) stopPending = false;                 // no heel strike came: let momentum stop it
            }
            else if (feet && stopT < 0f && s.speed < StopBelow && prevSpeed >= StopBelow && speedPeak > StopPeak)
            {
                stopHard = speedPeak > RunStopPeak;
                stopClip = stopHard ? Clip("run_stop") ?? Clip("walk_stop") : Clip("walk_stop");
                stopT = 0f; startT = -1f; stopRate = 1f; stopDriven = false;
            }
            prevSpeed = s.speed;
            if (stopT >= 0f)
            {
                stopT += dt * stopRate / (stopClip != null ? stopClip.length : stopHard ? 0.55f : 0.4f);
                if (stopT >= 1f || (!stopDriven && s.speed > 0.6f) || !feet) { stopT = -1f; stopDriven = false; }
            }

            // turning on the spot: steps
            if (upright && s.grounded && s.speed < 0.5f && Mathf.Abs(turn) > TurnStepRate && s.action == null) { turnHold = 0.25f; turnRight = turn > 0f; }
            else turnHold -= dt;
            bool turning = turnHold > 0f;
            turnW = Mathf.MoveTowards(turnW, turning ? 1f : 0f, dt * 8f);
            if (turning)
            {
                float prev = stepPhase;
                stepPhase += Mathf.Abs(turn) * dt / TurnStepDegrees * Mathf.PI;
                if (Mathf.Sign(Mathf.Sin(prev)) != Mathf.Sign(Mathf.Sin(stepPhase))) Footstep?.Invoke(Mathf.Sin(stepPhase) > 0f);
                var tc = TurnClip;
                if (tc != null) turnT += dt / tc.length;
            }
            else if (turnW <= 0f) { turnT = 0f; stepPhase = 0f; }

            tiredW = Mathf.MoveTowards(tiredW, Mathf.Clamp01(s.tired), dt * 0.8f);
            coldW = Mathf.MoveTowards(coldW, Mathf.Clamp01(-s.heat), dt * 0.5f);
            chewW = Mathf.MoveTowards(chewW, Mathf.Clamp01(s.chewing), dt * 4f);
        }

        // ---------------------------------------------------------------- clip path
        /// <summary>Over the locomotion targets: start / stop / turn clips or their stand-ins, the lean, tired breathing.</summary>
        void FeelClips(State s, float move)
        {
            bool handsBusy = s.tool.HasValue || s.hold != null || s.carrying;                    // a held tool keeps the arms: legs only
            if (startT >= 0f)
            {
                // the first frame is the idle stance, the last walk u = 0: in at once, out over the last quarter
                float e = startClip != null ? Mathf.Clamp01((1f - startT) * 4f) : Env(startT);
                if (startClip != null) { if (handsBusy) OverLegs(startClip, startT, e); else Over(startClip, startT, e, true); }
                else Post(BodyPart.Chest, 7f * e, 0f, 0f);
            }
            if (stopT >= 0f)
            {
                // a driven stop begins at its gait phase (in quickly) and ends in the idle stance
                float e = stopDriven ? Mathf.Clamp01(stopT * 10f) * Mathf.Clamp01((1f - stopT) * 4f) : Env(stopT);
                if (stopClip != null) { if (handsBusy) OverLegs(stopClip, stopT, e * 0.9f); else Over(stopClip, stopT, e * 0.9f, true); }
                else { Post(BodyPart.Chest, -(stopHard ? 10f : 5f) * e, 0f, 0f); pelAcc.y -= (stopHard ? 0.045f : 0.025f) * e; }
            }
            if (turnW > 0f)
            {
                var tc = TurnClip;
                if (tc != null) OverLegs(tc, turnT, turnW);
                else
                {
                    float a = Mathf.Max(0f, Mathf.Sin(stepPhase)) * turnW, b = Mathf.Max(0f, -Mathf.Sin(stepPhase)) * turnW;
                    Post(BodyPart.ThighL, -24f * a, 0, 0); Post(BodyPart.ShinL, 40f * a, 0, 0); Post(BodyPart.FootL, -12f * a, 0, 0);
                    Post(BodyPart.ThighR, -24f * b, 0, 0); Post(BodyPart.ShinR, 40f * b, 0, 0); Post(BodyPart.FootR, -12f * b, 0, 0);
                    pelAcc.y -= 0.012f * (a + b);
                }
            }
            Post(BodyPart.Pelvis, 0f, 0f, -bankCur * 0.6f);
            Post(BodyPart.Chest, slopeCur, 0f, -bankCur * 0.4f);
            if (tiredW > 0f && Clip("tired") == null)
            {
                // no tired clip: heavy breaths and a hunch
                float br = Mathf.Sin(time * 3.6f) * 3.2f * tiredW;
                Post(BodyPart.Chest, 6f * tiredW + br, 0f, 0f);
                Post(BodyPart.Head, 5f * tiredW - br * 0.4f, 0f, 0f);
            }
        }

        /// <summary>A clip over the legs and the pelvis turn only (stepping round).</summary>
        void OverLegs(HumanClip c, float t, float w)
        {
            c.Sample(c.loop ? t : Mathf.Repeat(t, 1f), tmp, out _);
            tgt[0] = Quaternion.Slerp(tgt[0], tmp[0], w * 0.5f);
            for (int i = (int)BodyPart.ThighL; i <= (int)BodyPart.FootR; i++) tgt[i] = Quaternion.Slerp(tgt[i], tmp[i], w);
        }

        /// <summary>Base clips for the low poses; false = a clip is missing (the procedural stand-in runs).</summary>
        bool LowClips(float dt, State s)
        {
            if (s.prone)
            {
                var idle = Clip("prone_idle"); var crawl = Clip("crawl");
                if (idle == null || crawl == null) return false;
                float stride = (crawl.speed > 0.05f ? crawl.speed * crawl.length : CrawlStride) * (pelvisBase / 0.94f);
                CrawlStep(dt, s, stride);
                float cm = Mathf.Clamp01(s.speed / 0.35f);
                Acc(idle, time / idle.length, 1f - cm);
                Acc(crawl, crawlPhase / (Mathf.PI * 2f), cm);
                return true;
            }
            if (s.groundSit)
            {
                var c = Clip("sit_ground");
                if (c == null) return false;
                Acc(c, time / c.length, 1f);
                Post(BodyPart.Head, Mathf.Clamp(s.lookPitch * 0.4f, -20f, 25f), 0, 0);
                return true;
            }
            return false;
        }

        void CrawlStep(float dt, State s, float stride)
        {
            float prev = crawlPhase;
            crawlPhase += s.speed / Mathf.Max(0.2f, stride) * Mathf.PI * 2f * dt;
            if (crawlPhase > 1000f) crawlPhase -= Mathf.PI * 200f;
            if (s.speed > 0.15f && Mathf.Sign(Mathf.Sin(prev)) != Mathf.Sign(Mathf.Sin(crawlPhase))) Footstep?.Invoke(Mathf.Sin(crawlPhase) > 0f);
        }

        // ---------------------------------------------------------------- procedural path
        /// <summary>Face down on the belly, head up; crawling: elbows and knees in turn (commando crawl).</summary>
        void ProneProcedural(float dt, State s, float k)
        {
            CrawlStep(dt, s, CrawlStride * (pelvisBase / 0.94f));
            float h = pelvisBase / 0.94f;
            var pelvis = rig.Bone(BodyPart.Pelvis);
            pelvis.localPosition = Vector3.Lerp(pelvis.localPosition, new Vector3(0f, 0.12f * h, 0.25f * h), k);   // the clips' prone layout
            float m = Mathf.Clamp01(s.speed / 0.35f);
            float sp = Mathf.Sin(crawlPhase) * m, a = Mathf.Max(0f, sp), b = Mathf.Max(0f, -sp);
            float breathe = Mathf.Sin(time * 1.5f) * 1.2f;
            Set(BodyPart.Pelvis, 86f, sp * 5f, 0f, k);
            Set(BodyPart.ThighL, -12f * a, 0f, -6f - 35f * a, k);
            Set(BodyPart.ThighR, -12f * b, 0f, 6f + 35f * b, k);
            Set(BodyPart.ShinL, 8f + 70f * a, 0f, 0f, k);
            Set(BodyPart.ShinR, 8f + 70f * b, 0f, 0f, k);
            Set(BodyPart.FootL, 55f, 0f, 0f, k);
            Set(BodyPart.FootR, 55f, 0f, 0f, k);
            Set(BodyPart.Chest, -10f + breathe, -sp * 6f, 0f, k);
            Set(BodyPart.Head, -58f + Mathf.Clamp(s.lookPitch * 0.3f, -10f, 15f), 0f, 0f, k);
            Set(BodyPart.UpperArmL, -150f + 28f * sp, 0f, -24f, k);
            Set(BodyPart.UpperArmR, -150f - 28f * sp, 0f, 24f, k);
            Set(BodyPart.ForearmL, -95f + 20f * b, 0f, 0f, k);
            Set(BodyPart.ForearmR, -95f + 20f * a, 0f, 0f, k);
            Set(BodyPart.HandL, 0f, 0f, 0f, k);
            Set(BodyPart.HandR, 0f, 0f, 0f, k);
        }

        /// <summary>Sitting on the ground: legs out, knees a little up, leaning back on the hands.</summary>
        void GroundSitProcedural(State s, float k)
        {
            float h = pelvisBase / 0.94f;
            var pelvis = rig.Bone(BodyPart.Pelvis);
            pelvis.localPosition = Vector3.Lerp(pelvis.localPosition, new Vector3(0f, 0.11f * h, 0f), k);
            float breathe = Mathf.Sin(time * 1.5f) * 0.9f;
            Set(BodyPart.Pelvis, -8f, 0f, 0f, k);
            Set(BodyPart.ThighL, -100f, 0f, -7f, k);
            Set(BodyPart.ThighR, -96f, 0f, 9f, k);
            Set(BodyPart.ShinL, 40f, 0f, 0f, k);
            Set(BodyPart.ShinR, 50f, 0f, 0f, k);
            Set(BodyPart.FootL, -8f, 0f, 0f, k);
            Set(BodyPart.FootR, -12f, 0f, 0f, k);
            Set(BodyPart.Chest, -2f + breathe, 0f, 0f, k);
            Set(BodyPart.Head, Mathf.Clamp(s.lookPitch * 0.4f, -20f, 25f), 0f, 0f, k);
            Set(BodyPart.UpperArmL, 30f, 0f, -16f, k);
            Set(BodyPart.UpperArmR, 30f, 0f, 16f, k);
            Set(BodyPart.ForearmL, -6f, 0f, 0f, k);
            Set(BodyPart.ForearmR, -6f, 0f, 0f, k);
            Set(BodyPart.HandL, -40f, 0f, 0f, k);
            Set(BodyPart.HandR, -40f, 0f, 0f, k);
        }

        // ---------------------------------------------------------------- after the pose
        /// <summary>The head stays steadier than the body under it while moving: less roll, a lagged aim.</summary>
        void SteadyHead(float dt, State s)
        {
            var head = rig.Bone(BodyPart.Head);
            if (!head) return;
            var f = head.forward;
            if (headFwd.sqrMagnitude < 0.5f) headFwd = f;
            headFwd = Vector3.Slerp(headFwd, f, 1f - Mathf.Exp(-HeadSteadyRate * dt));
            bool on = s.grounded && !s.sitting && !s.lying && !s.prone && !s.groundSit && s.action == null;
            float w = on ? HeadSteady * Mathf.Clamp01(s.speed / 1.2f) : 0f;
            if (w <= 0.001f || headFwd.sqrMagnitude < 0.5f || Mathf.Abs(headFwd.y) > 0.95f) return;
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(headFwd, Vector3.up), w);
        }

        /// <summary>Chewing nods and cold shivers (Head / Chest, additive like the talk nods).</summary>
        void ChewAndShiver(State s)
        {
            var head = rig.Bone(BodyPart.Head); var chest = rig.Bone(BodyPart.Chest);
            if (chewW > 0.001f && head)
            {
                float c = Mathf.Max(0f, Mathf.Sin(time * ChewRate * Mathf.PI * 2f)) * ChewSize * chewW;
                var add = Quaternion.Euler(c, 0f, Mathf.Sin(time * 1.3f) * 0.8f * chewW);
                head.localRotation *= add;
                headAdd = addHead == head ? headAdd * add : add;
                addHead = head;
            }
            bool shivering = coldW > 0.05f && !s.lying && (idleCur == null || idleCur.name != "idle_shiver");
            if (shivering && chest)
            {
                float tr = (Mathf.Sin(time * 71f) * 0.6f + Mathf.Sin(time * 43f) * 0.4f) * 0.9f * coldW;
                var add = Quaternion.Euler(tr * 0.4f, 0f, tr);
                chest.localRotation *= add;
                chestAdd = addChest == chest ? chestAdd * add : add;
                addChest = chest;
            }
        }

        // ---------------------------------------------------------------- idles by context
        static readonly string[] CalmIdles = { "idle_shift", "idle_look", "idle_scratch", "idle_shift", "idle_look", "idle_stretch", "idle_neck", "idle_hips", "idle_scratch" };

        /// <summary>The next idle variant: hot → wipe the brow, cold → shiver, a pack → check it, else a calm one.</summary>
        string PickIdle(State s, bool freeHands)
        {
            if (!freeHands) return IdleVariants[rnd.Next(IdleVariants.Length)];
            double r = rnd.NextDouble();
            if (s.heat > 0.4f && r < 0.5 && Clip("idle_wipe_brow") != null) return "idle_wipe_brow";
            if (s.heat < -0.4f && r < 0.6 && Clip("idle_shiver") != null) return "idle_shiver";
            if (s.pack && r > 0.75 && Clip("idle_check_pack") != null) return "idle_check_pack";
            for (int i = 0; i < 5; i++) { var n = CalmIdles[rnd.Next(CalmIdles.Length)]; if (Clip(n) != null) return n; }
            return "idle_shift";
        }
    }
}
