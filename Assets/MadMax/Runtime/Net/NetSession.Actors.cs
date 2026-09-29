using System;
using System.Collections.Generic;
using MadMax.Game;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Net
{
    /// <summary>Replication of what used to be host-only (gaps list, multiplayer): people and animals reach clients as
    /// proxies (introduced with <see cref="Msg.ActorSpawn"/> — the deterministic profile or species — then posed from
    /// snapshot entries; hits on a proxy go to the host), lightning strikes are rolled by the host near each player and
    /// sent as <see cref="Msg.Strike"/>, storms ride along with the weather, and world state that only the join snapshot
    /// carried (faction standing, market prices, fish records, aircraft found) is re-sent every 10 s. Vehicle looks
    /// (armour, tuning, paint, grime) go out from whoever simulates the vehicle when they change.</summary>
    public partial class NetSession
    {
        const float ActorRadius = 160f;
        ushort nextActor = 1;
        float worldStateTimer;
        readonly Dictionary<ushort, MadMax.Npc.Npc> proxyNpcs = new Dictionary<ushort, MadMax.Npc.Npc>();
        readonly Dictionary<ushort, MadMax.Animals.Animal> proxyAnimals = new Dictionary<ushort, MadMax.Animals.Animal>();
        readonly Dictionary<Peer, HashSet<ushort>> known = new Dictionary<Peer, HashSet<ushort>>();
        readonly HashSet<ushort> seen = new HashSet<ushort>();
        readonly Dictionary<ushort, string> sentLooks = new Dictionary<ushort, string>();

        // ------------------------------------------------------------------ server: actors in snapshots
        void ConsiderActors(Peer p, Action<long, float, Action<NetWriter>> consider)
        {
            if (!known.TryGetValue(p, out var set)) known[p] = set = new HashSet<ushort>();
            seen.Clear();
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || n.proxy) continue;
                float d = Vector3.Distance(n.transform.position, p.focus);
                if (d > ActorRadius) continue;
                if (n.netId == 0) n.netId = nextActor++;
                seen.Add(n.netId);
                if (set.Add(n.netId)) { var nn = n; Frame(p.batch, Msg.ActorSpawn, w => WriteNpcSpawn(w, nn)); }
                var npc = n;
                consider(3L << 40 | n.netId, (npc.NetSpeed > 0.1f ? 1f : 0.1f) * 30f / (30f + d), w =>
                {
                    w.Byte(3); w.UShort(npc.netId); w.Pos(npc.transform.position); w.Yaw(npc.transform.eulerAngles.y);
                    w.Byte((byte)Mathf.Clamp(Mathf.RoundToInt(npc.NetSpeed * 20f), 0, 255)); w.Byte(npc.NetFlags);
                });
            }
            foreach (var a in MadMax.Animals.Animal.All)
            {
                if (!a || a.proxy || a.Def == null) continue;
                float d = Vector3.Distance(a.transform.position, p.focus);
                if (d > ActorRadius) continue;
                if (a.netId == 0) a.netId = nextActor++;
                seen.Add(a.netId);
                if (set.Add(a.netId)) { var aa = a; Frame(p.batch, Msg.ActorSpawn, w => WriteAnimalSpawn(w, aa)); }
                var an = a;
                consider(3L << 40 | a.netId, (an.NetSpeed > 0.1f ? 1f : 0.08f) * 30f / (30f + d), w =>
                {
                    w.Byte(4); w.UShort(an.netId); w.Pos(an.transform.position); w.Yaw(an.transform.eulerAngles.y);
                    w.Byte((byte)Mathf.Clamp(Mathf.RoundToInt(an.NetSpeed * 10f), 0, 255)); w.Byte((byte)an.NetState);
                });
            }
            // out of range or gone: the client drops its proxy
            List<ushort> gone = null;
            foreach (var id in set) if (!seen.Contains(id)) (gone ??= new List<ushort>()).Add(id);
            if (gone != null) foreach (var id in gone) { set.Remove(id); ushort gid = id; Frame(p.batch, Msg.ActorGone, w => w.UShort(gid)); }
        }

        static void WriteNpcSpawn(NetWriter w, MadMax.Npc.Npc n)
        {
            var pr = n.Profile;
            w.UShort(n.netId); w.Byte(0);
            w.String(pr.id); w.Byte((byte)pr.role); w.Int(pr.seed); w.String(pr.kind ?? "");
            w.Pos(n.transform.position); w.Yaw(n.transform.eulerAngles.y); w.Byte(n.NetFlags);
        }

        static void WriteAnimalSpawn(NetWriter w, MadMax.Animals.Animal a)
        {
            w.UShort(a.netId); w.Byte(1);
            w.String(a.Def.id); w.String(a.key ?? "");
            w.Pos(a.transform.position); w.Yaw(a.transform.eulerAngles.y); w.Byte((byte)a.NetState);
        }

        // ------------------------------------------------------------------ client: proxies
        void ReadActorSpawn(NetReader r)
        {
            ushort id = r.UShort(); byte type = r.Byte();
            if (type == 0)
            {
                string pid = r.String(); var role = (MadMax.Npc.NpcRole)r.Byte(); int seed = r.Int(); string kind = r.String();
                var pos = r.Pos(); float yaw = r.Yaw(); byte flags = r.Byte();
                if (r.Failed || !game) return;
                if (proxyNpcs.TryGetValue(id, out var old) && old) Destroy(old.gameObject);
                var prof = MadMax.Npc.NpcProfile.Make(pid, role, seed, kind.Length > 0 ? kind : null);
                var n = MadMax.Npc.Npc.Spawn(prof, pos, yaw, null, game.propMaterial);
                n.proxy = true; n.netId = id;
                n.ProxyState(pos, yaw, 0f, flags);
                proxyNpcs[id] = n;
            }
            else
            {
                string def = r.String(), key = r.String();
                var pos = r.Pos(); float yaw = r.Yaw(); var st = (MadMax.Animals.Animal.State)r.Byte();
                var d = MadMax.Animals.AnimalLibrary.Get(def);
                if (r.Failed || !game || d == null) return;
                if (proxyAnimals.TryGetValue(id, out var old) && old) Destroy(old.gameObject);
                var a = MadMax.Animals.Animal.Spawn(d, pos, yaw, game.propMaterial, key.Length > 0 ? key : null);
                a.proxy = true; a.netId = id;
                a.ProxyState(pos, yaw, 0f, st);
                proxyAnimals[id] = a;
            }
        }

        void ReadActor(NetReader r, byte kind)
        {
            ushort id = r.UShort(); var pos = r.Pos(); float yaw = r.Yaw(); byte sp = r.Byte(), b = r.Byte();
            if (r.Failed) return;
            if (kind == 3) { if (proxyNpcs.TryGetValue(id, out var n) && n) n.ProxyState(pos, yaw, sp / 20f, b); }
            else if (proxyAnimals.TryGetValue(id, out var a) && a) a.ProxyState(pos, yaw, sp / 10f, (MadMax.Animals.Animal.State)b);
        }

        void ReadActorGone(NetReader r)
        {
            ushort id = r.UShort();
            if (proxyNpcs.TryGetValue(id, out var n)) { if (n) Destroy(n.gameObject); proxyNpcs.Remove(id); }
            if (proxyAnimals.TryGetValue(id, out var a)) { if (a) Destroy(a.gameObject); proxyAnimals.Remove(id); }
        }

        /// <summary>A client hit a proxy: the host applies it to the real one.</summary>
        public void SendActorHit(ushort id, Vector3 point, Vector3 dir, float power, float radius)
        {
            if (!IsClient || id == 0) return;
            Reliable(Msg.ActorHit, w => { w.UShort(id); w.Pos(point); w.Vel(dir.normalized * 100f); w.Float(power); w.Float(radius); });
        }

        void ReadActorHit(NetReader r, Peer from)
        {
            ushort id = r.UShort(); var p = r.Pos(); var d = r.Vel() / 100f; float power = r.Float(), radius = r.Float();
            if (r.Failed) return;
            var src = from != null && from.avatar ? from.avatar.gameObject : null;
            foreach (var n in MadMax.Npc.Npc.All) if (n && n.netId == id && !n.proxy) { n.ApplyHit(p, d, power, radius, src); return; }
            foreach (var a in MadMax.Animals.Animal.All) if (a && a.netId == id && !a.proxy) { a.ApplyHit(p, d, power, radius, src); return; }
        }

        // ------------------------------------------------------------------ lightning
        /// <summary>Host thunder: a strike rolled near each connected player, run here (fires, shocks) and shown there.</summary>
        public void SendStrikes()
        {
            if (!IsServer) return;
            foreach (var p in peers)
            {
                if (!p.ready || p.focus == Vector3.zero) continue;
                var ground = Storms.PickStrike(p.focus);
                Storms.Strike(ground);
                var peer = p;
                Reliable(Msg.Strike, w => w.Pos(ground), only: peer);
            }
        }

        void ReadStrike(NetReader r)
        {
            var ground = r.Pos();
            if (r.Failed) return;
            Atmosphere.LightningAt(ground);
            var cam = Camera.main;
            if (cam) MadMax.Audio.Sfx.Play("thunder", cam.transform.position + (ground - cam.transform.position).normalized * 20f, UnityEngine.Random.Range(0.5f, 0.9f), UnityEngine.Random.Range(0.85f, 1.05f), 200f, 3f);
        }

        // ------------------------------------------------------------------ world state (host → clients, every 10 s)
        void SendWorldState()
        {
            if (!game || peers.Count == 0) return;
            string fish = "";
            foreach (var kv in game.FishRecords) fish += kv.Key + "=" + kv.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ";";
            Reliable(Msg.WorldState, w =>
            {
                w.String(MadMax.Npc.Factions.Save() ?? ""); w.String(MadMax.Npc.Market.Save() ?? "");
                w.String(fish); w.String(string.Join(";", game.FoundAircraft));
            });
        }

        void ReadWorldState(NetReader r)
        {
            string factions = r.String(), market = r.String(), fish = r.String(), aircraft = r.String();
            if (r.Failed || !game) return;
            if (factions.Length > 0) MadMax.Npc.Factions.Load(factions);
            if (market.Length > 0) MadMax.Npc.Market.Load(market);
            foreach (var e in fish.Split(';'))
            {
                int i = e.IndexOf('=');
                if (i > 0 && float.TryParse(e.Substring(i + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var kg))
                    if (!game.FishRecords.TryGetValue(e.Substring(0, i), out var have) || kg > have) game.FishRecords[e.Substring(0, i)] = kg;
            }
            foreach (var k in aircraft.Split(';')) if (k.Length > 0) game.FoundAircraft.Add(k);
        }

        // ------------------------------------------------------------------ vehicle looks (armour, tuning, paint, grime)
        static string Looks(VehicleDriver v)
        {
            string arm = v.TryGetComponent<VehicleArmor>(out var a) ? a.SaveState() : "";
            string tun = v.TryGetComponent<VehicleTuning>(out var t) ? t.SaveState() : "";
            string paint = v.TryGetComponent<VehiclePaint>(out var p) ? p.SaveState() : "";
            int grime = v.TryGetComponent<VehicleGrime>(out var g) ? Mathf.RoundToInt(g.dirt * 10f) : 0;
            return arm + "\u001f" + tun + "\u001f" + paint + "\u001f" + grime;
        }

        /// <summary>Whoever simulates a vehicle sends its looks when they changed (server relays).</summary>
        void SendLooks()
        {
            if (!game || !Online) return;
            foreach (var v in game.AllVehicles)
            {
                if (!v || v.netId == 0 || !Simulates(v)) continue;
                if (IsClient && v.owner != LocalId) continue;
                var looks = Looks(v);
                if (sentLooks.TryGetValue(v.netId, out var last) && last == looks) continue;
                sentLooks[v.netId] = looks;
                var veh = v;
                Reliable(Msg.VehicleLooks, w => { w.UShort(veh.netId); w.String(looks); });
            }
        }

        void ReadVehicleLooks(NetReader r, Peer from)
        {
            ushort id = r.UShort(); string looks = r.String();
            if (r.Failed) return;
            var v = Vehicle(id);
            if (!v || (Simulates(v) && from == null)) return;
            var parts = looks.Split('\u001f');
            Applying = true;
            try
            {
                if (parts.Length > 0 && parts[0].Length > 0 && v.TryGetComponent<VehicleArmor>(out var a)) a.LoadState(parts[0]);
                if (parts.Length > 1 && parts[1].Length > 0 && v.TryGetComponent<VehicleTuning>(out var t)) t.LoadState(parts[1]);
                if (parts.Length > 2 && parts[2].Length > 0) VehiclePaint.Of(v).LoadState(parts[2]);
                if (parts.Length > 3 && int.TryParse(parts[3], out int grime) && v.TryGetComponent<VehicleGrime>(out var g)) g.dirt = grime / 10f;
            }
            finally { Applying = false; }
            sentLooks[id] = looks;
            if (IsServer) Reliable(Msg.VehicleLooks, w => { w.UShort(id); w.String(looks); }, except: from);
        }
    }
}
