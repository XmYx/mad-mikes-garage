using MadMax.Building;
using MadMax.Story;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>B4 WHO GETS A KEY?: reads the rooms (two beds and a door you built at the garage), brings the Quayle
    /// pump home when that's the rule (a well behind the garage), records the rules and the staffed service for
    /// <see cref="Residents"/>, and moves the household in once the pantry holds enough food.</summary>
    public partial class WastelandGame
    {
        partial void Scene_B4()
        {
            if (!StoryAnchors.Has("b4_rooms") || !Build || !Build.Structures) return;
            // two bundles on the step: everything Hester and Judd own
            PutAt("b4_rooms", "crate", new Vector3(-1.2f, 0f, -1.4f), 10f);
            PutAt("b4_rooms", "barrel", new Vector3(1.4f, 0f, -1.6f), 0f);
        }

        partial void Tick_B4()
        {
            var q = StoryLibrary.Get("B4");
            if (q == null || Time.frameCount % 15 != 0) return;
            string rooms = Story.Story.Route("B4", "rooms"), rules = Story.Story.Route("B4", "rules"), service = Story.Story.Route("B4", "service");
            q.payoff = StoryLibrary.B4_Payoff(rooms, rules, service);
            var cur = Story.Story.Current(q);
            string step = cur != null ? cur.id : null;
            if (step == "rooms" && Q7Built("garage", "bed", 24f) >= 2 && Q7Built("garage", "door_wood|door_metal|garage_door", 24f) >= 1) Story.Story.Note("b4:rooms");
            if (rules == StoryLibrary.B4Pump && !Story.Story.Flag("b4:pump"))
            {
                Story.Story.SetFlag("b4:pump");
                PutAt("garage", "well", new Vector3(-6.5f, 0f, -4.5f), 0f);
                Toast("JUDD FETCHES THE QUAYLE PUMP FROM NELL'S SHED AND SETS IT ON A WELL BEHIND THE GARAGE");
                Journal.Add("HOME", "THE QUAYLE PUMP IS BACK IN THE GROUND, BEHIND THE GARAGE. HESTER WATCHED EVERY BOLT.");
            }
            if (service != null && !Story.Story.Flag("b4:service"))
            {
                Story.Story.SetFlag("b4:service");
                Story.Story.SetFlag(service == StoryLibrary.B4Repair ? "residents:service:repair" : service == StoryLibrary.B4Crops ? "residents:service:crops" : "residents:service:medical");
            }
            if (step == "pantry")
            {
                int n = Residents.Portions();
                cur.text = "THEY EAT TOO: STOCK A CHEST OR FRIDGE AT THE GARAGE WITH AT LEAST 4 PORTIONS OF FOOD (" + Mathf.Min(n, 4) + "/4)";
                if (n >= 4)
                {
                    Story.Story.SetFlag("residents");                                             // they live here from now on (Residents runs the house)
                    Journal.Add("HOME", "HESTER AND JUDD LIVE AT THE GARAGE: SUPPER AT SEVEN FROM THE STORES, " + Residents.Name(Residents.Current) + " BY DAY. NO FOOD, NO WORK.");
                    Story.Story.Note("b4:pantry");
                }
            }
        }
    }
}
