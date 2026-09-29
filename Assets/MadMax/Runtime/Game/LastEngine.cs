using System.Collections.Generic;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>THE LAST ENGINE, the late game's pull: a supercharged V12 of legend in four relics across the wastes —
    /// the block deep in a far bunker, the heads in an airfield hangar, the crankshaft with the boss of the gang that
    /// holds the longest road, the twin blowers in the Church's keeping (given to a trusted friend of the Church).
    /// People talk about it (rumours pin the places on the map), a garage puts it together, and a car running it that
    /// reaches the edge of the world makes THE LAST RUN. Saved as flags (relics found, "heard:*", "built", "run").</summary>
    public static class LastEngine
    {
        public const string Part = "engine_v12_last";
        public static readonly string[] Relics = { "relic_block", "relic_heads", "relic_crank", "relic_blower" };
        static readonly HashSet<string> flags = new HashSet<string>();
        static WorldGen resolved;
        static Site bunker, airfield;
        static string gang;
        static float check;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { flags.Clear(); resolved = null; bunker = airfield = null; gang = null; check = 0f; }

        public static bool Has(string f) => flags.Contains(f);
        public static List<string> Save() => new List<string>(flags);
        public static void Load(List<string> l) { flags.Clear(); if (l != null) foreach (var f in l) flags.Add(f); }

        static WorldGen World => DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;

        /// <summary>Where the relics lie in this world: the second-nearest bunker to the start (a trip, not a stroll), the
        /// nearest airfield, the gang with the longest road.</summary>
        static void Resolve()
        {
            var w = World;
            if (w == null || resolved == w) return;
            resolved = w;
            var sites = new List<Site>();
            w.SitesNear(Vector3.zero, w.halfSize * 1.5f, sites);
            sites.Sort((a, b) => a.pos.sqrMagnitude.CompareTo(b.pos.sqrMagnitude));
            Site first = null;
            foreach (var s in sites)
            {
                if (s.kind == SiteKind.Bunker) { if (first == null) first = s; else if (bunker == null) bunker = s; }
                if (s.kind == SiteKind.Airfield && airfield == null) airfield = s;
            }
            bunker ??= first;
            gang = null;
            if (NpcDirector.Instance)
            {
                float longest = 0f;
                foreach (var c in NpcDirector.Instance.Convoys)
                {
                    if (!c.raiders || c.route == null) continue;
                    float len = 0f;
                    for (int i = 1; i < c.route.Count; i++) len += Vector3.Distance(c.route[i - 1], c.route[i]);
                    if (len > longest) { longest = len; gang = c.Gang; }
                }
            }
        }

        static void Found(WastelandGame g, string relic, string where)
        {
            flags.Add(relic);
            Journal.Add("RELIC", "FOUND " + ItemCatalogName(relic) + " " + where + " (" + Count + "/4)");
            g.Toast("A RELIC OF THE LAST ENGINE! (" + Count + "/4)" + (Count == 4 ? " TAKE THEM TO A GARAGE" : ""));
            MadMax.Audio.Sfx.Play2D("cash", 0.8f, 0.7f);
        }

        static string ItemCatalogName(string id) => MadMax.Items.ItemCatalog.Name(id);

        public static int Count { get { int n = 0; foreach (var r in Relics) if (flags.Contains(r)) n++; return n; } }

        /// <summary>Searching a loot spot: the chosen bunker and airfield hold their relic (in the first spot searched).</summary>
        public static void AddFinds(WastelandGame g, string key, List<(string id, int n)> found)
        {
            Resolve();
            if (key == null || !key.StartsWith("S")) return;
            if (bunker != null && !Has("relic_block") && key.StartsWith("S" + bunker.Key + ",")) { found.Add(("relic_block", 1)); Found(g, "relic_block", "IN THE BUNKER"); }
            if (airfield != null && !Has("relic_heads") && key.StartsWith("S" + airfield.Key + ",")) { found.Add(("relic_heads", 1)); Found(g, "relic_heads", "IN THE HANGAR"); }
        }

        /// <summary>A raider boss falls: the boss of the longest road carries the crankshaft.</summary>
        public static void BossDrop(NpcProfile p, Lootable loot)
        {
            Resolve();
            if (p.role != NpcRole.RaiderBoss || Has("relic_crank") || gang == null || p.gang != gang) return;
            loot.extra.Add("relic_crank");
            flags.Add("relic_crank");
            var g = WastelandGame.Instance;
            if (g) { Journal.Add("RELIC", "THE " + gang + " BOSS CARRIED THE V12 CRANKSHAFT (" + Count + "/4)"); g.Toast("THE BOSS CARRIED A RELIC - SEARCH THE BODY"); }
        }

        /// <summary>A rumour about a relic not yet found (pins it on the map), or null.</summary>
        public static string Rumour(Vector3 at, int roll)
        {
            Resolve();
            var open = new List<int>();
            for (int i = 0; i < Relics.Length; i++) if (!Has(Relics[i])) open.Add(i);
            if (open.Count == 0) return Has("built") ? null : "THEY SAY SOMEONE PUT THE LAST ENGINE BACK TOGETHER. GODSPEED.";
            int k = open[Mathf.Abs(roll) % open.Count];
            flags.Add("heard:" + Relics[k]);
            switch (k)
            {
                case 0: return bunker == null ? null : "EVER HEARD OF THE LAST ENGINE? A V12, BLOWN. THE BLOCK'S IN A BUNKER " + NpcLore.Compass(bunker.pos.x - at.x, bunker.pos.y - at.z) + ", " + NpcLore.Distance(Vector2.Distance(bunker.pos, new Vector2(at.x, at.z))) + ".";
                case 1: return airfield == null ? null : "THE LAST ENGINE'S HEADS WENT TO AN AIRFIELD " + NpcLore.Compass(airfield.pos.x - at.x, airfield.pos.y - at.z) + ". LOCKED IN A HANGAR, THEY SAY.";
                case 2: return gang == null ? null : "THE " + gang + " BOSS WEARS THE CRANK OF THE LAST ENGINE ROUND HIS NECK. ALMOST.";
                default: return "THE CHURCH KEEPS THE LAST ENGINE'S BLOWERS. ONLY A TRUE FRIEND OF THE CHURCH WILL SEE THEM.";
            }
        }

        /// <summary>Map pins for relics heard of and not yet found.</summary>
        public static void Pins(List<WastelandGame.Pin> into)
        {
            Resolve();
            var col = new Color32(255, 150, 40, 255);
            if (bunker != null && Has("heard:relic_block") && !Has("relic_block")) into.Add(new WastelandGame.Pin { label = "V12 BLOCK", pos = new Vector3(bunker.pos.x, 0f, bunker.pos.y), color = col });
            if (airfield != null && Has("heard:relic_heads") && !Has("relic_heads")) into.Add(new WastelandGame.Pin { label = "V12 HEADS", pos = new Vector3(airfield.pos.x, 0f, airfield.pos.y), color = col });
            if (gang != null && Has("heard:relic_crank") && !Has("relic_crank") && NpcDirector.Instance)
                foreach (var c in NpcDirector.Instance.Convoys)
                    if (c.raiders && c.Gang == gang && c.phase != Convoy.Phase.Gone) { into.Add(new WastelandGame.Pin { label = "V12 CRANK: " + gang, pos = c.Position, color = col }); break; }
        }

        /// <summary>The Church's gift, the engine's first start, the last run.</summary>
        public static void Tick(WastelandGame g)
        {
            if (Time.time < check || !g.Player) return;
            check = Time.time + 3f;
            if (!Has("relic_blower") && Factions.Rank(Faction.Church) >= 4)
            {
                g.Inventory.AddItem("relic_blower", 1);
                Found(g, "relic_blower", "- A GIFT OF THE CHURCH");
            }
            var car = g.Current;
            if (!car || !Mounted(car)) return;
            if (!Has("built"))
            {
                flags.Add("built");
                Journal.Add("RELIC", "THE LAST ENGINE RUNS AGAIN. NOW TAKE IT TO THE EDGE OF THE WORLD.");
                g.Toast("THE LAST ENGINE ROARS! DRIVE IT TO THE EDGE OF THE WORLD");
            }
            var w = World;
            if (Has("run") || w == null) return;
            var p = car.transform.position;
            if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) < w.halfSize - 150f) return;
            flags.Add("run");
            Journal.Add("RELIC", "THE LAST RUN: DAY " + DayNight.Day + ", THE LAST ENGINE REACHED THE EDGE OF THE WORLD.");
            g.Toast("THE LAST RUN! THE WASTES WILL TELL OF THIS ONE");
            MadMax.Audio.Sfx.Play2D("crowd_cheer", 0.9f);
            Factions.Shift(Faction.Settlers, 10);
        }

        static bool Mounted(VehicleDriver car)
        {
            if (!car.TryGetComponent<VehicleChassis>(out var ch)) return false;
            foreach (var s in ch.Sockets) if (s.Current && s.Current.partId == Part) return true;
            return false;
        }
    }
}
