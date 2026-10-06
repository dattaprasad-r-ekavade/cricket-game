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

### Current assumptions (after the first over)

Revisited 6 October 2026 at `3f90c38`. Detail: `docs/reviews/2026-10-06-planning.md`.

- Windows, offline, fictional teams, and one stadium still hold. The current prototype is a two-innings short match with one over per innings by default; the first release candidate remains a fuller short limited-overs match.
- Keyboard and mouse remain fully supported; a mapped GamePad now covers core batting, running, bowling-selection, pause, and result actions. Physical controller testing remains part of playtesting.
- Short-match visual bar: readable stylized prototype (13-bone kit, vertex-colored players, static crowd, textured ground). Cricket 07 fidelity stays the long-term presentation ambition, not the Phase 5 gate.
- CLI tools are the Delivery, Shot, and Field labs. In-game work is an F1 overlay for contact and release markers, not a second editor.
- Steps 23–46 rules, positioning, live overlay, first match loop, roster skills, team-kit palette, synthetic and physics-grounded headless match batches, skill-driven CPU bowling and batting, adaptive field placement, rating-aware outcomes, seeded score calibration, CPU singles/doubles, CPU leave decisions, Rookie/Standard/Pro difficulty, controller mapping, human shot-lane steering, saved match/accessibility/audio preferences, pause-menu controls, and production CPU chase regression coverage are complete. Authored audio, live GUI balance, visual/accessibility confirmation, and playtesting remain open in Phase 5.
- Human batters can bias each authored shot's lane with J/L or the GamePad right stick, with a live HUD indicator; Q/E and the D-pad still handle footwork. The CPU can leave a wide when chase pressure is low. An authored batting step, wider delivery coverage, and human playtesting remain before expanding the shot catalogue.
- A second stadium, career mode, spin catalogue, MCP service over the CLI, and a general engine rewrite stay deferred.

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
- [x] Step 17 — Author the bowler's delivery follow-through in Blender and preview the complete action deterministically.
- [x] Step 18 — Replace fielder markers with rigged, animated 3D players and batch skinned meshes by material.
- [x] Step 19 — Author catch, pickup, and throw actions for fielders; sync run-out throws to the exported release event.
- [x] Step 20 — Sweep the moving bat against the ball and derive shot outcomes from swing velocity and sweet-spot contact; add a local batting analyzer.
- [x] Step 21 — Calibrate shot timing from exported batter/bowler clips against full delivery presets with a repeatable batting-practice sweep.
- [x] Step 22 — Review and regression-test gameplay, fix pause/scoring/analyzer defects, and add one repeatable review command.
- [x] Step 23 — Extract delivery lifecycle and scoring resolution from `Game1` into Simulation match state.
- [x] Step 24 — Add dedicated fielding and rules scenarios for catches, rope-skim boundaries, throws, and run-outs.
- [x] Step 25 — Add batter footwork or leave so the wide preset can find contact.
- [x] Step 26 — Show contact, sweet-spot, and release markers on the F1 overlay.
- [x] Step 27 — Add a two-innings short-match loop with a target chase, result text, restart, and compact overs selection.
- [x] Step 28 — Add validated fictional team rosters, batting-order identities, and timing/power attributes.
- [x] Step 29 — Add validated team kit palettes and apply the batting/fielding team's colors to the shared player model.
- [x] Step 30 — Add seeded headless match batches and inspectable scorecard exports across supported overs lengths.
- [x] Step 31 — Add bowling/fielding player ratings, over-based bowler rotation, and skill-based field movement.
- [x] Step 32 — Add pressure-aware CPU bowling variations with skill-scaled line control, pace, and swing.
- [x] Step 33 — Adapt the field between boundary protection and attacking positions from match pressure and batter power.
- [x] Step 34 — Use batting, bowling, and fielding ratings in seeded automatic match outcomes.
- [x] Step 35 — Add live CPU batting decisions grounded in the exported swing clips and delivery physics.
- [x] Step 36 — Make live CPU running decisions from the struck ball, fielders, and pickup/throw timing.
- [x] Step 37 — Simulate automated match batches through shot clips, ball flight, fielding, and production match rules.
- [x] Step 38 — Calibrate shot transfer and CPU placement against seeded 10-over physics batches.
- [x] Step 39 — Let the live CPU plan and complete safe singles and doubles from the ball, fielders, and return timing.
- [x] Step 40 — Add Rookie, Standard, and Pro CPU difficulty profiles and let players choose the level between matches.
- [x] Step 41 — Add context-aware controller mappings for batting, running, delivery selection, pause, and match results.
- [x] Step 42 — Save match and accessibility preferences and provide a keyboard/controller-operable pause settings panel.
- [x] Step 43 — Share wide-ball classification across match paths and teach the CPU to leave wide deliveries when the chase allows.
- [x] Step 44 — Add persistent effects volume controls and deterministic prototype cues for bat contact, boundaries, wickets, and extras.
- [x] Step 45 — Exercise production CPU chases in the actual game update, retain repeatable delivery traces, and fix the all-out roster crash.
- [x] Step 46 — Add human shot-lane steering for keyboard and GamePad, with an in-game aim readout.

Each completed step is recorded in its own commit and pushed to `origin/main`. The milestones below are the wider roadmap beyond this initial work package. Steps 23–46 are complete; continue the remaining Phase 0–6 gates in dependency order before calling development complete.

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

### Step 17 notes

- Added a Blender-authored 0.45 m forward root-motion arc to `overarm-delivery`, beginning at the exported `ball-release` event and easing into the follow-through. Runtime bowler placement now samples the delivery clip instead of applying a separate linear game-code offset.
- Added `--delivery-time <seconds>` to the frame-capture tool so the delivery and follow-through can be inspected without a live simulation.
- Reset each Blender pose to its rig basis before evaluating a different action. This prevents unkeyed root transforms from leaking between clips; both player exports were regenerated and all sampled horizontal root offsets are zero after extraction.
- Both exports validated and repeated deterministically (bowler SHA-256 `5DE2922E344FFF76093B6F659E02F9008629B34E226C61C36751FED77C686FDF`; batter SHA-256 `02385B38E4F718FE5C6B20FD244B413F353A03DDDC43DAC1F57341009F5584C1`). The exported bowler path is 15 m for run-up and 0.45 m for delivery; the delivery path remains at zero through the 0.667 s release event.
- Verified `--delivery-time` captures at release and 1.0 s, repeated the 1.0 s capture exactly (SHA-256 `5EC442F03331FF53AF8883AF03B1CD931F82BF67DF1BEC998332032BF391F83F`), and confirmed conflicting preview-time options are rejected. Release build succeeded with zero warnings, both player assets validated, and the sample over still completes at 10/2.

### Step 18 notes

- Replaced the ten blocky fielder markers with rendered instances of the rigged bowler-team player. Each fielder faces into the field; idle players use `practice-stance`, while the active chaser transitions to the shared running cycle. Added soft ground shadows for the full fielding side.
- Combined skinned mesh parts that share a diffuse material: the batter renderer batches 85 source parts into 19 draws per avatar, and the fielding/bowler renderer batches 36 parts into 12. The F1 developer overlay reports 13 rendered players and the two batch counts.
- Captured and reviewed bowler-end and square-leg views with all ten avatars visible. Repeated square-leg capture matched exactly (SHA-256 `2B8A8074EC149B594D4E28B0BA6A91DB30C92D4D519A9DB44D715DC8D2B9B661`); the field preset and batting shots validate and the sample over completes at 10/2. All fielders currently share one team model and the same ready/run cycles.

### Step 19 notes

- Added `tools/blender/author_fielding_animations.py` to author repeatable `fielder-catch`, `fielder-pickup`, and `fielder-throw` actions on the existing 13-bone Blender player rig. The exported bowler/fielder asset now has ten clips; the three new actions are 1.0 s, 0.5 s, and 0.6 s, with ball-secured/release events exported from Blender frames.
- Close-up capture exposed a trouser-to-shoe gap in the shared player model. Added idempotent `tools/blender/add_fielder_lower_legs.py` with two `shin.L`/`shin.R`-weighted meshes; the exported bowler/fielder asset now has 38 mesh parts and the lower-leg silhouette connects to the shoes.
- Added non-looping playback to `PlayerAnimator`. Catches hold their final secure pose, pickups return to ready, and a run-out collection transitions from pickup to throw. The ball moves from the ground to the player's hand on the pickup/catch marker; the visible throw arc starts at the exported `ball-release` time (0.300 s into the throw), and delivery resolution occurs when the throw clip completes at the wicketkeeper.
- Added `--fielder-action` and `--action-time` to deterministic game capture. Reviewed Blender action strips and MonoGame captures for catch, pickup, and throw so clip timing and the skinned renderer can be checked through the same toolchain.
- Release build completed with zero warnings or errors, the bowler/fielder asset validates with 10 clips, and the sample over remains 10/2. The current throw path is a presentation arc; pickup/catch outcomes and fielder travel rules remain the existing simplified simulation, and every fielder still shares one rig and team appearance.

### Step 20 notes

- Extracted `SweptBattingContactResolver` into the graphics-independent simulation layer. It transforms each end of the ball segment into the corresponding start/end bat pose, so a swinging bat can contact the ball even when the ball barely moves during that tick. The game interpolates bat transforms across render frames while the ball simulation advances at 120 Hz.
- Added `BattingImpactModel`: contact across the blade now has a normalized sweet-spot offset and quality, vertical blade position adjusts launch angle, lateral position adjusts aim, and velocity of the actual contact point contributes to the outgoing ball velocity. The existing per-shot angle, aim, and speed-transfer values remain the tuning inputs.
- Added `analyze-batting` to emit a repeatable CSV grid across all three shots, nine blade contact points, and three sample swing speeds. Added `verify-batting` checks for stationary-ball/moving-bat hits, moving-ball/stationary-bat hits, outside-blade misses, centered-versus-edge quality, vertical launch response, and swing-speed response.
- Release build completed with zero warnings or errors. `verify-batting`, shot validation, and the sample-over scenario pass; the existing over still finishes at 10/2. The impact model is a controllable prototype rather than a rigid-body bat simulation; the next pass should tune its response from observed in-game contacts and expanded batting scenarios.

### Step 21 notes

- Added `BattingPracticeAnalyzer` and the `analyze-batting-practice` command. It uses the exported practice-stance and shot clip samples, the bowler's run-up duration and Blender release event, the bat blade bounds, the same swept-contact/impact code as gameplay, and the actual ball-flight simulator. It sweeps shot input timing in 25 ms increments from 0.5 s before release through the batter's contact plane.
- Added `verify-batting-practice` as a real-asset regression gate. Against standard pace, all three exported shots find contact: defence 26/45 tested input timings, drive 19/45, and loft 18/45. Best-quality results occurred at +0.200 s for defence (quality 0.91, 14.3 m/s in-play), and +0.250 s for drive (0.90, four at 22.9 m/s) and loft (0.90, six at 27.1 m/s).
- Repeated standard-pace analysis produced identical CSV hashes. The wide preset currently produces no contact timings for any of the three shots, identifying the need for batter footwork/reach before wide-ball shot coverage improves; the analyzer can now quantify that change.
- Release build and real-asset batting verification passed; the sample over remains 10/2. The sweep assumes the batter remains at the crease and reports the ball's boundary outcome without fielder interception or running, so those remain separate match outcomes.

### Step 22 notes

- Added `tools/review.ps1` and the game's `--verify-gameplay` mode. The latter loads real content and drives the normal match update with scripted inputs, checking pause/resume, pickup/throw/run-out completion, six legal deliveries, wide/no-ball scoring, and all three shots at simulated 30/60/120 FPS. Boundary and catch cases arrange state explicitly to cover scoring branches.
- Reproduced and fixed animation/running/throw advancement during pause, boundaries adding their allowance to earlier completed runs, and legal catches retaining completed runs and replacing the wrong batter. Boundary scoring now preserves the greater running allowance when applicable, including an already-crossed run; no-ball catches preserve runs and the penalty.
- Reproduced and fixed analyzer contact before shot input, clipped a tick at the actual input time, cleared contact timestamps on misses, and derived reported launch angle from final outgoing velocity. Added overlapping-blade and upward-swing regressions.
- The full review command passed: Release build with zero warnings/errors, all shipped player/shot/field/delivery validators, shared simulation diagnostics, rules scenario (10/2 in 1.0 overs), invalid-step rejection, and gameplay checks. Two standard sweeps had SHA256 `CF01930726FE8ACE1671C625E3888C961533BD893A9AE332B36E05994D3B4F5A`. Standard/no-ball contact windows now contain 25/18/17 contacts for defence/drive/loft; the wide sweep has none. This removed one false late-input contact per shot.
- Captured start, bowler follow-through, and three fielder actions through the DirectX renderer, and visually reviewed start/catch/throw/follow-through. Detailed findings, evidence, and remaining limitations are recorded in `docs/reviews/2026-10-06.md`. Art remains visibly primitive; fielding/collision heuristics, full-match playtesting, and GPU performance validation remain wider roadmap work.

### Planning review notes (6 October 2026)

- Reviewed plan versus code after Step 22. The first-over gates for Phases 0–3 hold at prototype depth; Phase 4 is started; Phases 5–6 are not started.
- Delivery resolution still lives on `Game1` (~1,300 lines). `OverScoreboard` cannot represent an innings. Fielding chase, catch height, throw arc, and run-out cuts are simplified heuristics.
- Phase 2 positioning is open: the batter stays at the crease and the wide practice sweep has no contacts.
- Recorded current assumptions above and queued Steps 23–26. Full findings: `docs/reviews/2026-10-06-planning.md`.

### Step 23 notes

- Added graphics-independent `MatchState` and `DeliverySession` types. The game now asks Simulation to begin and finish deliveries; Simulation owns batter/extra/completed runs, legality, catches, run-outs, boundary run allowances, dismissals, and scorecard updates.
- Kept ball-flight contact detection, fielder movement, authored throw timing, UI text, and animations in the game host. No render code moved into Simulation.
- Added `verify-match` checks for delivery lifecycle, wide/no-ball legality, caught scoring, boundaries with a run in progress, and no-ball run-outs; added the command to `tools/review.ps1` and the tool guide.
- Release build and game update regressions pass. Step 24 adds swept boundary-crossing classification, low-catch checks, and a returning-throw run-out scenario.

### Step 24 notes

- Added `BoundaryResolver` to find the segment crossing on the circular rope. The game now resolves four/six scoring at the crossing point; a ball that has bounced before crossing or skims the ground scores four.
- Lowered the catch threshold to a reachable height above each fielder's ground position. Swept-contact scenarios distinguish a low airborne catch, a ball picked up after a bounce, and a ball above reach.
- Added repeatable `verify-fielding` scenarios for those contacts and boundary cases, plus a real game-update scenario that completes pickup and throw while the runner is approaching the far crease.
- Release build, fielding checks, and gameplay regressions pass. Fielder movement, receiving, and throw travel remain simplified and should be playtested during short-match work.

### Step 25 notes

- Added Q/E lateral steps. Each press moves the batter 0.45 m toward off side or leg side, up to 2.25 m; the translation eases in and updates both the batter model and swept bat transform.
- Extended `analyze-batting-practice` to compare all 11 supported offsets at each input timing and report the chosen position. The wide pace preset now yields 20/45 defence, 17/45 drive, and 16/45 loft contacts; best drive contact is at +0.350 s with a +2.25 m off-side step.
- Added a wide-preset test to the full game update at 30/60/120 FPS and a `verify-footwork` command for step limits, input timing, and all three real-asset shot clips.
- The analyzer and game now use the same rope-crossing classifier for four/six outcomes. The complete review command passes. Footwork is a readable prototype translation using the existing movement cycle; a dedicated authored batting step remains art work.

### Step 26 notes

- The F1 view now draws world-space release, ball-contact, and bat sweet-spot crosses in gold, orange, and cyan. It reports the release time, elapsed contact time, sweet-spot quality and normalized offset, and each marker's position.
- The swept-contact result includes the blade's sweet-spot center interpolated to the hit fraction. Gameplay regressions require all three marker positions and contact metrics after standard and wide shots at 30/60/120 FPS; the resolver check also verifies the moving bat's sweet-spot world position.
- Added a deterministic F1 renderer capture to `tools/review.ps1`; the capture was visually checked. The full Release review passed with zero warnings/errors, including all asset, simulation, analysis, and actual-gameplay checks.

### Phase 0 target notes

- Added the short-match experience brief, original four-panel stadium/material/player/HUD reference board, and v1 Blender player asset contract in `docs/design/`.
- `PlayerAsset.Validate` now rejects rig/sample scales outside 0.5–2.0 per axis and player mesh heights outside 0.5–4.0 m. The review command mutates a temporary batter export to 10x scale and confirms the validator rejects it. The v1 format supports per-mesh diffuse colour; external image maps are not part of the current renderer contract.
- Added `--profile-frames <count>` with a warm-up window and frame-interval/CPU update/draw summaries. On the Lenovo IdeaPad S145-15IIL (81W8), a 300-frame sample at 1440x900 with VSync averaged 16.66 ms; P95 was 17.55 ms. CPU update and draw-submission P95 were 0.14 ms and 2.65 ms. GPU timing remains open for Phase 4.

### Step 27 notes

- Added `LimitedOversMatch` on top of the shared `MatchState`, with configurable innings lengths, two fictional teams, a first-innings summary, a target of first-innings runs plus one, immediate target completion, and defended/tied/won result text.
- The live game begins the chase with N, shows innings/team/score/overs/target, and displays the result. O cycles 1, 2, 5, and 10 overs per innings after a result and starts a new match; R restarts at the selected length.
- Added simulation checks for a two-over target chase and a defended target, plus real update-loop checks for N advancing to the chase, a tie result, and O changing the match length. Release build and the full `tools/review.ps1` suite passed with zero warnings/errors.
- This is the match-loop foundation only. Rosters/batting order, opponent decisions, settings, accessibility/controller support, automated match batches, and human playtesting remain open in Phase 5.

### Step 28 notes

- Added versioned roster files for Coastal XI and Highland XI. The validator requires eleven players, a unique batting order from 1–11, unique identities, supported roles, exactly one wicketkeeper, and timing/power ratings in the 0–100 range; unknown JSON fields fail validation.
- The match now resolves the on-strike and incoming batter by the scorecard's batting-order number. The HUD names the active pair and the roster timing/power values affect off-centre contact quality and outgoing shot speed. Ratings of 50 preserve the neutral practice response.
- Added `validate-team`, malformed-roster rejection to `tools/review.ps1`, and repeatable checks for roster rules, wicket substitutions, and both live batting attributes. Every roster player still shares the starter batter model; bowling/fielding ratings, opponent decisions, and distinct team appearance remain open.
- Removed the duplicate `practice-attack.json` content-copy entry. A clean Release review passed: both real player assets validate and load, invalid scale is rejected, all simulation/gameplay checks pass, and the renderer captures are produced.

### Step 29 notes

- Added primary and accent `#RRGGBB` kit colors to both fictional team files. The roster validator checks each color, and the content model converts valid colors to normalized RGB values for rendering.
- The skinned renderer recolors the shared batter/bowler asset's shirt, forearms, collar, sleeve bands, and crest accents. The current innings' batting and fielding teams supply their respective palettes, so the colors swap with the teams.
- Added `-SkipGame` to `tools/review.ps1` and documented the code-only invocation `pwsh -File tools/review.ps1 -SkipGame -SkipCaptures`. It still builds Release and runs CLI asset, simulation, rules, and analyzer checks without starting the game host.
- The code-only Release review passed with zero warnings or errors, including both roster files, invalid primary/accent color rejection, RGB conversion checks, innings-side checks, simulation scenarios, and repeatable analyzer output. Per the current no-GUI-testing instruction, palette appearance is not visually verified; renderer capture and in-game review remain for the later GUI pass.

### Step 30 notes

- Added a seeded CPU-versus-CPU match-batch simulator that uses the production `LimitedOversMatch` and `DeliverySession` rules, completes both innings, stops a chase at its target, and has a delivery guard for stuck innings.
- Added `verify-match-batch` coverage for deterministic replay, 1/2/5/10-over innings, valid scores and wicket limits, completed match results, and unsupported inputs. Added `simulate-match-batch` to export per-match scorecards as CSV.
- The full code-only Release review passed. A separate 1,000-match, 10-over batch completed; all 1,000 CSV rows had valid wicket/ball bounds and a result. This is a rules-state and stuck-match stress tool using a seeded synthetic outcome model; it does not exercise ball physics, live opponent control, field positions, animation, or gameplay balance.

### Step 31 notes

- Added validated 0–100 bowling and fielding ratings to all 22 roster players. Teams must include a bowler or all-rounder. The strongest available bowler/all-rounder opens each innings' rotation, and the match changes bowler at each over; the HUD names the current bowler.
- The fielding side excludes its current bowler, assigns the wicketkeeper to the wicketkeeper position, and maps each remaining player's fielding rating to reaction time, movement speed, and pickup radius. Rating 50 preserves the former constants.
- Release build and full code-only review passed with zero warnings or errors. Checks cover rating validation, role assignment, strongest bowler and over rotation, neutral fielding baseline, skill-based movement/pickup, match rules, and headless match batches. No game host or GUI was run.
- Step 32 also feeds bowling ratings into pace, line control, and swing execution, and chooses plans from striker power and chase pressure. Seeded headless checks cover repeatability, skill response, and tactical selection; balance still needs GUI play.

### Step 32 notes

- Added a renderer-free CPU bowling model. It chooses stock, outside-off, inswing, or outswing using wicket pressure, chase rate, and striker power; bowling skill scales pace, line control, and swing execution. It deep-copies the stock preset so delivery-by-delivery changes do not leak into later balls.
- Wired the model into normal live stock deliveries. The game creates a per-match seed and derives per-ball seeds from match state and player identities. Explicit wide/no-ball presets remain available as diagnostic overrides.
- Added seeded checks for repeatable decisions, pressure response, skill-scaled control and pace, valid physics inputs, source-preset preservation, and invalid ratings/state. The Release build and code-only review passed with zero warnings/errors; the game host and GUI were not started.
- Adaptive field placement is Step 33; live CPU batting and running are Steps 35–36, physics-grounded batches are Step 37, and seeded score calibration is Step 38. Multi-run decisions, live GUI calibration, and the remaining Phase 5 settings and accessibility work remain open.

### Step 33 notes

- Added two compact fielding tactics: protect the boundary against a power hitter or high chase rate, and bring close catchers in when the batting side has lost many wickets without chase pressure. Neutral situations preserve the authored field.
- Adapted positions from the validated field preset on every delivery. Deep fielders move toward the rope for protection; close fielders move in for a wicket chance. The wicketkeeper stays at the wicket, positions stay within the field boundary margin, and each decision starts from the original asset so offsets do not accumulate.
- Added headless checks for tactic choice, fielder movement, boundary clearance, surface height, wicketkeeper placement, and preset preservation. The Release build and code-only review passed with zero warnings/errors; the game host and GUI were not started.
- Play balance and clear in-game presentation remain open for the later GUI pass; CPU batting/running, adaptive outcomes in the automatic-match model, and settings remain open in Phase 5.

### Step 34 notes

- Extracted a CPU batting outcome model and used it in the seeded automatic match simulator. The batter's timing/power, current chase pressure and wickets, current bowler's rating, fielding team's average rating, and selected fielding tactic now affect shot choice and dismissal, boundary, running, run-out, and catch probabilities.
- The simulator continues to resolve every synthetic event through the production `LimitedOversMatch` and `DeliverySession` score rules. Seeded checks cover aggressive and defensive decisions, bowling/fielding effects, bounded probabilities, deterministic batches, and completion at all supported overs lengths.
- Release build and the full code-only review passed with zero warnings/errors; the game host and GUI were not started. This remains a synthetic outcome model: it does not yet drive live CPU batting or resolve every automated outcome from ball flight and fielder movement.

### Step 35 notes

- Added live second-innings CPU batting plans that select defence, drive, or loft from player skill and chase pressure. Timing and footwork come from the exported batter/bowler clips and the actual delivery simulation; timing error scales with the batter's rating and seed.
- Added a wicket-line timing guard and headless checks for standard and wide deliveries, deterministic plans, shot choice, timing skill response, and contact prediction. The build and code-only review passed without starting the game host.

### Step 36 notes

- The live CPU now checks its one-run intent against the contacted ball's outgoing trajectory, current fielder positions and ratings, boundary crossing, and actual pickup/throw clip duration before committing to a run.
- Added headless cases for clear gaps, quick pickups, boundary outcomes, and deterministic running decisions. Release build and code-only review passed; GUI balance review remains open.

### Step 37 notes

- Added `simulate-physics-match-batch`, which runs both innings using exported batting clips, actual incoming and outgoing ball-flight simulation, the adaptive field, fielder ratings and movement, delivery extras, and production match rules. CSV output includes contacts, misses, catches, pickups, boundaries, run intents, safe attempts, completed runs, run-outs, wickets, and extras.
- CPU shot placement now scores candidate lanes against the current field and fielder ratings. The chosen lane is applied to the live shot and to the headless flight simulation; the authored shot asset is never mutated.
- Extended `verify-cpu-batting` for field-aware placement and aim-driven trajectory changes. The code-only review repeats a seeded three-match physics batch, compares CSV hashes, and checks match results and event/run metrics. Release build and review passed; no game or GUI was started.

### Step 38 notes

- Calibrated the editable defence/drive/loft `speedTransfer` values to `0.413`, `0.671`, and `0.791` for the current 42 m boundary, and added seed-based shot-placement execution error that scales with timing and power ratings.
- A seeded batch of six 10-over matches averaged 100 runs per innings (range 70–135) and produced 285 boundaries, 186 ground pickups, 13 catches, and 6 run-outs. The review command now checks a broad score envelope and requires each event type; live visual balance remains for the later GUI pass.
- Release build and the full code-only review passed with zero warnings or errors. The game host and GUI were not started.

### Step 39 notes

- The live CPU now plans zero, one, or two runs by comparing the outgoing ball path, the current field, boundary crossing, and pickup/throw durations. The live match starts each planned crossing in sequence and moves the batters to the correct ends; a dead ball cancels any remaining plan.
- Physics batches resolve the same single/double plans through match rules and expose double plans and safely completed doubles in the CSV and summary. Review checks allow up to two safe runs per run intent and require seeded doubles to complete without forced run-outs.
- The Release build, focused CPU batting/running checks, a repeatable three-match short batch, and the six seeded 10-over balance batch passed. The six-match batch averaged 84.4 runs per innings and recorded 10 planned doubles, all safely completed. No game windows or popups were opened; visual balance and human playtesting remain open.

### Step 40 notes

- Added deterministic Rookie, Standard, and Pro profiles for CPU batting timing and shot-placement error, bowling accuracy, batting aggression, and outcome probabilities. The profiles tune opponent decisions while keeping ball physics and scoring rules fixed.
- At a match result, D cycles to the next CPU difficulty and starts another match; R preserves the selected level. The normal and developer overlays show the active level.
- Release build, difficulty direction/replay checks, CPU batting and bowling decision checks, and the full `tools/review.ps1 -SkipGame -SkipCaptures` review passed. No game host or GUI was launched; live difficulty balance remains for playtesting.

### Step 41 notes

- Added an edge-triggered controller input model and wired an Xbox-style GamePad mapping into live match updates. Face buttons select shots and running, shoulders cancel/advance, the D-pad selects deliveries or adjusts batting footwork by innings, and Start pauses.
- At the result, A restarts with the current settings, the left shoulder changes difficulty, and the right shoulder changes overs. README and the on-screen match panel list the mappings; keyboard controls remain available.
- Release build, action-mapping scenarios for human batting, CPU batting, and match results, and the full headless review passed. No game window was opened and no physical controller was tested; those remain in the playtest gate.

### Step 42 notes

- Added versioned JSON settings in the user's local application-data directory for CPU difficulty, overs per innings, high contrast, and larger text. Missing files use defaults; malformed or unsupported settings are rejected. Saving writes a temporary file and atomically replaces the saved file.
- Added a pause panel with keyboard and GamePad toggles for high contrast and larger text. These settings apply to the match overlay and persist immediately; failed reads/writes are reported in the panel while the current session remains playable.
- Release build, settings round-trip/default/invalid-data checks, paused controller mapping checks (including paused results), and the full `tools/review.ps1 -SkipGame -SkipCaptures` review passed. The game window and capture paths were not run; visual balance, sound controls, and playtesting remain open.

### Step 43 notes

- Centralized wide-line classification and used it in live incoming-ball resolution and physics-grounded match batches. Added interpolated wicket-line trajectory queries so the CPU bases leave decisions on the actual delivery path.
- The CPU leaves a wide when chase pressure is below 0.55 and plays it when pressure is higher. A leave schedules no swing, footwork, or run. Deterministic checks cover either side of the exact wide threshold, invalid measurements, pressure response, and repeatability.
- The six seeded 10-over physics matches exercised 22 wide deliveries, all left under low pressure; they averaged 81.4 runs per innings (range 49–116), with 16 of 17 double plans completed and the remaining plan ending in a caught dismissal. No run-outs occurred. The release build and full `tools/review.ps1 -SkipGame -SkipCaptures` review passed. No game window or capture path was run; authored batting footwork and wider delivery coverage remain open in Phase 2.

### Step 44 notes

- Added deterministic 22.05 kHz mono PCM drafts for bat contact, boundaries, wickets, and extras. Playback is connected to live match events, and each cue fades at its clip edges.
- Added saved effects volume with a 50% default. While paused, -/+ and GamePad LB/RB change it in 10% steps. Older settings files load with the default volume.
- Checks cover cue stability, duration, clean clip edges, peak level, uniqueness, invalid cue rejection, settings migration and validation, and paused controller mappings. The Release build and full `tools/review.ps1 -SkipGame -SkipCaptures` review passed with zero warnings or errors; no game window or capture path was run.
- These procedural cues are implementation drafts. Authored sound, mix quality, and live audio output remain unreviewed; Phase 4 and Phase 5 audio gates stay open.

### Step 45 notes

- Added `--verify-live-match [results.csv]` in the Windows game host. It runs the real match update with production CPU bowling, batting, placement, footwork, running, animations, and rules enabled, whereas the older `--verify-gameplay` mode intentionally uses manual batting and stock bowling for focused scenarios.
- The suite contains 13 matches driven through both innings with scripted first-innings inputs, plus seven explicitly labelled prepared-target CPU chase fixtures. It covers Rookie/Standard/Pro, simulated 30/60/120 FPS, one/two-over innings, pause/resume, wide/no-ball deliveries, targets, all-out, scorecard consistency, and restart. Each case repeats its seed and compares per-delivery traces and results.
- A live all-out case reproduced a HUD/roster crash: wicket ten assigned batting order 12. The scorecard now stops replacing batters at the last wicket. Simulation regressions cover both all-out innings, valid displayed identities, rejection of another delivery, and the final result.
- Review modes now use default preferences, suppress cue playback, and skip saved-settings writes. The full review script checks that the user's existing preference file is unchanged.
- The new suite passed with 187 traced deliveries, 86 CPU deliveries, 56 CPU contacts, eight leaves, four completed running runs (two doubles), and one defended target. The 24-run fixtures finished 24/0 versus 25/0 at each tested frame rate; the 72-run fixture finished 72/0 versus 43/2. Same-seed traces repeated exactly within each frame rate; this does not establish cross-frame-rate equivalence or gameplay balance. Trace SHA256: `CC1ADDDD5BC24D4B7187D23C16A632C501C76B6A44AA65E270AC66E128B487D4`.
- Detailed coverage and limits are in `docs/reviews/2026-10-06-live-match.md`. Update-only game-host scenarios do not render every match frame or replace human/controller/audio/visual playtesting. The scripted human shots in these seeds were largely held by the field; shot placement, readable aiming, and live balance remain priorities.
- The full `tools/review.ps1` run passed after the change: zero build warnings/errors, asset and simulation checks, deterministic match batches, both game-host suites, unchanged saved preferences, renderer profile, and six renderer smoke captures. The full review's live CSV matched the standalone trace hash above.

### Step 46 notes

- Added human shot-lane adjustment before the swing and while the bat is moving. J/L make discrete keyboard adjustments; the GamePad right stick steers continuously with a dead zone. The adjustment is relative to each authored shot's default, the resulting lane clamps to the simulation's supported `[-1, 1]` range, and the adjustment resets at the next delivery. CPU placement overrides remain independent.
- The normal match HUD shows an aim bar and numeric shift before shot selection, then the actual selected lane during the swing. README now documents the keyboard and controller controls.
- Gameplay review checks cover both keyboard directions, steering a lane while a swing is selected, the outgoing-velocity direction, GamePad axis scaling, and aim reset between deliveries.
- Captured and visually reviewed the normal 1440×900 match HUD; all aim and gameplay instructions fit in the panel.
- This addresses the scripted-match review's missing human placement control; it does not establish human-versus-CPU balance. Confirm controller feel, handedness/side labeling, and shot outcomes in live play before closing Phase 5.

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

**Phase 0 gate — passed 6 October 2026:** the brief, original visual reference board, player asset contract, and named renderer baseline are recorded in `docs/design/`. The clean Release review builds and loads both exported rigs without asset repair, creates the intended-look capture, and rejects a deliberately 10x-scaled rig. `--profile-frames 300` provides repeatable frame-interval and CPU timing samples. GPU timing and a second-machine check remain in their later gates.

## Phase 1 — Bowling and ball simulation

Build a practice delivery with a placeholder bowler. Add release position/velocity, gravity, drag, tunable swing/spin, pitch bounce with tangential response, rolling, ground and wicket contacts, and initial pace and spin deliveries. Show release speed, bounce point, and trajectory.

Build a Delivery Lab with pause, slow motion, single-step, trajectory trails, tunable parameters, and saveable presets. Add a headless command for seeded delivery batches and exported outcomes. Tools must call the same simulation as the game.

**Gate:** a saved preset reproduces its trajectory in the supported build; parameters change outcomes predictably; the ball cannot tunnel through the pitch or stumps at supported speeds.

## Phase 2 — Batting and synchronized animation

Add a compact first shot set: defence, drive, and lofted shot. Add intent and timing input, stance, footwork, bat attachment, animation blending, recovery, and markers for release, contact window, foot plant, and recovery. Resolve misses, edges, contact, and bowled dismissals.

Keep contact authored and tunable: determine eligibility from ball and bat position, then derive the outgoing ball from incoming velocity, bat motion, contact offset, and shot parameters. Make assistance bounded, visible in debug mode, and repeatable. Build an Animation and Shot Lab for clip scrubbing, bone/bat overlays, contact markers, presets, and automated playback against deliveries.

**Gate:** the player can intentionally defend, drive, and loft; timing and positioning differences are clear; contact aligns from both gameplay and replay cameras. Repeated delivery practice is enjoyable before expanding the shot catalogue.

**Open after Step 46:** defence, drive, and loft work on standard and wide pace. Q/E moves the batter laterally, J/L or the GamePad right stick steer the shot lane, and the HUD shows the current aim shift or selected lane. The analyzer identifies the step and timing needed to make contact, and the CPU leaves clear wides at low chase pressure. The current footwork step reuses the between-wickets cycle; an authored batting footwork clip, broader delivery coverage, and human confirmation of shot balance remain open.

## Phase 3 — Fielding and a complete over

Add wicketkeeper, non-striker, and a small functional field. Implement interception, movement limits, pickup, catch, throw, receiving, wicket breaks, running, turning, cancellation, and run-outs. Add boundaries, extras supported by the bowling mechanics, strike changes, and over progression.

Build a Field Lab to place players, inspect interception predictions and reachable areas, and save field presets. Add repeatable scenarios for catches, boundaries, close run-outs, overthrows, extras, and strike changes.

**Gate:** play six legal deliveries, resolve extras and supported dismissals, update score and striker correctly, and return to a stable state after every ball.

**Open after Step 27:** Simulation now supports innings limits and two-innings match results. The game starts the chase with N, shows the target and result, and cycles 1/2/5/10 overs per innings with O after a result. Fielder movement, throw travel, receiving, and wicket-breaking still use simplified rules and need full-match scenarios.

## Phase 4 — Demonstrate the visual target

Create one cohesive representative art set: stadium, pitch, outfield, stands, restrained crowd, polished rig with kit and equipment, and the animations needed for the full over. Add textured materials, sunlight, ambient lighting, a measured shadow solution, restrained post-processing, readable ball presentation, and mesh detail reduction where profiling supports it. Add broadcast delivery, ball-follow, fielding, and replay camera presets, plus impact and crowd audio and a readable scoreboard.

Build a Stadium and Presentation panel for placement, camera bookmarks, lighting presets, and capture. Begin with Blender-authored layout and overrides; expand into a dedicated editor only when iteration needs it.

**Gate:** capture an entire over at the intended visual level, with consistent movement and cameras. Measure CPU/GPU frame times and animation cost with the full fielding side on the target hardware. Aim for stable 60 fps at the agreed resolution and tune from measurements.

**Open after Step 29:** stadium, cameras, light, ground textures, static crowd, skinned fielders, and team-specific flat-color kit palettes are in. The short-match visual bar is a readable stylized prototype, now captured as an original reference board. Palette appearance still needs GUI review; richer kit materials, audio, post-process, and GPU timing stay in this phase. Two 300-frame live profiles at 1440×900 with VSync measured frame interval P95 at 18.40 and 18.57 ms, with maxima near 40 ms. CPU update and draw-submission P95 stayed below 5 ms; GPU time is not measured. The full-match sustained-60-fps gate remains open.

## Phase 5 — Complete short match

Add two fictional teams, player attributes and batting order, innings, a target chase, match results, restart, and a compact overs selection. Add opponent decisions for bowling variation, shots, running, and field placement. Provide understandable difficulty, pause/settings, controller mapping, audio controls, and essential accessibility options. Save settings and match state if required by the release scope.

Build roster and tuning editors around validated files. Run automated matches to find stuck states, invalid scores, impossible transitions, and matches that fail to finish. Keep human playtesting for control feel and fun.

**Gate:** complete a match repeatedly from start to results without debug intervention. Playtesters understand controls and can point to specific gameplay problems.

**Current progress (Step 46):** the first match implementation has fictional team rosters, batting-order identities, timing/power/bowling/fielding ratings, skill-based bowler rotation and fielder movement, pressure-aware CPU bowling variation, adaptive field tactics, and synthetic and physics-grounded automated match batches. In the live second innings, the CPU selects a shot from ratings and chase pressure, plans timing and footwork from exported clips and delivery physics, and picks a lane using the actual field and fielder ratings with rating-scaled execution error. Human batters can shift each authored shot's default lane with J/L or the GamePad right stick; the in-game HUD shows the adjustment before a shot and its actual lane during the swing. At contact, the CPU checks the outgoing ball, current field, pickup/throw timing, and boundary before planning a safe single or double. It leaves a clearly wide delivery when the chase pressure permits. Rookie, Standard, and Pro tune CPU timing, placement, bowling accuracy, batting aggression, and outcome probabilities without changing physics or match rules. Keyboard and GamePad controls cover live play and result actions. Difficulty, overs length, high contrast, larger text, and effects volume save to a versioned local settings file and can be changed from the pause panel. Bat contact, boundaries, wickets, and extras play deterministic procedural audio drafts. Automated physics batches use the exported batting clips, ball-flight simulation, fielding movement, deliveries, and match rules, with deterministic CSV metrics. `tools/review.ps1 -SkipGame -SkipCaptures` checks repeatable short batches and a six-match 10-over score/event envelope without starting the game host. The live loop includes the two-innings target chase, results, restart, and overs-length selection. Phase 5 remains open for authored audio and live output review, visual/balance review, visual/accessibility confirmation, physical controller testing, and human playtesting.

## Phase 6 — Stabilize and choose expansion

Package a reproducible release candidate, test on another machine, profile representative matches, and review logs and asset validation. Prioritize animation glitches, ball visibility, control latency, camera confusion, and rules. Consider additional stadiums, teams, formats, multiplayer, career systems, and advanced presentation only after the small release is enjoyable and stable.

## Tool priorities

1. Asset validator and importer: prevent export, scale, material, and skeleton failures.
2. Delivery Lab and headless scenarios: make ball tuning measurable and repeatable.
3. Animation and Shot Lab: inspect the critical movement/contact dependency.
4. Field Lab: inspect positioning, reach, and interception.
5. Replay and capture commands: reproduce defects and support visual review.
6. Stadium, roster, and presentation editors: speed production after formats settle.

The CLI is the lab for items 1–5 (`validate`, `simulate`, `analyze-*`, `verify-*`, `--capture-frame`, `--profile-frames`, `tools/review.ps1`). Do not build a second in-game editor for the same jobs. Step 26 delivered the F1 overlay for contact, sweet-spot, and release markers. Hot-reload tuning data first; reload models and shaders once resource replacement is safe. Add a service/MCP interface only when a repeated AI workflow justifies it. Keep output paths, errors, and changes reviewable.

## AI-assisted workflow

Give AI bounded tasks with desired behaviour, related code/schemas, examples, acceptance conditions, and a reproduction scenario. Have it implement one coherent change; compile, run the scenario, and inspect before dependent work. Good tasks include C# features, debug panels, import scripts, build automation, shader drafts, Blender batch/export checks, texture or concept drafts, simulation-result summaries, documentation, and scenario generation.

Review generated code and validate generated assets. Rigging, animation cleanup, bat contact alignment, and visual inspection remain part of asset production; do not plan on finished cricket animation appearing from a prompt without iteration. Track asset origin and usage rights; use fictional branding and original assets initially. Game simulation must never depend on an AI service.

## Validation and rhythm

Keep focused scenarios for fast-ball collision, known bounce, early/late/missed batting, boundary classification, catches, run-outs, extras, over completion, and innings completion. Add scenarios when new mechanics or defects warrant them. For every milestone, retain a runnable build, short capture, known limitations, and measured performance. Run regular play sessions and choose the next work from observed problems. Maintain backlog groups for current, next, and deferred work.

## Effort estimate and first package

For one developer working substantially full-time with AI assistance, allow roughly 3–6 months for a convincing playable slice and 9–18+ months for a polished small game. This is an uncertain planning estimate; experience, asset availability, animation, and scope can move it substantially. The first animated import and playable over are done; re-estimate the short-match calendar after Steps 23–25. Part-time work stretches the calendar estimate.

Initial work package (complete):

1. Scaffold the solution, pin dependencies, and establish build/run commands.
2. Render the ground, pitch, ball, controllable camera, and debug measurements.
3. Import one animated player and prove a clip transition.
4. Implement fixed-step ball flight, pitch collision, and trajectory visualization.
5. Save and replay one delivery preset through an initial Delivery Lab.

The initial milestone was an animated practice delivery with a reliable asset pipeline and observable simulation. Steps 23–44 completed the one-over rules loop, live batting overlay, first two-innings match/roster slice, safe CPU singles/doubles, CPU wide leaves, selectable CPU difficulty, controller mapping, saved match/accessibility/audio preferences, pause controls, and procedural audio drafts; the remaining Phase 2, Phase 4, Phase 5, and Phase 6 gates continue.

## References

- [MonoGame content pipeline overview](https://docs.monogame.net/articles/getting_to_know/whatis/content_pipeline/CP_Overview.html)
- [MonoGame configurable effects](https://docs.monogame.net/articles/getting_to_know/whatis/graphics/WhatIs_ConfigurableEffect.html)
- [MonoGame 3.8.5 release](https://monogame.net/blog/2026-07-15-3.8.5-release-2026/)
- [glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.pdf)
- Gameplay review: `docs/reviews/2026-10-06.md`
- Planning review: `docs/reviews/2026-10-06-planning.md`
