using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation.Tests;

public sealed class NoBallFieldingTests
{
    [Theory]
    [InlineData(false, false, FieldingContactKind.Catch, FieldingCollectionAction.CaughtDismissal)]
    [InlineData(false, true, FieldingContactKind.Catch, FieldingCollectionAction.CaughtDismissal)]
    [InlineData(true, false, FieldingContactKind.Catch, FieldingCollectionAction.Held)]
    [InlineData(true, true, FieldingContactKind.Catch, FieldingCollectionAction.ReturnThrow)]
    [InlineData(false, false, FieldingContactKind.GroundPickup, FieldingCollectionAction.Held)]
    [InlineData(false, true, FieldingContactKind.GroundPickup, FieldingCollectionAction.ReturnThrow)]
    [InlineData(true, false, FieldingContactKind.GroundPickup, FieldingCollectionAction.Held)]
    [InlineData(true, true, FieldingContactKind.GroundPickup, FieldingCollectionAction.ReturnThrow)]
    public void LiveCollectionDecisionPreservesRunsAndReturnOnNoBall(
        bool noBall, bool moving, FieldingContactKind kind, FieldingCollectionAction expected)
    {
        var session = new DeliverySession(noBall);
        session.RecordCompletedRun();
        Assert.Equal(expected, BattedBallFieldingModel.ResolveCollection(session, kind, moving));
        Assert.False(session.IsComplete);
        var dismissed = expected == FieldingCollectionAction.CaughtDismissal;
        Assert.Equal(dismissed ? DismissalKind.Caught : DismissalKind.None, session.Dismissal);
        Assert.Equal(dismissed ? 0 : 1, session.CompletedRuns);
        Assert.Equal(noBall ? 1 : 0, session.ExtraRuns);
        if (expected == FieldingCollectionAction.ReturnThrow)
        {
            session.RecordCompletedRun();
            session.ResolveRunOut();
            Assert.Equal(2, session.CompletedRuns);
            Assert.Equal(DismissalKind.RunOut, session.Dismissal);
        }
    }

    [Theory]
    [InlineData(false, 0.1f, 0.1f, 0)]
    [InlineData(false, 1.4f, 1.4f, 0)]
    [InlineData(true, 0.1f, 0.1f, 0)]
    [InlineData(true, 0.7f, 0.7f, 1)]
    [InlineData(true, 1.4f, 0.7f, 1)]
    [InlineData(true, 1.4f, 1.4f, 2)]
    public void CpuNoBallCatchUsesAuthoredCollectionDurationAndReturnWindow(
        bool noBall, float catchDuration, float throwDuration, int expectedRuns)
    {
        var delivery = Preset(noBall);
        var position = new Vector3(0f, 1f, 0f);
        var decision = CpuLiveRunningDecisionModel.Choose(true, delivery, position, Vector3.UnitX,
            Fielders(delivery).Positions, Enumerable.Repeat(50, FieldingSide.FielderCount).ToArray(),
            throwAnimationDurationSeconds: throwDuration, catchAnimationDurationSeconds: catchDuration);
        Assert.Equal(expectedRuns, decision.PlannedRuns);
        Assert.Equal(expectedRuns == 0 ? CpuLiveRunDecisionReason.FielderCanCollectBeforeRun
            : CpuLiveRunDecisionReason.SafeRunWindow, decision.Reason);
    }

    [Theory]
    [InlineData(false, 0.1f, 0, 1)]
    [InlineData(false, 2.1f, 0, 1)]
    [InlineData(true, 0.1f, 0, 1)]
    [InlineData(true, 0.7f, 1, 1)]
    [InlineData(true, 2.1f, 2, 0)]
    public void ActualBatchCatchOffNoBallContinuesThroughReturnAndCanRunOut(
        bool noBall, float catchDuration, int expectedRuns, int expectedWickets)
    {
        var delivery = Preset(noBall);
        var match = new LimitedOversMatch();
        var session = match.BeginDelivery(noBall);
        const float contactTime = 1.6f;
        var position = new Vector3(0f, 1f, 0f);
        var trajectory = new BattingPracticeTrajectory(new BattingPracticeSample("loft", delivery.Name,
            0.225f, "InPlay", contactTime, 0.8f, null, null, null, null, null), position, Vector3.UnitX,
            [new BallFlightFrame(contactTime, position, Vector3.UnitX, 0, BallMotionPhase.InFlight),
             new BallFlightFrame(contactTime + 0.1f, position, Vector3.UnitX, 0, BallMotionPhase.InFlight)]);
        var telemetry = new PhysicsAutomatedMatchBatchSimulator.PhysicsMatchTelemetry();
        PhysicsAutomatedMatchBatchSimulator.SimulateBattedBall(match, session, delivery, trajectory,
            Fielders(delivery), 2, 0.05f, 0.6f, telemetry, catchDuration);
        Assert.Equal(expectedRuns + (noBall ? 1 : 0), match.Runs);
        Assert.Equal(expectedRuns, session.CompletedRuns);
        Assert.Equal(expectedWickets, match.Wickets);
        Assert.Equal(noBall ? expectedWickets : 0, telemetry.RunOuts);
        Assert.Equal(noBall ? (expectedWickets == 0 ? DismissalKind.None : DismissalKind.RunOut)
            : DismissalKind.Caught, session.Dismissal);
        Assert.Equal(1, telemetry.Catches);
        Assert.Equal(0, telemetry.GroundPickups);
        Assert.Equal(noBall ? 0 : 1, match.LegalBalls);
    }

    [Fact]
    public void InvalidCollectionAndCatchDurationsAreRejected()
    {
        var delivery = Preset(true);
        var fielding = Fielders(delivery);
        foreach (var duration in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => CpuLiveRunningDecisionModel.Choose(
                true, delivery, new Vector3(0f, 1f, 0f), Vector3.UnitX, fielding.Positions,
                Enumerable.Repeat(50, FieldingSide.FielderCount).ToArray(), catchAnimationDurationSeconds: duration));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattedBallFieldingModel.ResolveCollection(
            new DeliverySession(true), (FieldingContactKind)99, true));
        var match = new LimitedOversMatch();
        var session = match.BeginDelivery(false);
        match.CompleteDelivery();
        Assert.Throws<InvalidOperationException>(() => BattedBallFieldingModel.ResolveCollection(
            session, FieldingContactKind.GroundPickup, true));
    }

    private static DeliveryPreset Preset(bool noBall)
    {
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        delivery.IsNoBall = noBall;
        return delivery;
    }

    private static FieldingSide Fielders(DeliveryPreset delivery)
    {
        var fielding = new FieldingSide();
        var positions = Enumerable.Range(0, FieldingSide.FielderCount)
            .Select(index => new Vector3(30f + index, delivery.FieldSurfaceHeightMeters, 28f)).ToArray();
        positions[0] = new Vector3(0f, delivery.FieldSurfaceHeightMeters, 0f);
        fielding.ConfigureStartingPositions(positions);
        return fielding;
    }
}
