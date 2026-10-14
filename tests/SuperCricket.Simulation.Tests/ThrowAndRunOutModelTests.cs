using System.Numerics;
using SuperCricket.Simulation.Fielding;

namespace SuperCricket.Simulation.Tests;

public sealed class ThrowAndRunOutModelTests
{
    [Fact]
    public void Create_UsesDistanceAndArmRatingForThrowSpeedAndKeepsThrowsFlat()
    {
        var shortWeakArm = ThrowModel.Create(Vector3.Zero, new Vector3(0, 0, 5), 0, 75, 1);
        var shortStrongArm = ThrowModel.Create(Vector3.Zero, new Vector3(0, 0, 5), 100, 75, 1);
        var deep = ThrowModel.Create(Vector3.Zero, new Vector3(0, 0, 30), 100, 75, 1);

        Assert.Equal(19f * 0.85f, shortWeakArm.SpeedMetersPerSecond);
        Assert.Equal(19f, shortStrongArm.SpeedMetersPerSecond);
        Assert.Equal(28f, deep.SpeedMetersPerSecond);
        Assert.True(shortStrongArm.ApexHeightMeters < 2f);
        Assert.True(deep.ApexHeightMeters < 2f);
    }

    [Fact]
    public void Create_UsesSeededDirectHitChanceScaledByDistanceAndAccuracy()
    {
        const int trials = 1000;
        var start = Vector3.Zero;
        var shortHits = Enumerable.Range(0, trials).Count(seed =>
            ThrowModel.Create(start, new Vector3(0, 0, 5), 80, 100, seed).IsDirectHit);
        var deepHits = Enumerable.Range(0, trials).Count(seed =>
            ThrowModel.Create(start, new Vector3(0, 0, 30), 80, 0, seed).IsDirectHit);
        var miss = ThrowModel.Create(start, new Vector3(0, 0, 30), 80, 0, 1);

        Assert.InRange(shortHits / (float)trials, 0.50f, 0.60f);
        Assert.InRange(deepHits / (float)trials, 0.04f, 0.14f);
        Assert.Equal(0.35f, miss.GatherDelaySeconds);
    }

    [Fact]
    public void ChooseThreatenedEnd_SelectsTheCloserWicket()
    {
        Assert.Equal(WicketEnd.Near, RunOutModel.ChooseThreatenedEnd(new Vector3(0, 0, -8)));
        Assert.Equal(WicketEnd.Far, RunOutModel.ChooseThreatenedEnd(new Vector3(0, 0, 8)));
    }

    [Fact]
    public void IsGroundedBehindCrease_UsesThePoppingCreaseForBothEnds()
    {
        var nearCrease = CricketPitchGeometry.NearWicketZ + RunOutModel.PoppingCreaseOffsetMeters;
        var farCrease = CricketPitchGeometry.FarWicketZ - RunOutModel.PoppingCreaseOffsetMeters;
        Assert.True(RunOutModel.IsGroundedBehindCrease(new Vector3(0, 0, nearCrease), WicketEnd.Near, isGrounded: true));
        Assert.False(RunOutModel.IsGroundedBehindCrease(new Vector3(0, 0, nearCrease - 0.01f), WicketEnd.Near, isGrounded: true));
        Assert.True(RunOutModel.IsGroundedBehindCrease(new Vector3(0, 0, farCrease), WicketEnd.Far, isGrounded: true));
        Assert.False(RunOutModel.IsGroundedBehindCrease(new Vector3(0, 0, farCrease + 0.01f), WicketEnd.Far, isGrounded: true));
        Assert.False(RunOutModel.IsGroundedBehindCrease(new Vector3(0, 0, nearCrease), WicketEnd.Near, isGrounded: false));
    }

    [Fact]
    public void ResolveAtBrokenWicket_DetectsOutAndNearRunOut()
    {
        var moving = new BetweenWicketsState();
        moving.StartRun();
        moving.Advance(1f, 2f);
        var outDecision = RunOutModel.ResolveAtBrokenWicket(moving, WicketEnd.Near, true, 10f);
        Assert.True(outDecision.IsOut);
        Assert.False(outDecision.NearRunOut);

        var nearCrease = new BetweenWicketsState();
        nearCrease.StartRun();
        nearCrease.Advance(1.9f, 2f);
        var safeDecision = RunOutModel.ResolveAtBrokenWicket(nearCrease, WicketEnd.Far, true, 1f);
        Assert.False(safeDecision.IsOut);
        Assert.True(safeDecision.NearRunOut);
    }

}
