# Step 130 — Verification hygiene (WP25)

Date: 2026-10-15

Status: complete; the latest hosted Windows validation run is green.

## Changes

- `tools/review.ps1` now stops before doing work unless it is running on PowerShell 7 or newer. The Windows PowerShell 5.1 check returned exit code 1 with the clear message: `tools/review.ps1 requires PowerShell 7 or newer; detected 5.1.26100.9711. Run it with 'pwsh'.`
- Added `tools/balance-bands.json` as the source for section 6's match-balance bands. The review checks that section 6 references the file, validates the target and smoke ranges, and reads its current smoke-check limits from the JSON rather than duplicating those numbers in PowerShell.
- The machine already provides PowerShell 7.6.5 at the Codex runtime path. `winget` is unavailable in this environment, so no separate system installation was needed to run the review.
- The quick 10-over review remains a smoke check until WP21 installs the full balance gate. Its latest sample averaged 123.2 runs per innings with a 15% run-out share; that is about 12.3 runs per over and is outside section 6's intended scoring band. WP21 still owns correcting match balance, and this result is not recorded as meeting the target.

## Checks

| Command | Result |
| --- | --- |
| `pwsh --version` | `PowerShell 7.6.5` |
| `pwsh tools/review.ps1` | Exit 0; solution and Godot trial Release builds passed with 0 warnings and 0 errors; Python animation-contract tests passed (4/4); content, field, match, gameplay and seeded-batch checks passed. Full output: [step-130-review-output.txt](step-130-review-output.txt). |
| `powershell.exe -NoLogo -NoProfile -File tools/review.ps1` | Exit 1 as intended under Windows PowerShell 5.1; version guard message shown above. |
| `git diff --check` | Passed. |

Review output excerpt:

```text
PowerShell 7.6.5
Build succeeded.
    0 Warning(s)
    0 Error(s)
Ran 4 tests in 0.858s
OK
PASS: six physics-grounded 10-over matches averaged 123.2 runs per innings with 16 leaves, 321 boundaries, 245 pickups, 47 results of 2+ runs from 116 multi-run plans, and a 15% run-out share across 20 dismissals.
Review checks passed. CSVs and any requested renderer captures are in artifacts/.
```

Hosted CI: [Windows validation run 96](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/38755908321) succeeded on commit `2c6fcf40310415395c5c6f79d606ba2419fbe954`.

## Hosted integration verification (16 October 2026)

[Windows validation run 98](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/38812431483) succeeded on commit `b02f59a941cdffae984178856603d2f1641d90d7`. The workflow's `dotnet test SuperCricket.sln -c Release` passed all 544 tests (308 Simulation, 203 Game, 33 Content; 0 failed, 0 skipped). `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures` passed the Release solution and Godot trial builds (0 warnings/errors), content checks, Standard 10/20-over balance gates, all five Human-profile batches, and the other headless checks.

```text
PASS: 50 Standard matches at 10 overs; 6.11 RPO, 3.36 wickets/innings, LBW 14.9%, caught 58.6%, bowled 15.8%, run-out 10.7%, max score 103.
PASS: 50 Standard matches at 20 overs; 6.58 RPO, 7.07 wickets/innings, LBW 15.1%, caught 59.8%, bowled 15.7%, run-out 9.3%, max score 182.
RESULT: Rookie Human-profile average score 305.6; Legend 255.4.
Review checks passed. CSVs and any requested renderer captures are in artifacts/.
```
