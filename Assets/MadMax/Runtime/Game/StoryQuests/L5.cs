using MadMax.Story;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game
{
    /// <summary>L5 THE LAST RUN: the pilgrims' fuel cache halfway north and a flag at the destination (the White Wall, or
    /// the last cape where the land gives out). A car carrying the V12 (<see cref="LastEngine.Mounted"/>) counts: it is
    /// running once LastEngine marks it built, tuned when its <see cref="VehicleTuning"/> is off stock, run in after 3 km,
    /// and arrives within 70 m of the destination (or at the polar ice anywhere: LastEngine's own Last Run).</summary>
    public partial class WastelandGame
    {
        float l5Check, l5Dist;
        Vector3 l5Last;

        /// <summary>The Last Run's destination by name: the ice at 80° or more, else the last land before the polar sea.</summary>
        static string L5Place => StoryAnchors.Has("l5_end") && WorldGen.Latitude(StoryAnchors.Get("l5_end").z) >= 80f ? "THE WHITE WALL" : "THE LAST CAPE BEFORE THE POLAR SEA";

        partial void Scene_L5()
        {
            Q3ResetPayoff("L5");
            if (!Build || !Build.Structures) return;
            if (StoryAnchors.Has("l5_cache"))
            {
                PutAt("l5_cache", "barrel", new Vector3(-1.2f, 0f, 0f), 0f);
                PutAt("l5_cache", "barrel", new Vector3(-0.2f, 0f, 0.6f), 0f);
                PutAt("l5_cache", "barrel", new Vector3(0.8f, 0f, -0.2f), 0f);
                PutAt("l5_cache", "tyres", new Vector3(2.2f, 0f, 0.8f), 0f);
                PutAt("l5_cache", "flag", new Vector3(0f, 0f, -2f), 0f);
            }
            if (StoryAnchors.Has("l5_end")) { PutAt("l5_end", "post", Vector3.zero, 0f); PutAt("l5_end", "flag", new Vector3(0.8f, 0f, 0f), 0f); }
        }

        partial void Tick_L5()
        {
            if (!Player) return;
            var car = Current;
            bool v12 = car && LastEngine.Mounted(car);
            if (v12)
            {
                var p = car.transform.position;
                if (l5Last != Vector3.zero) l5Dist += Mathf.Min(8f, Q3Flat(p, l5Last));
                l5Last = p;
            }
            else l5Last = Vector3.zero;
            if (Time.time < l5Check) return;
            l5Check = Time.time + 0.25f;
            if (LastEngine.Has("built")) { Plot.Note("l5:built"); Plot.Note("l5:running"); }
            if (v12)
            {
                if (l5Dist >= 3000f) Plot.Note("l5:run_in");
                if (car.TryGetComponent<VehicleTuning>(out var tune) && !tune.Stock) Plot.Note("l5:tuned");
                if (Plot.StepDone("L5", "tune") && StoryAnchors.Has("l5_end") && Q3Flat(car.transform.position, StoryAnchors.Get("l5_end")) < 70f) LastEngine.Run(this, L5Place);
            }
            if (LastEngine.Has("run") && !Plot.Flag("l5_arrived"))
            {
                Plot.SetFlag("l5_arrived");
                Plot.Note("l5:arrived");
                MadMax.Audio.RadioNetwork.Flash("NEWS: HARLAN'S V12, THE LAST ENGINE, RAN ALL THE WAY TO " + L5Place + " TODAY. DRIVERS ON EVERY ROAD ARE ALREADY TELLING IT WRONG.");
            }
            if (Plot.Flag("l5_arrived")) Plot.Note("l5:arrived");
            L5Payoff();
        }

        static void L5Payoff()
        {
            string tuned = Q3Route("L5", "tune", "TUNED") ? "TUNED AT A BENCH" : Q3Route("L5", "tune", "RUN IN") ? "RUN IN ON THE ROAD" : "BUILT";
            string blowers = Q3Route("L4", "decide", "LEND") ? " THE CHURCH'S BOOK SAYS THE BLOWERS' LOAN IS REPAID, WITH A STORY." : " THE CHURCH LISTENED FROM THE SHRINE.";
            Q3Payoff("L5", "HARLAN'S V12, " + tuned + ", MADE THE LAST RUN TO " + L5Place + "." + blowers + " THE ENGINE IS YOURS, THIRST AND ALL, AND THE RADIO TELLS IT WRONG IN SEVERAL WAYS.");
        }
    }
}
