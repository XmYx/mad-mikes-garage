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
        public List<string> searched = new List<string>();
        public bool hasSpawn; public Vector3 spawn;
        public int cameraMode;
        public GameRules rules;
        public MadMax.RPG.CharacterStats stats;
        public string[] hotbar;
        public List<string> pumpKeys = new List<string>(); public List<float> pumpUsed = new List<float>();
        public List<MadMax.World.DeformableTerrain.ChunkEdit> terrain = new List<MadMax.World.DeformableTerrain.ChunkEdit>();
    }

    /// <summary>Single save slot (JSON in persistentDataPath) plus the hand-over between scene reloads.</summary>
    public static class SaveSystem
    {
        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, "wasteland_save.json");
        public static bool HasSave => File.Exists(Path);

        /// <summary>Set before reloading the scene: load this state instead of generating a new game.</summary>
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
        static void Reset() { Pending = null; SkipMenu = false; PendingRules = null; PendingCharacter = null; PendingLook = null; PendingOutfit = null; PendingHost = false; }

        public static void Write(SaveData data)
        {
            data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            File.WriteAllText(Path, JsonUtility.ToJson(data));
        }

        public static SaveData Read()
        {
            try { return HasSave ? JsonUtility.FromJson<SaveData>(File.ReadAllText(Path)) : null; }
            catch (Exception e) { Debug.LogWarning("[MadMax] Save unreadable: " + e.Message); return null; }
        }
    }
}
