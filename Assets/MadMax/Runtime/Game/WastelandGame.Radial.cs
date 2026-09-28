using System.Collections.Generic;
using System.Text.RegularExpressions;
using MadMax.Vehicles;
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
        public struct RadialAction { public string label; public Key key; public System.Action run; }

        public bool RadialOpen { get; private set; }
        public int RadialHover { get; private set; } = -1;
        public readonly List<RadialAction> RadialActions = new List<RadialAction>();
        /// <summary>True while the radial owns the mouse (CameraRig skips mouse look).</summary>
        public static bool RadialBlocksLook;

        bool TabTapped;
        float tabHeld;
        Vector2 radialAim;
        Key injectedKey = Key.None;
        int injectedFrame = -1;

        static readonly Regex PromptKey = new Regex(@"\[([A-Z0-9]+)\]\s*([^\[]*)");

        bool Injected(Key k) => injectedFrame == Time.frameCount && injectedKey == k;
        bool KeyDown(Keyboard kb, Key k) => (kb != null && kb[k].wasPressedThisFrame) || Injected(k);

        void UpdateRadial(Keyboard kb, Mouse mouse)
        {
            TabTapped = false;
            if (kb == null || Menus.IsOpen || TitleSequence.Playing || (Build && Build.RadialOpen)) { CloseRadial(); return; }
            var tab = kb.tabKey;
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
            else { injectedKey = a.key; injectedFrame = Time.frameCount; }   // handled by the normal input code later this frame
        }

        void AddRadial(string label, Key key, System.Action run = null)
        {
            foreach (var r in RadialActions) if (r.label == label) return;
            RadialActions.Add(new RadialAction { label = label, key = key, run = run });
        }

        void GatherActions()
        {
            RadialActions.Clear();
            // whatever the prompt offers right now
            if (Prompt != null)
                foreach (Match m in PromptKey.Matches(Prompt))
                {
                    string label = m.Groups[2].Value.Trim().TrimEnd(',', '.', ' ');
                    if (label.Length == 0) continue;
                    if (System.Enum.TryParse<Key>(m.Groups[1].Value, true, out var key) && key != Key.None) AddRadial(label, key);
                }
            if (Current)
            {
                var car = Current;
                AddRadial("LIGHTS", Key.N);
                if (car.awdSelectable) AddRadial(car.FourWheelDrive ? "2WD" : "4WD", Key.X);
                if (car.hasDiffLock) AddRadial(car.diffLocked ? "OPEN DIFF" : "LOCK DIFF", Key.L);
                var radio = car.GetComponent<MadMax.Audio.RadioReceiver>();
                AddRadial(radio && radio.on ? "RADIO OFF" : "RADIO ON", Key.M);
                if (radio && radio.on) AddRadial("NEXT STATION", Key.Period);
                if (car.GetComponent<VehicleClimate>()) AddRadial("CLIMATE", Key.K);
                AddRadial("HORN", Key.None, () => MadMax.Audio.Sfx.Play("horn", car.transform.position + car.transform.forward * 1.5f, 1f, 1f, 80f, 0.4f));
                AddRadial("RECOVER", Key.T);
                if (car.TryGetComponent<VehicleDamage>(out _)) AddRadial("REPAIR", Key.G);
                // a tanker behind: pump from the cab
                foreach (var t in trailers)
                {
                    if (!t || !t.TryGetComponent<TowCoupling>(out var tc) || tc.Tower != car || !t.TryGetComponent<FuelTanker>(out var ft)) continue;
                    AddRadial(ft.Pumping == FuelTanker.Mode.Fill ? "STOP PUMP" : "FUEL FROM TANKER", Key.None, () => ft.Toggle(FuelTanker.Mode.Fill));
                    AddRadial(ft.Pumping == FuelTanker.Mode.Drain ? "STOP DRAIN" : "DRAIN INTO TANKER", Key.None, () => ft.Toggle(FuelTanker.Mode.Drain));
                }
            }
            else
            {
                AddRadial("INVENTORY", Key.I);
                AddRadial("SKILLS", Key.P);
                AddRadial("HEALTH", Key.O);
            }
            if (RadialActions.Count > 12) RadialActions.RemoveRange(12, RadialActions.Count - 12);
        }
    }
}
