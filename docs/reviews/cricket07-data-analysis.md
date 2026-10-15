# Cricket 07 data analysis: how its mechanics are built

13 October 2026. This is a structural analysis of the owner's installed PC copy at `C:\Program Files (x86)\EA SPORTS\EA SPORTS(TM) Cricket 07`. It extends [cricket07-research.md](cricket07-research.md), which relied on public sources and could not see inside the packed archives.

**Scope and rules.** We study ideas, structure, and tuning *shape* in order to understand why our game is hard to learn. No Cricket 07 assets, code, data files, audio, names, or branding enter this repository. Archives were read in place by a throwaway inspector in the session scratchpad; nothing was extracted to disk. The numbers below are summaries (ranges, medians, counts) used as design reference. Our values must be tuned against our own game.

## How the data is stored

| Archive | Contents |
| --- | --- |
| `Data/data.gob` (341 MB) | EA `BIGF` archive with MD5-hashed entry names; most entries are RefPack-compressed (`10 FB`). Includes 1,122 XML tuning files, 1,653 textures, 471 nested archives, 14 BVH files, models, and UI screens. |
| `Data/motion/script.big` | 1,255 named between-ball presentation sequences (cutscenes). |
| `Audio/pbp.big` | 8,161 commentary clips (play-by-play). `aems.big` and `ambient.big` hold effects and crowd beds. |
| `Cricket07.exe` | Game code. Its string table names subsystems such as `batsman\Stroke\ShotPredictor.cpp` and `keeper\CSharedPredictor.cpp`, the shot zones, and tuning keys. |

Decoded XML includes 830 `ShotAttributes` files, 210 fielding layouts, and the delivery, threshold, adjustment, replay, rumble, and fielding-peg tables summarised below.

## Finding 1: batting outcomes are authored, not simulated

This is the most important result for us.

Each `ShotAttributes` file describes one stroke variant. It divides the stroke's timing into five ordered **zones**: Start, Early, Ideal, Late, and End. Each zone directly states the outgoing ball:

- `Yaw` ± `YawRange`: horizontal direction in degrees.
- `Pitch`: elevation in degrees.
- `MinPower`–`MaxPower`: launch speed, apparently in m/s.

There is a second full set of zones for the six-hit modifier. Across all 830 shot files:

| Zone | Median elevation | Median max power | Meaning |
| --- | --- | --- | --- |
| Ideal | −3.9° (kept down) | 22.6 | Hardest, safest hit |
| Early | +6.8° | 18.4 | Weaker, lifted, direction shifts |
| Late | +8.6° | 18.7 | Weaker, lifted, typically finer |
| Start / End | +18° / +23° | about 16.6 | Badly mistimed: in the air, catchable |

By classification:

| Class | Count | Ideal max power | Six-hit ideal elevation | Reading |
| --- | --- | --- | --- | --- |
| 0 | 340 | 11.3 | 8.5° | Defensive strokes, AI reward 0 |
| 1 | 140 | 26.6 | 31.5° | Attacking strokes |
| 2 | 350 | 27.8 | 28.1° | Attacking strokes |

Each stroke also carries an AI block: `Difficulty`, `Reward`, `EdgeModifier`, the bowler types it suits, and the ball categories it applies to (`BallStumps`, `BallBatsman`, `BallLength`). The executable logs "Predicting shot %d of %d" and "Here are the actual for a shot with criteria %08x". This suggests a shot predictor that filters strokes by a criteria bitmask (delivery line/length plus player input) and then picks an animation.

**What this means.** The player's single decision, *when* they pressed, maps to a small, readable set of outcomes:

- Good timing keeps the ball on the ground and hits it hard in the intended direction.
- Early or late timing lifts and deflects it.
- Very bad timing skies it.

Animation and outcome agree because the outcome is *chosen*, and the animation plays to it. A physics contact never has to land on the moving bat.

Super Cricket instead sweeps the animated bat against the ball (Step 20) and derives the result from contact position and bat velocity. The result is physically consistent, but the player can't predict it. A small spatial miss gives "no contact", and the same timing can give different outcomes. That is the root of playtest finding 2 ("the batting mechanic isn't understood").

## Finding 2: timing difficulty is a window size, not an accuracy test

- An `Adjustments` table shrinks the **six-hit timing window** by a percentage per difficulty: 10% (Very Easy) to 40% (Very Hard) in limited-overs matches, and 15% to 55% in unlimited.
- The executable references `BatWindowIdealSize` and logs "Shrank ideal zone by %f".
- The loading-screen tips say batter confidence, combined with batting ability, "determines the difficulty of timing a shot". Confidence drops after a miss or a hit on the body.

So difficulty, skill, aggression, and confidence all act on **one value: how wide the Ideal zone is**. Players never see random physics noise.

## Finding 3: direction is 8-way, and height is a threshold

A `Thresholds` table defines eight shot directions: up, up-right, right, down-right, down, down-left, left, up-left. Each direction has a loft height threshold of 0.55–0.65 per pitch type, and the CPU uses one fixed threshold (0.60). Direction input picks a sector; the shot data supplies the exact angle.

## Finding 4: the control model, from the game's own tips

The packed string table includes the loading-screen tips. Paraphrased:

- **Batting:**
  - A direction input plus three shot buttons: **front foot**, **back foot**, and **advance** (dance down the pitch).
  - How long you hold the button sets the power: a tap plays a soft stroke, a full press a hard one.
  - A **six modifier** plus a shot button attempts a six; its timing window is narrower.
  - An optional **timing gauge** sits under the picture-in-picture.
  - Analog and "classic" schemes are both selectable.
- **Bowling:**
  - Choose a delivery, then place a **pitch-point cursor**. One button locks it; another sets it without locking.
  - Before release, the direction input sets how much the ball moves in the air or off the pitch.
  - Left/right before the run-up switches over or around the wicket.
  - Up/down cycles saved field formations without pausing.
  - A **stamina** meter reduces pace control and accuracy.
  - Consistent bowling fills a **special-ball meter**.
- **Fielding:** Semi-Auto mode gives control to the player once a fielder has the ball, with one button to throw to each end.
- **Training nets:** the player sets the bowler type, arm, line (random / outside off / middle / leg / leg side) and length (random / short / medium / full).
- **Options:** picture-in-picture, running assist, radar, bowling marker, fielding marker.
- **Cameras:** batting views are Broadcast, Behind Batsman, and Flip; fielding views are Dynamic, Classic, and Tower.

Front or back foot is an explicit **player choice** (one of three buttons), not something the game decides. This differs from our open plan item A4a, which proposes choosing it automatically.

## Finding 5: deliveries are typed and condition-aware

- **Delivery types.** The `Deliveries` table defines named types, each with:
  - a speed range (m/s);
  - ball "hardness" and "friction" for each pitch type (Damp, Wet, Green, Normal, Hard, Dusty);
  - side-spin and top-spin ranges with defaults.

  Examples are off-break, arm ball, doosra, teesra, leg-break, googly, flipper, top-spinner, zooter, and pace inswing/outswing at three speeds. Spin grows on dusty or hard pitches and shrinks on wet or green ones.
- **Swing and seam.** Executable keys tie swing to sky state (sunny, overcast, cloudy, grey, night) and seam movement to pitch type.
- **Stadium limits.** A weather table limits which pitch, sky and temperature combinations each venue allows.
- **Speed caps.** Individual fast bowlers have a max-speed override.

Variety comes from a **catalogue of named, tagged deliveries** plus conditions, not from free parameters.

## Finding 6: fielding is preset-driven

- **Peg system.** The `FieldingPegs` table defines about 79 named positions on four concentric rings (boundary, deep, inner circle, close catchers). Each has a radar coordinate and a field coordinate.
- **Field layouts.** 210 `fieldingoption` files each place nine fielders with a start sequence.
- **Presets by bowler type.** Executable names show presets grouped by bowler type (pace, off-spin, leg-spin) and intent: aggressive, attack, standard, tight, defend, retreat, and custom. Formations can be cycled during play.
- **Context-specific animations.** Names such as `FIELD_FORWARDS_RUNNING_ONEHAND_THROW_45` and the many keeper catch variants (by height and side, separately for spinners) show fielding animations selected by context.

## Finding 7: feedback is multi-channel and presentation-heavy

- **Rumble.** Separate profiles for `ball_hitbat_ideal`, `_early` and `_late`, bat on pads, on body and on helmet, a skier catch, a ground pickup, breaking the stumps, a dive, and a wicket. Timing quality is *felt*, not only shown.
- **Replays.** These are data scripts (`replaySeqRoot`): match events (bowl, hit, caught), rewind to "bowl − 20 ticks", then named cameras ("Replay Side1/Side2") at half speed. Each dismissal type gets its own scripted replay.
- **Cutscenes.** `script.big` holds 1,255 sequences between balls: fall of wicket (216), dismissals, batter and bowler celebrations, umpire signals (four, six, wide, no-ball, bye, leg-bye, out, third umpire), appeals, captain field changes, near misses, and crowd. This is most of the "broadcast" feel.
- **Commentary.** 8,161 clips, plus an "expert advice" system the executable logs, e.g. "set attacking field because batsman has scored 40 runs" and "bring in some spin".
- **Charts.** Post-ball and innings charts include wagon wheel, spider, worm, Manhattan, partnership, and fall-of-wicket graphs.

## Finding 8: the timing-window model (decompiled, 14 October 2026)

This comes from static Ghidra 12.1.4 analysis via REA of `Cricket07.exe` (SHA-256 `d776dba0…53a2`). It describes behaviour in our own words. No code was copied, and the default tuning values aren't in the executable (they're loaded from data at runtime).

| Evidence | Function | What it shows |
| --- | --- | --- |
| `ev_23258725…` | zone lookup (`0x54f150`) | Each stroke has a table of 7 position ranges, each `[low, high) → zone id`. The lookup returns the zone that contains the given position. |
| `ev_172782f7…` | zone builder (`0x54edd0`) | Builds those 7 ranges from three numbers: a centre, a total window size and an ideal size. |
| `ev_b482eca2…` | zone rescale (`0x54ef90`) | Rebuilds the table with a scaled ideal size and a "batter settling in" curve. |
| `ev_39c78b50…` | ideal-scale function (`0x550fd0`) | Computes the ideal-window scale from mode, difficulty and batter attributes. |
| `ev_97e28223…` | alternate zone mapping (`0x54f1d0`) | Maps a scalar error to a zone. |
| `0x55a6d0` (6.3 KB) | stroke update | Samples the ball, picks the zone, logs "shrank ideal zone by %f", and applies an assist. |
| `ev_c25943ad…` | tuning-key registry (`0x561580`) | Names the curves, including `DiffLevel`, `StrokeDiffLevel`, `BallsFacedLevel`, `BatOrderLevel`, `BatRandomLevel`, `BatWindowCentre`, `BatWindowSize`, `BatWindowIdealSize`, and the edge and CPU parameters. |

**1. Timing is measured by ball position, not elapsed time.** When the shot input is committed, the stroke update copies one coordinate of the ball and looks up which zone range contains it. "Early" therefore means "the ball was still this far away", which automatically scales with delivery pace.

**2. Zone geometry.** For window centre *C*, total window *W* and ideal size *I*, the seven ranges run along the ball's path:

| Zone | Meaning (inferred from the order and the data names) | Range |
| --- | --- | --- |
| 6 | Too early | beyond *C + W/2* |
| 1 | Start | *C + (W+I)/4* … *C + W/2* |
| 2 | Early | *C + I/2* … *C + (W+I)/4* |
| 3 | **Ideal** | *C − I/2* … *C + I/2* |
| 4 | Late | *C − (W+I)/4* … *C − I/2* |
| 5 | End | *C − W/2* … *C − (W+I)/4* |
| 0 | Too late | below *C − W/2* |

The Early and Late bands are each a quarter of the non-ideal part of the window, and so are Start and End, so **shrinking *I* automatically widens the mistimed bands**. A flag mirrors the whole table, probably for handedness or batting direction.

**3. The ideal size *I* is the only difficulty knob, made up of three parts:**

- **Settling-in curve.** A per-batter counter drives it; the thresholds of 8, 20 and 36 suggest balls faced, but that's inferred. Below 8, the ideal size is a fixed base. From 8 to 20 it moves linearly toward 75% of the batter's full ideal size, and from 20 to 36 it moves linearly up to the full size. New batters have a small sweet window that opens up as they get in.
- **Mode and difficulty scale.** This starts at 1.0. For a six-hit attempt it becomes (1.0 or 0.5 − a per-difficulty reduction) × base, which is consistent with the data's `Adjustments` table of 10–55%. An optional ×1.2 applies for one stroke class.
- **Skill bonus.** For each batter attribute relevant to the stroke's category (separate attributes cover different stroke groups, matching the UI's front-foot / back-foot batting strengths), the bonus is (rating − 80) / 2 × 0.01, capped at +0.1 per attribute and +0.2 in total. Ratings below 80 *reduce* the window. The final scale is clamped to at least 0.01.

**4. Assists and alternate paths.**

- **Timing assist.** Under an option flag, Early and Late inputs are promoted to Ideal (zone 2 or 4 → 3).
- **Too early.** An input in zone 6 takes a separate re-plan path and doesn't produce a normal stroke.
- **Alternate scalar mapping** (probably analog or CPU input; inferred): error > 0.5 → too late, 0.2–0.5 → End, 0.1–0.2 → Late, 0–0.1 → Ideal; a non-positive error falls back to the position lookup shifted one zone earlier.

**5. A debug outcome table** names the categories it resolves to: mishits 1–5, normal hits per direction group, late cut, mid-wicket and pull hits in the Ideal zone, and "big hit" variants for the six-hit modifier. Outcomes are selected categories, not physics results.

**6. Resolved follow-ups (14 October 2026).**

*Window defaults.* The tuning lives in plain-text difficulty files (`l_` = limited overs, `u_` = unlimited; `_h` = human batting, `_c` = CPU batting; one file per difficulty) inside a nested archive in `data.gob`. Every file uses **centre 0.0 and window size 14.0**. The human ideal size per difficulty is:

| Difficulty | Ideal size (human) | Ideal share of window | Batter position nudge `MaxPosAdjust` | Ball trajectory nudge `MaxTrajAdjust` |
| --- | --- | --- | --- | --- |
| Very Easy | 10 | 71% | 0.65 | 0.30 |
| Easy | 8 | 57% | 0.45 | 0.30 |
| Normal | 6 | 43% | 0.35 | 0.30 (limited) / 0.20 (unlimited) |
| Hard | 4 | 29% | 0.30 / 0.25 | 0.20 / 0.15 |
| Very Hard | 3 | 21% | 0.30 / 0.25 | 0.20 / 0.10 |

The CPU-batting files invert this: the CPU batter gets an ideal size of 10 on Very Hard and 2 on Very Easy, so a harder setting means a stronger CPU batter. The same files hold weights named `DiffLevel`, `StrokeDiffLevel`, `BallsFacedLevel`, `BatOrderLevel`, `BatRandomLevel` and the `BatEdge*` group (the curve inputs), CPU stroke-selection weights, the bowling cursor size (0.3–0.9), bowler speed and spin multipliers, and seam and swing factors per pitch and sky.

*Units* (inference): the window is measured on the same ball coordinate the stroke code samples, centred on the contact point. A window of 14 with an ideal of 3–10 is consistent with metres along the ball's path. At 35 m/s, Normal's 6-unit ideal is about 170 ms and Very Hard's 3 units about 85 ms. It is not proven that the unit is metres.

*Confidence.* Observed: confidence is a per-batter percentage stored in 6 bits (index 0–62 mapped through a fixed 0–100 table; `ev_ff462208…`, `ev_ccb65c79…`), saved and loaded as `Confidence` in match files (`0x516f90`). The ideal-scale function reads it directly: at anything below 100%, with the option enabled, the base scale drops to 0.75 or 0.8.

*"Eye in" counter.* Partially resolved. The 8/20/36 curve reads a *different* 6-bit per-batter field through the same 0–100 mapping and then through a lookup table that is all zeros in the executable (`ev_b948b31c…`), so it is built at runtime. The tuning key `BallsFacedLevel` and the curve's shape suggest balls faced, but static analysis can't confirm it. Observing it would need the game running, and this copy stalls at its title screen on Windows 11.

**What we take from this** (our design, our numbers):

- Measure timing as *ball distance from the contact plane at input*, not clock time. This makes one window feel the same against slow and fast bowling.
- Build the seven zones from three tunables (centre, window and ideal size) with the quarter-split rule, so one number controls difficulty and the bands stay readable on a timing gauge.
- Ideal size = base × mode factor (loft narrows it) + skill bonus (stroke-relevant rating, capped), × a settling-in curve over the first deliveries a batter faces.
- Treat "too early" and "too late" as distinct, readable results (an early commitment, a beaten batter) rather than a silent "no contact".
- Offer a timing assist for beginners that promotes Early and Late to Ideal.

## Where Super Cricket is failing, mapped to these findings

| Our problem (playtests) | Cricket 07 approach | Gap in our design |
| --- | --- | --- |
| Batting not understood; outcomes feel random | Timing zone → authored direction, elevation, power | Physical bat sweep decides contact; small spatial misses read as "no contact" |
| Hard to learn when to press | One Ideal window whose size depends on difficulty, skill, confidence and aggression; optional gauge | Several sources of variance (contact geometry, bat speed, sweet spot) on top of timing |
| Controls feel arbitrary | Direction + front/back/advance + hold-for-power + six modifier | Chords (S+D, W+D) recorded over 220 ms; foot choice not a first-class input |
| No clear feedback | Rumble by timing zone, replay scripts, cutscenes, umpire signals, commentary, timing gauge | On-screen card and flash only; no replay, no umpire signal, no audio cue by timing |
| Bowling unclear | Named delivery catalogue, lockable pitch cursor, movement input, stamina | Pitch target exists; no movement input or stamina; delivery presets instead of types |
| Field changes | Peg rings plus intent presets per bowler type, cycled live | Radar shows the field but can't change it |

## Recommendations

1. **Make batting outcome-authored, with physics as presentation.** Add a stroke table to our content (our own values) with Start, Early, Ideal, Late and End zones per shot. Each zone gives a direction range, elevation and power band. On input:
   1. Classify the timing zone from the delivery's arrival time.
   2. Pick the zone's outcome, with seeded variation inside the band.
   3. Play the matching clip, aligning the contact frame to the ball with IK or a small ball "magnet".

   Keep the existing ball-flight, fielding and rules simulation for everything after contact. Keep the swept contact only as a debug/analysis check, not as the gate for whether contact happens.
2. **Use one difficulty variable for batting.** The Ideal-window width is set by difficulty, batter rating, an in-match confidence value (drops on misses and body hits, rises with runs) and loft (which narrows it). Show this window on the timing gauge.
3. **Adopt the three-button batting model.** Front foot, back foot, and advance, plus direction, a hold-for-power press, and a loft/six modifier. Drop the chord recorder from the default scheme. Revisit plan item A4a (automatic foot choice) accordingly.
4. **Shot catalogue by ball category.** Tag each authored stroke with suitable lengths (short/good/full/yorker), lines (outside off/at stumps/leg) and foot, then select the clip from (direction, foot, ball category). Start with the planned ≈40 clips.
5. **Feedback channels.** Add rumble and an audio cue per timing zone, umpire signals for boundaries, wides and wickets, and a scripted replay per dismissal type. Our deterministic simulation can rewind to "release − N ticks" with two cameras at half speed.
6. **Bowling.** Replace presets with a typed delivery catalogue that responds to pitch and sky conditions, a lockable pitch cursor (more forgiving than Cricket 07's, per its reviews), a movement input before release, and bowler stamina affecting accuracy.
7. **Fielding.** Define a named peg ring and intent presets (attack/standard/defend) per bowler type, and let the radar cycle them without pausing.
8. **Practice nets.** Configurable line and length, used as the tutorial (open item A4b).

## Method notes (for reproducibility)

- Archive format: EA `BIGF` (big-endian entry table: offset, size, null-terminated name). Compression: EA RefPack (`10 FB` header). Both are publicly documented formats.
- Inspection used read-only scripts in the session scratchpad that printed summaries to the terminal. No game files were modified, extracted, or copied into this repository.
- Executable strings were read as plain text. Finding 8 comes from static decompilation with Ghidra 12.1.4 through REA 6.4.0 (MCP). The Evidence IDs are listed there. Nothing was executed, and no game code was copied into this repository.
- Window defaults, confidence and the counter are resolved or narrowed in Finding 8, section 6. Still open: the exact unit of the ball coordinate (metres inferred), and the exact identity of the runtime-built settling-in counter.
- Animation and model structure: see [cricket07-animation-and-models.md](cricket07-animation-and-models.md).
