using SuperCricket.Content;

namespace SuperCricket.Simulation;

/// <summary>Decides when the CPU should let a clearly wide delivery pass unused.</summary>
public static class CpuLeaveDecisionModel
{
    public const float MaximumLeavePressure = 0.55f;

    public static bool ShouldLeave(DeliveryPreset delivery, float pressure)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (!float.IsFinite(pressure) || pressure is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(pressure), "Batting pressure must be between 0 and 1.");
        if (pressure >= MaximumLeavePressure ||
            !BattingPracticeAnalyzer.TryGetWicketLinePosition(delivery, out var crossingPosition))
            return false;
        return CricketDeliveryRuleModel.IsWide(crossingPosition.X, delivery.PitchWidthMeters);
    }
}
