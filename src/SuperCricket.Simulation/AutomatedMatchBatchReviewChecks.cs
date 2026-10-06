using SuperCricket.Content;

namespace SuperCricket.Simulation;

public static class AutomatedMatchBatchReviewChecks
{
    public static void Run()
    {
        var firstTeam = TeamRosterAsset.CreatePlaceholder("Coastal XI");
        var secondTeam = TeamRosterAsset.CreatePlaceholder("Highland XI");
        firstTeam.Players[0].Timing = 76;
        firstTeam.Players[0].Power = 52;
        secondTeam.Players[0].Timing = 70;
        secondTeam.Players[0].Power = 58;

        var completedMatches = 0;
        var checkedBatches = 0;
        foreach (var overs in LimitedOversMatch.SupportedOversPerInnings)
        {
            var firstRun = AutomatedMatchBatchSimulator.RunBatch(firstTeam, secondTeam, 8, overs, seed: 3010 + overs);
            var repeatedRun = AutomatedMatchBatchSimulator.RunBatch(firstTeam, secondTeam, 8, overs, seed: 3010 + overs);
            Require(firstRun.SequenceEqual(repeatedRun), $"the {overs}-over match batch changed when its seed was repeated");
            Require(firstRun.Count == 8, $"the {overs}-over batch returned the wrong match count");

            foreach (var match in firstRun)
            {
                Require(match.FirstInnings.InningsNumber == 1 && match.SecondInnings.InningsNumber == 2,
                    "an automatic match did not record both innings in order");
                Require(match.FirstInnings.OversPerInnings == overs && match.SecondInnings.OversPerInnings == overs,
                    "an automatic match changed its selected innings length");
                Require(match.FirstInnings.Runs >= 0 && match.SecondInnings.Runs >= 0 &&
                    match.FirstInnings.Wickets is >= 0 and <= OverScoreboard.MaximumWickets &&
                    match.SecondInnings.Wickets is >= 0 and <= OverScoreboard.MaximumWickets,
                    "an automatic match produced an invalid score or wicket count");
                Require(match.FirstInnings.LegalBalls is >= 0 && match.FirstInnings.LegalBalls <= overs * OverScoreboard.BallsPerOver &&
                    match.SecondInnings.LegalBalls is >= 0 && match.SecondInnings.LegalBalls <= overs * OverScoreboard.BallsPerOver,
                    "an automatic match exceeded its legal-ball limit");
                Require(match.TotalDeliveries >= match.FirstInnings.LegalBalls + match.SecondInnings.LegalBalls &&
                    match.TotalDeliveries <= overs * OverScoreboard.BallsPerOver * 2 + AutomatedMatchBatchSimulator.MaximumExtraDeliveriesPerInnings * 2,
                    "an automatic match exceeded its delivery guard");
                Require(!string.IsNullOrWhiteSpace(match.ResultText), "an automatic match ended without a result");
            }

            completedMatches += firstRun.Count;
            checkedBatches++;
        }

        RequireThrows(() => AutomatedMatchBatchSimulator.RunBatch(firstTeam, secondTeam, 0, 1, 1),
            "a zero-sized automatic match batch was accepted");
        RequireThrows(() => AutomatedMatchBatchSimulator.RunBatch(firstTeam, secondTeam, 1, 3, 1),
            "an unsupported match length was accepted");
        Console.WriteLine($"PASS: {completedMatches} deterministic automatic matches across {checkedBatches} innings lengths; results, wicket limits, and delivery guards held.");
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }
        throw new InvalidOperationException($"Automatic match batch review failed: {message}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Automatic match batch review failed: {message}.");
    }
}
