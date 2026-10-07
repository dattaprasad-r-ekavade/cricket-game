using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

/// <summary>Deterministic fixed-step ball flight and simple pitch/ground contacts.</summary>
public sealed class BallFlightSimulator
{
    private readonly DeliveryPreset _preset;
    private Vector3 _position;
    private Vector3 _velocity;
    private float _elapsedSeconds;
    private int _bounceCount;
    private BallMotionPhase _phase;
    private float? _battingRollingDeceleration;

    public BallFlightSimulator(DeliveryPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var errors = preset.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException($"Invalid delivery preset: {string.Join(" ", errors)}", nameof(preset));
        }

        _preset = preset;
        _position = preset.StartPosition;
        _velocity = preset.StartVelocity;
        _phase = BallMotionPhase.InFlight;
    }

    public float FixedTimeStepSeconds => _preset.FixedTimeStepSeconds;
    public BallFlightFrame CurrentFrame => new(_elapsedSeconds, _position, _velocity, _bounceCount, _phase);

    public void ApplyBatContact(Vector3 contactPosition, Vector3 outgoingVelocity, float rollingDecelerationMetersPerSecondSquared = 7f)
    {
        var speedSquared = outgoingVelocity.LengthSquared();
        if (!IsFinite(contactPosition) || !IsFinite(outgoingVelocity) || !float.IsFinite(speedSquared) || speedSquared <= 0f ||
            !float.IsFinite(rollingDecelerationMetersPerSecondSquared) || rollingDecelerationMetersPerSecondSquared <= 0f)
            throw new ArgumentException("Bat contact requires finite positions, a non-zero finite velocity, and positive rolling deceleration.");

        _position = contactPosition;
        _velocity = outgoingVelocity;
        _battingRollingDeceleration = rollingDecelerationMetersPerSecondSquared;
        _bounceCount = 0;
        _phase = BallMotionPhase.InFlight;
    }

    public void StopAtContact(Vector3 contactPosition)
    {
        if (!IsFinite(contactPosition))
            throw new ArgumentException("A contact position must be finite.", nameof(contactPosition));

        _position = contactPosition;
        _velocity = Vector3.Zero;
        _phase = BallMotionPhase.Settled;
    }

    /// <param name="enforceSimulationLimit">Use the preset's laboratory time limit; live batted balls continue physically.</param>
    public BallFlightFrame Step(bool enforceSimulationLimit = true)
    {
        if (_phase == BallMotionPhase.Settled)
        {
            return CurrentFrame;
        }

        var deltaTime = _preset.FixedTimeStepSeconds;
        _elapsedSeconds += deltaTime;

        if (_phase == BallMotionPhase.Rolling)
        {
            StepRolling(deltaTime);
        }
        else
        {
            StepFlight(deltaTime);
        }

        if ((enforceSimulationLimit && _elapsedSeconds >= _preset.MaximumSimulationSeconds) ||
            _position.X * _position.X + _position.Z * _position.Z >=
            _preset.FieldBoundaryRadiusMeters * _preset.FieldBoundaryRadiusMeters)
        {
            _phase = BallMotionPhase.Settled;
            _velocity = Vector3.Zero;
        }

        return CurrentFrame;
    }

    private void StepFlight(float deltaTime)
    {
        var previousPosition = _position;
        var previousVelocity = _velocity;
        var speed = _velocity.Length();
        var dragAcceleration = -_velocity * (_preset.AirDragPerMeter * speed);
        var acceleration = new Vector3(
            _preset.LateralAccelerationMetersPerSecondSquared,
            -_preset.GravityMetersPerSecondSquared,
            0f) + dragAcceleration;

        _velocity += acceleration * deltaTime;
        _position += _velocity * deltaTime;

        var surfaceHeight = GetSurfaceHeight(_position.X, _position.Z);
        var contactHeight = surfaceHeight + _preset.BallRadiusMeters;
        if (_velocity.Y >= 0f || previousPosition.Y < contactHeight || _position.Y > contactHeight)
        {
            return;
        }

        var verticalSpan = previousPosition.Y - _position.Y;
        var impactFraction = verticalSpan <= 0f
            ? 1f
            : Math.Clamp((previousPosition.Y - contactHeight) / verticalSpan, 0f, 1f);
        _position = Vector3.Lerp(previousPosition, _position, impactFraction);
        _position.Y = contactHeight;
        var impactVelocity = previousVelocity + acceleration * (deltaTime * impactFraction);
        _bounceCount++;

        var restitution = _bounceCount == 1
            ? _preset.PitchBounceRestitution
            : _preset.GroundBounceRestitution;
        _velocity = new Vector3(
            impactVelocity.X * _preset.TangentialRetention,
            -impactVelocity.Y * restitution,
            impactVelocity.Z * _preset.TangentialRetention);

        if (_velocity.Y < 0.65f)
        {
            _velocity.Y = 0f;
            _phase = BallMotionPhase.Rolling;
        }
    }

    private void StepRolling(float deltaTime)
    {
        var surfaceHeight = GetSurfaceHeight(_position.X, _position.Z);
        _position.Y = surfaceHeight + _preset.BallRadiusMeters;

        var horizontalVelocity = new Vector2(_velocity.X, _velocity.Z);
        var speed = horizontalVelocity.Length();
        var deceleration = _battingRollingDeceleration ?? _preset.RollingDecelerationMetersPerSecondSquared;
        var remainingSpeed = MathF.Max(0f, speed - deceleration * deltaTime);
        if (speed > 0f)
        {
            horizontalVelocity *= remainingSpeed / speed;
        }

        _velocity = new Vector3(horizontalVelocity.X, 0f, horizontalVelocity.Y);
        _position += _velocity * deltaTime;
        if (remainingSpeed <= 0.1f)
        {
            _velocity = Vector3.Zero;
            _phase = BallMotionPhase.Settled;
        }
    }

    private float GetSurfaceHeight(float x, float z)
    {
        var withinPitch = MathF.Abs(x) <= _preset.PitchWidthMeters / 2f &&
            MathF.Abs(z) <= _preset.PitchLengthMeters / 2f;
        return withinPitch ? _preset.PitchSurfaceHeightMeters : _preset.FieldSurfaceHeightMeters;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
