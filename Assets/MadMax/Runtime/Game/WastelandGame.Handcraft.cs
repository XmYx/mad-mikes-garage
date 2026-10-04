using System.Collections.Generic;
using System.Globalization;
using MadMax.Items;
using MadMax.RPG;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Handcraft (the HANDCRAFT menu, <see cref="Controls.Act.Craft"/>): recipes flagged <see cref="Recipe.hand"/>
    /// are made anywhere from the pack, as personal jobs — paid when queued (up to <see cref="HandQueueMax"/>), worked
    /// off one at a time a third slower than at a bench, carried on while you walk or drive, refunded when cancelled.
    /// Saved in <c>SaveData.handcraft</c> ("recipe id|progress").</summary>
    public partial class WastelandGame
    {
        public const int HandQueueMax = 4;
        const float HandSlower = 1.35f;

        public class HandJob { public Recipe recipe; public float progress, speed, costMult; }
        public readonly List<HandJob> HandQueue = new List<HandJob>();

        /// <summary>The job being worked on (null: idle).</summary>
        public HandJob HandCurrent => HandQueue.Count > 0 ? HandQueue[0] : null;

        /// <summary>Queue a hand recipe: pays from the pack; false (with a toast) when it can't be made now.</summary>
        public bool Handcraft(Recipe r)
        {
            if (r == null || !r.hand) return false;
            if (HandQueue.Count >= HandQueueMax) { Toast("YOUR HANDS ARE FULL: " + HandQueueMax + " THINGS ON THE GO"); return false; }
            string why = CraftBlockReason(r, null);
            if (why != null) { Toast(why); return false; }
            using (Inventory.Source("MADE", "USED"))
            {
                foreach (var (t, n) in r.resources) if (t != ResourceType.None) Inventory.TrySpend(t, RecipeLibrary.Amount(n));
                foreach (var (i, n) in r.items) Inventory.TakeItem(i, n);
            }
            HandQueue.Add(new HandJob { recipe = r, speed = CraftSpeed(r), costMult = RecipeLibrary.CostMult });
            MadMax.Audio.Sfx.Play2D("click", 0.5f);
            Toast((HandQueue.Count == 1 ? "MAKING " : "NEXT UP: ") + r.name + " (" + Mathf.CeilToInt(HandSeconds(r) / CraftSpeed(r)) + " S)");
            return true;
        }

        /// <summary>Seconds a hand recipe takes at no skill.</summary>
        public static float HandSeconds(Recipe r) => RecipeLibrary.Seconds(r) * HandSlower;

        /// <summary>Cancel the last queued hand job: its materials come back.</summary>
        public void CancelLastHandJob()
        {
            if (HandQueue.Count == 0) { Toast("NOTHING IN HAND"); return; }
            var j = HandQueue[HandQueue.Count - 1];
            HandQueue.RemoveAt(HandQueue.Count - 1);
            using (Inventory.Source("REFUNDED"))
            {
                foreach (var (t, n) in j.recipe.resources) if (t != ResourceType.None) Inventory.Add(t, CharacterStats.Cost(n, j.costMult));
                foreach (var (i, n) in j.recipe.items) Inventory.AddItem(i, n);
            }
            Toast("PUT DOWN " + j.recipe.name);
        }

        void UpdateHandcraft(float dt)
        {
            var j = HandCurrent;
            if (j == null || dt <= 0f || (Vitals && Vitals.Dead)) return;
            j.progress += dt * j.speed / Mathf.Max(0.5f, HandSeconds(j.recipe));
            if (j.progress < 1f) return;
            HandQueue.RemoveAt(0);
            Produce(j.recipe, null);
        }

        // ------------------------------------------------------------------ save
        void SaveHandcraft(SaveData d)
        {
            d.handcraft = new List<string>();
            foreach (var j in HandQueue) d.handcraft.Add(j.recipe.id + "|" + j.progress.ToString("0.###", CultureInfo.InvariantCulture) + "|" + j.costMult.ToString("0.###", CultureInfo.InvariantCulture));
        }

        void LoadHandcraft(SaveData d)
        {
            HandQueue.Clear();
            if (d.handcraft == null) return;
            foreach (var s in d.handcraft)
            {
                var f = s.Split('|');
                var r = RecipeLibrary.Get(f[0]);
                if (r == null) continue;
                float.TryParse(f.Length > 1 ? f[1] : "0", NumberStyles.Float, CultureInfo.InvariantCulture, out float p);
                float cm = 1f; if (f.Length > 2) float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out cm);
                HandQueue.Add(new HandJob { recipe = r, progress = p, speed = CraftSpeed(r), costMult = cm });
            }
        }
    }
}
