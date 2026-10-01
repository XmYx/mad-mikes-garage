using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Item ids of the lighting block (build kits for the switch and the street lights).</summary>
    public static class LightIds
    {
        public const string SwitchKit = "kit_light_switch", CobraKit = "kit_lamp_cobra", LanternKit = "kit_lamp_lantern", PoleKit = "kit_lamp_pole",
            MastKit = "kit_floodlight_mast", SolarLampKit = "kit_lamp_solar";
    }

    /// <summary>Recipes of the lighting block: a wall switch and five street lights, all made at the workbench and
    /// built from the kit (the indoor fixtures cost raw materials in the build menu).</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> LightsRecipes()
        {
            var S = ResourceType.Scrap; var W = ResourceType.Wood; var G = ResourceType.Glass; var Fe = ResourceType.Iron; var Cu = ResourceType.Copper; var Al = ResourceType.Aluminium;
            var B = RecipeCategory.Building;
            yield return Itm("light_switch", "LIGHT SWITCH", B, "workbench", LightIds.SwitchKit, 1,
                "WALL SWITCH: RUNS THE LIGHTS CABLED TO IT, ELSE THOSE IN ITS ROOM OR VEHICLE", null, (Cu, 1), (S, 1));
            yield return Itm("lamp_pole", "POLE LAMP", B, "workbench", LightIds.PoleKit, 1,
                "WOODEN POLE, HANGING BULB. 60 W ON A POWER CABLE, DUSK TO DAWN", null, (W, 6), (Cu, 1), (G, 1), (S, 1));
            yield return Itm("lamp_lantern", "LANTERN POST", B, "workbench", LightIds.LanternKit, 1,
                "OLD GAS-STYLE LANTERN, WIRED. 60 W ON A POWER CABLE, DUSK TO DAWN", null, (Fe, 4), (G, 3), (Cu, 1));
            yield return Itm("lamp_cobra", "HIGHWAY LAMP", B, "workbench", LightIds.CobraKit, 1,
                "7 M COBRA-HEAD STREET LAMP. 150 W ON A POWER CABLE, DUSK TO DAWN", null, (Fe, 6), (Cu, 2), (G, 2), (Al, 1));
            yield return Itm("lamp_solar", "SOLAR LAMP", B, "workbench", LightIds.SolarLampKit, 1,
                "OWN PANEL AND BATTERY: NO CABLE NEEDED, DUSK TO DAWN", new[] { (ItemIds.SolarCell, 1) }, (Fe, 3), (Cu, 3), (G, 1));
            var mast = Itm("floodlight_mast", "FLOODLIGHT MAST", B, "workbench", LightIds.MastKit, 1,
                "8 M LATTICE MAST, TWIN FLOODS. 400 W ON A POWER CABLE, DUSK TO DAWN", null, (Fe, 10), (Cu, 3), (G, 4), (Al, 2));
            mast.seconds = 30f;
            yield return mast;
        }
    }
}
