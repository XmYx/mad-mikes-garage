using System.Collections.Generic;
using MadMax.Items;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>What came into and went out of the player's pack, as short HUD rows ("+3 SCRAP  PICKED UP"). Fed by
    /// <see cref="Inventory.Delta"/> with the <see cref="Inventory.Source"/> label; the same id, label and direction
    /// merge within <see cref="MergeWindow"/>; rows live <see cref="Life"/> seconds after their last change, then slide
    /// out. Drawn by <c>PixelHud.DrawItemFeed</c>. Unscaled time: menus that pause the world keep the feed running.</summary>
    public static class ItemFeed
    {
        public const float MergeWindow = 1.5f, Life = 4f, SlideOut = 0.4f;
        public const int MaxRows = 6;

        public sealed class Row
        {
            public string id;               // item id, or null for a resource
            public ResourceType res;
            public int amount;              // signed
            public string source;           // BOUGHT, MADE ... (null = not said)
            public float born, last;        // unscaled time of the first and the latest change
            public string text;             // "+3 SCRAP"
            public Color32[] icon;          // set by the HUD on first draw
            public string Key => id ?? "res:" + (int)res;
        }

        /// <summary>Newest first.</summary>
        public static readonly List<Row> Rows = new List<Row>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Rows.Clear();

        public static void Push(string id, ResourceType res, int amount, string source) => Push(id, res, amount, source, Time.unscaledTime);

        public static void Push(string id, ResourceType res, int amount, string source, float now)
        {
            if (amount == 0 || (id == null && res == ResourceType.None)) return;
            foreach (var r in Rows)
                if (r.id == id && r.res == res && r.source == source && (r.amount > 0) == (amount > 0) && now - r.last <= MergeWindow)
                {
                    r.amount += amount; r.last = now; r.text = Text(r);
                    return;
                }
            var row = new Row { id = id, res = res, amount = amount, source = source, born = now, last = now };
            row.text = Text(row);
            Rows.Insert(0, row);
            while (Rows.Count > MaxRows) Rows.RemoveAt(Rows.Count - 1);
        }

        /// <summary>Drop rows that have slid out.</summary>
        public static void Tick(float now)
        {
            for (int i = Rows.Count - 1; i >= 0; i--) if (now - Rows[i].last > Life + SlideOut) Rows.RemoveAt(i);
        }

        /// <summary>0 while shown, rising to 1 as the row slides out.</summary>
        public static float Out(Row r, float now) => Mathf.Clamp01((now - r.last - Life) / SlideOut);

        public static void Clear() => Rows.Clear();

        /// <summary>The live row for an id or resource with a label (null = any label), for tests.</summary>
        public static Row Find(string id, ResourceType res, string source = null, bool gain = true)
        {
            foreach (var r in Rows) if (r.id == id && r.res == res && (source == null || r.source == source) && (r.amount > 0) == gain) return r;
            return null;
        }

        static string Text(Row r)
        {
            string n = (r.amount > 0 ? "+" : "-") + Mathf.Abs(r.amount);
            if (r.id != null) return n + " " + ItemCatalog.Name(r.id);
            return n + (ResourceInfo.IsFluid(r.res) ? "L " : " ") + ResourceInfo.Name(r.res);
        }
    }
}
