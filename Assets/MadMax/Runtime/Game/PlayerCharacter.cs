using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The wastelander: HumanRig body (clothing, hair), HumanAnimator poses, CharacterController movement,
    /// footprints in mud. Can walk vehicle interiors (kinematic, vehicle-local) and sits physically in driver seats.
    /// Inputs are written by WastelandGame; movement is relative to <see cref="viewYaw"/>.</summary>
    [RequireComponent(typeof(CharacterController))]
    public partial class PlayerCharacter : MonoBehaviour
    {
        public float walkSpeed = 1.7f;                 // brisk walk (real 1.4 m/s)
        public float runSpeed = 5.2f;                  // run (real jog 3–4, sprint 6–8 m/s)
        public float jumpSpeed = 4.8f;
        public float gravity = -22f;

        [HideInInspector] public Vector2 moveInput;
        [HideInInspector] public bool run, jump;
        [HideInInspector] public float viewYaw, lookPitch;
        [HideInInspector] public bool faceView;   // first person: body follows the camera yaw
        [HideInInspector] public bool aiming;     // weapon raised (RMB): slow, steady steps

        public Vector3 Velocity { get; private set; }
        public HandTool Tool { get; private set; }
        public InteriorSpace Interior { get; private set; }
        public VehiclePart Carried { get; private set; }
        public VehicleDriver SeatedIn { get; private set; }
        /// <summary>Furniture seat the player sits on (chair, sofa, bench); see <see cref="Sitting"/>.</summary>
        public MadMax.Building.Seat SeatedOn { get; private set; }
        public bool Sitting { get; private set; }
        Vector3 seatSpot;
        InteriorSpace seatInterior;
        public HumanRig Rig { get; private set; }
        public Transform Eye => Rig ? Rig.Eye : transform;
        public bool Swinging => swingT >= 0f;
        /// <summary>0 = idle, else normalised swing time (network animation).</summary>
        public float SwingProgress => swingT >= 0f && Tool ? Mathf.Clamp(swingT / Tool.swingDuration, 0.004f, 1f) : 0f;

        CharacterController cc;
        HumanAnimator anim;
        float vy, swingT = -1f, lastYaw, fallSpeed;
        bool airborne;

        /// <summary>Carrying more than the strength-based capacity: slower, cannot run.</summary>
        public bool Swimming { get; private set; }
        public bool Encumbered { get; set; }
        bool struck, firstPerson;

        public static PlayerCharacter Create(Material mat, Appearance look = null, System.Collections.Generic.IEnumerable<string> outfit = null)
        {
            var go = new GameObject("Player");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.28f; cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.35f; cc.slopeLimit = 55f; cc.skinWidth = 0.03f;
            var p = go.AddComponent<PlayerCharacter>();
            var rig = go.AddComponent<HumanRig>();
            rig.material = mat;
            if (look != null) rig.appearance = look.Clone();
            rig.outfit.AddRange(outfit ?? ClothingLibrary.Starter);
            p.Rig = rig;
            p.RebuildBody();
            return p;
        }

        void Awake() { cc = GetComponent<CharacterController>(); }

        /// <summary>Apply appearance/outfit changes (keeps the equipped tool).</summary>
        public void RebuildBody()
        {
            if (Tool) Tool.transform.SetParent(transform, false);
            var g = WastelandGame.Instance;
            if (g) Rig.condition = g.GarmentCondition;
            Rig.Rebuild();
            anim = new HumanAnimator(Rig);
            anim.Footstep += OnFootstep;
            if (Tool) AttachTool(Tool);
            SetFirstPerson(firstPerson);
        }

        // ------------------------------------------------------------------ tools
        public void Equip(HandTool tool)
        {
            if (Tool) Destroy(Tool.gameObject);
            Tool = tool;
            if (tool) AttachTool(tool);
        }

        /// <summary>Scripted upper-body pose (refuelling, washing...) overriding the tool pose while set.</summary>
        [System.NonSerialized] public ToolPose? PoseOverride;
        GameObject prop;

        /// <summary>Hold a prop (jerry can...) in the right hand instead of the tool.</summary>
        public void HoldProp(Mesh mesh, Material mat)
        {
            DropProp();
            if (Tool) Tool.gameObject.SetActive(false);
            prop = new GameObject("Prop", typeof(MeshFilter), typeof(MeshRenderer));
            prop.GetComponent<MeshFilter>().sharedMesh = mesh;
            prop.GetComponent<MeshRenderer>().sharedMaterial = mat;
            prop.transform.SetParent(Rig.RightHand, false);
            prop.transform.localPosition = new Vector3(0f, -0.1f, 0.02f);
            prop.transform.localScale = Vector3.one * 0.7f;
        }

        public void DropProp()
        {
            if (prop) Destroy(prop);
            prop = null;
            if (Tool) Tool.gameObject.SetActive(true);
        }

        void AttachTool(HandTool tool)
        {
            tool.transform.SetParent(Rig.RightHand, false);
            tool.transform.localPosition = new Vector3(0f, -0.055f, 0.01f);
            tool.transform.localRotation = Quaternion.identity;
            tool.transform.localScale = Vector3.one * 0.7f;       // world-scale tool meshes next to the finer character voxels
        }


        /// <summary>Swing / fire the equipped tool (faces the view direction first in first/third person).</summary>
        public void Attack(bool faceViewYaw)
        {
            if (!Tool || swingT >= 0f || Carried || SeatedIn || (Sitting && !(SeatedOn && SeatedOn.standing)) || Ragdolled) return;
            var g = WastelandGame.Instance;
            if (g && Tool.TwoHanded && g.ArmBroken) { g.Toast("BROKEN ARM: CAN'T USE A TWO-HANDED " + Tool.toolName); return; }
            if (faceViewYaw) transform.rotation = Interior ? Quaternion.LookRotation(Vector3.ProjectOnPlane(Quaternion.Euler(0, viewYaw, 0) * Vector3.forward, Interior.transform.up), Interior.transform.up) : Quaternion.Euler(0, viewYaw, 0);
            swingT = 0f;
            struck = false;
            var v = WastelandGame.Instance ? WastelandGame.Instance.Vitals : null;
            v?.Spend(Tool.style == ToolStyle.Overhead ? 12f : Tool.style == ToolStyle.Gun ? 2f : 6f);
        }

        // ------------------------------------------------------------------ carrying
        public void Carry(VehiclePart part)
        {
            Carried = part;
            if (!part) return;
            if (part.TryGetComponent<Rigidbody>(out var rb)) Destroy(rb);
            foreach (var c in part.GetComponentsInChildren<Collider>()) c.enabled = false;
            part.transform.SetParent(transform, true);
            part.transform.localPosition = new Vector3(0f, 1.05f, 0.5f);
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = Vector3.one;
        }

        /// <summary>Release the carried part without dropping it (mounting takes over the transform).</summary>
        public VehiclePart TakeCarried() { var p = Carried; Carried = null; return p; }

        public void DropCarried()
        {
            var part = TakeCarried();
            if (!part) return;
            part.transform.SetParent(null, true);
            foreach (var c in part.GetComponentsInChildren<Collider>()) c.enabled = true;
            var rb = part.gameObject.AddComponent<Rigidbody>();
            rb.mass = part.mass;
            rb.linearVelocity = transform.forward * 1.5f;
        }

        // ------------------------------------------------------------------ places
        public void EnterInterior(InteriorSpace space, Vector3 local)
        {
            Unseat();
            cc.enabled = false;
            Interior = space;
            transform.SetParent(space.transform, false);
            transform.localPosition = space.Resolve(local, 0.26f);
            transform.localRotation = Quaternion.identity;
            vy = 0;
            Rig.ResetHair();
        }

        public void ExitInterior(Vector3 worldPos)
        {
            float yaw = transform.eulerAngles.y;
            Interior = null;
            transform.SetParent(null, true);
            Teleport(worldPos, yaw);
        }

        /// <summary>Sit in the driver seat: hips placed from the vehicle's DriverEye, pose driven by steering.</summary>
        public void SitIn(VehicleDriver car)
        {
            if (Carried) DropCarried();
            Interior = null;
            cc.enabled = false;
            SeatedIn = car;
            var eye = car.transform.Find("DriverEye");
            transform.SetParent(car.transform, false);
            float h = Rig.appearance.height;
            var eyeLocal = eye ? eye.localPosition : new Vector3(0.3f, 1.1f, 0f);
            transform.localPosition = eyeLocal + new Vector3(0f, -(0.94f + 0.74f) * h, -0.1f);
            transform.localRotation = Quaternion.identity;
            swingT = -1f;
            Rig.ResetHair();
        }

        /// <summary>Sit on a piece of furniture: hips at <paramref name="spot"/> (seat-local), facing the seat's front.
        /// Follows the seat every frame (seats on vehicles ride along); the seat breaking stands the player up.</summary>
        public void SitOn(MadMax.Building.Seat seat, Vector3 spot)
        {
            if (!seat || Ragdolled) return;
            if (Carried) DropCarried();
            Unseat();
            seatInterior = Interior;
            Interior = null;
            transform.SetParent(null, true);
            cc.enabled = false;
            SeatedOn = seat; seatSpot = spot; Sitting = true;
            swingT = -1f; vy = 0f; Velocity = Vector3.zero;
            FollowSeat();
            Rig.ResetHair();
        }

        void FollowSeat()
        {
            var t = SeatedOn.transform;
            var pos = t.TransformPoint(seatSpot + Vector3.down * (0.94f * Rig.appearance.height));
            // standing on a roof: upright, free to turn (first person follows the view)
            if (SeatedOn.standing) transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, faceView ? viewYaw : transform.eulerAngles.y, 0f));
            else transform.SetPositionAndRotation(pos, t.rotation);
        }

        public void StandUp()
        {
            if (!Sitting) return;
            Sitting = false;
            var seat = SeatedOn; SeatedOn = null;
            float yaw = transform.eulerAngles.y;
            var at = transform.position + Vector3.up * 0.3f;
            if (seat)
            {
                at = seat.StandPoint(seatSpot, 0);
                for (int i = 0; i < 4; i++)
                {
                    var c = seat.StandPoint(seatSpot, i);
                    if (!Physics.CheckCapsule(c + Vector3.up * 0.4f, c + Vector3.up * 1.5f, 0.26f, ~0, QueryTriggerInteraction.Ignore)) { at = c; break; }
                }
            }
            var space = seatInterior; seatInterior = null;
            if (space) EnterInterior(space, space.transform.InverseTransformPoint(at));
            else Teleport(at, yaw);
        }

        public void Unseat()
        {
            if (!SeatedIn) return;
            SeatedIn = null;
            transform.SetParent(null, true);
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            Unseat();
            cc.enabled = false;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            cc.enabled = true;
            vy = 0; Velocity = Vector3.zero;
            Rig.ResetHair();
        }

        public void SetVisible(bool v) => Rig.SetVisible(v);

        /// <summary>First person: hide own head and hair; body, arms and tool stay visible.</summary>
        public void SetFirstPerson(bool fp)
        {
            firstPerson = fp;
            if (Rig) Rig.SetHeadVisible(!fp);
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || anim == null) return;          // anim is rebuilt with the body (lost on a domain reload)
            if (Ragdolled) return;
            if (Sitting) { UpdateSitting(dt); return; }
            if (SeatedIn) { UpdateSeated(dt); return; }
            if (Interior) { UpdateInterior(dt); return; }
            if (Traversing) { TraverseTick(dt); return; }                                  // vault, climb, zip (roadmap 22)

            var terrain = DeformableTerrain.Instance;
            var dir = Quaternion.Euler(0, viewYaw, 0) * new Vector3(moveInput.x, 0, moveInput.y);
            dir = Vector3.ClampMagnitude(dir, 1f);
            var game = WastelandGame.Instance;
            var stats = game ? game.Stats : null;
            var vitals = game ? game.Vitals : null;
            bool canRun = run && (!vitals || !vitals.Exhausted) && !Encumbered && (!game || game.CanRunInjured);
            float speed = (canRun ? runSpeed : walkSpeed) * (stats != null ? stats.MoveSpeed : 1f);
            if (Encumbered) speed *= 0.7f;
            if (aiming) speed = Mathf.Min(speed, walkSpeed * 0.7f);
            if (game) speed *= game.InjurySpeed;
            if (Carried) speed *= 0.6f;
            speed *= CrouchSpeed(stats, canRun);
            if (terrain) speed *= Mathf.Lerp(1f, 0.6f, terrain.SurfaceAt(transform.position.x, transform.position.z).mud);
            if (MadMax.Building.DefenceHazard.All.Count > 0) speed *= MadMax.Building.DefenceHazard.SlowAt(transform.position);   // barbed wire
            // water: wade (slower), swim (float at the surface, costs stamina), drown when exhausted
            float waterLvl = terrain ? terrain.WaterLevel(transform.position.x, transform.position.z) : float.NaN;
            float waterDepth = float.IsNaN(waterLvl) ? 0f : waterLvl - transform.position.y;
            Swimming = waterDepth > 1.25f;
            if (waterDepth > 0.2f) speed *= Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(waterDepth / 1.25f));
            var horizontal = dir * speed;
            if (Sliding) horizontal = SlideVelocity(dt);
            if (Swimming)
            {
                bool tired = vitals && (vitals.Exhausted || !vitals.Spend(4f * dt));
                float targetY = waterLvl - (tired ? 1.9f : 1.3f);
                vy = Mathf.Lerp(vy, (targetY - transform.position.y) * 3f, 1f - Mathf.Exp(-4f * dt));
                if (tired) vitals?.Hurt(6f * dt, "DROWNED");
                airborne = false; fallSpeed = 0f; jump = false;
                if (horizontal.sqrMagnitude > 0.1f && Random.value < dt * 4f)
                    MadMax.World.Fx.Smoke(new Vector3(transform.position.x, waterLvl + 0.05f, transform.position.z), Vector3.up * 0.8f + Random.insideUnitSphere * 0.4f, 0.35f, new Color(0.8f, 0.88f, 0.92f, 0.6f), 0.6f);
                var flow = terrain ? terrain.World.RiverFlow(transform.position.x, transform.position.z) : Vector2.zero;   // swept downstream
                cc.Move((horizontal + new Vector3(flow.x, 0f, flow.y) * 0.85f + Vector3.up * vy) * dt);
                Velocity = cc.velocity;
                Face(dir, dt, Quaternion.Euler(0, viewYaw, 0));
                Animate(dt, new Vector2(Velocity.x, Velocity.z).magnitude * 0.6f, false, 0f);
                return;
            }

            if (cc.isGrounded)
            {
                if (airborne) Land(vitals);                                                   // hard landings hurt; a roll saves most of it
                airborne = false; fallSpeed = 0f;
                vy = -1f;
                if (jump && TryTraverse(dir)) { jump = false; return; }                        // vault or climb what is in front
                if (jump && !Carried && !Crouching && (!vitals || !vitals.Exhausted) && (!game || game.CanJumpInjured))
                {
                    bool sprint = canRun && dir.sqrMagnitude > 0.5f;
                    vy = jumpSpeed * (sprint ? 1.08f : 1f); vitals?.Spend(sprint ? 10f : 8f);
                    if (sprint) { sprintJump = 0.6f; game?.Stats?.Practice(MadMax.RPG.Skill.Athletics, 0.5f); }
                }
            }
            else { airborne = true; fallSpeed = Mathf.Min(fallSpeed, vy); }
            jump = false;
            vy += gravity * dt;
            if (sprintJump > 0f) { sprintJump -= dt; horizontal *= 1.12f; }                     // a running leap carries further
            horizontal += Carrier(dt);                                                        // riding on a vehicle bed or roof
            cc.Move((horizontal + Vector3.up * vy) * dt);
            Velocity = cc.velocity;
            CrouchTick(dt, canRun);

            if (terrain)
            {
                var p = transform.position;
                float h = terrain.Height(p.x, p.z);
                if (p.y < h - 0.25f) Teleport(new Vector3(p.x, h + 0.05f, p.z), transform.eulerAngles.y);
            }

            Face(dir, dt, Quaternion.Euler(0, viewYaw, 0));
            Animate(dt, new Vector2(Velocity.x - carry.x, Velocity.z - carry.z).magnitude, cc.isGrounded, Velocity.y);   // riding: legs don't run
        }

        void Face(Vector3 dir, float dt, Quaternion view)
        {
            if (faceView) transform.rotation = view;
            else if (aiming) return;                                                      // top-down aiming turns us to the cursor
            else if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-10f * dt));
        }

        void UpdateInterior(float dt)
        {
            var dir = Quaternion.Euler(0, viewYaw, 0) * new Vector3(moveInput.x, 0, moveInput.y);
            dir = Vector3.ClampMagnitude(dir, 1f);
            var local = Interior.transform.InverseTransformDirection(dir);
            local.y = 0;
            float speed = run ? runSpeed * 0.6f : walkSpeed;
            var before = transform.localPosition;
            transform.localPosition = Interior.Resolve(before + local * speed * dt, 0.26f);
            var moved = (transform.localPosition - before) / dt;
            if (faceView) transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Quaternion.Euler(0, viewYaw, 0) * Vector3.forward, Interior.transform.up), Interior.transform.up);
            else if (local.sqrMagnitude > 0.01f) transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(local), 1f - Mathf.Exp(-10f * dt));
            jump = false;
            Velocity = moved;
            Animate(dt, moved.magnitude, true, 0f);
        }

        void UpdateSitting(float dt)
        {
            if (SeatedOn && SeatedOn.ride != null)
            {
                // in the saddle: the input rides the animal; F (the game) gets off
                SeatedOn.ride(moveInput, run, jump, dt);
                jump = false;
                FollowSeat();
                Velocity = Vector3.zero;
                anim.Tick(dt, new HumanAnimator.State { sitting = true, riding = true, steer = moveInput.x, lookPitch = lookPitch, grounded = true });
                return;
            }
            if (!SeatedOn || moveInput.sqrMagnitude > 0.25f || jump) { jump = false; StandUp(); return; }
            FollowSeat();
            Velocity = Vector3.zero;
            if (SeatedOn.standing) Animate(dt, 0f, true, 0f);
            else anim.Tick(dt, new HumanAnimator.State { sitting = true, lounging = true, lookPitch = lookPitch, grounded = true });
        }

        void UpdateSeated(float dt)
        {
            Velocity = SeatedIn.Body ? SeatedIn.Body.linearVelocity : Vector3.zero;
            var bike = SeatedIn.GetComponent<BikeBalance>();                                   // astride a bike: legs down, pedalling a bicycle
            anim.Tick(dt, new HumanAnimator.State { sitting = true, riding = bike, pedaling = bike && bike.Pedals, pedal = bike ? bike.PedalPhase : 0f, steer = SeatedIn.steerInput, lookPitch = lookPitch, grounded = true });
        }

        /// <summary>Dead and limp (physics ragdoll): no control, no animation.</summary>
        public bool Ragdolled { get { var r = GetComponent<Ragdoll>(); return r && r.Active; } }

        void Animate(float dt, float hs, bool grounded, float vertical)
        {
            var gm = WastelandGame.Instance;
            float yaw = transform.eulerAngles.y;
            float turn = Mathf.DeltaAngle(lastYaw, yaw) / dt;
            lastYaw = yaw;
            ToolPose? toolPose = PoseOverride ?? (Tool ? Tool.IdlePose : null);
            if (Crouching || Sliding) toolPose = CrouchPose(toolPose);
            if (swingT >= 0f && Tool && PoseOverride == null)
            {
                var st = WastelandGame.Instance ? WastelandGame.Instance.Stats : null;
                var vt = WastelandGame.Instance ? WastelandGame.Instance.Vitals : null;
                swingT += dt * (st != null ? st.ToolSpeed : 1f) * (vt && vt.Exhausted ? 0.7f : 1f) * (WastelandGame.Instance ? WastelandGame.Instance.InjuryToolSpeed : 1f);
                float t = swingT / Tool.swingDuration;
                toolPose = Tool.Pose(Mathf.Min(t, 1f));
                if (!struck && t >= Tool.strikeAt) { struck = true; Tool.Strike(this); }
                if (t >= 1f) swingT = -1f;
            }
            anim.Tick(dt, new HumanAnimator.State
            {
                speed = hs, grounded = grounded, verticalSpeed = vertical, turnRate = turn, lookPitch = lookPitch,
                carrying = Carried, tool = Carried ? null : toolPose, twoHanded = Tool && Tool.TwoHanded,
                limpL = gm ? gm.LimpL : 0f, limpR = gm ? gm.LimpR : 0f, armHurtL = gm ? gm.ArmHurtL : 0f, armHurtR = gm ? gm.ArmHurtR : 0f
            });
        }

        /// <summary>The footstep for what is underfoot: a deck or floor piece, a vehicle, water, snow, mud, road, sand, gravel.</summary>
        public static string StepSound(Vector3 foot, Surface s)
        {
            if (Physics.Raycast(foot + Vector3.up * 0.3f, Vector3.down, out var hit, 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<MadMax.Vehicles.VehicleDriver>()) return "step_metal";
                if (hit.collider.GetComponentInParent<MadMax.Building.Placeable>()) return "step_wood";
            }
            var t = DeformableTerrain.Instance;
            if (t && t.WaterDepth(foot.x, foot.z) > 0.05f) return "step_water";
            if (Weather.Snow > 0.35f && s.road < 0.5f) return "step_snow";
            if (s.mud > 0.35f) return "step_mud";
            if (s.road > 0.5f) return "step_road";
            return t && t.BiomeAt(foot.x, foot.z) == Biome.Desert ? "step_sand" : "step_gravel";
        }

        void OnFootstep(bool left)
        {
            if (!SeatedIn && !Swimming && !Interior) MadMax.World.Fx.Footprint(transform.position, transform.forward, left);   // prints in snow, sand, mud
            var terrain = DeformableTerrain.Instance;
            if (!terrain || Interior || SeatedIn) return;
            var foot = transform.position + transform.right * (left ? -0.1f : 0.1f);
            terrain.Deform(foot, transform.forward, transform.right, 0.16f, run ? 1400f : 900f, 0f, 0f);
            var s = terrain.SurfaceAt(foot.x, foot.z);
            MadMax.Audio.Sfx.Play(StepSound(foot, s), foot, run ? 0.45f : 0.28f, Random.Range(0.9f, 1.1f), 18f);
            if (DebrisSystem.Instance && run)
                DebrisSystem.Instance.EmitPuff(foot + Vector3.up * 0.05f, s.wet > 0.5f ? new Color32(70, 42, 26, 255) : new Color32(190, 120, 70, 255), 0.05f, Vector3.up * 0.6f - transform.forward * 0.4f, 0.4f);
        }
    }
}
