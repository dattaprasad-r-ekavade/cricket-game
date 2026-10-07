namespace SuperCricket.Simulation;

public enum RunMovementResult { None, CompletedRun, ReturnedHome }
public enum WicketEnd { Near, Far }
public readonly record struct RunOutResolution(DismissedEnd DismissedEnd, bool SwapEnds);

/// <summary>Continuous equal-speed running, including returns and ground ownership at either wicket.</summary>
public sealed class BetweenWicketsState
{
    public bool IsMoving { get; private set; }
    public bool IsReturning { get; private set; }
    private double _progress;
    private bool _groundsSwapped;
    private bool _deliveryStopped;
    public float Progress => (float)_progress;
    public int CompletedRuns { get; private set; }
    public bool CurrentRunCrossed => RunningScoringModel.HasCrossed(Progress, 1f);
    public bool StrikerStartsNear => CompletedRuns % 2 == 0;
    public float StrikerPositionFraction => StrikerStartsNear ? Progress : 1f - Progress;
    public float NonStrikerPositionFraction => 1f - StrikerPositionFraction;

    public void Reset()
    {
        IsMoving = IsReturning = _groundsSwapped = _deliveryStopped = false;
        _progress = 0d;
        CompletedRuns = 0;
    }

    public bool StartRun()
    {
        if (IsMoving || _deliveryStopped)
            return false;
        IsMoving = true;
        IsReturning = _groundsSwapped = false;
        _progress = 0d;
        return true;
    }

    public void TurnBack()
    {
        if (IsMoving)
            IsReturning = true;
    }

    public RunMovementResult Advance(float deltaSeconds, float runDurationSeconds) =>
        Advance(deltaSeconds, runDurationSeconds, out _);

    public RunMovementResult Advance(float deltaSeconds, float runDurationSeconds, out float unusedSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (!float.IsFinite(runDurationSeconds) || runDurationSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(runDurationSeconds));
        unusedSeconds = 0f;
        if (!IsMoving)
            return RunMovementResult.None;

        var timeToEnd = (IsReturning ? _progress : 1d - _progress) * runDurationSeconds;
        if (deltaSeconds >= timeToEnd)
        {
            unusedSeconds = (float)(deltaSeconds - timeToEnd);
            _progress = IsReturning ? 0d : 1d;
        }
        else
            _progress += (IsReturning ? -1d : 1d) * deltaSeconds / runDurationSeconds;
        // At exact equality, retain the ground owner from immediately before drawing level (Law 30.2.3).
        if (_progress != 0.5d)
            _groundsSwapped = _progress > 0.5d;
        if (IsReturning && _progress == 0d)
        {
            IsMoving = IsReturning = false;
            return RunMovementResult.ReturnedHome;
        }
        if (!IsReturning && _progress == 1d)
        {
            CompleteRun();
            return RunMovementResult.CompletedRun;
        }
        return RunMovementResult.None;
    }

    /// <summary>Commits a completed or dead-ball-credited run and places the batters at their new ends.</summary>
    public void CompleteRun()
    {
        CompletedRuns++;
        _progress = 0d;
        IsMoving = IsReturning = _groundsSwapped = false;
    }

    /// <summary>Freezes the current positions at delivery completion; reset only when the next delivery starts.</summary>
    public void Stop()
    {
        IsMoving = IsReturning = false;
        _deliveryStopped = true;
    }

    public bool TryResolveRunOut(WicketEnd wicket, bool wicketBroken, out RunOutResolution resolution)
    {
        if (!Enum.IsDefined(wicket))
            throw new ArgumentOutOfRangeException(nameof(wicket));
        resolution = default;
        if (!wicketBroken || _deliveryStopped || _progress is <= 0d or >= 1d)
            return false;
        // DismissedEnd describes the physical end after completed runs and this uncompleted crossing.
        resolution = new RunOutResolution(wicket == WicketEnd.Near ? DismissedEnd.Striker : DismissedEnd.NonStriker,
            _groundsSwapped);
        return true;
    }
}
