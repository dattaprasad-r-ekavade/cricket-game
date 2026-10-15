using System.Numerics;
using SuperCricket.Content;
using SuperCricket.Simulation.Events;
using SuperCricket.Simulation.Fielding;
using SuperCricket.Simulation.Umpiring;

namespace SuperCricket.Simulation;

/// <summary>Runs seeded matches through exported swing clips, ball-flight physics, fielding, and match rules.</summary>
public static class PhysicsAutomatedMatchBatchSimulator
{
    public const int MaximumMatchesPerBatch = 100;
    private const float HumanProxyRunOutChanceMultiplier = 1.05f;

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
        int seed,
        StrokeTableAsset? strokeTable = null,
        CpuDifficulty difficulty = CpuDifficulty.Standard,
        DifficultySide battingSide = DifficultySide.Cpu)
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
        strokeTable ??= StrokeTableAsset.Load(ResolveStrokeTablePath());
        if (matchCount is < 1 or > MaximumMatchesPerBatch)
            throw new ArgumentOutOfRangeException(nameof(matchCount), $"Match count must be between 1 and {MaximumMatchesPerBatch}.");
        if (!LimitedOversMatch.SupportedOversPerInnings.Contains(oversPerInnings))
            throw new ArgumentOutOfRangeException(nameof(oversPerInnings), "Choose 1, 2, 5, 10, or 20 overs per innings.");
        if (!Enum.IsDefined(difficulty))
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        if (!Enum.IsDefined(battingSide))
            throw new ArgumentOutOfRangeException(nameof(battingSide));

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
                stockDelivery, wideDelivery, noBallDelivery, oversPerInnings, matchSeed, strokeTable, difficulty, battingSide);
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
        int seed,
        StrokeTableAsset strokeTable,
        CpuDifficulty difficulty,
        DifficultySide battingSide)
    {
        var random = new Random(seed);
        var match = new LimitedOversMatch(firstTeam, secondTeam, oversPerInnings);
        var telemetry = new PhysicsMatchTelemetry();
        var batterTimingStates = new Dictionary<string, BatterTimingState>(StringComparer.Ordinal);
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
                batterTimingStates.Clear();
                deliveriesThisInnings = 0;
                continue;
            }

            if (deliveriesThisInnings >= maximumDeliveriesThisInnings)
                throw new InvalidOperationException($"Physics match seed {seed} exceeded the delivery limit in innings {match.InningsNumber}.");

            var legalBallsBefore = match.LegalBalls;
            var runsBefore = match.Runs;
            SimulateDelivery(
                match, random, fieldPreset, batterAsset, bowlerAsset, shotSet,
                stockDelivery, wideDelivery, noBallDelivery, telemetry, strokeTable, batterTimingStates, difficulty, battingSide);
            telemetry.RecordPhaseRuns(
                legalBallsBefore, oversPerInnings, match.Runs - runsBefore);
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
        PhysicsMatchTelemetry telemetry,
        StrokeTableAsset strokeTable,
        IDictionary<string, BatterTimingState> batterTimingStates,
        CpuDifficulty difficulty,
        DifficultySide battingSide)
    {
        var striker = match.StrikerPlayer;
        var runOutChanceMultiplier = battingSide == DifficultySide.Human
            ? HumanProxyRunOutChanceMultiplier
            : CpuDifficultyModel.GetTuning(difficulty).BattingRunOutChanceMultiplier;
        if (!batterTimingStates.TryGetValue(striker.Id, out var batterTimingState))
            batterTimingStates.Add(striker.Id, batterTimingState = new BatterTimingState());
        var situation = new BowlingSituation(
            match.LegalBalls,
            match.OversPerInnings,
            match.Runs,
            match.Wickets,
            match.Target);
        var formation = FieldPlacementModel.Choose(fieldPreset, situation, striker.Power);
        var tactic = formation.Tactic;
        var fieldingPlayers = match.FieldingPlayers;
        var fieldingRatings = fieldingPlayers.Select(player => player.FieldingAttributes.Speed).ToArray();
        var catchingRatings = fieldingPlayers.Select(player => player.FieldingAttributes.Catching).ToArray();
        var throwingRatings = fieldingPlayers.Select(player => player.FieldingAttributes.Throwing).ToArray();
        var throwingAccuracyRatings = fieldingPlayers.Select(player => player.FieldingAttributes.Accuracy).ToArray();
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
            fielding.Positions,
            difficulty,
            strokeTable,
            batterTimingState,
            battingSide);
        var timingZone = decision.StrokeOutcome?.Zone ?? TimingZone.Early;
        var beaten = decision.StrokeOutcome?.Missed == true;
        if (decision.Leave)
        {
            telemetry.Leaves++;
            match.Events.Publish(new BallBeatBat(0f));
            ResolveMissedDeliveryWithPad(session, delivery, random, telemetry, shotOffered: false, match.Events, difficulty: difficulty);
            RegisterBatterTimingState(session, batterTimingState, timingZone, bodyHit: false, beaten: false);
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
        match.Events.Publish(new StrokePlayed(
            MathF.Max(0f, decision.InputDelaySeconds),
            decision.StrokeOutcome?.StrokeId ?? shotName));
        if (decision.StrokeOutcome is { Missed: true })
        {
            ResolveMissedShot(match, session, delivery, random, telemetry, decision.InputDelaySeconds,
                decision.StrokeOutcome?.Zone, decision.StrokeOutcome?.FootPlayed ?? StrokeFoot.Back, difficulty);
            RegisterBatterTimingState(session, batterTimingState, timingZone, bodyHit: false, beaten: true);
            return;
        }

        BattingPracticeTrajectory trajectory;
        if (decision.StrokeOutcome is { } strokeOutcome)
        {
            if (!TryBuildOutcomeFirstTrajectory(delivery, shotName, decision, strokeOutcome, out trajectory))
            {
                ResolveMissedShot(match, session, delivery, random, telemetry, decision.InputDelaySeconds,
                    decision.StrokeOutcome?.Zone, decision.StrokeOutcome?.FootPlayed ?? StrokeFoot.Back, difficulty);
                RegisterBatterTimingState(session, batterTimingState, timingZone, bodyHit: false, beaten: true);
                return;
            }
        }
        else
        {
            trajectory = BattingPracticeAnalyzer.AnalyzeShotTrajectory(
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
                ResolveMissedShot(match, session, delivery, random, telemetry, decision.InputDelaySeconds,
                    decision.StrokeOutcome?.Zone, decision.StrokeOutcome?.FootPlayed ?? StrokeFoot.Back, difficulty);
                RegisterBatterTimingState(session, batterTimingState, timingZone, bodyHit: false, beaten: true);
                return;
            }
        }

        if (trajectory.ContactPosition is not { } contactPosition ||
            trajectory.OutgoingVelocity is not { } outgoingVelocity ||
            trajectory.OutgoingFrames.Count < 2)
            throw new InvalidOperationException("A physics batting contact did not include its point, velocity, and flight frames.");

        var resolvedVelocity = decision.StrokeOutcome?.OutgoingVelocity ?? outgoingVelocity;
        if (decision.StrokeOutcome?.Edged == true)
            telemetry.Edges++;
        var resolvedSpeed = resolvedVelocity.Length();
        if (resolvedSpeed > 0.001f &&
            MathF.Asin(Math.Clamp(resolvedVelocity.Y / resolvedSpeed, -1f, 1f)) * (180f / MathF.PI) >= 15f)
            telemetry.AerialContacts++;

        var contactTimeSeconds = trajectory.Sample.ContactTimeSeconds ?? 0f;
        if (BowlingAimModel.FindFirstBounce(delivery) is { } firstBounce && firstBounce.TimeSeconds <= contactTimeSeconds)
            match.Events.Publish(new BallPitched(firstBounce.TimeSeconds, firstBounce.Position, firstBounce.BounceCount));
        match.Events.Publish(new BatContact(contactTimeSeconds, contactPosition,
            decision.StrokeOutcome?.Zone.ToString() ?? TimingZone.Ideal.ToString(),
            decision.StrokeOutcome?.Edged == true));

        var pickupDuration = GetAnimationDuration(bowlerAsset, "fielder-pickup");
        var throwDuration = GetAnimationDuration(bowlerAsset, "fielder-throw");
        var catchDuration = GetAnimationDuration(bowlerAsset, "fielder-catch");
        var runDecision = CpuLiveRunningDecisionModel.Choose(
            decision.AttemptRun,
            delivery,
            contactPosition,
            resolvedVelocity,
            fielding.Positions,
            fieldingRatings,
            CpuLiveRunningDecisionModel.DefaultRunDurationSeconds,
            pickupDuration,
            throwDuration,
            catchAnimationDurationSeconds: catchDuration,
            runOutChanceMultiplier: runOutChanceMultiplier);
        var plannedRuns = runDecision.PlannedRuns;
        telemetry.Contacts++;
        if (plannedRuns > 1)
            telemetry.TwoRunPlans++;
        telemetry.SafeRunAttempts += plannedRuns;
        SimulateBattedBall(
            match,
            session,
            delivery,
            trajectory,
            fielding,
            plannedRuns,
            pickupDuration,
            telemetry,
            fieldingRatings,
            random,
            catchDuration,
            catchingRatings,
            throwingRatings,
            throwingAccuracyRatings);
        RegisterBatterTimingState(session, batterTimingState, timingZone, bodyHit: false, beaten: beaten);
        if (session.Extra == DeliveryExtra.Wide)
            telemetry.WideDeliveries++;
        if (session.Extra == DeliveryExtra.NoBall)
            telemetry.NoBallDeliveries++;
    }

    private static void RegisterBatterTimingState(
        DeliverySession session,
        BatterTimingState timingState,
        TimingZone zone,
        bool bodyHit,
        bool beaten)
    {
        if (session.Extra != DeliveryExtra.Wide)
            timingState.RegisterBall(zone, bodyHit, beaten);
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
            bowler.BowlingAttributes.Accuracy,
            striker.Power,
            situation,
            random.Next()).Delivery;
    }

    internal static void ResolveMissedDelivery(
        DeliverySession session,
        DeliveryPreset delivery,
        MatchEventBus? eventBus = null)
    {
        var ball = new BallFlightSimulator(delivery);
        var previous = ball.CurrentFrame;
        var maximumSteps = (int)MathF.Ceiling(delivery.MaximumSimulationSeconds / delivery.FixedTimeStepSeconds) + 1;
        for (var step = 0; step < maximumSteps && previous.Phase != BallMotionPhase.Settled; step++)
        {
            var current = ball.Step();
            PublishBounceEvents(eventBus, previous, current);
            if (IncomingDeliveryModel.TryResolve(session, previous.Position, current.Position, delivery, out _, out _))
                return;
            previous = current;
        }
    }

    internal static void ResolveMissedDeliveryWithPad(
        DeliverySession session,
        DeliveryPreset delivery,
        Random random,
        PhysicsMatchTelemetry? telemetry,
        bool shotOffered,
        MatchEventBus? eventBus = null,
        StrokeFoot foot = StrokeFoot.Back,
        CpuDifficulty difficulty = CpuDifficulty.Standard)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(random);
        var ball = new BallFlightSimulator(delivery);
        var previous = ball.CurrentFrame;
        var firstBounce = BowlingAimModel.FindFirstBounce(delivery);
        var maximumSteps = (int)MathF.Ceiling(delivery.MaximumSimulationSeconds / delivery.FixedTimeStepSeconds) + 1;
        for (var step = 0; step < maximumSteps && previous.Phase != BallMotionPhase.Settled; step++)
        {
            var current = ball.Step();
            PublishBounceEvents(eventBus, previous, current);
            if (!session.IsNoBall && session.Extra != DeliveryExtra.Wide &&
                TryFindPadContact(previous, current, delivery, foot, out var contact))
            {
                eventBus?.Publish(new BodyHit(contact.TimeSeconds, contact.Position));
                eventBus?.Publish(new Appeal(contact.TimeSeconds, DismissalKind.Lbw.ToString()));
                var decision = LbwModel.Evaluate(new LbwContact(
                    contact.Position,
                    contact.Velocity,
                    delivery.BallRadiusMeters,
                    delivery.PitchSurfaceHeightMeters,
                    delivery.GravityMetersPerSecondSquared,
                    delivery.PitchBounceRestitution,
                    HasPitched: contact.BounceCount > 0,
                    PitchedOutsideLeg: firstBounce is { } bounce && bounce.Position.X < -CricketPitchGeometry.WicketHalfWidthMeters,
                    ImpactOutsideOffStump: contact.Position.X > CricketPitchGeometry.WicketHalfWidthMeters,
                    ShotOffered: shotOffered),
                    CpuDifficultyModel.GetProfile(difficulty, DifficultySide.Human),
                    seed: random.Next());
                eventBus?.Publish(new UmpireDecision(
                    contact.TimeSeconds,
                    decision.IsOut,
                    DismissalKind.Lbw.ToString(),
                    float.IsFinite(decision.ProjectedOffsetX) ? decision.ProjectedOffsetX : null,
                    float.IsFinite(decision.ProjectedHeightMeters) ? decision.ProjectedHeightMeters : null,
                    decision.Band.ToString()));
                if (decision.IsOut)
                {
                    session.ResolveLbw();
                    if (telemetry is not null) telemetry.LbwDismissals++;
                }
                return;
            }
            if (IncomingDeliveryModel.TryResolve(session, previous.Position, current.Position, delivery, out _, out _))
                return;
            previous = current;
        }
    }

    internal static bool TryFindPadContact(
        BallFlightFrame previous,
        BallFlightFrame current,
        DeliveryPreset delivery,
        StrokeFoot foot,
        out BallFlightFrame contact)
        => LbwModel.TryFindPadContact(previous, current, delivery, foot, out contact);

    private static void PublishBounceEvents(MatchEventBus? eventBus, BallFlightFrame previous, BallFlightFrame current)
    {
        if (eventBus is not null && current.BounceCount > previous.BounceCount)
            eventBus.Publish(new BallPitched(current.TimeSeconds, current.Position, current.BounceCount));
    }

    private static void ResolveMissedShot(
        LimitedOversMatch match,
        DeliverySession session,
        DeliveryPreset delivery,
        Random random,
        PhysicsMatchTelemetry telemetry,
        float inputDelaySeconds,
        TimingZone? timingZone = null,
        StrokeFoot foot = StrokeFoot.Back,
        CpuDifficulty difficulty = CpuDifficulty.Standard)
    {
        telemetry.Misses++;
        match.Events.Publish(new BallBeatBat(MathF.Max(0f, inputDelaySeconds), timingZone?.ToString()));
        ResolveMissedDeliveryWithPad(session, delivery, random, telemetry, shotOffered: true, match.Events, foot, difficulty);
        match.CompleteDelivery();
        if (session.Dismissal == DismissalKind.Bowled)
            telemetry.BowledDismissals++;
        if (session.Extra == DeliveryExtra.Wide)
            telemetry.WideDeliveries++;
        if (session.Extra == DeliveryExtra.NoBall)
            telemetry.NoBallDeliveries++;
    }

    private static bool TryBuildOutcomeFirstTrajectory(
        DeliveryPreset delivery,
        string shotName,
        CpuLiveBattingPlan decision,
        StrokeResolution outcome,
        out BattingPracticeTrajectory trajectory)
    {
        var simulator = new BallFlightSimulator(delivery);
        var previous = simulator.CurrentFrame;
        BallFlightFrame crossing = default;
        var crossed = false;
        while (simulator.CurrentFrame.Phase != BallMotionPhase.Settled)
        {
            var current = simulator.Step();
            if (ContactPlaneCrossingModel.TryResolve(
                previous,
                current,
                BattingPracticeAnalyzer.BatterWicketLineZ,
                out crossing,
                out _))
            {
                crossed = true;
                break;
            }
            previous = current;
        }

        if (!crossed)
        {
            trajectory = default;
            return false;
        }

        simulator.ApplyBatContact(crossing.Position, outcome.OutgoingVelocity);
        var frames = new List<BallFlightFrame> { simulator.CurrentFrame };
        var maximumSteps = (int)MathF.Ceiling(BattedBallFieldingModel.MaximumCollectionWaitSeconds / delivery.FixedTimeStepSeconds) + 1;
        for (var step = 0; step < maximumSteps && simulator.CurrentFrame.Phase != BallMotionPhase.Settled; step++)
            frames.Add(simulator.Step(enforceSimulationLimit: false));

        var horizontalSpeed = new Vector2(outcome.OutgoingVelocity.X, outcome.OutgoingVelocity.Z).Length();
        var launchAngle = MathF.Atan2(outcome.OutgoingVelocity.Y, horizontalSpeed) * (180f / MathF.PI);
        var contactQuality = outcome.Zone switch
        {
            TimingZone.Ideal => 1f,
            TimingZone.Early or TimingZone.Late => 0.7f,
            TimingZone.Start or TimingZone.End => 0.45f,
            _ => 0.3f
        };
        var sample = new BattingPracticeSample(
            shotName,
            delivery.Name,
            decision.InputDelaySeconds,
            "ResolvedStroke",
            crossing.TimeSeconds,
            contactQuality,
            launchAngle,
            outcome.OutgoingVelocity.Length(),
            0f,
            0f,
            0f,
            decision.FootworkOffsetMeters);
        trajectory = new BattingPracticeTrajectory(
            sample,
            crossing.Position,
            outcome.OutgoingVelocity,
            Array.AsReadOnly(frames.ToArray()));
        return true;
    }

    private static string ResolveStrokeTablePath()
    {
        var roots = new[] { AppContext.BaseDirectory, Environment.CurrentDirectory }
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            {
                foreach (var assetDirectory in new[] { "Assets/Batting", "assets/batting" })
                {
                    var candidate = Path.Combine(directory.FullName, assetDirectory, "strokes.json");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
        }
        throw new FileNotFoundException("Could not locate assets/batting/strokes.json for physics-match stroke outcomes.");
    }

    internal static void SimulateBattedBall(
        LimitedOversMatch match,
        DeliverySession session,
        DeliveryPreset delivery,
        BattingPracticeTrajectory trajectory,
        FieldingSide fielding,
        int plannedRuns,
        float pickupDuration,
        PhysicsMatchTelemetry telemetry,
        IReadOnlyList<int> fieldingRatings,
        Random random,
        float? catchDuration = null,
        IReadOnlyList<int>? catchingRatings = null,
        IReadOnlyList<int>? throwingRatings = null,
        IReadOnlyList<int>? throwingAccuracyRatings = null) =>
        SimulateBattedBallCore(match, session, delivery, trajectory, fielding, plannedRuns,
            pickupDuration, telemetry, fieldingRatings, random, useThrowModel: true,
            legacyThrowDuration: 0f, catchDuration, catchingRatings, throwingRatings, throwingAccuracyRatings);

    // Existing focused fielding tests use fixed animation timings; production batches use ThrowModel.
    internal static void SimulateBattedBall(
        LimitedOversMatch match,
        DeliverySession session,
        DeliveryPreset delivery,
        BattingPracticeTrajectory trajectory,
        FieldingSide fielding,
        int plannedRuns,
        float pickupDuration,
        float throwDuration,
        PhysicsMatchTelemetry telemetry,
        float? catchDuration = null) =>
        SimulateBattedBallCore(match, session, delivery, trajectory, fielding, plannedRuns,
            pickupDuration, telemetry, Enumerable.Repeat(70, FieldingSide.FielderCount).ToArray(),
            new Random(0), useThrowModel: false, throwDuration, catchDuration,
            catchingRatings: Enumerable.Repeat(100, FieldingSide.FielderCount).ToArray());

    private static void SimulateBattedBallCore(
        LimitedOversMatch match,
        DeliverySession session,
        DeliveryPreset delivery,
        BattingPracticeTrajectory trajectory,
        FieldingSide fielding,
        int plannedRuns,
        float pickupDuration,
        PhysicsMatchTelemetry telemetry,
        IReadOnlyList<int> fieldingRatings,
        Random random,
        bool useThrowModel,
        float legacyThrowDuration,
        float? catchDuration,
        IReadOnlyList<int>? catchingRatings = null,
        IReadOnlyList<int>? throwingRatings = null,
        IReadOnlyList<int>? throwingAccuracyRatings = null)
    {
        var frames = trajectory.OutgoingFrames;
        var contactTime = trajectory.Sample.ContactTimeSeconds
            ?? throw new InvalidOperationException("A hit must include a contact time.");
        var completedRuns = 0;
        var previous = frames[0];
        fielding.ConfigureBoundaryRadius(delivery.FieldBoundaryRadiusMeters);
        foreach (var current in BattedBallFieldingModel.FramesThroughCollection(frames, delivery.FixedTimeStepSeconds).Skip(1))
        {
            var deltaTime = current.TimeSeconds - previous.TimeSeconds;
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f)
                throw new InvalidOperationException("Physics match trajectory frames are not in strictly increasing time order.");

            PublishBounceEvents(match.Events, previous, current);

            var elapsedSinceContact = current.TimeSeconds - contactTime;
            while (completedRuns < plannedRuns &&
                elapsedSinceContact >= (completedRuns + 1) * CpuLiveRunningDecisionModel.DefaultRunDurationSeconds)
            {
                session.RecordCompletedRun();
                telemetry.CompletedRuns++;
                completedRuns++;
            }
            if (BattedBallFieldingModel.TryAdvance(fielding, previous, current, deltaTime, out var fieldingContact))
            {
                var collectionAction = BattedBallFieldingModel.ResolveCollection(
                    session,
                    fieldingContact.Kind,
                    completedRuns < plannedRuns,
                    catchingRatings?[fieldingContact.FielderIndex] ?? 100,
                    fieldingContact.Kind == FieldingContactKind.Catch ? random.Next() : 0,
                    fieldingContact.ReactionDistanceMeters,
                    MathF.Max(0f, fieldingContact.Position.Y - delivery.FieldSurfaceHeightMeters));
                if (fieldingContact.Kind == FieldingContactKind.Catch)
                    telemetry.Catches++;
                else
                    telemetry.GroundPickups++;
                if (collectionAction == FieldingCollectionAction.CaughtDismissal)
                    telemetry.CaughtDismissals++;
                if (collectionAction != FieldingCollectionAction.CaughtDismissal)
                {
                    var collectionDuration = fieldingContact.Kind == FieldingContactKind.Catch
                        ? catchDuration ?? pickupDuration : pickupDuration;
                    var targetEnd = WicketEnd.Near;
                    var throwArrivalTime = elapsedSinceContact + collectionDuration;
                    if (useThrowModel && completedRuns < plannedRuns)
                    {
                        targetEnd = RunOutModel.ChooseThreatenedEnd(fieldingContact.Position);
                        var targetZ = targetEnd == WicketEnd.Near ? CricketPitchGeometry.NearWicketZ : CricketPitchGeometry.FarWicketZ;
                        var target = new Vector3(0f, fieldingContact.Position.Y, targetZ);
                        var throwOutcome = ThrowModel.Create(
                            fieldingContact.Position,
                            target,
                            throwingRatings?[fieldingContact.FielderIndex] ?? fieldingRatings[fieldingContact.FielderIndex],
                            throwingAccuracyRatings?[fieldingContact.FielderIndex] ?? fieldingRatings[fieldingContact.FielderIndex],
                            random.Next());
                        throwArrivalTime += throwOutcome.TravelSeconds + throwOutcome.GatherDelaySeconds;
                    }
                    else if (!useThrowModel)
                        throwArrivalTime += legacyThrowDuration;
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
                        var closestCreaseDistance = MathF.Min(runners.Progress, 1f - runners.Progress) * CricketPitchGeometry.PitchLengthMeters;
                        if (useThrowModel)
                        {
                            var runOut = RunOutModel.ResolveAtBrokenWicket(
                                runners, targetEnd, wicketBroken: true, closestCreaseDistance);
                            if (runOut.IsOut)
                            {
                                session.ResolveRunOut(runOut.DismissedEnd, runOut.SwapEnds);
                                telemetry.RunOuts++;
                            }
                            else if (runOut.NearRunOut)
                            {
                                telemetry.NearRunOuts++;
                                match.Events.Publish(new NearRunOut(current.TimeSeconds, closestCreaseDistance));
                            }
                        }
                        else if (runners.TryResolveRunOut(WicketEnd.Near, wicketBroken: true, out var runOut))
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
                    elapsedInCurrentRun >= CpuLiveRunningDecisionModel.DefaultRunDurationSeconds * 0.5f,
                    boundary.Position,
                    current.TimeSeconds);
                if (session.CompletedRuns >= 2)
                    telemetry.TwoRunScores++;
                match.CompleteDelivery();
                return;
            }

            previous = current;
        }

        throw new InvalidOperationException("Physics match batted-ball simulation ended without a boundary or fielder contact.");
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
        public int NearRunOuts { get; set; }
        public int BowledDismissals { get; set; }
        public int LbwDismissals { get; set; }
        public int WideDeliveries { get; set; }
        public int NoBallDeliveries { get; set; }
        public int Leaves { get; set; }
        public int CaughtDismissals { get; set; }
        public int Edges { get; set; }
        public int AerialContacts { get; set; }
        public int PowerplayRuns { get; private set; }
        public int MiddleRuns { get; private set; }
        public int DeathRuns { get; private set; }

        public void RecordPhaseRuns(int legalBallsBefore, int oversPerInnings, int runs)
        {
            var progress = legalBallsBefore / (float)(oversPerInnings * OverScoreboard.BallsPerOver);
            if (progress < 0.30f)
                PowerplayRuns += runs;
            else if (progress < 0.75f)
                MiddleRuns += runs;
            else
                DeathRuns += runs;
        }

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
            Leaves,
            LbwDismissals,
            NearRunOuts,
            CaughtDismissals,
            Edges,
            AerialContacts,
            PowerplayRuns,
            MiddleRuns,
            DeathRuns);
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
