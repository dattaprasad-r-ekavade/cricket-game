using Microsoft.Xna.Framework.Input;
using SuperCricket.Game;
using SuperCricket.Simulation;

namespace SuperCricket.Game.Tests;

public sealed class MatchInputRouterTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.2f, 0f)]
    [InlineData(-0.2f, 0f)]
    [InlineData(0.6f, 0.5f)]
    [InlineData(-0.6f, -0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(-1f, -1f)]
    [InlineData(2f, 1f)]
    [InlineData(-2f, -1f)]
    public void AimAxisUsesClampedDeadZoneAndRescaledIntent(float axis, float expected)
    {
        Assert.Equal(expected, MatchInputRouter.NormalizeAimAxis(axis), precision: 6);
    }

    [Fact]
    public void NonFiniteAimAxisProducesNoIntent()
    {
        Assert.Equal(0f, MatchInputRouter.NormalizeAimAxis(float.NaN));
        Assert.Equal(0f, MatchInputRouter.NormalizeAimAxis(float.PositiveInfinity));
        Assert.Equal(0f, MatchInputRouter.NormalizeAimAxis(float.NegativeInfinity));
    }

    [Fact]
    public void KeyboardKeysAreReportedOnlyOnThePressEdge()
    {
        var router = new MatchInputRouter();
        var pressed = new KeyboardState(Keys.Space);

        router.BeginKeyboardFrame(pressed);
        Assert.True(router.WasKeyPressed(Keys.Space));
        Assert.False(router.WasKeyPressed(Keys.Left));
        router.EndKeyboardFrame();

        router.BeginKeyboardFrame(pressed);
        Assert.False(router.WasKeyPressed(Keys.Space));
        router.EndKeyboardFrame();

        router.BeginKeyboardFrame(default);
        Assert.False(router.WasKeyPressed(Keys.Space));
        router.EndKeyboardFrame();

        router.BeginKeyboardFrame(pressed);
        Assert.True(router.WasKeyPressed(Keys.Space));
        router.EndKeyboardFrame();
    }

    [Fact]
    public void ControllerActionsAreEdgeTriggeredAndCarryAnalogAndHeldInput()
    {
        var router = new MatchInputRouter();

        var pressed = router.ReadController(
            MatchControllerButtons.A | MatchControllerButtons.B,
            -0.35f,
            0.6f,
            runHeld: true,
            isCpuBattingControlled: false,
            isMatchComplete: false,
            isPaused: false,
            developerMode: false);

        Assert.Equal(MatchControllerActions.Defend | MatchControllerActions.Run, pressed.Actions);
        Assert.Equal(-0.35f, pressed.AimAxis);
        Assert.Equal(0.6f, pressed.AimLengthAxis);
        Assert.True(pressed.RunHeld);

        var held = router.ReadController(
            MatchControllerButtons.A | MatchControllerButtons.B,
            -0.35f,
            0.6f,
            runHeld: true,
            isCpuBattingControlled: false,
            isMatchComplete: false,
            isPaused: false,
            developerMode: false);
        Assert.Equal(MatchControllerActions.None, held.Actions);

        router.ReadController(
            MatchControllerButtons.None,
            0f,
            0f,
            runHeld: false,
            isCpuBattingControlled: false,
            isMatchComplete: false,
            isPaused: false,
            developerMode: false);
        var pressedAgain = router.ReadController(
            MatchControllerButtons.A,
            0f,
            0f,
            runHeld: false,
            isCpuBattingControlled: false,
            isMatchComplete: false,
            isPaused: false,
            developerMode: false);
        Assert.Equal(MatchControllerActions.Defend, pressedAgain.Actions);
    }

    [Fact]
    public void DisconnectedControllerStateProducesNoActions()
    {
        var input = new MatchInputRouter().ReadController(
            default,
            isCpuBattingControlled: false,
            isMatchComplete: false,
            isPaused: false,
            developerMode: false);

        Assert.Equal(MatchControllerActions.None, input.Actions);
        Assert.Equal(0f, input.AimAxis);
        Assert.Equal(0f, input.AimLengthAxis);
        Assert.False(input.RunHeld);
    }
}
