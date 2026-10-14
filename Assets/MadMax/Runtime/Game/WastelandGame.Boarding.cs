using System.Collections;
using System.Collections.Generic;
using MadMax.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Getting in and out on foot (setting GET IN / OUT ANIMATION): walk to the door, it swings open on its
    /// front hinge, slide into the seat, it shuts; out the same way backwards; online too (a local visual: the seat
    /// request goes out when the door is open, peers see the usual seat and work-pose updates). Bikes take a short hop
    /// on and off. The passenger door swings for anyone getting in or out beside the driver (<see cref="SwingDoorAt"/>).
    /// Only for the player's own key presses; scripted enters and exits (fleet cycling, crashes, tests) stay instant.
    /// Aircraft, boats and walk-in cabins keep their own ways in.</summary>
    public partial class WastelandGame
    {
        /// <summary>The driven vehicle has a working tool (machine arm, blade, tipper, paver, crane) or flies: the arrows
        /// and Q/E work it (Controls ToolUp..ToolB) instead of doubling as driving keys.</summary>
        public bool ToolKeys
        {
            get
            {
                if (!Current) return false;
                if (Current.GetComponent<FlightModel>()) return true;
                if (Current.TryGetComponent<Machine>(out var m) && m.enabled && m.kind != Machine.Kind.Roller) return true;
                if (LifterTool(Current)) return true;                                           // magnet crane, grapple
                return Current.TryGetComponent<Crane>(out var cr) && cr.CranePart;
            }
        }

        /// <summary>A get-in / get-out animation is playing (the vehicle takes no input, F is ignored).</summary>
        public bool Boarding { get; private set; }
        const float DoorOpen = BoardingAlign.DoorAngle;

        bool Animated(VehicleDriver car) => GameSettings.Current.boardingAnimation && !ExternalInput && car && Player && !Player.Interior
            && Player.LowState == PlayerCharacter.Low.None && !Player.Puppeted && !Player.Carried
            && !car.GetComponent<BikeBalance>() && !car.aircraft && !car.GetComponent<BoatModel>() && !car.GetComponent<InteriorSpace>();

        /// <summary>A bike (or sidecar outfit): a short hop on / off instead of a door.</summary>
        bool Hops(VehicleDriver car) => GameSettings.Current.boardingAnimation && !ExternalInput && car && Player && !Player.Interior && car.GetComponent<BikeBalance>();

        public void EnterAnimated(VehicleDriver car)
        {
            if (Hops(car)) { StartCoroutine(HopOnRoutine(car)); return; }
            if (!Animated(car)) { Enter(car); return; }
            StartCoroutine(BoardRoutine(car));
        }

        public void ExitAnimated()
        {
            var car = Current;
            if (Hops(car) && Mathf.Abs(car.ForwardSpeed) < 1.5f)
            {
                // off the saddle: a hop down to where the exit puts you
                var seat = Player.transform.position; var seatRot = Player.transform.rotation;
                Exit();
                if (!Current) Player.HopFrom(seat, seatRot, 0.3f);
                return;
            }
            if (!Animated(car) || Mathf.Abs(car.ForwardSpeed) > 1.5f) { Exit(); return; }
            StartCoroutine(AlightRoutine(car));
        }

        IEnumerator HopOnRoutine(VehicleDriver car)
        {
            Boarding = true;
            var startW = Player.transform.position;
            Enter(car);                                                                  // seats the player (online: asks first)
            if (car && Current == car && Player.SeatedIn == car)
            {
                var seat = Player.transform.localPosition;
                var start = car.transform.InverseTransformPoint(startW);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
                {
                    if (!car || Player.SeatedIn != car) break;
                    Player.transform.localPosition = Vector3.Lerp(start, seat, Mathf.SmoothStep(0f, 1f, t)) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.22f);
                    yield return null;
                }
                if (car && Player.SeatedIn == car) Player.transform.localPosition = seat;
            }
            Boarding = false;
        }

        // ------------------------------------------------------------------ doors swinging for anyone getting in or out

        static readonly HashSet<Transform> swinging = new HashSet<Transform>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetBoardingStatics() => swinging.Clear();

        /// <summary>The door (part in a door socket) of <paramref name="car"/> nearest <paramref name="near"/>, on that side;
        /// null without doors.</summary>
        public static Transform DoorNear(VehicleDriver car, Vector3 near)
        {
            if (!car || !car.TryGetComponent<VehicleChassis>(out var ch)) return null;
            var nl = car.transform.InverseTransformPoint(near);
            Transform best = null; float bd = float.MaxValue;
            foreach (var s in ch.Sockets)
            {
                if (s.accepts != PartCategory.Door || !s.Current) continue;
                var lp = car.transform.InverseTransformPoint(s.transform.position);
                if (Mathf.Sign(lp.x) != Mathf.Sign(nl.x)) continue;
                float d = Mathf.Abs(lp.z - nl.z);
                if (d < bd) { bd = d; best = s.Current.transform; }
            }
            return best;
        }

        /// <summary>Swing the door nearest <paramref name="near"/> open, hold it <paramref name="hold"/> s and shut it (someone
        /// getting in or out: the passenger, a companion boarding, a rear-seat rider). Visual only, local.</summary>
        public static void SwingDoorAt(VehicleDriver car, Vector3 near, float hold = 0.6f)
        {
            var g = Instance;
            if (!g || !car || !GameSettings.Current.boardingAnimation) return;
            var door = DoorNear(car, near);
            if (!door || swinging.Contains(door)) return;
            g.StartCoroutine(g.DoorCycle(car, door, hold));
        }

        IEnumerator DoorCycle(VehicleDriver car, Transform door, float hold)
        {
            swinging.Add(door);
            MadMax.Audio.Sfx.Play("door_open", door.position, 0.45f, 1.05f, 20f, 0.2f);
            yield return Swing(door, 0f, DoorOpen, 0.25f);
            for (float t = 0f; t < hold && door; t += Time.deltaTime) yield return null;
            yield return Swing(door, DoorOpen, 0f, 0.25f);
            if (door) MadMax.Audio.Sfx.Play("door_close", door.position, 0.45f, 1f, 20f, 0.2f);
            swinging.Remove(door);
        }

        /// <summary>The front door on the side you get in (the part in the door socket), or null.</summary>
        static Transform DoorOn(VehicleDriver car, float side)
        {
            if (!car.TryGetComponent<VehicleChassis>(out var ch)) return null;
            Transform best = null; float bz = float.MinValue;
            foreach (var s in ch.Sockets)
            {
                if (s.accepts != PartCategory.Door || !s.Current) continue;
                var lp = car.transform.InverseTransformPoint(s.transform.position);
                if (Mathf.Sign(lp.x) != side || lp.z < bz) continue;
                bz = lp.z; best = s.Current.transform;
            }
            return best;
        }

        static IEnumerator Swing(Transform door, float from, float to, float time)
        {
            if (!door) yield break;
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                if (!door) yield break;
                door.localRotation = Quaternion.Euler(0f, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)), 0f);
                yield return null;
            }
            if (door) door.localRotation = Quaternion.Euler(0f, to, 0f);
        }

        // ------------------------------------------------------------------ the sequence (animation revamp 2026-10-13)

        /// <summary>Where the body goes for one vehicle: the seat root and the door spot (car-local), the side and the
        /// variant (right, high). See <see cref="BoardingAlign"/>.</summary>
        struct BoardPlan
        {
            public Vector3 seat, door, nominal;     // car-local: seat root (sit_drive's), actual stand spot, the clips' stand spot
            public float side, hsDelta;             // door side (-1 left); actual minus assumed seat-hip height above the ground (m)
            public bool right, high, ok;
        }

        /// <summary>The boarding step playing (tests): walk, align, open, enter, close, out, shut; null = none.</summary>
        public string BoardingStep { get; private set; }
        /// <summary>The clip of the step (null = procedural stand-in) and its progress.</summary>
        public string BoardingClip { get; private set; }
        public float BoardingT { get; private set; }

        static readonly Collider[] boardHits = new Collider[16];
        static readonly RaycastHit[] boardRays = new RaycastHit[16];

        BoardPlan PlanBoard(VehicleDriver car, float h)
        {
            var plan = new BoardPlan { seat = PlayerCharacter.SeatRootLocal(car, h) };
            var t = car.transform;
            plan.side = Mathf.Abs(plan.seat.x) > 0.08f ? Mathf.Sign(plan.seat.x) : Mathf.Sign(t.InverseTransformPoint(ExitPoint(car)).x + 1e-4f);
            plan.right = plan.side > 0f;
            var hipLocal = plan.seat + Vector3.up * (0.94f * h);
            var hip = t.TransformPoint(hipLocal);
            float g0 = GroundBeside(car, t.TransformPoint(plan.seat + new Vector3(plan.side * BoardingAlign.DoorSide * h, 0f, BoardingAlign.DoorForward * h)), hip.y);
            plan.high = hip.y - g0 > BoardingAlign.HighSeat;
            // the clips' stand spot (seat frame, × h), pushed out from the body until a standing capsule fits
            float lat = (plan.high ? BoardingAlign.DoorSideHigh : BoardingAlign.DoorSide) * h, fwd = (plan.high ? BoardingAlign.DoorForwardHigh : BoardingAlign.DoorForward) * h;
            plan.nominal = plan.seat + new Vector3(plan.side * lat, 0f, fwd);
            for (float push = 0f; push <= BoardingAlign.DoorPushMax + 1e-3f; push += BoardingAlign.DoorPushStep)
            {
                var w = t.TransformPoint(plan.seat + new Vector3(plan.side * (lat + push), 0f, fwd));
                w.y = GroundBeside(car, w, hip.y) + 0.02f;
                if (!StandingRoom(w)) continue;
                plan.door = t.InverseTransformPoint(w);
                plan.hsDelta = (hipLocal.y - plan.door.y) - (plan.high ? BoardingAlign.SeatHipHigh : BoardingAlign.SeatHip) * h;
                plan.ok = true;
                break;
            }
            return plan;
        }

        /// <summary>The ground beside the vehicle under <paramref name="p"/> (not the vehicle, not the player).</summary>
        float GroundBeside(VehicleDriver car, Vector3 p, float from)
        {
            int n = Physics.RaycastNonAlloc(new Vector3(p.x, from + 0.5f, p.z), Vector3.down, boardRays, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue, y = terrain ? terrain.Height(p.x, p.z) : p.y;
            for (int i = 0; i < n; i++)
            {
                var c = boardRays[i].collider;
                if (c.transform.IsChildOf(car.transform) || (Player && c.transform.IsChildOf(Player.transform))) continue;
                if (boardRays[i].distance < best) { best = boardRays[i].distance; y = boardRays[i].point.y; }
            }
            return y;
        }

        /// <summary>A standing body fits with its feet at <paramref name="feet"/> (the player's own collider aside).</summary>
        bool StandingRoom(Vector3 feet)
        {
            int n = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.6f, 0.26f, boardHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!Player || !boardHits[i].transform.IsChildOf(Player.transform)) return false;
            return true;
        }

        int boardStart;
        readonly Vector3[] boardLegs = new Vector3[3];

        /// <summary>The player breaks off: a move key / the stick pressed after the sequence began, a blow, the car moving off.</summary>
        bool BoardBroken(VehicleDriver car, bool seated)
        {
            if (!car || !Player || Player.Ragdolled || (Vitals && Vitals.Dead)) return true;
            if (Vitals && Vitals.SinceBlow < 0.2f) return true;                                 // a blow, not the trickle of heat or thirst
            if (!seated && car.Body && car.Body.linearVelocity.sqrMagnitude > 4f) return true;
            if (Time.frameCount - boardStart < 3) return false;
            if (Controls.Down(Controls.Act.Forward) || Controls.Down(Controls.Act.Back) || Controls.Down(Controls.Act.Left) || Controls.Down(Controls.Act.Right) || Controls.Down(Controls.Act.Jump)) return true;
            var pad = Gamepad.current;
            return pad != null && pad.leftStick.ReadValue().sqrMagnitude > 0.5f && !padHeldAtBoard;
        }

        bool padHeldAtBoard;

        static void SetDoor(Transform door, float angle) { if (door) door.localRotation = Quaternion.Euler(0f, angle, 0f); }

        static HumanAnimator.State Standing(float speed = 0f) => new HumanAnimator.State { grounded = true, speed = speed };
        static HumanAnimator.State Driving() => new HumanAnimator.State { grounded = true, sitting = true };

        /// <summary>The reach for the handle without a car_open_door clip (the car is on the right of a body standing at
        /// the left-hand door facing forward: the right hand, the chest turned to it).</summary>
        static ToolPose ReachPose(float u, bool right) => right
            ? new ToolPose { armLX = Mathf.Lerp(-4f, -62f, u), armLZ = Mathf.Lerp(-9f, -26f, u), foreL = -22f, armRX = -2f, armRZ = 9f, foreR = -14f, chestY = -14f * u }
            : new ToolPose { armRX = Mathf.Lerp(-2f, -62f, u), armRZ = Mathf.Lerp(9f, 26f, u), foreR = -22f, armLX = -2f, armLZ = -9f, foreL = -12f, chestY = 14f * u };

        /// <summary>Car-local roots that put a clip's first frame on <paramref name="startFeet"/> (standing) or
        /// <paramref name="startSeat"/> (seated), and its last frame on the other: the root moves between them.</summary>
        static void Anchors(string clip, bool intoSeat, Vector3 door, Vector3 seat, float h, out Vector3 a0, out Vector3 a1)
        {
            var s0 = BoardingAlign.Pelvis("sit_drive", 0f);
            var p0 = BoardingAlign.Pelvis(clip, 0f); var p1 = BoardingAlign.Pelvis(clip, 1f);
            if (intoSeat) { a0 = door - p0 * h; a1 = seat - (p1 - s0) * h; }
            else { a0 = seat - (p0 - s0) * h; a1 = door - p1 * h; }
        }

        IEnumerator BoardRoutine(VehicleDriver car)
        {
            var p = Player;
            float h = p.BodyHeight;
            var plan = PlanBoard(car, h);
            if (!plan.ok) { Enter(car); yield break; }                                    // boxed in: straight in
            Boarding = true; boardStart = Time.frameCount;
            var pad = Gamepad.current; padHeldAtBoard = pad != null && pad.leftStick.ReadValue().sqrMagnitude > 0.5f;
            var door = DoorOn(car, plan.side);
            var cc = p.GetComponent<CharacterController>();
            p.moveInput = Vector2.zero; p.run = false;

            if (use != null) EndUse(use, use.applied);                                        // hands off whatever they were doing
            StopWalk();

            // 1. up to the door on your own legs (round the front or the back when on the far side)
            BoardingStep = "walk"; BoardingClip = null;
            int legs = 0;
            var lp = car.transform.InverseTransformPoint(p.transform.position);
            var he = HalfExtents(car.gameObject);
            if (Mathf.Abs(lp.x) > 0.2f && Mathf.Sign(lp.x) != plan.side && Mathf.Abs(lp.z) < he.z + 0.6f)
            {
                float zEnd = (lp.z >= plan.seat.z ? 1f : -1f) * (he.z + 0.9f);
                boardLegs[legs++] = new Vector3(lp.x, plan.door.y, zEnd);
                boardLegs[legs++] = new Vector3(plan.door.x + plan.side * 0.3f, plan.door.y, zEnd);
            }
            boardLegs[legs++] = plan.door;
            float t0 = Time.time, stuck = 0f;
            var last = p.transform.position;
            for (int leg = 0; leg < legs && car && p && cc && cc.enabled; leg++)
            {
                bool final = leg == legs - 1;
                while (car && p && cc && cc.enabled)
                {
                    var spot = car.transform.TransformPoint(boardLegs[leg]);
                    var d = spot - p.transform.position; d.y = 0f;
                    if (d.magnitude < (final ? BoardingAlign.Arrive : 0.6f)) break;
                    if (BoardBroken(car, false)) { p.AutoWalk = null; BoardDone(); yield break; }
                    float moved = (p.transform.position - last).magnitude;                       // < 0.1 m/s after the first steps = stuck (any frame rate)
                    stuck = Time.time - t0 > 0.6f && moved < 0.1f * Time.deltaTime ? stuck + Time.deltaTime : 0f;
                    last = p.transform.position;
                    if (stuck > BoardingAlign.WalkStuck || Time.time - t0 > BoardingAlign.WalkTimeout) break;
                    p.AutoWalk = d.normalized * Mathf.Clamp(d.magnitude / 0.6f, final ? 0.35f : 0.8f, 1f);
                    yield return null;
                }
            }
            p.AutoWalk = null;
            if (!car || !p) { BoardDone(); yield break; }
            {
                var d = car.transform.TransformPoint(plan.door) - p.transform.position; d.y = 0f;
                if (d.magnitude > 1.1f)
                {
                    // couldn't get round to the door: in the plain way (no sliding through the bodywork)
                    BoardDone();
                    Enter(car);
                    if (Current == car) SwingDoorAt(car, car.transform.TransformPoint(plan.door), 0.3f);
                    yield break;
                }
            }

            // 2. the last bit: square up on the stand spot, facing along the car
            p.BeginPuppet();
            BoardingStep = "align";
            var from = p.transform.position; var fromRot = p.transform.rotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / BoardingAlign.AlignTime)
            {
                if (BoardBroken(car, false)) { yield return AbortBoard(car, door, 0f); yield break; }
                var spot = car.transform.TransformPoint(plan.door);
                float u = Mathf.SmoothStep(0f, 1f, t);
                p.PuppetPose(Vector3.Lerp(from, spot, u), Quaternion.Slerp(fromRot, CarFacing(car), u), false, Standing(Vector3.Distance(from, spot) / BoardingAlign.AlignTime * (1f - t)), Time.deltaTime);
                yield return null;
            }

            // 3. open the door (car_open_door: upper body over standing legs; the hinge follows its door curve)
            var open = BoardingAlign.Get(BoardingAlign.Clip(BoardingAlign.OpenDoor, plan.right, false));
            float len = open != null ? open.length : BoardingAlign.OpenTime;
            float angle = 0f, prevT = -1f;
            BoardingStep = "open"; BoardingClip = open != null ? open.name : null;
            for (float t = 0f; t < 1f; t += Time.deltaTime / len)
            {
                if (BoardBroken(car, false)) { yield return AbortBoard(car, door, angle); yield break; }
                BoardingT = t;
                angle = BoardingAlign.DoorAngle * DoorShare(open, t, true);
                SetDoor(door, angle);
                DoorSounds(open, prevT, t, door, car, plan, true);
                var st = Standing();
                if (open != null) { st.action = open.name; st.actionT = t; } else st.tool = ReachPose(Mathf.Clamp01(t / BoardingAlign.OpenFrom), plan.right);
                p.PuppetPose(car.transform.TransformPoint(plan.door), CarFacing(car), false, st, Time.deltaTime);
                prevT = t;
                yield return null;
            }
            SetDoor(door, BoardingAlign.DoorAngle);
            if (!car) { BoardDone(); yield break; }

            // 4. into the seat
            Enter(car);                                                                       // seats the player (online: asks first)
            if (!(car && Current == car && p.SeatedIn == car))
            {
                p.EndPuppet(true);
                if (door) StartCoroutine(Swing(door, BoardingAlign.DoorAngle, 0f, 0.25f));
                BoardDone();
                yield break;
            }
            p.BeginPuppet();
            var enter = BoardingAlign.Get(BoardingAlign.Clip(BoardingAlign.Enter, plan.right, plan.high));
            len = enter != null ? enter.length : plan.high ? BoardingAlign.EnterTimeHigh : BoardingAlign.EnterTime;
            float seatedAt = enter != null && enter.EventAt("seated") >= 0f ? enter.EventAt("seated") : 0.5f;
            BoardingStep = "enter"; BoardingClip = enter != null ? enter.name : null;
            if (enter != null) p.PuppetSnap();
            bool broke = false;
            for (float t = 0f; t < 1f; t += Time.deltaTime / len)
            {
                if (BoardBroken(car, true)) { broke = true; break; }
                BoardingT = t;
                HumanAnimator.State st;
                Vector3 pos;
                if (enter != null)
                {
                    pos = SeatFrameRoot(enter, t, plan, h, true);
                    st = t < seatedAt ? Standing() : Driving();
                    st.action = enter.name; st.actionT = t;
                    if (enter.HasDoor) SetDoor(door, BoardingAlign.DoorAngle * enter.DoorAt(t));
                }
                else
                {
                    // no clip: in sideways over the sill, then down onto the seat
                    var flat = plan.seat - plan.door; float rise = flat.y; flat.y = 0f;
                    pos = plan.door + flat * Smooth01(0f, 0.8f, t) + Vector3.up * (rise * Mathf.Pow(Mathf.Clamp01(t / 0.75f), 1.3f));
                    st = t < 0.45f ? Standing(0.6f) : Driving();
                }
                p.PuppetPose(pos, Quaternion.identity, true, st, Time.deltaTime);
                yield return null;
            }

            // 5. pull the door shut from the seat (car_close_door: upper body over sit_drive)
            var close = BoardingAlign.Get(BoardingAlign.Clip(BoardingAlign.CloseDoor, plan.right, false));
            len = close != null ? close.length : BoardingAlign.CloseTime;
            BoardingStep = "close"; BoardingClip = close != null ? close.name : null;
            bool shut = false; prevT = -1f;
            for (float t = 0f; t < 1f && !broke; t += Time.deltaTime / len)
            {
                if (!car || p.SeatedIn != car) break;
                if (BoardBroken(car, true)) break;
                BoardingT = t;
                SetDoor(door, BoardingAlign.DoorAngle * DoorShare(close, t, false));
                shut |= DoorSounds(close, prevT, t, door, car, plan, false);
                var st = Driving();
                if (close != null) { st.action = close.name; st.actionT = t; }
                p.PuppetPose(plan.seat, Quaternion.identity, true, st, Time.deltaTime);
                prevT = t;
                yield return null;
            }
            // settled (or broken off: snap to the end)
            SetDoor(door, 0f);
            if (!shut && door) MadMax.Audio.Sfx.Play("door_close", door.position, 0.5f, 1f, 20f, 0.2f);
            if (car && p.SeatedIn == car) { p.transform.localPosition = plan.seat; p.transform.localRotation = Quaternion.identity; }
            p.EndPuppet(false);
            BoardDone();
        }

        static Quaternion CarFacing(VehicleDriver car) => Quaternion.LookRotation(Vector3.ProjectOnPlane(car.transform.forward, Vector3.up).normalized + Vector3.forward * 1e-4f);

        /// <summary>The root (car-local) for a seat-frame car clip at t (CLIPS.md): the seat root, lowered by the seat-height
        /// difference and shifted by the stand-spot difference while the feet carry the body outside (ground[f]); clips
        /// without a ground curve fall back to a root lerp fitted from their first and last pelvis offsets.</summary>
        static Vector3 SeatFrameRoot(HumanClip c, float t, BoardPlan plan, float h, bool intoSeat)
        {
            if (c.HasGround)
            {
                float g = Mathf.Clamp01(c.GroundAt(t));
                var shift = plan.door - plan.nominal; shift.y = 0f;
                return plan.seat + Vector3.down * (plan.hsDelta * g) + shift * g;
            }
            Anchors(c.name, intoSeat, plan.door, plan.seat, h, out var a0, out var a1);
            return Vector3.Lerp(a0, a1, Smooth01(BoardingAlign.RootFrom, BoardingAlign.RootTo, t));
        }

        /// <summary>The door's open share at t: the clip's door curve, else the fallback windows (open clip rising, close
        /// clip falling).</summary>
        static float DoorShare(HumanClip c, float t, bool opening)
        {
            if (c != null && c.HasDoor) return Mathf.Clamp01(c.DoorAt(t));
            return opening ? Smooth01(BoardingAlign.OpenFrom, BoardingAlign.OpenTo, t) : 1f - Smooth01(BoardingAlign.CloseFrom, BoardingAlign.CloseTo, t);
        }

        /// <summary>The door's sounds from the clip's door_open / door_shut events between two times (either direction:
        /// a clip played backwards opens what it shut). True = the door shut.</summary>
        static bool DoorSounds(HumanClip c, float t0, float t1, Transform door, VehicleDriver car, BoardPlan plan, bool opening)
        {
            var at = door ? door.position : car ? car.transform.TransformPoint(plan.door) : Vector3.zero;
            float lo = Mathf.Min(t0, t1), hi = Mathf.Max(t0, t1);
            bool back = t1 < t0;
            bool openEv, shutEv;
            if (c != null && c.EventCount > 0) { openEv = c.Crossed("door_open", lo, hi); shutEv = c.Crossed("door_shut", lo, hi); }
            else
            {
                float mark = opening ? BoardingAlign.OpenFrom : BoardingAlign.CloseTo;
                bool hit = (t0 < 0f ? 0f : lo) < mark && hi >= mark;
                openEv = hit && opening; shutEv = hit && !opening;
            }
            if (back) { var x = openEv; openEv = shutEv; shutEv = x; }                     // played backwards
            if (openEv) MadMax.Audio.Sfx.Play("door_open", at, 0.5f, 1f, 20f, 0.2f);
            if (shutEv) MadMax.Audio.Sfx.Play("door_close", at, 0.5f, 1f, 20f, 0.2f);
            return shutEv;
        }

        /// <summary>Broken off before sitting: back on the feet where the body is, the door swings shut.</summary>
        IEnumerator AbortBoard(VehicleDriver car, Transform door, float angle)
        {
            if (Player) Player.EndPuppet(true);
            BoardDone();
            if (door && Mathf.Abs(angle) > 1f) yield return Swing(door, angle, 0f, 0.25f);
        }

        void BoardDone() { Boarding = false; BoardingStep = null; BoardingClip = null; BoardingT = 0f; }

        static float Smooth01(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        IEnumerator AlightRoutine(VehicleDriver car)
        {
            var p = Player;
            float h = p.BodyHeight;
            var plan = PlanBoard(car, h);
            if (!plan.ok) { Exit(); yield break; }                                          // no room beside the door: the usual way out
            Boarding = true; boardStart = Time.frameCount;
            var pad = Gamepad.current; padHeldAtBoard = pad != null && pad.leftStick.ReadValue().sqrMagnitude > 0.5f;
            car.throttleInput = car.brakeInput = car.steerInput = 0f; car.handbrake = true;
            var door = DoorOn(car, plan.side);
            p.BeginPuppet();
            bool broke = false;
            var exit = BoardingAlign.Get(BoardingAlign.Clip(BoardingAlign.Exit, plan.right, plan.high));

            // 1. without an exit clip that opens the door itself: push it open from the seat (car_close_door backwards)
            float len, prevT = -1f;
            if (exit == null || !exit.HasDoor)
            {
                var close = BoardingAlign.Get(BoardingAlign.Clip(BoardingAlign.CloseDoor, plan.right, false));
                len = close != null ? close.length : BoardingAlign.CloseTime;
                BoardingStep = "open"; BoardingClip = close != null ? close.name : null;
                for (float t = 0f; t < 1f; t += Time.deltaTime / len)
                {
                    if (!car || p.SeatedIn != car || BoardBroken(car, true)) { broke = true; break; }
                    float u = 1f - t;
                    BoardingT = t;
                    SetDoor(door, BoardingAlign.DoorAngle * DoorShare(close, u, false));
                    DoorSounds(close, prevT < 0f ? 1f : 1f - prevT, u, door, car, plan, false);
                    var st = Driving();
                    if (close != null) { st.action = close.name; st.actionT = u; }
                    p.PuppetPose(plan.seat, Quaternion.identity, true, st, Time.deltaTime);
                    prevT = t;
                    yield return null;
                }
                SetDoor(door, BoardingAlign.DoorAngle);
            }

            // 2. out of the seat onto the stand spot (car_exit opens the door with the left hand, legs out, stand)
            len = exit != null ? exit.length : plan.high ? BoardingAlign.ExitTimeHigh : BoardingAlign.ExitTime;
            float standingAt = exit != null && exit.EventAt("standing") >= 0f ? exit.EventAt("standing") : 0.55f;
            BoardingStep = "out"; BoardingClip = exit != null ? exit.name : null;
            if (exit != null) p.PuppetSnap();
            prevT = -1f;
            for (float t = 0f; t < 1f && !broke; t += Time.deltaTime / len)
            {
                if (!car || p.SeatedIn != car || BoardBroken(car, true)) { broke = true; break; }
                BoardingT = t;
                HumanAnimator.State st;
                Vector3 pos;
                if (exit != null)
                {
                    pos = SeatFrameRoot(exit, t, plan, h, false);
                    st = t < standingAt ? Driving() : Standing();
                    st.action = exit.name; st.actionT = t;
                    if (exit.HasDoor) { SetDoor(door, BoardingAlign.DoorAngle * exit.DoorAt(t)); DoorSounds(exit, prevT, t, door, car, plan, true); }
                }
                else
                {
                    // no clip: up off the seat and out sideways over the sill
                    var flat = plan.door - plan.seat; float rise = flat.y; flat.y = 0f;
                    pos = plan.seat + flat * Smooth01(0.2f, 1f, t) + Vector3.up * (rise * Smooth01(0.3f, 1f, t));
                    st = t < 0.55f ? Driving() : Standing(0.6f);
                }
                p.PuppetPose(pos, Quaternion.identity, true, st, Time.deltaTime);
                prevT = t;
                yield return null;
            }
            SetDoor(door, BoardingAlign.DoorAngle);

            // 3. on the feet at the stand spot (where car_exit's last frame put them), push the door shut (car_open_door backwards)
            var spotW = car ? car.transform.TransformPoint(plan.door) : p.transform.position;
            float yaw = car ? Mathf.Atan2(car.transform.forward.x, car.transform.forward.z) * Mathf.Rad2Deg : p.transform.eulerAngles.y;
            if (Current == car) Exit();
            p.EndPuppet(true);
            if (car && StandingRoom(spotW)) p.Teleport(spotW, yaw);                       // else Exit's own clear spot
            if (broke || !car) { SetDoor(door, 0f); if (door) MadMax.Audio.Sfx.Play("door_close", door.position, 0.5f, 1f, 20f, 0.2f); BoardDone(); yield break; }
            p.BeginPuppet();
            var open = BoardingAlign.Get(BoardingAlign.Clip(BoardingAlign.OpenDoor, plan.right, false));
            len = open != null ? open.length : BoardingAlign.OpenTime;
            BoardingStep = "shut"; BoardingClip = open != null ? open.name : null;
            if (open != null) p.PuppetSnap();
            bool shut = false; prevT = -1f;
            var stand = p.transform.position; var standRot = p.transform.rotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / len)
            {
                if (!car || BoardBroken(car, false)) break;
                float u = 1f - t;
                BoardingT = t;
                SetDoor(door, BoardingAlign.DoorAngle * DoorShare(open, u, true));
                shut |= DoorSounds(open, prevT < 0f ? 1f : 1f - prevT, u, door, car, plan, true);
                var st = Standing();
                if (open != null) { st.action = open.name; st.actionT = u; } else st.tool = ReachPose(Mathf.Clamp01(u / BoardingAlign.OpenFrom), plan.right);
                p.PuppetPose(stand, standRot, false, st, Time.deltaTime);
                prevT = t;
                yield return null;
            }
            SetDoor(door, 0f);
            if (!shut && door) MadMax.Audio.Sfx.Play("door_close", door.position, 0.5f, 1f, 20f, 0.2f);
            p.EndPuppet(true);
            BoardDone();
        }
    }
}
