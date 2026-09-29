using System.Collections.Generic;

namespace MadMax.Npc
{
    public enum NpcRole { Wanderer, Resident, Shopkeeper, Stallkeeper, Trader, Raider, RaiderBoss, Leader, Packer }
    public enum Temper { Friendly, Gruff, Nervous, Greedy, Pious, Joker, Proud }
    public enum Tone { Polite, Blunt, Joke, Flatter, Threat }

    /// <summary>The wasteland's people as text: names, factions, vendor trades, backstory fragments and the lines
    /// each temperament says when greeted, flattered, joked with or threatened. Pure data, picked deterministically
    /// by <see cref="NpcProfile"/>.</summary>
    public static class NpcLore
    {
        public static readonly string[] First =
        {
            "MARLA", "DUKE", "TESS", "BARNABY", "ROSIE", "JUNO", "KOVAC", "PEARL", "AMOS", "NINA", "OTIS", "DELIA", "RUFUS", "IMKE",
            "SILAS", "VERA", "HOLT", "MAGDA", "EZRA", "LOTTE", "GIDEON", "SUNNY", "BOYD", "ZELDA", "ARLO", "MINA", "CASPER", "ODETTE",
            "LEVI", "GRETA", "TOBIAS", "IRMA", "FINN", "BEA", "MILO", "HESTER", "JONAH", "FREYA", "WALT", "NELL"
        };
        static readonly HashSet<string> feminine = new HashSet<string> { "MARLA", "TESS", "ROSIE", "JUNO", "PEARL", "NINA", "DELIA", "IMKE", "VERA", "MAGDA", "LOTTE", "SUNNY", "ZELDA", "MINA", "ODETTE", "GRETA", "IRMA", "BEA", "HESTER", "FREYA", "NELL" };
        /// <summary>First names read as women's (voice choice, no beard).</summary>
        public static bool Feminine(string first) => first != null && feminine.Contains(first);

        public static readonly string[] Nick =
        {
            "SPROCKET", "TWO-STROKE", "RUSTY", "GASKET", "MAMA DIESEL", "BOLT", "THE DEACON", "CHROME", "SALT", "PISTON", "GRIT",
            "HUBCAP", "SPARKY", "OLD MAN", "DUSTY", "NITRO", "SIX-GUN", "WHISPER", "TORQUE", "SKIDMARK", "PATCH", "CANDLE", "TIN"
        };
        public static readonly string[] RaiderNick =
        {
            "TOECUTTER", "SKULLGRIN", "RAZORJAW", "BLOODWHEEL", "SCAB", "HOOK", "GUTS", "RATCHET", "BONESAW", "CINDER", "WRECKER", "FANG"
        };
        public static readonly string[] Gangs = { "CHROME JACKALS", "RUSTMEN", "BONE CONVOY", "ASH RIDERS" };

        /// <summary>Vendor trades: id, stall / shop title, what they call themselves.</summary>
        public static readonly (string id, string title, string trade)[] Trades =
        {
            ("fuel", "GUZZOLINE", "FUEL TRADER"), ("parts", "GEARMONGER", "PARTS DEALER"), ("scrap", "JUNK BARON", "SCRAP DEALER"),
            ("food", "CHOW WAGON", "COOK"), ("salvage", "SALVAGE BROKER", "SALVAGER"), ("build", "BUILDER'S YARD", "YARD BOSS"),
            ("pack", "PACK MULE GOODS", "PACK TRADER")
        };

        public static string TradeTitle(string kind) { foreach (var t in Trades) if (t.id == kind) return t.title; return "TRADER"; }
        public static string TradeName(string kind) { foreach (var t in Trades) if (t.id == kind) return t.trade; return "TRADER"; }

        // ------------------------------------------------------------------ backstory: origin / drive / secret

        public static readonly string[] Origin =
        {
            "I DROVE A SCHOOL BUS BEFORE THE FALL. KIDS STILL WAVE AT ME IN MY DREAMS.",
            "GREW UP ON THE SALT FLATS. MY FOLKS MINED SALT AND TRADED IT FOR WATER.",
            "I RAN A NOODLE STAND IN THE CITY. THE CITY RAN OUT OF NOODLES FIRST, THEN PEOPLE.",
            "I WAS A MECHANIC FOR THE FUEL GUILD. SIX YEARS UNDER THEIR TRUCKS.",
            "BORN IN A BUNKER. SAW THE SKY FOR THE FIRST TIME AT FOURTEEN.",
            "MY FAMILY FARMED CORN OUT EAST UNTIL THE DUST TOOK THE FIELDS.",
            "I WAS A NURSE. STILL AM, WHEN SOMEONE NEEDS STITCHES.",
            "I USED TO SING IN A BAND. NOW I SING TO MY ENGINE.",
            "I ESCAPED A WORK CAMP. DUG OUT WITH A SPOON, IF YOU CAN BELIEVE IT.",
            "RADIO OPERATOR ON THE OLD RELAY TOWERS. I KNOW EVERY STATION BY ITS HISS.",
            "I HAULED WATER BETWEEN THE TOWNS FOR TEN YEARS. NEVER SPILLED A DROP.",
            "I WAS A PREACHER'S KID. THE CHURCH OF THE LAST ENGINE TOOK ME IN AFTER."
        };
        public static readonly string[] Drive =
        {
            "I'M LOOKING FOR MY SISTER. SHE WENT NORTH WITH A CARAVAN AND NEVER WROTE.",
            "SAVING UP FOR A V8. A REAL ONE. BLOWER AND ALL.",
            "I WANT TO SEE THE SEA. SOMEONE SAID IT'S STILL BLUE.",
            "THE RUSTMEN TOOK MY TRUCK. ONE DAY I'LL TAKE IT BACK.",
            "I'M BUILDING A HOUSE. FOUR WALLS, A ROOF, A GARDEN. THAT'S ALL.",
            "I OWE THE FUEL GUILD MORE THAN I'LL EVER EARN. SO I KEEP MOVING.",
            "I JUST WANT TO MAKE IT THROUGH THE WINTER.",
            "I'M WRITING DOWN EVERYONE I MEET. SOMEONE SHOULD REMEMBER.",
            "MY KID IS SICK. MEDICINE COSTS MORE THAN FUEL NOW.",
            "I'M MAPPING THE OLD BUNKERS. THEY HAVE THINGS PEOPLE FORGOT."
        };
        public static readonly string[] Secret =
        {
            "I BURIED TWO JERRY CANS BY A DEAD TREE, THREE HILLS WEST. DON'T TELL ANYONE.",
            "I RODE WITH RAIDERS ONCE. ONE SEASON. I STILL HEAR THE ENGINES.",
            "I'M NOT A REAL MEDIC. I READ HALF A BOOK. PEOPLE LIVED, MOSTLY.",
            "THE FUEL GUILD WATERS THEIR GAS. I'VE SEEN THE BARRELS.",
            "I STOLE THIS COAT FROM A DEAD MAN. HE DIDN'T COMPLAIN.",
            "MY REAL NAME IS SOMETHING ELSE. THE OLD ONE HAS A BOUNTY ON IT.",
            "I KNOW A BUNKER DOOR THAT STILL LOCKS. I SLEEP THERE WHEN IT STORMS.",
            "I'VE NEVER DRIVEN A CAR. I'M TERRIFIED OF THEM."
        };
        public static readonly string[] RaiderStory =
        {
            "THE ROAD BELONGS TO THE ONES WHO CAN HOLD IT.",
            "I WAS A FARMER. THEN THE FARM WAS DUST. NOW I TAKE.",
            "THE BOSS FEEDS US. THE BOSS GIVES US FUEL. THAT'S LOYALTY.",
            "EVERY WRECK OUT THERE WAS SOMEONE WHO WOULDN'T PAY THE TOLL."
        };

        // ------------------------------------------------------------------ lines

        public static string Greeting(Temper t, bool met, int disposition)
        {
            if (disposition < -40) return t == Temper.Nervous ? "STAY BACK. I MEAN IT." : "YOU AGAIN. MAKE IT QUICK.";
            if (met && disposition > 40) return t switch { Temper.Gruff => "HM. YOU'RE ALRIGHT.", Temper.Joker => "MY FAVOURITE CUSTOMER! OR VICTIM.", Temper.Pious => "THE ENGINE KEEPS YOU TURNING, FRIEND.", _ => "GOOD TO SEE YOU AGAIN!" };
            if (met) return t switch { Temper.Gruff => "WHAT.", Temper.Nervous => "OH. IT'S YOU.", Temper.Greedy => "BACK TO SPEND?", Temper.Joker => "WELL IF IT ISN'T THE DUST MAGNET.", Temper.Proud => "YOU REMEMBER ME. GOOD.", Temper.Pious => "BLESSED BE YOUR PISTONS.", _ => "HEY, IT'S YOU." };
            return t switch
            {
                Temper.Friendly => "HELLO THERE, STRANGER. ROUGH ROAD?",
                Temper.Gruff => "YOU'RE STANDING IN MY LIGHT.",
                Temper.Nervous => "WHO- WHO ARE YOU? WHAT DO YOU WANT?",
                Temper.Greedy => "A NEW FACE. NEW FACES HAVE SCRAP.",
                Temper.Pious => "MAY YOUR ENGINE NEVER SEIZE, TRAVELLER.",
                Temper.Joker => "OH GOOD, ANOTHER SURVIVOR. JUST WHAT THE WORLD NEEDS.",
                _ => "YOU'RE LOOKING AT A LEGEND. TAKE YOUR TIME."
            };
        }

        public static readonly string[] OpenPolite = { "EVENING. MIND IF I ASK A FEW THINGS?", "HELLO. I'M JUST PASSING THROUGH." };
        public static readonly string[] OpenBlunt = { "YOU. TALK.", "I NEED SOMETHING. YOU HAVE IT?" };
        public static readonly string[] OpenJoke = { "NICE WEATHER FOR THE END OF THE WORLD.", "DON'T WORRY, I ONLY BITE ON TUESDAYS." };
        public static readonly string[] OpenFlatter = { "YOU LOOK LIKE SOMEONE WHO KNOWS THINGS.", "FINE BOOTS. YOU CLEARLY KNOW QUALITY." };
        public static readonly string[] OpenThreat = { "HAND OVER WHAT YOU'VE GOT.", "I COULD MAKE THIS UNPLEASANT." };

        public static string ToneLine(Tone t, int i) => t switch
        {
            Tone.Polite => OpenPolite[i % OpenPolite.Length], Tone.Blunt => OpenBlunt[i % OpenBlunt.Length], Tone.Joke => OpenJoke[i % OpenJoke.Length],
            Tone.Flatter => OpenFlatter[i % OpenFlatter.Length], _ => OpenThreat[i % OpenThreat.Length]
        };

        static readonly int[,] Reactions =
        {
            //          Polite Blunt Joke Flatter Threat
            /*Friendly*/{  6,    0,    5,    2,    -15 },
            /*Gruff*/   { -1,    5,   -2,   -6,     -4 },
            /*Nervous*/ {  6,   -4,    1,    1,    -20 },
            /*Greedy*/  {  1,    1,   -1,    4,    -10 },
            /*Pious*/   {  5,   -2,   -6,    1,    -12 },
            /*Joker*/   {  1,   -1,    8,    1,     -8 },
            /*Proud*/   {  2,   -3,   -3,    8,    -15 },
        };

        /// <summary>How a temperament takes a tone: disposition change before charisma scaling.</summary>
        public static int Reaction(Temper temper, Tone tone) => Reactions[(int)temper, (int)tone];

        public static string ReactionLine(Temper temper, int delta) => delta >= 5
            ? temper switch { Temper.Gruff => "FINALLY, SOMEONE WHO DOESN'T WASTE MY TIME.", Temper.Joker => "HA! I LIKE YOU ALREADY.", Temper.Proud => "YOU HAVE A GOOD EYE.", Temper.Pious => "YOUR WORDS ARE CLEAN. RARE THESE DAYS.", Temper.Nervous => "OH. OKAY. YOU SEEM... NICE.", Temper.Greedy => "SWEET TALK AND A FULL POCKET? PERFECT.", _ => "WELL AREN'T YOU PLEASANT." }
            : delta >= 1 ? "HM. ALRIGHT."
            : delta >= -3 ? temper switch { Temper.Gruff => "SPARE ME.", Temper.Joker => "TOUGH CROWD, HUH?", _ => "...RIGHT." }
            : delta >= -9 ? temper switch { Temper.Proud => "WATCH HOW YOU TALK TO ME.", Temper.Pious => "THE ENGINE HEARS SUCH WORDS.", Temper.Gruff => "KEEP THAT UP AND WE'RE DONE.", _ => "THAT WASN'T VERY NICE." }
            : temper switch { Temper.Nervous => "PLEASE! I DON'T WANT TROUBLE!", Temper.Gruff => "TRY IT. SEE WHAT HAPPENS.", _ => "GET AWAY FROM ME." };

        static readonly string[] Styles = { "RAIDER", "HAZMAT", "DRIFTER", "RAGGED", "CLEAN" };
        // first impression of an outfit per temperament: Friendly, Gruff, Nervous, Greedy, Pious, Joker, Proud
        static readonly int[,] StyleReactions =
        {
            { -6, 2, -12, -2, -8, 0, -4 },     // raider
            { -2, -2, -8, -2, -4, 4, -2 },     // hazmat
            { 2, 5, -2, 1, 0, 2, 4 },          // drifter
            { 0, -1, -2, -6, 4, -1, -6 },      // ragged
            { 4, 1, 3, 4, 5, 1, 4 },           // clean
        };

        /// <summary>Disposition change the first time an NPC sees your outfit style (see WastelandGame.OutfitStyle).</summary>
        public static int StyleReaction(Temper temper, string style)
        {
            int i = System.Array.IndexOf(Styles, style);
            return i < 0 ? 0 : StyleReactions[i, (int)temper];
        }

        public static string StyleNote(string style, bool good) => style switch
        {
            "RAIDER" => good ? "(THEY LIKE YOUR WAR PAINT.)" : "(THEY EYE YOUR RAIDER GEAR WARILY.)",
            "HAZMAT" => good ? "(THEY GRIN AT YOUR SPACE SUIT.)" : "(THEY KEEP THEIR DISTANCE FROM YOUR HAZMAT GEAR.)",
            "DRIFTER" => good ? "(THEY NOD AT THE DUSTER AND HAT.)" : "(THEY SIZE UP THE GUNSLINGER LOOK.)",
            "RAGGED" => good ? "(THEY PITY YOUR RAGS.)" : "(THEY WRINKLE THEIR NOSE AT YOUR RAGS.)",
            _ => good ? "(YOU LOOK WELL KEPT.)" : "(TOO CLEAN FOR THE WASTES.)",
        };

        public static readonly string[] Farewell = { "SAFE ROADS.", "DON'T DIE OUT THERE.", "KEEP YOUR TANK FULL.", "SEE YOU AROUND.", "MIND THE RAIDERS." };

        /// <summary>Errands: what they ask for (item id or res:N), how many, and the kind of reward.</summary>
        public static readonly (string ask, string item, int n, string reward)[] Jobs =
        {
            ("MY STILL NEEDS SCRAP FOR A NEW COIL. BRING ME 12 SCRAP?", "res:1", 12, "part"),
            ("I HAVEN'T EATEN PROPERLY IN DAYS. THREE CANS OF FOOD AND I'LL MAKE IT WORTH IT.", "food_can", 3, "scrap"),
            ("MY LAMP'S DRY. 10 LITRES OF FUEL AND I'LL PAY WELL.", "res:7", 10, "scrap"),
            ("I NEED CLOTH FOR BANDAGES. 6 CLOTH, IF YOU CAN SPARE IT.", "res:6", 6, "item"),
            ("GLASS. 5 PIECES. DON'T ASK WHY.", "res:4", 5, "scrap"),
            ("SOMEONE STOLE MY WATER. 3 BOTTLES WOULD SAVE ME.", "drink_water", 3, "item"),
            ("WOOD FOR THE WINTER FIRE. 15 WOOD.", "res:2", 15, "scrap")
        };

        public static readonly string[] RaiderDemand =
        {
            "THIS ROAD'S A TOLL ROAD. PAY UP OR BLEED.",
            "NICE RIDE. SHAME IF IT GOT... REARRANGED. TOLL.",
            "THE {GANG} OWN THIS STRETCH. FUEL OR SCRAP, YOUR CHOICE."
        };

        public static string Compass(float dx, float dz)
        {
            float a = UnityEngine.Mathf.Atan2(dx, dz) * UnityEngine.Mathf.Rad2Deg;
            string[] n = { "NORTH", "NORTH-EAST", "EAST", "SOUTH-EAST", "SOUTH", "SOUTH-WEST", "WEST", "NORTH-WEST" };
            return n[((UnityEngine.Mathf.RoundToInt(a / 45f) % 8) + 8) % 8];
        }

        public static string Distance(float m) => m < 150f ? "JUST OVER THERE" : m < 600f ? "A SHORT DRIVE" : m < 1500f ? "HALF AN HOUR'S DRIVE" : "A LONG WAY";
    }
}
