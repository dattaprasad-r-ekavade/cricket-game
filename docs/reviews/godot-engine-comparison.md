# Godot and MonoGame engine comparison

**Date:** 7 October 2026

**Decision:** keep MonoGame as the shipping host; retain Godot as a bounded presentation trial.

## What was compared

Both hosts use the same graphics-free `SuperCricket.Simulation` and `SuperCricket.Content` projects. The B3 axis audit corrected a real shared bug: at the striker's wicket (`Z=-8.72 m`), a straight shot must move toward positive Z. The human batting impulse and CPU placement probes now share that convention. The previous Godot boundary screenshot came from the reversed direction and must not be used as a correct gameplay result.

The image set shows each host at its present scope. Godot uses the same Blender practice batter's seven exported clips in a procedurally built stadium; MonoGame is the interactive match renderer and includes gameplay HUD. These are comparison captures, not an art-identical benchmark.

| MonoGame | Godot .NET trial |
| --- | --- |
| [Behind-striker match view](../../artifacts/review-behind-striker.png) | [Calibrated contact](../../artifacts/godot-b3-contact.png) |
| [Batting feedback and result](../../artifacts/review-batting-feedback.png) | [Corrected in-play result](../../artifacts/godot-b3-result.png) |
| [Bowling target and feedback](../../artifacts/review-bowling-feedback.png) | [Procedural stadium view](../../artifacts/godot-b3-wide.png) |

## Trade-offs

| Area | MonoGame host | Godot trial |
| --- | --- | --- |
| Game scope | Playable keyboard match flow, batting/bowling/fielding systems, pause/settings, match rules, and live feedback are already integrated. | One deterministic standard-pace delivery and drive; no human batting/bowling, running, fielding decisions, full innings, or settings. |
| Scene and lighting workflow | Stadium geometry, player rendering, cameras, and presentation are custom game code. MonoGame's official 3D guide describes the vertex-buffer/effect pipeline; its content supports stock and custom effects, so the game owns the higher-level scene and rendering systems. | Godot provides an editable scene and built-in Forward+ renderer. Its official 4.7 asset guidance supports glTF/GLB workflows from Blender, and the trial uses built-in shadows, environment, tone mapping, fog, and glow. |
| Blender player | Current runtime still uses the custom validated player asset. A production GLB importer/animation pipeline for MonoGame remains work. | Imports the same Blender-authored GLB and all seven named actions directly. This is the clearest demonstrated asset-pipeline advantage. |
| Camera and feedback | Human keyboard feedback exists in the current host, but Step 59 needs a new human retest after its framing and visibility changes. | Three deterministic presentation views and a visible result card; no keyboard-driven camera/usability pass has been done. |
| Reliability evidence | The full Release review exercises the match, input routing, shared simulation, fielding, captures, and seeded replays. | Godot project builds and imports the Blender scene; its single delivery replays at the shared 120 Hz step. It does not yet pass an equivalent interactive-match review. |

Godot reduces the work needed to assemble a lit scene and preview Blender animation. That does not yet establish lower total game-development effort: its one-shot proof omits the game systems that MonoGame already has. The B2 implementation commit shows 1,115 added text lines and a 411 KB GLB, but no active-hours log exists. A raw line-count comparison also misleads: the current trial has 931 lines in `Main.cs` and `TrialStadiumBuilder.cs`, while the tracked MonoGame game project has 4,892 C# lines, including review checks and the complete match. The scopes, history, and feature counts differ.

There is also no same-machine, same-scene Godot-versus-MonoGame GPU/CPU profile. The MonoGame review profile and the Godot 1440×900 captures were produced separately, so this evidence supports no performance claim.

## Decision and reopening gate

Keep MonoGame for the shipping game. The user's original target and the existing keyboard-playable match make a renderer migration premature; the Godot result is not yet a game the owner can play. Continue using Blender as the authoring source, and keep the Godot GLB work as evidence for a later pipeline decision. If MonoGame remains the host, add a production-ready GLB import/animation path before expanding the character library.

Reopen the engine decision after the Godot trial can run one complete human-playable over with shared rules and simulation, batting and bowling input, running, field feedback, pause/settings, and the camera/result feedback called out in the keyboard reports. At that point, time both implementations on a clearly bounded equivalent feature and profile the same scene on the Lenovo baseline and current RTX 4060 system.

## Cricket 07 design reference

The local Cricket 07 installation's readme supports keyboard input but recommends an analog controller for tutorial button call-outs; it warns about multi-key rollover. Its instructions recommend practice nets and run assist. I could reach the title screen, but the installed game did not advance past “Click to continue” in this Windows 11 session, so the in-match details below are based on primary EA material and contemporary reviews rather than claimed direct observation.

EA deliberately made the default batting camera wider so players could see the bowler's run-up, then offered reverse perspectives, a handedness-flipping camera, and a wider fielding view. The close behind-batter view made the batter and keeper semi-transparent when they hid the pitch marker. EA also describes picture-in-picture for the bowler, batter, and run availability, plus a field radar and optional timing gauge. That is a better design comparison than copying one zoom value: each camera needs a purpose, and pitch point, contact timing, and run information need to stay visible in context. [EA Cricket 07 product page](https://www.ea.com/fi-fi/games/cricket/cricket-2007), [EA producer diary #3](https://worthplaying.com/article/2006/11/13/news/37744-cricket-07-ps2pc-developer-diary-3/).

The available official engine documentation also matches what this experiment demonstrated: Godot 4.7 describes a Blender-friendly glTF 3D import path and provides an integrated renderer; MonoGame documents a lower-level shader/effect-based 3D pipeline with custom effects. That supports keeping the Godot trial for scene and animation experimentation while the current MonoGame game remains the lower-risk interactive host. [Godot 4.7 3D scene import formats](https://docs.godotengine.org/en/4.7/tutorials/assets_pipeline/importing_3d_scenes/available_formats.html), [Godot 4.7 rendering architecture](https://docs.godotengine.org/en/4.7/engine_details/architecture/internal_rendering_architecture.html), [MonoGame 3D rendering](https://docs.monogame.net/articles/getting_to_know/whatis/graphics/WhatIs_3DRendering.html), [MonoGame custom effects](https://docs.monogame.net/articles/getting_started/content_pipeline/custom_effects.html).
