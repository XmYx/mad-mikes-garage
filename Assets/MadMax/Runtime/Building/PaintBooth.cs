using System.Collections.Generic;
using MadMax.Game;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Paint station (roadmap 19): park a vehicle beside it and [E] opens the paint shop — a new body colour
    /// (dyes, or scrap and oil for the mixed shades) and a decal for the flanks. Paid, the page closes on a real job
    /// (2026-10-28): the gun works down each flank and over the roof for <see cref="SpraySeconds"/> (a hiss, drifting
    /// overspray in the new colour, a little settling on the ground), then the paint stays wet for
    /// <see cref="WetSeconds"/> (<see cref="WetPaint"/>: driving off on dusty ground picks up grit).</summary>
    public class PaintBooth : MonoBehaviour, IInteractable
    {
        public float reach = 8f;
        public const float SpraySeconds = 6f, WetSeconds = 90f;
        public static readonly List<PaintBooth> All = new List<PaintBooth>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { All.Clear(); Sprays = 0; }
        void OnEnable() => All.Add(this);
        void OnDisable() { All.Remove(this); MadMax.Audio.Sfx.Loop(this, "spray", 0f, 1f, 18f); }

        /// <summary>Spray jobs finished (tests).</summary>
        public static int Sprays;
        VehicleDriver target, queued;
        int colour, queuedColour;
        float sprayT = -1f, puffT;
        Bounds body;

        /// <summary>A spray job is running.</summary>
        public bool Spraying => sprayT >= 0f;
        public float SprayProgress => sprayT < 0f ? 0f : Mathf.Clamp01(sprayT / SpraySeconds);

        public string Prompt(WastelandGame g)
        {
            if (Spraying) return "SPRAYING THE " + WastelandGame.Name(target);
            var v = Nearest(g);
            return v ? "[E] PAINT THE " + WastelandGame.Name(v) : "PAINT STATION: PARK A VEHICLE HERE";
        }

        public void Use(WastelandGame g, bool secondary)
        {
            if (secondary || Spraying) return;
            var v = Nearest(g);
            if (!v) { g.Toast("PARK A VEHICLE BY THE PAINT STATION"); return; }
            g.Menus.OpenPaint(VehiclePaint.Of(v));
        }

        /// <summary>The booth beside a vehicle being painted, null when none in reach.</summary>
        public static PaintBooth For(VehicleDriver v)
        {
            if (!v) return null;
            PaintBooth best = null; float bd = float.MaxValue;
            foreach (var b in All)
            {
                if (!b) continue;
                float d = (b.transform.position - v.transform.position).sqrMagnitude;
                if (d < b.reach * b.reach * 1.5f && d < bd) { bd = d; best = b; }
            }
            return best;
        }

        /// <summary>Paid for: spray it once the page is closed (the menu pauses time).</summary>
        public void Queue(VehicleDriver v, int newColour) { queued = v; queuedColour = newColour; }

        void Update()
        {
            var g = WastelandGame.Instance;
            if (queued && !Spraying && !(g && g.Menus && g.Menus.IsOpen)) Begin(queued, queuedColour);
            if (!Spraying) return;
            if (!target) { End(false); return; }
            float dt = Time.deltaTime;
            sprayT += dt;
            float u = SprayProgress;
            // the gun's path: down the near flank, over the roof, down the far flank (front to back each pass)
            int pass = Mathf.Min(2, (int)(u * 3f));
            float along = (u * 3f) - pass;
            float side = pass == 0 ? 1f : pass == 2 ? -1f : 0f;
            var near = transform.InverseTransformPoint(target.transform.position).x < 0f ? 1f : -1f;   // start on the booth's side
            side *= near;
            var local = new Vector3(body.center.x + side * (body.extents.x + 0.25f), pass == 1 ? body.max.y + 0.3f : body.center.y + Mathf.Sin(sprayT * 9f) * body.extents.y * 0.4f,
                                    Mathf.Lerp(body.max.z, body.min.z, along));
            var at = target.transform.TransformPoint(local);
            var inward = target.transform.TransformDirection(pass == 1 ? Vector3.down : new Vector3(-side, 0f, 0f));
            var c = Mist(colour);
            if ((puffT -= dt) <= 0f)
            {
                puffT = 0.05f;
                MadMax.World.Fx.Smoke(at, inward * 1.6f + Random.insideUnitSphere * 0.3f, 0.12f, c, 1.1f);
                var wind = MadMax.World.Fx.Wind; wind.y = 0f;
                MadMax.World.Fx.Smoke(at + inward * 0.4f, wind * 0.4f + Vector3.up * 0.15f + Random.insideUnitSphere * 0.2f, 0.25f, new Color(c.r, c.g, c.b, 0.35f), 3f);   // overspray drifting off
            }
            MadMax.Audio.Sfx.Loop(this, "spray", 0.45f, 1f + 0.05f * Mathf.Sin(sprayT * 3f), 18f);
            if (sprayT >= SpraySeconds) End(true);
        }

        void Begin(VehicleDriver v, int c)
        {
            queued = null;
            target = v; colour = c; sprayT = 0f; puffT = 0f;
            var b = v.transform.Find("Body");
            var mf = b ? b.GetComponent<MeshFilter>() : null;
            body = mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(1.8f, 1.4f, 4.2f));
            MadMax.Audio.Sfx.Play("click", transform.position + Vector3.up, 0.6f, 0.8f, 12f);
        }

        void End(bool done)
        {
            MadMax.Audio.Sfx.Loop(this, "spray", 0f, 1f, 18f);
            sprayT = -1f;
            if (done && target)
            {
                Sprays++;
                WetPaint.Mark(target, WetSeconds);
                var g = WastelandGame.Instance;
                if (g) g.Toast("THE PAINT IS WET: GIVE IT A MINUTE BEFORE THE DUST");
            }
            target = null;
        }

        static Color Mist(int c)
        {
            if (c <= 0) return new Color(0.55f, 0.55f, 0.55f, 0.6f);
            var r = VehiclePaint.Ramp(c);
            Color k = r != null && r.Length > 2 ? (Color)r[2] : Color.grey;
            k.a = 0.6f;
            return k;
        }

        VehicleDriver Nearest(WastelandGame g)
        {
            VehicleDriver best = null; float bd = reach * reach;
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.driveable || v.aiDriven) continue;
                float d = (v.transform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }
    }
}
