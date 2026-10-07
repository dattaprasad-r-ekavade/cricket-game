# Step 111 — HUD, shot mapping, and behind-batter view

## Trigger

The keyboard retest after Step 110 reported that the HUD did not map controls clearly, the player could not tell how a FOUR had been hit, the batting view still needed a direct behind-batter angle, and batter animations did not seem to respond. The full observation is in [the 7 October playtest log](../playtests/2026-10-07.md).

## Changes

- Replaced the flat match overlay with separate score and phase-action panels. The action panel maps keyboard and GamePad inputs, labels each action, shows the batting lane, and keeps pause/camera/zoom shortcuts visible.
- Clarified the batting model in the HUD, pause menu, and README: neutral aim plus Space/A is a straight defence; choosing a direction then pressing Space/A is a ground drive; Shift/Y lofts.
- Recorded the shot control at input time and included it with live batting feedback and the persistent delivery result, alongside timing and contact quality.
- Centered the behind-striker camera directly on the pitch axis.
- Changed batter shot clips to one-shot playback with a short blend and automatic recovery to stance. The HUD identifies when the batter is ready, stepping, swinging, or running.
- Added checks for keyboard/GamePad action mappings, result labels, shot-lane mapping, camera alignment, and action clip recovery.

## Verification

- `dotnet test tests/SuperCricket.Game.Tests/SuperCricket.Game.Tests.csproj -c Release --no-restore --verbosity minimal`: 143 passed.
- `dotnet run --project src/SuperCricket.Game -c Release -- --verify-gameplay`: passed.
- `tools/review.ps1`: passed; 9 Content, 206 Simulation, and 143 Game tests passed, along with the seeded match, batting, asset, and build checks.
- Inspected Release captures at `artifacts/step111-batting-controls.png` and `artifacts/step111-batting-feedback.png` at 1440x900. They show the separated control map, straight-behind-batter angle, readable timing/result panels, and a FOUR with shot/contact feedback.

## Acceptance still open

The project owner needs to retest keyboard batting, bowling, input-to-animation response, and feedback visibility in the running build. GamePad play is untested. The captures are not proof that the animation feels responsive to a player.
