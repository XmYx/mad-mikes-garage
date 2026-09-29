using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Clothes in use (roadmap 3): garment condition (hits, crashes and fire tear them, worn-out ones show holes,
    /// keep half their warmth and finally fall apart; mended at a sewing table or with a sewing kit), wetness (rain
    /// soaks you unless a waterproof layer keeps it off; shelter, heaters, fire and heat dry you), backpacks (carry),
    /// protection (radiation, dust and smoke) and the outfit's style, which colours first impressions.</summary>
    public partial class WastelandGame
    {
        /// <summary>0 = like new .. 1 = falls apart, per garment def id (saved).</summary>
        public readonly Dictionary<string, float> ClothWear = new Dictionary<string, float>();

        public float GarmentCondition(string defId) => ClothWear.TryGetValue(defId, out var w) ? 1f - w : 1f;
        public bool Wearing(string defId) => Player && Player.Rig && Player.Rig.outfit.Contains(defId);
        /// <summary>Coughing in dust or smoke without a face cover (HUD tag).</summary>
        public bool Coughing { get; private set; }

        static readonly List<string> tearing = new List<string>();

        static BodyPart[] PartsOf(BodyZone z) => z switch
        {
            BodyZone.Head => new[] { BodyPart.Head },
            BodyZone.Torso => new[] { BodyPart.Chest, BodyPart.Pelvis },
            BodyZone.ArmL => new[] { BodyPart.UpperArmL, BodyPart.ForearmL },
            BodyZone.ArmR => new[] { BodyPart.UpperArmR, BodyPart.ForearmR },
            BodyZone.HandL => new[] { BodyPart.HandL },
            BodyZone.HandR => new[] { BodyPart.HandR },
            BodyZone.LegL => new[] { BodyPart.ThighL, BodyPart.ShinL },
            BodyZone.LegR => new[] { BodyPart.ThighR, BodyPart.ShinR },
            BodyZone.FootL => new[] { BodyPart.FootL },
            _ => new[] { BodyPart.FootR },
        };

        /// <summary>Wear the garments covering <paramref name="zone"/> (a hit), or every garment (null: crash, fire).
        /// <paramref name="amount"/> is a fraction of a garment's life before durability.</summary>
        public void TearClothes(BodyZone? zone, float amount)
        {
            if (!Player || amount <= 0f) return;
            tearing.Clear();
            foreach (var id in Player.Rig.outfit)
            {
                var d = ClothingLibrary.Get(id);
                if (d == null) continue;
                bool hit = zone == null;
                if (!hit) foreach (var part in PartsOf(zone.Value)) if (d.coverage.ContainsKey(part)) { hit = true; break; }
                if (hit) tearing.Add(id);
            }
            bool rebuild = false;
            foreach (var id in tearing)
            {
                var d = ClothingLibrary.Get(id);
                ClothWear.TryGetValue(id, out var w);
                float before = w;
                w += amount / Mathf.Max(0.2f, d.durability * (d.armor != null ? 2f : 1f)) * GameRules.Current.DamageTaken * QualityWear(ClothingLibrary.ItemId(d));   // armour is built to take it
                if (w >= 1f) { FallApart(d); rebuild = true; continue; }
                ClothWear[id] = w;
                if (before < 0.65f && w >= 0.65f) { rebuild = true; Toast("YOUR " + d.name + " IS TORN"); }
            }
            if (rebuild) Player.RebuildBody();
        }

        void FallApart(ClothingDef d)
        {
            ClothWear[d.id] = 0f;
            var item = ClothingLibrary.ItemId(d);
            Inventory.TakeItem(item);
            if (Inventory.GetItem(item) <= 0) Player.Rig.outfit.Remove(d.id);
            Toast("YOUR " + d.name + " FELL APART" + (Inventory.GetItem(item) > 0 ? " (PUT ON A SPARE)" : ""));
        }

        /// <summary>Cloth it takes to mend a garment fully.</summary>
        public int MendCost(string defId) => Mathf.Max(1, Mathf.CeilToInt((1f - GarmentCondition(defId)) * (ClothingLibrary.Get(defId)?.armor != null ? 6f : 4f)));
        /// <summary>What a garment is mended with: cloth, or the armour's own material (scrap, rubber, leather, iron).</summary>
        public ResourceType MendWith(string defId) => ClothingLibrary.Get(defId)?.mendWith ?? ResourceType.Cloth;

        public bool Mend(string defId, float amount = 1f)
        {
            if (GarmentCondition(defId) >= 0.999f) return false;
            int cloth = amount >= 1f ? MendCost(defId) : 1;
            var res = MendWith(defId);
            if (!Inventory.TrySpend(res, cloth)) { Toast("NEED " + cloth + " " + ResourceInfo.Name(res)); return false; }
            ClothWear.TryGetValue(defId, out var w);
            ClothWear[defId] = Mathf.Max(0f, w - amount - Stats.Level(Skill.Crafting) * 0.02f);
            Stats.Practice(Skill.Crafting, 2f);
            MadMax.Audio.Sfx.Play2D("scratch", 0.5f, 1.4f);
            var d = ClothingLibrary.Get(defId);
            Toast("MENDED " + (d != null ? d.name : defId) + " (" + Mathf.RoundToInt(GarmentCondition(defId) * 100f) + "%)");
            if (Wearing(defId)) Player.RebuildBody();
            return true;
        }

        /// <summary>Sewing kit: +40 % on the most worn garment you have on.</summary>
        bool UseSewingKit()
        {
            string worst = null; float wc = 0.999f;
            foreach (var id in Player.Rig.outfit) { float c = GarmentCondition(id); if (c < wc) { wc = c; worst = id; } }
            if (worst == null) { Toast("NOTHING TO MEND"); return false; }
            return Mend(worst, 0.4f);
        }

        // ------------------------------------------------------------------ outfit effects
        /// <summary>Best rain protection of the outfit (0..1).</summary>
        public float Waterproof { get { float m = 0f; foreach (var d in Worn()) m = Mathf.Max(m, d.waterproof * Mathf.Lerp(0.5f, 1f, GarmentCondition(d.id))); return m; } }
        /// <summary>Radiation let through (1 = none blocked).</summary>
        public float RadiationPassed { get { float m = 1f; foreach (var d in Worn()) m *= 1f - d.radiation * Mathf.Lerp(0.4f, 1f, GarmentCondition(d.id)); return m; } }
        public bool FaceCovered { get { foreach (var d in Worn()) if (d.filter) return true; return false; } }
        public float CarryBonus { get { float m = 0f; foreach (var d in Worn()) m += d.carry; return m; } }

        readonly List<ClothingDef> worn = new List<ClothingDef>();
        List<ClothingDef> Worn()
        {
            worn.Clear();
            if (Player && Player.Rig) foreach (var id in Player.Rig.outfit) { var d = ClothingLibrary.Get(id); if (d != null) worn.Add(d); }
            return worn;
        }

        float coughTimer;

        void UpdateClothing(float dt)
        {
            if (!Player || Vitals == null || Vitals.Dead) return;
            var s = Stats;
            s.carryBonus = CarryBonus;
            UpdateArmourNoise(dt);
            // ---- wetness: rain and snow soak you outdoors, swimming at once; shelter, warmth and heat dry you
            bool exposed = Weather.Raining && !Sheltered && !Current;
            if (Player.Swimming) s.wetness = 1f;
            else if (exposed) s.wetness = Mathf.Min(1f, s.wetness + dt / 150f * (Weather.Snowing ? 0.4f : 1f) * (1f - Waterproof));
            else
            {
                float heat = Climate.At(Player.transform.position);
                foreach (var f in Fire.All) if (f && (f.transform.position - Player.transform.position).sqrMagnitude < 25f) heat += 10f * f.intensity;
                float dry = 1f / 600f * (Sheltered || Current ? 1.6f : 1f) * (1f + Mathf.Max(0f, Weather.Temperature - 20f) * 0.08f) * (1f + Mathf.Max(0f, heat) * 0.4f);
                s.wetness = Mathf.Max(0f, s.wetness - dt * dry);
            }
            // ---- dust storms and smoke without a face cover: coughing costs breath
            bool dusty = !Current && !Sheltered && WindDust.Strength > 9f && (CurrentBiome == Biome.Desert || CurrentBiome == Biome.Nuclear);
            bool smoky = false;
            foreach (var f in Fire.All) if (f && f.intensity > 0.3f && (f.transform.position - Player.transform.position).sqrMagnitude < 16f) { smoky = true; break; }
            Coughing = (dusty || smoky) && !FaceCovered;
            if (Coughing)
            {
                s.stamina = Mathf.Max(0f, s.stamina - dt * 2.5f);
                if ((coughTimer -= dt) <= 0f) { coughTimer = Random.Range(2.5f, 5f); MadMax.Audio.Sfx.Play("punch", Player.transform.position, 0.25f, 0.5f, 10f); }
            }
        }

        // ------------------------------------------------------------------ style
        /// <summary>What the outfit says about you: RAIDER, HAZMAT, DRIFTER, RAGGED, CLEAN or null (unremarkable).</summary>
        public string OutfitStyle()
        {
            int raider = 0, hazmat = 0, drifter = 0; bool torn = false;
            foreach (var d in Worn())
            {
                if (d.style == "raider") raider += d.id == "skull_mask" ? 2 : 1;
                else if (d.style == "hazmat") hazmat++;
                else if (d.style == "drifter") drifter++;
                if (GarmentCondition(d.id) < 0.35f) torn = true;
            }
            if (raider >= 2) return "RAIDER";
            if (hazmat >= 1) return "HAZMAT";
            if (drifter >= 2) return "DRIFTER";
            if (torn || Stats.hygiene < 25f) return "RAGGED";
            if (Stats.hygiene >= 70f && Stats.wetness < 0.3f) return "CLEAN";
            return null;
        }

        // ------------------------------------------------------------------ save
        void SaveClothes(SaveData d)
        {
            d.clothWearIds = new List<string>(); d.clothWear = new List<float>();
            foreach (var kv in ClothWear) if (kv.Value > 0f) { d.clothWearIds.Add(kv.Key); d.clothWear.Add(kv.Value); }
        }

        void RestoreClothes(SaveData d)
        {
            ClothWear.Clear();
            if (d.clothWearIds == null) return;
            for (int i = 0; i < d.clothWearIds.Count && i < d.clothWear.Count; i++) ClothWear[d.clothWearIds[i]] = d.clothWear[i];
        }
    }
}
