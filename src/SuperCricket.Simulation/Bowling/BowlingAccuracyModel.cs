using SuperCricket.Content;
using SuperCricket.Content.Bowling;

namespace SuperCricket.Simulation.Bowling;

public enum BowlingSkillLevel
{
    Rookie,
    Amateur,
    Standard,
    Pro
}

public readonly record struct BowlingAimResult(
    DeliveryPreset Delivery,
    float TargetRadiusMeters,
    float ScatterLineMeters,
    float ScatterLengthMeters,
    float Rhythm,
    float Stamina);

/// <summary>Seeded target accuracy, landing scatter, and stamina effects for a bowler.</summary>
public static class BowlingAccuracyModel
{
    public static BowlingAimResult Aim(
        DeliveryPreset delivery,
        float targetLineOffsetMeters,
        float targetLengthOffsetMeters,
        int accuracyRating,
        int ballsInSpell,
        float stamina,
        BowlingSkillLevel skillLevel,
        int seed,
        float minRadiusMeters = 0.12f,
        float maxRadiusMeters = 1.1f)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ValidateRating(accuracyRating, nameof(accuracyRating));
        if (ballsInSpell < 0) throw new ArgumentOutOfRangeException(nameof(ballsInSpell));
        if (!float.IsFinite(stamina) || stamina is < 0f or > 100f) throw new ArgumentOutOfRangeException(nameof(stamina));
        if (!Enum.IsDefined(skillLevel)) throw new ArgumentOutOfRangeException(nameof(skillLevel));
        if (!float.IsFinite(minRadiusMeters) || !float.IsFinite(maxRadiusMeters) || minRadiusMeters < 0f || maxRadiusMeters < minRadiusMeters)
            throw new ArgumentOutOfRangeException(nameof(maxRadiusMeters));
        if (!float.IsFinite(targetLineOffsetMeters) || MathF.Abs(targetLineOffsetMeters) > BowlingAimModel.MaximumLineOffsetMeters ||
            !float.IsFinite(targetLengthOffsetMeters) || MathF.Abs(targetLengthOffsetMeters) > BowlingAimModel.MaximumLengthOffsetMeters)
            throw new ArgumentOutOfRangeException(nameof(targetLineOffsetMeters));

        var rhythm = Math.Min(1f, ballsInSpell / 10f);
        var effectiveAccuracy = accuracyRating / 100f * rhythm;
        var radius = Lerp(maxRadiusMeters, minRadiusMeters, effectiveAccuracy);
        if (stamina < 50f) radius *= 1f + (50f - stamina) / 100f;

        var scatterScale = 1f - 0.65f * rhythm;
        var scatterEnabled = skillLevel is not (BowlingSkillLevel.Rookie or BowlingSkillLevel.Amateur);
        var random = new Random(seed);
        var scatterLine = scatterEnabled ? ((float)random.NextDouble() * 2f - 1f) * 0.25f * scatterScale : 0f;
        var scatterLength = scatterEnabled ? ((float)random.NextDouble() * 2f - 1f) * 0.25f * scatterScale : 0f;
        var aimed = BowlingAimModel.AimForPitchTarget(delivery,
            targetLineOffsetMeters + scatterLine, targetLengthOffsetMeters + scatterLength);
        return new BowlingAimResult(aimed, radius, scatterLine, scatterLength, rhythm, stamina);
    }

    public static float StaminaAfterOvers(float staminaRating, int oversBowled)
    {
        if (!float.IsFinite(staminaRating) || staminaRating is < 0f or > 100f) throw new ArgumentOutOfRangeException(nameof(staminaRating));
        if (oversBowled < 0) throw new ArgumentOutOfRangeException(nameof(oversBowled));
        var costPerOver = 8f - staminaRating / 20f;
        return Math.Clamp(100f - costPerOver * oversBowled, 0f, 100f);
    }

    private static float Lerp(float from, float to, float amount) => from + (to - from) * Math.Clamp(amount, 0f, 1f);
    private static void ValidateRating(int rating, string name)
    {
        if (rating is < 0 or > 100) throw new ArgumentOutOfRangeException(name, "Ratings must be between 0 and 100.");
    }
}

public enum PaceMeterDifficulty
{
    Easy,
    Normal,
    Hard
}

public enum DeliveryPaceBand
{
    Slow,
    Normal,
    Fast
}

public readonly record struct PaceMeterResult(DeliveryPaceBand Pace, float SpeedMultiplier, bool IsNoBall, float FillFraction);

/// <summary>Converts a stride-timed press to a discrete pace and front-foot result.</summary>
public static class PaceMeterModel
{
    public static float FillSeconds(PaceMeterDifficulty difficulty) => difficulty switch
    {
        PaceMeterDifficulty.Easy => 0.9f,
        PaceMeterDifficulty.Normal => 0.7f,
        PaceMeterDifficulty.Hard => 0.45f,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };

    public static PaceMeterResult Resolve(float? elapsedSeconds, PaceMeterDifficulty difficulty)
    {
        var fillSeconds = FillSeconds(difficulty);
        if (elapsedSeconds is null) return new PaceMeterResult(DeliveryPaceBand.Normal, 0.92f, false, 0f);
        if (!float.IsFinite(elapsedSeconds.Value) || elapsedSeconds.Value < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        var fill = Math.Clamp(elapsedSeconds.Value / fillSeconds, 0f, 1f);
        if (fill >= 0.92f) return new PaceMeterResult(DeliveryPaceBand.Fast, 1f, true, fill);
        if (fill < 1f / 3f) return new PaceMeterResult(DeliveryPaceBand.Slow, 0.82f, false, fill);
        if (fill < 2f / 3f) return new PaceMeterResult(DeliveryPaceBand.Normal, 0.92f, false, fill);
        return new PaceMeterResult(DeliveryPaceBand.Fast, 1f, false, fill);
    }

    public static DeliveryPreset CreateDelivery(
        DeliveryDefinition definition,
        DeliveryPreset stockDelivery,
        float targetLineOffsetMeters,
        float targetLengthOffsetMeters,
        float chosenSpeedKph,
        PitchType pitchType = PitchType.Normal)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(stockDelivery);
        if (!Enum.IsDefined(pitchType)) throw new ArgumentOutOfRangeException(nameof(pitchType));
        if (!float.IsFinite(chosenSpeedKph) || chosenSpeedKph < definition.MinSpeedKph || chosenSpeedKph > definition.MaxSpeedKph)
            throw new ArgumentOutOfRangeException(nameof(chosenSpeedKph), "Chosen speed must be inside the authored delivery range.");
        var result = stockDelivery.DeepCopy();
        var velocity = result.ReleaseVelocity.ToVector3();
        var baseSpeed = velocity.Length();
        if (baseSpeed <= 0f) throw new ArgumentException("Stock delivery must have a positive release speed.", nameof(stockDelivery));
        result.ReleaseVelocity = Vector3Data.From(System.Numerics.Vector3.Normalize(velocity) * (chosenSpeedKph / 3.6f));
        result.LateralAccelerationMetersPerSecondSquared = definition.SwingAcceleration + definition.SeamAcceleration;
        result.SideSpin = definition.SideSpin;
        result.TopSpin = definition.TopSpin;
        result.PitchSpinMultiplier = definition.Style == BowlerStyle.Pace ? 1f : definition.PitchSpinMultipliers[pitchType];
        result.Name = definition.Name;
        result = BowlingAimModel.AimForPitchTarget(result, targetLineOffsetMeters, targetLengthOffsetMeters);
        var errors = result.Validate();
        if (errors.Count > 0) throw new InvalidOperationException($"Catalogue delivery produced an invalid preset: {string.Join(" ", errors)}");
        return result;
    }
}
