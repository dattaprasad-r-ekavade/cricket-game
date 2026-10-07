using SuperCricket.Simulation;

namespace SuperCricket.Game;

public partial class Game1
{
    private readonly BattingTimingCoordinator _battingTiming = new();
    private BattingTimingRequest? _shotBattingTimingRequest;
    private float? _shotIdealInputDelaySeconds;

    private void PrepareBattingTiming()
    {
        CancelBattingTiming();
        if (IsCpuBattingControlled)
            return;

        var delivery = _deliveryPreset.DeepCopy();
        var batter = _playerAsset;
        var bowler = _bowlerAsset;
        var shots = _shotSet;
        var footwork = _targetBatterFootworkOffsetX;
        _battingTiming.Prepare(token => BattingPracticeAnalyzer.CalibrateTiming(
            batter, bowler, shots, delivery, footwork, cancellationToken: token),
            runSynchronously: _verifyGameplay || _captureTarget is not null);
    }

    private float? FindCurrentIdealInputDelaySeconds(string shotName) =>
        _battingTiming.FindIdealInputDelaySeconds(shotName);

    private void RecordShotTiming(string shotName)
    {
        _shotIdealInputDelaySeconds = _footworkTransitionActive
            ? null
            : FindCurrentIdealInputDelaySeconds(shotName);
        _shotBattingTimingRequest = _footworkTransitionActive ? null : _battingTiming.Current;
    }

    private float? FindShotIdealInputDelaySeconds(string shotName)
    {
        _shotIdealInputDelaySeconds ??= _shotBattingTimingRequest?.FindIdealInputDelaySeconds(shotName);
        return _shotIdealInputDelaySeconds;
    }

    private void CancelBattingTiming() => _battingTiming.Clear();
}
