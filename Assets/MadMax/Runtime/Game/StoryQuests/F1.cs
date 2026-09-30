using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Story;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>F1 THE LONG WAY HOME in the world: the Guild's dispatch yard outside the dispatch city (Ada's table under an
    /// awning, the dispatch board, two fuel pumps and two storage tanks, a generator, a Guild tractor, two guards at the
    /// gate). While it runs: Sera's plan answers carry today's numbers (<see cref="Allocation"/> of the loads the
    /// campaign's transport makes available) and the shares are fixed when posted; Ada's table negotiates (a refusal says
    /// why, in the journal, and can be tried again), the board calls a work stoppage, and guards down with the pumps and
    /// tanks whole takes the yard (a destroyed pump comes back wrecked, to be repaired). The board then shows the
    /// consequences and commits after an autosave; the first load (a story fuel truck, cans for the player's own vehicle, or
    /// a skiff at a lake by the destination) goes to the place with the biggest need, an escort from Sera's drivers rides
    /// along (<see cref="PlayerConvoy"/>), a lost load is replaced, and a scout hurt on the shortcut can be bandaged. The
    /// control route becomes the ending; the refuge's radio plays the last call.</summary>
    public partial class WastelandGame
    {
        static readonly string[] F1WorkIds = { "fuel_pump", "fuel_pump", "water_tank", "water_tank" };
        static readonly Vector3[] F1WorkAt = { new Vector3(6f, 0f, -2f), new Vector3(9.5f, 0f, -2f), new Vector3(7.5f, 0f, -8f), new Vector3(11.5f, 0f, -8f) };
        internal static readonly Vector3 F1TableAt = new Vector3(0f, 0f, -3f), F1BoardAt = new Vector3(-4.5f, 0f, -1.2f);
        internal static readonly Vector3 F1RadioTableAt = new Vector3(-3.5f, 0f, 0f), F1RadioAt = new Vector3(-3.5f, 0f, 1.3f);
        const string F1Fetched = "THE REFUGE'S RESIDENTS FETCHED HER";

        PlayerConvoy f1Convoy;
        readonly Queue<string> f1Captions = new Queue<string>();
        readonly HashSet<VehicleDriver> f1Tanked = new HashSet<VehicleDriver>();
        float f1CaptionAt;
        string f1Sig;
        int f1WaterFor = -1, f1WreckedLast;
        bool f1HasWater;
        Vector3 f1Landing, f1Launch, f1Short;

        static bool F1Flag(string f) => Story.Story.Flag(f);
        static void F1Set(string f) => Story.Story.SetFlag(f);
        static bool F1Done(string step) => Story.Story.StepDone("F1", step);
        static string F1Route(string step) => Story.Story.Route("F1", step);

        partial void Scene_F1()
        {
            if (!Build || !Build.Structures || !StoryAnchors.Has("f1_yard")) return;
            ArcCPut("f1_yard", "porch_awning", new Vector3(0f, 0f, -3.8f), 0f);
            ArcCPut("f1_yard", "table", F1TableAt, 0f);
            ArcCPut("f1_yard", "sign", F1BoardAt, 0f);
            for (int i = 0; i < F1WorkIds.Length; i++) F1PutWork(i, false);
            var gen = ArcCPut("f1_yard", "generator", new Vector3(3.5f, 0f, -9f), 0f);
            if (gen && gen.TryGetComponent<Generator>(out var g0)) { g0.fuel = g0.tankLitres; g0.on = true; gen.Dirty(); }
            ArcCPut("f1_yard", "flag", new Vector3(-9f, 0f, 2f), 0f);
            ArcCPut("f1_yard", "flag", new Vector3(14f, 0f, 2f), 0f);
            ArcCPut("f1_yard", "barrel", new Vector3(13f, 0f, -3.5f), 0f);
            ArcCPut("f1_yard", "barrel", new Vector3(13.8f, 0f, -2.6f), 0f);
            ArcCPut("f1_yard", "crate", new Vector3(-7f, 0f, -6f), 15f);
            ArcCPut("f1_yard", "crate", new Vector3(-8.4f, 0f, -5f), 60f);
            ArcCPut("f1_yard", "sandbag_wall", new Vector3(-2.5f, 0f, 4.5f), 0f);
            var tractor = ArcCVehicle("Hauler", "f1_yard", new Vector3(-14f, 0f, -7f), 90f, "f1_guild", "Guild Tractor");
            if (tractor) { var vp = tractor.GetComponent<VehiclePaint>() ?? tractor.gameObject.AddComponent<VehiclePaint>(); vp.colour = 5; vp.Apply(); }
            string city = "THE DISPATCH CITY";
            if (World != null && StoryAnchors.Has("dispatch"))
            {
                var d = StoryAnchors.Get("dispatch");
                var st = World.SettlementAt(d.x, d.z);
                if (st != null) city = Market.TownName(st);
            }
            Journal.Add("PLACE", "ADA VENN'S INVITATION: THE GUILD'S CENTRAL PUMPING AND DISPATCH YARD, ON THE ROAD OUT OF " + city + ". SHE'LL WAIT AS LONG AS IT TAKES.");
            Toast("A GUILD RUNNER BRINGS WORD: ADA VENN WILL SEE YOU AT THE DISPATCH YARD");
        }

        Placeable F1PutWork(int i, bool wrecked)
        {
            var p = ArcCPut("f1_yard", F1WorkIds[i], F1WorkAt[i], 0f);
            if (!p) return null;
            if (wrecked) p.hits = 1;
            p.Dirty();
            return p;
        }

        partial void Tick_F1()
        {
            if (World != null) StoryCast.ArcCTwin("sera_f1", "sera", World.seed);
            if (f1Captions.Count > 0 && Time.time >= f1CaptionAt) { Toast(f1Captions.Dequeue()); f1CaptionAt = Time.time + 3.4f; }
            if (f1Convoy != null && f1Convoy.Formed) f1Convoy.Update(this);
            if (Time.frameCount % 15 != 0) return;
            F1Attach();
            F1Pose();
            F1Places();
            F1Replies();
            F1PostShares();
            F1Works();
            F1Run();
            F1Rider();
            F1Resolve();
            F1Home();
        }

        // ------------------------------------------------------------------ the yard

        void F1Attach()
        {
            // the controls live on story props; one smashed (or never built) is put back, so no step can get stuck
            bool atYard = Q7Near("f1_yard", 120f);
            var table = Q7Prop("f1_yard", "table", F1TableAt, 2.5f);
            if (!table && atYard) table = ArcCPut("f1_yard", "table", F1TableAt, 0f);
            if (table && !table.GetComponent<StoryControl>()) StoryControl.On(table.gameObject, "f1_table", F1TablePrompt, F1TableUse);
            var board = Q7Prop("f1_yard", "sign", F1BoardAt, 2.5f);
            if (!board && atYard) board = ArcCPut("f1_yard", "sign", F1BoardAt, 0f);
            if (board && !board.GetComponent<StoryControl>()) StoryControl.On(board.gameObject, "f1_board", F1BoardPrompt, F1BoardUse);
            if (F1Flag("f1_radio_set"))
            {
                var rt = Q7Prop("garage_yard", "table", F1RadioTableAt, 2.5f);
                if (!rt && Q7Near("garage_yard", 120f)) rt = ArcCPut("garage_yard", "table", F1RadioTableAt, 0f);
                if (rt && !rt.GetComponent<StoryControl>()) StoryControl.On(rt.gameObject, "f1_radio", F1RadioPrompt, F1RadioUse);
            }
        }

        void F1Pose()
        {
            if (!StoryAnchors.Has("f1_yard")) return;
            float y = StoryAnchors.Yaw("f1_yard");
            Q7Pose("ada", Q7At("f1_yard", 0f, -4.4f), y);
            Q7Pose("f1_guard1", Q7At("f1_yard", -2.5f, 3.2f), y);
            Q7Pose("f1_guard2", Q7At("f1_yard", 4.5f, 3.2f), y);
            Q7Pose("sera_f1", Q7At("f1_yard", -6.5f, -1.5f), y + 90f);
        }

        /// <summary>Loads the Guild can move this week: its own eight trucks plus what the campaign built up.</summary>
        int F1Supply(out string why)
        {
            int s = 8;
            var parts = new List<string> { "THE GUILD'S EIGHT RUNNING TRUCKS 8" };
            int trucks = 0;
            if (F1Flag("c5_arrived"))
            {
                string[] steps = { "isaac", "pru", "tobias" }, tags = { "c5_v1", "c5_v2", "c5_v3" };
                for (int i = 0; i < 3; i++) if (Story.Story.StepDone("C5", steps[i]) && !F1Flag("c5_dropped:" + tags[i])) trucks++;
            }
            if (trucks > 0) { s += trucks; parts.Add("THE CONVOY'S TRUCKS +" + trucks); }
            if (F1Flag("c5_route_open")) { s += 2; parts.Add("THE CONVOY ROUTE +2"); }
            if (StoryTag.Find("c5_trailer") || F1Flag("c5_settle_done")) { s += 1; parts.Add("THE CO-OP CARGO TRAILER +1"); }
            bool tanker = false;
            foreach (var v in fleet) if (v && v.GetComponent<FuelTanker>()) { tanker = true; break; }
            if (tanker) { s += 2; parts.Add("YOUR TANKER +2"); }
            if (F1Flag("c4_done")) { s += 1; parts.Add("THE REBUILT CROSSING +1 (SHORTER TRIPS)"); }
            why = string.Join(", ", parts);
            return s;
        }

        /// <summary>Once posted: the drop at the place with the biggest need (or the landing below it, by water) and the
        /// shortcut's scouting point halfway (open dry ground near the midpoint). Worked out once per target.</summary>
        void F1Places()
        {
            int t = StoryLibrary.F1Target();
            if (t < 0 || World == null) return;
            string anchor = StoryLibrary.F1DropAnchor(t);
            var target = StoryAnchors.Get(anchor);
            var yard = StoryAnchors.Get("f1_yard");
            if (f1WaterFor != t)
            {
                f1WaterFor = t;
                f1HasWater = F1FindWater(target, out f1Landing, out f1Launch);
                var mid = Vector3.Lerp(yard, target, 0.5f);
                f1Short = mid; bool found = false;
                for (float r = 0f; r <= 120f && !found; r += 20f)
                    for (int k = 0; k < (r == 0f ? 1 : 8) && !found; k++)
                    {
                        var p = mid + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * r;
                        if (!float.IsNaN(World.Sample(p.x, p.z).water) || World.SettlementAt(p.x, p.z) != null) continue;
                        f1Short = p; found = true;
                    }
                f1Short.y = World.Sample(f1Short.x, f1Short.z).height;
            }
            bool water = F1Route("route") == StoryLibrary.F1Water && f1HasWater;
            StoryAnchors.ArcCMove("f1_drop", water ? f1Landing : target, StoryAnchors.Yaw(anchor));
            StoryAnchors.ArcCMove("f1_short", f1Short, ArcCFlat(target, yard) > 1f ? Mathf.Atan2(target.x - yard.x, target.z - yard.z) * Mathf.Rad2Deg : 0f);
        }

        float F1Depth(Vector3 p)
        {
            var s = World.Sample(p.x, p.z);
            return float.IsNaN(s.water) ? -1f : s.water - s.height;
        }

        /// <summary>Open water 1.4 m+ deep within 700 m of <paramref name="t"/> (a lake or the sea): the landing nearest it,
        /// and a launch point at least 50 m across the water from there.</summary>
        bool F1FindWater(Vector3 t, out Vector3 landing, out Vector3 launch)
        {
            landing = launch = t;
            int budget = 4000;                                                                          // samples: a bounded one-off search
            for (float r = 20f; r <= 700f && budget > 0; r += 20f)
                for (int a = 0; a < 24 && budget > 0; a++)
                {
                    var dir = Quaternion.Euler(0f, a * 15f, 0f) * Vector3.forward;
                    var p = t + dir * r;
                    budget--;
                    if (F1Depth(p) < 1.4f) continue;
                    var far = p;
                    for (float s = 10f; s <= 500f; s += 10f) { var q = p + dir * s; budget--; if (F1Depth(q) < 1.4f) break; far = q; }
                    if (ArcCFlat(far, p) < 50f) continue;
                    landing = p; launch = far;
                    return true;
                }
            return false;
        }

        /// <summary>Dialogue and objective text that carry today's numbers (Sera's plans, Ada's terms, the route).</summary>
        void F1Replies()
        {
            int supply = F1Supply(out _);
            int t = StoryLibrary.F1Target();
            string block = StoryLibrary.F1NegotiationBlock();
            var support = StoryLibrary.F1Support();
            string sig = supply + "|" + t + "|" + f1HasWater + "|" + support.Count + "|" + (block == null) + "|" + F1Flag("c4_done") + "|" + F1Route("route");
            if (sig == f1Sig) return;
            f1Sig = sig;
            if (!F1Done("plan"))
            {
                foreach (var (topic, label) in new[] { ("f1_byneed", "BY NEED"), ("f1_even", "EVEN"), ("f1_town", "THE FIRST TOWN"), ("f1_remote", "THE NEXT TOWN") })
                    ArcCReply("F1", "plan", topic, StoryLibrary.F1PlanReply(label, supply));
                int need = 0; foreach (var c in StoryLibrary.F1Customers()) need += c.need;
                var ps = StoryLibrary.Q7Step("F1", "plan");
                if (ps != null) ps.text = "SHARE OUT THE GUILD'S FUEL WEEK WITH SERA AT THE DISPATCH YARD: " + supply + " LOADS FOR FIVE PLACES THAT NEED " + need;
            }
            ArcCReply("F1", "ask", "f1_ask", block ?? "AUTHENTICATED RECORDS AND " + support.Count + " PEOPLE AT YOUR BACK: " + string.Join(", ", support) +
                                          ". SIT DOWN AT MY TABLE AND PUT THE TERMS TO ME. I'LL SIGN, AND I'LL HATE IT, AND THE NUMBERS WILL BE PUBLIC.");
            string tn = StoryLibrary.F1TargetName();
            ArcCReply("F1", "route", "f1_road", "THE ROAD TO " + tn + (F1Flag("c4_done") ? ", OVER THE CROSSING WE REBUILT" : "") + ": SLOWER, SAFE, AND EVERYONE SEES THE TRUCK GO BY.");
            ArcCReply("F1", "route", "f1_short", "STRAIGHT ACROSS TO " + tn + ". SHORTER AND ROUGHER. KIT ARNO RIDES AHEAD TO SCOUT IT; WATCH THE WASHES, AND WATCH HER.");
            ArcCReply("F1", "route", "f1_water", "THERE'S WATER BELOW " + tn + ". I'LL HAVE A SKIFF LOADED WITH CANS ON THE FAR SIDE: GET TO IT, SAIL IT ACROSS, AND THEY'LL CARRY IT UP FROM THE LANDING.");
            var rs = StoryLibrary.Q7Step("F1", "route");
            if (rs != null)
            {
                foreach (var c in rs.any) if (c.topic == "f1_water") c.requires = f1HasWater ? null : StoryLibrary.F1NoWater;
                if (t >= 0) rs.text = "CHOOSE WITH SERA HOW THE FIRST LOAD GOES TO " + tn + ": THE REPAIRED ROAD, THE RISKY SHORTCUT" + (f1HasWater ? ", OR BY WATER" : "");
            }
            var run = StoryLibrary.Q7Step("F1", "run");
            if (run != null && t >= 0)
                run.text = F1Route("route") == StoryLibrary.F1Water
                    ? "MAKE THE RUN: THE LOADED SKIFF WAITS ACROSS THE WATER. SAIL IT TO THE LANDING BELOW " + tn
                    : "MAKE THE RUN: THE FIRST LOAD TO " + tn + " (THE FUEL TRUCK AT THE YARD, OR CANS FROM THE BOARD IN YOUR OWN VEHICLE)";
        }

        /// <summary>The plan is fixed when posted: needs, shares and the week's loads go into the saved record.</summary>
        void F1PostShares()
        {
            string plan = F1Route("plan");
            if (plan == null || ArcCRecord.Has("f1:supply")) return;
            int supply = F1Supply(out string why);
            var cs = StoryLibrary.F1Customers();
            for (int i = 0; i < cs.Count; i++) ArcCRecord.Put("f1:need:" + cs[i].key, cs[i].need);
            var p = StoryLibrary.F1PlanOf(plan, out int fav);
            var sh = Allocation.Shares(cs, supply, p, fav);
            for (int i = 0; i < cs.Count; i++) ArcCRecord.Put("f1:share:" + cs[i].key, sh[i]);
            ArcCRecord.Put("f1:supply", supply);
            var support = StoryLibrary.F1Support();
            Journal.Add("STORY", "THE FUEL WEEK, POSTED ON THE YARD BOARD: " + Allocation.Posted(cs, sh, supply, "LOADS") + ".");
            Journal.Add("STORY", StoryLibrary.F1CanMeet(cs, sh));
            Journal.Add("STORY", "TRANSPORT THIS WEEK: " + why + " = " + supply + " LOADS.");
            Journal.Add("STORY", "WHO STANDS WITH YOU: " + (support.Count > 0 ? string.Join(", ", support) : "NOBODY YET") + ".");
            Journal.Add("STORY", "THE FIRST LOAD GOES TO " + StoryLibrary.F1TargetName() + ": THE BIGGEST NEED ON THE BOARD.");
            Toast("POSTED ON THE YARD BOARD. " + StoryLibrary.F1CanMeet(cs, sh));
        }

        // ------------------------------------------------------------------ take control of the decision

        string F1TablePrompt(WastelandGame g)
        {
            if (!Q7State.At("F1", "control")) return null;
            return "ADA'S TABLE: [E] PUT PUBLISHED ALLOCATION TERMS TO ADA (" + StoryLibrary.F1Support().Count + " OF " + StoryLibrary.F1TableSupport + " VOICES)";
        }

        void F1TableUse(WastelandGame g, bool secondary)
        {
            if (secondary || !Q7State.At("F1", "control")) return;
            if (NpcRegistry.IsDead("cast:ada")) { Toast("ADA VENN IS DEAD: NOBODY IS LEFT TO BARGAIN WITH. THE STOPPAGE AND THE YARD ARE STILL OPEN"); return; }
            string block = StoryLibrary.F1NegotiationBlock();
            if (block != null)
            {
                F1Set("f1_neg_failed");
                Journal.Add("STORY", "THE TABLE: " + block + " THE STOPPAGE AND THE YARD ARE STILL OPEN TO YOU.");
                Toast("ADA TURNS THE TERMS DOWN: " + (block.StartsWith("ADA COUNTS") ? "NOT ENOUGH VOICES" : "THE RECORDS AREN'T AUTHENTICATED") + " (SEE THE JOURNAL)");
                return;
            }
            Journal.Add("STORY", "ADA READS THE POSTED WEEK, THE CHECKED RECORDS AND WHO STANDS WITH YOU. 'PUBLISHED QUOTAS, AUDITED BY THE TOWNS. MY PUMPS, YOUR NUMBERS.' SHE SIGNS.");
            Toast("ADA SIGNS: THE ALLOCATION WILL BE PUBLISHED");
            Story.Story.Note("f1:agreed");
        }

        string F1BoardPrompt(WastelandGame g)
        {
            if (Q7State.At("F1", "control")) return "THE DISPATCH BOARD: [E] CALL A WORK STOPPAGE (SERA'S DRIVERS PARK UP)";
            if (Q7State.At("F1", "commit")) return "THE DISPATCH BOARD: [E] READ WHAT COMMITTING MEANS  [T] COMMIT TO THE RUN (THE GAME SAVES FIRST)";
            if (Q7State.At("F1", "run") && F1Route("route") != StoryLibrary.F1Water) return "THE DISPATCH BOARD: [E] TAKE THE FIRST LOAD AS " + StoryLibrary.F1Cans + " CANS FOR YOUR OWN VEHICLE";
            return null;
        }

        void F1BoardUse(WastelandGame g, bool secondary)
        {
            if (Q7State.At("F1", "control"))
            {
                if (secondary) return;
                string block = StoryLibrary.F1StoppageBlock(f1HasWater, StoryLibrary.F1TargetName());
                if (block != null)
                {
                    F1Set("f1_stop_failed");
                    Journal.Add("STORY", "THE STOPPAGE: " + block + " ADA'S TABLE AND THE YARD ARE STILL OPEN TO YOU.");
                    Toast("NO STOPPAGE YET: " + (block.Contains("DRIVERS") ? "NO DRIVERS TO PARK UP" : "NO ROUTE THAT ISN'T THE GUILD'S") + " (SEE THE JOURNAL)");
                    return;
                }
                ArcCVehicle("Pickup", "f1_yard", new Vector3(1f, 0f, 8.5f), 90f, "f1_stop1", "Co-op Truck");
                ArcCVehicle("Pickup", "f1_yard", new Vector3(7.5f, 0f, 8.5f), 90f, "f1_stop2", "Co-op Truck");
                Journal.Add("STORY", "SERA'S DRIVERS PARK ACROSS THE YARD GATE AND SIT ON THEIR BUMPERS. THE GUILD'S TANKERS DON'T ROLL. THE FUEL THE TOWNS NEED WILL MOVE ANYWAY, ON ROUTES THAT AREN'T THE GUILD'S, AND EVERYONE WILL SEE IT DOES.");
                Toast("THE STOPPAGE HOLDS: SERA'S DRIVERS BLOCK THE YARD GATE");
                Story.Story.Note("f1:stoppage");
                return;
            }
            if (Q7State.At("F1", "commit"))
            {
                if (!secondary) { F1Consequences(); F1Set("f1_read"); return; }
                if (!F1Flag("f1_read")) { F1Consequences(); F1Set("f1_read"); }
                F1Commit();
                return;
            }
            if (Q7State.At("F1", "run") && !secondary && F1Route("route") != StoryLibrary.F1Water)
            {
                int have = Inventory.GetItem(StoryLibrary.F1Can);
                if (have >= StoryLibrary.F1Cans) { Toast("YOU'RE ALREADY CARRYING THE FIRST LOAD"); return; }
                if (F1Flag("f1_cans_taken")) F1Lost("THE CANS NEVER ARRIVED", false);
                F1Set("f1_cans_taken");
                using (Inventory.Source("THE FIRST LOAD")) Inventory.AddItem(StoryLibrary.F1Can, StoryLibrary.F1Cans - have);
                Toast(StoryLibrary.F1Cans + " CANS OF GUILD FUEL: THE FIRST LOAD RIDES IN YOUR OWN VEHICLE");
            }
        }

        bool F1GuardsDown()
        {
            foreach (var k in new[] { "f1_guard1", "f1_guard2" })
            {
                if (NpcRegistry.IsDead("cast:" + k)) continue;
                var b = CastBody(k);
                if (!b || (b.Alive && !b.Surrendered)) return false;                                         // dead or given up
            }
            return true;
        }

        /// <summary>The yard's pumps and tanks: a destroyed one is dragged back wrecked (repairable, so nothing is lost for
        /// good); guards down with every work whole takes the yard, a wrecked one blocks it until repaired.</summary>
        void F1Works()
        {
            if (F1Flag("f1_resolved") || !Q7Near("f1_yard", 160f)) return;
            int wrecked = 0;
            for (int i = 0; i < F1WorkIds.Length; i++)
            {
                var p = Q7Prop("f1_yard", F1WorkIds[i], F1WorkAt[i], 2.5f);
                var def = FurnitureLibrary.Get(F1WorkIds[i]);
                if (!p)
                {
                    p = F1PutWork(i, true);
                    if (p) Journal.Add("STORY", "THE YARD HANDS DRAG THE WRECKED " + (def != null ? def.name : F1WorkIds[i].ToUpperInvariant()) + " BACK ONTO ITS PAD. IT WON'T WORK UNTIL IT'S REPAIRED ([B], AIM, R).");
                    wrecked++;
                }
                else if (def != null && p.hits * 2 < def.hits) wrecked++;
            }
            if (wrecked > 0 && f1WreckedLast == 0) Toast("THE YARD'S PUMPS AND TANKS ARE WRECKED: A PUMP THAT DOESN'T PUMP FEEDS NOBODY. REPAIR THEM ([B], AIM, R)");
            else if (wrecked == 0 && f1WreckedLast > 0) Toast("THE YARD'S PUMPS AND TANKS ARE WHOLE AGAIN");
            f1WreckedLast = wrecked;
            if (!Q7State.At("F1", "control")) return;
            bool down = F1GuardsDown();
            if (down && !F1Flag("f1_guards_down")) { F1Set("f1_guards_down"); Toast("THE YARD GUARDS ARE DOWN. ADA: 'CAREFUL WITH THOSE PUMPS. THEY'RE THE ONLY ARGUMENT EITHER OF US HAS.'"); }
            if (down && wrecked > 0 && !F1Flag("f1_seize_blocked"))
            {
                F1Set("f1_seize_blocked");
                Journal.Add("STORY", "TAKING THE YARD FAILS WHILE ITS PUMPS AND TANKS ARE WRECKED: A LOCKED YARD FEEDS NOBODY, AND A BROKEN ONE DOESN'T EITHER. REPAIR THEM AND THE YARD IS YOURS; ADA'S TABLE AND THE STOPPAGE ARE STILL OPEN.");
                Toast("THE YARD IS NO USE WRECKED: REPAIR ITS PUMPS AND TANKS");
            }
            if (down && wrecked == 0 && !F1Flag("f1_seized"))
            {
                F1Set("f1_seized");
                Journal.Add("STORY", "THE DISPATCH YARD IS YOURS, PUMPS AND TANKS WORKING. ADA HANDS OVER THE KEYS WITHOUT A WORD AND WATCHES WHAT YOU DO WITH THEM.");
                Story.Story.Note("f1:seized");
            }
        }

        // ------------------------------------------------------------------ commit

        int F1StrengthNow(out string parts)
        {
            int support = StoryLibrary.F1Support().Count;
            var cs = StoryLibrary.F1Customers();
            var sh = StoryLibrary.F1Shares() ?? new int[cs.Count];
            int gap = Allocation.Shortfall(cs, sh);
            bool clinicShort = sh[2] < cs[2].need;
            int lost = 0; while (F1Flag("f1_lost:" + lost)) lost++;
            int hurt = F1Flag("f1_rider_down") && !F1Done("treat") ? 1 : 0;
            int s = StoryLibrary.F1Strength(support, gap, clinicShort, lost + hurt);
            parts = "SUPPORT " + support + ", THE WEEK " + (gap == 0 ? "FULLY COVERED +2" : gap + " LOADS SHORT +" + (gap <= 2 ? 1 : 0)) + ", THE CLINIC " + (clinicShort ? "SHORT +0" : "COVERED +1")
                    + (lost + hurt > 0 ? ", LOSSES -" + (lost + hurt) : "") + " = " + s + " (" + StoryLibrary.F1Strong + " OR MORE FOR THE STRONGER OUTCOME)";
            return s;
        }

        /// <summary>What committing means, before the player commits: the ending the control route leads to, the posted
        /// week, the run and the agreed jobs, the support, the strength as it stands, and the safety net.</summary>
        void F1Consequences()
        {
            string control = F1Route("control"), e = StoryLibrary.F1EndingOf(control);
            int score = F1StrengthNow(out string parts);
            var lines = StoryLibrary.F1EndingLines(e, score >= StoryLibrary.F1Strong);
            var cs = StoryLibrary.F1Customers(); var sh = StoryLibrary.F1Shares() ?? new int[cs.Count];
            var support = StoryLibrary.F1Support();
            Journal.Add("STORY", "IF YOU COMMIT: " + StoryLibrary.F1EndingTitle(e) + ". " + lines[0]);
            Journal.Add("STORY", "WHAT STAYS HARD: " + lines[1]);
            Journal.Add("STORY", "THE WEEK AS POSTED: " + Allocation.Posted(cs, sh, ArcCRecord.Get("f1:supply"), "LOADS") + ". " + StoryLibrary.F1CanMeet(cs, sh));
            Journal.Add("STORY", "YOU DRIVE THE FIRST LOAD TO " + StoryLibrary.F1TargetName() + " BY " + (F1Route("route") ?? "ROAD") + "; ALLIES TAKE THE OTHER SHARES.");
            Journal.Add("STORY", "WHO STANDS WITH YOU: " + (support.Count > 0 ? string.Join(", ", support) : "NOBODY") + ". AS IT STANDS: " + parts + ".");
            Journal.Add("STORY", "A LOST LOAD IS REPLACED AT THE YARD AND A HURT ALLY CAN BE PATCHED UP: NOTHING HERE BECOMES UNWINNABLE. [T] AT THE BOARD SAVES, THEN COMMITS.");
            Toast("IF YOU COMMIT: " + StoryLibrary.F1EndingTitle(e) + ". THE REST IS IN YOUR JOURNAL; [T] TO COMMIT");
        }

        void F1Commit()
        {
            var before = System.DateTime.Now.AddSeconds(-2);
            Autosave("the long way home");
            bool saved = SaveSystem.Exists(0) && System.IO.File.GetLastWriteTime(SaveSystem.PathOf(0)) >= before;
            Journal.Add("STORY", saved ? "SAVED BEFORE THE RUN (AUTOSAVE): LOADING IT PUTS YOU BACK AT THE DISPATCH BOARD, READY TO COMMIT."
                                       : "THE GAME COULDN'T SAVE HERE. THE RUN GOES AHEAD ANYWAY: A LOST LOAD IS STILL REPLACED AT THE YARD.");
            F1Set("f1_committed");
            F1AgreedJobs();
            Toast("COMMITTED. THE FIRST LOAD IS WAITING IN THE YARD");
            Story.Story.Note("f1:committed");
        }

        /// <summary>Who takes the other shares while the player drives the first load.</summary>
        List<string> F1Hands()
        {
            var hands = new List<string>();
            if (F1Flag("arc_c_done")) hands.Add("SERA'S DRIVERS");
            if (Story.Story.StepDone("C5", "isaac")) hands.Add("ISAAC DUNE");
            if (Story.Story.StepDone("C5", "pru")) hands.Add("PRU HALLORAN");
            if (Story.Story.StepDone("C5", "tobias")) hands.Add("TOBIAS KERR");
            if (F1Flag("residents") || F1Flag("b4_done")) hands.Add("THE REFUGE'S RESIDENTS");
            string control = F1Route("control");
            if (control != StoryLibrary.F1Stop) hands.Add(control == StoryLibrary.F1Seize ? "THE GUILD'S DRIVERS, UNDER NEW MANAGEMENT" : "THE GUILD'S DRIVERS, UNDER THE NEW TERMS");
            if (hands.Count == 0) hands.Add("VOLUNTEERS FROM THE TOWNS");
            return hands;
        }

        void F1AgreedJobs()
        {
            var cs = StoryLibrary.F1Customers(); var sh = StoryLibrary.F1Shares();
            if (sh == null) return;
            var hands = F1Hands();
            int t = StoryLibrary.F1Target(), k = 0;
            for (int i = 0; i < cs.Count; i++)
            {
                if (i == t || sh[i] == 0) continue;
                Journal.Add("STORY", "AGREED JOB: " + Allocation.Units(sh[i], "LOADS") + " TO " + cs[i].name + ", " + hands[k++ % hands.Count] + ".");
            }
            if (F1Flag("refuge")) Journal.Add("STORY", "NELL KEEPS THE TRUCKS RUNNING AT THE REFUGE: A BREAKDOWN COSTS A DAY, NOT A LOAD.");
        }

        // ------------------------------------------------------------------ the run

        void F1Lost(string reason, bool atLaunch)
        {
            int n = 0; while (F1Flag("f1_lost:" + n)) n++;
            F1Set("f1_lost:" + n);
            Journal.Add("STORY", reason + ". " + (atLaunch ? "SERA HAS ANOTHER SKIFF LOADED AT THE FAR SHORE." : "DISPATCH LOADS A REPLACEMENT AT THE YARD.") + (F1Flag("refuge") ? " NELL'S PEOPLE SALVAGE WHAT THEY CAN." : ""));
            Toast(reason + ": A REPLACEMENT IS WAITING " + (atLaunch ? "AT THE FAR SHORE" : "AT THE YARD"));
        }

        bool F1Wrecked(VehicleDriver v)
        {
            if (!v.Body || v.Body.isKinematic) return false;                                            // far off and resting: as it was
            var ch = v.GetComponent<VehicleChassis>();
            var s = ch ? ch.FindSocket("cargo") : null;
            if (s && s.Current) { f1Tanked.Add(v); if (s.Current.damage >= 0.95f) return true; }
            else if (s && f1Tanked.Contains(v)) return true;                                           // the tanks came off
            var p = v.transform.position;
            if (terrain && terrain.WaterDepth(p.x, p.z) > 1.6f) return true;
            return ArcCWear(v) > 9f;
        }

        /// <summary>The story fuel truck at the yard (spawned at commit, replaced when lost or wrecked).</summary>
        void F1Load()
        {
            var load = ArcCTagged("f1_load");
            if (load && !F1Wrecked(load)) return;
            if (load) { StoryTag.Set(load.gameObject, "f1_wreck"); F1Lost("THE FIRST LOAD IS WRECKED", false); }
            else if (F1Flag("f1_load_out")) F1Lost("THE FIRST LOAD IS GONE", false);
            F1Set("f1_load_out");
            load = ArcCVehicle("Pickup", "f1_yard", new Vector3(-9f, 0f, 5.5f), 90f, "f1_load", "Guild Fuel Truck");
            if (!load) return;
            ArcCMount(load, "cargo", "cargo_twin_fuel_tanks");
            if (load.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = sys.fuelCapacity * 0.9f;
            var vp = load.GetComponent<VehiclePaint>() ?? load.gameObject.AddComponent<VehiclePaint>();
            vp.colour = 5; vp.Apply();
        }

        /// <summary>The prepared skiff at the launch (water route), replaced when it sinks or is lost.</summary>
        void F1Boat()
        {
            if (!f1HasWater) return;
            var boat = ArcCTagged("f1_boat");
            float lvl = World.Sample(f1Launch.x, f1Launch.z).water;
            bool sunk = boat && boat.Body && !boat.Body.isKinematic && !float.IsNaN(lvl) && boat.transform.position.y < lvl - 2.5f;
            if (boat && !sunk)
            {
                if (Current == boat && !F1Flag("f1_boat_aboard")) { F1Set("f1_boat_aboard"); Toast("THE CANS ARE LASHED IN THE BOAT: SAIL FOR THE LANDING BELOW " + StoryLibrary.F1TargetName()); }
                return;
            }
            if (boat) { StoryTag.Set(boat.gameObject, "f1_wreck"); F1Lost("THE BOAT WENT DOWN WITH THE CANS", true); }
            else if (F1Flag("f1_boat_out")) F1Lost("THE BOAT IS GONE", true);
            F1Set("f1_boat_out");
            var pf = PrefabFor("Skiff") ?? PrefabFor("Raft");
            if (!pf) return;
            var at = f1Launch; at.y = (float.IsNaN(lvl) ? World.Sample(at.x, at.z).height : lvl) + 0.4f;
            var d = f1Landing - f1Launch; d.y = 0f;
            var v = Instantiate(pf, at, Quaternion.LookRotation(d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward)).GetComponent<VehicleDriver>();
            v.name = "Loaded Skiff";
            Register(v, null);
            StoryTag.Set(v.gameObject, "f1_boat");
            if (v.TryGetComponent<VehicleSystems>(out var sys)) sys.fuel = sys.fuelCapacity;
            SetWaypoint(f1Launch, "THE LOADED SKIFF", true);
        }

        /// <summary>One of Sera's drivers escorts the load (formed at the yard behind the player's vehicle; after a reload it
        /// re-forms behind wherever they are).</summary>
        void F1Escort()
        {
            if (!F1Flag("arc_c_done") || f1Convoy != null || !Current) return;
            if (!F1Flag("f1_escort_formed") && ArcCFlat(Current.transform.position, StoryAnchors.Get("f1_yard")) > 60f) return;
            string who = Story.Story.StepDone("C5", "tobias") ? "TOBIAS" : Story.Story.StepDone("C5", "isaac") ? "ISAAC" : Story.Story.StepDone("C5", "pru") ? "PRU" : "SERA'S DRIVER";
            string key = who == "TOBIAS" ? "c5_tobias" : who == "ISAAC" ? "c1_driver" : who == "PRU" ? "c2_hauler" : "sera";
            f1Convoy = new PlayerConvoy();
            f1Convoy.Add("f1_escort", key, who);
            var lead = Current.transform;
            f1Convoy.Form(this, m =>
            {
                var back = lead.forward; back.y = 0f; back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.forward;
                var p = lead.position - back * 16f; p.y = terrain.Height(p.x, p.z) + 0.8f;
                var v = SpawnAiVehicle("Pickup", p, Quaternion.LookRotation(back));
                if (v) { StoryTag.Set(v.gameObject, "f1_escort"); v.name = "Escort Truck"; }
                return v;
            });
            if (!F1Flag("f1_escort_formed")) { F1Set("f1_escort_formed"); Toast(who + " FALLS IN BEHIND YOU IN AN ESCORT TRUCK"); }
        }

        void F1Run()
        {
            if (!F1Done("commit") || F1Done("run")) return;
            int t = StoryLibrary.F1Target();
            if (t < 0) return;
            bool water = F1Route("route") == StoryLibrary.F1Water && f1HasWater;
            if (water) F1Boat(); else { F1Load(); F1Escort(); }
            if (F1Flag("c4_done") && !F1Flag("f1_via_crossing") && StoryAnchors.Has("c4_crossing"))
            {
                var c = StoryAnchors.Get("c4_crossing"); var load = ArcCTagged("f1_load");
                if (ArcCFlat(FocusPos, c) < 14f || (load && ArcCFlat(load.transform.position, c) < 14f)) F1Set("f1_via_crossing");
            }
            var drop = StoryAnchors.Get("f1_drop");
            string how = null;
            if (water)
            {
                var boat = ArcCTagged("f1_boat");
                if (boat && Current == boat && ArcCFlat(boat.transform.position, drop) < 30f) how = "THE SKIFF";
            }
            else
            {
                var load = ArcCTagged("f1_load");
                if (load && ArcCFlat(load.transform.position, drop) < 18f) how = "THE FUEL TRUCK";
                else if (Inventory.GetItem(StoryLibrary.F1Can) >= StoryLibrary.F1Cans && ArcCFlat(FocusPos, drop) < 18f)
                {
                    using (Inventory.Source("DELIVERED", "HANDED OVER")) Inventory.TakeItem(StoryLibrary.F1Can, Inventory.GetItem(StoryLibrary.F1Can));
                    how = "CANS IN YOUR OWN VEHICLE";
                }
            }
            if (how == null) return;
            F1Set("f1_delivered");
            if (f1Convoy != null) { f1Convoy.Release(); f1Convoy = null; }
            string tn = StoryLibrary.F1TargetName();
            Journal.Add("STORY", "THE FIRST LOAD IS IN AT " + tn + ": " + how + ", BY " + (F1Route("route") ?? "ROAD") + (F1Flag("f1_via_crossing") ? ", OVER THE CROSSING YOU REBUILT" : "") + ".");
            // the allies report in on the other agreed jobs
            var cs = StoryLibrary.F1Customers(); var sh = StoryLibrary.F1Shares();
            var hands = F1Hands(); int k = 0;
            for (int i = 0; i < cs.Count; i++)
            {
                if (i == t) continue;
                if (sh[i] == 0) { Journal.Add("STORY", cs[i].name + " GOT NOTHING THIS WEEK. IT WAS ON THE BOARD; THEY'LL SAY SO."); continue; }
                Journal.Add("STORY", hands[k++ % hands.Count] + " DELIVERED " + Allocation.Units(sh[i], "LOADS") + " TO " + cs[i].name + (sh[i] < cs[i].need ? ", " + (cs[i].need - sh[i]) + " SHORT OF WHAT IT BURNS." : ", ALL IT NEEDS."));
            }
            Toast("THE FIRST LOAD IS IN AT " + tn + ". THE OTHER JOBS REPORT IN");
            Story.Story.Note("f1:delivered");
        }

        /// <summary>The shortcut's scout: hurt at the halfway point when the run comes near it; bandaged by the player, or
        /// fetched home by the refuge's residents once the load is in.</summary>
        void F1Rider()
        {
            if (F1Route("route") != StoryLibrary.F1Short || !F1Done("commit")) return;
            if (!F1Flag("f1_rider_down") && !F1Done("run") && ArcCFlat(FocusPos, StoryAnchors.Get("f1_short")) < 90f)
            {
                F1Set("f1_rider_down");
                Journal.Add("STORY", "KIT ARNO, SCOUTING THE SHORTCUT AHEAD OF YOU, WENT OVER HER HANDLEBARS IN A WASH. SHE'S SITTING UP, WHICH IS SOMETHING. A BANDAGE WOULD HELP.");
                Toast("KIT ARNO CAME OFF HER BIKE AHEAD: SHE'S HURT");
                SetWaypoint(StoryAnchors.Get("f1_short"), "KIT ARNO, HURT", true);
            }
            if (F1Flag("f1_rider_down") && !F1Done("treat") && F1Done("run") && (F1Flag("residents") || F1Flag("b4_done")) && !F1Flag("f1_fetch_noted"))
            {
                F1Set("f1_fetch_noted");
                Toast("THE REFUGE'S RESIDENTS FETCH KIT ARNO HOME AND PATCH HER UP");
                Story.Story.Note("f1:fetched");
            }
            if (F1Done("treat") && F1Route("treat") != F1Fetched && !F1Flag("f1_bandaged"))
            {
                F1Set("f1_bandaged");
                using (Inventory.Source("HANDED OVER")) Inventory.TakeItem("med_bandage", 1);
            }
        }

        // ------------------------------------------------------------------ resolution

        void F1Resolve()
        {
            if (!F1Done("run") || F1Flag("f1_resolved")) return;
            if (F1Flag("f1_fetch_noted") && !F1Done("treat")) return;                                  // let the rescue register first
            F1Set("f1_resolved");
            string e = StoryLibrary.F1EndingOf(F1Route("control")) ?? "better_bargain";
            int score = F1StrengthNow(out string parts);
            bool strong = score >= StoryLibrary.F1Strong;
            F1Set("f1_ending_" + e); F1Set(strong ? "f1_strong" : "f1_weak"); F1Set("campaign_done");
            var lines = StoryLibrary.F1EndingLines(e, strong);
            Journal.Add("STORY", StoryLibrary.F1EndingTitle(e) + (strong ? "" : " (HARD WON)") + ": " + lines[0]);
            Journal.Add("STORY", "WHAT STAYS HARD: " + lines[1]);
            Journal.Add("STORY", "FROM NOW ON: " + lines[2]);
            Journal.Add("STORY", "HOW IT WAS WEIGHED: " + parts + ".");
            bool june = F1Flag("broadcast:full") || F1Flag("broadcast:redacted");
            MadMax.Audio.RadioNetwork.Flash(StoryLibrary.F1Headline(e, june), 120f);
            // standing with the factions and fuel prices along the routes
            var towns = new List<MadMax.World.Settlement>();
            for (int i = 0; i < StoryLibrary.F1Keys.Length; i++)
            {
                var st = Market.Near(StoryAnchors.Get(StoryLibrary.F1DropAnchor(i)));
                if (st != null && !towns.Contains(st)) towns.Add(st);
            }
            string fuel = "res:" + (int)ResourceType.Fuel, diesel = "res:" + (int)ResourceType.Diesel;
            switch (e)
            {
                case "open_roads":
                    Factions.Shift(Faction.Settlers, strong ? 10 : 5);
                    Factions.Shift(Faction.Nomads, strong ? 5 : 3, false);
                    Factions.Shift(Faction.FuelGuild, -8, false);
                    foreach (var st in towns) { Market.Sold(st, fuel, strong ? 15 : 8); Market.Sold(st, diesel, strong ? 15 : 8); }
                    break;
                case "break_locks":
                    Factions.Shift(Faction.FuelGuild, -20);
                    Factions.Shift(Faction.Settlers, strong ? 5 : -2);
                    foreach (var st in towns) { Market.Bought(st, fuel, strong ? 10 : 25); Market.Bought(st, diesel, strong ? 10 : 25); }
                    break;
                default:
                    Factions.Shift(Faction.FuelGuild, strong ? 8 : 4);
                    Factions.Shift(Faction.Settlers, strong ? 6 : 3);
                    foreach (var st in towns) { Market.Sold(st, fuel, strong ? 20 : 10); Market.Sold(st, diesel, strong ? 20 : 10); }
                    break;
            }
            ArcCPayoff("F1", StoryLibrary.F1Payoff(null));
            Toast(StoryLibrary.F1EndingTitle(e) + (strong ? "" : ", HARD WON") + ": THE ROADS HEAR IT ON WASTETALK");
            Story.Story.Note("f1:resolved");
        }

        // ------------------------------------------------------------------ the last scene

        void F1Home()
        {
            if (!F1Done("resolve")) return;
            if (!F1Flag("f1_radio_set") && Build && Build.Structures && StoryAnchors.Has("garage_yard"))
            {
                F1Set("f1_radio_set");
                ArcCPut("garage_yard", "table", F1RadioTableAt, 0f);
                var radio = ArcCPut("garage_yard", "radio", F1RadioAt, 180f);
                if (radio && radio.TryGetComponent<MadMax.Audio.RadioReceiver>(out var rx) && !rx.on) rx.TogglePower();
            }
            if (Q7State.At("F1", "answer") && !F1Flag("f1_call_played") && Q7Near("garage_yard", 16f)) F1Call();
        }

        void F1Call()
        {
            F1Set("f1_call_played");
            foreach (var line in new[]
            {
                "RADIO: ...EVENING. THIS IS A DRIVER ON THE BEND ROAD, HALF A LOAD FOR " + StoryLibrary.F1TargetName() + ". NOBODY SENT ME. THAT'S NEW.",
                "RADIO: THERE'S A LIGHT ON UNDER THE OLD SIGN. MIKE'S GARAGE, IS IT? I'VE DRIVEN PAST IT DARK FOR YEARS.",
                "RADIO: DOES THE LIGHT MEAN I CAN STOP FOR THE NIGHT? ...ANYONE THERE?",
            })
            {
                Journal.Add("RADIO", line);
                f1Captions.Enqueue(line);
            }
            f1CaptionAt = 0f;
        }

        string F1RadioPrompt(WastelandGame g) =>
            Q7State.At("F1", "answer") ? "THE RADIO ON THE TABLE: A DRIVER ASKS IF SHE CAN STOP FOR THE NIGHT  [E] ANSWER HER  [T] LET THE LIGHT ANSWER" : null;

        void F1RadioUse(WastelandGame g, bool secondary)
        {
            if (!Q7State.At("F1", "answer")) return;
            if (!F1Flag("f1_call_played")) F1Call();
            string[] lines = secondary
                ? new[] { "YOU LEAVE THE HANDSET WHERE IT IS. A WHILE LATER, HEADLIGHTS SLOW AT THE BEND AND TURN IN UNDER THE LIGHT." }
                : new[] { "YOU: THE LIGHT'S ON FOR YOU. THERE'S A BED, AND THE KETTLE'S HOT.", "RADIO: ...COPY THAT. TWENTY MINUTES. I'LL BRING THE NEWS." };
            foreach (var l in lines) Journal.Add("RADIO", l);
            f1Captions.Clear();                                                                         // the quest closes with this: no captions queue after it
            Toast(lines[lines.Length - 1]);
            F1Set(secondary ? "f1_silent" : "f1_answered");
            ArcCPayoff("F1", StoryLibrary.F1Payoff(secondary ? StoryLibrary.F1Silent : StoryLibrary.F1Answer));
            Story.Story.Note(secondary ? "f1:silent" : "f1:answered");
        }
    }
}
