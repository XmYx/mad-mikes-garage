using MadMax.Items;
using MadMax.Npc;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>C4 A BRIDGE YOU CAN AFFORD in the world: road-closed signs either side of the crossing and Greta's yard
    /// beside the road with her digger and a tipper loaded with rubble; once the chapter is taken on, the washout itself
    /// (a trench about 1.4 m deep across the road, or the ford scoured that much deeper where a river crosses). While it runs: the washout is watched for fill (back
    /// within 35 cm of the road, or a causeway above the water at a ford), the loaded tipper for a crossing from one side
    /// to the other over the middle, the ferry for the tipper parked at the bank, and, when the machines are due back,
    /// their place, fuel and engines against Greta's terms (a toast says what's missing). The sign goes up at the end.</summary>
    public partial class WastelandGame
    {
        const float C4Depth = 1.4f;
        char c4Side;
        bool c4Mid, c4HasPrev;
        Vector3 c4Prev;
        float c4Nag;

        partial void Scene_C4()
        {
            if (!Build || !Build.Structures) return;
            ArcCPut("c4_yard", "porch_awning", new Vector3(0f, 0f, -2.2f), 0f);
            ArcCPut("c4_yard", "table", new Vector3(0.8f, 0f, -1.9f), 0f);
            ArcCPut("c4_yard", "barrel", new Vector3(-2.8f, 0f, -2.1f), 0f);
            ArcCPut("c4_yard", "barrel", new Vector3(-3.6f, 0f, -1.2f), 0f);
            ArcCPut("c4_yard", "tyres", new Vector3(3f, 0f, -2.6f), 15f);
            ArcCPut("c4_yard", "sign", new Vector3(-1.6f, 0f, 2.2f), 0f);
            var dig = ArcCVehicle("Excavator", "c4_yard", new Vector3(-7.5f, 0f, -3f), 0f, "c4_digger", "Road Crew Excavator");
            var tip = ArcCVehicle("DumpTruck", "c4_yard", new Vector3(7f, 0f, -3.5f), 0f, "c4_tipper", "Road Crew Tipper");
            foreach (var v in new[] { dig, tip })
                if (v && v.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = sys.fuelCapacity * 0.7f;
            C4Load(tip);
            if (StoryAnchors.Has("c4_ferry"))
            {
                ArcCPut("c4_ferry", "post", new Vector3(-1.2f, 0f, 1.5f), 0f);
                ArcCPut("c4_ferry", "barrel", new Vector3(1.4f, 0f, -1f), 0f);
            }
            Journal.Add("PLACE", "THE CROSSING ON THE PUBLIC ROAD IS WASHED OUT. GRETA HOLM'S ROAD CREW WAITS BESIDE IT WITH A DIGGER AND A LOADED TIPPER");
        }

        void C4Load(VehicleDriver tip)
        {
            if (!tip || Story.Story.Flag("c4_loaded") || !tip.TryGetComponent<Machine>(out var m) || !m.Store) return;
            Story.Story.SetFlag("c4_loaded");
            m.Store.inventory.Add(ResourceType.Rubble, 30);
        }

        /// <summary>The washout is gone: filled back near the road's level, or (at a ford) raised above the water.</summary>
        bool C4Filled()
        {
            if (!terrain || !StoryAnchors.Has("c4_crossing")) return false;
            var c = StoryAnchors.Get("c4_crossing");
            float water = terrain.WaterLevel(c.x, c.z);
            if (StoryAnchors.Has("c4_ferry") && !float.IsNaN(water)) return terrain.Height(c.x, c.z) >= water + 0.1f;
            return terrain.DugDepth(c.x, c.z) <= 0.35f;
        }

        partial void Tick_C4()
        {
            // the storm's washout: dug when the crossing becomes the player's problem (not before: an unaccepted chapter
            // doesn't cut the road for everyone)
            if (!Story.Story.Flag("c4_dug") && StoryAnchors.Has("c4_crossing"))
            {
                Story.Story.SetFlag("c4_dug");
                ArcCTrench(StoryAnchors.Get("c4_crossing"), StoryAnchors.Yaw("c4_crossing"), 12f, C4Depth, 3.2f);
                ArcCPut("c4_near", "sign", new Vector3(5f, 0f, -3f), -90f);
                ArcCPut("c4_far", "sign", new Vector3(-5f, 0f, 3f), 90f);
                Toast(StoryAnchors.Has("c4_ferry") ? "THE FORD ON THE PUBLIC ROAD IS SCOURED TOO DEEP TO WADE" : "THE CROSSING ON THE PUBLIC ROAD IS WASHED OUT: A GULLY WHERE THE CULVERT WAS");
            }
            if (Time.frameCount % 15 != 0) return;
            var dig = ArcCTagged("c4_digger"); var tip = ArcCTagged("c4_tipper");
            C4Load(tip);
            if (!Story.Story.StepDone("C4", "fix") && !Story.Story.Flag("c4_filled") && C4Filled()) { Story.Story.SetFlag("c4_filled"); Story.Story.Note("c4:filled"); }
            string step = ArcCStep("C4");
            if (step == "test" && tip) C4Test(tip);
            if (step == "return") C4Return(dig, tip);
            if (Story.Story.StepDone("C4", "credit") && !Story.Story.Flag("c4_signed"))
            {
                Story.Story.SetFlag("c4_signed");
                ArcCPut("c4_near", "sign_direction", new Vector3(-5f, 0f, -2f), 90f);
                ArcCPut("c4_far", "sign_direction", new Vector3(5f, 0f, 2f), -90f);
                ArcCPut("c4_yard", "flag", new Vector3(2.4f, 0f, 1.8f), 0f);
                Story.Story.SetFlag("c4_rental");
                string credit = Story.Story.Route("C4", "credit");
                Journal.Add("PLACE", "THE ROAD-WORK SIGN AT THE CROSSING: " + (credit == null ? "" : credit.StartsWith("CREDIT THE ROAD") ? "\"REBUILT BY THE ROAD CREW\""
                    : credit.StartsWith("CREDIT EVERYONE") ? "\"REBUILT BY EVERYONE WHO HELPED (AND A GOAT)\"" : "\"REBUILT BY A MECHANIC FROM THE BEND\""));
                Factions.Shift(Faction.Settlers, 3);
                ArcCPayoff("C4", StoryLibrary.C4Payoff());
                Story.Story.Note("c4:settled");
            }
        }

        /// <summary>The loaded tipper from one side of the crossing to the other, over its middle (not round it).</summary>
        void C4Test(VehicleDriver tip)
        {
            string fix = Story.Story.Route("C4", "fix");
            bool loaded = tip.TryGetComponent<Machine>(out var m) && m.BedUnits >= 8;
            var p = tip.transform.position;
            if (fix != null && fix.StartsWith("RUN YOUR FERRY"))
            {
                if (loaded && (ArcCFlat(p, StoryAnchors.Get("c4_near")) < 16f || ArcCFlat(p, StoryAnchors.Get("c4_far")) < 16f) && !Story.Story.Flag("c4_ferried"))
                {
                    Story.Story.SetFlag("c4_ferried");
                    Toast("OLD LUCAS TAKES THE TIPPER'S LOAD OVER IN THREE TRIPS AND BRINGS THE SACKS BACK EMPTY");
                    Story.Story.Note("c4:ferried");
                }
                return;
            }
            char side = ArcCFlat(p, StoryAnchors.Get("c4_near")) < 8f ? 'N' : ArcCFlat(p, StoryAnchors.Get("c4_far")) < 8f ? 'F' : '\0';
            // over the middle since the last sample (the tick is throttled: at low frame rates a truck can pass the middle between samples)
            var mid = StoryAnchors.Get("c4_crossing");
            var a0 = c4HasPrev ? c4Prev : p; var ab = p - a0; ab.y = 0f;
            float tt = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(new Vector3(mid.x - a0.x, 0f, mid.z - a0.z), ab) / ab.sqrMagnitude) : 0f;
            if (ArcCFlat(a0 + ab * tt, mid) < 4.5f) c4Mid = true;
            c4Prev = p; c4HasPrev = true;
            if (side == '\0') return;
            if (c4Side != '\0' && side != c4Side && c4Mid)
            {
                if (loaded && !Story.Story.Flag("c4_tested")) { Story.Story.SetFlag("c4_tested"); Toast("A LOADED TIPPER ACROSS AND NOTHING GAVE WAY"); Story.Story.Note("c4:tested"); }
                else if (!loaded) Toast("ACROSS, BUT THE TIPPER'S EMPTY: THE TEST NEEDS A LOAD (THE DIGGER FILLS THE BED)");
            }
            c4Side = side; c4Mid = false;
        }

        /// <summary>Greta's terms: both machines back at the yard, half a tank each, engines no worse than a scratch.</summary>
        void C4Return(VehicleDriver dig, VehicleDriver tip)
        {
            var yard = StoryAnchors.Get("c4_yard");
            string missing = null;
            foreach (var (v, name) in new[] { (dig, "DIGGER"), (tip, "TIPPER") })
            {
                if (!v) { missing = "THE " + name + " IS GONE: PAY GRETA FOR THE WEAR INSTEAD"; break; }
                if (ArcCFlat(v.transform.position, yard) > 18f) { missing = missing ?? "THE " + name + " ISN'T BACK AT THE YARD"; continue; }
                if (ArcCFuel(v) < 0.45f) { missing = missing ?? "THE " + name + " NEEDS FUEL: " + Mathf.RoundToInt(ArcCFuel(v) * 100f) + "% ([G] SERVICE, HALF A TANK)"; continue; }
                if (ArcCEngineDamage(v) > 0.3f) missing = missing ?? "THE " + name + "'S ENGINE IS WORSE THAN YOU FOUND IT: PATCH IT (REPAIR KIT)";
            }
            if (missing == null) { if (!Story.Story.Flag("c4_returned")) { Story.Story.SetFlag("c4_returned"); Toast("GRETA WALKS ROUND BOTH MACHINES AND NODS"); Story.Story.Note("c4:returned"); } return; }
            if (Player && ArcCFlat(FocusPos, yard) < 25f && Time.time > c4Nag) { c4Nag = Time.time + 12f; Toast("GRETA: " + missing); }
        }
    }
}
