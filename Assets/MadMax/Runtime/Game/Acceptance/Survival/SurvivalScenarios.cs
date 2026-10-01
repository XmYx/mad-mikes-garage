using System.Collections.Generic;

namespace MadMax.Game.Acceptance
{
    /// <summary>Roadmap 26 survival, home, world and UI scenarios (Q2 first session and navigation, Q4 survival and the
    /// productive home, Q5 persistence and the living world).</summary>
    public static class SurvivalScenarios
    {
        /// <summary>Long scenarios: the full run and their own suite (<c>-acceptance survival</c>), not the fast PR gate.</summary>
        public static readonly string[] Suites = { "full", "survival" };

        public static IEnumerable<Scenario> All()
        {
            yield return new SurvivalLoop();
            yield return new SurvivalVitals();
            yield return new SurvivalInjuries();
            yield return new SurvivalParkour();
            yield return new SurvivalBase();
            yield return new SurvivalConservation();
            yield return new SurvivalGardenFishLivestock();
            yield return new SurvivalSaveMidActivity();
            yield return new SurvivalWeatherDecay();
            yield return new WorldPlanet();
            yield return new WorldSites();
            yield return new UiPages();
        }
    }
}
