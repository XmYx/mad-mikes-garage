using MadMax.Building;
using MadMax.Net;
using MadMax.Vehicles;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Game side of protocol 6 (<c>NetSession.Tools</c>): a moving base started on another peer, a world thing
    /// another peer took apart (gone here too; the kit or materials went to them), and the join rule that learned designs
    /// stay the player's own.</summary>
    public partial class WastelandGame
    {
        /// <summary>This save is the world a client is joining (host data, not the player's own).</summary>
        static bool JoiningWorld(SaveData d)
        {
            var net = NetSession.Instance;
            return d != null && net && net.IsClient && NetSession.JoinWorld == d;
        }

        /// <summary>A moving base another peer started: the same frame on stands, with the ids they gave it.</summary>
        public RigVehicle SpawnRigRemote(ushort netId, string frameId, Vector3 pos, Quaternion rot, string owner, uint frameNet)
        {
            var rig = RigVehicle.Create(frameId, pos, rot, propMaterial, owner, out var frame);
            if (!rig) return null;
            if (frame) frame.netId = frameNet;
            rig.Driver.netId = netId;
            Register(rig.Driver, null);
            return rig;
        }

        /// <summary>Tell the other peers what is being taken apart (before it goes here).</summary>
        void NoteTakenApart(SalvagePlan p)
        {
            var net = NetSession.Instance;
            if (!net || !net.Online || p == null || !p.target) return;
            switch (p.kind)
            {
                case SalvagePlan.Kind.Prop: { var dv = (DestructibleVoxels)p.target; net.SendTakenApart(0, dv.StateKey, dv.TemplateId); break; }
                case SalvagePlan.Kind.Spot: net.SendTakenApart(1, ((Lootable)p.target).key, null); break;
                case SalvagePlan.Kind.Lamp: net.SendTakenApart(2, ((WorldLamp)p.target).key, null); break;
                case SalvagePlan.Kind.City: net.SendTakenApart(3, ((CitySalvage)p.target).KeyOf(p.mark), null); break;
            }
        }

        /// <summary>Another peer took a world thing apart (kind 0 prop, 1 searched spot, 2 street lamp, 3 city fixture).</summary>
        public void ApplyTakenApart(byte kind, string key, string template)
        {
            if (string.IsNullOrEmpty(key)) return;
            switch (kind)
            {
                case 0:
                {
                    foreach (var dv in DestructibleVoxels.All)
                        if (dv && dv.StateKey == key) { RemoveProp(dv); return; }
                    var t = DeformableTerrain.Instance;                                              // not streamed in here: remembered for when it is
                    if (t) { t.DestructionState[key] = new VoxelGrid(); if (template != null) t.DestructionTemplates[key] = template; }
                    DestructibleVoxels.Moved.Remove(key);
                    break;
                }
                case 1:
                    SalvageDesigns.Removed.Add(key);
                    foreach (var l in FindObjectsByType<Lootable>(FindObjectsSortMode.None)) if (l && l.key == key && l.gameObject.name == "LootSpot") Destroy(l.gameObject);
                    break;
                case 2:
                    SalvageDesigns.Removed.Add(key);
                    foreach (var w in FindObjectsByType<WorldLamp>(FindObjectsSortMode.None)) if (w && w.key == key) Destroy(w.gameObject);
                    break;
                case 3:
                {
                    int a = key.IndexOf('/'), b = key.LastIndexOf('/');
                    if (a > 0 && b > a && int.TryParse(key.Substring(0, a), out int ci) && int.TryParse(key.Substring(b + 1), out int mark))
                    {
                        string piece = key.Substring(a + 1, b - a - 1);
                        foreach (var cs in CitySalvage.All)
                            if (cs && cs.piece == piece && CityHost.Hosts.IndexOf(cs.city) == ci) { cs.Remove(mark); return; }
                    }
                    SalvageDesigns.CityRemoved.Add(key);                                             // applied when that city is built
                    break;
                }
            }
        }
    }
}
