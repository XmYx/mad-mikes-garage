using System.Collections.Generic;
using System.IO;
using MadMax.Designs;
using MadMax.Game;
using MadMax.World;
using MadMax.Rendering;
using MadMax.Vehicles;
using MadMax.Voxel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MadMax.EditorTools
{
    /// <summary>Generates voxel meshes, part prefabs, vehicle prefabs and the demo scene from the code designs.</summary>
    public static class MadMaxBuilder
    {
        const string Root = "Assets/MadMax/Generated";
        const string MeshDir = Root + "/Meshes";
        const string PartDir = Root + "/Prefabs/Parts";
        const string VehicleDir = Root + "/Prefabs/Vehicles";
        const string MatDir = Root + "/Materials";
        const string ScenePath = "Assets/MadMax/Scenes/Wasteland.unity";
        const string GameScenePath = "Assets/MadMax/Scenes/Wasteland_Game.unity";
        const string BootScenePath = "Assets/MadMax/Scenes/Boot.unity";
        const float S = VoxelMesher.DefaultSize;

        [MenuItem("MadMax/Build Parts + Vehicles")]
        public static void BuildAssets() => BuildAssetsInternal(out _);

        [MenuItem("MadMax/Build Demo Scene")]
        public static void BuildScene()
        {
            BuildAssetsInternal(out var vehicles);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mat = Material("PixelVoxel", 0);
            var sky = Material("PixelVoxel_Unlit", 1);

            const float yaw = 135f;                          // camera-aligned environment frame
            var env = new GameObject("Environment").transform;
            env.rotation = Quaternion.Euler(0, yaw, 0);

            AddMesh(env, "Ground", Scenery.Ground(120, -90, 62, 60, 5), S, Vector3.zero, mat);
            var mesas = Scenery.Mesas(70, 9, new[] { Pal.Hex("a24f28"), Pal.Hex("b45c2e"), Pal.Hex("c46a34") });
            AddMesh(env, "Mesas", mesas, 0.16f, new Vector3(0, -0.2f, 5.6f), sky);
            AddMesh(env, "Sky", Scenery.Sky(60, 40, 3), 0.28f, new Vector3(0, -0.4f, 6.4f), sky);
            AddMesh(env, "DeadTree_A", Scenery.DeadTree(3, 22), S, new Vector3(-4.6f, 0, 3.2f), mat);
            AddMesh(env, "DeadTree_B", Scenery.DeadTree(8, 16), S, new Vector3(5.2f, 0, 4.2f), mat);
            AddMesh(env, "DeadTree_C", Scenery.DeadTree(12, 12), S, new Vector3(-6.4f, 0, 4.6f), mat);

            var camGo = new GameObject("PixelCamera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3.4f;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Pal.Hex("cc6030");
            camGo.transform.SetParent(env, false);
            camGo.transform.localRotation = Quaternion.Euler(24, 0, 0);
            camGo.transform.localPosition = new Vector3(0, 0.9f, 0.6f) + camGo.transform.localRotation * new Vector3(0, 0, -30f);
            camGo.transform.SetParent(null, true);
            camGo.AddComponent<PixelArtCamera>().pixelHeight = 270;

            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.86f, 0.66f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(42, yaw - 55, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.3f, 0.25f);
            RenderSettings.skybox = null;

            // Vehicles: interceptor in 3/4 front view, scavenger behind-right.
            Place(vehicles["Interceptor"], env.TransformPoint(new Vector3(-2.5f, 0, -0.9f)), yaw - 127f);
            Place(vehicles["Scavenger"], env.TransformPoint(new Vector3(2.5f, 0, 0.8f)), yaw + 143f);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[MadMax] Demo scene built: " + ScenePath);
        }

        [MenuItem("MadMax/Build Game Scene")]
        public static void BuildGameScene()
        {
            BuildAssetsInternal(out var vehicles);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var terrainMat = Material("PixelTerrain", 0);
            terrainMat.SetFloat("_OutlinePx", 0);
            var propMat = Material("PixelVoxel", 0);
            var tilt = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/TiltShift.mat");
            if (!tilt)
            {
                tilt = new Material(Shader.Find("Hidden/MadMax/TiltShift"));
                AssetDatabase.CreateAsset(tilt, $"{MatDir}/TiltShift.mat");
            }

            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.86f, 0.66f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(45, -30, 0);

            var camGo = new GameObject("PixelCamera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.5f, 0.28f);
            var pixel = camGo.AddComponent<PixelArtCamera>();
            pixel.pixelHeight = 320;
            var rig = camGo.AddComponent<CameraRig>();
            rig.pixel = pixel;
            rig.tiltShiftMaterial = tilt;

            var game = new GameObject("WastelandGame").AddComponent<WastelandGame>();
            game.terrainMaterial = terrainMat;
            game.propMaterial = propMat;
            game.vehiclePrefabs = new[] { vehicles["Interceptor"], vehicles["Scavenger"], vehicles["Trabant"], vehicles["Hauler"],
                                          vehicles["Excavator"], vehicles["Backhoe"], vehicles["Bulldozer"], vehicles["DumpTruck"], vehicles["Paver"], vehicles["Roller"], vehicles["Wrecker"],
                                          vehicles["Pickup"], vehicles["Coupe"], vehicles["Sedan"], vehicles["Wagon"], vehicles["TowTruck"],
                                          vehicles["Bus"], vehicles["Ambulance"], vehicles["APC"], vehicles["Semi"], vehicles["MonsterTruck"], vehicles["DuneBuggy"],
                                          vehicles["DirtBike"], vehicles["Chopper"], vehicles["Bicycle"], vehicles["SidecarOutfit"],
                                          vehicles["Ultralight"], vehicles["Gyrocopter"] };
            game.boatPrefabs = new[] { vehicles["Raft"], vehicles["Skiff"], vehicles["Trawler"], vehicles["Houseboat"], vehicles["IronEel"] };
            game.trailerPrefabs = new[] { vehicles["Tanker"], vehicles["TankerSmall"], vehicles["CargoTrailer"], vehicles["CarTrailer"], vehicles["CarTrailerDouble"], vehicles["BoxTrailer"] };
            game.partPrefabs = new List<GameObject>(lastParts.Values).ToArray();
            game.cameraRig = rig;
            game.sun = sun;

            Directory.CreateDirectory(Path.GetDirectoryName(GameScenePath));
            EditorSceneManager.SaveScene(scene, GameScenePath);
            Debug.Log("[MadMax] Game scene built: " + GameScenePath);
            BuildBootScene();
            // Boot (intro film + background load) is the entry point; the game scene reloads itself for new game / load
            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(BootScenePath, true), new EditorBuildSettingsScene(GameScenePath, true) };
            foreach (var bs in EditorBuildSettings.scenes) if (bs.path != GameScenePath && bs.path != BootScenePath) list.Add(bs);
            EditorBuildSettings.scenes = list.ToArray();
        }

        /// <summary>Boot scene: a camera that shows the pre-rendered intro film while the game scene loads.</summary>
        static void BuildBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("BootCamera").AddComponent<Camera>();
            cam.gameObject.AddComponent<AudioListener>();
            cam.gameObject.AddComponent<BootLoader>();
            EditorSceneManager.SaveScene(scene, BootScenePath);
            EditorSceneManager.OpenScene(GameScenePath);
        }

        /// <summary>Standalone Linux player (also the dedicated server: run with -batchmode -nographics -server [-port N]).</summary>
        [MenuItem("MadMax/Build Linux Player")]
        public static void BuildLinux()
        {
            // always ship the latest content: regenerate parts, vehicles, game + boot scenes from the design code
            BuildGameScene();
            AssetDatabase.SaveAssets();
            // Vulkan first: it picks the discrete GPU on hybrid laptops/desktops (OpenGL would run on the iGPU)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore });
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { BootScenePath, GameScenePath },
                target = BuildTarget.StandaloneLinux64,
                locationPathName = "Builds/Linux/MadMikesGarage.x86_64",
                options = BuildOptions.None
            };
            var report = UnityEditor.BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[MadMax] Linux build: {report.summary.result} {report.summary.totalSize / 1048576} MB in {report.summary.totalTime}");
        }

        static void Place(GameObject prefab, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        }

        static void AddMesh(Transform parent, string name, VoxelGrid g, float size, Vector3 localPos, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.GetComponent<MeshFilter>().sharedMesh = SaveMesh(VoxelMesher.Build(g, name, size), "Scenery_" + name);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
        }

        static Dictionary<string, GameObject> lastParts;

        static void BuildAssetsInternal(out Dictionary<string, GameObject> vehicles)
        {
            foreach (var dir in new[] { MeshDir, PartDir, VehicleDir, MatDir }) Directory.CreateDirectory(dir);
            var mat = Material("PixelVoxel", 0);

            var parts = new Dictionary<string, GameObject>();
            var partDesigns = new Dictionary<string, PartDesign>();
            foreach (var p in PartLibrary.All()) { parts[p.key] = SavePart(p, mat); partDesigns[p.key] = p; }

            vehicles = new Dictionary<string, GameObject>();
            foreach (var d in new[] { VehicleDesigns.Interceptor(), VehicleDesigns.Scavenger(), VehicleDesigns.Trabant(), VehicleDesigns.Hauler(), VehicleDesigns.Tanker(), VehicleDesigns.TankerSmall(), VehicleDesigns.CargoTrailer(),
                                      VehicleDesigns.Excavator(), VehicleDesigns.Backhoe(), VehicleDesigns.Bulldozer(), VehicleDesigns.DumpTruck(), VehicleDesigns.Paver(), VehicleDesigns.Roller(),
                                      VehicleDesigns.Wrecker(), VehicleDesigns.CarTrailer(), VehicleDesigns.CarTrailerDouble(),
                                      VehicleDesigns.Pickup(), VehicleDesigns.Coupe(), VehicleDesigns.Sedan(), VehicleDesigns.Wagon(), VehicleDesigns.TowTruck(),
                                      VehicleDesigns.Bus(), VehicleDesigns.Ambulance(), VehicleDesigns.Apc(), VehicleDesigns.SemiTractor(), VehicleDesigns.BoxTrailer(),
                                      VehicleDesigns.MonsterTruck(), VehicleDesigns.DuneBuggy(),
                                      VehicleDesigns.DirtBike(), VehicleDesigns.Chopper(), VehicleDesigns.Bicycle(), VehicleDesigns.SidecarOutfit(),
                                      VehicleDesigns.Ultralight(), VehicleDesigns.Gyrocopter(),
                                      VehicleDesigns.Raft(), VehicleDesigns.Skiff(), VehicleDesigns.Trawler(), VehicleDesigns.Houseboat(), VehicleDesigns.IronEel() })
            {
                d.CarveWheelArches(k => partDesigns.TryGetValue(k, out var pd) ? pd : null);   // tyres never poke through panels (cut doors / hood too)
                foreach (var p in d.parts) parts[p.key] = SavePart(p, mat);
                vehicles[d.name] = SaveVehicle(d, parts, mat);
            }
            lastParts = parts;
            AssetDatabase.SaveAssets();
            Debug.Log($"[MadMax] Built {parts.Count} parts, {vehicles.Count} vehicles.");
        }

        static GameObject SavePart(PartDesign p, Material mat)
        {
            var go = new GameObject(p.key, typeof(MeshFilter), typeof(MeshRenderer));
            var baseGrid = p.segments.Count > 0 ? p.grid.Extract("body", Vector3Int.zero) : p.grid;
            var mesh = SaveMesh(VoxelMesher.Build(baseGrid, p.key), "Part_" + p.key);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (mesh.vertexCount > 0)
            {
                var col = go.AddComponent<BoxCollider>();
                col.center = mesh.bounds.center; col.size = mesh.bounds.size;
            }
            // hinged segments: a child per labelled voxel group, pivoting at its joint, nested by parent
            var made = new Dictionary<string, Transform>();
            foreach (var sd in p.segments)
            {
                var seg = new GameObject(sd.name, typeof(MeshFilter), typeof(MeshRenderer));
                var parentT = sd.parent != null && made.TryGetValue(sd.parent, out var pt) ? pt : go.transform;
                var parentPivot = sd.parent != null ? p.segments.Find(x => x.name == sd.parent).pivot : Vector3Int.zero;
                seg.transform.SetParent(parentT, false);
                seg.transform.localPosition = (Vector3)(sd.pivot - parentPivot) * VoxelMesher.DefaultSize;
                var sm = SaveMesh(VoxelMesher.Build(p.grid.Extract(sd.name, sd.pivot), p.key + "_" + sd.name), "Part_" + p.key + "_" + sd.name);
                seg.GetComponent<MeshFilter>().sharedMesh = sm;
                seg.GetComponent<MeshRenderer>().sharedMaterial = mat;
                if (sm.vertexCount > 0) { var sc = seg.AddComponent<BoxCollider>(); sc.center = sm.bounds.center; sc.size = sm.bounds.size; }
                made[sd.name] = seg.transform;
            }
            // machine tools dig into the ground: they must not collide with the terrain (MadMax.World.Layers)
            if (p.category == PartCategory.Tool)
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = MadMax.World.Layers.MachineTool;
            var part = go.AddComponent<VehiclePart>();
            part.partId = p.key; part.category = p.category; part.sizeClass = p.sizeClass; part.mass = p.mass; part.radius = p.radius;
            if (p.category == PartCategory.Engine)
            {
                var e = go.AddComponent<EngineStats>();
                e.maxTorque = p.torque; e.maxRpm = p.maxRpm; e.peakAt = p.peakAt;
            }
            if (p.category == PartCategory.Wheel)
            {
                var w = go.AddComponent<WheelStats>();
                w.grip = p.grip; w.mudGrip = p.mudGrip; w.width = p.width;
                if (p.wetGrip > 0f) w.wetGrip = p.wetGrip;
                if (p.rolling > 0f) w.rolling = p.rolling;
                if (p.wearRate > 0f) w.wearRate = p.wearRate;
                if (p.footprint > 0f) w.footprint = p.footprint;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PartDir}/{p.key}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject SaveVehicle(VehicleDesign d, Dictionary<string, GameObject> parts, Material mat)
        {
            var root = new GameObject(d.name);
            var chassis = root.AddComponent<VehicleChassis>();
            chassis.vehicleName = d.name; chassis.bodyMass = d.mass;

            var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(root.transform, false);
            var mesh = SaveMesh(VoxelMesher.Build(d.body, d.name + "_Body"), "Body_" + d.name);
            body.GetComponent<MeshFilter>().sharedMesh = mesh;
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (d.colliders.Count == 0)
            {
                var col = body.AddComponent<BoxCollider>();
                col.center = mesh.bounds.center; col.size = mesh.bounds.size;
            }
            else
            {
                var cols = new GameObject("Colliders").transform;
                cols.SetParent(root.transform, false);
                foreach (var b in d.colliders)
                {
                    bool roof = d.interior != null && b.yMin >= d.interior.ceilingY;
                    var c = new GameObject(roof ? "RoofCollider" : "Collider").AddComponent<BoxCollider>();
                    c.transform.SetParent(cols, false);
                    c.center = VoxelBoxCenter(b); c.size = (Vector3)b.size * S;
                }
            }
            foreach (var mv in d.movable)
            {
                var go = new GameObject(mv.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = (Vector3)mv.pivot * S;
                go.transform.localRotation = Quaternion.Euler(mv.euler);
                var mm = SaveMesh(VoxelMesher.Build(mv.grid, d.name + "_" + mv.name), mv.name + "_" + d.name);
                go.GetComponent<MeshFilter>().sharedMesh = mm;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                var bc = go.AddComponent<BoxCollider>(); bc.center = mm.bounds.center; bc.size = mm.bounds.size;
            }
            foreach (var sp in d.spinners)
            {
                var go = new GameObject(sp.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = (Vector3)sp.pivot * S;
                go.GetComponent<MeshFilter>().sharedMesh = SaveMesh(VoxelMesher.Build(sp.grid, d.name + "_" + sp.name), sp.name + "_" + d.name);
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            if (d.name.StartsWith("CarTrailer")) root.AddComponent<TrailerDeck>();
            if (d.legs != null && d.legs.Count > 0)
            {
                var legs = new GameObject("Legs", typeof(MeshFilter), typeof(MeshRenderer));
                legs.transform.SetParent(body.transform, false);
                var lm = SaveMesh(VoxelMesher.Build(d.legs, d.name + "_Legs"), "Legs_" + d.name);
                legs.GetComponent<MeshFilter>().sharedMesh = lm;
                legs.GetComponent<MeshRenderer>().sharedMaterial = mat;
                var lc = legs.AddComponent<BoxCollider>(); lc.center = lm.bounds.center; lc.size = lm.bounds.size;
            }
            if (d.roof != null && d.roof.Count > 0)
            {
                var roofGo = new GameObject("Roof", typeof(MeshFilter), typeof(MeshRenderer));
                roofGo.transform.SetParent(body.transform, false);
                roofGo.GetComponent<MeshFilter>().sharedMesh = SaveMesh(VoxelMesher.Build(d.roof, d.name + "_Roof"), "Roof_" + d.name);
                roofGo.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            if (d.hitch.HasValue) AddPoint(root.transform, "Hitch", d.hitch.Value);
            if (d.coupler.HasValue) AddPoint(root.transform, "Coupler", d.coupler.Value);

            if (d.passenger.HasValue) AddPoint(root.transform, "PassengerEye", d.passenger.Value);
            var eye = new GameObject("DriverEye").transform;
            eye.SetParent(root.transform, false);
            eye.localPosition = (Vector3)d.eye * S;

            if (d.driver != null && d.driver.Count > 0)
            {
                var drv = new GameObject("Driver", typeof(MeshFilter), typeof(MeshRenderer));
                drv.transform.SetParent(body.transform, false);
                drv.GetComponent<MeshFilter>().sharedMesh = SaveMesh(VoxelMesher.Build(d.driver, d.name + "_Driver"), "Driver_" + d.name);
                drv.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            if (d.glass != null && d.glass.Count > 0)
            {
                var glass = new GameObject("Glass", typeof(MeshFilter), typeof(MeshRenderer));
                glass.transform.SetParent(body.transform, false);
                glass.GetComponent<MeshFilter>().sharedMesh = SaveMesh(VoxelMesher.Build(d.glass, d.name + "_Glass"), "Glass_" + d.name);
                glass.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            var sockets = new GameObject("Sockets").transform;
            sockets.SetParent(root.transform, false);
            foreach (var s in d.sockets)
            {
                var sg = new GameObject(s.name).transform;
                sg.SetParent(sockets, false);
                sg.localPosition = (Vector3)s.position * S;
                var ms = sg.gameObject.AddComponent<MountSocket>();
                ms.accepts = s.accepts; ms.maxSizeClass = s.maxSizeClass;
                ms.SetMirrored(s.mirrored);
                if (s.part != null && parts.TryGetValue(s.part, out var prefab))
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (!ms.Attach(inst.GetComponent<VehiclePart>()))
                        Debug.LogWarning($"[MadMax] {s.part} rejected by socket {d.name}/{s.name}");
                }
            }
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = chassis.TotalMass;
            rb.angularDamping = 0.3f;
            var driver = root.AddComponent<VehicleDriver>();
            driver.drive = d.drive; driver.travel = d.travel; driver.finalDrive = d.finalDrive;
            driver.rideHeight = 0f; driver.archLift = d.archLift > 0f ? d.archLift : 99f;
            driver.frequency = d.frequency; driver.brakeForce = d.brakeForce; driver.maxSteer = d.maxSteer; driver.driveable = d.driveable;
            if (d.gears != null) driver.gears = d.gears;
            driver.awdSelectable = d.awdSelectable; driver.hasDiffLock = d.diffLock;
            driver.comOffsetX = d.comX;
            if (d.com.HasValue) { driver.customCom = true; driver.centerOfMass = d.com.Value; }
            if (d.aircraft != null)
            {
                var fm = root.AddComponent<FlightModel>();
                fm.kind = d.aircraft == "gyro" ? FlightModel.Kind.Gyro : FlightModel.Kind.Trike;
                fm.maxThrust = d.aircraft == "gyro" ? 1600f : 1300f;
                var prop = d.spinners.Find(sp => sp.name == "Prop");
                if (prop.grid != null) fm.propAt = (Vector3)prop.pivot * S;
            }
            if (d.boat != null)
            {
                // watercraft (user additions): the hull floats and the prop pushes; the Eel also dives
                var bm = root.AddComponent<BoatModel>();
                bm.kind = d.boat == "raft" ? BoatModel.Kind.Raft : d.boat == "skiff" ? BoatModel.Kind.Skiff : d.boat == "trawler" ? BoatModel.Kind.Trawler : d.boat == "houseboat" ? BoatModel.Kind.Houseboat : BoatModel.Kind.Submarine;
                bm.hull = d.boatHull; bm.draft = d.boatDraft; bm.maxThrust = d.boatThrust; bm.rudder = d.boatRudder;
                int minY = int.MaxValue; foreach (var k in d.body.voxels.Keys) minY = Mathf.Min(minY, k.y);
                bm.keelY = minY * S;
                var bprop = d.spinners.Find(sp => sp.name == "Prop");
                var es = d.sockets.Find(sk => sk.accepts == PartCategory.Engine);
                bm.propAt = bprop.grid != null ? (Vector3)bprop.pivot * S : es != null ? ((Vector3)es.position + new Vector3(0f, -13f, -8f)) * S : Vector3.zero;
                bm.deckAt = (Vector3)d.boatDeck * S;
                if (d.boat == "sub") root.AddComponent<Submarine>();
                if (d.boat == "trawler") root.AddComponent<TrawlNet>();
            }
            if (d.bike) { var bb = root.AddComponent<BikeBalance>(); bb.sidecar = d.sidecar; bb.maxLean = d.name == "Chopper" ? 36f : d.name == "Bicycle" ? 34f : 44f; }
            var sys = root.AddComponent<VehicleSystems>();
            sys.fuelCapacity = d.fuelL; sys.oilCapacity = d.oilL; sys.coolantCapacity = d.coolantL;
            sys.usesCoolant = d.usesCoolant; sys.oilInFuel = d.oilInFuel;
            sys.fuel = d.driveable ? d.fuelL * 0.6f : d.fuelL * 0.75f; sys.oil = d.oilL * 0.9f; sys.coolant = d.coolantL * 0.9f;
            if (d.driveable) root.AddComponent<VehicleDashboard>();
            root.AddComponent<VehicleDamage>();
            if (d.machine != null) root.AddComponent<Machine>().kind = (Machine.Kind)System.Enum.Parse(typeof(Machine.Kind), d.machine);
            if (d.coupler.HasValue) root.AddComponent<TowCoupling>();
            if (d.pumpLps > 0f) root.AddComponent<FuelTanker>().flowLps = d.pumpLps;
            if (d.cargoKg > 0f) { var hold = root.AddComponent<MadMax.Building.Container>(); hold.title = "CARGO HOLD"; hold.capacity = d.cargoKg; }
            if (d.interior != null)
            {
                var i = d.interior;
                var space = root.AddComponent<InteriorSpace>();
                space.floorY = i.floorY * S; space.ceilingY = i.ceilingY * S;
                space.min = i.min * S; space.max = i.max * S;
                space.doors = i.doors.ConvertAll(x => new InteriorSpace.Door { inside = new Vector3(x.inside.x, i.floorY, x.inside.y) * S, outside = x.outside * S }).ToArray();
                space.seat = new Vector3(i.seat.x, i.floorY, i.seat.y) * S;
                space.stand = new Vector3(i.stand.x, i.floorY, i.stand.y) * S;
                space.obstacles = i.obstacles.ConvertAll(b => new Bounds(VoxelBoxCenter(b), (Vector3)b.size * S)).ToArray();
                space.furnishings = i.furniture.ConvertAll(x => new InteriorSpace.Furnishing { id = x.id, position = x.pos * S, euler = x.euler }).ToArray();
                space.furnitureMaterial = mat;
                space.airtight = d.airtight;
                if (d.medical) root.AddComponent<MedicalBay>();
            }
            var result = PrefabUtility.SaveAsPrefabAsset(root, $"{VehicleDir}/{d.name}.prefab");
            Object.DestroyImmediate(root);
            return result;
        }

        static Vector3 VoxelBoxCenter(BoundsInt b) => ((Vector3)b.min + ((Vector3)b.size - Vector3.one) * 0.5f) * S;

        static void AddPoint(Transform root, string name, Vector3Int voxel)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = (Vector3)voxel * S;
        }

        static Mesh SaveMesh(Mesh mesh, string assetName)
        {
            string path = $"{MeshDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = assetName;
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            mesh.name = assetName;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material Material(string name, float unlit)
        {
            string path = $"{MatDir}/{name}.mat";
            Directory.CreateDirectory(MatDir);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("MadMax/PixelVoxel"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Unlit", unlit);
            if (unlit > 0) m.SetFloat("_OutlinePx", 0);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Render the pixel camera's low-res target and save an upscaled PNG (for review / MCP screenshots).</summary>
        [MenuItem("MadMax/Capture Pixel Screenshot")]
        public static string Capture()
        {
            var pc = Object.FindAnyObjectByType<PixelArtCamera>();
            if (!pc) return null;
            pc.Refresh();
            var cam = pc.GetComponent<Camera>();
            cam.Render();
            var rt = pc.Output;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            const int k = 3;
            var big = new Texture2D(rt.width * k, rt.height * k, TextureFormat.RGB24, false);
            var hud = Object.FindAnyObjectByType<MadMax.Game.PixelHud>();
            if (hud && hud.Texture && hud.Texture.width == tex.width && hud.Texture.height == tex.height)
            {
                var basePx = tex.GetPixels32(); var over = hud.Texture.GetPixels32();
                for (int i = 0; i < basePx.Length; i++) basePx[i] = Color32.Lerp(basePx[i], over[i], over[i].a / 255f);
                tex.SetPixels32(basePx);
            }
            var src = tex.GetPixels32();
            var dst = new Color32[big.width * big.height];
            for (int y = 0; y < big.height; y++)
            for (int x = 0; x < big.width; x++)
                dst[y * big.width + x] = src[(y / k) * rt.width + x / k];
            big.SetPixels32(dst);
            string path = Path.GetFullPath("Assets/../Temp/madmax_capture.png");
            File.WriteAllBytes(path, big.EncodeToPNG());
            Object.DestroyImmediate(tex); Object.DestroyImmediate(big);
            Debug.Log("[MadMax] Capture saved: " + path);
            return path;
        }
    }
}
