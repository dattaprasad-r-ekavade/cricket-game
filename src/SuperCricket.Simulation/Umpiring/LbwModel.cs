using System.Numerics;

namespace SuperCricket.Simulation.Umpiring;

public enum LbwBand
{
    NotOut,
    Hitting,
    UmpiresCall,
    Missing,
    GoingOver
}

public readonly record struct LbwContact(
    Vector3 Position,
    Vector3 PreImpactVelocity,
    float BallRadiusMeters = 0.036f,
    float PitchSurfaceHeightMeters = -0.025f,
    float GravityMetersPerSecondSquared = 9.81f,
    float PitchBounceRestitution = 0.53f,
    bool HasPitched = true,
    bool PitchedOutsideLeg = false,
    bool ImpactOutsideOffStump = false,
    bool ShotOffered = true,
    float WicketPlaneZ = CricketPitchGeometry.NearWicketZ);

public readonly record struct LbwDecision(LbwBand Band, bool IsOut, float ProjectedOffsetX, float ProjectedHeightMeters);

/// <summary>Projects a pad/body impact to the stumps and applies LBW law bands.</summary>
public static class LbwModel
{
    public const float HittingHalfWidthMeters = 0.09f;
    public const float UmpiresCallHalfWidthMeters = 0.15f;
    public const float HittingHeightMeters = 0.69f;
    public const float UmpiresCallHeightMeters = 0.74f;

    public static LbwDecision Evaluate(LbwContact contact, float umpiresCallProbability, int seed)
    {
        Validate(contact, umpiresCallProbability);
        if (contact.PitchedOutsideLeg || contact.ImpactOutsideOffStump && contact.ShotOffered)
            return new LbwDecision(LbwBand.NotOut, false, float.NaN, float.NaN);

        if (!TryProjectToWickets(contact, out var offsetX, out var height))
            return new LbwDecision(LbwBand.Missing, false, float.NaN, float.NaN);

        if (height > UmpiresCallHeightMeters)
            return new LbwDecision(LbwBand.GoingOver, false, offsetX, height);
        if (MathF.Abs(offsetX) > UmpiresCallHalfWidthMeters)
            return new LbwDecision(LbwBand.Missing, false, offsetX, height);

        if (MathF.Abs(offsetX) <= HittingHalfWidthMeters && height <= HittingHeightMeters)
            return new LbwDecision(LbwBand.Hitting, true, offsetX, height);

        var outOnCall = new Random(seed).NextDouble() < umpiresCallProbability;
        return new LbwDecision(LbwBand.UmpiresCall, outOnCall, offsetX, height);
    }

    public static bool TryProjectToWickets(LbwContact contact, out float offsetX, out float heightMeters)
    {
        ValidateContact(contact);
        offsetX = heightMeters = float.NaN;
        var velocity = contact.PreImpactVelocity;
        var timeToPlane = (contact.WicketPlaneZ - contact.Position.Z) / velocity.Z;
        if (!float.IsFinite(timeToPlane) || timeToPlane <= 0f)
            return false;

        var position = contact.Position;
        var verticalVelocity = velocity.Y;
        var groundHeight = contact.PitchSurfaceHeightMeters + contact.BallRadiusMeters;
        if (!contact.HasPitched && TryGetDescendingGroundContactTime(position.Y, verticalVelocity,
                contact.GravityMetersPerSecondSquared, groundHeight, out var bounceTime) && bounceTime < timeToPlane)
        {
            position.X += velocity.X * bounceTime;
            position.Z += velocity.Z * bounceTime;
            var velocityAtBounce = verticalVelocity - contact.GravityMetersPerSecondSquared * bounceTime;
            verticalVelocity = -velocityAtBounce * contact.PitchBounceRestitution;
            var remainingTime = timeToPlane - bounceTime;
            heightMeters = groundHeight + verticalVelocity * remainingTime -
                0.5f * contact.GravityMetersPerSecondSquared * remainingTime * remainingTime;
            offsetX = position.X + velocity.X * remainingTime;
            return float.IsFinite(offsetX) && float.IsFinite(heightMeters);
        }

        offsetX = position.X + velocity.X * timeToPlane;
        heightMeters = position.Y + verticalVelocity * timeToPlane -
            0.5f * contact.GravityMetersPerSecondSquared * timeToPlane * timeToPlane;
        return float.IsFinite(offsetX) && float.IsFinite(heightMeters);
    }

    private static bool TryGetDescendingGroundContactTime(float y, float verticalVelocity, float gravity, float groundHeight, out float time)
    {
        var heightAboveGround = y - groundHeight;
        var discriminant = verticalVelocity * verticalVelocity + 2f * gravity * heightAboveGround;
        if (heightAboveGround <= 0f || discriminant < 0f)
        {
            time = 0f;
            return false;
        }
        time = (verticalVelocity + MathF.Sqrt(discriminant)) / gravity;
        return time > 0f && float.IsFinite(time);
    }

    private static void Validate(LbwContact contact, float umpiresCallProbability)
    {
        ValidateContact(contact);
        if (!float.IsFinite(umpiresCallProbability) || umpiresCallProbability is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(umpiresCallProbability));
    }

    private static void ValidateContact(LbwContact contact)
    {
        if (!IsFinite(contact.Position) || !IsFinite(contact.PreImpactVelocity) || MathF.Abs(contact.PreImpactVelocity.Z) < 0.001f)
            throw new ArgumentException("LBW projection requires finite position and velocity toward a wicket.", nameof(contact));
        if (!float.IsFinite(contact.BallRadiusMeters) || contact.BallRadiusMeters is < 0.01f or > 0.2f ||
            !float.IsFinite(contact.PitchSurfaceHeightMeters) ||
            !float.IsFinite(contact.GravityMetersPerSecondSquared) || contact.GravityMetersPerSecondSquared <= 0f ||
            !float.IsFinite(contact.PitchBounceRestitution) || contact.PitchBounceRestitution is < 0f or > 1f ||
            !float.IsFinite(contact.WicketPlaneZ))
            throw new ArgumentOutOfRangeException(nameof(contact), "LBW contact physics values are invalid.");
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
