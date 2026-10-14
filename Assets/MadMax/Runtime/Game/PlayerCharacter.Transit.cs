using System.Collections.Generic;
using MadMax.Building;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Getting onto and off things with the body (setting WORK ANIMATION): sitting down walks to the front of the
    /// seat, turns and lowers onto it (clip sit_down), standing up leans forward and pushes off (stand_up); beds and clinic
    /// beds sit on the edge and lie back while the legs swing up (lie_back), getting up is the reverse; standing perches
    /// (watchtower, roof steps) climb up the ladder and step over, and climb back down; saddles take a hop. The logical
    /// state (<see cref="Sitting"/>, <see cref="SeatedOn"/>) changes at once and the body follows over 0.4-1.5 s.
    /// Teleport, getting into a vehicle or an interior ends it on the spot; moving while sitting down stands back up.</summary>
    public partial class PlayerCharacter
    {
        enum LegMode { Walk, Sit, Stand, Climb, Step, Hop, LieBack, GetUp, Hold }

        struct Leg
        {
            public LegMode mode;
            public float dur;
            public Vector3 a, b;                 // root (feet) positions, in the transit space
            public Quaternion ra, rb;            // root rotations, in the transit space
        }

        readonly List<Leg> legs = new List<Leg>();
        int legAt;
        float legT;
        Transform transitSpace;                  // seat / bed the positions are kept in (moves with vehicles); null = world
        System.Action transitDone;
        bool transitStanding;                    // the transit ends standing (stand up, get up): the caller's end teleports
        bool transitBed;                         // lying on a bed (not a seat): get up when the hold ends or on a move
        bool savedFootIK = true;
        static readonly Quaternion[] clipTmp = new Quaternion[HumanClip.Bones];

        // lying on a clinic bed / surgery table seat, or on a bed for a night
        bool seatLie;
        Vector3 lieRootLocal; Quaternion lieRotLocal = Quaternion.identity;

        /// <summary>The body is moving onto or off something (sitting down, standing up, lying down, climbing).</summary>
        public bool InTransit => legAt < legs.Count;
        /// <summary>Seconds the current transition still takes (0 = none).</summary>
        public float TransitLeft { get { float s = 0f; for (int i = legAt; i < legs.Count; i++) s += legs[i].dur; return Mathf.Max(0f, s - legT); } }
        /// <summary>The leg of the transition playing (Walk, Sit, Stand, Climb, Step, Hop, LieBack, GetUp, Hold), null = none.</summary>
        public string TransitLeg => InTransit ? legs[legAt].mode.ToString() : null;
        /// <summary>Lying down (on a bed, a clinic bed, a surgery table), the transition onto it finished.</summary>
        public bool Lying => !InTransit && (Sitting && seatLie || bedLie != null) || (InTransit && legs[legAt].mode == LegMode.Hold);
        /// <summary>The bed the player lies on for a night (<see cref="LieOn"/>), null = none.</summary>
        public Transform LyingOn => bedLie;
        /// <summary>Anything the player can't break off from with a key press this moment (transition, lying in a bed).</summary>
        public bool BodyBusy => InTransit || bedLie != null;

        Transform bedLie;
        System.Action bedAction;
        float bedStay;

        static bool Animated => GameSettings.Current == null || GameSettings.Current.workAnimation;

        /// <summary>A seat the player lies on (clinic bed, surgery table) instead of sitting.</summary>
        public static bool IsLieSeat(Seat seat) => seat && (seat.GetComponent<ClinicBed>() || seat.GetComponent<SurgeryTable>());

        float H => Rig ? Rig.appearance.height : 1f;

        // ------------------------------------------------------------------ building the paths

        void ClearTransit(bool restore = true)
        {
            legs.Clear(); legAt = 0; legT = 0f;
            transitSpace = null; transitDone = null; transitStanding = false;
            if (restore && anim != null) anim.FootIK = savedFootIK;
        }

        Vector3 ToSpace(Vector3 w) => transitSpace ? transitSpace.InverseTransformPoint(w) : w;
        Quaternion ToSpace(Quaternion w) => transitSpace ? Quaternion.Inverse(transitSpace.rotation) * w : w;
        Vector3 FromSpace(Vector3 p) => transitSpace ? transitSpace.TransformPoint(p) : p;
        Quaternion FromSpace(Quaternion r) => transitSpace ? transitSpace.rotation * r : r;
        Vector3 SpaceUp => transitSpace ? transitSpace.up : Vector3.up;

        void AddLeg(LegMode mode, float dur, Vector3 aWorld, Vector3 bWorld, Quaternion raWorld, Quaternion rbWorld)
            => legs.Add(new Leg { mode = mode, dur = Mathf.Max(0.05f, dur), a = ToSpace(aWorld), b = ToSpace(bWorld), ra = ToSpace(raWorld), rb = ToSpace(rbWorld) });

        static Quaternion Facing(Vector3 dir, Vector3 up, Quaternion fallback)
        {
            dir = Vector3.ProjectOnPlane(dir, up);
            return dir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(dir.normalized, up) : fallback;
        }

        /// <summary>A short walk from where the player stands to <paramref name="to"/> (skipped when already there).</summary>
        Vector3 WalkTo(Vector3 from, Vector3 to, ref Quaternion rot, float maxTime = 0.9f)
        {
            var flat = Vector3.ProjectOnPlane(to - from, SpaceUp);
            if (flat.magnitude < 0.3f) return from;
            var face = Facing(flat, SpaceUp, rot);
            AddLeg(LegMode.Walk, Mathf.Clamp(flat.magnitude / walkSpeed, 0.15f, maxTime), from, to, rot, face);
            rot = face;
            return to;
        }

        void StartTransit(Transform space)
        {
            if (!InTransit) savedFootIK = anim != null ? anim.FootIK : true;
            legs.Clear(); legAt = 0; legT = 0f;
            transitSpace = space; transitDone = null; transitStanding = false;
            cc.enabled = false;
            transform.SetParent(null, true);
            swingT = -1f; vy = 0f; Velocity = Vector3.zero;
        }

        /// <summary>Paths onto a seat (called by <see cref="SitOn"/> once the logical state is set).</summary>
        bool BeginSit(Seat seat, Vector3 from, Quaternion fromRot)
        {
            if (!Animated || !seat || anim == null || Ragdolled) return false;
            var t = seat.transform;
            float h = H;
            StartTransit(t);
            var up = t.up;
            var rot = fromRot;
            var hip = t.TransformPoint(seatSpot);
            if (seatLie)
            {
                // to the side of the bed, sit on its edge, then lie back with the legs swinging up
                if (!BedFrame(t, from, h, out var side, out var edgeRoot, out var edgeRot, out var lieRoot, out var lieRot)) { ClearTransit(); return false; }
                lieRootLocal = t.InverseTransformPoint(lieRoot); lieRotLocal = Quaternion.Inverse(t.rotation) * lieRot;
                var at = WalkTo(from, side, ref rot);
                AddLeg(LegMode.Sit, 0.6f, at, edgeRoot, rot, edgeRot);
                AddLeg(LegMode.LieBack, 0.8f, edgeRoot, lieRoot, edgeRot, lieRot);
                return true;
            }
            var root = hip + Vector3.down * (0.94f * h);
            var seatRot = seat.standing ? Facing(hip - from, up, transform.rotation) : t.rotation;
            if (seat.ride != null)
            {
                // a saddle: a hop up from beside it
                AddLeg(LegMode.Hop, 0.4f, from, root, rot, t.rotation);
                return true;
            }
            if (seat.standing)
            {
                // a perch up a ladder (watchtower, roof steps): to the foot, climb, step over onto it
                var foot = seat.hasExit ? seat.StandPoint(seatSpot, 0) : from;
                float rise = Vector3.Dot(root - foot, up);
                if (rise < 0.8f) { AddLeg(LegMode.Step, 0.35f, from, root, rot, seatRot); return true; }
                var face = Facing(root - foot, up, rot);
                var at = WalkTo(from, foot, ref rot);
                var top = foot + up * rise;
                AddLeg(LegMode.Climb, Mathf.Clamp(rise / 2.6f, 0.4f, 1.4f), at, top, face, face);
                AddLeg(LegMode.Step, 0.35f, top, root, face, Facing(root - top, up, face));
                return true;
            }
            // a chair, a bench, a passenger seat: to its front (the door), turn and sit down
            var front = seat.StandPoint(seatSpot, 0);
            var start = WalkTo(from, front, ref rot);
            AddLeg(LegMode.Sit, seat.hasExit ? 0.7f : 0.6f, start, root, rot, t.rotation);
            return true;
        }

        /// <summary>Paths off a seat (called by <see cref="StandUp"/>); the end puts the player at <paramref name="at"/>.</summary>
        bool BeginStand(Seat seat, Vector3 at)
        {
            if (!Animated || !seat || anim == null || Ragdolled) return false;
            var t = seat.transform;
            var from = transform.position; var rot = transform.rotation;
            bool wasLying = seatLie;
            StartTransit(t);
            var up = t.up;
            var end = Facing(at - from, up, rot);
            if (wasLying)
            {
                if (!BedFrame(t, at, H, out _, out var edgeRoot, out var edgeRot, out _, out _)) { ClearTransit(); return false; }
                AddLeg(LegMode.GetUp, 0.7f, from, edgeRoot, rot, edgeRot);
                AddLeg(LegMode.Stand, 0.5f, edgeRoot, at, edgeRot, edgeRot);
            }
            else if (seat.ride != null) AddLeg(LegMode.Hop, 0.35f, from, at, rot, rot);
            else if (seat.standing)
            {
                float drop = Vector3.Dot(from - at, up);
                if (drop < 0.8f) AddLeg(LegMode.Step, 0.35f, from, at, rot, end);
                else
                {
                    // back to the ladder top, climb down facing it
                    var top = at + up * drop;
                    var face = Facing(from - at, up, rot);
                    AddLeg(LegMode.Step, 0.35f, from, top, rot, face);
                    AddLeg(LegMode.Climb, Mathf.Clamp(drop / 2.8f, 0.4f, 1.3f), top, at, face, face);
                }
            }
            else AddLeg(LegMode.Stand, seat.hasExit ? 0.6f : 0.5f, from, at, rot, rot);
            transitStanding = true;
            return true;
        }

        /// <summary>Lie down on a bed for <paramref name="stay"/> seconds: to its side, sit on the edge, lie back; then
        /// <paramref name="lying"/> runs (the night's sleep) and the player gets up again by the bed. False = can't now
        /// (the caller does it at once).</summary>
        public bool LieOn(Transform bed, System.Action lying, float stay = 1.6f)
        {
            if (!Animated || !bed || anim == null || Ragdolled || Sitting || SeatedIn || Interior || Traversing || Climbing || Carried || BodyBusy || Swimming) return false;
            var from = transform.position;
            if (!BedFrame(bed, from, H, out var side, out var edgeRoot, out var edgeRot, out var lieRoot, out var lieRot)) return false;
            StartTransit(bed);
            var rot = transform.rotation;
            var at = WalkTo(from, side, ref rot, 1.2f);
            AddLeg(LegMode.Sit, 0.6f, at, edgeRoot, rot, edgeRot);
            AddLeg(LegMode.LieBack, 0.8f, edgeRoot, lieRoot, edgeRot, lieRot);
            bedLie = bed; bedAction = lying; bedStay = stay;
            lieRootLocal = bed.InverseTransformPoint(lieRoot); lieRotLocal = Quaternion.Inverse(bed.rotation) * lieRot;
            transitDone = () => { var a = bedAction; bedAction = null; a?.Invoke(); HoldInBed(); };
            return true;
        }

        void HoldInBed()
        {
            if (!bedLie) { bedLie = null; return; }
            var t = bedLie;
            StartTransit(t);
            var root = t.TransformPoint(lieRootLocal); var rot = t.rotation * lieRotLocal;
            AddLeg(LegMode.Hold, bedStay, root, root, rot, rot);
            transitDone = GetUpFromBed;
        }

        /// <summary>Off a bike's saddle: from <paramref name="seat"/> (where the rider sat, world) down to where the player
        /// stands now (after the exit put them there), a short hop.</summary>
        public void HopFrom(Vector3 seat, Quaternion seatRot, float dur = 0.3f)
        {
            if (!Animated || anim == null || Ragdolled || Sitting || SeatedIn || Interior) return;
            var to = transform.position; var rot = transform.rotation;
            StartTransit(null);
            AddLeg(LegMode.Hop, dur, seat, to, seatRot, rot);
            transitStanding = true; standAt = to; standSpace = null;
        }

        /// <summary>Out of the bed (the hold ended or the player moved): the reverse, standing beside it.</summary>
        public void GetUpFromBed()
        {
            var bed = bedLie;
            bedLie = null; bedAction = null;
            if (!bed) { ClearTransit(); if (!cc.enabled && !Sitting && !SeatedIn && !Interior) { cc.enabled = true; } return; }
            var from = transform.position; var rot = transform.rotation;
            if (!BedFrame(bed, from, H, out var side, out var edgeRoot, out var edgeRot, out _, out _)) { ClearTransit(); Teleport(from + Vector3.up * 0.3f, transform.eulerAngles.y); return; }
            StartTransit(bed);
            AddLeg(LegMode.GetUp, 0.7f, from, edgeRoot, rot, edgeRot);
            AddLeg(LegMode.Stand, 0.5f, edgeRoot, side, edgeRot, edgeRot);
            transitStanding = true;
            var end = side;
            transitDone = () => Teleport(end, transform.eulerAngles.y);
        }

        // ------------------------------------------------------------------ the bed's shape

        static readonly RaycastHit[] bedHits = new RaycastHit[16];
        static readonly List<MeshFilter> bedMeshes = new List<MeshFilter>();

        /// <summary>Where to lie on a bed-like piece (bed-local extent of its meshes): the long axis, the head at the end
        /// with the higher top (headboard), the mattress top under its middle. <paramref name="side"/> = standing beside it
        /// on the side nearer <paramref name="near"/>; <paramref name="edgeRoot"/> sitting on that edge facing out;
        /// <paramref name="lieRoot"/> lying on the back, feet towards the foot end.</summary>
        public static bool BedFrame(Transform bed, Vector3 near, float h, out Vector3 side, out Vector3 edgeRoot, out Quaternion edgeRot, out Vector3 lieRoot, out Quaternion lieRot)
        {
            side = edgeRoot = lieRoot = bed ? bed.position : Vector3.zero; edgeRot = lieRot = Quaternion.identity;
            if (!bed) return false;
            var inv = bed.worldToLocalMatrix;
            var b = new Bounds(); bool any = false;
            bed.GetComponentsInChildren(false, bedMeshes);
            foreach (var mf in bedMeshes)
            {
                if (!mf.sharedMesh || mf.GetComponentInParent<PlayerCharacter>()) continue;
                var mb = mf.sharedMesh.bounds; var m = inv * mf.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) != 0 ? 1f : -1f, (k & 2) != 0 ? 1f : -1f, (k & 4) != 0 ? 1f : -1f)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            bedMeshes.Clear();
            if (!any) b = new Bounds(new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 1.9f));
            bool alongZ = b.size.z >= b.size.x;
            Vector3 axis = alongZ ? Vector3.forward : Vector3.right, perp = alongZ ? Vector3.right : Vector3.forward;
            float half = (alongZ ? b.extents.z : b.extents.x), width = (alongZ ? b.extents.x : b.extents.z);
            var c = b.center; c.y = 0f;
            float topMid = Top(bed, c, b), topA = Top(bed, c + axis * (half - 0.1f), b), topB = Top(bed, c - axis * (half - 0.1f), b);
            float head = topA >= topB - 0.02f ? 1f : -1f;                                          // the headboard end
            float mattress = topMid;
            var nl = bed.InverseTransformPoint(near);
            float s = Vector3.Dot(nl - c, perp) >= 0f ? 1f : -1f;
            float pel = Mathf.Clamp(half - 0.92f * h, -half + 0.4f, half - 0.4f) * head;          // pelvis along the axis from the centre
            var up = bed.up;
            var feetDir = bed.TransformDirection(-axis * head);
            var outDir = bed.TransformDirection(perp * s);
            lieRoot = bed.TransformPoint(c + axis * pel + Vector3.up * (mattress - 0.02f));
            lieRot = Quaternion.LookRotation(feetDir, up);
            var hip = bed.TransformPoint(c + axis * pel + perp * s * Mathf.Max(0.05f, width - 0.2f) + Vector3.up * (mattress + 0.08f));
            edgeRoot = hip - up * (0.94f * h);
            edgeRot = Quaternion.LookRotation(outDir, up);
            var stand = bed.TransformPoint(c + axis * (pel - head * 0.2f) + perp * s * (width + 0.45f) + Vector3.up * (b.min.y + 0.02f));
            side = Floor(stand, bed, stand.y);
            return true;
        }

        /// <summary>Highest point of the bed's own colliders at <paramref name="local"/> (bed-local y), else 45 % up its extent.</summary>
        static float Top(Transform bed, Vector3 local, Bounds b)
        {
            var from = bed.TransformPoint(new Vector3(local.x, b.max.y + 0.5f, local.z));
            int n = Physics.RaycastNonAlloc(from, -bed.up, bedHits, b.size.y + 1f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; float y = Mathf.Lerp(b.min.y, b.max.y, 0.45f);
            for (int i = 0; i < n; i++)
                if (bedHits[i].collider.transform.IsChildOf(bed) && bedHits[i].distance < best) { best = bedHits[i].distance; y = bed.InverseTransformPoint(bedHits[i].point).y; }
            return y;
        }

        /// <summary>The walkable surface under <paramref name="p"/> (not <paramref name="skip"/>, not the player).</summary>
        static Vector3 Floor(Vector3 p, Transform skip, float fallback)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 1.2f, Vector3.down, bedHits, 4f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; float y = fallback;
            for (int i = 0; i < n; i++)
            {
                var col = bedHits[i].collider;
                if ((skip && col.transform.IsChildOf(skip)) || col.GetComponentInParent<PlayerCharacter>()) continue;
                if (bedHits[i].distance < best) { best = bedHits[i].distance; y = bedHits[i].point.y; }
            }
            var t = MadMax.World.DeformableTerrain.Instance;
            if (best == float.MaxValue && t) y = Mathf.Max(fallback, t.Height(p.x, p.z));
            return new Vector3(p.x, y, p.z);
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>One frame of the transition (Update skips the rest while it runs).</summary>
        void TransitTick(float dt)
        {
            if ((Sitting && !SeatedOn) || (transitSpace == null && (SeatedOn || bedLie)))
            {
                // the seat / bed went away under us
                bool stand = !Sitting;
                ClearTransit();
                if (Sitting) { StandUp(); return; }
                bedLie = null;
                if (stand || !cc.enabled) Teleport(transform.position + Vector3.up * 0.2f, transform.eulerAngles.y);
                return;
            }
            // a move while sitting down / lying in a bed: back up again
            bool moving = moveInput.sqrMagnitude > 0.25f || jump;
            jump = false;
            if (moving && (legAt > 0 || legT > 0.15f))
            {
                if (Sitting && !transitStanding && SeatedOn && SeatedOn.ride == null) { StandUp(); return; }
                if (bedLie && legs[legAt].mode != LegMode.GetUp && legs[legAt].mode != LegMode.Stand) { bedAction = null; GetUpFromBed(); return; }
            }
            var leg = legs[legAt];
            legT += dt;
            float u = Mathf.Clamp01(legT / leg.dur);
            Pose(leg, u, dt);
            if (legT < leg.dur) return;
            legT = 0f; legAt++;
            if (legAt < legs.Count) return;
            // done
            var done = transitDone;
            bool standing = transitStanding;
            ClearTransit();
            if (done != null) { done(); return; }
            if (standing) StandEnd();
        }

        Vector3 standAt; float standYaw; MadMax.Vehicles.InteriorSpace standSpace;

        void StandEnd()
        {
            var space = standSpace; standSpace = null;
            if (space) EnterInterior(space, space.transform.InverseTransformPoint(standAt));
            else Teleport(standAt, transform.eulerAngles.y);
        }

        void Pose(Leg leg, float u, float dt)
        {
            float h = H;
            var up = SpaceUp;
            Vector3 a = FromSpace(leg.a), b = FromSpace(leg.b);
            Quaternion ra = FromSpace(leg.ra), rb = FromSpace(leg.rb);
            Vector3 pos; Quaternion rot;
            var st = new HumanAnimator.State { grounded = true, lookPitch = lookPitch };
            var clip = (string)null; float clipU = u;
            switch (leg.mode)
            {
                case LegMode.Walk:
                case LegMode.Step:
                {
                    float w = leg.mode == LegMode.Step ? Smooth(0f, 1f, u) : u;
                    pos = Vector3.Lerp(a, b, w);
                    rot = Quaternion.Slerp(ra, rb, Smooth(0f, 0.3f, u));
                    st.speed = Vector3.Distance(a, b) / leg.dur * (leg.mode == LegMode.Step ? 0.8f : 1f);
                    break;
                }
                case LegMode.Sit:
                {
                    // back over the seat while turning, the hips sink as the knees bend
                    var flat = Vector3.ProjectOnPlane(b - a, up);
                    float hu = Smooth(0f, 0.8f, u), vu = Mathf.Pow(Mathf.Clamp01(u / 0.75f), 1.3f);
                    pos = a + flat * hu + up * (Vector3.Dot(b - a, up) * vu);
                    rot = Quaternion.Slerp(ra, rb, Smooth(0f, 0.35f, u));
                    clip = "sit_down";
                    if (!HasClip(clip)) { st.sitting = u > 0.45f; st.lounging = true; }
                    break;
                }
                case LegMode.Stand:
                {
                    var flat = Vector3.ProjectOnPlane(b - a, up);
                    float hu = Smooth(0.15f, 0.85f, u), vu = Smooth(0.25f, 1f, u);
                    pos = a + flat * hu + up * (Vector3.Dot(b - a, up) * vu);
                    rot = Quaternion.Slerp(ra, Facing(flat, up, rb), Smooth(0.2f, 0.8f, u));
                    clip = "stand_up";
                    st.sitting = true; st.lounging = true;                                          // the base under the clip (seamless at the start)
                    if (!HasClip(clip)) st.sitting = u < 0.5f;
                    break;
                }
                case LegMode.LieBack:
                case LegMode.GetUp:
                {
                    // the pelvis travels from the edge to the middle while the clip's own offset lowers it (compensated)
                    bool back = leg.mode == LegMode.LieBack;
                    float k = back ? u : 1f - u;                                                    // 0 = on the edge, 1 = lying
                    Vector3 edge = back ? a : b, lie = back ? b : a;
                    Quaternion re = back ? ra : rb, rl = back ? rb : ra;
                    float off0 = 0f, off1 = -0.8f, off = Mathf.Lerp(off0, off1, k);
                    clip = "lie_back"; clipU = k;
                    var c = HumanClips.Enabled ? HumanClips.Get(clip) : null;
                    if (c != null) { c.Sample(k, clipTmp, out var p); off = p.y; }
                    var pelA = edge + up * ((0.94f + off0) * h); var pelB = lie + up * ((0.94f + off1) * h);
                    var pel = Vector3.Lerp(pelA, pelB, Smooth(0f, 1f, k));
                    pos = pel - up * ((0.94f + off) * h);
                    rot = Quaternion.Slerp(re, rl, Smooth(0.1f, 0.8f, k));
                    if (back) { st.sitting = true; st.lounging = true; }                            // edge pose under the clip at its start
                    else st.lying = true;                                                           // lying pose under it
                    if (c == null) { st.sitting = k < 0.5f; st.lounging = true; st.lying = k >= 0.5f; }
                    break;
                }
                case LegMode.Climb:
                {
                    pos = Vector3.Lerp(a, b, u);
                    rot = rb;
                    climbPhase += dt * 6f;
                    float swing = Mathf.Sin(climbPhase) * 25f;
                    st.tool = new ToolPose { armRX = -150f + swing, armLX = -150f - swing, armRZ = 8f, armLZ = -8f, foreR = -40f, foreL = -40f, chestX = 6f, knees = 20f + Mathf.Abs(swing) };
                    st.twoHanded = true;
                    break;
                }
                case LegMode.Hop:
                {
                    float w = Smooth(0f, 1f, u);
                    pos = Vector3.Lerp(a, b, w) + up * (Mathf.Sin(u * Mathf.PI) * 0.25f);
                    rot = Quaternion.Slerp(ra, rb, w);
                    st.sitting = transitStanding ? u < 0.5f : u > 0.5f; st.riding = true;                // up onto the saddle, or down off it
                    break;
                }
                default:                                                                            // Hold: lying still
                    pos = a; rot = ra;
                    st.lying = true;
                    st.lyingSide = bedLie != null && u > 0.15f && u < 0.7f;                         // asleep in a bed: on the side (lie_side)
                    break;
            }
            if (clip != null && HasClip(clip)) { st.action = clip; st.actionT = Mathf.Clamp01(clipU); }
            bool lyingPose = leg.mode == LegMode.Hold || ((leg.mode == LegMode.LieBack || leg.mode == LegMode.GetUp) && !HasClip("lie_back") && st.lying);
            anim.FootIK = leg.mode == LegMode.Walk || leg.mode == LegMode.Step ? savedFootIK : false;
            if (lyingPose && !anim.UsingClips) LieProcedural(ref pos, ref rot, h);
            transform.SetPositionAndRotation(pos, rot);
            Velocity = Vector3.zero;
            anim.Tick(dt, st);
        }

        /// <summary>Without keyframed clips the body can't lie by its bones: the whole root tips onto its back, the pelvis
        /// where the clip would put it.</summary>
        static void LieProcedural(ref Vector3 pos, ref Quaternion rot, float h)
        {
            var pel = pos + rot * Vector3.up * (0.14f * h);
            var tipped = rot * Quaternion.Euler(-88f, 0f, 0f);
            pos = pel - tipped * Vector3.up * (0.94f * h);
            rot = tipped;
        }

        static bool HasClip(string n) => HumanClips.Enabled && HumanClips.Get(n) != null;

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
    }
}
