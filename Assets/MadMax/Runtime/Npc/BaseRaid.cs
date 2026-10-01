using System.Collections.Generic;
using MadMax.Building;
using MadMax.Game;
using MadMax.Items;
using MadMax.World;
using UnityEngine;

namespace MadMax.Npc
{
    /// <summary>Raids on claimed bases (<see cref="ClaimFlag"/> with a few pieces around it). Every few days
    /// (<see cref="GameRules.raids"/>) the gang whose road runs nearest comes (not when recruited, allied or paid off
    /// lately; WasteTalk FM warns an hour ahead; a party wiped out thins the gang's next convoy): live if the player is near (raiders batter the nearest pieces,
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
        Convoy gang;                 // whose road runs nearest the claim (null: no gangs on the map)
        float tick, endsAt, warnedFor = -1f;
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
            // WasteTalk hears of it an hour ahead
            if (warnedFor != NextDay && NextDay - DayNight.TotalDays < 1f / 24f)
            {
                warnedFor = NextDay;
                var g0 = GangFor(claim);
                if (g0 != null && !g0.SparesBases) MadMax.Audio.RadioNetwork.Flash("THE " + g0.Gang + " ARE RIDING ON A HOMESTEAD " + Near(claim.transform.position) + ". BAR YOUR DOORS, FOLKS", 45f, claim.transform.position);
            }
            if (DayNight.TotalDays < NextDay) return;
            // raiders prefer to come at dusk or night when the player is around (unless long overdue)
            if (dist < 250f && DayNight.Darkness < 0.3f && DayNight.TotalDays < NextDay + 0.5f) return;
            NextDay = DayNight.TotalDays + Interval * Random.Range(0.7f, 1.3f);
            if (!PickGang(claim, dist < 250f)) return;
            if (dist < 250f) Begin(claim);
            else Resolve(claim);
        }

        /// <summary>The gang that raids a claim: the one whose road runs nearest (raids by territory).</summary>
        static Convoy GangFor(ClaimFlag claim) => NpcDirector.Instance ? NpcDirector.Instance.RaidersByRoad(claim.transform.position) : null;

        /// <summary>Settle who comes; false when the territory's gang leaves the base alone (recruited, allied, paid off)
        /// or every gang is wiped out for now.</summary>
        bool PickGang(ClaimFlag claim, bool near)
        {
            gang = GangFor(claim);
            if (NpcDirector.Instance && NpcDirector.Instance.Convoys.Count > 0 && gang == null) return false;   // the roads are clear
            if (gang != null && gang.SparesBases)
            {
                if (near) game.Toast("THE " + gang.Gang + " RIDE PAST YOUR BASE AND LEAVE IT BE");
                return false;
            }
            return true;
        }

        /// <summary>How the raid ended, pinned up on the boards near the homestead (TownNews).</summary>
        void PostHomestead(Vector3 at, bool held)
        {
            string who = gang != null ? "THE " + gang.Gang : "RAIDERS";
            TownNews.Post(held ? who + " HIT A HOMESTEAD " + Near(at) + " AND WERE DRIVEN OFF" : who + " RAIDED A HOMESTEAD " + Near(at) + " AND MADE OFF WITH ITS STORES", at);
        }

        static string Near(Vector3 p)
        {
            var w = DeformableTerrain.Instance ? DeformableTerrain.Instance.World : null;
            if (w == null || w.settlements.Count == 0) return "OUT IN THE WASTE";
            Settlement best = null; float bd = float.MaxValue;
            foreach (var st in w.settlements) { float d = Vector2.Distance(st.pos, new Vector2(p.x, p.z)); if (d < bd) { bd = d; best = st; } }
            return bd < 400f ? "NEAR " + Market.TownName(best) : Mathf.RoundToInt(bd / 100f) / 10f + " KM OUT OF " + Market.TownName(best);
        }

        /// <summary>The claim raiders go for: the most built-up one.</summary>
        static ClaimFlag PickClaim()
        {
            ClaimFlag best = null; int most = 5;
            foreach (var c in ClaimFlag.All) { if (!c) continue; int n = c.Pieces(); if (n > most) { most = n; best = c; } }
            return best;
        }

        int Strength => Mathf.Max(2, 3 + Mathf.Min(4, DayNight.Day / 6) - (gang != null ? gang.save.losses : 0));

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
            int gi = gang != null ? System.Array.IndexOf(NpcLore.Gangs, gang.Gang) : -1;
            for (int i = 0; i < n; i++)
            {
                var role = i == 0 && DayNight.Day >= 4 ? NpcRole.RaiderBoss : NpcRole.Raider;
                int seed = gi >= 0 ? r.Next(1, int.MaxValue / 8) * NpcLore.Gangs.Length + gi : r.Next();   // the profile's gang is the territory's
                var p = NpcProfile.Make("raid:" + DayNight.Day + ":" + i, role, seed);
                var pos = origin + new Vector3((float)r.NextDouble() * 8f - 4f, 0f, (float)r.NextDouble() * 8f - 4f);
                if (t) pos.y = t.Height(pos.x, pos.z) + 0.1f;
                var npc = Npc.Spawn(p, pos, Quaternion.LookRotation(at - pos).eulerAngles.y, null, game.propMaterial);
                npc.aggro = true; npc.raiding = true; npc.raidAt = at;
                npc.carBreaker = i % 3 == 1;                                                  // some go for the parked vehicles
                party.Add(npc);
            }
            game.Toast((gang != null ? "THE " + gang.Gang : "RAIDERS") + " ARE COMING FOR YOUR BASE (" + n + ")");
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
                if (target) PostHomestead(target.transform.position, true);
                if (gang != null) { gang.save.losses = Mathf.Min(3, gang.save.losses + 1); game.Toast("RAID REPELLED: THE " + gang.Gang + " WILL BE THINNER ON THE ROAD"); }
                else game.Toast("RAID REPELLED");
                MadMax.Audio.Sfx.Play("crowd_cheer", game.Player.transform.position, 0.5f);
            }
            else if (!over && Time.time > endsAt)
            {
                // they grab what they can and go
                over = true;
                if (target) { Loot(target, 0.25f); PostHomestead(target.transform.position, false); }
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
            DefenceWorks.SpendMines(claim);                                                    // they walk into some of the mines
            foreach (var p in Placeable.All)                                                 // turrets spend a belt
                if (p && claim.Inside(p.transform.position) && p.TryGetComponent<AutoTurret>(out var tur) && tur.on)
                {
                    if (tur.rounds > 0) tur.rounds = 0;
                    else if (p.TryGetComponent<Container>(out var box)) box.inventory.TakeItem("ammo_mg");
                }
            if (defence >= strength * Random.Range(0.7f, 1.3f)) { Report = "YOUR DEFENCES DROVE OFF A RAID ON YOUR BASE"; PostHomestead(claim.transform.position, true); return; }
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
            int stripped = StripVehicles(claim, wreck);
            PostHomestead(claim.transform.position, false);
            Report = "YOUR BASE WAS RAIDED: " + broken + " PIECES WRECKED" + (looted ? ", STORES LOOTED" : "") + (stripped > 0 ? ", " + stripped + " VEHICLE" + (stripped > 1 ? "S" : "") + " SIPHONED AND STRIPPED" : "");
        }

        /// <summary>Vehicles parked on the claim: raiders siphon half the tank and carry off a part or two.</summary>
        int StripVehicles(ClaimFlag claim, int budget)
        {
            int n = 0;
            foreach (var v in game.AllVehicles)
            {
                if (!v || v == game.Current || v.aiDriven || !claim.Inside(v.transform.position) || n >= Mathf.Max(1, budget / 2)) continue;
                if (v.TryGetComponent<MadMax.Vehicles.VehicleSystems>(out var sys)) sys.fuel *= 0.5f;
                var chassis = v.GetComponent<MadMax.Vehicles.VehicleChassis>();
                int took = 0;
                if (chassis)
                    foreach (var s in chassis.Sockets)
                    {
                        var part = s.Current;
                        if (!part || part.category == MadMax.Vehicles.PartCategory.Engine || part.category == MadMax.Vehicles.PartCategory.Wheel || Random.value > 0.35f) continue;
                        var gone = s.Detach(false);
                        if (gone) Destroy(gone.gameObject);
                        if (++took >= 2) break;
                    }
                n++;
            }
            return n;
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
            if (!PickGang(claim, false)) { NextDay = DayNight.TotalDays + Interval * Random.Range(0.7f, 1.3f); return false; }
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
