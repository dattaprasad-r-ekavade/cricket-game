using System.Numerics;

namespace SuperCricket.Simulation.Events;

/// <summary>A typed simulation fact, timestamped relative to the start of its delivery.</summary>
public abstract record MatchEvent(float TimeSeconds);

public sealed record DeliveryReleased(float TimeSeconds, int InningsNumber, int DeliveryNumber) : MatchEvent(TimeSeconds);
public sealed record BallPitched(float TimeSeconds, Vector3 Position, int BounceCount) : MatchEvent(TimeSeconds);
public sealed record StrokePlayed(float TimeSeconds, string StrokeId) : MatchEvent(TimeSeconds);
public sealed record BatContact(float TimeSeconds, Vector3 Position) : MatchEvent(TimeSeconds);
public sealed record BallBeatBat(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record BodyHit(float TimeSeconds, Vector3 Position) : MatchEvent(TimeSeconds);
public sealed record Appeal(float TimeSeconds, string DecisionType) : MatchEvent(TimeSeconds);
public sealed record UmpireDecision(float TimeSeconds, bool IsOut, string DecisionType) : MatchEvent(TimeSeconds);
public sealed record WicketBroken(float TimeSeconds, string DismissalType) : MatchEvent(TimeSeconds);
public sealed record RunCompleted(
    float TimeSeconds,
    int Runs,
    int BatterRuns,
    int ExtraRuns,
    int CompletedRuns) : MatchEvent(TimeSeconds);
public sealed record NearRunOut(float TimeSeconds, float ClosestCreaseDistanceMeters) : MatchEvent(TimeSeconds);
public sealed record Boundary(float TimeSeconds, int Runs, bool ClearedInTheAir, Vector3? Position) : MatchEvent(TimeSeconds);
public sealed record Dismissal(float TimeSeconds, string Kind, string DismissedEnd) : MatchEvent(TimeSeconds);
public sealed record OverComplete(float TimeSeconds, int InningsNumber, int CompletedOvers) : MatchEvent(TimeSeconds);
