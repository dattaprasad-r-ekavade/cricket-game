using SuperCricket.Content;

namespace SuperCricket.Simulation;

public readonly record struct AutomatedMatchResult(
    int Seed,
    MatchInningsResult FirstInnings,
    MatchInningsResult SecondInnings,
    string ResultText,
    int TotalDeliveries,
    PhysicsMatchMetrics? PhysicsMetrics = null);

public readonly record struct PhysicsMatchMetrics(
    int ShotPlans,
    int Contacts,
    int Misses,
    int Catches,
    int GroundPickups,
    int Boundaries,
    int RunIntents,
    int SafeRunAttempts,
    int TwoRunPlans,
    int TwoRunScores,
    int CompletedRuns,
    int RunOuts,
    int BowledDismissals,
    int WideDeliveries,
    int NoBallDeliveries,
    int Leaves);

/// <summary>Runs seeded, renderer-free matches through the production innings and delivery rules.</summary>
public static class AutomatedMatchBatchSimulator
{
    public const int MaximumMatchesPerBatch = 5000;
    internal const int MaximumExtraDeliveriesPerInnings = 256;

    public static IReadOnlyList<AutomatedMatchResult> RunBatch(
        TeamRosterAsset firstTeam,
        TeamRosterAsset secondTeam,
        int matchCount,
        int oversPerInnings,
        int seed)
    {
        ArgumentNullException.ThrowIfNull(firstTeam);
        ArgumentNullException.ThrowIfNull(secondTeam);
        if (matchCount is < 1 or > MaximumMatchesPerBatch)
            throw new ArgumentOutOfRangeException(nameof(matchCount), $"Match count must be between 1 and {MaximumMatchesPerBatch}.");
        if (!LimitedOversMatch.SupportedOversPerInnings.Contains(oversPerInnings))
            throw new ArgumentOutOfRangeException(nameof(oversPerInnings), "Choose 1, 2, 5, or 10 overs per innings.");

        var results = new AutomatedMatchResult[matchCount];
        for (var index = 0; index < matchCount; index++)
        {
            var matchSeed = unchecked(seed + index);
            results[index] = SimulateMatch(firstTeam, secondTeam, oversPerInnings, matchSeed);
        }

        return Array.AsReadOnly(results);
    }

    private static AutomatedMatchResult SimulateMatch(
        TeamRosterAsset firstTeam,
        TeamRosterAsset secondTeam,
        int oversPerInnings,
        int seed)
    {
        var random = new Random(seed);
        var match = new LimitedOversMatch(firstTeam, secondTeam, oversPerInnings);
        var deliveries = 0;
        var deliveriesThisInnings = 0;
        var maximumDeliveriesThisInnings = oversPerInnings * OverScoreboard.BallsPerOver + MaximumExtraDeliveriesPerInnings;

        while (!match.IsMatchComplete)
        {
            if (match.IsInningsComplete)
            {
                if (match.InningsNumber != 1)
                    throw new InvalidOperationException("The second innings reached its limit without completing the match.");
                match.StartNextInnings();
                deliveriesThisInnings = 0;
                continue;
            }

            if (deliveriesThisInnings >= maximumDeliveriesThisInnings)
                throw new InvalidOperationException($"Seed {seed} exceeded the delivery limit in innings {match.InningsNumber}.");

            SimulateDelivery(match, random);
            deliveriesThisInnings++;
            deliveries++;
        }

        if (match.FirstInnings is not { } first || match.SecondInnings is not { } second || string.IsNullOrWhiteSpace(match.ResultText))
            throw new InvalidOperationException($"Seed {seed} ended without two innings and a match result.");

        return new AutomatedMatchResult(seed, first, second, match.ResultText, deliveries);
    }

    private static void SimulateDelivery(LimitedOversMatch match, Random random)
    {
        var wideChance = 0.025;
        if (random.NextDouble() < wideChance)
        {
            var wide = match.BeginDelivery(isNoBall: false);
            wide.ResolveIncoming(isWide: true, hitsWickets: false);
            match.CompleteDelivery();
            return;
        }

        var isNoBall = random.NextDouble() < 0.008;
        var delivery = match.BeginDelivery(isNoBall);
        var striker = match.StrikerPlayer;
        var situation = new BowlingSituation(
            match.LegalBalls,
            match.OversPerInnings,
            match.Runs,
            match.Wickets,
            match.Target);
        var fieldingTactic = FieldPlacementModel.ChooseTactic(situation, striker.Power);
        var battingDecision = CpuBattingOutcomeModel.Choose(
            striker,
            match.CurrentBowler,
            match.FieldingPlayers,
            situation,
            fieldingTactic);

        if (!isNoBall && random.NextDouble() < battingDecision.WicketChance)
        {
            if (battingDecision.Shot == CpuShotChoice.Loft && delivery.ResolveCatch())
            {
                match.CompleteDelivery();
                return;
            }

            delivery.ResolveIncoming(isWide: false, hitsWickets: true);
            match.CompleteDelivery();
            return;
        }

        if (random.NextDouble() < battingDecision.BoundaryChance)
        {
            var clearedInTheAir = battingDecision.Shot == CpuShotChoice.Loft &&
                random.NextDouble() < battingDecision.BoundaryClearedChance;
            var currentRunCrossed = random.NextDouble() < 0.025;
            delivery.ResolveBoundary(clearedInTheAir, currentRunCrossed);
            match.CompleteDelivery();
            return;
        }

        var runRoll = random.NextDouble();
        var runs = runRoll < battingDecision.OneRunChance
            ? 1
            : runRoll < battingDecision.OneRunChance + battingDecision.TwoRunChance
                ? 2
                : runRoll < battingDecision.OneRunChance + battingDecision.TwoRunChance + battingDecision.ThreeRunChance
                    ? 3
                    : 0;
        for (var run = 0; run < runs; run++)
            delivery.RecordCompletedRun();

        if (runs > 0 && random.NextDouble() < battingDecision.RunOutChance)
            delivery.ResolveRunOut();
        match.CompleteDelivery();
    }
}
