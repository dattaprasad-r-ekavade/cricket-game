using SuperCricket.Content;

namespace SuperCricket.Simulation;

public enum CpuShotChoice
{
    Defence,
    Drive,
    Loft
}

public readonly record struct CpuBattingDecision(
    CpuShotChoice Shot,
    float Aggression,
    float Pressure,
    float WicketChance,
    float BoundaryChance,
    float OneRunChance,
    float TwoRunChance,
    float ThreeRunChance,
    float RunOutChance,
    float BoundaryClearedChance);

/// <summary>Maps batting, bowling, fielding, and match ratings into CPU match-outcome probabilities.</summary>
public static class CpuBattingOutcomeModel
{
    public static CpuBattingDecision Choose(
        TeamPlayerData striker,
        TeamPlayerData bowler,
        IReadOnlyList<TeamPlayerData> fielders,
        BowlingSituation situation,
        FieldingTactic fieldingTactic,
        CpuDifficulty difficulty = CpuDifficulty.Standard)
    {
        ArgumentNullException.ThrowIfNull(striker);
        ArgumentNullException.ThrowIfNull(bowler);
        ArgumentNullException.ThrowIfNull(fielders);
        ValidateSituation(situation);
        ValidatePlayerRatings(striker, "striker");
        if (bowler.Bowling is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(bowler), "Bowling rating must be between 0 and 100.");
        if (fielders.Count != FieldingSide.FielderCount)
            throw new ArgumentException($"A CPU batting decision requires exactly {FieldingSide.FielderCount} fielders.", nameof(fielders));

        var totalFielding = 0f;
        foreach (var fielder in fielders)
        {
            ArgumentNullException.ThrowIfNull(fielder);
            if (fielder.Fielding is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(fielders), "Fielding ratings must be between 0 and 100.");
            totalFielding += fielder.Fielding;
        }

        var ballsRemaining = Math.Max(1, situation.OversPerInnings * OverScoreboard.BallsPerOver - situation.LegalBalls);
        var difficultyTuning = CpuDifficultyModel.GetTuning(difficulty);
        var requiredRate = situation.Target is { } target
            ? Math.Max(0, target - situation.Runs) / (float)ballsRemaining
            : 0f;
        var pressure = Math.Clamp((requiredRate - 0.7f) / 1.8f, 0f, 1f);
        var timing = striker.Timing / 100f;
        var power = striker.Power / 100f;
        var fielding = totalFielding / (fielders.Count * 100f);
        var aggression = Math.Clamp(
            0.28f + pressure * 0.48f + (power - 0.5f) * 0.24f + difficultyTuning.BattingAggressionOffset,
            0.12f,
            0.92f);
        if (situation.Target is null && situation.Wickets >= 7)
            aggression *= 0.78f;

        var shot = ChooseShot(aggression, power, situation.Wickets);
        var wicketChance = shot switch
        {
            CpuShotChoice.Defence => 0.035f,
            CpuShotChoice.Drive => 0.065f,
            _ => 0.13f
        };
        wicketChance *= 1.12f - timing * 0.24f;
        wicketChance += pressure * 0.025f + bowler.Bowling / 100f * 0.025f + fielding * 0.012f;
        if (fieldingTactic == FieldingTactic.AttackWickets)
            wicketChance += 0.02f;
        wicketChance *= difficultyTuning.BattingWicketChanceMultiplier;

        var boundaryChance = shot switch
        {
            CpuShotChoice.Defence => 0.025f,
            CpuShotChoice.Drive => 0.09f,
            _ => 0.18f
        };
        boundaryChance += power * 0.08f + pressure * 0.04f;
        boundaryChance -= bowler.Bowling / 100f * 0.015f + fielding * 0.05f;
        if (fieldingTactic == FieldingTactic.ProtectBoundary)
            boundaryChance -= 0.055f;
        boundaryChance *= difficultyTuning.BattingBoundaryChanceMultiplier;

        var oneRunChance = 0.50f + aggression * 0.12f - fielding * 0.08f;
        if (fieldingTactic == FieldingTactic.ProtectBoundary)
            oneRunChance -= 0.04f;
        oneRunChance *= difficultyTuning.BattingRunChanceMultiplier;
        var runOutChance = 0.008f + (1f - timing) * 0.012f + fielding * 0.012f;
        runOutChance *= difficultyTuning.BattingRunOutChanceMultiplier;
        var boundaryClearedChance = shot == CpuShotChoice.Loft ? 0.78f - fielding * 0.18f : 0f;

        return new CpuBattingDecision(
            shot,
            aggression,
            pressure,
            Math.Clamp(wicketChance, 0f, 0.85f),
            Math.Clamp(boundaryChance, 0f, 0.85f),
            Math.Clamp(oneRunChance, 0f, 0.90f),
            Math.Clamp((0.16f + aggression * 0.06f) * difficultyTuning.BattingRunChanceMultiplier, 0f, 0.5f),
            Math.Clamp((0.03f + aggression * 0.07f) * difficultyTuning.BattingRunChanceMultiplier, 0f, 0.5f),
            Math.Clamp(runOutChance, 0f, 0.1f),
            Math.Clamp(boundaryClearedChance, 0f, 1f));
    }

    private static CpuShotChoice ChooseShot(float aggression, float power, int wickets)
    {
        if (aggression >= 0.64f && power >= 0.4f)
            return CpuShotChoice.Loft;
        if (aggression < 0.29f || wickets >= 9 && aggression < 0.52f)
            return CpuShotChoice.Defence;
        return CpuShotChoice.Drive;
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

    private static void ValidatePlayerRatings(TeamPlayerData player, string role)
    {
        if (player.Timing is < 0 or > 100 || player.Power is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(player), $"The {role}'s batting ratings must be between 0 and 100.");
    }
}
