using System.Numerics;

namespace SuperCricket.Simulation;

public enum FieldingContactKind
{
    GroundPickup,
    Catch
}

public readonly record struct FieldingContact(int FielderIndex, Vector3 Position, FieldingContactKind Kind);

/// <summary>Small fielder set with bounded chase movement and swept ball interception.</summary>
public sealed class FieldingSide
{
    private static readonly Vector3[] DefaultStartingPositions =
    [
        new(-3.2f, -0.08f, -16.0f),
        new(3.2f, -0.08f, -17.5f),
        new(-10.0f, -0.08f, -18.0f),
        new(0.0f, -0.08f, -20.0f),
        new(-20.0f, -0.08f, -7.0f),
        new(2.25f, -0.08f, -24.0f),
        new(-17.0f, -0.08f, 16.0f),
        new(16.0f, -0.08f, 16.0f),
        new(0.0f, -0.08f, -31.0f),
        new(0.0f, -0.08f, -11.2f)
    ];

    private readonly Vector3[] _startingPositions = (Vector3[])DefaultStartingPositions.Clone();
    private readonly Vector3[] _positions = (Vector3[])DefaultStartingPositions.Clone();
    public const float MoveSpeedMetersPerSecond = 7.5f;
    public const float ReactionSeconds = 0.14f;
    public const float FieldingRadiusMeters = 0.68f;
    public const float MinimumCatchHeightAboveGroundMeters = 0.72f;
    public const int FielderCount = 10;
    private readonly float[] _moveSpeeds = Enumerable.Repeat(MoveSpeedMetersPerSecond, FielderCount).ToArray();
    private readonly float[] _reactionTimes = Enumerable.Repeat(ReactionSeconds, FielderCount).ToArray();
    private readonly float[] _pickupRadii = Enumerable.Repeat(FieldingRadiusMeters, FielderCount).ToArray();
    private int _activeChaser = -1;
    private float _reactionRemaining;
    private float _boundaryRadiusMeters = 42f;

    public IReadOnlyList<Vector3> Positions => _positions;
    public int ActiveChaserIndex => _activeChaser;

    public void ConfigureBoundaryRadius(float boundaryRadiusMeters)
    {
        if (!float.IsFinite(boundaryRadiusMeters) || boundaryRadiusMeters <= 0f)
            throw new ArgumentOutOfRangeException(nameof(boundaryRadiusMeters));
        _boundaryRadiusMeters = boundaryRadiusMeters;
    }

    public void ConfigureStartingPositions(IReadOnlyList<Vector3> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (positions.Count != FielderCount)
            throw new ArgumentException($"A fielding side requires exactly {FielderCount} starting positions.", nameof(positions));

        for (var index = 0; index < positions.Count; index++)
        {
            if (!IsFinite(positions[index]))
                throw new ArgumentException($"Fielder position {index + 1} must be finite.", nameof(positions));
            _startingPositions[index] = positions[index];
        }
        Reset();
    }

    public void Reset()
    {
        Array.Copy(_startingPositions, _positions, _startingPositions.Length);
        _activeChaser = -1;
        _reactionRemaining = 0f;
    }

    public static float EstimateReachTime(Vector3 start, Vector3 target)
    {
        if (!IsFinite(start) || !IsFinite(target))
            throw new ArgumentException("Reach estimates require finite positions.");
        var distance = MathF.Sqrt(HorizontalDistanceSquared(start, target));
        return ReactionSeconds + MathF.Max(0f, distance - FieldingRadiusMeters) / MoveSpeedMetersPerSecond;
    }

    public static float EstimateReachTime(Vector3 start, Vector3 target, int fieldingRating)
    {
        ValidateFieldingRating(fieldingRating, nameof(fieldingRating));
        if (!IsFinite(start) || !IsFinite(target))
            throw new ArgumentException("Reach estimates require finite positions.");
        var moveSpeed = GetMoveSpeed(fieldingRating);
        var reactionTime = GetReactionTime(fieldingRating);
        var pickupRadius = GetPickupRadius(fieldingRating);
        var distance = MathF.Sqrt(HorizontalDistanceSquared(start, target));
        return reactionTime + MathF.Max(0f, distance - pickupRadius) / moveSpeed;
    }

    public void ConfigureFieldingRatings(IReadOnlyList<int> fieldingRatings)
    {
        ArgumentNullException.ThrowIfNull(fieldingRatings);
        if (fieldingRatings.Count != FielderCount)
            throw new ArgumentException($"A fielding side requires exactly {FielderCount} fielding ratings.", nameof(fieldingRatings));
        for (var index = 0; index < fieldingRatings.Count; index++)
            ValidateFieldingRating(fieldingRatings[index], nameof(fieldingRatings));

        for (var index = 0; index < fieldingRatings.Count; index++)
        {
            var rating = fieldingRatings[index];
            _moveSpeeds[index] = GetMoveSpeed(rating);
            _reactionTimes[index] = GetReactionTime(rating);
            _pickupRadii[index] = GetPickupRadius(rating);
        }
    }

    public void Step(float deltaTime, Vector3 ballPosition)
    {
        if (!float.IsFinite(deltaTime) || deltaTime < 0f || !IsFinite(ballPosition))
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Fielder movement requires a finite elapsed time and target.");

        var target = new Vector3(ballPosition.X, _positions[0].Y, ballPosition.Z);
        if (_activeChaser < 0)
        {
            var chaser = 0;
            var fastestReachTime = float.PositiveInfinity;
            for (var index = 0; index < _positions.Length; index++)
            {
                var reachDistance = MathF.Sqrt(HorizontalDistanceSquared(_positions[index], target));
                var reachTime = _reactionTimes[index] + MathF.Max(0f, reachDistance - _pickupRadii[index]) / _moveSpeeds[index];
                if (reachTime < fastestReachTime)
                {
                    chaser = index;
                    fastestReachTime = reachTime;
                }
            }
            _activeChaser = chaser;
            _reactionRemaining = _reactionTimes[chaser];
        }

        if (_reactionRemaining > 0f)
        {
            _reactionRemaining = MathF.Max(0f, _reactionRemaining - deltaTime);
            return;
        }

        var current = _positions[_activeChaser];
        var delta = target - current;
        var distance = MathF.Sqrt(HorizontalDistanceSquared(current, target));
        if (distance <= 0.0001f)
            return;

        var travel = MathF.Min(distance, _moveSpeeds[_activeChaser] * deltaTime);
        var moved = new Vector3(delta.X / distance * travel, 0f, delta.Z / distance * travel);
        var next = current + moved;
        var boundaryRadius = MathF.Sqrt(next.X * next.X + next.Z * next.Z);
        if (boundaryRadius > _boundaryRadiusMeters)
            next = new Vector3(next.X * _boundaryRadiusMeters / boundaryRadius, next.Y, next.Z * _boundaryRadiusMeters / boundaryRadius);
        _positions[_activeChaser] = next;
    }

    public bool TryFindContact(BallFlightFrame previous, BallFlightFrame current, out FieldingContact contact)
    {
        var bestFraction = float.PositiveInfinity;
        var bestIndex = -1;
        var bestPosition = Vector3.Zero;
        for (var index = 0; index < _positions.Length; index++)
        {
            var fielder = _positions[index];
            var start = new Vector2(previous.Position.X, previous.Position.Z);
            var end = new Vector2(current.Position.X, current.Position.Z);
            var point = new Vector2(fielder.X, fielder.Z);
            var segment = end - start;
            var denominator = segment.LengthSquared();
            var fraction = denominator <= 0.000001f
                ? 0f
                : Math.Clamp(Vector2.Dot(point - start, segment) / denominator, 0f, 1f);
            var closest = Vector2.Lerp(start, end, fraction);
            var pickupRadius = _pickupRadii[index];
            if (Vector2.DistanceSquared(closest, point) > pickupRadius * pickupRadius || fraction >= bestFraction)
                continue;

            var ballHeight = previous.Position.Y + (current.Position.Y - previous.Position.Y) * fraction;
            if (ballHeight > 2.45f || ballHeight < -0.1f)
                continue;

            bestFraction = fraction;
            bestIndex = index;
            bestPosition = Vector3.Lerp(previous.Position, current.Position, fraction);
        }

        if (bestIndex < 0)
        {
            contact = default;
            return false;
        }

        var fielderGroundHeight = _positions[bestIndex].Y;
        var kind = current.BounceCount == 0 && bestPosition.Y > fielderGroundHeight + MinimumCatchHeightAboveGroundMeters
            ? FieldingContactKind.Catch
            : FieldingContactKind.GroundPickup;
        contact = new FieldingContact(bestIndex, bestPosition, kind);
        return true;
    }

    private static float HorizontalDistanceSquared(Vector3 first, Vector3 second)
    {
        var x = first.X - second.X;
        var z = first.Z - second.Z;
        return x * x + z * z;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static float GetMoveSpeed(int rating) => MoveSpeedMetersPerSecond * (0.75f + rating * 0.005f);
    private static float GetReactionTime(int rating) => 0.22f - rating * 0.0016f;
    private static float GetPickupRadius(int rating) => 0.50f + rating * 0.0036f;

    private static void ValidateFieldingRating(int rating, string parameterName)
    {
        if (rating is < 0 or > 100)
            throw new ArgumentOutOfRangeException(parameterName, "Fielding ratings must be between 0 and 100.");
    }
}
