using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

public static class CpuLiveRunningDecisionReviewChecks
{
    public static void Run(DeliveryPreset delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var contact = new Vector3(0f, 0.4f, BattingPracticeAnalyzer.BatterWicketLineZ);
        var farFielders = Enumerable.Range(0, FieldingSide.FielderCount)
            .Select(index => new Vector3(34f + index * 0.1f, delivery.FieldSurfaceHeightMeters, 28f))
            .ToArray();
        var ratings = Enumerable.Repeat(70, FieldingSide.FielderCount).ToArray();
        var outgoingVelocity = new Vector3(0f, 6f, -20f);

        var noIntent = CpuLiveRunningDecisionModel.Choose(
            false, delivery, contact, outgoingVelocity, farFielders, ratings);
        Require(!noIntent.AttemptRun && noIntent.Reason == CpuLiveRunDecisionReason.NoIntent,
            "a batter without run intent started a run");

        var clearGap = CpuLiveRunningDecisionModel.Choose(
            true, delivery, contact, outgoingVelocity, farFielders, ratings);
        Require(clearGap.PlannedRuns is >= 1 and <= CpuLiveRunningDecisionModel.MaximumPlannedRuns &&
            clearGap.Reason is CpuLiveRunDecisionReason.SafeRunWindow or CpuLiveRunDecisionReason.SafeRunAtStoppage,
            "a safe shot into a clear gap did not permit a run");

        var longOutfield = delivery.DeepCopy();
        longOutfield.FieldBoundaryRadiusMeters = 100f;
        var twoRuns = CpuLiveRunningDecisionModel.Choose(
            true, longOutfield, contact, new Vector3(0f, 6f, -20f), farFielders, ratings);
        Require(twoRuns.PlannedRuns == 2 &&
            twoRuns.Reason is CpuLiveRunDecisionReason.SafeRunWindow or CpuLiveRunDecisionReason.SafeRunAtStoppage,
            "a long, uncollected ball did not allow two runs");

        var closeFielders = (Vector3[])farFielders.Clone();
        closeFielders[0] = new Vector3(0f, delivery.FieldSurfaceHeightMeters, contact.Z - 0.2f);
        var quickPickup = CpuLiveRunningDecisionModel.Choose(
            true, delivery, contact, outgoingVelocity, closeFielders, ratings);
        Require(quickPickup.PlannedRuns == 0 && quickPickup.Reason == CpuLiveRunDecisionReason.FielderCanCollectBeforeRun,
            "a nearby fielder did not suppress a risky run");

        var boundaryDelivery = delivery.DeepCopy();
        boundaryDelivery.FieldBoundaryRadiusMeters = 15f;
        var boundaryShot = CpuLiveRunningDecisionModel.Choose(
            true, boundaryDelivery, contact, new Vector3(26f, 5f, 0f), farFielders, ratings);
        Require(boundaryShot.PlannedRuns == 0 && boundaryShot.Reason == CpuLiveRunDecisionReason.BoundaryBeforeRun,
            "a shot reaching the boundary during the run window still started a run");

        Require(clearGap == CpuLiveRunningDecisionModel.Choose(
                true, delivery, contact, outgoingVelocity, farFielders, ratings),
            "identical ball-flight and fielding inputs produced different run decisions");

        Console.WriteLine("PASS: CPU running decisions safely plan singles or doubles from the struck ball, fielders, pickup/throw timing, and boundary.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"CPU live running check failed: {message}");
    }
}
