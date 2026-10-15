# Step 129 — Field presets and picker

## Behaviour

- Added 25 named right-hander field positions in `assets/fields/positions.json`, expressed as polar coordinates around the pitch. Left-handed batters mirror the x coordinate.
- Added nine authored formations in `assets/fields/presets/`: Attack, Standard and Defend for Pace, OffSpin and LegSpin. Each has nine fielders plus a wicketkeeper and a separate bowler reference.
- The content validator checks all references, the nine style/intent combinations, the five-player leg-side limit, and the two-player outside-ring powerplay limit.
- The CPU captain chooses Attack for a wicket in the previous 12 legal balls; Defend for a required rate of at least 10 or two boundaries in the previous over; otherwise it chooses Standard. Live selection is reevaluated at the start of an over or after a wicket.
- Before a human bowling run-up, Up/Down (keyboard) or the D-pad opens and cycles a radar preview. S/A applies the formation; W/B closes without applying. The picker does not pause the match. Fielders walk to the selected positions with the existing running cycle.
- The bowling target remains adjustable with Left/Right or the GamePad left stick while the picker is closed.

## Files

- `assets/fields/positions.json`
- `assets/fields/presets/*.json`
- `src/SuperCricket.Content/FieldPositionCatalog.cs`
- `src/SuperCricket.Content/FieldFormationCatalog.cs`
- `src/SuperCricket.Simulation/FieldPlacementModel.cs`
- `src/SuperCricket.Simulation/FieldingSide.cs`
- `src/SuperCricket.Simulation/LimitedOversMatch.cs`
- `src/SuperCricket.Game/Game1.FieldPicker.cs`
- `src/SuperCricket.Game/Game1.FieldRadar.cs`
- `src/SuperCricket.Game/Game1.MatchLifecycle.cs`
- `src/SuperCricket.Game/Game1.MatchLoop.cs`
- `src/SuperCricket.Game/MatchController.cs`
- `src/SuperCricket.Game/MatchInputRouter.cs`
- `src/SuperCricket.Tools/Program.cs`
- `tools/review.ps1`
- `README.md`

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed, 0 warnings and 0 errors.
- `dotnet test -c Release --no-build` — passed: 288 Simulation, 192 Game and 28 Content tests (508 total).
- `dotnet src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll validate-fields assets/fields/positions.json assets/fields/presets` — passed: 9 formations and 25 named positions; both standard and powerplay restrictions are checked during load.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --verify-gameplay` — passed all gameplay review checks.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --capture-frame docs/steps/captures/step-129-field-picker.png --field-picker-preview` — wrote the 1440×900 field picker/radar capture; visually inspected for readable controls and an unclipped legend.
- `pwsh tools/review.ps1` — passed; the final output was `Review checks passed.`

## Playtest checklist

- Open the field picker with Up/Down and cycle all three formations; confirm the radar updates without pausing.
- Press S to apply, then bowl; confirm fielders move to their positions rather than teleporting. Press W to close and verify the active field remains unchanged.
- Repeat with D-pad, A and B on a GamePad.
- Check that left-handed batting mirrors the field across the pitch.
- Observe CPU field changes after a wicket and between overs, including attack after a recent wicket and boundary protection after a high required rate or two boundaries in the previous over.

## Open question

The short-match powerplay window uses 30% of the selected innings, with a one-over minimum. Revisit this when full format-specific powerplay rules are introduced.
