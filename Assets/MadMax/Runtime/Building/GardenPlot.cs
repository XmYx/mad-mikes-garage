using MadMax.Items;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Tilled bed or planter. A crop grows with water (rain on open ground, a watering can, sprinklers, drip
    /// lines, or a channel of lake water next to it), soil fertility (used up by harvests, restored with fertilizer /
    /// compost), light (mushrooms want the dark) and warmth: frost kills crops on open ground, a greenhouse protects
    /// and keeps them growing in winter. Drought wilts the crop, weeds slow it (hoe or [T] pull), crows peck ripening
    /// crops unless a scarecrow stands near. Seeds carry the make of the harvest they came from (Farming skill).</summary>
    public class GardenPlot : MonoBehaviour, IPlaceState, IInteractable
    {
        public static readonly System.Collections.Generic.List<GardenPlot> All = new System.Collections.Generic.List<GardenPlot>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetAll() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string crop;          // seed id
        public float growth, water, fertilizer;
        public float fertility = 0.8f, health = 1f, weeds, seedQuality = 1f;
        float irrigatedUntil;        // kept moist by a drip line or a channel (grows a little faster)
        public bool Irrigated => Time.time < irrigatedUntil;
        public Vector3 soil = new Vector3(0, 0.18f, 0);
        MeshFilter plantMesh;
        int shownStage = -1;
        float roofCheck, pestT, crowUntil, irrigT;
        int roof;                    // 0 open sky, 1 glass (greenhouse), 2 solid roof / underground
        GameObject crow;

        public bool Ripe => crop != null && growth >= 1f && health > 0f;
        public bool Dead => crop != null && health <= 0f;

        void Start()
        {
            var go = new GameObject("Plant", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = soil;
            plantMesh = go.GetComponent<MeshFilter>();
            go.GetComponent<MeshRenderer>().sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
        }

        /// <summary>What is over the plot: open sky, a greenhouse's glass, or a solid roof.</summary>
        int RoofOver()
        {
            if (!Physics.Raycast(transform.position + Vector3.up * 1.2f, Vector3.up, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)) return 0;
            var pl = hit.collider.GetComponentInParent<Placeable>();
            return pl && pl.id == "greenhouse" ? 1 : 2;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if ((roofCheck -= dt) < 0f) { roofCheck = 3f; roof = RoofOver(); }
            if (roof == 0 && Weather.Raining && !Weather.Snowing) water = 1f;
            // a channel or lake beside the bed keeps it wet
            if ((irrigT -= dt) < 0f)
            {
                irrigT = 4f;
                var t = DeformableTerrain.Instance;
                if (t)
                    for (int i = 0; i < 8; i++)
                    {
                        var q = transform.position + Quaternion.Euler(0, i * 45f, 0) * Vector3.forward * 2.2f;
                        if (t.WaterDepth(q.x, q.z) > 0.05f) { Irrigate(1f); break; }
                    }
            }
            var def = FoodLibrary.Crop(crop);
            if (def != null && growth < 1f && health > 0f)
            {
                var game = MadMax.Game.WastelandGame.Instance;
                float skill = game ? 1f + game.Stats.Level(Skill.Farming) * 0.06f : 1f;
                float temp = Weather.Temperature + (roof == 1 ? 10f : roof == 2 ? 4f : 0f);
                float season = roof == 1 ? 1f : temp < 4f ? 0.15f : Weather.Season == 2 ? 0.4f : Weather.Season == 1 ? 0.7f : 1f;
                float light = def.dark ? (roof == 2 ? 1f : 0.1f) : roof == 2 ? 0.35f : 1f;
                float rate = (water > 0f ? 1f : 0f) * (0.35f + 0.65f * fertility) * (1f - weeds * 0.6f) * skill * season * light
                           * (0.9f + 0.1f * seedQuality) * (Irrigated ? 1.1f : 1f) * (fertilizer > 0f ? 1.3f : 1f);
                growth = Mathf.Min(1f, growth + dt * rate / (def.growMinutes * 60f));
                // drought wilts, water brings it back; frost on open ground kills
                if (water <= 0f) health -= dt / (8f * 60f); else health = Mathf.Min(1f, health + dt / (10f * 60f));
                if (temp < 0.5f && roof == 0) health -= dt / (4f * 60f);
                if (health <= 0f) { health = 0f; Tell("YOUR " + def.name + " DIED"); GetComponent<Placeable>()?.Dirty(); }
                water = Mathf.Max(0f, water - dt / 240f);
                if (fertilizer > 0f) fertilizer = Mathf.Max(0f, fertilizer - dt / 600f);
                weeds = Mathf.Min(1f, weeds + dt / (25f * 60f) * (0.4f + fertility * 0.6f));
                Pests(def, dt);
                Grazers(def, dt);
            }
            else if (crop == null) weeds = Mathf.Min(1f, weeds + dt / (60f * 60f));
            if (crow && Time.time > crowUntil) { Destroy(crow); crow = null; }
            int stage = def == null ? -1 : Mathf.Min(3, Mathf.FloorToInt(growth * 3.999f));
            if (stage != shownStage && plantMesh)
            {
                shownStage = stage;
                plantMesh.sharedMesh = def == null ? null : CropVisuals.Get(def, stage);
            }
        }

        /// <summary>Crows on ripening crops under the open sky (unless a scarecrow stands guard): they peck growth away.</summary>
        void Pests(CropDef def, float dt)
        {
            if (roof != 0 || growth < 0.5f || Scarecrow.Near(transform.position)) return;
            if ((pestT += dt) < 150f) return;
            pestT = Random.Range(-60f, 0f);
            if (Random.value > 0.5f) return;
            growth = Mathf.Max(0.5f, growth - 0.08f);
            crowUntil = Time.time + 25f;
            if (!crow) crow = Crow();
            MadMax.Audio.Sfx.Play("birds", transform.position, 0.6f, 0.8f, 30f);
            Tell("CROWS ARE AT YOUR " + def.name);
        }

        float grazeT;

        /// <summary>A fence (any fence piece within 4 m) keeps grazing animals off the bed.</summary>
        public bool Fenced
        {
            get
            {
                foreach (var p in Placeable.All) if (p && p.id.StartsWith("fence") && (p.transform.position - transform.position).sqrMagnitude < 4f * 4f) return true;
                return false;
            }
        }

        /// <summary>A crop worth grazing, standing open to animals.</summary>
        public bool Tempting => crop != null && health > 0f && growth > 0.15f && !fenceCached;
        bool fenceCached; float fenceCheck;

        /// <summary>Goats, cows, horses and deer that wander onto an unfenced bed eat the crop and trample it.</summary>
        void Grazers(CropDef def, float dt)
        {
            if ((fenceCheck -= dt) <= 0f) { fenceCheck = 10f; fenceCached = Fenced; }
            if ((grazeT -= dt) > 0f || fenceCached) return;
            grazeT = 4f;
            foreach (var a in MadMax.Animals.Animal.All)
            {
                if (!a || !a.Alive || !a.Grazer || (a.transform.position - transform.position).sqrMagnitude > 2.2f * 2.2f) continue;
                growth = Mathf.Max(0f, growth - 0.06f);
                health = Mathf.Max(0.05f, health - 0.1f);
                GetComponent<Placeable>()?.Dirty();
                if (Random.value < 0.3f) Tell("A " + a.Label + " IS EATING YOUR " + def.name + " - PUT UP A FENCE");
                return;
            }
        }

        static Mesh crowMesh;
        GameObject Crow()
        {
            if (!crowMesh)
            {
                var g = new MadMax.Voxel.VoxelGrid();
                g.Box(-1, 1, -2, 1, 2, 1, MadMax.Voxel.Pal.Ramp(MadMax.Voxel.Pal.Black, 1, 1091));           // body
                g.Box(0, 3, 1, 0, 3, 2, MadMax.Voxel.Pal.Ramp(MadMax.Voxel.Pal.Black, 2)); g.Set(0, 3, 3, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Ochre[2]));   // head, beak
                g.Box(0, 1, -4, 0, 2, -3, MadMax.Voxel.Pal.Ramp(MadMax.Voxel.Pal.Black, 0));                // tail
                g.Set(-1, 0, 0, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Ochre[1])); g.Set(1, 0, 0, MadMax.Voxel.Pal.Solid(MadMax.Voxel.Pal.Ochre[1]));   // legs
                g.Bevel();
                crowMesh = MadMax.Voxel.VoxelMesher.Build(g, "Crow");
            }
            var c = new GameObject("Crow", typeof(MeshFilter), typeof(MeshRenderer));
            c.transform.SetParent(transform, false);
            c.transform.localPosition = soil + new Vector3(Random.Range(-0.3f, 0.3f), 0.05f, Random.Range(-0.3f, 0.3f));
            c.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            c.GetComponent<MeshFilter>().sharedMesh = crowMesh;
            c.GetComponent<MeshRenderer>().sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
            return c;
        }

        void Tell(string s)
        {
            var g = MadMax.Game.WastelandGame.Instance;
            if (g && g.Player && Vector3.Distance(g.Player.transform.position, transform.position) < 40f) g.Toast(s);
        }

        public void Water(float amount) { water = Mathf.Min(1f, water + amount); GetComponent<Placeable>()?.Dirty(); }
        /// <summary>Drip line / channel: water plus the steady-moisture growth bonus for a while.</summary>
        public void Irrigate(float amount) { water = Mathf.Min(1f, water + amount); irrigatedUntil = Time.time + 12f; }
        public void Weed() { weeds = 0f; if (crow) { Destroy(crow); crow = null; } GetComponent<Placeable>()?.Dirty(); }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            var def = FoodLibrary.Crop(crop);
            string soilNote = "  SOIL " + Mathf.RoundToInt(fertility * 100) + "%";
            if (def == null) return (g.FirstSeed(false) != null ? "[E] PLANT " + FoodLibrary.SeedName(g.FirstSeed(false)) : "PLOT: NEED SEEDS") + soilNote + (weeds > 0.3f ? "  [T] PULL WEEDS" : "");
            if (Dead) return "DEAD " + def.name + "  [E] CLEAR";
            if (Ripe) return "[E] HARVEST " + def.name + soilNote;
            string s = def.name + " " + Mathf.RoundToInt(growth * 100) + "%" + (water <= 0f ? " DRY" : "") + (health < 0.6f ? " WILTING" : "") + (weeds > 0.4f ? " WEEDY" : "")
                     + (def.dark && roof != 2 ? " (NEEDS DARK)" : "") + (roof == 1 ? " GREENHOUSE" : "");
            if (water < 0.5f && (g.Inventory.Get(ResourceType.Water) > 0 || g.Inventory.Get(ResourceType.DirtyWater) > 0)) s += "  [E] WATER";
            if (weeds > 0.3f) s += "  [T] PULL WEEDS";
            else if (g.Inventory.GetItem(ItemIds.Fertilizer) > 0 && fertility < 0.9f) s += "  [T] FERTILISE";
            return s;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            var def = FoodLibrary.Crop(crop);
            if (secondary)
            {
                if (weeds > 0.3f)
                {
                    if (!g.Vitals.Spend(8f)) return;
                    Weed(); g.Stats.Practice(Skill.Farming, 1f); g.Soil(3f); g.Toast("PULLED THE WEEDS");
                    g.FindWorms(transform.position, 0.3f);
                }
                else if (fertility < 0.9f && g.Inventory.TakeItem(ItemIds.Fertilizer))
                {
                    fertility = Mathf.Min(1f, fertility + 0.5f); fertilizer = 1f;
                    g.Toast("FERTILISED: SOIL " + Mathf.RoundToInt(fertility * 100) + "%");
                    GetComponent<Placeable>()?.Dirty();
                }
                return;
            }
            if (def == null)
            {
                var seed = g.FirstSeed(false);
                if (seed == null || !g.Inventory.TakeItem(seed)) return;
                crop = seed; growth = 0f; health = 1f; shownStage = -1;
                seedQuality = g.QualityOf(seed);
                g.Stats.Practice(Skill.Farming, 3f);
                g.Toast("PLANTED " + FoodLibrary.Crop(seed).name);
            }
            else if (Dead) { crop = null; growth = 0f; health = 1f; shownStage = -1; g.Toast("CLEARED THE BED"); }
            else if (Ripe)
            {
                g.Harvest(def, transform.position, fertility, health, seedQuality);
                fertility = Mathf.Max(0.1f, fertility - (def.tree ? 0.1f : 0.25f));               // a harvest takes from the soil
                crop = null; growth = 0f; health = 1f; shownStage = -1;
            }
            else
            {
                var t = g.Inventory.Get(ResourceType.Water) > 0 ? ResourceType.Water : ResourceType.DirtyWater;
                if (g.Inventory.TrySpend(t, 1)) { water = 1f; g.Toast("WATERED"); g.Stats.Practice(Skill.Farming, 0.5f); }
            }
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return (crop ?? "") + ";" + growth.ToString("0.###", ci) + ";" + water.ToString("0.##", ci) + ";" + fertilizer.ToString("0.##", ci)
                 + ";" + fertility.ToString("0.##", ci) + ";" + health.ToString("0.##", ci) + ";" + weeds.ToString("0.##", ci) + ";" + seedQuality.ToString("0.##", ci);
        }

        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            var ci = System.Globalization.CultureInfo.InvariantCulture; var st = System.Globalization.NumberStyles.Float;
            crop = p[0].Length > 0 ? p[0] : null;
            if (p.Length > 1) float.TryParse(p[1], st, ci, out growth);
            if (p.Length > 2) float.TryParse(p[2], st, ci, out water);
            if (p.Length > 3) float.TryParse(p[3], st, ci, out fertilizer);
            if (p.Length > 4) float.TryParse(p[4], st, ci, out fertility);
            if (p.Length > 5) float.TryParse(p[5], st, ci, out health);
            if (p.Length > 6) float.TryParse(p[6], st, ci, out weeds);
            if (p.Length > 7) float.TryParse(p[7], st, ci, out seedQuality);
            shownStage = -1;
        }
    }
}
