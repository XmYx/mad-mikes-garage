using System.Collections.Generic;
using MadMax.Building;
using UnityEngine;

namespace MadMax.World
{
    /// <summary>The lights of a settlement building (added by <see cref="TownLights"/> to the building's
    /// <see cref="DestructibleVoxels"/>): ceiling / wall fixtures and a <see cref="LightSwitch"/> inside by the door that
    /// runs them all. Inhabited houses and shops are on the town grid and the residents switch on at dusk and off at
    /// bedtime (22-24 h, per house); the player's flip holds until the residents' next change. Ruins (the city towers)
    /// have dead wiring: their lights only work while a live power network node the player built stands within
    /// <see cref="RuinFeedReach"/> m of the switch. A fixture or switch whose wall / ceiling is carved away falls and breaks.</summary>
    public class BuildingLights : MonoBehaviour
    {
        public const float RuinFeedReach = 8f;
        public static readonly List<BuildingLights> All = new List<BuildingLights>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => All.Clear();

        public int town = -1;
        public bool ruin;
        public float bedtime = 23f;
        public LightSwitch lightSwitch;
        public readonly List<PoweredLight> lights = new List<PoweredLight>();
        readonly List<(Transform t, Vector3Int support)> mounts = new List<(Transform, Vector3Int)>();
        DestructibleVoxels building;
        float poll;
        int lastWant = -1;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            building = GetComponent<DestructibleVoxels>();
            if (building) building.Carved += CheckSupport;
            Tick();
        }

        void OnDestroy() { if (building) building.Carved -= CheckSupport; }

        /// <summary>Remember the voxel a fixture hangs on (building grid coordinates).</summary>
        public void Mount(Transform t, Vector3Int support) => mounts.Add((t, support));

        /// <summary>Fed right now (town grid, or a player's live network beside a ruin's switch).</summary>
        public bool Powered { get; private set; }
        /// <summary>The residents want the lights on (dusk until their bedtime).</summary>
        public bool ResidentsWant => !ruin && DayNight.Darkness > 0.3f && !(DayNight.Hours >= bedtime || DayNight.Hours < 5f);

        void Update()
        {
            if ((poll -= Time.deltaTime) > 0f) return;
            poll = 1f;
            Tick();
        }

        void Tick()
        {
            Powered = ruin ? RuinFed() : TownLights.TownPowered(town);
            foreach (var l in lights) if (l) { l.external = true; l.externalPower = Powered; }
            if (!lightSwitch) return;
            lightSwitch.deadNote = ruin ? "OLD WIRING DEAD: BRING POWER WITHIN 8 M" : "TOWN POWER OFF";
            if (ruin) return;
            int want = ResidentsWant ? 1 : 0;
            if (want != lastWant) { lastWant = want; lightSwitch.Set(want == 1, false); }            // residents flip at their edges only
        }

        bool RuinFed()
        {
            if (!lightSwitch) return false;
            var at = lightSwitch.transform.position;
            foreach (var n in UtilityNode.All)
                if (n && n.Piece && n.Powered && (n.kinds & UtilityKind.Power) != 0 && (n.transform.position - at).sqrMagnitude < RuinFeedReach * RuinFeedReach) return true;
            return false;
        }

        void CheckSupport()
        {
            if (!building || building.Grid == null) return;
            var g = building.Grid;
            for (int i = mounts.Count - 1; i >= 0; i--)
            {
                var (t, s) = mounts[i];
                if (!t) { mounts.RemoveAt(i); continue; }
                bool held = false;
                for (int dx = -1; dx <= 1 && !held; dx++) for (int dy = -1; dy <= 1 && !held; dy++) for (int dz = -1; dz <= 1 && !held; dz++)
                    if (g.Has(s.x + dx, s.y + dy, s.z + dz)) held = true;
                if (held) continue;
                mounts.RemoveAt(i);
                var pl = t.GetComponent<PoweredLight>();
                if (pl) { pl.Smash(t.position); lights.Remove(pl); }
                else Shards.Drop(t.position, Shards.Kind.Clear);
                Destroy(t.gameObject);
            }
        }
    }
}
