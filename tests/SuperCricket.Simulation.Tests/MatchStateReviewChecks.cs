namespace SuperCricket.Simulation;

public static class MatchStateReviewChecks
{
    public static void Run()
    {
        var match = new MatchState();
        var first = match.BeginDelivery(isNoBall: false);
        Require(first.ResolveIncoming(isWide: true, hitsWickets: true) == IncomingDeliveryResolution.Wide,
            "a wide was not resolved before wicket contact");
        var wide = match.CompleteDelivery();
        Require(wide.Extra == DeliveryExtra.Wide && !wide.IsLegal && match.Runs == 1 && match.LegalBalls == 0,
            "a wide did not score once without using a legal ball");
        Require(match.CompleteDelivery() == wide && match.Runs == 1,
            "completing the same delivery twice changed the score");

        var caught = match.BeginDelivery(isNoBall: false);
        caught.RecordCompletedRun();
        Require(caught.ResolveCatch(), "a legal catch was not a dismissal");
        var caughtResult = match.CompleteDelivery();
        Require(caughtResult.BatterRuns == 0 && caughtResult.CompletedRuns == 0 &&
            caughtResult.Dismissal == DismissalKind.Caught && match.Wickets == 1,
            "a legal catch retained completed runs or failed to dismiss the batter");

        var noBall = match.BeginDelivery(isNoBall: true);
        noBall.RecordCompletedRun();
        Require(!noBall.ResolveCatch(), "a catch from a no-ball dismissed the batter");
        var noBallResult = match.CompleteDelivery();
        Require(noBallResult.Extra == DeliveryExtra.NoBall && noBallResult.ExtraRuns == 1 &&
            noBallResult.BatterRuns == 1 && noBallResult.Dismissal == DismissalKind.None &&
            match.LegalBalls == 1,
            "a no-ball catch did not preserve runs and the penalty without using a legal ball");

        var boundary = match.BeginDelivery(isNoBall: false);
        for (var run = 0; run < 4; run++)
            boundary.RecordCompletedRun();
        Require(boundary.ResolveBoundary(clearedInTheAir: false, currentRunCrossed: true) == 5,
            "the boundary did not preserve the higher running allowance");
        var boundaryResult = match.CompleteDelivery();
        Require(boundaryResult.BatterRuns == 5 && boundaryResult.CompletedRuns == 5 && match.Runs == 8,
            "the boundary result did not reach the scorecard with its crossed run");

        var noBallRunOut = match.BeginDelivery(isNoBall: true);
        noBallRunOut.RecordCompletedRun();
        noBallRunOut.ResolveRunOut();
        var runOutResult = match.CompleteDelivery();
        Require(runOutResult.Dismissal == DismissalKind.RunOut && runOutResult.BatterRuns == 1 &&
            runOutResult.ExtraRuns == 1,
            "a run-out on a no-ball did not preserve the run and penalty");

        Console.WriteLine("PASS: match delivery lifecycle, extras, catches, boundaries, and run-outs.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Match-state check failed: {message}");
    }
}
