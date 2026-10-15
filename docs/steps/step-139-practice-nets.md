# Step 139 — Practice nets and guided batting

## Scope

Implements the WP12 practice-net mode, the guided batting lesson, and a bowling target drill while reusing the existing live match simulation and authored delivery catalogue.

## Changes

- Added a keyboard/GamePad mode menu on startup and an `M` shortcut from a live match to reopen it. The Practice Nets setup lets the player choose batting or bowling drill, bowler style, line, length, and pace.
- Batting tutorial deliveries repeat automatically. The three coach prompts progress after three Ideal contacts per lesson step; direction and loft prompts require the corresponding input.
- Added a bowling target drill with a fixed pitch ring. Legal first-bounce hits count toward five; an over restarts until the target is reached.
- Added a recent 10-ball timing list and zone histogram to the practice HUD.
- Added `--practice-setup-preview` for deterministic UI captures and routed the capture through `tools/review.ps1 -CaptureVisuals`.
- Added presenter tests for step progression, delivery setup, zone histogram trimming, and the five-hit target.

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed with 0 warnings and 0 errors.
- `dotnet test SuperCricket.sln -c Release` — passed, 539 tests (306 Simulation, 203 Game, 30 Content).
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --verify-gameplay` — passed.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --capture-frame artifacts/review-practice-setup.png --practice-setup-preview` — passed; inspected the 1440×900 setup capture and corrected overlay ordering so the setup panel is unobscured.

## Human playtest still required

WP12 remains open until a person verifies the lesson can be completed in under three minutes. Please check:

- Start Practice Nets from the title menu and finish the three steps; confirm the prompts disappear after three qualifying Ideal contacts each and a mistimed loft still explains the result.
- Confirm the last-ten timing list and histogram update during batting practice.
- Select the bowling target drill and land five legal first bounces inside the ring.

## Artifact

- [Practice Nets setup capture](../../artifacts/review-practice-setup.png)
