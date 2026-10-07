using Microsoft.Xna.Framework.Input;
using SuperCricket.Game;

namespace SuperCricket.Game.Tests;

public sealed class BattingInputRecorderTests
{
    [Fact]
    public void SThenDRecordsAFrontFootDriveAndReplacesTheImmediateBlock()
    {
        var recorder = new BattingInputRecorder();

        var initial = recorder.Update(0f, [Keys.S]);
        var drive = recorder.Update(0.06f, [Keys.D]);

        Assert.Equal("defence", initial?.ShotName);
        Assert.Equal("defensive-block", initial?.AnimationClip);
        Assert.Equal(RecordedBattingStroke.FrontFootDrive, drive?.Stroke);
        Assert.Equal("S > D", drive?.ControlLabel);
        Assert.Equal("front-foot-drive", drive?.AnimationClip);
    }

    [Fact]
    public void DThenWRecordsABackFootDriveAndShiftUpgradesItToTheLoftClip()
    {
        var recorder = new BattingInputRecorder();

        Assert.Null(recorder.Update(0f, [Keys.D]));
        var drive = recorder.Update(0.04f, [Keys.W]);
        var loft = recorder.Update(0.03f, [Keys.LeftShift]);

        Assert.Equal(RecordedBattingStroke.BackFootDrive, drive?.Stroke);
        Assert.Equal("D > W", drive?.ControlLabel);
        Assert.Equal(RecordedBattingStroke.BackFootLoft, loft?.Stroke);
        Assert.Equal("D > W > SHIFT", loft?.ControlLabel);
        Assert.Equal("back-foot-loft", loft?.AnimationClip);
    }

    [Fact]
    public void ShiftAndSInEitherOrderSelectFrontFootLoft()
    {
        var recorder = new BattingInputRecorder();

        Assert.Null(recorder.Update(0f, [Keys.LeftShift]));
        var loft = recorder.Update(0.04f, [Keys.S]);

        Assert.Equal(RecordedBattingStroke.FrontFootLoft, loft?.Stroke);
        Assert.Equal("SHIFT > S", loft?.ControlLabel);
        Assert.Equal("lofted-drive", loft?.AnimationClip);
    }

    [Fact]
    public void SimultaneousChordKeysAreShownAsOneRecordedChord()
    {
        var recorder = new BattingInputRecorder();

        var loft = recorder.Update(0f, [Keys.D, Keys.S, Keys.LeftShift]);

        Assert.Equal(RecordedBattingStroke.FrontFootLoft, loft?.Stroke);
        Assert.Equal("SHIFT + S + D", loft?.ControlLabel);
    }

    [Theory]
    [InlineData(Keys.D)]
    [InlineData(Keys.W)]
    [InlineData(Keys.LeftShift)]
    public void IncompleteStrokeKeysDoNotSelectAnAnimation(Keys key)
    {
        var recorder = new BattingInputRecorder();

        Assert.Null(recorder.Update(0f, [key]));
    }

    [Fact]
    public void KeystrokesAfterTheChordWindowDoNotJoinThePreviousStroke()
    {
        var recorder = new BattingInputRecorder();

        Assert.Equal(RecordedBattingStroke.Defence, recorder.Update(0f, [Keys.S])?.Stroke);
        Assert.Null(recorder.Update(0.23f, [Keys.D]));
        Assert.False(recorder.HasPendingInput);
    }
}
