# Hardware and renderer baseline

Captured 6 October 2026 on the local development machine.

## Named reference machine

- Lenovo IdeaPad S145-15IIL, machine code 81W8
- Intel Core i5-1035G1, 8 logical processors
- Intel UHD Graphics, driver 27.20.100.9664
- 19.8 GiB system memory
- Windows 11 Home Single Language, build 26200, x64
- Display mode 1920 x 1080; game render viewport 1440 x 900
- .NET SDK 9.0.317; MonoGame WindowsDX 3.8.5.1

Use this machine as the working minimum development target for the first short-match art set. Confirm the target against a second machine before release. The current project asks for approximately 60 rendered frames per second at 1440 x 900.

## Profile method

Run from a Release build:

```powershell
dotnet run --project src/SuperCricket.Game -c Release -- --profile-frames 300
```

The game renders its normal scene with VSync enabled, discards 60 warm-up frames, and reports 300 subsequent frame intervals and CPU update/draw-submission times. CPU draw submission does not include GPU execution time.

## First measured sample

| Measurement | Average | Median | P95 | Maximum |
| --- | ---: | ---: | ---: | ---: |
| Frame interval | 16.66 ms | 16.69 ms | 17.55 ms | 20.43 ms |
| CPU update | 0.10 ms | 0.06 ms | 0.14 ms | 6.32 ms |
| CPU draw submission | 2.22 ms | 2.17 ms | 2.65 ms | 6.85 ms |

This 300-frame sample averages about 60 FPS. Its 17.55 ms P95 and 20.43 ms maximum leave room for improvement against a strict 16.67 ms frame budget. The sample uses the current stadium and full field; it does not include an entire user-controlled innings, GPU timing, a second machine, or a long-session run. Phase 4 keeps those profiling checks open.
