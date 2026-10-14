using System;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Simulation;
using SuperCricket.Simulation.Batting;

namespace SuperCricket.Game;

internal readonly record struct ControllerInputFrame(
    MatchControllerActions Actions,
    float AimAxis,
    float AimLengthAxis,
    bool RunHeld);

internal readonly record struct KeyboardBattingInputFrame(
    float HorizontalAim,
    float ForwardAim,
    StrokeRequest? Stroke,
    bool LeavePressed,
    bool AdvancePressed);

/// <summary>Turns keyboard and controller device state into edge-triggered match input and normalized aim.</summary>
internal sealed class MatchInputRouter
{
    private const float AimDeadZone = 0.2f;

    private KeyboardState _currentKeyboard;
    private KeyboardState _previousKeyboard;
    private MatchControllerButtons _previousControllerButtons;

    public ControllerInputFrame ReadController(
        GamePadState state,
        bool isCpuBattingControlled,
        bool isMatchComplete,
        bool isPaused,
        bool developerMode) => ReadController(
            ReadControllerButtons(state),
            state.ThumbSticks.Left.X,
            state.ThumbSticks.Left.Y,
            state.IsButtonDown(Buttons.B),
            isCpuBattingControlled,
            isMatchComplete,
            isPaused,
            developerMode);

    internal ControllerInputFrame ReadController(
        MatchControllerButtons buttons,
        float aimAxis,
        float aimLengthAxis,
        bool runHeld,
        bool isCpuBattingControlled,
        bool isMatchComplete,
        bool isPaused,
        bool developerMode)
    {
        var actions = MatchControllerInputModel.ReadPressedActions(
            buttons,
            _previousControllerButtons,
            isCpuBattingControlled,
            isMatchComplete,
            isPaused,
            developerMode);
        _previousControllerButtons = buttons;

        return new ControllerInputFrame(
            actions,
            aimAxis,
            aimLengthAxis,
            runHeld);
    }

    public void BeginKeyboardFrame(KeyboardState state) => _currentKeyboard = state;

    public bool WasKeyPressed(Keys key) =>
        _currentKeyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    public KeyboardBattingInputFrame ReadBattingInput(
        BattingInputScheme scheme,
        BattingFootSelection footSelection)
    {
        var horizontalAim = (_currentKeyboard.IsKeyDown(Keys.Right) ? 1f : 0f) -
            (_currentKeyboard.IsKeyDown(Keys.Left) ? 1f : 0f);
        var forwardAim = (_currentKeyboard.IsKeyDown(Keys.Up) ? 1f : 0f) -
            (_currentKeyboard.IsKeyDown(Keys.Down) ? 1f : 0f);
        if (MathF.Abs(horizontalAim) < 0.001f && MathF.Abs(forwardAim) < 0.001f)
            forwardAim = 1f;

        StrokeRequest? stroke = null;
        if (scheme == BattingInputScheme.Directional)
        {
            var intent = WasKeyPressed(Keys.S)
                ? StrokeIntent.Attack
                : WasKeyPressed(Keys.W)
                    ? StrokeIntent.Defend
                    : (StrokeIntent?)null;
            if (intent is { } committedIntent)
            {
                var direction = GetKeyboardDirection(horizontalAim, forwardAim);
                var foot = footSelection == BattingFootSelection.Manual
                    ? committedIntent == StrokeIntent.Defend ? StrokeFoot.Back : StrokeFoot.Front
                    : StrokeFoot.Auto;
                var loft = _currentKeyboard.IsKeyDown(Keys.LeftShift) || _currentKeyboard.IsKeyDown(Keys.RightShift);
                stroke = new StrokeRequest(direction, committedIntent, loft, foot);
            }
        }

        return new KeyboardBattingInputFrame(
            horizontalAim,
            forwardAim,
            stroke,
            WasKeyPressed(Keys.A),
            scheme == BattingInputScheme.Directional && WasKeyPressed(Keys.D));
    }

    private static ShotDirection8 GetKeyboardDirection(float horizontalAim, float forwardAim)
    {
        if (horizontalAim < 0f)
            return forwardAim > 0f ? ShotDirection8.OffDrive : forwardAim < 0f ? ShotDirection8.Square : ShotDirection8.Cover;
        if (horizontalAim > 0f)
            return forwardAim > 0f ? ShotDirection8.MidWicket : forwardAim < 0f ? ShotDirection8.LegFine : ShotDirection8.LegSquare;
        return forwardAim < 0f ? ShotDirection8.Fine : ShotDirection8.Straight;
    }

    public void EndKeyboardFrame() => _previousKeyboard = _currentKeyboard;

    public static float NormalizeAimAxis(float axis)
    {
        if (!float.IsFinite(axis))
            return 0f;

        var clampedAxis = Math.Clamp(axis, -1f, 1f);
        var magnitude = MathF.Abs(clampedAxis);
        if (magnitude <= AimDeadZone)
            return 0f;

        return MathF.CopySign((magnitude - AimDeadZone) / (1f - AimDeadZone), clampedAxis);
    }

    public void ResetKeyboardHistory()
    {
        _currentKeyboard = default;
        _previousKeyboard = default;
    }

    private static MatchControllerButtons ReadControllerButtons(GamePadState state)
    {
        var buttons = MatchControllerButtons.None;
        if (state.IsButtonDown(Buttons.A)) buttons |= MatchControllerButtons.A;
        if (state.IsButtonDown(Buttons.B)) buttons |= MatchControllerButtons.B;
        if (state.IsButtonDown(Buttons.X)) buttons |= MatchControllerButtons.X;
        if (state.IsButtonDown(Buttons.Y)) buttons |= MatchControllerButtons.Y;
        if (state.IsButtonDown(Buttons.Start)) buttons |= MatchControllerButtons.Start;
        if (state.IsButtonDown(Buttons.Back)) buttons |= MatchControllerButtons.Back;
        if (state.IsButtonDown(Buttons.LeftShoulder)) buttons |= MatchControllerButtons.LeftShoulder;
        if (state.IsButtonDown(Buttons.RightShoulder)) buttons |= MatchControllerButtons.RightShoulder;
        if (state.IsButtonDown(Buttons.DPadUp)) buttons |= MatchControllerButtons.DPadUp;
        if (state.IsButtonDown(Buttons.DPadDown)) buttons |= MatchControllerButtons.DPadDown;
        if (state.IsButtonDown(Buttons.DPadLeft)) buttons |= MatchControllerButtons.DPadLeft;
        if (state.IsButtonDown(Buttons.DPadRight)) buttons |= MatchControllerButtons.DPadRight;
        if (state.IsButtonDown(Buttons.LeftStick)) buttons |= MatchControllerButtons.LeftStick;
        return buttons;
    }
}
