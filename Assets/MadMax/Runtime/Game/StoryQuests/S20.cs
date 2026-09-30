using System.Collections.Generic;
using MadMax.Building;
using MadMax.Story;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game
{
    /// <summary>S20 LOW CLOUDS, HIGH HOPES: Oren's camp at the edge of town, his ultralight on the road, three ridge
    /// stations (post, vane, logger box). A logger is taken by arriving: on foot beside it, in a ground vehicle close by,
    /// or in an aircraft set down (or hovering low) next to it; the way you came is the step's route.</summary>
    public partial class WastelandGame
    {
        float s20Check;

        partial void Scene_S20()
        {
            if (!StoryAnchors.Has("oren") || !Build || !Build.Structures) return;
            PutAt("oren", "porch_awning", new Vector3(0f, 0f, -1.8f), 0f);
            PutAt("oren", "table", new Vector3(-2.2f, 0f, 0.4f), 0f);
            PutAt("oren", "chair", new Vector3(-2.2f, 0f, -0.9f), 0f);
            PutAt("oren", "radio", new Vector3(2.2f, 0f, -1.2f), 0f);
            PutAt("oren", "flag", new Vector3(3.6f, 0f, 1.6f), 0f);                                  // his windsock
            for (int i = 1; i <= 3; i++)
            {
                string a = "s20_i" + i;
                if (!StoryAnchors.Has(a)) continue;
                PutAt(a, "post", Vector3.zero, 0f);
                PutAt(a, "flag", new Vector3(0.7f, 0f, 0.5f), 0f);                                   // the wind vane
                PutAt(a, "crate", new Vector3(-0.8f, 0f, 0.3f), 15f);                                // the logger box
            }
            if (StoryAnchors.Has("s20_strip"))
            {
                var trike = Q3Vehicle("Ultralight", StoryAnchors.Get("s20_strip"), StoryAnchors.Yaw("s20_strip"), "s20_trike", 0.8f);
                if (trike) { trike.name = "Oren's Trike"; if (trike.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = sys.fuelCapacity; }
            }
        }

        partial void Tick_S20()
        {
            if (!Player || Time.time < s20Check) return;
            s20Check = Time.time + 0.25f;
            for (int i = 1; i <= 3; i++)
            {
                string step = "i" + i;
                if (Plot.StepDone("S20", step) || Plot.Flag("s20_got" + i)) continue;
                string how = S20Arrival(StoryAnchors.Get("s20_i" + i));
                if (how == null) continue;
                Plot.SetFlag("s20_got" + i);
                Plot.Note("s20:i" + i + ":" + how);
                var box = Q3Prop("crate", StoryAnchors.Get("s20_i" + i), 4f);
                if (box) Destroy(box.gameObject);
                MadMax.Audio.Sfx.Play("pickup", StoryAnchors.Get("s20_i" + i), 0.8f);
                Toast("WEATHER LOGGER " + i + " OF 3: " + (how == "air" ? "PLUCKED OFF THE RIDGE FROM THE COCKPIT" : how == "foot" ? "UNSCREWED FROM ITS POST BY HAND" : "LIFTED OUT THROUGH THE WINDOW"));
            }
            // Oren's navigation notes come with his reading: every airstrip he knows goes on the map
            if (Plot.StepDone("S20", "read") && !Plot.Flag("s20_notes"))
            {
                Plot.SetFlag("s20_notes");
                var sites = new List<Site>();
                World.SitesNear(StoryAnchors.Get("oren"), 3500f, sites);
                int n = 0;
                foreach (var s in sites) if (s.kind == SiteKind.Airfield && Discovered.Add(s.Key)) n++;
                Journal.Add("PLACE", n > 0 ? "OREN'S NAVIGATION NOTES: " + n + " AIRSTRIP" + (n > 1 ? "S" : "") + " MARKED ON YOUR MAP" : "OREN'S NAVIGATION NOTES: NO STRIP NEARBY YOU DIDN'T KNOW");
            }
            S20Payoff();
        }

        /// <summary>How the player reached a station: "foot" beside it, "road" in a ground vehicle close by, "air" in an
        /// aircraft set down or hovering low next to it (null = not there).</summary>
        string S20Arrival(Vector3 at)
        {
            if (!Current) return Q3Flat(Player.transform.position, at) < 3.5f ? "foot" : null;
            var p = Current.transform.position;
            float d = Q3Flat(p, at);
            if (Current.GetComponent<FlightModel>()) return d < 14f && p.y - terrain.Height(p.x, p.z) < 6f ? "air" : null;
            return d < 8f ? "road" : null;
        }

        static void S20Payoff()
        {
            int air = 0, foot = 0, road = 0;
            for (int i = 1; i <= 3; i++)
            {
                var r = Plot.Route("S20", "i" + i);
                if (r == "BY AIR") air++; else if (r == "ON FOOT") foot++; else if (r == "BY ROAD") road++;
            }
            string how = air == 3 ? "ALL THREE LOGGERS CAME DOWN BY AIR, AND OREN SAYS THE TRIKE HASN'T HAD THAT MUCH FUN SINCE THE WAR"
                       : foot == 3 ? "YOU WALKED THE WHOLE RIDGE FOR THEM, AND OREN'S KNEES SALUTE YOU"
                       : road == 3 ? "YOU DROVE THE LONG WAY ROUND FOR ALL THREE, WHICH OREN CALLS SENSIBLE AND MEANS AS A COMPLIMENT"
                       : "THE LOGGERS CAME DOWN BY WHATEVER WAS TO HAND";
            Q3Payoff("S20", how + ". IDA KNOWS WHAT THE SKY IS DOING, AND OREN'S NAVIGATION NOTES PUT EVERY AIRSTRIP HE KNOWS ON YOUR MAP.");
        }
    }
}
