using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

/// <summary>Adjusts a delivery so its predicted first bounce reaches a selected pitch point.</summary>
public static class BowlingAimModel
{
    public const float MaximumLineOffsetMeters = 2.4f;
    public const float MaximumLengthOffsetMeters = 2f;
    private const int MaximumSolveIterations = 6;

    public static DeliveryPreset AimForPitchTarget(
        DeliveryPreset preset,
        float lineOffsetMeters,
        float lengthOffsetMeters)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (!float.IsFinite(lineOffsetMeters) || MathF.Abs(lineOffsetMeters) > MaximumLineOffsetMeters)
            throw new ArgumentOutOfRangeException(nameof(lineOffsetMeters));
        if (!float.IsFinite(lengthOffsetMeters) || MathF.Abs(lengthOffsetMeters) > MaximumLengthOffsetMeters)
            throw new ArgumentOutOfRangeException(nameof(lengthOffsetMeters));

        var targetPreset = preset.DeepCopy();
        var baseline = FindFirstBounce(targetPreset)
            ?? throw new ArgumentException("The source delivery does not bounce during its simulated flight.", nameof(preset));
        var targetX = baseline.Position.X + lineOffsetMeters;
        var targetZ = baseline.Position.Z + lengthOffsetMeters;

        for (var iteration = 0; iteration < MaximumSolveIterations; iteration++)
        {
            var bounce = FindFirstBounce(targetPreset)
                ?? throw new InvalidOperationException("Aimed delivery lost its pitch bounce.");
            var elapsed = MathF.Max(0.1f, bounce.TimeSeconds);
            var velocity = targetPreset.ReleaseVelocity.ToVector3();
            velocity.X += (targetX - bounce.Position.X) / elapsed;
            velocity.Z += (targetZ - bounce.Position.Z) / elapsed;
            if (!float.IsFinite(velocity.X) || !float.IsFinite(velocity.Z) || velocity.Length() > 80f)
                throw new ArgumentOutOfRangeException(nameof(preset), "The requested pitch target exceeds the delivery's legal release speed.");

            targetPreset.ReleaseVelocity = Vector3Data.From(velocity);
            if (MathF.Abs(targetX - bounce.Position.X) < 0.025f &&
                MathF.Abs(targetZ - bounce.Position.Z) < 0.025f)
                break;
        }

        var finalBounce = FindFirstBounce(targetPreset)
            ?? throw new InvalidOperationException("Aimed delivery lost its first bounce during validation.");
        if (MathF.Abs(targetX - finalBounce.Position.X) > 0.15f ||
            MathF.Abs(targetZ - finalBounce.Position.Z) > 0.15f)
            throw new ArgumentOutOfRangeException(nameof(preset), "The selected pitch target could not be reached accurately.");

        var errors = targetPreset.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Aimed delivery is invalid: {string.Join(" ", errors)}", nameof(preset));
        return targetPreset;
    }

    public static BallFlightFrame? FindFirstBounce(DeliveryPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var simulator = new BallFlightSimulator(preset);
        var maximumSteps = (int)MathF.Ceiling(preset.MaximumSimulationSeconds / preset.FixedTimeStepSeconds);
        for (var step = 0; step < maximumSteps; step++)
        {
            var frame = simulator.Step();
            if (frame.BounceCount > 0)
                return frame;
            if (frame.Phase == BallMotionPhase.Settled)
                break;
        }
        return null;
    }
}
