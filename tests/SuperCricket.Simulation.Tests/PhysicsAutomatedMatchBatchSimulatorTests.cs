using SuperCricket.Content;
using SuperCricket.Simulation;

namespace SuperCricket.Simulation.Tests;

public sealed class PhysicsAutomatedMatchBatchSimulatorTests
{
    [Fact]
    public void HumanBattingSideBatchIsDeterministicThroughPhysicsMatchRunner()
    {
        var paths = TestAssets.CpuBattingInputs();
        var firstTeam = TeamRosterAsset.Load(paths[5]);
        var secondTeam = TeamRosterAsset.Load(paths[6]);
        var field = FieldPreset.Load(paths[7]);
        var batter = PlayerAsset.Load(paths[0]);
        var bowler = PlayerAsset.Load(paths[1]);
        var shots = BattingShotSet.Load(paths[2]);
        var stock = DeliveryPreset.Load(paths[3]);
        var wide = DeliveryPreset.Load(paths[4]);
        var noBall = DeliveryPreset.Load(TestAssets.Asset("deliveries", "no-ball-pace.json"));

        var first = PhysicsAutomatedMatchBatchSimulator.RunBatch(
            firstTeam, secondTeam, field, batter, bowler, shots, stock, wide, noBall,
            matchCount: 1, oversPerInnings: 1, seed: 9100,
            difficulty: CpuDifficulty.Standard, battingSide: DifficultySide.Human);
        var second = PhysicsAutomatedMatchBatchSimulator.RunBatch(
            firstTeam, secondTeam, field, batter, bowler, shots, stock, wide, noBall,
            matchCount: 1, oversPerInnings: 1, seed: 9100,
            difficulty: CpuDifficulty.Standard, battingSide: DifficultySide.Human);

        Assert.Equal(first[0], second[0]);
    }

    [Fact]
    public void PadContact_FrontFootMovesDownPitchAndTowardTheBallLine()
    {
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        var frontPrevious = PadFrame(x: 0.1f, z: BattingPracticeAnalyzer.BatterWicketLineZ + 1.1f);
        var frontCurrent = PadFrame(x: 0.1f, z: BattingPracticeAnalyzer.BatterWicketLineZ - 0.9f);
        var backPrevious = PadFrame(x: 0.1f, z: BattingPracticeAnalyzer.BatterWicketLineZ + 1f);
        var backCurrent = PadFrame(x: 0.1f, z: BattingPracticeAnalyzer.BatterWicketLineZ - 1f);

        Assert.True(PhysicsAutomatedMatchBatchSimulator.TryFindPadContact(
            frontPrevious, frontCurrent, delivery, StrokeFoot.Front, out var frontContact));
        Assert.False(PhysicsAutomatedMatchBatchSimulator.TryFindPadContact(
            backPrevious, backCurrent, delivery, StrokeFoot.Back, out _));
        Assert.Equal(BattingPracticeAnalyzer.BatterWicketLineZ + 0.6f, frontContact.Position.Z, 4);
    }

    private static BallFlightFrame PadFrame(float x, float z) =>
        new(0f, new System.Numerics.Vector3(x, 0.5f, z), new System.Numerics.Vector3(0f, -1f, -30f), 0,
            BallMotionPhase.InFlight);
}
