using System;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Game;
using MadMax.Vehicles;
using MadMax.World;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MadMax.Net
{
    public enum NetMode { Offline, Host, Client, Dedicated }

    enum Msg : byte
    {
        Hello = 1, Welcome, WorldChunk, Ready, PlayerJoined, PlayerLeft, Snapshot, OwnerState, RequestVehicle, OwnerChanged,
        Carve, Impact, PartDetached, PartMounted, PartCarried, PartDropped, LooseSpawn, Placed, PlaceBroken, Couple,
        VehicleMeta, Weather, Appearance, Salvage, Denied,
        PlaceState, Searched, VehicleSpawn, FireIgnite, Throw, Terraform,
        ActorSpawn, ActorGone, ActorHit, Strike, WorldState, VehicleLooks,
        ItemSpawn, ItemMove, ItemTake, ItemGone, ItemGrant, WorkPose,                     // protocol 4 (NetSession.Items)
        VehicleStore                                                                       // vehicle compartments (NetSession.Storage)
    }

    /// <summary>
    /// Listen-server / dedicated-server / client networking over Unity Transport (UDP).
    /// Authority: each client simulates its own character and the vehicles it drives (plus towed trailers); the server
    /// simulates everything unowned, relays owner state and gameplay events, and is the only one that saves.
    /// Bandwidth: 20 Hz snapshots per client, only entities inside the interest radius, ordered by a priority
    /// accumulator (near, moving and player entities first) under a per-packet byte budget; quantised states
    /// (~25 bytes per entity). Remote entities render ~120 ms in the past with snapshot interpolation.
    /// The world itself is deterministic from the seed; joining clients receive the save diff (gzip JSON).
    /// </summary>
    public partial class NetSession : MonoBehaviour
    {
        public static NetSession Instance { get; private set; }
        public const int ProtocolVersion = 4;
        public const ushort DefaultPort = 7777;
        public const ushort HostPlayerId = 1;

        public NetMode Mode { get; private set; }
        public bool Online => Mode != NetMode.Offline;
        public bool IsServer => Mode == NetMode.Host || Mode == NetMode.Dedicated;
        public bool IsClient => Mode == NetMode.Client;
        public ushort LocalId { get; private set; }
        public string Status { get; private set; } = "OFFLINE";
        public int PlayerCount => IsServer ? 1 + peers.FindAll(p => p.ready).Count - (Mode == NetMode.Dedicated ? 1 : 0) : avatars.Count + 1;

        [Header("Tuning")]
        public float snapshotRate = 20f;
        public float ownerRate = 30f;
        public float interestRadius = 240f;
        public float interpDelay = 0.12f;
        public int packetBudget = 1100;

        /// <summary>True while applying a remote event (gameplay hooks must not re-broadcast).</summary>
        public static bool Applying;
        /// <summary>World handed over by the server; consumed by WastelandGame.Start after the scene reload.</summary>
        public static SaveData JoinWorld;
        public static Vector3 JoinSpawn;

        public float ServerTime => IsServer ? Time.time : Time.time + serverOffset;
        public float RenderTime => ServerTime - interpDelay;

        class Peer
        {
            public NetworkConnection conn;
            public ushort id;
            public string name = "WANDERER";
            public Appearance look = new Appearance();
            public List<string> outfit = new List<string>();
            public bool ready;
            public RemoteAvatar avatar;
            public Vector3 focus;
            public readonly Dictionary<long, float> prio = new Dictionary<long, float>();
            public readonly NetWriter batch = new NetWriter(512);
            public byte[] lastState = new byte[0];
        }

        NetworkDriver driver;
        NetworkPipeline reliable;
        NetworkConnection toServer;
        readonly List<Peer> peers = new List<Peer>();
        readonly Dictionary<ushort, RemoteAvatar> avatars = new Dictionary<ushort, RemoteAvatar>();
        readonly NetWriter packet = new NetWriter(1400), serverBatch = new NetWriter(512), scratch = new NetWriter(256);
        readonly NetReader reader = new NetReader();
        byte[] rx = new byte[96 * 1024];
        float snapTimer, ownerTimer, metaTimer, serverOffset;
        bool offsetInit, worldReady;
        ushort nextPeerId = 2;
        uint nextEntity = 1;
        List<byte[]> chunks;
        int chunksExpected;
        string localName = "WANDERER";
        WastelandGame game;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Applying = false; JoinWorld = null; }

        // ------------------------------------------------------------------ lifecycle
        static NetSession Create(NetMode mode)
        {
            if (Instance) Instance.Shutdown();
            var go = new GameObject("NetSession");
            DontDestroyOnLoad(go);
            var s = go.AddComponent<NetSession>();
            s.Mode = mode;
            Instance = s;
            var settings = new NetworkSettings(Allocator.Temp);
            settings.WithFragmentationStageParameters(payloadCapacity: 64 * 1024);
            settings.WithReliableStageParameters(windowSize: 64);
            s.driver = NetworkDriver.Create(settings);
            s.reliable = s.driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            return s;
        }

        public static bool StartServer(WastelandGame game, ushort port, bool dedicated)
        {
            var s = Create(dedicated ? NetMode.Dedicated : NetMode.Host);
            if (s.driver.Bind(NetworkEndpoint.AnyIpv4.WithPort(port)) != 0 || s.driver.Listen() != 0)
            {
                Debug.LogError($"[Net] Cannot listen on port {port}");
                s.Shutdown();
                return false;
            }
            s.LocalId = dedicated ? (ushort)0 : HostPlayerId;
            s.Status = $"{(dedicated ? "DEDICATED" : "HOSTING")} :{port}";
            s.AttachGame(game);
            Debug.Log("[Net] " + s.Status);
            return true;
        }

        public static void StartClient(string address, ushort port, string name)
        {
            var s = Create(NetMode.Client);
            s.localName = string.IsNullOrWhiteSpace(name) ? "WANDERER" : name.ToUpperInvariant();
            if (!NetworkEndpoint.TryParse(address, port, out var ep)) { s.Status = "BAD ADDRESS"; s.Shutdown(); return; }
            s.toServer = s.driver.Connect(ep);
            s.Status = $"CONNECTING {address}:{port}";
        }

        public void Shutdown()
        {
            if (driver.IsCreated)
            {
                if (IsClient && toServer.IsCreated) driver.Disconnect(toServer);
                foreach (var p in peers) if (p.conn.IsCreated) driver.Disconnect(p.conn);
                driver.ScheduleUpdate().Complete();
                driver.Dispose();
            }
            foreach (var a in avatars.Values) if (a) Destroy(a.gameObject);
            foreach (var p in peers) if (p.avatar) Destroy(p.avatar.gameObject);
            peers.Clear(); avatars.Clear();
            Mode = NetMode.Offline;
            if (Instance == this) Instance = null;
            Destroy(gameObject);
        }

        void OnDestroy() { if (driver.IsCreated) driver.Dispose(); }

        /// <summary>Called by WastelandGame once its world exists (after start or scene reload).</summary>
        public void AttachGame(WastelandGame g)
        {
            game = g;
            worldReady = true;
            foreach (var v in game.AllVehicles) UpdateAuthority(v);
            if (IsClient)
            {
                Reliable(Msg.Ready, w => { });
                SendAppearance();
                Status = "CONNECTED";
            }
            if (IsServer)
                foreach (var p in peers) if (p.ready && !p.avatar) p.avatar = SpawnAvatar(p.id, p.name, p.look, p.outfit);
            AttachItems();
        }

        public uint NewEntityId() => ((uint)(LocalId + 1) << 24) | (nextEntity++ & 0xFFFFFF);

        // ------------------------------------------------------------------ authority
        public bool Simulates(VehicleDriver v)
        {
            if (!Online || !v) return true;
            return IsServer ? v.owner == 0 || v.owner == LocalId : v.owner == LocalId;
        }

        public void UpdateAuthority(VehicleDriver v)
        {
            if (!v || !Online) return;
            var rep = v.GetComponent<NetReplica>() ?? v.gameObject.AddComponent<NetReplica>();
            bool local = Simulates(v);
            if (rep.active == !local) return;
            rep.SetActive(!local);
            if (local) v.Body.WakeUp();
        }

        public VehicleDriver Vehicle(ushort id)
        {
            if (id == 0 || !game) return null;
            foreach (var v in game.AllVehicles) if (v && v.netId == id) return v;
            return null;
        }

        static VehiclePart Loose(uint id)
        {
            if (id == 0) return null;
            foreach (var p in VehiclePart.Registry) if (p && p.netId == id) return p;
            return null;
        }

        static Placeable PlaceableById(uint id) => Placeable.ById(id);

        public IEnumerable<Transform> RemoteFoci()
        {
            foreach (var p in peers) if (p.avatar) yield return p.avatar.transform;
        }

        // ------------------------------------------------------------------ transport pump
        void Update()
        {
            if (!driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();
            if (IsServer)
            {
                NetworkConnection c;
                while ((c = driver.Accept()) != default) peers.Add(new Peer { conn = c, id = nextPeerId++ });
            }
            NetworkEvent.Type ev;
            while ((ev = driver.PopEvent(out var conn, out var stream, out _)) != NetworkEvent.Type.Empty)
            {
                switch (ev)
                {
                    case NetworkEvent.Type.Connect:
                        if (IsClient) Reliable(Msg.Hello, w => { w.Int(ProtocolVersion); w.String(localName); });
                        break;
                    case NetworkEvent.Type.Data:
                        int len = stream.Length;
                        if (len > rx.Length) rx = new byte[len * 2];
                        stream.ReadBytes(new Span<byte>(rx, 0, len));
                        reader.Set(rx, len);
                        while (reader.Remaining >= 3 && !reader.Failed)
                        {
                            var type = (Msg)reader.Byte();
                            int size = reader.UShort();
                            int start = len - reader.Remaining;
                            try { Handle(conn, type); }
                            catch (Exception e) { Debug.LogWarning($"[Net] {type}: {e.Message}"); }
                            // skip to the next message whatever the handler consumed
                            int consumed = len - reader.Remaining - start;
                            if (consumed < size) reader.Bytes(size - consumed);
                        }
                        break;
                    case NetworkEvent.Type.Disconnect:
                        if (IsServer) DropPeer(conn);
                        else { Status = "DISCONNECTED"; game?.Toast("DISCONNECTED FROM SERVER"); Mode = NetMode.Offline; }
                        break;
                }
            }
            if (!worldReady || !game) { Flush(); return; }

            if (IsServer && (snapTimer += Time.deltaTime) >= 1f / snapshotRate) { SendSnapshots(snapTimer); snapTimer = 0f; }
            if (IsClient && (ownerTimer += Time.deltaTime) >= 1f / ownerRate) { ownerTimer = 0f; SendOwnerState(); }
            if ((metaTimer += Time.deltaTime) >= 1f) { metaTimer = 0f; SendMetaForDriven(); SendLooks(); }
            if (IsServer && (worldStateTimer += Time.deltaTime) >= 10f) { worldStateTimer = 0f; SendWorldState(); }
            TickItems();
            Flush();
        }

        // ------------------------------------------------------------------ framing / sending
        void Frame(NetWriter w, Msg type, Action<NetWriter> body)
        {
            w.Byte((byte)type);
            int lenPos = w.Length; w.UShort(0);
            int start = w.Length;
            body(w);
            int size = w.Length - start;
            var b = w.Buffer; b[lenPos] = (byte)size; b[lenPos + 1] = (byte)(size >> 8);
        }

        /// <summary>Queue a reliable message: to the server (client) or to every ready client (server).</summary>
        void Reliable(Msg type, Action<NetWriter> body, Peer except = null, Peer only = null)
        {
            if (IsClient) { Frame(serverBatch, type, body); return; }
            if (only != null) { Frame(only.batch, type, body); return; }
            foreach (var p in peers) if (p.ready && p != except) Frame(p.batch, type, body);
        }

        void Send(NetworkConnection c, NetworkPipeline pipe, NetWriter w)
        {
            if (w.Length == 0 || !c.IsCreated) return;
            if (driver.BeginSend(pipe, c, out var dsw) == 0)
            {
                dsw.WriteBytes(w.Span);
                driver.EndSend(dsw);
            }
        }

        void Flush()
        {
            if (IsClient) { Send(toServer, reliable, serverBatch); serverBatch.Reset(); }
            else foreach (var p in peers) { Send(p.conn, reliable, p.batch); p.batch.Reset(); }
        }

        // ------------------------------------------------------------------ server: peers, join, snapshots
        Peer PeerOf(NetworkConnection c) => peers.Find(p => p.conn == c);

        void DropPeer(NetworkConnection c)
        {
            var p = PeerOf(c);
            if (p == null) return;
            peers.Remove(p);
            known.Remove(p);
            if (p.avatar) Destroy(p.avatar.gameObject);
            if (game)
                foreach (var v in game.AllVehicles)
                    if (v && v.owner == p.id) { v.owner = 0; UpdateAuthority(v); Reliable(Msg.OwnerChanged, w => { w.UShort(v.netId); w.UShort(0); }); }
            Reliable(Msg.PlayerLeft, w => w.UShort(p.id));
            game?.Toast(p.name + " LEFT");
        }

        void SendWorld(Peer p)
        {
            var data = game.CaptureSave(forNetwork: true);
            var zipped = NetCompress.Zip(JsonUtility.ToJson(data));
            const int chunk = 30000;
            int count = (zipped.Length + chunk - 1) / chunk;
            var spawn = game.Player && game.Player.gameObject.activeInHierarchy ? game.Player.transform.position + game.Player.transform.right * 2f : game.WorldSpawn;
            Frame(p.batch, Msg.Welcome, w => { w.UShort(p.id); w.Int(game.seed); w.Pos(spawn); w.UShort((ushort)count); });
            for (int i = 0; i < count; i++)
            {
                int off = i * chunk, n = Mathf.Min(chunk, zipped.Length - off);
                int idx = i;
                Frame(p.batch, Msg.WorldChunk, w => { w.UShort((ushort)idx); w.UShort((ushort)n); w.Bytes(zipped, off, n); });
                Send(p.conn, reliable, p.batch); p.batch.Reset();
            }
        }

        RemoteAvatar SpawnAvatar(ushort id, string name, Appearance look, List<string> outfit)
        {
            var a = RemoteAvatar.Create(id, name, look, outfit, game.propMaterial);
            var rb = a.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            return a;
        }

        void SendSnapshots(float dt)
        {
            var hostPlayer = Mode == NetMode.Host && game.Player ? game.Player : null;
            foreach (var p in peers)
            {
                if (!p.ready) continue;
                var candidates = new List<(long key, float prio, Action<NetWriter> write)>();
                void Consider(long key, float weight, Action<NetWriter> write)
                {
                    p.prio.TryGetValue(key, out var pr);
                    pr += weight * dt * 20f;
                    p.prio[key] = pr;
                    candidates.Add((key, pr, write));
                }
                foreach (var v in game.AllVehicles)
                {
                    if (!v || v.owner == p.id) continue;
                    float d = Vector3.Distance(v.transform.position, p.focus);
                    if (d > interestRadius) continue;
                    bool moving = v.Body.linearVelocity.sqrMagnitude > 0.01f || v.Occupied;
                    var veh = v;
                    Consider(veh.netId, (moving ? 1f : 0.08f) * 40f / (40f + d), w => WriteVehicle(w, veh));
                }
                if (hostPlayer && Vector3.Distance(hostPlayer.transform.position, p.focus) < interestRadius)
                    Consider(1L << 40 | HostPlayerId, 3f, w => WriteLocalPlayer(w, HostPlayerId));
                foreach (var o in peers)
                {
                    if (o == p || !o.avatar || o.lastState.Length == 0 || Vector3.Distance(o.avatar.transform.position, p.focus) > interestRadius) continue;
                    var other = o;
                    Consider(1L << 40 | o.id, 3f, w => WriteAvatar(w, other));
                }
                ConsiderActors(p, Consider);
                foreach (var part in VehiclePart.Registry)
                {
                    if (!part || part.netId == 0 || part.Socket || part.transform.parent) continue;
                    if (!part.TryGetComponent<Rigidbody>(out var prb) || prb.isKinematic && prb.IsSleeping()) continue;
                    float d = Vector3.Distance(part.transform.position, p.focus);
                    if (d > 120f) continue;
                    var lp = part;
                    Consider(2L << 40 | part.netId, (prb.linearVelocity.sqrMagnitude > 0.01f ? 1f : 0.05f) * 20f / (20f + d), w => { w.Byte(2); w.UInt(lp.netId); w.Pos(lp.transform.position); w.Rot(lp.transform.rotation); });
                }
                candidates.Sort((a, b) => b.prio.CompareTo(a.prio));
                packet.Reset();
                packet.Byte((byte)Msg.Snapshot);
                int lenPos = packet.Length; packet.UShort(0);
                packet.Float(Time.time);
                int countPos = packet.Length; packet.Byte(0);
                int n = 0;
                foreach (var c in candidates)
                {
                    if (packet.Length > packetBudget || n == 255) break;
                    c.write(packet);
                    p.prio[c.key] = 0f;
                    n++;
                }
                var b = packet.Buffer;
                int size = packet.Length - lenPos - 2;
                b[lenPos] = (byte)size; b[lenPos + 1] = (byte)(size >> 8);
                b[countPos] = (byte)n;
                Send(p.conn, NetworkPipeline.Null, packet);
            }
        }

        static void WriteVehicle(NetWriter w, VehicleDriver v)
        {
            w.Byte(0); w.UShort(v.netId);
            w.Pos(v.transform.position); w.Rot(v.transform.rotation); w.Vel(v.Body.linearVelocity);
            w.SByte((sbyte)Mathf.RoundToInt(Mathf.Clamp(v.steerInput, -1f, 1f) * 127f));
            w.Byte((byte)Mathf.RoundToInt(Mathf.Clamp01(v.throttleInput) * 255f));
        }

        static void ReadVehicle(NetReader r, out ushort id, out NetReplica.State s, float time)
        {
            id = r.UShort();
            s = new NetReplica.State { time = time, pos = r.Pos(), rot = r.Rot(), vel = r.Vel(), steer = r.SByte() / 127f, throttle = r.Byte() / 255f };
        }

        // player entry: [1][id][flags][vehicle][pos][yaw][speed][vertical][look][swing][tool]
        void WriteLocalPlayer(NetWriter w, ushort id)
        {
            var pl = game.Player;
            var veh = pl.SeatedIn ? pl.SeatedIn : pl.Interior ? pl.Interior.GetComponent<VehicleDriver>() : null;
            byte flags = 0;
            if (pl.SeatedIn) flags |= 1;
            if (pl.Interior) flags |= 2;
            if (pl.Carried) flags |= 4;
            if (pl.GetComponent<CharacterController>().isGrounded || pl.SeatedIn || pl.Interior) flags |= 8;
            w.Byte(1); w.UShort(id); w.Byte(flags); w.UShort(veh ? veh.netId : (ushort)0);
            w.Pos(veh ? pl.transform.localPosition : pl.transform.position);
            w.Yaw(veh ? pl.transform.localEulerAngles.y : pl.transform.eulerAngles.y);
            var hv = new Vector2(pl.Velocity.x, pl.Velocity.z);
            w.Byte((byte)Mathf.Clamp(Mathf.RoundToInt(hv.magnitude * 20f), 0, 255));
            w.SByte((sbyte)Mathf.Clamp(Mathf.RoundToInt(pl.Velocity.y * 4f), -127, 127));
            w.SByte((sbyte)Mathf.Clamp(Mathf.RoundToInt(pl.lookPitch), -90, 90));
            w.Byte((byte)Mathf.RoundToInt(pl.SwingProgress * 255f));
            w.Byte((byte)(pl.Tool ? ToolIndex(pl.Tool.id) : 255));
        }

        static void WriteAvatar(NetWriter w, Peer p) { w.Bytes(p.lastState, 0, p.lastState.Length); }

        static RemoteAvatar.State ReadPlayer(NetReader r, out ushort id, float time)
        {
            id = r.UShort();
            byte flags = r.Byte();
            var s = new RemoteAvatar.State { time = time, seated = (flags & 1) != 0, inside = (flags & 2) != 0, carrying = (flags & 4) != 0, grounded = (flags & 8) != 0 };
            s.vehicle = r.UShort(); s.pos = r.Pos(); s.yaw = r.Yaw();
            s.speed = r.Byte() / 20f; s.vertical = r.SByte() / 4f; s.lookPitch = r.SByte();
            s.swing = r.Byte() / 255f; s.tool = r.Byte();
            return s;
        }

        // ------------------------------------------------------------------ client: owner state
        void SendOwnerState()
        {
            if (!game || !game.Player) return;
            packet.Reset();
            Frame(packet, Msg.OwnerState, w =>
            {
                WriteLocalPlayer(w, LocalId);
                int countPos = w.Length; w.Byte(0);
                byte n = 0;
                foreach (var v in game.AllVehicles)
                    if (v && v.owner == LocalId && n < 8) { WriteVehicle(w, v); n++; }
                w.Buffer[countPos] = n;
            });
            Send(toServer, NetworkPipeline.Null, packet);
        }

        // ------------------------------------------------------------------ message handling
        void Handle(NetworkConnection conn, Msg type)
        {
            var r = reader;
            var peer = IsServer ? PeerOf(conn) : null;
            switch (type)
            {
                // ---- join
                case Msg.Hello when IsServer:
                {
                    int ver = r.Int(); peer.name = r.String();
                    if (ver != ProtocolVersion) { Frame(peer.batch, Msg.Denied, w => w.String("VERSION MISMATCH")); return; }
                    SendWorld(peer);
                    break;
                }
                case Msg.Welcome when IsClient:
                    LocalId = r.UShort(); r.Int(); JoinSpawn = r.Pos(); chunksExpected = r.UShort();
                    chunks = new List<byte[]>(new byte[chunksExpected][]);
                    Status = "DOWNLOADING WORLD";
                    break;
                case Msg.WorldChunk when IsClient:
                {
                    int idx = r.UShort(), n = r.UShort();
                    chunks[idx] = r.Bytes(n);
                    if (chunks.TrueForAll(c => c != null)) LoadJoinedWorld();
                    break;
                }
                case Msg.Ready when IsServer:
                    peer.ready = true;
                    ItemsPeerReady();
                    if (game) peer.avatar = SpawnAvatar(peer.id, peer.name, peer.look, peer.outfit);
                    // introduce everyone to the newcomer, and the newcomer to everyone
                    if (Mode == NetMode.Host && game.Player)
                        Frame(peer.batch, Msg.PlayerJoined, w => { w.UShort(HostPlayerId); w.String("HOST"); WriteLook(w, game.Player.Rig.appearance, game.Player.Rig.outfit); });
                    foreach (var o in peers)
                        if (o != peer && o.ready) { var oo = o; Frame(peer.batch, Msg.PlayerJoined, w => { w.UShort(oo.id); w.String(oo.name); WriteLook(w, oo.look, oo.outfit); }); }
                    Reliable(Msg.PlayerJoined, w => { w.UShort(peer.id); w.String(peer.name); WriteLook(w, peer.look, peer.outfit); }, except: peer);
                    Reliable(Msg.Weather, w => { w.Bool(Weather.Raining); w.Float(Weather.Wetness); w.Float(Weather.Snow); w.Float(Weather.BaseTemperature); w.Float(Weather.LakeRise); w.Float(DayNight.Hours); }, only: peer);
                    game.Toast(peer.name + " JOINED");
                    break;
                case Msg.Denied:
                    Status = "DENIED: " + r.String();
                    break;
                case Msg.PlayerJoined when IsClient:
                {
                    ushort id = r.UShort(); string name = r.String(); ReadLook(r, out var look, out var outfit);
                    if (id == LocalId || avatars.ContainsKey(id)) break;
                    avatars[id] = SpawnAvatar(id, name, look, outfit);
                    game?.Toast(name + " IS HERE");
                    break;
                }
                case Msg.PlayerLeft when IsClient:
                {
                    ushort id = r.UShort();
                    if (avatars.TryGetValue(id, out var a)) { if (a) Destroy(a.gameObject); avatars.Remove(id); }
                    break;
                }
                case Msg.Appearance:
                {
                    ushort id = r.UShort(); ReadLook(r, out var look, out var outfit);
                    if (IsServer && peer != null) { peer.look = look; peer.outfit = outfit; id = peer.id; }
                    var av = IsServer ? peer?.avatar : avatars.TryGetValue(id, out var a2) ? a2 : null;
                    if (av) av.SetLook(look, outfit);
                    if (IsServer) Reliable(Msg.Appearance, w => { w.UShort(id); WriteLook(w, look, outfit); }, except: peer);
                    break;
                }

                // ---- state streams
                case Msg.Snapshot when IsClient:
                {
                    float st = r.Float();
                    float off = st - Time.time;
                    serverOffset = offsetInit ? Mathf.Lerp(serverOffset, off, 0.05f) : off;
                    offsetInit = true;
                    int count = r.Byte();
                    for (int i = 0; i < count && !r.Failed; i++)
                    {
                        byte kind = r.Byte();
                        if (kind == 0)
                        {
                            ReadVehicle(r, out var id, out var s, st);
                            var v = Vehicle(id);
                            if (v && !Simulates(v)) v.GetComponent<NetReplica>()?.Push(s);
                        }
                        else if (kind == 1)
                        {
                            var s = ReadPlayer(r, out var id, st);
                            if (avatars.TryGetValue(id, out var av) && av) av.Push(s);
                        }
                        else if (kind == 3 || kind == 4) ReadActor(r, kind);
                        else
                        {
                            uint id = r.UInt(); var pos = r.Pos(); var rot = r.Rot();
                            var part = Loose(id);
                            if (part && !part.transform.parent)
                            {
                                var rep = part.GetComponent<NetReplica>() ?? part.gameObject.AddComponent<NetReplica>();
                                if (!rep.active) rep.SetActive(true);
                                rep.Push(new NetReplica.State { time = st, pos = pos, rot = rot });
                            }
                        }
                    }
                    break;
                }
                case Msg.OwnerState when IsServer:
                {
                    int start = reader.Remaining;
                    r.Byte(); // kind 1
                    var s = ReadPlayer(r, out _, Time.time);
                    s.time = Time.time;
                    // keep the raw player entry to relay it in snapshots with the right id
                    var pw = new NetWriter(32);
                    pw.Byte(1); pw.UShort(peer.id); byte flags = (byte)((s.seated ? 1 : 0) | (s.inside ? 2 : 0) | (s.carrying ? 4 : 0) | (s.grounded ? 8 : 0));
                    pw.Byte(flags); pw.UShort(s.vehicle); pw.Pos(s.pos); pw.Yaw(s.yaw);
                    pw.Byte((byte)Mathf.Clamp(Mathf.RoundToInt(s.speed * 20f), 0, 255)); pw.SByte((sbyte)Mathf.RoundToInt(s.vertical * 4f)); pw.SByte((sbyte)s.lookPitch);
                    pw.Byte((byte)Mathf.RoundToInt(s.swing * 255f)); pw.Byte(s.tool);
                    peer.lastState = new byte[pw.Length]; Array.Copy(pw.Buffer, peer.lastState, pw.Length);
                    if (peer.avatar)
                    {
                        peer.avatar.Push(s);
                        var veh = Vehicle(s.vehicle);
                        peer.focus = veh && (s.seated || s.inside) ? veh.transform.TransformPoint(s.pos) : s.pos;
                    }
                    int count = r.Byte();
                    for (int i = 0; i < count && !r.Failed; i++)
                    {
                        r.Byte();
                        ReadVehicle(r, out var id, out var vs, Time.time);
                        var v = Vehicle(id);
                        if (v && v.owner == peer.id) { v.GetComponent<NetReplica>()?.Push(vs); v.steerInput = vs.steer; v.throttleInput = vs.throttle; }
                    }
                    break;
                }

                // ---- ownership
                case Msg.RequestVehicle when IsServer:
                {
                    var v = Vehicle(r.UShort()); bool enter = r.Bool();
                    if (!v) break;
                    if (enter)
                    {
                        bool free = v.owner == 0 && !(Mode == NetMode.Host && game.Current == v);
                        if (!free) { Frame(peer.batch, Msg.Denied, w => w.String("VEHICLE OCCUPIED")); break; }
                        SetOwner(v, peer.id);
                    }
                    else if (v.owner == peer.id) SetOwner(v, 0);
                    break;
                }
                case Msg.OwnerChanged when IsClient:
                {
                    var v = Vehicle(r.UShort()); ushort owner = r.UShort();
                    if (!v) break;
                    bool wasMine = v.owner == LocalId;
                    v.owner = owner;
                    UpdateAuthority(v);
                    if (owner == LocalId && !wasMine && pendingEnter == v) { pendingEnter = null; game.EnterGranted(v); }
                    break;
                }

                // ---- world events (applied locally, relayed by the server)
                case Msg.ActorSpawn when IsClient: ReadActorSpawn(r); break;
                case Msg.ActorGone when IsClient: ReadActorGone(r); break;
                case Msg.ActorHit when IsServer: ReadActorHit(r, peer); break;
                case Msg.Strike when IsClient: ReadStrike(r); break;
                case Msg.WorldState when IsClient: ReadWorldState(r); break;
                case Msg.VehicleLooks: ReadVehicleLooks(r, peer); break;
                case Msg.VehicleStore: ReadVehicleStore(r, peer); break;
                case Msg.ItemSpawn:
                case Msg.ItemMove:
                case Msg.ItemTake:
                case Msg.ItemGone:
                case Msg.ItemGrant:
                case Msg.WorkPose:
                    HandleItems(type, r, peer);
                    break;
                default:
                    ApplyEvent(type, r, peer);
                    break;
            }
        }

        VehicleDriver pendingEnter;

        void SetOwner(VehicleDriver v, ushort owner)
        {
            v.owner = owner;
            UpdateAuthority(v);
            // towed trailers follow their tow vehicle's owner
            foreach (var t in game.Trailers)
            {
                var tc = t ? t.GetComponent<TowCoupling>() : null;
                if (tc && tc.Tower == v) { t.owner = owner; UpdateAuthority(t); var tt = t; Reliable(Msg.OwnerChanged, w => { w.UShort(tt.netId); w.UShort(owner); }); }
            }
            Reliable(Msg.OwnerChanged, w => { w.UShort(v.netId); w.UShort(owner); });
        }

        void LoadJoinedWorld()
        {
            var bytes = new List<byte>();
            foreach (var c in chunks) bytes.AddRange(c);
            JoinWorld = JsonUtility.FromJson<SaveData>(NetCompress.Unzip(bytes.ToArray()));
            chunks = null;
            worldReady = false;
            Status = "LOADING WORLD";
            SaveSystem.Pending = JoinWorld;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        static void WriteLook(NetWriter w, Appearance a, List<string> outfit)
        {
            w.String(JsonUtility.ToJson(a ?? new Appearance()));
            w.Byte((byte)(outfit?.Count ?? 0));
            if (outfit != null) foreach (var o in outfit) w.String(o);
        }

        static void ReadLook(NetReader r, out Appearance a, out List<string> outfit)
        {
            a = JsonUtility.FromJson<Appearance>(r.String()) ?? new Appearance();
            int n = r.Byte();
            outfit = new List<string>();
            for (int i = 0; i < n; i++) outfit.Add(r.String());
        }

        // ------------------------------------------------------------------ gameplay → network (called by game code)
        bool ShouldSend => Online && !Applying && worldReady;

        public void RequestVehicle(VehicleDriver v, bool enter)
        {
            if (IsServer) { if (enter && v.owner == 0) SetOwner(v, LocalId); else if (!enter && v.owner == LocalId) SetOwner(v, 0); return; }
            if (enter) pendingEnter = v;
            Reliable(Msg.RequestVehicle, w => { w.UShort(v.netId); w.Bool(enter); });
        }

        public void SendCarve(string key, Vector3 p, Vector3 d, float radius, float power)
        {
            if (!ShouldSend || key == null) return;
            Reliable(Msg.Carve, w => { w.String(key); w.Pos(p); w.Vel(d * 100f); w.Float(radius); w.Float(power); });
        }

        public void SendImpact(VehicleDriver v, Vector3 point, Vector3 dir, float depth, float radius)
        {
            if (!ShouldSend || !v) return;
            Reliable(Msg.Impact, w => { w.UShort(v.netId); w.Pos(v.transform.InverseTransformPoint(point)); w.Vel(v.transform.InverseTransformDirection(dir) * 100f); w.Float(depth); w.Float(radius); });
        }

        public void SendSalvage(VehicleDriver v, Vector3 point, float amount)
        {
            if (!ShouldSend || !v) return;
            Reliable(Msg.Salvage, w => { w.UShort(v.netId); w.Pos(point); w.Float(amount); });
        }

        public void SendPartDetached(VehicleDriver v, string socket, VehiclePart part, ushort carrier)
        {
            if (!ShouldSend || !v || !part) return;
            if (part.netId == 0) part.netId = NewEntityId();
            Reliable(Msg.PartDetached, w => { w.UShort(v.netId); w.String(socket); w.UInt(part.netId); w.String(part.partId); w.Float(part.damage); w.UShort(carrier); w.Pos(part.transform.position); w.Rot(part.transform.rotation); });
        }

        public void SendPartMounted(VehicleDriver v, string socket, VehiclePart part)
        {
            if (!ShouldSend || !v || !part) return;
            Reliable(Msg.PartMounted, w => { w.UShort(v.netId); w.String(socket); w.UInt(part.netId); w.String(part.partId); w.Float(part.damage); });
        }

        public void SendPartCarried(VehiclePart part)
        {
            if (!ShouldSend || !part) return;
            if (part.netId == 0) part.netId = NewEntityId();
            Reliable(Msg.PartCarried, w => { w.UInt(part.netId); w.UShort(LocalId); });
        }

        public void SendPartDropped(VehiclePart part, Vector3 velocity)
        {
            if (!ShouldSend || !part) return;
            Reliable(Msg.PartDropped, w => { w.UInt(part.netId); w.Pos(part.transform.position); w.Rot(part.transform.rotation); w.Vel(velocity); });
        }

        public void SendLooseSpawn(VehiclePart part)
        {
            if (!ShouldSend || !part) return;
            if (part.netId == 0) part.netId = NewEntityId();
            Reliable(Msg.LooseSpawn, w => { w.UInt(part.netId); w.String(part.partId); w.Pos(part.transform.position); w.Rot(part.transform.rotation); w.Float(part.damage); });
        }

        public void SendPlaced(Placeable p)
        {
            if (!ShouldSend || !p) return;
            if (p.netId == 0) p.netId = NewEntityId();
            var chassis = p.GetComponentInParent<VehicleChassis>();
            var v = chassis ? chassis.GetComponent<VehicleDriver>() : null;
            var parent = v ? v.transform : p.transform.parent;
            Reliable(Msg.Placed, w => { w.UInt(p.netId); w.String(p.id); w.UShort(v ? v.netId : (ushort)0); w.Pos(parent.InverseTransformPoint(p.transform.position)); w.Rot(Quaternion.Inverse(parent.rotation) * p.transform.rotation); });
        }

        public void SendPlaceBroken(Placeable p)
        {
            if (!ShouldSend || !p || p.netId == 0) return;
            Reliable(Msg.PlaceBroken, w => w.UInt(p.netId));
        }

        public void SendCouple(VehicleDriver trailer, VehicleDriver tower)
        {
            if (!ShouldSend || !trailer) return;
            Reliable(Msg.Couple, w => { w.UShort(trailer.netId); w.UShort(tower ? tower.netId : (ushort)0); });
            if (IsServer) { trailer.owner = tower ? tower.owner : (ushort)0; UpdateAuthority(trailer); var tr = trailer; Reliable(Msg.OwnerChanged, w => { w.UShort(tr.netId); w.UShort(tr.owner); }); }
        }

        public void SendVehicleMeta(VehicleDriver v)
        {
            if (!ShouldSend || !v) return;
            var sys = v.GetComponent<VehicleSystems>();
            var dmg = v.GetComponent<VehicleDamage>();
            var sockets = v.GetComponent<VehicleChassis>().Sockets;
            Reliable(Msg.VehicleMeta, w =>
            {
                w.UShort(v.netId);
                w.Float(sys ? sys.fuel : 0f); w.Float(sys ? sys.oil : 0f); w.Float(sys ? sys.coolant : 0f);
                w.Float(dmg ? dmg.FrameDamage : 0f); w.Bool(v.FourWheelDrive); w.Bool(v.diffLocked);
                w.Byte((byte)sockets.Length);
                foreach (var s in sockets) w.Byte((byte)Mathf.RoundToInt(Mathf.Clamp01(s.Current ? s.Current.damage : 0f) * 255f));
            });
        }

        public void SendPlaceState(Placeable p)
        {
            if (!ShouldSend || !p) return;
            var id = p.Id; var state = p.SaveState() ?? ""; var owner = p.owner ?? "";
            Reliable(Msg.PlaceState, w => { w.UInt(id); w.String(state); w.String(owner); });
        }

        public void SendSearched(string key)
        {
            if (!ShouldSend || key == null) return;
            Reliable(Msg.Searched, w => w.String(key));
        }

        public void SendVehicleSpawn(VehicleDriver v, string design)
        {
            if (!ShouldSend || !v) return;
            Reliable(Msg.VehicleSpawn, w => { w.UShort(v.netId); w.String(design); w.Pos(v.transform.position); w.Rot(v.transform.rotation); });
        }

        /// <summary>Fire started by the authority (server) — clients mirror it (visual + local damage to themselves).</summary>
        public void SendFire(Vector3 p, float fuel, float intensity, bool ground)
        {
            if (!ShouldSend) return;
            Reliable(Msg.FireIgnite, w => { w.Pos(p); w.Float(fuel); w.Float(intensity); w.Bool(ground); });
        }

        public void SendThrow(string item, Vector3 p, Vector3 v)
        {
            if (!ShouldSend) return;
            Reliable(Msg.Throw, w => { w.String(item); w.Pos(p); w.Float(v.x); w.Float(v.y); w.Float(v.z); });
        }

        public void SendTerraform(byte op, Vector3 p, float radius, float amount, byte extra)
        {
            if (!ShouldSend) return;
            Reliable(Msg.Terraform, w => { w.Byte(op); w.Pos(p); w.Float(radius); w.Float(amount); w.Byte(extra); });
        }

        public void SendWeather()
        {
            if (!ShouldSend || !IsServer) return;
            int storm = 0; float left = 0f, total = 0f;
            if (Storms.Instance) Storms.Instance.SaveState(out storm, out left, out total);
            Reliable(Msg.Weather, w => { w.Bool(Weather.Raining); w.Float(Weather.Wetness); w.Float(Weather.Snow); w.Float(Weather.BaseTemperature); w.Float(Weather.LakeRise); w.Float(DayNight.Hours); w.Int(DayNight.Day); w.Byte((byte)storm); w.Float(left); w.Float(total); });
        }

        public void SendAppearance()
        {
            if (!Online || !game || !game.Player) return;
            var rig = game.Player.Rig;
            Reliable(Msg.Appearance, w => { w.UShort(LocalId); WriteLook(w, rig.appearance, rig.outfit); });
        }

        void SendMetaForDriven()
        {
            foreach (var v in game.AllVehicles)
                if (v && v.Occupied && Simulates(v) && v.owner == LocalId) SendVehicleMeta(v);
        }

        // ------------------------------------------------------------------ network → world
        void ApplyEvent(Msg type, NetReader r, Peer from)
        {
            if (!game) return;
            var relay = new NetWriter(64);
            int startRemaining = r.Remaining;
            Applying = true;
            try
            {
                switch (type)
                {
                    case Msg.Carve:
                    {
                        string key = r.String(); var p = r.Pos(); var d = r.Vel() / 100f; float rad = r.Float(), pow = r.Float();
                        DestructibleVoxels.ApplyRemoteCarve(key, p, d, rad, pow);
                        if (IsServer) Reliable(Msg.Carve, w => { w.String(key); w.Pos(p); w.Vel(d * 100f); w.Float(rad); w.Float(pow); }, except: from);
                        break;
                    }
                    case Msg.Impact:
                    {
                        ushort id = r.UShort(); var lp = r.Pos(); var ld = r.Vel() / 100f; float depth = r.Float(), rad = r.Float();
                        var v = Vehicle(id);
                        if (v && v.TryGetComponent<VehicleDamage>(out var dmg)) dmg.DentAt(v.transform.TransformPoint(lp), v.transform.TransformDirection(ld), depth, rad);
                        if (IsServer) Reliable(Msg.Impact, w => { w.UShort(id); w.Pos(lp); w.Vel(ld * 100f); w.Float(depth); w.Float(rad); }, except: from);
                        break;
                    }
                    case Msg.Salvage:
                    {
                        ushort id = r.UShort(); var p = r.Pos(); float amount = r.Float();
                        var v = Vehicle(id);
                        if (v && v.TryGetComponent<VehicleDamage>(out var dmg)) dmg.Salvage(p, amount, null);
                        if (IsServer) Reliable(Msg.Salvage, w => { w.UShort(id); w.Pos(p); w.Float(amount); }, except: from);
                        break;
                    }
                    case Msg.PartDetached:
                    {
                        ushort vid = r.UShort(); string socket = r.String(); uint pid = r.UInt(); string partId = r.String(); float dmg = r.Float(); ushort carrier = r.UShort();
                        var pos = r.Pos(); var rot = r.Rot();
                        var v = Vehicle(vid);
                        var s = v ? v.GetComponent<VehicleChassis>().FindSocket(socket) : null;
                        var part = s && s.Current ? s.Detach(IsServer && carrier == 0) : game.SpawnPart(partId, pos, rot);
                        if (part)
                        {
                            part.netId = pid; part.damage = dmg;
                            part.transform.SetPositionAndRotation(pos, rot);
                            PlaceLoose(part, carrier, Vector3.zero);
                        }
                        if (IsServer) Reliable(Msg.PartDetached, w => { w.UShort(vid); w.String(socket); w.UInt(pid); w.String(partId); w.Float(dmg); w.UShort(carrier); w.Pos(pos); w.Rot(rot); }, except: from);
                        break;
                    }
                    case Msg.PartMounted:
                    {
                        ushort vid = r.UShort(); string socket = r.String(); uint pid = r.UInt(); string partId = r.String(); float dmg = r.Float();
                        var v = Vehicle(vid);
                        var s = v ? v.GetComponent<VehicleChassis>().FindSocket(socket) : null;
                        var part = Loose(pid) ?? game.SpawnPart(partId, Vector3.zero, Quaternion.identity);
                        if (s && part)
                        {
                            if (part.TryGetComponent<NetReplica>(out var rep)) Destroy(rep);
                            part.transform.SetParent(null, true);
                            if (s.Current) { var old = s.Detach(false); if (old && old != part) Destroy(old.gameObject); }
                            s.Attach(part);
                            part.netId = 0; part.damage = dmg;
                        }
                        if (IsServer) Reliable(Msg.PartMounted, w => { w.UShort(vid); w.String(socket); w.UInt(pid); w.String(partId); w.Float(dmg); }, except: from);
                        break;
                    }
                    case Msg.PartCarried:
                    {
                        uint pid = r.UInt(); ushort carrier = r.UShort();
                        var part = Loose(pid);
                        if (part) PlaceLoose(part, carrier, Vector3.zero);
                        if (IsServer) Reliable(Msg.PartCarried, w => { w.UInt(pid); w.UShort(carrier); }, except: from);
                        break;
                    }
                    case Msg.PartDropped:
                    {
                        uint pid = r.UInt(); var pos = r.Pos(); var rot = r.Rot(); var vel = r.Vel();
                        var part = Loose(pid);
                        if (part) { part.transform.SetParent(null, true); part.transform.SetPositionAndRotation(pos, rot); PlaceLoose(part, 0, vel); }
                        if (IsServer) Reliable(Msg.PartDropped, w => { w.UInt(pid); w.Pos(pos); w.Rot(rot); w.Vel(vel); }, except: from);
                        break;
                    }
                    case Msg.LooseSpawn:
                    {
                        uint pid = r.UInt(); string partId = r.String(); var pos = r.Pos(); var rot = r.Rot(); float dmg = r.Float();
                        if (!Loose(pid))
                        {
                            var part = game.SpawnPart(partId, pos, rot);
                            if (part) { part.netId = pid; part.damage = dmg; PlaceLoose(part, 0, Vector3.zero); }
                        }
                        if (IsServer) Reliable(Msg.LooseSpawn, w => { w.UInt(pid); w.String(partId); w.Pos(pos); w.Rot(rot); w.Float(dmg); }, except: from);
                        break;
                    }
                    case Msg.Placed:
                    {
                        uint nid = r.UInt(); string id = r.String(); ushort vid = r.UShort(); var lp = r.Pos(); var lr = r.Rot();
                        var v = Vehicle(vid);
                        var parent = v ? v.transform : game.Build.Structures;
                        var placed = FurnitureLibrary.Spawn(id, parent, lp, lr, game.propMaterial);
                        if (placed) placed.netId = nid;
                        if (IsServer) Reliable(Msg.Placed, w => { w.UInt(nid); w.String(id); w.UShort(vid); w.Pos(lp); w.Rot(lr); }, except: from);
                        break;
                    }
                    case Msg.PlaceState:
                    {
                        uint nid = r.UInt(); string state = r.String(), owner = r.String();
                        var p = PlaceableById(nid);
                        if (p) { p.LoadState(state); p.owner = owner; }
                        if (IsServer) Reliable(Msg.PlaceState, w => { w.UInt(nid); w.String(state); w.String(owner); }, except: from);
                        break;
                    }
                    case Msg.Searched:
                    {
                        string key = r.String();
                        Lootable.Searched.Add(key);
                        if (IsServer) Reliable(Msg.Searched, w => w.String(key), except: from);
                        break;
                    }
                    case Msg.VehicleSpawn:
                    {
                        ushort vid = r.UShort(); string design = r.String(); var p = r.Pos(); var rot = r.Rot();
                        if (!Vehicle(vid)) game.SpawnVehicleRemote(design, vid, p, rot);
                        if (IsServer) Reliable(Msg.VehicleSpawn, w => { w.UShort(vid); w.String(design); w.Pos(p); w.Rot(rot); }, except: from);
                        break;
                    }
                    case Msg.FireIgnite:
                    {
                        var p = r.Pos(); float fuel = r.Float(), inten = r.Float(); bool ground = r.Bool();
                        Fire.Ignite(p, null, fuel, inten, ground);
                        if (IsServer) Reliable(Msg.FireIgnite, w => { w.Pos(p); w.Float(fuel); w.Float(inten); w.Bool(ground); }, except: from);
                        break;
                    }
                    case Msg.Throw:
                    {
                        string item = r.String(); var p = r.Pos(); var v = new Vector3(r.Float(), r.Float(), r.Float());
                        game.SpawnThrown(item, p, v, IsServer);
                        if (IsServer) Reliable(Msg.Throw, w => { w.String(item); w.Pos(p); w.Float(v.x); w.Float(v.y); w.Float(v.z); }, except: from);
                        break;
                    }
                    case Msg.Terraform:
                    {
                        byte op = r.Byte(); var p = r.Pos(); float rad = r.Float(), amt = r.Float(); byte extra = r.Byte();
                        DeformableTerrain.Instance?.ApplyTerraform(op, p, rad, amt, extra);
                        if (IsServer) Reliable(Msg.Terraform, w => { w.Byte(op); w.Pos(p); w.Float(rad); w.Float(amt); w.Byte(extra); }, except: from);
                        break;
                    }
                    case Msg.PlaceBroken:
                    {
                        uint nid = r.UInt();
                        var p = PlaceableById(nid);
                        if (p) Destroy(p.gameObject);
                        if (IsServer) Reliable(Msg.PlaceBroken, w => w.UInt(nid), except: from);
                        break;
                    }
                    case Msg.Couple:
                    {
                        ushort tid = r.UShort(), towId = r.UShort();
                        var t = Vehicle(tid); var tower = Vehicle(towId);
                        var tc = t ? t.GetComponent<TowCoupling>() : null;
                        if (tc) { if (tower) tc.Couple(tower); else tc.Uncouple(); }
                        if (IsServer && t)
                        {
                            Reliable(Msg.Couple, w => { w.UShort(tid); w.UShort(towId); }, except: from);
                            t.owner = tower ? tower.owner : (ushort)0; UpdateAuthority(t);
                            Reliable(Msg.OwnerChanged, w => { w.UShort(tid); w.UShort(t.owner); });
                        }
                        break;
                    }
                    case Msg.VehicleMeta:
                    {
                        ushort id = r.UShort(); float fuel = r.Float(), oil = r.Float(), coolant = r.Float(), frame = r.Float(); bool awd = r.Bool(), locked = r.Bool();
                        int n = r.Byte(); var dmgs = new float[n];
                        for (int i = 0; i < n; i++) dmgs[i] = r.Byte() / 255f;
                        var v = Vehicle(id);
                        if (v && !Simulates(v) || v && from != null)
                        {
                            if (v.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = fuel; sys.oil = oil; sys.coolant = coolant; }
                            if (v.TryGetComponent<VehicleDamage>(out var dmg) && Mathf.Abs(dmg.FrameDamage - frame) > 0.01f) dmg.AddFrameDamage(frame - dmg.FrameDamage, 1f);
                            if (v.FourWheelDrive != awd) v.ToggleFourWheelDrive();
                            v.diffLocked = locked;
                            var sockets = v.GetComponent<VehicleChassis>().Sockets;
                            for (int i = 0; i < n && i < sockets.Length; i++) if (sockets[i].Current) sockets[i].Current.damage = dmgs[i];
                        }
                        if (IsServer && v) SendVehicleMetaExcept(v, from);
                        break;
                    }
                    case Msg.Weather:
                    {
                        bool rain = r.Bool(); float wet = r.Float(), snow = r.Float(), temp = r.Float(), rise = r.Float(), hours = r.Float();
                        int day = r.Int();
                        int storm = r.Byte(); float left = r.Float(), total = r.Float();
                        if (IsClient) { Weather.Restore(rain, wet, snow, temp, rise); DayNight.SetHours(hours); DayNight.SetDay(day); if (Storms.Instance && !r.Failed) Storms.Instance.Restore(storm, left, total); }
                        break;
                    }
                }
            }
            finally { Applying = false; }
        }

        void SendVehicleMetaExcept(VehicleDriver v, Peer except)
        {
            var sys = v.GetComponent<VehicleSystems>();
            var dmg = v.GetComponent<VehicleDamage>();
            var sockets = v.GetComponent<VehicleChassis>().Sockets;
            Reliable(Msg.VehicleMeta, w =>
            {
                w.UShort(v.netId);
                w.Float(sys ? sys.fuel : 0f); w.Float(sys ? sys.oil : 0f); w.Float(sys ? sys.coolant : 0f);
                w.Float(dmg ? dmg.FrameDamage : 0f); w.Bool(v.FourWheelDrive); w.Bool(v.diffLocked);
                w.Byte((byte)sockets.Length);
                foreach (var s in sockets) w.Byte((byte)Mathf.RoundToInt(Mathf.Clamp01(s.Current ? s.Current.damage : 0f) * 255f));
            }, except: except);
        }

        /// <summary>Put a loose part in the right state: carried by a player, simulated here (server) or interpolated (client).</summary>
        void PlaceLoose(VehiclePart part, ushort carrier, Vector3 velocity)
        {
            foreach (var c in part.GetComponentsInChildren<Collider>()) c.enabled = carrier == 0;
            var rb = part.GetComponent<Rigidbody>() ?? part.gameObject.AddComponent<Rigidbody>();
            rb.mass = part.mass;
            var rep = part.GetComponent<NetReplica>();
            if (carrier != 0)
            {
                Transform hands = null;
                if (IsServer) { var p = peers.Find(x => x.id == carrier); hands = p?.avatar ? p.avatar.CarryPoint : null; }
                else if (avatars.TryGetValue(carrier, out var a) && a) hands = a.CarryPoint;
                if (rep) rep.SetActive(false);
                rb.isKinematic = true;
                if (hands) { part.transform.SetParent(hands, false); part.transform.localPosition = Vector3.zero; part.transform.localRotation = Quaternion.identity; }
                return;
            }
            if (IsServer) { if (rep) Destroy(rep); rb.isKinematic = false; rb.linearVelocity = velocity; }
            else { rep = rep ?? part.gameObject.AddComponent<NetReplica>(); rep.SetActive(true); }
        }
    
        static int ToolIndex(string id) { var ids = ToolLibrary.AllIds; for (int i = 0; i < ids.Count; i++) if (ids[i] == id) return i; return 255; }
    }
}
