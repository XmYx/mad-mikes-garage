using MadMax.Items;
using MadMax.RPG;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>A tree grown from a sapling: grows in size, fruit trees ripen fruit (E to pick), fully grown trees are
    /// felled with hits (wood + a chance of a sapling).</summary>
    public class PlantedTree : MonoBehaviour, IPlaceState, IInteractable
    {
        public string sapling;
        public float growth, fruit;
        MeshFilter mf;
        int shownStage = -1;

        void Awake()
        {
            mf = GetComponent<MeshFilter>();
            if (TryGetComponent<MeshRenderer>(out var mr)) mr.sharedMaterial = CropVisuals.Material(mr.sharedMaterial);
        }

        void Update()
        {
            var def = FoodLibrary.Crop(sapling);
            if (def == null) return;
            float dt = Time.deltaTime;
            float cold = MadMax.World.Weather.Temperature < 2f ? 0.2f : 1f;
            if (growth < 1f) growth = Mathf.Min(1f, growth + dt * cold / (def.growMinutes * 60f));
            else if (def.regrowMinutes > 0f && fruit < 1f) fruit = Mathf.Min(1f, fruit + dt * cold / (def.regrowMinutes * 60f));
            int stage = growth < 1f ? Mathf.Min(2, Mathf.FloorToInt(growth * 3f)) : (fruit >= 1f ? 3 : 2);
            if (stage != shownStage)
            {
                shownStage = stage;
                mf.sharedMesh = CropVisuals.Get(def, stage);
                transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.6f, growth);
                var box = GetComponent<BoxCollider>();
                if (box && mf.sharedMesh) { box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size; }
            }
        }

        public string Prompt(MadMax.Game.WastelandGame g)
        {
            var def = FoodLibrary.Crop(sapling);
            if (def == null) return null;
            if (growth < 1f) return def.name + " " + Mathf.RoundToInt(growth * 100) + "%";
            return fruit >= 1f ? "[E] PICK " + def.name : def.name + " (CHOP WITH AXE)";
        }

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            var def = FoodLibrary.Crop(sapling);
            if (def == null || secondary || growth < 1f || fruit < 1f) return;
            fruit = 0f;
            g.Harvest(def, transform.position);
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState() => sapling + ";" + growth.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ";" + fruit.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        public void LoadState(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var p = s.Split(';');
            sapling = p[0];
            if (p.Length > 1) float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out growth);
            if (p.Length > 2) float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fruit);
            shownStage = -1;
        }
    }
}
