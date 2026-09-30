using MadMax.Building;
using MadMax.Npc;
using MadMax.Story;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>C5 NO EMPTY SEAT in the world: the convoy yard outside the market with three trucks parked (Isaac's water
    /// pickup, Pru's produce pickup, Tobias's hauler), a Remnant checkpoint on the highway (hut props carrying a
    /// <see cref="Checkpoint"/> boom), Wren Mott's place halfway, the depot at the next town. While it runs: the convoy
    /// forms behind the player's vehicle at the yard (<see cref="PlayerConvoy"/>, the recruited drivers only) and is
    /// re-formed after a load; a third of the way out one truck breaks down (patched: it rejoins; moved off by winch or push:
    /// towed; left: its driver rides along); halfway Wren's cart blocks the road until settled; an arranged checkpoint
    /// lifts its boom for the column; the back track is watched at its bend; the whole column at the depot is the arrival.
    /// The terms chosen with Sera become the arc's conclusion flag, the trucks park and a cargo trailer joins the fleet.</summary>
    public partial class WastelandGame
    {
        static readonly string[] C5Tags = { "c5_v1", "c5_v2", "c5_v3" };
        static readonly string[] C5Drivers = { "c1_driver", "c2_hauler", "c5_tobias" };
        static readonly string[] C5Steps = { "isaac", "pru", "tobias" };
        static readonly string[] C5Names = { "ISAAC", "PRU", "TOBIAS" };
        PlayerConvoy c5Convoy;
        string c5Status;
        float c5StatusAt, c5BrokeAnchorAt;

        partial void Scene_C5()
        {
            if (!Build || !Build.Structures) return;
            ArcCPut("c5_yard", "flag", new Vector3(4f, 0f, -1.5f), 0f);
            ArcCPut("c5_yard", "sign", new Vector3(-3f, 0f, 1.8f), 0f);
            ArcCPut("c5_yard", "barrel", new Vector3(-5.5f, 0f, 1f), 0f);
            ArcCPut("c5_yard", "barrel", new Vector3(-6.2f, 0f, 1.6f), 0f);
            ArcCPut("c5_yard", "tyres", new Vector3(5.5f, 0f, 1.4f), 30f);
            for (int i = 0; i < 3; i++) C5Truck(i, null);
            // the Remnant checkpoint: a hut beside the highway, its boom across it
            ArcCPut("c5_check", "porch_awning", new Vector3(0f, 0f, -1.6f), 0f);
            ArcCPut("c5_check", "sandbag_wall", new Vector3(-2.8f, 0f, 1.2f), 0f);
            ArcCPut("c5_check", "chair", new Vector3(0.8f, 0f, -2.2f), 0f);
            ArcCPut("c5_check", "flag", new Vector3(2.8f, 0f, -1.8f), 0f);
            ArcCPut("c5_check", "sign", new Vector3(0f, 0f, 0.6f), 0f);
            C5Checkpoint();
            // Wren's place halfway, the depot at the far end
            ArcCPut("c5_wren", "barrel", new Vector3(1.5f, 0f, -1.4f), 0f);
            ArcCPut("c5_wren", "bench", new Vector3(-1.4f, 0f, -1.6f), 0f);
            ArcCPut("c5_wren", "crate", new Vector3(2.6f, 0f, -0.4f), 30f);
            ArcCPut("c5_depot", "porch_awning", new Vector3(0f, 0f, -2f), 0f);
            ArcCPut("c5_depot", "table", new Vector3(0.9f, 0f, -1.8f), 0f);
            ArcCPut("c5_depot", "crate", new Vector3(-2.4f, 0f, -2f), 10f);
            ArcCPut("c5_depot", "flag", new Vector3(3.2f, 0f, 1.6f), 0f);
            Journal.Add("PLACE", "THE CONVOY YARD OUTSIDE THE MARKET: THREE TRUCKS AND NO DRIVERS YET. A REMNANT CHECKPOINT HOLDS THE HIGHWAY TO THE NEXT TOWN");
        }

        /// <summary>Truck <paramref name="i"/> of the convoy: parked at the yard (<paramref name="behind"/> null) or, after
        /// a load mid-run, brought back behind the leader as an AI vehicle.</summary>
        VehicleDriver C5Truck(int i, Transform behind)
        {
            string design = i == 2 ? "Hauler" : "Pickup";
            VehicleDriver v;
            if (behind == null) v = ArcCVehicle(design, "c5_yard", new Vector3(i == 0 ? -10.5f : i == 1 ? -4.5f : 4.5f, 0f, -4f), 90f, C5Tags[i],
                                               i == 0 ? "Convoy Water Truck" : i == 1 ? "Convoy Produce Truck" : "Convoy Hauler");
            else
            {
                var back = behind.forward; back.y = 0f; back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.forward;
                var p = behind.position - back * (14f + 13f * i); p.y = terrain.Height(p.x, p.z) + 0.8f;
                v = SpawnAiVehicle(design, p, Quaternion.LookRotation(back));
                if (v) { StoryTag.Set(v.gameObject, C5Tags[i]); v.name = i == 0 ? "Convoy Water Truck" : i == 1 ? "Convoy Produce Truck" : "Convoy Hauler"; }
            }
            if (!v) return null;
            if (v.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = sys.fuelCapacity * 0.9f;
            if (i == 0) { ArcCMount(v, "cargo", "cargo_water_tank"); C5WaterTruck(v, 300f); }
            if (i == 1) ArcCMount(v, "cargo", "cargo_crate");
            var paint = v.GetComponent<VehiclePaint>() ?? v.gameObject.AddComponent<VehiclePaint>();
            paint.colour = i == 0 ? 2 : i == 1 ? 3 : 9; paint.Apply();
            return v;
        }

        static void C5WaterTruck(VehicleDriver v, float litres)
        {
            if (!v) return;
            foreach (var n in v.GetComponentsInChildren<UtilityNode>()) if (n.waterCapacity > 0f) { n.clean = Mathf.Min(n.waterCapacity, litres); n.dirty = 0f; break; }
        }

        /// <summary>The checkpoint's boom on the hut sign (re-added after a load while C5 runs).</summary>
        Checkpoint C5Checkpoint()
        {
            var hut = ArcCProp("sign", "c5_check", 2f);
            if (!hut) return null;
            var cp = hut.GetComponent<Checkpoint>();
            if (!cp)
            {
                cp = hut.gameObject.AddComponent<Checkpoint>();
                var b = StoryAnchors.Get("c5_boom");
                cp.Setup(new Vector2(b.x, b.z), StoryAnchors.Yaw("c5_boom"), propMaterial);
            }
            return cp;
        }

        static void C5Lift(Checkpoint cp, float seconds)
        {
            if (cp) cp.OpenFor(seconds);
        }

        static bool C5Smashed(Checkpoint cp)
        {
            return cp && cp.Broken;
        }

        /// <summary>Past a point on the way to the depot (closer to it than the point is, by <paramref name="margin"/>).</summary>
        static bool C5Past(Vector3 p, string anchor, float margin)
        {
            var dep = StoryAnchors.Get("c5_depot");
            return ArcCFlat(p, dep) < ArcCFlat(StoryAnchors.Get(anchor), dep) - margin;
        }

        partial void Tick_C5()
        {
            if (World != null) StoryCast.ArcCTwin("sera_c5", "sera", World.seed);
            bool rolling = Story.Story.Flag("c5_rolling") && !Story.Story.StepDone("C5", "arrive");
            if (rolling)
            {
                if (c5Convoy == null) C5Form(false);
                c5Convoy.Update(this);
            }
            if (Time.frameCount % 10 != 0) return;
            var cp = C5Checkpoint();
            string route = Story.Story.Route("C5", "route");
            bool back = route != null && route.StartsWith("THE BACK");
            if (back && !Story.Story.Flag("c5_nocp")) { Story.Story.SetFlag("c5_nocp"); Story.Story.Note("c5:no_checkpoint"); }
            string step = ArcCStep("C5");
            // form up at the yard behind the player's vehicle
            if (step == "roll" && Current && ArcCFlat(Current.transform.position, StoryAnchors.Get("c5_yard")) < 45f) C5Form(true);
            if (!rolling || c5Convoy == null) { C5Settle(); return; }
            var lead = Current ? Current.transform.position : Player ? Player.transform.position : Vector3.zero;
            foreach (var m in c5Convoy.members)
                if (m.dropped && !Story.Story.Flag("c5_dropped:" + m.tag))
                {
                    Story.Story.SetFlag("c5_dropped:" + m.tag);                                           // a truck lost on the road stays lost (not respawned)
                    if (!Story.Story.Flag("c5_victim:" + m.tag)) Toast(m.name + "'S TRUCK IS LOST. " + m.name + " RIDES WITH SOMEONE ELSE");
                }
            C5Breakdown(lead);
            C5Dispute(lead);
            // the checkpoint: lifted for an arranged column, bypassed on the back track, or broken
            string arranged = Story.Story.Route("C5", "arrange");
            if (!Story.Story.StepDone("C5", "checkpoint"))
            {
                if (C5Smashed(cp) && !Story.Story.Flag("c5_smashed")) { Story.Story.SetFlag("c5_smashed"); Story.Story.Note("c5:smashed"); }
                if (arranged != null && !arranged.StartsWith("NOT GOING") && ArcCFlat(lead, StoryAnchors.Get("c5_boom")) < 70f)
                {
                    C5Lift(cp, 6f);
                    if (!Story.Story.Flag("c5_lifted")) { Story.Story.SetFlag("c5_lifted"); Toast("CAPTAIN REED COUNTS THE TRUCKS AND LIFTS THE BOOM"); }
                }
                if (back && ArcCFlat(lead, StoryAnchors.Get("c5_back")) < 60f && !Story.Story.Flag("c5_bypassed")) { Story.Story.SetFlag("c5_bypassed"); Story.Story.Note("c5:bypassed"); }
                if (ArcCFlat(lead, StoryAnchors.Get("c5_boom")) < 30f) Story.Story.SetFlag("c5_at_boom");
                bool allPast = C5Past(lead, "c5_boom", 20f);
                foreach (var m in c5Convoy.members) if (!m.dropped && (!m.v || !C5Past(m.v.transform.position, "c5_boom", 20f))) allPast = false;
                if (allPast && !Story.Story.Flag("c5_through")) { Story.Story.SetFlag("c5_through"); Story.Story.Note(Story.Story.Flag("c5_at_boom") ? "c5:through" : "c5:bypassed"); }
            }
            // the whole column at the depot
            var dep = StoryAnchors.Get("c5_depot");
            if (!Story.Story.Flag("c5_arrived") && ArcCFlat(lead, dep) < 45f && c5Convoy.AllWithin(dep, 45f))
            {
                Story.Story.SetFlag("c5_arrived");
                c5Convoy.Release();
                Toast("THE CONVOY IS IN: " + c5Convoy.Count + (c5Convoy.Count == 1 ? " TRUCK" : " TRUCKS") + " AT THE DEPOT, AND THE TOWN COMING OUT TO LOOK");
                Story.Story.Note("c5:arrived");
                return;
            }
            string status = c5Convoy.Status(this);
            if (status != c5Status && Time.time > c5StatusAt) { c5Status = status; c5StatusAt = Time.time + 6f; Toast(status); }
        }

        /// <summary>The recruited drivers' trucks, in order, escorting the player's vehicle; after a load the missing ones
        /// come back behind the leader (a truck broken down and not yet settled comes back broken).</summary>
        void C5Form(bool start)
        {
            if (start && Story.Story.Flag("c5_rolling")) return;
            c5Convoy = new PlayerConvoy();
            for (int i = 0; i < 3; i++)
            {
                if (!Story.Story.StepDone("C5", C5Steps[i])) continue;
                var m = c5Convoy.Add(C5Tags[i], C5Drivers[i], C5Names[i]);
                if (Story.Story.Flag("c5_dropped:" + C5Tags[i])) m.dropped = true;
            }
            var leadT = Current ? Current.transform : Player ? Player.transform : null;
            c5Convoy.Form(this, m =>
            {
                int i = System.Array.IndexOf(C5Tags, m.tag);
                var v = C5Truck(i, leadT);
                if (v && Story.Story.Flag("c5_victim:" + m.tag) && !Story.Story.StepDone("C5", "breakdown")) C5Break(m, v);
                return v;
            });
            foreach (var m in c5Convoy.members)
                if (m.v && Story.Story.Flag("c5_victim:" + m.tag) && !Story.Story.StepDone("C5", "breakdown")) { c5Convoy.Hold(m, true); if (m.ai) m.ai.Release(); }
            if (!start) return;
            Story.Story.SetFlag("c5_rolling");
            // Wren's cart goes across the road halfway
            var cart = ArcCVehicle("CargoTrailer", "c5_dispute", Vector3.zero, 90f, "c5_cart", "Wren's Cart");
            if (cart) cart.handbrake = true;
            SetWaypoint(StoryAnchors.Get("c5_depot"), "THE DEPOT AT THE NEXT TOWN", true);
            Toast("THE CONVOY FORMS UP BEHIND YOU: " + c5Convoy.Count + (c5Convoy.Count == 1 ? " TRUCK" : " TRUCKS") + ". LEAD THEM TO THE NEXT TOWN");
            Story.Story.Note("c5:rolling");
        }

        void C5Break(PlayerConvoy.Member m, VehicleDriver v)
        {
            if (v && v.Engine && v.Engine.TryGetComponent<VehiclePart>(out var e)) e.damage = 0.99f;
        }

        /// <summary>A third of the way out, one truck (the second in line) stops; then it is patched, towed or left.</summary>
        void C5Breakdown(Vector3 lead)
        {
            if (!Story.Story.Flag("c5_broke"))
            {
                if (ArcCFlat(lead, StoryAnchors.Get("c5_break")) > 35f && !C5Past(lead, "c5_break", 0f)) return;
                PlayerConvoy.Member victim = null; int n = 0;
                foreach (var m in c5Convoy.members) if (!m.dropped && m.v && m.v != Current) { n++; if (victim == null || n == 2) victim = m; }
                if (victim == null) return;
                Story.Story.SetFlag("c5_broke");
                Story.Story.SetFlag("c5_victim:" + victim.tag);
                Story.Story.SetFlag("c5_stranded:" + victim.driver);
                C5Break(victim, victim.v);
                c5Convoy.Hold(victim, true);
                if (victim.ai) victim.ai.Release();
                ArcCFlagPoint("c5_break_at", victim.v.transform.position);
                StoryAnchors.ArcCMove("c5_broke", victim.v.transform.position + victim.v.transform.right * 2.6f, victim.v.transform.eulerAngles.y - 90f);
                SetWaypoint(victim.v.transform.position, victim.name + "'S TRUCK HAS STOPPED", true);
                Toast(victim.name + "'S TRUCK COUGHS AND STOPS: PATCH IT (REPAIR KIT), TOW IT, OR LEAVE IT AND TAKE " + victim.name);
                return;
            }
            PlayerConvoy.Member vic = null;
            foreach (var m in c5Convoy.members) if (Story.Story.Flag("c5_victim:" + m.tag)) vic = m;
            if (vic == null) return;
            if (!Story.Story.StepDone("C5", "breakdown"))
            {
                if (vic.v)
                {
                    if (ArcCEngineDamage(vic.v) < 0.9f && !Story.Story.Flag("c5_fixed")) { Story.Story.SetFlag("c5_fixed"); Story.Story.Note("c5:fixed"); }
                    else if (ArcCFlat(vic.v.transform.position, ArcCFlagPoint("c5_break_at")) > 20f && !Story.Story.Flag("c5_towed")) { Story.Story.SetFlag("c5_towed"); Story.Story.Note("c5:towed"); }
                    if (Time.time > c5BrokeAnchorAt) { c5BrokeAnchorAt = Time.time + 2f; StoryAnchors.ArcCMove("c5_broke", vic.v.transform.position + vic.v.transform.right * 2.6f, vic.v.transform.eulerAngles.y - 90f); }
                }
                return;
            }
            if (Story.Story.Flag("c5_brk_done")) return;
            Story.Story.SetFlag("c5_brk_done");
            string how = Story.Story.Route("C5", "breakdown");
            if (how == "PATCHED IT ON THE ROADSIDE") { c5Convoy.Hold(vic, false); Toast(vic.name + "'S TRUCK COUGHS BACK TO LIFE AND FALLS IN"); }
            else
            {
                c5Convoy.Hold(vic, false); c5Convoy.Drop(vic);
                Story.Story.SetFlag("c5_dropped:" + vic.tag);
                Toast(how == "TOWED IT" ? "THE BROKEN TRUCK COMES ALONG ON THE END OF A LINE" : vic.name + " CLIMBS IN WITH YOU. THE TRUCK WAITS ON THE VERGE FOR A TOW");
            }
        }

        /// <summary>Halfway: Wren's cart across the road until it's settled (talked out of the way, hauled off, or driven round).</summary>
        void C5Dispute(Vector3 lead)
        {
            var cartV = ArcCTagged("c5_cart");
            var at = StoryAnchors.Get("c5_dispute");
            if (!Story.Story.Flag("c5_met_wren") && ArcCFlat(lead, at) < 45f) { Story.Story.SetFlag("c5_met_wren"); Toast("A CART ACROSS THE ROAD. WREN MOTT WANTS A WORD WITH WHOEVER'S IN CHARGE"); }
            if (!Story.Story.StepDone("C5", "dispute"))
            {
                if (cartV && ArcCFlat(cartV.transform.position, at) > 7f && !Story.Story.Flag("c5_cleared")) { Story.Story.SetFlag("c5_cleared"); Story.Story.Note("c5:cleared"); }
                else if (C5Past(lead, "c5_dispute", 45f) && !Story.Story.Flag("c5_round")) { Story.Story.SetFlag("c5_round"); Story.Story.Note("c5:round"); }
                return;
            }
            if (Story.Story.Flag("c5_disp_done")) return;
            Story.Story.SetFlag("c5_disp_done");
            string how = Story.Story.Route("C5", "dispute");
            if (how == "DROVE ROUND HER") { Factions.Shift(Faction.Settlers, -3); Toast("WREN MOTT WATCHES THE CONVOY GO ROUND HER. SHE'LL REMEMBER THE NUMBER PLATES"); return; }
            if (how != null && how.StartsWith("OUR WATER"))
            {
                var wt = PlayerConvoy.Tagged("c5_v1");
                if (wt) foreach (var n in wt.GetComponentsInChildren<UtilityNode>()) if (n.waterCapacity > 0f) { n.clean = Mathf.Max(0f, n.clean - 60f); break; }
            }
            if (how != null && how.StartsWith("WE'LL STOP")) Story.Story.SetFlag("c5_eggs");
            if (cartV && ArcCFlat(cartV.transform.position, at) < 7f && cartV.Body)
            {
                var side = Quaternion.Euler(0f, StoryAnchors.Yaw("c5_dispute") + 90f, 0f) * Vector3.forward;
                var p = at + side * 9f; p.y = terrain.Height(p.x, p.z) + 0.6f;
                cartV.Body.position = p; cartV.Body.rotation = Quaternion.Euler(0f, StoryAnchors.Yaw("c5_dispute"), 0f);
                cartV.Body.linearVelocity = Vector3.zero; cartV.Body.WakeUp();
                Toast("WREN'S BOY PUTS HIS SHOULDER TO THE CART AND IT ROLLS INTO THE VERGE");
            }
        }

        /// <summary>After the terms: the arc's conclusion flag, the trucks parked, a cargo trailer for the fleet, the closing line.</summary>
        void C5Settle()
        {
            if (!Story.Story.StepDone("C5", "terms") || Story.Story.Flag("c5_settle_done")) return;
            Story.Story.SetFlag("c5_settle_done");
            string end = StoryLibrary.C5Conclusion();
            if (end != null) Story.Story.SetFlag(end);
            Story.Story.SetFlag("arc_c_done");
            Story.Story.SetFlag("c5_route_open");
            if (c5Convoy != null) c5Convoy.Release();
            var trailer = ArcCVehicle("CargoTrailer", "c5_depot", new Vector3(-7f, 0f, -3f), 90f, "c5_trailer", "Co-op Cargo Trailer");
            if (trailer) { fleet.Add(trailer); Toast("SERA: THE CARGO TRAILER IS YOURS. THE ROUTE WILL WANT IT."); }
            Factions.Shift(Faction.Settlers, 5);
            Factions.Shift(Faction.Nomads, 4, false);
            Journal.Add("STORY", "THE ROUTE TO THE NEXT TOWN IS OPEN: CONVOY TRUCKS PARK AT ITS DEPOT, AND " + (end == "arc_c_concession" ? "THE GUILD RUNS IT ON PUBLISHED TERMS"
                : end == "arc_c_coop" ? "A COOPERATIVE OF THE TOWNS ON IT RUNS IT" : "EACH TOWN ON IT KEEPS ITS OWN DEAL"));
            ArcCPayoff("C5", StoryLibrary.C5Payoff());
            Story.Story.Note("c5:settled");
        }

        /// <summary>A world point kept in the story flags (arc C: where a truck broke down), to the metre.</summary>
        static void ArcCFlagPoint(string key, Vector3 p)
        {
            ArcCRecord.Put(key + ":x", Mathf.RoundToInt(p.x + 100000f));
            ArcCRecord.Put(key + ":z", Mathf.RoundToInt(p.z + 100000f));
        }

        static Vector3 ArcCFlagPoint(string key) => new Vector3(ArcCRecord.Get(key + ":x") - 100000f, 0f, ArcCRecord.Get(key + ":z") - 100000f);
    }
}
