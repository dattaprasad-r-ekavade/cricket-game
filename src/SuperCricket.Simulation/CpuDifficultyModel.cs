namespace SuperCricket.Simulation;

public enum CpuDifficulty
{
    Rookie,
    Standard,
    Pro
}

public readonly record struct CpuDifficultyTuning(
    float BattingTimingErrorMultiplier,
    float ShotPlacementErrorMultiplier,
    float BowlingAccuracySpreadMultiplier,
    float BattingAggressionOffset,
    float BattingWicketChanceMultiplier,
    float BattingBoundaryChanceMultiplier,
    float BattingRunChanceMultiplier,
    float BattingRunOutChanceMultiplier);

/// <summary>Applies difficulty through opponent decision quality while leaving ball and scoring rules fixed.</summary>
public static class CpuDifficultyModel
{
    private static readonly CpuDifficulty[] OrderedDifficulties =
    [
        CpuDifficulty.Rookie,
        CpuDifficulty.Standard,
        CpuDifficulty.Pro
    ];

    public static IReadOnlyList<CpuDifficulty> Difficulties => Array.AsReadOnly(OrderedDifficulties);

    public static CpuDifficultyTuning GetTuning(CpuDifficulty difficulty) => difficulty switch
    {
        CpuDifficulty.Rookie => new CpuDifficultyTuning(
            BattingTimingErrorMultiplier: 1.65f,
            ShotPlacementErrorMultiplier: 1.4f,
            BowlingAccuracySpreadMultiplier: 1.5f,
            BattingAggressionOffset: -0.15f,
            BattingWicketChanceMultiplier: 1.25f,
            BattingBoundaryChanceMultiplier: 0.75f,
            BattingRunChanceMultiplier: 0.8f,
            BattingRunOutChanceMultiplier: 1.25f),
        CpuDifficulty.Standard => new CpuDifficultyTuning(
            BattingTimingErrorMultiplier: 1f,
            ShotPlacementErrorMultiplier: 1f,
            BowlingAccuracySpreadMultiplier: 1f,
            BattingAggressionOffset: 0f,
            BattingWicketChanceMultiplier: 1f,
            BattingBoundaryChanceMultiplier: 1f,
            BattingRunChanceMultiplier: 1f,
            BattingRunOutChanceMultiplier: 1f),
        CpuDifficulty.Pro => new CpuDifficultyTuning(
            BattingTimingErrorMultiplier: 0.65f,
            ShotPlacementErrorMultiplier: 0.7f,
            BowlingAccuracySpreadMultiplier: 0.7f,
            BattingAggressionOffset: 0.1f,
            BattingWicketChanceMultiplier: 0.9f,
            BattingBoundaryChanceMultiplier: 1.2f,
            BattingRunChanceMultiplier: 1.1f,
            BattingRunOutChanceMultiplier: 0.9f),
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown CPU difficulty.")
    };

    public static CpuDifficulty Next(CpuDifficulty difficulty)
    {
        var index = Array.IndexOf(OrderedDifficulties, difficulty);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown CPU difficulty.");
        return OrderedDifficulties[(index + 1) % OrderedDifficulties.Length];
    }
}
