using MadMax.Game;

namespace MadMax.Npc
{
    /// <summary>Medical (salvage) traders fit a prosthetic from the pack onto a healed stump for scrap
    /// (<see cref="WastelandGame.SurgeonFit"/>).</summary>
    public partial class Dialogue
    {
        void SurgeonChoice()
        {
            if (P.kind != "salvage" || P.Raider || S.disposition <= -30) return;
            if (!g.SurgeonCanFit(out var z, out var id)) return;
            Add("(" + WastelandGame.SurgeonScrap + " SCRAP) CAN YOU FIT THIS " + MadMax.Items.ItemCatalog.Name(id) + " ON MY " + Limbs.Name(z) + "?", () =>
            {
                asked = true;
                if (g.SurgeonFit()) { line = "HOLD STILL. STRAPS HERE, PADDING THERE... THERE. IT'LL FEEL LIKE YOURS IN A WEEK."; Change(2); }
                else line = "SCRAP FIRST, FRIEND. " + WastelandGame.SurgeonScrap + " AND I'LL DO IT RIGHT.";
                Hub(false);
            }, "FITTED PROPERLY, NO CHAFING");
        }
    }
}
