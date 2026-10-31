using System.Collections.Generic;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-31 (1 Hz, chained from the 1030 hook). Audit of the "Gaps &amp; suggestions" world
    /// landmarks plus three open suggestions; the pieces live on their own: the scrapyard's working crane
    /// (<c>ScrapyardCrane</c>), the Remnant guard at each checkpoint (<c>CheckpointGuard</c>), frost on parked cars' glass
    /// (<c>GlassFrost</c> + the <c>_Frost</c> shader pattern), chalk timetable boards on the platforms
    /// (<c>TimetableBoard</c>), gang marks at the ends of a gang's road (<c>GangMarks</c>). Here: showing boards and
    /// marks near the player and the SCRAPE THE ICE context option.</summary>
    public partial class WastelandGame
    {
        public const float ScrapeReach = 3.2f;

        /// <summary>The frosted car beside <paramref name="p"/> (null: none).</summary>
        public static GlassFrost FrostedNear(Vector3 p, float reach)
        {
            GlassFrost best = null; float bd = reach * reach;
            foreach (var f in GlassFrost.All)
            {
                if (!f || f.Frost < 0.05f) continue;
                var c = f.GetComponent<VehicleDriver>();
                var at = c && c.Body ? c.Body.ClosestPointOnBounds(p) : f.transform.position;
                float d = (at - p).sqrMagnitude;
                if (d < bd) { bd = d; best = f; }
            }
            return best;
        }

        void ScrapeOptions(ContextTarget t, List<ContextOption> into)
        {
            if (t.item != null || !Player || Current) return;
            var f = FrostedNear(Player.transform.position, ScrapeReach);
            if (!f) return;
            into.Add(new ContextOption
            {
                label = "SCRAPE THE ICE OFF THE GLASS",
                blocked = f.Scraping ? "UNDER WAY" : null,
                run = () => { if (f && f.StartScrape(Player.transform)) Toast("SCRAPING THE FROST OFF THE WINDOWS"); }
            });
        }

        void UpdateNightly1031()
        {
            if (!ContextActions.Has("car.scrape")) ContextActions.Register("car.scrape", (g, t, into) => g.ScrapeOptions(t, into));
            if (!Ready || !Player) return;
            var focus = FocusPos;
            TimetableBoard.Tend(focus, propMaterial);
            var net = MadMax.Net.NetSession.Instance;
            if (!(net && net.IsClient)) GangMarks.Ensure().Tend(focus, propMaterial, this);
        }
    }
}
