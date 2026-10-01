using System.Collections.Generic;
using MadMax.Game;
using MadMax.Items;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Net
{
    /// <summary>Protocol 4: world items and work poses. The host owns every <see cref="WorldItem"/>'s existence: a drop or a
    /// PLACE anywhere goes out as <see cref="Msg.ItemSpawn"/> (idempotent by id: a known id only updates the stack count),
    /// the host sweeps up items spawned by its own code (story props) and sends the resting pose of anything that moved
    /// (<see cref="Msg.ItemMove"/>). A client's [E] hides the item and asks (<see cref="Msg.ItemTake"/>); the host removes
    /// it once, hands the stack to that picker only (<see cref="Msg.ItemGrant"/>) and tells everyone it is gone
    /// (<see cref="Msg.ItemGone"/>); a late second request gets only the "gone". Joiners get the items with their ids in
    /// the world transfer (<c>SaveData.blockItems</c>). Timed vehicle work (<c>WastelandGame.Anim</c>) sends its pose and
    /// prop at the start and a stop at the end (<see cref="Msg.WorkPose"/>) so the worker's <see cref="RemoteAvatar"/>
    /// kneels, leans in or welds on every other peer; the effect itself stays with the worker's own events.
    /// The wire helpers are static so acceptance scenarios can round-trip them without a live session.</summary>
    public partial class NetSession
    {
        const float ItemTakeTimeout = 6f;
        static readonly Dictionary<uint, (WorldItem item, float until)> pendingTakes = new Dictionary<uint, (WorldItem, float)>();
        static readonly HashSet<uint> goneItems = new HashSet<uint>();
        static readonly HashSet<uint> grantedItems = new HashSet<uint>();
        readonly HashSet<uint> announcedItems = new HashSet<uint>();
        readonly Dictionary<uint, Vector3> itemPoses = new Dictionary<uint, Vector3>();
        float itemTick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetItemStatics() { pendingTakes.Clear(); goneItems.Clear(); grantedItems.Clear(); }

        // ------------------------------------------------------------------ world items: wire format (static, testable)

        /// <summary>The world item with this id, including one hidden while its pick-up request is on the way.</summary>
        public static WorldItem FindItem(uint id)
        {
            if (id == 0) return null;
            foreach (var w in WorldItem.All) if (w && w.netId == id) return w;
            return pendingTakes.TryGetValue(id, out var p) && p.item ? p.item : null;
        }

        static VehicleDriver VehicleIn(WastelandGame g, ushort id)
        {
            if (id == 0 || !g) return null;
            foreach (var v in g.AllVehicles) if (v && v.netId == id) return v;
            return null;
        }

        /// <summary>ItemSpawn: [id][key][count][quality][placed][vel][world pos][world rot][vehicle][local pos][local rot]
        /// (the world pose is the fallback when the receiver doesn't know that vehicle).</summary>
        public static void WriteItemSpawn(NetWriter w, WorldItem it, Vector3 vel)
        {
            var drv = it.OnVehicle ? it.GetComponentInParent<VehicleDriver>() : null;
            var t = it.transform;
            w.UInt(it.netId); w.String(it.key); w.Int(it.count); w.Float(it.quality); w.Bool(it.placed); w.Vel(vel);
            w.Pos(t.position); w.Rot(t.rotation);
            w.UShort(drv ? drv.netId : (ushort)0);
            if (drv) { w.Pos(drv.transform.InverseTransformPoint(t.position)); w.Rot(Quaternion.Inverse(drv.transform.rotation) * t.rotation); }
        }

        /// <summary>Apply an ItemSpawn: a new object for an unknown id, a stack-count update for a known one, nothing for an
        /// id already taken. Returns the item (null when dropped).</summary>
        public static WorldItem ApplyItemSpawn(NetReader r, WastelandGame g, out Vector3 vel)
        {
            uint id = r.UInt(); string key = r.String(); int count = r.Int(); float q = r.Float(); bool placed = r.Bool(); vel = r.Vel();
            var pos = r.Pos(); var rot = r.Rot();
            ushort vid = r.UShort();
            Vector3 lp = Vector3.zero; Quaternion lr = Quaternion.identity;
            if (vid != 0) { lp = r.Pos(); lr = r.Rot(); }
            if (r.Failed || id == 0 || string.IsNullOrEmpty(key) || count <= 0 || !g || goneItems.Contains(id)) return null;
            var existing = FindItem(id);
            if (existing)
            {
                if (existing.count != count) { existing.count = count; existing.Refresh(); }
                return existing;
            }
            var car = VehicleIn(g, vid);
            if (car) { pos = car.transform.TransformPoint(lp); rot = car.transform.rotation * lr; }
            bool was = Applying;
            Applying = true;
            try
            {
                var w = g.SpawnWorldItem(key, count, q, pos, rot, car ? car.transform : null);
                w.netId = id; w.placed = placed;
                if (w.Body) { if (placed) w.Body.Sleep(); else w.Body.linearVelocity = vel; }
                return w;
            }
            finally { Applying = was; }
        }

        /// <summary>ItemMove: [id][pos][rot] — where the host's copy came to rest.</summary>
        public static void WriteItemMove(NetWriter w, WorldItem it) { w.UInt(it.netId); w.Pos(it.transform.position); w.Rot(it.transform.rotation); }

        public static WorldItem ApplyItemMove(NetReader r)
        {
            uint id = r.UInt(); var pos = r.Pos(); var rot = r.Rot();
            var w = r.Failed ? null : FindItem(id);
            if (!w || w.OnVehicle) return null;
            w.transform.SetPositionAndRotation(pos, rot);
            var rb = w.Body;
            rb.position = pos; rb.rotation = rot;
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.Sleep(); }
            return w;
        }

        /// <summary>Host: a pick-up request for <paramref name="id"/>. True (with the stack) only while the item is still
        /// there — it is removed at once, so a second request for it finds nothing.</summary>
        public static bool HostTakeItem(uint id, out string key, out int count, out float quality)
        {
            key = null; count = 0; quality = -1f;
            var w = FindItem(id);
            if (!w || w.count <= 0) return false;
            key = w.key; count = w.count; quality = w.quality;
            RemoveItem(w, id);
            return true;
        }

        /// <summary>ItemGrant: [id][key][count][quality] — to the one peer whose request won.</summary>
        public static void WriteItemGrant(NetWriter w, uint id, string key, int count, float quality) { w.UInt(id); w.String(key); w.Int(count); w.Float(quality); }

        /// <summary>The picker's side of a grant: the stack goes into the pack once per id (a repeat is ignored) and the
        /// hidden local copy is removed. True when the pack got it.</summary>
        public static bool ApplyItemGrant(NetReader r, WastelandGame g)
        {
            uint id = r.UInt(); string key = r.String(); int count = r.Int(); float q = r.Float();
            if (r.Failed || id == 0 || !g || string.IsNullOrEmpty(key) || count <= 0) return false;
            var w = FindItem(id);
            var at = w ? w.transform.position : g.Player ? g.Player.transform.position : Vector3.zero;
            if (w) RemoveItem(w, id); else { pendingTakes.Remove(id); goneItems.Add(id); }
            if (!grantedItems.Add(id)) return false;
            g.ReceivePickedItem(key, count, q, at);
            return true;
        }

        /// <summary>ItemGone: taken by someone (or not there any more): remove the local copy for good.</summary>
        public static void ApplyItemGone(uint id)
        {
            var w = FindItem(id);
            if (w) RemoveItem(w, id);
            else { pendingTakes.Remove(id); goneItems.Add(id); }
        }

        static void RemoveItem(WorldItem w, uint id)
        {
            pendingTakes.Remove(id);
            goneItems.Add(id);
            w.count = 0;
            w.SetNear(false);
            w.gameObject.SetActive(false);
            Object.Destroy(w.gameObject);
        }

        /// <summary>WorkPose: [player][on][pose][prop key].</summary>
        public static void WriteWorkPose(NetWriter w, ushort player, bool on, byte pose, string prop) { w.UShort(player); w.Bool(on); w.Byte(pose); w.String(prop ?? ""); }

        public static bool ReadWorkPose(NetReader r, out ushort player, out bool on, out byte pose, out string prop)
        {
            player = r.UShort(); on = r.Bool(); pose = r.Byte(); prop = r.String();
            return !r.Failed;
        }

        // ------------------------------------------------------------------ gameplay → network
        /// <summary>A drop, a PLACE or a merge into a stack here: announce it (gives it an id first).</summary>
        public void SendItemSpawn(WorldItem w, Vector3 vel)
        {
            if (!ShouldSend || !w) return;
            if (w.netId == 0) w.netId = NewEntityId();
            if (IsServer) { announcedItems.Add(w.netId); itemPoses[w.netId] = w.transform.position; }
            Reliable(Msg.ItemSpawn, wr => WriteItemSpawn(wr, w, vel));
        }

        /// <summary>The host picked an item up itself: gone for everyone.</summary>
        public void SendItemGone(uint id)
        {
            if (!ShouldSend || !IsServer || id == 0) return;
            goneItems.Add(id);
            Reliable(Msg.ItemGone, w => w.UInt(id));
        }

        /// <summary>A client's [E] on a networked item: hide it and ask the host (the pack fills on the grant).</summary>
        public bool RequestItemTake(WorldItem w)
        {
            if (!w || w.netId == 0 || !IsClient) return false;
            if (pendingTakes.ContainsKey(w.netId)) return true;
            pendingTakes[w.netId] = (w, Time.time + ItemTakeTimeout);
            w.SetNear(false);
            w.gameObject.SetActive(false);
            uint id = w.netId;
            Reliable(Msg.ItemTake, wr => wr.UInt(id));
            return true;
        }

        /// <summary>Timed vehicle work started (pose + prop key) or ended here.</summary>
        public void SendWorkPose(bool on, byte pose, string prop)
        {
            if (!Online || !worldReady || Applying || Mode == NetMode.Dedicated) return;
            ushort me = LocalId;
            Reliable(Msg.WorkPose, w => WriteWorkPose(w, me, on, pose, prop));
        }

        // ------------------------------------------------------------------ network → world
        void HandleItems(Msg type, NetReader r, Peer from)
        {
            if (!game) return;
            switch (type)
            {
                case Msg.ItemSpawn:
                {
                    var item = ApplyItemSpawn(r, game, out var vel);
                    if (item && IsServer)
                    {
                        announcedItems.Add(item.netId); itemPoses[item.netId] = item.transform.position;
                        Reliable(Msg.ItemSpawn, w => WriteItemSpawn(w, item, vel), except: from);
                    }
                    break;
                }
                case Msg.ItemMove when IsClient: ApplyItemMove(r); break;
                case Msg.ItemGone when IsClient: ApplyItemGone(r.UInt()); break;
                case Msg.ItemGrant when IsClient: ApplyItemGrant(r, game); break;
                case Msg.ItemTake when IsServer:
                {
                    uint id = r.UInt();
                    if (from == null || r.Failed) break;
                    bool won = HostTakeItem(id, out var key, out int count, out float q);
                    if (won) Frame(from.batch, Msg.ItemGrant, w => WriteItemGrant(w, id, key, count, q));
                    if (won) Reliable(Msg.ItemGone, w => w.UInt(id));                            // everyone, the picker after its grant
                    else Frame(from.batch, Msg.ItemGone, w => w.UInt(id));                        // too late: only the hidden copy goes
                    break;
                }
                case Msg.WorkPose:
                {
                    if (!ReadWorkPose(r, out ushort id, out bool on, out byte pose, out string prop)) break;
                    RemoteAvatar av;
                    if (IsServer) { if (from == null) break; id = from.id; av = from.avatar; }
                    else av = avatars.TryGetValue(id, out var a) ? a : null;
                    if (av) av.SetWork(on, pose, prop);
                    if (IsServer) Reliable(Msg.WorkPose, w => WriteWorkPose(w, id, on, pose, prop), except: from);
                    break;
                }
            }
        }

        /// <summary>Per frame: the host announces items its own code spawned and sends the resting pose of anything that
        /// moved; a client shows an item again when its pick-up request went unanswered.</summary>
        void TickItems()
        {
            if ((itemTick -= Time.deltaTime) > 0f) return;
            itemTick = 0.5f;
            if (IsClient)
            {
                if (pendingTakes.Count == 0) return;
                List<uint> expired = null;
                foreach (var kv in pendingTakes) if (Time.time > kv.Value.until) (expired ??= new List<uint>()).Add(kv.Key);
                if (expired != null)
                    foreach (var id in expired)
                    {
                        var p = pendingTakes[id];
                        pendingTakes.Remove(id);
                        if (p.item && !goneItems.Contains(id)) p.item.gameObject.SetActive(true);
                    }
                return;
            }
            bool anyone = false;
            foreach (var p in peers) if (p.ready) { anyone = true; break; }
            if (!anyone) return;
            foreach (var w in WorldItem.All)
            {
                if (!w || w.count <= 0) continue;
                if (w.netId == 0) w.netId = NewEntityId();
                uint id = w.netId;
                var pos = w.transform.position;
                if (announcedItems.Add(id))
                {
                    itemPoses[id] = pos;
                    var item = w;
                    Reliable(Msg.ItemSpawn, wr => WriteItemSpawn(wr, item, Vector3.zero));
                    continue;
                }
                if (w.OnVehicle || w.Body.isKinematic || !w.Body.IsSleeping()) continue;
                if (itemPoses.TryGetValue(id, out var last) && (last - pos).sqrMagnitude < 0.05f * 0.05f) continue;
                itemPoses[id] = pos;
                var moved = w;
                Reliable(Msg.ItemMove, wr => WriteItemMove(wr, moved));
            }
        }

        /// <summary>A peer became ready: announce every item again (idempotent), covering drops between its world
        /// snapshot and now.</summary>
        void ItemsPeerReady() => announcedItems.Clear();

        /// <summary>A joining client: the host's world items (with their ids) from the world transfer.</summary>
        void AttachItems()
        {
            if (!IsClient || JoinWorld == null || !game) return;
            game.LoadWorldItems(JoinWorld);
        }
    }
}
