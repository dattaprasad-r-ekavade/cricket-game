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
- [x] Step 7 — Replace the practice scene with a representative stadium presentation and profile it.
- [x] Step 8 — Make field formations editable assets and build a Field Lab coverage analyzer.
- [x] Step 9 — Set up Blender MCP for local art iteration, detail the rigged batter kit, and export edited scenes repeatably.
- [x] Step 10 — Add directional daylight, soft contact shadows, and deterministic game-frame capture.
- [x] Step 11 — Generate and prepare seamless grass and pitch textures, then render them on measured oval and pitch meshes.
- [x] Step 12 — Populate the stadium bowl with a restrained static crowd and keep the crowd mesh on a reusable GPU buffer.
- [x] Step 13 — Create a Blender-authored bowler and synchronize his run-up, delivery animation, and ball release.
- [x] Step 14 — Author a bowling run cycle, derive approach timing from the clip, and add deterministic run-up capture.
- [x] Step 15 — Export validated animation events from Blender and use the bowler's authored release marker in gameplay.
- [x] Step 16 — Export character-local root motion from Blender and drive the bowler's approach from its authored path.

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

### Step 7 notes

- Replaced the open practice horizon with an original procedural oval stadium: a 15-row colored seating bowl, concourse and outer facade, boundary rope and board ring, in-world score screen, and four floodlight towers. Added a compact match HUD and made the full developer overlay optional with F1.
- Added broadcast, behind-striker, bowler-end, and square-leg camera presets. V cycles views, Home returns to broadcast, and the existing orbit/zoom controls remain available.
- Added elapsed frame, FPS, CPU Update/Draw timing, and submitted scene-vertex counters to the developer overlay. On the current Windows machine, one settled-scene sample at the square-leg view reported 140 FPS, 6.7 ms elapsed frame time, 0.01/0.61 ms Update/Draw, 63,594 stadium vertices, and 2,160 fielder vertices.
- Rebuilt with zero warnings, reran the one-over scenario, launched and visually checked broadcast and behind-wicket views, and captured the profiling overlay. This is still a low-detail vertex-colored prototype: it has no crowd animation, textured materials, shadows, or separately measured GPU time; those remain part of the art and target-hardware pass.

### Step 8 notes

- Moved the ten non-bowler fielding positions into the versioned `practice-attack.json` preset, with unique position names, one wicketkeeper, boundary checks, and finite world-space coordinates. The game now loads and resets from that same file.
- Added `validate-field` and `analyze-field` CLI commands. The analyzer samples the field on a configurable grid, estimates the fastest fielder from reaction time, movement speed, and pickup radius, and exports per-point CSV data with summary coverage counts.
- The 2 m sample contains 1,373 in-boundary points: 19.2% are estimated reachable within 1 s, 57.1% within 2 s, and the 95th-percentile reach estimate is 3.623 s. Repeated runs produced the same CSV hash. A 9-player preset is rejected with a clean exit code 1.
- Built the solution, validated the preset, ran the analyzer at both 2 m and 3 m spacing, launched the game with the data-driven formation, and reran the practice over. Reach times are straight-line estimates; they do not model other fielders' collisions, terrain, or a moving ball's interception window.

### Step 9 notes

- Installed Blender MCP 1.8 / protocol 13 into the active Blender 5.2 portable profile and enabled it in Blender preferences. Configured the Codex MCP server for loopback on `127.0.0.1:9876` with safe mode enabled, then verified the live handshake and used scene inspection, visual review, and Blender code execution tools.
- Added 64 skinned batter-kit details through Blender MCP: helmet cage and hardware, face details, jersey collar and fictional crest, sleeve bands, pad ribs and straps, shoe soles and laces, glove details, and bat grain and grip wraps. Every part is marked for the game exporter and weighted to an existing rig bone.
- Added `--from-scene` to the Blender export script so MCP or artist edits in the saved `.blend` can be exported without rebuilding the starter model. It preserves the five standard clips in the source file and validates the exported 13-bone, 85-mesh, five-clip player asset.
- This is a more readable stylized kit, not the final fidelity target; it still uses simple geometry and per-mesh diffuse colors without production textures or polished animation.

### Step 10 notes

- Added face normals to the procedural stadium mesh and lit it with a warm directional key plus ambient fill. Ball, fielder markers, seating, and the ground now share the lit world effect; trajectory lines remain unlit for contrast.
- Added soft alpha contact shadows below the batters, fielders, and ball. Ball shadows spread and fade as the ball rises; batter and fielder shadows sit slightly above the pitch and outfield surfaces to avoid depth fighting.
- Added `--capture-frame <path.png>` to launch the DirectX game at the paused release frame, render one normal game frame into a target, save it as PNG, and exit. `--camera` selects any of the four match presets. Captured and visually reviewed `artifacts/step10-lit-stadium.png`.
- Release build completed with zero warnings or errors. The behind-striker capture produced a 1440x900 PNG, and two repeated captures produced identical SHA-256 hashes after capture mode froze the player's first-frame animation. The sample over still completes at 10/2 and the player asset validates. The stadium remains vertex-colored; contact shadows are soft ground decals, with cast stadium shadows, textured surfaces, and post-processing still ahead.

### Step 11 notes

- Generated original repeatable green turf and compacted cricket-clay textures, then added `tools/prepare_texture.py` to resize square source images, feather opposite edges, and report the maximum wrapped RGB difference. Both shipped 256×256 tiles have matching opposite edges.
- Replaced the rectangular vertex-colored lawn and plain pitch quad with UV-mapped textured surfaces: an oval 42 m-radius outfield with restrained mowing bands and the regulation-size pitch. The meshes use wrap sampling and share the warm directional lighting; crease lines and soft ground decals still render above them.
- Added both textures to the game output and captured a paused 1440×900 behind-striker frame at `artifacts/step11-textured-surfaces.png`. Visual review confirms the oval surface, turf texture, clay pitch, wickets, and crease markings render together without a rectangular lawn or surface z-fighting.
- Release build completed with zero warnings or errors. The next visual pass still needs higher-detail stadium architecture, cast-shadow treatment, and camera-dependent texture filtering or mipmaps for close views.

### Step 12 notes

- Added 4,800 low-poly spectator silhouettes across the existing 15-tier oval bowl, with staggered rows and deterministic muted shirt, skin, and hair colors. The static crowd totals 201,600 vertices (67,200 triangles) and sits in front of the authored seat tiers.
- Uploaded the crowd once to a write-only vertex buffer and draw it with a flat vertex-color effect. This removes per-frame crowd geometry uploads and avoids specular glare on the small silhouettes. The developer overlay reports spectator and scene vertex counts.
- Captured and reviewed 1440×900 broadcast and behind-striker views at `artifacts/step12-crowd-broadcast.png` and `artifacts/step12-crowd-unlit.png`. Two repeated behind-striker captures matched byte-for-byte (SHA-256 `2A3FC7F90DCDDC6CA0C554DB55D897CEEBBD217358AA2E32A6F4E26C02DDFB3F`).
- Release build completed with zero warnings or errors. The crowd is a static distance treatment: it has no individual animation, gesture changes, or detailed facial meshes yet.

### Step 13 notes

- Used the connected Blender MCP workflow to save a separate `practice-bowler.blend` from the player scene, remove batter equipment, recolor the kit, and author an `overarm-delivery` action. The saved source exports a 13-bone, 36-mesh player with six clips; the exporter now supports extra named actions and takes the asset name from the armature.
- Added a visible 1.35 s run-up and follow-through at the bowling end. The game starts the ball simulation and its trail on the clip's authored frame-21 release (20/30 s after the delivery action begins), hides the ball before release, and draws the bowler's shadow. Capture mode shows a repeatable release pose.
- Captured and visually reviewed 1440×900 broadcast and behind-striker frames at `artifacts/step13-bowler-broadcast.png` and `artifacts/step13-bowler-behind.png`. The capture path loads the new player asset; repeated behind-striker captures matched byte-for-byte (SHA-256 `24626A527B4FA933FDA1FA290B694CA766E09A42B1D6D5AF7EDA2BFF6DACAC0B`).
- Release build completed without warnings or errors; batter and bowler exports validate, and the sample over still completes at 10/2. This is an early stylized bowler: the run-up uses the existing between-wickets cycle plus linear root movement, and the release event time is currently configured in game code rather than exported as clip metadata.

### Step 14 notes

- Authored and saved a looping `bowling-run-up` action in Blender MCP. The 2-second, 30 Hz cycle keys torso lean, arm counter-swing, thigh and shin drive, foot angle, and a small vertical bounce across the existing 13-bone rig. The bowler asset now exports seven clips.
- Replaced the borrowed between-wickets clip with the bowling-specific cycle. Runtime approach duration now comes from the exported clip duration, keeping the 15 m movement synchronized when the authored clip timing changes.
- Extended deterministic game capture with `--run-up-time <seconds>` so an exact point in the bowler's approach can be inspected in the actual MonoGame renderer; the default capture remains the release pose. Captured and reviewed `artifacts/step14-bowler-run-up-bowler-end.png`; repeated captures matched byte-for-byte (SHA-256 `683F2BE0EBC4BD6ED452C91D0272DA3872A7C6662BBF3CC80E096D6A91667783`).
- Release build completed without warnings or errors, the exported bowler validates, and the sample over still completes at 10/2. Root movement remains a straight game-space approach; the new authored cycle currently provides the body and limb motion.

### Step 15 notes

- Added an `sc_events` custom property to the Blender `overarm-delivery` action and authored `ball-release` at frame 21. The exporter converts event frames to clip-relative seconds and includes them in `.scplayer.json`; the bowler's exported event is 0.667 s.
- Extended player-asset validation to reject missing or duplicate event names and event times outside the clip. `validate-player` now lists events, making animation timing visible to the local content workflow.
- Removed the game-code release-time constant. Ball visibility, flight stepping, release-pose capture, and follow-through timing now read the `ball-release` event from the validated bowler asset.
- Updated the Blender exporter to refresh the evaluated view layer while switching actions and frames. Two consecutive bowler re-exports matched (SHA-256 `F3BE5BC211D2ECD0A8D5EEB83C356BD338A0CAE7BA104124DF2B7D4603885F76`).
- Verified export and player validation, tested rejection of an out-of-range event, rebuilt cleanly, reran the sample over at 10/2, and confirmed deterministic release captures (SHA-256 `AE16CF5F0ED5421E77EEB51872B287290DFB159DCFE5A1C87830F72CDDF98344`). Existing clips without events remain valid with an empty event list.

### Step 16 notes

- Used Blender MCP to author 15 m of forward root travel across the two-second `bowling-run-up` action. The curve follows the root bone's rest orientation, retains its vertical bounce, and reaches the delivery crease at the final sample.
- Extended the Blender exporter with per-pose `rootMotion` in player-local metres. It subtracts horizontal root displacement from each exported bone transform so the game can apply the path once, without double-transforming the skin. The exporter restores the scene's active action and frame after sampling.
- Replaced the game-space linear approach constant with sampled and interpolated motion from the bowler asset. The validator now prints net root displacement to make authored paths visible in the content workflow.
- Captured and reviewed run-up frames at 0, 0.5, 1, 1.5, and 2 seconds from the bowler-end camera. The endpoint reaches the release position; both player assets validate, the release event remains 0.667 s, the solution builds cleanly, and the sample over still completes at 10/2.
- Rejected a player export with a missing root-motion sample, confirmed consecutive Blender exports have matching SHA-256 `B392C45AEE9CCE386B16BBE501A846A2C05D78B6065CB2E2AE6A34E6798C57DC`, and repeated the 1.5 s game capture deterministically (`979C3D82AEDA0B60295C6BFF433D15E8B09D6A958AACA3D80FAD2E12D320D2FD`).

## Technical foundation

Use C# and MonoGame. Keep the first platform small and validate the graphics backend before investing in renderer features. Use Blender for source models, rigs, animation, and stadium authoring. The current runtime route is a validated `.scplayer.json` export from Blender with a MonoGame skinned renderer; retain deployment checks as the format evolves. If adding glTF/GLB, test skinning, animation, materials, coordinate conversion, and deployment explicitly.

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
