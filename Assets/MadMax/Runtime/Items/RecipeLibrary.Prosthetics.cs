using System.Collections.Generic;

namespace MadMax.Items
{
    /// <summary>Prosthetics (<see cref="MadMax.Game.ProstheticLibrary"/>): hooks, carved hands and feet and the peg leg
    /// at the workbench; the claw, steel strut and blade spring at the forge; the machinist's hand, the mount arms
    /// (blade, torch, shotgun) and the hydraulic arm at the machine shop once the prosthetics blueprint is learned.</summary>
    public static partial class RecipeLibrary
    {
        static IEnumerable<Recipe> ProstheticRecipes()
        {
            var W = ResourceType.Wood; var Le = ResourceType.Leather; var Fe = ResourceType.Iron; var S = ResourceType.Scrap;
            var St = ResourceType.Steel; var Cu = ResourceType.Copper; var Ru = ResourceType.Rubber;
            const RecipeCategory C = RecipeCategory.Supplies;
            yield return Itm("pros_hook", "HOOK HAND", C, "workbench", "pros_hook", 1, "A STEEL HOOK ON A LEATHER CUFF (FITS A LOST HAND)", null, (Fe, 1), (Le, 1), (S, 1));
            yield return Itm("pros_wood_hand", "CARVED HAND", C, "workbench", "pros_wood_hand", 1, "A WOODEN HAND: LOOKS RIGHT, GRIPS LITTLE (FITS A LOST HAND)", null, (W, 2), (Le, 1));
            yield return Itm("pros_wood_foot", "WOODEN FOOT", C, "workbench", "pros_wood_foot", 1, "A CARVED FOOT (FITS A LOST FOOT)", null, (W, 2), (Le, 1), (Ru, 1));
            yield return Itm("pros_peg_leg", "PEG LEG", C, "workbench", "pros_peg_leg", 1, "A TURNED PEG (FITS A LEG LOST BELOW THE KNEE)", null, (W, 4), (Le, 2));
            yield return Itm("pros_claw", "SPRUNG CLAW", C, "forge", "pros_claw", 1, "TWO SPRUNG PRONGS (FITS A LOST HAND)", null, (Fe, 2), (Le, 1), (S, 2));
            yield return Itm("pros_strut_leg", "STEEL STRUT", C, "forge", "pros_strut_leg", 1, "A WELDED STRUT, RUBBER FOOT (FITS A LEG LOST BELOW THE KNEE)", null, (St, 2), (Le, 2), (Ru, 1));
            yield return Itm("pros_spring_leg", "BLADE SPRING", C, "forge", "pros_spring_leg", 1, "A LEAF-SPRING BLADE: QUICK, TIRING (FITS A LEG LOST BELOW THE KNEE)", null, (St, 3), (Le, 2), (Ru, 1));
            var mech = Itm("pros_mech_hand", "MACHINIST'S HAND", C, "machine_shop", "pros_mech_hand", 1, "CABLES AND SPRINGS: NEARLY A HAND (FITS A LOST HAND)", null, (St, 2), (Cu, 1), (Le, 1));
            var blade = Itm("pros_blade_arm", "BLADE ARM", C, "machine_shop", "pros_blade_arm", 1, "A MACHETE MOUNT (FITS AN ARM LOST BELOW THE ELBOW)", null, (St, 3), (Le, 2));
            var torch = Itm("pros_torch_arm", "TORCH ARM", C, "machine_shop", "pros_torch_arm", 1, "A GAS-TORCH MOUNT (FITS AN ARM LOST BELOW THE ELBOW)", null, (St, 3), (Cu, 2), (Le, 2));
            var gun = Itm("pros_shotgun_arm", "SHOTGUN ARM", C, "machine_shop", "pros_shotgun_arm", 1, "A PIPE-SHOTGUN MOUNT (FITS AN ARM LOST BELOW THE ELBOW)", null, (St, 4), (Le, 2));
            var hyd = Itm("pros_hydraulic_arm", "HYDRAULIC ARM", C, "machine_shop", "pros_hydraulic_arm", 1, "PISTONS AND A STEEL GRAB: +15 KG CARRIED (FITS AN ARM LOST BELOW THE ELBOW)", null, (St, 5), (Cu, 2), (ResourceType.Oil, 2), (Le, 2));
            foreach (var r in new[] { mech, blade, torch, gun, hyd }) { r.knowledge = "bp_prosthetics"; r.seconds = 30f; yield return r; }
        }
    }
}
