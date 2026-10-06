using System.Numerics;

namespace SuperCricket.Simulation;

public readonly record struct SweptBattingContact(
    Vector3 Position,
    Vector3 SweetSpotPosition,
    Vector2 NormalizedSweetSpotOffset,
    Vector3 BatPointVelocity,
    float HitFraction);

/// <summary>Finds contact while both the ball and the animated bat move during a physics tick.</summary>
public static class SweptBattingContactResolver
{
    public static bool TryResolve(
        Vector3 ballStart,
        Vector3 ballEnd,
        Matrix4x4 batStartWorld,
        Matrix4x4 batEndWorld,
        Vector3 bladeMinimum,
        Vector3 bladeMaximum,
        float expansionMeters,
        float elapsedSeconds,
        out SweptBattingContact contact)
    {
        contact = default;
        if (!IsFinite(ballStart) || !IsFinite(ballEnd) || !IsFinite(bladeMinimum) || !IsFinite(bladeMaximum) ||
            !IsFinite(batStartWorld) || !IsFinite(batEndWorld) ||
            !float.IsFinite(expansionMeters) || expansionMeters < 0f ||
            !float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f ||
            bladeMinimum.X > bladeMaximum.X || bladeMinimum.Y > bladeMaximum.Y || bladeMinimum.Z > bladeMaximum.Z)
            throw new ArgumentException("Swept bat contact requires finite transforms, ordered blade bounds, and non-negative timing and expansion.");

        if (!Matrix4x4.Invert(batStartWorld, out var worldToBatStart) ||
            !Matrix4x4.Invert(batEndWorld, out var worldToBatEnd))
            return false;

        var localStart = Vector3.Transform(ballStart, worldToBatStart);
        var localEnd = Vector3.Transform(ballEnd, worldToBatEnd);
        var padding = new Vector3(expansionMeters);
        if (!SegmentIntersectsBox(localStart, localEnd, bladeMinimum - padding, bladeMaximum + padding, out var hitFraction))
            return false;

        var localContact = Vector3.Lerp(localStart, localEnd, hitFraction);
        var contactPosition = Vector3.Lerp(ballStart, ballEnd, hitFraction);
        var center = (bladeMinimum + bladeMaximum) * 0.5f;
        var sweetSpotPosition = Vector3.Lerp(
            Vector3.Transform(center, batStartWorld),
            Vector3.Transform(center, batEndWorld),
            hitFraction);
        var halfSize = (bladeMaximum - bladeMinimum) * 0.5f + padding;
        var normalizedOffset = new Vector2(
            Math.Clamp((localContact.X - center.X) / MathF.Max(halfSize.X, 0.001f), -1f, 1f),
            Math.Clamp((localContact.Y - center.Y) / MathF.Max(halfSize.Y, 0.001f), -1f, 1f));

        var batPointVelocity = elapsedSeconds <= 0.000001f
            ? Vector3.Zero
            : (Vector3.Transform(localContact, batEndWorld) - Vector3.Transform(localContact, batStartWorld)) / elapsedSeconds;
        contact = new SweptBattingContact(contactPosition, sweetSpotPosition, normalizedOffset, batPointVelocity, hitFraction);
        return true;
    }

    private static bool SegmentIntersectsBox(Vector3 start, Vector3 end, Vector3 minimum, Vector3 maximum, out float entry)
    {
        var direction = end - start;
        var lower = 0f;
        var upper = 1f;
        if (!ClipAxis(start.X, direction.X, minimum.X, maximum.X, ref lower, ref upper) ||
            !ClipAxis(start.Y, direction.Y, minimum.Y, maximum.Y, ref lower, ref upper) ||
            !ClipAxis(start.Z, direction.Z, minimum.Z, maximum.Z, ref lower, ref upper))
        {
            entry = 0f;
            return false;
        }

        entry = lower;
        return true;
    }

    private static bool ClipAxis(float origin, float direction, float minimum, float maximum, ref float lower, ref float upper)
    {
        if (MathF.Abs(direction) < 0.000001f)
            return origin >= minimum && origin <= maximum;

        var inverseDirection = 1f / direction;
        var first = (minimum - origin) * inverseDirection;
        var second = (maximum - origin) * inverseDirection;
        if (first > second)
            (first, second) = (second, first);
        lower = MathF.Max(lower, first);
        upper = MathF.Min(upper, second);
        return lower <= upper;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsFinite(Matrix4x4 value) =>
        float.IsFinite(value.M11) && float.IsFinite(value.M12) && float.IsFinite(value.M13) && float.IsFinite(value.M14) &&
        float.IsFinite(value.M21) && float.IsFinite(value.M22) && float.IsFinite(value.M23) && float.IsFinite(value.M24) &&
        float.IsFinite(value.M31) && float.IsFinite(value.M32) && float.IsFinite(value.M33) && float.IsFinite(value.M34) &&
        float.IsFinite(value.M41) && float.IsFinite(value.M42) && float.IsFinite(value.M43) && float.IsFinite(value.M44);
}
