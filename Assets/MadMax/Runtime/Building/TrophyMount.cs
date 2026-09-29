using MadMax.Items;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Wall plaque for a trophy item (trophy_*: licence plates, hood ornaments, skulls, later animal heads).
    /// [E] mounts the next trophy from the pack (swapping the current one back), [T] takes it down.</summary>
    public class TrophyMount : MonoBehaviour, IInteractable, IPlaceState
    {
        public string trophy;
        GameObject shown;

        public string Prompt(MadMax.Game.WastelandGame g) => string.IsNullOrEmpty(trophy) ? "[E] MOUNT TROPHY"
            : ItemCatalog.Name(trophy) + "  [E] SWAP  [T] TAKE DOWN";

        public void Use(MadMax.Game.WastelandGame g, bool secondary)
        {
            if (secondary)
            {
                if (string.IsNullOrEmpty(trophy)) return;
                g.Inventory.AddItem(trophy);
                g.Toast("TOOK DOWN " + ItemCatalog.Name(trophy));
                Set(null);
                return;
            }
            string next = null;
            foreach (var kv in g.Inventory.Items) if (kv.Value > 0 && kv.Key.StartsWith("trophy_") && kv.Key != trophy) { next = kv.Key; break; }
            if (next == null) { g.Toast("NO TROPHIES IN YOUR PACK"); return; }
            g.Inventory.TakeItem(next);
            if (!string.IsNullOrEmpty(trophy)) g.Inventory.AddItem(trophy);
            Set(next);
            MadMax.Audio.Sfx.Play("hammer", transform.position, 0.5f, 1.3f);
            g.Toast("MOUNTED " + ItemCatalog.Name(next));
        }

        void Set(string id)
        {
            trophy = id;
            if (shown) Destroy(shown);
            if (!string.IsNullOrEmpty(id))
            {
                var mesh = ItemModels.Get(id);
                shown = new GameObject("Trophy", typeof(MeshFilter), typeof(MeshRenderer));
                shown.transform.SetParent(transform, false);
                // item models stand on +Y facing +Z: +Y goes up the wall (+Z), their front out of it (+Y)
                var rot = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                shown.transform.localRotation = rot;
                shown.transform.localScale = Vector3.one * 1.6f;
                shown.transform.localPosition = new Vector3(0f, 0.1f, 0f) - rot * (mesh.bounds.center * 1.6f);
                shown.GetComponent<MeshFilter>().sharedMesh = mesh;
                shown.GetComponent<MeshRenderer>().sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
            }
            GetComponent<Placeable>()?.Dirty();
        }

        public string SaveState() => trophy ?? "";
        public void LoadState(string s) => Set(string.IsNullOrEmpty(s) ? null : s);
    }
}
