using SuperCricket.Content;
using SuperCricket.Game;
using SuperCricket.Simulation;

namespace SuperCricket.Game.Tests;

public sealed class MatchControllerTests
{
    [Fact]
    public void CompletingBothInningsMarksTheFirstMatchAndRestartKeepsThatHistory()
    {
        var controller = new MatchController(new LimitedOversMatch(oversPerInnings: 1));
        controller.StartNewMatch(oversPerInnings: 1, seed: 491);

        Assert.True(controller.IsFirstMatch);
        for (var ball = 0; ball < OverScoreboard.BallsPerOver; ball++)
            CompleteBoundary(controller, clearedInTheAir: false);

        Assert.True(controller.Match.IsInningsComplete);
        Assert.False(controller.Match.IsMatchComplete);
        Assert.True(controller.IsFirstMatch);

        controller.StartNextInnings();
        Assert.Equal(2, controller.Match.InningsNumber);
        for (var ball = 0; ball < 5; ball++)
            CompleteBoundary(controller, clearedInTheAir: true);

        Assert.True(controller.Match.IsMatchComplete);
        Assert.False(controller.IsFirstMatch);

        controller.StartNewMatch(oversPerInnings: 1, seed: 491);

        Assert.Equal(1, controller.Match.InningsNumber);
        Assert.Equal(0, controller.Match.LegalBalls);
        Assert.False(controller.IsFirstMatch);
    }

    [Fact]
    public void BowlingDecisionSeedIsRepeatableAndChangesWithMatchProgress()
    {
        var controller = new MatchController(new LimitedOversMatch());
        controller.StartNewMatch(oversPerInnings: 1, seed: 1729);
        var initialSeed = controller.CreateBowlingDecisionSeed();

        Assert.Equal(initialSeed, controller.CreateBowlingDecisionSeed());

        CompleteBoundary(controller, clearedInTheAir: true);

        Assert.NotEqual(initialSeed, controller.CreateBowlingDecisionSeed());
    }

    [Fact]
    public void NextInningsRequiresTheFirstInningsToBeComplete()
    {
        var controller = new MatchController(new LimitedOversMatch());

        Assert.Throws<InvalidOperationException>(() => controller.StartNextInnings());
    }

    [Fact]
    public void PreparingHumanBattingDeliveryAppliesFirstMatchPaceWithoutMutatingSource()
    {
        var controller = new MatchController(new LimitedOversMatch());
        var source = CreateDelivery();

        var prepared = controller.PrepareHumanBattingDelivery(
            source,
            CpuDifficulty.Standard,
            humanBattingControlled: true,
            developerMode: false,
            verificationRun: false);

        Assert.NotSame(source, prepared);
        Assert.Equal(34f, source.ReleaseVelocity.ToVector3().Length());
        Assert.Equal(34f * DeliveryPaceModel.RookieOrFirstMatchSpeedMultiplier,
            prepared.ReleaseVelocity.ToVector3().Length(), precision: 4);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void PreparingIneligibleDeliveryPreservesOriginalReferenceAndSpeed(
        bool humanBattingControlled,
        bool developerMode,
        bool verificationRun)
    {
        var controller = new MatchController(new LimitedOversMatch());
        var source = CreateDelivery();

        var prepared = controller.PrepareHumanBattingDelivery(
            source,
            CpuDifficulty.Rookie,
            humanBattingControlled,
            developerMode,
            verificationRun);

        Assert.Same(source, prepared);
        Assert.Equal(34f, prepared.ReleaseVelocity.ToVector3().Length());
    }

    [Fact]
    public void PreparingLaterStandardHumanDeliveryKeepsAuthoredPace()
    {
        var controller = new MatchController(new LimitedOversMatch());
        controller.StartNewMatch(oversPerInnings: 1, seed: 491);
        for (var ball = 0; ball < OverScoreboard.BallsPerOver; ball++)
            CompleteBoundary(controller, clearedInTheAir: false);
        controller.StartNextInnings();
        for (var ball = 0; ball < 5; ball++)
            CompleteBoundary(controller, clearedInTheAir: true);

        var source = CreateDelivery();
        var prepared = controller.PrepareHumanBattingDelivery(
            source,
            CpuDifficulty.Standard,
            humanBattingControlled: true,
            developerMode: false,
            verificationRun: false);

        Assert.NotSame(source, prepared);
        Assert.Equal(34f, prepared.ReleaseVelocity.ToVector3().Length());
    }

    private static DeliveryPreset CreateDelivery() => new()
    {
        Name = "Controller pace test",
        ReleasePosition = new Vector3Data { X = 0f, Y = 1f, Z = 20f },
        ReleaseVelocity = new Vector3Data { X = 0f, Y = 0f, Z = -34f }
    };

    private static void CompleteBoundary(MatchController controller, bool clearedInTheAir)
    {
        controller.BeginDelivery(isNoBall: false)
            .ResolveBoundary(clearedInTheAir, currentRunCrossed: false);
        controller.CompleteDelivery();
    }
}
