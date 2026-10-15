using Microsoft.Xna.Framework;
using SuperCricket.Content.Replays;
using SuperCricket.Game.Rendering;

namespace SuperCricket.Game.Tests;

public sealed class ReplayRecorderTests
{
    [Fact]
    public void RingBufferWrapRetainsNewestFramesInTimestampOrder()
    {
        var recorder = new ReplayRecorder(windowSeconds: 12d, frameCapacity: 3);
        recorder.Record(Frame(0d));
        recorder.Record(Frame(1d));
        recorder.Record(Frame(2d));
        recorder.Record(Frame(3d));

        Assert.Equal(3, recorder.Count);
        Assert.Equal([1d, 2d, 3d], recorder.Snapshot().Select(frame => frame.TimestampSeconds));
    }

    [Fact]
    public void TimeWindowDropsFramesOlderThanTwelveSecondsAndRejectsBackwardsTimestamps()
    {
        var recorder = new ReplayRecorder(windowSeconds: 2d, frameCapacity: 8);
        recorder.Record(Frame(0d));
        recorder.Record(Frame(1d));
        recorder.Record(Frame(2d));
        recorder.Record(Frame(3d));

        Assert.Equal([1d, 2d, 3d], recorder.Snapshot().Select(frame => frame.TimestampSeconds));
        Assert.Throws<ArgumentException>(() => recorder.Record(Frame(2.5d)));
    }

    [Fact]
    public void PlaybackUsesRecordedTimestampsAtQuarterAndHalfSpeedAndCanBeSkipped()
    {
        var frames = new[] { Frame(8d), Frame(9d), Frame(10d), Frame(11d), Frame(12d) };
        var script = new ReplayEventScript("lbw", [new ReplayScriptShot("recorded", -2f, 4f, 0.5f, "stump-line-strip")]);
        var playback = new ReplayPlayback(script, 10d, frames, projectedImpactX: 0.12f, projectedImpactHeightMeters: 0.8f);

        Assert.Equal(8d, playback.CurrentFrame.TimestampSeconds, precision: 4);
        playback.Advance(1f);
        Assert.Equal(8.5d, playback.CurrentFrame.TimestampSeconds, precision: 4);
        Assert.Equal(0.12f, playback.ProjectedImpactX);
        Assert.Equal("stump-line-strip", playback.CurrentOverlay);
        playback.Skip();
        Assert.True(playback.IsComplete);
    }

    private static ReplayFrame Frame(double timestamp) => new(
        timestamp,
        new Vector3((float)timestamp, 0f, 0f),
        true,
        Vector3.Zero,
        Vector3.UnitZ,
        45f,
        [],
        []);
}
