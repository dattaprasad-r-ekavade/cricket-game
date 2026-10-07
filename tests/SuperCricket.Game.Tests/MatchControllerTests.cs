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

    private static void CompleteBoundary(MatchController controller, bool clearedInTheAir)
    {
        controller.BeginDelivery(isNoBall: false)
            .ResolveBoundary(clearedInTheAir, currentRunCrossed: false);
        controller.CompleteDelivery();
    }
}
