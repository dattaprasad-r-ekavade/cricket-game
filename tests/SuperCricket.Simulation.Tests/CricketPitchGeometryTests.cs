namespace SuperCricket.Simulation.Tests;

public sealed class CricketPitchGeometryTests
{
    [Fact]
    public void PoppingCreasesAndBatterAnchorsUseTheSamePitchDimensions()
    {
        Assert.Equal(-8.84f, CricketPitchGeometry.NearPoppingCreaseZ, 2);
        Assert.Equal(8.84f, CricketPitchGeometry.FarPoppingCreaseZ, 2);
        Assert.Equal(-8.856f, BattingPracticeAnalyzer.BatterWicketLineZ, 3);
        Assert.Equal(CricketPitchGeometry.NearBatterAnchorZ, -8.856f, 3);
        Assert.Equal(CricketPitchGeometry.FarBatterAnchorZ, 8.856f, 3);
        Assert.Equal(CricketPitchGeometry.BatterAnchorOffsetTowardWicketMeters,
            CricketPitchGeometry.NearPoppingCreaseZ - BattingPracticeAnalyzer.BatterWicketLineZ, 3);
        Assert.Equal(CricketPitchGeometry.BatterAnchorOffsetTowardWicketMeters,
            CricketPitchGeometry.FarBatterAnchorZ - CricketPitchGeometry.FarPoppingCreaseZ, 3);
    }
}
