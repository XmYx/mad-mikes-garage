using System.Collections.Generic;
using MadMax.Game;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Fuel Guild escorts (roadmap 21 events): a Guild rig in Guild colours drives a road from the board's
    /// town to the town at its other end while the player rides shotgun. It waits when left behind, raiders ambush it
    /// halfway, and the job pays when it rolls into town (a wrecked rig fails it). One escort at a time; not saved —
    /// a reload scraps it.</summary>
    public static class GuildEscort
    {
        static VehicleDriver rig;
        static AiDriver ai;
        static List<Vector3> route;
        static Vector3 end;
        static Contract job;
        static bool ambushed, waiting;
        static float tick;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { rig = null; ai = null; route = null; job = null; ambushed = waiting = false; }

        public static bool Busy => rig;

        static WorldGen World => DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;

        static Settlement Near(Vector3 p, float slack)
        {
            var w = World;
            if (w == null) return null;
            foreach (var st in w.settlements) if (Vector2.Distance(st.pos, new Vector2(p.x, p.z)) < st.radius + slack) return st;
            return null;
        }

        /// <summary>A road from this town to another one.</summary>
        public static bool RouteFrom(Settlement st, out List<Vector3> path, out Settlement to)
        {
            path = null; to = null;
            var w = World;
            if (w == null || st == null) return false;
            foreach (var road in w.roads.roads)
            {
                if (road.points.Count < 4) continue;
                var a = Near(road.points[0], 40f); var b = Near(road.points[road.points.Count - 1], 40f);
                if (a == st && b != null && b != st) { path = new List<Vector3>(road.points); to = b; return true; }
                if (b == st && a != null && a != st) { path = new List<Vector3>(road.points); path.Reverse(); to = a; return true; }
            }
            return false;
        }

        public static void Start(WastelandGame g, Contract c)
        {
            if (Busy) return;
            var st = World != null && c.town >= 0 && c.town < World.settlements.Count ? World.settlements[c.town] : null;
            if (!RouteFrom(st, out var path, out var to)) { c.failed = true; return; }
            route = path; end = path[path.Count - 1];
            var t = DeformableTerrain.Instance;
            var p = path[1]; p.y = t.Height(p.x, p.z) + 1.4f;
            var dir = path[2] - path[1]; dir.y = 0f;
            rig = g.SpawnAiVehicle("Hauler", p, Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir : Vector3.forward));
            if (!rig) { c.failed = true; return; }
            if (rig.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = sys.fuelCapacity; sys.oil = sys.oilCapacity; sys.coolant = sys.coolantCapacity; }
            var paint = VehiclePaint.Of(rig); paint.colour = 4; paint.decal = 5; paint.Apply();          // Guild ochre with the drop
            ai = rig.gameObject.AddComponent<AiDriver>();
            ai.cruise = 11f;
            ai.SetPath(route, 1);
            job = c; ambushed = false; waiting = false;
            g.Toast("THE GUILD RIG ROLLS FOR " + Market.TownName(to) + " - STAY WITH IT");
        }

        public static void Tick(WastelandGame g)
        {
            if (!rig && job == null) return;
            if ((tick -= Time.deltaTime) > 0f) return;
            tick = 0.5f;
            if (job != null && (!rig || !ai || ai.Disabled || job.failed))
            {
                Fail(g, rig ? "THE GUILD RIG IS WRECKED - ESCORT FAILED" : "THE GUILD RIG IS LOST - ESCORT FAILED");
                return;
            }
            if (!rig) return;
            var me = g.Current ? g.Current.transform.position : g.Player.transform.position;
            var at = rig.transform.position;
            float dist = Flat(at - me).magnitude;
            // it waits for its escort
            if (!waiting && dist > 140f) { waiting = true; ai.goal = AiDriver.Goal.Park; g.Toast("THE GUILD RIG WAITS FOR YOU"); }
            else if (waiting && dist < 60f) { waiting = false; ai.SetPath(route, 1); }
            // halfway: the ambush
            if (!ambushed && Progress(at) > 0.45f) Ambush(g, at);
            if (Flat(at - end).magnitude < 30f) Arrive(g);
        }

        static float Progress(Vector3 at)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < route.Count; i++) { float d = (route[i] - at).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            return best / (float)Mathf.Max(1, route.Count - 1);
        }

        static void Ambush(WastelandGame g, Vector3 at)
        {
            ambushed = true;
            var t = DeformableTerrain.Instance;
            var gangs = NpcDirector.Instance ? NpcDirector.Instance.RaiderGangs() : new List<(string, string)>();
            string gang = gangs.Count > 0 ? gangs[Random.Range(0, gangs.Count)].Item2 : NpcLore.Gangs[0];
            int gi = Mathf.Max(0, System.Array.IndexOf(NpcLore.Gangs, gang));
            var ahead = rig.transform.forward * 45f;
            int n = 4 + Mathf.Min(3, DayNight.Day / 5);
            for (int i = 0; i < n; i++)
            {
                var side = rig.transform.right * ((i % 2 == 0 ? 1f : -1f) * (7f + i));
                var p = at + ahead + side;
                p.y = t.Height(p.x, p.z) + 0.1f;
                var prof = NpcProfile.Make("ambush:" + DayNight.Day + ":" + i + ":" + Mathf.RoundToInt(at.x), i == 0 ? NpcRole.RaiderBoss : NpcRole.Raider, Random.Range(1, int.MaxValue / 8) * NpcLore.Gangs.Length + gi);
                var npc = Npc.Spawn(prof, p, Quaternion.LookRotation(at - p).eulerAngles.y, null, g.propMaterial);
                npc.aggro = true;
            }
            g.Toast("AMBUSH! THE " + gang + " GO FOR THE GUILD RIG");
            MadMax.Audio.Sfx.Play("horn", at + ahead, 1f, 0.85f, 200f);
        }

        static void Arrive(WastelandGame g)
        {
            ai.Release();
            if (job != null)
            {
                job.completed = true;
                Contracts.Pay(g, job);
                Factions.Shift(Faction.FuelGuild, 6);
            }
            g.Toast("THE GUILD RIG MADE IT");
            Cleanup();
        }

        static void Fail(WastelandGame g, string why)
        {
            if (job != null) { job.failed = true; Factions.Shift(Faction.FuelGuild, -5); }
            g.Toast(why);
            if (ai) ai.Release();
            Cleanup();
        }

        /// <summary>The rig stays where it stopped; the ambushers fight on until they are dealt with.</summary>
        static void Cleanup() { job = null; route = null; ai = null; rig = null; }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
