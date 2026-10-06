# Step 59 — Camera and feedback visibility follow-up

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

**Scope:** Respond to the owner's keyboard report that Step 57 still felt poorly framed and the batting/bowling feedback remained hard to see.

## Changes

The default batting view is now an offset behind-striker shoulder camera at 11.5 m with a 38-degree field of view. Bowling uses a 15 m bowler-end shoulder camera with the target on the pitch's visual center. This keeps a clear line toward the far wicket while separating the main player from the center of the pitch. `V` still cycles the alternate cameras.

Keyboard players can adjust zoom during a match with Page Up (out) and Page Down (in); mouse-wheel zoom also works outside developer mode. Zoom stays within 6–32 m. The active camera prompt and pause help identify the keyboard control.

Live contact and pitch feedback now appears centered below the HUD in a wider, larger panel. Batting feedback includes the selected shot, calibrated timing, and contact percentage; bowling feedback includes length, line, and distance from the player's aim. Contact feedback lasts four seconds and bounce feedback lasts 4.5 seconds. The completed-delivery result card remains on screen until the player moves to the next ball.

The Cricket 07 research note now cross-checks EA's product page with its producer diary. Those materials describe a wider broadcast view, alternate behind-batter views, and contextual picture-in-picture for timing, running, and the bowler's delivery. The updated captures and sources are in [Cricket 07 research](../reviews/cricket07-research.md).

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed with zero warnings and errors.
- `tools/review.ps1` — passed, including asset checks, keyboard/GamePad gameplay assertions, live-match trace replays, renderer profile, and fresh captures.
- Camera checks cover the new distances, view offsets, and Page Up/Down zoom behavior with scripted keyboard state.
- `artifacts/step59-behind-striker.png`, `step59-batting-feedback.png`, and `step59-bowling-feedback.png` were inspected at 1440×900. The shot/timing banner and bowling line/length/aim banner are centered and legible; the results stay in the bottom card.
- `git diff --check` — passed.

## Retest

Capture review cannot prove the batting ball remains easy to track while actually playing. The owner's keyboard retest of these defaults and GamePad testing remain open. The control-learning question also remains unanswered.
