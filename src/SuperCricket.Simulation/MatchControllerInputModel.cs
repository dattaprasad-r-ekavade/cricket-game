namespace SuperCricket.Simulation;

[Flags]
public enum MatchControllerButtons
{
    None = 0,
    A = 1 << 0,
    B = 1 << 1,
    X = 1 << 2,
    Y = 1 << 3,
    Start = 1 << 4,
    Back = 1 << 5,
    LeftShoulder = 1 << 6,
    RightShoulder = 1 << 7,
    DPadUp = 1 << 8,
    DPadDown = 1 << 9,
    DPadLeft = 1 << 10,
    DPadRight = 1 << 11
}

[Flags]
public enum MatchControllerActions
{
    None = 0,
    Exit = 1 << 0,
    Pause = 1 << 1,
    Defend = 1 << 2,
    Drive = 1 << 3,
    Loft = 1 << 4,
    Run = 1 << 5,
    CancelRun = 1 << 6,
    NextBall = 1 << 7,
    SelectStandardDelivery = 1 << 8,
    SelectWideDelivery = 1 << 9,
    SelectNoBallDelivery = 1 << 10,
    StepOffSide = 1 << 11,
    StepLegSide = 1 << 12,
    RestartMatch = 1 << 13,
    CycleOvers = 1 << 14,
    CycleDifficulty = 1 << 15,
    ToggleHighContrast = 1 << 16,
    ToggleLargeText = 1 << 17,
    DecreaseEffectsVolume = 1 << 18,
    IncreaseEffectsVolume = 1 << 19,
    SelectYorkerDelivery = 1 << 20
}

/// <summary>Maps edge-triggered controller buttons to the current match context.</summary>
public static class MatchControllerInputModel
{
    public static MatchControllerActions ReadPressedActions(
        MatchControllerButtons currentButtons,
        MatchControllerButtons previousButtons,
        bool isCpuBattingControlled,
        bool isMatchComplete,
        bool isPaused = false)
    {
        var pressed = currentButtons & ~previousButtons;
        var actions = MatchControllerActions.None;
        if (Has(pressed, MatchControllerButtons.Back)) actions |= MatchControllerActions.Exit;
        if (Has(pressed, MatchControllerButtons.Start)) actions |= MatchControllerActions.Pause;

        if (isPaused)
        {
            if (Has(pressed, MatchControllerButtons.Y)) actions |= MatchControllerActions.ToggleHighContrast;
            if (Has(pressed, MatchControllerButtons.X)) actions |= MatchControllerActions.ToggleLargeText;
            if (Has(pressed, MatchControllerButtons.LeftShoulder)) actions |= MatchControllerActions.DecreaseEffectsVolume;
            if (Has(pressed, MatchControllerButtons.RightShoulder)) actions |= MatchControllerActions.IncreaseEffectsVolume;
            return actions;
        }

        if (isMatchComplete)
        {
            if (Has(pressed, MatchControllerButtons.A)) actions |= MatchControllerActions.RestartMatch;
            if (Has(pressed, MatchControllerButtons.LeftShoulder)) actions |= MatchControllerActions.CycleDifficulty;
            if (Has(pressed, MatchControllerButtons.RightShoulder)) actions |= MatchControllerActions.CycleOvers;
            return actions;
        }

        if (Has(pressed, MatchControllerButtons.A)) actions |= MatchControllerActions.Defend;
        if (Has(pressed, MatchControllerButtons.X)) actions |= MatchControllerActions.Drive;
        if (Has(pressed, MatchControllerButtons.Y)) actions |= MatchControllerActions.Loft;
        if (Has(pressed, MatchControllerButtons.B)) actions |= MatchControllerActions.Run;
        if (Has(pressed, MatchControllerButtons.LeftShoulder)) actions |= MatchControllerActions.CancelRun;
        if (Has(pressed, MatchControllerButtons.RightShoulder)) actions |= MatchControllerActions.NextBall;

        if (isCpuBattingControlled)
        {
            if (Has(pressed, MatchControllerButtons.DPadLeft)) actions |= MatchControllerActions.SelectStandardDelivery;
            if (Has(pressed, MatchControllerButtons.DPadUp)) actions |= MatchControllerActions.SelectWideDelivery;
            if (Has(pressed, MatchControllerButtons.DPadDown)) actions |= MatchControllerActions.SelectNoBallDelivery;
            if (Has(pressed, MatchControllerButtons.DPadRight)) actions |= MatchControllerActions.SelectYorkerDelivery;
        }
        else
        {
            if (Has(pressed, MatchControllerButtons.DPadLeft)) actions |= MatchControllerActions.StepOffSide;
            if (Has(pressed, MatchControllerButtons.DPadRight)) actions |= MatchControllerActions.StepLegSide;
        }

        return actions;
    }

    private static bool Has(MatchControllerButtons buttons, MatchControllerButtons button) =>
        (buttons & button) != 0;
}
