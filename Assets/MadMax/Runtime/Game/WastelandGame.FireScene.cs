using System.Collections.Generic;
using MadMax.Npc;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>A fire is a scene (roadmap 10-06 suggestion): a burning vehicle inside a settlement draws up to
    /// <see cref="FireCrew"/> residents with buckets and bottles (<see cref="MadMax.Npc.Npc.FightFire"/>). When it ends the
    /// town remembers: a player who stood by and helped (within <see cref="FireHelpReach"/>) gains the firefighters'
    /// disposition and the town's standing; a fire the player's own bottle started (a molotov within 6 m in the last
    /// minute) costs both; raiders' fire bombs make the news. Authority only.</summary>
    public partial class WastelandGame
    {
        public const int FireCrew = 2, FireBursts = 6;
        public const float FireCallReach = 60f, FireHelpReach = 14f;

        struct Bomb { public Vector3 at; public float time; public bool player; }
        static readonly List<Bomb> bombs = new List<Bomb>();
        class TownFire { public Settlement town; public List<MadMax.Npc.Npc> crew = new List<MadMax.Npc.Npc>(); public bool helped, byPlayer, byRaiders; }
        readonly Dictionary<VehicleSystems, TownFire> townFires = new Dictionary<VehicleSystems, TownFire>();
        float fireSceneNext;
        /// <summary>The last fire-scene outcome (tests): the town news line.</summary>
        public static string LastFireScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFireScene() { bombs.Clear(); LastFireScene = null; }

        /// <summary>A fire bottle broke at <paramref name="at"/> (Molotov): kept a minute so a car fire can be traced to it.</summary>
        public static void NoteFireBomb(Vector3 at, bool byPlayer)
        {
            bombs.Add(new Bomb { at = at, time = Time.time, player = byPlayer });
            if (bombs.Count > 16) bombs.RemoveAt(0);
        }

        static int BombNear(Vector3 p)
        {
            for (int i = bombs.Count - 1; i >= 0; i--)
                if (Time.time - bombs[i].time < 60f && (bombs[i].at - p).sqrMagnitude < 36f) return bombs[i].player ? 1 : 2;
            return 0;
        }

        void UpdateFireScenes()
        {
            if (Time.time < fireSceneNext || World == null || !Player) return;
            fireSceneNext = Time.time + 1f;
            if (MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient) return;
            var me = Current ? Current.transform.position : Player.transform.position;
            foreach (var vs in VehicleSystems.All)
            {
                if (!vs || !vs.Burning || townFires.ContainsKey(vs)) continue;
                var p = vs.FirePos;
                var st = World.SettlementAt(p.x, p.z);
                if (st == null) continue;
                var tf = new TownFire { town = st };
                int who = BombNear(p);
                tf.byPlayer = who == 1; tf.byRaiders = who == 2;
                foreach (var n in MadMax.Npc.Npc.All)
                {
                    if (tf.crew.Count >= FireCrew) break;
                    if (!n || !n.Available || n.proxy || n.companion || n.Hostile || n.Profile.Raider || n.fireTarget || n.Foe || n.tending) continue;
                    if ((n.transform.position - p).sqrMagnitude > FireCallReach * FireCallReach) continue;
                    n.FightFire(vs, FireBursts);
                    tf.crew.Add(n);
                }
                townFires[vs] = tf;
                if (tf.crew.Count > 0 && Flat(me - p) < 80f) Toast("FIRE! " + Market.TownName(st) + " FOLK RUN FOR BUCKETS AND BOTTLES");
            }
            List<VehicleSystems> done = null;
            foreach (var kv in townFires)
            {
                var vs = kv.Key; var tf = kv.Value;
                if (vs && vs.Burning)
                {
                    if (Flat(me - vs.FirePos) < FireHelpReach) tf.helped = true;                    // stood by it (bottle, sand, pulling cars clear)
                    continue;
                }
                (done ??= new List<VehicleSystems>()).Add(vs);
                var burn = vs ? vs.GetComponent<VehicleBurn>() : null;
                bool saved = vs && !(burn && burn.charred);
                Remember(tf, saved, vs ? vs.transform.position : new Vector3(tf.town.pos.x, 0f, tf.town.pos.y));
            }
            if (done != null) foreach (var vs in done) townFires.Remove(vs);
        }

        void Remember(TownFire tf, bool saved, Vector3 at)
        {
            if (tf.crew.Count == 0 && !tf.byPlayer && !tf.byRaiders) return;
            string town = Market.TownName(tf.town);
            var f = Factions.OfSettlement(tf.town);
            if (f == Faction.None) f = Faction.Settlers;
            if (tf.byPlayer)
            {
                foreach (var n in tf.crew) if (n) n.State.disposition = Mathf.Max(-100, n.State.disposition - 20);
                Factions.Shift(f, -6);
                LastFireScene = Stats.name + " SET A CAR ALIGHT IN " + town;
                Toast(town + " WON'T FORGET WHO STARTED THAT FIRE");
            }
            else if (tf.helped && saved)
            {
                foreach (var n in tf.crew) if (n) { n.State.disposition = Mathf.Min(100, n.State.disposition + 6); n.State.Set(NpcSave.Helped); }
                Factions.Shift(f, 2);
                LastFireScene = Stats.name + " HELPED " + town + " PUT OUT A BURNING CAR" + (tf.byRaiders ? " THE RAIDERS FIREBOMBED" : "");
                Toast(town + " REMEMBERS WHO HELPED PUT THAT FIRE OUT");
            }
            else if (tf.byRaiders) LastFireScene = "RAIDERS FIREBOMBED A CAR IN " + town + (saved ? ". THE STREET PUT IT OUT" : ". IT BURNED OUT");
            else if (tf.crew.Count > 0) LastFireScene = "A CAR CAUGHT FIRE IN " + town + (saved ? ". NEIGHBOURS PUT IT OUT" : ". IT BURNED OUT");
            else return;
            TownNews.Post(LastFireScene, at);
        }
    }
}
