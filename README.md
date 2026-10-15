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
| Aim and bat | Arrow keys choose one of 8 directions. `S` attacks; `W` defends; hold `Shift` to add loft. `A` leaves the ball. `D` advances before release. Pause and press `I` to switch to Classic chords. | Left stick chooses direction; A attacks; X defends; RB lofts; B leaves |
| Run | `D` starts or requests another run after contact; tap `A` to turn back | B starts or requests another run; hold to turn back |
| Advance | `D` steps down the pitch before release | LB steps down the pitch before release |
| Bowl | CPU bowling: Left/Right aim, C cycles delivery, N bowls. Human bowling: Left/Right aim; Up/Down opens and previews a field preset; S selects; W closes; 1–8 choose a delivery and start the run-up; Space sets pace. | CPU bowling: D-pad/left stick aim, LB cycles delivery, RB bowls. Human bowling: left stick aims; D-pad Up/Down previews a field preset; A selects; B closes; face buttons choose a delivery; LB/RB page; RT sets pace. |
| Match | P pause; Esc quit; V camera; PgDn/PgUp zoom | Start pause; Back quit; L3 camera |
| Result | R replay; D difficulty; O overs | A replay; LB difficulty; RB overs |

The default keyboard batting scheme separates direction, attack, defence, loft, and leave. The saved Classic chords option records staggered key presses for up to 220 ms. Pause with P/Start for full controls and settings; `I` switches schemes and `F` toggles automatic/manual foot selection (manual: W back foot, S front foot). While paused, H or Y toggles high contrast, T or X enlarges text, and -/+ or LB/RB changes effects volume. Preferences save to `%LOCALAPPDATA%\SuperCricket\settings.json`.

While batting, the field map shows the current fielders, your shot lane, and the projected bounce before release. After contact it switches to a live field inset that tracks the ball, chasing fielder, batters, and run progress.

## Project guides

- [Development tools, checks, captures, and asset workflow](docs/tool-reference.md)
- [Current plan and open gates](plan.md)
- [Player asset contract](docs/design/player-asset-contract.md)
- [Latest playtest findings](docs/playtests/2026-10-07.md)
