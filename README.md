# Super Cricket

A 3D cricket game built with C# and MonoGame. The current playable slice covers a complete over, with inspectable simulation and asset-authoring tools.

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

The match scene uses metres in world space and includes a procedural stadium preview with textured outfield and pitch surfaces, a marked oval boundary, and a static 4,800-spectator crowd. A teal-clad bowler runs in and plays an authored overarm delivery; the ball and its flight trail begin at the clip's release event. A defends, S drives, and D plays a lofted shot; choose the shot as the delivery approaches because an early or late swing can miss. Enter attempts a run; press it with a shot choice to start the runners at contact. X cancels a run. Number keys 1–3 select the next standard, wide, or no-ball delivery; N bowls the next ball, and R resets the over. P pauses/resumes the delivery, T cycles batter animations, V cycles broadcast/behind-striker/bowler-end/square-leg camera views, arrow keys orbit the camera, Page Up/Page Down change its elevation, the mouse wheel zooms, Home resets the broadcast camera, F1 toggles the developer overlay, and Escape exits.

## Repository layout

- `src/SuperCricket.Game` — MonoGame desktop application
- `src/SuperCricket.Simulation` — graphics-independent ball-flight, fielding, and over-scoring simulation
- `src/SuperCricket.Content` — validated delivery, player, and batting shot formats
- `src/SuperCricket.Tools` — local commands for validating presets, players, shots, and trajectories
- `assets` — editable delivery and field presets plus Blender-authored batter and bowler source/export
- `assets/textures` — seamless generated albedo tiles for the outfield and pitch
- `assets/batting/shots.json` — editable shot intent and launch tuning
- `tools/blender` — Blender scripts that generate and export players and animation clips
- `tools/prepare_texture.py` — resize and feather generated square texture tiles for repeat sampling
- `plan.md` — milestone plan and progress record

## Delivery tools

```powershell
dotnet run --project src/SuperCricket.Tools -- validate assets/deliveries/standard-pace.json
dotnet run --project src/SuperCricket.Tools -- simulate assets/deliveries/standard-pace.json
dotnet run --project src/SuperCricket.Tools -- validate-shots assets/batting/shots.json
dotnet run --project src/SuperCricket.Tools -- validate-field assets/fields/practice-attack.json
dotnet run --project src/SuperCricket.Tools -- analyze-field assets/fields/practice-attack.json
dotnet run --project src/SuperCricket.Tools -- simulate-over assets/scenarios/practice-over.json
```

The simulation command writes a CSV trajectory to `artifacts/standard-pace-trajectory.csv` by default. `analyze-field` estimates the fastest fielder to each point in the outfield on a 2 m grid and writes a coverage CSV; pass an output path and optional grid spacing in metres to change its defaults.

Validate the player export with `dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-batter.scplayer.json`. Regenerate the starter rig and game asset with:

```powershell
blender --background --factory-startup --python tools/blender/build_practice_batter.py -- `
  --blend-output assets/characters/practice-batter.blend `
  --asset-output assets/characters/practice-batter.scplayer.json
```

After editing the saved `.blend` in Blender or through Blender MCP, export that scene without rebuilding it:

```powershell
blender --background assets/characters/practice-batter.blend --python tools/blender/build_practice_batter.py -- `
  --from-scene `
  --blend-output assets/characters/practice-batter.blend `
  --asset-output assets/characters/practice-batter.scplayer.json
dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-batter.scplayer.json
```

The scene exporter reads skinned meshes marked `sc_player_part` from the `Player Mesh` collection. It keeps the five starter clips in the batter file and exports additional named actions with `--include-clip`. The bowler source is an edited copy of the player scene with authored `bowling-run-up` and `overarm-delivery` actions; re-export it with:

```powershell
blender --background assets/characters/practice-bowler.blend --python tools/blender/build_practice_batter.py -- `
  --from-scene --include-clip overarm-delivery --include-clip bowling-run-up `
  --blend-output assets/characters/practice-bowler.blend `
  --asset-output assets/characters/practice-bowler.scplayer.json
dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-bowler.scplayer.json
```

The Blender `overarm-delivery` action stores its release marker in the `sc_events` custom property (`ball-release` at frame 21). The exporter converts that frame to seconds; the game reads the event from the validated player asset to time ball visibility and flight.

Each sampled pose also stores `rootMotion`, measured from the clip's first frame in player-local metres. The exporter removes horizontal root travel from the sampled bone transforms while retaining vertical body motion. The game rotates the bowler's authored run-up path by his facing direction; `validate-player` reports each clip's net root displacement.

For local AI-assisted authoring, `uvx mcp-for-blender setup` can install the Blender add-on and configure Codex; keep the server on `127.0.0.1` and set `BLENDER_MCP_SAFE_MODE=1` in its launch environment. Start the Blender add-on's MCP server before asking Codex to inspect or edit the scene. See [MCP for Blender setup and safe mode](https://github.com/ahujasid/mcp-for-blender).

Capture a deterministic, paused release frame for visual review with:

```powershell
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/practice-ground.png
```

The game writes one PNG at the requested path and exits. Captures use the same DirectX renderer and content as a normal game launch. Choose a camera preset with `--camera broadcast`, `--camera behind-striker`, `--camera bowler-end`, or `--camera square-leg` to review another match view. Use `--run-up-time 0.5` to freeze in the run-up or `--delivery-time 1.0` to inspect the authored delivery and follow-through; either value must be within its clip duration, and the two time options cannot be combined. Delivery-time previews hide the ball so the bowler's pose and movement are clear.

```powershell
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/bowler-run-up.png --camera bowler-end --run-up-time 0.5
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/bowler-follow-through.png --camera bowler-end --delivery-time 1.0
```

Prepare an AI-generated or artist-authored square texture tile with Python and Pillow before adding it to `assets/textures`:

```powershell
py -m pip install Pillow
py tools/prepare_texture.py source.png assets/textures/outfield-grass.png
```

The tool writes a 256×256 RGB PNG by default, feathers opposite borders for repeat sampling, and checks the wrapped edge delta. Pass `--size` and `--blend-width` to adjust those values. The game currently uses the grass tile over 6 m and the pitch tile over 4 m, which keeps fine source detail from sparkling in the broadcast view.

