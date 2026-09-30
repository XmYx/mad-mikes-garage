namespace MadMax.Story
{
    /// <summary>Standalone side quests (S01-S24) and personal chains (P1-P3) from storyline.md: they never lead back to
    /// the conspiracy and most appear in sandbox as well. Givers are bound to fitting settlements (eligibility tags in
    /// <see cref="QuestDef.needs"/>).</summary>
    public static partial class StoryLibrary
    {
        static void Side()
        {
            QuestDef S(string id, string title, string giver, string summary, params string[] needs)
            {
                var q = Q(id, Arc.S, title, giver, summary, null, needs);
                q.storyOnly = false;
                return q;
            }
            S("S01", "A FRIDGE FULL OF FLOWERS", "orla", "Fix a flower seller's warm cold cabinet: ventilation, a failed connection, a part from an appliance wreck.", "cold_storage", "power", "salvage");
            S("S02", "THE GOAT HAS A LAWYER", "bess", "Track an escaped goat by damaged plants, lure it, mend the pen, settle who pays.", "animals", "building", "dialogue");
            S("S03", "HEARSE POWER", "sol", "Tow a seized hearse down a hill without tipping the flower trailer.", "towing", "driving");
            S("S04", "THE HOUSE THAT WALKED", "della", "Erosion under a kitchen: unload, shore the building, move a drain.", "building", "machinery");
            S("S05", "NOT THAT KIND OF SHOT", "amos", "Restore a range's targets and shoot a safe accuracy course.", "building");
            S("S06", "MUD, SWEAT AND GEARS", "jo", "Dig out a buried irrigation trench with a borrowed excavator; soil to the garden.", "machinery", "fields", "irrigation_control");
            S("S07", "AN HONEST FISH", "milt", "Catch a fair fish and expose a doctored weigh scale.", "fishing", "dialogue");
            S("S08", "THE WEDDING AT THE WRONG END OF THE ROAD", "fen", "Two villages with one name: deliver clothes and musicians, mend an outfit.", "sewing", "companions", "performance");
            S("S09", "THE SMALLEST WAR", "una", "Crows or thirst? Watch the beds, fix the water, move the scarecrow.", "garden");
            S("S10", "TWELVE VOLTS OF FAME", "pip", "Tune a tiny car for a local hill trial.", "racing", "driving");
            S("S11", "LETTERS NOBODY STOLE", "reva", "Deliver three twenty-year-old letters along one route.", "dialogue", "driving");
            S("S12", "THE DOG AT PLATFORM THREE", "etta", "A waiting dog, a paw injury and a retired owner.", "animals", "animal_treatment");
            S("S13", "ONE GOOD ROOF", "cal", "Reach a partner stranded above a collapsed stair and bring them down.", "parkour", "building");
            S("S14", "SOMETHING IN THE WELL", "oona", "Oil in the inn's water: trace the leak upstream, replace a filter.", "water_quality", "water");
            S("S15", "THE ORGAN RUNS ON DIESEL", "hollis", "Power a recital: generator, load, missing pipes.", "power", "power_control", "performance");
            S("S16", "RUST IN PEACE", "mo", "A bus to dismantle, signs of someone living in it.", "salvage", "dialogue");
            S("S17", "SOUP AGAINST THE WEATHER", "bea", "Plan and cook for stranded travellers; keep the fuel going.", "cooking");
            S("S18", "A PERFECTLY LEGAL RACE", "tamsin", "A mixed-surface loop against a bicycle courier.", "racing");
            S("S19", "THE BELL BENEATH THE WATER", "ester", "Rig a drowned village bell and lift it.", "diving", "towing");
            S("S20", "LOW CLOUDS, HIGH HOPES", "oren", "Collect ridge weather instruments by air, climb or road.", "aircraft", "parkour");
            S("S21", "THE LAST HONEST SAFE", "ruth", "Open a locksmith's own safe: records, linkage or a careful charge.", "salvage");
            S("S22", "A JACKET FOR THE END OF THE WORLD", "dax", "Finish a travelling coat: warmth or rain, then a test walk.", "sewing");
            S("S23", "NO TEETH, STILL TROUBLE", "nia", "A prizefighter bullying stallholders: testimony, a supervised bout, restitution.", "nonlethal_bout", "dialogue");
            S("S24", "THE BIRTHDAY MACHINE", "gus", "Gears, lights and a horn for a wobbling birthday sculpture.", "salvage", "building");
            // personal chains
            QuestDef P(string id, string title, string giver, string summary, string after, params string[] needs)
            {
                var q = Q(id, Arc.P, title, giver, summary, after != null ? new[] { after } : null, needs);
                q.storyOnly = id.StartsWith("P1");
                return q;
            }
            P("P1.1", "A SECOND PAIR OF HANDS", "nell", "Catalogue the parts of Nell's unfinished car.", "B1", "salvage");
            P("P1.2", "THE WRONG ORIGINAL", "nell", "A collector owns the original engine.", "P1.1", "trade", "dialogue");
            P("P1.3", "ONE MORE SUNDAY", "nell", "Nell drives; you ride along to a viewpoint.", "P1.2", "companions", "driving");
            P("P2.1", "THE MARKET NOBODY WANTED", "hamlets", "Survey water, access and flat ground for a market.", null, "building");
            P("P2.2", "A THIRD FIELD", "hamlets", "Pick a site or a rotation; build stalls and sanitation.", "P2.1", "building", "water");
            P("P2.3", "OPENING DAY", "hamlets", "Mediate the first-day price and livestock dispute.", "P2.2", "dialogue", "animals");
            P("P2.4", "ONE SUPPLY RUN", "hamlets", "Run the first supply trip; attend the opening evening.", "P2.3", "trade", "driving");
            P("P3.1", "THE SEA DOES NOT KEEP RECEIPTS", "trawler", "Repair a trawler's hold before hunting a sea monster.", null, "boats", "salvage");
            P("P3.2", "SHALLOW OR DEEP", "trawler", "Survey wrecks for a careful salvage dive.", "P3.1", "diving");
            P("P3.3", "WHO OWNS A WRECK", "trawler", "Settle salvage rights with the owners' descendants.", "P3.2", "dialogue");
        }
    }
}
