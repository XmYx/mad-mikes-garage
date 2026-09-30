using MadMax.Npc;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    // P3.3 WHO OWNS A WRECK: Iris Marrow's bench above the harbour; the settlement's consequences: a returned keepsake
    // leaves the pack; honest routes earn the settlers' regard and Halvard's mooring (flag p3_mooring); the lie costs
    // settler standing, the mooring, and is remembered (flag p3_dishonest).
    public partial class WastelandGame
    {
        partial void Scene_P3_3()
        {
            if (!Build || !Build.Structures || !StoryAnchors.Has("p3_heir")) return;
            PutAt("p3_heir", "bench", new Vector3(0f, 0f, -1.2f), 0f);
        }

        partial void Tick_P3_3()
        {
            if (Time.time < p3Check) return;
            p3Check = Time.time + 0.5f;
            string how = Story.Story.Route("P3.3", "settle");
            if (how == null) return;
            bool honest = StoryLibrary.P3Honest(how);
            if (!Story.Story.Flag("p3_settled"))
            {
                Story.Story.SetFlag("p3_settled");
                if (how.StartsWith("THESE WERE IN THE MERIDIAN")) Inventory.TakeItem("story_p3_letters");
                else if (how.StartsWith("THIS WAS IN THE ALBA")) Inventory.TakeItem("story_p3_photo");
                if (honest)
                {
                    Story.Story.SetFlag("p3_mooring");
                    Factions.Shift(Faction.Settlers, 3);
                    Journal.Add("PLACE", "HALVARD'S MOORING: YOUR BOATS MAY TIE UP AT HIS HARBOUR, AND HIS CREW WILL WATCH THEM");
                }
                else
                {
                    Story.Story.SetFlag("p3_dishonest");
                    Factions.Shift(Faction.Settlers, -4);
                    var iris = CastBody("p3_heir");
                    if (iris) iris.State.disposition -= 25;
                    Journal.Add("STORY", "THE HARBOUR HEARD WHAT REALLY CAME UP. SETTLERS THINK LESS OF YOU, AND HALVARD OFFERS NO MOORING.");
                }
            }
            StoryLibrary.Get("P3.3").payoff = !honest ? "YOU KEPT THE SALVAGE AND SAID NOTHING CAME UP. THE HARBOUR KNOWS; IRIS MARROW KNOWS. THE SEA DOESN'T KEEP RECEIPTS, BUT PEOPLE DO."
                : how.StartsWith("A THIRD") ? "IRIS MARROW TOOK A THIRD OF THE SALVAGE. HALVARD GAVE YOU A MOORING AND PEG GAVE YOU HER MONSTER'S NAME: MORAG."
                : "IRIS MARROW HAS HER GRANDFATHER'S THINGS BACK. HALVARD GAVE YOU A MOORING AND PEG GAVE YOU HER MONSTER'S NAME: MORAG.";
        }
    }
}
