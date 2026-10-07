using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

/// <summary>Runs seeded matches through exported swing clips, ball-flight physics, fielding, and match rules.</summary>
public static class PhysicsAutomatedMatchBatchSimulator
{
    public const int MaximumMatchesPerBatch = 100;

    public static IReadOnlyList<AutomatedMatchResult> RunBatch(
        TeamRosterAsset firstTeam,
        TeamRosterAsset secondTeam,
        FieldPreset fieldPreset,
        PlayerAsset batterAsset,
        PlayerAsset bowlerAsset,
        BattingShotSet shotSet,
        DeliveryPreset stockDelivery,
        DeliveryPreset wideDelivery,
        DeliveryPreset noBallDelivery,
        int matchCount,
        int oversPerInnings,
        int seed)
    {
        ArgumentNullException.ThrowIfNull(firstTeam);
        ArgumentNullException.ThrowIfNull(secondTeam);
        ArgumentNullException.ThrowIfNull(fieldPreset);
        ArgumentNullException.ThrowIfNull(batterAsset);
        ArgumentNullException.ThrowIfNull(bowlerAsset);
        ArgumentNullException.ThrowIfNull(shotSet);
        ArgumentNullException.ThrowIfNull(stockDelivery);
        ArgumentNullException.ThrowIfNull(wideDelivery);
        ArgumentNullException.ThrowIfNull(noBallDelivery);
        if (matchCount is < 1 or > MaximumMatchesPerBatch)
            throw new ArgumentOutOfRangeException(nameof(matchCount), $"Match count must be between 1 and {MaximumMatchesPerBatch}.");
        if (!LimitedOversMatch.SupportedOversPerInnings.Contains(oversPerInnings))
            throw new ArgumentOutOfRangeException(nameof(oversPerInnings), "Choose 1, 2, 5, or 10 overs per innings.");

        var fieldErrors = fieldPreset.Validate();
        if (fieldErrors.Count > 0)
            throw new ArgumentException($"Invalid physics-match field: {string.Join(" ", fieldErrors)}", nameof(fieldPreset));
        ValidateDelivery(stockDelivery, "stock delivery", expectedNoBall: false, fieldPreset.BoundaryRadiusMeters);
        ValidateDelivery(wideDelivery, "wide delivery", expectedNoBall: false, fieldPreset.BoundaryRadiusMeters);
        ValidateDelivery(noBallDelivery, "no-ball delivery", expectedNoBall: true, fieldPreset.BoundaryRadiusMeters);
        foreach (var shot in new[] { "defence", "drive", "loft" })
            _ = shotSet.Get(shot);

        var results = new AutomatedMatchResult[matchCount];
        for (var index = 0; index < matchCount; index++)
        {
            var matchSeed = unchecked(seed + index);
            results[index] = SimulateMatch(
                firstTeam, secondTeam, fieldPreset, batterAsset, bowlerAsset, shotSet,
                stockDelivery, wideDelivery, noBallDelivery, oversPerInnings, matchSeed);
        }
        return Array.AsReadOnly(results);
    }

    private static AutomatedMatchResult SimulateMatch(
        TeamRosterAsset firstTeam,
        TeamRosterAsset secondTeam,
        FieldPreset fieldPreset,
        PlayerAsset batterAsset,
        PlayerAsset bowlerAsset,
        BattingShotSet shotSet,
        DeliveryPreset stockDelivery,
        DeliveryPreset wideDelivery,
        DeliveryPreset noBallDelivery,
        int oversPerInnings,
        int seed)
    {
        var random = new Random(seed);
        var match = new LimitedOversMatch(firstTeam, secondTeam, oversPerInnings);
        var telemetry = new PhysicsMatchTelemetry();
        var deliveries = 0;
        var deliveriesThisInnings = 0;
        var maximumDeliveriesThisInnings = oversPerInnings * OverScoreboard.BallsPerOver +
            AutomatedMatchBatchSimulator.MaximumExtraDeliveriesPerInnings;

        while (!match.IsMatchComplete)
        {
            if (match.IsInningsComplete)
            {
                if (match.InningsNumber != 1)
                    throw new InvalidOperationException("The second physics innings reached its limit without completing the match.");
                match.StartNextInnings();
                deliveriesThisInnings = 0;
                continue;
            }

            if (deliveriesThisInnings >= maximumDeliveriesThisInnings)
                throw new InvalidOperationException($"Physics match seed {seed} exceeded the delivery limit in innings {match.InningsNumber}.");

            SimulateDelivery(
                match, random, fieldPreset, batterAsset, bowlerAsset, shotSet,
                stockDelivery, wideDelivery, noBallDelivery, telemetry);
            deliveriesThisInnings++;
            deliveries++;
        }

        if (match.FirstInnings is not { } first || match.SecondInnings is not { } second || string.IsNullOrWhiteSpace(match.ResultText))
            throw new InvalidOperationException($"Physics match seed {seed} ended without two innings and a result.");
        return new AutomatedMatchResult(seed, first, second, match.ResultText, deliveries, telemetry.ToMetrics());
    }

    private static void SimulateDelivery(
        LimitedOversMatch match,
        Random random,
        FieldPreset fieldPreset,
        PlayerAsset batterAsset,
        PlayerAsset bowlerAsset,
        BattingShotSet shotSet,
        DeliveryPreset stockDelivery,
        DeliveryPreset wideDelivery,
        DeliveryPreset noBallDelivery,
        PhysicsMatchTelemetry telemetry)
    {
        var striker = match.StrikerPlayer;
        var situation = new BowlingSituation(
            match.LegalBalls,
            match.OversPerInnings,
            match.Runs,
            match.Wickets,
            match.Target);
        var formation = FieldPlacementModel.Choose(fieldPreset, situation, striker.Power);
        var tactic = formation.Tactic;
        var fieldingPlayers = match.FieldingPlayers;
        var fieldingRatings = fieldingPlayers.Select(player => player.Fielding).ToArray();
        var fielding = new FieldingSide();
        fielding.ConfigureStartingPositions(formation.StartingPositions);
        fielding.ConfigureFieldingRatings(fieldingRatings);
        var delivery = ChooseDelivery(
            random, stockDelivery, wideDelivery, noBallDelivery,
            match.CurrentBowler, striker, situation);
        var session = match.BeginDelivery(delivery.IsNoBall);
        var decision = CpuLiveBattingPlanModel.Choose(
            striker,
            match.CurrentBowler,
            match.FieldingPlayers,
            situation,
            tactic,
            delivery,
            batterAsset,
            bowlerAsset,
            shotSet,
            random.Next(),
            fielding.Positions);
        if (decision.Leave)
        {
            telemetry.Leaves++;
            ResolveMissedDelivery(session, delivery);
            match.CompleteDelivery();
            if (session.Dismissal == DismissalKind.Bowled)
                telemetry.BowledDismissals++;
            if (session.Extra == DeliveryExtra.Wide)
                telemetry.WideDeliveries++;
            if (session.Extra == DeliveryExtra.NoBall)
                telemetry.NoBallDeliveries++;
            return;
        }

        telemetry.ShotPlans++;
        if (decision.AttemptRun)
            telemetry.RunIntents++;
        var shotName = decision.Shot switch
        {
            CpuShotChoice.Defence => "defence",
            CpuShotChoice.Drive => "drive",
            _ => "loft"
        };
        var trajectory = BattingPracticeAnalyzer.AnalyzeShotTrajectory(
            batterAsset,
            bowlerAsset,
            shotSet,
            shotName,
            delivery,
            decision.InputDelaySeconds,
            decision.FootworkOffsetMeters,
            striker,
            decision.HorizontalAim);

        if (!trajectory.Sample.ContactQuality.HasValue)
        {
            telemetry.Misses++;
            ResolveMissedDelivery(session, delivery);
            match.CompleteDelivery();
            if (session.Dismissal == DismissalKind.Bowled)
                telemetry.BowledDismissals++;
            if (session.Extra == DeliveryExtra.Wide)
                telemetry.WideDeliveries++;
            if (session.Extra == DeliveryExtra.NoBall)
                telemetry.NoBallDeliveries++;
            return;
        }
        if (trajectory.ContactPosition is not { } contactPosition ||
            trajectory.OutgoingVelocity is not { } outgoingVelocity ||
            trajectory.OutgoingFrames.Count < 2)
            throw new InvalidOperationException("A physics batting contact did not include its point, velocity, and flight frames.");

        var pickupDuration = GetAnimationDuration(bowlerAsset, "fielder-pickup");
        var throwDuration = GetAnimationDuration(bowlerAsset, "fielder-throw");
        var runDecision = CpuLiveRunningDecisionModel.Choose(
            decision.AttemptRun,
            delivery,
            contactPosition,
            outgoingVelocity,
            fielding.Positions,
            fieldingRatings,
            CpuLiveRunningDecisionModel.DefaultRunDurationSeconds,
            pickupDuration,
            throwDuration);
        telemetry.Contacts++;
        if (runDecision.PlannedRuns > 1)
            telemetry.TwoRunPlans++;
        telemetry.SafeRunAttempts += runDecision.PlannedRuns;
        SimulateBattedBall(
            match,
            session,
            delivery,
            trajectory,
            fielding,
            runDecision.PlannedRuns,
            pickupDuration,
            throwDuration,
            telemetry);
        if (session.Extra == DeliveryExtra.Wide)
            telemetry.WideDeliveries++;
        if (session.Extra == DeliveryExtra.NoBall)
            telemetry.NoBallDeliveries++;
    }

    private static DeliveryPreset ChooseDelivery(
        Random random,
        DeliveryPreset stockDelivery,
        DeliveryPreset wideDelivery,
        DeliveryPreset noBallDelivery,
        TeamPlayerData bowler,
        TeamPlayerData striker,
        BowlingSituation situation)
    {
        if (random.NextDouble() < 0.025)
            return wideDelivery.DeepCopy();
        if (random.NextDouble() < 0.008)
            return noBallDelivery.DeepCopy();

        return BowlingDecisionModel.ChooseDelivery(
            stockDelivery,
            bowler.Bowling,
            striker.Power,
            situation,
            random.Next()).Delivery;
    }

    private static void ResolveMissedDelivery(DeliverySession session, DeliveryPreset delivery)
    {
        var ball = new BallFlightSimulator(delivery);
        var previous = ball.CurrentFrame;
        var wicketLineZ = BattingPracticeAnalyzer.BatterWicketLineZ;
        var maximumSteps = (int)MathF.Ceiling(delivery.MaximumSimulationSeconds / delivery.FixedTimeStepSeconds) + 1;
        for (var step = 0; step < maximumSteps && previous.Phase != BallMotionPhase.Settled; step++)
        {
            var current = ball.Step();
            if (TryCrossPlane(previous.Position, current.Position, wicketLineZ, out var crossing))
            {
                var isWide = CricketDeliveryRuleModel.IsWide(crossing.X, delivery.PitchWidthMeters);
                var hitsWickets = MathF.Abs(crossing.X) <= 0.12f + delivery.BallRadiusMeters &&
                    crossing.Y >= 0f && crossing.Y <= 0.71f + delivery.BallRadiusMeters;
                session.ResolveIncoming(isWide, hitsWickets);
                return;
            }
            previous = current;
        }
    }

    internal static void SimulateBattedBall(
        LimitedOversMatch match,
        DeliverySession session,
        DeliveryPreset delivery,
        BattingPracticeTrajectory trajectory,
        FieldingSide fielding,
        int plannedRuns,
        float pickupDuration,
        float throwDuration,
        PhysicsMatchTelemetry telemetry)
    {
        var frames = trajectory.OutgoingFrames;
        var contactTime = trajectory.Sample.ContactTimeSeconds
            ?? throw new InvalidOperationException("A hit must include a contact time.");
        var completedRuns = 0;
        var previous = frames[0];
        for (var index = 1; index < frames.Count; index++)
        {
            var current = frames[index];
            var deltaTime = current.TimeSeconds - previous.TimeSeconds;
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f)
                throw new InvalidOperationException("Physics match trajectory frames are not in strictly increasing time order.");

            fielding.Step(deltaTime, previous.Position);
            var elapsedSinceContact = current.TimeSeconds - contactTime;
            while (completedRuns < plannedRuns &&
                elapsedSinceContact >= (completedRuns + 1) * CpuLiveRunningDecisionModel.DefaultRunDurationSeconds)
            {
                session.RecordCompletedRun();
                telemetry.CompletedRuns++;
                completedRuns++;
            }
            if (fielding.TryFindContact(previous, current, out var fieldingContact))
            {
                if (fieldingContact.Kind == FieldingContactKind.Catch)
                {
                    telemetry.Catches++;
                    session.ResolveCatch();
                }
                else
                {
                    telemetry.GroundPickups++;
                    var throwArrivalTime = elapsedSinceContact + pickupDuration + throwDuration;
                    while (completedRuns < plannedRuns &&
                        throwArrivalTime >= (completedRuns + 1) * CpuLiveRunningDecisionModel.DefaultRunDurationSeconds)
                    {
                        session.RecordCompletedRun();
                        telemetry.CompletedRuns++;
                        completedRuns++;
                    }
                    if (completedRuns < plannedRuns)
                    {
                        var runners = new BetweenWicketsState();
                        for (var run = 0; run < completedRuns; run++)
                            runners.CompleteRun();
                        runners.StartRun();
                        runners.Advance(throwArrivalTime - completedRuns * CpuLiveRunningDecisionModel.DefaultRunDurationSeconds,
                            CpuLiveRunningDecisionModel.DefaultRunDurationSeconds);
                        if (runners.TryResolveRunOut(WicketEnd.Near, wicketBroken: true, out var runOut))
                        {
                            session.ResolveRunOut(runOut.DismissedEnd, runOut.SwapEnds);
                            telemetry.RunOuts++;
                        }
                    }
                }
                if (session.CompletedRuns >= 2)
                    telemetry.TwoRunScores++;
                match.CompleteDelivery();
                return;
            }

            if (BoundaryResolver.TryFindCrossing(
                previous,
                current,
                delivery.FieldBoundaryRadiusMeters,
                delivery.FieldSurfaceHeightMeters,
                delivery.BallRadiusMeters,
                out var boundary))
            {
                telemetry.Boundaries++;
                var elapsedInCurrentRun = elapsedSinceContact -
                    completedRuns * CpuLiveRunningDecisionModel.DefaultRunDurationSeconds;
                session.ResolveBoundary(
                    boundary.ClearedInTheAir,
                    completedRuns < plannedRuns &&
                    elapsedInCurrentRun >= CpuLiveRunningDecisionModel.DefaultRunDurationSeconds * 0.5f);
                if (session.CompletedRuns >= 2)
                    telemetry.TwoRunScores++;
                match.CompleteDelivery();
                return;
            }

            if (current.Phase == BallMotionPhase.Settled)
            {
                if (completedRuns < plannedRuns)
                {
                    var elapsedInCurrentRun = elapsedSinceContact -
                        completedRuns * CpuLiveRunningDecisionModel.DefaultRunDurationSeconds;
                    var crossed = RunningScoringModel.HasCrossed(
                        elapsedInCurrentRun, CpuLiveRunningDecisionModel.DefaultRunDurationSeconds);
                    session.ResolveDeadBall(crossed);
                    if (crossed)
                    {
                        telemetry.CompletedRuns++;
                        completedRuns++;
                    }
                }
                if (session.CompletedRuns >= 2)
                    telemetry.TwoRunScores++;
                match.CompleteDelivery();
                return;
            }
            previous = current;
        }

        throw new InvalidOperationException("Physics match batted-ball simulation ended without a boundary, fielder contact, or settled ball.");
    }

    private static bool TryCrossPlane(Vector3 previous, Vector3 current, float planeZ, out Vector3 crossing)
    {
        if (previous.Z <= planeZ || current.Z > planeZ || MathF.Abs(current.Z - previous.Z) < 0.000001f)
        {
            crossing = default;
            return false;
        }

        var amount = (planeZ - previous.Z) / (current.Z - previous.Z);
        crossing = Vector3.Lerp(previous, current, Math.Clamp(amount, 0f, 1f));
        return true;
    }

    private static float GetAnimationDuration(PlayerAsset asset, string clipName) =>
        asset.Animations.Find(clip => string.Equals(clip.Name, clipName, StringComparison.OrdinalIgnoreCase))?.DurationSeconds
        ?? throw new InvalidDataException($"Player asset '{asset.Name}' is missing animation clip '{clipName}'.");

    internal sealed class PhysicsMatchTelemetry
    {
        public int ShotPlans { get; set; }
        public int Contacts { get; set; }
        public int Misses { get; set; }
        public int Catches { get; set; }
        public int GroundPickups { get; set; }
        public int Boundaries { get; set; }
        public int RunIntents { get; set; }
        public int SafeRunAttempts { get; set; }
        public int TwoRunPlans { get; set; }
        public int TwoRunScores { get; set; }
        public int CompletedRuns { get; set; }
        public int RunOuts { get; set; }
        public int BowledDismissals { get; set; }
        public int WideDeliveries { get; set; }
        public int NoBallDeliveries { get; set; }
        public int Leaves { get; set; }

        public PhysicsMatchMetrics ToMetrics() => new(
            ShotPlans,
            Contacts,
            Misses,
            Catches,
            GroundPickups,
            Boundaries,
            RunIntents,
            SafeRunAttempts,
            TwoRunPlans,
            TwoRunScores,
            CompletedRuns,
            RunOuts,
            BowledDismissals,
            WideDeliveries,
            NoBallDeliveries,
            Leaves);
    }

    private static void ValidateDelivery(
        DeliveryPreset delivery,
        string label,
        bool expectedNoBall,
        float fieldBoundaryRadiusMeters)
    {
        var errors = delivery.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid {label}: {string.Join(" ", errors)}", nameof(delivery));
        if (delivery.IsNoBall != expectedNoBall)
            throw new ArgumentException($"The {label} must have IsNoBall={expectedNoBall}.", nameof(delivery));
        if (MathF.Abs(delivery.FieldBoundaryRadiusMeters - fieldBoundaryRadiusMeters) > 0.001f)
            throw new ArgumentException($"The {label} boundary radius must match the field preset.", nameof(delivery));
    }
}
