using MadMax.Game;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Off the rails (2026-10-30). A free train taken into a curve too fast first grinds its flanges (squeal and
    /// sparks past <see cref="FlangeAt"/> m/s² sideways, <see cref="RailWheels"/>), then, held past
    /// <see cref="DerailAt"/> (<see cref="ScrapDerailAt"/> on scrap track) for <see cref="DerailHold"/> s on player-laid track, climbs off
    /// (the generated network's bends — some as tight as ~26 m — have check rails: they only squeal):
    /// the cars slew outward of the curve and lean over, ploughing to a stop in dust and sparks, and take damage by the
    /// speed. A derailed train will not move; a jack puts it back on (context RE-RAIL beside it: timed work, a car at a
    /// time, Railways practice). Services keep to their timetable speeds and never come off. Saved with the free train.</summary>
    public partial class Train
    {
        public const float FlangeAt = 1.6f, DerailAt = 3.2f, ScrapDerailAt = 2.4f, DerailHold = 0.35f, RerailSeconds = 5f;
        public bool Derailed { get; private set; }
        /// <summary>0 on the rails .. 1 fully off (eases in as the cars slew over, out while re-railed).</summary>
        public float DerailBlend { get; private set; }
        /// <summary>Re-railing in progress.</summary>
        public bool Rerailing => rerailBy != null;
        /// <summary>Cars back on so far.</summary>
        public int RerailedCars { get; private set; }
        public float LastHornTime { get; private set; } = -100f;
        public static int Derailments;
        public static Train LastDerailed;
        /// <summary>The worst sideways pull of a car in the last step (m/s²).</summary>
        public float CurvePull { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetDerail() { Derailments = 0; LastDerailed = null; }

        float overFor, rerailT, rerailClank, scrapeAt;
        float[] slew = new float[0], lean = new float[0];
        bool[] onRails = new bool[0];
        Transform rerailBy;

        /// <summary>Brakes biting at speed (sparks off the blocks).</summary>
        public bool BrakingHard => Mathf.Abs(speed) > 2f && !Derailed && (service != null ? service.Braking : driven && brake > 0.4f);

        /// <summary>Sideways offset and roll of car <paramref name="i"/> while off the rails.</summary>
        void DerailPose(int i, ref Vector3 pos, ref Quaternion rot)
        {
            if (DerailBlend <= 0f || i >= slew.Length) return;
            float k = i < onRails.Length && onRails[i] ? 0f : DerailBlend;
            if (k <= 0f) return;
            var right = rot * Vector3.right;
            pos += right * slew[i] * k - Vector3.up * 0.18f * k;
            rot = Quaternion.AngleAxis(lean[i] * k, rot * Vector3.forward) * Quaternion.AngleAxis(slew[i] * 4f * k, Vector3.up) * rot;
        }

        /// <summary>Signed curvature (1/m, + = turning right along the path) of the track under a car centred at path
        /// distance <paramref name="d"/>, from the tangents over the car's length plus 2 m each side — a joint or kink
        /// between two lines is spread over that span instead of spiking the pull.</summary>
        public float CurvatureAt(float d, float half)
        {
            float span = half + 2f;
            path.At(d + span, out var a); path.At(d - span, out var b);
            a.y = 0f; b.y = 0f;
            if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(b, a, Vector3.up) * Mathf.Deg2Rad / (2f * span);
        }

        /// <summary>Sideways acceleration of car <paramref name="i"/> in the curve (m/s²).</summary>
        public float PullOf(int i) => i < 0 || i >= cars.Count || !cars[i] || i >= offsets.Length ? 0f : Mathf.Abs(speed * speed * CurvatureAt(head - offsets[i], cars[i].def.Half));

        /// <summary>Free trains only: the pull in the curve, the flange limit, coming off.</summary>
        void DerailCheck(float dt)
        {
            float pull = 0f, yawSign = 0f;
            for (int i = 0; i < cars.Count; i++)
            {
                if (!cars[i] || i >= offsets.Length) continue;
                float k = CurvatureAt(head - offsets[i], cars[i].def.Half);
                float a = Mathf.Abs(speed * speed * k);
                if (a > pull) { pull = a; yawSign = Mathf.Sign(k * speed); }
            }
            CurvePull = pull;
            if (service != null || Derailed || dt <= 0f) return;
            var l = path.LineAt(head, out _, out _);
            if (l == null || !l.built) { overFor = 0f; return; }                                     // the Guild's lines have check rails on the bends
            float limit = l.scrap ? ScrapDerailAt : DerailAt;
            overFor = pull > limit ? overFor + dt : Mathf.Max(0f, overFor - dt * 2f);
            if (overFor > DerailHold)
            {
                float span = cars[0] ? cars[0].def.Half + 2f : 6f;
                var pa = path.At(head - offsets[0] + span, out var ta); var pb = path.At(head - offsets[0] - span, out var tb);
                Debug.Log($"[rail] derail {trainName}: speed {speed:0.00} pull {pull:0.00} limit {limit:0.0} head {head:0.0} path {path.Start:0.0}..{path.End:0.0} segs {path.segs.Count} line {(l != null ? l.id : -1)} ta {ta} tb {tb} pa {pa} pb {pb}");
                Derail(yawSign, Mathf.Abs(speed), (int)(head * 13f));
            }
        }

        /// <summary>Come off the rails: each car slews outward of the curve (<paramref name="outward"/> = sign of the
        /// turn) and leans over; damage by speed. <paramref name="seed"/> varies the pile-up.</summary>
        public void Derail(float outward, float atSpeed, int seed)
        {
            if (Derailed) return;
            Derailed = true; overFor = 0f;
            Derailments++; LastDerailed = this;
            int n = cars.Count;
            slew = new float[n]; lean = new float[n]; onRails = new bool[n];
            var r = new System.Random(seed);
            float side = outward >= 0f ? -1f : 1f;                                                 // a right turn throws them left
            for (int i = 0; i < n; i++)
            {
                float w = 0.6f + 0.4f * (float)r.NextDouble();
                slew[i] = side * (0.35f + 0.6f * w) * (i % 2 == 0 ? 1f : 0.55f);
                lean[i] = -side * (5f + 12f * w) * Mathf.Clamp01(atSpeed / 10f + 0.3f);
                if (cars[i]) cars[i].hp -= Mathf.Max(0f, atSpeed - 4f) * 30f;
            }
            throttle = 0f;
            var at = HeadPoint;
            MadMax.Audio.Sfx.Play("crash_big", at, 1f, 0.6f, 200f);
            MadMax.Audio.Sfx.Play("collapse", at, 0.8f, 1.2f, 150f);
            var g = WastelandGame.Instance;
            if (g && g.cameraRig && g.Player && (g.Player.transform.position - at).sqrMagnitude < 60f * 60f) g.cameraRig.Shake(Mathf.Min(8f, 2f + atSpeed * 0.4f));
            if (driven && g)
            {
                g.Toast("DERAILED! TOO FAST INTO THE CURVE - THE " + trainName + " IS OFF THE RAILS");
                Hints.Show("rerail", "A DERAILED TRAIN WON'T MOVE. WITH A JACK, OPEN THE CONTEXT MENU BESIDE IT: RE-RAIL. SCRAP TRACK TAKES CURVES SLOWER.");
            }
        }

        /// <summary>Put it back on at once (tests).</summary>
        public void SetOnRails() { Derailed = false; DerailBlend = 0f; rerailBy = null; RerailedCars = 0; }

        /// <summary>Restore the off-the-rails state from a save (no damage again, already slewed over).</summary>
        public void LoadDerailed(int seed)
        {
            var hp = new float[cars.Count];
            for (int i = 0; i < cars.Count; i++) hp[i] = cars[i] ? cars[i].hp : 0f;
            Derail(seed % 2 == 0 ? 1f : -1f, 0f, seed);
            for (int i = 0; i < cars.Count; i++) if (cars[i]) cars[i].hp = hp[i];
            DerailBlend = 1f;
        }

        /// <summary>Begin jacking it back on; the one doing it must stay beside the train.</summary>
        public bool StartRerail(Transform by)
        {
            if (!Derailed || rerailBy || Mathf.Abs(speed) > 0.3f) return false;
            rerailBy = by; rerailT = 0f; RerailedCars = 0;
            for (int i = 0; i < onRails.Length; i++) onRails[i] = false;
            return true;
        }

        /// <summary>Per frame: the cars slewing over / sliding to a stop, re-railing.</summary>
        void DerailUpdate()
        {
            float dt = Time.deltaTime;
            DerailBlend = Mathf.MoveTowards(DerailBlend, Derailed ? 1f : 0f, dt * (Derailed ? 1.4f : 0.7f));
            if (!Derailed) return;
            float sp = Mathf.Abs(speed);
            if (sp > 0.3f && Time.time >= scrapeAt)
            {
                scrapeAt = Time.time + 0.12f;
                var tt = DeformableTerrain.Instance;
                foreach (var c in cars)
                {
                    if (!c) continue;
                    var p = c.transform.position + c.transform.forward * Random.Range(-c.def.Half, c.def.Half) * 0.7f;
                    if (tt) p.y = tt.HeightNoLoad(p.x, p.z) + 0.1f;
                    Fx.Smoke(p, Vector3.up * 1.2f + Random.insideUnitSphere * 0.6f, 0.6f + sp * 0.05f, new Color(0.55f, 0.48f, 0.38f, 0.6f), 2.2f);
                    if (Random.value < 0.4f) Fx.Sparks(p, Vector3.up + Random.insideUnitSphere, 4, new Color(1f, 0.7f, 0.3f));
                }
            }
            if (sp > 0.3f) MadMax.Audio.Sfx.Loop(this, "rail_scrape", Mathf.Clamp01(sp / 6f) * 0.8f, 0.7f + sp * 0.03f, 140f);
            if (!rerailBy) return;
            var g = WastelandGame.Instance;
            var near = NearestCar(rerailBy.position);
            bool there = rerailBy.gameObject.activeInHierarchy && near && (near.transform.position - rerailBy.position).sqrMagnitude < 7f * 7f + near.def.Half * near.def.Half;
            if (!there || (g && g.Current)) { rerailBy = null; if (g) g.Toast("RE-RAILING STOPPED"); return; }
            rerailT += dt;
            if ((rerailClank -= dt) <= 0f)
            {
                rerailClank = 0.7f;
                var c = cars[Mathf.Min(RerailedCars, cars.Count - 1)];
                if (c) MadMax.Audio.Sfx.Play("ratchet", c.transform.position, 0.7f, Random.Range(0.85f, 1.05f), 30f);
            }
            int done = Mathf.Min(cars.Count, Mathf.FloorToInt(rerailT / RerailSeconds));
            while (RerailedCars < done)
            {
                if (RerailedCars < onRails.Length) onRails[RerailedCars] = true;
                var c = cars[RerailedCars];
                if (c) MadMax.Audio.Sfx.Play("hit_metal", c.transform.position, 0.9f, 0.6f, 50f);
                RerailedCars++;
                if (g && RerailedCars < cars.Count) g.Toast("RE-RAILED " + RerailedCars + " OF " + cars.Count);
            }
            if (RerailedCars >= cars.Count)
            {
                Derailed = false; rerailBy = null; speed = 0f;
                if (g) { g.Toast("THE " + trainName + " IS BACK ON THE RAILS"); g.Stats?.Practice(MadMax.RPG.Skill.Rail, 3f); }
            }
        }
    }
}
