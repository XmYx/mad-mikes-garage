using System.Collections.Generic;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>People of the town plans (<see cref="TownPlan"/>): residents walking the sidewalk loops round their
    /// blocks (pausing at the corners), stallkeepers on the market square or the village green, the town campfire, boss
    /// and bounty board on the square, and a gang lurking round a drum fire at its hangout — raiders who leave you be in
    /// daylight but go for anyone on foot who comes close after dark, unless parleyed with or friendly with the gang.</summary>
    public partial class NpcDirector
    {
        readonly List<Npc> gangs = new List<Npc>();
        readonly Dictionary<string, Vector3[]> beatCache = new Dictionary<string, Vector3[]>();

        /// <summary>The first spot of a kind in a town's plan (world position; y = 0).</summary>
        static bool PlanSpot(Settlement st, SpotKind kind, out Vector3 pos, out float yaw)
        {
            pos = default; yaw = 0f;
            if (st.plan == null) return false;
            foreach (var s in st.plan.spots)
                if (s.kind == kind) { pos = new Vector3(s.pos.x, 0f, s.pos.y); yaw = s.yaw; return true; }
            return false;
        }

        Vector3? PlanGround(Settlement st, SpotKind kind) => PlanSpot(st, kind, out var p, out _) ? Ground(new Vector2(p.x, p.z)) : (Vector3?)null;

        /// <summary>The gang that holds a town's hangout (deterministic from the world and the town).</summary>
        string TownGang(int town) => NpcLore.Gangs[(int)((uint)(game.World.seed * 31 + town * 7919) % (uint)NpcLore.Gangs.Length)];

        bool ScanTown(Settlement st, Vector3 focus)
        {
            var plan = st.plan;
            if (plan == null) return false;
            int idx = st.index;
            if (plan.beats.Count > 0)
            {
                int residents = st.kind == Biome.Village ? 4 : st.kind == Biome.Town ? 6 : 10;
                for (int i = 0; i < residents; i++)
                {
                    string id = "r" + idx + "," + i;
                    if (!beatCache.TryGetValue(id, out var beat))
                    {
                        var b = plan.beats[(i * 7 + idx) % plan.beats.Count];
                        beat = new Vector3[b.Length];
                        for (int k = 0; k < b.Length; k++) { var q = b[(k + i) % b.Length]; beat[k] = new Vector3(q.x, 0f, q.y); }
                        beatCache[id] = beat;
                    }
                    var at = Ground(new Vector2(beat[0].x, beat[0].z));
                    Want(new Spot { id = id, role = NpcRole.Resident, pos = at, radius = 10f, beat = beat }, focus);
                }
            }
            int stall = 0, gangN = 0;
            foreach (var s in plan.spots)
            {
                if (s.kind == SpotKind.Stall)
                {
                    Want(new Spot { id = stall == 0 ? "m" + idx : "m" + idx + "," + stall, role = NpcRole.Stallkeeper, kind = s.table, pos = Ground(s.pos), yaw = s.yaw, stall = 1 }, focus);
                    stall++;
                }
                else if (s.kind == SpotKind.Gang)
                {
                    bool city = st.kind == Biome.City;
                    int members = city ? 5 : 3;
                    for (int j = 0; j < members; j++, gangN++)
                    {
                        float a = j * Mathf.PI * 2f / members + idx;
                        var p = s.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.3f;
                        Want(new Spot { id = "g" + idx + "," + gangN, role = city && j == 0 ? NpcRole.RaiderBoss : NpcRole.Raider, pos = Ground(p), radius = 6f,
                                        yaw = Mathf.Atan2(s.pos.x - p.x, s.pos.y - p.y) * Mathf.Rad2Deg, town = idx, gang = true }, focus);
                    }
                }
            }
            return true;
        }

        /// <summary>Town gangs turn on a stranger on foot who walks up to them after dark (and calm down by day once
        /// the stranger is gone). A parley or a friendly standing keeps them off.</summary>
        void TickGangs()
        {
            if (gangs.Count == 0 || !game || !game.Player) return;
            var player = game.Current ? game.Current.transform.position : game.Player.transform.position;
            bool onFoot = !game.Current || game.Current.SpeedKmh < 12f;
            bool night = DayNight.Hours >= 20.5f || DayNight.Hours < 5.5f;
            for (int i = gangs.Count - 1; i >= 0; i--)
            {
                var n = gangs[i];
                if (!n) { gangs.RemoveAt(i); continue; }
                if (!n.Alive) continue;
                float d = Vector3.Distance(n.transform.position, player);
                bool friendly = Factions.Friendly(Factions.OfGang(n.Profile.gang)) || n.State.Has(NpcSave.Parleyed);
                if (!n.aggro && night && onFoot && !friendly && d < 15f && !game.Vitals.Dead)
                {
                    foreach (var m in gangs) if (m && m.Alive && (m.transform.position - n.transform.position).sqrMagnitude < 20f * 20f) { m.aggro = true; m.Attend(player); }
                    MadMax.Game.Hints.Show("town_gang", "A GANG HOLDS THIS CORNER AT NIGHT. TALK YOUR WAY PAST OR KEEP CLEAR.");
                }
                else if (n.aggro && !night && (d > 45f || friendly)) n.aggro = false;
            }
        }
    }
}
