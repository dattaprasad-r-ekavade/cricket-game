# Super Cricket development plan

## Goal

Build an original 3D cricket game with the presentation ambition of Cricket 07: readable stadiums, convincing player movement, broadcast cameras, responsive batting and bowling, fielding, and a complete match. Use AI for bounded coding, asset drafts, scripts, and analysis; keep the simulation and tools local, inspectable, repeatable, and independent of AI services.

## Where we are (7 October 2026)

Step 55 adds moment-to-moment feedback for contact quality and actual bounce, plus a speed-tinted ball trail. Automated checks and captures pass; the owner still needs to retest camera readability and batting/bowling feedback on keyboard, and GamePad play remains untested. A4a/A4b remain in progress; A4c's delivery-feedback items are implemented, with contact replay and guided practice still open. Implementation notes live in [docs/steps/step-notes-01-50.md](docs/steps/step-notes-01-50.md), [docs/steps/step-51-camera-and-feedback.md](docs/steps/step-51-camera-and-feedback.md), [docs/steps/step-52-batting-timing.md](docs/steps/step-52-batting-timing.md), [docs/steps/step-53-camera-discoverability.md](docs/steps/step-53-camera-discoverability.md), [docs/steps/step-54-pitch-map-feedback.md](docs/steps/step-54-pitch-map-feedback.md), and [docs/steps/step-55-immediate-delivery-feedback.md](docs/steps/step-55-immediate-delivery-feedback.md); Cricket 07 research is in [docs/reviews/cricket07-research.md](docs/reviews/cricket07-research.md).

| Area | Rating | State |
| --- | --- | --- |
| Simulation and rules | 7/10 | 120 Hz ball flight, swept bat contact, sweet-spot impact, two-innings match, CPU batting/bowling/running/field placement, seeded physics match batches |
| Tooling | 8/10 | Validators, analyzers, deterministic capture and profiling, `tools/review.ps1` |
| Architecture | 6/10 | `Simulation`/`Content` are graphics-free (only `System.Numerics`, `System.Text.Json`); `Game1.cs` is ≈2,250 lines with ≈130 fields; checks live in production assemblies |
| Visual fidelity vs Cricket 07 | 2/10 | Flat-colour capsule players, procedural stadium, static crowd, no shadow maps or post-processing |
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

**Playtest 2 (keyboard-only follow-up, 7 Oct 2026):** the batting and bowling camera view felt badly framed and too distant; batting/bowling feedback was still not visible. The tester did not report whether the Step 50 controls were understandable, so that question remains open. Step 51 adds closer role-based cameras and a persistent outcome card; human retest is pending. Full notes: [docs/playtests/2026-10-07.md](docs/playtests/2026-10-07.md).

**A4a — Controls redesign** (Cricket 07 model: *direction + shot type*, in progress)
- [x] Batting uses left/right direction for placement and two shot buttons: Space/A defends with no direction or drives with direction; Shift/Y lofts. This replaces A/S/D, J/L, and Q/E as primary controls.
- [ ] Choose front/back foot automatically for human batting from delivery line and length; manual footwork remains an advanced/debug option.
- [x] Running is one button: press to run, press again to queue another, and hold to turn back. Rookie cancels an unsafe queued follow-up run.
- [x] Bowling cycles delivery types with C/LB and moves a visible pitch target with arrows, D-pad, or the left stick; the next delivery's simulated bounce reaches the selected point.
- [ ] Replace delivery presets with a pace/accuracy meter.
- [x] Developer keys (T, 1–4 presets, F1, orbit camera, Page Up/Down) move behind `--debug` and leave the normal HUD.
- [x] The live HUD shows only phase-specific actions, labelled for the last-used keyboard or GamePad. The full control list moves to the pause menu.

**A4b — Teach batting**
- Interactive tutorial or practice nets: bowl five slow balls with an on-screen timing prompt, then remove the prompts.
- A timing aid: a ring or marker where the ball will pitch, plus a contact-zone indicator that shrinks with difficulty (Rookie shows it, Pro hides it).
- Make shot types distinct in outcome and animation so the player learns cause and effect.
- Slower default pace on Rookie and in the first match.

**A4c — Delivery and shot feedback**
- [x] After each completed ball, show a persistent result card with release speed (km/h), measured pitch length/line, shot/contact quality, and score or wicket.
- [x] When the human bowls, show the selected target, measured landing, and distance from aim.
- [x] Add calibrated early/perfect/late timing feedback from measured best-contact delays; the result card reports the timing band and offset.
- [x] Add a pitch map with the actual bounce and, for human bowling, the intended target marker.
- A short automatic replay of the contact moment from the behind-striker camera (skippable), using the existing deterministic capture state.
- [x] Immediate in-world feedback: a bat-contact flash scaled by quality, ball-trail colour by speed, and the actual pitch spot marked briefly.

**A4d — Gameplay cameras**
- [x] Tighten the default broadcast to 22 m, behind-striker to 16 m, bowler-end to 18 m, and square-leg to 27 m; frame the bowler-end target marker centrally.
- [x] Select batting/bowling camera by player role for each delivery and follow the ball after bat contact.
- [x] Show the camera shortcut in phase prompts; V cycles views on keyboard and left-stick click cycles views on GamePad.
- [ ] Confirm batting, bowling, and fielding readability in a human keyboard retest; controller retest remains open.

**Gate:** a new player understands the controls without the README within one over, can explain why a shot was early or late, and can describe the last delivery (pace, line, length) from the on-screen feedback. Phase 2's "repeated delivery practice is enjoyable" is answered yes or no with evidence.

### Milestone B — Engine decision trial (3–5 days, time-boxed)

MonoGame can render 2006-era graphics, but shadow maps, normal/PBR materials, MSAA, tone mapping, bloom, an animation blend tree, IK, and LODs would all be hand-written. Decide now, before more renderer investment.

- [ ] B1. Create a Godot 4 (.NET) trial branch that references the unchanged `SuperCricket.Simulation` and `SuperCricket.Content` projects.
- [ ] B2. Rebuild the stadium, pitch, one glTF character, and one delivery + shot in the trial, with shadows and post-processing.
- [ ] B3. Compare captures and hours spent against the MonoGame build; record the decision in `docs/reviews/`.

Unity (URP, Cinemachine, Animation Rigging) is the alternative if Godot's animation tooling falls short; it needs a `netstandard2.1` retarget and an older C# language version for the shared projects.

**Gate:** a written decision — stay on MonoGame or move the presentation layer — with side-by-side captures. Simulation, rules, Content, and Tools stay engine-independent either way.

### Milestone C — Asset and animation pipeline v2 (2–4 weeks)

The largest fidelity lever.

- [ ] C1. Adopt a standard humanoid rig (55–65 bones including fingers and wrists) and glTF/GLB as the runtime format (SharpGLTF if MonoGame stays). Retire `.scplayer.json` after migration; keep the validator and event/root-motion contract.
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
- [ ] Move `*ReviewChecks` into an xUnit test project; keep `review.ps1` for asset, capture, and game-host checks.
- [ ] Add GitHub Actions on Windows: build, tests, validators.
- [ ] Shorten the README: build/run, controls table, links to tool docs.

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
