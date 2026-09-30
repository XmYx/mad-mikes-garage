using System.Collections.Generic;
using MadMax.Game;
using UnityEngine;

namespace MadMax.Building
{
    /// <summary>Watchtower: [E] climbs the ladder to the platform, a standing perch <see cref="FloorY"/> m up (hand tools
    /// and guns work from there, binoculars too); [F] climbs down at the ladder foot. Up there the view reaches further
    /// (the zoom-out limits of every camera grow while the lookout is up) and hostiles within <see cref="SpotRange"/> m
    /// are called out with their distance and bearing.</summary>
    public class Watchtower : MonoBehaviour, IInteractable
    {
        public const float FloorY = 4.44f, SpotRange = 180f;
        public Vector3 ladderFoot = new Vector3(0f, 0f, 1.9f);
        Seat perch;
        float spotT;
        readonly Dictionary<MadMax.Npc.Npc, float> seen = new Dictionary<MadMax.Npc.Npc, float>();

        static Watchtower lookout;
        static Vector4 savedRanges;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => lookout = null;

        public bool Up(WastelandGame g) => g && g.Player && perch && g.Player.Sitting && g.Player.SeatedOn == perch;

        public string Prompt(WastelandGame g) => Up(g) ? null : "[E] CLIMB THE WATCHTOWER";

        public void Use(WastelandGame g, bool secondary)
        {
            if (secondary || !g.Player || Up(g)) return;
            if (!perch) { perch = gameObject.AddComponent<Seat>(); perch.standing = perch.hidden = true; perch.rest = 1f; perch.reading = 1f; perch.hasExit = true; }
            var foot = transform.TransformPoint(ladderFoot);
            var terrain = MadMax.World.DeformableTerrain.Instance;
            if (terrain) foot.y = Mathf.Max(foot.y, terrain.Height(foot.x, foot.z)) + 0.05f;
            perch.exitLocal = transform.InverseTransformPoint(foot);
            float hips = 0.94f * g.Player.Rig.appearance.height;
            MadMax.Audio.Sfx.Play("hit_wood", transform.position + Vector3.up * 2f, 0.4f, 1.3f, 20f);
            g.Player.SitOn(perch, new Vector3(0f, FloorY + hips, 0.3f));
        }

        void Update()
        {
            var g = WastelandGame.Instance;
            bool up = Up(g);
            if (up && lookout != this) Widen(g);
            else if (!up && lookout == this) Restore(g);
            if (up && (spotT -= Time.deltaTime) <= 0f) { spotT = 2f; Spot(g); }
        }

        void OnDisable() { if (lookout == this) Restore(WastelandGame.Instance); }

        /// <summary>Let every camera zoom out further while the lookout is up (and back to where it was).</summary>
        void Widen(WastelandGame g)
        {
            if (lookout) lookout.Restore(g);
            var rig = g ? g.cameraRig : null;
            if (!rig) return;
            lookout = this;
            savedRanges = new Vector4(rig.isoSizeRange.y, rig.topSizeRange.y, rig.tiltDistanceRange.y, rig.thirdDistanceRange.y);
            rig.isoSizeRange.y *= 1.8f; rig.topSizeRange.y *= 1.6f; rig.tiltDistanceRange.y *= 1.4f; rig.thirdDistanceRange.y *= 1.5f;
            rig.isoSize = Mathf.Min(rig.isoSize * 1.5f, rig.isoSizeRange.y);
            rig.topSize = Mathf.Min(rig.topSize * 1.4f, rig.topSizeRange.y);
            g.Toast("ON WATCH: THE VIEW REACHES FURTHER");
        }

        void Restore(WastelandGame g)
        {
            if (lookout != this) return;
            lookout = null;
            var rig = g ? g.cameraRig : null;
            if (!rig) return;
            rig.isoSizeRange.y = savedRanges.x; rig.topSizeRange.y = savedRanges.y; rig.tiltDistanceRange.y = savedRanges.z; rig.thirdDistanceRange.y = savedRanges.w;
            rig.isoSize = Mathf.Min(rig.isoSize, rig.isoSizeRange.y); rig.topSize = Mathf.Min(rig.topSize, rig.topSizeRange.y);
            rig.tiltDistance = Mathf.Min(rig.tiltDistance, rig.tiltDistanceRange.y); rig.thirdDistance = Mathf.Min(rig.thirdDistance, rig.thirdDistanceRange.y);
        }

        /// <summary>Call out hostiles the lookout hasn't reported in the last minute.</summary>
        void Spot(WastelandGame g)
        {
            int fresh = 0; float nearest = float.MaxValue; Vector3 toward = Vector3.zero;
            var me = transform.position;
            foreach (var n in MadMax.Npc.Npc.All)
            {
                if (!n || !n.Alive || !(n.Hostile || n.raiding)) continue;
                var d = n.transform.position - me; d.y = 0f;
                float dist = d.magnitude;
                if (dist > SpotRange) continue;
                if (seen.TryGetValue(n, out float at) && Time.time - at < 60f) continue;
                seen[n] = Time.time;
                fresh++;
                if (dist < nearest) { nearest = dist; toward = d; }
            }
            if (fresh == 0) return;
            if (seen.Count > 64) seen.Clear();
            g.Toast("LOOKOUT: " + fresh + (fresh == 1 ? " HOSTILE " : " HOSTILES ") + Mathf.RoundToInt(nearest) + " M " + Bearing(toward));
            MadMax.Audio.Sfx.Play2D("click", 0.5f);
        }

        static readonly string[] Compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        static string Bearing(Vector3 d) => Compass[Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f) / 45f) % 8];
    }
}
