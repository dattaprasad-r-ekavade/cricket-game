using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

/// <summary>Resolves an unbatted incoming ball at the physical near wicket in every match host.</summary>
public static class IncomingDeliveryModel
{
    public static bool TryResolve(
        DeliverySession session, Vector3 previousPosition, Vector3 currentPosition,
        DeliveryPreset delivery, out Vector3 wicketLinePosition, out IncomingDeliveryResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(delivery);
        if (!IsFinite(previousPosition) || !IsFinite(currentPosition))
            throw new ArgumentException("Incoming delivery positions must be finite.");

        wicketLinePosition = default;
        resolution = default;
        var wicketZ = CricketPitchGeometry.NearWicketZ;
        if (previousPosition.Z < wicketZ || currentPosition.Z > wicketZ || currentPosition.Z >= previousPosition.Z)
            return false;

        var fraction = (wicketZ - previousPosition.Z) / (currentPosition.Z - previousPosition.Z);
        wicketLinePosition = Vector3.Lerp(previousPosition, currentPosition, fraction);
        var isWide = CricketDeliveryRuleModel.IsWide(wicketLinePosition.X, delivery.PitchWidthMeters);
        var hitsWickets = WicketContactModel.IntersectsStumps(
            wicketLinePosition, wicketZ, delivery.BallRadiusMeters);
        resolution = session.ResolveIncoming(isWide, hitsWickets);
        return true;
    }

    private static bool IsFinite(Vector3 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z);
}
