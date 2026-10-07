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
        BattingShotData shot,
        TeamPlayerData? batter = null)
    {
        ArgumentNullException.ThrowIfNull(shot);
        if (batter is { Timing: < 0 or > 100 } or { Power: < 0 or > 100 })
            throw new ArgumentException("Batter timing and power ratings must be between 0 and 100.", nameof(batter));
        if (!IsFinite(incomingVelocity) || !IsFinite(batPointVelocity) ||
            !float.IsFinite(normalizedSweetSpotOffset.X) || !float.IsFinite(normalizedSweetSpotOffset.Y) ||
            !float.IsFinite(shot.LaunchAngleDegrees) || !float.IsFinite(shot.HorizontalAim) ||
            !float.IsFinite(shot.ForwardAim) ||
            !float.IsFinite(shot.SpeedTransfer))
            throw new ArgumentException("Batting impact inputs must be finite.");

        var incomingSpeed = incomingVelocity.Length();
        if (!float.IsFinite(incomingSpeed) || incomingSpeed <= 0.001f || shot.SpeedTransfer <= 0f)
            throw new ArgumentException("Batting impact requires a moving ball and positive shot speed transfer.");

        var offset = Vector2.Clamp(normalizedSweetSpotOffset, new Vector2(-1f), new Vector2(1f));
        var offsetDistance = MathF.Sqrt(offset.X * offset.X * 0.46f + offset.Y * offset.Y * 0.54f);
        var quality = Math.Clamp(1f - offsetDistance * 0.38f, 0.48f, 1f);
        if (batter is not null)
            quality = Math.Clamp(quality * (0.8f + batter.Timing * 0.004f), 0.38f, 1f);

        var horizontalAim = Math.Clamp(shot.HorizontalAim + offset.X * 0.12f, -1f, 1f);
        var launchAngle = Math.Clamp(shot.LaunchAngleDegrees + offset.Y * 10f, -5f, 70f);
        var launchRadians = launchAngle * (MathF.PI / 180f);
        var horizontalDirection = GetHorizontalShotDirection(horizontalAim, shot.ForwardAim);
        var shotDirection = new Vector3(
            horizontalDirection.X * MathF.Cos(launchRadians),
            MathF.Sin(launchRadians),
            horizontalDirection.Z * MathF.Cos(launchRadians));

        var limitedBatVelocity = ClampMagnitude(batPointVelocity, MaximumSwingSpeedMetersPerSecond);
        var powerMultiplier = batter is null ? 1f : 0.8f + batter.Power * 0.004f;
        var outgoingVelocity = (
            shotDirection * (incomingSpeed * shot.SpeedTransfer * powerMultiplier) +
            limitedBatVelocity * 0.18f) * quality;

        if (!IsFinite(outgoingVelocity) || outgoingVelocity.LengthSquared() <= 0.000001f)
            throw new InvalidOperationException("Batting impact produced an invalid outgoing ball velocity.");

        var outgoingHorizontalSpeed = new Vector2(outgoingVelocity.X, outgoingVelocity.Z).Length();
        var actualLaunchAngle = MathF.Atan2(outgoingVelocity.Y, outgoingHorizontalSpeed) * (180f / MathF.PI);
        return new BattingImpactResult(outgoingVelocity, quality, actualLaunchAngle);
    }

    /// <summary>Returns a shot direction relative to the striker-end coordinate system.</summary>
    public static Vector3 GetHorizontalShotDirection(float horizontalAim)
        => GetHorizontalShotDirection(horizontalAim, 1f);

    /// <summary>Returns a normalized shot direction in the striker coordinate system, including depth.</summary>
    public static Vector3 GetHorizontalShotDirection(float horizontalAim, float forwardAim)
    {
        if (!float.IsFinite(horizontalAim) || horizontalAim is < -1f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(horizontalAim), "Shot direction must be between -1 and 1.");
        if (!float.IsFinite(forwardAim) || forwardAim is < -1f or > 1f ||
            (MathF.Abs(horizontalAim) < 0.001f && MathF.Abs(forwardAim) < 0.001f))
            throw new ArgumentOutOfRangeException(nameof(forwardAim), "Shot direction must be finite and non-zero between -1 and 1.");
        return Vector3.Normalize(new Vector3(horizontalAim, 0f, forwardAim));
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
