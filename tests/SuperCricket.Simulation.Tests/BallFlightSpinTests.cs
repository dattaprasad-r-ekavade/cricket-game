using System.Numerics;
using SuperCricket.Content;
using SuperCricket.Content.Bowling;
using SuperCricket.Simulation.Bowling;

namespace SuperCricket.Simulation.Tests;

public sealed class BallFlightSpinTests
{
    private static string CataloguePath => TestAssets.Asset("bowling", "deliveries.json");
    private static DeliveryPreset StockDelivery => DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));

    [Fact]
    public void SideSpin_ProducesOppositeOffBreakAndLegBreakTurn_AndReversesForVariations()
    {
        var offBreak = SimulateToStrikerWicket(CreateDelivery("off-spin-off-break", PitchType.Normal));
        var legBreak = SimulateToStrikerWicket(CreateDelivery("leg-spin-leg-break", PitchType.Normal));
        var doosra = SimulateToStrikerWicket(CreateDelivery("off-spin-doosra", PitchType.Normal));
        var googly = SimulateToStrikerWicket(CreateDelivery("leg-spin-googly", PitchType.Normal));

        Assert.InRange(offBreak.LateralMeters, 0.3f, 0.6f);
        Assert.True(legBreak.LateralMeters < -0.3f, $"Leg-break turned {legBreak.LateralMeters:0.###} m.");
        Assert.True(doosra.LateralMeters < -0.3f, $"Doosra turned {doosra.LateralMeters:0.###} m.");
        Assert.True(googly.LateralMeters > 0.3f, $"Googly turned {googly.LateralMeters:0.###} m.");
    }

    [Fact]
    public void SideSpin_UsesPitchMultiplier_AndDryPitchTurnsAboutThreeTimesGreen()
    {
        var green = SimulateToStrikerWicket(CreateDelivery("off-spin-off-break", PitchType.Green));
        var dry = SimulateToStrikerWicket(CreateDelivery("off-spin-off-break", PitchType.Dusty));

        Assert.InRange(dry.LateralMeters / green.LateralMeters, 2.8f, 3.2f);
        Assert.InRange(MathF.Abs(SimulateToStrikerWicket(CreateDelivery("off-spin-flighted", PitchType.Dusty)).LateralMeters), 0.5f, 1f);
    }

    [Theory]
    [InlineData("off-spin-arm-ball", 1.085f)]
    [InlineData("leg-spin-top-spinner", 1.09f)]
    [InlineData("leg-spin-flipper", 0.92f)]
    public void VerticalSpin_HasNoLateralTurn_AndChangesBounceRestitution(string deliveryId, float expectedRestitutionRatio)
    {
        var spin = CreateDelivery(deliveryId, PitchType.Normal);
        Assert.Equal(0f, spin.SideSpin);
        var plain = spin.DeepCopy();
        plain.TopSpin = 0f;

        var spinBounce = FindFirstBounceFrame(spin);
        var plainBounce = FindFirstBounceFrame(plain);
        Assert.InRange(MathF.Abs(spinBounce.Position.X - plainBounce.Position.X), 0f, 0.01f);
        Assert.InRange(MathF.Abs(spinBounce.Velocity.Y / plainBounce.Velocity.Y), expectedRestitutionRatio - 0.005f, expectedRestitutionRatio + 0.005f);

        var path = SimulateToStrikerWicket(spin);
        Assert.InRange(MathF.Abs(path.LateralMeters), 0f, 0.01f);
    }

    [Fact]
    public void ApplyBatContact_DoesNotApplyBowlingSpinToTheBattedBall()
    {
        var simulator = new BallFlightSimulator(CreateDelivery("off-spin-off-break", PitchType.Dusty));
        simulator.ApplyBatContact(new Vector3(0f, 1f, -3f), new Vector3(0f, 3f, -14f));

        for (var step = 0; step < 200 && simulator.CurrentFrame.Phase != BallMotionPhase.Settled; step++)
        {
            simulator.Step();
            Assert.InRange(MathF.Abs(simulator.CurrentFrame.Position.X), 0f, 0.001f);
        }
    }

    private static DeliveryPreset CreateDelivery(string id, PitchType pitchType)
    {
        var definition = DeliveryCatalogue.Load(CataloguePath).Deliveries.Single(delivery => delivery.Id == id);
        var speed = (definition.MinSpeedKph + definition.MaxSpeedKph) * 0.5f;
        return PaceMeterModel.CreateDelivery(definition, StockDelivery, 0f, 0f, speed, pitchType);
    }

    private static (BallFlightFrame Bounce, float LateralMeters) SimulateToStrikerWicket(DeliveryPreset delivery)
    {
        var simulator = new BallFlightSimulator(delivery);
        BallFlightFrame? bounce = null;
        for (var step = 0; step < 1200; step++)
        {
            var frame = simulator.Step();
            if (bounce is null && frame.BounceCount > 0)
                bounce = frame;
            if (bounce is { } firstBounce && frame.Position.Z <= -CricketPitchGeometry.WicketOffsetMeters)
                return (firstBounce, frame.Position.X - firstBounce.Position.X);
            if (frame.Phase == BallMotionPhase.Settled)
                break;
        }

        throw new InvalidOperationException($"{delivery.Name} did not reach the striker's wicket after pitching.");
    }

    private static BallFlightFrame FindFirstBounceFrame(DeliveryPreset delivery)
    {
        var simulator = new BallFlightSimulator(delivery);
        for (var step = 0; step < 1200; step++)
        {
            var frame = simulator.Step();
            if (frame.BounceCount > 0)
                return frame;
            if (frame.Phase == BallMotionPhase.Settled)
                break;
        }

        throw new InvalidOperationException($"{delivery.Name} did not bounce.");
    }
}
