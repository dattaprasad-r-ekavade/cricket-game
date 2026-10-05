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

The match scene uses metres in world space. A defends, S drives, and D plays a lofted shot; choose the shot as the delivery approaches because an early or late swing can miss. Enter attempts a run; press it with a shot choice to start the runners at contact. X cancels a run. Number keys 1–3 select the next standard, wide, or no-ball delivery; N bowls the next ball, and R resets the over. P pauses/resumes the delivery, T cycles animations, arrow keys orbit the camera, Page Up/Page Down change its elevation, the mouse wheel zooms, Home resets the camera, and Escape exits.

## Repository layout

- `src/SuperCricket.Game` — MonoGame desktop application
- `src/SuperCricket.Simulation` — graphics-independent ball-flight, fielding, and over-scoring simulation
- `src/SuperCricket.Content` — validated delivery, player, and batting shot formats
- `src/SuperCricket.Tools` — local commands for validating presets, players, shots, and trajectories
- `assets` — editable delivery presets and Blender-authored player source/export
- `assets/batting/shots.json` — editable shot intent and launch tuning
- `tools/blender` — Blender scripts that generate and export the practice batter
- `plan.md` — milestone plan and progress record

## Delivery tools

```powershell
dotnet run --project src/SuperCricket.Tools -- validate assets/deliveries/standard-pace.json
dotnet run --project src/SuperCricket.Tools -- simulate assets/deliveries/standard-pace.json
dotnet run --project src/SuperCricket.Tools -- validate-shots assets/batting/shots.json
dotnet run --project src/SuperCricket.Tools -- simulate-over assets/scenarios/practice-over.json
```

The simulation command writes a CSV trajectory to `artifacts/standard-pace-trajectory.csv` by default. Pass a different CSV path as the third argument to choose another location.

Validate the player export with `dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-batter.scplayer.json`. Regenerate the sample rig and game asset with:

```powershell
blender --background --factory-startup --python tools/blender/build_practice_batter.py -- `
  --blend-output assets/characters/practice-batter.blend `
  --asset-output assets/characters/practice-batter.scplayer.json
```

