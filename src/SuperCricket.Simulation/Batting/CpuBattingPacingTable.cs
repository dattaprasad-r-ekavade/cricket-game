using SuperCricket.Content;

namespace SuperCricket.Simulation.Batting;

public enum CpuBattingPhase
{
    Early,
    Middle,
    Death
}

public readonly record struct CpuBattingPacingDecision(
    CpuBattingPhase Phase,
    float TargetRunsPerOver,
    float CurrentRunsPerOver,
    float RequiredRunsPerOver,
    int WantedAggression,
    float WantedReward);

/// <summary>Converts match phase, run-rate pressure, and batting style into a wanted stroke intent.</summary>
public static class CpuBattingPacingTable
{
    private const float EarlyPhaseEnd = 0.30f;
    private const float MiddlePhaseEnd = 0.75f;
    private const float RateGapForIntentShift = 2.5f;

    public static CpuBattingPacingDecision Choose(
        BowlingSituation situation,
        BatterStyle style,
        float difficultyAggressionOffset = 0f)
    {
        if (situation.OversPerInnings is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(situation), "Overs per innings must be between 1 and 20.");
        if (situation.LegalBalls < 0 || situation.LegalBalls >= situation.OversPerInnings * OverScoreboard.BallsPerOver)
            throw new ArgumentOutOfRangeException(nameof(situation), "Legal balls must be within an unfinished innings.");
        if (situation.Runs < 0 || situation.Target is <= 0)
            throw new ArgumentOutOfRangeException(nameof(situation), "Runs and target must be valid match values.");
        if (!Enum.IsDefined(style))
            throw new ArgumentOutOfRangeException(nameof(style));
        if (!float.IsFinite(difficultyAggressionOffset) || difficultyAggressionOffset is < -1f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(difficultyAggressionOffset));

        var progress = situation.LegalBalls / (float)(situation.OversPerInnings * OverScoreboard.BallsPerOver);
        var phase = progress < EarlyPhaseEnd
            ? CpuBattingPhase.Early
            : progress < MiddlePhaseEnd ? CpuBattingPhase.Middle : CpuBattingPhase.Death;
        var (targetRate, baseAggression) = phase switch
        {
            CpuBattingPhase.Early => (6.5f, 0),
            CpuBattingPhase.Middle => (7.5f, 1),
            _ => (10f, 2)
        };

        var currentRate = situation.Target is null && situation.LegalBalls == 0
            ? targetRate
            : situation.Runs * OverScoreboard.BallsPerOver /
                (float)Math.Max(OverScoreboard.BallsPerOver, situation.LegalBalls);
        var ballsRemaining = Math.Max(1, situation.OversPerInnings * OverScoreboard.BallsPerOver - situation.LegalBalls);
        var requiredRate = situation.Target is { } target
            ? Math.Max(0, target - situation.Runs) * OverScoreboard.BallsPerOver / (float)ballsRemaining
            : targetRate;
        var rateGap = (situation.Target is null ? targetRate : requiredRate) - currentRate;
        var gapShift = rateGap >= RateGapForIntentShift ? 1 : rateGap <= -RateGapForIntentShift ? -1 : 0;
        var styleShift = style switch
        {
            BatterStyle.Anchor => -1,
            BatterStyle.Aggressor => 1,
            _ => 0
        };
        var difficultyShift = difficultyAggressionOffset >= 0.1f ? 1 : difficultyAggressionOffset <= -0.1f ? -1 : 0;
        var wantedAggression = Math.Clamp(baseAggression + gapShift + styleShift + difficultyShift, 0, 2);
        var wantedReward = wantedAggression switch
        {
            0 => 0.35f,
            1 => 0.58f,
            _ => 0.82f
        };

        return new CpuBattingPacingDecision(
            phase,
            targetRate,
            currentRate,
            requiredRate,
            wantedAggression,
            wantedReward);
    }
}
