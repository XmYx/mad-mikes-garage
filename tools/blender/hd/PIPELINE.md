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

### Vehicles (`Editor/HD/HDVehicleBuilder.cs`, called by `MadMaxBuilder` for names in `HDVehicleBuilder.Enabled`)
The voxel design stays the source of gameplay data (mass, drive, gears, sockets, systems, interiors); the HD model
replaces what you see and touch:
1. `SaveParts` (before the vehicle prefab): every right-hand / centre HD object with a `socket` prop becomes a part
   prefab: doors and hood keep the voxel cut keys (`sedan_door`, `sedan_door_rear`, `sedan_hood`); wheels and bumpers
   get `<vehicle>_<socket>` keys (`sedan_wheel_front`, `sedan_bumper_front`) with the stats of the design's default part
   (`wheel_street`, `bumper_chrome`) and the HD radius / width. Wheels: pivot = inner tyre face on the axle (the socket
   convention), mesh child `Wheel` offset outward. Left sockets mirror the right prefab (as for voxel parts). Engines,
   radiators and tools keep their generic parts until the parts pack lands.
2. `ApplyBody` (in `SaveVehicle`, before saving): HD `Body` mesh on `Body`; root-level HD objects without a socket go
   under `Body` (`Glass` -> `Body/Glass`, `Lights` with `Lamp_*` children, `Trunk`, `Interior`, `Seat_*`); voxel
   `Glass`/`Driver`/`Roof` removed; colliders = two boxes (lower body up to the glass line, glasshouse) under
   `Colliders`; every socket moves to its HD object (wheels: inner face, `socket_inner_x_vox`), sockets without an HD
   object (armour, roof weapon, cargo, radiator, lights, snorkel, steps ...) are mapped from the voxel body box
   (body + glass voxels) onto the HD body box per axis; `DriverEye` = driver seat + (0, 0.9, -0.12) m; hitch, coupler,
   passenger eye and `InteriorSpace` (floor, bounds, doors, seat, obstacles, furniture) mapped the same way;
   `HDModel` on the root; rigidbody mass recomputed.
3. Per-part `LODGroup`s (LOD0 = the part's renderer, LOD1/LOD2 children) all sized like the whole vehicle so parts
   switch together; LOD children are skipped by dents, scrapes and armour (`HDModel.IsLod`).
To enable another vehicle: export it, add its design name to `HDVehicleBuilder.Enabled`, run **MadMax/Build Parts +
Vehicles** (or Build Game Scene), check sockets/colliders in the prefab. HD object names with a `socket` prop must
match the design's socket base names (`wheel_front`, `door`, `door_rear`, `hood`, `bumper_front`, ...).
Heavy vehicles: segments (`Tool` with `hinge`, booms) are separate objects with their pivot at the joint; an
integrator maps them onto `PartDesign.Segment` names (`Machine`/`Crane` pose children by name).

### Runtime contract for HD vehicles
* `Body` has the HD body mesh (UV0 = HD; `HDModel.IsHDMesh`); `Body/Glass` is the glazing; door glass is the door
  part's `Glass` child; lamps are `Lamp_Head`/`Lamp_Tail`/`Lamp_Amber`/`Lamp_Other` under `Body` (front/back and
  left/right from their position for `BrokenLamps` bits).
* `DeformableMesh` dents HD meshes and re-joins normals across UV-seam copies (smooth shading kept).
* `VehicleBreakables` treats each pair of triangles as a "quad" (same save format), smashes body and door glass,
  breaks lens triangles, and writes scrapes into UV2.x (bare metal in the shader).
* `VehicleArmor` samples HD triangles every half voxel to find the body shell (zones, grilles from body + door glass);
  plates render with `HDLitVoxel`.

### Characters (`group character`)
FBX = rig + skinned meshes (full body: the MASK that hid skin under clothes is dropped; garments are inflated shells
over it), bones named like `HumanRig`, rest pose = the sheet pose; sidecar `bones` has every bone's head/tail/matrix in
prefab space. Two ways to animate: (a) bind the imported `SkinnedMeshRenderer`s to the `HumanRig` bone transforms by
name (`bones[i] = rig.Find(name)`, `bindposes` from the sidecar rest matrices), keeping `HumanAnimator`; or (b) drive
the imported bones directly. Garments are separate meshes per piece (one atlas each) so `ClothingLibrary` slots can
toggle them; an outfit export (`outfit_*`) is a complete dressed character.

### World, furniture, items, tools, animals (runtime, no prefabs)
These keep their voxel objects as the gameplay truth and wear the imported model at runtime.
* **Catalog**: **MadMax/HD/Build HD Catalog** (also run by MadMax/Build Game Scene) writes one `HDAssetRef` per
  exported asset to `Assets/MadMax/Resources/HDGen/<Domain>/<game id>.asset` (gitignored like the models): domain from
  the root props (`category` Building/Prop/Vegetation/Landmark/Stall/Site -> World; furniture categories -> Furniture;
  `kind` item/cloth -> Item, tool -> Tool, animal/critter -> Animal, part -> Part), id = root prop `game_id`. It keeps the
  model, bounds, `voxel`, `world_scale`, `origin` and per object `part`, `rig_part`, `index`, `mover`, `shell`, markers
  and root matrices. `Rendering/HDAssets` loads entries on first use (misses cached); `--no-hd` or MadMax > Dev >
  Voxel Visuals keeps every voxel visual. Structure furniture imports read/write (StructureBatcher merges it).
* **Dressing** (`Rendering/HDVisual`): the model is instantiated as child `HD`; the voxel renderer keeps its mesh
  (colliders, bounds, debris, dyes, batching signatures) but `forceRenderingOff` (`HDVisual.IsHost`; LineOfSight keeps
  it dark). Objects with `mover` (and `Bulb`) are re-parented onto the voxel child of the same name (Rotor, Leaf,
  Stamp0..2, Head, Wheel, Lever, Bulb) so the game's animation moves them; `Lamp_*` splits glow while `LampState`
  says so (pieces: `PoweredLight.Glowing`; buildings: after dusk). Variants share copies per atlas material:
  `_WorldCut` for world props, `_Sway` / `_SwayTip` for vegetation (vertex alpha = 1 - Sway), `_Tint` for dyes.
  OccluderFade's `_CutY` block is copied onto the HD renderers while cut.
* **World props and buildings**: `DestructibleVoxels.Spawn` -> `World/HDProp` by template id (sites excluded). Hybrid
  destruction (`World/HDCarve`): pristine props draw only the shared HD materials; the first carve gives the prop a
  3D mask (`_CarveMask`, R8 over the template's voxel bounds + 2) and per-prop material copies with `_CarveOn`; HDLit
  clips every pass (colour, shadow, depth) where the cell half a voxel behind the surface was carved, and clears empty
  cells within 2 of a carved cell (trims, awnings and pipes go with their wall). The faces of remaining voxels that look
  into carved cells are drawn as `HDRim` (`HDLitVoxel`, voxel colours). Damage restored from the destruction state
  rebuilds the mask from the template. Collision, support, collapse, debris and yields stay voxel.
* **Sites**: SiteBuilder emits kit modules on its own plans (`Site_Bunker_*`: roofs per cell by biome, walls / outer
  walls / doorways / entry per edge with +Z out, pillars, lamps, rubble, vents, hatches, ramp walls + sandbags mirrored
  on the left, blast door; `Site_Tunnel_*`: walls where the mesa runs the whole 8 m segment, arch where it is roofed
  throughout, portals, boulders; `Site_Airfield_*`), all or none per piece; voxels no module covers (roof overhangs,
  mesa cap, high tunnel walls) are meshed on the worker and drawn HD-lit (`Residual`, re-meshed on carves).
* **Furniture**: `FurnitureLibrary.DressHD` on placed pieces, loot spots, town fixtures, switches, stashes; build
  ghost shows the HD model with the same tint; `StructureBatcher` merges the LOD1 meshes of far Structure pieces per
  atlas material.
* **Items / tools / icons**: `ToolLibrary.Create` (origin = grip, -Y along the arm, same as the voxel tools), weapon
  racks, the work jack; `WorldItemModels.AddVisual` puts the item at real size beside the voxel icon model (tools at
  `world_scale`, laid down; resources `world_res_fluid/sack/crate`; kits `world_kit`); `IconRenderer.Get` returns
  `HDIcons` (off-screen URP render at 4x, box filter, outline) once rendered, the voxel icon meanwhile.
* **Animals**: `HDAnimal.Dress(rig, species)` adds the HD mesh of each rig part (Body, Head, Leg i, Tail, WingR/L,
  Seg i, Rattle; Saddle, Fleece when made) under the part, placed by `rootMatrix` relative to the part's rest pose;
  the pack mule, bats, sea fish and jellyfish likewise.

### Parts (`misc/parts_*.blend`, kind part)
Each top-level object exports as its own asset named like the part key (`wheel_street`, `engine_v8` ...), origin =
mount point (right-hand authoring). Build prefabs the way `HDVehicleBuilder.BuildPart` does (VehiclePart + stats from
`PartLibrary`, wheel mesh offset from the inner face) and save them under the part key to replace the voxel prefab.

## 9. Checks
* `python3 share/tools/compile_check.py <worktree>` must print OK.
* `verify_render.py` side by side with `hd_preview/<group>/<name>.png` (heavy/misc show mirrored, props un-turned).
* In Unity: reimport `Assets/MadMax/Models/HD`, then **MadMax/Build Parts + Vehicles**; console `[HD] Sedan: HD body...`.
* World / furniture / items / animals: **MadMax/HD/Build HD Catalog** (console `[HD] catalog: World n Furniture n ...`),
  then the acceptance scenario `hd.world` (blocked while the catalog is empty).

## 10. Known gaps
* Source issues seen in the bakes: the cars pack has a few faces with an undefined material (magenta `ff00ff`
  fallback in `cars/hdlib.get_mat`, visible inside the Sedan cabin); fix the material name in the car scripts.
* Heavy/misc wheel instances share one mesh: their atlas islands overlap (identical bakes, fine). Large flat props
  get ~100 px/m at 1024: raise `--size` for hero buildings.
* LODs are plain decimation (UV seams kept by Blender's collapse); thin glass/lamp children have no LODs.
