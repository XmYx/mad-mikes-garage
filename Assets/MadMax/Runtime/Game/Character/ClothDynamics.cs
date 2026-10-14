using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Secondary motion of HD garments and hair: the dangle chains of the HD rig (coat tails, hood, scarf ends,
    /// long straps, packs, hose, holster, hair; tools/blender/hd/character/hdlib.py chain_groups) are Transforms under the
    /// HumanRig body bones that the skinned pieces bind to. One component per rig runs a verlet chain per group after the
    /// animator: spring towards the animated rest pose (coat tails partly follow the thighs), gravity, damping, air drag
    /// from the real wind (<see cref="MadMax.World.Fx.Wind"/>, gusts) against the body's velocity with flutter, inertia of
    /// the body (the carrier's motion is dragged along at vehicle speeds, cabins are sheltered), collisions with capsules on
    /// the pelvis, legs, torso, shoulders and head. Beyond <see cref="PlayerReach"/> / <see cref="NpcReach"/> from the
    /// camera the chains snap to rest and cost nothing; paused time (menus) freezes them; a teleport resets them.</summary>
    [DefaultExecutionOrder(1000)]
    public class ClothDynamics : MonoBehaviour
    {
        public enum Kind { Coat, Hood, Scarf, Strap, Bag, Hose, Holster, Hair, Fringe }

        /// <summary>Chain groups of the HD rig, by bone-name prefix (bones are prefix + index, the end marker "&lt;last&gt;_tip").</summary>
        public static readonly string[] Groups = { "TailL", "TailR", "TailB", "Hood", "ScarfL", "ScarfR", "StrapL", "StrapR", "Bag", "Hose", "Holster", "HairA", "HairB", "HairC", "HairF" };

        public const float PlayerReach = 40f, NpcReach = 25f;

        struct Spec
        {
            public float freq, damp, gravity, drag, flutter, margin; public int colliders;
            public Spec(float f, float d, float g, float dr, float fl, float m, int c) { freq = f; damp = d; gravity = g; drag = dr; flutter = fl; margin = m; colliders = c; }
        }

        // collider bits: 0 pelvis, 1 thigh L, 2 thigh R, 3 shin L, 4 shin R, 5 chest, 6 shoulders, 7 head
        const int CPelvis = 1, CThighL = 2, CThighR = 4, CShinL = 8, CShinR = 16, CChest = 32, CShoulders = 64, CHead = 128;
        const int CLegs = CPelvis | CThighL | CThighR | CShinL | CShinR;

        static Spec SpecOf(Kind k) => k switch
        {
            Kind.Coat => new Spec(1.4f, 3.2f, 1f, 0.5f, 0.5f, 0.02f, CLegs),
            Kind.Hood => new Spec(3.2f, 6f, 0.6f, 0.4f, 0.3f, 0.015f, CChest | CShoulders | CHead),
            Kind.Scarf => new Spec(1.3f, 3f, 1f, 1.4f, 1f, 0.01f, CChest | CShoulders),
            Kind.Strap => new Spec(1.6f, 3.5f, 1f, 0.15f, 0.1f, 0.03f, CLegs | CChest),
            Kind.Bag => new Spec(2.6f, 6f, 1f, 0.08f, 0f, 0.02f, CChest | CPelvis),
            Kind.Hose => new Spec(1.5f, 3.5f, 1f, 0.3f, 0.2f, 0.015f, CChest | CShoulders | CHead),
            Kind.Holster => new Spec(3.2f, 7f, 1f, 0.1f, 0f, 0.02f, CPelvis | CThighR),
            Kind.Hair => new Spec(1.7f, 3.2f, 0.8f, 0.9f, 0.9f, 0.008f, CHead | CChest | CShoulders),
            _ => new Spec(3.4f, 6f, 0.4f, 0.5f, 0.6f, 0.006f, CHead),
        };

        public static Kind KindOf(string group) => group switch
        {
            "TailL" or "TailR" or "TailB" => Kind.Coat,
            "Hood" => Kind.Hood,
            "ScarfL" or "ScarfR" => Kind.Scarf,
            "StrapL" or "StrapR" => Kind.Strap,
            "Bag" => Kind.Bag,
            "Hose" => Kind.Hose,
            "Holster" => Kind.Holster,
            "HairF" => Kind.Fringe,
            _ => Kind.Hair,
        };

        /// <summary>The chain group a bone name belongs to (null = not a chain bone). Tips: "TailL3_tip" -> "TailL".</summary>
        public static string GroupOf(string bone)
        {
            if (string.IsNullOrEmpty(bone)) return null;
            int end = bone.EndsWith("_tip") ? bone.Length - 4 : bone.Length;
            int d = end;
            while (d > 0 && char.IsDigit(bone[d - 1])) d--;
            var head = bone.Substring(0, d);
            foreach (var g in Groups) if (g == head) return g;
            return null;
        }

        public static bool IsChainBone(string bone) => GroupOf(bone) != null;

        class Chain
        {
            public string group;
            public Kind kind;
            public Spec spec;
            public Transform parent, follow;
            public float followAmount;
            public Transform[] bones;
            public Vector3[] rest;          // n + 1 points in the parent's space (identity rest rotations)
            public Vector3[] dir;           // per bone: rest direction to the next point (bone-local = parent-local)
            public float[] len;
            public Vector3[] pos, prev;
            public float seed;
        }

        readonly List<Chain> chains = new List<Chain>();
        HumanRig rig;
        Transform pelvis;
        bool isPlayer, simulating, needReset = true;
        Vector3 lastRoot, rootVel;
        float exposure = 1f, shelterCheck, h = 1f, w = 1f;
        // collider capsules (world, refreshed each step)
        readonly Vector3[] ca = new Vector3[8], cb = new Vector3[8];
        readonly float[] cr = new float[8];

        static Camera cam;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { cam = null; }

        /// <summary>Simulated chain groups (tests).</summary>
        public int ChainCount => chains.Count;
        /// <summary>True while the chains are being simulated (in reach of the camera).</summary>
        public bool Simulating => simulating;

        /// <summary>The tip of a chain group in world space (tests), or null when the rig has no such chain.</summary>
        public Vector3? Tip(string group)
        {
            foreach (var c in chains) if (c.group == group) return c.pos[c.pos.Length - 1];
            return null;
        }

        /// <summary>Rest tip of a chain group in world space (where it hangs without motion).</summary>
        public Vector3? RestTip(string group)
        {
            foreach (var c in chains) if (c.group == group) return Target(c, c.rest.Length - 1);
            return null;
        }

        /// <summary>(Re)binds to the rig's freshly built chain transforms (identity local rotations). No chains = disabled.</summary>
        public void Setup(HumanRig r, Dictionary<string, Transform> chainBones)
        {
            rig = r;
            chains.Clear();
            pelvis = r.Bone(BodyPart.Pelvis);
            h = r.appearance != null ? r.appearance.height : 1f;
            w = r.appearance != null ? r.appearance.build : 1f;
            isPlayer = GetComponentInParent<PlayerCharacter>() != null;
            var byGroup = new Dictionary<string, List<Transform>>();
            var tips = new Dictionary<string, Transform>();
            foreach (var kv in chainBones)
            {
                if (!kv.Value) continue;
                var g = GroupOf(kv.Key);
                if (g == null) continue;
                if (kv.Key.EndsWith("_tip")) { tips[g] = kv.Value; continue; }
                if (!byGroup.TryGetValue(g, out var l)) byGroup[g] = l = new List<Transform>();
                l.Add(kv.Value);
            }
            foreach (var kv in byGroup)
            {
                var list = kv.Value;
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));          // TailL1 < TailL2 < TailL3
                if (!tips.TryGetValue(kv.Key, out var tip) || !tip) continue;
                int n = list.Count;
                var c = new Chain { group = kv.Key, kind = KindOf(kv.Key), bones = list.ToArray(), parent = list[0].parent };
                c.spec = SpecOf(c.kind);
                c.rest = new Vector3[n + 1]; c.dir = new Vector3[n]; c.len = new float[n];
                c.pos = new Vector3[n + 1]; c.prev = new Vector3[n + 1];
                c.rest[0] = list[0].localPosition;
                for (int i = 0; i < n; i++)
                {
                    var next = i + 1 < n ? list[i + 1].localPosition : tip.localPosition;
                    c.rest[i + 1] = c.rest[i] + next;
                    c.dir[i] = next;
                    c.len[i] = next.magnitude;
                }
                if (kv.Key == "TailL") { c.follow = r.Bone(BodyPart.ThighL); c.followAmount = 0.55f; }
                else if (kv.Key == "TailR") { c.follow = r.Bone(BodyPart.ThighR); c.followAmount = 0.55f; }
                else if (kv.Key == "Holster") { c.follow = r.Bone(BodyPart.ThighR); c.followAmount = 0.75f; }
                c.seed = chains.Count * 7.31f + (transform.position.x * 3.7f % 50f);
                chains.Add(c);
            }
            needReset = true;
            simulating = false;
            enabled = chains.Count > 0;
        }

        /// <summary>Chains back to rest (teleports, respawns, rebuilt clothes).</summary>
        public void ResetChains() { needReset = true; }

        void OnDisable()
        {
            foreach (var c in chains) foreach (var b in c.bones) if (b) b.localRotation = Quaternion.identity;
            simulating = false;
        }

        Vector3 Target(Chain c, int k)
        {
            var p = c.rest[k];
            if (c.follow && c.followAmount > 0f)
                p = c.rest[0] + Quaternion.Slerp(Quaternion.identity, c.follow.localRotation, c.followAmount) * (p - c.rest[0]);
            return c.parent.TransformPoint(p);
        }

        bool InReach()
        {
            if (!cam || !cam.isActiveAndEnabled) cam = Camera.main;
            if (!cam) return true;
            float reach = isPlayer ? PlayerReach : NpcReach;
            return (cam.transform.position - pelvis.position).sqrMagnitude < reach * reach;
        }

        void LateUpdate()
        {
            if (!rig || !pelvis || chains.Count == 0) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;                                                      // paused (menus)
            if (!InReach())
            {
                if (simulating) { foreach (var c in chains) foreach (var b in c.bones) if (b) b.localRotation = Quaternion.identity; simulating = false; }
                needReset = true;
                return;
            }
            simulating = true;
            dt = Mathf.Min(dt, 0.05f);
            var root = pelvis.position;
            var delta = root - lastRoot;
            lastRoot = root;
            if (needReset || delta.sqrMagnitude > 9f) { Reset(); return; }
            rootVel = Vector3.Lerp(rootVel, delta / dt, Mathf.Clamp01(dt * 12f));
            if ((shelterCheck -= dt) <= 0f)
            {
                shelterCheck = 0.5f;
                // in a cabin (a vehicle that is not a bike) the air is still
                var vd = GetComponentInParent<MadMax.Vehicles.VehicleDriver>();
                exposure = vd && !vd.GetComponent<MadMax.Vehicles.BikeBalance>() ? 0.12f : 1f;
            }
            // at vehicle speed the particles ride along (the carrier's motion is not inertia the cloth would show)
            float speed = rootVel.magnitude;
            float carry = Mathf.InverseLerp(5f, 11f, speed);
            var shift = delta * carry;
            var wind = MadMax.World.Fx.Wind * (1f + 0.6f * MadMax.World.WindDust.Gust) - rootVel;
            float windMag = wind.magnitude;
            int steps = dt > 0.026f ? 2 : 1;
            float sdt = dt / steps;
            float t = Time.time;
            UpdateColliders();
            for (int ci = 0; ci < chains.Count; ci++)
            {
                var c = chains[ci];
                var s = c.spec;
                float k = (2f * Mathf.PI * s.freq) * (2f * Mathf.PI * s.freq);
                float damp = Mathf.Clamp01(1f - s.damp * sdt);
                // gusty flutter: per chain noise scaled by the relative air speed
                float n1 = Mathf.PerlinNoise(t * 3.1f + c.seed, 0.37f) - 0.5f, n2 = Mathf.PerlinNoise(0.71f, t * 2.7f + c.seed) - 0.5f;
                var air = (wind * s.drag + new Vector3(n1, n2 * 0.6f, -n1 * 0.5f + n2) * (windMag * s.flutter * 2f)) * exposure;
                if (air.sqrMagnitude > 900f) air = air.normalized * 30f;
                var acc = Physics.gravity * s.gravity + air;
                int n = c.pos.Length;
                for (int i = 1; i < n; i++) { c.pos[i] += shift; c.prev[i] += shift; }
                for (int step = 0; step < steps; step++)
                {
                    c.pos[0] = Target(c, 0);
                    for (int i = 1; i < n; i++)
                    {
                        var p = c.pos[i];
                        var v = (p - c.prev[i]) * damp;
                        c.prev[i] = p;
                        var tgt = Target(c, i);
                        p += v + (acc + (tgt - p) * k) * (sdt * sdt);
                        // length: keep the segment, then out of the body
                        var d = p - c.pos[i - 1];
                        float m = d.magnitude;
                        p = m > 1e-5f ? c.pos[i - 1] + d * (c.len[i - 1] / m) : tgt;
                        p = Collide(p, s.colliders, s.margin);
                        // never further from the rest pose than the chain is long from its root (no tangles)
                        var off = p - tgt;
                        float lim = 0.9f * i * c.len[0] + 0.05f;
                        if (off.sqrMagnitude > lim * lim) p = tgt + off.normalized * lim;
                        c.pos[i] = p;
                    }
                }
                Apply(c);
            }
        }

        void Reset()
        {
            needReset = false;
            lastRoot = pelvis.position;
            rootVel = Vector3.zero;
            foreach (var c in chains)
            {
                foreach (var b in c.bones) if (b) b.localRotation = Quaternion.identity;
                for (int i = 0; i < c.pos.Length; i++) c.pos[i] = c.prev[i] = Target(c, i);
                Apply(c);
            }
        }

        /// <summary>Bone rotations from the particles: each bone turns its rest direction onto its segment.</summary>
        static void Apply(Chain c)
        {
            for (int i = 0; i < c.bones.Length; i++)
            {
                var b = c.bones[i];
                if (!b) return;
                var parentRot = b.parent.rotation;
                var restDir = parentRot * c.dir[i];
                var want = c.pos[i + 1] - b.position;
                if (want.sqrMagnitude < 1e-8f || restDir.sqrMagnitude < 1e-8f) { b.localRotation = Quaternion.identity; continue; }
                b.rotation = Quaternion.FromToRotation(restDir, want) * parentRot;
            }
        }

        void UpdateColliders()
        {
            var B = rig.bones;
            void Cap(int i, BodyPart part, Vector3 a, Vector3 b, float r)
            {
                if (!B.TryGetValue(part, out var t) || !t) { cr[i] = -1f; return; }
                if (t.localScale.y < 0.5f) { cr[i] = -1f; return; }                     // a lost limb (scaled to 0)
                ca[i] = t.TransformPoint(a); cb[i] = t.TransformPoint(b); cr[i] = r;
            }
            Cap(0, BodyPart.Pelvis, new Vector3(-0.07f * w, -0.02f, 0f), new Vector3(0.07f * w, -0.02f, 0f), 0.12f * w);
            Cap(1, BodyPart.ThighL, Vector3.zero, new Vector3(0f, -0.4f * h, 0f), 0.07f * w);
            Cap(2, BodyPart.ThighR, Vector3.zero, new Vector3(0f, -0.4f * h, 0f), 0.07f * w);
            Cap(3, BodyPart.ShinL, Vector3.zero, new Vector3(0f, -0.38f * h, 0f), 0.05f * w);
            Cap(4, BodyPart.ShinR, Vector3.zero, new Vector3(0f, -0.38f * h, 0f), 0.05f * w);
            Cap(5, BodyPart.Chest, new Vector3(0f, 0.08f * h, 0f), new Vector3(0f, 0.3f * h, -0.01f), 0.14f * w);
            Cap(6, BodyPart.Chest, new Vector3(-0.15f * w, 0.37f * h, -0.02f), new Vector3(0.15f * w, 0.37f * h, -0.02f), 0.06f);
            Cap(7, BodyPart.Head, new Vector3(0f, 0.17f * h, -0.01f), new Vector3(0f, 0.25f * h, -0.01f), 0.08f);
        }

        Vector3 Collide(Vector3 p, int mask, float margin)
        {
            for (int i = 0; i < 8; i++)
            {
                if ((mask & (1 << i)) == 0 || cr[i] < 0f) continue;
                var a = ca[i]; var ab = cb[i] - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                var q = a + ab * t;
                var d = p - q;
                float r = cr[i] + margin, m = d.sqrMagnitude;
                if (m < r * r && m > 1e-10f) p = q + d * (r / Mathf.Sqrt(m));
            }
            return p;
        }
    }
}
