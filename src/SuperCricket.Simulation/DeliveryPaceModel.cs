using SuperCricket.Content;

namespace SuperCricket.Simulation;

/// <summary>Sets an accessible CPU delivery pace for a human batter without mutating authored presets.</summary>
public static class DeliveryPaceModel
{
    public const float RookieOrFirstMatchSpeedMultiplier = 0.82f;

    public static float GetHumanBattingSpeedMultiplier(CpuDifficulty difficulty, bool isFirstMatch)
    {
        if (!Enum.IsDefined(difficulty))
            throw new ArgumentOutOfRangeException(nameof(difficulty));

        return isFirstMatch || difficulty == CpuDifficulty.Rookie
            ? RookieOrFirstMatchSpeedMultiplier
            : 1f;
    }

    public static DeliveryPreset ApplyHumanBattingPace(
        DeliveryPreset delivery,
        CpuDifficulty difficulty,
        bool isFirstMatch)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var adjustedDelivery = delivery.DeepCopy();
        var multiplier = GetHumanBattingSpeedMultiplier(difficulty, isFirstMatch);
        adjustedDelivery.ReleaseVelocity = Vector3Data.From(
            adjustedDelivery.ReleaseVelocity.ToVector3() * multiplier);

        var errors = adjustedDelivery.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Adjusted human-batting delivery is invalid: {string.Join(" ", errors)}", nameof(delivery));

        return adjustedDelivery;
    }
}
