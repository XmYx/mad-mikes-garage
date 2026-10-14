using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Running gear for moving bases, made at the garage (the frames and mounts themselves are build pieces,
    /// <c>FurnitureLibrary.Rig</c>). Explicit costs: the mass-based garage formula would ask tonnes of rubber for a
    /// crawler drum.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> RigRecipes()
        {
            var S = ResourceType.Scrap; var Fe = ResourceType.Iron; var Stl = ResourceType.Steel; var Rb = ResourceType.Rubber;
            yield return RigPart("rig_wheel_heavy", "EARTHMOVER TYRE", "wheel_heavy", "1.1 M TYRE FOR RIG AXLES (M AND UP)", 120f, (Rb, 40), (Fe, 12), (Stl, 4));
            yield return RigPart("rig_wheel_giant", "HAUL TRUCK TYRE", "wheel_giant", "1.6 M TYRE: HEAVY RIG AXLES ONLY", 200f, (Rb, 80), (Fe, 20), (Stl, 12));
            yield return RigPart("rig_track_wide", "WIDE TRACK SPROCKET", "wheel_track_wide", "0.8 M WIDE CRAWLER RUN: FIT FRONT AND BACK ON TRACK MOUNTS", 120f, (Stl, 10), (Fe, 16), (Rb, 8), (S, 10));
            yield return RigPart("rig_track_heavy", "HEAVY TRACK DRUM", "wheel_track_heavy", "1.4 M WIDE CRAWLER RUN FOR BIG BASES", 220f, (Stl, 30), (Fe, 40), (Rb, 14), (S, 20));
            yield return RigPart("rig_track_giant", "CITY CRAWLER DRUM", "wheel_track_giant", "THE ROLLING CITY'S TREAD, CUT DOWN: 2 M DRUM, 2.2 M BELT", 420f, (Stl, 90), (Fe, 120), (Rb, 30), (S, 60));
        }

        static Recipe RigPart(string id, string name, string part, string desc, float seconds, params (ResourceType, int)[] res) =>
            new Recipe
            {
                id = id, name = name, category = RecipeCategory.Vehicles, kind = OutputKind.Part, output = part, station = "garage", description = desc,
                seconds = seconds, items = new (string, int)[0], resources = res
            };
    }
}
