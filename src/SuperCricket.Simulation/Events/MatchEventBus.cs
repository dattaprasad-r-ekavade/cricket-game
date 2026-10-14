using SuperCricket.Simulation;

namespace SuperCricket.Simulation.Events;

/// <summary>Publishes typed match events and retains completed per-delivery logs.</summary>
public sealed class MatchEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscriptions = [];
    private readonly List<DeliveryEventLog> _deliveryLogs = [];
    private List<MatchEvent>? _currentEvents;
    private int _currentInnings;
    private int _currentDelivery;

    public IReadOnlyList<DeliveryEventLog> DeliveryLogs => Array.AsReadOnly(_deliveryLogs.ToArray());

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : MatchEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        var eventType = typeof(TEvent);
        if (!_subscriptions.TryGetValue(eventType, out var handlers))
            _subscriptions.Add(eventType, handlers = []);
        handlers.Add(handler);
        return new Subscription(this, eventType, handler);
    }

    public void BeginDelivery(int inningsNumber, int deliveryNumber)
    {
        if (_currentEvents is not null)
            throw new InvalidOperationException("The current delivery event log must be completed before another begins.");
        if (inningsNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(inningsNumber));
        if (deliveryNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(deliveryNumber));

        _currentInnings = inningsNumber;
        _currentDelivery = deliveryNumber;
        _currentEvents = [];
        Publish(new DeliveryReleased(0f, inningsNumber, deliveryNumber));
    }

    public void Publish<TEvent>(TEvent matchEvent) where TEvent : MatchEvent
    {
        ArgumentNullException.ThrowIfNull(matchEvent);
        if (!float.IsFinite(matchEvent.TimeSeconds))
            throw new ArgumentOutOfRangeException(nameof(matchEvent), "Event time must be finite.");

        _currentEvents?.Add(matchEvent);
        if (_subscriptions.TryGetValue(typeof(TEvent), out var handlers))
        {
            foreach (var handler in handlers.ToArray())
                ((Action<TEvent>)handler)(matchEvent);
        }
    }

    public DeliveryEventLog CompleteDelivery(DeliveryResult result)
    {
        var currentEvents = _currentEvents
            ?? throw new InvalidOperationException("Begin a delivery before completing its event log.");
        var validationError = result.Validate();
        if (validationError is not null)
            throw new ArgumentException(validationError, nameof(result));

        var totalRuns = result.BatterRuns + result.ExtraRuns;
        if (totalRuns > 0)
            Publish(new RunCompleted(0f, totalRuns, result.BatterRuns, result.ExtraRuns, result.CompletedRuns));
        if (result.Dismissal is DismissalKind.Bowled or DismissalKind.RunOut)
            Publish(new WicketBroken(0f, result.Dismissal.ToString()));
        if (result.Dismissal != DismissalKind.None)
            Publish(new Dismissal(0f, result.Dismissal.ToString(), result.DismissedEnd.ToString()));

        var log = new DeliveryEventLog(
            _currentInnings,
            _currentDelivery,
            result,
            Array.AsReadOnly(currentEvents.ToArray()));
        _deliveryLogs.Add(log);
        _currentEvents = null;
        _currentInnings = 0;
        _currentDelivery = 0;
        return log;
    }

    public IReadOnlyList<DeliveryEventLog> GetDeliveryLogs(int inningsNumber) =>
        Array.AsReadOnly(_deliveryLogs
            .Where(log => log.InningsNumber == inningsNumber)
            .ToArray());

    public void ClearLogs()
    {
        if (_currentEvents is not null)
            throw new InvalidOperationException("Cannot clear event logs while a delivery is active.");
        _deliveryLogs.Clear();
    }

    public void Reset()
    {
        _deliveryLogs.Clear();
        _currentEvents = null;
        _currentInnings = 0;
        _currentDelivery = 0;
    }

    private void Unsubscribe(Type eventType, Delegate handler)
    {
        if (!_subscriptions.TryGetValue(eventType, out var handlers))
            return;
        handlers.Remove(handler);
        if (handlers.Count == 0)
            _subscriptions.Remove(eventType);
    }

    private sealed class Subscription(MatchEventBus owner, Type eventType, Delegate handler) : IDisposable
    {
        private MatchEventBus? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Unsubscribe(eventType, handler);
    }
}
