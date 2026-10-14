using Microsoft.Xna.Framework.Input;
using SuperCricket.Game;
using SuperCricket.Simulation;
using SuperCricket.Simulation.Batting;

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

    [Theory]
    [InlineData(Keys.D1, 0)]
    [InlineData(Keys.D4, 3)]
    [InlineData(Keys.D8, 7)]
    public void BowlingKeyboardMapsOneThroughEightToCatalogueSlots(Keys key, int expectedIndex)
    {
        var router = new MatchInputRouter();
        router.BeginKeyboardFrame(new KeyboardState(key));

        var input = router.ReadBowlingControls(default, page: 0);

        Assert.Equal(expectedIndex, input.DeliveryIndex);
    }

    [Fact]
    public void BowlingControllerFacesSelectFourSlotsAndShouldersPageThroughTheCatalogue()
    {
        var router = new MatchInputRouter();
        var firstPageFrame = router.ReadController(
            MatchControllerButtons.B,
            0f,
            0f,
            false,
            isCpuBattingControlled: true,
            isMatchComplete: false,
            isPaused: false,
            developerMode: false);
        Assert.Equal(3, router.ReadBowlingControls(firstPageFrame, page: 0).DeliveryIndex);

        router.ReadController(MatchControllerButtons.None, 0f, 0f, false, true, false, false, false);
        var nextPage = router.ReadController(
            MatchControllerButtons.RightShoulder | MatchControllerButtons.Y,
            0f,
            0f,
            false,
            isCpuBattingControlled: true,
            isMatchComplete: false,
            isPaused: false,
            developerMode: false);

        var input = router.ReadBowlingControls(nextPage, page: 1);
        Assert.True(input.NextPagePressed);
        Assert.Equal(6, input.DeliveryIndex);
    }

    [Fact]
    public void PaceTriggerIsEdgeTriggeredForTheBowlingMeter()
    {
        var router = new MatchInputRouter();
        var pressed = router.ReadController(
            MatchControllerButtons.None, 0f, 0f, false, true, false, false, false,
            paceTriggerHeld: true);
        var held = router.ReadController(
            MatchControllerButtons.None, 0f, 0f, false, true, false, false, false,
            paceTriggerHeld: true);

        Assert.True(pressed.PacePressed);
        Assert.False(held.PacePressed);
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

        Assert.Equal(MatchControllerActions.Attack | MatchControllerActions.Leave | MatchControllerActions.Run, pressed.Actions);
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
        Assert.Equal(MatchControllerActions.Attack, pressedAgain.Actions);
    }

    [Theory]
    [InlineData(Keys.Up, Keys.None, ShotDirection8.Straight)]
    [InlineData(Keys.Up, Keys.Left, ShotDirection8.OffDrive)]
    [InlineData(Keys.Left, Keys.None, ShotDirection8.Cover)]
    [InlineData(Keys.Down, Keys.Left, ShotDirection8.Square)]
    [InlineData(Keys.Down, Keys.None, ShotDirection8.Fine)]
    [InlineData(Keys.Down, Keys.Right, ShotDirection8.LegFine)]
    [InlineData(Keys.Right, Keys.None, ShotDirection8.LegSquare)]
    [InlineData(Keys.Up, Keys.Right, ShotDirection8.MidWicket)]
    public void DirectionalKeyboardInputsMapEveryArrowDirectionToAStrokeRequest(
        Keys firstDirection, Keys secondDirection, ShotDirection8 expected) =>
        AssertKeyboardDirection([firstDirection, secondDirection, Keys.S], expected);

    [Fact]
    public void AttackAndDefendPressesCreateRequestsWithAutoOrManualFoot()
    {
        var attackRouter = new MatchInputRouter();
        attackRouter.BeginKeyboardFrame(new KeyboardState(Keys.Right, Keys.Up, Keys.S, Keys.LeftShift));
        var attack = attackRouter.ReadBattingInput(BattingInputScheme.Directional, BattingFootSelection.Automatic);
        Assert.Equal(new StrokeRequest(ShotDirection8.MidWicket, StrokeIntent.Attack, true, StrokeFoot.Auto), attack.Stroke);

        var manualAttackRouter = new MatchInputRouter();
        manualAttackRouter.BeginKeyboardFrame(new KeyboardState(Keys.S));
        Assert.Equal(StrokeFoot.Front, manualAttackRouter.ReadBattingInput(
            BattingInputScheme.Directional, BattingFootSelection.Manual).Stroke?.Foot);

        var defendRouter = new MatchInputRouter();
        defendRouter.BeginKeyboardFrame(new KeyboardState(Keys.W));
        Assert.Equal(new StrokeRequest(ShotDirection8.Straight, StrokeIntent.Defend, false, StrokeFoot.Back),
            defendRouter.ReadBattingInput(BattingInputScheme.Directional, BattingFootSelection.Manual).Stroke);
    }

    [Fact]
    public void LeaveAndAdvanceAreSeparateKeyboardActionsAndClassicChordsStayAvailable()
    {
        var directional = new MatchInputRouter();
        directional.BeginKeyboardFrame(new KeyboardState(Keys.A, Keys.D));
        var input = directional.ReadBattingInput(BattingInputScheme.Directional, BattingFootSelection.Automatic);
        Assert.True(input.LeavePressed);
        Assert.True(input.AdvancePressed);
        Assert.Null(input.Stroke);

        var classic = new MatchInputRouter();
        classic.BeginKeyboardFrame(new KeyboardState(Keys.S, Keys.D));
        Assert.Null(classic.ReadBattingInput(BattingInputScheme.ClassicChords, BattingFootSelection.Automatic).Stroke);
        var recorder = new BattingInputRecorder();
        Assert.Equal(RecordedBattingStroke.FrontFootDrive, recorder.Update(0f, [Keys.S, Keys.D])?.Stroke);
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

    private static void AssertKeyboardDirection(Keys[] keys, ShotDirection8 expected)
    {
        var router = new MatchInputRouter();
        router.BeginKeyboardFrame(new KeyboardState(keys));
        var input = router.ReadBattingInput(BattingInputScheme.Directional, BattingFootSelection.Automatic);
        Assert.Equal(expected, input.Stroke?.Direction);
        Assert.Equal(StrokeIntent.Attack, input.Stroke?.Intent);
    }
}
