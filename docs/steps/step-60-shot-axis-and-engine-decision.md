# Step 60 — Shot axis, corrected trial replay, and engine decision

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

**Scope:** Finish Milestone B3 using the keyboard retest findings, shared-simulation evidence, refreshed Godot captures, and a bounded engine comparison.

## Findings and correction

The Step 58 drive was a false gameplay benchmark. At contact the batter is at `Z=-8.72 m`; the ball arrives from positive Z, so a straight shot from the striker travels toward positive Z. `BattingImpactModel` had built the straight direction with negative Z. The CPU placement model repeated the reversed direction and measured gaps from the opposite wicket at `Z=+8.72 m`.

The shared model now exposes one validated horizontal shot-direction function, and both human-impact calculation and CPU gap probes use it. CPU probes start at the actual striker's wicket line. Review checks assert that a straight drive and explicit CPU aims travel toward positive Z, and that the real-asset practice drive begins near the striker's wicket and travels forward. This corrects both the normal game and the Godot trial because they consume the same simulation project.

The standard real-asset sample contacts at 0.579 s with 0.90 contact quality. It leaves contact at approximately `(3.12, 2.94, 19.18) m/s`, travels 42 m, and settles inside the rope. The Godot result overlay now reports **IN PLAY · BALL SETTLED** instead of forcing a boundary label. The result camera is raised and moved to the side of the shot; procedural sight screens now sit outside the boundary rope. The delivery label uses the preset's calculated speed (122 km/h for this 34 m/s sample).

The 10-over review previously assumed every unscored two-run plan was resolved by a catch. That implication was not valid: run plans and final run totals are separate telemetry, and a delivery can end at a ground pickup or a stoppage. The review now checks the plan/result counts against contacts and boundaries, while the focused fielding review remains responsible for catch and run-out behavior.

## Captures and engine decision

Fresh 1440×900 Godot captures were generated with Godot .NET 4.7.2 on the RTX 4060 Laptop GPU and inspected. The contact capture shows the player, pitch, ball trail, and contact/timing feedback. The result capture now shows the actual in-play result, the ball, and the outfield; the wide capture shows the procedural oval. Comparable current MonoGame views are linked in [the B3 engine comparison](../reviews/godot-engine-comparison.md).

**Decision: keep MonoGame as the shipping game host for now.** It already runs the match loop, keyboard input, role cameras, feedback, match rules, and shared physics. The Godot trial proves that Blender GLB animation import, stadium scene authoring, shadows, and post-processing work, but it is still a deterministic one-delivery presentation. It has no human batting or bowling, running, full match, pause/settings flow, or keyboard usability evidence. Moving the shipping game now would trade a working interactive host for a rendering prototype without a measured net effort or performance win.

Keep the Godot scene as a reference and revisit migration only after a human-playable Godot over uses the shared simulation and has equivalent keyboard camera, delivery feedback, and match flow. No engine migration was made in this step.

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed with zero warnings and errors.
- `dotnet build trials/godot/SuperCricket.GodotTrial.csproj -c Release` — passed with zero warnings and errors.
- `tools/review.ps1` — passed: Release solution build, asset and gameplay checks, timing calibration, CPU batting, deterministic synthetic and physics matches, 20 live-match trace replays, renderer profile, and fresh MonoGame captures.
- Godot 4.7.2 runtime replay — loaded the Blender GLB and seven animations, reported the corrected positive-Z outgoing velocity, resolved the sample as `InPlay`, and wrote the fresh captures.
- Capture inspection — contact, result, and wide views inspected at 1440×900.
- `git diff --check` — passed.

## Limits

This is not a matched performance or active-time study. The earlier Godot B2 implementation commit records 1,115 added text lines across ten files plus a 411 KB GLB; current Godot trial code is a small presentation scene, while the tracked MonoGame game project has 4,892 C# lines including review checks and its much broader playable match. Those counts describe different scope and accumulated history, not equivalent development effort. No timesheet or same-scenario GPU/CPU profile exists for both hosts, so no hour-saved or frame-rate claim is justified.

The keyboard report that prompted Step 59 was taken on the earlier build. Step 59's closer role views and enlarged feedback still need a fresh human keyboard retest; GamePad testing and control-learning feedback remain open.
