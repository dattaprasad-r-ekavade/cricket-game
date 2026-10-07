# Super Cricket

A 3D cricket game built with C# and MonoGame. The playable prototype runs two innings, a target chase, and a match result. In the default single-player flow, the user bats first and the opponent controls the chase with live, physics-grounded batting decisions.

## Requirements

- Windows 10/11 x64
- .NET 9 SDK (the project was bootstrapped with 9.0.302)
- MonoGame 3.8.5.1 packages are restored automatically by NuGet

## Build and run

```powershell
dotnet restore
dotnet build
dotnet run --project src/SuperCricket.Game
```

The match scene uses metres in world space and includes a procedural stadium preview with textured outfield and pitch surfaces, a marked oval boundary, and a static 4,800-spectator crowd. Ten fielders use the shared skinned player rig; the active chaser switches to a running clip while the rest hold a ready stance. Catches and pickups play authored one-shot clips; run-out throws sequence pickup and overarm actions, with the ball leaving on the throw clip's release marker. Skinned parts are batched by material so the full 13-player scene avoids a draw call for every mesh part. A teal-clad bowler runs in and plays an authored overarm delivery; the ball and its flight trail begin at the clip's release event. The default match has one over per innings. In the first innings, use direction plus shot type: a neutral ground shot defends, a directed ground shot drives, and a lofted shot uses the other shot button. Choose as the delivery approaches because an early or late swing can miss. In the second innings, the CPU chooses shots, timing, and footwork from player ratings and chase pressure. After contact it checks the trajectory, fielders, pickup/throw timing, and boundary before planning a safe single or double. Rookie, Standard, and Pro difficulty tune CPU decisions while keeping ball and scoring rules fixed.

### Match controls

| Situation | Keyboard | GamePad |
| --- | --- | --- |
| Aim and bat | Left/Right aim; Space ground/defend; Shift loft | Left stick aim; A ground/defend; Y loft |
| Run | Enter starts; tap again to request another; hold to turn back | B starts; tap again to request another; hold to turn back |
| Bowl | Arrows move the pitch target; C changes the next delivery; N sends the next ball | D-pad/left stick move the pitch target; LB changes the next delivery; RB sends the next ball |
| Match | P pause; Esc quit; V changes camera; PgUp/PgDn zoom | Start pause; Back quit; L3 changes camera |
| Result | R replay; D difficulty; O overs | A replay; LB difficulty; RB overs |

The match HUD shows only the actions available in the current phase. Open the pause menu with P/Start for the full controls and settings. While paused, H or GamePad Y toggles high contrast, T or GamePad X toggles larger text, and -/+ or GamePad LB/RB lowers or raises effects volume. Bat contact, boundaries, wickets, and extras play short procedural prototype cues. Match, accessibility, and effects-volume preferences save to `%LOCALAPPDATA%\SuperCricket\settings.json`.

Run `dotnet run --project src/SuperCricket.Game -- --debug` to enable developer controls: A/S/D shot selection, J/L aim, Q/E authored footwork, T animation cycling, 1–4 delivery presets, camera orbit and elevation, mouse-wheel zoom, Home camera reset, and F1 diagnostics. In a normal match, Page Up/Down or the mouse wheel adjusts camera zoom. Capture either footwork pose deterministically with `--batter-footwork <clip> --action-time <seconds>`.

The CPU leaves a clear wide delivery when chase pressure is low and may take the shot when it needs runs.

## Repository layout

- `src/SuperCricket.Game` — MonoGame desktop application
- `src/SuperCricket.Simulation` — graphics-independent ball flight, batting impact, fielding, delivery resolution, and match state
- `src/SuperCricket.Content` — validated delivery, player, and batting shot formats
- `src/SuperCricket.Tools` — local commands for validating content, analyzing batting and field coverage, and replaying scenarios
- `assets` — editable delivery and field presets plus Blender-authored batter and bowler source/export
- `assets/teams` — validated fictional team rosters with batting order, ratings, and kit colors
- `assets/textures` — seamless generated albedo tiles for the outfield and pitch
- `assets/batting/shots.json` — editable shot intent and launch tuning
- `tools/blender` — Blender scripts that generate and export players, add the fielder lower legs, and author fielding actions
- `tools/prepare_texture.py` — resize and feather generated square texture tiles for repeat sampling
- `plan.md` — milestone plan, current assumptions, and progress record
- `docs/reviews` — dated gameplay and planning reviews
- `docs/design` — short-match brief, visual board, hardware baseline, and validated player/team asset contracts

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

Create the separate 61-joint humanoid/GLB migration pilot without overwriting the original player assets, then validate its glTF structure:

```powershell
blender --background --python tools/blender/build_humanoid_batter.py -- `
  --blend-output assets/characters/practice-batter-humanoid.blend `
  --glb-output assets/characters/practice-batter-humanoid.glb
python tools/blender/validate_humanoid_glb.py assets/characters/practice-batter-humanoid.glb
```

The Godot trial imports this GLB to verify its skeleton, named actions, and grip-preview pose. It does not replace the current MonoGame `.scplayer.json` runtime assets.

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

