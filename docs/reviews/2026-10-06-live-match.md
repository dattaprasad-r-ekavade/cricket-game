# Production CPU match regression — 6 October 2026

The older `--verify-gameplay` command disables CPU batting and bowling variation to keep its focused scenarios stable. Step 45 adds coverage of the production CPU paths through the actual game's match update.

## Command and scope

```powershell
dotnet run --project src/SuperCricket.Game -c Release -- --verify-live-match artifacts/live-match-review.csv
```

The command initializes the real Windows game host, assets, rigs, animation samplers, and simulation. It advances the same match update used in play with scripted keyboard inputs. It preserves CPU batting control in the second innings and production skill-based bowling in stock deliveries. Explicit wide/no-ball diagnostic deliveries test extras once a chase lasts long enough to reach them.

Thirteen scenarios play both innings through game updates, covering three difficulty levels at 30/60/120 simulated FPS, longer two-over matches, and an all-out first innings. Seven additional scenarios prepare a first-innings score through the production scoring state and then play the complete CPU chase through game updates. Those prepared targets of 6, 24, and 72 are marked in the CSV; they are coverage fixtures, not simulated batting performances or balance measurements.

Every scenario repeats with its seed and compares delivery traces and results. Checks include input-plan execution, pause/resume during CPU flight, wide leaves without a swing, capped running plans, scorecard/result consistency, innings limits, valid roster identities after dismissal, match completion, and restart. CSV fields include shot, prediction/actual quality, contact time, plans, runs, extras, wickets, and legal-ball totals.

Review modes use default preferences, skip preference persistence, and suppress playback. Normal play retains saved preferences and sound. `tools/review.ps1` includes both game-host commands and checks that the saved preference file's hash is unchanged.

## Defect found and fixed

The first live all-out scenario crashed while resolving the displayed striker: the tenth wicket incremented `NextBatter` into batting order 12, outside the eleven-player roster. The scorecard now retains valid end identities when the innings is all out and only assigns a replacement while wickets remain. Simulation checks now cover both all-out innings, subsequent-delivery rejection, and a completed tied match. The game-host case also reads the HUD's roster properties after each delivery.

## Results

- Release build: zero warnings/errors.
- All 20 scenario traces repeat within their individual seeds and frame rates.
- 187 exported delivery records, including 86 CPU deliveries.
- 56 CPU contacts and eight leaves.
- Four completed running runs, from two scored doubles; no CPU run-outs in this set.
- A 24/0 prepared first innings was chased to 25/0 at each tested frame rate.
- A 72/0 prepared first innings was defended against 43/2, winning by 29 runs.
- Trace SHA256: `CC1ADDDD5BC24D4B7187D23C16A632C501C76B6A44AA65E270AC66E128B487D4`.
- The complete `tools/review.ps1` run passed, including the existing simulation/batch checks, both game-host suites, saved-preference preservation, renderer profiling, and six smoke captures. Its live trace hash matched the standalone run.

CSV/log output is generated under `artifacts/` and is not committed source.

## Limits and next work

The game-host scenarios advance updates directly during setup; they do not draw every frame, measure the latency of physical input, benchmark a sustained match, assess sound, or establish human enjoyment. Renderer smoke captures remain separate. Same-seed equality is only asserted at a fixed frame rate on this supported build.

The scripted human first innings in these seeds mostly produced fielded dots and some catches. The chase fixtures ensure broader CPU coverage, but cannot be used to claim fair human-versus-CPU balance. Improve readable human placement and shot selection, assess the running risk through actual play, and retain representative full-match captures before closing the Phase 5 playtesting gate. The broader Cricket 07 presentation target and release stabilization also remain open.
