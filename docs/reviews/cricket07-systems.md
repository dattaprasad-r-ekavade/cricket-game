# Cricket 07 game systems: design notes

14 October 2026. These notes describe how Cricket 07's gameplay systems are structured: AI, rules, difficulty, players, commentary, cameras and modes. Companion to [cricket07-data-analysis.md](cricket07-data-analysis.md) (batting timing) and [cricket07-animation-and-models.md](cricket07-animation-and-models.md) (rig, clips, models).

**Rules:** read-only study of the owner's install. Tables, numbers and names are cited to describe *how the design works*. Our implementation uses our own data, names and tuning. Sources are the decoded data files in `Data/data.gob`, the executable's string table, and static decompilation through REA/Ghidra (evidence IDs are given where code was read).

## 1. The big picture: a data-driven, outcome-authored sim

Across every system, the same pattern repeats:

1. **Player intent is simple**: a direction, a stroke family and a timing press; or a delivery type, a pitch point and a movement input.
2. **The game classifies the input into a few readable bands**: timing zone, ball category, match phase.
3. **Outcomes come from authored tables** keyed by those bands, with seeded variation inside each band, then scaled by player attributes, difficulty and format.
4. **Presentation follows the authored outcome**: the matching animation frame, rumble, commentary, cutscene and replay script.

Physics exists (ball flight, bounce, swing, spin, pitch wear), but it never decides the *meaning* of an input. Super Cricket inverts this, and that is the core of its learnability problem.

The data is plain text or XML, organised by **format × difficulty × side (human/CPU)**. Designers could tune each format separately (5, 10, 20, 40/45, 50 overs and unlimited).

## 2. Difficulty: many small, format-specific dials

Per difficulty and format there are separate files for the **human batter** (`_h`) and the **CPU batter** (`_c`). Each holds:

| Group | Keys | What it controls |
| --- | --- | --- |
| Timing window | `BatWindowCentre/Size/IdealSize` | Ideal-zone width (human: 10 → 3 on a 14-wide window) |
| Assist | `MaxPosAdjust`, `MaxTrajAdjust` | How far the batter and ball are nudged to meet visually (0.65/0.3 on Very Easy → 0.25/0.1 on Very Hard) |
| Stroke difficulty curves | `DiffLevel`, `StrokeDiffLevel`, `BallsFacedLevel`, `BatOrderLevel`, `BatRandomLevel`, `ScoreBias` | Weights for how stroke difficulty, settling in, batting-order position and randomness affect the window |
| Edges | `BatEdgeDifficulty/Stroke/Average/Order/Bias` | Base edge tendencies |
| CPU stroke choice | `CpuBallBatsman`, `CpuBallStump`, `CpuBallLength`, `CpuBatsmanType`, `CpuReward`, `CpuContactHorz/Vert`, `CpuRandom` | Scoring weights the CPU uses to pick a stroke for a delivery (fit to line, stumps and length; batter style; reward; contact fit; noise) |
| CPU aggression | `CpuDifficulty`, `CpuBatsmanType2`, `CpuTacticsDefend`, `CpuBallsFaced` | Overall CPU batting quality and caution |
| Bowling cursor | `TargetSizeMin/Max` (0.3–0.9) | Size of the pitch-point target (the accuracy bloom) |
| Bowler speeds and spin | `Fast/Normal/Slow`, `SwingQuick/Normal`, `LegSpin*`, `OffSpin*` per pitch type | Speed multipliers and spin strength per pitch |
| Conditions | `BallSeam{Damp,Green,Normal,Hard,Dusty}`, `BallSwing{Sunny,Cloudy,Overcast,Grey,NightD,NightL}` | Seam movement by pitch, swing by sky (overcast is strongest) |

Separate per-format tables add **probability modifiers by timing zone**:

- **Edges** (`edges.xml`): human edges are suppressed heavily in short formats (Very Easy −100%, Easy about −95%, Medium −60%, Hard −30%, Very Hard 0 for 5 and 10 overs) and less in long formats. In Tests the *CPU* batter on easy settings edges **more** (+0.4 at Start, +0.1 at Ideal), so a human bowler can still take wickets.
- **Misses** and **bat-pads**: the same shape (human/CPU × overs × difficulty × zone); all zero in this build, so they're available but unused.
- **Six-hit window reduction:** 10–55% narrower depending on difficulty and format.
- A special case logs that maximum pitch wear on a dusty pitch *guarantees* a bat-pad chance.

**Lesson:** difficulty is not one multiplier. It's a handful of named, per-format dials. The *same mistake* (a late press) is punished differently in a T20 than in a Test, so each format feels right.

## 3. CPU batting AI

Two layers.

**Strategy (pacing files, one per match length).**

- `RunRate` is a target run rate per innings phase. For 50 overs: overall 6, phases 4 → 5 → 6 → 7 → 9 → 12. For 10 overs: overall 10, phases 8 → 9 → 10 → 12 → 15 → 15.
- `StrokeProb[situation][phase 0–6][tactic]` gives *relative* weights for **Defensive / Moderate / Aggressive / Six** strokes. The file header defines 0 = aggressive, 1 = moderate, 2 = defensive; reading the last index as the batter's style and the middle one as the innings phase is an inference from the table's shape.

  For example, in a 50-over game an aggressive batter early on is about 9% defensive, 44% moderate, 34% aggressive and 13% six. In the last phase it's 0% defensive and about 36% sixes. A defensive batter early on plays no aggressive strokes at all.
- The CPU compares its run rate to the phase target (`RunRateDifference`, `RunRateMeetsTarget`, `RequiredRunRate` are named conditions) and shifts between these rows.

**Tactics (per delivery).** The CPU picks a stroke from the 830-entry stroke table. It scores each candidate with the `Cpu*` weights (does the stroke suit this line, stump line and length; does it suit this batter's type; what's the reward), adds a little randomness, and plays it. The stroke's own `Difficulty`/`Reward` tags and `BallStumps/BallBatsman/BallLength` masks make this a lookup, not a search.

**Lesson:** a two-layer batting AI (a phase-based aggression table plus a per-ball stroke fit) is simple, tunable per format and produces believable innings shapes. Our CPU currently decides from chase pressure alone.

## 4. Captaincy, field setting and bowling changes

- **Field presets are a library, not generated.** There are 210 named formations:
  - Tests: 4 bowler types (fast, medium, leg-spin, off-spin) × aggressive 6, attack 6, standard 6, defend 4, retreat 4, tight 4, plus custom.
  - ODIs: 3 types (fast, medium, slow) × open 10, normal 10, defend 10, plus custom.

  Each formation places nine fielders and gives them a starting animation. The human cycles formations with up/down before a ball; a field editor exists for custom fields.
- **Fielding positions** are a peg board of about 79 named positions on four rings (boundary, deep/sweeper, ring, close catchers), each with radar and field coordinates.
- **Captaincy rules of thumb** are visible in the expert-advice code (`0x43b5c0`, `ev_5b538a0a…`). These are the cues the commentary expert gives, and they mirror the AI's heuristics:
  - *Tests, bowling:* attacking field at the start of an innings; attacking once the score exceeds 400 or a batter passes 40; bring on spin after 40 overs (pitch-type gated); swing bowlers after 20 overs in overcast conditions; avoid spin on a green pitch.
  - *Tests, batting:* in the third or fourth innings with a big target (over 250), play for the draw; otherwise take your time.
  - *ODIs:* save your best bowlers when they've used more than 75% of their overs early; bring on spin in the right phase; batting side "pick up the pace" when wickets in hand allow.
- **Bowler management:** stamina (a bowler attribute) degrades pace control and accuracy over a spell. The game tracks `SpellLength`, `NewSpell`, `OversLeftForBowler` and `LastOverForBowler` against bowler over limits.
- **Restrictions:** formats can impose fielding restrictions (a minimum-restriction-overs rule in the field editor) and bowler over limits.

**Lesson:** use a small, named formation library per bowler type and intent, plus a short list of readable captaincy heuristics. That beats an opaque optimiser, and the same heuristics can feed commentary.

## 5. Bowling (human)

From the in-game tips and the tuning files:

- Choose a delivery from the bowler's **repertoire**. Each bowler has a type-specific list; better bowlers have more:
  - pace: standard, out-swinger, in-swinger, leg cutter, off cutter, yorker, bouncer, slower ball or reverse swing;
  - leg-spin: leg break, googly, top-spinner, flipper, zooter, full flight;
  - off-spin: off break, arm ball, doosra, teesra, full flight.
- Place a **pitch-point cursor**. One button locks it; another sets it without locking. The cursor's *size* is the accuracy bloom (`TargetSizeMin/Max`), affected by bowler skill and stamina.
- Before release, the direction input sets the **amount of movement** (swing, seam or spin).
- A **pace meter** during the run-up; overshooting the cap gives a no-ball (from contemporary reviews).
- **Over or around the wicket** with left/right before the run-up.
- A **special-ball meter** fills with consistent bowling and unlocks a special delivery for strong bowlers.
- **Rules flavour:** one bouncer per over in limited overs and two in unlimited; too short is called a wide; foot-fault no-balls give a **free hit** in limited formats; no-ball and wide penalties vary by competition (1 or 2 runs).

**Ball and pitch model:**

- Five pitch types (Hard, Damp, Dusty, Normal, Green). Each has hardness, velocity reduction, spin↔velocity transfer and spin decay (`field_physics.cfg`), plus per-delivery spin ranges and pitch friction.
- **Pitch wear** accumulates and affects spin and bat-pads.
- **Swing** depends on the sky; **seam** depends on the pitch.
- Displayed speed and simulated speed can differ by design (a `MaxFake` mph for the overlay versus a `MaxReal` m/s for the simulation, per fast bowler).

**Lesson:** our bowling should be *repertoire + target + movement + meter*, with an accuracy bloom instead of a twitchy cursor. Conditions (pitch and sky) are cheap levers that make matches feel different.

## 6. Fielding, catching and running

- **Prediction-driven.** A shot predictor and a keeper predictor estimate where and when the ball will arrive (named conditions include `GoingToBeCaught`, `BallGoingToLand`, `DistToPickup`, `HitToFieldPos`). Fielders pick catch, pickup, slide or dive animations from height × approach grids (see the animation notes).
- **Fielding modes:** Automatic, or Semi-Auto, where the human takes over once a fielder has the ball and throws to either end. A direct-hit option (aim at the stumps) is available.
- **Events:** misfields, overthrows, dropped catches (fielder or keeper × high/moderate/low), near run-outs, suicidal run-outs and safe arrivals are all first-class events with commentary and crowd reactions.
- **Running:** a single run button; **Run Assist** and a **Running Display** show whether a run is safe. A third-umpire decision triggers on close run-outs and stumpings (`NeedThirdUmp`), with its own replay script and high and reverse camera angles.

**Lesson:** make run safety visible (an assist indicator), and make the near-misses (a near run-out, a dropped catch, an overthrow) events with feedback. Those moments carry the drama.

## 7. Umpiring and dismissals

Supported dismissals and calls appear as events and replay scripts: bowled, caught (fielder, slip, keeper, caught-and-bowled), LBW (with a not-out appeal path and a "missed LBW" condition), run-out, stumped, and third-umpire decisions. Umpire animations cover signals (four, six, wide, no-ball, bye, leg-bye, out, third umpire), plus ducking and hopping out of the ball's path. Every appeal type has bowler, keeper, slip and close-fielder appeal animations.

## 8. Player model

- **Identity:** type (opening batter, batter, all-rounder, bowler, spin bowler, keeper variants), batting position (opener, middle order, lower order, tailender), style or mentality (aggressive, moderate, defensive), batting hand, bowling style (10 styles), two preferred fielding positions (general, keeper, slip, cover, outfielder, gully, close-in, square leg), bat model, accessories, face morphs (about 30 morph sliders), age, nationality, body type.
- **Batting:** Skill, Concentration, Resilience, Attack, Defence, Offside, Legside, Straight, Front Foot, Back Foot.
- **Strengths:** front-foot shots (square, cover, straight, on drive, leg glance, sweep) and back-foot shots (square cut, back-foot drives, glance off the hips, hook, pull).
- **Weaknesses:** googly, sweeping, short-pitched bowling, hooking, the yorker, the slower ball, off-stump awareness.
- **Bowling:** Accuracy, Discipline, Seam, Swing, Spin, Stamina, plus the delivery repertoire.
- **Fielding:** Arm Strength, Accuracy, Agility, Speed, Catching.
- **Special skill badges:**
  - batting: Defensive Wall, Driver, Slogger, Demon Hooker;
  - bowling: Spin Expert, Partnership Breaker, Strike Bowler, Miser, Trickster, Work Horse;
  - fielding: Bullet Arm, Velcro Hands, Acrobat.
- **Dynamic state:** form, and **confidence** (0–100%, which narrows the timing window below 100%; misses and body blows lower it).

These attributes feed the timing window. The skill bonus is (rating − 80) / 2 % per relevant attribute, capped at +10% each and +20% in total, chosen by the stroke's category. That is how "good at the pull" is felt directly in the controls.

**Lesson:** ratings should map to *named, stroke-specific effects the player can feel* (a wider window on a strength, a narrower one on a weakness), not to opaque global multipliers.

## 9. Commentary engine (and why it feels alive)

- **Event bus:** about 60 game events (`GEVENT_*`): ball hit with bat, pads, helmet or body; bounced; missed; run; turn back; near run-out; safe; caught; dropped; misfield; overthrow; appeal; third umpire; wide; no-ball; new ball; bowling change; declare; rain; end of innings; partnership overlay; auto-replay start and finish; cutscenes; and more. Sound, crowd, rumble and commentary all listen to it.
- **Condition vocabulary:** about 200 named match-state predicates, for example `RunsToWin`, `RequiredRunRate`, `HatTrickBall`, `PartnershipMilestone`, `FirstBoundaryOfInnings`, `LastOverBeforeTea`, `BatsmanOutFirstBall`, `ShortPitched`, `Yorker`, `DivingCatch`, `SuicidalRunout`, `EvenMatch`, `MatchWonInLastOver`.
- **Selection:** each event has candidate lines with AND/OR condition stacks, weights and a **chance %**. A **history** filters out recently used lines, falling back to the full list when everything has been used.
- **Sentences from fragments:** lines are assembled from recorded fragments with slots (`BATSMAN_NAME|`, `RUNS_TO_WIN|`, `REQ_RUN_RATE|`, `PARTNERSHIP_TOTAL|`, `BOWLER_WICKETS|` …), with priority chains, delayed **co-events** (for example the expert following the main commentator), hush and cut-off events when action interrupts, and an expert-analyst layer (the advice rules in §4).
- **Crowd:** regional crowd chant banks, landmark sounds (fifties, centuries, team landmarks), and reactions to drops, appeals and LBW shouts.

**Lesson:** we don't need recorded commentary to borrow this. The same event bus + conditions + weighted, history-filtered line selection can drive on-screen text, crowd audio and the result card now, and voice later.

## 10. Cameras, replays and presentation

- **Camera library:** batting views (Broadcast, Behind Batsman, Flip for left-handers), fielding views (Dynamic, Classic, Tower), batter zoom speed by bowler pace, a stump cam, and picture-in-picture cameras for each bowler run-up (side-on during the approach).
- **Replays:**
  - recorded animation and ball state, replayed with a timeline (slow and fast forward or rewind, first and last frame);
  - **scripted per event**: bowled, caught (fielder or slip), LBW, run-out, four, six, third umpire, each with a no-ball/free-hit (`_NB`) variant;
  - angles chosen by which quadrant the ball went to (4 quadrants × 3 angles).
- **Between-ball cutscenes** (1,255): fall-of-wicket walks per stadium, celebrations, captain field changes, umpire signals, crowd.
- **HUD and analysis screens:** scorebox, partnership overlay, picture-in-picture, timing gauge, radar (field map), run display, and wagon-wheel, spider, worm, Manhattan, heartbeat and partnership graphs.

## 11. Modes and progression

- Play Now, international tours and series (including a full Ashes tour with a success-rate goal and a reward bat), domestic competitions with **real points rules** (bonus points, first-innings points, net run-rate bonus), World Cup style pools and Super Six, knockouts.
- **Scenarios with objectives** (for example "score 50 with this batter before the side is bowled out", "take 3 wickets before they reach 230").
- **Practice nets** with configurable bowler type, arm, line and length, plus an **aim-the-bounce** bowling drill.
- Auto-play to the fall of a wicket or the end of an innings; declarations, the follow-on, sessions (lunch, tea, close of play), rain.
- A Hall of Fame with records and a trophy cabinet, a player creator, a squad manager with form, and Man of the Match / Player of the Series scoring (one wicket ≈ 27 runs, with all-rounder thresholds).
- Tooling note: the schedule generator and audio mixer were reused from HB Studios' rugby game, which shows a shared engine and toolchain.

## 12. Applying this to Super Cricket (prioritised)

| Priority | Take this from Cricket 07 | Our plan item |
| --- | --- | --- |
| 1 | Outcome-authored strokes from timing zones; ball-position timing; one ideal-window dial | A5.1–A5.3 |
| 2 | Per-format, per-side difficulty files (window, assist nudges, edge/miss/bat-pad modifiers by zone) | A5.3 + new tuning files |
| 3 | Three-button batting + loft modifier; stroke selection by ball category | A5.4–A5.5 |
| 4 | Event bus → feedback (text, crowd, rumble now; voice later) with conditions and anti-repeat history | A5.6 + D4 |
| 5 | Scripted replays per dismissal type with a free-hit variant | A5.7 |
| 6 | Practice nets with line/length settings and an aim-the-bounce drill as the tutorial | A5.8 |
| 7 | Bowling: repertoire per bowler type, accuracy bloom, movement input, pace meter, stamina; pitch and sky conditions | A5.9 |
| 8 | Field preset library per bowler type × intent; captaincy heuristics shared with commentary | A5.10 |
| 9 | CPU batting: phase run-rate targets × style-based aggression weights + per-ball stroke fit | E1 |
| 10 | Attributes that map to felt effects (stroke strengths and weaknesses widen or narrow the window), confidence and form | E1 / later |
| 11 | Run assist and running display; near-miss events | A4c follow-up |

## 13. Decompiled follow-ups (14 October 2026)

### Tuning block layout

The parser (`0x563750`, `ev_89ce3f45…`) loads each difficulty file into one block of 62 floats, one block per difficulty (stride 0xF8 at `0x9C6E40`):

| Offsets | Fields |
| --- | --- |
| +0x00–0x28 | stroke-difficulty curves and edge settings |
| +0x2C/+0x30/+0x34 | `BatWindowCentre/Size/IdealSize`, the same offsets the timing code reads, which confirms Finding 8 |
| +0x38/+0x3C | `MaxPosAdjust`, `MaxTrajAdjust` |
| +0x40–0x58 | `CpuBallStump`, `CpuBallBatsman`, `CpuBallLength`, `CpuContactVert`, `CpuContactHorz`, `CpuRandom`, `CpuBatsmanType` |
| +0x5C | `CpuDifficulty` |
| +0x70/+0x74 | `CpuRequest`, `CpuReward` |
| +0x78/+0x7C | `TargetSizeMin/Max` |
| after +0x7C | bowler speed, spin, seam and swing factors |

### CPU stroke scoring (`0x557870`, `ev_fbbbdff8…`)

Each candidate stroke is scored as follows (our paraphrase):

1. **Load the weights** from the difficulty block: ball-to-batter fit, ball-to-stumps fit, length fit, stroke type (× `CpuRequest`), reward (× `CpuRequest`), horizontal contact, vertical contact, random. **Normalise them to sum to 100.** In limited-overs matches the reward weight is folded into the type weight. A fallback uses the defaults 7/7/7/22/22/15/17/3.
2. **Ball-fit terms** are 0–100% matches between the delivery's line-to-batter, line-to-stumps and length categories and the stroke's tags.
3. **Type term:** 1.0 if the stroke's aggression class equals the class wanted this ball (from the pacing table), 0.1 if one class away, else 0. In six-hit mode, any aggressive class scores 1.0.
4. **Reward term:** `(1 − max(0, strokeDifficulty − CpuDifficulty)) × (1 − |strokeReward − wantedReward|)`. Strokes harder than the CPU's skill are discounted.
5. **Random term:** a uniform 0–1 roll × the random weight.
6. **Disabled terms:** the two contact weights are multiplied by **0** in the shipped build.
7. **Score** = Σ weight × term. The highest-scoring stroke is played.

### Bowling accuracy and setup (`0x56c9c0`, `0x568a20`, `0x568af0`, `0x568bc0`, `0x568c10`)

- **Target size:** `TargetSizeMax + (TargetSizeMin − TargetSizeMax) × f`, with f = the bowler's accuracy rating (0–100%) × a ramp. The ramp reaches 1 once a per-bowler counter hits 10, so accuracy improves over the first deliveries of a spell. If this fails, the size falls back to 0.7.
- **Landing scatter:** the actual pitch point is offset from the target by a uniform ±0.25 × S in both line and length. S falls as that per-bowler counter rises: 320 → 240 → 200 → 160 → 120 → 40. **On the two easiest settings the scatter is zero**: the ball lands exactly where aimed.
- **Difficulty-dependent setup values:** a rate of 12 (Very Easy/Easy), 15 (Medium) or 30 (Hard/Very Hard), probably the pace-meter or cursor speed (inference), and an assist strength of 1.0 → 0.6 → 0, also ramped by the same counter.

**Lesson:** bowling accuracy is a *visible* target that shrinks with skill and rhythm, plus a hidden scatter that only exists above Easy. Beginners get exactly what they aim for; experts must earn it.

### Still open

- **LBW decision geometry, resolved 14 October** (the commentary condition `MissedLBW`, evaluator `0x440C60`, `ev_3ec32646…`). An LBW predictor object at `[0x817BC0]` stores where the ball would have passed the stumps: **+0x38 lateral offset** (m, sign flipped for left-handers and by end) and **+0x3C height** (m). The classification is:

  | Result | Condition |
  | --- | --- |
  | Missing off / missing leg | lateral beyond ±0.1375 m (half the stumps' width 0.114 m + ball radius ≈ 0.036 m) |
  | Going over | height > 0.70 m (stump height 0.711 m) |
  | Clipping (the "close call" band) | 0.0825 < \|lateral\| < 0.1375 m and height ≤ 0.70 |
  | Hitting | \|lateral\| ≤ 0.0825 m and height ≤ 0.70 |

  **Correction (15 October):** this predictor is the *commentary's* "how close was it" measure, not proven to be the umpire's decision input. Over the 20-minute memory log it changed only once (22:05:54, a not-out at −0.099 m / 0.45 m). It did **not** change for the out LBW at 21:57 (Boucher lbw b Colbert), and its starting value (−0.092 / 0.16) may be left over from an earlier delivery. The bands above describe how near-misses are classified for commentary; the code that actually gives LBWs out was not identified.

  **Design for us:** compute where the ball would pass the stumps and use three bands: hitting = out, clipping = probabilistic "umpire's call", missing/over = not out. Show the yellow stump-line strip in the replay so the player can see why.
- **Run-out proximity** (`VeryNearRunout`, `0x448B00`): a batter within **2.0 m** of the crease line when the wicket is broken counts as a "very near" run-out, which triggers the near-miss event and commentary. Crease objects sit at world+0x29E0 / +0x2D30 with crease lines at z = ±10.02 m. Engine units are ¾ inch (× 0.01905 = metres).
- **Pace meter input, confirmed by the owner:** press **S** during the stride meter to set pace; pressing too late, at the top of the meter, bowls a **no-ball**.
- **In-match field switching:** the 210 presets and the captaincy cues are known; the switching code isn't yet traced.
- **Pace-meter interaction:** the rate value above is the likely meter speed; not confirmed.

The game does run on this machine. It runs **elevated**, so input injection and memory reads from a normal-privilege process are blocked by Windows (UIPI). Observing these systems live needs either an elevated helper or the owner playing while screenshots are captured.

## 14. Live observation (14 October 2026, game running non-elevated)

The game was run by the owner without elevation, so input and read-only memory access worked. A 10-over exhibition (South Africa v West Indies, Easy) was played to the end of the first over. Frames were captured at about 8 fps.

**Official PC controls (in-game Controller Settings screen):**

- **Batting:** arrow keys choose shot direction and shot selection; **S** and **W** play a shot; **D** advances down the pitch; **A** ducks or leaves; **Shift** is the loft modifier.
- **The Front/Back Foot option defaults to *Auto*.** With Auto, W and S both simply play a shot and the game chooses the foot. With *Manual*, W is back foot and S is front foot. The batting system is "Classic".
- **Bowling:** arrow keys move the bowling cursor. **A** sets the pitch point without locking it. **S** confirms (locks) it. **D** sets a **fake pitch point**: the batter sees the cursor at the fake point, and as the bowler closes in it jumps back to the real one. The decoy is a deliberate mind-game mechanic.

**Gameplay options:** Picture-in-Picture, Running Assist, Batsman Timing Gauge (**off by default**), Radar, Bowling Marker, bowler-speed units and Game Speed. Match setup rolls the pitch, sky and temperature (this game rolled *Wet / Sunny / Warm*).

**Batting HUD during a delivery:**

- **Picture-in-picture**, top left: the bowler's run-up, with a vertical bar beside it.
- **Radar**, top right: fielder dots.
- **Arc gauge**, bottom left: fills during the run-up, with six ball markers for the over.
- Release speed (e.g. "85 MPH") shown in the score strip.
- **Pitch marker:** a blue ring appears at the bowler's delivery stride on the predicted bounce point and **shrinks towards the bounce as the ball arrives**. It's both a "where" cue and a "when" cue for the batter.

**Presentation:** a wicket first ball triggered a celebration cutscene and a fall-of-wicket banner, then an automatic replay from behind the stumps that **draws a yellow strip down the line of the stumps** (consistent with the `LBWMarker` class). The end of the over opens a menu with Change Bowler / Statistics / Options / Save.

**Live memory (read-only):**

- The menu cursor is two floats at `0xA01970` in 640×480 space; this enabled exact menu navigation.
- The difficulty tuning blocks at `0x9C6E40` hold exactly the values decoded from the `.cfg` files. Blocks 0–4 are limited-overs human batting (ideal size 10/8/6/4/3 with `MaxPosAdjust` 0.65/0.45/0.35/0.30/0.30), blocks 5–9 unlimited-overs human, blocks 10+ CPU. This confirms the static analysis.

**Bowling side (controller reassigned to West Indies at the end of an over):**

- **Bowler change** happens at the end of the over from a list showing a **stamina bar** per player. The bowler who just bowled, and the keeper, are greyed out. The tabs are *Bowling Attributes* (Accuracy, Discipline, Seam, Bouncer, Slow Ball, Inswing, Outswing, Stamina, plus the arm and style label) and *Bowling Deliveries*. A 67-accuracy medium pacer had only **Standard + Out Swinger**, which confirms that the repertoire scales with skill.
- **In-match field change:** pressing Up/Down before the run-up opens a preset picker (`Open1` … `Open10`, then further families) that **previews the formation as green dots on the radar** without pausing. S selects, W closes. This answers the "field switching" question for the human captain. The CPU uses the same preset library (§4).
- **Delivery selection by key:** when the ball is ready, a small panel maps each available delivery to a face key (here S = Standard, A = Out Swinger). Pressing the key picks the delivery, locks the pitch point at the cursor and starts the run-up in one step.
- **The pitch-point ring** is drawn on the pitch during the run-up and shrinks to a point by release, the bowler-side view of the accuracy target.
- **Pace meter** (see [cricket07-data-stats.md](cricket07-data-stats.md) §3: the observed 63–65 MPH equals the delivery's minimum + 0.8 × range, the `Normal` speed factor, so the meter most likely picks one of three discrete pace levels): a curved bar at the bottom left appears only in the **delivery stride** (about the last 0.8 s). It fills green → yellow → orange → red and leaves a marker at the level reached. With no input, or S held at various moments, a 67-accuracy medium pacer bowled **63–65 MPH**. The input that actually drives the meter wasn't identified in this session; red at the top is consistent with the reviewed overstep/no-ball rule.
- A slow, unaimed delivery was hit for six the first ball. The fielding camera follows big hits and shows the release speed in the score strip.

**LBW, observed in the owner's own play (recorded at 2 fps, 21:43 session):**

| Step | Not out (frames 604–611) | Out (frames 646–668) |
| --- | --- | --- |
| Delivery | 62 MPH | **86 MPH**, meter driven into the orange/red band (pace meter does change speed; the owner's input achieved it) |
| Cut | Umpire close-up beside the scoreboard, **no signal** (decision = stays still) | Umpire close-up, **raises the finger** (about 1 s after impact) |
| Then | Short replay along the pitch with the **yellow line-of-stumps strip** | Bowler appeal → celebration cutscene → batter walks off → replay with the same yellow strip |
| Score | unchanged | wicket added (18/4 → 18/5) |

- The decision is shown **before** the bowler's celebration: the umpire shot is the reveal, and the appeal animation plays as a reaction.
- The yellow strip (the `LBWMarker` model) lets the player judge "pitching / impact in line" in the replay. This is a lightweight, readable alternative to full ball-tracking.
- Other umpire signals in the same session: wide (arms horizontal, +1 run), boundary four.
- The decision geometry (pitch-in-line / impact / hitting thresholds) remains unread in code. The presentation flow and timing are now confirmed.

**Design corrections from live play:**

- **Front/back foot should be automatic by default**, with manual as an option. This reverses the earlier A5.4 reading of the tips text.
- **The shrinking bounce marker** is a better timing cue than our separate timing bar.
- **The decoy pitch point** is a cheap and distinctive bowling mechanic for human-vs-human play.

## 15. Research status (closed 15 October 2026)

| Question | Status |
| --- | --- |
| Batting timing window, zones, difficulty values | Resolved (decompiled + data + live memory) |
| Confidence and the "eye in" curve | Confidence resolved; the eye-in counter is a runtime table (balls faced, inferred) |
| CPU stroke scoring | Resolved (formula and weights) |
| CPU batting pacing | Resolved (pacing files) |
| Field presets and in-match switching | Resolved (library + live picker) |
| Bowling accuracy and scatter | Resolved (decompiled) |
| Pace meter | Resolved: S in the stride sets pace (three levels); late at the top = no-ball |
| LBW decision | Presentation and commentary near-miss bands resolved; the out/not-out decision code not identified (3 LBW outs + 2 not-outs observed in play) |
| Run-out | Two live run-outs captured with ball tracking (§16); proximity rule (2.0 m "very near") from code |
| Player attributes and distributions | Resolved (1,053 records) |
| Animation, models, commentary, replays, cameras | Resolved at the structural level |

Remaining unknowns are small: the extra test that decides clipping LBWs, and the exact run-out cut-off (batter grounded behind the crease at the moment the bails come off is the presumed rule). Neither blocks implementation.

## 16. Live run-outs (owner's match, 14 October; screen at 2 fps + ball position at about 30 Hz)

| | Run-out 1, 21:54:39 (A. de Villiers, FOW 18/6) | Run-out 2, 22:03:20 (last wicket, 32/9 → all out 33) |
| --- | --- | --- |
| Delivery | ~115 km/h | ~115–120 km/h |
| Shot | Pushed short, ball stopped near the pitch | Mistimed early (PiP showed "EARLY"), lofted towards mid-on at ~65 km/h, landed ~8 m from the bowler's stumps |
| Fielding | Close fielder ~6 m from the striker's stumps, quick flick | Deep fielder ~18 m from the bowler's stumps |
| Throw | 64–74 km/h, **direct hit** on the striker's end (ball at x = 0.00 at the stumps) | 89–104 km/h flat throw, flight 0.7 s, **direct hit** on the bowler's end |
| Bat contact → wicket broken | ~2.7 s | ~5.0 s (one run completed, out going for the second) |
| Presentation | Umpire close-up signal → fielders' celebration → batter walks off with "FOW" banner → replay from square of the batter's dive at the crease | Straight to the fielders' celebration (innings over), then the end-of-innings FOW highlight reel |

**Takeaways:**

- Both run-outs were **direct hits**, from about 6 m and about 18 m. Throw speed scales with distance: short flicks about 70 km/h, outfield throws about 90–100 km/h, consistent with the general hit/throw speed distribution (median 58, p90 101 km/h).
- The game shows close run-outs with a **side-on replay of the dive at the crease**: the same "show why" pattern as the stump-line strip for LBW.
- **The end-of-innings highlight reel** replays every wicket with a caption ("FOW: 18/6 A.de Villiers run out 0"). It's a cheap, satisfying summary feature built on the replay system.
- The batting timing label ("EARLY") appears in the picture-in-picture as the shot is played, so the player learns why the shot went up.

**Design for us:** throws should be fast and flat (70–100 km/h by distance) with a chance of a direct hit that scales with fielder accuracy. Judge the run-out at the moment the bails come off, and replay it side-on at the crease.

## Evidence and limits

- Data sources: decoded difficulty and pacing `.cfg` files, tuning XML (`edges`, `misses`, `batpad`, `adjustments`, `thresholds`, `deliveries`, `fieldingoption` × 210, `pegs`, Man of the Match, fast bowlers, replays, rumble), the UI string table (attributes, rules, tips) and the executable's string table (events, conditions, presets, cameras).
- Code read with REA/Ghidra: the timing-window functions (see the data-analysis notes) and the expert-advice rules (`0x43b5c0`, `ev_5b538a0a…`). CPU stroke scoring and field selection are described from their tuning keys and preset names, not from decompiled code.
- Not established: the exact CPU stroke-scoring formula, how the CPU switches formations during play, the LBW decision geometry, and the pace-meter implementation. These would need further decompilation or runtime observation, and the game stalls at its title screen on Windows 11.
