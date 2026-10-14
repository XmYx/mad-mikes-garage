using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Npc;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Scheduled update 2026-10-14: neighbours who visit the laid-up player by name (and ask after the leg),
    /// rivals who dig up a cache left too long (tracks lead to the digger), a raider boss car that runs once the gang
    /// knows the player wants a part back, the crew medic's kit running dry (towns pay part in supplies), and rescued
    /// survivors who tip the player off on the radio before their week is out.</summary>
    public partial class WastelandGame
    {
        // ------------------------------------------------------------------ neighbours by name

        /// <summary>Neighbours who looked in on the laid-up player: profile id → day of the visit (saved). They ask after
        /// the leg the next time the player talks to them (<see cref="AskAfterLeg"/>).</summary>
        public readonly Dictionary<string, int> NeighbourVisits = new Dictionary<string, int>();
        readonly HashSet<string> legAsked = new HashSet<string>();
        public const int NeighbourWarmth = 5;
        /// <summary>The last neighbour who visited (profile id, tests).</summary>
        public static string LastNeighbour;

        /// <summary>A living resident of <paramref name="town"/> picked by <paramref name="rnd"/> (the same people
        /// <c>NpcDirector</c> walks about the town), null when all are dead.</summary>
        public NpcProfile ResidentOf(Settlement town, System.Random rnd)
        {
            if (town == null || World == null) return null;
            int count = town.kind == Biome.Village ? 4 : 5, start = rnd.Next(count);
            for (int k = 0; k < count; k++)
            {
                string id = "r" + town.index + "," + (start + k) % count;
                if (NpcRegistry.IsDead(id)) continue;
                return NpcProfile.Make(id, NpcRole.Resident, NpcDirector.StableKey(id) ^ World.seed);
            }
            return null;
        }

        /// <summary>The visit makes them a known neighbour: met, a little warmer, and they will ask after the leg.</summary>
        string NeighbourCame(NpcProfile p, string food)
        {
            var s = NpcRegistry.Get(p);
            s.Set(NpcSave.Met);
            s.disposition = Mathf.Min(100, s.disposition + NeighbourWarmth);
            NeighbourVisits[p.id] = DayNight.Day;
            legAsked.Remove(p.id);
            LastNeighbour = p.id;
            return p.Name + " LOOKED IN WITH " + ItemIds.Name(food);
        }

        /// <summary>Talking to a neighbour who visited: once, they ask after the leg (a line for the dialogue).</summary>
        public string AskAfterLeg(NpcProfile p)
        {
            if (p == null || !NeighbourVisits.TryGetValue(p.id, out int day) || legAsked.Contains(p.id)) return null;
            legAsked.Add(p.id);
            var s = NpcRegistry.Get(p);
            s.disposition = Mathf.Min(100, s.disposition + 2);
            string since = DayNight.Day - day <= 1 ? "" : " SINCE I CAME BY";
            return LegLaidUp ? "HOW'S THE LEG, " + Stats.name + "? STILL LAID UP" + since + ", I SEE. DON'T PUSH IT."
                             : Stats.name + "! UP ON BOTH FEET" + since + ". TOLD YOU THE FOOD WOULD DO IT.";
        }

        // ------------------------------------------------------------------ cache rivals

        /// <summary>A cache heard of this many days ago may have been dug up first (<see cref="RivalChance"/>, rolled per
        /// cache); the digger walks off toward the nearest town ~<see cref="DiggerLead"/> m ahead with the loot.</summary>
        public const float CacheRivalDays = 3f, RivalChance = 0.65f, DiggerLead = 60f, DiggerCatch = 6f, DiggerStep = 20f, DiggerLost = 300f;
        /// <summary>Cache key → the person carrying its loot off (this session).</summary>
        public readonly Dictionary<string, MadMax.Npc.Npc> Diggers = new Dictionary<string, MadMax.Npc.Npc>();
        readonly Dictionary<MadMax.Npc.Npc, Settlement> diggerTown = new Dictionary<MadMax.Npc.Npc, Settlement>();
        /// <summary>The last dug-up cache's message (tests).</summary>
        public static string LastDugUp;
        float diggerNext;

        /// <summary>Somebody beat the player to it: left long enough and the roll says so.</summary>
        public bool RivalBeatYou(Cache c)
        {
            if (c == null || c.found) return false;
            if (c.dug) return true;
            if (DayNight.TotalDays - c.heard < CacheRivalDays) return false;
            var rnd = new System.Random(Market.Seed(seed, c.key.GetHashCode() * 3 + 11));
            return rnd.NextDouble() < RivalChance;
        }

        static void Empty(Inventory inv) => inv.Restore(new int[inv.ResourceArray.Length], new List<KeyValuePair<string, int>>());

        /// <summary>Called as the crate is set out: a dug-up cache is emptied into a digger walking away, with a trail
        /// of fresh footprints from the hole.</summary>
        void CacheRivals(Cache c, Container box, Vector3 at)
        {
            if (!RivalBeatYou(c)) return;
            bool fresh = !c.dug;
            c.dug = true;
            box.title = "BURIED STASH (DUG UP)";
            box.transform.position = at + Vector3.up * 0.05f;                                      // hauled out and left tipped by the hole
            box.transform.rotation *= Quaternion.Euler(12f, 0f, 8f);
            if (fresh && terrain)                                                                  // the hole and its spoil (saved with the chunk)
            {
                var spoil = at + box.transform.right * 1.5f;
                terrain.ApplyTerraform((byte)DeformableTerrain.TerraOp.Dig, at - box.transform.right * 0.9f, 0.8f, 0.4f, 0);
                terrain.ApplyTerraform((byte)DeformableTerrain.TerraOp.Dump, spoil, 0.7f, 0.25f, 0);
            }
            if (c.lost || (Diggers.TryGetValue(c.key, out var had) && had)) { Empty(box.inventory); return; }
            var town = NearestTown(at);
            var dir = town != null ? new Vector3(town.pos.x - at.x, 0f, town.pos.y - at.z) : Vector3.forward;
            if (dir.sqrMagnitude < 1f) dir = Vector3.forward;
            dir.Normalize();
            var p = at + dir * DiggerLead;
            p.y = terrain ? terrain.Height(p.x, p.z) + 0.1f : at.y;
            var prof = NpcProfile.Make("dig:" + c.key, NpcRole.Wanderer, Market.Seed(seed, c.key.GetHashCode() * 5 + 7));
            var n = MadMax.Npc.Npc.Spawn(prof, p, Quaternion.LookRotation(dir).eulerAngles.y, null, propMaterial);
            n.EnsurePack();
            n.pack.title = prof.Name + "'S SACK";
            var res = box.inventory.ResourceArray;
            for (int i = 0; i < res.Length; i++) if (res[i] > 0) n.pack.inventory.Add((ResourceType)i, res[i]);
            foreach (var kv in new List<KeyValuePair<string, int>>(box.inventory.Items)) if (kv.Value > 0) n.pack.inventory.AddItem(kv.Key, kv.Value);
            Empty(box.inventory);
            n.home = p + dir * DiggerStep; n.homeRadius = 2f;
            Diggers[c.key] = n;
            diggerTown[n] = town;
            // the trail: fresh prints from the hole to where they walk now
            var side = Vector3.Cross(Vector3.up, dir);
            bool left = false;
            for (float d = 1.2f; d < DiggerLead; d += 0.75f)
            {
                var q = at + dir * d + side * (Mathf.Sin(d * 0.21f) * 0.6f);
                if (terrain) q.y = terrain.Height(q.x, q.z);
                left = !left;
                Fx.Footprint(q, dir, left);
            }
        }

        /// <summary>The player reached a dug-up cache: what they see.</summary>
        string DugUpNote(Cache c)
        {
            if (Diggers.TryGetValue(c.key, out var n) && n && n.Alive)
                LastDugUp = "SOMEBODY BEAT YOU TO IT: THE STASH IS DUG UP AND FRESH TRACKS LEAD " + NpcLore.Compass(n.transform.position.x - c.at.x, n.transform.position.z - c.at.z);
            else LastDugUp = "SOMEBODY BEAT YOU TO IT: THE STASH IS DUG UP, THE TRACKS ALREADY BLOWN OVER";
            MadMax.Audio.Sfx.Play("dig", c.at, 0.3f, 0.8f, 15f, 1f);
            Journal.Add("FOUND", LastDugUp);
            return LastDugUp;
        }

        /// <summary>Diggers walk on toward town; caught on foot within <see cref="DiggerCatch"/> m they drop half the haul
        /// and run (killing them gives it all, through their body's loot). Too far behind and they are gone.</summary>
        void UpdateDiggers()
        {
            if (Diggers.Count == 0 || !Player || Time.time < diggerNext) return;
            diggerNext = Time.time + 0.4f;
            var me = Player.transform.position;
            foreach (var key in new List<string>(Diggers.Keys))
            {
                var n = Diggers[key];
                var c = Caches.Find(k => k.key == key);
                if (!n || !n.Alive) { Diggers.Remove(key); if (n) diggerTown.Remove(n); continue; }
                float d = Flat(n.transform.position - me);
                if (d > DiggerLost)
                {
                    if (c != null) c.lost = true;
                    Diggers.Remove(key); diggerTown.Remove(n);
                    Destroy(n.gameObject);
                    continue;
                }
                if (n.pack && Lootable.HasAny(n.pack.inventory) && !Current && d < DiggerCatch && !n.Hostile) { CatchDigger(n); continue; }
                if (diggerTown.TryGetValue(n, out var town) && town != null && Flat(n.home - n.transform.position) < 3f)
                {
                    var tp = new Vector3(town.pos.x, 0f, town.pos.y);
                    var at = n.transform.position;
                    if (Flat(tp - at) < town.radius) { if (c != null) c.lost = true; Diggers.Remove(key); diggerTown.Remove(n); n.LetGo(); continue; }   // in town: sold on
                    var step = at + (tp - new Vector3(at.x, 0f, at.z)).normalized * DiggerStep;
                    step.y = at.y;
                    n.home = step; n.homeRadius = 2f;
                }
            }
        }

        /// <summary>Caught up with: half the sack on the ground (a little more with Charisma), then they run.</summary>
        public int CatchDigger(MadMax.Npc.Npc n)
        {
            if (!n || !n.pack) return 0;
            var inv = n.pack.inventory;
            var drop = new List<KeyValuePair<string, int>>();
            var res = inv.ResourceArray;
            for (int i = 0; i < res.Length; i++) if (res[i] > 0) drop.Add(new KeyValuePair<string, int>("res:" + i, Mathf.Max(1, res[i] / 2)));
            int items = 0;
            foreach (var kv in inv.Items) items += kv.Value;
            int share = Mathf.CeilToInt(items * Mathf.Clamp01(0.5f + 0.05f * (Stats.Attribute(MadMax.RPG.Attr.Charisma) - 5)));
            foreach (var kv in new List<KeyValuePair<string, int>>(inv.Items))
            {
                if (share <= 0) break;
                int k = Mathf.Min(share, kv.Value);
                if (k > 0) { drop.Add(new KeyValuePair<string, int>(kv.Key, k)); share -= k; }
            }
            var at = n.transform.position + n.transform.forward * 0.8f;
            int dropped = 0;
            foreach (var kv in drop)
            {
                if (kv.Key.StartsWith("res:")) { if (int.TryParse(kv.Key.Substring(4), out int rt)) inv.TrySpend((ResourceType)rt, kv.Value); }
                else inv.TakeItem(kv.Key, kv.Value);
                var p = at + new Vector3((dropped % 3 - 1) * 0.45f, 0f, (dropped / 3) * 0.45f);
                if (terrain) p.y = terrain.Height(p.x, p.z) + 0.15f;
                WorldItem.Create(kv.Key, kv.Value, 0.5f, propMaterial, p, Quaternion.Euler(0f, dropped * 47f, 0f));
                dropped++;
            }
            foreach (var kv in new List<KeyValuePair<string, MadMax.Npc.Npc>>(Diggers)) if (kv.Value == n) Diggers.Remove(kv.Key);
            diggerTown.Remove(n);
            n.Scare(30f);
            Toast(n.Profile.Name + ": \"FINDERS KEEPERS... ALL RIGHT, ALL RIGHT, HALF!\" (" + dropped + " DROPPED, [E] TAKE)");
            Journal.Add("FOUND", "CAUGHT UP WITH WHOEVER DUG UP THE STASH: THEY DROPPED HALF AND RAN");
            TickLookFor("WHOEVER DUG UP THE STASH");
            return dropped;
        }

        // ------------------------------------------------------------------ boss car chase

        /// <summary>Gangs that know the player wants a part back (the salvager talked): gang → last day they run (saved).</summary>
        public readonly Dictionary<string, int> GangWary = new Dictionary<string, int>();
        public const int WaryDays = 3;
        /// <summary>The last gang whose boss car ran for it (tests).</summary>
        public static string LastRun;

        /// <summary>Word gets back to the gang: their boss car runs instead of stopping for a toll while they carry the part.</summary>
        public void MarkWary(string gang)
        {
            if (string.IsNullOrEmpty(gang)) return;
            GangWary[gang] = DayNight.Day + WaryDays;
            Journal.Add("RAID", "WORD WILL GET BACK TO THE " + gang + ": THEIR BOSS CAR WILL RUN WITH YOUR PART. RUN IT DOWN, STOP IT, OPEN THE TRUNK");
        }

        /// <summary>The gang's boss car flees the player (wary and still carrying one of the player's parts).</summary>
        public static bool GangRuns(string gang)
        {
            var g = Instance;
            if (!g || string.IsNullOrEmpty(gang) || !g.GangWary.TryGetValue(gang, out int until) || DayNight.Day > until) return false;
            return MadMax.Npc.Trade.CarriedBy(gang).Count > 0;
        }

        /// <summary>From <see cref="Convoy"/>: the chase starts.</summary>
        public void NoteRun(string gang)
        {
            LastRun = gang;
            Toast("THE " + gang + "'S BOSS CAR GUNS IT: THEY KNOW YOU WANT YOUR PART BACK");
        }

        // ------------------------------------------------------------------ the medic's kit runs dry

        public static readonly string[] MedicStock = { Companions.FirstAidKit, "med_bandage", "med_splint" };
        public const int TownPaysBandages = 2;
        readonly Dictionary<string, int> kitWarnDay = new Dictionary<string, int>();
        /// <summary>The last "out of dressings" call and what the last town call used (tests).</summary>
        public static string LastKitDry, LastMedicUsed;
        float kitNext;

        public static int MedicKit(MadMax.Npc.Npc n)
        {
            if (!n || !n.pack) return 0;
            int k = 0;
            foreach (var id in MedicStock) k += n.pack.inventory.GetItem(id);
            return k;
        }

        /// <summary>A town call uses the medic's own dressings (a kit, else up to two bandages); the town pays part in
        /// supplies (<see cref="TownPaysBandages"/> bandages into the medic's pack). Returns what was used.</summary>
        string MedicSpends(MadMax.Npc.Npc medic)
        {
            medic.EnsurePack();
            var inv = medic.pack.inventory;
            int bandages = Mathf.Min(2, inv.GetItem("med_bandage"));
            string used = inv.TakeItem(Companions.FirstAidKit) ? "A FIRST-AID KIT"
                        : bandages > 0 && inv.TakeItem("med_bandage", bandages) ? (bandages == 1 ? "A BANDAGE" : "TWO BANDAGES")
                        : "THE TOWN'S OWN RAGS";
            inv.AddItem("med_bandage", TownPaysBandages);
            return LastMedicUsed = used;
        }

        /// <summary>Every few seconds: a crew medic with nothing left to dress wounds says so once a day — on the radio
        /// when posted away, aloud when near.</summary>
        public void UpdateMedicKit(bool force = false)
        {
            if (!force && Time.time < kitNext) return;
            kitNext = Time.time + 6f;
            var radio = Companions.OnRadio();
            foreach (var n in Companions.Live)
            {
                if (!n || !n.Alive || !Companions.Patches(n.Profile) || MedicKit(n) > 0) continue;
                if (!force && kitWarnDay.TryGetValue(n.Profile.id, out int day) && day == DayNight.Day) continue;
                kitWarnDay[n.Profile.id] = DayNight.Day;
                string line = n.KnownName + ": \"I'M OUT OF DRESSINGS. BANDAGES OR A FIRST-AID KIT, IF YOU PASS A TRADER.\"";
                LastKitDry = line;
                bool far = Player && Flat(n.transform.position - Player.transform.position) > Companions.RadioRange;
                if (far && radio.Contains(n)) SendHomeCall("HOME FREQUENCY: " + line, n.transform.position);
                else { Toast(line); Journal.Add("CREW", line); }
            }
        }

        // ------------------------------------------------------------------ hitcher network

        /// <summary>A rescued survivor: the town they were brought to, their name, whether they have tipped the player off.</summary>
        public class Rescue { public int town; public string name; public bool tipped; }
        /// <summary>Rescued survivors by profile id (saved). One tip each, on a day within their week.</summary>
        public readonly Dictionary<string, Rescue> Rescues = new Dictionary<string, Rescue>();
        /// <summary>The last tip (tests).</summary>
        public static string LastTip;
        float tipNext;

        void NoteRescued(MadMax.Npc.Npc n, Settlement town)
        {
            NoteRescued(n);
            if (town != null) Rescues[n.Profile.id] = new Rescue { town = town.index, name = n.Profile.Name };
        }

        /// <summary>The day this survivor gets on the radio (1–4 days after the rescue).</summary>
        public static int TipDay(NpcSave s) => s.rescuedDay + 1 + ((s.id.GetHashCode() & 0x7fffffff) % 4);

        void UpdateHitcherTips()
        {
            if (Rescues.Count == 0 || Time.time < tipNext || World == null) return;
            tipNext = Time.time + 5f;
            if (DayNight.Hours < 8f || DayNight.Hours > 21f) return;
            foreach (var id in new List<string>(Rescues.Keys))
            {
                var s = NpcRegistry.Peek(id);
                if (s == null || s.dead || !Rescued(s)) { Rescues.Remove(id); continue; }
                if (Rescues[id].tipped || DayNight.Day < TipDay(s)) continue;
                HitcherTip(id);
            }
        }

        /// <summary>The survivor's tip on the home frequency: this week's stash near their town (60 %), else where the
        /// nearest raider gang rides. Null when they had nothing to tell (marked tipped anyway).</summary>
        public string HitcherTip(string id)
        {
            if (!Rescues.TryGetValue(id, out var r) || World == null || r.town < 0 || r.town >= World.settlements.Count) return null;
            r.tipped = true;
            var town = World.settlements[r.town];
            var tp = new Vector3(town.pos.x, 0f, town.pos.y);
            string tip = null;
            var rnd = new System.Random(Market.Seed(seed, id.GetHashCode() + 17));
            if (rnd.NextDouble() < 0.6)
            {
                var c = CacheNear(town, DayNight.Day / 7);
                if (c != null && !Caches.Exists(k => k.key == c.key))
                {
                    Caches.Add(c);
                    tip = "THERE'S A STASH BURIED " + NpcLore.Compass(c.at.x - tp.x, c.at.z - tp.z) + " OF " + Market.TownName(town) + ". GET THERE BEFORE THE DIGGERS DO";
                }
            }
            if (tip == null && GangNear(tp, out string gang, out var at))
                tip = "THE " + gang + " ARE RIDING " + NpcLore.Compass(at.x - tp.x, at.z - tp.z) + " OF " + Market.TownName(town) + ". KEEP CLEAR OF THAT ROAD - OR DON'T";
            if (tip == null) return null;
            LastTip = (r.name ?? "AN OLD PASSENGER") + " ON THE RADIO: \"STILL OWE YOU ONE. " + tip + ".\"";
            SendHomeCall("HOME FREQUENCY: " + LastTip, tp);
            return LastTip;
        }

        bool GangNear(Vector3 from, out string gang, out Vector3 at)
        {
            gang = null; at = default;
            var dir = NpcDirector.Instance;
            if (!dir) return false;
            float bd = 3000f;
            foreach (var c in dir.Convoys)
            {
                if (c == null || !c.raiders || c.Friendly) continue;
                float d = Flat(c.Position - from);
                if (d < bd) { bd = d; gang = c.Gang; at = c.Position; }
            }
            return gang != null;
        }

        // ------------------------------------------------------------------ tick, LOOK FOR, save

        void UpdateNightly1014()
        {
            UpdateDiggers();
            UpdateMedicKit();
            UpdateHitcherTips();
        }

        void LookFor1014(List<Pin> into, Color32 col)
        {
            foreach (var kv in Diggers)
                if (kv.Value && kv.Value.Alive && kv.Value.pack && Lootable.HasAny(kv.Value.pack.inventory))
                    into.Add(new Pin { label = "WHOEVER DUG UP THE STASH", pos = kv.Value.transform.position, color = col });
        }

        void Save1014(List<string> lines)
        {
            foreach (var kv in NeighbourVisits) lines.Add("n|" + kv.Key + "|" + kv.Value + "|" + (legAsked.Contains(kv.Key) ? 1 : 0));
            foreach (var kv in GangWary) lines.Add("g|" + kv.Key + "|" + kv.Value);
            foreach (var kv in Rescues) lines.Add("h|" + kv.Key + "|" + kv.Value.town + "|" + (kv.Value.tipped ? 1 : 0) + "|" + (kv.Value.name ?? "").Replace("|", " "));
        }

        void Load1014(string[] f)
        {
            if (f[0] == "n" && f.Length >= 4 && int.TryParse(f[2], out int day)) { NeighbourVisits[f[1]] = day; if (f[3] == "1") legAsked.Add(f[1]); }
            else if (f[0] == "g" && f.Length >= 3 && int.TryParse(f[2], out int until)) GangWary[f[1]] = until;
            else if (f[0] == "h" && f.Length >= 4 && int.TryParse(f[2], out int t)) Rescues[f[1]] = new Rescue { town = t, tipped = f[3] == "1", name = f.Length >= 5 && f[4].Length > 0 ? f[4] : null };
        }

        void Clear1014()
        {
            NeighbourVisits.Clear(); legAsked.Clear(); GangWary.Clear(); Rescues.Clear(); kitWarnDay.Clear();
            foreach (var n in Diggers.Values) if (n) Destroy(n.gameObject);
            Diggers.Clear(); diggerTown.Clear();
        }
    }
}
