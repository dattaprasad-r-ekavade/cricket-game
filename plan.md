# Super Cricket development plan

## Goal

Build an original 3D cricket game with the presentation ambition of Cricket 07: readable stadiums, convincing player movement, broadcast cameras, responsive batting and bowling, fielding, and a complete match. Use AI for bounded coding, asset drafts, scripts, and analysis; keep the simulation and tools local, inspectable, repeatable, and independent of AI services.

## Where we are (7 October 2026)

MonoGame remains the shipping host. Steps 69–77 complete the GLB runtime pilot, Windows CI, tool guide, partial-file split, xUnit migration, and initial HUD presenter. Step 78 adds slower Rookie/first-match CPU deliveries. Steps 79–86 extract match lifecycle, HUD presentation, and input normalization into headless, tested components; the latest pushed checkpoint passed local review and hosted Windows CI. `Game1` still owns much match orchestration and rendering. The human keyboard retest after Step 67 remains open; GamePad play, contact replay, and guided practice also remain open. C1 still needs a dedicated fielder rig, broader material/texture support, and legacy-format retirement. Keep gameplay additions behind the human retest and require human review of visual captures. See [Step 67](docs/steps/step-67-player-focused-camera-feedback.md), [Step 69](docs/steps/step-69-monogame-glb-player-runtime.md), [Step 70](docs/steps/step-70-windows-validation-workflow.md), [Step 71](docs/steps/step-71-readme-tool-reference.md), [Step 72](docs/steps/step-72-input-router.md), [Step 73](docs/steps/step-73-camera-director.md), [Step 74](docs/steps/step-74-game1-source-split.md), [Step 75](docs/steps/step-75-simulation-xunit-migration.md), [Step 76](docs/steps/step-76-headless-hud-presenter.md), [Step 77](docs/steps/step-77-test-hud-phase-resolution.md), [Step 78](docs/steps/step-78-rookie-and-first-match-pace.md), [Step 79](docs/steps/step-79-match-controller.md), [Step 80](docs/steps/step-80-score-status-presenter.md), [Step 81](docs/steps/step-81-delivery-feedback-presenter.md), [Step 82](docs/steps/step-82-pause-menu-presenter.md), [Step 83](docs/steps/step-83-pause-menu-layout.md), [Step 84](docs/steps/step-84-feedback-text-wrapping.md), [Step 85](docs/steps/step-85-aim-axis-normalization.md), [Step 86](docs/steps/step-86-contact-feedback-presentation.md), and earlier implementation notes in [docs/steps/step-notes-01-50.md](docs/steps/step-notes-01-50.md).

| Area | Rating | State |
| --- | --- | --- |
| Simulation and rules | 7/10 | 120 Hz ball flight, swept bat contact, sweet-spot impact, two-innings match, CPU batting/bowling/running/field placement, seeded physics match batches |
| Tooling | 8/10 | Validators, analyzers, deterministic capture and profiling, `tools/review.ps1` |
| Architecture | 6/10 | `Simulation`/`Content` are graphics-free; `Game1` is now divided into focused partial files but still shares one stateful class; graphics-free review checks are isolated in xUnit, while host-dependent checks stay in the Game review path |
| Visual fidelity vs Cricket 07 | 2/10 | MonoGame remains a flat-colour prototype; the Godot B2 trial now proves a low-detail GLB player, procedural stadium, shadows, and post-processing, but does not yet raise the game's fidelity |
| Animation | 2/10 | 13-bone rig, script-keyed clips, no hands/fingers, no IK |
| Audio | 1/10 | Procedural placeholder cues only |
| Game feel validated by a human | 1/10 | Steps 29–44 were built without running the game window |

**Diagnosis:** code is no longer the bottleneck — AI agents produce systems faster than they are validated. The gap to Cricket 07 is animation, character assets, rendering features, audio, and play-tested feel. Further gameplay systems will not close it.

**Fidelity outlook:** physics and batting simulation can exceed Cricket 07; rendering can match or exceed it with a modern engine feature set; animation variety and broadcast presentation are realistically 50–70% of Cricket 07 within about 12 months. Licensed teams and players stay out of scope.

## Working rules

1. **Play before building.** No new gameplay system starts until the previous one has been played by a human on keyboard and controller. Each playtest produces a dated note in `docs/playtests/` with a ranked issue list; the next steps come from that list.
2. **Visual work is reviewed visually.** Every art, animation, or camera step ends with a capture or short recording that a person has looked at.
3. **Commit before checking off.** A step is `[x]` only when its commit exists and the review script passes.
4. **Keep this file short.** Roadmap, gates, and current status only. Per-step notes go to `docs/steps/`.
5. **Bounded AI tasks.** Give agents a behaviour, acceptance conditions, and a reproduction scenario; one coherent change per task. Rigging, animation cleanup, bat-contact alignment, and visual inspection need human review. Track asset origin and usage rights; the game must never depend on an AI service at runtime.

## Roadmap

Ordered by dependency. Milestones A and B run first because their outcomes change everything after them.

### Milestone A — Playtest and stabilize the current build (1–3 days)

- [x] A1. Commit the outstanding Step 49 ball-follow camera work after the full review and capture pass.
- [x] A2. First human playtest (7 Oct 2026). Findings in [docs/playtests/2026-10-07.md](docs/playtests/2026-10-07.md).
- [x] A3. Record the ranked problems.
- [ ] A4. Fix the remaining control-learning and batting-timing problems below, then playtest the camera and result-card pass again.

**Playtest 1 findings (blocking, in priority order):**

1. **The controls can't be learned.** There are about 25 keys across batting, footwork, aim, running, delivery selection, camera, and result actions, all shown as one text block. Nobody could build a mental model of them.
2. **The batting mechanic isn't understood.** The player can't tell when to press, what the three shots do differently, or why a shot hit or missed.
3. **No feedback after a delivery.** Nothing communicates what the ball did (speed, line, length, where it pitched) or how the shot went (early/late, contact quality, edge or middle).

**Playtest 2 (keyboard-only follow-up, 7 Oct 2026):** the batting and bowling camera view felt badly framed and too distant; batting/bowling feedback was still not visible. The tester did not report whether the Step 50 controls were understandable, so that question remains open. Step 57 responds with tighter role-based cameras and larger player-focused live/result feedback; a human retest remains pending. Full notes: [docs/playtests/2026-10-07.md](docs/playtests/2026-10-07.md).

**A4a — Controls redesign** (Cricket 07 model: *direction + shot type*, in progress)
- [x] Batting uses left/right direction for placement and two shot buttons: Space/A defends with no direction or drives with direction; Shift/Y lofts. This replaces A/S/D, J/L, and Q/E as primary controls.
- [ ] Choose front/back foot automatically for human batting from delivery line and length; manual footwork remains an advanced/debug option.
- [x] Running is one button: press to run, press again to queue another, and hold to turn back. Rookie cancels an unsafe queued follow-up run.
- [x] Bowling cycles delivery types with C/LB and moves a visible pitch target with arrows, D-pad, or the left stick; the next delivery's simulated bounce reaches the selected point.
- [ ] Replace delivery presets with a pace/accuracy meter.
- [x] Developer keys (T, 1–4 presets, F1, orbit camera, Page Up/Down) move behind `--debug` and leave the normal HUD.
- [x] The live HUD shows only phase-specific actions, labelled for the last-used keyboard or GamePad. The full control list moves to the pause menu.

**A4b — Teach batting**
- [ ] Interactive tutorial or practice nets: bowl five slow balls with an on-screen timing prompt, then remove the prompts.
- [x] Show the live calibrated timing window and a moving, shot-timing marker while the human batter faces the released delivery (Step 62; Release review passed).
- [x] Add a projected pitch-point marker and a difficulty-scaled contact zone (Rookie shows the zone; Pro hides it) (Step 65; Release review passed).
- [ ] Make shot types distinct in outcome and animation so the player learns cause and effect.
- [x] Use a slower default pace on Rookie and in the first match (Step 78; apply 0.82 to CPU deliveries faced by a human; retain authored preset values; hosted Windows validation passed).

**A4c — Delivery and shot feedback**
- [x] After each completed ball, show a compact persistent lower-right result card with release speed, pitch length/line and distance, shot/contact quality/timing, score or wicket, and a pitch map; keep the live role banner clear of the phase HUD (Step 66).
- [x] When the human bowls, show the selected target, measured landing, and distance from aim.
- [x] Add calibrated early/perfect/late timing feedback from measured best-contact delays; the result card reports the timing band and offset.
- [x] Add a pitch map with the actual bounce and, for human bowling, the intended target marker.
- A short automatic replay of the contact moment from the behind-striker camera (skippable), using the existing deterministic capture state.
- [x] Immediate in-world feedback: a bat-contact flash scaled by quality, ball-trail colour by speed, and the actual pitch spot marked briefly.

**A4d — Gameplay cameras**
- [x] Frame the full delivery in the role views. Step 61 widened and centered both role views; Step 64 tightened them to 16 m / 38°; Step 66 moved to a 12.5 m / 45° slightly off-axis view. Page Up/Down and V retain zoom and alternate views.
- [x] Let keyboard users adjust camera distance with Page Up/Down and mouse-wheel zoom during normal play.
- [x] Select batting/bowling camera by player role for each delivery and follow the ball after bat contact.
- [x] Show the camera shortcut in phase prompts; V cycles views on keyboard and left-stick click cycles views on GamePad.
- [x] Step 64 — revise the role camera framing and move the detailed result card away from the foreground player; Release captures and full review passed.
- [x] Step 66 — move role views closer and make live batting/bowling feedback prominent without covering the phase HUD; Release captures and full review passed.
- [x] Step 67 — focus role cameras on the active player, make zoom direction explicit, follow the bowler then ball, and center live feedback below the scoreboard; Release review and captures passed.
- [ ] Confirm batting, bowling, and fielding readability in a human keyboard retest after Step 67; controller retest remains open.

**Gate:** a new player understands the controls without the README within one over, can explain why a shot was early or late, and can describe the last delivery (pace, line, length) from the on-screen feedback. Phase 2's "repeated delivery practice is enjoyable" is answered yes or no with evidence.

### Milestone B — Engine decision trial (3–5 days, time-boxed)

MonoGame can render 2006-era graphics, but shadow maps, normal/PBR materials, MSAA, tone mapping, bloom, an animation blend tree, IK, and LODs would all be hand-written. Decide now, before more renderer investment.

- [x] B1. Create a Godot 4 (.NET) trial branch that references the unchanged `SuperCricket.Simulation` and `SuperCricket.Content` projects.
- [x] B2. Rebuild the stadium, pitch, one glTF character, and one delivery + shot in the trial, with shadows and post-processing. See [Step 58](docs/steps/step-58-godot-trial-visuals.md).
- [x] B3. Correct the shared shot axis and CPU placement, compare current captures and bounded effort evidence against MonoGame, and record the decision to keep MonoGame as the shipping host in [the engine comparison](docs/reviews/godot-engine-comparison.md). See [Step 60](docs/steps/step-60-shot-axis-and-engine-decision.md).

Unity (URP, Cinemachine, Animation Rigging) is the alternative if Godot's animation tooling falls short; it needs a `netstandard2.1` retarget and an older C# language version for the shared projects.

**Gate:** a written decision — stay on MonoGame or move the presentation layer — with side-by-side captures. Simulation, rules, Content, and Tools stay engine-independent either way.

### Milestone C — Asset and animation pipeline v2 (2–4 weeks)

The largest fidelity lever.

- [ ] C1. Adopt a standard humanoid rig (55–65 bones including fingers and wrists) and glTF/GLB as the runtime format (SharpGLTF if MonoGame stays). Retire `.scplayer.json` after migration; keep the validator and event/root-motion contract.
  - [x] Step 63 pilot: author a 61-bone Blender batter and verify its imported joints and grip animation in the Godot trial.
  - [x] Step 68: add the matching bowler GLB and validate embedded gameplay events/root-motion for both roles.
  - [x] Step 69: load batter and bowler GLBs through SharpGLTF in the MonoGame runtime; full Release review passes. Fielders still reuse the bowler rig; textured-material support and `.scplayer.json` retirement remain open.
- [ ] C2. Build proper characters: MPFB2/MakeHuman (CC0 output) or Character Creator base bodies, textured kits with team colour masks, helmet, pads, gloves, bat. AI 3D generation is for props and stadium dressing only.
- [ ] C3. Video-to-mocap pipeline: record cricket movements (own footage or footage with usage rights), solve with Move.ai / Rokoko Vision / DeepMotion / QuickMagic, retarget and clean in Blender (batch via Blender MCP), and author `sc_events` markers.
- [ ] C4. Two-bone IK for hands on the bat and foot planting; animation blending driven by the existing contact and footwork systems.
- [ ] C5. First clip set (≈40 clips): 8 shots × front/back foot × off/leg, bowling run-up and delivery, fielding run, pickup, throw, catch, dive, celebration, idle variations.
- [ ] C6. Recalibrate `analyze-batting-practice` contact windows against the new clips.

**Gate:** the batter, bowler, and fielders read as people, not capsules, in a close behind-striker capture; every shot still finds contact in the analyzer; a recorded over shows no foot sliding or bat-hand separation.

### Milestone D — Presentation pass (3–6 weeks)

- [ ] D1. Lighting: shadow maps for players and stadium, textured/normal-mapped materials, MSAA, tone mapping, restrained bloom.
- [ ] D2. Stadium: authored Blender stadium with textured stands, sight screens, advertising boards (fictional), LODs; animated crowd via impostors or simple skinned instances.
- [ ] D3. Cameras: broadcast delivery cam, fielding cam, replay system (record simulation state, replay with alternate angles), cuts between them.
- [ ] D4. Audio: CC0/licensed bat, ball, stump, and crowd ambience with reactions to boundaries and wickets; short phrase-based commentary later.
- [ ] D5. Readable broadcast-style scoreboard and HUD; move the control help off the main screen.

**Gate:** capture a whole over at the intended visual level with consistent movement and cameras; stable 60 fps at the agreed resolution on the Lenovo IdeaPad S145 baseline with CPU and GPU time measured.

### Milestone E — Complete short match and release candidate

The match loop, rosters, CPU opponent, difficulty, settings, controller mapping, and accessibility toggles already exist (Steps 27–48). What remains:

- [ ] E1. Balance pass driven by playtest notes rather than seeded batches alone.
- [ ] E2. Front-end flow: title, team select, overs, difficulty.
- [ ] E3. Repeated external playtests (at least three people); fix control and camera confusion.
- [ ] E4. Package a reproducible build, test on a second machine, review logs.

**Gate:** playtesters complete matches repeatedly without debug intervention and can point to specific gameplay problems.

### Engineering tasks (run alongside A–E)

- [ ] Split `Game1` into match controller, presentation, input router, and camera director; target no file over ≈600 lines.
  - [x] Step 72: extract keyboard/controller state and edge tracking to `MatchInputRouter`; the match, presentation, and camera split remains open.
  - [x] Step 73: make the existing camera component's director role explicit and test its isolated behavior.
  - [x] Step 74: split `Game1` into focused partial files under 500 lines; class-level component extraction remains open.
  - [x] Step 76: extract phase-specific HUD hints and feedback-banner layout into a graphics-device-free `MatchHudPresenter`.
  - [x] Step 77: move HUD phase precedence into `MatchHudPresenter` and test all match/live phases without starting the host.
  - [x] Step 79: extract match lifecycle transitions, first-match tracking, and deterministic decision seeds into a graphics-free `MatchController`; presentation and input remain in `Game1`.
  - [x] Step 80: extract graphics-free scoreboard text formatting into `MatchHudPresenter` and test first-innings, chase, and completed-match output.
  - [x] Step 81: extract delivery-result card text and pitch labels into `MatchHudPresenter`; test batting contact, bowling accuracy, and full-toss cases without starting the host.
  - [x] Step 82: extract pause-menu control and settings copy into `MatchHudPresenter`; test normal, developer, and audio-unavailable states without starting the host.
  - [x] Step 83: extract pause-menu layout sizing into `MatchHudPresenter`; preserve normal sizing and fit the long developer menu inside a smaller viewport. Hosted Windows validation passed in [run 37580451887](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37580451887).
  - [x] Step 84: extract feedback-card line wrapping into `MatchHudPresenter`; preserve the current font-measured wrapping and verify continuation behavior without a graphics device. Hosted Windows validation passed in [run 37580959271](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37580959271).
  - [x] Step 85: move analog aim dead-zone and rescaling into `MatchInputRouter`; preserve current batting/bowling input and test clamping and non-finite values headlessly. Hosted Windows validation passed in [run 37581445032](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37581445032).
  - [x] Step 86: move contact-result labels and high-contrast colors into `MatchHudPresenter`; test quality thresholds and missing/miss states without starting the host. Hosted Windows validation passed in [run 37581972044](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37581972044).
  - [ ] Step 87: move ball-trail colors and difficulty-scaled contact-zone opacity into `MatchHudPresenter`; test speed fallbacks and difficulty levels headlessly.
- [x] Move graphics-free review checks into xUnit; keep `review.ps1` for asset, capture, and game-host checks.
  - [x] Step 72: add the xUnit project and its first three input tests; migrating the existing checks remains open.
  - [x] Step 75: migrate all graphics-free Simulation review checks and route the existing `verify-*` commands through filtered xUnit tests; host-dependent Game checks remain in the full review path.
- [x] Add GitHub Actions on Windows: build, tests, validators. Step 70's hosted run passes.
- [x] Shorten the README: build/run, controls table, links to tool docs. See Step 71.

### Deferred

Second stadium, career mode, spin catalogue, multiplayer, licensed material, MCP service over the CLI, and in-game editors stay deferred until Milestone E's gate passes.

## Technical foundation

| Area | Responsibility |
| --- | --- |
| Simulation | Ball flight, contacts, player movement, cricket rules, match state, CPU decisions; no graphics dependency |
| Content | Validated definitions for players, shots, deliveries, fields, teams, and stadiums |
| Game | Engine host: rendering, input, animation, audio, cameras, interface |
| Tools | Validate, analyze, simulate, capture, and run batch scenarios |
| Tests | Simulation, rules, serialization, and replay regressions |

Use metres, seconds, and kilograms. Document axis directions, handedness, origins, skeleton names, bind pose, clip naming, root motion, and animation events at the asset boundary (`docs/design/player-asset-contract.md`; update it for the glTF rig). Fixed-step 120 Hz simulation with render interpolation and swept collision for the ball. Seed randomness and record inputs/events for repeatable scenarios; do not promise bitwise determinism across platforms.

The CLI remains the lab (`validate`, `simulate`, `analyze-*`, `verify-*`, `--capture-frame`, `--profile-frames`, `tools/review.ps1`). Add editors only when iteration on a settled format needs them.

## Completed phases (summary)

| Phase | Status |
| --- | --- |
| 0 — Target and asset pipeline | Gate passed 6 Oct 2026: brief, reference board, asset contract, hardware baseline in `docs/design/` |
| 1 — Bowling and ball simulation | Prototype complete: standard, wide, no-ball, yorker presets; deterministic flight and bounce |
| 2 — Batting and animation | Mechanics complete (defence/drive/loft, footwork, aim, contact markers); **enjoyment gate open → Milestone A** |
| 3 — Fielding and a complete over | Rules complete; fielder movement, throws, and run-out cuts remain simplified heuristics |
| 4 — Visual target | Stylized prototype only → Milestones C and D |
| 5 — Short match | Systems complete; playtesting, balance, authored audio open → Milestones A and E |
| 6 — Stabilize | Not started → Milestone E |

## Effort estimate

With AI assistance, code throughput is high; art, animation, and playtesting set the calendar. For one developer substantially full-time: Milestones A–B in about 1 week, C in 2–4 weeks, D in 3–6 weeks, E in 3–6 weeks — roughly 3–4 months to a convincing playable slice and 9–15 months to a polished small game. Re-estimate after Milestone C.

## References

- Step history: [docs/steps/step-notes-01-50.md](docs/steps/step-notes-01-50.md)
- Reviews: `docs/reviews/2026-10-06.md`, `docs/reviews/2026-10-06-planning.md`, `docs/reviews/2026-10-06-live-match.md`
- Design: `docs/design/short-match-brief.md`, `docs/design/player-asset-contract.md`, `docs/design/hardware-baseline.md`
- [MonoGame 3.8.5 release](https://monogame.net/blog/2026-07-15-3.8.5-release-2026/)
- [glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.pdf)
- [Godot .NET / C# documentation](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html)
