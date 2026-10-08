using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-08: the trip heater (a litre of fuel under a frozen engine, away from any
    /// power), burned-out wrecks that tell whose they were and how the nearest town took it, and the gang behind a
    /// stolen part (<c>Trade.StolenBy</c>, asked of the salvage vendor in <c>Dialogue.StolenTrail</c>).</summary>
    public partial class WastelandGame
    {
        // ------------------------------------------------------------------ trip heater

        public const string TripHeater = "use_trip_heater";
        public const float TripHeatSeconds = 75f, TripHeatLitres = 1f, TripHeatReach = 4f;
        /// <summary>The vehicle the trip heater is burning under (null = none).</summary>
        public VehicleSystems TripHeating { get; private set; }
        float tripHeatUntil, tripPuff;

        /// <summary>Set the trip heater under the vehicle in or beside (<see cref="TripHeatReach"/>) the player: burns
        /// <see cref="TripHeatLitres"/> of fuel from a can in the pack (else from the vehicle's own tank unless it is
        /// iced) and keeps the engine warm for <see cref="TripHeatSeconds"/>, like a block heater. The heater is kept.</summary>
        public bool StartTripHeater(VehicleDriver v = null)
        {
            if (Inventory.GetItem(TripHeater) <= 0) return false;
            if (!v) v = Current ? Current : FindNearby(TripHeatReach);
            var sys = v ? v.GetComponent<VehicleSystems>() : null;
            if (!sys) { Toast("SET THE HEATER BESIDE A VEHICLE"); return false; }
            if (TripHeating == sys && Time.time < tripHeatUntil) { Toast("THE HEATER IS ALREADY BURNING UNDER IT"); return false; }
            if (sys.Started) { Toast("THE ENGINE IS ALREADY RUNNING"); return false; }
            if (Weather.Temperature >= BlockHeater.ColdBelow && !sys.tankIced) { Toast("NO NEED: IT ISN'T COLD ENOUGH"); return false; }
            string from;
            if (BurnCanFuel(TripHeatLitres)) from = "A LITRE FROM THE CAN";
            else if (!sys.tankIced && sys.fuel >= TripHeatLitres + 0.5f) { sys.fuel -= TripHeatLitres; from = "A LITRE FROM THE TANK"; }
            else { Toast(sys.tankIced ? "THE HEATER NEEDS A LITRE OF FUEL IN A CAN (THE TANK IS ICED)" : "THE HEATER NEEDS A LITRE OF FUEL"); return false; }
            if (TripHeating && TripHeating != sys) StopTripHeater(null);
            TripHeating = sys; tripHeatUntil = Time.time + TripHeatSeconds;
            MadMax.Audio.Sfx.Play("fire", sys.transform.position, 0.5f, 1.4f);
            Toast("TRIP HEATER LIT UNDER THE " + Name(v) + " (" + from + "): GIVE IT A MINUTE");
            Hints.Show("trip_heater", "A COLD ENGINE WON'T CATCH AND AN ICED TANK WON'T FLOW: THE HEATER WARMS BOTH IN ABOUT A MINUTE");
            return true;
        }

        void StopTripHeater(string why)
        {
            if (TripHeating) MadMax.Audio.Sfx.Loop(TripHeating, "fire", 0f);
            TripHeating = null;
            if (why != null) Toast(why);
        }

        /// <summary>Burn <paramref name="litres"/> of petrol, diesel or ethanol from a container in the pack.</summary>
        public bool BurnCanFuel(float litres)
        {
            foreach (var d in FluidContainers.All)
                foreach (var c in CansOf(d.id))
                {
                    if (c.Empty || c.litres < litres) continue;
                    var t = c.mix.Main;
                    if (t != ResourceType.Fuel && t != ResourceType.Diesel && t != ResourceType.Ethanol) continue;
                    c.litres -= litres;
                    if (c.litres < 0.01f) c.Clear();
                    return true;
                }
            return false;
        }

        void UpdateTripHeater()
        {
            if (!TripHeating) { TripHeating = null; return; }
            if (TripHeating.Started) { StopTripHeater("THE ENGINE CAUGHT: HEATER PACKED AWAY"); return; }
            if (Time.time > tripHeatUntil) { StopTripHeater("THE TRIP HEATER BURNED DOWN"); return; }
            TripHeating.KeepWarm(Time.deltaTime * 1.5f);
            MadMax.Audio.Sfx.Loop(TripHeating, "fire", 0.3f, 1.3f, 14f);
            if (Time.time > tripPuff)
            {
                tripPuff = Time.time + 0.7f;
                Fx.Smoke(TripHeating.transform.position + Vector3.up * 0.15f, Vector3.up * 0.5f, 0.22f, new Color(0.32f, 0.3f, 0.28f, 0.5f), 1.6f);
            }
        }

        // ------------------------------------------------------------------ burned-out wrecks: whose, and the town's word

        public const float BurnedTownReach = 1500f, BurnedTellReach = 9f;
        /// <summary>The last burned-out wreck's story told on approach (tests).</summary>
        public static string LastBurnedStory;

        static string Possessive(string name) => name.EndsWith("S") ? name + "'" : name + "'S";

        /// <summary>Whose the vehicle was: "YOUR", "A TRADER'S", "THE &lt;GANG&gt;'S" or "SOMEBODY'S".</summary>
        string BurnedWhose(VehicleDriver v, out bool raider, out string gang)
        {
            raider = false; gang = null;
            if (InFleet(v)) return "YOUR";
            var dir = NpcDirector.Instance;
            if (dir)
                foreach (var c in dir.Convoys)
                    foreach (var a in c.cars)
                        if (a && a.Vehicle == v)
                        {
                            if (!c.raiders) return "A TRADER'S";
                            raider = true; gang = c.Gang;
                            return "THE " + Possessive(gang);
                        }
            return "SOMEBODY'S";
        }

        /// <summary>The nearest town's word on it: raiders burning out near the player is cheered (the town's standing
        /// rises), a trader's car is mourned, the player's own is talked about. Posted to the town news.</summary>
        void BurnedReaction(VehicleDriver v, Vector3 p, string whose, bool raider, string gang)
        {
            var town = NearestTown(p);
            if (town == null || Vector2.Distance(town.pos, new Vector2(p.x, p.z)) > BurnedTownReach) return;
            string tn = Market.TownName(town), car = Name(v);
            if (raider)
            {
                TownNews.Post(tn + " DRINKS TO A BURNED " + gang + " " + car, p);
                if (Player && Flat(Player.transform.position - p) < 120f)
                {
                    Factions.Shift(Factions.OfSettlement(town), 2);
                    Journal.Add("ROAD", tn + " DRINKS TO THE " + gang + " " + car + " THAT BURNED");
                }
            }
            else if (whose == "A TRADER'S") TownNews.Post(tn + " MOURNS A TRADER'S " + car + " BURNED ON THE ROAD", p);
            else if (whose == "YOUR") TownNews.Post("FOLK AT " + tn + " HEARD A " + car + " BURNED OUT", p);
        }

        /// <summary>One line of what happened, for a burned-out landmark ("THE RUSTMEN' PICKUP, BURNED OUT YESTERDAY -
        /// DUSTWATER DRANK TO IT").</summary>
        public string BurnedStory(BurnedOut b)
        {
            string car = b.name != null && b.name.StartsWith("BURNED-OUT ") ? b.name.Substring(11) : "WRECK";
            string whose = string.IsNullOrEmpty(b.whose) ? "SOMEBODY'S" : b.whose;
            float d = DayNight.TotalDays - b.day;
            string ago = d < 0.25f ? "NOT LONG AGO" : d < 1.5f ? "YESTERDAY" : Mathf.RoundToInt(d) + " DAYS AGO";
            string tail = "";
            var town = NearestTown(b.pos);
            if (town != null && Vector2.Distance(town.pos, new Vector2(b.pos.x, b.pos.z)) <= BurnedTownReach)
            {
                string tn = Market.TownName(town);
                tail = whose == "A TRADER'S" ? " - " + tn + " MOURNED IT" : whose == "YOUR" ? " - THE TALK OF " + tn : whose.StartsWith("THE ") ? " - " + tn + " DRANK TO IT" : "";
            }
            return whose + " " + car + ", BURNED OUT " + ago + tail;
        }

        void UpdateBurnedStories()
        {
            if (Current || !Player) return;
            var me = Player.transform.position;
            for (int i = 0; i < BurnedOuts.Count; i++)
            {
                var b = BurnedOuts[i];
                if (b.told || Flat(b.pos - me) > BurnedTellReach) continue;
                b.told = true; BurnedOuts[i] = b;
                LastBurnedStory = BurnedStory(b);
                Toast(LastBurnedStory);
            }
        }

        // ------------------------------------------------------------------ stolen parts: the trail

        /// <summary>Where the gang that took a part rides now (their nearest raider convoy to <paramref name="from"/>).
        /// False when none of theirs is on the roads.</summary>
        public bool GangWhereabouts(string gang, Vector3 from, out Vector3 at)
        {
            at = default;
            var dir = NpcDirector.Instance;
            float bd = float.MaxValue; bool found = false;
            if (dir && !string.IsNullOrEmpty(gang))
                foreach (var c in dir.Convoys)
                {
                    if (!c.raiders || c.Gang != gang) continue;
                    var p = c.Position;
                    float d = Flat(p - from);
                    if (d < bd) { bd = d; at = p; found = true; }
                }
            return found;
        }

        /// <summary>The vendor named the gang: a waypoint on where they ride and a journal line.</summary>
        public void FollowStolenTrail(string id, string gang, Vector3 from)
        {
            string what = ItemName(id);
            if (GangWhereabouts(gang, from, out var at))
            {
                SetWaypoint(at, gang, true);
                Journal.Add("RAID", "THE " + gang + " SOLD ON YOUR " + what + " - THEY RIDE " + NpcLore.Distance(Flat(at - from)) + " " + NpcLore.Compass(at.x - from.x, at.z - from.z) + " (WAYPOINT SET)");
            }
            else Journal.Add("RAID", "THE " + gang + " SOLD ON YOUR " + what);
        }

        // ------------------------------------------------------------------ binoculars find news wrecks

        /// <summary>Through binoculars: an unfound wreck from the news within <see cref="SpotReach"/> (further from
        /// higher ground), ≤ 20° off the view in perspective, is pinned like a board's news wreck. Returns how many.</summary>
        public int SpotWrecks(Vector3 me, Camera cam)
        {
            int n = 0;
            foreach (var w in RoadWrecks)
            {
                if (Found(w)) continue;
                var at = new Vector3(w.x, w.y, w.z);
                if (!InSight(me, at, cam)) continue;
                bool pinned = false;
                foreach (var q in NewsPins) if (Flat(q - at) < 10f) { pinned = true; break; }
                if (pinned) continue;
                NewsPins.Add(at);
                n++;
            }
            return n;
        }

        /// <summary>The binocular sight check shared by camps and wrecks.</summary>
        public static bool InSight(Vector3 me, Vector3 at, Camera cam)
        {
            float reach = Mathf.Min(SpotReach * 2f, SpotReach + Mathf.Max(0f, me.y - at.y) * 12f);
            var to = at - me; to.y = 0f;
            if (to.magnitude > reach) return false;
            if (cam && !cam.orthographic && to.magnitude > 8f)
            {
                var f = cam.transform.forward; f.y = 0f;
                if (f.sqrMagnitude < 0.01f || Vector3.Angle(f, to) > 20f) return false;
            }
            return true;
        }

        void UpdateBinocularWrecks()
        {
            if (!BinocularsTool.Looking || Current || !Player || RoadWrecks.Count == 0) return;
            int n = SpotWrecks(Player.transform.position, Camera.main);
            if (n > 0) { Toast("SPOTTED THROUGH THE BINOCULARS: A WRECK FROM THE NEWS (ON THE MAP)"); Journal.Add("FOUND", "SPOTTED A WRECK FROM THE NEWS"); }
        }

        void UpdateTrail() => UpdateTripHeater();
    }
}
