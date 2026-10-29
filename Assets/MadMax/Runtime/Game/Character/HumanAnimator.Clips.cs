using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The keyframed layer of <see cref="HumanAnimator"/> (clips authored on the HD rig in Blender,
    /// <see cref="HumanClips"/>): locomotion blends idle variants / walk / run / sprint (phase-locked to the stride, so
    /// footsteps stay where they were) or crouch idle / crouch walk, jump / fall / land in the air, sit_drive / sit / ride
    /// (+ pedal) seated, lie when sleeping; action clips (tool swings, work, vault, death) and carry / aim / talk overlays
    /// play over the upper or the whole body. Procedural layers stay on top: steering hands, look pitch, turn bank, limp,
    /// hurt arms, tool poses without a clip, foot IK on uneven ground, head look-at. Without clips (or with
    /// --procedural-anim) the procedural animator runs as before.</summary>
    public partial class HumanAnimator
    {
        /// <summary>World point the head turns to (null = none).</summary>
        public Vector3? LookAt;
        /// <summary>Plant the feet on uneven ground (two-bone leg IK, close to the camera only).</summary>
        public bool FootIK = true;
        /// <summary>The last tick used keyframed clips.</summary>
        public bool UsingClips { get; private set; }
        /// <summary>Base clip of the last tick (walk, run, sit_drive...: the heaviest one) and the action clip playing.</summary>
        public string BaseClip { get; private set; }
        public string ActionClip => actClip != null && actW > 0.01f ? actClip.name : null;

        const int N = HumanClip.Bones;
        readonly Quaternion[] tgt = new Quaternion[N], tmp = new Quaternion[N], cur = new Quaternion[N];
        readonly Transform[] bt = new Transform[N];
        Vector3 pelCur;
        bool curValid;
        HumanClip idleCur, idlePrev, actClip;
        float idleT, idlePrevT, idleFade = 1f, idleNext = 6f;
        float airT, landT = 9f, airW, crouchW, actW, actT, carryW, aimW, talkW, lookW, ikL, ikR, ikPelvis;
        HumanClip holdClip, fidgetClip;
        float holdW, fidgetT = -1f, fidgetNext = 6f;
        bool wasGrounded = true;
        float baseW;
        Vector3 lastLook;
        System.Random rnd;

        /// <summary>One tick of the animator: clips when installed, the procedural poses otherwise.</summary>
        public void Tick(float dt, State s)
        {
            if (rig.Limp) { chestAdd = headAdd = Quaternion.identity; return; }                 // physics poses a ragdoll
            // last tick's look / nod additions come off first: the procedural poses slerp from the current rotation
            if (addChest && addChest == rig.Bone(BodyPart.Chest)) addChest.localRotation *= Quaternion.Inverse(chestAdd);
            if (addHead && addHead == rig.Bone(BodyPart.Head)) addHead.localRotation *= Quaternion.Inverse(headAdd);
            chestAdd = headAdd = Quaternion.identity;
            FeelBegin(dt, ref s);
            if (HumanClips.Enabled && HumanClips.Get("idle") != null && HumanClips.Get("walk") != null && HumanClips.Get("run") != null)
            {
                if (!UsingClips) curValid = false;
                UsingClips = true;
                TickClips(dt, s);
            }
            else
            {
                UsingClips = false;
                BaseClip = null;
                TickProcedural(dt, s);
                if (Bind()) ApplyFootIK(dt, s.grounded && !s.sitting && !s.lying && !s.prone && !s.groundSit && FootIK && NearCamera(25f));
            }
            SteadyHead(dt, s);
            if (!face) face = rig.GetComponent<HumanFace>();
            // where to look: what code asks for (talking), else the attention (aim, danger, sounds, things, glances),
            // else what the player could use / the camera's aim; nothing while lying or prone
            var auto = face ? face.AutoLook : null;
            if (Attention != null) Attention.Tick(dt, s, auto);
            var look = LookAt ?? (Attention != null && Attention.Target.HasValue ? Attention.Target : auto);
            if ((s.lying || s.prone) && !LookAt.HasValue) look = null;
            if (Attention != null && Attention.Shifted && face && !LookAt.HasValue) face.Blink();
            ApplyLookAt(dt, look, s);
            TalkNods(dt, s);
            ChewAndShiver(s);
            HitLean(s);
            Breathe(dt, s);                                                               // a cloud in the cold (HumanAnimator.Breath)
            if (face)
            {
                face.Target = look;
                face.Talking = s.talking;
                face.Stress = Mathf.Max(s.stress, Mathf.Max(Mathf.Max(s.limpL, s.limpR), Mathf.Max(s.armHurtL, s.armHurtR)) * 0.7f);
            }
        }

        HumanFace face;

        static HumanClip Clip(string n) => HumanClips.Get(n);

        bool Bind()
        {
            var pelvis = rig.Bone(BodyPart.Pelvis);
            if (!pelvis) return false;
            if (bt[0] != pelvis || !curValid)
            {
                if (!ReferenceEquals(bt[0], null) && bt[0] != pelvis) pelvisBase = pelvis.localPosition.y;    // rebuilt bones (new look)
                for (int i = 0; i < N; i++) { bt[i] = rig.Bone((BodyPart)i); cur[i] = bt[i] ? bt[i].localRotation : Quaternion.identity; }
                pelCur = pelvis.localPosition - new Vector3(0f, pelvisBase, 0f);
                curValid = true;
            }
            return true;
        }

        // ------------------------------------------------------------------ blending helpers
        float wsum;
        Vector3 pelAcc;

        void Begin() { wsum = 0f; pelAcc = Vector3.zero; for (int i = 0; i < N; i++) tgt[i] = Quaternion.identity; }

        /// <summary>Weighted blend of a clip into the targets (running normalised slerp).</summary>
        void Acc(HumanClip c, float t, float w)
        {
            if (c == null || w <= 0.0005f) return;
            c.Sample(t, tmp, out var p);
            wsum += w;
            float f = w / wsum;
            for (int i = 0; i < N; i++) tgt[i] = f >= 0.9999f ? tmp[i] : Quaternion.Slerp(tgt[i], tmp[i], f);
            pelAcc = Vector3.Lerp(pelAcc, p, f);
            if (w > baseW) { baseW = w; BaseClip = c.name; }
        }

        /// <summary>A clip over the current targets with weight w (only the bones its mask covers).</summary>
        void Over(HumanClip c, float t, float w, bool pelvis)
        {
            if (c == null || w <= 0.0005f) return;
            c.Sample(t, tmp, out var p);
            for (int i = 0; i < N; i++) if (c.Affects((BodyPart)i)) tgt[i] = Quaternion.Slerp(tgt[i], tmp[i], w);
            if (pelvis && c.mask == HumanClip.Mask.Full) pelAcc = Vector3.Lerp(pelAcc, p, w);
        }

        void Post(BodyPart p, float x, float y, float z) => tgt[(int)p] = tgt[(int)p] * Quaternion.Euler(x, y, z);

        /// <summary>An action clip over the targets; a full-body one (sledge swing, stagger, dig) leaves the legs to the
        /// gait while walking, so the feet don't skate in a planted stance.</summary>
        void OverAction(HumanClip c, float t, float w, float move)
        {
            if (c.mask != HumanClip.Mask.Full || move < 0.05f) { Over(c, t, w, true); return; }
            c.Sample(t, tmp, out var p);
            float legs = w * (1f - 0.85f * move);
            for (int i = 0; i < N; i++)
                tgt[i] = Quaternion.Slerp(tgt[i], tmp[i], HumanClip.IsUpper((BodyPart)i) ? w : legs);
            pelAcc = Vector3.Lerp(pelAcc, p, legs);
        }

        static float Remap(float t, float toolHit, float clipHit)
        {
            if (toolHit <= 0f || toolHit >= 1f || clipHit <= 0f || clipHit >= 1f) return t;
            return t < toolHit ? t / toolHit * clipHit : clipHit + (t - toolHit) / (1f - toolHit) * (1f - clipHit);
        }

        // ------------------------------------------------------------------ the tick
        void TickClips(float dt, State s)
        {
            if (!Bind()) return;
            time += dt;
            rnd ??= new System.Random(rig.GetEntityId().GetHashCode());
            Begin();
            baseW = 0f; BaseClip = null;
            float move = Mathf.Clamp01(s.speed / 1.2f);
            float run = Mathf.InverseLerp(2.6f, 5.5f, s.speed);
            bool feetDown = false;

            if (s.sitting)
            {
                var c = s.riding ? Clip("ride") : s.lounging ? Clip("sit") : Clip("sit_drive");
                if (c == null) { TickProcedural(dt, s); curValid = false; return; }
                Acc(c, time / c.length, 1f);
                if (s.riding && s.pedaling && Clip("pedal") is HumanClip pc) Over(pc, s.pedal / (Mathf.PI * 2f), 1f, false);
                if (s.riding) { Post(BodyPart.Chest, 0, s.steer * 6f, 0); Post(BodyPart.ForearmL, s.steer * 10f, 0, 0); Post(BodyPart.ForearmR, -s.steer * 10f, 0, 0); }
                else if (!s.lounging)
                {
                    // hands on the wheel, turned with the steering
                    Post(BodyPart.UpperArmL, s.steer * 10f, 0, s.steer * 12f); Post(BodyPart.UpperArmR, -s.steer * 10f, 0, s.steer * 12f);
                    Post(BodyPart.Chest, 0, s.steer * 4f, 0); Post(BodyPart.Head, 0, s.steer * 8f, 0);
                }
                Post(BodyPart.Head, Mathf.Clamp(s.lookPitch * 0.5f, -25f, 30f), 0, 0);
                airT = 0f; landT = 9f; wasGrounded = true; airW = 0f;
            }
            else if (s.lying && Clip("lie") is HumanClip lie)
            {
                var lc = s.lyingSide ? Clip("lie_side") ?? lie : lie;                           // asleep: on the side
                Acc(lc, time / lc.length, 1f);
                airW = 0f;
            }
            else if (s.prone || s.groundSit)
            {
                if (!LowClips(dt, s)) { TickProcedural(dt, s); curValid = false; return; }
                airW = 0f; airT = 0f; landT = 9f; wasGrounded = true;
            }
            else
            {
                Locomotion(dt, s, move, run);
                feetDown = s.grounded && airW < 0.5f;
            }
            Overlays(dt, s, move, run);

            // write: smoothed towards the targets (state changes blend, clips stay crisp)
            bool acting = s.tool.HasValue || actW > 0.05f;
            float k = snapNext ? 1f : 1f - Mathf.Exp(-(acting ? 40f : 24f) * dt);
            snapNext = false;
            for (int i = 0; i < N; i++)
            {
                cur[i] = Quaternion.Slerp(cur[i], tgt[i], k);
                if (bt[i]) bt[i].localRotation = cur[i];
            }
            float h = pelvisBase / 0.94f;
            pelCur = Vector3.Lerp(pelCur, pelAcc * h, k);
            bt[0].localPosition = new Vector3(0f, pelvisBase, 0f) + pelCur;

            ApplyFootIK(dt, feetDown && FootIK && NearCamera(25f));
            GripIK(dt, s);
        }

        void Locomotion(float dt, State s, float move, float run)
        {
            crouchW = Mathf.MoveTowards(crouchW, s.crouching ? 1f : 0f, dt * 5f);
            float a = Mathf.Clamp01(s.speed / 1.0f), b = Mathf.InverseLerp(2.0f, 3.6f, s.speed), c = Mathf.InverseLerp(4.6f, 5.6f, s.speed);
            var sprint = Clip("sprint") ?? Clip("run");
            float stand = 1f - crouchW;
            var tiredIdle = Clip("tired"); var tiredWalk = Clip("tired_walk");
            float ti = tiredIdle != null ? tiredW : 0f, tw = tiredWalk != null ? tiredW : 0f;
            var crouchWalk = Clip("crouch_walk");
            float cmove = Mathf.Clamp01(s.speed / 0.8f);

            // stride phase: one cycle = the metres the dominant gait clip covers (speed × length, blended by weight),
            // so the planted feet don't skate; footstep events at the heel strikes (left u = 0, right u = 0.5)
            float hgt = pelvisBase / 0.94f;
            float ws = 0f, st = 0f;
            StrideOf(Clip("walk"), a * (1f - b) * stand * (1f - tw), 1.4f, ref ws, ref st);
            StrideOf(tiredWalk, a * (1f - b) * stand * tw, 1.155f, ref ws, ref st);
            StrideOf(Clip("run"), a * b * (1f - c) * stand, 2.81f, ref ws, ref st);
            StrideOf(sprint, a * b * c * stand, 3.48f, ref ws, ref st);
            StrideOf(crouchWalk, crouchW * cmove, 0.95f, ref ws, ref st);
            float stride = (ws > 0.01f ? st / ws : 1.4f) * hgt;
            float prev = phase;
            phase += s.speed / Mathf.Max(0.3f, stride) * Mathf.PI * 2f * dt;
            if (Mathf.Sign(Mathf.Sin(prev)) != Mathf.Sign(Mathf.Sin(phase)) && s.grounded && move > 0.2f) { bool left = Mathf.Sin(phase) > 0f; Footstep?.Invoke(left); HeelStrike(left, s); }
            if (phase > 1000f) phase -= Mathf.PI * 200f;
            float u = phase / (Mathf.PI * 2f);                 // the clips start at the left heel strike: the footstep events fire there
            lastStride = stride;

            // idle variants only standing at ease (not talking, aiming, acting, out of breath)
            bool busy = s.talking || s.aiming || s.action != null || tiredW > 0.5f;
            UpdateIdle(dt, s.speed < 0.15f && !busy, s.hold == null && !s.carrying && !s.tool.HasValue, busy, s);
            float wIdle = (1f - a) * stand;
            if (idlePrev != null && idleFade < 1f) { Acc(idlePrev, idlePrevT / idlePrev.length, wIdle * (1f - idleFade) * (1f - ti)); Acc(idleCur, idleT / idleCur.length, wIdle * idleFade * (1f - ti)); }
            else Acc(idleCur, idleT / idleCur.length, wIdle * (1f - ti));
            Acc(tiredIdle, time / (tiredIdle != null ? tiredIdle.length : 1f), wIdle * ti);
            Acc(Clip("walk"), u, a * (1f - b) * stand * (1f - tw));
            Acc(tiredWalk, u, a * (1f - b) * stand * tw);
            Acc(Clip("run"), u, a * b * (1f - c) * stand);
            Acc(sprint, u, a * b * c * stand);
            if (crouchW > 0f)
            {
                var ci = Clip("crouch_idle");
                Acc(ci, time / (ci != null ? ci.length : 1f), crouchW * (1f - cmove));
                Acc(crouchWalk, u, crouchW * cmove);
            }

            // air: jump (rising), fall; land on touchdown
            if (!s.grounded) airT += dt;
            else { if (!wasGrounded && airT > 0.22f) landT = 0f; airT = 0f; }
            wasGrounded = s.grounded;
            landT += dt;
            airW = Mathf.MoveTowards(airW, s.grounded ? 0f : 1f, dt * 8f);
            if (airW > 0f)
            {
                var jump = Clip("jump"); var fall = Clip("fall");
                if (s.verticalSpeed > 0.3f && jump != null && airT < jump.length) Over(jump, airT / jump.length, airW, true);
                else if (fall != null) Over(fall, time / fall.length, airW, true);
            }
            var land = Clip("land");
            if (land != null && landT < land.length && s.grounded)
            {
                float lw = 1f - landT / land.length;
                Over(land, landT / land.length, lw * lw * (1f - 0.5f * move), true);
            }

            // set off / stop / step round, lean into turns and slopes, out of breath (HumanAnimator.Feel);
            // a hurt leg swings and bends less and the hips drop on it, the head follows the aim
            FeelClips(s, move);
            Limp(BodyPart.ThighL, BodyPart.ShinL, BodyPart.FootL, s.limpL);
            Limp(BodyPart.ThighR, BodyPart.ShinR, BodyPart.FootR, s.limpR);
            float sp = Mathf.Sin(phase);
            float hitch = (s.limpL * Mathf.Max(0f, sp) + s.limpR * Mathf.Max(0f, -sp)) * 0.045f * move;
            pelAcc.y -= hitch;
            Post(BodyPart.Pelvis, 0, 0, (s.limpL - s.limpR) * 7f * move);
            Post(BodyPart.Head, Mathf.Clamp(s.lookPitch * 0.5f, -25f, 30f), 0, 0);
        }

        void Limp(BodyPart thigh, BodyPart shin, BodyPart foot, float limp)
        {
            if (limp <= 0f) return;
            tgt[(int)thigh] = Quaternion.Slerp(tgt[(int)thigh], Quaternion.identity, 0.6f * limp);
            tgt[(int)shin] = Quaternion.Slerp(tgt[(int)shin], Quaternion.Euler(4f, 0, 0), 0.55f * limp);
            tgt[(int)foot] = Quaternion.Slerp(tgt[(int)foot], Quaternion.identity, 0.5f * limp);
        }

        static readonly string[] IdleVariants = { "idle_shift", "idle_look" };

        void UpdateIdle(float dt, bool idle, bool freeHands, bool busy, State s)
        {
            idleT += dt; idlePrevT += dt;
            idleFade = Mathf.MoveTowards(idleFade, 1f, dt / 0.8f);
            if (idleCur == null) { idleCur = Clip("idle"); idleT = 0f; }
            if (!idle)
            {
                idleNext = Mathf.Max(idleNext, idleT + 4f);
                // talking, aiming, acting: a variant in progress gives way to plain breathing
                if (busy && forcedIdle == null && idleCur != null && idleCur.name != "idle" && idleFade >= 1f && Clip("idle") is HumanClip back)
                { idlePrev = idleCur; idlePrevT = idleT; idleCur = back; idleT = 0f; idleFade = 0f; }
                if (forcedIdle == null) return;
            }
            if (idleT < idleNext || idleFade < 1f) return;
            // a variant now and then (every 6-16 s), picked by context: hot, cold, carrying a pack, else shift the
            // weight, look around, scratch; with empty hands also a stretch, a rub of the neck, hands on the hips
            string next = forcedIdle ?? (idleCur.name != "idle" ? "idle" : PickIdle(s, freeHands));
            forcedIdle = null;
            var c = Clip(next);
            if (c == null) { idleNext = idleT + 8f; return; }
            idlePrev = idleCur; idlePrevT = idleT;
            idleCur = c; idleT = 0f; idleFade = 0f;
            idleNext = c.name == "idle" ? 6f + (float)rnd.NextDouble() * 10f : c.length * (0.6f + (float)rnd.NextDouble() * 0.4f);
        }

        void Overlays(float dt, State s, float move, float run)
        {
            // action clip (tool swing, work, parkour, death): over the bones its mask covers
            var want = s.action != null ? Clip(s.action) : null;
            if (want != null)
            {
                if (actClip != want) { actClip = want; actW = Mathf.Min(actW, 0.3f); }
                actW = snapNext ? 1f : Mathf.MoveTowards(actW, 1f, dt * 10f);
                actT = s.actionT >= 0f ? Remap(s.actionT, s.actionHit, want.hit) : time / want.length;
            }
            else
            {
                actW = Mathf.MoveTowards(actW, 0f, dt * 7f);
                if (actClip != null && actClip.loop) actT += dt / actClip.length;
            }
            bool clipAction = actClip != null && actW > 0f;
            Holding(dt, s, move);
            if (clipAction) OverAction(actClip, actT, actW, move);

            // a tool pose without a clip: the procedural ToolPose (right arm, left arm when two-handed, chest, knees)
            if (s.tool.HasValue && want == null && !s.sitting)
            {
                var tp = s.tool.Value;
                tgt[(int)BodyPart.UpperArmR] = Quaternion.Euler(tp.armRX, tp.armRY, tp.armRZ);
                tgt[(int)BodyPart.ForearmR] = Quaternion.Euler(tp.foreR, 0, 0);
                tgt[(int)BodyPart.HandR] = Quaternion.Euler(0, 0, tp.handRZ);
                if (s.twoHanded || tp.armLX != 0f)
                {
                    tgt[(int)BodyPart.UpperArmL] = Quaternion.Euler(tp.armLX, tp.armLY, tp.armLZ);
                    tgt[(int)BodyPart.ForearmL] = Quaternion.Euler(tp.foreL, 0, 0);
                }
                tgt[(int)BodyPart.Chest] = Quaternion.Euler(run * 10f + tp.chestX, tp.chestY, 0);
                if (tp.knees > 0f && !s.crouching)
                {
                    Post(BodyPart.ThighL, -tp.knees * 0.6f, 0, 0); Post(BodyPart.ThighR, -tp.knees * 0.6f, 0, 0);
                    Post(BodyPart.ShinL, tp.knees, 0, 0); Post(BodyPart.ShinR, tp.knees, 0, 0);
                    Post(BodyPart.FootL, -tp.knees * 0.4f, 0, 0); Post(BodyPart.FootR, -tp.knees * 0.4f, 0, 0);
                    if (s.grounded)
                    {
                        float kr = tp.knees * Mathf.Deg2Rad;
                        pelAcc.y -= 0.94f * (1f - 0.5f * (Mathf.Cos(kr * 0.6f) + Mathf.Cos(kr * 0.4f)));
                    }
                }
            }

            // carrying, aiming, talking: upper-body loops faded in and out
            bool free = !s.tool.HasValue && !clipAction && !s.sitting && !s.lying;
            carryW = Mathf.MoveTowards(carryW, s.carrying && !s.sitting ? 1f : 0f, dt * 6f);
            aimW = Mathf.MoveTowards(aimW, s.aiming && !s.tool.HasValue && !s.sitting ? 1f : 0f, dt * 8f);
            talkW = Mathf.MoveTowards(talkW, s.talking && free && move < 0.3f ? 0.8f : 0f, dt * 3f);
            if (carryW > 0f) Over(Clip("carry"), time / 1f, carryW, false);
            if (aimW > 0f) Over(Clip("aim"), time / 3f, aimW, false);
            if (talkW > 0f) { var tc = Clip(((int)(time / 9f) & 1) == 0 ? "talk_a" : "talk_b"); if (tc != null) Over(tc, time / tc.length, talkW, false); }

            // a badly hurt arm is held against the chest, a sore one swings less
            HurtArm(s.armHurtL, BodyPart.UpperArmL, BodyPart.ForearmL, -1f, s.tool.HasValue || clipAction);
            HurtArm(s.armHurtR, BodyPart.UpperArmR, BodyPart.ForearmR, 1f, s.tool.HasValue || clipAction);
        }

        void HurtArm(float hurt, BodyPart upper, BodyPart fore, float side, bool busy)
        {
            if (hurt <= 0f) return;
            if (hurt > 0.6f) { tgt[(int)upper] = Quaternion.Euler(-22f, 0, 18f * side); tgt[(int)fore] = Quaternion.Euler(-100f, 0, 0); }
            else if (!busy) tgt[(int)upper] = Quaternion.Slerp(tgt[(int)upper], Quaternion.Euler(0, 0, 6f * side), 0.5f * hurt);
        }

        // ------------------------------------------------------------------ feet on the ground
        bool NearCamera(float range)
        {
            var cam = Camera.main;
            return cam && (cam.transform.position - rig.transform.position).sqrMagnitude < range * range;
        }

        static readonly RaycastHit[] hits = new RaycastHit[8];

        bool Probe(Transform foot, out float offset, out Vector3 normal)
        {
            offset = 0f; normal = Vector3.up;
            var root = rig.transform;
            var p = foot.position;
            float baseY = root.position.y;
            int n = Physics.RaycastNonAlloc(new Vector3(p.x, baseY + 0.6f, p.z), Vector3.down, hits, 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool ok = false;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.transform.IsChildOf(root) || h.rigidbody && !h.rigidbody.isKinematic && h.rigidbody.mass < 50f) continue;
                if (h.distance < best) { best = h.distance; offset = h.point.y - baseY; normal = h.normal; ok = true; }
            }
            offset = Mathf.Clamp(offset, -0.35f, 0.3f);
            return ok;
        }

        Vector3 nL = Vector3.up, nR = Vector3.up;

        /// <summary>Two-bone leg IK: each foot to the ground under it (relative to the body's own floor), the hips lowered
        /// for the lower foot, soles tilted to the slope.</summary>
        void ApplyFootIK(float dt, bool active)
        {
            float k = 1f - Mathf.Exp(-14f * dt);
            float tl = 0f, tr = 0f; Vector3 gl = Vector3.up, gr = Vector3.up;
            if (active)
            {
                if (!Probe(bt[(int)BodyPart.FootL], out tl, out gl)) tl = 0f;
                if (!Probe(bt[(int)BodyPart.FootR], out tr, out gr)) tr = 0f;
            }
            ikL = Mathf.Lerp(ikL, tl, k); ikR = Mathf.Lerp(ikR, tr, k);
            nL = Vector3.Slerp(nL, gl, k); nR = Vector3.Slerp(nR, gr, k);
            ikPelvis = Mathf.Lerp(ikPelvis, Mathf.Min(0f, Mathf.Min(tl, tr)), k);
            if (Mathf.Abs(ikL) < 0.003f && Mathf.Abs(ikR) < 0.003f && Mathf.Abs(ikPelvis) < 0.003f) return;
            var up = Vector3.up;
            var fl = bt[(int)BodyPart.FootL]; var fr = bt[(int)BodyPart.FootR];
            Vector3 wantL = fl.position + up * ikL, wantR = fr.position + up * ikR;
            Quaternion rotL = fl.rotation, rotR = fr.rotation;
            bt[0].position += up * ikPelvis;
            Solve(bt[(int)BodyPart.ThighL], bt[(int)BodyPart.ShinL], fl, wantL);
            Solve(bt[(int)BodyPart.ThighR], bt[(int)BodyPart.ShinR], fr, wantR);
            fl.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(up, nL), 0.7f) * rotL;
            fr.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(up, nR), 0.7f) * rotR;
        }

        static void Solve(Transform a, Transform b, Transform c, Vector3 t)
        {
            Vector3 A = a.position, B = b.position, C = c.position;
            float lab = (B - A).magnitude, lcb = (C - B).magnitude;
            if (lab < 1e-4f || lcb < 1e-4f) return;
            float lat = Mathf.Clamp((t - A).magnitude, 0.01f, lab + lcb - 0.002f);
            float Ang(Vector3 x, Vector3 y) => Mathf.Acos(Mathf.Clamp(Vector3.Dot(x.normalized, y.normalized), -1f, 1f));
            float acab0 = Ang(C - A, B - A), babc0 = Ang(A - B, C - B), acat0 = Ang(C - A, t - A);
            float acab1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float babc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));
            var axis0 = Vector3.Cross(C - A, B - A);
            if (axis0.sqrMagnitude < 1e-8f) axis0 = -(a.rotation * Vector3.right);   // straight leg: the knee bends forward
            axis0.Normalize();
            var axis1 = Vector3.Cross(C - A, t - A);
            var ia = Quaternion.Inverse(a.rotation); var ib = Quaternion.Inverse(b.rotation);
            var r0 = Quaternion.AngleAxis((acab1 - acab0) * Mathf.Rad2Deg, ia * axis0);
            var r1 = Quaternion.AngleAxis((babc1 - babc0) * Mathf.Rad2Deg, ib * axis0);
            var r2 = axis1.sqrMagnitude > 1e-10f ? Quaternion.AngleAxis(acat0 * Mathf.Rad2Deg, ia * axis1.normalized) : Quaternion.identity;
            a.localRotation = a.localRotation * (r0 * r2);
            b.localRotation = b.localRotation * r1;
        }

        /// <summary>Tests: play the hold's fidget now / switch to an idle variant now.</summary>
        public void ForceFidget() { fidgetNext = 0f; }
        public void ForceIdle(string variant) { forcedIdle = variant; idleNext = 0f; idleFade = 1f; }
        string forcedIdle;
        /// <summary>The fidget playing (null = none) and the idle variant.</summary>
        public string Fidget => fidgetT >= 0f && fidgetClip != null ? fidgetClip.name : null;
        public string IdleClip => idleCur != null ? idleCur.name : null;

        // ------------------------------------------------------------------ carrying a tool at rest
        /// <summary>The hold overlay (arms only, so breathing / shifting / looking around stay) faded out under actions,
        /// aiming and carrying; standing still with it, its fidget plays every 7-16 s (looks the tool over, shifts it).</summary>
        void Holding(float dt, State s, float move)
        {
            var want = s.hold != null && !s.sitting && !s.lying && !s.carrying ? Clip(s.hold) : null;
            if (want != null) holdClip = want;
            float target = want != null ? (1f - actW) * (1f - aimW) : 0f;
            holdW = Mathf.MoveTowards(holdW, target, dt * 6f);
            if (holdClip == null || holdW <= 0.001f) { fidgetT = -1f; return; }
            Over(holdClip, time / holdClip.length, holdW, false);

            bool still = move < 0.1f && actW < 0.05f && aimW < 0.05f && !s.talking && want != null;
            if (fidgetT < 0f)
            {
                if (!still) { fidgetNext = Mathf.Max(fidgetNext, 3f); return; }
                if ((fidgetNext -= dt) > 0f) return;
                fidgetClip = Clip(holdClip.name + "_fidget");
                fidgetNext = 7f + (float)rnd.NextDouble() * 9f;
                if (fidgetClip == null) return;
                fidgetT = 0f;
            }
            if (fidgetClip == null) { fidgetT = -1f; return; }
            fidgetT += dt / fidgetClip.length;
            if (fidgetT >= 1f || !still) { fidgetT = -1f; return; }
            float env = Mathf.Clamp01(Mathf.Min(fidgetT, 1f - fidgetT) / 0.12f);    // ease in and out
            Over(fidgetClip, fidgetT, holdW * env, false);
        }

        // ------------------------------------------------------------------ second hand on the handle
        float gripW;

        /// <summary>Two-bone arm IK: the left hand onto the handle of a tool held in both hands, at the point of the handle
        /// nearest to where the pose put it (between 0.1 and 0.45 tool units above the right hand), so a swing reads as one
        /// body moving one tool. Fades in and out; skipped with the left arm hurt or gone.</summary>
        void GripIK(float dt, State s)
        {
            bool on = s.grip2 && s.grip2.gameObject.activeInHierarchy && !s.sitting && !s.lying && !s.carrying && s.armHurtL <= 0.6f
                      && !rig.BoneGone(BodyPart.HandL) && !rig.BoneGone(BodyPart.ForearmL);
            gripW = Mathf.MoveTowards(gripW, on ? 1f : 0f, dt * 8f);
            if (gripW <= 0.001f || !s.grip2) return;
            var ua = rig.Bone(BodyPart.UpperArmL); var fa = rig.Bone(BodyPart.ForearmL); var hand = rig.Bone(BodyPart.HandL);
            if (!ua || !fa || !hand) return;
            var tool = s.grip2;
            var axis = tool.TransformVector(Vector3.down);
            float unit = axis.magnitude;
            if (unit < 1e-4f) return;
            axis /= unit;
            Quaternion ua0 = ua.localRotation, fa0 = fa.localRotation;
            for (int pass = 0; pass < 2; pass++)
            {
                var palm = hand.TransformVector(new Vector3(0f, -0.055f, 0f));
                float u = Mathf.Clamp(Vector3.Dot(hand.position + palm - tool.position, axis), 0.1f * unit, 0.45f * unit);
                Solve(ua, fa, hand, tool.position + axis * u - palm);
            }
            if (gripW < 1f)
            {
                ua.localRotation = Quaternion.Slerp(ua0, ua.localRotation, gripW);
                fa.localRotation = Quaternion.Slerp(fa0, fa.localRotation, gripW);
            }
        }

        // ------------------------------------------------------------------ look at
        Vector3 lookLag;
        float nodW;
        Transform addChest, addHead;
        Quaternion chestAdd = Quaternion.identity, headAdd = Quaternion.identity;

        /// <summary>Turns toward the look target: the chest takes a quarter of the turn, the head about two thirds and
        /// lags a little behind (HumanFace's eyes get there first and cover the rest).</summary>
        void ApplyLookAt(float dt, Vector3? target, State s)
        {
            if (target.HasValue) lastLook = target.Value;
            if (lookW <= 0.001f) lookLag = lastLook;
            lookW = Mathf.MoveTowards(lookW, target.HasValue ? 1f : 0f, dt * 3f);
            if (lookW <= 0.001f) return;
            var head = rig.Bone(BodyPart.Head); var chest = rig.Bone(BodyPart.Chest); var pelvis = rig.Bone(BodyPart.Pelvis);
            if (!head || !chest || !pelvis) return;
            lookLag = Vector3.Lerp(lookLag, lastLook, 1f - Mathf.Exp(-7f * dt));
            var dp = Quaternion.Inverse(pelvis.rotation) * (lookLag - head.position);
            float yawP = Mathf.Atan2(dp.x, dp.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(yawP) > 115f) return;                                  // behind: don't wring the neck
            float pitchP = -Mathf.Atan2(dp.y, new Vector2(dp.x, dp.z).magnitude) * Mathf.Rad2Deg;
            // the chest, unless the arms are busy aiming or swinging
            float cw = lookW * (1f - Mathf.Max(aimW, actW)) * (!s.sitting || s.lounging ? 1f : s.riding ? 0.4f : 0f);   // hands on a wheel stay on it
            if (cw > 0.001f)
            {
                var add = Quaternion.Euler(Mathf.Clamp(pitchP * 0.15f, -6f, 8f), Mathf.Clamp(yawP * 0.25f, -25f, 25f), 0f);
                chestAdd = Quaternion.Slerp(Quaternion.identity, add, cw);
                chest.localRotation *= chestAdd;
                addChest = chest;
            }
            var d = Quaternion.Inverse(chest.rotation) * (lookLag - head.position);
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            var want = Quaternion.Euler(Mathf.Clamp(pitch * 0.85f, -30f, 35f), Mathf.Clamp(yaw * 0.87f, -70f, 70f), 0f);
            head.localRotation = Quaternion.Slerp(head.localRotation, want, lookW * 0.9f);
        }

        /// <summary>A blow's direction over the flinch / stagger clip: the chest gives way from the side it came from (back
        /// from the front, forward from behind, bent away and twisted from a side), the head snaps with it.</summary>
        void HitLean(State s)
        {
            float m = s.hitFrom.magnitude;
            if (s.hitT <= 0f || s.hitT >= 1f || m < 0.05f) return;
            var chest = rig.Bone(BodyPart.Chest); var head = rig.Bone(BodyPart.Head);
            if (!chest) return;
            float w = Mathf.Clamp01(s.hitT * 8f) * (1f - s.hitT) * (1f - s.hitT) * Mathf.Min(m, 1.6f);
            var f = new Vector3(s.hitFrom.x, 0f, s.hitFrom.z).normalized;
            var add = Quaternion.Euler(-f.z * 16f * w, f.x * 14f * w, f.x * 11f * w);
            chest.localRotation *= add;
            chestAdd = addChest == chest ? chestAdd * add : add;
            addChest = chest;
            if (!head) return;
            var hadd = Quaternion.Euler(-f.z * 12f * w, f.x * 18f * w, f.x * 7f * w);
            head.localRotation *= hadd;
            headAdd = addHead == head ? headAdd * hadd : hadd;
            addHead = head;
        }

        /// <summary>Talking: small nods in bursts with the phrasing, a brow-raising lift of the chin now and then, a slow
        /// tilt (Head bone only, over whatever the talk_a/b overlay and the look-at set).</summary>
        void TalkNods(float dt, State s)
        {
            nodW = Mathf.MoveTowards(nodW, s.talking && !s.lying ? 1f : 0f, dt * 2.5f);
            if (nodW <= 0.001f) return;
            var head = rig.Bone(BodyPart.Head);
            if (!head) return;
            float t = time;
            float burst = Mathf.Max(0f, Mathf.Sin(t * 0.93f + 0.4f));
            float nod = Mathf.Sin(t * 12.6f) * 2.4f * burst * burst;
            float ph = Mathf.Repeat(t, 3.7f) / 0.55f;                             // the "eyebrows up" beat
            float lift = ph < 1f ? -4.5f * Mathf.Sin(ph * Mathf.PI) : 0f;
            headAdd = Quaternion.Euler((nod + lift) * nodW, Mathf.Sin(t * 0.53f) * 2f * nodW, Mathf.Sin(t * 0.71f) * 2.5f * nodW);
            head.localRotation *= headAdd;
            addHead = head;
        }
    }
}
