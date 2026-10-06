# Godot .NET presentation trial

This side project compares Godot 4 with the existing MonoGame host. It references the unchanged, graphics-free `SuperCricket.Simulation` and `SuperCricket.Content` projects; the main game stays on MonoGame while the engine decision is open.

The B2 scene builds an oval stadium and pitch procedurally, imports the Blender practice batter as glTF/GLB, plays its authored stance and front-foot drive, and replays the shared incoming and outgoing ball simulation. A role camera frames the striker, a wide camera shows the stadium, and a ball-follow camera takes over after contact. The scene has a persistent delivery/contact/result card, a shadow-casting sun, procedural sky, filmic tone mapping, fog, and glow.

## Requirements and run

Install the .NET 9 SDK and the .NET-enabled Godot 4.7.2 editor/runtime. From the repository root:

```powershell
dotnet build trials/godot/SuperCricket.GodotTrial.csproj -c Debug
& "$env:LOCALAPPDATA\GodotTrials\4.7.2-mono\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path trials/godot
```

Press `C` to switch between the behind-striker role view and the high stadium view. Press `R` to replay the delivery. The ball-follow view takes over after contact unless `C` has selected a manual view.

## Reproducible assets and captures

The source player is `assets/characters/practice-batter.blend`. Export the rig and all Blender actions into the Godot project with:

```powershell
& "$env:USERPROFILE\scoop\apps\blender\current\blender.exe" --background --python trials/godot/tools/export-practice-batter.py
```

The exporter checks for one rig plus the stance and drive actions before writing `trials/godot/assets/practice-batter.glb`. Godot imports the GLB when the editor scans the project.

Generate fixed review captures from the repository root:

```powershell
& "$env:LOCALAPPDATA\GodotTrials\4.7.2-mono\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path trials/godot -- --capture=contact
& "$env:LOCALAPPDATA\GodotTrials\4.7.2-mono\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path trials/godot -- --capture=result
& "$env:LOCALAPPDATA\GodotTrials\4.7.2-mono\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path trials/godot -- --capture=wide
```

Each run saves a 1440×900 PNG under the ignored repository `artifacts/` directory. `contact` stops on the calibrated bat-contact feedback, `result` stops after the shared simulation resolves the drive as a boundary, and `wide` captures the oval stadium.

## Scope

The 13-bone batter and procedural stadium are proof assets for B2, not final production art. This trial has one batting delivery and shot; it does not yet include human bowling, a complete match, a production character rig, authored stadium art, or the B3 engine comparison. See [`docs/steps/step-58-godot-trial-visuals.md`](../../docs/steps/step-58-godot-trial-visuals.md) for what was built and verified.
