using System.Collections.Generic;
using System.Globalization;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Living quarters aboard the generated cities (roadmap 28 R3): every flat of a <see cref="TrackedCity"/>
    /// plan has a door sign (<see cref="FlatDoor"/>); a free flat is rented by the week or bought outright for scrap,
    /// residents' flats and the ground-floor shops are taken. Aboard a city the player may only build inside a flat
    /// they hold (<see cref="CanBuildAt"/>); pieces built there ride with the city. Leases are saved
    /// (<c>SaveData.blockQuarters</c>: "city|room|owner|until day|bought").</summary>
    public partial class WastelandGame
    {
        public class Lease { public int city, room; public string owner; public float until; public bool bought; }
        public readonly List<Lease> Leases = new List<Lease>();
        public const int LeaseDays = 7;

        /// <summary>Weekly rent of a flat: its floor area and height up the block (shops pay more).</summary>
        public static int Rent(TrackedCity.Room r)
        {
            float area = (r.x1 - r.x0) * (r.y1 - r.y0);
            return Mathf.RoundToInt((24f + area * 0.7f + r.floor * 0.8f) * (r.shop ? 1.6f : 1f));
        }

        public static int BuyPrice(TrackedCity.Room r) => Rent(r) * 12;

        /// <summary>A resident's flat or a shop (deterministic per plan): not for rent.</summary>
        public static bool Occupied(TrackedCity.Plan plan, TrackedCity.Room r) => r.shop || MadMax.Voxel.Pal.Hash(r.index, plan.p.seed, 911) < 0.42f;

        public Lease LeaseOf(int city, int room) => Leases.Find(l => l.city == city && l.room == room);

        /// <summary>The player holds this lease (bought, or rent paid to a day still ahead).</summary>
        public bool Holds(Lease l) => l != null && l.owner == Stats.name && (l.bought || l.until > DayNight.TotalDays);

        static bool Live(Lease l) => l != null && (l.bought || l.until > DayNight.TotalDays);

        public static string FlatLabel(TrackedCity.Plan plan, TrackedCity.Room r)
        {
            int n = 0;
            foreach (var o in plan.rooms) { if (o == r) break; if (o.block == r.block && o.floor == r.floor) n++; }
            return "FLAT " + (r.block + 1) + "-" + (r.floor + 1).ToString("00") + (char)('A' + n);
        }

        public string FlatPrompt(CityHost c, int room)
        {
            if (!c || c.CityPlan == null || room < 0 || room >= c.CityPlan.rooms.Count) return null;
            var r = c.CityPlan.rooms[room];
            var l = LeaseOf(CityHost.Hosts.IndexOf(c), room);
            string name = FlatLabel(c.CityPlan, r);
            if (Holds(l)) return l.bought ? name + ": YOURS" : name + ": RENTED TO DAY " + Mathf.FloorToInt(l.until) + "  [T] PAY A WEEK (" + Rent(r) + " SCRAP)";
            if (r.shop) return name + ": A SHOP";
            if (Occupied(c.CityPlan, r) || Live(l)) return name + ": SOMEONE LIVES HERE";
            return name + ": FOR RENT  [E] RENT A WEEK (" + Rent(r) + " SCRAP)  [T] BUY (" + BuyPrice(r) + " SCRAP)";
        }

        public void FlatUse(CityHost c, int room, bool secondary)
        {
            if (!c || c.CityPlan == null || room < 0 || room >= c.CityPlan.rooms.Count) return;
            var r = c.CityPlan.rooms[room];
            int ci = CityHost.Hosts.IndexOf(c);
            var l = LeaseOf(ci, room);
            string name = FlatLabel(c.CityPlan, r);
            if (Holds(l))
            {
                if (!secondary || l.bought) return;
                if (!Inventory.TrySpend(ResourceType.Scrap, Rent(r))) { Toast("NOT ENOUGH SCRAP (" + Rent(r) + ")"); return; }
                l.until += LeaseDays;
                Toast(name + " PAID TO DAY " + Mathf.FloorToInt(l.until));
                return;
            }
            if (r.shop || Occupied(c.CityPlan, r) || Live(l)) return;
            int price = secondary ? BuyPrice(r) : Rent(r);
            if (!Inventory.TrySpend(ResourceType.Scrap, price)) { Toast("NOT ENOUGH SCRAP (" + price + ")"); return; }
            if (l == null) Leases.Add(l = new Lease { city = ci, room = room });
            l.owner = Stats.name; l.bought = secondary;
            l.until = Mathf.Max(l.until, DayNight.TotalDays) + LeaseDays;
            Toast(secondary ? name + " IS YOURS - BUILD INSIDE" : name + " RENTED FOR A WEEK - BUILD INSIDE");
            Journal.Add("HOME", (secondary ? "BOUGHT " : "RENTED ") + name + " IN " + c.CityName + ".");
            MadMax.Audio.Sfx.Play("cash", Player.transform.position, 0.6f);
        }

        /// <summary>Building rules aboard the generated cities: only inside a flat the player holds.</summary>
        public bool CanBuildAt(Vector3 p, out string why)
        {
            why = null;
            foreach (var c in CityHost.Hosts)
            {
                if (!c || c.CityPlan == null || !c.InCity(p)) continue;
                var r = c.RoomAt(p);
                if (r == null) { why = "NO BUILDING IN " + c.CityName + " - ONLY INSIDE A FLAT YOU RENT"; return false; }
                if (!Holds(LeaseOf(CityHost.Hosts.IndexOf(c), r.index))) { why = r.shop ? "NOT IN SOMEONE'S SHOP" : "NOT YOUR FLAT - RENT IT AT THE DOOR"; return false; }
                return true;
            }
            return true;
        }

        void QuartersSave(SaveData d)
        {
            d.blockQuarters = new List<string>();
            foreach (var l in Leases)
                d.blockQuarters.Add(l.city + "|" + l.room + "|" + l.owner + "|" + l.until.ToString("R", CultureInfo.InvariantCulture) + "|" + (l.bought ? 1 : 0));
        }

        void QuartersLoad(SaveData d)
        {
            Leases.Clear();
            if (d.blockQuarters == null) return;
            foreach (var line in d.blockQuarters)
            {
                var f = line.Split('|');
                if (f.Length < 5) continue;
                if (!int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ci) || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ri)) continue;
                float.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float until);
                Leases.Add(new Lease { city = ci, room = ri, owner = f[2], until = until, bought = f[4] == "1" });
            }
        }
    }
}
