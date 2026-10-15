# Cricket 07 data and statistics

14 October 2026. Quantitative reference extracted read-only from the owner's install (data archive, roster database, executable, live memory). Companion to [cricket07-systems.md](cricket07-systems.md). These are **reference distributions for tuning our own fictional players and content**; no Cricket 07 data enters this repository.

## 1. Content inventory (what a "complete" cricket game shipped)

| Content | Count |
| --- | --- |
| Player database records | 1,053 (360 bytes each; two roster files) |
| Per-player face models / hair meshes | 348 / 14 |
| Role body models (batter, bowler, keeper, umpire, accessories) × LOD × builds | 5 × 3 × 35 |
| Shared skeleton bones | 51 |
| Animation clips / named animation states | 335 / 775 |
| Stroke definitions (timing-zone tables) | 830 (from 91 authoring files) |
| Field formations / named field positions | 210 / ~79 |
| Delivery types | 35 (× 6 pitch types) |
| Difficulty files (format × level × human/CPU) | 20 + 4 pacing + 1 ground physics |
| Between-ball cutscenes | 1,255 |
| Scripted replay types | 8 (+ free-hit variants) |
| Stadium packs | 71 |
| Commentary audio clips | 8,161 |
| Game events / commentary condition predicates | ~60 / ~200 |
| UI screens | 266 |

## 2. Player attributes

**Record layout.** Each player is a 360-byte record. Ratings are stored as 6-bit indices (0–62) packed five per 32-bit word, then mapped to a 0–100 display scale through a fixed curve (index 43 → 67, 56 → 90, 62 → 100). The game code reads the same offsets, so the file layout is also the in-memory layout. Verified against the in-game Change Bowler screen for one bowler (all eight bowling values matched).

| Block | Offsets | Contents (labels from UI order and contrast between players) |
| --- | --- | --- |
| Batting | +0x64.0 – +0x6C.3 | ~13 batting ratings (skill, technique by side and foot, shot strengths); one lower-valued field at +0x6C.2 (likely concentration) |
| Form / state counters | +0x6C.4 – +0x70.2 | small values (6–12), likely form/confidence-style state |
| Pace bowling | +0x70.3 – +0x80.0 | Accuracy, Discipline, Seam, Bouncer, Slow ball, Inswing, Outswing, Stamina |
| Spin | +0x78.1 – +0x78.4 | spin ratings (default 47 for non-spinners) |
| Fielding | +0x7C.2 – +0x80.2 | Arm strength, Accuracy, Agility, Speed, Catching (+ keeper field) |

**Distributions across 1,053 players:**

| Measure | p10 | p25 | median | p75 | p90 | max |
| --- | --- | --- | --- | --- | --- | --- |
| Batting average (all players) | 47 | 55 | 63 | 71 | 80 | 98 |
| Batting average (specialist batters, n=308) | 71 | 73 | 77 | 82 | 88 | 98 |
| Spread of a player's batting ratings (max−min) | 5 | 6 | 8 | 11 | 15 | 54 |
| Pace bowling average (pacers, n=188) | 71 | 73 | 76 | 82 | 87 | 92 |
| Accuracy (pacers) | 72 | 74 | 79 | 84 | 88 | 94 |
| Spin average (spinners, n=76) | 71 | 72 | 76 | 82 | 90 | 97 |
| Stamina (pacers) | 79 | 82 | 86 | 88 | 90 | 94 |
| Fielding average (all) | 67 | 72 | 76 | 80 | 84 | 94 |

Batting histogram (10-point buckets): 30s: 27 · 40s: 123 · 50s: 268 · 60s: 323 · 70s: 203 · 80s: 86 · 90s: 19.

**Takeaways for our ratings:**
- **The useful range is narrow: about 45–95**, with internationals clustered at 60–85. Almost nobody is below 40 and only ~2% are 90+. Combined with the timing-window bonus (`(rating − 80)/2 %`, ±10% per attribute), a few rating points are clearly felt. Our 0–100 roster ratings should use the same compressed band.
- **Players are internally consistent**: the median spread across one player's batting ratings is only 8 points. Identity comes from a few standout strengths and weaknesses (named shots and named weaknesses), not from noisy per-field variation.
- **Everyone fields at 67–84.** Fielding barely differentiates players; keepers and specialists stand out on catching.
- Licensing shows in the data: some records are fictionalised spellings of real players, kept beside the originals.

## 3. Deliveries and pace

Speed range (min–max km/h) per delivery type, before the bowler's speed factor:

| Family | Normal | Swing | Cutters | Bouncer | Yorker | Slower | Other |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Fast | 104–126 | 101–119 | 104–126 / 104–110 | 126–137 | 126–137 | — | Reverse 126–146 |
| Medium-fast | 104–117 | 94–108 | 94–104 | 119–130 | 119–130 | 58–59 | — |
| Medium | 72–108 | 72–108 | 86–97 / 86–94 | 108–119 | 108–119 | 58–59 | — |
| Off-spin | 61–86 (off-break, doosra, teesra) | | | | | | arm ball 76–97, full flight 43–61 |
| Leg-spin | 54–86 (leg-break) | | | | | | googly 72–86, flipper 83–97, top-spinner 65–86, zooter 97–108, full flight 43–61 |

- **Side-spin and top-spin ranges are per pitch type.** For an off-break, the maximum side-spin is 150 on Green, 300 on Normal and 460 on Dusty, so dry pitches turn three times as much as green ones. Doosra and googly use the opposite sign; flipper, top-spinner and arm ball have no side-spin but more top-spin.
- **Bouncers and yorkers are faster than a stock ball** (about +15–20 km/h) in this model. Slower balls are a fixed 58–59 km/h.
- **Pace is a discrete choice.** The difficulty files define speed factors Fast 1.0 / Normal 0.8 / Slow 0.0. Observed in play: a medium Standard delivery was 63–65 MPH every time, which equals 72 + 0.8 × (108 − 72) ≈ 101 km/h. The owner's own deliveries in the same match came out at 62, 67 and 86 MPH, with the meter pushed into the orange/red band for 86, so the meter clearly controls pace. The stride meter most likely selects one of three speed levels rather than a continuous pace (inference; the exact meter input wasn't identified).

**Takeaway:** typed deliveries with **per-pitch-type spin ranges** and **three pace levels** are simpler and more readable than free continuous pace. Bowling variety comes from the delivery catalogue.

## 4. Difficulty (live-verified)

All 20 difficulty blocks were read from live memory and match the decoded files: timing-window ideal size 10/8/6/4/3 for human batting, Very Easy → Very Hard, with the visual-assist nudges (`MaxPosAdjust` 0.65 → 0.25, `MaxTrajAdjust` 0.3 → 0.1). See [cricket07-data-analysis.md](cricket07-data-analysis.md) Finding 8 for the full tables.

## 4b. Measured ball flight (live memory log, 14 October, owner's 10-over match)

The ball position was read from memory at about 30 Hz (world units × 0.01905 = metres; batting crease at z = ±10.02 m). 54 delivery segments were detected; scene-cut teleports (>1,000 km/h artefacts) and replays are excluded from these figures.

| Measure | p10 | median | p90 | Range |
| --- | --- | --- | --- | --- |
| Release speed (km/h) | 76 | 119 | 141 | 60–150 |
| Bounce point, metres in front of the batting crease | 2.6 | 3.9 | 10.1 | 1.5–10.5 |
| Speed retained through the bounce (after ÷ before) | 0.61 | 0.79 | 0.87 | 0.48–0.90 |
| Ball height when it reaches the batting crease (m) | 0.06 | 0.20 | 0.63 | 0.05–0.69 |
| Hit and throw flight speeds (km/h, off the pitch) | — | 58 | 101 | max 141 |
| Ball distance from the pitch centre (m) | — | — | 42 (p90) | max 102 |

- **Pace bands are clear in the data:** the West Indies quicks bowled 111–150 km/h; the South African part-timers in the second innings bowled 74–87 km/h.
- **Lengths cluster at 2.5–5 m (full/good) and about 10 m (short).** The 10 m group belongs to one late spell of short balls, all arriving at the crease below 0.1 m, i.e. after a second bounce or rolling. There is almost nothing in between, so length is a deliberate, readable choice.
- **The ball loses about 20% of its speed at the bounce** (median retention 0.79), which matches the ground-physics `VelocityReduce` and hardness values in §6.
- **Line:** bounce points sit within ±1 m of middle stump; the human bowler's line drifted to −0.7…−1.0 m (outside leg for the left-hander) in one spell.
- **LBW:** the commentary near-miss predictor changed once in the log (a not-out, −0.099 m / 0.45 m). It did not change for an out LBW at 21:57, so it isn't a reliable record of the umpire's decision input (see systems §13 correction). Observed outcomes in the whole session: 3 LBW outs and at least 2 not-out appeals.
- **Run-outs:** two captured, both direct hits; throws of ~70 km/h from 6 m and ~90–100 km/h from 18 m (systems §16).

**Takeaway for our tuning:** our stock deliveries should arrive within these envelopes: pitched 2.5–5 m in front of the batting crease for good/full length, 120–140 km/h for quicks, about 20% speed loss at the bounce, and crossing the crease at 0.1–0.6 m. Our current standard preset (bounce at z = −5.62 m, i.e. about 4.4 m in front of the crease) is inside this band.

## 5. Commentary composition

| Category | Clips |
| --- | --- |
| Player names (list) | 2,313 |
| Numbers (list) | 660 |
| Team names (list) | 329 |
| Match / fielding lists | 162 |
| Primary commentator lines | 706 |
| Secondary / during-play / post-ball lines | 651 |
| Expert summariser (halves, ends, bonus, fours, openings, outs, LBW, tactics, practice …) | ~1,700 |

About **40% of all commentary audio is name and number fragments** that the engine slots into sentence templates, so the line count stays manageable while every call can be specific. A separate text table holds ~1,000 surnames the commentator can pronounce for created and generated players.

## 6. Other reference values

- **Man of the Match:** one wicket ≈ 27 runs; all-rounder bonus thresholds of 200 runs / 6 wickets (scale 0.6); run-milestone tables for limited and unlimited formats.
- **Ground physics per pitch** (hardness / velocity reduction): Hard 0.35/0.035, Dusty 0.35/0.035, Normal 0.30/0.044, Green 0.25/0.050, Damp 0.15/0.060.
- **Over-rate HUD:** six ball markers per over beside the pace meter; release speed shown after every ball.
- **Formats in data:** limited overs at 5/10, 20, 40/45 and 50 overs, plus unlimited; competitions with real points rules (bonus points, net-run-rate bonus, first-innings points).

## Method

Read-only Node scripts in the session scratchpad parsed the archive (EA BIGF + RefPack) and the roster files in memory. Live values came from read-only `ReadProcessMemory` on the owner's running, non-elevated game. Field labels in §2 are inferred from UI order and from contrasts between known batters, bowlers, spinners and keepers; only the pace-bowling block was verified value-for-value against an in-game screen.
