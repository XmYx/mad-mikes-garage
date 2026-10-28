using MadMax.Items;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Throwing (2026-10-28 remodel; was an instant cube from the chest). A throw is a wind-up and a whip of the
    /// arm (<see cref="UseKind.Throw"/>, a gesture: walking and running go on) with the thing in the right hand: the
    /// molotov's rag catches as the arm goes back, a fuse is lit and fizzes, a smoke can's pin comes out with a click.
    /// It leaves the hand at the top of the whip (a swish), from the hand, along the facing (the camera's pitch in the
    /// over-the-shoulder views), and flies as itself — its item model, centred, spinning end over end, the bottle's rag
    /// burning, the fuse glowing (<see cref="ThrownLit"/>). Interrupted before the release (knocked down, a car taken, a
    /// tool job), the rag is smothered or the fuse pinched and it goes back in the pack.</summary>
    public partial class WastelandGame
    {
        string throwing;
        /// <summary>Throws started / released by the player (tests).</summary>
        public static int ThrowsStarted, ThrowsReleased;
        /// <summary>The last thrown body (tests: model, spin, lit rag).</summary>
        public static GameObject LastThrownBody;
        public static Vector3 LastReleaseAt;
        /// <summary>A throw is winding up (the item is in the hand, lit).</summary>
        public bool Throwing => throwing != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetThrowStatics() { ThrowsStarted = 0; ThrowsReleased = 0; LastThrownBody = null; LastReleaseAt = Vector3.zero; }

        void Throw(string id)
        {
            if (Current || throwing != null || !Vitals.Spend(6f)) return;
            if (!Inventory.TakeItem(id)) return;
            if (StartThrowGesture(id)) return;
            ReleaseThrow(id);                                                                       // no body to animate (low on the ground, setting off): at once
        }

        bool StartThrowGesture(string id)
        {
            if (!UseAnimatedNow(UseKind.Throw) || (use != null && !use.gesture)) return false;
            if (use != null) EndUse(use, true);
            var j = NewJob(null, new UseAnim(UseKind.Throw, 0f, id), () => ReleaseThrow(id));
            j.gesture = true;                                                                       // over walking and running; Use runs at the top of the whip
            j.pointWorld = Player.transform.position + Player.transform.forward * 0.6f + Vector3.up * 1.6f;
            BeginPose(j);
            use = j;
            throwing = id;
            ThrowsStarted++;
            var held = Player.HeldProp;
            if (held)
            {
                held.localScale = Vector3.one * 0.35f;                                              // world size, not the icon's 0.7
                held.localRotation = Quaternion.Euler(180f, 0f, 0f);                                // neck / fuse up out of the fist as the arm goes back
                ThrownLit.Light(held, id, true);
            }
            var at = Player.transform.position + Vector3.up * 1.5f;
            switch (ThrownLit.KindOf(id))
            {
                case ThrownLit.Kind.Rag: MadMax.Audio.Sfx.Play("strike", at, 0.5f, 1f, 10f); MadMax.Audio.Sfx.Play("fire", at, 0.35f, 1.8f, 14f); break;
                case ThrownLit.Kind.Fuse: MadMax.Audio.Sfx.Play("strike", at, 0.5f, 1.1f, 10f); break;
                case ThrownLit.Kind.Pin: MadMax.Audio.Sfx.Play("click", at, 0.6f, 1.5f, 10f); break;
            }
            return true;
        }

        void ReleaseThrow(string id)
        {
            throwing = null;
            if (!Player) return;
            var t = Player.transform;
            float power = 10f + Stats.Attribute(Attr.Strength) * 0.6f;
            var flat = t.forward; flat.y = 0f; flat = flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward;
            float lift = 4f + power * 0.15f;
            var cam = Camera.main;
            if (cameraRig && cameraRig.CrosshairView && cam)
            {
                var cf = cam.transform.forward;
                var cflat = new Vector3(cf.x, 0f, cf.z);
                if (cflat.sqrMagnitude > 0.01f && Vector3.Dot(cflat.normalized, flat) > 0.3f)
                {
                    flat = cflat.normalized;
                    lift += power * Mathf.Clamp(cf.y + 0.15f, -0.35f, 0.7f);                        // look up to lob it, down to bowl it
                }
            }
            var held = Player.HeldProp;
            var p = held ? held.position : t.position + Vector3.up * 1.6f + t.forward * 0.5f;
            p += flat * 0.25f;                                                                      // clear of the body
            var v = flat * power + Vector3.up * lift + Player.Velocity;
            Player.DropProp();
            var net = MadMax.Net.NetSession.Instance;
            bool authority = !(net && net.IsClient);
            SpawnThrown(id, p, v, authority, true);
            net?.SendThrow(id, p, v);
            Stats.Practice(Skill.Firearms, 1f);
            ThrowsReleased++;
            LastReleaseAt = p;
            MadMax.Audio.Sfx.Play("whoosh", p, 0.55f, 0.9f + power * 0.01f, 14f);
        }

        /// <summary>The throw pose: weight back and the arm cocked behind the head (0..0.7 of the reach), the whip over the
        /// shoulder to the release (0.7..1), then the follow-through across the body and back to rest.</summary>
        static ToolPose ThrowPose(float u, float f)
        {
            var rest = new ToolPose { armRX = -2f, armRZ = 9f, foreR = -14f, armLX = -2f, armLZ = -9f, foreL = -12f };
            var cocked = new ToolPose { chestX = -10f, chestY = 38f, armRX = -160f, armRY = -20f, armRZ = 48f, foreR = -95f, handRZ = -20f, armLX = -78f, armLZ = -14f, foreL = -18f, knees = 18f };
            var whip = new ToolPose { chestX = 16f, chestY = -18f, armRX = -112f, armRY = 10f, armRZ = 6f, foreR = -12f, handRZ = 30f, armLX = -20f, armLZ = -30f, foreL = -50f, knees = 12f };
            var through = new ToolPose { chestX = 30f, chestY = -34f, armRX = -36f, armRY = 30f, armRZ = -16f, foreR = -20f, handRZ = 10f, armLX = 6f, armLZ = -24f, foreL = -40f, knees = 16f };
            if (f <= 0f)
            {
                if (u < 0.7f) return ToolPose.Lerp(rest, cocked, Mathf.SmoothStep(0f, 1f, u / 0.7f));
                float w = (u - 0.7f) / 0.3f;
                return ToolPose.Lerp(cocked, whip, w * w);                                          // accelerating into the release
            }
            if (f < 0.4f) return ToolPose.Lerp(whip, through, Mathf.SmoothStep(0f, 1f, f / 0.4f));
            return ToolPose.Lerp(through, rest, Mathf.SmoothStep(0f, 1f, (f - 0.4f) / 0.6f));
        }

        /// <summary>A thrown thing as itself (molotov, dynamite, pipe bomb, smoke can, rock): its item model centred on a
        /// small body, a box collider round it, end over end. Only the authority's bottle starts the fire and only its
        /// charges blast for real (clients see a visual copy).</summary>
        public void SpawnThrown(string id, Vector3 p, Vector3 v, bool authority, bool byPlayer = false)
        {
            bool dynamite = id == "throw_dynamite", pipe = id == "throw_pipebomb", smoke = id == "throw_smoke", rock = id == "throw_rock";
            var go = new GameObject(dynamite ? "Dynamite" : pipe ? "PipeBomb" : smoke ? "SmokeBomb" : rock ? "Rock" : "Molotov");
            go.transform.SetPositionAndRotation(p, v.sqrMagnitude > 0.01f ? Quaternion.LookRotation(v) * Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity);   // neck first
            WorldItemModels.AddVisual(go.transform, id, propMaterial, out var b);
            for (int i = 0; i < go.transform.childCount; i++) go.transform.GetChild(i).localPosition -= b.center;   // spin about the middle
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.Max(b.size, Vector3.one * 0.05f);
            if (Player && Player.TryGetComponent<Collider>(out var pc)) Physics.IgnoreCollision(box, pc);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = rock ? 0.5f : 0.8f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = v;
            rb.angularVelocity = go.transform.right * Random.Range(9f, 14f) + Random.insideUnitSphere * 2f;
            var tip = new Vector3(0f, b.size.y * 0.5f, 0f);
            LastThrownBody = go;
            if (rock) { var rk = go.AddComponent<Molotov>(); rk.rock = true; rk.authority = authority; return; }
            ThrownLit.Light(go.transform, id, false, tip);
            if (smoke || dynamite || pipe)
            {
                var wick = new GameObject("Wick");                                                  // where Explosive spits its sparks
                wick.transform.SetParent(go.transform, false); wick.transform.localPosition = tip;
                var ex = go.AddComponent<Explosive>();
                ex.authority = authority;
                if (smoke) { ex.smoke = true; ex.fuse = 1.8f; return; }
                // a lit fuse, then the bang: dynamite breaks rock and digs craters, a pipe bomb shreds what stands near
                ex.source = Player ? Player.gameObject : null;
                ex.fuse = dynamite ? 4f : 3f; ex.radius = dynamite ? 3.2f : 2.2f; ex.power = dynamite ? 8f : 5f; ex.crater = dynamite ? 1.2f : 0.3f;
                return;
            }
            var mo = go.AddComponent<Molotov>(); mo.authority = authority; mo.byPlayer = byPlayer;
        }

        /// <summary>Per frame: a throw whose gesture ended before the release (knocked down, another job) puts the thing
        /// back in the pack, snuffed.</summary>
        void UpdateThrowing()
        {
            if (throwing == null) return;
            bool winding = use != null && use.anim.kind == UseKind.Throw && !use.applied;
            if (winding && (Boarding || Current || (Player && Player.SeatedIn))) { CancelUse(use, "~BOARDING"); winding = false; }   // walking to a car door: the throw stops
            if (winding) return;
            var id = throwing;
            throwing = null;
            Inventory.AddItem(id);
            Toast(ThrownLit.KindOf(id) == ThrownLit.Kind.Rag ? "YOU SMOTHER THE RAG" : ThrownLit.KindOf(id) == ThrownLit.Kind.Fuse ? "YOU PINCH OUT THE FUSE" : "YOU PUT IT AWAY");
        }
    }
}
