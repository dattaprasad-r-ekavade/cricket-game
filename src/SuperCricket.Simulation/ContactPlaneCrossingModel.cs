using System.Numerics;

namespace SuperCricket.Simulation;

/// <summary>Interpolates the incoming ball at a fixed batting contact plane.</summary>
public static class ContactPlaneCrossingModel
{
    public static bool TryResolve(
        BallFlightFrame previous,
        BallFlightFrame current,
        float contactPlaneZ,
        out BallFlightFrame crossing,
        out float fraction)
    {
        crossing = default;
        fraction = 0f;
        if (!float.IsFinite(contactPlaneZ) ||
            !IsFinite(previous.Position) || !IsFinite(current.Position) ||
            !IsFinite(previous.Velocity) || !IsFinite(current.Velocity) ||
            !float.IsFinite(previous.TimeSeconds) || !float.IsFinite(current.TimeSeconds) ||
            current.TimeSeconds < previous.TimeSeconds)
            throw new ArgumentException("A contact-plane crossing requires finite frames in time order.");

        var depthSpan = previous.Position.Z - current.Position.Z;
        if (previous.Position.Z <= contactPlaneZ || current.Position.Z > contactPlaneZ || depthSpan <= 0f ||
            previous.Velocity.Z >= 0f || current.Velocity.Z >= 0f)
            return false;

        fraction = Math.Clamp((previous.Position.Z - contactPlaneZ) / depthSpan, 0f, 1f);
        crossing = new BallFlightFrame(
            previous.TimeSeconds + (current.TimeSeconds - previous.TimeSeconds) * fraction,
            Vector3.Lerp(previous.Position, current.Position, fraction) with { Z = contactPlaneZ },
            Vector3.Lerp(previous.Velocity, current.Velocity, fraction),
            current.BounceCount,
            current.Phase);
        return true;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
