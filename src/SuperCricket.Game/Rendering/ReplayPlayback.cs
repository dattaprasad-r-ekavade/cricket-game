using System;
using System.Collections.Generic;
using System.Linq;
using SuperCricket.Content.Replays;

namespace SuperCricket.Game.Rendering;

internal sealed class ReplayPlayback
{
    private readonly ReplayFrame[] _frames;
    private readonly double _eventTimestampSeconds;
    private readonly ReplayEventScript _script;
    private double _elapsedRealSeconds;

    public ReplayPlayback(
        ReplayEventScript script,
        double eventTimestampSeconds,
        ReplayFrame[] frames,
        float? projectedImpactX = null,
        float? projectedImpactHeightMeters = null,
        float? creaseDistanceMeters = null,
        string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(frames);
        if (!double.IsFinite(eventTimestampSeconds) || eventTimestampSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(eventTimestampSeconds));
        _script = script;
        _eventTimestampSeconds = eventTimestampSeconds;
        ProjectedImpactX = projectedImpactX;
        ProjectedImpactHeightMeters = projectedImpactHeightMeters;
        CreaseDistanceMeters = creaseDistanceMeters;
        Caption = caption;
        _frames = frames.OrderBy(frame => frame.TimestampSeconds).ToArray();
        if (_frames.Length == 0)
            throw new ArgumentException("Replay playback requires at least one recorded frame.", nameof(frames));
    }

    public bool IsComplete => _elapsedRealSeconds >= TotalRealDurationSeconds;
    public double TotalRealDurationSeconds => _script.Shots.Sum(shot => shot.DurationSeconds / shot.Speed);
    public string EventType => _script.EventType;
    public float? ProjectedImpactX { get; }
    public float? ProjectedImpactHeightMeters { get; }
    public float? CreaseDistanceMeters { get; }
    public string? Caption { get; }
    public string CurrentCameraPreset => GetCurrentShot().CameraPreset;
    public string CurrentOverlay => GetCurrentShot().Overlay;
    public ReplayFrame CurrentFrame => FindFrame(CurrentSourceTimestampSeconds());

    public void Advance(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        _elapsedRealSeconds = Math.Min(TotalRealDurationSeconds, _elapsedRealSeconds + elapsedSeconds);
    }

    public void Skip() => _elapsedRealSeconds = TotalRealDurationSeconds;

    private ReplayScriptShot GetCurrentShot()
    {
        var remaining = _elapsedRealSeconds;
        foreach (var shot in _script.Shots)
        {
            var shotRealDuration = shot.DurationSeconds / shot.Speed;
            if (remaining < shotRealDuration)
                return shot;
            remaining -= shotRealDuration;
        }
        return _script.Shots[^1];
    }

    private double CurrentSourceTimestampSeconds()
    {
        var remaining = _elapsedRealSeconds;
        foreach (var shot in _script.Shots)
        {
            var shotRealDuration = shot.DurationSeconds / shot.Speed;
            var sourceStart = _eventTimestampSeconds + shot.StartOffsetSeconds;
            if (remaining < shotRealDuration)
                return sourceStart + remaining * shot.Speed;
            remaining -= shotRealDuration;
        }

        var last = _script.Shots[^1];
        return _eventTimestampSeconds + last.StartOffsetSeconds + last.DurationSeconds;
    }

    private ReplayFrame FindFrame(double timestampSeconds)
    {
        if (timestampSeconds <= _frames[0].TimestampSeconds)
            return _frames[0];
        if (timestampSeconds >= _frames[^1].TimestampSeconds)
            return _frames[^1];
        var upper = Array.BinarySearch(_frames, new ReplayFrame(
            timestampSeconds,
            default,
            false,
            default,
            default,
            0f,
            [],
            []), ReplayFrameTimestampComparer.Instance);
        var nextIndex = upper >= 0 ? upper : ~upper;
        var previous = _frames[nextIndex - 1];
        var next = _frames[nextIndex];
        var span = next.TimestampSeconds - previous.TimestampSeconds;
        var amount = span <= 0d ? 0d : (timestampSeconds - previous.TimestampSeconds) / span;
        return ReplayFrame.Interpolate(previous, next, amount);
    }

    private sealed class ReplayFrameTimestampComparer : IComparer<ReplayFrame>
    {
        public static ReplayFrameTimestampComparer Instance { get; } = new();
        public int Compare(ReplayFrame? x, ReplayFrame? y) =>
            (x?.TimestampSeconds ?? 0d).CompareTo(y?.TimestampSeconds ?? 0d);
    }
}
