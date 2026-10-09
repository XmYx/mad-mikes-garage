using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MadMax.Voxel;
using MadMax.World;
using UnityEngine;

namespace MadMax.Game
{
    /// <summary>The rolling city in the game (roadmap 28): its docks and the city are built at world start (on workers),
    /// its timetable clock is saved (<c>SaveData.blockCity</c>), a new sandbox world has it arriving at the first dock
    /// beside the start yard, and START: ABOARD THE ROLLING CITY puts the player on its deck, docked.</summary>
    public partial class WastelandGame
    {
        public RollingCity City { get; private set; }
        public readonly List<CityDock> CityDocks = new List<CityDock>();
        double pendingCityClock = -1;

        /// <summary>This new game starts aboard the rolling city.</summary>
        public bool CityStart => Rules != null && Rules.start == 1 && !Rules.story && World != null && World.city != null;

        /// <summary>Where the terrain should be built first for a city start: the first dock.</summary>
        Vector3 CityFocus() { var r = World.city; return r.At(r.docks[0], out _); }

        IEnumerator SpawnCity()
        {
            var route = World != null ? World.city : null;
            if (route == null) yield break;
            var data = new Task<VoxelMesher.MeshData>[route.docks.Length];
            for (int k = 0; k < data.Length; k++) { int s = World.seed * 7 + k; data[k] = Task.Run(() => VoxelMesher.BuildData(CityDesign.Dock(s), CityDesign.V)); }
            while (!System.Array.TrueForAll(data, t => t.IsCompleted)) yield return null;
            for (int k = 0; k < data.Length; k++)
            {
                if (data[k].IsFaulted) { Debug.LogException(data[k].Exception); continue; }
                CityDocks.Add(CityDock.Create(route, k, World.seed, propMaterial, data[k]));
            }
            City = RollingCity.Create(route, World.seed, propMaterial);
            // a fresh world: aboard, it stands at the first dock; else it is a minute and a half from arriving there
            City.Clock = pendingCityClock >= 0 ? pendingCityClock : CityStart ? 12.0 : City.Cycle - 90.0;
            while (!City.Ready) yield return null;
            City.Snap();
        }

        /// <summary>On the deck by the gangway, facing the dock.</summary>
        void PlaceAboard()
        {
            Player.gameObject.SetActive(true);
            var p = City.DeckPoint(new Vector3(9f, 0.15f, 0f));
            float yaw = City.transform.eulerAngles.y + 90f;
            Player.Teleport(p, yaw);
            terrain.focus = Player.transform;
            if (cameraRig) cameraRig.SetTarget(Player.transform);
            var dock = City.Dock >= 0 ? World.city.dockNames[City.Dock] : "THE CRAWLERWAY";
            Toast("ABOARD " + City.Name + ", DOCKED AT THE " + dock);
        }

        partial void CitySave(SaveData d)
        {
            d.blockCity = new List<string>();
            if (City) d.blockCity.Add(City.Clock.ToString("R", CultureInfo.InvariantCulture));
        }

        partial void CityLoad(SaveData d)
        {
            if (d.blockCity == null || d.blockCity.Count == 0 || !double.TryParse(d.blockCity[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var clock)) return;
            pendingCityClock = clock;
            if (City && City.Ready) { City.Clock = clock; City.Snap(); }
        }

        partial void CityNewGame() { }
    }
}
