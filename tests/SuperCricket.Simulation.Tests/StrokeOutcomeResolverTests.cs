using System.Numerics;
using SuperCricket.Content;
using SuperCricket.Simulation.Batting;

namespace SuperCricket.Simulation.Tests;

public sealed class StrokeOutcomeResolverTests
{
    private static StrokeTableAsset LoadTable() => StrokeTableAsset.Load(TestAssets.Asset("batting", "strokes.json"));

    [Fact]
    public void SameRequestAndSeedProduceIdenticalResolution()
    {
        var table = LoadTable();
        var request = new StrokeRequest(ShotDirection8.Cover, StrokeIntent.Attack, Loft: false, StrokeFoot.Front);
        var ball = new BallCategory(BallLength.Good, BallLine.OutsideOff, 0.5f);
        var window = TimingWindowModel.Build(0f, 2f, 0.6f);

        var first = StrokeOutcomeResolver.Resolve(table, request, ball, 0f, window, false, false, new Random(15));
        var replay = StrokeOutcomeResolver.Resolve(table, request, ball, 0f, window, false, false, new Random(15));

        Assert.Equal(first, replay);
        Assert.False(first.Missed);
        Assert.Equal(TimingZone.Ideal, first.Zone);
    }

    [Fact]
    public void EveryAuthoredStrokeResolvesIdealInputsAcrossTwoHundredSeeds()
    {
        var table = LoadTable();
        var window = TimingWindowModel.Build(0f, 2f, 0.6f);

        foreach (var stroke in table.Strokes)
        {
            var ball = new BallCategory(stroke.Suits.Lengths[0], stroke.Suits.Lines[0], 0.5f);
            var request = new StrokeRequest(stroke.Direction, stroke.Intent, stroke.Loft, stroke.Foot);
            var ideal = stroke.Zones[TimingZone.Ideal];
            var authoredElevation = ideal.ElevationDegrees;
            for (var seed = 0; seed < 200; seed++)
            {
                var resolution = StrokeOutcomeResolver.Resolve(
                    table, request, ball, 0f, window, leftHanded: false, assist: false, new Random(seed));

                Assert.False(resolution.Missed, $"{stroke.Id}, seed {seed}, zone {resolution.Zone}");
                Assert.Equal(TimingZone.Ideal, resolution.Zone);
                Assert.Equal(stroke.Id, resolution.StrokeId);
                var speed = resolution.OutgoingVelocity.Length();
                Assert.InRange(speed,
                    ideal.MinSpeedMetersPerSecond * (resolution.Edged ? 0.4f : 1f),
                    ideal.MaxSpeedMetersPerSecond * (resolution.Edged ? 0.6f : 1f));
                var elevation = MathF.Atan2(
                    resolution.OutgoingVelocity.Y,
                    new Vector2(resolution.OutgoingVelocity.X, resolution.OutgoingVelocity.Z).Length()) * (180f / MathF.PI);
                if (resolution.Edged)
                    Assert.InRange(elevation, 4.99f, 20.01f);
                else
                    Assert.InRange(elevation, authoredElevation - 0.01f, authoredElevation + 0.01f);
            }
        }
    }

    [Theory]
    [InlineData(2f, TimingZone.TooEarly)]
    [InlineData(-2f, TimingZone.TooLate)]
    public void OutsideWindowProducesBeatenOrMissed(float inputDistance, TimingZone zone)
    {
        var result = StrokeOutcomeResolver.Resolve(
            LoadTable(),
            new StrokeRequest(ShotDirection8.Straight, StrokeIntent.Attack, false, StrokeFoot.Front),
            new BallCategory(BallLength.Good, BallLine.Stumps, 0.5f),
            inputDistance,
            TimingWindowModel.Build(0f, 2f, 0.6f),
            leftHanded: false,
            assist: false,
            new Random(0));

        Assert.True(result.Missed);
        Assert.Equal(zone, result.Zone);
        Assert.Equal(Vector3.Zero, result.OutgoingVelocity);
    }

    [Fact]
    public void LeftHanderMirrorsTheOutgoingYaw()
    {
        var table = LoadTable();
        var request = new StrokeRequest(ShotDirection8.Cover, StrokeIntent.Attack, false, StrokeFoot.Front);
        var ball = new BallCategory(BallLength.Good, BallLine.OutsideOff, 0.5f);
        var window = TimingWindowModel.Build(0f, 2f, 0.6f);

        var right = StrokeOutcomeResolver.Resolve(table, request, ball, 0f, window, false, false, new Random(82));
        var left = StrokeOutcomeResolver.Resolve(table, request, ball, 0f, window, true, false, new Random(82));

        Assert.Equal(right.Edged, left.Edged);
        Assert.Equal(right.OutgoingVelocity.Y, left.OutgoingVelocity.Y, 5);
        Assert.Equal(right.OutgoingVelocity.Z, left.OutgoingVelocity.Z, 5);
        Assert.Equal(-right.OutgoingVelocity.X, left.OutgoingVelocity.X, 5);
    }

    [Fact]
    public void EdgedCoverDriveDeflectsFineBehindTheOffSideAndReplaysDeterministically()
    {
        var table = LoadTable();
        var request = new StrokeRequest(ShotDirection8.Cover, StrokeIntent.Attack, false, StrokeFoot.Front);
        var ball = new BallCategory(BallLength.Good, BallLine.OutsideOff, 0.5f);
        var window = TimingWindowModel.Build(0f, 2f, 0.6f);
        StrokeResolution? edged = null;
        var edgeSeed = 0;

        for (; edgeSeed < 10_000; edgeSeed++)
        {
            var result = StrokeOutcomeResolver.Resolve(table, request, ball, 0f, window, false, false, new Random(edgeSeed));
            if (result.Edged)
            {
                edged = result;
                break;
            }
        }

        Assert.True(edged.HasValue, "No deterministic seed produced the authored cover-drive edge.");
        var resolution = edged.Value;
        var replay = StrokeOutcomeResolver.Resolve(table, request, ball, 0f, window, false, false, new Random(edgeSeed));
        var yawDegrees = MathF.Atan2(resolution.OutgoingVelocity.X, resolution.OutgoingVelocity.Z) * (180f / MathF.PI);
        var ideal = table.Strokes.Single(stroke => stroke.Id == resolution.StrokeId).Zones[TimingZone.Ideal];
        var speed = resolution.OutgoingVelocity.Length();

        Assert.Equal(resolution, replay);
        Assert.True(resolution.Edged);
        Assert.True(resolution.OutgoingVelocity.Z < 0f, "The edge did not travel behind the batter.");
        Assert.True(resolution.OutgoingVelocity.X > 0f, "The right-handed cover edge did not stay on the off side.");
        Assert.InRange(yawDegrees, 154.99f, 180.01f);
        Assert.InRange(speed, ideal.MinSpeedMetersPerSecond * 0.4f, ideal.MaxSpeedMetersPerSecond * 0.6f);
        Assert.InRange(MathF.Atan2(
            resolution.OutgoingVelocity.Y,
            new Vector2(resolution.OutgoingVelocity.X, resolution.OutgoingVelocity.Z).Length()) * (180f / MathF.PI),
            4.99f,
            20.01f);

        var leftHanded = StrokeOutcomeResolver.Resolve(table, request, ball, 0f, window, true, false, new Random(edgeSeed));
        Assert.True(leftHanded.Edged);
        Assert.True(leftHanded.OutgoingVelocity.X < 0f, "The left-handed edge was not mirrored across the pitch.");
        Assert.Equal(resolution.OutgoingVelocity.Y, leftHanded.OutgoingVelocity.Y, 5);
        Assert.Equal(resolution.OutgoingVelocity.Z, leftHanded.OutgoingVelocity.Z, 5);
    }
}
