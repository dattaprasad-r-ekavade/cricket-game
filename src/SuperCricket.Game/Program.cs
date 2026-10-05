using System;
using System.Globalization;
using System.IO;

string? capturePath = null;
string? captureCamera = null;
float? captureRunUpTimeSeconds = null;
if (args.Length > 0)
{
    if (args[0] != "--capture-frame" || args.Length < 2 || args.Length % 2 != 0)
    {
        throw new ArgumentException(GetCaptureUsage());
    }

    capturePath = Path.GetFullPath(args[1]);
    for (var argumentIndex = 2; argumentIndex < args.Length; argumentIndex += 2)
    {
        var option = args[argumentIndex];
        var value = args[argumentIndex + 1];
        switch (option)
        {
            case "--camera" when captureCamera is null:
                captureCamera = value;
                break;
            case "--run-up-time" when captureRunUpTimeSeconds is null &&
                float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSeconds) &&
                float.IsFinite(parsedSeconds) && parsedSeconds >= 0f:
                captureRunUpTimeSeconds = parsedSeconds;
                break;
            default:
                throw new ArgumentException(GetCaptureUsage());
        }
    }
}

using var game = new SuperCricket.Game.Game1(capturePath, captureCamera, captureRunUpTimeSeconds);
game.Run();

static string GetCaptureUsage() =>
    "Usage: SuperCricket.Game --capture-frame <output.png> [--camera broadcast|behind-striker|bowler-end|square-leg] [--run-up-time <seconds>]";
