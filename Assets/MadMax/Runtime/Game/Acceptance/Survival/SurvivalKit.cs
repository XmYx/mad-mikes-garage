using System;
using System.Collections;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MadMax.Game.Acceptance
{
    /// <summary>Result of a wait: whether the condition came true, and after how long (real seconds, frames).</summary>
    public sealed class Waited { public bool ok; public float seconds; public int frames; }

    /// <summary>Shared fixtures for the survival, base, world and UI scenarios: frame-aware waits (real time, so they
    /// also run while a menu pauses the world), on-foot pads, pieces, first-person aiming for build mode and [E] use,
    /// pinned weather, ledgers for conservation checks. Every shortcut a scenario takes is disclosed by the scenario.</summary>
    public static class SurvivalKit
    {
        /// <summary>Yield frames until <paramref name="cond"/> holds or <paramref name="seconds"/> of real time pass.</summary>
        public static IEnumerator Until(Func<bool> cond, float seconds, Waited w = null)
        {
            float t0 = Time.realtimeSinceStartup; int f0 = Time.frameCount;
            bool ok = cond();
            while (!ok && Time.realtimeSinceStartup - t0 < seconds) { yield return null; ok = cond(); }
            if (w != null) { w.ok = ok; w.seconds = Time.realtimeSinceStartup - t0; w.frames = Time.frameCount - f0; }
        }

        /// <summary>Let <paramref name="seconds"/> of game time pass (at least <paramref name="minFrames"/> frames; a slow
        /// editor still simulates the same span), capped at ten times that in real time.</summary>
        public static IEnumerator GameSeconds(float seconds, int minFrames = 3)
        {
            float t0 = Time.time, r0 = Time.realtimeSinceStartup; int f0 = Time.frameCount;
            while ((Time.time - t0 < seconds || Time.frameCount - f0 < minFrames) && Time.realtimeSinceStartup - r0 < seconds * 10f + 5f) yield return null;
        }

        public static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// <summary>On foot, standing on a level clear pad near the start (a disclosed teleport).</summary>
        public static IEnumerator OnFoot(ScenarioContext c, float radius, Waited got, Vector3[] pad)
        {
            var g = c.Game;
            if (g.Current) { g.Exit(); yield return null; }
            if (g.Menus && g.Menus.IsOpen) g.Menus.Close();
            got.ok = TestWorld.Pad(radius, out var p);
            if (!got.ok) yield break;
            g.Player.Teleport(p + Vector3.up * 0.2f, 0f);
            pad[0] = p;
            c.Fixture($"on foot at a clear level pad ({p.x:0},{p.z:0})");
            yield return GameSeconds(0.4f, 5);
        }

        public static Vector3 Ground(Vector3 p) { var t = DeformableTerrain.Instance; p.y = t.Height(p.x, p.z); return p; }

        /// <summary>A built piece set down at <paramref name="at"/> (on the ground), facing <paramref name="face"/>.</summary>
        public static Placeable Piece(WastelandGame g, string id, Vector3 at, Vector3 face, bool onGround = true)
        {
            if (onGround) at = Ground(at);
            var root = g.Build.Structures;
            face.y = 0f;
            var rot = Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face.normalized : Vector3.forward);
            var p = FurnitureLibrary.Spawn(id, root, root.InverseTransformPoint(at), Quaternion.Inverse(root.rotation) * rot, g.propMaterial);
            if (p) { p.owner = g.Stats.name; _ = p.Id; }
            return p;
        }

        /// <summary>Turn the player (body and first-person view) toward a world point.</summary>
        public static void Face(WastelandGame g, Vector3 point)
        {
            var eye = g.Player.Eye.position;
            var d = point - eye;
            var flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude > 0.0001f) g.Player.transform.rotation = Quaternion.LookRotation(flat.normalized);
            g.Player.viewYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            if (g.cameraRig) g.cameraRig.LookToward(d);
        }

        /// <summary>First-person view (build mode aims through the screen centre there).</summary>
        public static void FirstPerson(WastelandGame g) { if (g.cameraRig && g.cameraRig.mode != ViewMode.FirstPerson) g.cameraRig.mode = ViewMode.FirstPerson; }

        /// <summary>Build mode with the claw hammer in hand: select <paramref name="id"/>, look at <paramref name="target"/>
        /// until the ghost is valid, then place it as the click does (pays the cost). The placed piece lands in
        /// <paramref name="placed"/>[0]; build mode's status line is noted when it fails.</summary>
        public static IEnumerator Build(ScenarioContext c, string id, Vector3 target, Placeable[] placed, Func<bool> extra = null)
        {
            var g = c.Game;
            placed[0] = null;
            FirstPerson(g);
            if (!g.Build.Active) g.Build.SetActive(true);
            g.Build.Select(id);
            var w = new Waited();
            yield return Until(() => { Face(g, target); return g.Build.Current != null && g.Build.Current.id == id && g.Build.Valid && (extra == null || extra()); }, 6f, w);
            if (!w.ok) { c.Note($"build {id}: not placeable at {target} ({g.Build.Status}, selected {(g.Build.Current != null ? g.Build.Current.id : "-")})"); yield break; }
            int before = Placeable.All.Count;
            placed[0] = g.Build.TryPlace();
            yield return null;
        }

        /// <summary>[E] (or [T]) on the thing in front, through the game's own interaction path. True when the focused
        /// target was <paramref name="target"/> as the key went down.</summary>
        public static IEnumerator Use(WastelandGame g, Component target, bool second, Waited w)
        {
            w.ok = false;
            var wf = new Waited();
            yield return Until(() => { Face(g, target.transform.position + Vector3.up * 0.5f); return g.Focused is Component f && f && (f.gameObject == target.gameObject || f.transform.IsChildOf(target.transform)); }, 3f, wf);
            if (!wf.ok) yield break;
            var press = ActionPress.Press(second ? Controls.Act.Second : Controls.Act.Use);
            yield return Until(() => press == null || press.Frame >= 0, 2f);
            yield return null;
            w.ok = true;
        }

        /// <summary>Walk (W, view turned toward it) until within <paramref name="stop"/> m of a point on the ground.</summary>
        public static IEnumerator WalkTo(WastelandGame g, Vector3 to, float stop, Waited w)
        {
            var P = g.Player;
            float Dist() { var d = to - P.transform.position; d.y = 0f; return d.magnitude; }
            yield return Until(() =>
            {
                var d = to - P.transform.position; d.y = 0f;
                P.viewYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                P.moveInput = Dist() > stop ? new Vector2(0f, 1f) : Vector2.zero;
                return Dist() <= stop;
            }, 8f, w);
            P.moveInput = Vector2.zero;
            yield return GameSeconds(0.2f);
        }

        /// <summary>Press a game action for one frame (as its key would), then let the frame run.</summary>
        public static IEnumerator Press(Controls.Act a)
        {
            var p = ActionPress.Press(a);
            yield return Until(() => !p || p.Frame >= 0, 2f);
            yield return null;
        }

        // ------------------------------------------------------------------ ledgers

        /// <summary>Every resource and item count of an inventory ("res:N" / item id → count), for conservation checks.</summary>
        public static Dictionary<string, int> Ledger(params Inventory[] invs)
        {
            var d = new Dictionary<string, int>();
            foreach (var inv in invs)
            {
                if (inv == null) continue;
                for (int t = 1; t < ResourceInfo.Count; t++) { int n = inv.Get((ResourceType)t); if (n != 0) { d.TryGetValue("res:" + t, out int h); d["res:" + t] = h + n; } }
                foreach (var kv in inv.Items) if (kv.Value != 0) { d.TryGetValue(kv.Key, out int h); d[kv.Key] = h + kv.Value; }
            }
            return d;
        }

        /// <summary>First difference between two ledgers ("key a -> b"), or null when they balance.</summary>
        public static string Diff(Dictionary<string, int> a, Dictionary<string, int> b)
        {
            foreach (var k in a.Keys) { b.TryGetValue(k, out int y); if (a[k] != y) return k + " " + a[k] + " -> " + y; }
            foreach (var k in b.Keys) if (!a.ContainsKey(k) && b[k] != 0) return k + " 0 -> " + b[k];
            return null;
        }

        /// <summary>The first placed piece of <paramref name="id"/> within <paramref name="radius"/> of a point.</summary>
        public static Placeable Near(string id, Vector3 at, float radius)
        {
            Placeable best = null; float bd = radius * radius;
            foreach (var p in Placeable.All)
            {
                if (!p || p.id != id) continue;
                float d = (p.transform.position - at).sqrMagnitude;
                if (d <= bd) { bd = d; best = p; }
            }
            return best;
        }
    }

    /// <summary>Holds the weather where a scenario wants it (rain on/off, air temperature, ground wetness) until
    /// destroyed; the automatic weather cycle is off meanwhile. A scene reload (the next scenario's fresh world)
    /// destroys it and the cycle comes back.</summary>
    public class WeatherPin : MonoBehaviour
    {
        public bool raining;
        public float temperature = float.NaN, wetness = float.NaN;

        public static WeatherPin Set(bool raining, float temperature = float.NaN, float wetness = float.NaN)
        {
            var p = FindAnyObjectByType<WeatherPin>();
            if (!p) p = new GameObject("WeatherPin").AddComponent<WeatherPin>();
            p.raining = raining; p.temperature = temperature; p.wetness = wetness;
            p.Apply();
            return p;
        }

        public static void Release() { var p = FindAnyObjectByType<WeatherPin>(); if (p) Destroy(p.gameObject); Weather.Auto = true; }

        void Apply()
        {
            Weather.Auto = false;
            Weather.Raining = raining;
            Weather.Restore(raining, float.IsNaN(wetness) ? Weather.Wetness : wetness, Weather.Snow, float.IsNaN(temperature) ? Weather.BaseTemperature : temperature, Weather.LakeRise);
        }

        void LateUpdate() => Apply();
        void OnDestroy() => Weather.Auto = true;
    }

    /// <summary>A virtual keyboard for UI scenarios: key presses go through the Input System as real key events (the
    /// production path: menus read <see cref="Keyboard.current"/>). In the editor the game view may not have focus, so
    /// keyboard events are routed to the game while it is in use and the setting is put back afterwards.</summary>
    public static class VirtualKeys
    {
        static Keyboard kb;
        static bool settingsChanged;
        static InputSettings.BackgroundBehavior oldBackground;
#if UNITY_EDITOR
        static InputSettings.EditorInputBehaviorInPlayMode oldEditor;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { kb = null; settingsChanged = false; }

        public static Keyboard Device
        {
            get
            {
                if (kb == null || !kb.added)
                {
                    kb = InputSystem.AddDevice<Keyboard>("AcceptanceKeyboard");
                    try
                    {
                        var s = InputSystem.settings;
                        if (!settingsChanged)
                        {
                            oldBackground = s.backgroundBehavior;
#if UNITY_EDITOR
                            oldEditor = s.editorInputBehaviorInPlayMode;
                            s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                            s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                            settingsChanged = true;
                        }
                    }
                    catch (Exception e) { Debug.LogWarning("[acceptance] input settings: " + e.Message); }
                }
                return kb;
            }
        }

        /// <summary>Press <paramref name="key"/> (held for <paramref name="frames"/> frames), then release it.</summary>
        public static IEnumerator Tap(Key key, int frames = 1)
        {
            var d = Device;
            d.MakeCurrent();
            InputSystem.QueueStateEvent(d, new KeyboardState(key));
            for (int i = 0; i < Mathf.Max(1, frames); i++) yield return null;
            InputSystem.QueueStateEvent(d, new KeyboardState());
            yield return null;
        }

        /// <summary>True when a press reaches the game: the key reads as held one frame after it was queued.</summary>
        public static IEnumerator Probe(Waited w)
        {
            var d = Device;
            d.MakeCurrent();
            InputSystem.QueueStateEvent(d, new KeyboardState(Key.F12));
            yield return null;
            yield return null;
            w.ok = Keyboard.current == d && d.f12Key.isPressed;
            InputSystem.QueueStateEvent(d, new KeyboardState());
            yield return null;
        }

        /// <summary>Remove the virtual keyboard and restore the input settings.</summary>
        public static void Remove()
        {
            if (kb != null && kb.added) InputSystem.RemoveDevice(kb);
            kb = null;
            if (!settingsChanged) return;
            try
            {
                var s = InputSystem.settings;
                s.backgroundBehavior = oldBackground;
#if UNITY_EDITOR
                s.editorInputBehaviorInPlayMode = oldEditor;
#endif
            }
            catch (Exception) { }
            settingsChanged = false;
        }

        /// <summary>Move the menu cursor (arrow keys) to the first entry whose label starts with <paramref name="label"/>
        /// at or after index <paramref name="from"/>. False when there is none or the cursor never gets there.</summary>
        public static IEnumerator Select(MenuSystem m, string label, Waited w, int from = 0)
        {
            w.ok = false;
            var labels = m.Labels();
            int target = -1;
            for (int i = Mathf.Max(0, from); i < labels.Count; i++) if (labels[i] != null && labels[i].StartsWith(label)) { target = i; break; }
            if (target < 0) yield break;
            for (int guard = 0; guard < labels.Count + 2 && m.Cursor != target; guard++)
                yield return Tap(target > m.Cursor ? Key.DownArrow : Key.UpArrow);
            w.ok = m.Cursor == target;
        }
    }
}
