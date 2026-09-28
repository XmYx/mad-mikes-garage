using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Solves the power and water networks twice a second: union of linked nodes per kind; power = supply +
    /// batteries vs demand (brown-out = nobody powered); water = pooled tanks, sources add dirty water, filters clean it.
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
            for (int i = 0; i < nodes.Count; i++)
                foreach (var l in nodes[i].links)
                    if (l.kind == kind && byId.TryGetValue(l.id, out int j)) parent[Find(i)] = Find(j);
            var map = new Dictionary<int, int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                int r = Find(i);
                if (!map.TryGetValue(r, out int ni)) { ni = nets.Count; map[r] = ni; nets.Add(new List<UtilityNode>()); }
                nets[ni].Add(nodes[i]);
                assign(nodes[i], ni);
            }
        }

        static void SolvePower(List<UtilityNode> net, float dt)
        {
            float supply = 0f, demand = 0f, stored = 0f, cap = 0f;
            foreach (var n in net) { if (!n) continue; supply += n.produce; demand += n.demand; stored += n.batteryCharge; cap += n.batteryWh; }
            float surplusWh = (supply - demand) * dt / 3600f;
            bool powered;
            if (surplusWh >= 0f)
            {
                powered = demand > 0f || supply > 0f;
                float room = cap - stored;
                float charge = Mathf.Min(room, surplusWh);
                Distribute(net, charge, cap);
            }
            else
            {
                powered = stored >= -surplusWh;
                if (powered) Distribute(net, surplusWh, cap);
            }
            foreach (var n in net) if (n) n.Powered = powered && (supply > 0f || stored > 0f);
        }

        static void Distribute(List<UtilityNode> net, float wh, float cap)
        {
            if (cap <= 0f || Mathf.Approximately(wh, 0f)) return;
            foreach (var n in net) if (n && n.batteryWh > 0f) n.batteryCharge = Mathf.Clamp(n.batteryCharge + wh * n.batteryWh / cap, 0f, n.batteryWh);
        }

        static void SolveWater(List<UtilityNode> net, float dt)
        {
            float cap = 0f, clean = 0f, dirtyW = 0f, filter = 0f;
            foreach (var n in net) { if (!n) continue; cap += n.waterCapacity; clean += n.clean; dirtyW += n.dirty + n.sourceDirty * dt; filter += n.filterRate; }
            float conv = Mathf.Min(dirtyW, filter * dt);
            dirtyW -= conv; clean += conv;
            float total = clean + dirtyW;
            if (total > cap) { float over = total - cap; float fromDirty = Mathf.Min(over, dirtyW); dirtyW -= fromDirty; clean -= over - fromDirty; }
            if (cap <= 0f) return;
            foreach (var n in net)
            {
                if (!n) continue;
                float share = n.waterCapacity / cap;
                n.clean = clean * share; n.dirty = dirtyW * share;
            }
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
