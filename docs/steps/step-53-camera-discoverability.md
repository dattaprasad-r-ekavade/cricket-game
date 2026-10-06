# Step 53 — Camera framing and discoverability

**Status:** implementation, automated review, and capture review complete; human keyboard retest pending; physical GamePad retest pending.

## Change

The user's follow-up said the camera still felt badly framed and too far away. Tightened the broadcast, behind-striker, bowler-end, and square-leg views to 22 m, 16 m, 18 m, and 27 m. Shifted the bowler-end look target toward the bowler so the closer view keeps the bowler and the pitch marker in frame.

The live phase prompt now shows the camera shortcut: `V` on keyboard and left-stick click (`L3`) on GamePad. Added that GamePad action to the shared input model and documented both shortcuts in the pause menu and README. The review script now captures the behind-striker view alongside the default and bowling views.

## Review

- `dotnet build SuperCricket.sln -c Release` — passed with zero warnings or errors.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --verify-gameplay` — passed, including camera-distance, HUD shortcut, and feedback-card checks.
- `pwsh -File tools/review.ps1` — passed, including GamePad action mapping, all 20 deterministic match traces, and the updated camera captures.
- Visually inspected fresh 1440x900 broadcast, behind-striker, and bowler-end captures. The pitch and players are larger, the bowling target is centered, and the camera shortcut is visible in the phase HUD.

## Follow-up

The keyboard tester still needs to confirm that the view is comfortable while batting and bowling through a live over. Physical GamePad camera cycling remains untested. Keep A4d's human-playtest gate open until those checks are reported.
