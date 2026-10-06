using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

public readonly record struct BattingPracticeSample(
    string ShotName,
    string DeliveryName,
    float InputDelaySeconds,
    string Outcome,
    float? ContactTimeSeconds,
    float? ContactQuality,
    float? LaunchAngleDegrees,
    float? OutgoingSpeedMetersPerSecond,
    float? BatPointSpeedMetersPerSecond,
    float? SweetSpotOffsetX,
    float? SweetSpotOffsetY,
    float FootworkOffsetMeters = 0f);

public readonly record struct BattingPracticeTrajectory(
    BattingPracticeSample Sample,
    Vector3? ContactPosition,
    Vector3? OutgoingVelocity,
    IReadOnlyList<BallFlightFrame> OutgoingFrames);

/// <summary>Replays actual exported swing clips against a delivery while sweeping the shot input time.</summary>
public static class BattingPracticeAnalyzer
{
    public const float MinimumInputDelaySeconds = -0.5f;
    public const float BatterWicketLineZ = -8.72f;
    private const float BatterZ = BatterWicketLineZ;
    private const float BatterX = -0.48f;
    private const float BatterGroundOffset = -0.025f;
    private const float MissPlaneOffsetMeters = 0.45f;
    private const float ShotTransitionSeconds = 0.12f;

    public static IReadOnlyList<BattingPracticeSample> Analyze(
        PlayerAsset batter,
        PlayerAsset bowler,
        BattingShotSet shotSet,
        DeliveryPreset delivery,
        float inputDelayStepSeconds = 0.025f,
        TeamPlayerData? battingRatings = null)
    {
        ArgumentNullException.ThrowIfNull(batter);
        ArgumentNullException.ThrowIfNull(bowler);
        ArgumentNullException.ThrowIfNull(shotSet);
        ArgumentNullException.ThrowIfNull(delivery);
        if (!float.IsFinite(inputDelayStepSeconds) || inputDelayStepSeconds is < 0.01f or > 0.25f)
            throw new ArgumentOutOfRangeException(nameof(inputDelayStepSeconds), "Input-delay step must be between 0.01 and 0.25 seconds.");
        ValidateBattingRatings(battingRatings);

        var batterSampler = new BatSampler(batter);
        var stance = FindClip(batter, "practice-stance");
        var bowlerRunUp = FindClip(bowler, "bowling-run-up");
        var bowlerDelivery = FindClip(bowler, "overarm-delivery");
        var releaseEvent = bowlerDelivery.Events.Find(animationEvent =>
            string.Equals(animationEvent.Name, "ball-release", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Bowler overarm-delivery clip is missing its ball-release event.");
        var stanceTimeAtRelease = bowlerRunUp.DurationSeconds + releaseEvent.TimeSeconds;
        var incomingDuration = GetBallApproachDuration(delivery);
        var results = new List<BattingPracticeSample>();

        foreach (var shot in shotSet.Shots)
        {
            var clip = FindClip(batter, shot.AnimationClip);
            for (var inputDelay = MinimumInputDelaySeconds;
                 inputDelay <= incomingDuration + inputDelayStepSeconds * 0.25f;
                 inputDelay += inputDelayStepSeconds)
            {
                var candidates = BatterFootwork.PracticeOffsets.Select(footworkOffset => SimulateOne(
                    batterSampler,
                    stance,
                    clip,
                    stanceTimeAtRelease,
                    shot,
                    shotSet.ContactPaddingMeters,
                    delivery,
                    inputDelay,
                    footworkOffset,
                    battingRatings));
                results.Add(candidates
                    .OrderByDescending(sample => sample.ContactQuality ?? -1f)
                    .ThenBy(sample => MathF.Abs(sample.FootworkOffsetMeters))
                    .First());
            }
        }

        return results;
    }

    public static IReadOnlyList<BattingPracticeSample> AnalyzeShot(
        PlayerAsset batter,
        PlayerAsset bowler,
        BattingShotSet shotSet,
        string shotName,
        DeliveryPreset delivery,
        float inputDelayStepSeconds = 0.025f,
        TeamPlayerData? battingRatings = null)
    {
        ArgumentNullException.ThrowIfNull(shotSet);
        if (string.IsNullOrWhiteSpace(shotName))
            throw new ArgumentException("A shot name must not be empty.", nameof(shotName));

        var shot = shotSet.Get(shotName);
        var singleShot = new BattingShotSet
        {
            ContactPaddingMeters = shotSet.ContactPaddingMeters,
            Shots = [shot]
        };
        return Analyze(batter, bowler, singleShot, delivery, inputDelayStepSeconds, battingRatings);
    }

    public static float GetWicketLineTimeSeconds(DeliveryPreset delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var ball = new BallFlightSimulator(delivery);
        var previous = ball.CurrentFrame;
        var maximumSteps = (int)MathF.Ceiling(delivery.MaximumSimulationSeconds / delivery.FixedTimeStepSeconds) + 1;
        for (var step = 0; step < maximumSteps && previous.Phase != BallMotionPhase.Settled; step++)
        {
            var frame = ball.Step();
            if (previous.Position.Z > BatterWicketLineZ && frame.Position.Z <= BatterWicketLineZ)
            {
                var fraction = Math.Clamp(
                    (previous.Position.Z - BatterWicketLineZ) / (previous.Position.Z - frame.Position.Z),
                    0f,
                    1f);
                return previous.TimeSeconds + (frame.TimeSeconds - previous.TimeSeconds) * fraction;
            }
            if (frame.Position.Z <= BatterWicketLineZ)
                return frame.TimeSeconds;
            previous = frame;
        }
        return previous.TimeSeconds;
    }

    public static bool TryGetWicketLinePosition(DeliveryPreset delivery, out Vector3 position)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var ball = new BallFlightSimulator(delivery);
        var previous = ball.CurrentFrame;
        var maximumSteps = (int)MathF.Ceiling(delivery.MaximumSimulationSeconds / delivery.FixedTimeStepSeconds) + 1;
        for (var step = 0; step < maximumSteps && previous.Phase != BallMotionPhase.Settled; step++)
        {
            var frame = ball.Step();
            if (previous.Position.Z > BatterWicketLineZ && frame.Position.Z <= BatterWicketLineZ)
            {
                var fraction = Math.Clamp(
                    (previous.Position.Z - BatterWicketLineZ) / (previous.Position.Z - frame.Position.Z),
                    0f,
                    1f);
                position = Vector3.Lerp(previous.Position, frame.Position, fraction);
                return true;
            }
            if (frame.Position.Z <= BatterWicketLineZ)
            {
                position = frame.Position;
                return true;
            }
            previous = frame;
        }

        position = default;
        return false;
    }

    public static BattingPracticeTrajectory AnalyzeShotTrajectory(
        PlayerAsset batter,
        PlayerAsset bowler,
        BattingShotSet shotSet,
        string shotName,
        DeliveryPreset delivery,
        float inputDelaySeconds,
        float footworkOffsetMeters,
        TeamPlayerData? battingRatings = null,
        float? horizontalAimOverride = null)
    {
        ArgumentNullException.ThrowIfNull(batter);
        ArgumentNullException.ThrowIfNull(bowler);
        ArgumentNullException.ThrowIfNull(shotSet);
        ArgumentNullException.ThrowIfNull(delivery);
        if (string.IsNullOrWhiteSpace(shotName))
            throw new ArgumentException("A shot name must not be empty.", nameof(shotName));
        if (!float.IsFinite(inputDelaySeconds) || inputDelaySeconds < MinimumInputDelaySeconds)
            throw new ArgumentOutOfRangeException(nameof(inputDelaySeconds), "Input delay must be finite and at least -0.5 seconds.");
        if (!float.IsFinite(footworkOffsetMeters) || MathF.Abs(footworkOffsetMeters) > BatterFootwork.MaximumOffsetMeters)
            throw new ArgumentOutOfRangeException(nameof(footworkOffsetMeters), "Footwork offset exceeds the supported movement range.");
        if (horizontalAimOverride is { } horizontalAim &&
            (!float.IsFinite(horizontalAim) || horizontalAim is < -1f or > 1f))
            throw new ArgumentOutOfRangeException(nameof(horizontalAimOverride), "Shot direction must be between -1 and 1.");
        ValidateBattingRatings(battingRatings);

        var authoredShot = shotSet.Get(shotName);
        var shot = horizontalAimOverride is { } aim
            ? new BattingShotData
            {
                Name = authoredShot.Name,
                AnimationClip = authoredShot.AnimationClip,
                LaunchAngleDegrees = authoredShot.LaunchAngleDegrees,
                HorizontalAim = aim,
                SpeedTransfer = authoredShot.SpeedTransfer
            }
            : authoredShot;
        var batterSampler = new BatSampler(batter);
        var stance = FindClip(batter, "practice-stance");
        var bowlerRunUp = FindClip(bowler, "bowling-run-up");
        var bowlerDelivery = FindClip(bowler, "overarm-delivery");
        var releaseEvent = bowlerDelivery.Events.Find(animationEvent =>
            string.Equals(animationEvent.Name, "ball-release", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Bowler overarm-delivery clip is missing its ball-release event.");
        return SimulateOneDetailed(
            batterSampler,
            stance,
            FindClip(batter, shot.AnimationClip),
            bowlerRunUp.DurationSeconds + releaseEvent.TimeSeconds,
            shot,
            shotSet.ContactPaddingMeters,
            delivery,
            inputDelaySeconds,
            footworkOffsetMeters,
            battingRatings,
            captureOutgoingFrames: true);
    }

    private static BattingPracticeSample SimulateOne(
        BatSampler batterSampler,
        PlayerAnimationData stance,
        PlayerAnimationData shotClip,
        float stanceTimeAtRelease,
        BattingShotData shot,
        float contactPaddingMeters,
        DeliveryPreset delivery,
        float inputDelaySeconds,
        float footworkOffsetMeters,
        TeamPlayerData? battingRatings) => SimulateOneDetailed(
            batterSampler,
            stance,
            shotClip,
            stanceTimeAtRelease,
            shot,
            contactPaddingMeters,
            delivery,
            inputDelaySeconds,
            footworkOffsetMeters,
            battingRatings,
            captureOutgoingFrames: false).Sample;

    private static BattingPracticeTrajectory SimulateOneDetailed(
        BatSampler batterSampler,
        PlayerAnimationData stance,
        PlayerAnimationData shotClip,
        float stanceTimeAtRelease,
        BattingShotData shot,
        float contactPaddingMeters,
        DeliveryPreset delivery,
        float inputDelaySeconds,
        float footworkOffsetMeters,
        TeamPlayerData? battingRatings,
        bool captureOutgoingFrames)
    {
        var ball = new BallFlightSimulator(delivery);
        var previousFrame = ball.CurrentFrame;
        var maximumSteps = (int)MathF.Ceiling(delivery.MaximumSimulationSeconds / delivery.FixedTimeStepSeconds) + 1;
        var requiredExpansion = delivery.BallRadiusMeters + contactPaddingMeters;

        for (var step = 0; step < maximumSteps && previousFrame.Phase != BallMotionPhase.Settled; step++)
        {
            var currentFrame = ball.Step();
            // Input can land between fixed ticks. Only sweep the portion after it.
            var contactStartTime = MathF.Max(previousFrame.TimeSeconds, inputDelaySeconds);
            var contactDeltaSeconds = currentFrame.TimeSeconds - contactStartTime;
            var startFraction = Math.Clamp((contactStartTime - previousFrame.TimeSeconds) /
                (currentFrame.TimeSeconds - previousFrame.TimeSeconds), 0f, 1f);
            var contactStartPosition = Vector3.Lerp(previousFrame.Position, currentFrame.Position, startFraction);
            var batStart = batterSampler.GetBatWorld(
                stance,
                shotClip,
                stanceTimeAtRelease + contactStartTime,
                contactStartTime - inputDelaySeconds,
                footworkOffsetMeters);
            var batEnd = batterSampler.GetBatWorld(
                stance,
                shotClip,
                stanceTimeAtRelease + currentFrame.TimeSeconds,
                currentFrame.TimeSeconds - inputDelaySeconds,
                footworkOffsetMeters);

            if (contactDeltaSeconds > 0f && SweptBattingContactResolver.TryResolve(
                contactStartPosition,
                currentFrame.Position,
                batStart,
                batEnd,
                batterSampler.BladeMinimum,
                batterSampler.BladeMaximum,
                requiredExpansion,
                contactDeltaSeconds,
                out var contact))
            {
                var impact = BattingImpactModel.Calculate(
                    currentFrame.Velocity,
                    contact.BatPointVelocity,
                    contact.NormalizedSweetSpotOffset,
                    shot,
                    battingRatings);
                ball.ApplyBatContact(contact.Position, impact.OutgoingVelocity);
                var outgoingFrames = captureOutgoingFrames ? new List<BallFlightFrame> { ball.CurrentFrame } : null;
                var outcome = SimulateOutgoingBall(ball, delivery, outgoingFrames);
                var contactTime = contactStartTime + contactDeltaSeconds * contact.HitFraction;
                var sample = new BattingPracticeSample(
                    shot.Name,
                    delivery.Name,
                    inputDelaySeconds,
                    outcome,
                    contactTime,
                    impact.ContactQuality,
                    impact.LaunchAngleDegrees,
                    impact.OutgoingVelocity.Length(),
                    contact.BatPointVelocity.Length(),
                    contact.NormalizedSweetSpotOffset.X,
                    contact.NormalizedSweetSpotOffset.Y,
                    footworkOffsetMeters);
                return new BattingPracticeTrajectory(
                    sample,
                    captureOutgoingFrames ? contact.Position : null,
                    captureOutgoingFrames ? impact.OutgoingVelocity : null,
                    outgoingFrames is null ? Array.Empty<BallFlightFrame>() : Array.AsReadOnly(outgoingFrames.ToArray()));
            }

            if (currentFrame.Position.Z <= BatterZ - MissPlaneOffsetMeters)
                return MissTrajectory(shot, delivery, inputDelaySeconds, "MissedBat", footworkOffsetMeters);

            previousFrame = currentFrame;
        }

        return MissTrajectory(shot, delivery, inputDelaySeconds, "SettledBeforeContact", footworkOffsetMeters);
    }

    private static BattingPracticeTrajectory MissTrajectory(
        BattingShotData shot,
        DeliveryPreset delivery,
        float inputDelaySeconds,
        string outcome,
        float footworkOffsetMeters) => new(
            Miss(shot, delivery, inputDelaySeconds, outcome, footworkOffsetMeters),
            null,
            null,
            Array.Empty<BallFlightFrame>());

    private static string SimulateOutgoingBall(
        BallFlightSimulator ball,
        DeliveryPreset delivery,
        List<BallFlightFrame>? trajectory = null)
    {
        var maximumSteps = (int)MathF.Ceiling(delivery.MaximumSimulationSeconds / delivery.FixedTimeStepSeconds) + 1;
        var previousFrame = ball.CurrentFrame;
        for (var step = 0; step < maximumSteps && ball.CurrentFrame.Phase != BallMotionPhase.Settled; step++)
        {
            var frame = ball.Step();
            trajectory?.Add(frame);
            if (BoundaryResolver.TryFindCrossing(
                previousFrame,
                frame,
                delivery.FieldBoundaryRadiusMeters,
                delivery.FieldSurfaceHeightMeters,
                delivery.BallRadiusMeters,
                out var crossing))
                return crossing.ClearedInTheAir ? "Six" : "Four";
            previousFrame = frame;
        }

        return "InPlay";
    }

    private static float GetBallApproachDuration(DeliveryPreset delivery)
    {
        var ball = new BallFlightSimulator(delivery);
        var maximumSteps = (int)MathF.Ceiling(delivery.MaximumSimulationSeconds / delivery.FixedTimeStepSeconds) + 1;
        for (var step = 0; step < maximumSteps && ball.CurrentFrame.Phase != BallMotionPhase.Settled; step++)
        {
            var frame = ball.Step();
            if (frame.Position.Z <= BatterZ - MissPlaneOffsetMeters)
                return frame.TimeSeconds;
        }

        return ball.CurrentFrame.TimeSeconds;
    }

    private static void ValidateBattingRatings(TeamPlayerData? battingRatings)
    {
        if (battingRatings is { Timing: < 0 or > 100 } or { Power: < 0 or > 100 })
            throw new ArgumentOutOfRangeException(nameof(battingRatings), "Batter timing and power ratings must be between 0 and 100.");
    }

    private static BattingPracticeSample Miss(
        BattingShotData shot,
        DeliveryPreset delivery,
        float inputDelaySeconds,
        string outcome,
        float footworkOffsetMeters) => new(
            shot.Name,
            delivery.Name,
            inputDelaySeconds,
            outcome,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            footworkOffsetMeters);

    private static PlayerAnimationData FindClip(PlayerAsset asset, string clipName) =>
        asset.Animations.Find(clip => string.Equals(clip.Name, clipName, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidDataException($"Player asset '{asset.Name}' is missing animation clip '{clipName}'.");

    private sealed class BatSampler
    {
        private readonly int _batBoneIndex;
        private readonly Matrix4x4[] _inverseBindMatrices;
        public BatSampler(PlayerAsset asset)
        {
            _batBoneIndex = asset.Bones.FindIndex(bone =>
                string.Equals(bone.Name, "forearm.R", StringComparison.OrdinalIgnoreCase));
            if (_batBoneIndex < 0)
                throw new InvalidDataException($"Player asset '{asset.Name}' is missing forearm.R for its bat attachment.");

            var blade = asset.Meshes.Find(mesh => string.Equals(mesh.Name, "Bat Blade", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Player asset '{asset.Name}' is missing its Bat Blade mesh.");
            (BladeMinimum, BladeMaximum) = FindBounds(blade.Positions);
            _inverseBindMatrices = new Matrix4x4[asset.Bones.Count];
            for (var index = 0; index < asset.Bones.Count; index++)
            {
                if (!Matrix4x4.Invert(asset.Bones[index].BindPose.ToNumericsMatrix(), out _inverseBindMatrices[index]))
                    throw new InvalidDataException($"Player asset '{asset.Name}' has a non-invertible bind pose at bone {index}.");
            }

        }

        public Vector3 BladeMinimum { get; }
        public Vector3 BladeMaximum { get; }

        public Matrix4x4 GetBatWorld(
            PlayerAnimationData stance,
            PlayerAnimationData shot,
            float stanceTimeSeconds,
            float shotAgeSeconds,
            float footworkOffsetMeters)
        {
            TransformData pose;
            if (shotAgeSeconds < 0f)
            {
                pose = SamplePose(stance, stanceTimeSeconds, _batBoneIndex);
            }
            else
            {
                var shotPose = SamplePose(shot, shotAgeSeconds, _batBoneIndex);
                if (shotAgeSeconds < ShotTransitionSeconds)
                {
                    var stancePose = SamplePose(stance, stanceTimeSeconds, _batBoneIndex);
                    pose = TransformData.Interpolate(stancePose, shotPose, shotAgeSeconds / ShotTransitionSeconds);
                }
                else
                {
                    pose = shotPose;
                }
            }

            var batterWorld = Matrix4x4.CreateTranslation(BatterX + footworkOffsetMeters, BatterGroundOffset, BatterZ);
            return _inverseBindMatrices[_batBoneIndex] * pose.ToNumericsMatrix() * batterWorld;
        }

        private static TransformData SamplePose(PlayerAnimationData clip, float timeSeconds, int boneIndex)
        {
            var samples = clip.Samples;
            var time = timeSeconds % clip.DurationSeconds;
            if (time < 0f)
                time += clip.DurationSeconds;
            if (time <= samples[0].TimeSeconds)
                return samples[0].Bones[boneIndex];

            for (var index = 1; index < samples.Count; index++)
            {
                var next = samples[index];
                if (time > next.TimeSeconds)
                    continue;

                var previous = samples[index - 1];
                var span = next.TimeSeconds - previous.TimeSeconds;
                var amount = span <= 0f ? 0f : (time - previous.TimeSeconds) / span;
                return TransformData.Interpolate(previous.Bones[boneIndex], next.Bones[boneIndex], amount);
            }

            return samples[^1].Bones[boneIndex];
        }

        private static (Vector3 Minimum, Vector3 Maximum) FindBounds(float[] positions)
        {
            var minimum = new Vector3(float.PositiveInfinity);
            var maximum = new Vector3(float.NegativeInfinity);
            for (var offset = 0; offset < positions.Length; offset += 3)
            {
                var point = new Vector3(positions[offset], positions[offset + 1], positions[offset + 2]);
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }
            return (minimum, maximum);
        }
    }
}
