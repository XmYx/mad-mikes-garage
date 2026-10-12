using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>The housing clerk aboard a generated city: finds a free flat (the cheapest), rents it for a week and pins
    /// its door on the map (WastelandGame.Quarters).</summary>
    public partial class Dialogue
    {
        void QuartersChoice()
        {
            if (!npc.clerk || !npc.aboard || npc.aboard.CityPlan == null) return;
            var city = npc.aboard;
            int ci = CityHost.Hosts.IndexOf(city);
            TrackedCity.Room pick = null; int best = int.MaxValue;
            foreach (var r in city.CityPlan.rooms)
            {
                if (MadMax.Game.WastelandGame.Occupied(city.CityPlan, r)) continue;
                var l = g.LeaseOf(ci, r.index);
                if (l != null && (l.bought || l.until > DayNight.TotalDays)) continue;
                int rent = MadMax.Game.WastelandGame.Rent(r);
                if (rent < best) { best = rent; pick = r; }
            }
            if (pick == null) { Add("I NEED A PLACE TO LIVE.", () => { asked = true; line = "FULL UP, FRIEND. EVERY FLAT HERE IS TAKEN."; Hub(false); }); return; }
            var room = pick;
            string label = MadMax.Game.WastelandGame.FlatLabel(city.CityPlan, room);
            Add("(" + best + " SCRAP) I NEED A PLACE TO LIVE.", () =>
            {
                asked = true;
                g.FlatUse(city, room.index, false);                                                   // pays the week
                if (!g.Holds(g.LeaseOf(ci, room.index))) { line = "A WEEK UP FRONT. " + best + " SCRAP, NO CREDIT."; Hub(false); return; }
                g.SetWaypoint(city.PlanPoint(room.door), label, true);
                line = label + ". BLOCK " + (room.block + 1) + ", FLOOR " + (room.floor + 1) + ". STAIRS ARE IN THE MIDDLE. RENT'S DUE EVERY SEVEN DAYS - PAY AT THE DOOR.";
                Change(1);
                Hub(false);
            }, "A WEEK'S RENT; BUILD INSIDE YOUR FLAT");
        }
    }
}
