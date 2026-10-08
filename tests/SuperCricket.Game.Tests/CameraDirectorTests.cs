using Microsoft.Xna.Framework;
using SuperCricket.Game.Rendering;
using SuperCricket.Simulation;

namespace SuperCricket.Game.Tests;

public sealed class CameraDirectorTests
{
    [Theory]
    [InlineData(false, "Behind striker")]
    [InlineData(true, "Bowler end")]
    public void RolePresetSelectionMatchesControlledBowlingSide(bool isHumanBowling, string expectedPreset)
    {
        var director = new CameraDirector();

        Assert.True(director.SelectRolePreset(isHumanBowling));
        Assert.Equal(expectedPreset, director.PresetName);
    }

    [Fact]
    public void RolePresetsFocusTheActiveCreaseAndCycleViews()
    {
        var director = new CameraDirector();

        Assert.True(director.SelectPreset("behind-striker"));
        Assert.Equal("Behind striker", director.PresetName);
        Assert.Equal(8f, director.Distance);
        Assert.Equal(CricketPitchGeometry.NearBatterAnchorZ, director.Target.Z);
        Assert.InRange(MathF.Abs(director.Position.X - director.Target.X), 0f, 0.001f);
        Assert.True(director.Position.Z < director.Target.Z, "Behind-striker view must sit on the near side of the batter, looking down the pitch.");

        director.CyclePreset();
        Assert.Equal("Bowler end", director.PresetName);
        Assert.Equal(CricketPitchGeometry.FarBatterAnchorZ, director.Target.Z);
    }

    [Fact]
    public void FollowBallAndTrackingMoveTheFocusTowardTheirTargets()
    {
        var director = new CameraDirector();
        director.SelectPreset("bowler-end");
        var initial = director.Target;
        var bowler = initial + new Vector3(2f, 0f, -3f);

        director.TrackTarget(bowler, 0.1f);
        Assert.True(director.Target.X > initial.X && director.Target.Z < initial.Z);
        Assert.True(director.Target.X < bowler.X && director.Target.Z > bowler.Z);

        director.SelectPreset("ball-follow");
        var ball = new Vector3(-4f, 2f, -10f);
        director.FollowBall(ball, 0f);
        Assert.Equal(ball, director.Target);

        var nextBallPosition = ball + Vector3.One;
        director.FollowBall(nextBallPosition, 0.1f);
        Assert.True(director.Target.X > ball.X && director.Target.X < nextBallPosition.X);
    }

    [Fact]
    public void ZoomIsClampedToThePlayerReadableRange()
    {
        var director = new CameraDirector();
        director.SelectPreset("behind-striker");

        director.ZoomBy(-100f);
        Assert.Equal(4.5f, director.Distance);

        director.ZoomBy(100f);
        Assert.Equal(28f, director.Distance);
    }
}
