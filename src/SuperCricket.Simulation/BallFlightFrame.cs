using System.Numerics;

namespace SuperCricket.Simulation;

public enum BallMotionPhase
{
    InFlight,
    Rolling,
    Settled
}

public readonly record struct BallFlightFrame(
    float TimeSeconds,
    Vector3 Position,
    Vector3 Velocity,
    int BounceCount,
    BallMotionPhase Phase);
