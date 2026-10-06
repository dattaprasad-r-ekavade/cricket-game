using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

/// <summary>Chooses a shot lane by comparing fielder reach time with ball arrival along candidate directions.</summary>
public static class CpuShotPlacementModel
{
    private static readonly float[] ProbeDistancesMeters = [6f, 12f, 18f, 24f, 30f];

    public static float ChooseHorizontalAim(
        BattingShotData shot,
        int batterPower,
        DeliveryPreset delivery,
        IReadOnlyList<Vector3> fielderPositions,
        IReadOnlyList<int> fieldingRatings)
    {
        ValidateInputs(shot, batterPower, delivery, fielderPositions, fieldingRatings);
        var candidates = Enumerable.Range(0, 9)
            .Select(index => -1f + index * 0.25f)
            .Append(shot.HorizontalAim)
            .Distinct()
            .Select(aim => new
            {
                Aim = aim,
                Score = EvaluateGapScore(shot, batterPower, delivery, fielderPositions, fieldingRatings, aim)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => MathF.Abs(candidate.Aim - shot.HorizontalAim))
            .ThenBy(candidate => candidate.Aim)
            .First();
        return candidates.Aim;
    }

    public static float EvaluateGapScore(
        BattingShotData shot,
        int batterPower,
        DeliveryPreset delivery,
        IReadOnlyList<Vector3> fielderPositions,
        IReadOnlyList<int> fieldingRatings,
        float horizontalAim)
    {
        ValidateInputs(shot, batterPower, delivery, fielderPositions, fieldingRatings);
        if (!float.IsFinite(horizontalAim) || horizontalAim is < -1f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(horizontalAim), "Shot direction must be between -1 and 1.");

        var direction = Vector3.Normalize(new Vector3(horizontalAim, 0f, -1f));
        var speed = MathF.Max(
            5f,
            delivery.StartVelocity.Length() * shot.SpeedTransfer * (0.8f + batterPower * 0.004f));
        var contact = new Vector3(-0.48f, delivery.FieldSurfaceHeightMeters, -BattingPracticeAnalyzer.BatterWicketLineZ);
        var minimumArrivalMargin = float.PositiveInfinity;
        for (var probeIndex = 0; probeIndex < ProbeDistancesMeters.Length; probeIndex++)
        {
            var distance = ProbeDistancesMeters[probeIndex];
            var target = contact + direction * distance;
            var earliestReachTime = float.PositiveInfinity;
            for (var fielderIndex = 0; fielderIndex < fielderPositions.Count; fielderIndex++)
            {
                earliestReachTime = MathF.Min(
                    earliestReachTime,
                    FieldingSide.EstimateReachTime(
                        fielderPositions[fielderIndex],
                        target,
                        fieldingRatings[fielderIndex]));
            }

            var arrivalMargin = earliestReachTime - distance / speed;
            minimumArrivalMargin = MathF.Min(minimumArrivalMargin, arrivalMargin);
        }

        // A small preference keeps the authored direction when two lanes offer similar space.
        return minimumArrivalMargin - MathF.Abs(horizontalAim - shot.HorizontalAim) * 0.05f;
    }

    public static float ApplyExecutionError(
        float intendedAim,
        int batterTiming,
        int batterPower,
        int seed,
        CpuDifficulty difficulty = CpuDifficulty.Standard)
    {
        if (!float.IsFinite(intendedAim) || intendedAim is < -1f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(intendedAim), "Shot direction must be between -1 and 1.");
        if (batterTiming is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(batterTiming), "Batter timing must be between 0 and 100.");
        if (batterPower is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(batterPower), "Batter power must be between 0 and 100.");

        var placementSkill = (batterTiming * 0.65f + batterPower * 0.35f) / 100f;
        var maximumAimError = (0.45f + (1f - placementSkill) * 0.90f) *
            CpuDifficultyModel.GetTuning(difficulty).ShotPlacementErrorMultiplier;
        var random = new Random(seed);
        var aimError = ((float)random.NextDouble() * 2f - 1f) * maximumAimError;
        return Math.Clamp(intendedAim + aimError, -1f, 1f);
    }

    private static void ValidateInputs(
        BattingShotData shot,
        int batterPower,
        DeliveryPreset delivery,
        IReadOnlyList<Vector3> fielderPositions,
        IReadOnlyList<int> fieldingRatings)
    {
        ArgumentNullException.ThrowIfNull(shot);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(fielderPositions);
        ArgumentNullException.ThrowIfNull(fieldingRatings);
        if (batterPower is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(batterPower), "Batter power must be between 0 and 100.");
        if (!float.IsFinite(shot.HorizontalAim) || shot.HorizontalAim is < -1f or > 1f ||
            !float.IsFinite(shot.SpeedTransfer) || shot.SpeedTransfer <= 0f)
            throw new ArgumentException("The batting shot must have a valid direction and positive speed transfer.", nameof(shot));
        if (fielderPositions.Count != FieldingSide.FielderCount || fieldingRatings.Count != FieldingSide.FielderCount)
            throw new ArgumentException($"Shot placement requires exactly {FieldingSide.FielderCount} fielder positions and ratings.");
        for (var index = 0; index < fieldingRatings.Count; index++)
        {
            if (fieldingRatings[index] is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(fieldingRatings), "Fielding ratings must be between 0 and 100.");
            var position = fielderPositions[index];
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
                throw new ArgumentException($"Fielder position {index + 1} must be finite.", nameof(fielderPositions));
        }
        var errors = delivery.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid shot-placement delivery: {string.Join(" ", errors)}", nameof(delivery));
    }
}
