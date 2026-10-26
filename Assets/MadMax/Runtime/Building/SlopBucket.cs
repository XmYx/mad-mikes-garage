using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>The pigs' own trough (2026-10-18): a slop bucket by the kitchen. Every game hour it takes the rotten food
    /// out of the larders and chests within <see cref="Gather"/> m (what spoiled in the pantry goes in the bucket, not
    /// on the floor); [E] tips the pack's rotten food and offcuts in. Kept pigs, dogs and chickens nearby come and eat
    /// from it (<c>Animal.Scraps</c>) — a scrap a visit, counted toward their day's feed. The slop rises in the bucket
    /// and draws flies once it is half full.</summary>
    public class SlopBucket : MonoBehaviour, IInteractable, IPlaceState
    {
        public static readonly List<SlopBucket> All = new List<SlopBucket>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public const int Cap = 12;
        public const float Gather = 6f, Reach = 14f;
        /// <summary>Scraps in the bucket.</summary>
        public int scraps;
        /// <summary>What goes in: spoiled food and the butcher's leavings.</summary>
        public static readonly string[] Slops = { "food_rotten", "misc_offal", "food_scraps" };
        float lastHour = -1f;
        /// <summary>Souring (2026-10-23): 0 fresh .. 1 sour; rises while it is half full in the warm sun, falls as it is
        /// eaten down. Sour slop draws twice the flies; the pigs still eat it.</summary>
        public float sour;
        /// <summary>Rain water standing in it (L, up to <see cref="WaterCap"/>): thin, dirty slop; [T] tips it out.</summary>
        public float water;
        public const float WaterCap = 8f, SourHours = 10f;
        public bool Sour => sour >= 0.6f;
        Transform slop;
        Material slopMat;
        int shown = -1;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start() => Swarm.Attach(gameObject, Swarm.Kind.Flies, new Vector3(0f, 0.55f, 0f), 0.3f, 5,
            () => scraps < Cap / 2 || DayNight.Darkness > 0.6f || Weather.TemperatureAt(transform.position.z) < 4f ? 0f : Mathf.Clamp01(scraps / (float)Cap * (Sour ? 2f : 1f)));

        public string Prompt(MadMax.Game.WastelandGame g) => "[E] TIP SCRAPS IN (" + Words + ")" + (water >= 1f ? "  [T] TIP OUT THE RAIN WATER" : "");

        string Words => (scraps == 0 ? (water >= 1f ? "RAIN WATER" : "EMPTY") : scraps < Cap / 3 ? "A FEW SCRAPS" : scraps < Cap ? "HALF FULL" : "FULL")
            + (scraps > 0 && Sour ? ", SOUR" : "") + (scraps > 0 && water >= 3f ? ", WATERY" : "");

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary)
            {
                if (water < 1f) { g.Toast("NO WATER STANDING IN THE BUCKET"); return; }
                TipWater();
                g.Toast("TIPPED THE DIRTY WATER OUT OF THE SLOP BUCKET");
                return;
            }
            int n = 0;
            foreach (var id in Slops)
                while (scraps < Cap && g.Inventory.TakeItem(id)) { scraps++; n++; }
            if (n == 0) { g.Toast(scraps >= Cap ? "THE BUCKET IS FULL: THE PIGS WILL SEE TO IT" : "NO ROTTEN FOOD OR OFFCUTS IN THE PACK"); return; }
            MadMax.Audio.Sfx.Play("pour", transform.position, 0.35f, 0.7f, 8f);
            g.Toast("TIPPED " + n + " SCRAPS IN THE SLOP BUCKET");
            Changed();
        }

        /// <summary>The standing water poured on the ground beside the bucket (a dirty puddle; host side spill).</summary>
        public void TipWater()
        {
            if (water <= 0f) return;
            var m = new MadMax.Items.FluidMix(); m.Set(MadMax.Items.ResourceType.DirtyWater);
            Spills.Pour(transform.position + transform.forward * 0.5f, m, water);
            MadMax.Audio.Sfx.Play("splash", transform.position, 0.4f, 1.2f, 10f);
            water = 0f;
            Changed();
        }

        /// <summary>One scrap for an animal (false: empty).</summary>
        public bool Take()
        {
            if (scraps <= 0) return false;
            scraps--;
            sour = Mathf.Max(0f, sour - 0.06f);
            if (scraps == 0) sour = 0f;
            if (water > 0f) water = Mathf.Max(0f, water - WaterCap / Cap);                                // a wet scrap takes its water
            Changed();
            return true;
        }

        void Changed() { Show(); GetComponent<Placeable>()?.Dirty(); }

        /// <summary>Pull spoiled food out of the stores within <see cref="Gather"/> m (once a game hour; host side).</summary>
        public int Collect()
        {
            int got = 0;
            foreach (var c in Container.All)
            {
                if (!c || scraps >= Cap || (c.transform.position - transform.position).sqrMagnitude > Gather * Gather) continue;
                foreach (var id in Slops)
                    while (scraps < Cap && c.inventory.TakeItem(id)) { scraps++; got++; }
            }
            if (got > 0) Changed();
            return got;
        }

        void Update()
        {
            if (shown != scraps) Show();
            float now = DayNight.TotalDays * 24f;
            if (lastHour < 0f || now < lastHour) { lastHour = now; return; }
            if (now - lastHour < 1f) return;
            float hours = Mathf.Min(24f, now - lastHour);
            lastHour = now;
            Weather1023(hours);
            var net = MadMax.Net.NetSession.Instance;
            if (!net || !net.IsClient) Collect();
        }

        /// <summary>An hour or more passed: half full in the warmth it sours (faster in the sun), rain fills it.</summary>
        void Weather1023(float hours)
        {
            float temp = Weather.TemperatureAt(transform.position.z);
            bool sun = DayNight.Darkness < 0.3f && !Weather.Raining;
            if (scraps >= Cap / 2 && temp > 8f)
                sour = Mathf.Min(1f, sour + hours / SourHours * (sun ? 1.6f : 1f) * Mathf.Clamp01((temp - 8f) / 14f + 0.4f));
            if (Weather.Raining && temp > 0f) water = Mathf.Min(WaterCap, water + hours * 0.9f * Mathf.Max(0.4f, Weather.Wetness));
            if (water > 0f && scraps + water / WaterCap * Cap > Cap * 1.2f)                           // brimming: it spills over
            { var m = new MadMax.Items.FluidMix(); m.Set(MadMax.Items.ResourceType.DirtyWater); Spills.Pour(transform.position, m, 1f); water -= 1f; }
            Changed();
        }

        /// <summary>The slop surface rising in the bucket.</summary>
        void Show()
        {
            shown = scraps;
            if (scraps <= 0 && water < 1f) { if (slop) slop.gameObject.SetActive(false); return; }
            if (!slop)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Slop";
                Destroy(go.GetComponent<Collider>());
                slop = go.transform; slop.SetParent(transform, false);
                slopMat = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = slopMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            slop.gameObject.SetActive(true);
            var fresh = Color.Lerp((Color)MadMax.Voxel.Pal.Wood[0], (Color)MadMax.Voxel.Pal.Moss[1], 0.35f);
            var c = Color.Lerp(fresh, Color.Lerp((Color)MadMax.Voxel.Pal.Moss[0], new Color(0.2f, 0.22f, 0.12f), 0.5f), sour);   // sour: greyer, greener
            float wet = Mathf.Clamp01(water / WaterCap);
            slopMat.SetColor("_BaseColor", Color.Lerp(c, new Color(0.36f, 0.33f, 0.26f), wet * (scraps > 0 ? 0.5f : 0.9f)));
            float f = Mathf.Clamp01(scraps / (float)Cap + wet * 0.35f);
            slop.localPosition = new Vector3(0f, 0.1f + f * 0.32f, 0f);
            slop.localScale = new Vector3(0.38f, 0.01f, 0.38f);
        }

        /// <summary>The nearest bucket with slop in it within <paramref name="radius"/> of <paramref name="p"/>.</summary>
        public static SlopBucket Nearest(Vector3 p, float radius)
        {
            SlopBucket best = null; float bd = radius * radius;
            foreach (var b in All) { if (!b || b.scraps <= 0) continue; float d = (b.transform.position - p).sqrMagnitude; if (d < bd) { bd = d; best = b; } }
            return best;
        }

        public string SaveState() => scraps + ";" + sour.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ";" + water.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        /// <summary>"scraps;sour;water" (a bare count from 2026-10-18 still loads).</summary>
        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var f = s.Split(';');
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (int.TryParse(f[0], out int n)) scraps = Mathf.Clamp(n, 0, Cap);
            if (f.Length > 1) float.TryParse(f[1], System.Globalization.NumberStyles.Float, ci, out sour);
            if (f.Length > 2) float.TryParse(f[2], System.Globalization.NumberStyles.Float, ci, out water);
            shown = -1;
        }
    }
}
