using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Placing the free frame (roadmap 29). Beams and ladders: click the start, then the end — any angle, any
    /// length up to 8 m, snapped to other beam ends (Shift: straight up or level); beams chain from the last end until
    /// [RMB]. Spans: click three to six beam ends, then the first again (or ENTER) to lay the slab.</summary>
    public partial class BuildMode
    {
        Vector3? beamStart;
        Transform frameParent;
        readonly List<Vector3> spanNodes = new List<Vector3>();
        /// <summary>The frame piece in progress (tests): the start of the beam being drawn, the span's picked ends.</summary>
        public Vector3? BeamStart => beamStart;
        public int SpanNodes => spanNodes.Count;

        void FrameTick(FurnitureDef def, RaycastHit hit, Keyboard kb, Mouse mouse, Gamepad pad)
        {
            var chassis = hit.collider.GetComponentInParent<VehicleChassis>();
            var parent = chassis ? chassis.transform : structures;
            var point = hit.point;
            bool onNode = Frame.NearestNode(point, Frame.NodeSnap, out var node);
            if (onNode) point = node;
            bool click = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame);
            bool stop = (mouse != null && mouse.rightButton.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            if (stop) { beamStart = null; spanNodes.Clear(); }
            if (def.frame == FrameKind.Span) { SpanTick(def, parent, point, onNode, click, kb != null && kb.enterKey.wasPressedThisFrame); return; }

            var kind = def.frame;
            int style = def.id.Contains("steel") ? 1 : 0;
            if (beamStart == null)
            {
                ghost.SetActive(true);
                ghostMesh.sharedMesh = Frame.BeamMesh(kind, style, 4);
                ghost.transform.SetPositionAndRotation(point, Quaternion.LookRotation(hit.normal, Mathf.Abs(hit.normal.y) > 0.9f ? Vector3.forward : Vector3.up));
                Tint(true);
                Valid = true;
                Status = "[LMB] START THE " + def.name + (onNode ? " ON THE BEAM END" : "");
                if (click) { beamStart = point; frameParent = parent; }
                return;
            }
            var a = beamStart.Value;
            var d = point - a;
            if (kb != null && kb.shiftKey.isPressed)
            {
                // straight up / down, or level
                if (Mathf.Abs(d.normalized.y) > 0.7f) d = Vector3.up * d.y; else d.y = 0f;
            }
            float len = Mathf.Min(d.magnitude, Frame.MaxBeam);
            if (len < Frame.MinBeam) { ghost.SetActive(false); Valid = false; Status = "PICK THE OTHER END  [RMB] STOP"; return; }
            var dir = d.normalized;
            var rot = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.98f ? Vector3.forward : Vector3.up);
            ghost.SetActive(true);
            ghostMesh.sharedMesh = Frame.BeamMesh(kind, style, Frame.Voxels(len));
            ghost.transform.SetPositionAndRotation(a, rot);
            var cost = FurnitureLibrary.FrameCost(def, len);
            bool afford = CanAfford(cost);
            Valid = afford;
            Tint(afford);
            Status = (afford ? "[LMB] PLACE " : "NOT ENOUGH MATERIALS FOR ") + len.ToString("0.0") + " M" + (onNode ? " TO THE BEAM END" : "") + "  [SHIFT] STRAIGHT  [RMB] STOP";
            if (!click || !afford) return;
            var placed = PlaceFrame(def, frameParent ? frameParent : parent, a, rot, cost);
            if (placed && placed.TryGetComponent<FrameBeam>(out var beam)) { beam.SetLength(len); placed.Dirty(); }
            beamStart = kind == FrameKind.Ladder ? (Vector3?)null : a + rot * new Vector3(0f, 0f, len);       // beams chain on from their end
        }

        int spanKey;
        Mesh spanPreview;

        void SpanTick(FurnitureDef def, Transform parent, Vector3 point, bool onNode, bool click, bool enter)
        {
            int style = def.id.Contains("sheet") ? 1 : def.id.Contains("grating") ? 2 : 0;
            if (spanNodes.Count == 0) frameParent = parent;
            bool closes = onNode && spanNodes.Count >= 3 && (point - spanNodes[0]).sqrMagnitude < 0.05f;
            // preview: the picked ends + the one under the cursor
            var preview = new List<Vector3>(spanNodes);
            if (onNode && !closes && !spanNodes.Contains(point)) preview.Add(point);
            if (preview.Count >= 3)
            {
                float off = Frame.SpanFrame(preview, out var origin, out var rot);
                var local = Local(preview, origin, rot);
                int key = 17;
                foreach (var p in local) key = key * 31 + Mathf.RoundToInt(p.x * 12.5f) * 7 + Mathf.RoundToInt(p.z * 12.5f);
                if (key != spanKey || !spanPreview) { if (spanPreview) Destroy(spanPreview); spanPreview = Frame.SpanMesh(style, local); spanKey = key; }
                ghost.SetActive(true);
                ghostMesh.sharedMesh = spanPreview;
                ghost.transform.SetPositionAndRotation(origin, rot);
                var cost = FurnitureLibrary.FrameCost(def, Frame.Area(local));
                bool ok = off <= 0.3f && CanAfford(cost) && Frame.Area(local) > 0.4f;
                Valid = ok; Tint(ok);
                Status = off > 0.3f ? "THE ENDS ARE NOT IN ONE PLANE" : !CanAfford(cost) ? "NOT ENOUGH MATERIALS" :
                         $"{spanNodes.Count} ENDS  {Frame.Area(local):0.0} M2  " + (spanNodes.Count >= 3 ? "[LMB] THE FIRST END / [ENTER] LAY IT" : "[LMB] NEXT END") + "  [RMB] START OVER";
            }
            else
            {
                ghost.SetActive(false);
                Valid = onNode;
                Status = onNode ? $"[LMB] PICK THIS BEAM END ({spanNodes.Count}/3+)" : "AIM AT A BEAM END  (" + spanNodes.Count + " PICKED)";
            }
            if (click && onNode && !closes && spanNodes.Count < 6 && !spanNodes.Contains(point)) { spanNodes.Add(point); MadMax.Audio.Sfx.Play2D("click", 0.5f); return; }
            if ((closes && click) || (enter && spanNodes.Count >= 3)) LaySpan(def, style);
        }

        void LaySpan(FurnitureDef def, int style)
        {
            float off = Frame.SpanFrame(spanNodes, out var origin, out var rot);
            var local = Local(spanNodes, origin, rot);
            var cost = FurnitureLibrary.FrameCost(def, Frame.Area(local));
            if (off > 0.3f || !CanAfford(cost) || Frame.Area(local) <= 0.4f) { game.Toast(off > 0.3f ? "THE ENDS ARE NOT IN ONE PLANE" : "NOT ENOUGH MATERIALS"); return; }
            var placed = PlaceFrame(def, frameParent ? frameParent : structures, origin, rot, cost);
            if (placed && placed.TryGetComponent<FrameSpan>(out var span)) { span.style = style; span.SetCorners(local); placed.Dirty(); }
            spanNodes.Clear();
        }

        static List<Vector3> Local(IReadOnlyList<Vector3> world, Vector3 origin, Quaternion rot)
        {
            var inv = Quaternion.Inverse(rot);
            var l = new List<Vector3>(world.Count);
            foreach (var p in world) { var q = inv * (p - origin); q.y = 0f; l.Add(q); }
            return l;
        }

        bool CanAfford((ResourceType type, int amount)[] cost)
        {
            foreach (var (t, c) in cost) if (game.Inventory.Get(t) < Cost(c)) return false;
            return true;
        }

        Placeable PlaceFrame(FurnitureDef def, Transform parent, Vector3 worldPos, Quaternion worldRot, (ResourceType type, int amount)[] cost)
        {
            using (Inventory.Source(null, "BUILT"))
                foreach (var (t, c) in cost) game.Inventory.TrySpend(t, Cost(c));
            game.Stats.Practice(MadMax.RPG.Skill.Construction, 4f);
            var placed = FurnitureLibrary.Spawn(def.id, parent, parent.InverseTransformPoint(worldPos), Quaternion.Inverse(parent.rotation) * worldRot, material);
            if (!placed) return null;
            placed.owner = game.Stats.name; _ = placed.Id;
            MadMax.Audio.Sfx.Play("hammer", placed.transform.position, 0.7f);
            MadMax.Net.NetSession.Instance?.SendPlaced(placed);
            return placed;
        }

        void Tint(bool ok)
        {
            mpb.SetColor("_Tint", ok ? new Color(0.7f, 1.3f, 0.7f) : new Color(1.4f, 0.5f, 0.45f));
            ghostRenderer.SetPropertyBlock(mpb);
        }

        /// <summary>Automation: place a beam / ladder from a to b, or a span over world corners (costs paid), as the
        /// clicks would.</summary>
        public Placeable PlaceBeam(string id, Vector3 a, Vector3 b)
        {
            var def = FurnitureLibrary.Get(id);
            if (def == null || def.frame == FrameKind.None || def.frame == FrameKind.Span) return null;
            var d = b - a; float len = Mathf.Min(d.magnitude, Frame.MaxBeam);
            var rot = Quaternion.LookRotation(d.normalized, Mathf.Abs(d.normalized.y) > 0.98f ? Vector3.forward : Vector3.up);
            var cost = FurnitureLibrary.FrameCost(def, len);
            if (!CanAfford(cost)) return null;
            var p = PlaceFrame(def, structures, a, rot, cost);
            if (p && p.TryGetComponent<FrameBeam>(out var beam)) { beam.SetLength(len); p.Dirty(); }
            return p;
        }

        public Placeable PlaceSpan(string id, IReadOnlyList<Vector3> corners)
        {
            var def = FurnitureLibrary.Get(id);
            if (def == null || def.frame != FrameKind.Span) return null;
            spanNodes.Clear(); spanNodes.AddRange(corners); frameParent = structures;
            int before = Placeable.All.Count;
            LaySpan(def, def.id.Contains("sheet") ? 1 : def.id.Contains("grating") ? 2 : 0);
            return Placeable.All.Count > before ? Placeable.All[Placeable.All.Count - 1] : null;
        }
    }
}
