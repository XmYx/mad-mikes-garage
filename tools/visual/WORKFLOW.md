# Visual workflow: block-out to dressed, lit, breakable places

Derived from "Is This Really AI? Insane Opus 5.5 GameDev Workflow" (Can It Code?, 2026-10-14, youtube.com/watch?v=XqtnBlPJ6KE):
a Godot neon street + shop built by Opus 5.5 with an image model (pictures/textures) and an image→3D model (small props).
Mapped onto this project's tools: **Krea-2 Turbo** (local ComfyUI 127.0.0.1:8188, client `tools/blender/hd/character/fabric_textures.py`)
for every picture, **TRELLIS.2-4B** (MIT, local, ComfyUI-Trellis2 node) for image→3D props, **Blender 5** scripts for
modular architecture, Unity HDLit for the look. Hunyuan3D is not used: its licence excludes the EU.

The rule the video lands on: **architecture = Blender script, surface = generated picture, small details = image→3D.**
Image→3D props stick out of walls and ignore measurements (an air conditioner 1 m off the wall); pure Blender scripts are
exact but too clean; Blender scripted *from a generated front-view drawing* is exact **and** dirty.

## 0. Plan with a paint-over (per place: a street, a shop, a camp)
1. Block the place out in grey (voxel/HD blocks already in the game, or `BiomeProps` templates), capture from the
   player's eye (`MadMax/Capture Pixel Screenshot`, vector render style, HD pack).
2. Krea-2 paints over the grey capture (img2img, denoise 0.55–0.7, prompt = mood: "dusk, wet, sodium lamps, ...").
   Make 3–4 looks, pick one (`tools/visual/paintover/<place>/picked.png`). That picture is the **target** for every
   later step: colour of light, density of clutter, what glows.
3. List what the picture has that the game lacks (signs, cables, AC units, crates, puddles, interior glow) — that list
   is the work for the place.

## 1. Modular architecture pieces (walls, shopfronts, kiosks)
Per piece (e.g. "2-storey backstreet facade, 3.0 m × 6.8 m"):
1. **Drawing** — Krea-2: one modular piece, straight front view, orthographic, even light, stated size and parts
   ("ribbed roller shutter, aluminium sliding window, AC unit under it, tiles, rain streaks").
2. **Measure** — a script reads the drawing (edges/colour regions → openings, sills, shutter, AC box) into a JSON of
   rectangles in metres; Opus checks it against the stated size.
3. **Build** — Blender `bpy` script from the JSON: wall slab, recessed openings with depth, frames, sills, shutter
   ribs, pipes, AC box flush on the wall; real-world sizes (doors 1.04 × 2.24 m, storeys 2.7–3.0 m).
4. **Textures** — Krea-2 makes 1 m² *material* patches (concrete, tiles, shutter metal, painted plaster); the existing
   `flatten` + `tileable` (copy shifted by half, blended seam) make them repeat; colour → normal + roughness by the
   `make_textures.py` high-pass. No baked lighting.
5. **Dirt layer** — the drawing itself, projected front-on as a multiply/overlay decal layer (rain streaks under sills,
   grime at the base): this is what made the video's walls read "real".
6. **Joins** — pieces snap on a 1 m / storey grid; a test stacks every combination and checks no gaps / z-fight.
7. Export through `tools/blender/hd/export` (atlas, `_Base/_Mask/_Normal/_Emission`, LODs) like every HD asset.

## 2. Props in bulk (image→3D)
1. A list file (`tools/visual/props/<set>.json`: id, prompt, real size in m, mount: floor|wall|ceiling, max faces).
2. Krea-2 draws each prop **front / side / back** on a plain background (one image, three views, or three seeds of
   the same prompt + "front view" / "side view").
3. TRELLIS.2 batch (headless, one command for the whole list) → GLB with PBR.
4. **Fix-up script** (Blender, automatic, this is where the video lost time):
   - scale to the stated real size (longest axis),
   - **orientation check**: render the mesh from ±X/±Z, compare with the front drawing (SSIM), rotate to the best
     match — 4 of 18 props came out facing backwards in the video,
   - decimate to the face budget (it asked 3 000, got 5 377) and make LOD1/LOD2,
   - origin = mount point (floor centre / wall back / ceiling top), wall props pressed flush,
   - materials → our atlas convention (metal → Mask R, roughness → Mask A).
5. Contact sheet of the whole batch (`contact_sheet.py`) for a human look before anything goes in the game.

**As built (2026-10-14), set `tools/visual/props/street_set.json` (20 street props):**
```
python3 tools/visual/props/gen_images.py street_set.json --seeds=1          # Krea-2 (ComfyUI running), ~6 s each
~/ai/TRELLIS.2/.venv/bin/python tools/visual/props/run_trellis.py street_set.json   # ~1 min each on the 4090
blender -b -P tools/visual/props/fixup_blend.py -- street_set.json           # -> tools/blender/hd/world/props_gen_street.blend
python3 tools/blender/hd/export/run_export.py world/props_gen_street        # HD atlas + LODs -> Models/HD/world/Gen_<id>
```
- TRELLIS.2 lives in `~/ai/TRELLIS.2` (uv venv py3.11, torch 2.14 cu130, xformers attention; CuMesh / FlexGEMM /
  o-voxel built with C++20 — torch ≥ 2.14 headers need it —, o-voxel with `--no-deps`; builds run under
  `systemd-run --user --scope -p MemoryMax=8G` with `MAX_JOBS=2` and `TMPDIR` on disk: `/tmp` is RAM here).
- Weights in `~/ai/TRELLIS.2/weights`: TRELLIS.2-4B (MIT), the TRELLIS-image-large sparse-structure decoder,
  DINOv3 ViT-L/16 (`DINOV3=<dir>`; Meta's repo is gated — the camenduru mirror matches its files byte for byte where
  the official listing shows them; the DINOv3 licence applies). Only the 512 stages are loaded (the 1024 flow models
  stay on disk; the run fits in 20 GB of RAM).
- Background removal: none at runtime — `cutout()` floods the white background from the borders and hands RGBA to the
  pipeline (RMBG-2.0 is non-commercial; BiRefNet would run remote model code). Local patch:
  `trellis2/modules/image_feature_extractor.py` finds the DINOv3 layers at `model.model.layer` (transformers ≥ 5).
- **Dashboard** `python3 tools/visual/props/dashboard/server.py` → http://127.0.0.1:8797 (Prop Foundry): sets on the
  left, a card per prop (picture, 3D preview, prompt / size / mount / faces, variants by seed — click to pick, re-roll,
  redraw, upload your own picture, make 3D, remove), add items and new sets, set-wide style prompt, "Publish set to
  game" (fix-up + HD export). One queue: pictures first (ComfyUI started on demand, 16 GB cap), then 3D per set
  (ComfyUI stopped, TRELLIS under a 20 GB cap). Sets: street, kitchen, bathroom, workshop, living.
- Interiors: `RoomDresser` puts `GenHome` / `GenWork` props against walls (55 % of wall picks when the HD pack has
  them), `GenTop` / `GenBench` small things on tables, counters, shelves and lockers.
- In game: town decor spots `Gen_<id>` (`TownPlan.Dress` `GenStreet` / `GenShopFront`) spawn the HD model with a box
  collider above knee height (`BiomeProps.GenDecor`); without the HD pack they are skipped.

**Cars (v3, `props/car_conform.py`):** never slice the TRELLIS shell (holes, inside-out patches, ground skirts).
Keep the design's part meshes, pull painted vertices ≤ 2.5 cm toward the generated surface (smoothed displacement),
bake the generated colour onto one shared UV layout and use it as a high-pass detail over the design colour (+ rust).
`blender -b -P tools/visual/props/car_conform.py -- [ids]` → `tools/blender/hd/cars_gen/`; then
`python3 tools/blender/hd/export/run_export.py --force --jobs 2 cars/`.

## 3. Interiors you can break (the shop)
The video's shop is the benchmark: no door is closed, shelves stocked with thousands of items, glass you see
through and shoot through, fridge doors opened by hand, bottles that break one by one.
- **Stock** = instanced meshes (one draw per product kind, `Graphics.RenderMeshInstanced`), filled by a shelf
  script (rows × facings, small random gaps / tilts / missing items). Only the item that is hit becomes a rigidbody.
- **Glass** = HDLit transparent pane (not dither) with the SSR/probe reflection; hit → crack decal at the point,
  second hit or a car → shatter: pane replaced by pooled shard bodies + `Shards.Drop` litter, the hole cut around
  the hit for small arms ("cut around the hit").
- **Fridges / coolers / doors** = hinged leaves (`Door`-style `IInteractable`), interior light on, glass door,
  cold-drink stock instanced, door swings by hand ([E] grab and drag, or tap = open/close).
- **Light** = practical lights only where the picture has them (fluorescent tubes, fridge glow), shadowed, budgeted.

## 4. Light pass (judged against the paint-over)
"From orange soup to a look I like": too many identical warm lamps (≈ 50 orange lamps) flatten the scene.
- One key colour per place + one contrast colour (sodium warm vs. fridge/neon cool), few strong lights, dark gaps
  between them.
- Every visible lamp has a glowing emitter (emission + bloom) and only the important ones cast shadows.
- Wet ground: smoothness up with `Weather.Wetness` + puddles, reflections (SSR, a reflection probe per interior and
  per street block) so every light shows twice.
- Compare the capture with `picked.png` side by side each iteration; stop when the light reads the same.

## 5. Signs and decals in bulk
Krea-2 draws sign faces (shop names, symbols, posters, graffiti, hazard plates) on black → emission maps for neon
(alpha from luminance) or albedo decals; 100+ per set; a placer script hangs them on facades by rules (one per
shopfront, vertical blades on corners, posters at eye height).

## 6. Effects from reference
Study real footage frame by frame (timing of flash → fire → smoke → dust), generate sprite sheets per layer
(Krea-2 grids, or a video model clip cut into 64 frames), stack layers (flash, fire, smoke, dust, debris).

## 7. Tests that break the place on purpose
- A scenario that hits every breakable in a place and checks it breaks, drops loot/shards, and nothing floats.
- A bot that walks every stair / door / shop aisle (NPC pathing) and reports what it can't reach.
- Captures from fixed cameras per place → `tools/visual/compare.py` puts capture and paint-over side by side.

## 8. HUD
Thin, modern, low-contrast panels: boxed counters (ammo / fuel) bottom-right, small caps labels, one accent colour,
icons over words, nothing permanent that isn't needed (fade when unchanged). HD HUD path (`PixelCanvas.res` 2 +
`HudTextGraphic`) carries it; pixel mode keeps its own look.
