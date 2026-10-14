using MadMax.Voxel;
using UnityEngine;
using UnityEngine.Rendering;

namespace MadMax.Game
{
    /// <summary>A living face on a <see cref="HumanRig"/> (added by every rebuild): blinks (2.5–7 s apart, faster when
    /// scared or hurt, sometimes twice, none while limp: the eyes fall half shut) and eyes that look at what the
    /// <see cref="HumanAnimator"/> looks at (±25°, ahead of the head, with saccades) or wander and glance at the player.
    /// HD: each eye's skinned renderer is re-bound from the Head bone to an eye pivot under it (yaw/pitch about the
    /// eyeball, blink = squash along y). Voxel: a skin-coloured lid voxel over each painted eye while it is shut.
    /// The player's face also picks a look target (<see cref="AutoLook"/>): the aim point, the thing [E] would use, or
    /// where the third-person camera looks while standing. Beyond 30 m from the focus nothing runs.</summary>
    public class HumanFace : MonoBehaviour
    {
        /// <summary>Blinks started by every face this session (tests).</summary>
        public static int TotalBlinks;
        static readonly Mesh[] lidMeshes = new Mesh[4];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { TotalBlinks = 0; for (int i = 0; i < lidMeshes.Length; i++) lidMeshes[i] = null; }

        const float Reach = 30f, BlinkTime = 0.12f, EyeYawMax = 25f, EyePitchMax = 20f;

        /// <summary>World point the head looks at this tick (set by HumanAnimator; null = none).</summary>
        [System.NonSerialized] public Vector3? Target;
        [System.NonSerialized] public bool Talking;
        /// <summary>0..1 fear / fight / pain: blinks come quicker.</summary>
        [System.NonSerialized] public float Stress;
        /// <summary>The player's own look target (aim point, focused interactable, camera aim when idle); null otherwise.</summary>
        public Vector3? AutoLook { get; private set; }
        public int Blinks { get; private set; }
        /// <summary>1 open .. 0 shut.</summary>
        public float Openness { get; private set; } = 1f;
        /// <summary>Eye rotation in the head's frame, degrees (+pitch = down).</summary>
        public float EyeYaw { get; private set; }
        public float EyePitch { get; private set; }
        /// <summary>The eyes are on <see cref="Target"/> (or a glance at the player) this frame.</summary>
        public bool OnTarget { get; private set; }
        public bool HDEyes => pivotCount > 0;
        public bool VoxelLids => lidCount > 0;
        /// <summary>Between the eyes, world space.</summary>
        public Vector3 EyeCentre => head ? head.TransformPoint(mid) : transform.position + Vector3.up * 1.6f;
        /// <summary>Where the eyes point, world space.</summary>
        public Vector3 Gaze => head ? head.rotation * (Quaternion.Euler(EyePitch, EyeYaw, 0f) * Vector3.forward) : transform.forward;

        HumanRig rig;
        Transform head;
        bool isPlayer, neutral;
        readonly Transform[] pivots = new Transform[2];
        readonly Vector3[] centres = new Vector3[2];
        readonly Renderer[] lids = new Renderer[2];
        int pivotCount, lidCount;
        Renderer headRef;
        Vector3 mid;
        float nextBlink, blinkT = -1f, secondBlinkAt = -1f;
        float wantYaw, wantPitch, nextSaccade, wanderYaw, wanderPitch, glanceUntil, autoAt;
        Vector2 micro;
        bool glancing;

        /// <summary>Called by <see cref="HumanRig"/> once its bones and renderers exist (after every rebuild).</summary>
        public static void Attach(HumanRig rig)
        {
            if (!rig) return;
            var f = rig.GetComponent<HumanFace>();
            if (!f) f = rig.gameObject.AddComponent<HumanFace>();
            f.Bind(rig);
        }

        void Bind(HumanRig r)
        {
            rig = r;
            head = rig.bones.TryGetValue(BodyPart.Head, out var h) ? h : null;
            isPlayer = GetComponent<PlayerCharacter>() != null;
            pivotCount = lidCount = 0; headRef = null;
            pivots[0] = pivots[1] = null; lids[0] = lids[1] = null;
            Openness = 1f; EyeYaw = EyePitch = 0f; blinkT = -1f; secondBlinkAt = -1f; neutral = false;
            nextBlink = Time.time + Random.Range(0.5f, 4f);
            if (!head) return;
            mid = new Vector3(0f, 0.195f * rig.appearance.height, 0.1f);
            if (rig.IsHD) BindHD(); else BindVoxel();
        }

        /// <summary>HD eyes are rigid to the Head bone: give each its own pivot child at the same rest pose.</summary>
        void BindHD()
        {
            Vector3 sum = Vector3.zero;
            foreach (Transform c in rig.transform)
            {
                if (pivotCount >= 2) break;
                if (!c.name.StartsWith("Eye") || c.name.Contains("__")) continue;
                var smr = c.GetComponent<SkinnedMeshRenderer>();
                if (!smr || !smr.sharedMesh) continue;
                var bones = smr.bones;
                int hi = -1;
                for (int i = 0; i < bones.Length; i++) if (bones[i] == head) { hi = i; break; }
                var mesh = smr.sharedMesh;
                var bind = mesh.bindposes;
                if (hi < 0 || hi >= bind.Length) continue;
                var pv = new GameObject("EyePivot" + pivotCount).transform;
                pv.SetParent(head, false);
                bones[hi] = pv;
                smr.bones = bones;
                centres[pivotCount] = bind[hi].MultiplyPoint3x4(mesh.bounds.center);
                sum += centres[pivotCount];
                pivots[pivotCount++] = pv;
                if (!headRef) headRef = smr;
            }
            if (pivotCount > 0) mid = sum / pivotCount;
        }

        /// <summary>Voxel faces: the eye is one painted voxel each side (HumanDesign.BodyData); a lid voxel covers it.</summary>
        void BindVoxel()
        {
            foreach (Transform c in head) if (c.name == "Body_Head") { headRef = c.GetComponent<Renderer>(); break; }
            var a = rig.appearance;
            const float S = HumanDesign.S;
            float eyeY = 0.195f * a.height;
            int vy = int.MinValue;
            for (int y = Mathf.FloorToInt(eyeY / S) - 1; y <= Mathf.FloorToInt(eyeY / S) + 2; y++)
                if (Mathf.Abs(y * S - eyeY) < 0.012f) { vy = y; break; }
            if (vy == int.MinValue || !headRef) return;
            var mesh = LidMesh(a.skinTone);
            Vector3 sum = Vector3.zero;
            for (int side = -1; side <= 1; side += 2)
                for (int z = 6; z >= 2; z--)
                {
                    var p = new Vector3(side * S, vy * S, z * S);
                    if (HumanDesign.Sdf(BodyPart.Head, p, a) > 0f) continue;
                    var go = new GameObject("EyeLid", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(head, false);
                    go.transform.localPosition = p;
                    go.transform.localScale = Vector3.one * 1.12f;
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = rig.material;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.enabled = false;
                    lids[lidCount++] = mr;
                    sum += p;
                    break;
                }
            if (lidCount > 0) mid = sum / lidCount;
        }

        static Mesh LidMesh(int tone)
        {
            tone = Mathf.Clamp(tone, 0, lidMeshes.Length - 1);
            var m = lidMeshes[tone];
            if (m) return m;
            var g = new VoxelGrid();
            var col = HumanDesign.SkinTones[tone][0];
            g.Set(Vector3Int.zero, _ => col);
            m = VoxelMesher.ToMesh(VoxelMesher.BuildData(g, HumanDesign.S), "EyeLid");
            lidMeshes[tone] = m;
            return m;
        }

        bool HeadShown => headRef && headRef.enabled && !headRef.forceRenderingOff;

        void LateUpdate()
        {
            if (!rig || !head) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var g = WastelandGame.Instance;
            if (!isPlayer && g && (transform.position - g.FocusPos).sqrMagnitude > Reach * Reach)
            {
                if (!neutral) { Openness = 1f; EyeYaw = EyePitch = 0f; Apply(false); neutral = true; }
                return;
            }
            neutral = false;
            bool shown = HeadShown;
            if (isPlayer) PlayerLook(g, shown);
            bool limp = rig.Limp;

            if (limp) { blinkT = -1f; secondBlinkAt = -1f; Openness = Mathf.MoveTowards(Openness, 0.25f, dt * 2f); }
            else
            {
                if (blinkT < 0f && Time.time >= nextBlink) StartBlink();
                if (blinkT < 0f && secondBlinkAt > 0f && Time.time >= secondBlinkAt) { secondBlinkAt = -1f; StartBlink(); }
                if (blinkT >= 0f)
                {
                    blinkT += dt;
                    float u = blinkT / BlinkTime;
                    Openness = u < 0.4f ? 1f - u / 0.4f : u < 1f ? (u - 0.4f) / 0.6f : 1f;
                    if (u >= 1f) blinkT = -1f;
                }
                else Openness = Mathf.MoveTowards(Openness, 1f, dt * 6f);
            }

            Gazing(g, limp);
            float rate = 520f * dt;                                                             // saccades: snap, don't drift
            EyeYaw = Mathf.MoveTowards(EyeYaw, wantYaw, rate);
            EyePitch = Mathf.MoveTowards(EyePitch, wantPitch, rate);
            Apply(shown);
        }

        /// <summary>Blink now (a big shift of attention), unless a blink is under way or the body is limp.</summary>
        public void Blink() { if (blinkT < 0f && rig && !rig.Limp) StartBlink(); }

        void StartBlink()
        {
            Blinks++; TotalBlinks++;
            blinkT = 0f;
            float pace = (1f + 1.5f * Mathf.Clamp01(Stress)) * (Talking ? 1.3f : 1f);
            nextBlink = Time.time + Random.Range(2.5f, 7f) / pace;
            if (secondBlinkAt < 0f && Random.value < 0.15f) secondBlinkAt = Time.time + BlinkTime + 0.08f;
        }

        void Gazing(WastelandGame g, bool limp)
        {
            OnTarget = false;
            if (limp) { wantYaw = wantPitch = 0f; return; }
            bool saccade = Time.time >= nextSaccade;
            Vector3? at = Target;
            // nobody to look at: now and then a glance at the player close by in front
            if (!at.HasValue && !isPlayer && g && g.Player)
            {
                var pe = g.Player.Eye.position;
                if (saccade && !glancing)
                {
                    var to = pe - EyeCentre;
                    if (to.sqrMagnitude < 64f && Vector3.Angle(head.forward, to) < 70f && Random.value < 0.35f)
                    { glancing = true; glanceUntil = Time.time + Random.Range(0.6f, 1.8f); }
                }
                if (glancing && Time.time > glanceUntil) glancing = false;
                if (glancing) at = pe;
            }
            else glancing = false;

            if (saccade)
            {
                nextSaccade = Time.time + (at.HasValue ? Random.Range(0.4f, 1.6f) : Random.Range(0.7f, 2.8f));
                micro = Random.insideUnitCircle * 1.5f;
                if (Random.value < 0.45f) { wanderYaw = wanderPitch = 0f; }
                else { wanderYaw = Random.Range(-14f, 14f); wanderPitch = Random.Range(-5f, 7f); }
            }
            float yaw = wanderYaw, pitch = wanderPitch;
            if (at.HasValue)
            {
                var d = Quaternion.Inverse(head.rotation) * (at.Value - EyeCentre);
                float ty = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float tp = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                if (Mathf.Abs(ty) <= 60f && Mathf.Abs(tp) <= 45f)
                {
                    yaw = ty + micro.x; pitch = tp + micro.y;
                    OnTarget = Mathf.Abs(ty) <= EyeYawMax + 2f && Mathf.Abs(tp) <= EyePitchMax + 2f;
                }
            }
            yaw = Mathf.Clamp(yaw, -EyeYawMax, EyeYawMax);
            pitch = Mathf.Clamp(pitch, -EyePitchMax, EyePitchMax);
            // a big shift of gaze often comes with a blink
            if (blinkT < 0f && Mathf.Abs(yaw - wantYaw) + Mathf.Abs(pitch - wantPitch) > 18f && Random.value < 0.35f) StartBlink();
            wantYaw = yaw; wantPitch = pitch;
        }

        void Apply(bool shown)
        {
            if (pivotCount > 0)
            {
                var r = Quaternion.Euler(EyePitch, EyeYaw, 0f);
                var s = new Vector3(1f, Mathf.Max(0.08f, Openness), 1f);
                for (int i = 0; i < pivotCount; i++)
                {
                    var pv = pivots[i];
                    if (!pv) continue;
                    pv.localRotation = r;
                    pv.localScale = s;
                    pv.localPosition = centres[i] - r * Vector3.Scale(s, centres[i]);    // turn and squash about the eyeball
                }
            }
            bool shut = shown && Openness < 0.5f;
            for (int i = 0; i < lidCount; i++) if (lids[i] && lids[i].enabled != shut) lids[i].enabled = shut;
        }

        /// <summary>The player: aim point while aiming, the thing [E] would use within 6 m, the third-person camera's
        /// aim while standing still. Never in first person (the camera rides the head).</summary>
        void PlayerLook(WastelandGame g, bool shown)
        {
            if (Time.time < autoAt) return;
            autoAt = Time.time + 0.2f;
            AutoLook = null;
            if (!shown || !g || g.Player == null || g.Player.Rig != rig || rig.Limp) return;
            if (g.Aiming) { AutoLook = g.AimPoint; return; }
            if (g.Focused is Component c && c)
            {
                Vector3 at;
                var other = c.GetComponentInParent<HumanRig>();
                if (other && other != rig && other.Eye) at = other.Eye.position;
                else if (c.TryGetComponent<Collider>(out var col)) at = col.bounds.center;
                else at = c.transform.position + Vector3.up * 0.5f;
                if ((at - head.position).sqrMagnitude < 36f) { AutoLook = at; return; }
            }
            var cr = g.cameraRig;
            if (cr && cr.pixel && cr.mode == ViewMode.ThirdPerson && g.Player.Velocity.sqrMagnitude < 0.25f)
            {
                var ct = cr.pixel.transform;
                var flat = ct.forward; flat.y = 0f;
                if (Vector3.Angle(transform.forward, flat) < 100f) AutoLook = ct.position + ct.forward * 15f;
            }
        }
    }
}
