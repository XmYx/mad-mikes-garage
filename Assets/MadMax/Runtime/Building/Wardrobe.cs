using System.Collections.Generic;
using MadMax.Game;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Wardrobe (with a Container): [T] swaps outfits — what you wear goes onto the hangers, the garments
    /// hanging there (one per slot) go on. Keeps a winter and a summer set a key press apart.</summary>
    public class Wardrobe : MonoBehaviour, IInteractable
    {
        public string Prompt(WastelandGame g) => "[T] SWAP OUTFIT";

        static readonly List<string> worn = new List<string>();
        static readonly Dictionary<ClothingSlot, string> hanging = new Dictionary<ClothingSlot, string>();

        public void Use(WastelandGame g, bool secondary)
        {
            if (!secondary || !TryGetComponent<Container>(out var box)) return;
            var rig = g.Player.Rig;
            worn.Clear(); worn.AddRange(rig.outfit);
            hanging.Clear();
            foreach (var kv in box.inventory.Items)
            {
                if (kv.Value <= 0) continue;
                var d = ClothingLibrary.Get(kv.Key);
                if (d != null && !hanging.ContainsKey(d.slot)) hanging[d.slot] = d.id;
            }
            if (worn.Count == 0 && hanging.Count == 0) { g.Toast("NOTHING TO WEAR OR HANG UP"); return; }
            foreach (var id in worn) { var item = "cloth_" + id; if (g.Inventory.TakeItem(item)) box.inventory.AddItem(item); }
            rig.outfit.Clear();
            int on = 0;
            foreach (var id in hanging.Values) { var item = "cloth_" + id; if (box.inventory.TakeItem(item)) { g.Inventory.AddItem(item); rig.outfit.Add(id); on++; } }
            g.Player.RebuildBody();
            MadMax.Audio.Sfx.Play2D("scratch", 0.5f);
            g.Toast(on == 0 ? "HUNG UP YOUR CLOTHES" : "CHANGED OUTFIT (" + on + " PIECE" + (on == 1 ? "" : "S") + ")");
            GetComponent<Placeable>()?.Dirty();
        }
    }
}
