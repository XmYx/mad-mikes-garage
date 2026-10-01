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
            new GameObject("StructureBatches").AddComponent<StructureBatcher>();             // far bases drawn as merged meshes
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
        public static readonly BuildCategory[] Categories = { BuildCategory.Structure, BuildCategory.Furniture, BuildCategory.Utility, BuildCategory.Garden, BuildCategory.Industry, BuildCategory.Defence, BuildCategory.Decor };
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
                if (Controls.Down(Controls.Act.Build)) { bHeld = 0f; RadialHover = -1; }
                if (Controls.Held(Controls.Act.Build))
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
                if (Controls.Up(Controls.Act.Build))
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
                if (Controls.Down(Controls.Act.BuildNextCategory)) SetCategory(Categories[(ci + 1) % Categories.Length]);
                if (Controls.Down(Controls.Act.BuildPrevCategory)) SetCategory(Categories[(ci + Categories.Length - 1) % Categories.Length]);
                all = Pieces;
                for (int i = 0; i < 10; i++)
                {
                    var key = i == 9 ? Key.Digit0 : Key.Digit1 + i;
                    if (kb[key].wasPressedThisFrame) Selected = Mathf.Min(i, all.Count - 1);
                }
                if (kb.pageDownKey.wasPressedThisFrame || Controls.Down(Controls.Act.BuildNextPiece)) Selected = (Selected + 1) % all.Count;
                if (kb.pageUpKey.wasPressedThisFrame || Controls.Down(Controls.Act.BuildPrevPiece)) Selected = (Selected + all.Count - 1) % all.Count;
                if (Controls.Down(Controls.Act.BuildRotate)) yawSteps = (yawSteps + 1) % 4;
            }
            if (pad != null)
            {
                // pad: d-pad up/down picks the piece, shoulders switch category, north rotates
                if (pad.dpad.up.wasPressedThisFrame) Selected = (Selected + 1) % all.Count;
                if (pad.dpad.down.wasPressedThisFrame) Selected = (Selected + all.Count - 1) % all.Count;
                int pci = System.Array.IndexOf(Categories, Category);
                if (pad.rightShoulder.wasPressedThisFrame) { SetCategory(Categories[(pci + 1) % Categories.Length]); all = Pieces; }
                if (pad.leftShoulder.wasPressedThisFrame) { SetCategory(Categories[(pci + Categories.Length - 1) % Categories.Length]); all = Pieces; }
                if (pad.buttonNorth.wasPressedThisFrame) yawSteps = (yawSteps + 1) % 4;
            }
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
            if (def.plan == -2) { CaptureTick(targetPiece, kb, mouse); return; }
            bool padX = pad != null && pad.buttonWest.wasPressedThisFrame;
            if (targetPiece && (Controls.Down(Controls.Act.BuildDismantle) || padX)) { Dismantle(targetPiece); return; }
            if (!targetPiece && def.plan >= 0 && Controls.Down(Controls.Act.BuildDismantle)) { StructurePlans.Forget(def.plan); Selected = Mathf.Max(0, Selected - 1); game.Toast("PLAN FORGOTTEN"); return; }
            if (targetPiece && Controls.Down(Controls.Act.BuildUpgrade)) { Upgrade(targetPiece); return; }
            if (targetPiece && Controls.Down(Controls.Act.BuildRepair)) { Repair(targetPiece); return; }

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

            string blocked = null;
            if (def.foundation || def.plan >= 0)
            {
                // level on the ground, yawed square to the world (or tiled against the foundation under the cursor)
                parent = structures;
                if (chassis) blocked = "BUILD IT ON THE GROUND";
                else if (def.foundation && targetPiece && FurnitureLibrary.Get(targetPiece.id) is FurnitureDef td && td.foundation) NextTo(targetPiece, hit.point, out localPos, out localRot);
                else if (!LevelSpot(def, hit.point, rot, out localPos, out localRot)) blocked = "TOO STEEP FOR THE LEGS";
            }
            ghost.SetActive(true);
            ghostMesh.sharedMesh = def.mesh;
            ghost.transform.SetPositionAndRotation(parent.TransformPoint(localPos), parent.rotation * localRot);

            bool affordable = Affordable(def);
            bool free = blocked == null && IsFree(def, hit.collider);
            Valid = affordable && free;
            Status = blocked ?? (!affordable ? "NOT ENOUGH MATERIALS" : !free ? "BLOCKED" : targetPiece ? "[LMB] PLACE  [Y] ROTATE" + PieceHint(targetPiece) : def.plan >= 0 ? "[LMB] BUILD PLAN  [Y] ROTATE  [X] FORGET PLAN" : "[LMB] PLACE  [Y] ROTATE  [B] EXIT");
            mpb.SetColor("_Tint", Valid ? new Color(0.7f, 1.3f, 0.7f) : new Color(1.4f, 0.5f, 0.45f));
            ghostRenderer.SetPropertyBlock(mpb);

            // doors snap into doorways, garage doors into their frames, shutters over windows
            if (def.snapTo != null && targetPiece && targetPiece.id.Contains(def.snapTo))
            {
                parent = targetPiece.transform.parent; localPos = targetPiece.transform.localPosition; localRot = targetPiece.transform.localRotation;
                ghost.transform.SetPositionAndRotation(parent.TransformPoint(localPos), parent.rotation * localRot);
                Valid = affordable; Status = affordable ? "[LMB] FIT " + def.name : "NOT ENOUGH MATERIALS";
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
            if (def.plan >= 0) return PlacePlan(def);
            using (Inventory.Source(null, "BUILT"))                                                  // item feed labels
            {
                if (def.kit != null) game.Inventory.TakeItem(def.kit);
                else foreach (var (t, c) in def.cost) game.Inventory.TrySpend(t, Cost(c));
                if (def.needsItem != null) game.Inventory.TakeItem(def.needsItem);
            }
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
            if (rig.CrosshairView) ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
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
            if (node && Controls.Down(Controls.Act.BuildDismantle)) { node.Unlink(); game.Toast("LINKS CUT"); linkStart = null; return; }
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
                using (Inventory.Source(null, "LAID")) game.Inventory.TrySpend(res, cost);
                linkStart.Link(node, def.link);
                game.Stats.Practice(MadMax.RPG.Skill.Construction, 2f);
                game.Toast(what + " CONNECTED");
                linkStart = null;
            }
        }

        /// <summary>Take a built piece apart: full refund of what building it costs (kit back, or the raw materials at
        /// the builder's skill price, so building and dismantling never makes material out of nothing).</summary>
        void Dismantle(Placeable p)
        {
            var def = FurnitureLibrary.Get(p.id);
            if (!game.OwnsPiece(p)) { game.Toast("NOT YOURS"); return; }
            if (p.TryGetComponent<Container>(out var box) && !Empty(box.inventory)) { game.Toast("EMPTY IT FIRST"); return; }   // coins weigh next to nothing
            if (p.TryGetComponent<UtilityNode>(out var un)) un.Unlink();
            if (def != null)
            {
                using var feed = Inventory.Source("DISMANTLED");
                if (def.kit != null) game.Inventory.AddItem(def.kit);
                else foreach (var (t, n) in def.cost) game.Inventory.Add(t, Cost(n));
                if (def.needsItem != null) game.Inventory.AddItem(def.needsItem);
            }
            if (MadMax.World.DebrisSystem.Instance)
                for (int i = 0; i < 6; i++) MadMax.World.DebrisSystem.Instance.EmitPuff(p.transform.position + Vector3.up * 0.3f, new Color32(200, 170, 120, 255), 0.05f, Random.insideUnitSphere + Vector3.up, 0.5f);
            MadMax.Net.NetSession.Instance?.SendPlaceBroken(p);
            game.Stats.Practice(MadMax.RPG.Skill.Construction, 3f);
            game.Toast("DISMANTLED " + (def != null ? def.name : p.id));
            StructureSupport.Removed(p, p.transform.position, p.transform.parent);
            Destroy(p.gameObject);
        }

        static bool Empty(Inventory inv)
        {
            foreach (var n in inv.ResourceArray) if (n > 0) return false;
            foreach (var kv in inv.Items) if (kv.Value > 0) return false;
            return true;
        }

        // ------------------------------------------------------------------ foundations, plans, upgrades, repairs

        /// <summary>Foundation / plan spot: upright, yaw snapped to 90°, origin on the highest ground under the
        /// footprint (legs reach 1.8 m down). False when the slope is too steep.</summary>
        bool LevelSpot(FurnitureDef def, Vector3 at, Quaternion aim, out Vector3 localPos, out Quaternion localRot)
        {
            float yaw = Mathf.Round(aim.eulerAngles.y / 90f) * 90f;
            if (Mathf.Abs(Vector3.Dot(aim * Vector3.up, Vector3.up)) < 0.7f) yaw = Mathf.Round(Quaternion.LookRotation(-rig.pixel.transform.forward).eulerAngles.y / 90f) * 90f + yawSteps * 90f;
            localRot = Quaternion.Euler(0f, yaw, 0f);
            at.x = Mathf.Round(at.x / grid) * grid; at.z = Mathf.Round(at.z / grid) * grid;
            var t = DeformableTerrain.Instance;
            var b = def.mesh.bounds;
            float hi = at.y, lo = at.y;
            if (t && t.World != null)
            {
                hi = float.MinValue; lo = float.MaxValue;
                for (int k = 0; k < 5; k++)
                {
                    var o = k == 4 ? b.center : new Vector3((k & 1) == 0 ? b.min.x : b.max.x, 0f, (k & 2) == 0 ? b.min.z : b.max.z);
                    var w = at + localRot * new Vector3(o.x, 0f, o.z);
                    float h = t.Height(w.x, w.z);
                    hi = Mathf.Max(hi, h); lo = Mathf.Min(lo, h);
                }
                bool legs = def.foundation || (def.plan >= 0 && StructurePlans.Get(def.plan).pieces.Exists(e => FurnitureLibrary.Get(e.id)?.foundation == true));
                at.y = legs && hi - lo <= 1.8f ? hi : lo;
            }
            localPos = structures.InverseTransformPoint(at);
            localRot = Quaternion.Inverse(structures.rotation) * localRot;
            return !def.foundation || hi - lo <= 1.8f;
        }

        /// <summary>Tile a foundation against the one under the cursor, on the side the cursor is nearest.</summary>
        void NextTo(Placeable f, Vector3 point, out Vector3 localPos, out Quaternion localRot)
        {
            var l = f.transform.InverseTransformPoint(point);
            var step = Mathf.Abs(l.x) > Mathf.Abs(l.z) ? new Vector3(Mathf.Sign(l.x) * 2f, 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(l.z) * 2f);
            localPos = structures.InverseTransformPoint(f.transform.TransformPoint(step));
            localRot = Quaternion.Inverse(structures.rotation) * f.transform.rotation;
        }

        /// <summary>Status suffix for the piece under the cursor: condition, dismantle, upgrade.</summary>
        string PieceHint(Placeable p)
        {
            var def = FurnitureLibrary.Get(p.id);
            if (def == null || !game.OwnsPiece(p)) return "";
            string s = "  [X] DISMANTLE";
            if (p.hits < def.hits) s += "  [R] REPAIR " + p.hits + "/" + def.hits;
            var up = def.upgrade != null ? FurnitureLibrary.Get(def.upgrade) : null;
            if (up != null) s += "  [U] " + up.name;
            return s;
        }

        /// <summary>Rebuild a piece as its better version in place (pays the new cost, gets half the old back).</summary>
        public void Upgrade(Placeable p)
        {
            var def = FurnitureLibrary.Get(p.id);
            var up = def != null && def.upgrade != null ? FurnitureLibrary.Get(def.upgrade) : null;
            if (up == null) { game.Toast("NOTHING TO UPGRADE IT TO"); return; }
            if (!game.OwnsPiece(p)) { game.Toast("NOT YOURS"); return; }
            if (!Affordable(up)) { game.Toast("UPGRADE NEEDS " + CostText(up)); return; }
            using (Inventory.Source("SALVAGED", "UPGRADED"))
            {
                foreach (var (t, c) in up.cost) game.Inventory.TrySpend(t, Cost(c));
                if (up.kit != null) game.Inventory.TakeItem(up.kit);                               // kit pieces (prefab panels) use up their kit
                foreach (var (t, n) in def.cost) if (n / 2 > 0) game.Inventory.Add(t, n / 2);
            }
            var parent = p.transform.parent; var lp = p.transform.localPosition; var lr = p.transform.localRotation;
            string owner = p.owner; byte dye = p.dye;
            var box = p.GetComponent<Container>();                                                  // stores move over (MG nest belts → the turret)
            MadMax.Net.NetSession.Instance?.SendPlaceBroken(p);
            Destroy(p.gameObject);
            var placed = FurnitureLibrary.Spawn(up.id, parent, lp, lr, material);
            if (placed) { placed.owner = owner; _ = placed.Id; if (dye != 0) placed.SetDye(dye); placed.Dirty(); MadMax.Net.NetSession.Instance?.SendPlaced(placed); }
            if (box && placed && placed.TryGetComponent<Container>(out var into)) InventoryCodec.Decode(into.inventory, InventoryCodec.Encode(box.inventory));
            else if (box) box.Spill();
            game.Stats.Practice(MadMax.RPG.Skill.Construction, 8f);
            MadMax.Audio.Sfx.Play("hammer", lp, 0.8f);
            game.Toast("UPGRADED TO " + up.name);
        }

        /// <summary>Hammer a damaged piece back to full: a share of its cost for the missing condition.</summary>
        public void Repair(Placeable p)
        {
            var def = FurnitureLibrary.Get(p.id);
            if (def == null || p.hits >= def.hits) { game.Toast("NOTHING TO REPAIR"); return; }
            float missing = 1f - p.hits / (float)def.hits;
            var need = new List<(ResourceType, int)>();
            foreach (var (t, n) in def.cost) need.Add((t, Mathf.Max(1, Mathf.CeilToInt(n * missing * 0.5f))));
            foreach (var (t, n) in need) if (game.Inventory.Get(t) < n) { game.Toast("REPAIR NEEDS " + n + " " + ResourceInfo.Name(t)); return; }
            using (Inventory.Source(null, "REPAIRED")) foreach (var (t, n) in need) game.Inventory.TrySpend(t, n);
            p.hits = def.hits; p.Dirty();
            MadMax.Story.Story.Note("repaired:" + p.id);
            game.Stats.Practice(MadMax.RPG.Skill.Construction, 2f);
            MadMax.Audio.Sfx.Play("hammer", p.transform.position, 0.7f);
            game.Toast("REPAIRED " + def.name);
        }

        string CostText(FurnitureDef d)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (t, n) in d.cost) { if (sb.Length > 0) sb.Append(", "); sb.Append(Cost(n)).Append(' ').Append(ResourceInfo.Name(t)); }
            return sb.ToString();
        }

        /// <summary>Plan capture tool: LMB on one of your pieces saves the whole connected structure.</summary>
        void CaptureTick(Placeable target, Keyboard kb, Mouse mouse)
        {
            ghost.SetActive(false);
            bool ok = target && game.OwnsPiece(target) && !target.GetComponentInParent<Rigidbody>();
            Valid = ok;
            Status = ok ? "[LMB] SAVE THIS STRUCTURE AS A PLAN (" + StructurePlans.Count + "/" + StructurePlans.Max + ")" : "AIM AT A STRUCTURE YOU BUILT";
            if (!ok || mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            int n = StructurePlans.Capture(target, out int skipped);
            game.Toast(n > 0 ? "PLAN SAVED: " + n + " PIECES" + (skipped > 0 ? " (" + skipped + " KIT PIECES LEFT OUT)" : "") : "NOTHING TO SAVE");
            MadMax.Audio.Sfx.Play("scratch", target.transform.position, 0.5f);
        }

        /// <summary>Build a whole plan at the ghost (pays the summed cost).</summary>
        Placeable PlacePlan(FurnitureDef def)
        {
            var plan = StructurePlans.Get(def.plan);
            if (plan == null) return null;
            using (Inventory.Source(null, "BUILT")) foreach (var (t, c) in def.cost) game.Inventory.TrySpend(t, Cost(c));
            var o = ghost.transform;
            Placeable first = null;
            foreach (var e in plan.pieces)
            {
                var wp = o.TransformPoint(e.pos); var wr = o.rotation * e.rot;
                var placed = FurnitureLibrary.Spawn(e.id, structures, structures.InverseTransformPoint(wp), Quaternion.Inverse(structures.rotation) * wr, material);
                if (!placed) continue;
                placed.owner = game.Stats.name; _ = placed.Id;
                if (e.dye != 0) placed.SetDye(e.dye);
                placed.Dirty();
                MadMax.Net.NetSession.Instance?.SendPlaced(placed);
                if (!first) first = placed;
            }
            game.Stats.Practice(MadMax.RPG.Skill.Construction, 4f * plan.pieces.Count);
            MadMax.Audio.Sfx.Play("hammer", o.position, 0.9f);
            game.Toast("BUILT " + def.name);
            return first;
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
