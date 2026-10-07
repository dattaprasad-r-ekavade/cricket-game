using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation.Tests;

public sealed class BetweenWicketsTests
{
    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.75f)]
    public void TurningBackPreservesPositionAndTakesTimeToReachHome(float progress)
    {
        var runners = new BetweenWicketsState();
        runners.StartRun();
        runners.Advance(progress, 1f);
        var strikerPosition = runners.StrikerPositionFraction;
        var nonStrikerPosition = runners.NonStrikerPositionFraction;
        runners.TurnBack();
        runners.TurnBack();
        Assert.True(runners.IsMoving);
        Assert.True(runners.IsReturning);
        Assert.Equal(strikerPosition, runners.StrikerPositionFraction);
        Assert.Equal(nonStrikerPosition, runners.NonStrikerPositionFraction);
        Assert.True(runners.TryResolveRunOut(WicketEnd.Near, true, out _));

        runners.Advance(progress / 2f, 1f);
        Assert.Equal(progress / 2f, runners.Progress);
        Assert.True(runners.IsMoving);
        Assert.Equal(RunMovementResult.ReturnedHome, runners.Advance(progress / 2f, 1f));
        Assert.False(runners.IsMoving);
        Assert.Equal(0, runners.CompletedRuns);
        Assert.Equal(0f, runners.StrikerPositionFraction);
        Assert.Equal(1f, runners.NonStrikerPositionFraction);
        Assert.False(runners.TryResolveRunOut(WicketEnd.Near, true, out _));
    }

    [Fact]
    public void ReturningFromSecondRunKeepsPreviouslyCompletedRunAndEnds()
    {
        var runners = new BetweenWicketsState();
        runners.StartRun();
        Assert.Equal(RunMovementResult.CompletedRun, runners.Advance(1f, 1f));
        runners.StartRun();
        runners.Advance(0.6f, 1f);
        runners.TurnBack();
        runners.Advance(0.6f, 1f);
        Assert.Equal(1, runners.CompletedRuns);
        Assert.Equal(1f, runners.StrikerPositionFraction);
        Assert.Equal(0f, runners.NonStrikerPositionFraction);
    }

    [Fact]
    public void UnusedFrameTimeCanContinueTheQueuedRun()
    {
        var runners = new BetweenWicketsState();
        runners.StartRun();
        var result = runners.Advance(1.4f, 1f, out var remaining);
        Assert.Equal(RunMovementResult.CompletedRun, result);
        Assert.InRange(remaining, 0.3999f, 0.4001f);
        runners.StartRun();
        runners.Advance(remaining, 1f);
        Assert.Equal(1, runners.CompletedRuns);
        Assert.InRange(runners.Progress, 0.3999f, 0.4001f);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void ReturnMovesContinuouslyAcrossFrameRates(int frameRate)
    {
        var runners = new BetweenWicketsState();
        runners.StartRun();
        runners.Advance(0.6f, 1f);
        runners.TurnBack();
        var previous = runners.StrikerPositionFraction;
        for (var frame = 0; frame <= frameRate && runners.IsMoving; frame++)
        {
            runners.Advance(1f / frameRate, 1f);
            Assert.InRange(previous - runners.StrikerPositionFraction, 0f, 1f / frameRate + 0.000001f);
            previous = runners.StrikerPositionFraction;
        }
        Assert.False(runners.IsMoving);
        Assert.Equal(0f, previous);
        Assert.Equal(0, runners.CompletedRuns);
    }

    [Fact]
    public void StopFreezesPositionsAndResetStartsANewDelivery()
    {
        var runners = new BetweenWicketsState();
        runners.StartRun();
        runners.Advance(0.4f, 1f);
        runners.Stop();
        runners.Advance(10f, 1f);
        Assert.Equal(0.4f, runners.StrikerPositionFraction);
        Assert.False(runners.StartRun());
        Assert.False(runners.TryResolveRunOut(WicketEnd.Near, true, out _));
        runners.Reset();
        Assert.Equal(0f, runners.StrikerPositionFraction);
        Assert.True(runners.StartRun());
    }

    [Theory]
    [InlineData(WicketEnd.Near)]
    [InlineData(WicketEnd.Far)]
    public void SafeEndsAndUnbrokenWicketsDoNotDismiss(WicketEnd wicket)
    {
        var runners = new BetweenWicketsState();
        Assert.False(runners.TryResolveRunOut(wicket, true, out _));
        runners.StartRun();
        Assert.False(runners.TryResolveRunOut(wicket, true, out _));
        runners.Advance(0.4f, 1f);
        Assert.False(runners.TryResolveRunOut(wicket, false, out _));
        runners.Advance(0.6f, 1f);
        Assert.False(runners.TryResolveRunOut(wicket, true, out _));
    }

    [Fact]
    public void LevelBattersRetainThePreviousGroundOwnerInBothDirections()
    {
        var runners = new BetweenWicketsState();
        runners.StartRun();
        runners.Advance(0.5f, 1f);
        Assert.True(runners.TryResolveRunOut(WicketEnd.Near, true, out var outward));
        Assert.False(outward.SwapEnds);
        runners.Advance(0.25f, 1f);
        runners.TurnBack();
        runners.Advance(0.25f, 1f);
        Assert.True(runners.TryResolveRunOut(WicketEnd.Near, true, out var returning));
        Assert.True(returning.SwapEnds);
        runners.Advance(0.1f, 1f);
        Assert.True(runners.TryResolveRunOut(WicketEnd.Near, true, out var crossedBack));
        Assert.False(crossedBack.SwapEnds);
    }

    public static IEnumerable<object[]> RunOutCases()
    {
        foreach (var completed in new[] { 0, 1, 2 })
        foreach (var progress in new[] { 0.25f, 0.75f })
        foreach (var wicket in new[] { WicketEnd.Near, WicketEnd.Far })
        foreach (var noBall in new[] { false, true })
        foreach (var lastBall in new[] { false, true })
            yield return [completed, progress, wicket, noBall, lastBall];
    }

    [Theory]
    [MemberData(nameof(RunOutCases))]
    public void RunOutReplacesTheGroundOwnerWithoutCreditingTheUncompletedRun(
        int completed, float progress, WicketEnd wicket, bool noBall, bool lastBall)
    {
        var match = new MatchState(oversPerInnings: 2);
        if (lastBall)
            for (var ball = 0; ball < 5; ball++) { match.BeginDelivery(false); match.CompleteDelivery(); }
        var session = match.BeginDelivery(noBall);
        var runners = new BetweenWicketsState();
        for (var run = 0; run < completed; run++)
        {
            runners.StartRun();
            runners.Advance(1f, 1f);
            session.RecordCompletedRun();
        }
        runners.StartRun();
        runners.Advance(progress, 1f);
        var nearOwner = runners.StrikerPositionFraction < runners.NonStrikerPositionFraction ? 1 : 2;
        var farOwner = nearOwner == 1 ? 2 : 1;
        var expectedNear = wicket == WicketEnd.Near ? 3 : nearOwner;
        var expectedFar = wicket == WicketEnd.Far ? 3 : farOwner;
        Assert.True(runners.TryResolveRunOut(wicket, true, out var resolution));
        session.ResolveRunOut(resolution.DismissedEnd, resolution.SwapEnds);
        var result = match.CompleteDelivery();

        Assert.Equal(completed, result.CompletedRuns);
        Assert.Equal(completed + (noBall ? 1 : 0), match.Runs);
        Assert.Equal(1, match.Wickets);
        Assert.Equal(lastBall && !noBall ? expectedFar : expectedNear, match.Striker);
        Assert.Equal(lastBall && !noBall ? expectedNear : expectedFar, match.NonStriker);
        Assert.Equal((lastBall ? 5 : 0) + (noBall ? 0 : 1), match.LegalBalls);
    }

    [Fact]
    public void EndSwapCannotBeAttachedToAnotherDismissalKind()
    {
        var result = new DeliveryResult(0, 0, 0, Dismissal: DismissalKind.Caught, SwapEndsOnRunOut: true);
        Assert.NotNull(result.Validate());
        var session = new DeliverySession(false);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.ResolveRunOut((DismissedEnd)123));
    }

    [Fact]
    public void InvalidMovementInputsAreRejected()
    {
        var runners = new BetweenWicketsState();
        foreach (var delta in new[] { -1f, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => runners.Advance(delta, 1f));
        foreach (var duration in new[] { -1f, 0f, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => runners.Advance(1f, duration));
        Assert.Throws<ArgumentOutOfRangeException>(() => runners.TryResolveRunOut((WicketEnd)123, true, out _));
    }

    [Theory]
    [InlineData(0.1f, 0.1f, 1, 0, 1, 3, 2)]
    [InlineData(0.4f, 0.4f, 1, 0, 1, 3, 1)]
    [InlineData(0.7f, 0.7f, 2, 1, 1, 3, 1)]
    [InlineData(1.15f, 1.15f, 2, 1, 1, 3, 2)]
    [InlineData(0.65f, 0.65f, 1, 1, 0, 2, 1)]
    public void BatchReturnThrowUsesElapsedTimeSinceContactAndCorrectGroundOwner(
        float pickup, float throwing, int plannedRuns, int expectedRuns, int expectedWickets,
        int expectedStriker, int expectedNonStriker)
    {
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        var match = new LimitedOversMatch();
        var session = match.BeginDelivery(false);
        var fielding = new FieldingSide();
        var position = new Vector3(0f, delivery.FieldSurfaceHeightMeters + delivery.BallRadiusMeters, 0f);
        var positions = Enumerable.Range(0, FieldingSide.FielderCount)
            .Select(index => new Vector3(30f + index, delivery.FieldSurfaceHeightMeters, 28f)).ToArray();
        positions[0] = new Vector3(0f, delivery.FieldSurfaceHeightMeters, 0f);
        fielding.ConfigureStartingPositions(positions);
        const float contactTime = 1.6f;
        var trajectory = new BattingPracticeTrajectory(
            new BattingPracticeSample("drive", delivery.Name, 0.225f, "InPlay", contactTime,
                0.8f, null, null, null, null, null), position, Vector3.UnitX,
            [new BallFlightFrame(contactTime, position, Vector3.UnitX, 1, BallMotionPhase.Rolling),
             new BallFlightFrame(contactTime + 0.1f, position, Vector3.UnitX, 1, BallMotionPhase.Rolling)]);
        var telemetry = new PhysicsAutomatedMatchBatchSimulator.PhysicsMatchTelemetry();
        PhysicsAutomatedMatchBatchSimulator.SimulateBattedBall(
            match, session, delivery, trajectory, fielding, plannedRuns, pickup, throwing, telemetry);
        Assert.Equal(expectedRuns, match.Runs);
        Assert.Equal(expectedWickets, match.Wickets);
        Assert.Equal(expectedStriker, match.Striker);
        Assert.Equal(expectedNonStriker, match.NonStriker);
        Assert.Equal(expectedWickets, telemetry.RunOuts);
        Assert.Equal(1, telemetry.GroundPickups);
    }
}
