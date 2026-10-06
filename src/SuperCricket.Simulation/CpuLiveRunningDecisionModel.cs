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
    bool AttemptRun,
    CpuLiveRunDecisionReason Reason,
    float? EventTimeSeconds);

/// <summary>Checks a shot's real ball path against the live field before committing the CPU to a run.</summary>
public static class CpuLiveRunningDecisionModel
{
    public const float DefaultRunDurationSeconds = 1.35f;

    public static CpuLiveRunDecision Choose(
        bool runIntent,
        DeliveryPreset delivery,
        Vector3 contactPosition,
        Vector3 outgoingVelocity,
        IReadOnlyList<Vector3> fielderPositions,
        IReadOnlyList<int> fieldingRatings,
        float runDurationSeconds = DefaultRunDurationSeconds)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(fielderPositions);
        ArgumentNullException.ThrowIfNull(fieldingRatings);
        if (!IsFinite(contactPosition) || !IsFinite(outgoingVelocity) || outgoingVelocity.LengthSquared() <= 0.000001f)
            throw new ArgumentException("CPU running requires a finite contact point and non-zero outgoing velocity.");
        if (!float.IsFinite(runDurationSeconds) || runDurationSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(runDurationSeconds), "Run duration must be finite and positive.");
        if (fielderPositions.Count != FieldingSide.FielderCount || fieldingRatings.Count != FieldingSide.FielderCount)
            throw new ArgumentException($"CPU running decisions require exactly {FieldingSide.FielderCount} fielding positions and ratings.");
        if (!runIntent)
            return new CpuLiveRunDecision(false, CpuLiveRunDecisionReason.NoIntent, null);

        var errors = delivery.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid CPU running delivery: {string.Join(" ", errors)}", nameof(delivery));

        var fielding = new FieldingSide();
        fielding.ConfigureStartingPositions(fielderPositions);
        fielding.ConfigureFieldingRatings(fieldingRatings);
        var ball = new BallFlightSimulator(delivery);
        ball.ApplyBatContact(contactPosition, outgoingVelocity);
        var previous = ball.CurrentFrame;
        var maximumSteps = (int)MathF.Ceiling(
            MathF.Min(runDurationSeconds, delivery.MaximumSimulationSeconds) / ball.FixedTimeStepSeconds) + 1;
        for (var step = 0; step < maximumSteps && previous.Phase != BallMotionPhase.Settled; step++)
        {
            fielding.Step(ball.FixedTimeStepSeconds, previous.Position);
            var current = ball.Step();
            if (fielding.TryFindContact(previous, current, out _))
            {
                var runCompleted = current.TimeSeconds >= runDurationSeconds;
                return new CpuLiveRunDecision(
                    runCompleted,
                    runCompleted
                        ? CpuLiveRunDecisionReason.SafeRunWindow
                        : CpuLiveRunDecisionReason.FielderCanCollectBeforeRun,
                    current.TimeSeconds);
            }
            if (BoundaryResolver.TryFindCrossing(
                previous,
                current,
                delivery.FieldBoundaryRadiusMeters,
                delivery.FieldSurfaceHeightMeters,
                delivery.BallRadiusMeters,
                out _))
                return new CpuLiveRunDecision(false, CpuLiveRunDecisionReason.BoundaryBeforeRun, current.TimeSeconds);

            if (current.Phase == BallMotionPhase.Settled)
            {
                var safeAtStoppage = current.TimeSeconds >= runDurationSeconds * 0.72f;
                return new CpuLiveRunDecision(
                    safeAtStoppage,
                    safeAtStoppage
                        ? CpuLiveRunDecisionReason.SafeRunAtStoppage
                        : CpuLiveRunDecisionReason.BallStopsBeforeSafeCrossing,
                    current.TimeSeconds);
            }

            previous = current;
            if (current.TimeSeconds >= runDurationSeconds)
                return new CpuLiveRunDecision(true, CpuLiveRunDecisionReason.SafeRunWindow, current.TimeSeconds);
        }

        return new CpuLiveRunDecision(true, CpuLiveRunDecisionReason.SafeRunWindow, previous.TimeSeconds);
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
