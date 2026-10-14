using SuperCricket.Content;

namespace SuperCricket.Simulation;

/// <summary>Selects the authored stroke that best matches a request and the delivered ball.</summary>
public static class StrokeSelector
{
    public static StrokeDefinition Select(StrokeTableAsset table, StrokeRequest request, BallCategory ball)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Strokes is null || table.Strokes.Count == 0)
            throw new ArgumentException("Stroke table must contain at least one stroke.", nameof(table));
        if (!Enum.IsDefined(request.Direction) || !Enum.IsDefined(request.Intent) || !Enum.IsDefined(request.Foot))
            throw new ArgumentOutOfRangeException(nameof(request));
        if (request.Intent == StrokeIntent.Leave)
            throw new ArgumentException("A leave request does not select a batting stroke.", nameof(request));
        if (!Enum.IsDefined(ball.Length) || !Enum.IsDefined(ball.Line) ||
            !float.IsFinite(ball.ArrivalHeightMeters) || ball.ArrivalHeightMeters < 0f)
            throw new ArgumentOutOfRangeException(nameof(ball));

        var foot = request.Foot == StrokeFoot.Auto
            ? BallCategoryClassifier.ChooseFoot(ball, request.Direction)
            : request.Foot;
        var contextMatches = table.Strokes.Where(stroke => stroke is not null &&
            stroke.Intent == request.Intent && stroke.Loft == request.Loft && stroke.Foot == foot).ToArray();
        if (contextMatches.Length == 0)
            throw new InvalidOperationException($"No strokes match {request.Intent}, loft={request.Loft}, foot={foot}.");

        var exactMatches = contextMatches.Where(stroke => stroke.Direction == request.Direction).ToArray();
        if (exactMatches.Length > 0)
            return exactMatches
                .OrderByDescending(stroke => SuitsBall(stroke, ball))
                .ThenBy(stroke => stroke.Id, StringComparer.Ordinal)
                .First();

        var sameSideMatches = contextMatches
            .Where(stroke => IsSameSide(request.Direction, stroke.Direction))
            .OrderByDescending(stroke => SuitsBall(stroke, ball))
            .ThenBy(stroke => DirectionDistance(request.Direction, stroke.Direction))
            .ThenBy(stroke => stroke.Id, StringComparer.Ordinal)
            .ToArray();
        if (sameSideMatches.Length == 0)
            throw new InvalidOperationException($"No {request.Intent} stroke with loft={request.Loft} and foot={foot} exists on the {GetSideName(request.Direction)} side.");
        return sameSideMatches[0];
    }

    private static bool SuitsBall(StrokeDefinition stroke, BallCategory ball) =>
        stroke.Suits is not null && stroke.Suits.Lengths is not null && stroke.Suits.Lines is not null &&
        stroke.Suits.Lengths.Contains(ball.Length) && stroke.Suits.Lines.Contains(ball.Line);

    private static bool IsSameSide(ShotDirection8 requested, ShotDirection8 candidate)
    {
        var requestedSide = Side(requested);
        return requestedSide == 0 || Side(candidate) == requestedSide;
    }

    private static int Side(ShotDirection8 direction) => direction switch
    {
        ShotDirection8.OffDrive or ShotDirection8.Cover or ShotDirection8.Square or ShotDirection8.Fine => 1,
        ShotDirection8.LegFine or ShotDirection8.LegSquare or ShotDirection8.MidWicket => -1,
        ShotDirection8.Straight => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    private static string GetSideName(ShotDirection8 direction) => Side(direction) switch
    {
        1 => "off",
        -1 => "leg",
        _ => "either"
    };

    private static float DirectionDistance(ShotDirection8 first, ShotDirection8 second)
    {
        var difference = MathF.Abs(DirectionYaw(first) - DirectionYaw(second)) % 360f;
        return MathF.Min(difference, 360f - difference);
    }

    private static float DirectionYaw(ShotDirection8 direction) => direction switch
    {
        ShotDirection8.Straight => 0f,
        ShotDirection8.OffDrive => 22f,
        ShotDirection8.Cover => 48f,
        ShotDirection8.Square => 78f,
        ShotDirection8.Fine => 108f,
        ShotDirection8.LegFine => -108f,
        ShotDirection8.LegSquare => -78f,
        ShotDirection8.MidWicket => -25f,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
}
