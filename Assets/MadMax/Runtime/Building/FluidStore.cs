using System.Collections.Generic;
using System.Globalization;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A placed store of liquid (makeshift pool, clay pond, storage drum) holding one blend up to its capacity.
    /// Water in a store with a pipe port belongs to the water network (it counts as a tank: pipes, taps, sprinklers and
    /// pumps use it); anything else (fuel, oil, coolant) is kept apart in the store and moves only by container, the
    /// drain or a spill. Open stores evaporate like a pool (<see cref="Spills.EvapMM"/>), catch rain (fuel left out in
    /// the rain soon smells off) and overflow, and freeze over; clay ponds seep a little and muddy their water. [T] opens
    /// the drain (onto the ground, or into a store below it); a broken store spills everything.</summary>
    public class FluidStore : MonoBehaviour, IInteractable, IPlaceState
    {
        public static readonly List<FluidStore> All = new List<FluidStore>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public string title = "STORE";
        public float capacity = 1000f;
        /// <summary>Open to the sky: evaporates, catches rain, freezes over, can be bathed in.</summary>
        public bool open = true;
        /// <summary>Share lost into the ground per game day (unlined earth).</summary>
        public float seepPerDay;
        /// <summary>Earth banks: water in it turns dirty.</summary>
        public bool muddy;
        /// <summary>Local box the liquid fills (bottom to brim), and the drain outlet.</summary>
        public Vector3 surfaceMin = new Vector3(-1f, 0.05f, -1f), surfaceMax = new Vector3(1f, 0.5f, 1f), drain = new Vector3(0f, 0.05f, 1.2f);
        public float drainRate = 2f;

        /// <summary>Contents that are not network water (litres + blend).</summary>
        public float litres;
        public readonly FluidMix mix = new FluidMix();
        public bool draining;

        UtilityNode node;
        Transform surface;
        Material surfaceMat;
        float tick, shadeAt, drainAcc;
        bool shaded;
        double lastDays = -1;
        static readonly System.Func<ResourceType, float> volatility = FluidProps.Volatility;

        public float Area => (surfaceMax.x - surfaceMin.x) * (surfaceMax.z - surfaceMin.z);
        /// <summary>Holding network water (or empty with a pipe port): the store is a tank of its water network.</summary>
        public bool OnNet => node && litres < 0.01f;
        public float Contents => OnNet ? node.Water : litres;
        public float Room => Mathf.Max(0f, capacity - Contents);
        public UtilityNode Node => node;

        /// <summary>What is in it (network water: clean, dirty or salty).</summary>
        public FluidMix Mix
        {
            get
            {
                if (!OnNet) return mix;
                var m = new FluidMix();
                float w = node.Water;
                if (w < 0.01f) return m;
                var dirtyKind = (WaterQuality.TaintOf(node) & WaterTaint.Salt) != 0 ? ResourceType.SeaWater : ResourceType.DirtyWater;
                if (node.dirty < 0.01f) m.Set(ResourceType.Water);
                else if (node.clean < 0.01f) m.Set(dirtyKind);
                else { m.Set(ResourceType.Water); m.Blend(node.clean, dirtyKind, node.dirty); }
                return m;
            }
        }

        public bool Frozen => open && Contents > 0.5f && FluidProps.Frozen(Mix, Weather.TemperatureAt(transform.position.z));

        void Awake() { node = GetComponent<UtilityNode>(); }
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = "Liquid";
            surface = go.transform;
            surface.SetParent(transform, false);
            surface.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var r = go.GetComponent<MeshRenderer>();
            surfaceMat = Fx.TransparentMaterial(null);
            r.sharedMaterial = surfaceMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Visual();
        }

        void OnDestroy() { if (surfaceMat) Destroy(surfaceMat); }

        public static bool WaterOnly(FluidMix m) => m != null && !m.Empty && m[ResourceType.Water] + m[ResourceType.DirtyWater] + m[ResourceType.SeaWater] >= 0.98f;

        /// <summary>Why <paramref name="m"/> can't go in (null = fine).</summary>
        public string Refuse(FluidMix m)
        {
            if (Frozen) return "FROZEN OVER";
            if (m == null || m.Empty || Contents < 0.5f) return null;
            if (OnNet) return WaterOnly(m) ? null : "IT HOLDS WATER: EMPTY IT FIRST";
            return m.MainFamily == mix.MainFamily ? null : "IT HOLDS " + mix.Label() + ": DRAIN IT FIRST";
        }

        /// <summary>Pour liquid in; returns the litres taken.</summary>
        public float Pour(FluidMix m, float l)
        {
            if (m == null || m.Empty || l <= 0f || Refuse(m) != null) return 0f;
            float add = Mathf.Min(l, Room);
            if (add <= 0f) return 0f;
            if (node && litres < 0.01f && WaterOnly(m))
            {
                float clean = muddy ? 0f : m[ResourceType.Water];
                node.clean += add * clean; node.dirty += add * (1f - clean);
                if (m[ResourceType.SeaWater] > 0.05f) node.taint |= WaterTaint.Salt;
                else if (clean < 0.99f) node.taint |= WaterTaint.Silt;
            }
            else
            {
                if (node && litres < 0.01f) { node.clean = 0f; node.dirty = 0f; }      // switches to holding something else
                mix.Blend(litres, m, add); litres += add;
            }
            GetComponent<Placeable>()?.Dirty();
            Visual();
            return add;
        }

        /// <summary>Draw up to <paramref name="l"/> into <paramref name="into"/>; returns the litres drawn.</summary>
        public float Draw(float l, FluidMix into)
        {
            if (l <= 0f || Frozen) return 0f;
            if (OnNet)
            {
                var kind = Mix;
                float got = UtilityGrid.Draw(node, l, out bool clean);
                if (got > 0f) { if (clean) into.Set(ResourceType.Water); else into.CopyFrom(kind.Empty ? new FluidMix(ResourceType.DirtyWater) : kind); }
                Visual();
                return got;
            }
            float t = Mathf.Min(l, litres);
            if (t <= 0f) return 0f;
            into.CopyFrom(mix);
            litres -= t;
            if (litres < 0.01f) { litres = 0f; mix.Clear(); }
            GetComponent<Placeable>()?.Dirty();
            Visual();
            return t;
        }

        /// <summary>Everything out onto the ground (the store broke).</summary>
        public void SpillAll()
        {
            var m = Mix.Clone();
            float l = Contents;
            if (l < 0.05f || m.Empty) return;
            if (OnNet) { node.clean = 0f; node.dirty = 0f; } else { litres = 0f; mix.Clear(); }
            Spills.Pour(transform.position + Vector3.up * 0.1f, m, l);
        }

        void Update()
        {
            if (node) node.waterCapacity = litres < 0.01f ? capacity : 0f;
            float dt = Time.deltaTime;
            if (draining) Drain(dt);
            if ((tick -= dt) > 0f) return;
            tick = 0.5f;
            double days = DayNight.TotalDays;
            float hours = lastDays < 0 ? 0f : (float)System.Math.Max(0.0, System.Math.Min(48.0, (days - lastDays) * 24.0));
            lastDays = days;
            if (Time.time >= shadeAt)
            {
                shadeAt = Time.time + 20f;
                var top = transform.TransformPoint(new Vector3(0f, surfaceMax.y + 0.4f, 0f));
                shaded = Physics.Raycast(top, Vector3.up, 30f, ~0, QueryTriggerInteraction.Ignore);
            }
            if (hours > 0f) Weathering(hours);
            Visual();
        }

        /// <summary>Evaporation, rain, seepage, mud and overflow over <paramref name="hours"/> of game time (tests call it directly).</summary>
        public void Weathering(float hours)
        {
            float have = Contents;
            if (open && have > 0.01f)
            {
                float e = Spills.EvapMM(transform.position, shaded) * hours * Area * (Frozen ? 0.05f : 1f);
                if (OnNet) { float k = Mathf.Max(0f, 1f - e / have); node.clean *= k; node.dirty *= k; }
                else litres = mix.Evaporate(litres, e, volatility);
            }
            if (open && Weather.Raining && !Weather.Snowing && !shaded)
            {
                float rain = Spills.RainMM * Area * hours * Mathf.Max(0.3f, Weather.Wetness);
                if (OnNet) { node.dirty += rain; node.taint |= WaterTaint.Silt; }
                else if (litres > 0.01f) { mix.Blend(litres, ResourceType.Water, rain); litres += rain; }
            }
            if (seepPerDay > 0f && Contents > 0.01f)
            {
                float k = Mathf.Max(0f, 1f - seepPerDay * hours / 24f);
                if (OnNet) { node.clean *= k; node.dirty *= k; } else litres *= k;
            }
            if (muddy && OnNet && node.clean > 0f) { float m = Mathf.Min(node.clean, node.clean * 0.2f * hours + 0.01f); node.clean -= m; node.dirty += m; node.taint |= WaterTaint.Silt; }
            // over the brim: the rest runs over the side
            float over = Contents - capacity;
            if (over > 0.5f)
            {
                var m = Mix.Clone();
                if (OnNet) { float k = capacity / Contents; node.clean *= k; node.dirty *= k; } else litres = capacity;
                Spills.Pour(transform.TransformPoint(drain), m, over);
            }
            if (litres < 0.01f && !OnNet) { litres = 0f; mix.Clear(); }
        }

        void Drain(float dt)
        {
            drainAcc += drainRate * dt;
            if (drainAcc < 0.5f) return;
            float want = drainAcc; drainAcc = 0f;
            if (Frozen) { draining = false; GetComponent<Placeable>()?.Dirty(); return; }
            var m = new FluidMix();
            float got = Draw(want, m);
            if (got <= 0.01f) { draining = false; GetComponent<Placeable>()?.Dirty(); return; }
            var at = transform.TransformPoint(drain);
            var below = Below(at, this);
            if (below && below.Refuse(m) == null) got -= below.Pour(m, got);
            if (got > 0f) Spills.Pour(at, m, got);
            var col = FluidProps.Colour(m); col.a = 0.8f;
            for (int i = 0; i < 2; i++) Fx.Smoke(at, Vector3.down * 0.5f + Random.insideUnitSphere * 0.3f, 0.05f, col, 0.4f);
            MadMax.Audio.Sfx.Play("pour", at, 0.35f, 0.8f, 14f, 0.4f);
        }

        /// <summary>Another store whose liquid box takes what falls at <paramref name="at"/>.</summary>
        public static FluidStore Below(Vector3 at, FluidStore not = null)
        {
            foreach (var s in All)
            {
                if (!s || s == not) continue;
                var q = s.transform.InverseTransformPoint(at);
                if (q.x >= s.surfaceMin.x - 0.2f && q.x <= s.surfaceMax.x + 0.2f && q.z >= s.surfaceMin.z - 0.2f && q.z <= s.surfaceMax.z + 0.2f
                    && q.y >= s.surfaceMin.y - 0.5f && q.y <= s.surfaceMax.y + 1.5f) return s;
            }
            return null;
        }

        /// <summary>The nearest store within <paramref name="reach"/> of a point (measured to its liquid box).</summary>
        public static FluidStore Near(Vector3 p, float reach, FluidStore not = null)
        {
            FluidStore best = null; float bd = reach;
            foreach (var s in All)
            {
                if (!s || s == not) continue;
                var q = s.transform.InverseTransformPoint(p);
                var c = new Vector3(Mathf.Clamp(q.x, s.surfaceMin.x, s.surfaceMax.x), Mathf.Clamp(q.y, s.surfaceMin.y, s.surfaceMax.y), Mathf.Clamp(q.z, s.surfaceMin.z, s.surfaceMax.z));
                float d = Vector3.Distance(s.transform.TransformPoint(c), p);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        void Visual()
        {
            if (!surface) return;
            float have = Contents, fill = Mathf.Clamp01(have / Mathf.Max(1f, capacity));
            bool show = have > 0.3f;
            if (surface.gameObject.activeSelf != show) surface.gameObject.SetActive(show);
            if (!show) return;
            surface.localPosition = new Vector3((surfaceMin.x + surfaceMax.x) * 0.5f, Mathf.Lerp(surfaceMin.y, surfaceMax.y, Mathf.Max(0.04f, fill)), (surfaceMin.z + surfaceMax.z) * 0.5f);
            surface.localScale = new Vector3(surfaceMax.x - surfaceMin.x, surfaceMax.z - surfaceMin.z, 1f);
            var col = FluidProps.Colour(Mix);
            col.a = Mathf.Clamp(col.a + 0.25f, 0.5f, 0.95f);
            if (Frozen) col = Color.Lerp(col, new Color(0.82f, 0.9f, 0.96f, 0.9f), 0.75f);
            surfaceMat.color = col;
        }

        // ------------------------------------------------------------------ interaction

        string Level() => Mathf.RoundToInt(Contents) + "/" + Mathf.RoundToInt(capacity) + " L" + (Contents > 0.3f ? " " + Mix.Label() : " EMPTY") + (Frozen ? " (FROZEN)" : "");

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            string use = MadMax.Game.Controls.Name(MadMax.Game.Controls.Act.Use), second = MadMax.Game.Controls.Name(MadMax.Game.Controls.Act.Second);
            var s = title + "  " + Level();
            if (g.HeldCanDef != null) s += "  [" + use + "] FILL / POUR";
            else if (WaterOnly(Mix) && Contents >= 0.5f && !Frozen) s += "  [" + use + "] " + (open && Contents >= 200f ? "BATHE" : "DRINK");
            if (Contents >= 0.5f) s += "  [" + second + "] " + (draining ? "CLOSE THE DRAIN" : "OPEN THE DRAIN");
            return s;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary)
            {
                if (!draining && Contents < 0.5f) return;
                if (!draining && Frozen) { g.Toast(title + " IS FROZEN OVER"); return; }
                draining = !draining;
                GetComponent<Placeable>()?.Dirty();
                g.Toast(draining ? "DRAINING THE " + title : "DRAIN CLOSED");
                return;
            }
            if (g.HeldCanDef != null) { g.OpenFluidChoice(g.HeldCan != null && g.HeldCan.Empty); return; }
            if (!WaterOnly(Mix) || Contents < 0.5f || Frozen) { g.Toast(title + ": " + Level()); return; }
            bool clean = Mix[ResourceType.Water] > 0.99f;
            if (open && Contents >= 200f)
            {
                Draw(5f, new FluidMix());
                g.Wash(clean ? 80f : 50f, clean ? "WASHED IN THE POOL" : "WASHED IN THE MURKY WATER");
                MadMax.Audio.Sfx.Play("splash", transform.position, 0.7f, 0.9f, 15f);
                return;
            }
            var taint = OnNet ? WaterQuality.TaintOf(node) : WaterTaint.Silt;
            var d = new FluidMix();
            float got = Draw(0.5f, d);
            if (got > 0.05f) g.DrinkTainted(got * 60f, d[ResourceType.Water] > 0.99f ? WaterTaint.None : taint);
        }

        // ------------------------------------------------------------------ state

        public string SaveState() => litres.ToString("0.###", CultureInfo.InvariantCulture) + ";" + mix.Save() + ";" + (draining ? "1" : "0");

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out litres);
            mix.Load(p.Length > 1 ? p[1] : "");
            if (mix.Empty) litres = 0f;
            draining = p.Length > 2 && p[2] == "1";
            Visual();
        }
    }
}
