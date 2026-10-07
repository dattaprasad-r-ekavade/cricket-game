namespace SuperCricket.Simulation;

public enum FieldingCollectionAction { CaughtDismissal, ReturnThrow, Held }

/// <summary>Ball rest stops movement, while fielders and runners continue until collection.</summary>
public static class BattedBallFieldingModel
{
    public const float MaximumCollectionWaitSeconds = 30f;

    public static bool CanAdvance(bool battedBall, bool deliveryComplete, bool returnThrowActive, BallMotionPhase phase) =>
        !deliveryComplete && !returnThrowActive && (phase != BallMotionPhase.Settled || battedBall);

    public static FieldingCollectionAction ResolveCollection(
        DeliverySession delivery, FieldingContactKind kind, bool runnersMoving)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (delivery.IsComplete)
            throw new InvalidOperationException("A completed delivery cannot receive another fielder collection.");
        if (kind == FieldingContactKind.Catch && delivery.ResolveCatch())
            return FieldingCollectionAction.CaughtDismissal;
        return runnersMoving ? FieldingCollectionAction.ReturnThrow : FieldingCollectionAction.Held;
    }

    public static bool TryAdvance(
        FieldingSide fielding, BallFlightFrame previous, BallFlightFrame current,
        float elapsedSeconds, out FieldingContact contact)
    {
        ArgumentNullException.ThrowIfNull(fielding);
        fielding.Step(elapsedSeconds, previous.Position);
        return fielding.TryFindContact(previous, current, out contact);
    }

    /// <summary>Extends a physically resting trajectory for fielding, without manufacturing a dead ball.</summary>
    public static IEnumerable<BallFlightFrame> FramesThroughCollection(
        IReadOnlyList<BallFlightFrame> trajectory, float fixedTimeStepSeconds)
    {
        ArgumentNullException.ThrowIfNull(trajectory);
        if (trajectory.Count == 0)
            throw new ArgumentException("A batted-ball trajectory must include its contact frame.", nameof(trajectory));
        if (!float.IsFinite(fixedTimeStepSeconds) || fixedTimeStepSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(fixedTimeStepSeconds));
        foreach (var frame in trajectory)
            yield return frame;
        var resting = trajectory[^1];
        if (resting.Phase != BallMotionPhase.Settled || resting.Velocity.LengthSquared() > 0f)
            throw new InvalidOperationException("The batted-ball trajectory exhausted its analysis budget before physical rest or collection.");
        var maximumSteps = (int)MathF.Ceiling(MaximumCollectionWaitSeconds / fixedTimeStepSeconds);
        for (var step = 1; step <= maximumSteps; step++)
            yield return resting with { TimeSeconds = resting.TimeSeconds + step * fixedTimeStepSeconds };
        // This is a diagnostic guard, never an instruction to score or finish a delivery.
        throw new InvalidOperationException("No fielder collected the resting ball within the fielding analysis budget.");
    }
}
