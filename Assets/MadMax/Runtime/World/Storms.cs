using System.Collections.Generic;
using MadMax.Game;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>Storms (roadmap 17). Dust storms roll over the desert: a front builds up, blows for a few minutes and
    /// passes (sand streaming on the wind, visibility gone, sandblasting the uncovered face, raiders half-blind, ruts
    /// and tracks drifted over). Radiation storms flare in the fallout zones (green motes, a heavy dose outdoors — get
    /// under a roof or into a hazmat suit). Lightning (<see cref="Strike"/>) seeks tall things, sets dry ground and
    /// trees alight, electrifies metal near it — the cab keeps you safe — and can strike a walker down.
    /// Read by Atmosphere (fog, grade), WastelandGame (radiation, sandblast), Convoy (sight) and the HUD.</summary>
    public class Storms : MonoBehaviour
    {
        public static float Dust { get; private set; }
        public static float Rad { get; private set; }
        public static string Name => Rad > 0.15f ? "RAD STORM" : Dust > 0.15f ? "DUST STORM" : null;
        public static Storms Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { Dust = Rad = 0f; electrified.Clear(); Instance = null; }

        void Awake() => Instance = this;

        /// <summary>The storm in progress (kind 0 = none) for the save.</summary>
        public void SaveState(out int k, out float left, out float total) { k = kind; left = kind != 0 ? end - Time.time : 0f; total = end - start; }

        /// <summary>Resume a saved storm where it was (no warning toast; it is already blowing).</summary>
        public void Restore(int k, float left, float total)
        {
            if (k == 0 || left <= 1f) return;
            kind = k; end = Time.time + left; start = end - Mathf.Max(total, left);
            float envelope = Mathf.Clamp01(Mathf.Min((Time.time - start) / 45f, left / 60f));
            if (k == 1) Dust = envelope; else Rad = envelope;
        }

        int kind;                    // 0 none, 1 dust, 2 radiation
        float start, end, nextCheck = 90f, sandblastToast;
        static readonly List<(Transform t, float until)> electrified = new List<(Transform, float)>();
        static readonly HashSet<MadMax.Items.IDamageable> hitOnce = new HashSet<MadMax.Items.IDamageable>();

        void Update()
        {
            var g = WastelandGame.Instance;
            var t = DeformableTerrain.Instance;
            if (!g || !g.Player || !t || !g.Ready) return;
            float dt = Time.deltaTime;
            var focus = g.Current ? g.Current.transform.position : g.Player.transform.position;
            var biome = t.BiomeAt(focus.x, focus.z);
            float freq = Weather.Frequency switch { 0 => 0f, 1 => 0.5f, 3 => 1.6f, _ => 1f };
            bool authority = !(MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient);
            if (authority && kind == 0 && (nextCheck -= dt) <= 0f)
            {
                nextCheck = Random.Range(120f, 300f);
                if (biome == Biome.Desert && !Weather.Raining && Weather.Temperature > 12f && Random.value < 0.35f * freq) Begin(g, 1, Random.Range(180f, 360f));
                else if (biome == Biome.Nuclear && Random.value < 0.3f * freq) Begin(g, 2, Random.Range(150f, 300f));
            }
            // the front: builds up over ~45 s, blows, passes in the last minute
            float envelope = 0f;
            if (kind != 0)
            {
                float left = end - Time.time;
                if (left <= 0f) kind = 0;
                else envelope = Mathf.Clamp01(Mathf.Min((Time.time - start) / 45f, left / 60f));
            }
            // it covers its own country: leave the desert and the dust thins out
            float inside = kind == 1 ? (biome == Biome.Desert ? 1f : biome == Biome.Town || biome == Biome.Village || biome == Biome.City ? 0.5f : 0.15f)
                         : kind == 2 ? (biome == Biome.Nuclear ? 1f : 0.2f) : 0f;
            Dust = Mathf.MoveTowards(Dust, kind == 1 ? envelope * inside : 0f, dt * 0.08f);
            Rad = Mathf.MoveTowards(Rad, kind == 2 ? envelope * inside : 0f, dt * 0.08f);
            Effects(g, focus, dt);
            for (int i = electrified.Count - 1; i >= 0; i--)
            {
                var (tr, until) = electrified[i];
                if (!tr || Time.time > until) { electrified.RemoveAt(i); continue; }
                if (Random.value < dt * 12f && tr.TryGetComponent<Rigidbody>(out var rb))
                    Fx.Sparks(rb.worldCenterOfMass + Vector3.Scale(Random.insideUnitSphere, new Vector3(1f, 0.7f, 2f)), Vector3.up, 3, new Color(0.6f, 0.8f, 1f));
                if (!g.Current && (g.Player.transform.position - tr.position).sqrMagnitude < 3.5f * 3.5f && g.Vitals && Random.value < dt * 2f)
                {
                    g.Vitals.Hurt(12f, "SHOCK");
                    g.Toast("THE METAL IS LIVE!");
                    electrified.RemoveAt(i);
                }
            }
        }

        void Begin(WastelandGame g, int k, float seconds)
        {
            kind = k; start = Time.time; end = Time.time + seconds;
            MadMax.Net.NetSession.Instance?.SendWeather();                                       // clients get the storm with the weather
            g.Toast(k == 1 ? "A DUST STORM IS ROLLING IN - COVER YOUR FACE" : "RADIATION STORM! GET UNDER A ROOF");
            MadMax.Audio.Sfx.Play2D(k == 1 ? "wind" : "static", 0.5f, 0.8f);
        }

        void Effects(WastelandGame g, Vector3 focus, float dt)
        {
            if (Application.isBatchMode) return;
            var wind = Fx.Wind.sqrMagnitude > 0.01f ? Fx.Wind.normalized : Vector3.right;
            // sand streaming past on the wind (~150 streaks a second at full blow) and rolling drifts of dust low on the ground
            int n = Mathf.FloorToInt(Dust * 150f * dt + Random.value);
            for (int i = 0; i < n && Dust > 0.02f; i++)
            {
                var p = focus + new Vector3(Random.Range(-24f, 24f), Random.Range(0.2f, 5f), Random.Range(-24f, 24f)) - wind * 10f;
                float shade = Random.Range(0.85f, 1.1f);
                Fx.Streak(p, wind * Random.Range(14f, 22f) + Vector3.up * Random.Range(-0.5f, 1f), Random.Range(0.05f, 0.09f), new Color(0.86f * shade, 0.66f * shade, 0.44f * shade, 0.75f), 1.3f);
            }
            int m = Mathf.FloorToInt(Dust * 10f * dt + Random.value);
            for (int i = 0; i < m && Dust > 0.02f; i++)
            {
                var p = focus + new Vector3(Random.Range(-26f, 26f), Random.Range(0.1f, 1.2f), Random.Range(-26f, 26f)) - wind * 14f;
                Fx.Smoke(p, wind * Random.Range(8f, 12f), Random.Range(2.5f, 4.5f), new Color(0.78f, 0.6f, 0.4f, 0.22f * Dust + 0.05f), 3f);
            }
            if (Rad > 0.02f && Random.value < Rad * dt * 25f)
            {
                var p = focus + new Vector3(Random.Range(-15f, 15f), Random.Range(0.5f, 5f), Random.Range(-15f, 15f));
                Fx.Smoke(p, wind * 2f + Vector3.up * 0.3f, 0.25f, new Color(0.55f, 1f, 0.35f, 0.7f), 3f);
            }
            // sandblasting: an uncovered face out in it
            bool sheltered = g.Current || g.Player.Interior || g.Sheltered;
            if (Dust > 0.3f && !sheltered && !g.FaceCovered && g.Vitals)
            {
                g.Vitals.Hurt(0.35f * Dust * dt, "SANDBLASTED");
                if (Time.time > sandblastToast) { sandblastToast = Time.time + 20f; g.Toast("SAND IN YOUR EYES AND LUNGS - COVER YOUR FACE"); }
            }
            // vehicles out in it get dusty
            if (Dust > 0.3f && g.Current && g.Current.TryGetComponent<MadMax.Vehicles.VehicleGrime>(out var grime)) grime.dirt = Mathf.Min(0.6f, grime.dirt + Dust * dt * 0.004f);
        }

        // ------------------------------------------------------------------ lightning
        /// <summary>Where lightning comes down near a focus: now and then close, and it seeks tall things.</summary>
        public static Vector3 PickStrike(Vector3 focus)
        {
            var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            var p = focus + dir * (Random.value < 0.06f ? Random.Range(8f, 30f) : Random.Range(40f, 180f));
            var t = DeformableTerrain.Instance;
            if (t) p.y = t.Height(p.x, p.z);
            float best = p.y + 1f;
            foreach (var c in Physics.OverlapSphere(p, 14f, ~0, QueryTriggerInteraction.Ignore))
            {
                var top = c.bounds.max.y;
                if (top > best + 2f) { best = top; p = new Vector3(c.bounds.center.x, top, c.bounds.center.z); }
            }
            return p;
        }

        /// <summary>A strike at a point: fire, splinters, live metal around it, and woe to anyone right there.</summary>
        public static void Strike(Vector3 at)
        {
            var g = WastelandGame.Instance;
            Fx.Flash(at + Vector3.up * 2f, new Color(0.75f, 0.85f, 1f), 45f, 14f, 0.2f);
            Fx.Sparks(at, Vector3.up, 25, new Color(0.7f, 0.85f, 1f));
            MadMax.Npc.NpcDirector.Instance?.Noise(at, 150f, false);
            var t = DeformableTerrain.Instance;
            // what it hits burns or splinters
            bool lit = false;
            hitOnce.Clear();
            foreach (var c in Physics.OverlapSphere(at, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.GetComponentInParent<PlayerCharacter>()) continue;                    // the walker is handled below
                var d = c.GetComponentInParent<MadMax.Items.IDamageable>();
                if (d != null && !(d is MadMax.Vehicles.VehicleDamage) && hitOnce.Add(d)) d.ApplyHit(at, Vector3.down, 3f, 0.6f, null);
                if (!lit && Fire.Flammable(c.transform.root.gameObject)) { Fire.Ignite(at, c.transform, 40f, 1f); lit = true; }
            }
            if (!lit && t && (t.BiomeAt(at.x, at.z) is Biome.Forest or Biome.Tropical or Biome.Village) && Weather.Wetness < 0.8f)
                Fire.Ignite(new Vector3(at.x, t.Height(at.x, at.z), at.z), null, 15f, 0.6f, true);
            // metal around it goes live for a few seconds
            if (g)
                foreach (var v in g.AllVehicles)
                {
                    if (!v || (v.transform.position - at).sqrMagnitude > 12f * 12f) continue;
                    electrified.Add((v.transform, Time.time + 4f));
                    if (v == g.Current) g.Toast("LIGHTNING! THE CAB KEPT YOU SAFE");
                }
            // right on top of a walker
            if (g && g.Player && !g.Current && g.Vitals && (g.Player.transform.position - at).sqrMagnitude < 3.5f * 3.5f)
            {
                g.Vitals.Hurt(40f, "BURNED");
                g.Toast("STRUCK BY LIGHTNING!");
            }
            if (g && g.Player && (g.Player.transform.position - at).sqrMagnitude < 60f * 60f)
                MadMax.Audio.Sfx.Play("thunder", at, 1f, 1.1f, 300f);
        }
    }
}
