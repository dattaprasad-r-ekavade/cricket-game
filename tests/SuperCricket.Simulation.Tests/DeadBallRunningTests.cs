using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation.Tests;

public sealed class DeadBallRunningTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void StoppageCreditsOnlyCrossedRunAndNeverDismisses(bool noBall, bool crossed)
    {
        var match = new MatchState();
        var delivery = match.BeginDelivery(noBall);
        delivery.ResolveDeadBall(crossed);
        var result = match.CompleteDelivery();

        Assert.Equal(DismissalKind.None, result.Dismissal);
        Assert.Equal(0, match.Wickets);
        Assert.Equal(crossed ? 1 : 0, result.BatterRuns);
        Assert.Equal(result.BatterRuns, result.CompletedRuns);
        Assert.Equal(noBall ? 1 : 0, result.ExtraRuns);
        Assert.Equal(result.BatterRuns + result.ExtraRuns, match.Runs);
        Assert.Equal(noBall ? 0 : 1, match.LegalBalls);
        Assert.Equal(crossed ? 2 : 1, match.Striker);
        Assert.Equal(crossed ? 1 : 2, match.NonStriker);
    }

    [Theory]
    [InlineData(false, 1, 2)]
    [InlineData(true, 2, 1)]
    public void PreviousRunsRemainCreditedAtStoppage(bool crossed, int expectedRuns, int expectedStriker)
    {
        var match = new MatchState();
        var delivery = match.BeginDelivery(false);
        delivery.RecordCompletedRun();
        delivery.ResolveDeadBall(crossed);
        match.CompleteDelivery();

        Assert.Equal(expectedRuns, match.Runs);
        Assert.Equal(expectedStriker, match.Striker);
        Assert.Equal(0, match.Wickets);
    }

    [Fact]
    public void DeadBallOnLastBallAppliesCrossingThenOverEndSwap()
    {
        var match = new MatchState();
        for (var ball = 0; ball < 5; ball++)
        {
            match.BeginDelivery(false).ResolveDeadBall(false);
            match.CompleteDelivery();
        }
        match.BeginDelivery(false).ResolveDeadBall(true);
        match.CompleteDelivery();
        Assert.Equal(1, match.Runs);
        Assert.Equal(1, match.Striker);
        Assert.Equal(2, match.NonStriker);
        Assert.True(match.IsOverComplete);
    }

    [Fact]
    public void CompletedDeliveryRejectsAnotherDeadBallResolution()
    {
        var match = new MatchState();
        var delivery = match.BeginDelivery(false);
        delivery.ResolveDeadBall(true);
        match.CompleteDelivery();
        Assert.Throws<InvalidOperationException>(() => delivery.ResolveDeadBall(true));
        Assert.Equal(1, match.Runs);
    }

    [Fact]
    public void DeadBallCreditCannotFollowADismissal()
    {
        var match = new MatchState();
        var delivery = match.BeginDelivery(false);
        delivery.ResolveRunOut();
        Assert.Throws<InvalidOperationException>(() => delivery.ResolveDeadBall(true));
        match.CompleteDelivery();
        Assert.Equal(0, match.Runs);
        Assert.Equal(1, match.Wickets);
    }

    [Fact]
    public void CrossingOccursAtHalfwayWithoutTheOldSeventyTwoPercentCutoff()
    {
        var duration = CpuLiveRunningDecisionModel.DefaultRunDurationSeconds;
        var halfway = duration * 0.5f;
        Assert.False(RunningScoringModel.HasCrossed(MathF.BitDecrement(halfway), duration));
        Assert.True(RunningScoringModel.HasCrossed(halfway, duration));
        Assert.True(RunningScoringModel.HasCrossed(duration * 0.6f, duration));
    }

    [Theory]
    [InlineData(0f, 2, 0)]
    [InlineData(0.49f, 2, 0)]
    [InlineData(0.5f, 2, 1)]
    [InlineData(0.6f, 2, 1)]
    [InlineData(1f, 2, 1)]
    [InlineData(1.49f, 2, 1)]
    [InlineData(1.5f, 2, 2)]
    [InlineData(1.6f, 2, 2)]
    [InlineData(2f, 2, 2)]
    [InlineData(20f, 2, 2)]
    [InlineData(1.6f, 1, 1)]
    [InlineData(1f, 0, 0)]
    public void DeadBallCountIncludesCrossingAndRespectsAttemptLimit(float elapsed, int limit, int expected) =>
        Assert.Equal(expected, RunningScoringModel.CountRunsAtDeadBall(elapsed, 1f, limit));

    [Fact]
    public void InvalidRunningTimesAreRejected()
    {
        foreach (var elapsed in new[] { float.NaN, float.PositiveInfinity, -1f })
            Assert.Throws<ArgumentOutOfRangeException>(() => RunningScoringModel.HasCrossed(elapsed, 1f));
        foreach (var duration in new[] { float.NaN, float.PositiveInfinity, -1f, 0f })
            Assert.Throws<ArgumentOutOfRangeException>(() => RunningScoringModel.CountRunsAtDeadBall(1f, duration, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => RunningScoringModel.CountRunsAtDeadBall(1f, 1f, -1));
    }

    [Theory]
    [InlineData(0.49f, false, 2)]
    [InlineData(0.6f, false, 2)]
    [InlineData(1.49f, false, 2)]
    [InlineData(1.6f, false, 2)]
    [InlineData(0.49f, true, 2)]
    [InlineData(0.6f, true, 2)]
    [InlineData(1.49f, true, 2)]
    [InlineData(1.6f, true, 2)]
    public void BatchRestingBallIsCollectedAfterTheBattersCompleteTheirPlannedRuns(
        float runProgress, bool noBall, int expectedRuns)
    {
        var match = new LimitedOversMatch();
        var session = match.BeginDelivery(noBall);
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        delivery.IsNoBall = noBall;
        var fielding = DistantFielders(delivery);
        var position = new Vector3(0f, delivery.FieldSurfaceHeightMeters + delivery.BallRadiusMeters, 0f);
        const float contactTime = 1.6f;
        var elapsed = runProgress * CpuLiveRunningDecisionModel.DefaultRunDurationSeconds;
        var trajectory = new BattingPracticeTrajectory(
            new BattingPracticeSample("drive", delivery.Name, 0.225f, "InPlay",
                contactTime, 0.8f, null, null, null, null, null),
            position, Vector3.UnitX,
            [new BallFlightFrame(contactTime, position, Vector3.UnitX, 1, BallMotionPhase.Rolling),
             new BallFlightFrame(contactTime + elapsed, position, Vector3.Zero, 1, BallMotionPhase.Settled)]);
        var telemetry = new PhysicsAutomatedMatchBatchSimulator.PhysicsMatchTelemetry();

        PhysicsAutomatedMatchBatchSimulator.SimulateBattedBall(
            match, session, delivery, trajectory, fielding, plannedRuns: 2,
            pickupDuration: 0.5f, throwDuration: 0.6f, telemetry);

        Assert.Equal(expectedRuns, session.BatterRuns);
        Assert.Equal(expectedRuns + (noBall ? 1 : 0), match.Runs);
        Assert.Equal(0, match.Wickets);
        Assert.Equal(DismissalKind.None, session.Dismissal);
        Assert.Equal(expectedRuns % 2 == 0 ? 1 : 2, match.Striker);
        Assert.Equal(expectedRuns, telemetry.CompletedRuns);
        Assert.Equal(0, telemetry.RunOuts);
        Assert.Equal(1, telemetry.GroundPickups);
    }

    [Theory]
    [InlineData(0.49f, 2)]
    [InlineData(0.6f, 2)]
    [InlineData(1.49f, 2)]
    [InlineData(1.6f, 2)]
    public void CpuDecisionKeepsRunningAfterPhysicalRest(float runProgress, int expectedRuns)
    {
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        var position = new Vector3(0f, delivery.FieldSurfaceHeightMeters + delivery.BallRadiusMeters + 0.001f, 0f);
        var ball = new BallFlightSimulator(delivery);
        ball.ApplyBatContact(position, Vector3.UnitX);
        while (ball.CurrentFrame.Phase != BallMotionPhase.Settled)
            ball.Step();
        var runDuration = ball.CurrentFrame.TimeSeconds / runProgress;
        var fielding = DistantFielders(delivery);
        var decision = CpuLiveRunningDecisionModel.Choose(
            true, delivery, position, Vector3.UnitX, fielding.Positions,
            Enumerable.Repeat(70, FieldingSide.FielderCount).ToArray(), runDurationSeconds: runDuration);

        Assert.Equal(expectedRuns, decision.PlannedRuns);
        Assert.Equal(CpuLiveRunDecisionReason.SafeRunWindow, decision.Reason);
        Assert.True(decision.EventTimeSeconds > ball.CurrentFrame.TimeSeconds);
    }

    private static FieldingSide DistantFielders(DeliveryPreset delivery)
    {
        var fielding = new FieldingSide();
        fielding.ConfigureStartingPositions(Enumerable.Range(0, FieldingSide.FielderCount)
            .Select(index => new Vector3(30f + index, delivery.FieldSurfaceHeightMeters, 28f)).ToArray());
        return fielding;
    }
}
