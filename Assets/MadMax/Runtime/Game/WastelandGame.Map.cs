using System.Collections.Generic;
using MadMax.Building;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>World map support: places found (towns entered, sites visited or spotted from the air), the player's
    /// waypoint with its route along the roads, and pins for the jobs in hand (contracts, town bosses).</summary>
    public partial class WastelandGame
    {
        /// <summary>Found places: "town:N" and site keys (saved).</summary>
        public readonly HashSet<string> Discovered = new HashSet<string>();
        public bool HasWaypoint { get; private set; }
        public Vector3 Waypoint { get; private set; }
        /// <summary>The waypoint route along the roads (focus → … → waypoint).</summary>
        public readonly List<Vector3> Route = new List<Vector3>();

        public struct Pin
        {
            public string label; public Vector3 pos; public Color32 color;
            /// <summary>A search circle (metres) instead of a point: somewhere around <see cref="pos"/>.</summary>
            public float radius;
        }

        /// <summary>Wreck sites marked from the road news on a board (saved); a pin clears when the player gets there.</summary>
        public readonly List<Vector3> NewsPins = new List<Vector3>();

        /// <summary>Mark a wreck from the road news: a map pin and the waypoint.</summary>
        public void MarkNewsWreck(Vector3 p)
        {
            foreach (var q in NewsPins) if (Flat(q - p) < 10f) { SetWaypoint(q, "WRECK SITE"); return; }
            NewsPins.Add(p);
            SetWaypoint(p, "WRECK SITE");
            Journal.Add("NEWS", "MARKED A WRECK SITE AT " + Mathf.RoundToInt(p.x) + "," + Mathf.RoundToInt(p.z));
        }

        float mapCheck;
        bool waypointQuiet;                 // set by races and guides: no toasts
        readonly List<Site> nearSites = new List<Site>();

        Vector3 FocusPos => Current ? Current.transform.position : Player ? Player.transform.position : Vector3.zero;

        public void SetWaypoint(Vector3 p, string what = null, bool silent = false)
        {
            p.y = terrain ? terrain.HeightNoLoad(p.x, p.z) : p.y;
            Waypoint = p; HasWaypoint = true; waypointQuiet = silent;
            RecomputeRoute();
            if (!silent) Toast("WAYPOINT" + (what != null ? ": " + what : "") + "  " + Mathf.RoundToInt(Flat(p - FocusPos)) + " M");
        }

        public void ClearWaypoint() { HasWaypoint = false; Route.Clear(); }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        void RecomputeRoute()
        {
            if (!HasWaypoint) { Route.Clear(); return; }
            RoadRoute.Find(World, FocusPos, Waypoint, Route);
        }

        public static string SiteName(Site s) => s.kind == SiteKind.Bunker ? "BUNKER" : s.kind == SiteKind.Airfield ? "AIRFIELD" : "ROCK TUNNEL";

        void UpdateMap()
        {
            if (Time.time < mapCheck || World == null) return;
            mapCheck = Time.time + 1f;
            UpdateHomeRadio();
            UpdateRoadWrecks();
            UpdateRoadside();
            var at = FocusPos;
            // towns entered, sites walked into or seen from the air
            foreach (var st in World.settlements)
                if (Vector2.Distance(st.pos, new Vector2(at.x, at.z)) < st.radius + 40f && Discovered.Add("town:" + st.index))
                    Journal.Add("FOUND", "REACHED " + MadMax.Npc.Market.TownName(st));
            World.SitesNear(at, 400f, nearSites);
            foreach (var s in nearSites)
            {
                bool seen = Vector2.Distance(s.pos, new Vector2(at.x, at.z)) < Mathf.Max(120f, s.reach + 30f);
                if (seen && Discovered.Add(s.Key)) { Journal.Add("FOUND", SiteName(s) + " AT " + Mathf.RoundToInt(s.pos.x) + "," + Mathf.RoundToInt(s.pos.y)); Toast("FOUND: " + SiteName(s) + " (ON THE MAP)"); }
            }
            foreach (var k in Scouted) Discovered.Add(k);
            foreach (var m in BiomeProps.Landmarks(World))
                if (Vector2.Distance(m.pos, new Vector2(at.x, at.z)) < 90f && Discovered.Add("lm:" + m.index))
                    Journal.Add("FOUND", m.Name + " AT " + Mathf.RoundToInt(m.pos.x) + "," + Mathf.RoundToInt(m.pos.y));
            for (int i = NewsPins.Count - 1; i >= 0; i--)
                if (Flat(NewsPins[i] - at) < 25f) { NewsPins.RemoveAt(i); Toast("THE WRECK FROM THE NEWS - SALVAGE IT"); }
            // the waypoint: arrive, or re-route when far off the line
            if (HasWaypoint)
            {
                if (Flat(Waypoint - at) < 18f) { ClearWaypoint(); if (!waypointQuiet) Toast("WAYPOINT REACHED"); return; }
                float off = float.MaxValue;
                foreach (var p in Route) off = Mathf.Min(off, Flat(p - at));
                if (Route.Count == 0 || off > 70f) RecomputeRoute();
                else if (Route.Count > 1) Route[0] = at;                                          // the line starts where you are
            }
        }

        /// <summary>Where the jobs in hand lead: delivery and escort destinations, the town to hand supplies in to, the gang
        /// a bounty or cull is on, the town-boss chain's next stop.</summary>
        public void JobPins(List<Pin> into)
        {
            into.Clear();
            if (World == null) return;
            var job = new Color32(255, 200, 60, 255);
            foreach (var c in MadMax.Npc.Contracts.Active)
            {
                if (c.completed || c.failed) continue;
                Settlement town = null;
                if ((c.Delivery || c.Escort) && c.dest >= 0 && c.dest < World.settlements.Count) town = World.settlements[c.dest];
                else if (c.Supply && c.town >= 0 && c.town < World.settlements.Count) town = World.settlements[c.town];
                if (town != null) { into.Add(new Pin { label = c.title, pos = new Vector3(town.pos.x, 0f, town.pos.y), color = job }); continue; }
                if ((c.kind == 0 || c.kind == 1) && MadMax.Npc.NpcDirector.Instance)
                    foreach (var cv in MadMax.Npc.NpcDirector.Instance.Convoys)
                        if (cv.raiders && cv.Gang == c.target && cv.phase != MadMax.Npc.Convoy.Phase.Gone) { into.Add(new Pin { label = c.title, pos = cv.Position, color = new Color32(235, 70, 50, 255) }); break; }
            }
            foreach (var st in World.settlements)
            {
                if (!MadMax.Npc.TownQuests.Accepted(st.index)) continue;
                int stage = MadMax.Npc.TownQuests.Stage(st.index);
                var target = stage == 2 ? MadMax.Npc.TownQuests.Sister(st.index) : st;
                if (target != null) into.Add(new Pin { label = MadMax.Npc.TownQuests.Title(stage), pos = new Vector3(target.pos.x, 0f, target.pos.y), color = new Color32(150, 220, 120, 255) });
            }
            foreach (var p in NewsPins) into.Add(new Pin { label = "WRECK (NEWS)", pos = p, color = new Color32(200, 170, 130, 255) });
            BurnedPins(into);
            LastEngine.Pins(into);
            StoryPins(into);
        }

        /// <summary>The player's stations away from them (> 40 m) that have work: WORKING with the job's progress, NO POWER
        /// (a powered station whose grid is down: the queue has stalled) or READY (goods waiting on the tray).</summary>
        public void WorkshopPins(List<Pin> into)
        {
            into.Clear();
            var me = FocusPos;
            foreach (var st in CraftingStation.All)
            {
                if (!st || (!st.Busy && st.TrayCount == 0) || Flat(st.transform.position - me) < 40f) continue;
                var p = st.GetComponent<Placeable>();
                if (!p || p.owner != Stats.name) continue;
                string state; Color32 col;
                if (st.Busy && !st.Powered) { state = "NO POWER"; col = PixelHud.Bad; }
                else if (st.Busy) { state = "WORKING " + Mathf.RoundToInt(st.Current.progress * 100f) + "%" + (st.queue.Count > 1 ? " +" + (st.queue.Count - 1) : ""); col = PixelHud.Good; }
                else { state = "READY (" + st.TrayCount + ")"; col = new Color32(240, 240, 220, 255); }
                into.Add(new Pin { label = st.title + ": " + state, pos = st.transform.position, color = col });
                if (into.Count >= 8) break;
            }
        }

        void SaveMap(SaveData d)
        {
            d.discovered = new List<string>(Discovered);
            d.hasWaypoint = HasWaypoint; d.waypoint = Waypoint;
            d.newsPins = new List<Vector3>(NewsPins);
            d.journal = Journal.Save();
            d.townNews = MadMax.Npc.TownNews.Save();
            d.starter = StarterStep;
            SaveHomestead(d);
            d.lastEngine = LastEngine.Save();
            SaveStory(d);
            BlocksSave(d);
            d.records = Racing.Save();
            SaveWreckPlan(d);
            SaveHome(d);
        }

        void LoadMap(SaveData d)
        {
            Discovered.Clear();
            if (d.discovered != null) foreach (var k in d.discovered) Discovered.Add(k);
            NewsPins.Clear();
            if (d.newsPins != null) NewsPins.AddRange(d.newsPins);
            Journal.Load(d.journal);
            MadMax.Npc.TownNews.Load(d.townNews);
            StarterStep = d.starter;
            LoadHomestead(d);
            LastEngine.Load(d.lastEngine);
            LoadStory(d);
            BlocksLoad(d);
            Racing.Load(d.records);
            LoadWreckPlan(d);
            LoadHome(d);
            if (d.hasWaypoint) { Waypoint = d.waypoint; HasWaypoint = true; RecomputeRoute(); }
        }
    }
}
