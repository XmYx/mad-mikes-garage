using System.Collections.Generic;
using MadMax.Items;
using MadMax.RPG;
using MadMax.Vehicles;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>Tool wear and repair. Each tool id has one "in hand" condition (the stack wears one item at a time):
    /// strikes, shots and burning wear it; at 100 % wear the item breaks and the next one of the stack takes over.
    /// Crafting skill slows wear. Workbenches repair (Repair page) for the tool's own materials. Also: the repair kit,
    /// and the jack needed for big wheels.</summary>
    public partial class WastelandGame
    {
        /// <summary>0 = like new .. 1 = breaks, per tool id (saved).</summary>
        public readonly Dictionary<string, float> ToolWear = new Dictionary<string, float>();

        public float Condition(string id) => ToolWear.TryGetValue(id, out var w) ? Mathf.Clamp01(1f - w) : 1f;

        /// <summary>Wear the held tool by <paramref name="amount"/> (fraction of its life).</summary>
        public void WearTool(string id, float amount)
        {
            if (string.IsNullOrEmpty(id) || Inventory.GetItem(id) <= 0) return;
            ToolWear.TryGetValue(id, out var w);
            w += amount * Mathf.Max(0.5f, 1f - Stats.Level(Skill.Crafting) * 0.04f) * GameRules.Current.DamageTaken * QualityWear(id);
            if (id == "tool_flashlight" && w >= 1f)
            {
                // a flat battery, not a broken torch: swap in a fresh one if there is one
                if (Inventory.TakeItem("use_battery")) { w = 0f; Toast("FLASHLIGHT: FRESH BATTERY"); }
                else { if (w < 1.5f) Toast("FLASHLIGHT: BATTERY DEAD (CRAFT A CAR BATTERY)"); w = 1.5f; }
                ToolWear[id] = w;
                return;
            }
            if (w < 1f) { ToolWear[id] = w; return; }
            ToolWear[id] = 0f;
            Inventory.TakeItem(id);
            MadMax.Audio.Sfx.Play("hit_metal", Player.transform.position, 0.8f, 0.6f);
            Toast("YOUR " + ItemCatalog.Name(id) + " BROKE" + (Inventory.GetItem(id) > 0 ? " (NEXT ONE IN HAND)" : ""));
            if (Inventory.GetItem(id) <= 0 && Player.Tool && Player.Tool.id == id) Player.Equip(null);
            SyncHotbar();
        }

        /// <summary>What a repair at a workbench costs: the tool's main material, more for badly worn tools.</summary>
        public (ResourceType type, int amount) RepairCost(string id)
        {
            float w = 1f - Condition(id);
            var t = id.Contains("torch") && !id.Contains("gas") ? ResourceType.Cloth
                  : id == "tool_axe" || id == "tool_shovel" || id == "tool_pickaxe" || id == ItemIds.Sledgehammer ? ResourceType.Iron
                  : id.Contains("geiger") || id.Contains("flashlight") || id.Contains("welder") ? ResourceType.Copper
                  : id.Contains("binoculars") || id.Contains("lantern") ? ResourceType.Glass : ResourceType.Scrap;
            return (t, Mathf.Max(1, Mathf.CeilToInt(w * (t == ResourceType.Scrap ? 5f : 3f))));
        }

        public bool RepairTool(string id)
        {
            if (Condition(id) >= 0.999f) return false;
            if (id == "tool_flashlight") { Toast("A FLASHLIGHT NEEDS A BATTERY, NOT A REPAIR"); return false; }
            var (t, n) = RepairCost(id);
            if (!Inventory.TrySpend(t, n)) { Toast("NEED " + n + " " + ResourceInfo.Name(t)); return false; }
            ToolWear[id] = 0f;
            Stats.Practice(Skill.Crafting, 3f);
            MadMax.Audio.Sfx.Play2D("ratchet", 0.6f);
            Toast(ItemCatalog.Name(id) + " REPAIRED");
            return true;
        }

        /// <summary>Repair kit: the most damaged part of the vehicle you are in or next to (or <paramref name="on"/>) gets +30 % condition.</summary>
        bool UseRepairKit(VehicleDriver on = null)
        {
            VehicleDriver v = on ? on : Current;
            if (!v)
            {
                float best = 4f;
                foreach (var c in AllVehicles)
                {
                    if (!c) continue;
                    float d = Vector3.Distance(c.transform.position, Player.transform.position);
                    if (d < best) { best = d; v = c; }
                }
            }
            if (!v) { Toast("NO VEHICLE IN REACH"); return false; }
            VehiclePart worst = null;
            foreach (var p in v.GetComponentsInChildren<VehiclePart>()) if (p.Socket && (!worst || p.damage > worst.damage)) worst = p;
            if (!worst || worst.damage < 0.02f) { Toast("NOTHING TO FIX"); return false; }
            if (DeferRepairKit(v, worst)) return false;                                      // patched at the part first; the kit is used then (WastelandGame.Anim)
            worst.damage = Mathf.Max(0f, worst.damage - 0.3f - Stats.Level(Skill.Mechanics) * 0.02f);
            if (worst.TryGetComponent<WheelStats>(out var ws) && ws.Popped) ws.wear = 0.8f;       // patched, not new
            Stats.Practice(Skill.Mechanics, 5f);
            MadMax.Audio.Sfx.Play("ratchet", v.transform.position, 0.8f);
            Toast("PATCHED " + worst.partId.Replace('_', ' ').ToUpperInvariant() + " (" + Mathf.RoundToInt((1f - worst.damage) * 100f) + "%)");
            return true;
        }

        /// <summary>Wheels of size 3+ (truck, tracks) need the jack in the pack as well as the wrench.</summary>
        bool NeedsJack(VehiclePart p) => p && p.category == PartCategory.Wheel && p.sizeClass >= 3 && Inventory.GetItem("tool_jack") <= 0;

        void SaveTools(SaveData d)
        {
            d.toolWearIds = new List<string>(); d.toolWear = new List<float>();
            foreach (var kv in ToolWear) if (kv.Value > 0f) { d.toolWearIds.Add(kv.Key); d.toolWear.Add(kv.Value); }
            d.gunRoundIds = new List<string>(); d.gunRounds = new List<int>();
            foreach (var kv in GunRounds) if (kv.Value > 0) { d.gunRoundIds.Add(kv.Key); d.gunRounds.Add(kv.Value); }
        }

        void RestoreTools(SaveData d)
        {
            ToolWear.Clear(); GunRounds.Clear();
            if (d.toolWearIds != null)
                for (int i = 0; i < d.toolWearIds.Count && i < d.toolWear.Count; i++) ToolWear[d.toolWearIds[i]] = d.toolWear[i];
            if (d.gunRoundIds != null)
                for (int i = 0; i < d.gunRoundIds.Count && i < d.gunRounds.Count; i++) GunRounds[d.gunRoundIds[i]] = d.gunRounds[i];
        }

        // ------------------------------------------------------------------ guns (roadmap 13)
        /// <summary>Rounds in each gun's magazine, per tool id (saved).</summary>
        public readonly Dictionary<string, int> GunRounds = new Dictionary<string, int>();
        public int Rounds(string id) => GunRounds.TryGetValue(id, out var n) ? n : 0;
        public void SetRounds(string id, int n) => GunRounds[id] = Mathf.Max(0, n);
        /// <summary>Rounds the player has fired this session (hooks count shots by it; sampling rounds misses a reload and shot in one frame).</summary>
        public int ShotsFired { get; set; }

        /// <summary>Holding RMB with a ranged weapon on foot.</summary>
        public bool Aiming { get; private set; }
        /// <summary>Where the aim is (cursor in the top-down views, screen centre in first / third person).</summary>
        public Vector3 AimPoint { get; private set; }
        /// <summary>The aim point on the pixel canvas (HUD crosshair); negative when not aiming.</summary>
        public Vector2 AimScreen { get; private set; } = new Vector2(-1f, -1f);

        void UpdateAim(UnityEngine.InputSystem.Mouse mouse, UnityEngine.InputSystem.Gamepad pad)
        {
            bool ranged = !Current && Player && (Player.Tool is RangedTool || Player.Tool is GrappleTool) && !Menus.IsOpen;
            Aiming = ranged && (ForceAim || (mouse != null && mouse.rightButton.isPressed) || (pad != null && pad.leftTrigger.isPressed));
            Player.aiming = Aiming;
            AimScreen = new Vector2(-1f, -1f);
            if (!Aiming || !cameraRig) return;
            var cam = cameraRig.pixel.GetComponent<Camera>();
            bool topDown = cameraRig.TopDownView;
            Vector2 view = ForceAim ? (topDown ? ForceAimViewport : new Vector2(0.5f, 0.5f))
                : topDown && mouse != null ? new Vector2(mouse.position.ReadValue().x / Screen.width, mouse.position.ReadValue().y / Screen.height) : new Vector2(0.5f, 0.5f);
            // top-down with a pad: the right stick points the aim around the character (camera-relative)
            if (!ForceAim && topDown && pad != null && pad.leftTrigger.isPressed && pad.rightStick.ReadValue().sqrMagnitude > 0.05f)
            {
                var st = pad.rightStick.ReadValue();
                var f = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized; var r = Vector3.Cross(Vector3.up, f);
                var target = Player.transform.position + Vector3.up * 1.1f + (f * st.y + r * st.x).normalized * 12f;
                var vp = cam.WorldToViewportPoint(target);
                view = new Vector2(vp.x, vp.y);
            }
            var ray = cam.ViewportPointToRay(new Vector3(view.x, view.y, 0f));
            AimPoint = Physics.Raycast(ray, out var hit, 300f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(Player.transform) ? hit.point : ray.GetPoint(60f);
            if (topDown)
            {
                // face the cursor and aim at chest height over flat ground
                var flat = AimPoint - Player.transform.position; flat.y = 0f;
                if (flat.sqrMagnitude > 0.04f) Player.transform.rotation = Quaternion.Slerp(Player.transform.rotation, Quaternion.LookRotation(flat), 1f - Mathf.Exp(-14f * Time.deltaTime));
                if (hit.collider && hit.collider.GetComponentInParent<MadMax.World.DeformableTerrain>()) AimPoint += Vector3.up * 1.1f;
            }
            AimScreen = new Vector2(view.x, view.y);
        }

        /// <summary>Direction of a shot from <paramref name="origin"/>: at the aim point while aiming, along the camera in
        /// first / third person, else straight ahead.</summary>
        public Vector3 AimDirection(PlayerCharacter user, Vector3 origin, float range)
        {
            if (Aiming) return (AimPoint - origin).normalized;
            if (cameraRig && cameraRig.CrosshairView)
            {
                var cam = cameraRig.pixel.transform;
                return Physics.Raycast(cam.position, cam.forward, out var ch, range, ~0, QueryTriggerInteraction.Ignore) ? (ch.point - origin).normalized : cam.forward;
            }
            return user.transform.forward;
        }
    }
}
