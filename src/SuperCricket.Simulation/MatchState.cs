namespace SuperCricket.Simulation;

/// <summary>Owns the current delivery lifecycle and applies each completed result to the scorecard.</summary>
public sealed class MatchState
{
    private readonly OverScoreboard _scorecard = new();

    public OverScoreboard Scorecard => _scorecard;
    public DeliverySession? CurrentDelivery { get; private set; }

    public int Runs => _scorecard.Runs;
    public int Wickets => _scorecard.Wickets;
    public int LegalBalls => _scorecard.LegalBalls;
    public int Striker => _scorecard.Striker;
    public int NonStriker => _scorecard.NonStriker;
    public bool IsOverComplete => _scorecard.IsOverComplete;
    public string OversText => _scorecard.OversText;

    public void Reset()
    {
        _scorecard.Reset();
        CurrentDelivery = null;
    }

    public DeliverySession BeginDelivery(bool isNoBall)
    {
        if (_scorecard.IsOverComplete)
            throw new InvalidOperationException("The over is complete; start a new over before beginning another delivery.");
        if (CurrentDelivery is { IsComplete: false })
            throw new InvalidOperationException("The current delivery must be completed before beginning another delivery.");

        CurrentDelivery = new DeliverySession(isNoBall);
        return CurrentDelivery;
    }

    public DeliveryResult CompleteDelivery()
    {
        var currentDelivery = CurrentDelivery
            ?? throw new InvalidOperationException("Begin a delivery before completing it.");
        if (currentDelivery.IsComplete)
            return currentDelivery.Result!.Value;

        var result = currentDelivery.Complete();
        _scorecard.RecordDelivery(result);
        return result;
    }
}
