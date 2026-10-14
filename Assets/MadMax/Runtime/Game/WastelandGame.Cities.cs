using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MadMax.Building;
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
        public readonly List<RollingCity> Cities = new List<RollingCity>();
        public readonly List<CityDock> CityDocks = new List<CityDock>();
        double pendingCityClock = -1;
        double pendingCityClock2 = -1;

        /// <summary>This new game starts aboard the rolling city.</summary>
        public bool CityStart => Rules != null && Rules.start >= 1 && Rules.start <= 2 && !Rules.story && World != null && World.city != null;

        /// <summary>Where the terrain should be built first for a city start: the first dock.</summary>
        public int StartingCityIndex => Rules != null && Rules.start == 2 ? 1 : 0;
        Vector3 CityFocus() { var r = StartingCityIndex == 1 && World.city2 != null ? World.city2 : World.city; return r.At(r.docks[0], out _); }

        IEnumerator SpawnCity()
        {
            if (World == null || World.city == null) yield break;
            var routes = new[] { World.city, World.city2 };
            for (int r = 0; r < routes.Length; r++)
            {
                var route = routes[r];
                if (route == null) continue;
                if (route.tracked != null)
                {
                    // the generated tracked city: high docks with a stair tower and a vehicle lift
                    var kits = new Task<CityKit>[route.docks.Length];
                    float H = route.deckHeight;
                    for (int k = 0; k < kits.Length; k++) { int s = World.seed * 7 + k + r * 101; kits[k] = Task.Run(() => { var kit = TrackedCity.Dock(H, s); CityPrep.Pack(kit); return kit; }); }
                    int liftSeed = World.seed + r;
                    var liftKit = Task.Run(() => { var kit = TrackedCity.LiftPlatform(liftSeed); CityPrep.Pack(kit); return kit; });
                    while (!System.Array.TrueForAll(kits, t => t.IsCompleted) || !liftKit.IsCompleted) yield return null;
                    for (int k = 0; k < kits.Length; k++)
                    {
                        if (kits[k].IsFaulted) { Debug.LogException(kits[k].Exception); continue; }
                        UnityEngine.Profiling.Profiler.BeginSample("MadMax.Start.CityDock");
                        CityDocks.Add(CityDock.CreateTracked(route, k, kits[k].Result, liftKit.Result, propMaterial, RollingCity.GlowMaterial(propMaterial)));
                        UnityEngine.Profiling.Profiler.EndSample();
                        yield return null;
                    }
                }
                else
                {
                    var data = new Task<VoxelMesher.MeshData>[route.docks.Length];
                    for (int k = 0; k < data.Length; k++) { int s = World.seed * 7 + k + r * 101; data[k] = Task.Run(() => VoxelMesher.BuildData(CityDesign.Dock(s), CityDesign.V)); }
                    while (!System.Array.TrueForAll(data, t => t.IsCompleted)) yield return null;
                    for (int k = 0; k < data.Length; k++)
                    {
                        if (data[k].IsFaulted) { Debug.LogException(data[k].Exception); continue; }
                        CityDocks.Add(CityDock.Create(route, k, World.seed + r * 31, propMaterial, data[k]));
                    }
                }
                UnityEngine.Profiling.Profiler.BeginSample("MadMax.Start.City");
                var city = RollingCity.Create(route, World.seed + r * 31, propMaterial, r == 1);
                Cities.Add(city);
                if (r == 0) City = city;
                city.Clock = r == 0
                    ? (pendingCityClock >= 0 ? pendingCityClock : CityStart && StartingCityIndex == 0 ? 12.0 : city.Cycle - 90.0)
                    : (pendingCityClock2 >= 0 ? pendingCityClock2 : CityStart && StartingCityIndex == 1 ? 12.0 : city.Cycle - 90.0);
                UnityEngine.Profiling.Profiler.EndSample();
                float t0 = Time.realtimeSinceStartup, p0 = r == 0 ? 0.7f : 0.78f;
                while (!city.Ready) { if (!Ready) ScreenFader.Progress(p0 + 0.07f * (1f - Mathf.Exp(-(Time.realtimeSinceStartup - t0) / 2f))); yield return null; }
                UnityEngine.Profiling.Profiler.BeginSample("MadMax.Start.CityPeople");
                city.Snap();
                RestoreCityPieces(city);
                if (city.Plan != null) MadMax.Npc.CityFolk.Attach(this, city, CityHost.Hosts.IndexOf(city));                 // shopkeepers, the clerk, residents
                UnityEngine.Profiling.Profiler.EndSample();
            }
        }

        /// <summary>The megacity (roadmap 28 R4), null until spawned or in worlds without one.</summary>
        public MegaCityHost Mega { get; private set; }

        /// <summary>The megacity: created at once (its host index is fixed after the rolling cities), built on workers
        /// while the game runs; then saved pieces return and its people move in.</summary>
        IEnumerator SpawnMegaCity()
        {
            Mega = MegaCityHost.Create(World.mega, propMaterial);
            while (Mega && !Mega.IsReady) yield return null;
            if (!Mega) yield break;
            RestoreCityPieces(Mega);
            MadMax.Npc.CityFolk.Attach(this, Mega, CityHost.Hosts.IndexOf(Mega));
        }

        /// <summary>On the deck by the gangway, facing the dock.</summary>
        void PlaceAboard()
        {
            Player.gameObject.SetActive(true);
            var startCity = Cities[Mathf.Min(StartingCityIndex, Cities.Count - 1)];
            var p = startCity.DeckPoint(startCity.Tracked != null ? new Vector3(startCity.Route.halfWidth - 6f, 0.15f, 0f) : new Vector3(9f, 0.15f, 0f));
            float yaw = startCity.transform.eulerAngles.y + 90f;
            Player.Teleport(p, yaw);
            terrain.focus = Player.transform;
            if (cameraRig) cameraRig.SetTarget(Player.transform);
            var dock = startCity.Dock >= 0 ? startCity.Route.dockNames[startCity.Dock] : "THE CRAWLERWAY";
            Toast("ABOARD " + startCity.Name + ", DOCKED AT THE " + dock);
        }

        partial void CitySave(SaveData d)
        {
            d.blockCity = new List<string>();
            foreach (var city in Cities) if (city) d.blockCity.Add(city.Clock.ToString("R", CultureInfo.InvariantCulture));
            QuartersSave(d);
        }

        partial void CityLoad(SaveData d)
        {
            QuartersLoad(d);
            if (d.blockCity == null || d.blockCity.Count == 0) return;
            if (double.TryParse(d.blockCity[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var clock))
            {
                pendingCityClock = clock;
                if (City && City.Ready) { City.Clock = clock; City.Snap(); }
            }
            if (d.blockCity.Count > 1 && double.TryParse(d.blockCity[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var clock2))
            {
                pendingCityClock2 = clock2;
                if (Cities.Count > 1 && Cities[1].Ready) { Cities[1].Clock = clock2; Cities[1].Snap(); }
            }
        }

        partial void CityNewGame() { Leases.Clear(); pendingCityPieces.Clear(); }

        /// <summary>Pieces built aboard a city wait in the save until that city is built, then ride it again.</summary>
        readonly List<PlacedSave> pendingCityPieces = new List<PlacedSave>();

        public void RestoreCityPieces(CityHost city)
        {
            for (int i = pendingCityPieces.Count - 1; i >= 0; i--)
            {
                var p = pendingCityPieces[i];
                if (p.city != CityHost.Hosts.IndexOf(city)) continue;
                var placed = FurnitureLibrary.Spawn(p.id, city.transform, p.localPosition, p.localRotation, propMaterial);
                if (placed) { placed.hits = p.hits; placed.netId = p.netId; placed.owner = p.owner; placed.LoadState(p.state); }
                pendingCityPieces.RemoveAt(i);
            }
        }
    }
}
