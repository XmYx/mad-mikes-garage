using MadMax.Items;
using MadMax.Story;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game
{
    /// <summary>S22 A JACKET FOR THE END OF THE WORLD: Dax's stall with a working sewing table, Ottilie's doorstep. The
    /// leather makings are swapped for cloth once, the coat handed over is taken and Ottilie wears it (her profile's
    /// outfit, body respawned), and on the test walk she follows the player like a companion until the lookout.</summary>
    public partial class WastelandGame
    {
        float s22Check;

        partial void Scene_S22()
        {
            Q3ResetPayoff("S22");
            if (!StoryAnchors.Has("dax") || !Build || !Build.Structures) return;
            PutAt("dax", "porch_awning", new Vector3(0f, 0f, -1.8f), 0f);
            PutAt("dax", "sewing_table", new Vector3(-2.2f, 0f, -0.6f), 0f);
            PutAt("dax", "chair", new Vector3(-2.2f, 0f, -1.8f), 0f);
            PutAt("dax", "shelf", new Vector3(2.6f, 0f, -2.2f), 0f);                                 // bolts of cloth
            PutAt("dax", "table", new Vector3(2.4f, 0f, 0.4f), 0f);
            if (StoryAnchors.Has("s22_home"))
            {
                PutAt("s22_home", "bench", new Vector3(-1.4f, 0f, -1.2f), 0f);
                PutAt("s22_home", "flower_pot", new Vector3(1.2f, 0f, -1.4f), 0f);
            }
        }

        /// <summary>The garment Ottilie was handed (a clothing id), from the fit step's route.</summary>
        static string S22Coat()
        {
            var r = Plot.Route("S22", "fit");
            if (r == null) return null;
            return r.Contains("PARKA") ? "coat" : r.Contains("BOMBER") ? "bomber" : r.Contains("DUSTER") ? "duster" : r.Contains("PONCHO") ? "poncho" : null;
        }

        partial void Tick_S22()
        {
            if (!Player || Time.time < s22Check) return;
            s22Check = Time.time + 0.25f;
            // leather makings: Dax keeps the leather for the facings and cuts the lining from his remnants
            if (Plot.StepDone("S22", "makings") && Q3Route("S22", "makings", "LEATHER") && !Plot.Flag("s22_swapped"))
            {
                Plot.SetFlag("s22_swapped");
                if (Inventory.TrySpend(ResourceType.Leather, 4)) Inventory.Add(ResourceType.Cloth, 8);
                Toast("DAX TAKES THE LEATHER FOR THE FACINGS AND CUTS 8 CLOTH OF LINING FROM HIS REMNANTS");
            }
            string coat = S22Coat();
            if (coat != null && !Plot.Flag("s22_handed"))
            {
                Plot.SetFlag("s22_handed");
                Inventory.TakeItem("cloth_" + coat);
            }
            if (coat != null) S22Dress(coat);
            // the test walk: she falls in beside you and walks up to the lookout
            var her = CastBody("s22_ottilie");
            bool walking = Plot.StepDone("S22", "fit") && !Plot.StepDone("S22", "walk");
            if (her)
            {
                if (walking && !her.companion && Vector3.Distance(her.transform.position, Player.transform.position) < 8f)
                {
                    her.companion = true; her.order = 0;
                    Toast("OTTILIE BUTTONS THE COAT AND FALLS IN BESIDE YOU. THE LOOKOUT IS UP THE HILL");
                }
                if (!walking && her.companion) her.companion = false;
                if (walking && her.companion && StoryAnchors.Has("s22_view") && Q3Flat(Player.transform.position, StoryAnchors.Get("s22_view")) < 9f
                    && Vector3.Distance(her.transform.position, Player.transform.position) < 14f)
                {
                    her.companion = false;
                    Plot.Note("s22:walked");
                    Plot.SetFlag(MadMax.World.Weather.Raining ? "s22_wet" : "s22_dry");
                    Toast("OTTILIE: " + S22Verdict(coat));
                }
            }
            S22Payoff(coat);
        }

        /// <summary>Ottilie wears the coat: it replaces what she wore in its slot; her body is rebuilt once.</summary>
        void S22Dress(string coat)
        {
            var p = StoryCast.Profile("s22_ottilie", World.seed);
            if (p == null || p.outfit.Contains(coat)) return;
            var def = ClothingLibrary.Get(coat);
            if (def != null) p.outfit.RemoveAll(o => ClothingLibrary.Get(o)?.slot == def.slot);
            p.outfit.Add(coat);
            var body = CastBody("s22_ottilie");
            if (body) { castBodies.Remove("s22_ottilie"); Destroy(body.gameObject); }                // back in a moment, dressed
        }

        static string S22Verdict(string coat)
        {
            bool warm = coat == "coat" || coat == "bomber", wet = MadMax.World.Weather.Raining;
            return warm ? (wet ? "\"DAMP AND CHEERFUL. WARM IS WARM; I'LL WALK FASTER IN THE WET.\"" : "\"TOASTY. THE WIND UP HERE CAN'T FIND ME.\"")
                        : (wet ? "\"DRY AS A SERMON. THIS IS THE ONE.\"" : "\"A BIT COOL IN THE WIND. I'LL ADD A JUMPER. IT'S STILL THE ONE.\"");
        }

        static void S22Payoff(string coat)
        {
            if (coat == null) return;
            var def = ClothingLibrary.Get(coat);
            bool warm = coat == "coat" || coat == "bomber";
            string walk = Plot.Flag("s22_wet") ? (warm ? " SHE CAME DOWN FROM THE LOOKOUT DAMP BUT WARM." : " IT RAINED ON THE TEST WALK AND SHE CAME DOWN DRY.")
                        : Plot.Flag("s22_dry") ? (warm ? " ON THE LOOKOUT THE WIND COULDN'T FIND HER." : " THE WIND ON THE LOOKOUT WAS COOL; SHE'LL ADD A JUMPER.") : "";
            Q3Payoff("S22", "OTTILIE WALKS NORTH IN A " + (def != null ? def.name : "COAT") + " YOU SEWED FOR THE " + (warm ? "COLD" : "RAIN") + "." + walk
                            + " DAX'S ROAD PATCH IS YOURS, AND SO IS THE HABIT OF CHECKING OTHER PEOPLE'S SEAMS.");
        }
    }
}
