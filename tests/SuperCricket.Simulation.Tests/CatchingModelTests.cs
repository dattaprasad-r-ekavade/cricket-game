using SuperCricket.Simulation.Fielding;

namespace SuperCricket.Simulation.Tests;

public sealed class CatchingModelTests
{
    [Fact]
    public void HoldChance_UsesCatchingRatingReactionDistanceAndHeight()
    {
        var chestHigh = CatchingModel.GetHoldChance(100, reactionDistanceMeters: 0f, ballHeightAboveGroundMeters: 1.4f);
        var diving = CatchingModel.GetHoldChance(100, reactionDistanceMeters: 0f, ballHeightAboveGroundMeters: 0.72f);
        var longReaction = CatchingModel.GetHoldChance(100, reactionDistanceMeters: 20f, ballHeightAboveGroundMeters: 1.4f);
        var lowSkill = CatchingModel.GetHoldChance(50, reactionDistanceMeters: 0f, ballHeightAboveGroundMeters: 1.4f);

        Assert.Equal(0.9f, chestHigh, 6);
        Assert.Equal(0.3f, diving, 6);
        Assert.True(longReaction < chestHigh);
        Assert.True(lowSkill < chestHigh);
    }
}
