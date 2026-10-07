# Step 56 — Godot .NET trial bootstrap

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

**Scope:** Milestone B1 only. This is an engine-host proof, not an engine decision.

## Result

Added a Godot 4 .NET project under `trials/godot/`. The project references the existing `SuperCricket.Simulation` and `SuperCricket.Content` projects directly; neither shared project nor the MonoGame host was retargeted. It loads the repository's `assets/deliveries/standard-pace.json`, advances the existing fixed-step `BallFlightSimulator`, displays the ball and predicted bounce, then displays the actual bounce event and a status message.

The trial was run with the official Windows Godot .NET 4.7.2 build. The runtime log confirms the shared `Standard pace` content loaded and the simulation predicted its first bounce at `(0.00, 0.01, -5.62) m`. A movie-mode capture was inspected: it proves the scene and labels render, while also making clear the scene is still a simple flat-colour pitch prototype. It does not meet the project's visual-fidelity target.

## Verification

- `dotnet build trials/godot/SuperCricket.GodotTrial.csproj -c Debug` — passed, zero warnings/errors.
- `dotnet build trials/godot/SuperCricket.GodotTrial.csproj -c Release` — passed, zero warnings/errors.
- `dotnet build SuperCricket.sln -c Release` — passed, zero warnings/errors.
- Godot .NET 4.7.2 movie-mode run — loaded the shared delivery and emitted the predicted first-bounce log; the captured frame was visually inspected.
- `tools/review.ps1` — passed, including asset validation, deterministic simulation/gameplay checks, 20 live-match trace replays, profiling, and capture generation.
- `git diff --check` — passed; Git reported only its configured LF-to-CRLF conversion notices.

## Limits and next work

B1 demonstrates that Godot can host the existing simulation and content without an engine dependency leaking into those projects. It does not implement a stadium, player/glTF import, batting contact or shot presentation, quality shadows, or post-processing; those are B2. B3 must compare captures and effort and make a written engine decision. Continue the MonoGame keyboard retest gate for the changed batting/bowling views and feedback; this engineering trial does not close that gameplay gate.

The trial reads assets from this checkout, so run it from the repository. See [trials/godot/README.md](../../trials/godot/README.md) for setup and launch instructions.
