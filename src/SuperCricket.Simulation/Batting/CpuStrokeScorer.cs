using SuperCricket.Content;

namespace SuperCricket.Simulation.Batting;

public readonly record struct NormalizedCpuStrokeScoringWeights(float Fit, float Intent, float Reward, float Random);

public readonly record struct CpuStrokeScoreTerms(float Fit, float Intent, float Reward, float Random);

public readonly record struct CpuScoredStroke(StrokeDefinition Stroke, CpuStrokeScoreTerms Terms, float Score);

/// <summary>Scores authored strokes against the delivery, batter intent, reward, and seeded variation.</summary>
public static class CpuStrokeScorer
{
    public static NormalizedCpuStrokeScoringWeights NormalizeWeights(CpuStrokeScoringWeights weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var errors = weights.Validate();
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(weights));

        var sum = weights.Fit + weights.Intent + weights.Reward + weights.Random;
        return new NormalizedCpuStrokeScoringWeights(
            weights.Fit / sum,
            weights.Intent / sum,
            weights.Reward / sum,
            weights.Random / sum);
    }

    public static CpuStrokeScoreTerms EvaluateTerms(
        StrokeDefinition stroke,
        BallCategory ball,
        int wantedAggression,
        float wantedReward,
        float cpuSkill,
        float randomValue)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        if (wantedAggression is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(wantedAggression));
        if (!float.IsFinite(wantedReward) || wantedReward is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(wantedReward));
        if (!float.IsFinite(cpuSkill) || cpuSkill is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(cpuSkill));
        if (!float.IsFinite(randomValue) || randomValue is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(randomValue));
        if (stroke.Suits?.Lengths is null || stroke.Suits.Lines is null ||
            stroke.Suits.Lengths.Count == 0 || stroke.Suits.Lines.Count == 0)
            throw new ArgumentException($"Stroke '{stroke.Id}' must suit at least one length and line.", nameof(stroke));
        if (stroke.Aggression is < 0 or > 2 ||
            !float.IsFinite(stroke.Difficulty) || stroke.Difficulty is < 0f or > 1f ||
            !float.IsFinite(stroke.Reward) || stroke.Reward is < 0f or > 1f)
            throw new ArgumentException($"Stroke '{stroke.Id}' has invalid scoring values.", nameof(stroke));

        var lengthFit = stroke.Suits.Lengths.Contains(ball.Length) ? 1f : 0f;
        var lineFit = stroke.Suits.Lines.Contains(ball.Line) ? 1f : 0f;
        var aggressionDistance = Math.Abs(stroke.Aggression - wantedAggression);
        var intent = aggressionDistance == 0 ? 1f : aggressionDistance == 1 ? 0.1f : 0f;
        var skillAdjustedDifficulty = 1f - Math.Max(0f, stroke.Difficulty - cpuSkill);
        var rewardMatch = 1f - MathF.Abs(stroke.Reward - wantedReward);
        return new CpuStrokeScoreTerms(
            (lengthFit + lineFit) * 0.5f,
            intent,
            Math.Clamp(skillAdjustedDifficulty, 0f, 1f) * Math.Clamp(rewardMatch, 0f, 1f),
            randomValue);
    }

    public static CpuScoredStroke Select(
        IReadOnlyList<StrokeDefinition> strokes,
        BallCategory ball,
        int wantedAggression,
        float wantedReward,
        float cpuSkill,
        CpuStrokeScoringWeights weights,
        int seed)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        if (strokes.Count == 0)
            throw new ArgumentException("At least one candidate stroke is required.", nameof(strokes));
        var normalized = NormalizeWeights(weights);
        var random = new Random(seed);
        CpuScoredStroke? best = null;
        foreach (var stroke in strokes)
        {
            var terms = EvaluateTerms(stroke, ball, wantedAggression, wantedReward, cpuSkill, (float)random.NextDouble());
            var score = normalized.Fit * terms.Fit +
                normalized.Intent * terms.Intent +
                normalized.Reward * terms.Reward +
                normalized.Random * terms.Random;
            var candidate = new CpuScoredStroke(stroke, terms, score);
            if (best is null || candidate.Score > best.Value.Score ||
                candidate.Score == best.Value.Score && string.CompareOrdinal(candidate.Stroke.Id, best.Value.Stroke.Id) < 0)
                best = candidate;
        }
        return best!.Value;
    }
}
