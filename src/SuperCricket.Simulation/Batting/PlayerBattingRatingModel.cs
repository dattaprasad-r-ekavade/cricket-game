using SuperCricket.Content;

namespace SuperCricket.Simulation;

/// <summary>Maps player identity ratings onto a particular authored stroke.</summary>
public static class PlayerBattingRatingModel
{
    public static float GetStrokeRating(TeamPlayerData player, StrokeFoot foot, ShotDirection8 direction, bool loft)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!Enum.IsDefined(foot) || foot == StrokeFoot.Auto)
            throw new ArgumentOutOfRangeException(nameof(foot), "Resolve automatic footwork before reading a stroke rating.");
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));

        var batting = player.BattingRatings
            ?? throw new ArgumentException("Player batting ratings are required.", nameof(player));
        var footRating = foot == StrokeFoot.Front ? batting.FrontFoot : batting.BackFoot;
        var sideRating = direction switch
        {
            ShotDirection8.OffDrive or ShotDirection8.Cover or ShotDirection8.Square or ShotDirection8.Fine => batting.OffSide,
            ShotDirection8.LegFine or ShotDirection8.LegSquare or ShotDirection8.MidWicket => batting.LegSide,
            ShotDirection8.Straight => (batting.OffSide + batting.LegSide) / 2f,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
        return loft
            ? (footRating + sideRating + batting.Lofted) / 3f
            : (footRating + sideRating) / 2f;
    }
}
