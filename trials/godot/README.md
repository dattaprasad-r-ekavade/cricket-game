# Godot .NET engine trial

This branch isolates an engine comparison without changing the shipping MonoGame host. The project references the existing `SuperCricket.Simulation` and `SuperCricket.Content` projects directly; both remain engine-independent and target `net9.0`.

The proof scene loads the same `assets/deliveries/standard-pace.json` used by the MonoGame build. It advances the existing fixed-step `BallFlightSimulator`, displays its ball in a simple Godot 3D pitch scene, and compares the predicted pitch point with the actual bounce event.

## Run

Install the .NET-enabled Godot editor and .NET 9 SDK, then from the repository root run:

```powershell
dotnet build trials/godot/SuperCricket.GodotTrial.csproj
& "$env:LOCALAPPDATA/GodotTrials/4.7.2-mono/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64.exe" --path trials/godot
```

The trial reads the delivery JSON from the repository's `assets/` folder, so keep the project in this checkout. B1 proves the engine host can consume the unchanged simulation and content projects; the stadium, glTF player, shot/contact, shadows and post-processing comparison belongs to B2.
