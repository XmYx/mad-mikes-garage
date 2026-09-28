using MadMax.Items;
using MadMax.RPG;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Tilled bed or planter: plant a seed, keep it watered (rain, sprinkler or by hand), fertilise for faster growth, harvest.</summary>
    public class GardenPlot : MonoBehaviour, IPlaceState, IInteractable
    {
        public string crop;          // seed id
        public float growth, water, fertilizer;
        public Vector3 soil = new Vector3(0, 0.18f, 0);
        MeshFilter plantMesh;
        int shownStage = -1;
        float openSkyCheck; bool openSky = true;

        public bool Ripe => crop != null && growth >= 1f;

        void Start()
        {
            var go = new GameObject("Plant", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = soil;
            plantMesh = go.GetComponent<MeshFilter>();
            go.GetComponent<MeshRenderer>().sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if ((openSkyCheck -= dt) < 0f)
            {
                openSkyCheck = 3f;
                openSky = !Physics.Raycast(transform.position + Vector3.up * 1.2f, Vector3.up, 30f, ~0, QueryTriggerInteraction.Ignore);
            }
            if (openSky && MadMax.World.Weather.Raining) water = 1f;
            var def = FoodLibrary.Crop(crop);
            if (def != null && growth < 1f)
            {
                var game = MadMax.Game.WastelandGame.Instance;
                float skill = game ? 1f + game.Stats.Level(Skill.Farming) * 0.06f : 1f;
                float season = openSky ? (MadMax.World.Weather.Temperature < 2f ? 0.15f : MadMax.World.Weather.Season == 1 ? 0.7f : 1f) : 1f;
                float rate = (water > 0f ? 1f : 0.05f) * (fertilizer > 0f ? 1.8f : 1f) * skill * season;
                growth = Mathf.Min(1f, growth + dt * rate / (def.growMinutes * 60f));
                water = Mathf.Max(0f, water - dt / 240f);
                if (fertilizer > 0f) fertilizer = Mathf.Max(0f, fertilizer - dt / 600f);
            }
            int stage = def == null ? -1 : Mathf.Min(3, Mathf.FloorToInt(growth * 3.999f));
            if (stage != shownStage && plantMesh)
            {
                shownStage = stage;
                plantMesh.sharedMesh = def == null ? null : CropVisuals.Get(def, stage);
            }
        }

        public void Water(float amount) { water = Mathf.Min(1f, water + amount); GetComponent<Placeable>()?.Dirty(); }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            var def = FoodLibrary.Crop(crop);
            if (def == null) return g.FirstSeed(false) != null ? "[E] PLANT " + FoodLibrary.SeedName(g.FirstSeed(false)) : "PLOT: NEED SEEDS";
            if (Ripe) return "[E] HARVEST " + def.name;
            string s = def.name + " " + Mathf.RoundToInt(growth * 100) + "%" + (water <= 0f ? " DRY" : "");
            if (water < 0.5f && (g.Inventory.Get(ResourceType.Water) > 0 || g.Inventory.Get(ResourceType.DirtyWater) > 0)) s += "  [E] WATER";
            if (fertilizer <= 0f && g.Inventory.GetItem(ItemIds.Fertilizer) > 0) s += "  [T] FERTILISE";
            return s;
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            var def = FoodLibrary.Crop(crop);
            if (secondary)
            {
                if (def != null && fertilizer <= 0f && g.Inventory.TakeItem(ItemIds.Fertilizer)) { fertilizer = 1f; g.Toast("FERTILISED"); GetComponent<Placeable>()?.Dirty(); }
                return;
            }
            if (def == null)
            {
                var seed = g.FirstSeed(false);
                if (seed == null || !g.Inventory.TakeItem(seed)) return;
                crop = seed; growth = 0f; shownStage = -1;
                g.Stats.Practice(Skill.Farming, 3f);
                g.Toast("PLANTED " + FoodLibrary.Crop(seed).name);
            }
            else if (Ripe)
            {
                g.Harvest(def, transform.position);
                crop = null; growth = 0f; shownStage = -1;
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
            return (crop ?? "") + ";" + growth.ToString("0.###", ci) + ";" + water.ToString("0.##", ci) + ";" + fertilizer.ToString("0.##", ci);
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
            shownStage = -1;
        }
    }
}
