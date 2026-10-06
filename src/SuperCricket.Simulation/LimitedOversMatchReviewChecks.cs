using SuperCricket.Content;

namespace SuperCricket.Simulation;

public static class LimitedOversMatchReviewChecks
{
    public static void Run()
    {
        var firstTeam = TeamRosterAsset.CreatePlaceholder("Coastal XI");
        firstTeam.PrimaryKitColorHex = "#1575B8";
        var secondTeam = TeamRosterAsset.CreatePlaceholder("Highland XI");
        secondTeam.PrimaryKitColorHex = "#8F2443";
        var chase = new LimitedOversMatch(firstTeam, secondTeam, oversPerInnings: 2);
        Require(chase.FirstTeamName == "Coastal XI" && chase.BattingTeamName == "Coastal XI",
            "the match did not begin with the first fictional team batting");
        Require(ReferenceEquals(chase.BattingTeam, firstTeam) && ReferenceEquals(chase.FieldingTeam, secondTeam),
            "the first innings assigned the wrong teams to the batting and fielding sides");
        RequireThrows(chase.StartNextInnings, "the second innings started before the first was complete");

        var firstBall = chase.BeginDelivery(isNoBall: false);
        firstBall.ResolveBoundary(clearedInTheAir: false, currentRunCrossed: false);
        chase.CompleteDelivery();
        for (var ball = 1; ball < OverScoreboard.BallsPerOver * 2; ball++)
            CompleteDotBall(chase);

        Require(chase.IsInningsComplete && !chase.IsMatchComplete && chase.FirstInnings is { Runs: 4, LegalBalls: 12 } first &&
            first.OversText == "2.0" && chase.Target == null,
            "the two-over first innings did not stop at its configured limit");
        RequireThrows(() => chase.BeginDelivery(isNoBall: false), "another delivery started after the first innings limit");
        chase.StartNextInnings();
        Require(chase.InningsNumber == 2 && chase.BattingTeamName == "Highland XI" && chase.Target == 5,
            "the second innings did not swap teams and set a target one above the first score");
        Require(ReferenceEquals(chase.BattingTeam, secondTeam) && ReferenceEquals(chase.FieldingTeam, firstTeam),
            "the second innings did not swap the batting and fielding team identities");

        var chasingBoundary = chase.BeginDelivery(isNoBall: false);
        chasingBoundary.ResolveBoundary(clearedInTheAir: false, currentRunCrossed: false);
        chase.CompleteDelivery();
        Require(!chase.IsMatchComplete, "the chase ended before reaching its target");
        var winningRun = chase.BeginDelivery(isNoBall: false);
        winningRun.RecordCompletedRun();
        chase.CompleteDelivery();
        Require(chase.IsMatchComplete && chase.SecondInnings is { Runs: 5, LegalBalls: 2 } second &&
            second.TeamName == "Highland XI" && chase.ResultText == "Highland XI wins by 10 wickets",
            "the second innings did not finish immediately when the target was reached");
        RequireThrows(() => chase.BeginDelivery(isNoBall: false), "a delivery started after the match result was decided");

        var defended = new LimitedOversMatch(oversPerInnings: 1);
        for (var ball = 0; ball < OverScoreboard.BallsPerOver; ball++)
        {
            var delivery = defended.BeginDelivery(isNoBall: false);
            if (ball < 3)
                delivery.RecordCompletedRun();
            defended.CompleteDelivery();
        }
        defended.StartNextInnings();
        for (var ball = 0; ball < OverScoreboard.BallsPerOver; ball++)
            CompleteDotBall(defended);
        Require(defended.IsMatchComplete && defended.ResultText == "Coastal XI wins by 3 runs",
            "the defending side's run margin was not reported when the chase ended on overs");

        Console.WriteLine("PASS: two-innings match, over limits, target chase, and match results.");
    }

    private static void CompleteDotBall(LimitedOversMatch match)
    {
        match.BeginDelivery(isNoBall: false);
        match.CompleteDelivery();
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException($"Limited-overs review failed: {message}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Limited-overs review failed: {message}.");
    }
}
