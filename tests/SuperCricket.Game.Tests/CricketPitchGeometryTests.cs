using SuperCricket.Game.Rendering;
using SuperCricket.Simulation;

namespace SuperCricket.Game.Tests;

public sealed class CricketPitchGeometryTests
{
    [Fact]
    public void RenderedWicketsUseTheIncomingDeliveryGeometry()
    {
        Assert.Equal(CricketPitchGeometry.PitchLengthMeters, PracticeGround.PitchLength);
        Assert.Equal(CricketPitchGeometry.PitchWidthMeters, PracticeGround.PitchWidth);
        Assert.Equal(CricketPitchGeometry.WicketHeightMeters, PracticeGround.WicketHeight);
        Assert.Equal(CricketPitchGeometry.NearWicketZ, -PracticeGround.WicketOffset);
        Assert.Equal(CricketPitchGeometry.FarWicketZ, PracticeGround.WicketOffset);
        Assert.Equal(-10.06f, CricketPitchGeometry.NearWicketZ);
        Assert.Equal(10.06f, CricketPitchGeometry.FarWicketZ);
    }
}
