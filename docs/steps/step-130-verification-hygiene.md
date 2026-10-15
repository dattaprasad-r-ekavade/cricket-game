# Step 130 — Verification hygiene (WP25)

Date: 2026-10-15

Status: complete; the hosted Windows validation run is green.

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
