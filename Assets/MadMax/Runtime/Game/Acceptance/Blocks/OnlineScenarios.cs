using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MadMax.Building;
using MadMax.Items;
using MadMax.Net;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game.Acceptance
{
    /// <summary>Acceptance scenarios of the online/road wave: world items and work poses over the wire (the message
    /// round trips and the host-side handlers, run in one process: there is no multi-process harness in the editor),
    /// build-mode costs in the item feed, and AI drivers routing over a road the player paved.</summary>
    public static class OnlineScenarios
    {
        public static IEnumerable<Scenario> All()
        {
            yield return new OnlineItems();
            yield return new OnlineWorkPose();
            yield return new BuildFeed();
            yield return new AiPlayerRoad();
        }

        public static NetReader Reader(NetWriter w) { var r = new NetReader(); r.Set(w.Buffer, w.Length); return r; }

        public static int CountId(uint id) => WorldItem.All.Count(w => w && w.netId == id);
    }

    /// <summary>World items online: an ItemSpawn written on one peer and applied on another makes exactly one item with
    /// the sender's id (a repeat or a count update never makes a second); a move puts it where the host's copy rests;
    /// the host hands a stack to the first pick-up request only (a second request finds nothing), the picker's grant
    /// fills the pack once even if it arrived twice, and a spawn for a taken id is ignored; the world transfer
    /// (<c>CaptureSave(forNetwork)</c> through JSON + gzip) carries the items with their ids.</summary>
    class OnlineItems : Scenario
    {
        public override string Id => "online.items";

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ItemsScenarios.OnFootAtPad(c, 5f);
            c.Check(NetSession.ProtocolVersion >= 4, "protocol 4 carries world items and work poses (version " + NetSession.ProtocolVersion + ")");
            c.Check(!NetSession.Instance, "offline: the handlers run without a session (single process)");
            const string food = "food_can";
            const uint id = 0x7E000001;                                                       // a remote peer's entity id
            var t = g.Player.transform;
            var at = t.position + t.forward * 1.2f + Vector3.up * 0.5f;

            // ---- a spawn from "the other peer": encoded there, the sender's copy is not in this world
            var sender = g.SpawnWorldItem(food, 3, -1f, at, Quaternion.identity, null);
            sender.netId = id;
            var w = new NetWriter(128);
            NetSession.WriteItemSpawn(w, sender, new Vector3(0f, 0.5f, 1f));
            sender.gameObject.SetActive(false); Object.Destroy(sender.gameObject);
            c.Fixture("an ItemSpawn for 3 canned food written by a stand-in sender, whose copy is then removed");
            yield return null;
            c.Check(OnlineScenarios.CountId(id) == 0, "before the message: no item with the sender's id here");
            var got = NetSession.ApplyItemSpawn(OnlineScenarios.Reader(w), g, out var vel);
            c.Check(got && got.netId == id && got.key == food && got.count == 3 && ItemsScenarios.HasMesh(got), "applied: one canned-food stack of 3 with the sender's id and its mesh");
            c.Check(!NetSession.Applying, "Applying is cleared after the handler");
            c.Check(Mathf.Abs(vel.z - 1f) < 0.02f && got && got.Body && !got.Body.isKinematic, $"it flies off with the sender's toss ({vel.z:0.00} m/s forward)");
            NetSession.ApplyItemSpawn(OnlineScenarios.Reader(w), g, out _);
            c.Check(OnlineScenarios.CountId(id) == 1, "the same message again (a relay echo) makes no second item");
            if (!got) yield break;

            // ---- a merge on the host: the same id with a new count updates the stack
            got.count = 5;
            var w5 = new NetWriter(128); NetSession.WriteItemSpawn(w5, got, Vector3.zero);
            got.count = 3; got.Refresh();
            NetSession.ApplyItemSpawn(OnlineScenarios.Reader(w5), g, out _);
            c.Check(OnlineScenarios.CountId(id) == 1 && got.count == 5 && got.Label.StartsWith("5 "), "a stack update (merge) sets the count to 5 on the one item: " + got.Label);

            // ---- the host's resting pose
            yield return new WaitForSeconds(1.5f);
            var rest = got.transform.position + t.right * 0.6f;
            var rot = Quaternion.Euler(0f, 70f, 0f);
            var wm = new NetWriter(32); wm.UInt(id); wm.Pos(rest); wm.Rot(rot);
            var moved = NetSession.ApplyItemMove(OnlineScenarios.Reader(wm));
            c.Check(moved == got && Vector3.Distance(got.transform.position, rest) < 0.01f && Quaternion.Angle(got.transform.rotation, rot) < 1f, "ItemMove puts it where the host's copy rests");

            // ---- the world transfer carries it with its id (JSON + gzip, like the join payload)
            var d = g.CaptureSave(forNetwork: true);
            var back = JsonUtility.FromJson<SaveData>(NetCompress.Unzip(NetCompress.Zip(JsonUtility.ToJson(d))));
            var line = back.blockItems != null ? back.blockItems.FirstOrDefault(l => l.Split('|').Length > 10 && l.Split('|')[10] == id.ToString()) : null;
            c.Check(line != null && line.Split('|')[1] == food && line.Split('|')[2] == "5", "the join payload (blockItems) carries the stack with its id: " + (line ?? "missing"));

            // ---- pick-up: the host hands it to the first request only
            int cans = g.Inventory.GetItem(food);
            bool first = NetSession.HostTakeItem(id, out var key, out int count, out float q);
            c.Check(first && key == food && count == 5, $"the host grants the first request ({key} x{count})");
            bool second = NetSession.HostTakeItem(id, out _, out int count2, out _);
            c.Check(!second && count2 == 0, "a second request for the same item gets nothing (no duplicate)");
            c.Check(g.Inventory.GetItem(food) == cans, "the host's own pack is untouched by a client's pick-up");
            yield return null;
            c.Check(OnlineScenarios.CountId(id) == 0 && !NetSession.FindItem(id), "the item is gone from the host's world");

            // ---- the picker's side: the grant fills the pack once
            var wg = new NetWriter(64); NetSession.WriteItemGrant(wg, id, key, count, q);
            ItemFeed.Clear();
            bool granted = NetSession.ApplyItemGrant(OnlineScenarios.Reader(wg), g);
            bool again = NetSession.ApplyItemGrant(OnlineScenarios.Reader(wg), g);
            c.Check(granted && !again && g.Inventory.GetItem(food) == cans + 5, $"the grant puts 5 cans in the picker's pack once ({cans} -> {g.Inventory.GetItem(food)})");
            var row = ItemFeed.Find(food, ResourceType.None, "PICKED UP");
            c.Check(row != null && row.amount == 5, "the feed shows +5 CANNED FOOD, PICKED UP");
            c.Check(!NetSession.ApplyItemSpawn(OnlineScenarios.Reader(w), g, out _) && OnlineScenarios.CountId(id) == 0, "a late spawn for the taken id is ignored");

            // ---- a gone message removes a peer's copy
            const uint id2 = 0x7E000002;
            var other = g.SpawnWorldItem("res:" + (int)ResourceType.Scrap, 4, -1f, at + t.right, Quaternion.identity, null);
            other.netId = id2;
            yield return null;
            NetSession.ApplyItemGone(id2);
            yield return null;
            c.Check(OnlineScenarios.CountId(id2) == 0 && !other, "ItemGone removes the local copy");

            // ---- offline saves keep no stale ids after a load (the host numbers items afresh)
            var dropped = g.DropFromPack(food, 1);
            if (dropped) dropped.netId = 0x7E000003;
            var sd = new SaveData(); g.SaveWorldItems(sd);
            c.Check(sd.blockItems.Any(l => l.EndsWith("|" + 0x7E000003u)), "a networked item's save line ends with its id");
            g.LoadWorldItems(sd);
            yield return null;
            c.Check(WorldItem.All.Count > 0 && WorldItem.All.All(x => x.netId == 0), "loading outside a joined session leaves the ids unset");
            foreach (var x in WorldItem.All.ToList()) g.PickUpItem(x);
        }
    }

    /// <summary>Work poses online: the WorkPose message round-trips; applied to another player's avatar it takes the
    /// working pose with the job's prop in its right hand (the jug, the can, the wrench or the welder) and the stop
    /// clears it; the pose table the avatar reads kneels for a wheel and leans in for the engine bay.</summary>
    class OnlineWorkPose : Scenario
    {
        public override string Id => "online.work_pose";

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ItemsScenarios.OnFootAtPad(c, 4f);
            var t = g.Player.transform;
            var av = RemoteAvatar.Create(250, "PEER", new Appearance(), new List<string>(), g.propMaterial);
            av.transform.SetPositionAndRotation(t.position + t.forward * 2f, Quaternion.LookRotation(-t.forward));
            c.Fixture("another player's avatar 2 m in front of the player (no session: posed from the message only)");
            yield return null;

            var w = new NetWriter(32);
            NetSession.WriteWorkPose(w, 250, true, 2, ItemIds.Wrench);                          // Kneel with the wrench
            bool read = NetSession.ReadWorkPose(OnlineScenarios.Reader(w), out ushort id, out bool on, out byte pose, out string prop);
            c.Check(read && id == 250 && on && pose == 2 && prop == ItemIds.Wrench, "WorkPose round-trips (player, on, pose, prop)");
            av.SetWork(on, pose, prop);
            var mf = av.WorkProp ? av.WorkProp.GetComponent<MeshFilter>() : null;
            c.Check(av.Working && av.WorkPoseIndex == 2, "the avatar is working in pose 2");
            c.Check(mf && mf.sharedMesh && mf.sharedMesh.vertexCount > 0 && av.WorkProp.transform.parent == av.Rig.RightHand, "the wrench prop is in the avatar's right hand");
            var kneel = WastelandGame.WorkPoseAt(2, 0.5f); var lean = WastelandGame.WorkPoseAt(0, 0.5f);
            c.Check(kneel.knees > 90f && lean.chestX > 30f && lean.knees < 20f, $"the pose table kneels (knees {kneel.knees:0}°) and leans in (chest {lean.chestX:0}°)");
            foreach (var key in new[] { "oil", "can", "tool_welder" })
                c.Check(WastelandGame.WorkPropMesh(key), "prop '" + key + "' has a mesh");
            c.Screenshot("remote_work");
            yield return null;

            var ws = new NetWriter(16); NetSession.WriteWorkPose(ws, 250, false, 0, null);
            NetSession.ReadWorkPose(OnlineScenarios.Reader(ws), out _, out bool on2, out byte pose2, out string prop2);
            av.SetWork(on2, pose2, prop2);
            yield return null;
            c.Check(!av.Working && !av.WorkProp, "the stop message ends the pose and puts the prop away");
            Object.Destroy(av.gameObject);
        }
    }

    /// <summary>Build-mode costs in the item feed: repairing a piece shows its materials as REPAIRED, upgrading shows
    /// the new piece's cost as UPGRADED and the half of the old one that comes back as SALVAGED.</summary>
    class BuildFeed : Scenario
    {
        public override string Id => "items.build_feed";

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game;
            yield return ItemsScenarios.OnFootAtPad(c, 5f);
            yield return null;                                                                // the feed hooks in on a playing frame
            var def = FurnitureLibrary.All.FirstOrDefault(d => d.plan < 0 && d.upgrade != null && d.kit == null && d.cost != null && d.cost.Any(x => x.amount >= 2)
                                                               && FurnitureLibrary.Get(d.upgrade) is FurnitureDef u && u.kit == null && u.cost != null && u.cost.Length > 0);
            if (def == null) { c.Block("no upgradable piece with a raw cost"); yield break; }
            var up = FurnitureLibrary.Get(def.upgrade);
            var t = g.Player.transform;
            var at = t.position + t.forward * 3f;
            var piece = FurnitureLibrary.Spawn(def.id, g.Build.Structures, g.Build.Structures.InverseTransformPoint(at), Quaternion.LookRotation(t.forward), g.propMaterial);
            if (!c.Check(piece, "spawned " + def.id)) yield break;
            piece.owner = g.Stats.name;
            foreach (var d in new[] { def, up }) foreach (var (res, n) in d.cost) g.Inventory.Add(res, n * 4 + 20);
            c.Fixture($"a {def.id} (upgrades to {up.id}) 3 m ahead, the player's; its and the upgrade's materials x4 + 20 in the pack");
            yield return null;

            // ---- repair
            piece.hits = Mathf.Max(1, def.hits / 3);
            ItemFeed.Clear();
            g.Build.Repair(piece);
            var (r0, _) = def.cost[0];
            var rep = ItemFeed.Find(null, r0, "REPAIRED", false);
            c.Check(piece.hits == def.hits && rep != null && rep.amount < 0, $"repairing shows {(rep != null ? rep.text : "nothing")}, REPAIRED");

            // ---- upgrade
            yield return new WaitForSecondsRealtime(ItemFeed.MergeWindow + 0.1f);
            ItemFeed.Clear();
            g.Build.Upgrade(piece);
            yield return null;
            var (u0, _) = up.cost[0];
            var paid = ItemFeed.Find(null, u0, "UPGRADED", false);
            c.Check(paid != null && paid.amount < 0, $"upgrading to {up.id} shows {(paid != null ? paid.text : "nothing")}, UPGRADED");
            var back = def.cost.FirstOrDefault(x => x.amount / 2 > 0);
            var salvaged = ItemFeed.Rows.FirstOrDefault(r => r.res == back.type && r.source == "SALVAGED" && r.amount > 0);
            c.Check(salvaged != null, $"half the old cost back shows {(salvaged != null ? salvaged.text : "nothing")}, SALVAGED");
            c.Screenshot("build_feed");
        }
    }

    /// <summary>AI on the player's roads: an L-shaped gravel road paved off the generated network (32 m, a right angle,
    /// 24 m) between a start and a goal diagonally apart; the driving route (<c>RoadRoute.FindForDriving</c>) goes round
    /// the corner along the paving rather than across the open ground, and an <c>AiDriver</c> sent there with
    /// <c>DriveTo</c> drives it: through the corner, mostly on the paving, parking at the goal.</summary>
    class AiPlayerRoad : Scenario
    {
        public override string Id => "roads.ai_player_road";
        public override float Timeout => 150f;

        const float LegA = 32f, LegB = 24f, Lead = 8f;

        static Vector3 Ground(Vector3 p) { var t = DeformableTerrain.Instance; return new Vector3(p.x, t.Height(p.x, p.z), p.z); }
        static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            p.y = a.y = b.y = 0f;
            var ab = b - a; float k = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * k);
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            var g = c.Game; var t = DeformableTerrain.Instance;
            if (g.Current) { g.Exit(); yield return new WaitForSeconds(0.4f); }
            if (!Site(out var a, out var dir, out var side)) { c.Block("no level, clear L-shaped site off the roads near the start"); yield break; }
            var b = a + dir * LegA; var cc = b + side * LegB;
            var start = a - dir * Lead; var goal = cc + side * Lead;
            c.Fixture($"test site off the roads: road corner {a.x:0},{a.z:0} -> {b.x:0},{b.z:0} -> {cc.x:0},{cc.z:0}");
            g.Player.Teleport(Ground(a - side * 12f) + Vector3.up * 0.3f, 0f);
            c.Fixture("the player 12 m beside the first leg, out of the way");
            yield return new WaitForSeconds(1.5f);
            Physics.SyncTransforms();
            if (!Usable(t, start, b, a) || !Usable(t, b, goal, a)) { c.Block("the site is not clear once its props streamed in"); yield break; }

            // ---- pave the L in gravel, 3.4 m wide, the corner rounded
            int v0 = t.PlayerRoadVersion;
            foreach (var (p0, p1) in new[] { (a, b), (b, cc) })
            {
                float len = Flat(p0, p1);
                for (float s = 0f; s <= len; s += 1.2f) t.ApplyTerraform((byte)DeformableTerrain.TerraOp.Pave, Ground(Vector3.Lerp(p0, p1, s / len)), 1.7f, 0f, DeformableTerrain.PaveGravel);
            }
            t.ApplyTerraform((byte)DeformableTerrain.TerraOp.Pave, Ground(b), 2.6f, 0f, DeformableTerrain.PaveGravel);
            c.Fixture("an L of gravel road paved with the Pave terraform op (as the rake and paver lay it)");
            yield return null;
            var midA = (a + b) * 0.5f; var midB = (b + cc) * 0.5f;
            c.Check(t.PaveAt(midA.x, midA.z) == DeformableTerrain.PaveGravel && t.PaveAt(b.x, b.z) == DeformableTerrain.PaveGravel, "the gravel is down along the L");
            c.Check(t.PlayerRoadAt(midA.x, midA.z, out _) && t.PlayerRoadAt(b.x, b.z, out _) && t.PlayerRoadAt(midB.x, midB.z, out _), "both legs and the corner count as the player's road");
            c.Check(t.PlayerRoadVersion != v0, "the road version moved (route caches rebuild)");

            // ---- the driving route takes the paving
            var route = new List<Vector3>();
            bool ok = RoadRoute.FindForDriving(g.World, start, goal, route, out bool viaPlayer);
            float corner = route.Count > 0 ? route.Min(p => Flat(p, b)) : 999f;
            float len2 = 0f; for (int i = 1; i < route.Count; i++) len2 += Flat(route[i - 1], route[i]);
            c.Metric("route_points", route.Count, "");
            c.Metric("route_length", len2, "m");
            c.Metric("straight_length", Flat(start, goal), "m");
            c.Check(ok && viaPlayer, $"a road route over the player's paving is found ({route.Count} points)");
            c.Check(corner < 4f, $"it goes round the corner ({corner:0.0} m from it) instead of across the open ground");
            int off = 0;
            for (int i = 1; i < route.Count - 1; i++) if (Mathf.Min(SegDist(route[i], a, b), SegDist(route[i], b, cc)) > 3f) off++;
            c.Check(off == 0, $"every road point of it lies on the paving ({off} off)");
            var mapRoute = new List<Vector3>();
            RoadRoute.Find(g.World, start, goal, mapRoute);
            c.Note("map route points: " + mapRoute.Count);

            // ---- an AI car drives it
            var rot = Quaternion.LookRotation(dir);
            var car = g.SpawnAiVehicle("Sedan", Ground(start) + Vector3.up * 1.2f, rot);
            if (!car) { c.Block("no Sedan prefab for the AI car"); yield break; }
            if (car.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = sys.fuelCapacity; sys.oil = sys.oilCapacity; sys.coolant = sys.coolantCapacity; }
            var ai = car.gameObject.AddComponent<AiDriver>();
            ai.cruise = 12f;
            c.Fixture("an AI-driven Sedan spawned 8 m before the first leg, facing along it, full tanks");
            yield return new WaitForSeconds(1.2f);
            bool planned = ai.DriveTo(goal);
            c.Check(planned && ai.RouteUsesPlayerRoad && ai.oneWay, "DriveTo plans a one-way route over the player's road");
            var cam = Object.FindAnyObjectByType<CameraRig>();
            if (cam) cam.SetTarget(car.transform);
            float minCorner = float.MaxValue, maxOff = 0f; int samples = 0, onRoad = 0; bool shot = false;
            float t0 = Time.time;
            while (Time.time - t0 < 60f && !ai.Arrived)
            {
                yield return new WaitForSeconds(0.25f);
                if (!car) break;
                var p = car.transform.position;
                minCorner = Mathf.Min(minCorner, Flat(p, b));
                if (Flat(p, start) > Lead + 3f && Flat(p, goal) > Lead + 3f)
                {
                    samples++;
                    if (t.PlayerRoadAt(p.x, p.z, out _) || t.PaveAt(p.x, p.z) != 0) onRoad++;
                    maxOff = Mathf.Max(maxOff, Mathf.Min(SegDist(p, a, b), SegDist(p, b, cc)));
                }
                if (!shot && Flat(p, b) < 6f) { shot = true; c.Screenshot("corner"); }
            }
            float took = Time.time - t0;
            c.Metric("drive_time", took, "s");
            c.Metric("closest_to_corner", minCorner, "m");
            c.Metric("max_off_road_line", maxOff, "m");
            c.Metric("on_paving_share", samples > 0 ? onRoad / (float)samples : 0f, "");
            c.Check(car && minCorner < 5f, $"the car drives through the corner ({minCorner:0.0} m from it)");
            c.Check(samples > 4 && onRoad >= samples * 0.6f, $"mostly on the paving between the ends ({onRoad}/{samples} samples)");
            c.Check(maxOff < 6f, $"it never cuts across the open ground ({maxOff:0.0} m off the road line at most)");
            c.Check(ai.Arrived && car && Flat(car.transform.position, goal) < 6f, $"it parks at the goal ({(car ? Flat(car.transform.position, goal) : -1f):0.0} m, {took:0} s)");
            if (cam) cam.SetTarget(g.Player.transform);
        }

        static readonly Collider[] hits = new Collider[32];

        /// <summary>A level, dry site near the player, 45 m+ from every generated road point, clear of props along the L
        /// and its lead-in / lead-out.</summary>
        static bool Site(out Vector3 a, out Vector3 dir, out Vector3 side)
        {
            var g = WastelandGame.Instance; var t = DeformableTerrain.Instance;
            var focus = g.Player.transform.position;
            var dirs = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            near.Clear();
            foreach (var road in g.World.roads.roads)
                foreach (var rp in road.points) if (Flat(rp, focus) < 260f) near.Add(rp);
            for (int k = 0; k < 520; k++)
            {
                float ang = k * 2.39996f, r = 10f + k * 0.25f;
                var p = focus + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                foreach (var d in dirs)
                foreach (float turn in new[] { 1f, -1f })
                {
                    var sd = Vector3.Cross(Vector3.up, d) * turn;
                    var b = p + d * LegA; var c2 = b + sd * LegB;
                    var s0 = p - d * Lead; var e = c2 + sd * Lead;
                    if (FarFromRoads(new[] { s0, p, b, c2, e }) && Usable(t, s0, b, p) && Usable(t, b, e, p))
                    { a = new Vector3(p.x, t.Height(p.x, p.z), p.z); dir = d; side = sd; return true; }
                }
            }
            a = dir = side = default;
            return false;
        }

        static bool Usable(DeformableTerrain t, Vector3 from, Vector3 to, Vector3 origin)
        {
            float h0 = t.Height(origin.x, origin.z);
            float len = Flat(from, to);
            for (float s = 0f; s <= len; s += 2f)
            {
                var q = Vector3.Lerp(from, to, s / len);
                var sf = t.SurfaceAt(q.x, q.z);
                if (sf.road > 0.3f || sf.mud > 0.4f || t.WaterDepth(q.x, q.z) > 0f || t.HardRoadAt(q.x, q.z) || t.PaveAt(q.x, q.z) != 0) return false;
                if (Vector3.Angle(t.Normal(q.x, q.z), Vector3.up) > 7f || Mathf.Abs(t.Height(q.x, q.z) - h0) > 3f) return false;
                if (((int)s) % 4 == 0 && !Clear(t, q, 2f)) return false;
            }
            return true;
        }

        static readonly List<Vector3> near = new List<Vector3>();

        static bool FarFromRoads(Vector3[] pts)
        {
            foreach (var rp in near)
                foreach (var p in pts) if (Flat(rp, p) < 45f) return false;
            return true;
        }

        static bool Clear(DeformableTerrain t, Vector3 p, float radius)
        {
            var q = new Vector3(p.x, t.Height(p.x, p.z) + 0.4f + radius, p.z);
            int n = Physics.OverlapCapsuleNonAlloc(q, q + Vector3.up * 2f, radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.transform.name.StartsWith("Chunk") || h.GetComponentInParent<PlayerCharacter>()) continue;
                return false;
            }
            return true;
        }
    }
}
