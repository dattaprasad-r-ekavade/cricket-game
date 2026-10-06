using System;
using System.Globalization;
using System.IO;

string? capturePath = null;
string? captureCamera = null;
float? captureRunUpTimeSeconds = null;
float? captureDeliveryTimeSeconds = null;
string? captureFielderAction = null;
float? captureActionTimeSeconds = null;
var captureDebugOverlay = false;
var verifyGameplay = args.Length == 1 && args[0] == "--verify-gameplay";
if (args.Length > 0 && !verifyGameplay)
{
    if (args[0] != "--capture-frame" || args.Length < 2)
    {
        throw new ArgumentException(GetCaptureUsage());
    }

    capturePath = Path.GetFullPath(args[1]);
    for (var argumentIndex = 2; argumentIndex < args.Length;)
    {
        var option = args[argumentIndex];
        if (option == "--show-debug-overlay" && !captureDebugOverlay)
        {
            captureDebugOverlay = true;
            argumentIndex++;
            continue;
        }
        if (argumentIndex + 1 >= args.Length)
            throw new ArgumentException(GetCaptureUsage());

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
            case "--delivery-time" when captureDeliveryTimeSeconds is null &&
                float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDeliverySeconds) &&
                float.IsFinite(parsedDeliverySeconds) && parsedDeliverySeconds >= 0f:
                captureDeliveryTimeSeconds = parsedDeliverySeconds;
                break;
            case "--fielder-action" when captureFielderAction is null:
                captureFielderAction = value;
                break;
            case "--action-time" when captureActionTimeSeconds is null &&
                float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedActionSeconds) &&
                float.IsFinite(parsedActionSeconds) && parsedActionSeconds >= 0f:
                captureActionTimeSeconds = parsedActionSeconds;
                break;
            default:
                throw new ArgumentException(GetCaptureUsage());
        }
        argumentIndex += 2;
    }

    if ((captureFielderAction is null) != (captureActionTimeSeconds is null) ||
        captureFielderAction is not null && (captureRunUpTimeSeconds is not null || captureDeliveryTimeSeconds is not null))
        throw new ArgumentException(GetCaptureUsage());
}

using var game = new SuperCricket.Game.Game1(
    capturePath,
    captureCamera,
    captureRunUpTimeSeconds,
    captureDeliveryTimeSeconds,
    captureFielderAction,
    captureActionTimeSeconds,
    verifyGameplay,
    captureDebugOverlay);
game.Run();

static string GetCaptureUsage() =>
    "Usage: SuperCricket.Game --capture-frame <output.png> [--camera broadcast|behind-striker|bowler-end|square-leg] [--run-up-time <seconds> | --delivery-time <seconds> | --fielder-action fielder-catch|fielder-pickup|fielder-throw --action-time <seconds>] [--show-debug-overlay]";
