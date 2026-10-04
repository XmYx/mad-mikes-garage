using MadMax.Items;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>A1 KEEP THE LIGHT ON: the player's own things lie in a canvas satchel thrown clear of the wreck (wrench,
    /// knife, canteen, a can of food, a bandage, the delivery chit). Nothing comes to the pack by walking up: the step
    /// "things" closes when the satchel (or anything in it) is taken or opened by hand.</summary>
    public partial class WastelandGame
    {
        public const string SatchelEvent = "looted:satchel";

        partial void Scene_A1()
        {
            if (Story.Story.StepDone("A1", "things") || Story.Story.Flag("a1_satchel")) return;
            var inv = new Inventory();
            inv.AddItem(ItemIds.Wrench, 1); inv.AddItem("tool_knife", 1); inv.AddItem(ItemIds.Canteen, 1);
            inv.AddItem("food_can", 1); inv.AddItem("med_bandage", 1); inv.AddItem("misc_delivery_chit", 1);
            inv.Add(ResourceType.Water, 1);
            var at = StoryAnchors.Get("satchel");
            at.y = terrain.Height(at.x, at.z) + 0.25f;
            var w = SpawnWorldItem(BagLibrary.Key("canvas_satchel", inv), 1, -1f, at, Quaternion.Euler(0f, StoryAnchors.Yaw("satchel") + 70f, 0f), null);
            w.Body.Sleep();
            Story.Story.SetFlag("a1_satchel");
        }

        partial void Tick_A1() { }

        /// <summary>Something was taken (<paramref name="key"/>, now in the pack) or opened by hand at <paramref name="at"/>:
        /// the A1 satchel counts when it lies at its anchor. Taken whole, it is emptied into the pack (the tools must be at
        /// hand for what follows) and the empty satchel kept.</summary>
        void StoryLooted(Vector3 at, string key = null)
        {
            if (Story.Story.StateOf("A1") != Story.Story.State.Active || Story.Story.StepDone("A1", "things")) return;
            var s = StoryAnchors.Get("satchel");
            if (new Vector2(at.x - s.x, at.z - s.z).sqrMagnitude >= 16f) return;
            if (key != null && BagLibrary.IsBag(key) && BagLibrary.IsFilled(key) && Inventory.TakeItem(key))
            {
                var inv = new Inventory();
                BagLibrary.Contents(key, inv);
                using (Inventory.Source("YOUR SATCHEL"))
                {
                    foreach (var kv in inv.Items) if (kv.Value > 0) GiveToPack(kv.Key, kv.Value, -1f);
                    for (int r = 1; r < ResourceInfo.Count; r++) if (inv.Get((ResourceType)r) > 0) Inventory.Add((ResourceType)r, inv.Get((ResourceType)r));
                    Inventory.AddItem(BagLibrary.ItemId(key));
                }
            }
            Story.Story.Note(SatchelEvent);
        }
    }
}
