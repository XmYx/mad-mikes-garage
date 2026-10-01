using System;
using System.Collections.Generic;
using MadMax.Game;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A job from a bounty board: a raider boss, a number of a gang's raiders, pests, or a haul.</summary>
    [Serializable]
    public class Contract
    {
        public string id, title, target;
        public int kind;                  // 0 boss bounty, 1 raider cull, 2 pests, 3 delivery, 4 faction supply (target "res:N"), 5 Guild escort
        public int faction = -1;          // whose standing it moves (roadmap 21); -1 = by kind
        public int town, dest = -1;       // offered at / delivery destination (settlement index)
        public int need, done, reward, chits, rep, days, deadline = -1;
        public bool completed, failed;
        public bool Delivery => kind == 3;
        public bool Supply => kind == 4;
        public bool Escort => kind == 5;
    }

    /// <summary>Bounty boards and the Fuel Guild's hauling (roadmap 15). Every town's board offers today's jobs
    /// (deterministic per town and day): the boss of a raider gang that runs the roads, a cull of a gang's raiders, pests
    /// (animals), and crates to haul to another town before a deadline — physical cargo (<c>cargo_crate</c> parts) to
    /// strap on a vehicle or trailer, which makes raiders keener. Rewards: scrap, Fuel Guild chits and reputation;
    /// finished bounties are claimed at any board, crates delivered at the destination's board.</summary>
    public static class Contracts
    {
        public const string Chit = "coin_chit";
        public static readonly List<Contract> Active = new List<Contract>();
        static readonly HashSet<string> taken = new HashSet<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Active.Clear(); taken.Clear(); }

        static readonly (string id, string name)[] Pests = { ("dog", "WILD DOGS"), ("boar", "BOARS"), ("wolf", "WOLVES"), ("rat", "GIANT RATS") };

        static WorldGen World => DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;

        /// <summary>Crates are aboard (a delivery is running): raiders spot you from farther away.</summary>
        public static bool Hauling { get { foreach (var c in Active) if (c.Delivery && !c.completed && !c.failed) return true; return false; } }

        /// <summary>Today's jobs at a town's board, minus the ones already taken.</summary>
        public static List<Contract> Offers(Settlement st)
        {
            var list = new List<Contract>();
            var w = World;
            if (w == null || st == null) return list;
            int day = DayNight.Day;
            var r = new System.Random(Market.Seed(st.index, day, w.seed));
            var gangs = NpcDirector.Instance ? NpcDirector.Instance.RaiderGangs() : new List<(string, string)>();
            if (gangs.Count > 0)
            {
                var (cid, gang) = gangs[r.Next(gangs.Count)];
                list.Add(new Contract { id = "B" + st.index + ":" + day, kind = 0, target = cid, title = "WANTED: THE BOSS OF THE " + gang, need = 1, reward = 120 + r.Next(80), chits = 2, rep = 8, town = st.index });
                var (_, gang2) = gangs[r.Next(gangs.Count)];
                int n = 3 + r.Next(4);
                list.Add(new Contract { id = "R" + st.index + ":" + day, kind = 1, target = gang2, title = "CULL " + n + " " + gang2 + " RAIDERS", need = n, reward = 20 * n + r.Next(40), rep = 4, town = st.index });
            }
            var pest = Pests[r.Next(Pests.Length)];
            int pn = 3 + r.Next(3);
            list.Add(new Contract { id = "P" + st.index + ":" + day, kind = 2, target = pest.id, title = "PESTS: KILL " + pn + " " + pest.name, need = pn, reward = 12 * pn, rep = 2, town = st.index });
            // hauls to other towns
            var others = new List<Settlement>();
            foreach (var o in w.settlements) if (o != st) others.Add(o);
            for (int i = 0; i < 2 && others.Count > 0; i++)
            {
                var dest = others[r.Next(others.Count)];
                float dist = Vector2.Distance(st.pos, dest.pos);
                int crates = 1 + r.Next(3), days = 1 + Mathf.FloorToInt(dist / 1500f);
                list.Add(new Contract
                {
                    id = "D" + st.index + ":" + day + ":" + i, kind = 3, town = st.index, dest = dest.index, need = crates, days = days,
                    title = "HAUL " + crates + (crates > 1 ? " CRATES" : " CRATE") + " TO " + Market.TownName(dest) + " (" + Mathf.RoundToInt(dist / 100f) / 10f + " KM, " + days + (days > 1 ? " DAYS)" : " DAY)"),
                    reward = 10 * crates + Mathf.RoundToInt(dist / 40f), chits = crates + Mathf.FloorToInt(dist / 800f), rep = 3,
                });
            }
            // the town's own faction asks for supplies (roadmap 21)
            var owner = Factions.OfSettlement(st);
            if (owner != Faction.None && !Factions.Hostile(owner))
            {
                var (res, n, what) = owner switch
                {
                    Faction.Church => (MadMax.Items.ResourceType.Fuel, 40, "OFFERING FOR THE LAST ENGINE: 40 L FUEL"),
                    Faction.Nomads => (MadMax.Items.ResourceType.Water, 30, "WATER FOR THE CARAVANS: 30 L"),
                    Faction.Remnants => (MadMax.Items.ResourceType.Copper, 8, "WIRE FOR THE BUNKER: 8 COPPER"),
                    _ => (MadMax.Items.ResourceType.Wood, 25, "FIREWOOD FOR THE WINTER: 25 WOOD"),
                };
                list.Add(new Contract { id = "S" + st.index + ":" + day, kind = 4, faction = (int)owner, target = "res:" + (int)res, need = n, title = what, reward = n * 2 + 20, rep = 6, town = st.index });
            }
            // the Guild wants a rig escorted to the town down the road
            if (!GuildEscort.Busy && !Factions.Hostile(Faction.FuelGuild) && GuildEscort.RouteFrom(st, out var route, out var to) && r.NextDouble() < 0.7)
            {
                float km = 0f; for (int i = 1; i < route.Count; i++) km += Vector3.Distance(route[i - 1], route[i]);
                list.Add(new Contract { id = "E" + st.index + ":" + day, kind = 5, faction = (int)Faction.FuelGuild, title = "ESCORT A GUILD RIG TO " + Market.TownName(to) + " (" + Mathf.RoundToInt(km / 100f) / 10f + " KM)",
                                        reward = 80 + Mathf.RoundToInt(km / 20f), chits = 3, rep = 6, town = st.index });
            }
            list.RemoveAll(c => taken.Contains(c.id));
            return list;
        }

        public static void Accept(WastelandGame g, Contract c, Vector3 board)
        {
            if (!taken.Add(c.id) || Active.Contains(c)) return;                                  // a job is taken once (a second press, a stale board row)
            if (c.Delivery)
            {
                c.deadline = DayNight.Day + c.days;
                for (int i = 0; i < c.need; i++)
                {
                    var p = board + Quaternion.Euler(0f, i * 50f, 0f) * Vector3.forward * 2.2f + Vector3.up * 0.8f;
                    var part = g.SpawnPart("cargo_crate", p, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                    if (part && !part.GetComponent<Rigidbody>()) part.gameObject.AddComponent<Rigidbody>().mass = part.mass;
                }
                g.Toast("CRATES BY THE BOARD: STRAP THEM ON (E) AND DRIVE");
            }
            else g.Toast("JOB TAKEN: " + c.title);
            Active.Add(c);
            MadMax.Game.Journal.Add("JOB", c.title);
            if (c.Escort) GuildEscort.Start(g, c);
        }

        /// <summary>A kill by the player (NPC deaths; animals report with <see cref="ReportPest"/>).</summary>
        public static void ReportKill(Npc n)
        {
            if (!n || !n.Profile.Raider) return;
            foreach (var c in Active)
            {
                if (c.completed || c.failed) continue;
                if (c.kind == 0 && n.Profile.role == NpcRole.RaiderBoss && n.convoy != null && n.convoy.id == c.target) c.done = 1;
                else if (c.kind == 1 && n.Profile.gang == c.target) c.done++;
                else continue;
                Check(c);
            }
        }

        public static void ReportPest(string kind)
        {
            foreach (var c in Active)
                if (!c.completed && !c.failed && c.kind == 2 && c.target == kind) { c.done++; Check(c); }
        }

        static void Check(Contract c)
        {
            if (c.done < c.need) { WastelandGame.Instance?.Toast(c.title + ": " + c.done + "/" + c.need); return; }
            c.completed = true;
            WastelandGame.Instance?.Toast("JOB DONE: " + c.title + " - CLAIM AT A BOARD");
        }

        /// <summary>Deadlines pass.</summary>
        public static void Tick()
        {
            foreach (var c in Active)
                if (c.Delivery && !c.completed && !c.failed && c.deadline >= 0 && DayNight.Day > c.deadline)
                {
                    c.failed = true;
                    Factions.Shift(Faction.FuelGuild, -c.rep);
                    WastelandGame.Instance?.Toast("HAUL FAILED: " + c.title);
                }
        }

        /// <summary>Pay a finished job.</summary>
        public static void Pay(WastelandGame g, Contract c)
        {
            if (!c.completed || !Active.Contains(c)) return;                                    // paid once: a claimed job leaves the list
            if (c.id.StartsWith("Q")) { g.Toast("TELL THE TOWN BOSS IT'S DONE"); return; }        // a town boss's job (TownQuests)
            int pay = c.reward;
            if ((c.Delivery || c.Escort) && g.Current && MadMax.Vehicles.VehiclePaint.DecalOf(g.Current) == Factions.Decal[(int)Faction.FuelGuild]) pay += pay / 10;   // Guild colours
            using var feed = MadMax.Items.Inventory.Source("REWARD");
            g.Inventory.Add(MadMax.Items.ResourceType.Scrap, pay);
            if (c.chits > 0) g.Inventory.AddItem(Chit, c.chits);
            Factions.Shift(c.faction >= 0 ? (Faction)c.faction : c.Delivery ? Faction.FuelGuild : Faction.Settlers, c.rep);
            Active.Remove(c);
            MadMax.Audio.Sfx.Play2D("cash", 0.8f);
            g.Stats.Practice(MadMax.RPG.Skill.Speech, 3f);
            g.Toast("PAID " + pay + " SCRAP" + (c.chits > 0 ? " + " + c.chits + " CHITS" : ""));
            WastelandGame.StarterNote("paid");
        }

        /// <summary>Hand in a faction's supply job at a board (roadmap 21).</summary>
        public static bool HandIn(WastelandGame g, Contract c)
        {
            if (!c.Supply || c.completed || c.failed || c.target == null || !c.target.StartsWith("res:")) return false;
            var res = (MadMax.Items.ResourceType)int.Parse(c.target.Substring(4));
            if (g.Inventory.Get(res) < c.need) { g.Toast("NEED " + c.need + " " + MadMax.Items.ResourceInfo.Name(res) + " (YOU HAVE " + g.Inventory.Get(res) + ")"); return false; }
            g.Inventory.TrySpend(res, c.need);
            c.done = c.need; c.completed = true;
            Pay(g, c);
            return true;
        }

        /// <summary>Crates within reach of the destination board count for their haul; delivered ones go away.</summary>
        public static int Deliver(WastelandGame g, Contract c, Vector3 board)
        {
            if (!c.Delivery || c.completed || c.failed) return 0;
            int n = 0;
            foreach (var part in UnityEngine.Object.FindObjectsByType<MadMax.Vehicles.VehiclePart>())
            {
                if (c.done + n >= c.need) break;
                if (!part || part.partId != "cargo_crate" || Vector3.Distance(part.transform.position, board) > 15f) continue;
                var socket = part.GetComponentInParent<MadMax.Vehicles.MountSocket>();
                if (socket && socket.Current == part) socket.Detach(false);
                UnityEngine.Object.Destroy(part.gameObject);
                n++;
            }
            c.done += n;
            if (c.done >= c.need) { c.completed = true; Pay(g, c); }
            else if (n > 0) g.Toast("DELIVERED " + c.done + "/" + c.need);
            else g.Toast("BRING THE CRATES CLOSER TO THE BOARD");
            return n;
        }

        // ------------------------------------------------------------------ save
        public static void Save(SaveData d)
        {
            d.contracts = new List<Contract>(Active);
            d.contractsTaken = new List<string>(taken);
        }

        public static void Load(SaveData d)
        {
            Active.Clear(); taken.Clear();
            if (d.contracts != null) Active.AddRange(d.contracts);
            if (d.contractsTaken != null) foreach (var t in d.contractsTaken) taken.Add(t);
        }
    }
}
