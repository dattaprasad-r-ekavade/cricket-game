using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

/// <summary>Classifies a prepared delivery at the striker's crease for stroke selection.</summary>
public static class BallCategoryClassifier
{
    public const float YorkerLengthLimitMeters = 1f;
    public const float FullLengthLimitMeters = 3f;
    public const float GoodLengthLimitMeters = 6.5f;
    public const float HighArrivalThresholdMeters = 0.9f;
    public const float WideLineMarginMeters = 0.45f;

    public static BallCategory Classify(DeliveryPreset prepared, float batterStanceX, bool leftHanded)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (!float.IsFinite(batterStanceX))
            throw new ArgumentOutOfRangeException(nameof(batterStanceX));

        var crossing = FindCreaseCrossing(prepared);
        var creaseZ = prepared.StartVelocity.Z < 0f
            ? CricketPitchGeometry.NearWicketZ
            : CricketPitchGeometry.FarWicketZ;
        var travelDirection = MathF.Sign(prepared.StartVelocity.Z);
        var firstBounce = BowlingAimModel.FindFirstBounce(prepared);
        var bounceDistance = firstBounce is { } bounce
            ? (creaseZ - bounce.Position.Z) * travelDirection
            : float.NaN;
        var length = firstBounce is null || !float.IsFinite(bounceDistance) || bounceDistance <= 0f
            ? BallLength.FullToss
            : ClassifyLength(bounceDistance);
        var line = ClassifyLine(crossing.Position.X, batterStanceX, leftHanded);
        var surfaceHeight = MathF.Abs(crossing.Position.X) <= prepared.PitchWidthMeters / 2f
            ? prepared.PitchSurfaceHeightMeters
            : prepared.FieldSurfaceHeightMeters;
        var arrivalHeight = crossing.Position.Y - surfaceHeight;
        if (!float.IsFinite(arrivalHeight))
            throw new InvalidOperationException("The prepared delivery crossed the batting crease at an invalid height.");

        return new BallCategory(length, line, MathF.Max(0f, arrivalHeight));
    }

    public static BallLength ClassifyLength(float bounceDistanceInFrontOfCreaseMeters)
    {
        if (!float.IsFinite(bounceDistanceInFrontOfCreaseMeters))
            throw new ArgumentOutOfRangeException(nameof(bounceDistanceInFrontOfCreaseMeters));
        if (bounceDistanceInFrontOfCreaseMeters <= 0f)
            return BallLength.FullToss;
        if (bounceDistanceInFrontOfCreaseMeters < YorkerLengthLimitMeters)
            return BallLength.Yorker;
        if (bounceDistanceInFrontOfCreaseMeters < FullLengthLimitMeters)
            return BallLength.Full;
        if (bounceDistanceInFrontOfCreaseMeters <= GoodLengthLimitMeters)
            return BallLength.Good;
        return BallLength.Short;
    }

    public static BallLine ClassifyLine(float ballCrossingX, float batterStanceX, bool leftHanded)
    {
        if (!float.IsFinite(ballCrossingX) || !float.IsFinite(batterStanceX))
            throw new ArgumentOutOfRangeException(nameof(ballCrossingX), "Ball and stance positions must be finite.");

        var relativeX = ballCrossingX - batterStanceX;
        if (leftHanded)
            relativeX = -relativeX;
        var outsideStumps = MathF.Abs(relativeX) > CricketPitchGeometry.WicketHalfWidthMeters;
        if (!outsideStumps)
            return BallLine.Stumps;
        if (relativeX > 0f)
            return relativeX > CricketPitchGeometry.WicketHalfWidthMeters + WideLineMarginMeters
                ? BallLine.WideOutsideOff
                : BallLine.OutsideOff;
        return relativeX < -CricketPitchGeometry.WicketHalfWidthMeters - WideLineMarginMeters
            ? BallLine.WideLeg
            : BallLine.Leg;
    }

    public static StrokeFoot ChooseFoot(BallCategory ball, ShotDirection8 direction)
    {
        if (!Enum.IsDefined(ball.Length) || !Enum.IsDefined(ball.Line))
            throw new ArgumentOutOfRangeException(nameof(ball));
        if (!float.IsFinite(ball.ArrivalHeightMeters) || ball.ArrivalHeightMeters < 0f)
            throw new ArgumentOutOfRangeException(nameof(ball));
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));

        if (ball.Length == BallLength.Short || ball.ArrivalHeightMeters > HighArrivalThresholdMeters)
            return StrokeFoot.Back;
        if (ball.Length is BallLength.Full or BallLength.Yorker or BallLength.FullToss)
            return StrokeFoot.Front;
        return direction is ShotDirection8.Square or ShotDirection8.Fine or ShotDirection8.LegFine or ShotDirection8.LegSquare
            ? StrokeFoot.Back
            : StrokeFoot.Front;
    }

    private static BallFlightFrame FindCreaseCrossing(DeliveryPreset prepared)
    {
        var direction = MathF.Sign(prepared.StartVelocity.Z);
        if (direction == 0f)
            throw new ArgumentException("A delivery must have a non-zero velocity along the pitch.", nameof(prepared));
        var creaseZ = direction < 0f ? CricketPitchGeometry.NearWicketZ : CricketPitchGeometry.FarWicketZ;
        var simulator = new BallFlightSimulator(prepared);
        var previous = simulator.CurrentFrame;
        var maximumSteps = (int)MathF.Ceiling(prepared.MaximumSimulationSeconds / prepared.FixedTimeStepSeconds);
        for (var step = 0; step < maximumSteps; step++)
        {
            var current = simulator.Step();
            var crossed = direction < 0f
                ? previous.Position.Z >= creaseZ && current.Position.Z <= creaseZ
                : previous.Position.Z <= creaseZ && current.Position.Z >= creaseZ;
            if (crossed)
            {
                var span = current.Position.Z - previous.Position.Z;
                if (MathF.Abs(span) < 0.000001f)
                    return current;
                var fraction = Math.Clamp((creaseZ - previous.Position.Z) / span, 0f, 1f);
                return new BallFlightFrame(
                    previous.TimeSeconds + (current.TimeSeconds - previous.TimeSeconds) * fraction,
                    Vector3.Lerp(previous.Position, current.Position, fraction),
                    Vector3.Lerp(previous.Velocity, current.Velocity, fraction),
                    current.BounceCount,
                    current.Phase);
            }
            if (current.Phase == BallMotionPhase.Settled)
                break;
            previous = current;
        }

        throw new ArgumentException("The prepared delivery does not reach the batting crease during its simulated flight.", nameof(prepared));
    }
}
