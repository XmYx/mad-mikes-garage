using System;
using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MadMax.Game
{
    /// <summary>Pixel-art menus drawn into the HUD canvas: main menu, pause, settings, crafting.
    /// Keyboard (W/S, A/D, Enter, Esc), gamepad (d-pad, A, B) and mouse (hover, click, wheel) all work.</summary>
    public class MenuSystem : MonoBehaviour
    {
        public enum Page { None, Main, Pause, Settings, Crafting, Character, Join, NewGame, Creation, Inventory, Skills, Research, Container, Health, Talk, Trade, Repair, Salvage, Armour, Tuning, Board, Paint }

        public Page Current { get; private set; }
        public bool IsOpen => Current != Page.None;
        /// <summary>Menus that stop the world (time scale 0).</summary>
        public bool Pauses => !TitleSequence.Playing && (MadMax.Net.NetSession.Instance == null || !MadMax.Net.NetSession.Instance.Online) && (Current == Page.Main || Current == Page.Pause || Current == Page.Character || (Current == Page.Settings && settingsFrom != Page.None));

        class Item
        {
            public string label;
            public Func<string> value;
            public Action confirm;
            public Action<int> adjust;
            public Func<bool> enabled;
            public Recipe recipe;
            public System.Func<string> text;            // editable text field
            public System.Action<string> setText;
            public string hint;
            public string id;
            public RectInt rect;
        }

        WastelandGame game;
        readonly List<Item> items = new List<Item>();
        int cursor, category, scroll;
        Page settingsFrom;
        CraftingStation station;
        Vector2 lastMouse;

        static readonly Color32 Text = new Color32(255, 226, 170, 255), Dim = new Color32(150, 110, 75, 255), Amber = new Color32(255, 170, 50, 255),
            Red = new Color32(235, 60, 45, 255), Green = new Color32(130, 210, 90, 255), Hi = new Color32(110, 55, 25, 230);

        public void Init(WastelandGame g) { game = g; }

        public void Open(Page p)
        {
            if (p != Page.Character) mirror = false;
            if (p == Page.Settings) settingsFrom = Current;
            Current = p;
            cursor = 0; scroll = 0;
            Rebuild();
            while (cursor < items.Count - 1 && !Enabled(items[cursor])) cursor++;   // skip greyed-out entries
            Time.timeScale = Pauses ? 0f : 1f;
        }

        public void OpenCrafting(CraftingStation s) { station = s; category = 0; Open(Page.Crafting); }

        /// <summary>The character page at a mirror: hair and beard can be changed there (not from the pack).</summary>
        public void OpenMirror() { Open(Page.Character); mirror = true; Rebuild(); }
        bool mirror;

        // ---- conversations and trade with NPCs
        MadMax.Npc.Dialogue talk;
        MadMax.Npc.Npc talkNpc;
        /// <summary>The NPC in an open conversation or trade (they face the player).</summary>
        public MadMax.Npc.Npc TalkingTo => Current == Page.Talk || Current == Page.Trade ? talkNpc : null;

        public void OpenTalk(MadMax.Npc.Npc n, bool trade)
        {
            talkNpc = n;
            talk = new MadMax.Npc.Dialogue(game, n);
            Open(trade && n.Profile.Vendor ? Page.Trade : Page.Talk);
        }

        void Choose(MadMax.Npc.Dialogue.Choice c)
        {
            c.act?.Invoke();
            if (talk.WantsTrade) { talk.WantsTrade = false; Open(Page.Trade); return; }
            if (talk.Ended) { Close(); return; }
            Rebuild();
            cursor = 0;
        }

        string StationType => station ? station.type : "workbench";

        List<RecipeCategory> StationCategories()
        {
            var l = new List<RecipeCategory>();
            foreach (var r in RecipeLibrary.All) if (r.station == StationType && !l.Contains(r.category)) l.Add(r.category);
            l.Sort();
            return l;
        }

        readonly List<Container> pool = new List<Container>();
        int PoolRes(ResourceType t) { int n = game.Inventory.Get(t); if (station) { Container.Near(station.transform.position, 5f, pool); foreach (var c in pool) n += c.inventory.Get(t); } return n; }
        int PoolItem(string id) { int n = game.Inventory.GetItem(id); if (station) { Container.Near(station.transform.position, 5f, pool); foreach (var c in pool) n += c.inventory.GetItem(id); } return n; }

        // ---- container transfer
        Container container;
        bool containerSide;              // false = player's items, true = container's
        public void OpenContainer(Container c) { container = c; containerSide = false; Open(Page.Container); }

        MadMax.Building.BountyBoard boardTarget;
        /// <summary>A town's bounty board: jobs to take, to claim, crates to hand in.</summary>
        public void OpenBoard(MadMax.Building.BountyBoard b) { boardTarget = b; Open(Page.Board); }

        MadMax.Vehicles.VehicleTuning tuneTarget;
        /// <summary>Tuning page (tuning bench / garage) for a vehicle.</summary>
        public void OpenTuning(MadMax.Vehicles.VehicleTuning t) { tuneTarget = t; Open(Page.Tuning); }
        static readonly string[] BrakeNames = { "STOCK", "VENTED DISCS", "RACING" };

        MadMax.Vehicles.VehiclePaint paintTarget;
        int paintWasColour, paintWasDecal;
        /// <summary>Paint shop page for a vehicle (paint station): colour and decal preview live, paid on SPRAY.</summary>
        public void OpenPaint(MadMax.Vehicles.VehiclePaint p)
        {
            if (!p) return;
            paintTarget = p; paintWasColour = p.colour; paintWasDecal = p.decal;
            Open(Page.Paint);
        }

        /// <summary>Leaving the paint shop without paying puts the old paint back.</summary>
        void RevertPaint()
        {
            if (!paintTarget || (paintTarget.colour == paintWasColour && paintTarget.decal == paintWasDecal)) return;
            paintTarget.colour = paintWasColour; paintTarget.decal = paintWasDecal;
            paintTarget.Apply();
        }

        string PaintCost(MadMax.Vehicles.VehiclePaint p)
        {
            var parts = new List<string>();
            if (p.colour != paintWasColour)
            {
                var dye = MadMax.Vehicles.VehiclePaint.Dye(p.colour);
                parts.Add(p.colour == 0 ? "2 SCRAP (STRIP)" : dye != null ? "2 " + ItemIds.Name(dye) : "3 SCRAP + 1 OIL");
            }
            if (p.decal != paintWasDecal && p.decal != 0) parts.Add("1 SCRAP");
            return parts.Count == 0 ? "NO CHANGE" : string.Join(" + ", parts);
        }

        void SprayPaint(MadMax.Vehicles.VehiclePaint p)
        {
            var inv = game.Inventory;
            bool recolour = p.colour != paintWasColour, redecal = p.decal != paintWasDecal && p.decal != 0;
            var dye = MadMax.Vehicles.VehiclePaint.Dye(p.colour);
            bool ok = (!recolour || (p.colour == 0 ? inv.Get(ResourceType.Scrap) >= 2 : dye != null ? inv.GetItem(dye) >= 2 : inv.Get(ResourceType.Scrap) >= 3 && inv.Get(ResourceType.Oil) >= 1))
                      && (!redecal || inv.Get(ResourceType.Scrap) >= (recolour && dye == null ? 4 : 1));
            if (!ok) { game.Toast("NOT ENOUGH PAINT: " + PaintCost(p)); return; }
            if (recolour)
            {
                if (p.colour == 0) inv.TrySpend(ResourceType.Scrap, 2);
                else if (dye != null) inv.TakeItem(dye, 2);
                else { inv.TrySpend(ResourceType.Scrap, 3); inv.TrySpend(ResourceType.Oil, 1); }
            }
            if (redecal) inv.TrySpend(ResourceType.Scrap, 1);
            paintWasColour = p.colour; paintWasDecal = p.decal;
            game.Stats.Practice(MadMax.RPG.Skill.Mechanics, 2f);
            MadMax.Audio.Sfx.Play("pour", p.transform.position, 0.6f, 1.6f);
            game.Toast("FRESH PAINT: " + MadMax.Vehicles.VehiclePaint.ColourNames[p.colour] + (p.decal > 0 ? " WITH " + MadMax.Vehicles.Decals.Names[p.decal] : ""));
            Rebuild();
        }

        MadMax.Vehicles.VehicleArmor armourTarget;
        readonly int[] armourPlan = new int[MadMax.Vehicles.VehicleArmor.Zones];
        /// <summary>Armour page for a vehicle: pick a material per zone and weld it on (welder in the pack).</summary>
        public void OpenArmour(MadMax.Vehicles.VehicleArmor a)
        {
            armourTarget = a;
            for (int i = 0; i < armourPlan.Length; i++) armourPlan[i] = a.mat[i] == MadMax.Vehicles.ArmorMat.None ? 1 : (int)a.mat[i];
            Open(Page.Armour);
        }

        string ArmourLine(MadMax.Vehicles.VehicleArmor a, int i)
        {
            var z = (MadMax.Vehicles.ArmorZone)i;
            var cur = a.mat[i]; var plan = (MadMax.Vehicles.ArmorMat)armourPlan[i];
            string now = cur == MadMax.Vehicles.ArmorMat.None ? "BARE" : MadMax.Vehicles.VehicleArmor.MatNames[(int)cur] + " " + Mathf.RoundToInt(a.condition[i] * 100f) + "%";
            if (a.Voxels(z) == 0) return "N/A";
            if (plan == MadMax.Vehicles.ArmorMat.None) return now + (cur == MadMax.Vehicles.ArmorMat.None ? "" : "  > STRIP");
            float share = plan == cur ? 1f - a.condition[i] : 1f;
            if (share < 0.02f) return now + "  (" + VehicleArmorName(plan) + ")";
            a.Cost(z, plan, share, out var r1, out int n1, out var r2, out int n2);
            return now + "  > " + VehicleArmorName(plan) + "  " + n1 + " " + ResourceInfo.Name(r1) + (n2 > 0 ? " " + n2 + " " + ResourceInfo.Name(r2) : "");
        }

        static string VehicleArmorName(MadMax.Vehicles.ArmorMat m) => m == MadMax.Vehicles.ArmorMat.Steel ? "STEEL" : m == MadMax.Vehicles.ArmorMat.Composite ? "COMPOSITE" : m == MadMax.Vehicles.ArmorMat.Scrap ? "SCRAP" : "NONE";

        void MoveRes(Inventory from, Inventory to, ResourceType t, int n, bool intoBox)
        {
            if (n <= 0) return;
            if (intoBox)
            {
                float room = container.capacity - container.Weight;
                float w = MadMax.Items.ItemCatalog.ResourceWeight(t);
                if (w > 0f) n = Mathf.Min(n, Mathf.FloorToInt(room / w));
                if (n <= 0) { game.Toast(container.title + " IS FULL"); return; }
            }
            if (from.TrySpend(t, n)) to.Add(t, n);
            Rebuild();
        }

        void MoveItem(Inventory from, Inventory to, string id, int n, bool intoBox)
        {
            if (n <= 0) return;
            if (intoBox)
            {
                float room = container.capacity - container.Weight;
                float w = MadMax.Items.ItemCatalog.Weight(id);
                if (w > 0f) n = Mathf.Min(n, Mathf.FloorToInt(room / w));
                if (n <= 0) { game.Toast(container.title + " IS FULL"); return; }
            }
            if (from.TakeItem(id, n)) to.AddItem(id, n);
            if (!intoBox && id.StartsWith("tool_")) game.UpdateHotbarNow();
            Rebuild();
        }

        public void Close()
        {
            if (Current == Page.Paint) RevertPaint();
            if (TitleSequence.Playing && TitleSequence.Instance && !IntroRecorder.Recording) TitleSequence.Instance.Finish();
            if (Current == Page.Settings) GameSettings.Current.Save();
            Current = Page.None;
            Time.timeScale = 1f;
        }

        void Back()
        {
            switch (Current)
            {
                case Page.Settings: GameSettings.Current.Save(); if (settingsFrom != Page.None) Open(settingsFrom); else Close(); break;
                case Page.Main: break;                                   // main menu has no "back"
                case Page.NewGame: Open(Page.Main); break;
                case Page.Creation: Open(Page.NewGame); break;
                case Page.Research: Open(Page.Crafting); break;
                case Page.Repair: Open(Page.Crafting); break;
                case Page.Salvage: Open(Page.Crafting); break;
                case Page.Trade: if (talk != null && !talk.Ended) Open(Page.Talk); else Close(); break;
                default: Close(); break;
            }
        }

        void Rebuild()
        {
            items.Clear();
            var s = GameSettings.Current;
            switch (Current)
            {
                case Page.Main:
                    Add("CONTINUE", () => game.LoadGame(), () => SaveSystem.HasSave);
                    Add("NEW GAME", () => { hostNew = false; Open(Page.NewGame); });
                    Add("HOST GAME", () => { hostNew = true; Open(Page.NewGame); });
                    Add("JOIN GAME", () => Open(Page.Join));
                    Add("SETTINGS", () => Open(Page.Settings));
                    Add("QUIT", Quit);
                    break;
                case Page.Pause:
                    Add("RESUME", Close);
                    Add("OUTFIT", () => Open(Page.Character));
                    {
                        bool solo = MadMax.Net.NetSession.Instance == null || !MadMax.Net.NetSession.Instance.Online;
                        items.Add(new Item { label = "CLOCK", value = () => Mathf.FloorToInt(MadMax.World.DayNight.Hours).ToString("00") + ":" + Mathf.FloorToInt(MadMax.World.DayNight.Hours % 1f * 60f).ToString("00"),
                            adjust = d => MadMax.World.DayNight.SetHours(MadMax.World.DayNight.Hours + d), confirm = () => MadMax.World.DayNight.SetHours(MadMax.World.DayNight.Hours + 1f), enabled = () => solo, hint = "SINGLE PLAYER: A/D SETS THE TIME" });
                        items.Add(new Item { label = "DAY/NIGHT", value = () => GameRules.DayLengthNames[game.Rules.dayLength],
                            adjust = d => { game.Rules.dayLength = Mathf.Clamp(game.Rules.dayLength + d, 0, GameRules.DayLengths.Length - 1); MadMax.World.DayNight.DayMinutes = GameRules.DayLengths[game.Rules.dayLength]; },
                            confirm = () => { game.Rules.dayLength = (game.Rules.dayLength + 1) % GameRules.DayLengths.Length; MadMax.World.DayNight.DayMinutes = GameRules.DayLengths[game.Rules.dayLength]; }, enabled = () => solo });
                    }
                    Add("SAVE GAME", () => { game.SaveGame(); Close(); }, () => !(MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient));
                    Add("OPEN TO NETWORK", () => { game.Host(); Close(); }, () => MadMax.Net.NetSession.Instance == null || !MadMax.Net.NetSession.Instance.Online);
                    Add("DISCONNECT", () => { MadMax.Net.NetSession.Instance.Shutdown(); game.ReturnToMainMenu(); }, () => MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.Online);
                    Add("LOAD GAME", () => game.LoadGame(), () => SaveSystem.HasSave);
                    Add("SETTINGS", () => Open(Page.Settings));
                    Add("MAIN MENU", () => game.ReturnToMainMenu());
                    Add("QUIT", Quit);
                    break;
                case Page.Settings:
                    Opt("TRANSMISSION", () => s.manualTransmission ? "MANUAL" : "AUTO", d => s.manualTransmission = !s.manualTransmission);
                    Opt("PIXEL SIZE", () => s.PixelHeight + " LINES", d => s.pixelHeightIndex = Mathf.Clamp(s.pixelHeightIndex - d, 0, GameSettings.PixelHeights.Length - 1));
                    Opt("RENDER STYLE", () => s.vector ? "VECTOR (FULL RES)" : "PIXEL ART", d => s.vector = !s.vector);
                    Opt("DITHER", () => s.dither ? "ON" : "OFF", d => s.dither = !s.dither);
                    Opt("LINE OF SIGHT", () => s.lineOfSight ? "ON" : "OFF", d => s.lineOfSight = !s.lineOfSight);
                    Opt("INTRO FILM", () => s.intro ? "ON" : "OFF", d => s.intro = !s.intro);
                    Opt("BLOOD", () => s.blood ? "ON" : "OFF", d => s.blood = !s.blood);
                    Opt("SFX VOLUME", () => Mathf.RoundToInt(s.sfxVolume * 100) + "%", d => s.sfxVolume = Mathf.Clamp01(Mathf.Round((s.sfxVolume + d * 0.1f) * 10f) / 10f));
                    Opt("RADIO VOLUME", () => Mathf.RoundToInt(s.radioVolume * 100) + "%", d => s.radioVolume = Mathf.Clamp01(Mathf.Round((s.radioVolume + d * 0.1f) * 10f) / 10f));
                    Opt("LIGHT DETAIL", () => GameSettings.LightNames[s.lightDetail], d => s.lightDetail = Mathf.Clamp(s.lightDetail + d, 0, 2));
                    Opt("OUTLINE", () => s.outline == 0 ? "OFF" : s.outline + " PX", d => s.outline = Mathf.Clamp(s.outline + d, 0, 2));
                    Opt("SHADOWS", () => new[] { "OFF", "LOW", "HIGH" }[s.shadows], d => s.shadows = Mathf.Clamp(s.shadows + d, 0, 2));
                    Opt("BLOOM", () => s.bloom ? "ON" : "OFF", d => s.bloom = !s.bloom);
                    Opt("BRIGHTNESS", () => Mathf.RoundToInt(s.brightness * 100) + "%", d => s.brightness = Mathf.Clamp(s.brightness + d * 0.1f, 0.5f, 1.8f));
                    Opt("DETAIL / LOD", () => GameSettings.LodNames[s.lod], d => s.lod = Mathf.Clamp(s.lod + d, 0, 3));
                    Opt("RESOLUTION", () => s.ResolutionName, d => { int n = GameSettings.Resolutions.Length; s.resolutionIndex = n == 0 ? -1 : ((s.resolutionIndex < 0 ? n - 1 : s.resolutionIndex) + d + n) % n; });
                    Opt("FULLSCREEN", () => s.fullscreen ? "ON" : "OFF", d => s.fullscreen = !s.fullscreen);
                    Opt("VSYNC", () => s.vsync ? "ON" : "OFF", d => s.vsync = !s.vsync);
                    Add("BACK", Back);
                    break;
                case Page.Character:
                {
                    var rig = game.Player.Rig;
                    var a = rig.appearance;
                    void Look(string label, System.Func<string> v, System.Action<int> change) =>
                        items.Add(new Item { label = label, value = v, adjust = d => { change(d); game.Player.RebuildBody(); }, confirm = () => { change(1); game.Player.RebuildBody(); } });
                    // skin, height and build are chosen at character creation; a mirror allows a haircut or a shave
                    if (mirror)
                    {
                        Look("HAIR", () => a.hair.ToString().ToUpperInvariant(), d => a.hair = (HairStyle)(((int)a.hair + d + 6) % 6));
                        Look("HAIR COLOUR", () => (a.hairColor + 1).ToString(), d => a.hairColor = (a.hairColor + d + HumanDesign.HairColors.Length) % HumanDesign.HairColors.Length);
                        Look("BEARD", () => new[] { "NONE", "STUBBLE", "FULL" }[a.beard], d => a.beard = (a.beard + d + 3) % 3);
                    }
                    foreach (ClothingSlot slot in System.Enum.GetValues(typeof(ClothingSlot)))
                    {
                        var sl = slot;
                        items.Add(new Item { label = sl.ToString().ToUpperInvariant(), value = () => WornName(sl), adjust = d => CycleClothing(sl, d), confirm = () => CycleClothing(sl, 1) });
                    }
                    Add("BACK", Back);
                    break;
                }
                case Page.NewGame:
                {
                    var r = draftRules;
                    void R(string label, System.Func<string> v, System.Action<int> change) => items.Add(new Item { label = label, value = v, adjust = change, confirm = () => change(1) });
                    items.Add(new Item { label = "SEED", text = () => draftSeed, setText = v => { draftSeed = new string(System.Array.FindAll(v.ToCharArray(), char.IsDigit)); r.randomSeed = draftSeed.Length == 0; } });
                    R("RANDOM SEED", () => r.randomSeed ? "YES" : "NO", d => r.randomSeed = !r.randomSeed);
                    R("DIFFICULTY", () => GameRules.DifficultyNames[r.difficulty], d => r.difficulty = Mathf.Clamp(r.difficulty + d, 0, 3));
                    R("STARTING KIT", () => GameRules.KitNames[r.startingKit], d => r.startingKit = Mathf.Clamp(r.startingKit + d, 0, 2));
                    R("VEHICLES", () => GameRules.FleetNames[r.fleet], d => r.fleet = (r.fleet + d + 4) % 4);
                    R("RESOURCE YIELD", () => "X" + r.yield.ToString("0.0"), d => r.yield = Mathf.Clamp(r.yield + d * 0.25f, 0.25f, 3f));
                    R("FUEL USE", () => "X" + r.fuelUse.ToString("0.0"), d => r.fuelUse = Mathf.Clamp(r.fuelUse + d * 0.25f, 0.25f, 3f));
                    R("SKILL LEARNING", () => "X" + r.learning.ToString("0.0"), d => r.learning = Mathf.Clamp(r.learning + d * 0.25f, 0.25f, 4f));
                    R("DAMAGE TAKEN", () => "X" + r.damage.ToString("0.0"), d => r.damage = Mathf.Clamp(r.damage + d * 0.25f, 0.25f, 3f));
                    R("WRECKS", () => r.wrecks.ToString(), d => r.wrecks = Mathf.Clamp(r.wrecks + d * 6, 0, 120));
                    R("WEATHER", () => GameRules.WeatherNames[r.weather], d => r.weather = Mathf.Clamp(r.weather + d, 0, 3));
                    R("SEASON", () => GameRules.SeasonNames[r.season], d => r.season = (r.season + d + 4) % 4);
                    R("SEASONS TURN", () => GameRules.SeasonLengthNames[r.seasonLength], d => r.seasonLength = (r.seasonLength + d + GameRules.SeasonLengths.Length) % GameRules.SeasonLengths.Length);
                    R("RAIDS ON BASES", () => GameRules.RaidNames[r.raids], d => r.raids = (r.raids + d + GameRules.RaidNames.Length) % GameRules.RaidNames.Length);
                    R("SNOW AND ICE", () => r.snow ? "ON" : "OFF", d => r.snow = !r.snow);
                    R("BIOME SIZE", () => "X" + r.biomeScale.ToString("0.0"), d => r.biomeScale = Mathf.Clamp(r.biomeScale + d * 0.25f, 0.5f, 2.5f));
                    R("PERMADEATH", () => r.permadeath ? "ON" : "OFF", d => r.permadeath = !r.permadeath);
                    R("DAY LENGTH", () => GameRules.DayLengthNames[r.dayLength], d => r.dayLength = Mathf.Clamp(r.dayLength + d, 0, GameRules.DayLengths.Length - 1));
                    R("SURVIVAL NEEDS", () => r.survival ? "ON" : "OFF", d => r.survival = !r.survival);
                    R("HUNGER RATE", () => "X" + r.hungerRate.ToString("0.00"), d => r.hungerRate = Mathf.Clamp(r.hungerRate + d * 0.25f, 0.25f, 3f));
                    R("LOOT", () => GameRules.LootNames[r.loot], d => r.loot = Mathf.Clamp(r.loot + d, 0, 2));
                    Add("CHARACTER >", () => Open(Page.Creation));
                    Add(hostNew ? "START AND HOST" : "START", StartGame);
                    Add("BACK", () => Open(Page.Main));
                    break;
                }
                case Page.Creation:
                {
                    var st = draftStats; var a = draftLook;
                    items.Add(new Item { label = "NAME", text = () => st.name, setText = v => st.name = v });
                    void L(string label, System.Func<string> v, System.Action<int> change) => items.Add(new Item { label = label, value = v, adjust = change, confirm = () => change(1) });
                    L("SKIN", () => (a.skinTone + 1).ToString(), d => a.skinTone = (a.skinTone + d + 4) % 4);
                    L("HAIR", () => a.hair.ToString().ToUpperInvariant(), d => a.hair = (HairStyle)(((int)a.hair + d + 6) % 6));
                    L("HAIR COLOUR", () => (a.hairColor + 1).ToString(), d => a.hairColor = (a.hairColor + d + HumanDesign.HairColors.Length) % HumanDesign.HairColors.Length);
                    L("BEARD", () => new[] { "NONE", "STUBBLE", "FULL" }[a.beard], d => a.beard = (a.beard + d + 3) % 3);
                    L("HEIGHT", () => Mathf.RoundToInt(a.height * 180) + " CM", d => a.height = Mathf.Clamp(a.height + d * 0.02f, 0.9f, 1.1f));
                    L("BUILD", () => Mathf.RoundToInt(a.build * 100) + "%", d => a.build = Mathf.Clamp(a.build + d * 0.05f, 0.85f, 1.2f));
                    for (int i = 0; i < MadMax.RPG.CharacterStats.AttrCount; i++)
                    {
                        int ai = i;
                        L(MadMax.RPG.CharacterStats.AttrNames[i], () => st.attributes[ai].ToString(), d =>
                        {
                            int nv = Mathf.Clamp(st.attributes[ai] + d, 2, 9);
                            if (d > 0 && PointsLeft() <= 0) return;
                            st.attributes[ai] = nv;
                        });
                    }
                    foreach (var t in MadMax.RPG.Traits.All)
                    {
                        var tt = t;
                        items.Add(new Item { label = t.name, value = () => (st.traits.Contains(tt.id) ? "[X] " : "[ ] ") + (tt.cost > 0 ? "-" : "+") + Mathf.Abs(tt.cost), confirm = () => ToggleTrait(tt), adjust = d => ToggleTrait(tt), hint = t.description });
                    }
                    Add("DONE", () => Open(Page.NewGame));
                    break;
                }
                case Page.Join:
                    items.Add(new Item { label = "ADDRESS", text = () => joinAddress, setText = v => joinAddress = v });
                    items.Add(new Item { label = "PORT", text = () => joinPort, setText = v => joinPort = v });
                    items.Add(new Item { label = "NAME", text = () => joinName, setText = v => joinName = v });
                    Add("CONNECT", () => { ushort.TryParse(joinPort, out var port); game.Join(joinAddress, port == 0 ? MadMax.Net.NetSession.DefaultPort : port, joinName); });
                    Add("BACK", () => Open(settingsFrom == Page.None ? Page.Main : Page.Main));
                    break;
                case Page.Inventory:
                {
                    var inv = game.Inventory;
                    foreach (MadMax.Items.ItemCategory cat2 in System.Enum.GetValues(typeof(MadMax.Items.ItemCategory)))
                    {
                        bool header = false;
                        foreach (var kv in inv.Items)
                        {
                            if (kv.Value <= 0 || MadMax.Items.ItemCatalog.Category(kv.Key) != cat2) continue;
                            if (!header) { items.Add(new Item { label = "- " + cat2.ToString().ToUpperInvariant() + " -", enabled = () => false }); header = true; }
                            var id = kv.Key;
                            items.Add(new Item
                            {
                                id = id, label = MadMax.Items.ItemCatalog.Name(id) + (game.HasMake(id) ? " (" + game.QualityName(id) + ")" : ""),
                                value = () => { int slot = System.Array.IndexOf(game.Hotbar, id); return (slot >= 0 ? "[" + (slot + 1) + "] " : "") + "X" + inv.GetItem(id) + "  " + (inv.GetItem(id) * MadMax.Items.ItemCatalog.Weight(id)).ToString("0.0") + "KG"; },
                                confirm = () => game.UseItem(id),
                                hint = MadMax.RPG.MediaLibrary.IsMedia(id) ? (game.Stats.consumed.Contains(id) ? "ALREADY STUDIED - LITTLE LEFT TO LEARN" : "ENTER TO STUDY") : "ENTER USE   1-8 ASSIGN TO HOTBAR"
                            });
                        }
                    }
                    items.Add(new Item { label = "- RESOURCES -", enabled = () => false });
                    for (int t = 1; t < ResourceInfo.Count; t++)
                    {
                        var rt = (ResourceType)t;
                        if (inv.Get(rt) <= 0) continue;
                        items.Add(new Item { label = ResourceInfo.Name(rt), value = () => inv.Get(rt) + (ResourceInfo.IsFluid(rt) ? "L  " : "  ") + (inv.Get(rt) * MadMax.Items.ItemCatalog.ResourceWeight(rt)).ToString("0.0") + "KG" });
                    }
                    break;
                }
                case Page.Skills:
                {
                    var st = game.Stats;
                    items.Add(new Item { label = st.name, value = () => "HP " + Mathf.CeilToInt(st.health) + "/" + Mathf.CeilToInt(st.MaxHealth) + "  STA " + Mathf.CeilToInt(st.stamina), enabled = () => false });
                    for (int i = 0; i < MadMax.RPG.CharacterStats.AttrCount; i++)
                    {
                        var a = (MadMax.RPG.Attr)i;
                        items.Add(new Item { label = MadMax.RPG.CharacterStats.AttrNames[i], value = () => st.Attribute(a).ToString() });
                    }
                    for (int i = 0; i < MadMax.RPG.CharacterStats.SkillCount; i++)
                    {
                        var sk = (MadMax.RPG.Skill)i;
                        items.Add(new Item { label = MadMax.RPG.CharacterStats.SkillNames[i], value = () => "LV " + st.Level(sk) + " " + Bar(st.LevelProgress(sk)) });
                    }
                    foreach (var id in st.traits) { var t = MadMax.RPG.Traits.Get(id); if (t != null) items.Add(new Item { label = t.name, value = () => "TRAIT", hint = t.description }); }
                    foreach (var k in st.knowledge) items.Add(new Item { label = k.Replace("k_", "").Replace('_', ' ').ToUpperInvariant(), value = () => "KNOWN" });
                    items.Add(new Item { label = "CARRYING", value = () => game.CarriedWeight.ToString("0.0") + " / " + st.CarryCapacity.ToString("0") + " KG" });
                    foreach (var kv in game.FishRecords)
                    {
                        var fd = MadMax.Items.FishLibrary.Get(kv.Key);
                        float kg = kv.Value;
                        if (fd != null) items.Add(new Item { label = "BEST " + fd.name, value = () => kg.ToString("0.00") + " KG", enabled = () => false });
                    }
                    break;
                }
                case Page.Container:
                {
                    if (!container) { Close(); break; }
                    var box = container.inventory;
                    void Side(Inventory from, Inventory to, string header, bool intoBox)
                    {
                        items.Add(new Item { label = header, enabled = () => false });
                        for (int t = 1; t < ResourceInfo.Count; t++)
                        {
                            var rt = (ResourceType)t;
                            if (from.Get(rt) <= 0) continue;
                            items.Add(new Item
                            {
                                label = ResourceInfo.Name(rt), value = () => from.Get(rt) + (ResourceInfo.IsFluid(rt) ? "L" : ""),
                                confirm = () => MoveRes(from, to, rt, Mathf.Min(5, from.Get(rt)), intoBox), adjust = d => MoveRes(from, to, rt, from.Get(rt), intoBox),
                                hint = "ENTER MOVE 5   A/D MOVE ALL"
                            });
                        }
                        foreach (var kv in new List<KeyValuePair<string, int>>(from.Items))
                        {
                            if (kv.Value <= 0) continue;
                            var id = kv.Key;
                            items.Add(new Item
                            {
                                id = intoBox ? id : null, label = MadMax.Items.ItemCatalog.Name(id), value = () => "X" + from.GetItem(id),
                                confirm = () => MoveItem(from, to, id, 1, intoBox), adjust = d => MoveItem(from, to, id, from.GetItem(id), intoBox),
                                hint = "ENTER MOVE 1   A/D MOVE ALL"
                            });
                        }
                    }
                    Side(game.Inventory, box, "- YOUR PACK -", true);
                    Side(box, game.Inventory, "- " + container.title + " -", false);
                    break;
                }
                case Page.Health:
                {
                    var st = game.Stats;
                    foreach (var inj in st.injuries)
                    {
                        var i2 = inj;
                        items.Add(new Item { label = MadMax.RPG.Injury.ZoneNames[(int)inj.zone] + ": " + MadMax.RPG.Injury.WoundNames[(int)inj.type], value = () => i2.Status, confirm = () => { game.Treat(i2); Rebuild(); }, hint = "ENTER TREAT (SPLINT / DISINFECT / BANDAGE)" });
                    }
                    if (st.injuries.Count == 0) items.Add(new Item { label = "NO INJURIES", enabled = () => false });
                    break;
                }
                case Page.Salvage:
                {
                    // break items down into half their materials
                    var ids = new List<string>();
                    foreach (var kv in game.Inventory.Items) if (kv.Value > 0 && game.SalvageRecipe(kv.Key) != null) ids.Add(kv.Key);
                    foreach (var id in ids)
                    {
                        var sid = id;
                        items.Add(new Item
                        {
                            label = ItemCatalog.Name(sid) + " X" + game.Inventory.GetItem(sid),
                            value = () =>
                            {
                                var sb = new System.Text.StringBuilder();
                                foreach (var (t, n) in game.SalvageYield(sid)) sb.Append('+').Append(n).Append(' ').Append(ResourceInfo.Name(t)).Append(' ');
                                return sb.ToString();
                            },
                            enabled = () => game.Inventory.GetItem(sid) > 0,
                            confirm = () => { game.Salvage(sid); Rebuild(); },
                            hint = "ENTER BREAK ONE DOWN INTO MATERIALS"
                        });
                    }
                    // loose vehicle parts by the bench: tyres to rubber, engines to metal
                    foreach (var part in game.SalvageableParts(station))
                    {
                        var vp = part;
                        items.Add(new Item
                        {
                            label = vp.partId.Replace('_', ' ').ToUpperInvariant() + " (PART)",
                            value = () =>
                            {
                                if (!vp) return "";
                                var sb = new System.Text.StringBuilder();
                                foreach (var (t, n) in game.PartYield(vp)) sb.Append('+').Append(n).Append(' ').Append(ResourceInfo.Name(t)).Append(' ');
                                return sb.ToString();
                            },
                            enabled = () => vp,
                            confirm = () => { game.SalvagePart(vp); Rebuild(); },
                            hint = "ENTER BREAK THE LOOSE PART DOWN"
                        });
                        ids.Add(vp.partId);
                    }
                    if (ids.Count == 0) items.Add(new Item { label = "NOTHING TO SALVAGE", enabled = () => false });
                    Add("BACK", () => Open(Page.Crafting));
                    break;
                }
                case Page.Board:
                {
                    var b = boardTarget;
                    if (!b) { Close(); break; }
                    var st = b.Settlement;
                    items.Add(new Item { label = MadMax.Npc.Market.Hint(st), enabled = () => false });
                    // yours: claim, hand in, or see progress
                    foreach (var job in new List<MadMax.Npc.Contract>(MadMax.Npc.Contracts.Active))
                    {
                        var c = job;
                        bool here = c.Delivery && c.dest == b.town;
                        items.Add(new Item
                        {
                            label = c.title,
                            value = () => c.failed ? "FAILED" : c.completed ? "CLAIM " + c.reward : here ? "HAND IN " + c.done + "/" + c.need : c.Delivery ? "DUE DAY " + (c.deadline + 1) : c.done + "/" + c.need,
                            confirm = () =>
                            {
                                if (c.failed) { MadMax.Npc.Contracts.Active.Remove(c); Rebuild(); return; }
                                if (c.completed) MadMax.Npc.Contracts.Pay(game, c);
                                else if (here) MadMax.Npc.Contracts.Deliver(game, c, b.transform.position);
                                Rebuild();
                            },
                            hint = c.failed ? "E: STRIKE IT OFF" : c.completed ? "E: TAKE THE PAY" : here ? "E: HAND IN THE CRATES WITHIN 15 M" : "IN PROGRESS",
                        });
                    }
                    items.Add(new Item { label = "- TODAY'S JOBS -", enabled = () => false });
                    foreach (var job in MadMax.Npc.Contracts.Offers(st))
                    {
                        var c = job;
                        items.Add(new Item
                        {
                            label = c.title, value = () => c.reward + " SCRAP" + (c.chits > 0 ? " +" + c.chits + " CHITS" : ""),
                            confirm = () => { MadMax.Npc.Contracts.Accept(game, c, b.transform.position); Rebuild(); },
                            hint = c.Delivery ? "E: TAKE THE HAUL (CRATES APPEAR BY THE BOARD; RAIDERS SMELL CARGO)" : "E: TAKE THE JOB",
                        });
                    }
                    break;
                }
                case Page.Paint:
                {
                    var p = paintTarget;
                    if (!p) { Close(); break; }
                    int nc = MadMax.Vehicles.VehiclePaint.ColourNames.Length, nd = MadMax.Vehicles.Decals.Names.Length;
                    items.Add(new Item { label = "COLOUR", value = () => MadMax.Vehicles.VehiclePaint.ColourNames[p.colour], adjust = d => { p.colour = (p.colour + d + nc) % nc; p.Apply(); },
                        hint = "A/D PICK A COLOUR (PREVIEW). DYES FOR THE PLAIN ONES, SCRAP + OIL FOR THE MIXED" });
                    items.Add(new Item { label = "DECAL", value = () => MadMax.Vehicles.Decals.Names[p.decal], adjust = d => { p.decal = (p.decal + d + nd) % nd; p.Apply(); },
                        hint = "A/D PICK AN EMBLEM. A GANG'S EMBLEM FOOLS ITS LOOKOUTS FROM AFAR" });
                    items.Add(new Item { label = "SPRAY IT", value = () => PaintCost(p), confirm = () => SprayPaint(p), enabled = () => p.colour != paintWasColour || p.decal != paintWasDecal, hint = "E PAY AND SPRAY. LEAVING WITHOUT PAYING KEEPS THE OLD PAINT" });
                    break;
                }
                case Page.Tuning:
                {
                    var t = tuneTarget;
                    if (!t) { Close(); break; }
                    int mech = game.Stats.Level(MadMax.RPG.Skill.Mechanics);
                    void Slider(string label, int need, Func<float> get, Action<float> set, float min, float max, float step, Func<float, string> show, string hint)
                    {
                        items.Add(new Item
                        {
                            label = label + (mech < need ? " [MECH " + need + "]" : ""), value = () => show(get()), hint = hint,
                            adjust = d =>
                            {
                                if (mech < need) { game.Toast("NEEDS MECHANICS " + need); return; }
                                set(Mathf.Clamp(Mathf.Round((get() + d * step) / step) * step, min, max));
                                t.Apply(); game.Stats.Practice(MadMax.RPG.Skill.Mechanics, 0.5f);
                            },
                        });
                    }
                    string Signed(float v, float scale, string unit) => (v > 0.001f ? "+" : "") + Mathf.RoundToInt(v * scale) + unit;
                    Slider("ENGINE MAP", 2, () => t.map, v => t.map = v, -1f, 1f, 0.25f, v => v < -0.01f ? "ECONOMY " + Mathf.RoundToInt(-v * 100) + "%" : v > 0.01f ? "POWER " + Mathf.RoundToInt(v * 100) + "%" : "STOCK",
                        "POWER: +15% TORQUE, MORE FUEL AND HEAT.  ECONOMY: LESS OF BOTH");
                    foreach (var (name, kit, get, fit) in new (string, string, Func<bool>, Action)[] { ("TURBO", "kit_turbo", () => t.turbo, () => t.turbo = true), ("SUPERCHARGER", "kit_supercharger", () => t.supercharger, () => t.supercharger = true) })
                        items.Add(new Item
                        {
                            label = name + (mech < 4 ? " [MECH 4]" : ""), value = () => get() ? "FITTED" : game.Inventory.GetItem(kit) > 0 ? "E TO FIT" : "NEEDS A KIT",
                            confirm = () => game.FitTuningKit(t, kit, get(), fit), enabled = () => !get(),
                            hint = name == "TURBO" ? "BIG PUSH HIGH IN THE REVS, RUNS HOT" : "+20% ALL THE WAY, THIRSTY",
                        });
                    items.Add(new Item { label = "NITROUS", value = () => t.nitrous + " BOTTLES", hint = "E FIT A BOTTLE (5 S BOOST).  FIRE WITH LEFT CTRL / LEFT STICK",
                        confirm = () => { if (game.Inventory.TakeItem("use_nitrous")) { t.nitrous++; game.Toast("NITROUS BOTTLE FITTED (" + t.nitrous + ")"); } else game.Toast("NO NITROUS BOTTLES"); } });
                    Slider("GEARING", 3, () => t.gearing, v => t.gearing = v, -1f, 1f, 0.25f, v => v < -0.01f ? "SHORT" : v > 0.01f ? "LONG" : "STOCK", "SHORT: PULLS HARDER, LOWER TOP SPEED.  LONG: THE OTHER WAY");
                    Slider("FINAL DRIVE", 3, () => t.finalDrive, v => t.finalDrive = v, -1f, 1f, 0.25f, v => Signed(v, 15f, "%"), "HIGHER: MORE PULL, LOWER TOP SPEED");
                    Slider("RIDE HEIGHT", 1, () => t.ride, v => t.ride = v, -1f, 1f, 0.25f, v => Signed(v, v < 0f ? 5f : 12f, " CM"), "LIFT FOR ROCKS AND MUD, DROP FOR THE ROAD");
                    Slider("SPRINGS", 2, () => t.stiffness, v => t.stiffness = v, -1f, 1f, 0.25f, v => v < -0.01f ? "SOFT" : v > 0.01f ? "STIFF" : "STOCK", "SOFT SOAKS BUMPS, STIFF HOLDS CORNERS");
                    Slider("DAMPERS", 2, () => t.damping, v => t.damping = v, -1f, 1f, 0.25f, v => Signed(v, 35f, "%"), "MORE DAMPING: LESS BOUNCE, HARSHER RIDE");
                    Slider("BRAKE BIAS", 1, () => t.brakeBias, v => t.brakeBias = v, 0.4f, 0.8f, 0.05f, v => "FRONT " + Mathf.RoundToInt(v * 100) + "%", "REARWARD BIAS TURNS IN, TOO MUCH SPINS YOU");
                    items.Add(new Item { label = "BRAKES" + (mech < 2 ? " [MECH 2]" : ""), value = () => BrakeNames[t.brakeLevel], confirm = () => game.UpgradeBrakes(t), enabled = () => t.brakeLevel < 2, hint = "E UPGRADE: 4 IRON + 2 COPPER (+25% STOPPING)" });
                    Slider("TYRE PRESSURE", 0, () => t.pressure, v => t.pressure = v, 0.6f, 1.25f, 0.05f, v => (v * 2.2f).ToString("0.0") + " BAR" + (v < 0.85f ? " SOFT" : v > 1.1f ? " HARD" : ""), "LOW: SAND AND MUD GRIP, SLOWER, WEARS.  HIGH: FAST ON ROADS");
                    items.Add(new Item { label = "BALLAST", value = () => Mathf.RoundToInt(t.ballast) + " KG", adjust = d => game.TuneBallast(t, d), hint = "A/D LOAD OR UNLOAD 25 KG OF STONE (TRACTION, STABILITY)" });
                    items.Add(new Item { label = "INTERIOR", value = () => t.stripped ? "STRIPPED" : "STOCK", confirm = () => game.StripInterior(t), hint = "E STRIP IT (-8% BODY WEIGHT, +SCRAP, CLOTH) OR REFIT IT" });
                    break;
                }
                case Page.Armour:
                {
                    var a = armourTarget;
                    if (!a) { Close(); break; }
                    items.Add(new Item { label = WastelandGame.Name(a), value = () => "+" + Mathf.RoundToInt(a.TotalKg) + " KG ARMOUR", enabled = () => false });
                    for (int zi = 0; zi < MadMax.Vehicles.VehicleArmor.Zones; zi++)
                    {
                        int i = zi;
                        items.Add(new Item
                        {
                            label = MadMax.Vehicles.VehicleArmor.ZoneNames[i],
                            value = () => ArmourLine(a, i),
                            adjust = d => armourPlan[i] = (armourPlan[i] + d + 4) % 4,
                            confirm = () => game.WeldArmour(a, (MadMax.Vehicles.ArmorZone)i, (MadMax.Vehicles.ArmorMat)armourPlan[i]),
                            enabled = () => a.Voxels((MadMax.Vehicles.ArmorZone)i) > 0,
                            hint = "A/D PICK MATERIAL (NONE = STRIP)   E WELD IT ON / REPAIR   STEEL STOPS BULLETS, COMPOSITE SOAKS CRASHES BUT BURNS",
                        });
                    }
                    break;
                }
                case Page.Repair when station && station.type == "sewing":
                {
                    // mend garments: the ones you wear and the spares in the pack
                    var ids = new List<string>();
                    foreach (var id in game.Player.Rig.outfit) ids.Add(id);
                    foreach (var kv in game.Inventory.Items)
                    {
                        var cd = kv.Value > 0 ? ClothingLibrary.Get(kv.Key) : null;
                        if (cd != null && !ids.Contains(cd.id)) ids.Add(cd.id);
                    }
                    foreach (var id in ids)
                    {
                        var gid = id; var cd = ClothingLibrary.Get(id);
                        if (cd == null) continue;
                        items.Add(new Item
                        {
                            label = cd.name + (game.Wearing(gid) ? " (WORN)" : ""),
                            value = () => Mathf.RoundToInt(game.GarmentCondition(gid) * 100f) + "%" + (game.GarmentCondition(gid) < 0.999f ? "  " + game.MendCost(gid) + " " + ResourceInfo.Name(game.MendWith(gid)) : ""),
                            enabled = () => game.GarmentCondition(gid) < 0.999f,
                            confirm = () => { game.Mend(gid); Rebuild(); },
                            hint = "ENTER MEND (ARMOUR TAKES ITS OWN MATERIAL)"
                        });
                    }
                    if (ids.Count == 0) items.Add(new Item { label = "NO CLOTHES TO MEND", enabled = () => false });
                    Add("BACK", () => Open(Page.Crafting));
                    break;
                }
                case Page.Repair:
                {
                    bool any = false;
                    foreach (var kv in game.Inventory.Items)
                    {
                        if (kv.Value <= 0 || !ToolLibrary.Has(kv.Key)) continue;
                        var tid = kv.Key;
                        any = true;
                        items.Add(new Item
                        {
                            label = ItemCatalog.Name(tid),
                            value = () => { var (t, n) = game.RepairCost(tid); return Mathf.RoundToInt(game.Condition(tid) * 100f) + "%" + (game.Condition(tid) < 0.999f ? "  " + n + " " + ResourceInfo.Name(t) : ""); },
                            enabled = () => game.Condition(tid) < 0.999f,
                            confirm = () => { game.RepairTool(tid); Rebuild(); },
                            hint = "ENTER REPAIR WITH THE MATERIAL SHOWN"
                        });
                    }
                    if (!any) items.Add(new Item { label = "NO TOOLS TO REPAIR", enabled = () => false });
                    Add("BACK", () => Open(Page.Crafting));
                    break;
                }
                case Page.Talk:
                    if (talk == null || !talkNpc) { Close(); break; }
                    foreach (var ch in talk.choices) { var c = ch; items.Add(new Item { label = c.label, hint = c.hint, confirm = () => Choose(c) }); }
                    break;
                case Page.Trade:
                {
                    if (!talkNpc) { Close(); break; }
                    var npc = talkNpc; var p = npc.Profile; var inv = game.Inventory;
                    MadMax.Npc.Trade.Town = MadMax.Npc.Market.Near(npc.transform.position);
                    float bargain = MadMax.Npc.Trade.Bargain(game, npc.State);
                    items.Add(new Item { label = MadMax.Npc.Market.TownName(MadMax.Npc.Trade.Town) + ": " + MadMax.Npc.Market.Hint(MadMax.Npc.Trade.Town), enabled = () => false });
                    if (p.kind == "fuel" && inv.GetItem(MadMax.Npc.Contracts.Chit) > 0) items.Add(new Item { label = "GUILD CHITS", value = () => inv.GetItem(MadMax.Npc.Contracts.Chit) + " (" + MadMax.Npc.Trade.ChitValue + " SCRAP EACH HERE)", enabled = () => false });
                    items.Add(new Item { label = "- " + MadMax.Npc.NpcLore.TradeTitle(p.kind) + " SELLS -", enabled = () => false });
                    foreach (var o in MadMax.Npc.Trade.Stock(p, npc.State, bargain))
                    {
                        var offer = o;
                        bool fluid = o.id.StartsWith("res:") && ResourceInfo.IsFluid((ResourceType)int.Parse(o.id.Substring(4)));
                        items.Add(new Item
                        {
                            label = MadMax.Npc.Trade.Name(o.id), value = () => "X" + offer.count + (fluid ? "L" : "") + "  " + offer.price + " SCRAP",
                            confirm = () => { MadMax.Npc.Trade.Buy(game, npc, offer, 1); Rebuild(); },
                            adjust = d => { MadMax.Npc.Trade.Buy(game, npc, offer, d > 0 ? 10 : 5); Rebuild(); },
                            enabled = () => inv.Get(ResourceType.Scrap) + (p.kind == "fuel" ? inv.GetItem(MadMax.Npc.Contracts.Chit) * MadMax.Npc.Trade.ChitValue : 0) >= offer.price, hint = "ENTER BUY 1   A BUY 5   D BUY 10"
                        });
                    }
                    items.Add(new Item { label = "- YOU SELL -", enabled = () => false });
                    for (int t = 2; t < ResourceInfo.Count; t++)
                    {
                        var rt = (ResourceType)t; string id = "res:" + t;
                        if (inv.Get(rt) <= 0 || !MadMax.Npc.Trade.Buys(p.kind, id)) continue;
                        int price = MadMax.Npc.Trade.SellPrice(id, bargain);
                        if (price <= 0) continue;
                        items.Add(new Item
                        {
                            label = ResourceInfo.Name(rt), value = () => inv.Get(rt) + (ResourceInfo.IsFluid(rt) ? "L" : "") + "  " + price + " EACH",
                            confirm = () => { MadMax.Npc.Trade.Sell(game, npc, id, 1, bargain); Rebuild(); },
                            adjust = d => { MadMax.Npc.Trade.Sell(game, npc, id, d > 0 ? inv.Get(rt) : 10, bargain); Rebuild(); },
                            hint = "ENTER SELL 1   A SELL 10   D SELL ALL"
                        });
                    }
                    foreach (var kv in new List<KeyValuePair<string, int>>(inv.Items))
                    {
                        var id = kv.Key;
                        if (kv.Value <= 0 || !MadMax.Npc.Trade.Buys(p.kind, id)) continue;
                        int price = MadMax.Npc.Trade.SellPrice(id, bargain);
                        if (price <= 0) continue;
                        items.Add(new Item
                        {
                            label = ItemCatalog.Name(id), value = () => "X" + inv.GetItem(id) + "  " + price + " EACH",
                            confirm = () => { MadMax.Npc.Trade.Sell(game, npc, id, 1, bargain); Rebuild(); },
                            adjust = d => { MadMax.Npc.Trade.Sell(game, npc, id, d > 0 ? inv.GetItem(id) : 1, bargain); Rebuild(); },
                            hint = "ENTER SELL 1   D SELL ALL"
                        });
                    }
                    Add("BACK", () => { if (talk != null && !talk.Ended) Open(Page.Talk); else Close(); });
                    break;
                }
                case Page.Research:
                    foreach (var r in MadMax.RPG.MediaLibrary.Research)
                    {
                        var rr = r;
                        string cost = ""; foreach (var (t, n) in r.cost) cost += n + " " + ResourceInfo.Name(t) + " ";
                        items.Add(new Item
                        {
                            label = r.name,
                            value = () => game.Stats.Knows(rr.grants) ? "KNOWN" : game.LearningId == rr.id ? Mathf.RoundToInt(game.LearningProgress * 100) + "%" : cost,
                            enabled = () => game.CanResearch(rr) || game.LearningId == rr.id,
                            confirm = () => { game.StartResearch(rr, station); Close(); },
                            hint = r.description + "   NEEDS " + MadMax.RPG.CharacterStats.SkillNames[(int)r.skill] + " " + r.minLevel
                        });
                    }
                    Add("BACK", () => Open(Page.Crafting));
                    break;
                case Page.Crafting:
                    var cats = StationCategories();
                    category = Mathf.Clamp(category, 0, Mathf.Max(0, cats.Count - 1));
                    var cat = cats.Count > 0 ? cats[category] : RecipeCategory.Tools;
                    foreach (var r in RecipeLibrary.All)
                    {
                        if (r.category != cat || r.station != StationType) continue;
                        var rec = r;
                        bool known = game.Stats.Knows(RecipeLibrary.KnowledgeFor(r));
                        items.Add(new Item { label = known ? r.name : r.name + " ?", recipe = r, confirm = () => game.Craft(rec, station), enabled = () => known && game.CanCraft(rec, station) });
                    }
                    break;
            }
            cursor = Mathf.Clamp(cursor, 0, Mathf.Max(0, items.Count - 1));
        }

        void Add(string label, Action confirm, Func<bool> enabled = null) => items.Add(new Item { label = label, confirm = confirm, enabled = enabled });

        void Opt(string label, Func<string> value, Action<int> adjust)
        {
            var s = GameSettings.Current;
            items.Add(new Item { label = label, value = value, adjust = d => { adjust(d); s.Apply(game); }, confirm = () => { adjust(1); s.Apply(game); } });
        }

        static void Quit() => ScreenFader.Quit(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        bool Enabled(Item i) => i.enabled == null || i.enabled();

        static string Bar(float t) { int n = Mathf.RoundToInt(Mathf.Clamp01(t) * 8); return new string('#', n) + new string('-', 8 - n); }

        // ---- new game drafts
        GameRules draftRules = new GameRules();
        MadMax.RPG.CharacterStats draftStats = new MadMax.RPG.CharacterStats();
        Appearance draftLook = new Appearance();
        string draftSeed = "";
        bool hostNew;
        const int CreationPoints = 6;

        int PointsLeft()
        {
            int spent = 0;
            foreach (var v in draftStats.attributes) spent += v - 5;
            foreach (var id in draftStats.traits) { var t = MadMax.RPG.Traits.Get(id); if (t != null) spent += t.cost; }
            return CreationPoints - spent;
        }

        void ToggleTrait(MadMax.RPG.TraitDef t)
        {
            if (draftStats.traits.Contains(t.id)) { draftStats.traits.Remove(t.id); return; }
            if (t.cost > PointsLeft()) { game.Toast("NOT ENOUGH POINTS"); return; }
            draftStats.traits.Add(t.id);
        }

        void StartGame()
        {
            if (PointsLeft() < 0) { game.Toast("TOO MANY POINTS SPENT"); return; }
            if (int.TryParse(draftSeed, out var sd) && sd > 0) { draftRules.seed = sd; draftRules.randomSeed = false; }
            var st = draftStats;
            draftStats = new MadMax.RPG.CharacterStats();
            game.StartNewGame(draftRules, st, draftLook, hostNew);
        }

        string joinAddress = "127.0.0.1", joinPort = "7777", joinName = "WANDERER";
        string typed = "";

        void OnEnable() { if (Keyboard.current != null) Keyboard.current.onTextInput += OnText; }
        void OnDisable() { if (Keyboard.current != null) Keyboard.current.onTextInput -= OnText; }
        void OnText(char c) { if (c >= ' ' && c < 127) typed += c; }

        string WornName(ClothingSlot slot)
        {
            foreach (var id in game.Player.Rig.outfit) { var d = ClothingLibrary.Get(id); if (d != null && d.slot == slot) return d.name; }
            return "NONE";
        }

        /// <summary>Step through owned garments for a slot (plus "none").</summary>
        void CycleClothing(ClothingSlot slot, int dir)
        {
            var options = new List<string> { null };
            foreach (var d in ClothingLibrary.All) if (d.slot == slot && game.Inventory.GetItem(ClothingLibrary.ItemId(d)) > 0) options.Add(d.id);
            var rig = game.Player.Rig;
            string current = null;
            foreach (var id in rig.outfit) { var d = ClothingLibrary.Get(id); if (d != null && d.slot == slot) current = d.id; }
            int i = (options.IndexOf(current) + dir + options.Count) % options.Count;
            if (current != null) rig.outfit.Remove(current);
            if (options[i] != null) rig.outfit.Add(options[i]);
            game.Player.RebuildBody();
        }

        /// <summary>Handles input. Called by WastelandGame every frame (also while closed, for Esc).</summary>
        public void Tick()
        {
            var kb = Keyboard.current; var pad = Gamepad.current; var mouse = Mouse.current;
            bool esc = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (!IsOpen)
            {
                if (esc) Open(Page.Pause);
                return;
            }
            if (esc || (pad != null && pad.buttonEast.wasPressedThisFrame)) { if (Current == Page.Join) Open(Page.Main); else Back(); return; }
            if ((Current == Page.Talk || Current == Page.Trade) && (!talkNpc || !talkNpc.Alive || Vector3.Distance(talkNpc.transform.position, game.Current ? game.Current.transform.position : game.Player.transform.position) > (game.Current ? 40f : 6f))) { Close(); return; }
            if (items.Count > 0 && items[cursor].text != null)
            {
                var field = items[cursor];
                string v = field.text();
                if (typed.Length > 0) { v = (v + typed.ToUpperInvariant()); if (v.Length > 40) v = v.Substring(0, 40); field.setText(v); }
                if (kb != null && kb.backspaceKey.wasPressedThisFrame && v.Length > 0) field.setText(v.Substring(0, v.Length - 1));
            }
            typed = "";
            if (Current == Page.Crafting && ((kb != null && kb.eKey.wasPressedThisFrame) || (kb != null && kb.tabKey.wasPressedThisFrame))) { Close(); return; }
            if (Current == Page.Crafting && kb != null && kb.rKey.wasPressedThisFrame) { Open(Page.Research); return; }
            if (Current == Page.Crafting && kb != null && kb.tKey.wasPressedThisFrame) { Open(Page.Repair); return; }
            if (Current == Page.Crafting && kb != null && kb.yKey.wasPressedThisFrame) { Open(Page.Salvage); return; }
            if (Current == Page.Crafting && kb != null && kb.xKey.wasPressedThisFrame) { game.CancelLastJob(station); return; }
            if ((Current == Page.Inventory && kb != null && kb.iKey.wasPressedThisFrame) || (Current == Page.Skills && kb != null && kb.pKey.wasPressedThisFrame) || (Current == Page.Health && kb != null && kb.oKey.wasPressedThisFrame)) { Close(); return; }
            if (Current == Page.Inventory && kb != null && items.Count > 0 && items[cursor].id != null)
                for (int i = 0; i < WastelandGame.HotbarSize; i++) if (kb[Key.Digit1 + i].wasPressedThisFrame) game.AssignHotbar(i, items[cursor].id);

            int dy = 0, dx = 0; bool ok = false;
            if (kb != null)
            {
                bool editing = items.Count > 0 && items[cursor].text != null;   // letters go into text fields
                if ((!editing && kb.wKey.wasPressedThisFrame) || kb.upArrowKey.wasPressedThisFrame) dy = -1;
                if ((!editing && kb.sKey.wasPressedThisFrame) || kb.downArrowKey.wasPressedThisFrame || (editing && kb.tabKey.wasPressedThisFrame)) dy = 1;
                if ((!editing && kb.aKey.wasPressedThisFrame) || kb.leftArrowKey.wasPressedThisFrame) dx = -1;
                if ((!editing && kb.dKey.wasPressedThisFrame) || kb.rightArrowKey.wasPressedThisFrame) dx = 1;
                ok |= kb.enterKey.wasPressedThisFrame || (!editing && kb.spaceKey.wasPressedThisFrame);
            }
            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame) dy = -1;
                if (pad.dpad.down.wasPressedThisFrame) dy = 1;
                if (pad.dpad.left.wasPressedThisFrame) dx = -1;
                if (pad.dpad.right.wasPressedThisFrame) dx = 1;
                ok |= pad.buttonSouth.wasPressedThisFrame;
            }
            if (items.Count > 0) cursor = (cursor + dy + items.Count) % items.Count;
            if (dy != 0 || dx != 0) MadMax.Audio.Sfx.Play2D("click", 0.5f);
            else if (ok) MadMax.Audio.Sfx.Play2D("menu", 0.6f);

            // mouse: hover selects, click confirms / adjusts
            var canvas = PixelHud.Canvas;
            if (mouse != null && canvas != null)
            {
                var m = mouse.position.ReadValue();
                var p = new Vector2Int(Mathf.FloorToInt(m.x / Screen.width * canvas.w), Mathf.FloorToInt((1f - m.y / Screen.height) * canvas.h));
                bool moved = (m - lastMouse).sqrMagnitude > 1f;
                lastMouse = m;
                for (int i = 0; i < items.Count; i++)
                    if (items[i].rect.Contains(p) && (moved || mouse.leftButton.wasPressedThisFrame))
                    {
                        cursor = i;
                        if (mouse.leftButton.wasPressedThisFrame) { if (items[i].adjust != null) dx = p.x > items[i].rect.center.x ? 1 : -1; else ok = true; }
                    }
                float wheel = mouse.scroll.ReadValue().y;
                if (Current == Page.Crafting && Mathf.Abs(wheel) > 0.01f && items.Count > 0) cursor = Mathf.Clamp(cursor - (int)Mathf.Sign(wheel), 0, items.Count - 1);
            }

            if (Current == Page.Crafting && dx != 0)
            {
                { int n = Mathf.Max(1, StationCategories().Count); category = (category + dx + n) % n; }
                cursor = 0;
                Rebuild();
                return;
            }
            if (items.Count == 0) return;
            var it = items[cursor];
            if (dx != 0 && it.adjust != null) it.adjust(dx);
            else if (ok && Enabled(it)) it.confirm?.Invoke();
        }

        // ------------------------------------------------------------------ drawing
        public void Draw(PixelCanvas c)
        {
            if (!IsOpen) return;
            switch (Current)
            {
                case Page.Main: DrawMain(c); break;
                case Page.Crafting: DrawCrafting(c); break;
                case Page.Character: DrawList(c, mirror ? "MIRROR" : "OUTFIT", 200); break;
                case Page.NewGame: DrawList(c, hostNew ? "NEW HOSTED GAME" : "NEW GAME", 240); break;
                case Page.Inventory: DrawList(c, "INVENTORY   " + game.CarriedWeight.ToString("0.0") + "/" + game.Stats.CarryCapacity.ToString("0") + " KG" + (game.CarriedWeight > game.Stats.CarryCapacity ? "  OVERLOADED" : ""), 250); DrawHint(c); break;
                case Page.Skills: DrawList(c, "SURVIVOR", 230); DrawHint(c); break;
                case Page.Research: DrawList(c, "RESEARCH", 280); DrawHint(c); break;
                case Page.Health: DrawHealth(c); break;
                case Page.Talk: DrawTalk(c); break;
                case Page.Repair: DrawList(c, station && station.type == "sewing" ? "MEND CLOTHES" : "REPAIR TOOLS", 250); DrawHint(c); break;
                case Page.Salvage: DrawList(c, "SALVAGE", 250); DrawHint(c); break;
                case Page.Armour: DrawList(c, "ARMOUR", 320); DrawHint(c); break;
                case Page.Tuning: DrawTuning(c); DrawHint(c); break;
                case Page.Paint: DrawList(c, "PAINT SHOP - " + (paintTarget ? WastelandGame.Name(paintTarget) : ""), 300); DrawHint(c); break;
                case Page.Board: DrawList(c, "BOUNTY BOARD - " + (boardTarget ? MadMax.Npc.Market.TownName(boardTarget.Settlement) : ""), 380); DrawHint(c); break;
                case Page.Trade:
                    if (talkNpc) DrawList(c, MadMax.Npc.NpcLore.TradeTitle(talkNpc.Profile.kind) + " - " + talkNpc.Profile.Name + "   YOUR SCRAP " + game.Inventory.Get(ResourceType.Scrap), 290);
                    DrawHint(c);
                    break;
                case Page.Container:
                    if (container) DrawList(c, container.title + "  " + container.Weight.ToString("0") + "/" + container.capacity.ToString("0") + " KG" + (container.fridge ? (container.Cooling ? "  COLD" : "  NO POWER") : ""), 240);
                    DrawHint(c);
                    break;
                case Page.Creation:
                    DrawList(c, "CHARACTER CREATION   POINTS LEFT " + PointsLeft(), 250);
                    DrawHint(c);
                    break;
                case Page.Join: DrawList(c, "JOIN GAME", 220); { var net = MadMax.Net.NetSession.Instance; if (net) c.Text((c.w - PixelCanvas.TextWidth(net.Status)) / 2, c.h - 20, net.Status, Amber); } break;
                default: DrawList(c, Current == Page.Pause ? "PAUSED" : "SETTINGS", 180); break;
            }
        }

        void DrawMain(PixelCanvas c)
        {
            if (TitleSequence.Playing)
            {
                // the 3D logo is in the scene: only the menu, low on screen over a dark band
                int bandY = c.h * 56 / 100;
                for (int x = 0; x < c.w * 2 / 5; x++) c.Rect(x, bandY - 6, 1, c.h - bandY + 6, new Color32(8, 4, 3, (byte)(170 * (1f - x / (c.w * 0.4f)))));
                DrawItems(c, 16, bandY, 100, 2);
                c.Text(4, c.h - 8, "W/S SELECT  ENTER CONFIRM", Dim);
                DrawVersion(c);
                return;
            }
            c.Rect(0, 0, c.w, c.h, new Color32(20, 10, 6, 150));
            string title = "MAD MIKE'S GARAGE";
            int tw = PixelCanvas.TextWidth(title, 4);
            c.Text((c.w - tw) / 2, c.h / 5, title, Amber, 4);
            string sub = "BUILD - SCAVENGE - SURVIVE";
            c.Text((c.w - PixelCanvas.TextWidth(sub, 2)) / 2, c.h / 5 + 28, sub, Text, 2);
            DrawItems(c, (c.w - 100) / 2, c.h / 2, 100, 2);
            c.Text(4, c.h - 8, "W/S SELECT  ENTER CONFIRM  MOUSE OK", Dim);
            DrawVersion(c);
        }

        /// <summary>Build stamp (set at build time by the editor, see BuildStamp) so a player can tell which build is running.</summary>
        static void DrawVersion(PixelCanvas c)
        {
            string v = "V" + Application.version + (Debug.isDebugBuild ? " DEV" : "");
            c.Text(c.w - PixelCanvas.TextWidth(v) - 4, c.h - 8, v, new Color32(150, 110, 80, 255));
        }

        void DrawList(PixelCanvas c, string title, int width)
        {
            c.Rect(0, 0, c.w, c.h, new Color32(10, 5, 3, (byte)(Current == Page.NewGame || Current == Page.Creation ? 220 : 120)));
            int rows = Mathf.Min(items.Count, (c.h - 44) / 10);
            listFirst = Mathf.Clamp(listFirst, Mathf.Max(0, cursor - rows + 1), Mathf.Min(cursor, items.Count - rows));
            int h = rows * 10 + 22;
            int x = (c.w - width) / 2, y = Mathf.Max(4, (c.h - h) / 2);
            c.Panel(x, y, width, h);
            c.Text(x + 6, y + 5, title, Amber, 1);
            DrawItems(c, x + 6, y + 16, width - 12, 1, listFirst, rows);
            if (rows < items.Count) c.Text(x + width - 30, y + 5, (cursor + 1) + "/" + items.Count, Dim);
            if (Current == Page.Settings) c.Text(x + 6, y + h + 3, "A/D OR CLICK TO CHANGE   ESC BACK", Dim);
        }

        int listFirst;

        /// <summary>Tuning bench: the settings on the left, the stat card and dyno graph on the right.</summary>
        void DrawTuning(PixelCanvas c)
        {
            var t = tuneTarget;
            if (!t) return;
            c.Rect(0, 0, c.w, c.h, new Color32(10, 5, 3, 120));
            int rows = Mathf.Min(items.Count, (c.h - 44) / 10);
            listFirst = Mathf.Clamp(listFirst, Mathf.Max(0, cursor - rows + 1), Mathf.Min(cursor, items.Count - rows));
            int lw = Mathf.Min(300, c.w - 210), h = rows * 10 + 22;
            int x = 8, y = Mathf.Max(4, (c.h - h) / 2);
            c.Panel(x, y, lw, h);
            c.Text(x + 6, y + 5, "TUNING  " + WastelandGame.Name(t), Amber, 1);
            DrawItems(c, x + 6, y + 16, lw - 12, 1, listFirst, rows);

            int sx = x + lw + 6, sw = c.w - sx - 8, sh = 160;
            c.Panel(sx, y, sw, sh);
            t.Card(out float torque, out float kw, out float top);
            var rb = t.GetComponent<Rigidbody>();
            var drv = t.GetComponent<MadMax.Vehicles.VehicleDriver>();
            float mass = rb ? rb.mass : 1f;
            float grip = 0f; int n = 0;
            foreach (var ws in t.GetComponentsInChildren<MadMax.Vehicles.WheelStats>()) { grip += ws.grip; n++; }
            grip = n > 0 ? grip / n * t.GripFactor(0f) : 0f;
            float decel = Mathf.Min(drv ? drv.brakeForce / mass : 0f, grip * 9.81f);
            int ty = y + 6;
            void Row(string k, string v) { c.Text(sx + 6, ty, k, Dim); c.Text(sx + sw - 6 - PixelCanvas.TextWidth(v), ty, v, Text); ty += 9; }
            Row("POWER", Mathf.RoundToInt(kw) + " KW  " + Mathf.RoundToInt(kw * 1.341f) + " HP");
            Row("TORQUE", Mathf.RoundToInt(torque) + " NM");
            Row("TOP SPEED", Mathf.RoundToInt(top) + " KM/H");
            Row("WEIGHT", Mathf.RoundToInt(mass) + " KG");
            Row("BRAKING", decel.ToString("0.0") + " M/S2");
            Row("TYRE GRIP", grip.ToString("0.00") + (t.NitrousOn ? "  NOS!" : ""));
            // dyno: torque (amber) and power (green) over the rev range
            var e = t.GetComponentInChildren<MadMax.Vehicles.EngineStats>();
            int gx = sx + 8, gy = ty + 4, gw = sw - 16, gh = y + sh - gy - 12;
            c.Rect(gx, gy, gw, gh, new Color32(10, 6, 4, 220));
            if (e && gh > 10 && torque > 0f)
            {
                int px = -1, pt = 0, pp = 0;
                for (int i = 0; i <= gw; i++)
                {
                    float frac = Mathf.Max(0.05f, (float)i / gw), rpm = e.maxRpm * frac;
                    float tq = e.TorqueAt(rpm) * t.TorqueFactor(frac), p = tq * rpm * 2f * Mathf.PI / 60000f;
                    int yt = gy + gh - 1 - Mathf.RoundToInt(tq / (torque * 1.1f) * (gh - 2)), yp = gy + gh - 1 - Mathf.RoundToInt(p / (kw * 1.1f) * (gh - 2));
                    if (px >= 0) { c.Line(gx + px, pt, gx + i, yt, Amber); c.Line(gx + px, pp, gx + i, yp, Green); }
                    px = i; pt = yt; pp = yp;
                }
                c.Text(gx + 2, gy + gh + 3, "0", Dim);
                string rmax = Mathf.RoundToInt(e.maxRpm) + " RPM";
                c.Text(gx + gw - PixelCanvas.TextWidth(rmax), gy + gh + 3, rmax, Dim);
                c.Text(gx + 2, gy + 2, "TORQUE", Amber); c.Text(gx + 34, gy + 2, "POWER", Green);
            }
        }

        void DrawItems(PixelCanvas c, int x, int y, int w, int scale, int first = 0, int rows = int.MaxValue)
        {
            int lh = 7 * scale + 3;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (i < first || i >= first + rows) { it.rect = new RectInt(-100, -100, 0, 0); continue; }
                int ly = y + (i - first) * lh;
                it.rect = new RectInt(x - 2, ly - 2, w + 4, lh);
                bool sel = i == cursor;
                if (sel) c.Rect(x - 2, ly - 2, w + 4, lh, Hi);
                var col = !Enabled(it) ? Dim : sel ? Amber : Text;
                c.Text(x, ly, it.label, col, scale);
                if (it.text != null)
                {
                    string v = it.text() + (i == cursor && (Time.unscaledTime * 2f) % 2f > 1f ? "_" : " ");
                    c.Rect(x + w / 2 - 2, ly - 1, w / 2 + 2, 7 * scale + 1, new Color32(10, 6, 4, 200));
                    c.Text(x + w / 2, ly, v, sel ? Amber : Text, scale);
                }
                if (it.value != null)
                {
                    string v = "< " + it.value() + " >";
                    c.Text(x + w - PixelCanvas.TextWidth(v, scale), ly, v, sel ? Amber : Text, scale);
                }
            }
        }

        static readonly Color32 SkinC = new Color32(170, 130, 100, 255), Hurt1 = new Color32(230, 190, 60, 255), Hurt2 = new Color32(230, 110, 40, 255), Hurt3 = new Color32(210, 40, 30, 255);

        /// <summary>Body chart (front view) coloured by the worst wound per zone, temperatures, and the wound list.</summary>
        void DrawHealth(PixelCanvas c)
        {
            c.Rect(0, 0, c.w, c.h, new Color32(10, 5, 3, 200));
            int w = 300, h = 190, x = (c.w - w) / 2, y = (c.h - h) / 2;
            c.Panel(x, y, w, h);
            var st = game.Stats;
            c.Text(x + 6, y + 5, "HEALTH  " + Mathf.CeilToInt(st.health) + "/" + Mathf.CeilToInt(st.MaxHealth), Amber);
            // body chart
            int bx = x + 30, by = y + 22;
            Color32 Zone(MadMax.RPG.BodyZone z)
            {
                float worst = 0f; bool bleed = false;
                foreach (var i in st.injuries) if (i.zone == z) { worst = Mathf.Max(worst, i.severity * (i.type == MadMax.RPG.Wound.Fracture || i.type == MadMax.RPG.Wound.DeepWound ? 1f : 0.6f)); bleed |= i.Bleeding; }
                if (bleed && (Time.unscaledTime * 3f) % 1f > 0.5f) return Hurt3;
                return worst <= 0f ? SkinC : worst < 0.35f ? Hurt1 : worst < 0.7f ? Hurt2 : Hurt3;
            }
            c.Rect(bx + 13, by, 10, 11, Zone(MadMax.RPG.BodyZone.Head));
            c.Rect(bx + 10, by + 12, 16, 26, Zone(MadMax.RPG.BodyZone.Torso));
            c.Rect(bx + 3, by + 12, 6, 20, Zone(MadMax.RPG.BodyZone.ArmR));      // figure faces the viewer: its right is on the left
            c.Rect(bx + 27, by + 12, 6, 20, Zone(MadMax.RPG.BodyZone.ArmL));
            c.Rect(bx + 3, by + 33, 6, 6, Zone(MadMax.RPG.BodyZone.HandR));
            c.Rect(bx + 27, by + 33, 6, 6, Zone(MadMax.RPG.BodyZone.HandL));
            c.Rect(bx + 10, by + 39, 7, 30, Zone(MadMax.RPG.BodyZone.LegR));
            c.Rect(bx + 19, by + 39, 7, 30, Zone(MadMax.RPG.BodyZone.LegL));
            c.Rect(bx + 8, by + 70, 9, 5, Zone(MadMax.RPG.BodyZone.FootR));
            c.Rect(bx + 19, by + 70, 9, 5, Zone(MadMax.RPG.BodyZone.FootL));
            int ty = by + 82;
            var tempCol = st.bodyTemp < 35.5f ? new Color32(120, 170, 255, 255) : st.bodyTemp > 38.5f ? Hurt3 : Green;
            c.Text(x + 6, ty, "CORE " + st.bodyTemp.ToString("0.0") + "C", tempCol);
            c.Text(x + 6, ty + 8, "FEELS " + Mathf.RoundToInt(game.FeltTemperature) + "C" + (game.Sheltered ? " INDOORS" : ""), Dim);
            var (warm, cool) = game.Insulation();
            c.Text(x + 6, ty + 16, "CLOTHES +" + warm.ToString("0") + " WARM " + (cool >= 0 ? "+" : "") + cool.ToString("0") + " COOL", Dim);
            string cond = st.bodyTemp < 33f ? "SEVERE HYPOTHERMIA" : st.bodyTemp < 35f ? "HYPOTHERMIA" : st.bodyTemp < 36f ? "COLD" : st.bodyTemp > 40.5f ? "SEVERE HEATSTROKE" : st.bodyTemp > 39f ? "HEATSTROKE" : st.bodyTemp > 38f ? "HOT" : "OK";
            c.Text(x + 6, ty + 24, cond, tempCol);
            if (st.sick > 0f) c.Text(x + 6, ty + 32, "SICK", new Color32(160, 220, 80, 255));
            // wound list
            DrawItems(c, x + 100, y + 22, w - 106, 1);
            c.Text(x + 100, y + h - 10, "ENTER TREAT   O / ESC CLOSE", Dim);
            DrawHint(c);
        }

        /// <summary>Conversation box at the bottom: who, how they feel about you, what they say, your replies.</summary>
        void DrawTalk(PixelCanvas c)
        {
            if (talk == null || !talkNpc) return;
            int w = Mathf.Min(c.w - 16, 340);
            var lines = Wrap(talk.line, w - 12);
            int rows = Mathf.Min(items.Count, 8);
            int h = 22 + lines.Count * 8 + 6 + rows * 10;
            int x = (c.w - w) / 2, y = c.h - h - 14;
            c.Panel(x, y, w, h);
            var p = talkNpc.Profile;
            int disp = talkNpc.State.disposition;
            string mood = talkNpc.Hostile ? "HOSTILE" : disp < -40 ? "HATES YOU" : disp < -10 ? "WARY" : disp < 20 ? "NEUTRAL" : disp < 50 ? "FRIENDLY" : "TRUSTS YOU";
            var moodCol = disp < -10 || talkNpc.Hostile ? Red : disp >= 20 ? Green : Dim;
            c.Text(x + 6, y + 5, p.Name + "  " + p.Title, Amber);
            c.Text(x + w - 6 - PixelCanvas.TextWidth(mood), y + 5, mood, moodCol);
            int ly = y + 16;
            foreach (var l in lines) { c.Text(x + 6, ly, l, Text); ly += 8; }
            listFirst = Mathf.Clamp(listFirst, Mathf.Max(0, cursor - rows + 1), Mathf.Min(cursor, items.Count - rows));
            DrawItems(c, x + 6, ly + 6, w - 12, 1, listFirst, rows);
            c.Text(x + 6, y + h + 2, "CHA " + game.Stats.Attribute(MadMax.RPG.Attr.Charisma) + "  SPEECH " + game.Stats.Level(MadMax.RPG.Skill.Speech), Dim);
            DrawHint(c);
        }

        static List<string> Wrap(string s, int width)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(s)) return result;
            string cur = "";
            foreach (var word in s.Split(' '))
            {
                string next = cur.Length == 0 ? word : cur + " " + word;
                if (PixelCanvas.TextWidth(next) > width && cur.Length > 0) { result.Add(cur); cur = word; }
                else cur = next;
            }
            if (cur.Length > 0) result.Add(cur);
            return result;
        }

        void DrawHint(PixelCanvas c)
        {
            if (items.Count > 0 && items[cursor].hint != null) c.Text((c.w - PixelCanvas.TextWidth(items[cursor].hint)) / 2, c.h - 12, items[cursor].hint, Amber);
        }

        void DrawCrafting(PixelCanvas c)
        {
            c.Text(6, c.h - 10, (station && station.type == "sewing" ? "T MEND CLOTHES" : "R RESEARCH   T REPAIR TOOLS") + "   Y SALVAGE   X CANCEL LAST", Dim);
            int w = Mathf.Min(c.w - 12, 300), h = Mathf.Min(c.h - 20, 170);
            int x = (c.w - w) / 2, y = (c.h - h) / 2;
            c.Panel(x, y, w, h);
            c.Text(x + 6, y + 5, station ? station.title : "WORKBENCH", Amber);
            // category tabs
            int tx = x + 6;
            var cats = StationCategories();
            for (int i = 0; i < cats.Count; i++)
            {
                string name = cats[i].ToString().ToUpperInvariant();
                if (name == "ATTACHMENTS") name = "PARTS";
                int tw = PixelCanvas.TextWidth(name) + 6;
                if (i == category) c.Rect(tx - 1, y + 13, tw, 9, Hi);
                c.Text(tx + 2, y + 15, name, i == category ? Amber : Dim);
                tx += tw + 2;
            }
            // recipe list
            int listW = w / 2 - 8, lx = x + 6, ly = y + 27, lh = 9;
            int visible = (h - 40) / lh;
            if (cursor < scroll) scroll = cursor;
            if (cursor >= scroll + visible) scroll = cursor - visible + 1;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (i < scroll || i >= scroll + visible) { it.rect = new RectInt(-99, -99, 0, 0); continue; }
                int iy = ly + (i - scroll) * lh;
                it.rect = new RectInt(lx - 2, iy - 2, listW + 4, lh);
                bool sel = i == cursor;
                if (sel) c.Rect(lx - 2, iy - 2, listW + 4, lh, Hi);
                c.Text(lx, iy, it.label, Enabled(it) ? (sel ? Amber : Text) : Dim);
            }
            // detail panel
            int dx = x + w / 2 + 2, dy = y + 27;
            if (items.Count > 0)
            {
                var r = items[cursor].recipe;
                c.Text(dx, dy, r.name, Amber);
                c.Text(dx, dy + 9, r.description ?? "", Dim);
                int ry = dy + 22;
                c.Text(dx, ry, "NEEDS", Text); ry += 8;
                foreach (var (t, n) in r.resources)
                {
                    if (t == ResourceType.None) continue;
                    int have = PoolRes(t);
                    c.Rect(dx, ry + 1, 3, 3, ResourceInfo.Color(t));
                    c.Text(dx + 6, ry, $"{ResourceInfo.Name(t)} {n}  ({have})", have >= n ? Green : Red);
                    ry += 8;
                }
                foreach (var (i2, n) in r.items)
                {
                    int have = PoolItem(i2);
                    c.Text(dx + 6, ry, $"{ItemIds.Name(i2)} {n}  ({have})", have >= n ? Green : Red);
                    ry += 8;
                }
                if (r.fuel != ResourceType.None) { c.Text(dx + 6, ry, $"FUEL: {ResourceInfo.Name(r.fuel)} {r.fuelAmount}", Dim); ry += 8; }
                if (r.byproducts != null) foreach (var (bt, bn) in r.byproducts) { c.Text(dx + 6, ry, $"+ {ResourceInfo.Name(bt)} {bn}", Dim); ry += 8; }
                ry += 4;
                string owned = r.kind == OutputKind.Item ? $"OWNED {game.Inventory.GetItem(r.output)}" : r.kind == OutputKind.Resource ? $"HAVE {game.Inventory.Get(r.outputResource)} L" : "SPAWNS ON THE BENCH";
                c.Text(dx, ry, owned, Dim);
                bool can = game.CanCraft(r, station);
                c.Text(dx, y + h - 10, can ? "[ENTER] QUEUE (" + Mathf.CeilToInt(RecipeLibrary.Seconds(r)) + " S)" : "MISSING MATERIALS", can ? Green : Red);
            }
            else c.Text(dx, dy, "NOTHING HERE", Dim);
            c.Text(x + 6, y + h - 10, "A/D TAB  W/S SELECT  ESC CLOSE", Dim);
            // the station's queue, under the panel
            if (station && station.queue.Count > 0)
            {
                int qy = y + h + 3, qx = x;
                for (int i = 0; i < station.queue.Count && qx < x + w - 40; i++)
                {
                    var job = station.queue[i];
                    var jr = RecipeLibrary.Get(job.recipe);
                    string label = (jr != null ? jr.name : "?") + (i == 0 ? " " + Mathf.RoundToInt(job.progress * 100f) + "%" : "");
                    int tw = PixelCanvas.TextWidth(label) + 6;
                    c.Panel(qx, qy, tw, 11);
                    if (i == 0) c.Rect(qx + 1, qy + 9, Mathf.RoundToInt((tw - 2) * Mathf.Clamp01(job.progress)), 1, Green);
                    c.Text(qx + 3, qy + 2, label, i == 0 ? Amber : Dim);
                    qx += tw + 2;
                }
            }
        }
    }
}
