using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SuperCricket.Game.Rendering;

/// <summary>A metre-scaled orbit camera for inspecting the practice ground.</summary>
public sealed class OrbitCamera
{
    private const float MinDistance = 12f;
    private const float MaxDistance = 100f;
    private const float MinElevation = 0.12f;
    private const float MaxElevation = 1.25f;
    private readonly (string Name, float Yaw, float Elevation, float Distance, Vector3 Target)[] _presets =
    [
        ("Broadcast", 0.18f, 0.48f, 54f, Vector3.Zero),
        ("Behind striker", 0f, 0.23f, 29f, new Vector3(0f, 0f, -5f)),
        ("Bowler end", MathHelper.Pi, 0.28f, 31f, Vector3.Zero),
        ("Square leg", MathHelper.PiOver2, 0.38f, 52f, Vector3.Zero)
    ];
    private int _presetIndex;
    private int _previousWheel;

    public float Yaw { get; private set; }
    public float Elevation { get; private set; }
    public float Distance { get; private set; }
    public Vector3 Target { get; private set; }
    public string PresetName => _presets[_presetIndex].Name;

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
        var preset = _presets[index];
        Yaw = preset.Yaw;
        Elevation = preset.Elevation;
        Distance = preset.Distance;
        Target = preset.Target;
    }

    public void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        const float orbitSpeed = 1.0f;
        const float elevationSpeed = 0.7f;

        if (keyboard.IsKeyDown(Keys.Left)) Yaw += orbitSpeed * seconds;
        if (keyboard.IsKeyDown(Keys.Right)) Yaw -= orbitSpeed * seconds;
        if (keyboard.IsKeyDown(Keys.PageUp)) Elevation += elevationSpeed * seconds;
        if (keyboard.IsKeyDown(Keys.PageDown)) Elevation -= elevationSpeed * seconds;
        if (keyboard.IsKeyDown(Keys.Home)) Reset();

        Elevation = MathHelper.Clamp(Elevation, MinElevation, MaxElevation);
        Distance = MathHelper.Clamp(Distance, MinDistance, MaxDistance);

        var mouse = Mouse.GetState();
        if (!_hasReadWheel)
        {
            _previousWheel = mouse.ScrollWheelValue;
            _hasReadWheel = true;
        }

        var wheelDelta = mouse.ScrollWheelValue - _previousWheel;
        Distance = MathHelper.Clamp(Distance - wheelDelta * 0.0125f, MinDistance, MaxDistance);
        _previousWheel = mouse.ScrollWheelValue;
    }

    private bool _hasReadWheel;
}
