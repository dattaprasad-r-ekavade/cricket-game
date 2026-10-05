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

The match scene uses metres in world space and includes a procedural stadium preview. A defends, S drives, and D plays a lofted shot; choose the shot as the delivery approaches because an early or late swing can miss. Enter attempts a run; press it with a shot choice to start the runners at contact. X cancels a run. Number keys 1–3 select the next standard, wide, or no-ball delivery; N bowls the next ball, and R resets the over. P pauses/resumes the delivery, T cycles animations, V cycles broadcast/behind-striker/bowler-end/square-leg camera views, arrow keys orbit the camera, Page Up/Page Down change its elevation, the mouse wheel zooms, Home resets the broadcast camera, F1 toggles the developer overlay, and Escape exits.

## Repository layout

- `src/SuperCricket.Game` — MonoGame desktop application
- `src/SuperCricket.Simulation` — graphics-independent ball-flight, fielding, and over-scoring simulation
- `src/SuperCricket.Content` — validated delivery, player, and batting shot formats
- `src/SuperCricket.Tools` — local commands for validating presets, players, shots, and trajectories
- `assets` — editable delivery and field presets plus Blender-authored player source/export
- `assets/batting/shots.json` — editable shot intent and launch tuning
- `tools/blender` — Blender scripts that generate and export the practice batter
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

The scene exporter reads skinned meshes marked `sc_player_part` from the `Player Mesh` collection. It keeps the five starter clips in the Blender file and exports them with the meshes. For local AI-assisted authoring, `uvx mcp-for-blender setup` can install the Blender add-on and configure Codex; keep the server on `127.0.0.1` and set `BLENDER_MCP_SAFE_MODE=1` in its launch environment. Start the Blender add-on's MCP server before asking Codex to inspect or edit the scene. See [MCP for Blender setup and safe mode](https://github.com/ahujasid/mcp-for-blender).

Capture a deterministic, paused startup frame for visual review with:

```powershell
dotnet run --project src/SuperCricket.Game -- --capture-frame artifacts/practice-ground.png
```

The game writes one PNG at the requested path and exits. Captures use the same DirectX renderer and content as a normal game launch. Choose a camera preset with `--camera broadcast`, `--camera behind-striker`, `--camera bowler-end`, or `--camera square-leg` to review another match view.

