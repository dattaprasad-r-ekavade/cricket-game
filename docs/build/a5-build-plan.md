# A5 build plan: work packages for independent agents

15 October 2026. This turns the Cricket 07 research into self-contained work packages (WPs). Each package can be handed to one agent with no other context than this file, the files it links, and the repository.

The research it builds on: [cricket07-data-analysis.md](../reviews/cricket07-data-analysis.md) (timing model, Finding 8), [cricket07-systems.md](../reviews/cricket07-systems.md) (CPU, bowling, fielding, LBW, run-outs, §12–16), [cricket07-data-stats.md](../reviews/cricket07-data-stats.md) (reference distributions, measured ball flight), [cricket07-animation-and-models.md](../reviews/cricket07-animation-and-models.md) (clip organisation). The roadmap entries are A5.1–A5.11, C5 and E1 in [plan.md](../../plan.md).

## 1. Rules for every agent

1. **Originality.** Cricket 07 is a design reference only. Never copy its data, code, tables, strings, audio or assets into the repo, and never transcribe its stroke or delivery tables. The numbers quoted in this plan are *design targets and sanity ranges* (e.g. "Ideal is the narrowest band; window/ideal ratio about 14:6 on Normal"); author our own values from our own physics and tune them by playtest.
2. **Layering.** `SuperCricket.Simulation`, `SuperCricket.Content`, and any small shared `SuperCricket.Contracts` assembly stay graphics-free (`System.Numerics` only). Gameplay rules live in Simulation; Content owns assets and validation; `SuperCricket.Game` only reads inputs, calls Simulation, and presents results.
3. **Determinism.** All randomness takes an explicit seed or a `Random` passed in. The same inputs and seed give the same result. Every new model gets a repeatability test.
4. **Data, not constants.** Tunables go into JSON under `assets/` with a `Validate()` method returning `List<string>` errors, like `BattingShotSet` and `DeliveryPreset`. Add a `validate-*` CLI command in `src/SuperCricket.Tools/Program.cs` and route it through `tools/review.ps1`.
5. **Tests.** Graphics-free behaviour gets xUnit tests in `tests/SuperCricket.Simulation.Tests`, `tests/SuperCricket.Content.Tests` or `tests/SuperCricket.Game.Tests` (presenters, routers). Host-dependent checks go in the existing `*ReviewChecks.cs` files only when unavoidable.
6. **Done means:** `dotnet build -c Release` passes, `dotnet test` passes, `pwsh tools/review.ps1` passes, one commit per WP, and a step note `docs/steps/step-NNN-<slug>.md` (next free number; check `docs/steps/` first) with behaviour, files, tests and open questions. Visual or feel changes also list exactly what a human must check in the next playtest.
7. **Don't edit `plan.md`.** The integrator ticks plan items after merging, which avoids merge conflicts on that file.
8. **Hotspot files** (`Game1.MatchLoop.cs`, `Game1.MatchActions.cs`, `MatchInputRouter.cs`, `MatchHudPresenter.cs`, `Program.cs` in Tools) may be edited by **one in-flight WP at a time**. Section 3 orders the waves so this holds; if a WP must touch a hotspot outside its wave, rebase first and keep the diff there minimal.
9. **Keep files under about 600 lines.** Add new files rather than growing a hotspot.
10. **Units:** metres, seconds, m/s, radians internally, degrees in JSON. Pitch axis: z along the pitch, batting crease at the striker's end; use `CricketPitchGeometry` for positions, never hard-coded crease numbers.
11. **Never loosen a review threshold to make a WP pass** (added after review 1). If a change moves a measured value outside its band, leave the band alone, report the value in the step note as an open question, and flag it to the integrator. Bands change only in a dedicated balance WP.
12. **Report only checks you actually ran.** Write the exact command and its result. If a tool is missing (for example `pwsh`), say so; don't record the check as passed.
13. **Keep the working tree building between sessions.** Commit finished work. Move unfinished work to a branch or stash, never leave it in `main`'s working tree.

## 2. Target design in one page

**Batting (A5.1–A5.5).** The player chooses a *direction* (8-way) and a *shot type* (defend / attack / loft modifier) and presses at the right moment. The front or back foot is picked automatically from the ball's length. When the player commits, we measure **how far the ball still is from the contact plane** (not how much time is left) and classify it into one of seven zones: Too early, Start, Early, **Ideal**, Late, End, Too late. Each stroke has an authored outcome per zone: direction ± spread, elevation and a speed band. The ball leaves the bat with that outcome (seeded variation inside the band), and the animation is aligned to the contact. Ideal = hard and along the ground in the chosen direction; Early/Late = weaker, lifted, deflected; Start/End = skied or edged; Too early/late = played and missed or beaten. The only difficulty knob is the **ideal width**, which depends on difficulty, the stroke-relevant rating, how long the batter has been in, confidence, and the loft modifier.

**Bowling (A5.9).** Pick a delivery from the bowler's catalogue (one key each), aim a visible target that shrinks with accuracy and rhythm, then a stride pace meter: three pace levels, and pressing late at the top of the meter bowls a no-ball. A hidden landing scatter exists only above Easy. Stamina erodes accuracy across a spell.

**Fielding (A5.10, A5.11).** Named position ring; attack / standard / defensive presets per bowler type; a picker previews them on the radar without pausing. Throws are flat and fast (about 70 km/h short, 90–100 km/h from the deep), with a direct-hit chance scaled by fielding accuracy. Run-outs are judged when the bails come off.

**Umpiring (A5.11).** LBW projects the ball to the stumps: hitting = out, clipping = umpire's call (probabilistic), missing/over = not out. Decisions are shown with an umpire close-up and a replay with a stump-line strip.

**Presentation (A5.6–A5.8).** Every outcome raises a typed event; HUD text, timing label, audio, rumble, umpire signals and replays all subscribe to events. Practice nets are the tutorial.

## 3. Waves and dependencies

```
Wave 0  WP0 contracts ─────────────────────────────────────────────┐
Wave 1  (parallel, Simulation/Content only, no hotspot edits)       │
        WP1 stroke table   WP2 timing window   WP5 ball category    │
        WP7a bowling model WP9 LBW            WP10 throws/run-outs  │
        WP13 CPU stroke scoring  WP14 ratings  WP15 difficulty      │
        WP16 delivery envelopes  WP17 event bus                     │
Wave 2  (serial on hotspots, in this order)                         │
        WP3 outcome-first contact (needs 1,2,5,15,17)               │
        WP4 batting input scheme (needs 3)                          │
        WP6 timing gauge + feedback (needs 2,3,17)                  │
        WP7b bowling controls + pace meter (needs 7a,17)            │
        WP8 field presets + picker (needs 0)                        │
Wave 2b (added by review 1, before anything else in wave 2/3)       │
        WP25 verification hygiene (first, S)                        │
        WP22 small correctness fixes (S)                            │
        WP13 CPU stroke scoring (was skipped in wave 1)             │
        WP21 match balance (needs 13, 22)                           │
        WP23 spin turn after the bounce (parallel with 21)          │
        WP24 pace-meter bands (parallel, owns MatchHudPresenter)    │
        WP9b/WP10b umpiring and run-out integration (needs 9,10,21) [x] │
Wave 3  WP11 replays (needs 17 + wave 2) [x]   WP12 practice nets (3,4,6,7b; Step 139 implementation, playtest pending)
        → human playtest gate (section 6)
Art track (parallel throughout): WP18 clip set, WP19 characters, WP20 props/stadium
```

Rough sizes for one agent: S = under half a day, M = 1–2 days, L = 3–5 days.

## 3a. Review 1: status and findings (15 October 2026)

**Status.** 15 packages were committed overnight (Steps 114–128, commits `ec85a53`…`00431b9`): WP0, 1, 2, 3, 4, 5, 6, 7a, 7b, 9, 10, 14, 15, 16, 17. WP8 is in progress and uncommitted. WP13 was skipped. WP9b/10b, 11, 12 and the art track (WP18–20) have not started.

**Verified by the reviewer** on a clean worktree of `00431b9`: Release build with 0 warnings and 0 errors; 501 tests pass (288 Simulation, 188 Game, 25 Content). The timing gauge, the four-line batting action panel and the bowling rotation panel read clearly in the Step 127/128 captures. `TimingWindowModel` and `LbwModel` follow this plan's formulas.

**Findings, highest priority first:**

| # | Finding | Evidence | Fixed by |
| --- | --- | --- | --- |
| R1 | **Match balance is broken.** 30 seeded 10-over physics matches (seed 9100): 11.6 runs per over (target 6–9), 1.8 wickets per innings, 25 boundaries per innings (about 42% of balls), LBW 48% of dismissals (target 8–18%), catches 3% (real cricket about 50–60%), run-outs 9% (on target). WP3's own note reported runs per over rising from 12.2 to 15.1. | `simulate-physics-match-batch … 30 10 9100`; Step 125 note | WP21 |
| R2 | **Review thresholds were loosened instead of fixing balance.** In `tools/review.ps1` the maximum innings score went from 150 to 190, the average from 120 to 125, and the allowed run-out share to 18% (plan: 12%). The review never checks LBW or catch share. | `git diff 317a6a9..HEAD -- tools/review.ps1` | WP21, WP25 |
| R3 | **"review.ps1 passed" claims could not be reproduced.** PowerShell 7 (`pwsh`) is not installed on the development machine. Under Windows PowerShell 5.1 the script stops at the intentional invalid-roster check because native stderr becomes a terminating error. The 15 commits are not pushed, so hosted CI has not run. | Step 125–127 notes; local run | WP25 |
| R4 | **The working tree does not build.** In-progress WP8 code uses a `BowlerStyle` type that doesn't exist (`FieldFormationCatalog.cs`). | `dotnet build` on the working tree | WP8 (finish or stash) |
| R5 | **The CPU batter's settling-in uses the match ball count.** `new BatterTimingState(_match.LegalBalls)` in `Game1.MatchActions.cs` means a new CPU batter late in an innings starts fully settled. | code | WP22 |
| R6 | **Edges don't behave like edges.** `StrokeOutcomeResolver` turns an edge into the stroke's own yaw ±15° at half speed, so an edged cover drive still goes to cover. The plan specifies a deflection fine of the keeper's line. | code | WP22 |
| R7 | **Spin doesn't turn.** WP7a stores spin and pitch multipliers, but `BallFlightSimulator` doesn't apply turn after the bounce (the Step 118 note says so). | Step 118 note | WP23 |
| R8 | **The pace meter shows only the no-ball marker**, not the Slow / Normal / Fast bands, so the player can't aim for a pace. | Step 128 capture | WP24 |
| R9 | **The batch tool only accepts 1, 2, 5 or 10 overs**, so section 6's "50 seeded T20s" gate can't be run. | `simulate-physics-match-batch … 20` error | WP21 |

**Process rules added after this review:** see rules 11–13 in section 1.

### 3b. Current implementation status (16 October 2026)

WP9b/WP10b live integration and WP11 replays are complete in Steps 138 and 137. The WP12 implementation is in [Step 139](../steps/step-139-practice-nets.md); its under-three-minute human tutorial check remains open. WP18's technical clip set is in [Step 140](../steps/step-140-art-clip-set.md); its required human visual review remains open. WP19's three CC0 body variants, nine 61-joint LOD exports, runtime animation/kit integration and camera-distance LOD selection are implemented and validated in [Step 142](../steps/step-142-player-variants.md); human review of physique, kit fit and LOD transitions remains open. WP20's 11 static props are loaded from a validated 15-placement manifest and drawn in the match scene, as recorded in [Step 141](../steps/step-141-stadium-props.md); human review of scale, placement, readability and clipping remains open.

On 16 October 2026, the final `pwsh tools/review.ps1 -RunGameChecks -CaptureVisuals` run completed with exit code 0 after the Step 143 fixes. The Release solution and Godot trial builds passed with no warnings or errors; animation-contract parity tests, content validators, gameplay/live-match checks, renderer captures, the 11-prop stadium manifest validation, and all review batch gates passed. `dotnet test SuperCricket.sln -c Release --no-build` also passed all 544 tests (308 Simulation, 203 Game, 33 Content; 0 failed, 0 skipped). Current Standard 50-match samples passed at 10 overs (6.11 RPO, 3.36 wickets/innings; LBW 14.9%, caught 58.6%, bowled 15.8%, run-out 10.7%) and 20 overs (6.58 RPO, 7.07 wickets/innings; LBW 15.1%, caught 59.8%, bowled 15.7%, run-out 9.3%). The five 50-match Human-profile samples also pass every section 6 band; see [Step 134](../steps/step-134-cpu-stroke-acceptance.md). Hosted [Windows validation run 98](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/38812431483) succeeded on commit `b02f59a`; details are recorded in [Step 130](../steps/step-130-verification-hygiene.md). Automated verification does not close the person-led gates in section 6 or the WP18–WP20 visual reviews.

The implementation commits are WP21 (`56f7644`, with the lofted-edge balance correction in `6ddf596`), WP11 (`5df534d`), WP12 (`348b346`), WP19 (`38ce4ec`), and WP20 (`fccd0c9`). Live-host integration for WP9b/WP10b and the connected review/presentation paths is in `7defba1`. Earlier wave commits include WP13 (`eb6c211`), WP23 (`7779298`), WP24 (`087c1c5`), and the WP18 clip set (`5432396`). The review gate runs 50 seeded 20-over batches for all five simulated Human profiles, collects every out-of-band result, completes the other review checks, and enforces section 6's unchanged bands and Rookie-over-Legend scoring direction. The full 1.05 Human-proxy sample passes every band: Rookie records 7.71 RPO, 5.06 wickets per innings and 11.5% run-outs; Rookie averages 305.6 combined runs to Legend's 255.4. CPU Standard match tuning is unchanged. Results are in [Step 134](../steps/step-134-cpu-stroke-acceptance.md). Section 6 item 6's automated batch gate passes. The 16 October full review then found a contact-zone fade regression from the broadened timing windows; the difficulty-based presentation fix and verification are tracked in [Step 143](../steps/step-143-contact-zone-difficulty-fade.md). Code and test verification are complete locally and hosted CI run 98 is green. Person-led playtests and art reviews remain open as listed in section 6 and the pending worksheet.

## 4. Work packages

### WP0 — Shared contracts (S, serial, first) [x]

**Why:** wave 1 runs in parallel; the packages must agree on types before they start.

**Create** `src/SuperCricket.Simulation/Batting/StrokeContracts.cs` (types only, no logic):

```csharp
public enum TimingZone { TooLate = 0, Start = 1, Early = 2, Ideal = 3, Late = 4, End = 5, TooEarly = 6 }
public enum StrokeFoot { Auto, Front, Back }
public enum StrokeIntent { Leave, Defend, Attack }        // loft is a modifier, not an intent
public enum BallLength { Short, Good, Full, Yorker, FullToss }
public enum BallLine { WideOutsideOff, OutsideOff, Stumps, Leg, WideLeg }
public enum ShotDirection8 { Straight, OffDrive, Cover, Square, Fine, LegFine, LegSquare, MidWicket } // relative to a right-hander
public readonly record struct BallCategory(BallLength Length, BallLine Line, float ArrivalHeightMeters);
public readonly record struct StrokeRequest(ShotDirection8 Direction, StrokeIntent Intent, bool Loft, StrokeFoot Foot);
public readonly record struct ZoneOutcome(float YawDegrees, float YawSpreadDegrees, float ElevationDegrees,
    float MinSpeedMetersPerSecond, float MaxSpeedMetersPerSecond, float EdgeChance, bool BeatenOrMissed);
public readonly record struct StrokeResolution(string StrokeId, TimingZone Zone, float BallDistanceAtInputMeters,
    System.Numerics.Vector3 OutgoingVelocity, bool Edged, bool Missed, StrokeFoot FootPlayed);
```

Also add `src/SuperCricket.Simulation/Events/MatchEvent.cs` with an empty `abstract record MatchEvent(float TimeSeconds)` and the event record *names* WP17 will fill in (`DeliveryReleased`, `BallPitched`, `StrokePlayed`, `BatContact`, `BallBeatBat`, `BodyHit`, `Appeal`, `UmpireDecision`, `WicketBroken`, `RunCompleted`, `NearRunOut`, `Boundary`, `Dismissal`, `OverComplete`).

**Accept:** builds; one test that `TimingZone` numeric values match the table in section WP2 (the HUD and assets depend on the order). Nothing else changes.

---

### WP1 — Zone-based stroke table (M) · A5.1 [x]

**Goal:** replace the three-shot `assets/batting/shots.json` model with an authored stroke table.

**Create**
- `src/SuperCricket.Content/StrokeTableAsset.cs`: `StrokeTableAsset { Version, List<StrokeDefinition> Strokes }`; `StrokeDefinition { Id, Direction (ShotDirection8), Intent, Foot (Front|Back), Loft (bool), AnimationClip, Aggression (0 defend, 1 attack, 2 big hit), Difficulty 0–1, Reward 0–1, Suits (lists of BallLength and BallLine), Zones: Dictionary<TimingZone, ZoneData> }`; `ZoneData` mirrors `ZoneOutcome` with degrees in JSON. A `Load`, `Validate()` and `Mirror()` (for a left-hander: negate yaw).
- `assets/batting/strokes.json`: **our own** first table. Minimum set: 8 directions × {defend, attack} × {front, back} = 32, plus 8 loft strokes = 40 strokes. Every stroke must define Start, Early, Ideal, Late and End; TooEarly/TooLate are global (beaten).
- CLI `validate-strokes <path>` in Tools; add it to `review.ps1`.

**Authoring rules** (shape targets from the research, values ours):
- Ideal is the fastest and lowest band: attack Ideal elevation −5°…+3°, defend Ideal speed under 12 m/s.
- Early and Late lose about 15–25% speed and gain 5–12° elevation; Late deflects *finer* (towards the keeper side), Early deflects *squarer/across*.
- For the right-hander reference table, positive yaw moves through the off side toward fine and negative yaw through the leg side toward leg fine; author Early offsets toward square and Late offsets toward the finer/keeper side, then mirror yaw for a left-hander.
- Start/End: elevation 15–30°, speed 60–75% of Ideal: catchable.
- Loft strokes: Ideal elevation 25–40°, Start/End elevation over 40° (skied).
- Off-side strokes on leg-stump balls and vice versa must exist only where cricket allows (validator warns, doesn't fail).

**Validator checks:** all five zones present; Ideal max speed ≥ every other zone's max speed for that stroke; min ≤ max; spread 0–45°; elevation −10…70°; clip name exists in the batter's animation contract (`assets/characters/practice-batter.animation-contract.json`); every (direction, intent, foot) combination resolves to at least one stroke.

**Accept:** `validate-strokes assets/batting/strokes.json` passes; tests for load, mirror, each validator failure. `shots.json` stays until WP3 removes its use.

---

### WP2 — Timing-window model (M) · A5.3 [x]

**Create** `src/SuperCricket.Simulation/Batting/TimingWindowModel.cs`:

1. `TimingWindow Build(float centre, float window, float ideal)` returns the seven ranges, using the ball's distance *d* from the contact plane (positive = ball still coming):

   | Zone | Range of d |
   | --- | --- |
   | TooEarly | d > C + W/2 |
   | Start | C + (W+I)/4 < d ≤ C + W/2 |
   | Early | C + I/2 < d ≤ C + (W+I)/4 |
   | Ideal | C − I/2 ≤ d ≤ C + I/2 |
   | Late | C − (W+I)/4 ≤ d < C − I/2 |
   | End | C − W/2 ≤ d < C − (W+I)/4 |
   | TooLate | d < C − W/2 |

2. `TimingZone Classify(TimingWindow, float d)`.
3. `float IdealWidth(TimingDifficulty profile, BatterTimingState state, bool loft, float strokeRating)`:
   `width = profile.IdealBase × modeFactor × settle(state.BallsFaced) × confidenceFactor + skillBonus`, where
   - `modeFactor = loft ? (1 − profile.LoftReduction) : 1`;
   - `settle(n)`: a piecewise-linear curve from data, default shape: `profile.SettleStartFraction` (0.6) until 8 balls, linear to 0.75 at 20, to 1.0 at 36;
   - `confidenceFactor = 0.8 + 0.2 × confidence` (confidence 0–1);
   - `skillBonus = clamp((rating − 80)/200, −0.1, +0.1) × profile.IdealBase` per relevant rating, total clamped to ±0.2 × IdealBase;
   - clamp to at least `profile.IdealMinimum`.
4. `TimingZone ApplyAssist(TimingZone z, bool assist)`: Early/Late → Ideal when assist is on.
5. `float DistanceToContactPlane(BallFlightFrame frame, float contactPlaneZ)`: measured along the ball's velocity, so it scales with pace automatically.
6. `BatterTimingState { int BallsFaced; float Confidence; }` with `RegisterBall(TimingZone zone, bool bodyHit, bool beaten)`: confidence +0.05 on Ideal, −0.1 on beaten, −0.15 on body hit, clamped to 0–1; starts at 0.6.

The window centre and size are in metres of ball travel. **Starting values to tune** (ours, inside the research's ratio range): window 1.4 m, ideal 1.0/0.8/0.6/0.4/0.3 m for Rookie → Legend (ideal shares of 71/57/43/29/21%). At 35 m/s, 0.6 m is about 17 ms each side of perfect; if playtests show this is too tight, scale *all* values together, not just one.

**Accept:** tests: zone boundaries (exact edges belong to the inner zone); shrinking ideal widens Early/Late; assist mapping; settle curve endpoints; skill bonus clamp; confidence clamp; Classify is monotonic in d; the same d gives the same zone against 25 m/s and 40 m/s deliveries.

---

### WP3 — Outcome-first contact (L, wave 2, owns `Game1.MatchLoop.cs`) · A5.2 [x]

**Create** `src/SuperCricket.Simulation/Batting/StrokeOutcomeResolver.cs`:
`StrokeResolution Resolve(StrokeTableAsset table, StrokeRequest request, BallCategory ball, float distanceAtInput, TimingWindow window, bool leftHanded, bool assist, Random rng)`:
1. Select the stroke (WP5's `StrokeSelector`).
2. Classify the zone, then apply the assist.
3. TooEarly/TooLate → `Missed = true`; the ball continues on its own flight (it can hit pad or stumps: WP9 and the existing wicket check handle that).
4. Otherwise sample yaw in `Yaw ± Spread`, speed uniformly in `[Min, Max]`, elevation as authored; roll `EdgeChance` (edge → fine yaw ±15° off the keeper's line, speed × 0.5, elevation 5–20°).
5. Mirror for a left-hander.

**Integrate:**
- In `Game1.MatchLoop.cs`, at the input commit, store `distanceAtInput`. When the ball reaches the contact plane, apply `StrokeResolution.OutgoingVelocity` at the bat's sweet-spot position via `_ballFlight.ApplyBatContact` instead of the `TryBatContact` + `BattingImpactModel.Calculate` path. Keep the swept resolver behind `--debug` as a visual cross-check.
- Ball snap: if the bat's sweet spot is within 0.35 m of the ball at the contact frame, start the outgoing ball from the sweet spot (no visible gap). Otherwise use the ball position.
- Switch `PhysicsAutomatedMatchBatchSimulator` and `CpuBattingOutcomeModel` to the resolver, with the CPU's input distance drawn from its own window (WP15's CPU profile). Batch and live must agree for the same seed.
- `BattingPracticeAnalyzer` keeps the swept path; it is the art tool that checks clips actually meet the ball.
- Remove the uses of `shots.json` from the runtime after migration; keep the file as a test fixture if tests use it.

**Accept:**
- Determinism: same request, ball, distance and seed → identical outgoing velocity (test).
- Learnability: for each stroke, distances in the Ideal band give 100% non-missed results with elevation in the authored Ideal range (test over 200 seeds).
- The batch simulator still completes a 2-innings match for 50 seeds; report median runs per over before and after in the step note (expected change: more runs, fewer "no contact").
- Live: `review.ps1 -RunGameChecks` passes.
- The playtest checklist names: "same timing on same ball gives the same kind of shot".

---

### WP4 — Batting input scheme (M, wave 2, owns `MatchInputRouter.cs`) · A5.4 [x]

**Default keyboard scheme** (based on the Cricket 07 PC layout the owner knows; our wording):

| Input | Action |
| --- | --- |
| Arrow keys (8-way incl. diagonals) | Direction, relative to a right-hander, mirrored for left-handers |
| S | Attacking stroke (foot chosen automatically) |
| W | Defensive stroke |
| Shift held | Loft modifier |
| A | Leave |
| D | Advance down the pitch (before release only) |
| Run keys | Unchanged (D/A after contact) |

GamePad: left stick direction, A attack, X defend, RB loft, B leave, LB advance.

- **Automatic foot** (`StrokeFoot.Auto`): Short or a ball arriving above 0.9 m → Back; Full/Yorker/FullToss → Front; Good → Front for drives (Straight/OffDrive/Cover/MidWicket), Back for Square/Fine/LegFine/LegSquare. Put this in `BallCategoryClassifier.ChooseFoot` (WP5), not in the router.
- **Manual foot** setting (pause menu, persisted in `GameSettingsStore`): W = back-foot stroke, S = front-foot stroke.
- The commit moment is the **press**, not the release.
- Keep the Step 112 chord recorder as an alternative scheme "Classic chords" in settings; not the default.
- Update the HUD action list (`Game1.ActionHud.cs`) to four lines at most for batting: direction, attack/defend, loft, leave.

**Accept:** router tests for each key → `StrokeRequest`; diagonals; the left-hander mirror; manual vs auto; chords still work when selected. README controls table updated.

---

### WP5 — Ball category and stroke selection (S–M) · A5.5 [x]

**Create** `src/SuperCricket.Simulation/Batting/BallCategoryClassifier.cs`:
- `BallCategory Classify(DeliveryPreset prepared, float batterStanceX, bool leftHanded)`: use the predicted first bounce (`BowlingAimModel.FindFirstBounce`) and the crossing at the batting crease. Length by bounce distance in front of the batting crease: Yorker < 1.0 m, Full 1.0–3.0, Good 3.0–6.5, Short > 6.5, no bounce before the crease = FullToss. Line by crossing x relative to off/middle/leg stump, mirrored for a left-hander.
- `StrokeFoot ChooseFoot(BallCategory, ShotDirection8)`: rules in WP4.

**Create** `src/SuperCricket.Simulation/Batting/StrokeSelector.cs`: `StrokeDefinition Select(StrokeTableAsset, StrokeRequest, BallCategory)`: exact match on (direction, intent, loft, foot); if absent, nearest direction on the same side; prefer strokes whose `Suits` include the ball. Deterministic tie-break by `Id`.

**Accept:** tests on every preset in `assets/deliveries/` with known categories; boundary values; tie-break stability; mirror symmetry.

---

### WP6 — Timing gauge and feedback channels (M, wave 2, owns `MatchHudPresenter.cs`) · A5.6 [x]

**Review follow-up (Step 143):** Contact-zone opacity now uses the selected difficulty ladder independently of timing-window tuning, preserving the Rookie-to-Pro fade and hiding the cue on Legend.

- Drive the existing live timing gauge (Step 62) from `TimingWindow`: seven coloured bands scaled to window width, a marker moving with the ball's distance; the Ideal band visibly narrows as difficulty rises and widens as the batter settles in. Hidden above the "Pro" difficulty, as now.
- On commit, show the zone label (EARLY / PERFECT / LATE / VERY EARLY / VERY LATE / BEATEN) in a small inset near the batter for about 1 s, and put it on the result card.
- Subscribe to WP17 events for: audio cue per zone (distinct "middle" sound for Ideal, thin sound for edges, dull sound for body hits using the existing procedural audio), GamePad rumble (strong-short for Ideal, buzzy for edge, long for body hit), and umpire signal placeholders (text banner until animations exist: FOUR, SIX, WIDE, NO BALL, OUT).
- Presenter logic stays graphics-free and tested; drawing stays in `Game1.*Hud.cs`.

**Accept:** presenter tests for band layout from a window, label mapping for every zone, signal mapping for every event; capture at 1440×900 for the step note.

---

### WP7a — Bowling model (M) · A5.9 [x]

**Create** under `src/SuperCricket.Simulation/Bowling/`:
- `DeliveryCatalogue` + `assets/bowling/deliveries.json` (Content asset + validator): typed deliveries per bowler style: *pace* (stock, outswing, inswing, off-cutter, leg-cutter, bouncer, yorker, slower ball), *off-spin* (off-break, arm ball, doosra-style variation, flighted), *leg-spin* (leg-break, googly, top-spinner, flipper, flighted). Each has a speed range in km/h, swing/seam or spin parameters, and a per-pitch-type multiplier for spin. Our own values; sanity ranges from data-stats §3 (quicks 120–145 km/h stock, slower ball about 40% slower, spinners 70–90 km/h; dry pitches turn about 3× more than green).
- `BowlingAccuracyModel`: `TargetRadius = lerp(MaxRadius, MinRadius, accuracy × rhythm)`, `rhythm = min(1, ballsInSpell / 10)`. `Scatter`: uniform ±0.25 × S in line and length, with `S` stepping down as the spell settles. **On Rookie and Amateur the scatter is zero.** Stamina: each over costs `8 − staminaRating/20` points; below 50 the target radius grows linearly up to +50%.
- `PaceMeterModel`: the meter fills over the stride (`fillSeconds` from difficulty: 0.9 s easy, 0.7 normal, 0.45 hard). Pressing in the lower third → Slow (×0.82 speed), middle → Normal (×0.92), top band → Fast (×1.0), the last 8% of the meter → **no-ball** (front foot over the line). No press → Normal.
- Output: a `DeliveryPreset` via the existing `BowlingAimModel.AimForPitchTarget`, with the scattered target and the chosen speed.

**Accept:** tests: zero scatter on easy settings; the rhythm ramp; stamina decay; meter bands, including the no-ball band; every catalogue entry produces a delivery that bounces inside the pitch for a centred target (seeded batch of 100 per entry).

### WP7b — Bowling controls (M, wave 2, owns `Game1.MatchActions.cs`) [x]

- One key per available delivery (1–8 / face buttons with a page toggle); pressing it locks the target and starts the run-up in a single step. Arrows move the target before the press.
- Show the shrinking target ring, the meter in the stride, and "NO BALL" when it happens. The CPU bowler uses the same model with its own target choice.
- Bowler change at the end of an over: a list with each bowler's stamina and overs bowled (presenter + simple panel).

**Accept:** router/presenter tests; the CPU and human paths both pass through `BowlingAccuracyModel`; a capture of the meter.

---

### WP8 — Field presets and picker (M) · A5.10 [x]

- `assets/fields/positions.json`: a ring of named positions (slip 1–3, gully, point, cover, extra cover, mid-off, mid-on, mid-wicket, square leg, fine leg, third man, long-on, long-off, deep cover, deep mid-wicket, deep square, short leg, silly point, keeper…) as polar coordinates for a right-hander, mirrored for left-handers.
- `assets/fields/presets/*.json`: for each bowler style (pace, off-spin, leg-spin) × intent (attack, standard, defend) = 9 presets of 9 fielders + keeper + bowler, referencing position names. Validator: 11 players, legal under limited-overs fielding restrictions if they are enabled (no more than 5 on the leg side; inner ring counts if a powerplay is active).
- CPU captain: `FieldPlacementModel.ChooseTactic` already exists. Extend it to choose a preset name: new batter or wicket in the last 2 overs → attack; required rate high or boundaries in the last over → defend; otherwise standard. Re-evaluated only between overs and after a wicket.
- Human picker: Up/Down before the run-up opens the list, previews the formation on the existing radar (`Game1.FieldRadar.cs`) without pausing; select/close keys from WP4's scheme. Fielders walk to new spots (no teleport).

**Accept:** validator tests; mirror test; CPU choice tests on constructed situations; radar preview capture.

---

### WP9 — LBW (M, sim) · A5.11 [x]

**Create** `src/SuperCricket.Simulation/Umpiring/LbwModel.cs`:
1. Triggered when the ball's first contact after pitching is the batter's pad/body (needs a pad hit volume: a capsule per leg from the batter's pose, or a fixed stance capsule in the batch simulator).
2. Rule checks, in order: pitched outside leg → not out; impact outside the line of off stump **and** a genuine shot offered → not out; no shot offered → impact outside off allowed.
3. Project the ball from the impact point along its pre-impact velocity (with gravity, no further bounce if it already bounced; with a bounce if not) to the stumps plane and record lateral offset *x* and height *h*.
4. Bands (our numbers; stump half-width 0.114 m, ball radius 0.036 m): `|x| ≤ 0.09` and `h ≤ 0.69` → **Hitting** (out); `0.09 < |x| ≤ 0.15` or `0.69 < h ≤ 0.74` → **Umpire's call** (out with probability `p` from difficulty, default 0.35); beyond → **Missing** / **Going over** (not out).
5. Raise `Appeal` and `UmpireDecision(kind = Lbw, out, band, x, h)` events (WP17).
6. CPU fielding side appeals on Hitting and Umpire's call; a human gets an appeal button only when bowling.

**Accept:** geometry tests on constructed trajectories for each band and rule; seeded umpire's-call frequency within ±5% of `p` over 1,000 trials; the batch simulator produces LBWs at a sane rate (report: LBW share of dismissals over 50 seeded matches; target 8–18%).

### WP10 — Throws and run-outs (M, sim) · A5.11 [x]

- `ThrowModel`: speed = lerp(19 m/s at 5 m, 28 m/s at 30 m+) × (0.85 + 0.15 × armRating/100); flat trajectory (apex under 2 m for throws under 25 m); target the end the CPU judges closer to a run-out.
- Direct-hit chance = `base(distance) × (0.6 + 0.4 × accuracyRating/100)`, with `base` 0.55 at 5 m falling to 0.15 at 30 m. A miss goes to the keeper/bowler who gathers and breaks the wicket after 0.35 s.
- Run-out decision: at the moment the wicket is broken, out if the batter's grounded bat/foot is not behind the popping crease (uses the existing ground-ownership code from Step 107). `NearRunOut` event when the batter is within 2.0 m of the crease line when the wicket is broken and not out.
- Update `BattedBallFieldingModel` / `FieldingSide` return-throw code to call `ThrowModel` rather than its current fixed values.

**Accept:** tests on throw speed by distance, direct-hit frequency (seeded), the decision at the crease edge, and the near-miss event; the batch simulator's run-out share of dismissals over 50 seeded matches is reported (target 5–12%).

WP9b/WP10b (wave 2): wire both into the live host, umpire close-up camera preset (`CameraDirector`), decision banner. Live appeals, projected LBW decisions, run-out judgments, replay triggers, and the decision presentation are implemented and verified in [Step 138](../steps/step-138-live-umpiring-and-run-outs.md). [x]

---

### WP11 — Replays (L, wave 3) · A5.7 [x]

**Progress (Step 137):** The recorder, event scripts, playback, skip, innings wicket reel, stump-line and crease overlays are integrated with WP9b/WP10b. The script validator accepts all six event types, replay tests pass, and the inspected LBW capture shows the strip and impact marker.

- `ReplayRecorder` (Game): a ring buffer of the last 12 s of render state per tick (ball, all player poses as clip id + time, camera). It records what was rendered, so it doesn't re-simulate.
- `ReplayScript` asset: per event type (bowled, caught, LBW, run-out, six, near run-out) a list of shots: camera preset, start offset from the event, duration, speed (1.0 / 0.5 / 0.25), overlay (`StumpLineStrip` for LBW using `UmpireDecision.x/h`; `CreaseZoom` for run-outs).
- Plays automatically after a wicket or six; skippable with any key. End-of-innings "fall of wickets" reel replays each wicket with a caption ("FOW 18/6, Name run out 0").

**Accept:** recorder tests (buffer wrap, timestamps); script validator; capture of an LBW replay with the strip; skip works.

### WP12 — Practice nets as the tutorial (M, wave 3) · A5.8

**Progress (Step 139):** Added the mode/setup menus, repeat batting tutorial, coaching progression, rolling zone history/histogram, and five-hit bowling target drill. Automated tests and the inspected setup capture pass. Keep WP12 unchecked until the required under-three-minute human tutorial playtest is recorded.

- Mode from the title menu: choose bowler style, line, length, pace; ball repeats until changed.
- First session: three guided steps with on-screen prompts (1. "press S as the ball reaches the yellow band", 2. "add a direction", 3. "hold Shift to loft"), each passed after three Ideal-zone hits. Then prompts disappear.
- Bowling drill: hit a target ring five times.
- Stats panel: last 10 balls' zones and a histogram.

**Accept:** prompt progression tests in a presenter; a human can finish the tutorial in under three minutes (playtest item).

---

### WP13 — CPU stroke scoring (M) · E1 — skipped in wave 1, now wave 2b (WP21 depends on it) [x]

**Progress (Steps 132 and 134):** The scorer, pacing table, profile weights, live integration, and component tests are complete. The 50-match T20 report across all five CPU difficulties is recorded in [Step 134](../steps/step-134-cpu-stroke-acceptance.md); every level scores faster in the death phase than the middle phase, and average combined score rises substantially from Rookie to Legend.

**Create** `src/SuperCricket.Simulation/Batting/CpuStrokeScorer.cs`: for each candidate stroke,
`score = Σ wᵢ × termᵢ` with weights normalised to sum to 1, from `assets/difficulty/*.json` (WP15):
- *fit* terms: 0–1 match of the stroke's `Suits` against the ball's length and line;
- *intent* term: 1 if the stroke's aggression class equals the wanted class this ball, 0.1 if one class away, 0 otherwise;
- *reward* term: `(1 − max(0, stroke.Difficulty − cpuSkill)) × (1 − |stroke.Reward − wantedReward|)`;
- *random* term: seeded 0–1.

The wanted class and reward come from a **pacing table**: per format and phase (overs 1–6, 7–15, 16–20 for T20; scaled for other lengths), a target run rate, and the gap between required and current rate pushes the class up or down. Style per batter (anchor, balanced, aggressor) shifts it by one notch.

The CPU's commit distance is drawn from a normal distribution around the window centre with σ from the CPU difficulty profile, so a better CPU hits more Ideal zones.

Replace the shot choice in `CpuBattingOutcomeModel` / `CpuLiveBattingPlanModel` with the scorer; keep their leave and running logic.

**Accept:** tests: normalisation; each term in isolation; the pacing table shifts aggression; determinism. Batch report over 50 seeded T20s per difficulty: runs per over and wickets per innings by phase (target shape: death overs faster than the middle; harder CPU scores more).

### WP14 — Player ratings and identity (M) · E1 [x]

- Extend `TeamPlayerData` (version 2, with a migration that fills new fields from `Timing`/`Power`): batting `FrontFoot`, `BackFoot`, `OffSide`, `LegSide`, `Lofted`, `Concentration`; bowling `Style`, `Accuracy`, `Pace`, `Movement` (swing or spin), `Variation`, `Stamina`; fielding `Catching`, `Throwing` (arm), `Accuracy`, `Speed`; plus `BattingStyle` (anchor / balanced / aggressor) and `Handedness`.
- Rating band rules (validator warns): values 40–98; a player's batting ratings spread under 20 points except for one or two signature strengths; fielding 60–90 for everyone except specialists.
- Map ratings to felt effects: the stroke-relevant batting rating feeds WP2's skill bonus (front-foot strokes use `FrontFoot`, etc.); bowling `Accuracy` feeds WP7a; fielding feeds WP10 and catching.
- Regenerate the fictional rosters in `assets/teams/` with these bands.

**Accept:** migration test; validator tests; rosters pass `validate`.

### WP15 — Difficulty profiles (S) [x]

`assets/difficulty/{format}-{level}-{human|cpu}.json`, five levels × two sides, for "limited" now (Test format later): timing window (centre, window, ideal base, ideal minimum, loft reduction, settle curve, assist on/off), umpire's-call probability, bowling target radius min/max, scatter steps, meter fill time, CPU stroke weights and commit σ. The CPU-batting files invert the human ones (harder = stronger CPU). Validator + loader in Content; `CpuDifficultyModel` reads from it.

**Accept:** validator tests; every existing difficulty name maps to a file.

### WP16 — Delivery envelopes (S) [x]

Tune `assets/deliveries/*.json` and the WP7a catalogue so that seeded deliveries land in the measured reference envelope (data-stats §4b): good/full length pitches 2.5–5 m in front of the batting crease; quicks 120–145 km/h; about 20% speed lost at the bounce (adjust `PitchBounceRestitution`/`TangentialRetention`); crossing the crease at 0.1–0.6 m for good length. Add a `verify-delivery-envelopes` test that fails if a stock delivery leaves the envelope.

### WP17 — Match event bus (S–M) [x]

`MatchEventBus` in Simulation: `Publish(MatchEvent)`, typed subscriptions, and a per-delivery event log saved with the scorecard (useful for replays, commentary and tests). Raise events from the existing code paths (release, bounce, contact, wicket, runs, boundary, over end). No presentation changes.

**Accept:** tests that a seeded batch match produces an event log whose runs and wickets reconcile with the scoreboard.

---

### WP21 — Match balance (M–L, wave 2b, needs WP13 and WP22) · R1, R2, R9 [x]

**Goal:** seeded batch matches look like limited-overs cricket, and the review enforces it.

**Progress (Step 133):** 20-over innings and phase/dismissal CSV metrics are available. The passing PowerShell 7 review measured attack ground Ideal median 24.66 m/s and loft Ideal median 28.14 m/s. With 1.15× Standard live run-out risk, the final 50-match samples passed every unchanged match-balance band at 10 and 20 overs; T20 death-over scoring is faster than middle-over scoring. The review script captures the dedicated field-overview camera so the 62.5 m boundary and stadium can be checked together. The zero-distance throw issue is fixed.

1. **Measure first.** Add 20 overs to `simulate-physics-match-batch` (keep 1/2/5/10). Extend its CSV with `caught_dismissals`, `edges`, `aerial_contacts` and runs by phase (powerplay / middle / death). Record a baseline in the step note: 50 matches each at 10 and 20 overs, Standard difficulty, seeds 9100–9149.
2. **Shot speed vs boundary.** The Ideal zone in `strokes.json` gives 24–30 m/s at about +3°, and `FieldBoundaryRadiusMeters` is 42 m, which is far smaller than a real ground. Do both:
   - raise the boundary to a realistic **60–65 m** in the delivery/field assets and in `FieldPlacementModel` (deep-fielder radius scaled to match), checking the camera, radar and stadium still frame it;
   - re-tune Ideal speeds so that a ground stroke reaches the boundary only when it beats the field. Attack Ideal along the ground should have a median of about 22–26 m/s; reserve 28+ m/s for lofted Ideal.
   Check the rolling deceleration in `BallFlightSimulator.ApplyBatContact` (currently 7 m/s²) against outfield behaviour: a 25 m/s drive should stop or be cut off inside about 55 m unless it is placed in a gap.
3. **Catches.** Start/End zones and edges must produce catchable chances: elevation 15–35° and speed 12–20 m/s so the ball lands 15–35 m away or carries to slip and keeper. Review `CatchingModel` (14 lines): add a catch probability from the fielder's `Catching` rating, the reaction distance and the ball's height (easy chest-high ≈ 0.9, diving ≈ 0.3). Edges carry to the keeper and slips (WP22 fixes their direction).
4. **LBW share.** Replace the batch's fixed stance pad capsule with a capsule that follows the chosen foot (front foot: 0.6 m further down the pitch and towards the line of the ball; back foot: on the crease). Only a **beaten** ball (TooEarly/TooLate or a leave) may reach the pad. A shot offered outside off must stay not out. Target LBW 8–18% of dismissals.
5. **Wickets.** With 2–5 in place, tune the CPU commit σ (WP15 CPU profiles) so that wickets per innings land at 5–8 in 20 overs and 3–6 in 10 overs.
6. **Restore and extend the review gate** in `tools/review.ps1`:
   - restore the maximum innings score of 150 for 10 overs and the run-out share band of 5–12%;
   - add LBW share 8–18%, caught share 35–65% and bowled share 10–30%;
   - add runs per over: 6–9 for Standard, both formats;
   - if a band can't be met, **leave the band unchanged**, don't widen it, and report the measured value as an open question in the step note.

**Accept:** the baseline and after tables in the step note (runs per over, wickets per innings, boundaries per innings, LBW / caught / bowled / run-out share, by format); every band above met for Standard at 10 and 20 overs; Rookie scores more than Legend in the human-side batch; the restored gate passes under `pwsh`.

### WP22 — Small correctness fixes (S, wave 2b) · R5, R6 [x]

1. **CPU settling-in.** Keep a `BatterTimingState` per batter for CPU batters too (the human path already has `GetCurrentBatterTimingState()`). Reset it when a new batter arrives; `RegisterBall` after each delivery. Do the same in `CpuLiveBattingPlanModel` and `PhysicsAutomatedMatchBatchSimulator`.
2. **Edge direction.** In `StrokeOutcomeResolver`, an edge goes **fine**: yaw = the keeper's line (180° behind the batter, our pitch axis) ± 25°, biased to the off side for off-side strokes (thick/thin outside edge) and to the leg side for leg-side strokes; speed × 0.4–0.6; elevation 5–20°. Mirror for left-handers.
3. Add an `Edged` flag to the `BatContact` event payload (WP17) so audio, rumble and the result card can say "EDGE".

**Accept:** tests: a new batter's ideal width starts at the settle-curve start value, even late in an innings; an edged cover drive lands behind square on the off side; the left-hander mirror; determinism.

### WP23 — Spin turn after the bounce (M, wave 2b) · R7 [x]

**Progress (Step 135):** Signed first-bounce turn and top-spin restitution/skid response pass the six simulation cases and the delivery-envelope check. The normal off-break/leg-break comparison measures +0.388/−0.446 m at the striker's wicket line. The path capture is in [Step 135](../steps/step-135-spin-turn.md).

- In `BallFlightSimulator`, on the first pitch bounce, add sideways velocity `Δvx = spinRate × pitchTurnMultiplier × gripFactor`, with the direction from the delivery type (off-break turns into a right-hander, leg-break away; googly/doosra reversed; arm ball, flipper and top-spinner have none and instead change bounce or skid by ±10% vertical restitution).
- Use the per-pitch multipliers WP7a already stores. Sanity targets from data-stats §3: dry pitch turn about 3× green; a stock off-break on a normal pitch deviates about 0.3–0.6 m between bounce and stumps; a big-spinning ball on a dry pitch up to about 1 m.
- `BallCategoryClassifier` and the projected-bounce marker use the pre-turn line; LBW projection (WP9) uses the post-turn velocity.

**Accept:** deviation tests per delivery type and pitch; a reversed sign for googly/doosra; the WP16 envelope tests still pass; one capture comparing off-break and leg-break paths.

### WP24 — Pace-meter bands (S, wave 2b, owns `MatchHudPresenter.cs`) · R8 [x]

**Progress (Step 136):** Added model-derived band edges, presenter layout for all four zones, selected-band labeling, and a reviewed 1440×900 capture. Tests confirm the displayed edges match all five human profile fill times, and the delivery result card continues to show release speed.

Draw the Slow / Normal / Fast bands and the no-ball band on the stride meter, using the same thresholds as `PaceMeterModel`. Label the selected band after the press ("FAST", "NO BALL") and show the release speed on the result card. Keep the band layout in the presenter (tested); drawing stays in `Game1.*Hud.cs`.

**Accept:** presenter test that the drawn band edges equal the model thresholds for each difficulty's fill time; a capture.

### WP25 — Verification hygiene (S, first in wave 2b) · R2, R3, R4 [x]

1. Install PowerShell 7 on the development machine (`winget install Microsoft.PowerShell`) and run `pwsh tools/review.ps1` for real. Add a guard at the top of `review.ps1` that stops with a clear message under Windows PowerShell 5.1.
2. Get the working tree back to a building state: finish WP8 or move its partial work to a branch.
3. Push `main` so hosted CI runs. Record the run URL in the step note; fix anything it finds.
4. Add a review check that fails if any balance band constant in `review.ps1` differs from section 6 of this plan without a matching note in the step history (simplest form: keep the bands in one `tools/balance-bands.json` file that both the review and this plan reference).

**Accept:** a hosted CI run URL showing green on the pushed commit; `pwsh tools/review.ps1` output pasted into the step note; the working tree builds.

---

### Art track (parallel; human visual review required for each)

- **WP18 — Clip set (C5, about 40 clips).** Author one side, mirror the other. Batting: 8 directions × front/back × attack, 4 defends, 4 lofts, leave, beaten, body hit. Bowling: pace run-up + delivery, spin run-up + delivery. Fielding: run, stop-and-pickup, dive left/right, catch grid (low / chest / high × stationary / running), throw (flat, underarm flick), wicket-break gather. Keeper: stand, take, stumping. Umpire: out, four, six, wide, no-ball. Every batting clip carries `sc_events` contact markers; `analyze-batting-practice` must find contact for each. Technical implementation: [Step 140](../steps/step-140-art-clip-set.md); human visual review still pending.
- **WP19 — Characters (C2).** MPFB2/MakeHuman CC0 base bodies (3 builds), our 61-bone rig, kit with team-colour masks, pads, gloves, helmet, bat; 3 LODs. See section 7 on AI generation. Technical implementation and validation: [Step 142](../steps/step-142-player-variants.md); human in-game visual review remains open.
- **WP20 — Props and stadium dressing (D2).** Stumps and bails (breakable), bats, helmets, sight screens, fictional boards, stands modules, scoreboard, roller, umbrellas, crowd impostor cards. Technical implementation: [Step 141](../steps/step-141-stadium-props.md); human in-game visual review still pending.

## 5. Agent hand-off template

Give each agent exactly this:

```
Task: WPn — <title> from docs/build/a5-build-plan.md.
Read: section 1 (rules), section 2 (design), WPn, and the files WPn names.
Dependencies merged: <list>. Do not start if any is missing.
Files you own: <list>. Do not edit hotspot files outside this list.
Done when: WPn's Accept list passes, plus build/test/review.ps1, one commit, a step note.
Report: what changed, test results, numbers asked for in Accept, open questions, and the playtest checklist lines.
```

## 6. Gate after wave 3 (human playtest)

1. A new player learns batting from the nets tutorial in under three minutes and can say why a shot went up ("I was late").
2. The same input timing against the same delivery gives the same zone and the same class of shot, 10 times out of 10 ("same timing on same ball gives the same kind of shot").
3. Rookie: at least 60% of attacking inputs on good-length balls make contact; Legend: under 30% Ideal without practice.
4. The bowler can name what they bowled and see where it landed; a no-ball from a late meter press is understood.
5. An LBW and a run-out each show a decision and a replay that explain it.
6. Batch: 50 seeded T20s per difficulty meet the Standard runs-per-over, wickets-per-innings and dismissal-share bands in [tools/balance-bands.json](../../tools/balance-bands.json); WP21's review gate enforces them. Rookie scores more than Legend.

Findings go into `docs/playtests/<date>.md`; the next waves are chosen from that list.

**Status after the 16 October review:** Section 6 item 6 passes: 50 seeded T20s at each of five simulated Human profiles meet every unchanged band, and Rookie scores more than Legend; results are in [Step 134](../steps/step-134-cpu-stroke-acceptance.md). Item 2 has an automated ten-run repeatability check for identical delivery category, input timing, and seed, recorded in [Step 132](../steps/step-132-cpu-stroke-scoring.md); the requested human repeat remains open. Items 1, 3, 4, and 5 still need a human playtest report. The WP18, WP19, and WP20 visual sign-offs remain open for a person to inspect the actual game and record findings. Use the [pending human acceptance worksheet](../playtests/2026-10-16-pending-gates.md) to record these outcomes.

## 7. Notes on AI-generated 3D assets

AI 3D generation is useful for **props and stadium dressing** (WP20) and for **concept passes** of kit and equipment. Generated meshes come out as dense, unrigged, single-piece surfaces with baked lighting in the textures, so **player bodies stay on MPFB2/MakeHuman bases with our rig** (WP19). Generated gear (helmet, pads, gloves, bat) is retopologised, UV-cleaned and weight-painted onto that rig in Blender. Every generated asset records its generator, model licence, prompt and date in `assets/ASSET-ORIGINS.md`, and the game never calls a generator at runtime.
