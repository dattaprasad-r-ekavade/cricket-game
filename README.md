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

The practice scene uses metres in world space. Arrow keys orbit the camera, Page Up/Page Down change its elevation, the mouse wheel zooms, Home resets the camera, and Escape exits.

## Repository layout

- `src/SuperCricket.Game` — MonoGame desktop application
- `plan.md` — milestone plan and progress record

