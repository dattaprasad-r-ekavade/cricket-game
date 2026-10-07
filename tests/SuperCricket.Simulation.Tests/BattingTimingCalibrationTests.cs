using SuperCricket.Content;

namespace SuperCricket.Simulation.Tests;

public sealed class BattingTimingCalibrationTests
{
    private static readonly Lazy<Assets> SharedAssets = new(() => new Assets(
        PlayerAsset.Load(TestAssets.Asset("characters", "practice-batter-humanoid.glb")),
        PlayerAsset.Load(TestAssets.Asset("characters", "practice-bowler-humanoid.glb")),
        BattingShotSet.Load(TestAssets.Asset("batting", "shots.json"))));

    [Theory]
    [InlineData("defence", 0.15f, 0.55f)]
    [InlineData("drive", 0.225f, 0.525f)]
    [InlineData("loft", 0.225f, 0.525f)]
    public void SlowerDeliveryTargetsRealContactInsteadOfStockTiming(
        string shotName, float stockDelay, float slowerDelay)
    {
        var assets = SharedAssets.Value;
        var stock = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        var delivery = DeliveryPaceModel.ApplyHumanBattingPace(stock, CpuDifficulty.Rookie, isFirstMatch: false);
        var profile = BattingPracticeAnalyzer.CalibrateTiming(assets.Batter, assets.Bowler, assets.Shots, delivery, cancellationToken: TestContext.Current.CancellationToken);
        var ideal = profile.FindIdealInputDelaySeconds(shotName);

        Assert.NotNull(ideal);
        Assert.InRange(MathF.Abs(ideal.Value - slowerDelay), 0f, 0.001f);
        var trajectory = BattingPracticeAnalyzer.AnalyzeShotTrajectory(
            assets.Batter, assets.Bowler, assets.Shots, shotName, delivery, ideal.Value, 0f);
        Assert.True(trajectory.Sample.ContactQuality > 0.95f);
        Assert.Equal(BattingTimingBand.Perfect, BattingTimingFeedbackModel.Assess(
            ideal.Value, ideal.Value, 0.075f).Band);
        Assert.Equal(BattingTimingCueState.SwingNow, BattingTimingFeedbackModel.EvaluateCue(
            ideal.Value, ideal.Value, 0.075f).State);
        Assert.Equal(BattingTimingBand.Late, BattingTimingFeedbackModel.Assess(
            ideal.Value, stockDelay, 0.075f).Band);

        if (shotName == "drive")
            Assert.Null(BattingPracticeAnalyzer.AnalyzeShotTrajectory(
                assets.Batter, assets.Bowler, assets.Shots, shotName, delivery, stockDelay, 0f).Sample.ContactQuality);
        Assert.Equal(-34f, stock.ReleaseVelocity.Z);
    }

    [Fact]
    public void UnchangedStockDeliveryStillMatchesAuthoredCalibration()
    {
        var assets = SharedAssets.Value;
        var stock = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        var authored = BattingTimingCalibrationAsset.Load(TestAssets.Asset("batting", "timing-calibration.json"));
        var profile = BattingPracticeAnalyzer.CalibrateTiming(assets.Batter, assets.Bowler, assets.Shots, stock, cancellationToken: TestContext.Current.CancellationToken);

        foreach (var shot in assets.Shots.Shots)
            Assert.InRange(MathF.Abs(profile.FindIdealInputDelaySeconds(shot.Name)!.Value -
                authored.FindIdealInputDelaySeconds(stock.Name, shot.Name)!.Value), 0f, 0.001f);
    }

    [Theory]
    [InlineData(1729, CpuDifficulty.Standard, true, true)]
    [InlineData(491, CpuDifficulty.Rookie, false, false)]
    [InlineData(129, CpuDifficulty.Standard, false, true)]
    public void PreparedCpuVariationUsesItsMeasuredTrajectory(
        int seed, CpuDifficulty difficulty, bool firstMatch, bool reachableAtCenter)
    {
        var assets = SharedAssets.Value;
        var stock = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        var variation = BowlingDecisionModel.ChooseDelivery(
            stock, 91, 65, new BowlingSituation(0, 1, 0, 0, null), seed, difficulty).Delivery;
        var delivery = DeliveryPaceModel.ApplyHumanBattingPace(variation, difficulty, firstMatch);
        var profile = BattingPracticeAnalyzer.CalibrateTiming(assets.Batter, assets.Bowler, assets.Shots, delivery, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(delivery.Name, profile.DeliveryName);
        if (reachableAtCenter)
            Assert.NotEmpty(profile.Shots);
        else
        {
            Assert.Empty(profile.Shots);
            var stepped = BattingPracticeAnalyzer.CalibrateTiming(
                assets.Batter, assets.Bowler, assets.Shots, delivery, footworkOffsetMeters: 0.45f,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotEmpty(stepped.Shots);
        }
        foreach (var shot in profile.Shots)
        {
            var trajectory = BattingPracticeAnalyzer.AnalyzeShotTrajectory(
                assets.Batter, assets.Bowler, assets.Shots, shot.ShotName, delivery, shot.IdealInputDelaySeconds, 0f);
            Assert.NotNull(trajectory.Sample.ContactQuality);
            Assert.Equal(BattingTimingBand.Perfect, BattingTimingFeedbackModel.Assess(
                shot.IdealInputDelaySeconds, profile.FindIdealInputDelaySeconds(shot.ShotName)!.Value, 0.075f).Band);
        }
    }

    [Fact]
    public void UnreachableDeliveryDoesNotReuseItsStockNameToOfferTiming()
    {
        var assets = SharedAssets.Value;
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        delivery.ReleasePosition.X = 20f;

        var profile = BattingPracticeAnalyzer.CalibrateTiming(assets.Batter, assets.Bowler, assets.Shots, delivery, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(profile.Shots);
        Assert.Null(profile.FindIdealInputDelaySeconds("drive"));
    }

    [Fact]
    public void WideDeliveryOnlyOffersTimingAtAReachableFootworkPosition()
    {
        var assets = SharedAssets.Value;
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "wide-pace.json"));
        var centered = BattingPracticeAnalyzer.CalibrateTiming(assets.Batter, assets.Bowler, assets.Shots, delivery, cancellationToken: TestContext.Current.CancellationToken);
        var stepped = BattingPracticeAnalyzer.CalibrateTiming(
            assets.Batter, assets.Bowler, assets.Shots, delivery, footworkOffsetMeters: 2.25f, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(centered.Shots);
        Assert.InRange(stepped.FindIdealInputDelaySeconds("drive")!.Value, 0.324f, 0.326f);
    }

    [Fact]
    public void SupersededCalibrationCanBeCancelled()
    {
        var assets = SharedAssets.Value;
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => BattingPracticeAnalyzer.CalibrateTiming(
            assets.Batter, assets.Bowler, assets.Shots, delivery, cancellationToken: cancellation.Token));
    }

    private sealed record Assets(PlayerAsset Batter, PlayerAsset Bowler, BattingShotSet Shots);
}
