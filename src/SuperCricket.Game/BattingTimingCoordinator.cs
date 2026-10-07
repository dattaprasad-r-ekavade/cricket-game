using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SuperCricket.Content;

namespace SuperCricket.Game;

/// <summary>Keeps only the current delivery's measurement visible without blocking the update loop.</summary>
internal sealed class BattingTimingCoordinator : IDisposable
{
    public BattingTimingRequest? Current { get; private set; }

    public void Prepare(Func<CancellationToken, BattingTimingDeliveryProfile> measure, bool runSynchronously = false)
    {
        Clear();
        Current = new BattingTimingRequest(measure, runSynchronously);
    }

    public float? FindIdealInputDelaySeconds(string shotName) =>
        Current?.FindIdealInputDelaySeconds(shotName);

    public void Clear()
    {
        Current?.Cancel();
        Current = null;
    }

    public void Dispose() => Clear();
}

/// <summary>A shot can retain its measurement even after a new delivery replaces the current one.</summary>
internal sealed class BattingTimingRequest
{
    private CancellationTokenSource? _cancellation = new();

    internal Task<BattingTimingDeliveryProfile> Completion { get; }

    public BattingTimingRequest(Func<CancellationToken, BattingTimingDeliveryProfile> measure, bool runSynchronously)
    {
        var token = _cancellation.Token;
        if (runSynchronously)
        {
            try
            {
                Completion = Task.FromResult(measure(token));
            }
            catch
            {
                Cancel();
                throw;
            }
        }
        else
        {
            Completion = Task.Run(() => measure(token), token);
            // Observe failures even when this request has already been superseded.
            _ = Completion.ContinueWith(task =>
                Trace.TraceError("Batting timing calibration failed: {0}", task.Exception),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    public float? FindIdealInputDelaySeconds(string shotName) =>
        Completion.IsCompletedSuccessfully
            ? Completion.Result.FindIdealInputDelaySeconds(shotName)
            : null;

    public void Cancel()
    {
        var cancellation = Interlocked.Exchange(ref _cancellation, null);
        if (cancellation is null)
            return;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
