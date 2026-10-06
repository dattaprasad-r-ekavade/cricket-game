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
                MatchControllerActions.Defend | MatchControllerActions.Drive | MatchControllerActions.Loft |
                MatchControllerActions.Run | MatchControllerActions.CancelRun | MatchControllerActions.NextBall |
                MatchControllerActions.StepOffSide | MatchControllerActions.StepLegSide) &&
            !HasAny(battingActions, MatchControllerActions.SelectStandardDelivery |
                MatchControllerActions.SelectWideDelivery | MatchControllerActions.SelectNoBallDelivery),
            "controller batting buttons did not map to the documented match actions");

        var heldActions = MatchControllerInputModel.ReadPressedActions(
            playerButtons, playerButtons, isCpuBattingControlled: false, isMatchComplete: false);
        Require(heldActions == MatchControllerActions.None,
            "a held controller button repeatedly retriggered an edge action");

        var bowlingButtons = MatchControllerButtons.DPadLeft | MatchControllerButtons.DPadUp |
            MatchControllerButtons.DPadDown | MatchControllerButtons.RightShoulder;
        var bowlingActions = MatchControllerInputModel.ReadPressedActions(
            bowlingButtons, MatchControllerButtons.None, isCpuBattingControlled: true, isMatchComplete: false);
        Require(HasAll(bowlingActions,
                MatchControllerActions.SelectStandardDelivery | MatchControllerActions.SelectWideDelivery |
                MatchControllerActions.SelectNoBallDelivery | MatchControllerActions.NextBall) &&
            !HasAny(bowlingActions, MatchControllerActions.StepOffSide | MatchControllerActions.StepLegSide),
            "controller bowling buttons did not select deliveries in the CPU batting innings");

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
