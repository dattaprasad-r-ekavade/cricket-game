# Super Cricket

A Windows 3D cricket prototype built with C# and MonoGame. Play a short two-innings match: bat first, then face the opponent's live, physics-grounded chase.

## Requirements

- Windows 10/11 x64
- .NET 9 SDK (selected through `global.json`)
- NuGet access for MonoGame packages

## Build and run

```powershell
dotnet restore
dotnet build SuperCricket.sln -c Release
dotnet run --project src/SuperCricket.Game
```

## Controls

| Situation | Keyboard | GamePad |
| --- | --- | --- |
| Aim and bat | Left/Right aim; Space ground/defend; Shift loft | Left stick aim; A ground/defend; Y loft |
| Run | Enter starts; tap again to request another; hold to turn back | B starts; tap again to request another; hold to turn back |
| Bowl | Arrows move the pitch target; C cycles delivery; N bowls | D-pad/left stick move the target; LB cycles delivery; RB bowls |
| Match | P pause; Esc quit; V camera; PgDn/PgUp zoom | Start pause; Back quit; L3 camera |
| Result | R replay; D difficulty; O overs | A replay; LB difficulty; RB overs |

The HUD shows actions for the current phase. Pause with P/Start for full controls and settings. While paused, H or Y toggles high contrast, T or X enlarges text, and -/+ or LB/RB changes effects volume. Preferences save to `%LOCALAPPDATA%\SuperCricket\settings.json`.

## Project guides

- [Development tools, checks, captures, and asset workflow](docs/tool-reference.md)
- [Current plan and open gates](plan.md)
- [Player asset contract](docs/design/player-asset-contract.md)
- [Latest playtest findings](docs/playtests/2026-10-07.md)
