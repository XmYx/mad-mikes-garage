using MadMax.Items;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Town mechanics (parts vendors) as locksmiths: bring a vehicle whose key is lost within 40 m, pay
    /// <c>WastelandGame.LocksmithScrap</c> and collect the key from them the next day.</summary>
    public partial class Dialogue
    {
        public bool Locksmith => P.kind == "parts" && !P.Raider;

        void KeyChoice()
        {
            if (!Locksmith || S.disposition <= -40) return;
            bool waiting = g.KeyOrders.Exists(o => o.smith == P.id);
            if (waiting)
                Add("IS MY KEY READY?", () =>
                {
                    asked = true;
                    int n = g.CollectKeys(P.id);
                    line = n > 0 ? "HERE. CUT IT FROM THE LOCK BARREL - SHE'LL TURN OVER SWEET." : "NOT YET. COME BACK TOMORROW.";
                    if (n > 0) g.Toast("GOT " + (n == 1 ? "THE KEY" : n + " KEYS"));
                    Hub(false);
                });
            var ign = g.KeylessNear(npc.transform.position);
            if (!ign) return;
            string car = MadMax.Game.WastelandGame.Name(ign);
            Add("(" + MadMax.Game.WastelandGame.LocksmithScrap + " SCRAP) CAN YOU CUT A KEY FOR THE " + car + "?", () =>
            {
                asked = true;
                if (g.OrderKey(ign, P.id)) { line = "LEAVE IT WITH ME. I'LL PULL THE BARREL AND HAVE A KEY BY TOMORROW."; Change(1); }
                else line = "SCRAP FIRST. " + MadMax.Game.WastelandGame.LocksmithScrap + ", AND IT'S YOURS TOMORROW.";
                Hub(false);
            }, "READY TOMORROW - COLLECT IT HERE");
        }
    }
}
