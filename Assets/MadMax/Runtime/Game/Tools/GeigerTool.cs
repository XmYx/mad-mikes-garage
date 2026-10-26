using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Geiger counter: clicks faster with the dose rate (fallout zones, waste barrels, hot loot and ore)
    /// and shows the reading on the HUD while held. Held out in front like a torch (2026-10-23), its dial needle swings
    /// with the reading (log scale, kicking on each click) and a red lamp on the case blinks with the counts.</summary>
    public class GeigerTool : HandTool
    {
        public static float Reading { get; private set; } = -1f;
        float nextClick, needle, kick;
        Transform needleT, lampT;
        Material lampMat;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Reading = -1f;

        public override void Strike(PlayerCharacter user) { }

        /// <summary>Needle angle (degrees from the left stop; tests).</summary>
        public float NeedleAngle => needle;

        void Start()
        {
            float s = VoxelMesher.DefaultSize;
            needleT = Part("Needle", new Vector3(0f, -3.4f * s, 2.56f * s), new Vector3(0.012f, 1.7f * s, 0.01f), new Vector3(0f, 0.85f * s, 0f), Color.black);
            lampT = Part("Lamp", new Vector3(0.9f * s, -0.5f * s, 1.56f * s), new Vector3(0.03f, 0.03f, 0.02f), Vector3.zero, new Color(0.35f, 0.05f, 0.04f));
            lampMat = lampT.GetComponentInChildren<MeshRenderer>().sharedMaterial;
        }

        /// <summary>A small unlit cube under a pivot at <paramref name="pivot"/> (the cube offset by <paramref name="offset"/>).</summary>
        Transform Part(string name, Vector3 pivot, Vector3 size, Vector3 offset, Color c)
        {
            var p = new GameObject(name).transform;
            p.SetParent(transform, false);
            p.localPosition = pivot;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(p, false);
            go.transform.localPosition = offset;
            go.transform.localScale = size;
            var m = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return p;
        }

        void OnDestroy()
        {
            if (lampMat) Destroy(lampMat);
            if (needleT) { var r = needleT.GetComponentInChildren<MeshRenderer>(); if (r) Destroy(r.sharedMaterial); }
        }

        /// <summary>The needle: 0..120° over 0.01..10 (log), a kick on each count, damped like a moving-coil meter.</summary>
        void Dial(float dt)
        {
            float target = Reading <= 0.005f ? 0f : Mathf.Clamp01((Mathf.Log10(Mathf.Max(Reading, 0.01f)) + 2f) / 3f) * 120f;
            kick = Mathf.MoveTowards(kick, 0f, 60f * dt);
            needle = Mathf.Lerp(needle, Mathf.Min(120f, target + kick), 1f - Mathf.Exp(-7f * dt));
            if (needleT) needleT.localRotation = Quaternion.Euler(0f, 0f, 60f - needle);
            if (lampMat)
            {
                float on = Mathf.Clamp01(kick / 6f);
                lampMat.SetColor("_BaseColor", Color.Lerp(new Color(0.35f, 0.05f, 0.04f), new Color(1f, 0.25f, 0.15f), on));
            }
        }

        void Update()
        {
            var g = WastelandGame.Instance;
            if (!g || g.World == null) { Reading = -1f; return; }
            var p = transform.position;
            float dose = g.World.Radiation(p.x, p.z);
            // a hot spot reads louder with the probe pointed at it: sweep to find the loot or the barrel
            var fwd = g.Player ? g.Player.transform.forward : transform.forward;
            foreach (var h in Hazard.All)
            {
                if (!h) continue;
                var to = h.transform.position - p; to.y = 0f;
                float aim = to.sqrMagnitude > 0.25f ? 0.45f + 0.55f * Mathf.Max(0f, Vector3.Dot(fwd, to.normalized)) : 1f;
                dose += h.radiation * aim * Mathf.Clamp01(1f - Vector3.Distance(h.transform.position, p) / 8f);
            }
            // uranium deposits glow faintly through the ground
            float ore = g.World.OreAt(p.x, p.z, out var kind);
            if (kind == MadMax.Items.ResourceType.UraniumOre) dose += ore * 0.35f;
            Reading = Mathf.Lerp(Reading < 0f ? dose : Reading, dose, 1f - Mathf.Exp(-4f * Time.deltaTime));
            Dial(Time.deltaTime);
            if (Time.time >= nextClick && Reading > 0.005f)
            {
                kick = Mathf.Min(14f, kick + 6f);
                MadMax.Audio.Sfx.Play("click", p, Mathf.Clamp01(0.3f + Reading), Random.Range(1.6f, 2.4f), 6f, 0f);
                nextClick = Time.time + Random.Range(0.02f, 0.06f) / Mathf.Clamp(Reading, 0.02f, 2f);
            }
        }

        void OnDisable() => Reading = -1f;
    }
}
