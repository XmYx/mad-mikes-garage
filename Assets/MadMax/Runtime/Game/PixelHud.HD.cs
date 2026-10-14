using System.Collections.Generic;
using MadMax.Items;
using MadMax.Rendering;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using G = MadMax.Rendering.HudGlyphs.Glyph;

namespace MadMax.Game
{
    /// <summary>HD HUD (setting HD HUD, the default): the same layout as the pixel HUD in its logical pixels (HUD SIZE
    /// still scales it), drawn crisp at the screen's resolution — rounded vector panels with soft shadows and edges
    /// (<see cref="HudVector"/>, two <see cref="HudVectorGraphic"/> layers around the pixel image), vector glyphs, ring
    /// and bar gauges, the HD font at fractional sizes. Icons and the minimap stay in the pixel image at the canvas'
    /// detail. Also: the minimap is composed at most 15 times a second (a cached block otherwise).</summary>
    public partial class PixelHud
    {
        HudVectorGraphic hdBack, hdFront;
        RawImage hdViewImage;
        HudVehicleView hdView;
        bool hdViewShown; Rect hdViewRect;

        bool HD => canvas != null && canvas.vec != null;
        HudVector V => canvas.vec;

        static readonly Color32 Track = new Color32(6, 8, 8, 190), Lamp0 = new Color32(120, 120, 110, 60), FaceDark = new Color32(10, 12, 12, 225);

        /// <summary>Layers: [vector back] [pixel image] [damage view] [vector front] [HD text].</summary>
        void InitHD()
        {
            hdBack = NewVectorLayer("HDVectorBack", false);
            hdBack.transform.SetSiblingIndex(0);
            var vi = new GameObject("HDVehicleView", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RawImage>();
            vi.transform.SetParent(overlay.transform, false);
            vi.raycastTarget = false; vi.enabled = false;
            hdViewImage = vi;
            hdFront = NewVectorLayer("HDVectorFront", true);
            if (hdText) hdText.transform.SetAsLastSibling();
            hdView = new HudVehicleView();
        }

        HudVectorGraphic NewVectorLayer(string name, bool front)
        {
            var g = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<HudVectorGraphic>();
            g.transform.SetParent(overlay.transform, false);
            g.raycastTarget = false; g.frontLayer = front; g.enabled = false;
            var rt = g.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return g;
        }

        /// <summary>Each frame after the canvas is sized: vector shapes on with HD HUD.</summary>
        int hdBackHash, hdFrontHash;

        void PrepareHD(bool on)
        {
            hdBackHash = hdFrontHash = 0;
            if (on && canvas.vec == null) canvas.vec = new HudVector();
            else if (!on) canvas.vec = null;
            if (hdBack) { hdBack.source = canvas; hdBack.enabled = on; }
            if (hdFront) { hdFront.source = canvas; hdFront.enabled = on; }
            hdViewShown = false;
        }

        /// <summary>Before the menus draw (in pixels, over the HUD): the vector HUD goes beneath them.</summary>
        void SinkHD() { if (canvas.vec != null) canvas.vec.Sink(); }

        void FlushHD()
        {
            if (canvas.vec != null)
            {                                                                                   // rebuild a layer's mesh only when its shapes changed
                int hb = canvas.vec.Hash(canvas.vec.back), hf = canvas.vec.Hash(canvas.vec.front);
                if (hdBack && hdBack.enabled && hb != hdBackHash) { hdBack.SetVerticesDirty(); hdBackHash = hb; }
                if (hdFront && hdFront.enabled && hf != hdFrontHash) { hdFront.SetVerticesDirty(); hdFrontHash = hf; }
            }
            if (hdView != null && hdView.Target && Time.unscaledTime - hdViewLastShown > 3f) hdView.Tick(null, 0, 0);   // let go after a while unseen
            if (!hdViewImage) return;
            bool show = hdViewShown && hdView != null && hdView.Ready && !(game.Menus && game.Menus.IsOpen) && !(canvas.vec != null && canvas.vec.holeCovered);   // a later panel over it (loot, help) hides it
            hdViewImage.enabled = show;
            if (!show) return;
            if (hdViewImage.texture != hdView.Texture) hdViewImage.texture = hdView.Texture;
            var rt = hdViewImage.rectTransform;
            rt.anchorMin = new Vector2(hdViewRect.xMin / canvas.w, 1f - hdViewRect.yMax / canvas.h);
            rt.anchorMax = new Vector2(hdViewRect.xMax / canvas.w, 1f - hdViewRect.yMin / canvas.h);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void ReleaseHD() { if (hdView != null) hdView.Release(); }

        // ------------------------------------------------------------------ kit
        void HBar(float x, float y, float w, float h, float t, Color32 col, Color32? track = null)
        {
            V.Fill(x, y, w, h, h * 0.5f, track ?? Track);
            float fw = w * Mathf.Clamp01(t);
            if (fw > 0.02f) V.Fill(x, y, Mathf.Max(fw, Mathf.Min(h, w)), h, h * 0.5f, HudVector.Lighten(col, 34), col);
        }

        void HRing(float cx, float cy, float r, float t, float frac, Color32 col)
        {
            V.Ring(cx, cy, r, t, Track);
            V.Arc(cx, cy, r, t * 0.78f, 0f, 360f * Mathf.Clamp01(frac), col);
        }

        /// <summary>A pill with text; returns its width.</summary>
        float HChip(float x, float y, string s, Color32 fg, Color32 bg, float size = 0.72f)
        {
            float w = PixelCanvas.TextWidthF(s, size) + 4.4f, h = 5f * size + 2.8f;
            V.Fill(x, y, w, h, h * 0.5f, bg);
            canvas.TextF(x + 2.2f, y + 1.4f, s, fg, size, false);
            return w;
        }

        void HGlyph(G g, float cx, float cy, float size, Color32 c) => HudGlyphs.Draw(V, g, cx, cy, size, c);

        static bool Blink(float rate = 2.5f, float duty = 0.5f) => (Time.unscaledTime * rate) % 1f < duty;

        // small string caches (no garbage for numbers drawn every frame)
        readonly Dictionary<int, string> tempTexts = new Dictionary<int, string>(), pctTexts = new Dictionary<int, string>();
        string TempText(float c)
        {
            bool m = GameSettings.Current.metric;
            int v = Mathf.RoundToInt(m ? c : c * 1.8f + 32f), key = v * 2 + (m ? 0 : 1);
            if (!tempTexts.TryGetValue(key, out var s)) tempTexts[key] = s = v + (m ? "C" : "F");
            return s;
        }
        string Pct(float f)
        {
            int v = Mathf.RoundToInt(Mathf.Clamp01(f) * 100f);
            if (!pctTexts.TryGetValue(v, out var s)) pctTexts[v] = s = v + "%";
            return s;
        }

        // ------------------------------------------------------------------ header + resources
        int headerKey = int.MinValue; string headerWhere, headerLine;

        void DrawHeaderHD(VehicleDriver car)
        {
            var interior = game.Player ? game.Player.Interior : null;
            int minute = Mathf.FloorToInt(DayNight.Hours * 60f);
            int key = (car ? car.GetHashCode() : interior ? interior.GetHashCode() : 1) * 31 + minute * 7 + (int)rig.mode * 3 + (int)game.CurrentBiome * 101;
            if (key != headerKey)
            {
                headerKey = key;
                headerWhere = car ? WastelandGame.Name(car) : interior ? "IN " + WastelandGame.Name(interior) : "ON FOOT";
                headerLine = ViewName(rig.mode) + "  " + game.CurrentBiome.ToString().ToUpperInvariant() + "  " + (minute / 60 % 24).ToString("00") + ":" + (minute % 60).ToString("00");
            }
            canvas.TextF(7f, 5f, headerWhere, Text, 1.1f);
            canvas.TextF(7f, 12.2f, headerLine, Dim, 0.78f);
            if (!Atmosphere.SkyVisible) SkyArcHD(7f + PixelCanvas.TextWidthF(headerLine, 0.78f) + 5f, 11f);
        }

        void SkyArcHD(float x, float y)
        {
            const float w = 20f, h = 6f;
            V.Line(x, y + h, x + w, y + h, 0.35f, new Color32(140, 110, 85, 200));
            var sun = DayNight.SunDirection;
            bool day = sun.y > -0.05f;
            var dir = day ? sun : DayNight.MoonDirection;
            if (dir.y < -0.05f) return;
            float hours = DayNight.Hours;
            float along = day ? Mathf.Clamp01((hours - 6f) / 12f) : Mathf.Repeat(hours - 18f, 24f) / 12f;
            float px = x + along * w, py = y + h - 0.8f - Mathf.Clamp01(dir.y) * (h - 1.5f);
            if (day) HGlyph(G.Sun, px, py, 4.2f, MadMax.Voxel.Pal.LightY);
            else HGlyph(G.Moon, px, py, 3.8f, MadMax.Voxel.Pal.Cream[4]);
        }

        readonly string[] resText = new string[256]; readonly int[] resVal = new int[256];

        void DrawResourcesHD(int x, int y)
        {
            var inv = game.Inventory;
            float w = 4f;
            foreach (var type in ResourceInfo.HudOrder) w += 5f + PixelCanvas.TextWidthF(ResText(type, inv.Get(type)), 0.78f) + 4.5f + (type == ResourceType.Fuel ? 3f : 0f);
            V.Fill(x - 1f, y - 1.6f, w, 7.6f, 3.8f, new Color32(8, 10, 10, 140), true);
            float cx = x + 2f;
            foreach (var type in ResourceInfo.HudOrder)
            {
                if (type == ResourceType.Fuel) cx += 3f;
                int n = inv.Get(type);
                V.Circle(cx + 1.4f, y + 2.2f, 1.6f, ResourceInfo.Color(type));
                cx += 4.5f;
                cx += canvas.TextF(cx, y + 0.3f, ResText(type, n), n > 0 ? Text : Dim, 0.78f, false) + 4.5f;
            }
        }

        string ResText(ResourceType t, int n)
        {
            int i = (int)t & 255;
            if (resText[i] == null || resVal[i] != n) { resVal[i] = n; resText[i] = ResourceInfo.IsFluid(t) ? n + "L" : n.ToString(); }
            return resText[i];
        }

        // ------------------------------------------------------------------ vitals
        static readonly Color32 HealthCol = new Color32(222, 72, 64, 255), StaminaCol = new Color32(126, 206, 104, 255), FoodCol = new Color32(226, 156, 72, 255),
            WaterCol = new Color32(92, 166, 236, 255), SoapCol = new Color32(236, 176, 196, 255), ColdCol = new Color32(130, 180, 255, 255);

        void DrawVitalsHD(int x, int y)
        {
            Profiler.BeginSample("MadMax.Hud.Vitals");
            var st = game.Stats;
            bool needs = game.Rules.survival;
            int tagN = Tags(st, needs);
            float w = 116f, h = 6f + 18f + 17f + (tagN > 0 ? 9f : 0f);
            float px = x, py = canvas.h - 7f - h;
            canvas.VPanel(px, py, w, h, 3.2f, 200);
            float hp = st.health / Mathf.Max(1f, st.MaxHealth), sp = st.stamina / Mathf.Max(1f, st.MaxStamina);
            bool hpLow = st.health < st.MaxHealth * 0.25f, spLow = game.Vitals && game.Vitals.Exhausted;
            VitalRow(px, py + 4f, w, G.Heart, hp, hpLow ? Red : HealthCol, hpLow);
            VitalRow(px, py + 13f, w, G.Bolt, sp, spLow ? Red : StaminaCol, spLow);
            float ry = py + 31.5f, rx = px + 10f;
            if (needs)
            {
                VitalRing(rx, ry, G.Food, st.hunger / 100f, FoodCol, st.hunger < 20f); rx += 17f;
                VitalRing(rx, ry, G.Drop, st.thirst / 100f, WaterCol, st.thirst < 20f); rx += 17f;
                VitalRing(rx, ry, G.Soap, st.hygiene / 100f, SoapCol, st.hygiene < 25f); rx += 13f;
            }
            else rx -= 6f;
            // core temperature
            var tc = st.bodyTemp < 35.5f ? ColdCol : st.bodyTemp > 38.5f ? Red : Text;
            HGlyph(G.Thermo, rx + 3f, ry, 7f, tc);
            canvas.TextF(rx + 7.5f, ry - 2.4f, BodyTemp(st.bodyTemp), tc, 0.95f);
            if (tagN > 0) DrawTags(px + 4f, py + h - 8.5f, st, needs);
            var horse = MadMax.Animals.Animal.Mounted;
            if (horse)
            {
                canvas.VPanel(px, py - 11f, 70f, 9f, 3f, 200);
                canvas.TextF(px + 3f, py - 9f, "HORSE", Dim, 0.7f, false);
                HBar(px + 22f, py - 8.2f, 44f, 3f, horse.stamina / 100f, horse.Winded ? Red : new Color32(200, 160, 90, 255));
            }
            Profiler.EndSample();
        }

        void VitalRow(float px, float y, float w, G g, float t, Color32 col, bool low)
        {
            HGlyph(g, px + 7f, y + 3f, 6.5f, low && Blink() ? Text : col);
            HBar(px + 13f, y + 1.4f, w - 36f, 3.4f, t, col);
            canvas.TextF(px + w - 20f, y + 0.6f, Pct(t), low ? Red : Dim, 0.82f, false);
        }

        void VitalRing(float cx, float cy, G g, float t, Color32 col, bool low)
        {
            var c = low ? Red : col;
            V.Circle(cx, cy, 6.6f, new Color32(10, 12, 12, 200));
            HRing(cx, cy, 6f, 1.5f, t, c);
            HGlyph(g, cx, cy, 6f, low && Blink() ? Text : c);
        }

        string BodyTemp(float t)
        {
            int k = Mathf.RoundToInt(t * 10f) * 2 + (GameSettings.Current.metric ? 0 : 1);
            if (!tempTexts.TryGetValue(100000 + k, out var s))
                tempTexts[100000 + k] = s = GameSettings.Current.metric ? t.ToString("0.0") + "C" : (t * 1.8f + 32f).ToString("0.0") + "F";
            return s;
        }

        int Tags(CharacterStats st, bool needs)
        {
            int n = 0;
            if (game.Player.Encumbered || game.BackTag != null) n++;
            if (st.sick > 0f) n++;
            foreach (var inj in st.injuries) if (inj.Bleeding) { n++; break; }
            if (st.rested) n++;
            if (st.fed) n++;
            if (st.wetness > 0.3f) n++;
            if (game.Coughing) n++;
            if (needs && st.waste >= 100f) n++;
            return n;
        }

        void DrawTags(float x, float y, CharacterStats st, bool needs)
        {
            var bg = new Color32(10, 12, 12, 200);
            if (game.Player.Encumbered) x += HChip(x, y, "OVERLOADED", Red, bg) + 1.5f;
            else if (game.BackTag != null) x += HChip(x, y, game.BackTag, game.BackStrain > 0.75f ? Red : Amber, bg) + 1.5f;
            foreach (var inj in st.injuries) if (inj.Bleeding) { x += HChip(x, y, "BLEEDING", Blink(2f, 0.6f) ? Text : Red, Blink(2f, 0.6f) ? Red : bg) + 1.5f; break; }
            if (st.sick > 0f) x += HChip(x, y, "SICK", new Color32(160, 220, 80, 255), bg) + 1.5f;
            var good = new Color32(150, 210, 150, 255);
            if (st.rested) x += HChip(x, y, "RESTED", good, bg) + 1.5f;
            if (st.fed) x += HChip(x, y, "FED", good, bg) + 1.5f;
            if (st.wetness > 0.3f) x += HChip(x, y, "WET", WaterCol, bg) + 1.5f;
            if (game.Coughing) x += HChip(x, y, "COUGH", Amber, bg) + 1.5f;
            if (needs && st.waste >= 100f) HChip(x, y, "LATRINE", new Color32(210, 170, 90, 255), bg);
        }

        /// <summary>Vitals in a vehicle: a row of small ring gauges under the resources.</summary>
        void DrawVitalsCompactHD(int x, int y)
        {
            var st = game.Stats;
            bool needs = game.Rules.survival;
            int n = needs ? 5 : 2;
            bool hot = st.bodyTemp < 35.5f || st.bodyTemp > 38.5f;
            float w = n * 11.5f + 3f + (hot ? 26f : 0f);
            V.Fill(x - 1f, y - 1f, w, 11f, 5.5f, new Color32(8, 10, 10, 150), true);
            float cx = x + 5f, cy = y + 4.5f;
            void Cell(G g, float t, Color32 col, bool low)
            {
                var c = low ? Red : col;
                HRing(cx, cy, 4.2f, 1.1f, t, c);
                HGlyph(g, cx, cy, 4.4f, low && Blink() ? Text : c);
                cx += 11.5f;
            }
            Cell(G.Heart, st.health / Mathf.Max(1f, st.MaxHealth), HealthCol, st.health < st.MaxHealth * 0.25f);
            Cell(G.Bolt, st.stamina / Mathf.Max(1f, st.MaxStamina), StaminaCol, game.Vitals && game.Vitals.Exhausted);
            if (needs)
            {
                Cell(G.Food, st.hunger / 100f, FoodCol, st.hunger < 20f);
                Cell(G.Drop, st.thirst / 100f, WaterCol, st.thirst < 20f);
                Cell(G.Soap, st.hygiene / 100f, SoapCol, st.hygiene < 25f);
            }
            if (hot)
            {
                var tc = st.bodyTemp < 35.5f ? ColdCol : Red;
                HGlyph(G.Thermo, cx - 2f, cy, 6f, tc);
                canvas.TextF(cx + 1.5f, cy - 2f, BodyTemp(st.bodyTemp), tc, 0.8f);
                cx += 26f;
            }
            foreach (var inj in st.injuries) if (inj.Bleeding) { if (Blink(2f, 0.6f)) HChip(x + w + 2f, y + 0.5f, "BLEEDING", Text, Red); break; }
        }

        // ------------------------------------------------------------------ hotbar
        readonly Dictionary<string, string> slotNames = new Dictionary<string, string>();
        readonly string[] countTexts = new string[64];

        void ToolbarSlotsHD(int x0, int y, int slot, int n)
        {
            Profiler.BeginSample("MadMax.Hud.Hotbar");
            for (int i = 0; i < n; i++)
            {
                float x = x0 + i * (slot + 2);
                var id = game.Hotbar[i];
                bool sel = id != null && game.Player.Tool && game.Player.Tool.id == id;
                var sh = HudVector.ShadowCol; sh.a = 90;
                V.Shadow(x + 0.3f, y + 0.9f, slot, slot, 4f, 2.6f, sh);
                V.Fill(x, y, slot, slot, 3.2f, sel ? new Color32(92, 64, 34, 225) : new Color32(30, 36, 35, 190), sel ? new Color32(58, 38, 20, 225) : new Color32(14, 18, 18, 190), true);
                V.Stroke(x, y, slot, slot, 3.2f, sel ? 0.7f : 0.35f, sel ? Amber : HudVector.Alpha(MadMax.Voxel.Pal.PanelEdge, 140));
                canvas.TextF(x + 1.8f, y + 1.6f, NumText(i + 1), sel ? Amber : Dim, 0.66f, false);
                if (id == null) continue;
                canvas.Blit((int)x + 4, y + 1, 14, Icon(id, canvas.res));
                if (id == game.LearningId) HBar(x + 2.5f, y + 13.6f, slot - 5f, 1.2f, game.LearningProgress, Green);
                if (!slotNames.TryGetValue(id, out var name))
                {
                    name = ItemCatalog.Name(id).Replace("VHS: ", "");
                    if (name.Length > 6) name = name.Substring(0, 6);
                    slotNames[id] = name = name.ToUpperInvariant();
                }
                float nw = PixelCanvas.TextWidthF(name, 0.6f);
                canvas.TextF(x + (slot - nw) * 0.5f, y + 15.6f, name, sel ? Amber : Text, 0.6f, false);
                int count = game.Inventory.GetItem(id);
                if (count > 1)
                {
                    string cs = NumText(count);
                    HChip(x + slot - PixelCanvas.TextWidthF(cs, 0.6f) - 5f, y + 1f, cs, Text, new Color32(10, 12, 12, 210), 0.6f);
                }
                if (ToolLibrary.Has(id))
                {
                    float cond = game.Condition(id);
                    if (cond < 0.999f) HBar(x + 3f, y + slot - 2.4f, slot - 6f, 1.3f, cond, cond > 0.5f ? Green : cond > 0.2f ? Amber : Red);
                }
            }
            Profiler.EndSample();
        }

        string NumText(int v) => v >= 0 && v < countTexts.Length ? (countTexts[v] ??= v.ToString()) : Num(v);

        /// <summary>A gap crosshair with a dark under-stroke.</summary>
        void CrossHD(float cx, float cy, Color32 col, float r = 3.6f, float gap = 1.6f)
        {
            var ink = new Color32(8, 6, 4, 170);
            for (int pass = 0; pass < 2; pass++)
            {
                float t = pass == 0 ? 1.1f : 0.5f; var c = pass == 0 ? ink : col;
                V.Line(cx - r - gap, cy, cx - gap, cy, t, c); V.Line(cx + gap, cy, cx + r + gap, cy, t, c);
                V.Line(cx, cy - r - gap, cx, cy - gap, t, c); V.Line(cx, cy + gap, cx, cy + r + gap, t, c);
            }
            V.Circle(cx, cy, 0.45f, col);
        }

        /// <summary>Frost / heat creeping in from the edges: gradient bands (back layer, under the HUD's pixels).</summary>
        void VignetteHD(Color32 col, float k)
        {
            float depth = canvas.h * 0.25f * k, a = 150f * k;
            var c0 = HudVector.Alpha(col, (byte)a); var c1 = HudVector.Alpha(col, 0);
            V.Fill(0f, 0f, canvas.w, depth, 0f, c0, c1, true);
            V.Fill(0f, canvas.h - depth, canvas.w, depth, 0f, c1, c0, true);
            V.FillH(0f, depth, depth, canvas.h - 2f * depth, 0f, c0, c1, true);
            V.FillH(canvas.w - depth, depth, depth, canvas.h - 2f * depth, 0f, c1, c0, true);
        }

        // ------------------------------------------------------------------ toasts, hints
        void DrawToastHD(string toast)
        {
            float tw = PixelCanvas.TextWidthF(toast, 1f), w = tw + 16f, x = (canvas.w - w) * 0.5f, y = 43f;
            canvas.VPanel(x, y, w, 12f, 6f, 238);
            V.Circle(x + 6f, y + 6f, 1.7f, Amber);
            canvas.TextF(x + 10f, y + 3.5f, toast, Text, 1f, false);
        }

        void DrawHintHD(string hint, int hy)
        {
            float tw = PixelCanvas.TextWidthF(hint, 0.9f), w = tw + 12f, x = (canvas.w - w) * 0.5f;
            V.Fill(x, hy - 0.5f, w, 10f, 5f, new Color32(8, 10, 10, 175), true);
            V.Stroke(x, hy - 0.5f, w, 10f, 5f, 0.3f, HudVector.Alpha(Amber, 120));
            canvas.TextF(x + 6f, hy + 2.3f, hint, Text, 0.9f, false);
        }

        // ------------------------------------------------------------------ compass, weather
        static readonly string[] CompassNames = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        void DrawCompassHD()
        {
            var cam = rig && rig.pixel ? rig.pixel.transform : null;
            if (!cam) return;
            float yaw = cam.eulerAngles.y;
            float w = 136f, x0 = canvas.w * 0.5f - w * 0.5f, y0 = 2f, half = w * 0.5f, span = 66f;
            V.Fill(x0, y0, w, 10f, 5f, new Color32(8, 10, 10, 150), true);
            for (int k = 0; k < 24; k++)
            {
                float a = Mathf.DeltaAngle(yaw, k * 15f);
                if (Mathf.Abs(a) > span) continue;
                float px = x0 + half + a / span * (half - 6f);
                float fade = 1f - Mathf.Abs(a) / span;
                if (k % 3 == 0)
                {
                    string nm = CompassNames[k / 3];
                    var c = k % 6 == 0 ? Text : Dim; c.a = (byte)(255 * Mathf.Clamp01(fade * 1.6f));
                    canvas.TextF(px - PixelCanvas.TextWidthF(nm, 0.8f) * 0.5f, y0 + 3f, nm, c, 0.8f, false);
                }
                else V.Line(px, y0 + 3.5f, px, y0 + 6.5f, 0.35f, HudVector.Alpha(Dim, (byte)(200 * fade)));
            }
            V.Tri(canvas.w * 0.5f - 1.6f, y0 + 10.8f, canvas.w * 0.5f + 1.6f, y0 + 10.8f, canvas.w * 0.5f, y0 + 8.6f, Amber);
            void Mark(Vector3 p, Color32 col)
            {
                var d = p - cam.position;
                float a = Mathf.Clamp(Mathf.DeltaAngle(yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg), -span, span);
                float px = x0 + half + a / span * (half - 6f);
                V.Tri(px - 1.8f, y0 + 10.4f, px + 1.8f, y0 + 10.4f, px, y0 + 13f, col);
            }
            game.JobPins(pins);
            foreach (var pin in pins) Mark(pin.pos, pin.color);
            if (game.HasWaypoint) Mark(game.Waypoint, RouteFill);
        }

        void DrawWeatherHD(int x, int y)
        {
            bool storm = Storms.Name != null, ice = Weather.Ice > 0.3f, snow = Weather.LocalSnow > 0.01f;
            float h = 15f + (storm || ice ? 8f : 0f);
            canvas.VPanel(x, y - 1f, 64f, h, 3f, 200);
            var g = Weather.Snowing ? G.Snow : Weather.Raining ? G.Rain : DayNight.SunDirection.y > -0.05f ? G.Sun : G.Moon;
            HGlyph(g, x + 5f, y + 3.6f, 6.4f, Weather.Raining || Weather.Snowing ? MadMax.Voxel.Pal.PaleBlue[3] : g == G.Sun ? MadMax.Voxel.Pal.LightY : MadMax.Voxel.Pal.Cream[3]);
            canvas.TextF(x + 10f, y + 1.6f, Weather.SeasonNames[Mathf.Clamp(Weather.Season, 0, 3)], Dim, 0.78f, false);
            string t = TempText(Weather.Temperature);
            canvas.TextF(x + 61f - PixelCanvas.TextWidthF(t, 0.9f), y + 1.3f, t, Weather.Temperature < 0f ? ColdCol : Text, 0.9f, false);
            HBar(x + 3f, y + 9f, snow ? 28f : 58f, 2f, Weather.Wetness, new Color32(120, 140, 170, 255));
            if (snow) HBar(x + 33f, y + 9f, 28f, 2f, Weather.LocalSnow, new Color32(230, 230, 240, 255));
            float ly = y + 13.5f, lx = x + 3f;
            var bg = new Color32(10, 12, 12, 210);
            if (storm) lx += HChip(lx, ly, Storms.Name, Blink(1f, 0.6f) ? Red : Amber, bg, 0.62f) + 1.5f;
            if (ice) HChip(lx, ly, "ICE", ColdCol, bg, 0.62f);
        }

        // ------------------------------------------------------------------ minimap: cached composition + vector rim
        Color32[] miniCache; RectInt miniCacheRect; int miniCacheKey; float miniNext;
        readonly List<PixelCanvas.TextRun> miniRuns = new List<PixelCanvas.TextRun>();
        /// <summary>Minimap compositions per second (the terrain, roads and markers); frames between reuse the block.</summary>
        public static float MinimapHz = 15f;

        void DrawMinimapCached(int x, int y, int size)
        {
            if (map == null || !game.Player) return;
            if (game.Menus && game.Menus.Current == MenuSystem.Page.Map) return;
            Profiler.BeginSample("MadMax.Hud.Minimap");
            int rs = canvas.res, m = 4;
            int fx0 = Mathf.Max(0, (x - m) * rs), fy0 = Mathf.Max(0, (y - m) * rs), fx1 = Mathf.Min(canvas.pw, (x + size + m) * rs), fy1 = Mathf.Min(canvas.ph, (y + size + m) * rs);
            int key = ((x * 31 + y) * 31 + size) * 31 + rs * 7 + canvas.pw * 13 + canvas.ph + (HD ? 1 : 0);
            float now = Time.unscaledTime;
            bool compose = miniCache == null || key != miniCacheKey || now >= miniNext;
            if (compose)
            {
                miniNext = now + 1f / Mathf.Max(1f, MinimapHz);
                int r0 = canvas.runs != null ? canvas.runs.Count : 0;
                DrawRoundMinimap(x, y, size);
                miniRuns.Clear();
                if (canvas.runs != null) for (int i = r0; i < canvas.runs.Count; i++) miniRuns.Add(canvas.runs[i]);
                int bw = fx1 - fx0, bh = fy1 - fy0;
                if (bw > 0 && bh > 0)
                {
                    if (miniCache == null || miniCache.Length != bw * bh) miniCache = new Color32[bw * bh];
                    for (int j = 0; j < bh; j++) System.Array.Copy(canvas.px, (canvas.ph - 1 - (fy0 + j)) * canvas.pw + fx0, miniCache, j * bw, bw);
                }
                miniCacheRect = new RectInt(fx0, fy0, bw, bh);
                miniCacheKey = key;
            }
            else
            {
                int bw = miniCacheRect.width, bh = miniCacheRect.height;
                for (int j = 0; j < bh; j++) System.Array.Copy(miniCache, j * bw, canvas.px, (canvas.ph - 1 - (miniCacheRect.y + j)) * canvas.pw + miniCacheRect.x, bw);
                if (canvas.runs != null) canvas.runs.AddRange(miniRuns);
                LastMinimapFrame = Time.frameCount;
            }
            if (HD) MinimapRimHD(x, y, size);
            Profiler.EndSample();
        }

        /// <summary>The HD minimap frame: rim, compass dots, health (left) and stamina (right) arcs, the N badge and
        /// the waypoint distance tab, every frame over the cached map.</summary>
        void MinimapRimHD(int x, int y, int size)
        {
            float R = size * 0.5f - 2f, cx = x + size * 0.5f, cy = y + size * 0.5f - 1f;
            V.Shadow(cx - R + 0.4f, cy - R + 1.2f, R * 2f, R * 2f, R, 3.5f, HudVector.ShadowCol);
            V.Ring(cx, cy, R + 0.15f, 1.25f, new Color32(18, 14, 11, 255));
            V.Ring(cx, cy, R - 1.1f, 0.3f, new Color32(236, 206, 156, 70));
            for (int k = 1; k < 4; k++)
            {
                var p = HudVector.Polar(cx, cy, R + 0.15f, LastNorthAngle + k * 90f);
                V.Circle(p.x, p.y, 0.75f, new Color32(210, 190, 160, 230));
            }
            var stats = game.Stats;
            float hp = stats != null ? Mathf.Clamp01(stats.health / Mathf.Max(1f, stats.MaxHealth)) : 1f;
            float sp = stats != null ? Mathf.Clamp01(stats.stamina / Mathf.Max(1f, stats.MaxStamina)) : 1f;
            float rr = R + 2.9f, rw = 1.3f;
            V.Arc(cx, cy, rr, rw + 0.6f, 182f, 176f, new Color32(10, 10, 8, 170));
            V.Arc(cx, cy, rr, rw + 0.6f, 178f, -176f, new Color32(10, 10, 8, 170));
            bool low = hp < 0.25f && Blink(2f, 0.6f);
            V.Arc(cx, cy, rr, rw, 182f, 176f * hp, low ? Red : Green);
            V.Arc(cx, cy, rr, rw, 178f, -176f * sp, Amber);
            var n = HudVector.Polar(cx, cy, R + 0.15f, LastNorthAngle);
            V.Circle(n.x, n.y, 3.6f, new Color32(20, 16, 12, 245));
            V.Ring(n.x, n.y, 3.6f, 0.5f, Amber);
            canvas.TextF(n.x - 1.6f * 0.95f + 0.1f, n.y - 2.4f, "N", Text, 0.95f, false);
            LastWaypointText = null;
            if (game.HasWaypoint)
            {
                string dist = FormatDistance(game.WaypointDistance);
                LastWaypointText = dist;
                float tw = PixelCanvas.TextWidthF(dist, 0.8f) + 8f, tx = cx - tw * 0.5f, ty = y + size - 6.5f;
                V.Fill(tx, ty, tw, 8.5f, 4.25f, new Color32(10, 14, 16, 240));
                V.Stroke(tx, ty, tw, 8.5f, 4.25f, 0.45f, RouteFill);
                canvas.TextF(tx + 4f, ty + 2.2f, dist, RouteFill, 0.8f, false);
            }
        }
    }
}
