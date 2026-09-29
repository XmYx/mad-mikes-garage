using MadMax.Rendering;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    public enum ViewMode { Isometric, TiltShift, ThirdPerson, FirstPerson, TopDown, Hood, Bumper }

    /// <summary>All views render through the PixelArtCamera. Works for vehicles and the on-foot player.
    /// Scroll / +/- zoom, Z/C rotate and PgUp/PgDn tilt (iso, tilt-shift, top-down; RMB drag does both there), mouse
    /// look (RMB in vehicles, free on foot), V cycles views. Vehicles add the hood and bumper cameras.</summary>
    [DefaultExecutionOrder(-90)]
    public class CameraRig : MonoBehaviour
    {
        public PixelArtCamera pixel;
        public Material tiltShiftMaterial;
        public ViewMode mode = ViewMode.Isometric;

        [Header("Isometric (orthographic)")]
        public float isoPitch = 35f;
        public Vector2 isoSizeRange = new Vector2(2.5f, 16f);
        public float isoSize = 6f;

        [Header("2.5D tilt-shift (narrow perspective)")]
        public float tiltPitch = 48f;
        public float tiltFov = 14f;
        public Vector2 tiltDistanceRange = new Vector2(25f, 160f);
        public float tiltDistance = 60f;

        [Header("Top-down (orthographic, straight down)")]
        public Vector2 topSizeRange = new Vector2(5f, 40f);
        public float topSize = 14f;

        [Header("Third person")]
        public float thirdFov = 55f;
        public Vector2 thirdDistanceRange = new Vector2(2.5f, 18f);
        public float thirdDistance = 7.5f;
        public float thirdHeight = 1.6f;
        public float groundClearance = 0.4f;

        [Header("First person")]
        public Vector2 fpsFovRange = new Vector2(35f, 95f);
        public float fpsFov = 60f;                       // vertical: ≈ 90° horizontal at 16:9 (real-feeling scale)

        [Header("Fog (perspective views)")]
        public Color fogColor = new Color(0.86f, 0.5f, 0.28f);
        public float fogStart = 38f, fogEnd = 70f;

        /// <summary>Camera pitch for head look / aiming (degrees, + = down).</summary>
        public float LookPitch => mode == ViewMode.FirstPerson || mode == ViewMode.Hood || mode == ViewMode.Bumper ? lookPitch : mode == ViewMode.ThirdPerson ? orbitPitch - 12f : 20f;

        /// <summary>Views looking down on the scene: the mouse cursor aims and picks, cutaways are on.</summary>
        public bool TopDownView => mode == ViewMode.Isometric || mode == ViewMode.TiltShift || mode == ViewMode.TopDown;
        /// <summary>Views that aim through the screen centre.</summary>
        public bool CrosshairView => !TopDownView;
        /// <summary>Extra pitch (degrees) on the iso and tilt-shift views: PgUp/PgDn or a vertical RMB drag.</summary>
        public float tilt;

        /// <summary>Horizontal camera heading; on-foot movement is relative to it.</summary>
        public float ViewYaw { get; private set; } = 45f;

        Transform target;
        OccluderFade fade;
        Transform eye;
        Renderer glass;
        VehicleDriver vehicle;
        PlayerCharacter player;
        InteriorSpace cutawayInterior;
        float targetScale = 1f;
        float yaw = 45f, orbitYaw, orbitPitch = 12f, lookYaw, lookPitch;
        Vector3 smoothFocus, lastTargetPos, velocity, filteredTarget, lastCamPos;
        Quaternion lastCamRot = Quaternion.identity;
        Vector3 rawCamPos;
        bool snapNext = true, cursorReleased;
        Vector3 hoodLocal, bumperLocal;

        public void SetTarget(Transform t)
        {
            if (glass) glass.enabled = true;
            if (vehicle) vehicle.SetDriverVisible(true);
            var gp = WastelandGame.Instance ? WastelandGame.Instance.Player : null;
            if (gp) gp.SetFirstPerson(false);
            target = t;
            if (!fade) fade = gameObject.AddComponent<OccluderFade>();
            fade.target = t; fade.cam = pixel ? pixel.GetComponent<Camera>() : null;
            vehicle = t ? t.GetComponent<VehicleDriver>() : null;
            player = t ? t.GetComponent<PlayerCharacter>() : null;
            eye = player ? player.Eye : t ? t.Find("DriverEye") : null;
            var g = t ? t.Find("Body/Glass") : null;
            glass = g ? g.GetComponent<Renderer>() : null;
            if (player) { orbitYaw = ViewYaw; lookYaw = ViewYaw; lookPitch = 0; }
            else { orbitYaw = 0; lookYaw = 0; lookPitch = 0; }
            targetScale = 1f;
            var body = t ? t.Find("Body") : null;
            if (vehicle && body && body.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh)
                targetScale = Mathf.Clamp(mf.sharedMesh.bounds.size.z / 4.6f, 0.8f, 2.4f);
            if (vehicle) VehicleMounts(t, body);
            if (player && (mode == ViewMode.Hood || mode == ViewMode.Bumper)) mode = ViewMode.FirstPerson;   // vehicle-only views
            lastTargetPos = t ? t.position : Vector3.zero;
            velocity = Vector3.zero;
            snapNext = true;
            Apply(0f);
        }

        float shake;

        /// <summary>Screen shake scaled by impact strength (m/s above the damage threshold).</summary>
        public void Shake(float strength) { if (GameSettings.Current.cameraShake) shake = Mathf.Min(1f, shake + strength * 0.08f); }

        static readonly ViewMode[] Order = { ViewMode.Isometric, ViewMode.TiltShift, ViewMode.TopDown, ViewMode.ThirdPerson, ViewMode.FirstPerson, ViewMode.Hood, ViewMode.Bumper };

        /// <summary>Hood and bumper camera spots (target-local) from the body mesh: on the bonnet just ahead of the
        /// windscreen, and low on the front bumper.</summary>
        void VehicleMounts(Transform t, Transform body)
        {
            var e = eye ? t.InverseTransformPoint(eye.position) : new Vector3(0f, 1.2f, 0.4f);
            hoodLocal = e + new Vector3(0f, 0.2f, 0.8f); bumperLocal = new Vector3(0f, 0.55f, 2.3f);
            if (!body || !body.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh || !mf.sharedMesh.isReadable) return;
            var b = mf.sharedMesh.bounds;
            var lo = t.InverseTransformPoint(body.TransformPoint(new Vector3(b.center.x, b.min.y, b.max.z)));
            float front = lo.z;
            float hoodY = float.MinValue, z0 = Mathf.Min(e.z + 0.5f, front - 0.3f);
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = t.InverseTransformPoint(body.TransformPoint(v));
                if (Mathf.Abs(w.x) < 0.35f && w.z > z0 && w.z < front && w.y > hoodY) hoodY = w.y;
            }
            if (hoodY == float.MinValue) hoodY = e.y - 0.2f;
            hoodLocal = new Vector3(0f, hoodY + 0.28f, Mathf.Min(z0 + 0.25f, front - 0.1f));
            bumperLocal = new Vector3(0f, lo.y + 0.3f, front + 0.08f);
        }

        public void Cycle()
        {
            int i = System.Array.IndexOf(Order, mode);
            do i = (i + 1) % Order.Length; while (player && (Order[i] == ViewMode.Hood || Order[i] == ViewMode.Bumper));
            mode = Order[i];
            if (player) { orbitYaw = ViewYaw; lookYaw = ViewYaw; }
            else { orbitYaw = lookYaw = 0; }
            lookPitch = 0;
            snapNext = true;
        }

        bool FreeLook => player && (mode == ViewMode.ThirdPerson || mode == ViewMode.FirstPerson);

        void Update()
        {
            var kb = Keyboard.current; var mouse = Mouse.current; var pad = Gamepad.current;
            var game = WastelandGame.Instance;
            if (game && game.Menus && game.Menus.IsOpen) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return; }
            if (Controls.Down(Controls.Act.View)) Cycle();
            if (pad != null && pad.buttonNorth.wasPressedThisFrame) Cycle();

            float zoom = 0f;
            if (mouse != null) { float s = mouse.scroll.ReadValue().y; if (Mathf.Abs(s) > 0.01f) zoom -= Mathf.Sign(s); }
            if (kb != null) zoom += ((Controls.Held(Controls.Act.ZoomOut) || kb.numpadMinusKey.isPressed ? 1f : 0f) - (Controls.Held(Controls.Act.ZoomIn) || kb.numpadPlusKey.isPressed ? 1f : 0f)) * Time.deltaTime * 8f;
            if (pad != null) zoom += ((pad.dpad.down.isPressed ? 1f : 0f) - (pad.dpad.up.isPressed ? 1f : 0f)) * Time.deltaTime * 8f;
            if (zoom != 0f) ApplyZoom(zoom);

            yaw += ((Controls.Held(Controls.Act.CamRight) ? 1f : 0f) - (Controls.Held(Controls.Act.CamLeft) ? 1f : 0f)) * 90f * Time.deltaTime;

            // cursor: locked for on-foot free look, Esc releases, click re-locks
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) cursorReleased = false;
            bool lockCursor = FreeLook && !cursorReleased;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;

            Vector2 look = Vector2.zero;
            var gs = GameSettings.Current;
            if (mouse != null && !WastelandGame.RadialBlocksLook && (lockCursor || mouse.rightButton.isPressed)) look = mouse.delta.ReadValue() * 0.15f * gs.mouseSensitivity;
            if (pad != null && !(game && game.Current && game.Current.GetComponent<MadMax.Vehicles.FlightModel>())) look += pad.rightStick.ReadValue() * 120f * Time.deltaTime * gs.mouseSensitivity;   // aircraft: the stick flies
            if (gs.invertY) look.y = -look.y;
            if (mode == ViewMode.ThirdPerson) { orbitYaw += look.x; orbitPitch = Mathf.Clamp(orbitPitch - look.y, -10f, 70f); }
            if (TopDownView)
            {
                // 2.5D views: RMB drag (or the pad's right stick when not aiming) turns and tilts, PgUp/PgDn tilt
                bool aim = game && game.Aiming, glass = game && game.Player && game.Player.Tool is BinocularsTool;
                Vector2 drag = Vector2.zero;
                if (mouse != null && mouse.rightButton.isPressed && !aim && !glass && !WastelandGame.RadialBlocksLook) drag = mouse.delta.ReadValue() * 0.25f * gs.mouseSensitivity;
                if (pad != null && !pad.leftTrigger.isPressed && !(game && game.Current)) drag += pad.rightStick.ReadValue() * 90f * Time.deltaTime * gs.mouseSensitivity;
                if (gs.invertY) drag.y = -drag.y;
                yaw += drag.x;
                tilt = Mathf.Clamp(tilt - drag.y * 0.5f + ((Controls.Held(Controls.Act.CamTiltUp) ? 1f : 0f) - (Controls.Held(Controls.Act.CamTiltDown) ? 1f : 0f)) * 40f * Time.deltaTime, -25f, 40f);
            }
            if (mode == ViewMode.FirstPerson || mode == ViewMode.Hood || mode == ViewMode.Bumper)
            {
                lookYaw += look.x;
                if (!player) lookYaw = Mathf.Clamp(lookYaw, -120f, 120f);
                lookPitch = Mathf.Clamp(lookPitch - look.y, -60f, 60f);
                if (!player && look == Vector2.zero)
                {
                    lookYaw = Mathf.Lerp(lookYaw, 0, 2f * Time.deltaTime);
                    lookPitch = Mathf.Lerp(lookPitch, 0, 2f * Time.deltaTime);
                }
            }
        }

        void ApplyZoom(float steps)
        {
            switch (mode)
            {
                case ViewMode.Isometric: isoSize = Mathf.Clamp(isoSize * Mathf.Pow(1.12f, steps), isoSizeRange.x, isoSizeRange.y); break;
                case ViewMode.TiltShift: tiltDistance = Mathf.Clamp(tiltDistance * Mathf.Pow(1.12f, steps), tiltDistanceRange.x, tiltDistanceRange.y); break;
                case ViewMode.ThirdPerson: thirdDistance = Mathf.Clamp(thirdDistance * Mathf.Pow(1.1f, steps), thirdDistanceRange.x, thirdDistanceRange.y); break;
                case ViewMode.FirstPerson: case ViewMode.Hood: case ViewMode.Bumper: fpsFov = Mathf.Clamp(fpsFov + steps * 4f, fpsFovRange.x, fpsFovRange.y); break;
                case ViewMode.TopDown: topSize = Mathf.Clamp(topSize * Mathf.Pow(1.12f, steps), topSizeRange.x, topSizeRange.y); break;
            }
        }

        void LateUpdate() => Apply(Time.deltaTime);

        void Apply(float dt)
        {
            if (!pixel || !target) return;
            var cam = pixel.GetComponent<Camera>();
            var ct = pixel.transform;

            if (dt > 0f)
            {
                var v = (target.position - lastTargetPos) / dt;
                velocity = Vector3.Lerp(velocity, Vector3.ClampMagnitude(v, 40f), 1f - Mathf.Exp(-4f * dt));
            }
            lastTargetPos = target.position;
            // low-pass the followed position: sub-centimetre suspension/physics noise must not shift the pixel grid
            var p = target.position;
            if (snapNext || (p - filteredTarget).sqrMagnitude > 0.0004f) filteredTarget = Vector3.Lerp(filteredTarget, p, snapNext ? 1f : 1f - Mathf.Exp(-25f * dt));
            Vector3 focus = filteredTarget + Vector3.up * (player ? 1.0f : 0.8f) + velocity * 0.25f;
            smoothFocus = snapNext ? focus : Vector3.Lerp(smoothFocus, focus, 1f - Mathf.Exp(-6f * dt));

            if (!vehicle && (mode == ViewMode.Hood || mode == ViewMode.Bumper)) mode = ViewMode.FirstPerson;
            bool perspectiveFog = CrosshairView;
            float mistAmount = MadMax.World.Atmosphere.Fog;
            var sky = MadMax.World.DayNight.Tint(Color.Lerp(fogColor, new Color(0.7f, 0.7f, 0.68f), Mathf.Clamp01(mistAmount * 1.3f)));
            RenderSettings.fog = perspectiveFog || mistAmount > 0.02f;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            if (perspectiveFog)
            {
                // up in an aircraft the far terrain carries the view out to the horizon haze
                float air = MadMax.World.FarTerrain.Aerial;
                RenderSettings.fogStartDistance = Mathf.Lerp(Mathf.Lerp(fogStart, 260f, air), 1f, mistAmount);
                RenderSettings.fogEndDistance = Mathf.Lerp(Mathf.Lerp(fogEnd, 620f, air), 18f, mistAmount * 0.85f);
            }
            else
            {
                // top-down views: the focus sits at the camera distance; haze there = half the mist, thicker towards the top of the screen
                float focusDepth = mode == ViewMode.TiltShift ? tiltDistance : 60f;
                RenderSettings.fogStartDistance = focusDepth - 12f;
                RenderSettings.fogEndDistance = focusDepth - 12f + 12f / Mathf.Max(0.02f, mistAmount * 0.5f);
            }
            MadMax.World.Atmosphere.SkyVisible = perspectiveFog;
            cam.backgroundColor = OccluderFade.Underground ? new Color(0.045f, 0.032f, 0.026f) : sky;   // earth around an underground cutaway
            if (fade)
            {
                fade.worldCut = TopDownView;
                fade.cutMode = mode == ViewMode.ThirdPerson ? 1 : TopDownView ? 2 : 0;
            }
            pixel.postMaterial = mode == ViewMode.TiltShift ? tiltShiftMaterial : null;
            bool fps = mode == ViewMode.FirstPerson;
            if (glass) glass.enabled = !fps;
            if (vehicle) vehicle.SetDriverVisible(!fps);
            var seated = WastelandGame.Instance ? WastelandGame.Instance.Player : null;
            if (player) player.SetFirstPerson(fps);
            else if (seated && vehicle && seated.SeatedIn == vehicle) seated.SetFirstPerson(fps);
            var interior = player ? player.Interior : null;
            if (cutawayInterior && cutawayInterior != interior) cutawayInterior.SetCutaway(false);
            if (interior) interior.SetCutaway(!fps);
            cutawayInterior = interior;

            switch (mode)
            {
                case ViewMode.Isometric:
                {
                    cam.orthographic = true;
                    cam.orthographicSize = isoSize * (BinocularsTool.Looking ? 1.8f : 1f);     // binoculars: see further
                    cam.nearClipPlane = 0.3f; cam.farClipPlane = 300f;
                    var r = Quaternion.Euler(player && player.Interior ? 68f : Mathf.Clamp(isoPitch + tilt, 12f, 85f), yaw, 0f);   // look down into interiors
                    ct.rotation = r;
                    var pos = smoothFocus - r * Vector3.forward * 60f;
                    ct.position = pos;
                    pixel.SetTruePosition(pos);
                    ViewYaw = yaw;
                    break;
                }
                case ViewMode.TiltShift:
                {
                    cam.orthographic = false;
                    cam.fieldOfView = tiltFov;
                    cam.nearClipPlane = 1f; cam.farClipPlane = 600f;
                    var r = Quaternion.Euler(player && player.Interior ? 70f : Mathf.Clamp(tiltPitch + tilt, 20f, 85f), yaw, 0f);
                    ct.SetPositionAndRotation(smoothFocus - r * Vector3.forward * tiltDistance, r);
                    ViewYaw = yaw;
                    break;
                }
                case ViewMode.TopDown:
                {
                    cam.orthographic = true;
                    cam.orthographicSize = topSize * (BinocularsTool.Looking ? 1.8f : 1f);
                    cam.nearClipPlane = 0.3f; cam.farClipPlane = 300f;
                    var r = Quaternion.Euler(90f, yaw, 0f);
                    ct.rotation = r;
                    var pos = smoothFocus + Vector3.up * 60f;
                    ct.position = pos;
                    pixel.SetTruePosition(pos);
                    ViewYaw = yaw;
                    break;
                }
                case ViewMode.Hood:
                case ViewMode.Bumper:
                {
                    cam.orthographic = false;
                    cam.fieldOfView = BinocularsTool.Looking ? 12f : mode == ViewMode.Bumper ? Mathf.Max(fpsFov, 64f) : fpsFov;
                    cam.nearClipPlane = 0.05f; cam.farClipPlane = Mathf.Max(Mathf.Lerp(fogEnd, 640f, MadMax.World.FarTerrain.Aerial) + 10f, 120f);
                    var local = mode == ViewMode.Hood ? hoodLocal : bumperLocal;
                    ct.SetPositionAndRotation(target.TransformPoint(local), target.rotation * Quaternion.Euler(lookPitch + (mode == ViewMode.Hood ? 3f : 0f), lookYaw, 0f));
                    ViewYaw = ct.eulerAngles.y;
                    break;
                }
                case ViewMode.ThirdPerson:
                {
                    cam.orthographic = false;
                    bool aim = player && WastelandGame.Instance && WastelandGame.Instance.Aiming;
                    cam.fieldOfView = BinocularsTool.Looking ? 14f : aim ? thirdFov * 0.75f : thirdFov;
                    cam.nearClipPlane = 0.1f; cam.farClipPlane = Mathf.Max(Mathf.Lerp(fogEnd, 640f, MadMax.World.FarTerrain.Aerial) + 10f, 120f);   // sky clouds sit up to ~100 m out
                    float heading = player ? 0f : Quaternion.LookRotation(Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized + Vector3.forward * 1e-4f).eulerAngles.y;
                    // on foot: over-the-shoulder at ~3.5 m, like most third-person games; vehicles: chase cam
                    float dist = player ? Mathf.Min(thirdDistance, interior ? 3f : 3.6f) * (aim ? 0.6f : 1f) : thirdDistance * targetScale;   // aiming: close over the shoulder
                    float height = player ? 1.6f : thirdHeight * Mathf.Sqrt(targetScale);
                    var r = Quaternion.Euler(interior ? Mathf.Max(orbitPitch, 40f) : orbitPitch, heading + orbitYaw, 0f);
                    var pivot = filteredTarget + Vector3.up * height + (player ? r * Vector3.right * 0.45f : Vector3.zero);
                    var desired = pivot - r * Vector3.forward * dist;
                    // pull in when a wall or rock is between the pivot and the camera (no cutaway needed)
                    if (Physics.SphereCast(pivot, 0.25f, desired - pivot, out var block, dist, ~0, QueryTriggerInteraction.Ignore) && !block.collider.transform.IsChildOf(target))
                        desired = pivot + (desired - pivot).normalized * Mathf.Max(0.6f, block.distance - 0.05f);
                    var pos = snapNext ? desired : Vector3.Lerp(rawCamPos, desired, 1f - Mathf.Exp(-10f * dt));
                    pos = ClampAboveGround(pos);
                    rawCamPos = pos;
                    ct.position = pos;
                    ct.rotation = Quaternion.LookRotation(filteredTarget + Vector3.up * height * 0.7f - ct.position);
                    ViewYaw = ct.eulerAngles.y;
                    break;
                }
                case ViewMode.FirstPerson:
                {
                    cam.orthographic = false;
                    var gg = WastelandGame.Instance;
                    float aimZoom = player && gg && gg.Aiming && gg.Player.Tool is RangedTool rt ? rt.aimZoom : 1f;   // down the sights
                    cam.fieldOfView = BinocularsTool.Looking ? 12f : fpsFov * aimZoom;
                    cam.nearClipPlane = 0.03f; cam.farClipPlane = Mathf.Max(Mathf.Lerp(fogEnd, 640f, MadMax.World.FarTerrain.Aerial) + 10f, 120f);   // sky clouds sit up to ~100 m out
                    var e = eye ? eye : target;
                    var rot = player ? Quaternion.Euler(lookPitch, lookYaw, 0f) : e.rotation * Quaternion.Euler(lookPitch, lookYaw, 0f);
                    ct.SetPositionAndRotation(e.position, rot);
                    ViewYaw = player ? lookYaw : ct.eulerAngles.y;
                    break;
                }
            }
            Underwater(cam, ct);
            // perspective deadband: sub-millimetre / sub-arcminute camera noise re-samples every low-res pixel (flicker)
            if (!cam.orthographic && !snapNext)
            {
                if ((ct.position - lastCamPos).sqrMagnitude < 0.0025f * 0.0025f && Quaternion.Angle(ct.rotation, lastCamRot) < 0.04f)
                    ct.SetPositionAndRotation(lastCamPos, lastCamRot);
            }
            lastCamPos = ct.position; lastCamRot = ct.rotation;
            if (shake > 0.001f)
            {
                float a = shake * shake * (mode == ViewMode.Isometric ? isoSize * 0.05f : mode == ViewMode.TopDown ? topSize * 0.03f : 0.25f);
                ct.position += ct.rotation * new Vector3(Random.Range(-a, a), Random.Range(-a, a), 0f);
                shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            }
            snapNext = false;
        }

        static readonly int HoleId = Shader.PropertyToID("_MadMaxWaterHole"), SeaId = Shader.PropertyToID("_MadMaxSeaLevel");

        /// <summary>Under the sea (user additions): a camera below the surface sees a short teal murk (darker deeper);
        /// in the top-down views a diver or a submerged submarine opens a window in the surface around it.</summary>
        void Underwater(Camera cam, Transform ct)
        {
            var t = DeformableTerrain.Instance;
            Shader.SetGlobalFloat(SeaId, MadMax.World.WorldGen.SeaLevel + MadMax.World.Weather.LakeRise);
            if (!t) return;
            var p = ct.position;
            float wl = t.WaterLevel(p.x, p.z);
            bool dry = (player && player.Interior && player.Interior.airtight) || MadMax.Building.AirPocket.Contains(p);
            bool cameraUnder = !float.IsNaN(wl) && p.y < wl - 0.05f && !dry;
            if (cameraUnder)
            {
                float depth = wl - p.y;
                var murk = Color.Lerp(new Color(0.08f, 0.26f, 0.3f), new Color(0.01f, 0.05f, 0.08f), Mathf.Clamp01(depth / 35f)) * Mathf.Lerp(1f, 0.25f, MadMax.World.DayNight.Darkness);
                RenderSettings.fog = true; RenderSettings.fogColor = murk;
                RenderSettings.fogStartDistance = 0.3f; RenderSettings.fogEndDistance = Mathf.Lerp(24f, 12f, Mathf.Clamp01(depth / 30f));
                cam.backgroundColor = murk;
            }
            // the window: only looking down from above at someone under the surface, cut where the line of sight to them
            // crosses the surface (the camera looks down at an angle: straight above them it would miss)
            var f = target ? target.position : p;
            bool inBase = player && !vehicle && MadMax.Building.AirPocket.Contains(player.transform.position + Vector3.up * 1.5f)
                          && player.transform.position.y < MadMax.World.WorldGen.SeaLevel + MadMax.World.Weather.LakeRise;   // dry in a sea base
            bool under = TopDownView && ((player && player.HeadUnder) || inBase || (vehicle && vehicle.TryGetComponent<MadMax.Vehicles.BoatModel>(out var bm) && bm.Submersion > 0.9f));
            if (under)
            {
                float surface = MadMax.World.WorldGen.SeaLevel + MadMax.World.Weather.LakeRise;
                float fl = t.WaterLevel(f.x, f.z); if (!float.IsNaN(fl)) surface = fl;
                var toCam = cam.orthographic ? -ct.forward : (ct.position - f).normalized;
                if (toCam.y > 0.05f && f.y < surface) f += toCam * ((surface - f.y) / toCam.y);
            }
            // the window also switches on the water's colour loss on everything below the surface (seen from inside it too)
            Shader.SetGlobalVector(HoleId, under || cameraUnder ? new Vector4(f.x, f.z, 11f, 0.12f) : Vector4.zero);
        }

        Vector3 ClampAboveGround(Vector3 p)
        {
            var t = DeformableTerrain.Instance;
            if (!t) return p;
            float h = t.Height(p.x, p.z) + groundClearance;
            if (p.y < h) p.y = h;
            return p;
        }
    }
}
