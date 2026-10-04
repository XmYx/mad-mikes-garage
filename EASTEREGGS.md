# Mad Mike's Garage — Easter eggs

Design backlog, not implemented features. Read alongside [ROADMAP.md](ROADMAP.md) and [SUGGESTIONS.md](SUGGESTIONS.md). All checkboxes here are deliberately open. Numbers, timings and rewards below are starting tuning values, not claims about current behaviour.

The wasteland should occasionally feel as if somebody lived here, somebody cared, and somebody made an exceptionally bad business decision. Favour discoveries made while driving, repairing, cooking and exploring over secret input sequences. Most rewards are a laugh, a story or something to put in the garage.

## Ground rules

- Keep the campaign's serious scenes intact. Suppress comic interruptions during authored dialogue, rescue, death, medical emergencies and the finale; defer them until a calm moment.
- No required recipe, superior combat part or ending depends on an Easter egg. Cosmetic recipes use ordinary materials and have ordinary resale value.
- Clues are physical details, overheard remarks and optional notebook entries. Do not populate a new player's map with undiscovered secrets, completion percentages or spoilers.
- Timed events use world time and recur. Never require a real-world holiday, a particular save creation date or leaving the game running overnight.
- Chance chooses a stable site or a schedule, not a new reward every reload. No per-frame random rolls. Use a documented seed mixer with world seed, stable site identity and egg ID.
- Cap ambient surprises initially at one per 20 minutes of active play per player, with entry-specific cooldowns. Static scenery is exempt. Defer audible gags behind useful radio news and threat cues.
- Items obey normal pickup, weight, ownership, destruction, storage and crafting rules. The recent explicit-looting change applies: a prize appears in a reachable tray or marked object; it is not silently collected.
- Support both voxel/pixel and HD presentation. Author palette colours through `Pal`, use material-labelled voxels, and rebuild generated content. Decorative bodies must not introduce invisible collision traps.
- Let an optional WHIMSY setting reduce animated and audio surprises; static environmental storytelling can remain. Never hide useful warnings through this setting.

Effort: **S** = text/data or one small prop and a hook; **M** = a saved interaction or a small event; **L** = coordinated rendering or multi-site content. These are relative scope labels, not time estimates. **Site** means a single stable instance; **world** means once per save; **player** means personal discovery. Unless stated otherwise, scenes remain revisitable and consumable rewards pay once per site.

## The lunar running joke

### EE-01 — Mad Mike's Garage: lunar branch [L]
- [ ] **Experience:** on some clear nights, the Moon carries a tiny neon shop sign. Through binoculars it unmistakably reads **MAD MIKE'S GARAGE**. Below it, a smaller line reads **WE SERVICE ALL MAKES. EVENTUALLY.** A little OPEN lamp comes on after the main letters.
- **Existing foundation:** the roadmap already records a Moon title set with a neon sign. `TitleSequence` constructs it with `TitleArt.NeonSign()` beside `MoonArt` scenery. The new feature is seeing a corresponding sign on the in-world Moon during ordinary play; the title scene is not a gameplay lunar destination.
- **Trigger:** a seeded night in each four-world-day block has a two-world-hour candidate window. Activate only while the Moon is above the horizon, sufficiently illuminated and visible through the current cloud/sky rules. If the whole window is obscured, retain the opportunity for the next suitable night. Once active, leave it available for the rest of the window; a blink-and-miss flash is insufficient.
- **Clues:** a rare mechanic remark, “He's opened another branch. Terrible parking.” A scrap advert depicts a crescent beside a spanner. After a first sighting the notebook records the player's observation, without revealing unrelated secrets.
- **Rendering:** extend `Atmosphere`'s camera-relative Moon rendering, attached to `DayNight.MoonDirection`. Bake a small sign representation from the existing title artwork; do not instantiate the complete title set or add an enormous world collider. Clip the sign to the lunar disc, account for phase, and respect the same foreground depth/sky visibility rules as the Moon. Use a dedicated shared material and no real point light.
- **Readability:** the present Moon is only about 3 m across at a camera-relative 100 m distance; full lettering will not fit its normal pixel footprint. At ordinary FOV, show a recognisable cyan/pink shop silhouette. Binocular inspection may open a clearly labelled magnified optical inset containing the Moon and readable sign; render that inset at a minimum 160 × 96 pixels before HUD scaling. The sign must remain visibly located on the lunar surface, not become a floating notification. Offer INSPECT MOON from the existing top-down clock arc when eligible so camera preference does not block discovery.
- **Payoff:** the observation unlocks an ordinary-cost miniature lunar-branch sign recipe, with no mechanical bonus. The real garage's sign may gain a tiny “EARTH BRANCH” plate after the player crafts it.
- **State:** proposed stable ID `ee_moon_branch`; world schedule plus player seen/unlock flags. Host owns the schedule in multiplayer; each player discovers it independently. Loading mid-window preserves the window and does not pay again. Cloud obstruction and time skips must not permanently consume an unseen opportunity.
- **Acceptance:** force visible and obscured windows; inspect from ground, binoculars and the top-down arc; capture readable text in pixel and HD modes with bloom off/on. Verify no sign through roofs, below the horizon, during the title's own lunar scene or on the daytime sun. Reload mid-event; reconnect a second player; confirm identical schedule, independent discovery and one unlock. Inspect an older save with no egg state.

### EE-02 — No atmosphere, no complaints [S]
- [ ] A small lunar-title placard says “TYRE PRESSURES CHECKED AT YOUR OWN RISK.” Reuse the sign lettering pipeline and place it beside the scrap rocket, where the existing camera can actually resolve it. Pure scenery, no save state. Capture it in both title render modes; re-record the boot film if its filmed portion changes.

### EE-03 — The longest warranty claim [M]
- [ ] After EE-01 is seen, a rare radio caller asks for a tow from “the other side of the crater.” The dispatcher pauses: “We'll put you on the list.” Add a short captioned `RadioNetwork` segment, queued behind news and limited to once per player. Discovery persists; subtitles carry the joke with audio muted. It grants no quest pin or fake rescue obligation.

### EE-04 — Lunar opening hours [S]
- [ ] While EE-01 is active, its little OPEN lamp briefly changes to BACK IN FIVE, then returns. Drive the cycle from the shared event clock, not randomness. Freeze on OPEN when reduced motion is selected. This is an extra visual for repeat observers, with no additional reward or checklist entry.

### EE-05 — Free delivery within reason [M]
- [ ] A garage advert shows a delivery circle around Earth and a handwritten exclusion for the Moon. Inspecting it after EE-01 adds “Apparently the exclusion is personal.” One text variant through the normal inspect/context action and the player's discovery flag; no new item or world simulation needed.

## Garage archaeology and vehicle personality

### EE-06 — The missing 10 mm socket [M]
- [ ] A workshop drawer contains a note about a vanished socket; a reachable prop under that site's bench holds it. Returning it to the drawer reveals a second note: “Now where's the ratchet?” Use one inspectable keepsake item and site state; it must never gate normal wrench work. Destroying the bench drops the socket once with its other contents. Payoff: a tiny labelled display mount.

### EE-07 — Percussive maintenance [M]
- [ ] A dead decorative workshop radio has TAP GENTLY as an explicit interaction. A tap restores one captioned weather jingle, followed by “Certified adjustment.” Use a prop-specific interaction and `Sfx`, not an arbitrary damage threshold that teaches players to hit every appliance. It stays repaired across streaming; no repeated salvage or reward.

### EE-08 — The world's slowest land-speed record [M]
- [ ] A dry, safe scrapyard lane has two chalk lines 20 m apart and a hand-painted record board for reversing a tractor. Opt in at the board; crossing under the normal driving controls records the time and earns a cardboard crown recipe once. Extend `Racing` with a low-speed variant. No traffic spawning in the lane, no reward for idling indefinitely, and abandoning is free.

### EE-09 — Factory extra [S]
- [ ] One seeded wreck's glovebox contains an invoice listing “Optional air: fitted to all tyres.” Its paper is searchable loot via the current wreck storage path, not a hint to ignition-key placement. The invoice is a keepable ordinary-value collectible; destruction and save/load follow existing item rules.

### EE-10 — The ceremonial spare bolt [M]
- [ ] After the player completes a substantial vehicle service, a nearby friendly mechanic places an oversized wooden display bolt on their counter: “Left over means it's lighter.” Trigger from completed work, never cancel/start. Use a once-per-player mechanic topic and a claimable prop. Actual installed parts, vehicle mass and service results remain accurate.

### EE-11 — Professional parking [M]
- [ ] Three tyre outlines mark a ridiculous tiny parking bay outside a large-vehicle workshop. Parking a bicycle within it prompts a nearby mechanic's deadpan approval. Read the chassis bounds and stationary state for two seconds; never judge by vehicle name. Award a cosmetic parking plaque once per site. No fines, blocked road or compulsory precision challenge.

### EE-12 — The dyno's tea break [S]
- [ ] After a normal tuning session, a purely decorative printer spits out “OPERATOR OUTPUT: ONE CUP SHORT.” Add it to an optional printout inspection, not the real torque graph. `VehicleTuning` supplies the session-complete hook; a daily cooldown prevents repetition. Stat readings stay mechanically trustworthy.

### EE-13 — A remarkably honest spoiler [S]
- [ ] A rare cosmetic part description reads “Adds confidence. Confidence is not downforce.” Make it a visual variant of an existing spoiler, with identical declared handling data and compatible sockets. Find it in normal parts stock; price and salvage match its base part. Do not introduce hidden speed bonuses.

### EE-14 — The washing line [M]
- [ ] Washing a heavily muddy owned car reveals a small painted “ALSO AVAILABLE IN BROWN” stencil. Use a grime-revealed decal on a seeded cosmetic paint variant, through `VehicleGrime` and `VehiclePaint`. Reveal by the existing wash threshold; it disappears under new mud and is not a second grime simulation. Keep the stencil off glass and driver sightlines.

## Roads, ruins and tiny mysteries

### EE-15 — Department of unnecessary detours [S]
- [ ] A short walkable path behind a roadside office loops back to its own sign: “YOU HAVE ARRIVED AT THE START.” Bake the loop into one site template, outside the useful road network. A rubber-stamp keepsake waits at the far desk. Validate pedestrian clearance and avoid placing it at a story anchor or the date-line band.

### EE-16 — The queue that survived [M]
- [ ] In a ruined office, take ticket 004 from a dispenser. A mechanical board falls from 003 to “LUNCH.” One animated prop, one optional inspection and saved dispensed state. Repeated presses say the roll is empty. The ticket can decorate a pinboard; the player never waits for real service.

### EE-17 — The road ends politely [S]
- [ ] A naturally terminating side road receives a worn “THANK YOU FOR USING THIS ROAD” sign and a small turning circle. Select only reachable dead ends already found in `RoadNetwork`; never terminate a through-route for the joke. Static reward: a good view and a small picnic spot, with ordinary regional loot.

### EE-18 — Emergency emergency supplies [M]
- [ ] An emergency cabinet holds a smaller cabinet, which holds a tin marked “Emergency biscuits.” Two inspect/open steps at most. Use nested prop states with one ordinary food reward; opening or smashing resolves the same contents ledger. Avoid recursive containers or a long gag that delays urgent supplies.

### EE-19 — Lost property, mostly left boots [S]
- [ ] A station shelf contains mismatched boot props and a ledger: “Right boots: under investigation.” One normal clothing item may be recovered from the site. Reuse clothing meshes, `Lootable` and ordinary storage. Do not force a unique wearable pairing system for a single scene.

### EE-20 — The scenic shortcut [M]
- [ ] Two signs claim “SHORTCUT” and “SHORTCUT, HONEST” on a harmless side trail. Both arrive at the same overlook, where a note admits “We just wanted visitors.” Seed the trail only where a passable return route exists. A guestbook tracks player visits with a capped list in co-op; no generated fake player names or hostile ambush reward.

### EE-21 — The last traffic cone [M]
- [ ] A fenced concrete plinth honours a solitary cone as “Keeper of the Lane.” Inspecting three plaques unlocks a cone-shaped trophy recipe. Use site interactions and a three-bit progress mask; plaques can be read in any order, and destroyed text remains in the notebook after discovery. The ordinary cone can be moved without blocking progression.

### EE-22 — Pothole with a name [M]
- [ ] A village has affectionately named a pothole “Gerald.” Repairing its actual terrain damage produces a later thank-you note: “Gerald has retired.” Tie completion to the existing road repair result, with a saved baseline and sufficient filled area. The road stays improved; nobody recreates the defect for a daily reward.

### EE-23 — Extremely local history [S]
- [ ] A roadside museum contains a spoon, a bent axle and a wall of exaggerated captions. All props use ordinary material yields. One inspection unlocks miniature display labels for the garage. Treat the site as authored scenery with normal loot; no combat encounter or museum simulation is necessary.

### EE-24 — The surveyor's lunch [M]
- [ ] A metal detector leads to three shallow buried markers ending at a lunchbox: “Ore body: disappointing. Sandwich body: promising.” Extend prospecting with one stable buried-container signal. The food is preserved, the markers are recoverable scrap, and the egg never replaces a real ore vein or distorts geiger hazard readings.

## Radio, television and local conversation

### EE-25 — Forecast: still outside [S]
- [ ] On a calm day, a weather presenter corrects an absurdly obvious forecast before giving the real weather. Add a short captioned radio variant selected from actual weather state. Once per several world days; never replace storm, raid or fuel-emergency information. No item reward.

### EE-26 — Hold music for the apocalypse [M]
- [ ] Repairing a derelict roadside phone plays a short original instrumental, then “Your call is important to someone.” A nearby written transcript offers the same joke. Use a powered interactable, finite playback and a once-per-site discovery; stop audio on distance/unload. No live network connection or endless ringing.

### EE-27 — The phantom mechanic caller [M]
- [ ] A call-in show describes the player's recently repaired car in broad terms: “The one with the brave amount of rust.” Select from real, coarse service events and omit names or precise location. Save only a pending event token. Fire once after returning to calm travel; it never reveals hidden damage or secret story knowledge.

### EE-28 — VHS tracking advice [S]
- [ ] One scavenged comedy tape features a mechanic confidently adjusting the wrong screw on a television, then admitting defeat. Use `MediaLibrary` and `TvSet`; short still panels and captions can deliver the first version without a video-production dependency. It gives an ordinary entertainment benefit only, no required knowledge gate.

### EE-29 — The sponsor nobody remembers [S]
- [ ] A radio advert promises “Genuine replacement left-handed piston returns.” A matching empty parts box appears in garage clutter. Shared flavour ID links the two discoveries in the notebook. Schedule the advert like other optional radio segments and make the box normal cardboard/wood salvage.

### EE-30 — Duelling workshop signs [M]
- [ ] Two neighbouring mechanics' chalkboards gradually reply to each other: “WE FIX ANYTHING” / “WE FIX WHAT THEY FIXED.” Choose a paired shop site and advance through three text states on visits separated by a world day. Save the pair's stage; no repeated price changes or reputation penalties. Final reward is a printable sign recipe.

### EE-31 — The polite echo [M]
- [ ] A marked listening alcove in a safe cave returns a softly delayed “hello,” then a quieter “keep it down.” Opt in by inspecting an old speaking tube. Play authored lines through `Sfx` with captions; do not record microphone audio. Suppress during combat and disable when the cave is destroyed or unloaded.

## Kitchen, garden and animals

### EE-32 — Soup of the previous day [S]
- [ ] A diner's menu names today's stew “Yesterday's Tomorrow.” Inspecting it reveals the cook's practical preservation notes. Ordinary recipe clue and dialogue flavour through `MediaLibrary`/`NpcLore`; no new spoilage rules or secret health effect. Repeat visits change the chalk date from world time.

### EE-33 — Prize-winning crooked carrot [M]
- [ ] A seeded, well-tended garden harvest can yield one comically forked carrot keepsake alongside its normal crop. Use the completed-harvest hook and a saved plot/event roll. Eating or selling it is allowed; inspection unlocks a craftable replica trophy so displaying it is not a permanent inventory obligation. No boosted nutrition or repeat-harvest rerolls.

### EE-34 — Scarecrow on break [M]
- [ ] Beside one authored farm, a second decorative scarecrow sits in a chair holding a cup. A sign reads “UNION BREAK.” Its standing partner supplies the ordinary scarecrow function; the seated prop advertises no pest protection. Reuse furniture and a static character pose. The joke must not silently invalidate crop defence.

### EE-35 — The bee inspector [M]
- [ ] After a healthy hive's first honey collection, an inspection card reads “WORKFORCE: VERY BUSY.” Place it in the hive output with the honey, once per owned hive lineage. `Animal`/hive production hooks supply real output; the card is flavour only. No swarm attack or extra honey farm from dismantling and replacing the hive.

### EE-36 — The goat's union representative [M]
- [ ] A calm village goat stands on a low crate labelled MANAGEMENT. Talking to its owner produces a complaint about scheduling. Select a safe idle perch at one farm and fall back to the ground when blocked. No new pathfinding layer, forced theft, displaced player animal or global goat behaviour; the animal remains an ordinary goat.

### EE-37 — The guard dog clocked out [M]
- [ ] On a safe evening, a well-fed guard dog's nearby bed has a little reversible ON DUTY / DREAMING placard. Flip it from the animal's actual sleep state and offer one owner remark. Combat wakes the dog by normal rules; the gag never disables defence. No reward and no extra per-frame world scan.

### EE-38 — Fish with a receipt [M]
- [ ] A rare ordinary fishing catch arrives with an old tagged lure and a receipt for “one very large fish.” Add the paper through a once-per-waterbody fishing event, keeping the caught species and weight honest. Display or sell the lure; it does not alter bite rates. This extends the existing boot/can/lockbox catches rather than introducing them again.

### EE-39 — The champion of absolutely nothing [M]
- [ ] A village fair table awards a handmade ribbon for submitting any ordinary locally grown vegetable. Different items get affectionate, specific comments; one ribbon per player per world. Use a small `Dialogue` hand-over and standard inventory transaction. No stat ranking, repeated barter exploit or requirement for rare quality.

### EE-40 — Midnight pantry audit [M]
- [ ] Once a stocked home has survived a winter, a resident leaves a note: “Counted the beans. Most are present.” Use the real pantry summary and resident presence to select a truthful line; hungry residents get a practical request instead. Place the note on a reachable table after sleep. No food disappears for the joke.

## Machines, utilities and the working home

### EE-41 — The professional bubble level [M]
- [ ] Inspecting a level workshop floor with a decorative spirit level yields “SUSPICIOUSLY COMPETENT.” Read the surface normal at the placed tool; a sloped floor gets “ARTISTIC.” One small tool/prop interaction, no building buff. Offer a wall certificate recipe after either result so precise terrain work is optional.

### EE-42 — Breaker labels from a previous owner [S]
- [ ] One salvaged breaker panel carries labels “LIGHTS,” “IMPORTANT LIGHTS” and “DON'T ASK.” These are flavour decals; the real switch UI continues to name actual connected loads and essential/normal/low priorities. Normal `UtilityGrid` and `IPlaceState` behaviour, no prank power outage or undocumented wiring.

### EE-43 — The forklift ballet [M]
- [ ] At a cleared cargo yard, three painted loading pads form a voluntary precision-parking challenge for a compatible machine. A tinny original waltz plays only after the player opts in. Extend `Racing` or a local challenge component with stationary-pad checks. Reward a loading-company decal once; cargo damage and collisions still follow normal physics.

### EE-44 — Certified hole [M]
- [ ] Dig to a modest marked depth at an optional survey site and a clipboard certifies “YES, THAT IS A HOLE.” Reuse the story engine's ground-change goal, baseline saved at acceptance. The certificate is a keepsake; soil is normal soil. Site selection excludes groundwater hazards, roads and story foundations; cancellation leaves no obligation to finish.

### EE-45 — A turbine's fan club [S]
- [ ] A ruined wind-farm hut holds a framed “FAN OF THE MONTH” award beneath a drawing of a turbine. A decorative build recipe unlocks on inspection. Reuse wall furniture, palettes and ordinary resource costs; no generation bonus and no dependency on deferred regional weather.

### EE-46 — The laundry forecast [M]
- [ ] A dry-home resident points at laundry and says “Forecast says indoors.” Trigger only when actual rain approaches under the existing global weather model and shelter is available. One captioned line and static laundry prop; ordinary weather remains authoritative. Do not pretend local forecasting exists or force clothes to become wet.

## Coast, airfields and gentle impossibilities

### EE-47 — The underwater car wash [M]
- [ ] A sunken roadside sign advertises “ULTIMATE RINSE.” Nearby shallow wrecks tell the story; a waterproof till contains a single refund token keepsake. Place via a validated coastal site template with safe shore access and optional deeper scenery. No invisible air pocket, submarine obligation or special car-washing mechanic.

### EE-48 — The most optimistic lighthouse [S]
- [ ] A miniature lighthouse stands beside a puddle at a coastal workshop. Its placard says “START SMALL.” A capped, low-cost lamp follows dusk through existing light-budget rules. Static site decoration; inspecting unlocks the same small build piece. It must not register as a navigation aid on the map.

### EE-49 — Airfield complaints department [S]
- [ ] At a hangar, a complaints box sits beside a windsock with a note: “Wind enquiries outside.” Use normal props and inspection text. Keep it clear of taxi routes and actual aviation blueprints; no interaction with wind strength or controls.

### EE-50 — Message in a bottle, local delivery [M]
- [ ] A bottle on a reachable shore contains directions to a person only 30 m away: “Boat unavailable.” Complete a tiny optional hand-over for a thank-you and a bottle display recipe. Anchor both endpoints together; if the recipient is gone, the paper remains a complete joke and inspection unlocks the recipe. Do not depend on per-instance liquid contents until that storage gap is resolved.

### EE-51 — The boat inspection boat [M]
- [ ] A toy boat sits inside a dock inspector's lunch tray, bearing a full-size registration plaque. Recover it as an ordinary keepsake world item. Use `ItemModels` and the existing place/drop loop; no miniature `BoatModel` or autonomous actor. It can decorate a houseboat without affecting buoyancy.

### EE-52 — The last mile marker [M]
- [ ] After a player completes an east–west circumnavigation, a port clerk stamps a card “BACK WHERE YOU STARTED, EXPERIENCED.” Track signed wrap crossings plus travelled route segments to reject seam oscillation. Use `CrossDateLine` and existing journey records, not a new planetary system. Award once per player; no need to land exactly on the seam or carry a fragile quest paper.

## Delivery and verification

### Suggested first release

Ship EE-01 as the signature discovery, plus EE-06, EE-09, EE-16, EE-22, EE-25, EE-33 and EE-48. This gives one sky event, garage humour, useful-world interaction, an audio surprise and home keepsakes. Build later groups only after their recurrence feels welcome in normal play.

### Minimal shared support, added only as needed

Use existing story/event, journal, item and place-state infrastructure where it fits. Proposed new names below are design placeholders:

- An egg definition needs a stable ID, prerequisites, trigger category, cooldown policy, optional site key and reward ID. Keep raw flavour text localisable and original; avoid borrowed franchise dialogue or music.
- Start with a compact saved ledger of discoveries, paid rewards, event windows and site phases. Use existing stable prop/site identities, not array positions that change when content is added. Add new state to `SaveData`, current capture/restore paths and authoritative replication where applicable; absent fields mean undiscovered.
- Reward claims resolve atomically with ordinary inventory/world-item operations. If placement/storage is full, keep a pending claim at the source. Reload, destroying the source and two simultaneous clients cannot duplicate it. Cosmetics unlocked by inspection need no consumable reward transaction.
- Physical alterations belong in normal destruction, item or `IPlaceState` persistence. Keep purely visual timing derived from world time. Bound history, scheduled events, audio voices and streamed actors.
- Evaluate triggers from completed actions, day changes and nearby site activation. No global scans over every crop, vehicle or animal. Reset statics on SubsystemRegistration and validate cached Unity objects with Unity's live-object check.

### Acceptance checklist for each shipped entry

- [ ] A natural route exposes at least one clue without knowing the solution; a fresh-save playthrough records discovery time and whether the joke interrupted anything useful.
- [ ] Deterministic fixture proves trigger, non-trigger, cancellation where relevant, reward conservation and cooldown. Proposed scenario names use `eastereggs.<id>`; they do not exist yet.
- [ ] Reload before/after reward, unload/revisit, source destruction and an older save are covered. Shared events also cover simultaneous claims and reconnect.
- [ ] Capture the actual discovery in pixel and HD modes; verify ordinary FOV/HUD scaling, muted audio with captions, rebound controls and gamepad access.
- [ ] Confirm quests, AI routes, warning audio, collision clearance and loot balance remain intact. Missed timed appearances recur without calendar chores.
- [ ] Keep spoiler-filled developer fixtures out of ordinary player maps and help text. Mark an entry done only after implementation and evidence, not after writing its design.
