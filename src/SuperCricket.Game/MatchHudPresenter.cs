using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SuperCricket.Simulation;

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
    string NextDeliveryName);

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
    Vector3? ActiveBowlingTargetPosition);

internal readonly record struct MatchPauseMenuState(
    string ScoreLine,
    CpuDifficulty Difficulty,
    bool DeveloperMode,
    bool HighContrast,
    bool LargeText,
    float EffectsVolume,
    string? SettingsStatusMessage,
    bool AudioUnavailable);

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
    float BatterWicketLineZ);

internal readonly record struct LiveFeedbackBannerContent(
    string Title,
    string Detail,
    Color Accent,
    float RemainingSeconds);

/// <summary>Builds match HUD text and layout values without depending on a graphics device.</summary>
internal static class MatchHudPresenter
{
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
            var detail = state.ContactIsMiss
                ? "Your swing missed the ball"
                : string.IsNullOrWhiteSpace(timingDetail)
                    ? $"{state.ShotName} | {qualityLabel}"
                    : $"{state.ShotName} | {timingDetail} | {qualityLabel}";
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
            var shotButtons = state.IsGamePad ? "A / Y" : "Space / Shift";
            var detail = cue.State switch
            {
                BattingTimingCueState.Waiting => $"Press {shotButtons} as the marker enters green",
                BattingTimingCueState.SwingNow => $"Press {shotButtons} now for on-time contact",
                _ => $"Window passed; press {shotButtons} for late contact"
            };
            var accent = cue.State switch
            {
                BattingTimingCueState.Waiting => state.HighContrast ? Color.Yellow : new Color(255, 220, 74),
                BattingTimingCueState.SwingNow => state.HighContrast ? Color.Yellow : new Color(135, 255, 159),
                _ => state.HighContrast ? Color.White : new Color(255, 164, 77)
            };
            if (state.HasPredictedBounce)
                detail += state.Difficulty == CpuDifficulty.Pro
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

    public static int GetBattingContactZoneAlpha(CpuDifficulty difficulty) => difficulty switch
    {
        CpuDifficulty.Rookie => 220,
        CpuDifficulty.Standard => 120,
        _ => 0
    };

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
        var lines = new List<string>
        {
            "SUPER CRICKET  /  PAUSED",
            state.ScoreLine,
            $"CPU difficulty: {state.Difficulty}",
            "Batting: Left/Right aim | Space ground/defend | Shift loft",
            "GamePad batting: left stick aim | A ground/defend | Y loft",
            "Running: Enter/B starts; tap again for another; hold to turn back",
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
        var battingDescription = state.ShotName is { } shotName
            ? state.ContactQuality is { } quality
                ? $"{shotName}: {GetContactQualityLabel(quality)} ({quality:P0})"
                : $"{shotName}: no contact - ball beat the bat"
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
                : $"Enter: request another run    hold Enter: turn back    {pause}"),
            MatchHudPhase.BallBatted => WithCamera(state.IsGamePad
                ? $"B: run    {pause}"
                : $"Enter: run    {pause}"),
            MatchHudPhase.Batting => WithCamera(state.IsGamePad
                ? $"Left stick: aim    A: ground / defend    Y: loft    {pause}"
                : $"Left / Right: aim    Space: ground / defend    Shift: loft    {pause}"),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.Phase, "Unsupported match HUD phase.")
        };
    }

    public static Rectangle CalculateFeedbackBannerBounds(
        int viewportWidth,
        int viewportHeight,
        int hudBottom,
        int panelWidth,
        int panelHeight)
    {
        var width = Math.Min(viewportWidth - 40, panelWidth);
        var height = Math.Min(viewportHeight - 24, panelHeight);
        var x = (viewportWidth - width) / 2;
        var y = Math.Clamp(hudBottom + 12, 20, viewportHeight - height - 12);
        return new Rectangle(x, y, width, height);
    }

    private static string GetBowlingHint(MatchHudState state, string pause, bool includeNextBall)
    {
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
}
