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
        /// <summary>0 clean .. 1 caked: a dust storm settles on the wash (2026-10-26).</summary>
        public float dust;
        /// <summary>Garments a storm tore off the line and where they lie (not yet picked up).</summary>
        public readonly List<(string id, Vector3 at)> lost = new List<(string, Vector3)>();
        /// <summary>Wind (m/s) from which a gust can tear a garment off; times it has happened (tests).</summary>
        public const float TearWind = 6.5f;
        public static int Torn;
        public static System.Func<bool> ForceStorm;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetWind() { Torn = 0; ForceStorm = null; }
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
            wash.Clear(); dust = 0f;
            foreach (var id in ids) if (wash.Count < 6) wash.Add(id);
            dryness = 0f;
            lastHour = DayNight.TotalDays * 24f;
            Changed();
        }

        /// <summary>Take the wash down (returns how dry it was).</summary>
        public float TakeIn()
        {
            float d = dryness;
            wash.Clear(); dryness = 0f; dust = 0f;
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

        /// <summary>A storm gust may tear a garment off (lighter when dry) and throw it downwind 8-30 m.</summary>
        void Gusts()
        {
            bool storm = ForceStorm != null ? ForceStorm() : Fx.Wind.magnitude > TearWind && WindDust.Gust > 0.55f;
            if (!storm || wash.Count == 0) return;
            if (Random.value > Time.deltaTime * 0.05f * wash.Count * Mathf.Lerp(0.4f, 1f, dryness) * (ForceStorm != null ? 20f : 1f)) return;
            TearOff(Random.Range(0, wash.Count));
        }

        /// <summary>Garment <paramref name="i"/> blows off downwind.</summary>
        public LostWash TearOff(int i)
        {
            if (i < 0 || i >= wash.Count) return null;
            var id = wash[i];
            var start = i < cloths.Count && cloths[i] ? cloths[i].position : transform.position + Vector3.up * 1.5f;
            wash.RemoveAt(i);
            var dir = Fx.Wind.sqrMagnitude > 0.01f ? Fx.Wind.normalized : transform.forward;
            dir = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * dir;
            var at = start + dir * Random.Range(8f, 30f);
            var t = DeformableTerrain.Instance;
            at.y = t ? t.HeightNoLoad(at.x, at.z) : transform.position.y;
            lost.Add((id, at));
            Torn++;
            Fx.Streak(start, dir * 6f + Vector3.up * 2f, 0.05f, Colour(id), 0.8f);
            MadMax.Audio.Sfx.Play("scratch", start, 0.4f, 1.4f, 14f);
            var w = LostWash.Spawn(this, id, at, Colour(id));
            var g = MadMax.Game.WastelandGame.Instance;
            if (g && g.Player && (g.Player.transform.position - transform.position).sqrMagnitude < 60f * 60f)
                g.Toast("THE WIND TORE YOUR " + MadMax.Items.ItemIds.Name(id) + " OFF THE LINE: IT BLEW DOWNWIND");
            Changed();
            return w;
        }

        /// <summary>A blown-off garment picked up while the wash is still out: back on the line, a little dusty.</summary>
        public void Return(string id)
        {
            Found(id);
            if (wash.Count < 6) wash.Add(id);
            dust = Mathf.Min(1f, dust + 0.15f);
            Changed();
        }

        /// <summary>No longer lying out there.</summary>
        public void Found(string id)
        {
            for (int i = 0; i < lost.Count; i++) if (lost[i].id == id) { lost.RemoveAt(i); break; }
            GetComponent<Placeable>()?.Dirty();
        }

        void ClearLost()
        {
            foreach (var w in Object.FindObjectsByType<LostWash>(FindObjectsSortMode.None)) if (w && w.line == this) Destroy(w.gameObject);
            lost.Clear();
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
                    if (Storms.Dust > 0.15f) { dust = Mathf.Min(1f, dust + Storms.Dust * 0.6f * (now - lastHour + 0.25f)); shownCount = -1; }
                    if (was < DripBelow) Spills.Pour(transform.position, MadMax.Items.ResourceType.Water, DripLitres * wash.Count * (1f - was), false);   // the wet wash darkens the ground under it
                    if ((was < 1f) != (dryness < 1f)) Changed();
                }
                Gusts();
            }
            if (shownCount != wash.Count) Show();
            Flap();
            Drips();
        }

        /// <summary>Wet below this dryness the wash drips (litres per garment per quarter hour into <see cref="Spills"/>).</summary>
        public const float DripBelow = 0.6f, DripLitres = 0.12f;
        Droplets drips;
        /// <summary>Drops falling off the wet wash now, and landed so far (tests).</summary>
        public bool Dripping => drips && drips.emitting;
        public int DripsLanded => drips ? drips.Landed : 0;

        /// <summary>Drops off the hems of the wet garments (2026-10-27).</summary>
        void Drips()
        {
            bool wet = Hung && dryness < DripBelow && PieceBits.Near(transform.position, 40f);
            if (!drips)
            {
                if (!wet || cloths.Count == 0) return;
                var o = new GameObject("WashDrips");
                o.transform.SetParent(transform, false);
                drips = Droplets.Attach(o, 6, (int i, out Vector3 at, out Vector3 v) =>
                {
                    int n = Mathf.Min(wash.Count, cloths.Count);
                    var c = n > 0 ? cloths[Random.Range(0, n)] : transform;
                    at = transform.InverseTransformPoint(c.TransformPoint(new Vector3(Random.Range(-0.16f, 0.16f), -0.56f, 0f)));
                    v = Vector3.zero;
                }, new Color(0.72f, 0.82f, 0.92f), 0.025f, 1.4f);
                drips.floorY = 0.02f;
            }
            drips.emitting = wet;
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
                var c = Colour(wash[i]) * Mathf.Lerp(0.62f, 1f, dryness);
                mats[i].SetColor("_BaseColor", Color.Lerp(c, DustTone, dust * 0.65f));
            }
        }

        static readonly Color DustTone = new Color(0.72f, 0.6f, 0.44f);

        /// <summary>A garment's main colour (its paint at the chest).</summary>
        public static Color Colour(string id)
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

        public string SaveState()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            foreach (var l in lost) sb.Append(sb.Length > 0 ? ";" : "").Append(l.id).Append('@').Append(l.at.x.ToString("0.#", ci)).Append(',').Append(l.at.z.ToString("0.#", ci));
            return dryness.ToString("0.##", ci) + "|" + string.Join(",", wash) + "|" + dust.ToString("0.##", ci) + "|" + sb;
        }
        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var f = s.Split('|');
            float.TryParse(f[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dryness);
            wash.Clear();
            if (f.Length > 1) foreach (var id in f[1].Split(',')) if (id.Length > 0) wash.Add(id);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (f.Length > 2) float.TryParse(f[2], System.Globalization.NumberStyles.Float, ci, out dust);
            ClearLost();
            if (f.Length > 3)
                foreach (var e in f[3].Split(';'))
                {
                    int at = e.IndexOf('@'), comma = e.LastIndexOf(',');
                    if (at <= 0 || comma < at) continue;
                    if (!float.TryParse(e.Substring(at + 1, comma - at - 1), System.Globalization.NumberStyles.Float, ci, out float x)
                        || !float.TryParse(e.Substring(comma + 1), System.Globalization.NumberStyles.Float, ci, out float z)) continue;
                    var id = e.Substring(0, at);
                    var t = DeformableTerrain.Instance;
                    var p = new Vector3(x, t ? t.HeightNoLoad(x, z) : transform.position.y, z);
                    lost.Add((id, p));
                    LostWash.Spawn(this, id, p, Colour(id));
                }
            shownCount = -1;
        }
    }
}
