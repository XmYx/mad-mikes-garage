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
        Transform slop;
        int shown = -1;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start() => Swarm.Attach(gameObject, Swarm.Kind.Flies, new Vector3(0f, 0.55f, 0f), 0.3f, 5,
            () => scraps < Cap / 2 || DayNight.Darkness > 0.6f || Weather.TemperatureAt(transform.position.z) < 4f ? 0f : Mathf.Clamp01(scraps / (float)Cap));

        public string Prompt(MadMax.Game.WastelandGame g) => "[E] TIP SCRAPS IN (" + Words + ")";

        string Words => scraps == 0 ? "EMPTY" : scraps < Cap / 3 ? "A FEW SCRAPS" : scraps < Cap ? "HALF FULL" : "FULL";

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            int n = 0;
            foreach (var id in Slops)
                while (scraps < Cap && g.Inventory.TakeItem(id)) { scraps++; n++; }
            if (n == 0) { g.Toast(scraps >= Cap ? "THE BUCKET IS FULL: THE PIGS WILL SEE TO IT" : "NO ROTTEN FOOD OR OFFCUTS IN THE PACK"); return; }
            MadMax.Audio.Sfx.Play("pour", transform.position, 0.35f, 0.7f, 8f);
            g.Toast("TIPPED " + n + " SCRAPS IN THE SLOP BUCKET");
            Changed();
        }

        /// <summary>One scrap for an animal (false: empty).</summary>
        public bool Take()
        {
            if (scraps <= 0) return false;
            scraps--;
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
            lastHour = now;
            var net = MadMax.Net.NetSession.Instance;
            if (!net || !net.IsClient) Collect();
        }

        /// <summary>The slop surface rising in the bucket.</summary>
        void Show()
        {
            shown = scraps;
            if (scraps <= 0) { if (slop) slop.gameObject.SetActive(false); return; }
            if (!slop)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Slop";
                Destroy(go.GetComponent<Collider>());
                slop = go.transform; slop.SetParent(transform, false);
                var mat = new Material(Fx.RuntimeShader("Unlit", "Universal Render Pipeline/Unlit"));
                mat.SetColor("_BaseColor", Color.Lerp((Color)MadMax.Voxel.Pal.Wood[0], (Color)MadMax.Voxel.Pal.Moss[1], 0.35f));
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            slop.gameObject.SetActive(true);
            float f = Mathf.Clamp01(scraps / (float)Cap);
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

        public string SaveState() => scraps.ToString();
        public void LoadState(string s) { if (int.TryParse(s, out int n)) scraps = Mathf.Clamp(n, 0, Cap); }
    }
}
