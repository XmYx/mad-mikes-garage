using MadMax.Rendering;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    public enum ViewMode { Isometric, TiltShift, ThirdPerson, FirstPerson }

    /// <summary>All views render through the PixelArtCamera. Works for vehicles and the on-foot player.
    /// Scroll / +/- zoom, Z/C rotate (iso, tilt-shift), mouse look (RMB in vehicles, free on foot), V cycles views.</summary>
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
        public float LookPitch => mode == ViewMode.FirstPerson ? lookPitch : mode == ViewMode.ThirdPerson ? orbitPitch - 12f : 20f;

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
            lastTargetPos = t ? t.position : Vector3.zero;
            velocity = Vector3.zero;
            snapNext = true;
            Apply(0f);
        }

        float shake;

        /// <summary>Screen shake scaled by impact strength (m/s above the damage threshold).</summary>
        public void Shake(float strength) { shake = Mathf.Min(1f, shake + strength * 0.08f); }

        public void Cycle()
        {
            mode = (ViewMode)(((int)mode + 1) % 4);
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
            if (kb != null && kb.vKey.wasPressedThisFrame) Cycle();
            if (pad != null && pad.buttonNorth.wasPressedThisFrame) Cycle();

            float zoom = 0f;
            if (mouse != null) { float s = mouse.scroll.ReadValue().y; if (Mathf.Abs(s) > 0.01f) zoom -= Mathf.Sign(s); }
            if (kb != null) zoom += ((kb.minusKey.isPressed || kb.numpadMinusKey.isPressed ? 1f : 0f) - (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed ? 1f : 0f)) * Time.deltaTime * 8f;
            if (pad != null) zoom += ((pad.dpad.down.isPressed ? 1f : 0f) - (pad.dpad.up.isPressed ? 1f : 0f)) * Time.deltaTime * 8f;
            if (zoom != 0f) ApplyZoom(zoom);

            if (kb != null) yaw += ((kb.cKey.isPressed ? 1f : 0f) - (kb.zKey.isPressed ? 1f : 0f)) * 90f * Time.deltaTime;

            // cursor: locked for on-foot free look, Esc releases, click re-locks
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) cursorReleased = false;
            bool lockCursor = FreeLook && !cursorReleased;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;

            Vector2 look = Vector2.zero;
            if (mouse != null && !WastelandGame.RadialBlocksLook && (lockCursor || mouse.rightButton.isPressed)) look = mouse.delta.ReadValue() * 0.15f;
            if (pad != null) look += pad.rightStick.ReadValue() * 120f * Time.deltaTime;
            if (mode == ViewMode.ThirdPerson) { orbitYaw += look.x; orbitPitch = Mathf.Clamp(orbitPitch - look.y, -10f, 70f); }
            if (mode == ViewMode.FirstPerson)
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
                case ViewMode.FirstPerson: fpsFov = Mathf.Clamp(fpsFov + steps * 4f, fpsFovRange.x, fpsFovRange.y); break;
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

            bool perspectiveFog = mode == ViewMode.ThirdPerson || mode == ViewMode.FirstPerson;
            float mistAmount = MadMax.World.Atmosphere.Fog;
            var sky = MadMax.World.DayNight.Tint(Color.Lerp(fogColor, new Color(0.7f, 0.7f, 0.68f), Mathf.Clamp01(mistAmount * 1.3f)));
            RenderSettings.fog = perspectiveFog || mistAmount > 0.02f;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            if (perspectiveFog)
            {
                RenderSettings.fogStartDistance = Mathf.Lerp(fogStart, 1f, mistAmount);
                RenderSettings.fogEndDistance = Mathf.Lerp(fogEnd, 18f, mistAmount * 0.85f);
            }
            else
            {
                // top-down views: the focus sits at the camera distance; haze there = half the mist, thicker towards the top of the screen
                float focusDepth = mode == ViewMode.Isometric ? 60f : tiltDistance;
                RenderSettings.fogStartDistance = focusDepth - 12f;
                RenderSettings.fogEndDistance = focusDepth - 12f + 12f / Mathf.Max(0.02f, mistAmount * 0.5f);
            }
            MadMax.World.Atmosphere.SkyVisible = perspectiveFog;
            cam.backgroundColor = OccluderFade.Underground ? new Color(0.045f, 0.032f, 0.026f) : sky;   // earth around an underground cutaway
            if (fade)
            {
                fade.worldCut = mode == ViewMode.Isometric || mode == ViewMode.TiltShift;
                fade.cutMode = mode == ViewMode.FirstPerson ? 0 : mode == ViewMode.ThirdPerson ? 1 : 2;
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
                    var r = Quaternion.Euler(player && player.Interior ? 68f : isoPitch, yaw, 0f);   // look down into interiors
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
                    var r = Quaternion.Euler(player && player.Interior ? 70f : tiltPitch, yaw, 0f);
                    ct.SetPositionAndRotation(smoothFocus - r * Vector3.forward * tiltDistance, r);
                    ViewYaw = yaw;
                    break;
                }
                case ViewMode.ThirdPerson:
                {
                    cam.orthographic = false;
                    bool aim = player && WastelandGame.Instance && WastelandGame.Instance.Aiming;
                    cam.fieldOfView = BinocularsTool.Looking ? 14f : aim ? thirdFov * 0.75f : thirdFov;
                    cam.nearClipPlane = 0.1f; cam.farClipPlane = Mathf.Max(fogEnd + 10f, 120f);   // sky clouds sit up to ~100 m out
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
                    cam.nearClipPlane = 0.03f; cam.farClipPlane = Mathf.Max(fogEnd + 10f, 120f);   // sky clouds sit up to ~100 m out
                    var e = eye ? eye : target;
                    var rot = player ? Quaternion.Euler(lookPitch, lookYaw, 0f) : e.rotation * Quaternion.Euler(lookPitch, lookYaw, 0f);
                    ct.SetPositionAndRotation(e.position, rot);
                    ViewYaw = player ? lookYaw : ct.eulerAngles.y;
                    break;
                }
            }
            // perspective deadband: sub-millimetre / sub-arcminute camera noise re-samples every low-res pixel (flicker)
            if (!cam.orthographic && !snapNext)
            {
                if ((ct.position - lastCamPos).sqrMagnitude < 0.0025f * 0.0025f && Quaternion.Angle(ct.rotation, lastCamRot) < 0.04f)
                    ct.SetPositionAndRotation(lastCamPos, lastCamRot);
            }
            lastCamPos = ct.position; lastCamRot = ct.rotation;
            if (shake > 0.001f)
            {
                float a = shake * shake * (mode == ViewMode.Isometric ? isoSize * 0.05f : 0.25f);
                ct.position += ct.rotation * new Vector3(Random.Range(-a, a), Random.Range(-a, a), 0f);
                shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            }
            snapNext = false;
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
