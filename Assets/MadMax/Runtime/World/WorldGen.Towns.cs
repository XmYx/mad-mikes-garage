using System.Collections.Generic;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Town plans in the world (coherent towns): radii by kind (village 55 m, town 80 m, city 120 m, capped by
    /// the nearest neighbour), one <see cref="TownPlan"/> per settlement, highways rerouted along each plan's boulevards
    /// (round its roundabouts, ending on the central ring; off-axis approaches swing round on a bypass arc outside the
    /// lots) and regraded to the street level near towns. <see cref="PlanAt"/> finds a plan by position (any thread).</summary>
    public partial class WorldGen
    {
        /// <summary>Bump when town layouts change: placed props are keyed by their index in the layout.</summary>
        public const int TownPlanVersion = 2;   // 2: mountable kerbs (ramped raise), flush roundabout islands

        const float PlanCell = 64f;
        readonly Dictionary<Vector2Int, TownPlan[]> planCells = new Dictionary<Vector2Int, TownPlan[]>();

        /// <summary>Ground cover grows here: open ground, mesa rock, park and yard lawns, roundabout islands, fields, bare lots.</summary>
        public static bool Grows(byte f) => f < 2 || f == TownPlan.FPark || f == TownPlan.FYard || f == TownPlan.FIsland || f == TownPlan.FField || f == TownPlan.FLot;
        /// <summary>A hoe or a seeder works it (open ground, lawns, fields, bare lots).</summary>
        public static bool Tillable(byte f) => f == 0 || f == TownPlan.FPark || f == TownPlan.FYard || f == TownPlan.FField || f == TownPlan.FLot;
        /// <summary>Sealed town ground: streets, paint, sidewalks, kerbs, squares, car parks.</summary>
        public static bool HardTown(byte f) => f == TownPlan.FSidewalk || f == TownPlan.FPlaza || f == TownPlan.FParking || f == TownPlan.FStreet
                                               || f == TownPlan.FPaint || f == TownPlan.FKerb || f == TownPlan.FYellow;
        public static bool TownFeature(byte f) => f >= TownPlan.FSidewalk && f <= TownPlan.FYellow;

        /// <summary>The town plan whose graded area covers a point (null: none).</summary>
        public TownPlan PlanAt(float x, float z)
        {
            if (!planCells.TryGetValue(new Vector2Int(Mathf.FloorToInt(x / PlanCell), Mathf.FloorToInt(z / PlanCell)), out var list)) return null;
            foreach (var p in list)
            {
                float dx = x - p.centre.x, dz = z - p.centre.y, o = p.Outer;
                if (dx * dx + dz * dz < o * o) return p;
            }
            return null;
        }

        /// <summary>Inside a planned town's streets and lots (not its shoulder).</summary>
        public bool InTown(float x, float z)
        {
            var p = PlanAt(x, z);
            if (p == null) return false;
            float dx = x - p.centre.x, dz = z - p.centre.y;
            return dx * dx + dz * dz < (p.radius + 2f) * (p.radius + 2f);
        }

        static bool At(Vector3 p, Vector2 town) => (new Vector2(p.x, p.z) - town).sqrMagnitude < 1f;

        /// <summary>Radii, plan skeletons and the highways rerouted through them (before the start yard is chosen).</summary>
        void BuildTownPlans()
        {
            int n = settlements.Count;
            var near = new float[n];
            for (int i = 0; i < n; i++)
            {
                near[i] = 4000f;
                for (int j = 0; j < n; j++) if (i != j) near[i] = Mathf.Min(near[i], (settlements[i].pos - settlements[j].pos).magnitude);
            }
            var rnd = new System.Random(seed * 389 + 3);
            for (int i = 0; i < n; i++)
            {
                var st = settlements[i];
                float want = st.kind == Biome.Village ? 62f : st.kind == Biome.Town ? 80f : 120f;
                st.radius = Mathf.Max(36f, Mathf.Min(want, near[i] * 0.5f - TownPlan.CutOut - 6f));
                float angle = (float)rnd.NextDouble() * Mathf.PI * 2f;
                foreach (var road in roads.roads)
                {
                    var pts = road.points;
                    bool first = At(pts[0], st.pos), last = At(pts[pts.Count - 1], st.pos);
                    if (!first && !last) continue;
                    for (int k = 0; k < pts.Count; k++)
                    {
                        var p = pts[first ? k : pts.Count - 1 - k];
                        var d = new Vector2(p.x, p.z) - st.pos;
                        if (d.magnitude < st.radius + TownPlan.CutOut) continue;
                        angle = Mathf.Atan2(d.y, d.x);
                        break;
                    }
                    break;
                }
                st.plan = new TownPlan(this, st, angle, near[i] * 0.5f - 2f);
            }
            foreach (var road in roads.roads)
            {
                var list = new List<Vector3>(road.points);
                Settlement a = null, b = null;
                foreach (var st in settlements) { if (At(list[0], st.pos)) a = st; if (At(list[list.Count - 1], st.pos)) b = st; }
                if (a != null) list = Reroute(list, a.plan);
                if (b != null) { list.Reverse(); list = Reroute(list, b.plan); list.Reverse(); }
                // the approaches ease onto each town's street level
                for (int k = 0; k < list.Count; k++)
                {
                    var p = list[k];
                    foreach (var st in settlements)
                    {
                        float d = (new Vector2(p.x, p.z) - st.pos).magnitude, r0 = st.radius + 2f;
                        if (d > r0 + 60f) continue;
                        float w = Mathf.Clamp01(1f - (d - r0) / 60f); w = w * w * (3f - 2f * w);
                        p.y = Mathf.Lerp(p.y, st.plan.LevelAt(p.x, p.z), w);
                    }
                    list[k] = p;
                }
                road.points.Clear(); road.points.AddRange(list);
            }
            roads.Rebuild();
        }

        /// <summary>A highway (points ordered out of the town) rerouted inside a plan: hub → boulevard → connector → the
        /// original road from the first point <see cref="TownPlan.CutOut"/> beyond the edge.</summary>
        static List<Vector3> Reroute(List<Vector3> pts, TownPlan plan)
        {
            float cut = plan.radius + TownPlan.CutOut;
            int ic = pts.Count - 1;
            for (int i = 0; i < pts.Count; i++) if ((new Vector2(pts[i].x, pts[i].z) - plan.centre).magnitude >= cut) { ic = i; break; }
            var lc = plan.Local(pts[ic].x, pts[ic].z);
            int k = TownPlan.AxisOf(lc);
            plan.MarkAxis(k);
            var o = new List<Vector3>();
            void Add(Vector2 l) { var w = plan.World(l); o.Add(new Vector3(w.x, plan.Level(l), w.y)); }
            foreach (var l in plan.PathOut(k)) Add(l);
            foreach (var l in plan.Connector(k, lc)) Add(l);
            for (int i = ic; i < pts.Count; i++) o.Add(pts[i]);
            return o;
        }

        /// <summary>Streets, lots and dressing of every plan, then the lookup grid (after the start yard is known).</summary>
        void FinishTownPlans()
        {
            foreach (var st in settlements)
            {
                st.plan.Build(this);
                st.height = st.plan.LevelAt(st.pos.x, st.pos.y);
                float o = st.plan.Outer;
                var tmp = new List<TownPlan>();
                for (int cx = Mathf.FloorToInt((st.pos.x - o) / PlanCell); cx <= Mathf.FloorToInt((st.pos.x + o) / PlanCell); cx++)
                for (int cz = Mathf.FloorToInt((st.pos.y - o) / PlanCell); cz <= Mathf.FloorToInt((st.pos.y + o) / PlanCell); cz++)
                {
                    var key = new Vector2Int(cx, cz);
                    tmp.Clear();
                    if (planCells.TryGetValue(key, out var old)) tmp.AddRange(old);
                    tmp.Add(st.plan);
                    planCells[key] = tmp.ToArray();
                }
            }
        }
    }
}
