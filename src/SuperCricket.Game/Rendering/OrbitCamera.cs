using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SuperCricket.Game.Rendering;

/// <summary>A metre-scaled orbit camera for inspecting the practice ground.</summary>
public sealed class OrbitCamera
{
    private const float MinDistance = 12f;
    private const float MaxDistance = 60f;
    private const float MinElevation = 0.12f;
    private const float MaxElevation = 1.25f;
    private int _previousWheel;

    public float Yaw { get; private set; } = 0.22f;
    public float Elevation { get; private set; } = 0.38f;
    public float Distance { get; private set; } = 27f;

    public Vector3 Position
    {
        get
        {
            var horizontalDistance = MathF.Cos(Elevation) * Distance;
            return new Vector3(
                MathF.Sin(Yaw) * horizontalDistance,
                MathF.Sin(Elevation) * Distance + 0.5f,
                MathF.Cos(Yaw) * horizontalDistance);
        }
    }

    public void Reset()
    {
        Yaw = 0.22f;
        Elevation = 0.38f;
        Distance = 27f;
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
