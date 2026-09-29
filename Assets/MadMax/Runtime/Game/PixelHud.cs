using System.Collections.Generic;
using MadMax.Items;
using MadMax.Rendering;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.UI;

namespace MadMax.Game
{
    /// <summary>Screen HUD drawn at the pixel-art resolution: vehicle panel, parts damage map, minimap, weather, prompts.</summary>
    [DefaultExecutionOrder(200)]
    public class PixelHud : MonoBehaviour
    {
        public float minimapMetresPerPixel = 4f;

        /// <summary>The HUD canvas (menus map the mouse onto it).</summary>
        public static PixelCanvas Canvas { get; private set; }

        /// <summary>Current HUD image (same size as the pixel target), for screenshots.</summary>
        public Texture2D Texture => canvas != null ? canvas.texture : null;

        WastelandGame game;
        CameraRig rig;
        PixelCanvas canvas;
        RawImage image;
        GameObject overlay;
        Color32[] map; int mapSize; float mapHalf;

        static readonly Color32 Text = new Color32(255, 226, 170, 255), Dim = new Color32(190, 140, 90, 255), Amber = new Color32(255, 170, 50, 255),
            Red = new Color32(235, 50, 35, 255), Green = new Color32(130, 210, 90, 255), Empty = new Color32(70, 45, 30, 255);

        public void Init(WastelandGame g, CameraRig r)
        {
            game = g; rig = r;
            overlay = new GameObject("PixelHud");
            overlay.transform.SetParent(transform, false);
            var c = overlay.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = -990;
            var img = new GameObject("Image").AddComponent<RawImage>();
            img.transform.SetParent(overlay.transform, false);
            img.raycastTarget = false;
            var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            image = img;
            BuildMinimap(g.World, 512);
        }

        void BuildMinimap(WorldGen world, int size)
        {
            mapSize = size; mapHalf = world.halfSize;
            map = new Color32[size * size];
            float mpp = 2f * mapHalf / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float wx = -mapHalf + (x + 0.5f) * mpp, wz = -mapHalf + (y + 0.5f) * mpp;
                float b = world.Basin(wx, wz);
                float h = world.BaseHeight(wx, wz);
                byte shade = (byte)Mathf.Clamp(120 + h * 2.2f, 70, 170);
                map[y * size + x] = b > 0.5f ? new Color32(70, 42, 30, 255) : new Color32(shade, (byte)(shade * 0.55f), (byte)(shade * 0.3f), 255);
            }
            foreach (var road in world.roads.roads)
            {
                var col = road.paved ? new Color32(40, 34, 32, 255) : new Color32(120, 70, 40, 255);
                for (int i = 1; i < road.points.Count; i++)
                {
                    var a = ToMap(road.points[i - 1]); var b = ToMap(road.points[i]);
                    int n = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f) + 1;
                    for (int k = 0; k <= n; k++)
                    {
                        var p = Vector2.Lerp(a, b, k / (float)n);
                        int px = Mathf.RoundToInt(p.x), py = Mathf.RoundToInt(p.y);
                        if (px >= 0 && py >= 0 && px < size && py < size) map[py * size + px] = col;
                    }
                }
            }
            foreach (var t in world.roads.towns)
            {
                var m = ToMap(new Vector3(t.x, 0, t.y));
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                {
                    int px = (int)m.x + dx, py = (int)m.y + dy;
                    if (px >= 0 && py >= 0 && px < size && py < size) map[py * size + px] = new Color32(200, 90, 40, 255);
                }
            }
        }

        Vector2 ToMap(Vector3 w) => new Vector2((w.x + mapHalf) / (2f * mapHalf) * mapSize, (w.z + mapHalf) / (2f * mapHalf) * mapSize);

        void LateUpdate()
        {
            if (!game || !rig || !rig.pixel || !rig.pixel.Target) return;
            var t = rig.pixel.Target;
            // the HUD stays at the chosen pixel height even when the world renders at full resolution (vector mode)
            int hh = GameSettings.Current.PixelHeight, hw = Mathf.Max(1, Mathf.RoundToInt(hh * t.width / (float)t.height));
            if (canvas == null || canvas.w != hw || canvas.h != hh)
            {
                canvas = new PixelCanvas(hw, hh);
                image.texture = canvas.texture;
            }
            canvas.Clear(new Color32(0, 0, 0, 0));
            Canvas = canvas;
            if (TitleSequence.Playing || (game.Menus && game.Menus.Current == MenuSystem.Page.Main)) { if (game.Menus && game.Menus.IsOpen) game.Menus.Draw(canvas); canvas.Upload(); return; }
            var car = game.Current;
            bool fps = rig.mode == ViewMode.FirstPerson;

            DrawMinimap(canvas.w - 70, 6, 64);
            DrawWeather(canvas.w - 70, 74);
            var net = MadMax.Net.NetSession.Instance;
            if (net && net.Online) canvas.Text(canvas.w - 70, 94, $"{net.Status} {net.PlayerCount}P", Dim);
            string where = car ? WastelandGame.Name(car) : game.Player.Interior ? "IN " + WastelandGame.Name(game.Player.Interior) : "ON FOOT";
            canvas.Text(6, 6, where, Text);
            string clock = Mathf.FloorToInt(MadMax.World.DayNight.Hours).ToString("00") + ":" + Mathf.FloorToInt(MadMax.World.DayNight.Hours % 1f * 60f).ToString("00");
            canvas.Text(6, 13, "VIEW " + ViewName(rig.mode) + "  " + game.CurrentBiome.ToString().ToUpperInvariant() + "  " + clock, Dim);
            if (game.RadiationLevel > 0.02f && (Time.time * (2f + game.RadiationLevel * 6f)) % 1f > 0.35f)
                canvas.Text(canvas.w / 2 - 20, 34, "RADIATION " + Mathf.RoundToInt(game.RadiationLevel * 100), new Color32(156, 255, 58, 255));
            if (!car && game.Player.Swimming) canvas.Text(canvas.w / 2 - 16, 26, "SWIMMING", new Color32(150, 190, 255, 255));
            DrawResources(6, 22);
            if (!car && !(game.Build && game.Build.Active) && !(game.Menus && game.Menus.IsOpen)) DrawToolbar();
            if (!car) DrawVitals(6, canvas.h - 16);
            DrawTemperatureVignette();
            if (game.LearningId != null) DrawLearning();
            if (game.Build && game.Build.RadialOpen) DrawRadial();
            if (game.RadialOpen) DrawActionRadial();
            if (!car && (rig.mode == ViewMode.ThirdPerson || fps))
            {
                int cx = canvas.w / 2, cy = canvas.h / 2;
                canvas.Rect(cx - 3, cy, 2, 1, Text); canvas.Rect(cx + 2, cy, 2, 1, Text);
                canvas.Rect(cx, cy - 3, 1, 2, Text); canvas.Rect(cx, cy + 2, 1, 2, Text);
            }

            if (car)
            {
                int sy = 30;
                void Line(string st) { if (st == null) return; int mw = PixelCanvas.TextWidth(st) + 8; canvas.Panel((canvas.w - mw) / 2, sy, mw, 11); canvas.Text((canvas.w - mw) / 2 + 4, sy + 3, st, Amber); sy += 12; }
                if (car.TryGetComponent<MadMax.Vehicles.Machine>(out var mach)) Line(mach.Status);
                if (car.TryGetComponent<MadMax.Vehicles.Winch>(out var wn) && wn.WinchPart) Line(wn.Status);
                if (car.TryGetComponent<MadMax.Vehicles.Crane>(out var cr) && cr.CranePart) Line(cr.Status);
                if (car.TryGetComponent<MadMax.Vehicles.VehicleWeapons>(out var vw) && vw.Armed) Line(vw.Status);
                if (car.TryGetComponent<MadMax.Vehicles.VehicleTuning>(out var tn) && (tn.nitrous > 0 || tn.NitrousOn)) Line(tn.NitrousOn ? "NITROUS!" : "NOS X" + tn.nitrous + "  [CTRL]");
                if (car.TryGetComponent<MadMax.Vehicles.VehicleClimate>(out var cl) && cl.Enclosed) canvas.Text(canvas.w - 70, 102, "CABIN " + Mathf.RoundToInt(cl.CabinTemperature) + "C" + (cl.on ? "" : " OFF"), Dim);
            }
            if (car && !fps)
            {
                DrawVehiclePanel(car, 6, canvas.h - 38);
                DrawPartsMap(car, canvas.w - 44, canvas.h - 70);
            }

            string prompt = game.RadialOpen ? null : game.Prompt;
            if (prompt != null)
            {
                int w = PixelCanvas.TextWidth(prompt) + 8;
                int x = (canvas.w - w) / 2, y = canvas.h - (car && !fps ? 52 : 40);
                canvas.Panel(x, y, w, 11);
                canvas.Text(x + 4, y + 3, prompt, Amber);
            }
            if (!car && game.Build && game.Build.Active) DrawBuildPanel();
            DrawRadio(car);
            if (game.ShowHelp) DrawHelp();
            var toast = game.ToastText;
            if (toast != null)
            {
                int w = PixelCanvas.TextWidth(toast) + 10;
                canvas.Panel((canvas.w - w) / 2, 44, w, 12);
                canvas.Text((canvas.w - w) / 2 + 5, 48, toast, Text);
            }
            if (game.Menus) game.Menus.Draw(canvas);
            canvas.Upload();
        }

        void DrawResources(int x, int y)
        {
            var inv = game.Inventory;
            foreach (var type in ResourceInfo.HudOrder)
            {
                if (type == ResourceType.Fuel) x += 6;
                canvas.Rect(x, y + 1, 3, 3, ResourceInfo.Color(type));
                int n = inv.Get(type);
                x += 5 + canvas.Text(x + 5, y, ResourceInfo.IsFluid(type) ? n + "L" : n.ToString(), n > 0 ? Text : Dim) + 4;
            }
        }

        /// <summary>8-slot hotbar along the bottom centre.</summary>
        void DrawToolbar()
        {
            const int slot = 22;
            int n = WastelandGame.HotbarSize, x0 = (canvas.w - n * (slot + 2)) / 2, y = canvas.h - slot - 4;
            for (int i = 0; i < n; i++)
            {
                int x = x0 + i * (slot + 2);
                var id = game.Hotbar[i];
                bool sel = id != null && game.Player.Tool && game.Player.Tool.id == id;
                canvas.Rect(x, y, slot, slot, sel ? new Color32(110, 60, 25, 220) : new Color32(20, 12, 8, 170));
                canvas.Text(x + 1, y + 1, (i + 1).ToString(), sel ? Amber : Dim, 1, false);
                if (id == null) continue;
                canvas.Blit(x + 4, y + 1, 14, Icon(id));
                if (id == game.LearningId) canvas.Rect(x + 2, y + 13, Mathf.RoundToInt(18 * game.LearningProgress), 1, Green);
                string name = MadMax.Items.ItemCatalog.Name(id).Replace("VHS: ", "");
                if (name.Length > 5) name = name.Substring(0, 5);
                canvas.Text(x + 1, y + 15, name, sel ? Amber : Text, 1, false);
                int count = game.Inventory.GetItem(id);
                if (count > 1) canvas.Text(x + slot - PixelCanvas.TextWidth(count.ToString()) - 1, y + 1, count.ToString(), Text, 1, false);
                if (ToolLibrary.Has(id))
                {
                    // condition of the tool in hand: green → amber → red
                    float cond = game.Condition(id);
                    if (cond < 0.999f)
                    {
                        var cc = cond > 0.5f ? Green : cond > 0.2f ? Amber : new Color32(220, 60, 40, 255);
                        canvas.Rect(x + 2, y + slot - 2, slot - 4, 1, new Color32(10, 6, 4, 220));
                        canvas.Rect(x + 2, y + slot - 2, Mathf.Max(1, Mathf.RoundToInt((slot - 4) * cond)), 1, cc);
                    }
                }
            }
            if (game.Player.Tool is RangedTool rt)
            {
                // rounds in the gun / in the pack, and what it is doing
                string st = game.Rounds(rt.id) + "/" + rt.magazine + " +" + rt.Reserve(game) + (rt.Jammed ? "  JAMMED [R]" : rt.Reloading ? "  RELOADING" : "");
                canvas.Text(x0 + n * (slot + 2) + 4, y + 8, st, rt.Jammed ? Red : rt.Reloading ? Amber : Text);
            }
            if (game.Aiming && game.AimScreen.x >= 0f)
            {
                int cx = Mathf.RoundToInt(game.AimScreen.x * canvas.w), cy = Mathf.RoundToInt((1f - game.AimScreen.y) * canvas.h);
                var col = game.Player.Tool is RangedTool gr && (gr.Jammed || gr.Reloading) ? Red : Amber;
                canvas.Rect(cx - 5, cy, 3, 1, col); canvas.Rect(cx + 3, cy, 3, 1, col); canvas.Rect(cx, cy - 5, 1, 3, col); canvas.Rect(cx, cy + 3, 1, 3, col);
            }
            if (GeigerTool.Reading >= 0f)
            {
                float r = GeigerTool.Reading;
                string s = "RAD " + (r * 10f).ToString("0.0");
                canvas.Text(x0 + n * (slot + 2) + 4, y + 1, s, r > 0.5f ? new Color32(220, 60, 40, 255) : r > 0.15f ? Amber : Green);
            }
            if (MetalDetectorTool.Reading >= 0f)
            {
                float r = MetalDetectorTool.Reading;
                string s = r < 0.05f ? "ORE: NOTHING" : "ORE: " + ResourceInfo.Name(MetalDetectorTool.Kind).Replace(" ORE", "") + " " + Mathf.RoundToInt(r * 100f) + "%";
                canvas.Text(x0 + n * (slot + 2) + 4, y + 1, s, r > 0.5f ? Green : r > 0.15f ? Amber : Dim);
            }
            if (BinocularsTool.Looking) DrawSpotting();
            if (FishingRodTool.Active && FishingRodTool.Active.Busy) DrawFishing(FishingRodTool.Active);
        }

        /// <summary>Fishing: the rod's status line, and the line tension bar while a fish is on (snaps at the red mark).</summary>
        void DrawFishing(FishingRodTool rod)
        {
            string s = rod.Status ?? "";
            bool hooked = rod.phase == FishingRodTool.Phase.Hooked;
            int w = Mathf.Max(PixelCanvas.TextWidth(s) + 8, 90);
            int x = (canvas.w - w) / 2, y = canvas.h - 68;
            var red = new Color32(230, 70, 40, 255);
            canvas.Panel(x, y, w, hooked ? 20 : 11);
            canvas.Text(x + 4, y + 3, s, rod.phase == FishingRodTool.Phase.Bite ? red : Amber);
            if (!hooked) return;
            int bw = w - 8, bx = x + 4, by = y + 13;
            canvas.Rect(bx, by, bw, 4, new Color32(10, 6, 4, 230));
            var col = rod.Tension > 0.85f ? red : rod.Tension > 0.6f ? Amber : rod.Tension < 0.1f ? Dim : Green;
            canvas.Rect(bx, by, Mathf.Max(1, Mathf.RoundToInt(bw * rod.Tension)), 4, col);
            canvas.Rect(bx + Mathf.RoundToInt(bw * 0.9f), by - 1, 1, 6, red);
        }

        /// <summary>Binoculars: name what is in view out to 350 m (people, vehicles, landmarks) at its screen position.</summary>
        void DrawSpotting()
        {
            var cam = rig ? rig.pixel.GetComponent<Camera>() : null;
            if (!cam) return;
            var eye = cam.transform.position;
            void Tag(Vector3 world, string text, Color32 col)
            {
                var sp = cam.WorldToViewportPoint(world);
                if (sp.z <= 0f || sp.x < 0f || sp.x > 1f || sp.y < 0f || sp.y > 1f) return;
                int px = Mathf.RoundToInt(sp.x * canvas.w), py = Mathf.RoundToInt((1f - sp.y) * canvas.h);
                string d = Mathf.RoundToInt(Vector3.Distance(world, game.Player.transform.position)) + "M";
                canvas.Text(px - PixelCanvas.TextWidth(text) / 2, py - 10, text, col);
                canvas.Text(px - PixelCanvas.TextWidth(d) / 2, py - 3, d, Dim);
            }
            foreach (var npc in MadMax.Npc.Npc.All)
                if (npc && npc.Alive && (npc.transform.position - eye).sqrMagnitude < 350f * 350f)
                    Tag(npc.transform.position + Vector3.up * 2f, npc.Profile.Name, npc.Hostile ? new Color32(220, 60, 40, 255) : npc.Profile.Vendor ? Green : Text);
            foreach (var v in game.AllVehicles)
                if (v && v != game.Current && (v.transform.position - eye).sqrMagnitude < 350f * 350f && v.aiDriven)
                    Tag(v.transform.position + Vector3.up * 2.5f, v.name.Replace("(Clone)", "").ToUpperInvariant(), Amber);
            var near = new System.Collections.Generic.List<MadMax.World.Site>();
            game.World.SitesNear(game.Player.transform.position, 400f, near);
            foreach (var s in near)
            {
                var p = new Vector3(s.pos.x, DeformableTerrainHeight(s.pos) + 4f, s.pos.y);
                Tag(p, s.kind == MadMax.World.SiteKind.Bunker ? "BUNKER" : "ROCK TUNNEL", new Color32(160, 200, 255, 255));
            }
        }

        static float DeformableTerrainHeight(Vector2 p) { var t = MadMax.World.DeformableTerrain.Instance; return t ? t.Height(p.x, p.y) : 0f; }

        /// <summary>Hotbar icon: the item's own voxel mesh (tool, kit furniture) or a small item model.</summary>
        static Color32[] Icon(string id)
        {
            Mesh mesh;
            if (ToolLibrary.Has(id)) mesh = ToolLibrary.MeshFor(id);
            else
            {
                mesh = null;
                foreach (var d in MadMax.Building.FurnitureLibrary.All) if (d.kit == id) { mesh = d.mesh; break; }
                if (!mesh) mesh = MadMax.Items.ItemModels.Get(id);
            }
            return MadMax.Rendering.IconRenderer.Get(id, mesh, 14, ToolLibrary.Has(id));
        }

        /// <summary>Frost / heat creeping in from the screen edges when the core temperature leaves the safe band.</summary>
        void DrawTemperatureVignette()
        {
            float t = game.Stats.bodyTemp;
            float cold = Mathf.Clamp01((36f - t) / 3f), hot = Mathf.Clamp01((t - 38f) / 2.5f);
            float k = Mathf.Max(cold, hot);
            if (k <= 0f) return;
            var col = cold > 0f ? new Color32(200, 225, 255, 0) : new Color32(200, 40, 20, 0);
            int depth = Mathf.RoundToInt(canvas.h * 0.25f * k);
            for (int i = 0; i < depth; i++)
            {
                byte a = (byte)(Mathf.Clamp01(1f - i / (float)depth) * 150f * k);
                if (((i * 7) % 3) == 0 && cold > 0f) a = (byte)(a * 1.3f);            // frosty streaks
                col.a = a;
                canvas.Rect(0, i, canvas.w, 1, col); canvas.Rect(0, canvas.h - 1 - i, canvas.w, 1, col);
                canvas.Rect(i, 0, 1, canvas.h, col); canvas.Rect(canvas.w - 1 - i, 0, 1, canvas.h, col);
            }
        }

        // 7x7 cozy icons: o outline, h highlight, other letters = fill (see IconColor)
        static readonly string[] HeartIcon = { ".oo.oo.", "oHhoHHo", "oHHHHHo", "oHHHHHo", ".oHHHo.", "..oHo..", "...o..." };
        static readonly string[] BoltIcon  = { "...ooo.", "..oYYo.", ".oYYo..", "oYYYYo.", ".ooYYo.", "..oYo..", "..oo..." };
        static readonly string[] AppleIcon = { "...bGG.", "..obG..", ".oRbRRo", "oRhRRRo", "oRRRRRo", ".oRRRo.", "..ooo.." };
        static readonly string[] DropIcon  = { "...o...", "..oBo..", ".oBBBo.", "oBhBBBo", "oBhBBBo", ".oBBBo.", "..ooo.." };
        static readonly string[] SoapIcon  = { "w..w...", "..w..ww", ".ooooo.", "oPPPPPo", "oPhhPPo", "oPPPPPo", ".ooooo." };
        static readonly string[] ThermoIcon = { "..o....", ".oTo...", ".oTo...", ".oTo...", "oTTTo..", "oTTTo..", ".ooo..." };

        static Color32 IconColor(char c) => c switch
        {
            'o' => new Color32(46, 26, 16, 255),
            'h' => new Color32(255, 236, 214, 255),
            'H' => new Color32(222, 70, 70, 255),
            'Y' => new Color32(250, 214, 80, 255),
            'R' => new Color32(214, 64, 48, 255),
            'G' => new Color32(110, 190, 80, 255),
            'b' => new Color32(120, 78, 42, 255),
            'B' => new Color32(90, 160, 235, 255),
            'P' => new Color32(240, 170, 190, 255),
            'w' => new Color32(210, 235, 255, 255),
            'T' => new Color32(230, 90, 70, 255),
            _ => new Color32(0, 0, 0, 0),
        };

        void Icon(int x, int y, string[] rows)
        {
            for (int j = 0; j < rows.Length; j++)
                for (int i = 0; i < rows[j].Length; i++)
                    if (rows[j][i] != '.') canvas.Set(x + i, y + j, IconColor(rows[j][i]));
        }

        /// <summary>One stat as its own little card: icon, rounded bar, pulses when low.</summary>
        void StatCard(int x, int y, string[] icon, float t, Color32 col, bool low)
        {
            const int w = 58;
            var bg = new Color32(24, 14, 9, 215); var edge = new Color32(70, 46, 30, 255);
            canvas.Rect(x + 1, y, w + 12, 11, bg); canvas.Rect(x, y + 1, w + 14, 9, bg);          // soft rounded card
            canvas.Rect(x + 1, y, w + 12, 1, edge); canvas.Rect(x + 1, y + 10, w + 12, 1, edge);
            bool blink = low && (Time.unscaledTime * 2.5f) % 1f < 0.5f;
            Icon(x + 2, y + 2, icon);
            if (blink) canvas.Rect(x + 1, y + 1, 9, 9, new Color32(255, 60, 40, 70));
            int bx = x + 11, by = y + 4, fill = Mathf.RoundToInt(w * Mathf.Clamp01(t));
            canvas.Rect(bx + 1, by, w - 2, 3, new Color32(8, 5, 3, 255)); canvas.Rect(bx, by + 1, w, 1, new Color32(8, 5, 3, 255));
            if (fill > 0)
            {
                var c = low ? Red : col;
                canvas.Rect(bx + 1, by, Mathf.Max(1, fill - 2), 3, c); canvas.Rect(bx, by + 1, fill, 1, c);
                canvas.Rect(bx + 1, by, Mathf.Max(1, fill - 2), 1, new Color32((byte)Mathf.Min(255, c.r + 50), (byte)Mathf.Min(255, c.g + 50), (byte)Mathf.Min(255, c.b + 50), 255));   // gloss
            }
        }

        void DrawVitals(int x, int y)
        {
            var st = game.Stats;
            bool needs = game.Rules.survival;
            int n = needs ? 5 : 2, step = 13;
            int top = y + 8 - n * step;
            int row = 0;
            StatCard(x, top + step * row++, HeartIcon, st.health / Mathf.Max(1f, st.MaxHealth), new Color32(214, 70, 70, 255), st.health < st.MaxHealth * 0.25f);
            StatCard(x, top + step * row++, BoltIcon, st.stamina / Mathf.Max(1f, st.MaxStamina), new Color32(120, 200, 100, 255), game.Vitals && game.Vitals.Exhausted);
            if (needs)
            {
                StatCard(x, top + step * row++, AppleIcon, st.hunger / 100f, new Color32(220, 150, 70, 255), st.hunger < 20f);
                StatCard(x, top + step * row++, DropIcon, st.thirst / 100f, new Color32(90, 160, 235, 255), st.thirst < 20f);
                StatCard(x, top + step * row++, SoapIcon, st.hygiene / 100f, new Color32(236, 176, 196, 255), st.hygiene < 25f);
            }
            int tx = x + 78;
            var tc = st.bodyTemp < 35.5f ? new Color32(120, 170, 255, 255) : st.bodyTemp > 38.5f ? Red : Dim;
            Icon(tx, top + 2, ThermoIcon);
            canvas.Text(tx + 7, top + 3, st.bodyTemp.ToString("0.0") + "C", tc);
            if (game.Player.Encumbered) canvas.Text(tx, top + step + 3, "OVERLOADED", Red);
            if (st.sick > 0f) canvas.Text(tx, top + step * 2 + 3, "SICK", new Color32(160, 220, 80, 255));
            foreach (var inj in st.injuries) if (inj.Bleeding) { if ((Time.time * 2f) % 1f > 0.4f) canvas.Text(tx, top + step * 3 + 3, "BLEEDING  O", Red); break; }
            // buffs and the latrine need, one short tag each
            string tags = (st.rested ? "RESTED " : "") + (st.fed ? "FED " : "") + (st.wetness > 0.3f ? "WET " : "") + (game.Coughing ? "COUGH " : "") + (needs && st.waste >= 100f ? "LATRINE" : "");
            if (tags.Length > 0) canvas.Text(tx, top + step * 4 + 3, tags.TrimEnd(), st.waste >= 100f && needs ? new Color32(210, 170, 90, 255) : new Color32(150, 210, 150, 255));
        }

        void Bar(int x, int y, int w, float t, Color32 col, string label)
        {
            canvas.Text(x, y, label, Dim);
            canvas.Rect(x + 12, y + 1, w, 4, new Color32(20, 12, 8, 200));
            canvas.Rect(x + 12, y + 1, Mathf.RoundToInt(w * Mathf.Clamp01(t)), 4, col);
        }

        void DrawLearning()
        {
            string label = game.LearningName;
            int w = Mathf.Max(90, PixelCanvas.TextWidth(label) + 8), x = (canvas.w - w) / 2, y = 60;
            canvas.Panel(x, y, w, 16);
            canvas.Text(x + 4, y + 3, label, Text);
            canvas.Rect(x + 4, y + 11, w - 8, 2, new Color32(20, 12, 8, 200));
            canvas.Rect(x + 4, y + 11, Mathf.RoundToInt((w - 8) * game.LearningProgress), 2, Green);
        }

        /// <summary>Hold-B radial build picker: categories on the inner ring, their pieces on the outer ring.</summary>
        void DrawRadial()
        {
            var b = game.Build;
            var cats = BuildMode.Categories;
            int cx = canvas.w / 2, cy = canvas.h / 2;
            int r1 = Mathf.RoundToInt(canvas.h * 0.13f), r2 = Mathf.Min(canvas.h / 2 - 16, Mathf.RoundToInt(canvas.h * 0.38f));
            canvas.Rect(0, 0, canvas.w, canvas.h, new Color32(10, 5, 3, 120));
            for (int i = 0; i < cats.Length; i++)
            {
                float a = i * Mathf.PI * 2f / cats.Length;
                int x = cx + Mathf.RoundToInt(Mathf.Sin(a) * r1), y = cy - Mathf.RoundToInt(Mathf.Cos(a) * r1);
                string name = cats[i].ToString().ToUpperInvariant();
                int w = PixelCanvas.TextWidth(name) + 6;
                bool sel = i == b.RadialCategory;
                if (sel) canvas.Panel(x - w / 2, y - 5, w, 11); else canvas.Rect(x - w / 2, y - 5, w, 11, new Color32(20, 12, 8, 200));
                canvas.Text(x - w / 2 + 3, y - 2, name, sel ? Amber : Dim);
            }
            var all = MadMax.Building.FurnitureLibrary.InCategory(cats[b.RadialCategory]);
            int hover = b.RadialHover;
            for (int i = 0; i < all.Count; i++)
            {
                float a = i * Mathf.PI * 2f / all.Count;
                int x = cx + Mathf.RoundToInt(Mathf.Sin(a) * r2), y = cy - Mathf.RoundToInt(Mathf.Cos(a) * r2 * 0.85f);
                string name = all[i].name;
                int w = PixelCanvas.TextWidth(name) + 6;
                bool sel = i == hover;
                if (sel) canvas.Panel(x - w / 2, y - 5, w, 11); else canvas.Rect(x - w / 2, y - 5, w, 11, new Color32(20, 12, 8, 190));
                canvas.Text(x - w / 2 + 3, y - 2, name, b.Affordable(all[i]) ? (sel ? Amber : Text) : Red);
            }
            if (hover >= 0 && hover < all.Count)
            {
                var d = all[hover];
                string cost = "";
                if (d.kit != null) cost = "KIT X" + game.Inventory.GetItem(d.kit);
                else foreach (var (t, n) in d.cost) cost += b.Cost(n) + " " + ResourceInfo.Name(t) + " ";
                canvas.Text(cx - PixelCanvas.TextWidth(d.name) / 2, cy - 6, d.name, Amber);
                canvas.Text(cx - PixelCanvas.TextWidth(cost) / 2, cy + 3, cost, Dim);
            }
        }

        /// <summary>Hold-Tab action wheel: one slice per available action, key hint under each.</summary>
        void DrawActionRadial()
        {
            var acts = game.RadialActions;
            int n = acts.Count;
            int cx = canvas.w / 2, cy = canvas.h / 2;
            int r = Mathf.Min(canvas.h / 2 - 20, Mathf.RoundToInt(canvas.h * 0.3f));
            canvas.Rect(0, 0, canvas.w, canvas.h, new Color32(10, 5, 3, 110));
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                int x = cx + Mathf.RoundToInt(Mathf.Sin(a) * r * 1.25f), y = cy - Mathf.RoundToInt(Mathf.Cos(a) * r);
                string name = acts[i].label;
                if (name.Length > 26) name = name.Substring(0, 26);
                string key = acts[i].key == UnityEngine.InputSystem.Key.None ? "" : KeyName(acts[i].key);
                int w = Mathf.Max(PixelCanvas.TextWidth(name), PixelCanvas.TextWidth(key)) + 8;
                bool sel = i == game.RadialHover;
                if (sel) canvas.Panel(x - w / 2, y - 7, w, key.Length > 0 ? 17 : 11); else canvas.Rect(x - w / 2, y - 7, w, key.Length > 0 ? 17 : 11, new Color32(20, 12, 8, 200));
                canvas.Text(x - PixelCanvas.TextWidth(name) / 2, y - 4, name, sel ? Amber : Text);
                if (key.Length > 0) canvas.Text(x - PixelCanvas.TextWidth(key) / 2, y + 3, key, Dim);
            }
            // pointer from the centre towards the hovered slice
            canvas.Rect(cx - 1, cy - 1, 3, 3, Amber);
            if (game.RadialHover >= 0)
            {
                float a = game.RadialHover * Mathf.PI * 2f / n;
                for (int s = 4; s < r / 2; s += 2) canvas.Rect(cx + Mathf.RoundToInt(Mathf.Sin(a) * s * 1.25f), cy - Mathf.RoundToInt(Mathf.Cos(a) * s), 1, 1, Amber);
            }
            string hint = "MOVE MOUSE TO PICK - RELEASE TAB OR CLICK";
            canvas.Text(cx - PixelCanvas.TextWidth(hint) / 2, cy - r - 20, hint, Dim);
        }

        static string KeyName(UnityEngine.InputSystem.Key k) =>
            k == UnityEngine.InputSystem.Key.Period ? "[.]" : k == UnityEngine.InputSystem.Key.Comma ? "[,]" : "[" + k.ToString().ToUpperInvariant().Replace("DIGIT", "") + "]";

        static string ViewName(ViewMode m) => m == ViewMode.Isometric ? "ISO" : m == ViewMode.TiltShift ? "TILT" : m == ViewMode.ThirdPerson ? "3RD" : "1ST";

        void DrawVehiclePanel(VehicleDriver car, int x, int y)
        {
            canvas.Panel(x, y - 14, 150, 46);
            float kmh = Mathf.Abs(car.SpeedKmh);
            canvas.Text(x + 4, y + 4, Mathf.RoundToInt(kmh).ToString("000"), Text, 3);
            canvas.Text(x + 42, y + 4, "KM/H", Dim);
            string gear = !car.Engine ? "-" : car.Reversing ? "R" : car.manual && car.Gear == 0 ? "N" : car.Gear.ToString();
            canvas.Frame(x + 42, y + 11, 11, 9, Dim);
            canvas.Text(x + 46, y + 13, gear, Amber);
            canvas.Text(x + 55, y + 13, car.manual ? "M" : "A", Dim);
            canvas.Text(x + 64, y + 4, "MUD", Dim);
            Bar(x + 64, y + 11, 36, car.Mud, new Color32(120, 72, 40, 255));
            canvas.Text(x + 64, y + 15, "SLIP", Dim);
            Bar(x + 64, y + 22, 36, car.WheelSlip, Amber);
            // drivetrain state
            canvas.Text(x + 4, y - 10, car.FourWheelDrive ? "4WD" : "2WD", car.FourWheelDrive ? Green : Dim);
            if (car.hasDiffLock) canvas.Text(x + 22, y - 10, "LOCK", car.diffLocked ? Amber : Dim);
            // fluids
            if (car.TryGetComponent<VehicleSystems>(out var sys))
            {
                int fx = x + 104;
                Gauge(fx, y - 10, sys.FuelKind == ResourceType.Diesel ? "D" : "F", sys.FuelFraction, ResourceInfo.Color(sys.FuelKind));
                Gauge(fx, y - 2, "O", sys.oilInFuel ? 1f : sys.OilFraction, ResourceInfo.Color(ResourceType.Oil));
                Gauge(fx, y + 6, "T", Mathf.InverseLerp(25f, 130f, sys.Temperature), sys.Temperature > 110f ? Red : Amber);
                Gauge(fx, y + 14, "C", sys.usesCoolant ? sys.CoolantFraction : 1f, ResourceInfo.Color(ResourceType.Coolant));
                var fault = sys.FaultText();
                if (fault != null && (Time.unscaledTime * 2f) % 2f > 0.6f)
                {
                    canvas.Panel(x, y - 27, PixelCanvas.TextWidth(fault) + 8, 11);
                    canvas.Text(x + 4, y - 24, fault, Red);
                }
            }
            // analogue speedometer and rev counter beside the digital readout
            int gx = x + 152;
            canvas.Panel(gx, y - 14, 96, 46);
            AnalogGauge(gx + 24, y + 5, 18, kmh, 180f, 999f, 20f, 60f, "KM/H");
            float maxK = car.Engine ? Mathf.Ceil(car.Engine.maxRpm / 1000f) : 7f;
            AnalogGauge(gx + 72, y + 5, 18, car.Engine ? car.Rpm / 1000f : 0f, maxK, car.Engine ? car.Engine.maxRpm * 0.88f / 1000f : 99f, 1f, maxK > 5f ? 2f : 1f, "RPM X1000");
            // rpm segments
            float rpm = car.Engine ? car.Rpm / car.Engine.maxRpm : 0f;
            for (int i = 0; i < 26; i++)
            {
                float f = i / 26f;
                var c = f < rpm ? (f > 0.85f ? Red : f > 0.65f ? Amber : Green) : Empty;
                canvas.Rect(x + 4 + i * 2, y + 25, 1, 3, c);
            }
        }

        readonly Dictionary<int, string> numbers = new Dictionary<int, string>();
        string Num(int v) { if (!numbers.TryGetValue(v, out var s)) numbers[v] = s = v.ToString(); return s; }

        /// <summary>Round pixel gauge: dark bezel, 270° scale (minor ticks, major ticks with numbers every labelStep),
        /// red zone from redFrom (value units), needle with shadow, title under the dial.</summary>
        void AnalogGauge(int cx, int cy, int r, float value, float max, float redFrom, float majorStep, float labelStep, string title)
        {
            var face = new Color32(14, 9, 6, 255); var rim = new Color32(90, 62, 40, 255);
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int d2 = dx * dx + dy * dy;
                if (d2 > r * r) continue;
                canvas.Set(cx + dx, cy + dy, d2 > (r - 1) * (r - 1) ? rim : face);
            }
            int minorCount = Mathf.RoundToInt(max / majorStep) * 4;
            for (int k = 0; k <= minorCount; k++)
            {
                float v = k * majorStep / 4f, f = v / max;
                float a = (225f - f * 270f) * Mathf.Deg2Rad;
                bool major = k % 4 == 0;
                var col = v >= redFrom ? Red : major ? Text : Dim;
                for (int s2 = 0; s2 < (major ? 3 : 1); s2++) canvas.Set(cx + Mathf.RoundToInt(Mathf.Cos(a) * (r - 2 - s2)), cy - Mathf.RoundToInt(Mathf.Sin(a) * (r - 2 - s2)), col);
            }
            for (float v = 0f; v <= max + 0.01f; v += labelStep)
            {
                float f = v / max, a = (225f - f * 270f) * Mathf.Deg2Rad;
                string lbl = Num(Mathf.RoundToInt(v));
                int lx = cx + Mathf.RoundToInt(Mathf.Cos(a) * (r - 8)), ly = cy - Mathf.RoundToInt(Mathf.Sin(a) * (r - 8));
                canvas.Text(lx - PixelCanvas.TextWidth(lbl) / 2 + 1, ly - 2, lbl, v >= redFrom ? Red : Dim, 1, false);
            }
            float t = Mathf.Clamp01(value / max);
            float na = (225f - t * 270f) * Mathf.Deg2Rad;
            int tx = cx + Mathf.RoundToInt(Mathf.Cos(na) * (r - 3)), ty = cy - Mathf.RoundToInt(Mathf.Sin(na) * (r - 3));
            canvas.Line(cx + 1, cy + 1, tx + 1, ty + 1, new Color32(0, 0, 0, 255));
            canvas.Line(cx, cy, tx, ty, value >= redFrom ? Red : Amber);
            canvas.Rect(cx - 1, cy - 1, 3, 3, rim);
            canvas.Text(cx - PixelCanvas.TextWidth(title) / 2, cy + r + 2, title, Dim, 1, false);
        }

        void Gauge(int x, int y, string label, float v, Color32 c)
        {
            canvas.Text(x, y, label, Dim);
            Bar(x + 5, y + 2, 36, v, v < 0.15f && label != "T" ? Red : c);
        }

        void Bar(int x, int y, int w, float v, Color32 c)
        {
            canvas.Rect(x, y, w, 2, Empty);
            canvas.Rect(x, y, Mathf.RoundToInt(w * Mathf.Clamp01(v)), 2, c);
        }

        /// <summary>Top-down chassis outline with each socket lit green (mounted) or red (missing).</summary>
        void DrawPartsMap(VehicleDriver car, int x, int y)
        {
            canvas.Panel(x, y, 38, 64);
            canvas.Text(x + 3, y + 3, "PARTS", Dim);
            var dmg = car.GetComponent<VehicleDamage>();
            if (dmg) Bar(x + 3, y + 10, 32, dmg.FrameDamage, DamageColor(dmg.FrameDamage));
            int cx = x + 19, cy = y + 36;
            canvas.Frame(cx - 9, cy - 22, 19, 44, new Color32(110, 70, 45, 255));
            var sockets = car.GetComponent<VehicleChassis>().Sockets;
            float extent = 0.5f;
            foreach (var s in sockets) extent = Mathf.Max(extent, Mathf.Abs(s.transform.localPosition.z), Mathf.Abs(s.transform.localPosition.x) * 2f);
            float unit = extent / 20f;
            foreach (var s in sockets)
            {
                var lp = s.transform.localPosition;
                int px = cx + Mathf.RoundToInt(lp.x / unit), py = cy - Mathf.RoundToInt(lp.z / unit);
                var col = s.Current ? DamageColor(s.Current.damage) : (s.accepts == PartCategory.Wheel || s.accepts == PartCategory.Engine ? Red : Empty);
                int sz = s.accepts == PartCategory.Wheel ? 3 : 2;
                canvas.Rect(px - sz / 2, py - sz / 2, sz, sz, col);
            }
        }

        static Color32 DamageColor(float d) => d < 0.5f ? Color32.Lerp(Green, Amber, d * 2f) : Color32.Lerp(Amber, Red, (d - 0.5f) * 2f);

        void DrawMinimap(int x, int y, int size)
        {
            canvas.Panel(x - 2, y - 2, size + 4, size + 4);
            var focus = game.Current ? game.Current.transform : game.Player.transform;
            var center = ToMap(focus.position);
            float scale = minimapMetresPerPixel / (2f * mapHalf / mapSize);
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                int mx = Mathf.FloorToInt(center.x + (i - size / 2) * scale);
                int my = Mathf.FloorToInt(center.y - (j - size / 2) * scale);
                var c = (mx < 0 || my < 0 || mx >= mapSize || my >= mapSize) ? new Color32(30, 18, 12, 255) : map[my * mapSize + mx];
                canvas.Set(x + i, y + j, c);
            }
            void Dot(Transform t, Color32 c)
            {
                var m = ToMap(t.position);
                int px = x + size / 2 + Mathf.RoundToInt((m.x - center.x) / scale), py = y + size / 2 - Mathf.RoundToInt((m.y - center.y) / scale);
                if (px > x && py > y && px < x + size - 1 && py < y + size - 1) canvas.Rect(px - 1, py - 1, 2, 2, c);
            }
            foreach (var w in game.Wrecks) if (w) Dot(w.transform, new Color32(150, 140, 130, 255));
            foreach (var t in game.Trailers) if (t) Dot(t.transform, new Color32(110, 160, 220, 255));
            foreach (var fv in game.Fleet) if (fv && fv != game.Current) Dot(fv.transform, Amber);
            var f = focus.forward;
            int ox = x + size / 2, oy = y + size / 2;
            canvas.Line(ox, oy, ox + Mathf.RoundToInt(f.x * 4), oy - Mathf.RoundToInt(f.z * 4), Text);
            canvas.Rect(ox - 1, oy - 1, 3, 3, Text);
            canvas.Text(x + size / 2 - 1, y + 1, "N", Text);
        }

        void DrawWeather(int x, int y)
        {
            string sky = Weather.Snowing ? "SNOW" : Weather.Raining ? "RAIN" : Weather.Snow > 0.2f ? "SNOWY" : "DRY";
            canvas.Text(x, y, sky, Weather.Raining ? Amber : Dim);
            string t = Mathf.RoundToInt(Weather.Temperature) + "C";
            canvas.Text(x + 64 - PixelCanvas.TextWidth(t), y, t, Weather.Temperature < 0f ? new Color32(150, 190, 255, 255) : Dim);
            Bar(x, y + 8, 64, Weather.Wetness, new Color32(110, 120, 140, 255));
            if (Weather.Snow > 0.01f) Bar(x, y + 11, 64, Weather.Snow, new Color32(230, 230, 240, 255));
            if (Weather.Ice > 0.3f) canvas.Text(x, y + 15, "ICE", new Color32(150, 190, 255, 255));
        }

        void DrawBuildPanel()
        {
            var b = game.Build;
            var all = b.Pieces;
            int w = 130, h = all.Count * 7 + 18, x = 6, y = 42;
            canvas.Panel(x, y, w, h);
            canvas.Text(x + 4, y + 3, "BUILD: " + b.Category.ToString().ToUpperInvariant() + "  < , . >", Amber);
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                int ly = y + 11 + i * 7;
                bool sel = i == b.Selected;
                if (sel) canvas.Rect(x + 2, ly - 1, w - 4, 7, new Color32(90, 50, 25, 200));
                var col = b.Affordable(d) ? (sel ? Text : Dim) : Red;
                canvas.Text(x + 4, ly, ((i + 1) % 10) + " " + d.name, col);
                int cx = x + 76;
                if (d.kit != null) canvas.Text(cx, ly, "KIT " + game.Inventory.GetItem(d.kit), col, 1, false);
                else if (d.link != MadMax.Building.UtilityKind.None) canvas.Text(cx, ly, "1 " + ResourceInfo.Name(d.cost[0].type) + "/5M", col, 1, false);
                else foreach (var (t, n) in d.cost)
                {
                    canvas.Rect(cx, ly + 1, 3, 3, ResourceInfo.Color(t));
                    cx += 4 + canvas.Text(cx + 4, ly, n.ToString(), col, 1, false) + 2;
                }
            }
            canvas.Text(x + 4, y + h - 7, b.Status ?? "", b.Valid ? Green : Amber);
        }

        /// <summary>Station and title for a few seconds after tuning or when the item on air changes.</summary>
        void DrawRadio(VehicleDriver car)
        {
            MadMax.Audio.RadioReceiver rx = null;
            if (car) car.TryGetComponent(out rx);
            else if (game.Focused is MadMax.Building.RadioSet rs) rx = rs.GetComponent<MadMax.Audio.RadioReceiver>();
            if (!rx || !rx.on || Time.unscaledTime - rx.ChangedAt > 6f) return;
            string line = rx.StationLabel() + (string.IsNullOrEmpty(rx.NowPlaying) ? "" : "  -  " + rx.NowPlaying);
            int w = PixelCanvas.TextWidth(line) + 10, x = (canvas.w - w) / 2, y = 4;
            canvas.Panel(x, y, w, 12);
            canvas.Text(x + 5, y + 4, line, Amber);
        }

        void DrawHelp()
        {
            string[] lines =
            {
                "WASD DRIVE/WALK  SPACE HANDBRAKE/JUMP  SHIFT RUN  LMB USE TOOL  1-8 HOTBAR  I INVENTORY  P SKILLS  B BUILD (HOLD: RADIAL)",
                "F ENTER/EXIT  E USE/OPEN/CRAFT  T SECOND ACTION (LOCK, REFUEL, WASH, FILL)  Q DROP  J HITCH  G SERVICE  K SIPHON  TAB FLEET",
                "BUILD: , . CATEGORY  1-0 PIECE  Y ROTATE  X DISMANTLE  CABLE/PIPE: CLICK TWO PIECES",
                "DRIVING: X 4WD  L DIFF LOCK  E/Q SHIFT (MANUAL)  N LIGHTS  T RECOVER  MACHINES: 1 2 3   V VIEW  ESC MENU  H HELP",
                "RADIO: M ON/OFF  , . TUNE  [ ] VOLUME  (RADIO SET: E POWER  T TUNE)   HOLD TAB: ACTION WHEEL"
            };
            int w = 0; foreach (var l in lines) w = Mathf.Max(w, PixelCanvas.TextWidth(l));
            int x = (canvas.w - w) / 2 - 4, y = 26;
            canvas.Panel(x, y, w + 8, lines.Length * 7 + 5);
            for (int i = 0; i < lines.Length; i++) canvas.Text(x + 4, y + 4 + i * 7, lines[i], Text);
        }
    }
}
