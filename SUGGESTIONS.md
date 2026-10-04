# Mad Mike's Garage — Experience and system-depth suggestions

Companion backlog to [ROADMAP.md](ROADMAP.md) and [EASTEREGGS.md](EASTEREGGS.md). These are proposals for review, not verified defects or implementation claims. All tasks remain open. References use roadmap section names because the document contains overlapping historical plans and later completion notes; inspect the current implementation before promoting an item.

The roadmap already covers a remarkable amount of simulation. The next gains should come from memorable decisions: what to take, whom to help, which route to trust, what to repair now, and what kind of home to return to. More upkeep meters alone would make that loop longer without making it better.

## What this builds on

| Already recorded in the roadmap | Extension proposed here |
|---|---|
| FIRST STEPS, playable campaign, map, journal, context actions and catalogue menus | A calmer first session, optional learning, clear next actions and fewer interruptions |
| HOME ledger, pantry estimates, station radio calls, root cellar and seasonal stock | Player priorities, practical shortage responses and planning around an actual trip |
| Road building, player-road routing, convoys, delivery jobs and route-comparison story quests | Repeatable service routes and lasting public works outside a single quest |
| Damage, tuning, service, physical fluids, ignition keys, engine fires and prosthetics | Diagnosis, recovery and adaptation that reward judgement without trapping a damaged character |
| Faction standing, residents, companions, town news, markets and scavenged wrecks | Named relationships, specific needs and visible consequences rather than more abstract reputation |
| Seasons, water contamination, animal care, farming and utility controls | Manageable local tradeoffs and understandable resource flows |
| Q0–Q8 acceptance programme and W0–W3 coherence audit | Focused experience hypotheses with measurable checks and short human playtests |

**Existing open work to extend, not duplicate:** journal job flooding; actionable HOME lines; handheld home radio; scavenger trade; resident harvest/fishing work; campaign relocation/pacing/voices/shared ledger; per-instance fluid storage; extinguisher and mobility aids; catalogue search/descriptions. The proposals below give those items a useful first slice. Regional weather and hemisphere seasons remain deferred, as the roadmap requests.

Priority: **P1** improves comprehension, recovery or a central loop; **P2** adds meaningful depth after the basics hold; **P3** is optional breadth. Effort: **S** data/UI changes, **M** one system extension, **L** several connected systems. Priorities are recommendations, not permission to implement this whole document.

## First session, travel and coming home

### SG-01 — One intention at a time · P1 / M
- [ ] **Player choice:** pin one current intention—repair the car, reach a town, finish a home job—while keeping other discovered work available in the journal.
- **First slice:** extend the existing journal-flooding item: separate accepted work, discovered leads and completed history; show one optional objective line, with a dismissible NEXT STEP explanation based on known information. Never reveal all story-cast identities at game start.
- **Hooks:** `MenuSystem`, map/journal and `Story` progress. Save selected intention per player; preserve it through death/load and gracefully clear an invalid destination.
- **Evidence:** new story and sandbox players can find an accepted job among 40 available leads, pin it, cancel the pin and return to play by keyboard and pad. Capture the opening screen for spoilers and text overflow.

### SG-02 — A trip plan that admits uncertainty · P1 / M
- [ ] **Player choice:** pack light for a known short route, or carry water, fuel and a spare for a longer uncertain journey.
- **First slice:** before accepting a haul, show destination, known road distance, deadline and a qualitative cargo/vehicle suitability note. Estimate fuel as a range from the current vehicle's observed consumption, including towing; mark unknown stretches instead of inventing precision. Let players save a packing checklist.
- **Hooks:** `RoadRoute`, `Contracts`, `VehicleSystems`, cargo and the map. Do not expose hidden hostiles, undiscovered roads or exact future weather. Planning does not reserve or conjure supplies.
- **Evidence:** a loaded round trip falls within a declared broad estimate on the reference route; an unknown route clearly says so. Replacing the engine or attaching a trailer invalidates stale estimates. Plans survive restart.

### SG-03 — A fair way back from a breakdown · P1 / L
- [ ] **Player choice:** self-repair, recover with a winch, walk for help, or pay a known settlement for a tow.
- **First slice:** one town mechanic offers a recovery job for a reachable owned disabled vehicle. Quote the fee and destination before acceptance; schedule a tow when the route is valid, or offer a clearly described off-screen recovery after the player leaves. No recovery into blocked terrain, across oceans or out of active combat.
- **Hooks:** town dialogue, `TowCoupling`, road routing and stable vehicle IDs. Save the accepted job and payment state; do not create a second copy of the car. A radio call is a convenience after the service is discovered, not a starting requirement.
- **Evidence:** fuel-empty, overturned and engine-damaged cars each have an attainable recovery path under scarce loot. Reload during recovery preserves cargo, damage and ownership and charges once. Failure refunds or leaves a resumable job with an explicit reason.

### SG-04 — Warnings should point to a doable response · P1 / M
- [ ] **Player choice:** continue with a understood risk, attempt a field remedy, or head for help.
- **First slice:** review fuel, heat, bleeding, back strain, cold and air warnings. Show a short observable symptom plus one available action; optional inspection explains more. Keep the recent words-first interface: “engine knocking” before a diagnosis unless a tool or skill supports it.
- **Hooks:** existing vitals, vehicle systems, `Hints`, context actions and the health page. Rate-limit repeated warnings but escalate genuine deterioration. Let the player inspect the last warning after it fades.
- **Evidence:** trigger each warning with and without supplies. The suggested action is actually possible, essential feedback fits every camera, and muted audio still communicates urgency. Avoid a universal countdown that reveals hidden numerical systems.

### SG-05 — Protect the quiet part of the loop · P1 / M
- [ ] **Player choice:** take a longer expedition without returning to five unrelated emergencies every time.
- **First slice:** measure an ordinary 20-minute journey's accumulated chores. Add configurable grace/recovery windows to discretionary raid scheduling and reminder frequency if needed; keep physics, fuel use and existing fires honest. Distinguish overdue optional maintenance from imminent loss.
- **Hooks:** `BaseRaid`, `BaseUpkeep`, home ledger and event schedules. Save scheduled windows so reload cannot reroll threats. Existing difficulty rules still control risk.
- **Evidence:** compare normal and harsh presets across a season. Report minutes of useful exploration versus compulsory maintenance, and whether players can name what caused losses. Tune from results; a target ratio is a design budget to agree on, not a universal definition of fun.

### SG-06 — A return-home ritual · P2 / M
- [ ] **Player choice:** unload, eat, service the car or tell a resident about the journey in whichever order feels natural.
- **First slice:** use existing home-ledger data to offer a single optional arrival summary: ready work, one urgent need and one relevant resident remark. Add a deposit action with a preview for player-selected cargo categories; exclude equipped gear, quest items and reserved supplies by default.
- **Hooks:** HOME ledger, `Container`, residents and `WastelandGame.Garage`. Remember category preferences per home. Capacity failure leaves remaining items in the source.
- **Evidence:** a full truck unloads into several nearly full containers without losing items or reserved fuel. Arrival feedback appears once per meaningful journey, not on every step over the claim boundary.

## A garage worth knowing

### SG-07 — Diagnose before replacing everything · P1 / M
- [ ] **Player choice:** follow symptoms and perform a cheap check before buying an expensive part.
- **First slice:** add two or three contextual checks using existing faults: inspect a fouled filter, test a battery connection, inspect an overheating radiator. Novices get plain observations; Mechanics skill or appropriate tools narrow the cause. A service quote lists the work and materials before it starts.
- **Hooks:** `VehicleSystems`, `VehicleIgnition`, work animations and service UI. Read the actual model; do not add random false diagnoses or another hidden reliability stat.
- **Evidence:** three faults with similar loss-of-power symptoms produce distinguishable observations and successful repairs. Cancelling work neither consumes materials nor claims success; the repaired vehicle actually performs differently.

### SG-08 — A field patch with an honest limit · P2 / M
- [ ] **Player choice:** use a temporary patch to get home or spend more time and materials on a workshop repair.
- **First slice:** one radiator patch reduces a real leak for a limited amount of engine operation. Inspection calls it PATCHED and names the workshop remedy. Keep full repairs available through existing service; avoid temporary versions of every component.
- **Hooks:** part-level `IPlaceState`, `VehicleSystems` and recipes. The patch belongs to the part when swapped. Remaining life advances under the documented off-screen policy, not real wall time.
- **Evidence:** swapping, selling, loading and towing the patched part preserve its state. Warnings precede the renewed leak. No reload refresh and no compounded patch stack that outperforms a sound radiator.

### SG-09 — Workshop comparison drives · P2 / M
- [ ] **Player choice:** tune for a muddy haul, a road race or fuel economy and feel the compromise.
- **First slice:** save two setup presets for the same owned vehicle and provide an optional marked test loop near a garage. Report measured stopping distance, climbing result and consumed fuel alongside the existing dyno estimate; separate unloaded and loaded runs.
- **Hooks:** `VehicleTuning`, `Racing`, map and current vehicle IDs. Applying a preset still checks parts, materials and skill. Do not let it repair damage or restore spent nitrous.
- **Evidence:** short gearing helps the intended loaded climb while changing cruise behaviour. Swapping a part marks incompatible preset fields. Saved results record conditions and cannot be misrepresented as universal top speed.

### SG-10 — The car keeps its story · P2 / M
- [ ] **Player choice:** restore a battered chassis, preserve its scars, or pass it to another driver.
- **First slice:** an optional vehicle log records acquisition, first major repair, notable haul and chosen name. Let paint work preserve an existing decal where practical. Record a handful of milestones, not every collision.
- **Hooks:** vehicle save identity, existing paint/dent persistence and completed jobs. Service records are descriptions of real events, with a bounded history; avoid hidden affection bonuses.
- **Evidence:** the log travels with the vehicle through sale or supported transfer, replacement parts do not change chassis identity, and a new game does not inherit the previous fleet's records.

### SG-11 — Fire response before burnout · P1 / M
- [ ] **Player choice:** stop and suppress an early engine fire, unload valuable cargo, or abandon the vehicle.
- **First slice:** promote the roadmap's extinguisher suggestion into a complete response: clear ignition feedback, reachable extinguisher interaction, finite charge, actual suppression and a persistent damaged aftermath. Add shovel sand only where material is available and targeting is understandable.
- **Hooks:** `VehicleBurn`, `Fire`, tool use and work cancellation. Suppression checks once per vehicle, not once per collider. Extinguishing does not repair the engine or refill fluids.
- **Evidence:** an attentive player with equipment can interrupt early burnout, delayed response can fail, and the outcome is readable before the tank event. Repeated input, reload and two players cannot duplicate charges or bypass terminal wreck state.

### SG-12 — Injury should change the plan, not end all plans · P1 / L
- [ ] **Player choice:** adapt controls and equipment, seek care, or arrange transport while healing.
- **First slice:** connect the open crutch and clinic-prosthetics suggestions to a recovery path: affordable basic aid, reachable fitting service, seated work that injured players can still do, and clear tool/vehicle restrictions. Audit manual clutch and two-handed tool gates for a viable alternative.
- **Hooks:** `Limbs`, `ProstheticLibrary`, clinic/vendor stock, `CharacterStats` and action eligibility. Aids use normal item/state rules; avoid an additional daily rehabilitation chore in the first slice.
- **Evidence:** a character with a severe leg or arm injury can obtain help from both story and sandbox locations without test grants. No aid advertises an action the rig or controls cannot perform. Save/load retains fitted equipment and treatment progress.

## Work, people and a world that remembers

### SG-13 — Specific shortages, small visible outcomes · P2 / L
- [ ] **Player choice:** bring a town the material it lacks instead of trading only against price multipliers.
- **First slice:** one visible workshop or clinic publishes a bounded need derived from its stock. A delivery restores one real service for a stated world-time period. Price/news/dialogue use the same shortage record; it cannot also be an unrelated generic supply quest.
- **Hooks:** `Market`, `Contracts`, town services and `TownNews`. Save quantity, donor contribution, expiry and service effect. Cap repeated reputation rewards; buying and returning the same local stock must not farm standing.
- **Evidence:** partial supply, completion, expiry and reload produce consistent vendor availability and news. The town does not require the player to solve every shortage indefinitely.

### SG-14 — Scavengers as competitors and contacts · P2 / M
- [ ] **Player choice:** hurry for untouched wrecks, negotiate with a late-arriving scavenger, or trade information.
- **First slice:** extend the existing scavenger-trade suggestion: removed parts enter a bounded stock with original condition, and an inspectable note connects the trader to the wreck. A peaceful bargain can establish a recurring parts contact.
- **Hooks:** road-wreck age, `NpcDirector.Scavengers`, `Trade` and part identity. Explicitly transfer ownership of stripped parts; do not leave duplicate copies on the car. Save stock while the NPC is away.
- **Evidence:** leave before stripping and return after it; total parts are conserved. Killing, trading, streaming and reloading cannot award the same engine twice. Host authority resolves simultaneous buyers.

### SG-15 — Public works earn their place · P2 / L
- [ ] **Player choice:** invest in a bridge or reliable road because it changes a useful route, or leave the detour and spend resources elsewhere.
- **First slice:** outside the campaign's one-off crossing quests, identify one demonstrated bottleneck. A completed, validated player crossing lets a scheduled trader use the shorter route and creates a modest local service benefit. A board thanks the builder after a successful crossing.
- **Hooks:** existing player roads, `RoadRoute.FindForDriving`, bridge decks, traffic and town news. Benefit follows actual reachability and survives destruction/rebuild without repeated first-build rewards.
- **Evidence:** a loaded convoy physically crosses the new route; removing support makes it choose the safe alternative. Save/load and off-screen simulation agree on the route's availability. Traffic must not appear through locked player gates.

### SG-16 — Recurring routes with manageable commitments · P2 / L
- [ ] **Player choice:** become a reliable haulier, delegate a familiar run or keep travelling freely.
- **First slice:** extend C5's open recurring-traffic work with one optional weekly supply agreement between two known towns. Limit scheduled runs and allow a clear pause/cancel policy. An assigned companion uses an owned suitable vehicle and actual stocked cargo.
- **Hooks:** `Contracts`, companions, `AiDriver` and the campaign's convoy work. Save route, driver, vehicle, reserved cargo and current stage. Off-screen results use a declared approximation; never duplicate cargo when the route becomes live.
- **Evidence:** live and off-screen runs preserve supplies, ownership and pay; interruption leaves a recoverable location. The driver has a usable status report, and cancellation does not confiscate the player's vehicle.

### SG-17 — Residents with useful preferences · P2 / M
- [ ] **Player choice:** make a home that suits its people rather than add identical furniture for a larger comfort score.
- **First slice:** give each resident one preference and one contribution: a quiet reading corner, a workbench with shelter, a regular shared meal. Satisfaction changes optional dialogue or willingness to offer a service, with a broad grace period. No friendship decay from a missed daily greeting.
- **Hooks:** `NpcProfile`, residents, `Comfort`, existing work roles and pantry. Preferences refer to real usable spaces; assign an accessible seat/workplace and handle demolition gracefully.
- **Evidence:** two layouts with the same generic comfort can satisfy different preferences. A week away does not erase a relationship; hunger and dangerous conditions still produce truthful concerns.

### SG-18 — Small acts remembered by the right people · P2 / M
- [ ] **Player choice:** help someone on the road and later recognise the person, without every favour moving an entire faction score.
- **First slice:** save a few specific deeds—tow, shared water, returned property—against the recipient. The next encounter offers a relevant greeting and at most one modest favour. Keep faction consequences for deeds with witnesses or established communication.
- **Hooks:** `NpcSave`, `Dialogue`, town news and existing stable NPC identities. Bound memory to a few important records. Do not respawn dead recipients to pay a reward.
- **Evidence:** only the relevant person or informed witnesses know the deed; a distant stranger does not. Repeated hand-over interactions cannot farm favours, and a loaded save retains the same relationship.

### SG-19 — Retreat is a complete outcome · P1 / M
- [ ] **Player choice:** escape a losing fight, pay a toll, abandon cargo or negotiate, with understandable consequences.
- **First slice:** audit one road ambush from warning through disengagement. Give raiders a bounded pursuit/leash policy and a clear condition for accepting a toll or abandoned designated cargo. Do not require killing every enemy to regain ordinary travel.
- **Hooks:** `Convoy`, parley, surrender and combat targets. Save accepted terms so re-entering a streamed road does not immediately repeat the same demand.
- **Evidence:** a lightly equipped player can escape using an available route; enemies neither chase forever nor disappear arbitrarily in plain sight. Co-op clients receive the same terms and cannot pay twice.

## A productive home without a second job

### SG-20 — Stock targets and reserved supplies · P1 / L
- [ ] **Player choice:** reserve drinking water and travel fuel while letting workshops use surplus.
- **First slice:** add per-container reserve thresholds for a small set of critical resources, respected by unattended crafting and automatic home service. Manual use may override with a clear preview. Later, optionally repeat a single recipe until a stock target is reached.
- **Hooks:** `Container`, crafting spend paths, `UtilityGrid` and home service. Use one shared availability calculation for UI and consumption. A reserve is a rule on existing stock, not a copy of items.
- **Evidence:** simultaneous consumers cannot overspend, reserve the same unit twice or report work as ready when blocked. Reload and cancellation preserve stock and targets. Empty reserves stop work with an actionable reason.

### SG-21 — Power and water faults you can trace · P1 / M
- [ ] **Player choice:** isolate a faulty branch, shed a luxury load or increase supply.
- **First slice:** a temporary inspect overlay traces the selected consumer to its source and names the first real blocker: disconnected, insufficient supply, empty source, closed valve or poor water quality. Add the proposed actionable HOME entry as a shortcut to inspect that consumer.
- **Hooks:** `UtilityGrid`, breaker priorities, valves and water quality. Reuse network membership; do not build a second graph that can disagree. Do not reveal distant undiscovered infrastructure.
- **Evidence:** break and restore each failure type and compare overlay, physical flow and machine operation. Moving a vehicle-mounted consumer or crossing the seam updates the path correctly.

### SG-22 — Container identity before deeper chemistry · P1 / L
- [ ] **Player choice:** label a clean-water bottle, keep a suspicious fuel sample and know which can is safe to pour.
- **First slice:** close the roadmap's per-instance fluid-state gap before adding settling or drain plugs. Contents, label and capacity must belong to one item instance across pack, bag, world, vehicle storage and trade. Keep sensory descriptions consistent with the recent interface pass; analysis tools can reveal more.
- **Hooks:** `FluidMix`, `FluidContainers`, `Container`, `WorldItem`, item codecs and save migration. Define how older per-ID pools migrate without inventing or discarding litres; surface ambiguous legacy state in development diagnostics.
- **Evidence:** two identical cans with different blends keep their identities through every transfer, drop, sale, reload and client hand-over. Assert volume and constituent conservation, including partial pours and full destinations.

### SG-23 — Local contamination with a way to repair it · P2 / M
- [ ] **Player choice:** put industry where it is convenient, or protect a nearby water source and accept a longer pipe run.
- **First slice:** explain the existing latrine/trough/oil contamination influences through inspection and a test kit; add one reversible cleanup action for an identified source. Record baseline quality and let recovery take clear world time. Avoid unbounded pollution spreading across the whole world.
- **Hooks:** current `WaterQuality`, wells, oil ground and utility network. Placement preview may warn of a known nearby drinking source; it must not reveal hidden deposits for free.
- **Evidence:** moving the cause or treating the affected supply changes measured quality for the same reason the warning named. Returning after days matches the off-screen policy, and clean/dirty mixing does not create free clean water.

### SG-24 — Farming decisions beyond bigger batches · P2 / M
- [ ] **Player choice:** specialise for profit, grow reliable staples or diversify against a bad season.
- **First slice:** use existing fertility, water, season and storage data to show a qualitative crop suitability note before planting. Add at most one simple rotation relationship after measuring current yields; do not start with a new multi-nutrient chemistry simulation.
- **Hooks:** `GardenPlot`, `Fields`, `CropDef` and pantry estimates. Crop history belongs to the plot and survives reseeding. The player can ignore the bonus and still sustain a modest farm.
- **Evidence:** two plausible crop plans both work with different labour/water/storage costs. A rotation improves a declared outcome without becoming the only viable strategy. Test winter and greenhouse rules already present.

### SG-25 — Animal care as relationship and logistics · P2 / M
- [ ] **Player choice:** keep a smaller herd close to home, build a dependable feed system or arrange a caretaker before leaving.
- **First slice:** combine existing trough/feed/treatment information into a simple care estimate and a resident caretaker role with defined supplies and capacity. Give important kept animals names and a few event-based reactions; no separate affection chore bar.
- **Hooks:** `AnimalDirector`, kept-animal save state, husbandry and residents. Treat food and medicine as real inventory transactions. Audit actual enclosure reachability before relying on nearby-fence protection.
- **Evidence:** a planned trip consumes the expected feed, a blocked trough produces an understandable warning, and returning after streaming does not double breeding or products. Care cannot work without supplies or an available caretaker.

## Discovery, expression and lasting purpose

### SG-26 — Regional expertise instead of endless new resources · P2 / M
- [ ] **Player choice:** revisit a familiar place for a known speciality or explore for another source.
- **First slice:** assign a few towns existing specialities—good filters, preserved fish, machine work—using geography and actual vendor/service capacity. Clues come from residents, prices and delivered stock. Give critical goods at least one alternative route under scarce-loot rules.
- **Hooks:** deterministic settlement profiles, `Trade`, `Market` and rumours. Keep the current resource enum; first prove identity using existing items.
- **Evidence:** seed-corpus journeys find useful differences without mandatory cross-world shopping for basic survival. Depleting stock and waiting for replenishment agree with the town's stated role.

### SG-27 — Optional mastery jobs · P2 / M
- [ ] **Player choice:** become an expert tow operator, economical haulier, careful pilot or precision builder.
- **First slice:** three voluntary contracts reuse existing verbs with explicit conditions: return a fragile load intact, tow within a damage allowance, or complete a fuel-budget run. Offer ordinary pay plus cosmetic recognition, with no permanent penalty for declining or retrying.
- **Hooks:** `Contracts`, damage/service records, `Racing` timing and authoritative completion checks. Tell players what counts before they commit; aggregate chassis/part changes robustly.
- **Evidence:** a competent run passes, a deliberately violated constraint fails clearly, and reload cannot reset consumed fuel or damage. Never reward dangerous high-speed driving on an unvalidated public route.

### SG-28 — Keep the player's history in the garage · P3 / M
- [ ] **Player choice:** display travel keepsakes, a retired part, a community thank-you or an Easter egg discovery.
- **First slice:** expand existing trophy mounts and the B5 keepsake wall with named labels for ordinary world items and a few significant completed deeds. Offer a small travel notebook with player-entered captions before considering a full photo mode.
- **Hooks:** `TrophyMount`, placeable state, item models and completed story records. Displaying an item transfers it into the mount; destruction returns it through ordinary loot. Never copy a working engine into a free display item.
- **Evidence:** place, rename, move, destroy, restore and share a display without duplication or lost text. Labels remain readable in pixel and HD modes and do not leak undiscovered story outcomes.

### SG-29 — Post-campaign life reflects the ending · P2 / L
- [ ] **Player choice:** stay at the refuge, run the workshop, travel with a convoy or support a chosen community after the finale.
- **First slice:** build on existing ending flags and fuel-price/faction consequences: one recurring service opportunity and two local changes specific to the chosen outcome. Let the player opt out. Finish open relocation and shared-story authority before moving households or making co-op promises.
- **Hooks:** `Story`, resident roles, contracts, world news and garage ownership. Changes refer to surviving characters and actual infrastructure; do not reset campaign decisions to issue repeatable quests.
- **Evidence:** each ending variant creates internally consistent services, dialogue and news after reload. A dead or departed cast member is not required to continue. No compulsory daily maintenance treadmill replaces the ending.

### SG-30 — Feedback hierarchy and human playtests · P1 / M
- [ ] **Player choice:** notice danger, understand the current action, then enjoy atmosphere and jokes.
- **First slice:** prioritise vital threats, committed work feedback, chosen objective, useful news and ambient humour in that order. Merge related item messages and defer optional chatter during important dialogue. Preserve the player's audio/caption choices.
- **Hooks:** item feed, `PixelHud`, `NpcVoice`, `RadioNetwork`, hints and the proposed Easter egg scheduler. Avoid a monolithic event framework until the existing channels demonstrate a shared need.
- **Evidence:** build a busy arrival scene with engine trouble, station completion and radio chatter; capture what remains legible and audible. Pair Q-stage assertions with short fresh-player sessions: can players explain their next action, the cause of a setback and a thing they want to try next? Automation measures reliability; those answers guide enjoyment work.

## Recommended sequence

| Slice | Select from this backlog | Playable outcome |
|---|---|---|
| 1: understand and recover | SG-01, SG-04, SG-07, SG-11, SG-30; scope SG-03/12 recovery paths | A new player can identify the problem, choose an action and continue after a mistake |
| 2: go somewhere and come home | SG-02, SG-05, SG-06, SG-21 | One ordinary expedition feels purposeful and leaves time for something enjoyable at home |
| 3: trust possessions and production | SG-22 before deeper fluids; SG-20 and SG-25 | Stored goods, travelling supplies and unattended care behave predictably |
| 4: a town worth revisiting | Pick SG-13/14/15 plus SG-17/18/26 | A delivery or repair visibly changes a place and somebody remembers it |
| 5: lasting player identity | Pick SG-09/10/27/28, then SG-16/29 if foundations hold | The player develops a preferred role and a garage with a history |

Ship a small selection of [Easter eggs](EASTEREGGS.md) alongside these slices. Use discoveries as texture during ordinary activity; do not make optional humour carry the burden of basic progression or retention.

## Promotion rule

Before moving an item into the main roadmap, name its smallest playable scope, current implementation overlap, resource costs, player-facing feedback, failure/recovery path, persistence and multiplayer authority. Add a relevant Q-stage scenario and a W-stage coherence check. Validate through real input where the claim concerns usability, and require captures for presentation claims. Do not tick implementation or test completion based on this proposal.

For experience review, sample story and sandbox, standard and scarce supplies, keyboard and pad, pixel and HD, solo and supported co-op. Measure excessive repeated actions and interrupted travel, but preserve room for deliberate repair, uncertainty and quiet discovery. Keep findings separate from assumptions and record what players actually tried.
