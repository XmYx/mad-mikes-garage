using System.Collections.Generic;
using MadMax.Building;
using MadMax.Game;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Story
{
    /// <summary>Residents who staff a service at the home (system key <c>residents</c>, storyline B4): people who live
    /// at the garage, eat from its stores every evening and, fed, work one service by day — REPAIR LABOUR (they mend the
    /// fleet parked in the yard, a scrap of material every few hours), TENDING CROPS (water, weed and harvest the beds
    /// into the pantry) or MEDICAL RESUPPLY (roll bandages from cloth into the medicine cabinet). Nothing comes free: no
    /// food, no work (and the journal says so); no materials, no repairs or bandages. State lives in story flags
    /// ("residents", "residents:service:*", "residents:supper:{day}", "residents:short:{day}"), so it saves with the
    /// campaign.</summary>
    public static class Residents
    {
        public enum Service { None, Repair, Crops, Medical }

        /// <summary>Cast keys of the residents (see B4): Hester runs the house, Judd works with his hands.</summary>
        public static readonly string[] Keys = { "b4_hester", "b4_judd" };
        public const float HomeRadius = 24f;
        public const int WorkFrom = 8, WorkTo = 18, SupperAt = 19;

        static float lastHour = -1f, worked;
        static WastelandGame game;
        static readonly List<string> scratch = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { lastHour = -1f; worked = 0f; game = null; }

        /// <summary>People live at the garage (B4 done).</summary>
        public static bool Living => Story.Flag("residents");

        public static Service Current =>
            Story.Flag("residents:service:repair") ? Service.Repair : Story.Flag("residents:service:crops") ? Service.Crops :
            Story.Flag("residents:service:medical") ? Service.Medical : Service.None;

        public static string Name(Service s) => s switch { Service.Repair => "REPAIR LABOUR", Service.Crops => "TENDING CROPS", Service.Medical => "MEDICAL RESUPPLY", _ => "NONE" };

        /// <summary>How many sit down to eat (Judd lodges in town under one of the rules, but still eats here by day).</summary>
        public static int Mouths => Keys.Length;

        /// <summary>Last night's supper was short: nobody works today.</summary>
        public static bool Hungry => HungryOn(DayNight.Day);
        static bool HungryOn(int day) => Story.Flag("residents:short:" + (day - 1));

        /// <summary>Who does the chosen service: Judd repairs, Hester tends the beds and the cabinet.</summary>
        public static string Worker(Service s) => s == Service.Repair ? "b4_judd" : s == Service.None ? null : "b4_hester";

        /// <summary>Within working hours and fed (a hungry day stops the work).</summary>
        public static bool Working => WorkingAt(DayNight.Day, DayNight.Hours);
        static bool WorkingAt(int day, float h) => Living && Current != Service.None && !HungryOn(day) && h >= WorkFrom && h < WorkTo;

        static Vector3 Home => StoryAnchors.Get("garage");
        static bool AtHome(Vector3 p) { var h = Home; return new Vector2(p.x - h.x, p.z - h.z).sqrMagnitude < HomeRadius * HomeRadius; }

        /// <summary>The garage's own stores: built containers on the ground within reach of the garage.</summary>
        public static void Stores(List<Container> into)
        {
            into.Clear();
            foreach (var c in Container.All)
                if (c && c.GetComponent<Placeable>() && !c.GetComponentInParent<Rigidbody>() && AtHome(c.transform.position)) into.Add(c);
        }

        static readonly List<Container> stores = new List<Container>();

        /// <summary>Portions of food in the garage's stores.</summary>
        public static int Portions()
        {
            Stores(stores);
            int n = 0;
            foreach (var c in stores) foreach (var kv in c.inventory.Items) if (kv.Value > 0 && IsMeal(kv.Key)) n += kv.Value;
            return n;
        }

        static bool IsMeal(string id)
        {
            if (id == "food_rotten") return false;
            var f = FoodLibrary.Get(id);
            return f != null && f.hunger > 0f;
        }

        static int TakeFood(int n)
        {
            Stores(stores);
            int took = 0;
            foreach (var c in stores)
            {
                scratch.Clear();
                foreach (var kv in c.inventory.Items) if (kv.Value > 0 && IsMeal(kv.Key)) scratch.Add(kv.Key);
                foreach (var id in scratch)
                    while (took < n && c.inventory.TakeItem(id)) took++;
                if (took >= n) break;
            }
            return took;
        }

        static bool TakeResource(ResourceType t, int n)
        {
            Stores(stores);
            foreach (var c in stores) if (c.inventory.Get(t) >= n && c.inventory.TrySpend(t, n)) return true;
            return false;
        }

        /// <summary>Once a game hour: supper at 19:00, the service by day. Called every frame by the story's game side.</summary>
        public static void Tick(WastelandGame g)
        {
            if (!g || !Living || !StoryAnchors.Has("garage")) { lastHour = -1f; return; }
            if (game != g) { game = g; lastHour = -1f; }
            float hour = Mathf.Floor(DayNight.TotalDays * 24f);
            if (lastHour < 0f || hour < lastHour) { lastHour = hour; return; }
            if (hour <= lastHour) return;
            // every hour that passed (sleeping through the evening still serves supper), at most a day's worth
            for (float h = Mathf.Max(lastHour + 1f, hour - 23f); h <= hour; h += 1f)
            {
                int day = Mathf.FloorToInt(h / 24f);
                Hour(g, day, h - day * 24f);
            }
            lastHour = hour;
        }

        static void Hour(WastelandGame g, int day, float h)
        {
            if (h >= SupperAt && !Story.Flag("residents:supper:" + day)) Supper(g, day);
            if (!WorkingAt(day, h)) return;
            worked += 1f;
            switch (Current)
            {
                case Service.Repair: Repair(g); break;
                case Service.Crops: Crops(g); break;
                case Service.Medical: Medical(g); break;
            }
        }

        /// <summary>Everyone eats one portion from the garage's stores; short, and tomorrow nobody works.</summary>
        public static void Supper(WastelandGame g, int day)
        {
            Story.SetFlag("residents:supper:" + day);
            int got = TakeFood(Mouths);
            if (got >= Mouths)
            {
                Story.Note("residents:supper");
                if (Near(g)) g.Toast("SUPPER AT THE GARAGE: THE RESIDENTS ATE " + got + " PORTIONS FROM THE STORES");
                return;
            }
            Story.SetFlag("residents:short:" + day);
            Journal.Add("HOME", "THE RESIDENTS WENT HUNGRY (" + got + "/" + Mouths + " PORTIONS). NO WORK TOMORROW: PUT FOOD IN A CHEST OR FRIDGE AT THE GARAGE");
            if (Near(g)) g.Toast("NOT ENOUGH FOOD IN THE GARAGE'S STORES: THE RESIDENTS GO HUNGRY");
        }

        static bool Near(WastelandGame g)
        {
            var me = g.Current ? g.Current.transform.position : g.Player ? g.Player.transform.position : Vector3.zero;
            return AtHome(me) || Vector3.Distance(me, Home) < 80f;
        }

        static void Repair(WastelandGame g)
        {
            int mended = 0;
            foreach (var v in g.Fleet)
            {
                if (!v || v == g.Current || v.Occupied || !AtHome(v.transform.position) || (v.Body && v.Body.linearVelocity.sqrMagnitude > 0.25f)) continue;
                var chassis = v.GetComponent<MadMax.Vehicles.VehicleChassis>();
                bool worn = false;
                if (chassis) foreach (var p in chassis.Parts) if (p && p.damage > 0.01f) worn = true;
                var dmg = v.GetComponent<MadMax.Vehicles.VehicleDamage>();
                if (dmg && dmg.FrameDamage > 0.01f) worn = true;
                if (!worn) continue;
                if (worked >= 4f)
                {
                    if (!TakeResource(ResourceType.Scrap, 1)) { Journal.Add("HOME", "JUDD'S OUT OF SCRAP FOR REPAIRS: LEAVE SOME IN A CHEST AT THE GARAGE"); return; }
                    worked = 0f;
                }
                if (chassis) foreach (var p in chassis.Parts) if (p) p.damage = Mathf.Max(0f, p.damage - 0.04f);
                if (dmg && dmg.FrameDamage > 0f) dmg.StraightenFrame(0.02f);
                mended++;
            }
            if (mended > 0) Story.Note("residents:worked");
        }

        static void Crops(WastelandGame g)
        {
            Stores(stores);
            var pantry = stores.Count > 0 ? stores[0].inventory : null;
            int tended = 0;
            foreach (var plot in Object.FindObjectsByType<GardenPlot>(FindObjectsSortMode.None))
            {
                if (!plot || !AtHome(plot.transform.position) || plot.crop == null) continue;
                bool touched = false;
                if (plot.water < 0.8f) { plot.water = 0.9f; touched = true; }
                if (plot.weeds > 0.05f) { plot.weeds = Mathf.Max(0f, plot.weeds - 0.3f); touched = true; }
                if (plot.Ripe && pantry != null && plot.Reap(g, 1f, pantry)) touched = true;
                if (!touched) continue;
                plot.GetComponent<Placeable>()?.Dirty();
                tended++;
            }
            if (tended > 0) Story.Note("residents:worked");
        }

        static void Medical(WastelandGame g)
        {
            if (worked < 6f) return;
            if (!TakeResource(ResourceType.Cloth, 1)) { Journal.Add("HOME", "HESTER HAS NO CLOTH FOR BANDAGES: LEAVE SOME IN A CHEST AT THE GARAGE"); return; }
            worked = 0f;
            Stores(stores);
            Container cab = null;
            foreach (var c in stores) if (c.GetComponent<Placeable>().id == MedSupply.CabinetId) { cab = c; break; }
            if (!cab && stores.Count > 0) cab = stores[0];
            if (!cab) return;
            cab.inventory.AddItem("med_bandage");
            if (Random.value < 0.34f && TakeResource(ResourceType.Ethanol, 1)) cab.inventory.AddItem("med_disinfectant");
            cab.GetComponent<Placeable>()?.Dirty();
            Story.Note("residents:worked");
        }
    }
}
