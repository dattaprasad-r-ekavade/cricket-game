namespace SuperCricket.Simulation;

public static class MatchControllerInputReviewChecks
{
    public static void Run()
    {
        var playerButtons = MatchControllerButtons.A | MatchControllerButtons.X |
            MatchControllerButtons.Y | MatchControllerButtons.B | MatchControllerButtons.LeftShoulder |
            MatchControllerButtons.RightShoulder | MatchControllerButtons.Start | MatchControllerButtons.Back |
            MatchControllerButtons.DPadLeft | MatchControllerButtons.DPadRight;
        var battingActions = MatchControllerInputModel.ReadPressedActions(
            playerButtons, MatchControllerButtons.None, isCpuBattingControlled: false, isMatchComplete: false);
        Require(HasAll(battingActions,
                MatchControllerActions.Exit | MatchControllerActions.Pause |
                MatchControllerActions.Defend | MatchControllerActions.Loft |
                MatchControllerActions.Run | MatchControllerActions.NextBall |
                MatchControllerActions.AimOffSide | MatchControllerActions.AimLegSide) &&
            !HasAny(battingActions, MatchControllerActions.SelectStandardDelivery |
                MatchControllerActions.SelectWideDelivery | MatchControllerActions.SelectNoBallDelivery |
                MatchControllerActions.SelectYorkerDelivery | MatchControllerActions.Drive |
                MatchControllerActions.CancelRun | MatchControllerActions.StepOffSide |
                MatchControllerActions.StepLegSide),
            "normal controller batting input did not map to the directional two-shot controls");

        var heldActions = MatchControllerInputModel.ReadPressedActions(
            playerButtons, playerButtons, isCpuBattingControlled: false, isMatchComplete: false);
        Require(heldActions == MatchControllerActions.None,
            "a held controller button repeatedly retriggered an edge action");

        var pausedButtons = MatchControllerButtons.X | MatchControllerButtons.Y |
            MatchControllerButtons.A | MatchControllerButtons.B | MatchControllerButtons.DPadLeft |
            MatchControllerButtons.Start | MatchControllerButtons.LeftShoulder |
            MatchControllerButtons.RightShoulder;
        var pausedActions = MatchControllerInputModel.ReadPressedActions(
            pausedButtons, MatchControllerButtons.None, isCpuBattingControlled: false,
            isMatchComplete: false, isPaused: true);
        Require(HasAll(pausedActions, MatchControllerActions.Pause |
                MatchControllerActions.ToggleHighContrast | MatchControllerActions.ToggleLargeText |
                MatchControllerActions.DecreaseEffectsVolume | MatchControllerActions.IncreaseEffectsVolume) &&
            !HasAny(pausedActions, MatchControllerActions.Defend | MatchControllerActions.Drive |
                MatchControllerActions.Run | MatchControllerActions.SelectStandardDelivery),
            "paused controller inputs did not expose settings without triggering match actions");

        var pausedResultActions = MatchControllerInputModel.ReadPressedActions(
            pausedButtons, MatchControllerButtons.None, isCpuBattingControlled: false,
            isMatchComplete: true, isPaused: true);
        Require(HasAll(pausedResultActions, MatchControllerActions.Pause |
                MatchControllerActions.ToggleHighContrast | MatchControllerActions.ToggleLargeText |
                MatchControllerActions.DecreaseEffectsVolume | MatchControllerActions.IncreaseEffectsVolume) &&
            !HasAny(pausedResultActions, MatchControllerActions.RestartMatch |
                MatchControllerActions.CycleDifficulty | MatchControllerActions.CycleOvers),
            "paused result controls did not prioritize accessibility settings over result actions");

        var bowlingButtons = MatchControllerButtons.LeftShoulder | MatchControllerButtons.DPadLeft | MatchControllerButtons.DPadUp |
            MatchControllerButtons.DPadDown | MatchControllerButtons.DPadRight |
            MatchControllerButtons.RightShoulder;
        var bowlingActions = MatchControllerInputModel.ReadPressedActions(
            bowlingButtons, MatchControllerButtons.None, isCpuBattingControlled: true, isMatchComplete: false);
        Require(HasAll(bowlingActions,
                MatchControllerActions.CycleDelivery | MatchControllerActions.NextBall |
                MatchControllerActions.AimOffSide | MatchControllerActions.AimLegSide |
                MatchControllerActions.AimLong | MatchControllerActions.AimShort) &&
            !HasAny(bowlingActions, MatchControllerActions.SelectStandardDelivery |
                MatchControllerActions.SelectWideDelivery | MatchControllerActions.SelectNoBallDelivery |
                MatchControllerActions.SelectYorkerDelivery | MatchControllerActions.StepOffSide |
                MatchControllerActions.StepLegSide),
            "normal controller bowling input did not use one delivery selector and one next-ball button");

        var developerBattingActions = MatchControllerInputModel.ReadPressedActions(
            playerButtons, MatchControllerButtons.None, isCpuBattingControlled: false,
            isMatchComplete: false, developerMode: true);
        Require(HasAll(developerBattingActions,
                MatchControllerActions.Defend | MatchControllerActions.Drive | MatchControllerActions.Loft |
                MatchControllerActions.CancelRun | MatchControllerActions.StepOffSide |
                MatchControllerActions.StepLegSide),
            "developer mode did not retain the detailed legacy batting controls");

        var developerBowlingActions = MatchControllerInputModel.ReadPressedActions(
            bowlingButtons, MatchControllerButtons.None, isCpuBattingControlled: true,
            isMatchComplete: false, developerMode: true);
        Require(HasAll(developerBowlingActions,
                MatchControllerActions.SelectStandardDelivery | MatchControllerActions.SelectWideDelivery |
                MatchControllerActions.SelectNoBallDelivery | MatchControllerActions.SelectYorkerDelivery |
                MatchControllerActions.NextBall),
            "developer mode did not retain individual bowling delivery selection");

        var resultButtons = MatchControllerButtons.A | MatchControllerButtons.LeftShoulder |
            MatchControllerButtons.RightShoulder;
        var resultActions = MatchControllerInputModel.ReadPressedActions(
            resultButtons, MatchControllerButtons.None, isCpuBattingControlled: true, isMatchComplete: true);
        Require(HasAll(resultActions,
                MatchControllerActions.RestartMatch | MatchControllerActions.CycleDifficulty |
                MatchControllerActions.CycleOvers) &&
            !HasAny(resultActions, MatchControllerActions.Defend | MatchControllerActions.CancelRun |
                MatchControllerActions.NextBall),
            "controller result buttons did not map to restart, difficulty, and overs options");

        Console.WriteLine("PASS: controller actions are context-aware and edge-triggered for batting, bowling, pause, and match results.");
    }

    private static bool HasAll(MatchControllerActions actions, MatchControllerActions expected) =>
        (actions & expected) == expected;

    private static bool HasAny(MatchControllerActions actions, MatchControllerActions values) =>
        (actions & values) != 0;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Controller input check failed: {message}");
    }
}
