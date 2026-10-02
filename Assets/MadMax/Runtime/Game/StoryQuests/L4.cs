using MadMax.Npc;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;
using Plot = MadMax.Story.Story;

namespace MadMax.Game
{
    /// <summary>L4 WHAT A RELIC IS FOR: the Church's procession truck parked near the shrine (painted, the Church's
    /// emblem, an engine nine years stood: nearly seized, a dry tank, old oil). Standing counts from FRIENDLY (the
    /// sandbox gift arrives by itself then); the truck brought to the shrine earns the Church's thanks. After Cask's
    /// question the blowers come down from the altar (<see cref="LastEngine.Give"/>, unless the gift already came).</summary>
    public partial class WastelandGame
    {
        float l4Check;

        partial void Scene_L4()
        {
            Q3ResetPayoff("L4");
            if (!StoryAnchors.Has("l4_truck")) return;
            var truck = Q3Vehicle("Pickup", StoryAnchors.Get("l4_truck"), StoryAnchors.Yaw("l4_truck"), "l4_truck") ?? Q3Vehicle("Sedan", StoryAnchors.Get("l4_truck"), StoryAnchors.Yaw("l4_truck"), "l4_truck");
            if (!truck) return;
            truck.name = "Procession Truck";
            var paint = truck.GetComponent<VehiclePaint>(); if (!paint) paint = truck.gameObject.AddComponent<VehiclePaint>();
            paint.colour = 5; paint.decal = Factions.Decal[(int)Faction.Church]; paint.Apply();
            if (truck.Engine && truck.Engine.TryGetComponent<VehiclePart>(out var ep)) ep.damage = 0.88f;           // nine years standing
            if (truck.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = 0f; sys.oil *= 0.3f; }
            if (Build && Build.Structures) PutAt("l4_truck", "flag", new Vector3(-3f, 0f, 0f), 0f);
        }

        partial void Tick_L4()
        {
            if (!Player || Time.time < l4Check) return;
            l4Check = Time.time + 0.3f;
            if (!Plot.StepDone("L4", "trust") && Factions.Rank(Faction.Church) >= 4) Plot.Note("l4:standing");
            if (Plot.StepDone("L4", "trust") && Q3Route("L4", "trust", "THE PROCESSION") && !Plot.Flag("l4_thanked"))
            {
                Plot.SetFlag("l4_thanked");
                Factions.Shift(Faction.Church, 8);
                Toast("THE CHURCH RINGS ITS LITTLE BELL FOR THE TRUCK. YOU'RE IN THEIR BOOK NOW");
            }
            if (Plot.StepDone("L4", "decide"))
            {
                bool loan = Q3Route("L4", "decide", "LEND");
                if (!LastEngine.Has("relic_blower")) LastEngine.Give(this, "relic_blower", loan ? "ON LOAN FROM THE CHURCH" : "FROM THE CHURCH'S ALTAR");
                Plot.Note("l4:given");
                Q3Payoff("L4", (Q3Route("L4", "trust", "A FRIEND") ? "THE CHURCH TRUSTED A FRIEND" : "THE PROCESSION TRUCK RUNS AGAIN, AND THE CHURCH TRUSTED THE HANDS THAT DID IT")
                               + (loan ? ". THE BLOWERS ARE YOURS ON LOAN, WRITTEN IN THE BOOK IN YOUR NAME." : ". THE BLOWERS ARE YOURS: CASK DECIDED HARLAN DIDN'T BUILD A PHOTOGRAPH."));
            }
        }
    }
}
