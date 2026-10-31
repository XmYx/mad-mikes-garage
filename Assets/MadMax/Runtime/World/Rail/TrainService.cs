using System.Collections.Generic;
using MadMax.Game;
using MadMax.Npc;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>A timetabled run (like the rolling city's): a fixed route through the network from station to station
    /// and back (ping-pong), a dwell at each stop, legs with accelerate / cruise / brake; the train's head is a function
    /// of <see cref="clock"/>, so far-off services need no simulation and a save keeps only the clock. Makeshift
    /// services (Fuel Guild traders and their families) put people and a trader on the platform while they stand;
    /// armoured patrols (a raider gang's, the Bunker Remnants') shoot at whoever their faction hates. A train held up
    /// by another on the line ahead slows its clock to a stop. Knocked out, its train becomes a free train anyone with
    /// the skill can drive off; the service is back three days later.</summary>
    public sealed class TrainService
    {
        public int id;
        public bool armoured;
        public string name;
        public Faction faction;
        public RailPath route;
        public CarKind[] consist;
        public float trainLength, vmax, accel = 0.35f, dwell;
        public readonly List<float> stops = new List<float>();          // head positions on the route
        public readonly List<int> stopStation = new List<int>();
        public double clock;
        public bool ended;
        public int endDay;
        public float aggroUntil;
        public Train live;
        public int seed;

        float[] legT; int[] seq; float cycle;
        float hold = 1f;
        public float Load { get; private set; }
        public bool Braking { get; private set; }
        public int AtStop { get; private set; } = -1;
        public float DwellLeft { get; private set; }
        int lastStop = -2;
        float fireAt, scanAt, holdCheckAt;
        bool holdWanted;
        public float Cycle => cycle;

        /// <summary>Precompute the legs (after stops are set).</summary>
        public void Timetable()
        {
            int n = stops.Count;
            var s = new List<int>();
            for (int i = 0; i < n; i++) s.Add(i);
            for (int i = n - 2; i >= 1; i--) s.Add(i);
            seq = s.ToArray();
            legT = new float[seq.Length];
            cycle = 0f;
            for (int i = 0; i < seq.Length; i++)
            {
                float L = Mathf.Abs(stops[seq[(i + 1) % seq.Length]] - stops[seq[i]]);
                legT[i] = LegTime(L);
                cycle += dwell + legT[i];
            }
        }

        float LegTime(float L) { float ta = vmax / accel; return L > vmax * ta ? L / vmax + ta : 2f * Mathf.Sqrt(L / accel); }

        /// <summary>Head position, speed (signed along the route) and the stop it stands at (-1: under way).</summary>
        public float Evaluate(double c, out float v, out int stop, out float dwellLeft, out float acc)
        {
            float t = (float)(c % cycle); if (t < 0f) t += cycle;
            v = 0f; stop = -1; dwellLeft = 0f; acc = 0f;
            for (int i = 0; i < seq.Length; i++)
            {
                if (t < dwell) { stop = seq[i]; dwellLeft = dwell - t; return stops[seq[i]]; }
                t -= dwell;
                if (t < legT[i])
                {
                    float a = stops[seq[i]], b = stops[seq[(i + 1) % seq.Length]];
                    float L = Mathf.Abs(b - a), T = legT[i], sgn = Mathf.Sign(b - a), d;
                    float ta = Mathf.Min(vmax / accel, T * 0.5f), vtop = accel * ta;
                    if (t < ta) { d = 0.5f * accel * t * t; v = accel * t; acc = 1f; }
                    else if (t < T - ta) { d = 0.5f * accel * ta * ta + vtop * (t - ta); v = vtop; acc = 0.35f; }
                    else { float r = T - t; d = L - 0.5f * accel * r * r; v = accel * r; acc = -1f; }
                    v *= sgn;
                    return a + sgn * d;
                }
                t -= legT[i];
            }
            return stops[0];
        }

        /// <summary>Seconds of the clock until the train next stands at <paramref name="stop"/> (0 while it dwells there;
        /// -1 when the route never calls there), and the stop it heads for after it (2026-10-31: timetable boards).</summary>
        public float SecondsUntil(int stop, out int next)
        {
            next = -1;
            if (seq == null || cycle <= 0f) return -1f;
            float t = (float)(clock % cycle); if (t < 0f) t += cycle;
            float at = 0f;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < seq.Length; i++)
                {
                    if (seq[i] == stop)
                    {
                        if (t >= at && t < at + dwell) { next = seq[(i + 1) % seq.Length]; return 0f; }
                        if (at >= t) { next = seq[(i + 1) % seq.Length]; return at - t; }
                    }
                    at += dwell + legT[i];
                }
            return -1f;
        }

        public Vector3 WorldPoint() { float h = Evaluate(clock, out _, out _, out _, out _); return route.At(h - trainLength * 0.5f); }

        /// <summary>One physics step of the live train: advance the clock (held back by a train on the line ahead), pose
        /// the head, stop events, the turrets.</summary>
        public void Step(Train t, float dt)
        {
            Evaluate(clock, out float v0, out _, out _, out _);
            if (Time.time >= holdCheckAt)
            {
                holdCheckAt = Time.time + 0.25f;
                int sign = v0 >= 0f ? 1 : -1;
                float stopDist = v0 * v0 / (2f * accel) + 18f;
                holdWanted = Mathf.Abs(v0) > 0.2f && t.Obstacle(sign, stopDist, out var other, out _) && other.service == null;
            }
            hold = Mathf.MoveTowards(hold, holdWanted ? 0f : 1f, dt * 0.6f);
            clock += dt * hold;
            t.head = Evaluate(clock, out float v, out int stop, out float left, out float acc);
            t.speed = v * hold;
            Load = Mathf.Clamp01(acc > 0f ? 0.9f : acc < 0f ? 0.15f : 0.45f) * (Mathf.Abs(t.speed) > 0.1f || stop < 0 ? 1f : 0.4f);
            Braking = acc < 0f || holdWanted;
            AtStop = stop; DwellLeft = left;
            if (stop != lastStop)
            {
                if (lastStop >= -1) TrainDirector.Instance?.StopEvent(this, t, stop, lastStop);
                lastStop = stop;
            }
            if (stop >= 0 && left < 4f && left > 3.5f) t.Horn();
            if (armoured) Turrets(t, dt);
        }

        /// <summary>The gang or faction this train belongs to wants the player dead right now.</summary>
        public bool Hostile
        {
            get
            {
                if (Time.time < aggroUntil) return true;
                if (!armoured) return false;
                return faction == Faction.Remnants ? Factions.Hostile(faction) : !Factions.Friendly(faction);
            }
        }

        void Turrets(Train t, float dt)
        {
            var g = WastelandGame.Instance;
            if (!g || !g.Player) return;
            var target = g.Current ? g.Current.transform.position + Vector3.up : g.Player.transform.position + Vector3.up * 1.2f;
            bool hostile = Hostile && (g.Current || g.Player.gameObject.activeSelf);
            foreach (var c in t.cars)
            {
                if (!c || !c.Turret || c.Broken) continue;
                var tp = c.Turret.position;
                float dist = Vector3.Distance(tp, target);
                Quaternion want;
                if (hostile && dist < 90f) want = Quaternion.LookRotation(Vector3.ProjectOnPlane(target - tp, Vector3.up).normalized + Vector3.up * 0.0001f, Vector3.up);
                else want = c.transform.rotation * Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.3f + c.variant * 1.7f + c.transform.position.x * 0.01f) * 70f, 0f);
                c.Turret.rotation = Quaternion.RotateTowards(c.Turret.rotation, want, 40f * dt);
                if (!hostile || dist > 80f || Time.time < fireAt) continue;
                if (Quaternion.Angle(c.Turret.rotation, want) > 8f) continue;
                var muzzle = tp + c.Turret.forward * 3.1f + Vector3.up * 0.5f;
                if (Physics.Linecast(muzzle, target, out var hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    var hitT = hit.collider.transform;
                    bool atTarget = (g.Current && hitT.IsChildOf(g.Current.transform)) || hitT.IsChildOf(g.Player.transform);
                    if (!atTarget && !hitT.IsChildOf(c.transform)) continue;                          // no line of fire
                }
                Npc.Npc.Blast(c.gameObject, muzzle, target, dist, 1.5f);
                Fx.Flash(muzzle, new Color(1f, 0.7f, 0.3f), 8f, 3f, 0.08f);
                fireAt = Time.time + Random.Range(1.6f, 2.6f);
                if (Time.time >= scanAt) { scanAt = Time.time + 20f; g.Toast(name + " OPENS FIRE"); }
            }
        }

        /// <summary>The player hit this train: armoured ones fight back for five minutes, traders take offence.</summary>
        public void Hit(float power)
        {
            if (aggroUntil < Time.time + 200f && !armoured) Factions.Shift(Faction.FuelGuild, -2);
            aggroUntil = Time.time + 300f;
        }

        /// <summary>A car was knocked out: an armoured train with its loco or every gun gone (a makeshift one with its
        /// loco gone) stops for good — the crew climbs out, the train is free for the taking.</summary>
        public void CarBroken(Train t, RailCar car)
        {
            bool guns = false;
            foreach (var c in t.cars) if (c && c.def.turret && !c.Broken) guns = true;
            bool stop = car.def.cab || (armoured && !guns);
            if (!stop) return;
            End(t, armoured ? "THE " + name + " IS KNOCKED OUT - ITS CREW BAILS OUT" : "THE " + name + " GRINDS TO A HALT");
            TrainDirector.Instance?.Crew(this, t, armoured);
            var loco = t.Loco;
            if (loco) loco.hp = Mathf.Max(loco.hp, loco.def.hp * 0.3f);                            // the engine still turns over
        }

        /// <summary>Hand the train over as a free train (knocked out, stolen) and park the service for three days.</summary>
        public void End(Train t, string toast)
        {
            ended = true; endDay = DayNight.Day;
            if (t)
            {
                t.service = null; t.path = t.path.Clone(); t.owned = false; t.driven = false; t.brake = 1f; t.speed = 0f; t.fuel = t.tank * 0.3f;
                t.trainName = name;
                t.KeyLoot("rail:taken:" + id + ":" + endDay);
                TrainDirector.Instance?.ClearPlatform(this);
            }
            live = null;
            if (toast != null) WastelandGame.Instance?.Toast(toast);
        }
    }
}
