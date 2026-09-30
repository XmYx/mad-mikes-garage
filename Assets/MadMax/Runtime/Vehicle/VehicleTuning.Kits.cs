using System.Globalization;
using UnityEngine;

namespace MadMax.Vehicles
{
    /// <summary>Drivetrain and chassis kits (depth stage D), made at the machine shop or the forge and fitted at a tuning
    /// bench: gearbox (close ratio: gaps closed down from first, every gear pulls harder, tops out lower; wide ratio: a
    /// crawler first and an overdrive top), transfer case (selectable 4WD on a two-wheel-drive vehicle), heavy-duty
    /// brakes, one suspension kit (lift, lowered, heavy duty) and a long-range tank. Applied on top of the tuning sliders
    /// from the captured design values, so <see cref="Apply"/> stays repeatable; saved in the tuning state.</summary>
    public partial class VehicleTuning
    {
        public enum Slot { Gearbox, Transfer, Brakes, Suspension, Tank }

        /// <summary>A kit slot on the tuning page: the kit item per option (index 0 = stock, no item).</summary>
        public class KitSlot
        {
            public Slot slot;
            public string label, hint;
            public int mech;                      // Mechanics level needed to fit
            public string[] kits, names;
        }

        public static readonly KitSlot[] KitSlots =
        {
            new KitSlot { slot = Slot.Gearbox, label = "GEARBOX", mech = 3, kits = new[] { null, "kit_gearbox_close", "kit_gearbox_wide" }, names = new[] { "STOCK", "CLOSE-RATIO", "WIDE-RATIO" },
                hint = "E FIT THE NEXT GEARBOX KIT IN THE PACK (THE OLD ONE COMES BACK).  CLOSE: EVERY GEAR PULLS HARDER, LOWER TOP.  WIDE: CRAWLER FIRST, OVERDRIVE TOP" },
            new KitSlot { slot = Slot.Transfer, label = "TRANSFER CASE", mech = 4, kits = new[] { null, "kit_transfer_case" }, names = new[] { "NONE", "4X4 SELECTABLE" },
                hint = "E FIT: A TWO-WHEEL-DRIVE VEHICLE GETS SELECTABLE 4WD" },
            new KitSlot { slot = Slot.Brakes, label = "BRAKE KIT", mech = 2, kits = new[] { null, "kit_brakes_hd" }, names = new[] { "STOCK", "HEAVY DUTY" },
                hint = "E FIT: +35% STOPPING, ON TOP OF THE DISC UPGRADES" },
            new KitSlot { slot = Slot.Suspension, label = "SUSPENSION KIT", mech = 2, kits = new[] { null, "kit_lift", "kit_lowering", "kit_suspension_hd" }, names = new[] { "STOCK", "LIFTED", "LOWERED", "HEAVY DUTY" },
                hint = "E FIT THE NEXT KIT.  LIFT: +10 CM, LONG TRAVEL.  LOWERED: -5 CM, STIFF.  HEAVY DUTY: LEAF PACKS FOR LOADS" },
            new KitSlot { slot = Slot.Tank, label = "FUEL TANK", mech = 1, kits = new[] { null, "kit_long_range_tank" }, names = new[] { "STOCK", "LONG RANGE" },
                hint = "E FIT: HALF AS MUCH FUEL AGAIN (AT LEAST 30 L MORE)" },
        };

        public static KitSlot SlotFor(string kit)
        {
            foreach (var s in KitSlots) if (System.Array.IndexOf(s.kits, kit) > 0) return s;
            return null;
        }

        public int gearbox;                  // 0 stock, 1 close ratio, 2 wide ratio
        public bool transferCase;
        public bool hdBrakes;
        public int suspension;               // 0 stock, 1 lift kit, 2 lowered, 3 heavy duty
        public bool longRange;

        VehicleSystems systems;
        float baseTravel, baseFuelCap;
        bool baseAwd, kitsCaptured;

        bool KitsStock => gearbox == 0 && !transferCase && !hdBrakes && suspension == 0 && !longRange;

        public int Fitted(Slot s) => s switch
        {
            Slot.Gearbox => gearbox, Slot.Transfer => transferCase ? 1 : 0, Slot.Brakes => hdBrakes ? 1 : 0,
            Slot.Suspension => suspension, _ => longRange ? 1 : 0
        };

        public void SetFitted(Slot s, int v)
        {
            switch (s)
            {
                case Slot.Gearbox: gearbox = v; break;
                case Slot.Transfer: transferCase = v > 0; break;
                case Slot.Brakes: hdBrakes = v > 0; break;
                case Slot.Suspension: suspension = v; break;
                default: longRange = v > 0; break;
            }
        }

        /// <summary>Why a kit option can't go on this vehicle (null = it fits).</summary>
        public string CannotFit(Slot s, int option)
        {
            if (!driver) return "NO VEHICLE";
            CaptureKits();
            bool special = driver.aircraft || GetComponent<BikeBalance>() || GetComponent<BoatModel>();
            switch (s)
            {
                case Slot.Gearbox: return special || driver.gears.Length < 2 ? "NO GEARBOX TO SWAP ON THIS ONE" : null;
                case Slot.Transfer:
                    if (special || driver.Tracked) return "NOTHING TO DRIVE ON THIS ONE";
                    return baseAwd || driver.drive == VehicleDriver.Drive.All ? "ALREADY FOUR-WHEEL DRIVE" : null;
                case Slot.Suspension: return special || driver.Tracked ? "NO SPRINGS TO SWAP ON THIS ONE" : null;
                case Slot.Tank: return !systems || systems.fuelCapacity <= 0f ? "NO FUEL TANK" : null;
            }
            return null;
        }

        /// <summary>Fit a kit by item id without paying for it (tests, dev); false when it doesn't go on this vehicle.</summary>
        public bool FitKit(string kit)
        {
            var s = SlotFor(kit);
            if (s == null) return false;
            int option = System.Array.IndexOf(s.kits, kit);
            if (CannotFit(s.slot, option) != null) return false;
            SetFitted(s.slot, option);
            Apply();
            return true;
        }

        void CaptureKits()
        {
            if (kitsCaptured || !driver) return;
            kitsCaptured = true;
            systems = GetComponent<VehicleSystems>();
            baseTravel = driver.travel; baseAwd = driver.awdSelectable; baseFuelCap = systems ? systems.fuelCapacity : 0f;
        }

        /// <summary>Called by <see cref="Apply"/> after the sliders: layers the kits over the tuned values.</summary>
        void ApplyKits()
        {
            CaptureKits();
            int n = driver.gears.Length;
            if (gearbox != 0 && n > 1 && driver.gears[0] > 0.01f && driver.gears[n - 1] > 0.01f)
            {
                // ratios re-spread on a log scale. Close: anchored on first, gaps squeezed (k 0.9), so every gear pulls at
                // least as hard and top ends ~13 % shorter. Wide: around the geometric middle, gaps opened (k 1.2): a
                // crawler first ~13 % lower, an overdrive top ~12 % taller
                float anchor = gearbox == 1 ? driver.gears[0] : Mathf.Sqrt(driver.gears[0] * driver.gears[n - 1]);
                float k = gearbox == 1 ? 0.9f : 1.2f;
                for (int i = 0; i < n; i++) driver.gears[i] = anchor * Mathf.Pow(driver.gears[i] / anchor, k);
            }
            driver.awdSelectable = baseAwd || transferCase;
            if (hdBrakes) driver.brakeForce *= 1.35f;
            float travel = 1f;
            switch (suspension)
            {
                case 1: driver.rideHeight += 0.1f; travel = 1.25f; driver.frequency *= 0.92f; break;                 // lift kit: long-travel coils
                case 2: driver.rideHeight -= 0.05f; travel = 0.75f; driver.frequency *= 1.25f; driver.damping *= 1.1f; break;   // lowered
                case 3: travel = 1.1f; driver.frequency *= 1.3f; driver.damping *= 1.15f; break;                   // heavy duty leaf packs
            }
            driver.damping = Mathf.Clamp(driver.damping, 0.1f, 1.5f);
            driver.travel = baseTravel * travel;
            if (systems && baseFuelCap > 0f) systems.fuelCapacity = longRange ? baseFuelCap + Mathf.Max(30f, baseFuelCap * 0.5f) : baseFuelCap;
        }

        /// <summary>Kit fields appended to <see cref="SaveState"/> (indices 14..18).</summary>
        string KitsState()
        {
            var ci = CultureInfo.InvariantCulture;
            return ";" + gearbox.ToString(ci) + ";" + (transferCase ? "1" : "0") + ";" + (hdBrakes ? "1" : "0") + ";" + suspension.ToString(ci) + ";" + (longRange ? "1" : "0");
        }

        void LoadKits(string[] p)
        {
            int I(int i) => i < p.Length && int.TryParse(p[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
            gearbox = Mathf.Clamp(I(14), 0, 2); transferCase = I(15) > 0; hdBrakes = I(16) > 0; suspension = Mathf.Clamp(I(17), 0, 3); longRange = I(18) > 0;
        }
    }
}
