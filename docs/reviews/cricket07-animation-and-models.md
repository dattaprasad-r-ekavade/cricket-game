# Cricket 07 animation and model structure

14 October 2026. These are research notes on how Cricket 07 organises its character models, shot, catch and stumping animations, and the data that ties animation to gameplay. Companion to [cricket07-data-analysis.md](cricket07-data-analysis.md).

**Rules, as before:** read-only inspection of the owner's install. Nothing was extracted to disk or copied into this repository. Names and counts are cited to describe *structure*; our assets, names and values must be our own.

## Formats at a glance

| What | Where | Format | Notes |
| --- | --- | --- | --- |
| Skeleton | `!skel.o` (with `!eagl`) | EA "EAGL" asset compiled to an **ELF relocatable object**; symbols name each bone | One shared skeleton for every human character |
| Body, kit and props | about 79 top-level `.o` files in `data.gob` | Same ELF format | Role-specific bodies with LOD levels and variations |
| Faces | 348 small archives of `model.o` + `textures.fsh` | ELF model + EA `.fsh` texture | One head per real player |
| Animation clips | `Data/motion/moai.viv` (335 entries, 2.6 MB) | EA BIGF archive of compressed, bit-packed clips with short codes (`SH…`, `FF…`, `KA…`) | Per-channel quantization; roughly 27 animated body bones |
| Animation database | one 321 KB `.bin` in `data.gob` | Binary table | **775 named states** mapped to clip codes, plus hand and face pose clips |
| Stroke tuning | 91 `.saf` (plain text) → 830 `ShotAttributes` XML | Text authoring source → compiled XML | Timing zones tied to **animation frame numbers** |
| Per-clip helper tracks | 438 `.bhf` | Small binary keyframe tracks | Position and rotation keys near contact frames |
| Wicket breaks | 14 `.bvh` | Standard BVH, 25 fps, 35–57 frames | Pre-baked stump and bail break variations |
| Cutscenes | `Data/motion/script.big` (1,255 `.viv`) | Scripted sequences | Fall of wicket, celebrations, umpire signals, crowd |
| Effects | `.jdi` particle definitions | Text-like | Ball splash, foot splash, sun flare |
| Ground physics | `field_physics.cfg` (text) | Per pitch type | Hardness, velocity and spin transfer, friction |

## Skeleton: one rig for everyone

51 bones (from symbol names):

- **Body:** Hips; Lower, Middle and Upper Spine; Neck; Head; left and right collarbone, arm, forearm, hand; left and right leg, shin, foot, toe.
- **Twist bones:** an arm-twist and a forearm-twist bone per side. This is what keeps wrists and elbows from collapsing during big shot rotations.
- **Fingers:** thumb 1, index 1–2 and middle 1–2 per hand (ring and little fingers are presumably skinned to these). Enough to wrap a bat handle and form catching hands.
- **Face:** jaw, upper and lower lips (with corners), brows, lids, nose, eye. These drive expression poses.
- **No bat bone.** The bat (`GameFaceBat`, shape `batt`) is a separate model attached to the hands.

**Lesson for us:** our 13-bone rig is the root of the "toy" look. The C1 target (55–65 bones with fingers and twist bones) matches what Cricket 07 shipped in 2006. Twist bones are the single biggest deformation win for batting.

## Models: bodies by role, heads by player

- **Role bodies:** `CricketBatsman`, `CricketBowler`, `CricketKeeper`, `CricketUmpire`, plus `CricketAccessories` (helmet and accessories). Each comes in **High, Med and Low LOD** with **35 variations** (body builds or proportions).
- **Named parts inside each body:** `kit1`/`kit2`/`kit3` (shirt, trousers, pads/gloves), `helm`, `numb` (shirt number), `name` (name on back), `batt` (bat) and `skin`. Team kits are texture or colour swaps on shared geometry; the number and name are separate parts so they can be textured per player.
- **Heads:** 348 per-player face models, plus a generic high-detail face and **14 hair meshes**. The face shader uses specular, **subsurface**, rim light and Fresnel terms, so skin got the most shading effort.
- **Shadows:** low-poly **shadow proxy bodies**, one per role and lighting setup (sun or night), skinned to the same skeleton (`ShadowBatsmanSun` etc.). This is cheaper than casting from the full mesh.
- **Props:**
  - the ball and its shadow;
  - stumps and bails, including single-stump variants, separate bail models and a stump shadow;
  - pitch wear decals (3 variations), mowing and "detail" grass layers, crease strip models;
  - markers for the ball, bowling target, player and fielding disc;
  - the scoreboard with per-digit shapes, sky domes, flags with their own small animation bank, clock hands, and an LBW visualisation.
- **Stadiums:** 71 stadium packs, each holding `Stadium.o`, `Crowd.fsh` and textures, a clock and a `.collision` mesh.

**Lesson for us:**

- Separate the *body* (shared, LOD, a few builds), the *head* (per player or generated) and the *kit* (texture parts). This scales to many players with a small modelling budget.
- Use cheap skinned shadow proxies before full shadow maps.
- Keep stumps and bails as separate rigid pieces driven by baked break clips.

## Animation organisation

### State vocabulary (775 named states)

| Family | Count | Examples |
| --- | --- | --- |
| Catching | 138 | height × approach grid (below) |
| Fielding (`FIELD_…`) | 77 | `FIELD_BOUNDARY_SLIDE_90_THROW`, `FIELD_DIVE_MEDIUM_LOW_THROW`, `FIELD_FORWARDS_RUNNING_ONEHAND_THROW_45`, `FIELD_STRAIGHT_LONGBARRIER` |
| Keeper | 64 | stance per bowler type, catch grid, dives, move-to-stumps, appeals, celebrations |
| Locomotion phases | 63 | `PHASE_FORWARD_RUN/SPRINT`, `PHASE_LEFT/RIGHT/BACK…`, walk variants, `_PMG` motion groups |
| Batting (strokes) | about 110 incl. variants | see below |
| Batter (non-stroke) | 48 | stance, tapping, wait loops, run start, turn, dive, run end, hit by ball (body, hand, head), backing up per bowler type |
| Throws | 33 | `THROW`, `THROW_180(_QUICK)`, chase-and-throw at 45/90/135/180° |
| Bowling | about 90 | fast, medium, off-spin and leg-spin × left/right arm × over/round, each with *bowled*, *caught*, *LBW appeal*, *near miss* endings and *run to stumps* at 0/90/180/270° |
| Misses (fielding) | 15 | dive miss low/medium (with mirrors), jump miss, slide, "watch it go over head / past side" |
| Umpire | about 8 | stand, watch loop, duck, hop, no-ball signal, run left/right |
| Slips | about 12 | stance per bowler type, watch ball, appeals |

Most clips are authored **once per side and mirrored** (the `_MIRROR` and `_LEFT/_RIGHT` pairs), and many have 2–3 numbered variants to avoid visible repetition.

Separate **hand-pose** clips (closed, open, relaxed, pointing, half, shaking, holding the ball…) and **face-pose** clips (default, "yeah", "woo", annoyed…) are layered on top of the body. This lets one body clip serve many situations.

### Batting strokes

- **Families:** front foot (default), back foot (`BACKFOOT_…`), and advance down the pitch (`ADVANCE_…`). These match the three batting buttons.
- **Strokes:** cover, straight, on and square drives (plus "punched" variants), square cut (and straight-bat version), late cut, pull, hook, hook-ducking, sweep, paddle sweep, reverse sweep, leg glance (push and "CPU" variants), steer, and full-swing back-foot variants.
- **Defence:** forward defensive split by **length and line** (full/short × off/straight/leg), back-foot defensive (off, straight, fend, against spin) and a generic block.
- **Leaves:** duck, sway, shoulder arms, hide bat, front-foot leave.
- **Six variants:** `6HIT_…` and `ADVANCE_6HIT_…` copies of the main drives, pull, hook, sweep and glance.
- **Outcome clips:** `HIT_…` versions of the main strokes (follow-through after a hit) and mishits 1–5 for bad zones.

**The link from animation to gameplay** (from the `.saf` sources): each stroke file lists, for both Normal and Six modes, five keyframes (Start, Early, Ideal, Late, End). Each keyframe gives an **animation frame number** (for example frames 27–29 for an aggressive drive, or 29, 32, 36, 39 for a defensive block) plus direction, direction range, vertical angle and power.

So the timing zone you land in decides **which frame of the swing meets the ball**, and that frame's authored direction and power become the shot. Animation and outcome can't disagree because they come from the same table.

### Catching (grid selection)

Catch clips are organised as **ball height at the fielder** × **how the fielder must move**:

- **Heights:** knees, waist, chest, above head.
- **Approach:** easy/standing, side, 90° steps, step back, run back, forwards (short and long), jump, skier, and dives (forwards; medium-height dives with mirrored variants).
- **Close catchers and keepers** have their own sets: close-in dive forward or side, catch at face, flinch, hide head; keeper straight, side, wide and spinner catches by height, plus dives (low/medium/high × close/medium/long).

The game predicts where and when the ball reaches the fielder (the executable has `ShotPredictor` and a keeper `CSharedPredictor`), picks the cell for that height and direction, and times the clip so its "secure" frame meets the ball.

### Stumping and run-outs (composed, not single clips)

- **Keeper stumping:** `KEEPER_MOVE_TO_STUMPS_…` (four directions: 0/90/180/270°, separate for spinners), then `KEEPER_AT_STUMPS_CATCH_{LOW|WAIST|CHEST|ABOVE_HEAD}` and its `_HIT` twin, which gathers *and* breaks the wicket. The `_HIT` variant is chosen when a stumping or run-out is on.
- **Bowler's end:** each bowling style has *run to stumps* in four directions, then `BOWLER_AT_STUMPS_CROUCH_CATCH_{height}` and `_HIT` (gather and break).
- **Fielder direct actions:** `FIELD_BREAK_STUMPS_CLOSE/RUNNING` (left and right).
- **Stumps:** the break itself is one of 14 **pre-baked BVH break animations** (2 bails + 3 stumps), not live physics.
- **Batter:** `BATSMAN_RUN_DIVE` / `RUN_END` / `RUN_TURN(_SHORT)` for the grounding side of a close run-out.

### Locomotion and turns

Fielders and runners use **phase-matched motion groups**: walk, jog, run and sprint in forward, back, left and right directions, with explicit turn clips (90/135/180°, left and right) and transitions (backpedal ↔ jog). Combined with "chase and throw at angle" clips, a fielder can run, turn, pick up and throw in one connected motion.

### Per-clip helper tracks (`.bhf`)

Each stroke, and several running and cutscene clips, has a small `.bhf` file: the clip name followed by keyed records (frame, type, values). Type 2 holds three floats (a position) and type 3 holds four floats (a rotation quaternion). The keys cluster around the contact frames (for example frames 26–56 for a cover drive).

**Inference:** these are bat or contact helper paths. They let the runtime know where the bat is at each frame, so it can nudge the ball trajectory (`MaxTrajAdjust`) and the batter's position (`MaxPosAdjust`) for visually exact contact without physics. Both nudges shrink as difficulty rises (0.3 → 0.1 and 0.65 → 0.25 in the tuning files).

### Presentation clips

The 1,255 `script.big` sequences reuse the same rig: fall-of-wicket walks per stadium, batter celebrations (fifty, century, win), annoyance and LBW-unhappy reactions, umpire signals and talking non-strikers. Many have per-stadium versions so walk-off paths match each ground.

## What this means for Super Cricket

1. **Rig (C1):** a single humanoid rig of about 50–65 bones with **twist bones and finger bones** for all roles, and the bat as a separate prop parented to the hands with IK. Add face bones only if faces will be seen close up.
2. **Clips are data-addressed states, not code.** Keep a state table (name → clip, mirror flag, variants, events) in Content, validated like our other assets. Author one side and mirror.
3. **Tie strokes to frames.** Each stroke's five timing zones should name the clip frame that meets the ball and the outcome at that frame (our A5.1 and A5.2). Keep a small per-clip contact helper track (bat sweet-spot position per frame) and use it, rather than physics, to align the ball. Allow a bounded, difficulty-scaled nudge to ball and batter.
4. **Catching as a grid.** Start with 4 heights × 4 approaches (standing, side step, run forward, dive), mirrored. Choose the cell from the predicted interception and time the clip's secure frame to the ball. This is far fewer clips than Cricket 07's 138 and still readable.
5. **Stumpings and run-outs as composition:** move-to-stumps (4 directions) + gather-at-height + break, with the stump break played from a few baked variations.
6. **Locomotion:** directional run/sprint motion groups with turn clips, before adding more catch variety. Fielders that turn and throw convincingly sell the scene more than extra catch clips.
7. **Layered hands and faces:** additive hand poses (grip, open, point, catch) give a lot of variety for little effort.
8. **Models:** shared role bodies (3 LODs, a few builds) + swappable heads + kit texture parts, and skinned low-poly shadow proxies.

## Evidence and limits

- Model, skeleton and state names come from ELF symbol tables and the animation database's string table in the installed `Data/data.gob`. Clip encodings in `moai.viv` were identified only to the level of "compressed, quantized per-channel"; they were not decoded.
- `.saf` field values are fixed-point. Only frame numbers and structure are interpreted here.
- The `.bhf` meaning is inferred from structure and naming.
- Executable references: `ShotPredictor.cpp` and `CSharedPredictor.cpp` source-path strings; catch and throw state names in the string table (rea search evidence `ev_ac958da9…` and earlier string scans).
