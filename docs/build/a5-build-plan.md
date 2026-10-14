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
        WP9b/WP10b umpiring and run-out integration (needs 9,10,17) │
Wave 3  WP11 replays (needs 17 + wave 2)   WP12 practice nets (3,4,6,7b)
        → human playtest gate (section 6)
Art track (parallel throughout): WP18 clip set, WP19 characters, WP20 props/stadium
```

Rough sizes for one agent: S = under half a day, M = 1–2 days, L = 3–5 days.

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

### WP6 — Timing gauge and feedback channels (M, wave 2, owns `MatchHudPresenter.cs`) · A5.6

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

### WP7b — Bowling controls (M, wave 2, owns `Game1.MatchActions.cs`)

- One key per available delivery (1–8 / face buttons with a page toggle); pressing it locks the target and starts the run-up in a single step. Arrows move the target before the press.
- Show the shrinking target ring, the meter in the stride, and "NO BALL" when it happens. The CPU bowler uses the same model with its own target choice.
- Bowler change at the end of an over: a list with each bowler's stamina and overs bowled (presenter + simple panel).

**Accept:** router/presenter tests; the CPU and human paths both pass through `BowlingAccuracyModel`; a capture of the meter.

---

### WP8 — Field presets and picker (M) · A5.10

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

WP9b/WP10b (wave 2): wire both into the live host, umpire close-up camera preset (`CameraDirector`), decision banner. Small; can be one agent.

---

### WP11 — Replays (L, wave 3) · A5.7

- `ReplayRecorder` (Game): a ring buffer of the last 12 s of render state per tick (ball, all player poses as clip id + time, camera). It records what was rendered, so it doesn't re-simulate.
- `ReplayScript` asset: per event type (bowled, caught, LBW, run-out, six, near run-out) a list of shots: camera preset, start offset from the event, duration, speed (1.0 / 0.5 / 0.25), overlay (`StumpLineStrip` for LBW using `UmpireDecision.x/h`; `CreaseZoom` for run-outs).
- Plays automatically after a wicket or six; skippable with any key. End-of-innings "fall of wickets" reel replays each wicket with a caption ("FOW 18/6, Name run out 0").

**Accept:** recorder tests (buffer wrap, timestamps); script validator; capture of an LBW replay with the strip; skip works.

### WP12 — Practice nets as the tutorial (M, wave 3) · A5.8

- Mode from the title menu: choose bowler style, line, length, pace; ball repeats until changed.
- First session: three guided steps with on-screen prompts (1. "press S as the ball reaches the yellow band", 2. "add a direction", 3. "hold Shift to loft"), each passed after three Ideal-zone hits. Then prompts disappear.
- Bowling drill: hit a target ring five times.
- Stats panel: last 10 balls' zones and a histogram.

**Accept:** prompt progression tests in a presenter; a human can finish the tutorial in under three minutes (playtest item).

---

### WP13 — CPU stroke scoring (M) · E1

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

### Art track (parallel; human visual review required for each)

- **WP18 — Clip set (C5, about 40 clips).** Author one side, mirror the other. Batting: 8 directions × front/back × attack, 4 defends, 4 lofts, leave, beaten, body hit. Bowling: pace run-up + delivery, spin run-up + delivery. Fielding: run, stop-and-pickup, dive left/right, catch grid (low / chest / high × stationary / running), throw (flat, underarm flick), wicket-break gather. Keeper: stand, take, stumping. Umpire: out, four, six, wide, no-ball. Every batting clip carries `sc_events` contact markers; `analyze-batting-practice` must find contact for each.
- **WP19 — Characters (C2).** MPFB2/MakeHuman CC0 base bodies (3 builds), our 61-bone rig, kit with team-colour masks, pads, gloves, helmet, bat; 3 LODs. See section 7 on AI generation.
- **WP20 — Props and stadium dressing (D2).** Stumps and bails (breakable), bats, helmets, sight screens, fictional boards, stands modules, scoreboard, roller, umbrellas, crowd impostor cards.

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
6. Batch: 50 seeded T20s per difficulty show runs per over 6–9 (Normal), LBW 8–18% and run-outs 5–12% of dismissals.

Findings go into `docs/playtests/<date>.md`; the next waves are chosen from that list.

## 7. Notes on AI-generated 3D assets

AI 3D generation is useful for **props and stadium dressing** (WP20) and for **concept passes** of kit and equipment. Generated meshes come out as dense, unrigged, single-piece surfaces with baked lighting in the textures, so **player bodies stay on MPFB2/MakeHuman bases with our rig** (WP19). Generated gear (helmet, pads, gloves, bat) is retopologised, UV-cleaned and weight-painted onto that rig in Blender. Every generated asset records its generator, model licence, prompt and date in `assets/ASSET-ORIGINS.md`, and the game never calls a generator at runtime.
