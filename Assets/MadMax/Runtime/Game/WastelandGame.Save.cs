using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MadMax.Game
{
    /// <summary>Save / load / new game. Loading reloads the scene and rebuilds the world from the save
    /// (vehicles with their parts and fluids, loose parts, built pieces, destroyed props, inventory, player).
    /// Not saved: terrain ruts, dents, crate positions.</summary>
    public partial class WastelandGame
    {
        public void NewGame()
        {
            if (!played) { played = true; ScreenFader.FadeThrough(() => Menus.Close(), 0.3f); return; }     // fresh world already running behind the menu
            SaveSystem.SkipMenu = true;
            Reload();
        }

        /// <summary>Start a configured game: rules, created character and look carry across the scene reload.</summary>
        public void StartNewGame(GameRules rules, MadMax.RPG.CharacterStats character, Appearance look, bool host)
        {
            SaveSystem.PendingRules = rules.Clone();
            SaveSystem.PendingCharacter = character;
            SaveSystem.PendingLook = look.Clone();
            SaveSystem.PendingHost = host;
            SaveSystem.SkipMenu = true;
            Reload();
        }

        public void ReturnToMainMenu() => Reload();

        /// <summary>Load the most recent save (CONTINUE).</summary>
        public void LoadGame() { int latest = SaveSystem.Latest(); if (latest < 0) { Toast("NO SAVE FOUND"); return; } LoadGame(latest); }

        public void LoadGame(int slot)
        {
            var data = SaveSystem.Read(slot);
            if (data == null) { Toast("NO SAVE FOUND"); return; }
            SaveSystem.Slot = slot == 0 ? SaveSystem.Slot : slot;                       // an autosave keeps saving to the slot it came from
            SaveSystem.Pending = data;
            Reload();
        }

        static void Reload()
        {
            Time.timeScale = 1f;
            if (TitleSequence.Instance) TitleSequence.Instance.Finish();
            ScreenFader.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void SaveGame() => SaveGame(SaveSystem.Slot);

        public void SaveGame(int slot)
        {
            if (MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient) { Toast("ONLY THE HOST CAN SAVE"); return; }
            SaveSystem.Write(CaptureSave(), slot, SaveSummary());
            if (slot > 0) SaveSystem.Slot = slot;
            autosaveAt = Time.unscaledTime + Mathf.Max(1, GameSettings.Current.autosaveMinutes) * 60f;
            Toast(slot == 0 ? "AUTOSAVED" : "GAME SAVED (SLOT " + slot + ")");
        }

        /// <summary>Slot list line: day, place, what you are in.</summary>
        string SaveSummary()
        {
            var at = Current ? Current.transform.position : Player ? Player.transform.position : Vector3.zero;
            var st = World != null ? World.SettlementAt(at.x, at.z) : null;
            string where = st != null ? MadMax.Npc.Market.TownName(st) : World != null ? World.BiomeAt(at.x, at.z).ToString().ToUpperInvariant() : "";
            return "DAY " + (DayNight.Day + 1) + "  " + where + (Current ? "  " + Name(Current) : "");
        }

        float autosaveAt = -1f;

        /// <summary>Autosave on the clock (setting), after sleeping and on quit. Host / single player only.</summary>
        public void Autosave(string why = null)
        {
            if (!played || (MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient) || (Vitals && Vitals.Dead)) return;
            try { SaveGame(0); }
            catch (System.Exception e) { Debug.LogWarning("[MadMax] Autosave failed: " + e.Message); }
        }

        void UpdateAutosave()
        {
            int minutes = GameSettings.Current.autosaveMinutes;
            if (minutes <= 0 || !played || (Menus && Menus.IsOpen)) return;
            if (autosaveAt < 0f) { autosaveAt = Time.unscaledTime + minutes * 60f; return; }
            if (Time.unscaledTime >= autosaveAt) Autosave();
        }

        void OnApplicationQuit() { if (played && GameSettings.Current.autosaveMinutes > 0) Autosave(); }

        /// <summary>Snapshot of the world (also the join payload for network clients).</summary>
        public SaveData CaptureSave(bool forNetwork = false)
        {
            var d = new SaveData { seed = seed, raining = Weather.Raining, wetness = Weather.Wetness, snow = Weather.Snow, temperature = Weather.BaseTemperature, lakeRise = Weather.LakeRise, hours = DayNight.Hours, day = DayNight.Day, searched = new List<string>(Lootable.Searched), hasSpawn = spawnPoint.HasValue, spawn = spawnPoint ?? Vector3.zero, resources = Inventory.ResourceArray, rules = Rules, stats = Stats };
            if (cameraRig) d.cameraMode = (int)cameraRig.mode;
            foreach (var kv in Inventory.Items) if (kv.Value > 0) d.items.Add(new ItemSave { id = kv.Key, count = kv.Value });
            d.hotbar = (string[])Hotbar.Clone();
            d.tankAir = TankAir;
            d.boatsFound = new List<string>(BoatsFound);
            foreach (var kv in GasPump.Used) { d.pumpKeys.Add(kv.Key); d.pumpUsed.Add(kv.Value); }
            d.terrain = terrain.SaveEdits(FocusPos);
            d.npcs = MadMax.Npc.NpcRegistry.SaveAll();
            d.reputation = MadMax.Npc.NpcRegistry.Reputation;
            SaveTools(d);
            SaveClothes(d);
            SaveCrafting(d);
            SaveFishing(d);
            d.market = MadMax.Npc.Market.Save();
            d.raidNext = MadMax.Npc.BaseRaid.NextDay; d.raidReport = MadMax.Npc.BaseRaid.Report;
            d.plans = StructurePlans.Save();
            MadMax.Npc.Contracts.Save(d);
            MadMax.Npc.Companions.Save(this, d);
            MadMax.Npc.TownQuests.Save(d);
            d.factions = MadMax.Npc.Factions.Save();
            MadMax.Animals.AnimalDirector.Instance?.Save(d);
            d.foundAircraft = new List<string>(FoundAircraft); d.scouted = new List<string>(Scouted);
            SaveMap(d);
            SaveStashes(d);
            DestructibleVoxels.CaptureMoved();
            foreach (var kv in DestructibleVoxels.Moved) d.moved.Add(new MovedSave { key = kv.Key, position = kv.Value.p, rotation = kv.Value.r });
            if (MadMax.World.Storms.Instance) MadMax.World.Storms.Instance.SaveState(out d.storm, out d.stormLeft, out d.stormFor);
            foreach (var f in MadMax.World.Fire.All)
                if (f && f.fuel > 1f && !f.GetComponentInParent<VehicleDriver>())                  // vehicle fires come from their heat
                    d.fires.Add(new FireSave { position = f.transform.position, fuel = f.fuel, intensity = f.intensity, ground = f.ground });
            if (MadMax.Npc.NpcDirector.Instance) d.convoys = MadMax.Npc.NpcDirector.Instance.SaveConvoys();

            vehicles.RemoveAll(v => !v);
            var saved = vehicles.FindAll(v => !v.aiDriven);            // NPC-driven vehicles come back with their convoy
            foreach (var v in saved)
            {
                var vs = new VehicleSave
                {
                    design = v.GetComponent<VehicleChassis>().vehicleName, position = v.transform.position, rotation = v.transform.rotation,
                    fleet = fleet.Contains(v), wreck = wrecks.Contains(v), fourWheel = v.FourWheelDrive, diffLocked = v.diffLocked, radio = v.TryGetComponent<MadMax.Audio.RadioReceiver>(out var rr) ? rr.SaveState() : null,
                    sub = v.TryGetComponent<Submarine>(out var subm) ? subm.SaveState() : null,
                    netId = v.netId, owner = v.owner,
                    cargo = v.TryGetComponent<Container>(out var cg) ? cg.SaveState() : null
                };
                foreach (var s in v.GetComponent<VehicleChassis>().Sockets)
                    vs.sockets.Add(new SocketSave { socket = s.name, part = s.Current ? s.Current.partId : null, state = s.Current ? s.Current.SaveState() : null, q = s.Current ? s.Current.quality + 1 : 0, damage = s.Current ? s.Current.damage : 0f, wear = s.Current && s.Current.TryGetComponent<WheelStats>(out var ws) ? ws.wear : 0f });
                if (v.TryGetComponent<VehicleSystems>(out var sys)) { vs.fuel = sys.fuel; vs.oil = sys.oil; vs.coolant = sys.coolant; vs.additive = sys.additive; vs.tank = (int)sys.tankKind; vs.disconnected = sys.disconnected; vs.fluids = sys.FluidState(); }
                if (v.TryGetComponent<MadMax.Story.StoryTag>(out var stag)) vs.storyTag = stag.key;
                if (v.TryGetComponent<VehicleDamage>(out var dmg)) { vs.frame = dmg.FrameDamage; vs.salvage = dmg.salvagePool; }
                if (v.TryGetComponent<VehicleArmor>(out var arm)) vs.armor = arm.SaveState();
                if (v.TryGetComponent<VehicleTuning>(out var tun)) vs.tuning = tun.SaveState();
                if (v.TryGetComponent<VehiclePaint>(out var vp)) vs.paint = vp.SaveState();
                if (v.TryGetComponent<VehicleSystems>(out var mt)) vs.service = mt.MaintenanceState();
                if (v.TryGetComponent<VehicleBreakables>(out var wr)) vs.wear = wr.SaveState();
                foreach (var dm in v.GetComponentsInChildren<DeformableMesh>())
                {
                    var st = dm.SaveState();
                    if (st != null) vs.dents.Add(RelativePath(v.transform, dm.transform) + "\u001f" + st);
                }
                var tc = v.GetComponent<TowCoupling>();
                if (tc && tc.Tower) vs.towedBy = saved.IndexOf(tc.Tower);
                d.vehicles.Add(vs);
            }
            foreach (var part in VehiclePart.Registry)
                if (part && !part.Socket && part != Player.Carried)
                    d.loose.Add(new LooseSave { part = part.partId, state = part.SaveState(), q = part.quality + 1, position = part.transform.position, rotation = part.transform.rotation, damage = part.damage, netId = part.netId });
            foreach (var p in FindObjectsByType<Placeable>(FindObjectsSortMode.None))
            {
                var chassis = p.GetComponentInParent<VehicleChassis>();
                int vi = chassis ? saved.IndexOf(chassis.GetComponent<VehicleDriver>()) : -1;
                var parent = vi >= 0 ? saved[vi].transform : Build.Structures;
                var wp = p.transform.position; var wr = p.transform.rotation;
                if (p.TryGetComponent<Door>(out var door)) door.ClosedWorld(out wp, out wr);
                d.placed.Add(new PlacedSave
                {
                    id = p.id, vehicle = vi, hits = p.hits, netId = p.Id, state = p.SaveState(), owner = p.owner,
                    localPosition = parent.InverseTransformPoint(wp),
                    localRotation = Quaternion.Inverse(parent.rotation) * wr
                });
            }
            foreach (var kv in terrain.DestructionState)
            {
                if (!terrain.DestructionTemplates.TryGetValue(kv.Key, out var tid)) continue;
                var template = PropLibrary.TemplateGrid(tid);
                if (template == null) continue;
                var ds = new DestroyedSave { key = kv.Key, template = tid, all = kv.Value.Count == 0 };
                if (!ds.all) foreach (var pos in template.voxels.Keys) if (!kv.Value.Has(pos)) ds.removed.Add(pos);
                d.destroyed.Add(ds);
            }

            var ps = d.player;
            ps.vehicle = Current ? saved.IndexOf(Current) : -1;
            ps.interior = Player.Interior ? saved.IndexOf(Player.Interior.GetComponent<VehicleDriver>()) : -1;
            ps.position = Player.transform.position;
            ps.localPosition = Player.transform.localPosition;
            ps.yaw = Player.transform.eulerAngles.y;
            ps.tool = Player.Tool ? Player.Tool.id : null;
            ps.appearance = Player.Rig.appearance.Clone();
            ps.outfit = new List<string>(Player.Rig.outfit);
            if (Player.Carried) { ps.carried = Player.Carried.partId; ps.carriedDamage = Player.Carried.damage; }
            return d;
        }

        /// <summary>"a/b/c" from <paramref name="root"/> down to <paramref name="t"/> ("" for the root itself).</summary>
        static string RelativePath(Transform root, Transform t)
        {
            var sb = new System.Text.StringBuilder();
            for (; t && t != root; t = t.parent) sb.Insert(0, sb.Length > 0 ? t.name + "/" : t.name);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ restore (called from Start)
        void RestoreDestruction(SaveData d)
        {
            DestructibleVoxels.Moved.Clear();
            if (d.moved != null) foreach (var m in d.moved) DestructibleVoxels.Moved[m.key] = (m.position, m.rotation);
            foreach (var ds in d.destroyed)
            {
                var template = PropLibrary.TemplateGrid(ds.template);
                if (template == null) continue;
                var g = template.Clone();
                if (ds.all) g.voxels.Clear();
                else foreach (var p in ds.removed) g.voxels.Remove(p);
                terrain.DestructionState[ds.key] = g;
                terrain.DestructionTemplates[ds.key] = ds.template;
            }
        }

        GameObject PrefabFor(string design)
        {
            foreach (var p in vehiclePrefabs) if (p.name == design) return p;
            foreach (var p in trailerPrefabs) if (p.name == design) return p;
            if (boatPrefabs != null) foreach (var p in boatPrefabs) if (p && p.name == design) return p;
            return null;
        }

        public VehiclePart SpawnPart(string id, Vector3 pos, Quaternion rot)
        {
            if (!partLookup.TryGetValue(id ?? "", out var prefab)) return null;
            var go = Instantiate(prefab, pos, rot);
            go.name = id;
            return go.GetComponent<VehiclePart>();
        }

        void RestoreVehicles(SaveData d)
        {
            var spawned = new List<VehicleDriver>();
            foreach (var vs in d.vehicles)
            {
                var prefab = PrefabFor(vs.design);
                if (!prefab) { spawned.Add(null); continue; }
                var go = Instantiate(prefab, vs.position, vs.rotation);
                if (vs.wreck) go.name = "Wreck " + prefab.name;
                go.GetComponent<VehicleDriver>().netId = vs.netId;
                go.GetComponent<VehicleDriver>().owner = vs.owner;
                if (go.TryGetComponent<InteriorSpace>(out var interior)) interior.furnish = false;   // pieces come from the save
                var v = go.GetComponent<VehicleDriver>();
                Register(v, vs.fleet ? fleet : vs.wreck ? wrecks : null);
                spawned.Add(v);
                var chassis = go.GetComponent<VehicleChassis>();
                foreach (var s in chassis.Sockets)
                {
                    var saved = vs.sockets.Find(x => x.socket == s.name);
                    if (saved == null) continue;
                    if (s.Current && s.Current.partId == saved.part) { s.Current.damage = saved.damage; s.Current.LoadState(saved.state); if (saved.q > 0) s.Current.quality = saved.q - 1; if (s.Current.TryGetComponent<WheelStats>(out var ws0)) ws0.wear = saved.wear; continue; }
                    var old = s.Detach(false);
                    if (old) Destroy(old.gameObject);
                    if (string.IsNullOrEmpty(saved.part)) continue;
                    var part = SpawnPart(saved.part, s.transform.position, s.transform.rotation);
                    if (part) { s.Attach(part); part.damage = saved.damage; part.LoadState(saved.state); if (saved.q > 0) part.quality = saved.q - 1; if (part.TryGetComponent<WheelStats>(out var ws1)) ws1.wear = saved.wear; }
                }
                if (go.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = vs.fuel; sys.oil = vs.oil; sys.coolant = vs.coolant; sys.additive = vs.additive; sys.tankKind = (ResourceType)vs.tank; sys.disconnected = vs.disconnected; sys.LoadFluidState(vs.fluids); }
                if (!string.IsNullOrEmpty(vs.storyTag)) MadMax.Story.StoryTag.Set(go, vs.storyTag);
                if (!string.IsNullOrEmpty(vs.cargo) && go.TryGetComponent<Container>(out var cargo)) cargo.LoadState(vs.cargo);
                if (go.TryGetComponent<VehicleDamage>(out var dmg)) { dmg.AddFrameDamage(vs.frame, 1f); dmg.salvagePool = vs.salvage; }
                if (!string.IsNullOrEmpty(vs.armor) && go.TryGetComponent<VehicleArmor>(out var arm)) arm.LoadState(vs.armor);
                if (!string.IsNullOrEmpty(vs.sub) && go.TryGetComponent<Submarine>(out var sub)) sub.LoadState(vs.sub);
                if (!string.IsNullOrEmpty(vs.tuning) && go.TryGetComponent<VehicleTuning>(out var tun)) tun.LoadState(vs.tuning);
                if (!string.IsNullOrEmpty(vs.paint)) VehiclePaint.Of(v).LoadState(vs.paint);
                if (!string.IsNullOrEmpty(vs.service) && go.TryGetComponent<VehicleSystems>(out var mt)) mt.LoadMaintenance(vs.service);
                if (vs.dents != null)
                    foreach (var dent in vs.dents)
                    {
                        int cut = dent.IndexOf('\u001f');
                        var t = cut > 0 ? go.transform.Find(dent.Substring(0, cut)) : cut == 0 ? go.transform : null;
                        if (!t || !t.GetComponent<MeshFilter>()) continue;
                        if (!t.TryGetComponent<DeformableMesh>(out var dm)) dm = t.gameObject.AddComponent<DeformableMesh>();
                        dm.LoadState(dent.Substring(cut + 1));
                    }
                if (!string.IsNullOrEmpty(vs.wear) && go.TryGetComponent<VehicleBreakables>(out var wear)) wear.LoadState(vs.wear);
                if (vs.fourWheel != v.FourWheelDrive) v.ToggleFourWheelDrive();
                v.diffLocked = vs.diffLocked;
                if (!string.IsNullOrEmpty(vs.radio)) MadMax.Audio.RadioReceiver.On(v.gameObject).LoadState(vs.radio);
                v.Body.isKinematic = true;       // woken by the sleeper near the player
            }
            for (int i = 0; i < d.vehicles.Count; i++)
                if (d.vehicles[i].towedBy >= 0 && spawned[i] && spawned[d.vehicles[i].towedBy])
                    spawned[i].GetComponent<TowCoupling>()?.Couple(spawned[d.vehicles[i].towedBy], true);
            foreach (var l in d.loose)
            {
                var part = SpawnPart(l.part, l.position, l.rotation);
                if (!part) continue;
                part.damage = l.damage;
                part.LoadState(l.state);
                if (l.q > 0) part.quality = l.q - 1;
                part.netId = l.netId;
                var rb = part.gameObject.AddComponent<Rigidbody>();
                rb.mass = part.mass; rb.isKinematic = true;
            }
            foreach (var p in d.placed)
            {
                Transform parent = p.vehicle >= 0 && p.vehicle < spawned.Count && spawned[p.vehicle] ? spawned[p.vehicle].transform : Build.Structures;
                var placed = FurnitureLibrary.Spawn(p.id, parent, p.localPosition, p.localRotation, propMaterial);
                if (placed) { placed.hits = p.hits; placed.netId = p.netId; placed.owner = p.owner; placed.LoadState(p.state); }
            }
            restoredVehicles = spawned;
        }

        List<VehicleDriver> restoredVehicles;

        void RestorePlayer(SaveData d)
        {
            Inventory.Restore(d.resources, d.items.ConvertAll(i => new KeyValuePair<string, int>(i.id, i.count)));
            if (d.tankAir >= 0f) TankAir = d.tankAir;
            BoatsFound.Clear(); if (d.boatsFound != null) foreach (var k in d.boatsFound) BoatsFound.Add(k);
            if (d.hotbar != null && d.hotbar.Length == HotbarSize) { Hotbar = d.hotbar; for (int i = 0; i < HotbarSize; i++) if (Hotbar[i] == "") Hotbar[i] = null; }
            SyncHotbar();
            terrain.LoadEdits(d.terrain);
            Weather.Restore(d.raining, d.wetness, d.snow, d.temperature, d.lakeRise);
            if (MadMax.World.Storms.Instance) MadMax.World.Storms.Instance.Restore(d.storm, d.stormLeft, d.stormFor);
            if (d.fires != null) foreach (var f in d.fires) MadMax.World.Fire.Ignite(f.position, null, f.fuel, f.intensity, f.ground);
            if (d.hours >= 0f) DayNight.SetHours(d.hours);
            DayNight.SetDay(d.day);
            MadMax.Npc.NpcRegistry.Load(d.npcs, d.reputation);
            RestoreTools(d);
            RestoreClothes(d);
            RestoreCrafting(d);
            RestoreFishing(d);
            MadMax.Npc.Market.Load(d.market);
            MadMax.Npc.BaseRaid.NextDay = d.raidNext; MadMax.Npc.BaseRaid.Report = d.raidReport;
            StructurePlans.Load(d.plans);
            MadMax.Npc.Contracts.Load(d);
            MadMax.Npc.Companions.Load(d);
            MadMax.Npc.TownQuests.Load(d);
            MadMax.Npc.Factions.Load(d.factions);
            MadMax.Animals.AnimalDirector.Instance?.Load(d);
            if (d.foundAircraft != null) foreach (var k in d.foundAircraft) FoundAircraft.Add(k);
            if (d.scouted != null) foreach (var k in d.scouted) Scouted.Add(k);
            LoadMap(d);
            LoadStashes(d);
            if (MadMax.Npc.NpcDirector.Instance) MadMax.Npc.NpcDirector.Instance.LoadConvoys(d.convoys);
            if (d.searched != null) foreach (var k in d.searched) Lootable.Searched.Add(k);
            if (d.hasSpawn) spawnPoint = d.spawn;
            if (d.pumpKeys != null) for (int i = 0; i < d.pumpKeys.Count && i < d.pumpUsed.Count; i++) GasPump.Used[d.pumpKeys[i]] = d.pumpUsed[i];
            if (cameraRig) cameraRig.mode = (ViewMode)d.cameraMode;
            var ps = d.player;
            if (ps.appearance != null)
            {
                Player.Rig.appearance = ps.appearance.Clone();
                Player.Rig.outfit.Clear();
                Player.Rig.outfit.AddRange(ps.outfit);
                Player.RebuildBody();
            }
            if (!string.IsNullOrEmpty(ps.tool)) Player.Equip(ToolLibrary.Create(ps.tool, propMaterial));
            VehicleDriver At(int i) => restoredVehicles != null && i >= 0 && i < restoredVehicles.Count ? restoredVehicles[i] : null;
            var car = At(ps.vehicle);
            var home = At(ps.interior);
            if (car) { car.Body.isKinematic = false; Enter(car); }
            else
            {
                Player.gameObject.SetActive(true);
                if (home && home.TryGetComponent<InteriorSpace>(out var space)) { home.Body.isKinematic = false; Player.EnterInterior(space, ps.localPosition); }
                else Player.Teleport(ps.position, ps.yaw);
                terrain.focus = Player.transform;
                if (cameraRig) cameraRig.SetTarget(Player.transform);
            }
            if (!string.IsNullOrEmpty(ps.carried))
            {
                var part = SpawnPart(ps.carried, Player.transform.position, Quaternion.identity);
                if (part) { part.damage = ps.carriedDamage; Player.Carry(part); }
            }
        }
    }
}
