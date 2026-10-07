# Step 102 — Separate animation metadata from legacy player assets

## Change

Moved the humanoid GLB builder and validator to compact `*.animation-contract.json` files. These contain only asset identity, clip names, events, and sampled root motion; Blender scenes continue to author the mesh and animation actions. The scene exporter now writes the contract directly from its in-memory asset model alongside `.scplayer.json`, so the legacy file is no longer an intermediate for contract refresh. It remains a C# compatibility input and a legacy-versus-GLB parity fixture.

Added an extractor for bootstrapping contracts from existing legacy assets, validation for contract schema and sample order, and automated tests that compare every embedded GLB animation's metadata before and after contract-based export.

## Verification

- `python -m py_compile` on all updated Blender and test scripts passed.
- `python -m unittest discover -s tests/tools -p 'test_*.py' -v`: all 4 tests passed, including deterministic contract writing, parity against both legacy assets, and exact event/root-motion metadata round trips for both GLBs.
- `dotnet test SuperCricket.sln -c Release`: all 127 tests passed.
- `pwsh -NoProfile -File tools/review.ps1`: passed in the default headless mode, including the new Python tests, contract-based GLB validation, both Release builds, timing calibration, and deterministic physics batches.
- No game window or renderer capture was started.
- Blender is not installed on this host, so the `.blend` scene export itself was not run; the exporter's direct contract-writing call was syntax-checked, its helper was tested, and re-embedding/validation used temporary copies of both checked-in GLBs.
- Hosted Windows validation passed in [run 37600403000](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37600403000).

## Remaining

The 61-joint batter and bowler remain untextured, fielders still use the bowler asset, and the legacy runtime loader remains until all player roles have migrated. Human visual review is still required for character and animation work.
