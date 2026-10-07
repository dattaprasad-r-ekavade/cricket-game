# Step 66 — Closer role cameras and clearer live feedback

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

## Scope

The keyboard retest after Step 64 still found batting and bowling views badly framed and too distant, and the player could not see the live feedback. This pass addresses those two findings before resuming broader controls work.

## Changes

- The default behind-striker and bowler-end cameras move from 16 m / 38° to 12.5 m / 45°, with a slight side offset and a modestly higher view. They keep the pitch and incoming delivery visible while making the active batter or bowler larger and the bat/delivery line easier to separate.
- The V camera cycle, wider broadcast and square-leg views, ball-follow view, Page Up/Down, and mouse-wheel zoom remain available.
- Live batting and bowling feedback is larger and more prominent. The layout measures the phase HUD's rendered bounds and uses the upper-right area only when it fits beside that HUD; it adapts the card width for longer bowling prompts and moves below the HUD at narrower widths. This avoids covering the player or hiding keyboard prompts.
- No gameplay or ball-flight outcomes changed. Step 65's simulation-predicted bounce ring and Rookie/Standard/Pro contact-zone states remain in place.

## Cricket 07 research applied

The installed copy's `Support\en-uk\readme.txt` confirms that Behind Batsman and Flip camera views were shipped; it also documents an old Radeon 7500 transparency issue for those views. The same readme warns that simultaneous key presses can lose input on some keyboards and recommends practice nets for learning shots and timing. Its technical-help pages cover installation and display troubleshooting rather than gameplay controls.

EA's Cricket 07 producer described a deliberately wider default batting view to retain the bowler's run-up, alternate reverse views for players who wanted to face the bowler, a wider fielding view, and HUD aids such as a side-on bowler/shot picture-in-picture, running availability, radar, and an optional timing gauge. We preserve the useful distinction between readable action and a wider alternate view instead of forcing one framing on every player. Sources and limitations are documented in [the Cricket 07 research note](../reviews/cricket07-research.md).

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed, zero warnings and errors.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --verify-gameplay` — passed; checks camera defaults, dynamic safe-area layout, and earlier batting/bowling feedback behavior.
- Reviewed 1440×900 Release captures: `artifacts/step66-batting-feedback.png` and `artifacts/step66-bowling-feedback.png`.
- `tools/review.ps1` — passed; 20 live-match traces replayed exactly, full capture suite passed. 1440×900 VSync profile: 37.85 ms average / 47.06 ms p95 frame interval. GPU execution time is not included in CPU draw timing.

The keyboard retest is still required to confirm the revised framing and callout are understandable during actual play. GamePad and narrow-window visual checks remain open; the current profile also remains outside a 60 FPS frame budget.
