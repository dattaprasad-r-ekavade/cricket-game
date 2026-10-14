using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation.Batting;

/// <summary>Resolves an authored stroke outcome from the player's timing input.</summary>
public static class StrokeOutcomeResolver
{
    public static StrokeResolution Resolve(
        StrokeTableAsset table,
        StrokeRequest request,
        BallCategory ball,
        float distanceAtInputMeters,
        TimingWindow window,
        bool leftHanded,
        bool assist,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(random);
        if (!float.IsFinite(distanceAtInputMeters))
            throw new ArgumentOutOfRangeException(nameof(distanceAtInputMeters));

        var stroke = StrokeSelector.Select(table, request, ball);
        var zone = TimingWindowModel.ApplyAssist(
            TimingWindowModel.Classify(window, distanceAtInputMeters),
            assist);
        if (zone is TimingZone.TooEarly or TimingZone.TooLate ||
            !stroke.Zones.TryGetValue(zone, out var outcome) || outcome.BeatenOrMissed)
        {
            return new StrokeResolution(
                stroke.Id,
                zone,
                distanceAtInputMeters,
                Vector3.Zero,
                Edged: false,
                Missed: true,
                stroke.Foot);
        }

        var yawDegrees = Sample(random, outcome.YawDegrees - outcome.YawSpreadDegrees, outcome.YawDegrees + outcome.YawSpreadDegrees);
        var speed = Sample(random, outcome.MinSpeedMetersPerSecond, outcome.MaxSpeedMetersPerSecond);
        var elevationDegrees = outcome.ElevationDegrees;
        var edged = random.NextDouble() < outcome.EdgeChance;
        if (edged)
        {
            yawDegrees += random.NextDouble() < 0.5 ? -15f : 15f;
            speed *= 0.5f;
            elevationDegrees = Sample(random, 5f, 20f);
        }

        if (leftHanded)
            yawDegrees = -yawDegrees;

        var yawRadians = yawDegrees * (MathF.PI / 180f);
        var elevationRadians = elevationDegrees * (MathF.PI / 180f);
        var horizontalSpeed = speed * MathF.Cos(elevationRadians);
        var outgoingVelocity = new Vector3(
            MathF.Sin(yawRadians) * horizontalSpeed,
            MathF.Sin(elevationRadians) * speed,
            MathF.Cos(yawRadians) * horizontalSpeed);

        return new StrokeResolution(
            stroke.Id,
            zone,
            distanceAtInputMeters,
            outgoingVelocity,
            edged,
            Missed: false,
            stroke.Foot);
    }

    private static float Sample(Random random, float minimum, float maximum) =>
        minimum + (float)random.NextDouble() * (maximum - minimum);
}
