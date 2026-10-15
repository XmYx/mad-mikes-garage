using MadMax.Game;

namespace MadMax.Npc
{
    /// <summary>2026-10-15: the favour a neighbour who looked in on the laid-up player asks later
    /// (<see cref="WastelandGame.NeighbourErrands"/>), and letting a trusted crew medic buy their own dressings.</summary>
    public partial class Dialogue
    {
        void NeighbourChoice()
        {
            var e = g.ErrandOf(P);
            if (e == null) return;
            var a = WastelandGame.NeighbourAsks[e.kind];
            if (WastelandGame.ErrandDue(e))
                Add("YOU LOOKED IN ON ME. ANYTHING I CAN DO FOR YOU?", () =>
                {
                    asked = true;
                    line = a.ask;
                    choices.Clear();
                    Add("CONSIDER IT DONE.", () => { g.TakeErrand(P); Change(2); line = "YOU'RE A GOOD NEIGHBOUR. I'LL BE HERE."; NpcVoice.Say(npc, "thanks", true); Hub(false); });
                    Add("NOT RIGHT NOW.", () => Hub());
                }, "A FAVOUR FOR A FAVOUR");
            else if (e.state == 1)
                Add("I BROUGHT WHAT YOU NEEDED.", () =>
                {
                    asked = true;
                    string thanks = g.DoErrand(P);
                    line = thanks ?? "THAT'S NOT IT. " + a.n + " " + Trade.Name(a.item) + ", REMEMBER?";
                    if (thanks != null) NpcVoice.Say(npc, "thanks", true);
                    Hub(false);
                });
        }

        void MedicShopChoice()
        {
            if (!Companions.Patches(npc.Profile)) return;
            bool may = g.MedicMayShop.Contains(P.id);
            if (!may && S.disposition >= WastelandGame.MedicShopTrust)
                Add("WHEN YOUR KIT RUNS DRY IN A TOWN, BUY BANDAGES OUT OF THE CREW'S SCRAP.", () => { g.LetMedicShop(P, true); End("WILL DO. I'LL TELL YOU WHAT IT COST."); }, "THEY RESTOCK THEMSELVES AT TOWN STALLS");
            else if (!may) Add("(THEY DON'T KNOW YOU WELL ENOUGH TO SPEND THE CREW'S SCRAP)", () => CompanionHub());
            else Add("ASK ME BEFORE YOU SPEND THE CREW'S SCRAP.", () => { g.LetMedicShop(P, false); End("FAIR ENOUGH."); });
        }
    }
}
