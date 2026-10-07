namespace SuperCricket.Simulation;

public enum BattingTimingBand
{
    Early,
    Perfect,
    Late
}

public enum BattingTimingCueState
{
    Waiting,
    SwingNow,
    WindowPassed
}

public readonly record struct BattingTimingAssessment(BattingTimingBand Band, float OffsetFromIdealSeconds);

/// <summary>Live timing-gauge position and its calibrated on-time region.</summary>
public readonly record struct BattingTimingCue(
    BattingTimingCueState State,
    float Progress,
    float WindowStart,
    float WindowEnd,
    float SecondsUntilWindow);

/// <summary>Classifies the player's shot-input time against a calibrated ideal for the shot and delivery.</summary>
public static class BattingTimingFeedbackModel
{
    public static BattingTimingCue EvaluateCue(
        float elapsedSinceReleaseSeconds,
        float idealInputDelaySeconds,
        float perfectWindowSeconds,
        float timelineWindowMultiples = 3f)
    {
        if (!float.IsFinite(elapsedSinceReleaseSeconds) || elapsedSinceReleaseSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSinceReleaseSeconds));
        if (!float.IsFinite(idealInputDelaySeconds))
            throw new ArgumentOutOfRangeException(nameof(idealInputDelaySeconds));
        if (!float.IsFinite(perfectWindowSeconds) || perfectWindowSeconds is <= 0f or > 0.5f)
            throw new ArgumentOutOfRangeException(nameof(perfectWindowSeconds));
        if (!float.IsFinite(timelineWindowMultiples) || timelineWindowMultiples is <= 1f or > 20f)
            throw new ArgumentOutOfRangeException(nameof(timelineWindowMultiples));

        var windowStartSeconds = idealInputDelaySeconds - perfectWindowSeconds;
        var windowEndSeconds = idealInputDelaySeconds + perfectWindowSeconds;
        var timelineStartSeconds = idealInputDelaySeconds - perfectWindowSeconds * timelineWindowMultiples;
        var timelineDurationSeconds = perfectWindowSeconds * timelineWindowMultiples * 2f;
        var state = elapsedSinceReleaseSeconds < windowStartSeconds
            ? BattingTimingCueState.Waiting
            : elapsedSinceReleaseSeconds > windowEndSeconds
                ? BattingTimingCueState.WindowPassed
                : BattingTimingCueState.SwingNow;
        return new BattingTimingCue(
            state,
            Math.Clamp((elapsedSinceReleaseSeconds - timelineStartSeconds) / timelineDurationSeconds, 0f, 1f),
            Math.Clamp((windowStartSeconds - timelineStartSeconds) / timelineDurationSeconds, 0f, 1f),
            Math.Clamp((windowEndSeconds - timelineStartSeconds) / timelineDurationSeconds, 0f, 1f),
            MathF.Max(0f, windowStartSeconds - elapsedSinceReleaseSeconds));
    }

    public static BattingTimingAssessment Assess(
        float actualInputDelaySeconds,
        float idealInputDelaySeconds,
        float perfectWindowSeconds)
    {
        if (!float.IsFinite(actualInputDelaySeconds))
            throw new ArgumentOutOfRangeException(nameof(actualInputDelaySeconds));
        if (!float.IsFinite(idealInputDelaySeconds))
            throw new ArgumentOutOfRangeException(nameof(idealInputDelaySeconds));
        if (!float.IsFinite(perfectWindowSeconds) || perfectWindowSeconds is <= 0f or > 0.5f)
            throw new ArgumentOutOfRangeException(nameof(perfectWindowSeconds));

        var offset = actualInputDelaySeconds - idealInputDelaySeconds;
        var band = offset < -perfectWindowSeconds
            ? BattingTimingBand.Early
            : offset > perfectWindowSeconds
                ? BattingTimingBand.Late
                : BattingTimingBand.Perfect;
        return new BattingTimingAssessment(band, offset);
    }
}
