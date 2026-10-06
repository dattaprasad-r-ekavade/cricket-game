namespace SuperCricket.Simulation;

/// <summary>Shared line rules used by live matches, automated batches, and CPU batting decisions.</summary>
public static class CricketDeliveryRuleModel
{
    public const float WideClearanceMeters = 0.55f;

    public static bool IsWide(float wicketLineX, float pitchWidthMeters)
    {
        if (!float.IsFinite(wicketLineX))
            throw new ArgumentOutOfRangeException(nameof(wicketLineX), "Wicket-line position must be finite.");
        if (!float.IsFinite(pitchWidthMeters) || pitchWidthMeters <= 0f)
            throw new ArgumentOutOfRangeException(nameof(pitchWidthMeters), "Pitch width must be finite and positive.");
        return MathF.Abs(wicketLineX) > pitchWidthMeters / 2f + WideClearanceMeters;
    }
}
