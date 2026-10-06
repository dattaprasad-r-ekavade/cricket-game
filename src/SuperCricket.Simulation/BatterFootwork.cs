namespace SuperCricket.Simulation;

/// <summary>Shared lateral footwork limits for live play and batting-practice analysis.</summary>
public static class BatterFootwork
{
    public const float StepDistanceMeters = 0.45f;
    public const int MaximumSteps = 5;
    public const float MaximumOffsetMeters = StepDistanceMeters * MaximumSteps;
    public const float MovementSpeedMetersPerSecond = 4.5f;

    public static IReadOnlyList<float> PracticeOffsets { get; } = Array.AsReadOnly(
        Enumerable.Range(-MaximumSteps, MaximumSteps * 2 + 1)
            .Select(step => step * StepDistanceMeters)
            .ToArray());

    public static float AddStep(float currentTargetMeters, float direction)
    {
        if (!float.IsFinite(currentTargetMeters) || !float.IsFinite(direction))
            throw new ArgumentOutOfRangeException(nameof(currentTargetMeters), "Footwork input must be finite.");

        var stepDirection = MathF.Sign(direction);
        return Math.Clamp(currentTargetMeters + stepDirection * StepDistanceMeters,
            -MaximumOffsetMeters,
            MaximumOffsetMeters);
    }

    public static float Advance(float currentMeters, float targetMeters, float deltaTimeSeconds)
    {
        if (!float.IsFinite(currentMeters) || !float.IsFinite(targetMeters) ||
            !float.IsFinite(deltaTimeSeconds) || deltaTimeSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds), "Footwork motion requires finite positions and a non-negative elapsed time.");

        var maximumTravel = MovementSpeedMetersPerSecond * deltaTimeSeconds;
        return currentMeters + Math.Clamp(targetMeters - currentMeters, -maximumTravel, maximumTravel);
    }
}
