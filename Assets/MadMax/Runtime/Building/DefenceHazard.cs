using System.Collections.Generic;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Base defence that hurts what pushes into it: a spike wall (stakes wound walkers, gouge and puncture
    /// rammers) or barbed wire (snags walkers to a crawl and cuts them, wraps round tyres). Wears as it works.</summary>
    public class DefenceHazard : MonoBehaviour
    {
        public enum Kind { Spikes, Wire }
        public Kind kind;

        public static readonly List<DefenceHazard> All = new List<DefenceHazard>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        Bounds local;
        float tick, playerCd;
        readonly Dictionary<Object, float> cooldown = new Dictionary<Object, float>();

        void Start() { local = GetComponent<MeshFilter>().sharedMesh.bounds; local.Expand(new Vector3(0.1f, 0.3f, 0.5f)); }

        bool Inside(Vector3 world) => local.Contains(transform.InverseTransformPoint(world));

        /// <summary>Walking-speed multiplier at a point (barbed wire snags).</summary>
        public static float SlowAt(Vector3 p)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var h = All[i];
                if (h && h.kind == Kind.Wire && (h.transform.position - p).sqrMagnitude < 9f && h.Inside(p + Vector3.up * 0.3f)) return 0.3f;
            }
            return 1f;
        }

        bool Ready(Object who, float every)
        {
            if (cooldown.TryGetValue(who, out float t) && Time.time < t) return false;
            cooldown[who] = Time.time + every;
            return true;
        }

        void Update()
        {
            if ((tick -= Time.deltaTime) > 0f) return;
            tick = 0.15f;
            var g = MadMax.Game.WastelandGame.Instance;
            if (!g) return;
            var at = transform.position;
            bool spikes = kind == Kind.Spikes;
            // the walker on foot
            if (g.Player && !g.Current && g.Player.gameObject.activeSelf && (g.Player.transform.position - at).sqrMagnitude < 9f
                && Inside(g.Player.transform.position + Vector3.up * 0.4f) && Time.time > playerCd)
            {
                playerCd = Time.time + (spikes ? 0.9f : 1.4f);
                g.Vitals.Hurt(spikes ? 8f : 3f, "MELEE");
                MadMax.World.BloodStains.Splash(g.Player.transform.position, 0.3f);
                MadMax.Audio.Sfx.Play("scratch", g.Player.transform.position, 0.6f);
            }
            // people: raiders storming the base
            foreach (var n in Npc.Npc.All)
            {
                if (!n || !n.Alive || (n.transform.position - at).sqrMagnitude > 9f || !Inside(n.transform.position + Vector3.up * 0.4f)) continue;
                if (!Ready(n, spikes ? 0.9f : 1.2f)) continue;
                n.ApplyHit(n.transform.position + Vector3.up, transform.forward, spikes ? 0.45f : 0.12f, 0.2f, gameObject);
                if (spikes) Wear(1);
            }
            // vehicles ramming the line
            foreach (var v in g.AllVehicles)
            {
                if (!v || !v.Body || v.Body.isKinematic || (v.transform.position - at).sqrMagnitude > 64f) continue;
                var cp = v.Body.ClosestPointOnBounds(at + transform.up * 0.5f);
                if (!Inside(cp)) continue;
                float sp = v.Body.linearVelocity.magnitude;
                if (sp < 1.5f || !Ready(v, 0.8f)) continue;
                if (spikes) v.GetComponent<VehicleDamage>()?.ApplyHit(cp, v.Body.linearVelocity.normalized, sp * 0.12f, 0.35f, gameObject);
                else v.Body.linearVelocity *= 0.8f;                                                   // wire wraps the axles
                if (Random.value < (spikes ? 0.5f : 0.35f)) PopNearest(v, cp);
                MadMax.Audio.Sfx.Play(spikes ? "hit_wood" : "scratch", cp, 0.8f);
                Wear(spikes ? Mathf.CeilToInt(sp * 0.3f) : 2);
            }
        }

        void PopNearest(VehicleDriver v, Vector3 p)
        {
            WheelStats best = null; float bd = float.MaxValue;
            foreach (var w in v.GetComponentsInChildren<WheelStats>())
            {
                float d = (w.transform.position - p).sqrMagnitude;
                if (!w.Popped && d < bd) { bd = d; best = w; }
            }
            if (best && bd < 4f) { best.Pop(); MadMax.Audio.Sfx.Play("pop", p, 0.9f); }
        }

        void Wear(int blows) => GetComponent<Placeable>()?.ApplyHit(transform.position + transform.up * 0.5f, -transform.forward, blows, 0.2f, gameObject);
    }
}
