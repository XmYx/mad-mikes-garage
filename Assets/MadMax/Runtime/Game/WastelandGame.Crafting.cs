using System.Collections.Generic;
using System.Text;
using MadMax.Building;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Crafting over time (roadmap 5): stations work off queued jobs (inputs paid when queued, refunded on
    /// cancel), finished goods go to the crafter if near or wait in the station's tray. Tools, weapons, clothes and
    /// parts come out crude / sturdy / fine from skill, station and luck; the make changes wear, power and toughness.
    /// Also salvage (items back into half their materials) and blueprints (bp_*) that teach recipes.</summary>
    public partial class WastelandGame
    {
        /// <summary>Average make of the items of an id in the pack (0 crude .. 2 fine; unknown = sturdy). Saved.</summary>
        public readonly Dictionary<string, float> ItemQuality = new Dictionary<string, float>();
        public static readonly string[] QualityNames = { "CRUDE", "STURDY", "FINE" };

        public float QualityOf(string id) => id != null && ItemQuality.TryGetValue(id, out var q) ? q : 1f;
        /// <summary>Wear multiplier from the make: crude tools and clothes wear out faster.</summary>
        public float QualityWear(string id) => Mathf.Lerp(1.4f, 0.7f, QualityOf(id) * 0.5f);
        /// <summary>Power / accuracy multiplier from the make (0.9 .. 1.1).</summary>
        public float QualityPower(string id) => 0.9f + 0.1f * QualityOf(id);
        public string QualityName(string id) => QualityNames[Mathf.Clamp(Mathf.RoundToInt(QualityOf(id)), 0, 2)];
        public bool HasMake(string id)
        {
            var c = ItemCatalog.Category(id);
            return (c == ItemCategory.Tool || c == ItemCategory.Weapon || c == ItemCategory.Clothing) && ItemQuality.ContainsKey(id);
        }

        void AddMade(string id, int n, int q)
        {
            int have = Inventory.GetItem(id);
            if (q >= 0) ItemQuality[id] = (QualityOf(id) * have + q * n) / Mathf.Max(1, have + n);
            Inventory.AddItem(id, n);
        }

        /// <summary>Work speed of a job: the crafter's skill for its kind of work.</summary>
        public float CraftSpeed(Recipe r) => 1f + Stats.Level(SkillFor(r)) * 0.08f;

        static Skill SkillFor(Recipe r) => r.kind == OutputKind.Part || r.kind == OutputKind.Vehicle ? Skill.Mechanics
            : r.category == RecipeCategory.Cooking || r.category == RecipeCategory.Farming ? Skill.Farming : Skill.Crafting;

        /// <summary>Crude / sturdy / fine: half the skill level, the station's tools, and a bit of luck.</summary>
        int RollQuality(Recipe r, CraftingStation st)
        {
            if (!RecipeLibrary.HasQuality(r)) return -1;
            float score = Stats.Level(SkillFor(r)) * 0.5f + (st ? st.tier : 0f) + Random.Range(-1.5f, 1.5f);
            return score < 1f ? 0 : score < 4f ? 1 : 2;
        }

        /// <summary>A queued job is done: make the goods.</summary>
        public void FinishJob(Recipe r, CraftingStation st) => Produce(r, st);

        /// <summary>Turn a paid recipe into goods (items to the crafter if near the station, else the tray).</summary>
        void Produce(Recipe r, CraftingStation station)
        {
            using var feed = Inventory.Source("MADE");
            MadMax.Story.Story.Note("craft:" + (r.kind == OutputKind.Resource ? "res:" + (int)r.outputResource : r.output));
            int q = RollQuality(r, station);
            bool near = !station || (Player && Vector3.Distance(Player.transform.position, station.transform.position) < 6f);
            string make = q >= 0 ? " (" + QualityNames[q] + ")" : "";
            switch (r.kind)
            {
                case OutputKind.Item:
                    if (near) AddMade(r.output, r.amount, q);
                    else { station.tray.AddItem(r.output, r.amount); if (q >= 0) ItemQuality[r.output] = (QualityOf(r.output) + q) * 0.5f; }
                    break;
                case OutputKind.Resource:
                    if (near) Inventory.Add(r.outputResource, r.amount); else station.tray.Add(r.outputResource, r.amount);
                    break;
                case OutputKind.Part:
                {
                    var at = station ? station.OutputPoint : Player.transform.position + Vector3.up;
                    var part = SpawnPart(r.output, at, station ? station.transform.rotation : Quaternion.identity);
                    if (part)
                    {
                        if (q >= 0) part.quality = q;
                        var rb = part.gameObject.AddComponent<Rigidbody>(); rb.mass = part.mass;
                        MadMax.Net.NetSession.Instance?.SendLooseSpawn(part);
                    }
                    break;
                }
                case OutputKind.Vehicle:
                {
                    var prefab = PrefabFor(r.output);
                    if (!prefab) break;
                    var at = station ? station.OutputPoint : Player.transform.position + Player.transform.forward * 5f;
                    var v = Instantiate(prefab, at + Vector3.up * 0.6f, station ? station.transform.rotation : Quaternion.identity).GetComponent<VehicleDriver>();
                    Register(v, fleet);
                    if (v.TryGetComponent<VehicleSystems>(out var vs)) { vs.fuel = 5f; vs.oil = vs.oil * 0.5f; }
                    MadMax.Net.NetSession.Instance?.SendVehicleSpawn(v, r.output);
                    break;
                }
            }
            if (r.byproducts != null) foreach (var (t, n) in r.byproducts) { if (near) Inventory.Add(t, n); else station.tray.Add(t, n); }
            if (MadMax.World.DebrisSystem.Instance && station)
                for (int i = 0; i < 6; i++) MadMax.World.DebrisSystem.Instance.EmitPuff(station.OutputPoint, MadMax.Voxel.Pal.Accent, 0.03f, Random.insideUnitSphere * 2f + Vector3.up, 0.3f);
            Stats.Practice(SkillFor(r), 4f + r.resources.Length * 1.5f + (q == 2 ? 3f : 0f));
            if (station) MadMax.Audio.Sfx.Play("ding", station.OutputPoint, near ? 0.6f : 0.3f, 1f, 30f);
            Toast((near || !station ? "CRAFTED " : "READY AT THE " + station.title + ": ") + r.name + make);
        }

        /// <summary>[T] at a station: take everything waiting in its tray.</summary>
        public void CollectTray(CraftingStation st)
        {
            if (!st) return;
            using var feed = Inventory.Source("MADE");
            int n = 0;
            var items = new List<KeyValuePair<string, int>>(st.tray.Items);
            foreach (var kv in items) if (kv.Value > 0) { Inventory.AddItem(kv.Key, kv.Value); n += kv.Value; }
            for (int t = 1; t < ResourceInfo.Count; t++)
            {
                int a = st.tray.Get((ResourceType)t);
                if (a > 0) { Inventory.Add((ResourceType)t, a); n += a; }
            }
            st.tray.Restore(new int[ResourceInfo.Count], new List<KeyValuePair<string, int>>());
            st.GetComponent<Placeable>()?.Dirty();
            Toast(n > 0 ? "COLLECTED FROM THE " + st.title : "THE TRAY IS EMPTY");
        }

        /// <summary>Cancel the last queued job at a station: its inputs come back.</summary>
        public void CancelLastJob(CraftingStation st)
        {
            var paidFuel = st && st.queue.Count > 0 ? st.queue[st.queue.Count - 1].paidFuel : ResourceType.None;
            var r = st ? st.CancelLast() : null;
            if (r == null) { Toast("NOTHING QUEUED"); return; }
            using var feed = Inventory.Source("REFUNDED");
            foreach (var (t, n) in r.resources) if (t != ResourceType.None) Inventory.Add(t, RecipeLibrary.Amount(n));
            foreach (var (i, n) in r.items) Inventory.AddItem(i, n);
            if (paidFuel != ResourceType.None) Inventory.Add(paidFuel, r.fuelAmount);
            Toast("CANCELLED " + r.name);
        }

        // ------------------------------------------------------------------ salvage
        /// <summary>The recipe that makes one of an item, if any (what salvage gives back half of).</summary>
        public Recipe SalvageRecipe(string id)
        {
            foreach (var r in RecipeLibrary.All)
                if (r.kind == OutputKind.Item && r.output == id && r.resources.Length > 0) return r;
            return null;
        }

        /// <summary>What salvaging one item returns: half its recipe per unit made (more with the Salvaging skill).</summary>
        public List<(ResourceType, int)> SalvageYield(string id)
        {
            var list = new List<(ResourceType, int)>();
            var r = SalvageRecipe(id);
            if (r == null) return list;
            float share = (0.5f + Stats.Level(Skill.Salvaging) * 0.03f) / Mathf.Max(1, r.amount);
            bool any = false;
            foreach (var (t, n) in r.resources)
            {
                if (t == ResourceType.None || ResourceInfo.IsFluid(t)) continue;
                int got = Mathf.FloorToInt(n * share);
                if (got <= 0 && !any) got = 1;
                if (got > 0) { list.Add((t, got)); any = true; }
            }
            return list;
        }

        public bool Salvage(string id)
        {
            var yield = SalvageYield(id);
            using var feed = Inventory.Source("SALVAGED");
            if (yield.Count == 0 || !Inventory.TakeItem(id)) return false;
            var sb = new StringBuilder("SALVAGED " + ItemCatalog.Name(id) + ":");
            foreach (var (t, n) in yield) { Inventory.Add(t, n); sb.Append(" +").Append(n).Append(' ').Append(ResourceInfo.Name(t)); }
            // the last one of a worn garment or a held tool is gone
            if (Inventory.GetItem(id) <= 0)
            {
                var cd = ClothingLibrary.Get(id);
                if (cd != null && Player.Rig.outfit.Remove(cd.id)) Player.RebuildBody();
                if (Player.Tool && Player.Tool.id == id) Player.Equip(null);
                SyncHotbar();
            }
            Stats.Practice(Skill.Salvaging, 2f);
            MadMax.Audio.Sfx.Play2D("ratchet", 0.5f, 0.8f);
            Toast(sb.ToString());
            return true;
        }

        /// <summary>Loose vehicle parts a station can break down (within 5 m): tyres give rubber, engines metal and copper.</summary>
        public List<VehiclePart> SalvageableParts(CraftingStation st)
        {
            var list = new List<VehiclePart>();
            if (!st || (st.type != "garage" && st.type != "workbench")) return list;
            foreach (var p in VehiclePart.Registry)
                if (p && !p.Socket && p != Player.Carried && Vector3.Distance(p.transform.position, st.transform.position) < 5f) list.Add(p);
            return list;
        }

        public List<(ResourceType, int)> PartYield(VehiclePart p)
        {
            var list = new List<(ResourceType, int)>();
            float k = (0.6f + Stats.Level(Skill.Salvaging) * 0.03f) * (1f - Mathf.Clamp01(p.damage) * 0.5f);
            int M(float x) => Mathf.Max(1, Mathf.RoundToInt(x * k));
            switch (p.category)
            {
                case PartCategory.Wheel: list.Add((ResourceType.Rubber, M(p.mass / 4f))); list.Add((ResourceType.Iron, M(p.mass / 20f))); break;
                case PartCategory.Engine: list.Add((ResourceType.Iron, M(p.mass / 10f))); list.Add((ResourceType.Aluminium, M(3f))); list.Add((ResourceType.Copper, M(4f))); break;
                case PartCategory.Radiator: list.Add((ResourceType.Copper, M(p.mass / 8f))); list.Add((ResourceType.Scrap, M(3f))); break;
                default: list.Add((ResourceType.Scrap, M(p.mass / 8f))); list.Add((ResourceType.Iron, M(p.mass / 25f))); break;
            }
            return list;
        }

        public void SalvagePart(VehiclePart p)
        {
            if (!p) return;
            using var feed = Inventory.Source("SALVAGED");
            var sb = new StringBuilder("BROKE DOWN " + p.partId.Replace('_', ' ').ToUpperInvariant() + ":");
            foreach (var (t, n) in PartYield(p)) { Inventory.Add(t, n); sb.Append(" +").Append(n).Append(' ').Append(ResourceInfo.Name(t)); }
            Destroy(p.gameObject);
            Stats.Practice(Skill.Salvaging, 4f);
            MadMax.Audio.Sfx.Play2D("ratchet", 0.6f, 0.7f);
            Toast(sb.ToString());
        }

        // ------------------------------------------------------------------ blueprints
        /// <summary>Study a blueprint (bp_*): its recipe knowledge is learned for good.</summary>
        bool UseBlueprint(string id)
        {
            if (Stats.knowledge.Contains(id)) { Toast("YOU ALREADY KNOW THIS ONE"); return false; }
            Stats.knowledge.Add(id);
            Toast("LEARNED: " + ItemCatalog.Name(id).Replace("BLUEPRINT: ", ""));
            Stats.Practice(Skill.Crafting, 5f);
            MadMax.Audio.Sfx.Play2D("scratch", 0.5f, 1.2f);
            return true;
        }

        void SaveCrafting(SaveData d)
        {
            d.itemQualityIds = new List<string>(); d.itemQuality = new List<float>();
            foreach (var kv in ItemQuality) { d.itemQualityIds.Add(kv.Key); d.itemQuality.Add(kv.Value); }
        }

        void RestoreCrafting(SaveData d)
        {
            ItemQuality.Clear();
            if (d.itemQualityIds == null) return;
            for (int i = 0; i < d.itemQualityIds.Count && i < d.itemQuality.Count; i++) ItemQuality[d.itemQualityIds[i]] = d.itemQuality[i];
        }
    }
}
