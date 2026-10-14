using System.Collections.Generic;
using MadMax.Building;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Net
{
    /// <summary>Protocol 6: what the 2026-10-13 systems need on every peer.
    /// Moving bases: a new rig goes out as <see cref="Msg.RigSpawn"/> (vehicle id, first frame and its piece id); its other
    /// pieces travel as ordinary Placed / PlaceBroken, its parts as PartMounted / PartDetached, and every peer refreshes
    /// the rig from them (commissioning follows from the same pieces); joiners get it in <c>VehicleSave.rig</c>.
    /// Tool poses: whoever simulates a vehicle sends its machine hinges and loads (<see cref="Machine"/>), magnet crane
    /// and grapple poses as one compact blob (<see cref="Msg.ToolPose"/>, ≤ 5 Hz, only when it changed; resent to a
    /// joiner). A lifter only takes a body this peer simulates: a client's magnet or grapple asks the host for the wreck
    /// (<see cref="Msg.Carry"/>, owner = that client while held, back to the host on release). A crush cycle is announced
    /// (<see cref="Msg.Crush"/>): the other peers run the press as a show and remove the car; the bale is a world item.
    /// Taking a world thing apart (<see cref="Msg.TakenApart"/>) removes it everywhere; the kit or materials stay with
    /// the doer, learned designs stay per player.</summary>
    public partial class NetSession
    {
        const float ToolRate = 0.2f, CarryRetry = 1f;
        float toolTimer;
        readonly Dictionary<ushort, byte[]> sentTools = new Dictionary<ushort, byte[]>();
        readonly NetWriter toolBlob = new NetWriter(64);
        readonly NetReader toolReader = new NetReader();
        static readonly Dictionary<ushort, float> carryAsked = new Dictionary<ushort, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetToolStatics() => carryAsked.Clear();

        // ------------------------------------------------------------------ authority helpers (lifters)
        /// <summary>This peer runs the physics of <paramref name="v"/> (always offline).</summary>
        public static bool SimulatesHere(VehicleDriver v) { var n = Instance; return !n || n.Simulates(v); }

        /// <summary>A lifter here may take <paramref name="body"/>: something this peer simulates. A vehicle the host runs is
        /// asked for (a client), so a later try takes it; loose parts the host runs stay where they are.</summary>
        public static bool MayLift(Rigidbody body)
        {
            var n = Instance;
            if (!n || !n.Online || !body) return true;
            if (body.TryGetComponent<VehicleDriver>(out var v))
            {
                if (n.Simulates(v)) return true;
                if (n.IsClient && v.owner == 0) n.RequestCarry(v, true);
                return false;
            }
            return !(body.TryGetComponent<NetReplica>(out var rep) && rep.active);
        }

        /// <summary>A lifter here let go of <paramref name="body"/>: a wreck a client held goes back to the host.</summary>
        public static void ReleaseCarry(Rigidbody body)
        {
            var n = Instance;
            if (!n || !n.IsClient || !body || !body.TryGetComponent<VehicleDriver>(out var v)) return;
            if (v.owner == n.LocalId && (!n.game || n.game.Current != v)) n.RequestCarry(v, false);
        }

        void RequestCarry(VehicleDriver v, bool hold)
        {
            if (!IsClient || !v || v.netId == 0) return;
            if (hold && carryAsked.TryGetValue(v.netId, out var t) && Time.time - t < CarryRetry) return;
            if (hold) carryAsked[v.netId] = Time.time; else carryAsked.Remove(v.netId);
            ushort id = v.netId;
            Reliable(Msg.Carry, w => { w.UShort(id); w.Bool(hold); });
        }

        // ------------------------------------------------------------------ gameplay → network
        /// <summary>A moving base was started here (its first frame laid on stands).</summary>
        public void SendRigSpawn(VehicleDriver v, string frameId, Placeable frame)
        {
            if (!ShouldSend || !v || frame == null) return;
            ushort id = v.netId; uint fid = frame.Id; string owner = frame.owner ?? "";
            var pos = v.transform.position; var rot = v.transform.rotation;
            Reliable(Msg.RigSpawn, w => { w.UShort(id); w.String(frameId); w.Pos(pos); w.Rot(rot); w.UInt(fid); w.String(owner); });
            UpdateAuthority(v);                                                                   // a client's new rig is the host's to run
        }

        /// <summary>A crush cycle started here (the truck's simulating side): the others press the same car.</summary>
        public void SendCrush(VehicleDriver truck, VehicleDriver victim, List<Rigidbody> scraps)
        {
            if (!ShouldSend || !truck) return;
            ushort tid = truck.netId, vid = victim ? victim.netId : (ushort)0;
            var ids = new List<uint>();
            if (scraps != null) foreach (var s in scraps) if (s && s.TryGetComponent<VehiclePart>(out var p) && p.netId != 0) ids.Add(p.netId);
            Reliable(Msg.Crush, w => WriteCrush(w, tid, vid, ids));
        }

        static void WriteCrush(NetWriter w, ushort truck, ushort victim, List<uint> scraps)
        {
            w.UShort(truck); w.UShort(victim); w.Byte((byte)Mathf.Min(scraps.Count, 255));
            for (int i = 0; i < scraps.Count && i < 255; i++) w.UInt(scraps[i]);
        }

        /// <summary>A world thing was taken apart here (kind: 0 prop, 1 searched spot, 2 street lamp, 3 city fixture).</summary>
        public void SendTakenApart(byte kind, string key, string template)
        {
            if (!ShouldSend || string.IsNullOrEmpty(key)) return;
            string tid = template ?? "";
            Reliable(Msg.TakenApart, w => { w.Byte(kind); w.String(key); w.String(tid); });
        }

        // ------------------------------------------------------------------ tool poses
        /// <summary>[flags: 1 machine, 2 magnet, 4 grapple][sections] — false when the vehicle has nothing to pose.</summary>
        static bool WriteTools(NetWriter w, VehicleDriver v)
        {
            w.Reset();
            w.Byte(0);
            byte flags = 0;
            if (v.TryGetComponent<Machine>(out var m)) { flags |= 1; m.NetWrite(w); }
            if (v.TryGetComponent<MagnetCrane>(out var mc) && mc.Part) { flags |= 2; mc.NetWrite(w); }
            if (v.TryGetComponent<GrappleCrusher>(out var gc) && gc.Part) { flags |= 4; gc.NetWrite(w); }
            w.Buffer[0] = flags;
            return flags != 0;
        }

        static void ReadTools(NetReader r, VehicleDriver v)
        {
            byte flags = r.Byte();
            if ((flags & 1) != 0) { if (!v.TryGetComponent<Machine>(out var m)) return; m.NetRead(r); }
            if ((flags & 2) != 0) { if (!v.TryGetComponent<MagnetCrane>(out var mc)) return; mc.NetRead(r); }
            if ((flags & 4) != 0) { if (!v.TryGetComponent<GrappleCrusher>(out var gc)) return; gc.NetRead(r); }
        }

        /// <summary>Client: a wreck asked for but not (or no longer) on one of this peer's lifters goes back to the host.</summary>
        void SweepCarries()
        {
            if (!IsClient || carryAsked.Count == 0) return;
            List<ushort> done = null;
            foreach (var kv in carryAsked)
            {
                if (Time.time - kv.Value < 3f) continue;
                var v = Vehicle(kv.Key);
                if (v && v.owner == LocalId && (v == game.Current || HeldHere(v.Body))) continue;
                (done ??= new List<ushort>()).Add(kv.Key);
            }
            if (done == null) return;
            foreach (var id in done)
            {
                carryAsked.Remove(id);
                var v = Vehicle(id);
                if (v && v.owner == LocalId) RequestCarry(v, false);
            }
        }

        bool HeldHere(Rigidbody body)
        {
            foreach (var v in game.AllVehicles)
            {
                if (!v || v.owner != LocalId) continue;
                if (v.TryGetComponent<MagnetCrane>(out var m) && m.Holds(body)) return true;
                if (v.TryGetComponent<GrappleCrusher>(out var g) && (g.Load == body || g.Crushing)) return true;
            }
            return false;
        }

        void TickTools()
        {
            if ((toolTimer += Time.deltaTime) < ToolRate) return;
            toolTimer = 0f;
            SweepCarries();
            if (IsServer) { bool anyone = false; foreach (var p in peers) if (p.ready) { anyone = true; break; } if (!anyone) return; }
            foreach (var v in game.AllVehicles)
            {
                if (!v || v.netId == 0 || !Simulates(v)) continue;
                if (IsClient && v.owner != LocalId) continue;
                if (!WriteTools(toolBlob, v)) continue;
                int len = toolBlob.Length;
                if (sentTools.TryGetValue(v.netId, out var last) && Same(last, toolBlob.Buffer, len)) continue;
                var blob = new byte[len];
                System.Array.Copy(toolBlob.Buffer, blob, len);
                sentTools[v.netId] = blob;
                ushort id = v.netId;
                Reliable(Msg.ToolPose, w => { w.UShort(id); w.Byte((byte)blob.Length); w.Bytes(blob, 0, blob.Length); });
            }
        }

        static bool Same(byte[] a, byte[] b, int len)
        {
            if (a.Length != len) return false;
            for (int i = 0; i < len; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>A peer became ready: every tool pose goes out again.</summary>
        void ToolsPeerReady() => sentTools.Clear();

        // ------------------------------------------------------------------ network → world
        void HandleTools(Msg type, NetReader r, Peer from)
        {
            if (!game) return;
            switch (type)
            {
                case Msg.RigSpawn:
                {
                    ushort id = r.UShort(); string frameId = r.String(); var pos = r.Pos(); var rot = r.Rot(); uint fid = r.UInt(); string owner = r.String();
                    if (r.Failed) break;
                    if (!Vehicle(id))
                    {
                        Applying = true;
                        try { var rig = game.SpawnRigRemote(id, frameId, pos, rot, owner, fid); if (rig) UpdateAuthority(rig.Driver); }
                        finally { Applying = false; }
                    }
                    if (IsServer) Reliable(Msg.RigSpawn, w => { w.UShort(id); w.String(frameId); w.Pos(pos); w.Rot(rot); w.UInt(fid); w.String(owner); }, except: from);
                    break;
                }
                case Msg.ToolPose:
                {
                    ushort id = r.UShort(); int len = r.Byte(); var blob = r.Bytes(len);
                    if (r.Failed) break;
                    var v = Vehicle(id);
                    if (v && !Simulates(v))
                    {
                        toolReader.Set(blob, blob.Length);
                        Applying = true;
                        try { ReadTools(toolReader, v); }
                        finally { Applying = false; }
                    }
                    if (IsServer && from != null && (!v || v.owner == from.id)) Reliable(Msg.ToolPose, w => { w.UShort(id); w.Byte((byte)blob.Length); w.Bytes(blob, 0, blob.Length); }, except: from);
                    break;
                }
                case Msg.Crush:
                {
                    ushort tid = r.UShort(), vid = r.UShort(); int n = r.Byte();
                    var ids = new List<uint>(n);
                    for (int i = 0; i < n; i++) ids.Add(r.UInt());
                    if (r.Failed) break;
                    var truck = Vehicle(tid); var victim = Vehicle(vid);
                    var scraps = new List<Rigidbody>();
                    foreach (var pid in ids) { var lp = Loose(pid); if (lp && lp.TryGetComponent<Rigidbody>(out var lrb)) scraps.Add(lrb); }
                    Applying = true;
                    try
                    {
                        var gc = truck ? truck.GetComponent<GrappleCrusher>() : null;
                        if (gc) gc.RemoteCrush(victim, scraps);
                        else
                        {
                            if (victim) { game.ForgetVehicle(victim); Destroy(victim.gameObject); }
                            foreach (var s in scraps) if (s) Destroy(s.gameObject);
                        }
                    }
                    finally { Applying = false; }
                    if (IsServer) Reliable(Msg.Crush, w => WriteCrush(w, tid, vid, ids), except: from);
                    break;
                }
                case Msg.Carry when IsServer:
                {
                    var v = Vehicle(r.UShort()); bool hold = r.Bool();
                    if (r.Failed || !v || from == null) break;
                    if (hold && v.owner == 0 && !(Mode == NetMode.Host && game.Current == v) && !v.Occupied) SetOwner(v, from.id);
                    else if (!hold && v.owner == from.id) SetOwner(v, 0);
                    break;
                }
                case Msg.TakenApart:
                {
                    byte kind = r.Byte(); string key = r.String(), tid = r.String();
                    if (r.Failed) break;
                    Applying = true;
                    try { game.ApplyTakenApart(kind, key, tid.Length > 0 ? tid : null); }
                    finally { Applying = false; }
                    if (IsServer) Reliable(Msg.TakenApart, w => { w.Byte(kind); w.String(key); w.String(tid); }, except: from);
                    break;
                }
            }
        }
    }
}
