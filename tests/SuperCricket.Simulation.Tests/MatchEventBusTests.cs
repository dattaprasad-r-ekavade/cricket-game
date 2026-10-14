using SuperCricket.Content;
using SuperCricket.Simulation.Events;

namespace SuperCricket.Simulation.Tests;

public sealed class MatchEventBusTests
{
    [Fact]
    public void TypedSubscriptionsReceiveEventsAndCanBeDisposed()
    {
        var bus = new MatchEventBus();
        var deliveriesReleased = 0;
        var subscription = bus.Subscribe<DeliveryReleased>(_ => deliveriesReleased++);

        bus.BeginDelivery(1, 1);
        bus.CompleteDelivery(new DeliveryResult(0, 0, 0));
        subscription.Dispose();
        bus.BeginDelivery(1, 2);
        bus.CompleteDelivery(new DeliveryResult(0, 0, 0));

        Assert.Equal(1, deliveriesReleased);
        Assert.Equal(new[] { 1, 2 }, bus.DeliveryLogs.Select(log => log.DeliveryNumber));
        Assert.All(bus.DeliveryLogs, log => Assert.Contains(log.Events, matchEvent => matchEvent is DeliveryReleased));
    }

    [Fact]
    public void MatchStatePublishesRunsBoundaryDismissalAndOverEnd()
    {
        var match = new MatchState(oversPerInnings: 1);
        var runEvents = new List<RunCompleted>();
        var overEvents = new List<OverComplete>();
        using var runSubscription = match.Events.Subscribe<RunCompleted>(runEvents.Add);
        using var overSubscription = match.Events.Subscribe<OverComplete>(overEvents.Add);

        var scoringRun = match.BeginDelivery(isNoBall: false);
        scoringRun.RecordCompletedRun();
        match.CompleteDelivery();

        var wicket = match.BeginDelivery(isNoBall: false);
        Assert.True(wicket.ResolveCatch());
        match.CompleteDelivery();

        for (var ball = 0; ball < 3; ball++)
        {
            match.BeginDelivery(isNoBall: false);
            match.CompleteDelivery();
        }

        var boundary = match.BeginDelivery(isNoBall: false);
        boundary.ResolveBoundary(clearedInTheAir: false, currentRunCrossed: false);
        match.CompleteDelivery();

        var deliveryLogs = match.Events.DeliveryLogs;
        Assert.Equal(5, runEvents.Sum(matchEvent => matchEvent.Runs));
        Assert.Contains(deliveryLogs.SelectMany(log => log.Events), matchEvent => matchEvent is Boundary);
        Assert.Contains(deliveryLogs.SelectMany(log => log.Events), matchEvent => matchEvent is Dismissal { Kind: "Caught" });
        Assert.Single(overEvents);
        Assert.Equal(5, match.Runs);
        Assert.Equal(1, match.Wickets);
        Assert.Equal(6, match.LegalBalls);
    }

    [Fact]
    public void SeededBatchEventLogsReconcileWithBothInningsScorecards()
    {
        var firstTeam = TeamRosterAsset.Load(TestAssets.Asset("teams", "coastal-xi.json"));
        var secondTeam = TeamRosterAsset.Load(TestAssets.Asset("teams", "highland-xi.json"));
        var match = Assert.Single(AutomatedMatchBatchSimulator.RunBatch(firstTeam, secondTeam, 1, 1, 7319));

        AssertInningsReconciles(match.FirstInnings);
        AssertInningsReconciles(match.SecondInnings);
    }

    private static void AssertInningsReconciles(MatchInningsResult innings)
    {
        Assert.NotEmpty(innings.DeliveryLogs);
        Assert.All(innings.DeliveryLogs, log =>
        {
            Assert.Equal(innings.InningsNumber, log.InningsNumber);
            Assert.Contains(log.Events, matchEvent => matchEvent is DeliveryReleased);
        });

        var events = innings.DeliveryLogs.SelectMany(log => log.Events).ToArray();
        Assert.Equal(innings.Runs, events.OfType<RunCompleted>().Sum(matchEvent => matchEvent.Runs));
        Assert.Equal(innings.Wickets, events.OfType<Dismissal>().Count());
        Assert.Equal(innings.Runs, innings.DeliveryLogs.Sum(log => log.Result.BatterRuns + log.Result.ExtraRuns));
        Assert.Equal(innings.Wickets, innings.DeliveryLogs.Count(log => log.Result.Dismissal != DismissalKind.None));
    }
}
