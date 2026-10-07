using SuperCricket.Content;
using SuperCricket.Simulation;

namespace SuperCricket.Simulation.Tests;

public sealed class DeliveryPaceModelTests
{
    [Theory]
    [InlineData(CpuDifficulty.Rookie, false, 0.82f)]
    [InlineData(CpuDifficulty.Standard, true, 0.82f)]
    [InlineData(CpuDifficulty.Pro, true, 0.82f)]
    [InlineData(CpuDifficulty.Standard, false, 1f)]
    [InlineData(CpuDifficulty.Pro, false, 1f)]
    public void RookieAndFirstMatchUseTheAccessiblePace(
        CpuDifficulty difficulty,
        bool isFirstMatch,
        float expectedMultiplier) =>
        Assert.Equal(expectedMultiplier, DeliveryPaceModel.GetHumanBattingSpeedMultiplier(difficulty, isFirstMatch));

    [Fact]
    public void PaceAdjustmentScalesReleaseVelocityAndPreservesTheAuthoredPreset()
    {
        var source = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        var originalVelocity = source.ReleaseVelocity.ToVector3();

        var adjusted = DeliveryPaceModel.ApplyHumanBattingPace(
            source,
            CpuDifficulty.Standard,
            isFirstMatch: true);

        Assert.Equal(originalVelocity, source.ReleaseVelocity.ToVector3());
        Assert.NotSame(source, adjusted);
        Assert.Equal(source.StartPosition, adjusted.StartPosition);
        Assert.Equal(originalVelocity.Length() * 0.82f, adjusted.ReleaseVelocity.ToVector3().Length(), 0.0001f);
        Assert.Equal(source.GravityMetersPerSecondSquared, adjusted.GravityMetersPerSecondSquared);
        Assert.Empty(adjusted.Validate());
    }

    [Fact]
    public void StandardPaceAfterTheFirstMatchRetainsTheOriginalSpeed()
    {
        var source = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));

        var adjusted = DeliveryPaceModel.ApplyHumanBattingPace(
            source,
            CpuDifficulty.Standard,
            isFirstMatch: false);

        Assert.Equal(source.ReleaseVelocity.ToVector3().Length(), adjusted.ReleaseVelocity.ToVector3().Length());
    }
}
