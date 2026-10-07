using System.Numerics;

namespace SuperCricket.Simulation;

/// <summary>Shared stump bounds for incoming deliveries and released return throws.</summary>
public static class WicketContactModel
{
    public const float WicketHalfWidthMeters = 0.12f;
    public const float WicketHeightMeters = CricketPitchGeometry.WicketHeightMeters;

    public static bool IsBroken(Vector3 ballPosition, float wicketZ, float ballRadiusMeters,
        bool fielderSecuredBall, bool ballReleased)
    {
        var intersects = IntersectsStumps(ballPosition, wicketZ, ballRadiusMeters);
        return fielderSecuredBall && ballReleased && intersects;
    }

    public static bool IntersectsStumps(Vector3 ballPosition, float wicketZ, float ballRadiusMeters)
    {
        if (!float.IsFinite(ballPosition.X) || !float.IsFinite(ballPosition.Y) || !float.IsFinite(ballPosition.Z) ||
            !float.IsFinite(wicketZ) || !float.IsFinite(ballRadiusMeters) || ballRadiusMeters <= 0f)
            throw new ArgumentException("Wicket contact requires a finite position and positive ball radius.");
        return MathF.Abs(ballPosition.X) <= WicketHalfWidthMeters + ballRadiusMeters &&
            ballPosition.Y >= 0f && ballPosition.Y <= WicketHeightMeters + ballRadiusMeters &&
            MathF.Abs(ballPosition.Z - wicketZ) <= ballRadiusMeters;
    }
}
