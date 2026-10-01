using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Rebindable keyboard actions. Every gameplay key goes through here (<see cref="Down"/>, <see cref="Held"/>);
    /// bindings are saved in <see cref="GameSettings.keys"/> and edited on the CONTROLS page. Menu navigation (arrows,
    /// Enter, Esc), the hotbar digits and the gamepad stay fixed. Prompts written with the default letters ("[E] TAKE")
    /// are shown with the bound keys by <see cref="Localize"/>.</summary>
    public static class Controls
    {
        public enum Act
        {
            Forward, Back, Left, Right, Jump, Run, Crouch,
            Use, Second, Drop, Enter, Hitch, Service, Siphon, Build, Armour, Reload,
            Inventory, Skills, Health, Map, Help, View, CamLeft, CamRight, ZoomIn, ZoomOut, CamTiltUp, CamTiltDown,
            ShiftUp, ShiftDown, FourWheel, DiffLock, Lights, Horn, Nitrous, Recover, Dropper, Smoke, Climate,
            RadioPower, RadioPrev, RadioNext, VolumeDown, VolumeUp,
            BuildRotate, BuildDismantle, BuildUpgrade, BuildRepair, BuildPrevCategory, BuildNextCategory, BuildPrevPiece, BuildNextPiece,
            DevWeather, DevDropPart, DevRepair,
            ToolUp, ToolDown, ToolLeft, ToolRight, ToolA, ToolB,                // machines, cranes and aircraft (appended: saved maps keep their slots)
            Context,                                                            // context menu on what the cursor / crosshair is on (RMB tap too)
            Loot                                                                // pin / unpin the floating loot panels
        }

        public static readonly int Count = System.Enum.GetValues(typeof(Act)).Length;

        static readonly Key[] Defaults =
        {
            Key.W, Key.S, Key.A, Key.D, Key.Space, Key.LeftShift, Key.LeftCtrl,
            Key.E, Key.T, Key.Q, Key.F, Key.J, Key.G, Key.K, Key.B, Key.U, Key.R,
            Key.I, Key.P, Key.O, Key.M, Key.H, Key.V, Key.Z, Key.C, Key.Equals, Key.Minus, Key.PageUp, Key.PageDown,
            Key.E, Key.Q, Key.X, Key.L, Key.N, Key.Y, Key.LeftCtrl, Key.T, Key.B, Key.U, Key.K,
            Key.Slash, Key.Comma, Key.Period, Key.LeftBracket, Key.RightBracket,
            Key.Y, Key.X, Key.U, Key.R, Key.Comma, Key.Period, Key.LeftBracket, Key.RightBracket,
            Key.F9, Key.Backspace, Key.F10,
            Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.Q, Key.E,
            Key.Backquote, Key.L
        };

        /// <summary>Labels for the CONTROLS page.</summary>
        public static readonly string[] Labels =
        {
            "FORWARD / THROTTLE", "BACK / BRAKE", "LEFT", "RIGHT", "JUMP / HANDBRAKE / PULL UP", "RUN / WHEELIE / GALLOP", "CROUCH / PUSH DOWN",
            "USE / TAKE / TALK", "SECOND ACTION / TRADE", "DROP PART / TOOL", "ENTER / EXIT / RIDE", "HITCH TRAILER", "SERVICE VEHICLE", "SIPHON FUEL", "BUILD MODE", "WELD ARMOUR", "RELOAD",
            "INVENTORY", "SKILLS", "HEALTH", "MAP & JOURNAL", "HELP SHEET", "CAMERA VIEW", "CAMERA LEFT", "CAMERA RIGHT", "ZOOM IN", "ZOOM OUT", "CAMERA TILT UP", "CAMERA TILT DOWN",
            "SHIFT UP", "SHIFT DOWN", "4WD", "DIFF LOCK", "LIGHTS", "HORN", "NITROUS", "RECOVER / PARLEY", "REAR DROPPER", "SMOKE SCREEN", "CLIMATE",
            "RADIO POWER", "RADIO TUNE DOWN", "RADIO TUNE UP", "RADIO VOLUME DOWN", "RADIO VOLUME UP",
            "BUILD: ROTATE", "BUILD: DISMANTLE", "BUILD: UPGRADE", "BUILD: REPAIR", "BUILD: PREV CATEGORY", "BUILD: NEXT CATEGORY", "BUILD: PREV PIECE", "BUILD: NEXT PIECE",
            "DEV: WEATHER", "DEV: DROP PART", "DEV: INSTANT REPAIR",
            "TOOL UP / NOSE", "TOOL DOWN / NOSE", "TOOL LEFT / RUDDER", "TOOL RIGHT / RUDDER", "TOOL A: CURL, GRAB / ROLL LEFT", "TOOL B: DUMP / ROLL RIGHT",
            "CONTEXT MENU (ALSO RMB TAP)", "LOOT PANELS (PIN / UNPIN)"
        };

        static Key[] map;
        static Act injected;
        static int injectedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { map = null; injectedFrame = -1; }

        static Key[] Map
        {
            get
            {
                if (map != null) return map;
                map = (Key[])Defaults.Clone();
                var saved = GameSettings.Current.keys;
                if (!string.IsNullOrEmpty(saved))
                    foreach (var pair in saved.Split(';'))
                    {
                        var kv = pair.Split('=');
                        if (kv.Length == 2 && System.Enum.TryParse<Act>(kv[0], out var a) && System.Enum.TryParse<Key>(kv[1], out var k)) map[(int)a] = k;
                    }
                return map;
            }
        }

        public static Key Of(Act a) => Map[(int)a];
        public static Key Default(Act a) => Defaults[(int)a];

        /// <summary>Pressed this frame (or fired from the action wheel).</summary>
        public static bool Down(Act a)
        {
            if (injectedFrame == Time.frameCount && injected == a) return true;
            if (IsDev(a) && !LaunchOptions.Dev) return false;
            var kb = Keyboard.current; var k = Of(a);
            return kb != null && k != Key.None && kb[k].wasPressedThisFrame;
        }

        public static bool Held(Act a)
        {
            if (IsDev(a) && !LaunchOptions.Dev) return false;
            var kb = Keyboard.current; var k = Of(a);
            return kb != null && k != Key.None && kb[k].isPressed;
        }

        public static bool Up(Act a)
        {
            var kb = Keyboard.current; var k = Of(a);
            return kb != null && k != Key.None && kb[k].wasReleasedThisFrame;
        }

        static bool IsDev(Act a) => a >= Act.DevWeather && a <= Act.DevRepair;

        /// <summary>Fire an action this frame as if its key was pressed (action wheel).</summary>
        public static void Inject(Act a) { injected = a; injectedFrame = Time.frameCount; }

        public static void Set(Act a, Key k)
        {
            Map[(int)a] = k;
            Save();
        }

        public static void ResetAll() { map = (Key[])Defaults.Clone(); Save(); }

        static void Save()
        {
            var parts = new List<string>();
            for (int i = 0; i < Count; i++) if (map[i] != Defaults[i]) parts.Add((Act)i + "=" + map[i]);
            GameSettings.Current.keys = string.Join(";", parts);
            GameSettings.Current.Save();
        }

        /// <summary>Another action on the same key in the same situation (the CONTROLS page warns).</summary>
        public static Act? Clash(Act a)
        {
            var k = Of(a);
            if (k == Key.None) return null;
            for (int i = 0; i < Count; i++)
            {
                var b = (Act)i;
                if (b != a && Of(b) == k && Group(a) == Group(b)) return b;
            }
            return null;
        }

        /// <summary>Situations: actions in different groups may share a key (E = use on foot, shift up in a car).</summary>
        public static int Group(Act a) =>
            a <= Act.Crouch ? 0 : a <= Act.ZoomOut ? 1 : a <= Act.Climate ? 2 : a <= Act.VolumeUp ? 3 : a <= Act.BuildNextPiece ? 4 : 5;

        public static string Name(Act a) => KeyName(Of(a));

        public static string KeyName(Key k)
        {
            switch (k)
            {
                case Key.None: return "-";
                case Key.Space: return "SPACE";
                case Key.LeftShift: return "SHIFT";
                case Key.RightShift: return "R-SHIFT";
                case Key.LeftCtrl: return "CTRL";
                case Key.RightCtrl: return "R-CTRL";
                case Key.LeftAlt: return "ALT";
                case Key.Comma: return ",";
                case Key.Period: return ".";
                case Key.Slash: return "/";
                case Key.Semicolon: return ";";
                case Key.Quote: return "'";
                case Key.LeftBracket: return "[";
                case Key.RightBracket: return "]";
                case Key.Minus: return "-";
                case Key.Equals: return "=";
                case Key.Backquote: return "`";
                case Key.Backspace: return "BKSP";
                case Key.UpArrow: return "UP";
                case Key.DownArrow: return "DOWN";
                case Key.LeftArrow: return "LEFT";
                case Key.RightArrow: return "RIGHT";
                case Key.PageUp: return "PGUP";
                case Key.PageDown: return "PGDN";
                case Key.Backslash: return "\\";
                default:
                    var s = k.ToString().ToUpperInvariant();
                    return s.StartsWith("DIGIT") ? s.Substring(5) : s;
            }
        }

        // prompts are written with the default keys; show the player's bindings instead
        static readonly Act[] PromptActs = { Act.Use, Act.Second, Act.Drop, Act.Enter, Act.Hitch, Act.Service, Act.Siphon, Act.Armour, Act.Reload, Act.Build, Act.Jump, Act.Run, Act.Crouch, Act.Map };
        static readonly Regex Token = new Regex(@"\[([A-Z0-9]+)\]");

        public static string Localize(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return prompt;
            var m0 = Map;
            return Token.Replace(prompt, m =>
            {
                foreach (var a in PromptActs)
                    if (KeyName(Default(a)) == m.Groups[1].Value) return m0[(int)a] == Default(a) ? m.Value : "[" + Name(a) + "]";
                return m.Value;
            });
        }

        /// <summary>The prompt action whose bound key is shown as <paramref name="token"/> (action wheel).</summary>
        public static Act? FromToken(string token)
        {
            foreach (var a in PromptActs) if (Name(a) == token) return a;
            return null;
        }

        /// <summary>The key pressed this frame, if any (the CONTROLS page's rebind).</summary>
        public static bool TryReadKey(out Key key)
        {
            key = Key.None;
            var kb = Keyboard.current;
            if (kb == null) return false;
            foreach (var c in kb.allKeys)
                if (c != null && c.wasPressedThisFrame) { key = c.keyCode; return true; }
            return false;
        }
    }
}
