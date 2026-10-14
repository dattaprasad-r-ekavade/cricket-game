using System.Text.Json.Nodes;
using SuperCricket.Content;

namespace SuperCricket.Content.Tests;

public sealed class TeamRosterMigrationTests
{
    private static string CoastalRosterPath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "../../../../../assets/teams/coastal-xi.json"));

    [Fact]
    public void Load_MigratesVersionOneLegacyRatings()
    {
        var legacy = JsonNode.Parse(File.ReadAllText(CoastalRosterPath))!.AsObject();
        legacy["version"] = 1;
        foreach (var player in legacy["players"]!.AsArray())
        {
            var data = player!.AsObject();
            data.Remove("battingRatings");
            data.Remove("bowlingAttributes");
            data.Remove("fieldingAttributes");
            data.Remove("battingStyle");
            data.Remove("handedness");
        }

        var path = Path.Combine(Path.GetTempPath(), $"roster-v1-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, legacy.ToJsonString());
            var migrated = TeamRosterAsset.Load(path);

            Assert.Equal(2, migrated.Version);
            Assert.Equal(76, migrated.Players[0].BattingRatings.FrontFoot);
            Assert.Equal(64, migrated.Players[0].BattingRatings.Lofted);
            Assert.Equal(40, migrated.Players[3].BowlingAttributes.Accuracy);
            Assert.Equal(94, migrated.Players[0].FieldingAttributes.Catching);
            Assert.Equal(BatterStyle.Anchor, migrated.Players[0].BattingStyle);
            Assert.Equal(BatterHandedness.Left, migrated.Players[2].Handedness);
            Assert.DoesNotContain(migrated.Validate(), message => !TeamRosterAsset.IsWarning(message));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FictionalRosters_LoadWithVersionTwoRatingsInsideBands()
    {
        foreach (var path in Directory.GetFiles(Path.GetDirectoryName(CoastalRosterPath)!, "*.json"))
        {
            var roster = TeamRosterAsset.Load(path);
            Assert.Equal(2, roster.Version);
            Assert.DoesNotContain(roster.Validate(), TeamRosterAsset.IsWarning);
        }
    }

    [Fact]
    public void Validate_WarnsForRatingBandsAndBattingSpread()
    {
        var roster = TeamRosterAsset.CreatePlaceholder("Rating Warning XI");
        var player = roster.Players[1];
        player.BattingRatings.FrontFoot = 99;
        player.BattingRatings.BackFoot = 40;
        player.FieldingAttributes.Speed = 50;

        var warnings = roster.Validate().Where(TeamRosterAsset.IsWarning).ToArray();

        Assert.Contains(warnings, warning => warning.Contains("outside the recommended 40–98 band", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Contains("spread", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Contains("fielding ratings should be 60–90", StringComparison.Ordinal));
    }
}
