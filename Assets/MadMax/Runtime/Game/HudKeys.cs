using System.Collections.Generic;
using System.Text.RegularExpressions;
using MadMax.Rendering;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Key help drawn on the HUD canvas: the F1 / help keyboard (every key bound in the current situation lit
    /// by group — moving, actions, vehicle, tool, build, interface — with a legend) and the quiet prompt list on the
    /// left (one row per "[KEY] action", a small icon where the action has one).</summary>
    public static class HudKeys
    {
        // ------------------------------------------------------------------ icons (7x7)
        static readonly Dictionary<string, string> Icons = new Dictionary<string, string>
        {
            { "drive", "0111110100000110111011110111100100110000010111110" },
            { "fuel", "0001000001110001111100111110111111111111110111110" },
            { "siphon", "1100000010000001111100000010000001000111000001110" },
            { "wrench", "0110000100100010001000101110000011100000110000001" },
            { "talk", "1111111100000110000011111111001100001000000000000" },
            { "cash", "0011100011111011010111110111110101101111100011100" },
            { "sleep", "1111000001000001000001111111000001000001000001111" },
            { "hand", "0010100011011001101101111110111111001111100011100" },
            { "door", "1111100100010010001001001100100010010001001111100" },
            { "hammer", "1111100111110000110000011000001100000110000011000" },
            { "chain", "0110000100100010011000110110001100100010010000110" },
            { "eat", "0001000011011011111111111111111111101111100011100" },
            { "book", "1110111101010110101011010101101010111101110001000" },
        };

        static readonly (string icon, string[] words)[] IconWords =
        {
            ("drive", new[] { "DRIVE", "RIDE", "FLY", "SAIL", "HELM" }), ("fuel", new[] { "REFUEL", "FUEL", "FILL", "PUMP" }),
            ("siphon", new[] { "SIPHON", "DRAIN" }), ("wrench", new[] { "TAKE", "MOUNT", "REPAIR", "SERVICE", "FIX", "WRENCH", "DISMANTLE", "WELD", "TUNE" }),
            ("talk", new[] { "TALK", "PARLEY", "ASK" }), ("cash", new[] { "TRADE", "BUY", "SELL", "PAY", "TOLL" }), ("sleep", new[] { "SLEEP", "REST" }),
            ("door", new[] { "EXIT", "ENTER", "DOOR", "BOARD", "LEAVE", "GET IN" }), ("hammer", new[] { "BUILD", "CRAFT", "BENCH", "RESEARCH" }),
            ("chain", new[] { "HITCH", "TOW", "WINCH", "COUPLE" }), ("eat", new[] { "EAT", "DRINK", "COOK" }), ("book", new[] { "READ", "WATCH", "STUDY" }),
            ("hand", new[] { "OPEN", "USE", "SEARCH", "LOOT", "PICK", "COLLECT", "GRAB", "FEED", "MILK", "BUTCHER" }),
        };

        static string IconFor(string label)
        {
            foreach (var (icon, words) in IconWords) foreach (var w in words) if (label.Contains(w)) return icon;
            return null;
        }

        static void DrawIcon(PixelCanvas c, string icon, int x, int y, Color32 col)
        {
            if (icon == null || !Icons.TryGetValue(icon, out var bits)) return;
            for (int r = 0; r < 7; r++)
            for (int k = 0; k < 7; k++)
                if (bits[r * 7 + k] == '1') c.Set(x + k, y + r, col);
        }

        // ------------------------------------------------------------------ prompt list
        static readonly Regex Entry = new Regex(@"\[([^\]]+)\]\s*([^\[]*)");

        /// <summary>The context prompt as a small list on the left, bottom row at <paramref name="bottom"/>.</summary>
        public static void DrawPromptList(PixelCanvas c, string prompt, int x, int bottom, Color32 text, Color32 dim, Color32 accent)
        {
            if (string.IsNullOrEmpty(prompt)) return;
            int first = prompt.IndexOf('[');
            string head = (first < 0 ? prompt : prompt.Substring(0, first)).Trim();
            var rows = new List<(string key, string label)>();
            foreach (Match m in Entry.Matches(prompt)) rows.Add((m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim()));
            int w = head.Length > 0 ? PixelCanvas.TextWidth(head) : 0;
            foreach (var (key, label) in rows) w = Mathf.Max(w, PixelCanvas.TextWidth(key) + 4 + 10 + PixelCanvas.TextWidth(label));
            int h = rows.Count * 10 + (head.Length > 0 ? 9 : 0) + 4;
            int y = bottom - h;
            c.Rect(x - 2, y, w + 8, h, new Color32(8, 10, 10, 120));
            int ry = y + 3;
            if (head.Length > 0) { c.Text(x + 1, ry, head, dim, 1, false); ry += 9; }
            foreach (var (key, label) in rows)
            {
                int kw = PixelCanvas.TextWidth(key) + 4;
                c.Frame(x, ry - 1, kw, 9, accent);
                c.Text(x + 2, ry + 1, key, accent, 1, false);
                var icon = IconFor(label);
                DrawIcon(c, icon, x + kw + 2, ry, dim);
                c.Text(x + kw + (icon != null ? 11 : 3), ry + 1, label, text, 1, false);
                ry += 10;
            }
        }

        // ------------------------------------------------------------------ keyboard
        struct Cap { public Key key; public string label; public float w; public Cap(Key k, string l, float w = 1f) { key = k; label = l; this.w = w; } }

        static readonly Cap[][] Rows =
        {
            new[] { new Cap(Key.Escape, "ESC"), new Cap(Key.None, "", 0.5f), new Cap(Key.F1, "F1"), new Cap(Key.F2, "F2"), new Cap(Key.F3, "F3"), new Cap(Key.F4, "F4"), new Cap(Key.None, "", 0.5f),
                    new Cap(Key.F5, "F5"), new Cap(Key.F6, "F6"), new Cap(Key.F7, "F7"), new Cap(Key.F8, "F8"), new Cap(Key.None, "", 0.5f), new Cap(Key.F9, "F9"), new Cap(Key.F10, "F10"), new Cap(Key.F11, "F11"), new Cap(Key.F12, "F12") },
            new[] { new Cap(Key.Backquote, "`"), new Cap(Key.Digit1, "1"), new Cap(Key.Digit2, "2"), new Cap(Key.Digit3, "3"), new Cap(Key.Digit4, "4"), new Cap(Key.Digit5, "5"), new Cap(Key.Digit6, "6"),
                    new Cap(Key.Digit7, "7"), new Cap(Key.Digit8, "8"), new Cap(Key.Digit9, "9"), new Cap(Key.Digit0, "0"), new Cap(Key.Minus, "-"), new Cap(Key.Equals, "="), new Cap(Key.Backspace, "BKSP", 2f) },
            new[] { new Cap(Key.Tab, "TAB", 1.5f), new Cap(Key.Q, "Q"), new Cap(Key.W, "W"), new Cap(Key.E, "E"), new Cap(Key.R, "R"), new Cap(Key.T, "T"), new Cap(Key.Y, "Y"), new Cap(Key.U, "U"),
                    new Cap(Key.I, "I"), new Cap(Key.O, "O"), new Cap(Key.P, "P"), new Cap(Key.LeftBracket, "["), new Cap(Key.RightBracket, "]"), new Cap(Key.Backslash, "", 1.5f) },
            new[] { new Cap(Key.CapsLock, "CAPS", 1.75f), new Cap(Key.A, "A"), new Cap(Key.S, "S"), new Cap(Key.D, "D"), new Cap(Key.F, "F"), new Cap(Key.G, "G"), new Cap(Key.H, "H"), new Cap(Key.J, "J"),
                    new Cap(Key.K, "K"), new Cap(Key.L, "L"), new Cap(Key.Semicolon, ";"), new Cap(Key.Quote, "'"), new Cap(Key.Enter, "ENTER", 2.25f) },
            new[] { new Cap(Key.LeftShift, "SHIFT", 2.25f), new Cap(Key.Z, "Z"), new Cap(Key.X, "X"), new Cap(Key.C, "C"), new Cap(Key.V, "V"), new Cap(Key.B, "B"), new Cap(Key.N, "N"), new Cap(Key.M, "M"),
                    new Cap(Key.Comma, ","), new Cap(Key.Period, "."), new Cap(Key.Slash, "/"), new Cap(Key.RightShift, "SHIFT", 2.75f) },
            new[] { new Cap(Key.LeftCtrl, "CTRL", 1.5f), new Cap(Key.None, "", 0.25f), new Cap(Key.LeftAlt, "ALT", 1.5f), new Cap(Key.Space, "SPACE", 6.5f), new Cap(Key.RightAlt, "ALT", 1.5f), new Cap(Key.None, "", 0.25f), new Cap(Key.RightCtrl, "CTRL", 1.5f) },
        };

        enum Grp { Move, Action, Vehicle, Tool, Build, Ui }
        static readonly Color32[] GrpColor = { Pal.Moss[4], Pal.Accent, Pal.PaleBlue[3], Pal.Ochre[4], Pal.Pink[3], Pal.Cream[2] };
        static readonly string[] GrpName = { "MOVE", "ACTIONS", "VEHICLE", "TOOL", "BUILD", "GAME" };

        struct Bind { public Key key; public string label; public Grp grp; }

        static void Add(List<Bind> b, Controls.Act a, string label, Grp g) => b.Add(new Bind { key = Controls.Of(a), label = label, grp = g });
        static void Add(List<Bind> b, Key k, string label, Grp g) => b.Add(new Bind { key = k, label = label, grp = g });

        /// <summary>What every key does right now, and a title for the situation.</summary>
        static List<Bind> Bindings(WastelandGame g, out string title)
        {
            var b = new List<Bind>();
            var car = g.Current;
            if (g.Build && g.Build.Active && !car)
            {
                title = "BUILDING";
                Add(b, Controls.Act.BuildRotate, "ROTATE", Grp.Build); Add(b, Controls.Act.BuildDismantle, "DISMANTLE", Grp.Build);
                Add(b, Controls.Act.BuildUpgrade, "UPGRADE", Grp.Build); Add(b, Controls.Act.BuildRepair, "REPAIR", Grp.Build);
                Add(b, Controls.Act.BuildPrevCategory, "PREV CATEGORY", Grp.Build); Add(b, Controls.Act.BuildNextCategory, "NEXT CATEGORY", Grp.Build);
                Add(b, Controls.Act.BuildPrevPiece, "PREV PIECE", Grp.Build); Add(b, Controls.Act.BuildNextPiece, "NEXT PIECE", Grp.Build);
                Add(b, Controls.Act.Build, "PUT AWAY (HOLD: BUILD MENU)", Grp.Build);
                MoveKeys(b, "WALK");
            }
            else if (car && car.GetComponent<FlightModel>())
            {
                title = "FLYING " + WastelandGame.Name(car);
                Add(b, Controls.Act.Forward, "THROTTLE UP", Grp.Vehicle); Add(b, Controls.Act.Back, "THROTTLE DOWN / BRAKE", Grp.Vehicle);
                bool stick = GameSettings.Current.flightStick;
                Add(b, Controls.Act.ToolUp, stick ? "NOSE DOWN" : "NOSE UP", Grp.Tool); Add(b, Controls.Act.ToolDown, stick ? "NOSE UP" : "NOSE DOWN", Grp.Tool);
                Add(b, Controls.Act.ToolA, "ROLL LEFT", Grp.Tool); Add(b, Controls.Act.ToolB, "ROLL RIGHT", Grp.Tool);
                Add(b, Controls.Act.ToolLeft, "RUDDER LEFT", Grp.Tool); Add(b, Controls.Act.ToolRight, "RUDDER RIGHT", Grp.Tool);
                Add(b, Controls.Act.Left, "RUDDER LEFT", Grp.Tool); Add(b, Controls.Act.Right, "RUDDER RIGHT", Grp.Tool);
                Add(b, Controls.Act.Jump, "PULL UP", Grp.Tool); Add(b, Controls.Act.Crouch, "PUSH DOWN", Grp.Tool);
                Add(b, Controls.Act.Enter, "GET OUT", Grp.Action); Add(b, Controls.Act.Lights, "LIGHTS", Grp.Vehicle);
            }
            else if (car)
            {
                title = "DRIVING " + WastelandGame.Name(car);
                Add(b, Controls.Act.Forward, "THROTTLE", Grp.Move); Add(b, Controls.Act.Back, "BRAKE / REVERSE", Grp.Move);
                Add(b, Controls.Act.Left, "STEER LEFT", Grp.Move); Add(b, Controls.Act.Right, "STEER RIGHT", Grp.Move);
                Add(b, Controls.Act.Jump, "HANDBRAKE", Grp.Move);
                bool tool = g.ToolKeys;
                if (GameSettings.Current.manualTransmission)
                {
                    Add(b, Controls.Act.ShiftUp, tool ? "SHIFT+: GEAR UP" : "GEAR UP", Grp.Vehicle); Add(b, Controls.Act.ShiftDown, tool ? "SHIFT+: GEAR DOWN" : "GEAR DOWN", Grp.Vehicle);
                }
                Add(b, Controls.Act.FourWheel, "4WD", Grp.Vehicle); Add(b, Controls.Act.DiffLock, "DIFF LOCK", Grp.Vehicle);
                Add(b, Controls.Act.Lights, "LIGHTS", Grp.Vehicle); Add(b, Controls.Act.Horn, "HORN", Grp.Vehicle);
                Add(b, Controls.Act.Nitrous, "NITROUS", Grp.Vehicle); Add(b, Controls.Act.Recover, "RECOVER / PARLEY", Grp.Vehicle);
                Add(b, Controls.Act.Dropper, "REAR DROPPER", Grp.Vehicle); Add(b, Controls.Act.Smoke, "SMOKE SCREEN", Grp.Vehicle);
                Add(b, Controls.Act.Climate, "CLIMATE", Grp.Vehicle); Add(b, Controls.Act.Enter, "GET OUT", Grp.Action);
                Add(b, Controls.Act.Hitch, "HITCH TRAILER", Grp.Action); Add(b, Controls.Act.Service, "SERVICE", Grp.Action);
                Add(b, Controls.Act.RadioPower, "RADIO", Grp.Vehicle); Add(b, Controls.Act.RadioPrev, "TUNE DOWN", Grp.Vehicle); Add(b, Controls.Act.RadioNext, "TUNE UP", Grp.Vehicle);
                Add(b, Controls.Act.VolumeDown, "VOLUME DOWN", Grp.Vehicle); Add(b, Controls.Act.VolumeUp, "VOLUME UP", Grp.Vehicle);
                if (tool) ToolBindings(b, car);
            }
            else
            {
                title = "ON FOOT";
                MoveKeys(b, "WALK");
                Add(b, Controls.Act.Jump, "JUMP / CLIMB", Grp.Move); Add(b, Controls.Act.Run, "RUN", Grp.Move); Add(b, Controls.Act.Crouch, "CROUCH / SLIDE", Grp.Move);
                Add(b, Controls.Act.Use, "USE / TAKE / TALK", Grp.Action); Add(b, Controls.Act.Second, "SECOND ACTION", Grp.Action);
                Add(b, Controls.Act.Drop, "DROP PART / TOOL", Grp.Action); Add(b, Controls.Act.Enter, "GET IN / RIDE", Grp.Action);
                Add(b, Controls.Act.Build, "BUILD MENU", Grp.Build); Add(b, Controls.Act.Reload, "RELOAD / RESEARCH", Grp.Action);
                Add(b, Controls.Act.Hitch, "HITCH", Grp.Action); Add(b, Controls.Act.Service, "SERVICE", Grp.Action);
                Add(b, Controls.Act.Siphon, "SIPHON", Grp.Action); Add(b, Controls.Act.Armour, "WELD ARMOUR", Grp.Action);
                for (int i = 0; i < 8; i++) Add(b, Key.Digit1 + i, "HOTBAR 1-8", Grp.Action);
            }
            Add(b, Controls.Act.Inventory, "INVENTORY", Grp.Ui); Add(b, Controls.Act.Skills, "SKILLS", Grp.Ui);
            Add(b, Controls.Act.Health, "HEALTH", Grp.Ui); Add(b, Controls.Act.Map, "MAP & JOURNAL", Grp.Ui);
            Add(b, Controls.Act.View, "CAMERA VIEW", Grp.Ui); Add(b, Controls.Act.CamLeft, "TURN CAMERA", Grp.Ui); Add(b, Controls.Act.CamRight, "TURN CAMERA", Grp.Ui);
            Add(b, Controls.Act.ZoomIn, "ZOOM IN", Grp.Ui); Add(b, Controls.Act.ZoomOut, "ZOOM OUT", Grp.Ui);
            Add(b, Controls.Act.CamTiltUp, "TILT CAMERA", Grp.Ui); Add(b, Controls.Act.CamTiltDown, "TILT CAMERA", Grp.Ui);
            Add(b, Controls.Act.Help, "THIS KEY MAP", Grp.Ui); Add(b, Key.F1, "THIS KEY MAP", Grp.Ui);
            Add(b, Controls.Act.Context, "CONTEXT MENU (RMB TAP)", Grp.Ui);
            if (!car) Add(b, Controls.Act.Loot, "LOOT PANELS (PIN)", Grp.Ui);
            Add(b, Key.Escape, "MENU", Grp.Ui); Add(b, Key.Tab, "FLEET (HOLD: WHEEL)", Grp.Ui);
            return b;
        }

        static void MoveKeys(List<Bind> b, string verb)
        {
            Add(b, Controls.Act.Forward, verb + " FORWARD", Grp.Move); Add(b, Controls.Act.Back, verb + " BACK", Grp.Move);
            Add(b, Controls.Act.Left, verb + " LEFT", Grp.Move); Add(b, Controls.Act.Right, verb + " RIGHT", Grp.Move);
        }

        static void ToolBindings(List<Bind> b, VehicleDriver car)
        {
            if (car.TryGetComponent<Machine>(out var m) && m.enabled && m.kind != Machine.Kind.Roller)
            {
                switch (m.kind)
                {
                    case Machine.Kind.Excavator:
                        Tool(b, "BOOM UP", "BOOM DOWN", "SWING LEFT", "SWING RIGHT", "CURL / DIG", "OPEN / DUMP"); Add(b, Controls.Act.Run, "SHIFT+UP/DN: STICK", Grp.Tool); break;
                    case Machine.Kind.Backhoe:
                        Tool(b, "ARMS UP", "ARMS DOWN", "HOE SWING (SHIFT)", "HOE SWING (SHIFT)", "CURL", "DUMP"); Add(b, Controls.Act.Run, "SHIFT: REAR HOE", Grp.Tool); break;
                    case Machine.Kind.Dozer: Tool(b, "BLADE UP", "BLADE DOWN", "BLADE ANGLE", "BLADE ANGLE", "BLADE PITCH", "BLADE PITCH"); break;
                    case Machine.Kind.DumpTruck: Tool(b, "RAISE BED", "LOWER BED", null, null, null, null); break;
                    case Machine.Kind.Paver: Tool(b, "SCREED UP", "SCREED DOWN", null, null, "PAVE ON / OFF", "MATERIAL"); break;
                }
            }
            else if (car.TryGetComponent<Crane>(out var cr) && cr.CranePart)
            {
                Tool(b, "HOIST UP", "HOIST DOWN", "SLEW LEFT", "SLEW RIGHT", "GRAB / RELEASE", null); Add(b, Controls.Act.Run, "SHIFT+UP/DN: BOOM", Grp.Tool);
            }
        }

        static void Tool(List<Bind> b, string up, string down, string left, string right, string a, string bb)
        {
            if (up != null) Add(b, Controls.Act.ToolUp, up, Grp.Tool);
            if (down != null) Add(b, Controls.Act.ToolDown, down, Grp.Tool);
            if (left != null) Add(b, Controls.Act.ToolLeft, left, Grp.Tool);
            if (right != null) Add(b, Controls.Act.ToolRight, right, Grp.Tool);
            if (a != null) Add(b, Controls.Act.ToolA, a, Grp.Tool);
            if (bb != null) Add(b, Controls.Act.ToolB, bb, Grp.Tool);
        }

        static readonly string[] ArrowBits =
        {
            "0001000001110011111110001000000100000010000001000",  // up
            "0001000000100000010000001000111111100111000001000",  // down
            "0001000001100011111111111111011000000100000000000",  // left
            "0001000000110011111111111111000110000010000000000",  // right
        };

        /// <summary>The keyboard map for the current situation: bound keys lit by group, a legend below.</summary>
        public static void DrawKeyboard(PixelCanvas c, WastelandGame g)
        {
            var binds = Bindings(g, out string title);
            var byKey = new Dictionary<Key, Bind>();
            foreach (var bd in binds) if (bd.key != Key.None && !byKey.ContainsKey(bd.key)) byKey[bd.key] = bd;

            const int U = 14, Gap = 1;
            int kbW = Mathf.RoundToInt(15f * U) + 4 * U, x0 = (c.w - kbW) / 2, y0 = 22;
            int panelH = 6 * (U + Gap) + 28;
            // legend size
            var legend = new List<Bind>();
            var seen = new HashSet<string>();
            foreach (var grp in System.Enum.GetValues(typeof(Grp)))
                foreach (var bd in binds)
                    if (bd.grp == (Grp)grp && bd.key != Key.None && seen.Add(bd.label)) legend.Add(bd);
            int cols = 3, perCol = Mathf.CeilToInt(legend.Count / (float)cols);
            var colX = new int[cols + 1];
            for (int col = 0; col < cols; col++)
            {
                int widest = 0;
                for (int i = col * perCol; i < Mathf.Min(legend.Count, (col + 1) * perCol); i++)
                    widest = Mathf.Max(widest, KeyColumn(legend[i]) + PixelCanvas.TextWidth(legend[i].label));
                colX[col + 1] = colX[col] + widest + 14;
            }
            int panelW = Mathf.Max(kbW, colX[cols]);
            int legendH = perCol * 8 + 22;
            x0 = (c.w - panelW) / 2;
            c.Panel(x0 - 8, y0 - 12, panelW + 16, panelH + legendH + 8);
            c.Text(x0 - 2, y0 - 8, "KEYS: " + title, Pal.Accent);
            string hint = "F1 CLOSE   REBIND: ESC > SETTINGS > CONTROLS";
            c.Text(x0 + panelW - PixelCanvas.TextWidth(hint), y0 + panelH + legendH - 6, hint, Pal.MutedInk, 1, false);
            x0 += (panelW - kbW) / 2;

            int y = y0 + 2;
            foreach (var row in Rows)
            {
                float cx = x0;
                foreach (var cap in row)
                {
                    int w = Mathf.RoundToInt(cap.w * U) - Gap;
                    if (cap.key != Key.None) DrawCap(c, (int)cx, y, w, U - Gap, cap.label, byKey.TryGetValue(cap.key, out var bd) ? GrpColor[(int)bd.grp] : (Color32?)null, -1);
                    cx += cap.w * U;
                }
                y += U + Gap;
            }
            // navigation cluster: page up / down over the arrows
            int ax = x0 + 15 * U + U / 2, ay = y0 + 2 + 2 * (U + Gap);
            DrawCap(c, ax, ay, 2 * U - Gap, U - Gap, "PGUP", Lit(byKey, Key.PageUp), -1);
            DrawCap(c, ax + 2 * U, ay, 2 * U - Gap, U - Gap, "PGDN", Lit(byKey, Key.PageDown), -1);
            ay = y0 + 2 + 4 * (U + Gap);
            DrawCap(c, ax + U, ay, U - Gap, U - Gap, "", Lit(byKey, Key.UpArrow), 0);
            ay += U + Gap;
            DrawCap(c, ax, ay, U - Gap, U - Gap, "", Lit(byKey, Key.LeftArrow), 2);
            DrawCap(c, ax + U, ay, U - Gap, U - Gap, "", Lit(byKey, Key.DownArrow), 1);
            DrawCap(c, ax + 2 * U, ay, U - Gap, U - Gap, "", Lit(byKey, Key.RightArrow), 3);

            // legend: group-coloured rows, three columns
            int ly = y0 + panelH - 6;
            for (int i = 0; i < legend.Count; i++)
            {
                var bd = legend[i];
                int col = i / Mathf.Max(1, perCol), row = i % Mathf.Max(1, perCol);
                int lx = (c.w - Mathf.Max(kbW, colX[cols])) / 2 + colX[col], yy = ly + row * 8;
                string k = Controls.KeyName(bd.key);
                c.Rect(lx, yy + 1, 3, 3, GrpColor[(int)bd.grp]);
                c.Text(lx + 6, yy, k, GrpColor[(int)bd.grp], 1, false);
                c.Text(lx + KeyColumn(bd), yy, bd.label, Pal.Ink, 1, false);
            }
        }

        static int KeyColumn(Bind bd) => 6 + Mathf.Max(24, PixelCanvas.TextWidth(Controls.KeyName(bd.key)) + 5);

        static Color32? Lit(Dictionary<Key, Bind> byKey, Key k) => byKey.TryGetValue(k, out var bd) ? GrpColor[(int)bd.grp] : (Color32?)null;

        static void DrawCap(PixelCanvas c, int x, int y, int w, int h, string label, Color32? lit, int arrow)
        {
            var fill = lit.HasValue ? Color32.Lerp(new Color32(20, 16, 14, 255), lit.Value, 0.35f) : new Color32(24, 20, 18, 235);
            var edge = lit ?? new Color32(70, 60, 52, 255);
            c.Rect(x, y, w, h, fill);
            c.Frame(x, y, w, h, edge);
            var ink = lit ?? new Color32(110, 96, 84, 255);
            if (arrow >= 0)
            {
                var bits = ArrowBits[arrow];
                int ix = x + (w - 7) / 2, iy = y + (h - 7) / 2;
                for (int r = 0; r < 7; r++) for (int k = 0; k < 7; k++) if (bits[r * 7 + k] == '1') c.Set(ix + k, iy + r, ink);
                return;
            }
            if (string.IsNullOrEmpty(label)) return;
            int tw = PixelCanvas.TextWidth(label);
            c.Text(x + Mathf.Max(1, (w - tw) / 2), y + (h - 5) / 2, label, ink, 1, false);
        }
    }
}
