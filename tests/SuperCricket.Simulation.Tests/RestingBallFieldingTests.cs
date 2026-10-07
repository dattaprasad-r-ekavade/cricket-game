using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation.Tests;

public sealed class RestingBallFieldingTests
{
    [Theory]
    [InlineData(true, false, false, BallMotionPhase.Settled, true)]
    [InlineData(false, false, false, BallMotionPhase.Settled, false)]
    [InlineData(true, true, false, BallMotionPhase.Settled, false)]
    [InlineData(true, false, true, BallMotionPhase.Settled, false)]
    [InlineData(true, false, false, BallMotionPhase.Rolling, true)]
    [InlineData(false, false, false, BallMotionPhase.InFlight, true)]
    [InlineData(true, true, false, BallMotionPhase.InFlight, false)]
    public void LiveAdvanceGateKeepsRestingBattedBallsActive(
        bool batted, bool complete, bool throwing, BallMotionPhase phase, bool expected) =>
        Assert.Equal(expected, BattedBallFieldingModel.CanAdvance(batted, complete, throwing, phase));

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void FixedStepFieldingAndRunningContinueAtPhysicalRest(int frameRate)
    {
        var delivery = Preset();
        var ball = new BallFlightSimulator(delivery);
        var position = GroundPosition(delivery);
        ball.ApplyBatContact(position, Vector3.UnitX);
        var fielding = Fielders(delivery, new Vector3(15f, delivery.FieldSurfaceHeightMeters, 0f));
        var runners = new BetweenWicketsState();
        runners.StartRun();
        var session = new DeliverySession(false);
        var accumulator = 0f;
        var elapsed = 0f;
        var stoppedAt = float.NaN;
        var collected = false;
        for (var renderFrame = 0; renderFrame < frameRate * 10 && !collected; renderFrame++)
        {
            accumulator += 1f / frameRate;
            while (accumulator >= ball.FixedTimeStepSeconds &&
                BattedBallFieldingModel.CanAdvance(true, session.IsComplete, collected, ball.CurrentFrame.Phase))
            {
                var previous = ball.CurrentFrame;
                var current = ball.Step(enforceSimulationLimit: false);
                elapsed += ball.FixedTimeStepSeconds;
                if (runners.Advance(ball.FixedTimeStepSeconds, 1.35f) == RunMovementResult.CompletedRun)
                    session.RecordCompletedRun();
                if (current.Phase == BallMotionPhase.Settled && float.IsNaN(stoppedAt))
                {
                    stoppedAt = elapsed;
                    Assert.False(session.IsComplete);
                    Assert.Equal(0, session.CompletedRuns);
                }
                collected = BattedBallFieldingModel.TryAdvance(fielding, previous, current,
                    ball.FixedTimeStepSeconds, out var contact);
                if (collected)
                    Assert.Equal(FieldingContactKind.GroundPickup, contact.Kind);
                accumulator -= ball.FixedTimeStepSeconds;
            }
        }
        Assert.True(collected);
        Assert.True(elapsed > stoppedAt + 1f);
        Assert.Equal(1, session.CompletedRuns);
        Assert.False(session.IsComplete); // Collection and subsequent return, rather than rest, resolve the delivery.
    }

    [Theory]
    [InlineData(1.8f, false, 0, 1)]
    [InlineData(8f, false, 0, 1)]
    [InlineData(15f, false, 1, 1)]
    [InlineData(25f, false, 2, 0)]
    [InlineData(1.8f, true, 0, 1)]
    [InlineData(8f, true, 0, 1)]
    [InlineData(15f, true, 1, 1)]
    [InlineData(25f, true, 2, 0)]
    public void BatchCollectsRestingBallAndResolvesRealReturnInsteadOfDeadBallCredit(
        float fielderDistance, bool noBall, int expectedRuns, int expectedWickets)
    {
        var delivery = Preset();
        delivery.IsNoBall = noBall;
        delivery.FixedTimeStepSeconds = 0.01f;
        var match = new LimitedOversMatch();
        var session = match.BeginDelivery(noBall);
        var fielding = Fielders(delivery, new Vector3(fielderDistance, delivery.FieldSurfaceHeightMeters, 0f));
        var telemetry = new PhysicsAutomatedMatchBatchSimulator.PhysicsMatchTelemetry();
        PhysicsAutomatedMatchBatchSimulator.SimulateBattedBall(match, session, delivery,
            RestingTrajectory(delivery), fielding, 2, 0.05f, 0.1f, telemetry);
        Assert.Equal(expectedRuns + (noBall ? 1 : 0), match.Runs);
        Assert.Equal(expectedRuns, session.CompletedRuns);
        Assert.Equal(expectedWickets, match.Wickets);
        Assert.Equal(expectedWickets, telemetry.RunOuts);
        Assert.Equal(1, telemetry.GroundPickups);
        Assert.True(session.IsComplete);
        Assert.Equal(noBall ? 0 : 1, match.LegalBalls);
    }

    [Theory]
    [InlineData(42f)]
    [InlineData(55f)]
    public void RestingBallNearTheActualBoundaryRemainsCollectible(float radius)
    {
        var delivery = Preset();
        delivery.FieldBoundaryRadiusMeters = radius;
        var fielding = Fielders(delivery, new Vector3(radius - 2f, delivery.FieldSurfaceHeightMeters, 0f));
        var position = GroundPosition(delivery) with { X = radius - 0.1f };
        var resting = new BallFlightFrame(0f, position, Vector3.Zero, 1, BallMotionPhase.Settled);
        var collected = false;
        for (var step = 0; step < 200 && !collected; step++)
            collected = BattedBallFieldingModel.TryAdvance(fielding, resting, resting, 0.01f, out _);
        Assert.True(collected);
        Assert.InRange(fielding.Positions[0].X, radius - 0.8f, radius);
    }

    [Fact]
    public void ShortLaboratoryBudgetDoesNotStopLiveOutgoingPhysicsOrCpuRunning()
    {
        var delivery = Preset();
        delivery.MaximumSimulationSeconds = 0.02f;
        var position = GroundPosition(delivery) + Vector3.UnitY;
        var bounded = new BallFlightSimulator(delivery);
        var live = new BallFlightSimulator(delivery);
        bounded.ApplyBatContact(position, Vector3.UnitX * 5f);
        live.ApplyBatContact(position, Vector3.UnitX * 5f);
        for (var step = 0; step < 12; step++)
        {
            bounded.Step();
            live.Step(enforceSimulationLimit: false);
        }
        Assert.Equal(BallMotionPhase.Settled, bounded.CurrentFrame.Phase);
        Assert.Equal(BallMotionPhase.InFlight, live.CurrentFrame.Phase);
        Assert.True(live.CurrentFrame.TimeSeconds > delivery.MaximumSimulationSeconds);
        var fielding = Fielders(delivery, new Vector3(30f, delivery.FieldSurfaceHeightMeters, 25f));
        var decision = CpuLiveRunningDecisionModel.Choose(true, delivery, position, Vector3.UnitX * 5f,
            fielding.Positions, Enumerable.Repeat(50, FieldingSide.FielderCount).ToArray());
        Assert.Equal(2, decision.PlannedRuns);
        Assert.Equal(CpuLiveRunDecisionReason.SafeRunWindow, decision.Reason);
    }

    [Fact]
    public void AnalysisExhaustionThrowsWithoutCompletingOrScoringTheDelivery()
    {
        var delivery = Preset();
        var match = new LimitedOversMatch();
        var session = match.BeginDelivery(false);
        var frame = new BallFlightFrame(1.6f, GroundPosition(delivery), Vector3.UnitX, 1, BallMotionPhase.Rolling);
        var trajectory = RestingTrajectory(delivery) with { OutgoingFrames = [frame] };
        var telemetry = new PhysicsAutomatedMatchBatchSimulator.PhysicsMatchTelemetry();
        Assert.Throws<InvalidOperationException>(() => PhysicsAutomatedMatchBatchSimulator.SimulateBattedBall(
            match, session, delivery, trajectory, Fielders(delivery, Vector3.Zero), 2, 0.5f, 0.6f, telemetry));
        Assert.False(session.IsComplete);
        Assert.Equal(0, match.Runs);
        Assert.Equal(0, match.LegalBalls);
        Assert.Equal(0, match.Wickets);
    }

    [Fact]
    public void CollectionBudgetReportsFailureInsteadOfInventingDeadBallScore()
    {
        var delivery = Preset();
        var match = new LimitedOversMatch();
        var session = match.BeginDelivery(false);
        // Malformed stationary airborne input must not masquerade as a normal grounded ball.
        var trajectory = RestingTrajectory(delivery);
        trajectory = trajectory with { OutgoingFrames = trajectory.OutgoingFrames.Select(frame =>
            frame with { Position = frame.Position + Vector3.UnitY * 5f }).ToArray() };
        Assert.Throws<InvalidOperationException>(() => PhysicsAutomatedMatchBatchSimulator.SimulateBattedBall(
            match, session, delivery, trajectory, Fielders(delivery, Vector3.Zero), 0, 0.5f, 0.6f,
            new PhysicsAutomatedMatchBatchSimulator.PhysicsMatchTelemetry()));
        Assert.False(session.IsComplete);
        Assert.Equal(0, match.Runs);
        Assert.Equal(0, match.LegalBalls);
    }

    [Fact]
    public void InvalidBoundaryAndRestingFrameInputsAreRejected()
    {
        var fielding = new FieldingSide();
        foreach (var radius in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => fielding.ConfigureBoundaryRadius(radius));
        Assert.Throws<ArgumentException>(() => BattedBallFieldingModel.FramesThroughCollection([], 0.01f).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => BattedBallFieldingModel.FramesThroughCollection(
            RestingTrajectory(Preset()).OutgoingFrames, 0f).ToArray());
    }

    private static DeliveryPreset Preset() => DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));

    private static Vector3 GroundPosition(DeliveryPreset delivery) => new(
        0f, delivery.PitchSurfaceHeightMeters + delivery.BallRadiusMeters, 0f);

    private static FieldingSide Fielders(DeliveryPreset delivery, Vector3 first)
    {
        var fielding = new FieldingSide();
        var positions = Enumerable.Range(0, FieldingSide.FielderCount)
            .Select(index => new Vector3(30f + index, delivery.FieldSurfaceHeightMeters, 28f)).ToArray();
        positions[0] = first;
        fielding.ConfigureStartingPositions(positions);
        fielding.ConfigureBoundaryRadius(delivery.FieldBoundaryRadiusMeters);
        return fielding;
    }

    private static BattingPracticeTrajectory RestingTrajectory(DeliveryPreset delivery)
    {
        const float contactTime = 1.6f;
        var position = GroundPosition(delivery);
        return new BattingPracticeTrajectory(new BattingPracticeSample("drive", delivery.Name, 0.225f, "InPlay",
            contactTime, 0.8f, null, null, null, null, null), position, Vector3.UnitX,
            [new BallFlightFrame(contactTime, position, Vector3.UnitX, 1, BallMotionPhase.Rolling),
             new BallFlightFrame(contactTime + delivery.FixedTimeStepSeconds, position, Vector3.Zero, 1, BallMotionPhase.Settled)]);
    }
}
