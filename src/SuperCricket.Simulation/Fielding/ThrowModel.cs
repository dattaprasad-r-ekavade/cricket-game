using System.Numerics;

namespace SuperCricket.Simulation.Fielding;

public readonly record struct ThrowOutcome(
    float DistanceMeters,
    float SpeedMetersPerSecond,
    float TravelSeconds,
    float GatherDelaySeconds,
    float ApexHeightMeters,
    float DirectHitChance,
    bool IsDirectHit,
    Vector3 InitialVelocity);

/// <summary>Creates seeded, flat return throws with rating-scaled speed and accuracy.</summary>
public static class ThrowModel
{
    public const float GravityMetersPerSecondSquared = 9.81f;

    public static ThrowOutcome Create(Vector3 start, Vector3 target, int armRating, int accuracyRating, int seed)
    {
        ValidatePosition(start, nameof(start));
        ValidatePosition(target, nameof(target));
        ValidateRating(armRating, nameof(armRating));
        ValidateRating(accuracyRating, nameof(accuracyRating));
        var horizontal = new Vector2(target.X - start.X, target.Z - start.Z);
        var distance = horizontal.Length();
        if (distance < 0.01f)
            throw new ArgumentOutOfRangeException(nameof(target), "A throw must travel at least 0.01 m.");

        var baseSpeed = Lerp(19f, 28f, Math.Clamp((distance - 5f) / 25f, 0f, 1f));
        var speed = baseSpeed * (0.85f + 0.15f * armRating / 100f);
        var elevationDegrees = distance < 25f ? 6f : 12f;
        var elevationRadians = elevationDegrees * (MathF.PI / 180f);
        var horizontalSpeed = speed * MathF.Cos(elevationRadians);
        var verticalSpeed = speed * MathF.Sin(elevationRadians);
        var direction = Vector2.Normalize(horizontal);
        var velocity = new Vector3(direction.X * horizontalSpeed, verticalSpeed, direction.Y * horizontalSpeed);
        var apex = verticalSpeed * verticalSpeed / (2f * GravityMetersPerSecondSquared);
        var baseHitChance = Lerp(0.55f, 0.15f, Math.Clamp((distance - 5f) / 25f, 0f, 1f));
        var chance = baseHitChance * (0.6f + 0.4f * accuracyRating / 100f);
        var directHit = new Random(seed).NextDouble() < chance;
        return new ThrowOutcome(
            distance,
            speed,
            distance / horizontalSpeed,
            directHit ? 0f : 0.35f,
            apex,
            chance,
            directHit,
            velocity);
    }

    private static float Lerp(float start, float end, float amount) => start + (end - start) * amount;

    private static void ValidateRating(int rating, string parameterName)
    {
        if (rating is < 0 or > 100)
            throw new ArgumentOutOfRangeException(parameterName, "Ratings must be between 0 and 100.");
    }

    private static void ValidatePosition(Vector3 value, string parameterName)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new ArgumentOutOfRangeException(parameterName, "Throw positions must be finite.");
    }
}
