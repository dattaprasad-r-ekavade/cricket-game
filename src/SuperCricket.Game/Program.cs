using System;
using System.Globalization;
using System.IO;

string? capturePath = null;
string? captureCamera = null;
float? captureRunUpTimeSeconds = null;
float? captureDeliveryTimeSeconds = null;
float? captureBallFlightTimeSeconds = null;
string? captureFielderAction = null;
string? captureBatterFootworkAction = null;
float? captureActionTimeSeconds = null;
var captureDebugOverlay = false;
var captureBowlingTarget = false;
var captureFeedbackPreview = false;
var developerMode = args.Length == 1 && args[0] == "--debug";
var verifyGameplay = args.Length == 1 && args[0] == "--verify-gameplay";
var liveMatchReviewMode = args.Length > 0 && args[0] == "--verify-live-match";
string? liveMatchReviewPath = null;
if (liveMatchReviewMode)
{
    if (args.Length > 2) throw new ArgumentException(GetCaptureUsage());
    liveMatchReviewPath = Path.GetFullPath(args.Length == 2 ? args[1] : Path.Combine("artifacts", "live-match-review.csv"));
}
var profileFrameCount = 0;
var profileMode = args.Length > 0 && args[0] == "--profile-frames";
if (profileMode)
{
    if (args.Length != 2 || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out profileFrameCount) ||
        profileFrameCount is < 1 or > 36000)
        throw new ArgumentException(GetCaptureUsage());
}
if (args.Length > 0 && !verifyGameplay && !profileMode && !liveMatchReviewMode && !developerMode)
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
        if (option == "--bowling-target" && !captureBowlingTarget)
        {
            captureBowlingTarget = true;
            argumentIndex++;
            continue;
        }
        if (option == "--feedback-preview" && !captureFeedbackPreview)
        {
            captureFeedbackPreview = true;
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
            case "--ball-flight-time" when captureBallFlightTimeSeconds is null &&
                float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedBallFlightSeconds) &&
                float.IsFinite(parsedBallFlightSeconds) && parsedBallFlightSeconds >= 0f:
                captureBallFlightTimeSeconds = parsedBallFlightSeconds;
                break;
            case "--fielder-action" when captureFielderAction is null:
                captureFielderAction = value;
                break;
            case "--batter-footwork" when captureBatterFootworkAction is null:
                captureBatterFootworkAction = value;
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

    var hasActionCapture = captureFielderAction is not null || captureBatterFootworkAction is not null;
    var captureTimeCount = (captureRunUpTimeSeconds is null ? 0 : 1) +
        (captureDeliveryTimeSeconds is null ? 0 : 1) + (captureBallFlightTimeSeconds is null ? 0 : 1);
    if (hasActionCapture != (captureActionTimeSeconds is not null) ||
        captureTimeCount > 1 ||
        captureBowlingTarget && (hasActionCapture || captureTimeCount > 0) ||
        captureFeedbackPreview && (hasActionCapture || captureTimeCount > 0) ||
        captureFielderAction is not null && captureBatterFootworkAction is not null ||
        hasActionCapture && captureTimeCount > 0 ||
        captureBatterFootworkAction is not null && captureBatterFootworkAction is not
            ("batting-step-offside" or "batting-step-legside"))
        throw new ArgumentException(GetCaptureUsage());
}

using var game = new SuperCricket.Game.Game1(
    capturePath,
    captureCamera,
    captureRunUpTimeSeconds,
    captureDeliveryTimeSeconds,
    captureFielderAction,
    captureFielderAction is null ? null : captureActionTimeSeconds,
    verifyGameplay,
    captureDebugOverlay,
    profileFrameCount,
    liveMatchReviewPath,
    captureBatterFootworkAction,
    captureBatterFootworkAction is null ? null : captureActionTimeSeconds,
    captureBallFlightTimeSeconds,
    developerMode || verifyGameplay || liveMatchReviewMode,
    captureBowlingTarget,
    captureFeedbackPreview);
game.Run();

static string GetCaptureUsage() =>
    "Usage: SuperCricket.Game --debug | --capture-frame <output.png> [--camera broadcast|behind-striker|bowler-end|square-leg|ball-follow] [--run-up-time <seconds> | --delivery-time <seconds> | --ball-flight-time <seconds> | --fielder-action fielder-catch|fielder-pickup|fielder-throw --action-time <seconds> | --batter-footwork batting-step-offside|batting-step-legside --action-time <seconds> | --bowling-target] [--feedback-preview] [--show-debug-overlay] | --profile-frames <1..36000> | --verify-gameplay | --verify-live-match [results.csv]";
