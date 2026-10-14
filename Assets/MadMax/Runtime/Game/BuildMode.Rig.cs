using MadMax.Building;
using MadMax.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Placing moving-base pieces (<see cref="RigVehicle"/>): a frame on open level ground starts a base on axle
    /// stands; extensions snap flush against the side of the frame section under the cursor (lengthwise centred, sideways
    /// sliding along it); axle and track mounts snap under the frame at the cursor's position along it (square to the
    /// base, wheels at its sides); the other rig pieces go on a base's deck like furniture. Automation:
    /// <see cref="PlaceRigPiece"/>.</summary>
    public partial class BuildMode
    {
        bool rigNewFrame;

        static bool IsRigFrame(string id) => id.StartsWith("rig_frame_") && !id.StartsWith("rig_frame_ext");
        static bool IsRigExtension(string id) => id.StartsWith("rig_frame_ext") || id == "rig_outrigger";
        static bool IsRigMount(string id) => id.StartsWith("rig_axle") || id.StartsWith("rig_track_mount");

        /// <summary>Axle-stand height a new frame is laid at (its bottom above the ground): about the rolling radius of the
        /// wheels its size takes, so commissioning barely moves it.</summary>
        static float StandHeight(string id) => id.EndsWith("_XL") ? 1.0f : id.EndsWith("_L") ? 0.75f : id.EndsWith("_M") ? 0.5f : 0.36f;

        /// <summary>Handles the tick for rig pieces that need their own snapping (true = handled).</summary>
        bool RigTick(FurnitureDef def, RaycastHit hit, Mouse mouse, Gamepad pad)
        {
            rigNewFrame = false;
            if (def.category != BuildCategory.Vehicle || !def.id.StartsWith(FurnitureLibrary.RigPrefix)) return false;
            var chassis = hit.collider.GetComponentInParent<VehicleChassis>();
            var onBase = chassis ? chassis.GetComponent<RigVehicle>() : null;
            string blocked = null, verb = "PLACE";
            if (FurnitureLibrary.NeedsWelder(def.id) && game.Inventory.GetItem("tool_welder") <= 0) blocked = "NEEDS A WELDER IN THE PACK";
            Transform parent; Vector3 lp; Quaternion lr;
            if (IsRigFrame(def.id))
            {
                parent = structures; verb = "LAY FRAME";
                var flat = Vector3.ProjectOnPlane(-CamForward(), Vector3.up);
                if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
                var aim = Quaternion.AngleAxis(yawSteps * 90f, Vector3.up) * Quaternion.LookRotation(flat.normalized, Vector3.up);
                if (chassis) blocked = blocked ?? "LAY A NEW FRAME ON OPEN GROUND (EXTENSIONS BOLT ON)";
                if (!LevelSpot(def, hit.point, aim, out lp, out lr) && blocked == null) blocked = "TOO STEEP: FIND LEVEL GROUND";
                lp += structures.InverseTransformDirection(Vector3.up) * StandHeight(def.id);
                rigNewFrame = true;
            }
            else if (IsRigExtension(def.id) || IsRigMount(def.id))
            {
                parent = onBase ? onBase.transform : structures; lp = Vector3.zero; lr = Quaternion.identity;
                if (!onBase) { blocked = blocked ?? (IsRigMount(def.id) ? "AIM AT A RIG FRAME: THE MOUNT GOES UNDER IT" : "AIM AT A RIG FRAME TO BOLT IT ON"); lp = parent.InverseTransformPoint(hit.point); }
                else if (!RigSnap(def, onBase, onBase.transform.InverseTransformPoint(hit.point), out lp, out string why)) blocked = blocked ?? why;
                verb = IsRigMount(def.id) ? "FIT MOUNT" : "BOLT ON";
            }
            else
            {
                if (onBase) return false;                                                          // seat, bay, tank, springs: like furniture on its deck
                ghost.SetActive(false); Valid = false;
                Status = "BUILD IT ON A MOVING BASE (LAY A RIG FRAME FIRST)";
                return true;
            }
            if (onBase && onBase.Commissioned && onBase.Driver && onBase.Driver.Body.linearVelocity.sqrMagnitude > 0.25f) blocked = blocked ?? "STOP THE BASE FIRST";
            ghost.SetActive(true);
            ghostMesh.sharedMesh = def.mesh;
            GhostHD(def.id);
            ghost.transform.SetPositionAndRotation(parent.TransformPoint(lp), parent.rotation * lr);
            bool affordable = Affordable(def);
            bool free = blocked != null || !rigNewFrame || IsFree(def, hit.collider);
            Valid = affordable && blocked == null && free;
            string need = onBase && !onBase.Commissioned && onBase.Missing != null ? "   NEXT: " + onBase.Missing : "";
            Status = blocked ?? (!affordable ? "NOT ENOUGH MATERIALS" : !free ? "BLOCKED" : "[LMB] " + verb + "  [Y] ROTATE" + need);
            mpb.SetColor("_Tint", Valid ? new Color(0.7f, 1.3f, 0.7f) : new Color(1.4f, 0.5f, 0.45f));
            ghostRenderer.SetPropertyBlock(mpb);
            foreach (var r in ghostHDRenderers) if (r) r.SetPropertyBlock(mpb);
            aimParent = parent; aimPos = lp; aimRot = lr;
            bool place = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame);
            if (place) TryPlace();
            return true;
        }

        Vector3 CamForward() => rig.pixel.transform.forward;

        /// <summary>Where an extension or a mount goes on <paramref name="r"/> for a vehicle-space point under the cursor.</summary>
        static bool RigSnap(FurnitureDef def, RigVehicle r, Vector3 local, out Vector3 lp, out string why)
        {
            why = null; lp = Vector3.zero;
            var f = r.FrameAt(local);
            if (!f) { why = "NO FRAME THERE"; return false; }
            var fl = f.transform.localPosition;
            const float Grid = 0.08f;
            if (IsRigMount(def.id))
            {
                float z = Mathf.Round(local.z / Grid) * Grid;
                r.SidesAt(z, out float left, out float right);
                lp = new Vector3((left + right) * 0.5f, fl.y, z);
                if (r.MountNear(z, 1.0f)) { why = "TOO CLOSE TO ANOTHER MOUNT (1 M)"; return false; }
                return true;
            }
            var half = new Vector2(def.deck.x, def.deck.y);
            var d = local - fl;
            bool sideways = Mathf.Abs(d.x) / Mathf.Max(0.1f, f.half.x) > Mathf.Abs(d.z) / Mathf.Max(0.1f, f.half.y);
            float y = fl.y + f.top - def.deck.z;
            if (sideways)
            {
                float span = Mathf.Max(0f, f.half.y - half.y);
                float z = Mathf.Clamp(Mathf.Round(local.z / Grid) * Grid, fl.z - span, fl.z + span);
                lp = new Vector3(fl.x + Mathf.Sign(d.x) * (f.half.x + half.x), y, z);
            }
            else lp = new Vector3(fl.x, y, fl.z + Mathf.Sign(d.z) * (f.half.y + half.y));
            if (r.FrameOverlaps(lp, half)) { why = "A FRAME SECTION IS ALREADY THERE"; return false; }
            return true;
        }

        Placeable PlaceRigFrame(FurnitureDef def)
        {
            var w = aimParent.TransformPoint(aimPos);
            var q = aimParent.rotation * aimRot;
            game.StartRig(def.id, w, q, out var frame);
            game.Toast("FRAME ON STANDS: FIT AXLE MOUNTS UNDER IT (VEHICLE TAB)");
            return frame;
        }

        /// <summary>Automation: place a rig piece as the build tool would aimed at <paramref name="at"/> (world), free of
        /// cost. Frames start a new base there (on stands); extensions and mounts snap to <paramref name="onto"/>; other
        /// rig pieces stand on its deck at that point.</summary>
        public Placeable PlaceRigPiece(string id, Vector3 at, RigVehicle onto, float yaw = 0f)
        {
            var def = FurnitureLibrary.Get(id);
            if (def == null) return null;
            if (IsRigFrame(id))
            {
                var t = MadMax.World.DeformableTerrain.Instance;
                if (t) at.y = t.Height(at.x, at.z);
                game.StartRig(id, at + Vector3.up * StandHeight(id), Quaternion.Euler(0f, yaw, 0f), out var frame);
                return frame;
            }
            if (!onto) return null;
            var local = onto.transform.InverseTransformPoint(at);
            Vector3 lp;
            if (IsRigExtension(id) || IsRigMount(id)) { if (!RigSnap(def, onto, local, out lp, out _)) return null; }
            else
            {
                var f = onto.FrameAt(local);
                lp = new Vector3(local.x, f ? f.transform.localPosition.y + f.top : 0.4f, local.z);
            }
            var placed = FurnitureLibrary.Spawn(id, onto.transform, lp, Quaternion.Euler(0f, yaw, 0f), material);
            if (placed) { placed.owner = game.Stats.name; _ = placed.Id; }
            onto.Refresh();
            return placed;
        }
    }
}
