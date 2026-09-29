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

- [ ] **Coverage zones**: front, sides, rear, roof, wheel guards, window grilles on every vehicle
- [ ] **Materials**: scrap sheet, steel plate, tyre-rubber composite (absorb, weight, fire resistance)
- [ ] **Damage routing**: crashes, gunfire and fire hit the zone's armour first; armour loses condition and falls off
- [ ] **Window grilles** stop shots at the driver (slightly blocked view)
- [ ] **Weight** penalty on handling and fuel

*Ties:* raiders, tuning, crafting, welder.

## 12. Player armor `T2`
- [ ] **Zones**: helmets, chest plates (scrap plate, tyre-rubber vest, kevlar), arm guards, gauntlets, shin guards
- [ ] **Protection per damage type** (melee, shot, crash, fall, burn) reduces damage and injury chance on the
      covered zones (ties the injury system)
- [ ] **Weight & noise**; durability and repair
- [ ] Raiders drop their armour

*Ties:* injuries, crafting, raiders, economy.

## 13. Player weapons `T2`
*Exists:* melee tools, pipe shotgun, molotov.

- [ ] Melee: spear (reach), nail bat, knife (fast, bleeds), leaf-spring blade
- [ ] Ranged: slingshot, bow & arrows (silent), crossbow, pipe pistol, revolver, bolt rifle, flare gun
- [ ] **Ammo crafting** (arrows, bolts, shells, cartridges; gunpowder from charcoal + sulfur + saltpeter)
- [ ] **Aim mode** (hold RMB): crosshair, shoulder aim, iso aims at the cursor
- [ ] **Reload, magazines, jams** for crude guns
- [ ] **Noise** alerts NPCs and animals
- [ ] Throwables: pipe bomb, smoke bomb, rock

*Ties:* hunting, raiders, crafting, mining (explosives).

## 14. Vehicle tuning `T2`
- [ ] **Tuning bench** (garage station) with a stat card: power, torque, top speed, weight, grip, braking
- [ ] **Engine map**: power ↔ economy ↔ reliability; **turbo / supercharger** parts; **nitrous** (bottles)
- [ ] **Gearing**: short / long ratios, final drive
- [ ] **Suspension**: ride height, stiffness, damping (saved per vehicle)
- [ ] **Brakes**: bias and upgrades; **tyre pressure** (low = soft-ground grip, slower, wears)
- [ ] **Weight**: strip the interior / add ballast
- [ ] **Dyno** graph on the bench

*Ties:* economy, races (NPCs), mechanics skill gates, armour weight.

## 15. Economy `T2`
*Exists:* scrap as money, vendors with daily stock, barter bonuses.

- [ ] **Regional prices**: each town has supply and demand per good; prices drift; selling floods a market
- [ ] **Bounty board**: raider bosses, pests, wanted people → scrap and reputation
- [ ] **Delivery contracts**: haul crates between towns (truck bed / trailer), deadline, raider risk
- [ ] **Player stall**: sell goods to passers-by (passive income)
- [ ] **Guild chits**: light second currency from the Fuel Guild

*Ties:* NPCs, factions, vehicles, farming, mining, refining.

## 16. Visuals `T2`
- [ ] Heat haze over hot desert ground
- [ ] Night sky: stars, moon; lightning flashes
- [ ] Dust trails behind vehicles (speed × dryness); **mud on vehicles** that rain washes off
- [ ] Headlight beams in dust and rain
- [ ] Muzzle flashes, spent casings, explosion shockwave rings
- [ ] Colour grading per biome and weather
- [ ] Footprints and tyre tracks in snow and sand that fade

*Ties:* weather, environment, vehicles, weapons.

## 17. Environment `T2`
*Exists:* biomes, rain / snow, day / night, lakes, fire, radiation, sites, ground flora, overgrowth, wind.

- [ ] **Seasons** advance with the days (temperature, snow, autumn colours)
- [ ] **Dust storms**: moving fronts, low visibility, sandblasting, drifting sand
- [ ] **Radiation storms** in the fallout zones
- [ ] **Thunderstorms**: lightning strikes (fires, electrified metal)
- [ ] **Ruts heal** over days (rain speeds it up)
- [ ] **Caves** in mesas (ore, bats, shelter)

*Ties:* gardening, clothing, visuals, animals, mining.

## 18. Base building `T2–T3`
*Exists:* walls, doors, floors, roofs, stairs, utility grid, lockable doors, pieces on vehicles.

- [ ] **Foundations** on slopes, **ramps**
- [ ] **Garage door** (wide, powered), shutters
- [ ] **Structural support**: unsupported pieces collapse
- [ ] **Blueprints**: save a structure and place it again
- [ ] **Defences**: spike wall, barbed wire, powered auto-turret, alarm bell, floodlights
- [ ] **Claim flag**: your territory; raiders may raid it
- [ ] **Upgrade in place** (wood → brick → concrete) and **repair** with the hammer

*Ties:* factions (raids), economy, gardening, irrigation, power.

## 19. Vehicles `T2–T3`
*Exists:* 21 vehicles and machines, trailers, crane / winch / towing, fuel systems, damage.

- [ ] New: **bus** (walk-in mobile home), **dune buggy**, **APC** 6×6, **semi tractor** + box trailer,
      **ambulance** (heals inside), **monster truck**
- [ ] **Paint shop**: repaint (palette swap) and faction decals
- [ ] **Horn**: NPCs react, raiders read it as a parley signal, animals scatter
- [ ] **Maintenance**: oil changes, filters, plugs (engine wear)
- [ ] **Passengers**: companions ride along; ride as a passenger

*Ties:* tuning, armour, attachments, NPCs, economy.

## 20. NPCs `T3`
*Exists:* shopkeepers, residents, stalls, wanderers, traders, raider hordes, dialogue with tones, backstories,
rumours, errands, haggling, parley, combat, ragdolls.

- [ ] **Companions**: recruit (charisma / pay) → follow, fight, carry, guard the base, drive a second vehicle
- [ ] **Schedules**: sleep at night, shops open by day, campfires in the evening
- [ ] **Town leaders** with quest chains
- [ ] **Combat AI**: cover, flanking, retreat, surrender
- [ ] **Pack traders** walking with animals

*Ties:* factions, economy, animals, base building.

## 21. Factions `T3`
- [ ] **Registry**: town settlers, Fuel Guild, Church of the Last Engine, Salt Nomads, Bunker Remnants and the four
      raider gangs
- [ ] **Reputation** per faction with ranks (hostile → allied); actions shift it; **relations** between factions
- [ ] **Territories**: each town and region belongs to a faction
- [ ] **Faction leaders & jobs**; rewards: discounts, gear, safe passage, guards
- [ ] **Events**: raider attacks on towns you can defend, Guild convoys to escort

*Ties:* NPCs, economy, dialogue, base raids, bounties.

## 22. Player parkour `T3`
- [ ] **Vault** low obstacles (0.5–1.2 m)
- [ ] **Mantle / climb** ledges up to 2.3 m (crates, cars, walls, roofs)
- [ ] **Crouch & slide**
- [ ] **Landing roll** cuts fall damage; sprint jumps
- [ ] **Athletics** skill
- [ ] **Ride on vehicles** (truck beds, roofs)

*Ties:* combat, bunkers, base defence, injuries (falls), grappling hook.

## 23. Animals `T3`
- [ ] **Framework**: voxel quadruped and bird rigs with procedural gaits; senses (sight, smell with the wind,
      hearing); needs; health; drops (meat, hide, bone, feathers)
- [ ] **Wild**: wild dogs (packs), rad-boars (charge), antelope (flee), vultures (circle carcasses), snakes, rats,
      wild horses
- [ ] **Farm**: chickens (eggs), goats (milk), cows (milk, leather), pigs; guard dog
- [ ] **Pens, troughs, feeding, breeding and growth**
- [ ] **Horse taming**: calm approach, feeding, trust → saddle → **riding** (walk, trot, gallop, stamina),
      saddlebags, care
- [ ] **Hunting**: stealth against wind and noise, skinning, tanning

*Ties:* gardening (feed, manure), crafting (leather), economy, NPC traders, parkour (mounting), weapons.

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
