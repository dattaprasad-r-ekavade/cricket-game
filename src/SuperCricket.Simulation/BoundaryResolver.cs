using System.Numerics;

namespace SuperCricket.Simulation;

public readonly record struct BoundaryCrossing(float Fraction, Vector3 Position, bool ClearedInTheAir)
{
    public int BoundaryRuns => ClearedInTheAir ? 6 : 4;
}

public static class BoundaryResolver
{
    public static bool TryFindCrossing(
        BallFlightFrame previous,
        BallFlightFrame current,
        float boundaryRadiusMeters,
        float fieldSurfaceHeightMeters,
        float ballRadiusMeters,
        out BoundaryCrossing crossing)
    {
        if (!float.IsFinite(boundaryRadiusMeters) || boundaryRadiusMeters <= 0f ||
            !float.IsFinite(fieldSurfaceHeightMeters) ||
            !float.IsFinite(ballRadiusMeters) || ballRadiusMeters <= 0f ||
            !IsFinite(previous.Position) || !IsFinite(current.Position))
            throw new ArgumentException("Boundary checks require finite positions, a positive boundary radius, and a positive ball radius.");

        var radiusSquared = boundaryRadiusMeters * boundaryRadiusMeters;
        if (HorizontalDistanceSquared(previous.Position) >= radiusSquared ||
            HorizontalDistanceSquared(current.Position) < radiusSquared)
        {
            crossing = default;
            return false;
        }

        var low = 0f;
        var high = 1f;
        for (var iteration = 0; iteration < 24; iteration++)
        {
            var fraction = (low + high) * 0.5f;
            var position = Vector3.Lerp(previous.Position, current.Position, fraction);
            if (HorizontalDistanceSquared(position) < radiusSquared)
                low = fraction;
            else
                high = fraction;
        }

        var crossingPosition = Vector3.Lerp(previous.Position, current.Position, high);
        var stayedAirborne = previous.BounceCount == 0 && current.BounceCount == 0;
        var clearedInTheAir = stayedAirborne &&
            crossingPosition.Y > fieldSurfaceHeightMeters + ballRadiusMeters + 0.0001f;
        crossing = new BoundaryCrossing(high, crossingPosition, clearedInTheAir);
        return true;
    }

    private static float HorizontalDistanceSquared(Vector3 position) =>
        position.X * position.X + position.Z * position.Z;

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
