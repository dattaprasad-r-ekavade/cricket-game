using System.Numerics;
using SuperCricket.Content;
using SuperCricket.Content.Bowling;
using SuperCricket.Simulation.Bowling;

namespace SuperCricket.Simulation.Tests;

public sealed class DeliveryEnvelopeTests
{
    private static readonly string[] GoodLengthPresets = ["standard-pace.json", "wide-pace.json", "no-ball-pace.json"];
    private const float MinimumGoodLengthMeters = 2.5f;
    private const float MaximumGoodLengthMeters = 5f;
    private const float MinimumQuickSpeedKph = 120f;
    private const float MaximumQuickSpeedKph = 145f;
    private const float MinimumBounceSpeedRetention = 0.74f;
    private const float MaximumBounceSpeedRetention = 0.86f;
    private const float MinimumCreaseHeightMeters = 0.1f;
    private const float MaximumCreaseHeightMeters = 0.6f;

    [Fact]
    public void VerifyDeliveryEnvelopes_SeededStockDeliveriesStayWithinReferenceBands()
    {
        foreach (var presetName in GoodLengthPresets)
        {
            var stock = DeliveryPreset.Load(TestAssets.Asset("deliveries", presetName));
            for (var seed = 0; seed < 32; seed++)
            {
                var prepared = BowlingAccuracyModel.Aim(stock, 0, 0, 80, 10, 100,
                    BowlingSkillLevel.Standard, seed).Delivery;
                AssertEnvelope(prepared, $"{presetName}, seed {seed}", quick: true);
            }
        }
        var yorkerPreset = DeliveryPreset.Load(TestAssets.Asset("deliveries", "yorker-pace.json"));
        AssertInRange(yorkerPreset.StartVelocity.Length() * 3.6f,
            MinimumQuickSpeedKph, MaximumQuickSpeedKph, "yorker-pace.json", "release speed");
        var stockPreset = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        for (var seed = 0; seed < 32; seed++)
        {
            var cpuDelivery = BowlingDecisionModel.ChooseDelivery(stockPreset, 91, 65,
                new BowlingSituation(0, 1, 0, 0, null), seed).Delivery;
            AssertEnvelope(cpuDelivery, $"CPU stock pace, seed {seed}", quick: true);
        }

        var catalogue = DeliveryCatalogue.Load(TestAssets.Asset("bowling", "deliveries.json"));
        for (var definitionIndex = 0; definitionIndex < catalogue.Deliveries.Count; definitionIndex++)
        {
            var definition = catalogue.Deliveries[definitionIndex];
            var quick = definition.Style == BowlerStyle.Pace && definition.Id != "pace-slower-ball";
            if (!quick)
                continue;

            Assert.InRange(definition.MinSpeedKph, MinimumQuickSpeedKph, MaximumQuickSpeedKph);
            Assert.InRange(definition.MaxSpeedKph, MinimumQuickSpeedKph, MaximumQuickSpeedKph);

            for (var seed = 0; seed < 32; seed++)
            {
                var random = new Random(1009 * (definitionIndex + 1) + seed);
                var speed = definition.MinSpeedKph +
                    (float)random.NextDouble() * (definition.MaxSpeedKph - definition.MinSpeedKph);
                var delivery = PaceMeterModel.CreateDelivery(definition, stockPreset, 0, 0, speed, PitchType.Normal);
                var prepared = BowlingAccuracyModel.Aim(delivery, 0, 0, 80, 10, 100,
                    BowlingSkillLevel.Standard, seed).Delivery;
                AssertEnvelope(prepared, $"{definition.Id}, seed {seed}", quick);
            }
        }
    }

    private static void AssertEnvelope(DeliveryPreset delivery, string label, bool quick)
    {
        var releaseSpeedKph = delivery.StartVelocity.Length() * 3.6f;
        if (quick)
            AssertInRange(releaseSpeedKph, MinimumQuickSpeedKph, MaximumQuickSpeedKph, label, "release speed");

        var simulator = new BallFlightSimulator(delivery);
        var previous = simulator.CurrentFrame;
        BallFlightFrame? firstBounce = null;
        var speedBeforeBounce = 0f;
        BallFlightFrame? creaseCrossing = null;
        var creaseZ = -delivery.PitchLengthMeters / 2f;
        var maximumSteps = (int)MathF.Ceiling(delivery.MaximumSimulationSeconds / delivery.FixedTimeStepSeconds);

        for (var step = 0; step < maximumSteps; step++)
        {
            var current = simulator.Step();
            if (firstBounce is null && current.BounceCount > previous.BounceCount)
            {
                firstBounce = current;
                speedBeforeBounce = previous.Velocity.Length();
            }

            if (firstBounce is not null && previous.BounceCount > 0 && current.Position.Z <= creaseZ && previous.Position.Z > creaseZ)
            {
                var fraction = (creaseZ - previous.Position.Z) / (current.Position.Z - previous.Position.Z);
                creaseCrossing = current with
                {
                    Position = Vector3.Lerp(previous.Position, current.Position, fraction)
                };
                break;
            }

            if (current.Phase == BallMotionPhase.Settled)
                break;
            previous = current;
        }

        Assert.True(firstBounce is not null, $"{label}: no pitch bounce.");
        var bounce = firstBounce!.Value;
        var lengthInFrontOfCrease = delivery.PitchLengthMeters / 2f - MathF.Abs(bounce.Position.Z);
        AssertInRange(lengthInFrontOfCrease, MinimumGoodLengthMeters, MaximumGoodLengthMeters, label, "bounce length");

        var speedRetention = bounce.Velocity.Length() / speedBeforeBounce;
        AssertInRange(speedRetention, MinimumBounceSpeedRetention, MaximumBounceSpeedRetention, label, "bounce speed retention");

        Assert.True(creaseCrossing is not null, $"{label}: did not cross the batting crease after the pitch bounce.");
        var crossingHeight = creaseCrossing!.Value.Position.Y - delivery.PitchSurfaceHeightMeters;
        AssertInRange(crossingHeight, MinimumCreaseHeightMeters, MaximumCreaseHeightMeters, label, "crease height");
    }

    private static void AssertInRange(float value, float minimum, float maximum, string label, string metric) =>
        Assert.True(value >= minimum && value <= maximum,
            $"{label}: {metric} {value:0.000} outside [{minimum:0.000}, {maximum:0.000}].");
}
