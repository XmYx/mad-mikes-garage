using MadMax.Npc;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>C2 THE CHEAP ROAD in the world: Pru's trial truck (a pickup with a crate of seed potatoes in the bed) at the
    /// village, the warden's post out on the flats, and a soft wash dug across the public road. While it runs, the trial
    /// truck keeps the score of each run between the village's edge and the market's (minutes, litres, knocks, distance)
    /// and says which way it went (past the warden's gate, or along the public road through the wash); the wash is watched
    /// for fill or a hard surface. Once the road is decided: tyres to match it beside the truck, a first haul contract.</summary>
    public partial class WastelandGame
    {
        char c2From;
        float c2T0, c2Fuel0, c2Wear0, c2Dist, c2GateMin, c2PubMin;
        Vector3 c2Last;
        bool c2Drove;

        partial void Scene_C2()
        {
            if (!Build || !Build.Structures) return;
            ArcCPut("c2_start", "tyres", new Vector3(2.4f, 0f, -1.8f), 20f);
            ArcCPut("c2_start", "barrel", new Vector3(-2.6f, 0f, -1.6f), 0f);
            ArcCPut("c2_start", "sign", new Vector3(3.2f, 0f, 0.6f), 0f);
            var truck = ArcCVehicle("Pickup", "c2_start", new Vector3(-5.5f, 0f, 3.6f), 90f, "c2_trial", "Village Trial Truck");
            if (truck)
            {
                if (truck.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = Mathf.Min(sys.fuelCapacity, 30f);
                ArcCMount(truck, "cargo", "cargo_crate");
                var paint = truck.GetComponent<VehiclePaint>() ?? truck.gameObject.AddComponent<VehiclePaint>();
                paint.colour = 3; paint.Apply();
            }
            // the warden's post beside his track
            ArcCPut("c2_gate", "porch_awning", new Vector3(-4f, 0f, -2f), 0f);
            ArcCPut("c2_gate", "chair", new Vector3(-4f, 0f, -2.6f), 0f);
            ArcCPut("c2_gate", "sign", new Vector3(-2f, 0f, 1f), 0f);
            ArcCPut("c2_gate", "flag", new Vector3(3.5f, 0f, 1f), 0f);
            ArcCPut("c2_gate", "fence_wood", new Vector3(-6.5f, 0f, 1f), 0f);
            ArcCPut("c2_gate", "fence_wood", new Vector3(6.5f, 0f, 1f), 0f);
            // the soft wash across the public road
            if (!Story.Story.Flag("c2_dug") && StoryAnchors.Has("c2_crossing"))
            {
                Story.Story.SetFlag("c2_dug");
                ArcCTrench(StoryAnchors.Get("c2_crossing"), StoryAnchors.Yaw("c2_crossing"), 4.5f, 0.7f, 3.5f);
            }
            ArcCPut("c2_crossing", "sign", new Vector3(6.5f, 0f, -5f), -90f);
            Journal.Add("PLACE", "PRU HALLORAN'S TRIAL TRUCK AT THE VILLAGE; THE WARDEN'S TRACK ACROSS THE FLATS; A SOFT WASH ON THE PUBLIC ROAD");
        }

        partial void Tick_C2()
        {
            var truck = ArcCTagged("c2_trial");
            if (truck) C2Survey(truck);
            if (Time.frameCount % 30 != 0) return;
            if (ArcCStep("C2") == "survey" && !Story.Story.Flag("c2_way"))
            {
                Story.Story.SetFlag("c2_way");
                bool shortDone = Story.Story.StepDone("C2", "short");
                SetWaypoint(StoryAnchors.Get(shortDone ? "c2_crossing" : "c2_gate"), shortDone ? "SURVEY: THE PUBLIC ROAD" : "SURVEY: THE WARDEN'S TRACK", true);
            }
            C2Wash();
            C2Decided(truck);
        }

        /// <summary>Runs between the two edges in the trial truck: timed, measured and classified by the way it went.</summary>
        void C2Survey(VehicleDriver truck)
        {
            var p = truck.transform.position;
            bool inV = ArcCFlat(p, StoryAnchors.Get("c_village")) < 45f, inM = ArcCFlat(p, StoryAnchors.Get("c_market")) < 45f;
            char zone = inV ? 'V' : inM ? 'M' : '\0';
            if (Current == truck) c2Drove = true;
            if (zone != '\0')
            {
                if (c2From != '\0' && zone != c2From && c2Dist > 60f) C2Finish(truck);
                c2From = zone; c2T0 = Time.time; c2Fuel0 = truck.TryGetComponent<VehicleSystems>(out var s0) ? s0.fuel : 0f;
                c2Wear0 = ArcCWear(truck); c2Dist = 0f; c2GateMin = c2PubMin = float.MaxValue; c2Last = p; c2Drove = Current == truck;
                return;
            }
            if (c2From == '\0') return;
            c2Dist += ArcCFlat(p, c2Last); c2Last = p;
            c2GateMin = Mathf.Min(c2GateMin, ArcCFlat(p, StoryAnchors.Get("c2_gate")));
            c2PubMin = Mathf.Min(c2PubMin, Mathf.Min(ArcCFlat(p, StoryAnchors.Get("c2_public")), ArcCFlat(p, StoryAnchors.Get("c2_crossing"))));
        }

        void C2Finish(VehicleDriver truck)
        {
            if (!c2Drove) { Toast("THE TRIAL COUNTS WHEN YOU DRIVE THE TRUCK YOURSELF"); return; }
            string route = c2GateMin < 40f && c2GateMin + 15f < c2PubMin ? "short" : c2PubMin < 25f && c2PubMin + 15f < c2GateMin ? "public" : null;
            if (route == null) { Toast("THAT RUN MIXED BOTH WAYS: DRIVE ONE ROUTE OR THE OTHER"); return; }
            string k = "c2:" + route + ":";
            if (ArcCRecord.Has(k + "s")) { Toast((route == "short" ? "THE WARDEN'S TRACK" : "THE PUBLIC ROAD") + " IS ALREADY SURVEYED"); return; }
            float fuel = truck.TryGetComponent<VehicleSystems>(out var s) ? s.fuel : c2Fuel0;
            ArcCRecord.Put(k + "s", Mathf.RoundToInt(Time.time - c2T0));
            ArcCRecord.Put(k + "dl", Mathf.RoundToInt(Mathf.Max(0f, c2Fuel0 - fuel) * 10f));
            ArcCRecord.Put(k + "dmg", Mathf.RoundToInt(Mathf.Max(0f, ArcCWear(truck) - c2Wear0) * 100f));
            ArcCRecord.Put(k + "m", Mathf.RoundToInt(c2Dist));
            string line = StoryLibrary.C2Run(route);
            Journal.Add("SURVEY", line);
            Toast("SURVEYED: " + line);
            Story.Story.SetFlag("c2_way_" + route);
            Story.Story.Note("c2:" + route);
            if (StoryLibrary.C2Run("short") != null && StoryLibrary.C2Run("public") != null)
                Journal.Add("SURVEY", "THE CHEAP ROAD, SIDE BY SIDE: " + StoryLibrary.C2Run("short") + " | " + StoryLibrary.C2Run("public"));
        }

        /// <summary>The wash on the public road: filled back to the road (dug depth under 15 cm) or surfaced (paving over a
        /// fifth of it).</summary>
        void C2Wash()
        {
            if (Story.Story.StepDone("C2", "decide") || !Story.Story.Flag("c2_dug") || !StoryAnchors.Has("c2_crossing") || !terrain) return;
            var c = StoryAnchors.Get("c2_crossing");
            if (!Story.Story.Flag("c2_filled") && terrain.DugDepth(c.x, c.z) <= 0.15f) { Story.Story.SetFlag("c2_filled"); Story.Story.Note("c2:filled"); }
            if (!Story.Story.Flag("c2_surfaced"))
            {
                int paved = 0;
                for (float x = -3.5f; x <= 3.5f; x += 0.5f)
                for (float z = -3.5f; z <= 3.5f; z += 0.5f)
                    if (x * x + z * z <= 12.25f && terrain.PaveAt(c.x + x, c.z + z) != 0 && terrain.PaveAt(c.x + x, c.z + z) != MadMax.World.DeformableTerrain.PavePothole) paved++;
                if (paved >= 30) { Story.Story.SetFlag("c2_surfaced"); Story.Story.Note("c2:surfaced"); }
            }
        }

        /// <summary>The road is decided: tyres that suit it, the village's first haul contract, Sera's answer, the closing line.</summary>
        void C2Decided(VehicleDriver truck)
        {
            string how = Story.Story.Route("C2", "decide");
            if (how == null || Story.Story.Flag("c2_paid")) return;
            Story.Story.SetFlag("c2_paid");
            string tyre = how.StartsWith("A SEASON'S") ? "wheel_mud" : how.StartsWith("RUN IT") ? "wheel_street" : "wheel_offroad";
            var at = truck ? truck.transform.position + truck.transform.right * 2.8f : StoryAnchors.Get("c2_start");
            for (int i = 0; i < 2; i++)
            {
                var p = at + Vector3.forward * (i * 1.2f); p.y = terrain.Height(p.x, p.z) + 0.7f;
                var part = SpawnPart(tyre, p, Quaternion.Euler(90f, 0f, 0f));
                if (part && !part.GetComponent<Rigidbody>()) part.gameObject.AddComponent<Rigidbody>().mass = part.mass;
            }
            Toast("PRU LEAVES A PAIR OF " + (tyre == "wheel_mud" ? "MUD" : tyre == "wheel_street" ? "ROAD" : "ALL-TERRAIN") + " TYRES BY THE TRIAL TRUCK: YOURS");
            var village = Market.Near(StoryAnchors.Get("c_village")); var market = Market.Near(StoryAnchors.Get("town1"));
            if (village != null && market != null && village != market)
            {
                var c = new Contract
                {
                    id = "C2:HAUL", kind = 3, town = village.index, dest = market.index, need = 2, days = 3, rep = 3,
                    title = "VILLAGE HAUL: 2 CRATES OF PRODUCE TO " + Market.TownName(market) + " (THE " + (how.StartsWith("A SEASON'S") ? "WARDEN'S TRACK" : "PUBLIC ROAD") + ")",
                    reward = 30, chits = 1, faction = (int)Faction.Settlers
                };
                Contracts.Accept(this, c, StoryAnchors.Get("c2_start"));
            }
            ArcCReply("C2", "report", "c2_report", how.StartsWith("A SEASON'S")
                ? "SO THE CHEAP ROAD IS THE WARDEN'S, AT TWENTY-FIVE A SEASON. HONEST, AT LEAST: IT'S ON A RECEIPT. I'LL PIN THE NUMBERS UP AND LET PEOPLE ARGUE."
                : how.StartsWith("RUN IT") ? "ONE TRUCK, TWO TOWNS, HALF THE FUEL EACH. IT'LL BREAK DOWN AT THE WORST MOMENT AND THEY'LL FIX IT TOGETHER. THAT'S THE POINT."
                : "A FREE ROAD THAT HOLDS A LOADED TRUCK. THE WARDEN WILL SULK. I'LL PIN THE NUMBERS UP WHERE THE GUILD CLERK HAS TO WALK PAST THEM.");
            ArcCPayoff("C2", StoryLibrary.C2Payoff());
        }
    }
}
