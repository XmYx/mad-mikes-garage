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

        public void LoadGame()
        {
            var data = SaveSystem.Read();
            if (data == null) { Toast("NO SAVE FOUND"); return; }
            SaveSystem.Pending = data;
            Reload();
        }

        static void Reload()
        {
            Time.timeScale = 1f;
            if (TitleSequence.Instance) TitleSequence.Instance.Finish();
            ScreenFader.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void SaveGame()
        {
            if (MadMax.Net.NetSession.Instance && MadMax.Net.NetSession.Instance.IsClient) { Toast("ONLY THE HOST CAN SAVE"); return; }
            SaveSystem.Write(CaptureSave());
            Toast("GAME SAVED");
        }

        /// <summary>Snapshot of the world (also the join payload for network clients).</summary>
        public SaveData CaptureSave(bool forNetwork = false)
        {
            var d = new SaveData { seed = seed, raining = Weather.Raining, wetness = Weather.Wetness, snow = Weather.Snow, temperature = Weather.Temperature, lakeRise = Weather.LakeRise, hours = DayNight.Hours, day = DayNight.Day, searched = new List<string>(Lootable.Searched), hasSpawn = spawnPoint.HasValue, spawn = spawnPoint ?? Vector3.zero, resources = Inventory.ResourceArray, rules = Rules, stats = Stats };
            if (cameraRig) d.cameraMode = (int)cameraRig.mode;
            foreach (var kv in Inventory.Items) if (kv.Value > 0) d.items.Add(new ItemSave { id = kv.Key, count = kv.Value });
            d.hotbar = (string[])Hotbar.Clone();
            foreach (var kv in GasPump.Used) { d.pumpKeys.Add(kv.Key); d.pumpUsed.Add(kv.Value); }
            d.terrain = terrain.SaveEdits();
            d.npcs = MadMax.Npc.NpcRegistry.SaveAll();
            d.reputation = MadMax.Npc.NpcRegistry.Reputation;
            SaveTools(d);
            SaveClothes(d);
            if (MadMax.Npc.NpcDirector.Instance) d.convoys = MadMax.Npc.NpcDirector.Instance.SaveConvoys();

            vehicles.RemoveAll(v => !v);
            var saved = vehicles.FindAll(v => !v.aiDriven);            // NPC-driven vehicles come back with their convoy
            foreach (var v in saved)
            {
                var vs = new VehicleSave
                {
                    design = v.GetComponent<VehicleChassis>().vehicleName, position = v.transform.position, rotation = v.transform.rotation,
                    fleet = fleet.Contains(v), wreck = wrecks.Contains(v), fourWheel = v.FourWheelDrive, diffLocked = v.diffLocked, radio = v.TryGetComponent<MadMax.Audio.RadioReceiver>(out var rr) ? rr.SaveState() : null,
                    netId = v.netId, owner = v.owner,
                    cargo = v.TryGetComponent<Container>(out var cg) ? cg.SaveState() : null
                };
                foreach (var s in v.GetComponent<VehicleChassis>().Sockets)
                    vs.sockets.Add(new SocketSave { socket = s.name, part = s.Current ? s.Current.partId : null, damage = s.Current ? s.Current.damage : 0f, wear = s.Current && s.Current.TryGetComponent<WheelStats>(out var ws) ? ws.wear : 0f });
                if (v.TryGetComponent<VehicleSystems>(out var sys)) { vs.fuel = sys.fuel; vs.oil = sys.oil; vs.coolant = sys.coolant; }
                if (v.TryGetComponent<VehicleDamage>(out var dmg)) { vs.frame = dmg.FrameDamage; vs.salvage = dmg.salvagePool; }
                var tc = v.GetComponent<TowCoupling>();
                if (tc && tc.Tower) vs.towedBy = saved.IndexOf(tc.Tower);
                d.vehicles.Add(vs);
            }
            foreach (var part in VehiclePart.Registry)
                if (part && !part.Socket && part != Player.Carried)
                    d.loose.Add(new LooseSave { part = part.partId, position = part.transform.position, rotation = part.transform.rotation, damage = part.damage, netId = part.netId });
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

        // ------------------------------------------------------------------ restore (called from Start)
        void RestoreDestruction(SaveData d)
        {
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
                    if (s.Current && s.Current.partId == saved.part) { s.Current.damage = saved.damage; if (s.Current.TryGetComponent<WheelStats>(out var ws0)) ws0.wear = saved.wear; continue; }
                    var old = s.Detach(false);
                    if (old) Destroy(old.gameObject);
                    if (string.IsNullOrEmpty(saved.part)) continue;
                    var part = SpawnPart(saved.part, s.transform.position, s.transform.rotation);
                    if (part) { s.Attach(part); part.damage = saved.damage; if (part.TryGetComponent<WheelStats>(out var ws1)) ws1.wear = saved.wear; }
                }
                if (go.TryGetComponent<VehicleSystems>(out var sys)) { sys.fuel = vs.fuel; sys.oil = vs.oil; sys.coolant = vs.coolant; }
                if (!string.IsNullOrEmpty(vs.cargo) && go.TryGetComponent<Container>(out var cargo)) cargo.LoadState(vs.cargo);
                if (go.TryGetComponent<VehicleDamage>(out var dmg)) { dmg.AddFrameDamage(vs.frame, 1f); dmg.salvagePool = vs.salvage; }
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
            if (d.hotbar != null && d.hotbar.Length == HotbarSize) { Hotbar = d.hotbar; for (int i = 0; i < HotbarSize; i++) if (Hotbar[i] == "") Hotbar[i] = null; }
            SyncHotbar();
            terrain.LoadEdits(d.terrain);
            Weather.Restore(d.raining, d.wetness, d.snow, d.temperature, d.lakeRise);
            if (d.hours >= 0f) DayNight.SetHours(d.hours);
            DayNight.SetDay(d.day);
            MadMax.Npc.NpcRegistry.Load(d.npcs, d.reputation);
            RestoreTools(d);
            RestoreClothes(d);
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
