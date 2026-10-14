using MadMax.Items;
using MadMax.RPG;

namespace MadMax.Game
{
    /// <summary>Surgeons fitting prosthetics for scrap: medical (salvage) traders strap a prosthetic from the pack onto a
    /// healed stump properly (no chafing, as at a clinic bed) for <see cref="SurgeonScrap"/>.</summary>
    public partial class WastelandGame
    {
        public const int SurgeonScrap = 25;

        /// <summary>The first healed, bare stump and a prosthetic in the pack that fits it (false: nothing to fit).</summary>
        public bool SurgeonCanFit(out BodyZone zone, out string id)
        {
            zone = BodyZone.ArmL; id = null;
            if (!Player || Look == null) return false;
            foreach (var z in Limbs.All)
            {
                if (!Limbs.Severed(Look.lost, z) || ProstheticLibrary.FittedOn(Look, z) != null || !StumpReady(z)) continue;
                foreach (var d in ProstheticLibrary.All)
                    if (d.Fits(z) && Inventory.GetItem(d.id) > 0) { zone = z; id = d.id; return true; }
            }
            return false;
        }

        /// <summary>Pay the surgeon and have it fitted (false: not enough scrap or nothing fits).</summary>
        public bool SurgeonFit()
        {
            if (!SurgeonCanFit(out var z, out var id)) return false;
            if (Inventory.Get(ResourceType.Scrap) < SurgeonScrap) { Toast("THE SURGEON WANTS " + SurgeonScrap + " SCRAP"); return false; }
            if (!FitProsthetic(z, id, true)) return false;
            using (Inventory.Source(null, "PAID")) Inventory.TrySpend(ResourceType.Scrap, SurgeonScrap);
            return true;
        }
    }
}
