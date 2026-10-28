using UnityEngine;

namespace MadMax.World
{
    /// <summary>A throwable while it is lit (2026-10-28): a molotov's rag burns (two flickering flame bits, a small orange
    /// light, black smoke off it, a low roar), a fuse glows and fizzes, a smoke can trails a thin wisp once its pin is
    /// out. Rides the held prop during the wind-up and the flying body after the release (<see cref="Light"/>); the
    /// charges' own sparks and sizzle in flight stay <see cref="Explosive"/>'s.</summary>
    public class ThrownLit : MonoBehaviour
    {
        public enum Kind { None, Rag, Fuse, Pin }
        public Kind kind;
        public bool inHand;
        /// <summary>Local point of the rag / fuse end.</summary>
        public Vector3 tip;
        Transform flameA, flameB;
        UnityEngine.Light glow;
        float smokeT, flick, unit = 1f;

        static readonly Color32 Outer = new Color32(255, 128, 32, 255), Inner = new Color32(255, 214, 96, 255), Ember = new Color32(255, 96, 24, 255);
        static readonly Color RagSmoke = new Color(0.12f, 0.1f, 0.09f, 0.75f), CanSmoke = new Color(0.78f, 0.8f, 0.74f, 0.6f);

        public static Kind KindOf(string id) => id == "throw_molotov" ? Kind.Rag : id == "throw_dynamite" || id == "throw_pipebomb" ? Kind.Fuse : id == "throw_smoke" ? Kind.Pin : Kind.None;

        /// <summary>Light <paramref name="id"/> on <paramref name="t"/> at its local <paramref name="tip"/> (null = the top of
        /// its mesh). Nothing for things that don't light (rocks).</summary>
        public static ThrownLit Light(Transform t, string id, bool inHand, Vector3? tip = null)
        {
            var k = KindOf(id);
            if (!t || k == Kind.None) return null;
            var l = t.gameObject.AddComponent<ThrownLit>();
            l.kind = k; l.inHand = inHand;
            if (tip.HasValue) l.tip = tip.Value;
            else if (t.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh) { var b = mf.sharedMesh.bounds; l.tip = new Vector3(b.center.x, b.max.y, b.center.z); }
            return l;
        }

        void Start()
        {
            unit = 1f / Mathf.Max(0.05f, transform.lossyScale.x);                                          // flame sizes in metres whatever the prop's scale
            if (kind == Kind.Rag)
            {
                flameA = MadMax.Building.PieceBits.Glow(transform, "RagFlame", tip + Vector3.up * 0.03f * unit, Vector3.one * 0.06f * unit, Outer);
                flameB = MadMax.Building.PieceBits.Glow(transform, "RagCore", tip + Vector3.up * 0.02f * unit, Vector3.one * 0.035f * unit, Inner);
                var go = new GameObject("RagLight");
                go.transform.SetParent(transform, false); go.transform.localPosition = tip + Vector3.up * 0.06f * unit;
                glow = go.AddComponent<UnityEngine.Light>();
                glow.type = LightType.Point; glow.color = new Color(1f, 0.58f, 0.22f); glow.range = inHand ? 3.5f : 4.5f; glow.intensity = 0f;
                glow.shadows = LightShadows.None;
            }
            else if (kind == Kind.Fuse) flameA = MadMax.Building.PieceBits.Glow(transform, "FuseEmber", tip, Vector3.one * 0.022f * unit, Ember);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            flick += dt;
            var at = transform.TransformPoint(tip);
            switch (kind)
            {
                case Kind.Rag:
                {
                    // the rag catches over a quarter second, then flutters; the flame stands up out of the neck whatever the spin
                    float grow = Mathf.Clamp01(flick / 0.25f);
                    float f = 0.75f + 0.25f * Mathf.Sin(flick * 31f) * Mathf.Sin(flick * 17.3f);
                    if (flameA)
                    {
                        flameA.rotation = Quaternion.identity; flameB.rotation = Quaternion.identity;
                        flameA.localScale = new Vector3(0.06f, 0.09f * f, 0.06f) * unit * grow;
                        flameB.localScale = new Vector3(0.035f, 0.05f * f, 0.035f) * unit * grow;
                        flameA.position = at + Vector3.up * 0.04f * grow; flameB.position = at + Vector3.up * 0.025f * grow;
                    }
                    if (glow) glow.intensity = (inHand ? 1.1f : 1.6f) * grow * (0.8f + 0.2f * f);
                    if ((smokeT -= dt) <= 0f) { smokeT = inHand ? 0.12f : 0.05f; Fx.Smoke(at + Vector3.up * 0.08f, Vector3.up * 0.6f, 0.07f, RagSmoke, 0.9f); }
                    MadMax.Audio.Sfx.Loop(this, "fire", 0.18f * grow, 1.7f, 12f);
                    break;
                }
                case Kind.Fuse:
                    if (flameA) flameA.localScale = Vector3.one * (0.018f + 0.008f * Mathf.Abs(Mathf.Sin(flick * 40f))) * unit;
                    if (inHand)
                    {
                        if (Random.value < dt * 24f) Fx.Sparks(at, Vector3.up, 1, new Color(1f, 0.8f, 0.4f));
                        MadMax.Audio.Sfx.Loop(this, "sizzle", 0.3f, 1.9f, 12f);
                    }
                    break;
                case Kind.Pin:
                    if (!inHand && (smokeT -= dt) <= 0f) { smokeT = 0.06f; Fx.Smoke(at, Vector3.up * 0.3f, 0.05f, CanSmoke, 0.8f); }
                    break;
            }
        }

        void OnDisable()
        {
            if (kind == Kind.Rag) MadMax.Audio.Sfx.Loop(this, "fire", 0f, 1f, 12f);
            else MadMax.Audio.Sfx.Loop(this, "sizzle", 0f, 1f, 12f);
        }
    }
}
