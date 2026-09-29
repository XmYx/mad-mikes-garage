using System.Collections.Generic;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Tool wear and repair. Each tool id has one "in hand" condition (the stack wears one item at a time):
    /// strikes, shots and burning wear it; at 100 % wear the item breaks and the next one of the stack takes over.
    /// Crafting skill slows wear. Workbenches repair (Repair page) for the tool's own materials. Also: the repair kit,
    /// and the jack needed for big wheels.</summary>
    public partial class WastelandGame
    {
        /// <summary>0 = like new .. 1 = breaks, per tool id (saved).</summary>
        public readonly Dictionary<string, float> ToolWear = new Dictionary<string, float>();

        public float Condition(string id) => ToolWear.TryGetValue(id, out var w) ? 1f - w : 1f;

        /// <summary>Wear the held tool by <paramref name="amount"/> (fraction of its life).</summary>
        public void WearTool(string id, float amount)
        {
            if (string.IsNullOrEmpty(id) || Inventory.GetItem(id) <= 0) return;
            ToolWear.TryGetValue(id, out var w);
            w += amount * Mathf.Max(0.5f, 1f - Stats.Level(Skill.Crafting) * 0.04f) * GameRules.Current.DamageTaken * QualityWear(id);
            if (w < 1f) { ToolWear[id] = w; return; }
            ToolWear[id] = 0f;
            Inventory.TakeItem(id);
            MadMax.Audio.Sfx.Play("hit_metal", Player.transform.position, 0.8f, 0.6f);
            Toast("YOUR " + ItemCatalog.Name(id) + " BROKE" + (Inventory.GetItem(id) > 0 ? " (NEXT ONE IN HAND)" : ""));
            if (Inventory.GetItem(id) <= 0 && Player.Tool && Player.Tool.id == id) Player.Equip(null);
            SyncHotbar();
        }

        /// <summary>What a repair at a workbench costs: the tool's main material, more for badly worn tools.</summary>
        public (ResourceType type, int amount) RepairCost(string id)
        {
            float w = 1f - Condition(id);
            var t = id.Contains("torch") && !id.Contains("gas") ? ResourceType.Cloth
                  : id == "tool_axe" || id == "tool_shovel" || id == "tool_pickaxe" || id == ItemIds.Sledgehammer ? ResourceType.Iron
                  : id.Contains("geiger") || id.Contains("flashlight") || id.Contains("welder") ? ResourceType.Copper
                  : id.Contains("binoculars") || id.Contains("lantern") ? ResourceType.Glass : ResourceType.Scrap;
            return (t, Mathf.Max(1, Mathf.CeilToInt(w * (t == ResourceType.Scrap ? 5f : 3f))));
        }

        public bool RepairTool(string id)
        {
            if (Condition(id) >= 0.999f) return false;
            var (t, n) = RepairCost(id);
            if (!Inventory.TrySpend(t, n)) { Toast("NEED " + n + " " + ResourceInfo.Name(t)); return false; }
            ToolWear[id] = 0f;
            Stats.Practice(Skill.Crafting, 3f);
            MadMax.Audio.Sfx.Play2D("ratchet", 0.6f);
            Toast(ItemCatalog.Name(id) + " REPAIRED");
            return true;
        }

        /// <summary>Repair kit: the most damaged part of the vehicle you are in or next to gets +30 % condition.</summary>
        bool UseRepairKit()
        {
            VehicleDriver v = Current;
            if (!v)
            {
                float best = 4f;
                foreach (var c in AllVehicles)
                {
                    if (!c) continue;
                    float d = Vector3.Distance(c.transform.position, Player.transform.position);
                    if (d < best) { best = d; v = c; }
                }
            }
            if (!v) { Toast("NO VEHICLE IN REACH"); return false; }
            VehiclePart worst = null;
            foreach (var p in v.GetComponentsInChildren<VehiclePart>()) if (p.Socket && (!worst || p.damage > worst.damage)) worst = p;
            if (!worst || worst.damage < 0.02f) { Toast("NOTHING TO FIX"); return false; }
            worst.damage = Mathf.Max(0f, worst.damage - 0.3f - Stats.Level(Skill.Mechanics) * 0.02f);
            if (worst.TryGetComponent<WheelStats>(out var ws) && ws.Popped) ws.wear = 0.8f;       // patched, not new
            Stats.Practice(Skill.Mechanics, 5f);
            MadMax.Audio.Sfx.Play("ratchet", v.transform.position, 0.8f);
            Toast("PATCHED " + worst.partId.Replace('_', ' ').ToUpperInvariant() + " (" + Mathf.RoundToInt((1f - worst.damage) * 100f) + "%)");
            return true;
        }

        /// <summary>Wheels of size 3+ (truck, tracks) need the jack in the pack as well as the wrench.</summary>
        bool NeedsJack(VehiclePart p) => p && p.category == PartCategory.Wheel && p.sizeClass >= 3 && Inventory.GetItem("tool_jack") <= 0;

        void SaveTools(SaveData d)
        {
            d.toolWearIds = new List<string>(); d.toolWear = new List<float>();
            foreach (var kv in ToolWear) if (kv.Value > 0f) { d.toolWearIds.Add(kv.Key); d.toolWear.Add(kv.Value); }
        }

        void RestoreTools(SaveData d)
        {
            ToolWear.Clear();
            if (d.toolWearIds == null) return;
            for (int i = 0; i < d.toolWearIds.Count && i < d.toolWear.Count; i++) ToolWear[d.toolWearIds[i]] = d.toolWear[i];
        }
    }
}
