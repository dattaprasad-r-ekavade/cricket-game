using System.Numerics;
using SuperCricket.Simulation;

namespace SuperCricket.Simulation.Tests;

public sealed class ContactPlaneCrossingModelTests
{
    [Fact]
    public void InterpolatesTheBallAtTheContactPlane()
    {
        var previous = new BallFlightFrame(0.4f, new Vector3(-0.2f, 0.5f, -7f), new Vector3(1f, -2f, -10f), 0, BallMotionPhase.InFlight);
        var current = new BallFlightFrame(0.5f, new Vector3(0.2f, 0.3f, -9f), new Vector3(3f, -4f, -10f), 1, BallMotionPhase.InFlight);

        var crossed = ContactPlaneCrossingModel.TryResolve(previous, current, -8f, out var crossing, out var fraction);

        Assert.True(crossed);
        Assert.Equal(0.5f, fraction, 4);
        Assert.Equal(0.45f, crossing.TimeSeconds, 4);
        Assert.Equal(new Vector3(0f, 0.4f, -8f), crossing.Position);
        Assert.Equal(new Vector3(2f, -3f, -10f), crossing.Velocity);
        Assert.Equal(1, crossing.BounceCount);
    }

    [Fact]
    public void DoesNotReportAFrameThatDoesNotCrossThePlane()
    {
        var previous = new BallFlightFrame(0.4f, new Vector3(0f, 0.3f, -7f), new Vector3(0f, 0f, -10f), 0, BallMotionPhase.InFlight);
        var current = new BallFlightFrame(0.5f, new Vector3(0f, 0.3f, -7.5f), new Vector3(0f, 0f, -5f), 0, BallMotionPhase.InFlight);

        Assert.False(ContactPlaneCrossingModel.TryResolve(previous, current, -8f, out _, out _));
    }
}
