# Mad Mike's Garage — Roadmap

The game already has a strong spine: a procedural wasteland (biomes, towns, roads, lakes, bunkers, mesas with
tunnels, weather, day/night, fire, radiation), voxel vehicles built from swappable parts with a real drivetrain and
damage model, a survival character (needs, injuries, clothing, skills, knowledge), base building with power and
water networks, farming, a soil → ore → metal → asphalt industry chain, NPCs (traders, raiders, wanderers,
dialogue, trade) and a radio network.

This roadmap grows every system so they **feed each other**. The core loop it aims for:

> **scavenge → build & tune → travel & trade → fight or talk → settle & produce → gain standing → go further**

Cross-links are called out per system under *Ties*. Status: `[x]` implemented, `[ ]` planned.
Systems are listed in implementation order (by complexity: T1 data-driven additions → T3 new simulation).

---

## Phase 0 — fixes from the latest review `[done]`

- [x] Tyre types: mud-terrain, street, rain, sport (+ crawler tracks); wet-ground grip per tyre, ground pressure
- [x] Mud more permissive in rain (rain soaks less than standing water; basins stay nasty)
- [x] Machines: every hinge moves separately (excavator boom/stick/bucket/swing, backhoe loader + rear hoe,
      dozer blade lift/pitch/angle, crane slew + luff); tools never collide with terrain; crawlers on wide tracks
- [x] Ragdolls for the player and NPCs; injuries by body zone change movement and actions
- [x] Cutaway only in top-down views; line-of-sight only in third person; none in first person
- [x] Realistic scale feel: FPS FOV 60° vertical, over-the-shoulder third person, walk/run speeds, door sizes
- [x] Blender allowed for delicate models (noted in CLAUDE.md)

---

## 1. Tools `T1`
*Exists:* sledgehammer, claw hammer, axe, pickaxe, shovel, wrench, salvage cutter, machete, pipes, torch, gas torch,
lantern, pipe shotgun, molotov.

- [x] **Durability**: every tool wears with use (hits, cuts, digs) and breaks at 0; condition shown on the hotbar;
      repair at a workbench with the tool's material
- [x] **Crowbar**: pries locked lockers / doors / crates open (noise, Strength check); decent melee
- [x] **Welder** (`tool_welder`): fixes vehicle dents, frame damage and part damage for scrap + fuel
- [x] **Jack**: lift a vehicle to swap wheels (wheels of size ≥ 3 need it) and right a flipped car on foot
- [x] **Binoculars**: zoom; reveals NPC names / hostility and landmarks far away
- [x] **Geiger counter**: clicks with dose, points at hot loot and uranium ore
- [x] **Flashlight**: battery spot beam (vs the lantern's all-round glow)
- [x] **Repair kit** (`use_repair_kit`): field repair of one vehicle part (+30 % condition)
- [ ] **Grappling hook**: climb walls / ledges (ties parkour), pull loose parts to you
- [ ] **Fishing rod, hoe, watering can** (see Fishing, Gardening)

*Ties:* crafting (repair, recipes), vehicles (welder, jack, repair kit), exploration (binoculars, geiger), parkour.

## 2. Furniture `T1`
*Exists:* beds, storage, stoves, ovens, lights, radio, TV, workbenches, crafting stations, climate pieces.

- [x] **Comfort**: a room's furniture (sheltered, enclosed) gives a comfort score → better sleep (rest regen,
      "WELL RESTED" buff for XP / stamina)
- [x] **Sit** on chairs, sofas and benches (rest, reading bonus)
- [x] New pieces: sofa, armchair, wardrobe (outfit storage), bookshelf (media storage, reading speed), weapon rack,
      trophy mount (animal heads), rug, wall clock, mirror (change hair / beard), shower + bathtub (hygiene,
      needs water), latrine (makes compost), sink + kitchen counter (water), dining table (meal buff)
- [x] **Dyes**: repaint pieces with dye items
      *Done:* `Comfort` (roof + structure walls + distinct pieces, halved outdoors; blood/bodies subtract), `Seat`
      (player sits, lounging pose), WELL RESTED / WELL FED in `CharacterStats`, latrine need (`waste`), trophies are
      loot for now (plates, hubcaps, ornaments, bull skulls; animal heads come with system 23), 6 dyes (stove).

*Ties:* survival (sleep, hygiene), irrigation (water pieces), gardening (compost), animals (trophies), media.

## 3. Player clothing `T1`
*Exists:* ~17 garments in slots, warmth / cooling, layered voxel shells.

- [x] **Condition**: garments tear from hits, crashes and fire; mend with cloth (sewing kit / sewing table)
- [x] **Backpacks** (back slot): school bag +8 kg, hiking pack +18 kg, frame pack +28 kg carry capacity
- [x] **Wetness**: rain soaks clothes (warmth drops), fires and shelter dry them; ponchos / dusters keep you dry
- [x] New garments: leather duster, poncho, hazmat suit (radiation), gas mask (smoke, dust, radiation), wool
      sweater, work overalls, cowboy hat, bomber jacket, shemagh, fingerless gloves, combat boots, welding mask,
      raider skull mask
- [x] **Style**: outfit changes first impressions in dialogue (raider gear scares the nervous, clean clothes help)
- [x] **Sewing table** station for clothing recipes
      *Done:* `WastelandGame.Clothing` (wear per garment, tatters render as holes below 35 %, fall apart at 100 %),
      backpacks got their own PACK slot (shoulder armour keeps BACK) with rigid voxel props, `CharacterStats.wetness`,
      radiation / dust / smoke protection (coughing without a face cover), welding without a mask flashes the
      screen, skull mask needs a bull skull, hazmat needs the chemistry book, raider gear opens a parley option.

*Ties:* weather & temperature, radiation, dialogue, crafting, economy.

## 4. Vehicle attachments `T1`
*Exists:* bumpers, bull bar, plow, ram, spikes, plates, winch, crane, jerry rack, twin tanks, spoiler, exhausts,
decorative turret.

- [x] **Roof rack / cargo box**: container on the roof
- [x] **Snorkel**: engine keeps breathing in deep water (no flooding up to the snorkel)
- [x] **Light bar / searchlight**: bright spot beam
- [x] **Working weapons**: roof machine gun (driver aims with the mouse), harpoon launcher (pull a vehicle),
      flamethrower, rear caltrop / oil-slick dropper, smoke launcher
- [x] **Side steps / roof ladder**: climb onto the roof
- [x] **Water tank**: carry water for farms and showers
- [x] **Generator on board**: powers pieces built on the vehicle
      *Done:* 14 parts (`PartLibrary.Attachments`), new sockets lights / snorkel / steps on the four cars, functions
      added at runtime by `PartFunctions` (state saved per socket), `VehicleWeapons` (LMB roof weapon aimed by mouse
      or crosshair, B dropper, U smoke), `RoadHazards`, `SmokeScreen` (raider fire misses through it), the vehicle
      power / water bus in `UtilityGrid`, roof perch via `RoofAccess`.

*Ties:* combat (raiders), irrigation, base building on vehicles, economy.

## 5. Crafting `T1–T2`
*Exists:* recipe library per station, research, knowledge gates, crafting from nearby containers.

- [x] **Crafting time & queue**: stations work over time, the player can walk away
- [x] **Quality**: crude / sturdy / fine from skill and station → durability and stats
- [x] **Salvage & repair** items back into materials / condition
- [x] New stations: sewing table, chemistry lab (gunpowder, medicine, fuel additives), tanning rack (hide → leather),
      smokehouse (preserves meat and fish), loom (cotton / hemp → cloth), gunsmith bench
- [x] **Blueprints** found in bunkers unlock recipes
      *Done:* `CraftingStation` queue (8 jobs, paid up front, X cancels, finished goods to the crafter within 6 m or
      the tray: [T] collect), `RecipeLibrary.Seconds`, make rolls (`WastelandGame.Crafting`: item make per id, part
      `quality` saved per socket), Y salvage page, 13 new resources (hide … uranium ore), hemp crop, meat / fish foods,
      antibiotics, painkillers, fuel additive, blueprints for the MG, flamer, harpoon, generator, searchlight, frame pack.

*Ties:* every system that produces or consumes items.

## 6. Refining `T2`
*Exists:* wash plant, furnace, arc furnace, kiln, mixer, still.

- [x] **Crude oil**: seeps in some regions; a pumpjack extracts `CrudeOil`
- [x] **Refinery**: crude → petrol, diesel, engine oil, tar (needs power and heat)
- [x] **Diesel** as a separate fuel for diesel engines; petrol for petrol engines
- [x] **Biodiesel**: seed oil (sunflower / hemp press) + ethanol → diesel
- [x] **Scrap smelting** & **rubber reclamation** (tyres → rubber); **sulfur** from nuclear craters / bunkers
- [x] **Batteries** (lead + acid) for flashlights and power storage
      *Done:* `WorldGen.OilAt` fields with tar-stained ground, `Pumpjack` (power, rocking beam), refinery cuts with
      byproducts, oil press, biodiesel at the chemistry lab, typed fuel tanks (`VehicleSystems.tankKind`, WRONG FUEL
      fault, pumps sell petrol or diesel, tanker checks), coal / charcoal stand in for wood fuel, loose parts break down
      at a garage / workbench (salvage page), sulfur crusts in nuclear digs, flashlight batteries, battery rack.

*Ties:* vehicles (fuel types), mining, gardening (oil crops), economy, power grid.

## 7. Mining `T2`
*Exists:* digging soils by depth (sand, clay, laterite, rubble, slag, stone), wash plant → ores.

- [x] **Ore veins**: visible outcrops (iron, copper, tin, bauxite, coal, sulfur, uranium) on mesas and hills;
      pickaxe / sledge / explosives yield ore
- [x] **Explosives**: dynamite and pipe bombs blast rock and terrain (craters)
- [x] **Drill** attachment for the excavator
- [x] **Prospecting**: geiger and metal detector show buried ore strength
- [x] **Coal** as fuel for furnaces and generators
      *Done:* `WorldGen.Ores` deposits (140 m cells, biome rules, + lead ore), outcrop templates whose vein voxels are
      the ore material (any carving yields it), deep digs over deposits bring ore up (`SoilAt`), `Explosion` /
      `Explosive` (dynamite, pipe bomb, BLAST wounds), `tool_excavator_drill` (auger "bit" segment, bores 12 m, spoil to
      the operator), `MetalDetectorTool`, geiger reads uranium, steam generator burns coal, coal stands in for charcoal.

*Ties:* refining, tools, weapons (explosives), economy.

## 8. Gardening `T2`
*Exists:* garden plots, crops and saplings, fertilizer, composter, planted trees.

- [x] **Soil fertility** per plot, depleted by harvests, restored by compost / manure
- [x] **Water need**: crops wilt without water; watered / irrigated plots grow faster
- [x] **Seasons & frost**; greenhouse (glass roof) protects
- [x] **Weeds & pests**: weeds slow growth (hoe), crows peck (scarecrow)
- [x] **Quality & seed saving** (Farming skill)
- [x] New crops: wheat (flour → bread), cotton, hemp, sunflower (oil), sugar beet, herbs (medicine), mushrooms (dark)
      *Done:* `GardenPlot` fertility / health / weeds / seed make (saved), drought wilting, open-sky frost, winter and
      autumn slow-down, light (a raycast up: glass `greenhouse` piece, solid roof — mushrooms need the dark), crows on
      ripening crops (voxel crow) unless a `Scarecrow` stands within 12 m, [T] pull weeds / fertilise, `tool_hoe`,
      `tool_watering_can` (also douses fires via `Fire.Douse`, refills from lakes); yields scale with fertility, health,
      seed make and Farming, seeds inherit a make; wheat → flour (workbench) → bread / porridge, sugar beet → sugar
      and beet ethanol, mushroom spawn → mushroom soup; seeds and garden tools in farm loot.

*Ties:* irrigation, animals (feed, manure), cooking, refining (biodiesel), economy.

## 9. Irrigation `T2`
*Exists:* water network (utility nodes, pipes), water source / outlet, sprinklers.

- [x] **Wells**: dig a well (water table by biome), hand pump; electric pump on the power grid
- [x] **Tanks & towers** store litres; **rain barrels** fill in rain
- [x] **Drip lines** (efficient) and **sprinklers** (radius) water plots automatically
- [x] **Channels**: dig a trench from a lake — water flows along it and soaks the fields beside it
- [x] **Water quality**: dirty water → sand + charcoal **filter** → clean
      *Done:* `WorldGen.WaterTable` (forest 3 m, jungle 2 m, desert 16 m, shallow by lakes), `well` piece (`HandPump`
      [E] 1–6 L per stroke for stamina, `WaterSource.Well` pumps when powered, tainted in the nuclear zone, [T] fills
      the canteen), `water_tower` (3000 L), `drip_line` (`Sprinkler.drip`: 0.05 L sips, irrigated beds grow 10 %
      faster), `DeformableTerrain.Channels` floods dug trenches connected to a lake at its level (water array + mesh,
      re-derived when chunks stream in); beds within 2.2 m of water stay wet. Build aiming passes through roofs the
      cutaway has clipped (build inside houses and greenhouses from the isometric view).

*Ties:* gardening, survival (drinking, washing), furniture, refining.

## 10. Fishing `T2`
- [x] **Fishing rod** + bait (worms dug with a shovel, insects, meat)
- [x] **Cast & reel**: bite timing by species, time and weather; tension minigame on the HUD
- [x] **Species** per biome and lake (toxic lakes: glowing mutants); size records
- [x] **Cooking & smoking** fish; **fish traps** in shallow water
      *Done:* `FishingRodTool` (swing casts onto water 7 m + Survival, float + sagging line, bite window → click to
      strike, hold to reel against bursts; snap / thrown hook / run-off; pose), `FishLibrary` (7 species by biome,
      3 toxic-lake mutants, boot / can / lockbox with its own loot table; bite rate by favourite bait, dawn/dusk,
      night or day feeders, rain, cold; weight skewed small, deep water for big ones), HUD status + tension bar, records
      per species (saved, listed on the skills page), `trophy_fish` / `trophy_fish_mutant` for the trophy mount;
      bait: worms from shovel digs and weeding (moist biomes, rain), insects shaken from bushes / logs / trees, cut bait
      from raw or rotten meat, corn dough from corn or bread (use a bait item to put it on the hook next); glowing fish
      (radiation when eaten, grilled, collectors pay 18 scrap); `fish_trap` piece (baited, catches on its own in
      0.15 m+ water); existing smokehouse / stove recipes cook the catch.

*Ties:* survival, economy, gardening (worms), smokehouse.

## 11. Vehicle armor `T2`
*Exists:* plate, spikes, ram and bull bars with absorb / spikes / ram stats.

- [x] **Coverage zones**: front, sides, rear, roof, wheel guards, window grilles on every vehicle
- [x] **Materials**: scrap sheet, steel plate, tyre-rubber composite (absorb, weight, fire resistance)
- [x] **Damage routing**: crashes, gunfire and fire hit the zone's armour first; armour loses condition and falls off
- [x] **Window grilles** stop shots at the driver (slightly blocked view)
- [x] **Weight** penalty on handling and fuel
      *Done:* `VehicleArmor` on every vehicle: the body's voxel shell (body, roof, doors, hood; undented) is recovered
      from its mesh per vehicle type and each zone becomes a fitted one-voxel plate (sides sill → window line with the
      arches open, front / rear faces, cabin roof, arcs over each tyre, bars over the glass — grilles ride outside the
      body so first person looks through them). Scrap patchwork / riveted steel / tyre-tread composite differ in crash and
      bullet share, fire intake, kg and wear. `VehicleDamage` crash and hit paths soak through the zone first; worn out
      plates are torn off (debris). Grilles cut the driver-hit chance from raider fire; the fire factor feeds
      `VehicleSystems.Heat`; the kg go into `VehicleChassis.extraMass`. [U] next to a vehicle with a welder opens the
      ARMOUR page (per zone: pick material, weld / repair for iron, scrap, rubber + petrol, or strip for some back).
      Raider cars get random kits, some wrecks carry worn ones; saved per vehicle.

*Ties:* raiders, tuning, crafting, welder.

## 12. Player armor `T2`
- [x] **Zones**: helmets, chest plates (scrap plate, tyre-rubber vest, kevlar), arm guards, gauntlets, shin guards
- [x] **Protection per damage type** (melee, shot, crash, fall, burn) reduces damage and injury chance on the
      covered zones (ties the injury system)
- [x] **Weight & noise**; durability and repair
- [x] Raiders drop their armour
      *Done:* `ClothingDef.armor` (per `DamageKind`), `weight`, `noise`, `mendWith`; new slots Vest / Arms / Shins;
      scrap plate / tyre-rubber / kevlar vests, arm guards, gauntlets, shin guards, motorcycle helmet, and the scrap
      helmet and shoulder armour now protect. `WastelandGame.Armour`: zone cover from the garment's coverage,
      protection fades with condition, `ArmourFactor` cuts damage in `PlayerVitals.Hurt`, `InjuryRules.Apply` stops or
      softens wounds on covered zones (the armour takes the wear, "YOUR ARMOUR TOOK THE BLOW"); weight counts in the
      pack, metal clanks (NPC noise, louder running); mended at the sewing table with its own material (scrap, rubber,
      leather, iron). Raiders wear vests and guards (hits on them are softened, metal clang) and drop them (70 %) on
      their bodies; kevlar and moto helmets are loot; workbench / sewing recipes; traders price by protection.

*Ties:* injuries, crafting, raiders, economy.

## 13. Player weapons `T2`
*Exists:* melee tools, pipe shotgun, molotov.

- [x] Melee: spear (reach), nail bat, knife (fast, bleeds), leaf-spring blade
- [x] Ranged: slingshot, bow & arrows (silent), crossbow, pipe pistol, revolver, bolt rifle, flare gun
- [x] **Ammo crafting** (arrows, bolts, shells, cartridges; gunpowder from charcoal + sulfur + saltpeter)
- [x] **Aim mode** (hold RMB): crosshair, shoulder aim, iso aims at the cursor
- [x] **Reload, magazines, jams** for crude guns
- [x] **Noise** alerts NPCs and animals
- [x] Throwables: pipe bomb, smoke bomb, rock
      *Done:* `RangedTool` generalised (hitscan or `Projectile`; magazine rounds per gun saved in `GunRounds`, drawn
      guns chamber a round, R reloads / clears a jam (R still cycles rain without a gun), jam chance grows with wear,
      per-weapon noise to `NpcDirector.Noise`, recoil shake, aim zoom). `Projectile`: arrows / bolts / stones / flares
      with gravity, stick where they land (arrows and bolts picked up again, stones return as stone), NPCs bleed
      (`Npc.Bleed`), flares light up, ignite dry ground and are seen 60 m off. Melee: spear and knife use the new
      `ToolStyle.Thrust`; nail bat, knife, spear, leaf-spring blade open bleeding wounds (`MeleeTool.bleeds`). Aim:
      RMB (or pad LT) slows you, halves spread, pulls the third-person camera over the shoulder, zooms first person
      (scoped rifle more), and in the top-down views turns you to the cursor with a crosshair on the HUD; the ammo
      line shows rounds / magazine + reserve, RELOADING, JAMMED. Smoke bombs (cloud after a fuse) and rocks (a knock
      and a noise where they land that draws NPCs away; the stone can be picked up). Recipes at the workbench /
      gunsmith / chem lab (gunpowder already comes from charcoal, sulfur and compost nitre); loot and trade prices.

*Ties:* hunting, raiders, crafting, mining (explosives).

## 14. Vehicle tuning `T2`
- [x] **Tuning bench** (garage station) with a stat card: power, torque, top speed, weight, grip, braking
- [x] **Engine map**: power ↔ economy ↔ reliability; **turbo / supercharger** parts; **nitrous** (bottles)
- [x] **Gearing**: short / long ratios, final drive
- [x] **Suspension**: ride height, stiffness, damping (saved per vehicle)
- [x] **Brakes**: bias and upgrades; **tyre pressure** (low = soft-ground grip, slower, wears)
- [x] **Weight**: strip the interior / add ballast
- [x] **Dyno** graph on the bench
      *Done:* `VehicleTuning` on every driveable vehicle (offsets from the design, written by `Apply()`, saved in
      `VehicleSave.tuning`): engine map ±15 % torque vs fuel and heat, turbo kit (boost above ~45 % revs) and
      supercharger kit (+20 %, thirsty), nitrous bottles (Left Ctrl / pad left stick: 5 s of +60 %, blue flame,
      doubles heat; HUD "NOS Xn"), gearing ±15 %, final drive, ride height, springs, dampers, brake bias (per-wheel
      share in `VehicleDriver`) and two brake upgrades, tyre pressure (grip on soft ground vs rolling and wear),
      ballast in 25 kg stone steps (`VehicleChassis.tuneMass`) and a stripped interior. The TUNING page ([T] at a
      garage or the new `tuning_bench` piece, wrench in the pack) gates settings by Mechanics level and shows the stat
      card (kW / hp, Nm, top speed from gearing, drag and rolling, weight, braking, grip) and a dyno of torque and power
      over the rev range. Kits at the garage / chem lab.

*Ties:* economy, races (NPCs), mechanics skill gates, armour weight.

## 15. Economy `T2`
*Exists:* scrap as money, vendors with daily stock, barter bonuses.

- [x] **Regional prices**: each town has supply and demand per good; prices drift; selling floods a market
- [x] **Bounty board**: raider bosses, pests, wanted people → scrap and reputation
- [x] **Delivery contracts**: haul crates between towns (truck bed / trailer), deadline, raider risk
- [x] **Player stall**: sell goods to passers-by (passive income)
- [x] **Guild chits**: light second currency from the Fuel Guild
      *Done:* `Market`: 11 goods, a standing price per town from the world (village / town / city, oil fields, ore
      deposits, nuclear and desert ground) × pressure (selling floods, buying drains; −15 % a day back to normal) ×
      a daily wobble; roadside vendors +10 % / −10 %; towns got names; the trade page and NPC rumours say what is cheap
      and dear where. `Contracts` + a `BountyBoard` in every settlement (NpcDirector): today's wanted raider boss (per
      convoy), gang culls, pests (reported by animals), and Fuel Guild hauls — `cargo_crate` parts to strap into any
      cargo socket or load loose, a deadline in days, raiders spot you 40 % farther while hauling; claims at any board,
      crates handed in at the destination's; pay in scrap, chits and reputation; saved. `coin_chit`: fuel vendors take
      them at 6 scrap. `player_stall` piece (`PlayerStall`): stock it, passers-by buy at 80 % of the local price
      (towns > roads > wilds, more people = more sales), takings in its cash box.

*Ties:* NPCs, factions, vehicles, farming, mining, refining.

## 16. Visuals `T2`
- [x] Heat haze over hot desert ground
- [x] Night sky: stars, moon; lightning flashes
- [x] Dust trails behind vehicles (speed × dryness); **mud on vehicles** that rain washes off
- [x] Headlight beams in dust and rain
- [x] Muzzle flashes, spent casings, explosion shockwave rings
- [x] Colour grading per biome and weather
- [x] Footprints and tyre tracks in snow and sand that fade
      *Done:* a grade pass (`Hidden/MadMax/Grade`, material in Resources, last blit of `PixelArtCamera`): tint,
      saturation and contrast per biome (warm desert, lush tropics, sickly nuclear zone) and weather / night, heat
      haze (whole-pixel shimmer low on screen when it is hot, dry and bright), lightning flash. `Atmosphere`: star dome
      and moon (rises at dusk) in the perspective views, lightning bolts with a double flash on each thunderclap.
      Tyres throw dust on dry loose ground (biome colour); `VehicleGrime` cakes mud splatters on vehicles in mud and wet
      ground (shader `_Dirt` / `_DirtTop`), rain rinses and fording washes it. Headlight beam cones show in rain, fog
      and dust. Guns flash (pooled `Fx.Flash` lights) and eject brass; blasts send a shockwave ring and a dust ring.
      `Fx.Track` / `Fx.Footprint`: tyre tracks and footprints in snow, sand and mud, fading over 90 s.

*Ties:* weather, environment, vehicles, weapons.

## 17. Environment `T2`
*Exists:* biomes, rain / snow, day / night, lakes, fire, radiation, sites, ground flora, overgrowth, wind.

- [x] **Seasons** advance with the days (temperature, snow, autumn colours)
- [x] **Dust storms**: moving fronts, low visibility, sandblasting, drifting sand
- [x] **Radiation storms** in the fallout zones
- [x] **Thunderstorms**: lightning strikes (fires, electrified metal)
- [x] **Ruts heal** over days (rain speeds it up)
- [x] **Caves** in mesas (ore, bats, shelter)
      *Done:* `Weather.Season` turns every `DaysPerSeason` days (new game: SEASONS TURN every 2/4/8 days or never;
      summer → autumn → winter → spring with their temperatures); the global `_MadMaxAutumn` turns swaying foliage gold
      and rust through autumn, holds in winter and greens up in spring. `World/Storms`: dust storms roll over the
      desert when it is hot and dry (fronts build over 45 s, blow for 3–6 min, pass): sand streaks on the wind, fog
      and a sandy grade, sandblasting an uncovered face (a mask or scarf stops it), raiders half-blind, vehicles
      dusted, ruts drifting over six times faster. Radiation storms flare in the nuclear zone: green motes, a heavy
      dose outdoors (a roof or an interior cuts it to 15 %, the cab to 35 %). Lightning seeks tall things near the
      player, breaks what it hits, lights flammable props and dry forest ground, electrifies vehicles within 12 m
      for a few seconds (the cab keeps you safe; touching one outside shocks), and strikes a walker down. Ruts and
      berms from tyres heal by half each day (three times faster in rain) while terraforming stays. Mesa tunnels
      carry ore seams in their walls (the mapped deposit, else a seeded pick of iron, copper, coal, tin, sulphur or
      lead; mined like any voxel), a bat roost in the deepest roofed stretch (flushes at 7 m or a light at 14 m) and
      an old camp: a guttering oil lamp and a crate (`cave` loot table: guano, tins, a lantern, dynamite, bait).

*Ties:* gardening, clothing, visuals, animals, mining.

## 18. Base building `T2–T3`
*Exists:* walls, doors, floors, roofs, stairs, utility grid, lockable doors, pieces on vehicles.

- [x] **Foundations** on slopes, **ramps**
- [x] **Garage door** (wide, powered), shutters
- [x] **Structural support**: unsupported pieces collapse
- [x] **Blueprints**: save a structure and place it again
- [x] **Defences**: spike wall, barbed wire, powered auto-turret, alarm bell, floodlights
- [x] **Claim flag**: your territory; raiders may raid it
- [x] **Upgrade in place** (wood → brick → concrete) and **repair** with the hammer
      *Done:* timber and stone **foundations** (2 × 2 m deck on 1.9 m stilts / a stone plinth) stand level on slopes
      (origin on the highest ground under the footprint, refused past 1.8 m of fall) and tile against the foundation
      under the cursor; wood / concrete **ramps** (5.9 m, rising 1 m under 10°). Floors, foundations, ramps and the
      garage slab are drivable: `StructureGround` adds upright decks to the wheels' analytic ground (firm, no ruts),
      and a carried body ignores the deck colliders so it never snags on lips and seams. **Garage doorway** (4 m
      concrete frame) + **garage door** (`RollerDoor`, rolls up into the drum: 3.5 s on its 400 W motor, 11 s cranked
      by hand without power, lockable) and **window shutters** (snap over window walls; `FurnitureDef.snapTo`
      generalises the door → doorway snap). `StructureSupport`: a piece stands if touching pieces lead down to the
      ground (terrain, rock, a world building); breaking or dismantling a piece brings down whatever it held up,
      lowest first (a quarter of the cost survives). **Structure plans** (`StructurePlans`, the NEW STRUCTURE PLAN
      tool in the Structure category): LMB on your building saves every connected piece (kits left out, 6 plans,
      saved); each plan shows as a piece with the whole structure as its ghost and the summed cost, X forgets it. New
      **Defence** category: spike wall and barbed wire (`DefenceHazard`: stakes wound walkers and gouge / puncture
      rammers, wire snags walkers to 30 % speed and wraps axles; both wear as they work), **auto-turret** (powered, MG
      belts in its own ammo box, tracks hostiles within 30 m and sight, never the player), **alarm bell** (rings by
      itself at hostiles within 35 m, [E] by hand); floodlights were already a kit. **Claim flag** (40 m, shows pieces
      and a defence score, [T] respawn here). `Npc/BaseRaid` sends a gang at the most built-up claim every
      1.5 / 3 / 6 days (new game: RAIDS ON BASES): live when the player is within 250 m (they gather 75 m out at dusk,
      batter the nearest pieces, fight the player when close; repelled = +2 reputation, else they loot a container and
      leave after 7 min), off-screen otherwise (defence score vs strength → wrecked outer pieces + looted stores,
      reported on return); sleeping at the claim on a raid night wakes you at 2:00 to the bell. Build mode: [U]
      upgrade in place (wood → brick → concrete walls / doorways, floors, foundations, ramps, doors, fences; pays the
      new cost, half the old back), [R] repair (a share of the cost for the missing condition); the status line shows
      condition and the upgrade.

*Ties:* factions (raids), economy, gardening, irrigation, power.

## 19. Vehicles `T2–T3`
*Exists:* 21 vehicles and machines, trailers, crane / winch / towing, fuel systems, damage.

- [x] New: **bus** (walk-in mobile home), **dune buggy**, **APC** 6×6, **semi tractor** + box trailer,
      **ambulance** (heals inside), **monster truck**
- [x] **Paint shop**: repaint (palette swap) and faction decals
- [x] **Horn**: NPCs react, raiders read it as a parley signal, animals scatter
- [x] **Maintenance**: oil changes, filters, plugs (engine wear)
- [x] **Passengers**: companions ride along; ride as a passenger
      *Done:* `Designs/RoadmapVehicles.cs` — school bus mobile home (walk-in: bed, sofa, table, kitchen, locker),
      high-roof ambulance (walk-in bay: `MedicalBay` bandages, splints and heals 5× while you are inside; emergency
      bar `lights_emergency` sweeps red/blue with the lights on and turns the horn into a siren), APC 6×6 (V-hull,
      front two axles steer, vision blocks, roof MG, smoke dischargers), long-nose semi with sleeper and fifth
      wheel + box semi-trailer (landing legs, 3 t cargo hold), monster truck (`wheel_monster` 1.7 m tyres, flames),
      tube-frame dune buggy (air-cooled, rear engine). All join the fleet, wrecks and trailers. Paint station piece
      (Industry): `VehiclePaint` swaps the vehicle's main paint ramp (bevel shades kept; stripes, crosses and flames
      stay) for 11 colours (dyes or scrap + oil), previewed live and paid on SPRAY; `Decals` — gang emblems, Fuel
      Guild, Last Engine, Salt Nomads, Bunker Remnants, red cross, flames, checkers, shark teeth; a gang's emblem cuts
      that gang's sight range to 35 %. Horn (Y / middle mouse / right stick): people turn and the nervous jump clear,
      a trader convoy pulls over, a blocking raider gang opens the parley, a friendly gang honks back
      (`NpcDirector.Horn`; animals hook in with roadmap 23). Maintenance in `VehicleSystems`: oil life, air filter
      (dust, dust storms) and spark plugs (petrol) wear with running hours → OIL CHANGE DUE (engine wear), AIR FILTER
      CLOGGED (power), MISFIRING; G services with `use_oil_filter` + a sump of oil, `use_air_filter`,
      `use_spark_plugs` (workbench recipes, parts traders, garage loot); dashboard lamp S; saved per vehicle.
      `PassengerSeat` on every drivable vehicle: [E] RIDE ALONG with a trader convoy (10 scrap; the convoy keeps
      driving its road) or another player's vehicle; F gets out at the door. Companions will use the same seat.

*Ties:* tuning, armour, attachments, NPCs, economy.

## 20. NPCs `T3`
*Exists:* shopkeepers, residents, stalls, wanderers, traders, raider hordes, dialogue with tones, backstories,
rumours, errands, haggling, parley, combat, ragdolls.

- [x] **Companions**: recruit (charisma / pay) → follow, fight, carry, guard the base, drive a second vehicle
- [x] **Schedules**: sleep at night, shops open by day, campfires in the evening
- [x] **Town leaders** with quest chains
- [x] **Combat AI**: cover, flanking, retreat, surrender
- [x] **Pack traders** walking with animals
      *Done:* `Npc/Companions` — ask a wanderer or resident ([CHA 6] check, or 80 scrap; one companion, two at CHA 7,
      three at CHA 9, also a spared raider at CHA 7). They follow (and board your passenger seat; left behind they catch
      up), wait, guard a claim (+3 defence each in `ClaimFlag.Defence`), drive a spare fleet vehicle behind you
      (`AiDriver.Goal.Escort`), fight any hostile near you or them, and carry a 25 kg pack ([T]); dismissed they drop
      it and walk off; saved (`SaveData.companions`). Schedules (`Npc.Routine`): shops trade 7:00–20:00, stallkeepers
      and bosses sleep at night, residents gather round the town campfire in the evening (`World/Campfire`: stone
      ring, sitting logs, flames and light at dusk) and go indoors at night, wanderers light their own fire and sit
      up by it. Town bosses (`NpcRole.Leader`, one per settlement) run a 4-stage chain (`Npc/TownQuests`): supply run,
      cull the local gang (a bounty contract), courier to the nearest town's boss and back, hold the town against a
      dusk raid — each pays scrap, chits and reputation (last: a blower V8 and friends' prices, 10 % off, in that
      town); saved. Combat AI: gunmen move to cover (a spot with something solid between them and the target), melee
      fighters fan out and come in from the sides, the badly hurt break off once, beaten non-bosses may surrender
      (more likely with their boss dead or alone) — spare them (+rep), take their pockets, or recruit them; killing
      a surrendered one costs 10 reputation. Fighters hit back at whoever hurt them, so raiders and companions fight
      each other. Pack traders (`NpcRole.Packer`) walk beats along the roads with a voxel pack mule (`PackAnimal`)
      selling food, medicine, ammo, filters and dyes.

*Ties:* factions, economy, animals, base building.

## 21. Factions `T3`
- [x] **Registry**: town settlers, Fuel Guild, Church of the Last Engine, Salt Nomads, Bunker Remnants and the four
      raider gangs
- [x] **Reputation** per faction with ranks (hostile → allied); actions shift it; **relations** between factions
- [x] **Territories**: each town and region belongs to a faction
- [x] **Faction leaders & jobs**; rewards: discounts, gear, safe passage, guards
- [x] **Events**: raider attacks on towns you can defend, Guild convoys to escort
      *Done:* `Npc/Factions` — nine factions, standing −100..100 in ranks HUNTED / HOSTILE / DISTRUSTED / NEUTRAL /
      FRIENDLY / TRUSTED / ALLIED (the settlers' is the old reputation), a relations matrix (settlers and Guild allied,
      Church and Rustmen both worship engines, gangs feud with each other, Remnants against the Church) — every shift
      ripples a third to friends (same way) and enemies (other way). Deeds: kills (a raider hurts their gang, pleases
      their enemies; killing peaceful folk or someone who surrendered costs a lot), parley outcomes (pay, recruit,
      threaten), mercy, bounties, hauls and escorts (Guild), town bosses' jobs (the town's faction), faction supply
      jobs. Territories: settlements belong to settlers, the Church, the Nomads (desert towns) or the Remnants
      (cities); roads to the gang that rides them — crossing in shows whose ground it is and your standing.
      Effects: traders price by their faction's standing (−15 % allied … +40 % hunted) and hostile factions refuse to
      trade; FRIENDLY gangs give safe passage (they honk instead of blocking), HOSTILE ones spot you from 30 % farther;
      a gift at TRUSTED (chits and fuel, medicine, blessed nitrous, a shemagh and canteen, a geiger and hazmat, a skull
      mask) and a guard companion at ALLIED (settlers, Guild, Remnants); Guild colours on your vehicle add 10 % to
      haul and escort pay. Faction jobs on boards: supply runs by the town's faction (Church fuel offerings, Nomad
      water, Remnant copper, settler firewood). Events: a gang hits the town you are in at dusk now and then (beat
      them: +60 scrap, +8 standing); Guild escort jobs (`Npc/GuildEscort`): a Guild rig in Guild colours drives the
      road to the next town, waits when you fall behind, is ambushed halfway, pays on arrival. Standings on the P
      page; saved (`SaveData.factions`).

*Ties:* NPCs, economy, dialogue, base raids, bounties.

## 22. Player parkour `T3`
- [x] **Vault** low obstacles (0.5–1.2 m)
- [x] **Mantle / climb** ledges up to 2.3 m (crates, cars, walls, roofs)
- [x] **Crouch & slide**
- [x] **Landing roll** cuts fall damage; sprint jumps
- [x] **Athletics** skill
- [x] **Ride on vehicles** (truck beds, roofs)

*Ties:* combat, bunkers, base defence, injuries (falls), grappling hook.

*Done:* `PlayerCharacter.Parkour` (partial). Space at a wall-like face (never a slope or a person) probes the top: 0.45–1.2 m vaults over and down the far side (or steps up when it is deep), walls too thin to stand on are climbed over, ledges to 2.3 m are mantled when a standing capsule fits on top (reach → pull-up → press over, keyed arm/knee poses; hop for high edges); a vault never drops more than 1.5 m below the feet. Ctrl / R3 crouches (capsule 1.3 m, 45 % speed, the animator now lowers the pelvis for bent knees so feet stay planted), crouching at a run slides (capsule 1.0 m, decaying speed, dust); standing up waits for headroom. Holding Ctrl as you land a hard fall rolls (a third of the damage, a short roll forward); the fall threshold rises with Athletics. Running jumps are higher and carry 12 % further. Standing on anything moving carries you with its point velocity and turns you with it; standing still on a vehicle doing > 4 m/s braces on a hidden standing perch (let go = walk). New `Skill.Athletics` (Agility; practised by all of it: quicker climbs, longer slides, quicker feet). Crouching halves raider convoy sight and lets hostile people notice you at 28 m instead of 60 m. `GrappleTool` (`tool_grapple`, workbench: iron 3, cloth 3, rubber 1; bunker loot): aim with RMB, 24 m rope — top surfaces land you on them, walls leave you hanging below the hook and climb over an edge in reach.

## 23. Animals `T3`
- [x] **Framework**: voxel quadruped and bird rigs with procedural gaits; senses (sight, smell with the wind,
      hearing); needs; health; drops (meat, hide, bone, feathers)
- [x] **Wild**: wild dogs (packs), rad-boars (charge), antelope (flee), vultures (circle carcasses), snakes, rats,
      wild horses
- [x] **Farm**: chickens (eggs), goats (milk), cows (milk, leather), pigs; guard dog
- [x] **Pens, troughs, feeding, breeding and growth**
- [x] **Horse taming**: calm approach, feeding, trust → saddle → **riding** (walk, trot, gallop, stamina),
      saddlebags, care
- [x] **Hunting**: stealth against wind and noise, skinning, tanning

*Ties:* gardening (feed, manure), crafting (leather), economy, NPC traders, parkour (mounting), weapons.

*Done:* `Runtime/Animals`. `AnimalLibrary` (14 species as data: proportions, coat ramps incl. new `Pal.Fur`/`Pal.Pink`, features, senses, herd sizes, biomes, drops, products, likes), `AnimalModels` (procedural voxel quadrupeds — barrel torso with belly shading, spots, bristle ridge, neck + head with snout, ears, horns, tusks, mane, beard, udder, legs with hooves or paws, tails — plus birds with folding / fingered wings and patterned snakes with a rattle), `AnimalCall` (procedural barks, growls, howls, neighs, snorts, moos, bleats, grunts, clucks, squeaks, screeches and rattles through `SynthVoice`). `Animal`: kinematic capsule (hits, run-overs), ground following over floors, obstacle / water / cliff avoidance, gaits (walk / trot / gallop leg phases, bob and pitch; hens bob and peck; vultures flap, glide and bank; snakes slither and coil), sleeping at night (nocturnal ones by day). Senses: sight (crouch halves it, night dims it), hearing (running, engines; `NpcDirector.Noise`/`Horn` now reach animals — gunshots scatter packs), smell only downwind (`Fx.Wind`). Natures: prey bolt with the herd, boars charge and run past, dog/wolf packs stalk in a ring and dart in by turns (bolder at night, when you are hurt, or in numbers; break when half the pack is down; chase cars; raid livestock at night), vultures circle fresh bodies (animals and dead people) and land when nobody is near, rats scatter (bite at night in fallout zones), rattlesnakes rattle then strike (venom = sick). Bites are a new injury cause `BITE` (limbs, leg armour counts). `Carcass`: butcher with a blade for hide and trophies (tusks, horns, wolf pelt, skulls), meat, bone, feathers; rots after 1.5 days. Hunting kills report pest contracts (dog, boar, wolf, rat now exist). `AnimalDirector`: deterministic herds per 200 m cell and biome (spawn ≤ 150 m, fold ≥ 220 m, ≤ 45 wild, a hunted herd stays smaller for 3 days), village livestock (killing it angers the Settlers), vulture flocks, kept animals saved (`SaveData.animals`). Farm: chicks, goat kids, calves, piglets, puppies bought from food / pack vendors (`animal_*` items released at your feet), `trough` (crops / scraps + water, rain refills) and `nest_box` pieces; once a day kept animals eat (trough near home, hand-fed, or grazing in green lands), hens lay (into a nest box), goats and cows give milk ([E]), young grow up (size), fed pairs breed, the forgotten go hungry, then starve or wander off. Guard dogs (bought or a tamed wild dog) follow, stay or guard and fight hostile people and dangerous animals. Horses: crouch-walk close, stay calm, offer apples / carrots / sugar / corn until trust reaches 100 % → yours; `use_saddle` (workbench: leather 4, iron 1, cloth 2) → [E] ride: W trot, Shift gallop on horse stamina (HUD bar), Space jumps fences, a crash at a gallop throws you, [T] saddlebags (40 kg), F dismounts (rider sits astride, reins pose). Recipes: fried eggs, cheese, fletched arrows (feathers), bone meal. Tanning (existing rack) now has a hide source.


## 24. Two-wheel vehicles `T3`
- [ ] **Bike physics**: two wheels, lean into turns (counter-steer), wheelies, crashes throw the rider (ragdoll)
- [ ] **Dirt bike, chopper, bicycle** (pedal power = stamina, silent), **sidecar** outfit
- [ ] Same part system (engines, wheels); rider lean pose
- [ ] **Raider bikers** flanking in convoys

*Ties:* vehicles, ragdolls, raiders, tuning.

## 25. Aviation `T3`
- [ ] **Flight model**: lift / drag / thrust / control torques per surface; stall; crash damage
- [ ] **Gyrocopter** (autogyro: rotor spun by airspeed, pusher prop) and **ultralight** trike
- [ ] **Flight HUD**: altitude, airspeed, compass; chase camera
- [ ] **Airstrips & hangar**; take off from straight roads
- [ ] **Aerial scouting**: sites and convoys show up from the air

*Ties:* vehicles, crafting, exploration, economy (fast deliveries).

---

## Gaps & suggestions

*(written after the full review at the end of the roadmap work)*

Found while testing base building (Roadmap 18):
- [ ] **Burning wrecks near the start**: a wreck spawned on fire ~17 m from the starting fleet; the fleet caught
      (Interceptor, Excavator: OnFire + Seized). Keep ignition-prone wrecks away from the spawn, or spawn them cold.
- [ ] **Hill starts**: heavy cars (Scavenger, 2.7 t) cannot pull away on a 10° slope at part throttle (1st gear at
      ~1500 rpm gives ~430 N per wheel); add launch torque / clutch slip so ramps and hills don't need a run-up.
- [ ] **Stall overheating**: a car pinned against an obstacle at full throttle cooks its engine and catches fire in
      well under a minute; slow the heat build-up or cut the throttle with a rev limiter warning.

Ties that would pull the loop together:
- [ ] **Raids by territory**: the raiding gang is the one whose road stretch is nearest (`Convoy` gangs); recruited
      or bribed gangs skip the base, wiping a gang's raid party dents its next convoy generation.
- [ ] **Radio warns of raids**: WasteTalk FM names settlements (and your claim) on a gang's warpath an hour ahead.
- [ ] **Motion-sensor floodlights**: [T] on lights: SENSOR (on when someone moves within 15 m); raiders avoid lit
      approaches.
- [ ] **Base upkeep**: pieces weather slowly (rain on wood, sand on everything) unless roofed or repaired; a claim
      shows what needs the hammer.
- [ ] **Garage as the fleet's home**: vehicles parked on a claim's decks repair slowly with a garage piece, refuel
      from its tanks, and are what Tab (fleet) cycles first; raiders go for parked vehicles.
- [ ] **Map markers**: claims, raided pieces, plans' outlines on the minimap; a claim is a fast respawn (done) and a
      waypoint.
- [ ] **Companions guard the base** (Roadmap 20): recruited NPCs posted at a claim man turrets and sound the bell.

