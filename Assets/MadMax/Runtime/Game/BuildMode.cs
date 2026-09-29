using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>On-foot building: place furniture/panels on any floor, wall or ceiling — world, structures or vehicles
    /// (pieces on a vehicle ride with it). B toggle · 1-0 select · Y rotate · LMB place. Costs come from the Inventory.</summary>
    public class BuildMode : MonoBehaviour
    {
        public float reach = 4f;
        public float grid = 0.08f;

        public bool Active { get; private set; }
        public bool RadialOpen { get; private set; }
        public int RadialHover { get; private set; } = -1;
        float bHeld;

        public void Select(string id) { var d = FurnitureLibrary.Get(id); if (d == null) return; SetCategory(d.category); var all = Pieces; for (int i = 0; i < all.Count; i++) if (all[i].id == id) Selected = i; }
        public int Selected { get; private set; }
        public bool Valid { get; private set; }
        public string Status { get; private set; }

        WastelandGame game;
        CameraRig rig;
        Material material;
        GameObject ghost;
        MeshFilter ghostMesh;
        MeshRenderer ghostRenderer;
        MaterialPropertyBlock mpb;
        Transform structures;
        int yawSteps;
        Transform aimParent;
        Vector3 aimPos;
        Quaternion aimRot;
        readonly RaycastHit[] hits = new RaycastHit[16];

        public void Init(WastelandGame g, CameraRig r, Material mat)
        {
            game = g; rig = r; material = mat;
            structures = new GameObject("Structures").transform;
            ghost = new GameObject("BuildGhost", typeof(MeshFilter), typeof(MeshRenderer));
            ghostMesh = ghost.GetComponent<MeshFilter>();
            ghostRenderer = ghost.GetComponent<MeshRenderer>();
            ghostRenderer.sharedMaterial = mat;
            ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mpb = new MaterialPropertyBlock();
            ghost.SetActive(false);
        }

        public void SetActive(bool on)
        {
            Active = on;
            if (ghost) ghost.SetActive(false);
        }

        public BuildCategory Category { get; private set; }
        public List<FurnitureDef> Pieces => FurnitureLibrary.InCategory(Category);
        public FurnitureDef Current { get { var p = Pieces; return p[Mathf.Clamp(Selected, 0, p.Count - 1)]; } }
        public static readonly BuildCategory[] Categories = { BuildCategory.Structure, BuildCategory.Furniture, BuildCategory.Utility, BuildCategory.Garden, BuildCategory.Industry, BuildCategory.Decor };
        public int RadialCategory { get; private set; }
        UtilityNode linkStart;
        public UtilityNode LinkStart => linkStart;

        public bool Affordable(FurnitureDef def)
        {
            if (def.needsItem != null && game.Inventory.GetItem(def.needsItem) <= 0) return false;
            if (def.kit != null) return game.Inventory.GetItem(def.kit) > 0;
            foreach (var (t, n) in def.cost) if (game.Inventory.Get(t) < Cost(n)) return false;
            return true;
        }

        public void SetCategory(BuildCategory c) { if (Category != c) { Category = c; Selected = 0; linkStart = null; } }

        public Transform Structures => structures;

        /// <summary>Material cost after the builder's Construction skill.</summary>
        public int Cost(int n) => MadMax.RPG.CharacterStats.Cost(n, game.Stats.BuildCostMult);

        public void Tick(Keyboard kb, Mouse mouse, Gamepad pad)
        {
            if (pad != null && pad.dpad.left.wasPressedThisFrame) SetActive(!Active);
            if (kb != null && game.Player && !game.Current)
            {
                // tap B toggles, hold B opens the radial picker (release on a slice to build it)
                if (kb.bKey.wasPressedThisFrame) { bHeld = 0f; RadialHover = -1; }
                if (kb.bKey.isPressed)
                {
                    bHeld += Time.unscaledDeltaTime;
                    if (bHeld > 0.22f) RadialOpen = true;
                    if (RadialOpen && Mouse.current != null)
                    {
                        // inner ring: categories; outer ring: the pieces of the last hovered category
                        var d = Mouse.current.position.ReadValue() - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                        float r = d.magnitude / Screen.height;
                        float a = (Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + 360f) % 360f;
                        if (r > 0.05f && r < 0.2f)
                        {
                            int nc = Categories.Length;
                            RadialCategory = Mathf.Clamp(Mathf.FloorToInt(((a + 180f / nc) % 360f) / (360f / nc)), 0, nc - 1);
                            RadialHover = -1;
                        }
                        else if (r >= 0.2f)
                        {
                            int cnt = FurnitureLibrary.InCategory(Categories[RadialCategory]).Count;
                            RadialHover = Mathf.Clamp(Mathf.FloorToInt(((a + 180f / cnt) % 360f) / (360f / cnt)), 0, cnt - 1);
                        }
                    }
                }
                if (kb.bKey.wasReleasedThisFrame)
                {
                    if (RadialOpen) { SetCategory(Categories[RadialCategory]); if (RadialHover >= 0) Selected = RadialHover; SetActive(true); RadialOpen = false; }
                    else SetActive(!Active);
                }
                if (RadialOpen) { if (ghost) ghost.SetActive(false); return; }
            }
            if (!Active || !game.Player || !game.Player.gameObject.activeSelf) { if (ghost) ghost.SetActive(false); return; }

            var all = Pieces;
            if (kb != null)
            {
                int ci = System.Array.IndexOf(Categories, Category);
                if (kb.periodKey.wasPressedThisFrame) SetCategory(Categories[(ci + 1) % Categories.Length]);
                if (kb.commaKey.wasPressedThisFrame) SetCategory(Categories[(ci + Categories.Length - 1) % Categories.Length]);
                all = Pieces;
                for (int i = 0; i < 10; i++)
                {
                    var key = i == 9 ? Key.Digit0 : Key.Digit1 + i;
                    if (kb[key].wasPressedThisFrame) Selected = Mathf.Min(i, all.Count - 1);
                }
                if (kb.pageDownKey.wasPressedThisFrame || kb.rightBracketKey.wasPressedThisFrame) Selected = (Selected + 1) % all.Count;
                if (kb.pageUpKey.wasPressedThisFrame || kb.leftBracketKey.wasPressedThisFrame) Selected = (Selected + all.Count - 1) % all.Count;
                if (kb.yKey.wasPressedThisFrame) yawSteps = (yawSteps + 1) % 4;
            }
            if (pad != null && pad.dpad.up.wasPressedThisFrame) Selected = (Selected + 1) % all.Count;
            var def = Current;
            var player = game.Player;
            if (!player.Tool || player.Tool.id != ItemIds.ClawHammer)
            {
                ghost.SetActive(false); Valid = false;
                Status = game.Inventory.GetItem(ItemIds.ClawHammer) > 0 ? "EQUIP THE CLAW HAMMER" : "CRAFT A CLAW HAMMER TO BUILD";
                return;
            }

            if (!Aim(out var hit))
            {
                ghost.SetActive(false);
                Valid = false;
                Status = "AIM AT A SURFACE";
                return;
            }

            // parent: the vehicle hit (pieces ride along) or the world structures root
            // dismantle what the crosshair / cursor is on
            var targetPiece = hit.collider.GetComponentInParent<Placeable>();
            if (def.link != UtilityKind.None) { LinkTick(def, targetPiece, kb, mouse); return; }
            if (targetPiece && kb != null && kb.xKey.wasPressedThisFrame) { Dismantle(targetPiece); return; }

            var chassis = hit.collider.GetComponentInParent<VehicleChassis>();
            Transform parent = chassis ? chassis.transform : structures;
            var n = hit.normal;
            var localN = parent.InverseTransformDirection(n);
            bool axisAligned = Mathf.Max(Mathf.Abs(localN.x), Mathf.Max(Mathf.Abs(localN.y), Mathf.Abs(localN.z))) > 0.95f;

            var camFwd = rig.pixel.transform.forward;
            var fwd = Vector3.ProjectOnPlane(-camFwd, n);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(parent.forward, n);
            var rot = Quaternion.AngleAxis(yawSteps * 90f, n) * Quaternion.LookRotation(fwd.normalized, n);
            var localRot = Quaternion.Inverse(parent.rotation) * rot;
            var localPos = parent.InverseTransformPoint(hit.point);
            if (axisAligned)
            {
                var e = localRot.eulerAngles;
                localRot = Quaternion.Euler(Mathf.Round(e.x / 90f) * 90f, Mathf.Round(e.y / 90f) * 90f, Mathf.Round(e.z / 90f) * 90f);
                var an = new Vector3(Mathf.Abs(localN.x), Mathf.Abs(localN.y), Mathf.Abs(localN.z));
                if (an.x < 0.9f) localPos.x = Mathf.Round(localPos.x / grid) * grid;
                if (an.y < 0.9f) localPos.y = Mathf.Round(localPos.y / grid) * grid;
                if (an.z < 0.9f) localPos.z = Mathf.Round(localPos.z / grid) * grid;
            }

            ghost.SetActive(true);
            ghostMesh.sharedMesh = def.mesh;
            ghost.transform.SetPositionAndRotation(parent.TransformPoint(localPos), parent.rotation * localRot);

            bool affordable = Affordable(def);
            bool free = IsFree(def, hit.collider);
            Valid = affordable && free;
            Status = !affordable ? "NOT ENOUGH MATERIALS" : !free ? "BLOCKED" : targetPiece ? "[LMB] PLACE  [X] DISMANTLE  [Y] ROTATE" : "[LMB] PLACE  [Y] ROTATE  [B] EXIT";
            mpb.SetColor("_Tint", Valid ? new Color(0.7f, 1.3f, 0.7f) : new Color(1.4f, 0.5f, 0.45f));
            ghostRenderer.SetPropertyBlock(mpb);

            // doors snap into doorways
            if (def.id.StartsWith("door_") && targetPiece && targetPiece.id.StartsWith("doorway"))
            {
                parent = targetPiece.transform.parent; localPos = targetPiece.transform.localPosition; localRot = targetPiece.transform.localRotation;
                ghost.transform.SetPositionAndRotation(parent.TransformPoint(localPos), parent.rotation * localRot);
                Valid = affordable; Status = affordable ? "[LMB] HANG DOOR" : "NOT ENOUGH MATERIALS";
            }
            aimParent = parent; aimPos = localPos; aimRot = localRot;
            bool place = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame);
            if (place) TryPlace();
        }

        /// <summary>Place the current piece at the last valid aim (pays the cost).</summary>
        public Placeable TryPlace()
        {
            if (!Valid || !aimParent) return null;
            var def = Current;
            if (def.kit != null) game.Inventory.TakeItem(def.kit);
            else foreach (var (t, c) in def.cost) game.Inventory.TrySpend(t, Cost(c));
            if (def.needsItem != null) game.Inventory.TakeItem(def.needsItem);
            game.Stats.Practice(MadMax.RPG.Skill.Construction, 6f);
            var placed = FurnitureLibrary.Spawn(def.id, aimParent, aimPos, aimRot, material);
            if (placed) { placed.owner = game.Stats.name; _ = placed.Id; placed.Dirty(); MadMax.Audio.Sfx.Play("hammer", placed.transform.position, 0.7f); }
            MadMax.Net.NetSession.Instance?.SendPlaced(placed);
            return placed;
        }

        public void Select(int index) => Selected = Mathf.Clamp(index, 0, Pieces.Count - 1);

        bool Aim(out RaycastHit best)
        {
            best = default;
            var cam = rig.pixel.GetComponent<Camera>();
            Ray ray;
            if (rig.mode == ViewMode.FirstPerson || rig.mode == ViewMode.ThirdPerson) ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            else
            {
                var m = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
                ray = cam.ViewportPointToRay(new Vector3(m.x / Screen.width, m.y / Screen.height, 0f));
            }
            int count = Physics.RaycastNonAlloc(ray, hits, 250f, ~0, QueryTriggerInteraction.Ignore);
            float bd = float.MaxValue; bool found = false;
            var player = game.Player.transform;
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                if (h.collider.transform.IsChildOf(player) || h.collider.attachedRigidbody && h.collider.GetComponent<VehiclePart>() && !h.collider.GetComponentInParent<VehicleChassis>()) continue;
                if (OccluderFade.Active && OccluderFade.Active.Hides(h.collider, h.point)) continue;   // aim through the cut-away roof
                if (h.distance < bd) { bd = h.distance; best = h; found = true; }
            }
            if (!found) return false;
            // within reach of the player's eyes, and visible from them (no building through walls)
            var eye = game.Player.Eye.position;
            if (Vector3.Distance(best.point, eye) > reach) return false;
            return Visible(eye, best.point + best.normal * 0.04f, best.collider);
        }

        /// <summary>Nothing between the eye and the point except the target itself and the player.</summary>
        public bool Visible(Vector3 eye, Vector3 point, Collider target)
        {
            var d = point - eye;
            int n = Physics.RaycastNonAlloc(eye, d.normalized, hits, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
            var me = game.Player.transform;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == target || c.transform.IsChildOf(me) || (target && c.transform.IsChildOf(target.transform))) continue;
                if (hits[i].distance < d.magnitude - 0.06f) return false;
            }
            return true;
        }

        /// <summary>Cable / pipe tool: click a node, then another within reach. X on a node cuts its links.</summary>
        void LinkTick(FurnitureDef def, Placeable target, Keyboard kb, Mouse mouse)
        {
            ghost.SetActive(false);
            var node = target ? target.GetComponent<UtilityNode>() : null;
            bool fits = node && (node.kinds & def.link) != 0;
            string what = def.link == UtilityKind.Power ? "CABLE" : "PIPE";
            float reach = def.link == UtilityKind.Power ? UtilityGrid.CableReach : UtilityGrid.PipeReach;
            if (node && kb != null && kb.xKey.wasPressedThisFrame) { node.Unlink(); game.Toast("LINKS CUT"); linkStart = null; return; }
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (!linkStart)
            {
                Valid = fits;
                Status = fits ? "[LMB] START " + what + " HERE  [X] CUT LINKS" : "AIM AT A " + (def.link == UtilityKind.Power ? "POWERED" : "WATER") + " PIECE";
                if (click && fits) linkStart = node;
                return;
            }
            if (!linkStart) return;
            float d = fits ? Vector3.Distance(linkStart.PortWorld, node.PortWorld) : 0f;
            int cost = Mathf.Max(1, Mathf.CeilToInt(d / 5f));
            var (res, _) = def.cost[0];
            bool ok = fits && node != linkStart && d <= reach && game.Inventory.Get(res) >= cost;
            Valid = ok;
            Status = !fits ? "CONNECT TO...  [RMB] CANCEL" : node == linkStart ? "PICK ANOTHER PIECE" : d > reach ? "TOO FAR (" + Mathf.RoundToInt(d) + " M)" :
                     game.Inventory.Get(res) < cost ? "NEED " + cost + " " + ResourceInfo.Name(res) : "[LMB] CONNECT " + Mathf.RoundToInt(d) + " M (" + cost + " " + ResourceInfo.Name(res) + ")";
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) { linkStart = null; return; }
            if (click && ok)
            {
                game.Inventory.TrySpend(res, cost);
                linkStart.Link(node, def.link);
                game.Stats.Practice(MadMax.RPG.Skill.Construction, 2f);
                game.Toast(what + " CONNECTED");
                linkStart = null;
            }
        }

        /// <summary>Take a built piece apart: full refund (kit back, or raw materials).</summary>
        void Dismantle(Placeable p)
        {
            var def = FurnitureLibrary.Get(p.id);
            if (!game.OwnsPiece(p)) { game.Toast("NOT YOURS"); return; }
            if (p.TryGetComponent<Container>(out var box) && (box.inventory.ResourceArray.Length > 0 && box.Weight > 0.01f)) { game.Toast("EMPTY IT FIRST"); return; }
            if (p.TryGetComponent<UtilityNode>(out var un)) un.Unlink();
            if (def != null)
            {
                if (def.kit != null) game.Inventory.AddItem(def.kit);
                else foreach (var (t, n) in def.cost) game.Inventory.Add(t, n);
                if (def.needsItem != null) game.Inventory.AddItem(def.needsItem);
            }
            if (MadMax.World.DebrisSystem.Instance)
                for (int i = 0; i < 6; i++) MadMax.World.DebrisSystem.Instance.EmitPuff(p.transform.position + Vector3.up * 0.3f, new Color32(200, 170, 120, 255), 0.05f, Random.insideUnitSphere + Vector3.up, 0.5f);
            MadMax.Net.NetSession.Instance?.SendPlaceBroken(p);
            game.Stats.Practice(MadMax.RPG.Skill.Construction, 3f);
            game.Toast("DISMANTLED " + (def != null ? def.name : p.id));
            Destroy(p.gameObject);
        }

        bool IsFree(FurnitureDef def, Collider surface)
        {
            var b = def.mesh.bounds;
            var t = ghost.transform;
            var center = t.TransformPoint(b.center) + t.up * 0.03f;
            var half = Vector3.Max(b.extents - Vector3.one * 0.04f, Vector3.one * 0.01f);
            foreach (var c in Physics.OverlapBox(center, half, t.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == surface || c.transform.IsChildOf(game.Player.transform)) continue;
                if (c is MeshCollider && !c.GetComponent<DestructibleVoxels>()) continue;   // terrain
                return false;
            }
            return true;
        }
    }
}
