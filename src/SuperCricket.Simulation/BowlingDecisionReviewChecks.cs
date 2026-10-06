using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

public static class BowlingDecisionReviewChecks
{
    public static void Run()
    {
        var stock = CreateStockDelivery();
        var situation = new BowlingSituation(LegalBalls: 4, OversPerInnings: 2, Runs: 5, Wickets: 1, Target: 18);
        var first = BowlingDecisionModel.ChooseDelivery(stock, 76, 68, situation, seed: 20261006);
        var replay = BowlingDecisionModel.ChooseDelivery(stock, 76, 68, situation, seed: 20261006);
        Require(first.Variation == replay.Variation &&
            NearlyEqual(first.Delivery.ReleaseVelocity.ToVector3(), replay.Delivery.ReleaseVelocity.ToVector3()) &&
            first.Delivery.LateralAccelerationMetersPerSecondSquared == replay.Delivery.LateralAccelerationMetersPerSecondSquared,
            "a bowling decision did not replay from its seed");
        Require(first.Delivery.Validate().Count == 0 && !first.Delivery.IsNoBall,
            "a legal CPU bowling decision created an invalid or illegal stock delivery");
        Require(!ReferenceEquals(first.Delivery.ReleasePosition, stock.ReleasePosition) &&
            !ReferenceEquals(first.Delivery.ReleaseVelocity, stock.ReleaseVelocity),
            "a CPU delivery shared mutable release vectors with its stock preset");
        Require(stock.Name == "Review stock" && stock.ReleaseVelocity.ToVector3() == new Vector3(0f, -1.6f, -34f) &&
            stock.LateralAccelerationMetersPerSecondSquared == 0f,
            "creating a CPU delivery changed the reusable stock preset");

        var weakError = 0f;
        var strongError = 0f;
        var weakSpeed = 0f;
        var strongSpeed = 0f;
        var weakMovement = 0f;
        var strongMovement = 0f;
        var variations = new HashSet<BowlingVariation>();
        var minimumWicketLineX = float.PositiveInfinity;
        var maximumWicketLineX = float.NegativeInfinity;
        for (var seed = 0; seed < 64; seed++)
        {
            var weak = BowlingDecisionModel.ChooseDelivery(stock, 0, 55, situation, seed);
            var strong = BowlingDecisionModel.ChooseDelivery(stock, 100, 55, situation, seed);
            Require(weak.Variation == strong.Variation,
                "bowling ability changed the tactical plan instead of changing its execution");
            weakError += MathF.Abs(weak.AccuracyErrorVelocityX);
            strongError += MathF.Abs(strong.AccuracyErrorVelocityX);
            weakSpeed += weak.Delivery.StartVelocity.Length();
            strongSpeed += strong.Delivery.StartVelocity.Length();
            weakMovement += MathF.Abs(weak.Delivery.LateralAccelerationMetersPerSecondSquared);
            strongMovement += MathF.Abs(strong.Delivery.LateralAccelerationMetersPerSecondSquared);
            variations.Add(weak.Variation);

            var flight = new BallFlightSimulator(strong.Delivery);
            var previousFrame = flight.CurrentFrame;
            var wicketLineZ = -stock.PitchLengthMeters / 2f;
            var crossedWicketLine = false;
            for (var step = 0; step < 500 && flight.CurrentFrame.Phase != BallMotionPhase.Settled; step++)
            {
                var frame = flight.Step();
                Require(IsFinite(frame.Position) && IsFinite(frame.Velocity),
                    "a skill-scaled delivery produced a non-finite flight frame");
                if (!crossedWicketLine && previousFrame.Position.Z >= wicketLineZ && frame.Position.Z < wicketLineZ)
                {
                    var fraction = (previousFrame.Position.Z - wicketLineZ) /
                        (previousFrame.Position.Z - frame.Position.Z);
                    var lineX = previousFrame.Position.X + (frame.Position.X - previousFrame.Position.X) * fraction;
                    minimumWicketLineX = MathF.Min(minimumWicketLineX, lineX);
                    maximumWicketLineX = MathF.Max(maximumWicketLineX, lineX);
                    crossedWicketLine = true;
                }
                previousFrame = frame;
            }
            Require(crossedWicketLine && flight.CurrentFrame.Phase == BallMotionPhase.Settled,
                "a skill-scaled delivery did not cross the wicket line and settle within its configured simulation time");
        }
        Require(strongError < weakError && strongSpeed > weakSpeed && strongMovement > weakMovement,
            "bowling skill did not improve control, pace, and swing execution across repeated deliveries");
        Require(variations.Count >= 3,
            "seeded bowling decisions did not produce a useful range of delivery plans");
        Require(maximumWicketLineX - minimumWicketLineX > 0.4f,
            "CPU bowling plans did not create distinguishable physical lines at the wicket");

        var defensiveSituation = new BowlingSituation(0, 2, 0, 0, Target: 10);
        var calmSituation = new BowlingSituation(0, 2, 0, 0, Target: 1);
        var defensivePlans = 0;
        var calmPlans = 0;
        var powerPlans = 0;
        for (var seed = 0; seed < 64; seed++)
        {
            var defensive = BowlingDecisionModel.ChooseDelivery(stock, 70, 55, defensiveSituation, seed);
            var calm = BowlingDecisionModel.ChooseDelivery(stock, 70, 55, calmSituation, seed);
            var powerThreat = BowlingDecisionModel.ChooseDelivery(stock, 70, 90, calmSituation, seed);
            if (defensive.Variation is BowlingVariation.OutsideOff or BowlingVariation.Outswing)
                defensivePlans++;
            if (calm.Variation is BowlingVariation.OutsideOff or BowlingVariation.Outswing)
                calmPlans++;
            if (powerThreat.Variation is BowlingVariation.OutsideOff or BowlingVariation.Outswing)
                powerPlans++;
        }
        Require(defensivePlans > calmPlans && powerPlans > calmPlans,
            "the CPU bowler did not respond to a high chase requirement or a power hitter");

        RequireThrows(() => BowlingDecisionModel.ChooseDelivery(stock, 101, 55, situation, 1),
            "an out-of-range bowling rating was accepted");
        RequireThrows(() => BowlingDecisionModel.ChooseDelivery(stock, 55, 101, situation, 1),
            "an out-of-range striker rating was accepted");
        RequireThrows(() => BowlingDecisionModel.ChooseDelivery(stock, 55, 55,
                situation with { LegalBalls = 12 }, 1),
            "a delivery decision was accepted after the innings limit");

        Console.WriteLine("PASS: seeded CPU bowling decisions, tactical pressure, delivery control, pace, and variation.");
    }

    private static DeliveryPreset CreateStockDelivery() => new()
    {
        Name = "Review stock",
        ReleasePosition = new Vector3Data { X = 0f, Y = 2f, Z = 11.06f },
        ReleaseVelocity = new Vector3Data { X = 0f, Y = -1.6f, Z = -34f },
        GravityMetersPerSecondSquared = 9.81f,
        AirDragPerMeter = 0.00025f,
        BallRadiusMeters = 0.036f,
        PitchSurfaceHeightMeters = -0.025f,
        FieldSurfaceHeightMeters = -0.08f,
        PitchWidthMeters = 3.05f,
        PitchLengthMeters = 20.12f,
        PitchBounceRestitution = 0.53f,
        GroundBounceRestitution = 0.38f,
        TangentialRetention = 0.96f,
        RollingDecelerationMetersPerSecondSquared = 1.6f,
        FixedTimeStepSeconds = 1f / 120f,
        MaximumSimulationSeconds = 3.5f,
        FieldBoundaryRadiusMeters = 42f
    };

    private static bool NearlyEqual(Vector3 first, Vector3 second) => Vector3.DistanceSquared(first, second) < 0.000001f;

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Bowling decision check failed: {message}");
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }
        throw new InvalidOperationException($"Bowling decision check failed: {message}");
    }
}
