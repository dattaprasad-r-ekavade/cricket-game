using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

public readonly record struct CpuLiveBattingPlan(
    CpuShotChoice Shot,
    float InputDelaySeconds,
    float FootworkOffsetMeters,
    bool AttemptRun,
    float? PredictedContactQuality,
    float HorizontalAim);

/// <summary>Chooses a repeatable live shot, timing, footwork, placement, and running intent.</summary>
public static class CpuLiveBattingPlanModel
{
    public const float InputDelayStepSeconds = 0.025f;

    public static CpuLiveBattingPlan Choose(
        TeamPlayerData striker,
        TeamPlayerData bowler,
        IReadOnlyList<TeamPlayerData> fielders,
        BowlingSituation situation,
        FieldingTactic fieldingTactic,
        DeliveryPreset delivery,
        PlayerAsset batterAsset,
        PlayerAsset bowlerAsset,
        BattingShotSet shotSet,
        int seed,
        IReadOnlyList<Vector3>? fieldingPositions = null)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(batterAsset);
        ArgumentNullException.ThrowIfNull(bowlerAsset);
        ArgumentNullException.ThrowIfNull(shotSet);
        ArgumentNullException.ThrowIfNull(fielders);
        var outcome = CpuBattingOutcomeModel.Choose(striker, bowler, fielders, situation, fieldingTactic);
        var deliveryErrors = delivery.Validate();
        if (deliveryErrors.Count > 0)
            throw new ArgumentException($"Invalid CPU batting delivery: {string.Join(" ", deliveryErrors)}", nameof(delivery));

        var shotName = outcome.Shot switch
        {
            CpuShotChoice.Defence => "defence",
            CpuShotChoice.Drive => "drive",
            _ => "loft"
        };
        var samples = BattingPracticeAnalyzer.AnalyzeShot(
            batterAsset,
            bowlerAsset,
            shotSet,
            shotName,
            delivery,
            InputDelayStepSeconds,
            striker);
        var authoredShot = shotSet.Get(shotName);
        var plan = ChooseFromSamples(striker, outcome, delivery, samples, seed, authoredShot.HorizontalAim);
        if (fieldingPositions is null)
            return plan;

        var fieldingRatings = fielders.Select(fielder => fielder.Fielding).ToArray();
        var horizontalAim = CpuShotPlacementModel.ChooseHorizontalAim(
            authoredShot,
            striker.Power,
            delivery,
            fieldingPositions,
            fieldingRatings);
        var executedAim = CpuShotPlacementModel.ApplyExecutionError(
            horizontalAim,
            striker.Timing,
            striker.Power,
            seed ^ unchecked((int)0x4cf5ad43));
        return plan with { HorizontalAim = executedAim };
    }

    internal static CpuLiveBattingPlan ChooseFromSamples(
        TeamPlayerData striker,
        CpuBattingDecision outcome,
        DeliveryPreset delivery,
        IReadOnlyList<BattingPracticeSample> samples,
        int seed,
        float horizontalAim = 0f)
    {
        ArgumentNullException.ThrowIfNull(striker);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(samples);
        if (!float.IsFinite(horizontalAim) || horizontalAim is < -1f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(horizontalAim), "Shot direction must be between -1 and 1.");
        var random = new Random(seed);
        var wicketLineTime = BattingPracticeAnalyzer.GetWicketLineTimeSeconds(delivery);
        var liveSamples = samples
            .Where(sample => sample.InputDelaySeconds >= 0f && sample.InputDelaySeconds < wicketLineTime)
            .ToArray();
        if (liveSamples.Length == 0)
            throw new InvalidOperationException($"The delivery '{delivery.Name}' has no live CPU batting input timings.");

        var bestContact = liveSamples
            .Where(sample => sample.ContactQuality.HasValue &&
                sample.ContactTimeSeconds is { } contactTime && contactTime < wicketLineTime)
            .OrderByDescending(sample => sample.ContactQuality)
            .ThenBy(sample => MathF.Abs(sample.FootworkOffsetMeters))
            .FirstOrDefault();
        BattingPracticeSample selectedSample;
        if (bestContact.ContactQuality.HasValue)
        {
            // Lower timing ratings add a repeatable input error around the best physical timing.
            // The selected analyzer sample then predicts the actual contact or miss from that timing.
            var timingSkill = striker.Timing / 100f;
            var maximumTimingError = (1f - timingSkill) * 0.16f;
            var timingError = (float)(random.NextDouble() * 2.0 - 1.0) * maximumTimingError;
            var desiredInputDelay = Math.Clamp(
                bestContact.InputDelaySeconds + timingError,
                liveSamples[0].InputDelaySeconds,
                liveSamples[^1].InputDelaySeconds);
            selectedSample = liveSamples
                .OrderBy(sample => MathF.Abs(sample.InputDelaySeconds - desiredInputDelay))
                .ThenByDescending(sample => sample.ContactQuality)
                .ThenBy(sample => MathF.Abs(sample.FootworkOffsetMeters))
                .First();
        }
        else
        {
            selectedSample = liveSamples
                .OrderBy(sample => MathF.Abs(sample.InputDelaySeconds - 0.25f))
                .ThenBy(sample => MathF.Abs(sample.FootworkOffsetMeters))
                .First();
        }
        var attemptRun = random.NextDouble() < outcome.OneRunChance;
        return new CpuLiveBattingPlan(
            outcome.Shot,
            selectedSample.InputDelaySeconds,
            selectedSample.FootworkOffsetMeters,
            attemptRun,
            selectedSample.ContactQuality,
            horizontalAim);
    }
}
