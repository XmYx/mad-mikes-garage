using MadMax.Items;
using MadMax.World;

namespace MadMax.Npc
{
    /// <summary>Seasonal chores (Seasons): what village residents need doing with the year — water for the parched beds
    /// in summer, bringing in the crop at harvest, cutting firewood before the first snow, restocking a bare cellar in
    /// winter, seed for the spring planting. A resident asks once per half season (<see cref="ChoreStamp"/>); the state
    /// lives in <see cref="NpcSave.choreStamp"/>.</summary>
    public static partial class NpcLore
    {
        public struct Chore
        {
            public int season;            // 0 summer, 1 autumn, 2 winter, 3 spring
            public bool late;             // second half of the season only (else first half, or both when no late one exists)
            public string ask, item, what, thanks, gift;
            public int n, giftN;
        }

        /// <summary>item: an id, <c>res:N</c>, or a group <c>any:harvest</c> / <c>any:preserve</c> / <c>any:seed</c>.</summary>
        public static readonly Chore[] Chores =
        {
            new Chore { season = 0, ask = "THE BEDS ARE CRACKING IN THIS HEAT AND THE WELL'S HALF DRY. 20 LITRES OF WATER FOR THE FIELD?",
                item = "res:" + (int)ResourceType.Water, n = 20, what = "LITRES OF WATER", thanks = "THE BEANS WILL LIVE. TAKE A FEW TOMATOES FOR THE ROAD.", gift = "food_tomato", giftN = 3 },
            new Chore { season = 1, ask = "HARVEST'S STANDING IN THE FIELD AND MY BACK'S GONE. BRING IN 6 OF THE CROP - CORN, POTATOES, WHEAT, PUMPKINS, WHATEVER YOU PICK.",
                item = "any:harvest", n = 6, what = "CROP", thanks = "IN BEFORE THE RAIN. HAVE A JAR OF LAST YEAR'S PICKLES.", gift = "food_pickles", giftN = 2 },
            new Chore { season = 1, late = true, ask = "FIRST SNOW'S COMING AND THE WOODPILE'S A JOKE. CUT ME 20 WOOD BEFORE IT FLIES?",
                item = "res:" + (int)ResourceType.Wood, n = 20, what = "WOOD", thanks = "THAT'LL SEE US THROUGH THE FIRST FREEZE. HERE, SOME JERKY FOR THE COLD NIGHTS.", gift = "food_jerky", giftN = 2 },
            new Chore { season = 2, ask = "THE CELLAR'S BARE AND IT'S A LONG WAY TO SPRING. 4 OF ANYTHING THAT KEEPS - SMOKED, DRIED, PICKLED, TINNED?",
                item = "any:preserve", n = 4, what = "PRESERVED FOOD", thanks = "WE'LL EAT THIS WINTER. TAKE SOME SEED - YOU'LL WANT IT COME THE THAW.", gift = "seed_tomato", giftN = 3 },
            new Chore { season = 3, ask = "PLANTING TIME AND THE MICE HAD MY SEED. 4 PACKETS OF ANY SEED AND THE FIELD GOES IN.",
                item = "any:seed", n = 4, what = "SEED PACKETS", thanks = "IT'S IN THE GROUND. HERE, A SAPLING FROM MY OLD TREE.", gift = "sapling_apple", giftN = 1 },
        };

        /// <summary>The chore residents ask for now (-1: none).</summary>
        public static int ChoreNow() => ChoreFor(Weather.Season, Weather.SeasonProgress >= 0.5f);

        /// <summary>The chore of a season's first or second half (-1: none).</summary>
        public static int ChoreFor(int season, bool late)
        {
            int any = -1;
            for (int i = 0; i < Chores.Length; i++)
            {
                if (Chores[i].season != season) continue;
                if (Chores[i].late == late) return i;
                if (!Chores[i].late) any = i;
            }
            return late ? any : -1;
        }

        /// <summary>A number that changes every half season (never repeats within a game when seasons run by the clock).</summary>
        public static int ChoreStamp()
        {
            int half = Weather.SeasonProgress >= 0.5f ? 1 : 0;
            if (Weather.DaysPerSeason <= 0) return Weather.Season * 2 + half;
            int abs = Weather.SeasonStart + (int)((DayNight.Day + DayNight.Hours / 24f) / Weather.DaysPerSeason);
            return abs * 2 + half;
        }

        /// <summary>Does <paramref name="id"/> count towards the chore?</summary>
        public static bool ChoreTakes(Chore c, string id) => c.item switch
        {
            "any:harvest" => FoodLibrary.Harvest(id),
            "any:preserve" => FoodLibrary.Preserved(id),
            "any:seed" => id.StartsWith("seed_"),
            _ => id == c.item,
        };
    }
}
