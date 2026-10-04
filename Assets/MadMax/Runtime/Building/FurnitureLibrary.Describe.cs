using System.Collections.Generic;
using MadMax.Items;

namespace MadMax.Building
{
    public static partial class FurnitureLibrary
    {
        /// <summary>What a piece is for, in a line or three (build menu panel): its own <see cref="FurnitureDef.desc"/>,
        /// else read from what it is — a crafting station (and how much it makes), storage, light, power, water, a
        /// seat, a bed, a defence — and how it builds (foundation, drivable deck, fits a doorway, upgrades, toughness).</summary>
        public static List<string> Describe(FurnitureDef d)
        {
            var l = new List<string>();
            if (!string.IsNullOrEmpty(d.desc)) l.Add(d.desc);
            if (d.plan == -2) { l.Add("CAPTURE TOOL: SAVES THE STRUCTURE YOU AIM AT AS A PLAN TO BUILD AGAIN."); return l; }
            if (d.plan >= 0) { l.Add("A SAVED PLAN: PLACES THE WHOLE STRUCTURE AT ONCE, PAID PIECE BY PIECE."); return l; }
            if (d.link != UtilityKind.None) { l.Add("RUNS A " + d.link.ToString().ToUpperInvariant() + " LINE: CLICK ONE POINT, THEN THE OTHER."); return l; }
            int makes = 0;
            foreach (var r in RecipeLibrary.All) if (r.station == d.id) makes++;
            if (makes > 0) l.Add("A WORK STATION: " + makes + " THING" + (makes == 1 ? "" : "S") + " TO MAKE HERE ([E] TO USE).");
            if (string.IsNullOrEmpty(d.desc) && makes == 0)
            {
                string k = d.id;
                string role =
                    Has(k, "bed") && !Has(k, "field_bed", "garden", "flower") ? "SLEEP HERE. YOUR OWN BED IS WHERE YOU WAKE UP." :
                    Has(k, "chest", "crate", "locker", "cabinet", "shelf", "wardrobe", "cupboard", "box", "silo", "rack") ? "STORAGE: KEEPS THINGS, LOCKABLE BY ITS BUILDER." :
                    Has(k, "fridge", "cold", "cellar") ? "COLD STORAGE: FOOD KEEPS LONGER." :
                    Has(k, "door", "gate", "shutter", "hatch") ? "OPENS AND SHUTS; ITS BUILDER CAN LOCK IT." :
                    Has(k, "window") ? "A WALL WITH A WINDOW: LIGHT IN, A VIEW OUT." :
                    Has(k, "wall", "fence", "palisade") ? "A WALL SECTION: KEEPS WIND, BEASTS AND RAIDERS OUT." :
                    Has(k, "roof", "awning", "canopy") ? "OVERHEAD COVER: KEEPS RAIN AND SUN OFF." :
                    Has(k, "floor", "platform", "deck", "slab") ? "A FLOOR TO BUILD ON." :
                    Has(k, "stair", "ladder", "ramp") ? "GETS YOU UP AND DOWN." :
                    Has(k, "lamp", "light", "lantern", "candle", "torch") ? "LIGHT FOR THE DARK HOURS." :
                    Has(k, "generator", "solar", "turbine", "windmill", "wheel_water", "water_wheel") ? "MAKES POWER FOR WHAT IS CABLED TO IT." :
                    Has(k, "battery") ? "STORES POWER FOR LATER." :
                    Has(k, "switch", "breaker", "timer") ? "CONTROLS A POWER OR WATER LINE." :
                    Has(k, "tank", "barrel", "cistern", "well", "pump", "collector") ? "WATER: COLLECTS, HOLDS OR DRAWS IT." :
                    Has(k, "sprinkler", "drip", "irrigat") ? "WATERS THE BEDS AROUND IT." :
                    Has(k, "planter", "plot", "garden", "pot", "greenhouse") ? "GROWS PLANTS: SOW SEEDS, KEEP IT WATERED." :
                    Has(k, "trough", "coop", "pen", "stable", "hive", "kennel") ? "FOR ANIMALS: FEED, SHELTER OR PRODUCE." :
                    Has(k, "heater", "stove", "fireplace", "fire", "brazier") ? "WARMTH: HEATS THE AIR AROUND IT." :
                    Has(k, "air_con", "aircon", "cooler", "fan") ? "COOLS THE AIR AROUND IT." :
                    Has(k, "turret", "gun_nest", "spike", "wire", "mine", "tripwire", "bell", "watchtower", "trap") ? "DEFENCE: HURTS, SLOWS OR WARNS OF INTRUDERS." :
                    Has(k, "chair", "bench", "sofa", "stool", "seat", "couch", "hammock") ? "A SEAT: REST YOUR LEGS." :
                    Has(k, "table") ? "A TABLE: A MEAL EATEN AT ONE LEAVES YOU WELL FED." :
                    Has(k, "radio") ? "PLAYS THE STATIONS." :
                    Has(k, "tv", "television") ? "WATCH TAPES: SOME TEACH YOU THINGS." :
                    Has(k, "bookshelf", "bookcase") ? "HOLDS BOOKS: READING GOES FASTER BESIDE IT." :
                    Has(k, "mirror") ? "CHANGE YOUR HAIR AND BEARD HERE." :
                    Has(k, "latrine", "toilet", "outhouse") ? "RELIEVE YOURSELF; THE WASTE BECOMES FERTILISER." :
                    Has(k, "shower", "bath", "wash", "sink") ? "KEEP CLEAN: LESS SICKNESS." :
                    Has(k, "flag", "claim") ? "CLAIMS THE GROUND AROUND IT AS YOURS." :
                    Has(k, "board", "sign") ? "A NOTICE BOARD OR SIGN." :
                    d.category == BuildCategory.Decor ? "DECORATION: A HOME FEELS MORE LIKE ONE." :
                    d.category == BuildCategory.Furniture ? "FURNITURE: MAKES A PLACE COMFORTABLE." : null;
                if (role != null) l.Add(role);
            }
            if (d.foundation) l.Add("STANDS LEVEL ON ROUGH GROUND AND TILES WITH ITS NEIGHBOURS.");
            if (d.deck != UnityEngine.Vector4.zero) l.Add("SOLID ENOUGH TO DRIVE ON.");
            if (!string.IsNullOrEmpty(d.snapTo)) l.Add("FITS INTO A " + d.snapTo.Replace('_', ' ').Trim().ToUpperInvariant() + ".");
            var up = Get(d.upgrade);
            if (up != null) l.Add("CAN BE UPGRADED TO " + up.name + " LATER.");
            l.Add(d.hits >= 8 ? "VERY TOUGH." : d.hits >= 5 ? "STURDY." : d.hits <= 2 ? "FLIMSY." : "TAKES A FEW HITS.");
            return l;
        }

        static bool Has(string id, params string[] keys) { foreach (var k in keys) if (id.Contains(k)) return true; return false; }
    }
}
