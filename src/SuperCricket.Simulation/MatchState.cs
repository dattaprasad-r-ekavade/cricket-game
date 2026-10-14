using SuperCricket.Simulation.Events;

namespace SuperCricket.Simulation;

/// <summary>Owns the current delivery lifecycle and applies each completed result to the scorecard.</summary>
public sealed class MatchState
{
    private readonly OverScoreboard _scorecard;
    private readonly int _inningsNumber;
    private int _deliveryNumber;

    public MatchState(int oversPerInnings = 1, MatchEventBus? eventBus = null, int inningsNumber = 1)
    {
        _scorecard = new OverScoreboard(oversPerInnings);
        Events = eventBus ?? new MatchEventBus();
        _inningsNumber = inningsNumber;
    }

    public OverScoreboard Scorecard => _scorecard;
    public MatchEventBus Events { get; }
    public DeliverySession? CurrentDelivery { get; private set; }

    public int Runs => _scorecard.Runs;
    public int Wickets => _scorecard.Wickets;
    public int LegalBalls => _scorecard.LegalBalls;
    public int Striker => _scorecard.Striker;
    public int NonStriker => _scorecard.NonStriker;
    public bool IsOverComplete => _scorecard.IsOverComplete;
    public string OversText => _scorecard.OversText;
    public int OversPerInnings => _scorecard.OversPerInnings;

    public void Reset()
    {
        _scorecard.Reset();
        Events.Reset();
        _deliveryNumber = 0;
        CurrentDelivery = null;
    }

    public DeliverySession BeginDelivery(bool isNoBall)
    {
        if (_scorecard.IsOverComplete)
            throw new InvalidOperationException("The over is complete; start a new over before beginning another delivery.");
        if (CurrentDelivery is { IsComplete: false })
            throw new InvalidOperationException("The current delivery must be completed before beginning another delivery.");

        Events.BeginDelivery(_inningsNumber, ++_deliveryNumber);
        CurrentDelivery = new DeliverySession(isNoBall, Events);
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
        if (_scorecard.LegalBalls > 0 && _scorecard.LegalBalls % OverScoreboard.BallsPerOver == 0)
            Events.Publish(new OverComplete(0f, _inningsNumber, _scorecard.LegalBalls / OverScoreboard.BallsPerOver));
        Events.CompleteDelivery(result);
        return result;
    }
}
