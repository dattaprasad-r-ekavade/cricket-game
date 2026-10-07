# Step 61 — Reframe role cameras and live feedback

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

**Scope:** Respond to the owner's keyboard retest after Step 59: the batting and bowling views still felt badly framed, and the batting/bowling feedback was not apparent during play.

## Findings and changes

The Step 59 checks passed, but they verified camera distance and lens values, not the resulting composition. Its capture showed a large striker close to the camera and a small far wicket. The feedback card also sat over the middle of the pitch, competing with the ball and players.

Cricket 07's producer diary describes a wider default batting camera chosen to show the bowler's run-up and preserve anticipation. The game kept reverse-perspective views for people who preferred the batter's viewpoint; it used semi-transparent batter/keeper models in one close camera because those bodies hid the pitch-point marker. Its fielding camera also favored a wider view of developing play. The contemporary reviews document an optional early/on-time/late timing gauge and criticize the bowling pitch cursor as too sensitive, so Super Cricket keeps its visible target and measured outcome without copying that control problem. See [Cricket 07 research](../reviews/cricket07-research.md).

Human batting now starts in a wide behind-striker view at 21 m / 44° and human bowling uses a pitch-centered bowler-end view at 20 m / 44°. Both keep the shoulder offset, show the full pitch and delivery approach, and can still be tightened with Page Down or the mouse wheel. V continues to cycle the broadcast, square-leg, and ball-follow alternatives. This widens our existing behind-striker view; Cricket 07's separate close semi-transparent view and handedness-flipping Flip Cam remain future camera work.

Live feedback is now a compact top-right callout with an explicit role label: `YOUR BATTING` or `YOUR BOWLING`. It reports the measured contact/timing result or the line, length, and distance from the bowling target. The callout stays for seven seconds after a bounce or contact, while the world-space marker keeps its shorter visibility period. The persistent result card remains between balls.

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed with zero warnings and errors.
- `--verify-gameplay` — passed after adjusting the timer check for the simulation frame in which the bounce occurs.
- `tools/review.ps1` — Release review passed; asset checks, gameplay checks, live-match traces, renderer profile, and captures completed.
- The renderer profile recorded a 39.91 ms average frame interval (45.19 ms p50, 46.34 ms p95) at 1440×900 with VSync. CPU update and draw submission averaged 0.20 ms and 0.98 ms; GPU execution is not measured. The pre-change profile was similarly slow at 38.55 ms average, so this result is not attributed to Step 61 and does not establish the 60 fps target. It needs a separate GPU/target-machine check.
- 1440×900 captures were inspected: `artifacts/step61-behind-striker.png`, `artifacts/step61-behind-striker-flight.png`, `artifacts/step61-bowling-target.png`, `artifacts/step61-batting-feedback.png`, and `artifacts/step61-bowling-feedback.png`. Both role views show the near/far wicket relationship and pitch target; the batting and bowling callouts are readable at the top-right without covering the central pitch.
- `git diff --check` — passed.

These screenshots are a visual review of the generated state, not a human playtest. The owner's keyboard retest of this build is still required; GamePad validation and control-learning evidence remain open.
