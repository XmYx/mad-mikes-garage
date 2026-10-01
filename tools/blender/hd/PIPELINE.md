# HD asset pipeline (Blender -> Unity)

The approved HD pack (`hd_preview/sheet_*.png`) is authored as Blender scripts in this folder (`cars/`, `heavy/`,
`misc/`, `character/`, stage in `render_common.py`). This document is the contract between those sources, the export
tool and the game. Integrators for vehicles, characters, props and parts follow it.

## 1. Export

```
# all slice jobs (default set), or names from the job table / every built .blend
python3 tools/blender/hd/export/run_export.py [--jobs 2] [Sedan DumpTruck ...|all] [--list] [--report]
# one asset by hand
blender -b <asset.blend> -P tools/blender/hd/export/export_hd.py -- --group cars --out Assets/MadMax/Models/HD/cars
# check an export against its sheet tile (renders it on the sheet stage)
blender -b -P tools/blender/hd/export/verify_render.py -- Assets/MadMax/Models/HD/cars/Sedan /tmp/out
```

* Source `.blend` files are gitignored; build them with the group scripts (`cars/build.py`, `heavy/build_all.py`,
  `misc/*.py`, `character/build_characters.py`) or point `HD_BLEND_ROOT` / `--src` at a checkout that has them.
* Output: `Assets/MadMax/Models/HD/<group>/<Asset>/` = `<Asset>.fbx`, `<Atlas>_Base.png`, `<Atlas>_Mask.png`,
  `[<Atlas>_Normal.png]`, `[<Atlas>_Emission.png]`, `<Asset>.hd.json` (sidecar). Logs: `<repo>/Logs/hd_export/`.
  Size report: `tools/blender/hd/export/hd_report.json`.
* Bake runs on the GPU (CUDA/OptiX) when available; the first run compiles kernels (~4 min), then a car takes ~90 s.
* `run_export.py all` picks up every built blend: cars by file, heavy by file, misc files export each top-level
  object as its own asset (`--kind part|vehicle|prop` by file prefix `parts_`, `bike_/boat_/air_`, else prop).

| group | atlas | default size | kind | axis fix |
|---|---|---|---|---|
| cars | one per asset (the blend's own smart UVs) | 2048 | vehicle | none |
| heavy | one per asset (unwrapped by the exporter) | 2048 | vehicle | mirrored back |
| misc | one per asset (unwrapped) | 1024 (parts 512, bikes/boats/aircraft 2048) | prop / part / vehicle | mirrored back |
| character | one per mesh (re-unwrapped per mesh) | body 1024, garments 256-1024 by area, eyes 128 | character | none |

Sizes of the first exports (FBX + PNG, LOD0/1/2 triangles): Sedan 8.9 MB 57.7k/26.3k/10.5k, Pickup 8.8 MB
58.0k/26.8k/10.7k, Fiat126p 7.8 MB 43.4k/19.8k/7.9k, DumpTruck 11.2 MB 29.3k/14.5k/5.8k, BrickHouse 4.0 MB
8.4k/4.1k/1.6k, CharacterMale 4.6 MB 19.1k, CharacterMale_Starter 14.7 MB 34.1k. Plan ~9 MB per road car, ~11 MB per
heavy vehicle, ~4 MB per building, 5-15 MB per dressed character on disk (PNG; Unity compresses to BC7/BC5, roughly
1/4 of that in the build).

## 2. Axes, units, hierarchy

* Metres. Game/Unity axes: +X right, +Y up, +Z forward. Every transform in the sidecar is already in Unity space.
* `cars/` and `character/` scripts map game (x, y, z) to Blender (-x, -z, y): a proper rotation the FBX chain undoes.
  `heavy/` and `misc/` used (x, -z, y), which is the mirror image of the game; the exporter mirrors those back
  (`mirroredOnExport: true`). Left-hand objects keep the game's `localScale.x = -1` (heavy wheel instances).
* FBX: forward -Z, up Y, "apply transform" (`bake_space_transform`), FBX unit scale; Unity imports with scale 1,
  no root rotation. Rigs are written the standard way (root keeps the axis rotation) and Unity's *Bake Axis Conversion*
  removes it (`bakeAxisConversion: true` in the sidecar; the postprocessor reads it).
* The asset root (empty / rig) is reset to the origin: preview offsets and turns are removed (buildings were turned
  180 degrees for the sheet; their street side faces -Z again). The game origin of a vehicle is the voxel design's
  origin: the ground under the tyres is at `rootProps.game_ground_y_m` (cars: -0.032 m).
* In the FBX every object is a direct child of the root (nested children came out wrong with the applied transform).
  The sidecar keeps the logical hierarchy (`parent`) and both local and root-space transforms.

## 3. Naming

| Blender / FBX object | role | Unity (built prefabs) |
|---|---|---|
| `Body`, `Glass`, `Hood`, `Trunk`, `Door_R`, `DoorRear_L`, `Wheel_FR`, `Bumper_F`, `Engine`, `Lights`, `Interior`, `Seat_Driver`, `Tool`, ... | `mesh` | same name |
| `<Obj>__Glass` | `glass` (glazing faces split out of a mixed mesh) | child `Glass` |
| `<Obj>__Lamp_Head` / `__Lamp_Tail` / `__Lamp_Amber` / `__Lamp_Other` | `lamp_head` ... (emissive faces by kind) | child `Lamp_Head` ... |
| `<Obj>__L1`, `<Obj>__L2` | `lod1`, `lod2` (Decimate collapse 50 % / 20 %, meshes > 800 triangles) | child `LOD1`, `LOD2` + `LODGroup` |

* Never use `_LOD<n>` in names (Unity would build its own LOD groups from them).
* Mesh data names = object names, so imported meshes are found by object name.
* An object that is wholly glass keeps its name (`Glass`, with its seals). Lamp kinds come from material names
  (tail/red/brake -> Tail, amber/indicator -> Amber, head/white/yellow/lamp/light -> Head).
* Characters: `<collection>_Rig` (16 bones named like `HumanRig`: Root Pelvis Chest Head UpperArmL ForearmL HandL
  UpperArmR ForearmR HandR ThighL ShinL FootL ThighR ShinR FootR), skinned siblings `<collection>_Body`, `_hair_*`,
  garments `<collection>_<garment>`; LODs are siblings `__L1/__L2` (all skinned to the rig).

Custom properties (sidecar `props`, also in the FBX): `socket` (design socket base name: `door` = the design's
`door_R`, `door_L`), `part` (part key the object stands for), `mirrored`, `radius_m`, `width_m`,
`socket_inner_x_vox` (wheels: |x| of the inner tyre face in voxels = the socket), `pivot_game_vox`, `category`,
`hinge`, `design`, `voxel`. Root: `game_ground_y_m`.

## 4. Sidecar `<Asset>.hd.json` (JsonUtility-friendly: lists, no dictionaries; `Editor/HD/HDSidecar.cs`)

```
format 1, asset, group, kind (vehicle|part|prop|character), source, fbx, units, axes,
mirroredOnExport, bakeAxisConversion, readable,
rootName, rootProps [{k, s, n, v}], rootBlenderMatrix [16],
atlases [{name, size, baseMap, maskMap, normalMap, emissionMap, emissionScale, paintRef [r,g,b linear], hasAlpha, materials []}],
materials [{name, cls}]          cls: opaque | paint | glass | lamp_head | lamp_tail | lamp_amber | lamp_other
paintMaterials [names]           the body paint (vehicles): <Asset>_paint, else the largest "paint" material
objects [{name, parent ("" = root), type, role, position [3], rotation [x,y,z,w], scale [3] (parent-local),
          rootPosition [3], rootMatrix [16 row-major] (prefab space), props [], mesh, tris, verts,
          boundsMin [3], boundsMax [3] (mesh, own frame), materials [], classes [], atlas, skinned}]
bones [{name, parent, head [3], tail [3], matrix [16]}]   (rest pose, prefab space)
boundsMin, boundsMax (whole asset, LOD0), lodRatios, lodSuffix "__L", splitSuffix "__",
tris [lod0, lod1, lod2], files [{file, bytes}]
```

## 5. Textures

| file | channels | colour space | content |
|---|---|---|---|
| `_Base` | RGB albedo, A opacity | sRGB | the procedural colour incl. wear, rust, dust, chips; times `1 - 0.35 (1 - AO)`; A < 1 only on glass |
| `_Mask` | R metallic, G ambient occlusion, B paint mask, A smoothness | linear | B = 1 where the main body paint shows (not rusted/dusted/chipped); 0 for non-vehicles |
| `_Normal` | tangent-space normal, +Y up (OpenGL, Unity's convention) | linear (normal map) | only when the materials have bump/normal nodes (bricks, cloth, hair) |
| `_Emission` | RGB lamp glow / `emissionScale` | sRGB | only when the asset has emissive materials |

* Bakes are Cycles EMIT bakes of each Principled input (base, alpha, metallic+roughness+paint id, emission) plus AO
  (48 samples, distance by group, blurred 3x3) and NORMAL; margin extend 1/128 of the size.
* Paint mask: texels of the paint material whose chromaticity and brightness stay near the paint's median colour
  (`paintRef`); the shader recolours `albedo * tint / paintRef` there.
* Vertex colours (`Col`, sRGB bytes): per-vertex average of the baked base colour, A = 1. CPU code that reads vertex
  colours (debris bursts, salvage chips) gets sensible colours; A is the wind-sway weight like the voxel meshes
  (1 = rooted). UV1 = `UVMap` (atlas), UV2 = `Wear` (zero; `VehicleBreakables` writes scrape amounts into x).

## 6. Unity import (`Editor/HD/HDAssetPostprocessor.cs`)

* Models under `Assets/MadMax/Models/HD/**`: scale 1 (file units), hierarchy preserved, no cameras/lights/blend
  shapes, mesh compression off, normals imported, Mikk tangents, no generated UV2, **materials not imported**
  (`MaterialImportMode.None`); every renderer gets its atlas material `<Atlas>_HD.mat` (created next to the asset from
  the sidecar by `HDMaterials`, re-imported automatically when created). Read/Write = sidecar `readable` (vehicles,
  parts, characters: dents, paint scrapes, armour shapes, glass breaking need CPU access). Characters: Generic rig,
  avatar from the model, game objects not optimised, standard skin weights. Props/characters used as they are get one
  `LODGroup` on the root (`HDLod.Heights` 0.2 / 0.07 / 0.01); vehicles and parts get per-part groups from the builder.
* Textures by suffix: `_Base`/`_Emission` sRGB, `_Mask` linear, `_Normal` normal map; mipmaps, trilinear, aniso 4,
  clamp, high-quality compression.
* Menu **MadMax/HD/Refresh HD Materials** rewrites every atlas material from its sidecar.

## 7. Materials and the shader (`Shaders/HDLit.shader`, `Resources/RuntimeMaterials/HDLit.mat`, `HDLitVoxel.mat`)

* Inputs: `_BaseMap`, `_MaskMap` (+ `_MaskStrength`), `_NormalMap` (+ `_NormalStrength`, 0 = none), `_EmissionMap`,
  `_EmissionScale`, `_LampOn` (per renderer: lamp state), `_PaintColor` (rgb sRGB tint, a = amount), `_PaintRef`,
  `_VertexAlbedo` (1 = albedo x vertex colour: voxel meshes in the HD look, `HDLitVoxel.mat`), `_Smoothness`
  (when no mask), `_SpecularScale`, `_Cull`.
* Every PixelVoxel per-renderer property and global works the same: `_CutY`, `_WorldCut` + `_MadMaxCut`, `_Sway`,
  `_SwayTip` + `_MadMaxWind`, `_Dirt`/`_DirtTop` (smooth blotches instead of voxel cells), `_MadMaxSnow`/`_SnowLat`
  (+ `_SnowMask`), `_MadMaxNight`, `_MadMaxUnderFill`, `_MadMaxCurve`, `_MadMaxWaterHole`/`_MadMaxSeaLevel`,
  `_MadMaxClouds`, `_MadMaxAutumn`, `_MadMaxBrightnessDelta`, `_MadMaxOutlineDelta`, fog + `_NoFog`, `_Unlit`,
  `_OutlinePx` (default 0: the sheets have no outline; > 0 draws the inverted-hull outline). Glass is dithered by
  the base alpha (4x4 Bayer, every pass; glass casts no shadow). SRP-batcher compatible (one `UnityPerMaterial`).
* Lighting = the sheet stage: sun radiance = the scene sun light x `_MadMaxHDSun.rgb` (default (2.72, 2.38, 1.91):
  the game's noon sun -> Blender's 3.4 x #ffe2b8), GGX specular, a blue fill sun low on the opposite side
  (`_MadMaxHDFill`, default 0.6 x #9ab4d6), uniform sky (`_MadMaxHDSky`, default 0.55 x #c9a37a) and a warm ground
  bounce, all times AO; a sky reflection by Schlick fresnel; additional lights smooth. Night scales sky/fill and adds
  the voxel shader's night blue. Tonemapping: Blender AgX (base look) **inside the shader** (polynomial fit), so HD
  objects match the sheets while the voxel world keeps its look and the URP camera keeps no tonemapper (only bloom);
  `_MadMaxHDTonemap = 1` turns it off if a URP tonemapper is ever added; `_MadMaxHDExposure` scales before it.
  The `PixelArtCamera.Grade` pass and fog apply after, as for everything else. All `_MadMaxHD*` globals are optional
  (a = 0 / 0 -> defaults); `Atmosphere` can drive them for dusk/weather later.
* Pixel mode = the same shader at low resolution (no light bands, no dither, no outline). Default `pixelHeight` 270
  lines: a sedan is ~115 px wide at the default iso zoom (`isoSize` 6), as on the sheets' `_px` tiles (22.5 px/m).
  The HUD stays at 320 lines (`GameSettings.hudScale` 4; settings v3 migrates the old 320-line default).
* Runtime material rules: `VehiclePaint` makes one material copy per atlas per vehicle (keeps SRP batching) and sets
  `_PaintColor`; `VehicleLights` sets `_LampOn` through a property block on `Lamp_*` renderers (head and other 1 when on,
  tail 0.7 / 1.6 braking, amber 0); voxel add-ons on HD vehicles (armour plates, decals) use `HDLitVoxel`
  (`HDModel.VoxelMaterialFor`). Runtime-created HD materials must copy an existing HD material or load
  `Resources/RuntimeMaterials/HDLit` (so the shader is in builds).

## 8. Building prefabs in Unity

### Vehicles (`Editor/HD/HDVehicleBuilder*.cs`, called by `MadMaxBuilder` for every design)
Every design with a usable export (`Models/HD/<group>/<DesignName>/`, kind vehicle, model imported, a `Body` or `Hull`)
builds from it; others keep the voxel model (console `[HD] <name>: ... voxel model kept`). **MadMax/HD/Force Voxel
Vehicles** (EditorPrefs) builds every vehicle and generic part from voxels for comparison; `HDVehicleBuilder.Disabled`
keeps single designs voxel. The voxel design stays the source of gameplay data (mass, drive, gears, systems); the HD
model replaces what you see and touch and sets the real-world geometry:
0. `SaveGenericParts` (after the voxel parts): every PartLibrary key with an HD part asset (`parts_all`, kind part,
   origin = mount point) or an object in a vehicle export carrying `part=<key>` (machine tools, implements: the `Tool`
   object at the socket, hinged children named by their `segment` prop) is saved under its key, replacing the voxel
   prefab for every vehicle, loot pile and recipe. Parts already HD (another pass) are kept.
1. `SaveParts` (before each vehicle prefab) makes the plan: HD objects map to design sockets by `socket` prop, by name
   (`Wheel_Front` / `Wheel_Main_L` / `Wheel_Side` -> `wheel_*`, `Drum_front` -> the axle's wheel sockets), or by being
   named like a part key (`engine_vtwin` -> the engine socket); an object whose socket the design lacks stays on the
   body. Per-vehicle parts: wheels `<vehicle>_<socket>` (HD mesh + radius `radius_m` / mesh, width, design tyre
   stats; pivot = inner tyre face on the axle, centre-line wheels at their left face; mesh child `Wheel`), crawlers one
   invisible `wheel_track_<vehicle>` (inside the HD belts; the id keeps `VehicleDriver.Tracked`), roller drums (the
   drum on the right wheel, `<key>_hidden` on the left), doors / hood under the voxel cut keys, bumpers
   `<vehicle>_bumper_*` (also where the design has none: the real cars), `Trunk` / `Tailgate` as Door-category parts on
   new sockets `trunk` / `tailgate` (pivot = lid centre).
2. `ApplyBody` (in `SaveVehicle`): HD `Body` (or `Hull`) on `Body`; root / Body-level objects without a socket go under
   `Body` (`Glass`, `Lights` + `Lamp_*`, `Interior`, `Seat_*`, `Roof`, `Hull`, `Legs` with a box collider for
   `TowCoupling`, chains, spare wheels); `Track_L/R` under `Body` (animated by `CrawlerTracks`); `movable` objects
   (Deck, UpperDeck, RampL/R: flat ramps get the design's rest angle) and spinners (Prop, Rotor) / control surfaces
   (Wing, Rudder) as root children; voxel `Glass`/`Driver`/`Roof`/`Legs` removed. Colliders: the design's explicit
   boxes (walk-ins, decks, bikes, boats) mapped onto the HD box, else the HD lower body (kept 12 cm above the lowest
   tyre bottom) up to the glass line + the glasshouse. Sockets: at their part's pivot (wheels, lids), at the HD
   object that names them, mirrored from the right twin, else mapped (voxel body box -> HD shell box per axis). Points:
   `DriverEye` = `Seat_Driver` + (0, 0.9, -0.12) m, `PassengerEye` likewise from `Seat_Passenger`, else mapped; hitch,
   coupler, `InteriorSpace` mapped, default furniture at the export's `Furn_<piece>_<n>` spots; boats: keel = HD hull
   bottom, prop at the HD `Prop`; aircraft prop at the HD `Prop`. `HDModel` on the root records the model box and the
   wheel radii (acceptance `hd.vehicles`). Each vehicle's changes go to the console and `Logs/hd_vehicles.md`.
3. Per-part `LODGroup`s (LOD0 = the part's renderer, LOD1/LOD2 children) all sized like the whole vehicle so parts
   switch together; LOD children are skipped by dents, scrapes and armour (`HDModel.IsLod`).
To add a vehicle: export it under the design's name, run **MadMax/Build Parts + Vehicles** (or Build Game Scene), read
its `[HD]` line / `Logs/hd_vehicles.md`, check sockets / colliders in the prefab, run acceptance `hd.vehicles`.

### Runtime contract for HD vehicles
* `Body` has the HD body mesh (UV0 = HD; `HDModel.IsHDMesh`); `Body/Glass` is the glazing; door glass is the door
  part's `Glass` child (first person hides every `Glass` renderer of the vehicle); lamps are
  `Lamp_Head`/`Lamp_Tail`/`Lamp_Amber`/`Lamp_Other` under `Body` or on lamp parts (front/back and left/right from their
  position for `BrokenLamps` bits).
* `DeformableMesh` dents HD meshes and re-joins normals across UV-seam copies (smooth shading kept); track belts
  (`HDModel.IsBelt`) are never dented or scraped.
* `CrawlerTracks`: with `Body/Track_L|R` it animates the belt (link pieces slide round the stadium path by the track
  travel modulo the link pitch, normals turned on the arcs) instead of building voxel links.
* `VehicleBreakables` treats each pair of triangles as a "quad" (same save format), smashes body and door glass,
  breaks lens triangles, and writes scrapes into UV2.x (bare metal in the shader).
* `VehicleArmor` samples HD triangles every half voxel to find the body shell (zones, grilles from body + door glass);
  plates render with `HDLitVoxel`. `VehiclePaint` decals sit on the HD flank (outermost body / door vertices).
* `InteriorSpace`: a walk-in without a `Body/Roof` (HD houseboat, submarine) clips the body above the ceiling
  (`_CutY`) for the cutaway. `FlightModel` tilts a `Wing` with the pilot's bar; `BoatModel` turns a `Rudder`.

### Characters (`group character`)
FBX = rig + skinned meshes (full body: the MASK that hid skin under clothes is dropped; garments are inflated shells
over it), bones named like `HumanRig`, rest pose = the sheet pose; sidecar `bones` has every bone's head/tail/matrix in
prefab space. Two ways to animate: (a) bind the imported `SkinnedMeshRenderer`s to the `HumanRig` bone transforms by
name (`bones[i] = rig.Find(name)`, `bindposes` from the sidecar rest matrices), keeping `HumanAnimator`; or (b) drive
the imported bones directly. Garments are separate meshes per piece (one atlas each) so `ClothingLibrary` slots can
toggle them; an outfit export (`outfit_*`) is a complete dressed character.

### Props and buildings (`group misc`, kind prop)
Use the imported model as it is (root `LODGroup` from the importer). Buildings come as `Shell` (walls, slabs:
carvable), `Glass`, `Detail`; for destruction voxelize `Shell` into a `VoxelGrid` at bake time (materials by
material class) as CLAUDE.md asks for Blender props, and keep the HD mesh for looks until the first hit.

### Parts (`misc/parts_*.blend`, kind part)
Each top-level object exports as its own asset named like the part key (`wheel_street`, `engine_v8` ...), origin =
mount point (right-hand authoring). Build prefabs the way `HDVehicleBuilder.BuildPart` does (VehiclePart + stats from
`PartLibrary`, wheel mesh offset from the inner face) and save them under the part key to replace the voxel prefab.

## 9. Checks
* `python3 share/tools/compile_check.py <worktree>` must print OK.
* `verify_render.py` side by side with `hd_preview/<group>/<name>.png` (heavy/misc show mirrored, props un-turned).
* In Unity: reimport `Assets/MadMax/Models/HD`, then **MadMax/Build Parts + Vehicles**; console `[HD] Sedan: HD body...`.

## 10. Known gaps
* Source issues seen in the bakes: the cars pack has a few faces with an undefined material (magenta `ff00ff`
  fallback in `cars/hdlib.get_mat`, visible inside the Sedan cabin); fix the material name in the car scripts.
* Heavy/misc wheel instances share one mesh: their atlas islands overlap (identical bakes, fine). Large flat props
  get ~100 px/m at 1024: raise `--size` for hero buildings.
* LODs are plain decimation (UV seams kept by Blender's collapse); thin glass/lamp children have no LODs.
