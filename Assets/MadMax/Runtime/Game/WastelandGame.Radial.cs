using System.Collections.Generic;
using System.Text.RegularExpressions;
using MadMax.Vehicles;
using MadMax.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Hold Tab: radial menu of everything you can do right now. Entries come from the current interaction
    /// prompt ("[G] PUMP INTO ...", "[J] HITCH ...") plus vehicle and character actions; choosing one injects the same key
    /// press, so every action keeps a single implementation. Tap Tab still switches fleet vehicles. The mouse picks a
    /// slice by accumulated movement (works with the locked cursor); camera look pauses meanwhile.</summary>
    public partial class WastelandGame
    {
        public struct RadialAction { public string label; public Controls.Act? act; public System.Action run; public string detail; }

        public bool RadialOpen { get; private set; }
        public int RadialHover { get; private set; } = -1;
        public readonly List<RadialAction> RadialActions = new List<RadialAction>();
        /// <summary>True while the radial owns the mouse (CameraRig skips mouse look).</summary>
        public static bool RadialBlocksLook;

        bool TabTapped;
        float tabHeld;
        Vector2 radialAim;
        static readonly Regex PromptKey = new Regex(@"\[([^\]]+)\]\s*([^\[]*)");


        /// <summary>Gamepad Select tapped (not held for the wheel): map on foot, recover in a vehicle.</summary>
        public bool PadSelectTapped { get; private set; }
        float padHeld = -1f;

        void UpdateRadial(Keyboard kb, Mouse mouse)
        {
            TabTapped = false; PadSelectTapped = false;
            var pad = Gamepad.current;
            if (UpdateFluidChoice(kb, mouse, pad)) return;                                   // a container's siphon / pour choice (WastelandGame.Fluids)
            if (Menus.IsOpen || TitleSequence.Playing) { CloseRadial(); padHeld = -1f; return; }
            // gamepad: hold Select for the wheel, aim with the right stick, release to run
            if (pad != null)
            {
                if (pad.selectButton.wasPressedThisFrame) padHeld = 0f;
                if (padHeld >= 0f && pad.selectButton.isPressed)
                {
                    padHeld += Time.unscaledDeltaTime;
                    if (!RadialOpen && padHeld > 0.3f) { GatherActions(); RadialOpen = RadialActions.Count > 0; RadialHover = -1; }
                    if (RadialOpen)
                    {
                        var st = pad.rightStick.ReadValue();
                        int n = RadialActions.Count;
                        if (st.magnitude > 0.5f)
                        {
                            float a = (Mathf.Atan2(st.x, st.y) * Mathf.Rad2Deg + 360f) % 360f;
                            RadialHover = Mathf.Clamp(Mathf.FloorToInt(((a + 180f / n) % 360f) / (360f / n)), 0, n - 1);
                        }
                    }
                    RadialBlocksLook = RadialOpen;
                    return;
                }
                if (padHeld >= 0f && pad.selectButton.wasReleasedThisFrame)
                {
                    if (RadialOpen) { if (RadialHover >= 0) RunRadial(RadialActions[RadialHover]); CloseRadial(); }
                    else if (padHeld <= 0.3f) PadSelectTapped = true;
                    padHeld = -1f;
                    return;
                }
            }
            if (kb == null) { CloseRadial(); return; }
            var tab = kb.tabKey;                                                             // Tab: fixed (wheel / next fleet vehicle)
            if (tab.wasPressedThisFrame) tabHeld = 0f;
            if (tab.isPressed)
            {
                tabHeld += Time.unscaledDeltaTime;
                if (!RadialOpen && tabHeld > 0.22f) { GatherActions(); RadialOpen = RadialActions.Count > 0; radialAim = Vector2.zero; RadialHover = -1; }
                if (RadialOpen && mouse != null)
                {
                    radialAim = Vector2.ClampMagnitude(radialAim + mouse.delta.ReadValue(), 120f);
                    int n = RadialActions.Count;
                    if (radialAim.magnitude > 18f)
                    {
                        float a = (Mathf.Atan2(radialAim.x, radialAim.y) * Mathf.Rad2Deg + 360f) % 360f;
                        RadialHover = Mathf.Clamp(Mathf.FloorToInt(((a + 180f / n) % 360f) / (360f / n)), 0, n - 1);
                    }
                    if (mouse.leftButton.wasPressedThisFrame && RadialHover >= 0) { RunRadial(RadialActions[RadialHover]); CloseRadial(); tabHeld = -99f; }
                }
            }
            if (tab.wasReleasedThisFrame)
            {
                if (RadialOpen) { if (RadialHover >= 0) RunRadial(RadialActions[RadialHover]); CloseRadial(); }
                else if (tabHeld >= 0f && tabHeld <= 0.22f) TabTapped = true;
            }
            RadialBlocksLook = RadialOpen;
        }

        void CloseRadial() { RadialOpen = false; RadialHover = -1; RadialBlocksLook = false; }

        void RunRadial(RadialAction a)
        {
            if (a.run != null) a.run();
            else if (a.act.HasValue) Controls.Inject(a.act.Value);                       // handled by the normal input code later this frame
        }

        void AddRadial(string label, Controls.Act? act, System.Action run = null)
        {
            foreach (var r in RadialActions) if (r.label == label) return;
            RadialActions.Add(new RadialAction { label = label, act = act, run = run });
        }

        void GatherActions()
        {
            RadialActions.Clear();
            // whatever the prompt offers right now
            if (Prompt != null)
                foreach (Match m in PromptKey.Matches(Controls.Localize(Prompt)))
                {
                    string label = m.Groups[2].Value.Trim().TrimEnd(',', '.', ' ');
                    if (label.Length == 0) continue;
                    var act = Controls.FromToken(m.Groups[1].Value);
                    if (act.HasValue) AddRadial(label, act);
                }
            if (Current)
            {
                var car = Current;
                AddRadial("LIGHTS", Controls.Act.Lights);
                if (car.awdSelectable) AddRadial(car.FourWheelDrive ? "2WD" : "4WD", Controls.Act.FourWheel);
                if (car.hasDiffLock) AddRadial(car.diffLocked ? "OPEN DIFF" : "LOCK DIFF", Controls.Act.DiffLock);
                var radio = car.GetComponent<MadMax.Audio.RadioReceiver>();
                AddRadial(radio && radio.on ? "RADIO OFF" : "RADIO ON", Controls.Act.RadioPower);
                if (radio && radio.on) AddRadial("NEXT STATION", Controls.Act.RadioNext);
                if (car.GetComponent<VehicleClimate>()) AddRadial("CLIMATE", Controls.Act.Climate);
                AddRadial("HORN", null, () => MadMax.Audio.Sfx.Play("horn", car.transform.position + car.transform.forward * 1.5f, 1f, 1f, 80f, 0.4f));
                AddRadial("RECOVER", Controls.Act.Recover);
                if (LaunchOptions.Dev && car.TryGetComponent<VehicleDamage>(out _)) AddRadial("REPAIR (DEV)", Controls.Act.DevRepair);
                // a tanker behind: pump from the cab
                foreach (var t in trailers)
                {
                    if (!t || !t.TryGetComponent<TowCoupling>(out var tc) || tc.Tower != car || !t.TryGetComponent<FuelTanker>(out var ft)) continue;
                    AddRadial(ft.Pumping == FuelTanker.Mode.Fill ? "STOP PUMP" : "FUEL FROM TANKER", null, () => ft.Toggle(FuelTanker.Mode.Fill));
                    AddRadial(ft.Pumping == FuelTanker.Mode.Drain ? "STOP DRAIN" : "DRAIN INTO TANKER", null, () => ft.Toggle(FuelTanker.Mode.Drain));
                }
            }
            else
            {
                // tools from the hotbar (the pad's way to switch)
                for (int i = 0; i < HotbarSize; i++)
                {
                    int slot = i;
                    if (Hotbar[slot] != null && (!Player.Tool || Player.Tool.id != Hotbar[slot]) && Hotbar[slot].StartsWith("tool_"))
                        AddRadial(ItemCatalog.Name(Hotbar[slot]), null, () => UseItem(Hotbar[slot]));
                }
                AddRadial("INVENTORY", Controls.Act.Inventory);
                AddRadial("SKILLS", Controls.Act.Skills);
                AddRadial("HEALTH", Controls.Act.Health);
                AddRadial("MAP", Controls.Act.Map);
            }
            if (RadialActions.Count > 12) RadialActions.RemoveRange(12, RadialActions.Count - 12);
        }
    }
}
