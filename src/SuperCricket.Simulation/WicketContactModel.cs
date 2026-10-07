using System.Numerics;

namespace SuperCricket.Simulation;

/// <summary>Checks whether a released return ball reaches the prototype's stump target.</summary>
public static class WicketContactModel
{
    public const float WicketHalfWidthMeters = 0.12f;
    public const float WicketHeightMeters = 0.71f;

    public static bool IsBroken(Vector3 ballPosition, float wicketZ, float ballRadiusMeters,
        bool fielderSecuredBall, bool ballReleased)
    {
        if (!float.IsFinite(ballPosition.X) || !float.IsFinite(ballPosition.Y) || !float.IsFinite(ballPosition.Z) ||
            !float.IsFinite(wicketZ) || !float.IsFinite(ballRadiusMeters) || ballRadiusMeters <= 0f)
            throw new ArgumentException("Wicket contact requires a finite position and positive ball radius.");
        return fielderSecuredBall && ballReleased &&
            MathF.Abs(ballPosition.X) <= WicketHalfWidthMeters + ballRadiusMeters &&
            ballPosition.Y >= 0f && ballPosition.Y <= WicketHeightMeters + ballRadiusMeters &&
            MathF.Abs(ballPosition.Z - wicketZ) <= ballRadiusMeters;
    }
}
