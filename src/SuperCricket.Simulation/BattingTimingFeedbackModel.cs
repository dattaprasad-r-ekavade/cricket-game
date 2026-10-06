namespace SuperCricket.Simulation;

public enum BattingTimingBand
{
    Early,
    Perfect,
    Late
}

public readonly record struct BattingTimingAssessment(BattingTimingBand Band, float OffsetFromIdealSeconds);

/// <summary>Classifies the player's shot-input time against a calibrated ideal for the shot and delivery.</summary>
public static class BattingTimingFeedbackModel
{
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
