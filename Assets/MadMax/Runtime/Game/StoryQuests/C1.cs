using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>C1 WATER HAS NO FLAG in the world: the Guild toll post (awning, table, sandbags, a flag), Isaac's water
    /// bowser (a TankerSmall) on the verge with a weeping outlet valve holding the shipment (a water valve piece carrying
    /// the bowser's 380 L of silty water, so the test kit reads litres and quality at the back of the bowser), and his
    /// pickup to tow it. While it runs: the kit's reading at the valve gives the test slip; the valve weeps (down to a
    /// level, never empty) until it is repaired in build mode; moving the bowser off before it's settled is taking it (the
    /// Guild remembers); the salt road's cairn is watched; the drop at the village records the litres and hands over a
    /// spare water tank and the route map.</summary>
    public partial class WastelandGame
    {
        const float C1Litres = 380f, C1Floor = 230f, C1Leak = 0.05f;
        Placeable c1Valve;
        float c1Drip;

        partial void Scene_C1()
        {
            if (!Build || !Build.Structures) return;
            ArcCPut("c1_toll", "porch_awning", new Vector3(0f, 0f, -1.6f), 0f);
            ArcCPut("c1_toll", "table", new Vector3(0.9f, 0f, -1.3f), 0f);
            ArcCPut("c1_toll", "chair", new Vector3(0.9f, 0f, -2.4f), 0f);
            ArcCPut("c1_toll", "sign", new Vector3(-2.4f, 0f, 1.8f), 0f);
            ArcCPut("c1_toll", "sandbag_wall", new Vector3(3.2f, 0f, 0.8f), 90f);
            ArcCPut("c1_toll", "barrel", new Vector3(-2.8f, 0f, -1.9f), 0f);
            ArcCPut("c1_toll", "flag", new Vector3(3.6f, 0f, -2.4f), 0f);

            // Isaac's bowser on the verge, his pickup ahead of it, both facing along the road
            var bowser = ArcCVehicle("TankerSmall", "c1_rig", new Vector3(-9f, 0f, 3.8f), 90f, "c1_bowser", "Isaac's Water Bowser");
            if (bowser)
            {
                if (bowser.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = 0f; sys.tankKind = ResourceType.None; }
                var valve = FurnitureLibrary.Spawn("water_valve", bowser.transform, new Vector3(0f, 0.62f, -2.05f), Quaternion.Euler(-90f, 0f, 0f), propMaterial);
                if (valve)
                {
                    storyProps.Add(valve.Id);
                    valve.hits = 1;
                    if (valve.TryGetComponent<UtilityNode>(out var n)) { n.waterCapacity = 450f; n.dirty = C1Litres; n.clean = 0f; n.taint = WaterTaint.Silt; }
                    valve.Dirty();
                    c1Valve = valve;
                }
            }
            var truck = ArcCVehicle("Pickup", "c1_rig", new Vector3(-3.2f, 0f, 3.8f), 90f, "c1_truck", "Isaac's Pickup");
            if (truck && truck.TryGetComponent<VehicleSystems>(out var ts)) ts.fuel = Mathf.Min(ts.fuelCapacity, 22f);
            Journal.Add("PLACE", "THE GUILD TOLL POST ON THE VILLAGE ROAD: ISAAC DUNE'S WATER BOWSER HELD ON THE VERGE");
        }

        /// <summary>The bowser's valve (the shipment's water), found again after a load.</summary>
        Placeable C1Valve()
        {
            if (c1Valve) return c1Valve;
            var b = ArcCTagged("c1_bowser");
            if (!b) return null;
            foreach (var p in b.GetComponentsInChildren<Placeable>()) if (p.id == "water_valve") return c1Valve = p;
            return null;
        }

        static float C1Water(Placeable valve) => valve && valve.TryGetComponent<UtilityNode>(out var n) ? n.Water : 0f;

        partial void Tick_C1()
        {
            var valve = C1Valve();
            var bowser = ArcCTagged("c1_bowser");
            if (valve && valve.TryGetComponent<UtilityNode>(out var node))
            {
                if (node.waterCapacity < 450f) node.waterCapacity = 450f;                               // the valve piece's own setup holds none
                var def = FurnitureLibrary.Get("water_valve");
                bool weeping = def != null && valve.hits < def.hits;
                if (weeping && node.dirty > C1Floor)
                {
                    node.dirty = Mathf.Max(C1Floor, node.dirty - C1Leak * Time.deltaTime);
                    if (Player && ArcCFlat(Player.transform.position, valve.transform.position) < 60f && (c1Drip -= Time.deltaTime) <= 0f)
                    {
                        c1Drip = 0.35f;
                        if (MadMax.World.DebrisSystem.Instance)
                            MadMax.World.DebrisSystem.Instance.EmitPuff(valve.transform.position + Vector3.down * 0.1f, new Color32(120, 150, 170, 255), 0.05f, Vector3.down * 1.5f, 0.5f);
                    }
                }
                if (!weeping && !Story.Story.Flag("c1_valve_ok")) { Story.Story.SetFlag("c1_valve_ok"); Story.Story.Note("c1:valve"); Toast("THE VALVE HOLDS: THE BOWSER STOPS WEEPING"); }
                // the kit's reading at the valve: the slip, and the claim beside it in the journal
                if (!Story.Story.Flag("c1_tested") && waterSamples.ContainsKey(valve.Id))
                {
                    Story.Story.SetFlag("c1_tested");
                    using (Inventory.Source("MEASURED")) Inventory.AddItem(StoryLibrary.C1Slip);
                    Journal.Add("WATER", "ISAAC'S BOWSER, MEASURED: " + Mathf.RoundToInt(node.Water) + " L, " + WaterQuality.Words(WaterQuality.TaintOf(node)) +
                                         ". THE TOLL BOOK CLAIMS 600 L, FOUL.");
                    Story.Story.Note("c1:tested");
                }
            }

            if (Time.frameCount % 15 != 0) return;
            var rig = StoryAnchors.Get("c1_rig");
            // taking it: the bowser away from the verge (or the keeper down) before anything was settled
            if (!Story.Story.StepDone("C1", "resolve") && !Story.Story.Flag("c1_taken")
                && ((bowser && ArcCFlat(bowser.transform.position, rig) > 50f) || NpcRegistry.IsDead("cast:c1_keeper")))
            {
                Story.Story.SetFlag("c1_taken");
                Story.Story.Note("c1:taken");
            }
            string how = Story.Story.Route("C1", "resolve");
            if (how != null && !Story.Story.Flag("c1_settled"))
            {
                Story.Story.SetFlag("c1_settled");
                if (how.StartsWith("YOUR BOOK")) { Factions.Shift(Faction.FuelGuild, 2); Factions.Shift(Faction.Nomads, 5); Toast("THE KEEPER WRITES 'MEASURED' IN THE MARGIN: THE BOWSER IS FREE"); }
                else if (how.StartsWith("THE OLD SALT")) { Factions.Shift(Faction.Nomads, 8, false); Toast("THE SALT ROAD: OFF THE GUILD ROAD BY THE OLD CAIRN (ON YOUR MAP)"); }
                else if (how.StartsWith("FINE. SIXTY")) { Factions.Shift(Faction.Nomads, 3, false); Story.Story.SetFlag("c1_paid_claim"); }
                else
                {
                    Factions.Shift(Faction.FuelGuild, -10); Factions.Shift(Faction.Nomads, 2, false); Story.Story.SetFlag("c1_theft");
                    Toast("YOU TOOK THE BOWSER WITHOUT THE KEEPER'S SAY-SO: THE TOLL BOOK HAS YOUR NAME IN IT NOW");
                    Journal.Add("STORY", "THE GUILD TOLL POST WROTE YOUR NAME IN ITS BOOK: THE FUEL GUILD TRUSTS YOU LESS");
                }
            }
            // the salt road: the cairn, then the village
            if (how != null && how.StartsWith("THE OLD SALT") && bowser && !Story.Story.Flag("c1_via_salt"))
            {
                if (ArcCFlat(bowser.transform.position, StoryAnchors.Get("c1_salt")) < 45f) { Story.Story.SetFlag("c1_via_salt"); Toast("THE OLD CAIRN: YOU'RE ON THE SALT ROAD"); }
                else if (ArcCStep("C1") == "deliver" && !Story.Story.Flag("c1_salt_way")) { Story.Story.SetFlag("c1_salt_way"); SetWaypoint(StoryAnchors.Get("c1_salt"), "THE OLD SALT ROAD'S CAIRN", true); }
            }
            // at the village: the litres, the spare tank, the route map
            if (Story.Story.StepDone("C1", "deliver") && !Story.Story.Flag("c1_delivered"))
            {
                Story.Story.SetFlag("c1_delivered");
                int litres = Mathf.RoundToInt(C1Water(valve));
                ArcCRecord.Put("c1:litres", litres);
                if (how != null && how.StartsWith("THE OLD SALT") && !Story.Story.Flag("c1_via_salt"))
                {
                    Factions.Shift(Faction.FuelGuild, -3);
                    Toast("IT CAME DOWN THE GUILD ROAD AFTER ALL: THE KEEPER WRITES IT DOWN");
                }
                var d = StoryAnchors.Get("c1_dest") + Quaternion.Euler(0f, StoryAnchors.Yaw("c1_dest"), 0f) * new Vector3(2.5f, 0f, -1.5f);
                d.y = terrain.Height(d.x, d.z) + 0.6f;
                var tank = SpawnPart("cargo_water_tank", d, Quaternion.Euler(0f, StoryAnchors.Yaw("c1_dest"), 0f));
                if (tank && !tank.GetComponent<Rigidbody>()) tank.gameObject.AddComponent<Rigidbody>().mass = tank.mass;
                C1RouteMap();
                ArcCPayoff("C1", StoryLibrary.C1Payoff(litres));
                Toast(litres + " L DELIVERED. ISAAC'S SPARE WATER TANK SITS BY THE CISTERN: IT'S YOURS ([E] TO TAKE, A CARGO SOCKET TAKES IT)");
            }
        }

        /// <summary>The accurate route map Isaac hands over: every settlement within 3 km of the market and the checkpoints on those roads.</summary>
        void C1RouteMap()
        {
            var m = StoryAnchors.Get("town1"); int n = 0;
            foreach (var st in World.settlements)
                if (Vector2.Distance(st.pos, new Vector2(m.x, m.z)) < 3000f && Discovered.Add("town:" + st.index)) n++;
            foreach (var mk in MadMax.World.BiomeProps.Landmarks(World))
                if (mk.kind == MadMax.World.BiomeProps.MarkKind.Checkpoint && Vector2.Distance(mk.pos, new Vector2(m.x, m.z)) < 3000f && Discovered.Add("lm:" + mk.index)) n++;
            Journal.Add("PLACE", "ISAAC'S ROUTE MAP: THE TOWNS AND CHECKPOINTS WITHIN 3 KM OF THE MARKET ARE ON YOUR MAP" + (n > 0 ? " (" + n + " NEW)" : ""));
        }
    }
}
