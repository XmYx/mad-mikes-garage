using System.Collections.Generic;
using System.Globalization;
using MadMax.Animals;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Snares and cage traps (depth stage E). Set on open dry ground, they catch the small game of the country
    /// around them over game hours: the more small animals live in the biome (jackrabbits, rats, raccoons,
    /// armadillos...), the sooner; near a settlement only the rats come. A snare needs nothing but kills what it
    /// catches (take it within a day and a half or it spoils); a cage trap needs bait ([T]) and holds its catch alive and
    /// fresh — a live jackrabbit comes home as a rabbit to keep. [E] takes the catch and resets the trap.</summary>
    public class SnareTrap : MonoBehaviour, IInteractable, IPlaceState
    {
        public static readonly List<SnareTrap> All = new List<SnareTrap>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        /// <summary>Game hours to a catch in the best country (snare / cage trap).</summary>
        public const float SnareHours = 8f, CageHours = 6f;
        public const int MaxBait = 4;
        static readonly string[] Baits = { "food_carrot", "food_cabbage", "food_apple", "food_corn", "crop_wheat", "food_beet", "bait_insects", "bait_worms", "food_meat_raw", "food_rotten" };
        static readonly List<AnimalDef> tmp = new List<AnimalDef>(), pool = new List<AnimalDef>();

        public bool cage;
        public int bait;
        public string caught;                // species id of the catch, or null
        public float caughtAt, progress, lastDays = -1f;
        float checkT;
        Transform shown;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if ((checkT -= Time.deltaTime) > 0f) return;
            checkT = 3f;
            Tick();
        }

        /// <summary>Work off the game time since the last look: the catch comes when enough of it has passed.</summary>
        public void Tick()
        {
            float now = DayNight.TotalDays;
            if (lastDays < 0f || lastDays > now) { lastDays = now; return; }
            float days = Mathf.Min(6f, now - lastDays);
            if (days < 0.005f) return;
            lastDays = now;
            if (caught != null || (cage && bait <= 0)) return;
            float habitat = Habitat(out var species);
            if (habitat <= 0f || species == null) return;
            progress += days * 24f / (cage ? CageHours : SnareHours) * habitat;
            if (progress < 1f) return;
            progress = 0f;
            caught = species; caughtAt = now;
            if (cage) bait--;
            Show();
            GetComponent<Placeable>()?.Dirty();
        }

        /// <summary>How good the spot is (0..1) and what would walk into the trap next.</summary>
        public float Habitat(out string species)
        {
            species = null;
            var t = DeformableTerrain.Instance;
            if (!t || t.World == null) return 0f;
            var p = transform.position;
            if (t.WaterDepth(p.x, p.z) > 0.05f) return 0f;
            if (Physics.Raycast(p + Vector3.up * 0.6f, Vector3.up, out var roof, 12f, ~0, QueryTriggerInteraction.Ignore) && roof.collider.GetComponentInParent<Placeable>()) return 0f;   // under a built roof: nothing wanders in
            var biome = t.BiomeAt(p.x, p.z);
            bool town = t.World.SettlementAt(p.x, p.z) != null;
            AnimalLibrary.WildIn(biome, tmp);
            float sum = 0f;
            pool.Clear();
            foreach (var d in tmp)
            {
                if (d.mass > 12f || d.flies || d.plan == BodyPlan.Snake || d.nature == Nature.Predator || d.Has("venom")) continue;
                if (town && d.nature != Nature.Vermin) continue;                                     // only rats come in among the houses
                pool.Add(d); sum += d.density;
            }
            if (pool.Count == 0)
            {
                if (town || biome == Biome.City || biome == Biome.Nuclear) species = "rat";
                else species = "jackrabbit";                                                          // strays from the next valley
                return 0.35f;
            }
            // which one: by the day and the spot, weighted by how common each is
            var rnd = new System.Random(Mathf.FloorToInt(p.x * 3f) * 73856093 ^ Mathf.FloorToInt(p.z * 3f) * 19349663 ^ DayNight.Day * 83492791);
            float pick = (float)rnd.NextDouble() * sum;
            species = pool[pool.Count - 1].id;
            foreach (var d in pool) { if ((pick -= d.density) <= 0f) { species = d.id; break; } }
            return Mathf.Clamp(sum, 0.35f, 1f) * (town ? 0.6f : 1f);
        }

        AnimalDef Catch => caught != null ? AnimalLibrary.Get(caught) : null;
        bool Spoiled => !cage && caught != null && DayNight.TotalDays - caughtAt > 1.5f;

        string Bait(MadMax.Game.WastelandGame g)
        {
            foreach (var b in Baits) if (g.Inventory.GetItem(b) > 0) return b;
            return null;
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            var d = Catch;
            string name = cage ? "CAGE TRAP" : "SNARE";
            if (d != null) return "[E] TAKE THE " + d.name + (cage ? " (ALIVE)" : Spoiled ? " (SPOILED)" : "") + " FROM THE " + name;
            float hab = Habitat(out _);
            string s = name + (hab <= 0f ? ": SET IT OUTDOORS ON DRY GROUND" : hab < 0.5f ? ": LITTLE GAME HERE" : ": GAME TRAILS") + (cage ? "  BAIT " + bait : "");
            if (cage && bait < MaxBait && Bait(g) != null) s += "  [T] BAIT (" + ItemCatalog.Name(Bait(g)) + ")";
            else if (cage && bait <= 0) s += " (NEEDS BAIT)";
            return s;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary)
            {
                var b = Bait(g);
                if (!cage || b == null || bait >= MaxBait || !g.Inventory.TakeItem(b)) return;
                bait++;
                g.Toast("BAITED THE CAGE TRAP (" + bait + ")");
                GetComponent<Placeable>()?.Dirty();
                return;
            }
            var d = Catch;
            if (d == null) return;
            var got = new List<string>();
            if (cage && d.young != null) { g.Inventory.AddItem(d.young); got.Add(ItemCatalog.Name(d.young) + " (RELEASE IT FROM THE PACK TO KEEP IT)"); }
            else
            {
                bool spoiled = Spoiled;
                float y = (cage ? 1f : 0.8f) * MadMax.Game.GameRules.Current.yield;
                foreach (var (id, min, max) in d.drops)
                {
                    if (id.StartsWith("trophy_")) continue;
                    int n = Mathf.RoundToInt(Mathf.Max(min, (min + max) * 0.5f) * y);
                    if (id == "food_meat_raw") n = Mathf.Max(1, n);                                    // even a rat is a meal
                    if (n <= 0) continue;
                    if (id.StartsWith("res:"))
                    {
                        if (spoiled) continue;
                        var t = (ResourceType)int.Parse(id.Substring(4));
                        g.Inventory.Add(t, n); got.Add(n + " " + ResourceInfo.Name(t));
                    }
                    else
                    {
                        string item = spoiled && id == "food_meat_raw" ? "food_rotten" : id;
                        g.Inventory.AddItem(item, n); got.Add((n > 1 ? n + " " : "") + ItemCatalog.Name(item));
                    }
                }
            }
            caught = null; progress = 0f;
            Show();
            MadMax.Audio.Sfx.Play("click", transform.position, 0.5f, 0.9f);
            g.Stats.Practice(MadMax.RPG.Skill.Survival, 2f);
            g.HusbandryTally("trapped");
            g.Toast("FROM THE " + (cage ? "CAGE TRAP" : "SNARE") + ": " + (got.Count > 0 ? string.Join(", ", got) : "NOTHING LEFT") + " - RESET");
            GetComponent<Placeable>()?.Dirty();
        }

        /// <summary>The catch in the trap: lying in the noose, or sitting up in the cage.</summary>
        void Show()
        {
            var d = Catch;
            if (d == null) { if (shown) shown.gameObject.SetActive(false); return; }
            var mesh = AnimalModels.For(d).body;
            if (!mesh) return;
            var mr = GetComponent<MeshRenderer>();
            if (!shown)
            {
                shown = new GameObject("Catch", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                shown.SetParent(transform, false);
                shown.GetComponent<MeshRenderer>().sharedMaterial = mr ? mr.sharedMaterial : null;
            }
            shown.GetComponent<MeshFilter>().sharedMesh = mesh;
            shown.gameObject.SetActive(true);
            float s = Mathf.Min(1f, 0.5f / Mathf.Max(0.1f, mesh.bounds.size.z));
            shown.localScale = Vector3.one * s;
            if (cage) { shown.localPosition = new Vector3(0f, 0.08f - mesh.bounds.min.y * s, 0f); shown.localRotation = Quaternion.identity; }
            else { shown.localPosition = new Vector3(0.1f, mesh.bounds.extents.x * s, 0.25f); shown.localRotation = Quaternion.Euler(0f, 70f, 90f); }
        }

        public string SaveState() => progress.ToString("0.###", CultureInfo.InvariantCulture) + ";" + (caught ?? "") + ";" + caughtAt.ToString("0.###", CultureInfo.InvariantCulture)
            + ";" + bait + ";" + lastDays.ToString("0.####", CultureInfo.InvariantCulture);

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            if (p.Length > 0) float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out progress);
            caught = p.Length > 1 && p[1].Length > 0 ? p[1] : null;
            if (p.Length > 2) float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out caughtAt);
            if (p.Length > 3) int.TryParse(p[3], out bait);
            if (p.Length > 4) float.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out lastDays);
            Show();
        }
    }
}
