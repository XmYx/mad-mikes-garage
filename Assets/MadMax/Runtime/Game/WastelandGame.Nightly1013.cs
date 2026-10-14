using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-13: stolen parts in the raider boss's car, hitchers who remember (and the news
    /// of those left on the road), the crew medic's reputation and towns calling for them on a bad day, rumoured caches
    /// on the LOOK FOR list, and convalescence at home (lying up in bed with a broken leg).</summary>
    public partial class WastelandGame
    {
        // ------------------------------------------------------------------ stolen parts in the boss car

        /// <summary>The raider gang whose boss drives <paramref name="v"/> (null: not a boss car).</summary>
        public static string BossCarGang(VehicleDriver v)
        {
            var d = NpcDirector.Instance;
            if (!v || !d) return null;
            foreach (var c in d.Convoys)
                if (c != null && c.raiders && c.cars.Count > 0 && c.cars[0] && c.cars[0].Vehicle == v) return c.Gang;
            return null;
        }

        /// <summary>The player opened the storage of <paramref name="v"/>: if it is a raider boss's car, the gang's stolen
        /// parts still on their way to market are strapped in the back. Returns how many came back.</summary>
        public int OpenBossCar(VehicleDriver v)
        {
            string gang = BossCarGang(v);
            if (gang == null || MadMax.Npc.Trade.CarriedBy(gang).Count == 0) return 0;
            if (Mathf.Abs(v.ForwardSpeed) > 1.5f) { Toast("THE " + gang + "'S BOSS CAR IS MOVING: STOP IT FIRST"); return 0; }
            var behind = v.transform.position - v.transform.forward * (VehicleLength(v) * 0.5f + 1.2f);
            int n = ReturnStolen(gang, behind, v.transform.right);
            if (n > 0) Toast("IN THE BACK OF THE " + gang + "'S BOSS CAR: " + (n == 1 ? "YOUR " + ItemName(LastReclaimed) : n + " OF YOUR STOLEN PARTS") + " ([E] TAKE)");
            return n;
        }

        static float VehicleLength(VehicleDriver v)
        {
            var b = new Bounds(v.transform.position, Vector3.zero); bool any = false;
            foreach (var r in v.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return any ? Mathf.Max(b.size.x, b.size.z) : 4.5f;
        }

        /// <summary>Spawn the gang's in-transit stolen parts in a row from <paramref name="at"/> along
        /// <paramref name="side"/>, take them off the market. Shared by the boss's body and the boss car.</summary>
        int ReturnStolen(string gang, Vector3 at, Vector3 side)
        {
            int n = 0;
            foreach (var id in MadMax.Npc.Trade.CarriedBy(gang))
            {
                if (!id.StartsWith("part:")) continue;
                var p = at + side.normalized * (n * 1.3f);
                if (DeformableTerrain.Instance) p.y = DeformableTerrain.Instance.Height(p.x, p.z) + 0.6f;
                var part = SpawnPart(id.Substring(5), p, Quaternion.LookRotation(side.sqrMagnitude > 0f ? side : Vector3.forward));
                if (!part) continue;
                MadMax.Npc.Trade.Recover(id);
                LastReclaimed = id;
                n++;
                Journal.Add("RAID", "TOOK BACK YOUR " + ItemName(id) + " FROM THE " + gang);
                TickLookFor("THE " + gang + " (YOUR " + ItemName(id) + ")");
            }
            return n;
        }

        // ------------------------------------------------------------------ hitchers remember

        /// <summary>Days a rescued survivor greets the player and trades cheaper; their bargain bonus.</summary>
        public const int RescueDays = 7;
        public const float RescueBargain = 0.1f;
        readonly Dictionary<MadMax.Npc.Npc, float> hitchSince = new Dictionary<MadMax.Npc.Npc, float>();
        /// <summary>The last road-news line about a survivor who walked in alone (tests).</summary>
        public static string LastWalkedIn;

        /// <summary>A survivor the player brought in within <see cref="RescueDays"/>.</summary>
        public static bool Rescued(NpcSave s) => s != null && s.rescuedDay >= 0 && DayNight.Day - s.rescuedDay < RescueDays;

        void NoteStranded(MadMax.Npc.Npc n) { if (n && !hitchSince.ContainsKey(n)) hitchSince[n] = DayNight.TotalDays; }

        void NoteRescued(MadMax.Npc.Npc n) { n.State.rescuedDay = DayNight.Day; hitchSince.Remove(n); }

        /// <summary>Nobody stopped: the survivor walked into <paramref name="town"/> alone and the town talks about it.</summary>
        void NoteWalkedIn(MadMax.Npc.Npc n, Settlement town)
        {
            float days = hitchSince.TryGetValue(n, out var since) ? DayNight.TotalDays - since : 0f;
            hitchSince.Remove(n);
            string how = days >= 1.5f ? "AFTER " + Mathf.RoundToInt(days) + " DAYS ON THE ROAD" : days >= 0.75f ? "AFTER A DAY ON THE ROAD" : "ON FOOT, NOBODY STOPPED";
            LastWalkedIn = "A TRADER WHOSE CAR BURNED WALKED INTO " + Market.TownName(town) + " " + how;
            TownNews.Post(LastWalkedIn, new Vector3(town.pos.x, 0f, town.pos.y));
        }

        // ------------------------------------------------------------------ the crew medic's reputation

        /// <summary>Treatments by the crew per town (saved); a town with <see cref="MedicCallAt"/> calls for the medic on a
        /// bad day (<see cref="MedicCallChance"/> a day), the call stays open until the end of the next day.</summary>
        public readonly Dictionary<int, int> MedicWord = new Dictionary<int, int>();
        public readonly Dictionary<int, int> MedicCalls = new Dictionary<int, int>();             // town → last day to answer
        public const int MedicCallAt = 2, MedicFee = 30;
        public const float MedicCallChance = 0.3f, MedicAnswerReach = 25f;
        readonly Dictionary<int, int> medicNewsDay = new Dictionary<int, int>();
        int medicRollDay = -1;
        /// <summary>The last town that called for / was helped by the crew medic (tests).</summary>
        public static string LastMedicCall, LastMedicHelp;

        /// <summary>A companion treated a townsperson: the town hears of it (news once a day per town).</summary>
        void NoteMedicWord(MadMax.Npc.Npc medic, MadMax.Npc.Npc patient)
        {
            if (World == null) return;
            var p = patient.transform.position;
            var town = World.SettlementAt(p.x, p.z) ?? NearestTown(p);
            if (town == null || Vector2.Distance(town.pos, new Vector2(p.x, p.z)) > town.radius + 400f) return;
            MedicWord[town.index] = (MedicWord.TryGetValue(town.index, out int had) ? had : 0) + 1;
            if (medicNewsDay.TryGetValue(town.index, out int day) && day == DayNight.Day) return;
            medicNewsDay[town.index] = DayNight.Day;
            TownNews.Post(medic.KnownName + " OF " + Stats.name + "'S CREW PATCHED UP " + patient.KnownName, new Vector3(town.pos.x, 0f, town.pos.y));
        }

        /// <summary>A companion with the knack on the crew (the medic towns ask for); reach ≤ 0 = anywhere.</summary>
        public static MadMax.Npc.Npc CrewMedic(Vector3 near, float reach)
        {
            foreach (var n in Companions.Live)
                if (n && n.Alive && Companions.Patches(n.Profile) && (reach <= 0f || Flat(n.transform.position - near) < reach)) return n;
            return null;
        }

        /// <summary>Once a day: towns that know the crew medic may call for them (seeded per town and day).</summary>
        public int RollMedicCalls(bool force = false)
        {
            if (World == null || (!force && medicRollDay == DayNight.Day)) return 0;
            medicRollDay = DayNight.Day;
            foreach (var t in new List<int>(MedicCalls.Keys)) if (MedicCalls[t] < DayNight.Day) MedicCalls.Remove(t);
            if (!CrewMedic(Vector3.zero, 0f)) return 0;
            int k = 0;
            foreach (var kv in MedicWord)
            {
                if (kv.Value < MedicCallAt || MedicCalls.ContainsKey(kv.Key) || kv.Key < 0 || kv.Key >= World.settlements.Count) continue;
                var rnd = new System.Random(Market.Seed(seed, kv.Key * 7919 + DayNight.Day * 104729 + 13));
                if (!force && rnd.NextDouble() >= MedicCallChance) continue;
                var town = World.settlements[kv.Key];
                MedicCalls[kv.Key] = DayNight.Day + 1;
                LastMedicCall = Market.TownName(town);
                MadMax.Audio.RadioNetwork.Flash(LastMedicCall + " ASKS FOR " + Stats.name + "'S CREW MEDIC: A BAD DAY, PEOPLE HURT", 30f, new Vector3(town.pos.x, 0f, town.pos.y));
                Journal.Add("CREW", LastMedicCall + " CALLS FOR YOUR CREW MEDIC: BRING THEM INTO TOWN BY TOMORROW NIGHT (" + MedicFee + " SCRAP)");
                k++;
            }
            return k;
        }

        /// <summary>The player and a crew medic in a town that called: they help out. True when answered.</summary>
        public bool AnswerMedicCall(Settlement town, MadMax.Npc.Npc medic)
        {
            if (town == null || !medic || !MedicCalls.ContainsKey(town.index)) return false;
            MedicCalls.Remove(town.index);
            string tn = Market.TownName(town);
            Inventory.Add(ResourceType.Scrap, MedicFee);
            string used = MedicSpends(medic);                                                      // their dressings; the town pays part in bandages
            var f = Factions.OfSettlement(town);
            Factions.Shift(f != Faction.None ? f : Faction.Settlers, 3);
            NpcRegistry.Reputation = Mathf.Min(100, NpcRegistry.Reputation + 2);
            Companions.Practise(medic.Profile);
            LastMedicHelp = tn;
            TownNews.Post(medic.KnownName + " OF " + Stats.name + "'S CREW SPENT A HARD DAY AT " + tn + "'S SICKBEDS", new Vector3(town.pos.x, 0f, town.pos.y));
            Toast(medic.KnownName + " GETS TO WORK ON " + tn + "'S HURT WITH " + used + ": " + MedicFee + " SCRAP, " + TownPaysBandages + " BANDAGES AND THE TOWN'S THANKS");
            Journal.Add("CREW", medic.KnownName + " HELPED OUT AT " + tn);
            return true;
        }

        void UpdateMedicCalls()
        {
            RollMedicCalls();
            if (MedicCalls.Count == 0 || !Player || World == null) return;
            var me = Player.transform.position;
            var town = World.SettlementAt(me.x, me.z);
            if (town == null || !MedicCalls.ContainsKey(town.index)) return;
            var medic = CrewMedic(me, MedicAnswerReach);
            if (medic) AnswerMedicCall(town, medic);
        }

        // ------------------------------------------------------------------ rumoured caches

        /// <summary>A buried stash heard of in town talk: a search circle until spotted (binoculars) or walked onto.</summary>
        public class Cache { public string key; public Vector3 at; public float radius; public bool spotted, found, dug, lost; public float heard = DayNight.TotalDays; }
        public readonly List<Cache> Caches = new List<Cache>();
        public const float CacheRadius = 160f, CacheSpawnReach = 70f, CacheFindReach = 4f;
        public const string CacheTable = "cache";
        readonly Dictionary<string, Container> cacheBoxes = new Dictionary<string, Container>();

        /// <summary>This week's cache near <paramref name="town"/> (deterministic from the seed, town and week; null when
        /// no dry spot was found).</summary>
        public Cache CacheNear(Settlement town, int week)
        {
            if (town == null || !terrain) return null;
            var rnd = new System.Random(Market.Seed(seed, town.index * 31337 + week * 7451 + 5));
            for (int tries = 0; tries < 8; tries++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = town.radius + 220f + (float)rnd.NextDouble() * 480f;
                float x = town.pos.x + Mathf.Cos(a) * d, z = town.pos.y + Mathf.Sin(a) * d;
                if (terrain.WaterDepthNoLoad(x, z) > 0.05f || World.SettlementAt(x, z) != null) continue;
                return new Cache { key = "cache:" + town.index + ":" + week, at = new Vector3(x, terrain.HeightNoLoad(x, z), z), radius = CacheRadius };
            }
            return null;
        }

        /// <summary>Town talk about a buried stash (in <c>Dialogue.Rumours</c>): adds it to LOOK FOR as a search circle.
        /// Null when this town's cache this week is already known.</summary>
        public string CacheRumour(Vector3 at)
        {
            if (World == null) return null;
            var town = World.SettlementAt(at.x, at.z) ?? NearestTown(at);
            if (town == null) return null;
            var c = CacheNear(town, DayNight.Day / 7);
            if (c == null || Caches.Exists(k => k.key == c.key)) return null;
            Caches.Add(c);
            if (Caches.Count > 12) Caches.RemoveAll(k => k.found);
            return "SOMEBODY BURIED A STASH " + NpcLore.Compass(c.at.x - at.x, c.at.z - at.z) + " OF HERE, " + NpcLore.Distance(Flat(c.at - at)) + ". MARKED IT WITH A POST, THEY SAY.";
        }

        void UpdateCaches()
        {
            if (Caches.Count == 0 || !Player) return;
            var me = Player.transform.position;
            foreach (var c in Caches)
            {
                if (c.found) continue;
                float d = Flat(c.at - me);
                if (!c.spotted && BinocularsTool.Looking && !Current && d < c.radius + 40f && InSight(me, c.at, Camera.main))
                {
                    c.spotted = true;
                    TickLookFor("A BURIED STASH");
                    Toast("SPOTTED THROUGH THE BINOCULARS: A MARKER POST (ON THE MAP)");
                    Journal.Add("FOUND", "SPOTTED THE POST OF A BURIED STASH");
                }
                if (d < CacheSpawnReach) SpawnCache(c);
                if (d < CacheFindReach) FindCache(c);
            }
        }

        /// <summary>The marker post and the half-buried crate (spawned when the player comes near).</summary>
        public Container SpawnCache(Cache c)
        {
            if (cacheBoxes.TryGetValue(c.key, out var box) && box) return box;
            var at = c.at;
            if (terrain) at.y = terrain.Height(at.x, at.z);
            var def = FurnitureLibrary.Get("crate");
            var crate = new GameObject("CacheCrate", typeof(MeshFilter), typeof(MeshRenderer));
            crate.transform.position = at - Vector3.up * 0.15f;                                        // half in the ground
            crate.transform.rotation = Quaternion.Euler(0f, (c.key.GetHashCode() & 0xff) * 1.4f, 0f);
            if (def != null) { crate.GetComponent<MeshFilter>().sharedMesh = def.mesh; FurnitureLibrary.DressHD(crate, def.id); }
            crate.GetComponent<MeshRenderer>().sharedMaterial = propMaterial;
            var col = crate.AddComponent<BoxCollider>();
            if (def != null && def.mesh) { col.center = def.mesh.bounds.center; col.size = def.mesh.bounds.size; } else col.size = Vector3.one * 0.6f;
            var post = FurnitureLibrary.Get("sign");
            var go = new GameObject("CachePost", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = at + new Vector3(0.9f, 0f, 0f);
            if (post != null) { go.GetComponent<MeshFilter>().sharedMesh = post.mesh; FurnitureLibrary.DressHD(go, post.id); }
            go.GetComponent<MeshRenderer>().sharedMaterial = propMaterial;
            go.transform.SetParent(crate.transform, true);
            box = crate.AddComponent<Container>();
            box.title = "BURIED STASH";
            box.capacity = 999f;
            var rnd = new System.Random(Market.Seed(seed, c.key.GetHashCode()));
            foreach (var (id, n) in LootTables.Roll(CacheTable, rnd, 2, Stats.Attribute(MadMax.RPG.Attr.Perception)))
            {
                if (id.StartsWith("res:") && int.TryParse(id.Substring(4), out int rt)) box.inventory.Add((ResourceType)rt, n);
                else box.inventory.AddItem(id, n);
            }
            if (ItemCatalog.TotalWeight(box.inventory) <= 0f) box.inventory.AddItem("food_can", 2);
            CacheRivals(c, box, at);                                                               // left too long: somebody dug it up first
            cacheBoxes[c.key] = box;
            return box;
        }

        void FindCache(Cache c)
        {
            c.found = true;
            TickLookFor("A BURIED STASH");
            if (c.dug) { Toast(DugUpNote(c)); return; }
            Toast("FOUND THE BURIED STASH BY THE POST (OPEN IT: [E])");
            Journal.Add("FOUND", "DUG OUT A BURIED STASH");
        }

        // ------------------------------------------------------------------ convalescence at home

        /// <summary>Lying up by day: game hours per rest, break healing pace (radio on: × <see cref="LieUpRadio"/>),
        /// reading pace, chance a neighbour from town looks in (once a day).</summary>
        public const float LieUpHours = 4f, LieUpPace = 2.2f, LieUpRadio = 1.15f, LieUpReading = 1.5f, VisitChance = 0.65f, VisitReach = 2500f;
        static readonly string[] VisitFood = { "food_stew", "food_bread", "food_meat_pie", "food_apple_pie", "food_fish_soup", "food_cornbread" };
        int visitDay = -1;
        /// <summary>The last neighbour's visit (tests).</summary>
        public static string LastVisit;

        public static bool NightForSleep => DayNight.Darkness > 0.2f || DayNight.Hours > 20f || DayNight.Hours < 5f;

        /// <summary>A splinted break in a leg or foot that still needs rest.</summary>
        public bool LegLaidUp { get { foreach (var i in Stats.injuries) if (i.BrokenLeg && i.severity > 0.05f) return true; return false; } }

        /// <summary>Inside a claim the player owns.</summary>
        public bool AtHome(Vector3 p)
        {
            var c = ClaimFlag.Near(p);
            return c && c.Owner == Stats.name;
        }

        /// <summary>The bed offers LIE UP instead of REST: by day, a broken leg, at home.</summary>
        public bool CanLieUp(Bed bed) => bed && !NightForSleep && LegLaidUp && AtHome(bed.transform.position);

        /// <summary>Lie up in <paramref name="bed"/> for a few hours: the break mends faster (more with the radio on), the
        /// book in hand (or the first unread one in the pack) gets read, and once a day a neighbour may bring food.</summary>
        public string LieUp(Bed bed, float comfort)
        {
            float hours = Mathf.Clamp(20f - DayNight.Hours, 0.5f, LieUpHours);
            DayNight.SetHours(DayNight.Hours + hours);
            Stats.hunger = Mathf.Max(5f, Stats.hunger - hours * 2f);
            Stats.thirst = Mathf.Max(5f, Stats.thirst - hours * 3f);
            float seconds = hours * DayNight.DayMinutes * 60f / 24f;
            MadMax.Audio.RadioReceiver radio = null;
            foreach (var r in MadMax.Audio.RadioReceiver.All) if (r && r.on && Flat(r.transform.position - bed.transform.position) < 10f && !r.GetComponentInParent<VehicleDriver>()) { radio = r; break; }
            float pace = LieUpPace * (radio ? LieUpRadio : 1f) * Mathf.Lerp(0.8f, 1.2f, Mathf.Clamp01(comfort / 8f));
            foreach (var inj in Stats.injuries) inj.Rest(seconds, inj.BrokenLeg ? pace : 1f);
            Stats.injuries.RemoveAll(i => i.severity <= 0f);
            Stats.stamina = Stats.MaxStamina;
            var parts = new List<string> { "LAID UP " + Mathf.RoundToInt(hours) + " HOURS" };
            if (radio) parts.Add("WITH " + radio.StationLabel() + " ON");
            string read = ReadInBed(seconds);
            if (read != null) parts.Add(read);
            string visit = NeighbourVisit(bed.transform.position);
            if (visit != null) parts.Add(visit);
            string msg = string.Join(", ", parts);
            Toast(msg);
            Journal.Add("HEALTH", msg);
            return msg;
        }

        /// <summary>Reading in bed: the open book (or the first unread one in the pack) advances.</summary>
        string ReadInBed(float seconds)
        {
            if (LearningId == null)
                foreach (var kv in Inventory.Items)
                {
                    if (kv.Value <= 0 || !kv.Key.StartsWith("book_") || Stats.consumed.Contains(kv.Key)) continue;
                    var md = MediaLibrary.Get(kv.Key);
                    if (md == null) continue;
                    BeginLearning(kv.Key, md.name);
                    break;
                }
            if (LearningId == null || !LearningId.StartsWith("book_")) return null;
            var m = MediaLibrary.Get(LearningId);
            if (m == null) return null;
            learnStart = Player.transform.position;
            LearningProgress = Mathf.Min(1f, LearningProgress + seconds * Stats.ReadingSpeed * LieUpReading / Mathf.Max(1f, m.seconds));   // finishes on the next frame
            return "READ " + LearningName + (LearningProgress >= 1f ? " TO THE END" : "");
        }

        /// <summary>Once a day: a neighbour from the nearest friendly town looks in with food.</summary>
        string NeighbourVisit(Vector3 home, bool force = false)
        {
            if (visitDay == DayNight.Day && !force) return null;
            var town = NearestTown(home);
            if (town == null || Vector2.Distance(town.pos, new Vector2(home.x, home.z)) > VisitReach) return null;
            var f = Factions.OfSettlement(town);
            if (f != Faction.None && Factions.Hostile(f)) return null;
            var rnd = new System.Random(Market.Seed(seed, town.index * 977 + DayNight.Day * 61 + 3));
            if (!force && rnd.NextDouble() >= VisitChance) return null;
            visitDay = DayNight.Day;
            string food = VisitFood[rnd.Next(VisitFood.Length)];
            if (FoodLibrary.Get(food) == null) food = "food_bread";
            Inventory.AddItem(food);
            var who = ResidentOf(town, rnd);                                                       // a real resident of that town
            LastVisit = who != null ? NeighbourCame(who, food) + " FROM " + Market.TownName(town)
                                    : "A NEIGHBOUR FROM " + Market.TownName(town) + " LOOKED IN WITH " + ItemIds.Name(food);
            MadMax.Audio.Sfx.Play("door_close", home, 0.5f, 1.1f, 20f, 1f);
            return LastVisit;
        }

        /// <summary>Tests: a visit regardless of the day's roll.</summary>
        public string ForceNeighbourVisit(Vector3 home) => NeighbourVisit(home, true);

        // ------------------------------------------------------------------ tick + save

        void UpdateNightly1013()
        {
            UpdateMedicCalls();
            UpdateCaches();
            UpdateNightly1014();
        }

        void SaveNightly1013(SaveData d)
        {
            d.crewRoad = new List<string>();
            foreach (var kv in MedicWord) d.crewRoad.Add("w|" + kv.Key + "|" + kv.Value);
            foreach (var kv in MedicCalls) d.crewRoad.Add("m|" + kv.Key + "|" + kv.Value);
            foreach (var c in Caches)
                d.crewRoad.Add("c|" + c.key + "|" + c.at.x.ToString("0.#", RoadInv) + "|" + c.at.y.ToString("0.#", RoadInv) + "|" + c.at.z.ToString("0.#", RoadInv) + "|" + (c.spotted ? 1 : 0) + "|" + (c.found ? 1 : 0)
                               + "|" + c.heard.ToString("0.###", RoadInv) + "|" + (c.dug ? 1 : 0) + "|" + (c.lost ? 1 : 0));
            Save1014(d.crewRoad);
        }

        void LoadNightly1013(SaveData d)
        {
            MedicWord.Clear(); MedicCalls.Clear(); Caches.Clear(); cacheBoxes.Clear();
            Clear1014();
            if (d.crewRoad == null) return;
            foreach (var line in d.crewRoad)
            {
                var f = line.Split('|');
                if (f[0] == "w" && f.Length >= 3 && int.TryParse(f[1], out int t) && int.TryParse(f[2], out int n)) MedicWord[t] = n;
                else if (f[0] == "m" && f.Length >= 3 && int.TryParse(f[1], out int t2) && int.TryParse(f[2], out int day)) MedicCalls[t2] = day;
                else if (f[0] == "c" && f.Length >= 7
                    && float.TryParse(f[2], System.Globalization.NumberStyles.Float, RoadInv, out float x)
                    && float.TryParse(f[3], System.Globalization.NumberStyles.Float, RoadInv, out float y)
                    && float.TryParse(f[4], System.Globalization.NumberStyles.Float, RoadInv, out float z))
                {
                    var c = new Cache { key = f[1], at = new Vector3(x, y, z), radius = CacheRadius, spotted = f[5] == "1", found = f[6] == "1" };
                    if (f.Length >= 10 && float.TryParse(f[7], System.Globalization.NumberStyles.Float, RoadInv, out float heard)) { c.heard = heard; c.dug = f[8] == "1"; c.lost = f[9] == "1"; }
                    Caches.Add(c);
                }
                else Load1014(f);
            }
        }
    }
}
