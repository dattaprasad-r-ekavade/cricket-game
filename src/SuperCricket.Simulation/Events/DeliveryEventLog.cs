namespace SuperCricket.Simulation.Events;

/// <summary>All simulation events and the resolved score for a single delivery.</summary>
public sealed record DeliveryEventLog
{
    public DeliveryEventLog(
        int inningsNumber,
        int deliveryNumber,
        DeliveryResult result,
        IReadOnlyList<MatchEvent> events)
    {
        InningsNumber = inningsNumber;
        DeliveryNumber = deliveryNumber;
        Result = result;
        Events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public int InningsNumber { get; init; }
    public int DeliveryNumber { get; init; }
    public DeliveryResult Result { get; init; }
    public IReadOnlyList<MatchEvent> Events { get; init; }

    public bool Equals(DeliveryEventLog? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        InningsNumber == other.InningsNumber &&
        DeliveryNumber == other.DeliveryNumber &&
        Result == other.Result &&
        Events.SequenceEqual(other.Events);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(InningsNumber);
        hash.Add(DeliveryNumber);
        hash.Add(Result);
        foreach (var matchEvent in Events)
            hash.Add(matchEvent);
        return hash.ToHashCode();
    }
}
