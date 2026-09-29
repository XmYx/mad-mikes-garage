using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MadMax.Game
{
    [Serializable] public class SocketSave { public string socket, part, state; public float damage, wear; public int q; }   // q = make + 1 (0: unknown, sturdy)

    [Serializable]
    public class VehicleSave
    {
        public string design;
        public ushort netId, owner;
        public Vector3 position;
        public Quaternion rotation;
        public bool fleet, wreck;
        public List<SocketSave> sockets = new List<SocketSave>();
        public float fuel, oil, coolant, frame, salvage = -1f, additive;
        public int tank;                   // ResourceType in the tank (0: whatever the engine burns)
        public bool fourWheel, diffLocked;
        public int towedBy = -1;
        public string cargo;               // machine bed / hopper contents
        public string radio;               // RadioReceiver state (on, station, volume)
        public string armor;               // VehicleArmor zones (material, condition)
        public string tuning;              // VehicleTuning settings
        public string paint;               // VehiclePaint colour,decal
        public string service;             // VehicleSystems maintenance (oil life, air filter, plugs, hours)
    }

    [Serializable] public class LooseSave { public string part, state; public Vector3 position; public Quaternion rotation; public float damage; public uint netId; public int q; }
    [Serializable] public class PlacedSave { public string id; public int vehicle = -1; public Vector3 localPosition; public Quaternion localRotation; public int hits; public uint netId; public string state, owner; }
    [Serializable] public class DestroyedSave { public string key, template; public bool all; public List<Vector3Int> removed = new List<Vector3Int>(); }
    [Serializable] public class ItemSave { public string id; public int count; }

    [Serializable]
    public class PlayerSave
    {
        public int vehicle = -1, interior = -1;
        public Vector3 position, localPosition;
        public float yaw;
        public string tool, carried;
        public Appearance appearance;
        public List<string> outfit = new List<string>();
        public float carriedDamage;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int seed;
        public string savedAt;
        public PlayerSave player = new PlayerSave();
        public List<VehicleSave> vehicles = new List<VehicleSave>();
        public List<LooseSave> loose = new List<LooseSave>();
        public List<PlacedSave> placed = new List<PlacedSave>();
        public List<DestroyedSave> destroyed = new List<DestroyedSave>();
        public int[] resources;
        public List<ItemSave> items = new List<ItemSave>();
        public bool raining;
        public float wetness, snow, temperature = float.NaN, lakeRise, hours = -1f;
        public int day;
        public List<MadMax.Npc.NpcSave> npcs = new List<MadMax.Npc.NpcSave>();
        public List<MadMax.Npc.ConvoySave> convoys = new List<MadMax.Npc.ConvoySave>();
        public int reputation;
        public List<string> toolWearIds = new List<string>(); public List<float> toolWear = new List<float>();
        public List<string> clothWearIds = new List<string>(); public List<float> clothWear = new List<float>();
        public List<string> itemQualityIds = new List<string>(); public List<float> itemQuality = new List<float>();
        public List<string> fishRecordIds = new List<string>(); public List<float> fishRecordKg = new List<float>();
        public List<string> gunRoundIds = new List<string>(); public List<int> gunRounds = new List<int>();
        public string market;
        public List<MadMax.Npc.Contract> contracts = new List<MadMax.Npc.Contract>();
        public List<string> contractsTaken = new List<string>();
        public float raidNext = -1f; public string raidReport;
        public string plans;
        public List<MadMax.Npc.CompanionSave> companions = new List<MadMax.Npc.CompanionSave>();
        public List<string> townQuests = new List<string>();
        public string factions;
        public List<MadMax.Animals.AnimalSave> animals = new List<MadMax.Animals.AnimalSave>();
        public List<string> foundAircraft = new List<string>(), scouted = new List<string>();
        public List<string> discovered = new List<string>(), journal = new List<string>();
        public bool hasWaypoint; public Vector3 waypoint;
        public List<WastelandGame.StashSave> stashes = new List<WastelandGame.StashSave>();
        public string animalKills;
        public List<string> searched = new List<string>();
        public bool hasSpawn; public Vector3 spawn;
        public int cameraMode;
        public GameRules rules;
        public MadMax.RPG.CharacterStats stats;
        public string[] hotbar;
        public List<string> pumpKeys = new List<string>(); public List<float> pumpUsed = new List<float>();
        public List<MadMax.World.DeformableTerrain.ChunkEdit> terrain = new List<MadMax.World.DeformableTerrain.ChunkEdit>();
    }

    /// <summary>Save slots (JSON in persistentDataPath): 0 = autosave, 1..3 = the player's slots (slot 1 keeps the old
    /// single-slot file name). Every write keeps the previous file as a .bak; a small .meta line per slot feeds the slot
    /// list without parsing whole saves. Also the hand-over between scene reloads.</summary>
    public static class SaveSystem
    {
        public const int Slots = 4;                                  // 0 autosave + 3
        /// <summary>The slot the running game was loaded from / last saved to (autosaves go to 0 regardless).</summary>
        public static int Slot = 1;

        public static string PathOf(int slot) => System.IO.Path.Combine(Application.persistentDataPath,
            slot == 0 ? "wasteland_autosave.json" : slot == 1 ? "wasteland_save.json" : "wasteland_save_" + slot + ".json");
        public static string Path => PathOf(Slot);
        public static bool Exists(int slot) => File.Exists(PathOf(slot));
        /// <summary>Any save at all (CONTINUE, LOAD).</summary>
        public static bool HasSave { get { for (int i = 0; i < Slots; i++) if (Exists(i)) return true; return false; } }

        /// <summary>The most recently written slot, -1 if none.</summary>
        public static int Latest()
        {
            int best = -1; DateTime bt = DateTime.MinValue;
            for (int i = 0; i < Slots; i++)
            {
                if (!Exists(i)) continue;
                var t = File.GetLastWriteTime(PathOf(i));
                if (t > bt) { bt = t; best = i; }
            }
            return best;
        }

        /// <summary>One line for the slot list: when, which day, where.</summary>
        public static string Info(int slot)
        {
            if (!Exists(slot)) return "EMPTY";
            try { var meta = PathOf(slot) + ".meta"; if (File.Exists(meta)) return File.ReadAllText(meta).Trim(); } catch { }
            return File.GetLastWriteTime(PathOf(slot)).ToString("yyyy-MM-dd HH:mm");
        }

        public static void Delete(int slot)
        {
            foreach (var f in new[] { PathOf(slot), PathOf(slot) + ".meta" }) if (File.Exists(f)) File.Delete(f);
        }

        public static SaveData Pending;
        /// <summary>Set before reloading the scene: skip the main menu (new game requested from the menu).</summary>
        public static bool SkipMenu;
        /// <summary>New game setup handed across the scene reload (rules + created character).</summary>
        public static GameRules PendingRules;
        public static MadMax.RPG.CharacterStats PendingCharacter;
        public static Appearance PendingLook;
        public static List<string> PendingOutfit;
        public static bool PendingHost;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Pending = null; SkipMenu = false; PendingRules = null; PendingCharacter = null; PendingLook = null; PendingOutfit = null; PendingHost = false; Slot = 1; }

        public static void Write(SaveData data) => Write(data, Slot, null);

        /// <summary>Write a slot, keeping the previous file as a .bak (written to a temp file first: a crash mid-write
        /// never leaves a half save).</summary>
        public static void Write(SaveData data, int slot, string summary)
        {
            data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            string path = PathOf(slot), tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data));
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Copy(tmp, path, true);
            File.Delete(tmp);
            File.WriteAllText(path + ".meta", data.savedAt + (string.IsNullOrEmpty(summary) ? "" : "  " + summary));
        }

        public static SaveData Read() => Read(Slot);

        public static SaveData Read(int slot)
        {
            string path = PathOf(slot);
            try { return File.Exists(path) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(path)) : null; }
            catch (Exception e)
            {
                // a damaged file: fall back to the backup
                Debug.LogWarning("[MadMax] Save unreadable, trying the backup: " + e.Message);
                try { return File.Exists(path + ".bak") ? JsonUtility.FromJson<SaveData>(File.ReadAllText(path + ".bak")) : null; }
                catch { return null; }
            }
        }
    }
}
