# Step 102 — Separate animation metadata from legacy player assets

## Change

Moved the humanoid GLB builder and validator to compact `*.animation-contract.json` files. These contain only asset identity, clip names, events, and sampled root motion; Blender scenes continue to author the mesh and animation actions. The `.scplayer.json` files remain as a temporary bridge for refreshing contracts, C# compatibility inputs, and legacy-versus-GLB parity fixtures, but are no longer loaded directly by the humanoid GLB builder or validator.

Added an extractor for refreshing contracts from legacy exports, validation for contract schema and sample order, and automated tests that compare every embedded GLB animation's metadata before and after contract-based export.

## Verification

- `python -m py_compile ...`: all updated Blender and test scripts compiled.
- `python -m unittest discover -s tests/tools -p 'test_*.py' -v`: all 3 tests passed, including parity against both legacy assets and exact event/root-motion metadata round trips for both GLBs.
- `dotnet test SuperCricket.sln -c Release`: all 127 tests passed.
- `pwsh -NoProfile -File tools/review.ps1`: passed in the default headless mode, including the new Python tests, contract-based GLB validation, both Release builds, timing calibration, and deterministic physics batches.
- No game window or renderer capture was started.
- Blender is not installed on this host, so the `.blend` scene export itself was not run; contract re-embedding and validation were exercised on temporary copies of both checked-in GLBs.

## Remaining

The 61-joint batter and bowler remain untextured, fielders still use the bowler asset, and the legacy runtime loader remains until all player roles have migrated. Human visual review is still required for character and animation work.
