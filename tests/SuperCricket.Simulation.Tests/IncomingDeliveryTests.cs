using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation.Tests;

public sealed class IncomingDeliveryTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(42)]
    [InlineData(54)]
    [InlineData(62)]
    [InlineData(69)]
    public void PassingTheBatterDoesNotInventABowledDismissal(int seed)
    {
        var stock = LoadDelivery("standard-pace");
        var delivery = BowlingDecisionModel.ChooseDelivery(
            stock, 91, 65, new BowlingSituation(0, 1, 0, 0, null), seed).Delivery;
        var batterPoint = FindCrossing(delivery, BattingPracticeAnalyzer.BatterWicketLineZ);
        var stumpPoint = FindCrossing(delivery, -10.06f);
        Assert.InRange(MathF.Abs(batterPoint.X), 0f, 0.12f + delivery.BallRadiusMeters);
        Assert.True(MathF.Abs(stumpPoint.X) > 0.12f + delivery.BallRadiusMeters);

        var batch = new DeliverySession(false);
        PhysicsAutomatedMatchBatchSimulator.ResolveMissedDelivery(batch, delivery);
        var live = ResolveThroughLiveModel(delivery);
        Assert.Equal(IncomingDeliveryResolution.PassedBatAndWickets, live.Resolution);
        Assert.Equal(-10.06f, live.Crossing.Z, 4);
        Assert.Equal(DismissalKind.None, batch.Dismissal);
        Assert.Equal(batch.Complete(), live.Session.Complete());
    }

    [Theory]
    [InlineData("standard-pace", IncomingDeliveryResolution.Bowled, DismissalKind.Bowled, DeliveryExtra.None, true)]
    [InlineData("wide-pace", IncomingDeliveryResolution.Wide, DismissalKind.None, DeliveryExtra.Wide, false)]
    [InlineData("no-ball-pace", IncomingDeliveryResolution.NoBall, DismissalKind.None, DeliveryExtra.NoBall, false)]
    public void ShippedDeliveriesHaveTheSameLiveAndBatchScore(
        string name, IncomingDeliveryResolution expectedResolution, DismissalKind dismissal, DeliveryExtra extra, bool legal)
    {
        var delivery = LoadDelivery(name);
        var batch = new DeliverySession(delivery.IsNoBall);
        PhysicsAutomatedMatchBatchSimulator.ResolveMissedDelivery(batch, delivery);
        var live = ResolveThroughLiveModel(delivery);
        var result = batch.Complete();
        Assert.Equal(expectedResolution, live.Resolution);
        Assert.Equal(dismissal, result.Dismissal);
        Assert.Equal(extra, result.Extra);
        Assert.Equal(legal, result.IsLegal);
        Assert.Equal(legal ? 0 : 1, result.ExtraRuns);
        Assert.Equal(result, live.Session.Complete());
    }

    [Fact]
    public void CpuVariationsAgreeWithAnIndependentPhysicalStumpCalculation()
    {
        var stock = LoadDelivery("standard-pace");
        var bowling = new BowlingSituation(0, 1, 0, 0, null);
        var bowled = 0;
        var dots = 0;
        for (var seed = 0; seed < 250; seed++)
        {
            var delivery = BowlingDecisionModel.ChooseDelivery(stock, 91, 65, bowling, seed).Delivery;
            var point = FindCrossing(delivery, -10.06f);
            var expected = MathF.Abs(point.X) > delivery.PitchWidthMeters / 2f + 0.55f
                ? IncomingDeliveryResolution.Wide
                : MathF.Abs(point.X) <= 0.12f + delivery.BallRadiusMeters &&
                  point.Y >= 0f && point.Y <= 0.71f + delivery.BallRadiusMeters
                    ? IncomingDeliveryResolution.Bowled : IncomingDeliveryResolution.PassedBatAndWickets;
            var live = ResolveThroughLiveModel(delivery);
            var batch = new DeliverySession(false);
            PhysicsAutomatedMatchBatchSimulator.ResolveMissedDelivery(batch, delivery);
            Assert.Equal(expected, live.Resolution);
            Assert.Equal(batch.Complete(), live.Session.Complete());
            if (expected == IncomingDeliveryResolution.Bowled) bowled++;
            if (expected == IncomingDeliveryResolution.PassedBatAndWickets) dots++;
        }
        Assert.True(bowled > 0);
        Assert.True(dots > 0);
    }

    [Theory]
    [InlineData(-8.7f, -8.8f)]
    [InlineData(-10.1f, -10.2f)]
    [InlineData(-10.1f, -10f)]
    [InlineData(-10.06f, -10.06f)]
    public void NoForwardCrossingLeavesTheSessionUnchanged(float previousZ, float currentZ)
    {
        var session = new DeliverySession(false);
        Assert.False(IncomingDeliveryModel.TryResolve(session,
            new Vector3(0f, 0.3f, previousZ), new Vector3(0f, 0.3f, currentZ), LoadDelivery("standard-pace"),
            out _, out _));
        Assert.Equal(DismissalKind.None, session.Dismissal);
        Assert.Equal(DeliveryExtra.None, session.Extra);
        Assert.False(session.IsComplete);
    }

    [Theory]
    [InlineData(-10f, -10.06f)]
    [InlineData(-10.06f, -10.1f)]
    public void AFrameEndpointAtTheWicketStillResolves(float previousZ, float currentZ)
    {
        Assert.True(IncomingDeliveryModel.TryResolve(new DeliverySession(false),
            new Vector3(0f, 0.3f, previousZ), new Vector3(0f, 0.3f, currentZ), LoadDelivery("standard-pace"),
            out var crossing, out var resolution));
        Assert.Equal(-10.06f, crossing.Z, 4);
        Assert.Equal(IncomingDeliveryResolution.Bowled, resolution);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void InterpolationUsesTheSamePhysicalPlaneAtDifferentFrameRates(int frequency)
    {
        var delivery = LoadDelivery("standard-pace");
        var session = new DeliverySession(false);
        var start = new Vector3(0f, 0.3f, -8.72f);
        var velocity = new Vector3(4f, 0f, -20f);
        for (var frame = 1; frame < frequency; frame++)
        {
            if (!IncomingDeliveryModel.TryResolve(session,
                start + velocity * ((frame - 1f) / frequency), start + velocity * (frame / (float)frequency), delivery,
                out var crossing, out var resolution)) continue;
            Assert.Equal(-10.06f, crossing.Z, 4);
            Assert.Equal(0.268f, crossing.X, 4);
            Assert.Equal(IncomingDeliveryResolution.PassedBatAndWickets, resolution);
            return;
        }
        Assert.Fail("The incoming ball never reached the physical wicket.");
    }

    [Fact]
    public void CrossingSamplesHeightAtTheWicketRatherThanTheFrameEnd()
    {
        Assert.True(IncomingDeliveryModel.TryResolve(new DeliverySession(false),
            new Vector3(0f, 0.3f, -10f), new Vector3(0f, 1.3f, -10.2f), LoadDelivery("standard-pace"),
            out var crossing, out var resolution));
        Assert.Equal(0.6f, crossing.Y, 4);
        Assert.Equal(IncomingDeliveryResolution.Bowled, resolution);
    }

    [Fact]
    public void InvalidInputsAreRejectedWithoutChangingTheScore()
    {
        var session = new DeliverySession(false);
        var delivery = LoadDelivery("standard-pace");
        var before = new Vector3(0f, 0.3f, -10f);
        var after = new Vector3(0f, 0.3f, -10.2f);
        Assert.Throws<ArgumentNullException>(() => IncomingDeliveryModel.TryResolve(null!, before, after, delivery, out _, out _));
        Assert.Throws<ArgumentNullException>(() => IncomingDeliveryModel.TryResolve(session, before, after, null!, out _, out _));
        Assert.Throws<ArgumentException>(() => IncomingDeliveryModel.TryResolve(session, new Vector3(float.NaN), after, delivery, out _, out _));
        Assert.Throws<ArgumentException>(() => IncomingDeliveryModel.TryResolve(session, before, new Vector3(float.PositiveInfinity), delivery, out _, out _));
        delivery.BallRadiusMeters = 0f;
        Assert.Throws<ArgumentException>(() => IncomingDeliveryModel.TryResolve(session, before, after, delivery, out _, out _));
        delivery.BallRadiusMeters = 0.036f;
        delivery.PitchWidthMeters = float.NaN;
        Assert.Throws<ArgumentOutOfRangeException>(() => IncomingDeliveryModel.TryResolve(session, before, after, delivery, out _, out _));
        Assert.Equal(DismissalKind.None, session.Dismissal);
        Assert.Equal(0, session.ExtraRuns);
    }

    private static DeliveryPreset LoadDelivery(string name) =>
        DeliveryPreset.Load(TestAssets.Asset("deliveries", name + ".json"));

    private static (DeliverySession Session, Vector3 Crossing, IncomingDeliveryResolution Resolution)
        ResolveThroughLiveModel(DeliveryPreset delivery)
    {
        var session = new DeliverySession(delivery.IsNoBall);
        var ball = new BallFlightSimulator(delivery);
        var previous = ball.CurrentFrame;
        for (var step = 0; step < 1000 && previous.Phase != BallMotionPhase.Settled; step++)
        {
            var current = ball.Step();
            if (IncomingDeliveryModel.TryResolve(session, previous.Position, current.Position, delivery, out var crossing, out var resolution))
                return (session, crossing, resolution);
            previous = current;
        }
        throw new InvalidOperationException("The fixture delivery never reached the physical wicket.");
    }

    private static Vector3 FindCrossing(DeliveryPreset delivery, float planeZ)
    {
        var ball = new BallFlightSimulator(delivery);
        var previous = ball.CurrentFrame;
        for (var step = 0; step < 1000 && previous.Phase != BallMotionPhase.Settled; step++)
        {
            var current = ball.Step();
            if (previous.Position.Z >= planeZ && current.Position.Z <= planeZ)
                return Vector3.Lerp(previous.Position, current.Position,
                    (planeZ - previous.Position.Z) / (current.Position.Z - previous.Position.Z));
            previous = current;
        }
        throw new InvalidOperationException("The fixture delivery never crossed its expected plane.");
    }
}
