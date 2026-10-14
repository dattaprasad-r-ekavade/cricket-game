namespace SuperCricket.Simulation.Batting;

/// <summary>Maps a relative horizontal/forward aim vector to the nearest authored eight-way direction.</summary>
public static class StrokeDirectionModel
{
    private static readonly (ShotDirection8 Direction, float YawDegrees)[] Directions =
    [
        (ShotDirection8.Straight, 0f),
        (ShotDirection8.OffDrive, 22f),
        (ShotDirection8.Cover, 48f),
        (ShotDirection8.Square, 78f),
        (ShotDirection8.Fine, 108f),
        (ShotDirection8.LegFine, -108f),
        (ShotDirection8.LegSquare, -78f),
        (ShotDirection8.MidWicket, -25f)
    ];

    public static ShotDirection8 FromAim(float horizontalAim, float forwardAim)
    {
        if (!float.IsFinite(horizontalAim) || horizontalAim is < -1f or > 1f ||
            !float.IsFinite(forwardAim) || forwardAim is < -1f or > 1f ||
            (MathF.Abs(horizontalAim) < 0.001f && MathF.Abs(forwardAim) < 0.001f))
            throw new ArgumentOutOfRangeException(nameof(horizontalAim), "Aim must be a finite, non-zero vector within [-1, 1].");
        if (MathF.Abs(horizontalAim) < 0.001f)
            return ShotDirection8.Straight;

        var yaw = MathF.Atan2(horizontalAim, forwardAim) * (180f / MathF.PI);
        return Directions
            .OrderBy(candidate => AngularDistance(yaw, candidate.YawDegrees))
            .ThenBy(candidate => candidate.Direction)
            .First()
            .Direction;
    }

    private static float AngularDistance(float first, float second)
    {
        var distance = MathF.Abs(first - second) % 360f;
        return MathF.Min(distance, 360f - distance);
    }
}
