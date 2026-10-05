using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

public readonly record struct BattingImpactResult(
    Vector3 OutgoingVelocity,
    float ContactQuality,
    float LaunchAngleDegrees);

/// <summary>Turns a swept bat contact into a tunable, repeatable shot impulse.</summary>
public static class BattingImpactModel
{
    private const float MaximumSwingSpeedMetersPerSecond = 16f;

    public static BattingImpactResult Calculate(
        Vector3 incomingVelocity,
        Vector3 batPointVelocity,
        Vector2 normalizedSweetSpotOffset,
        BattingShotData shot)
    {
        ArgumentNullException.ThrowIfNull(shot);
        if (!IsFinite(incomingVelocity) || !IsFinite(batPointVelocity) ||
            !float.IsFinite(normalizedSweetSpotOffset.X) || !float.IsFinite(normalizedSweetSpotOffset.Y) ||
            !float.IsFinite(shot.LaunchAngleDegrees) || !float.IsFinite(shot.HorizontalAim) ||
            !float.IsFinite(shot.SpeedTransfer))
            throw new ArgumentException("Batting impact inputs must be finite.");

        var incomingSpeed = incomingVelocity.Length();
        if (!float.IsFinite(incomingSpeed) || incomingSpeed <= 0.001f || shot.SpeedTransfer <= 0f)
            throw new ArgumentException("Batting impact requires a moving ball and positive shot speed transfer.");

        var offset = Vector2.Clamp(normalizedSweetSpotOffset, new Vector2(-1f), new Vector2(1f));
        var offsetDistance = MathF.Sqrt(offset.X * offset.X * 0.46f + offset.Y * offset.Y * 0.54f);
        var quality = Math.Clamp(1f - offsetDistance * 0.38f, 0.48f, 1f);

        var horizontalAim = Math.Clamp(shot.HorizontalAim + offset.X * 0.12f, -1f, 1f);
        var launchAngle = Math.Clamp(shot.LaunchAngleDegrees + offset.Y * 10f, -5f, 70f);
        var launchRadians = launchAngle * (MathF.PI / 180f);
        var horizontalDirection = Vector3.Normalize(new Vector3(horizontalAim, 0f, -1f));
        var shotDirection = new Vector3(
            horizontalDirection.X * MathF.Cos(launchRadians),
            MathF.Sin(launchRadians),
            horizontalDirection.Z * MathF.Cos(launchRadians));

        var limitedBatVelocity = ClampMagnitude(batPointVelocity, MaximumSwingSpeedMetersPerSecond);
        var outgoingVelocity = (
            shotDirection * (incomingSpeed * shot.SpeedTransfer) +
            limitedBatVelocity * 0.18f) * quality;

        if (!IsFinite(outgoingVelocity) || outgoingVelocity.LengthSquared() <= 0.000001f)
            throw new InvalidOperationException("Batting impact produced an invalid outgoing ball velocity.");

        return new BattingImpactResult(outgoingVelocity, quality, launchAngle);
    }

    private static Vector3 ClampMagnitude(Vector3 value, float maximum)
    {
        var lengthSquared = value.LengthSquared();
        if (lengthSquared <= maximum * maximum)
            return value;
        return value * (maximum / MathF.Sqrt(lengthSquared));
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
