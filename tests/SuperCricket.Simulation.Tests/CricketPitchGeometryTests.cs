namespace SuperCricket.Simulation.Tests;

public sealed class CricketPitchGeometryTests
{
    [Fact]
    public void PoppingCreasesAndBatterAnchorsUseTheSamePitchDimensions()
    {
        Assert.Equal(-8.84f, CricketPitchGeometry.NearPoppingCreaseZ, 2);
        Assert.Equal(8.84f, CricketPitchGeometry.FarPoppingCreaseZ, 2);
        Assert.Equal(-8.72f, BattingPracticeAnalyzer.BatterWicketLineZ, 2);
        Assert.Equal(CricketPitchGeometry.FarBatterAnchorZ, 8.72f, 2);
        Assert.Equal(CricketPitchGeometry.BatterAnchorInsetFromPoppingCreaseMeters,
            BattingPracticeAnalyzer.BatterWicketLineZ - CricketPitchGeometry.NearPoppingCreaseZ, 3);
        Assert.Equal(CricketPitchGeometry.BatterAnchorInsetFromPoppingCreaseMeters,
            CricketPitchGeometry.FarPoppingCreaseZ - CricketPitchGeometry.FarBatterAnchorZ, 3);
    }
}
