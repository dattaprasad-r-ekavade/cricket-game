using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation.Tests;

public sealed class BattedBallPhysicsTests
{
    [Fact]
    public void ApplyBatContact_UsesBattedBallBounceTuningInsteadOfDeliveryBounceTuning()
    {
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        var simulator = new BallFlightSimulator(delivery);
        simulator.ApplyBatContact(new Vector3(0f, 0.5f, 0f), new Vector3(0f, -3f, 10f));
        var previous = simulator.CurrentFrame;
        BallFlightFrame? bounce = null;

        for (var step = 0; step < 30; step++)
        {
            var current = simulator.Step();
            if (current.BounceCount > 0)
            {
                bounce = current;
                break;
            }
            previous = current;
        }

        Assert.True(bounce is not null, "The batted ball did not bounce on the pitch.");
        var retention = bounce!.Value.Velocity.Length() / previous.Velocity.Length();
        Assert.InRange(retention, 0.9f, 1f);
    }
}
