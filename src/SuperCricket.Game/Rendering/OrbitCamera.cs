using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SuperCricket.Game.Rendering;

/// <summary>A metre-scaled orbit camera for inspecting the practice ground.</summary>
public sealed class OrbitCamera
{
    private const float MinDistance = 4f;
    private const float MaxDistance = 100f;
    private const float MinElevation = 0.12f;
    private const float MaxElevation = 1.25f;
    private readonly (string Name, float Yaw, float Elevation, float Distance, Vector3 Target, bool FollowsBall)[] _presets =
    [
        ("Broadcast", 0.18f, 0.48f, 54f, Vector3.Zero, false),
        ("Behind striker", 0f, 0.23f, 29f, new Vector3(0f, 0f, -5f), false),
        ("Bowler end", MathHelper.Pi, 0.28f, 31f, Vector3.Zero, false),
        ("Square leg", MathHelper.PiOver2, 0.38f, 52f, Vector3.Zero, false),
        ("Ball follow", 0f, 0.36f, 9f, Vector3.Zero, true)
    ];
    private int _presetIndex;
    private int _previousWheel;
    private string? _focusName;
    private bool _hasBallTarget;

    public float Yaw { get; private set; }
    public float Elevation { get; private set; }
    public float Distance { get; private set; }
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
        Target = preset.Target;
    }

    public void Update(GameTime gameTime, bool allowDeveloperControls)
    {
        var keyboard = Keyboard.GetState();
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
        _previousWheel = mouse.ScrollWheelValue;
    }

    private bool _hasReadWheel;
}
