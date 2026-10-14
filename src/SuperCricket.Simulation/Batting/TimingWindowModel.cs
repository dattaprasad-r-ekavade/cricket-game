using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

public readonly record struct TimingZoneBand(
    float MinimumDistanceMeters,
    float MaximumDistanceMeters,
    bool IncludesMinimum,
    bool IncludesMaximum)
{
    public float WidthMeters => MaximumDistanceMeters - MinimumDistanceMeters;

    public bool Contains(float distanceMeters)
    {
        var aboveMinimum = IncludesMinimum ? distanceMeters >= MinimumDistanceMeters : distanceMeters > MinimumDistanceMeters;
        var belowMaximum = IncludesMaximum ? distanceMeters <= MaximumDistanceMeters : distanceMeters < MaximumDistanceMeters;
        return aboveMinimum && belowMaximum;
    }
}

/// <summary>The seven contiguous distance bands around one prepared ball's contact plane.</summary>
public sealed record TimingWindow
{
    internal TimingWindow(
        float centreMeters,
        float windowMeters,
        float idealMeters,
        TimingZoneBand tooEarly,
        TimingZoneBand start,
        TimingZoneBand early,
        TimingZoneBand ideal,
        TimingZoneBand late,
        TimingZoneBand end,
        TimingZoneBand tooLate)
    {
        CentreMeters = centreMeters;
        WindowMeters = windowMeters;
        IdealMeters = idealMeters;
        TooEarly = tooEarly;
        Start = start;
        Early = early;
        Ideal = ideal;
        Late = late;
        End = end;
        TooLate = tooLate;
    }

    public float CentreMeters { get; }
    public float WindowMeters { get; }
    public float IdealMeters { get; }
    public TimingZoneBand TooEarly { get; }
    public TimingZoneBand Start { get; }
    public TimingZoneBand Early { get; }
    public TimingZoneBand Ideal { get; }
    public TimingZoneBand Late { get; }
    public TimingZoneBand End { get; }
    public TimingZoneBand TooLate { get; }

    public TimingZoneBand GetBand(TimingZone zone) => zone switch
    {
        TimingZone.TooLate => TooLate,
        TimingZone.Start => Start,
        TimingZone.Early => Early,
        TimingZone.Ideal => Ideal,
        TimingZone.Late => Late,
        TimingZone.End => End,
        TimingZone.TooEarly => TooEarly,
        _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };
}

public sealed class BatterTimingState
{
    public BatterTimingState(int ballsFaced = 0, float confidence = 0.6f)
    {
        if (ballsFaced < 0)
            throw new ArgumentOutOfRangeException(nameof(ballsFaced));
        if (!float.IsFinite(confidence) || confidence is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        BallsFaced = ballsFaced;
        Confidence = confidence;
    }

    public int BallsFaced { get; private set; }
    public float Confidence { get; private set; }

    public void RegisterBall(TimingZone zone, bool bodyHit, bool beaten)
    {
        if (!Enum.IsDefined(zone))
            throw new ArgumentOutOfRangeException(nameof(zone));

        BallsFaced = checked(BallsFaced + 1);
        var adjustment = zone == TimingZone.Ideal ? 0.05f : 0f;
        if (beaten)
            adjustment -= 0.1f;
        if (bodyHit)
            adjustment -= 0.15f;
        Confidence = Math.Clamp(Confidence + adjustment, 0f, 1f);
    }
}

/// <summary>Distance-based timing classification and adjustment for a batter's ideal zone.</summary>
public static class TimingWindowModel
{
    public static TimingWindow Build(float centre, float window, float ideal)
    {
        if (!float.IsFinite(centre))
            throw new ArgumentOutOfRangeException(nameof(centre));
        if (!float.IsFinite(window) || window <= 0f)
            throw new ArgumentOutOfRangeException(nameof(window));
        if (!float.IsFinite(ideal) || ideal <= 0f || ideal > window)
            throw new ArgumentOutOfRangeException(nameof(ideal), "Ideal width must be positive and no wider than the full timing window.");

        var windowHalf = window / 2f;
        var idealHalf = ideal / 2f;
        var outerBandOffset = (window + ideal) / 4f;
        var tooLateEdge = centre - windowHalf;
        var endEdge = centre - outerBandOffset;
        var idealLow = centre - idealHalf;
        var idealHigh = centre + idealHalf;
        var earlyEdge = centre + outerBandOffset;
        var tooEarlyEdge = centre + windowHalf;

        return new TimingWindow(
            centre,
            window,
            ideal,
            new TimingZoneBand(tooEarlyEdge, float.PositiveInfinity, false, false),
            new TimingZoneBand(earlyEdge, tooEarlyEdge, false, true),
            new TimingZoneBand(idealHigh, earlyEdge, false, true),
            new TimingZoneBand(idealLow, idealHigh, true, true),
            new TimingZoneBand(endEdge, idealLow, true, false),
            new TimingZoneBand(tooLateEdge, endEdge, true, false),
            new TimingZoneBand(float.NegativeInfinity, tooLateEdge, false, false));
    }

    public static TimingZone Classify(TimingWindow window, float distanceMeters)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!float.IsFinite(distanceMeters))
            throw new ArgumentOutOfRangeException(nameof(distanceMeters));

        if (window.TooEarly.Contains(distanceMeters)) return TimingZone.TooEarly;
        if (window.Start.Contains(distanceMeters)) return TimingZone.Start;
        if (window.Early.Contains(distanceMeters)) return TimingZone.Early;
        if (window.Ideal.Contains(distanceMeters)) return TimingZone.Ideal;
        if (window.Late.Contains(distanceMeters)) return TimingZone.Late;
        if (window.End.Contains(distanceMeters)) return TimingZone.End;
        if (window.TooLate.Contains(distanceMeters)) return TimingZone.TooLate;
        throw new InvalidOperationException("Timing window bands do not cover the supplied distance.");
    }

    public static float IdealWidth(TimingDifficulty profile, BatterTimingState state, bool loft, float strokeRating)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(state);
        if (!float.IsFinite(strokeRating) || strokeRating is < 0f or > 100f)
            throw new ArgumentOutOfRangeException(nameof(strokeRating));
        var profileErrors = profile.Validate();
        if (profileErrors.Count > 0)
            throw new ArgumentException($"Invalid timing difficulty profile: {string.Join(" ", profileErrors)}", nameof(profile));

        var modeFactor = loft ? 1f - profile.LoftReduction : 1f;
        var settleFactor = SettleFraction(profile.SettleCurve, state.BallsFaced);
        var confidenceFactor = 0.8f + 0.2f * state.Confidence;
        var skillBonus = Math.Clamp((strokeRating - 80f) / 200f, -0.1f, 0.1f) * profile.IdealBase;
        skillBonus = Math.Clamp(skillBonus, -0.2f * profile.IdealBase, 0.2f * profile.IdealBase);
        var width = profile.IdealBase * modeFactor * settleFactor * confidenceFactor + skillBonus;
        return MathF.Max(profile.IdealMinimum, width);
    }

    public static float IdealWidth(
        TimingDifficulty profile,
        BatterTimingState state,
        TeamPlayerData batter,
        StrokeFoot foot,
        ShotDirection8 direction,
        bool loft)
    {
        ArgumentNullException.ThrowIfNull(batter);
        return IdealWidth(profile, state, loft,
            PlayerBattingRatingModel.GetStrokeRating(batter, foot, direction, loft));
    }

    public static TimingZone ApplyAssist(TimingZone zone, bool assist)
    {
        if (!Enum.IsDefined(zone))
            throw new ArgumentOutOfRangeException(nameof(zone));
        return assist && zone is TimingZone.Early or TimingZone.Late
            ? TimingZone.Ideal
            : zone;
    }

    public static float DistanceToContactPlane(BallFlightFrame frame, float contactPlaneZ)
    {
        if (!float.IsFinite(contactPlaneZ) ||
            !IsFinite(frame.Position) || !IsFinite(frame.Velocity) || MathF.Abs(frame.Velocity.Z) < 0.000001f)
            throw new ArgumentException("A finite contact plane and a moving ball with depth velocity are required.", nameof(frame));

        var speed = frame.Velocity.Length();
        if (!float.IsFinite(speed) || speed <= 0.000001f)
            throw new ArgumentException("A finite, non-zero ball velocity is required.", nameof(frame));

        var timeToPlane = (contactPlaneZ - frame.Position.Z) / frame.Velocity.Z;
        return timeToPlane * speed;
    }

    private static float SettleFraction(IReadOnlyList<TimingSettlePoint> curve, int ballsFaced)
    {
        if (ballsFaced <= curve[0].BallsFaced)
            return curve[0].Fraction;
        for (var index = 1; index < curve.Count; index++)
        {
            var next = curve[index];
            if (ballsFaced > next.BallsFaced)
                continue;
            var previous = curve[index - 1];
            var progress = (ballsFaced - previous.BallsFaced) / (float)(next.BallsFaced - previous.BallsFaced);
            return previous.Fraction + (next.Fraction - previous.Fraction) * progress;
        }
        return curve[^1].Fraction;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
