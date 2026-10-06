using SuperCricket.Content;

namespace SuperCricket.Simulation;

public enum BowlingVariation
{
    Stock,
    OutsideOff,
    Inswing,
    Outswing
}

public readonly record struct BowlingSituation(
    int LegalBalls,
    int OversPerInnings,
    int Runs,
    int Wickets,
    int? Target);

public readonly record struct BowlingDecision(
    BowlingVariation Variation,
    DeliveryPreset Delivery,
    float IntendedLineVelocityX,
    float AccuracyErrorVelocityX,
    float AccuracySpreadVelocityX,
    float Pressure);

/// <summary>Chooses a repeatable, skill-scaled delivery plan for the CPU bowler.</summary>
public static class BowlingDecisionModel
{
    public static BowlingDecision ChooseDelivery(
        DeliveryPreset stockDelivery,
        int bowlingRating,
        int strikerPower,
        BowlingSituation situation,
        int seed)
    {
        ArgumentNullException.ThrowIfNull(stockDelivery);
        if (bowlingRating is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(bowlingRating), "Bowling ratings must be between 0 and 100.");
        if (strikerPower is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(strikerPower), "Batting power must be between 0 and 100.");
        ValidateSituation(situation);
        var presetErrors = stockDelivery.Validate();
        if (presetErrors.Count > 0)
            throw new ArgumentException($"Invalid stock delivery: {string.Join(" ", presetErrors)}", nameof(stockDelivery));
        if (stockDelivery.IsNoBall)
            throw new ArgumentException("The CPU bowling model requires a legal stock delivery.", nameof(stockDelivery));

        var random = new Random(seed);
        var pressure = GetPressure(situation);
        var variation = SelectVariation(random.NextDouble(), strikerPower, situation.Wickets, pressure);
        var intendedLineX = GetIntendedLineVelocity(variation);
        var skill = bowlingRating / 100f;
        var accuracySpread = 0.85f - skill * 0.65f;
        var accuracyError = ((float)random.NextDouble() * 2f - 1f) * accuracySpread;
        var paceJitter = ((float)random.NextDouble() * 2f - 1f) * 0.035f;
        var paceScale = 0.92f + skill * 0.10f + paceJitter;

        var delivery = stockDelivery.DeepCopy();
        delivery.Name = $"{stockDelivery.Name} - {GetDisplayName(variation)}";
        delivery.ReleaseVelocity.X = stockDelivery.ReleaseVelocity.X + intendedLineX + accuracyError;
        delivery.ReleaseVelocity.Z = stockDelivery.ReleaseVelocity.Z * paceScale;
        delivery.LateralAccelerationMetersPerSecondSquared = GetMovementAcceleration(variation, skill);

        var deliveryErrors = delivery.Validate();
        if (deliveryErrors.Count > 0)
            throw new InvalidOperationException($"The CPU bowling model created an invalid delivery: {string.Join(" ", deliveryErrors)}");

        return new BowlingDecision(variation, delivery, intendedLineX, accuracyError, accuracySpread, pressure);
    }

    private static BowlingVariation SelectVariation(double roll, int strikerPower, int wickets, float pressure)
    {
        if (wickets >= 7)
            return roll < 0.58 ? BowlingVariation.Inswing
                : roll < 0.82 ? BowlingVariation.Stock
                : BowlingVariation.OutsideOff;

        if (pressure >= 0.75f || strikerPower >= 75)
            return roll < 0.43 ? BowlingVariation.OutsideOff
                : roll < 0.78 ? BowlingVariation.Outswing
                : BowlingVariation.Stock;

        if (pressure <= 0.30f)
            return roll < 0.48 ? BowlingVariation.Stock
                : roll < 0.80 ? BowlingVariation.Inswing
                : BowlingVariation.Outswing;

        return roll < 0.30 ? BowlingVariation.Stock
            : roll < 0.55 ? BowlingVariation.OutsideOff
            : roll < 0.78 ? BowlingVariation.Inswing
            : BowlingVariation.Outswing;
    }

    private static float GetIntendedLineVelocity(BowlingVariation variation) => variation switch
    {
        BowlingVariation.OutsideOff => 1.1f,
        BowlingVariation.Inswing => 0.35f,
        BowlingVariation.Outswing => -0.25f,
        _ => 0f
    };

    private static float GetMovementAcceleration(BowlingVariation variation, float skill) => variation switch
    {
        BowlingVariation.Inswing => -0.3f - skill * 1.2f,
        BowlingVariation.Outswing => 0.3f + skill * 1.2f,
        _ => 0f
    };

    private static string GetDisplayName(BowlingVariation variation) => variation switch
    {
        BowlingVariation.OutsideOff => "outside off",
        BowlingVariation.Inswing => "inswing",
        BowlingVariation.Outswing => "outswing",
        _ => "stock"
    };

    private static float GetPressure(BowlingSituation situation)
    {
        if (situation.Target is not { } target)
            return 0f;
        var ballsRemaining = Math.Max(1, situation.OversPerInnings * OverScoreboard.BallsPerOver - situation.LegalBalls);
        return Math.Clamp((target - situation.Runs) / (float)ballsRemaining, 0f, 1f);
    }

    private static void ValidateSituation(BowlingSituation situation)
    {
        if (situation.OversPerInnings is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(situation), "Overs per innings must be between 1 and 10.");
        if (situation.LegalBalls < 0 || situation.LegalBalls >= situation.OversPerInnings * OverScoreboard.BallsPerOver)
            throw new ArgumentOutOfRangeException(nameof(situation), "Legal balls must be within an unfinished innings.");
        if (situation.Runs < 0 || situation.Wickets is < 0 or >= OverScoreboard.MaximumWickets)
            throw new ArgumentOutOfRangeException(nameof(situation), "Runs and wickets must describe an unfinished innings.");
        if (situation.Target is <= 0)
            throw new ArgumentOutOfRangeException(nameof(situation), "A chase target must be positive when present.");
    }
}
