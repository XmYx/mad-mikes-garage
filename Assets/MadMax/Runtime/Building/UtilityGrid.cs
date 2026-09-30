using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Solves the power and water networks twice a second: union of linked nodes per kind (open switches and
    /// closed valves break the links through them); power = supply + batteries vs demand, served by priority (load
    /// breakers mark the loads behind them essential / normal / low: low loads shed first, and when essential or normal
    /// loads go dark the net is overloaded and its generators stall); water = pooled tanks, sources add dirty water with
    /// its taint, filters clean everything but salt, desalinators and stills clean anything.
    /// Also draws cables (sagging black lines) and pipes (straight grey lines).</summary>
    public static class UtilityGrid
    {
        public const float CableReach = 25f, PipeReach = 18f;
        static bool dirty = true;
        static float timer;
        static readonly List<List<UtilityNode>> powerNets = new List<List<UtilityNode>>(), waterNets = new List<List<UtilityNode>>();
        static readonly Dictionary<UtilityNode, int> index = new Dictionary<UtilityNode, int>();
        static readonly Dictionary<(uint, uint, UtilityKind), LineRenderer> lines = new Dictionary<(uint, uint, UtilityKind), LineRenderer>();
        static Material lineMat;
        static Transform root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { dirty = true; timer = 0f; powerNets.Clear(); waterNets.Clear(); index.Clear(); lines.Clear(); root = null; }

        public static void Invalidate() => dirty = true;

        public static void Tick(float dt)
        {
            timer += dt;
            if (timer < 0.5f) return;
            float step = timer; timer = 0f;
            if (dirty) { Rebuild(); dirty = false; }
            foreach (var net in powerNets) SolvePower(net, step);
            foreach (var net in waterNets) SolveWater(net, step);
            UpdateLines();
        }

        static void Rebuild()
        {
            Group(UtilityKind.Power, powerNets, (n, i) => n.powerNet = i);
            Group(UtilityKind.Water, waterNets, (n, i) => n.waterNet = i);
            Priorities();
        }

        static void Group(UtilityKind kind, List<List<UtilityNode>> nets, System.Action<UtilityNode, int> assign)
        {
            nets.Clear(); index.Clear();
            var nodes = new List<UtilityNode>();
            foreach (var n in UtilityNode.All) if (n && (n.kinds & kind) != 0) { index[n] = nodes.Count; nodes.Add(n); }
            var parent = new int[nodes.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            var byId = new Dictionary<uint, int>();
            for (int i = 0; i < nodes.Count; i++) byId[nodes[i].Id] = i;
            // an open switch / closed valve conducts nothing of the kind it cuts: it stands alone
            for (int i = 0; i < nodes.Count; i++)
            {
                if ((nodes[i].cut & kind) != 0) continue;
                foreach (var l in nodes[i].links)
                    if (l.kind == kind && byId.TryGetValue(l.id, out int j) && (nodes[j].cut & kind) == 0) parent[Find(i)] = Find(j);
            }
            // everything on one vehicle shares its wiring and plumbing (onboard generator, water tank, built pieces)
            var byVehicle = new Dictionary<MadMax.Vehicles.VehicleChassis, int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if ((nodes[i].cut & kind) != 0) continue;
                var v = nodes[i].GetComponentInParent<MadMax.Vehicles.VehicleChassis>();
                if (!v) continue;
                if (byVehicle.TryGetValue(v, out int j)) parent[Find(i)] = Find(j); else byVehicle[v] = i;
            }
            var map = new Dictionary<int, int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                int r = Find(i);
                if (!map.TryGetValue(r, out int ni)) { ni = nets.Count; map[r] = ni; nets.Add(new List<UtilityNode>()); }
                nets[ni].Add(nodes[i]);
                assign(nodes[i], ni);
            }
        }

        /// <summary>Loads behind a breaker (every component of the net without the breaker that holds no power source)
        /// take its priority; a load behind breakers in series takes the nearest one's.</summary>
        static void Priorities()
        {
            foreach (var net in powerNets) foreach (var n in net) if (n) n.priority = 1;
            foreach (var net in powerNets)
            {
                bool any = false;
                foreach (var n in net) if (n && n.breaker >= 0) { any = true; break; }
                if (!any) continue;
                var at = new Dictionary<uint, int>();
                for (int i = 0; i < net.Count; i++) if (net[i]) at[net[i].Id] = i;
                var adj = new List<int>[net.Count];
                for (int i = 0; i < net.Count; i++) adj[i] = new List<int>();
                var byVehicle = new Dictionary<MadMax.Vehicles.VehicleChassis, int>();
                for (int i = 0; i < net.Count; i++)
                {
                    if (!net[i]) continue;
                    foreach (var l in net[i].links)
                        if (l.kind == UtilityKind.Power && at.TryGetValue(l.id, out int j) && j != i) { adj[i].Add(j); adj[j].Add(i); }
                    var v = net[i].GetComponentInParent<MadMax.Vehicles.VehicleChassis>();
                    if (!v) continue;
                    if (byVehicle.TryGetValue(v, out int k)) { adj[i].Add(k); adj[k].Add(i); } else byVehicle[v] = i;
                }
                var sides = new List<(int prio, List<int> nodes)>();
                var queue = new Queue<int>();
                for (int b = 0; b < net.Count; b++)
                {
                    if (!net[b] || net[b].breaker < 0) continue;
                    var seen = new bool[net.Count];
                    seen[b] = true;
                    foreach (int start in adj[b])
                    {
                        if (seen[start]) continue;
                        var comp = new List<int>();
                        bool source = false;
                        seen[start] = true; queue.Enqueue(start);
                        while (queue.Count > 0)
                        {
                            int x = queue.Dequeue();
                            comp.Add(x);
                            if (net[x] && net[x].IsSource) source = true;
                            foreach (int y in adj[x]) if (!seen[y]) { seen[y] = true; queue.Enqueue(y); }
                        }
                        if (!source) sides.Add((net[b].breaker, comp));
                    }
                }
                sides.Sort((a, b) => b.nodes.Count.CompareTo(a.nodes.Count));      // the nearest breaker (smallest side) wins
                foreach (var (prio, nodes) in sides) foreach (int x in nodes) if (net[x]) net[x].priority = Mathf.Clamp(prio, 0, 2);
            }
        }

        static readonly float[] tierDemand = new float[3];

        static void SolvePower(List<UtilityNode> net, float dt)
        {
            float supply = 0f, stored = 0f, cap = 0f;
            tierDemand[0] = tierDemand[1] = tierDemand[2] = 0f;
            foreach (var n in net) { if (!n) continue; supply += n.produce; tierDemand[Mathf.Clamp(n.priority, 0, 2)] += n.demand + n.auxDemand; stored += n.batteryCharge; cap += n.batteryWh; }
            // serve the tiers in order (essential, normal, low) while the producers plus the batteries carry them
            float served = 0f;
            int level = -1;
            for (int p = 0; p < 3; p++)
            {
                float need = served + tierDemand[p];
                if (tierDemand[p] > 0f && (need - supply) * dt / 3600f > stored) break;
                served = need; level = p;
            }
            float surplusWh = (supply - served) * dt / 3600f;
            if (surplusWh >= 0f) Distribute(net, Mathf.Min(cap - stored, surplusWh), cap);
            else Distribute(net, surplusWh, cap);
            bool on = (served > 0f || supply > 0f) && (supply > 0f || stored > 0f);
            bool overloaded = false;                                   // essential or normal loads left dark
            for (int p = level + 1; p < 2; p++) if (tierDemand[p] > 0f) overloaded = true;
            foreach (var n in net)
            {
                if (!n) continue;
                n.Powered = on && n.priority <= level;
                n.Shed = on && n.priority > level;
                n.Overloaded = overloaded;
                n.NetLevel = level;
                n.load = supply > 0f ? Mathf.Min(served, supply) * n.produce / supply : 0f;
            }
        }

        static void Distribute(List<UtilityNode> net, float wh, float cap)
        {
            if (cap <= 0f || Mathf.Approximately(wh, 0f)) return;
            foreach (var n in net) if (n && n.batteryWh > 0f) n.batteryCharge = Mathf.Clamp(n.batteryCharge + wh * n.batteryWh / cap, 0f, n.batteryWh);
        }

        static void SolveWater(List<UtilityNode> net, float dt)
        {
            float cap = 0f, clean = 0f, dirtyW = 0f, filter = 0f, desal = 0f;
            var taint = WaterTaint.None;
            foreach (var n in net)
            {
                if (!n) continue;
                cap += n.waterCapacity; clean += n.clean + n.sourceClean * dt; dirtyW += n.dirty + n.sourceDirty * dt; filter += n.filterRate; desal += n.desalRate;
                if (n.dirty > 0.01f) taint |= n.taint == WaterTaint.None ? WaterTaint.Silt : n.taint;
                if (n.sourceDirty > 0f) taint |= n.sourceTaint == WaterTaint.None ? WaterTaint.Silt : n.sourceTaint;
            }
            // desalinators and stills take anything out, filters everything but salt
            float byDesal = Mathf.Min(dirtyW, desal * dt);
            dirtyW -= byDesal; clean += byDesal;
            float byFilter = (taint & WaterTaint.Salt) != 0 ? 0f : Mathf.Min(dirtyW, filter * dt);
            dirtyW -= byFilter; clean += byFilter;
            var carried = taint;
            if (dirtyW <= 0.001f) taint = WaterTaint.None;
            float total = clean + dirtyW;
            if (total > cap) { float over = total - cap; float fromDirty = Mathf.Min(over, dirtyW); dirtyW -= fromDirty; clean -= over - fromDirty; }
            if (cap <= 0f) return;
            foreach (var n in net)
            {
                if (!n) continue;
                float share = n.waterCapacity / cap;
                n.clean = clean * share; n.dirty = dirtyW * share;
                n.taint = taint;
                float mine = (desal > 0f ? byDesal * n.desalRate / desal : 0f) + (filter > 0f ? byFilter * n.filterRate / filter : 0f);
                if (mine > 0f) { n.converted += mine; n.convertedTaint |= carried; }
            }
        }

        /// <summary>Litres the node's water network can hold.</summary>
        public static float NetCapacity(UtilityNode node)
        {
            if (!node) return 0f;
            if (node.waterNet < 0 || node.waterNet >= waterNets.Count) return node.waterCapacity;
            float c = 0f;
            foreach (var n in waterNets[node.waterNet]) if (n) c += n.waterCapacity;
            return c;
        }

        public static float NetWater(UtilityNode node, out float cleanShare)
        {
            cleanShare = 0f;
            if (!node || node.waterNet < 0 || node.waterNet >= waterNets.Count) return node ? node.Water : 0f;
            float c = 0f, d = 0f;
            foreach (var n in waterNets[node.waterNet]) if (n) { c += n.clean; d += n.dirty; }
            cleanShare = c + d > 0f ? c / (c + d) : 0f;
            return c + d;
        }

        /// <summary>Draw water from the node's network (clean first). Returns litres drawn; <paramref name="gotClean"/> = all clean.</summary>
        public static float Draw(UtilityNode node, float litres, out bool gotClean)
        {
            gotClean = true;
            if (!node) return 0f;
            var net = node.waterNet >= 0 && node.waterNet < waterNets.Count ? waterNets[node.waterNet] : new List<UtilityNode> { node };
            float got = 0f;
            foreach (var n in net) { if (!n) continue; float t = Mathf.Min(n.clean, litres - got); n.clean -= t; got += t; }
            if (got < litres)
            {
                foreach (var n in net) { if (!n) continue; float t = Mathf.Min(n.dirty, litres - got); if (t > 0f) gotClean = false; n.dirty -= t; got += t; }
            }
            node.Piece?.Dirty();
            return got;
        }

        // ------------------------------------------------------------------ drawing
        static void UpdateLines()
        {
            if (!root) root = new GameObject("UtilityLines").transform;
            if (!lineMat) lineMat = MadMax.World.Fx.TransparentMaterial(null);
            var seen = new HashSet<(uint, uint, UtilityKind)>();
            foreach (var n in UtilityNode.All)
            {
                if (!n) continue;
                foreach (var l in n.links)
                {
                    var other = Find(l.id);
                    if (!other) continue;
                    var key = (n.Id, l.id, l.kind);
                    seen.Add(key);
                    if (!lines.TryGetValue(key, out var lr) || !lr)
                    {
                        lr = new GameObject(l.kind == UtilityKind.Power ? "Cable" : "Pipe").AddComponent<LineRenderer>();
                        lr.transform.SetParent(root, false);
                        lr.sharedMaterial = lineMat;
                        lr.positionCount = l.kind == UtilityKind.Power ? 9 : 2;
                        lr.widthMultiplier = l.kind == UtilityKind.Power ? 0.035f : 0.09f;
                        var c = l.kind == UtilityKind.Power ? new Color(0.08f, 0.07f, 0.07f, 1f) : new Color(0.45f, 0.47f, 0.5f, 1f);
                        lr.startColor = lr.endColor = c;
                        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        lines[key] = lr;
                    }
                    Vector3 a = n.PortWorld, b = other.PortWorld;
                    if (l.kind == UtilityKind.Power)
                    {
                        float sag = Vector3.Distance(a, b) * 0.06f;
                        for (int i = 0; i < 9; i++) { float t = i / 8f; lr.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t)); }
                    }
                    else { lr.SetPosition(0, a); lr.SetPosition(1, b); }
                }
            }
            var drop = new List<(uint, uint, UtilityKind)>();
            foreach (var kv in lines) if (!seen.Contains(kv.Key)) { if (kv.Value) Object.Destroy(kv.Value.gameObject); drop.Add(kv.Key); }
            foreach (var k in drop) lines.Remove(k);
        }

        static UtilityNode Find(uint id)
        {
            foreach (var n in UtilityNode.All) if (n && n.Id == id) return n;
            return null;
        }
    }
}
