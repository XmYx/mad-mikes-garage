using System.Collections.Generic;
using MadMax.Game;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Town bosses' quest chains (roadmap 20). Every settlement has a boss (<see cref="NpcRole.Leader"/>) who
    /// hands out four jobs in turn, drawn per town from the world seed: a supply run (materials the town is short of),
    /// a cull of the gang working the nearest road (a bounty contract), a courier run to the boss of the nearest other
    /// town and back, and holding the town against a raid at dusk. Each pays scrap, Fuel Guild chits and standing; the
    /// last makes the player a friend of the town (10 % better prices in its shops). Saved per settlement.</summary>
    public static class TownQuests
    {
        public const int Stages = 4;
        public const string Parcel = "misc_parcel";
        static readonly Dictionary<int, int> stage = new Dictionary<int, int>();
        static readonly HashSet<int> accepted = new HashSet<int>();
        static readonly HashSet<int> won = new HashSet<int>();                       // the raid on this town was beaten
        static readonly List<Npc> attackers = new List<Npc>();
        static int defending = -1, eventDay = -1;
        static bool eventRaid;
        static float nextCheck;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { stage.Clear(); accepted.Clear(); won.Clear(); attackers.Clear(); defending = -1; nextCheck = 0f; eventDay = -1; eventRaid = false; }

        static WorldGen World => DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;

        public static int Stage(int town) => stage.TryGetValue(town, out var s) ? s : 0;
        public static bool Friend(Settlement st) => st != null && Stage(st.index) >= Stages;
        public static bool Accepted(int town) => accepted.Contains(town);

        static Settlement Town(int i) { var w = World; return w != null && i >= 0 && i < w.settlements.Count ? w.settlements[i] : null; }

        static System.Random Rng(int town, int salt) => new System.Random(Market.Seed(town, 900 + salt, World != null ? World.seed : 0));

        static (ResourceType res, int n, string what) Supply(int town)
        {
            var opts = new[] { (ResourceType.Wood, 30, "TIMBER FOR THE WALLS"), (ResourceType.Iron, 12, "IRON FOR THE PUMP"), (ResourceType.Water, 40, "CLEAN WATER"),
                               (ResourceType.Scrap, 60, "SCRAP FOR THE FORGE"), (ResourceType.Stone, 40, "STONE FOR THE WELL"), (ResourceType.Cloth, 15, "CLOTH FOR BANDAGES") };
            return opts[Rng(town, 0).Next(opts.Length)];
        }

        static string Gang(int town)
        {
            var gangs = NpcDirector.Instance ? NpcDirector.Instance.RaiderGangs() : new List<(string, string)>();
            return gangs.Count > 0 ? gangs[Rng(town, 1).Next(gangs.Count)].Item2 : NpcLore.Gangs[Rng(town, 1).Next(NpcLore.Gangs.Length)];
        }

        /// <summary>The courier's other end: the nearest other settlement.</summary>
        public static Settlement Sister(int town)
        {
            var w = World; var st = Town(town);
            if (w == null || st == null) return null;
            Settlement best = null; float bd = float.MaxValue;
            foreach (var o in w.settlements) { if (o == st) continue; float d = Vector2.Distance(o.pos, st.pos); if (d < bd) { bd = d; best = o; } }
            return best;
        }

        static string CullId(int town) => "Q" + town + ":cull";

        /// <summary>What the boss asks now (and how far along it is).</summary>
        public static string Describe(WastelandGame g, int town)
        {
            int s = Stage(town);
            var st = Town(town);
            string name = st != null ? Market.TownName(st) : "THIS TOWN";
            switch (s)
            {
                case 0: { var (res, n, what) = Supply(town); return "WE'RE SHORT OF " + what + ". BRING " + n + " " + ResourceInfo.Name(res) + " (YOU HAVE " + g.Inventory.Get(res) + ")."; }
                case 1:
                {
                    var c = Contracts.Active.Find(x => x.id == CullId(town));
                    return "THE " + Gang(town) + " BLEED OUR ROADS. PUT DOWN FOUR OF THEM" + (c != null ? " (" + c.done + "/" + c.need + ")." : ".");
                }
                case 2:
                {
                    var sis = Sister(town);
                    string other = sis != null ? Market.TownName(sis) : "THE NEXT TOWN";
                    return g.Inventory.GetItem(Parcel) > 0 ? "YOU HAVE THE PARCEL FROM " + other + "? HAND IT OVER." : "THE BOSS OF " + other + " HOLDS MEDICINE FOR US. FETCH THE PARCEL.";
                }
                case 3: return won.Contains(town) ? "YOU HELD " + name + ". WE WON'T FORGET IT." : "THE RAIDERS WILL HIT " + name + " AT DUSK. BE HERE AND HOLD THE LINE.";
                default: return "YOU'RE FAMILY IN " + name + " NOW. OUR SHOPS KNOW IT.";
            }
        }

        public static string Title(int s) => s switch { 0 => "SUPPLY RUN", 1 => "RAIDER CULL", 2 => "COURIER", 3 => "HOLD THE TOWN", _ => "FRIEND OF THE TOWN" };

        public static void Accept(WastelandGame g, int town)
        {
            int s = Stage(town);
            if (s >= Stages || accepted.Contains(town)) return;
            accepted.Add(town);
            if (s == 1 && !Contracts.Active.Exists(x => x.id == CullId(town)))
                Contracts.Active.Add(new Contract { id = CullId(town), kind = 1, target = Gang(town), title = "BOSS'S CULL: 4 " + Gang(town) + " RAIDERS", need = 4, reward = 0, town = town });
            g.Toast("JOB TAKEN: " + Title(s));
            MadMax.Game.Journal.Add("TOWN", Market.TownName(Town(town)) + " BOSS: " + Title(s));
        }

        static Faction rewardFaction = Faction.Settlers;

        /// <summary>Hand the job in. Returns the boss's reply (null = not done yet).</summary>
        public static string TurnIn(WastelandGame g, int town, Vector3 at)
        {
            int s = Stage(town);
            rewardFaction = Factions.OfSettlement(Town(town));
            if (s >= Stages || !accepted.Contains(town)) return null;
            switch (s)
            {
                case 0:
                {
                    var (res, n, _) = Supply(town);
                    if (g.Inventory.Get(res) < n) return null;
                    g.Inventory.TrySpend(res, n);
                    Reward(g, 60, 1, 4, null);
                    break;
                }
                case 1:
                {
                    var c = Contracts.Active.Find(x => x.id == CullId(town));
                    if (c == null || !c.completed) return null;
                    Contracts.Active.Remove(c);
                    Reward(g, 110, 2, 6, "med_antibiotics");
                    break;
                }
                case 2:
                    if (!g.Inventory.TakeItem(Parcel)) return null;
                    Reward(g, 140, 3, 6, "use_repair_kit");
                    break;
                case 3:
                {
                    if (!won.Contains(town)) return null;
                    Reward(g, 260, 6, 12, null);
                    var part = g.SpawnPart("engine_v8_blower", at + Vector3.up * 0.8f, Quaternion.identity);
                    if (part && !part.GetComponent<Rigidbody>()) part.gameObject.AddComponent<Rigidbody>().mass = part.mass;
                    break;
                }
            }
            accepted.Remove(town);
            stage[town] = s + 1;
            return s + 1 >= Stages ? "THE TOWN OWES YOU. FROM NOW ON YOU PAY FRIENDS' PRICES HERE." : "GOOD WORK. COME BACK TOMORROW, THERE'S MORE.";
        }

        static void Reward(WastelandGame g, int scrap, int chits, int rep, string item)
        {
            g.Inventory.Add(ResourceType.Scrap, scrap);
            g.Inventory.AddItem(Contracts.Chit, chits);
            if (item != null) g.Inventory.AddItem(item, 2);
            Factions.Shift(rewardFaction, rep);
            g.Stats.Practice(MadMax.RPG.Skill.Speech, 4f);
            MadMax.Audio.Sfx.Play2D("cash", 0.8f);
            g.Toast("PAID " + scrap + " SCRAP + " + chits + " CHITS" + (item != null ? " + " + ItemIds.Name(item) : ""));
        }

        /// <summary>The courier's pickup: the sister town's boss hands over the parcel.</summary>
        public static bool ParcelFor(WastelandGame g, int bossTown, out string from)
        {
            from = null;
            foreach (var t in accepted)
            {
                if (Stage(t) != 2) continue;
                var sis = Sister(t);
                if (sis == null || sis.index != bossTown || g.Inventory.GetItem(Parcel) > 0) continue;
                from = Market.TownName(Town(t));
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ holding the town (stage 3)

        public static void Tick(WastelandGame g)
        {
            if (Time.time < nextCheck || !g.Player) return;
            nextCheck = Time.time + 2f;
            var me = g.Current ? g.Current.transform.position : g.Player.transform.position;
            if (defending >= 0)
            {
                int alive = 0;
                foreach (var n in attackers) if (n && n.Alive && !n.Surrendered) alive++;
                if (alive == 0 && eventRaid)
                {
                    // a raid out of the blue, beaten off (roadmap 21 events)
                    g.Inventory.Add(ResourceType.Scrap, 60);
                    Factions.Shift(Factions.OfSettlement(Town(defending)), 8);
                    g.Toast(Market.TownName(Town(defending)) + " CHEERS YOU - THE RAIDERS ARE BEATEN (+60 SCRAP)");
                    MadMax.Audio.Sfx.Play("crowd_cheer", me, 0.6f);
                    defending = -1; attackers.Clear(); eventRaid = false;
                }
                else if (alive == 0)
                {
                    won.Add(defending);
                    g.Toast("THE RAID IS BROKEN - " + Market.TownName(Town(defending)) + " IS SAFE. SEE THE BOSS");
                    MadMax.Audio.Sfx.Play("crowd_cheer", me, 0.6f);
                    defending = -1; attackers.Clear();
                }
                else if (Town(defending) != null && Vector2.Distance(Town(defending).pos, new Vector2(me.x, me.z)) > 400f)
                {
                    foreach (var n in attackers) if (n) Object.Destroy(n.gameObject);                   // you left: they sack it off-screen
                    attackers.Clear(); defending = -1;
                    g.Toast("YOU LEFT THE TOWN TO THE RAIDERS");
                }
                return;
            }
            if (DayNight.Hours < 17.5f || DayNight.Hours > 21.5f) return;
            foreach (var t in accepted)
            {
                if (Stage(t) != 3 || won.Contains(t)) continue;
                var st = Town(t);
                if (st == null || Vector2.Distance(st.pos, new Vector2(me.x, me.z)) > st.radius + 60f) continue;
                Raid(g, st);
                return;
            }
            // now and then a gang hits the town the player is in at dusk (roadmap 21 events)
            if (eventDay == DayNight.Day) return;
            var w = World;
            var here = w != null ? w.SettlementAt(me.x, me.z) : null;
            if (here == null) return;
            eventDay = DayNight.Day;
            if (Rng(here.index, 50 + DayNight.Day).NextDouble() > 0.3) return;
            eventRaid = true;
            Raid(g, here);
        }

        static void Raid(WastelandGame g, Settlement st)
        {
            defending = st.index;
            var terrain = DeformableTerrain.Instance;
            var r = Rng(st.index, 3 + DayNight.Day);
            float a = (float)r.NextDouble() * Mathf.PI * 2f;
            var edge = st.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (st.radius + 25f);
            string gang = Gang(st.index);
            int gi = Mathf.Max(0, System.Array.IndexOf(NpcLore.Gangs, gang));
            var centre = new Vector3(st.pos.x, terrain.Height(st.pos.x, st.pos.y), st.pos.y);
            int count = 5 + r.Next(3);
            for (int i = 0; i < count; i++)
            {
                // seeds picked so the profile's gang is the one that holds the road
                int seed = r.Next(1, int.MaxValue / 8) * NpcLore.Gangs.Length + gi;
                var p = NpcProfile.Make("traid:" + st.index + ":" + DayNight.Day + ":" + i, i == 0 ? NpcRole.RaiderBoss : NpcRole.Raider, seed);
                var pos = new Vector3(edge.x + (float)r.NextDouble() * 8f - 4f, 0f, edge.y + (float)r.NextDouble() * 8f - 4f);
                pos.y = terrain.Height(pos.x, pos.z) + 0.1f;
                var n = Npc.Spawn(p, pos, Quaternion.LookRotation(centre - pos).eulerAngles.y, null, g.propMaterial);
                n.aggro = true; n.raiding = true; n.raidAt = centre;
                attackers.Add(n);
            }
            MadMax.Audio.Sfx.Play("horn", new Vector3(edge.x, centre.y, edge.y), 1f, 0.8f, 250f);
            g.Toast("THE " + gang + " ARE HITTING " + Market.TownName(st) + "! (" + count + ")");
        }

        // ------------------------------------------------------------------ save

        public static void Save(SaveData d)
        {
            d.townQuests = new List<string>();
            foreach (var kv in stage) d.townQuests.Add(kv.Key + ":" + kv.Value + ":" + (accepted.Contains(kv.Key) ? 1 : 0) + ":" + (won.Contains(kv.Key) ? 1 : 0));
            foreach (var t in accepted) if (!stage.ContainsKey(t)) d.townQuests.Add(t + ":0:1:0");
        }

        public static void Load(SaveData d)
        {
            stage.Clear(); accepted.Clear(); won.Clear(); attackers.Clear(); defending = -1;
            if (d.townQuests == null) return;
            foreach (var e in d.townQuests)
            {
                var a = e.Split(':');
                if (a.Length < 4 || !int.TryParse(a[0], out int t) || !int.TryParse(a[1], out int s)) continue;
                if (s > 0) stage[t] = s;
                if (a[2] == "1") accepted.Add(t);
                if (a[3] == "1") won.Add(t);
            }
        }
    }
}
