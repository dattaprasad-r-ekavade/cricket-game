using System.Numerics;

namespace SuperCricket.Simulation.Fielding;

public readonly record struct RunOutResolution(bool IsOut, DismissedEnd DismissedEnd, bool SwapEnds, bool NearRunOut);

/// <summary>Chooses the threatened wicket and resolves ground ownership when the bails are broken.</summary>
public static class RunOutModel
{
    public const float NearRunOutDistanceMeters = 2f;
    public const float PoppingCreaseOffsetMeters = 1.22f;
    public static WicketEnd ChooseThreatenedEnd(Vector3 fielderPosition)
    {
        if (!IsFinite(fielderPosition)) throw new ArgumentOutOfRangeException(nameof(fielderPosition));
        var near = new Vector3(0f, fielderPosition.Y, CricketPitchGeometry.NearWicketZ);
        var far = new Vector3(0f, fielderPosition.Y, CricketPitchGeometry.FarWicketZ);
        return Vector3.DistanceSquared(fielderPosition, near) <= Vector3.DistanceSquared(fielderPosition, far)
            ? WicketEnd.Near
            : WicketEnd.Far;
    }

    public static bool IsGroundedBehindCrease(Vector3 batOrFootPosition, WicketEnd end, bool isGrounded)
    {
        if (!IsFinite(batOrFootPosition)) throw new ArgumentOutOfRangeException(nameof(batOrFootPosition));
        if (!Enum.IsDefined(end)) throw new ArgumentOutOfRangeException(nameof(end));
        var creaseZ = end == WicketEnd.Near
            ? CricketPitchGeometry.NearWicketZ + PoppingCreaseOffsetMeters
            : CricketPitchGeometry.FarWicketZ - PoppingCreaseOffsetMeters;
        return isGrounded && (end == WicketEnd.Near
            ? batOrFootPosition.Z >= creaseZ
            : batOrFootPosition.Z <= creaseZ);
    }

    public static RunOutResolution ResolveAtBrokenWicket(
        BetweenWicketsState runners,
        WicketEnd end,
        bool wicketBroken,
        float closestRunnerDistanceToCreaseMeters)
    {
        ArgumentNullException.ThrowIfNull(runners);
        if (!Enum.IsDefined(end)) throw new ArgumentOutOfRangeException(nameof(end));
        if (!float.IsFinite(closestRunnerDistanceToCreaseMeters) || closestRunnerDistanceToCreaseMeters < 0f)
            throw new ArgumentOutOfRangeException(nameof(closestRunnerDistanceToCreaseMeters));
        if (wicketBroken && closestRunnerDistanceToCreaseMeters <= PoppingCreaseOffsetMeters)
            return new RunOutResolution(false, DismissedEnd.Striker, false,
                closestRunnerDistanceToCreaseMeters <= NearRunOutDistanceMeters);
        if (runners.TryResolveRunOut(end, wicketBroken, out var resolution))
            return new RunOutResolution(true, resolution.DismissedEnd, resolution.SwapEnds, false);
        var nearMiss = wicketBroken && closestRunnerDistanceToCreaseMeters <= NearRunOutDistanceMeters;
        return new RunOutResolution(false, DismissedEnd.Striker, false, nearMiss);
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
