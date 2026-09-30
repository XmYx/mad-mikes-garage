using MadMax.Building;
using MadMax.Items;
using MadMax.Story;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>A5 NO ONE RIDES IN THE BACK: the Guild transfer yard (the box trailer the convoy sits in, a broken
    /// transfer pump, the crew hut where Wes lies, the overseer's table, the escort's pickup), the staging point with
    /// Otis's bus, and the transfer operation itself: Mara's list (seats that aren't a truck bed, water, medicine) read
    /// live, the player's go at the trailer door, up to two trips, a hired bus, the arrival at the garage.</summary>
    public partial class WastelandGame
    {
        Operation a5Op;
        bool a5ToStaging;
        int a5Release = -2;                                                                  // the day the rest walk free (-1 none, -2 not looked up)
        const int A5BusSeats = 8;

        /// <summary>Passenger seats a vehicle offers (the driver not counted; no truck beds, no trailers: nobody rides
        /// in the back).</summary>
        internal static int Q7Seats(VehicleDriver v)
        {
            if (!v || !v.driveable || v.GetComponent<Machine>()) return 0;
            string n = v.name;
            if (n.StartsWith("Bus")) return 12;
            if (v.GetComponent<InteriorSpace>()) return 6;
            if (v.GetComponent<BikeBalance>()) return v.transform.Find("PassengerEye") ? 1 : 0;
            if (v.GetComponent<FlightModel>()) return 1;
            foreach (var two in new[] { "Pickup", "TowTruck", "Wrecker", "Tractor", "Hauler", "Semi", "DumpTruck", "Coupe", "Interceptor", "DuneBuggy", "MonsterTruck", "Peugeot207CC", "Scavenger", "Fiat500" })
                if (n.StartsWith(two)) return 1;
            return 3;
        }

        int A5People => (Story.Story.Route("A5", "plan") == StoryLibrary.A5Group ? 3 : 6) - (Story.Story.Route("A5", "wes") == StoryLibrary.A5Medic ? 1 : 0);
        bool A5Hired => Story.Story.StepDone("A5", "hire");
        bool A5Sneaking => Story.Story.Route("A5", "way") == StoryLibrary.A5Sneak;
        static bool A5Dark => DayNight.Darkness > 0.3f;

        static float Q7Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>The car that carries them: the one tagged at the go, else the player's (or the roomiest) fleet vehicle
        /// at the yard or the staging point.</summary>
        VehicleDriver A5Transport()
        {
            var tag = StoryTag.Find("a5_riders");
            if (tag && tag.TryGetComponent<VehicleDriver>(out var tagged)) return tagged;
            VehicleDriver best = null; int bs = 0;
            foreach (var v in fleet)
            {
                if (!v) continue;
                var p = v.transform.position;
                bool near = (StoryAnchors.Has("a5_yard") && Q7Flat(p, StoryAnchors.Get("a5_yard")) < 60f) || (StoryAnchors.Has("a5_staging") && Q7Flat(p, StoryAnchors.Get("a5_staging")) < 30f);
                if (!near) continue;
                int s = Q7Seats(v) + (v == Current ? 100 : 0);                                 // the one you're in first
                if (s > bs) { bs = s; best = v; }
            }
            return best;
        }

        int A5Capacity() => Q7Seats(A5Transport()) * 2 + (A5Hired ? A5BusSeats : 0);    // two trips at most, plus the bus

        int A5Water()
        {
            int w = Inventory.Get(ResourceType.Water);
            var t = A5Transport();
            if (t) foreach (var c in t.GetComponentsInChildren<Container>()) w += c.inventory.Get(ResourceType.Water);
            return w;
        }

        static readonly string[] A5Meds = { "med_firstaid", "med_bandage", "med_splint", "med_disinfectant", "med_poultice", "med_antibiotics", "med_painkillers", "med_pills" };

        int A5Medicine()
        {
            int Count(Inventory inv) { int n = 0; foreach (var id in A5Meds) n += inv.GetItem(id) * (id == "med_firstaid" ? 3 : 1); return n; }
            int m = Count(Inventory);
            var t = A5Transport();
            if (t) foreach (var c in t.GetComponentsInChildren<Container>()) m += Count(c.inventory);
            return m;
        }

        Operation A5Op() => a5Op ??= new Operation("a5", "THE TRANSFER")
            .Needs("SEATS", "", () => Mathf.Min(A5People, A5Capacity()), () => A5People)
            .Needs("WATER", " L", A5Water, () => A5People * 2)
            .Needs("MEDICINE", "", A5Medicine, () => (A5People + 1) / 2);

        partial void Scene_A5()
        {
            if (!StoryAnchors.Has("a5_yard") || !Build || !Build.Structures) return;
            Journal.Add("RADIO", "A HANDWRITTEN NOTE WRAPPED ROUND A SPANNER, LEFT AT NELL'S: \"TRANSFER YARD. NEXT FULL TANKER. SIX OF US. T.\"");
            // the box trailer the convoy sits in, its rear door at the anchor
            var pf = PrefabFor("BoxTrailer") ?? PrefabFor("CargoTrailer");
            if (pf)
            {
                float yy = StoryAnchors.Yaw("a5_yard") - 90f;
                var fwd = Quaternion.Euler(0f, yy, 0f) * Vector3.forward;
                var c = StoryAnchors.Get("a5_trailer") + fwd * (HalfExtents(pf).z + 0.8f);
                c.y = terrain.HeightNoLoad(c.x, c.z) + 1f;
                var tr = Instantiate(pf, c, Quaternion.Euler(0f, yy, 0f)).GetComponent<VehicleDriver>();
                tr.name = pf.name;
                Register(tr, null);
                StoryTag.Set(tr.gameObject, "a5_trailer");
                var paint = tr.GetComponent<VehiclePaint>() ?? tr.gameObject.AddComponent<VehiclePaint>();
                paint.colour = 11; paint.Apply();
            }
            PutAt("a5_trailer", "post", new Vector3(0.8f, 0f, 1.4f), 0f);                     // the door latch (the go)
            // the tanker bay: an empty stand and a broken transfer pump
            var pump = PutAt("a5_yard", "fuel_pump", new Vector3(-2f, 0f, 3.5f), 0f);
            if (pump) { pump.hits = 3; pump.Dirty(); Story.Story.SetFlag("a5:pump_broken"); }
            PutAt("a5_yard", "barrel", new Vector3(-4f, 0f, 3.2f), 0f);
            PutAt("a5_yard", "barrel", new Vector3(-4.8f, 0f, 2.4f), 0f);
            PutAt("a5_yard", "sign", new Vector3(0f, 0f, 6.5f), 0f);
            Q7Vehicle("Pickup", "a5_yard", new Vector3(10f, 0f, 3f), 90f, "a5_escort_car", 11);
            // the crew hut where Wes lies, the overseer's table
            PutAt("a5_hut", "porch_awning", Vector3.zero, 0f);
            PutAt("a5_hut", "bed", new Vector3(-0.6f, 0f, 0f), 90f);
            PutAt("a5_hut", "crate", new Vector3(1.3f, 0f, -0.8f), 0f);
            PutAt("a5_hut", "lamp", new Vector3(1.3f, 0f, 0.8f), 0f);
            PutAt("a5_office", "table", Vector3.zero, 0f);
            PutAt("a5_office", "chair", new Vector3(0f, 0f, -1.1f), 0f);
            // the staging point: an old weigh stop, a fire, Otis's bus
            if (StoryAnchors.Has("a5_staging"))
            {
                PutAt("a5_staging", "porch_awning", new Vector3(0f, 0f, -2f), 0f);
                PutAt("a5_staging", "bench", new Vector3(-1.6f, 0f, -2f), 90f);
                PutAt("a5_staging", "campfire", new Vector3(2.4f, 0f, 1f), 0f);
                PutAt("a5_staging", "barrel", new Vector3(-2.6f, 0f, 0.6f), 0f);
                Q7Vehicle("Bus", "a5_staging", new Vector3(-9f, 0f, 3f), 90f, "a5_bus");
            }
            Journal.Add("PLACE", "THE GUILD TRANSFER YARD: A BOX TRAILER WITH PEOPLE IN IT, AN EMPTY TANKER BAY, A HUT, AN OVERSEER");
        }

        partial void Tick_A5()
        {
            var q = StoryLibrary.Get("A5");
            if (q == null) return;
            q.payoff = StoryLibrary.A5_Payoff(Story.Story.Route("A5", "plan"), Story.Story.Route("A5", "way"));
            // the trailer door: the player's own "go" (re-attached after a reload)
            if (Time.frameCount % 20 == 0)
            {
                var door = Q7Prop("a5_trailer", "post", new Vector3(0.8f, 0f, 1.4f), 2.5f);
                if (door && !door.GetComponent<StoryControl>()) StoryControl.On(door.gameObject, "a5_door", A5DoorPrompt, A5DoorUse);
            }
            A5Poses();
            if (Time.frameCount % 15 != 0) return;
            var cur = Story.Story.Current(q);
            string step = cur != null ? cur.id : null;
            // what the routes cost or hand over
            if (Story.Story.Route("A5", "wes") == StoryLibrary.A5Kit && !Story.Story.Flag("a5:kit_used")) { Story.Story.SetFlag("a5:kit_used"); Inventory.TakeItem("med_firstaid"); }
            string planItem = Story.Story.Route("A5", "plan") == StoryLibrary.A5Group ? StoryLibrary.A5PlanGroup : StoryLibrary.A5PlanAll;
            if (Story.Story.StepDone("A5", "plan") && !Story.Story.Flag("a5:plan_given"))
            {
                Story.Story.SetFlag("a5:plan_given");
                Inventory.AddItem(planItem);
            }
            // lost papers come back while they still matter (Mara keeps a copy of her list, Prue signs another sheet)
            if (!Story.Story.StepDone("A5", "way"))
            {
                if (Story.Story.Flag("a5:plan_given") && Inventory.GetItem(planItem) == 0) Inventory.AddItem(planItem);
                if (Story.Story.StepDone("A5", "pump") && Inventory.GetItem(StoryLibrary.A5Worksheet) == 0) Inventory.AddItem(StoryLibrary.A5Worksheet);
            }
            if (Story.Story.Flag("a5:pump_broken") && !Story.Story.StepDone("A5", "pump"))
            {
                var pump = Q7Prop("a5_yard", "fuel_pump", new Vector3(-2f, 0f, 3.5f), 3f);
                if (pump && pump.hits >= pump.MaxHits) Story.Story.Note("a5:pump");
            }
            if (step == "wes" && Q7Built("a5_staging", "bed|clinic_bed", 12f) > 0) Story.Story.Note("a5:shelter");
            if (step == "way")
            {
                var me = Player.transform.position;
                if (A5Dark && !Current && (Player.Crouching || Player.crouch) && StoryAnchors.Has("a5_trailer") && Q7Flat(me, StoryAnchors.Get("a5_trailer")) < 2.8f)
                {
                    Story.Story.Note("a5:sneak");
                    Toast("YOU LIFT THE LATCH AN INCH. NOBODY STIRS: THE ESCORT SLEEPS IN THE CAB");
                }
                if (A5EscortDown()) Story.Story.Note("a5:escort_down");
            }
            if (step == "ready")
            {
                var op = A5Op();
                cur.text = "GET READY (MARA'S LIST): " + op.Checklist() + (A5Sneaking ? "  (GO AFTER DARK)" : "");
                if (op.Ready || Story.Story.Flag("a5:started")) Story.Story.Note("a5:ready");
            }
            if (step == "go") cur.text = "WHEN YOU SAY SO: [E] AT THE TRAILER DOOR  (" + A5Op().Checklist() + ")";
            A5Deliver(cur);
        }

        /// <summary>Wes lies on the hut's cot (or the clinic bed you built there, or the staging shelter's bed); Mara stands
        /// at the trailer door.</summary>
        void A5Poses()
        {
            var wes = CastBody("a5_wes");
            string wesRoute = Story.Story.Route("A5", "wes");
            bool onClinic = wes && wes.Berthed && wes.Berthed.parent && wes.Berthed.parent.TryGetComponent<Placeable>(out var lying) && lying.id == "clinic_bed";
            if (wes && (!wes.Berthed || (wesRoute == StoryLibrary.A5Clinic && !onClinic)) && Time.frameCount % 10 == 0)
            {
                if (wes.Berthed) wes.Unberth();
                string at = StoryAnchors.Has("a5_staging") && wesRoute == StoryLibrary.A5Shelter ? "a5_staging" : "a5_hut";
                Placeable bed = null; float bd = 144f;
                foreach (var p in Placeable.All)
                {
                    if (!p || (p.id != "bed" && p.id != "clinic_bed")) continue;
                    var d = p.transform.position - StoryAnchors.Get(at); d.y = 0f;
                    float s = d.sqrMagnitude - (p.id == "clinic_bed" ? 50f : 0f);
                    if (s < bd) { bd = s; bed = p; }
                }
                if (bed)
                {
                    var cot = bed.transform.Find("Q7Cot");
                    if (!cot) { cot = new GameObject("Q7Cot").transform; cot.SetParent(bed.transform, false); cot.localPosition = ClinicBed.Spot; }
                    wes.Berth(cot, bed.transform.TransformPoint(new Vector3(0f, 0.05f, 1f)));
                }
            }
            if (StoryAnchors.Has("a5_trailer")) Q7Pose("mara_yard", Q7At("a5_trailer", 0.6f, 0.6f), StoryAnchors.Yaw("a5_trailer") + 90f);
        }

        bool A5EscortDown()
        {
            foreach (var k in new[] { "a5_escort", "a5_overseer" })
            {
                if (MadMax.Npc.NpcRegistry.IsDead("cast:" + k)) continue;
                var b = CastBody(k);
                if (!b || (b.Alive && !b.Surrendered)) return false;
            }
            return true;
        }

        string A5DoorPrompt(WastelandGame g)
        {
            if (Story.Story.Flag("a5:started") || Story.Story.StateOf("A5") != Story.Story.State.Active) return null;
            if (!Story.Story.StepDone("A5", "way")) return "THE TRAILER DOOR: LOCKED AND WATCHED" + (A5Dark && Story.Story.StepDone("A5", "plan") ? "  (CROUCH HERE TO TRY THE LATCH)" : "");
            var op = A5Op();
            string head = "THE TRAILER DOOR  " + op.Checklist();
            if (A5Sneaking && !A5Dark) return head + "  WAIT FOR DARK";
            return head + (op.Ready ? "  [E] GO: OPEN IT" : "  NOT READY");
        }

        void A5DoorUse(WastelandGame g, bool secondary)
        {
            if (secondary || Story.Story.Flag("a5:started") || Story.Story.StateOf("A5") != Story.Story.State.Active) return;
            if (!Story.Story.StepDone("A5", "way")) { Toast("NOT YET: THEY NEED A WAY OUT (CONTRACTS, A DEAL, THE DARK, OR NO ESCORT)"); return; }
            if (A5Sneaking && !A5Dark) { Toast("NOT IN DAYLIGHT: THE ESCORT'S AWAKE"); return; }
            if (!A5Op().TryStart(out var why)) { if (why != null) Toast(why); return; }
            Story.Story.Note("a5:ready");                                                     // the list was met when you said go
            A5Begin();
        }

        /// <summary>The go: who rides where (your car first, then the bus, the rest wait at the staging point for a
        /// second trip), the water drunk and the cuts dressed, what the way out costs with the Guild.</summary>
        void A5Begin()
        {
            int people = A5People;
            var t = A5Transport();
            int tLoad = Mathf.Min(people, Q7Seats(t));
            int bus = A5Hired ? Mathf.Min(people - tLoad, A5BusSeats) : 0;
            int rest = Mathf.Max(0, people - tLoad - bus);
            Story.Story.SetFlag("a5:people:" + people); Story.Story.SetFlag("a5:tload:" + tLoad); Story.Story.SetFlag("a5:bus:" + bus); Story.Story.SetFlag("a5:rest:" + rest);
            if (t) StoryTag.Set(t.gameObject, "a5_riders");
            // two litres a head and something for the cuts, from the pack first, then the car's stores
            using (Inventory.Source("USED", "USED ON THE ROAD"))
            {
                int water = people * 2;
                int w = Mathf.Min(water, Inventory.Get(ResourceType.Water));
                if (w > 0) { Inventory.TrySpend(ResourceType.Water, w); water -= w; }
                if (t && water > 0) foreach (var c in t.GetComponentsInChildren<Container>()) { int k = Mathf.Min(water, c.inventory.Get(ResourceType.Water)); if (k > 0 && c.inventory.TrySpend(ResourceType.Water, k)) water -= k; }
                int meds = (people + 1) / 2;
                foreach (var id in A5Meds)
                {
                    while (meds > 0 && id != "med_firstaid" && Inventory.TakeItem(id)) meds--;
                    if (meds > 0 && id == "med_firstaid" && Inventory.TakeItem(id)) meds -= 3;
                }
            }
            string way = Story.Story.Route("A5", "way");
            if (way == StoryLibrary.A5Sneak) MadMax.Npc.Factions.Shift(MadMax.Npc.Faction.FuelGuild, -5);
            else if (way == StoryLibrary.A5Escort) MadMax.Npc.Factions.Shift(MadMax.Npc.Faction.FuelGuild, -10);
            else if (way == StoryLibrary.A5Labour) MadMax.Npc.Factions.Shift(MadMax.Npc.Faction.FuelGuild, 2);
            if (Story.Story.Route("A5", "plan") == StoryLibrary.A5Group)
                Story.Story.SetFlag("a5:release:" + (DayNight.Day + (way == StoryLibrary.A5Bought || way == StoryLibrary.A5Labour ? 1 : 3)));
            // Otis takes his share straight to the garage
            if (bus > 0)
            {
                var busTag = StoryTag.Find("a5_bus");
                if (busTag && StoryAnchors.Has("garage")) Q7Move(busTag.GetComponent<VehicleDriver>(), Q7At("garage", 10f, 11f), StoryAnchors.Yaw("garage") + 90f);
                Story.Story.SetFlag("a5:bus_gone");
            }
            string car = t ? t.name.Replace("(Clone)", "").ToUpperInvariant() : "NOTHING";
            Toast("THE DOOR SWINGS OPEN: " + tLoad + " INTO YOUR " + car + (bus > 0 ? ", " + bus + " ONTO OTIS'S BUS" : "") + (rest > 0 ? ", " + rest + " WAIT AT THE STAGING POINT" : "") + ". NOBODY RIDES IN THE BACK");
            Journal.Add("STORY", "THE TRANSFER: " + people + " OUT OF THE TRAILER. " + tLoad + " RIDE WITH YOU" + (bus > 0 ? ", " + bus + " WITH OTIS" : "") + (rest > 0 ? "; " + rest + " WAIT AT THE STAGING POINT FOR A SECOND TRIP" : "") + ".");
        }

        void A5Deliver(StepDef cur)
        {
            if (!Story.Story.Flag("a5:started") || Story.Story.StepDone("A5", "deliver") || !StoryAnchors.Has("garage_yard")) return;
            int tLoad = Mathf.Max(0, Q7State.Number("a5:tload:")), bus = Mathf.Max(0, Q7State.Number("a5:bus:")), rest = Mathf.Max(0, Q7State.Number("a5:rest:"));
            var t = A5Transport();
            if (!t && Current && fleet.Contains(Current) && Q7Seats(Current) > 0) { StoryTag.Set(Current.gameObject, "a5_riders"); t = Current; }   // the car was lost: they follow you in the next one
            var home = StoryAnchors.Get("garage_yard");
            bool Home() => tLoad > 0 ? t && Q7Flat(t.transform.position, home) < 25f : Q7Near("garage_yard", 25f);
            if (!Story.Story.Flag("a5:trip1"))
            {
                if (cur != null && cur.id == "deliver") cur.text = "BRING THEM IN: DRIVE THEM TO THE GARAGE AT THE BEND" + (tLoad == 0 ? " (THEY'RE ON OTIS'S BUS: MEET THEM THERE)" : "");
                if (!Home()) return;
                Story.Story.SetFlag("a5:trip1");
                Toast(tLoad > 0 ? tLoad + " CLIMB OUT AT THE GARAGE" + (bus > 0 ? ". OTIS'S BUS IS ALREADY IN THE YARD WITH " + bus + " MORE" : "") : "OTIS'S BUS IS IN THE YARD: " + bus + " CLIMB DOWN, STIFF AND GRINNING");
                if (rest <= 0) { Story.Story.Note("a5:delivered"); return; }
                if (StoryAnchors.Has("a5_staging")) SetWaypoint(StoryAnchors.Get("a5_staging"), "THE REST WAIT AT THE STAGING POINT", true);
                return;
            }
            if (!Story.Story.Flag("a5:trip2_loaded"))
            {
                if (cur != null && cur.id == "deliver") cur.text = "SECOND TRIP: " + rest + " STILL WAIT AT THE STAGING POINT";
                if (!a5ToStaging && StoryAnchors.Has("a5_staging")) { a5ToStaging = true; SetWaypoint(StoryAnchors.Get("a5_staging"), "THE REST WAIT AT THE STAGING POINT", true); }
                if (!t || !StoryAnchors.Has("a5_staging") || Q7Flat(t.transform.position, StoryAnchors.Get("a5_staging")) > 18f) return;
                Story.Story.SetFlag("a5:trip2_loaded");
                Toast(rest + " MORE CLIMB IN AT THE STAGING POINT");
                SetWaypoint(home, "THE GARAGE AT THE BEND", true);
                return;
            }
            if (cur != null && cur.id == "deliver") cur.text = "SECOND TRIP: BRING THE LAST " + rest + " TO THE GARAGE";
            if (t && Q7Flat(t.transform.position, home) < 25f)
            {
                Story.Story.SetFlag("a5:trip2");
                Toast("THE LAST " + rest + " CLIMB OUT AT THE GARAGE. EVERYONE WHO CAME IS HERE");
                Story.Story.Note("a5:delivered");
            }
        }

        /// <summary>After A5: the ones signed off on paper walk up the road when the agreement holds; Otis drives home.</summary>
        void A5Aftermath()
        {
            if (Story.Story.StateOf("A5") != Story.Story.State.Done) return;
            if (!Story.Story.Flag("a5:rest_free"))
            {
                if (a5Release == -2) { a5Release = -1; for (int d = 0; d <= DayNight.Day + 4 && a5Release < 0; d++) if (Story.Story.Flag("a5:release:" + d)) a5Release = d; }
                int day = a5Release;
                if (day < 0) Story.Story.SetFlag("a5:rest_free");
                else if (DayNight.Day >= day)
                {
                    Story.Story.SetFlag("a5:rest_free");
                    Journal.Add("STORY", "THE AGREEMENT HELD: REN AND THE OTHER TWO WALKED UP THE ROAD THIS MORNING WITH SIGNED RELEASES IN THEIR POCKETS.");
                    Toast("THE REST OF MARA'S CONVOY ARRIVED: THE AGREEMENT HELD");
                }
            }
            if (!Story.Story.Flag("a5:tidied"))
            {
                var bus = StoryTag.Find("a5_bus");
                if (bus && Q7Flat(bus.transform.position, FocusPos) > 160f) { Q7Remove(bus.GetComponent<VehicleDriver>()); bus = null; }
                var riders = StoryTag.Find("a5_riders");
                if (riders) Destroy(riders);
                if (!bus) Story.Story.SetFlag("a5:tidied");
            }
        }

        /// <summary>Take a story vehicle out of the world (it drove off).</summary>
        void Q7Remove(VehicleDriver v)
        {
            if (!v) return;
            if (v == Current) Exit();
            cars.Remove(v); fleet.Remove(v); trailers.Remove(v); wrecks.Remove(v); vehicles.Remove(v);
            Destroy(v.gameObject);
        }
    }
}
