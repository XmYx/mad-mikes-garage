using System;
using System.Collections.Generic;
using MadMax.Game;
using MadMax.RPG;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A companion as saved: who they are (the profile is rebuilt from id/role/seed/kind), their order,
    /// where they stand, their pack and the fleet vehicle they drive.</summary>
    [Serializable]
    public class CompanionSave
    {
        public string id, kind, pack;
        public int role, seed, order, vehicle = -1;
        public Vector3 position, post;
    }

    /// <summary>Recruited companions (roadmap 20): ordinary people flagged <see cref="NpcSave.Companion"/> whom the
    /// director leaves alone. They follow the player (on foot, in the passenger seat, or driving a second vehicle behind
    /// them), wait where told, guard a claimed base (counted in its defence), fight what threatens the player and carry
    /// a 25 kg pack. Recruited by charisma or for pay; one, two with CHA 7, three with CHA 9. Authority only; saved.</summary>
    public static class Companions
    {
        public static readonly List<Npc> Live = new List<Npc>();
        static List<CompanionSave> pending;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Live.Clear(); pending = null; borrowed.Clear(); LastFireFighter = null; LastMedic = null; medicNext = 0f; warming.Clear(); heaterDay = -1; LastHeaterLit = null; }

        public const int HireScrap = 80;

        public static int Max(WastelandGame g)
        {
            int cha = g.Stats.Attribute(Attr.Charisma);
            return 1 + (cha >= 7 ? 1 : 0) + (cha >= 9 ? 1 : 0);
        }

        public static bool Full(WastelandGame g) { Prune(); return Live.Count >= Max(g); }

        /// <summary>This person walks with the player (or will, once loaded) — the director must not spawn them.</summary>
        public static bool Has(string id)
        {
            foreach (var n in Live) if (n && n.Profile.id == id) return true;
            if (pending != null) foreach (var s in pending) if (s.id == id) return true;
            return false;
        }

        /// <summary>Who can be asked: ordinary folk, not traders, bosses or raiders still fighting.</summary>
        public static bool CanAsk(Npc n) => n && n.Alive && !n.companion && !n.Hostile && n.convoy == null &&
            (n.Profile.role == NpcRole.Wanderer || n.Profile.role == NpcRole.Resident || (n.Profile.Raider && n.Surrendered));

        public static void Recruit(WastelandGame g, Npc n)
        {
            if (!n || n.companion) return;
            n.State.Set(NpcSave.Companion);
            n.State.Set(NpcSave.Surrendered, false);
            n.companion = true; n.order = 0; n.leaving = false; n.raiding = false; n.aggro = false;
            n.transform.SetParent(null, true);
            n.EnsurePack();
            NpcDirector.Instance?.Release(n);
            Live.Add(n);
            g.Stats.Practice(Skill.Speech, 5f);
            g.Toast(n.Profile.Name + " JOINS YOU  ([E] ORDERS  [T] PACK)");
        }

        /// <summary>Part ways: they go back to their old life (spill what they carry for you first).</summary>
        public static void Dismiss(WastelandGame g, Npc n)
        {
            if (!n) return;
            Live.Remove(n);
            if (n.Driving) n.LeaveWheel();
            if (n.Riding) n.Unboard();
            if (n.pack) n.pack.Spill();
            n.State.Set(NpcSave.Companion, false);
            n.companion = false;
            n.LetGo();
            g.Toast(n.Profile.Name + " GOES THEIR OWN WAY");
        }

        public static void Lost(Npc n)
        {
            Live.Remove(n);
            n.State.Set(NpcSave.Companion, false);
            WastelandGame.Instance?.Toast(n.Profile.Name + " IS DEAD");
        }

        static void Prune() { for (int i = Live.Count - 1; i >= 0; i--) if (!Live[i] || !Live[i].Alive) Live.RemoveAt(i); }

        /// <summary>Companions guarding inside a claim (for its defence score).</summary>
        public static int GuardsAt(Vector3 at, float radius)
        {
            int k = 0;
            foreach (var n in Live) if (n && n.Alive && n.order == 2 && (n.home - at).sqrMagnitude < radius * radius) k++;
            return k;
        }

        /// <summary>Bring loaded companions into the world beside the player (or at their posts).</summary>
        public static void Tick(WastelandGame g)
        {
            Prune();
            FireDrill(g);
            MedicDrill(g);
            MorningRoutine(g);
            if (pending == null || !g.Player || !DeformableTerrainReady()) return;
            var list = pending; pending = null;
            foreach (var s in list)
            {
                var p = (s.id != null && s.id.StartsWith("cast:") && g.World != null ? MadMax.Story.StoryCast.Profile(s.id.Substring(5), g.World.seed) : null)   // story cast keep their authored name and looks
                        ?? NpcProfile.Make(s.id, (NpcRole)s.role, s.seed, s.kind);
                if (NpcRegistry.IsDead(p.id)) continue;
                var pos = s.order == 0 ? g.Player.transform.position - g.Player.transform.forward * 2.5f + g.Player.transform.right * (Live.Count - 1) : s.position;
                var t = MadMax.World.DeformableTerrain.Instance;
                pos.y = t.Height(pos.x, pos.z) + 0.1f;
                var n = Npc.Spawn(p, pos, 0f, null, g.propMaterial);
                n.companion = true; n.order = s.order;
                if (s.order != 0) { n.home = s.post; n.homeRadius = s.order == 2 ? 10f : 1f; }
                n.EnsurePack();
                if (!string.IsNullOrEmpty(s.pack)) n.pack.LoadState(s.pack);
                Live.Add(n);
                if (s.vehicle >= 0 && s.vehicle < g.Fleet.Count && g.Fleet[s.vehicle]) n.TakeWheel(g.Fleet[s.vehicle]);
            }
        }

        // ------------------------------------------------------------------ morning routine (2026-10-09)

        /// <summary>Dawn window (game hours) and how near the companion and the player must be to the fleet car.</summary>
        public const float DawnFrom = 5.5f, DawnTo = 8.5f, MorningReach = 60f;
        static int heaterDay = -1;
        static readonly Dictionary<VehicleSystems, float> warming = new Dictionary<VehicleSystems, float>();
        /// <summary>The companion who lit the heater this morning (tests).</summary>
        public static Npc LastHeaterLit;

        /// <summary>On a cold dawn a companion with a trip heater in their pack lights it under the fleet car nearest the
        /// player (a litre from its tank), once a day, so the cold start is waiting when the player walks out.</summary>
        static void MorningRoutine(WastelandGame g)
        {
            if (warming.Count > 0)
            {
                List<VehicleSystems> done = null;
                foreach (var kv in warming)
                {
                    if (!kv.Key || kv.Key.Started || Time.time > kv.Value) (done ??= new List<VehicleSystems>()).Add(kv.Key);
                    else { kv.Key.KeepWarm(Time.deltaTime); MadMax.Audio.Sfx.Loop(kv.Key, "fire", 0.25f, 1.4f, 12f); }
                }
                if (done != null) foreach (var s in done) { if (s) MadMax.Audio.Sfx.Loop(s, "fire", 0f); warming.Remove(s); }
            }
            var h = MadMax.World.DayNight.Hours;
            if (heaterDay == MadMax.World.DayNight.Day || h < DawnFrom || h > DawnTo || !g.Player || g.Current) return;
            if (MadMax.World.Weather.Temperature >= MadMax.Building.BlockHeater.ColdBelow) return;
            LightHeaterNow(g);
        }

        /// <summary>The morning routine's step without the clock checks (tests, and the dawn tick). True if lit.</summary>
        public static bool LightHeaterNow(WastelandGame g)
        {
            VehicleDriver car = null; float bd = MorningReach * MorningReach;
            var at = g.Player.transform.position;
            foreach (var v in g.Fleet)
            {
                if (!v || v.aiDriven || !v.TryGetComponent<VehicleSystems>(out var s) || s.Started || s.BlockWarm || s.Temperature >= 45f || warming.ContainsKey(s)) continue;
                float d = (v.transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; car = v; }
            }
            if (!car) return false;
            var sys = car.GetComponent<VehicleSystems>();
            if (sys.tankIced || sys.fuel < WastelandGame.TripHeatLitres + 0.5f) return false;
            foreach (var n in Live)
            {
                if (!n || !n.Alive || n.Driving || n.Riding || !n.pack || n.pack.inventory.GetItem(WastelandGame.TripHeater) <= 0) continue;
                if ((n.transform.position - car.transform.position).sqrMagnitude > MorningReach * MorningReach) continue;
                sys.fuel -= WastelandGame.TripHeatLitres;
                warming[sys] = Time.time + WastelandGame.TripHeatSeconds;
                heaterDay = MadMax.World.DayNight.Day;
                LastHeaterLit = n;
                g.Toast(n.KnownName + " LIT THE TRIP HEATER UNDER THE " + WastelandGame.Name(car) + ": IT'LL BE WARM IN A MINUTE");
                MadMax.Game.Journal.Add("CREW", n.KnownName + " WARMED THE " + WastelandGame.Name(car) + " AT DAWN");
                return true;
            }
            return false;
        }

        /// <summary>A companion's heater is burning under this vehicle.</summary>
        public static bool Warming(VehicleSystems s) => s && warming.ContainsKey(s);

        // ------------------------------------------------------------------ walkie-talkie orders (2026-10-09)

        public enum Order { Come, Hold, BringCar }
        /// <summary>A spare fleet car must be this close to a companion for BRING A CAR.</summary>
        public const float CarReach = 40f;
        /// <summary>The last order heard over the radio and how many answered (tests).</summary>
        public static Order LastOrder; public static int LastAnswered;

        /// <summary>Companions with a handheld radio in their pack (they hear radio orders anywhere).</summary>
        public static List<Npc> OnRadio()
        {
            Prune();
            var l = new List<Npc>();
            foreach (var n in Live)
                if (n && n.Alive && n.pack && n.pack.inventory.GetItem(MadMax.Game.SafetyTools.HandRadio) > 0) l.Add(n);
            return l;
        }

        /// <summary>The fleet car nearest <paramref name="n"/> (or any radio companion) within <see cref="CarReach"/> that
        /// nobody drives.</summary>
        public static VehicleDriver CarFor(WastelandGame g, Npc n)
        {
            VehicleDriver best = null; float bd = CarReach * CarReach;
            foreach (var c in n ? new List<Npc> { n } : OnRadio())
            {
                if (c.Driving) continue;
                foreach (var v in g.Fleet)
                {
                    if (!v || v == g.Current || v.aiDriven || !v.driveable || v.Occupied) continue;
                    float d = (v.transform.position - c.transform.position).sqrMagnitude;
                    if (d < bd) { bd = d; best = v; }
                }
            }
            return best;
        }

        /// <summary>Give an order over the handheld radio: COME (follow; drivers keep driving behind), HOLD (stay put,
        /// a driver parks) or BRING A CAR (the nearest spare fleet car, driven behind the player). Returns how many answered.</summary>
        public static int RadioOrder(WastelandGame g, Order o)
        {
            if (!g || g.Inventory.GetItem(MadMax.Game.SafetyTools.HandRadio) <= 0) return 0;
            int k = 0;
            bool carSent = false;
            foreach (var n in OnRadio())
            {
                switch (o)
                {
                    case Order.Come: n.order = 0; k++; break;
                    case Order.Hold:
                        if (n.Driving) n.LeaveWheel();
                        n.order = 1; n.home = n.transform.position; n.homeYaw = n.transform.eulerAngles.y; n.homeRadius = 1f; k++; break;
                    case Order.BringCar:
                        if (carSent || n.Driving) { if (n.Driving) { n.order = 0; k++; carSent = true; } break; }
                        var car = CarFor(g, n);
                        if (!car) break;
                        n.order = 0; n.TakeWheel(car); carSent = true; k++; break;
                }
            }
            LastOrder = o; LastAnswered = k;
            MadMax.Audio.Sfx.Play2D("beep", 0.4f, 1.1f);
            g.Toast(k == 0 ? "RADIO: STATIC. NOBODY ANSWERS" : o == Order.Come ? "RADIO: \"ON OUR WAY.\"" : o == Order.Hold ? "RADIO: \"HOLDING HERE.\"" : "RADIO: \"BRINGING IT ROUND.\"");
            return k;
        }

        // ------------------------------------------------------------------ engine fires

        public const string Extinguisher = "tool_extinguisher";
        /// <summary>A companion this close to a burning fleet car goes for it.</summary>
        public const float FireReach = 30f;
        /// <summary>Companion → the compartment whose extinguisher they took (it goes back when the fight ends).</summary>
        static readonly Dictionary<Npc, MadMax.Building.Container> borrowed = new Dictionary<Npc, MadMax.Building.Container>();

        /// <summary>A fleet car's engine catches with a companion near: the nearest one with a bottle (their own, or the
        /// one riding in the car's compartments) climbs out and beats it (<see cref="Npc.FightFire"/>); a borrowed bottle
        /// goes back where it was. The trip kit pays off without the player leaving the seat.</summary>
        static void FireDrill(WastelandGame g)
        {
            if (borrowed.Count > 0)
            {
                List<Npc> back = null;
                foreach (var kv in borrowed) if (!kv.Key || !kv.Key.fireTarget) (back ??= new List<Npc>()).Add(kv.Key);
                if (back != null)
                    foreach (var n in back)
                    {
                        var box = borrowed[n];
                        borrowed.Remove(n);
                        if (box) box.inventory.AddItem(Extinguisher);                               // put back (also if they fell: the bottle lies with the car)
                    }
            }
            if (Live.Count == 0) return;
            foreach (var v in g.Fleet)
            {
                if (!v || !v.TryGetComponent<VehicleSystems>(out var vs) || !vs.Burning || Mathf.Abs(v.ForwardSpeed) > 3f) continue;
                bool taken = false;
                foreach (var n in Live) if (n && n.fireTarget == vs) taken = true;
                if (taken) continue;
                Npc best = null; float bd = FireReach;
                foreach (var n in Live)
                {
                    if (!n || !n.Alive || n.fireTarget || n.Hostile || n.Surrendered) continue;
                    float d = Vector3.Distance(n.transform.position, v.transform.position);
                    if (d < bd) { bd = d; best = n; }
                }
                if (!best) continue;
                MadMax.Building.Container from = null;
                if (!(best.pack && best.pack.inventory.GetItem(Extinguisher) > 0))
                {
                    var storage = VehicleStorage.For(v);
                    if (storage) foreach (var c in storage.compartments) if (c && c.container && c.container.inventory.GetItem(Extinguisher) > 0) { from = c.container; break; }
                    if (!from) continue;                                                            // no bottle anywhere: nothing to fight it with
                    from.inventory.TakeItem(Extinguisher);
                    borrowed[best] = from;
                }
                if (best.Driving) best.LeaveWheel();
                best.Unboard();
                best.FightFire(vs, MadMax.Game.ExtinguisherTool.Bursts);
                LastFireFighter = best;
                g.Toast(best.KnownName + (from ? " GRABS THE EXTINGUISHER FROM THE " + from.title : " GOES FOR THE FIRE WITH THEIR EXTINGUISHER"));
            }
        }

        /// <summary>The last companion sent to an engine fire (tests).</summary>
        public static Npc LastFireFighter;

        // ------------------------------------------------------------------ the medic

        public const string FirstAidKit = "med_firstaid";
        /// <summary>A companion this close comes to dress the player's wounds once the fight is over.</summary>
        public const float MedicReach = 30f, QuietSeconds = 6f;
        static float medicNext;

        /// <summary>A companion with the knack for wounds (a nurse by origin, or the half-a-book medic): their dressings
        /// are as good as the player's own. Anyone else leaves <see cref="MadMax.RPG.Injury.rough"/> dressings.</summary>
        public static bool Patches(NpcProfile p)
        {
            if (p == null) return false;
            string o = p.origin >= 0 && p.origin < NpcLore.Origin.Length ? NpcLore.Origin[p.origin] : "";
            string s = p.secret >= 0 && p.secret < NpcLore.Secret.Length ? NpcLore.Secret[p.secret] : "";
            if (o.Contains("NURSE") || o.Contains("MEDIC") || s.Contains("MEDIC")) return true;
            var st = NpcRegistry.Peek(p.id);
            return st != null && (st.Has(NpcSave.StudiedWounds) || st.tended >= KnackAfter);
        }

        // ---- learning the knack (2026-10-09): practice or the first-aid book makes anyone a proper medic
        /// <summary>Dressings a companion must have done before theirs stop being rough.</summary>
        public const int KnackAfter = 4;
        public const string FirstAidBook = "book_first_aid";

        /// <summary>A companion finished a dressing: count it; returns true the moment they got the knack.</summary>
        public static bool Practise(NpcProfile p)
        {
            if (p == null || Patches(p)) return false;
            var st = NpcRegistry.Get(p);
            st.tended++;
            return st.tended >= KnackAfter;
        }

        /// <summary>The player hands a companion the first-aid book: their next dressings are proper ones.</summary>
        public static bool Study(NpcProfile p, MadMax.Items.Inventory from)
        {
            if (p == null || Patches(p) || from == null || !from.TakeItem(FirstAidBook)) return false;
            NpcRegistry.Get(p).Set(NpcSave.StudiedWounds);
            return true;
        }

        /// <summary>What a companion could treat the player with: a first-aid kit in their pack, else one in the
        /// compartments of a fleet car within 20 m (<paramref name="from"/>), else bandages / a splint in their pack.</summary>
        public static bool MedicSupplies(WastelandGame g, Npc n, out MadMax.Building.Container from)
        {
            from = null;
            if (n.pack && n.pack.inventory.GetItem(FirstAidKit) > 0) return true;
            foreach (var v in g.Fleet)
            {
                if (!v || Vector3.Distance(v.transform.position, g.Player.transform.position) > 20f) continue;
                var storage = VehicleStorage.For(v);
                if (storage) foreach (var c in storage.compartments) if (c && c.container && c.container.inventory.GetItem(FirstAidKit) > 0) { from = c.container; return true; }
            }
            return n.pack && (n.pack.inventory.GetItem("med_bandage") > 0 || n.pack.inventory.GetItem("med_splint") > 0);
        }

        /// <summary>The player bleeds or has a broken bone and nobody is fighting: the nearest companion with
        /// supplies walks over and treats them (<see cref="Npc.Tend"/>) — the trip kit's dressings used without the
        /// player opening a menu.</summary>
        static void MedicDrill(WastelandGame g)
        {
            if (Live.Count == 0 || Time.time < medicNext || !g.Player || g.Current || g.Vitals.Dead || !g.NeedsTending) return;
            if (g.Vitals.SinceHurt < QuietSeconds) return;
            foreach (var n in Live) if (n && (n.tending || n.Foe)) return;                           // one at a time; not mid-fight
            medicNext = Time.time + 2f;
            Npc best = null; float bd = MedicReach; MadMax.Building.Container bestFrom = null;
            foreach (var n in Live)
            {
                if (!n || !n.Alive || n.fireTarget || n.Hostile || n.Surrendered || n.Driving) continue;
                float d = Vector3.Distance(n.transform.position, g.Player.transform.position);
                if (Patches(n.Profile)) d -= 12f;                                                       // the one who knows how comes first
                if (d >= bd || !MedicSupplies(g, n, out var from)) continue;
                bd = d; best = n; bestFrom = from;
            }
            if (!best) { medicNext = Time.time + 20f; return; }
            best.Unboard();
            best.Tend(bestFrom);
            LastMedic = best;
            g.Toast(best.KnownName + (bestFrom ? " FETCHES THE FIRST-AID KIT FROM THE " + bestFrom.title : " COMES OVER TO PATCH YOU UP"));
        }

        /// <summary>The last companion sent to tend the player (tests).</summary>
        public static Npc LastMedic;

        static int IndexOf(IReadOnlyList<VehicleDriver> list, VehicleDriver v) { for (int i = 0; i < list.Count; i++) if (list[i] == v) return i; return -1; }

        static bool DeformableTerrainReady() => MadMax.World.DeformableTerrain.Instance && MadMax.World.DeformableTerrain.Instance.World != null;

        public static void Save(WastelandGame g, SaveData d)
        {
            Prune();
            d.companions = new List<CompanionSave>();
            foreach (var n in Live)
            {
                var p = n.Profile;
                d.companions.Add(new CompanionSave
                {
                    id = p.id, role = (int)p.role, seed = p.seed, kind = p.kind, order = n.order, position = n.transform.position, post = n.home,
                    pack = n.pack ? n.pack.SaveState() : null, vehicle = n.Driving ? IndexOf(g.Fleet, n.DrivenCar) : -1
                });
            }
            if (pending != null) d.companions.AddRange(pending);
        }

        public static void Load(SaveData d)
        {
            Live.Clear();
            pending = d.companions != null && d.companions.Count > 0 ? new List<CompanionSave>(d.companions) : null;
        }
    }
}
