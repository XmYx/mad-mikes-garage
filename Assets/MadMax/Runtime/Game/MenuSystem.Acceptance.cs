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

        /// <summary>What the person in an open conversation just said (null when no conversation is open).</summary>
        public string TalkLine => (Current == Page.Talk || Current == Page.Trade) && talk != null ? talk.line : null;
    }
}
