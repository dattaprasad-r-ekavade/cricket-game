using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SuperCricket.Content;
using SuperCricket.Simulation;
using SuperCricket.Simulation.Events;

namespace SuperCricket.Game;

internal enum MatchHudPhase
{
    Batting,
    BallBatted,
    Running,
    DeliveryComplete,
    Bowling,
    BowlingDeliveryComplete,
    InningsComplete,
    MatchComplete
}

internal readonly record struct MatchHudState(
    bool IsGamePad,
    MatchHudPhase Phase,
    string NextDeliveryName,
    BattingInputScheme InputScheme = BattingInputScheme.Directional,
    BattingFootSelection FootSelection = BattingFootSelection.Automatic,
    bool IsHumanBowling = false,
    IReadOnlyList<HudActionHint>? BowlingOptions = null,
    int BowlingPage = 0,
    bool BowlingStarted = false,
    float BowlingMeterProgress = 0f,
    bool BowlingNoBall = false,
    bool FieldPickerOpen = false,
    string? FieldPickerOption = null);

internal readonly record struct MatchHudConditions(
    bool IsMatchComplete,
    bool IsInningsComplete,
    bool IsBowling,
    bool IsDeliveryComplete,
    bool IsRunning,
    bool HasBattedBall);

internal readonly record struct CompactMatchOverlayState(
    CpuDifficulty Difficulty,
    string ScoreStatusText,
    string ShotOutcome,
    bool IsMatchComplete,
    bool IsInningsComplete,
    bool IsCpuBattingControlled,
    string StrikerName,
    string NonStrikerName,
    string PrimaryControlHint);

internal readonly record struct MatchScoreStatusState(
    bool IsMatchComplete,
    string ResultText,
    int InningsNumber,
    string BattingTeamName,
    int Runs,
    int Wickets,
    string OversText,
    int OversPerInnings,
    int? Target,
    string CurrentBowlerName);

internal readonly record struct DeliveryFeedbackState(
    DeliveryResult Result,
    bool IsHumanBowling,
    float DeliverySpeedKilometersPerHour,
    Vector3? FirstBouncePosition,
    float BatterWicketLineZ,
    string? ShotName,
    float? ContactQuality,
    string? TimingText,
    Vector3? ActiveBowlingTargetPosition,
    string? ShotControlLabel = null);

internal readonly record struct MatchPauseMenuState(
    string ScoreLine,
    CpuDifficulty Difficulty,
    bool DeveloperMode,
    bool HighContrast,
    bool LargeText,
    float EffectsVolume,
    string? SettingsStatusMessage,
    bool AudioUnavailable,
    BattingInputScheme InputScheme = BattingInputScheme.Directional,
    BattingFootSelection FootSelection = BattingFootSelection.Automatic);

internal readonly record struct PauseMenuLayout(
    Rectangle OverlayBounds,
    Rectangle PanelBounds,
    float TextScale,
    int LineSpacing);

internal readonly record struct ContactFeedbackPresentationState(
    bool IsMiss,
    float? Quality,
    bool HighContrast);

internal readonly record struct LiveFeedbackBannerState(
    bool IsHumanBowling,
    Vector3? ActiveBowlingTargetPosition,
    Vector3? FirstBouncePosition,
    float RemainingSeconds,
    float? ContactQuality,
    bool ContactIsMiss,
    string? ShotName,
    string? TimingText,
    BattingTimingCue? TimingCue,
    bool IsGamePad,
    bool HighContrast,
    bool HasPredictedBounce,
    CpuDifficulty Difficulty,
    float BatterWicketLineZ,
    string? ShotControlLabel = null,
    BattingInputScheme InputScheme = BattingInputScheme.Directional,
    TimingGaugePresentation? TimingGauge = null,
    string? UmpireSignal = null,
    float UmpireSignalRemainingSeconds = 0f);

internal readonly record struct HudActionHint(string Input, string Action);

internal readonly record struct TimingGaugeBand(TimingZone Zone, float Start, float End);

internal sealed record TimingGaugePresentation(
    IReadOnlyList<TimingGaugeBand> Bands,
    float Progress,
    TimingZone CurrentZone);

internal enum GamePadFeedbackCue
{
    None,
    IdealContact,
    EdgeContact,
    BodyHit
}

internal readonly record struct GamePadFeedbackProfile(
    float LeftMotor,
    float RightMotor,
    float DurationSeconds,
    float PulseSeconds);

internal readonly record struct LiveFeedbackBannerContent(
    string Title,
    string Detail,
    Color Accent,
    float RemainingSeconds);

/// <summary>Builds match HUD text and layout values without depending on a graphics device.</summary>
internal static class MatchHudPresenter
{
    public static TimingGaugePresentation BuildTimingGauge(TimingWindow window, float ballDistanceMeters)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!float.IsFinite(ballDistanceMeters))
            throw new ArgumentOutOfRangeException(nameof(ballDistanceMeters));

        var rangeStart = window.CentreMeters + window.WindowMeters;
        var rangeEnd = window.CentreMeters - window.WindowMeters;
        var span = rangeStart - rangeEnd;
        var zones = new[]
        {
            TimingZone.TooEarly,
            TimingZone.Start,
            TimingZone.Early,
            TimingZone.Ideal,
            TimingZone.Late,
            TimingZone.End,
            TimingZone.TooLate
        };
        var bands = new TimingGaugeBand[zones.Length];
        for (var index = 0; index < zones.Length; index++)
        {
            var band = window.GetBand(zones[index]);
            var clippedMinimum = Math.Clamp(band.MinimumDistanceMeters, rangeEnd, rangeStart);
            var clippedMaximum = Math.Clamp(band.MaximumDistanceMeters, rangeEnd, rangeStart);
            bands[index] = new TimingGaugeBand(
                zones[index],
                Math.Clamp((rangeStart - clippedMaximum) / span, 0f, 1f),
                Math.Clamp((rangeStart - clippedMinimum) / span, 0f, 1f));
        }

        return new TimingGaugePresentation(
            bands,
            Math.Clamp((rangeStart - ballDistanceMeters) / span, 0f, 1f),
            TimingWindowModel.Classify(window, ballDistanceMeters));
    }

    public static Rectangle CalculateTimingZoneInsetBounds(
        int viewportWidth,
        int viewportHeight,
        Point batterAnchor,
        int width,
        int height)
    {
        if (viewportWidth < 1 || viewportHeight < 1 || width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Viewport and inset dimensions must be positive.");

        var insetWidth = Math.Min(width, Math.Max(1, viewportWidth - 16));
        var insetHeight = Math.Min(height, Math.Max(1, viewportHeight - 16));
        var x = Math.Clamp(batterAnchor.X - insetWidth / 2, 0, viewportWidth - insetWidth);
        var y = Math.Clamp(batterAnchor.Y - insetHeight - 18, 0, viewportHeight - insetHeight);
        return new Rectangle(x, y, insetWidth, insetHeight);
    }

    public static Color GetTimingGaugeBandColor(TimingZone zone, bool highContrast) => highContrast
        ? zone == TimingZone.Ideal ? Color.Yellow : zone is TimingZone.Early or TimingZone.Late ? Color.White : new Color(132, 132, 132)
        : zone switch
        {
            TimingZone.TooEarly or TimingZone.TooLate => new Color(164, 62, 57),
            TimingZone.Start or TimingZone.End => new Color(227, 124, 45),
            TimingZone.Early or TimingZone.Late => new Color(236, 196, 54),
            TimingZone.Ideal => new Color(77, 190, 101),
            _ => throw new ArgumentOutOfRangeException(nameof(zone))
        };

    public static string GetTimingZoneLabel(TimingZone zone, bool missed = false)
    {
        if (!Enum.IsDefined(zone))
            throw new ArgumentOutOfRangeException(nameof(zone));
        if (missed || zone is TimingZone.TooEarly or TimingZone.TooLate)
            return "BEATEN";
        return zone switch
        {
            TimingZone.Start => "VERY EARLY",
            TimingZone.Early => "EARLY",
            TimingZone.Ideal => "PERFECT",
            TimingZone.Late => "LATE",
            TimingZone.End => "VERY LATE",
            _ => "BEATEN"
        };
    }

    public static CricketAudioCue GetTimingAudioCue(TimingZone zone, bool missed = false)
    {
        if (!Enum.IsDefined(zone))
            throw new ArgumentOutOfRangeException(nameof(zone));
        if (missed || zone is TimingZone.TooEarly or TimingZone.TooLate)
            return CricketAudioCue.BatBeaten;
        return zone == TimingZone.Ideal ? CricketAudioCue.BatContact : CricketAudioCue.BatContactEdge;
    }

    public static CricketAudioCue? GetTimingAudioCue(MatchEvent matchEvent) => matchEvent switch
    {
        BatContact { Edged: true } => CricketAudioCue.BatContactEdge,
        BatContact => CricketAudioCue.BatContact,
        BallBeatBat { TimingZone: { } zoneName } when Enum.TryParse<TimingZone>(zoneName, out var zone) =>
            GetTimingAudioCue(zone, missed: true),
        BodyHit => CricketAudioCue.BodyHit,
        _ => null
    };

    public static GamePadFeedbackCue GetGamePadFeedbackCue(MatchEvent matchEvent) => matchEvent switch
    {
        BodyHit => GamePadFeedbackCue.BodyHit,
        BatContact { Edged: true } => GamePadFeedbackCue.EdgeContact,
        BatContact contact when Enum.TryParse<TimingZone>(contact.TimingZone, out var zone) && zone == TimingZone.Ideal =>
            GamePadFeedbackCue.IdealContact,
        BatContact => GamePadFeedbackCue.None,
        BallBeatBat { TimingZone: { } zoneName } when Enum.TryParse<TimingZone>(zoneName, out var zone) &&
            zone is not (TimingZone.TooEarly or TimingZone.TooLate) => GamePadFeedbackCue.EdgeContact,
        _ => GamePadFeedbackCue.None
    };

    public static GamePadFeedbackProfile? GetGamePadFeedbackProfile(GamePadFeedbackCue cue) => cue switch
    {
        GamePadFeedbackCue.IdealContact => new GamePadFeedbackProfile(0.95f, 0.95f, 0.15f, 0f),
        GamePadFeedbackCue.EdgeContact => new GamePadFeedbackProfile(0.72f, 0.72f, 0.38f, 0.055f),
        GamePadFeedbackCue.BodyHit => new GamePadFeedbackProfile(0.62f, 0.62f, 0.68f, 0f),
        GamePadFeedbackCue.None => null,
        _ => throw new ArgumentOutOfRangeException(nameof(cue))
    };

    public static string? GetUmpireSignalLabel(MatchEvent matchEvent) => matchEvent switch
    {
        Boundary { Runs: 4 } => "FOUR",
        Boundary { Runs: 6 } => "SIX",
        DeliveryExtraSignalled { Extra: "Wide" } => "WIDE",
        DeliveryExtraSignalled { Extra: "NoBall" } => "NO BALL",
        UmpireDecision { IsOut: true } or WicketBroken or Dismissal => "OUT",
        _ => null
    };

    public static string[] BuildCompactOverlayLines(CompactMatchOverlayState state)
    {
        var eventText = state.ShotOutcome.Length > 72 ? state.ShotOutcome[..69] + "..." : state.ShotOutcome;
        var batterText = state.IsMatchComplete
            ? "Match complete"
            : state.IsInningsComplete
                ? "Innings complete; press N to start the chase"
                : state.IsCpuBattingControlled
                    ? $"CPU batting: {state.StrikerName}    Non-striker: {state.NonStrikerName}"
                    : $"On strike: {state.StrikerName}    Non-striker: {state.NonStrikerName}";

        return
        [
            $"SUPER CRICKET / SHORT MATCH  |  CPU {state.Difficulty}  |  TRAIL cool = slower / warm = faster",
            state.ScoreStatusText,
            batterText,
            state.IsMatchComplete ? "Match finished" : eventText,
            state.PrimaryControlHint
        ];
    }

    public static string FormatBattingTimingText(BattingTimingAssessment assessment)
    {
        var offsetMilliseconds = (int)MathF.Round(MathF.Abs(assessment.OffsetFromIdealSeconds) * 1000f);
        return assessment.Band switch
        {
            BattingTimingBand.Perfect => "PERFECT",
            BattingTimingBand.Early => $"EARLY {offsetMilliseconds} ms",
            _ => $"LATE {offsetMilliseconds} ms"
        };
    }

    public static LiveFeedbackBannerContent? BuildLiveFeedbackBanner(LiveFeedbackBannerState state)
    {
        if (!string.IsNullOrWhiteSpace(state.UmpireSignal) && state.UmpireSignalRemainingSeconds > 0f)
        {
            var signalAccent = state.HighContrast ? Color.Yellow : state.UmpireSignal switch
            {
                "OUT" => new Color(255, 103, 91),
                "WIDE" or "NO BALL" => new Color(255, 193, 67),
                _ => new Color(135, 255, 159)
            };
            return new LiveFeedbackBannerContent(
                $"UMPIRE | {state.UmpireSignal}",
                state.UmpireSignal switch
                {
                    "FOUR" or "SIX" => "Boundary awarded",
                    "WIDE" => "Wide called",
                    "NO BALL" => "No-ball called",
                    "OUT" => "Wicket awarded",
                    _ => "Umpire decision"
                },
                signalAccent,
                state.UmpireSignalRemainingSeconds);
        }

        if (state.IsHumanBowling && state.ActiveBowlingTargetPosition is { } target &&
            state.FirstBouncePosition is { } landing && state.RemainingSeconds > 0f)
        {
            var summary = GetBowlingFeedbackSummary(target, landing, state.BatterWicketLineZ);
            var aimDistance = Vector2.Distance(new Vector2(target.X, target.Z), new Vector2(landing.X, landing.Z));
            return new LiveFeedbackBannerContent(
                $"BOWLING | {summary.Title}",
                $"{summary.Detail} | {aimDistance:0.0} m from your aim",
                state.HighContrast ? Color.Yellow : new Color(74, 224, 255),
                state.RemainingSeconds);
        }

        if (!state.IsHumanBowling && state.RemainingSeconds > 0f &&
            (state.ContactQuality is not null || state.ContactIsMiss) && state.ShotName is not null)
        {
            var timingDetail = state.TimingText switch
            {
                "PERFECT" => "PERFECT TIMING",
                { } assessment => $"TIMING {assessment}",
                _ => string.Empty
            };
            var qualityLabel = state.ContactQuality is { } quality
                ? $"{quality:P0} contact"
                : "no contact";
            var controlLabel = string.IsNullOrWhiteSpace(state.ShotControlLabel)
                ? state.ShotName
                : $"{state.ShotName} ({state.ShotControlLabel})";
            var detail = state.ContactIsMiss
                ? "Your swing missed the ball"
                : string.IsNullOrWhiteSpace(timingDetail)
                    ? $"{controlLabel} | {qualityLabel}"
                    : $"{controlLabel} | {timingDetail} | {qualityLabel}";
            var accent = GetContactFeedbackColor(new ContactFeedbackPresentationState(
                state.ContactIsMiss,
                state.ContactQuality,
                state.HighContrast));
            return new LiveFeedbackBannerContent(
                $"BATTING | {GetContactFeedbackLabel(new ContactFeedbackPresentationState(
                    state.ContactIsMiss,
                    state.ContactQuality,
                    state.HighContrast))}",
                detail,
                accent,
                state.RemainingSeconds);
        }

        if (state.TimingCue is { } cue)
        {
            var title = cue.State switch
            {
                BattingTimingCueState.Waiting => "WATCH THE MARKER",
                BattingTimingCueState.SwingNow => "SWING NOW",
                _ => "LATE SHOT POSSIBLE"
            };
            var shotButtons = state.IsGamePad
                ? "A / X"
                : state.InputScheme == BattingInputScheme.Directional ? "S / W" : "S, S+D or W+D";
            var loftModifier = state.IsGamePad ? string.Empty : "; Shift adds loft";
            var detail = state.IsGamePad
                ? cue.State switch
                {
                    BattingTimingCueState.Waiting => $"Press {shotButtons} as the marker enters green",
                    BattingTimingCueState.SwingNow => $"Press {shotButtons} now for on-time contact",
                    _ => $"Window passed; press {shotButtons} for late contact"
                }
                : cue.State switch
                {
                    BattingTimingCueState.Waiting => $"Choose a stroke ({shotButtons}{loftModifier}) as the marker enters green",
                    BattingTimingCueState.SwingNow => $"Choose a stroke ({shotButtons}{loftModifier}) now",
                    _ => $"Window passed; choose a stroke ({shotButtons}{loftModifier}) for late contact"
                };
            var accent = cue.State switch
            {
                BattingTimingCueState.Waiting => state.HighContrast ? Color.Yellow : new Color(255, 220, 74),
                BattingTimingCueState.SwingNow => state.HighContrast ? Color.Yellow : new Color(135, 255, 159),
                _ => state.HighContrast ? Color.White : new Color(255, 164, 77)
            };
            if (state.HasPredictedBounce)
                detail += state.Difficulty is CpuDifficulty.Pro or CpuDifficulty.Legend
                    ? " | ring = projected bounce"
                    : " | ring = bounce point; bat outline = contact zone";
            return new LiveFeedbackBannerContent(title, detail, accent, 1f);
        }

        if (!state.IsHumanBowling && state.FirstBouncePosition is { } bounce && state.RemainingSeconds > 0f)
        {
            return new LiveFeedbackBannerContent(
                $"YOUR DELIVERY | {GetPitchLengthLabel(bounce, state.BatterWicketLineZ).ToUpperInvariant()}",
                $"{GetPitchLineLabel(bounce)} | {MathF.Abs(bounce.Z - state.BatterWicketLineZ):0.0} m from you",
                state.HighContrast ? Color.Yellow : new Color(255, 220, 74),
                state.RemainingSeconds);
        }

        return null;
    }

    public static Color GetBallTrailColor(float speedMetersPerSecond)
    {
        if (!float.IsFinite(speedMetersPerSecond))
            speedMetersPerSecond = 0f;

        var speedFraction = Math.Clamp((speedMetersPerSecond - 8f) / 28f, 0f, 1f);
        var cool = new Color(68, 220, 255);
        var warm = new Color(255, 226, 70);
        var hot = new Color(255, 86, 58);
        return speedFraction < 0.5f
            ? Color.Lerp(cool, warm, speedFraction * 2f)
            : Color.Lerp(warm, hot, (speedFraction - 0.5f) * 2f);
    }

    public static int GetBattingContactZoneAlpha(CpuDifficulty difficulty)
    {
        var profile = CpuDifficultyModel.GetProfile(difficulty, DifficultySide.Human);
        return Math.Clamp((int)MathF.Round((profile.Timing.WindowMeters - 6.4f) * 150f), 0, 220);
    }

    public static string GetContactFeedbackLabel(ContactFeedbackPresentationState state) => state.IsMiss
        ? "NO CONTACT"
        : state.Quality switch
        {
            >= 0.88f => "MIDDLE",
            >= 0.75f => "CLEAN CONTACT",
            >= 0.60f => "EDGE CONTACT",
            > 0f => "THIN CONTACT",
            _ => string.Empty
        };

    public static Color GetContactFeedbackColor(ContactFeedbackPresentationState state)
    {
        if (state.IsMiss)
            return state.HighContrast ? Color.Red : new Color(255, 91, 77);
        if (state.Quality is not { } quality)
            return Color.White;
        if (state.HighContrast)
            return quality >= 0.75f ? Color.Yellow : Color.Red;
        return quality switch
        {
            >= 0.88f => new Color(135, 255, 159),
            >= 0.75f => new Color(89, 232, 255),
            >= 0.60f => new Color(255, 220, 85),
            _ => new Color(255, 143, 75)
        };
    }

    public static PauseMenuLayout CalculatePauseMenuLayout(
        int viewportWidth,
        int viewportHeight,
        bool largeText,
        int lineCount)
    {
        if (viewportWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        if (viewportHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportHeight));
        if (lineCount < 0)
            throw new ArgumentOutOfRangeException(nameof(lineCount));

        var textScale = largeText ? 1.3f : 1.1f;
        var lineSpacing = (int)MathF.Round(30 * textScale);
        var panelWidth = Math.Min(900, Math.Max(1, viewportWidth - 40));
        var maximumPanelHeight = Math.Max(1, viewportHeight - 40);

        if (lineCount > 0 && 44L + (long)lineCount * lineSpacing > maximumPanelHeight)
        {
            var availableLineSpacing = Math.Max(1, (maximumPanelHeight - 44) / lineCount);
            lineSpacing = Math.Min(lineSpacing, availableLineSpacing);
            textScale = Math.Min(textScale, lineSpacing / 30f);
        }

        var panelHeight = (int)Math.Min(maximumPanelHeight, 44L + (long)lineCount * lineSpacing);
        var panelX = (viewportWidth - panelWidth) / 2;
        var panelY = (viewportHeight - panelHeight) / 2;
        return new PauseMenuLayout(
            new Rectangle(0, 0, viewportWidth, viewportHeight),
            new Rectangle(panelX, panelY, panelWidth, panelHeight),
            textScale,
            lineSpacing);
    }

    public static IReadOnlyList<string> BuildPauseMenuLines(MatchPauseMenuState state)
    {
        var battingControls = state.InputScheme == BattingInputScheme.Directional
            ? "Batting: arrows choose direction | S attack | W defend | Shift lofts | A leave | D advances before release"
            : "Batting: arrows aim | S block | S+D front drive | W+D back drive | Shift adds loft | chords record for 220 ms";
        var keyboardFoot = state.FootSelection == BattingFootSelection.Automatic
            ? "automatic"
            : "manual (W back foot, S front foot)";
        var lines = new List<string>
        {
            "SUPER CRICKET  /  PAUSED",
            state.ScoreLine,
            $"CPU difficulty: {state.Difficulty}",
            battingControls,
            $"Keyboard: I toggles Directional / Classic chords | F toggles {keyboardFoot}",
            "GamePad batting: left stick direction | A attack | X defend | RB loft | B leave/run | LB advance",
            "Running: D/B starts or repeats; tap A to run back (GamePad: hold B to turn back)",
            "Bowling: arrows/D-pad move pitch target | C/LB changes delivery | N/RB bowls",
            "V/L3: camera | PgDn zoom in / PgUp out | R/A replay | D/LB difficulty | O/RB overs when match ends",
            $"Paused: P/Start resumes | Esc/Back quits | H/Y contrast {(state.HighContrast ? "ON" : "OFF")}",
            $"T/Pad X: larger text {(state.LargeText ? "ON" : "OFF")} | -/LB volume down | +/RB volume up {state.EffectsVolume:P0}",
            state.SettingsStatusMessage ?? (state.AudioUnavailable
                ? "Audio output is unavailable; the match remains playable."
                : "Match, audio, and accessibility settings save on this device.")
        };
        if (state.DeveloperMode)
            lines.Insert(3, "Debug: A/S/D shots | J/L aim | Q/E steps | 1-4 presets | arrows orbit | PgUp/PgDn height");
        return lines;
    }

    public static IReadOnlyList<string> WrapTextLines(
        IReadOnlyList<string> lines,
        float availableWidth,
        float scale,
        Func<string, float> measureText)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(measureText);

        var wrapped = new List<string>(lines.Count);
        foreach (var line in lines)
        {
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = current.Length == 0 ? word : $"{current} {word}";
                if (current.Length > 0 && measureText(candidate) * scale > availableWidth)
                {
                    wrapped.Add(current);
                    current = $"  {word}";
                }
                else
                {
                    current = candidate;
                }
            }
            if (current.Length > 0)
                wrapped.Add(current);
        }
        return wrapped;
    }

    public static IReadOnlyList<string> BuildDeliveryFeedbackLines(DeliveryFeedbackState state)
    {
        var result = state.Result;
        var outcome = result.Dismissal != DismissalKind.None
            ? $"WICKET - {result.Dismissal.ToString().ToUpperInvariant()}"
            : result.Extra switch
            {
                DeliveryExtra.Wide => $"WIDE - {result.ExtraRuns} extra run(s)",
                DeliveryExtra.NoBall => $"NO BALL - {result.ExtraRuns} extra run(s)",
                _ => result.BatterRuns switch
                {
                    0 => "DOT BALL",
                    1 => "1 RUN",
                    4 => "FOUR",
                    6 => "SIX",
                    _ => $"{result.BatterRuns} RUNS"
                }
            };

        var bounce = state.FirstBouncePosition;
        var pitchDetail = bounce is { } position
            ? $"{GetPitchLengthLabel(position, state.BatterWicketLineZ)} {GetPitchLineLabel(position)}"
            : "full toss";
        var shotControl = string.IsNullOrWhiteSpace(state.ShotControlLabel)
            ? string.Empty
            : $" ({state.ShotControlLabel})";
        var battingDescription = state.ShotName is { } shotName
            ? state.ContactQuality is { } quality
                ? $"{shotName}{shotControl}: {GetContactQualityLabel(quality)} ({quality:P0})"
                : $"{shotName}{shotControl}: no contact - ball beat the bat"
            : "No shot played";
        if (state.TimingText is { } timingText)
            battingDescription += $" | timing {timingText}";

        var lines = new List<string>
        {
            $"{(state.IsHumanBowling ? "YOUR BOWLING RESULT" : "YOUR BATTING RESULT")}  |  {outcome}",
            $"BALL  |  {state.DeliverySpeedKilometersPerHour:0} km/h  |  {pitchDetail}",
            bounce is { } pitched
                ? $"PITCH  |  {MathF.Abs(pitched.Z - state.BatterWicketLineZ):0.0} m from striker"
                : "PITCH  |  full toss - no bounce"
        };

        if (state.IsHumanBowling && state.ActiveBowlingTargetPosition is { } target)
        {
            if (bounce is { } landed)
            {
                var aimDistance = Vector2.Distance(new Vector2(landed.X, landed.Z), new Vector2(target.X, target.Z));
                var summary = GetBowlingFeedbackSummary(target, landed, state.BatterWicketLineZ);
                lines.Add($"YOUR BOWL  |  {summary.Title} ({aimDistance:0.0} m from aim)");
            }
            else
            {
                lines.Add($"YOUR BOWL  |  aimed {GetPitchLengthLabel(target, state.BatterWicketLineZ)} / {GetPitchLineLabel(target)}  |  full toss");
            }
            lines.Add($"BATTER  |  {battingDescription}");
        }
        else
        {
            lines.Add($"YOUR SHOT  |  {battingDescription}");
        }

        return lines;
    }

    public static (string Title, string Detail) GetBowlingFeedbackSummary(
        Vector3 target,
        Vector3 landing,
        float batterWicketLineZ)
    {
        var aimDistance = Vector2.Distance(
            new Vector2(target.X, target.Z),
            new Vector2(landing.X, landing.Z));
        var title = aimDistance <= 0.65f ? "ON TARGET" : $"{aimDistance:0.0} M FROM AIM";
        var detail = $"{GetPitchLengthLabel(landing, batterWicketLineZ)} | {GetPitchLineLabel(landing)}";
        return (title, detail);
    }

    public static string GetPitchLengthLabel(Vector3 position, float batterWicketLineZ)
    {
        var distanceFromStriker = MathF.Abs(position.Z - batterWicketLineZ);
        return distanceFromStriker switch
        {
            <= 1.6f => "yorker",
            <= 3.0f => "full",
            <= 6.3f => "good length",
            <= 8.3f => "back of a length",
            _ => "short"
        };
    }

    public static string GetPitchLineLabel(Vector3 position) => MathF.Abs(position.X) switch
    {
        <= 0.35f => "on the stumps",
        <= 1.525f => position.X < 0f ? "left of the stumps" : "right of the stumps",
        _ => position.X < 0f ? "wide left" : "wide right"
    };

    private static string GetContactQualityLabel(float quality) => quality switch
    {
        >= 0.88f => "middled",
        >= 0.75f => "clean contact",
        >= 0.60f => "edged",
        _ => "thin contact"
    };

    public static string GetScoreStatus(MatchScoreStatusState state)
    {
        if (state.IsMatchComplete)
            return state.ResultText;

        var targetText = state.Target is { } target ? $"    target {target}" : string.Empty;
        return $"Innings {state.InningsNumber}/2    {state.BattingTeamName} {state.Runs}/{state.Wickets}    " +
            $"{state.OversText}/{state.OversPerInnings} overs{targetText}    bowler {state.CurrentBowlerName}";
    }

    public static MatchHudPhase ResolvePhase(MatchHudConditions conditions)
    {
        if (conditions.IsMatchComplete)
            return MatchHudPhase.MatchComplete;
        if (conditions.IsInningsComplete)
            return MatchHudPhase.InningsComplete;
        if (conditions.IsBowling)
            return conditions.IsDeliveryComplete
                ? MatchHudPhase.BowlingDeliveryComplete
                : MatchHudPhase.Bowling;
        if (conditions.IsDeliveryComplete)
            return MatchHudPhase.DeliveryComplete;
        if (conditions.IsRunning)
            return MatchHudPhase.Running;
        return conditions.HasBattedBall
            ? MatchHudPhase.BallBatted
            : MatchHudPhase.Batting;
    }

    public static string GetPrimaryControlHint(MatchHudState state)
    {
        var pause = state.IsGamePad ? "Start: pause" : "P: pause";
        var camera = state.IsGamePad ? "L3: camera" : "V: camera    PgDn: zoom in / PgUp: out";
        string WithCamera(string hint) => $"{hint}    {camera}";

        if (state.FieldPickerOpen && state.IsHumanBowling)
            return WithCamera(state.IsGamePad
                ? $"D-pad: preview {state.FieldPickerOption}    A: choose    B: close    {pause}"
                : $"Up/Down: preview {state.FieldPickerOption}    S: choose    W: close    {pause}");

        return state.Phase switch
        {
            MatchHudPhase.MatchComplete => WithCamera(state.IsGamePad
                ? $"A: replay    LB: difficulty    RB: overs    {pause}"
                : $"R: replay    D: difficulty    O: overs    {pause}"),
            MatchHudPhase.InningsComplete => WithCamera(state.IsGamePad
                ? $"RB: start the chase    {pause}"
                : $"N: start the chase    {pause}"),
            MatchHudPhase.Bowling => WithCamera(GetBowlingHint(state, pause, includeNextBall: false)),
            MatchHudPhase.BowlingDeliveryComplete => WithCamera(GetBowlingHint(state, pause, includeNextBall: true)),
            MatchHudPhase.DeliveryComplete => WithCamera(state.IsGamePad
                ? $"RB: next ball    {pause}"
                : $"N: next ball    {pause}"),
            MatchHudPhase.Running => WithCamera(state.IsGamePad
                ? $"B: request another run    hold B: turn back    {pause}"
                : $"D: request another run    A: run back    {pause}"),
            MatchHudPhase.BallBatted => WithCamera(state.IsGamePad
                ? $"B: run    {pause}"
                : $"D: run    {pause}"),
            MatchHudPhase.Batting => WithCamera(state.IsGamePad
                ? $"Left stick: direction    A: attack    X: defend    RB: loft    {pause}"
                : state.InputScheme == BattingInputScheme.Directional
                    ? $"Arrows: direction    S: attack    W: defend    Shift: loft    {pause}"
                    : $"Arrows: aim    S / S+D / W+D: classic chords    Shift: loft    {pause}"),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.Phase, "Unsupported match HUD phase.")
        };
    }

    public static IReadOnlyList<HudActionHint> BuildActionHints(MatchHudState state) => state.Phase switch
    {
        MatchHudPhase.Batting => state.IsGamePad
            ? [
                new("LEFT STICK", "Choose one of 8 directions"),
                new("A / X", "Attack / defend"),
                new("RB / B", "Loft / leave"),
                new("LB / B", "Advance / run after contact")
            ]
            : state.InputScheme == BattingInputScheme.Directional
                ? [
                    new("ARROWS", "Choose one of 8 directions"),
                    new("S / W", $"Attack / defend ({(state.FootSelection == BattingFootSelection.Automatic ? "auto" : "manual")} foot)"),
                    new("SHIFT", "Add loft to attack or defend"),
                    new("A / D", "Leave / advance before release; D runs after contact")
                ]
                : [
                    new("ARROWS", "Aim left / right and downfield / behind"),
                    new("S / S+D / W+D", "Block / front drive / back drive"),
                    new("SHIFT", "Add loft to the chord stroke"),
                    new("D / A", "Run after contact / turn back")
                ],
        MatchHudPhase.BallBatted or MatchHudPhase.Running => state.IsGamePad
            ? [new("B", "Run again  |  hold to turn back")]
            : [new("D  /  A", "D runs again  |  A turns back")],
        MatchHudPhase.Bowling when state.IsHumanBowling => BuildHumanBowlingHints(state),
        MatchHudPhase.BowlingDeliveryComplete when state.IsHumanBowling => BuildHumanBowlingHints(state),
        MatchHudPhase.Bowling => state.IsGamePad
            ? [
                new("D-PAD / STICK", "Aim the next bounce"),
                new("LB", $"Choose next delivery: {state.NextDeliveryName}")
            ]
            : [
                new("ARROWS", "Aim the next bounce"),
                new("C", $"Choose next delivery: {state.NextDeliveryName}")
            ],
        MatchHudPhase.BowlingDeliveryComplete => state.IsGamePad
            ? [
                new("D-PAD / STICK", "Aim the next bounce"),
                new("LB", $"Delivery: {state.NextDeliveryName}"),
                new("RB", "Bowl next")
            ]
            : [
                new("ARROWS", "Aim the next bounce"),
                new("C", $"Delivery: {state.NextDeliveryName}"),
                new("N", "Bowl next")
            ],
        MatchHudPhase.DeliveryComplete => state.IsGamePad
            ? [new("RB", "Next delivery")]
            : [new("N", "Next delivery")],
        MatchHudPhase.InningsComplete => state.IsGamePad
            ? [new("RB", "Start the chase")]
            : [new("N", "Start the chase")],
        MatchHudPhase.MatchComplete => state.IsGamePad
            ? [new("A", "Restart match"), new("LB / RB", "Change difficulty / overs")]
            : [new("R", "Restart match"), new("D / O", "Change difficulty / overs")],
        _ => throw new ArgumentOutOfRangeException(nameof(state), state.Phase, "Unsupported match HUD phase.")
    };

    public static IReadOnlyList<HudActionHint> BuildBowlingDeliveryOptions(
        IReadOnlyList<string> names,
        bool isGamePad,
        int page)
    {
        ArgumentNullException.ThrowIfNull(names);
        var buttonNames = isGamePad ? new[] { "A", "X", "Y", "B" } :
            new[] { "1", "2", "3", "4", "5", "6", "7", "8" };
        var pageSize = buttonNames.Length;
        var pageCount = Math.Max(1, (names.Count + pageSize - 1) / pageSize);
        var selectedPage = Math.Clamp(page, 0, pageCount - 1);
        var start = selectedPage * pageSize;
        var end = Math.Min(names.Count, start + pageSize);
        var options = new List<HudActionHint>();
        for (var index = start; index < end; index++)
            options.Add(new HudActionHint(buttonNames[index - start], names[index]));
        return options;
    }

    public static IReadOnlyList<string> BuildBowlerStatusLines(IReadOnlyList<BowlerSpellStatistics> figures)
    {
        ArgumentNullException.ThrowIfNull(figures);
        return figures.Select(item =>
            $"{(item.IsCurrentBowler ? ">" : " ")} {item.Bowler.Name}  {item.OversBowled}.0 ov  stamina {item.Stamina:0}%")
            .ToArray();
    }

    public static string GetShotControlLabel(
        string shotName,
        bool isGamePad,
        BattingInputScheme inputScheme = BattingInputScheme.Directional) =>
        shotName.ToLowerInvariant() switch
        {
            "defence" => isGamePad ? "X (defend)" : inputScheme == BattingInputScheme.Directional ? "W (defend)" : "S (block)",
            "drive" => isGamePad ? "A (attack)" : inputScheme == BattingInputScheme.Directional ? "S (attack)" : "S + D / W + D",
            "loft" => isGamePad ? "RB (loft)" : inputScheme == BattingInputScheme.Directional ? "SHIFT + S (loft)" : "SHIFT chord",
            _ => isGamePad ? "A / X / RB" : inputScheme == BattingInputScheme.Directional ? "S / W / SHIFT" : "S / S + D / W + D"
        };

    public static Rectangle CalculateFeedbackBannerBounds(
        int viewportWidth,
        int viewportHeight,
        int hudBottom,
        int panelWidth,
        int panelHeight)
    {
        var width = Math.Min(viewportWidth - 40, panelWidth);
        var height = Math.Min(viewportHeight - 24, panelHeight);
        const int margin = 20;
        var x = Math.Clamp(margin, 0, viewportWidth - width);
        var y = Math.Clamp(hudBottom + 12, 20, viewportHeight - height - 12);
        return new Rectangle(x, y, width, height);
    }

    private static string GetBowlingHint(MatchHudState state, string pause, bool includeNextBall)
    {
        if (state.IsHumanBowling)
        {
            if (state.BowlingStarted)
                return state.IsGamePad
                    ? $"RT: set pace    {pause}"
                    : $"Space: set pace    {pause}";
            if (!includeNextBall && state.FieldPickerOpen)
                return state.IsGamePad
                    ? $"D-pad: preview field    A: choose    B: close    {pause}"
                    : $"Up/Down: preview field    S: choose    W: close    {pause}";
            return state.IsGamePad
                ? $"Stick: aim    D-pad: field picker    A/X/Y/B: delivery    LB/RB: page    {pause}"
                : $"Left/Right: aim    Up/Down: field picker    1-8: choose delivery and bowl    {pause}";
        }

        var aim = state.IsGamePad ? "Next pitch: D-pad / left stick aim" : "Next pitch: arrows aim";
        var changeDelivery = state.IsGamePad ? "LB: delivery" : "C: delivery";
        var nextDelivery = state.IsGamePad ? "RB: bowl next" : "N: bowl next";
        var delivery = includeNextBall
            ? $"{changeDelivery} ({state.NextDeliveryName})"
            : $"{changeDelivery} ({state.NextDeliveryName}; next ball)";

        return includeNextBall
            ? $"{aim}    {delivery}    {nextDelivery}    {pause}"
            : $"{aim}    {delivery}    {pause}";
    }

    private static IReadOnlyList<HudActionHint> BuildHumanBowlingHints(MatchHudState state)
    {
        var hints = new List<HudActionHint>();
        if (state.FieldPickerOpen)
        {
            hints.Add(new HudActionHint(state.IsGamePad ? "D-PAD" : "UP / DOWN", state.FieldPickerOption ?? "Preview field"));
            hints.Add(new HudActionHint(state.IsGamePad ? "A / B" : "S / W", "Choose / close field"));
            return hints;
        }
        if (state.BowlingStarted)
        {
            hints.Add(new HudActionHint(state.IsGamePad ? "D-PAD / STICK" : "ARROWS", "Aim locked for this delivery"));
            hints.Add(new HudActionHint(state.IsGamePad ? "RT" : "SPACE", "Set pace in the meter"));
            if (state.BowlingNoBall)
                hints.Add(new HudActionHint("CALL", "NO BALL"));
            return hints;
        }

        if (state.BowlingOptions is { Count: > 0 } options)
            hints.AddRange(options);
        hints.Add(new HudActionHint(state.IsGamePad ? "LEFT STICK" : "LEFT / RIGHT", "Move pitch target"));
        hints.Add(new HudActionHint(state.IsGamePad ? "D-PAD" : "UP / DOWN", "Preview field preset"));
        if (state.IsGamePad && state.BowlingOptions is { Count: > 4 })
            hints.Add(new HudActionHint("LB / RB", $"Delivery page {state.BowlingPage + 1}"));
        return hints;
    }
}
