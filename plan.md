# Super Cricket development plan

## Goal

Build an original 3D cricket game with the presentation ambition of Cricket 07: readable stadiums, convincing player movement, broadcast cameras, responsive batting and bowling, fielding, and a complete match. Build a focused set of production tools alongside the game, using AI for bounded coding, asset drafts, scripts, and analysis. Keep simulation and authoring tools local, inspectable, repeatable, and independent of AI services.

### Starting assumptions

- Windows desktop first; offline single-player; keyboard and controller.
- Fictional teams and one stadium until a playable match proves the core loop.
- The first playable milestone is a complete over. The first release candidate is a short limited-overs match.
- Multiplayer, career mode, licensed material, and a general-purpose engine are deferred until the small game is enjoyable and stable.
- Revisit these assumptions after the first playable over.

Visual fidelity depends on assets, rigging, animation, lighting, and camera work as well as rendering code. Prove animated characters early rather than treating polygon count as a proxy for quality.

## Progress

- [x] Step 1 — Bootstrap the Windows MonoGame application, pin the SDK and runtime versions, and verify a clean build.
- [x] Step 2 — Render a measured practice ground, marked pitch, stumps, ball placeholder, orbit camera, and debug overlay.
- [x] Step 3 — Add a fixed-step ball flight, pitch bounce, trajectory display, and a saved delivery preset.
- [x] Step 4 — Prove import and playback of one rigged player with two clips and a transition.
- [x] Step 5 — Add player-controlled batting, contact timing, and an inspectable first shot set.
- [x] Step 6 — Add fielding, running, wickets, and a complete over.
- [ ] Step 7 — Replace the practice scene with a representative stadium presentation and profile it.

Each completed step is recorded in its own commit and pushed to `origin/main`. The milestones below are the wider roadmap beyond this initial work package.

### Step 1 notes

- Created a Windows DirectX 11 MonoGame project and solution.
- Pinned MonoGame runtime/content packages and local content tools to 3.8.5.1.
- Added a .NET 9 SDK selection, build instructions, and repository ignore rules.
- Verified restore and build on Windows x64 with .NET SDK 9.0.302.

### Step 2 notes

- Added a metre-scaled practice ground with a 20.12 m pitch, 3.05 m width, crease markings, 0.71 m wickets, and a cricket-ball-sized placeholder.
- Added an orbit camera with keyboard, mouse-wheel zoom, and reset controls.
- Added a SpriteFont debug overlay with dimensions, camera values, ball position, FPS, and frame time.
- Built and launched the Windows game, captured and visually reviewed the rendered scene, and confirmed clean build output.

### Step 3 notes

- Added a graphics-independent, 120 Hz ball simulation with gravity, drag, tunable lateral acceleration, swept pitch/ground contact, bounce response, and rolling friction.
- Added a validated JSON delivery preset shared by the game and command-line tools.
- Added `validate` and `simulate` commands; simulation writes a CSV containing position, velocity, speed, bounce count, and motion phase for each fixed step.
- Repeated the headless run and confirmed the trajectory CSVs match exactly. The standard 123 km/h preset bounces at approximately 0.492 s and z = -5.62 m.
- Connected the game to the same simulation, drawing the moving ball and its trajectory. Space pauses; R restarts the delivery.
- Built the solution, ran both tool commands, launched the game with the copied preset, and visually reviewed the trajectory in the scene.

### Step 4 notes

- Added a dedicated content project with a versioned, validated player asset format and a `validate-player` command.
- Added a Blender 5.2 script that generates an editable `.blend` batter and exports the game's skinned mesh, 13-bone rig, material colors, and two 30 Hz animation clips.
- Added the exported practice batter to the game assets: 21 material-separated mesh parts, `practice-stance`, and `front-foot-drive`.
- Added animation sampling and a 0.35 s pose crossfade using MonoGame's GPU `SkinnedEffect`; T advances to the next clip.
- Regenerated and validated the player asset, built the solution without warnings, launched the game, zoomed in on the player, and confirmed the second clip displays.

### Step 5 notes

- Added a validated, editable shot set for defence, drive, and loft; `validate-shots` checks the authoring data from the command line.
- Added shot input, animation selection, a swept ball segment against the animated bat blade, and a first outgoing-velocity model driven by shot angle and speed transfer.
- Corrected the authored bat so the blade extends below the grip and can meet the post-bounce ball. Pose/path analysis showed a usable timing window; in the running game all three shot types produced contact, while an immediate drive produced a miss.
- Rebuilt the Blender asset and verified the player export, shot configuration, delivery preset, and solution build. The current contact model is a first playable approximation; edges, contact-offset response, fielding, and innings rules remain future work.

### Step 6 notes

- Added a single-over scoreboard with legal-ball progression, batter ends, strike swaps, extras, wickets, and next-batter replacement. The validated delivery result format rejects inconsistent extra and dismissal combinations.
- Added ten fielding positions including a wicketkeeper, bounded fielder movement and reaction time, swept pickup/catch checks, and boundary classification for batted balls.
- Added running between wickets, cancellation, and a visible fielder-to-wicketkeeper throw. A runner still short when the ball is received is dismissed run out; bowled and caught dismissals also update the over and stump presentation.
- Added playable standard, wide, and no-ball presets. The CLI `simulate-over` tool replays a JSON scenario containing dot balls, batter runs, a wide, a no-ball, a bye, a bowled dismissal, and a run-out.
- Verified the sample scenario completes six legal balls at 10/2 in 1.0 overs, validated the player, shot, and extra-delivery assets, built the full solution with zero warnings, and reviewed an in-game run-out capture. This is a functional prototype; fielders, throws, and wicket breaks use simple game-ready approximations pending the stadium/animation pass.

## Technical foundation

Use C# and MonoGame. Keep the first platform small and validate the graphics backend before investing in renderer features. Use Blender for source models, rigs, animation, and stadium authoring. The runtime asset route still needs to be proven: custom MonoGame content pipeline processing or an external-format importer. If using glTF/GLB, test skinning, animation, materials, coordinate conversion, and deployment explicitly.

Organize code around responsibilities as the implementation grows:

| Area | Responsibility |
| --- | --- |
| Simulation | Ball flight, contacts, player movement, cricket rules, match state; no graphics dependency |
| Game | MonoGame host, rendering, input, animation, audio, cameras, interface |
| Content | Validated definitions for players, shots, deliveries, fields, and stadiums |
| Tools | Import, validate, preview, tune, capture, and run batch scenarios |
| Checks | Useful simulation, rules, serialization, and replay regression scenarios |

Use metres, seconds, and kilograms in simulation data. Document axis directions, handedness, origins, coordinate conversion, skeleton names, bind pose, clip naming, root motion, materials, and animation events at the asset boundary. Keep authoring data versioned, human-readable, validated, and consumed directly by the game.

Use a fixed-step simulation (start at 120 Hz and measure) with render interpolation. Handle a fast, small ball using swept collision or time-of-impact logic; a high tick rate alone does not prevent tunnelling. Seed randomness and record inputs/events for repeatable local scenarios. Do not promise bitwise determinism across platforms.

## Phase 0 — Establish the target and asset pipeline

Deliver a short design brief for batting, bowling, camera feel, difficulty, and initial match mode; a visual reference board for proportions, lighting, surface detail, animation, and UI readability; and a running scene with ground, pitch, ball, camera, and debug measurements. Import one Blender player, play two clips, and prove a transition. Document the asset contract and choose the model import route with a real test asset.

Build `content validate` to catch missing textures, unsupported materials, scale errors, missing bones, and malformed animation metadata. Capture the representative target scene and record a frame-time baseline on named hardware. Establish the minimum hardware target before art production scales up.

**Gate:** a clean checkout builds and displays the same animated player without manual repair of exported assets. Record a short capture of the intended look.

## Phase 1 — Bowling and ball simulation

Build a practice delivery with a placeholder bowler. Add release position/velocity, gravity, drag, tunable swing/spin, pitch bounce with tangential response, rolling, ground and wicket contacts, and initial pace and spin deliveries. Show release speed, bounce point, and trajectory.

Build a Delivery Lab with pause, slow motion, single-step, trajectory trails, tunable parameters, and saveable presets. Add a headless command for seeded delivery batches and exported outcomes. Tools must call the same simulation as the game.

**Gate:** a saved preset reproduces its trajectory in the supported build; parameters change outcomes predictably; the ball cannot tunnel through the pitch or stumps at supported speeds.

## Phase 2 — Batting and synchronized animation

Add a compact first shot set: defence, drive, and lofted shot. Add intent and timing input, stance, footwork, bat attachment, animation blending, recovery, and markers for release, contact window, foot plant, and recovery. Resolve misses, edges, contact, and bowled dismissals.

Keep contact authored and tunable: determine eligibility from ball and bat position, then derive the outgoing ball from incoming velocity, bat motion, contact offset, and shot parameters. Make assistance bounded, visible in debug mode, and repeatable. Build an Animation and Shot Lab for clip scrubbing, bone/bat overlays, contact markers, presets, and automated playback against deliveries.

**Gate:** the player can intentionally defend, drive, and loft; timing and positioning differences are clear; contact aligns from both gameplay and replay cameras. Repeated delivery practice is enjoyable before expanding the shot catalogue.

## Phase 3 — Fielding and a complete over

Add wicketkeeper, non-striker, and a small functional field. Implement interception, movement limits, pickup, catch, throw, receiving, wicket breaks, running, turning, cancellation, and run-outs. Add boundaries, extras supported by the bowling mechanics, strike changes, and over progression.

Build a Field Lab to place players, inspect interception predictions and reachable areas, and save field presets. Add repeatable scenarios for catches, boundaries, close run-outs, overthrows, extras, and strike changes.

**Gate:** play six legal deliveries, resolve extras and supported dismissals, update score and striker correctly, and return to a stable state after every ball.

## Phase 4 — Demonstrate the visual target

Create one cohesive representative art set: stadium, pitch, outfield, stands, restrained crowd, polished rig with kit and equipment, and the animations needed for the full over. Add textured materials, sunlight, ambient lighting, a measured shadow solution, restrained post-processing, readable ball presentation, and mesh detail reduction where profiling supports it. Add broadcast delivery, ball-follow, fielding, and replay camera presets, plus impact and crowd audio and a readable scoreboard.

Build a Stadium and Presentation panel for placement, camera bookmarks, lighting presets, and capture. Begin with Blender-authored layout and overrides; expand into a dedicated editor only when iteration needs it.

**Gate:** capture an entire over at the intended visual level, with consistent movement and cameras. Measure CPU/GPU frame times and animation cost with the full fielding side on the target hardware. Aim for stable 60 fps at the agreed resolution and tune from measurements.

## Phase 5 — Complete short match

Add two fictional teams, player attributes and batting order, innings, a target chase, match results, restart, and a compact overs selection. Add opponent decisions for bowling variation, shots, running, and field placement. Provide understandable difficulty, pause/settings, controller mapping, audio controls, and essential accessibility options. Save settings and match state if required by the release scope.

Build roster and tuning editors around validated files. Run automated matches to find stuck states, invalid scores, impossible transitions, and matches that fail to finish. Keep human playtesting for control feel and fun.

**Gate:** complete a match repeatedly from start to results without debug intervention. Playtesters understand controls and can point to specific gameplay problems.

## Phase 6 — Stabilize and choose expansion

Package a reproducible release candidate, test on another machine, profile representative matches, and review logs and asset validation. Prioritize animation glitches, ball visibility, control latency, camera confusion, and rules. Consider additional stadiums, teams, formats, multiplayer, career systems, and advanced presentation only after the small release is enjoyable and stable.

## Tool priorities

1. Asset validator and importer: prevent export, scale, material, and skeleton failures.
2. Delivery Lab and headless scenarios: make ball tuning measurable and repeatable.
3. Animation and Shot Lab: inspect the critical movement/contact dependency.
4. Field Lab: inspect positioning, reach, and interception.
5. Replay and capture commands: reproduce defects and support visual review.
6. Stadium, roster, and presentation editors: speed production after formats settle.

Use one shared in-game debug UI shell. Hot-reload tuning data first; reload models and shaders once resource replacement is safe. Start with an explicit local CLI that returns structured results for commands such as validating content, running scenarios, comparing metrics, and capturing a frame. Add a service/MCP interface only when a repeated AI workflow justifies it. Keep output paths, errors, and changes reviewable.

## AI-assisted workflow

Give AI bounded tasks with desired behaviour, related code/schemas, examples, acceptance conditions, and a reproduction scenario. Have it implement one coherent change; compile, run the scenario, and inspect before dependent work. Good tasks include C# features, debug panels, import scripts, build automation, shader drafts, Blender batch/export checks, texture or concept drafts, simulation-result summaries, documentation, and scenario generation.

Review generated code and validate generated assets. Rigging, animation cleanup, bat contact alignment, and visual inspection remain part of asset production; do not plan on finished cricket animation appearing from a prompt without iteration. Track asset origin and usage rights; use fictional branding and original assets initially. Game simulation must never depend on an AI service.

## Validation and rhythm

Keep focused scenarios for fast-ball collision, known bounce, early/late/missed batting, boundary classification, catches, run-outs, extras, over completion, and innings completion. Add scenarios when new mechanics or defects warrant them. For every milestone, retain a runnable build, short capture, known limitations, and measured performance. Run regular play sessions and choose the next work from observed problems. Maintain backlog groups for current, next, and deferred work.

## Effort estimate and first package

For one developer working substantially full-time with AI assistance, allow roughly 3–6 months for a convincing playable slice and 9–18+ months for a polished small game. This is an uncertain planning estimate; experience, asset availability, animation, and scope can move it substantially. Re-estimate after the first animated import and playable over. Part-time work stretches the calendar estimate.

Initial work package:

1. Scaffold the solution, pin dependencies, and establish build/run commands.
2. Render the ground, pitch, ball, controllable camera, and debug measurements.
3. Import one animated player and prove a clip transition.
4. Implement fixed-step ball flight, pitch collision, and trajectory visualization.
5. Save and replay one delivery preset through an initial Delivery Lab.

The initial milestone is an animated practice delivery with a reliable asset pipeline and observable simulation.

## References

- [MonoGame content pipeline overview](https://docs.monogame.net/articles/getting_to_know/whatis/content_pipeline/CP_Overview.html)
- [MonoGame configurable effects](https://docs.monogame.net/articles/getting_to_know/whatis/graphics/WhatIs_ConfigurableEffect.html)
- [MonoGame 3.8.5 release](https://monogame.net/blog/2026-07-15-3.8.5-release-2026/)
- [glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.pdf)
