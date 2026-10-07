using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

public enum CpuLiveRunDecisionReason
{
    NoIntent,
    FielderCanCollectBeforeRun,
    BoundaryBeforeRun,
    BallStopsBeforeSafeCrossing,
    SafeRunWindow,
    SafeRunAtStoppage
}

public readonly record struct CpuLiveRunDecision(
    int PlannedRuns,
    CpuLiveRunDecisionReason Reason,
    float? EventTimeSeconds)
{
    public bool AttemptRun => PlannedRuns > 0;
}

/// <summary>Checks a shot's real ball path against the live field before committing the CPU to a run.</summary>
public static class CpuLiveRunningDecisionModel
{
    public const float DefaultRunDurationSeconds = 1.35f;
    public const int MaximumPlannedRuns = 2;

    public static CpuLiveRunDecision Choose(
        bool runIntent,
        DeliveryPreset delivery,
        Vector3 contactPosition,
        Vector3 outgoingVelocity,
        IReadOnlyList<Vector3> fielderPositions,
        IReadOnlyList<int> fieldingRatings,
        float runDurationSeconds = DefaultRunDurationSeconds,
        float pickupAnimationDurationSeconds = 0.5f,
        float throwAnimationDurationSeconds = 0.6f,
        int maximumRunCount = MaximumPlannedRuns)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(fielderPositions);
        ArgumentNullException.ThrowIfNull(fieldingRatings);
        if (!IsFinite(contactPosition) || !IsFinite(outgoingVelocity) || outgoingVelocity.LengthSquared() <= 0.000001f)
            throw new ArgumentException("CPU running requires a finite contact point and non-zero outgoing velocity.");
        if (!float.IsFinite(runDurationSeconds) || runDurationSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(runDurationSeconds), "Run duration must be finite and positive.");
        if (!float.IsFinite(pickupAnimationDurationSeconds) || pickupAnimationDurationSeconds <= 0f ||
            !float.IsFinite(throwAnimationDurationSeconds) || throwAnimationDurationSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(pickupAnimationDurationSeconds), "Fielder pickup and throw durations must be finite and positive.");
        if (maximumRunCount is < 1 or > MaximumPlannedRuns)
            throw new ArgumentOutOfRangeException(nameof(maximumRunCount), $"Planned run count must be between 1 and {MaximumPlannedRuns}.");
        if (fielderPositions.Count != FieldingSide.FielderCount || fieldingRatings.Count != FieldingSide.FielderCount)
            throw new ArgumentException($"CPU running decisions require exactly {FieldingSide.FielderCount} fielding positions and ratings.");
        if (!runIntent)
            return new CpuLiveRunDecision(0, CpuLiveRunDecisionReason.NoIntent, null);

        var errors = delivery.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid CPU running delivery: {string.Join(" ", errors)}", nameof(delivery));

        var fielding = new FieldingSide();
        fielding.ConfigureStartingPositions(fielderPositions);
        fielding.ConfigureFieldingRatings(fieldingRatings);
        var ball = new BallFlightSimulator(delivery);
        ball.ApplyBatContact(contactPosition, outgoingVelocity);
        var previous = ball.CurrentFrame;
        var maximumRunTime = runDurationSeconds * maximumRunCount;
        var maximumSteps = (int)MathF.Ceiling(
            MathF.Min(maximumRunTime, delivery.MaximumSimulationSeconds) / ball.FixedTimeStepSeconds) + 1;
        for (var step = 0; step < maximumSteps && previous.Phase != BallMotionPhase.Settled; step++)
        {
            fielding.Step(ball.FixedTimeStepSeconds, previous.Position);
            var current = ball.Step();
            if (fielding.TryFindContact(previous, current, out var contact))
            {
                if (contact.Kind == FieldingContactKind.Catch)
                    return new CpuLiveRunDecision(0, CpuLiveRunDecisionReason.FielderCanCollectBeforeRun, current.TimeSeconds);

                var safeRuns = CountCompletedRuns(
                    current.TimeSeconds + pickupAnimationDurationSeconds + throwAnimationDurationSeconds,
                    runDurationSeconds,
                    maximumRunCount);
                return new CpuLiveRunDecision(
                    safeRuns,
                    safeRuns == 0
                        ? CpuLiveRunDecisionReason.FielderCanCollectBeforeRun
                        : CpuLiveRunDecisionReason.SafeRunWindow,
                    current.TimeSeconds);
            }
            if (BoundaryResolver.TryFindCrossing(
                previous,
                current,
                delivery.FieldBoundaryRadiusMeters,
                delivery.FieldSurfaceHeightMeters,
                delivery.BallRadiusMeters,
                out _))
            {
                var safeRuns = CountCompletedRuns(current.TimeSeconds, runDurationSeconds, maximumRunCount);
                return new CpuLiveRunDecision(
                    safeRuns,
                    safeRuns == 0 ? CpuLiveRunDecisionReason.BoundaryBeforeRun : CpuLiveRunDecisionReason.SafeRunWindow,
                    current.TimeSeconds);
            }

            if (current.Phase == BallMotionPhase.Settled)
            {
                var safeRunsAtStoppage = RunningScoringModel.CountRunsAtDeadBall(current.TimeSeconds, runDurationSeconds, maximumRunCount);
                return new CpuLiveRunDecision(
                    safeRunsAtStoppage,
                    safeRunsAtStoppage > 0
                        ? CpuLiveRunDecisionReason.SafeRunAtStoppage
                        : CpuLiveRunDecisionReason.BallStopsBeforeSafeCrossing,
                    current.TimeSeconds);
            }

            previous = current;
            if (current.TimeSeconds >= maximumRunTime)
                return new CpuLiveRunDecision(
                    CountCompletedRuns(current.TimeSeconds, runDurationSeconds, maximumRunCount),
                    CpuLiveRunDecisionReason.SafeRunWindow,
                    current.TimeSeconds);
        }

        var safeRunsAtEnd = RunningScoringModel.CountRunsAtDeadBall(previous.TimeSeconds, runDurationSeconds, maximumRunCount);
        return new CpuLiveRunDecision(
            safeRunsAtEnd,
            safeRunsAtEnd > 0 ? CpuLiveRunDecisionReason.SafeRunAtStoppage : CpuLiveRunDecisionReason.BallStopsBeforeSafeCrossing,
            previous.TimeSeconds);
    }

    private static int CountCompletedRuns(float elapsedSeconds, float runDurationSeconds, int maximumRunCount) =>
        Math.Clamp((int)MathF.Floor(elapsedSeconds / runDurationSeconds + 0.00001f), 0, maximumRunCount);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
