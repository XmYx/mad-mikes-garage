using System.Collections.Generic;

namespace MadMax.Game
{
    /// <summary>Acceptance access to the open page: what each row says (label, and the full text a row shows as its hint
    /// when the label is cut to fit).</summary>
    public partial class MenuSystem
    {
        public List<string> Rows()
        {
            var l = new List<string>();
            foreach (var it in items) l.Add(it.hint != null ? it.label + " | " + it.hint : it.label);
            return l;
        }
    }
}
