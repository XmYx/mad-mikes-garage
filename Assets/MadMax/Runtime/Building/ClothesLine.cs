using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Wash day (2026-10-23): a clothes line between two posts. [E] with water near (a pool / drum / tank of
    /// water within <see cref="WaterReach"/> m, which gives <see cref="WashLitres"/> L, or a lake) washes what the player
    /// wears — scrubbing mends a little of each garment's wear — and hangs the wash on the line, where it flaps in the
    /// wind and dries by sun and wind (not in rain; rain wets it again). [E] when dry takes it in: fresh clothes (clean
    /// and dry); taken in damp they soak you. See <c>WastelandGame.WashClothes</c>.</summary>
    public class ClothesLine : MonoBehaviour, IInteractable, IPlaceState
    {
        public static readonly List<ClothesLine> All = new List<ClothesLine>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public const float WaterReach = 10f, WashLitres = 10f, DryHours = 3f;
        /// <summary>Garment ids on the line (empty: nothing hung).</summary>
        public readonly List<string> wash = new List<string>();
        /// <summary>0 dripping .. 1 dry.</summary>
        public float dryness;
        float lastHour = -1f;
        readonly List<Transform> cloths = new List<Transform>();
        readonly List<Material> mats = new List<Material>();
        int shownCount = -1;

        public bool Hung => wash.Count > 0;
        public bool Dry => Hung && dryness >= 1f;

        public string Prompt(MadMax.Game.WastelandGame g) => !Hung ? "[E] WASH YOUR CLOTHES AND HANG THEM OUT"
            : Dry ? "[E] TAKE IN THE WASH (DRY)" : "THE WASH IS DRYING (" + (dryness < 0.35f ? "DRIPPING" : dryness < 0.75f ? "DAMP" : "NEARLY DRY") + ")  [E] TAKE IT IN";

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (!Hung) g.WashClothes(this);
            else g.TakeInWash(this);
        }

        /// <summary>Hang <paramref name="ids"/> out wet.</summary>
        public void Hang(IEnumerable<string> ids)
        {
            wash.Clear();
            foreach (var id in ids) if (wash.Count < 6) wash.Add(id);
            dryness = 0f;
            lastHour = DayNight.TotalDays * 24f;
            Changed();
        }

        /// <summary>Take the wash down (returns how dry it was).</summary>
        public float TakeIn()
        {
            float d = dryness;
            wash.Clear(); dryness = 0f;
            Changed();
            return d;
        }

        /// <summary>Drying per game hour: sun and wind dry it, night slows it, rain wets it again.</summary>
        public static float DryRate(Vector3 at)
        {
            if (Weather.Raining) return -0.5f;
            float r = 1f / DryHours * (1f + Fx.Wind.magnitude * 0.12f) * (DayNight.Darkness > 0.5f ? 0.35f : 1f);
            float t = Weather.TemperatureAt(at.z);
            return t < 0f ? r * 0.3f : r * Mathf.Clamp(0.6f + t / 40f, 0.6f, 1.4f);
        }

        void Changed() { shownCount = -1; GetComponent<Placeable>()?.Dirty(); }

        void Update()
        {
            if (Hung)
            {
                float now = DayNight.TotalDays * 24f;
                if (lastHour < 0f || now < lastHour) lastHour = now;
                else if (now - lastHour >= 0.25f)
                {
                    float was = dryness;
                    dryness = Mathf.Clamp01(dryness + DryRate(transform.position) * (now - lastHour));
                    lastHour = now;
                    if ((was < 1f) != (dryness < 1f)) Changed();
                }
            }
            if (shownCount != wash.Count) Show();
            Flap();
        }

        /// <summary>One cloth panel per garment, pegged along the line, in the garment's colour (darker while wet).</summary>
        void Show()
        {
            shownCount = wash.Count;
            while (cloths.Count < wash.Count)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Wash" + cloths.Count;
                Destroy(go.GetComponent<Collider>());
                var pivot = new GameObject("Peg" + cloths.Count).transform;
                pivot.SetParent(transform, false);
                go.transform.SetParent(pivot, false);
                go.transform.localPosition = new Vector3(0f, -0.28f, 0f);
                go.transform.localScale = new Vector3(0.36f, 0.56f, 0.03f);
                var m = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cloths.Add(pivot); mats.Add(m);
            }
            for (int i = 0; i < cloths.Count; i++)
            {
                bool on = i < wash.Count;
                cloths[i].gameObject.SetActive(on);
                if (!on) continue;
                float x = Mathf.Lerp(-0.7f, 0.7f, wash.Count == 1 ? 0.5f : i / (float)(wash.Count - 1));
                cloths[i].localPosition = new Vector3(x, 1.62f - 0.06f * (1f - Mathf.Abs(x) / 0.85f), 0f);   // the line sags in the middle
                mats[i].SetColor("_BaseColor", Colour(wash[i]) * Mathf.Lerp(0.62f, 1f, dryness));
            }
        }

        static Color Colour(string id)
        {
            var d = MadMax.Game.ClothingLibrary.Get(id);
            Color c = new Color(0.6f, 0.55f, 0.48f);
            if (d != null && d.paint != null) { try { c = d.paint(Vector3Int.zero, MadMax.Game.BodyPart.Chest, 0.5f); } catch { } }
            c.a = 1f;
            return c;
        }

        void Flap()
        {
            if (wash.Count == 0) return;
            float w = Fx.Wind.magnitude, wet = 1f - dryness * 0.6f;                                      // wet cloth hangs heavier
            for (int i = 0; i < wash.Count && i < cloths.Count; i++)
                cloths[i].localRotation = Quaternion.Euler(Mathf.Sin(Time.time * (1.8f + w * 0.5f) + i * 1.7f) * (4f + w * 6f) / wet + w * 3f / wet, 0f, 0f);
            if (dryness < 0.25f && Random.value < Time.deltaTime * 3f && wash.Count > 0)                // drips while sodden
            {
                var p = cloths[Random.Range(0, wash.Count)].position + Vector3.down * 0.56f;
                Fx.Streak(p, Vector3.down * 2f, 0.025f, new Color(0.7f, 0.82f, 0.95f), 0.4f);
            }
        }

        public string SaveState() => dryness.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "|" + string.Join(",", wash);
        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var f = s.Split('|');
            float.TryParse(f[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dryness);
            wash.Clear();
            if (f.Length > 1) foreach (var id in f[1].Split(',')) if (id.Length > 0) wash.Add(id);
            shownCount = -1;
        }
    }
}
