# Human clips: 2026-10 revamp (data side)

Source: `tools/blender/hd/character/clips_life.py` (new and remodelled clips), `clipkit.py` (IK, footstep gait synth,
dense 30 fps clips, checks), merged into `clips.CLIPS`. Export: `python3 tools/blender/hd/character/export_clips.py
Assets/MadMax/Resources/CharacterAnims [--clips a,b] [--check] [--strip strips] [--sheet strips/sheet.png]`.
Every old clip name still exists. Filmstrips: `strips/<clip>.png` (side / back / top rows, root motion applied, car props
drawn), contact sheet `strips/sheet.png`.

## JSON extras (HumanClip.Parse ignores them today; read them in the runtime)
All per-frame arrays have `frames` entries (frame f is at t = f / (frames - 1) for one-shots, f / frames for loops).

| field | meaning |
|---|---|
| `rootX`, `rootZ` | root motion in the clip's **start** root frame, metres for height 1 (scale by `h = pelvisBase / 0.94`), +z = start facing. The pelvis offsets are relative to this moving root. Apply: `root = start * (rootX, 0, rootZ)` each frame. Absent = no root motion. |
| `rootYaw` | root yaw in degrees (+ = turn right), same frame. Present whenever rootX/Z are. |
| `door` | 0..1 of the door's open angle (game `DoorOpen` = -68 deg) to drive the door from the clip so the hand stays on it. |
| `ground` | car clips only: 1 while the feet carry the body on the ground outside, 0 once seated. Vertical correction for a vehicle whose seat height differs from the assumed one: `root.y -= (hsActual - hsAssumed) * ground[f]` (hs = seat hip height above the ground: car 0.52 m, `_high` 1.45 m, both × h). |
| `eventNames`, `eventTimes` | parallel arrays, t 0..1: `stepL/stepR` (heel / foot contact: footstep sounds, dust), `door_open`, `door_shut`, `seated`, `standing`, `legs_in`, `lying`, `bite`, `sip`, `swallow`, `wipe`, `point`, `covered`, `hold`/`release`, `knees`, `down`, `elbowL/R`, `kneeL/R`. |

`clips_index.json` entries gain `events` ("name@t" strings) and `root: true` where present.

Mirroring: the runtime has no mirror support, so mirrored clips are exported explicitly (`_r`, `startle_l`, `turn_r90`).

## Gait stride (runtime note)
Gait clips are geometrically planted: one cycle covers `speed × length` metres (walk 1.40, run 2.81, sprint 3.48,
crouch_walk 0.95, tired_walk 1.155, crawl 0.66). `HumanAnimator.Locomotion` currently advances the phase with
`stride = lerp(1.35, 2.3, run)`, which over-cycles run/sprint (feet skate). Use the dominant clip's `speed * length`
as the stride (blend by the clip weights) and the feet stay planted. All gait cycles start at the left heel (or left
elbow for crawl) at u = 0, right at u = 0.5.

## Clips
Masks: full / upper (chest, head, arms) / lower / arms / arm_r. "Planted" = feet held in the world by IK (slide ≈ 0;
`--check` reports it).

### Locomotion
| name | len s | loop | mask | events | placement / notes | trigger |
|---|---|---|---|---|---|---|
| walk | 1.0 | y | full | stepL 0, stepR .5 | gait, speed 1.4; pelvis yaw/tilt/sway, counter-rotating chest, head stabilised, elbow lag | moving 0.15–2 m/s |
| run | 0.72 | y | full | steps | gait 3.9 m/s, flight phase, fists, elbows ~85° | 2–4.6 m/s |
| sprint | 0.6 | y | full | steps | gait 5.8 m/s, forefoot strike, 17° lean | > 4.6 m/s |
| tired_walk | 1.05 | y | full | steps | gait 1.1 m/s, slumped, low arm swing, heavy hip drop | low stamina / heavy load / hurt |
| crouch_walk | 0.95 | y | full | steps | gait 1.0 m/s, pelvis -0.27 (matches crouch_idle) | crouch + move |
| crouch_idle | 4.0 | y | full | – | pelvis -0.27, looks each way, breathing | crouch still |
| walk_start | 1.0 | n | full | stepR .5, stepL 1 | root motion: rootZ 0 → 1.4 m/s (0.66 m). First frame = idle stance; last frame = walk u = 0 (continue walk at u = 0). | idle → walk |
| walk_stop | 0.75 | n | full | stepL 0, stepR .59 | starts at walk u = 0; rootZ 1.4 m/s → 0 (0.43 m); ends in the idle stance | walk → stop |
| run_stop | 0.75 | n | full | steps | starts at run u = 0; rootZ 3.9 m/s → 0 (0.94 m); braking step, knees absorb | run/sprint → stop |
| turn_l90 / turn_r90 | 0.75 | n | full | steps | **root yaw is not in the bones**: the runtime turns the root by `rootYaw` (smoothstep 0 → -90 / +90 over the clip; use the array). Root position fixed; feet step and stay planted in the world; head leads, chest then pelvis follow. | standing turn > 60° |
| tired | 2.4 | y | full | – | hands on knees, bent over, 3 heaving breaths per loop, feet planted wide | stamina empty while still |

### Prone
| name | len | loop | mask | events | placement | trigger |
|---|---|---|---|---|---|---|
| go_prone | 1.1 | n | full | knees .41, down .86 | root fixed; ends with pelvis offset (0, -0.82, +0.25): hips 0.25 m ahead of the root, head ~1 m ahead, feet ~0.6 m behind | prone key |
| prone_idle | 5.0 | y | full | – | same layout; on the forearms, head up, breathing, glances | prone still |
| crawl | 1.2 | y | full | elbowL .32, kneeR .37, elbowR .82, kneeL .87 | gait, speed 0.55; leopard crawl (forearms + inner knees, hips wriggle); same pelvis layout | prone + move |
| prone_up | 1.2 | n | full | standing .83 | from prone_idle layout to standing at the root (pelvis offset 0) | leave prone |

### Ground sit / lie
| name | len | loop | mask | events | placement | trigger |
|---|---|---|---|---|---|---|
| sit_ground_down | 1.4 | n | full | seated .6 | root fixed; feet stay where they stood, the seat lands 0.45 m behind (pelvis (0,-0.8,-0.45)); right hand to the ground | sit on ground |
| sit_ground | 9.0 | y | full | – | same layout; knees up, forearms across the knees, look round, hands rubbed | sitting on ground |
| sit_ground_up | 1.5 | n | full | standing .8 | back to standing at the root; right hand pushes off | leave |
| lie_down | 2.4 | n | full | seated .4, lying .83 | via sitting; **root motion rootZ 0 → -0.45** so the end equals lie (pelvis (0,-0.8,0)) | lie on ground / sleep rough |
| lie | 6.0 | y | full | – | remodel: arms resting on the ground, breathing, right knee drawn up and back; layout unchanged (head toward -z) | lying (bed or ground) |
| lie_side | 5.0 | y | full | – | same layout, on the right side, knees drawn up, right arm under the head | sleeping variant |
| lie_up | 2.4 | n | full | seated .4, standing .88 | sit up, knees up, push off, stand; **rootZ 0 → +0.45** (stands over the feet) | wake / get up |
| lie_back | 0.8 | n | full | – | unchanged contract (bed edge → lying, PlayerCharacter.Transit compensates); now starts at the new sit pose and ends at the new lie | bed |

### Chair, driving
| name | len | loop | mask | placement | trigger |
|---|---|---|---|---|---|
| sit | 9.0 | y | full | pelvis 0 = seat hip; settled back, hands on the thighs, weight shift, looks, foot moves | lounging seat, passenger |
| sit_down / stand_up | 0.8 / 0.85 | n | full | **pelvis offset stays 0** (Transit moves the root standing spot ↔ seat as before); arms out for balance down, hands push on the thighs up | chairs, benches |
| sit_drive | 7.0 | y | full | hands held on the wheel at 10 and 2 (wheel centre seat-frame (0, 1.34, 0.47), r 0.19, tilted 25°; hands pinned per frame), breathing, mirror and gauge glances. The runtime's steering Post rotations still add on top. | driving |

### Vehicles (seat frame)
Seat frame = the root where sit_drive puts it (seat hip − 0.94·h, facing the car's forward); pelvis offset (0,0,0) =
seated. Driver door on the **left** (-x). Clips are authored with the root **fixed** at the seat root; the pelvis
offsets carry the whole path (no rootX/Z).

Car (`car_*`): hs = 0.52 m (ground at y +0.42 in the seat frame), sill top 0.74 at x -0.36, door-frame top 1.80,
door hinge (x -0.36, z +0.55), door 0.95 m, open = 68°.
- **Start / end standing spot**: seat frame **(x -0.60, z -0.45)**, facing forward, feet on the ground (this is behind
  the open door's sweep, beside the door's rear edge). Walk the player there, then play.
- **Seated end/start**: exactly sit_drive's first frame (driver) or sit's first frame (`_r`, passenger).

Truck / bus (`_high`): hs = 1.45 m (ground y -0.51), step top 0.5 m over the ground at x -0.66, cab floor 0.91 m over
the ground, grab bars at x -0.45, z +0.36 / -0.38. **Start / end standing spot: seat frame (x -0.90, z 0)** facing
forward.

| name | len | mask | door | ground | events | content |
|---|---|---|---|---|---|---|
| car_open_door | 1.4 | upper | 0 → 1 | – | door_open .21 | **standing frame** (root at the start spot): right hand pulls the handle (standing-frame (0.22, 0.95, 0.17)) out to ~34°, left hand pushes it wide; over idle legs |
| car_enter | 2.4 | full | 1 | 1 → 0 | stepR, stepL, seated .55, legs_in .83 | turn the back to the seat (feet step round, planted), right hand on the door's window frame, left on the B-pillar, sit onto the seat edge, legs swing in over the sill under a ducked head, hands to the wheel |
| car_close_door | 1.4 | upper | 1 → 0 | – | door_shut .61 | seated: lean out, left hand on the inner pull, swing it shut, back to the wheel |
| car_exit | 2.5 | full | 0 → 1 | 0 → 1 | door_open .13, steps, standing .69 | left hand opens and shoves the door, knees up, legs out over the sill, feet down, hands on the frame / pillar, stand, step round to face forward at the start spot |
| car_enter_high | 2.8 | full | 1 | 1 → 0 | steps, seated .86 | turn to the cab, both hands on the bars, left foot on the step, push up, right foot to the cab floor, duck in, turn and sit |
| car_exit_high | 2.8 | full | 0 → 1 | 0 → 1 | door_open, steps | turn the back to the door, hands on the bars, climb down backwards onto the step and the ground, turn forward |
| `<each>_r` | same | same | same | same | L/R swapped | passenger side (+x door), mirrored; seated poses are sit (hands on thighs) instead of sit_drive |

Runtime alignment: `root = seatRoot` (position + car rotation) for the whole clip, `+ up * -(hsActual - hsAssumed) *
ground[f]`; doors: `angle = DoorOpen * door[f]`. car_open_door / car_close_door are upper-mask overlays (play over
idle legs / sit_drive). Expect the player to walk to the start spot before car_enter (a walk_stop there reads well).

### Idles and reactions
| name | len | loop | mask | events | content / trigger |
|---|---|---|---|---|---|
| idle | 4.0 | y | full | – | remodel: one slow breath, few-mm weight drift, head settling; feet planted |
| idle_look | 6.0 | y | full | – | remodel: head leads, chest and weight follow, glance back over the other shoulder |
| idle_shift | 7.0 | y | full | – | unchanged |
| idle_stretch / idle_neck / idle_hips | 6/5/6 | y | full | – | unchanged (play occasionally with empty hands) |
| idle_scratch | 2.6 | n | upper | – | right hand scratches the back of the head |
| idle_wipe_brow | 2.4 | n | upper | wipe .3 | heat: wrist across the brow, flick, sigh |
| idle_check_pack | 2.6 | n | upper | – | thumbs under the straps, shrug, tug (with a pack) |
| idle_shiver | 3.0 | y | full | steps | cold: arms wrapped, hunched, 9 Hz tremor, stepping on the spot |
| startle / startle_l | 1.2 | n | upper | – | jolt (shoulders up, hands up) then turn toward the noise on the right / left |
| shield_eyes | 2.2 | n | upper | hold .3, release .75 | dust / low sun: right hand flat over the brow, scanning |
| point | 1.6 | n | upper | point .28 | right arm points ahead (the root / look-at aims it) |
| cover_head | 1.6 | n | upper | covered .14 | explosion / gunfire: hunch, forearms over the head, peek |
| eat | 2.4 | n | upper | bite .17, .53 | remodel: two bites, chewing nods (also fine seated: upper mask) |
| drink | 2.2 | n | upper | sip .34, swallow .44 .6 | remodel: bottle up, head tips back, two gulps |

(eat / drink got longer: 1.0 → 2.4 / 2.2 s. `WastelandGame.UseAnim` drives them by `actionT`, so its timing decides.)

## Checks (`export_clips.py --check`, all clips)
- Foot slide (heel / toe within 6 mm of the floor, root motion applied): gaits and transitions ≤ 0.05 m/s (walk 0.04,
  run/sprint/crouch_walk 0.00, starts/stops/turns ≤ 0.05). lie_down / lie_up / crawl report > 1 m/s: legs that
  stretch or drag while lying (intended), not planted steps.
- Floor: lying / prone bodies within -2 cm (prone_idle -2.1, lie_side -0.5); go_prone / prone_up / crawl -4..-6 cm
  for a few frames (thigh / upper-arm capsule while the body lowers).
- Arm clearance (2.5 cm clothes margin at the stocky build w 1.2): standing clips ≤ 1.2 cm except contacts by design:
  hands on knees (tired 3.2), thumbs on straps (idle_check_pack 3.1), hugging (idle_shiver 5.8), hands on the door
  frame / thighs in car clips (≤ 4.7; passenger `_r` up to 10 while the lap hands meet the rising knees),
  lie_side top arm over the chest (11), sit_ground_down 8.
- Leg overlap: 0 everywhere (car_exit_high 0.3 cm).
