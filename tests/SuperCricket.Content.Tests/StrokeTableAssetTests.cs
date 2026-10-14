using SuperCricket.Content;
using SuperCricket.Simulation;

namespace SuperCricket.Content.Tests;

public sealed class StrokeTableAssetTests
{
    private static string StrokeTablePath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "../../../../../assets/batting/strokes.json"));

    [Fact]
    public void Load_ParsesAuthoredTableAndCoversAllBaseCombinations()
    {
        var table = StrokeTableAsset.Load(StrokeTablePath);

        Assert.Equal(40, table.Strokes.Count);
        Assert.Empty(table.Validate());
        Assert.All(table.Strokes, stroke =>
        {
            Assert.Contains(TimingZone.Start, stroke.Zones.Keys);
            Assert.Contains(TimingZone.Early, stroke.Zones.Keys);
            Assert.Contains(TimingZone.Ideal, stroke.Zones.Keys);
            Assert.Contains(TimingZone.Late, stroke.Zones.Keys);
            Assert.Contains(TimingZone.End, stroke.Zones.Keys);
        });
        foreach (var direction in Enum.GetValues<ShotDirection8>())
        foreach (var intent in new[] { StrokeIntent.Defend, StrokeIntent.Attack })
        foreach (var foot in new[] { StrokeFoot.Front, StrokeFoot.Back })
            Assert.Contains(table.Strokes, stroke => stroke.Direction == direction && stroke.Intent == intent && stroke.Foot == foot);

        var offDrive = table.Strokes.Single(stroke => stroke.Direction == ShotDirection8.OffDrive && stroke.Intent == StrokeIntent.Attack && stroke.Foot == StrokeFoot.Front && !stroke.Loft);
        Assert.True(offDrive.Zones[TimingZone.Early].YawDegrees > offDrive.Zones[TimingZone.Ideal].YawDegrees);
        Assert.True(offDrive.Zones[TimingZone.Late].YawDegrees > offDrive.Zones[TimingZone.Ideal].YawDegrees);
        var legFine = table.Strokes.Single(stroke => stroke.Direction == ShotDirection8.LegFine && stroke.Intent == StrokeIntent.Attack && stroke.Foot == StrokeFoot.Front && !stroke.Loft);
        Assert.True(legFine.Zones[TimingZone.Early].YawDegrees > legFine.Zones[TimingZone.Ideal].YawDegrees);
        Assert.True(legFine.Zones[TimingZone.Late].YawDegrees < legFine.Zones[TimingZone.Ideal].YawDegrees);
    }

    [Fact]
    public void Mirror_NegatesYawWithoutChangingTheSourceTable()
    {
        var table = StrokeTableAsset.Load(StrokeTablePath);

        var mirrored = table.Mirror();

        Assert.Equal(table.Strokes.Count, mirrored.Strokes.Count);
        for (var index = 0; index < table.Strokes.Count; index++)
        foreach (var zone in table.Strokes[index].Zones.Keys)
        {
            Assert.Equal(-table.Strokes[index].Zones[zone].YawDegrees, mirrored.Strokes[index].Zones[zone].YawDegrees);
            Assert.Equal(table.Strokes[index].Zones[zone].ElevationDegrees, mirrored.Strokes[index].Zones[zone].ElevationDegrees);
        }
        Assert.Empty(table.Validate());
        Assert.Empty(mirrored.Validate());
    }

    [Fact]
    public void Validate_RejectsMissingZonesAndGlobalBeatenZones()
    {
        ExpectError(table => table.Strokes[0].Zones.Remove(TimingZone.Start), "missing its Start zone");
        ExpectError(table => table.Strokes[0].Zones.Add(TimingZone.TooEarly, table.Strokes[0].Zones[TimingZone.Start]), "leave TooEarly and TooLate");
    }

    [Fact]
    public void Validate_RejectsInvalidZoneRanges()
    {
        ExpectError(table => table.Strokes[0].Zones[TimingZone.Ideal] = table.Strokes[0].Zones[TimingZone.Ideal] with { YawSpreadDegrees = 46 }, "yaw spread");
        ExpectError(table => table.Strokes[0].Zones[TimingZone.Ideal] = table.Strokes[0].Zones[TimingZone.Ideal] with { ElevationDegrees = 71 }, "elevation");
        ExpectError(table => table.Strokes[0].Zones[TimingZone.Ideal] = table.Strokes[0].Zones[TimingZone.Ideal] with { MinSpeedMetersPerSecond = 9 }, "min <= max");
        ExpectError(table => table.Strokes[0].Zones[TimingZone.Ideal] = table.Strokes[0].Zones[TimingZone.Ideal] with { YawDegrees = float.NaN }, "yaw must be finite");
        ExpectError(table => table.Strokes[0].Zones[TimingZone.Ideal] = table.Strokes[0].Zones[TimingZone.Ideal] with { EdgeChance = -0.1f }, "edge chance");
        ExpectError(table => table.Strokes[0].Zones[TimingZone.Late] = table.Strokes[0].Zones[TimingZone.Late] with { MaxSpeedMetersPerSecond = table.Strokes[0].Zones[TimingZone.Ideal].MaxSpeedMetersPerSecond + 1 }, "Ideal max speed");
    }

    [Fact]
    public void Validate_RejectsInvalidStrokeMetadataAndSuitFilters()
    {
        ExpectError(table => table.Version = 2, "version");
        ExpectError(table => table.Strokes[0].Id = "", "unique and non-empty");
        ExpectError(table => table.Strokes[1].Id = table.Strokes[0].Id, "unique and non-empty");
        ExpectError(table => table.Strokes[0].Direction = (ShotDirection8)99, "unknown direction");
        ExpectError(table => table.Strokes[0].Intent = (StrokeIntent)99, "unknown intent");
        ExpectError(table => table.Strokes[0].Foot = StrokeFoot.Auto, "Front or Back");
        ExpectError(table => table.Strokes[0].Aggression = 3, "aggression");
        ExpectError(table => table.Strokes[0].Difficulty = float.NaN, "difficulty");
        ExpectError(table => table.Strokes[0].Reward = -0.1f, "reward");
        ExpectError(table => table.Strokes[0].AnimationClip = "missing-clip", "not in the batter animation contract");
        ExpectError(table => table.Strokes[0].Suits.Lengths.Clear(), "at least one length and line");
        ExpectError(table => table.Strokes[0].Suits.Lines[0] = (BallLine)99, "unknown ball length or line");
    }

    [Fact]
    public void Validate_WarnsAboutUnusualLineAndDirectionPairsWithoutFailing()
    {
        var table = StrokeTableAsset.Load(StrokeTablePath);
        var stroke = table.Strokes.First(candidate => candidate.Direction == ShotDirection8.Cover);
        stroke.Suits.Lines.Add(BallLine.Leg);

        var messages = table.Validate();

        Assert.Contains(messages, StrokeTableAsset.IsWarning);
        Assert.DoesNotContain(messages, message => !StrokeTableAsset.IsWarning(message));
    }

    [Fact]
    public void Validate_ReportsAnUnreadableAnimationContract()
    {
        var table = StrokeTableAsset.Load(StrokeTablePath);

        var messages = table.Validate(Path.Combine(Path.GetTempPath(), "missing-animation-contract.json"));

        Assert.Contains(messages, message => message.Contains("Could not read batter animation contract", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RequiresFortyStrokesAndEveryDirectionIntentFootCombination()
    {
        ExpectError(table => table.Strokes.RemoveAt(0), "At least 40 strokes");
        ExpectError(table => table.Strokes.RemoveAll(stroke => stroke.Direction == ShotDirection8.Cover &&
            stroke.Intent == StrokeIntent.Defend && stroke.Foot == StrokeFoot.Back), "No stroke covers Cover/Defend/Back");
    }

    private static void ExpectError(Action<StrokeTableAsset> change, string expectedMessage)
    {
        var table = StrokeTableAsset.Load(StrokeTablePath);
        change(table);

        var errors = table.Validate().Where(message => !StrokeTableAsset.IsWarning(message)).ToArray();

        Assert.Contains(errors, error => error.Contains(expectedMessage, StringComparison.Ordinal));
    }
}
