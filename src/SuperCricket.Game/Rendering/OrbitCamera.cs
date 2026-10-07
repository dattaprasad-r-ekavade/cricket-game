using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SuperCricket.Game.Rendering;

/// <summary>A metre-scaled orbit camera for inspecting the practice ground.</summary>
public sealed class OrbitCamera
{
    private const float MinDistance = 4f;
    private const float MaxDistance = 100f;
    private const float MinPlayerZoomDistance = 4.5f;
    private const float MaxPlayerZoomDistance = 28f;
    private const float MinElevation = 0.12f;
    private const float MaxElevation = 1.25f;
    private readonly (string Name, float Yaw, float Elevation, float Distance, float FieldOfViewDegrees, Vector3 Target, bool FollowsBall)[] _presets =
    [
        ("Broadcast", 0.34f, 0.32f, 20f, 44f, new Vector3(0f, 0f, -1f), false),
        // Aim at the active crease/player. Keeping the focus at mid-pitch made
        // zooming move the end-on cameras away from the person being controlled.
        ("Behind striker", MathHelper.Pi + 0.22f, 0.30f, 8f, 45f, new Vector3(0f, 0.9f, -8.72f), false),
        ("Bowler end", 0.22f, 0.30f, 8f, 45f, new Vector3(0f, 0.9f, 8.72f), false),
        ("Square leg", MathHelper.PiOver2, 0.34f, 24f, 44f, Vector3.Zero, false),
        ("Ball follow", 0f, 0.36f, 8f, 43f, Vector3.Zero, true)
    ];
    private int _presetIndex;
    private int _previousWheel;
    private string? _focusName;
    private bool _hasBallTarget;

    public float Yaw { get; private set; }
    public float Elevation { get; private set; }
    public float Distance { get; private set; }
    public float FieldOfViewDegrees { get; private set; }
    public Vector3 Target { get; private set; }
    public string PresetName => _focusName ?? _presets[_presetIndex].Name;
    public bool FollowsBall => _focusName is null && _presets[_presetIndex].FollowsBall;

    public OrbitCamera() => ApplyPreset(0);

    public Vector3 Position
    {
        get
        {
            var horizontalDistance = MathF.Cos(Elevation) * Distance;
            return Target + new Vector3(
                MathF.Sin(Yaw) * horizontalDistance,
                MathF.Sin(Elevation) * Distance + 0.5f,
                MathF.Cos(Yaw) * horizontalDistance);
        }
    }

    public void Reset()
    {
        ApplyPreset(0);
    }

    public void CyclePreset()
    {
        ApplyPreset((_presetIndex + 1) % _presets.Length);
    }

    public void ZoomBy(float distanceChangeMeters)
    {
        if (!float.IsFinite(distanceChangeMeters))
            throw new ArgumentOutOfRangeException(nameof(distanceChangeMeters), "Camera zoom change must be finite.");
        Distance = MathHelper.Clamp(Distance + distanceChangeMeters, MinPlayerZoomDistance, MaxPlayerZoomDistance);
    }

    public void SetTarget(Vector3 target)
    {
        _hasBallTarget = false;
        Target = target;
    }

    public void TrackTarget(Vector3 target, float elapsedSeconds)
    {
        var blend = 1f - MathF.Exp(-12f * MathF.Max(0f, elapsedSeconds));
        Target = Vector3.Lerp(Target, target, blend);
    }

    public void Focus(Vector3 target, float distance, float yaw, float elevation, string name = "Focus")
    {
        _hasBallTarget = false;
        Target = target;
        Distance = MathHelper.Clamp(distance, MinDistance, MaxDistance);
        Yaw = yaw;
        Elevation = MathHelper.Clamp(elevation, MinElevation, MaxElevation);
        _focusName = name;
    }

    public void FollowBall(Vector3 position, float elapsedSeconds)
    {
        if (!FollowsBall)
        {
            _hasBallTarget = false;
            return;
        }

        if (!_hasBallTarget)
        {
            Target = position;
            _hasBallTarget = true;
            return;
        }

        var blend = 1f - MathF.Exp(-18f * MathF.Max(0f, elapsedSeconds));
        Target = Vector3.Lerp(Target, position, blend);
    }

    public bool SelectPreset(string name)
    {
        var normalizedName = name.Replace('-', ' ').Trim();
        for (var index = 0; index < _presets.Length; index++)
        {
            if (!string.Equals(_presets[index].Name, normalizedName, StringComparison.OrdinalIgnoreCase))
                continue;
            ApplyPreset(index);
            return true;
        }

        return false;
    }

    private void ApplyPreset(int index)
    {
        _presetIndex = index;
        _focusName = null;
        _hasBallTarget = false;
        var preset = _presets[index];
        Yaw = preset.Yaw;
        Elevation = preset.Elevation;
        Distance = preset.Distance;
        FieldOfViewDegrees = preset.FieldOfViewDegrees;
        Target = preset.Target;
    }

    public void Update(GameTime gameTime, bool allowDeveloperControls, KeyboardState keyboard)
    {
        var seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        const float orbitSpeed = 1.0f;
        const float elevationSpeed = 0.7f;

        if (allowDeveloperControls)
        {
            if (keyboard.IsKeyDown(Keys.Left)) Yaw += orbitSpeed * seconds;
            if (keyboard.IsKeyDown(Keys.Right)) Yaw -= orbitSpeed * seconds;
            if (keyboard.IsKeyDown(Keys.PageUp)) Elevation += elevationSpeed * seconds;
            if (keyboard.IsKeyDown(Keys.PageDown)) Elevation -= elevationSpeed * seconds;
            if (keyboard.IsKeyDown(Keys.Home)) Reset();
        }
        else
        {
            const float playerZoomSpeedMetersPerSecond = 7f;
            if (keyboard.IsKeyDown(Keys.PageUp)) ZoomBy(playerZoomSpeedMetersPerSecond * seconds);
            if (keyboard.IsKeyDown(Keys.PageDown)) ZoomBy(-playerZoomSpeedMetersPerSecond * seconds);
        }

        Elevation = MathHelper.Clamp(Elevation, MinElevation, MaxElevation);
        Distance = MathHelper.Clamp(Distance, MinDistance, MaxDistance);

        var mouse = Mouse.GetState();
        if (!_hasReadWheel)
        {
            _previousWheel = mouse.ScrollWheelValue;
            _hasReadWheel = true;
        }

        var wheelDelta = mouse.ScrollWheelValue - _previousWheel;
        if (allowDeveloperControls)
            Distance = MathHelper.Clamp(Distance - wheelDelta * 0.0125f, MinDistance, MaxDistance);
        else if (wheelDelta != 0)
            Distance = MathHelper.Clamp(Distance - wheelDelta * 0.0125f,
                MinPlayerZoomDistance, MaxPlayerZoomDistance);
        _previousWheel = mouse.ScrollWheelValue;
    }

    private bool _hasReadWheel;
}
