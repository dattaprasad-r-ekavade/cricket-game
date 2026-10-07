using System.Numerics;
using SuperCricket.Simulation;

namespace SuperCricket.Simulation.Tests;

public sealed class BattingShotDirectionTests
{
    [Fact]
    public void ShotDirectionUsesBothTheHorizontalAndDepthAim()
    {
        var forward = BattingImpactModel.GetHorizontalShotDirection(0f, 1f);
        var behind = BattingImpactModel.GetHorizontalShotDirection(0f, -1f);
        var diagonal = BattingImpactModel.GetHorizontalShotDirection(1f, 1f);

        Assert.Equal(Vector3.UnitZ, forward);
        Assert.Equal(-Vector3.UnitZ, behind);
        Assert.Equal(Vector3.Normalize(new Vector3(1f, 0f, 1f)), diagonal);
    }
}
