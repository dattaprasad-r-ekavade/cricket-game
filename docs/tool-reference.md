# Development and tool reference

This guide contains the full CLI, content-authoring, validation, capture, and profiling workflow. The main build/run path and normal controls are in [README.md](../README.md).

## Automated tests

Run the isolated xUnit tests with `dotnet test tests/SuperCricket.Game.Tests/SuperCricket.Game.Tests.csproj -c Release`. They do not launch the game. The Windows validation workflow runs this suite before the asset and simulation review.

## Developer controls

Start the game with `dotnet run --project src/SuperCricket.Game -- --debug` to expose diagnostic shot selection, aim/footwork controls, animation cycling, delivery presets, camera orbit/elevation, and F1 diagnostics. Capture a named authored batting-footwork pose with `--batter-footwork <clip> --action-time <seconds>`.

## Delivery tools

`dotnet run --project src/SuperCricket.Game -c Release -- --verify-live-match [results.csv]` exercises the production match update with CPU control and skill-based bowling enabled. It plays scripted human first innings and live CPU chases across Rookie/Standard/Pro, 30/60/120 FPS, and one/two-over lengths, with separate prepared-target chase fixtures. Every case repeats with the same seed and compares delivery traces and results. It checks pause/resume, wide leaves, runs, all-out identities, match completion, and restart, then exports the per-delivery CSV. This loads the Windows game host and real assets; it advances updates directly without drawing every frame. Both game review modes use default preferences, preserve the saved settings file, and suppress playback. Normal play still uses saved preferences and audio.

Run the full review checks on Windows with `pwsh -File tools/review.ps1`. This builds Release, validates assets, runs batting/field/rules diagnostics, checks repeated CSV output and rejected inputs, exercises the actual match update, and saves renderer captures under `artifacts/`. Use `-SkipCaptures` to omit screenshots. For a code-only review that does not start the game host, run `pwsh -File tools/review.ps1 -SkipGame -SkipCaptures`; this keeps the build, content validators, analyzers, and simulation checks. It also checks a seeded six-match 10-over physics batch against a broad score and event envelope, including safe double runs. The game checks can also run directly with `dotnet run --project src/SuperCricket.Game -- --verify-gameplay`; they load the real content and exercise pause/resume, shot contact at 30/60/120 FPS, a two-innings match, extras, boundaries, catches, and pickup/throw run-outs. `verify-match` checks innings limits, target chasing, results, seeded CPU bowling decisions, adaptive field tactics, and batting/bowling/fielding outcome responses; `verify-cpu-batting` checks live CPU shot, timing, field-aware placement, and physics-based single/double running decisions against standard and wide deliveries; `verify-match-batch` verifies deterministic synthetic match completion across all supported overs lengths; `simulate-match-batch` writes scorecards from seeded, rating-aware synthetic outcomes; `simulate-physics-match-batch` uses exported swing clips, ball-flight physics, the saved field, fielder ratings and movement, and production match rules to write inspectable scorecards with run intents, safe runs, double plans, and doubles scored; `verify-fielding` covers low catches, ground pickups, and airborne versus rope-skim boundary crossings.

To capture a scene with the debug overlay and its release marker, pass `--show-debug-overlay` to the game's `--capture-frame` command; `tools/review.ps1` saves one under `artifacts/review-debug-overlay.png`.

To compare the projected pitch point and batting contact-zone guide across difficulty tiers, capture a released ball with `--contact-zone-preview Rookie`, `Standard`, or `Pro`; Rookie has the strongest bat outline, Standard a lighter outline, and Pro hides it. For example: `dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/contact-zone-rookie.png --camera behind-striker --ball-flight-time 0.10 --contact-zone-preview Rookie`.

Use `dotnet run --project src/SuperCricket.Game -- --profile-frames 300` for a live renderer profile. It warms up for up to 60 frames, then reports frame-interval and CPU update/draw-submission distributions; GPU execution timing requires a GPU profiler.

```powershell
dotnet run --project src/SuperCricket.Tools -- validate assets/deliveries/standard-pace.json
dotnet run --project src/SuperCricket.Tools -- simulate assets/deliveries/standard-pace.json
dotnet run --project src/SuperCricket.Tools -- validate assets/deliveries/yorker-pace.json
dotnet run --project src/SuperCricket.Tools -- simulate assets/deliveries/yorker-pace.json artifacts/yorker-flight.csv
dotnet run --project src/SuperCricket.Tools -- validate-shots assets/batting/shots.json
dotnet run --project src/SuperCricket.Tools -- analyze-batting assets/batting/shots.json
dotnet run --project src/SuperCricket.Tools -- verify-batting assets/batting/shots.json
dotnet run --project src/SuperCricket.Tools -- verify-match-batch
dotnet run --project src/SuperCricket.Tools -- verify-cpu-batting assets/characters/practice-batter.scplayer.json assets/characters/practice-bowler.scplayer.json assets/batting/shots.json assets/deliveries/standard-pace.json assets/deliveries/wide-pace.json assets/teams/highland-xi.json assets/teams/coastal-xi.json assets/fields/practice-attack.json
dotnet run --project src/SuperCricket.Tools -- simulate-match-batch assets/teams/coastal-xi.json assets/teams/highland-xi.json 100 2 3026 artifacts/match-batch.csv
dotnet run --project src/SuperCricket.Tools -- simulate-physics-match-batch assets/teams/coastal-xi.json assets/teams/highland-xi.json assets/fields/practice-attack.json assets/characters/practice-batter.scplayer.json assets/characters/practice-bowler.scplayer.json assets/batting/shots.json assets/deliveries/standard-pace.json assets/deliveries/wide-pace.json assets/deliveries/no-ball-pace.json 10 2 3710 artifacts/physics-match-batch.csv
dotnet run --project src/SuperCricket.Tools -- analyze-batting-practice assets/characters/practice-batter.scplayer.json assets/characters/practice-bowler.scplayer.json assets/batting/shots.json assets/deliveries/standard-pace.json artifacts/standard-batting-practice.csv
dotnet run --project src/SuperCricket.Tools -- verify-batting-practice assets/characters/practice-batter.scplayer.json assets/characters/practice-bowler.scplayer.json assets/batting/shots.json assets/deliveries/standard-pace.json
dotnet run --project src/SuperCricket.Tools -- analyze-batting-practice assets/characters/practice-batter.scplayer.json assets/characters/practice-bowler.scplayer.json assets/batting/shots.json assets/deliveries/yorker-pace.json artifacts/yorker-batting-practice.csv
dotnet run --project src/SuperCricket.Tools -- verify-batting-practice assets/characters/practice-batter.scplayer.json assets/characters/practice-bowler.scplayer.json assets/batting/shots.json assets/deliveries/yorker-pace.json
dotnet run --project src/SuperCricket.Tools -- validate-field assets/fields/practice-attack.json
dotnet run --project src/SuperCricket.Tools -- validate-team assets/teams/coastal-xi.json
dotnet run --project src/SuperCricket.Tools -- validate-team assets/teams/highland-xi.json
dotnet run --project src/SuperCricket.Tools -- analyze-field assets/fields/practice-attack.json
dotnet run --project src/SuperCricket.Tools -- simulate-over assets/scenarios/practice-over.json
dotnet run --project src/SuperCricket.Tools -- verify-match
dotnet run --project src/SuperCricket.Tools -- verify-fielding
dotnet run --project src/SuperCricket.Tools -- verify-footwork assets/characters/practice-batter.scplayer.json assets/characters/practice-bowler.scplayer.json assets/batting/shots.json assets/deliveries/wide-pace.json
```

The simulation command writes a CSV trajectory to `artifacts/standard-pace-trajectory.csv` by default. `analyze-field` estimates the fastest fielder to each point in the outfield on a 2 m grid and writes a coverage CSV; pass an output path and optional grid spacing in metres to change its defaults.

`analyze-batting` writes a CSV of shot speed, launch angle, and contact quality across the nine normalized blade contact points and three sample swing speeds. `verify-batting` checks moving-bat and moving-ball collision, outside-blade misses, sweet-spot quality, blade-height launch response, and swing-speed response using the same simulation code as the game.

`analyze-batting-practice` samples the exported batter clips against a real delivery preset and the bowler's run-up/release timing. It sweeps input time from 0.5 s before release until the ball reaches the batter and checks the available lateral footwork positions, then writes the chosen step distance alongside the contact window, sweet-spot quality, bat-point speed, outgoing speed, and in-play/four/six result for every shot. Contact is eligible only after shot input; misses have blank contact metrics, and launch angle describes the final outgoing velocity including bat movement. Outcomes are ballistic estimates without fielders or running. Pass an optional final step size in seconds to refine the timing grid; `verify-batting-practice` requires all three shots to find contact against the supplied delivery and checks an overlapping-blade fixture for pre-input contact. `verify-footwork` checks the step limits and confirms that all three shots can reach the wide-pace preset with a timed step. The yorker pace preset pitches near the striker and is checked for a low wicket-line crossing.

Validate the player export with `dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-batter.scplayer.json`. Regenerate the starter rig and game asset with:

```powershell
blender --background --factory-startup --python tools/blender/build_practice_batter.py -- `
  --blend-output assets/characters/practice-batter.blend `
  --asset-output assets/characters/practice-batter.scplayer.json
```

Create the editable 61-joint humanoid batter and bowler GLBs. The MonoGame runtime loads these GLBs directly through SharpGLTF.Core; the original `.scplayer.json` assets remain the authoring/analyzer reference while fielder-specific content is migrated. The exporter carries each gameplay clip's events and sampled root motion in `animations[].extras.superCricket`; the review checks those values against the source assets and validates the C# runtime import:

```powershell
blender --background --python tools/blender/build_humanoid_batter.py -- `
  --role batter `
  --blend-output assets/characters/practice-batter-humanoid.blend `
  --glb-output assets/characters/practice-batter-humanoid.glb
blender --background --python tools/blender/build_humanoid_batter.py -- `
  --role bowler `
  --blend-output assets/characters/practice-bowler-humanoid.blend `
  --glb-output assets/characters/practice-bowler-humanoid.glb
python tools/blender/validate_humanoid_glb.py assets/characters/practice-batter-humanoid.glb `
  --role batter --player-asset assets/characters/practice-batter.scplayer.json
python tools/blender/validate_humanoid_glb.py assets/characters/practice-bowler-humanoid.glb `
  --role bowler --player-asset assets/characters/practice-bowler.scplayer.json
dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-batter-humanoid.glb
dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-bowler-humanoid.glb
```

The Godot trial also imports the batter GLB to verify its skeleton, named actions, and grip-preview pose. The MonoGame loader currently supports opaque, untextured triangle meshes with identity mesh transforms, four joint influences per vertex, and the embedded animation metadata contract.

After editing the saved `.blend` in Blender or through Blender MCP, export that scene without rebuilding it:

```powershell
blender --background assets/characters/practice-batter.blend --python tools/blender/build_practice_batter.py -- `
  --from-scene --include-clip batting-step-offside --include-clip batting-step-legside `
  --blend-output assets/characters/practice-batter.blend `
  --asset-output assets/characters/practice-batter.scplayer.json
dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-batter.scplayer.json
```

The scene exporter reads skinned meshes marked `sc_player_part` from the `Player Mesh` collection. It keeps the five base clips in the batter file, authors the mirrored `batting-step-offside` and `batting-step-legside` actions when requested, and exports other named actions with `--include-clip`. The bowler source is an edited copy of the player scene with authored `bowling-run-up` and `overarm-delivery` actions; re-export it with:

When regenerating these procedural batting steps, pass `--rebuild-batting-footwork` with both `--include-clip` options. Without the rebuild flag, edited footwork actions in the `.blend` are preserved.

```powershell
blender --background assets/characters/practice-bowler.blend --python tools/blender/add_fielder_lower_legs.py
blender --background assets/characters/practice-bowler.blend --python tools/blender/author_fielding_animations.py
blender --background assets/characters/practice-bowler.blend --python tools/blender/build_practice_batter.py -- `
  --from-scene --include-clip overarm-delivery --include-clip bowling-run-up `
  --include-clip fielder-catch --include-clip fielder-pickup --include-clip fielder-throw `
  --blend-output assets/characters/practice-bowler.blend `
  --asset-output assets/characters/practice-bowler.scplayer.json
dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-bowler.scplayer.json
```

The bowler source tools add shin-weighted trouser meshes so the leg silhouette meets the shoes, then author the three fielder actions. The Blender `overarm-delivery` action stores its release marker in the `sc_events` custom property (`ball-release` at frame 21). The exporter converts that frame to seconds; the game reads the event from the validated player asset to time ball visibility and flight. The fielder throw has its own `ball-release` marker, and the catch and pickup clips mark when the ball is secured.

Each sampled pose also stores `rootMotion`, measured from the clip's first frame in player-local metres. The exporter removes horizontal root travel from the sampled bone transforms while retaining vertical body motion. The game rotates the bowler's authored run-up path by his facing direction; `validate-player` reports each clip's net root displacement.

For local AI-assisted authoring, `uvx mcp-for-blender setup` can install the Blender add-on and configure Codex; keep the server on `127.0.0.1` and set `BLENDER_MCP_SAFE_MODE=1` in its launch environment. Start the Blender add-on's MCP server before asking Codex to inspect or edit the scene. See [MCP for Blender setup and safe mode](https://github.com/ahujasid/mcp-for-blender).

Capture a deterministic, paused release frame for visual review with:

```powershell
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/practice-ground.png
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/bowling-target.png --bowling-target
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/ball-follow.png --camera ball-follow
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/ball-follow-flight.png --camera ball-follow --ball-flight-time 0.45
```

The game writes one PNG at the requested path and exits. Captures use the same DirectX renderer and content as a normal game launch. `--bowling-target` prepares the second innings and shows the pitch marker with the normal bowling prompts. Choose a camera preset with `--camera broadcast`, `--camera behind-striker`, `--camera bowler-end`, `--camera square-leg`, or `--camera ball-follow` to review another match view. Ball-follow tracks the incoming delivery, struck ball, and fielder throw with a smooth camera target. Use `--run-up-time 0.5` or `--delivery-time 1.0` to inspect the bowler animation, or `--ball-flight-time 0.45` to freeze a simulated ball and its trail at a chosen time after release. Use only one preview-time option; clip times must fit their actions and ball-flight time must fit the delivery preset. Delivery-time previews hide the ball so the bowler's pose and movement are clear.

```powershell
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/bowler-run-up.png --camera bowler-end --run-up-time 0.5
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/bowler-follow-through.png --camera bowler-end --delivery-time 1.0
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/fielder-catch.png --fielder-action fielder-catch --action-time 0.5
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/fielder-pickup.png --fielder-action fielder-pickup --action-time 0.32
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/fielder-throw.png --fielder-action fielder-throw --action-time 0.2
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/batter-step-offside.png --camera behind-striker --batter-footwork batting-step-offside --action-time 0.15
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/batter-step-legside.png --camera behind-striker --batter-footwork batting-step-legside --action-time 0.15
```

Prepare an AI-generated or artist-authored square texture tile with Python and Pillow before adding it to `assets/textures`:

```powershell
py -m pip install Pillow
py tools/prepare_texture.py source.png assets/textures/outfield-grass.png
```

The tool writes a 256×256 RGB PNG by default, feathers opposite borders for repeat sampling, and checks the wrapped edge delta. Pass `--size` and `--blend-width` to adjust those values. The game currently uses the grass tile over 6 m and the pitch tile over 4 m, which keeps fine source detail from sparkling in the broadcast view.
