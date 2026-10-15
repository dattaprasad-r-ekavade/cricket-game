using SuperCricket.Content;
using SuperCricket.Simulation.Batting;

namespace SuperCricket.Simulation.Tests;

public sealed class CpuStrokeScorerTests
{
    private static readonly BallCategory GoodStumps = new(BallLength.Good, BallLine.Stumps, 0.5f);

    [Fact]
    public void NormalizeWeights_ScalesAllTermsToOne()
    {
        var weights = CpuStrokeScorer.NormalizeWeights(new CpuStrokeScoringWeights
        {
            Fit = 2f,
            Intent = 1f,
            Reward = 1f,
            Random = 0f
        });

        Assert.Equal(1f, weights.Fit + weights.Intent + weights.Reward + weights.Random, 6);
        Assert.Equal(0.5f, weights.Fit, 6);
        Assert.Equal(0.25f, weights.Intent, 6);
        Assert.Equal(0.25f, weights.Reward, 6);
    }

    [Fact]
    public void FitTerm_AveragesLengthAndLineMatches()
    {
        var terms = CpuStrokeScorer.EvaluateTerms(Stroke(lengths: [BallLength.Full]), GoodStumps,
            wantedAggression: 1, wantedReward: 0.5f, cpuSkill: 0.5f, randomValue: 0f);

        Assert.Equal(0.5f, terms.Fit);
    }

    [Fact]
    public void IntentTerm_IsExactOneNotchPointOneAndTwoNotchesZero()
    {
        var exact = CpuStrokeScorer.EvaluateTerms(Stroke(aggression: 1), GoodStumps, 1, 0.5f, 0.5f, 0f);
        var adjacent = CpuStrokeScorer.EvaluateTerms(Stroke(aggression: 2), GoodStumps, 1, 0.5f, 0.5f, 0f);
        var distant = CpuStrokeScorer.EvaluateTerms(Stroke(aggression: 0), GoodStumps, 2, 0.5f, 0.5f, 0f);

        Assert.Equal(1f, exact.Intent);
        Assert.Equal(0.1f, adjacent.Intent, 6);
        Assert.Equal(0f, distant.Intent);
    }

    [Fact]
    public void RewardTerm_AppliesDifficultyAndWantedRewardPenalties()
    {
        var stroke = Stroke(difficulty: 0.8f, reward: 0.7f);

        var terms = CpuStrokeScorer.EvaluateTerms(stroke, GoodStumps, 1,
            wantedReward: 0.4f, cpuSkill: 0.3f, randomValue: 0f);

        Assert.Equal(0.35f, terms.Reward, 5);
    }

    [Fact]
    public void RandomTerm_UsesTheSeededUnitIntervalSample()
    {
        var terms = CpuStrokeScorer.EvaluateTerms(Stroke(), GoodStumps, 1, 0.5f, 0.5f, randomValue: 0.73f);

        Assert.Equal(0.73f, terms.Random, 6);
    }

    [Fact]
    public void Select_IsDeterministicForTheSameSeedAndCandidates()
    {
        var strokes = new[]
        {
            Stroke(id: "drive", aggression: 1, difficulty: 0.4f, reward: 0.6f),
            Stroke(id: "loft", aggression: 2, difficulty: 0.7f, reward: 0.8f)
        };
        var weights = new CpuStrokeScoringWeights { Fit = 0.3f, Intent = 0.3f, Reward = 0.3f, Random = 0.1f };

        var first = CpuStrokeScorer.Select(strokes, GoodStumps, 1, 0.6f, 0.7f, weights, seed: 1234);
        var replay = CpuStrokeScorer.Select(strokes, GoodStumps, 1, 0.6f, 0.7f, weights, seed: 1234);

        Assert.Equal(first.Stroke.Id, replay.Stroke.Id);
        Assert.Equal(first.Score, replay.Score);
    }

    [Fact]
    public void CpuBattingOutcomeModel_UsesScoredAuthoredStrokeWhenAssetsAreProvided()
    {
        var table = StrokeTableAsset.Load(TestAssets.Asset("batting", "strokes.json"));
        var striker = new TeamPlayerData { Id = "batter-1", BattingStyle = BatterStyle.Balanced };
        var bowler = new TeamPlayerData { Id = "bowler-1" };
        var fielders = Enumerable.Range(0, FieldingSide.FielderCount)
            .Select(index => new TeamPlayerData { Id = $"fielder-{index}" })
            .ToArray();
        var situation = new BowlingSituation(LegalBalls: 0, OversPerInnings: 5, Runs: 0, Wickets: 0, Target: null);

        var first = CpuBattingOutcomeModel.Choose(
            striker, bowler, fielders, situation, FieldingTactic.Balanced,
            CpuDifficulty.Standard, table, GoodStumps, scoringSeed: 8142);
        var replay = CpuBattingOutcomeModel.Choose(
            striker, bowler, fielders, situation, FieldingTactic.Balanced,
            CpuDifficulty.Standard, table, GoodStumps, scoringSeed: 8142);

        Assert.NotNull(first.SelectedStroke);
        Assert.Equal(first.SelectedStroke.Id, replay.SelectedStroke?.Id);
        Assert.Equal(first.SelectedStroke.Loft ? CpuShotChoice.Loft :
            first.SelectedStroke.Intent == StrokeIntent.Defend ? CpuShotChoice.Defence : CpuShotChoice.Drive, first.Shot);
    }

    [Fact]
    public void PacingTable_ScalesPhasesAndShiftsForRunRateAndStyle()
    {
        var early = CpuBattingPacingTable.Choose(
            new BowlingSituation(LegalBalls: 0, OversPerInnings: 20, Runs: 0, Wickets: 0, Target: null),
            BatterStyle.Balanced);
        var middleAnchor = CpuBattingPacingTable.Choose(
            new BowlingSituation(LegalBalls: 60, OversPerInnings: 20, Runs: 75, Wickets: 1, Target: null),
            BatterStyle.Anchor);
        var middleBalanced = CpuBattingPacingTable.Choose(
            new BowlingSituation(LegalBalls: 60, OversPerInnings: 20, Runs: 75, Wickets: 1, Target: null),
            BatterStyle.Balanced);
        var middleAggressor = CpuBattingPacingTable.Choose(
            new BowlingSituation(LegalBalls: 60, OversPerInnings: 20, Runs: 75, Wickets: 1, Target: null),
            BatterStyle.Aggressor);
        var pressured = CpuBattingPacingTable.Choose(
            new BowlingSituation(LegalBalls: 30, OversPerInnings: 20, Runs: 20, Wickets: 1, Target: 180),
            BatterStyle.Balanced);
        var death = CpuBattingPacingTable.Choose(
            new BowlingSituation(LegalBalls: 90, OversPerInnings: 20, Runs: 110, Wickets: 3, Target: null),
            BatterStyle.Balanced);

        Assert.Equal(CpuBattingPhase.Early, early.Phase);
        Assert.Equal(CpuBattingPhase.Middle, middleBalanced.Phase);
        Assert.Equal(CpuBattingPhase.Death, death.Phase);
        Assert.Equal(middleBalanced.WantedAggression - 1, middleAnchor.WantedAggression);
        Assert.Equal(middleBalanced.WantedAggression + 1, middleAggressor.WantedAggression);
        Assert.True(pressured.WantedAggression > early.WantedAggression);
        Assert.True(death.TargetRunsPerOver > middleBalanced.TargetRunsPerOver);
        Assert.True(death.WantedReward > middleBalanced.WantedReward);
    }

    private static StrokeDefinition Stroke(
        string id = "candidate",
        int aggression = 1,
        float difficulty = 0.5f,
        float reward = 0.5f,
        IReadOnlyList<BallLength>? lengths = null,
        IReadOnlyList<BallLine>? lines = null) => new()
    {
        Id = id,
        Aggression = aggression,
        Difficulty = difficulty,
        Reward = reward,
        Suits = new StrokeSuit
        {
            Lengths = lengths?.ToList() ?? [BallLength.Good],
            Lines = lines?.ToList() ?? [BallLine.Stumps]
        }
    };

}
