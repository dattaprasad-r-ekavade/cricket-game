using SuperCricket.Content;

namespace SuperCricket.Simulation.Tests;

public sealed class CpuLiveBattingTimingTests
{
    [Fact]
    public void NewCpuBatterUsesSettleCurveStartEvenLateInTheInnings()
    {
        var paths = TestAssets.CpuBattingInputs();
        var batterAsset = PlayerAsset.Load(paths[0]);
        var bowlerAsset = PlayerAsset.Load(paths[1]);
        var shotSet = BattingShotSet.Load(paths[2]);
        var delivery = DeliveryPreset.Load(paths[3]);
        var battingTeam = TeamRosterAsset.Load(paths[5]);
        var fieldingTeam = TeamRosterAsset.Load(paths[6]);
        var striker = battingTeam.GetPlayerAtBattingOrder(1);
        var bowler = fieldingTeam.Players.First(player =>
            player.Role.Equals("Bowler", StringComparison.OrdinalIgnoreCase));
        var fielders = fieldingTeam.Players.Where(player => player.Id != bowler.Id).ToArray();
        var strokeTable = StrokeTableAsset.Load(TestAssets.Asset("batting", "strokes.json"));
        var situation = new BowlingSituation(LegalBalls: 54, OversPerInnings: 10, Runs: 100, Wickets: 2, Target: null);
        var newBatterState = new BatterTimingState();
        var profile = CpuDifficultyModel.GetProfile(CpuDifficulty.Standard, DifficultySide.Cpu);

        var plan = CpuLiveBattingPlanModel.Choose(
            striker,
            bowler,
            fielders,
            situation,
            FieldingTactic.Balanced,
            delivery,
            batterAsset,
            bowlerAsset,
            shotSet,
            seed: 1907,
            strokeTable: strokeTable,
            timingState: newBatterState);

        Assert.False(plan.Leave);
        Assert.NotNull(plan.StrokeOutcome);
        Assert.Equal(0, newBatterState.BallsFaced);
        var stroke = strokeTable.Strokes.Single(item => item.Id == plan.StrokeOutcome!.Value.StrokeId);
        var expectedInitialWidth = TimingWindowModel.IdealWidth(
            profile,
            newBatterState,
            striker,
            stroke.Foot,
            stroke.Direction,
            stroke.Loft);
        var fullySettledWidth = TimingWindowModel.IdealWidth(
            profile,
            new BatterTimingState(situation.LegalBalls),
            striker,
            stroke.Foot,
            stroke.Direction,
            stroke.Loft);

        Assert.Equal(expectedInitialWidth, plan.IdealWidthMeters!.Value, 5);
        Assert.True(plan.IdealWidthMeters.Value < fullySettledWidth);
    }
}
