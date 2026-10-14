# Step 126 — Batting input scheme

## Behaviour

- Directional keyboard batting is the saved default: arrows choose any of eight right-hander-relative lanes, `S` attacks, `W` defends, held Shift lofts, `A` leaves, and `D` advances before release. Directional shot requests commit on the press edge.
- The non-developer GamePad map uses the left stick for direction, A to attack, X to defend, RB to loft, B to leave/run, and LB to advance while batting. After contact, keyboard D/A and GamePad B retain run and turn-back behavior.
- Keyboard I switches between Directional and the saved Classic chords scheme while paused; F switches between automatic and manual foot selection. Manual selection maps W to back foot and S to front foot. Both settings persist in `GameSettingsStore`.
- Batting action hints fit within four rows and identify the active device and scheme. README controls and settings instructions describe the same bindings.
- Left-handed outcomes mirror the selected right-hander-relative direction through the existing outcome resolver.

## Files

- `README.md`
- `src/SuperCricket.Game/Game1.ActionHud.cs`
- `src/SuperCricket.Game/Game1.FeedbackHud.cs`
- `src/SuperCricket.Game/Game1.InterfaceAndSettings.cs`
- `src/SuperCricket.Game/Game1.MatchActions.cs`
- `src/SuperCricket.Game/Game1.MatchLifecycle.cs`
- `src/SuperCricket.Game/Game1.MatchLoop.cs`
- `src/SuperCricket.Game/Game1.SceneRendering.cs`
- `src/SuperCricket.Game/Game1.cs`
- `src/SuperCricket.Game/GameplayAdvancedReviewChecks.cs`
- `src/SuperCricket.Game/GameplayReviewChecks.cs`
- `src/SuperCricket.Game/MatchHudPresenter.cs`
- `src/SuperCricket.Game/MatchInputRouter.cs`
- `src/SuperCricket.Simulation/Batting/StrokeDirectionModel.cs`
- `src/SuperCricket.Simulation/GameSettingsStore.cs`
- `src/SuperCricket.Simulation/MatchControllerInputModel.cs`
- `tests/SuperCricket.Game.Tests/MatchHudPresenterTests.cs`
- `tests/SuperCricket.Game.Tests/MatchInputRouterTests.cs`
- `tests/SuperCricket.Simulation.Tests/GameSettingsReviewChecks.cs`
- `tests/SuperCricket.Simulation.Tests/MatchControllerInputReviewChecks.cs`
- `docs/build/a5-build-plan.md`

## Verification

- `dotnet test -c Release --no-restore` — passed: 285 Simulation, 166 Game, and 25 Content tests (476 total).
- `pwsh tools/review.ps1 -RunGameChecks` — passed, including full build, headless checks, seeded physics and live match batches, saved-preference checks, and gameplay checks.
- Router tests cover all eight arrow directions, attack/defend requests, loft, manual/automatic foot values, leave/advance, and the Classic chords recorder. Existing resolver coverage verifies the left-hander mirror.
- Gameplay review verifies the default S attack, Classic chord animations, keyboard/gamepad aiming, manual running and turn-back, and the non-developer GamePad path.

## Playtest checklist

- Try all eight arrow directions with a right-handed batter and a left-handed batter; confirm the lane mirrors while the control remains consistent.
- Compare automatic and manual foot selection against short, full, and good-length balls.
- Pause and switch schemes; confirm the choice survives restarting the game.
- Advance before release, then verify D/A still control running and turning back after contact.

## Open questions

- None for this work package. Human control feel remains part of the wave 3 playtest gate.
