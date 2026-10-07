using SuperCricket.Content;

namespace SuperCricket.Simulation;

public static partial class BattingPracticeAnalyzer
{
    /// <summary>Measures the best contact input for this prepared delivery at the batter's current stance.</summary>
    public static BattingTimingDeliveryProfile CalibrateTiming(
        PlayerAsset batter,
        PlayerAsset bowler,
        BattingShotSet shotSet,
        DeliveryPreset delivery,
        float footworkOffsetMeters = 0f,
        float inputDelayStepSeconds = 0.025f,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batter);
        ArgumentNullException.ThrowIfNull(bowler);
        ArgumentNullException.ThrowIfNull(shotSet);
        ArgumentNullException.ThrowIfNull(delivery);
        if (!float.IsFinite(footworkOffsetMeters) || MathF.Abs(footworkOffsetMeters) > BatterFootwork.MaximumOffsetMeters)
            throw new ArgumentOutOfRangeException(nameof(footworkOffsetMeters));
        if (!float.IsFinite(inputDelayStepSeconds) || inputDelayStepSeconds is < 0.01f or > 0.25f)
            throw new ArgumentOutOfRangeException(nameof(inputDelayStepSeconds));

        var sampler = new BatSampler(batter);
        var stance = FindClip(batter, "practice-stance");
        var runUp = FindClip(bowler, "bowling-run-up");
        var bowlingClip = FindClip(bowler, "overarm-delivery");
        var release = bowlingClip.Events.Find(animationEvent =>
            string.Equals(animationEvent.Name, "ball-release", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Bowler overarm-delivery clip is missing its ball-release event.");
        var stanceTimeAtRelease = runUp.DurationSeconds + release.TimeSeconds;
        var incomingDuration = GetBallApproachDuration(delivery);
        var profile = new BattingTimingDeliveryProfile { DeliveryName = delivery.Name };

        foreach (var shot in shotSet.Shots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clip = FindClip(batter, shot.AnimationClip);
            BattingPracticeSample? best = null;
            for (var delay = MinimumInputDelaySeconds;
                 delay <= incomingDuration + inputDelayStepSeconds * 0.25f;
                 delay += inputDelayStepSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sampler.ClearPoseCache();
                var candidate = SimulateOneDetailed(
                    sampler, stance, clip, stanceTimeAtRelease, shot,
                    shotSet.ContactPaddingMeters, delivery, delay, footworkOffsetMeters,
                    battingRatings: null, captureOutgoingFrames: false, resolveOutgoingOutcome: false).Sample;
                if (candidate.ContactQuality is { } quality &&
                    (best is null || quality > best.Value.ContactQuality!.Value))
                    best = candidate;
            }

            // An unreachable delivery must not acquire a plausible-looking timing prompt.
            if (best is { } contact)
                profile.Shots.Add(new BattingShotTimingCalibration
                {
                    ShotName = shot.Name,
                    IdealInputDelaySeconds = contact.InputDelaySeconds
                });
        }

        return profile;
    }
}
