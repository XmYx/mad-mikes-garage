namespace MadMax.Game
{
    /// <summary>What the character can tell by looking, not the numbers behind it: player-facing text uses these words
    /// for hidden values (condition, odds, growth, trust, soil, amounts). Instruments (a water test, a detector, the
    /// gauges) may still read out numbers.</summary>
    public static class Words
    {
        /// <summary>Wear of a part, garment or piece (1 = new).</summary>
        public static string Condition(float c) => c > 0.95f ? "LIKE NEW" : c > 0.75f ? "GOOD" : c > 0.5f ? "WORN" : c > 0.25f ? "BATTERED" : "FALLING APART";

        /// <summary>How an attempt feels before you make it.</summary>
        public static string Odds(float p) => p > 0.85f ? "STEADY HANDS" : p > 0.65f ? "LIKELY" : p > 0.4f ? "EVEN ODDS" : p > 0.2f ? "RISKY" : "A LONG SHOT";

        /// <summary>A growing thing: crop, tree, fleece.</summary>
        public static string Growth(float g) => g >= 1f ? "READY" : g > 0.7f ? "NEARLY THERE" : g > 0.35f ? "GROWING" : g > 0.05f ? "YOUNG" : "JUST STARTED";

        /// <summary>Soil: what the earth looks like in the hand.</summary>
        public static string Soil(float f) => f > 0.8f ? "RICH SOIL" : f > 0.55f ? "GOOD SOIL" : f > 0.3f ? "THIN SOIL" : "TIRED SOIL";

        /// <summary>An animal's trust.</summary>
        public static string Trust(float t) => t > 0.85f ? "TRUSTS YOU" : t > 0.5f ? "WARMING TO YOU" : t > 0.2f ? "CURIOUS" : "WARY";

        /// <summary>How much of something is there (a vial, a field, a fleece).</summary>
        public static string Amount(float a) => a > 0.9f ? "FULL" : a > 0.6f ? "MOSTLY FULL" : a > 0.35f ? "ABOUT HALF" : a > 0.1f ? "A LITTLE" : "NEXT TO NOTHING";

        /// <summary>Someone's (or something's) health.</summary>
        public static string Health(float h) => h > 0.9f ? "WELL" : h > 0.6f ? "MENDING" : h > 0.3f ? "POORLY" : "IN A BAD WAY";
    }
}
