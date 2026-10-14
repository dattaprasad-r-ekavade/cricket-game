using System.Numerics;

namespace SuperCricket.Simulation;

public enum TimingZone
{
    TooLate = 0,
    Start = 1,
    Early = 2,
    Ideal = 3,
    Late = 4,
    End = 5,
    TooEarly = 6
}

public enum StrokeFoot
{
    Auto,
    Front,
    Back
}

public enum StrokeIntent
{
    Leave,
    Defend,
    Attack
}

public enum BallLength
{
    Short,
    Good,
    Full,
    Yorker,
    FullToss
}

public enum BallLine
{
    WideOutsideOff,
    OutsideOff,
    Stumps,
    Leg,
    WideLeg
}

public enum ShotDirection8
{
    Straight,
    OffDrive,
    Cover,
    Square,
    Fine,
    LegFine,
    LegSquare,
    MidWicket
}

public readonly record struct BallCategory(BallLength Length, BallLine Line, float ArrivalHeightMeters);

public readonly record struct StrokeRequest(
    ShotDirection8 Direction,
    StrokeIntent Intent,
    bool Loft,
    StrokeFoot Foot);

public readonly record struct ZoneOutcome(
    float YawDegrees,
    float YawSpreadDegrees,
    float ElevationDegrees,
    float MinSpeedMetersPerSecond,
    float MaxSpeedMetersPerSecond,
    float EdgeChance,
    bool BeatenOrMissed);

public readonly record struct StrokeResolution(
    string StrokeId,
    TimingZone Zone,
    float BallDistanceAtInputMeters,
    Vector3 OutgoingVelocity,
    bool Edged,
    bool Missed,
    StrokeFoot FootPlayed);
