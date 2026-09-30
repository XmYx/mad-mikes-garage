# Mad Mike's Garage

A pixel-art survival game about cars in a voxel wasteland. You scavenge wrecks for parts, bolt them onto whatever still
runs, and keep yourself alive with what you can find, grow, trade or build. Every vehicle is assembled from swappable
parts, and most of the world can be dug up, knocked down or burnt. Made with Unity 6.

![Title screen](Docs/screenshots/title.png)

---

## What's in the game

### Driving and repairing
- Vehicles are made of parts in sockets: wheels, engines, radiators, exhausts, doors, hoods, bumpers, rams, spikes, armour plates, roof racks, winches, cranes, lights, snorkels and weapons. Most parts fit most vehicles.
- Raycast suspension, open or locked differentials, switchable 4WD, automatic or manual gearbox, launch and hill-start assists.
- Crashes dent the bodywork, bend the frame (the car starts pulling to one side) and knock parts off. Tyres wear, heat up and burst.
- Each engine needs fuel, oil and coolant. Petrol and diesel are separate, and filling up with the wrong one causes a fault until you siphon it out. Engines crank and sometimes refuse to start when they are worn or cold, and old ones leak.
- Servicing: oil changes, air filters, spark plugs. A tuning bench changes gearing, ride height, dampers, brake bias, turbo, supercharger and nitrous.
- A paint station for colours and decals. A gang decal can get you past that gang's convoys.
- Towing with hitches and couplers, fuel tankers you can pump from, and car transporters with a tilting upper deck.
- Construction machines really change the terrain. Excavators, backhoes and dozers dig and push soil, and you can see it in the bucket, in front of the blade and in the tipper bed. Pavers lay asphalt or concrete and rollers finish it.
- Vehicle weapons: roof guns, rear droppers for oil and caltrops, smoke screens.
- Mud, ruts, snow, ice, fords and rain all change how much grip you have.

### The world
- A procedural planet about 9.6 km around. It wraps east to west through an open ocean, and latitude sets the climate, from the wet tropics to tundra and polar ice.
- Deserts, forests, meadows, tropical lakes, swamps and radioactive zones. Rivers, dry wadis, coasts and open sea.
- Villages, towns and ruined cities joined by highways and dirt tracks. You can walk into the buildings, including multi-storey ones, with a cutaway view.
- Bunkers, rock tunnels with caves, airfields with hangars, scrapyards, a refinery, radio masts and military checkpoints.
- Day and night, seasons, rain, snow, storms, lightning, dust devils and wind. Fire spreads through dry country, and puddles and wet roads dry out in the sun.
- Buildings slowly overgrow and crumble. Grass gets flattened where you drive and grows back.

### Surviving
- You create a character with attributes and traits. Skills improve with practice, from books and VHS tapes, and through research at a workbench.
- Hunger, thirst, hygiene, food spoilage, sickness, radiation.
- Body temperature depends on clothing layers, shelter, heaters and getting wet.
- Injuries are tracked per body part (cuts, fractures, burns, bleeding, infection) and treated with bandages, splints and medicine. A bad leg makes you limp and a broken arm stops you using two-handed tools.
- Clothing and body armour wear out, get holes and can be mended.
- Melee tools, guns with magazines and jams, thrown molotovs and smoke, fishing rods, a grappling hook.
- Parkour: vaulting, climbing, sliding and rolling.
- Diving gear with tank air for working underwater.

### Building, farming and industry
- Walls, floors, roofs, doors, stairs and ladders. Foundations and drivable decks, garages, defences (spikes, wire, turrets, alarm bells), and saved blueprints of whole structures.
- Furniture that does something: beds, seats, dining tables, wardrobes, bookshelves, mirrors, TVs, radios, stoves and a latrine.
- Power from generators, solar panels, wind turbines and water wheels, carried by cables. Water from wells, pumps, rain collectors, tanks and pipes, feeding sprinklers and drip lines.
- Gardens with crops, fruit trees, fertiliser, weeds, crows and scarecrows. Greenhouses and seasons matter.
- A production chain: dig soil, wash it for ore, then smelt metals, fire glass and lime, burn charcoal and mix concrete and asphalt. Crude oil comes from pumpjacks and is refined into petrol, diesel and oil.
- Crafting is timed and happens at stations (workbench, stove, furnace, kiln, still, garage, slipway and others). The results come in crude, sturdy or fine quality.
- Sea bases: domes, tunnels, a shore entrance and a docking collar for the submarine.

### People and animals
- Every NPC has a name, a job, a temperament and a history that you learn over several conversations. Many of them speak, in ten Southern voices with over a thousand lines and short two-person chats.
- Shopkeepers, market stalls, roadside vendors, wanderers, trader convoys and raider gangs. Trade uses scrap as money, and prices vary by town, by season and by how much you have sold there. You can haggle.
- Raiders track you down on the road. You can talk your way out, pay, bluff, recruit them or fight.
- Factions with territory and standing, bounty boards, supply and escort contracts, and quests for each town.
- Companions who follow you, ride with you, drive and fight.
- Herds, predators, birds, snakes, scorpions and insects. Livestock can be kept, fed, bred and ridden, and fish and crabs live in the sea.

### Radio
Eleven stations run around the clock: ten music stations with about 250 original tracks, plus WasteTalk FM 90.1 with
DJs, call-in shows, news and a weather report that matches the sky. Every vehicle has a radio, you can build radio sets
for a base, and away from the masts the signal fades into hiss.

### Other
- Multiplayer through a listen server, a dedicated server or as a client (Unity Transport, UDP).
- Three save slots plus an autosave. A map and journal with waypoints and road routing.
- A daily race board (road race, bike trial, air race, arena) and a long quest called The Last Engine.
- Views: isometric, tilt-shift, top-down, third person, first person, hood and bumper cameras. The pixel-art renderer can also switch to full-resolution "vector" mode.
- Rebindable controls, gamepad support, colour-blind palette, HUD scaling and separate volume channels.

| | |
|---|---|
| ![Convoy](Docs/screenshots/convoy.png) | ![Burnout](Docs/screenshots/burnout.png) |
| ![Crane truck lifting a car](Docs/screenshots/crane.png) | ![Construction machines](Docs/screenshots/machines.png) |
| ![Ruined city](Docs/screenshots/city.png) | ![Walk-in buildings with cutaway](Docs/screenshots/city_interior.png) |
| ![Village](Docs/screenshots/village.png) | ![Tropical lake](Docs/screenshots/swimming.png) |
| ![Snow](Docs/screenshots/snow_vehicles.png) | ![Forest fire](Docs/screenshots/forest_fire.png) |
| ![Night base with power](Docs/screenshots/night_base.png) | ![Inventory](Docs/screenshots/inventory.png) |
| ![Health and injuries](Docs/screenshots/health.png) | ![Character creation](Docs/screenshots/character.png) |

---

## Vehicles

| Group | Vehicles |
|---|---|
| Wasteland cars | Interceptor, Scavenger, Trabant, Pickup (4x4), Coupe, Sedan, Wagon (woody), Tow Truck, Wrecker, Dune Buggy, Monster Truck |
| Real cars (modelled in Blender) | Fiat 126p, Fiat 500, Renault 5, Citroën BX, Citroën XM, Citroën Xantia, Lancia Ypsilon, Peugeot 205, Peugeot 206, Peugeot 207 CC, Peugeot 405, Peugeot 406 Break, Fiat Multipla |
| Trucks and buses | Hauler war rig with a walk-in module, Semi tractor, Bus, Ambulance with a medical bay, APC |
| Machines | Excavator, Backhoe loader, Bulldozer, Dump truck, Asphalt/concrete paver, Roller |
| Trailers | Tanker (2000 L), Small bowser (450 L), Cargo trailer, Box trailer, Car transporter (single and double deck) |
| Two wheels | Dirt bike, Chopper, Bicycle, Sidecar outfit |
| Aircraft | Ultralight trike, Gyrocopter |
| Boats | Raft, Skiff, Trawler, Houseboat, Iron Eel submarine |

All of these are built from code in `Assets/MadMax/Runtime/Designs`. The real cars start as Blender lofts made from
the actual dimensions (`tools/blender/cars.py`), which are voxelized and then built like every other car.

---

## Download

Ready-to-play builds are on the **[Releases](../../releases)** page:

| Platform | Installer | Portable |
|---|---|---|
| Windows 10/11 (x64) | `MadMikesGarage-<version>-windows-x64-setup.exe` | `...-windows-x64.zip` |
| Linux (x64) | `MadMikesGarage-<version>-linux-x64.deb` | `...-linux-x64.tar.gz` |
| macOS 11+ (Intel + Apple Silicon) | `MadMikesGarage-<version>-macos-universal.zip` | |

Each release also carries the full source code (zip / tar.gz).

### Making a release (maintainers)
Releases are built by GitHub Actions (`.github/workflows/release.yml`, [GameCI](https://game.ci)) on Linux runners for all three platforms.

1. One-time: add repository secrets (Settings > Secrets and variables > Actions) for the Unity account that holds the licence: `UNITY_EMAIL`, `UNITY_PASSWORD`, and `UNITY_LICENSE` if you have a `.ulf` file (see [GameCI activation](https://game.ci/docs/github/activation)).
2. Push a version tag: `git tag v0.2.0 && git push origin v0.2.0` — or run **Actions > Release > Run workflow** and enter a version.
3. The workflow creates the release, builds Windows / Linux / macOS in parallel, uploads the installers and publishes the release when all three succeed.

Locally, `MadMax > Build Linux Player` (or File > Build) produces the same player; every build shows its version bottom-right on the main menu.

## Getting started

1. Install **Unity 6000.6** with URP.
2. Clone this repository and open the folder in Unity Hub.
3. Menu **MadMax → Build Game Scene** (generates meshes, part and vehicle prefabs, and the scene under `Assets/MadMax/Generated` and `Assets/MadMax/Scenes`).
4. Open `Assets/MadMax/Scenes/Wasteland_Game.unity` and press Play.

Dedicated server: **MadMax → Build Linux Player**, then
`MadMikesGarage.x86_64 -batchmode -nographics -server -port 7777`.

### Controls (short version)
`WASD` drive/walk · `F` enter/exit · `E` use/open/craft · `T` second action · `1-8` hotbar · `I` inventory · `P` skills · `O` health · `B` build (hold for radial menu) · `N` lights · `K` climate · machines `1 2 3` · winch `4 5 6` · crane `7 8 9 0` · `V` camera view · `/` radio (`,` `.` tune, `[` `]` volume) · `M` map and journal · hold `Tab` action wheel · `H` full help.

---

## Contributing

Pull requests are welcome. Some places to start:

| Area | Where | Ideas |
|---|---|---|
| New vehicle parts | `Assets/MadMax/Runtime/Designs/PartLibrary.cs` | bumpers, exhausts, roof racks, armour, engines |
| New vehicles | `Designs/VehicleDesigns.cs`, `CarDesigns.cs`, `ModelCars.cs` + `tools/blender/cars.py` | more real cars, a tracked tank |
| Buildings & furniture | `Building/BuildPieces.cs`, `FurnitureLibrary.cs` | new walls, decor, workshop stations |
| World props & biomes | `World/BiomeProps.cs`, `WorldGen.cs` | new settlement types, landmarks, regional weather |
| Recipes, food, crops | `Items/Recipes.cs`, `Items/FoodLibrary.cs` | cooking, medicine, new crops |
| Systems | `Game/`, `Vehicle/`, `World/`, `Npc/` | quests, NPC behaviour, sound |

**Ground rules:**
- 1 voxel = 8 cm; colours come from the `Pal` palette ramps; call `Bevel()` once after painting.
- Parts are vehicle-agnostic and authored for the right side; sockets mirror them.
- World generation is deterministic from the seed (`System.Random`, never `UnityEngine.Random`).
- New persistent state goes into `SaveData` and both the save and the restore path; gameplay events that others must see go through `NetSession.Send*`.
- Enter Play Mode skips domain reload: reset every static in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` method.
- Keep `FixedUpdate` and per-cell loops allocation-free.

Open a pull request with a screenshot of what you added. Bug reports help most with a save file and the steps that lead to the problem.

The full list of what is done and what is planned lives in [ROADMAP.md](ROADMAP.md).

---

## Credits
- Sound effects: CC0, via the Lots of Sounds free API (see `Assets/MadMax/Resources/Sfx/CREDITS.txt`).
- Radio music generated locally with ACE-Step 1.5 (MIT); radio voices synthesised with F5-TTS from public-domain LibriVox recordings. Note: the F5-TTS pretrained weights are CC BY-NC 4.0, so the voice tracks are fine for a free/open project but must be re-voiced (another TTS or real actors) before any commercial release.

## Licence
Not chosen yet; the maintainers will add one. Until then, please ask before redistributing.
