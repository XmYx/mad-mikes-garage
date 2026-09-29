using System.Collections.Generic;
using MadMax.Building;
using MadMax.Game;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Raids on claimed bases (<see cref="ClaimFlag"/> with a few pieces around it). Every few days
    /// (<see cref="GameRules.raids"/>) a gang comes: live if the player is near (raiders batter the nearest pieces,
    /// fight the player when close; turrets, spikes, wire and bells defend), else resolved off-screen against the
    /// claim's defence score (lost pieces, looted stores) and reported when the player returns. Sleeping at the base
    /// on a raid night wakes the player to the bell. Authority only.</summary>
    public class BaseRaid : MonoBehaviour
    {
        public static BaseRaid Instance { get; private set; }
        /// <summary>World day (<see cref="DayNight.TotalDays"/>) of the next raid; &lt; 0 until a base is claimed. Saved.</summary>
        public static float NextDay = -1f;
        /// <summary>What happened while the player was away (shown on return). Saved.</summary>
        public static string Report;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Instance = null; NextDay = -1f; Report = null; }

        public static readonly float[] Intervals = { 0f, 6f, 3f, 1.5f };
        public bool Live => party.Count > 0 && !over;

        readonly List<Npc> party = new List<Npc>();
        ClaimFlag target;
        float tick, endsAt;
        bool over;
        WastelandGame game;

        void Awake() { Instance = this; game = WastelandGame.Instance; }

        static float Interval => Intervals[Mathf.Clamp(GameRules.Current.raids, 0, Intervals.Length - 1)];

        void Update()
        {
            if ((tick -= Time.deltaTime) > 0f) return;
            tick = 2f;
            if (!game) game = WastelandGame.Instance;
            if (!game || !game.Player || (MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient)) return;
            if (party.Count > 0) { TickRaid(); return; }
            if (Interval <= 0f) return;
            var claim = PickClaim();
            if (!claim) return;
            var me = game.Current ? game.Current.transform.position : game.Player.transform.position;
            float dist = Vector3.Distance(me, claim.transform.position);
            if (Report != null && dist < 120f) { game.Toast(Report); Report = null; }
            if (NextDay < 0f) { NextDay = DayNight.TotalDays + Interval * Random.Range(0.8f, 1.2f) + 0.5f; return; }
            if (DayNight.TotalDays < NextDay) return;
            // raiders prefer to come at dusk or night when the player is around (unless long overdue)
            if (dist < 250f && DayNight.Darkness < 0.3f && DayNight.TotalDays < NextDay + 0.5f) return;
            NextDay = DayNight.TotalDays + Interval * Random.Range(0.7f, 1.3f);
            if (dist < 250f) Begin(claim);
            else Resolve(claim);
        }

        /// <summary>The claim raiders go for: the most built-up one.</summary>
        static ClaimFlag PickClaim()
        {
            ClaimFlag best = null; int most = 5;
            foreach (var c in ClaimFlag.All) { if (!c) continue; int n = c.Pieces(); if (n > most) { most = n; best = c; } }
            return best;
        }

        int Strength => 3 + Mathf.Min(4, DayNight.Day / 6);

        /// <summary>Raiders gather ~75 m out and storm the claim.</summary>
        public void Begin(ClaimFlag claim)
        {
            target = claim; over = false; endsAt = Time.time + 420f;
            var t = DeformableTerrain.Instance;
            var at = claim.transform.position;
            var r = new System.Random(DayNight.Day * 7919 + Mathf.RoundToInt(at.x) * 31 + Mathf.RoundToInt(at.z));
            float yaw = (float)r.NextDouble() * 360f;
            Vector3 origin = at;
            for (int k = 0; k < 8; k++)
            {
                var dir = Quaternion.Euler(0f, yaw + k * 45f, 0f) * Vector3.forward;
                origin = at + dir * 75f;
                if (!t || t.WaterDepth(origin.x, origin.z) < 0.2f) break;
            }
            int n = Strength;
            for (int i = 0; i < n; i++)
            {
                var role = i == 0 && DayNight.Day >= 4 ? NpcRole.RaiderBoss : NpcRole.Raider;
                var p = NpcProfile.Make("raid:" + DayNight.Day + ":" + i, role, r.Next());
                var pos = origin + new Vector3((float)r.NextDouble() * 8f - 4f, 0f, (float)r.NextDouble() * 8f - 4f);
                if (t) pos.y = t.Height(pos.x, pos.z) + 0.1f;
                var npc = Npc.Spawn(p, pos, Quaternion.LookRotation(at - pos).eulerAngles.y, null, game.propMaterial);
                npc.aggro = true; npc.raiding = true; npc.raidAt = at;
                party.Add(npc);
            }
            game.Toast("RAIDERS ARE COMING FOR YOUR BASE (" + n + ")");
            MadMax.Audio.Sfx.Play("horn", origin, 1f, 0.8f, 220f);
        }

        void TickRaid()
        {
            int alive = 0;
            foreach (var n in party) if (n && n.Alive) alive++;
            if (!over && alive == 0)
            {
                over = true;
                NpcRegistry.Reputation = Mathf.Min(100, NpcRegistry.Reputation + 2);
                game.Toast("RAID REPELLED");
                MadMax.Audio.Sfx.Play("crowd_cheer", game.Player.transform.position, 0.5f);
            }
            else if (!over && Time.time > endsAt)
            {
                // they grab what they can and go
                over = true;
                if (target) Loot(target, 0.25f);
                foreach (var n in party) if (n && n.Alive) { n.raiding = false; n.aggro = false; n.Scare(60f); }
                game.Toast("THE RAIDERS MADE OFF WITH SOME OF YOUR STORES");
            }
            if (!over) return;
            // clear away once out of sight
            var me = game.Player.transform.position;
            for (int i = party.Count - 1; i >= 0; i--)
            {
                var n = party[i];
                if (!n) { party.RemoveAt(i); continue; }
                if ((n.transform.position - me).sqrMagnitude > 140f * 140f) { Destroy(n.gameObject); party.RemoveAt(i); }
            }
        }

        /// <summary>Off-screen raid: defence score against the gang's strength.</summary>
        void Resolve(ClaimFlag claim)
        {
            float strength = Strength * 2f + DayNight.Day * 0.2f;
            int defence = claim.Defence();
            foreach (var p in Placeable.All)                                                 // turrets spend a belt
                if (p && claim.Inside(p.transform.position) && p.TryGetComponent<AutoTurret>(out var tur) && tur.on)
                {
                    if (tur.rounds > 0) tur.rounds = 0;
                    else if (p.TryGetComponent<Container>(out var box)) box.inventory.TakeItem("ammo_mg");
                }
            if (defence >= strength * Random.Range(0.7f, 1.3f)) { Report = "YOUR DEFENCES DROVE OFF A RAID ON YOUR BASE"; return; }
            int wreck = Mathf.Clamp(Mathf.RoundToInt(strength - defence), 1, 6);
            var outer = new List<Placeable>();
            foreach (var p in Placeable.All) if (p && !p.GetComponentInParent<Rigidbody>() && claim.Inside(p.transform.position) && !p.GetComponent<ClaimFlag>()) outer.Add(p);
            var c = claim.transform.position;
            outer.Sort((a, b) => (b.transform.position - c).sqrMagnitude.CompareTo((a.transform.position - c).sqrMagnitude));   // outermost first
            int broken = 0;
            for (int i = 0; i < outer.Count && broken < wreck; i++)
            {
                if (!outer[i] || outer[i].Collapsing) continue;
                outer[i].ApplyHit(outer[i].transform.position, Vector3.down, 999f, 0.5f, gameObject);
                broken++;
            }
            bool looted = Loot(claim, 0.3f);
            Report = "YOUR BASE WAS RAIDED: " + broken + " PIECES WRECKED" + (looted ? ", STORES LOOTED" : "");
        }

        /// <summary>Raiders empty part of the fullest container in the claim.</summary>
        static bool Loot(ClaimFlag claim, float share)
        {
            Container best = null; float most = 0.5f;
            foreach (var box in Container.All)
                if (box && box.GetComponent<Placeable>() && claim.Inside(box.transform.position) && box.Weight > most) { most = box.Weight; best = box; }
            if (!best) return false;
            var res = best.inventory.ResourceArray;
            for (int t = 0; t < res.Length; t++) { int take = Mathf.FloorToInt(res[t] * share); if (take > 0) best.inventory.TrySpend((ResourceType)t, take); }
            return true;
        }

        /// <summary>Sleeping at a base on a raid night: the bell wakes you in the small hours and the raid begins.</summary>
        public bool WakeFor(WastelandGame g)
        {
            if (Interval <= 0f || party.Count > 0 || NextDay < 0f) return false;
            var claim = ClaimFlag.Near(g.Player.transform.position);
            if (!claim || claim != PickClaim()) return false;
            float morning = DayNight.Day + (DayNight.Hours > 7f ? 1f : 0f) + 7f / 24f;
            if (NextDay > morning) return false;
            if (DayNight.Hours > 7f && DayNight.Hours < 23f) { DayNight.SetHours(2f); DayNight.SetDay(DayNight.Day + 1); }
            else if (DayNight.Hours < 2f) DayNight.SetHours(2f);
            NextDay = DayNight.TotalDays + Interval * Random.Range(0.7f, 1.3f);
            Begin(claim);
            foreach (var bell in AlarmBell.All) if (bell && claim.Inside(bell.transform.position)) { bell.Ring(false); break; }
            g.Toast("WOKEN IN THE NIGHT: RAIDERS AT THE BASE!");
            return true;
        }
    }
}
