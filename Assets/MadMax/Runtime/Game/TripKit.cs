using System.Collections.Generic;
using MadMax.Items;
using MadMax.Vehicles;

namespace MadMax.Game
{
    /// <summary>What should ride along on a trip: a short checklist (extinguisher, first-aid kit, spare key, jack,
    /// flares, fuel can) checked against the vehicle's compartments and the pack. Shown under the LOOT panel when a
    /// vehicle's storage is opened (<see cref="LootOverlay.OpenVehicle"/>).</summary>
    public static class TripKit
    {
        public enum Where { Missing, Pack, Car }

        public struct Line { public string label; public Where where; }

        /// <summary>Checklist entries: label and the item ids that satisfy it (the spare key is matched per vehicle).</summary>
        static readonly (string label, string[] ids)[] Entries =
        {
            ("EXTINGUISHER", new[] { "tool_extinguisher" }),
            ("FIRST-AID KIT", new[] { "med_firstaid" }),
            ("SPARE KEY", null),
            ("JACK", new[] { "tool_jack" }),
            ("FLARES", new[] { "ammo_flare", "tool_flare_gun" }),
            ("FUEL CAN", new[] { FluidContainers.JerryCan, FluidContainers.FuelCan }),
        };

        public static int Count => Entries.Length;

        /// <summary>Fill <paramref name="into"/> with the checklist for <paramref name="v"/>; returns how many ride in the car.</summary>
        public static int Check(VehicleDriver v, Inventory pack, List<Line> into)
        {
            into.Clear();
            var storage = v ? VehicleStorage.For(v) : null;
            string key = v && v.TryGetComponent<VehicleIgnition>(out var ign) && ign.NeedsKey ? ign.KeyItem : null;
            int inCar = 0;
            foreach (var (label, ids) in Entries)
            {
                if (ids == null && key == null) continue;                                     // keyless (bicycles, carts)
                var w = Where.Missing;
                if (InCar(storage, ids, key)) w = Where.Car;
                else if (pack != null && Has(pack, ids, key)) w = Where.Pack;
                if (w == Where.Car) inCar++;
                into.Add(new Line { label = label, where = w });
            }
            return inCar;
        }

        static bool InCar(VehicleStorage s, string[] ids, string key)
        {
            if (!s) return false;
            foreach (var c in s.compartments) if (c && c.container && Has(c.container.inventory, ids, key)) return true;
            return false;
        }

        static bool Has(Inventory inv, string[] ids, string key)
        {
            if (inv == null) return false;
            if (ids == null) return key != null && inv.GetItem(key) > 0;
            foreach (var id in ids) if (inv.GetItem(id) > 0) return true;
            return false;
        }
    }
}
