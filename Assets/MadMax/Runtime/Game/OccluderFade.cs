using System.Collections.Generic;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Cutaway for props standing between the camera and the followed target (buildings, trees): everything
    /// above the target's head is clipped (shader _CutY), so the player stays visible and building interiors open up
    /// like a doll's house. Their shadows stay whole. Under a bunker or tunnel roof (<see cref="MadMax.World.Subterranean"/>)
    /// the whole surface around the target is cut at the same height (shader global <c>_MadMaxCut</c>, materials with
    /// <c>_WorldCut</c>: terrain, flora, world props), leaving a dark earth backdrop.</summary>
    public class OccluderFade : MonoBehaviour
    {
        public Transform target;
        public Camera cam;
        public float radius = 1.2f;
        /// <summary>Top-down views only (CameraRig): perspective views look around inside instead.</summary>
        public bool worldCut = true;
        /// <summary>0 = no cutaway (first person), 1 = only what blocks the camera's line of sight (third person),
        /// 2 = full doll's-house cutaway (isometric / tilt-shift).</summary>
        public int cutMode = 2;
        public float undergroundRadius = 26f;
        public static bool Underground { get; private set; }
        static readonly int WorldCutId = Shader.PropertyToID("_MadMaxCut");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Underground = false; Active = null; Shader.SetGlobalVector(WorldCutId, Vector4.zero); Shader.SetGlobalFloat("_MadMaxUnderFill", 0f); }
        /// <summary>The camera's cutaway (build aiming skips roofs it has clipped away).</summary>
        public static OccluderFade Active { get; private set; }
        float lastCutY = float.MaxValue;
        void OnEnable() => Active = this;
        void OnDisable() { if (Active == this) Active = null; }

        /// <summary>True when a hit lies in the clipped-away part of a cut building (the roof over the player).</summary>
        public bool Hides(Collider c, Vector3 point)
        {
            if (cut.Count == 0 || point.y < lastCutY || !c) return false;
            var d = c.GetComponentInParent<MadMax.World.DestructibleVoxels>();
            Renderer r = d ? d.GetComponent<Renderer>() : null;
            if (!r) { var p = c.GetComponentInParent<MadMax.Building.Placeable>(); if (p) r = p.GetComponent<Renderer>(); }
            return r && cut.Contains(r);
        }
        readonly HashSet<Renderer> cut = new HashSet<Renderer>(), now = new HashSet<Renderer>();
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Collider[] around = new Collider[32];
        readonly List<Renderer> restore = new List<Renderer>();
        MaterialPropertyBlock mpb;
        static readonly int CutId = Shader.PropertyToID("_CutY"), FillId = Shader.PropertyToID("_MadMaxUnderFill");
        bool subRoof;
        float fill;

        void LateUpdate()
        {
            now.Clear();
            mpb ??= new MaterialPropertyBlock();
            float cutY = 0f;
            bool under = false;
            if (target && cam && target.gameObject.activeInHierarchy && cutMode > 0)
            {
                Vector3 feet = target.position, head = feet + Vector3.up * 1f;
                cutY = feet.y + 2.3f;
                var dir = head - cam.transform.position;
                float len = dir.magnitude;
                int n = Physics.SphereCastNonAlloc(cam.transform.position, cutMode == 1 ? radius * 0.5f : radius, dir / len, hits, len - 1.2f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++) Consider(hits[i].collider, head);
                if (cutMode == 2)
                {
                    // standing inside a building: open it up even when its walls are not in the line of sight
                    int m = Physics.OverlapSphereNonAlloc(head, 0.6f, around, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < m; i++) Consider(around[i], head);
                    if (Physics.Raycast(head, Vector3.up, out var roof, 40f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Consider(roof.collider, head);
                        under = worldCut && roof.collider.GetComponentInParent<MadMax.World.Subterranean>();
                    }
                }
                if (under) Shader.SetGlobalVector(WorldCutId, new Vector4(feet.x, feet.z, undergroundRadius, cutY));
            }
            if (under != Underground)
            {
                Underground = under;
                if (!under) Shader.SetGlobalVector(WorldCutId, Vector4.zero);
            }
            // under rock or a bunker slab (any view): a faint cool fill so the dark between lamps still reads
            if (target && (Time.frameCount & 7) == 0)
                subRoof = Physics.Raycast(target.position + Vector3.up, Vector3.up, out var sr, 30f, ~0, QueryTriggerInteraction.Ignore) && sr.collider.GetComponentInParent<MadMax.World.Subterranean>();
            fill = Mathf.MoveTowards(fill, subRoof ? 1f : 0f, Time.deltaTime * 1.5f);
            Shader.SetGlobalFloat(FillId, fill);
            lastCutY = cutY;
            foreach (var r in now)
            {
                if (!r) continue;
                cut.Add(r);
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(CutId, cutY);
                r.SetPropertyBlock(mpb);
                MadMax.Rendering.HDVisual.CopyBlock(r, mpb);                       // HD model over the voxels
            }
            restore.Clear();
            foreach (var r in cut) if (!now.Contains(r)) restore.Add(r);
            foreach (var r in restore) { cut.Remove(r); if (r) { r.GetPropertyBlock(mpb); mpb.SetFloat(CutId, 100000f); r.SetPropertyBlock(mpb); MadMax.Rendering.HDVisual.CopyBlock(r, null); } }
        }

        void Consider(Collider c, Vector3 head)
        {
            if (!c || (c.attachedRigidbody && !c.attachedRigidbody.isKinematic)) return;   // loose things stay visible
            var d = c.GetComponentInParent<MadMax.World.DestructibleVoxels>();
            Renderer r = d ? d.GetComponent<Renderer>() : null;
            if (!r)
            {
                var p = c.GetComponentInParent<MadMax.Building.Placeable>();       // player-built walls and roofs
                if (p) r = p.GetComponent<Renderer>();
            }
            if (!r || r.bounds.max.y < head.y + 1.2f) return;                        // low props don't block
            now.Add(r);
        }
    }
}
