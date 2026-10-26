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

Next quality programme: [unattended acceptance stages Q0–Q8](#26-quality-of-life--unattended-acceptance-playtests-t2t3--planned)
and [world-coherence audit W0–W3](#27-world-coherence--gaps-to-investigate-and-close-t2t3--planned).

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
- [x] **Grappling hook**: climb walls / ledges (ties parkour), pull loose parts to you
- [x] **Fishing rod, hoe, watering can** (see Fishing, Gardening)

*Done (late):* `GrappleTool` (roadmap 22) zips you up walls and roofs and reels loose parts / crates under 300 kg to your feet; the fishing rod, hoe and watering can shipped with roadmaps 8–10.

*Audit (2026-10-13):* the geiger now reads louder pointed at a source; uranium ore / glowing fish on the ground and loot spots in the fallout are hot (`Hazard`); the grapple and watering can wear.

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
- [x] **Backpacks** (back slot): school bag 10 kg, hiking pack 26 kg, frame pack 40 kg in their own storage (see bags below)
- [x] **Bags with their own storage** (2026-10-01): backpack, tourist frame pack, military MOLLE pack, handcrafted
      stick pack (wears out), school bag, hiking pack, leather satchel; belts (work, gun, tool belt: tools drawn from
      the hotbar; fanny bag); one-shoulder bags (shoulder bag, sling bag, canvas satchel); hand luggage (duffel,
      suitcase: both hands, slower, [Q] sets it down); back brace. A worn bag is a `Container` (`worn`,
      `WastelandGame.WornStorage`); off the body its contents ride in its item key (`cloth_<bag>~<contents>`), so a
      dropped backpack keeps them ([T] looks inside), saves and replicates as an item. `Game/Bags/BagLibrary`,
      `WastelandGame.Bags`, scenarios `bags.*`
- [x] **Back strain** (2026-10-01): load over 70 % of the carry capacity (Athletics, a brace raise it; hip belts and
      frames carry part of a bag's weight, one-sided and hand loads pull) strains the back, faster running, swimming,
      jumping and climbing; HUD BACK % warning, then a STRAINED BACK (torso: no running or jumping, slower, less
      stamina) mended by rest, sleep, painkillers, a brace
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

*Audit (2026-10-13):* the water tank part got a hose (`TankHose`: [E] fill from / [T] empty into a built water piece within 8 m, pump lake water in) so it carries water to farms; the MG keeps a part-used belt across saves; spilled oil / fuel / ice now take grip without a dropper patch around.

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

*Audit (2026-10-13):* `bp_framepack` now gates the frame pack recipe; goods left in a station's tray keep their own make until collected (`CraftingStation.trayMake`, saved).

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

*Audit (2026-10-13):* the watering can's fire check no longer halves fires by itself (`Fire.Douse` with 0 L is a probe).

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

*Audit (2026-10-13):* rain collectors under a roof catch nothing.

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

*Audit (2026-10-13):* fire around the vehicle chars the plates (`VehicleArmor.Scorch` from `VehicleSystems.Heat`) and burnt-through ones fall off.

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
- [x] **Weight of a blow** (2026-10-08): two-handed sledge / pick swing as a full-body clip (`sledge`: stance, lift
      up the side, beat at the top, hips and knees drive it down ahead), the left hand kept on long handles by arm IK
      (`HandTool.SecondGrip`, `HumanAnimator.GripIK`), feet planted while it plays; hit-stop by hardness and tool weight,
      bounce off stone/metal, sparks or dust, camera jolt (`PlayerCharacter.Impact`, `MeleeTool.LastHit`); NPCs flinch,
      heavy blows stagger them back a step, a solid hit in the wind-up spoils their swing; the player flinches when
      struck. Scenario `combat.melee_feel`.
- [x] Blocking / parrying and directional hit reactions (left/right/behind); reactions on network proxies
      *Done (2026-10-13):* `WastelandGame.Guard` — in a fight (someone hostile within 7 m) RMB / pad LT with a melee tool
      raises the weapon across the body (`hold_port`, slower steps; RMB no longer opens the context menu then): a blow
      from the front within 0.25 s (+ Melee skill) of raising it is parried (no harm, the striker staggers back, 1.8 s
      before they swing again), later it is blocked for stamina (no wound, a flinch), out of stamina the guard breaks
      (full blow, stagger, 1.4 s without guard); sides and back get through. Armed fighters catch a share of blows from
      the front on their own weapon (`Npc.TryGuard`: raiders 25 %, bosses 40 %, proud / gruff +10 %; sledge-weight
      blows go through) — the player's tool rings off it. Every flinch / stagger also gives way from the blow's side
      (`HumanAnimator.State.hitFrom/hitT` → `HitLean`: back from the front, forward from behind, bent and twisted from
      a side). Proxies: snapshot flags `FlagReact/FlagStagger/FlagGuard` replay reactions and guards on clients, a
      client's own hit plays at once with its side, proxies now also play the swing clips. Scenario `combat.guard`.
- [x] **Ammo crafting** (arrows, bolts, shells, cartridges; gunpowder from charcoal + sulfur + saltpeter)
- [x] **Aim mode** (hold RMB): crosshair, shoulder aim, iso aims at the cursor
- [x] **Reload, magazines, jams** for crude guns
- [x] **Noise** alerts NPCs and animals
- [x] Throwables: pipe bomb, smoke bomb, rock
      *Done:* `RangedTool` generalised (hitscan or `Projectile`; magazine rounds per gun saved in `GunRounds`, drawn
      guns chamber a round, R reloads / clears a jam, jam chance grows with wear,
      per-weapon noise to `NpcDirector.Noise`, recoil shake, aim zoom). `Projectile`: arrows / bolts / stones / flares
      with gravity, stick where they land (arrows and bolts picked up again, stones return as stone), NPCs bleed
      (`Npc.Bleed`), flares light up, ignite dry ground and are seen 60 m off. Melee: spear and knife use the new
      `ToolStyle.Thrust`; nail bat, knife, spear, leaf-spring blade open bleeding wounds (`MeleeTool.bleeds`). Aim:
      RMB (or pad LT) slows you, halves spread, pulls the third-person camera over the shoulder, zooms first person
      (scoped rifle more), and in the top-down views turns you to the cursor with a crosshair on the HUD; the ammo
      line shows rounds / magazine + reserve, RELOADING, JAMMED. Smoke bombs (cloud after a fuse) and rocks (a knock
      and a noise where they land that draws NPCs away; the stone can be picked up). Recipes at the workbench /
      gunsmith / chem lab (gunpowder already comes from charcoal, sulfur and compost nitre); loot and trade prices.

*Audit (2026-10-13):* the knife and crutch are one-handed (`HandTool.oneHanded`).

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

*Audit (2026-10-13):* moving bases (`RigVehicle`) keep the bench's settings after their re-tune (`VehicleTuning.Rebase`).

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

*Audit (2026-10-13):* roadside vendors pay 10 % less as documented (was ~1 %).

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

*Audit (2026-10-13):* roof MGs and NPC guns flash and eject brass too (`GunFx`).

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

*Audit (2026-10-13):* barbed wire wears as raiders struggle in it; upgrades refund half of the builder's price and keep doors locked.

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
      (dust, dust storms) and spark plugs (petrol) wear with running hours → DUE A SERVICE (engine wear), SPLUTTERS UNDER
      LOAD (clogged filter: power), MISFIRING (the HUD names symptoms); G services with `use_oil_filter` + a sump of oil, `use_air_filter`,
      `use_spark_plugs` (workbench recipes, parts traders, garage loot); dashboard lamp S; saved per vehicle.
      `PassengerSeat` on every drivable vehicle: [E] RIDE ALONG with a trader convoy (10 scrap; the convoy keeps
      driving its road) or another player's vehicle; F gets out at the door. Companions will use the same seat.

*Audit (2026-10-13):* the APC stowage bin / map case and the semi's sleeper locker exist (storage matched the wrong design names).

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

*Audit (2026-10-13):* town bosses spawn again in planned towns (the leader spot sat in the unplanned-town branch); a fleet car a companion drives is saved; pack traders walk a road beat; saddled animals take orders with run + [T]; a faction guard keeps gun and vest after a load.

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

*Audit (2026-10-13):* every standing change goes through `Factions.Shift` (errands, chores, threats, raids, races, crew medics); gang ground follows their road (`RaidersByRoad`, 120 m); Guild escorts pay Guild standing once, and an escort job running at save time fails on load.

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
- [x] **Bike physics**: two wheels, lean into turns (counter-steer), wheelies, crashes throw the rider (ragdoll)
- [x] **Dirt bike, chopper, bicycle** (pedal power = stamina, silent), **sidecar** outfit
- [x] Same part system (engines, wheels); rider lean pose
- [x] **Raider bikers** flanking in convoys

*Ties:* vehicles, ragdolls, raiders, tuning.

*Done:* `BikeBalance` on designs with `bike` (added by the builder): A/D asks for a lean (up to 34–44° by bike, scaled in from 2.5 m/s); the front wheel gets a counter-steer flick towards it and then the angle that balances the current lean at this speed (`VehicleDriver.steerOverride`); a roll PD holds it (weak in the air). Slow = feet down, direct steering; parked = on the kickstand. Shift at speed on the throttle lifts a wheelie balanced at 25°. Falling past 60°, looping a wheelie (> 65°) or hitting something hard at > 7 m/s throws the rider: `WastelandGame.ThrowRider` exits, ragdolls the player with the bike's velocity, hurts by speed (CRASH) and gets you up where you landed 2.6 s later; a downed bike is picked up when you get back on. Parts (`PartLibrary.Bikes`): spoked `wheel_bike` (knobby), `wheel_bike_street` (chrome, whitewall), `wheel_bicycle`; `engine_single` (thumper voice), `engine_vtwin` (45° potato-potato), `engine_pedals` (silent; power from stamina, practises Athletics; no fuel/oil/heat). Designs: `DirtBike`, `Chopper` (raked fork, ape hangers, forward controls, sissy bar), `Bicycle` (rack and crate), `SidecarOutfit` (3 wheels, no lean, CoM offset `comX`, idler `wheel_side` neither driven nor steered — lifts the chair on hard right-handers; gun socket on the chair's nose). Riders sit astride (`HumanAnimator.State.riding`, pedalling legs on bicycles) and lean with the bike; pillion / chair passengers via `PassengerEye`. Raider convoys bring 0–3 outriders (dirt bikes, choppers, sidecar gunners) that ride the target's flanks (`AiDriver.Goal.Flank`) and shoot from there; a downed biker counts as disabled (the crew fights on foot).


## 25. Aviation `T3`
- [x] **Flight model**: lift / drag / thrust / control torques per surface; stall; crash damage
- [x] **Gyrocopter** (autogyro: rotor spun by airspeed, pusher prop) and **ultralight** trike
- [x] **Flight HUD**: altitude, airspeed, compass; chase camera
- [x] **Airstrips & hangar**; take off from straight roads
- [x] **Aerial scouting**: sites and convoys show up from the air

*Audit (2026-10-13):* a hard landing no longer mends the engine (negative damage below 9 m/s).

*Ties:* vehicles, crafting, exploration, economy (fast deliveries).

*Done:* `FlightModel` on designs with `aircraft` (`VehicleDriver.aircraft`: free-rolling gear, no reverse): per-surface aerodynamics from each surface's point velocity (angle of attack, linear lift to the stall, flat-plate lift and drag beyond it, induced + profile drag), ailerons / elevator / rudder by deflecting the surfaces, pusher thrust from engine rpm fading with airspeed, airframe drag, wind (`Fx.Wind`). The gyrocopter's rotor autorotates (tip speed follows the airflow, a pre-rotator spins it on the ground with throttle), lifts along the disc axis and the stick tilts the disc (authority grows with rotor speed). Controls: W/S throttle lever, A/D bank + coordinated rudder (nose wheel on the ground), Space pull / Ctrl push, S at idle brakes (pad: triggers, left stick, right stick pitch). Hard contacts (impact across the surface ≥ 8 m/s) damage the engine by the impact and throw the pilot (ragdoll); > 22 m/s explode. Designs `Ultralight` (flex-wing trike, tandem seats, 10 m striped wing) and `Gyrocopter` (enclosed pod, 8 m rotor, twin fins), spinning `Prop` / `Rotor` meshes (`VehicleDesign.spinners`), explicit centre of mass ahead of the main gear (`VehicleDesign.com`), parts `engine_2stroke_aero`, `wheel_aero`. Flight HUD: altitude over ground, airspeed, climb, heading, throttle, rotor rpm, artificial horizon, blinking STALL. Chase camera = the existing vehicle cameras. Airfields: new `SiteKind.Airfield` (1–9 per world, mostly desert): a 190–260 m strip graded level with painted centreline, thresholds, edges and aiming blocks (terrain features 4/5), an apron and a derelict Quonset hangar complex (rusted-through shell, radio hut with a locker — loot table `airfield` with `bp_aviation`, windsock, fuel drums) with one flying machine to find (`WastelandGame.FoundAircraft`, saved). Buildable `hangar` piece (coarse 0.16 m voxels, station `hangar`); aircraft recipes need it and the blueprint; bicycles are a workbench recipe. Straight roads work as runways. Aerial scouting above 25 m: sites, towns, convoys (raiders in red) and herds are tagged out to 250 m + 6 × altitude (≤ 1.2 km); first sightings of sites are noted (`Scouted`, saved).

---

## Gaps & suggestions

*Written after the whole game was reviewed at the end of the roadmap work (2026-09-29). Every roadmap system was
play-tested in the editor, and performance was measured in a development player (road benchmark, seed 7). Fixed
during that pass: bicycle pedals dragging on the ground, bike drift and parked-bike balance, sidecar pull, flight
handling, the HUD overlap for aircraft, cabin climate on open vehicles, the stray roof prompt, an NPC death
exception, a bleed-out loop after respawn, aircraft among roadside wrecks, and the streaming hitches (from 24 frames
over 33 ms on a town approach to 4). The items below are what is left: suggestions, not bugs in progress.*

### Gameplay
- [x] **No world map or waypoints.** The minimap shows wrecks, trailers and fleet vehicles only. Hauls, escorts
      and town-boss couriers go to towns kilometres away, and bounty gangs, claims, caves, bunkers, airfields and
      aerially scouted sites (roadmap 25, only a toast) have no marker. Suggest: a full-screen map page (towns by
      name, roads, discovered sites, claims, faction territory), pins for active jobs, a route line along the roads
      on the minimap, and a compass strip in first person.
      *Done:* MAP page (M; pad Select tap): the land, roads, towns by name, found sites (bunker/tunnel/airfield letters), claims, fleet, job pins; drag/WASD pan, wheel zoom, click sets a waypoint (RMB/X clears); the route follows the roads (A* `World/RoadRoute`) on the minimap, with rim markers for waypoint and pins; compass strip in first/third person. Found places: `WastelandGame.Discovered` (towns entered, sites visited or scouted), saved.
- [x] **Death has little weight.** Respawn keeps the whole pack and only halves health. Open wounds are now
      bandaged on respawn, which fixed a loop where a bad crash bled the player out again after every respawn. A
      crash can still bleed you out in about a minute with no warning. Suggest: drop part of the pack as a
      searchable stash where you fell (scaled by difficulty), and a bleed-out warning with a bandage prompt.
      *Done:* a share of the pack stays in a searchable stash where you fell (NORMAL a third, HARD two thirds, BRUTAL all but what you wear; saved, waypoint set), and a flashing BLEEDING OUT countdown with a hint to bandage.
- [x] **Context menus, looting and where to wake up** (wave 3). Everything is reached by its own key; nothing can
      be right-clicked, storage is a one-column list, and death always respawns at the last spawn point.
      *Done:* `ContextActions` provider registry + `WastelandGame.Context`: an RMB tap (or the CONTEXT key, default `)
      on what the cursor / crosshair is on opens a pixel popup of the same calls the keys make ([E]/[T] of every
      interactable, drive/enter/get out, take off / pick up / mount parts, refuel, siphon, weld, repair / upgrade /
      dismantle own pieces, walk here, item use / equip / wear / drop / place, hotbar); out of reach the player walks
      up first. RMB keeps aiming with guns, the grapple and binoculars and cancelling in build mode / PLACE. The loot
      window (`MenuSystem.Loot`, page Container): pack and storage side by side, tabs for nearby storage, searchable
      spots (searched into the spot, leftovers saved in `blockContext`) and the FLOOR, drag and drop, row menus (split
      stacks), storage preview on hover. Respawn chooser: owned beds (the spawn bed first, by place name), spawn point,
      claims, the fleet, the start road (`LastRespawn` saved). Scenarios `ui.context_menu`, `ui.loot_window`,
      `ui.respawn_choice`. Open: a pad button to open the menu, item icons in the loot rows.
      *Wave 4:* floating loot panels (`Game/LootOverlay`, Project Zomboid style) over the running game: LOOT (floor,
      storage in reach by `Container.AccessAt` incl. vehicle compartments, searchable spots, bodies) and YOU (pack +
      `WornStorage` bags) as collapsible sections; auto-shown on hover (storage, spot, ground item), L pins (frees the
      cursor in first / third person); panels drag / collapse / close; drag and drop between sections or off the
      panels to the floor, double-click quick move, RMB row menus, tooltips. Scenario `ui.loot_overlay`.
- [x] **No guided first hour.** A new game drops you into a town with a fleet and an eight-line help strip.
      Suggest a short starter chain on the town board (patch the car, fuel it, first haul, first workbench and
      wall, first tame), each step unlocking the next and teaching one system.
      *Done:* FIRST STEPS (`WastelandGame.Starter`): drive 300 m, top up a tank, swap a part off a wreck (waypoint to the nearest), run a haul from a town board (waypoint), build a workbench and a wall, tame an animal; one objective line under the compass, journal entries, scrap per step; saved (`SaveData.starter`).
- [x] **No long-term goal or ending.** Skills, research, factions and bases grow, but nothing pulls the late game
      together. Suggest a myth to chase (the "Last Engine": a legendary V12 in parts across bunkers, airfields and
      faction vaults), faction endgames (the Guild's pipeline, the Church's pilgrimage), or a convoy run to the
      map edge.
      *Done:* THE LAST ENGINE (`Game/LastEngine`): four relics — the block in the second-nearest bunker, the heads in the nearest airfield hangar, the crank on the boss of the gang with the longest road, the twin blowers from the Church at FRIENDLY standing; rumours name and pin them; the garage assembles `engine_v12_last` (1050 Nm V12 with its own engine sound); a car running it that reaches the world's edge makes THE LAST RUN (journal, standing). Faction endgames are left for later.
- [x] **No racing or arena activity in a driving game.** Suggest: point-to-point races between towns against
      rival drivers (entry fee and purse, tuning matters), bike trials on the mesas, air races between airfields,
      a scrap-metal arena with bets, and jump ramps with a hang-time score.
      *Done:* `Game/Racing`: one event a day per town board ([T]): ROAD RACE to the nearest town against three AI rivals (fee, purse, places), BIKE TRIAL through off-road gates round the town, AIR RACE through gates in the sky round the nearest airfield, city ARENA (bet, outlast three armoured wreckers); countdown at the start, gate marker + waypoint, HUD line with gate/time/place, best times saved; JUMP RAMP build piece (sloped deck) and hang-time scores (best saved).
- [x] **Raids by territory** (from base building): the raiding gang should be the one whose road stretch is
      nearest; recruited or bribed gangs skip the base; wiping a raid party dents its next convoy generation.
      *Done:* `NpcDirector.RaidersByRoad` picks the gang whose road passes nearest (base raids and town raids); `Convoy.SparesBases` (recruited, allied, or a toll paid: `ConvoySave.spareUntil`, 3 days paid / 1 day scared or fooled); a party wiped out adds `ConvoySave.losses` (fewer cars in the gang's next generation, smaller raids; heals one per generation).
- [x] **Radio warns of raids**: WasteTalk FM names settlements (and your claim) on a gang's warpath an hour ahead.
      *Done:* `RadioNetwork.Flash`: an hour before a base raid and before a dusk town raid (the held town, or tonight's roll on the town you are in) the talk station breaks in with a NEWSFLASH line (journal entry when listening), other stations hint "NEWSFLASH ON WASTETALK".
- [x] **Base upkeep**: pieces weather slowly (rain on wood, sand on everything) unless roofed or repaired; a claim
      shows what needs the hammer.
      *Done:* `Building/BaseUpkeep`: every in-game hour, pieces in the open may lose a hit (never below one): wood and cloth rot in rain, metal rusts, stone barely ages, sand scours everything in the desert (×16 in a dust storm); a roof overhead protects. Build mode marks worn pieces (amber, red at one hit); the claim flag counts what needs the hammer.
- [x] **Garage as the fleet's home**: vehicles parked on a claim repair slowly with a garage piece, refuel from its
      tanks, and come first in the Tab cycle; raiders go for parked vehicles.
      *Done:* `WastelandGame.Garage`: fleet vehicles parked within 14 m of a GARAGE on a claim mend parts, dents and frame hourly and fill from the claim's built fuel pumps; Tab visits them first. Raiders: a third of a live raid party are car breakers (siphon, smash), and off-screen raids siphon and strip parked vehicles.
- [x] **Companions man the base**: companions posted at a claim use turrets and ring the bell; motion-sensor
      floodlights ([T] SENSOR) that raiders avoid.
      *Done:* a guarding companion (`Npc.ManPost`) runs to ring the claim's bell when hostiles come within 70 m, then takes the nearest free turret (`AutoTurret.gunner`: a manned gun fires without power, faster and truer). Floodlights have a [T] SENSOR mode (dark until something moves within 18 m, then 30 s on); raiders pick siege targets outside lit floodlights.
- [x] **Animals × gardens**: livestock gives milk, eggs and hides, but no manure. Pens could yield dung for the
      composter, and grazing animals could trample or eat crops without a fence.
      *Done:* fed cows, horses, goats and pigs leave manure at their trough ([T] SHOVEL MANURE, saved), the composter makes fertilizer from it (`fertilizer_m`); hoofed plant-eaters wander onto unfenced beds and eat them (`GardenPlot.Grazers`, `Animal.Grazer`); any fence within 4 m protects a bed.

### Driving, riding, flying
- [x] **Hill starts**: heavy cars (Scavenger, 2.7 t) cannot pull away on a 10° slope at part throttle; add launch
      torque so ramps and hills don't need a run-up.
      *Done:* uphill the clutch is slipped nearer the torque peak, and while it slips the drive gets a launch multiplier (automatic 1.9x like a torque converter, manual 1.3x).
- [x] **Stall overheating**: a car pinned against an obstacle at full throttle cooks its engine and catches fire
      in well under a minute; slow the heat build-up, or add a rev-limiter warning.
      *Done:* thermal mass ~30 s, radiator fan at a standstill, a pinned engine on the limiter makes less heat, and ENGINE LABOURING: EASE OFF warns first.
- [x] **Bikes and small vegetation**: a biker is thrown off by a bush at 30 km/h (bushes are solid destructible
      props). Let small shrubs and fences break under a bike, with a wobble instead of a crash.
      *Done:* bushes, fences, cacti and small props no longer throw the rider: the bike ploughs through (the prop carves) with a wobble.
- [x] **Sidecar outfits are assisted**: steering is grip-limited, holds the heading when the bars are released,
      and most of the pull towards the chair is trimmed out. A "vintage" setting could bring the pull and the
      chair-lift back for players who want the real thing.
      *Done:* setting SIDECAR HANDLING: ASSISTED / VINTAGE (straight bars, most of the pull left in).
- [x] **Flight is arcade-assisted**: the trike flies on rate commands with auto-level, a bank limit and an AoA
      limiter; the gyro has rate-commanded disc tilt. Suggest a SIM FLIGHT setting (raw weight shift, stalls and
      spins, crosswind landings), plus instruments for it (slip ball, stall horn).
      *Done:* setting FLIGHT MODEL: SIMULATION (raw weight shift / disc tilt, no auto-level, bank or AoA limits, no turn coordinator); slip ball and STALL WARN on the flight panel, a stall horn.
- [x] **No runway lights or night flying aids**: airfields are unlit, and a night landing means aiming at the dark.
      *Done:* `World/RunwayLights` (with the hangar piece): amber edge lamps every 30 m that glow after dusk, green threshold lights; the flight HUD adds an approach aid near airfields (threshold marker, runway heading, distance, left/right of the centreline, 4° glide slope HIGH / LOW / ON GLIDE).
- [x] **Start line-up**: the starting fleet parks on the road through the start town, and wrecks can spawn on road
      surfaces (a parked ambulance across the lane). Park the fleet on a yard beside the road; keep wrecks on the
      verge.
      *Done:* a graded gravel start yard beside the highway (`WorldGen.YardWeight`, terrain feature 6, no props or ground cover): the fleet parks in rows facing the road, machines across it, trailers at the back; wrecks never spawn on a lane (town wrecks off the streets, roadside ones on the verge) or in the yard.
- [x] **Burning wrecks near the start**: a wreck spawned on fire about 17 m from the starting fleet set the
      Interceptor and Excavator alight. Keep ignition-prone wrecks away from the spawn, or spawn them cold.
      *Done:* the three scavenge wrecks sit at the back of the yard, cold (no fuel), and every spawned or woken wreck gets a settling grace (`VehicleDamage.graceUntil`) so drops and depenetration never damage them.

### World
- [x] **Thin variety for driving.** There are no rivers, fords or bridges, scrapyards, military checkpoints,
      refineries or radio masts (the radio has stations but no towers). Scrapyards would give parts hunting, fords
      would make snorkels matter, and checkpoints would carry faction tolls; each feeds an existing system.
      *Done:* `WorldGen.Rivers`: three rivers per world walk downhill from high ground into lakes or basins (carved beds and banks, water stepping down the course, dry wadis in the desert); dirt tracks cross at fords (the road dips 0.35 m under the water), highways on graded causeways. `BiomeProps.Landmarks`: radio masts outside towns and cities (blinking lamp; `RadioReceiver.Signal` fades stations into hiss away from masts, WEAK SIGNAL), Remnant checkpoints on long highways (`World/Checkpoint`: boom across the road, honk or [E] to pay 10 scrap, free for friends, ram it and lose standing), scrapyards beside every third town (fenced lot, crushed-car stacks, parts locker, four wrecks), a refinery on the richest oil near a road (column, tank farm, 900 L diesel pump, fuel store). Landmarks enter the journal when found.
- [x] **The start town is cramped**: sheds, the fleet and props leave little room to turn a truck. The autotest
      pilot got stuck there repeatedly and now starts outside town.
      *Done:* the start yard keeps a 90 x 76 m lot clear of buildings and props.
- [x] **Caves and bunkers read as black voids**: under the underground cutaway everything outside lamp radii is
      pure black. Suggest a faint ambient fill under `Subterranean` roofs, glowing fungus, or light spilling in at
      the portals.
      *Done:* under a `Subterranean` roof a faint cool fill light (`_MadMaxUnderFill`) keeps the dark between lamps readable, in every view.
- [ ] **Regional weather** (deferred on purpose): tropical regions get no extra rain, and weather is still
      global.

### NPCs & animals
- [x] **NPCs cannot path.** They walk straight at a goal and sidestep when stuck (`Npc.Blocked` checks only water
      and cliffs), so fences, walls and buildings trap them, including companions and raid parties. They need a
      path grid (terrain plus structure occupancy, doors as portals) or a NavMesh.
      *Done:* `Npc/NpcPath`: when the straight line is blocked, A* on a 0.5 m grid (≤ 48 m window, knee-to-head overlap tests against buildings, fences, pieces, vehicles; shallow water only; steps under 0.6 m; no corner cutting), string-pulled; `Npc.Toward` follows it (all on-foot modes), unlocked doors open on the way, locked doors are walls; 3 searches per frame, re-planned when stuck.
- [x] **Road life is one-sided**: raider convoys hunt the player and town raids happen, but raiders never
      ambush trader convoys or pack traders on the road unless you are there. Off-screen skirmishes, and the wrecks
      they leave, would make the roads feel alive.
      *Done:* `NpcDirector.Skirmishes`: a raider gang meeting a trader convoy far from the player (both > 350 m away) rolls a fight (gang size vs escort); the loser is gone until its next generation (beaten raiders also thin out), a wreck and spilled cargo stay on the verge (`SpawnRoadWreck`, searchable crate), and WasteTalk / the journal carry the news.
- [x] **Vulture flocks outlive the scene**: a flock keeps circling a far carcass after the player leaves (flocks
      are only culled by count). Fold them away with their carcass.
      *Done:* a flock leaves with its meal: carcass gone, picked clean, or the scene left beyond fold range.

### Saves
- [x] **No autosave, one slot.** The game only saves from the pause menu, not on quit or sleep, and permadeath
      deletes the only file. Suggest: autosave on sleep and every N minutes (a setting), save on quit, three slots
      plus a rolling backup.
      *Done:* three save slots plus an autosave (`SaveSystem.Slots`), the previous file kept as .bak (written via a temp file; a damaged save falls back to it), a slot list with day/place, autosave every N minutes (setting), after sleeping and on quit; permadeath deletes the run's slot and the autosave.
      *Audit (2026-10-13):* permadeath deleted slot 1 (or the last slot used this session) even when the run had never saved there: `SaveSystem.SlotOwned` (set by saving to / loading from a slot, cleared on a new game) now gates it, and `Delete` removes the `.bak` too.
- [x] Not saved: ruts, dents, crate positions (known), storm state and lightning fires in flight.
      *Done:* ruts still healing (sparse cells, the 300 rutted chunks nearest the player) ride in `ChunkEdit.rut`; dents per mesh (`DeformableMesh.SaveState`, `VehicleSave.dents` by child path); crates pushed off their spot (`DestructibleVoxels.Moved`, applied when they stream back in); the storm in progress (`Storms.SaveState/Restore`); fires not on vehicles (`SaveData.fires`).
      *Audit (2026-10-13):* moved crates of the previous run leaked into a new game (the unloading scene re-noted them): `DestructibleVoxels.Moved` is cleared at `Start` when no save is pending.

### Multiplayer
- [x] **NPCs and animals are host-only.** `NpcDirector` and `AnimalDirector` run on the authority, and `Net/` has
      no NPC or animal messages. Clients see no shopkeepers, traders, convoys, raiders, herds or livestock, and
      cannot trade, tame, ride or hunt.
      *Done:* `NetSession.Actors`: people and animals within 160 m reach clients as proxies (`ActorSpawn` with the deterministic profile / species, then snapshot entries: position, yaw, speed, flags — dead, sitting, surrender, swing — or the animal state; `ActorGone` when out of range); proxies animate, ragdoll / fall on death with the same searchable loot, and hits on them go to the host (`ActorHit`). Talking and trading on a client act on the local copy; taming and riding still need the host.
- [x] **Storms and lightning are per machine.** Clients never roll storms, so `Storms.Dust/Rad` stay 0 there;
      every peer rolls its own lightning, and `Fire.Ignite` broadcasts fires from any peer. Fix: host-only strikes
      replicated by position, and Dust/Rad added to `SendWeather`.
      *Done:* storms ride in the weather message (sent when one begins), clients never roll thunder: the host rolls a strike near each player (`SendStrikes`: effects run on the host, `Strike` shows the flash and bolt there).
      *Audit (2026-10-13):* the weather sent to a joining client (`Msg.Ready`) lacked day + storm (the reader over-read into the next message): it now carries the same fields as `SendWeather`, so a mid-storm joiner gets the storm; the packet loop seeks to every message boundary (`NetReader.Seek`), so one malformed message can't shift the rest.
- [x] Armour plates, tuning, grime, gun rounds, fish records, market state, faction standing and aircraft found at
      airfields reach other players only through the join snapshot.
- [x] **Moving bases, lifters, machine loads and TAKE APART were host-local** (2026-10-13 systems).
      *Done (protocol 6, `NetSession.Tools`):* `RigSpawn` starts the same base everywhere (rig pieces keep their ids in `VehicleSave.rig`, Placed is idempotent); `ToolPose` (≤ 5 Hz, on change, resent to joiners) carries machine hinges + bucket / blade / bed loads, magnet crane and grapple poses from the simulating peer; lifters only take bodies simulated here (a client asks the host for the wreck: `Carry`, returned when let go); `Crush` runs the press as a show elsewhere and removes the car, the bale is a world item; `TakenApart` removes props / spots / lamps / city fixtures everywhere (kit and learned design stay with the doer; joiners keep their own designs and waypoint).
      Open: city timetable clocks drift between peers after local pauses (only the join snapshot sets them); loose parts the host simulates can't be lifted by a client's magnet; client-made vehicles can still clash with host vehicle ids.

### Controls
      *Done:* `WorldState` every 10 s (faction standing, market prices, fish records, aircraft found) and `VehicleLooks` (armour, tuning, paint, grime) sent by whoever simulates the vehicle when they change, relayed by the host. Gun rounds stay per player (each keeps their own).
      *Audit (2026-10-13):* a joining client now applies the join snapshot's terrain edits, factions, market, fish records and found aircraft at once (`JoinAsClient`) instead of waiting for the first WorldState, and only the host spawns a hangar's found aircraft (clients had a duplicate).
- [x] **Keys are overloaded by context and cannot be rebound**: G service/repair, X 4WD/dismantle, E/Q gear
      shift/take/drop part, R reload/research/weather debug, T recover/second action/parley, Shift run/wheelie/
      sprint/gallop, Space handbrake/jump/pull up. Suggest a controls page with rebinding, and moving the debug keys
      (R rain cycle, Backspace drop part, G instant repair, T recover) behind a dev flag.
      *Done:* `Game/Controls`: every gameplay key is a rebindable action (CONTROLS page, clash warnings, reset), prompts and the help sheet show the bound keys; debug keys (weather F9, drop part Backspace, instant repair F10) only with --dev (MadMax > Dev > Debug Keys); radio power moved to / (M opens the map); recover only when slow or on its side.
      *Audit (2026-10-13):* the CONTROLS page hid every action listed after the dev keys (tool keys, CONTEXT, LOOT, HANDCRAFT) without `--dev`; it now hides only the three dev keys (`Controls.IsDev`).
- [x] **Gamepad is partial**: driving and some menus are mapped; building, fishing, aiming, radial menus, flying
      trim and the (future) map are not.
      *Done:* hold Select for the action wheel (right stick picks; hotbar tools are on it), tap Select = map / recover; build mode on the pad (d-pad pieces, shoulders categories, Y rotate, X dismantle, RT place); top-down aiming with LT + right stick; the right stick flies the aircraft instead of turning the camera.
- [x] **The help strip is a wall of text**: eight dense lines cover the top third of the screen for 12 s after
      every load. Replace it with context hints ("F — ride the horse", "Space — vault") and keep H for the full
      sheet.
      *Done:* context hints (`Game/Hints`): one line the first couple of times you drive, ride, fly, aim, fish, build, bleed or run low on fuel; setting HINTS, SHOW ALL HINTS AGAIN; H still shows the full sheet (with the bound keys).

### Settings
- [x] Missing:
  - mouse sensitivity and invert-Y
  - FOV for first and third person
  - camera shake toggle
  - HUD scale independent of the pixel height
  - colour-blind-safe HUD colours (the red/green bars)
  - separate ambient, UI and radio volumes
  - autosave interval
  - units (km/h / mph, °C / °F)
  - radio captions (DJ talk and weather reports are voice only)
  - SIM FLIGHT and vintage sidecar assists (see above)
      *Done:* settings are grouped (GAMEPLAY, MOUSE & CAMERA, GRAPHICS, AUDIO, INTERFACE, CONTROLS): mouse sensitivity, invert Y, first/third person FOV, camera shake, HUD size, colour-blind HUD (blue/orange), effects / ambient / interface / radio volumes, autosave interval, units (km/h or mph, C or F), radio captions (from `audio/export_captions.py` → `Radio/captions.json`), flight model, sidecar handling.

### Audio
- [x] **No footsteps**: the `footsteps` clip is never played, so the player, NPCs and animals are silent on foot.
      Suggest per-surface steps (sand, mud, snow, wood and metal floors, water) from `HumanAnimator.Footstep`, and
      hooves for the horse.
      *Done:* per-surface steps for the player and nearby NPCs (road, gravel, sand, mud, snow, water, wood decks, vehicle floors) and hooves for big animals, synthesised by `Audio/ProceduralSfx` (the fallback for keys without a recorded clip).
      *Audit (2026-10-13):* steps inside walk-in interiors (bus, houseboat, sea base) were silent; they now play metal / wood floor steps.
- [x] **Weapons share sounds**: the pistol, rifle and roof MG reuse `shotgun` at other pitches, and the bow reuses
      `pop`/`scratch`. There are no night insects, no creak or collapse sounds for structures, and no rotor chop
      for the gyro beyond the engine synth.
      *Done:* pistols, the rifle and machine guns have their own synthesised reports, the bow twangs; crickets on warm dry nights; structures creak before they give and rumble when they collapse; the gyro's rotor slaps.
      *Audit (2026-10-13):* the rotor and stall horn were requested from `FixedUpdate` and stuttered above 100 fps; they are requested from `Update`, and `Sfx` keeps a loop 50 ms past its last request (city lifts, magnet crane, crusher, scraping bodies request from physics too). Carcass flies follow the AMBIENT volume.

### Visuals
- [x] **Flying sees a small world**: terrain and props only stream within the view radius (72 m by default)
      around the aircraft, so at 60 m+ the ground ends in fog close by. A far-terrain impostor ring (heights and
      biome colours only, no props) would sell altitude and help navigation.
      *Done:* `World/FarTerrain`: while an aircraft is more than 20 m up, a 1.1 km sheet of coarse terrain (12 m steps, heights, water, roads and biome colours from `DeformableTerrain.FarColor`, no props or colliders) is sampled on a worker around it and rebuilt as it travels, sunk 1.5 m under the streamed chunks; `FarTerrain.Aerial` pushes the perspective fog and far clip out to ~620 m.
- [x] The TowTruck showed its lights on while parked (seen during roadmap 14).

### UI / HUD
      *Done:* likely a stale `Occupied` flag: an NPC leaving the wheel skipped `AiDriver.Release` when the AI was already switched off. `Npc.LeaveWheel` now always releases (unless the player took over), and auto headlights need a real driver (AI or the player); to confirm in the test pass.
- [x] **The HUD and font scale with the pixel height**: at 480–540 px the 3×5 font becomes tiny on big screens.
      Draw the HUD canvas at its own (settable) resolution.
      *Done:* setting HUD SIZE draws the HUD canvas at its own resolution.
- [x] **No journal**: contracts, errands, town-boss chains, research and known recipes each live on their own
      page. A journal (jobs with destinations and deadlines, rumours heard, sites found or scouted) would tie them
      together.
      *Done:* JOURNAL page (Tab from the map): the jobs in hand with distances (ENTER sets a waypoint), then a notebook of rumours heard, errands and jobs taken, places found, by day (`Game/Journal`, saved).
- [x] **HD HUD in every mode + 3D damage view** (2026-10-13): with HD HUD on (now the default for new settings) the
      whole HUD is crisp at screen resolution in its pixel layout (HUD SIZE still scales it): rounded vector panels with
      soft shadows, edges and highlights (`Rendering/HudVector` + two `HudVectorGraphic` layers around the pixel image;
      `PixelCanvas.Panel` becomes a vector panel), vector glyphs (`HudGlyphs`), bar / ring / arc gauges, the HD font at
      fractional sizes (`PixelCanvas.TextF`). Covered: header + resources, vitals (bars + ring gauges + tag chips),
      hotbar, crosshairs, prompt list (key caps), toasts, hints, item feed, work bar, LOOT / build / rail / status pills
      (via `Panel`), the driving cluster (rev dial with the speed inside, gear, fuel / temperature arcs, oil / coolant /
      mud / slip bars, 4WD / LOCK / NOS chips, warning lamps), boats (throttle, rudder) and submarines (depth, ballast,
      battery, air), the flight panel (attitude ball, airspeed, altitude, climb, heading, throttle, rotor, stall), the
      compass strip, weather, temperature vignette and the minimap rim. Damage view (`Game/HudVehicleView`): the current
      vehicle (or the one you could drive, on foot) rendered 3/4 into a RenderTexture by its own camera on a hidden
      stage (proxies share the vehicle's meshes; ≤ 10 Hz while moving or changing, 1 Hz at rest), parts tinted green →
      amber → red by damage / tyre wear / frame damage, parts lost while watching as red ghosts, empty wheel / engine
      sockets ringed, the hidden engine marked, armour zones, a status glyph row; replaces the PARTS map. Perf: minimap
      composed ≤ 15 Hz (cached block), `MadMax.Hud.*` profiler markers, no-garbage text caches, memcpy upload.
      Scenario `ui.hd_hud`.

### Performance
- [x] **New-game load is a ~4 s single frame** behind the fader: terrain around the spawn (0.7 s after
      parallelising), 36 wrecks (0.9 s), template baking and the scene switch. The spinner freezes during
      it. Suggest a loading coroutine with a progress bar, and wrecks spawned lazily as their chunks stream in.
      *Done:* `WastelandGame.Start` is a coroutine: world/terrain, vehicles and HUD are built over separate frames behind the fader, the wheel keeps turning and a progress bar fills (`ScreenFader.Progress`); Update and the directors wait for `Ready`. Wrecks are planned up front and spawned as a player comes within 220 m (`wrecksPending`, saved; scrapyard wrecks too).
- [x] **No multi-second frames while loading** (2026-10-13, from a dev-build boot profile: 6.4 s / 3.2 s / 2.4 s / 1.2 s
      frames): `WorldGen` is built on its own thread (16 MB stack) while the prop templates (`BiomeProps`, `PropLibrary`)
      are made on workers; chunk data is prefetched on workers as soon as the focus is known (`DeformableTerrain.PrefetchArea`)
      and the chunk objects are built nearest first within `WastelandGame.LoadBudgetMs` a frame (`BuildAround`), their
      mesh colliders cooked on workers (`BakeLater`/`PollBakes`; loose props held kinematic until the collider is in; in
      game too for chunks beyond 2 rings); fleet vehicles and yard wrecks spawn within the budget; the tracked city and the
      megacity merge and pack their meshes on the worker and switch colliders on a budget at a time; `SettleWorld` caps
      physics at 6 steps a frame. Every load step has a `MadMax.Start.*` marker (terrain: `MadMax.Terrain.*`, city:
      `MadMax.City.*`). `Pal.Hex` parses hex in C# (palettes are built on workers).
- [x] **GC near towns**: the allocation rate triples entering a settlement (chunk arrays, prop spawns, carve
      snapshots), and an incremental GC slice sometimes takes 30+ ms. Pool chunk arrays and debris lists, and try
      IL2CPP for release builds.
      *Done:* dropped chunks hand their arrays back to a pool (`DeformableTerrain.Recycle`, up to 96 sets) that `Generate` reuses on any thread (≈50 KB per chunk no longer garbage). IL2CPP is not switched on: the Linux IL2CPP module is not installed in this editor (add it in Unity Hub, then set the backend in `CiBuild` for release builds).
- [x] **First chunk with a big site** (bunker, airfield hangar) still builds its objects in one 25–35 ms frame;
      spread `SiteBuilder.Populate` over frames.
      *Done:* `SiteBuilder`: the piece's collider is cooked on a worker (`Physics.BakeMesh`) once its mesh is uploaded, and pieces spawn through `SitePending`, one per frame across all sites.
      *Audit (2026-10-13):* a far piece could be dropped (its mesh destroyed) while PhysX was still cooking it on a worker; `Prewarm` now waits for the bake.
- [x] **Crash frames**: a car ploughing through a building spends 15–25 ms in `VehicleDamage` dents and the carve
      before the async remesh. Dent mesh updates could batch per frame.
      *Done:* `DeformableMesh.Dent` only moves the vertex targets; one snap + upload + normals per mesh in `LateUpdate`, however many contacts and hits landed that frame.
- [x] **Big bases**: every built piece is its own GameObject and collider (mesh colliders for walls and floors).
      Merge static pieces per structure cell into combined meshes, rebuilt when a piece changes.

### Balance / tech notes
      *Done:* `Building/StructureBatcher`: Structure pieces (walls, floors, roofs, foundations) in every 16 m cell beyond ~28 m from the player are merged into one mesh per cell (their renderers off, colliders and logic untouched); cells near the player draw piece by piece for cutaways and build highlights; a cell re-merges when its pieces change, one merge per frame.
- [x] `Fire.Burn` heats vehicles once per overlapping collider (`VehicleSystems.Heat`), so vehicles with many part
      colliders cook faster than simple ones. The player-damage half of this bug was fixed in roadmap 17.
      *Done:* fires heat each vehicle (and try to ignite each piece) once per tick.
- [x] The tuning card's top speed is an estimate (drag and gearing only).
      *Done:* the card solves the real force balance per gear (torque curve and tuning, rev limiter, drag and tyre rolling resistance).

## User additions (2026-09-29)

### Environment
- [x] Solar panels, wind turbines (large, medium; small one mountable on a lorry) and water turbines generate electricity, with their crafting and resource chain. *Done:* `SolarPanel` (400 W, sun × cloud/rain/dust/snow), `Windmill` (wind at hub height + vehicle speed; large 3 kW on a 15 m tower, medium 900 W, lorry part `cargo_wind_turbine` 500 W), `WaterTurbine` (1.2 kW × river current); kits from coils, blades and arc-furnace solar cells (`FurnitureLibrary.Power`).
- [x] River flow: the water visibly runs downstream and carries floating things and swimmers. *Done:* `WorldGen.Rivers` (`RiverFlow`), `RiverFoam` flecks, buoyancy drag and swim drift follow the current.
- [x] Wet and dry roads, puddles that clear when it is sunny. *Done:* wet-asphalt sheen and road/paved puddles in `DeformableTerrain`; drying × sun and temperature in `Weather`.

### Audio
- [x] New hillbilly / Southern US voice sources; an armada of NPC lines (greetings, reactions in conversation, bumped into, nearby chatter, NPC-to-NPC exchanges) with some rough language, sarcasm and wasteland puns.
      *Done:* `Npc/NpcVoice` — 10 Southern voices by temper and gender, 1265 unique lines, 110 two-voice exchanges, speech bubbles (see CLAUDE.md, NPC voices).

### Game / HUD
- [x] Separate audio channel volumes in the settings. *Done:* MASTER / EFFECTS / VEHICLE / WEAPON / VOICE / AMBIENT / INTERFACE / RADIO (`Sfx.Channel` by key).
- [x] Player stat icons in the car too. *Done:* `PixelHud.DrawVitalsCompact` (icon + upright gauge row above the vehicle panel; bottom-left in first person).
- [x] Camera rotation and tilt in the 2.5D views; a top-down camera, a hood camera and a car FPV view. *Done:* RMB drag / pad stick / Z C / PgUp PgDn in iso, tilt-shift and TOP; `ViewMode.TopDown`, `Hood`, `Bumper` (spots from the body mesh); `CameraRig.TopDownView` / `CrosshairView`.
- [x] Loot and wreck density settings for a new game. *Done:* WRECKAGE (NONE..SCRAPYARD) and LOOT (SCARCE..HOARDER: loot rolls, crate rolls, how stripped wrecks are — `GameRules.StripChance`).

### Weather effects
- [x] Rain and snow fill the whole view in every camera; in 2.5D the rain falls as diagonal lines. *Done:* `Weather.CoverView` fits the emitting sheet to the view frustum between the ground and the layer top; wind + a screen-right slant in top-down views.

### Vehicles
- [x] Engine start: cranking with sound, the chance to start falls with engine condition. *Done:* `VehicleSystems.Crank/StartChance` (condition², quality, cold, plugs, filter, oil), starter / catch / sputter sounds, stalls on no fuel, seizure, flooding or wrong fuel.

## User additions (2026-09-29, second list)

### Boats and submarines
- [x] Boats of various sizes and jobs to get around, fish from and live on. *Done:* `Designs/BoatDesigns` Raft, Skiff, Trawler (fish hold + `TrawlNet`, [1] at the helm), Houseboat (walk-in cabin, bunk, stove); `BoatModel` (float points, drag, prop, rudder, wake) on a wheel-less `VehicleDriver`; built at the `slipway` piece; found boats moored off coastal towns and a skiff near the start (`WastelandGame.Boats`, saved `boatsFound`).
- [x] Submarines, usable as bases too: oxygen and electricity from onboard resources. *Done:* Iron Eel (`Vehicle/Submarine`: ballast dive/surface, 30 kWh battery charged by the diesel on the surface or snorkel depth and at a docking collar, cabin air from the snorkel and `O2Rack` bottles, sealed walk-in interior as a base); `bp_submarine` from loot and traders.
- [x] Diving suits for working underwater. *Done:* `dive_helmet`, `dive_suit`, `air_tank` (clothing, crafted), tank air (saved), `air_compressor` refills, O2 bottles; `PlayerCharacter.Diving` (swim at depth, head under), air bar on the HUD, murk + water window (`CameraRig.Underwater`).
- [x] Underwater base building with a ground-level entrance and submarine docking. *Done:* `FurnitureLibrary.Sea`: `sea_dome`, `sea_tunnel` (airtight `AirPocket`s: dry inside), `shore_entrance` (walk down from the beach), `docking_collar` (`DockingCollar`: the sub docks, charges and the crew walks through).

### Wildlife
- [x] Fish and other underwater fauna and flora. *Done:* `World/SeaLife` (schools of sardines, mackerel, perch, reef fish that scatter from divers and hulls, jumping fish, stinging jellyfish, beach crabs), sea species in `FishLibrary` (rod, trawl), seabed kelp / coral / sea grass in the flora.
- [x] Birds, mammals, lizards, snakes, scorpions, spiders and bugs. *Done:* `AnimalLibrary.Wild` (jackrabbit, coyote, deer, bear, armadillo, raccoon, lizard, gila, rad lizard, cottonmouth, python, crow, turkey, scorpion, rad scorpion, tarantula, cave spider, rad roach, crab, beetle; `BodyPlan.Arthropod` + sprawling gaits, venom → antivenom), flying insects (`World/Insects`: fireflies, butterflies, dragonflies, gnats, carcass flies); chitin vest, venom and hide recipes.

### Vehicles
- [x] Aircraft easy to turn on the ground, to brake and to reverse. *Done:* `FlightModel` taxi yaw control, pull brake on the ground, reverse thrust (reversed prop pitch, REV on the flight panel).

### World
- [x] The world is a rotating planet (illusion, chosen by the user): continents and oceans with biomes spread to fit, east–west wrap, a slight horizon curvature, and sun, moon and seasons driven by spin, tilt and orbit. Big enough to feel large, light enough to simulate. *Done:* `WorldGen.Planet` (continent noise, 9.6 km circumference with an open-ocean date line → `CrossDateLine`, latitude biomes incl. `Tundra`, ice walls at the poles, climate offset by latitude), 40 towns on land, `_MadMaxCurve` horizon (setting HORIZON CURVE), `DayNight.SunDirection/MoonDirection` from hour, declination and latitude, moon phases, polar snow line.
- [x] Intro lands on the Moon with the neon sign, Earth visible in space. *Done:* the title climbs into a darkening, starry sky and cuts to the Moon (`Designs/MoonArt`: cratered regolith, Mad Mike's crash-landed scrap rocket and its tyre tracks, an old-world lander with a bleached flag, the planet sampled from this world's generator under a cloud shell); `TitleSequence.OnMoon` stands weather, clouds and ambience down; boot film re-recorded.
- [x] Everything new ties into crafting and resources and fits the post-apocalyptic look and lore. *Done:* boats and the sub from `slipway` recipes (scrap, wood, iron, aluminium, rubber, cloth, copper; the sub needs `bp_submarine`), dive gear at the workbench, sea base pieces paid in iron, glass, concrete and scrap, wildlife drops (hides, meat, chitin, venom) into food and clothing recipes, blueprints from loot; rusted, patched voxel looks from `Pal` ramps.

## Scheduled update (2026-09-30)

### Done
- [x] **Homestead rest stop** at the start yard for a new game with a starting kit: patchwork awning, porch lanterns, workbench, chest, rug, chair, table with a radio and lamp, two tin herb planters. Ordinary placed pieces (editable, salvageable, saved). *Done:* `WastelandGame.SpawnHomestead`, pieces in `FurnitureLibrary.Homestead` (`porch_awning`, `porch_lights`, `herb_planter`); the workbench model gained a pegboard, drawers and a task lamp.
- [x] **Crafting page readability**: wider two-column layout, HAVE / NEED material table (PgUp/PgDn scrolls long lists), one reason why a recipe is blocked (`WastelandGame.CraftBlockReason`: knowledge, power, queue, the first missing input or fuel). Menu colours come from `Pal` (`Ink`, `MutedInk`, `Accent`, `Selection`, `PanelEdge`).
- [x] **Fuel bookkeeping**: the fuel a job actually burned (wood, charcoal or coal stand-in) is stored on the job (`CraftingStation.Job.paidFuel`, saved) and refunded on cancel; fuel that is also a recipe input is no longer double-counted (`PickFuel`). Stations near the player puff steam / heat haze, workbenches spark and ratchet.
- [x] **Seasonal markets**: `Market.SeasonFactor` — food is cheap at the autumn harvest (cheapest in farm villages) and dear in winter and the hungry spring; winter raises fuel, cloth and medicine, summer water, spring timber and brick. The season change toasts and breaks into WasteTalk with the news (`Market.SeasonNews`).
- [x] **Feel pass**: keyboard driving eases the steering and pedal (short ramps, a 0.35 s hold before switching between forward and automatic reverse; AI, bikes and aircraft unchanged), no park-sleep while reversing on the brake pedal; the iso camera leads along the road and widens with speed instead of bouncing with the suspension; lower wheel dust; softer grade contrast, sky bounce in `PixelVoxel`, dusk/night haze and sun colours from `Pal`; rain and wind duck smoothly under a roof or in a closed cabin; 168 m shadow distance.
- [x] `VehicleAudio` survives a play-mode script reload (the tyre synth is rebuilt instead of throwing every frame).

### Suggestions (tie the experience together)
- [x] **Store the harvest, sell the winter**: the seasonal price swing only pays if food keeps. Give preserved foods (smoked, dried, canned, pickled) near-zero spoilage and a recipe chain from the garden and the smokehouse, and let a root cellar / cold store piece slow spoilage in containers. Turns farming + crafting + trading into one loop across the year.
      *Done (2026-10-01):* preserves already keep (smoked, salted, pickled, jerky, tins: no spoilage) and the cold store chain exists; added the unpowered ROOT CELLAR piece (`root_cellar`, 160 kg, `Container.keep` 0.33, halved again in winter). Scenario `food.root_cellar`.
- [x] **Seasonal stock** (chores split out below): vendors carry seeds and saplings in spring, preserves and firewood in winter, fishing gear in summer; residents work the village fields at harvest (`NpcLore` errands: bring in the crop, cut firewood before the first snow).
      *Done (2026-10-01):* `Trade.SeasonalStock(kind, season)` adds lines per vendor kind: spring seeds and saplings, summer rods/bait/water, autumn the harvest, salt and timber, winter preserves, firewood and charcoal (more of a stocked line, or a new one). Scenario `economy.seasonal_stock`. Resident harvest chores remain open (see below).
- [ ] **Hemisphere seasons** (goes with the deferred regional weather): south of `ZEquator` the seasons should run half a year out of phase, and markets with them, so a long haul across the equator is a trade run.
- [x] **The homestead as the first home**: it should count as the player's home for the garage mend/refuel (`WastelandGame.Garage`), the bed-respawn and the raid target, and FIRST STEPS could end by sleeping there. *Done:* the homestead comes with a bed (the respawn point) and a claim flag (raids know it); its workbench looks after fleet cars parked within 14 m like a garage; FIRST STEPS gains "sleep the night in a bed"; homestead pieces are remembered (`SaveData.homestead`) so the prebuilt workbench no longer half-completes "build a workbench and a wall".
- [x] **Station sounds**: only workbenches have a working sound; stoves (sizzle), furnaces (roar), mixers (churn), stills (bubble) and sewing (clack) would let a base be heard working, through `ProceduralSfx`.
      *Done (2026-10-01):* `CraftingStation.WorkLoop(type)` → seamless 2 s `station_*` loops (sizzle, roar, churn, bubble, clack, grind, saw, hum) via `Sfx.Loop` while a job runs (18 m); the forge rings an `anvil`. Every fired/powered station type covered. Scenario `audio.station_sounds`.
- [x] **Town notice boards as a news digest**: the bounty board could also post the last few journal/radio headlines about that town (raids beaten, skirmish wrecks, season prices), so what happens off-screen is visible where the player trades.
      *Done (2026-10-01):* `Npc/TownNews` (day + place, saved `SaveData.townNews`): every `RadioNetwork.Flash` posts (skirmishes, base and town raid warnings carry their place), beaten town raids post too; the board page lists ROAD NEWS within 1.8 km plus region-wide news. Scenario `towns.road_news`. Also: the journal and news ledger now clear on a new game in the same session (statics outlived the scene reload).

## User fixes (2026-09-30)
- [x] **Bulldozer, excavator, paver and roller could not move.** Their tracks / drums are body voxels, so the body box rested on the ground and no wheel ever touched down. *Done:* `VehicleDriver.FitUndercarriage`: wheels get extra ray reach down to the body's lowest voxel, the body box starts at the axles.
- [x] **Stalled cars pressed together stick.** *Done:* vehicle colliders use `VehicleDriver.Skin` (friction 0.1, Minimum combine) so they slide off each other; terrain `DeformableTerrain.GroundMaterial` (0.6, Maximum) keeps full grip against bodies.
- [x] **Cars could not pull away on a slope** (wheels broke loose, spun, the box upshifted and the car slid back). *Done:* launch traction assist below 4 m/s (drive held just under grip), hill hold (brakes catch a roll-back against the throttle), automatic upshift only on road speed. A front-drive Trabant on skinny tyres still cannot climb a wet 15° sand slope (grip-limited), it now holds instead of sliding.
- [x] **Terraforming machines show their work.** *Done:* soil clods (pooled physical debris in the soil colour) fly from the teeth, spill off a heaped bucket, pour when dumping and roll ahead of the dozer blade; `Vehicle/SoilHeap` mounds fill the excavator / loader / hoe buckets, pile against the dozer blade and fill the tipper bed by volume; buckets dump into the tipper bed (bed centre as the target) and the load stays usable (containers feed crafting within 5 m, the tipper unloads into containers or onto the ground).
- [x] **Road cars sat low, tyres overlapped panels.** *Done:* `rideHeight` sign fixed (it lowered the body; the tuning slider now lifts), default 0; `BuildCar` stands the body on its wheels (belt above the tyre at arch bump, +2..4 voxels of sill clearance) and adds rocker panels, side markers, fuel cap, valance, rain gutters, wipers and an aerial; `CarveWheelArches` carves to the wheel's real position plus `ArchBumpVox` and also carves the cut doors / hood; `VehicleDriver.archLift` clamps the wheel mesh inside its arch (physics travel unchanged).
- [x] The homestead moved behind the machine row (the loader bucket reached the awning).
- [x] **Real cars modelled in Blender**: Fiat 126p, Renault 5, Citroën BX, XM, Xantia, Lancia Ypsilon (843), Fiat 500, Peugeot 205, 206, 207 CC, 405, 406 Break, Fiat Multipla. *Done:* `tools/blender/cars.py` lofts each body from its real dimensions (profile, beltline, plan rounding, tumblehome, pillars, doors, lamps, bumpers), voxelizes it at 0.08 m with material codes and renders side / 3/4 previews; `Designs/ModelCars.cs` builds the vehicles (shell, interior, seams, cut doors and hood, sockets); new `wheel_compact` (0.31 m). They join the fleet, wrecks and garage recipes.
- [x] Real cars: the Blender 3/4 preview camera is off-frame; the BX rear wheel spats and the Multipla's split windscreen line are not modelled yet; engines are the generic i4 / i6 / diesel (no flat-twin for the 126p and 500).
      *Done (2026-10-13):* `cars.py` previews aim at the body's centre and fit the ortho scale to the projected body (both views); BX rear wheel spats (`ModelSpec.spats`: the skin outboard of the tyre over the upper half of the rear wheel is kept by the arch carve via `VehicleDesign.archKeep`, black panel gap round it); the Multipla's windscreen base is a black cowl line (`ModelSpec.cowl`); new part `engine_twin_air` (650 cc air-cooled four-stroke twin, 70 Nm, `EngineSpec` 360° crank alternate firing, `PartLibrary.Cars.cs`) in the 126p and 500.

## 26. Quality of life & unattended acceptance playtests `T2–T3` — planned

The README describes the public feature contract. An implemented checkbox above is not evidence that a
feature is tested. This programme adds a second, traceable acceptance layer: every advertised feature must
work, explain its state, survive the transitions it promises, and leave the player a sensible next action.
All stages below are **planned**, including the runner and CI gates; none is claimed to exist yet.

**Starting point:** `Game/FrameStats` already provides `-autotest`, `-towtest`, frame logging, profiling and
screenshots. Its road pilot starts a fixed-seed game, can recover a stuck/flipped car, and quits after a timed
run. That is useful performance infrastructure, but a normal quit is not an assertion that the journey or
other features passed. `Controls.Inject` supplies an action press, not a complete held/released keyboard,
mouse or gamepad test driver. Extend these seams without replacing their existing profiling use.

**Unattended means:** one repository command builds or selects the matching player, creates disposable test
profiles, launches every required process, supplies all input, collects evidence and exits. No clicks, focus
changes, controller, Unity MCP session, dialog dismissal, save selection or human judgement may be needed
for a run to finish. Unity MCP remains useful for developing and debugging scenarios, not a prerequisite
for the acceptance runner. A clean machine still needs documented Unity modules, licensing and display
prerequisites; missing infrastructure must produce a bounded, explicit failure rather than prompt or hang.

Automation can demonstrate usability and comfort proxies, not prove subjective enjoyment. Measure clear
feedback, predictable controls, readable information, recoverable mistakes and uninterrupted progress.
Screenshots alone do not establish that crafting, driving or trading works; state assertions alone do not
establish that the player can see and understand it.

### Q0 — Feature coverage contract and measurable budgets
- [ ] Create a versioned feature-to-scenario manifest from **every README feature group**, roadmap systems
      1–25 and the content registries. Each entry names its prerequisites, supported modes, observable success,
      expected failure, recovery, persistence/authority requirements, timeout and evidence. New registered
      content without coverage fails validation; intentionally unsupported combinations are explicit gaps.
- [ ] Separate catalogue checks (all recipes, vehicles, items, stations and blueprints resolve) from behavioural
      checks (each distinct mechanic succeeds through gameplay). Use pairwise combinations for interacting
      settings and terrain, plus mandatory regressions; do not mistake one representative car for all vehicles.
- [ ] Define budgets before implementing each scenario: completion time, input-to-feedback latency, action
      count, rollback distance, readability constraints and performance on a named hardware/settings profile.
      Distinguish in-game time from wall time. Record baseline and target; fail on the target, not a silently
      moving average. Only explicitly reviewed changes may replace a baseline.
- [x] Classify outcomes as PASS, FAIL or BLOCKED, with a reason. Missing prerequisites, unexpected skips,
      unsupported advertised behaviour and missing evidence cannot count as PASS for a required suite.
      *Done (2026-10-13):* `Outcome` Pass/Fail/Blocked with a reason everywhere (`Check`/`Block`), exit 2 for blocked-only runs, junit `<skipped>` for BLOCKED, coverage BLOCKED/NOT_RUN; a run whose suite/filter matches nothing is now BLOCKED (`run.empty`), not an empty pass. Open: required vs optional suites.

*Exit evidence:* a coverage report with zero unassigned advertised features; unresolved gaps stay visible.

*Progress (2026-09-30):* `tools/acceptance/manifest.py` builds and checks `Assets/StreamingAssets/Acceptance/manifest.json`
from every README feature bullet and vehicle row (51 features: 11 mapped to scenarios, 40 explicit gaps, 0 unassigned; by 2026-10-13 the manifest has 56 features, 54 mapped, 2 explicit gaps: character creation, sea bases);
runs write `coverage.json` per feature. Catalogue checks (`catalogue.recipes`, `.vehicles`, `.manifest`) are separate from
behavioural ones: 440 recipes resolve against 22 station types, all 77 knowledge gates have a loot, trade, media or
research source, all 52 vehicle prefabs are complete. PASS / FAIL / BLOCKED with a reason are reported. Budgets per scenario
are still missing.

### Q1 — Reproducible runner, input and isolation
- [x] Add a CLI entry point and process supervisor with per-step and whole-run watchdogs. Exit code 0 means
      every required assertion passed; crashes, timeouts, assertion failures and blocked required scenarios
      exit nonzero. A separate watchdog must still report a hung Unity main thread or failed startup.
      *Done (2026-10-13):* runner flags + per-scenario timeout + run deadline + watchdog thread (exit 3), `run.py` external kill, exit 4 infrastructure; nested helper coroutines are now stepped by the runner, so the scenario timeout stops them too; `run.py --startup` kills a player whose runner never reports (failed startup, exit 3). Open: per-step timeouts.
- [ ] Use temporary saves, preferences, output folders and multiplayer ports; never read or overwrite the
      player's slots or autosave. Record seed, commit/build hash, scenario version, platform, settings and
      random streams. Restore input, clocks, audio settings and processes even after failure.
      *Progress (2026-10-13):* results.json records the commit (`MADMAX_COMMIT`, set by `run.py` from git) and the settings JSON; the runner releases injected key holds between scenarios. Open: random streams, multiplayer ports, restoring settings/audio after a failure.
- [ ] Add full press/hold/release, pointer, scroll and gamepad input through the production input path, with
      observable menu focus and readiness conditions. Keep semantic adapters for fixture setup and state
      inspection. A UI scenario must actually navigate its UI, not invoke the success method directly.
      *Progress (2026-10-13):* `Controls.InjectHold` (Down → Held → Up through the same queries the game reads) + `ActionPress.Hold(act, frames)`; scenario `controls.hold_release`. Existing: `VirtualKeys` (real keyboard device) in `ui.pages`/`survival.conservation`, real mouse scroll in `ui.mouse_wheel`. Open: pointer position/click and gamepad injection.
- [ ] Seed fixtures before the observed segment; disclose granted resources, teleports and clock changes in
      the report. End-to-end progression scenarios obtain resources and travel normally. Never let the road
      pilot's automatic recovery hide a mobility failure; test player recovery as a separate explicit action.
      *Progress (2026-10-13):* fixtures are disclosed (`Fixture`, in results.json); the player's own recovery is now an explicit, measured action (`vehicle.recover_key`: roll a car onto its roof, RECOVER key, `recover_response` budget 0.5 s). Open: progression chains still grant mid-chain.
- [ ] Emit machine-readable JSON and JUnit results, step timings, logs and replayable input traces, plus
      screenshots/state snapshots on failure. Validate the harness itself with an intentional failed assertion,
      crash and hang: each must produce a bounded nonzero result and useful evidence.
      *Progress (2026-10-13):* results.json / junit.xml / coverage.json / step times / logs / failure screenshots, harness self-tests (`harness.*`, `tools/acceptance/selftest.py`), and `run.py` writes `replay.sh` (exact command + commit). Open: replayable input traces, state snapshots on failure, a process-crash self-test.

*Exit evidence:* the same scenario passes from a cold player and repeated editor Play sessions with domain
reload disabled; deliberately broken runs reliably fail without user input.

*Progress (2026-09-30):* `Game/Acceptance/AcceptanceRunner` (player flags `-acceptance <suite> -results -scenario
-runtimeout`, editor entry `StartInEditor`) runs every scenario with its own timeout, a watchdog thread for hangs and the
run deadline, exceptions during a scenario fail it, and exit codes 0/1/2/3; `tools/acceptance/run.py` supervises a player
build (kills it past the deadline, exit 4 when no build). `Game/Profile` (`-profiledir`) puts saves, settings and hints in a
disposable folder. Each scenario gets a fresh seed-7 world; fixtures (placements, grants, clock changes) are disclosed;
results.json, junit.xml, coverage.json and failure screenshots with contact / engine / gearbox evidence. The fast suite
(16 scenarios) passes in the editor. Open: production-path keyboard/mouse/pad input, harness self-tests (deliberate
fail / crash / hang), a player-build run in CI.

### Q2 — First session, navigation and everyday comfort
- [ ] Run boot/title → new game → FIRST STEPS → scavenge → drive → craft → place/use a home piece → trade →
      return home → save → restart/load. Cover starting-kit and no-kit rules separately; a prebuilt homestead
      must not accidentally complete or block objectives. Exercise all three slots and autosave in test profiles.
- [ ] Check that interaction prompts identify the actual target, show the rebound key/gamepad action and
      explain why an action is unavailable. Repeated use, cancel/back, full inventory and opening a menu
      while moving must neither lose items nor leave controls captured or the player trapped.
- [ ] Exercise inventory, crafting, build placement, health, skills, map/waypoint, journal, garage, help and
      settings through their real controls. Cover long names/lists, empty states, queued actions and scrolling;
      measure action counts for common tasks so adding polish does not add needless steps.
- [x] Exercise walking, sprinting, crouching, vaulting, mantling, sliding, rolling and grappling, including low
      ceilings, failed ledge clearance and moving platforms. Verify input release/cancel and safe landing.
      *Done (2026-10-13):* `survival.parkour` (walk, sprint, slide, vault 0.9 m, mantle 1.8 m, refused mantle without headroom, crouch under a 1.4 m bar, roll from 4.5 m vs a flat landing, grapple, inputs released stop the move), `build.frame` ladder climb, `player.momentum`. Open: moving platforms.
- [ ] Check all seven camera modes, pixel-height extremes, independent HUD sizes, colour-blind mode, unit
      changes, shake disabled and keyboard/gamepad rebinding. Assert essential text stays on-screen, selected
      items remain visible and warnings remain distinguishable without colour or audio alone.

*Exit evidence:* both first-session routes finish from the menu with no fixture grants after starting;
all navigation paths can be cancelled safely and saved settings survive a fresh process.

### Q3 — Driving, machinery and every way of travelling
- [ ] Run every registered vehicle, including the 13 Blender cars, through spawn, enter, start, move, steer,
      stop, reverse, exit and reload. Add dedicated suites for bikes/sidecars, horses, tracked machines,
      aircraft, boats and submarines; their control models need their own acceptance criteria.
      *Progress (2026-10-13):* `vehicle.drive.<prefab>` now also checks steering (heading change at full lock, metric `turn_1_5s`); the generated list no longer drops its fallback (a dangling `else` in `AcceptanceScenarios.All`). Dedicated suites exist: `mobility.bike.*`, `mobility.aircraft*.*`, `mobility.boat.Skiff`, `mobility.submarine`, `vehicle.crawler_mud.*`, horse riding in `animals.herds_taming`. Open: reload per vehicle, the other boats.
- [ ] Lock in the recent fixes: machine wheel contact/body clearance, cars separating after contact, forward
      and reverse from rest, hill hold and automatic gears, ride height and tyre/arch clearance. Compare dry
      and wet slopes with suitable tyres and loads. The skinny-tyre Trabant on wet 15° sand should hold safely
      when grip is insufficient; do not demand impossible climbing from every vehicle.
- [ ] Measure pedal/steering response, the 0.35 s forward/reverse hold, braking distance, camera settling and
      low-speed manoeuvring. Test digital and analogue input, manual/automatic transmission, damaged parts,
      cold starts, wrong/empty fuel, fluids, punctures and service. Warnings must precede avoidable stranding.
- [x] Fit/remove compatible parts, armour, attachments and mounted weapons; tune, paint and apply decals.
      Check actual handling/weapon effects, shown stats, costs, sockets and saved/networked appearance.
      *Done (2026-10-13):* `mobility.parts_swap` (stats, mass, wear, saved sockets), `mobility.armour_paint` (paint, decal, armour cost, reload), `mobility.weapons`, `metal.ladder` (tuning kits). Open: networked appearance.
- [ ] Test winch, crane, towing, tanker transfer and transporter loading with conservation and attachment
      assertions across saving and streaming. Dig → visibly fill bucket → dump into tipper → unload/use soil
      must transfer the same resource quantity; visual heaps and clods must agree with useful cargo.
- [ ] Test taxi/take-off/land/reverse, shore launch/mooring, dive/surface, cabin oxygen/power, underwater dock
      and safe dismount. Include a blocked exit, overturned vehicle and depleted air/fuel recovery route with
      clear feedback; no silent teleport or invulnerability during the measured segment.

*Exit evidence:* vehicle coverage is complete, known regressions are deterministic tests, and each mobility
family has a successful journey plus a deliberate failure and supported recovery.

### Q4 — Crafting, survival and the productive home
- [ ] Validate the recipe/blueprint/resource dependency graph for missing IDs, unreachable unlocks and circular
      gates. Execute each distinct station mechanic; test every recipe's inputs, outputs and requirements.
      Require end-to-end chains for food, medicine/clothing, vehicle service, soil → metal → road, electricity,
      irrigation → harvest, and slipway/diving/sea-base construction without mid-chain inventory grants.
- [ ] Assert conservation for storage transfers, queues, output collection, cancellation and reload. Cover fuel
      substitution, fuel also used as an ingredient, actual paid-fuel refunds, quality, knowledge, full queues,
      nearby container range boundaries, missing power and interrupted work. The displayed blocked reason
      must agree with the actual gate, and finishing a job must give visible/audible feedback once.
- [ ] Exercise foundations, plans, doors, furniture, claim/home services, structural damage and salvage. Check
      costs and refunds, reachable stations, moving cargo and collision clearance. Build power/water networks,
      disconnect/reconnect a branch and confirm only the affected consumers stop and recover.
- [ ] Run hunger/thirst, spoilage/sickness, injury treatment, radiation, temperature, wet clothing, shelter and
      diving air through warning → action → recovery. Verify armour/tool wear, repair, research/books/VHS and
      skill progression. Use accelerated simulation clocks only where appropriate; do not change physics time
      scale to make a driving or responsiveness test pass.
- [ ] Test gardening, pests, greenhouses, irrigation, fishing/trawling, livestock feeding/breeding/products and
      hunting/butchery. Follow outputs into actual recipes and trade; a decorative animation is not completion.

*Exit evidence:* the resource ledger balances with declared sinks/sources, all required production chains
finish, and interruptions explain themselves without losing work or trapping progression.

### Q5 — World journeys, persistence and long-range consistency
- [ ] Keep seed 7 as a regression anchor and add a checked-in corpus of at least 20 seeds covering difficult
      roads, rivers, coastlines, settlements and site placement. Run a rotating additional seed nightly and log
      it for exact replay. Validate reachability with actual player/vehicle clearance, not only map connectivity.
- [ ] Travel from home through town trade, a dangerous site and a different biome, then return with useful
      cargo. Cover equator, poles, the ocean date line, altitude and underwater space. Assert destinations,
      waypoints and interactions remain meaningful across streaming and coordinate wrapping.
- [ ] Save/restart while crafting, towing, carrying soil, injured, farming, in a storm/fire and aboard a boat/sub.
      Compare canonical state by stable identity with documented float/time tolerances, then resume the action.
      Include interrupted/corrupt saves and prior supported save versions; fail safely with an actionable message.
- [ ] Leave and revisit a settled area after days/seasons: inventories, terrain/ruts, damage, crops, animals,
      production, jobs and faction consequences must obey a documented off-screen policy. Test the boundary
      of bounded persistence (such as saved rut chunks) and prevent disappearing player-owned progress.
- [ ] Exercise day/night, seasons, rain/snow, drying/puddles, fire, radiation, decay and overgrowth together.
      Check that effects agree with traction, visibility, shelter and resource use, including after reload.

*Exit evidence:* all fixed-corpus journeys complete within their budgets, save/reload resumes meaningful
activity, and any intentional simulation/persistence limits are recorded rather than hidden by test fixtures.

### Q6 — People, wildlife, conflict and shared-world authority
- [ ] Complete dialogue, trade, errands, contracts, town chains, faction reputation, companions and escorts;
      test acceptance, refusal, expiry, abandonment and reload. Check race and Last Engine objectives/rewards.
      Rewards, inventory and standing must change exactly once, including repeated interaction attempts.
- [x] Test combat, weapon/ammo/reload, armour, surrender/parley, alarms/defences, companion commands, taming
      and riding. Verify perception and wildlife reactions, friendly/hostile distinctions and corpse loot;
      threats must leave understandable feedback and a supported escape or recovery path.
      *Done (2026-10-13):* `people.combat` (reload, jam, armour, surrender, corpse search), `combat.melee_feel`, `people.raiders` (parley / bluff), `people.defences`, `defence.ladder`, `people.companion`, `animals.herds_taming` (perception, wolves, taming, riding), `npc.fire_fight`. Open: escape/recovery-path assertions.
- [ ] Launch a host plus two scripted clients and a separate dedicated-server suite automatically. Exercise
      joining, simultaneous trade/build/loot, travel, reconnect and authority transfer where supported, under
      declared latency/loss/duplicate-packet conditions. Assert ownership and resource conservation at authority.
- [x] Turn documented multiplayer limitations into explicit failing/blocked coverage entries: client dialogue
      and trading currently act on local copies; taming/riding still require the host. Close the authority gaps
      before claiming those advertised loops pass in multiplayer; never silently substitute a host-only test.
      *Done (2026-10-13):* `online.client_trade_authority` and `online.client_taming_authority` (suite full) are always BLOCKED with the documented reason, so the coverage report shows them until the authority gaps close.

*Exit evidence:* social/combat objectives pass solo, supported shared actions converge without duplication,
and unsupported client actions remain visible in the release coverage report until implemented.

### Q7 — Coherent feedback, presentation and performance
- [ ] Capture deterministic rendered checkpoints at noon, dusk, night, rain/snow, indoors, in a moving car and
      underwater. Check missing/pink materials, clipped HUD, unreadable prompts, camera obstruction and effect
      density. Use tolerance/masks for intentional motion; a golden image cannot replace gameplay assertions.
- [ ] Verify engine, wheel, tool, station, weather, wildlife, NPC and radio events select the correct audio
      channels, stop when their source stops and respect settings. Test captions and radio coverage/schedules;
      roof/cabin attenuation must agree with shelter. Add clipping/overlap and repeated-loop diagnostics.
- [ ] Measure input-to-visible response and blocked-action feedback against Q0 budgets. Record camera jerk,
      repeated prompt changes, forced recovery, unnecessary menu actions and unexplained idle time as comfort
      regressions. Keep subjective art/audio review optional and separate from the unattended gate.
- [ ] Benchmark built Development players on a declared reference machine at fixed resolution/settings:
      60 fps target, median/p95/p99 frame times, worst stalls, allocations and memory after warm-up. Include
      towns, big bases, streaming, collisions/destruction, machinery and aircraft. Set explicit per-scene budgets;
      keep screenshot capture and profiler instrumentation separate from normal timing runs.
      *Progress (2026-10-13):* `FrameStats` logs a run summary on quit (`[fps] summary`: frames after a 20 s warm-up, median / p95 / p99 / worst frame, GC count, managed / allocated / reserved memory). Open: reference machine, per-scene budgets.

*Exit evidence:* rendered and audio checks pass on a graphics-capable worker, timing budgets pass on the
reference profile, and headless simulation success is never reported as visual/audio coverage.

### Q8 — Continuous gates and unattended soak
- [ ] Add a fast PR suite (target ≤5 minutes after build), nightly full feature/seed coverage and a release
      suite on supported Linux, Windows and macOS players. Build/license/display failures report infrastructure
      failure; the existing release workflow must not publish a tested badge just because compilation passed.
- [ ] Add a ≥2-hour soak combining travel, production, combat, weather, saves and reconnects. Separately
      advance world days/seasons to expose off-screen progression errors. Check bounded memory/object/audio
      growth by returning to the same warmed-up scene, finite physics values and no repeated exceptions.
- [ ] Publish coverage, results and failure artifacts with retention and an exact replay command. Re-run to
      diagnose flakes, but retain the first failure; retries and quarantine must not make a required test green.
- [ ] Gate release on complete required coverage, no data-loss/duplication/soft-lock/crash failures and agreed
      QoL/performance budgets. Report remaining optional gaps explicitly. Teardown all test processes/profiles
      and verify the runner needs no input from launch through final report.

*Exit evidence:* a scheduled run and a clean-machine release run both finish unattended with trustworthy
exit codes; deliberately injected faults block release. Implement stages in Q0 → Q1 → Q2–Q6 → Q7 → Q8 order.

## 27. World coherence — gaps to investigate and close `T2–T3` — planned

The world already has broad systems. Tighten their shared rules before adding more content. The candidates
below are **audit questions, not confirmed defects**, except where the roadmap already states a limitation.
First reproduce the gap in a Q-stage scenario, then implement the smallest coherent improvement and retain
the regression. Regional weather remains deliberately deferred; this plan does not reopen that scope.

| Priority / area | Potential gap and tightening work | Broadening only when the connection works | Unattended evidence |
|---|---|---|---|
| P1 · Home and journey rhythm | The homestead has moved safely behind the machines; first-home garage service, bed/respawn, raid ownership and FIRST STEPS integration remain suggested. Audit rest, water, repair and turning space along ordinary journeys so maintenance does not become surprise stranding. | A clear return-home milestone and useful roadside rest/service stops, driven by measured travel gaps. | Q2/Q3/Q5: first return, loaded truck access, service, sleep and respawn without overlapping machinery or losing the route. |
| P1 · Recipe and exploration gates | Do every blueprint, ingredient and required station have an attainable source under the promised loot/start rules? Audit low-loot progression, tools needed to obtain their own ingredients, and land/sea construction dependencies. | Multiple believable salvage/trade/research sources for critical bottlenecks, with journal clues. | Q0/Q4: dependency validation plus no-grant progression under scarce and standard rules. |
| P1 · Roads, rivers and access | A connected road graph may still have unusable grades, fords, doorways, moorings or site entrances. Audit physical access for loaded trucks, pedestrian routes, current and boats; road crossings must not silently block waterways. | Bridges, marked fords, slipways and settlement-specific parking where measured access needs them. | Q3/Q5: clearance and journey corpus, including water crossings and alternatives when one route is impassable. |
| P1 · Shared shelter and water rules | Audit agreement between roof/cabin sound attenuation, rain exposure, warmth, greenhouse conditions, air pockets and submarine oxygen. Shoreline/river surface, flow, buoyancy and wet-ground feedback should describe the same place. | Better seals, ventilation or drainage only when players can understand and inspect the underlying rule. | Q4/Q5/Q7: cross a doorway, waterline and docking collar; compare HUD, effects, audio and actual resource/health changes. |
| P1 · Persistence, streaming and the seam | Audit everything carried across the date line or out of simulation: trailers, cargo, companions, jobs, claims, utilities and map routes. Define what persists, approximates or expires; review active escort restoration and bounded terrain history explicitly. | An event/state ledger for off-screen work and consequences if existing snapshots cannot preserve causal continuity. | Q5/Q6: leave, wrap, reload and return with stable ownership, conserved cargo and resumable objectives. |
| P1 · Multiplayer truth | Client-local dialogue/trade and host-required taming/riding are documented limitations. Resolve authoritative transactions and ownership before describing the whole survival loop as shared. | Cooperative home/companion roles after basic client actions are equivalent and duplication-safe. | Q6: two clients compete for one item/animal/job; exactly one authoritative result persists after reconnect. |
| P2 · Seasonal production and trade | Seasonal prices already exist. Audit whether spoilage, storage capacity, physical vendor stock and travel cost permit the advertised harvest-to-winter loop. A price multiplier alone may offer no practical player choice. | Preserves/cold storage, seasonal stock/chores and shortage-driven deliveries, as suggested above; local-season markets stay dependent on the deferred hemisphere/weather decision. | Q4/Q5: harvest, preserve/store, transport and sell across a season; report net costs, spoilage and available demand. |
| P2 · Ecology and resource renewal | Audit whether biome fauna, fish, crops, water and mineable resources support local livelihoods. Check hunted-herd recovery, livestock feeding and whether fence protection reflects a real enclosure; avoid both infinite free output and irreversible early depletion. | Distinct regional specialities and renewable alternatives where scarcity creates a dead end rather than a useful trade journey. | Q4/Q5/Q6: repeated harvest/hunt/return cycles, enclosure breach and sustained farm inputs/outputs. |
| P2 · Town identity and visible consequences | Existing factions, markets, jobs, radio and journal should agree on who lives here and what changed. Audit repeated/contradictory news, quest destinations after streaming, shortages without causes and settlements with identical practical roles. | Notice-board news digest, occupation-based stock and local repair/rebuilding work tied to actual events. | Q5/Q6/Q7: complete or fail a local job, revisit and compare prices/standing, NPC response, journal and radio without duplicate rewards. |
| P2 · Damage, maintenance and a calm home | Audit how raids, fire, decay, weather, spoilage and personal needs compound while away. Ensure warning and repair options precede avoidable losses; existing cosy props should support a usable rest space amid those pressures. | Configurable pressure/recovery windows and rebuilding services if measured upkeep crowds out exploration and crafting. | Q2/Q4/Q8: an ordinary expedition returns to understandable, recoverable consequences within declared upkeep budgets. |
| P2 · One visual and acoustic language | Audit the new Blender cars against voxel scale, palette, interaction highlights, collision silhouettes and damage feedback. Machinery soil, station progress, weather and environmental sounds should communicate actual state, with room for radio/dialogue. | Remaining vehicle identity details and station-specific sound families once readability and source/state agreement pass. | Q3/Q7: tyre/body clearance, load/progress readability, camera/weather matrix and competing audio events. |

- [ ] **W0 — Audit:** map each row to a concrete existing implementation, reproducible scenario and player-facing
      consequence. Close non-issues with evidence; distinguish defects, intentional limits and design choices.
- [ ] **W1 — Tighten:** fix P1 contradictions first and add their Q-stage regressions. Keep the current 9.6 km
      world and existing content useful before increasing map size or adding more unrelated systems.
- [ ] **W2 — Broaden:** choose P2 additions only where the audit demonstrates a broken or thin connection;
      attach resource costs, feedback, persistence and authority requirements before implementation.
- [ ] **W3 — Re-run the whole loop:** scavenge → build/tune → travel/trade → conflict/dialogue → settle/produce
      → standing → farther travel, with no fixture shortcuts. Compare task budgets and consequences against Q0,
      and publish what improved and what remains unresolved rather than ticking off content volume.
- *Audit 2026-10-13:* W0–W3 stay open: they are play-through audits (Q-stage scenarios run in the editor over whole
  journeys), not code. Gaps the code audit closed on the way: walkable lake ice, rain in dug pits, liquids on built
  floors, cans keeping their contents in storage, residents' seasonal work and the fishers' catch, traffic-paced wreck
  stripping, the HOME ledger's actions, power draw on station pages.

## Acceptance findings and user requests (2026-09-30, later)

Defects the first acceptance runs caught (each now a regression scenario):
- [x] An automatic held on the brake at rest parked itself (rest sleep won the race against selecting reverse), so some cars never reversed. Only the handbrake parks an occupied automatic now.
- [x] The hill hold braked on top of a drive force already at the grip limit, broke the tyres loose and left a car stuck at a standstill on a 12° slope; it now only uses the grip the drive leaves free (the Sedan climbs 60+ m in 6 s).
- [x] The bulldozer carried its blade in the ground by default, cut a trench wherever it drove and then could not back out of it. The blade is carried up until the operator lowers it.
- [x] FIRST STEPS counted the homestead's prebuilt workbench as built by the player.
- [x] The tipper test showed how easily a truck parked against a crawler gets launched: acceptance fixtures now park clear and check reach.

User requests:
- [x] **Scratches**: vehicles sliding along vehicles, walls or rocks throw sparks (dust off stone, splinters off wood), play a grinding loop and wear the paint down to bare metal where they rub (`VehicleDamage.OnCollisionStay`, `VehicleBreakables.Scrape`; saved). Building surfaces only get sparks and dust (their templates are shared).
- [x] **CAR DEFORMATION setting** (GAMEPLAY tab: OFF, LIGHT, NORMAL, HEAVY, EXTREME) scales dent depth; network impacts carry the raw depth and every peer applies its own setting.
- [x] **Breakable windows**: knocks, gunfire and melee hits shatter the glass around the hit (quads removed from `Body/Glass`), with flying shards and broken glass left on the ground (`World/Shards`, pooled). Door windows are part of the door panels and do not break yet.
- [x] **Breakable lights**: a knock at a lamp breaks it: the lens goes dark, that headlamp dims the beam (both gone: dark), a broken tail lamp stays off; red, amber or clear shards stay behind. Saved with the vehicle (`VehicleSave.wear`).
- [x] **Visible sun and moon**: a sun disc (white-gold high, orange low, red on the horizon) in the perspective views, the moon also by day; the top-down views show a small sun / moon arc beside the clock.

Still failing in the fast suite (kept visible, not skipped):
- [x] `vehicle.drive.Bulldozer` on the start-road pad stopped dead in reverse. *Fixed* by the modelled tracks (below): 22 m forward / 18 m back in the same test.
- [x] `visuals.sun_moon` after world reloads: the disc was right (104 m out on the sun's bearing); the camera just was not facing it. The check now asserts the bearing and distance; the screenshot stays evidence only.

## User requests (2026-09-30, evening)
- [x] **Modelled tracks for all tracked machines** (bulldozer, excavator, paver). *Done:* each track bears on five points a side (sprocket, three road wheels, idler, each on its own suspension); skid steering (outer track leads, inner holds back or reverses, pivot turn at a standstill, sideways slew while steering) instead of steered wheels; `Vehicle/CrawlerTracks` draws the belt as voxel links (plate + grouser) running round the wheels with the ground speed, road wheels roll. The old painted tracks became a frame rail and a fender. Heavy mud with a full load: bulldozer 28 m / excavator 33 m in 8 s, turns on the move, pivots on the spot (`vehicle.crawler_mud.*`).
- [x] **Carried things hidden while driving**: the tool or prop in hand is put away when seated and comes back on foot.
- [x] **Get in / out animations** (setting GET IN / OUT ANIMATION, on by default): walk to the door, the door swings open on its front hinge, slide into the seat, door shuts; out the same way. Only for the player's own key presses (scripted enters, fleet cycling and tests stay instant); bikes, aircraft, boats and walk-in cabins keep their own ways (`vehicle.boarding`).
- [x] **Natural machine controls**: arrows and Q/E (rebindable Tool actions) work the tool when the vehicle has one — excavator up/down boom, left/right swing, Q curl/dig, E dump, Shift+up/down stick; backhoe loader arms and bucket, Shift for the rear hoe; dozer blade up/down, angle, pitch; tipper bed; paver screed, Q pave, E material; crane hoist, slew, Shift+up/down boom, Q grab. Number keys still work; on machines Shift+E/Q shifts gear.
- [x] **Graphical key map** (F1 or H): a drawn keyboard with every key bound in the current situation (on foot, driving, machine, crane, aircraft, build mode) lit by group, with a legend; replaces the text help sheet.
- [x] **Quieter prompts**: the context prompt is a small list on the left (key cap, icon where the action has one, label) instead of a bar across the bottom.
- [x] **Far view in first and third person** (also hood / bumper cameras): the far terrain carries the view to ~450 m (fog end) instead of the chunk radius.
- [x] **Flight controls**: arrows pitch (setting FLIGHT PITCH: up climbs, or stick style), Q/E roll, left/right and A/D rudder, W/S throttle; Space / Ctrl still pull up / push down.
- [x] **Exhaust smoke by engine condition**: a thin haze from the tailpipe (exhaust part outlet, else under the rear bumper) that grows with load; engine wear, worn plugs, a clogged filter and old/low oil turn it thicker and blacker, diesels soot up under load, two-strokes always smoke a little, cold air adds white vapour, a cough when it catches. Worn engines also smoke from the engine bay, and a leaking or missing radiator steams at the front while warm (`vehicle.exhaust_smoke`).
- [x] **Tracks grip in mud**: track mud grip 1.05 → 1.6 (grousers bite harder in soft ground than rubber on dry tarmac), rut drag divided by √footprint (a long track bridges ruts). Loaded in mud (0.52): bulldozer 30.6 m / 8 s, slip 0.00, turns 46° in 3 s (was 19°); excavator 33.9 m, slip 0.01. Open: the excavator drifts 8 m while pivoting in mud.

## Depth ladders (gap review 2026-09-30)
Survey of the catalogue (440 recipes, 138 build pieces, 47 foods, 18 crops, 34 animals, 21 fish, 71 parts). Rich: clothing (57 garments), vehicles (52), garage parts (168), weapons (28), tools (23). Every production chain below should run **by hand → workshop → powered/industrial → vehicle-scale**, each tier faster, bigger batches or better quality, and each tier fed by the previous one.

| System | Today | Gap in the ladder | Plan |
|---|---|---|---|
| **Cooking** | stove and oven share the same 17 dishes; counter 3, smokehouse 3 | no fire-side tier, no industrial tier, no preserving beyond smoking, no brewed drinks | **A.** campfire piece (spit and embers), stove = pot and pan dishes, oven = baking, powered kitchen range (triple batches, finer), cannery (tins that never spoil), tea / beer / cider |
| **Farming** | garden plot, planter, greenhouse, hoe, watering can, drip line, sprinkler | no field on open ground, no tractor or implements, irrigation has no controller, 3 tree kinds, no silo | **B.** hoe tills field beds on the ground; tractor + plough (tills rows), seeder (plants from the pack), harvester (into the cargo bed), sprayer (water / fertilizer tank); irrigation timer + river / lake pump; grain silo; more orchard trees (pear, olive, walnut) |
| **Roads** | dozer grades, paver lays asphalt, roller compacts, mixer makes asphalt / concrete | nothing below the paver, nothing after it | **C.** hand-laid gravel and cobbles (shovel / rake), dump-truck gravel spreading, line painter (road paint), road signs, guard rails, curbs, bridges (timber, steel deck), player roads counted by the map router and AI drivers, potholes that need patching |
| **Metalworking** | furnace 7, arc furnace 4, kiln 3; every vehicle part at the garage | no smithing tier, no machining tier, no steel | **D.** forge + anvil (hand tools, horseshoes, nails), steel (iron + charcoal at the arc furnace), machine shop (lathe / mill: engine blocks, gearboxes, brakes from cast parts), parts gated by the shop |
| **Vehicle parts** | wheels 14, engines 13, cargo 9; exhaust 2, radiator 2, lights 3, armour 3, weapons 4, spoiler 1, snorkel 1 | thin categories; no gearbox, brakes, suspension, seats, fuel tanks | **D.** gearbox (close / wide ratio, 4x4 transfer), brake kits, suspension kits (lift, lowered, heavy), long-range tank, more exhausts / radiators / lights / armour |
| **Husbandry** | trough, nest box, saddle; eggs, milk, cheese, manure | no feed chain, no wool, no honey, no stable | **E.** hay (scythe / baler implement), feed mix, shearing (wool → loom), beehive (honey, wax → candles), stable (horse stamina), butchering table |
| **Textiles & leather** | loom 3, tanning 1 (leather) | leather has one recipe; no spinning or rope | **E.** spinning wheel (thread, rope), leather goods (saddlebags, belts, holsters, leather armour), sails and tarps (boats, shade) |
| **Water** | rain collector, filter, well, pump, water tower, boiling | no sea-water tier, no quality levels, no canals | **F.** solar still → desalinator (power) on the coast, water quality (dirty / boiled / clean) in tanks, irrigation canals from rivers |
| **Power** | generators (fuel, coal), solar, wind (2), water wheel, batteries | producers only; no control | **F.** switches, timers, light sensors, pressure switches (automation), biogas digester (manure → gas → generator), electric tools |
| **Medicine** | bandage, splint, disinfectant, pills, antibiotics, painkillers, antivenom; medical bay only on vehicles | no herbal tier, no clinic | **G.** herbal poultice / tea (campfire), first-aid kit, clinic bed + surgery table (sets fractures, removes shrapnel), blood bags |
| **Mining** | pickaxe, excavator drill, outcrops, wash plant, dynamite | no hand-panning tier, no mine structures, no powered crusher | **H.** gold pan / sluice box, mine supports and ore carts, powered rock crusher (stone → gravel for roads) |
| **Building materials** | wood / brick / concrete / scrap walls | no processing ladder | **I.** sawmill (logs → planks, powered = faster), brick moulds + kiln bricks, prefab concrete panels from the mixer |
| **Defence** | 5 pieces | thinnest build category | **I.** watchtower, sandbags, landmines / tripwires, motorised gate, gun emplacement tiers |
| **Hunting & trapping** | bows, rifles, fish traps | no snares, butchering by hand only | **E.** snares and cage traps, butchering table (more meat and hide, cleaner) |

Order: A (cooking) → B (fields and tractor) → C (roads) → D (metal + parts) → E (husbandry, leather, traps) → F (water + power control) → G, H, I. Each stage adds its catalogue entries, a piece or implement per tier, and an acceptance scenario that walks the whole ladder.

### Stage A — cooking ladder (done 2026-09-30)
- [x] **Campfire** piece (stone ring, crossed logs, spit, billy can; warms 5 m, glows): spit-roast meat and fish, roast corn, potatoes in the embers, bug and mushroom skewers, flatbread, herb tea, boil 2 L of water. Slow (18 s) and 1 wood a dish.
- [x] **Wood stove** gains pot-and-pan dishes: meat stew, fish soup, pancakes, herb tea. **Electric oven** gains baking: meat pie, roast dinner, cornbread, apple pie.
- [x] **Kitchen range** (2.4 kW, a steam generator runs it; tier 1 = finer food): every stove and oven dish in triple batches in the same time (`range_*`, generated).
- [x] **Cannery** (1.5 kW): tinned stew, meat, fish, vegetables, fruit and beans (4 tins, never spoil), bottled water. **Still** brews beer (wheat) and cider (apples).
- [x] 17 new foods and drinks, tins priced higher by traders. Recipes 440 → 491, station types 22 → 25. Scenario `cooking.ladder` cooks one dish on every rung (6/6).
- Found: the small generator (1.5 kW) cannot run the electric oven (2 kW) on its own — batteries or a steam generator are needed. Consider showing the draw next to the station name. *Done (2026-10-13): the crafting page footer shows READY ... DRAWS n W / PAUSED - NO POWER (NEEDS n W) (`MenuSystem.Catalogue`).*

## Storyline (storyline.md) in tandem with the depth ladders
The campaign KEEP THE LIGHT ON is built stage by stage alongside the depth ladders: each story stage needs verbs a depth stage delivers, so they ship together. The missing-system register (`Story/Systems.cs`) is the single list of what a quest still waits for; `story.contract` reports it.

| Story stage | Quests | Needs from the depth ladders | Status |
|---|---|---|---|
| **N0 story contract** | all 56 quests with stable ids, prerequisites, needs | — | **done**: `Story/` (QuestDef, StoryLibrary + .Side, Systems, Story runtime, StoryCast, StoryAnchors, StoryTalk), `story.contract`, `story.anchor_seeds` |
| **N1 first hour** | A1, B1, A2, S03, S06, S09, S24 (done) | evidence records, tagged vehicles, ground goals (done); **B fields** (done) | playable: `story.first_hour`, `story.a2`, `story.side_quests`, `story.work_quests` |
| **N2 parallel lives** | A3, A4, B2, B3, C1-C3 | **C roads** (C2), **F water quality + power control** (C1, B3), cooking (A, done) | **playable**: `story.chapter_two`, `story.b3`, `story.c1`-`c3`, `story.allocation` |
| **N3 rescue and reckoning** | A5-A6, B4-B5, C4-C5, F1 | **C bridges** (C4), **G clinic** (A5), residents, broadcast, allocation, player convoy | **playable**: `story.a5`, `a6`, `b4`, `b5`, `b5_defence`, `c4`, `c5`, `f1`, `f1_routes` |
| **N4 other stories** | S01-S24, P1-P3, L1-L5 | **F cold storage** (S01), **E animal treatment** (S12), nonlethal bout, performance | **playable**: `story.s01`-`s24`, `p1`-`p3`, `last_engine` |
| **N5 delivery** | voices, captions, pacing, multiplayer ledger, relocation | — | open |

Order of work: N1 remainder with stage B (fields and tractor) → N2 with stages C and F → N3 with G and the remaining story verbs → N4 with E, H, I.

### N0/N1 (done 2026-09-30)
- [x] **Story mode**: NEW GAME → MODE: STORY. You wake beside a box trailer on its side out on the road 450-900 m from the first town, with worn clothes and nothing else; one stranded Fiat 126p (loose battery lead, empty tank) 13 m away; FIRST STEPS is off; a radio line in the journal.
- [x] **Anchors** bound per seed along the road network (wreck, satchel, badge, car, Nell's stop, first town, garage at the bend, relay mast, depot bunker, dispatch city), with clearings that keep wild props (cactus, bushes, ore) off the scenes. Checked on 8 seeds.
- [x] **Quest engine**: Reach / Talk / Have / Build / Craft / Drive / Event conditions, alternatives per step (route recorded), optional steps in parallel, rewards through a once-only ledger (save/load pays nothing twice), journal lines, waypoints, the HUD objective line, saves in `SaveData.story`.
- [x] **Cast**: Nell Mercer at her stop (authored name, title, temper, outfit, female voice), story topics in her dialogue hub.
- [x] **A1 SOMEONE LEFT THE RADIO ON**: satchel (wrench, knife, canteen, tin, bandage) → Nell's stop → talk (she lends a claw hammer and patch tin) → patch her rain collector in build mode (or build a new one) → 8 L fuel → reconnect the battery lead ([G] with a wrench), fill up and drive 150 m, or walk to the town → optional convoy badge.
- [x] **B1 THE SIGN STILL STANDS**: Nell offers the garage at the bend (brick walls, garage doorway, half a roof, junk, a sign); break or dismantle the barricade, build a workbench inside, plant a claim flag.
- Found while testing: the start yard sits 40 m from the start town (the story start moved out on the road); a wreck spawned on its side inside the ground was thrown 70 m up when physics woke it (wrecks at an angle are now rested on the ground first); the player spawned against a cactus and bled out (scene clearings).
- [x] **Side quests in any world** (sandbox too): givers stand at the edge of the first town while their quest is on offer, "?" pins on the map, story topics in their dialogue. **S09 THE SMALLEST WAR** (Una Pritch: three dry beds and a scarecrow planted where no crow lands; water the beds with the bed's own action, build a scarecrow beside them, tell her; seeds + Farming) and **S24 THE BIRTHDAY MACHINE** (Gus Alder: craft the machine at a workbench from scrap, copper, glass and rubber, hand it over and pick its sound: horn, bell or whistle; the choice is remembered).

### Stage B — fields and the tractor (done 2026-09-30)
- [x] **Field beds on open ground**: the hoe on open ground tills a 2 × 2 m field bed on a world grid (not on roads, water, rock, slopes or under things); field beds are garden plots that yield 2.2× a small plot, sown, watered and harvested like any bed (`Building/Fields`).
- [x] **Tractor** (red, cab, big lugged rear tyres `wheel_tractor`, small front `wheel_tractor_front`, four gears to ~40 km/h, rear three-point hitch) with four implements on the tool socket: **plough** (four furrows: tills two 2 m lanes as it drives), **seeder** (sows from the hopper, then the driver's pack), **sprayer** (waters from the tank, then the pack; booms reach wider), **harvester** (reaps ripe beds into the grain bin). Up/down arrows raise and lower the implement; the tractor's store is the hopper, tank and bin. Built at the garage like other machines and parts; sandbox full-fleet yards include one.
- [x] **Irrigation timer**: sprinklers and drip lines on its water network run only in its window (dawn and dusk / dawn / overnight; [E] cycles), unless a bed is bone dry; without a timer, midday watering costs 60 % more water (evaporation).
- [x] **Grain silo** (1200 kg store).
- Verified by `farming.ladder`: hoe tills a bed, sown by hand; the tractor ploughs 8 beds in one pass, seeds 8, sprays 8, and harvests 72 wheat; the timer holds at noon, lets a dry bed through, runs at dawn.
- Open: tractors at village farms to borrow (S06), hay and feed (stage E, done), more orchard trees (*done 2026-10-13: pear, olive, walnut — `FoodLibrary` trees + fruit, food vendors stock the saplings, they count as harvest*), a proper headland turn in the test (the test places the tractor back at the headland between passes).

### N1 complete (2026-09-30)
- [x] **A2 THE DEAD DON'T BUY DIESEL**: the freight clerk (Holland Cross) turns your chit away; three leads in the first town (the pump's receipt spike, Mae the cook, tyre marks at the repair stall), any two will do; the receipt or a 10-scrap fee makes the clerk talk; Len Pike at his stall testifies, goes to the boss or runs (remembered). Evidence records (receipt, cook, manifest) survive losing the paper.
- [x] **S03 HEARSE POWER**: Sol Moss's black hearse is seized on the road out of town; winch it behind your car or patch its engine and drive it to the chapel; a tip for no new scratches.
- [x] **S06 MUD, SWEAT AND GEARS**: Jo Kettle's buried trench: three flags to dig out (ground goals: 0.6 m below where it was), the soil tipped on her patch, her excavator to borrow.
- [x] Quest engine: `Goal.Steps` (any n of listed steps), `Goal.Bring` (a tagged vehicle at a place), `Goal.Ground` (dug / raised since the quest began), priced talk options, evidence, `StoryTag` (saved with the vehicle), `Story.Complete` (chapter skip); optional steps are checked before required ones (a bonus met on arrival counts).
- [x] Engine start odds by condition (user request): 75 %+ always catches, about 80 % at half condition, rarely below 20 %; a failed crank says why (no fuel, wrong fuel, seized, worn plugs, cold). `vehicle.start_chance`.

## Parallel depth blocks (2026-09-30)
Each block is built on its own branch by its own agent against a scaffold in `main` (own partial files for pieces, recipes, parts, tools, game hooks, saved state and scenarios, already registered), then merged, compiled in Unity and tested together. `tools/compile_check.py` compiles any checkout exactly like the editor.

| Block | Scope |
|---|---|
| **Items** | every carriable or attachable thing can be placed in the world and shows there as an object; pop-ups for everything received or handed over |
| **Anim** | interaction animations at vehicles: service, refuel, repair, mount/take parts, siphon, salvage |
| **Roads** (C) | gravel and cobbles, road paint, rock crusher, rake, dump-truck spreading, signs, guard rails, bridges, player roads on the map |
| **Metal** (D) | forge and anvil, steel, machine shop, gearbox / brake / suspension kits, more exhausts, radiators, lights, armour |
| **Husbandry** (E) | hay and feed, shearing and wool, bees, stable, spinning, rope, leather goods, snares, butchering table, animal treatment |
| **Utilities** (F) | water quality, desalination, switches / timers / sensors, biogas, cold storage |
| **MedMine** (G, H) | herbal remedies, first aid, clinic, gold pan and sluice, mine supports and carts, powered crusher |
| **Defence** (I) | sawmill and planks, bricks, prefab concrete, watchtower, sandbags, mines and tripwires, motorised gate, emplacements |

### N2 so far (2026-09-30)
- [x] **A3 A VOICE WITH YESTERDAY'S WEATHER**: June Bell at the relay mast; fuel and start the relay's generator; get the recording module (build a ladder at the mast, or pay her crew 15); play three fragments (the count, old weather, a lift bell); ask her straight (accuse, or let her help). Receiver, the recording as evidence, every mast on the map.
- [x] **A4 THE WEIGHT OF EMPTY TRUCKS**: the pumping depot (a bunker site) with the convoy's trucks at the gate and two Guild guards; watch it, then get in with the forged manifest, a 40-scrap bribe, the service hatch, or through the guards; the store (evidence: diverted cargo); Ren Okafor locked in with it: take the medicine or leave the traced shipment.
- [x] **B2 SUPPER FOR FOUR**: safe water, a kitchen and a table at the garage, four hot portions; Nell brings Vic (salt-route driver) and Ezra (a gardener from the old bus) to the yard; after supper, who is the place for (Vic stops by, Ezra moves in, or nobody yet).
- Engine: Have/Build goals and hand-overs accept "a|b|c"; cast members can move (Nell at the garage for supper); dead cast don't return; story prompts use [E]-style key tokens (the old {Build} placeholders showed raw).

### Blocks merged (2026-10-01)
All eight blocks built in parallel, merged into main, compiled in Unity and run together.

**Items** (`items.world`, `items.feed`)
- [x] Every item and resource can be dropped or placed in the world as its own object (tool meshes, clothing props, icon models, resource crate / sack / jerry can, kit crate), picked up with [E] and saved; the PLACE preview from the pack page (rotate, LMB, Esc/RMB); Q drops the tool in hand.
- [x] HUD item feed of every gain and loss of the pack with labels (bought, paid, sold, made, harvested, reward, found, picked up, dropped...), merged within 1.5 s, sliding out.
- [x] World items online (local to each peer); build-mode costs in the feed. *Done in the online wave (protocol 4); scenarios `online.items`, `items.build_feed`.*

**Anim** (`anim.vehicle_work`)
- [x] Timed work at vehicles: service, refuel walk-up, siphon, battery lead, take/mount parts, repair kit, armour welding, welder/cutter/jack: walk to the spot, pose, prop/tool, sparks and sounds, the hood lifted, effect at the end, cancel without effect; progress bar; setting WORK ANIMATION.
- [x] Work poses online (`online.work_pose`). Pose tuning from captures still open.

**C Roads** (`roads.ladder`)
- [x] Gravel and cobbles by hand (road rake, tamper); rock crusher (2 kW); road paint (line painter on set asphalt/concrete, white/yellow); the tipper spreads gravel on the move, the paver lays gravel; potholes from heavy traffic, patched with the rake; signs, guard rail, curb, bollard; timber (8 m) and steel (12 m) bridges; player roads on the minimap and map.
- [x] Player roads in `RoadRoute` and AI driving (`RoadRoute.FindForDriving`, `AiDriver.DriveTo`; `roads.ai_player_road`).

**D Metal** (`metal.parts`, `metal.ladder`)
- [x] Forge and anvil; steel at the forge, furnace, arc furnace; castings and a cast V8 block; machine shop (2 kW, tier 1.5): gearbox (close / wide), transfer case, HD brakes, lift / lowered / heavy suspension, long-range tank as kits at the tuning bench; parts 71 → 83 (exhausts, radiators, lamp pods, armour, ducktail, forged V8); horseshoes.

**E Husbandry** (`husbandry.ladder`)
- [x] Scythe and tractor baler (hay), feed at the workbench and feed mill, richer mixed rations; sheep, shears, wool regrowth, spinning wheel (thread, felt, rope), loom; beehive (honey, wax by forage and weather), candles, mead; stable; bark tanning and the leather bench (boots, belts, cuirass, chaps, satchel, saddle, saddlebags; Belt slot); snares and cage traps; butchering table; animal wounds and treatment.

**F Utilities** (`utilities.ladder`)
- [x] Water quality (silt, oil, salt, sewage, fallout; wells fouled by oil ground, latrines, troughs; filter cartridges; the water test kit; tainted drinking); sea water, solar still, desalinator, salt and salted foods; power switch, timer, light sensor, float switch, valve, load breaker (essential / normal / low shedding; overloads stall generators); biogas digester and generator; fridges by temperature, freezer, ice box.

**G/H MedMine** (`medmine.ladder`, `.clinic`, `.works`)
- [x] Poultice and willow-bark tea (campfire), first aid kit, clinic bed and medicine cabinet, surgery table (shrapnel); gold pan (river stretches run thin), sluice box, stamp mill (concentrate), gold smelting, ring; mine props and lamps (unpropped deep pits slump), mine rail and a pushable ore cart.

**I Defence** (`defence.ladder`)
- [x] Saw bench and sawmill (planks), brick mould and kiln bricks, prefab concrete panels; plank / fired-brick / panel walls (stronger upgrade chain); sandbag wall, watchtower (perch, far view, lookout), landmines, tripwire, gate frame + motorised gate, MG nest → auto turret; raids count and trip the new works.
- Fixed on integration: `Explosion.Blast` shoved vehicles with ~26 m/s per point of power (dynamite ≈ 200 m/s); now capped at 14 m/s. `ToolLibrary.Has/AllIds` include block tools (hotbar icons, repair page, network index).

### The whole campaign playable (2026-10-01)
Built by seven parallel quest branches (per-quest hook files), merged, compiled in Unity and run scenario by scenario; every quest in the catalogue is `Build.Playable` and every story system is ready except `relocation` (N5).

**Arc A: KEEP THE LIGHT ON**
- [x] **A5 NO ONE RIDES IN THE BACK**: the Guild transfer yard, Wes stabilised four ways (first aid kit, clinic bed in the hut, the town medic, a bed at the staging point), everyone or one group plus an agreement, contracts bought / worked off / sneaking out / stopping the escort, Mara's live list (seats, water, medicine) and the player's go at the trailer door, two trips or Otis's bus, survivors choose where they go, the convoy horn and Ada's schedule (`transfer`).
- [x] **A6 TELL IT STRAIGHT**: Ivo's Remnant checkpoint, the cross-check by the clerk's book or Ivo's runner, full / redacted (+ Wes's logbook) / held with June, captioned rehearsal and broadcast (radio flash, journal transcript, two days of follow-up news), Mara stays / travels as a companion / departs (`broadcast`).
**Arc B: the garage**
- [x] **B3 LIGHTS WORTH COMING BACK TO**: fuel, solar or wind/water supply, a powered lamp and one essential service, roof, bed, sanitation; Nell's compressor surge on a load breaker (LOW and restart, unplug, or a battery bank); the refuge (respawn at the garage bed, the lantern under the sign).
- [x] **B4 WHO GETS A KEY?**: rooms or lodging at Mae's, Hester's grievance over her father's pump (ask Judd or Nell), rules (the pump comes home on a well, house rules, Judd sleeps in town), one staffed service (repair labour, crops, medical resupply), residents eating from the garage stores at 19:00 (`residents`).
- [x] **B5 THE NIGHT WE STAYED**: Silas Vance's levy; Moth's double ledger; a written levy, passage for repairs, exposing the ledger, or three defences and the flag at the gate (defence 9+ deters, below that a real `BaseRaid`); a shared meal, a keepsake wall, the charter (home / paid workshop / cooperative refuge).
**Arc C: THE PRICE OF PASSAGE** (Sera Dune; the village nearest the first town, the road between, the next town out)
- [x] **C1 WATER HAS NO FLAG**: Isaac's bowser at a Guild toll post, the test kit at its valve against the toll book, the leak mended in build mode; the measured toll, the Nomads' salt road, the booked sixty, or taking it; tow it to the village.
- [x] **C2 THE CHEAP ROAD**: the trial truck logs minutes, litres, knocks and wear per route (warden's track vs the public road through the wash); the warden's pass, fill / surface / bridge the wash, or shared hauling; matching tyres and a haul contract.
- [x] **C3 ENOUGH TO GO AROUND**: twelve cans against sixteen claimed; ask what each really burns, mend the waste, share by need / evenly / one favoured openly (`allocation`), deliver the posted shares, a provisional passage agreement; clinic bed, produce stall, lamps.
- [x] **C4 A BRIDGE YOU CAN AFFORD**: the washout at a ford; fill, a timber or steel deck, or the ferry; Greta's digger and tipper on return terms; the loaded-tipper test; the road-work sign.
- [x] **C5 NO EMPTY SEAT**: hire drivers, load Bo's crates, highway or back track, the Remnant checkpoint arranged (`Checkpoint.OpenFor`), the convoy behind you (`player_convoy`), a breakdown and Wren's cart on the road, terms at the depot (`arc_c_concession|coop|bilateral`).
**Finale**
- [x] **F1 THE LONG WAY HOME**: Ada's offer at the Guild dispatch yard; support from earlier outcomes (Mara, June, Nell, Sera, residents, the agreement, the crossing); the fuel week shared over five places (supply from campaign transport, fixed when posted, what can be met); control by published terms (refusal says why), a work stoppage, or the yard seized with its pumps whole; consequences and an autosave before the commitment; the run by road, shortcut (a hurt scout) or water (a loaded skiff), escorted, lost loads replaced; A BETTER BARGAIN / OPEN ROADS / BREAK THE LOCKS (strong / weak variants, factions, fuel prices, radio flash, `campaign_done`); the last call on the refuge radio.
**Side stories** (sandbox and campaign)
- [x] S01 A FRIDGE FULL OF FLOWERS (Orla's cold cabinet), S02, S04, S05 NOT THAT KIND OF SHOT (Amos's range, the dummy round), S07, S08 THE WEDDING AT THE WRONG END OF THE ROAD (`performance`), S10, S11, S12 THE DOG AT PLATFORM THREE (`Animal.TreatWith`), S13, S14 SOMETHING IN THE WELL (the oily well, the pumpjack uphill), S15 THE ORGAN RUNS ON DIESEL (load balancing, a procedural organ), S16-S18, S19 THE BELL BENEATH THE WATER (diving), S20-S22, S23 NO TEETH, STILL TROUBLE (`nonlethal_bout`), P1-P3 personal threads, L1-L5 THE LAST ENGINE chapters.
- Fixed on integration: shots counted by `WastelandGame.ShotsFired` (a reload and a shot in one frame hid the round from S05); S19's shore search used `Sample.water` (the lake level past the shoreline) and fell back to dry ground; tests that found "the first story prop" picked other quests' props (scoped to their anchors); frame-throttled hooks vs fixed test waits (wait for the state instead).
- Open: `relocation` (N5), voices and captions for the new cast, pacing on real routes (the dispatch city can be kilometres out), recurring convoy traffic after C5, B5's low-defence raid and A6's travel/depart branches without scenarios, multiplayer story ledger.

## Scheduled update (2026-10-01)
Done this pass (above, ticked): station sounds, road news on the boards, seasonal vendor stock, the root cellar; the
acceptance runner's `-scenario` filter takes a comma list of prefixes. All four new scenarios pass in the editor (seed 7).

### Suggestions (tie the experience together)
- [x] **Harvest chores in the villages** (*Done (2026-10-13):* `Npc.Chores`: in the season's harvest half two in three residents walk out to the village / town-plan field rows 8-17 h with a scythe and cut them as they go (`DeformableTerrain.Trample`, the flora regrows), before the first snow they split wood at the town fire with an axe (chop sounds, chips); the errands were already `NpcLore.Chores`): residents walk out to the village crop rows at harvest and to the woodpile before the first snow (`Npc` Gather mode on `Fields`/flora crop rows), and `NpcLore` errands follow (bring in the crop, split firewood) — the seasons visible in town life, not only in prices.
- [x] **Road news you can act on** (2026-10-02: `TownNews.MarkWreck` ties the skirmish headline to its wreck; a board's ROAD NEWS line [E] marks it — `WastelandGame.MarkNewsWreck`, pin `WRECK (NEWS)`, waypoint, `SaveData.newsPins`, cleared on arrival; scenario `towns.news_wreck_pin`): a skirmish headline on a board pins the wreck site on the map (`SpawnRoadWreck` position → `TownNews` entry → `WastelandGame.Map` pin, cleared when looted), so reading the board leads to salvage.
- [x] **Hear the base from the road** (2026-10-02, the map half: `WastelandGame.WorkshopPins` — the player's stations > 40 m away with work show WORKING n% / NO POWER / READY (n) as chips on the map and under THE WORKSHOP AT HOME in the journal (Enter = waypoint home); scenario `base.workshop_pins`. The muffled cabin loop is still open): a powered or fired station heard from a parked car — loops through the vehicle cabin (muffled) and a HUD "WORKING" chip on claim pieces in the map, so a player away from home knows the queue is still running or has stalled for fuel/power.
- [x] **Preserving as a winter plan** (2026-10-02: `WastelandGame.Pantry/PantryNote` — 4 s after AUTUMN / WINTER arrives, a toast + journal PLAN line counts the pack and the player's containers in days of eating, split into food that keeps through the cold at its storage spoil rate and food that rots first, and names smoking / canning / the cellar; scenario `food.winter_pantry`): the WINTER market toast could name what the player has stored vs. needs (days of food in containers by spoilage-adjusted count) and suggest the smokehouse/cannery — one hint that ties seasons, spoilage and cooking stations together.
- [x] **Cellar in the story** (*Done (2026-10-13):* `Residents.TakeFood` eats from the coolest store first (root cellar / cold store by `SpoilFactor`); after supper `Residents.Larder` counts what rotted in the garage's stores since yesterday (Hester complains, journal HOME, toast) and in autumn / winter says once per season whether the larder holds to spring (`Rotted`, `LastLarder`)): B2/B4 (supper, residents eating at 19:00) could draw from the root cellar first and complain when stores rot — residents as the reason to keep a pantry.
- [x] **Seasonal fishing** (2026-10-02: `FishDef.seasons` warm / cold / run profiles × `BiteRate(..., season, iceHole)`, cold-water fish ignore the cold penalty; under −4 °C fresh water ices over: casts become a hole cut at the edge (≤ 3.5 m, 2.6 s chop, ICE FISHING, cold-water fish ×1.4); a first-cast hint names what is biting this season; the trawl net follows the season too; scenario `fishing.seasons`): species bite rates by season (`FishLibrary.BiteRate` × season), so the summer rods at the stalls mean something and winter ice fishing (hole through `Weather.Ice`) is its own activity.


## Fluids: containers, siphon and pour, blends (2026-10-01, wave 4)
- [x] Hand liquid containers equipped like tools: jerry can 20 L (`tool_jerrycan`), fuel can 5 L, water bottle 1 L, bucket 10 L, oil jug 4 L (`Items/FluidContainers`, recipes at the workbench, one empty jerry can in a sandbox new game). Each holds one blend; contents per container in the pack (`WastelandGame.Fluids`: `CansOf(id)`, index 0 in hand; containers leaving the pack leave their contents in a per-id pool the next one takes back), weight counted, shown on the HUD (toolbar line) and in the inventory label; LMB dips at open water, drinks clean water, douses fires. No hand drum: 200 L is tanker / pump / vehicle-tank work.
- [x] Siphon (K) and pour (G) with the container in hand open a radial of what fits: a vehicle's fuel tank, sump and cooling system, a tanker, a pump (player-built pumps take clean fuel back), a liquid generator, open water / the sea, the pack's liquids (pure liquids back into the pack), the ground. Siphon fills up to the container's capacity with that single fluid (sources of another family than the can's contents are not offered); pour shows the blend the tank ends up with ("WON'T RUN" / "ROUGH"). Timed work at vehicles (filler for fuel, engine bay for oil / coolant, the container in hand). Without one in hand, K equips the best container in the pack and opens the choice; no container at all = the old siphon into the pack (each liquid of a blend to its own resource). Scenario `fluids.siphon_refill`.
- [x] Blends by volume in every tank (`FluidMix`: fractions per liquid; pour blends, draining / burning keeps the fractions) replace the binary wrong-fuel flag: `FuelBlend` (table below) feeds `PowerFactor`, burn, misfires, exhaust smoke, start chance and wear; `Fault.WrongFuel` = a blend that won't run, `RoughFuel`, `BadOil`, `BadCoolant`, `Frozen`. Pumps, tankers and the garage still top up only the same kind; the tanker hose carries the blend as it is. Saved (`VehicleSave.fluids`, old saves load as the pure tank kind; `SaveData.blockFluids` for containers) and sent with the vehicle meta. Scenarios `fluids.mixing`, `fluids.save`.

| engine | in the tank | runs fine up to | rough until | won't run above | effect |
|---|---|---|---|---|---|
| petrol | ethanol | 100 % | – | – | E85: −4 % power, +30 % burn; harder cold starts near E100 |
| petrol | diesel | 10 % | 25 % | 25 % | misfire, smoke |
| petrol / diesel | water (+dirty, sea ×3 wear, coolant ×2 wear) | 10 % | – | 10 % | −15 % power and +15 % burn at 10 %, misfires, slow engine wear |
| petrol (4-stroke) | oil | 4 % (2T mix: smoky) | 20 % | 20 % | smoke, fouled plugs |
| two-stroke | oil | 8 % (the proper mix) | 25 % | 25 % | pure petrol counts as pump pre-mix |
| diesel | petrol | 15 % (−6 % power, knock) | 35 % | 35 % | damage on cranking a dead blend |
| diesel | ethanol | 5 % | 20 % | 20 % | poor |
| diesel | engine oil / seed oil | 15 % / 30 % | 50 % / 80 % | 50 % / 80 % | smoke; veg oil runs a diesel |
| any | crude | 2–5 % | 12–30 % | 12–30 % | thick smoke, wear |

Contaminants add up (Σ fraction / limit > 1 = won't run). Sump: protection = 1 − 5·water − 2.5·fuel − 1.5·crude − 0.6·seed oil (oil ages faster and the engine wears below 0.95). Cooling: the COOLANT resource is a ready mix rated −37 °C, so the freezing point is −37 °C × its share (pure water 0 °C: a parked engine below it won't start, the radiator cracks 5 °C under it); fuel or oil in the radiator cut cooling, sea water corrodes it.

### Suggestions
- [x] Containers in storages and on the ground keep their own contents (*Done (2026-10-13):* `WastelandGame.CanStates`: a can moved pack → storage (any `Container`, bags and car compartments too) or dropped onto a `WorldItem` takes its litres along through a same-frame transit in the pool, and back; storage that rolls a can as loot gets an empty one; saved as `b` (placed piece id) / `w` (world item position) lines in `blockFluids`, storage that isn't a placed piece hands its litres back to the pool on save): a per-instance item state in `Container` / `WorldItem`.
- [x] Water network ends (taps, barrels, `WaterOutlet`, vehicle water tanks) and the still / refinery as siphon and pour targets; drums as vehicle cargo. (*Done (2026-10-13):* `NetEnd` takes water back into its network (clean stays clean, dirty/sea counts dirty, refuses other liquids) and is offered at any node with a water capacity — tanks, barrels, the `cargo_water_tank` part; `StationEnd`: a still / refinery takes a pure liquid as feed (into storage within 5 m, else the pack) and gives what it made from its tray. Drums: a `storage_drum` built on a vehicle rides along and is a `StoreEnd` like any other.)
- [x] Settling: water sinks to the bottom of a parked tank over hours, a drain plug lets the first litres out (clean the tank without siphoning it all). (*Done (2026-10-13):* `VehicleSystems.settled` (4 game hours parked, stirred back in by driving / running; saved in the fluid state `s=`), `SettledWater`, `DrainPlug`; the K radial lists DRAIN PLUG (settled water + 0.3 L, a sample when nothing settled).)

## Scheduled update (2026-10-02)
Done this pass (ticked above under the 2026-10-01 suggestions): seasonal and ice fishing, road news that marks the wreck,
the winter pantry plan, the home workshop on the map. Scenarios in `Acceptance/Blocks/UpdateScenarios1002.cs`.

### Suggestions (tie the experience together)
- [x] **Ice you can walk on** (*Done (2026-10-13):* `World/LakeIce`: the lake nearest the player gets 6 m tiles (box colliders + `StructureGround` decks) at the water level below −4 °C; thickness from the frost and the winter, shore tiles thinner early on; bears 3.5 kg × cm² — creaks near it, breaks under heavier loads (vehicle falls through, floats), refreezes after half a day; not saved (derived from the weather)): lakes below −4 °C get a walkable ice sheet (a flat collider at the water level, thin near the shore early in winter, cracking under vehicles above a weight) — ice fishing from the middle of the lake, winter shortcuts, and a real risk for a heavy truck.
- [x] **One home ledger** (2026-10-03: the journal opens with a HOME section — `WastelandGame.HomeLedger`: per owned claim the workshop (working / stalled / ready), power in / out + battery, water held, weathered pieces, and the pantry days; scenario `base.home_ledger`): a HOME page (or the journal section) that merges the workshop chips, the pantry days, the power / water balance of the claim grid and BaseUpkeep wear into one "how is my base" screen — the systems already report separately.
- [x] **Wreck news decays** (2026-10-03: skirmish wrecks are recorded (`WastelandGame.RoadWrecks`, `NoteRoadWreck`, saved `SaveData.roadWrecks`); reached after 1.5 days they have lost parts, loose parts, storage and fuel by age (`ScavengedShare`, 85 % by day 4), between 1.5 and 4 days a scavenger is still on site (`NpcDirector.Scavengers`); scenario `towns.wreck_scavenged`): marked wrecks get picked over by other scavengers after a few days (fewer parts, a scavenger NPC on site if the player is late), so acting on the news soon pays.
- [x] **Fish in the economy by season** (2026-10-03, the price half: `Market.FishSeason` from the weighted mean bite factor of the season — fresh fish cheap in the summer runs, dear in winter, preserved fish a third of the swing; the season flash adds `Market.FishNews`; scenario `market.fish_seasons`. Fishermen residents *Done (2026-10-13):* see *Fishermen at the lakes*): `Market.SeasonFactor` for fish (cheap in the summer runs, dear in winter) and fishermen residents who sell the catch of the season — the bite tables feeding prices.
- [x] **Pantry in the residents' talk** (2026-10-03: in autumn / winter residents, leaders, shopkeepers and wanderers answer WILL MY STORES SEE ME THROUGH THE WINTER? with `Dialogue.PantryVerdict` (temper × `Pantry` days vs. the days to spring, rot warning); scenario `talk.pantry`. The B2/B4 supper hook: *Done (2026-10-13):* `Residents.Larder`): B2/B4 residents and village NPCs comment on the player's stores at supper ("the larder holds" / "we'll be eating boot leather by spring") using `PantryNote` numbers.
- [x] **Workshop alerts on the radio** (2026-10-03: the player's stations > 40 m away call the HOME FREQUENCY on lost power, power back and a finished queue (`WastelandGame.UpdateHomeRadio/HomeCall`, `RadioNetwork.Flash(..., post: false)` keeps it off the boards, journal HOME); scenario `base.home_radio`): a powered station stalling while the player is away triggers a short WasteTalk-style radio ping on the home frequency (`RadioNetwork.Flash` with `at` = the claim) — the radio as the base's voice.


## HD consistency pass (2026-10-02)
The HD rework left the ground, the cabin and a few runtime-built visuals in the voxel / pixel look. The new acceptance
scenario `hd.audit` (full suite) walks every visible renderer near the player in full-resolution mode, on foot and in
two cabins, and lists what is not HD by shader and object (counts + captures) — rerun it after each HD change.

### Done
- [x] **Terrain**: with the HD pack, chunks get smooth normals, corner colours blended from the neighbouring cells and
  per-corner weights of nine ground classes (`World/HDTerrain`, `TerrainClass`; `CellColor` sets the class); HDLit's
  `_TERRAIN` variant (`RuntimeMaterials/HDTerrain.mat`) tints tileable detail albedo + normal maps (texture arrays,
  triplanar on slopes, a second slow sample against tiling) with the game's own palette. Maps:
  `tools/blender/hd/terrain/make_textures.py` → `Resources/HDTerrain` (gitignored, rendered by `release.sh`).
- [x] **Dashboards**: `DashboardHD` geometry (dials with ticks and red zone, smooth needles, seven-segment gear and
  speed, LED fuel / temperature bars, nine lamps) instead of the 64×24 canvas whenever the HD pack is on.
  (Superseded 2026-10-13 by the data-driven instrument clusters below; `DashboardHD` is gone.)
- [x] **First-person cabins**: `HDVehicleBuilder.FitCabin` keeps the driver eye inside the glasshouse and 18 cm under
  the roof skin (the MonsterTruck's HD seat sat behind the cab wall, its eye in the roof) and seats the gauge cluster on
  the HD dashboard surface when it is 15–60 cm ahead.
- [x] **Ground cover**: grass blades are tapered crossed blades off the voxel grid in HDLit (two-sided); other plants
  are lit boxes in HDLit for now.
- [x] HD model import: root LOD groups were never added (`GetComponent ?? AddComponent` with Unity's fake null);
  fixed (postprocessor version 5 re-imports the pack once). The same pattern fixed in 15 runtime spots.

### Second pass (2026-10-02, evening): everything left from the audit
- [x] **Flora**: every ground-cover shape has an HD form (flowers with petals, shrub cushions with leaves, fern fronds,
  mushrooms, wheat ears, cattails, branching twigs, cactus with an arm, glowing pods, coral, kelp leaves, boulders with
  urchins) — `DeformableTerrain.Flora` HD primitives; crops in plots / planters / planted trees (`CropVisuals.BuildHD`:
  leaf rosettes, tall stalks with cobs, canopies, fruit, pumpkins) on two-sided `HDShapes.Foliage`.
- [x] **Overgrowth**: leaf clusters lying on the walls (hanging, more and longer with age), moss cushions on tops, weeds
  on ledges, blossoms / spores (`Overgrowth.BuildOverlayHD`, worker thread).
- [x] **Wildlife**: every species was exported; `hd.audit` now spawns lizard, scorpion, rat, crow and snake and checks
  their rigs carry HD parts.
- [x] **Welded armour**: `VehicleArmor.BuildHD` — bevelled sheets per material cell (steel with rivet rows, crooked
  scrap patchwork, composite tiles on a strip), curved guard plates round the wheel arcs, round grille bars.
- [x] **Small runtime pieces** (`Rendering/HDBits`, `Rendering/HDShapes`): debris chunks and round puffs, resource
  pickups (scrap plates, planks, stones, glass, a tyre, a sack), glass / lamp shards, switch levers, smooth bulbs (every
  `PoweredLight` bulb, town lamps; the voxel bulb hides under an HD one), runway lamps, the refuel jerry can and oil jug,
  the fishing bobber, projectiles (arrow, bolt, stone, bottle), stamp-mill stamps, clock hands, the garden crow, soil
  heaps in buckets / blades / beds, painted (flat) vehicle decals; smoke / dust particles, blood splats and the loading
  wheel at high resolution.
- [x] **Clothing**: the 28 garments without an HD mesh are modelled in `tools/blender/hd/character/garments.py`
  (poncho, hazmat, dive suit + brass helmet + air tanks, welding mask, chitin / tyre / leather vests, gauntlets, moto
  helmet, bee veil, leather boots / chaps, work / gun belts, fanny bag, back brace, five packs, three shoulder bags,
  duffel, suitcase); all four wardrobes re-exported.
- [x] **Road paint and ruts**: the highway centre line is painted by HDLit from per-vertex road distance (uv1), sharp at
  any distance; rut depth rides in the vertex alpha and blends the mud layer per pixel.
- [x] **Sky and far**: smooth cloud cushions, sun disc with glow ring, a smooth phased moon, star diamonds, tube
  lightning; `FarTerrain` (the distance in ground and air views) gets shared smooth vertices and HD class weights (it was
  drawn with the HD terrain material but no splat data).
- [x] **Title and Moon**: bevelled 3D logo bars (sunset face, red extrusion, cream lip), neon tube lettering, smooth
  regolith colours, round boulders, the scrap rocket and old lander built from tubes and plates, a smooth Earth.
- [x] **HUD font**: setting HUD FONT (AUTO = HD with full-resolution rendering, PIXEL, HD) — `HudTextGraphic` draws the
  canvas's text runs as smooth glyphs from `Resources/HDFont/hud_font.png` (`tools/hud_font.py`, DejaVu Sans Condensed
  Bold) in the pixel font's exact layout; later opaque panels hide text under them.
- [x] **Fixes found on the way**: HD animals never dressed (the part lookup hit the rig bone named like the mesh) and,
  once found, stood on end and faced backwards (placement now from the imported node + a half turn when the import faces
  -Z, `HDAnimal.Placement`); the wardrobe script shared one mesh between `tshirt` and `tshirt_worn`, so the plain shirt
  carried the worn material and every t-shirt outfit drew both; `PlayerCharacter.SetFirstPerson` re-applied the HD
  arms-only meshes every frame (now on change only); `hd.vehicles` destroyed the player with the walk-in Hauler.

### Still open
- [ ] The boot film (`StreamingAssets/Intro/intro.webm`) shows the old voxel title: re-record with `IntroRecorder`.
- [ ] Flora / overgrowth are procedural HD stand-ins, not exported Blender assets; HUD icons drawn into the pixel canvas
  (minimap, prompt icons) stay pixel art.


## Scheduled update (2026-10-03)
Done this pass (ticked above under the 2026-10-02 suggestions): the HOME ledger at the top of the journal, home-frequency
radio calls from the workshop, news wrecks picked over by scavengers, seasonal fish prices, pantry talk. Scenarios in
`Acceptance/Blocks/UpdateScenarios1003.cs` (all five pass in the editor, seed 7). `fishing.seasons` reports BLOCKED in
the editor world (no lake within 2.4 km of the start) — look at the lake search radius.

### Suggestions (tie the experience together)
- [x] **Journal job list is flooded**: on day 1 the journal lists ~40 `? NAME` story-cast pins (every authored person
  as a job) before the player's real jobs — group them under one STORY line per quest, or show only quests in progress.
- [x] **Scavengers you can deal with** (2026-10-05, the trade half: what they strip is their stock — `Trade.Hauls`, under the usual price, saved; scenario `towns.scavenger_trade`. Fighting for it: *Done (2026-10-13):* at reputation ≤ `NpcDirector.ScavengerFightsBelow` (−20) the scavenger spawns hostile with a pipe club): the wreck scavenger offers what they stripped for trade (the removed parts go
  into their stock instead of vanishing), or fights for it if the player is hostile — the late arrival becomes a choice.
- [x] **Home frequency needs a radio**: the calls reach the journal always but the radio only when one is on; a cheap
  handheld radio item (`tool_radio`) in the starting kit would make the base's voice part of every trip.
- [x] **Ledger with actions** (*Done (2026-10-13):* `HomeLedger(lines, acts)` / `LedgerAction`: the workshop line waypoints to a stalled (else ready) station, WEATHERED lists the worn pieces worst first with their repair cost and waypoints to the worst, PANTRY names what rots first and waypoints to the nearest preserving station or cold store): HOME lines become selectable — ENTER on a stalled station sets a waypoint, on WEATHERED
  lists the pieces (and their repair cost), on PANTRY opens the cooking stations that would save the rotting food.
- [x] **Fishermen at the lakes** (*Done (2026-10-13):* one resident in three is a fisher (`Npc.Fisher`): 5-9 h outside winter they sit at the nearest shore within 240 m with a rod (casts with a splash); food vendors of a settlement with open water within 260 m sell `TODAY'S CATCH` fresh fish, as much as the season's bite tables give (`Trade.Fishers`, `CatchFactor`)): village residents by a lake fish at dawn (Gather mode at the shore with a rod visual)
  and sell the catch of the season at their stall — the bite tables visible in town life.
- [x] **Seasonal wreck salvage** (2026-10-05: `SeasonPace` summer 1.35 / winter 0.4 scales `ScavengedShare` and the scavenger's window, winter tanks stay frozen and full; scenario `towns.wreck_seasons`. Road traffic: *Done (2026-10-13):* `TrafficPace` — paved highway 1.3, dirt track 1, off-road 0.7 — multiplies the season's pace and the scavenger's window): winter wrecks keep their fuel frozen and their storage (nobody travels), summer ones
  are stripped fastest — scale `ScavengedShare` by season and road traffic.

## Fixes and features (2026-10-04)
- [x] **Engine fire burns the car out**: `VehicleBurn` (90–180 s to a charred, undriveable wreck; 35 % tank explosion
  with fuel aboard; smoulders; 12 % of wrecks spawn charred).
- [x] **Ignition keys and hotwiring**: `VehicleIgnition` (key in / glovebox / lost on wrecks; key items), hotwire by
  holding Service in the seat — anyone, badly; skill `Hotwiring`, trait CAR THIEF, book.
- [x] **Blown tyres leave traces**: rubber burst, rim gouge line, rubber strips, carcass tears off into a bare rim.
- [x] **Explicit looting**: no auto pickup (debris and carving yields too); [E] one, hold [E] for all within 3 m; every
  item has a visible model; the A1 satchel lies by the wreck.
- [x] **Mud on tyres**: picked up in mud, flung as clods, shed on dry firm ground, washed off fording.
- [x] **Plough only unpaved ground**.
- [x] **Limbs**: mangled (hanging on) and severed limbs for the player and NPCs; amputation with a blade (clean at a
  surgery table); effects on hands, gait, speed, climbing, clutch.
- [x] **Prosthetics**: 12 pieces (hooks, claw, carved and machinist's hands, blade / torch / shotgun mount arms,
  hydraulic arm, peg, strut, spring blade, wooden foot); crafted, looted; fitted on healed stumps.

### Suggestions
- [x] **Extinguisher item**: a fire extinguisher to beat a vehicle fire before the tank roll (2026-10-04 scheduled; sand thrown from the shovel: *Done (2026-10-13):* `WastelandGame.ShovelSand` — a shovel swung within 3 m of a vehicle fire throws soil from bare ground, else sand / clay / rubble from the pack, half a burst each; smothers burning pools).
- [x] **Key rings on NPC drivers**: convoy drivers carry their car's key (loot it off the body) instead of leaving it in.
- [~] **HD prosthetic meshes** (2026-10-09: the salvaged cyber arm is now a textured, skinned multi-part HD assembly across all four body shapes; 11 other prosthetics still use voxel props): Blender models for all twelve pieces.
- [x] **Crutch tool**: a one-legged player hops at 30 %; a crutch (two-handed, no tools) could lift that to 55 %.
- [x] **Prosthetics at the clinic vendor**: medical (salvage) traders stock hooks and pegs (2026-10-04 scheduled); surgeons fitting them for scrap: *Done (2026-10-13):* salvage traders offer to fit a prosthetic from the pack on a healed stump for 25 scrap, painless (`Dialogue.Surgeon`, `WastelandGame.SurgeonFit`).

## Interface pass (2026-10-05)
- [x] **No spoilers in player text**: condition / odds / growth / trust in words, fuel blends by smell, symptoms not
  diagnoses, strangers unnamed until met, no hint where a key is.
- [x] **Catalogue menus**: crafting and building with categories on the left, 3D icons in a scrolling grid, a hover
  panel (what it does, what it takes, how long).
- [x] **HD HUD / HD MAP settings**.
- [x] **HD text in editor play mode** (missing CanvasRenderer).

### Suggestions
- [x] **Descriptions for every build piece** (`FurnitureDef.desc`): the panel reads most from the piece's id today. (*Done (2026-10-13):* `FurnitureLibrary.Descriptions`: a written line for all 216 base pieces; city, frame, rig, fluid and salvaged pieces carry their own `desc`; `Describe` falls back to reading the id only for anything new.)
- [x] **survival.loop** still grants loose fuel and presses G: update it to the can-based refuel (2026-10-02 fluids).
- [x] **Search box** in the catalogues, labelled cells sorted A–Z, filter chips (known / can make / can build), unknown recipes greyed, hover-only panel.
- [x] **HANDCRAFT** on foot (N): simple raw-material recipes without a bench, slower, queue of 4, saved.

## Fluids in the open (2026-10-06)
- [x] Ground spills: soak to soil capacity (asphalt 0), then pools that run downhill, fill holes, drain to lakes
- [x] Evaporation by liquid (oil never, water 1, petrol 12×), temperature, sun/shade, humidity, wind, rain; freezing
- [x] Pour stream from any container (trails), scooping puddles, store / tap ends in the container radial
- [x] Makeshift pool, clay pond, storage drum, pond liner, standpipe tap, transfer pump, one-way pipes
- [x] Burning fuel pools, water douses, mud from soaked water, slick oil/ice, well taint from soaked fuel, beds drink, vehicle leaks drip
- [~] Spills on structure floors and vehicle decks (they land on the terrain below today). *Structure floors done (2026-10-13):* `Spills.Cell.floor` — a pour onto a static built floor makes cells at its height (raycast `FloorAt`), nothing soaks in, it spreads and runs off the edge to the ground; saved with the floor height. Vehicle decks stay open (cells are world-fixed; a puddle on a moving deck needs carrier-local cells).
- [ ] Fuel / oil pipe networks (pipes carry water only; other liquids move by container, drain or drum tap). *Audit 2026-10-13: left open — needs a second network kind in `UtilityGrid` (per-net blend, no mixing with water, valve/pump rules); fuel already moves store → store with the `transfer_pump`.*
- [x] Rain filling unlined dug holes (only existing pools, liners and open stores catch rain). *Done (2026-10-13):* `DeformableTerrain.RainPits` — every 2 × 2 m block of terraformed ground dug ≥ 25 cm within 60 m has its low point catch the block's rain (`Spills`, every 5 s while it rains); the soil drinks what it can, clay pits become ponds.
- [x] `survival.loop` refuel step goes through the can radial; A1 pays Nell's fuel in her can (story start had no container)

## Engine sound (2026-10-06)
- [x] Engine specs (cylinders, firing angles, cycle, fuel, displacement, valvetrain, aspiration, cooling, idle by size)
- [x] Event-driven synth: exhaust pulses, headers, pipe, muffler, tailpipe; intake; valvetrain, bearings, slap, diesel clatter, ping, accessories, forced induction
- [x] Starter with compression rhythm, cough, catch flare, coast-down; wear, oil, misfire, cold, fuel blend, exhaust leak and muffler damage modelled proportionally
- [x] Per-cylinder exhaust part damage positions (a leak at the manifold vs the tailpipe sound alike today). *Done (2026-10-13):* `EngineSynth.leakCyl` (a battered engine blows one manifold port: a tick once a cycle from that cylinder, heard in the bay) and `leakPipe` (a dented exhaust part splits the pipe: raw pulses before the muffler, heard at the tailpipe); fed by `VehicleAudio.Feed`.
- [x] Gearbox whine / clutch / driveline clunks (not engine sources). *Done (2026-10-13):* `Audio/DrivelineSynth` (third voice per vehicle in `VehicleAudio.Driveline`): gear-mesh whine (loud in straight-cut reverse and first, worn), diff whine on the overrun, clutch judder while it slips at pull-away, clunks on shifts, engaging drive / reverse and throttle reversal at speed; telemetry `VehicleDriver.Coupling/ShaftRpm`.

## Graphics options (2026-10-06)
- [x] Anti-aliasing (FXAA, SMAA, TAA, MSAA 2/4/8x), upscaling (STP, FSR 1, DLSS 4 on Windows/NVIDIA) with render scale
- [x] Shadow quality (5 levels), ambient occlusion (3 levels), screen-space reflections (HDLit), bloom levels, motion blur, anisotropic filtering
- [ ] DLSS runtime test on a Windows machine with an NVIDIA card (Linux has no NGX runtime; the path is compile-checked only at the Windows build) *(audit 2026-10-13: still open — needs Windows + NVIDIA hardware)*
- [ ] SSR on water (needs a DepthNormals pass on the transparent water shader) and in the orthographic views (URP SSR skips orthographic cameras) *(audit 2026-10-13: open — orthographic is a URP limitation; the water pass needs an in-editor visual check)*
- [ ] GTAO ambient occlusion (needs URP's `MODERN_SSAO` define) *(audit 2026-10-13: open — a ProjectSettings scripting define, not in the script copy)*

## Scheduled update (2026-10-04)
Done this pass (ticked above): story offers in the journal (one "?" per giver, named only once met, at most the three
nearest strangers; jobs in progress always), the fire extinguisher (`tool_extinguisher`: 8 bursts, an engine fire takes
2–7 by how far it has taken hold, stays out 90 s, refilled with sand like a repair; also smothers prop/ground fires and
burning spill pools), convoy drivers who climb out carry the key (`Npc.carriedKey`, looted off the body; back in the
ignition when they get back in), the crutch (hop 0.3 → 0.55, broken leg 0.45 → 0.6, splinted 0.7 → 0.85), the handheld
radio (`tool_radio`, starting kit: a receiver in hand, the home frequency from the pack), prosthetics / crutches /
radios at salvage vendors with real prices; a mount arm's tool always gets a hotbar slot (the starting radio filled the
bar). Scenarios `Acceptance/Blocks/ScheduledScenarios1004.cs` — all six pass in a Linux player build, plus vehicle.burnout,
vehicle.keys, body.limbs, body.prosthetics, base.home_radio, story.first_hour, story.sandbox, items.visible, ui.catalogue,
ui.no_spoilers. `survival.loop` is flaky: once the Sedan sat at 2000 rpm in gear 1 doing 0 m/s in rain (radiator leak),
then passed on the re-run — look at the launch assists on wet ground.

### Suggestions (tie the experience together)
- [x] **The car as a kit** (2026-10-05: `TripKit` under the LOOT panel of an opened vehicle — in the car / in the pack / none, hint `trip_kit`; scenario `ui.trip_kit`. A FIRST STEPS step is still open): a glovebox / boot checklist (extinguisher, first-aid kit, spare key, tyre patch, flares)
  shown on the vehicle's LOOT panel and as a FIRST STEPS tip — every trip starts with deciding what rides along.
- [x] **NPC drivers fight fires too** (2026-10-05: `Convoy.FireWatch` + `Npc.FightFire`; 8/10 trader and 5/10 raider cars carry a bottle, the driver beats the fire and drives on, or gets clear past saving; scenario `npc.fire_fight`): convoy cars carry an extinguisher; a driver whose engine catches climbs out and
  beats it (or abandons the car when it's past saving) — raider molotovs become a fight over a car, not a coin toss.
- [x] **Spare keys and locksmiths** (2026-10-07: `misc_key_blank` (workbench) + using a car key at a workbench cuts a spare (`CutSpareKey`); parts vendors cut a key for a vehicle with a lost key brought within 40 m — 40 scrap, collect it from them the next day (`Dialogue.Keys`, `KeyOrders`, saved); scenario `vehicle.spare_keys`. Companions taking a fleet car by its key is still open): a key blank + the original at a workbench cuts a copy (companions can take a fleet
  car); town mechanics sell a key for a found car whose key is lost, for scrap and a day's wait.
- [x] **Walkie-talkie orders** (2026-10-09: with `tool_radio` in the pack, companions carrying one hear RADIO THE CREW: COME / HOLD / BRING A CAR from any context menu at any distance (`Companions.RadioOrder`, `OnRadio`, `CarFor`); no radio on their side = static; scenario `npc.radio_orders`. Open: a dedicated key on the held radio): a companion carrying a handheld radio takes orders from any distance (come, hold, bring
  the car) through the radio keys — the radio as the tool that keeps a crew together.
- [x] **Rumours reveal givers** (2026-10-05: `WastelandGame.GiverRumour` in town talk, `HeardOf` pinned and saved; scenario `talk.giver_rumours`): unmet story givers beyond the nearest three are heard of in town talk (`Dialogue`
  rumours: "THE RELAY WOMAN OUT EAST NEEDS A HAND") and only then pinned — the journal grows by talking to people.
- [x] **Recovery arc for a broken leg** (2026-10-09: `WastelandGame.RecoveryPace` — walked on ×0.5, crutch ×1, seated / driving ×1.5; nights in a bed knit wounds (`HealWhileAsleep`, by comfort); a broken left leg (`LeftLegBroken`) takes the clutch (`HasClutchFoot`) until severity < 0.3; scenario `body.leg_recovery`. Open: clinic bed for the player): a splinted fracture heals over days; the crutch, rest in a bed and the clinic
  speed it; until then driving a manual is out (clutch foot) — the injury becomes a few days of play, not a number.

## Scheduled update (2026-10-05)
Done this pass (ticked above): wreck scavengers sell what they stripped, wrecks age with the season (winter tanks
frozen and kept), convoy drivers fight their own engine fires, town talk tells of story givers beyond the three nearest
strangers, the trip kit under the LOOT panel. Scenarios in `Acceptance/Blocks/ScheduledScenarios1005.cs`.

### Suggestions (tie the experience together)
- [x] **The kit pays off on the road** (2026-10-06: `TripKit.Alarm/Prompt` — engine fire, blowout and a bleeding wound name the pack or the compartment holding the extinguisher / jack / dressings, or that none came along; scenario `ui.kit_alarm`): when an emergency starts (engine fire, flat, a bleeding wound) the prompt names
  where the remedy is ("EXTINGUISHER IN THE TRUNK - [LOOT]", "JACK UNDER THE SEAT") or that it was left at home — the
  trip-kit checklist turns into a decision the player feels.
- [x] **Companions with a bottle** (2026-10-06, the fire half: `Companions.FireDrill` — the nearest companion within 30 m takes their own bottle or the one in the burning fleet car's compartment, climbs out, beats it and puts it back; scenario `npc.companion_fire`. The medic half is still open): a companion riding along with an extinguisher in their pack climbs out and beats the
  player's engine fire (the same `Npc.FightFire`), and a companion medic uses the first-aid kit from the car.
- [x] **Scavengers haul to market** (2026-10-06: `Trade.HaulToMarket` / `MarketHauls` — on the nearest town's salvage stalls from the next day, marked OFF A WRECK, under the usual price, bought off the haul; saved with the hauls; scenario `towns.haul_market`): an unbought haul leaves with the scavenger and turns up in the nearest town's
  salvage stock a day later (named "OFF A WRECK ON THE ROAD") — parts travel through the economy instead of vanishing.
- [x] **Frozen fuel thaws** (2026-10-06: `VehicleSystems.tankIced` on winter wrecks — no siphon, no start until warmed by fire heat, a torch on the tank or air above freezing; saved `VehicleSave.iced`; scenario `vehicle.frozen_tank`): a winter wreck's tank can't be siphoned until thawed (a gas torch, a fire beside it or
  spring) — the season shows up at the can, not only in the share.
- [x] **Rumours need trust** (2026-10-06: below disposition 20 and without a friendly faction the teller gives a direction only — `HeardVague`, a dotted search circle off the real spot on the map; a trusted teller sharpens it to a pin; saved; scenario `talk.vague_rumours`): town talk tells of givers only to someone the town likes (faction rank / disposition),
  strangers get vaguer directions ("SOMEWHERE EAST") that pin a search circle, not a point.
- [x] **Fire spreads to the convoy** (2026-10-06: `VehicleSystems.SpreadFire` heats vehicles within 3 m of the flames — nose to tail a car catches in ~15 s; `Convoy.ClearOfFire` drives the cars within 9 m of a burning one out of reach in a halted column; scenario `vehicle.fire_spread`): a burning car parked in formation heats its neighbours (`VehicleSystems.Heat` from
  `Fire` within 3 m), so a molotov on a halted convoy can chain — and drivers pull their cars apart first.

## Scheduled update (2026-10-06)
Done this pass (ticked above): frozen winter-wreck tanks that must be thawed, strangers' rumours as search circles
(trusted tellers give the point), engine fires spreading to parked neighbours and halted columns pulling apart, the
trip kit named in emergencies, companions beating fleet-car fires with the car's own bottle, unbought scavenger hauls
carried to the nearest town's salvage stalls. A new game now also clears the trade hauls (statics outlived the scene
reload). Scenarios in `Acceptance/Blocks/ScheduledScenarios1006.cs`.

### Suggestions (tie the experience together)
- [x] **A fire is a scene, not a number** (*Done (2026-10-13):* `WastelandGame.FireScene` — a vehicle burning inside a settlement draws up to 2 residents within 60 m with buckets/bottles (`Npc.FightFire`, 6 bursts); when it ends the town remembers: standing by within 14 m while it was saved = firefighters' disposition +6, `Helped`, town faction +2; a fire traced to the player's own molotov (`Molotov.byPlayer` → `NoteFireBomb`, within 6 m / 60 s) = −20 disposition, faction −6; raiders' fire bombs and neighbours' saves make `TownNews` (`LastFireScene`)): a burning car in town draws residents with buckets and bottles (the same
  `Npc.FightFire`), the town remembers who saved it (disposition) and who started it (molotov source) — fire becomes a
  social event.
- [x] **Companion medic** (2026-10-07: `Companions.MedicDrill` + `Npc.Tend` — 6 s after the last hit, with no companion fighting, the nearest one with a first-aid kit (their pack or a fleet car compartment within 20 m) or bandages/splints walks over and treats bleeding wounds and fractures; scenario `npc.companion_medic`): a companion with a first-aid kit (their pack or the car's glovebox) dresses the player's
  bleeding wound when the fight is over, and splints a broken leg — the other half of "companions with a bottle".
- [x] **Search circles shrink by asking** (2026-10-07: each new untrusted teller halves the circle (`VagueAsks`, saved as "key|n"; the same teller twice doesn't count) down to 60 m; binoculars from inside the circle, facing the camp within 260 m (further from higher ground), pin it (`SpotCamp`); scenario `talk.search_shrink`): each further rumour about a vague giver (even untrusted) halves the circle
  around the real place; binoculars from a rise inside the circle reveal the camp — finding people becomes a small
  exploration loop.
- [x] **Market memory of the road** (2026-10-07: `Trade.HaulNote` — "OFF A WRECK A SHORT DRIVE NORTH" from where the haul was stripped (`MarketHaulFrom`, saved); car breakers on a raid unbolt a part every 4th blow (`BreakerSteals`), it reaches the nearest salvage stall next day marked STOLEN FROM YOU at half price (`Trade.Stolen`, saved); scenario `towns.haul_memory`): salvage stalls that sell a haul OFF A WRECK name where it came from; buying back
  your own stolen part (a part id once mounted on a fleet car) costs half — the economy remembers the player.
- [x] **Winter starts** (2026-10-07, the home half: piece `block_heater` (`BlockHeater`, 400 W while a cold vehicle stands within 5 m) keeps coolant from freezing, holds the engine near 50 °C and thaws an iced tank in about a minute (`VehicleSystems.KeepWarm`); scenario `vehicle.block_heater`. The fuel-burning trip heater kit is still open): iced tanks and frozen coolant both block a start; a block heater piece at home (power) and a
  heater kit for the trip (burns fuel) keep a fleet car ready on cold mornings — winter planning becomes a garage job.
- [x] **Burned-out wrecks as landmarks** (2026-10-07: `VehicleBurn.Char` → `NoteBurnedOut`: a "BURNED-OUT <CAR>" map pin and journal line for a week, scavengers skip it (`BurnedNear`), saved `SaveData.burnedOut`; scenario `map.burned_landmark`): a charred car left on a road becomes a map landmark ("BURNED-OUT COUPE")
  and a scavenger-free wreck for a week (nobody wants it) — consequences of fights stay visible on the map.

## Scheduled update (2026-10-07)
Done this pass (ticked above): search circles that shrink with each new teller and binoculars that find the camp,
salvage stalls that say where a haul was stripped and sell the player's own stolen parts back at half (car breakers
now walk off with parts), burned-out wrecks as week-long landmarks nobody scavenges, the companion medic, spare keys
cut at a workbench and keys for lost ones ordered from town mechanics, the engine block heater. Scenarios in
`Acceptance/Blocks/ScheduledScenarios1007.cs` — all six pass in a Linux player build, plus the 2026-10-04..06 scheduled
scenarios, vehicle.burnout, vehicle.keys, body.crutch and story.first_hour. The runs were headless (`-batchmode -nographics`):
the GPU was nearly full from other processes and the windowed player hung during the new-game load (a build from
2026-10-04 hung the same way), so this pass has no screenshots.

### Suggestions (tie the experience together)
- [x] **A trip heater for the road** (2026-10-08: item `use_trip_heater` (workbench; parts stalls in winter; on the TRIP KIT list in the cold): beside a cold vehicle it burns a litre from a fuel can in the pack (else the tank unless iced) and keeps the engine warm for 75 s like a block heater — coolant thawed, engine warmed, an iced tank thawing (`StartTripHeater`, `TripHeating`); scenario `vehicle.trip_heater`): a fuel-burning engine heater kit carried in the trunk (the other half of winter
  starts) — ten minutes beside a frozen car and a litre of fuel, so a winter trip needs fuel planned for the morning.
- [x] **Companions drive on their own key** (*Done (2026-10-13):* `Companions.Keys` — a companion takes a fleet car only when its key is in it / the glovebox or a key for it is in their pack (`CanStart`, gates BRING A CAR); new radio order DRIVE A CAR TO MY WAYPOINT (`Order.ToWaypoint`): a driving companion, or one with a startable car within 40 m, drives it along the roads to the waypoint (`AiDriver.DriveTo`), parks, climbs out and holds beside it (`Deliveries`, `LastDelivered`)): a companion handed a spare key (`misc_key_blank` copy) can take a fleet
  car without the player — fetch the second car from home, follow in convoy, bring the car to the player's waypoint.
- [x] **Stolen parts leave a trail** (2026-10-08: the car breaker's gang is kept with the stolen part (`Trade.StolenBy`, saved as a `?` line); the salvage vendor with it on the stall names the gang if they trust the player (disposition ≥ 20), else [CHA 6], one try a day (`Dialogue.StolenTrail`), and the waypoint goes on that gang's nearest raider convoy (`FollowStolenTrail`); scenario `towns.stolen_trail`. Taking it back off the convoy is still open): a part STOLEN FROM YOU names the raid in the stall note and the seller's gang;
  asking the vendor who sold it (CHA) points at the car breakers' camp — buying back or taking back.
- [x] **Burned-out wrecks tell what happened** (2026-10-08: `BurnedOut.whose` (YOUR / A TRADER'S / THE <GANG>'S, saved); the nearest town within 1.5 km posts it (drinks to raiders — its standing +2 when the player was within 120 m —, mourns a trader, talks about the player's); walking up within 9 m tells the story once (`BurnedStory`); scenario `map.burned_story`): walking up to a burned landmark gives one line of the story (whose
  car, which gang, how long ago) from the town news; the town nearest mourns or cheers it (faction shift).
- [x] **Patched up, not healed** (2026-10-08: `Injury.rough` — companion dressings soil after 6 min instead of 10 and a rough splint knits at 0.8, unless the companion was a nurse / medic (`Companions.Patches`; preferred by the medic drill); the player's own care, a first-aid kit, clinic, surgery or medical bay redoes them; scenario `npc.rough_dressing`): a companion's dressing is rougher than the player's own care (bandage soils sooner,
  splint at 0.8 effect) unless the companion has the Survival trade — who rides along starts to matter for skills.
- [x] **Finding people by sight** (2026-10-08, wrecks: binoculars pin unfound news wrecks in reach and in view (`SpotWrecks`, shared `InSight`); scenario `map.binocular_wrecks`. The LOOK FOR journal list, caches and raider camps are still open): the binocular spotting works for anything heard of — wrecks in the news, rumoured
  caches, raider camps — a "LOOK FOR" list in the journal fed by rumours and ticked off through the glasses.

## Scheduled update (2026-10-08)
Done this pass (ticked above): the trip heater for winter mornings away from power, the gang behind a stolen part named
by the salvager (waypoint on their convoy), burned-out wrecks that tell whose they were and how the nearest town took
it, rough companion dressings (nurses and medics excepted), binoculars that find news wrecks. Scenarios in
`Acceptance/Blocks/ScheduledScenarios1008.cs` — all five pass in a headless Linux player build (`-batchmode -nographics`),
with the 10-07 regressions (towns.haul_memory, map.burned_landmark, npc.companion_medic, vehicle.block_heater), ui.trip_kit,
vehicle.keys, body.crutch and story.first_hour. Headless, so the screenshots are blank.

### Suggestions (tie the experience together)
- [x] **Taking it back** (2026-10-12: until market day a stolen part rides with the gang (`Trade.InTransit/CarriedBy`); their raider boss killed drops it beside the body and it is off the market (`WastelandGame.ReclaimFromBoss`, `Trade.Recover`); the salvager's trail says whether they still carry it; scenario `towns.take_back`. Kept in the boss's body, not yet the boss car's compartment): a part `Trade.StolenBy` a gang rides in their boss car's compartment until it reaches the
  stall — ambush the convoy on the road named by the salvager and it is in the loot, free.
- [x] **Companions learn the knack** (2026-10-09: `NpcSave.tended` counts their dressings, the 4th (`Companions.KnackAfter`) makes them a proper medic; FIELD FIRST AID (`book_first_aid`, loot / shops) handed over in their talk sets `NpcSave.StudiedWounds`; scenario `npc.companion_knack`): a companion who has dressed wounds a few times (or read `book_first_aid` given to
  them) stops being rough — riding with someone grows them, not only the player.
- [x] **Morning routine for the column** (2026-10-09: 05:30–08:30 below 5 °C, once a day, a companion with `use_trip_heater` in their pack within 60 m warms the stopped fleet car nearest the player from a litre of its tank (`Companions.MorningRoutine` / `LightHeaterNow`); scenario `npc.morning_heater`): at dawn in winter a companion with a trip heater lights it under the fleet
  car that will be driven first, so the cold start is waiting when the player walks out.
- [x] **Survivors of a burned trader** (2026-10-12: trader crew within 40 m of a trader car that burns out become hitchers (`WastelandGame.Hitchers`, `Npc.hitchTown`) walking toward the nearest town; stop beside them and they climb into the passenger seat (waypoint to town); stopped in town they get out — town and Fuel Guild standing, disposition, a hot meal, town news; scenario `npc.hitcher`. Not saved): the driver who got clear walks the road toward town and asks for a lift
  (passenger seat); delivered, the town that mourned the car remembers it (standing, a free meal at the stall).
- [x] **Cold-start dashboard** (2026-10-09: COLD lamp (`VehicleSystems.ColdStart`, pixel + HD dashboards, 10th HD lamp); diesel glow plugs warm while someone sits in (`GlowFor`: 3 s at 0 °C, longer in frost), the lamp blinks, cranking before it goes out is held back and a cold diesel without glow is a long shot (−0.3); block / trip heater clears it; scenario `vehicle.cold_start_lamp`): a COLD lamp and, on diesels, a glow-plug wait before cranking; the trip heater or
  block heater clears it — the winter morning reads from the gauges, not only the toast.
- [x] **LOOK FOR list** (2026-10-12: journal section `- LOOK FOR -` (`WastelandGame.LookFor`): story camps known as search circles, news wrecks within reach nobody marked (150 m circle), gangs still carrying the player's parts; ENTER sets a waypoint; found items tick off as `[X]` (`LookTicked`, this session); scenario `ui.look_for`. Rumoured caches still open): rumours about caches, raider camps and missing people go to a journal list the binoculars
  tick off (`InSight` already shared by camps and wrecks).
- [x] **Crew check-ins on the radio** (2026-10-12: companions on WAIT / GUARD with a `tool_radio`, out of earshot (40 m), report once at dusk 19:00–21:30 ("ALL QUIET AT HOME", hurt, guard up) and shout at once when a hostile raider column comes within 90 m of the post (waypoint on them) — `Companions.CheckIns/CheckInNow/RaidAlarm` over `SendHomeCall`; scenario `npc.crew_checkin`): companions holding a post (WAIT / GUARD) call in at dusk on the home frequency
  ("ALL QUIET AT THE YARD") and shout when raiders show up — the radio orders become a two-way crew line.
- [x] **Glow-plug relay click** (2026-10-12: the relay clicks in when the plugs start and out when they are hot (`VehicleSystems.GlowClicks`); `FrostDrag` (frost-thick oil, 0 °C → −25 °C on a cold engine) slows the starter and deepens each compression in `EngineSynth` (`frost`); scenario `vehicle.glow_relay`): a dash relay clicks when the plugs are hot and the starter turns heavier in the frost
  (`EngineSynth` crank load from `Temperature`) — the cold start heard, not only seen.
- [x] **A medic in the crew treats others** (2026-10-12: with the player whole and things quiet, a companion with the knack and supplies walks to another hurt companion or a wounded townsperson within 30 m (`Companions.TendOthers`, `Npc.tendWho`): a kit mends 50, a bandage 25; townsfolk +10 disposition, helped, their faction +2, reputation +1 (`CompanionTreatOther`); scenario `npc.medic_others`): a companion with the knack patches other companions and hurt townsfolk
  (disposition, faction standing) — the knack as a social skill.
- [x] **Convalescence at home** (2026-10-13: by day, a broken leg, inside an own claim, the bed offers LIE UP (`WastelandGame.LieUp`): 4 hours pass, the break rests × 2.2 (× 1.15 with a placed radio on, its station named), the open or first unread book reads at 1.5×, once a day a neighbour from the nearest non-hostile town brings food; scenario `body.lie_up`): a broken leg at home rests in the bed with books and the radio (reading speed bonus,
  radio shows), visitors from town bring food — downtime as play.

## Scheduled update (2026-10-12)
Done this pass (ticked above): the crew medic treats other companions and hurt townsfolk, companions holding a post
check in on the home frequency at dusk and shout when raiders come, the glow-plug relay clicks and the starter labours
in a frost, stolen parts can be taken back off a raider boss before market day, the journal's LOOK FOR list, stranded
trader crews who need a lift into town. Also committed the parked city/megacity work of 2026-10-09/10. Scenarios in
`Acceptance/Blocks/ScheduledScenarios1012.cs` — all six pass in a headless Linux player build (`tools/acceptance/run.py
--headless`, new flag), with npc.companion_medic / companion_knack / rough_dressing, vehicle.cold_start_lamp,
towns.stolen_trail, map.burned_story, map.binocular_wrecks, audio.engine, story.first_hour, city.ride and city.living.
Open, not from this pass (they fail the same on the build before it): `npc.radio_orders` (the recruited companion is
missing from `Companions.Live` right after `Recruit`), `npc.morning_heater` only after the other companion scenarios in
one run (passes alone: test isolation), `megacity.living` (stepping off the lift lands 2.2 m from the deck spot).

### Suggestions (tie the experience together)
- [x] **Stolen parts in the boss car** (2026-10-13: opening the storage of a raider boss car (cars[0] of the convoy, stopped) puts the gang's in-transit parts behind it, off the market (`OpenBossCar`, `ReturnStolen` shared with the boss's body); scenario `towns.boss_car`): the part rides in the boss car's compartment (a crate in `VehicleStorage`) as
  well as on the body — stop the car, open the trunk, and the raid didn't need a kill.
- [x] **Hitchers remember** (2026-10-13: `NpcSave.rescuedDay`: for 7 days they greet the player by name and bargain +0.1 (`Rescued`, `Trade.Bargain`); a survivor who walks into town alone is town news (`NoteWalkedIn`); scenario `npc.hitcher_remembers`): a delivered survivor greets the player by name in town next time and gives a trader
  discount for a week; a survivor left on the road is found in the news ("WALKED IN AFTER TWO DAYS").
- [x] **Medic reputation** (2026-10-13: crew treatments count per town (`MedicWord`, news once a day); from 2, a 30 % daily "bad day" call (radio, journal, LOOK FOR) open until the next night — bring the medic into town for 30 scrap, standing, reputation and practice (`RollMedicCalls`, `AnswerMedicCall`); no clinic building needed; scenario `npc.medic_word`): townsfolk treated by the crew spread it (`TownNews`), and a town with a clinic asks the
  crew medic to help on a bad day (a small contract) — the knack earns its keep.
- [x] **Check-ins carry the base** (2026-10-13: `HomeWorries` (stalled/done stations, flat batteries, water, food < 3 days, worn pieces) in the dusk report; station calls from a claim with a radio companion on post go out in their voice (`HomeVoice`, `Companions.Relay`); scenario `npc.crew_base`): the dusk report reads the home ledger (power out, food short, a station done) in
  the companion's voice, so a crew at home replaces most HOME FREQUENCY station calls.
- [x] **Rumoured caches on LOOK FOR** (2026-10-13: town talk (disposition ≥ 0) names this week's buried stash near the town (`CacheRumour`/`CacheNear`, deterministic) as a 160 m circle; binoculars pin the marker post; walking up finds a half-buried crate (loot table `cache`) and ticks it off; saved; scenario `ui.cache_rumour`): town talk names a cache (a buried stash with a loot table) as a search circle;
  binoculars from inside show a marker post — the list gets a third kind of find.
- [x] **Frost on the gauges** (2026-10-13: `VehicleSystems.OilPressure` builds in 0.4 s warm, ~5–7 s in a hard frost — the OIL lamp glows amber until it does (pixel + HD); fast idle up to +45 % below 50 °C (`EngineStats.IdleRpm`), heard through the engine synth; no needle gauge yet; scenario `vehicle.frost_gauges`): the oil-pressure needle rises slowly on a frosty start and the idle runs high until warm
  (fast idle), heard and seen — the other end of the cold start.

## Scheduled update (2026-10-13)
Done this pass (ticked above): all seven open suggestions — stolen parts in the boss car, hitchers who remember (and
the news of those left on the road), the crew medic's reputation and towns calling for them on a bad day, check-ins
that carry the base, rumoured caches on LOOK FOR, frost on the gauges (oil-pressure lamp, fast idle), lying up at home
with a broken leg. Scenarios in `Acceptance/Blocks/ScheduledScenarios1013.cs` — all seven pass in a headless Linux
player built from the worktree, with towns.take_back, npc.hitcher, npc.crew_checkin, npc.medic_others,
vehicle.glow_relay, vehicle.cold_start_lamp, base.home_ledger, ui.look_for, vehicle.drive.Sedan and body.crutch.
Not released tonight: `/` was 100 % full (1.2 GB free) and `share/` held other sessions' uncommitted, unverified work
(visual revamp, HD character polish), so the work sits on branch `nightly/1013` (the test player was built with
Library/Temp on tmpfs).

### Suggestions (tie the experience together)
- [x] **Oil-pressure needle** (2026-10-14: the sag — `VehicleSystems.OilWear` (engine damage, tired oil, low sump) pulls the pressure down at a hot idle to ~0.35 of a healthy engine's, revs bring part of it back (`OilSagAt`); the OIL lamp flickers on a worn engine at idle; scenario `vehicle.oil_sag`): a real gauge on the pixel and HD dashboards (2026-10-13: the heavy and rugged clusters
  carry an OIL dial reading `VehicleSystems.OilPressure`), with a
  sagging needle on worn engines and low oil — the gauge tells the engine's health before a fault does.
- [x] **Neighbours by name** (2026-10-14: the visitor is one of the town's residents (`ResidentOf`: the same spot profiles `NpcDirector` walks about, skipping the dead), named in the toast, met, disposition +5; next talk opens with the leg (`AskAfterLeg`, once, +2); saved; scenario `npc.neighbour_by_name`): the neighbour who visits the laid-up player is a real resident of that town (their
  `NpcProfile`), disposition +5 per visit, and asks after the leg next time they meet.
- [x] **Cache rivals** (2026-10-14: a cache heard of ≥ 3 days ago is dug up first 65 % of the time (`RivalBeatYou`, per cache): a fresh hole and spoil heap, the crate tipped out empty, footprints to a digger walking to the nearest town with the loot in a sack (LOOK FOR pin); caught on foot they drop half (more with Charisma) and run, killed they drop it all, left > 300 m behind or reaching town it is gone; scenario `ui.cache_rivals`): other scavengers hear the same rumour; a cache left a few days is found dug up with tracks
  leading off (a wanderer carrying the loot) — the circle is a race, not a chore.
- [x] **Boss car chase** (2026-10-14: the salvager naming the gang marks it wary for 3 days (`MarkWary`); seen, a wary gang still carrying the part runs (`Convoy.Run`: the boss car takes its road away from the player at 26 m/s, re-checked every 4 s, escorts chase the player); disabled or boxed in for 2.5 s the crew come out fighting (then open the trunk), 260 m ahead it got away; saved; scenario `towns.boss_car_chase`): once the gang knows the player wants the part back (after asking the salvager), the boss car
  runs instead of parking for a toll — a chase on the road named by the trail.
- [x] **Medic's kit runs dry** (2026-10-14: a town call uses the medic's first-aid kit or two bandages and the town adds two bandages to the fee (`MedicSpends`); a crew medic with no kit, bandage or splint says so once a day — on the home frequency when posted away with a radio (`UpdateMedicKit`); scenario `npc.medic_kit`): a town call uses the medic's bandages; the town pays part in medical supplies, and an
  empty kit makes the medic say so on the radio — supplies become a crew errand.
- [x] **Hitcher network** (2026-10-14: each survivor brought in calls once, 1–4 days later by day, on the home frequency (`HitcherTip`): this week's stash near their town onto LOOK FOR (60 %), else where the nearest raider gang rides; `Rescues` saved in `crewRoad`; scenario `npc.hitcher_tip`. Not yet: the cheap part): rescued survivors tip the player off once (a cache, a gang's route, a cheap part) on the
  radio before their week runs out.

## Scheduled update (2026-10-14)
Done this pass (ticked above): all six open suggestions of 2026-10-13 — the oil-pressure sag of a worn engine,
neighbours by name who ask after the leg, cache rivals (a hole, tracks, a digger to catch), the boss car chase, the
medic's kit running dry (towns pay in bandages), the hitcher network (radio tips). Scenarios in
`Acceptance/Blocks/ScheduledScenarios1014.cs` — all six pass in a headless Linux player built from the worktree, with ui.cache_rumour,
npc.medic_word, body.lie_up, towns.boss_car, npc.hitcher(_remembers), vehicle.frost_gauges, ui.look_for and towns.take_back.

### Suggestions (tie the experience together)
- [x] **Digger's word**: a digger let go remembers being caught — they turn up in the town they walked to and tell
  the stall (prices +10 % for a few days), or, spared with their half, tip the player off about the next stash.
- [x] **Chase on the radio**: a running boss car calls its gang (raider convoys nearby converge on the road) — a long
  chase brings the rest of the gang; a short one doesn't.
- [x] **Neighbour's errand**: the neighbour who visited asks a small favour later (water for their garden, a part for
  their pump) — the visit opens a thread instead of ending at a meal.
- [x] **Medic restock at the stall**: a crew medic standing in town with an empty kit buys bandages at the stall out
  of the crew's scrap (asks first on the radio) — the errand runs itself once the player trusts them.
- [x] **Oil gauge on the pixel dash**: a small OIL bar beside the temperature bar on the pixel cluster, so the sag is
  seen in every view, not only on the heavy/rugged HD dials.
- [x] **A tip's cheap part**: the hitcher network's third kind — a stall in their town holds one part for the player
  at half price until the week is out.

## Scheduled update (2026-10-15)
Done this pass (ticked above): all six open suggestions of 2026-10-14 — the digger's word (`DiggerWord`: a caught
digger sours their town's stalls +10 % for a few days and tells the town news, or, let off with their half, finds the
player in town with the next stash), the chase on the radio (`Convoy.Call`: a boss car running 20 s calls its gang;
columns within reach hurry along their road to the call and attack on sight), the neighbour's errand (water for the
garden, pump leathers, seed potatoes: asked two days after the visit, waved over in town, on LOOK FOR), the medic
restocking at the stall (asks once a day, then buys 3 bandages out of the crew's scrap when trusted), the oil-pressure
bar on both vehicle panels (lamp threshold marked), and a tip's held part (`Trade.Held`: half price at the parts stall
until the week is out).

Liveliness audit of placed pieces (sections 2, 8, 9, 18 — what stood still while in use):
- [x] **TV picture**: a playing tape shows a moving 40×30 programme on the screen (road movie, newsreader, cartoon,
  workshop show by tape) with scan roll, tracking noise and cuts; the room light follows the picture's brightness.
- [x] **Bees and flies** (`World/Swarm`): bees work in and out of the hive mouth by day (forage, rain, cold); flies
  circle a fresh latrine pit. Tiny shared-mesh specks, animated only within 28 m of the camera.
- [x] **Shower and sprinkler spray** (`World/Droplets`: pooled pixel-sized droplets on ballistic paths, since smoke
  puffs did not read at pixel resolution): the shower rose runs for 5 s (falling drops, steam, water loop) instead of
  one burst; a sprinkler's turning head throws water for 7 s after it waters.
- [x] **The piece in work** (`Building/StationWork`): hand-work stations (workbench, garage, gunsmith, sewing, leather,
  forge, machine shop, loom, spinning) show the item being made at the output point, growing with the job, turned now
  and then, nudged on each tool tap.
- [x] `tools/compile_check.py` removes its temporary DLLs (643 dirs ≈ 5 GB had piled up on the tmpfs).

Scenarios in `Acceptance/Blocks/ScheduledScenarios1015.cs` (trade.digger_word, raid.chase_radio, npc.neighbour_errand,
npc.medic_restock, trade.held_part, ui.oil_pressure_bar, living.tv_picture, living.pieces).

### Suggestions (tie the experience together)
- [x] **Sprinkler arc** (2026-10-16): the cause was the integrator — one semi-implicit Euler step per frame took the
  whole climb off at a low frame rate (at 0.3 s a 3 m/s drop rose 2 cm). `Droplets.Step` now integrates in ≤ 1/120 s
  steps with the exact ballistic term; the jet rises ~1.1 m and carries ~3.5 m (to `reach`), one drop in four breaks up
  at the nozzle and falls close in, and the impact head ticks 5° every 0.11 s across ±135° and springs back (click).
- [ ] **Moving parts in the HD models**: the spinning wheel, loom beater, saw blades and sprinkler head have no named
  movers (`Wheel`, `Piston`, `Blade`), so `PieceMotion` cannot turn them — add movers in `tools/blender/hd/furniture`
  and matching voxel cuts, then re-render.
- [x] **Sawdust and shavings** (2026-10-16, `Building/Sawdust`): dust drifts off the blade while a cut runs (slow,
  drag-bound droplets; the mill throws more and further); each finished job leaves its share of the wood (hand saw
  30 %, mill 10 %) as a growing cone on the floor in front of the cut; [E] sweeps whole logs' worth up as wood.
- [x] **Honey bees on the beds** (2026-10-16): every flowering bed within a hive's 14 m gets foragers
  (`Swarm.Kind.Forage`: hop flower to flower, hover, jink), as busy as the busiest hive in reach (`Beehive.BedBees`).
- [x] **Flies follow rot** (2026-10-16): carcasses past their first hours (thicker with age, fewer at night / in rain,
  none in the cold), rotten food on the ground, and a player below 30 hygiene (`PlayerFlies`; a swarm that follows them)
  draw flies. Food left out on the ground now spoils where it lies (×1.5, ×3 above 25 °C, host only).
- [x] **What's on tonight** (2026-10-16): each tape its own programme (`TvSet.ShowFor`): driving school = road movie,
  survival = a night camp, karate = a dojo bout (slow-motion replays), demolition = the derby, salesman = the seminar
  with a climbing chart, new tapes GARAGE HOUR (workshop show) and GROW YOUR OWN (time-lapse garden). Anyone idling
  within 12 m of a playing set (`Npc.Tv`) takes a free seat facing it or stands to watch, and gets up when it stops.
- [x] **The favour returned** (2026-10-16): a neighbour whose errand was done (town within 3 km of the claim) radios
  the home frequency while the player is away — scouts seen a few hours before a raid (waypoint home), a raider column
  passing (once a day) — and turns out with their neighbours when a raid is fought in the player's absence (+3 defence
  each, at most two; named in the report).

## Scheduled update (2026-10-16)
Done this pass (ticked above): six of the seven open suggestions of 2026-10-15 — the sprinkler arc (a real bug in the
droplet integrator, now exact at any frame rate; impact-head ticking), sawdust at the saws, bees on the flowering beds,
flies following rot (with food that spoils in the open), a programme per tape plus people who watch the TV, and the
neighbour's watch over the base. Not done: movers in the HD furniture models (needs a Blender re-render).

Scenarios in `Acceptance/Blocks/ScheduledScenarios1016.cs` (living.sprinkler_arc, living.sawdust, living.bed_bees,
living.flies_rot, living.tv_programmes, npc.neighbour_watch).

### Suggestions (tie the experience together)
- [x] **Wet ground under the sprinkler** (2026-10-17): each spray pours its water (`Sprinkler.WetGround`, 6 L) into
  `Spills` at ten points across the ±135° fan between 1 m and the reach — the soil soaks it and shows darker in patches
  where the jet lands, paving gets a film, it dries by evaporation; a handful of replicated pours by the host, not every drop.
- [x] **Sawdust uses** (2026-10-17): [T] at a pile bags it (`use_sawdust`, SACK OF SAWDUST, half a log each). A sack in
  the pack goes a scoop at a time down the latrine after each use (six scoops a sack: a covered pit draws 15 % of the flies
  and composts ×1.5 for a day), spreads as stable bedding when the manger is filled (a pale floor in the stalls that browns
  over three days; dung comes out half again with it), and, used from the hotbar, soaks up a spill (4 L of fuel, oil or
  water within 1.6 m). Tinder for the stove was swapped for the spill: stoves have no lighting step to feed.
- [x] **Smell and company** (2026-10-17): a first meeting with an unwashed player costs disposition by temperament (proud
  −6/−12 … friendly −1/−2, the joker laughs it off) with an aside; townsfolk passing within 2.6 m remark on it in a speech
  bubble (once a day each, one every 20 s at most); a companion says so once a day and the WASH hint shows.
- [x] **Scraps for the animals** (2026-10-17, `Animals/Animal.Scraps`): kept pigs, chickens, goats and dogs spot food on
  the ground within 9 m of home, trot over, root/peck for 3 s and eat it (pigs and dogs take rotten food); three scraps feed
  a pig for the day (chicken one, goat/dog two). Drop the kitchen waste in the yard instead of carrying it to a trough.
- [x] **Watch together** (2026-10-17): sitting within 3.5 m of a companion or acquaintance watching a tape gets a line
  about the programme (two per show, once per person, show and day); a companion warms to it (+2), the karate tape is a
  guard lesson (melee practice), the derby a dare to the rolling city's arena (journal).
- [x] **Neighbours' lamps** (2026-10-17): the watching neighbour's town shows on the map (gold LAMP LIT 16–23 h at dusk,
  WATCHING YOUR PLACE otherwise) and on the HOME ledger line; with a handheld radio in the pack, the context menu offers
  RADIO <NAME>: ALL WELL? while away — the answer reads the base (`HomeWorries`: power, water, pantry…), warns of raiders
  due, or nobody answers after 23 h.

## Scheduled update (2026-10-17)
Done this pass (ticked above): all six open suggestions of 2026-10-16 — wet ground under the sprinkler (real `Spills`
water, so it soaks, darkens and dries), sawdust in use (sacks; the sawdust toilet, stable bedding, soaking spills), kept
animals eating scraps off the ground, people noticing the smell, watching TV together, and the neighbour's lamp with
ALL WELL? on the radio. None of the new state is saved (latrine cover, bedding, remarks are per session).

Scenarios in `Acceptance/Blocks/ScheduledScenarios1017.cs` (living.sprinkler_wet, living.sawdust_uses, animals.scraps,
npc.smell, living.watch_together, npc.neighbour_lamp).

### Suggestions (tie the experience together)
- [x] **Save the chores** (2026-10-18): the latrine state appends `;coveredUntil;scoops` (old `fresh;ripe` saves still
  load), the stable's bedding rides in its manger's `Trough` state as a fifth field (`beddedAt`, restored with the straw
  showing); both mark the piece dirty so they replicate.
- [x] **Animals at the bedding** (2026-10-18, `Animals/Animal.Bedding`): after dark kept livestock within 30 m of a bedded
  stable walk in, each to its own spot in the straw (`Stable.BedSpot`), and lie down till morning; a night on fresh bedding
  heals a limp half again as fast and gives an owned animal health back (+25 % a day). A dirty stable (8+ dung in the pen,
  no fresh bedding over it) draws flies (`Stable.Filth`).
- [x] **Puddles that call birds** (2026-10-18, `AnimalDirector.Puddles`): for three hours after a spray, by day and out
  of the rain, two or three crows (the real `crow` species: hop, peck, flee, fly back) come down to the sprinkler's wet
  ground; every 30 s they pull seedlings (growth < 0.35, `GardenPlot.Peck`) on the open beds in its reach. A scarecrow in
  reach keeps them off and sends them away; a hint says so.
- [x] **Washing as a moment** (2026-10-18): washing back above the flies' line (shower, pool, tub, lake, rain) throws a
  splash of droplets off the body, the flies scatter in dark streaks, and a companion within 15 m says so (once a day,
  +1). Standing in the rain under open sky now really rinses (6 a minute, up to 70), as the WASH hint always claimed.
- [x] **Neighbour visits back** (2026-10-18): ALL WELL? answered on three different days → the neighbour promises to come
  the next afternoon; between 13 and 17.5 h with the player on foot at home they walk up from the road toward their town
  (32 m off), sit on a seat by the fire (campfire / fireplace / stove, else a seat, else beside the flag) for 90 s, then
  leave a dish on the ground and walk home (`Npc.Visit`, `StartCallIn`; disposition +5, journal). Calls saved as `q` lines.
- [x] **The pigs' own trough** (2026-10-18, piece `slop_bucket`, `Building/SlopBucket`): takes the rotten food out of the
  chests within 6 m every game hour, [E] tips the pack's in (up to 12); kept pigs, dogs and hens within 14 m of home come
  and eat from it (a scrap a visit toward their day's feed); the slop rises in the bucket and draws flies when half full.

## Scheduled update (2026-10-18)
Done this pass (ticked above): all six open suggestions of 2026-10-17. Also finished the 2026-10-17 pass, which had been
built but not tested or released: three of its checks were over-strict (the sprinkler water was measured after it had
spread thin, the pig's eaten scraps before the end-of-frame destroy, the smell remark after the game's own 1 Hz look had
already made it) and are fixed; both days ship in one nightly.

Scenarios in `Acceptance/Blocks/ScheduledScenarios1018.cs` (living.chores_saved, animals.bedding, animals.puddle_birds,
living.slop_bucket, npc.wash_moment, npc.neighbour_calls_in).

### Suggestions (tie the experience together)
- [x] **Hens roost, dogs keep the door** (2026-10-23, `Animals/Animal.Roost`): after dark kept hens walk into the nearest
  stable and hop up onto a roost bar along its back wall (`Stable.RoostSpot`, the bar appears with the first hen), down at
  first light; a kept guard dog lies across the stable mouth (`Stable.DoorSpot`) facing out and barks at anyone hostile
  or a prowling predator within 30 m (toast, attention ping); a raid that wakes you names the dog.
- [x] **The visit goes both ways** (2026-10-23): the call-in's goodbye is an invitation (`InviteToSupper`); from 17 to
  21.5 h the neighbour waves the player over in town and the context menu on them offers SIT DOWN TO SUPPER (hunger +45,
  WELL FED 5 h, a giver or cache rumour over the plates, disposition +4; every 3 days); a map pin SUPPER AT …'S. Saved as
  `v` lines.
- [x] **Crows have a memory** (2026-10-23, `Scarecrow`): it frightens crows for 4 days after it is put up, moved (>0.5 m)
  or dressed ([E] with a garment from the pack: the garment hangs on it in its colour, flapping in the wind); then they
  come back to the beds and the puddles, and a toast says so. State saved per piece.
- [x] **Slop bucket smell and spoil** (2026-10-23): half full in the warmth it sours over ~10 h (faster in the sun; greyer
  slop, twice the flies; the pigs still eat it), rain fills it with up to 8 L (watery slop, spills over when brimming),
  [T] tips the water out as a dirty puddle (`Spills`). State `scraps;sour;water` (old counts load).
- [x] **Wash days** (2026-10-23, piece `clothes_line`, `Building/ClothesLine`): [E] with water within 10 m (10 L from a pool /
  drum / tank, or a lake) washes what you wear (each garment's wear −5 %, less in dirty water) and hangs it out — cloth
  panels in the garments' colours flapping in the wind, dripping while sodden — drying in ~3 h by sun, wind and warmth
  (slower at night, rain wets it again); taken in dry = FRESH CLOTHES (+25 hygiene, drier), damp = cold and clammy.
- [x] **Morning chores list** (2026-10-23, `ChoresLine`): a CHORES line per claim on the HOME ledger (no / trodden bedding,
  dung to shovel, trough feed or water low, slop bucket empty / souring / rained in, wash dry on the line, a stale
  scarecrow; ALL DONE), ENTER sets a waypoint to the first.

## Scheduled update (2026-10-23)
Done this pass (ticked above): all six open suggestions of 2026-10-18. Also committed the 2026-10-18 pass (built and
tested, never released) and ships it in this nightly.

Remodel audit of roadmap sections 1–5 (tools, furniture, clothing, attachments, crafting) for pieces that worked only as
a number or a toast:
- [x] **Bookshelf** shows what it holds: a spine per stored book (colour, width, height by title, gilt band), tapes as black
  cassettes with labels, filled from the top shelf down; the baked shelf is bare (voxel look; the HD model keeps its own).
- [x] **Dining table** lays the meal: a plate at the place nearest the eater, the food heaped on it by kind, steaming when
  cooked, eaten down to crumbs; the plate stays 3 h or until [E] clears the table.
- [x] **Geiger counter** held out in front (`hold_flash`), a dial needle on a log scale that kicks with each click, a red
  count lamp on the case.
- [x] **Snorkel** wading: a bow wave round the pipe at the water line by speed, gurgle (`station_bubble`) and spray at the
  mouth when the water is within 30 / 12 cm, a toast with the margin, drips from the pipe for 20 s after the crossing.
- [ ] Still thin from the audit: mirror (no facing pose, no clipping sound/puff when hair changes), binoculars (no
  raise-to-eyes pose, no lens mask or glint), crowbar on doors (no sparks, the door doesn't jolt open).

Scenarios in `Acceptance/Blocks/ScheduledScenarios1023.cs` (animals.roost_door, garden.scarecrow_memory, living.slop_sour,
living.wash_day, npc.supper, ui.chores_line, living.shelf_table_dial).

### Suggestions (tie the experience together)
- [ ] **Eggs in the straw**: roosting hens lay by morning in the stable (a nest box piece, eggs as food), more on fresh
  bedding, fewer with the dog barking all night.
- [ ] **Bring something to supper**: a dish or a bottle brought to the neighbour's table warms them more and loosens a
  better rumour; going empty-handed every time cools them.
- [ ] **The wash in the wind**: a storm tears a garment off the line (found later downwind), a dust storm dirties it.
- [ ] **Dressed scarecrows in town**: village gardens get scarecrows wearing the town's cast-offs; a raider gang's colours on
  one is a warning sign.
- [ ] **The dog's name and its bed**: name the yard dog at the stable door (a word with the crew), a dog blanket piece it
  prefers, it greets the player home.
- [ ] **Mirror, binoculars, crowbar** (from the audit): a facing pose and a clipping puff at the mirror; binoculars raised to
  the eyes with a lens mask and a glint raiders can see; the crowbar levering a door with sparks and a jolt.

## User requests (2026-10-13: GTA-style town driving)
- [x] **Drive straight through walkable areas and roundabouts** (2026-10-13: kerbs are mountable — `TownPlan.Kerb` 0.10 m eased up over `KerbRamp` 0.45 m from the carriageway edge (street and ring sidewalks, driveways, pedestrian-street ends at their junction boxes), roundabout islands ramped up to the sidewalk level instead of a 0.18 m step, the off-plan highway edge eased; `TownPlanVersion` 2. `VehicleDriver` tyre envelope: each wheel also samples ±0.55 r along its heading less the arc drop, so it climbs a step on its tread instead of the damper being slammed. Street furniture gives way (`World/Breakaway` look-ahead one physics step ahead of every moving vehicle's body box, called from `VehicleDamage.FixedUpdate`; contacts it missed are handed over from `VehicleDamage.Handle`): `StreetFurniture` Loose — benches, bins, bollards, hydrants, phone booths, drums torn off and flung (rigid bodies, remembered through `DestructibleVoxels.Moved`); Plough — planters, hedges, bushes, park trees, island statues/fountains, bus shelters carved through car-wide (carve broadcast), the car keeps ≥ 55-80 % of its speed. Pedestrians jump clear of cars on their line (`Npc.Dodge`). Scenario `towns.drive_through`. Open: kerb faces in the HD terrain shader still read as vertical (the band is drawn, the ramp is not shaded); hydrants could gush; player-built street lamps / power poles (Placeables) don't snap yet.)
- [x] **Light and electric poles break realistically** (2026-10-13: `World/BreakablePole` on town street lamps (`TownLights`, by piece: pole / gas / cobra / solar / floodlight mast), voxel traffic lights, stop signs and old city streetlights, and rail telegraph poles (now split out of the track tiles: each its own body with its wire span, `RailKit.Pole`). Below its snap energy a hit bends it (leans away, the next hit needs less) and the car stops as on a post; above it the foot snaps, the post falls as a rigid body (120-650 kg, capsule, pivoting over the foot, away along the hit and off to the struck side), the car keeps the momentum the post's effective mass at the hit height and the snap energy don't take (≈ 85 % at 50 km/h into a highway lamp) with a moderate crunch (`VehicleDamage.Crash`, no wall stop). Lamps go dark with glass shards, live poles arc (sparks, blue flash, sizzle), wires come down (both spans), a thud and dust when it lands; at rest it is remembered (`BreakablePole.Fallen`, saved as `f:` lines in the lights block) and lies kinematic until a vehicle shoves it. Network: protocol 7 `Msg.Knock` (the peer simulating the car sends, every peer breaks its copy; unloaded pieces fall when they stream in). Open: fallen poles never despawn (cleared with a new game); no wire physics (spans just vanish).)

## User requests (2026-10-09, night: generated cities)
- [~] **Replace the tracked cities with the Blender-generated ones** (2026-10-09: `World/RollingCity/TrackedCity*.cs` ports modelbench/lib/citygen.py line for line through `CityKit` (box/span/beam/tube/lathe/ring/loft/wing/cable/ladder/railing, generator axes → deck-local game axes, per-vertex weathered `CM` palette, glow mesh on an unlit material, collision mesh of solid spans); both circuits carry a profile (Granite Falls 104 × 54 m, Saint Rust 120 × 58 m; `CityRoute.tracked` sets deck size, deck height 26.7 m, pose chord, bed and pier gap); crawler units with trucks, trestles, slewing rings, chain drives and four belt phases; walkable panel blocks (storey modules shared per floor: corridor, stair core, flats with furniture, balconies, AC, dishes, laundry; shops on the ground floor); halls, plants, banded chimneys, crane, pipe trunk with cabin stacks, pods, pilot bridge, railings with collision walls, lamps served by `CityLights`; high docks with lattice pier, stair tower and `CityLift`; `IDeckCarrier` carries the player on lifts. Pixel mode is only the shader over the same geometry. Scenario `city.ride` passes. Open: LOD for far views, smoke per stack size, HD look check. *Audit 2026-10-13:* the NEW GAME START names no longer name the retired voxel downtowns (ABOARD THE ROLLING CITY / THE RUST-WORKS CITY))
- [~] **Living aboard: purchasable quarters, build permission, shops, NPCs, walkable areas** (2026-10-09: `WastelandGame.Quarters` — every flat's door sign (`FlatDoor`) rents a week or buys outright for scrap, residents' flats and shops are taken, `SaveData.blockQuarters`; `CanBuildAt` → aboard only inside a held flat (BuildMode + free frame), pieces parent to the city (mesh colliders kept on the kinematic hull) and restore with it (`PlacedSave.city`); `Npc/CityFolk` — shopkeepers behind every ground-floor counter (food/parts/salvage/build/fuel/pack/scrap), the housing clerk at the gangway (`Dialogue.Quarters` rents the cheapest free flat and pins it), residents walking `TrackedCity.RouteIn` (stair cores) between their flat and deck spots by the clock; `Npc.aboard` skips terrain pathing. Scenario `city.living`. Open: resident schedules beyond day/night, network replication of leases. *Done (2026-10-13):* lease reminders — a rented flat warns a day before the rent runs out and once when it has (`UpdateLeaseReminders`, toast + journal HOME), held flats listed in the journal's ROLLING CITIES section)
- [x] **Megacities** (2026-10-10: `World/Megacity` + `MegaCityHost` — towers, cluster decks with rentable panel blocks, shops, clerk, residents, a gated lift per cluster from the plaza, landing pad + hangar, bridges, undercroft; graded site 1.5-2.2 km from the yard, on the world map; scenario `megacity.living`. Open: far LOD (~2.0 M triangles, always loaded), flying vehicles from the Blender set, a road to the plaza, residents riding the lifts, lease replication for clients. Designed 2026-10-09 in Blender — modelbench/lib/megacity.py: towers above a cloud sea, platform clusters of panel blocks, machinery undersides, bridges, cable webs, undercroft, landing pad + hangar; vehicles gunship / freighter / crawler / land-ship / skiffs): port through `CityKit` as a world site with tower lifts, the pad, quarters, shops and NPCs like the rolling cities.

## User requests (2026-10-09, concept art)
- [x] **Lead character look**: default new-game look = the concept art — black undercut (`HairStyle.Undercut`), amber temple implant (`Appearance.implant`), salvaged cyber arm on the left (`pros_cyber_arm`, LEFT ARM in character creation), `work_jacket` (patched rust-orange), `patched_cargo` with knee pads, `canvas_satchel`; NPC wanderers wear the new pieces too. Open: HD garments / hair for the HD pack (`garments.py`), implant glow in HD.
- [x] **Rolling city look + procedural generation** (roadmap 28 R1b).

## User requests (2026-10-08, evening)
- [x] **Parked cars float** (2026-10-09: `WastelandGame.SettleOnGround` on freeze / road wrecks / wreck spawn; scenario `vehicle.parked_settle`): a parked / sleeping car sometimes rests above the ground (settle onto the wheels'
      terrain contact before sleeping; re-ground on wake and after streaming).
- [x] **Excavator bucket looks off** (2026-10-09: `PartLibrary.ExcavatorBucket` D-shell on the pin, mouth to the cab, teeth; `machines.excavator_bucket`): bucket shape / hinge and its curl against the stick (check the segment pivots and
      the bucket voxel design against a real excavator).
- [x] **Dozer blade wider range** (2026-10-09: −34°..+22°; `machines.dozer_blade`): the blade arm lifts and drops further (raise for travel, cut below grade).
- [x] **Crash injuries too easy** (2026-10-09: `CrashHarm(dv, open)`, safe below 25 km/h belted / 18 km/h open, CRASH < 6 no wound; `vehicle.crash_harm`): car accidents injure the driver too readily — thresholds by Δv (belted occupant,
      cabin crush, airbag-less wrecks), minor bumps never injure.
- [x] **Mouse wheel scrolls menus and panels** (2026-10-09: list pages move the cursor; catalogue, container and map keep their own wheel; `ui.mouse_wheel`): every scrolling list / page (catalogue, inventory, trade, journal,
      settings, loot panel) follows the wheel.

## 28. Rolling cities `T3` — in progress
Mobilised downtowns: old city blocks bolted onto a giant deck that crawls across the wasteland on tracked bogies,
stopping at city docks (graded yards with ramps, cranes and stalls) along a fixed circuit.
- [~] **R1 — one rolling city** (2026-10-08: the circuit, docks, deck + bogies + four downtown blocks with real storeys and stairs, shacks / scaffold / bridge / tank / windmill / engine house / crane, the timetable, gangway, the player carried, parked cars strapped, driven cars grip the moving deck, stack smoke and sound, the cutaway opens its blocks; scenario `city.ride`. Open: ruts, HD look, network sync beyond the join snapshot. NPCs and stalls aboard: see Living aboard; night lights: `CityLights`. *Done (2026-10-13):* map pin and timetable — `WastelandGame.CityTimetable`: each city's live position on the world map (DOCKED / ROLLING), journal section ROLLING CITIES (AT <dock>, LEAVES IN … / BOUND FOR <dock>, ARRIVES IN …; ENTER = waypoint at the dock)): a deterministic city (deck, four tracked bogies, downtown core of pre-war
      brick/concrete blocks with post-apocalyptic attachments: scaffolds, shacks hung off the facades, catwalks and
      bridges between roofs, smokestacks, a crane, water tanks, windmills), a circuit through the world graded like
      a road, docks at the stops (ramps meet the deck edge), movement (slow, steady, stops on a timetable), riders (the
      player, NPCs and parked vehicles move with it), track ruts, sound (engines, treads, horn at departure).
- [~] **R2 — start on the city** (2026-10-08: RULES START: ABOARD THE ROLLING CITY puts the player on the deck at the first dock; the fleet and homestead stay at the yard 180 m away. Open: kit in a deck-side garage, a car on the deck): a new-game start (RULES: START ON THE ROLLING CITY) beginning aboard at a dock,
      with the homestead kit in a deck-side garage; acceptance scenario that boards, rides a leg and steps off at a dock.
- [~] **R1b — procedural city profiles** (2026-10-09: seeded Granite Falls + second industrial profile Saint Rust on its own circuit; the industrial city adds taller apartment towers, denser stacks, trussed galleries, pipe runs, condensers and connectors; 512² weathered material/triplanar detail; shared façade windows are inset into their openings for both profiles. Scenario `city.ride` now checks the second city's route and dock. Open: finish window/city screenshot acceptance, smoke per stack size, track modules visible past the deck, HD pass on the older city)
- [~] **R3 — living aboard** (*audit 2026-10-13:* residents, shopkeepers, the housing clerk and rentable / buyable flats with build permission are in — see "Living aboard" above; open: a town leader (faction), jobs aboard, the city as a settlement for news / contracts): residents, stalls and a town leader (faction), jobs aboard (stoking, repairs), the
      city as a settlement for trade/news/contracts, player rooms to rent or build (deck plots).
- [~] **R4 — more cities** (*audit 2026-10-13:* a second circuit with the industrial Saint Rust profile and the megacity are in; open: church city / raider hulk styles, cities meeting at docks, raids, breakdowns): other circuits and styles (industrial, church city, raider hulk), cities meeting at docks,
      raids on a moving city, the city breaking down (a stranded city becomes a temporary town).

## 29. Free building: beams, spans and climbing `T3` — in progress (2026-10-08, with roadmap 28)
Paving the way for a post-punk high-rise look (stacked shacks, walkways and platforms grown over old towers and the
rolling cities).
- [x] **Beams anywhere** (2026-10-09: `Building/Frame` — `beam_wood` / `beam_steel` from click to click at any angle, ≤ 8 m, snap to beam ends, Shift straight, chain on; cost per 2 m; `BuildMode.Frame`. Open: struts/cables, snapping to piece edges): a beam piece snaps by either end to any surface, piece edge or another beam's end at any
      angle (not the 90° / 0.08 m grid), length set by the second click; struts and cables as variants.
- [x] **Spans between beam ends** (2026-10-09: `span_planks/sheet/grating` over 3-6 picked ends, plane check ≤ 0.3 m, level spans are decks (`StructureGround.AddPolygonDeck`). *Audit 2026-10-13:* `StructureSupport` already runs through beams and spans (they are `Placeable`s with root renderers: their bounds chain to the ground and they collapse when what holds them is cut)): pick 3-6 beam ends → an elevated surface (planks, sheet, grating) is laid over the
      irregular polygon they enclose (walkable, drivable when strong enough, `StructureSupport` through the beams).
- [x] **Working ladders** (2026-10-09: `ladder_frame(_steel)` any length and lean, `PlayerCharacter.Climb` grabs on walking in, steps off at the top; scenario `build.frame`. *Done (2026-10-13):* NPCs use ladders — `Npc.Ladders`: a goal on a raised level (≥ 1.8 m above / below, not a hillside) with a ladder joining the levels sends the walker to its end, climbs it with the player's hand-over-hand pose and steps off (`Npc.Climbing`, `laddersClimbed`)): ladders climb (on foot input, enter/leave at the top and bottom, NPCs use them in
      `NpcPath`), any length, onto beams and spans.
- [ ] **Post-punk high rise** (*audit 2026-10-13:* collapse when beams are cut works through `StructureSupport`; open: hybrid plan library — `StructurePlans.Entry` does not keep a beam's length / a span's corners yet — and authored shacks on towers / city blocks): shacks and spans hung off existing towers and the rolling city's blocks; a structure
      plan library of hybrid pieces; collapse when beams are cut.

## Visual feel pass: the overhead look (2026-10-12)
Reference: a modern top-down GTA 1/2 (perspective camera high overhead, saturated daylight, crisp shadows, clean roads,
headlight pools at night).
- [x] **Overhead view, the new default**: `ViewMode.Overhead` (perspective, 40° FOV, 78° down) ~27 m over the walker,
      ~33 m over a parked car (by length), climbing with speed (+30 m at 120 km/h, rises fast, settles slowly), leading
      half a second of travel, lazily turning behind a driven vehicle (Z/C or RMB pause it 4 s), thinner mist veil.
      New games open in it (saves keep their view). Motion blur only in the chase / first-person views.
- [x] **Grade**: vibrance, log-space S contrast with highlight roll-off, split toning (cool shadows, warm light; at night
      blue shadows and warm lamplight), vignette; punchier per-biome saturation/contrast. Sun light capped at 58° so
      everything keeps a readable shadow from above at noon. Voxel props shade smoothly at full resolution
      (`_MadMaxSmoothLight`) beside the HD assets.
- [x] **Highways in HD terrain**: one crisp tarmac band from the signed centre-line distance (crumbling edge, tyre
      lanes, faded edge lines, straight centre dashes, dusty shoulder), procedural grain; junctions and road ends fall
      back to the cells where the interpolated distance isn't one.
- [x] **Handling**: player-only yaw assist (`VehicleDriver.yawAssist`: the body follows the steered geometry up to
      ~1 g, a released wheel settles the tail, handbrake = tyres only), quicker steering; on foot a short momentum ramp.
      Headlights throw a longer beam (visible from above). Scenarios `camera.overhead`, `vehicle.yaw_assist`, `player.momentum`.
- [x] **Machine loads you can believe**: excavator / backhoe buckets, the dozer's blade and the tipper bed show their
      load shaped by the tool itself (level in the bucket as it tilts, heaped past the rim, a windrow in front of the
      blade), lumpy, darker freshly dug soil; loads go only into tipper beds and bins (not car boots).
- [x] **The world at rest when you arrive**: wrecks and loose parts wake already on the ground; at game start everything
      near the player settles behind the loading fade (the story's overturned box trailer no longer tips in view).
- [x] **Photo ground**: the HD terrain's detail maps come from Krea-2 photos (sand ripples, pebbles, aggregate, grass
      blades), high-passed so nothing blotches or repeats from above.
- [x] **If you see it, you can have it**: TAKE APART world props, lamps, stalls, loot furniture and city fixtures with the
      right tool and skill; learned designs build from resources (SALVAGED); city parts, decks, ramps, lights as pieces.
- [x] **Moving bases at any scale**: frames + extensions, axle/track mounts, engine bay, seat, tanks, suspension; giant
      wheels and tracks; pieces add mass; decks carry vehicles; saved.
- [x] **Heavy lifters**: magnet crane truck and car crusher with grapple (bales of scrap).
- [x] **Towns that make sense**: a street plan per settlement (village / town / city densities), roundabouts, boulevards,
      ring road, sidewalks, plazas, parks, lots with yards; street furniture, flowers, litter, birds, residents on their
      rounds, gang hangouts. Open: crisp vector street markings in HD, intact apartment blocks, town streets on the map.
- [x] **HD minimap and map**: round minimap, heading-up / north-up, waypoint route, edge marker, smooth map zoom.
- [x] **HD map land**: `Game/MapLand` tiles (0.25–8 m texels, built on workers from `WorldGen.Sample`): smooth biome colours, NW hillshade, water by depth with a shoreline, contour lines, town-plan ground (streets, walks, plazas, lawns, car parks, fields) and building footprints with shadows; feeds the minimap and the map page, base map (hillshaded) as fallback and above 12 m/px.
- [x] Settings toggle for the heading follow and the yaw assist (both default on), and an overhead height slider. *Done (2026-10-13):* `GameSettings.steerAssist` (GAMEPLAY: STEERING ASSIST → `VehicleDriver.YawAssist`), `headingFollow` (MOUSE & CAMERA: OVERHEAD FOLLOWS HEADING), `overheadHeight` 60–160 % (OVERHEAD HEIGHT, multiplies the overhead camera height).
- [x] Round minimap with the health ring (the reference HUD), speed-dependent minimap zoom. *Audit 2026-10-13:* already in — `PixelHud.DrawRoundMinimap` (health arc left, stamina arc right, low-health blink; zoom 1×→2.6× by speed, 2× in the air; `LastMinimapMpp`).
- [ ] Junction fillets and kerbs on highways; road decals (oil, cracks) from the same distance field. *(audit 2026-10-13: left open — the HD terrain shader's road block is being reworked by the town-street-markings pass; do it after that lands)*

## 30. The railway `T3` — in progress (2026-10-13)
*User request:* a train network with connected, tiered stations, makeshift and armoured trains running, driving with
the right skill, points, and building / extending the network.

- [x] **Network** (`World/Rail/RailNetwork*.cs`, `WorldGen.rail`): a station per settlement at the edge of its plan —
      city TERMINAL (3 platform roads to buffer stops, yard siding with an end-loading ramp, engine shed road, water
      tower, signal box, head house with a clock tower), town STATION (2 platforms, passing loop, signal box), village
      HALT (one platform, shelter); lines as a spanning tree + loops (same landmass), junctions in the wasteland,
      water/fuel stops on long runs; A* round towns, lakes, sites, crawlerways; grades ≤ 2.5 %; cuttings and
      embankments graded into the land (features 19 ballast, 20 platform), level crossings pinned to the roads,
      bridges over rivers / low ground on piers.
- [x] **Track you can see**: ballast, sleepers, flat-bottom rails, crossing panels, girder bridges, buffer stops,
      telegraph poles with wires, mile posts; platforms with coping, warning strip, ramps, lamps, benches, name boards,
      canopies; lever stands with moving blades; crossing barriers / lamps / bell; colour-light signals.
- [x] **Trains**: makeshift Guild services on timetables (ping-pong, dwell at every station: a trader and travellers on
      the platform), armoured patrols (Rustmen war train, Remnant ironclad) whose turrets fire on their faction's
      enemies; sounds (diesel, joint clacks, rolling rumble, brake squeal, two-tone horn at crossings, platform bell);
      trains strike people on the line and shove cars; a train ahead holds a service up.
- [x] **Driving**: [F] at a cab (steal a makeshift train at a platform; take a knocked-out armoured one), W/S throttle /
      brake, Space emergency, R reverser, Q/E set the next facing points left/right, horn, J uncouple; coupling by
      buffering up gently; diesel by [G] with a can; skill RAILWAYS (book, research) — untrained locos lurch and crawl,
      the ironclad needs RAILWAYS 2.
- [x] **Building**: RAIL TRACK / SCRAP TRACK laid point to point from ends or branching off any track (new points
      with a lever stand), take up your own; buffer stop, platform section, signal, halt shelter; kits for a draisine,
      flatcar, hopper, shack wagon and a makeshift loco; derelict wagons / locos / draisines in station yards.
- [x] **Saved**: laid track (replayed in order), points, service clocks, free trains with their cars and place.
- [x] **Map**: lines and stations on the minimap and the map page; stations found on arrival (journal).
- [ ] Station buildings as destructible voxel props (now procedural meshes) and as buildable pieces.
- [ ] AI services meeting each other head-on (they pass through; only player trains hold them up).
- [ ] Derailment on over-speed in curves; wheels turning; NPC drivers visible in cabs; stationmasters.
- [ ] Network play: trains are not replicated yet (each peer runs its own timetable from the shared clock).

## Living bodies — animation revamp (2026-10-13)
Runtime side; the clip data (walk_start … car_exit_high, idle_* variants, startle / cover_head / shield_eyes, prone,
ground sit / lie, eat / drink, boarding) is authored separately into `Resources/CharacterAnims`. Every new clip name
falls back to a procedural stand-in (or an existing clip) until it lands; "_r" clips are mirrored from the left-hand
ones when not authored (`HumanClip.Mirror`).
- [x] **Attention** (`Game/Character/Attention.cs`, one per `HumanAnimator`): a prioritised look target — the player's
      aim; danger (hostiles fighting, charging / stalking animals, a car coming straight at you, fire close by,
      gunshots, blasts); sounds; points of interest (what [E] would use, passing vehicles, animals, loose items, the
      waypoint now and then); idle glances (driving: the mirrors and into the turn). Static bus `Attention.Ping` fed by
      `Sfx.Play` (loud keys only, classified once per key), `NpcDirector.Noise/Horn`, `AnimalDirector.Noise`,
      `Explosion.Blast`, `Fire.Ignite`, projectile impacts. Hold times, re-glance cooldowns, a blink on big shifts
      (`HumanFace.Blink`), no owl turns (> 100°: give up, or for danger while the player stands idle the body turns
      round). Close bangs: `startle` (flinch without the clip), blasts: `cover_head` (arms over the head stand-in).
      First person suppresses it; NPCs run it within 35 m of the focus.
- [x] **Locomotion feel** (`HumanAnimator.Feel.cs`): walk_start / walk_stop / run_stop (else a lean into the first
      step and a settle back), turning on the spot by steps (turn_l90 / turn_r90 over the legs; else stepping legs and
      footsteps) — the player turns before walking off when the new direction is > 60° away; lean into turns from
      atan(ω·v/g), uphill / downhill lean, a steadied head while moving, tired / tired_walk under 30 % stamina (else
      heavy breathing and a hunch), cold shivers, foot IK now also on the procedural path.
- [x] **Prone & crawl** (`PlayerCharacter.Low.cs`, `Controls.Act.Prone` appended, default X): go_prone / prone_idle /
      crawl / prone_up, 0.6 m capsule, 0.6 m/s, quiet scrapes, harder to spot (NPC hostiles 16 m, raider convoys
      ×0.35, animals sight ×0.3 / noise 1.5); no crawling in water over 0.4 m; standing up waits for headroom.
- [x] **Sit / lie on the ground** anywhere level (context menu SIT DOWN / LIE DOWN / GO PRONE): sit_ground_down /
      sit_ground / sit_ground_up, lie_down / lie / lie_up, rolls onto the side (lie_side) after 25 s; stamina back ×1.6 /
      ×2.2; any move gets up. Beds: lie_side while asleep.
- [x] **Eating seated**: eat / drink over the chair or ground sitting pose with the food in the hand; chewing nods after
      a bite (standing too).
- [x] **Boarding** (`WastelandGame.Boarding`, `BoardingAlign`, `PlayerCharacter.Board`): walk to the door spot on your
      own legs (round the car when on the far side), square up, car_open_door with the hinged door following the hand,
      car_enter (root placed from the clip's sampled pelvis so seat- or door-anchored clips both line up),
      car_close_door; out: door pushed open from the seat, car_exit, door pushed shut from outside. *_high for seats
      > 0.75 m up, *_r on the right. A move key, a blow or the car rolling breaks it off cleanly; never left inside
      the bodywork (door spot pushed out until a standing capsule fits).
- [x] **Idles by context**: every 6–16 s standing at ease — hot: idle_wipe_brow, cold: idle_shiver, a pack:
      idle_check_pack, else scratch / look / shift / stretch / neck / hips; never while talking, aiming or acting; NPCs
      use the same scheduler with their own seed (heat from the air temperature).
- [x] **Clip contract wired** (CLIPS.md): `HumanClip` reads rootX / rootZ / rootYaw, door, ground and events
      (`RootAt`, `RootYawAt`, `RootSpeedAt`, `TimeAtRootZ`, `DoorAt`, `GroundAt`, `EventAt`, `Crossed`); explicit `_r`,
      `startle_l` and `turn_r90` clips win over the mirror fallback. Gait stride = the dominant clip's speed × length
      (walk 1.40, run 2.81, sprint 3.48, crouch_walk 0.95, tired_walk 1.155, crawl 0.66 m, × h) — run / sprint no
      longer over-cycle. walk_start plays by the distance walked and hands over at walk u = 0; letting go of the input
      runs on to the next heel strike, then walk_stop / run_stop (or the _r mirror from the right heel) move the body by
      their rootZ; turning on the spot from standing plays turn_l90 / turn_r90 and turns the root by their rootYaw;
      lie_down / lie_up move the resting root ∓0.45 m. Boarding in the seat frame: the walk-to point is the clips'
      stand spot (car −0.60 / −0.45, high −0.90 / 0, × h), root at the seat lowered by the seat-height difference and
      shifted by the stand-spot difference × ground[f], the hinge from door[f], sounds from door_open / door_shut,
      base pose from seated / standing; car_exit opens the door itself. eat / drink run their full 2.4 / 2.2 s; bite
      starts the chewing, sip a gulp; action-clip steps sound as footsteps. Prone: the controller stays at the root, a
      probe keeps the head (1 m ahead) out of walls; crawl at the clip's 0.55 m/s.
- [ ] Network: remote avatars and NPC proxies don't replicate prone / ground rest / boarding steps / attention targets
      (peers see them standing, then seated).
- [ ] NPCs sit and lie on the ground at camps and stalls, go prone / crouch behind cover in a fight, glance at each
      other in NPC-NPC chats (Attention POI for the partner).
- [ ] Passenger and rear doors: the same open / enter / close sequence (with *_r) for `PassengerSeat` boarding and
      companions getting in.
- [ ] Prone shooting (aim from the ground with long guns), crawling under low obstacles (vehicles, fences).
- [ ] A jaw / mouth on `HumanFace` for chewing and talking; breath fog in the cold and sweat sheen in the heat.
- [ ] Save the ground rest / prone state (load stands you up today).
- [x] **Editor pass (2026-10-13)**: `anim.revamp`, `combat.guard`, `rail.network`, `rig.commission`, `vehicle.lifters`, `ui.map_land`, `towns.plan_smoke`, `player.momentum` pass in the editor; close-up captures of idle / walk / prone / crawl / ground sit / eat / lie and the full door sequence into a Sedan. Fixed on the way: boarding cancelled by trickle damage (`SinceBlow`) and by a frame-rate dependent stuck test; rail stations dropped by a summed slope score, networks split by straight-line dry checks, the crawlerway ring and the polar ice (landmass labels); test fixtures (raider pinned for the guard, far pads, on foot before the rail test).
- [ ] Eat / drink with the real food model in hand for every food id (`food_can` fell back to the kit crate in the capture).
- [ ] Walk stop: the run-to-heel + `walk_stop` takes ~0.9 s; offer a SNAPPY STOP setting or shorten for the top-down view if it feels slow.
- [x] **Climbable terrain** (2026-10-13): softer ridges, and road / river / rail / yard / crawlerway banks that widen with the cut (~20°); steep spots near the start 0.56 % → 0.16 %.

## Instrument clusters (2026-10-13)
- [x] **Data-driven clusters** (`Vehicle/Dashboard/`): `DashboardStyle` (panel size, binnacle visor or flat panel,
  Pal colours, bezel kind, backlight colour, key-on needle sweep; `GaugeDef` dial / LED bar / seven-segment digital /
  rolling drum / shift-light LEDs with ranges, red zones, end letters and captions; `LampDef`s; dummy `SwitchDef`s),
  `DashboardBuilder` (real geometry, one mesh set per style shared by every vehicle: lit housing with chrome / black /
  square screwed bezels, unlit backlit face with ticks, 5x7 numerals, red zones and glass glints, needle transforms,
  drum wheels, digit / LED meshes swapped, lamp lenses with icons tinted through a MaterialPropertyBlock), PixelVoxel
  copies or HDLit with the HD pack. Built lazily (sat in, or the focus within 30 m).
- [x] **Four generic styles** (`DashboardStyles`): classic analog (Sedan, Wagon, Pickup, Trabant, model cars: speedo +
  tacho, fuel / temp, odometer drum, amber backlight), heavy panel (Hauler, Semi, Bus, TowTruck, Wrecker, Ambulance,
  MagnetCrane, CarCrusher, boats; machines and moving bases on `heavy_slow`: speedo, tacho, oil, coolant, fuel, volts,
  air, turbo, hour meter, rocker row), sport (Interceptor, Coupe, DuneBuggy, MonsterTruck, bikes at 62 %, aircraft:
  white-faced tacho, digital speed, gear, shift lights, LED bars, needle sweep), rugged (APC, Scavenger: square gauges,
  toggles, green backlight). Pedal bikes have none. `VehicleDesign.dashboard` / `VehicleDashboard.style` override; a
  custom style named after a design is picked for it (`DashboardStyles.Custom.cs`).
- [x] **Behaviour**: speed / rpm needles damped, fuel / temperature slow and falling to E / C with the key off, cranking
  flicker, bulb check for 1.6 s on key-on, OIL + CHARGE red while the engine is off (OIL amber on low pressure, lamp
  index 5; COLD lamp 9), headlamp / 4x4 / diff-lock / fire / air lamps, backlight at night with the headlamps (sport
  always), odometer from the engine hours + the trip, tacho range from the mounted engine, units from the settings.
  Scenario `vehicle.dashboards`.
- [ ] Vehicle-specific clusters (Interceptor supercharger gauge, Bus door lamps, aircraft panels with altimeter / ASI,
  boat panels with depth sounder), turn-signal arrows once indicators exist, a saved odometer.
