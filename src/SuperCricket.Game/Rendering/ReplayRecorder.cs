using System;
using System.Linq;

namespace SuperCricket.Game.Rendering;

/// <summary>Retains a rolling window of already-renderable match poses for replay.</summary>
internal sealed class ReplayRecorder
{
    public const double DefaultWindowSeconds = 12d;
    public const int DefaultFrameCapacity = 1441;

    private readonly ReplayFrame?[] _frames;
    private readonly double _windowSeconds;
    private int _oldestIndex;
    private int _count;
    private bool _hasTimestamp;

    public ReplayRecorder(double windowSeconds = DefaultWindowSeconds, int frameCapacity = DefaultFrameCapacity)
    {
        if (!double.IsFinite(windowSeconds) || windowSeconds <= 0d)
            throw new ArgumentOutOfRangeException(nameof(windowSeconds));
        if (frameCapacity < 2)
            throw new ArgumentOutOfRangeException(nameof(frameCapacity));
        _windowSeconds = windowSeconds;
        _frames = new ReplayFrame[frameCapacity];
    }

    public int Count => _count;
    public double CurrentTimeSeconds => _count == 0 ? 0d : GetAt(_count - 1).TimestampSeconds;

    public void Record(ReplayFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!double.IsFinite(frame.TimestampSeconds) || frame.TimestampSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(frame), "Replay timestamps must be finite and non-negative.");
        if (_hasTimestamp && frame.TimestampSeconds < CurrentTimeSeconds)
            throw new ArgumentException("Replay timestamps must be monotonic.", nameof(frame));

        _hasTimestamp = true;
        if (_count == _frames.Length)
        {
            _frames[_oldestIndex] = null;
            _oldestIndex = (_oldestIndex + 1) % _frames.Length;
            _count--;
        }

        var writeIndex = (_oldestIndex + _count) % _frames.Length;
        _frames[writeIndex] = frame;
        _count++;
        TrimToWindow(frame.TimestampSeconds);
    }

    public ReplayFrame[] Snapshot() => Enumerable.Range(0, _count).Select(GetAt).ToArray();

    public ReplayFrame[] Snapshot(double startTimestampSeconds, double endTimestampSeconds)
    {
        if (!double.IsFinite(startTimestampSeconds) || !double.IsFinite(endTimestampSeconds) ||
            endTimestampSeconds < startTimestampSeconds)
            throw new ArgumentOutOfRangeException(nameof(endTimestampSeconds));
        if (_count == 0)
            return [];

        // Keep one sample on either side so playback can interpolate at the range edges.
        var startIndex = 0;
        while (startIndex + 1 < _count && GetAt(startIndex + 1).TimestampSeconds < startTimestampSeconds)
            startIndex++;
        var endIndex = startIndex;
        while (endIndex + 1 < _count && GetAt(endIndex).TimestampSeconds <= endTimestampSeconds)
            endIndex++;
        return Enumerable.Range(startIndex, endIndex - startIndex + 1).Select(GetAt).ToArray();
    }

    private void TrimToWindow(double newestTimestamp)
    {
        var earliest = newestTimestamp - _windowSeconds;
        while (_count > 1 && GetAt(0).TimestampSeconds < earliest)
        {
            _frames[_oldestIndex] = null;
            _oldestIndex = (_oldestIndex + 1) % _frames.Length;
            _count--;
        }
    }

    private ReplayFrame GetAt(int orderedIndex) =>
        _frames[(_oldestIndex + orderedIndex) % _frames.Length]
        ?? throw new InvalidOperationException("Replay buffer contains an empty frame slot.");
}
