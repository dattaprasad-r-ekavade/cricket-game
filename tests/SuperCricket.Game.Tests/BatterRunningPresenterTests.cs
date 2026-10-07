using SuperCricket.Simulation;

namespace SuperCricket.Game.Tests;

public sealed class BatterRunningPresenterTests
{
    [Fact]
    public void TurningBackChangesFacingWithoutTeleportingEitherBatter()
    {
        var runners = new BetweenWicketsState();
        runners.StartRun();
        runners.Advance(0.6f, 1f);
        var before = BatterRunningPresenter.GetWorlds(runners, -8.72f, 8.72f, 0.45f);
        runners.TurnBack();
        var turned = BatterRunningPresenter.GetWorlds(runners, -8.72f, 8.72f, 0.45f);
        Assert.Equal(before.Striker.Translation, turned.Striker.Translation);
        Assert.Equal(before.NonStriker.Translation, turned.NonStriker.Translation);
        Assert.InRange(before.Striker.M11, 0.999f, 1.001f);
        Assert.InRange(turned.Striker.M11, -1.001f, -0.999f);
        runners.Advance(0.1f, 1f);
        var returning = BatterRunningPresenter.GetWorlds(runners, -8.72f, 8.72f, 0.45f);
        Assert.True(returning.Striker.Translation.Z < turned.Striker.Translation.Z);
        Assert.True(returning.NonStriker.Translation.Z > turned.NonStriker.Translation.Z);
    }

    [Fact]
    public void CompletedRunPlacesBothBattersAtTheOppositeEnds()
    {
        var runners = new BetweenWicketsState();
        runners.StartRun();
        runners.Advance(1f, 1f);
        var worlds = BatterRunningPresenter.GetWorlds(runners, -8.72f, 8.72f, 0f);
        Assert.Equal(8.72f, worlds.Striker.Translation.Z);
        Assert.Equal(-8.72f, worlds.NonStriker.Translation.Z);
    }

    [Fact]
    public void DeliveryCompletionPreservesDisplayedPositionsUntilReset()
    {
        var runners = new BetweenWicketsState();
        runners.StartRun();
        runners.Advance(0.7f, 1f);
        var moving = BatterRunningPresenter.GetWorlds(runners, -8.72f, 8.72f, 0f);
        runners.Stop();
        var stopped = BatterRunningPresenter.GetWorlds(runners, -8.72f, 8.72f, 0f);
        Assert.Equal(moving.Striker.Translation, stopped.Striker.Translation);
        Assert.Equal(moving.NonStriker.Translation, stopped.NonStriker.Translation);
        runners.Reset();
        Assert.Equal(-8.72f, BatterRunningPresenter.GetWorlds(runners, -8.72f, 8.72f, 0f).Striker.Translation.Z);
    }
}
