using SuperCricket.Content;

namespace SuperCricket.Simulation.Tests;

public sealed class AutomaticFootworkCalibrationTests
{
    private static readonly Lazy<Assets> SharedAssets = new(() => new Assets(
        PlayerAsset.Load(TestAssets.Asset("characters", "practice-batter-humanoid.glb")),
        PlayerAsset.Load(TestAssets.Asset("characters", "practice-bowler-humanoid.glb")),
        BattingShotSet.Load(TestAssets.Asset("batting", "shots.json"))));

    [Fact]
    public void RookieSeed491StepsToTheFirstStanceThatCanContactTheBall()
    {
        var assets = SharedAssets.Value;
        var stock = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        var variation = BowlingDecisionModel.ChooseDelivery(
            stock, 91, 65, new BowlingSituation(0, 1, 0, 0, null), 491, CpuDifficulty.Rookie).Delivery;
        var delivery = DeliveryPaceModel.ApplyHumanBattingPace(variation, CpuDifficulty.Rookie, isFirstMatch: false);

        var centered = BattingPracticeAnalyzer.CalibrateTiming(
            assets.Batter, assets.Bowler, assets.Shots, delivery, cancellationToken: TestContext.Current.CancellationToken);
        var automatic = BattingPracticeAnalyzer.CalibrateReachableFootwork(
            assets.Batter, assets.Bowler, assets.Shots, delivery, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(centered.Shots);
        Assert.Equal(BatterFootwork.StepDistanceMeters, automatic.FootworkOffsetMeters);
        Assert.Equal(new[] { "defence", "drive", "loft" },
            automatic.TimingProfile.Shots.Select(shot => shot.ShotName).OrderBy(name => name).ToArray());
        foreach (var shot in automatic.TimingProfile.Shots)
        {
            var trajectory = BattingPracticeAnalyzer.AnalyzeShotTrajectory(
                assets.Batter, assets.Bowler, assets.Shots, shot.ShotName, delivery,
                shot.IdealInputDelaySeconds, automatic.FootworkOffsetMeters);
            Assert.NotNull(trajectory.Sample.ContactQuality);
            Assert.True(trajectory.Sample.ContactQuality >= 0.8f);
        }
    }

    [Theory]
    [InlineData(1729, CpuDifficulty.Standard, 0f)]
    [InlineData(129, CpuDifficulty.Standard, 0f)]
    [InlineData(25, CpuDifficulty.Standard, 0f)]
    [InlineData(42, CpuDifficulty.Standard, 0f)]
    [InlineData(54, CpuDifficulty.Standard, 0.45f)]
    [InlineData(62, CpuDifficulty.Standard, 0f)]
    [InlineData(69, CpuDifficulty.Standard, 0f)]
    public void CpuVariationsChooseTheNearestContactableStance(int seed, CpuDifficulty difficulty, float expectedOffset)
    {
        var assets = SharedAssets.Value;
        var stock = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        var variation = BowlingDecisionModel.ChooseDelivery(
            stock, 91, 65, new BowlingSituation(0, 1, 0, 0, null), seed, difficulty).Delivery;

        var automatic = BattingPracticeAnalyzer.CalibrateReachableFootwork(
            assets.Batter, assets.Bowler, assets.Shots, variation, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expectedOffset, automatic.FootworkOffsetMeters);
        Assert.NotEmpty(automatic.TimingProfile.Shots);
    }

    [Fact]
    public void SearchStopsAtTheNearestReachableStepAndIsRepeatable()
    {
        var assets = SharedAssets.Value;
        var stock = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        var variation = BowlingDecisionModel.ChooseDelivery(
            stock, 91, 65, new BowlingSituation(0, 1, 0, 0, null), 491, CpuDifficulty.Rookie).Delivery;
        var first = BattingPracticeAnalyzer.CalibrateReachableFootwork(
            assets.Batter, assets.Bowler, assets.Shots, variation, cancellationToken: TestContext.Current.CancellationToken);
        var second = BattingPracticeAnalyzer.CalibrateReachableFootwork(
            assets.Batter, assets.Bowler, assets.Shots, variation, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(first.FootworkOffsetMeters, second.FootworkOffsetMeters);
        Assert.Equal(first.TimingProfile.Shots.Select(shot => (shot.ShotName, shot.IdealInputDelaySeconds)),
            second.TimingProfile.Shots.Select(shot => (shot.ShotName, shot.IdealInputDelaySeconds)));
    }

    [Fact]
    public void OutOfReachDeliveryDoesNotManufactureAPositionOrTimingCue()
    {
        var assets = SharedAssets.Value;
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "wide-pace.json"));

        var automatic = BattingPracticeAnalyzer.CalibrateReachableFootwork(
            assets.Batter, assets.Bowler, assets.Shots, delivery, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0f, automatic.FootworkOffsetMeters);
        Assert.Empty(automatic.TimingProfile.Shots);
    }

    [Fact]
    public void PreCancelledAutomaticFootworkSearchStopsBeforeAnalyzingTheDelivery()
    {
        var assets = SharedAssets.Value;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => BattingPracticeAnalyzer.CalibrateReachableFootwork(
            assets.Batter, assets.Bowler, assets.Shots,
            DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json")),
            cancellationToken: cancellation.Token));
    }

    private sealed record Assets(PlayerAsset Batter, PlayerAsset Bowler, BattingShotSet Shots);
}
