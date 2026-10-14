using System.Numerics;
using SuperCricket.Content;
using SuperCricket.Content.Bowling;
using SuperCricket.Simulation.Bowling;

namespace SuperCricket.Simulation.Tests;

public sealed class BowlingModelTests
{
    private static string CataloguePath => TestAssets.Asset("bowling", "deliveries.json");
    private static DeliveryPreset StockDelivery => DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));

    [Fact]
    public void Aim_RookieAndAmateurHaveNoScatter_AndResultsAreRepeatable()
    {
        foreach (var difficulty in new[] { BowlingSkillLevel.Rookie, BowlingSkillLevel.Amateur })
        {
            var result = BowlingAccuracyModel.Aim(StockDelivery, 0.25f, -0.3f, 70, 5, 100, difficulty, 28);
            Assert.Equal(0f, result.ScatterLineMeters);
            Assert.Equal(0f, result.ScatterLengthMeters);
        }

        var first = BowlingAccuracyModel.Aim(StockDelivery, 0.25f, -0.3f, 70, 5, 100, BowlingSkillLevel.Standard, 28);
        var replay = BowlingAccuracyModel.Aim(StockDelivery, 0.25f, -0.3f, 70, 5, 100, BowlingSkillLevel.Standard, 28);
        Assert.Equal(first.ScatterLineMeters, replay.ScatterLineMeters);
        Assert.Equal(first.ScatterLengthMeters, replay.ScatterLengthMeters);
        Assert.InRange(first.ScatterLineMeters, -0.25f, 0.25f);
        Assert.InRange(first.ScatterLengthMeters, -0.25f, 0.25f);
    }

    [Fact]
    public void Aim_RhythmImprovesRadiusAndScatterInTheFirstTenBalls()
    {
        var cold = BowlingAccuracyModel.Aim(StockDelivery, 0f, 0f, 80, 0, 100, BowlingSkillLevel.Standard, 41);
        var settled = BowlingAccuracyModel.Aim(StockDelivery, 0f, 0f, 80, 10, 100, BowlingSkillLevel.Standard, 41);

        Assert.Equal(0f, cold.Rhythm);
        Assert.Equal(1f, settled.Rhythm);
        Assert.True(settled.TargetRadiusMeters < cold.TargetRadiusMeters);
        Assert.True(MathF.Abs(settled.ScatterLineMeters) < MathF.Abs(cold.ScatterLineMeters));
    }

    [Fact]
    public void Stamina_DecaysByRatingPerOverAndExpandsTargetBelowFifty()
    {
        Assert.Equal(100f, BowlingAccuracyModel.StaminaAfterOvers(80f, 0));
        Assert.Equal(76f, BowlingAccuracyModel.StaminaAfterOvers(80f, 6));
        Assert.Equal(72.5f, BowlingAccuracyModel.StaminaAfterOvers(50f, 5));
        var fresh = BowlingAccuracyModel.Aim(StockDelivery, 0, 0, 80, 10, 100, BowlingSkillLevel.Rookie, 1);
        var tired = BowlingAccuracyModel.Aim(StockDelivery, 0, 0, 80, 10, 0, BowlingSkillLevel.Rookie, 1);
        Assert.Equal(fresh.TargetRadiusMeters * 1.5f, tired.TargetRadiusMeters);
    }

    [Fact]
    public void Aim_ProfileUsesPlayerAccuracyStaminaAndScatterSettings()
    {
        var profile = DifficultyProfileAsset.Load(TestAssets.Asset("difficulty", "limited-standard-cpu.json"));
        var bowler = TeamRosterAsset.CreatePlaceholder("Profile Bowler XI").Players[7];
        bowler.BowlingAttributes.Accuracy = 84;
        bowler.BowlingAttributes.Stamina = 78;
        var lessAccurateBowler = TeamRosterAsset.CreatePlaceholder("Profile Bowler XI").Players[7];
        lessAccurateBowler.BowlingAttributes.Accuracy = 45;
        lessAccurateBowler.BowlingAttributes.Stamina = 78;

        var result = BowlingAccuracyModel.Aim(
            StockDelivery, 0f, 0f, bowler, ballsInSpell: 10,
            BowlingSkillLevel.Standard, seed: 712, profile);
        var lessAccurate = BowlingAccuracyModel.Aim(
            StockDelivery, 0f, 0f, lessAccurateBowler, ballsInSpell: 10,
            BowlingSkillLevel.Standard, seed: 712, profile);

        Assert.Equal(78f, result.Stamina);
        Assert.InRange(result.TargetRadiusMeters,
            profile.BowlingTargetRadiusMinMeters, profile.BowlingTargetRadiusMaxMeters);
        Assert.True(result.TargetRadiusMeters < lessAccurate.TargetRadiusMeters);
        Assert.Equal(profile.MeterFillSeconds, PaceMeterModel.FillSeconds(profile));
        Assert.Equal(DeliveryPaceBand.Normal, PaceMeterModel.Resolve(0.35f, profile).Pace);
    }

    [Fact]
    public void PaceMeter_ResolvesBandsNoBallAndNoPress()
    {
        Assert.Equal(0.9f, PaceMeterModel.FillSeconds(PaceMeterDifficulty.Easy));
        Assert.Equal(0.7f, PaceMeterModel.FillSeconds(PaceMeterDifficulty.Normal));
        Assert.Equal(0.45f, PaceMeterModel.FillSeconds(PaceMeterDifficulty.Hard));
        Assert.Equal(DeliveryPaceBand.Slow, PaceMeterModel.Resolve(0.1f, PaceMeterDifficulty.Normal).Pace);
        Assert.Equal(0.82f, PaceMeterModel.Resolve(0.1f, PaceMeterDifficulty.Normal).SpeedMultiplier);
        Assert.Equal(DeliveryPaceBand.Normal, PaceMeterModel.Resolve(0.35f, PaceMeterDifficulty.Normal).Pace);
        Assert.Equal(DeliveryPaceBand.Fast, PaceMeterModel.Resolve(0.5f, PaceMeterDifficulty.Normal).Pace);
        var noBall = PaceMeterModel.Resolve(0.65f, PaceMeterDifficulty.Normal);
        Assert.True(noBall.IsNoBall);
        Assert.Equal(1f, noBall.SpeedMultiplier);
        Assert.False(PaceMeterModel.Resolve(null, PaceMeterDifficulty.Normal).IsNoBall);
        Assert.Equal(DeliveryPaceBand.Normal, PaceMeterModel.Resolve(null, PaceMeterDifficulty.Normal).Pace);
    }

    [Fact]
    public void CatalogueEntries_CreateCenteredPitchBouncesWithinThePitch()
    {
        var catalogue = DeliveryCatalogue.Load(CataloguePath);
        var stock = StockDelivery;
        foreach (var definition in catalogue.Deliveries)
        {
            for (var seed = 0; seed < 100; seed++)
            {
                var random = new Random(seed);
                var speed = definition.MinSpeedKph + (float)random.NextDouble() * (definition.MaxSpeedKph - definition.MinSpeedKph);
                var delivery = PaceMeterModel.CreateDelivery(definition, stock, 0, 0, speed, PitchType.Dusty);
                var prepared = BowlingAccuracyModel.Aim(delivery, 0, 0, 80, 10, 100,
                    BowlingSkillLevel.Rookie, seed).Delivery;
                var bounce = BowlingAimModel.FindFirstBounce(prepared);
                Assert.True(bounce is not null, $"{definition.Id} seed {seed} did not bounce.");
                Assert.InRange(bounce!.Value.Position.X, -prepared.PitchWidthMeters / 2f, prepared.PitchWidthMeters / 2f);
                Assert.InRange(bounce.Value.Position.Z, -prepared.PitchLengthMeters / 2f, prepared.PitchLengthMeters / 2f);
                Assert.InRange(prepared.ReleaseVelocity.ToVector3().Length() * 3.6f, definition.MinSpeedKph - 0.05f, definition.MaxSpeedKph + 0.05f);
                Assert.Equal(definition.Style == BowlerStyle.Pace ? 1f : definition.PitchSpinMultipliers[PitchType.Dusty], prepared.PitchSpinMultiplier);
            }
        }
    }
}
