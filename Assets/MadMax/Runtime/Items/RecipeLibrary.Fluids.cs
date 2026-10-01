using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Recipes for the hand liquid containers (fluids block): jerry can, fuel can, bottle, bucket, oil jug.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> FluidsRecipes()
        {
            var S = ResourceType.Scrap; var Rb = ResourceType.Rubber; var G = ResourceType.Glass;
            foreach (var d in FluidContainers.All)
            {
                (ResourceType, int)[] cost = d.id == FluidContainers.JerryCan ? new[] { (S, 5), (Rb, 1) }
                    : d.id == FluidContainers.FuelCan ? new[] { (S, 2), (Rb, 1) }
                    : d.id == FluidContainers.Bottle ? new[] { (G, 1) }
                    : d.id == FluidContainers.Bucket ? new[] { (S, 3) }
                    : new[] { (Rb, 2) };
                yield return Itm(d.id.Replace("tool_", "fluid_"), d.name, RecipeCategory.Tools, "workbench", d.id, 1, d.blurb, null, cost);
            }
        }
    }
}
