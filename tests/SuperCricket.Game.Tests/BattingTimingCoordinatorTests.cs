using SuperCricket.Content;

namespace SuperCricket.Game.Tests;

public sealed class BattingTimingCoordinatorTests
{
    [Fact]
    public async Task PendingMeasurementDoesNotBlockOrOfferAnOldTiming()
    {
        using var coordinator = new BattingTimingCoordinator();
        coordinator.Prepare(_ => Profile(0.225f), runSynchronously: true);
        var original = coordinator.Current!;
        using var release = new ManualResetEventSlim();
        coordinator.Prepare(token => { release.Wait(token); return Profile(0.525f); });
        try
        {
            Assert.Null(coordinator.FindIdealInputDelaySeconds("drive"));
            Assert.Equal(0.225f, original.FindIdealInputDelaySeconds("drive"));
        }
        finally { release.Set(); }
        await coordinator.Current!.Completion.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0.525f, coordinator.FindIdealInputDelaySeconds("drive"));
    }

    [Fact]
    public async Task ReplacementCancelsOldMeasurementAndIgnoresItsResult()
    {
        using var coordinator = new BattingTimingCoordinator();
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.Prepare(token => { started.SetResult(); release.Wait(token); return Profile(0.225f); });
        var original = coordinator.Current!;
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        coordinator.Prepare(_ => Profile(0.525f), runSynchronously: true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            original.Completion.WaitAsync(TestContext.Current.CancellationToken));
        Assert.Null(original.FindIdealInputDelaySeconds("drive"));
        Assert.Equal(0.525f, coordinator.FindIdealInputDelaySeconds("drive"));
    }

    [Fact]
    public async Task LateSupersededResultCannotReplaceCurrentProfile()
    {
        using var coordinator = new BattingTimingCoordinator();
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.Prepare(_ => { started.SetResult(); release.Wait(); return Profile(0.225f); });
        var original = coordinator.Current!;
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        coordinator.Prepare(_ => Profile(0.525f), runSynchronously: true);
        release.Set();
        await original.Completion.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0.225f, original.FindIdealInputDelaySeconds("drive"));
        Assert.Equal(0.525f, coordinator.FindIdealInputDelaySeconds("drive"));
    }

    [Fact]
    public async Task FailedMeasurementDoesNotThrowFromTimingLookup()
    {
        using var coordinator = new BattingTimingCoordinator();
        coordinator.Prepare(_ => throw new InvalidDataException("test measurement failure"));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            coordinator.Current!.Completion.WaitAsync(TestContext.Current.CancellationToken));
        Assert.Null(coordinator.FindIdealInputDelaySeconds("drive"));
    }

    [Fact]
    public void SynchronousVerificationStillSurfacesMeasurementErrors()
    {
        using var coordinator = new BattingTimingCoordinator();
        Assert.Throws<InvalidDataException>(() => coordinator.Prepare(
            _ => throw new InvalidDataException("test measurement failure"), runSynchronously: true));
        Assert.Null(coordinator.Current);
    }

    [Fact]
    public async Task ClearingCoordinatorCancelsPendingWorkAndIsRepeatable()
    {
        using var coordinator = new BattingTimingCoordinator();
        using var release = new ManualResetEventSlim();
        coordinator.Prepare(token => { release.Wait(token); return Profile(0.525f); });
        var request = coordinator.Current!;
        coordinator.Clear();
        coordinator.Clear();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            request.Completion.WaitAsync(TestContext.Current.CancellationToken));
        Assert.Null(coordinator.Current);
        Assert.Null(coordinator.FindIdealInputDelaySeconds("drive"));
    }

    private static BattingTimingDeliveryProfile Profile(float delay) => new()
    {
        DeliveryName = "test delivery",
        Shots = [new BattingShotTimingCalibration { ShotName = "drive", IdealInputDelaySeconds = delay }]
    };
}
