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

        // ------------------------------------------------------------------ emergencies on the road

        /// <summary>Emergencies the kit answers.</summary>
        public enum Need { Fire, Flat, Bleeding }

        static string[] Remedy(Need n) => n switch
        {
            Need.Fire => new[] { "tool_extinguisher" },
            Need.Flat => new[] { "tool_jack" },
            _ => new[] { "med_firstaid", "med_bandage" },
        };

        static string RemedyName(Need n) => n == Need.Fire ? "EXTINGUISHER" : n == Need.Flat ? "JACK" : "DRESSINGS";

        /// <summary>Where the remedy for <paramref name="need"/> is: in the pack first (to hand), then the compartment of
        /// <paramref name="v"/> that holds it (<paramref name="box"/> = its title), else missing.</summary>
        public static Where Locate(Need need, VehicleDriver v, Inventory pack, out string box)
        {
            box = null;
            var ids = Remedy(need);
            if (pack != null && Has(pack, ids, null)) return Where.Pack;
            var s = v ? VehicleStorage.For(v) : null;
            if (s) foreach (var c in s.compartments)
                    if (c && c.container && Has(c.container.inventory, ids, null)) { box = c.container.title; return Where.Car; }
            return Where.Missing;
        }

        /// <summary>The line that names where the remedy is ("EXTINGUISHER IN THE TRUNK - [L] LOOT") or that it
        /// didn't come along.</summary>
        public static string Prompt(Need need, VehicleDriver v, Inventory pack)
        {
            var w = Locate(need, v, pack, out string box);
            string what = RemedyName(need);
            if (w == Where.Pack) return what + " IN YOUR PACK" + (need == Need.Fire ? " - GET OUT AND SPRAY THE ENGINE" : need == Need.Flat ? " - SWAP THE WHEEL" : " - [" + Controls.Name(Controls.Act.Health) + "] HEALTH");
            if (w == Where.Car) return what + (box.StartsWith("BEHIND") ? " " : " IN THE ") + box + " - [" + Controls.Name(Controls.Act.Loot) + "] OPEN IT";
            return need == Need.Fire ? "NO EXTINGUISHER ABOARD - GET CLEAR OF THE CAR"
                 : need == Need.Flat ? "NO JACK ABOARD - LIMP ON THE RIM OR WALK"
                 : "NOTHING TO DRESS IT WITH ABOARD";
        }

        /// <summary>Last emergency line shown (tests).</summary>
        public static string LastAlarm;
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAlarm() => LastAlarm = null;

        /// <summary>An emergency started on or by <paramref name="v"/>: the toast names the remedy's place, prefixed by
        /// <paramref name="what"/> ("ENGINE FIRE!").</summary>
        public static void Alarm(WastelandGame g, Need need, VehicleDriver v, string what)
        {
            if (!g) return;
            LastAlarm = what + " " + Prompt(need, v, g.Inventory);
            g.Toast(LastAlarm);
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
