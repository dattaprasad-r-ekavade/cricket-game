namespace SuperCricket.Simulation.Events;

public abstract record MatchEvent(float TimeSeconds);

public sealed record DeliveryReleased(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record BallPitched(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record StrokePlayed(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record BatContact(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record BallBeatBat(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record BodyHit(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record Appeal(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record UmpireDecision(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record WicketBroken(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record RunCompleted(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record NearRunOut(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record Boundary(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record Dismissal(float TimeSeconds) : MatchEvent(TimeSeconds);
public sealed record OverComplete(float TimeSeconds) : MatchEvent(TimeSeconds);
