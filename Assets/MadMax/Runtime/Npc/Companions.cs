using System;
using System.Collections.Generic;
using MadMax.Game;
using MadMax.RPG;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>A companion as saved: who they are (the profile is rebuilt from id/role/seed/kind), their order,
    /// where they stand, their pack and the fleet vehicle they drive.</summary>
    [Serializable]
    public class CompanionSave
    {
        public string id, kind, pack;
        public int role, seed, order, vehicle = -1;
        public Vector3 position, post;
    }

    /// <summary>Recruited companions (roadmap 20): ordinary people flagged <see cref="NpcSave.Companion"/> whom the
    /// director leaves alone. They follow the player (on foot, in the passenger seat, or driving a second vehicle behind
    /// them), wait where told, guard a claimed base (counted in its defence), fight what threatens the player and carry
    /// a 25 kg pack. Recruited by charisma or for pay; one, two with CHA 7, three with CHA 9. Authority only; saved.</summary>
    public static class Companions
    {
        public static readonly List<Npc> Live = new List<Npc>();
        static List<CompanionSave> pending;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Live.Clear(); pending = null; }

        public const int HireScrap = 80;

        public static int Max(WastelandGame g)
        {
            int cha = g.Stats.Attribute(Attr.Charisma);
            return 1 + (cha >= 7 ? 1 : 0) + (cha >= 9 ? 1 : 0);
        }

        public static bool Full(WastelandGame g) { Prune(); return Live.Count >= Max(g); }

        /// <summary>This person walks with the player (or will, once loaded) — the director must not spawn them.</summary>
        public static bool Has(string id)
        {
            foreach (var n in Live) if (n && n.Profile.id == id) return true;
            if (pending != null) foreach (var s in pending) if (s.id == id) return true;
            return false;
        }

        /// <summary>Who can be asked: ordinary folk, not traders, bosses or raiders still fighting.</summary>
        public static bool CanAsk(Npc n) => n && n.Alive && !n.companion && !n.Hostile && n.convoy == null &&
            (n.Profile.role == NpcRole.Wanderer || n.Profile.role == NpcRole.Resident || (n.Profile.Raider && n.Surrendered));

        public static void Recruit(WastelandGame g, Npc n)
        {
            if (!n || n.companion) return;
            n.State.Set(NpcSave.Companion);
            n.State.Set(NpcSave.Surrendered, false);
            n.companion = true; n.order = 0; n.leaving = false; n.raiding = false; n.aggro = false;
            n.transform.SetParent(null, true);
            n.EnsurePack();
            NpcDirector.Instance?.Release(n);
            Live.Add(n);
            g.Stats.Practice(Skill.Speech, 5f);
            g.Toast(n.Profile.Name + " JOINS YOU  ([E] ORDERS  [T] PACK)");
        }

        /// <summary>Part ways: they go back to their old life (spill what they carry for you first).</summary>
        public static void Dismiss(WastelandGame g, Npc n)
        {
            if (!n) return;
            Live.Remove(n);
            if (n.Driving) n.LeaveWheel();
            if (n.Riding) n.Unboard();
            if (n.pack) n.pack.Spill();
            n.State.Set(NpcSave.Companion, false);
            n.companion = false;
            n.LetGo();
            g.Toast(n.Profile.Name + " GOES THEIR OWN WAY");
        }

        public static void Lost(Npc n)
        {
            Live.Remove(n);
            n.State.Set(NpcSave.Companion, false);
            WastelandGame.Instance?.Toast(n.Profile.Name + " IS DEAD");
        }

        static void Prune() { for (int i = Live.Count - 1; i >= 0; i--) if (!Live[i] || !Live[i].Alive) Live.RemoveAt(i); }

        /// <summary>Companions guarding inside a claim (for its defence score).</summary>
        public static int GuardsAt(Vector3 at, float radius)
        {
            int k = 0;
            foreach (var n in Live) if (n && n.Alive && n.order == 2 && (n.home - at).sqrMagnitude < radius * radius) k++;
            return k;
        }

        /// <summary>Bring loaded companions into the world beside the player (or at their posts).</summary>
        public static void Tick(WastelandGame g)
        {
            Prune();
            if (pending == null || !g.Player || !DeformableTerrainReady()) return;
            var list = pending; pending = null;
            foreach (var s in list)
            {
                var p = NpcProfile.Make(s.id, (NpcRole)s.role, s.seed, s.kind);
                if (NpcRegistry.IsDead(p.id)) continue;
                var pos = s.order == 0 ? g.Player.transform.position - g.Player.transform.forward * 2.5f + g.Player.transform.right * (Live.Count - 1) : s.position;
                var t = MadMax.World.DeformableTerrain.Instance;
                pos.y = t.Height(pos.x, pos.z) + 0.1f;
                var n = Npc.Spawn(p, pos, 0f, null, g.propMaterial);
                n.companion = true; n.order = s.order;
                if (s.order != 0) { n.home = s.post; n.homeRadius = s.order == 2 ? 10f : 1f; }
                n.EnsurePack();
                if (!string.IsNullOrEmpty(s.pack)) n.pack.LoadState(s.pack);
                Live.Add(n);
                if (s.vehicle >= 0 && s.vehicle < g.Fleet.Count && g.Fleet[s.vehicle]) n.TakeWheel(g.Fleet[s.vehicle]);
            }
        }

        static int IndexOf(IReadOnlyList<VehicleDriver> list, VehicleDriver v) { for (int i = 0; i < list.Count; i++) if (list[i] == v) return i; return -1; }

        static bool DeformableTerrainReady() => MadMax.World.DeformableTerrain.Instance && MadMax.World.DeformableTerrain.Instance.World != null;

        public static void Save(WastelandGame g, SaveData d)
        {
            Prune();
            d.companions = new List<CompanionSave>();
            foreach (var n in Live)
            {
                var p = n.Profile;
                d.companions.Add(new CompanionSave
                {
                    id = p.id, role = (int)p.role, seed = p.seed, kind = p.kind, order = n.order, position = n.transform.position, post = n.home,
                    pack = n.pack ? n.pack.SaveState() : null, vehicle = n.Driving ? IndexOf(g.Fleet, n.DrivenCar) : -1
                });
            }
            if (pending != null) d.companions.AddRange(pending);
        }

        public static void Load(SaveData d)
        {
            Live.Clear();
            pending = d.companions != null && d.companions.Count > 0 ? new List<CompanionSave>(d.companions) : null;
        }
    }
}
