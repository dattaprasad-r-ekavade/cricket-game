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
| Aim and bat | Arrow keys aim (Up/Down choose downfield or behind). `S` defends; `S+D` is a forward stroke; `W+D` is a hit behind. `Shift+S` lofts / attempts a six; `Shift+W+D` is an extra back-foot loft option. Keystrokes are recorded in order for up to 220 ms, so they may be rolled instead of pressed together. | Left stick chooses a lane; A at centre defends, with a lane selected drives; Y lofts |
| Run | `D` starts or requests another run after contact; tap `A` to run back | B starts; tap again to request another; hold to turn back |
| Bowl | Arrows move the pitch target; C cycles delivery; N bowls | D-pad/left stick move the target; LB cycles delivery; RB bowls |
| Match | P pause; Esc quit; V camera; PgDn/PgUp zoom | Start pause; Back quit; L3 camera |
| Result | R replay; D difficulty; O overs | A replay; LB difficulty; RB overs |

The batting HUD records staggered key presses for up to 220 ms and shows the sequence that selected the shot. This follows a secondary PC reference; the installed Cricket 07 readme warns some keyboards can lose simultaneous keys. The HUD shows actions for the current phase. Pause with P/Start for full controls and settings. While paused, H or Y toggles high contrast, T or X enlarges text, and -/+ or LB/RB changes effects volume. Preferences save to `%LOCALAPPDATA%\SuperCricket\settings.json`.

While batting, the field map shows the current fielders, your shot lane, and the projected bounce before release. After contact it switches to a live field inset that tracks the ball, chasing fielder, batters, and run progress.

## Project guides

- [Development tools, checks, captures, and asset workflow](docs/tool-reference.md)
- [Current plan and open gates](plan.md)
- [Player asset contract](docs/design/player-asset-contract.md)
- [Latest playtest findings](docs/playtests/2026-10-07.md)
