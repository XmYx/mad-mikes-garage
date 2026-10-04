using System.Collections.Generic;

namespace MadMax.Game
{
    /// <summary>Automation hooks for acceptance scenarios (roadmap 26 Q1): read the open page's rows and choose one the
    /// way ENTER / A / D on it would, so talk, trade and board pages are driven through their real entries.</summary>
    public partial class MenuSystem
    {
        /// <summary>Labels of the open page's rows (only the selectable ones unless <paramref name="all"/>).</summary>
        public List<string> Labels(bool all = false)
        {
            var l = new List<string>();
            foreach (var it in items) if (it.label != null && (all || (Enabled(it) && (it.confirm != null || it.adjust != null)))) l.Add(it.label);
            return l;
        }

        /// <summary>ENTER on the first selectable row whose label starts with <paramref name="prefix"/>; false when there is none.</summary>
        public bool Pick(string prefix)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.label == null || !it.label.StartsWith(prefix) || !Enabled(it) || it.confirm == null) continue;
                cursor = i;
                it.confirm();
                return true;
            }
            return false;
        }

        /// <summary>A (dir &lt; 0) or D (dir &gt; 0) on the first selectable row whose label starts with <paramref name="prefix"/>.</summary>
        public bool Adjust(string prefix, int dir)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.label == null || !it.label.StartsWith(prefix) || !Enabled(it) || it.adjust == null) continue;
                cursor = i;
                it.adjust(dir);
                return true;
            }
            return false;
        }

        /// <summary>Catalogue pages (crafting, build): switch to category <paramref name="i"/> as a click on it would.</summary>
        public void PickCategory(int i) { if (IsCatalogue) SetCatalogueCategory(i); }

        /// <summary>Catalogue pages: cells laid out on screen in the last draw (inside the canvas) and the grid's columns.</summary>
        public int VisibleCells(out int columns)
        {
            columns = gridCols;
            var canvas = PixelHud.Canvas;
            int n = 0;
            foreach (var it in items) if (it.rect.width > 0 && canvas != null && it.rect.xMax <= canvas.w && it.rect.yMax <= canvas.h && it.rect.x >= 0 && it.rect.y >= 0) n++;
            return n;
        }

        /// <summary>Catalogue pages: the panel lines of the selected cell (what the hover panel shows).</summary>
        public List<string> TipLines()
        {
            var l = new List<string>();
            if (cursor < 0 || cursor >= items.Count) return l;
            var it = items[cursor];
            var lines = it.recipe != null ? RecipeTip(it.recipe, it.known) : it.piece != null ? PieceTip(it.piece) : null;
            if (lines != null) foreach (var (t, _) in lines) if (t != null) l.Add(t);
            return l;
        }

        /// <summary>What the person in an open conversation just said (null when no conversation is open).</summary>
        public string TalkLine => (Current == Page.Talk || Current == Page.Trade) && talk != null ? talk.line : null;
    }
}
