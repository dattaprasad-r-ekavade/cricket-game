namespace SuperCricket.Simulation;

public enum IncomingDeliveryResolution
{
    PassedBatAndWickets,
    Wide,
    NoBall,
    Bowled
}

/// <summary>Scoring and dismissal state for one delivery, independent of presentation.</summary>
public sealed class DeliverySession
{
    private int _batterRuns;
    private int _extraRuns;
    private int _completedRuns;
    private DeliveryExtra _extra;
    private DismissalKind _dismissal;
    private bool _isComplete;
    private DeliveryResult? _result;

    public DeliverySession(bool isNoBall)
    {
        IsNoBall = isNoBall;
        if (isNoBall)
        {
            _extraRuns = 1;
            _extra = DeliveryExtra.NoBall;
        }
    }

    public bool IsNoBall { get; }
    public bool IsComplete => _isComplete;
    public int BatterRuns => _batterRuns;
    public int ExtraRuns => _extraRuns;
    public int CompletedRuns => _completedRuns;
    public DeliveryExtra Extra => _extra;
    public DismissalKind Dismissal => _dismissal;
    public DeliveryResult? Result => _result;

    public IncomingDeliveryResolution ResolveIncoming(bool isWide, bool hitsWickets)
    {
        EnsureActive();
        if (IsNoBall)
            return IncomingDeliveryResolution.NoBall;
        if (isWide)
        {
            _extraRuns = 1;
            _extra = DeliveryExtra.Wide;
            return IncomingDeliveryResolution.Wide;
        }
        if (hitsWickets)
        {
            _dismissal = DismissalKind.Bowled;
            return IncomingDeliveryResolution.Bowled;
        }
        return IncomingDeliveryResolution.PassedBatAndWickets;
    }

    public void RecordCompletedRun()
    {
        EnsureActive();
        _batterRuns++;
        _completedRuns++;
    }

    /// <summary>Resolves the current ball against the boundary and any run in progress.</summary>
    public int ResolveBoundary(bool clearedInTheAir, bool currentRunCrossed)
    {
        EnsureActive();
        var boundaryRuns = clearedInTheAir ? 6 : 4;
        var runningRuns = _completedRuns + (currentRunCrossed ? 1 : 0);
        _batterRuns = Math.Max(boundaryRuns, runningRuns);
        _completedRuns = runningRuns > boundaryRuns ? runningRuns : 0;
        return _batterRuns;
    }

    /// <summary>Applies the match rules for a catch; a catch from a no-ball is not a dismissal.</summary>
    public bool ResolveCatch()
    {
        EnsureActive();
        if (IsNoBall)
            return false;

        _batterRuns = 0;
        _completedRuns = 0;
        _dismissal = DismissalKind.Caught;
        return true;
    }

    /// <summary>Credits a crossed run in progress at dead ball. Stoppage alone cannot dismiss a batter.</summary>
    public void ResolveDeadBall(bool currentRunCrossed)
    {
        EnsureActive();
        if (_dismissal != DismissalKind.None)
            throw new InvalidOperationException("Dead-ball run credit cannot follow a dismissal.");
        if (currentRunCrossed)
            RecordCompletedRun();
    }

    /// <summary>Applies a run-out at a broken wicket. Completed runs remain credited.</summary>
    public void ResolveRunOut()
    {
        EnsureActive();
        _dismissal = DismissalKind.RunOut;
    }

    internal DeliveryResult Complete()
    {
        if (_isComplete)
            return _result!.Value;

        var result = new DeliveryResult(
            _batterRuns,
            _extraRuns,
            _completedRuns,
            _extra is not (DeliveryExtra.Wide or DeliveryExtra.NoBall),
            _extra,
            _dismissal,
            DismissedEnd.Striker);
        var validationError = result.Validate();
        if (validationError is not null)
            throw new InvalidOperationException($"Delivery resolution produced an invalid result: {validationError}");

        _result = result;
        _isComplete = true;
        return result;
    }

    private void EnsureActive()
    {
        if (_isComplete)
            throw new InvalidOperationException("The delivery is complete; begin another delivery before changing its result.");
    }
}
