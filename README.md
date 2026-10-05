# Super Cricket

A 3D cricket game built with C# and MonoGame. The first milestone is a small, inspectable practice scene that can grow into a complete over.

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

The practice scene uses metres in world space. Arrow keys orbit the camera, Page Up/Page Down change its elevation, the mouse wheel zooms, Home resets the camera, Space pauses/resumes the delivery, R restarts it, T blends to the next player animation, and Escape exits.

## Repository layout

- `src/SuperCricket.Game` — MonoGame desktop application
- `src/SuperCricket.Simulation` — graphics-independent ball-flight simulation
- `src/SuperCricket.Content` — validated delivery and player asset formats
- `src/SuperCricket.Tools` — local commands for validating presets, players, and trajectories
- `assets` — editable delivery presets and Blender-authored player source/export
- `tools/blender` — Blender scripts that generate and export the practice batter
- `plan.md` — milestone plan and progress record

## Delivery tools

```powershell
dotnet run --project src/SuperCricket.Tools -- validate assets/deliveries/standard-pace.json
dotnet run --project src/SuperCricket.Tools -- simulate assets/deliveries/standard-pace.json
```

The simulation command writes a CSV trajectory to `artifacts/standard-pace-trajectory.csv` by default. Pass a different CSV path as the third argument to choose another location.

Validate the player export with `dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-batter.scplayer.json`. Regenerate the sample rig and game asset with:

```powershell
blender --background --factory-startup --python tools/blender/build_practice_batter.py -- `
  --blend-output assets/characters/practice-batter.blend `
  --asset-output assets/characters/practice-batter.scplayer.json
```

