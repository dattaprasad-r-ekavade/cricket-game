using System.Numerics;
using SuperCricket.Content;
using SuperCricket.Simulation.Batting;

namespace SuperCricket.Simulation;

public readonly record struct CpuLiveBattingPlan(
    CpuShotChoice Shot,
    float InputDelaySeconds,
    float FootworkOffsetMeters,
    bool AttemptRun,
    float? PredictedContactQuality,
    float HorizontalAim,
    bool Leave = false,
    StrokeResolution? StrokeOutcome = null,
    float? IdealWidthMeters = null);

/// <summary>Chooses a repeatable live shot, timing, footwork, placement, and running intent.</summary>
public static class CpuLiveBattingPlanModel
{
    public const float InputDelayStepSeconds = 0.025f;

    public static CpuLiveBattingPlan Choose(
        TeamPlayerData striker,
        TeamPlayerData bowler,
        IReadOnlyList<TeamPlayerData> fielders,
        BowlingSituation situation,
        FieldingTactic fieldingTactic,
        DeliveryPreset delivery,
        PlayerAsset batterAsset,
        PlayerAsset bowlerAsset,
        BattingShotSet shotSet,
        int seed,
        IReadOnlyList<Vector3>? fieldingPositions = null,
        CpuDifficulty difficulty = CpuDifficulty.Standard,
        StrokeTableAsset? strokeTable = null,
        BatterTimingState? timingState = null)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(batterAsset);
        ArgumentNullException.ThrowIfNull(bowlerAsset);
        ArgumentNullException.ThrowIfNull(shotSet);
        ArgumentNullException.ThrowIfNull(fielders);
        timingState ??= new BatterTimingState();
        var outcome = CpuBattingOutcomeModel.Choose(striker, bowler, fielders, situation, fieldingTactic, difficulty);
        var deliveryErrors = delivery.Validate();
        if (deliveryErrors.Count > 0)
            throw new ArgumentException($"Invalid CPU batting delivery: {string.Join(" ", deliveryErrors)}", nameof(delivery));

        if (CpuLeaveDecisionModel.ShouldLeave(delivery, outcome.Pressure))
        {
            var wicketLineTime = BattingPracticeAnalyzer.GetWicketLineTimeSeconds(delivery);
            return new CpuLiveBattingPlan(
                outcome.Shot,
                MathF.Max(0f, wicketLineTime - 0.1f),
                0f,
                AttemptRun: false,
                PredictedContactQuality: null,
                HorizontalAim: 0f,
                Leave: true);
        }

        StrokeDefinition? selectedStroke = null;
        BallCategory? selectedBall = null;
        if (strokeTable is not null)
        {
            var leftHanded = striker.Handedness == BatterHandedness.Left;
            selectedBall = BallCategoryClassifier.Classify(
                delivery, BattingPracticeAnalyzer.BatterStanceX, leftHanded);
            outcome = CpuBattingOutcomeModel.Choose(
                striker, bowler, fielders, situation, fieldingTactic, difficulty,
                strokeTable, selectedBall, seed ^ unchecked((int)0x6a09e667));
            selectedStroke = outcome.SelectedStroke;
        }

        var shotName = selectedStroke is { } scoredStroke
            ? scoredStroke.Loft ? "loft" : scoredStroke.Intent == StrokeIntent.Defend ? "defence" : "drive"
            : outcome.Shot switch
        {
            CpuShotChoice.Defence => "defence",
            CpuShotChoice.Drive => "drive",
            _ => "loft"
        };
        var samples = BattingPracticeAnalyzer.AnalyzeShot(
            batterAsset,
            bowlerAsset,
            shotSet,
            shotName,
            delivery,
            InputDelayStepSeconds,
            striker);
        var authoredShot = shotSet.Get(shotName);
        var plan = ChooseFromSamples(striker, outcome, delivery, samples, seed, authoredShot.HorizontalAim, difficulty);
        if (fieldingPositions is null)
            return WithStrokeOutcome(plan, striker, delivery, samples, authoredShot.ForwardAim, strokeTable, difficulty, seed, timingState,
                selectedStroke, selectedBall);

        var fieldingRatings = fielders.Select(fielder => fielder.FieldingAttributes.Speed).ToArray();
        var horizontalAim = CpuShotPlacementModel.ChooseHorizontalAim(
            authoredShot,
            striker.Power,
            delivery,
            fieldingPositions,
            fieldingRatings);
        var executedAim = CpuShotPlacementModel.ApplyExecutionError(
            horizontalAim,
            striker.Timing,
            striker.Power,
            seed ^ unchecked((int)0x4cf5ad43),
            difficulty);
        plan = plan with { HorizontalAim = executedAim };
        return WithStrokeOutcome(plan, striker, delivery, samples, authoredShot.ForwardAim, strokeTable, difficulty, seed, timingState,
            selectedStroke, selectedBall);
    }

    private static CpuLiveBattingPlan WithStrokeOutcome(
        CpuLiveBattingPlan plan,
        TeamPlayerData striker,
        DeliveryPreset delivery,
        IReadOnlyList<BattingPracticeSample> samples,
        float forwardAim,
        StrokeTableAsset? strokeTable,
        CpuDifficulty difficulty,
        int seed,
        BatterTimingState timingState,
        StrokeDefinition? selectedStroke,
        BallCategory? selectedBall)
    {
        if (strokeTable is null || plan.Leave)
            return plan;

        var leftHanded = striker.Handedness == BatterHandedness.Left;
        var ball = selectedBall ?? BallCategoryClassifier.Classify(delivery, BattingPracticeAnalyzer.BatterStanceX, leftHanded);
        var direction = selectedStroke?.Direction ?? StrokeDirectionModel.FromAim(plan.HorizontalAim, forwardAim);
        var shot = plan.Shot;
        var request = new StrokeRequest(
            direction,
            selectedStroke?.Intent ?? (shot == CpuShotChoice.Defence ? StrokeIntent.Defend : StrokeIntent.Attack),
            selectedStroke?.Loft ?? (shot == CpuShotChoice.Loft),
            selectedStroke?.Foot ?? StrokeFoot.Auto);
        selectedStroke ??= StrokeSelector.Select(strokeTable, request, ball);
        var profile = CpuDifficultyModel.GetProfile(difficulty, DifficultySide.Cpu);
        if (profile.Cpu is not { } cpuProfile)
            throw new InvalidOperationException($"The {difficulty} CPU profile has no commit-timing settings.");
        var idealWidth = TimingWindowModel.IdealWidth(
            profile, timingState, striker, selectedStroke.Foot, direction, request.Loft);
        var window = TimingWindowModel.Build(
            profile.Timing.CentreMeters,
            profile.Timing.WindowMeters,
            idealWidth);
        var liveSamples = samples
            .Where(sample => sample.InputDelaySeconds >= 0f && sample.InputDelaySeconds < BattingPracticeAnalyzer.GetWicketLineTimeSeconds(delivery))
            .ToArray();
        if (liveSamples.Length > 0)
        {
            var random = new Random(seed ^ unchecked((int)0x165667b1));
            var referenceSpeed = FrameAtInput(delivery, plan.InputDelaySeconds).Velocity.Length();
            var desiredDistance = Math.Clamp(
                window.CentreMeters + SampleStandardNormal(random) * cpuProfile.CommitSigmaSeconds * referenceSpeed,
                window.CentreMeters - window.WindowMeters * 0.5f,
                window.CentreMeters + window.WindowMeters * 0.5f);
            var selectedSample = liveSamples
                .Select(sample => (Sample: sample, Distance: DistanceAtInput(delivery, sample.InputDelaySeconds)))
                .OrderBy(candidate => MathF.Abs(candidate.Distance - desiredDistance))
                .ThenBy(candidate => MathF.Abs(candidate.Sample.InputDelaySeconds - plan.InputDelaySeconds))
                .First().Sample;
            plan = plan with
            {
                InputDelaySeconds = selectedSample.InputDelaySeconds,
                FootworkOffsetMeters = selectedSample.FootworkOffsetMeters,
                PredictedContactQuality = selectedSample.ContactQuality
            };
        }
        var inputDistance = DistanceAtInput(delivery, plan.InputDelaySeconds);
        var outcome = CpuBattingOutcomeModel.ResolveStrokeOutcome(
            shot,
            plan.HorizontalAim,
            forwardAim,
            ball,
            inputDistance,
            window,
            leftHanded,
            profile.Timing.AssistEnabled,
            strokeTable,
            new Random(seed ^ unchecked((int)0x27d4eb2d)),
            selectedStroke);
        return plan with { StrokeOutcome = outcome, IdealWidthMeters = idealWidth };
    }

    private static float DistanceAtInput(DeliveryPreset delivery, float inputDelaySeconds)
    {
        var frame = FrameAtInput(delivery, inputDelaySeconds);
        return TimingWindowModel.DistanceToContactPlane(frame, BattingPracticeAnalyzer.BatterWicketLineZ);
    }

    private static BallFlightFrame FrameAtInput(DeliveryPreset delivery, float inputDelaySeconds)
    {
        var flight = new BallFlightSimulator(delivery);
        while (flight.CurrentFrame.TimeSeconds < inputDelaySeconds && flight.CurrentFrame.Phase != BallMotionPhase.Settled)
            flight.Step();
        return flight.CurrentFrame;
    }

    private static float SampleStandardNormal(Random random)
    {
        var first = Math.Max(1e-12, random.NextDouble());
        var second = random.NextDouble();
        return (float)(Math.Sqrt(-2d * Math.Log(first)) * Math.Cos(2d * Math.PI * second));
    }

    internal static CpuLiveBattingPlan ChooseFromSamples(
        TeamPlayerData striker,
        CpuBattingDecision outcome,
        DeliveryPreset delivery,
        IReadOnlyList<BattingPracticeSample> samples,
        int seed,
        float horizontalAim = 0f,
        CpuDifficulty difficulty = CpuDifficulty.Standard)
    {
        ArgumentNullException.ThrowIfNull(striker);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(samples);
        if (!float.IsFinite(horizontalAim) || horizontalAim is < -1f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(horizontalAim), "Shot direction must be between -1 and 1.");
        var random = new Random(seed);
        var wicketLineTime = BattingPracticeAnalyzer.GetWicketLineTimeSeconds(delivery);
        var liveSamples = samples
            .Where(sample => sample.InputDelaySeconds >= 0f && sample.InputDelaySeconds < wicketLineTime)
            .ToArray();
        if (liveSamples.Length == 0)
            throw new InvalidOperationException($"The delivery '{delivery.Name}' has no live CPU batting input timings.");

        var bestContact = liveSamples
            .Where(sample => sample.ContactQuality.HasValue &&
                sample.ContactTimeSeconds is { } contactTime && contactTime < wicketLineTime)
            .OrderByDescending(sample => sample.ContactQuality)
            .ThenBy(sample => MathF.Abs(sample.FootworkOffsetMeters))
            .FirstOrDefault();
        BattingPracticeSample selectedSample;
        if (bestContact.ContactQuality.HasValue)
        {
            // Lower timing ratings add a repeatable input error around the best physical timing.
            // The selected analyzer sample then predicts the actual contact or miss from that timing.
            var timingSkill = striker.Timing / 100f;
            var maximumTimingError = (1f - timingSkill) * 0.16f *
                CpuDifficultyModel.GetTuning(difficulty).BattingTimingErrorMultiplier;
            var timingError = (float)(random.NextDouble() * 2.0 - 1.0) * maximumTimingError;
            var desiredInputDelay = Math.Clamp(
                bestContact.InputDelaySeconds + timingError,
                liveSamples[0].InputDelaySeconds,
                liveSamples[^1].InputDelaySeconds);
            selectedSample = liveSamples
                .OrderBy(sample => MathF.Abs(sample.InputDelaySeconds - desiredInputDelay))
                .ThenByDescending(sample => sample.ContactQuality)
                .ThenBy(sample => MathF.Abs(sample.FootworkOffsetMeters))
                .First();
        }
        else
        {
            selectedSample = liveSamples
                .OrderBy(sample => MathF.Abs(sample.InputDelaySeconds - 0.25f))
                .ThenBy(sample => MathF.Abs(sample.FootworkOffsetMeters))
                .First();
        }
        var attemptRun = random.NextDouble() < outcome.OneRunChance;
        return new CpuLiveBattingPlan(
            outcome.Shot,
            selectedSample.InputDelaySeconds,
            selectedSample.FootworkOffsetMeters,
            attemptRun,
            selectedSample.ContactQuality,
            horizontalAim);
    }
}
