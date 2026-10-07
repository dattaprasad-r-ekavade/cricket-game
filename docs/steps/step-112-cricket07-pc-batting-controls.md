# Step 112 — Cricket 07 PC batting controls and stroke animations

## Trigger

The keyboard playtest after Step 111 produced a single successfully, then reported that the controls should follow Cricket 07's PC key combinations and that the batter animations felt arbitrary. The installed game warns that some keyboards lose input when multiple keys are pressed. Research and its source limits are recorded in [the Cricket 07 notes](../reviews/cricket07-research.md).

## Changes

- Added a 220 ms ordered key recorder for batting chords. Keys pressed in one frame display as a chord (`S + D`); keys rolled over several frames display in order (`S → D`). A lone `S` starts its block animation immediately, and a following `D` changes it to a front-foot stroke without waiting for the input window to expire.
- Matched the secondary PC guide's core layout: arrows aim, `S` blocks, `S+D` is the forward stroke, `W+D` is the hit-behind stroke, `Shift+S` is the loft/six attempt, and `D`/`A` run and run back. `Shift+W+D` is identified in the HUD as an extra back-foot loft option, not an established Cricket 07 binding.
- Added authored `back-foot-drive` and `back-foot-loft` clips to the 13-bone Blender source and 61-bone humanoid GLB. Each recorded keyboard stroke now selects its matching animation clip; feedback includes the direction and actual recorded keys.
- Added up/down depth aim to the shot vector. A back-foot `W+D` stroke defaults behind the striker; arrow aim can redirect it downfield or diagonally.
- Kept GamePad controls unchanged. Keyboard running now uses `D` to run/repeat and a tap of `A` to run back.
- Updated the HUD, timing cue, pause menu, README, Blender export/validation requirements, and gameplay checks.

## Verification

- `dotnet test SuperCricket.sln -c Release --no-restore`: passed, 371 tests (9 Content, 207 Simulation, 155 Game).
- `dotnet run --project src/SuperCricket.Game -c Release -- --verify-gameplay`: passed, including immediate block-to-drive selection, front/back-foot/loft clips, depth aim, and keyboard/GamePad running.
- `tools/review.ps1 -RunGameChecks -CaptureVisuals`: passed, including GLB/contract validation, seeded matches, and 1440x900 captures. After correcting unsupported HUD glyphs, reran all 371 tests, gameplay review, and the batting timing capture; the ASCII chord order and two-axis aim read clearly.
- Blender 5.2 exported the existing practice batter with nine actions and regenerated the humanoid GLB. `validate_humanoid_glb.py` passed with 61 joints, 10 runtime clips, 89 skinned primitives, and all 30 finger segments in the grip animation.
- A Blender MCP frame strip was inspected for the back-foot drive at frames 1, 13, 19, 28, 40, and 61; the contact pose shows the bat crossing the front of the body before follow-through and recovery.

## Acceptance still open

The owner needs to retest keyboard rollover, whether the 220 ms chord window feels right, shot direction, and animation response in Release. The PC key chart came from a secondary guide, and the installed manual warns that simultaneous-key support varies by keyboard. GamePad play is still untested.
