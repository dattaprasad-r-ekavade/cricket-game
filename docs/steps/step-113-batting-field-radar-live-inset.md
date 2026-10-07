# Step 113 — Early pitch marker and live batting field inset

## Trigger

The latest keyboard playtest asked for the projected landing marker to appear before the ball reached the batter, a small in-play view to follow the ball and runners, and a field overview to help choose shot direction. Cricket 07's product page describes its radar, running assistance and picture-in-picture as snap-decision aids; its producer diary says the inset showed the bowler's approach, the live batting stroke and run availability. [EA Cricket 07](https://www.ea.com/fi-fi/games/cricket/cricket-2007), [producer diary #3](https://worthplaying.com/article/2006/11/13/news/37744-cricket-07-ps2pc-developer-diary-3/).

## Changes

- Show the human batter's projected bounce from the beginning of the delivery, before release, until the real bounce occurs. The bright marker is drawn through batter/bowler occlusion, and does not expose the human bowler's target to a CPU batter.
- Add a compact overhead batting field map with the boundary, wickets, current fielders, projected pitch point and an arrow for the current shot aim. The map updates with arrow-key or GamePad direction changes.
- At bat contact, switch that inset into a live field view. It tracks the ball and active chaser, marks both moving batters, reports run progress/returning state and repeats the run/turn controls.
- Add separate Release captures for the pre-shot field map and live field inset. The inset is an overhead tactical view to make player/ball positions legible; it is not a second rendered 3D camera.

## Verification

- `dotnet build SuperCricket.sln -c Release`: passed with no warnings or errors.
- `dotnet test SuperCricket.sln -c Release --no-build`: passed, 371 tests.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --verify-gameplay`: passed, including pre-release marker visibility, field-map orientation, aim-to-boundary projection and existing running checks.
- `tools/review.ps1 -CaptureVisuals -SkipGame`: passed, including the new dedicated live-run preview capture. The combined `-RunGameChecks -CaptureVisuals` review also passed before adding the standalone capture option.
- Inspected `artifacts/review-batting-field-map.png` and `artifacts/review-batting-field-pip.png`: projected landing and shot aim read in the batting map; the live inset shows the ball, active fielder, both runners and run progress. It closes when the delivery ends so the existing result card has its space.

## Acceptance still open

The owner needs to confirm that the early pitch point is visible from the normal batting view, that the shot arrow helps pick gaps, and that the ball/runners remain easy to read in the inset during a single. Keyboard and GamePad play should both be checked; GamePad remains untested.
