using SuperCricket.Content;

namespace SuperCricket.Simulation;

public readonly record struct AutomatedMatchResult(
    int Seed,
    MatchInningsResult FirstInnings,
    MatchInningsResult SecondInnings,
    string ResultText,
    int TotalDeliveries);

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
        var ballsRemaining = Math.Max(1, match.OversPerInnings * OverScoreboard.BallsPerOver - match.LegalBalls);
        var runsRequired = match.Target is { } target ? Math.Max(0, target - match.Runs) : 0;
        var requiredRate = match.Target is null ? 0f : runsRequired / (float)ballsRemaining;
        var pressure = Math.Clamp((requiredRate - 0.7f) / 1.8f, 0f, 1f);
        var timing = striker.Timing / 100f;
        var power = striker.Power / 100f;
        var aggression = Math.Clamp(0.28f + pressure * 0.48f + (power - 0.5f) * 0.24f, 0.12f, 0.92f);
        if (match.Target is null && match.Wickets >= 7)
            aggression *= 0.78f;

        var shotChoice = ChooseShot(aggression, power, match.Wickets);
        var wicketChance = shotChoice switch
        {
            SimulatedShot.Defence => 0.035f,
            SimulatedShot.Drive => 0.065f,
            _ => 0.13f
        };
        wicketChance *= 1.12f - timing * 0.24f;
        wicketChance += pressure * 0.025f;

        if (!isNoBall && random.NextDouble() < wicketChance)
        {
            if (shotChoice == SimulatedShot.Loft && delivery.ResolveCatch())
            {
                match.CompleteDelivery();
                return;
            }

            delivery.ResolveIncoming(isWide: false, hitsWickets: true);
            match.CompleteDelivery();
            return;
        }

        var boundaryChance = shotChoice switch
        {
            SimulatedShot.Defence => 0.025f,
            SimulatedShot.Drive => 0.09f,
            _ => 0.18f
        };
        boundaryChance += power * 0.08f + pressure * 0.04f;
        if (random.NextDouble() < boundaryChance)
        {
            var clearedInTheAir = shotChoice == SimulatedShot.Loft && random.NextDouble() < 0.78;
            var currentRunCrossed = random.NextDouble() < 0.025;
            delivery.ResolveBoundary(clearedInTheAir, currentRunCrossed);
            match.CompleteDelivery();
            return;
        }

        var runChance = 0.50f + aggression * 0.12f;
        var runRoll = random.NextDouble();
        var runs = runRoll < runChance
            ? 1
            : runRoll < runChance + 0.16f + aggression * 0.06f
                ? 2
                : runRoll < runChance + 0.19f + aggression * 0.07f
                    ? 3
                    : 0;
        for (var run = 0; run < runs; run++)
            delivery.RecordCompletedRun();

        if (runs > 0 && random.NextDouble() < 0.008 + (1f - timing) * 0.012)
            delivery.ResolveRunOut();
        match.CompleteDelivery();
    }

    private static SimulatedShot ChooseShot(float aggression, float power, int wickets)
    {
        if (aggression >= 0.64f && power >= 0.4f)
            return SimulatedShot.Loft;
        if (aggression < 0.29f || wickets >= 9 && aggression < 0.52f)
            return SimulatedShot.Defence;
        return SimulatedShot.Drive;
    }

    private enum SimulatedShot
    {
        Defence,
        Drive,
        Loft
    }
}
