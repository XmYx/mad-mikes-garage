using System.Collections.Generic;
using MadMax.Building;
using MadMax.Items;
using MadMax.Net;
using MadMax.RPG;
using MadMax.Vehicles;
using MadMax.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MadMax.Game
{
    /// <summary>Game bootstrap and input router: world, fleet, trailers, wrecks, the on-foot player, HUD.
    /// Interaction (enter/exit, parts, towing) lives in WastelandGame.Interact.cs, wrecks in WastelandGame.Wrecks.cs.</summary>
    [DefaultExecutionOrder(-100)]
    public partial class WastelandGame : MonoBehaviour
    {
        public int seed = 7;
        public Material terrainMaterial;
        public Material propMaterial;
        [Tooltip("Player's starting vehicles (Tab cycles them).")]
        public GameObject[] vehiclePrefabs;
        public GameObject[] trailerPrefabs;
        [Tooltip("Every part prefab (crafting output, save/load).")]
        public GameObject[] partPrefabs;
        public CameraRig cameraRig;
        public Light sun;
        public float viewRadius = 72f;
        public float enterDistance = 2.2f;
        public int wreckCount = 36;
        [Tooltip("URP shadow distance; must exceed the farthest camera-to-focus distance (iso 60 m, tilt-shift up to 160 m).")]
        public float shadowDistance = 190f;

        /// <summary>When true, device input is ignored for the current vehicle/player (automation writes inputs directly).</summary>
        public static bool ExternalInput;
        public static WastelandGame Instance { get; private set; }

        // Enter Play Mode Options may skip domain reload; statics must be reset explicitly.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ExternalInput = false; VehiclePart.Registry.Clear(); }

        public WorldGen World { get; private set; }
        /// <summary>All driveable vehicles (fleet + wrecks).</summary>
        public IReadOnlyList<VehicleDriver> Cars => cars;
        public IReadOnlyList<VehicleDriver> Fleet => fleet;
        public IReadOnlyList<VehicleDriver> Trailers => trailers;
        public IReadOnlyList<VehicleDriver> Wrecks => wrecks;
        public IReadOnlyList<VehicleDriver> AllVehicles => vehicles;
        public Vector3 WorldSpawn { get; private set; }
        /// <summary>World built and the first frame is set up (loading screens wait for this).</summary>
        public bool Ready { get; private set; }
        public Vector3 SpawnDir { get; private set; } = Vector3.forward;
        public bool Dedicated { get; private set; }
        ushort nextVehicleId = 1;
        public VehicleDriver Current { get; private set; }
        public PlayerCharacter Player { get; private set; }
        public VehicleDriver NearbyVehicle { get; private set; }
        public bool ShowHelp { get; private set; } = true;
        public Inventory Inventory { get; } = new Inventory();
        public BuildMode Build { get; private set; }
        public MenuSystem Menus { get; private set; }
        public CharacterStats Stats { get; private set; } = new CharacterStats();
        public GameRules Rules { get; private set; } = new GameRules();
        public PlayerVitals Vitals { get; private set; }
        public string ToastText => Time.unscaledTime < toastUntil ? toastText : null;

        string toastText;
        float toastUntil;
        bool played;
        readonly Dictionary<string, GameObject> partLookup = new Dictionary<string, GameObject>();

        public void Toast(string text) { toastText = text; toastUntil = Time.unscaledTime + 2.5f; }

        readonly List<VehicleDriver> cars = new List<VehicleDriver>();
        readonly List<VehicleDriver> fleet = new List<VehicleDriver>();
        readonly List<VehicleDriver> trailers = new List<VehicleDriver>();
        readonly List<VehicleDriver> wrecks = new List<VehicleDriver>();
        readonly List<VehicleDriver> vehicles = new List<VehicleDriver>();   // everything with a VehicleDriver
        DeformableTerrain terrain;
        PickupSystem pickups;
        float helpUntil;

        void Start()
        {
            Instance = this;
            Time.fixedDeltaTime = 0.01f;
            Time.timeScale = 1f;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.shadowDistance = shadowDistance;
            if (partPrefabs != null) foreach (var pp in partPrefabs) if (pp) partLookup[pp.name] = pp;
            var partList = new List<(string, PartCategory, float, int)>();
            foreach (var kv in partLookup) if (kv.Value.TryGetComponent<VehiclePart>(out var vp)) partList.Add((kv.Key, vp.category, vp.mass, vp.sizeClass));
            RecipeLibrary.RegisterParts(partList);
            var vehList = new List<(string, float)>();
            foreach (var pf in vehiclePrefabs) if (pf && pf.TryGetComponent<VehicleChassis>(out var ch)) vehList.Add((pf.name, ch.TotalMass));
            RecipeLibrary.RegisterVehicles(vehList);
            var pending = SaveSystem.Pending;
            SaveSystem.Pending = null;
            var net = NetSession.Instance;
            bool joining = net && net.IsClient && NetSession.JoinWorld != null && pending == NetSession.JoinWorld;
            Dedicated = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-server") >= 0 || (net && net.Mode == NetMode.Dedicated);
            // rules: new-game setup > saved world > defaults
            if (SaveSystem.PendingRules != null) { Rules = SaveSystem.PendingRules.Clone(); if (Rules.randomSeed) Rules.seed = Random.Range(1, 99999); seed = Rules.seed; }
            else if (pending != null && pending.rules != null) Rules = pending.rules;
            else Rules.seed = seed;
            if (pending != null) seed = pending.seed;
            GameRules.Current = Rules;
            Weather.Configure(Rules.weather, Rules.season, Rules.snow);
            wreckCount = Rules.wrecks;
            Stats = SaveSystem.PendingCharacter ?? (pending != null && pending.stats != null && !joining ? pending.stats : new CharacterStats());
            if (SaveSystem.PendingCharacter != null) Stats.ApplyTraitStart();
            Stats.learningSpeed = Rules.learning;
            CharacterStats.Notice -= Toast; CharacterStats.Notice += Toast;
            Inventory.Changed += SyncHotbar;
            WheelStats.Popped_ -= OnTyrePop; WheelStats.Popped_ += OnTyrePop;
            viewRadius = GameSettings.Current.ViewRadius;
            VehicleDamage.Scrapped += OnScrapped;
            World = new WorldGen(seed);
            terrain = new GameObject("Terrain").AddComponent<DeformableTerrain>();
            terrain.viewRadius = viewRadius;
            terrain.Init(World, terrainMaterial, propMaterial);
            var weather = new GameObject("Weather").AddComponent<Weather>();
            var fx = new GameObject("WorldFx");
            fx.AddComponent<DebrisSystem>().Init(propMaterial);
            pickups = fx.AddComponent<PickupSystem>();
            pickups.Init(propMaterial, Inventory);

            World.roads.SpawnPoint(out var p, out var dir);
            WorldSpawn = p;
            SpawnDir = dir;
            Player = PlayerCharacter.Create(propMaterial, SaveSystem.PendingLook);
            Vitals = Player.gameObject.AddComponent<PlayerVitals>();
            Stats.EnsureArrays();
            Vitals.Init(Stats);
            Player.gameObject.SetActive(false);
            Build = gameObject.AddComponent<BuildMode>();
            Menus = gameObject.AddComponent<MenuSystem>();
            gameObject.AddComponent<MadMax.Audio.RadioNetwork>();
            gameObject.AddComponent<MadMax.Audio.AmbientAudio>();
            gameObject.AddComponent<MadMax.World.Atmosphere>();
            gameObject.AddComponent<MadMax.World.WindDust>();
            gameObject.AddComponent<MadMax.Npc.NpcDirector>().Init(this);
            Menus.Init(this);
            if (cameraRig) Build.Init(this, cameraRig, propMaterial);

            if (pending != null)
            {
                RestoreDestruction(pending);
                var focus = joining ? NetSession.JoinSpawn : pending.player.position;
                if (!joining && pending.player.vehicle >= 0 && pending.player.vehicle < pending.vehicles.Count) focus = pending.vehicles[pending.player.vehicle].position;
                terrain.BuildAllNow(focus);
                Physics.SyncTransforms();
                RestoreVehicles(pending);
            }
            else
            {
                terrain.BuildAllNow(p);
                Physics.SyncTransforms();
                SpawnFleet(p, dir);
                SpawnWrecks(p);
                GiveStartingKit(Rules.startingKit);
            }
            Player.Equip(ToolLibrary.Create(ItemIds.Sledgehammer, propMaterial));

            if (cameraRig)
            {
                weather.Init(cameraRig.pixel.transform, sun);
                InitSurvival();
                gameObject.AddComponent<LineOfSight>();
                gameObject.AddComponent<PixelHud>().Init(this, cameraRig);
            }
            if (joining) { JoinAsClient(); played = true; }
            else if (pending != null) { RestorePlayer(pending); played = true; Toast("GAME LOADED"); }
            else if (fleet.Count == 0 && !Dedicated) { Player.gameObject.SetActive(true); var sp = p + Vector3.Cross(Vector3.up, dir) * 6f; sp.y = terrain.Height(sp.x, sp.z) + 0.1f; Player.Teleport(sp, 0f); terrain.focus = Player.transform; if (cameraRig) cameraRig.SetTarget(Player.transform); }
            else if (!Dedicated) Enter(fleet[0]);
            GameSettings.Current.Apply(this);
            if (Dedicated) StartDedicated();
            else if (pending == null && !SaveSystem.SkipMenu && LaunchOptions.NoMenu)
            {
                // --no-menu: play the freshly generated world right away (--continue: load the save instead)
                played = true;
                if (LaunchOptions.Continue && SaveSystem.HasSave) LoadGame();
            }
            else if (pending == null && !SaveSystem.SkipMenu)
            {
                // the neon sign + logo + burnout is the menu backdrop: always shown; INTRO off only skips the film/flyover
                if (!GameSettings.Current.intro || LaunchOptions.NoIntro) TitleSequence.SkipToFinale = true;
                if (cameraRig) TitleSequence.Begin(this); else Menus.Open(MenuSystem.Page.Main);
            }
            else played = true;
            if (net && net.Online && !Dedicated) net.AttachGame(this);
            if (SaveSystem.PendingHost) { SaveSystem.PendingHost = false; Host(); }
            SaveSystem.PendingRules = null; SaveSystem.PendingCharacter = null; SaveSystem.PendingLook = null;
            SaveSystem.SkipMenu = false;
            helpUntil = Time.time + 12f;
            Ready = true;
        }

        void OnTyrePop(WheelStats w)
        {
            if (!this || !w) return;
            if (Current && w.transform.IsChildOf(Current.transform)) { Toast("TYRE BLOWOUT!"); if (cameraRig) cameraRig.Shake(5f); }
        }

        void GiveStartingKit(int kit)
        {
            Inventory.AddItem(ItemIds.Sledgehammer);
            Inventory.AddItem(ItemIds.Canteen);
            Inventory.Add(ResourceType.Water, 3);
            Inventory.AddItem("food_can", 2);
            if (kit >= 1)
            {
                Inventory.AddItem("tool_shovel");
                Inventory.AddItem("seed_corn", 3); Inventory.AddItem("seed_potato", 3);
                Inventory.AddItem(ItemIds.ClawHammer);
                Inventory.Add(ResourceType.Scrap, 20); Inventory.Add(ResourceType.Wood, 10); Inventory.Add(ResourceType.Rubber, 4);
                Inventory.Add(ResourceType.Cloth, 2); Inventory.Add(ResourceType.Fuel, 20); Inventory.Add(ResourceType.Oil, 2); Inventory.Add(ResourceType.Coolant, 5);
                Inventory.AddItem("book_mechanics_1");
            }
            if (kit >= 2)
            {
                Inventory.AddItem(ItemIds.Wrench); Inventory.AddItem(ItemIds.Cutter);
                Inventory.Add(ResourceType.Scrap, 80); Inventory.Add(ResourceType.Wood, 40); Inventory.Add(ResourceType.Glass, 10); Inventory.Add(ResourceType.Rubber, 16);
                Inventory.AddItem("vhs_driving"); Inventory.AddItem("book_builder");
            }
            foreach (var c in ClothingLibrary.Starter) Inventory.AddItem("cloth_" + c);
            Inventory.AddItem("cloth_jeans"); Inventory.AddItem("cloth_goggles");
        }

        /// <summary>Death: respawn at the fleet with half health, or back to the menu with permadeath.</summary>
        public void PlayerDied(string cause)
        {
            if (dying) return;
            dying = true;
            Toast("YOU DIED: " + cause);
            if (Current) Exit();
            if (Player.Carried) Player.DropCarried();
            Player.StandUp();
            // go limp where you fell; the camera stays on the body for a moment
            var cc = Player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            var push = (-Player.transform.forward * (cause == "CRASH" ? 160f : 70f) + Vector3.up * 30f);
            Ragdoll.For(Player.Rig).Go(push, Player.transform.position + Vector3.up * 1.2f, Player.Velocity);
            if (Rules.permadeath)
            {
                if (SaveSystem.HasSave) System.IO.File.Delete(SaveSystem.Path);
                Invoke(nameof(ReturnToMainMenu), 4f);
                return;
            }
            Invoke(nameof(Respawn), 4f);
        }

        bool dying;

        void Respawn()
        {
            dying = false;
            var rd = Player.GetComponent<Ragdoll>();
            if (rd) rd.Restore();
            var cc = Player.GetComponent<CharacterController>();
            if (cc) cc.enabled = true;
            var home = fleet.Count > 0 && fleet[0] ? fleet[0] : null;
            var at = spawnPoint ?? (home ? ExitPoint(home) : WorldSpawn);
            Stats.hunger = Mathf.Max(Stats.hunger, 50f); Stats.thirst = Mathf.Max(Stats.thirst, 50f); Stats.sick = 0f;
            if (Player.Interior) Player.ExitInterior(at); else Player.Teleport(at, 0f);
            Stats.health = Stats.MaxHealth * 0.5f;
            Stats.stamina = Stats.MaxStamina;
        }

        void OnDestroy()
        {
            CharacterStats.Notice -= Toast;
            VehicleDamage.Scrapped -= OnScrapped;
            if (Instance == this) Instance = null;
        }

        void OnScrapped(VehicleDriver v)
        {
            if (v == Current) Exit();
            cars.Remove(v); fleet.Remove(v); trailers.Remove(v); wrecks.Remove(v); vehicles.Remove(v);
            Toast("STRIPPED TO THE FRAME");
        }

        /// <summary>Craft at a bench: pay inputs, then add the item/resource or spawn the part on the bench.</summary>
        readonly List<Container> craftPool = new List<Container>();

        /// <summary>Resources and items available for crafting: the player's pockets plus storage within 5 m of the station.</summary>
        List<Inventory> CraftSources(CraftingStation station)
        {
            var list = new List<Inventory> { Inventory };
            if (station) { Container.Near(station.transform.position, 5f, craftPool); foreach (var c in craftPool) list.Add(c.inventory); }
            return list;
        }

        int CountRes(List<Inventory> src, ResourceType t) { int n = 0; foreach (var i in src) n += i.Get(t); return n; }
        int CountItem(List<Inventory> src, string id) { int n = 0; foreach (var i in src) n += i.GetItem(id); return n; }

        public bool CanCraft(Recipe r, CraftingStation station)
        {
            var src = CraftSources(station);
            foreach (var (t, n) in r.resources) if (t != ResourceType.None && CountRes(src, t) < RecipeLibrary.Amount(n)) return false;
            foreach (var (i, n) in r.items) if (CountItem(src, i) < n) return false;
            if (r.fuel != ResourceType.None && CountRes(src, r.fuel) < r.fuelAmount && !(r.fuel == ResourceType.Wood && CountRes(src, ResourceType.Charcoal) >= r.fuelAmount)) return false;
            return true;
        }

        void PayFrom(List<Inventory> src, ResourceType t, int n) { foreach (var i in src) { int take = Mathf.Min(n, i.Get(t)); if (take > 0) { i.TrySpend(t, take); n -= take; } if (n <= 0) return; } }
        void TakeFrom(List<Inventory> src, string id, int n) { foreach (var i in src) { int take = Mathf.Min(n, i.GetItem(id)); if (take > 0) { i.TakeItem(id, take); n -= take; } if (n <= 0) return; } }

        public void Craft(Recipe r, CraftingStation station)
        {
            if (!Stats.Knows(RecipeLibrary.KnowledgeFor(r))) { Toast("UNKNOWN RECIPE: READ, WATCH OR RESEARCH"); return; }
            if (station && !station.Powered) { Toast("NO POWER"); return; }
            if (!CanCraft(r, station)) { Toast("MISSING MATERIALS"); return; }
            var src = CraftSources(station);
            foreach (var (t, n) in r.resources) if (t != ResourceType.None) PayFrom(src, t, RecipeLibrary.Amount(n));
            foreach (var (i, n) in r.items) TakeFrom(src, i, n);
            if (r.fuel != ResourceType.None)
            {
                if (CountRes(src, r.fuel) >= r.fuelAmount) PayFrom(src, r.fuel, r.fuelAmount);
                else PayFrom(src, ResourceType.Charcoal, r.fuelAmount);
            }
            switch (r.kind)
            {
                case OutputKind.Item: Inventory.AddItem(r.output, r.amount); break;
                case OutputKind.Resource: Inventory.Add(r.outputResource, r.amount); break;
                case OutputKind.Part:
                {
                    var at = station ? station.OutputPoint : Player.transform.position + Vector3.up;
                    var part = SpawnPart(r.output, at, station ? station.transform.rotation : Quaternion.identity);
                    if (part) { var rb = part.gameObject.AddComponent<Rigidbody>(); rb.mass = part.mass; MadMax.Net.NetSession.Instance?.SendLooseSpawn(part); }
                    break;
                }
                case OutputKind.Vehicle:
                {
                    var prefab = PrefabFor(r.output);
                    if (!prefab) break;
                    var at = station ? station.OutputPoint : Player.transform.position + Player.transform.forward * 5f;
                    var v = Instantiate(prefab, at + Vector3.up * 0.6f, station ? station.transform.rotation : Quaternion.identity).GetComponent<VehicleDriver>();
                    Register(v, fleet);
                    if (v.TryGetComponent<VehicleSystems>(out var vs)) { vs.fuel = 5f; vs.oil = vs.oil * 0.5f; }
                    MadMax.Net.NetSession.Instance?.SendVehicleSpawn(v, r.output);
                    break;
                }
            }
            if (r.byproducts != null) foreach (var (t, n) in r.byproducts) Inventory.Add(t, n);
            if (DebrisSystem.Instance && station)
                for (int i = 0; i < 6; i++) DebrisSystem.Instance.EmitPuff(station.OutputPoint, new Color32(255, 220, 120, 255), 0.03f, Random.insideUnitSphere * 2f + Vector3.up, 0.3f);
            Stats.Practice(r.category == RecipeCategory.Cooking || r.category == RecipeCategory.Farming ? Skill.Farming : Skill.Crafting, 4f + r.resources.Length * 1.5f);
            Toast("CRAFTED " + r.name);
        }

        // ------------------------------------------------------------------ network
        void StartDedicated()
        {
            ushort port = NetSession.DefaultPort;
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-port");
            if (i >= 0 && i + 1 < args.Length) ushort.TryParse(args[i + 1], out port);
            Player.gameObject.SetActive(false);
            if (cameraRig) cameraRig.gameObject.SetActive(false);
            Application.targetFrameRate = 60;
            if (!NetSession.Instance || NetSession.Instance.Mode != NetMode.Dedicated) NetSession.StartServer(this, port, true);
            else NetSession.Instance.AttachGame(this);
        }

        /// <summary>Open this world to the network (listen server).</summary>
        public bool Host(ushort port = NetSession.DefaultPort)
        {
            if (NetSession.Instance && NetSession.Instance.Online) return true;
            bool ok = NetSession.StartServer(this, port, false);
            Toast(ok ? $"HOSTING ON PORT {port}" : "COULD NOT HOST");
            if (ok) foreach (var v in vehicles) if (v == Current) v.owner = NetSession.HostPlayerId;
            return ok;
        }

        public void Join(string address, ushort port, string name)
        {
            NetSession.StartClient(address, port, name);
            Toast("CONNECTING...");
        }

        void JoinAsClient()
        {
            Player.gameObject.SetActive(true);
            var spawn = NetSession.JoinSpawn;
            spawn.y = terrain.Height(spawn.x, spawn.z) + 0.1f;
            Player.Teleport(spawn, 0f);
            terrain.focus = Player.transform;
            if (cameraRig) cameraRig.SetTarget(Player.transform);
            // a fresh character: starter kit, the host keeps the shared world and its own stock
            Inventory.AddItem(ItemIds.Sledgehammer);
            Inventory.AddItem(ItemIds.ClawHammer);
            foreach (var c in ClothingLibrary.Starter) Inventory.AddItem("cloth_" + c);
            Inventory.Add(ResourceType.Scrap, 20); Inventory.Add(ResourceType.Wood, 10);
        }

        /// <summary>Server keeps terrain colliders and awake bodies around every connected player.</summary>
        void UpdateServerFoci()
        {
            var net = NetSession.Instance;
            if (!net || !net.IsServer) return;
            terrain.extraFoci.Clear();
            foreach (var f in net.RemoteFoci()) terrain.extraFoci.Add(f);
            if (Dedicated && terrain.extraFoci.Count > 0) terrain.focus = terrain.extraFoci[0];
        }

        /// <summary>Server granted this client the vehicle it asked for.</summary>
        public void EnterGranted(VehicleDriver v) => EnterLocal(v);

        /// <summary>Owned tools in hotkey order (1..6 on foot).</summary>
        public List<string> OwnedTools()
        {
            var l = new List<string>();
            foreach (var id in ToolLibrary.Order) if (Inventory.GetItem(id) > 0) l.Add(id);
            return l;
        }

        void SpawnFleet(Vector3 p, Vector3 dir)
        {
            var side = Vector3.Cross(Vector3.up, dir);
            float along = 0f;
            // starting fleet by rule: everything, a single vehicle, or nothing (on foot)
            var chosen = new List<GameObject>();
            foreach (var pf in vehiclePrefabs)
            {
                if (pf.GetComponent<Machine>()) continue;
                if (Rules.fleet == 0 || (Rules.fleet == 1 && pf.name == "Scavenger") || (Rules.fleet == 2 && pf.name == "Trabant")) chosen.Add(pf);
            }
            for (int i = 0; i < chosen.Count; i++)
            {
                var half = HalfExtents(chosen[i]);
                along += half.z;
                var pos = FindClearSpot(p + side * (i % 2 == 0 ? 2f : -2f) + dir * along, dir, half);
                along += half.z + 1.5f;
                Register(Instantiate(chosen[i], pos, Quaternion.LookRotation(dir)).GetComponent<VehicleDriver>(), fleet);
            }
            // construction yard: the machines parked in a row off the road (full fleet only)
            if (Rules.fleet == 0)
            {
                var yard = p + side * 22f + dir * 10f;
                int mi = 0;
                foreach (var pf in vehiclePrefabs)
                {
                    if (!pf.GetComponent<Machine>()) continue;
                    var half = HalfExtents(pf);
                    var pos = FindClearSpot(yard + dir * (mi * 7f), side, half);
                    Register(Instantiate(pf, pos, Quaternion.LookRotation(side)).GetComponent<VehicleDriver>(), fleet);
                    mi++;
                }
            }
            // trailers wait on the shoulder behind the convoy
            for (int i = 0; i < trailerPrefabs.Length && Rules.fleet == 0; i++)
            {
                var half = HalfExtents(trailerPrefabs[i]);
                var pos = FindClearSpot(p - dir * (8f + i * 16f) + side * 6.5f, -dir, half);
                Register(Instantiate(trailerPrefabs[i], pos, Quaternion.LookRotation(dir)).GetComponent<VehicleDriver>(), null);
            }
        }

        void Register(VehicleDriver v, List<VehicleDriver> group)
        {
            if (!v.GetComponent<VehicleLights>()) v.gameObject.AddComponent<VehicleLights>();
            if (v.driveable)
            {
                if (!v.GetComponent<Winch>()) v.gameObject.AddComponent<Winch>();
                if (!v.GetComponent<Crane>()) v.gameObject.AddComponent<Crane>();
                if (!v.GetComponent<VehicleClimate>()) v.gameObject.AddComponent<VehicleClimate>();
                if (!v.GetComponent<VehicleWeapons>()) v.gameObject.AddComponent<VehicleWeapons>();
            }
            if (v.netId == 0) v.netId = nextVehicleId++;
            else nextVehicleId = (ushort)Mathf.Max(nextVehicleId, v.netId + 1);
            vehicles.Add(v);
            if (v.driveable) cars.Add(v); else trailers.Add(v);
            group?.Add(v);
        }

        static Vector3 HalfExtents(GameObject prefab)
        {
            var body = prefab.transform.Find("Body");
            var mf = body ? body.GetComponent<MeshFilter>() : null;
            if (!mf || !mf.sharedMesh) return new Vector3(1.3f, 0.9f, 2.8f);
            var e = mf.sharedMesh.bounds.extents;
            return new Vector3(e.x + 0.2f, e.y, e.z + 0.4f);
        }

        /// <summary>First position along <paramref name="dir"/> where a vehicle-sized box is free of props and vehicles,
        /// lifted above the highest terrain point under its footprint.</summary>
        Vector3 FindClearSpot(Vector3 start, Vector3 dir, Vector3 half)
        {
            var rot = Quaternion.LookRotation(dir);
            for (int step = 0; step < 40; step++)
            {
                var pos = start + dir * (step * 3f);
                float top = float.MinValue;
                for (int ix = -1; ix <= 1; ix++)
                for (int iz = -1; iz <= 1; iz++)
                {
                    var q = pos + rot * new Vector3(ix * half.x, 0, iz * half.z);
                    top = Mathf.Max(top, terrain.Height(q.x, q.z));
                }
                pos.y = top + 0.35f;
                bool blocked = false;
                foreach (var h in Physics.OverlapBox(pos + Vector3.up * (half.y + 0.1f), half, rot, ~0, QueryTriggerInteraction.Ignore))
                    if (!h.GetComponent<MeshCollider>() || h.GetComponent<DestructibleVoxels>()) { blocked = true; break; }
                if (!blocked) return pos;
            }
            return start + Vector3.up * 3f;
        }

        void Update()
        {
            if (!Player) return;
            var kb = Keyboard.current; var pad = Gamepad.current; var mouse = Mouse.current;
            bool Pressed(Key k) => (kb != null && kb[k].wasPressedThisFrame) || Injected(k);
            UpdateRadial(kb, mouse);

            if (Dedicated) { UpdateServerFoci(); UpdateSleepers(); return; }
            Menus.Tick();
            var settings = GameSettings.Current;
            foreach (var c in cars)
            {
                if (!c) continue;
                c.SetManual(settings.manualTransmission && !c.aiDriven);
                c.gripMultiplier = c == Current ? Stats.DrivingGrip : 1f;
                if (c.TryGetComponent<VehicleSystems>(out var vs)) vs.fuelMultiplier = Rules.fuelUse * (c == Current ? Stats.FuelEfficiency : 1f);
            }
            if (Menus.IsOpen)
            {
                // hold everything on the handbrake (brake at standstill would engage reverse in automatic)
                foreach (var c in cars) { if (!c) continue; c.steerInput = c.throttleInput = c.brakeInput = 0f; c.handbrake = true; }
                Player.moveInput = Vector2.zero;
                Prompt = null;
                return;
            }

            if (Pressed(Key.H)) { ShowHelp = !ShowHelp; helpUntil = float.MaxValue; }
            if (ShowHelp && Time.time > helpUntil) ShowHelp = false;
            if (Pressed(Key.R) && !(NetSession.Instance && NetSession.Instance.IsClient)) { Weather.Raining = !Weather.Raining; NetSession.Instance?.SendWeather(); Toast(Weather.Raining ? (Weather.Snowing ? "SNOW" : "RAIN") : "CLEAR SKIES"); }
            if ((TabTapped || (pad != null && pad.buttonWest.wasPressedThisFrame)) && fleet.Count > 0)
            {
                int i = fleet.IndexOf(Current);
                if (Player.Interior) Player.ExitInterior(Player.transform.position);
                Enter(fleet[(i + 1) % fleet.Count]);
            }
            RecipeLibrary.CostMult = Stats.CraftCostMult;
            if (Current)
            {
                float v = Mathf.Abs(Current.ForwardSpeed);
                Stats.Practice(Skill.Driving, v * Time.deltaTime * 0.02f * (1f + Current.WheelSlip * 2f + Current.Mud));
                if (Pressed(Key.X)) { Current.ToggleFourWheelDrive(); if (Current.awdSelectable) Toast(Current.FourWheelDrive ? "4WD ENGAGED" : "2WD"); }
                if (Pressed(Key.L)) { Current.ToggleDiffLock(); Toast(Current.hasDiffLock ? (Current.diffLocked ? "DIFF LOCKED" : "DIFF OPEN") : "NO DIFF LOCK ON THIS VEHICLE"); }
                if (Pressed(Key.E) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) Current.ShiftUp();
                if (Pressed(Key.Q) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) Current.ShiftDown();
                if (Pressed(Key.N)) { var vl = Current.GetComponent<VehicleLights>(); if (vl) { vl.mode = (vl.mode + 1) % 3; Toast(VehicleLights.ModeNames[vl.mode]); } }
                if (kb != null && Current.TryGetComponent<Winch>(out var winch)) winch.Control(Pressed(Key.Digit4), kb.digit5Key.isPressed, kb.digit6Key.isPressed);
                if (kb != null && Current.TryGetComponent<Crane>(out var crane)) crane.Control(Pressed(Key.Digit7), kb.digit8Key.isPressed, kb.digit9Key.isPressed, kb.digit0Key.isPressed ? (kb.leftShiftKey.isPressed ? -1f : 1f) : 0f, kb.leftShiftKey.isPressed);
                if (Pressed(Key.K) && Current.TryGetComponent<VehicleClimate>(out var clim)) { clim.on = !clim.on; Toast(clim.on ? "CLIMATE AUTO" : "CLIMATE OFF"); }
                if (Current.TryGetComponent<VehicleWeapons>(out var guns) && !ExternalInput)
                    guns.Control(new WeaponInput
                    {
                        fire = mouse != null && mouse.leftButton.isPressed, firePressed = mouse != null && mouse.leftButton.wasPressedThisFrame,
                        alt = kb != null && kb.leftShiftKey.isPressed, drop = Pressed(Key.B), smoke = Pressed(Key.U),
                        aim = VehicleAim(), dt = Time.deltaTime
                    });
                if (Current.TryGetComponent<Machine>(out var machine) && kb != null)
                    machine.Control(new MachineKeys
                    {
                        h1 = kb.digit1Key.isPressed, h2 = kb.digit2Key.isPressed, h3 = kb.digit3Key.isPressed, h4 = kb.digit4Key.isPressed, h5 = kb.digit5Key.isPressed, h6 = kb.digit6Key.isPressed,
                        p1 = Pressed(Key.Digit1), p2 = Pressed(Key.Digit2), p3 = Pressed(Key.Digit3), p4 = Pressed(Key.Digit4), p5 = Pressed(Key.Digit5), p6 = Pressed(Key.Digit6),
                        shift = kb.leftShiftKey.isPressed
                    });
            }
            {
                // radio: the driven vehicle's head unit, or a radio set the player is looking at
                var radio = Current ? MadMax.Audio.RadioReceiver.On(Current.gameObject) : Focused is MadMax.Building.RadioSet rs ? rs.GetComponent<MadMax.Audio.RadioReceiver>() : null;
                if (radio && !(Build && Build.Active))
                    radio.HandleKeys(Current && Pressed(Key.M), Pressed(Key.Comma) ? -1 : Pressed(Key.Period) ? 1 : 0, Pressed(Key.LeftBracket) ? -1 : Pressed(Key.RightBracket) ? 1 : 0);
            }
            UpdateHotbar(kb, mouse);
            UpdateEncumbrance();
            UpdateLearning();
            UpdateEnvironment();
            UpdateSurvival(Time.deltaTime);
            UpdateRefuel(Time.deltaTime);
            UpdateHealth(Time.deltaTime);
            UpdateClothing(Time.deltaTime);
            if (Pressed(Key.I)) Menus.Open(MenuSystem.Page.Inventory);
            if (Pressed(Key.P)) Menus.Open(MenuSystem.Page.Skills);
            if (Pressed(Key.O)) Menus.Open(MenuSystem.Page.Health);
            UpdateInteraction(kb, pad);
            UpdateServerFoci();
            UpdateSleepers();
            if (Current && (Pressed(Key.T) || (pad != null && pad.selectButton.wasPressedThisFrame)) && !MadMax.Npc.NpcDirector.TryParley()) Current.Recover();
            if (Current && Pressed(Key.Backspace)) DropRandomPart(Current);
            if (Current && Pressed(Key.G) && Current.TryGetComponent<VehicleDamage>(out var dmg)) dmg.Repair();

            Vector2 move = Vector2.zero; bool space = false, shift = false, spaceDown = false;
            if (kb != null)
            {
                move.x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                move.y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
                space = kb.spaceKey.isPressed; spaceDown = kb.spaceKey.wasPressedThisFrame; shift = kb.leftShiftKey.isPressed;
            }
            float throttle = Mathf.Max(0f, move.y), brake = Mathf.Max(0f, -move.y);
            if (pad != null)
            {
                var ls = pad.leftStick.ReadValue();
                if (ls.sqrMagnitude > 0.02f) move = ls;
                throttle = Mathf.Max(throttle, pad.rightTrigger.ReadValue());
                brake = Mathf.Max(brake, pad.leftTrigger.ReadValue());
                space |= pad.buttonSouth.isPressed; spaceDown |= pad.buttonSouth.wasPressedThisFrame;
                shift |= pad.leftStickButton.isPressed;
            }

            if (pickups)
            {
                pickups.collector = Current ? Current.transform : Player.transform;
                pickups.collectRadius = Current ? 3.2f : 1.8f;
            }

            foreach (var c in cars)
            {
                if (!c || c == Current || c.aiDriven) continue;                                // NPC drivers steer themselves
                if (c.TryGetComponent<TowCoupling>(out var towed) && towed.Tower) continue;   // brakes follow the tow vehicle
                c.steerInput = c.throttleInput = c.brakeInput = 0f;
                c.handbrake = true;
            }
            if (!Current && Build) Build.Tick(kb, mouse, pad);
            if (ExternalInput) return;

            if (Current)
            {
                Current.steerInput = Mathf.Clamp(move.x, -1f, 1f);
                Current.throttleInput = throttle;
                Current.brakeInput = brake;
                Current.handbrake = space;
            }
            else
            {
                Player.moveInput = move;
                Player.run = shift;
                if (spaceDown) Player.jump = true;
                bool attack = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame);
                if (attack && cameraRig && !(Build && Build.Active)) Player.Attack(cameraRig.mode == ViewMode.ThirdPerson || cameraRig.mode == ViewMode.FirstPerson);
                if (cameraRig)
                {
                    Player.viewYaw = cameraRig.ViewYaw;
                    Player.lookPitch = cameraRig.LookPitch;
                    Player.faceView = cameraRig.mode == ViewMode.FirstPerson;
                }
            }
        }

        static void DropRandomPart(VehicleDriver car)
        {
            var parts = new List<VehiclePart>(car.GetComponent<VehicleChassis>().Parts);
            if (parts.Count == 0) return;
            var part = parts[Random.Range(0, parts.Count)];
            var dropped = part.Socket.Detach();
            if (dropped && dropped.TryGetComponent<Rigidbody>(out var body))
            {
                body.linearVelocity = car.Body.linearVelocity;
                body.AddForce((part.transform.position - car.transform.position).normalized * 3f + Vector3.up * 2f, ForceMode.VelocityChange);
            }
        }
    }
}
