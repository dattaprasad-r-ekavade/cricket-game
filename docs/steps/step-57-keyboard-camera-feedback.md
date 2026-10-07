# Step 57 — Keyboard camera and feedback response

**Branch:** `codex/keyboard-camera-feedback`

**Date:** 7 October 2026

**Scope:** Respond to the owner's keyboard retest after Step 55. No new gameplay system.

## Findings and changes

The owner reported that batting and bowling still had poor camera framing and that batting/bowling feedback was not visible. The earlier renderer captures established that overlays existed, but the playtest showed their presentation was not clear enough in use.

Human batting now begins behind the striker (13 m, 40-degree field of view) so the batter, incoming ball, and near pitch are the primary view. Human bowling retains the bowler-end view with a tighter 40-degree lens. Broadcast and square-leg remain available through the existing V / left-stick camera cycle. The normal HUD now uses a content-sized, five-line panel with less screen height and combines the trail key into its heading.

The live batting banner is larger, higher contrast, lasts longer, and uses direct player language such as `MIDDLE`, `EDGE CONTACT`, or `NO CONTACT`. Human bowling gets a distinct `ON TARGET` / distance-from-aim banner at the actual bounce, with measured pitch line and length. Completed-delivery cards are larger and put `YOUR BATTING RESULT` or `YOUR BOWLING RESULT` first, followed by the relevant contact/timing or aim/landing detail. The bowling card no longer presents the CPU batter's contact quality as the player's primary feedback.

Cricket 07's developer diary describes a useful camera trade-off: its wide default preserved the bowler's run-up and anticipation, while its behind-batter views showed the pitch from the player's side. It made the batter and keeper semi-transparent in the close view so they would not hide the pitch marker. Cricket 07 also used contextual picture-in-picture for the bowler's side-on run-up, the live batter shot, and run availability, plus an optional timing gauge. Super Cricket keeps the wider views available but now starts from the player's role view after the reported keyboard feedback.

The installed readme supports Windows 2000/XP and recommends an analog gamepad even though it permits keyboard/mouse. Native app windows were unavailable to the current computer-use surface, so this pass used the installed support files, prior local research, developer diary/review sources, and inspected Super Cricket captures. More detail is in [Cricket 07 research](../reviews/cricket07-research.md).

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed with zero warnings and errors.
- `--verify-gameplay` — passed, including role-camera settings, role-specific result content, and aim-accuracy labels.
- Batting and bowling `--feedback-preview` captures at 1440×900 — generated and visually inspected.
- Full `tools/review.ps1` — passed, including asset validation, gameplay checks, 20 deterministic live-match trace replays, renderer profiling, and capture generation.

## Retest still needed

The captures show the revised framing and overlay layout, but only the owner can confirm during play that the ball can be tracked and the feedback read without distraction. Keyboard retest remains open; GamePad retest remains open.
