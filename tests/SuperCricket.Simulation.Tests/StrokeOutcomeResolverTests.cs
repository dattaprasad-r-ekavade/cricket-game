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
                Assert.InRange(speed, ideal.MinSpeedMetersPerSecond * (resolution.Edged ? 0.5f : 1f), ideal.MaxSpeedMetersPerSecond);
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
}
